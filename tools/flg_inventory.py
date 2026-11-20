"""Inventory of FlgNet (LAD/FBD) and IdentXmlPart element/attribute names found in a sample: python tools/flg_inventory.py <sample>"""
import collections
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import code  # noqa: E402
from openpedata.model import Model  # noqa: E402

m = Model(os.path.join(ROOT, "samples", sys.argv[1]))
p = m.p
tags, attrs, idtags, idattrs = collections.Counter(), collections.Counter(), collections.Counter(), collections.Counter()
for blk in p.of_type("CodeBlockData"):
    for k in p.targets(blk, "Sources"):
        cu = p.attributes(k)[0].get("ICompileUnitData", {})
        if cu.get("ProgrammingLanguage") not in ("LAD_CLASSIC", "FBD_CLASSIC"):
            continue
        root = code._xml(cu.get("Data"))
        if root is None:
            continue
        for e in root.iter():
            t = code._local(e.tag)
            tags[t] += 1
            for a in e.attrib:
                attrs[t + "@" + a] += 1
    ic = m.rel1(blk, "CoreObject2IdentContainer")
    for part in p.targets(ic, "IdentParts") if ic else []:
        root = code._xml(p.attr(part, "IIdentPartData", "PayLoad"))
        if root is None:
            continue
        for e in root.iter():
            t = code._local(e.tag)
            idtags[t] += 1
            for a in e.attrib:
                idattrs[t + "@" + a] += 1
print("FlgNet tags:", dict(tags))
print("FlgNet attrs:", dict(attrs))
print("Ident tags:", dict(idtags))
print("Ident attrs:", dict(idattrs))
