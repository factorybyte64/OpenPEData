"""Recipes of the faceplate samples F01-F36 (samples/MANIFEST.md section "Faceplates F", analysis/F3_faceplate/*.md).

F01 is made with MCP calls only (TiaHmiUnified on HMI_1). Every later sample was originally made by the user by hand in
the TIA user interface; its rebuild pauses the recipe (rhelp.pause) for the steps Openness V21 cannot do (creating a
faceplate type from screen objects, editing the content and the interface of a type, function lists, AlwaysDownload,
snippets) and does the rest with MCP (typeVersionRelease, instances via itemCreate containedType, interface values via
partSet). The exact clicks are in the NOTE_* texts (MANUAL).

Chain (state order): 103 -> F01 -> F02 -> F03 -> F05 -> F16 -> F18 -> F18b -> F12 -> F15 -> F20 -> F30 -> F33 -> F18c
-> F36 -> F32. F02 and F03 are project folders (no archive), all others .zap21.

Truth: the old truth folders were written by separate scripts (truth_f01.py, json indent 1, full results); the generators
below make the same calls. Files of samples whose old truth came from a helper instance in a working copy (F05, F16)
are written after the archive (after_archive); the helper instance is then discarded with the project
(discard_on_close)."""
import json
import os
import re
import shutil

from rhelp import pause

H = "HMI_1"


def hu(op, kind="screen", ok_errors=False, **a):
    """TiaHmiUnified call on HMI_1."""
    def step(ctx):
        args = {k: v for k, v in a.items() if v is not None}
        if "attributes" in args and not isinstance(args["attributes"], str):
            args["attributes"] = json.dumps(args["attributes"])
        ctx.call("TiaHmiUnified", ok_errors=ok_errors, operation=op, objectKind=kind, device=H, **args)
    return step


def item(screen, name, typ, attrs=None, **a):
    return hu("itemCreate", screenName=screen, name=name, itemType=typ, attributes=attrs, **a)


def iset(screen, name, attrs, **a):
    return hu("itemSet", screenName=screen, name=name, attributes=attrs, **a)


def L(left, top, w, h, **k):
    d = {"Left": left, "Top": top, "Width": w, "Height": h}
    d.update(k)
    return d


def C(cx, cy, rx, ry=None, **k):
    d = {"CenterX": cx, "CenterY": cy}
    if ry is None:
        d["Radius"] = rx
    else:
        d.update(RadiusX=rx, RadiusY=ry)
    d.update(k)
    return d


TAG = "HmiTag_Ext1"
FLASH_XML = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                         "samples", "F01_hmi_allobjects_truth", "_scripts", "FP_Flash.import.xml")


def flash_import(ctx):
    """F01 step 7: X_Rect1 with a Visible tag dynamization, then the screen XML extended by a flashing dynamization of
    BackColor (the original input file, samples\\F01_hmi_allobjects_truth\\_scripts\\FP_Flash.import.xml) imported."""
    work = os.path.join(ctx.stage, "work", ctx.name)
    if os.path.isdir(work):
        shutil.rmtree(work)
    os.makedirs(work)
    ctx.call("TiaHmiUnified", operation="exportXml", objectKind="screen", device=H, name="FP_Flash", exportDir=work)
    shutil.copyfile(FLASH_XML, os.path.join(work, "FP_Flash.xml"))
    ctx.call("TiaHmiUnified", operation="importXml", objectKind="screen", device=H, name="FP_Flash", importDir=work)


