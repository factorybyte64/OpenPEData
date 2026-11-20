"""Recipes L01-L40 (project library steps L1, L2, L4 and its addendum, global library step L6, HMI library types of
samples/MANIFEST.md).

Project samples (L01-L29, L33-L35, L37-L40) are built like every other sample (folder copy of the predecessor -> open ->
change -> save -> truth -> archive -> close). The global library samples L30, L31, L32 and L36 have their own build
function (recipe key "build", called by rebuild.build): folder copy of the predecessor library (or TiaLibrary(create))
-> TiaLibrary(open) -> change -> save -> truth -> TiaLibrary(archive) to <stage>\\out\\<sample>.zal21 -> close. The library
name stays L30_glib_empty in every copy (as in the original); the closed library folder stays in <stage>\\<sample>\\.

The ground truth files of these samples were written by the original scripts in reduced form (selected keys, results of
the change calls in calls.json with notes written by hand). The generators below make the same queries and reduce the
result to the keys of the old file (`like`); notes ("note", "source", "afterwards") are taken over from the old file.
calls.json keeps the call descriptions of the old file and gets the results of the replayed calls (by index); entries
that are not replayed (failed attempts on throwaway copies, notes) stay as they were.
"""
import json
import os
import re
import shutil
import time

from rhelp import _set_number, pause
from truth import _raw, _w

HERE = os.path.dirname(os.path.abspath(__file__))
SAMPLES = os.path.join(os.path.dirname(os.path.dirname(HERE)), "samples")
LIBNAME = "L30_glib_empty"
KEEP_OLD = ("note", "source", "afterwards", "typeExport")


# --- helpers -------------------------------------------------------------------------------------------------------
def _jd(txt):
    try:
        return json.loads(txt)
    except ValueError:
        return txt


def _old(ctx, rel):
    """Old truth file parsed. Some calls.json files of the original hold Windows paths with single backslashes (invalid
    JSON escapes): parsed with those backslashes taken literally."""
    p = os.path.join(ctx.old_truth, rel)
    if not os.path.isfile(p):
        return None
    with open(p, encoding="utf-8-sig") as f:
        txt = f.read()
    try:
        return json.loads(txt)
    except ValueError:
        return json.loads(re.sub(r'\\(?!["\\/bfnrtu])', r"\\\\", txt))


def _raw_backslashes(ctx, rel):
    """True when the old file writes backslashes unescaped (see _old)."""
    with open(os.path.join(ctx.old_truth, rel), encoding="utf-8-sig") as f:
        txt = f.read()
    try:
        json.loads(txt)
        return False
    except ValueError:
        return True


def like(new, old):
    """`new` reduced to the structure of `old`: dict keys of old (in its order; notes taken from old), list items
    item by item against the old item at the same index (or the last one)."""
    if isinstance(old, dict) and isinstance(new, dict):
        out = {}
        for k, v in old.items():
            if k in KEEP_OLD and k not in new:
                out[k] = v
            elif k in new:
                out[k] = like(new[k], v)
        return out
    if isinstance(old, list) and isinstance(new, list) and old:
        return [like(x, old[min(i, len(old) - 1)]) for i, x in enumerate(new)]
    if isinstance(old, str) and isinstance(new, dict):  # the original scripts wrote results as short texts
        if "errorCode" in new:
            return "%s: %s" % (new["errorCode"], new.get("message"))
        if "state" in new:
            return new["state"] + (": " + new["messages"][0] if new.get("messages") else "")
    return new


def _dump(path, obj):
    _w(path, json.dumps(obj, ensure_ascii=False, separators=(",", ":")))


def _input_dir(ctx):
    """Copy of the input documents of the old truth (samples\\<sample>_truth\\input) in <stage>\\work\\<sample>_input."""
    dst = os.path.join(ctx.stage, "work", ctx.name + "_input")
    if not os.path.isdir(dst):
        shutil.copytree(os.path.join(ctx.old_truth, "input"), dst)
    return dst


INP = _input_dir


def inpf(fn):
    return lambda ctx: os.path.join(_input_dir(ctx), fn)


