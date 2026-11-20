"""H-020 check after the parser change: decode problems of all objects in all sample folders with the new slot
order (ordinal set name) against the old declaration order. Usage: python h020_problems.py"""
import os
import sys
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import metamodel  # noqa: E402
from openpedata.project import Project  # noqa: E402

NEW = metamodel.Metamodel.attribute_sets_ordered


def old(self, tid):
    return sorted(self.attribute_sets(tid), key=lambda s: self.doc_order.get(s, 1 << 30))


def count(path):
    P = Project(path, verify=False)
    objs = probs = expando_short = 0
    for k in P.objects:
        a, p = P.attributes(k)
        objs += 1
        if p:
            probs += 1
    return objs, probs


tot = {"new": [0, 0], "old": [0, 0]}
rows = []
for n in sorted(os.listdir(os.path.join(ROOT, "samples"))):
    p = os.path.join(ROOT, "samples", n)
    if not os.path.isfile(os.path.join(p, "System", "PEData.plf")):
        continue
    res = {}
    for mode, fn in (("new", NEW), ("old", old)):
        metamodel.Metamodel.attribute_sets_ordered = fn
        res[mode] = count(p)
        tot[mode][0] += res[mode][0]
        tot[mode][1] += res[mode][1]
    if res["new"][1] != res["old"][1]:
        rows.append("%-34s objects %6d  problems new %5d  old %5d" % (n, res["new"][0], res["new"][1], res["old"][1]))
metamodel.Metamodel.attribute_sets_ordered = NEW
print("\n".join(rows))
print("TOTAL objects %d, objects with decode problems: new (ordinal) %d, old (declaration) %d" % (tot["new"][0], tot["new"][1], tot["old"][1]))
