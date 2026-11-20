"""Faceplate test: openpedata.faceplates (WinCC Unified faceplate types, versions, content, hidden device, instances)
over every sample (project folder and .zap21) against the Openness ground truth of F03 and F05
(<sample>_truth/typesList.json; F02 holds two of the F03 types and is checked against the F03 truth; F05 also
itemGet of the instance on Main and partGet Interface of an FP_Interface instance) and the expectations of step F3
(analysis/F3_faceplate HIDDEN-DEVICE.md, F03.md, F05.md): screen items per version, 79 standard objects of the hidden
device per version, tag shells, scripts, interface tags, instances, the typeless container C_Faceplate1.
Scripts and dynamizations of the types FP_Interface / FP_Script (F03, F05) are compared with the Openness truth of the
screens they were made from (F01_hmi_allobjects_truth/HMI_1/screen_<type>.full.json, SCRIPTS.md).
F40 (Siemens demo project, local copy only, truth not in the repo; skipped without its truth): faceplate types against typesList.json, every instance
against item_<screen>_<name>.json (ContainedType, position, size, interface values and whether they are dynamized)
and four interface dynamizations against interface_dynamizations.json.
F16 (User: all 11 property interface types in FP_Interface, versions 0.0.2 and 0.0.3, F16.md): versions of FP_Interface
against typesList_FP_Interface.json, interface properties of the default version (name, data type, default) against
the interface of a helper instance (instance_FP_Iface_3.json).
F18 (User: interface event EvTest(Nr: Int, Flag: Bool) in FP_Interface 0.0.4/0.0.5, raised by Button_1, F18.md): fixed
expectations from User's configuration (event, parameters, RaiseEvent script, five versions).
F18b (User: instance of FP_Interface 0.0.5 on Main with handler OnEvTest and interface values): against the Openness
view of the instance (F18b_event_handler_truth/instance_Main_Faceplate_container_2.json).
F12 (User: interface tags Real, DateTime, Bool, Array [0..3] of Int, HMI UDT in FP_Interface 0.0.6, F12.md): fixed
expectations from User's configuration (data types, array bounds, UDT reference, 79 standard objects).
F15 (User: local tag LocalInt in FP_Interface 0.0.7, F15.md): local tag in the Default tag table of the hidden device,
no limits stored, not part of the interface, 79 standard objects.
F20 (User: FP_Elements 0.0.2 with suspendable, background, layer Hilfsebene not visible in runtime, F20.md) and F30
(User: C_Faceplate1 in FP_Controls typed with FP_Basic, dependency of the versions, F30.md): fixed expectations, F30
also against F30_nested_truth/typesList.json. F33 (User: FP_Controls 0.0.2 with a hidden alarm column, a changed column
width and a second trend on HmiTag_Ext1, F33.md): column lists, trend areas, local tag, no undecoded map value, against
F33_control_columns_truth/typesList.json.
F18c (User: handler of an FP_Interface instance as function list, second instance with script, F18c.md) and F36 (User:
FP_Basic AlwaysDownload, popup script with mediator, F36.md): against Openness itemGet.
F32 (User: second language in FP_Interface 0.0.8, F32.md): texts against TIA's library text export.
Every sample without faceplate types and containers: "faceplates" = []. Also checks that Model.to_dict() carries the
same list under "faceplates".

Usage: python parser/tests/test_faceplates.py [samples dir]     exit code 0 = all checks passed
Result table: tests\\RESULTS_FACEPLATES.md.
"""
import json
import os
import re
import sys
import time
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from openpedata.faceplates import CONTAINER_TYPE, FACEPLATE_TYPE, extract_faceplates  # noqa: E402
from openpedata.model import Model  # noqa: E402

# version 0.0.1 of each type in F03: screen items, of them faceplate containers, objects with Environment -> version,
# scripts, tag shells (HIDDEN-DEVICE.md, F03.md)
EXPECT_F03 = {
    "FP_Basic": (12, 0, 92, 0, []),
    "FP_Controls": (4, 1, 88, 0, []),
    "FP_Elements": (13, 0, 97, 0, []),
    "FP_Interface": (4, 0, 88, 1, ["HmiTag_Ext1"]),
    "FP_Script": (2, 0, 87, 3, ["HmiTag_Ext1"]),
}
STANDARD_OBJECTS = 79
# content size per type (Width/Height expandos 05/06 of ILFaceplateTypeData; FP_Basic = size of its instance) and
# the default layer list (TYPE-PROPERTIES.md)
CONTENT_SIZE = {"FP_Basic": (750, 300), "FP_Controls": (810, 880), "FP_Elements": (800, 400),
                "FP_Interface": (360, 260), "FP_Script": (180, 140)}
LAYERS = ["Layer_%d" % i for i in range(32)]
# FP_Controls keeps only the controls a faceplate type allows (CONTROLS.md, TIA help "Faceplate types" editor)
CONTROLS = ["C_AlarmControl1", "C_DetParam1", "C_Faceplate1", "C_Trend1"]


def font_lists(x, acc):
    """All values of 0x054e FontList entries in the decoded parts of an item."""
    if isinstance(x, dict):
        for k, v in x.items():
            if k.startswith("0x054e"):
                acc.append(v)
            else:
                font_lists(v, acc)
    elif isinstance(x, list):
        for v in x:
            font_lists(v, acc)
    return acc