F01_STEPS = [hu("create", name=s) for s in ("FP_Basic", "FP_Elements", "FP_Controls", "FP_Interface", "FP_Script", "Main",
                                            "FP_Flash")] + [
    # 2. FP_Basic
    item("FP_Basic", "B_Rect1", "HmiRectangle", L(20, 20, 120, 60, BackColor="0xFFFF0000")),
    item("FP_Basic", "B_Line1", "HmiLine", L(160, 20, 120, 60, LineColor="0xFF0000FF", LineWidth=3)),
    item("FP_Basic", "B_Polyline1", "HmiPolyline", L(300, 20, 120, 60, LineColor="0xFF00AA00", LineWidth=2)),
    hu("partCreate", screenName="FP_Basic", name="B_Polyline1", partPath="Points", value="110,5"),
    item("FP_Basic", "B_Polygon1", "HmiPolygon", L(440, 20, 120, 60, BackColor="0xFFFFCC00", BorderColor="0xFF800080")),
    hu("partCreate", screenName="FP_Basic", name="B_Polygon1", partPath="Points", value="100,55"),
    item("FP_Basic", "B_Ellipse1", "HmiEllipse", {"BackColor": "0xFF00CCCC"}),
    iset("FP_Basic", "B_Ellipse1", C(80, 150, 60, 30)),
    item("FP_Basic", "B_EllipseSeg1", "HmiEllipseSegment", C(220, 150, 60, 30, BackColor="0xFFCC6600", StartAngle=30,
                                                            AngleRange=240)),
    item("FP_Basic", "B_EllArc1", "HmiEllipticalArc", C(360, 150, 60, 30, LineColor="0xFF0066CC", LineWidth=4,
                                                       StartAngle=45, AngleRange=200)),
    item("FP_Basic", "B_Circle1", "HmiCircle", C(80, 260, 40, BackColor="0xFF66CC00")),
    item("FP_Basic", "B_CircleSeg1", "HmiCircleSegment", C(200, 260, 40, BackColor="0xFFCC0066", StartAngle=60,
                                                           AngleRange=270)),
    item("FP_Basic", "B_CircArc1", "HmiCircularArc", C(320, 260, 40, LineColor="0xFF990000", LineWidth=5, StartAngle=10,
                                                       AngleRange=180)),
    item("FP_Basic", "B_Text1", "HmiText", L(400, 230, 160, 40, Text="F01 Basic Text", ForeColor="0xFF0000CC"),
         language="en-US"),
    item("FP_Basic", "B_GraphicView1", "HmiGraphicView", L(600, 20, 150, 100, BackColor="0xFFDDEEFF")),
    # 3. FP_Elements (HmiLabel and HmiComboBox were rejected by TIA in the original: left out)
    item("FP_Elements", "E_Button1", "HmiButton", L(20, 20, 140, 50, Text="F01 Button", BackColor="0xFF3366CC",
                                                    ForeColor="0xFFFFFF00"), language="en-US"),
    item("FP_Elements", "E_IOField1", "HmiIOField", L(180, 20, 140, 40, BackColor="0xFFCCFFCC", OutputFormat="{F2}")),
    item("FP_Elements", "E_SymIOField1", "HmiSymbolicIOField", L(340, 20, 140, 40, BackColor="0xFFFFE0C0")),
    item("FP_Elements", "E_TextBox1", "HmiTextBox", L(500, 20, 160, 60, BackColor="0xFFF0F0FF", Text="F01 TextBox"),
         language="en-US"),
    item("FP_Elements", "E_Bar1", "HmiBar", L(20, 100, 60, 180, BackColor="0xFFEEEEEE")),
    item("FP_Elements", "E_Slider1", "HmiSlider", L(100, 100, 80, 180, BackColor="0xFFDDFFDD")),
    item("FP_Elements", "E_Gauge1", "HmiGauge", L(200, 100, 180, 180, BackColor="0xFFFFF0E0")),
    item("FP_Elements", "E_Clock1", "HmiClock", L(400, 100, 160, 160, BackColor="0xFFE0E0FF")),
    item("FP_Elements", "E_Switch1", "HmiToggleSwitch", L(580, 100, 120, 50, BackColor="0xFFFFCCCC")),
    item("FP_Elements", "E_CheckBox1", "HmiCheckBoxGroup", L(20, 300, 160, 100, BackColor="0xFFFFFFCC")),
    hu("partCreate", screenName="FP_Elements", name="E_CheckBox1", partPath="SelectionItems"),
    hu("partSet", screenName="FP_Elements", name="E_CheckBox1", partPath="SelectionItems/#3", attribute="Text",
       value="F01 Check 4", language="en-US"),
    item("FP_Elements", "E_Radio1", "HmiRadioButtonGroup", L(200, 300, 160, 100, BackColor="0xFFCCFFFF")),
    item("FP_Elements", "E_ListBox1", "HmiListBox", L(380, 300, 160, 100, BackColor="0xFFFFDDFF")),
    item("FP_Elements", "E_TouchArea1", "HmiTouchArea", L(560, 300, 140, 100)),
    iset("FP_Elements", "E_TouchArea1", {"BackColor": "0x40FF0000", "TabIndex": 7}),
    item("FP_Elements", "E_AlarmInd1", "HmiAlarmIndicator", L(720, 100, 80, 80)),
    iset("FP_Elements", "E_AlarmInd1", {"AlarmState.BackColor": "0xFFFF9900", "FlashingColor": "0xFF00FF99"}),
    # 4. FP_Controls
    item("FP_Controls", "C_AlarmControl1", "HmiAlarmControl", L(10, 10, 380, 200, BackColor="0xFFE8F0FF")),
    item("FP_Controls", "C_AlarmLine1", "HmiAlarmLineControl", L(10, 220, 380, 40, BackColor="0xFFFFF0F0")),
    item("FP_Controls", "C_Trend1", "HmiTrendControl", L(400, 10, 380, 200, BackColor="0xFFF0FFF0")),
    item("FP_Controls", "C_TrendCompanion1", "HmiTrendCompanion", L(400, 220, 380, 120, BackColor="0xFFF0F0E0")),
    item("FP_Controls", "C_FTrend1", "HmiFunctionTrendControl", L(10, 280, 380, 200, BackColor="0xFFE0F8FF")),
    item("FP_Controls", "C_DetParam1", "HmiDetailedParameterControl", L(400, 350, 190, 120, BackColor="0xFFFFF8E0")),
    item("FP_Controls", "C_Web1", "HmiWebControl", L(600, 350, 190, 120, Url="https://example.invalid/f01")),
    item("FP_Controls", "C_Media1", "HmiMediaControl", L(10, 490, 190, 120, BackColor="0xFF202040")),
    item("FP_Controls", "C_SysDiag1", "HmiSystemDiagnosisControl", L(210, 490, 190, 120, BackColor="0xFFF4F4D0")),
    item("FP_Controls", "C_ProDiagOv1", "HmiProcessDiagnosisOverviewControl", L(410, 490, 190, 120, BackColor="0xFFE0FFE8")),
    item("FP_Controls", "C_GraphOv1", "HmiProcessDiagnosisGraphOverviewControl", L(610, 490, 190, 120,
                                                                                  BackColor="0xFFE8E0FF")),
    item("FP_Controls", "C_CritAnalysis1", "HmiProcessDiagnosisCriteriaAnalysisControl", L(10, 620, 190, 120,
                                                                                          BackColor="0xFFFFE8E8")),
    item("FP_Controls", "C_PlcCode1", "HmiProcessDiagnosisPlcCodeViewerControl", L(210, 620, 190, 120,
                                                                                  BackColor="0xFFE8FFFF")),
    item("FP_Controls", "C_ScreenWin1", "HmiScreenWindow", L(410, 620, 390, 200, Screen="Main")),
    iset("FP_Controls", "C_ScreenWin1", {"CaptionColor": "0xFF884400"}),
    item("FP_Controls", "C_Faceplate1", "HmiFaceplateContainer", L(10, 750, 190, 120)),
    iset("FP_Controls", "C_Faceplate1", {"CaptionColor": "0xFF008844"}),
    # 5. FP_Interface; the original itemBindTag binding=processValue only wrote the static text (MCP-025): itemSet
    item("FP_Interface", "I_Rect1", "HmiRectangle", L(20, 20, 120, 80, BackColor="0xFF808080", BorderColor="0xFF000080",
                                                      BorderWidth=2)),
    item("FP_Interface", "I_Text1", "HmiText", L(160, 20, 200, 40, Text="F01 Interface Text", ForeColor="0xFF006600"),
         language="en-US"),
    item("FP_Interface", "I_IOField1", "HmiIOField", L(20, 120, 140, 40, BackColor="0xFFFFFFE0", OutputFormat="{F0}")),
    item("FP_Interface", "I_Bar1", "HmiBar", L(200, 100, 60, 160, BackColor="0xFFE0E0FF")),
    iset("FP_Interface", "I_IOField1", {"ProcessValue": TAG}),
    hu("itemBindTag", screenName="FP_Interface", name="I_Bar1", tagName=TAG),
    hu("itemBindTag", screenName="FP_Interface", name="I_Rect1", tagName=TAG, propertyName="BackColor",
       colorFalse="0xFF808080", colorTrue="0xFF00AA00"),
    hu("itemBindTag", screenName="FP_Interface", name="I_Rect1", tagName=TAG, propertyName="BorderColor",
       rangeEntries=json.dumps([{"from": 0, "to": 0, "value": "0xFF0000FF"}, {"from": 1, "to": 1, "value": "0xFFFF0000"}])),
    hu("itemBindScript", screenName="FP_Interface", name="I_Text1", propertyName="ForeColor",
       script='let v = Tags("HmiTag_Ext1").Read();\nreturn v ? 0xFFFF0000 : 0xFF006600;',
       globalDefinition="// F01 global definition I_Text1"),
    hu("itemBindScript", screenName="FP_Interface", propertyName="BackColor",
       script='return Tags("HmiTag_Ext1").Read() ? 0xFFFFF0F0 : 0xFFF0F0FF;'),
    # 6. FP_Script (S_Switch1 was created and deleted again in the original)
    item("FP_Script", "S_Button1", "HmiButton", L(20, 20, 160, 50, Text="F01 Set Tag", BackColor="0xFF006699"),
         language="en-US"),
    hu("itemEventCreate", screenName="FP_Script", name="S_Button1", eventType="Tapped",
       script='// F01 S_Button1 Tapped\nTags("HmiTag_Ext1").Write(true);'),
    hu("itemEventCreate", screenName="FP_Script", name="S_Button1", eventType="Down",
       script='// F01 S_Button1 Down\nHMIRuntime.Trace("F01 button down");'),
    item("FP_Script", "S_IOField1", "HmiIOField", L(20, 100, 160, 40, BackColor="0xFFE0FFE0")),
    iset("FP_Script", "S_IOField1", {"ProcessValue": TAG}),
    hu("itemEventCreate", screenName="FP_Script", name="S_IOField1", propertyName="ProcessValue", eventType="Change",
       script='// F01 S_IOField1 ProcessValue Change\nHMIRuntime.Trace("F01 value changed: " + value);'),
    hu("itemEventCreate", screenName="FP_Script", eventType="Loaded",
       script='// F01 FP_Script Loaded\nHMIRuntime.Trace("F01 screen loaded");'),
    item("FP_Script", "S_Switch1", "HmiToggleSwitch"),
    hu("itemDelete", screenName="FP_Script", name="S_Switch1"),
    # 7. FP_Flash
    item("FP_Flash", "X_Rect1", "HmiRectangle", L(20, 20, 120, 80, BackColor="0xFF00CC66")),
    hu("itemBindTag", screenName="FP_Flash", name="X_Rect1", tagName=TAG, propertyName="Visible"),
    flash_import,
]


