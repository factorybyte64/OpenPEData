r"""System\PEData.idx: index of the record log System\PEData.plf (a derived cache, the plf alone is authoritative).

Layout (decoded from samples, all little endian):
  header        98 bytes, same layout as the plf header (magic u32 0x80 instead of 0x40, same project GUID), SHA-256 over bytes
                [0, 65) at 65..97, byte 97 = 0xFF
  entry         38 bytes, one per plf record (all versions, not only the current one; the session marker records "0015:0007:20"
                excepted). The order is NOT the log order: per commit interval the entries are grouped (store records, then the
                other records in log order), so entries are matched to records by the offset field:
                  +0   u16 typeLo, u16 typeHi, u64 object id        (the 12-byte record key)
                  +12  16 bytes record GUID                          (= plf record body bytes 28..43)
                  +28  u8  0
                  +29  u32 plf file offset of the record            (offset of the record's length field)
                  +33  i32 class code = plf record bytes 16..19 (store records 0007 with 0 -> -1)
                  +37  u8  flags (0; 0x40 / 0x80 for two record classes with plf field 20..23 = 7 / 11; 4 = deletion)
                A deleted object (49-byte tombstone record in the plf) gets an entry with flag 4, the GUID of the deleted object,
                class -1 and, if the object was created and deleted in the same session, offset 0xFFFFFFFF, otherwise a copy of the
                last live entry (same offset) with flag 4.
  commit copy   follows the entry of every commit record (0014:0007:*): u32 421 (size incl. itself), a copy of the commit record
                (same bytes except both length fields +32) with 32 extra bytes behind the chain hash (offset 128) and the
                256-byte block at offset 160, which IS an RSA signature of the project key (PKCS#1 v1.5, SHA-256 DigestInfo
                after decryption with the public key of the plf) over a message that is not known; trailer u32 0x174, 0xFF
  session mark  38 bytes after every commit copy (and at the end of the file): key 0015:0007:20 with a session GUID that is not
                a plf record GUID, offset field 0xFFFFFFFF, count field 0
"""
import collections
import hashlib
import struct

from . import plf
from .record import is_tombstone

ENTRY = 38
COMMIT_COPY = 421
SESSION_KEY = 0x0007_0015


def _entry(b, i):
    typ_lo, typ_hi, oid = struct.unpack_from("<HHQ", b, i)
    guid = bytes(b[i + 12:i + 28])
    flag, off, cls, fl = struct.unpack_from("<BIiB", b, i + 28)
    return {"key": "%04x:%04x:%x" % (typ_lo, typ_hi, oid), "guid": guid.hex(), "offset": off, "class": cls, "flags": fl,
            "_typeId": (typ_hi << 16) | typ_lo, "_pos": i}


def parse(buf):
    """Entries (log order), commit copies and session marks of an idx file."""
    res = {"size": len(buf), "headerOk": len(buf) >= 98 and hashlib.sha256(buf[:65]).digest() == buf[65:97] and buf[97] == 0xFF,
           "magic": struct.unpack_from("<I", buf, 0)[0], "guid": bytes(buf[8:24]).hex(), "entries": [], "commits": [],
           "sessions": [], "unparsed": 0}
    i = plf.RECORDS_START
    n = len(buf)
    while i < n:
        if i + 4 <= n and buf[i + 4:i + 6] == b"\x14\x00" and struct.unpack_from("<I", buf, i)[0] == COMMIT_COPY:
            res["commits"].append({"pos": i, "key": "%04x:%04x:%x" % struct.unpack_from("<HHQ", buf, i + 4)})
            i += COMMIT_COPY
        elif i + ENTRY <= n and buf[i:i + 4] == b"\x15\x00\x07\x00":
            res["sessions"].append(_entry(buf, i))
            i += ENTRY
        elif i + ENTRY <= n:
            res["entries"].append(_entry(buf, i))
            i += ENTRY
        else:
            res["unparsed"] = n - i
            break
    return res


