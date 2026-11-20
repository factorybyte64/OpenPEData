"""Recipes 100-172 (WinCC Unified samples: steps U1-U11, Z1.3 U1-U6, Z1.5 R0/R1 and later HMI samples of
samples/MANIFEST.md).

Every sample is one change against its predecessor (folder copy -> open -> change -> save -> truth -> archive -> close).
The ground truth files of these samples are results of TiaHmiUnified/TiaDevice/TiaNetwork* calls, partly reduced by the
original scripts (name lists, selected keys); the generators below make the same calls and apply the same reduction.
Notes written by hand into the old truth (source descriptions, editor observations) are taken over from the old file.

Differences of the current TiaMcpXE server to the one the samples were made with, compensated here to keep the content:
- hmiConnectionIntegrate joins both interfaces on a new subnet by default: connectSubnet false (U1 had none), and it
  leaves the connection online, the old samples have it offline: set connection Online false;
- partSet on the root object needs partPath "/" ("" is rejected as missing);
- itemBindTag with binding processValue now makes a tag dynamization; the static text "HmiTag_IO" of 121 is written
  with itemSet, the dynamization of 124 with itemBindTag tagName only."""
import json
import os
import re
import shutil

from rhelp import pause
from truth import _raw

H = "HMI_1"
PLC = "PLC_1"
PC = "PC-System_1"
PANEL = "OrderNumber:6AV2 128-3GB06-0AXx/21.0.1.0"
HMI_IF = "HMI_1.IE_CP_1/PROFINET Interface_1"
PLC_IF = "PLC_1/PROFINET interface_1"
VOLATILE = ("durationMs", "queueMs", "defaulted", "notices", "docsHint", "summary", "hint", "nextSteps")


def _jd(txt):
    try:
        return json.loads(txt)
    except ValueError:
        return {"raw": txt}


def _strip(d):
    if isinstance(d, dict):
        for k in VOLATILE:
            d.pop(k, None)
    return d


def _old(ctx, rel):
    p = os.path.join(ctx.old_truth, rel)
    if not os.path.isfile(p):
        return None
    with open(p, encoding="utf-8-sig") as f:
        return json.load(f)


def _dump(path, obj, indent=None):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(json.dumps(obj, ensure_ascii=False, indent=indent, separators=(",", ": ") if indent else (",", ":")))


def _keys_like(new, old):
    """`new` reduced to the keys of `old` (in the order of `old`): for truth files the original script reduced."""
    return {k: new.get(k) for k in old}


# --- change steps ------------------------------------------------------------------------------------------------------
def chg(tool, **a):
    """Change call recorded for change.json (tool, operation, arguments without operation/path, parsed result)."""
    def step(ctx):
        args = {k: v for k, v in a.items() if v is not None}
        res = _jd(ctx.call(tool, **args))
        if not hasattr(ctx, "changes"):
            ctx.changes = []
        ctx.changes.append({"tool": tool, "operation": args.get("operation"),
                            "arguments": {k: v for k, v in args.items() if k not in ("operation", "path")}, "result": res})
    return step


def hu(op, kind, **a):
    """Recorded TiaHmiUnified change on HMI_1 (device=None: call without device, e.g. plant view)."""
    dev = a.pop("device", H)
    return chg("TiaHmiUnified", operation=op, objectKind=kind, device=dev, **a)


def q(ctx, op, kind, ok_errors=False, **a):
    """Truth query (parsed, volatile keys dropped); device HMI_1 unless given (None: without device)."""
    a.setdefault("device", H)
    return _strip(_jd(_raw(ctx, "TiaHmiUnified", ok_errors=ok_errors, operation=op, objectKind=kind, **a)))


# --- truth generators ---------------------------------------------------------------------------------------------------
def _dev(rel):
    """Device of a truth file: the folder (HMI_1/..., PC-System_1/...), HMI_1 for top level files."""
    return rel.split("/")[0] if "/" in rel and rel.split("/")[0] in (H, PC) else H


def _plain(op, kind, **a):
    def g(ctx, out, rel):
        _dump(os.path.join(out, rel), q(ctx, op, kind, ok_errors=True, device=_dev(rel), **a))
    return g


def g_devices(ctx, out, rel):
    _dump(os.path.join(out, rel), _strip(_jd(_raw(ctx, "TiaDevice", operation="list"))))


def g_change(ctx, out, rel):
    _dump(os.path.join(out, rel), getattr(ctx, "changes", []))


def g_names(op, kind):
    """systemTags.json / systemTextLists.json: 100 wrote the names only, later samples the list result."""
    def g(ctx, out, rel):
        d = q(ctx, op, kind, device=_dev(rel))
        o = _old(ctx, rel) or {}
        if o.get("items") and isinstance(o["items"][0], str):
            d = {"count": d["count"], "items": [x["name"] for x in d["items"]]}
        _dump(os.path.join(out, rel), d)
    return g


def g_connection(ctx, out, rel):
    """connection_<name>.json: get connection; the hand-written key "export" of 100 is taken over from the old file."""
    d = q(ctx, "get", "connection", name=re.match(r".*connection_(.+)\.json$", rel).group(1))
    o = _old(ctx, rel) or {}
    if "export" in o:
        d["export"] = o["export"]
    _dump(os.path.join(out, rel), d)


def g_integrate(ctx, out, rel):
    """connection_integrate_result.json: result of the recorded hmiConnectionIntegrate call."""
    for c in getattr(ctx, "changes", []):
        if c["operation"] == "hmiConnectionIntegrate":
            _dump(os.path.join(out, rel), _strip(c["result"]))
            return