# --- truth ---------------------------------------------------------------------------------------------------------------
def _obj(ctx, tool, calls, ok_errors=True, **a):
    """Truth call as truth_f01.py made it (no path; result parsed, fullResultPath resolved by ctx.call)."""
    from truth import _raw
    txt = _raw(ctx, tool, ok_errors=ok_errors, **a)
    err = bool(ctx.log[-1]["error"])
    calls.append({"tool": tool, "args": dict(a), "error": err})
    try:
        return err, json.loads(txt)
    except ValueError:
        return True, {"raw": txt}


def _save(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=1)


def screens_truth(ctx, out, calls):
    """screens.json + screen_<name>.full.json (get detail=full; fallback items + itemGet full per item), truth_f01.py."""
    err, screens = _obj(ctx, "TiaHmiUnified", calls, operation="list", objectKind="screen", device=H)
    _save(os.path.join(out, "screens.json"), screens)
    for n in [s["name"] for s in screens.get("objects", screens.get("items", []))]:
        err, full = _obj(ctx, "TiaHmiUnified", calls, operation="get", objectKind="screen", device=H, names=n, detail="full")
        old = os.path.join(ctx.old_truth, os.path.relpath(out, ctx.tdir), "screen_%s.full.json" % n)
        fallback_before = False
        if os.path.isfile(old):
            with open(old, encoding="utf-8-sig") as f:
                fallback_before = "getFullFailed" in json.load(f)
        # the original server failed on FP_Basic/FP_Elements ("Ambiguous match found."); the current one reads them:
        # keep the fallback form of the old file (getFullFailed then holds the successful result)
        if not err and full.get("count") == 1 and not full.get("skipped") and not fallback_before:
            _save(os.path.join(out, "screen_%s.full.json" % n), full)
            continue
        _, items = _obj(ctx, "TiaHmiUnified", calls, operation="get", objectKind="screen", device=H, names=n, detail="items")
        _, il = _obj(ctx, "TiaHmiUnified", calls, operation="itemList", objectKind="screen", device=H, screenName=n)
        per = {}
        for it in il.get("items", il.get("objects", [])):
            iname = it.get("name") or it.get("Name")
            e2, g = _obj(ctx, "TiaHmiUnified", calls, operation="itemGet", objectKind="screen", device=H, screenName=n,
                         name=iname, detail="full")
            per[iname] = g if not e2 else {"error": g}
        _save(os.path.join(out, "screen_%s.full.json" % n),
              {"getFullFailed": full, "detailItems": items, "itemList": il, "itemGetFull": per})