def lc(key, tool, ok_errors=False, **a):
    """Recorded change call: result (parsed) goes to ctx.lres[key] (index in calls.json or name of a *.result.json).
    Callable argument values are resolved with ctx at run time."""
    def step(ctx):
        args = {k: (v(ctx) if callable(v) else v) for k, v in a.items() if v is not None}
        txt = ctx.call(tool, ok_errors=ok_errors, **args)
        if not hasattr(ctx, "lres"):
            ctx.lres = {}
        ctx.lres[key] = _jd(txt)
    return step


def pl(key, op, ok_errors=False, **a):
    return lc(key, "TiaProjectLibrary", ok_errors=ok_errors, operation=op, **a)


def _q(ctx, tool, ok_errors=False, **a):
    return _jd(_raw(ctx, tool, ok_errors=ok_errors, **a))


# --- truth generators ----------------------------------------------------------------------------------------------
def g_calls(ctx, out, rel):
    old = _old(ctx, rel) or []
    res = getattr(ctx, "lres", {})
    new = []
    for i, e in enumerate(old):
        e = dict(e)
        if i in res:
            e["result"] = like(res[i], e.get("result"))
        new.append(e)
    extra = sorted(k for k in res if isinstance(k, int) and k >= len(old))
    if extra:
        raise RuntimeError("calls.json: results without an old entry: %s" % extra)
    txt = "[" + ",\n ".join(json.dumps(e, ensure_ascii=False, separators=(",", ":")) for e in new) + "]"
    if _raw_backslashes(ctx, rel):  # same notation as the old file
        txt = txt.replace("\\\\", "\\")
    with open(os.path.join(ctx.old_truth, rel), "rb") as f:
        if f.read().endswith(b"\n"):  # same file end as the old file
            txt += "\n"
    _w(os.path.join(out, rel), txt)


def g_result(ctx, out, rel):
    _dump(os.path.join(out, rel), like(ctx.lres[rel], _old(ctx, rel)))


def g_types(ctx, out, rel):
    old = _old(ctx, rel)
    if getattr(ctx, "lib", None):  # global library: typesList merged with typeVersionsList (as the original)
        d = _q(ctx, "TiaLibraryContent", operation="typesList", library=ctx.lib)
        for it in d.get("items", []):
            v = _q(ctx, "TiaLibraryContent", operation="typeVersionsList", library=ctx.lib, typeName=it["name"])
            # key names of the merged file of the original: version, modified
            it["versions"] = [dict(x, version=x.get("versionNumber"), modified=x.get("modifiedDate")) for x in v.get("items", [])]
    else:
        d = _q(ctx, "TiaProjectLibrary", operation="typesList")
    _dump(os.path.join(out, rel), like(d, old))


def _instances(ctx, typ, device, version=None):
    return _q(ctx, "TiaProjectLibrary", operation="instancesList", typeName=typ, device=device, version=version)


def g_instances(ctx, out, rel):
    """instancesList_<type>.json: the full result (old file has items) or a map key -> reduced result, key "all",
    "version <v>", "<device> version <v>" or a type name (instancesList.json of L09)."""
    old = _old(ctx, rel)
    m = re.fullmatch(r"instancesList_(.+)\.json", rel)
    typ = m.group(1) if m else None
    if "items" in old:
        d = like(_instances(ctx, typ, old.get("device", "PLC_1")), old)
    else:
        d = {}
        for k, v in old.items():
            mm = re.fullmatch(r"(?:(\S+) )?version (\S+)", k)
            if k == "all":
                r = _instances(ctx, typ, "PLC_1")
            elif mm:
                r = _instances(ctx, typ, mm.group(1) or "PLC_1", mm.group(2))
            else:
                r = _instances(ctx, k, "PLC_1")
            d[k] = like(r, v)
    _w(os.path.join(out, rel), "{" + ",\n ".join("%s:%s" % (json.dumps(k), json.dumps(v, ensure_ascii=False, separators=(",", ":")))
                                              for k, v in d.items()) + "}")


def g_attributes(ctx, out, rel):
    """<name>[.<device>].attributes.json: attributeGet of the object named in the old file (kind type/block)."""
    old = _old(ctx, rel)
    d = _q(ctx, "TiaPlcSoftware", operation="attributeGet", objectKind=old["kind"], device=old["device"], name=old["name"])
    _dump(os.path.join(out, rel), like(d, old))


