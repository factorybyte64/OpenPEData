"""Z1.2 tests (docs/PLAN-Z1.2-plc.md): PLC alarms, text lists, watch tables, software units against the Openness truth
of samples 041-099 (alarm text export .xlsx, list/unitList JSON). Writes parser/tests/RESULTS_PLC.md.

Usage: python -I parser\\tests\\test_plc.py
"""
import glob
import html
import json
import os
import re
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.model import Model  # noqa: E402

SAMPLES = os.path.join(ROOT, "samples")
INHERITED = "### Inherited from Type ###"

# Findings made after 2026-11-01 exist in the .NET parser only (dotnet\README.md). For these samples the Python parser is
# checked only for what it can read; the feature the sample was made for is checked by the .NET test program
# (dotnet\tests\OpenPEData.Tests). A sample with 0 checks that is not listed here is reported as an error.
DOTNET_ONLY = {
    "078_plc_force_entry": "force table entries: PlcForceTableTests",
    "079_plc_to_hw_connect": "TO hardware connection: PlcToConnectionTests",
    "081_plc_opcua_nodes": "OPC UA server interface nodes: PlcOpcUaNodeTests",
    "082_plc_opcua_refns": "OPC UA reference namespace content: PlcOpcUaNodeTests",
}
OPCUA_KINDS = {"server": "OpcUa.ServerConfiguration", "reference": "OpcUa.ReferenceNamespace"}


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def xlsx_rows(path, sheet=1):
    """Rows of a worksheet as [{column letter: text}] (shared and inline strings; namespace prefix allowed)."""
    p = r"(?:\w+:)?"
    z = zipfile.ZipFile(path)
    ss = []
    if "xl/sharedStrings.xml" in z.namelist():
        x = z.read("xl/sharedStrings.xml").decode("utf-8-sig")
        for si in re.findall(r"<%ssi>(.*?)</%ssi>" % (p, p), x, re.S):
            ss.append(html.unescape("".join(re.findall(r"<%st(?:\s[^>]*)?>(.*?)</%st>" % (p, p), si, re.S))))
    rows = []
    x = z.read("xl/worksheets/sheet%d.xml" % sheet).decode("utf-8-sig")
    for r in re.findall(r"<%srow[^>]*>(.*?)</%srow>" % (p, p), x, re.S):
        cells = {}
        for ref, attrs, body in re.findall(r'<%sc r="([A-Z]+)\d+"([^>]*?)(?:/>|>(.*?)</%sc>)' % (p, p), r, re.S):
            v = re.search(r"<%sv>(.*?)</%sv>" % (p, p), body or "")
            t = re.search(r'\bt="(\w+)"', attrs)
            if t and t.group(1) == "s" and v:
                cells[ref] = ss[int(v.group(1))]
            elif t and t.group(1) == "inlineStr":
                cells[ref] = html.unescape("".join(re.findall(r"<%st(?:\s[^>]*)?>(.*?)</%st>" % (p, p), body or "", re.S)))
            else:
                cells[ref] = v.group(1) if v else None
        rows.append(cells)
    return rows


