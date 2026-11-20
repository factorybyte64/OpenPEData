"""Try rungs directly: python tools/lad_dbg.py LAD  "Contact( #a )|Coil( #q )"  ["pragma||rung line|..."]
Each argument is one candidate network, lines separated by '|' (first line may be a { S7_Templates := ... } pragma).
Prints import/compile result and, on success, the SD text TIA exports (documentGet) for the first network."""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_lad  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

lang = sys.argv[1].upper()
c = TiaClient(default_pid())
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sources", "dbg")
os.makedirs(out, exist_ok=True)
for i, arg in enumerate(sys.argv[2:]):
    body = "{R}\n" + arg.replace("|", "\n") + "\nEND_RUNG"
    fb, src, res = gen_lad.source(lang, "DBG%d" % i, "dbg", [body])
    p = os.path.join(out, fb + ".s7dcl")
    open(p, "w", encoding="utf-8").write(src)
    open(p[:-6] + ".s7res", "w", encoding="utf-8").write(res)
    err, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=p, waitMs="45000")
    if err:
        print("[%d] IMPORT:" % i, gen_lad.condense(r if isinstance(r, str) else json.dumps(r)))
        continue
    comp = r.get("compile") or {}
    msgs = [m["description"] for m in comp.get("messages", []) if m.get("state") == "Error"]
    print("[%d] %s %s" % (i, "OK" if not msgs else "COMPILE", "; ".join(msgs)[:300]))
    if not msgs:
        err, g = c.json("TiaPlcSoftware", operation="documentGet", objectKind="block", name=fb)
        s = g.get("source", "") if isinstance(g, dict) else ""
        k = s.find("NETWORK")
        print(s[k:s.find("END_NETWORK")] if k >= 0 else s[:400])
    c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