def g_type_export(ctx, out, rel):
    typ, ver = re.fullmatch(r"typeExport_(.+)_(\d+\.\d+\.\d+)\.xml", rel).groups()
    _raw(ctx, "TiaProjectLibrary", operation="typeExport", typeName=typ, version=ver, targetPath=os.path.join(out, rel))


def g_sd(ctx, out, rel):
    """SD export of one object: <name>.sd.s7dcl/.s7res (PLC_1) or <device>_sd/<name>.s7dcl/.s7res."""
    m = re.fullmatch(r"(?:(\w+)_sd/)?(.+?)(\.sd)?\.(s7dcl|s7res)", rel)
    dev, name = m.group(1) or "PLC_1", m.group(2)
    kind = "type" if name.startswith("UDT_") else "block"
    tmp = os.path.join(ctx.stage, "work", ctx.name + "_sd", dev)
    if not getattr(ctx, "sd_clean", False):  # exports of an earlier build of this sample
        shutil.rmtree(os.path.dirname(tmp), ignore_errors=True)
        ctx.sd_clean = True
    if not os.path.isdir(tmp):
        os.makedirs(tmp)
    if not os.path.exists(os.path.join(tmp, name + ".s7dcl")):
        _raw(ctx, "TiaPlcSoftware", operation="documentExport", objectKind=kind, device=dev, name=name, targetPath=tmp)
    src = os.path.join(tmp, name + "." + m.group(4))
    if os.path.exists(src):
        os.makedirs(os.path.dirname(os.path.join(out, rel)), exist_ok=True)
        shutil.copyfile(src, os.path.join(out, rel))


def g_master_copies(ctx, out, rel):
    if getattr(ctx, "lib", None):
        d = _q(ctx, "TiaLibraryContent", operation="masterCopiesList", library=ctx.lib)
    else:
        d = _q(ctx, "TiaProjectLibrary", operation="masterCopiesList")
    _dump(os.path.join(out, rel), like(d, _old(ctx, rel)))


def g_library_get(ctx, out, rel):
    if getattr(ctx, "lib", None):
        d = _q(ctx, "TiaLibrary", operation="get", library=ctx.lib)
    else:
        d = _q(ctx, "TiaProjectLibrary", operation="get")
    _dump(os.path.join(out, rel), like(d, _old(ctx, rel)))


def g_lib_content(op):
    def g(ctx, out, rel):
        _dump(os.path.join(out, rel), like(_q(ctx, "TiaLibraryContent", operation=op, library=ctx.lib), _old(ctx, rel)))
    return g


def _blocks(ctx, dev):
    return _q(ctx, "TiaPlcSoftware", operation="list", objectKind="block", device=dev)


def g_blocks_dev(ctx, out, rel):
    """blocks_<device>.json (L13): block list, for PLC_2 with the type list (typesPLC_2)."""
    dev = re.fullmatch(r"blocks_(.+)\.json", rel).group(1)
    old = _old(ctx, rel)
    d = _blocks(ctx, dev)
    for k in old:
        if k.startswith("types"):
            d[k] = _q(ctx, "TiaPlcSoftware", operation="list", objectKind="type", device=k[5:])
    _dump(os.path.join(out, rel), like(d, old))


def g_blocks_map(ctx, out, rel):
    """blocks.json of L22: {device: block items}."""
    old = _old(ctx, rel)
    d = {dev: like(_blocks(ctx, dev).get("items", []), v) for dev, v in old.items()}
    _w(os.path.join(out, rel), "{" + ",\n ".join("%s:%s" % (json.dumps(k), json.dumps(v, ensure_ascii=False, separators=(",", ":")))
                                              for k, v in d.items()) + "}")


def g_devices(ctx, out, rel):
    """devices.json of L35: device list plus "blocks <device>" item lists."""
    old = _old(ctx, rel)
    d = _q(ctx, "TiaDevice", operation="list")
    for k in old:
        if k.startswith("blocks "):
            d[k] = _blocks(ctx, k[7:]).get("items", [])
    _dump(os.path.join(out, rel), like(d, old))


