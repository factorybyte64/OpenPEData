"""WinCC Unified faceplate types and instances of a TIA Portal V21 project (step F3, see analysis/F3_faceplate:
METAMODEL.md, HIDDEN-DEVICE.md, F03.md, A123.md, COREMAPARRAY.md).

extract_faceplates(model) takes an openpedata.model.Model and returns a list; Model.to_dict() puts it under
"faceplates" ([] in a project without faceplate types and containers).
Standalone (from the parser directory):
  python -m openpedata.faceplates <project folder | .ap21 | .zap21> [--json] [--keys]

Types, versions, default, state, author and release time come from library.py (extract_library); this module adds
what is specific to faceplates. Navigation (relation names from the metamodel):
  FaceplateLibraryTypeData 0x00d30002  ICoreLibAttributes, ITypeInfo.TypeGuid, IHmiByRefTypeObjectBaseAttributes
                                       .AlwaysDownload, -TypeObjectToVersionObject-> versions
  FaceplateLibraryVersionData 0x00d30003  IVersionInfo, -VersionToContent-> content; every object with
                                       -Environment-> version belongs to the version ("hidden device": a minimal
                                       Unified HMI software, fixed core of 79 objects plus content objects)
  ILFaceplateTypeData 0x00cd1004       the content, an ILScreenData: size and -ILScreenItems-> screen items
                                       (decoded with hmi.py); IFaceplateInterface (InterfaceProperties,
                                       TagInterfaceProperties, InterfaceEvents, CoreMapArray of pe:MapT) has no block
                                       while the interface is empty; interface tags in a tag table of their own
                                       (-ILFaceplateToTagTable->, F05.md), properties / events still without sample
  unresolved tags                      HmiTagData shells in the version (only ICoreAttributes, no -TagTable->),
                                       the compile blocker of F03 FP_Interface / FP_Script
  ILFaceplateContainerData 0x00d30006  instance on a screen; typed: -IsInstanceOf-> version (library.py
                                       "instances"); without type (ContainedType empty, F01, F03) listed in an
                                       entry with name None; a container inside a version is part of that version
"""
import json
import re
import struct
import sys
import uuid

from .attrs import decode_blob, varint


def decode_version_text(raw):
    """IVersionInfo.Version (A3: u32 20, i32 Build, Major, Minor, Revision) -> 'Major.Minor.Build'."""
    try:
        _, build, major, minor, _ = struct.unpack_from("<Iiiii", raw, 0)
        return "%d.%d.%d" % (major, minor, build)
    except (struct.error, TypeError):
        return None

FACEPLATE_TYPE = "FaceplateLibraryTypeData"
CONTAINER_TYPE = "ILFaceplateContainerData"
VERSION_FIELDS = ("version", "state", "isDefault", "finalizationTime", "author", "comment", "minimumTargetDeviceVersion",
                  "versionGuid", "creationTime", "modifiedTime", "lastModifiedBy")  # versionGuid changes on release (RELEASE.md)
TYPE_FIELDS = ("name", "typeGuid", "folderPath", "isObsolete", "defaultVersion", "latestVersion", "instanceCount")
_FONT_LIST_CONTENT = re.compile(r"FontList_C\d+$")  # font list per used font combination (content dependent)
# fixed-size MapT values by expando code (attrs.EXPANDO_CODES): byte, uint, long, float, double, dateTime ticks, long?
_MAP_FIXED = {0x02: "<B", 0x04: "<I", 0x06: "<q", 0x07: "<f", 0x08: "<d", 0x0a: "<q", 0x14: "<q"}
# DataType codes of interface tags / interface properties (one enumeration; F05, F16, F40 (Siemens demo project, not in the repo), Openness truth of
# F16: property interface data types of TIA V21 and their codes)
DATA_TYPES = {1: "Bool", 3: "Int", 5: "LInt", 9: "ULInt", 10: "Real", 11: "LReal", 13: "DateTime", 14: "WString",
              30: "Struct", 1282: "Color",
              65542: "Graphic", 65544: "Multilingual text", 65555: "Authorization", 65575: "Parameter set type",
              65585: "Resource list"}
CONTENT_TYPES = {"HmiArrayMemberTagData", "HmiStructureMemberTagData", "CpmUdtData", "CpmHmiStructureMemberTypeData", "ImageInstanceData", "TextListInstanceData",
                 "ScopedLinkMaintainerData", "HmiFunctionListEmbeddedData", "FaceplateInstanceMediatorData",
                 "DynamicSVGInstanceMediatorData"}
LAYERS_PROPERTY = "K\x02"  # expando name of property id 0x024b (layer list of a screen / faceplate content)


def _short(p, k):
    return (p.type_name(k) or "").rsplit(".", 1)[-1]


