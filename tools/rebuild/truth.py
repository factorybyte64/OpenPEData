"""Ground truth regeneration: for every file of the old `samples\\<sample>_truth\\` folder a generator writes the same
file for the rebuilt sample (same MCP call as when the sample was created). Generators are matched on the path relative
to the truth folder (forward slashes). A recipe may add files with `truth_extra` (list of relative paths) or replace
the generators with its own `truth` callable."""
import json
import os
import re
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SOURCES = os.path.join(ROOT, "tools", "sources")


def _w(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        f.write(text)


def _raw(ctx, tool, ok_errors=False, **args):
    """Truth call as the original scripts made it: without path (the only open project is used)."""
    args = {k: v for k, v in args.items() if v is not None}
    saved, ctx.ap_hidden = ctx.ap, True
    ctx.ap = None
    try:
        return ctx.call(tool, ok_errors=ok_errors, **args)
    finally:
        ctx.ap = saved


def _plc_devices(ctx):
    d = json.loads(_raw(ctx, "TiaDevice", operation="list"))
    return [x["name"] for x in d["items"] if x.get("softwareKind") == "PlcSoftware"]


# --- Save-Truth (tools\Truth.ps1) -------------------------------------------------------------------------------
def save_truth_device(ctx, out, dn, files):
    """Save-Truth for one PLC device; writes only what the old truth has below <dn>/."""
    dd = os.path.join(out, dn)
    tt = _raw(ctx, "TiaPlcSoftware", operation="list", objectKind="tagTable", path=ctx.ap, device=dn)
    if dn + "/tagTables.json" in files:
        _w(os.path.join(dd, "tagTables.json"), tt)
    for t in json.loads(tt)["items"]:
        rel = "%s/tagtables/%s.xml" % (dn, t["name"])
        if rel in files:
            os.makedirs(os.path.join(dd, "tagtables"), exist_ok=True)
            _raw(ctx, "TiaPlcSoftware", operation="export", objectKind="tagTable", path=ctx.ap, device=dn, name=t["name"],
                 groupPath=t.get("groupPath") or None, targetPath=os.path.join(dd, "tagtables", t["name"] + ".xml"))
    bl = _raw(ctx, "TiaPlcSoftware", operation="list", objectKind="block", path=ctx.ap, device=dn)
    if dn + "/blocks.json" in files:
        _w(os.path.join(dd, "blocks.json"), bl)
    ty = _raw(ctx, "TiaPlcSoftware", operation="list", objectKind="type", path=ctx.ap, device=dn)
    if dn + "/types.json" in files:
        _w(os.path.join(dd, "types.json"), ty)
    exp = [("block", b) for b in json.loads(bl)["items"] if "DB" in (b.get("blockType") or "")]
    exp += [("type", t) for t in json.loads(ty)["items"]]
    errs = []
    for kind, o in exp:
        xml = "%s/blocks/%s.xml" % (dn, o["name"])
        sdj = "%s/blocks/%s.sd.json" % (dn, o["name"])
        os.makedirs(os.path.join(dd, "blocks"), exist_ok=True)
        if xml in files or (dn + "/blocks/_export_errors.txt") in files:
            try:
                _raw(ctx, "TiaPlcSoftware", operation="export", objectKind=kind, path=ctx.ap, device=dn, name=o["name"],
                     groupPath=o.get("groupPath") or None, targetPath=os.path.join(dd, "blocks", o["name"] + ".xml"))
            except Exception as e:  # noqa: BLE001
                errs.append("export %s failed: %s" % (o["name"], e))
        if sdj in files:
            try:
                _w(os.path.join(dd, "blocks", o["name"] + ".sd.json"),
                   _raw(ctx, "TiaPlcSoftware", operation="documentGet", objectKind=kind, path=ctx.ap, device=dn, name=o["name"]))
            except Exception:  # noqa: BLE001
                pass
    if errs:
        _w(os.path.join(dd, "blocks", "_export_errors.txt"), "\n".join(errs) + "\n")
    done = {dn + "/tagTables.json", dn + "/blocks.json", dn + "/types.json"}
    if errs:
        done.add(dn + "/blocks/_export_errors.txt")
    # only what was written; other files below <dn>/ (e.g. FB/FC exports, memberGet) are left to the generators
    done |= {f for f in files if (f.startswith(dn + "/tagtables/") or re.match(re.escape(dn) + r"/blocks/[^/]+\.(xml|sd\.json)$", f))
             and os.path.exists(os.path.join(out, f))}
    return done


def save_truth(ctx, out, files):
    done = set()
    if "project.json" in files:
        _w(os.path.join(out, "project.json"), _raw(ctx, "TiaProject", operation="list")); done.add("project.json")
    if "devices.json" in files:
        _w(os.path.join(out, "devices.json"), _raw(ctx, "TiaDevice", operation="list", path=ctx.ap)); done.add("devices.json")
    if "groups.json" in files:
        _w(os.path.join(out, "groups.json"), _raw(ctx, "TiaDevice", operation="groupList", path=ctx.ap)); done.add("groups.json")
    for dn in _plc_devices(ctx):
        if any(f.startswith(dn + "/") for f in files):
            done |= save_truth_device(ctx, out, dn, files)
    return done


# --- single files -------------------------------------------------------------------------------------------------
def g_grouplist(ctx, out, rel):
    _w(os.path.join(out, rel), _raw(ctx, "TiaDevice", operation="groupList"))


def g_device_details(ctx, out, rel):
    dn = re.match(r"device_(.+)_details\.json$", os.path.basename(rel)).group(1)
    _w(os.path.join(out, rel), _raw(ctx, "TiaDevice", operation="getDetails", device=dn))


def g_hardware_aml(ctx, out, rel):
    t = _raw(ctx, "TiaCax", operation="export", targetPath=os.path.join(out, rel))
    return t


def g_toplevel_tagtable_xml(ctx, out, rel):
    name = rel[:-4]
    _raw(ctx, "TiaPlcSoftware", operation="export", objectKind="tagTable", name=name, targetPath=os.path.join(out, rel))


def g_tagtable_get(ctx, out, rel):
    name = rel[:-len(".get.json")]
    _w(os.path.join(out, rel), _raw(ctx, "TiaPlcSoftware", operation="get", objectKind="tagTable", name=name))


def g_blocks_json(ctx, out, rel):
    _w(os.path.join(out, rel), _raw(ctx, "TiaPlcSoftware", operation="list", objectKind="block"))


def g_block_attributes(ctx, out, rel):
    name = rel[:-len(".attributes.json")]
    _w(os.path.join(out, rel), _raw(ctx, "TiaPlcSoftware", operation="attributeGet", objectKind="block", name=name))


def g_block_sd(ctx, out, rel):
    name = rel[:-len(".sd.json")]
    _w(os.path.join(out, rel), _raw(ctx, "TiaPlcSoftware", operation="documentGet", objectKind="block", name=name))


def g_copy_old(ctx, out, rel):
    """Input documents (not Openness output): copied unchanged."""
    dst = os.path.join(out, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copyfile(os.path.join(ctx.old_truth, rel), dst)


# --- Z1.2 PLC truth (samples 041-058: alarms, text lists, watch tables, units, TOs, OPC UA, ProDiag) -----------------
def _jd(txt):
    """Parse a result; error texts that are no JSON are kept as {"raw": text}."""
    try:
        return json.loads(txt)
    except ValueError:
        return {"raw": txt}


def _dump(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(json.dumps(obj, ensure_ascii=False))


def g_change(ctx, out, rel):
    """change.json: the recorded change calls of the recipe (rhelp.chg) with arguments and result."""
    _dump(os.path.join(out, rel), getattr(ctx, "changes", []))


def _export_pair(op, data_file, json_file, **extra):
    """Program service exports (XLSX/DAT): the result of the call goes to <json_file>, the file to <data_file>; errors
    (e.g. "no exportable alarm") are part of the truth."""
    def g(ctx, out, rel):
        if os.path.exists(os.path.join(out, json_file)):
            return
        txt = _raw(ctx, "TiaPlcSoftware", ok_errors=True, operation=op, objectKind="block",
                   targetPath=os.path.join(out, data_file), **extra)
        _dump(os.path.join(out, json_file), _jd(txt))
    return g


def _plain(tool, **a):
    def g(ctx, out, rel):
        _w(os.path.join(out, rel), _raw(ctx, tool, **a))
    return g


def g_watch_get(ctx, out, rel):
    _w(os.path.join(out, rel), _raw(ctx, "TiaPlcSoftware", operation="get", objectKind="watchTable",
                                    name=re.match(r"watchTable_(.+)\.json$", rel).group(1)))


def g_watch_xml(ctx, out, rel):
    _raw(ctx, "TiaPlcSoftware", operation="export", objectKind="watchTable", name=os.path.basename(rel)[:-4],
         targetPath=os.path.join(out, rel))


def g_to_get(ctx, out, rel):
    _w(os.path.join(out, rel), _raw(ctx, "TiaPlcSoftware", operation="get", objectKind="technologyObject",
                                    name=re.match(r"to_(.+)\.json$", rel).group(1)))


GENERATORS_Z12 = [
    (r"change\.json", g_change),
    (r"alarmClasses\.dat|alarmClassExport\.json", _export_pair("alarmClassExport", "alarmClasses.dat", "alarmClassExport.json")),
    (r"alarmTexts\.xlsx|alarmTextExport\.json", _export_pair("alarmTextExport", "alarmTexts.xlsx", "alarmTextExport.json")),
    (r"alarmTextLists\.xlsx|alarmTextListExport\.json",
     _export_pair("alarmTextListExport", "alarmTextLists.xlsx", "alarmTextListExport.json")),
    (r"supervisions\.xlsx|supervisionExport\.json", _export_pair("supervisionExport", "supervisions.xlsx", "supervisionExport.json")),
    (r"list_technologyObject\.json", _plain("TiaPlcSoftware", operation="list", objectKind="technologyObject")),
    (r"list_watchTable\.json", _plain("TiaPlcSoftware", operation="list", objectKind="watchTable")),
    (r"units\.json", _plain("TiaPlcSoftware", operation="unitList", objectKind="block")),
    (r"opcUaInterfaces\.json|opcUaInterfaceList\.json", _plain("TiaPlcSoftware", operation="opcUaInterfaceList", objectKind="block")),
    (r"watchTable_[^/]+\.json", g_watch_get),
    (r"watchtables/[^/]+\.xml", g_watch_xml),
    (r"to_[^/]+\.json", g_to_get),
]

GENERATORS = GENERATORS_Z12 + [
    (r"groupList\.json", g_grouplist),
    (r"device_[^/]+_details\.json", g_device_details),
    (r"hardware\.aml", g_hardware_aml),
    (r"[^/]+\.input\.s7dcl", g_copy_old),
    (r"input/.+", g_copy_old),
    (r"(Default tag table|TagTable_\d+)\.xml", g_toplevel_tagtable_xml),
    (r"TagTable_\d+\.get\.json", g_tagtable_get),
    (r"blocks\.json", g_blocks_json),
    (r"[^/]+\.attributes\.json", g_block_attributes),
    (r"[^/]+\.sd\.json", g_block_sd),
]


def old_files(old):
    out = []
    for dp, _, fns in os.walk(old):
        for fn in fns:
            out.append(os.path.relpath(os.path.join(dp, fn), old).replace("\\", "/"))
    return sorted(out)


def make(ctx):
    rec = ctx.rec
    if callable(rec.get("truth")):
        rec["truth"](ctx)
        ctx.truth_missing = []
        return
    files = set(old_files(ctx.old_truth)) if os.path.isdir(ctx.old_truth) else set()
    files |= set(rec.get("truth_extra", []))
    if not files:
        ctx.truth_missing = []
        return
    os.makedirs(ctx.tdir, exist_ok=True)
    done = set()
    # Save-Truth layout (project/devices/groups + per device folders)
    tops = {f.split("/")[0] for f in files if "/" in f}
    if "devices.json" in files or (tops and tops & set(_plc_devices(ctx))):
        done |= save_truth(ctx, ctx.tdir, files)
    missing = []
    for rel in sorted(files - done):
        for pat, gen in rec.get("generators", []) + GENERATORS:
            if re.fullmatch(pat, rel):
                gen(ctx, ctx.tdir, rel)
                break
        else:
            missing.append(rel)
    ctx.truth_missing = missing
    if missing:
        _w(os.path.join(ctx.out, ctx.name + "_truth_missing.txt"), "\n".join(missing) + "\n")
