r"""Integrity check of a TIA Portal V21 project without TIA and without any secret (option --verify).

Checked per plf file (System\PEData.plf and continuation files PEData-NN.plf):
  1. header hash: SHA-256 over bytes [0, 65) must equal bytes [65, 97), byte 97 = 0xFF
  2. every record: u32 length, last byte 0xFF, SHA-256 over the record = the 32 bytes behind it
  3. no bytes behind the last record (torn write)
  4. every $$COMMIT$ record (store type 0x00070014):
       chain hash  = SHA-256(previous chain hash || SHA-256 of every record since the previous commit in log order, CLOSE records
                     0x00070015 excluded); seed = header hash (bytes 65..97); the chain continues from the stored value
       signature   = RSA PKCS#1 v1.5 / SHA-256 over the first 128 bytes of the commit record, checked against the public key of the
                     key record 0016:0007:18 (CAPI PUBLICKEYBLOB, found by "RSA1"); the modulus is 2048 bit (pure Python pow())
  Commits of a project folder reference the previous commit by file offset (+78); after compaction into a .zap21 the first commit has -1.
Result: dict with "ok" (all checks passed) and per file the numbers. The private key is stored encrypted in the key record and is not read.
"""
import hashlib
import struct

from . import plf

COMMIT, CLOSE, KEY = 0x00070014, 0x00070015, 0x00070016
SHA256_DIGESTINFO = bytes.fromhex("3031300d060960864801650304020105000420")


def _sha(b):
    return hashlib.sha256(b).digest()


def public_key(rec):
    d = rec.data
    i = d.index(b"RSA1")
    bitlen, e = struct.unpack_from("<II", d, i + 4)
    n = int.from_bytes(d[i + 12:i + 12 + bitlen // 8], "little")
    return n, e, bitlen


def verify_plf(buf, name="PEData.plf"):
    res = {"file": name, "size": len(buf), "ok": True, "problems": []}

    def problem(msg):
        res["ok"] = False
        res["problems"].append(msg)

    if len(buf) < plf.RECORDS_START:
        problem("file shorter than the plf header")
        return res
    header_ok = _sha(buf[0:65]) == buf[65:97] and buf[97] == 0xFF
    res["headerHashOk"] = header_ok
    if not header_ok:
        problem("header hash mismatch")
    recs = plf.read_records(buf)
    res["records"] = len(recs)
    bad = [r.offset for r in recs if not r.hash_ok]
    res["recordHashErrors"] = bad[:20]
    if bad:
        problem("%d record hash mismatches (first at offset %d)" % (len(bad), bad[0]))
    ends_ff = [r.offset for r in recs if r.data[-1] != 0xFF]
    if ends_ff:
        problem("%d records without terminating 0xFF" % len(ends_ff))
    end = recs[-1].offset + recs[-1].length + 32 if recs else plf.RECORDS_START
    res["trailingBytes"] = len(buf) - end
    if len(buf) != end:
        problem("%d bytes behind the last record" % (len(buf) - end))
    rec_sha = lambda r: buf[r.offset + r.length:r.offset + r.length + 32]  # noqa: E731
    keys = [r for r in recs if r.type_id == KEY]
    n = e = None
    if keys:
        n, e, bitlen = public_key(keys[-1])
        res["key"] = {"record": keys[-1].key, "bits": bitlen, "exponent": e, "modulusPrefix": "%x" % n if False else ("%x" % n)[:16]}
    chain = buf[65:97]
    pending, commits = [], []
    for r in recs:
        if r.type_id == COMMIT:
            d = r.data
            (ticks,) = struct.unpack_from("<Q", d, 68)
            (prev_off,) = struct.unpack_from("<i", d, 78)
            field = d[96:128]
            c = {"offset": r.offset, "ticks": ticks, "recordsSince": len(pending), "prevCommitOffset": prev_off,
                 "chainOk": field == _sha(chain + b"".join(pending))}
            if n:
                m = pow(int.from_bytes(d[128:384], "big"), e, n).to_bytes(256, "big")
                z = m.find(b"\x00", 2)
                c["signatureOk"] = bool(m[:2] == b"\x00\x01" and set(m[2:z]) == {0xFF} and z >= 10
                                       and m[z + 1:] == SHA256_DIGESTINFO + _sha(d[:128]))
            else:
                c["signatureOk"] = False
            if not (c["chainOk"] and c["signatureOk"]):
                problem("commit at offset %d: chain %s, signature %s" % (r.offset, c["chainOk"], c["signatureOk"]))
            commits.append(c)
            chain = field
            pending = []
        elif r.type_id != CLOSE:
            pending.append(rec_sha(r))
    res["commits"] = commits
    res["uncommittedRecords"] = len(pending)  # records behind the last commit (open session)
    if not commits:
        problem("no commit record")
    return res


def verify_project(path):
    """Report for a project folder, .ap21 or .zap21 (all plf parts)."""
    from . import project
    parts = project._read_plf(path)  # noqa: SLF001
    files = [verify_plf(b, n) for n, b in parts]
    rep = {"path": path, "ok": all(f["ok"] for f in files), "files": files}
    from . import pidx
    idx = pidx.check_project(path)  # a derived cache: reported, does not decide "ok"
    if idx is not None:
        rep["idx"] = idx
    stores = pidx.external_stores(path)
    if stores:
        rep["externalStores"] = stores
    return rep


def text_report(rep):
    out = ["Verify %s: %s" % (rep["path"], "OK" if rep["ok"] else "FAILED")]
    for f in rep["files"]:
        out.append("  %s: %d records, header hash %s, record hashes %s, trailing bytes %d" % (
            f["file"], f.get("records", 0), "ok" if f.get("headerHashOk") else "BAD",
            "ok" if not f.get("recordHashErrors") else "BAD (%d)" % len(f["recordHashErrors"]), f.get("trailingBytes", 0)))
        k = f.get("key")
        if k:
            out.append("    key %s RSA-%d e=%d modulus %s..." % (k["record"], k["bits"], k["exponent"], k["modulusPrefix"]))
        for c in f.get("commits", []):
            out.append("    commit @%d: %d records, chain %s, signature %s, previous commit @%d" % (
                c["offset"], c["recordsSince"], "ok" if c["chainOk"] else "BAD", "ok" if c["signatureOk"] else "BAD", c["prevCommitOffset"]))
        if f.get("uncommittedRecords"):
            out.append("    %d records behind the last commit" % f["uncommittedRecords"])
        for p in f["problems"]:
            out.append("    PROBLEM: " + p)
    ix = rep.get("idx")
    if ix:
        out.append("  PEData.idx: %s (%d entries for %d plf records, %d commit copies)" % (
            "consistent with the plf" if ix["ok"] else "INCONSISTENT", ix["entries"], ix["records"], ix["commitCopies"]))
        for p in ix["problems"][:5]:
            out.append("    idx: " + p)
    for name, st in (rep.get("externalStores") or {}).items():
        out.append("  %s savepoint: plf %s, file %s (%s)" % (name, st["plf"], st["file"], "equal" if st["plf"] == st["file"] else "differs"))
    return "\n".join(out)