def decode_map(raw):
    """pe:MapT value (COREMAPARRAY.md) -> {key: value} or None. raw: pe blob (attrs.decode_blob) or plain map:
    u32 length, u32 count, u32 offset of the value area, count x u32 type code (bit 31 set), count x key
    (varint(n+1) + UTF-8), values in key order: 0x05 / 0x13 i32, 0x0c GUID (16 bytes, .NET order), 0x0b u32 offset
    (relative to the value area) of a string varint(n+1) + UTF-8 (F05 TagInterfaceProperties), 0x15 u32 offset of a
    nested map (CONTROLS.md). Expando values may carry a varint prefix = total length incl. the prefix."""
    if not isinstance(raw, (bytes, bytearray)) or len(raw) < 12:
        return None
    try:
        n, q = varint(raw, 0)
        if 24 < n <= len(raw) and q < len(raw) and raw[q] in (0, 1, 4):
            raw = raw[q:n]  # Properties 4f ..., Q b9 01 ...; instance Properties carry a few trailing bytes (F40 Siemens demo project, not in the repo)
        if raw[0] in (0, 1, 4) and struct.unpack_from("<I", raw, 0)[0] != len(raw):
            total = struct.unpack_from("<Q", raw, 1)[0]  # blob header: u8 kind, u64 total size
            if total > 64 * 1024 * 1024 or (raw[0] == 0 and total + 24 > len(raw)):
                return None  # not a blob (would make decode_blob allocate garbage sizes)
            raw = decode_blob(raw)
            if not raw or len(raw) < 12:
                return None
        plen, cnt, voff = struct.unpack_from("<III", raw, 0)
        if plen != len(raw) or cnt > 10000:
            return None
        codes = struct.unpack_from("<%dI" % cnt, raw, 12)
        q = 12 + 4 * cnt
        keys = []
        for _ in range(cnt):
            n, q = varint(raw, q)
            keys.append(bytes(raw[q:q + n - 1]).decode("utf-8", errors="replace"))
            q += max(n - 1, 0)
        out, v = {}, voff
        for key, c in zip(keys, codes):
            # type code = expando code (attrs.EXPANDO_CODES) or, above them, a metamodel type id (DEPENDENCIES.md)
            c &= 0x7fffffff
            if c in _MAP_FIXED:
                fmt = _MAP_FIXED[c]
                out[key] = struct.unpack_from(fmt, raw, v)[0]
                v += struct.calcsize(fmt)
            elif c in (0x0e, 0x0d):  # xml / blob: u32 offset (value area) of varint(n+1) + bytes, like 0x0b
                n, s = varint(raw, voff + struct.unpack_from("<I", raw, v)[0])
                b = bytes(raw[s:s + n - 1])
                out[key] = b.decode("utf-8", errors="replace") if c == 0x0e else "hex:" + b.hex()
                v += 4
            elif c in (0x05, 0x13):
                out[key] = struct.unpack_from("<i", raw, v)[0]
                v += 4
            elif c == 0x0c:
                out[key] = str(uuid.UUID(bytes_le=bytes(raw[v:v + 16])))
                v += 16
            elif c == 0x01:  # bool, 1 byte (control column / toolbar lists, CONTROLS.md)
                out[key] = bool(raw[v])
                v += 1
            elif c == 0x0b:
                n, s = varint(raw, voff + struct.unpack_from("<I", raw, v)[0])
                out[key] = bytes(raw[s:s + n - 1]).decode("utf-8", errors="replace")
                v += 4
            elif c == 0x15:  # nested map: u32 offset (relative to the value area) of a pe blob with a MapT (CONTROLS.md)
                out[key] = decode_map(bytes(raw[voff + struct.unpack_from("<I", raw, v)[0]:]))
                v += 4
            elif c == 0xcd4001:  # metamodel type id of CoreMapArray: u32 offset of an array of maps (CONTROLS.md)
                o = voff + struct.unpack_from("<I", raw, v)[0]
                out[key] = decode_map_array(bytes(raw[o:o + struct.unpack_from("<I", raw, o)[0]]))
                v += 4
            elif c > 0xffff and voff + struct.unpack_from("<I", raw, v)[0] + 4 <= len(raw) and \
                    struct.unpack_from("<I", raw, voff + struct.unpack_from("<I", raw, v)[0])[0] <= len(raw):
                # other metamodel type: u32 offset of a block whose first u32 is its length. 8-byte blocks hold one u32
                # (0x1600d color, F40 TankColor 0xFFA3BABB); arrays of maps (0xbe3009 range entries) or of strings
                # (0x1400b tag references) are decoded; anything else (0x66002 DependencyIdentifierValue) stays raw
                o = voff + struct.unpack_from("<I", raw, v)[0]
                blk = bytes(raw[o:o + struct.unpack_from("<I", raw, o)[0]])
                arr = decode_map_array(blk) if len(blk) > 8 else []
                strs = _string_array(blk) if len(blk) > 8 else None
                if len(blk) == 8:
                    out[key] = struct.unpack_from("<I", blk, 4)[0]
                elif strs is not None:
                    out[key] = strs
                elif arr and all(isinstance(x, dict) for x in arr):
                    out[key] = arr
                else:
                    out[key] = "type %#x hex:%s" % (c, blk.hex())  # JSON-safe, like OpenPEData --json
                v += 4
            else:
                out[key] = ("code %#x" % c, None)  # not decoded yet (0x72, 0x76, 0x31d00 seen, CONTROLS.md)
                return out
        return out
    except (struct.error, IndexError, ValueError):
        return None


def _string_array(raw):
    """Array of strings (u32 length, u32 count, count x u32 offset, elements varint(n+1) + UTF-8) or None."""
    try:
        ln, cnt = struct.unpack_from("<II", raw, 0)
        if ln != len(raw) or cnt == 0 or 8 + 4 * cnt > ln:
            return None
        offs = list(struct.unpack_from("<%dI" % cnt, raw, 8)) + [ln]
        out = []
        for i in range(cnt):
            n, q = varint(raw, offs[i])
            if n < 1 or q + n - 1 != offs[i + 1]:
                return None
            out.append(bytes(raw[q:q + n - 1]).decode("utf-8", errors="replace"))
        return out
    except (struct.error, IndexError):
        return None


def decode_map_array(raw):
    """CoreMapArray (Array of pe:MapT): u32 length, u32 count, count x u32 element offset, elements -> [map]."""
    if not isinstance(raw, (bytes, bytearray)) or len(raw) < 8:
        return []
    try:
        ln, cnt = struct.unpack_from("<II", raw, 0)
        if ln > len(raw) or 8 + 4 * cnt > ln:
            return []
        offs = list(struct.unpack_from("<%dI" % cnt, raw, 8)) + [ln]
        return [decode_map(bytes(raw[offs[i]:offs[i + 1]])) for i in range(cnt)]
    except struct.error:
        return []


def _ref(p, k, entry, set_name="IReferenceExpandoAttributes"):
    """Object referenced by an IReferenceExpandoAttributes (or IPropertyDynamicLinkExpandoAttributes) entry of k: the
    value (u32 big endian + 1 byte, as H-031) indexes the ExpandoReferences links of k in stored order, empty entries
    (0000:0000:0) included."""
    v = p.attributes(k)[0].get(set_name, {}).get(entry) if isinstance(entry, str) else None
    if not isinstance(v, (bytes, bytearray)) or len(v) < 4:
        return None
    refs = [l.key for l in p.links(k, "ExpandoReferences")]
    idx = int.from_bytes(v[:4], "big")
    return refs[idx] if idx < len(refs) and refs[idx] in p.objects else None