LIB_TRUTH = [
    (r"calls\.json", g_calls),
    (r"[^/]+\.result\.json", g_result),
    (r"typesList\.json", g_types),
    (r"instancesList(_.+)?\.json", g_instances),
    (r"[^/]+\.attributes\.json", g_attributes),
    (r"typeExport_.+\.xml", g_type_export),
    (r"([^/]+\.sd\.(s7dcl|s7res))|(\w+_sd/[^/]+\.(s7dcl|s7res))", g_sd),
    (r"masterCopiesList\.json", g_master_copies),
    (r"libraryGet\.json", g_library_get),
    (r"foldersList\.json", g_lib_content("foldersList")),
    (r"languagesList\.json", g_lib_content("languagesList")),
    (r"blocks_[^/]+\.json", g_blocks_dev),
    (r"blocks\.json", g_blocks_map),
    (r"devices\.json", g_devices),
    # notes written by hand about what was typed into the TIA type editors (L38-L40): taken over unchanged
    (r"(content|textlist|graphiclist)\.json", lambda ctx, out, rel: shutil.copyfile(os.path.join(ctx.old_truth, rel),
                                                                                  os.path.join(out, rel))),
]


def lib_truth(ctx):
    """Truth of the L samples: every file of the old truth folder from LIB_TRUTH or the generic generators of truth.py
    (without the Save-Truth layout of truth.make, which would write devices.json unreduced)."""
    import truth
    files = truth.old_files(ctx.old_truth) if os.path.isdir(ctx.old_truth) else []
    os.makedirs(ctx.tdir, exist_ok=True)
    missing = []
    for rel in files:
        for pat, gen in LIB_TRUTH + truth.GENERATORS:
            if re.fullmatch(pat, rel):
                gen(ctx, ctx.tdir, rel)
                break
        else:
            missing.append(rel)
    if missing:
        _w(os.path.join(ctx.out, ctx.name + "_truth_missing.txt"), "\n".join(missing) + "\n")
        raise RuntimeError("no truth generator for %s" % missing)


def lib(pred, *steps, **extra):
    r = {"from": pred, "steps": list(steps), "truth": lib_truth}
    r.update(extra)
    return r


# --- global libraries --------------------------------------------------------------------------------------------------
def _lib_path(ctx):
    return os.path.join(ctx.dir, LIBNAME + ".al21")


def glib_open(key):
    def step(ctx):
        lc(key, "TiaLibrary", operation="open", path=_lib_path(ctx))(ctx)
        ctx.lib = _lib_path(ctx)
    return step


def glib_save(key):
    return lambda ctx: lc(key, "TiaLibrary", operation="save", library=ctx.lib)(ctx)


def with_scratch_l17(key, fn):
    """Open a scratch copy of L17_inst_outdated (source of L31/L32 in the original, never saved), call fn(ctx, ap), close
    it without saving. The open result goes to ctx.lres[key] (None: not recorded)."""
    def step(ctx):
        src = os.path.join(ctx.stage, "L17_inst_outdated")
        if not os.path.isdir(src):
            raise RuntimeError("L17_inst_outdated not built")
        tmp = os.path.join(ctx.stage, "work", ctx.name + "_L17")
        if os.path.exists(tmp):
            shutil.rmtree(tmp)
        shutil.copytree(src, tmp)
        ap = os.path.join(tmp, [x for x in os.listdir(tmp) if x.endswith(".ap21")][0])
        if key is None:
            ctx.call("TiaProject", operation="open", path=ap)
        else:
            lc(key, "TiaProject", operation="open", path=ap)(ctx)
        try:
            fn(ctx, ap)
        finally:
            ctx.call("TiaProject", operation="close", path=ap, discardChanges="true")
        shutil.rmtree(tmp, ignore_errors=True)
    return step


def with_l32(key, fn):
    """Open the rebuilt global library of L32 (<stage>\\L32_glib_mc_device) read-only for a project sample, call
    fn(ctx, lib), close it. The open result goes to ctx.lres[key] (None: not recorded)."""
    def step(ctx):
        p = os.path.join(ctx.stage, "L32_glib_mc_device", LIBNAME + ".al21")
        if not os.path.isfile(p):
            raise RuntimeError("L32_glib_mc_device not built")
        if key is None:
            ctx.call("TiaLibrary", operation="open", path=p, openMode="ReadOnly")
        else:
            lc(key, "TiaLibrary", operation="open", path=p, openMode="ReadOnly")(ctx)
        try:
            fn(ctx, p)
        finally:
            ctx.call("TiaLibrary", operation="close", library=p, ok_errors=True)
    return step


