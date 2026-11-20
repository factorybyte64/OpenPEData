"""Library test: openpedata.library (project library: master copies, types / versions, typed instances and their
version state) over every sample (project or global library folder, .zap21 and .zal21) against the Openness ground truth of the library
samples (021, L01-L21: <sample>_truth/typesList.json, instancesList*.json, <name>.<device>.attributes.json,
masterCopiesList.json) and the expectations of steps L1, L2 and L4 (analysis/L1_types, L2_mastercopy,
L4_instances REPORT.md). Every other sample: one empty project library without user types, versions, instances.
Also checks that Model.to_dict() carries the same view under "library".

Usage: python parser/tests/test_library.py [samples dir]     exit code 0 = all checks passed
Result table: tests\\RESULTS_LIBRARY.md.
"""
import json
import os
import sys
import time
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from openpedata import library  # noqa: E402
from openpedata.model import Model  # noqa: E402
from openpedata.project import Project  # noqa: E402


# HMI library types (script module, dynamic SVG, text list, graphic list, graphic): the expectations below know PLC
# types only. These samples are read here (no exception, hashes, type paths); their types, versions and contents are
# checked by the .NET test program (dotnet\tests\OpenPEData.Tests\LibraryHmiTypeTests.cs).
DOTNET_ONLY = {"L37_hmi_lib_types", "L38_hmi_lib_type_content", "L39_hmi_lib_textlist_type",
               "L40_hmi_lib_graphiclist_type"}


def samples(root):
    out = []
    for n in sorted(os.listdir(root)):
        p = os.path.join(root, n)
        if os.path.isdir(p) and os.path.isfile(os.path.join(p, "System", "PEData.plf")):
            out.append(p)
        elif n.lower().endswith((".zap21", ".zal21")):
            out.append(p)
    return out


def truth_dir(path):
    base = os.path.basename(path)
    if base.lower().endswith((".zap21", ".zal21")):
        base = base[:-6]
    return os.path.join(os.path.dirname(path), base + "_truth")