def g_device_items(ctx, out, rel):
    """deviceItems.json (100): device item tree of HMI_1 reduced to names/positions, leaves as names; notes from the
    old file."""
    d = _jd(_raw(ctx, "TiaDevice", operation="getDetails", device=H))
    o = _old(ctx, rel) or {}
    notes = {(x.get("name"), x.get("positionNumber")): x["note"] for x in o.get("deviceItems", []) if x.get("note")}

    def node(it, top):
        kids = it.get("deviceItems") or []
        if not top and not kids:
            return it["name"]
        n = {"name": it["name"], "positionNumber": it["positionNumber"]}
        if it.get("typeIdentifier"):
            n["typeIdentifier"] = it["typeIdentifier"]
        if kids:
            n["children"] = [node(k, False) for k in kids]
        if (it["name"], it["positionNumber"]) in notes:
            n["note"] = notes[(it["name"], it["positionNumber"])]
        return n
    res = {"name": d["name"], "typeIdentifier": d["deviceItems"][0].get("typeIdentifier"), "softwareKind": d["softwareKind"],
           "softwareName": d["softwareName"], "deviceItems": [node(x, True) for x in d["deviceItems"]]}
    _dump(os.path.join(out, rel), res)


def g_interfaces(ctx, out, rel):
    """interfaces.json (100): network interfaces of HMI_1 and PLC_1 (path, address, subnet)."""
    def ifs(dev):
        d = _jd(_raw(ctx, "TiaNetwork", operation="interfacesDiscover", device=dev))
        return [{"deviceItemPath": x["deviceItemPath"], "address": x.get("address"),
                 "connectedSubnetName": x.get("connectedSubnetName")} for x in d["items"] if x.get("hasNetworkInterface")]
    _dump(os.path.join(out, rel), {"items": ifs(H), "plc": ifs(PLC)})


def g_alarm_class(ctx, out, rel):
    _dump(os.path.join(out, rel), q(ctx, "get", "alarmClass", name=re.match(r".*alarmClass_(.+)\.json$", rel).group(1)))


def g_tags(ctx, out, rel):
    """tags.json: get tag (all); 105 also wrote the texts and compositions of the root part (partGet "/")."""
    d = q(ctx, "get", "tag")
    o = _old(ctx, rel) or {}
    if any("compositions" in x for x in o.get("objects", [])):
        for x in d.get("objects", []):
            p = q(ctx, "partGet", "tag", name=x["Name"], partPath="/")
            for k in ("Comment", "DisplayName", "compositions"):
                if k in p:
                    x[k] = p[k]
    _dump(os.path.join(out, rel), d)


def g_tag(ctx, out, rel):
    """tag_<name>.json: get tag; 119 wrote a selection of keys (reduced like the original)."""
    d = q(ctx, "get", "tag", name=re.match(r".*tag_(.+)\.json$", rel).group(1))
    o = _old(ctx, rel) or {}
    if len(o) < 10:
        d = _keys_like(d, o)
    _dump(os.path.join(out, rel), d)


def g_alarms(kind, texts):
    def g(ctx, out, rel):
        d = q(ctx, "get", kind, includeTexts=texts)
        d.pop("includeTexts", None)
        o = _old(ctx, rel) or {}
        olds = {x.get("Name"): x for x in o.get("objects", [])}
        if texts == "raw":  # the original reduced the raw texts (EventText1-9 combined when equal)
            objs = []
            for x in d.get("objects", []):
                ox = olds.get(x.get("Name"), {})
                ev = [x.pop("EventText%d" % i, None) for i in range(1, 10)]
                if "EventText1..9" in ox and all(e == ev[0] for e in ev):
                    x["EventText1..9"] = ev[0]
                objs.append(_keys_like(x, ox) if ox else x)
            d["objects"] = objs
        _dump(os.path.join(out, rel), d)
    return g


def g_screens_full(ctx, out, rel):
    """screens_full.json: get screen detail=full; 128/129 added the compositions Points/SelectionItems of the items
    (partGet per element, keys as in the original)."""
    d = q(ctx, "get", "screen", device=_dev(rel), detail="full")
    o = _old(ctx, rel) or {}
    oitems = {(s["name"], i["name"]): i for s in o.get("objects", []) for i in s.get("items", [])}
    for s in d.get("objects", []):
        for it in s.get("items", []):
            oi = oitems.get((s["name"], it["name"]), {}).get("attributes", {})
            for comp in ("Points", "SelectionItems"):
                if comp not in oi:
                    continue
                keys = list(dict.fromkeys(k for e in oi[comp] for k in e))
                lst = q(ctx, "partGet", "screen", device=_dev(rel), screenName=s["name"], name=it["name"], partPath=comp)
                els = []
                for e in lst.get("elements", []):
                    p = q(ctx, "partGet", "screen", device=_dev(rel), screenName=s["name"], name=it["name"],
                          partPath="%s/#%d" % (comp, e["index"]))
                    els.append({k: p.get(k) for k in keys})
                it.setdefault("attributes", {})[comp] = els
    _dump(os.path.join(out, rel), d)


def g_export_xml(ctx, out, rel):
    """<screen>.xml (110): exportXml of the screen (TiaMcpXE's own XML)."""
    _raw(ctx, "TiaHmiUnified", operation="exportXml", objectKind="screen", device=_dev(rel), name=os.path.basename(rel)[:-4],
         exportDir=os.path.dirname(os.path.join(out, rel)))