def build_glib(c, stage, name, Ctx):
    """Build function of the global library samples (see module doc); returns the Ctx like rebuild.build."""
    import recipes
    import truth
    rec = recipes.RECIPES[name]
    ctx = Ctx(c, stage, name, rec)
    ctx.lib = None
    os.makedirs(ctx.out, exist_ok=True)
    os.makedirs(os.path.join(stage, "log"), exist_ok=True)
    for p in (ctx.dir, ctx.tdir, os.path.join(ctx.out, name + ".zal21")):
        if os.path.isdir(p):
            shutil.rmtree(p)
        elif os.path.exists(p):
            os.remove(p)
    frm = rec.get("from")
    if frm:
        src = os.path.join(stage, frm)
        if not os.path.isfile(os.path.join(src, LIBNAME + ".al21")):
            raise RuntimeError("predecessor library %s not built" % frm)
        shutil.copytree(src, ctx.dir)
    try:
        for step in rec.get("steps", []):
            step(ctx)
        truth.make(ctx)
        ctx.call("TiaLibrary", operation="archive", library=ctx.lib, archiveDirectory=ctx.out, archiveName=name)
        if not os.path.isfile(os.path.join(ctx.out, name + ".zal21")):
            raise RuntimeError("archive %s.zal21 not written" % name)
    finally:
        if ctx.lib:
            try:
                ctx.call("TiaLibrary", operation="close", library=ctx.lib)
            except Exception as e:  # noqa: BLE001
                ctx.log.append({"close_error": str(e)})
        with open(os.path.join(stage, "log", name + ".json"), "w", encoding="utf-8") as f:
            json.dump(ctx.log, f, indent=1, ensure_ascii=False)
    return ctx


def glib_create(ctx):
    lc(0, "TiaLibrary", operation="create", directory=ctx.stage, name=LIBNAME, author="factorybyte",
       comment="L30 empty global library")(ctx)
    ctx.lib = _lib_path(ctx)


def glib(pred, *steps):
    return {"from": pred, "steps": list(steps), "truth": lib_truth, "build": build_glib, "out_ext": ".zal21"}


def _update_to_glib(ctx, ap):
    lc(2, "TiaLibraryContent", operation="update", path=ap, sourceScope="project", targetScope="global",
       targetLibrary=ctx.lib, typeNameList="UDT_Motor")(ctx)


def _mc_from_device(ctx, ap):
    lc(1, "TiaLibraryContent", operation="masterCopyCreateFromDevice", library=ctx.lib, path=ap, device="PLC_1")(ctx)


def _update_from_glib(ctx, p):
    lc(1, "TiaLibraryContent", operation="update", path=ctx.ap, sourceScope="global", sourceLibrary=p, targetScope="project",
       typeNameList="UDT_Motor")(ctx)


def _mc_from_glib(ctx, p):
    lc(0, "TiaLibraryContent", operation="masterCopyInstantiate", library=p, path=ctx.ap, masterCopyName="PLC_1")(ctx)


def _plc_delete_udt(ctx):
    ctx.call("TiaPlcSoftware", operation="delete", objectKind="type", name="UDT_Motor")


def _tcd(key, fn, **a):
    return pl(key, "typeCreateFromDocument", documentDirectory=INP, fileNameWithoutExtension=fn, **a)


def _rel(key, typ, ver=None, comment=None):
    return pl(key, "typeVersionRelease", typeName=typ, versionNumber=ver, comment=comment)


# manual steps of L37-L40 (Openness V21 cannot create WinCC Unified library types; TiaProjectLibrary(typeCreateFromDocument)
# needs documents that do not exist for these type kinds). Exact clicks: see MANUAL and samples/MANIFEST.md.
NOTE_L37 = ("Libraries task card (right) > Project library > Types: right click > Add new type > Script module type "
            "(name Script module type_1, V0.0.1 in work, no change, close the editor); again Add new type > Dynamic SVG "
            "(name DynamicSVG type_1, default graphic, OK; editor closed without change). Release of DynamicSVG type_1 "
            "follows via MCP typeVersionRelease. Do not save in TIA.")
