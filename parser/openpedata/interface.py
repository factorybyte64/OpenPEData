"""Block interfaces: members of data blocks and PLC data types (UDTs) with data type, start value and comment.

Objects (samples 017-022, relation names from the metamodel):
  block/UDT --CurrentInterface--> InterfaceVersionRootData   IInterfacePartData.Payload = XML <Root>
  block/UDT --InterfaceComments--> InterfaceCommentsPartData  ICommentsExpandoAttributeSet (expando="simple"):
                                                              member path -> CoreText
  root --UsedParts--> [InterfacePartData | InterfaceVersionRootData]   ordered list, indexed by SubPartIndex
      InterfacePartData Kind=Structure: Payload <Member ParentId=..> with the members of a Struct
      InterfacePartData Kind=Values:    Payload <Values><V p=path v=value/></Values>  start values (data block)
      InterfaceVersionRootData:         interface of a used UDT (member of type "UDT")
  UDT start values: root ICoreExpandoAttributes.Values (same <Values> XML)

Instance DB (samples 025/029): CurrentInterface is an own InterfaceVersionRootData (Kind=Values) whose Payload is the
<Values> XML with the DB's own start values (p = member ID in the FB); its UsedParts point to the FB's interface root.

Code blocks (FB/FC/OB, samples 023/024): the root payload lists the sections as members (ID 2 Input, 3 Output,
4 InOut, 5 Static, 6 Temp, 7 Return, 8 Constant). A section either points to an InterfacePartData
(SubPartIndex; its payload is a wrapper <Member> around the section members) or holds its members inline
(Return, Constant; those have n/ph attributes). UsedParts is an index list in which unused slots are null links
(0000:0000:0), so the index must be taken from the raw link list; SubPartIndex of a UDT-typed member and of
<ExternalType> also counts in that list. Member IDs are unique per block (they do not repeat the section ID), so the
comment, start value and element paths are just the member ID. Start values of FB come from a Values part, those of
FC from the root expando Values.

<Member ID Name RID [Type] [SubPartIndex] StdO LID/>: ID is unique inside its parent; a member path is the IDs
joined by ':' (e.g. "55:52"), an array element appends "[i]" ("54[1]"). RID: high byte 0x02 elementary type with
DataTypeID in the low 16 bits (name from DataTypeItemData of the CPU family scope, e.g. S71500: Bool 1, Word 4,
Int 5, DInt 7, Real 8); 0x92.. Struct; 0x91.. UDT; arrays and UDTs carry the declaration in Type.
Start values of a UDT-typed member are only the values the data block sets itself (as in the SimaticML export).
"""
import re
import xml.etree.ElementTree as ET

from . import attrs


# presentation order of Openness / the TIA editor; the payload lists Return (ID 7) before Constant (ID 8)
SECTION_ORDER = ["Input", "Output", "InOut", "Static", "Temp", "Constant", "Return"]


def _local(tag):
    return tag.rsplit("}", 1)[-1]


def xml_of(blob, store=None):
    """Parsed XML element of a pe:XmlT/BlobT value, or None. store: record store for blobs with pages in store
    records (attrs.decode_blob); a blob whose store records cannot be read raises attrs.BlobError."""
    if not isinstance(blob, bytes) or not blob:
        return None
    try:
        b = attrs.decode_blob(blob, store)
    except attrs.BlobError:
        raise
    except Exception:  # noqa: BLE001 - unknown blob layout
        return None
    if not b:
        return None
    try:
        return ET.fromstring(b.decode("utf-8-sig"))
    except (ET.ParseError, UnicodeDecodeError):
        return None


def split_indexed(key):
    """Member path and the indices of all array levels of a value path, like the Subelement Path of Openness:
    "56[2]:51" -> ("56:51", "2"), "55:57[1]:51[2]" -> ("55:57:51", "1,2") (sample 059). None without an index."""
    if not isinstance(key, str) or key.find("[") <= 0:
        return None
    idx = re.findall(r"\[([^\]]*)\]", key)
    return re.sub(r"\[[^\]]*\]", "", key), ",".join(idx)