def _interface(m, c):
    """IFaceplateInterface of ILFaceplateTypeData c (F05): TagInterfaceProperties = one map per interface tag
    {Name, DataType (code), _BlDataType -> DataTypeItemData, Tag -> HmiTagData, _TypeId, _Guid}; _BlDataType and Tag
    are names of IReferenceExpandoAttributes entries of c. The tags live in a tag table of their own
    (-ILFaceplateToTagTable->, named by a GUID) in the version's hidden device. InterfaceProperties / InterfaceEvents
    are decoded the same way but not yet checked against a sample (raw maps)."""
    p = m.p
    o = p.objects[c]
    fi = p.attributes(c)[0].get("IFaceplateInterface", {})
    tags = []
    for e in decode_map_array(fi.get("TagInterfaceProperties")):
        e = dict(e or {})
        tag = _ref(p, c, e.pop("Tag", None))
        dt = _ref(p, c, e.pop("_BlDataType", None))
        d = {"name": e.pop("Name", None), "dataType": p.name(dt) if dt else None, "dataTypeCode": e.pop("DataType", None),
             "tag": p.name(tag) if tag else None,
             "acquisitionCycle": p.name(m.rel1(tag, "IHmiTagAttributes_AcquisitionCycle")) if tag else None,
             "guid": e.pop("_Guid", None), "typeId": e.pop("_TypeId", None)}
        if "_SelectedStructureType" in e:  # structured interface tag (F40 fp_Motor, TempControl): PLC UDT references
            # _UDTStructureInLib -> version of the library type (UserTypeDataByCopyVersionObject), _UDTStructure -> its
            # content copy (UserTypeData named by a dependency key)
            d["structureType"] = e.pop("_SelectedStructureType")
            e.pop("_UDTStructure", None)
            lv = _ref(p, c, e.pop("_UDTStructureInLib", None))
            lt = m.rel1(lv, "VersionObjectToTypeObject") if lv else None
            d["udt"] = p.name(lt) if lt else None
            d["udtVersion"] = decode_version_text(p.attr(lv, "IVersionInfo", "Version")) if lv else None
        bounds = e.pop("_BLDataTypeBounds", None)
        if bounds is not None and d["dataTypeCode"] is not None and d["dataTypeCode"] & 0x40:
            # array: code = 0x40 | element type, bounds "low:high" (F12: Array [0..3] of Int = 67)
            d["elementType"] = d["dataType"]
            d["bounds"] = bounds
            d["dataType"] = "Array[%s] of %s" % (bounds.replace(":", ".."), d["elementType"])
        if e:
            d["other"] = e
        d["key"] = tag
        tags.append(d)
    table = m.rel1(c, "ILFaceplateToTagTable")
    return {"present": any(o.set_name(i) == "IFaceplateInterface" for i in o.blocks), "tags": tags,
            "tagTable": p.name(table) if table else None,
            "properties": [_iface_property(x) for x in decode_map_array(fi.get("InterfaceProperties"))],
            "events": [_iface_event(x) for x in decode_map_array(fi.get("InterfaceEvents"))]}


def _iface_event(e):
    """InterfaceEvents element (F18.md): {Name, _Guid, _TypeId -66604, InterfaceEventParameters: [{Name, DataType?,
    _Guid, _TypeId -66605}]}; the parameter data type is stored as its name and left out for Int (the default)."""
    e = dict(e or {})
    d = {"name": e.pop("Name", None), "guid": e.pop("_Guid", None), "typeId": e.pop("_TypeId", None),
         "parameters": [{"name": (x or {}).get("Name"), "dataType": (x or {}).get("DataType", "Int"),
                         "dataTypeStored": "DataType" in (x or {}), "guid": (x or {}).get("_Guid")}
                        for x in e.pop("InterfaceEventParameters", None) or []]}
    if e:
        d["other"] = e
    return d


def _iface_property(e):
    """InterfaceProperties element (F40, F16): {Name, DataType, Default?, _Guid, _TypeId}; DataType names in DATA_TYPES
    (all 11 property interface types of V21 in F16.md), color defaults as u32 ARGB."""
    e = dict(e or {})
    code = e.pop("DataType", None)
    d = {"name": e.pop("Name", None), "dataType": DATA_TYPES.get(code), "dataTypeCode": code,
         "default": e.pop("Default", None),
         "guid": e.pop("_Guid", None), "typeId": e.pop("_TypeId", None)}
    if d["dataTypeCode"] == 1282 and isinstance(d["default"], int):
        d["default"] = "0x%08X" % d["default"]
    if e:
        d["other"] = e
    return d


def _tag_name(m, k):
    """Name of a tag as Openness shows it: structure members with the path of their root tag (Tag.member)."""
    p = m.p
    parts = []
    while k and _short(p, k) == "HmiStructureMemberTagData":
        parts.append(p.name(k))
        k = m.rel1(k, "MembersInverse")
    if k:
        parts.append(p.name(k))
    return ".".join(reversed(parts)) or None


