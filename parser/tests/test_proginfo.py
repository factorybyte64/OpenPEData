"""Checks the program information (openpedata.proginfo, option --proginfo) against TIA Portal V21 "Program info" of the
samples 006, 025, 031, 036, 039 and 040 (screenshots of 2026-10-31 in <shared folder>\\proginfo\\<sample>\\, values
copied into EXPECTED below). 025, 031, 036, 039 and 040 were rebuilt on 2026-11-10 (owner rename); TIA compiles a block
to a slightly different load memory size each time (same source written repeatedly: DB_Arr 1362-1366 bytes), so their
Resources values (load memory) were measured again in TIA "Program info" of the rebuilt archives on 2026-11-10 (031 and 039, rebuilt
once more with corrected recipes, on 2026-11-11). The 039 load memory values below were measured on 2026-11-20 on the
archive now in samples\\ (saved 2026-11-11 01:49): Openness PlcBlock.LoadMemoryLength via TiaMcpXE memoryList, read on
a retrieved copy without compiling (analysis\\proginfo_039\\REPORT.md); the values of 2026-11-10 belonged to the
previous build of the archive. Work
memory, local data, call/dependency structure and assignment list are unchanged. Known deviations are checked too and
reported as known deviations, not suppressed.
Result table: tests\\RESULTS_PROGINFO.md.

Usage: python -I parser\\tests\\test_proginfo.py
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import proginfo  # noqa: E402
from openpedata.model import Model  # noqa: E402

SAMPLES = os.path.join(ROOT, "samples")

# Resources rows: (load memory, code work memory, data work memory, retain memory, ">" shown); None = empty cell in TIA
EXPECTED = {
    "036_tech": {
        "rows": {"OB": (2413, 82, None, None, True), "FC": (105614, 5532, None, None, True), "FB": (99884, 1750, None, None, False),
                 "DB": (9487, None, 1006, 0, True), "DataTypes": (115684,), "PlcTags": (2856,)},
        "used": (335938, 7364, 1006, 0),
        "local": {"FC_SclOps": 40, "FC_SclCtl": 8, "FC_SclLit": 888, "FC_SclGlobal": 8, "FC_SclAbs": 8, "FC_SclSlice": 24,
                  "FC_SclMath": 40, "FC_SclStr": 1032, "FC_SclMisc": 16, "FC_SclArr": 0},
        "roots": ["Main", "MC_Servo", "MC_Interpolator", "DB_Global", "FB_Empty_DB", "DB_InGroup", "DB_Motor", "FB_Iface_DB",
                  "DB_Std", "PID_Comp_2_3", "TO_Speed_5_0", "High_Spe_3_0", "TO_Posit_6_0", "PID_3Ste_2_3", "PID_Temp_1_1",
                  "TO_Exter_6_0", "SpeedAx_6_0", "FC_SclOps", "FC_SclCtl", "FC_SclLit", "FC_SclGlobal", "FC_SclAbs",
                  "FC_SclSlice", "FC_SclMath", "FC_SclStr", "FC_SclMisc", "FC_SclArr"],
        "expanded": {"FC_SclGlobal": True, "FC_SclMisc": True, "FC_SclOps": False, "FC_SclLit": False},
        # assignment list: (area, byte, bit or size)
        "assign": {("Input", 0, 0), ("Input", 0, "W"), ("Input", 1, 0), ("Output", 0, 0), ("Output", 0, "W"), ("Output", 1, 0),
                   ("Memory", 0, 0), ("Memory", 0, 1), ("Memory", 2, "W"), ("Memory", 8, "D"), ("Memory", 10, 3),
                   ("Memory", 12, "W"), ("Memory", 16, "D"), ("Memory", 100, "W"), ("Memory", 104, "D"), ("Memory", 110, "B")},
    },
    "025_fb_idb": {
        "rows": {"OB": (2413, 82, None, None, False), "FB": (8202, 201, None, None, False), "DB": (8347, None, 924, 0, False),
                 "DataTypes": (1160,), "PlcTags": (802,)},
        "used": (20924, 283, 924, 0),
        "local": {"FB_Iface": 8, "FB_Empty": 0, "Main": 0},
        "roots": ["Main", "DB_Global", "FB_Empty_DB", "DB_InGroup", "DB_Motor", "FB_Iface_DB", "FB_Empty", "FB_Iface"],
        "dependency": ["UDT_Motor", "DB_Global", "FB_Empty_DB", "DB_InGroup", "DB_Motor", "FB_Iface_DB", "FB_Empty", "FB_Iface", "Main"],
        "assign": set(),
    },
    "006_tag_addr": {  # Main not compiled: TIA shows "?" for OB
        "rows": {"OB": (0, 0, None, None, True)},
        "roots": ["Main"],
        "assign": set(),
        "known": {"PlcTags": "TIA 1232 bytes, without compiling the plf has no TagLoadMemory_* values"},
    },
    "031_lad_all": {
        "rows": {"OB": (2413, 82, None, None, False), "FC": (142578, 9852, None, None, True), "FB": (841591, 20513, None, None, False),
                 "DB": (10861, None, 1148, 0, False), "DataTypes": (1160,), "PlcTags": (2856,)},
        "used": (1001459, 30447, 1148, 0),
        "local": {"SEG": 8, "BCDCPL": 0, "BITSUM": 16, "SCALE": 8, "UNSCALE": 8, "SERIALIZE": 64, "FC_SclAbs2": 16,
                  "FC_SclVar": 16, "FC_SclMore": 808},
        "expanded": {"FC_SclGlobal": True, "FC_SclMisc": True, "FC_SclAbs2": True, "FC_SclVar": True, "FC_SclMore": True,
                     "SEG": False},
    },
    "040_s71200": {  # S7-1200: TIA shows one work memory column (code + data)
        "rows": {"OB": (2414, 18, None, None, False), "FB": (9862, 160, None, None, False), "PlcTags": (655,)},
        "used": (12931, 178, 0, 0),
        "roots": ["Main", "FB_Lad_ex134", "FB_Fbd_ex134"],
        "dependency": ["FB_Lad_ex134", "FB_Fbd_ex134", "Main"],
        "known": {"assign": "TIA lists IB0-1, IB64-67, IB1000-1023 (onboard DI, AI, HSC) without marking: hardware addresses "
                            "that the output does not contain"},
    },
    "039_fw41": {
        "rows": {"OB": (2416, 82, None, None, False), "FC": (397605, 91164, None, None, False),
                 "FB": (3045659, 192653, None, None, False), "DB": (1376, None, 142, 0, False), "DataTypes": (28164,),
                 "PlcTags": (836,)},
        "used": (3476056, 283899, 142, 0),
        "local": {"FC_SclRt": 32, "SEG": 8, "BCDCPL": 0, "BITSUM": 16, "SCALE": 8, "UNSCALE": 8, "Get_AlarmState": 8,
                  "LED": 56, "DeviceStates": 24, "ModuleStates": 24, "PE_ERR": 64, "PE_STRT": 64, "PE_END": 72,
                  "PE_Q_LST": 336, "PE_Q_GET": 160, "PE_STAT": 128, "PE_IDENT": 64, "PE_M_LST": 80, "PE_M_VAL": 80,
                  "SERIALIZE": 64, "DESERIALIZE": 64, "MOVE_BLK_VARIANT": 72, "ReadFromArrayDB": 88},
        "knownLocal": {"Ack_Alarms": 2, "RT_INFO": 18},  # LocalNenaDataLength 0 / 16: TIA shows 2 bytes more
        "assign": set(),
    },
}

COLS = ("loadMemory", "codeWorkMemory", "dataWorkMemory", "retainMemory")


def walk(nodes):
    for n in nodes:
        yield n
        yield from walk(n["children"])


def check(name, exp):
    path = os.path.join(SAMPLES, name + ".zap21")
    plc = proginfo.extract(Model(path), path)["plcs"][0]
    res = plc["resources"]
    fails, known, n = [], [], 0
    rows = {r["objects"]: r for r in res["rows"]}
    for obj, want in exp.get("rows", {}).items():
        r = rows[obj]
        for col, w in zip(COLS, want[:4]):
            n += 1
            if r.get(col) != w:
                fails.append("%s %s: %s instead of %s" % (obj, col, r.get(col), w))
        if len(want) > 4:
            n += 1
            if r["incomplete"] != want[4]:
                fails.append("%s '>': %s instead of %s" % (obj, r["incomplete"], want[4]))
    if "used" in exp:
        u = res["used"]
        got = (u["loadMemory"], u["codeWorkMemory"], u["dataWorkMemory"], u["retainMemory"])
        n += 1
        if name == "040_s71200":  # one work memory column
            got = (got[0], got[1] + got[2], 0, got[3])
        if got != exp["used"]:
            fails.append("Used: %s instead of %s" % (got, exp["used"]))
    nodes = list(walk(plc["callStructure"]))
    local = {}
    for x in nodes:
        local.setdefault(x["name"], x.get("localData"))
    for b, w in exp.get("local", {}).items():
        n += 1
        if local.get(b) != w:
            fails.append("Local data %s: %s instead of %s" % (b, local.get(b), w))
    for b, w in exp.get("knownLocal", {}).items():
        known.append("Local data %s: %s instead of %s" % (b, local.get(b), w))
    if "roots" in exp:
        n += 1
        got = [x["name"] for x in plc["callStructure"]][:len(exp["roots"])]
        if got != exp["roots"] or (name in ("025_fb_idb", "040_s71200", "006_tag_addr") and len(plc["callStructure"]) != len(exp["roots"])):
            fails.append("Call structure: %s" % [x["name"] for x in plc["callStructure"]])
    roots = {x["name"]: x for x in plc["callStructure"]}
    for b, w in exp.get("expanded", {}).items():
        n += 1
        if bool(roots[b]["children"]) != w:
            fails.append("Call structure %s expandable: %s" % (b, not w))
    if "dependency" in exp:
        n += 1
        got = [x["name"] for x in plc["dependencyStructure"]]
        if got != exp["dependency"]:
            fails.append("Dependency structure: %s" % got)
    if "assign" in exp:
        n += 1
        got = {(a["area"], a["byte"], a["bit"] if a["bit"] is not None else a["size"]) for a in plc["assignmentList"]}
        if got != exp["assign"]:
            fails.append("Assignment list: missing %s, extra %s" % (sorted(exp["assign"] - got, key=str), sorted(got - exp["assign"], key=str)))
    for k, v in exp.get("known", {}).items():
        known.append("%s: %s" % (k, v))
    return n, fails, known


def main():
    lines = ["# Program info (openpedata.proginfo against TIA screenshots)", "",
             "| Sample | Checks | Result | Known deviations |", "| --- | --- | --- | --- |"]
    bad = 0
    for name, exp in EXPECTED.items():
        n, fails, known = check(name, exp)
        bad += bool(fails)
        lines.append("| %s | %d | %s | %s |" % (name, n, "OK" if not fails else "ERROR: " + "; ".join(fails), "; ".join(known) or "-"))
        print(name, n, "OK" if not fails else fails, known or "")
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "RESULTS_PROGINFO.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
