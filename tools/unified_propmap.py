"""Derive the WinCC Unified property id table (expando dictionary names that are property ids, H-029) from
screen items whose Openness ground truth is known, and report how far the decoded values match the truth.

Usage: python -I tools\\unified_propmap.py [sample ...]   (default: F01_hmi_allobjects 115..124)
Truth: <sample>_truth\\HMI_1\\screen_*.full.json (itemGetFull per item) or screens_full.json (items[].attributes).
For every (property id, type code) the stored values of all items are decoded per code and compared with every
truth attribute of the same item (flattened, e.g. Font.Size); an attribute is a candidate when it equals the
decoded value in every occurrence. Prints: id, raw name bytes, code, occurrences, value kind, candidates.
"""
import collections
import glob
import json
import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import attrs as A  # noqa: E402
from openpedata.project import Project  # noqa: E402

SAMPLES = os.path.join(ROOT, "samples")


def raw_dicts(p):
    out = {}
    for r in p.latest.values():
        if r.type_id != 0x00070011:
            continue
        d = r.data
        _, _, tid, cnt, _ = struct.unpack_from("<IIIII", d, 44)
        o, res = 64, {}
        for _ in range(cnt):
            (eid,) = struct.unpack_from("<I", d, o)
            n, o = A.varint(d, o + 4)
            nm = d[o:o + n]
            o += n
            (code,) = struct.unpack_from("<I", d, o)
            o += 4
            res[eid] = (nm, code & 0x7FFFFFFF)
        out[tid] = res
    return out


def entries(p, dicts, k, set_suffix=".IPropertyExpandoAttributes"):
    o = p.objects[k]
    i = next((j for j, s in enumerate(o.set_names) if s.endswith(set_suffix)), None)
    if i is None or i not in o.blocks:
        return []
    a, e = o.blocks[i]
    b = o.data[a:e]
    if len(b) < 18:
        return []
    used, cnt, tid = struct.unpack_from("<I", b, 0)[0], struct.unpack_from("<I", b, 8)[0], struct.unpack_from("<I", b, 14)[0]
    if 18 + 8 * cnt > used:
        return []
    ids = struct.unpack_from("<%dI" % cnt, b, 18)
    offs = struct.unpack_from("<%dI" % cnt, b, 18 + 4 * cnt)
    base = 18 + 8 * cnt
    out = []
    for eid, off in zip(ids, offs):
        nm, code = dicts.get(tid, {}).get(eid, (b"", None))
        out.append((nm, code, off, b, base))
    return out


def decode(code, off, b, base):
    """Candidate decodings of one value: list of (kind, value)."""
    if code in (2, 3, 5, 7, 18, 19, 21):  # inline integers / enums
        return [("int", off), ("int-signed", struct.unpack("<i", struct.pack("<I", off))[0]), ("bool", bool(off))]
    res = []
    p = base + off
    if code == 0x00CD4002:  # list of points: u32 total, u32 count, count x u32 offset, entries u32 12, i32 x, i32 y
        try:
            _, cnt = struct.unpack_from("<II", b, p)
            offs = struct.unpack_from("<%dI" % cnt, b, p + 8)
            pts = [list(struct.unpack_from("<ii", b, p + o + 4)) for o in offs]
            res.append(("points", json.dumps([{"X": x, "Y": y} for x, y in pts], separators=(",", ":"))))
        except struct.error:
            pass
    try:
        n, q = A.varint(b, p)
        raw = b[q:q + n - 1] if n else b""
        if n == 8 and len(raw) == 7:
            res.append(("color", "0x%08X" % struct.unpack_from("<I", raw, 3)[0]))
        if 1 <= len(raw) <= 8:
            res.append(("i32", struct.unpack_from("<i", raw.ljust(4, b"\0"), 0)[0]))
        if len(raw) >= 8:
            res.append(("f64", struct.unpack_from("<d", raw, len(raw) - 8)[0]))
        try:
            s = raw.decode("utf-8")
            if s.isprintable():
                res.append(("str", s))
        except UnicodeDecodeError:
            pass
        res.append(("raw", raw.hex()))
    except (IndexError, struct.error):
        pass
    if len(b) >= p + 8:
        res.append(("f64@0", struct.unpack_from("<d", b, p)[0]))
        res.append(("u32@0", struct.unpack_from("<I", b, p)[0]))
    return res


