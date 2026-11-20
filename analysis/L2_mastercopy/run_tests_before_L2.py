"""L5 test: run library.extract_library over all samples (project folders and .zap21), check expectations,
write RESULTS.md and 021_library.json next to this script.
Usage: python run_tests.py [samples dir]"""
import json
import os
import sys
import time
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import library  # noqa: E402
from openpedata.project import Project  # noqa: E402  (path set by library)


def samples(root):
    out = []
    for n in sorted(os.listdir(root)):
        p = os.path.join(root, n)
        if os.path.isdir(p) and os.path.isfile(os.path.join(p, "System", "PEData.plf")):
            out.append(p)
        elif n.lower().endswith(".zap21"):
            out.append(p)
    return out


def truth_dir(path):
    base = os.path.basename(path)
    if base.lower().endswith(".zap21"):
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
    got = {t["name"]: t for t in L["types"]}
    truth = load_json(os.path.join(tdir, "typesList.json"))["items"]
    if sorted(got) != sorted(t["name"] for t in truth):
        fails.append("types %s, expected %s" % (sorted(got), sorted(t["name"] for t in truth)))
    for tt in truth:
        t = got.get(tt["name"])
        if t is None:
            continue
        for a, b, what in ((t["typeGuid"], tt["guid"], "guid"), (t["opennessKind"], tt["kind"], "kind"),
                           (t["folderPath"], tt["folderPath"], "folderPath"),
                           ([v["version"] for v in t["versions"]], [v["version"] for v in tt["versions"]], "versions")):
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
    # or a dict {"<type>" | "version <x>" | "all": result}
    inst = {}
    for i in d["instances"]:
        for x in i["instanceOf"]:
            inst.setdefault(x["typeName"], []).append((i["name"], x["version"]))
    for fn in sorted(os.listdir(tdir)):
        if not (fn.startswith("instancesList") and fn.endswith(".json")):
            continue
        data = load_json(os.path.join(tdir, fn))
        single = fn[len("instancesList_"):-5] if fn.startswith("instancesList_") else None
        groups = {"all": data} if "items" in data else data
        for key, res in groups.items():
            tname = single or (key if not key.startswith("version ") and key != "all" else None)
            ver = key[len("version "):] if key.startswith("version ") else None
            exp = sorted(x["instanceName"] for x in res["items"])
            have = sorted(nm for nm, v in inst.get(tname, []) if ver is None or v == ver)
            n += 1
            if have != exp:
                fails.append("instances of %s%s: %s, expected %s" % (tname, " " + ver if ver else "", have, exp))
    return fails, n