def check(sample, src):
    d = Model(src).to_dict()
    errs, n = [], 0

    def eq(what, got, exp):
        nonlocal n
        n += 1
        if got != exp:
            errs.append("%s: parser %r, truth %r" % (what, got, exp))

    truth = os.path.join(SAMPLES, sample + "_truth")
    pa = d["project"]["plcAlarms"]
    fx = os.path.join(truth, "alarmTexts.xlsx")
    if os.path.exists(fx):  # instance alarms (samples 042 ff.)
        rows = xlsx_rows(fx)
        head = rows[0]
        cols = {}  # column -> text name
        for c, h in head.items():
            m = re.match(r'"(Alarm text|Info text|Additional text (\d))" - .*?\[([a-z]{2}-[A-Z]{2})\]', h or "")
            if m:
                name = "alarmText" if m.group(1) == "Alarm text" else "infoText" if m.group(1) == "Info text" else "additionalText" + m.group(2)
                cols[c] = (name, m.group(3))
        inst = {(a["block"], a["name"]): a for a in pa["alarms"] if a["kind"] == "InstanceAlarm"}
        typ = {a["name"]: a for a in pa["alarms"] if a["kind"] == "TypeAlarm"}
        eq("instance alarm count", len(inst), len(rows) - 1)
        for r in rows[1:]:
            a = inst.get((r.get("A"), r.get("B")))
            eq("alarm %s/%s exists" % (r.get("A"), r.get("B")), a is not None, True)
            if not a:
                continue
            cls = r.get("C")
            eq("alarm %s/%s class" % (r["A"], r["B"]), a["alarmClass"],
               typ[a["name"]]["alarmClass"] if cls == INHERITED and a["name"] in typ else cls)
            eq("alarm %s/%s class set here" % (r["A"], r["B"]), a["alarmClassSetHere"], cls != INHERITED)
            for c, (name, culture) in cols.items():
                v = r.get(c)
                got = (a["texts"].get(name) or {}).get(culture)
                eq("alarm %s/%s %s %s" % (r["A"], r["B"], name, culture), got, None if v in (INHERITED, "", None) else v)
    wts = {w["name"]: w for w in d["project"]["plcWatchTables"] if w["kind"] == "watch"}
    fl = os.path.join(truth, "list_watchTable.json")
    if os.path.exists(fl):  # samples 047 ff.: Openness lists the watch tables (the default force table is not listed)
        names = sorted(x["name"] for x in load(fl).get("items", []))
        eq("watch tables", sorted(wts), names)
    for f in glob.glob(os.path.join(truth, "watchTable_*.json")):
        t = load(f)
        w = wts.get(t["name"])
        eq("watch table %s exists" % t["name"], w is not None, True)
        if w:
            got = [e for e in w["entries"] if not e["isComment"] or e["comment"]]
            eq("watch table %s entries" % t["name"], len(got), t["entryCount"])
            for g, e in zip(got, t["entries"]):
                for mine, theirs in (("name", "name"), ("address", "address"), ("displayFormat", "displayFormat"), ("modifyValue", "modifyValue")):
                    eq("watch table %s entry %s %s" % (t["name"], e.get("index"), theirs), g[mine], e.get(theirs))
    for f in glob.glob(os.path.join(truth, "to_*.json")):  # samples 052 ff.: TO parameters = start values of the TO DB
        t = load(f)
        if "parameters" not in t:
            continue
        blk = next((b for dev in d["project"]["devices"] for plc in dev["plcs"] or [] for b in plc["blocks"]
                    if b["name"] == t["name"]), None)
        eq("TO %s DB" % t["name"], blk is not None, True)
        if not blk or blk["type"] != "TechnologyDB":
            continue  # PID TOs are instance DBs of know-how protected FBs (members via the FB interface)
        sv = {}

        def walk(ms, prefix):
            for x in ms:
                if x.get("startValue") is not None:
                    sv[prefix + x["name"]] = x["startValue"]
                walk(x.get("members") or [], prefix + x["name"] + ".")
        for sec in blk.get("interface") or []:
            walk(sec["members"], "")
        if t["name"] == "TO_Speed_5_0":
            eq("TO %s stored start values" % t["name"], len(sv) > 0, True)
        for par in t["parameters"]:
            if par["name"] in sv:  # only values stored in the DB (decision 2026-10-24)
                got = sv[par["name"]]
                exp = par["value"]
                if isinstance(exp, bool):
                    got = str(got).lower() == "true"
                elif isinstance(exp, (int, float)):
                    got = float(got)
                eq("TO %s %s" % (t["name"], par["name"]), got, exp)
    oi = {x["name"]: x for x in d["project"]["plcOpcUaInterfaces"]}
    for fn in ("opcUaInterfaces.json", "opcUaInterfaceList.json"):  # samples 054 ff.: OPC UA interfaces (082: list)
        fo = os.path.join(truth, fn)
        if os.path.exists(fo) and isinstance(load(fo).get("items"), list):
            exp = load(fo)["items"]
            eq("OPC UA interfaces", sorted(oi), sorted(x["name"] for x in exp))
            for x in exp:
                if x["name"] in oi:
                    eq("OPC UA interface %s enabled" % x["name"], oi[x["name"]]["enabled"], x["enabled"])
                    if x.get("interfaceKind") in OPCUA_KINDS:
                        eq("OPC UA interface %s kind" % x["name"], oi[x["name"]]["subtype"], OPCUA_KINDS[x["interfaceKind"]])
    for fx in sorted(glob.glob(os.path.join(truth, "*.xml"))):  # 081/082: node set export of each interface
        name = os.path.basename(fx)[:-4]
        if not name.endswith("_import"):  # *_import.xml = the file imported into TIA, not an export
            eq("OPC UA interface %s exists" % name, name in oi, True)
    fh = os.path.join(truth, "hardwareConnection.json")
    if os.path.exists(fh):  # 079: the technology object exists as a TechnologyDB (the connection itself: .NET only)
        h = load(fh)
        blk = next((b for dev in d["project"]["devices"] for plc in dev["plcs"] or [] for b in plc["blocks"]
                    if b["name"] == h["to"]), None)
        eq("TO %s TechnologyDB" % h["to"], blk and blk["type"], "TechnologyDB")
    ff = os.path.join(truth, "forceTable.json")
    if os.path.exists(ff):  # 078: the force table and its entry addresses (force value and triggers: .NET only)
        f = load(ff)
        tab = next((t for t in d["project"]["plcWatchTables"] if t["kind"] == "force" and t["name"] == f["table"]), None)
        eq("force table %s exists" % f["table"], tab is not None, True)
        eq("force table %s addresses" % f["table"], [e["address"] for e in (tab or {}).get("entries", [])],
           [e["address"] for e in f["entries"]])
    fs = os.path.join(truth, "supervisions.xlsx")
    if os.path.exists(fs):  # samples 056 ff.: ProDiag supervisions (Openness supervision export)
        rows = xlsx_rows(fs)
        txtcol = next((c for c, h in rows[0].items() if (h or "").startswith('"Specific text field"')), None)
        cult = re.search(r"\[([a-z]{2}-[A-Z]{2})\]", rows[0].get(txtcol) or "")
        sv = {(x["proDiagFb"], s["number"]): s for x in d["project"]["plcSupervisions"] for s in x["supervisions"]}
        eq("supervision count", len(sv), len(rows) - 1)
        for r in rows[1:]:
            g = sv.get((r["B"], int(r["C"])))
            eq("supervision %s/%s exists" % (r["B"], r["C"]), g is not None, True)
            if g:
                eq("supervision %s/%s name" % (r["B"], r["C"]), g["name"], r["E"])
                eq("supervision %s/%s type" % (r["B"], r["C"]), (g["type"] or "")[:1], r["H"])
                eq("supervision %s/%s trigger" % (r["B"], r["C"]), g["supervisedStatus"], r["G"])
                eq("supervision %s/%s category" % (r["B"], r["C"]), str(g["category"]), r["I"])
                if txtcol and cult:
                    eq("supervision %s/%s text" % (r["B"], r["C"]), g["specificText"].get(cult.group(1)), r.get(txtcol) or None)
    ft = os.path.join(truth, "alarmTextLists.xlsx")
    if os.path.exists(ft):  # samples 045 ff.: user text lists (sheet TextList: Name, ListRange, Comment; TextListEntry)
        lists = {x["name"]: x for x in d["project"]["plcTextLists"] if x["kind"] == "user"}
        tl, te = xlsx_rows(ft, 1), xlsx_rows(ft, 2)
        eq("text lists", sorted(lists), sorted(r["A"] for r in tl[1:]))
        for r in tl[1:]:
            if r["A"] in lists:
                eq("text list %s range" % r["A"], lists[r["A"]]["listRange"], r["B"])
        tcols = {c: re.search(r"\[([a-z]{2}-[A-Z]{2})\]", h).group(1) for c, h in te[0].items() if (h or "").startswith("Text [")}
        for r in te[1:]:
            lst = lists.get(r["A"])
            ent = next((e for e in (lst or {}).get("entries", []) if str(e["from"]) == r["B"] and str(e["to"]) == r["C"]), None)
            eq("text list %s entry %s-%s" % (r["A"], r["B"], r["C"]), ent is not None, True)
            if ent:
                for c, cult in tcols.items():
                    eq("text list %s entry %s-%s %s" % (r["A"], r["B"], r["C"], cult), ent["text"].get(cult), r.get(c) or None)
        eq("text list entry count", sum(len(x["entries"]) for x in lists.values()), len(te) - 1)
    fu = os.path.join(truth, "units.json")
    if os.path.exists(fu) and isinstance(load(fu).get("items"), list):  # samples 049 ff.: software units
        units = {u["name"]: u for u in d["project"]["plcUnits"]}
        exp = load(fu)["items"]
        eq("software units", sorted(units), sorted(x["name"] for x in exp))
        for x in exp:
            u = units.get(x["name"])
            if u:
                eq("unit %s relations" % x["name"], u["relations"],
                   sorted(r["relatedObject"] for r in x["relations"] if r["relationType"] == "SoftwareUnit"))
    fc = os.path.join(truth, "change.json")
    if os.path.exists(fc):
        for ch in load(fc):
            if ch["operation"] == "sourceImport":  # a Program_Alarm in an FB gives a type alarm (041)
                for b in ch["result"]["items"][0]["blocks"]:
                    eq("type alarm in %s" % b, any(a["block"] == b and a["kind"] == "TypeAlarm" for a in pa["alarms"]), True)
    return n, errs