def _dynamization(m, k, entry):
    """IPropertyDynamicExpandoAttributes entry of container k (a MapT): Type (Tag, TagParameter, ResourceList, ...);
    Tag / ResourceList / TagRefs elements are names of IPropertyDynamicLinkExpandoAttributes entries ->
    ExpandoReferences; TagParameter: FormatString with '0', '1', ... standing for TagRefs (F40: HeaterControl'0' ->
    HeaterControl'Praefix')."""
    p = m.p
    d = decode_map(p.attributes(k)[0].get("IPropertyDynamicExpandoAttributes", {}).get(entry))
    if not isinstance(d, dict):
        return None

    def link(x):
        t = _ref(p, k, x, "IPropertyDynamicLinkExpandoAttributes")
        return _tag_name(m, t) if t and "Tag" in _short(p, t) else (p.name(t) if t else None)

    out = {"type": d.get("Type")}
    # bound to an interface property of the enclosing faceplate (nested container): its name
    if d.get("Type") == "FaceplateInterface" and d.get("PropertyName"):
        out["interfaceProperty"] = d["PropertyName"]
    if d.get("Tag"):
        out["tag"] = link(d["Tag"])
    if d.get("ResourceList"):
        out["resourceList"] = link(d["ResourceList"])
    if d.get("FormatString") is not None:
        names = [link(x) for x in d.get("TagRefs") or [] if isinstance(x, str)]
        out["formatString"] = d["FormatString"]
        out["dynamicTagName"] = re.sub(r"'(\d+)'", lambda mo: "'%s'" % (
            names[int(mo.group(1))] if int(mo.group(1)) < len(names) else mo.group(1)), d["FormatString"])
    if "TwoWay" in d:
        out["twoWay"] = d["TwoWay"]
    if isinstance(d.get("ValueConverter"), dict):
        out["valueConverter"] = d["ValueConverter"]
    return out


def _property_value(m, k, val):
    """Static value of an interface property: plain values as they are; a string naming an
    IReferenceExpandoAttributes entry is resolved: 5-byte index -> referenced object (text list, ...), CoreText ->
    {culture: text} (multilingual text, '' = default text)."""
    p = m.p
    refs = p.attributes(k)[0].get("IReferenceExpandoAttributes", {})
    if isinstance(val, str) and val in refs:
        rv = refs[val]
        if isinstance(rv, (bytes, bytearray)) and len(rv) == 5:
            if rv[4] & 0x80:
                # flag 0x80: the index counts the ExpandoChildren (ImageInstanceData -> library image type), as for
                # Graphic of screen items (interface property of type graphic, Openness "<image type> V 0.1.0")
                from .hmi import library_image_name  # late import: hmi imports this module
                kids = [l.key for l in p.links(k, "ExpandoChildren")]
                i = int.from_bytes(rv[:4], "big")
                return library_image_name(p, kids[i]) if i < len(kids) and kids[i] in p.objects else None
            t = _ref(p, k, val)
            return p.name(t) if t else None
        if isinstance(rv, (bytes, bytearray)):
            t = {c: x for c, x in m.core_text_of(rv).items() if c != "127"}
            return {"" if c == "65535" else c: x for c, x in t.items()}
        return rv
    return val.hex() if isinstance(val, (bytes, bytearray)) else val


def _instance_values(m, k):
    """Interface values of a faceplate instance (F40 Siemens demo project, not in the repo): expando Properties = MapT {_TypeId 900, _Guid, one entry
    per interface element, _#Dynamics {element: dynamization entry}}; an interface tag is a map {Tag -> reference
    expando -> ExpandoReferences -> HmiTagData / member, _TypeId 4710, _Guid, _#Dynamics}; an interface property
    holds its static value directly. Dynamizations: IPropertyDynamicExpandoAttributes (_dynamization)."""
    p = m.p
    d = decode_map(p.attributes(k)[0].get("IPropertyExpandoAttributes", {}).get("Properties"))
    if not isinstance(d, dict):
        return []
    dyn = d.get("_#Dynamics") or {}
    out = []
    for name, val in d.items():
        if name.startswith("_"):
            continue
        e = {"name": name}
        if isinstance(val, dict) and ("Tag" in val or val.get("_TypeId") == 4710):
            t = _ref(p, k, val.get("Tag"))
            e["kind"] = "tag"
            e["value"] = _tag_name(m, t) if t else None
            for de in (val.get("_#Dynamics") or {}).values():
                e["dynamization"] = _dynamization(m, k, de)
        else:
            e["kind"] = "property"
            e["value"] = _property_value(m, k, val)
        if name in dyn:
            e["dynamization"] = _dynamization(m, k, dyn[name])
        out.append(e)
    for name, de in dyn.items():  # dynamized only, no static value
        if name not in d:
            out.append({"name": name, "kind": "property", "value": None, "dynamization": _dynamization(m, k, de)})
    return out


def _code(b):
    """Script token table (as IScriptAttributes.Code, see Hmi.script) -> text."""
    if not isinstance(b, (bytes, bytearray)):
        return b
    try:
        _, cnt = struct.unpack_from("<II", b, 0)
        out = []
        for off in struct.unpack_from("<%dI" % cnt, b, 8):
            n, q = varint(b, off + 9)
            out.append(bytes(b[q:q + n - 1]).decode("utf-8", errors="replace"))
        return "".join(out)
    except (struct.error, IndexError):
        return None


def _script_module(m, c):
    """-ScriptModuleContainer-> HmiScriptModuleContainerData of the content (SCRIPTS.md): Scope 'Faceplate', global
    definition areas of events, script dynamizations and the function list (shared by all instances of the type)."""
    k = m.rel1(c, "ScriptModuleContainer")
    if not k:
        return None
    a = m.p.attributes(k)[0].get("IScriptModuleContainerAttributes", {})
    return {"scope": a.get("Scope"), "language": a.get("Language"),
            "globalDefinitions": {"events": _code(a.get("Event_GlobalSectionCode")),
                                  "dynamizations": _code(a.get("Trigger_GlobalSectionCode")),
                                  "functionList": _code(a.get("FunctionList_GlobalSectionCode"))}}


# property ids of map-valued expandos seen in faceplate types (CONTROLS.md); names are hypotheses from Openness
PART_NAMES = {0x0051: "Caption", 0x021c: "Font", 0x054e: "FontList"}


