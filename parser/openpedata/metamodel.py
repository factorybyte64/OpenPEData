"""Metamodel (MetaInfo XML) stored in PEData.plf record 0000:0007:1.

Record layout: u32 compressed length at +44, zlib stream at +48 (UTF-8 XML, namespace
http://www.siemens.com/Automation/2004/04/ObjectFrame/Meta).

Ids are u32 = (package << 16) | local id. Local id ranges seen: 0x1xxx ObjectType, 0x2xxx Relation/Inverse,
0x3xxx AttributeSet, 0x4xxx Structure, 0x5xxx Enumeration, 0x7xxx generated relations (Implements ... relation=).
Attributes inside an AttributeSet carry a small ordinal id (0, 1, 2, ...).
Package ids are u32 as well (e.g. 0x00400003); several Packages share the high half (0x0040, 0x0010, 0x0002 ...),
so the high half of a type id does not identify its Package: the declaring Package is taken from the XML nesting.
"""
import struct
import xml.etree.ElementTree as ET
import zlib

NS = "{http://www.siemens.com/Automation/2004/04/ObjectFrame/Meta}"
TYPE_KINDS = ("ObjectType", "Structure", "Enumeration", "Array", "AttributeSet", "BehaviourType")


def _id(s):
    return int(s, 16) if s else None


class MetaType:
    def __init__(self, kind, name, full, tid, el):
        self.kind, self.name, self.full_name, self.id = kind, name, full, tid
        self.bases = []          # full names
        self.implements = []     # full names of AttributeSets
        self.attributes = []     # (ordinal, name, type, default) for AttributeSet / Structure
        self.attr_flags = {}     # AttributeSet: name -> XML attributes of the Attribute element
        self.overrides = {}      # ObjectType: AttributeSet full name -> {attr name: XML attributes} (Implements)
        self.relations = []      # MetaRelation declared here
        self.constants = {}      # Enumeration value -> name
        self.abstract = el.get("abstract") == "true"
        self.base_type = el.get("base")  # Enumeration underlying type (default xs:int)
        self.expando = el.get("expando")  # AttributeSet with dynamic attributes: "true" or "simple"
        self.persistent = el.get("persistent")
        self.package = None      # name of the declaring Package
        self.elements = []       # Structure: (name, type) of its Element children in declaration order (no ids)
        self.item_type = el.get("type") if kind == "Array" else None  # Array: type of its items


class MetaRelation:
    def __init__(self, name, rid, owner, target, cardinality, behaviour, inverse_of=None):
        self.name, self.id, self.owner, self.target = name, rid, owner, target
        self.cardinality, self.behaviour, self.inverse_of = cardinality, behaviour, inverse_of
        self.inverse_ref = None   # name of the inverse relation declared on the target type
        self.inverse_id = None