def index_elements(values):
    """{member path: {indices: value}} of all indexed value paths."""
    out = {}
    for p, v in values.items():
        sp = split_indexed(p)
        if sp:
            out.setdefault(sp[0], {})[sp[1]] = v
    return out


def _add_attributes(m, el, retain):
    """MFlags of a member (default 15 without the attribute): bit 0 accessible from HMI/OPC UA, bit 1 visible, bit 6
    read-only from outside, bit 9 setpoint; bits 0..2 clear = hidden system member (sample 060). Accessible gates visible
    and writable like in Openness."""
    f = int(el.get("MFlags")) if el.get("MFlags") is not None else 15
    acc = bool(f & 1) and m["dataType"] != "Variant"  # a Variant (reference) is never accessible (sample 066)
    m["retain"] = retain
    m["externalAccessible"] = acc
    m["externalVisible"] = acc and bool(f & 2)
    m["externalWritable"] = acc and not f & 64
    m["setpoint"] = bool(f & 512)


def _inaccessible(members):
    """Members below a member that is not accessible are not accessible either (e.g. the private timers rcv_timer,
    wd_timer of MB_CLIENT; sample 066)."""
    for m in members:
        m["externalAccessible"] = m["externalVisible"] = m["externalWritable"] = False
        _inaccessible(m.get("members", []))


def clear_setpoint(members):
    """Openness reports setpoints only in the Static section of an FB: members below Input, Output and InOut (also of a
    multi-instance) are no setpoints (sample 060)."""
    for m in members:
        m["setpoint"] = False
        clear_setpoint(m.get("members", []))


def values_map(el):
    return {v.get("p"): v.get("v") for v in el.iter() if _local(v.tag) == "V"} if el is not None else {}