def g_subnet(ctx, out, rel):
    _dump(os.path.join(out, rel), _strip(_jd(_raw(ctx, "TiaNetworkSubnet", operation="get", subnet="PN/IE_1"))))


def g_item_full(ctx, out, rel):
    """item_<name>.full.json (135): itemGet detail=full on Screen_1."""
    _dump(os.path.join(out, rel), q(ctx, "itemGet", "screen", screenName="Screen_1",
                                    name=re.match(r".*item_(.+)\.full\.json$", rel).group(1), detail="full"))


def g_export(kind, sub):
    """Openness export of script modules / text lists / graphic lists into <out>/<device>/<sub>."""
    def g(ctx, out, rel):
        d = os.path.join(out, _dev(rel), sub) if sub else os.path.dirname(os.path.join(out, rel))
        if os.path.exists(os.path.join(out, rel)):
            return
        os.makedirs(d, exist_ok=True)
        _raw(ctx, "TiaHmiUnified", operation="export", objectKind=kind, device=_dev(rel), exportDir=d)
    return g


def g_project_languages(ctx, out, rel):
    _dump(os.path.join(out, rel), _strip(_jd(_raw(ctx, "TiaProject", operation="languageGet"))))


def g_plant(op, kind):
    def g(ctx, out, rel):
        _dump(os.path.join(out, rel), q(ctx, op, kind, device=None))
    return g


def g_runtime_scope(ctx, out, rel):
    _dump(os.path.join(out, rel), q(ctx, "get", "runtimeSetting", scope=re.match(r".*runtimeSetting_(.+)\.json$", rel).group(1)))


def g_screen_items(ctx, out, rel):
    """screenItems/<screen>.json (165): screen get (items) and itemGet detail=full of <screen>_1."""
    s = os.path.basename(rel)[:-5]
    _dump(os.path.join(out, rel), {"screen": q(ctx, "get", "screen", name=s),
                                   "item": q(ctx, "itemGet", "screen", screenName=s, name=s + "_1", detail="full")})


def g_start_screen(ctx, out, rel):
    """runtimeSetting.json (169): StartScreen before (read by the recipe before the change) and after."""
    o = _old(ctx, rel)
    after = q(ctx, "get", "runtimeSetting")["properties"]["StartScreen"]
    _dump(os.path.join(out, rel), {"source": o["source"],
                                   "before": {"sample": ctx.rec["from"], "StartScreen": ctx.start_screen_before},
                                   "after": {"sample": ctx.name, "StartScreen": after}})


def g_plant_view_nodes(ctx, out, rel):
    """plantView.json (170): nodes of the plant view, interface tags of CPM objects reduced to the keys of the original."""
    o = _old(ctx, rel)
    lst = q(ctx, "list", "plantViewNode", device=None)
    okeys = [k for n in o["nodes"] for t in n.get("interfaceTags", []) for k in t]
    okeys = list(dict.fromkeys(okeys))
    nodes = []
    for n in lst["items"]:
        x = {"name": n["name"], "path": n["path"], "plantObjectType": n.get("plantObjectType")}
        if n.get("plantObjectType"):
            g = q(ctx, "get", "plantViewNode", device=None, nodePath=n["path"])
            x["interfaceTags"] = [{k: t.get(k) for k in okeys} for t in g.get("interfaceTags", [])]
        nodes.append(x)
    _dump(os.path.join(out, rel), {"plantView": lst.get("plantView"), "nodes": nodes})


def g_thresholds(ctx, out, rel):
    """thresholds.json (171): layout, scale and thresholds (partGet Thresholds/#n) of Bar_T and Gauge_T on
    PC-System_1/Screen_1; source and editor notes from the old file."""
    o = _old(ctx, rel)
    items = {}
    for name, it in o["items"].items():
        full = q(ctx, "itemGet", "screen", device=PC, screenName="Screen_1", name=name, detail="full")
        x = {k: full.get(k) for k in ("Left", "Top", "Width", "Height")}
        scale = [k for k in it if k.endswith("Scale")][0]
        x[scale] = {k: (full.get(scale) or {}).get(k) for k in it[scale]}
        ths = []
        for i, ot in enumerate(it["Thresholds"]):
            t = q(ctx, "partGet", "screen", device=PC, screenName="Screen_1", name=name, partPath="Thresholds/#%d" % i)
            ths.append({k: t.get(k) for k in ot})
        x["Thresholds"] = ths
        items[name] = x
    _dump(os.path.join(out, rel), {"source": o["source"], "screen": o["screen"], "items": items, "editor": o["editor"]},
          indent=1)


def _typed(value, like):
    """itemGet returns numbers as text; keep the JSON type of the original truth file (the tests read numbers)."""
    if isinstance(like, int) and not isinstance(like, bool) and isinstance(value, str):
        try:
            return int(value)
        except ValueError:
            return value
    return value


def g_bar_items(ctx, out, rel):
    """items.json (172): BarMode and ProcessValue of Bar_T and Gauge_T; source and runtime note from the old file."""
    o = _old(ctx, rel)
    items = []
    for it in o["items"]:
        full = q(ctx, "itemGet", "screen", device=PC, screenName=o["screen"], name=it["name"], detail="full")
        items.append({"name": it["name"], **{k: _typed(full.get(k), it[k]) for k in it if k != "name"}})
    _dump(os.path.join(out, rel), {"source": o["source"], "screen": o["screen"], "items": items, "runtime": o["runtime"]},
          indent=2)


