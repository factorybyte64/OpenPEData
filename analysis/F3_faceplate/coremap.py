"""F3 list item 2: decoder for pe:MapT values (element type of CoreMapArray / IFaceplateInterface) and a check over
all samples.

Layout (checked by main()):
  value  = pe blob (attrs.decode_blob): kind 0 = one uncompressed page with the 24-byte page header
           (u8 kind, u64 total, u32, u16 page size, u32 pages, varint 1, u32 bitmap), kind 1 = zlib page;
           the plain content is the map (this is the "24-byte blob header" of L1 DependencyIdentifiers)
  map:     u32 length (= len(payload)), u32 count, u32 offset of the value area (relative to payload start),
           count x u32 type code (bit 31 set), count x key (varint(n+1) + UTF-8), padding up to the value area,
           values in key order, size by type code
Usage: python -I analysis\\F3_faceplate\\coremap.py [sample dirs ...]   (default: all sample folders)
"""
import collections
import glob
import os
import struct
import sys
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
sys.path.insert(0, os.path.join(ROOT, "tools"))
import unified_tagdiff  # noqa: E402,F401  (H-023)
from openpedata import project  # noqa: E402
from openpedata.attrs import decode_blob, varint  # noqa: E402

HEADER = 24
# value sizes per type code (low bits); None = variable, read as varint(n+1) string or nested map
SIZES = {0x05: 4, 0x13: 4, 0x0c: 16}  # 0x0b = string (varint n+1), as expando code 11


def decode_map(raw, depth=0):
    """-> (dict key -> (code, value), problems[]). raw = attribute value as returned by Project.attributes."""
    probs = []
    if not isinstance(raw, (bytes, bytearray)) or not raw:
        return None, ["empty"]
    raw = decode_blob(raw) if raw[0] in (0, 1, 4) else None
    if raw is None or len(raw) < 12:
        return None, ["blob layout not recognised"]
    p0 = 0
    (plen, cnt, voff) = struct.unpack_from("<III", raw, 0)
    if plen != len(raw) or cnt > 100000:
        probs.append("length head %d / plain %d / count %d" % (plen, len(raw), cnt))
        return None, probs
    codes = struct.unpack_from("<%dI" % cnt, raw, p0 + 12) if cnt else ()
    q = p0 + 12 + 4 * cnt
    keys = []
    for _ in range(cnt):
        n, q = varint(raw, q)
        keys.append(raw[q:q + n - 1].decode("utf-8", errors="replace") if n else "")
        q += max(n - 1, 0)
    v = p0 + voff
    out = {}
    for k, c in zip(keys, codes):
        code = c & 0x7fffffff
        if not c & 0x80000000:
            probs.append("code without bit 31: %08x" % c)
        size = SIZES.get(code)
        if size == 4:
            val = struct.unpack_from("<i", raw, v)[0]
        elif size == 16:
            val = str(uuid.UUID(bytes_le=bytes(raw[v:v + 16])))
        else:
            # unknown: string attempt (varint n+1)
            n, q2 = varint(raw, v)
            if 0 < n <= p0 + plen - q2 + 1:
                val = ("str?", raw[q2:q2 + n - 1].decode("utf-8", errors="replace"))
                size = (q2 - v) + n - 1
            else:
                probs.append("unknown code %#x for key %r" % (code, k))
                return out, probs
        out[k] = (code, val)
        v += size
    if v != p0 + plen:
        probs.append("values end at %d, payload end %d" % (v - p0, plen))
    return out, probs


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    paths = sys.argv[1:] or sorted(p for p in glob.glob(os.path.join(ROOT, "samples", "*"))
                                   if os.path.isdir(p) and os.path.exists(os.path.join(p, "System", "PEData.plf")))
    stats = collections.Counter()
    codes = collections.Counter()
    examples = {}
    bad = []
    for s in paths:
        try:
            P = project.Project(s)
        except Exception:  # noqa: BLE001
            continue
        # the metamodel differs per project (packages are loaded on demand), so per project
        attrs = [(t.name, n) for t in P.mm.types.values() if t.kind == "AttributeSet"
                 for _, n, ty, _ in t.attributes if ty == "pe:MapT"]
        for k in P.objects:
            vals = P.attributes(k)[0]
            for sn, an in attrs:
                raw = vals.get(sn, {}).get(an)
                if not isinstance(raw, (bytes, bytearray)) or not raw:
                    continue
                m, pr = decode_map(raw)
                stats[(sn, an, "ok" if not pr else "problem")] += 1
                for key, (c, _) in (m or {}).items():
                    codes[c] += 1
                if pr and len(bad) < 15:
                    bad.append((os.path.basename(s), k, sn, an, pr, raw[:80].hex(" ")))
                if not pr and (sn, an) not in examples and m:
                    examples[(sn, an)] = (os.path.basename(s), k, m)
    for key in sorted(stats):
        print("%-34s %-28s %-8s %d" % (key[0], key[1], key[2], stats[key]))
    print("type codes:", {("%#x" % c): n for c, n in sorted(codes.items())})
    for (sn, an), (s, k, m) in sorted(examples.items()):
        print("example %s.%s  %s %s: %s" % (sn, an, s, k, str({kk: v for kk, v in list(m.items())[:6]})[:300]))
    for b in bad:
        print("PROBLEM", b)


if __name__ == "__main__":
    main()
