"""Read-only gap scan of a TIA V21 project (folder, .ap21 or .zap21) with the OpenPEData parser.
python tools/scan_project.py <project> [--json | --brief]

Counts: objects (latest versions), deleted, records of types the metamodel does not know, objects whose attribute blocks do not decode
cleanly (grouped by type/set/message), objects with an unusable layout, then walks the model output for "unknown*" keys (tokens, ident
kinds, parts) and "error" entries and reports exceptions of the model build. Nothing is written; the project is only read."""
import collections
import json
import os
import sys
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "parser"))
from openpedata import verify  # noqa: E402
from openpedata.model import Model  # noqa: E402
from openpedata.project import Project  # noqa: E402


def walk(o, path, found):
    if isinstance(o, dict):
        for k, v in o.items():
            if k.startswith("unknown") and v:
                found["unknown:" + k].update(v if isinstance(v, list) else [str(v)])
            if k == "error" and v:
                found["error"].append("%s: %s" % (path, v))
            walk(v, path + "/" + str(k), found)
    elif isinstance(o, list):
        for i, v in enumerate(o):
            walk(v, path + "[%d]" % i, found)


def scan(path):
    res = {"path": path}
    try:
        rep = verify.verify_project(path)
        res["verify"] = {"ok": rep.get("ok"), "problems": rep.get("problems", [])[:5]}
        for f in rep.get("files", []):
            res["verify"].setdefault("records", 0)
            res["verify"]["records"] += f.get("records", 0)
    except Exception as e:  # noqa: BLE001
        res["verify"] = {"exception": repr(e)}
    try:
        p = Project(path)
    except Exception as e:  # noqa: BLE001
        res["projectException"] = repr(e)
        return res
    mm = p.mm
    res["objects"] = len(p.objects)
    res["deleted"] = len(p.deleted)
    unknown_types = collections.Counter()
    for k, r in p.latest.items():
        if not r.is_store and r.type_id not in mm.types:
            unknown_types["%04x:%04x" % (r.type_id & 0xFFFF, r.type_id >> 16)] += 1
    res["unknownTypeRecords"] = dict(unknown_types)
    bad_layout, probs = [], collections.Counter()
    objs_with_problems = 0
    types = collections.Counter()
    for k, o in p.objects.items():
        types[p.type_name(k)] += 1
        if not o.layout_ok:
            bad_layout.append(k)
            continue
        try:
            _, problems = p.attributes(k)
        except Exception as e:  # noqa: BLE001
            probs["EXC %s %s" % (p.type_name(k), repr(e)[:80])] += 1
            objs_with_problems += 1
            continue
        if problems:
            objs_with_problems += 1
            for s, msgs in problems.items():
                for m in msgs:
                    probs["%s / %s: %s" % (p.type_name(k), s, m.split(" at ")[0][:60])] += 1
    res["objectsWithDecodeProblem"] = objs_with_problems
    res["objectsBadLayout"] = len(bad_layout)
    res["decodeProblems"] = probs.most_common(15)
    res["types"] = types.most_common(12)
    found = {"error": [], "unknown": None}
    found = collections.defaultdict(lambda: set())
    found["error"] = []
    try:
        d = Model(path).to_dict()
        walk(d, "", found)
        pr = d["project"]
        res["devices"] = sum(len(g.get("devices", [])) for g in pr.get("deviceGroups", [])) + len(pr.get("devices", []))
    except Exception:  # noqa: BLE001
        res["modelException"] = traceback.format_exc().splitlines()[-3:]
    res["unknown"] = {k: sorted(v)[:20] for k, v in found.items() if k.startswith("unknown")}
    res["errors"] = found["error"][:20]
    return res


def main(argv):
    out = scan(argv[0])
    if "--brief" in argv:
        print("%s verify=%s objects=%s deleted=%s unknownTypes=%s decodeProblemObjects=%s badLayout=%s models=%s unknown=%s errors=%s %s" % (
            os.path.basename(argv[0]), (out.get("verify") or {}).get("ok"), out.get("objects"), out.get("deleted"), len(out.get("unknownTypeRecords", {})),
            out.get("objectsWithDecodeProblem"), out.get("objectsBadLayout"), "EXC" if out.get("modelException") or out.get("projectException") else "ok",
            sum(len(v) for v in out.get("unknown", {}).values()), len(out.get("errors", [])), (out.get("decodeProblems") or [""])[:1]))
        return
    if "--json" in argv:
        print(json.dumps(out, indent=1, ensure_ascii=False, default=list))
        return
    for k, v in out.items():
        print("%-26s %s" % (k, v if not isinstance(v, (list, dict)) or len(str(v)) < 160 else ""))
        if isinstance(v, (list, dict)) and len(str(v)) >= 160:
            items = v.items() if isinstance(v, dict) else enumerate(v)
            for a, b in items:
                print("    %s %s" % (a, b))


if __name__ == "__main__":
    main(sys.argv[1:])