HMI_TRUTH = [
    (r"devices\.json", g_devices),
    (r"change\.json", g_change),
    (r"[^/]+/alarmClasses\.json", _plain("list", "alarmClass")),
    (r"[^/]+/alarmClass_.+\.json", g_alarm_class),
    (r"[^/]+/connection_integrate_result\.json", g_integrate),
    (r"[^/]+/connection_.+\.json", g_connection),
    (r"[^/]+/connections\.json", _plain("list", "connection")),
    (r"[^/]+/deviceItems\.json", g_device_items),
    (r"[^/]+/interfaces\.json", g_interfaces),
    (r"[^/]+/languageAndFont\.json", _plain("list", "languageAndFont")),
    (r"[^/]+/runtimeSettings?\.json", _plain("get", "runtimeSetting")),
    (r"[^/]+/runtimeSetting_.+\.json", g_runtime_scope),
    (r"[^/]+/software\.json", _plain("get", "software")),
    (r"[^/]+/systemTags\.json", g_names("list", "systemTag")),
    (r"[^/]+/systemTextLists\.json", g_names("list", "systemTextList")),
    (r"[^/]+/tagTables\.json", _plain("list", "tagTable")),
    (r"[^/]+/tagTableGroups\.json", _plain("list", "tagTableGroup")),
    (r"[^/]+/tagList\.json", _plain("list", "tag")),
    (r"[^/]+/tags\.json", g_tags),
    (r"[^/]+/tag_.+\.json", g_tag),
    (r"[^/]+/discreteAlarms\.json", g_alarms("discreteAlarm", "true")),
    (r"[^/]+/discreteAlarms_raw\.json", g_alarms("discreteAlarm", "raw")),
    (r"[^/]+/analogAlarms_raw\.json", g_alarms("analogAlarm", "raw")),
    (r"[^/]+/screens\.json", _plain("list", "screen")),
    (r"[^/]+/screens_full\.json", g_screens_full),
    (r"[^/]+/screens_items\.json", _plain("get", "screen")),
    (r"[^/]+/screenGroups\.json", _plain("groupList", "screen")),
    (r"[^/]+/Screen_\d+\.xml", g_export_xml),
    (r"[^/]+/subnet_PN_IE_1\.json", g_subnet),
    (r"[^/]+/item_.+\.full\.json", g_item_full),
    (r"[^/]+/screenItems/[^/]+\.json", g_screen_items),
    (r"[^/]+/(alarmLog|auditTrail|dataLog|loggingTag|scriptModule)List\.json",
     lambda ctx, out, rel: _plain("list", re.match(r".*/(\w+)List\.json$", rel).group(1))(ctx, out, rel)),
    (r"[^/]+/(alarmLog|auditTrail|dataLog|loggingTag|scriptModule|graphicList|textList|opcUaAlarmType)\.json",
     lambda ctx, out, rel: _plain("get", re.match(r".*/(\w+)\.json$", rel).group(1))(ctx, out, rel)),
    (r"[^/]+/plantView\.json", g_plant("list", "plantView")),
    (r"[^/]+/plantViewNode\.json", g_plant("list", "plantViewNode")),
    (r"[^/]+/projectLanguages\.json", g_project_languages),
    (r"[^/]+/scriptModules/.+", g_export("scriptModule", "scriptModules")),
    (r"[^/]+/textLists/.+", g_export("textList", "textLists")),
    (r"[^/]+/graphicLists/.+", g_export("graphicList", "graphicLists")),
    (r"[^/]+/HmiGraphicLists\.hmi\.yml", g_export("graphicList", None)),
    (r"runtimeSetting\.json", g_start_screen),
    (r"plantView\.json", g_plant_view_nodes),
    (r"thresholds\.json", g_thresholds),
    (r"items\.json", g_bar_items),
]


def hmi(pred, *steps, **extra):
    r = {"from": pred, "steps": list(steps), "generators": HMI_TRUTH}
    r.update(extra)
    return r


# --- special steps -------------------------------------------------------------------------------------------------------
# the panel and its integrated connection (U1); connectSubnet false: the original server left both interfaces without
# subnet (MANIFEST U1), the current one joins them on a new PN/IE_1 by default
DEVICE = [chg("TiaDevice", operation="create", name=H, deviceType=PANEL),
          chg("TiaNetwork", operation="hmiConnectionIntegrate", hmiDevice=H, plcDevice=PLC, connectionName="HMI_Connection_1",
              hmiInterfacePath=HMI_IF, plcInterfacePath=PLC_IF, connectSubnet="false"),
          # the original server reset DisabledAtStartup to false, which Openness V21 inverts: every old sample has the
          # connection offline (TIA connection editor: "Online" unchecked)
          chg("TiaHmiUnified", operation="set", objectKind="connection", device=H, name="HMI_Connection_1", attribute="Online",
              value="false")]

SCRIPT_130 = ('// line comment\r\nlet a = 1;   /* block comment */\r\nif (a > 0) {\r\n\tHMIRuntime.Trace("multi line");\r\n\r\n'
              '    a = a + 2;\r\n}')

# self-written graphic list file of 159 (MANIFEST Z1.3 U5)
GL_159 = "#Version: 2.0\n\nGraphicListContainers:\n  DeviceGraphicList:\n    ResourceListType: GraphicList\n    ResourceLists:\n" \
         "      GL_State:\n        Entries: {}\n"
