"""Compact dump of the ident table and FlgNets of one block: python tools/ident_dump.py <sample> <block>"""
import os
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import code  # noqa: E402
from openpedata.model import Model  # noqa: E402

m = Model(os.path.join(ROOT, "samples", sys.argv[1]))
p = m.p
blk = [k for k in p.of_type("CodeBlockData") if p.name(k) == sys.argv[2]][0]


def short(e, depth=0, maxd=4):
    t = code._local(e.tag)
    a = " ".join('%s=%s' % (k, (v if len(v) < 60 else v[:60] + "...")) for k, v in e.attrib.items())
    s = "  " * depth + t + (" " + a if a else "")
    if depth < maxd:
        s += "".join("\n" + short(c, depth + 1, maxd) for c in e)
    return s


ic = m.rel1(blk, "CoreObject2IdentContainer")
for part in p.targets(ic, "IdentParts"):
    r = code._xml(p.attr(part, "IIdentPartData", "PayLoad"))
    for e in r:
        print(short(e, 0, int(sys.argv[3]) if len(sys.argv) > 3 else 3))
        print()
for k in p.targets(blk, "Sources"):
    cu = p.attributes(k)[0].get("ICompileUnitData", {})
    r = code._xml(cu.get("Data"))
    if r is not None and code._local(r.tag) == "FlgNet":
        print("--- CU RefID", cu.get("RefID"))
        print(short(r, 0, 5))
