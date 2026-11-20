"""Compare parser netlists with the SimaticML truth of one sample: python tools/lad_cmp.py <sample> [block-prefix]"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
sys.path.insert(0, os.path.join(ROOT, "parser", "tests"))
import test_samples as T  # noqa: E402
from openpedata.model import Model  # noqa: E402

sample = sys.argv[1]
pre = sys.argv[2] if len(sys.argv) > 2 else ""
d = Model(os.path.join(ROOT, "samples", sample)).to_dict(keys=True)["project"]
bad = tot = 0
xmldir = os.path.join(ROOT, "samples", sample + "_truth", "simaticml")
for dev in d["devices"]:
    for plc in dev["plcs"]:
        for b in plc["blocks"]:
            f = os.path.join(xmldir, b["name"] + ".xml")
            if not b["name"].startswith(pre) or not os.path.isfile(f) or not b.get("networks"):
                continue
            exp = T.simaticml_networks(f)
            for pn, (lang, title, comment, nl) in zip(b["networks"], exp):
                if nl is None:
                    continue
                tot += 1
                got = T.parsed_netlist(pn)
                if got != nl:
                    bad += 1
                    print("DIFF", b["name"], pn["index"])
                    print("   got", got)
                    print("   exp", nl)
                    if pn.get("unhandled"):
                        print("   unhandled", pn["unhandled"])
print("networks %d, differing %d" % (tot, bad))