# graphic list of 168 with two entries. The original server did not take the graphic references over from YAML (they
# were selected by hand in TIA); the current one does, so no manual step is needed
GL_168 = "#Version: 2.0\n\nGraphicListContainers:\n  DeviceGraphicList:\n    ResourceListType: GraphicList\n    ResourceLists:\n" \
         "      GL_State:\n        Entries:\n          Graphic_list_entry_0:\n            Graphic: GraphicLibrary.Gfx_Red\n" \
         "          Graphic_list_entry_1:\n            Value: 1\n            FromValue: 1\n            ToValue: 1\n" \
         "            Graphic: GraphicLibrary.Gfx_Green\n"


def gl_import(text, sub):
    """Step: write the graphic list YAML to <stage>\\work\\<sub>\\GL.hmi.yml and import it (recorded)."""
    def step(ctx):
        d = os.path.join(ctx.stage, "work", sub)
        if os.path.exists(d):
            shutil.rmtree(d)
        os.makedirs(d)
        with open(os.path.join(d, "GL.hmi.yml"), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        hu("import", "graphicList", importDir=d, name="GL.hmi.yml")(ctx)
    return step


# the own 16x16 PNGs of 168 (red and green square), as exported from the old sample (graphicExport)
GFX_168 = {
    "Gfx_Red": "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAF0lEQVR4nGM4wcBAEiJN9aiGUQ1DSgMAHQ3IAQ5sGQoAAAAASUVORK5CYII=",
    "Gfx_Green": "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAFUlEQVR4nGNgWMVAGhrVMKph+GoAAGJMqgHK0pc4AAAAAElFTkSuQmCC",
}


def graphic_import(ctx):
    """Step: TiaProject(graphicImport) of the project graphics Gfx_Red/Gfx_Green (PNG written to <stage>\\work)."""
    import base64
    d = os.path.join(ctx.stage, "work", "gfx_168")
    os.makedirs(d, exist_ok=True)
    for n, b64 in GFX_168.items():
        p = os.path.join(d, n + ".png")
        with open(p, "wb") as f:
            f.write(base64.b64decode(b64))
        chg("TiaProject", operation="graphicImport", sourcePath=p, name=n)(ctx)


ITEM_TYPES_165 = ["Text", "TextBox", "Rectangle", "GraphicView", "Line", "IOField", "ScreenWindow", "Circle", "Button",
                  "ToggleSwitch", "Slider", "Bar", "AlarmControl", "AlarmLineControl", "SymbolicIOField", "AlarmIndicator",
                  "Polyline", "ListBox", "SystemDiagnosisControl", "Clock", "CircularArc", "CircleSegment", "EllipticalArc",
                  "Ellipse", "EllipseSegment", "Polygon", "FunctionTrendControl", "MediaControl", "TrendControl",
                  "WebControl", "CheckBoxGroup", "Gauge", "RadioButtonGroup", "TouchArea", "DetailedParameterControl",
                  "TrendCompanion", "ProcessDiagnosisOverviewControl"]


def item_defaults(ctx):
    """165: per item type one screen <type> with one item <type>_1 without attributes (change.json as the original
    script z15defaults.py wrote it)."""
    ctx.changes = []
    for t in ITEM_TYPES_165:
        sc = _jd(ctx.call("TiaHmiUnified", operation="create", objectKind="screen", device=H, name=t))
        it = _jd(ctx.call("TiaHmiUnified", operation="itemCreate", objectKind="screen", device=H, screenName=t,
                          name=t + "_1", itemType="Hmi" + t))
        ctx.changes.append({"screen": t, "itemType": "Hmi" + t, "screenCreate": sc, "itemCreate": it})


def start_screen_before(ctx):
    ctx.start_screen_before = _jd(ctx.call("TiaHmiUnified", operation="get", objectKind="runtimeSetting",
                                           device=H))["properties"]["StartScreen"]


def item_set(name, attr, value, device=H, screen="Screen_1"):
    return hu("itemSet", "screen", device=device, screenName=screen, name=name, attribute=attr, value=value)


# --- manual steps (TIA user interface via VNC) ------------------------------------------------------------------------
NOTE_164 = ("164_hmi_pc_base: in TIA Portal (project of this sample open), Project tree > 'Add new device' > 'PC systems' > "
            "'SIMATIC HMI application' > 'SIMATIC WinCC Unified PC' > 'WinCC Unified PC RT', version 21.0.0.0 > OK. "
            "TIA creates station PC-System_1 with software HMI_RT_1. Then PC-System_1 > HMI_RT_1 > Screens > double-click "
            "'Add new screen' (creates Screen_1, which becomes the start screen). Do not save; delete the pause file.")
NOTE_166 = ("{name}: in TIA Portal, Project tree > HMI_1 > double-click 'Runtime settings' > section General > Screen > "
            "'Style' drop-down = '{style}'. Do not save; delete the pause file.")
NOTE_170 = ("170_hmi_plant_object_type: in TIA Portal, Project tree > tab 'Plant objects' > toolbar button 'Show plant object "
            "types' > task card 'Plant object types' (right) > button 'Add new plant object type' (wait for 'Setting up "
            "environment'), type the name Mixer + Enter (no members). Do not save; delete the pause file.")

RECIPES = {
    # --- U1/U2: device, base, HMI tags ---
    "100_hmi_device": hmi("002_cpu", *DEVICE),
    "101_hmi_base": hmi("003_tagtable", *DEVICE),
    "102_hmi_tag_int": hmi("101_hmi_base", hu("create", "tag", name="HmiTag_Int1", dataType="Bool")),
    "103_hmi_tag_ext": hmi("101_hmi_base", hu("create", "tag", name="HmiTag_Ext1", connection="HMI_Connection_1",
                                             plcTag="Tag_Bool_1")),
    "104_hmi_tag_cycle": hmi("103_hmi_tag_ext", hu("set", "tag", name="HmiTag_Ext1", attribute="AcquisitionCycle",
                                                   value="T100ms")),
    "105_hmi_tag_comment": hmi("103_hmi_tag_ext", hu("partSet", "tag", name="HmiTag_Ext1", partPath="/", attribute="Comment",
                                                     value="Test", language="en-US")),
    "106_hmi_tagtable_group": hmi("101_hmi_base", hu("create", "tagTableGroup", name="HmiTagGroup_1"),
                                  hu("create", "tagTable", name="HmiTagTable_1", groupPath="HmiTagGroup_1")),
    # --- U3: alarms ---
    "107_hmi_alarmclass": hmi("103_hmi_tag_ext", hu("create", "alarmClass", name="AlarmClass_Test")),
    "108_hmi_alarm_discrete": hmi("103_hmi_tag_ext", hu("create", "discreteAlarm", name="Alarm_1", language="en-US", attributes=json.dumps(
        {"RaisedStateTag": "HmiTag_Ext1", "RaisedStateTagBitNumber": 0, "AlarmClass": "Alarm", "EventText": "Test alarm"}))),
    "109_hmi_alarm_analog": hmi("103_hmi_tag_ext", hu("create", "tag", name="HmiTag_Int2", dataType="Int"),
                                hu("create", "analogAlarm", name="AnalogAlarm_1", language="en-US", attributes=json.dumps(
                                    {"RaisedStateTag": "HmiTag_Int2", "AlarmClass": "Warning", "Condition": "UpperLimit",
                                     "ConditionValue": 100, "EventText": "Test analog alarm"}))),
    # --- U4: screens ---
    "110_hmi_screen": hmi("101_hmi_base", hu("create", "screen", name="Screen_1")),
    "111_hmi_screen_rename": hmi("110_hmi_screen", hu("partSet", "screen", screenName="Screen_1", partPath="/", attribute="Name",
                                                      value="Screen_Renamed")),
    "112_hmi_screen_backcolor": hmi("110_hmi_screen", hu("partSet", "screen", screenName="Screen_1", partPath="/",
                                                         attribute="BackColor", value="0xFF00FF00")),
    "113_hmi_screen_group": hmi("101_hmi_base", hu("groupCreate", "screen", name="ScreenGroup_1"),
                                hu("create", "screen", name="Screen_G1", groupPath="ScreenGroup_1")),
    "114_hmi_screen2": hmi("110_hmi_screen", hu("create", "screen", name="Screen_2")),
    # --- U5: screen items ---
    "115_hmi_item_rect": hmi("110_hmi_screen", hu("itemCreate", "screen", screenName="Screen_1", name="Rectangle_1",
                                                  itemType="HmiRectangle")),
    "116_hmi_item_rect_pos": hmi("115_hmi_item_rect", item_set("Rectangle_1", "Left", "100")),
    "117_hmi_item_rect_color": hmi("115_hmi_item_rect", item_set("Rectangle_1", "BackColor", "0xFFFF0000")),
    "118_hmi_item_text": hmi("110_hmi_screen", hu("itemCreate", "screen", screenName="Screen_1", name="Text_1", itemType="HmiText",
                                                  attributes=json.dumps({"Text": "Hello"}), language="en-US")),
    "119_hmi_screen_tag": hmi("110_hmi_screen", hu("create", "tag", name="HmiTag_IO", dataType="Int")),
    "120_hmi_item_iofield": hmi("119_hmi_screen_tag", hu("itemCreate", "screen", screenName="Screen_1", name="IOField_1",
                                                         itemType="HmiIOField")),
    "121_hmi_item_iofield_pvtext": hmi("120_hmi_item_iofield", item_set("IOField_1", "ProcessValue", "HmiTag_IO")),
    "122_hmi_item_button": hmi("110_hmi_screen", hu("itemCreate", "screen", screenName="Screen_1", name="Button_1",
                                                    itemType="HmiButton")),
    "123_hmi_button_event": hmi("122_hmi_item_button", hu("itemEventCreate", "screen", screenName="Screen_1", name="Button_1",
                                                          eventType="Tapped", script='HMIRuntime.Trace("Button_1 tapped");')),
    "124_hmi_item_iofield_dyn": hmi("120_hmi_item_iofield", hu("itemBindTag", "screen", screenName="Screen_1", name="IOField_1",
                                                               tagName="HmiTag_IO")),
    # --- U8: runtime files ---
    "126_hmi_subnet": hmi("124_hmi_item_iofield_dyn",
                          chg("TiaNetworkSubnet", operation="nodeCreateAndConnect", device=PLC, deviceItemPath=PLC_IF,
                              subnetType="Profinet"),
                          chg("TiaNetworkSubnet", operation="nodeConnect", device=H, deviceItemPath=HMI_IF, subnet="PN/IE_1")),
    # --- U10: gaps in the hmi parser ---
    "128_hmi_item_polyline": hmi("110_hmi_screen", hu("itemCreate", "screen", screenName="Screen_1", name="Polyline_1",
                                                      itemType="HmiPolyline")),
    "129_hmi_item_listbox": hmi("110_hmi_screen", hu("itemCreate", "screen", screenName="Screen_1", name="ListBox_1",
                                                     itemType="HmiListBox")),
    "130_hmi_script_multiline": hmi("122_hmi_item_button", hu("itemEventCreate", "screen", screenName="Screen_1",
                                                              name="Button_1", eventType="Tapped", script=SCRIPT_130)),
    "131_hmi_event_down": hmi("122_hmi_item_button", hu("itemEventCreate", "screen", screenName="Screen_1", name="Button_1",
                                                        eventType="Down", script='HMIRuntime.Trace("down");')),
    "132_hmi_dyn_color": hmi("124_hmi_item_iofield_dyn", hu("itemBindTag", "screen", screenName="Screen_1", name="IOField_1",
                                                            tagName="HmiTag_IO", propertyName="BackColor", threshold="100",
                                                            valueAbove="0xFFFF0000", valueBelow="0xFFFFFFFF")),
    "133_hmi_dyn_script": hmi("115_hmi_item_rect", hu("itemBindScript", "screen", screenName="Screen_1", name="Rectangle_1",
                                                      propertyName="Visible", script="return true;")),
    # --- U11 ---
    "134_hmi_dyn_range3": hmi("124_hmi_item_iofield_dyn", hu("itemBindTag", "screen", screenName="Screen_1", name="IOField_1",
                                                             tagName="HmiTag_IO", propertyName="Width", rangeEntries=json.dumps(
                                                                 [{"from": 0, "to": 10, "value": "100"},
                                                                  {"from": 11, "to": 20, "value": "150"},
                                                                  {"from": 21, "to": 30, "value": "200"}]))),
    "135_hmi_item_alarmind": hmi("110_hmi_screen", hu("itemCreate", "screen", screenName="Screen_1", name="AlarmInd_1",
                                                      itemType="HmiAlarmIndicator")),
    # --- Z1.3 U1: logging ---
    "136_hmi_datalog": hmi("102_hmi_tag_int", hu("create", "dataLog", name="DataLog_1")),
    "137_hmi_datalog_size": hmi("136_hmi_datalog", hu("set", "dataLog", name="DataLog_1", attribute="Settings.LogMaxSize",
                                                      value="200")),
    "138_hmi_logging_tag": hmi("137_hmi_datalog_size", hu("create", "loggingTag", tagName="HmiTag_Int1", name="HmiTag_Int1_Log"),
                               hu("set", "loggingTag", tagName="HmiTag_Int1", name="HmiTag_Int1_Log", attribute="DataLog",
                                  value="DataLog_1")),
    "139_hmi_alarmlog": hmi("138_hmi_logging_tag", hu("create", "alarmLog", name="AlarmLog_1")),
    "140_hmi_logging_cyclic": hmi("139_hmi_alarmlog", hu("set", "loggingTag", tagName="HmiTag_Int1", name="HmiTag_Int1_Log",
                                                         attribute="LoggingMode", value="Cyclic")),
    "141_hmi_datalog_period": hmi("140_hmi_logging_cyclic", hu("set", "dataLog", name="DataLog_1",
                                                               attribute="Settings.LogTimePeriod.Days", value="14")),
    "142_hmi_alarmlog_size": hmi("141_hmi_datalog_period", hu("set", "alarmLog", name="AlarmLog_1",
                                                              attribute="Settings.LogMaxSize", value="300")),
    "143_hmi_logging_cycle": hmi("142_hmi_alarmlog_size", hu("set", "loggingTag", tagName="HmiTag_Int1", name="HmiTag_Int1_Log",
                                                             attribute="Cycle", value="T10s")),
    "144_hmi_datalog_backup": hmi("143_hmi_logging_cycle", hu("set", "dataLog", name="DataLog_1", attribute="Backup.BackupMode",
                                                              value="PrimaryPath")),
    # --- Z1.3 U2: runtime settings ---
    "145_hmi_rt_gmp": hmi("102_hmi_tag_int", hu("set", "runtimeSetting", scope="root", attribute="GMPEnabled", value="true")),
    "146_hmi_audittrail_size": hmi("145_hmi_rt_gmp", hu("set", "auditTrail", name="Audit trail_1",
                                                        attribute="Settings.LogMaxSize", value="20000")),
    "147_hmi_rt_panning": hmi("145_hmi_rt_gmp", hu("set", "runtimeSetting", scope="root", attribute="CentralPanning",
                                                   value="Enabled")),
    "148_hmi_rt_maxlogin": hmi("147_hmi_rt_panning", hu("set", "runtimeSetting", scope="maxLogin",
                                                        attribute="EnableLockAfterNumberOfAttempts", value="true")),
    "149_hmi_rt_maxlogin_errors": hmi("148_hmi_rt_maxlogin", hu("set", "runtimeSetting", scope="maxLogin",
                                                                attribute="MaxLoginErrors", value="5")),
    "150_hmi_rt_prodiag": hmi("149_hmi_rt_maxlogin_errors", hu("set", "runtimeSetting", scope="processDiagnostics",
                                                               attribute="EnableProcessDiagnostics", value="true")),
    "151_hmi_rt_opcua": hmi("150_hmi_rt_prodiag", hu("set", "runtimeSetting", scope="opcUa", attribute="ActAsOPCServer",
                                                     value="true")),
    "152_hmi_rt_reporting": hmi("151_hmi_rt_opcua", hu("set", "runtimeSetting", scope="reporting", attribute="IsReportingEnabled",
                                                       value="true")),
    # --- Z1.3 U3: languages ---
    "153_hmi_lang_de": hmi("102_hmi_tag_int", chg("TiaProject", operation="languageSet", add="de-DE")),
    "154_hmi_lang_enable": hmi("153_hmi_lang_de", hu("set", "languageAndFont", index="1", attribute="Enable", value="true")),
    "155_hmi_lang_logging": hmi("154_hmi_lang_enable", hu("set", "languageAndFont", index="1", attribute="EnableForLogging",
                                                          value="true")),
    # --- Z1.3 U4: global script modules ---
    "156_hmi_script_module": hmi("102_hmi_tag_int", hu("create", "scriptModule", name="Utils",
                                                       code="export function Add(a, b) {\n  return a + b;\n}")),
    "157_hmi_script_globaldef": hmi("156_hmi_script_module", hu(
        "create", "scriptModule", name="Utils", replace="true", globalDefinitions="const FACTOR = 10;",
        code="export function Add(a, b) {\n  return a + b;\n}\nexport function Scale(x) {\n  return x * FACTOR;\n}")),
    # --- Z1.3 U5: text and graphic lists ---
    "158_hmi_textlist": hmi("102_hmi_tag_int", hu("create", "textList", name="TL_State", entries=json.dumps(
        [{"value": 0, "text": "Stopped"}, {"value": 1, "text": "Running"}], separators=(",", ":")))),
    "159_hmi_graphiclist": hmi("158_hmi_textlist", gl_import(GL_159, "glimport_159")),
    # --- Z1.3 U6: plant view, OPC UA ---
    "160_hmi_plantview": hmi("102_hmi_tag_int", hu("create", "plantView", device=None, name="PV_Plant")),
    "161_hmi_plantview_node": hmi("160_hmi_plantview", hu("create", "plantViewNode", device=None, name="Area1")),
    "162_hmi_opcua_connection": hmi("161_hmi_plantview_node", hu("create", "connection", name="OPCUA_Conn",
                                                                 communicationDriver="OPC UA")),
    "163_hmi_opcua_alarmtype": hmi("162_hmi_opcua_connection", hu("create", "opcUaAlarmType", name="AT_Test",
                                                                  nodeId="ns=3;s=MyAlarmType", connection="OPCUA_Conn")),
    # --- Z1.5 R0/R1 ---
    # Openness V21 cannot create the WinCC Unified PC RT (MCP-098): added by hand in the TIA user interface
    "164_hmi_pc_base": hmi("110_hmi_screen", pause(NOTE_164)),
    "165_hmi_item_defaults": hmi("110_hmi_screen", item_defaults),
    # the style is not settable through Openness V21: by hand; these samples are project folders (no archive)
    "166_hmi_style_dark": hmi("165_hmi_item_defaults", pause(NOTE_166.format(name="166_hmi_style_dark", style="Dark style")),
                              archive=False),
    "167_hmi_style_bright": hmi("165_hmi_item_defaults", pause(NOTE_166.format(name="167_hmi_style_bright",
                                                                               style="Bright style")), archive=False),
    "168_hmi_graphiclist_entry": hmi("159_hmi_graphiclist", graphic_import,
                                     gl_import(GL_168, "glimport_168")),
    "169_hmi_start_screen": hmi("114_hmi_screen2", start_screen_before,
                                hu("set", "runtimeSetting", scope="root", attribute="StartScreen", value="Screen_2")),
    "170_hmi_plant_object_type": hmi("161_hmi_plantview_node", pause(NOTE_170),
                                     hu("create", "plantViewNode", device=None, name="Mixer1", parentPath="Area1",
                                        plantObjectType="Mixer")),
    "171_hmi_thresholds": hmi(
        "164_hmi_pc_base",
        hu("itemCreate", "screen", device=PC, screenName="Screen_1", name="Bar_T", itemType="HmiBar",
           attributes=json.dumps({"Left": 40, "Top": 40, "Width": 120, "Height": 300})),
        hu("itemCreate", "screen", device=PC, screenName="Screen_1", name="Gauge_T", itemType="HmiGauge",
           attributes=json.dumps({"Left": 240, "Top": 40, "Width": 300, "Height": 300})),
        *[hu("partSet", "screen", device=PC, screenName="Screen_1", name="Bar_T", partPath="Thresholds/#%d" % i,
             attribute="Enable", value="true") for i in (0, 1)],
        *[hu("partSet", "screen", device=PC, screenName="Screen_1", name="Gauge_T", partPath="Thresholds/#%d" % i,
             attribute="Enable", value="true") for i in range(5)],
        item_set("Gauge_T", "CurvedScale.EndValue", "100", device=PC)),
    "172_hmi_bar_segmented": hmi("171_hmi_thresholds",
                                 item_set("Bar_T", "BarMode", "Segmented", device=PC), item_set("Bar_T", "ProcessValue", "30", device=PC),
                                 item_set("Gauge_T", "BarMode", "SegmentedStatic", device=PC),
                                 item_set("Gauge_T", "ProcessValue", "55", device=PC)),
}

MANUAL = {
    "164_hmi_pc_base": "WinCC Unified PC RT station added in the TIA user interface (VNC): " + NOTE_164,
    "166_hmi_style_dark": "runtime style set in the TIA user interface (VNC)",
    "167_hmi_style_bright": "runtime style set in the TIA user interface (VNC)",
    "170_hmi_plant_object_type": "plant object type Mixer created in the TIA user interface (VNC)",
}