class Metamodel:
    def __init__(self, xml_bytes):
        self.xml = xml_bytes
        self.types = {}       # id -> MetaType
        self.by_name = {}     # full name -> MetaType
        self.relations = {}   # id -> MetaRelation (incl. inverses with own id)
        self.packages = {}    # full package id -> name
        root = ET.fromstring(xml_bytes)
        for pkg in root.iter(NS + "Package"):
            pid = _id(pkg.get("id"))
            if pid is not None:
                self.packages[pid] = pkg.get("name")
            for ns in pkg.iter(NS + "Namespace"):
                nsname = ns.get("name")
                for el in ns:
                    kind = el.tag[len(NS):]
                    if kind not in TYPE_KINDS or not el.get("id"):
                        continue
                    t = MetaType(kind, el.get("name"), nsname + "." + el.get("name"), _id(el.get("id")), el)
                    t.package = pkg.get("name")
                    self.types[t.id] = t
                    self.by_name[t.full_name] = t
                    for c in el:
                        ck = c.tag[len(NS):]
                        if ck == "Element" and kind == "Structure":
                            t.elements.append((c.get("name"), c.get("type")))
                        if ck == "Base":
                            t.bases.append(c.get("ref"))
                        elif ck == "Implements":
                            t.implements.append(c.get("ref"))
                            t.overrides[c.get("ref")] = {a.get("name"): dict(a.attrib) for a in c.findall(NS + "Attribute")}
                        elif ck in ("Attribute", "Element") and c.get("id") is not None:
                            dv = c.find(NS + "DefaultValue")
                            t.attributes.append((int(c.get("id"), 0), c.get("name"), c.get("type"),
                                                 dv.text if dv is not None else None))
                            t.attr_flags[c.get("name")] = dict(c.attrib)
                        elif ck == "Constant":
                            t.constants[c.get("value")] = c.get("name")
                        elif ck == "Relation":
                            tgt = c.find(NS + "Target")
                            r = MetaRelation(c.get("name"), _id(c.get("id")), t.full_name,
                                             tgt.get("ref") if tgt is not None else None,
                                             c.get("cardinality"), c.get("behaviourType"))
                            t.relations.append(r)
                            self.relations[r.id] = r
                            inv = c.find(NS + "Inverse")
                            if inv is not None and inv.get("ref"):
                                r.inverse_ref = inv.get("ref")
                            if inv is not None and inv.get("id"):
                                ir = MetaRelation(inv.get("name"), _id(inv.get("id")), r.target, t.full_name,
                                                  inv.get("cardinality"), inv.get("behaviourType"), inverse_of=r.id)
                                self.relations[ir.id] = ir
                                r.inverse_id, ir.inverse_id = ir.id, r.id
        self._ancestors = {}
        # resolve Inverse ref="Name": relation of that name declared on the target type or one of its bases
        for r in list(self.relations.values()):
            if r.inverse_ref and r.inverse_id is None and r.target:
                for a in self.ancestors(r.target):
                    at = self.by_name.get(a)
                    hit = [x for x in at.relations if x.name == r.inverse_ref] if at else []
                    if hit:
                        r.inverse_id = hit[0].id
                        break
        # declaration order of every type in the document (not the slot order of AttributeSets, see H-020)
        self.doc_order = {t.full_name: i for i, t in enumerate(self.types.values())}

    @classmethod
    def from_record(cls, rec_data):
        (clen,) = struct.unpack_from("<I", rec_data, 44)
        return cls(zlib.decompress(rec_data[48:48 + clen]))

    def type_name(self, tid):
        t = self.types.get(tid)
        return t.full_name if t else "?0x%08x" % tid

    def short_name(self, tid):
        t = self.types.get(tid)
        return t.name if t else "?0x%08x" % tid

    def type_package(self, tid):
        """Name of the Package element that declares a type (XML nesting; the high half of the id is ambiguous)."""
        t = self.types.get(tid)
        return t.package if t else None

    def ancestors(self, full_name):
        """Type and all base types (depth first, each once)."""
        if full_name in self._ancestors:
            return self._ancestors[full_name]
        out, stack, seen = [], [full_name], set()
        while stack:
            n = stack.pop(0)
            if n in seen:
                continue
            seen.add(n)
            out.append(n)
            t = self.by_name.get(n)
            if t:
                stack.extend(t.bases)
        self._ancestors[full_name] = out
        return out

    def ancestors_topological(self, full_name):
        """Type and all base types, every type after all of its own bases (base ... derived). Used to merge
        Implements overrides: with diamond inheritance the breadth-first order of ancestors() can apply a base
        (e.g. CoreObject: Comment constant) after a type derived from it (HmiReusableConfiguration: Comment not
        constant), which decodes ICoreAttributes of ILScreenData, HmiDiscreteAlarmData, ... misaligned."""
        cache = self.__dict__.setdefault("_topo", {})
        if full_name in cache:
            return cache[full_name]
        out, seen = [], set()
        stack = [(full_name, False)]
        while stack:
            n, done = stack.pop()
            if done:
                out.append(n)
                continue
            if n in seen:
                continue
            seen.add(n)
            stack.append((n, True))
            t = self.by_name.get(n)
            for b in reversed(t.bases if t else []):
                if b not in seen:
                    stack.append((b, False))
        cache[full_name] = out
        return out

    def resolve(self, type_ref, context_ns=None):
        """Metamodel type for an attribute type reference (full or namespace-relative name)."""
        t = self.by_name.get(type_ref)
        if t is None and context_ns:
            t = self.by_name.get(context_ns + "." + type_ref)
        if t is None:
            t = self._short.get(type_ref) if hasattr(self, "_short") else None
            if t is None and not hasattr(self, "_short"):
                self._short = {}
                for x in self.types.values():
                    self._short.setdefault(x.name, x)
                t = self._short.get(type_ref)
        return t

    def stored_attributes(self, tid, set_full):
        """Attributes of an AttributeSet that are stored in a record of type tid, in ordinal order:
        list of (ordinal, name, xml type, flags). An attribute is stored unless it is intrinsic, mapped to a
        relation, or constant (flags merged from the set definition and the Implements overrides of all
        ancestors, more derived types overriding per flag)."""
        key = (tid, set_full)
        cache = self.__dict__.setdefault("_stored", {})
        if key in cache:
            return cache[key]
        st = self.by_name.get(set_full)
        t = self.types.get(tid)
        res = []
        if st and t:
            anc = self.ancestors_topological(t.full_name)
            for ordinal, name, typ, _ in sorted(st.attributes):
                fl = dict(st.attr_flags.get(name, {}))
                for a in anc:  # bases before derived types, more derived overrides per flag
                    at = self.by_name.get(a)
                    ov = at.overrides.get(set_full, {}).get(name) if at else None
                    if ov is not None:
                        fl.update(ov)
                if fl.get("intrinsic") == "true" or fl.get("relation") or fl.get("constant") == "true":
                    continue
                res.append((ordinal, name, typ, fl))
        cache[key] = res
        return res

    def attribute_sets_ordered(self, tid):
        """Implemented AttributeSets in record slot order: ordinal (code point) order of the qualified set name
        (H-020, decided in L2: 924/924 contested slots of the Unified samples decode only this way, e.g.
        IReportStorageLocationAttributes before IReportingPropertyExpandoAttributes; the metamodel declaration order
        (doc_order) differs for 126 Unified types and LibraryMasterCopyData)."""
        return sorted(self.attribute_sets(tid))

    def is_a(self, tid, full_name):
        t = self.types.get(tid)
        return bool(t) and full_name in self.ancestors(t.full_name)

    def attribute_sets(self, tid):
        """AttributeSets implemented by a type including inherited ones (full names)."""
        t = self.types.get(tid)
        if not t:
            return []
        res = []
        for a in self.ancestors(t.full_name):
            at = self.by_name.get(a)
            if at:
                for s in at.implements:
                    if s not in res:
                        res.append(s)
        return res
