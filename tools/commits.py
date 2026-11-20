"""H-008: verification of the $$COMMIT$ records (store type 0x00070014) in PEData.plf.

Findings (confirmed on all samples, project folders and .zap21):
  Key record 0016:0007:18 (store type 0x00070016) holds a CAPI PUBLICKEYBLOB at +97:
    06 02 0000 | aiKeyAlg 0x0000a400 (CALG_RSA_KEYX) | "RSA1" | bitlen 2048 | pubexp 65537 | modulus 256 B LE
  Commit record:
    +44  u32 payload length (340), repeated as trailer at +384
    +48  u32 1          +68 u64 DateTime ticks (UTC)    +76 u16 1
    +78  i32 plf offset of the previous commit record (-1: none, e.g. after compaction into .zap21)
    +82  string "$$COMMIT$" (varint n+1)   +92 u32 288 = length of the following hash + signature
    +96  32 B chain hash  = SHA-256(previous chain hash || SHA-256 of every record since the previous commit,
                            in log order, CLOSE records 0x00070015 excluded); seed = plf header hash (+65..+97)
    +128 256 B signature  = RSA PKCS#1 v1.5, SHA-256, big-endian, over commit bytes [0, 128)
         (record length, key, header, timestamp, previous-commit offset, chain hash)

Usage: python tools/commits.py [sample ...]   (default: every sample folder and .zap21)
Writes samples/<sample>_analysis/commits.json (folder) or commits_zap21.json (.zap21).
"""
import hashlib
import json
import os
import struct
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import plf  # noqa: E402

COMMIT, CLOSE, KEY = 0x00070014, 0x00070015, 0x00070016
SHA256_DIGESTINFO = bytes.fromhex("3031300d060960864801650304020105000420")


def sha(b):
    return hashlib.sha256(b).digest()


def public_key(rec):
    d = rec.data
    i = d.index(b"RSA1")
    bitlen, e = struct.unpack_from("<II", d, i + 4)
    n = int.from_bytes(d[i + 12:i + 12 + bitlen // 8], "little")
    return n, e, bitlen, i - 8


def verify(buf):
    recs = plf.read_records(buf)
    rec_sha = lambda r: buf[r.offset + r.length:r.offset + r.length + 32]
    keys = [r for r in recs if r.type_id == KEY]
    key = keys[-1] if keys else None
    n = e = None
    if key:
        n, e, bitlen, blob_off = public_key(key)
    chain = buf[65:97]  # header hash
    pending = []
    out = []
    for r in recs:
        if r.type_id == COMMIT:
            d = r.data
            ticks, prev_off = struct.unpack_from("<Qi", d, 68)[0], struct.unpack_from("<i", d, 78)[0]
            expect = sha(chain + b"".join(pending))
            field = d[96:128]
            res = {"key": r.key, "offset": r.offset, "records": len(pending), "prevCommitOffset": prev_off,
                   "chainOk": field == expect}
            if n:
                m = pow(int.from_bytes(d[128:384], "big"), e, n).to_bytes(256, "big")
                z = m.find(b"\x00", 2)
                res["paddingOk"] = m[:2] == b"\x00\x01" and set(m[2:z]) == {0xFF} and z >= 10
                res["signatureOk"] = res["paddingOk"] and m[z + 1:] == SHA256_DIGESTINFO + sha(d[:128])
            out.append(res)
            chain = field  # continue from the stored value
            pending = []
        elif r.type_id != CLOSE:
            pending.append(rec_sha(r))
    return {"records": len(recs), "keyRecord": key.key if key else None,
            "modulusPrefix": ("%x" % n)[:16] if n else None, "commits": out}


def load(src):
    if src.endswith(".zap21"):
        with zipfile.ZipFile(src) as z:
            return z.read(next(x for x in z.namelist() if x.lower() == "system/pedata.plf"))
    with open(os.path.join(src, "System", "PEData.plf"), "rb") as f:
        return f.read()


def main(args):
    sdir = os.path.join(ROOT, "samples")
    names = args or sorted(x for x in os.listdir(sdir) if os.path.isdir(os.path.join(sdir, x, "System")))
    total = ok = 0
    for s in names:
        for src, suffix in ((os.path.join(sdir, s), ""), (os.path.join(sdir, s + ".zap21"), "_zap21")):
            if not os.path.exists(src):
                continue
            r = verify(load(src))
            os.makedirs(os.path.join(sdir, s + "_analysis"), exist_ok=True)
            with open(os.path.join(sdir, s + "_analysis", "commits%s.json" % suffix), "w", encoding="utf-8") as f:
                json.dump(r, f, indent=1)
            c = r["commits"]
            total += len(c)
            good = sum(x["chainOk"] and x.get("signatureOk", False) for x in c)
            ok += good
            print("%-18s %-6s key %-14s modulus %s  commits %d  chain+signature ok %d  prevOffsets %s" % (
                s, suffix[1:] or "folder", r["keyRecord"], r["modulusPrefix"], len(c), good,
                [x["prevCommitOffset"] for x in c]))
    print("total commits %d, verified %d" % (total, ok))


if __name__ == "__main__":
    main(sys.argv[1:])