def f01_truth(ctx):
    out = os.path.join(ctx.tdir, H)
    os.makedirs(out, exist_ok=True)
    calls = []
    screens_truth(ctx, out, calls)
    for kind, fn in (("tag", "tags.json"), ("tagTable", "tagTables.json"), ("scriptModule", "scriptModules.json")):
        _save(os.path.join(out, fn), _obj(ctx, "TiaHmiUnified", calls, operation="list", objectKind=kind, device=H)[1])
    _save(os.path.join(out, "tags.get.json"), _obj(ctx, "TiaHmiUnified", calls, operation="get", objectKind="tag", device=H)[1])
    _save(os.path.join(out, "software.json"), _obj(ctx, "TiaHmiUnified", calls, operation="get", objectKind="software",
                                                   device=H)[1])
    _save(os.path.join(out, "scriptSearch.json"), _obj(ctx, "TiaHmiUnified", calls, operation="scriptSearch",
                                                       objectKind="screen", device=H, pattern=".",
                                                       names="FP_Script,FP_Interface")[1])
    _save(os.path.join(out, "_truth_calls.json"), calls)
    xml = os.path.join(out, "xml")
    os.makedirs(xml, exist_ok=True)
    for s in ("FP_Controls", "FP_Flash", "FP_Interface", "FP_Script", "Main"):
        _obj(ctx, "TiaHmiUnified", [], ok_errors=False, operation="exportXml", objectKind="screen", device=H, name=s,
             exportDir=xml)
    # input files of the original (not Openness output)
    src = os.path.join(ctx.old_truth, "_scripts")
    if os.path.isdir(src):
        shutil.copytree(src, os.path.join(ctx.tdir, "_scripts"), dirs_exist_ok=True)
    ctx.truth_missing = []


def _old_json(ctx, rel):
    p = os.path.join(ctx.old_truth, rel)
    if not os.path.isfile(p):
        return None
    with open(p, encoding="utf-8-sig") as f:
        return json.load(f)


def _excerpt(d, old):
    """Excerpt of an Openness result with the keys of the old excerpt (in their order); `source` from the old file."""
    out = {}
    for k in old:
        if k == "source":
            out[k] = old[k]
        elif k == "events":
            out[k] = [{kk: e.get(kk) for kk in (old[k][0] if old[k] else {})} for e in d.get("events", [])]
        else:
            out[k] = d.get(k, d.get(k[0].lower() + k[1:]) if k[:1].isupper() else None)
    return out


def instance_dump(ctx, calls, screen, name, events=False, types=None):
    """instance_<...>.json of F16/F18b: typesList, itemGet full, partGet Interface and every element (+ EventHandlers)."""
    q = lambda **a: _obj(ctx, "TiaHmiUnified", calls, objectKind="screen", device=H, screenName=screen, name=name, **a)[1]  # noqa: E731
    d = {"typesList": _obj(ctx, "TiaProjectLibrary", calls, operation="typesList", nameFilter=types)[1] if types else None,
         "itemGet": q(operation="itemGet", detail="full")}
    if not types:
        d.pop("typesList")
    d["Interface"] = q(operation="partGet", partPath="Interface")
    d["elements"] = [q(operation="partGet", partPath="Interface/#%d" % i) for i in range(d["Interface"].get("count", 0))]
    if events:
        d["EventHandlers"] = q(operation="partGet", partPath="EventHandlers")
        d["eventHandlers"] = [q(operation="partGet", partPath="EventHandlers/#%d" % i)
                              for i in range(d["EventHandlers"].get("count", 0))]
    return d


