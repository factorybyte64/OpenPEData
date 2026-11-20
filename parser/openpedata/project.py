"""Loads a TIA Portal V21 project (project folder, .ap21 file or .zap21 archive) into an object store view."""
import os
import zipfile

from . import attrs as attrcodec
from . import plf
from .metamodel import Metamodel
from .record import ObjectRecord, is_tombstone

METAMODEL_KEY = "0000:0007:1"


def _plf_parts(names):
    """PEData.plf plus continuation files PEData-01.plf, PEData-02.plf ... (large projects, per public prior
    work; not yet seen in our samples). Returns the names in reading order."""
    import re
    norm = lambda n: n.replace("\\", "/").lower()  # noqa: E731
    base = [n for n in names if norm(n) == "system/pedata.plf"]
    more = sorted((n for n in names if re.fullmatch(r"system/pedata-(\d+)\.plf", norm(n))),
                  key=lambda n: int(re.fullmatch(r"system/pedata-(\d+)\.plf", norm(n)).group(1)))
    return base + more


def _read_plf(path):
    """[(name, bytes)] of the plf file(s) of a project folder, .ap21 or .zap21."""
    if path.lower().endswith(".ap21"):
        path = os.path.dirname(os.path.abspath(path))
    if os.path.isdir(path):
        sysdir = os.path.join(path, "System")
        names = _plf_parts([os.path.join("System", n) for n in os.listdir(sysdir)])
        out = []
        for n in names:
            with open(os.path.join(path, n), "rb") as f:
                out.append((n, f.read()))
        return out
    with zipfile.ZipFile(path) as z:
        return [(n, z.read(n)) for n in _plf_parts(z.namelist())]


class Project:
    def __init__(self, path, verify=True):
        self.path = path
        parts = _read_plf(path)
        self.plf_files = [n for n, _ in parts]   # more than one: continuation files (unverified layout)
        self.plf_bytes = parts[0][1]
        self.records = []
        for _, b in parts:
            self.records.extend(plf.read_records(b, verify))
        self.latest = plf.latest(self.records)
        mm_rec = self.latest.get(METAMODEL_KEY) or next(r for r in self.latest.values() if r.type_id == 0x00070000)  # other oid in some libraries (Lib\Sys\Buttons-and-Switches: 0000:0007:8f28)
        self.mm = Metamodel.from_record(mm_rec.data)
        self.objects = {}  # key -> ObjectRecord (domain objects, current version)
        self.deleted = []  # keys whose current version is a tombstone
        for k, r in self.latest.items():
            if r.is_store or r.type_id not in self.mm.types:
                continue
            if is_tombstone(r):
                self.deleted.append(k)
            else:
                self.objects[k] = ObjectRecord(r, self.mm)
        self._attrs = {}
        self._is_a = {}  # (type id, type name) -> bool
        self.expando_dicts = {}  # object type id -> {expando id: (name, type code)}
        for r in self.latest.values():
            if r.type_id == 0x00070011:
                tid, d = attrcodec.read_dictionary(r.data)
                self.expando_dicts[tid] = d

    # --- attributes -------------------------------------------------------------------------------
    def attributes(self, key):
        """{set short name: {attr: value}} for all decodable attribute sets of an object."""
        if key in self._attrs:
            return self._attrs[key]
        o = self.objects[key]
        res, problems = {}, {}
        for i in range(o.n_sets if o.layout_ok else 0):
            if i not in o.blocks:
                continue
            a, e = o.blocks[i]
            if o.is_expando(i) and self.mm.by_name[o.set_names[i]].expando == "simple":
                vals, p = attrcodec.decode_simple_expando(o.data[a:e])
            elif o.is_expando(i):
                vals, p = attrcodec.decode_expando(o.data[a:e], self.expando_dicts)
            else:
                vals, p = attrcodec.decode_set(self.mm, o.rec.type_id, o.set_names[i], o.data[a:e])
            for n, v in vals.items():
                if isinstance(v, bytes) and v[:1] == b"\x04" and attrcodec.external_refs(v) is not None:
                    vals[n] = attrcodec.StoredBlob(v, self.store_data)  # pages in store records
            res[o.set_name(i)] = vals
            if p:
                problems[o.set_name(i)] = p
        self._attrs[key] = (res, problems)
        return res, problems

    def store_data(self, key):
        """Bytes of the current version of a store record (e.g. the pages of a large blob), or None."""
        r = self.latest.get(key)
        return r.data if r is not None and r.is_store else None

    def decode_blob(self, raw):
        """attrs.decode_blob with this project's store records (also for blob values that are not a StoredBlob, e.g.
        expando blobs after their own varint)."""
        return attrcodec.decode_blob(raw, self.store_data)

    def attr(self, key, set_name, name, default=None):
        return self.attributes(key)[0].get(set_name, {}).get(name, default)

    def name(self, key):
        return self.attr(key, "ICoreAttributes", "Name")

    # --- types and links --------------------------------------------------------------------------
    def type_name(self, key):
        return self.mm.short_name(self.objects[key].rec.type_id)

    def is_a(self, key, type_name):
        """type_name: short or full metamodel type name."""
        o = self.objects.get(key)
        if not o:
            return False
        ck = (o.rec.type_id, type_name)  # per type, not per object (large projects: millions of calls)
        r = self._is_a.get(ck)
        if r is None:
            anc = self.mm.ancestors(self.mm.types[o.rec.type_id].full_name)
            r = self._is_a[ck] = any(a == type_name or a.endswith("." + type_name) for a in anc)
        return r

    def links(self, key, relation=None):
        """Outgoing links (tagged ones carry the relation id). relation: relation name filter."""
        o = self.objects.get(key)
        if not o:
            return []
        out = []
        for l in o.links:
            if relation is not None:
                if l.relation is None:
                    name = self._slot_relation(key, o, l.slot)
                else:
                    r = self.mm.relations.get(l.relation)
                    name = r.name if r else None
                if name != relation:
                    continue
            out.append(l)
        return out

    def _slot_relation(self, key, o, slot):
        """Relation of a typed link list (long lists, e.g. UsedParts of an FB interface with many multi-instances,
        sample 066): the slot carries no relation id, so it is taken from the target side, which links back with the
        inverse relation as a tagged link."""
        cache = self.__dict__.setdefault("_slot_rel", {})
        if (key, slot) in cache:
            return cache[(key, slot)]
        name = None
        for l in o.links:
            t = self.objects.get(l.key) if l.relation is None and l.slot == slot else None
            if not t:
                continue
            for b in t.links:
                r = self.mm.relations.get(b.relation) if b.relation is not None and b.key == key else None
                if r:
                    inv = self.mm.relations.get(r.inverse_id) if r.inverse_id is not None else None
                    name = inv.name if inv else r.inverse_ref
                    if name:
                        break
            if name:
                break
        cache[(key, slot)] = name
        return name

    def targets(self, key, relation=None):
        return [l.key for l in self.links(key, relation) if l.key in self.objects]

    def of_type(self, type_name):
        return [k for k in self.objects if self.is_a(k, type_name)]