def _parts(p, k):
    """Map-valued property expandos of a screen item that hmi.py does not name (sub-objects such as the caption with
    its font, CONTROLS.md): {"0x0051 Caption": {...}}; values naming an IReferenceExpandoAttributes entry are
    resolved to the referenced object (e.g. FontList_C1939294067)."""
    from .hmi import PROPERTY_NAMES  # late import: hmi uses core_text from model
    ex = p.attributes(k)[0].get("IPropertyExpandoAttributes", {})
    refs = p.attributes(k)[0].get("IReferenceExpandoAttributes", {})

    def name(key):
        """Expando key -> ("0x0066 BackColor", kind); names from PART_NAMES, else hmi.PROPERTY_NAMES."""
        kb = key.encode("latin-1", errors="replace")
        pid = int.from_bytes(kb, "little")
        n, kind = PART_NAMES.get(pid), None
        if n is None and kb in PROPERTY_NAMES:
            n, kind = PROPERTY_NAMES[kb]
        return ("%#06x %s" % (pid, n or "")).strip(), kind

    def res(x):
        if isinstance(x, dict):
            out = {}
            for kk, vv in x.items():
                if kk.startswith("_"):
                    out[kk] = res(vv)
                    continue
                nm, kind = name(kk)
                # colors: 8-byte structure (u32 8, u32 ARGB) decoded as int by decode_map (F40 toolbar BackColor)
                out[nm] = "0x%08X" % vv if kind == "color" and isinstance(vv, int) else res(vv)
            return out
        if isinstance(x, list):
            return [res(v) for v in x]
        if isinstance(x, str) and x in refs:
            t = _ref(p, k, x)
            return p.name(t) if t else refs[x] if isinstance(refs[x], str) else None
        return x

    parts = {}
    for key, val in ex.items():
        if key.startswith("_") or key in ("Properties", LAYERS_PROPERTY) or not isinstance(val, (bytes, bytearray)):
            continue
        d = decode_map(val)
        if d is None or (isinstance(d, dict) and any(isinstance(x, tuple) for x in d.values())):
            # F33.md: some expandos are a CoreMapArray whose elements are pe blobs with a MapT (trend areas 0x3839);
            # read as a map they showed the blob header as type "code 0x31d00"
            arr = decode_map_array(val)
            if arr and all(isinstance(x, dict) for x in arr):
                d = arr
        if isinstance(d, (dict, list)):
            parts[name(key)[0]] = res(d)
    return parts


def _content(m, h, c):
    """ILFaceplateTypeData c: size, screen items (sorted by name) decoded like screen items (hmi.py Hmi.items: properties,
    text, tags, dynamizations, events with script code; scripts in the type have Scope Faceplate.Event /
    Faceplate.Trigger instead of Screen.*), global script definitions, interface."""
    p = m.p
    props = h.screen_properties(c)
    items = []
    # layers as on screens (Hmi.layers, same property 0x024b): items get layerIndex, zIndex and parent for drawing
    for it in h.items(c, h.layers(c)):
        ip = it["properties"]
        # nested faceplate (F30.md): container in the content -IsInstanceOf-> version of the inner type
        inner = m.rel1(it["key"], "IsInstanceOf") if it["type"] == "HmiFaceplateContainer" else None
        items.append({"name": it["name"], "type": it["type"],
                      "containedType": "V%s\\%s" % (decode_version_text(p.attr(inner, "IVersionInfo", "Version")),
                                                    p.name(m.rel1(inner, "VersionObjectToTypeObject")))
                      if inner else None,
                      "left": ip.get("Left", ip.get("CenterX")), "top": ip.get("Top", ip.get("CenterY")),
                      "width": it["width"], "height": it["height"], "text": it["text"], "tags": it["tags"],
                      "properties": ip, "parts": _parts(p, it["key"]), "dynamizations": it["dynamizations"],
                      # Faceplate.Trigger scripts are script dynamizations (listed there), not events
                      "events": [e for e in it["events"] if not str(e.get("kind")).endswith(".Trigger")],
                      "layerIndex": it["layerIndex"], "zIndex": it["zIndex"], "parent": it["parent"],
                      # interface values of a nested container; FaceplateInterface dynamizations name the property
                      # of the enclosing faceplate they take their value from (interfaceProperty)
                      **({"interface": _instance_values(m, it["key"])} if inner else {}),
                      "key": it["key"]})
    # TYPE-PROPERTIES.md: layers = CoreMapArray in property 0x024b ({_TypeId 20002, _Guid, 0x03 Name}, 32 entries as
    # on screens), active layer = reference expando named in _#ActiveLayer -> layer GUID; Properties = {_TypeId, _Guid}
    ex = p.attributes(c)[0].get("IPropertyExpandoAttributes", {})
    refs = p.attributes(c)[0].get("IReferenceExpandoAttributes", {})
    # F20.md: a layer with "Visible in runtime" off has field 0x07 = False in its map (absent = visible)
    # _#ESVisible False = layer hidden in the screen editor (as on screens, Hmi.layers; such a layer
    # is not shown by TIA ES)
    layers = [{"name": (x or {}).get("\x03"), "guid": (x or {}).get("_Guid"), "visible": (x or {}).get("\x07", True),
               "esVisible": (x or {}).get("_#ESVisible")}
              for x in decode_map_array(ex.get(LAYERS_PROPERTY))]
    active = refs.get(ex.get("_#ActiveLayer"))
    return {"width": props.get("Width"), "height": props.get("Height"), "items": items,
            # Suspendable: bool expando 0x0263 (F40 Faceplate_Suspendable True / _Not_Suspendable False, F20 True);
            # absent = default
            "suspendable": ex.get("c\x02"),
            # background color of the faceplate: expando 0x66 BackColor as on screens (F20, absent = default)
            "backColor": props.get("BackColor"),
            "layers": [x["name"] for x in layers],
            "hiddenLayers": [x["name"] for x in layers if x["visible"] is False],
            "esHiddenLayers": [x["name"] for x in layers if x["esVisible"] is False],
            "activeLayer": next((x["name"] for x in layers if x["guid"] == active), active),
            "properties": decode_map(ex.get("Properties")),
            "scriptModule": _script_module(m, c), "interface": _interface(m, c), "key": c}


