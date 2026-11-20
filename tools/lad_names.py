"""Which catalog instructions does TIA know by name (SD import)? python tools/lad_names.py <cat> [<cat> ...]
Writes docs/catalog/<cat>_names.json: {number: {title, sdName | null}} using gen_lad.probe_name (case-insensitive SD names)."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_lad  # noqa: E402
import lad_instr  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

c = TiaClient(default_pid())
out = os.path.join(HERE, "sources", "names")
os.makedirs(out, exist_ok=True)
for cat in sys.argv[1:]:
    items = json.load(open(os.path.join(HERE, "..", "docs", "catalog", cat + ".json"), encoding="utf-8"))
    res = {}
    for i, it in enumerate(items):
        base = it["name"]
        cands = lad_instr.name_variants(base) if base.replace("_", "").isalnum() else []
        found = next((n for n in cands if gen_lad.probe_name(c, out, n)), None)
        res["%03d" % (i + 1)] = {"title": it["title"].split(" (S7")[0], "sdName": found}
        print(cat, "%03d" % (i + 1), base, "->", found, flush=True)
    json.dump(res, open(os.path.join(HERE, "..", "docs", "catalog", cat + "_names.json"), "w"), indent=1)
