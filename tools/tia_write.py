"""python tools/tia_write.py <pid> <s7dcl file> ... : documentWrite each file (explicit attach), print compile result"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tiaclient import TiaClient  # noqa: E402

c = TiaClient(int(sys.argv[1]))
for f in sys.argv[2:]:
    e, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=os.path.abspath(f), waitMs="45000")
    if e:
        print(os.path.basename(f), "IMPORT FAILED", str(r)[:400])
    else:
        comp = r.get("compile") or {}
        print(os.path.basename(f), comp.get("state"), comp.get("errorCount"), [m["description"] for m in comp.get("messages", []) if m.get("state") == "Error"][:3])