def _local_tag(m, k):
    """Local tag of a faceplate version (F15.md): name, data type, table, cycle, limits (IHmiTagStructureAttributes
    Limit*Type / Limit*Constant, NoLimit = none) and start value."""
    p = m.p
    a = p.attributes(k)[0].get("IHmiTagStructureAttributes", {})
    limits = {n: a.get("Limit%sConstant" % n) for n in ("Low", "LowHigh", "HighLow", "High")
              if a.get("Limit%sType" % n) not in (None, "NoLimit")}
    return {"name": p.name(k), "dataType": p.name(m.rel1(k, "DataTypeRef")) if m.rel1(k, "DataTypeRef") else None,
            "table": p.name(m.rel1(k, "TagTable")), "acquisitionCycle": p.name(m.rel1(k, "IHmiTagAttributes_AcquisitionCycle")),
            "limits": limits, "startValue": a.get("StartValue") or None, "key": k}


def _version(m, h, v):
    """One version of library.py (with keys) -> faceplate version dict."""
    p = m.p
    vk = v["key"]
    e = {f: v.get(f) for f in VERSION_FIELDS}
    if (e["finalizationTime"] or "").startswith("0001-"):
        e["finalizationTime"] = None  # not released
    mem = [k for k in v.get("copiedObjectKeys") or [] if k in p.objects]
    c = m.rel1(vk, "VersionToContent")
    e["content"] = _content(m, h, c) if c else None
    shells = [k for k in mem if _short(p, k) == "HmiTagData" and not m.rel1(k, "TagTable")]
    e["unresolvedTags"] = sorted(p.name(k) for k in shells)
    scripts = [k for k in mem if _short(p, k) in ("HmiScriptEmbeddedData", "HmiScriptModuleContainerData")]
    e["scripts"] = sum(1 for k in scripts if _short(p, k) == "HmiScriptEmbeddedData")
    e["environmentObjects"] = len(mem)
    # fixed core of the hidden device = all members except content dependent objects (HIDDEN-DEVICE.md)
    var = {it["key"] for it in (e["content"] or {}).get("items", [])} | set(shells) | set(scripts)
    var |= {k for k in mem if _short(p, k) == "HmiFontListData" and _FONT_LIST_CONTENT.match(p.name(k) or "")}
    # content dependent object types seen in F40 (Siemens demo project, not in the repo): HMI copies of PLC UDTs of structured interface tags,
    # graphics / text list instances, script function lists, link maintainers, mediators
    var |= {k for k in mem if _short(p, k) in CONTENT_TYPES}
    table = m.rel1(c, "ILFaceplateToTagTable") if c else None  # interface tag table and its tags (F05)
    if table:
        var |= {table} | set(p.targets(table, "Content"))
    # local tags (F15): HmiTagData of the version in another tag table of the hidden device (Default tag table)
    local = [k for k in mem if _short(p, k) == "HmiTagData" and m.rel1(k, "TagTable") and m.rel1(k, "TagTable") != table]
    var |= set(local)
    e["localTags"] = [_local_tag(m, k) for k in sorted(local, key=lambda x: p.name(x) or "")]
    e["standardObjects"] = sum(1 for k in mem if k not in var)
    e["instances"] = v.get("instances") or []
    # project languages of the version (F32.md): CoreCulture names like "de-DE(1031)" -> "de-DE"
    cult = lambda k: (p.name(k) or "").split("(")[0] or None  # noqa: E731
    e["cultures"] = {"reference": cult(m.rel1(vk, "ReferenceCulture")) if m.rel1(vk, "ReferenceCulture") else None,
                     "editing": cult(m.rel1(vk, "EditingCulture")) if m.rel1(vk, "EditingCulture") else None,
                     "used": sorted(cult(k) for k in p.targets(vk, "UsedCultures"))}
    # F30.md: a typed nested faceplate makes the outer version -DependsOn-> the inner version (no
    # DependencyIdentifiers as with PLC types); library.py resolves both directions
    e["dependsOn"] = ["%s %s" % (x.get("typeName"), x.get("version")) for x in v.get("dependsOn") or []]
    e["usedBy"] = ["%s %s" % (x.get("typeName"), x.get("version")) for x in v.get("usedBy") or []]
    e["key"] = vk
    return e


def _geometry(h, k):
    """Position and size of a container plus its interface values and used tags (F40 Siemens demo project, not in the repo)."""
    ip = h.screen_properties(k)
    m = h.m
    d = {f: ip.get(n) for f, n in (("left", "Left"), ("top", "Top"), ("width", "Width"), ("height", "Height"))}
    d["interface"] = _instance_values(m, k)
    d["usedTags"] = [_tag_name(m, t) for t in m.p.targets(k, "ILFaceplateContainer_UsedTag")]
    # event handlers of the instance, e.g. On<interface event> (F18b): scripts like those of screen items; a function
    # list (F18c) is a HmiFunctionListEmbeddedData with the same script attributes, its code generated by TIA
    d["events"] = []
    for e in m.p.targets(k, "EmbeddedScriptChildren"):
        ev = dict(h.script(e), name=m.p.name(e))
        # Openness EventHandlers Async (F18c: function list True, script False)
        ev["async"] = m.p.attr(e, "IScriptSignatureAttributes", "IsAsync")
        if _short(m.p, e) == "HmiFunctionListEmbeddedData":
            ev["handler"] = "functionList"
            ev["functions"] = _function_calls(ev.get("code"))
        else:
            ev["handler"] = "script"
        d["events"].append(ev)
    return d


_SYSFCT_CALL = re.compile(r"HMIRuntime\.(\w+)\.SysFct\.(\w+)\((.*)\);\s*$")


def _function_calls(code):
    """System function calls of a function list (F18c.md): TIA writes one line per function,
    HMIRuntime.<Area>.SysFct.<Function>(<arguments>); -> [{"function", "area", "arguments"}] with the arguments as
    written (JavaScript literals)."""
    out = []
    for line in (code or "").splitlines():
        mt = _SYSFCT_CALL.search(line.strip())
        if not mt:
            continue
        args, cur, quote = [], "", None
        for ch in mt.group(3):
            if quote:
                cur += ch
                quote = None if ch == quote and not cur.endswith("\\" + ch) else quote
            elif ch in "\"'":
                cur, quote = cur + ch, ch
            elif ch == ",":
                args.append(cur.strip())
                cur = ""
            else:
                cur += ch
        if cur.strip():
            args.append(cur.strip())
        out.append({"function": mt.group(2), "area": mt.group(1), "arguments": args})
    return out