def flat(d, prefix=""):
    out = {}
    for k, v in d.items():
        if isinstance(v, dict):
            out.update(flat(v, prefix + k + "."))
        elif isinstance(v, list):
            out[prefix + k] = json.dumps(v, separators=(",", ":"))
        else:
            out[prefix + k] = v
    return out


def norm(v):
    if isinstance(v, bool):
        return v
    if isinstance(v, float) and v.is_integer():
        return int(v)
    if isinstance(v, str) and v.startswith("0x"):
        return v.upper().replace("0X", "0x")
    return v


def truth_items(sample):
    d = os.path.join(SAMPLES, sample + "_truth", "HMI_1")
    res = {}
    for f in glob.glob(os.path.join(d, "screen_*.full.json")):
        j = json.load(open(f, encoding="utf-8-sig"))
        for name, it in (j.get("itemGetFull") or {}).items():
            res[name] = flat({k: v for k, v in it.items() if k not in ("compositions", "durationMs", "queueMs")})
    f = os.path.join(d, "screens_full.json")
    if os.path.exists(f):
        for o in json.load(open(f, encoding="utf-8-sig"))["objects"]:
            for it in o.get("items", []):
                if "attributes" in it:
                    res[it["name"]] = flat(it["attributes"])
    return res


def main():
    samples = sys.argv[1:] or ["F01_hmi_allobjects"] + [os.path.basename(x)[:-len("_truth")] for x in
                                                       sorted(glob.glob(os.path.join(SAMPLES, "11[5-9]_hmi_*_truth")))
                                                       + sorted(glob.glob(os.path.join(SAMPLES, "12[0-9]_hmi_*_truth")))]
    occ = collections.defaultdict(list)  # (name bytes, code) -> [(sample, item, decodings, truth dict)]
    for s in samples:
        truth = truth_items(s)
        if not truth:
            continue
        p = Project(os.path.join(SAMPLES, s))
        dicts = raw_dicts(p)
        for k in p.objects:
            if not (p.type_name(k) or "").endswith("ILScreenItemData"):
                continue
            name = p.attributes(k)[0].get("ICoreAttributes", {}).get("Name")
            if name not in truth:
                continue
            for nm, code, off, b, base in entries(p, dicts, k):
                occ[(nm, code)].append((s, name, decode(code, off, b, base), truth[name]))
    rows = []
    for (nm, code), lst in sorted(occ.items(), key=lambda x: (len(x[0][0]), x[0][0])):
        hits = collections.Counter()  # (attribute, kind) -> occurrences where it matches
        for _, _, decs, tr in lst:
            here = set()
            for kind, val in decs:
                for an, av in tr.items():
                    if isinstance(av, bool) == isinstance(val, bool) and norm(av) == norm(val):
                        here.add((an, kind))
            hits.update(here)
        best = [c for c, n in hits.most_common() if n == hits.most_common(1)[0][1]] if hits else []
        rate = hits.most_common(1)[0][1] if hits else 0
        rows.append((nm, code, len(lst), rate, best[:4], sorted({it for _, it, _, _ in lst})[:3], lst[0][2][:2]))
    print("| name bytes | code | n | hits | best candidates (attribute:value kind) | items (first) | first value |")
    print("| --- | --- | --- | --- | --- | --- | --- |")
    for nm, code, n, rate, best, items, first in rows:
        label = nm.hex() if len(nm) <= 3 else nm.decode("utf-8", "replace")
        print("| %s | %s | %d | %d | %s | %s | %s |" % (label, code, n, rate, ", ".join("%s:%s" % c for c in best) or "-",
                                                     ",".join(items), "; ".join("%s=%s" % (k, str(v)[:18]) for k, v in first)))


if __name__ == "__main__":
    main()
