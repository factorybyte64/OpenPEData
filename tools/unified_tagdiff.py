"""U2 analysis of WinCC Unified HMI tag samples (base vs. sample), without changing the shared parser.

Usage: python -I tools\\unified_tagdiff.py <base> <sample> [--out file] [--dump TypeName ...]
- attrdiff.diff(base, sample) with a local workaround for H-023: expando blocks shorter than 8 bytes
  (e.g. IAdditionalAttributes of ReportStorageLocationData = 06 00 00 00 01 00) are reported as
  "short expando" instead of raising; parser\\openpedata\\attrs.py itself is not touched.
- --dump: every object of the given short type names in the sample with all decoded sets and all links
  (relation name, target key, target type, target name).
- H-022: files outside System\\PEData.plf that differ between base and sample (SHA-256), and every file
  (incl. PEData.plf) that contains the names given with --find in UTF-8 or UTF-16LE.
"""
import hashlib
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
sys.path.insert(0, os.path.join(ROOT, "tools"))
from openpedata import attrs as attrcodec  # noqa: E402
from openpedata import project as projmod  # noqa: E402

_orig = attrcodec.decode_expando


def _decode_expando(block, dictionaries):
    if len(block) < 8:
        return {}, ["short expando block (%d bytes): %s" % (len(block), block.hex(" "))]
    return _orig(block, dictionaries)


attrcodec.decode_expando = _decode_expando
projmod.attrcodec.decode_expando = _decode_expando

import attrdiff  # noqa: E402


def dump(P, short_types):
    out = []
    for k in sorted(P.objects):
        tn = P.type_name(k) or ""
        if tn.split(".")[-1] not in short_types:
            continue
        o = P.objects[k]
        out.append("OBJECT %s %s name=%r size=%d" % (k, tn, P.name(k), len(o.data)))
        vals, probs = P.attributes(k)
        for i in range(o.slot_count):
            name = o.set_name(i) if i < o.slot_count else "slot%d" % i
            b = o.blocks.get(i)
            out.append("  slot %2d %-55s %s" % (i, name, "%d bytes" % (b[1] - b[0]) if b else "-"))
        for s, vs in vals.items():
            for n, v in vs.items():
                out.append("    %s.%s = %s" % (s, n, attrdiff.short(v, 120)))
        for s, p in probs.items():
            out.append("    problem %s: %s" % (s, p))
        for l in o.links:
            r = P.mm.relations.get(l.relation)
            t = P.type_name(l.key) if l.key in P.objects else "(not an object)"
            out.append("    link %-40s -> %s %s name=%r" % (r.name if r else "typed slot %d" % l.slot, l.key, t,
                                                       P.name(l.key) if l.key in P.objects else None))
    return out


def files(root):
    r = {}
    for d, _, fs in os.walk(root):
        for f in fs:
            p = os.path.join(d, f)
            r[os.path.relpath(p, root).replace("\\", "/")] = p
    return r


def h022(base, sample, names):
    out = ["H-022: files outside System/PEData.plf, base %s vs. %s (SHA-256, first 12)" % (base, sample)]
    A, B = files(os.path.join(ROOT, "samples", base)), files(os.path.join(ROOT, "samples", sample))
    for rel in sorted(set(A) | set(B)):
        if rel == "System/PEData.plf":
            continue
        ha = hashlib.sha256(open(A[rel], "rb").read()).hexdigest()[:12] if rel in A else "-"
        hb = hashlib.sha256(open(B[rel], "rb").read()).hexdigest()[:12] if rel in B else "-"
        if ha != hb:
            out.append("  %-6s %s  %s -> %s" % ("NEW" if ha == "-" else "GONE" if hb == "-" else "CHG", rel, ha, hb))
    for n in names:
        pats = {"utf-8": n.encode("utf-8"), "utf-16le": n.encode("utf-16le")}
        hits = []
        for rel, p in sorted(B.items()):
            data = open(p, "rb").read()
            for enc, pat in pats.items():
                c = data.count(pat)
                if c:
                    hits.append("%s (%s x%d)" % (rel, enc, c))
        out.append("  name %r found in: %s" % (n, ", ".join(hits) or "nothing"))
    return out


def main():
    args = sys.argv[1:]
    outf, dumps, finds = None, [], []
    for flag, target in (("--out", None), ("--dump", dumps), ("--find", finds)):
        if flag in args:
            i = args.index(flag)
            j = i + 1
            while j < len(args) and not args[j].startswith("--"):
                j += 1
            vals = args[i + 1:j]
            del args[i:j]
            if flag == "--out":
                outf = vals[0]
            else:
                target.extend(vals)
    base, sample = args[0], args[1]
    lines = attrdiff.diff(base, sample)
    if dumps:
        P = projmod.Project(os.path.join(ROOT, "samples", sample))
        lines += ["", "DUMP %s" % ", ".join(dumps)] + dump(P, set(dumps))
    lines += [""] + h022(base, sample, finds)
    text = "\n".join(lines) + "\n"
    if outf:
        with open(outf, "w", encoding="utf-8") as f:
            f.write(text)
    sys.stdout.reconfigure(encoding="utf-8")
    print(text)


if __name__ == "__main__":
    main()