def _mediators(m):
    """FaceplateInstanceMediatorData: a typed reference from a script to a faceplate version (-ExpandoParent->
    HmiScriptEmbeddedData, e.g. a popup opened in OnTapped; -IsInstanceOf-> the referenced version). The script lies
    inside a faceplate version (Siemens demo project, not in the repo) or on a screen (F36.md). In the script code the
    type is a placeholder: the IScriptLinkExpandoAttributes entry of that name has flag 0x80 and indexes ExpandoChildren
    (empty entries included) instead of ExpandoReferences; Openness shows it as <type>_V_<version with _>.
    -> {referenced version key: [{"type", "version", "screen", "script", "item", "placeholder", "opennessName"}]}"""
    p = m.p
    out = {}
    for k in p.objects:
        if _short(p, k) != "FaceplateInstanceMediatorData":
            continue
        target = m.rel1(k, "IsInstanceOf")
        env = m.rel1(k, "Environment")
        in_version = bool(env) and _short(p, env) == "FaceplateLibraryVersionData"
        owner = m.rel1(env, "VersionObjectToTypeObject") if in_version else None
        scr = m.rel1(k, "ExpandoParent")
        item = m.rel1(scr, "EmbeddedScriptParent") if scr else None
        screen = m.rel1(item, "ILScreenItemParent") if item and not in_version else None
        placeholder = None
        if scr:
            children = [l.key for l in p.links(scr, "ExpandoChildren")]
            for name, v in p.attributes(scr)[0].get("IScriptLinkExpandoAttributes", {}).items():
                if isinstance(v, (bytes, bytearray)) and len(v) > 4 and v[4] & 0x80:
                    idx = int.from_bytes(v[:4], "big")
                    if idx < len(children) and children[idx] == k:
                        placeholder = name
        ttype = m.rel1(target, "VersionObjectToTypeObject") if target else None
        tver = decode_version_text(p.attr(target, "IVersionInfo", "Version")) if target else None
        out.setdefault(target, []).append({
            "type": p.name(owner) if owner else None,
            "version": decode_version_text(p.attr(env, "IVersionInfo", "Version")) if in_version else None,
            "screen": p.name(screen) if screen else None,
            "script": p.name(scr) if scr else None, "item": p.name(item) if item else None,
            "placeholder": placeholder,
            "opennessName": "%s_V_%s" % (p.name(ttype), tver.replace(".", "_")) if ttype and tver else None,
            "key": k})
    return out


def _instances(m, h, lib):
    """Faceplate containers -> (typed: type key -> [instance], without type: [instance]); position and size from the
    container expandos (Openness itemGet Left/Top/Width/Height)."""
    p = m.p
    typed, seen, untyped = {}, set(), []
    vers = {v.get("versionGuid"): t["key"] for L in lib["libraries"] for t in L["types"] for v in t["versions"]}
    for i in lib.get("instances", []):
        k = i.get("key")
        if k not in p.objects or _short(p, k) != CONTAINER_TYPE:
            continue
        seen.add(k)
        scr = m.rel1(k, "ILScreenItemParent")
        # ContainedType as Openness shows and itemCreate accepts it: V<version>\<type name> (F05)
        ct = ["V%s\\%s" % (x.get("version"), x.get("typeName")) for x in i.get("instanceOf") or []]
        inst = {"name": i["name"], "screen": p.name(scr) if scr else None, "device": i.get("device"),
                "containedType": ct[0] if len(ct) == 1 else ct or None,
                "instanceOf": i.get("instanceOf"), "versionState": i.get("versionState"), **_geometry(h, k), "key": k}
        tk = {vers.get(x.get("versionGuid")) for x in i.get("instanceOf") or []} - {None}
        for t in tk or {None}:
            (typed.setdefault(t, []) if t else untyped).append(inst)
    for k in sorted(p.objects):
        if k in seen or _short(p, k) != CONTAINER_TYPE:
            continue
        env = m.rel1(k, "Environment")
        if env and _short(p, env) == "FaceplateLibraryVersionData":
            continue  # nested container inside a faceplate type, listed with the version's content
        scr = m.rel1(k, "ILScreenItemParent")
        untyped.append({"name": p.name(k), "screen": p.name(scr) if scr else None, "device": None,
                        "containedType": None, "instanceOf": None, "versionState": None, **_geometry(h, k),
                        "key": k})
    return typed, untyped


def extract_faceplates(m, keys=False):
    """Faceplate types (versions, content, hidden device counts, instances) of an openpedata Model.
    keys=True keeps record keys for debugging."""
    from .hmi import Hmi  # late imports: hmi / library use core_text from model
    from .library import extract_library
    from .model import _strip
    p = m.p
    if not any(_short(p, k) in (FACEPLATE_TYPE, CONTAINER_TYPE) for k in p.objects):
        return []
    h = Hmi(m)
    lib = extract_library(p, keys=True)
    typed, untyped = _instances(m, h, lib)
    med = _mediators(m)
    out = []
    for L in lib["libraries"]:
        for t in L["types"]:
            if t["type"] != FACEPLATE_TYPE:
                continue
            d = {f: t.get(f) for f in TYPE_FIELDS}
            d["library"] = L["name"]
            d["alwaysDownload"] = p.attr(t["key"], "IHmiByRefTypeObjectBaseAttributes", "AlwaysDownload")
            d["versions"] = [_version(m, h, v) for v in t["versions"]]
            for e in d["versions"]:
                e["referencedBy"] = med.get(e["key"], [])  # mediators: typed references from other faceplates
            d["instances"] = typed.get(t["key"], [])
            codes = {x["name"]: x["dataTypeCode"] for v in d["versions"] if v["content"]
                     for x in v["content"]["interface"]["properties"]}
            for i in d["instances"]:  # color values as Openness shows them (0xAARRGGBB)
                for e in i["interface"]:
                    if codes.get(e["name"]) == 1282 and isinstance(e.get("value"), int):
                        e["value"] = "0x%08X" % e["value"]
            d["key"] = t["key"]
            out.append(d)
    if untyped:
        out.append({"name": None, "versions": [], "instances": untyped})
    if not keys:
        _strip(out)
    return out


