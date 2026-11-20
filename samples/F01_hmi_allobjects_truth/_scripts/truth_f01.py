"""F01 ground truth: per screen get detail=full; fallback detail=items + itemGet full per item. Read-only calls only."""
import json
import os
import sys

sys.path.insert(0, r"C:\GitHub\factorybyte\OpenPEData\tools")
from tiaclient import TiaClient  # noqa: E402

PID = 40268
OUT = r"C:\GitHub\factorybyte\OpenPEData\samples\F01_hmi_allobjects_truth\HMI_1"
os.makedirs(OUT, exist_ok=True)
c = TiaClient(pid=PID)
calls = []


def call(tool, **kw):
    kw["instance"] = str(PID)
    err, txt = c.call(tool, **kw)
    calls.append({"tool": tool, "args": kw, "error": err})
    try:
        obj = json.loads(txt)
    except Exception:  # noqa: BLE001
        return True, {"raw": txt}
    if isinstance(obj, dict) and obj.get("fullResultPath"):
        with open(obj["fullResultPath"], encoding="utf-8-sig") as f:
            obj = json.load(f)
    return err, obj


def save(name, obj):
    with open(os.path.join(OUT, name), "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=1)


err, screens = call("TiaHmiUnified", operation="list", objectKind="screen", device="HMI_1")
save("screens.json", screens)
names = [s["name"] for s in screens.get("objects", screens.get("items", []))] if not err else []
for n in names:
    err, full = call("TiaHmiUnified", operation="get", objectKind="screen", device="HMI_1", names=n, detail="full")
    ok = not err and full.get("count") == 1 and not full.get("skipped")
    if ok:
        save("screen_%s.full.json" % n, full)
        print(n, "full ok")
        continue
    print(n, "full failed:", json.dumps(full.get("skipped", full))[:200])
    _, items = call("TiaHmiUnified", operation="get", objectKind="screen", device="HMI_1", names=n, detail="items")
    _, il = call("TiaHmiUnified", operation="itemList", objectKind="screen", device="HMI_1", screenName=n)
    per = {}
    for it in il.get("items", il.get("objects", [])):
        iname = it.get("name") or it.get("Name")
        e2, g = call("TiaHmiUnified", operation="itemGet", objectKind="screen", device="HMI_1", screenName=n,
                     name=iname, detail="full")
        per[iname] = g if not e2 else {"error": g}
        print("  ", iname, "ok" if not e2 else "ERROR " + json.dumps(g)[:200])
    save("screen_%s.full.json" % n, {"getFullFailed": full, "detailItems": items, "itemList": il, "itemGetFull": per})

for kind, fn in (("tag", "tags.json"), ("tagTable", "tagTables.json"), ("scriptModule", "scriptModules.json")):
    e, r = call("TiaHmiUnified", operation="list", objectKind=kind, device="HMI_1")
    save(fn, r)
e, r = call("TiaHmiUnified", operation="get", objectKind="tag", device="HMI_1")
save("tags.get.json", r)
e, r = call("TiaHmiUnified", operation="get", objectKind="software", device="HMI_1")
save("software.json", r)
save("_truth_calls.json", calls)
e, r = call("TiaHmiUnified", operation="scriptSearch", objectKind="screen", device="HMI_1", pattern=".",
            names="FP_Script,FP_Interface")
save("scriptSearch.json", r)
print("scriptSearch", "ERR" if e else "ok", json.dumps(r)[:200])
save("_truth_calls.json", calls)