def check(name, d, path=None):
    """List of failed expectations."""
    fails = []
    libs = d["libraries"]
    if len(libs) != 1:
        return ["%d libraries instead of 1" % len(libs)]
    L = libs[0]
    if L["libraryType"] != "ProjectLib":
        fails.append("libraryType %s" % L["libraryType"])
    subs = [f["subtype"] for f in L["systemFolders"]]
    if sorted(subs) != sorted(library.SYSTEM_FOLDER_ORDER):
        fails.append("system folders %s" % subs)
    if any(f["folders"] for f in L["systemFolders"] if f["subtype"] != "LibrarySystemTypeSystemFolder"):
        fails.append("unexpected sub folders")
    tdir = truth_dir(path) if path else None
    if tdir and os.path.isfile(os.path.join(tdir, "typesList.json")):
        f2, n = check_types(d, tdir)
        fails += f2
        check.compared = n
        tf = [f for f in L["systemFolders"] if f["subtype"] == "LibraryTypeSystemFolder"][0]
        if sorted(e["name"] for e in tf["elements"]) != sorted(t["name"] for t in L["types"]):
            fails.append("type folder elements %s" % tf["elements"])
        if d.get("unassigned"):
            fails.append("unassigned library objects")
        if name.startswith("L09_type_depends"):
            fb = [t for t in L["types"] if t["name"] == "FB_Motor"]
            dep = fb and [(x.get("typeName"), x.get("version")) for x in fb[0]["versions"][0]["dependsOn"]]
            if dep != [("UDT_Motor", "0.0.1")]:
                fails.append("FB_Motor dependsOn %s" % dep)
        if L["masterCopies"]:
            fails.append("unexpected master copies")
        return fails
    check.compared = 0
    # without typesList truth: no user types / versions / instances; system types (instruction types, CoreLibraryType)
    # may exist, but only in LibrarySystemTypeSystemFolder (with GUID-named sub folders there)
    if [t for t in L["types"] if t["category"] != "system" or t["systemFolder"] != "LibrarySystemTypeSystemFolder"] \
            or d["instances"] or d.get("unassigned") or d["statistics"]["versions"]:
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
        if mcs or any(f["elements"] for f in L["systemFolders"]):
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
    rows, all_ok, details, l1 = [], True, [], []
    for s in samples(root):
        name = os.path.basename(s)
        t0 = time.time()
        try:
            p = Project(s)
            d = library.extract_library(p)
            json.dumps(d, default=str)
            fails = check(name, d, s)
            smoke = smoke_type_paths(p)
            st = d["statistics"]
            hash_err = sum(not r.hash_ok for r in p.records)
            L = d["libraries"][0] if d["libraries"] else {}
            mcs = ", ".join("%s (%s)" % (m["name"], "; ".join("%s %s" % (c["type"], c["name"]) for c in m["contents"]))
                            for m in L.get("masterCopies", []))
            types = ", ".join("%s [%s]" % (t["name"], ", ".join("%s %s%s" % (v["version"], v["state"], "*" if v["isDefault"] else "")
                                                               for v in t["versions"])) for t in L.get("types", [])
                              if t["category"] == "user")
            nsys = sum(t["category"] == "system" for t in L.get("types", []))
            if nsys:
                types += (", " if types else "") + "%d system types" % nsys
            rows.append("| %s | %s | %s | %s | %d | %d | %d | %s | %d | %s | %.1f s |" % (
                name, "ok" if not fails else "ERROR: " + "; ".join(fails), check.compared or "–",
                mcs or "–", st["types"], st["versions"], len(d["instances"]), types or "–",
                st["objectsInsideVersions"], "%d/%d" % (smoke, hash_err), time.time() - t0))
            all_ok &= not fails
            if name == "021_mastercopy":
                with open(os.path.join(HERE, "021_library.json"), "w", encoding="utf-8") as f:
                    json.dump(library.extract_library(p, keys=True), f, indent=1, ensure_ascii=False, default=str)
                details.append("```\n" + library.to_text(d) + "\n```")
            if name.startswith("L") and not name.endswith(".zap21"):
                l1.append("### %s\n\n```\n%s\n```" % (name, library.to_text(d)))
                if name == "L09_type_depends":
                    with open(os.path.join(HERE, "L09_library.json"), "w", encoding="utf-8") as f:
                        json.dump(library.extract_library(p, keys=True), f, indent=1, ensure_ascii=False, default=str)
        except Exception:
            all_ok = False
            rows.append("| %s | EXCEPTION | | | | | | | | | |" % name)
            details.append("%s:\n```\n%s```" % (name, traceback.format_exc()))
    lines = ["# L5 – test result library.py (as of L1)", "",
             "Run: `python analysis\L5_library\run_tests.py` over all samples (project folders with "
             "System\PEData.plf and .zap21) in `samples\`, hash check enabled.", "",
             "**Overall: %s** (%d samples)" % ("all expectations met, no exception" if all_ok else "ERROR", len(rows)), "",
             "Expectation: exactly one project library (ProjectLib) with the five system folders, no subfolders. "
             "Samples with `<sample>_truth\typesList.json` (L01–L09, folder and .zap21): types, TypeGuid, Openness kind, "
             "folder path, version list, per version State, isDefault, author and FinalizationTime (= `modified` of "
             "typesList, to microseconds) as in the ground truth; instances (name, for `version x` also the "
             "linked version) as `instancesList`; type folder elements = types; L09: FB_Motor 1.2.3 depends on "
             "UDT_Motor 0.0.1. 021_mastercopy: exactly the master copy FB_Empty (as before). All others: "
             "empty library without types, versions and instances.", "",
             "| Sample | Check | compared values | Master copies (contents) | Types | Versions | Instances | "
             "Types [Versions, * = default] | Objects in versions (copies) | Smoke test type paths / hash errors | Time |",
             "|---|---|---|---|---|---|---|---|---|---|---|"] + rows + [
             "", "Column “Smoke test”: number of objects to which `lib_type()` and `version()` were applied on a trial basis "
             "(without exception, checks missing sets and relations).", "",
             "## Text output L samples", ""] + l1 + ["", "## Text output 021_mastercopy", ""] + details + ["",
             "The complete JSON output for 021 and L09 (with record keys, `--keys`) is in `021_library.json` "
             "and `L09_library.json`."]
    with open(os.path.join(HERE, "RESULTS.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())