def load_json(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def check_types(d, tdir):
    """L1: compare types / versions / instances with the Openness ground truth (typesList.json, instancesList*.json)
    of the sample. Returns (failed expectations, number of compared values)."""
    fails, n = [], 0
    L = d["libraries"][0]
    # Openness typesList walks ProjectLibrary.TypeFolder only; system types (instruction types TIA puts into
    # LibrarySystemTypeSystemFolder, F40 demo project) are no Openness LibraryType and are not reachable there,
    # so they stay out of the comparison and are checked for consistency instead
    got = {t["name"]: t for t in L["types"] if t["category"] != "system"}
    truth = load_json(os.path.join(tdir, "typesList.json"))["items"]
    for t in L["types"]:
        if t["category"] != "system":
            continue
        n += 1
        lib = (t["originalLibrary"] or "").split(":")[0]
        # folder path = <original library GUID>/<version id>, version from ICoreLibTypeAttributes, no versions
        if t["systemFolder"] != "LibrarySystemTypeSystemFolder" or t["opennessKind"] is not None or t["versions"]                 or not t["coreLibVersion"] or not t["coreLibState"] or not t["typeObjectId"]                 or t["folderPath"] != "%s/%s" % (lib, (t["versionId"] or "").upper()) or t["name"] in {x["name"] for x in truth}:
            fails.append("system type %s: %s" % (t["name"], {k: t[k] for k in ("systemFolder", "folderPath", "opennessKind",
                         "coreLibVersion", "coreLibState", "typeObjectId", "versionId", "originalLibrary")}))
    if sorted(got) != sorted(t["name"] for t in truth):
        fails.append("types %s, expected %s" % (sorted(got), sorted(t["name"] for t in truth)))
    for tt in truth:
        t = got.get(tt["name"])
        if t is None:
            continue
        # global library typesList (L31+, TiaLibraryContent) has no kind; versions carry their guid
        for a, b, what in ((t["typeGuid"], tt["guid"], "guid"), (t["opennessKind"], tt.get("kind", t["opennessKind"]), "kind"),
                           (t["folderPath"], tt.get("folderPath") or "", "folderPath"),
                           ([v["version"] for v in t["versions"]], [v["version"] for v in tt["versions"]], "versions"),
                           ([v["versionGuid"] for v in t["versions"]], [v.get("guid", w["versionGuid"]) for v, w in zip(tt["versions"], t["versions"])], "version guids")):
            n += 1
            if a != b:
                fails.append("%s %s=%r, expected %r" % (tt["name"], what, a, b))
        # typesList doNotUse = ITypeBaseAttributesInfo.IsObsolete, setForUpdate = not IsSetAsNonUpdateable (F03)
        for a, b, what in ((t["isObsolete"], tt.get("doNotUse"), "doNotUse"),
                           (None if t["isSetAsNonUpdateable"] is None else not t["isSetAsNonUpdateable"],
                            tt.get("setForUpdate"), "setForUpdate")):
            if b is None:
                continue
            n += 1
            if a != b:
                fails.append("%s %s=%r, expected %r" % (tt["name"], what, a, b))
        gv = {v["version"]: v for v in t["versions"]}
        for tv in tt["versions"]:
            v = gv.get(tv["version"])
            if v is None:
                continue
            fin = v["finalizationTime"] if v["finalizationTime"] and not v["finalizationTime"].startswith("0001-") else None
            for a, b, what in ((v["state"], tv["state"], "state"), (v["isDefault"], tv["isDefault"], "isDefault"),
                               (v["author"], tv["author"], "author"),
                               (fin and fin[:26], tv["modified"] and tv["modified"][:26], "finalizationTime/modified")):
                n += 1
                if a != b:
                    fails.append("%s %s %s=%r, expected %r" % (tt["name"], tv["version"], what, a, b))
        if len([v for v in t["versions"] if v["isDefault"]]) > 1:
            fails.append("%s: more than one default version" % tt["name"])
    # instances: instancesList*.json either one Openness result for one type (file name instancesList_<type>.json)
    # or a dict {"<type>" | "version <x>" | "<device> version <x>" (L4) | "all": result}
    inst = {}
    for i in d["instances"]:
        for x in i["instanceOf"]:
            inst.setdefault(x["typeName"], []).append((i["name"], x["version"], i["device"]))
    for fn in sorted(os.listdir(tdir)):
        if not (fn.startswith("instancesList") and fn.endswith(".json")):
            continue
        data = load_json(os.path.join(tdir, fn))
        if "errorCode" in data:
            continue  # Openness / MCP could not list (F03: no IInstanceSearchScope for Unified faceplates)
        single = fn[len("instancesList_"):-5] if fn.startswith("instancesList_") else None
        groups = {"all": data} if "items" in data else data
        for key, res in groups.items():
            dev = None
            if " version " in key:
                dev, key = key.split(" ", 1)
            tname = single or (key if not key.startswith("version ") and key != "all" else None)
            ver = key[len("version "):] if key.startswith("version ") else None
            exp = sorted(x["instanceName"] for x in res["items"])
            have = sorted(nm for nm, v, dv in inst.get(tname, []) if (ver is None or v == ver) and (dev is None or dv == dev))
            n += 1
            if have != exp:
                fails.append("instances of %s%s%s: %s, expected %s" % (tname, " " + ver if ver else "",
                                                                       " on " + dev if dev else "", have, exp))
    # L4: <name>.<device>.attributes.json (Openness attributeGet): CreationDate = instance CreationTime (a discard
    # re-creates the instance, L19)
    by_dev = {(i["name"], i["device"]): i for i in d["instances"]}
    for fn in sorted(os.listdir(tdir)):
        parts = fn.split(".")
        if len(parts) == 4 and parts[2] == "attributes" and parts[3] == "json":
            i = by_dev.get((parts[0], parts[1]))
            want = load_json(os.path.join(tdir, fn))["attributes"].get("CreationDate")
            n += 1
            if i is None or (i["creationTime"] or "")[:26] != (want or "")[:26]:
                fails.append("%s on %s CreationTime %r, expected %r" % (parts[0], parts[1], i and i["creationTime"], want))
    return fails, n


# L4: derived version state per (instance, device): version, usesDefaultVersion, outdated, BlockToVersionElements
L4_STATE = {
    "L16_inst_two_devices": {("UDT_Motor", "PLC_1"): ("0.0.1", True, False, True),
                             ("UDT_Motor", "PLC_2"): ("0.0.1", True, False, False)},
    "L17_inst_outdated": {("UDT_Motor", "PLC_1"): ("1.0.0", True, False, True),
                          ("UDT_Motor", "PLC_2"): ("0.0.1", False, True, False)},
    "L18_inst_edit_outdated": {("UDT_Motor", "PLC_1"): ("1.0.0", False, True, True),
                               ("UDT_Motor", "PLC_2"): ("1.1.0", True, False, True)},
    "L19_version_discarded": {("UDT_Motor", "PLC_1"): ("1.0.0", False, True, False),
                              ("UDT_Motor", "PLC_2"): ("1.1.0", True, False, True)},
    "L20_default_two_devices": {("UDT_Motor", "PLC_1"): ("1.0.0", True, True, True),
                                ("UDT_Motor", "PLC_2"): ("1.1.0", False, False, True)},
    "L21_fb_inst_outdated": {("FB_Typed", "PLC_1"): ("1.0.0", True, False, True),
                             ("FB_Typed", "PLC_2"): ("0.0.1", False, True, False)},
    # L4 addendum (MCP updateProject / typeVersionDelete / typeSet / folders / Override): L22 has no instances left
    "L22_update_removes_unused": {},
    "L23_inst_called": {("FB_Typed", "PLC_1"): ("1.0.0", True, False, True),
                        ("FB_Typed", "PLC_2"): ("0.0.1", False, True, True)},
    "L24_inst_update_fb": {("FB_Typed", "PLC_1"): ("1.0.0", True, False, True),
                           ("FB_Typed", "PLC_2"): ("1.0.0", True, False, True)},
    "L25_version_deleted": {("FB_Typed", "PLC_1"): ("1.0.0", True, False, True),
                            ("FB_Typed", "PLC_2"): ("1.0.0", True, False, True)},
    "L26_type_obsolete": {("FB_Typed", "PLC_1"): ("1.0.0", True, False, True),
                          ("FB_Typed", "PLC_2"): ("1.0.0", True, False, True)},
    "L27_inst_update_udt": {("UDT_Motor", "PLC_1"): ("1.0.0", True, False, True),
                            ("UDT_Motor", "PLC_2"): ("1.0.0", True, False, True)},
    "L08_type_in_folder": {("UDT_Motor", "PLC_1"): ("0.0.1", True, False, True),
                           ("FC_Typed", "PLC_1"): ("1.0.0", True, False, True)},
    "L29_version_content": {("UDT_Motor", "PLC_1"): ("1.0.0", True, False, True)},
    # L6: project library updated from the global library (1.0.0 imported, instance stays at 0.0.1), device from a
    # device master copy (typed UDT at 1.0.0 as in the source device); the global libraries have no instances
    "L31_glib_type": {}, "L32_glib_mc_device": {}, "L36_glib_mc_deleted": {},
    "L33_proj_update_from_glib": {("UDT_Motor", "PLC_1"): ("0.0.1", False, True, True)},
    "L34_proj_mc_from_glib": {("UDT_Motor", "PLC_1"): ("0.0.1", False, True, True)},
    "L35_device_from_mc": {("UDT_Motor", "PLC_1"): ("0.0.1", False, True, True),
                           ("UDT_Motor", "PLC_2"): ("1.0.0", True, False, True)},
    "L03_udt_type_v2": {("UDT_Motor", "PLC_1"): ("1.0.0", True, False, True)},
    "L04_default_changed": {("UDT_Motor", "PLC_1"): ("1.0.0", False, False, True)},
}


L4_PREVIOUS = {"L24_inst_update_fb": {("FB_Typed", "PLC_2"): "0.0.1"},
               "L27_inst_update_udt": {("UDT_Motor", "PLC_2"): "0.0.1"}}


def check_instance_state(name, d):
    """L4: version state of every instance: both stored version references (CurrentLibraryInterface name, expando
    UserTypeVersionGUID of the own interface root) agree with IsInstanceOf; derived flags per sample (L4_STATE)."""
    fails, n = [], 0
    base = name[:-6] if name.endswith((".zap21", ".zal21")) else name
    for i in d["instances"]:
        vs = i["versionState"]
        itf = vs["interface"]
        n += 1
        # PLC instances only: HMI instances (faceplate containers, F05) have no block interface roots
        plc = i["type"] in ("CodeBlockData", "UserTypeData")
        if plc and vs["state"] == "Committed" and (itf["interfaceVersionGuidMatches"] is not True or itf["libraryInterfaceMatches"] is not True):
            fails.append("%s on %s: interface version references %s" % (i["name"], i["device"], itf))
        if vs["state"] == "InWork" and vs["outdated"] is not None:
            fails.append("%s on %s: InWork instance with outdated=%r" % (i["name"], i["device"], vs["outdated"]))
    # L4 addendum: after updateProject the re-created instance keeps the old interface root ('xBIVE:'), whose
    # version GUID names the version it was updated from
    for (nm, dev), prev in L4_PREVIOUS.get(base, {}).items():
        i = next((x for x in d["instances"] if (x["name"], x["device"]) == (nm, dev)), None)
        n += 1
        got_prev = i and (i["versionState"].get("previousVersion"),
                          (i["versionState"]["interface"].get("previousInterfaceRoot") or "")[:5])
        if got_prev != (prev, "xBIVE"):
            fails.append("%s on %s previous version %r, expected %r" % (nm, dev, got_prev, (prev, "xBIVE")))
    exp = L4_STATE.get(base)
    if exp is not None:
        got = {(i["name"], i["device"]): (i["versionState"]["version"], i["versionState"]["usesDefaultVersion"],
                                          i["versionState"]["outdated"], i["versionState"]["interface"]["hasBlockToVersionElements"])
               for i in d["instances"]}
        n += len(exp)
        if got != exp:
            fails.append("version state %s, expected %s" % (got, exp))
        out_exp = sum(1 for v in exp.values() if v[2])
        n += 1
        if sum(t["outdatedInstances"] for t in d["libraries"][0]["types"]) != out_exp:
            fails.append("outdatedInstances != %d" % out_exp)
    return fails, n


# L2: Openness masterCopiesList content type -> IGeneralBlockSourceData.BlockType of the copied block
OPENNESS_BLOCK = {"FB": "FB", "FC": "FC", "GlobalDB": "DB", "OB": "OB"}
# L2: objects a master copy only references (name stubs), per sample prefix
MC_REFERENCES = {"L10_mc_fc": {"FC_Scl": [("CodeBlockData", "FB_Iface"), ("DataBlockData", "FB_Iface_DB")]},
                 "L11_mc_fb_udt": {"FB_Iface": [("UserTypeData", "UDT_Motor")]},
                 "L12_mc_two": {"FC_Iface": [("UserTypeData", "UDT_Motor")], "FB_Iface": [("UserTypeData", "UDT_Motor")]},
                 "L13_mc_instantiate": {"FB_Iface": [("UserTypeData", "UDT_Motor")]},
                 "L14_mc_delete": {"FB_Iface": [("UserTypeData", "UDT_Motor")]},
                 "L15_mc_db": {"DB_Motor": [("UserTypeData", "UDT_Motor")]},
                 "L28_mc_in_folder": {"FC_Scl": [("CodeBlockData", "FB_Iface"), ("DataBlockData", "FB_Iface_DB")]}}
# master copies deleted in the sample folder (tombstones; a .zap21 has none). L28: masterCopyMove copies, then deletes
DELETED_MC = {"L14_mc_delete": 1, "L28_mc_in_folder": 1, "L36_glib_mc_deleted": 1}


def folder_tree(f, prefix=""):
    """(user folder paths, element names) below a library system folder, recursively."""
    paths, elems = [], [e["name"] for e in f["elements"]]
    for sub in f["folders"]:
        pth = prefix + sub["name"]
        paths.append(pth)
        p2, e2 = folder_tree(sub, pth + "/")
        paths += p2
        elems += e2
    return paths, elems


def with_parents(paths):
    out = set()
    for pth in paths:
        parts = pth.split("/") if pth else []
        for i in range(1, len(parts) + 1):
            out.add("/".join(parts[:i]))
    return sorted(out)


def is_global_library(path):
    """L6: a user global library sample (folder with an .al21 file, or a .zal21 archive)."""
    if path.lower().endswith(".zal21"):
        return True
    return os.path.isdir(path) and any(f.lower().endswith(".al21") for f in os.listdir(path))


def check_device_master_copy(m, t):
    """L6: master copy of a whole device (masterCopyCreateFromDevice): subtype Hardware, contents the DeviceData,
    the device as context, one SelectedObject of type ...Device."""
    co = (m["coreObjects"] or {}).get("entries")
    td = (m["source"] or {}).get("typeDescriptors", [])
    return [((m["subtype"], m["state"]), ("Hardware", "Committed"), "subtype/state"),
            ([(c["type"], c["name"]) for c in m["contents"]], [("DeviceData", c["name"]) for c in t["contents"]], "contents"),
            ([(c["type"], c["name"], c["relation"]) for c in m["context"]], [("DeviceData", c["name"], "Devices") for c in t["contents"]], "context"),
            ([(e["objectRole"], e["sourceObjectId"], e["parentObjectId"], e["visualPartIndex"]) for e in co or []],
             [("SelectedObject", 0, -1, 0)], "coreObjects"),
            ([x["typeName"].rsplit(".", 1)[-1] for x in td], ["Device"], "typeDescriptor"),
            (m.get("decodeProblems"), None, "decode problems")]


def check_master_copies(name, d, tdir, has_types=False):
    """L2: compare the master copies with the Openness ground truth masterCopiesList.json (names in Openness order,
    folder, contents name + type) and check the structure found in L2 (CoreObjectData, source scopes, reference
    stubs, frozen interfaces of used PLC data types). Returns (failed expectations, number of compared values)."""
    fails, n = [], 0
    L = d["libraries"][0]
    mcs = L["masterCopies"]
    truth = load_json(os.path.join(tdir, "masterCopiesList.json"))["items"]
    n += 1
    if [m["name"] for m in mcs] != [t["name"] for t in truth]:
        fails.append("master copies %s, expected %s (Openness order)" % ([m["name"] for m in mcs], [t["name"] for t in truth]))
    base = name[:-6] if name.endswith((".zap21", ".zal21")) else name
    refs = MC_REFERENCES.get(base, {})
    for m, t in zip(mcs, truth):
        fp = t["folderPath"]
        exp_folder = ("LibraryMasterCopySystemFolder", fp[len("Master copies/"):] if fp.startswith("Master copies/") else "")             if fp == "Master copies" or fp.startswith("Master copies/") else (None, fp)
        got = [(c["name"], c.get("blockType")) for c in m["contents"]]
        want = [(c["name"], OPENNESS_BLOCK.get(c["type"], c["type"])) for c in t["contents"]]
        co = (m["coreObjects"] or {}).get("entries")
        scopes = {sc["type"]: (sc.get("sourceObject") or {}).get("type") for sc in (m["source"] or {}).get("sourceScopePath", [])}
        td = (m["source"] or {}).get("typeDescriptors", [])
        if all(c["type"] == "DeviceImpl" for c in t["contents"]):
            for a, b, what in [((m["systemFolder"], m["folderPath"]), exp_folder, "folder")] + check_device_master_copy(m, t):
                n += 1
                if a != b:
                    fails.append("%s %s=%r, expected %r" % (m["name"], what, a, b))
            continue
        checks = [((m["systemFolder"], m["folderPath"]), exp_folder, "folder"),
                  (got, want, "contents"),
                  ((m["subtype"], m["state"]), ("IECPL", "Committed"), "subtype/state"),
                  ([(e["objectRole"], e["sourceObjectId"], e["parentObjectId"], e["visualPartIndex"], e["option"]) for e in co or []],
                   [("SelectedObject", 0, -1, 0, None)], "coreObjects"),
                  (m["nonCoreObjects"], 0, "nonCoreObjects"),
                  ([x["typeName"].rsplit(".", 1)[-1] for x in td], [c[1] for c in want], "typeDescriptor"),
                  ((scopes.get("Project"), scopes.get("Target")), ("ProjectData", "S7ControllerTargetData"), "source scopes"),
                  (sorted((c["type"], c["name"]) for c in m["context"]), [("DeviceData", "PLC_1"), ("S7ControllerTargetData", "PLC_1")], "context"),
                  ([(c["type"], c["name"]) for c in m["referencedObjects"]], refs.get(m["name"], []), "referencedObjects"),
                  (sorted(x["name"].split("/")[0] for x in m["frozenInterfaces"]),
                   sorted("_BIVE:" + nm for ty, nm in refs.get(m["name"], []) if ty == "UserTypeData"), "frozenInterfaces"),
                  (m.get("decodeProblems"), None, "decode problems")]
        for a, b, what in checks:
            n += 1
            if a != b:
                fails.append("%s %s=%r, expected %r" % (m["name"], what, a, b))
    mcf = [f for f in L["systemFolders"] if f["subtype"] == "LibraryMasterCopySystemFolder"][0]
    n += 1
    paths, elems = folder_tree(mcf)
    if sorted(elems) != sorted(t["name"] for t in truth):
        fails.append("master copy folder elements %s" % elems)
    n += 1
    want_paths = with_parents(t["folderPath"][len("Master copies/"):] for t in truth if t["folderPath"].startswith("Master copies/"))
    if sorted(paths) != want_paths:
        fails.append("master copy user folders %s, expected %s" % (sorted(paths), want_paths))
    if not has_types and (L["types"] and any(t["category"] != "system" for t in L["types"]) or d["instances"]) or d.get("unassigned"):
        fails.append("unexpected types/instances/unassigned")
    deleted = d["statistics"]["deletedLibraryObjectsHidden"].get("LibraryMasterCopyData", 0)
    n += 1
    # tombstones only exist in the project folder; a .zap21 holds the compacted store (current versions only)
    if deleted != DELETED_MC.get(name, 0):
        fails.append("deleted master copies %d" % deleted)
    return fails, n


def check(name, d, path=None):
    """List of failed expectations."""
    fails = []
    libs = d["libraries"]
    if len(libs) != 1:
        return ["%d libraries instead of 1" % len(libs)]
    L = libs[0]
    want_lib = "GlobalLib" if path and is_global_library(path) else "ProjectLib"
    if L["libraryType"] != want_lib:
        fails.append("libraryType %s, expected %s" % (L["libraryType"], want_lib))
    subs = [f["subtype"] for f in L["systemFolders"]]
    if sorted(subs) != sorted(library.SYSTEM_FOLDER_ORDER):
        fails.append("system folders %s" % subs)
    tdir = truth_dir(path) if path else None
    # user folders (L08, L28): only where the ground truth has types / master copies below the system folder
    allowed = set()
    if tdir and os.path.isfile(os.path.join(tdir, "typesList.json")):
        allowed.add("LibraryTypeSystemFolder") if any(t.get("folderPath") for t in load_json(os.path.join(tdir, "typesList.json"))["items"]) else None
    if tdir and os.path.isfile(os.path.join(tdir, "masterCopiesList.json")):
        allowed.add("LibraryMasterCopySystemFolder") if any(t["folderPath"] != "Master copies" for t in load_json(os.path.join(tdir, "masterCopiesList.json"))["items"]) else None
    if any(f["folders"] for f in L["systemFolders"] if f["subtype"] not in allowed | {"LibrarySystemTypeSystemFolder"}):
        fails.append("unexpected sub folders")
    if tdir and os.path.isdir(tdir) and not os.listdir(tdir):
        check.compared = 0  # sample in preparation: copy of a predecessor, ground truth not yet written
        return fails
    has_types = bool(tdir) and os.path.isfile(os.path.join(tdir, "typesList.json"))
    n_mc = 0
    if tdir and os.path.isfile(os.path.join(tdir, "masterCopiesList.json")):
        f2, n_mc = check_master_copies(name, d, tdir, has_types)
        fails += f2
        if not has_types:
            check.compared = n_mc
            return fails
    if has_types:
        f2, n = check_types(d, tdir)
        f3, n3 = check_instance_state(name, d)
        fails += f2 + f3
        check.compared = n + n3 + n_mc
        tf = [f for f in L["systemFolders"] if f["subtype"] == "LibraryTypeSystemFolder"][0]
        paths, elems = folder_tree(tf)
        if sorted(elems) != sorted(t["name"] for t in L["types"] if t["category"] != "system"):
            fails.append("type folder elements %s" % elems)
        want_paths = with_parents(t["folderPath"] for t in load_json(os.path.join(tdir, "typesList.json"))["items"] if t.get("folderPath"))
        check.compared += 1
        if sorted(paths) != want_paths:
            fails.append("type user folders %s, expected %s" % (sorted(paths), want_paths))
        if d.get("unassigned"):
            fails.append("unassigned library objects")
        if name.startswith("L09_type_depends"):
            fb = [t for t in L["types"] if t["name"] == "FB_Motor"]
            dep = fb and [(x.get("typeName"), x.get("version")) for x in fb[0]["versions"][0]["dependsOn"]]
            if dep != [("UDT_Motor", "0.0.1")]:
                fails.append("FB_Motor dependsOn %s" % dep)
        if L["masterCopies"] and not n_mc:
            fails.append("unexpected master copies")
        return fails
    check.compared = 0
    # without typesList truth: no user types / versions / instances; system types (instruction types, CoreLibraryType)
    # may exist, but only in LibrarySystemTypeSystemFolder (with GUID-named sub folders there)
    # HMI types (faceplates, samples F01/F02 of the Unified threads) may exist without a library truth set
    # HMI UDT library types come with faceplates that use them (F12: interface tag of an HMI UDT)
    hmi_types = [t for t in L["types"] if t["type"] in ("FaceplateLibraryTypeData", "HmiUdtTypeData")]
    if [t for t in L["types"] if t not in hmi_types and (t["category"] != "system" or t["systemFolder"] != "LibrarySystemTypeSystemFolder")] \
            or [i for i in d["instances"] if any(x["typeName"] not in {t["name"] for t in hmi_types} for x in i["instanceOf"])]             or d.get("unassigned") or d["statistics"]["versions"] != sum(len(t["versions"]) for t in hmi_types):
        fails.append("unexpected types/versions/instances")
    mcs = L["masterCopies"]
    if name.startswith("021_mastercopy"):
        if len(mcs) != 1:
            return fails + ["%d master copies instead of 1" % len(mcs)]
        m = mcs[0]
        exp = {"name": "FB_Empty", "systemFolder": "LibraryMasterCopySystemFolder", "folderPath": "",
               "subtype": "IECPL", "state": "Committed"}
        for k, v in exp.items():
            if m[k] != v:
                fails.append("master copy %s=%r, expected %r" % (k, m[k], v))
        if [(c["type"], c["name"]) for c in m["contents"]] != [("CodeBlockData", "FB_Empty")]:
            fails.append("contents %s" % m["contents"])
        if sorted((c["type"], c["name"]) for c in m["context"]) != [("DeviceData", "PLC_1"), ("S7ControllerTargetData", "PLC_1")]:
            fails.append("context %s" % m["context"])
        td = (m["source"] or {}).get("typeDescriptors", [])
        if [(t["typeName"], t["roles"]) for t in td] != [("Siemens.Simatic.Lang.Model.Blocks.CodeBlockData.FB", ["SelectedObject"])]:
            fails.append("source type descriptors %s" % td)
        mcf = [f for f in L["systemFolders"] if f["subtype"] == "LibraryMasterCopySystemFolder"][0]
        if [(e["type"], e["name"]) for e in mcf["elements"]] != [("LibraryMasterCopyData", "FB_Empty")]:
            fails.append("master copy folder elements %s" % mcf["elements"])
        others = [f for f in L["systemFolders"] if f["subtype"] != "LibraryMasterCopySystemFolder" and f["elements"]]
        if others:
            fails.append("elements in other system folders")
    else:
        hmi_names = {t["name"] for t in hmi_types}
        if mcs or any(e["name"] not in hmi_names for f in L["systemFolders"] for e in f["elements"]):
            fails.append("library not empty")
    return fails


def smoke_type_paths(p):
    """Run the type/version code paths on arbitrary objects (not only library objects) to make sure
    they do not raise on missing attribute sets / relations."""
    v = library.LibraryView(p)
    n = 0
    for k in list(p.objects)[:400]:
        v.lib_type(k, True)
        v.version(k, True)
        n += 1
    return n


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "samples")
    rows, all_ok, details = [], True, []
    for s in samples(root):
        name = os.path.basename(s)
        if name == "F40_siemens_demo_bestpractices" and not os.path.isdir(truth_dir(s)):
            continue  # Siemens demo project: sample and truth only local, not in the repo
        t0 = time.time()
        try:
            p = Project(s)
            d = library.extract_library(p)
            json.dumps(d, default=str)
            hmi_types = name.split(".")[0] in DOTNET_ONLY
            fails = [] if hmi_types else check(name, d, s)
            if name.startswith(("021_", "L17_", "L13_")):
                # Model.to_dict() carries the same library view
                md = Model(s).to_dict()
                if json.dumps(md.get("library"), default=str, sort_keys=True) != json.dumps(d, default=str, sort_keys=True):
                    fails.append("Model.to_dict()['library'] differs from extract_library")
            smoke = smoke_type_paths(p)
            st = d["statistics"]
            hash_err = sum(not r.hash_ok for r in p.records)
            if hash_err:
                fails.append("%d hash errors" % hash_err)
            L = d["libraries"][0] if d["libraries"] else {}
            types = ", ".join("%s [%s]" % (t["name"], ", ".join("%s %s%s" % (v["version"], v["state"], "*" if v["isDefault"] else "")
                                                               for v in t["versions"])) for t in L.get("types", [])
                              if t["category"] == "user")
            nsys = sum(t["category"] == "system" for t in L.get("types", []))
            if nsys:
                types += (", " if types else "") + "%d system types" % nsys
            outd = sum(1 for i in d["instances"] if (i.get("versionState") or {}).get("outdated"))
            rows.append("| %s | %s | %s | %d | %d | %d | %d | %d | %s | %.1f s |" % (
                name, ("OK (read only; types checked by the .NET tests)" if hmi_types else "OK") if not fails
                else "ERROR: " + "; ".join(fails), "–" if hmi_types else check.compared or "–",
                st["masterCopies"], st["types"], st["versions"], len(d["instances"]), outd, types or "–", time.time() - t0))
            all_ok &= not fails
            del smoke
        except Exception:
            all_ok = False
            rows.append("| %s | EXCEPTION | | | | | | | | |" % name)
            details.append("%s:\n```\n%s```" % (name, traceback.format_exc()))
    lines = ["# Library tests (openpedata.library against Openness ground truth)", "",
             "**Overall: %s** (%d samples)" % ("all expectations met, no exception" if all_ok else "ERROR", len(rows)), "",
             "| Sample | Result | compared values | Master copies | Types | Versions | Instances | of which outdated | "
             "Types [Versions, * = default] | Time |",
             "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"] + rows + [""] + details
    text = "\n".join(lines) + "\n"
    with open(os.path.join(HERE, "RESULTS_LIBRARY.md"), "w", encoding="utf-8") as f:
        f.write(text)
    print(text)
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())