class Interface:
    @staticmethod
    def _drop_neutral(t):
        """The language-neutral entry (65535) repeats the text of a project language in system templates (OB Main: Initial_Call); the export shows only the project languages."""
        if "65535" in t and any(c != "65535" and x == t["65535"] for c, x in t.items()):
            t = {c: x for c, x in t.items() if c != "65535"}
        return t

    def __init__(self, model, owner, scope, root=None):
        self.m, self.p = model, model.p
        self.types = model.elementary_types(scope)
        # root: an explicit interface root (technology object type from the system library, model.to_sections)
        # system and library blocks (PID_Compact, High_Speed_Counter, F-blocks, ...) link their interface root by
        # CurrentLibraryInterface instead of CurrentInterface (sample 036)
        self.root = root or model.rel1(owner, "CurrentInterface") or model.rel1(owner, "CurrentLibraryInterface")
        # index-preserving: null links (0000:0000:0) stay None so that SubPartIndex counts correctly
        self.parts = [l.key if l.key in self.p.objects else None
                      for l in self.p.links(self.root, "UsedParts")] if self.root else []
        self.values = {}
        self.keep_path = False  # instance DB: members keep their path ("_path") to look up the DB's own values
        self.standard = False  # standard (not optimized) block access: members get their offset "byte.bit"
        if self.root and self.p.attr(self.root, "IInterfacePartData", "Kind") == "Values":
            # instance DB: the DB's own start values (paths = member IDs of the FB) are the root payload itself
            self.values.update(values_map(xml_of(self.p.attr(self.root, "IInterfacePartData", "Payload"))))
        for k in self.parts:
            if k is not None and self.p.attr(k, "IInterfacePartData", "Kind") == "Values":
                self.values.update(values_map(xml_of(self.p.attr(k, "IInterfacePartData", "Payload"))))
        if self.root:
            ex = self.p.attributes(self.root)[0].get("ICoreExpandoAttributes", {})
            if isinstance(ex.get("Values"), bytes):  # expando blob values carry their own varint
                self.values.update(values_map(xml_of(attrs.read_var(ex["Values"], 0)[0], self.p.store_data)))
        cm = model.rel1(owner, "InterfaceComments")
        raw = self.p.attributes(cm)[0].get("ICommentsExpandoAttributeSet", {}) if cm else {}
        self.comments = {path: self._drop_neutral(model.core_text_of(v)) for path, v in raw.items()}

    def payload(self, key):
        return xml_of(self.p.attr(key, "IInterfacePartData", "Payload"))

    def members(self):
        """Top-level members (data block: the Static section; UDT: its elements)."""
        if not self.root:
            return []
        root = self.payload(self.root)
        if root is None:
            return []
        return self._members(root, "", own=True)

    def _type(self, el):
        if el.get("Type"):
            return el.get("Type")
        rid = int(el.get("RID", "0"), 16)
        if rid >> 24 == 0x02:
            if rid & 0xFFFF == 0:
                return "Void"  # return value of an FC declared ": Void" (DataTypeID 0)
            return self.types.get(rid & 0xFFFF, "RID 0x%08X" % rid)
        if rid >> 24 in (0x91, 0x92, 0x93, 0x94):
            return "Struct"  # anonymous Struct: 0x92 in a DB, 0x91 in a UDT, 0x93 in an FB (sample 059), 0x94 in an FC (sample 073)
        return "RID 0x%08X" % rid

    def _parts_of(self, root_key):
        """UsedParts of an interface root with null links kept (SubPartIndex counts in each root's own list)."""
        return [l.key if l.key in self.p.objects else None for l in self.p.links(root_key, "UsedParts")]

    def _sub_members(self, part, path, base_off=0, retain=False):
        """Members shown below a member whose type is the interface root `part`: elements of a UDT, or for an FB
        (multi-instance) the members of its Input, Output, InOut and Static sections."""
        sub = self.payload(part)
        if sub is None:
            return []
        subparts = self._parts_of(part)
        # section members of a code block root have the fixed IDs 2..8, user members start at 51 (UDT element "Temp")
        sections = [el for el in sub if _local(el.tag) == "Member" and el.get("Name") in SECTION_ORDER
                    and 2 <= int(el.get("ID", "0")) <= 8]
        if not sections:
            return self._members(sub, path + ":", False, subparts, base_off, retain)
        out = []
        for name in ("Input", "Output", "InOut", "Static"):
            for el in sections:
                if el.get("Name") != name:
                    continue
                spi = el.get("SubPartIndex")
                if spi is not None and int(spi) < len(subparts) and subparts[int(spi)] is not None:
                    wrap = self.payload(subparts[int(spi)])
                else:
                    wrap = el
                ms = self._members(wrap, path + ":", False, subparts, base_off, retain) if wrap is not None else []
                if name != "Static":
                    clear_setpoint(ms)
                out += ms
        return out

    def _element_values(self, path):
        """Start values of the array elements of a member: {"1": v} for the value path "path[1]" (member paths contain
        no "["). Indexed once per state of self.values instead of a scan per member (large data blocks)."""
        if getattr(self, "_elem_n", None) != len(self.values):
            self._elem_index, self._elem_n = index_elements(self.values), len(self.values)
        return dict(self._elem_index.get(path, {}))

    def _members(self, parent, prefix, own, parts=None, base_off=0, retain=False):
        parts = self.parts if parts is None else parts
        out = []
        for el in parent:
            if _local(el.tag) != "Member":
                continue
            if el.get("MFlags") is not None and int(el.get("MFlags")) & 7 == 0:
                continue  # hidden system member (ST, RU of IEC_TIMER, internal statics of PID_Compact): not in Openness
            if el.get("AtMemberID") is not None:
                continue  # AT view over another member (e.g. FC1_4_Req of MB_CLIENT): not shown by Openness (sample 066)
            path = prefix + el.get("ID")
            m = {"name": el.get("Name"), "dataType": self._type(el), "startValue": self.values.get(path),
                 "comment": self.comments.get(path, {}) if own else {}}
            elems = self._element_values(path)
            if elems:
                m["elementStartValues"] = elems
            # Remanence="Retain" on the member (optimized) holds for all its elements; StdO = standard offset in bits,
            # relative to the enclosing struct/UDT
            ret = retain or el.get("Remanence") == "Retain"
            off = base_off + int(el.get("StdO", "0"))
            _add_attributes(m, el, ret)
            if el.get("Accessibility") == "Private":
                m["accessibility"] = "Private"  # internal member of a system/library FB (InternalDiagnostic of PID_Compact)
            if self.standard:
                m["offset"] = "%d.%d" % (off // 8, off % 8)
            if self.keep_path:
                m["_path"] = path
            kids = self._members(el, path + ":", own, parts, off, ret)
            spi = el.get("SubPartIndex")
            if not kids and spi is not None and int(spi) < len(parts) and parts[int(spi)] is not None:
                part = parts[int(spi)]
                if self.p.is_a(part, "InterfaceVersionRootData"):
                    kids = self._sub_members(part, path, off, ret)  # used UDT / system type / FB (multi-instance)
                else:
                    sub = self.payload(part)
                    if sub is not None:
                        kids = self._members(sub, path + ":", own, parts, off, ret)
            if kids:
                # Openness reports a member of a named structured type (UDT, FB, system type, array of them) as
                # setpoint; anonymous Structs and DTL are not (226 values in samples 060, 066-068). Below Input, Output
                # and InOut clear_setpoint resets it.
                dt = m["dataType"] or ""
                if dt != "Struct" and not dt.endswith(" of Struct") and dt != "DTL" and not dt.endswith(" of DTL"):
                    m["setpoint"] = True
                if not m["externalAccessible"]:
                    _inaccessible(kids)
                m["members"] = kids
            out.append(m)
        return out

    def sections(self):
        """Code block interface: [{"section": name, "members": [...]}] in section order; empty sections are omitted."""
        if not self.root:
            return []
        return self._sections(self.root, self.parts, "")

    def _sections(self, root_key, parts, prefix):
        """Sections of the interface root root_key. A technology object type derives from a base type
        (TO_SynchronousAxis : TO_PositioningAxis : TO_SpeedAxis : TO_Axis : TO_Object, sample 072): the root member
        "Base" (ID 49, outside the sections) points to the base type's root. Its members come first in each section,
        with the value path prefix "49:" per level, as in the SimaticML export (nested Section "Base")."""
        root = self.payload(root_key)
        if root is None:
            return []
        out, bases = [], []
        for el in root:
            if _local(el.tag) != "Member":
                continue
            spi = el.get("SubPartIndex")
            sp = parts[int(spi)] if spi is not None and int(spi) < len(parts) else None
            name = el.get("Name")
            if name == "Base" and name not in SECTION_ORDER and sp is not None and self.p.is_a(sp, "InterfaceVersionRootData"):
                bases = self._sections(sp, self._parts_of(sp), prefix + el.get("ID") + ":")
                continue
            if sp is not None:
                sub = self.payload(sp)
                members = self._members(sub, prefix, True, parts) if sub is not None else []
            else:  # inline section (Return, Constant)
                members = self._members(el, prefix, True, parts)
            if members:
                out.append({"section": name, "members": members})
        for b in bases:
            own = next((s for s in out if s["section"] == b["section"]), None)
            if own is None:
                out.append({"section": b["section"], "members": list(b["members"])})
            else:
                own["members"] = b["members"] + own["members"]
        out.sort(key=lambda s: SECTION_ORDER.index(s["section"]) if s["section"] in SECTION_ORDER else len(SECTION_ORDER))
        return out
