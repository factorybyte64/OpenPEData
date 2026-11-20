"""Inventory of the old sample archives (reference for rhelp.transfer): retrieve samples\\<name>.zap21 into
<FBR_OLD>\\<name>\\, open, list devices/blocks/types/tag tables/TOs/watch tables, bulk SimaticML export, close.
Output <FBR_OLD>\\<name>\\inv.json and export\\<device>\\<kind>\\*.xml (FBR_OLD default <stage>\\_old).

    py -3 -I tools\\rebuild\\old_inventory.py --pid 16196 030_scl_all 031_lad_all ...
    py -3 -I tools\\rebuild\\old_inventory.py --pid 16196 --noexport 029_fb_idb_value"""
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import rebuild  # noqa: E402
from tiaclient import TiaClient  # noqa: E402

OLD = os.path.abspath(os.environ.get("FBR_OLD", os.path.join(os.sep, "Temp", "FBR", "_old")))
SAMPLES = os.path.join(os.path.dirname(os.path.dirname(HERE)), "samples")


def j(ctx, tool, **a):
    t = ctx.call(tool, ok_errors=True, **a)
    try:
        return json.loads(t)
    except ValueError:
        return {"raw": t[:2000]}


def run(c, name, export=True):
    d = os.path.join(OLD, name)
    if os.path.exists(os.path.join(d, "inv.json")):
        print(name, "done already")
        return
    if os.path.exists(d):
        shutil.rmtree(d)
    os.makedirs(d)
    ctx = rebuild.Ctx(c, OLD, name, {})
    r = json.loads(ctx.call("TiaProject", operation="retrieve", archivePath=os.path.join(SAMPLES, name + ".zap21"),
                            targetDirectory=d))
    ap = r.get("path") or r.get("expectedFilePath")
    ctx.call("TiaProject", operation="open", path=ap)
    ctx.ap = ap
    inv = {"ap": ap, "devices": j(ctx, "TiaDevice", operation="list")}
    inv["plc"] = {}
    try:
        for dev in inv["devices"].get("items", []):
            if dev.get("softwareKind") != "PlcSoftware":
                continue
            dn = dev["name"]
            p = {}
            for kind in ("block", "type", "tagTable", "technologyObject", "watchTable"):
                a = dict(operation="list", objectKind=kind, device=dn)
                if kind == "block":
                    a["includeSystemBlocks"] = "true"
                p[kind] = j(ctx, "TiaPlcSoftware", **a)
            inv["plc"][dn] = p
            if export:
                for kind in ("block", "type", "tagTable", "technologyObject"):
                    tp = os.path.join(d, "export", dn, kind)
                    p["export_" + kind] = j(ctx, "TiaPlcSoftware", operation="export", objectKind=kind, device=dn,
                                            targetPath=tp, summaryOnly="true")
    finally:
        with open(os.path.join(d, "inv.json"), "w", encoding="utf-8") as f:
            json.dump(inv, f, indent=1, ensure_ascii=False)
        ctx.call("TiaProject", operation="close", ok_errors=True, discardChanges="true")
    print(name, "ok", flush=True)


if __name__ == "__main__":
    args = sys.argv[1:]
    pid = 16196
    if args[:1] == ["--pid"]:
        pid, args = int(args[1]), args[2:]
    c = TiaClient(pid)
    exp = True
    names = args
    if names and names[0] == "--noexport":
        exp, names = False, names[1:]
    for n in names:
        try:
            run(c, n, exp)
        except Exception as e:  # noqa: BLE001
            print(n, "FAIL", str(e)[:500], flush=True)
            c.call("TiaProject", operation="close", discardChanges="true")
