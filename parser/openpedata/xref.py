r"""XRef\XRef.db: cross-reference database (SQLite 3, derived from the plf, rebuildable).

Tables (all ids are rowids):
  info(id, version, savepointID)   version 10; savepointID = GUID of the XRef savepoint that the plf expects (record
                                   1006:0001:449, see pidx.external_stores); empty in a project that was never saved
  parts(id, binary)                object references: b"OG" + u16 typeLo + u16 typeHi + u64 object id (= the plf record key,
                                   14 bytes, rarely 15 with a trailing 01) or the single byte 00 (none); the part may point to an object that is deleted
  objs(id, scopeObject, primaryObject, subObject)
                                   a referable item: scope (the PLC), primary object (block, tag, type, instruction proxy,
                                   technology object) and a sub object: 00 or u32 member id inside the primary object
  rels(leftType, left, bindingObject, leftFilter, location, rightType, right, rightFilter, relationInfo)
                                   "left uses right": left/right are objs ids, bindingObject a part (none in all samples),
                                   location = path to the use site inside the left object: u32 depth, then per level
                                   u32 element id + u16 kind (e.g. 02000000 <u32 compile unit/element> 6510), or for members
                                   00000000 <u32 member id> 01 <u32> ...; relationInfo = u16 usage kind | u16 flags << 16
  addrs(obj, filter, type, range, firstBit, lastBit)
                                   absolute addresses of tags: type 0x1080, range 1 = input, 2 = output, 3 = memory, bit range
Type codes (leftType / rightType) seen: 0x1000 OB, 0x1010 FC, 0x1020 FB, 0x1060 instance DB, 0x1065 instruction,
0x1070 technology object, 0x1080 tag, 0x10a0 constant, 0x10b0 user data type, 0x1100 label, 0x1110 DB member,
0x1120 interface member, 0x1125 UDT member.
"""
import sqlite3
import struct
import tempfile


def _open(path):
    from . import pidx
    data = pidx._member(path, "XRef/XRef.db")  # noqa: SLF001
    if data is None:
        return None, None
    tmp = tempfile.TemporaryDirectory()
    fn = tmp.name + "/XRef.db"
    open(fn, "wb").write(data)
    return sqlite3.connect(fn), tmp


def part_key(b):
    """plf record key of a part blob, None for the empty part. 7 parts of WinCC Unified projects (103_hmi_tag_ext) have one
    more byte 01 behind the key (meaning not known)."""
    if b == b"\x00":
        return None
    if b[:2] != b"OG" or len(b) not in (14, 15):
        raise ValueError("unknown XRef part %s" % b.hex())
    lo, hi, oid = struct.unpack_from("<HHQ", b, 2)
    return "%04x:%04x:%x" % (lo, hi, oid)


def read(path):
    """XRef content of a project (folder, .ap21, .zap21): {"info", "parts", "objs", "rels", "addrs"} with parts resolved to
    plf keys; None without XRef.db."""
    con, tmp = _open(path)
    if con is None:
        return None
    try:
        info = con.execute("select version, savepointID from info").fetchone()
        parts = {i: part_key(b) for i, b in con.execute("select id, binary from parts")}
        objs = {i: {"scope": parts[s], "primary": parts[p], "member": struct.unpack_from("<I", sub)[0] if len(sub) == 4 else None}
                for i, s, p, sub in con.execute("select id, scopeObject, primaryObject, subObject from objs")}
        rels = [{"leftType": lt, "left": l, "rightType": rt, "right": r, "location": loc.hex(), "info": ri}
                for lt, l, _bo, _lf, loc, rt, r, _rf, ri in con.execute("select * from rels")]
        addrs = [{"obj": o, "type": t, "range": rg, "firstBit": fb, "lastBit": lb}
                 for o, _f, t, rg, fb, lb in con.execute("select * from addrs")]
        return {"version": info[0] if info else None, "savepointId": info[1] if info else None, "parts": parts, "objs": objs,
                "rels": rels, "addrs": addrs}
    finally:
        con.close()
        tmp.cleanup()


TYPE_NAMES = {0x1000: "OB",0x1010: "FC", 0x1020: "FB", 0x1060: "InstanceDB", 0x1065: "Instruction",
              0x1070: "TechnologyObject", 0x1080: "Tag", 0x10a0: "Constant", 0x10b0: "UDT", 0x1100: "Label",
              0x1110: "DBMember", 0x1120: "InterfaceMember", 0x1125: "UDTMember"}
RANGE_NAMES = {1: "Input", 2: "Output", 3: "Memory"}


def resolved(path, proj):
    """XRef content with object names from the project (option --xref): "left uses right" relations grouped by
    (user, used object, member, usage) with a count, and the tag addresses; "scope" is the PLC or HMI software. Relations
    whose user or used object is no longer in the plf (XRef.db keeps deleted objects) are only counted (staleRelations).
    Kinds the type code table does not name are given as the plf type (HmiTagData, ILScreenData ...). None without XRef.db."""
    d = read(path)
    if d is None:
        return None

    def obj(i):
        o = d["objs"].get(i) or {}
        k = o.get("primary")
        exists = k in proj.objects
        name = proj.name(k) if exists else None
        if exists and proj.type_name(k) == "IECPLInstructionProxyData":  # Name is the version GUID; Author holds e.g. R_TRIG
            name = proj.attr(k, "ICoreAttributes", "Author") or proj.attr(k, "IGenericInterfaceData", "InstanceDefaultName") or name
        return {"name": name, "objectType": proj.type_name(k) if exists else None,
                "scope": proj.name(o["scope"]) if o.get("scope") in proj.objects else None,
                "member": o.get("member"), "key": k, "deleted": k is not None and not exists}

    def live(i):
        return (d["objs"].get(i) or {}).get("primary") in proj.objects

    groups = {}
    stale = 0
    for r in d["rels"]:
        if not (live(r["left"]) and live(r["right"])):
            stale += 1
            continue
        g = (r["left"], r["right"], r["info"], r["leftType"], r["rightType"])
        groups[g] = groups.get(g, 0) + 1
    refs = []
    for (left, right, info, lt, rt), n in groups.items():
        a, b = obj(left), obj(right)
        refs.append({"user": a["name"], "userKind": TYPE_NAMES.get(lt) or a["objectType"] or hex(lt), "userMember": a["member"],
                     "used": b["name"], "usedKind": TYPE_NAMES.get(rt) or b["objectType"] or hex(rt), "usedMember": b["member"],
                     "usage": info & 0xFFFF, "flags": info >> 16, "count": n, "scope": a["scope"] or b["scope"],
                     "userKey": a["key"], "usedKey": b["key"]})
    refs.sort(key=lambda x: (x["scope"] or "", x["used"] or "", x["user"] or ""))
    addrs = []
    for a in d["addrs"]:
        o = obj(a["obj"])
        addrs.append({"tag": o["name"], "scope": o["scope"], "area": RANGE_NAMES.get(a["range"], a["range"]),
                      "firstBit": a["firstBit"], "lastBit": a["lastBit"], "key": o["key"]})
    return {"version": d["version"], "savepointId": d["savepointId"], "parts": len(d["parts"]), "objects": len(d["objs"]),
            "relations": len(d["rels"]), "staleRelations": stale, "references": refs, "addresses": addrs}
