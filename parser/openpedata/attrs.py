"""Decoding of AttributeSet blocks in object records.

Block = u32 length (from block start, incl. this field), then the stored attributes of the set (see
Metamodel.stored_attributes) in ordinal order:
  fixed-size types inline (bool/byte 1, short 2, int/uint/float 4, long/ulong/double/dateTime 8, GuidT 16,
  Enumeration: size of its base type, default xs:int)
  all other types as u32 offset relative to the block start (0 = null); the value at the offset is
  a 7-bit varint holding the total value size incl. the varint itself (0 = null), then the bytes; strings UTF-8.
  Structure and Array* values instead start with a u32 LE total size incl. itself, like a block (H-L2-1); they are
  returned with this head (offsets inside the value are relative to its start), see decode_structure.
"""
import datetime
import struct

FIXED = {
    "xs:boolean": 1, "xs:unsignedByte": 1, "xs:byte": 1,
    "xs:short": 2, "xs:ushort": 2,
    "xs:int": 4, "xs:uint": 4, "xs:float": 4,
    "xs:long": 8, "xs:ulong": 8, "xs:double": 8, "xs:dateTime": 8,
    "pe:GuidT": 16,
}
FMT = {"xs:boolean": "<?", "xs:unsignedByte": "<B", "xs:byte": "<b", "xs:short": "<h", "xs:ushort": "<H",
       "xs:int": "<i", "xs:uint": "<I", "xs:float": "<f", "xs:long": "<q", "xs:ulong": "<Q", "xs:double": "<d"}


def type_size(mm, typ):
    if typ in FIXED:
        return FIXED[typ]
    t = mm.resolve(typ)
    if t is not None and t.kind == "Enumeration":
        return FIXED.get(t.base_type or "xs:int", 4)
    return None  # variable: stored by offset


def varint(d, o):
    v = shift = 0
    while True:
        b = d[o]
        o += 1
        v |= (b & 0x7F) << shift
        shift += 7
        if not b & 0x80:
            return v, o


def read_var(block, off):
    """(bytes or None, end offset) of a length-prefixed value at block offset off. The varint holds the total
    size of the value including the varint itself (so a 1-byte prefix reads as length + 1); 0 = null."""
    n, o = varint(block, off)
    if n == 0:
        return None, o
    end = off + n
    return bytes(block[o:end]), end


def is_structured(mm, typ):
    """Structure and Array types: their values carry a u32 total length head instead of a varint (H-L2-1)."""
    t = mm.resolve(typ)
    return t is not None and t.kind in ("Structure", "Array")


def read_struct(block, off, end):
    """(bytes incl. the u32 length head, end offset) of a Structure / Array value at block offset off, or None if
    the u32 length does not fit up to end. Values of 128 bytes and more are only readable this way; the varint
    of read_var takes the first length byte (>= 0x80) as a continuation byte (e.g. LibParameter in 030, 036)."""
    if off + 4 > end:
        return None
    (n,) = struct.unpack_from("<I", block, off)
    if n < 4 or off + n > end:
        return None
    return bytes(block[off:off + n]), off + n


def _struct_at(buf, off):
    """Slice of a nested Structure / Array value (with u32 head) at off, or None."""
    r = read_struct(buf, off, len(buf)) if off else None
    return r[0] if r else None


def decode_structure(mm, typ, raw, _depth=0):
    """Decode a Structure / Array value as returned by decode_set (bytes with u32 length head).
    Structure = like a block: u32 length, its Elements in declaration order, fixed-size types inline, other types
    as u32 offset relative to the element start (0 = null): strings with varint size, nested Structure / Array
    values with u32 length head. Array = u32 length, u32 count, count x u32 offset of the item (relative to the
    value start), the items (L2: ArrayLibraryClipboardCoreObject); items of a fixed-size type are packed inline
    after the count instead (CoreArrayInt16/Int32/Boolean). Returns a dict (Structure) or list (Array);
    values that do not fit stay bytes. Confirmed on all Structure / Array values of the samples
    (analysis/L4_instances/struct_scan_out.txt)."""
    t = mm.resolve(typ) if isinstance(typ, str) else typ
    if t is None or not isinstance(raw, (bytes, bytearray)) or len(raw) < 4 or _depth > 16:
        return raw
    try:
        if t.kind == "Array":
            (cnt,) = struct.unpack_from("<I", raw, 4)
            isz = type_size(mm, t.item_type)
            if isz is not None:  # fixed-size items (CoreArrayInt16/Int32/Boolean) are packed inline, no offsets
                if 8 + isz * cnt != len(raw):
                    return raw
                return [decode_value(mm, t.item_type, raw[8 + i * isz:8 + (i + 1) * isz]) for i in range(cnt)]
            if 8 + 4 * cnt > len(raw):
                return raw
            items = []
            for off in struct.unpack_from("<%dI" % cnt, raw, 8):
                items.append(_element_value(mm, t.item_type, raw, off, _depth))
            return items
        if t.kind != "Structure":
            return raw
        (ln,) = struct.unpack_from("<I", raw, 0)
        out, o = {}, 4
        for name, et in t.elements:
            if et == "pe:CharT":  # UTF-16 code unit inline (StructValueElement.Separator, 100_hmi_device)
                out[name] = chr(struct.unpack_from("<H", raw, o)[0])
                o += 2
                continue
            sz = type_size(mm, et)
            if sz is None:
                if o + 4 > ln:
                    return raw
                (off,) = struct.unpack_from("<I", raw, o)
                out[name] = _element_value(mm, et, raw, off, _depth) if off else None
                o += 4
            else:
                if o + sz > ln:
                    return raw
                out[name] = decode_value(mm, et, raw[o:o + sz])
                o += sz
        return out
    except (struct.error, IndexError, ValueError):
        return raw