# --- text output ----------------------------------------------------------------------------------
def to_text(types):
    out = []
    for t in types:
        if t["name"] is None:
            for i in t["instances"]:
                out.append("Faceplate instance %s on %s (no type)" % (i["name"], i["screen"]))
            continue
        out.append("Faceplate type %s  guid %s%s%s  default %s" % (
            t["name"], t["typeGuid"], "  [do not use]" if t["isObsolete"] else "",
            "  [always download]" if t["alwaysDownload"] else "", t["defaultVersion"]))
        for v in t["versions"]:
            out.append("  V %s %s%s  author %s%s  env %s objects (%s standard)" % (
                v["version"], v["state"], " default" if v["isDefault"] else "", v["author"],
                "  released " + v["finalizationTime"] if v["finalizationTime"] else "", v["environmentObjects"],
                v["standardObjects"]))
            c = v["content"]
            if c:
                out.append("    content %sx%s, interface %s, %d scripts%s%s%s" % (
                    c["width"], c["height"], "present" if c["interface"]["present"] else "empty", v["scripts"],
                    ", background %s" % c["backColor"] if c.get("backColor") else "",
                    ", suspendable %s" % c["suspendable"] if c.get("suspendable") is not None else "",
                    ", hidden in runtime: %s" % ", ".join(c["hiddenLayers"]) if c.get("hiddenLayers") else ""))
                itf = c["interface"]
                for x in itf["tags"]:
                    out.append("      interface tag %-16s %-10s -> tag %s in table %s, cycle %s%s" % (
                        x["name"], x["dataType"], x["tag"], itf["tagTable"], x["acquisitionCycle"],
                        ", %s %s %s" % (x["structureType"], x.get("udt"), x.get("udtVersion"))
                        if x.get("structureType") else ""))
                for x in itf["properties"]:
                    out.append("      interface property %-16s %s (%s)%s" % (
                        x["name"], x["dataType"], x["dataTypeCode"], ", default %r" % x["default"] if x["default"] is not None else ""))
                for x in itf["events"]:
                    out.append("      interface event %s(%s)" % (x["name"], ", ".join(
                        "%s: %s" % (q["name"], q["dataType"]) for q in x["parameters"])))
                for it in c["items"]:
                    out.append("      %-16s %-28s %s,%s%s%s%s" % (
                        it["name"], it["type"], it["left"], it["top"],
                        " %sx%s" % (it["width"], it["height"]) if it["width"] is not None else "",
                        " -> %s" % it["containedType"] if it.get("containedType") else "",
                        "  events " + ",".join(e.get("name") or "?" for e in it["events"]) if it["events"] else ""))
                    for dz in it["dynamizations"]:
                        if not dz.get("type"):
                            continue  # property with a change event only (listed under events)
                        out.append("        dynamization %s: %s%s" % (dz.get("property"), dz.get("type"),
                                                                    " " + dz["tag"] if dz.get("tag") else ""))
                        if dz.get("code"):
                            out += ["          | " + line for line in dz["code"].splitlines()]
                    for e in it["events"]:
                        out.append("        event %s (%s)" % (e.get("name"), e.get("kind")))
                        out += ["          | " + line for line in (e.get("code") or "").splitlines()]
                sm = c.get("scriptModule") or {}
                for area, code in (sm.get("globalDefinitions") or {}).items():
                    if code and "Insert definitions" not in code and code.strip() != "// [FunctionListModule]":
                        out.append("      global definitions (%s):" % area)
                        out += ["        | " + line for line in code.splitlines()]
            if v["unresolvedTags"]:
                out.append("    unresolved tags (shells): %s" % ", ".join(v["unresolvedTags"]))
            for x in v.get("localTags") or []:
                out.append("    local tag %-16s %-10s in %s, cycle %s%s" % (x["name"], x["dataType"], x["table"],
                                                                         x["acquisitionCycle"],
                                                                         ", limits %s" % x["limits"] if x["limits"] else ""))
            if v.get("dependsOn"):
                out.append("    depends on %s" % ", ".join(v["dependsOn"]))
            if v.get("usedBy"):
                out.append("    used by %s" % ", ".join(v["usedBy"]))
            for r in v.get("referencedBy") or []:
                where = "screen %s" % r["screen"] if r.get("screen") else "%s %s" % (r["type"], r["version"])
                out.append("    referenced by %s, script %s of %s as %s (mediator)" % (
                    where, r["script"], r["item"], r.get("opennessName")))
        for i in t["instances"]:
            out.append("  instance %s on %s (ContainedType %s)" % (i["name"], i["screen"], i["containedType"]))
            for e in i.get("events") or []:
                out.append("    event %s (%s%s)" % (e.get("name"), e.get("kind"),
                                                 ", function list" if e.get("handler") == "functionList" else ""))
                out += ["      | " + line for line in (e.get("code") or "").splitlines()]
            for e in i.get("interface") or []:
                dz = e.get("dynamization") or {}
                how = ""
                if dz:
                    how = "  <- %s %s" % (dz.get("type"), dz.get("dynamicTagName") or " ".join(
                        str(dz[f]) for f in ("tag", "resourceList") if dz.get(f)))
                out.append("    %-24s %-8s %s%s" % (e["name"], e["kind"], json.dumps(e.get("value"), ensure_ascii=False),
                                                   how))
    return "\n".join(out)


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        print(__doc__.split("\n\n")[1].strip(), file=sys.stderr)
        return 2
    from .model import Model
    d = extract_faceplates(Model(args[0]), keys="--keys" in argv)
    sys.stdout.reconfigure(encoding="utf-8")
    if "--json" in argv:
        print(json.dumps(d, indent=1, ensure_ascii=False, default=str))
    else:
        print(to_text(d))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
