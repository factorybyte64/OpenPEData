"""WinCC Unified styles in the PEData (docs/spec/hmi-styles.md):ILStyles.StyleData per device with the style values
per item type (IPropertyExpandoAttributes), font references (IReferenceExpandoAttributesSimple -> ExpandoReferences)
and the rendering templates (IRenderingTemplateExpandoContainer, SVG); StyleSettingsData names the selected style.
"""
import struct
import zlib

HDR_MARK = 0x10
# type code -> (name, inline size); size None = 4-byte offset into the variable part
TYPES = {
    0x80000001: ("bool", 1),
    0x80000002: ("uint8", 1),
    0x80000005: ("int32", 4),
    0x80000007: ("float", 4),
    0x80000008: ("double", 8),
    0x8000000B: ("string", None),
    0x8000000C: ("guid", 16),
    0x8000000D: ("bytes", None),
    0x80000012: ("uint16", 2),
    0x80000013: ("uint32", 4),
    0x80000015: ("object", None),
    0x0001600D: ("color", None),
}
META = {"_Guid", "_TypeId", "IsDefault", "StyleItemType"}


def _u32(b, o):
    return struct.unpack_from("<I", b, o)[0]


def _varint(b, o):
    v = s = 0
    while True:
        c = b[o]
        o += 1
        v |= (c & 0x7F) << s
        s += 7
        if not c & 0x80:
            return v, o


def _body(b, o):
    """u32 length, u32 n, u32 data offset, n type codes, n names (length+1 prefix), data part."""
    n, doff = _u32(b, o + 4), _u32(b, o + 8)
    codes = [_u32(b, o + 12 + 4 * i) for i in range(n)]
    p = o + 12 + 4 * n
    names = []
    for _ in range(n):
        ln = b[p]
        names.append(b[p + 1:p + ln].decode("utf-8"))
        p += ln
    d = o + doff
    if p != d:
        raise ValueError(f"names end at {p:#x}, data at {d:#x}")
    out, var, q = {}, [], d
    for code, name in zip(codes, names):
        if code not in TYPES:
            raise ValueError(f"unknown type code {code:#x} ({name})")
        kind, size = TYPES[code]
        if size is None:
            var.append((name, kind, d + _u32(b, q)))
            q += 4
            continue
        raw = b[q:q + size]
        q += size
        if kind == "bool":
            out[name] = bool(raw[0])
        elif kind == "int32":
            out[name] = struct.unpack("<i", raw)[0]
        elif kind == "float":
            out[name] = struct.unpack("<f", raw)[0]
        elif kind == "double":
            out[name] = struct.unpack("<d", raw)[0]
        elif kind == "guid":
            out[name] = raw.hex()
        else:
            out[name] = int.from_bytes(raw, "little")
    if var and min(a for _, _, a in var) != q:
        raise ValueError("variable part does not follow the fixed part")
    for name, kind, a in var:
        if kind == "string":
            ln = b[a]
            out[name] = b[a + 1:a + ln].decode("utf-8", "replace") if ln else None
        elif kind == "color":
            out[name] = "0x%08X" % _u32(b, a + 4)  # u32 8, then ARGB as little-endian u32
        elif kind == "bytes":
            out[name] = unwrap(b, a)
        else:
            out[name] = decode_object(b, a)
    return out


def unwrap(b, o):
    """Flag (0 plain, 1 zlib chunks), u32 payload length, 9 zero bytes, 0x10, u32 chunk count n, 1 + 4n bytes, then the
    payload (plain) or n chunks of varint length + zlib data (each 4096 bytes unpacked, the last one padded)."""
    flag, ln = b[o], _u32(b, o + 1)
    if b[o + 5:o + 14] != bytes(9) or b[o + 14] != HDR_MARK:
        raise ValueError(f"unexpected object header at {o:#x}")
    n = _u32(b, o + 15)
    p = o + 20 + 4 * n
    if flag == 0:
        return b[p:p + ln]
    data = b""
    for _ in range(n):
        zl, z = _varint(b, p)
        data += zlib.decompress(b[z:z + zl])
        p = z + zl
    if len(data) < ln:
        raise ValueError("unpacked length too short")
    return data[:ln]


def decode_object(b, o):
    return _body(unwrap(b, o), 0)


