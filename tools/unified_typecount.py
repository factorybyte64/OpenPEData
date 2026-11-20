"""Object count per metamodel type (current version per key, without store records): base vs. sample.

Usage: python -I tools\\unified_typecount.py <base> <sample> [--out file] [--focus Pkg,...]
<base>/<sample>: project folder, .ap21 or .zap21. Prints the metamodel change (new packages/types), every
type whose count differs, and for the focus packages (default: DL.ModernUI, Dynamics, DL.Dynamics, Scripting)
all types with their counts on both sides. H-023 workaround as in unified_tagdiff.py (attributes are not
decoded here, but Project() reads expando dictionaries).
"""
import argparse
import collections
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import project as projmod  # noqa: E402

FOCUS = ["DL.ModernUI", "Dynamics", "DL.Dynamics", "Scripting"]


def counts(P):
    c = collections.Counter()
    for k, o in P.objects.items():
        c[P.mm.types[o.rec.type_id].full_name] += 1
    return c


def pkg(mm, full):
    t = mm.by_name.get(full)
    return (t.package if t and t.package else "?")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("base")
    ap.add_argument("sample")
    ap.add_argument("--out")
    ap.add_argument("--focus", default=",".join(FOCUS))
    a = ap.parse_args()
    focus = [f.strip() for f in a.focus.split(",") if f.strip()]
    B, S = projmod.Project(a.base), projmod.Project(a.sample)
    cb, cs = counts(B), counts(S)
    out = []
    w = out.append
    w("%s -> %s" % (a.base, a.sample))
    w("objects %d -> %d, deleted %d -> %d, records %d -> %d" % (len(B.objects), len(S.objects), len(B.deleted),
                                                             len(S.deleted), len(B.records), len(S.records)))
    nb, ns = set(B.mm.by_name), set(S.mm.by_name)
    pb = set(t.package for t in B.mm.types.values())
    ps = set(t.package for t in S.mm.types.values())
    w("metamodel types %d -> %d, packages %d -> %d" % (len(B.mm.types), len(S.mm.types), len(pb), len(ps)))
    for p in sorted(x for x in ps - pb if x):
        w(" NEW PKG %s (%d types)" % (p, sum(1 for t in S.mm.types.values() if t.package == p)))
    for n in sorted(ns - nb):
        if S.mm.by_name[n].package in pb:
            w(" NEW TYPE in existing pkg %s" % n)
    w("")
    w("== types with different object count (base, sample, delta, package)")
    rows = []
    for n in sorted(set(cb) | set(cs)):
        if cb[n] != cs[n]:
            rows.append((pkg(S.mm, n) if n in S.mm.by_name else pkg(B.mm, n), n, cb[n], cs[n]))
    for p, n, x, y in sorted(rows):
        w("%-6s %-6s %+6d  %-45s %s" % (x, y, y - x, p, n))
    w("")
    for f in focus:
        names = sorted(n for n in S.mm.by_name if (S.mm.by_name[n].package or "").endswith("." + f)
                       or (S.mm.by_name[n].package or "") == f)
        names = [n for n in names if S.mm.by_name[n].kind == "ObjectType"] or names
        w("== focus %s: %d types (base, sample)" % (f, len(names)))
        for n in names:
            if cb[n] or cs[n]:
                w("%-6s %-6s %s" % (cb[n], cs[n], n))
        w("   (types with 0 objects on both sides: %d)" % sum(1 for n in names if not cb[n] and not cs[n]))
        w("")
    text = "\n".join(out) + "\n"
    if a.out:
        with open(a.out, "w", encoding="utf-8") as fh:
            fh.write(text)
    sys.stdout.write(text)


if __name__ == "__main__":
    main()