def fp_truth_file(ctx, out, rel, calls):
    """Generators of the F truth files (same calls as the original truth scripts); False when there is none."""
    o = _old_json(ctx, rel) if rel.endswith(".json") else None
    pj = lambda op, **a: _obj(ctx, "TiaProjectLibrary", calls, operation=op, **a)[1]  # noqa: E731
    hj = lambda op, kind="screen", **a: _obj(ctx, "TiaHmiUnified", calls, operation=op, objectKind=kind, device=H, **a)[1]  # noqa: E731
    m = re.fullmatch(r"typesList(?:_(.+))?\.json", rel)
    if m:
        _save(os.path.join(out, rel), pj("typesList", nameFilter=m.group(1)))
    elif rel == "library.json":
        _save(os.path.join(out, rel), pj("get"))
    elif rel == "software.json":
        _save(os.path.join(out, rel), hj("get", "software"))
    elif rel == "tags.json":
        _save(os.path.join(out, rel), hj("get", "tag"))
    elif re.fullmatch(r"instancesList_(.+)\.json", rel):
        _save(os.path.join(out, rel), pj("instancesList", typeName=rel[14:-5], scopeKind="HmiTarget"))
    elif re.fullmatch(r"typeExport_(.+)_(\d+\.\d+\.\d+)\.xml", rel):
        t, v = re.fullmatch(r"typeExport_(.+)_(\d+\.\d+\.\d+)\.xml", rel).groups()
        _obj(ctx, "TiaProjectLibrary", calls, ok_errors=False, operation="typeExport", typeName=t, version=v,
             targetPath=os.path.join(out, rel))
    elif re.fullmatch(r"(FP_\w+)_(C_\w+)_Interface\.json", rel):
        s, i = re.fullmatch(r"(FP_\w+)_(C_\w+)_Interface\.json", rel).groups()
        _save(os.path.join(out, rel), hj("partGet", screenName=s, name=i, partPath="Interface"))
    elif re.fullmatch(r"itemGet_Main_(.+)\.json", rel):
        _save(os.path.join(out, rel), hj("itemGet", screenName="Main", name=rel[13:-5].replace("_", " ").replace(
            "Faceplate container ", "Faceplate container_"), detail="full"))
    elif re.fullmatch(r"(item|instance)_Main_(.+)\.json", rel) and o and "source" in o:
        name = re.fullmatch(r"(item|instance)_Main_(.+)\.json", rel).group(2).replace("Faceplate_container", "Faceplate container")
        _save(os.path.join(out, rel), _excerpt(hj("itemGet", screenName="Main", name=name, detail="full"), o))
    elif re.fullmatch(r"instance_Main_(.+)\.json", rel) and o and "itemGet" in o:
        # F18b: typesList FP_Interface, itemGet, Interface and every element, EventHandlers and every handler
        name = rel[14:-5].replace("Faceplate_container", "Faceplate container")
        _save(os.path.join(out, rel), instance_dump(ctx, calls, "Main", name, events=True,
                                                    types="FP_Interface" if "typesList" in o else None))
    elif rel.startswith("_scripts/") or rel == "library_texts.json":
        # input files / excerpts of a user export (F32_texts.xlsx, not regenerated): copied
        dst = os.path.join(out, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(os.path.join(ctx.old_truth, rel), dst)
    else:
        return False
    return True


def fp_truth(ctx, extra=None):
    """Truth of F03-F36: every file of the old truth folder; screens.json/screen_*.full.json via screens_truth;
    _truth_calls.json = the calls made. `extra(ctx, out, calls, files)` writes files that need a helper instance."""
    import truth
    files = truth.old_files(ctx.old_truth) if os.path.isdir(ctx.old_truth) else []
    out = ctx.tdir
    os.makedirs(out, exist_ok=True)
    calls = []
    if any(f.startswith("screen_") or f == "screens.json" for f in files):
        screens_truth(ctx, out, calls)
    done = {f for f in files if f.startswith("screen_") or f == "screens.json"}
    done |= set(ctx.rec.get("helper_files", ()))  # written after the archive (helper_truth)
    if extra:
        done |= extra(ctx, out, calls, files)
    missing = [f for f in files if f not in done and f != "_truth_calls.json" and not fp_truth_file(ctx, out, f, calls)]
    if "_truth_calls.json" in files:
        _save(os.path.join(out, "_truth_calls.json"), calls)
    for f in files:  # hand-written provenance note ("source") of the old truth file: taken over
        o = _old_json(ctx, f) if f.endswith(".json") else None
        p = os.path.join(out, f)
        if isinstance(o, dict) and "source" in o and os.path.isfile(p):
            with open(p, encoding="utf-8-sig") as fh:
                n = json.load(fh)
            if isinstance(n, dict) and "source" not in n:
                n["source"] = o["source"]
                _save(p, n)
    ctx.truth_missing = missing
    if missing:
        raise RuntimeError("no truth generator for %s" % missing)


def pl(op, ok_errors=False, **a):
    """TiaProjectLibrary call."""
    def step(ctx):
        ctx.call("TiaProjectLibrary", ok_errors=ok_errors, operation=op, **{k: v for k, v in a.items() if v is not None})
    return step


_CREATE = ("Project tree > HMI_1 > Screens > double click {s} (screen editor); click an empty spot of the screen, Ctrl+A "
           "(all items), right click on a selected item > 'Create faceplate'. TIA creates the type Faceplate_1 with "
           "V 0.0.1 [in work] and opens it in the faceplate editor. Libraries task card > Project library > Types > "
           "Faceplate_1: F2, Ctrl+A, Ctrl+V of '{s}' (clipboard; '_' arrives as '-' over VNC), Enter. ")
_RELEASE_UI = ("In the yellow bar of the faceplate editor click 'release the version'; dialog 'Release type version' "
               "(version 0.0.1, author factorybyte, no comment, options unchanged) > OK; the editor closes. ")
_CLOSE = "Close the type editor (X of the editor) without release. "
NOTE_F02 = (_CREATE.format(s="FP_Basic") + _RELEASE_UI + "Close the screen FP_Basic. "
            + _CREATE.format(s="FP_Controls") + "(Only C_AlarmControl1, C_Trend1, C_DetParam1 and C_Faceplate1 are "
            "taken over.) " + _CLOSE + "Close the screen FP_Controls. Do not save in TIA. (The original also created "
            "and discarded FP_Elements, and TIA crashed when FP_Flash was opened; the folder of the original therefore "
            "held session files. Neither is repeated: the rebuild is the saved, closed state.)")
NOTE_F03 = (_CREATE.format(s="FP_Elements") + "(E_AlarmInd1 is not taken over.) " + _RELEASE_UI
            + "Close the screen. " + _CREATE.format(s="FP_Interface") + _CLOSE + "Close the screen. "
            + _CREATE.format(s="FP_Script") + _CLOSE + "Close the screen. Do not save in TIA.")

def helper_truth(files, version, n):
    """Truth files read through a helper instance of FP_Interface (F05, F16): written after the archive, the helper
    instance FP_Iface_<n> is created on Main and discarded with the project (discard_on_close). `files`: {rel: kind}."""
    def step(ctx):
        calls = []
        name = "FP_Iface_%d" % n
        _obj(ctx, "TiaHmiUnified", calls, ok_errors=False, operation="itemCreate", objectKind="screen", device=H,
             screenName="Main", name=name, itemType="HmiFaceplateContainer", containedType="V%s\\FP_Interface" % version)
        for rel, kind in files.items():
            o = _old_json(ctx, rel) or {}
            if kind == "partGet":  # F05: note, Interface, Interface/#0..
                q = lambda p: _obj(ctx, "TiaHmiUnified", calls, objectKind="screen", device=H, screenName="Main",  # noqa: E731
                                   name=name, operation="partGet", partPath=p)[1]
                d = {"note": o.get("note"), "Interface": q("Interface")}
                for i in range(d["Interface"].get("count", 0)):
                    d["Interface/#%d" % i] = q("Interface/#%d" % i)
            else:  # F16: typesList, itemGet, Interface, elements, note
                d = instance_dump(ctx, calls, "Main", name, types="FP_Interface")
                d["note"] = o.get("note")
            _save(os.path.join(ctx.tdir, rel), d)
    return step


NOTE_F05 = ("Libraries task card > Project library > Types > FP_Interface > V 0.0.1 [in work]: double click (faceplate "
            "editor). Task card on the right side of the editor: tab 'Interface' > 'Tag interface' (Tag-Schnittstelle): "
            "double click <Add new>, name 'HmiTag_Ext1' (clipboard), data type Bool. Close the editor (stays in "
            "work; release follows via MCP typeVersionRelease). Do not save in TIA.")

F16_PROPS = [("P_Bool", "Bool"), ("P_Int", "LInt"), ("P_Real", "LReal"), ("P_ULInt", "LInt"), ("P_Color", "Color"),
             ("P_Graphic", "Graphic"), ("P_Text", "Multilingual text"), ("P_String", "WString"), ("P_Auth", "Authorization"),
             ("P_List", "Resource list"), ("P_ParamSet", "Parameter set type")]
_EDITOR = ("Libraries task card > Project library > Types > {t} > V {v} [in work]: right click > Open (faceplate editor; "
           "close the yellow 'in work' bar with its X to see the tabs Visualization / Tag Interface / Property Interface / "
           "Local tags / Event Interface). ")
NOTE_F16A = (_EDITOR.format(t="FP_Interface", v="0.0.2") + "Tab 'Property Interface': for each of " +
             ", ".join("%s %s" % p for p in F16_PROPS) + " (in this order) double click <Add new>, Ctrl+V of the name "
             "(clipboard), Enter, then the Data type cell: double click, choose the data type from the drop-down list, "
             "Enter. Close the editor (stays in work; release follows via MCP typeVersionRelease). Do not save in TIA.")
NOTE_F16B = (_EDITOR.format(t="FP_Interface", v="0.0.3") + "Tab 'Property Interface': data type of P_ULInt LInt -> ULInt. "
             "Close the editor. Do not save in TIA.")
F18_CODE_A = '\r\nlet parameters = {Nr:1, Flag:true};\r\nFaceplate.RaiseEvent("EvTest", parameters);\r\n\r\n\r\n'
F18_CODE_B = '\r\n    Faceplate.RaiseEvent("EvTest", { Nr: 5, Flag: true });\r\n'
NOTE_F18A = (_EDITOR.format(t="FP_Interface", v="0.0.4") + "Tab 'Event Interface': <Add new> event 'EvTest' with the "
             "parameters 'Nr' (Int, the default) and 'Flag' (Bool). Tab 'Visualization': drag a Button from the toolbox "
             "(Elements) into the faceplate (TIA names it Button_1), Properties > Size and position: X 270, Y 50, 80x40 "
             "(default size). Properties > Events > Click (Tapped): script; select all of the script text and paste "
             "F18_CODE_A (clipboard, exact text incl. empty lines). Close the editor (stays in work; release follows via "
             "MCP typeVersionRelease). Do not save in TIA.")
NOTE_F18B = (_EDITOR.format(t="FP_Interface", v="0.0.5") + "Tab 'Visualization': Button_1 > Properties > Events > Click: "
             "replace the script text by F18_CODE_B (clipboard). Close the editor. Do not save in TIA.")
# interface values of Faceplate container_2 in the stored order of the original (parser faceplates.py)
# (MCP partSet rejects Color and ULInt values: "data type ... does not match"; Openness V21 cannot create interface
# event handlers: those and the texts are set in the UI, the rest with MCP, keeping the order)
F18B_VALUES = [("P_Graphic", "Left_Arrow"), ("P_Text", "Hallo"), ("P_String", "abc"), ("P_Color", "0xFFFF0000"),
               ("P_ULInt", "89"), ("P_Int", "42"), ("P_List", "SystemTextList_AllenBradleyDF1AlarmTextListCollection_9"),
               ("P_Bool", "true"), ("P_Real", "3.5")]
NOTE_F18bA = ("Project tree > HMI_1 > Screens > Main (open); select 'Faceplate container_2' > Properties > Properties > "
              "Interface: P_Text static value 'Hallo' (en-US), P_String 'abc', P_Color red 0xFFFF0000 (type FFFF0000 "
              "in the color cell), P_ULInt 89 (in this order). Do not save in TIA.")
NOTE_F18bB = ("Main, 'Faceplate container_2' > Properties > Events > EvTest: convert the function list to a script "
              "(toolbar button 'Converts the function list to a script'), then in line 2 of the function body "
              "paste 'HMIRuntime.Trace(\"EvTest \" + Nr);' (clipboard). Close the screen. Do not save in TIA.")

NOTE_F12 = (_EDITOR.format(t="FP_Interface", v="0.0.6") + "Tab 'Tag Interface': <Add new> T_Real (Real), T_DateTime "
            "(DateTime), T_WString (Bool; WString is not offered), T_Array ('Array[0..3] of Int'). Libraries > Project "
            "library > Types > 'Add new type' > HMI user data type, name User_data_type_1 (default), OK; in its editor "
            "members v1 (Int) and v2 (Int); release it (0.0.1). Back in FP_Interface 0.0.6, Tag Interface: T_Udt with "
            "data type User_data_type_1. Release FP_Interface from the yellow bar: version 0.0.6, option 'Update "
            "instances in the project' checked, OK. Do not save in TIA.")
_RELEASE_UPD = ("Release from the library: right click on the version [in work] > 'Release version'; dialog: option "
                "'Update instances in the project' checked, OK. ")
NOTE_F15 = (_EDITOR.format(t="FP_Interface", v="0.0.7") + "Tab 'Local tags': <Add new>, name LocalInt (data type Int). "
            "Close the editor. " + _RELEASE_UPD + "Do not save in TIA.")
NOTE_F20 = (_EDITOR.format(t="FP_Elements", v="0.0.2") + "Click an empty spot of the faceplate (type selected) > "
            "Properties: display name 'Elemente' (not stored by TIA, as in the original), background color 255, 255, 153 "
            "(0xFFFFFF99), Suspendable on. Task card Layout > Layers: rename Layer_1 to 'Hilfsebene' and switch off "
            "'Visible in runtime'; select E_Button1 > Properties > Miscellaneous > Layer = Hilfsebene. Close the editor "
            "(release follows via MCP typeVersionRelease). Do not save in TIA.")
NOTE_F30 = (_EDITOR.format(t="FP_Controls", v="0.0.1") + "Select C_Faceplate1 > Properties > Miscellaneous > "
            "Faceplate type: FP_Basic V 0.0.1. Close the editor (release follows via MCP typeVersionRelease). Do not save "
            "in TIA.")
NOTE_F33 = (_EDITOR.format(t="FP_Controls", v="0.0.2") + "C_AlarmControl1 > Properties > Columns (alarm view): column "
            "'Name' width 100 -> 123, column 'Alarm class' visible off. C_Trend1 > Properties > Trend areas > [0] > Trends: "
            "add a trend (Trend_2) with data source HmiTag_Ext1 (TIA creates the local tag HmiTag_Ext1, Int). Close the "
            "editor (release follows via MCP typeVersionRelease). Do not save in TIA.")
NOTE_F18c = ("Screens > Main (open). Faceplate container_2 > Properties > Events > EvTest: delete the script (toolbar X "
             "'Delete', Yes), which leaves an empty function list; <Add function> SetTagValue, Tag HmiTag_Ext1, Value 1; "
             "<Add function> ChangeScreen, Screen name FP_Basic (Screen window '.' is the default). Faceplate "
             "container_3 > Events > EvTest: convert the empty function list to a script, in line 2 of the function body "
             "type ' HMIRuntime.Trace(\"xx\");' (leading space, clipboard). Close the screen. Do not save in TIA.")
NOTE_F36 = ("Libraries > Project library > Types > FP_Basic (the type, not the version): right click > User tags > "
            "Set 'AlwaysDownload'. Screens > Main (open): toolbox Elements > Button, click into the screen (TIA names it Button_1); "
            "Properties > Size and position: X 77, Y 33 (size 160x40 is the default). Events > Click left mouse button "
            "(Tapped): convert the function list to a script; in the empty line 2 insert the snippet (script editor "
            "right click > Snippets > HMI Runtime > Screen > 'Open faceplate in popup'; double click the inserted "
            "placeholder \"Faceplate_1_V_0_0_1\" and paste FP_Basic_V_0_0_1 (clipboard; TIA links it to FP_Basic 0.0.1), "
            "replace \"title\" by \"PopupBasic\". Close the screen. Do not save in TIA.")
NOTE_F32 = (_EDITOR.format(t="FP_Interface", v="0.0.8") + "Select I_Text1 > Properties > Text: 'Hello' (editing language "
            "en-US); inspector tab 'Texts' (or the project text editor): de-DE text of I_Text1 'Hallo'. Close the editor. "
            + _RELEASE_UPD + "Project tree > HMI_1 > Runtime settings > General > Encrypted transfer: uncheck "
            "'Activate encrypted transfer' (original F32: ActivateEncryptedTransfer False; Openness cannot write it). "
            "Close the editor. Do not save in TIA.")

RECIPES = {
    "F01_hmi_allobjects": {"from": "103_hmi_tag_ext", "steps": F01_STEPS, "truth": f01_truth},
    # project folders (no archive), like the originals
    "F02_hmi_faceplates_crash": {"from": "F01_hmi_allobjects", "steps": [pause(NOTE_F02)], "archive": False},
    # the original had the project languages de-DE and it-IT active in F03 (truth: texts of the screen items in de-DE/it-IT
    # "Text"), but the comments of the released versions FP_Basic/FP_Elements only carry en-US (typeExport) while the
    # versions in work carry 3 used cultures (analysis RELEASE.md): the languages were added after the last release;
    # how is not recorded, here with TiaProject(languageSet) after the faceplates were created
    "F03_hmi_faceplates_user": {"from": "F02_hmi_faceplates_crash",
                                # original: OnlineLanguageOrder 'en-US;it-IT' in the hidden devices of the versions
                                # in work (FP_Controls/FP_Interface/FP_Script). TIA writes an added language only
                                # into hidden devices that are already loaded; after a reopen the first languageSet
                                # call loads them without writing its language. Hence: save, reopen, add de-DE,
                                # save, add it-IT. Adding both right after the UI steps (devices loaded) gives
                                # 'en-US;de-DE;it-IT' (probes 2026-11-09)
                                "steps": [pause(NOTE_F03),
                                          lambda ctx: ctx.call("TiaProject", operation="save"),
                                          lambda ctx: ctx.call("TiaProject", operation="close"),
                                          lambda ctx: ctx.call("TiaProject", operation="open", path=ctx.ap),
                                          lambda ctx: ctx.call("TiaProject", operation="languageSet", add="de-DE"),
                                          lambda ctx: ctx.call("TiaProject", operation="save"),
                                          lambda ctx: ctx.call("TiaProject", operation="languageSet", add="it-IT")],
                                "archive": False,
                                "truth": fp_truth},
    # F25 + F11 of the original: instance of FP_Basic on Main (dragged in the original; the instance is created with
    # itemCreate containedType, position and size of the original), interface tag of FP_Interface (editor), release
    "F05_fp_iface_instance": {
        "from": "F03_hmi_faceplates_user", "truth": fp_truth,
        "steps": [hu("itemCreate", screenName="Main", name="Faceplate container_1", itemType="HmiFaceplateContainer",
                     containedType="V0.0.1\\FP_Basic", attributes={"Left": 130, "Top": 73, "Width": 750, "Height": 300}),
                  pause(NOTE_F05), pl("typeVersionRelease", typeName="FP_Interface")],
        "helper_files": ["partGet_FP_Interface_instance_Interface.json"],
        "after_archive": [helper_truth({"partGet_FP_Interface_instance_Interface.json": "partGet"}, "0.0.1", 1)],
        "discard_on_close": True},
    # F16: two new versions of FP_Interface ("Edit type" = MCP typeVersionEdit, release = MCP typeVersionRelease)
    "F16_iface_props": {
        "from": "F05_fp_iface_instance", "truth": fp_truth,
        "steps": [pl("typeVersionEdit", typeName="FP_Interface"), pause(NOTE_F16A),
                  pl("typeVersionRelease", typeName="FP_Interface"),
                  pl("typeVersionEdit", typeName="FP_Interface"), pause(NOTE_F16B),
                  pl("typeVersionRelease", typeName="FP_Interface")],
        "helper_files": ["instance_FP_Iface_3.json"],
        "after_archive": [helper_truth({"instance_FP_Iface_3.json": "instance"}, "0.0.3", 3)],
        "discard_on_close": True},
    "F18_iface_event": {
        "from": "F16_iface_props", "truth": fp_truth,
        "steps": [pl("typeVersionEdit", typeName="FP_Interface"), pause(NOTE_F18A),
                  pl("typeVersionRelease", typeName="FP_Interface"),
                  pl("typeVersionEdit", typeName="FP_Interface"), pause(NOTE_F18B),
                  pl("typeVersionRelease", typeName="FP_Interface")]},
    # F18b: instance of FP_Interface 0.0.5 on Main with an interface event handler and interface values (dragged and
    # edited in the UI in the original; here with MCP: itemCreate containedType, partSet Interface, itemEventCreate)
    "F18b_event_handler": {
        "from": "F18_iface_event", "truth": fp_truth,
        "steps": [hu("itemCreate", screenName="Main", name="Faceplate container_2", itemType="HmiFaceplateContainer",
                     containedType="V0.0.5\\FP_Interface", attributes={"Left": 19, "Top": 187}),
                  *[hu("partSet", screenName="Main", name="Faceplate container_2", partPath="Interface/" + p,
                       attribute="Value", value=v) for p, v in F18B_VALUES[:1]],
                  pause(NOTE_F18bA),
                  *[hu("partSet", screenName="Main", name="Faceplate container_2", partPath="Interface/" + p,
                       attribute="Value", value=v) for p, v in F18B_VALUES[5:]],
                  pause(NOTE_F18bB)]},
    # F12: new version 0.0.6 of FP_Interface with interface tags of further data types and a new HMI UDT type
    # (Openness V21 cannot create HMI UDT library types nor edit the tag interface: UI); released in the UI with
    # "Update instances in the project" (Faceplate container_2 -> 0.0.6, as in the original)
    "F12_iface_tags": {
        "from": "F18b_event_handler", "truth": fp_truth,
        "steps": [pl("typeVersionEdit", typeName="FP_Interface"), pause(NOTE_F12)]},
    # F15: FP_Interface 0.0.7 with the local tag LocalInt (the analysis: limits not stored, no binding)
    "F15_local_tag": {
        "from": "F12_iface_tags", "truth": fp_truth,
        "steps": [pl("typeVersionEdit", typeName="FP_Interface"), pause(NOTE_F15)]},
    # F20: properties of the type FP_Elements (new version 0.0.2)
    "F20_type_props": {
        "from": "F15_local_tag", "truth": fp_truth,
        "steps": [pl("typeVersionEdit", typeName="FP_Elements"), pause(NOTE_F20),
                  pl("typeVersionRelease", typeName="FP_Elements")]},
    # F30: in FP_Controls 0.0.1 (in work since F02) the empty container C_Faceplate1 gets the type FP_Basic; release
    "F30_nested": {
        "from": "F20_type_props", "truth": fp_truth,
        "steps": [pause(NOTE_F30), pl("typeVersionRelease", typeName="FP_Controls")]},
    # F33: FP_Controls 0.0.2 with changed alarm view columns and a second trend
    "F33_control_columns": {
        "from": "F30_nested", "truth": fp_truth,
        "steps": [pl("typeVersionEdit", typeName="FP_Controls"), pause(NOTE_F33),
                  pl("typeVersionRelease", typeName="FP_Controls")]},
    # F18c: handler of Faceplate container_2 as function list, second instance with a script handler (Openness V21 has
    # neither function lists nor interface event handlers: UI; the instance itself with MCP)
    "F18c_event_funclist": {
        "from": "F33_control_columns", "truth": fp_truth,
        "steps": [hu("itemCreate", screenName="Main", name="Faceplate container_3", itemType="HmiFaceplateContainer",
                     containedType="V0.0.7\\FP_Interface", attributes={"Left": 505, "Top": 18}),
                  pause(NOTE_F18c)]},
    # F36: AlwaysDownload on the type FP_Basic, Button_1 on Main with the snippet "Open faceplate in popup" (both UI only)
    "F36_popup_alwaysdownload": {
        "from": "F18c_event_funclist", "truth": fp_truth, "steps": [pause(NOTE_F36)]},
    # F32: project languages en-US/de-DE (it-IT removed, reference de-DE), German as runtime and logging language,
    # FP_Interface 0.0.8 with I_Text1 "Hello"/"Hallo", released with instance update. The F32_texts.xlsx of the original
    # is a user export (not regenerated); library_texts.json is an excerpt of it and is copied.
    "F32_multilingual": {
        "from": "F36_popup_alwaysdownload", "truth": fp_truth,
        "steps": [lambda ctx: ctx.call("TiaProject", operation="languageSet", remove="it-IT", referenceLanguage="de-DE"),
                  hu("list", "languageAndFont"),
                  hu("set", "languageAndFont", index="1", attribute="Enable", value="true"),
                  hu("set", "languageAndFont", index="1", attribute="EnableForLogging", value="true"),
                  pl("typeVersionEdit", typeName="FP_Interface"), pause(NOTE_F32)]},
}

MANUAL = {
    "F02_hmi_faceplates_crash": "faceplate types FP_Basic and FP_Controls created from the screens in the TIA user "
                                "interface (VNC): " + NOTE_F02,
    "F03_hmi_faceplates_user": "faceplate types FP_Elements, FP_Interface and FP_Script created from the screens in "
                               "the TIA user interface (VNC): " + NOTE_F03,
    "F05_fp_iface_instance": "interface tag of FP_Interface added in the faceplate editor (VNC): " + NOTE_F05,
    "F16_iface_props": "property interface of FP_Interface edited in the faceplate editor (VNC): " + NOTE_F16A + " / "
                       + NOTE_F16B,
    "F18_iface_event": "interface event and Button_1 with RaiseEvent script in FP_Interface (faceplate editor, VNC): "
                       + NOTE_F18A + " / " + NOTE_F18B,
    "F18b_event_handler": "interface values (Text, String, Color, ULInt) and the EvTest script handler of Faceplate "
                          "container_2 in the TIA user interface (VNC): " + NOTE_F18bA + " / " + NOTE_F18bB,
    "F12_iface_tags": "tag interface of FP_Interface and HMI UDT type in the TIA user interface (VNC): " + NOTE_F12,
    "F15_local_tag": "local tag of FP_Interface in the faceplate editor (VNC): " + NOTE_F15,
    "F20_type_props": "type properties and layer of FP_Elements in the faceplate editor (VNC): " + NOTE_F20,
    "F30_nested": "faceplate type of C_Faceplate1 in FP_Controls in the faceplate editor (VNC): " + NOTE_F30,
    "F33_control_columns": "alarm columns and trend of FP_Controls in the faceplate editor (VNC): " + NOTE_F33,
    "F18c_event_funclist": "function list / script handlers of the instances on Main (VNC): " + NOTE_F18c,
    "F36_popup_alwaysdownload": "AlwaysDownload user tag and popup snippet (VNC): " + NOTE_F36,
    "F32_multilingual": "texts of I_Text1 in FP_Interface 0.0.8 and release with instance update (VNC): " + NOTE_F32,
}
