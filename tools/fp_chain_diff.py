"""F3 preparation: diff a chain of project archives pair by pair (what changes from step to step while faceplates are
built by hand: F01 -> F02a_open -> ... -> F02h_instances).

Usage: python -I tools\\fp_chain_diff.py [archive ...] [--outdir analysis\\F3_faceplate]
Without arguments the chain is samples\\F01_hmi_allobjects.zap21 followed by every samples\\F02?_*.zap21 in name order.
Missing archives are skipped; consecutive existing ones are diffed. A path may be a project folder, .ap21 or .zap21.

Per pair the output file diff_<a>_<b>.txt holds
  1. object count per metamodel type (types whose count differs), new metamodel types/packages
  2. new and removed objects (key, type, name), deleted keys (tombstones)
  3. new and removed links of all objects with relation name and target type/name
  4. attribute diff per changed object (tools\\attrdiff.py: decoded attributes, raw slot changes, links, slot table)
H-023 workaround (short expando blocks) as in tools\\unified_tagdiff.py.
"""
import argparse
import collections
import glob
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
sys.path.insert(0, os.path.join(ROOT, "tools"))
import unified_tagdiff  # noqa: E402,F401  (patches attrs.decode_expando, H-023)
import attrdiff  # noqa: E402
from openpedata import project as projmod  # noqa: E402


def default_chain():
    s = os.path.join(ROOT, "samples")
    return [os.path.join(s, "F01_hmi_allobjects.zap21")] + sorted(glob.glob(os.path.join(s, "F02?_*.zap21")))


def label(path):
    b = os.path.basename(os.path.normpath(path))
    return os.path.splitext(b)[0] if b.lower().endswith((".zap21", ".ap21")) else b


def desc(P, k):
    if k in P.objects:
        return "%s %s name=%r" % (k, P.type_name(k), P.name(k))
    return "%s (no current object)" % k


def rel(P, l):
    r = P.mm.relations.get(l.relation) if l.relation else None
    return r.name if r else ("typed slot %d" % l.slot if l.relation is None else "?0x%08x" % l.relation)


def pair(a, b):
    A, B = projmod.Project(a), projmod.Project(b)
    out = []
    w = out.append
    w("A: %s" % a)
    w("B: %s" % b)
    w("objects %d -> %d, tombstones %d -> %d, records %d -> %d" % (len(A.objects), len(B.objects), len(A.deleted),
                                                                 len(B.deleted), len(A.records), len(B.records)))
    w("")
    w("== 1. object count per type (only differences)")
    ca = collections.Counter(A.mm.types[o.rec.type_id].full_name for o in A.objects.values())
    cb = collections.Counter(B.mm.types[o.rec.type_id].full_name for o in B.objects.values())
    for n in sorted(set(ca) | set(cb)):
        if ca[n] != cb[n]:
            t = B.mm.by_name.get(n) or A.mm.by_name.get(n)
            w("%6d %6d %+6d  %-45s %s" % (ca[n], cb[n], cb[n] - ca[n], t.package if t else "?", n))
    pa = {t.package for t in A.mm.types.values()}
    for p in sorted({t.package for t in B.mm.types.values()} - pa, key=str):
        w("NEW PACKAGE %s" % p)
    for n in sorted(set(B.mm.by_name) - set(A.mm.by_name)):
        w("NEW METATYPE %s" % n)
    w("")
    w("== 2. new / removed objects")
    for k in sorted(set(B.objects) - set(A.objects), key=lambda k: (B.type_name(k), B.name(k) or "", k)):
        w("NEW     %s" % desc(B, k))
    for k in sorted(set(A.objects) - set(B.objects), key=lambda k: (A.type_name(k), A.name(k) or "", k)):
        w("REMOVED %s%s" % (desc(A, k), "  (tombstone in B)" if k in B.deleted else ""))
    w("")
    w("== 3. links added / removed (owner -> relation -> target)")
    for k in sorted(set(A.objects) | set(B.objects)):
        la = {(l.relation, l.slot, l.key) for l in A.objects[k].links} if k in A.objects else set()
        lb = {(l.relation, l.slot, l.key) for l in B.objects[k].links} if k in B.objects else set()
        if la == lb:
            continue
        P = B if k in B.objects else A
        lines = []
        for sign, S, src in (("+", lb - la, B), ("-", la - lb, A)):
            for l in [l for l in src.objects[k].links if (l.relation, l.slot, l.key) in S] if k in src.objects else []:
                lines.append("    %s %-40s -> %s" % (sign, rel(src, l), desc(src if l.key in src.objects else P, l.key)))
        w("  %s" % desc(P, k))
        out.extend(lines)
    w("")
    w("== 4. attribute diff (tools\\attrdiff.py)")
    out.extend(attrdiff.diff(os.path.abspath(a), os.path.abspath(b)))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("archives", nargs="*")
    ap.add_argument("--outdir", default=os.path.join(ROOT, "analysis", "F3_faceplate"))
    a = ap.parse_args()
    chain = a.archives or default_chain()
    present = []
    for p in chain:
        if os.path.exists(p):
            present.append(p)
        else:
            print("skip (missing): %s" % p)
    os.makedirs(a.outdir, exist_ok=True)
    for x, y in zip(present, present[1:]):
        f = os.path.join(a.outdir, "diff_%s_%s.txt" % (label(x), label(y)))
        lines = pair(x, y)
        with open(f, "w", encoding="utf-8") as fh:
            fh.write("\n".join(lines) + "\n")
        print("%s: %d lines" % (f, len(lines)))
    if len(present) < 2:
        print("fewer than two archives present, nothing to diff")


if __name__ == "__main__":
    main()
