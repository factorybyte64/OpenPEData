"""Checks the WinCC Unified part of the parser (openpedata.hmi) against the Openness ground truth of the HMI
samples (samples\\1xx_hmi_*_truth\\HMI_1\\*.json, written from TiaHmiUnified get/list calls), for the project
folder and the .zap21 archive of each sample. Result table: tests\\RESULTS_HMI.md.

Usage: python -I parser\\tests\\test_hmi.py
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.model import Model  # noqa: E402
from openpedata.project import Project  # noqa: E402

sys.path.insert(0, os.path.join(ROOT, "tools"))
import hmi_styles  # noqa: E402

SAMPLES = os.path.join(ROOT, "samples")
_TAG = re.compile(r"<[^>]+>")
# samples whose style User switched by hand in TIA (Openness cannot set it) -> expected selected style
STYLE_SAMPLES = {"166_hmi_style_dark": "FlatStyle_DarkV2101", "167_hmi_style_bright": "FlatStyle_BrightV2101"}


VIEW_SKIP = {"Name", "Left", "Top", "Width", "Height", "StyleItemClass", "RenderingTemplate"}
_VIEWER_DEFAULTS = []


def _viewer_defaults():
    if not _VIEWER_DEFAULTS:
        with open(os.path.join(ROOT, "ui", "OpenPEData.Viewer", "HmiDefaults.json"), encoding="utf-8") as f:
            _VIEWER_DEFAULTS.append(json.load(f))
    return _VIEWER_DEFAULTS[0]


def _merge(into, src):
    for k, v in src.items():
        if isinstance(v, dict) and isinstance(into.get(k), dict) and k != "Text":
            _merge(into[k], v)
        else:
            into[k] = json.loads(json.dumps(v))


def _flat_style(o, p=""):
    out = {}
    for k, v in o.items():
        if k in hmi_styles.META:
            continue
        if isinstance(v, dict):
            out.update(_flat_style(v, p + k + "."))
        else:
            out[p + k] = v
    return out


# Openness runtime settings scope -> HmiILSettingsData record (root = ILScreenSettings)
RT_SCOPES = {"opcUa": "OpcUaServer", "maxLogin": "MaxLogin", "processDiagnostics": "ProcessDiagnostics",
             "reporting": "Reporting", "telemetry": "Telemetry"}


def simple_yaml(path):
    """Minimal reader for the Openness .hmi.yml exports: nested 'key: value' mappings by indentation and '- item'
    lists of scalars."""
    root, stack = {}, [(-1, None)]
    stack[0] = (-1, root)
    last = {}
    with open(path, encoding="utf-8-sig") as fh:
        for raw in fh:
            line = raw.rstrip("\r\n")
            if not line.strip() or line.lstrip().startswith("#"):
                continue
            ind = len(line) - len(line.lstrip(" "))
            txt = line.strip()
            while stack and ind <= stack[-1][0]:
                stack.pop()
            parent = stack[-1][1]
            if txt.startswith("- "):
                lst = last.get(id(parent))
                if lst is not None:
                    parent[lst] = parent.get(lst) if isinstance(parent.get(lst), list) else []
                    parent[lst].append(txt[2:].strip())
                continue
            k, _, v = txt.partition(":")
            v = v.strip()
            if v and v != "{}":
                parent[k] = v
            else:
                parent[k] = {}
                stack.append((ind, parent[k]))
            last[id(parent)] = k
    return root


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def hmi_of(model_dict, device="HMI_1"):
    for d in model_dict["project"]["devices"]:
        for sw in d.get("hmi", []):
            if sw["name"] == device:
                return sw
    return None


def all_tags(sw):
    return {t["name"]: dict(t, table=tt["name"]) for tt in sw["tagTables"] for t in tt["tags"]}


def check(sample, src):
    truth = os.path.join(SAMPLES, sample + "_truth", "HMI_1")
    full = Model(src).to_dict()
    sw, proj = hmi_of(full), full["project"]
    errs, n = [], 0
    if sw is None:
        return 1, ["no HMI software HMI_1"]

    def eq(what, got, exp):
        nonlocal n
        n += 1
        if got != exp:
            errs.append("%s: parser %r, truth %r" % (what, got, exp))

    tags = all_tags(sw)
    f = os.path.join(truth, "software.json")
    if os.path.exists(f):
        c = load(f)["counts"]
        eq("count Tags", len(tags), c["Tags"])
        eq("count TagTables", len(sw["tagTables"]), c["TagTables"])
        eq("count Connections", len(sw["connections"]), c["Connections"])
        eq("count AlarmClasses", len(sw["alarmClasses"]), c["AlarmClasses"])
        eq("count DiscreteAlarms", len(sw["discreteAlarms"]), c["DiscreteAlarms"])
        eq("count AnalogAlarms", len(sw["analogAlarms"]), c["AnalogAlarms"])
        eq("count Screens", len(sw["screens"]), c["Screens"])
    if os.path.exists(os.path.join(truth, "software.json")):
        c = load(os.path.join(truth, "software.json"))["counts"]
        eq("count SystemTags", sw["counts"]["systemTags"], c["SystemTags"])
        eq("count HmiSystemTextLists", sum(1 for t in sw["textLists"] if t["system"]), c["HmiSystemTextLists"])
        eq("count HmiTextLists", sum(1 for t in sw["textLists"] if not t["system"]), c["HmiTextLists"])
        for key, cnt in (("dataLogs", "DataLogs"), ("alarmLogs", "AlarmLogs"), ("auditTrails", "AuditTrails")):
            if cnt in c:
                eq("count %s" % cnt, len(sw.get(key, [])), c[cnt])
    # logging (samples 136 ff.): data logs, alarm logs, audit trails and logging tags against TiaHmiUnified get
    for key, fn in (("dataLogs", "dataLog.json"), ("alarmLogs", "alarmLog.json"), ("auditTrails", "auditTrail.json")):
        f = os.path.join(truth, fn)
        if not os.path.exists(f):
            continue
        logs = {x["name"]: x for x in sw.get(key, [])}
        for o in load(f).get("objects", []):
            g = logs.get(o["Name"])
            eq("%s %s exists" % (key, o["Name"]), g is not None, True)
            if not g:
                continue
            st, sg, bk = o.get("Settings", {}), o.get("Segment", {}), o.get("Backup", {})
            eq("%s %s LogMaxSize" % (key, o["Name"]), g["logMaxSize"], st.get("LogMaxSize"))
            lp = st.get("LogTimePeriod") or {}
            eq("%s %s LogTimePeriod" % (key, o["Name"]), g["logTimePeriod"],
               {"days": lp.get("Days"), "hours": lp.get("Hours"), "minutes": lp.get("Minutes"), "seconds": lp.get("Seconds")})
            eq("%s %s StorageDevice" % (key, o["Name"]), g["storageDevice"], st.get("StorageDevice"))
            eq("%s %s SegmentMaxSize" % (key, o["Name"]), g["segmentMaxSize"], sg.get("SegmentMaxSize"))
            stp = sg.get("SegmentTimePeriod") or {}
            eq("%s %s SegmentTimePeriod" % (key, o["Name"]), g["segmentTimePeriod"],
               {"days": stp.get("Days"), "hours": stp.get("Hours"), "minutes": stp.get("Minutes"), "seconds": stp.get("Seconds")})
            eq("%s %s SegmentStartTime" % (key, o["Name"]), g["segmentStartTime"], sg.get("SegmentStartTime"))
            eq("%s %s BackupMode" % (key, o["Name"]), g["backupMode"], bk.get("BackupMode"))
    # runtime settings (samples 145 ff.): every stored value against TiaHmiUnified get runtimeSetting (scope root and
    # runtimeSetting_<scope>.json); the value changed in the sample must be among the stored ones
    rs = sw.get("runtimeSettings") or {}
    for scope, fn in (("root", "runtimeSetting.json"),) + tuple((sc, "runtimeSetting_%s.json" % sc) for sc in RT_SCOPES):
        f = os.path.join(truth, fn)
        if not os.path.exists(f):
            continue
        props = load(f).get("properties") or {}
        if scope == "root":
            eq("runtime GMPEnabled", bool(rs.get("gmpEnabled")), props.get("GMPEnabled"))
        mine = rs.get("sections", {}).get(RT_SCOPES.get(scope, "ILScreenSettings"), {})
        for nm, v in mine.items():
            if nm in props:
                eq("runtime %s.%s" % (scope, nm), v, props[nm])
    f = os.path.join(truth, "..", "change.json")
    if os.path.exists(f):
        for ch in load(f):
            a = ch.get("arguments", {})
            if a.get("objectKind") == "runtimeSetting" and a.get("attribute") != "GMPEnabled":
                sc = a.get("scope", "root")
                mine = rs.get("sections", {}).get(RT_SCOPES.get(sc, "ILScreenSettings"), {})
                eq("runtime %s.%s stored" % (sc, a["attribute"]), str(mine.get(a["attribute"])).lower(), a["value"].lower())
    # runtime languages (samples 153 ff.): LanguageAndFonts entries in the order of the project languages
    f, fp = os.path.join(truth, "languageAndFont.json"), os.path.join(truth, "projectLanguages.json")
    if os.path.exists(f) and os.path.exists(fp):
        pl = load(fp)
        eq("project languages", (proj.get("languages") or {}).get("active"), pl.get("activeLanguages"))
        eq("editing language", (proj.get("languages") or {}).get("editing"), pl.get("editingLanguage"))
        eq("reference language", (proj.get("languages") or {}).get("reference"), pl.get("referenceLanguage"))
        mine = sw.get("runtimeLanguages") or []
        items = load(f).get("items", [])
        eq("runtime language count", len(mine), len(items))
        for i, it in enumerate(items):
            if i < len(mine):
                for a, b in (("enable", "enable"), ("enableForLogging", "enableForLogging"), ("order", "order")):
                    eq("runtime language %s %s" % (it.get("language"), b), mine[i][a], it.get(b))
    # global script modules (samples 156 ff.): names and the module text against the Openness export <name>.hmi.js
    f = os.path.join(truth, "scriptModuleList.json")
    if os.path.exists(f):
        mods = {x["name"]: x for x in sw.get("scriptModules", [])}
        eq("script modules", sorted(mods), sorted(i["name"] for i in load(f).get("items", [])))
        for nm, mo in sorted(mods.items()):
            js = os.path.join(truth, "scriptModules", nm + ".hmi.js")
            if os.path.exists(js):
                with open(js, encoding="utf-8-sig", newline="") as fh:
                    eq("script module %s code" % nm, mo["code"], fh.read())
    # user text lists (samples 158 ff.) against the Openness export (HmiTextLists.hmi.yml + TextLibrary), graphic
    # lists by name (Openness exposes only the name)
    f = os.path.join(truth, "textLists", "HmiTextLists.hmi.yml")
    if os.path.exists(f):
        lib = simple_yaml(os.path.join(truth, "textLists", "HmiTextLists.TextLibrary.hmi.yml")).get("TextLibraries", {})
        langs = {n: (v.get("Languages") or [v.get("DefaultLanguage")]) for n, v in lib.items()}
        mine = {t["name"]: t for t in sw["textLists"] if not t["system"]}
        exp = {}
        for cont in simple_yaml(f).get("TextListContainers", {}).values():
            for nm, tl in (cont.get("ResourceLists") or {}).items():
                ents = []
                for e in (tl.get("Entries") or {}).values():
                    libn, _, txt = (e.get("Text") or "").partition(".")
                    texts = lib.get(libn, {}).get("Entries", {}).get(txt, {}).get("Text") or []
                    ents.append((int(e.get("FromValue", e.get("Value", 0))), int(e.get("ToValue", e.get("Value", 0))),
                                 dict(zip(langs.get(libn, []), texts))))
                exp[nm] = sorted(ents, key=lambda x: x[0])
        eq("user text lists", sorted(mine), sorted(exp))
        for nm, ents in exp.items():
            if nm in mine:
                eq("text list %s entries" % nm, sorted(((e["from"], e["to"], e["text"]) for e in mine[nm]["entries"]),
                                                       key=lambda x: x[0]), ents)
    f = os.path.join(truth, "graphicList.json")
    if os.path.exists(f) and os.path.exists(os.path.join(truth, "textLists")):
        eq("graphic lists", sorted(g["name"] for g in sw.get("graphicLists", [])),
           sorted(o["Name"] for o in load(f).get("objects", [])))
    # plant view, OPC UA alarm types, connections (samples 160 ff.)
    f = os.path.join(truth, "plantViewNode.json")
    if os.path.exists(f):
        pv = proj.get("plantViews") or []
        eq("plant views", [(v["name"], v["assignedHmiDevice"]) for v in pv],
           [(i["name"], i.get("assignedHmiDevice") or "") for i in load(os.path.join(truth, "plantView.json")).get("items", [])])
        eq("plant view nodes", sorted(n["path"] for v in pv for n in v["nodes"]),
           sorted(i["path"] for i in load(f).get("items", [])))
    f = os.path.join(truth, "opcUaAlarmType.json")
    if os.path.exists(f) and os.path.exists(os.path.join(truth, "plantViewNode.json")):
        mine = {x["name"]: x for x in sw.get("opcUaAlarmTypes", [])}
        objs = load(f).get("objects", [])
        eq("OPC UA alarm types", sorted(mine), sorted(o["Name"] for o in objs))
        for o in objs:
            g = mine.get(o["Name"])
            if g:
                for a, b in (("nodeId", "NodeId"), ("conditionTypeId", "ConditionTypeId"), ("area", "Area"),
                             ("alarmClass", "AlarmClass"), ("connection", "Connection")):
                    eq("OPC UA alarm type %s %s" % (o["Name"], b), g[a], o.get(b))
    f = os.path.join(truth, "connections.json")
    if os.path.exists(f):
        eq("connection names", sorted(c["name"] for c in sw["connections"]), sorted(i["name"] for i in load(f).get("items", [])))
    # screen items with all Openness values (sample 165, itemGet detail=full): every value the parser finds stored
    # must equal the Openness value
    d = os.path.join(truth, "screenItems")
    if os.path.isdir(d):
        scr = {x["name"]: x for x in sw["screens"]}
        for fn in sorted(os.listdir(d)):
            full = load(os.path.join(d, fn)).get("item") or {}
            s = scr.get(fn[:-5])
            eq("screen %s exists" % fn[:-5], s is not None, True)
            mine = {i["name"]: i for i in (s or {}).get("items", [])}.get(full.get("Name"))
            eq("item %s exists" % full.get("Name"), mine is not None, True)
            if not mine:
                continue
            for k, v in sorted((mine.get("properties") or {}).items()):
                if k in full:
                    eq("item %s %s" % (full["Name"], k), v, full[k])
            for k, key in (("Left", "left"), ("Width", "width"), ("Height", "height")):
                if k in full:
                    eq("item %s %s (top level)" % (full["Name"], k), mine.get(key), full[k])
    # selected style (tools\hmi_styles.py): every style value of an item type equals the Openness value unless the
    # item stores the property itself; enums are numbers in the style and names in Openness (not compared)
    items = []
    d = os.path.join(truth, "screenItems")
    if os.path.isdir(d):
        items = [(fn[:-5], load(os.path.join(d, fn)).get("item") or {}) for fn in sorted(os.listdir(d))]
    f = os.path.join(truth, "screens_full.json")
    if os.path.exists(f) and sample in STYLE_SAMPLES:
        for s in load(f).get("objects", []):
            items.append((s["name"], dict(s["attributes"], Name=None, StyleItemClass="HmiScreen")))
            items += [(s["name"], dict(i["attributes"], Name=i["name"])) for i in s.get("items", [])]
    if items:
        P = Project(src)
        st = hmi_styles.styles(P, fonts=False)
        sel = [s for k, _, s, _ in hmi_styles.settings(P) if P.name(P.targets(k, "Target")[0]) == "HMI_1"]
        eq("selected style", st[sel[0]][0] if sel else None, STYLE_SAMPLES.get(sample, "ExtendedStyleV2101"))
        values = st[sel[0]][2] if sel else {}
        scr = {x["name"]: x for x in sw["screens"]}
        for screen, it in items:
            cls = it.get("StyleItemClass")
            if it.get("Name") is None:
                stored = set((scr.get(screen) or {}).get("properties") or {})
            else:
                mine = {i["name"]: i for i in (scr.get(screen) or {}).get("items", [])}.get(it["Name"]) or {}
                stored = set(mine.get("properties") or {}) | {k.capitalize() for k in ("left", "top", "width", "height")
                                                              if mine.get(k) is not None}
            for k, v in sorted(_flat_style(values.get(cls, {})).items()):
                top = k.split(".")[0]
                exp = _flat_style(it).get(k)
                if top in stored or exp is None or k.endswith("Font.Description") \
                        or (isinstance(v, (int, float)) and not isinstance(v, bool) and isinstance(exp, str)):
                    continue
                if isinstance(v, float):
                    v = round(v, 6)
                    exp = round(exp, 6)
                eq("style %s %s %s" % (screen, cls, k), v if not isinstance(v, str) else v.upper(),
                   exp if not isinstance(exp, str) else exp.upper())
        # viewer layering: object defaults (HmiDefaults.json, without style) -> parser style values (enum names) ->
        # stored values gives every Openness value the defaults or the style know
        dv = _viewer_defaults()
        style_vals = (sw.get("style") or {}).get("values") or {}
        for screen, it in items:
            if it.get("Name") is None:
                continue
            mine = {i["name"]: i for i in (scr.get(screen) or {}).get("items", [])}.get(it["Name"]) or {}
            typ = mine.get("type") or it.get("itemType") or it.get("type")
            eff = json.loads(json.dumps(dv["items"].get(typ, {})))
            _merge(eff, style_vals.get(it.get("StyleItemClass") or typ, {}))
            _merge(eff, mine.get("properties") or {})
            fe = _flat_style(eff)
            for k, exp in sorted(_flat_style(it).items()):
                if k.split(".")[0] in VIEW_SKIP or k not in fe or k.startswith("Text."):
                    continue
                got = fe[k]
                num = (int, float)
                if isinstance(got, num) and isinstance(exp, num) and not isinstance(got, bool) and not isinstance(exp, bool):
                    got, exp = round(float(got), 6), round(float(exp), 6)
                eq("view %s %s %s" % (screen, it["Name"], k), str(got).upper(), str(exp).upper())
    f = os.path.join(truth, "loggingTag.json")
    if os.path.exists(f):
        lts ={(t["name"], lt["name"]): lt for t in tags.values() for lt in t.get("loggingTags", [])}
        exp = load(f).get("objects", [])
        eq("logging tag count", len(lts), len(exp))
        for o in exp:
            g = lts.get((o["tagName"], o["Name"]))
            eq("logging tag %s exists" % o["Name"], g is not None, True)
            if g:
                for mine, theirs in (("dataLog", "DataLog"), ("loggingMode", "LoggingMode"), ("cycleFactor", "CycleFactor")):
                    eq("logging tag %s %s" % (o["Name"], theirs), g[mine], o.get(theirs))
                if o.get("LoggingMode") == "Cyclic":
                    eq("logging tag %s Cycle" % o["Name"], g["cycle"], o.get("Cycle"))
    f = os.path.join(truth, "systemTextLists.json")
    if os.path.exists(f):
        eq("system text lists", sorted(t["name"] for t in sw["textLists"] if t["system"]), sorted(load(f)["items"]))
    f = os.path.join(truth, "systemTags.json")
    if os.path.exists(f):
        eq("count system tags", sw["counts"]["systemTags"], load(f)["count"])
        if load(f).get("items"):
            eq("system tag names", sw.get("systemTags"), sorted(i if isinstance(i, str) else i["name"] for i in load(f)["items"]))
    # acquisition cycles referenced by tags must be cycles of the device
    for tt in sw["tagTables"]:
        for t in tt["tags"]:
            if t["acquisitionCycle"]:
                eq("tag %s cycle known" % t["name"], t["acquisitionCycle"] in [x["name"] for x in sw["cycles"]], True)
    f = os.path.join(truth, "tagTables.json")
    if os.path.exists(f):
        exp = sorted((i.get("groupPath", ""), i["name"]) for i in load(f)["items"])
        eq("tag tables", sorted((t["group"], t["name"]) for t in sw["tagTables"]), exp)
    for f in glob.glob(os.path.join(truth, "tag*.json")):
        d = load(f)
        objs = d.get("objects") or ([d] if "Name" in d else [])
        for o in objs:
            t = tags.get(o["Name"])
            eq("tag %s exists" % o["Name"], t is not None, True)
            if not t:
                continue
            eq("tag %s dataType" % o["Name"], t["dataType"], o["DataType"])
            eq("tag %s connection" % o["Name"], t["connection"] or "<Internal tag>", o["Connection"])
            eq("tag %s plcTag" % o["Name"], t["plcTag"], o.get("PlcTag"))
            eq("tag %s plcName" % o["Name"], t["plcName"], o.get("PlcName"))
            eq("tag %s acquisitionCycle" % o["Name"], t["acquisitionCycle"], o["AcquisitionCycle"])
            eq("tag %s persistent" % o["Name"], t["persistent"], o["Persistent"])
            eq("tag %s table" % o["Name"], t["table"], o["TagTableName"])
            if "Comment" in o:
                eq("tag %s comment" % o["Name"], t["comment"], o["Comment"])
    f = os.path.join(truth, "alarmClasses.json")
    if os.path.exists(f):
        eq("alarm class names", sorted(c["name"] for c in sw["alarmClasses"]), sorted(i["name"] for i in load(f)["items"]))
    for f in glob.glob(os.path.join(truth, "alarmClass_*.json")):
        o = load(f)
        c = next((x for x in sw["alarmClasses"] if x["name"] == o["Name"]), None)
        eq("alarm class %s id" % o["Name"], c and c["id"], o["Id"])
    for kind, key in (("discreteAlarms", "discreteAlarms"), ("analogAlarms", "analogAlarms")):
        for f in glob.glob(os.path.join(truth, kind + "*.json")):
            for o in load(f)["objects"]:
                a = next((x for x in sw[key] if x["name"] == o["Name"]), None)
                eq("%s %s exists" % (kind, o["Name"]), a is not None, True)
                if not a:
                    continue
                eq("%s %s id" % (kind, o["Name"]), a["id"], o["Id"])
                if "AlarmClass" in o:
                    eq("%s %s class" % (kind, o["Name"]), a["alarmClass"], o["AlarmClass"])
                if "RaisedStateTag" in o:
                    eq("%s %s trigger" % (kind, o["Name"]), a["triggerTag"], o["RaisedStateTag"])
                if "RaisedStateTagBitNumber" in o:
                    eq("%s %s bit" % (kind, o["Name"]), a["triggerBit"], o["RaisedStateTagBitNumber"])
                if "TriggerMode" in o:
                    eq("%s %s mode" % (kind, o["Name"]), a["triggerMode"], o["TriggerMode"])
                if "Condition" in o:
                    eq("%s %s condition" % (kind, o["Name"]), a["condition"], o["Condition"])
                    eq("%s %s limit" % (kind, o["Name"]), a["limit"], str(o["ConditionValue"]))
                if "EventText" in o:
                    exp = {c: _TAG.sub("", t) for c, t in o["EventText"].items()}
                    eq("%s %s text" % (kind, o["Name"]), a["text"], exp)
    # decoded item properties against the truth attributes (screens_full.json items, F01 itemGetFull)
    truth_items = {}
    for f in glob.glob(os.path.join(truth, "screen_*.full.json")):
        truth_items.update(load(f).get("itemGetFull") or {})
    f = os.path.join(truth, "screens_full.json")
    if os.path.exists(f):
        for o in load(f)["objects"]:
            for ti in o.get("items", []):
                if "attributes" in ti:
                    truth_items[ti["name"]] = ti["attributes"]
    for s in sw["screens"]:
        for it in s["items"]:
            at = truth_items.get(it["name"])
            if not at:
                continue
            for prop, val in sorted(it["properties"].items()):
                if prop not in at:
                    continue
                exp = at[prop]
                if prop == "SelectionItems":  # compare the texts (IsSelected is not stored when false)
                    exp = [e.get("Text") for e in exp]
                    val = [e.get("Text") for e in val]
                eq("item %s %s" % (it["name"], prop), val, exp)
    # every decoded script (events and script dynamizations) must equal a ScriptCode of the truth; tag references are
    # stored as ids (Tags("1285367001")) and resolved to names like Openness shows them
    codes = set()
    for f in glob.glob(os.path.join(truth, "screen_*.full.json")):
        codes |= set(re.findall(r'"ScriptCode":\s*("(?:[^"\\]|\\.)*")', open(f, encoding="utf-8-sig").read()))
    codes = {json.loads(c) for c in codes}
    if codes:
        for s in sw["screens"]:
            for it in s["items"]:
                for c in [e.get("code") for e in it["events"]] + [d.get("code") for d in it.get("dynamizations", [])
                                                                  if d.get("type") == "Script"]:
                    eq("item %s script in truth" % it["name"], c in codes, True)
    f = os.path.join(truth, "screens.json")
    if os.path.exists(f):
        exp = sorted((i.get("groupPath") or "", i["name"]) for i in load(f)["items"])
        eq("screens", sorted((s["group"], s["name"]) for s in sw["screens"]), exp)
    f = os.path.join(truth, "screens_full.json")
    if os.path.exists(f):
        for o in load(f)["objects"]:
            s = next((x for x in sw["screens"] if x["name"] == o["name"]), None)
            if not s:
                continue
            at = o["attributes"]
            eq("screen %s width" % o["name"], s["width"], at["Width"])
            eq("screen %s height" % o["name"], s["height"], at["Height"])
            # BackColor is only stored when it differs from the default 0xFFC0C0C0 of ExtendedStyle (other styles: style
            # check above)
            if sample not in STYLE_SAMPLES:
                eq("screen %s backColor" % o["name"], s["backColor"] or "0xFFC0C0C0", at["BackColor"])
    truth_objects = []
    for fn in ("screens_full.json", "screens_items.json"):
        f = os.path.join(truth, fn)
        if os.path.exists(f):
            truth_objects += load(f)["objects"]
    for f in glob.glob(os.path.join(truth, "screen_*.full.json")):  # F01: per screen, get result (top level or sub keys)
        j = load(f)
        for v in [j] + list(j.values()):
            if isinstance(v, dict) and v.get("objects"):
                truth_objects += [dict(o, items=[i for i in o.get("items", []) if "dynamizations" in i], partial=True)
                                  for o in v["objects"]]
    if truth_objects:
        for o in truth_objects:
            s = next((x for x in sw["screens"] if x["name"] == o["name"]), None)
            if not s:
                continue
            if not o.get("partial"):  # F01 truth lists only items with dynamizations here
                eq("screen %s items" % o["name"], sorted((i["name"], i["type"]) for i in s["items"]),
                   sorted((i["name"], i["type"]) for i in o.get("items", [])))
            for ti in o.get("items", []):
                it = next((x for x in s["items"] if x["name"] == ti["name"]), None)
                if not it:
                    continue
                at = ti.get("attributes")
                if at and "Width" in at:  # circles and arcs: CenterX/CenterY/Radius
                    eq("item %s width" % ti["name"], it["width"], at["Width"])
                    eq("item %s height" % ti["name"], it["height"], at["Height"])
                    eq("item %s left" % ti["name"], it["left"], at["Left"])
                    if it["backColor"] is not None:  # only stored when not the type default
                        eq("item %s backColor" % ti["name"], it["backColor"], at["BackColor"])
                    if "Text" in at:  # the default text is stored without language (0xFFFF)
                        eq("item %s text" % ti["name"], it["text"].get("en-US", it["text"].get("")), at["Text"]["en-US"])
                if "dynamizations" in ti:
                    # tags the item references: by tag dynamization or as Tags("name") in a script dynamization
                    exp_tags = {d["Tag"] for d in ti["dynamizations"] if d.get("DynamizationType") == "Tag"}
                    for d in ti["dynamizations"]:
                        exp_tags |= set(re.findall(r'Tags\("([^"]+)"\)', d.get("ScriptCode") or ""))
                    eq("item %s tags" % ti["name"], sorted(it["tags"]), sorted(exp_tags))
                    got = sorted((d["property"], d["type"], d.get("tag") if d["type"] == "Tag" else d.get("code"))
                                 for d in it["dynamizations"])
                    exp = sorted((d["PropertyName"], d["DynamizationType"],
                                  d.get("Tag") if d["DynamizationType"] == "Tag" else d.get("ScriptCode"))
                                 for d in ti["dynamizations"])
                    eq("item %s dynamizations" % ti["name"], got, exp)
                    for d in ti["dynamizations"]:
                        cond = (d.get("ValueConverter") or {}).get("MappingTable", {}).get("ConditionType")
                        if d["DynamizationType"] == "Tag" and cond:
                            g = next((x for x in it["dynamizations"] if x["property"] == d["PropertyName"]), {})
                            m = g.get("mapping") or {"conditionType": "None", "entries": []}
                            eq("item %s %s conditionType" % (ti["name"], d["PropertyName"]), m["conditionType"], cond)
                            if "mappingEntries" in d:
                                eq("item %s %s mapping entries" % (ti["name"], d["PropertyName"]), m["entries"],
                                   d["mappingEntries"])
                if "events" in ti:
                    eq("item %s events" % ti["name"], sorted(e["name"] for e in it["events"]),
                       sorted("On" + e["eventType"] for e in ti["events"]))
                    for te in ti["events"]:
                        ev = next((e for e in it["events"] if e["name"] == "On" + te["eventType"]), None)
                        eq("item %s event %s code" % (ti["name"], te["eventType"]), ev and ev["code"], te["ScriptCode"])
    return n, errs


def check_f40(sample, src):
    """F40 (Siemens demo project, HMI MTP1200_Comfort_RT): item dynamizations per screen against
    screen_*.full.json (property, type, tag incl. structure path Tag.member, mapping ConditionType). The truth has
    only Tag dynamizations on items; ResourceList/TagParameter sit on faceplate interfaces (test_faceplates)."""
    truth = os.path.join(SAMPLES, sample + "_truth")
    sw = hmi_of(Model(src).to_dict(), "MTP1200_Comfort_RT")
    if sw is None:
        return 1, ["no HMI software MTP1200_Comfort_RT"]
    screens = {s["name"]: s for s in sw["screens"]}
    errs, n = [], 0
    for f in sorted(glob.glob(os.path.join(truth, "screen_*.full.json"))):
        for o in load(f)["objects"]:
            s = screens.get(o["name"])
            n += 1
            if not s:
                errs.append("screen %s missing" % o["name"])
                continue
            for ti in o.get("items", []):
                it = next((x for x in s["items"] if x["name"] == ti["name"]), None)
                tds = ti.get("dynamizations") or []
                n += 1
                got = sorted(x["property"] for x in (it or {}).get("dynamizations", []))
                if it is not None and got != sorted(td["PropertyName"] for td in tds):
                    errs.append("%s/%s dynamized properties: parser %r, truth %r" % (
                        o["name"], ti["name"], got, sorted(td["PropertyName"] for td in tds)))
                for td in tds:
                    what = "%s/%s %s" % (o["name"], ti["name"], td["PropertyName"])
                    g = next((x for x in (it or {}).get("dynamizations", []) if x["property"] == td["PropertyName"]), {})
                    kind = td["DynamizationType"]
                    m = g.get("mapping") or {}
                    exp, got = [kind], [g.get("type")]
                    if kind in ("Tag", "ResourceList"):
                        exp.append(td.get("Tag"))
                        got.append(g.get("tag"))
                    if kind == "ResourceList":
                        exp.append(td.get("ResourceList"))
                        got.append(g.get("resourceList"))
                    if kind in ("Tag", "Expression"):
                        exp.append((td.get("ValueConverter") or {}).get("MappingTable", {}).get("ConditionType", "None"))
                        got.append(m.get("conditionType", "None"))
                    if "mappingEntries" in td:
                        exp.append(td["mappingEntries"])
                        got.append(m.get("entries"))
                    if kind == "Script":
                        exp.append(td.get("ScriptCode"))
                        got.append(g.get("code"))
                    if kind == "Flashing":
                        exp += [td.get("Color"), td.get("AlternateColor"), td.get("FlashingRate"), td.get("FlashingCondition")]
                        got += [g.get("color"), g.get("alternateColor"), g.get("flashingRate"), g.get("flashingCondition")]
                    n += 1
                    if got != exp:
                        errs.append("%s: parser %r, truth %r" % (what, got, exp))
    return n, errs


def main():
    rows, failed = [], 0
    for truth in sorted(glob.glob(os.path.join(SAMPLES, "1[0-9][0-9]_hmi_*_truth"))
                        + glob.glob(os.path.join(SAMPLES, "F[0-9][0-9]_hmi_*_truth"))):
        sample = os.path.basename(truth)[:-len("_truth")]
        for kind, src in (("folder", os.path.join(SAMPLES, sample)), ("zap21", os.path.join(SAMPLES, sample + ".zap21"))):
            if not os.path.exists(src):
                continue
            try:
                n, errs = check(sample, src)
            except Exception as e:  # noqa: BLE001
                n, errs = 0, ["%s: %s" % (type(e).__name__, e)]
            failed += bool(errs)
            rows.append((sample, kind, n, errs))
    sample = "F40_siemens_demo_bestpractices"  # project folder only (local copy, gitignored)
    if os.path.isdir(os.path.join(SAMPLES, sample)) and os.path.isdir(os.path.join(SAMPLES, sample + "_truth")):
        try:
            n, errs = check_f40(sample, os.path.join(SAMPLES, sample))
        except Exception as e:  # noqa: BLE001
            n, errs = 0, ["%s: %s" % (type(e).__name__, e)]
        failed += bool(errs)
        rows.append((sample, "folder", n, errs))
    lines =["# HMI tests (openpedata.hmi against Openness ground truth)", "",
             "| Sample | Source | Checks | Result |", "| --- | --- | --- | --- |"]
    for sample, kind, n, errs in rows:
        lines.append("| %s | %s | %d | %s |" % (sample, kind, n, "OK" if not errs else "%d errors" % len(errs)))
    for sample, kind, n, errs in rows:
        for e in errs:
            lines.append("- %s %s: %s" % (sample, kind, e))
    text = "\n".join(lines) + "\n"
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "RESULTS_HMI.md"), "w", encoding="utf-8") as f:
        f.write(text)
    sys.stdout.reconfigure(encoding="utf-8")
    print(text)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