def decode_style_value(raw):
    """Expando value of a StyleData property (one object type): varint total length, then one object. Values that are
    not a blob (an expando can hold a plain bool, number or text) are returned unchanged; a blob that does not decode
    gives {"error", "hex"} instead of stopping the parser."""
    if not isinstance(raw, (bytes, bytearray, memoryview)):
        return raw
    raw = bytes(raw)
    try:
        return _decode_style_blob(raw)
    except (ValueError, IndexError, struct.error, zlib.error, UnicodeDecodeError) as e:
        return {"error": str(e), "hex": raw[:64].hex()}


def _decode_style_blob(raw):
    total, o = _varint(raw, 0)
    if total != len(raw):
        raise ValueError("length prefix differs")
    return decode_object(raw, o)


def strip_meta(v):
    if isinstance(v, dict):
        return {k: strip_meta(x) for k, x in v.items() if k not in META}
    return v


def references(P, k):
    """Reference expando of a StyleData: {4-character key: name of the linked object}. Value = u32 big-endian index
    into the ExpandoReferences links + 1 byte; font values (Font.Description) name such a key."""
    refs = P.attributes(k)[0].get("IReferenceExpandoAttributesSimple", {})
    links = P.targets(k, "ExpandoReferences")
    out = {}
    for key, v in refs.items():
        i = int.from_bytes(v[:4], "big")
        out[key] = P.name(links[i]) if i < len(links) else None
    return out


def resolve_fonts(v, refs):
    if isinstance(v, dict):
        if set(v) - META == {"Description"} and v["Description"] in refs:
            return dict(v, Description=refs[v["Description"]])
        return {k: resolve_fonts(x, refs) for k, x in v.items()}
    return v


def styles(P, fonts=True):
    """{style key: (name, system style name, {type: values})}."""
    res = {}
    for k in P.objects:
        if P.type_name(k) != "StyleData":
            continue
        props = P.attributes(k)[0].get("IPropertyExpandoAttributes", {})
        sysname = props.get("SystemStyleName")
        refs = references(P, k) if fonts else {}
        values = {}
        for t, v in props.items():
            if t == "SystemStyleName":
                continue
            values[t] = decode_style_value(getattr(v, "raw", v))
            if fonts:
                values[t] = resolve_fonts(values[t], refs)
        res[k] = (P.name(k), sysname, values)
    return res


def templates(P, k):
    """Rendering templates of a StyleData: {template name: SVG bytes}."""
    out = {}
    for name, v in P.attributes(k)[0].get("IRenderingTemplateExpandoContainer", {}).items():
        o = decode_style_value(getattr(v, "raw", v))
        out[name] = o.get("Content")
    return out


def settings(P):
    """[(settings key, device key, selected style key, [system style keys])]."""
    out = []
    for k in P.objects:
        if P.type_name(k) != "StyleSettingsData":
            continue
        dev = P.targets(k, "StyleSettingsParent")
        sel = P.targets(k, "IStyleSettingsAttributes_SelectedStyle")
        sys_ = P.targets(k, "IStyleSettingsAttributes_SystemStyles")
        out.append((k, dev[0] if dev else None, sel[0] if sel else None, sys_))
    return out


_ENUMS = None


def _enums():
    global _ENUMS
    if _ENUMS is None:
        import json
        import os
        with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "unified_enums.json"), encoding="utf-8") as f:
            _ENUMS = json.load(f)
    return _ENUMS


def enum_name(prop, value):
    """Openness name of an enum style value (numbers = Openness enum values, unified_enums.json); flags as
    "A, B" in value order like Openness; the number itself when the property is no known enum."""
    e = _enums()
    t = e["properties"].get(prop)
    if t is None or isinstance(value, bool) or not isinstance(value, int):
        return value
    en = e["enums"][t]
    names = {v: n for n, v in en["values"].items()}
    if value in names:
        return names[value]
    if en["flags"]:
        parts = [n for n, v in sorted(en["values"].items(), key=lambda x: x[1]) if v and value & v == v]
        if sum(en["values"][n] for n in parts) == value:
            return ", ".join(parts)
    return value


def enum_names(v):
    """enum_name for every member of a style value (nested objects by member name)."""
    if isinstance(v, dict):
        return {k: enum_names(x) if isinstance(x, dict) else enum_name(k, x) for k, x in v.items()}
    return v