def check(idx_buf, plf_buf):
    """Compares the idx with the plf. Returns {"ok", "problems", counts}."""
    res = {"ok": True, "problems": []}

    def problem(msg):
        res["ok"] = False
        if len(res["problems"]) < 20:
            res["problems"].append(msg)

    ix = parse(idx_buf)
    recs = [r for r in plf.read_records(plf_buf, verify=False) if r.type_id != SESSION_KEY]
    res.update(entries=len(ix["entries"]), records=len(recs), commitCopies=len(ix["commits"]), sessionMarks=len(ix["sessions"]))
    if not ix["headerOk"]:
        problem("idx header hash mismatch")
    if ix["unparsed"]:
        problem("%d unparsed bytes at the end" % ix["unparsed"])
    if len(ix["entries"]) > len(recs):
        problem("%d entries for %d plf records" % (len(ix["entries"]), len(recs)))
    live = [e for e in ix["entries"] if not e["flags"] & 4]
    dead = collections.Counter(e["key"] for e in ix["entries"] if e["flags"] & 4)
    by_off = {e["offset"]: e for e in live}
    if len(by_off) != len(live):
        problem("duplicate offsets in the entries")
    for r in recs:
        if is_tombstone(r):
            if not dead[r.key]:
                problem("no deletion entry for tombstone at %d (%s)" % (r.offset, r.key))
            else:
                dead[r.key] -= 1
            continue
        cls = struct.unpack_from("<i", r.data, 16)[0]
        if r.is_store and cls == 0 and r.type_id != 0x00070014:
            cls = -1
        e = by_off.get(r.offset)
        if e is None:
            if r.is_store:  # store records written while the project is closed are not indexed yet
                res["unindexedStore"] = res.get("unindexedStore", 0) + 1
            else:
                problem("no entry for plf record at %d (%s)" % (r.offset, r.key))
        elif (e["key"], e["guid"]) != (r.key, bytes(r.data[28:44]).hex()) or e["class"] not in (cls, struct.unpack_from("<i", r.data, 16)[0]):
            problem("entry at %d differs from plf record at %d (%s)" % (e["_pos"], r.offset, r.key))
    if sum(dead.values()):
        problem("%d deletion entries without tombstone" % sum(dead.values()))
    matched = sum(1 for r in recs if not is_tombstone(r) and r.offset in by_off)
    live = sum(1 for r in recs if not is_tombstone(r))
    res["stale"] = bool(live and matched < live // 2)  # plf compacted (archive/close) without rebuilt idx: offsets do not fit
    ncommit = sum(1 for r in recs if r.type_id == 0x00070014)
    if len(ix["commits"]) != ncommit:
        problem("%d commit copies for %d commit records" % (len(ix["commits"]), ncommit))
    res["commitCopySignatures"] = _copy_signatures(idx_buf, ix, recs)
    return res


def _copy_signatures(idx_buf, ix, recs):
    """Commit copies whose 256-byte block (offset 160) decrypts with the public key of the plf to a PKCS#1 v1.5 block with a SHA-256
    DigestInfo, i.e. is a genuine RSA signature of the project key. What exactly is signed is not known (the SHA-256 of
    the usual candidates - the copy's first 128/160 bytes, with original or enlarged length fields, the chain or the extra hash - does not
    match), so the digest itself cannot be checked. Returns {"total": n, "padded": k}."""
    from . import verify
    keys = [r for r in recs if r.type_id == verify.KEY]
    out = {"total": len(ix["commits"]), "padded": 0}
    if not keys or not ix["commits"]:
        return out
    n, e, _ = verify.public_key(keys[-1])
    for c in ix["commits"]:
        blk = idx_buf[c["pos"] + 160:c["pos"] + 416]
        if len(blk) < 256:
            continue
        m = pow(int.from_bytes(blk, "big"), e, n).to_bytes(256, "big")
        z = m.find(b"\x00", 2)
        if m[:2] == b"\x00\x01" and z >= 10 and set(m[2:z]) == {0xFF} and m[z + 1:z + 1 + len(verify.SHA256_DIGESTINFO)] == verify.SHA256_DIGESTINFO and len(m) - z - 1 == len(verify.SHA256_DIGESTINFO) + 32:
            out["padded"] += 1
    return out


def _member(path, name):
    """Bytes of System/<name> in a project folder, next to an .ap21 or in a .zap21; None if absent."""
    import os
    import zipfile
    if path.lower().endswith(".zap21"):
        with zipfile.ZipFile(path) as z:
            return z.read(name) if name in z.namelist() else None
    if path.lower().endswith(".ap21"):
        path = os.path.dirname(os.path.abspath(path))
    p = os.path.join(path, *name.split("/"))
    return open(p, "rb").read() if os.path.isfile(p) else None


def check_project(path):
    """idx check of a project folder or .ap21; None for a .zap21 (the archive carries the plf compacted but the idx of the
    folder it was made from, so the offsets do not match; TIA rebuilds the idx when it retrieves the archive) and without
    idx/plf."""
    if path.lower().endswith(".zap21"):
        return None
    idx, plf_buf = _member(path, "System/PEData.idx"), _member(path, "System/PEData.plf")
    if idx is None or plf_buf is None:
        return None
    return check(idx, plf_buf)


def external_stores(path):
    """Savepoints the plf (record 1006:0001:449, project history) expects for the stores outside the plf versus the ones found:
    {"xref": {"plf": guid, "file": guid}, "searchIndex": {"plf": guid, "file": guid}}. Informational: TIA refreshes these
    stores after the commit, so a mismatch is seen in a few saved projects."""
    import re
    import sqlite3
    import tempfile
    plf_buf = _member(path, "System/PEData.plf")
    out = {}
    if plf_buf is None:
        return out
    # project history record 1006:0001:<oid>: the oid differs between projects (449, 446 in the demo project), so look for the content
    recs = [r for r in plf.read_records(plf_buf, verify=False) if r.key.startswith("1006:0001:") and (b"XRef%" in r.data or b"SaveCommit_" in r.data)]
    txt = b" ".join(re.findall(rb"[ -~]{4,}", recs[-1].data)).decode("latin-1") if recs else ""
    m = re.search(r"XRef%([0-9a-f-]{36})", txt)
    s = re.search(r"SaveCommit_([0-9a-f-]{36})", txt)
    db = _member(path, "XRef/XRef.db")
    found = None
    if db:
        with tempfile.TemporaryDirectory() as t:
            fn = t + "/x.db"
            open(fn, "wb").write(db)
            con = sqlite3.connect(fn)
            try:
                row = con.execute("select savepointID from info").fetchone()
                found = row[0] if row else None
            finally:
                con.close()
    out["xref"] = {"plf": m.group(1) if m else None, "file": found}
    seg = None
    import os
    import zipfile
    if path.lower().endswith(".zap21"):
        with zipfile.ZipFile(path) as z:
            names = sorted(n for n in z.namelist() if re.fullmatch(r"IM/SearchIndex/segments_\w+", n))
            seg = z.read(names[-1]) if names else None
    else:
        base = os.path.dirname(os.path.abspath(path)) if path.lower().endswith(".ap21") else path
        d = os.path.join(base, "IM", "SearchIndex")
        names = sorted(n for n in (os.listdir(d) if os.path.isdir(d) else []) if re.fullmatch(r"segments_\w+", n))
        seg = open(os.path.join(d, names[-1]), "rb").read() if names else None
    sm = re.search(rb"SaveCommit_([0-9a-f-]{36})", seg) if seg else None
    out["searchIndex"] = {"plf": s.group(1) if s else None, "file": sm.group(1).decode() if sm else None}
    return out
