"""Object records (domain objects) in PEData.plf.

Layout (all little endian, offsets from record start):
  +0   u32 record length (incl. trailing 0xFF)
  +4   u16 typeLo, u16 typeHi, u64 object id          (record key)
  +16  u32 ?  (1..11)            +20 u32 ? (2, 3 or 11)
  +24  u16 0  +26 u8 slot count  +27 u8 ? (1 or 2)
  +28  16-byte object GUID
  +44  u32[slot count] slot table: absolute offset of the slot's block, 0 = empty / default
  ...  blocks, then 0xFF
Deleted object: a new version of the record (49 bytes) with u32 @16 = 0xFFFFFFFF, u32 @24 = 4, zero GUID and no
slots ("tombstone"); see is_tombstone.
Slot order: the S AttributeSets implemented by the type (incl. inherited), sorted by declaration order in the
metamodel XML, followed by link slots:
  slot S:        tagged link block (relation id per link)
  slots S+1..:   u32 @20 == 3 (standard): exactly one more tagged link block (usually the *-relations)
                 u32 @20 == 2 / 11 (extended): more slots, each a tagged block or a typed link list of one
                 relation (relation implicit by slot; mapping slot -> relation not yet known);
                 slot count varies per object
Attribute set block:  u32 length (incl. itself), payload (length may be < span: padding)
Expando set block:    u32 used length, u32 allocated length, payload (AttributeSets with expando="true"),
                      see attrs.decode_expando
Tagged link block:    u16 allocated size (incl. 4-byte head), u16 count, link16 entries, zero padding;
                      entries with relation 0 are holes and not counted
  link16:             u32 relation id, u16 typeLo, u16 typeHi, u64 object id of the target
Typed link list:      u32 allocated size, u32 ? (0x7fffffff or small), u32 count, count x link12, zero padding
  link12:             u16 typeLo, u16 typeHi, u64 object id (relation = the slot's relation)
"""
import struct

LINK_SIZE = 16


class Link:
    __slots__ = ("relation", "type_id", "oid", "pos", "slot")

    def __init__(self, relation, type_id, oid, pos, slot=None):
        self.relation, self.type_id, self.oid, self.pos, self.slot = relation, type_id, oid, pos, slot

    @property
    def key(self):
        return "%04x:%04x:%x" % (self.type_id & 0xFFFF, self.type_id >> 16, self.oid)


def parse_link(d, o):
    rel, lo, hi, oid = struct.unpack_from("<IHHQ", d, o)
    return Link(rel, (hi << 16) | lo, oid, o)


def read_link_block(d, o):
    """Entries with relation id 0 are holes (e.g. removed links) and do not count."""
    size, count = struct.unpack_from("<HH", d, o)
    links, p = [], o + 4
    while len(links) < count and p + LINK_SIZE <= min(o + size, len(d)):
        if struct.unpack_from("<I", d, p)[0] != 0:
            links.append(parse_link(d, p))
        p += LINK_SIZE
    return size, links


def read_typed_list(d, o, slot):
    size, x, count = struct.unpack_from("<III", d, o)
    links = []
    if 12 + count * 12 > min(size, len(d) - o):
        return size, x, None
    for i in range(count):
        lo, hi, oid = struct.unpack_from("<HHQ", d, o + 12 + i * 12)
        links.append(Link(None, (hi << 16) | lo, oid, o + 12 + i * 12, slot))
    return size, x, links


def is_tombstone(rec):
    return struct.unpack_from("<I", rec.data, 16)[0] == 0xFFFFFFFF


class ObjectRecord:
    """Decoded slot structure of one domain record."""

    def __init__(self, rec, mm):
        self.rec = rec
        d = self.data = rec.data
        self.flags16, self.flags20 = struct.unpack_from("<II", d, 16)
        self.slot_count, self.b27 = d[26], d[27]
        self.guid = d[28:44]
        n = self.slot_count
        self.slots = list(struct.unpack_from("<%dI" % n, d, 44))
        self.table_end = 44 + 4 * n
        self.mm = mm
        self.set_names = mm.attribute_sets_ordered(rec.type_id) if mm else []
        self.n_sets = len(self.set_names)
        self.layout_ok = (self.n_sets + 2 == n) if self.flags20 == 3 else (self.n_sets + 1 < n)
        self.errors = []
        self.blocks = {}       # slot index -> (start, end)
        self.links = []         # tagged (relation known) and typed (relation None, slot known)
        self._blocks()

    def _blocks(self):
        d, n = self.data, self.slot_count
        end_all = len(d) - 1
        used = sorted((o, i) for i, o in enumerate(self.slots) if o)
        for k, (o, i) in enumerate(used):
            nxt = used[k + 1][0] if k + 1 < len(used) else end_all
            if o < self.table_end or o > end_all:
                self.errors.append("slot %d offset %d outside record" % (i, o))
                continue
            self.blocks[i] = (o, nxt)
            if self.layout_ok and i >= self.n_sets:
                size16, cnt16 = struct.unpack_from("<HH", d, o)
                if i == self.n_sets or (cnt16 > 0 and o + size16 == nxt and 4 + cnt16 * LINK_SIZE <= size16):
                    size, links = read_link_block(d, o)
                    hl = 4 + len(links) * LINK_SIZE
                    if len(links) < cnt16:
                        self.errors.append("link slot %d: %d of %d links found" % (i, len(links), cnt16))
                else:
                    size, _, links = read_typed_list(d, o, i)
                    if links is None:
                        self.errors.append("link slot %d: neither tagged block nor typed list" % i)
                        continue
                    hl = 12 + len(links) * 12
                if o + size != nxt:
                    self.errors.append("link slot %d size %d != span %d" % (i, size, nxt - o))
                if hl > size:
                    self.errors.append("link slot %d count %d exceeds size %d" % (i, len(links), size))
                for l in links:
                    l.slot = i
                self.links.extend(links)
            else:
                (ln,) = struct.unpack_from("<I", d, o)
                if self.is_expando(i):
                    (alloc,) = struct.unpack_from("<I", d, o + 4)
                    if o + alloc != nxt or ln > alloc:
                        self.errors.append("expando slot %d used %d alloc %d span %d" % (i, ln, alloc, nxt - o))
                elif o + ln > nxt or ln < 4:
                    self.errors.append("slot %d (%s) length %d != span %d" % (i, self.set_name(i), ln, nxt - o))
        if used and used[0][0] != self.table_end:
            self.errors.append("first block at %d, table ends at %d" % (used[0][0], self.table_end))

    def set_name(self, i):
        if not self.layout_ok:
            return "?slot%d" % i
        if i >= self.n_sets:
            return "<links %d>" % (i - self.n_sets)
        return self.set_names[i].split(".")[-1]

    def is_expando(self, i):
        if not self.layout_ok or i >= self.n_sets:
            return False
        t = self.mm.by_name.get(self.set_names[i]) if self.mm else None
        return bool(t and t.expando)

    def payload(self, i):
        """Bytes of a set block after its length head, or None if the slot is empty."""
        if i not in self.blocks:
            return None
        o, e = self.blocks[i]
        if self.is_expando(i):
            return self.data[o + 8:o + struct.unpack_from("<I", self.data, o)[0]]
        return self.data[o + 4:o + struct.unpack_from("<I", self.data, o)[0]]

    def payload_by_set(self, short_name):
        for i in range(self.n_sets if self.layout_ok else 0):
            if self.set_name(i) == short_name:
                return self.payload(i)
        return None
