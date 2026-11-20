"""Quiet helper around TiaClient (explicit attach to the given PID; prints one line per call).
python tools/tia_do.py <pid> save
python tools/tia_do.py <pid> truth <nameFilter> <truthDir>         (SimaticML -> <truthDir>/simaticml, SD -> <truthDir>/sd)
python tools/tia_do.py <pid> archive <sampleDir> <zap21>
python tools/tia_do.py <pid> close <ap21>
python tools/tia_do.py <pid> open <ap21>
python tools/tia_do.py <pid> compile"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tiaclient import TiaClient  # noqa: E402


def main(a):
    c = TiaClient(int(a[0]))
    cmd = a[1]
    if cmd == "save":
        print(c.call("TiaProject", operation="save", waitMs="45000")[1][:200])
    elif cmd == "compile":
        e, r = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")
        print(json.dumps({k: r.get(k) for k in ("state", "errorCount", "warningCount")} if isinstance(r, dict) else r)[:300])
    elif cmd == "open":
        print(c.call("TiaProject", operation="open", path=a[2], waitMs="45000")[1][:200])
    elif cmd == "close":
        print(c.call("TiaProject", operation="close", path=a[2], waitMs="45000")[1][:200])
    elif cmd == "archive":
        a[2] = os.path.abspath(a[2]); a[3] = os.path.abspath(a[3]); ap = [f for f in os.listdir(a[2]) if f.endswith(".ap21")][0]
        print(c.call("TiaProject", operation="archive", path=os.path.join(a[2], ap), archivePath=a[3], waitMs="50000")[1][:200])
    elif cmd == "truth":
        a[3] = os.path.abspath(a[3])
        e1, r1 = c.json("TiaPlcSoftware", operation="export", objectKind="block", nameFilter=a[2], targetPath=os.path.join(a[3], "simaticml"), summaryOnly=True, waitMs="50000")
        e2, r2 = c.json("TiaPlcSoftware", operation="documentExport", objectKind="block", nameFilter=a[2], targetPath=os.path.join(a[3], "sd"), summaryOnly=True, waitMs="50000")
        for r in (r1, r2):
            if isinstance(r, dict):
                its = r.get("items", [])
                bad = [i.get("name") for i in its if i.get("status") not in ("exported",)]
                print("items %d, not exported: %s" % (len(its), bad[:10]))
            else:
                print(str(r)[:300])
    else:
        print("unknown", cmd)


if __name__ == "__main__":
    main(sys.argv[1:])
