"""One-off probe: SD rung variants for one box in a LAD/FBD FB, TIA's answer per variant (python tools/probe_sd_rung.py <LAD|FBD> "<rung line>" ...).
Declarations: v1 Variant (in/out), ref1 Ref_To Int (temp) on top of the usual operands."""
import json
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_lad  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402
lang = sys.argv[1]
src0 = open(os.path.join(HERE, "sources", "lad", "FB_Lad_104.s7dcl"), encoding="utf-8").read()
c = TiaClient(default_pid())
for k, rung in enumerate(sys.argv[2:]):
    src = src0.replace("FB_Lad_104", "FB_Probe").replace("    VAR_TEMP\n", "    VAR_TEMP\n        ref1 : Ref_To Int;\n        ref2 : Ref_To Int;\n")
    if rung.startswith("RUNG"):  # complete rung text (first line RUNG ...)
        src = src.replace("        RUNG wire#powerrail\n        assignmentattempt( SRC := #ia, DST => #io )", "        " + rung)
    else:
        src = src.replace("        assignmentattempt( SRC := #ia, DST => #io )", "        " + rung)
    if lang == "FBD":
        src = src.replace('"LAD"', '"FBD"')
    src = src.replace("\r\n", "\n").replace("\n", "\r\n")
    path = os.path.join(HERE, "sources", "dbg", "FB_Probe.s7dcl")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "w", encoding="utf-8", newline="").write(src)
    res = open(os.path.join(HERE, "sources", "lad", "FB_Lad_104.s7res"), encoding="utf-8").read().replace("FB_Lad_104", "FB_Probe")
    open(path[:-6] + ".s7res", "w", encoding="utf-8", newline="").write(res)
    e, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=path, waitMs="45000")
    if e:
        print(k, "IMPORT", gen_lad.condense(r if isinstance(r, str) else json.dumps(r))[:300])
        continue
    comp = r.get("compile") or {}
    msgs = "; ".join(m["description"] for m in comp.get("messages", []) if m.get("state") == "Error")
    print(k, "OK" if comp.get("errorCount", 1) == 0 else "COMPILE " + msgs[:300])
    c.call("TiaPlcSoftware", operation="delete", objectKind="block", name="FB_Probe")