# per sample: truth folder, types, default versions, interface tags (name, data type, tag), typed instances
# (type -> [(name, screen, ContainedType)]); F05 = F03 + interface tag HmiTag_Ext1 (Bool) in FP_Interface, released,
# + instance of FP_Basic on Main (F05.md)
SAMPLES = {
    "F02_hmi_faceplates_crash": {"truth": "F03_hmi_faceplates_user",
                                 "types": {k: EXPECT_F03[k] for k in ("FP_Basic", "FP_Controls")},
                                 "defaults": ["FP_Basic"], "interface": {}, "instances": {}},
    "F03_hmi_faceplates_user": {"truth": "F03_hmi_faceplates_user", "types": EXPECT_F03,
                                "defaults": ["FP_Basic", "FP_Elements"], "interface": {}, "instances": {},
                                "versionGuids": {"FP_Interface": None}},
    "F05_fp_iface_instance": {"truth": "F05_fp_iface_instance",
                              "types": dict(EXPECT_F03, FP_Interface=(4, 0, 89, 1, [])),
                              "defaults": ["FP_Basic", "FP_Elements", "FP_Interface"],
                              "interface": {"FP_Interface": [("HmiTag_Ext1", "Bool", "HmiTag_Ext1")]},
                              "instances": {"FP_Basic": [("Faceplate container_1", "Main", "V0.0.1\\FP_Basic")]},
                              # release of FP_Interface 0.0.1 gives the version a new GUID (RELEASE.md): it must
                              # differ from the InWork version in F03 (GUIDs are random, so no fixed value)
                              "versionGuids": {"FP_Interface": "F03_hmi_faceplates_user"}},
}
GUID_RE = re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")
_VERSION_GUIDS = {}


def version_guids(root, sample):
    """{(type, version): versionGuid} of another sample (folder or .zap21), parsed once."""
    if sample not in _VERSION_GUIDS:
        p = os.path.join(root, sample)
        m = Model(p if os.path.isdir(p) else p + ".zap21")
        _VERSION_GUIDS[sample] = {(t["name"], v["version"]): v["versionGuid"]
                                  for t in extract_faceplates(m) if t["name"] is not None for v in t["versions"]}
    return _VERSION_GUIDS[sample]


def valid_guid(g):
    return bool(g) and GUID_RE.fullmatch(str(g)) is not None and set(str(g)) != set("0-")


def samples(root):
    out = []
    for n in sorted(os.listdir(root)):
        p = os.path.join(root, n)
        if os.path.isdir(p) and os.path.isfile(os.path.join(p, "System", "PEData.plf")):
            out.append(p)
        elif n.lower().endswith(".zap21"):
            out.append(p)
    return out