def _element_value(mm, typ, buf, off, depth):
    if is_structured(mm, typ):
        v = _struct_at(buf, off)
        return decode_structure(mm, typ, v, depth + 1) if v is not None else None
    sz = type_size(mm, typ)
    if sz is not None:
        return decode_value(mm, typ, buf[off:off + sz])
    v, _ = read_var(buf, off)
    if typ == "xs:string" and v is not None:
        return v.decode("utf-8", errors="replace")
    return v


def ticks_to_iso(v):
    kind = v >> 62
    t = v & 0x3FFFFFFFFFFFFFFF
    try:
        dt = datetime.datetime(1, 1, 1) + datetime.timedelta(microseconds=t // 10)
    except OverflowError:
        return None
    return dt.isoformat() + ("Z" if kind == 1 else "")


def decode_value(mm, typ, raw):
    if typ in FMT:
        return struct.unpack(FMT[typ], raw)[0]
    if typ == "xs:dateTime":
        return ticks_to_iso(struct.unpack("<q", raw)[0])
    if typ == "pe:GuidT":
        import uuid
        return str(uuid.UUID(bytes_le=bytes(raw)))
    t = mm.resolve(typ)
    if t is not None and t.kind == "Enumeration":
        v = struct.unpack(FMT.get(t.base_type or "xs:int", "<i"), raw)[0]
        return t.constants.get(str(v), v)
    return raw


def decode_set(mm, tid, set_full, block):
    """Decode one AttributeSet block (starting at its u32 length).
    Returns (values dict name -> value, list of problems)."""
    attrs = mm.stored_attributes(tid, set_full)
    (ln,) = struct.unpack_from("<I", block, 0)
    vals, problems, var = {}, [], []
    o = 4
    for ordinal, name, typ, _ in attrs:
        sz = type_size(mm, typ)
        if sz is None:
            if o + 4 > ln:
                problems.append("fixed part overruns block at %s" % name)
                return vals, problems
            (off,) = struct.unpack_from("<I", block, o)
            var.append((off, name, typ))
            o += 4
        else:
            if o + sz > ln:
                problems.append("fixed part overruns block at %s" % name)
                return vals, problems
            vals[name] = decode_value(mm, typ, block[o:o + sz])
            o += sz
    fixed_end = o
    pos = fixed_end
    gap = False
    for off, name, typ in sorted(var):
        if off == 0:
            vals[name] = None
            continue
        if off < pos or off >= ln:
            problems.append("var %s at %d, expected %d" % (name, off, pos))
            continue
        gap |= off > pos  # nested data of a previous value lies in between
        r = read_struct(block, off, ln) if is_structured(mm, typ) else None
        if r is not None:
            vals[name], pos = r
            continue
        raw, pos = read_var(block, off)
        if typ == "xs:string" and raw is not None:
            try:
                vals[name] = raw.decode("utf-8")
            except UnicodeDecodeError:
                vals[name] = raw
        else:
            vals[name] = raw
    if (pos < ln or gap) and not problems:
        # top level consistent; the rest belongs to nested values (arrays/structures with own offsets)
        problems.append("nested: block length %d, top level decoded up to %d" % (ln, pos))
    elif pos > ln and not problems:
        problems.append("block length %d, decoded up to %d" % (ln, pos))
    return vals, problems


# --- expando attributes -------------------------------------------------------------------------------
# Name dictionary per object type: store record type 0x00070011, +44 u32 len, u32 len, u32 object type id,
# u32 count, u32 max id, count x (u32 id, string (7-bit length + UTF-8), u32 type code | 0x80000000).
# Expando block: u32 used, u32 allocated, u32 count, u16 ?, u32 object type id (dictionary), count x u32 id,
# count x u32 slot, values up to "used". A slot holds the value itself for bool/byte/int types (INLINE_CODES),
# else the offset of the value relative to the value area that follows the slots (length = up to next value).
EXPANDO_CODES = {1: "bool", 2: "byte", 4: "uint", 5: "int", 6: "long", 7: "float", 8: "double", 0x12: "int", 0xA: "dateTime", 0xB: "string", 0xC: "guid",
                 0xD: "blob", 0xE: "xml", 0x13: "int?", 0x14: "long?", 0x1F: "text"}


FIXED_SIZES = {"bool": 1, "byte": 1, "int": 4, "int?": 4, "long": 8, "long?": 8, "double": 8, "dateTime": 8, "guid": 16}
INLINE_CODES = {1, 2, 4, 5, 7, 0x12, 0x13}  # 0x12: int stored inline (ILScreenData property 0x0507 = screen number, DemoProject_BestPractices_V4); 7: float32 bits (HmiScreenItem MaximumValue, CenterSize; Lib\Sys\Monitoring-and-control-objects); 4: only seen as InterfaceVersionRootData.ValuesUndoVersion (value 1)


def expando_inline(code, v):
    if code == 1:
        return v != 0
    if code in (5, 0x12, 0x13):
        return struct.unpack("<i", struct.pack("<I", v))[0]
    if code == 7:
        return struct.unpack("<f", struct.pack("<I", v))[0]
    return v


def read_dictionary(data):
    """{id: (name, code)} from a 0x00070011 store record; plus the object type id."""
    _, _, tid, cnt, _ = struct.unpack_from("<IIIII", data, 44)
    o, res = 64, {}
    for _ in range(cnt):
        (eid,) = struct.unpack_from("<I", data, o)
        n, o = varint(data, o + 4)
        name = data[o:o + n].decode("utf-8", errors="replace")
        o += n
        (code,) = struct.unpack_from("<I", data, o)
        o += 4
        res[eid] = (name, code & 0x7FFFFFFF)
    return tid, res


def decode_expando(block, dictionaries):
    """Decode an expando block (starting at its u32 used length). Returns (values, problems)."""
    if len(block) < 8:
        # H-023: short form without the alloc field (e.g. 06 00 00 00 01 00 in IAdditionalAttributes of
        # ReportStorageLocationData) carries no entries
        return {}, []
    used, alloc = struct.unpack_from("<II", block, 0)
    if used <= 8:
        return {}, []
    (cnt,) = struct.unpack_from("<I", block, 8)
    (tid,) = struct.unpack_from("<I", block, 14)
    o = 18
    if o + 8 * cnt > used:
        return {}, ["expando count %d does not fit used %d" % (cnt, used)]
    ids = struct.unpack_from("<%dI" % cnt, block, o)
    offs = struct.unpack_from("<%dI" % cnt, block, o + 4 * cnt)
    base = o + 8 * cnt
    names = dictionaries.get(tid, {})
    vals, problems = {}, []
    out = []  # (offset, id) of values stored in the value area
    for i in range(cnt):
        name, code = names.get(ids[i], ("#%d" % ids[i], None))
        if code in INLINE_CODES:
            vals[name] = expando_inline(code, offs[i])
        else:
            out.append((offs[i], i))
    out.sort()
    for j, (off, i) in enumerate(out):
        a = base + off
        e = base + out[j + 1][0] if j + 1 < len(out) else used
        name, code = names.get(ids[i], ("#%d" % ids[i], None))
        if not base <= a <= e <= used:
            problems.append("expando value %s out of range" % name)
            continue
        raw = bytes(block[a:e])
        size = FIXED_SIZES.get(EXPANDO_CODES.get(code))
        if size and len(raw) > size:  # free space after a value (a rewritten value moved to the end, sample 238)
            raw = raw[:size]
        vals[name] = expando_value(code, raw)
    return vals, problems


def decode_simple_expando(block):
    """Expando set with expando="simple" (seen: ICommentsExpandoAttributeSet, member comments of block
    interfaces): u32 used, u32 allocated, u32 count, u16 ?, u32 offset of the value table, count x u32 type code
    (| 0x80000000), count x u32 name offset, names UTF-16LE NUL-terminated, value table count x u32 offset
    relative to the end of the table, each value a varint total size (incl. itself) + bytes.
    Code 0x1F = pe:CoreTextAttributeT (multilingual text). All offsets relative to the block start.
    Returns ({name: value}, problems); text values stay raw bytes (model.core_text decodes them)."""
    used, _alloc, cnt = struct.unpack_from("<III", block, 0)
    if used <= 12 or cnt == 0:
        return {}, []
    (vtab,) = struct.unpack_from("<I", block, 14)
    if 18 + 8 * cnt > used or vtab + 4 * cnt > used:
        return {}, ["simple expando count %d does not fit used %d" % (cnt, used)]
    codes = struct.unpack_from("<%dI" % cnt, block, 18)
    noffs = struct.unpack_from("<%dI" % cnt, block, 18 + 4 * cnt)
    voffs = struct.unpack_from("<%dI" % cnt, block, vtab)
    base = vtab + 4 * cnt
    vals, problems = {}, []
    for code, no, vo in zip(codes, noffs, voffs):
        e = no
        while e + 1 < used and block[e:e + 2] != b"\0\0":
            e += 2
        name = bytes(block[no:e]).decode("utf-16-le", errors="replace")
        code &= 0x7FFFFFFF
        if code in INLINE_CODES and not base <= base + vo < used:  # bool/int stored in the table itself (WebApplicationData.IsDefaultWebApplication)
            vals[name] = expando_inline(code, vo)
            continue
        if not base <= base + vo < used:
            problems.append("simple expando value %s out of range" % name)
            continue
        raw, _ = read_var(block, base + vo)
        if EXPANDO_CODES.get(code) == "string":
            # read_var already removed the length prefix; expando_value would read a second one and drop the
            # first character (e.g. layer GUID "1c130bbc-..." -> "c130bbc-...", U10)
            vals[name] = None if raw is None else raw.decode("utf-8", errors="replace")
        else:
            vals[name] = raw if code == 0x1F else expando_value(code, raw or b"")
    return vals, problems


def expando_value(code, raw):
    kind = EXPANDO_CODES.get(code)
    try:
        if kind == "bool" and len(raw) == 1:
            return raw[0] != 0
        if kind == "byte" and len(raw) == 1:
            return raw[0]
        if kind in ("int", "int?") and len(raw) == 4:
            return struct.unpack("<i", raw)[0]
        if kind in ("long", "long?") and len(raw) == 8:
            return struct.unpack("<q", raw)[0]
        if kind == "double" and len(raw) == 8:
            return struct.unpack("<d", raw)[0]
        if kind == "dateTime" and len(raw) == 8:
            return ticks_to_iso(struct.unpack("<q", raw)[0])
        if kind == "guid" and len(raw) == 16:
            import uuid
            return str(uuid.UUID(bytes_le=raw))
        if kind == "string":
            v, _ = read_var(raw, 0)
            return None if v is None else v.decode("utf-8", errors="replace")
    except (struct.error, IndexError):
        pass
    return raw


# --- blobs (pe:BlobT, pe:XmlT, expando blobs) ---------------------------------------------------------------
# Content after the value's varint: u8 kind.
#   kind 0/1: paged ("sparse") blob: u64 total size, u32 ?, u16 page size, u32 page count, varint n + n x u32
#             bitmap of present pages; per present page: kind 0 raw page bytes (page size, last page shorter),
#             kind 1 varint compressed length + zlib stream (decompresses to a full page, cut to the total size).
#             Missing pages are zero.
#   kind 4:   paged blob whose pages live outside the record (large blobs, e.g. the ident table and interface of big
#             blocks; seen with blobs of 150-170 KB): the kind 0/1 head (page count = capacity,
#             a multiple of 32), then one link12 (u16 typeLo, u16 typeHi, u64 oid) per non-zero bitmap word, in order.
#             Each link points to a store record of type BLOB_STORE_TYPE that holds 32 pages of the blob (word i =
#             pages 32*i .. 32*i+31) as an own kind 0/1 blob, see blob_store_content. The content is the
#             concatenation of these chunk blobs; the store records are resolved through the record store of the
#             project (Project.store_data), the value is a StoredBlob that carries it.
#   kind 4 without that layout: varint n + n bytes (prior work; not seen in any file so far).
# (Layout of kinds 0/1 from public prior work, ERNW tia-parser / TIAconv docs; all kinds verified by tools/blobcheck.py.)
BLOB_STORE_TYPE = 0x00070013
CHUNK_PAGES = 32


class BlobError(ValueError):
    """A blob whose layout is recognised but whose content cannot be rebuilt (missing or damaged store record)."""


class StoredBlob(bytes):
    """Blob value (raw bytes unchanged) bound to the record store of its project, so that decode_blob can resolve
    pages that live in store records (kind 4). store: callable key -> record bytes or None."""

    def __new__(cls, raw, store):
        b = super().__new__(cls, raw)
        b.store = store
        return b


def _paged_head(raw):
    """(kind, total, page size, page count, bitmap, offset after the bitmap) of a paged blob, or None."""
    if len(raw) < 20 or raw[0] not in (0, 1, 4):
        return None
    try:
        total, _unk, page, pages = struct.unpack_from("<QIHI", raw, 1)
        nwords, o = varint(raw, 19)
        if not page or nwords > 1 << 16 or o + 4 * nwords > len(raw):
            return None
        bitmap = struct.unpack_from("<%dI" % nwords, raw, o)
    except (struct.error, IndexError):
        return None
    return raw[0], total, page, pages, bitmap, o + 4 * nwords


def external_refs(raw):
    """Record keys ('0013:0007:oid') of the store records of a kind 4 blob, in page order, or None if raw is not a
    kind 4 blob with external pages. Strict: the links must fill the value exactly and match the bitmap."""
    if not isinstance(raw, (bytes, bytearray)) or not raw or raw[0] != 4:
        return None
    h = _paged_head(raw)
    if h is None:
        return None
    _, total, page, pages, bitmap, o = h
    words = [w for w in bitmap if w]
    if page * pages < total or pages % CHUNK_PAGES or o + 12 * len(words) != len(raw):
        return None
    keys = []
    for i in range(len(words)):
        lo, hi, oid = struct.unpack_from("<HHQ", raw, o + 12 * i)
        if (hi << 16) | lo != BLOB_STORE_TYPE:
            return None
        keys.append("%04x:%04x:%x" % (lo, hi, oid))
    return keys


def blob_store_content(data):
    """The chunk blob of a BLOB_STORE_TYPE store record: 44-byte record head (u32 length, key, u32 ?, u32 ?, u32 0,
    GUID), u32 n, n bytes = a kind 0/1 blob, trailing 0xFF."""
    if data is None or len(data) < 49:
        return None
    (n,) = struct.unpack_from("<I", data, 44)
    return bytes(data[48:48 + n]) if 48 + n <= len(data) else None


def _decode_paged(raw):
    import zlib
    h = _paged_head(raw)
    if h is None or h[0] not in (0, 1):
        return None
    kind, total, page, pages, bitmap, o = h
    if total > page * max(pages, 1) or total > 1 << 30:
        return None
    out = bytearray(total)
    for i in range(pages):
        if i // 32 >= len(bitmap) or not (bitmap[i // 32] >> (i % 32)) & 1:
            continue
        size = min(page, total - i * page)
        if kind == 1:
            clen, o = varint(raw, o)
            data = zlib.decompress(raw[o:o + clen])
            o += clen
        else:
            data = raw[o:o + size]
            o += size
        data = data[:size]  # compressed pages decompress to a full page
        out[i * page:i * page + len(data)] = data
    return bytes(out)


def _decode_external(raw, keys, store):
    _, total, page, _, bitmap, _ = _paged_head(raw)
    if store is None:
        raise BlobError("blob with %d pages in store records, but no record store" % (total // page + 1))
    out = bytearray(total)
    chunk = page * CHUNK_PAGES
    for key, word in zip(keys, (i for i, w in enumerate(bitmap) if w)):
        inner = blob_store_content(store(key))
        if inner is None:
            raise BlobError("blob store record %s missing" % key)
        data = _decode_paged(inner)
        expect = min(chunk, total - word * chunk)
        if data is None or len(data) != expect:
            raise BlobError("blob store record %s: %s bytes, expected %d" % (key, None if data is None else len(data), expect))
        out[word * chunk:word * chunk + expect] = data
    return bytes(out)


def decode_blob(raw, store=None):
    """Plain content of a blob value, or None if the layout is not recognised. Pages in store records (kind 4) are
    read through store (callable key -> record bytes), by default the one a StoredBlob carries; raises BlobError if
    they cannot be read."""
    if not raw:
        return b""
    if raw[0] == 4:
        keys = external_refs(raw)
        if keys is not None:
            return _decode_external(raw, keys, store or getattr(raw, "store", None))
        n, o = varint(raw, 1)
        return bytes(raw[o:o + n]) if o + n == len(raw) else None
    return _decode_paged(raw)