def main(argv):
    rows, ok, fails = [], True, []
    samples = sorted(os.path.basename(p)[:-6] for p in glob.glob(os.path.join(SAMPLES, "0[4-9][0-9]_plc_*_truth")))
    for s in samples:
        for kind, src in (("folder", os.path.join(SAMPLES, s)), ("zap21", os.path.join(SAMPLES, s + ".zap21"))):
            if not os.path.exists(src):
                continue
            try:
                n, errs = check(s, src)
            except Exception as e:  # noqa: BLE001
                n, errs = 0, ["exception: %r" % e]
            if n == 0 and not errs:
                errs = ["no checks: the truth folder has no file this test evaluates"]
            ok &= not errs
            res = "OK" if not errs else "%d errors" % len(errs)
            if not errs and s in DOTNET_ONLY:
                res = "OK (partly; %s, checked by the .NET tests)" % DOTNET_ONLY[s]
            rows.append((s, kind, n, res))
            fails += ["- %s %s: %s" % (s, kind, e) for e in errs]
    out = ["# PLC tests (Z1.2 against Openness ground truth)", "", "| Sample | Source | Checks | Result |", "| --- | --- | --- | --- |"]
    out += ["| %s | %s | %d | %s |" % r for r in rows]
    if fails:
        out += [""] + fails
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "RESULTS_PLC.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")
    sys.stdout.reconfigure(encoding="utf-8")
    print("\n".join(out))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
