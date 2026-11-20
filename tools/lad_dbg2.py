"""Multi-network probe: python tools/lad_dbg2.py LAD "net1 lines|..." "net2 lines|..." -- one candidate block with all given networks."""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_lad  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

lang = sys.argv[1].upper()
bodies = ["{R}\n" + a.replace("|", "\n") + "\nEND_RUNG" for a in sys.argv[2:]]
c = TiaClient(default_pid())
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sources", "dbg")
fb, src, res = gen_lad.source(lang, "DBGM", "dbg", bodies)
p = os.path.join(out, fb + ".s7dcl")
open(p, "w", encoding="utf-8").write(src)
open(p[:-6] + ".s7res", "w", encoding="utf-8").write(res)
err, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=p, waitMs="45000")
if err:
    print("IMPORT:", gen_lad.condense(r if isinstance(r, str) else json.dumps(r))[:400])
else:
    msgs = [m["description"] for m in (r.get("compile") or {}).get("messages", []) if m.get("state") == "Error"]
    print("OK" if not msgs else "COMPILE " + "; ".join(msgs)[:300])
    if not msgs:
        err, g = c.json("TiaPlcSoftware", operation="documentGet", objectKind="block", name=fb)
        s = g.get("source", "") if isinstance(g, dict) else ""
        k = s.find("NETWORK")
        print(s[k:s.rfind("END_NETWORK") + 11] if k >= 0 else s[:600])
    c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