def load_json(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def check_truth(types, truth):
    """Compare types / versions with typesList.json (only the types the sample has). -> (fails, compared values)"""
    fails, n = [], 0
    for t in types:
        tt = truth.get(t["name"])
        if tt is None:
            fails.append("type %s not in the ground truth" % t["name"])
            continue
        for a, b, what in ((t["typeGuid"], tt["guid"], "guid"), (t["isObsolete"], tt["doNotUse"], "doNotUse"),
                           (t["folderPath"], tt["folderPath"], "folderPath")):
            n += 1
            if a != b:
                fails.append("%s %s %r, expected %r" % (t["name"], what, a, b))
        got = {v["version"]: v for v in t["versions"]}
        n += 1
        if sorted(got) != sorted(v["version"] for v in tt["versions"]):
            fails.append("%s versions %s, expected %s" % (t["name"], sorted(got), [v["version"] for v in tt["versions"]]))
        for tv in tt["versions"]:
            v = got.get(tv["version"])
            if v is None:
                continue
            for what in ("state", "isDefault", "author"):
                n += 1
                if v[what] != tv[what]:
                    fails.append("%s %s %s %r, expected %r" % (t["name"], tv["version"], what, v[what], tv[what]))
            # release time: Openness has 100 ns ticks, the store microseconds; not released = None / null
            n += 1
            a, b = v["finalizationTime"], tv["modified"]
            if (a is None) != (b is None) or (a and a.rstrip("Z")[:26] != b.rstrip("Z")[:26]):
                fails.append("%s %s release time %r, expected %r" % (t["name"], tv["version"], a, b))
    return fails, n


def check_expect(name, fps, root):
    fails = []
    ex = SAMPLES[name]
    types = [t for t in fps if t["name"] is not None]
    if sorted(t["name"] for t in types) != sorted(ex["types"]):
        fails.append("types %s, expected %s" % (sorted(t["name"] for t in types), sorted(ex["types"])))
    for t in types:
        if t["name"] not in ex["types"]:
            continue
        n_items, n_cont, n_env, n_scripts, shells = ex["types"][t["name"]]
        for v in t["versions"]:
            items = (v["content"] or {}).get("items", [])
            got = (len(items), sum(i["type"] == "HmiFaceplateContainer" for i in items), v["environmentObjects"],
                   v["scripts"], v["unresolvedTags"])
            if got != (n_items, n_cont, n_env, n_scripts, shells):
                fails.append("%s %s items/containers/environment/scripts/shells %s, expected %s" % (
                    t["name"], v["version"], got, (n_items, n_cont, n_env, n_scripts, shells)))
            if v["standardObjects"] != STANDARD_OBJECTS:
                fails.append("%s %s standard objects %s, expected %d" % (t["name"], v["version"], v["standardObjects"],
                                                                       STANDARD_OBJECTS))
            c = v["content"] or {}
            got_c = ((c.get("width"), c.get("height")), c.get("layers"), c.get("activeLayer"),
                     (c.get("properties") or {}).get("_TypeId"))
            want_c = (CONTENT_SIZE[t["name"]], LAYERS, "Layer_0", 900)
            if got_c != want_c:
                fails.append("%s %s size/layers/active layer/_TypeId %s, expected %s" % (
                    t["name"], v["version"], str(got_c)[:200], str(want_c)[:200]))
            if t["name"] == "FP_Controls":
                got_ctl = [i["name"] for i in c.get("items", [])]
                if got_ctl != CONTROLS:
                    fails.append("FP_Controls items %s, expected %s" % (got_ctl, CONTROLS))
                for i in c.get("items", []):
                    cap = (i.get("parts") or {}).get("0x0051 Caption") or {}
                    if cap.get("_TypeId") != 40009 or not str(cap.get("0x021c Font", {}).get("0x054e FontList")).startswith("FontList_C"):
                        fails.append("FP_Controls %s caption part %s" % (i["name"], str(cap)[:120]))
            for i in c.get("items", []):
                bad = [x for x in font_lists(i.get("parts"), []) if not str(x).startswith("FontList_C")]
                if bad:
                    fails.append("%s %s font list references not resolved: %s" % (t["name"], i["name"], bad[:3]))
            itf = c.get("interface") or {}
            want_itf = ex["interface"].get(t["name"], [])
            got_itf = [(x["name"], x["dataType"], x["tag"]) for x in itf.get("tags", [])]
            if got_itf != want_itf or bool(itf.get("present")) != bool(want_itf):
                fails.append("%s %s interface %s (block %s), expected %s" % (t["name"], v["version"], got_itf,
                                                                           itf.get("present"), want_itf))
        got_inst = [(i["name"], i["screen"], i["containedType"]) for i in t["instances"]]
        if got_inst != ex["instances"].get(t["name"], []):
            fails.append("%s instances %s, expected %s" % (t["name"], got_inst, ex["instances"].get(t["name"], [])))
        if t["name"] in ex.get("versionGuids", {}):  # one version with a valid GUID, new against a reference sample
            got = [v["versionGuid"] for v in t["versions"]]
            ref = ex["versionGuids"][t["name"]]
            old = ref and version_guids(root, ref).get((t["name"], "0.0.1"))
            if len(got) != 1 or not valid_guid(got[0]) or (ref and (not valid_guid(old) or got[0] == old)):
                fails.append("%s version GUID %s, expected one valid GUID%s" % (
                    t["name"], got, " other than %s in %s" % (old, ref) if ref else ""))
        default = [v["version"] for v in t["versions"] if v["isDefault"]]
        want = ["0.0.1"] if t["name"] in ex["defaults"] else []
        if default != want or t["defaultVersion"] != (want[0] if want else None):
            fails.append("%s default %s / %s, expected %s" % (t["name"], default, t["defaultVersion"], want))
    untyped = [i["name"] for t in fps if t["name"] is None for i in t["instances"]]
    if untyped != ["C_Faceplate1"]:
        fails.append("instances without type %s, expected ['C_Faceplate1']" % untyped)
    return fails


def check_openness_instances(fps, tdir):
    """F05: ContainedType of the instance on Main (itemGet) and the interface of FP_Interface as an instance shows it
    (partGet Interface). -> (fails, compared values)"""
    fails, n = [], 0
    p = os.path.join(tdir, "itemGet_Main_Faceplate_container_1.json")
    if os.path.isfile(p):
        tr = load_json(p)
        got = [i for t in fps for i in t["instances"] if i["name"] == tr["Name"]]
        n += 6
        want = (tr["screenName"], tr["ContainedType"], tr["Left"], tr["Top"], tr["Width"], tr["Height"])
        got = [(i["screen"], i["containedType"], i["left"], i["top"], i["width"], i["height"]) for i in got]
        if got != [want]:
            fails.append("instance %s: %s, expected %s" % (tr["Name"], got, want))
    p = os.path.join(tdir, "partGet_FP_Interface_instance_Interface.json")
    if os.path.isfile(p):
        want = [e["name"] for e in load_json(p)["Interface"]["elements"]]
        got = [x["name"] for t in fps if t["name"] == "FP_Interface" for v in t["versions"] if v["isDefault"]
               for x in v["content"]["interface"]["tags"]]
        n += 1
        if got != want:
            fails.append("FP_Interface interface %s, expected %s (Openness)" % (got, want))
    return fails, n


SCRIPT_TRUTH = os.path.join("F01_hmi_allobjects_truth", "HMI_1")


def check_scripts(fps, root):
    """Dynamizations (property, type, tag or script code) and events / property events (script code) of the items of
    FP_Interface and FP_Script against the truth of the source screens. -> (fails, compared values)"""
    fails, n = [], 0
    for t in fps:
        if t["name"] not in ("FP_Interface", "FP_Script"):
            continue
        tr = load_json(os.path.join(root, SCRIPT_TRUTH, "screen_%s.full.json" % t["name"]))["objects"][0]["items"]
        for v in t["versions"]:
            items = {i["name"]: i for i in v["content"]["items"]}
            for ti in tr:
                it = items.get(ti["name"])
                if it is None:
                    continue
                want = sorted((d["PropertyName"], d["DynamizationType"], d.get("Tag") or d.get("ScriptCode"))
                              for d in ti.get("dynamizations", []))
                got = sorted((d.get("property"), d.get("type"), d.get("tag") or d.get("code"))
                             for d in it["dynamizations"] if d.get("type"))
                want_ev = sorted(["On" + e["eventType"], e["ScriptCode"]] for e in ti.get("events", []))
                want_ev += sorted([e["propertyName"] + "OnPropertyChanged", e["ScriptCode"]]
                                  for e in ti.get("propertyEvents", []))
                got_ev = sorted([e.get("name"), e.get("code")] for e in it["events"])
                n += len(want) + len(want_ev)
                if got != want:
                    fails.append("%s %s dynamizations %s, expected %s" % (t["name"], ti["name"], got, want))
                if got_ev != sorted(want_ev):
                    fails.append("%s %s events %s, expected %s" % (t["name"], ti["name"], got_ev, want_ev))
    return fails, n


F40 = "F40_siemens_demo_bestpractices"


def _text(v):
    """Plain text of an Openness ProjectText XML value, or the value itself."""
    if isinstance(v, str) and v.startswith("<?xml"):
        return re.sub(r"<[^>]+>", "", v.split("?>", 1)[1])
    return v


def check_f40(fps, tdir):
    """F40: instances against item_*.json, dynamizations against interface_dynamizations.json. -> (fails, compared)"""
    fails, n = [], 0
    inst = {(i["screen"], i["name"]): i for t in fps for i in t["instances"]}
    # Suspendable (expando 0x0263): the two demo types named after it, every other type without the expando
    for t in fps:
        want = {"Faceplate_Suspendable": True, "Faceplate_Not_Suspendable": False}.get(t["name"])
        for v in t["versions"]:
            n += 1
            if v["content"]["suspendable"] != want:
                fails.append("%s %s suspendable %s, expected %s" % (t["name"], v["version"], v["content"]["suspendable"],
                                                                  want))
    for fn in sorted(os.listdir(tdir)):
        if not fn.startswith("item_"):
            continue
        tr = load_json(os.path.join(tdir, fn))
        ig = tr["itemGet"]
        i = inst.get((ig["screenName"], ig["Name"]))
        if i is None:
            fails.append("instance %s/%s missing" % (ig["screenName"], ig["Name"]))
            continue
        got = (i["containedType"], i["left"], i["top"], i["width"], i["height"])
        want = (ig["ContainedType"], ig["Left"], ig["Top"], ig["Width"], ig["Height"])
        n += 5
        if got != want:
            fails.append("%s/%s %s, expected %s" % (ig["screenName"], ig["Name"], got, want))
        vals = {e["name"]: e for e in i["interface"]}
        for e in tr["elements"]:
            name, val = e["PropertyName"], _text(e.get("Value"))
            dyn = bool(e.get("compositions", {}).get("Dynamizations"))
            g = vals.get(name, {})
            gv = g.get("value")
            if isinstance(gv, dict):  # multilingual text: editing language en-US, else the default text
                gv = gv.get("en-US", gv.get(""))
            n += 2
            if (gv if gv not in ("",) else None) != (val if val not in ("",) else None):
                fails.append("%s/%s %s value %r, expected %r" % (ig["screenName"], ig["Name"], name, gv, val))
            if bool(g.get("dynamization")) != dyn:
                fails.append("%s/%s %s dynamized %s, expected %s" % (ig["screenName"], ig["Name"], name,
                                                                   bool(g.get("dynamization")), dyn))
    for x in load_json(os.path.join(tdir, "interface_dynamizations.json"))["items"]:
        r = x["result"]
        if "DynamizationType" not in r:
            continue
        i = inst.get((x["screen"], x["container"]), {})
        g = next((e.get("dynamization") or {} for e in i.get("interface", []) if e["name"] == r["PropertyName"]), {})
        want = {"Tag": ("tag", r.get("Tag")), "TagParameter": ("dynamicTagName", r.get("DynamicTagName")),
                "ResourceList": ("resourceList", r.get("ResourceList"))}[r["DynamizationType"]]
        n += 2
        if g.get("type") != r["DynamizationType"] or g.get(want[0]) != want[1]:
            fails.append("%s/%s %s dynamization %s, expected %s %s" % (x["screen"], x["container"], r["PropertyName"],
                                                                     g, r["DynamizationType"], want[1]))
        if r["DynamizationType"] == "ResourceList":
            n += 1
            if g.get("tag") != r["Tag"]:
                fails.append("%s %s resource list tag %s, expected %s" % (x["container"], r["PropertyName"],
                                                                        g.get("tag"), r["Tag"]))
    return fails, n


F16 = "F16_iface_props"
# F16: data types as chosen in the property interface editor (P_ULInt was LInt in 0.0.2, ULInt in 0.0.3)
F16_TYPES = {"P_Bool": "Bool", "P_Int": "LInt", "P_Real": "LReal", "P_ULInt": "ULInt", "P_Color": "Color",
             "P_Graphic": "Graphic", "P_Text": "Multilingual text", "P_String": "WString", "P_Auth": "Authorization",
             "P_List": "Resource list", "P_ParamSet": "Parameter set type"}


def check_f16(fps, tdir):
    fails, n = [], 0
    t = next((x for x in fps if x["name"] == "FP_Interface"), None)
    tt = load_json(os.path.join(tdir, "typesList_FP_Interface.json"))["items"][0]
    if t is None:
        return ["FP_Interface missing"], 0
    got = [(v["version"], v["state"], v["isDefault"], v["author"]) for v in t["versions"]]
    want = [(v["version"], v["state"], v["isDefault"], v["author"]) for v in tt["versions"]]
    n += 4 * len(want)
    if got != want:
        fails.append("FP_Interface versions %s, expected %s" % (got, want))
    # every version is a full copy: own content, own hidden device (89 objects), own version GUID
    if len({v["versionGuid"] for v in t["versions"]}) != len(t["versions"]) or \
            any(v["environmentObjects"] != 89 or v["standardObjects"] != STANDARD_OBJECTS for v in t["versions"]):
        fails.append("FP_Interface version copies %s" % [(v["versionGuid"], v["environmentObjects"], v["standardObjects"])
                                                        for v in t["versions"]])
    byv = {v["version"]: {x["name"]: x for x in v["content"]["interface"]["properties"]} for v in t["versions"]}
    if [x.get("dataType") for x in byv.get("0.0.2", {}).values()] != \
            [F16_TYPES[k] if k != "P_ULInt" else "LInt" for k in F16_TYPES] or \
            [x.get("dataType") for x in byv.get("0.0.3", {}).values()] != list(F16_TYPES.values()):
        fails.append("property data types 0.0.2 %s / 0.0.3 %s" % ([x.get("dataType") for x in byv.get("0.0.2", {}).values()],
                                                                 [x.get("dataType") for x in byv.get("0.0.3", {}).values()]))
    n += 22
    inst = load_json(os.path.join(tdir, "instance_FP_Iface_3.json"))
    props = byv.get("0.0.3", {})
    want_names = [e["PropertyName"] for e in inst["elements"] if e["PropertyName"] in F16_TYPES]
    n += 1
    if list(props) != want_names:
        fails.append("property names %s, expected %s" % (list(props), want_names))
    for e in inst["elements"]:
        p = props.get(e["PropertyName"])
        if p is None:
            continue
        a, b = p["default"], _text(e.get("Value"))
        n += 1
        if (None if a in ("", None) else a) != (None if b in ("", None) else b):
            fails.append("%s default %r, expected %r" % (e["PropertyName"], a, b))
    return fails, n


F18 = "F18_iface_event"


def check_f18(fps):
    fails, n = [], 0
    t = next((x for x in fps if x["name"] == "FP_Interface"), None)
    if t is None:
        return ["FP_Interface missing"], 0
    vers = [(v["version"], v["isDefault"]) for v in t["versions"]]
    n += 1
    if vers != [("0.0.1", False), ("0.0.2", False), ("0.0.3", False), ("0.0.4", False), ("0.0.5", True)]:
        fails.append("FP_Interface versions %s" % vers)
    for v in t["versions"]:
        ev = [(e["name"], [(q["name"], q["dataType"], q["dataTypeStored"]) for q in e["parameters"]])
              for e in v["content"]["interface"]["events"]]
        want = [("EvTest", [("Nr", "Int", False), ("Flag", "Bool", True)])] if v["version"] in ("0.0.4", "0.0.5") else []
        n += 1
        if ev != want:
            fails.append("FP_Interface %s events %s, expected %s" % (v["version"], ev, want))
        if v["version"] in ("0.0.4", "0.0.5"):
            code = [e.get("code") or "" for i in v["content"]["items"] if i["name"] == "Button_1" for e in i["events"]
                    if e.get("name") == "OnTapped"]
            n += 1
            if len(code) != 1 or 'Faceplate.RaiseEvent("EvTest"' not in code[0]:
                fails.append("FP_Interface %s Button_1 OnTapped %s" % (v["version"], code))
    return fails, n


F18B = "F18b_event_handler"


def check_f18b(fps, tdir):
    fails, n = [], 0
    tr = load_json(os.path.join(tdir, "instance_Main_Faceplate_container_2.json"))
    ig = tr["itemGet"]
    i = next((x for t in fps for x in t["instances"] if (x["screen"], x["name"]) == ("Main", ig["Name"])), None)
    if i is None:
        return ["instance %s missing" % ig["Name"]], 0
    n += 5
    got = (i["containedType"], i["left"], i["top"], i["width"], i["height"])
    want = (ig["ContainedType"], ig["Left"], ig["Top"], ig["Width"], ig["Height"])
    if got != want:
        fails.append("instance %s, expected %s" % (got, want))
    vals = {e["name"]: e for e in i["interface"]}
    for e in tr["elements"]:
        name, val = e["PropertyName"], _text(e.get("Value"))
        gv = vals.get(name, {}).get("value")
        if isinstance(gv, dict):
            gv = gv.get("en-US", gv.get(""))
        n += 1
        if (None if gv in ("", None) else gv) != (None if val in ("", None) else val):
            fails.append("%s value %r, expected %r" % (name, gv, val))
    want_ev = [h["Script"]["ScriptCode"] for h in tr["eventHandlers"]]
    got_ev = [e.get("code") for e in i.get("events", [])]
    n += len(want_ev)
    if got_ev != want_ev or [e.get("name") for e in i.get("events", [])] != ["OnEvTest"]:
        fails.append("event handlers %s, expected %s" % ([(e.get("name"), e.get("code")) for e in i.get("events", [])],
                                                         want_ev))
    return fails, n


F18C = "F18c_event_funclist"
# F18c.md: handler of Faceplate container_2 is a function list (SetTagValue, ChangeScreen), container_3 keeps a script
F18C_FUNCTIONS = {"Faceplate container_2": [("SetTagValue", ['"HmiTag_Ext1"', "1"]), ("ChangeScreen", ['"FP_Basic"', '"."'])]}


def check_f18c(fps, tdir):
    """Event handlers of both FP_Interface instances on Main against Openness itemGet (code, Async, position) and the
    handler kind: function list (HmiFunctionListEmbeddedData, Openness global area "// [FunctionListModule]") or
    script."""
    fails, n = [], 0
    for name in ("Faceplate container_2", "Faceplate container_3"):
        tr = load_json(os.path.join(tdir, "instance_Main_%s.json" % name.replace(" ", "_")))
        i = next((x for t in fps for x in t["instances"] if (x["screen"], x["name"]) == ("Main", name)), None)
        if i is None:
            fails.append("instance %s missing" % name)
            continue
        n += 5
        got = (i["containedType"], i["left"], i["top"], i["width"], i["height"])
        want = (tr["ContainedType"], tr["Left"], tr["Top"], tr["Width"], tr["Height"])
        if got != want:
            fails.append("%s %s, expected %s" % (name, got, want))
        ev = i.get("events") or []
        want_ev = [(h["ScriptCode"], h["Async"], "functionList" if "[FunctionListModule]" in h["GlobalDefinitionAreaScriptCode"]
                    else "script") for h in tr["events"]]
        got_ev = [(e.get("code"), e.get("async"), e.get("handler")) for e in ev]
        n += 3 * len(want_ev)
        if got_ev != want_ev or [e.get("name") for e in ev] != ["OnEvTest"]:
            fails.append("%s handlers %s, expected %s" % (name, got_ev, want_ev))
        n += 1
        got_fn = [(f["function"], f["arguments"]) for e in ev for f in e.get("functions") or []]
        if got_fn != F18C_FUNCTIONS.get(name, []):
            fails.append("%s functions %s" % (name, got_fn))
    return fails, n


F36 = "F36_popup_alwaysdownload"


def check_f36(m, fps, tdir):
    """F36.md: AlwaysDownload True only at FP_Basic; popup script OnTapped of Button_1 on Main references FP_Basic 0.0.1
    through a mediator; its code with the placeholder replaced by the Openness name equals the Openness ScriptCode."""
    from openpedata.hmi import Hmi
    fails, n = [], 0
    ad = {t["name"]: t["alwaysDownload"] for t in fps if t["name"]}
    n += len(ad)
    if [k for k, v in ad.items() if v] != ["FP_Basic"]:
        fails.append("alwaysDownload %s" % ad)
    t = next(x for x in fps if x["name"] == "FP_Basic")
    refs = [r for v in t["versions"] for r in v.get("referencedBy") or []]
    n += 4
    got = [(r["screen"], r["item"], r["script"], r["opennessName"]) for r in refs]
    if got != [("Main", "Button_1", "OnTapped", "FP_Basic_V_0_0_1")]:
        fails.append("mediators %s" % got)
    tr = load_json(os.path.join(tdir, "item_Main_Button_1.json"))
    h = Hmi(m)
    scr = next(k for k in m.p.objects if m.p.name(k) == "Main" and (m.p.type_name(k) or "").endswith("ILScreenData"))
    item = next(i for i in h.items(scr) if i["name"] == "Button_1")
    code = item["events"][0]["code"] if item["events"] else ""
    if refs and refs[0]["placeholder"]:
        code = code.replace('"%s"' % refs[0]["placeholder"], '"%s"' % refs[0]["opennessName"])
    n += 2
    if code != tr["events"][0]["ScriptCode"] or item["events"][0].get("name") != "On" + tr["events"][0]["eventType"]:
        fails.append("popup script differs from Openness")
    return fails, n


F32 = "F32_multilingual"


def check_f32(fps, tdir):
    """F32.md: FP_Interface 0.0.8 (default) with I_Text1 in en-US and de-DE; texts of the version against the library
    text export of TIA (F32_texts.xlsx, excerpt in library_texts.json: ViewPath ending in <item> and Text, one column per
    language) and the project languages of the version (reference de-DE, editing en-US, used en-US/de-DE)."""
    fails, n = [], 0
    tr = load_json(os.path.join(tdir, "library_texts.json"))
    t = next((x for x in fps if x["name"] == "FP_Interface"), None)
    v = next((x for x in (t or {}).get("versions", []) if x["version"] == "0.0.8"), None)
    if v is None or not v["isDefault"]:
        return ["FP_Interface 0.0.8 (default) missing"], 0
    items = {i["name"]: i for i in v["content"]["items"]}
    for row in tr["userTexts"]:
        name = row["ViewPath"].split("\\")[-2]
        got = (items.get(name) or {}).get("text") or {}
        for lang in tr["languages"]:
            n += 1
            if got.get(lang) != row[lang]:
                fails.append("%s %s %r, expected %r" % (name, lang, got.get(lang), row[lang]))
    n += 3
    c = v["cultures"]
    if (c["reference"], c["editing"], c["used"]) != (tr["referenceLanguage"], "en-US", sorted(tr["languages"])):
        fails.append("cultures %s" % c)
    n += 1
    if [x["containedType"] for x in t["instances"]] != ["V0.0.8\\FP_Interface"] * 2:
        fails.append("instances %s" % [x["containedType"] for x in t["instances"]])
    return fails, n


F12 = "F12_iface_tags"
F12_TAGS = [("HmiTag_Ext1", "Bool", 1), ("T_Real", "Real", 10), ("T_DateTime", "DateTime", 13), ("T_WString", "Bool", 1),
            ("T_Array", "Array[0..3] of Int", 67), ("T_Udt", "Struct", 30)]


def check_f12(fps):
    fails, n = [], 0
    t = next((x for x in fps if x["name"] == "FP_Interface"), None)
    v = next((x for x in (t or {}).get("versions", []) if x["version"] == "0.0.6"), None)
    if v is None:
        return ["FP_Interface 0.0.6 missing"], 0
    tags = v["content"]["interface"]["tags"]
    got = [(x["name"], x["dataType"], x["dataTypeCode"]) for x in tags]
    n += 3 * len(F12_TAGS)
    if got != F12_TAGS:
        fails.append("interface tags %s, expected %s" % (got, F12_TAGS))
    udt = [(x.get("structureType"), x.get("udt"), x.get("udtVersion")) for x in tags if x["name"] == "T_Udt"]
    n += 2
    if udt != [("HMIUDT", "User_data_type_1", "0.0.1")]:
        fails.append("T_Udt %s" % udt)
    if (v["environmentObjects"], v["standardObjects"]) != (102, STANDARD_OBJECTS):
        fails.append("0.0.6 objects %s / standard %s" % (v["environmentObjects"], v["standardObjects"]))
    return fails, n


F15 = "F15_local_tag"


def check_f15(fps):
    fails, n = [], 0
    t = next((x for x in fps if x["name"] == "FP_Interface"), None)
    vers = {v["version"]: v for v in (t or {}).get("versions", [])}
    v = vers.get("0.0.7")
    if v is None or not v["isDefault"]:
        return ["FP_Interface 0.0.7 (default) missing"], 0
    got = [(x["name"], x["dataType"], x["table"], x["limits"]) for x in v["localTags"]]
    n += 4
    if got != [("LocalInt", "Int", "Default tag table", {})]:
        fails.append("local tags %s" % got)
    n += 2
    if "LocalInt" in [x["name"] for x in v["content"]["interface"]["tags"]] or v["standardObjects"] != STANDARD_OBJECTS:
        fails.append("LocalInt in interface or standard objects %s" % v["standardObjects"])
    if any(x["localTags"] for k, x in vers.items() if k != "0.0.7"):
        fails.append("local tags in other versions")
    return fails, n


F20 = "F20_type_props"
F30 = "F30_nested"
F33 = "F33_control_columns"
# entries of the selection lists (F01_hmi_allobjects_truth/HMI_1/screen_FP_Elements.full.json SelectionItems)
SELECTION_ITEMS = {"E_CheckBox1": 4, "E_ListBox1": 3, "E_Radio1": 3}
# version dependencies before F30 (FP_Interface 0.0.6 / 0.0.7 use the HMI UDT of the interface tag T_Udt, F12.md)
OTHER_DEPENDENCIES = {("FP_Interface", "0.0.6"): ["User_data_type_1 0.0.1"],
                      ("FP_Interface", "0.0.7"): ["User_data_type_1 0.0.1"]}


def check_f20(fps):
    """F20.md: FP_Elements 0.0.2 (copy of 0.0.1) with suspendable, yellow background, layer 1 renamed Hilfsebene and
    not visible in runtime, E_Button1 on it; 0.0.1 keeps all defaults."""
    fails, n = [], 0
    t = next((x for x in fps if x["name"] == "FP_Elements"), None)
    vers = {v["version"]: v for v in (t or {}).get("versions", [])}
    if set(vers) != {"0.0.1", "0.0.2"} or not vers["0.0.2"]["isDefault"]:
        return ["FP_Elements versions %s" % sorted(vers)], 0
    want = {"0.0.1": (None, None, "Layer_1", [], 0),
            "0.0.2": (True, "0xFFFFFF99", "Hilfsebene", ["Hilfsebene"], 1)}
    for k, (susp, back, layer1, hidden, btn) in want.items():
        c = vers[k]["content"]
        b = next(i for i in c["items"] if i["name"] == "E_Button1")
        got = (c["suspendable"], c["backColor"], c["layers"][1], c["hiddenLayers"], b["layerIndex"])
        n += 5
        if got != (susp, back, layer1, hidden, btn):
            fails.append("FP_Elements %s suspendable/background/layer 1/hidden/E_Button1 layer %s" % (k, got))
        n += 3
        if (len(c["layers"]), c["activeLayer"], vers[k]["standardObjects"]) != (32, "Layer_0", STANDARD_OBJECTS):
            fails.append("FP_Elements %s layers %d, active %s, standard %s" % (
                k, len(c["layers"]), c["activeLayer"], vers[k]["standardObjects"]))
    n += 1
    if len(vers["0.0.2"]["content"]["items"]) != 13:
        fails.append("FP_Elements 0.0.2 items %d" % len(vers["0.0.2"]["content"]["items"]))
    # selection lists 0x0934 (CoreMapArray of pe blobs, F33.md): entry count as in the F01 truth (SelectionItems)
    got = {i["name"]: len(i["parts"].get("0x0934 SelectionItems") or []) for i in vers["0.0.2"]["content"]["items"]
           if i["name"] in SELECTION_ITEMS}
    n += len(SELECTION_ITEMS)
    if got != SELECTION_ITEMS:
        fails.append("FP_Elements 0.0.2 selection items %s, expected %s" % (got, SELECTION_ITEMS))
    return fails, n


def check_f30(fps, n_controls=1, root=None):
    """F30.md: C_Faceplate1 in FP_Controls 0.0.1 typed with FP_Basic 0.0.1 (released in place), version dependency in
    both directions; the container of the same name on the screen FP_Controls stays without type. F33 keeps 0.0.1 and
    adds 0.0.2 (n_controls 2) with the same dependency and the same GUID of 0.0.1 as F30 (GUIDs are random per
    build, so F30 only gets a validity check)."""
    fails, n = [], 0
    ts = {x["name"]: x for x in fps}
    ctl = (ts.get("FP_Controls") or {}).get("versions") or []
    bas = (ts.get("FP_Basic") or {}).get("versions") or []
    if len(ctl) != n_controls or len(bas) != 1:
        return ["FP_Controls / FP_Basic versions %d / %d" % (len(ctl), len(bas))], 0
    c, b = ctl[0], bas[0]
    got = (c["version"], c["state"], c["isDefault"], c["versionGuid"])
    n += 4
    want_guid = version_guids(root, F30).get(("FP_Controls", "0.0.1")) if n_controls == 2 else c["versionGuid"]
    if got != ("0.0.1", "Committed", n_controls == 1, want_guid) or not valid_guid(c["versionGuid"]):
        fails.append("FP_Controls 0.0.1 version/state/default/GUID %s" % (got,))
    inner = [(i["name"], i["containedType"]) for i in c["content"]["items"] if i["type"] == "HmiFaceplateContainer"]
    n += 2
    if inner != [("C_Faceplate1", "V0.0.1\\FP_Basic")]:
        fails.append("nested containers %s" % inner)
    n += 1 + len(ctl)
    want_used = ["FP_Controls %s" % v["version"] for v in ctl]
    if [v["dependsOn"] for v in ctl] != [["FP_Basic 0.0.1"]] * len(ctl) or b["usedBy"] != want_used:
        fails.append("dependsOn %s / usedBy %s" % ([v["dependsOn"] for v in ctl], b["usedBy"]))
    n += 2
    other = {(t["name"], v["version"]): v["dependsOn"] for t in fps if t["name"] not in (None, "FP_Controls", "FP_Basic")
             for v in t["versions"] if v["dependsOn"]}
    if other != OTHER_DEPENDENCIES or c["standardObjects"] != STANDARD_OBJECTS:
        fails.append("other dependencies %s or standard objects %s" % (other, c["standardObjects"]))
    untyped = [i["name"] for t in fps if t["name"] is None for i in t["instances"]]
    n += 1
    if untyped != ["C_Faceplate1"]:
        fails.append("instances without type %s" % untyped)
    return fails, n


def _columns(item, part):
    # 0x0007 is named Visible since hmi.PROPERTY_NAMES knows it (Z1.5)
    return [(x.get("0x0003"), x.get("0x0007 Visible", x.get("0x0007")), x.get("0x0005 Width"))
            for x in (item["parts"].get(part) or {}).get("0x0011", [])]


def check_f33(fps, root):
    """F33.md: FP_Controls 0.0.2 (copy of 0.0.1): alarm control column list 0x3815 with Name width 100 -> 123 and
    Alarm class hidden, everything else as in 0.0.1; trend control 0x3839 (trend areas, CoreMapArray of pe blobs) with
    a second trend whose data source 0x0440 is HmiTag_Ext1, created as local tag Int of the version; no map value is
    left undecoded in any faceplate item."""
    fails, n = check_f30(fps, n_controls=2, root=root)
    t = next(x for x in fps if x["name"] == "FP_Controls")
    v1, v2 = t["versions"]
    n += 2
    if (v2["version"], v2["isDefault"]) != ("0.0.2", True) or v2["versionGuid"] == v1["versionGuid"]:
        fails.append("FP_Controls 0.0.2 %s default %s" % (v2["version"], v2["isDefault"]))
    a1, a2 = [next(i for i in v["content"]["items"] if i["name"] == "C_AlarmControl1") for v in (v1, v2)]
    c1, c2 = _columns(a1, "0x3815"), _columns(a2, "0x3815")
    changed = [(x, y) for x, y in zip(c1, c2) if x != y]
    n += 3
    if len(c1) != 49 or changed != [(("Name", False, 100), ("Name", False, 123)),
                                    (("Alarm class", True, 120), ("Alarm class", False, 120))]:
        fails.append("alarm columns 0x3815: %d, changed %s" % (len(c1), changed))
    n += 1
    if _columns(a1, "0x3832") != _columns(a2, "0x3832") or len(_columns(a2, "0x3832")) != 56:
        fails.append("alarm column list 0x3832 changed or not 56 columns")
    trends = []
    for v in (v1, v2):
        tr = next(i for i in v["content"]["items"] if i["name"] == "C_Trend1")
        areas = tr["parts"].get("0x3839") or []
        trends.append([(a.get("0x0003"), [(x.get("0x0440") or {}).get("0x0b1f") for x in a.get("0x2150", [])],
                        [x.get("0x0003") for x in a.get("0x3855", [])], [x.get("0x0003") for x in a.get("0x3858", [])])
                       for a in areas])
    n += 2
    want = [("TrendArea_1", [None], ["Left value axis_1"], ["Time axis - bottom_1"])]
    if trends != [want, [("TrendArea_1", [None, "HmiTag_Ext1"], ["Left value axis_1"], ["Time axis - bottom_1"])]]:
        fails.append("trend areas %s" % trends)
    n += 2
    if [(x["name"], x["dataType"], x["table"]) for x in v2["localTags"]] != [("HmiTag_Ext1", "Int", "Default tag table")] \
            or v1["localTags"]:
        fails.append("local tags %s / %s" % (v1["localTags"], v2["localTags"]))
    n += 1
    undecoded = [(x["name"], v["version"], i["name"]) for x in fps if x["name"] for v in x["versions"]
                 for i in (v["content"] or {}).get("items", []) if "'code 0x" in str(i["parts"])]
    if undecoded:
        fails.append("undecoded map values %s" % undecoded[:5])
    f, n2 = check_f20(fps)
    return fails + f, n + n2


def has_faceplate_objects(m):
    p = m.p
    return any((p.type_name(k) or "").rsplit(".", 1)[-1] in (FACEPLATE_TYPE, CONTAINER_TYPE) for k in p.objects)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "samples")
    rows, all_ok, details, compared = [], True, [], 0
    for s in samples(root):
        name = os.path.basename(s)
        base = name[:-6] if name.lower().endswith(".zap21") else name
        t0 = time.time()
        try:
            m = Model(s)
            fps = extract_faceplates(m)
            json.dumps(fps, default=str)
            fails, n = [], 0
            # F16/F18: only the fixed archive copies; User keeps working in the project folders of the same name
            if name == F33 + ".zap21":
                f, n = check_f33(fps, root)
                fails += f
                truth = {t["name"]: t for t in load_json(os.path.join(root, F33 + "_truth", "typesList.json"))["items"]}
                f, n2 = check_truth([t for t in fps if t["name"] is not None], truth)
                fails += f
                n += n2
            elif name == F30 + ".zap21":
                f, n = check_f30(fps)
                fails += f
                f, n2 = check_f20(fps)  # F30 builds on F20 (FP_Elements 0.0.2 unchanged)
                fails += f
                n += n2
                truth = {t["name"]: t for t in load_json(os.path.join(root, F30 + "_truth", "typesList.json"))["items"]}
                f, n2 = check_truth([t for t in fps if t["name"] is not None], truth)
                fails += f
                n += n2
            elif name == F20 + ".zap21":
                f, n = check_f20(fps)
                fails += f
            elif name == F15 + ".zap21":
                f, n = check_f15(fps)
                fails += f
            elif name == F12 + ".zap21":
                f, n = check_f12(fps)
                fails += f
            elif name == F32 + ".zap21":
                f, n = check_f32(fps, os.path.join(root, F32 + "_truth"))
                fails += f
            elif name == F36 + ".zap21":
                f, n = check_f36(m, fps, os.path.join(root, F36 + "_truth"))
                fails += f
            elif name == F18C + ".zap21":
                f, n = check_f18c(fps, os.path.join(root, F18C + "_truth"))
                fails += f
            elif name == F18B + ".zap21":
                f, n = check_f18b(fps, os.path.join(root, F18B + "_truth"))
                fails += f
            elif name == F18 + ".zap21":
                f, n = check_f18(fps)
                fails += f
            elif name == F16 + ".zap21":
                f, n = check_f16(fps, os.path.join(root, F16 + "_truth"))
                fails += f
            elif base == F40 and not os.path.isdir(os.path.join(root, F40 + "_truth")):
                continue  # Siemens demo project: sample and truth only local, not in the repo
            elif base == F40:
                tdir = os.path.join(root, F40 + "_truth")
                truth = {t["name"]: t for t in load_json(os.path.join(tdir, "typesList.json"))["items"]}
                f, n = check_truth([t for t in fps if t["name"] is not None], truth)
                fails += f
                f, n2 = check_f40(fps, tdir)
                fails += f
                n += n2
            elif base in SAMPLES:
                tdir = os.path.join(root, SAMPLES[base]["truth"] + "_truth")
                truth = {t["name"]: t for t in load_json(os.path.join(tdir, "typesList.json"))["items"]}
                f, n = check_truth([t for t in fps if t["name"] is not None], truth)
                fails += f + check_expect(base, fps, root)
                f, n2 = check_openness_instances(fps, tdir)
                fails += f
                n += n2
                f, n2 = check_scripts(fps, root)
                fails += f
                n += n2
                md = m.to_dict()
                if json.dumps(md.get("faceplates"), default=str, sort_keys=True) != json.dumps(fps, default=str, sort_keys=True):
                    fails.append("Model.to_dict()['faceplates'] differs from extract_faceplates")
            elif not has_faceplate_objects(m) and fps != []:
                fails.append("faceplates %r, expected []" % fps[:1])
            compared += n
            types = ", ".join("%s [%s]" % (t["name"], ", ".join("%s %s%s" % (v["version"], v["state"], "*" if v["isDefault"] else "")
                                                               for v in t["versions"])) for t in fps if t["name"] is not None)
            inst = sum(len(t["instances"]) for t in fps)
            rows.append("| %s | %s | %s | %s | %d | %.1f s |" % (
                name, "OK" if not fails else "ERROR: " + "; ".join(fails), n or "–", types or "–", inst, time.time() - t0))
            all_ok &= not fails
        except Exception:
            all_ok = False
            rows.append("| %s | EXCEPTION | | | | |" % name)
            details.append("%s:\n```\n%s```" % (name, traceback.format_exc()))
    lines = ["# Faceplate tests (openpedata.faceplates against Openness ground truth and F3 expectations)", "",
             "**Overall: %s** (%d samples, %d values compared against the Openness truth)" % (
                 "all expectations met, no exception" if all_ok else "ERROR", len(rows), compared), "",
             "| Sample | Result | compared values | Faceplate types [Versions, * = default] | Instances | Time |",
             "| --- | --- | --- | --- | --- | --- |"] + rows + [""] + details
    text = "\n".join(lines) + "\n"
    with open(os.path.join(HERE, "RESULTS_FACEPLATES.md"), "w", encoding="utf-8") as f:
        f.write(text)
    print(text)
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())
