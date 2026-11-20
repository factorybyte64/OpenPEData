import os, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import code
from openpedata.model import Model
m = Model(os.path.join(ROOT, "samples", sys.argv[1])); p = m.p
want = set(sys.argv[2:]); seen = set()
def short(e, depth=0):
    a = " ".join('%s=%s' % (k, (v if len(v) < 70 else v[:70] + "...")) for k, v in e.attrib.items())
    return "  " * depth + code._local(e.tag) + (" " + a if a else "") + "".join("\n" + short(c, depth + 1) for c in e)
for blk in p.of_type("CodeBlockData"):
    ic = m.rel1(blk, "CoreObject2IdentContainer")
    for part in p.targets(ic, "IdentParts") if ic else []:
        r = code._xml(p.attr(part, "IIdentPartData", "PayLoad"))
        for e in r or []:
            t = code._local(e.tag)
            if t in want and t not in seen:
                seen.add(t); print("== block", p.name(blk)); print(short(e)); print()
