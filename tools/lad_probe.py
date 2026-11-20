"""Probe SD instruction names: python tools/lad_probe.py Name1 Name2 ...   prints for each name how TIA's import classifies
`Name( in := #ia, out => #io )`: INVALID ('Not a Valid instruction'), CALL (compiles as a call of an unknown block), or OTHER (valid
instruction, message shown)."""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_lad  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

c = TiaClient(default_pid())
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sources", "dbg")
os.makedirs(out, exist_ok=True)
for nm in sys.argv[1:]:
    body = "{R}\n" + nm + "( in := #ia, out => #io )\nEND_RUNG"
    fb, src, res = gen_lad.source("LAD", "PRB", "probe", [body])
    p = os.path.join(out, fb + ".s7dcl")
    open(p, "w", encoding="utf-8").write(src)
    open(p[:-6] + ".s7res", "w", encoding="utf-8").write(res)
    err, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=p, waitMs="45000")
    if err:
        m = gen_lad.condense(r if isinstance(r, str) else json.dumps(r))
        kind = "INVALID" if "Not a Valid instruction" in m else "OTHER"
        print("%-24s %s %s" % (nm, kind, m[:120]))
    else:
        msgs = " ".join(x["description"] for x in (r.get("compile") or {}).get("messages", []) if x.get("state") == "Error")
        print("%-24s %s %s" % (nm, "CALL" if "_Callee" in msgs else "OK/OTHER", msgs[:120]))
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