NOTE_L38 = ("Project library > Types > Script module type_1 > V0.0.1 (in work): right click > Open (script editor, "
            "Global definition selected); line 1: 'export const OPD_TEST = 42;' (via clipboard: '_' arrives as '-' over "
            "VNC; select line 1 with End, Shift+Home, then Ctrl+V), close the editor (keep the version in work). "
            "Do not save in TIA.")
NOTE_L39 = ("Project library > Types > Add new type (double click); scroll the dialog down, Text list / Graphics list, "
            "HMI text list, name Text list type_1, OK; in the editor (list Text_list, Value/Range) double click <Add new> "
            "in Text list entries (value 0 - 1 is proposed, Enter), double click Text, type 'Motor an', Enter, Escape; close "
            "the editor (version stays in work). Do not save in TIA.")
NOTE_L40A = ("Project library > Types > Add new type (double click); Graphic / Dynamic SVG, Graphic, name Graphic "
             "type_1, OK (default graphic); close the editor (in work). Release follows via MCP typeVersionRelease. Do not "
             "save in TIA.")
NOTE_L40B = ("Project library > Types > Add new type (double click); scroll the dialog down, Text list / Graphics list, "
             "HMI graphics list, name Graphic list type_1, OK; the editor proposes the entry 0 - 1 without graphic: click "
             "the GraphicName cell, open its drop-down, select Types > Graphic type_1 > V 0.0.1, apply (green check); close "
             "the editor (version stays in work). Do not save in TIA.")
CLONE_PLC2 = dict(operation="create", name="PLC_2", cloneTypeFromDevice="PLC_1")
NET_CALL = json.dumps([{"code": '"FB_Typed_DB"(in1 := 1);', "language": "SCL", "title": "call FB_Typed"}])

RECIPES = {
    # --- L1: PLC types and versions ---
    "L01_udt_type_inwork": lib("017_udt", _plc_delete_udt, _tcd(None, "UDT_Motor", device="PLC_1")),
    "L02_udt_type_released": lib("L01_udt_type_inwork", _rel("typeVersionRelease.result.json", "UDT_Motor")),
    "L03_udt_type_v2": lib(
        "L02_udt_type_released",
        pl(0, "typeVersionEdit", typeName="UDT_Motor", device="PLC_1"),
        lc(1, "TiaPlcSoftware", ok_errors=True, operation="documentWrite", objectKind="type", sourceFile=inpf("UDT_Motor.s7dcl"),
           compileAfter="false"),
        lc(2, "TiaPlcSoftware", ok_errors=True, operation="documentImport", objectKind="type", sourcePath=INP, name="UDT_Motor",
           importMode="Override"),
        _rel(3, "UDT_Motor", "1.0.0", "L03 release")),
    "L04_default_changed": lib("L03_udt_type_v2", pl("typeVersionSetDefault.result.json", "typeVersionSetDefault",
                                                     typeName="UDT_Motor", version="0.0.1")),
    "L05_fb_type_inwork": lib("004_fb", _tcd(None, "FB_Typed", device="PLC_1")),
    "L06_fb_type_released": lib("L05_fb_type_inwork", _rel("typeVersionRelease.result.json", "FB_Typed")),
    "L07_fc_type": lib("L06_fb_type_released", _tcd(0, "FC_Typed", device="PLC_1"), _rel(1, "FC_Typed", "2.3.4", "L07 FC release")),
    "L09_type_depends": lib("L02_udt_type_released", _tcd(0, "FB_Motor", device="PLC_1"), _rel(1, "FB_Motor", "1.2.3")),
    # --- L2: master copies ---
    "L10_mc_fc": lib("026_fc_scl", pl(0, "masterCopyCreate", device="PLC_1", blockName="FC_Scl")),
    "L11_mc_fb_udt": lib("023_fb_iface", pl(0, "masterCopyCreate", device="PLC_1", blockName="FB_Iface")),
    "L12_mc_two": lib("024_fc_iface", pl(0, "masterCopyCreate", device="PLC_1", blockName="FC_Iface"),
                      pl(1, "masterCopyCreate", device="PLC_1", blockName="FB_Iface")),
    # the original server ignored newName=FB_Iface_Copy (TIA named the block FB_Iface_1); the current one applies it, so
    # it is left out to keep the content of the sample (calls.json keeps the original call text)
    "L13_mc_instantiate": lib("L11_mc_fb_udt",
                              pl(0, "masterCopyInstantiate", masterCopyName="FB_Iface", device="PLC_1"),
                              # the current server moves the copy to the next free number (3, AutoNumber off); the
                              # original block kept number 2 of FB_Iface (number conflict, inconsistent) with AutoNumber on
                              lambda ctx: _set_number(ctx, "PLC_1", "FB_Iface_1", 2),
                              lc(1, "TiaDevice", **CLONE_PLC2),
                              pl(2, "masterCopyInstantiate", masterCopyName="FB_Iface", device="PLC_2")),
    "L14_mc_delete": lib("L12_mc_two", pl(0, "masterCopyDelete", masterCopyName="FC_Iface")),
    # entry 0 of calls.json is a failed attempt on a discarded copy of 024 (not replayed)
    "L15_mc_db": lib("022_db_udt", pl(1, "masterCopyCreate", device="PLC_1", blockName="DB_Motor")),
    # --- L4: version status of type instances ---
    "L16_inst_two_devices": lib("L02_udt_type_released", lc(0, "TiaDevice", **CLONE_PLC2),
                                _tcd(1, "UDT_Motor", addVersionTypeName="UDT_Motor", device="PLC_2"),
                                pl(2, "typeVersionDiscard", typeName="UDT_Motor")),
    "L17_inst_outdated": lib("L16_inst_two_devices", pl(0, "typeVersionEdit", typeName="UDT_Motor", device="PLC_1"),
                             _rel(1, "UDT_Motor", "1.0.0", "L17 release")),
    # entry 0 of calls.json (L18, L19) is a note
    "L18_inst_edit_outdated": lib("L17_inst_outdated",
                                  pl(1, "typeVersionEdit", typeName="UDT_Motor", version="0.0.1", device="PLC_2"),
                                  _rel(2, "UDT_Motor", "1.1.0", "L18 release from PLC_2")),
    "L19_version_discarded": lib("L18_inst_edit_outdated",
                                 pl(1, "typeVersionEdit", typeName="UDT_Motor", version="1.0.0", device="PLC_1"),
                                 pl(2, "typeVersionDiscard", typeName="UDT_Motor")),
    "L20_default_two_devices": lib("L18_inst_edit_outdated", pl(0, "typeVersionSetDefault", typeName="UDT_Motor", version="1.0.0")),
    "L21_fb_inst_outdated": lib(
        "L06_fb_type_released", lc(0, "TiaDevice", **CLONE_PLC2),
        _tcd(1, "FB_Typed", addVersionTypeName="FB_Typed", device="PLC_2"),
        pl(2, "typeVersionDiscard", typeName="FB_Typed"),
        pl(3, "typeVersionEdit", typeName="FB_Typed", device="PLC_1"),
        # failed in the original (content of 1.0.0 = 0.0.1); a success would change the content: see RECIPES check
        lc(4, "TiaPlcSoftware", ok_errors=True, operation="networkWrite", objectKind="block", device="PLC_1", name="FB_Typed",
           mode="replace", index="1", networks=json.dumps([{"code": "#out1 := #in1 + 2;", "language": "SCL"}])),
        _rel(5, "FB_Typed", "1.0.0", "L21 release")),
    # --- L4 addendum ---
    "L22_update_removes_unused": lib("L21_fb_inst_outdated", pl(0, "updateProject", typeNameList="FB_Typed")),
    "L23_inst_called": lib("L21_fb_inst_outdated",
                           *[lc(i, "TiaPlcSoftware", operation="create", objectKind="block", device=d, name="FB_Typed_DB",
                                blockType="InstanceDB", instanceOf="FB_Typed") for i, d in enumerate(("PLC_1", "PLC_2"))],
                           *[lc(2 + i, "TiaPlcSoftware", operation="networkWrite", objectKind="block", device=d, name="Main",
                                mode="append", networks=NET_CALL) for i, d in enumerate(("PLC_1", "PLC_2"))]),
    "L24_inst_update_fb": lib("L23_inst_called", pl(0, "updateProject", typeNameList="FB_Typed")),
    "L25_version_deleted": lib("L24_inst_update_fb", pl(0, "typeVersionDelete", typeName="FB_Typed", version="0.0.1")),
    "L26_type_obsolete": lib("L25_version_deleted", pl(0, "typeSet", typeName="FB_Typed", doNotUse="true")),
    "L27_inst_update_udt": lib("L17_inst_outdated",
                               *[lc(i, "TiaPlcSoftware", operation="documentWrite", objectKind="block", device=d,
                                    sourceFile=inpf("DB_Motor.s7dcl")) for i, d in enumerate(("PLC_1", "PLC_2"))],
                               pl(2, "updateProject", typeNameList="UDT_Motor")),
    "L08_type_in_folder": lib("L02_udt_type_released", pl(0, "folderCreate", folderKind="types", folderPath="Motors"),
                              _tcd(1, "FC_Typed", device="PLC_1", folderPath="Motors/Pumps"),
                              _rel(2, "FC_Typed", "1.0.0", "L08 release in folder"), pl(3, "get")),
    "L28_mc_in_folder": lib("L10_mc_fc", pl(0, "folderCreate", folderKind="masterCopies", folderPath="MC_Group/MC_Sub"),
                            pl(1, "masterCopyMove", masterCopyName="FC_Scl", targetFolder="MC_Group/MC_Sub"), pl(2, "get")),
    "L29_version_content": lib("L02_udt_type_released", pl(0, "typeVersionEdit", typeName="UDT_Motor", device="PLC_1"),
                               _tcd(1, "UDT_Motor", addVersionTypeName="UDT_Motor", createOptions="Override", device="PLC_1"),
                               _rel(2, "UDT_Motor", "1.0.0", "L29 Temp added")),
    # --- L6: global library ---
    "L30_glib_empty": glib(None, glib_create, glib_save(1)),
    "L31_glib_type": glib("L30_glib_empty", glib_open(0), with_scratch_l17(1, _update_to_glib), glib_save(3)),
    "L32_glib_mc_device": glib("L31_glib_type", glib_open(0), with_scratch_l17(None, _mc_from_device), glib_save(2)),
    "L36_glib_mc_deleted": glib("L32_glib_mc_device", glib_open(0),
                                lambda ctx: lc(1, "TiaLibraryContent", operation="masterCopyDelete", library=ctx.lib,
                                               masterCopyName="PLC_1")(ctx), glib_save(2)),
    "L33_proj_update_from_glib": lib("L02_udt_type_released", with_l32(0, _update_from_glib)),
    "L34_proj_mc_from_glib": lib("L33_proj_update_from_glib", with_l32(None, _mc_from_glib)),
    # the original server ignored newName=PLC_3 (TIA named the device PLC_2); the current one applies it, so it is left
    # out to keep the content of the sample (calls.json keeps the original call text)
    "L35_device_from_mc": lib("L34_proj_mc_from_glib", pl(0, "masterCopyInstantiate", masterCopyName="PLC_1", targetKind="Device")),
    # --- HMI library types (made in the TIA user interface, VNC) ---
    "L37_hmi_lib_types": lib("101_hmi_base", pause(NOTE_L37), pl(None, "typeVersionRelease", typeName="DynamicSVG type_1")),
    "L38_hmi_lib_type_content": lib("L37_hmi_lib_types", pause(NOTE_L38)),
    "L39_hmi_lib_textlist_type": lib("L38_hmi_lib_type_content", pause(NOTE_L39)),
    "L40_hmi_lib_graphiclist_type": lib("L39_hmi_lib_textlist_type", pause(NOTE_L40A),
                                        pl(None, "typeVersionRelease", typeName="Graphic type_1"), pause(NOTE_L40B)),
}

MANUAL = {
    "L37_hmi_lib_types": "HMI library types created in the TIA user interface (VNC): " + NOTE_L37,
    "L38_hmi_lib_type_content": "script module type content typed in the TIA user interface (VNC): " + NOTE_L38,
    "L39_hmi_lib_textlist_type": "text list type created in the TIA user interface (VNC): " + NOTE_L39,
    "L40_hmi_lib_graphiclist_type": "graphic type and graphic list type created in the TIA user interface (VNC): "
                                    + NOTE_L40A + " / " + NOTE_L40B,
}
