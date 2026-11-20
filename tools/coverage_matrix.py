r"""Coverage matrix (GOALS.md Z1.1-Z1.4): which object types of the embedded metamodel occur in the samples,
and which of them reach the parser output. Read-only.

Usage: python -I tools\coverage_matrix.py [samples folder] [--out docs\COVERAGE-data.json] [--cli <OpenPEData.Cli.exe>]
Evaluates every *.zap21 in the folder (folder samples are skipped to avoid double counting).

"emitted" = at least one object of the type appears with its record key in `OpenPEData --json --keys`.
This is a lower bound: objects that the parser only processes inline (texts, idents, interface parts) do not count
as emitted, and an emitted type does not mean that all of its attributes are emitted.
"""
import collections
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.project import Project  # noqa: E402

KEY = re.compile(r'\b([0-9a-f]{4}:[0-9a-f]{4}:[0-9a-f]+)\b')
CLI = None  # --cli <OpenPEData.Cli.exe>: product parser (.NET) instead of the Python reference
ORDER = ["Z1.1", "Z1.2", "Z1.3", "Z1.4", "Infrastructure"]


def goal(pkg, name):
    """Mapping package -> goal from GOALS.md (rough, by package name)."""
    p = pkg.replace("Siemens.", "")
    if ("FaceplateLib" in p or "DL.Library" in p or "Libraries" in p or "ReleaseManagement" in p
            or "CommonServices.Library" in p or name == "LibraryData"):
        return "Z1.4"
    if p.startswith("Simatic.Hmi"):
        return "Z1.3"
    if p.startswith(("Simatic.HwConfiguration", "Simatic.OpcUa", "Simatic.Tech.WebApplication", "Simatic.Safety",
                     "Simatic.SystemDiagnosis", "Automation.Umac", "Simatic.Rse")):
        return "Z1.1"
    if p.startswith(("Simatic.Lang", "Simatic.AlarmServices", "TechTrace", "Automation.DomainModel.TextLists",
                     "Automation.DomainModel.OnlineBackup")):
        return "Z1.2"
    if p == "Automation.DomainModel" and name not in ("ProjectData", "FolderData"):
        return "Z1.2"
    return "Infrastructure"


def main():
    argv = sys.argv[1:]
    args = [a for i, a in enumerate(argv) if not a.startswith("--") and (i == 0 or argv[i - 1] not in ("--out", "--cli"))]
    sdir = args[0] if args else os.path.join(ROOT, "samples")
    out_json = sys.argv[sys.argv.index("--out") + 1] if "--out" in sys.argv else None
    global CLI
    CLI = os.path.abspath(sys.argv[sys.argv.index("--cli") + 1]) if "--cli" in sys.argv else None
    objs, hit, where = collections.Counter(), collections.Counter(), collections.defaultdict(set)
    alltypes, samples = set(), []
    for s in sorted(os.listdir(sdir)):
        if not s.endswith(".zap21") or re.search(r"_\d{8}_\d{4}\.zap21$", s):
            continue
        path = os.path.join(sdir, s)
        try:
            pr = Project(path, verify=False)
        except Exception as e:  # noqa: BLE001
            print("skipped %s: %s" % (s, e), file=sys.stderr)
            continue
        samples.append(s[:-6])
        for t in pr.mm.types.values():
            if t.kind == "ObjectType" and not t.abstract:
                alltypes.add((t.package or "?", t.name))
        cmd = ([CLI] if CLI else [sys.executable, "-I", os.path.join(ROOT, "parser", "OpenPEData.py")]) + [path, "--json", "--keys"]
        res = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
        keys = set(KEY.findall(res.stdout))
        for k in pr.objects:
            lo, hi, _ = k.split(":")
            t = pr.mm.types.get(int(hi, 16) << 16 | int(lo, 16))
            n = ((t.package or "?") if t else "?", t.name if t else "?")
            objs[n] += 1
            hit[n] += k in keys
            where[n].add(s[:-6])
    G = {z: {"meta": 0, "present": 0, "out": 0, "rows": [], "missing": collections.Counter()} for z in ORDER}
    for pkg, name in sorted(alltypes):
        g = G[goal(pkg, name)]
        g["meta"] += 1
        if (pkg, name) in objs:
            g["present"] += 1
            g["out"] += hit[(pkg, name)] > 0
            g["rows"].append((name, pkg.replace("Siemens.", ""), objs[(pkg, name)], hit[(pkg, name)]))
        else:
            g["missing"][pkg.replace("Siemens.", "")] += 1
    print("Samples: %d (%s)" % (len(samples), ", ".join(samples)))
    print()
    print("| Goal | concrete object types in the metamodel | of which in samples | of which emitted by the parser | in samples, not emitted | without sample |")
    print("|---|---|---|---|---|---|")
    for z in ORDER:
        g = G[z]
        print("| %s | %d | %d | %d | %d | %d |" % (z, g["meta"], g["present"], g["out"], g["present"] - g["out"], g["meta"] - g["present"]))
    for z in ORDER:
        g = G[z]
        print("\n### %s: present in samples, not emitted\n" % z)
        print("| Type | Package | Objects |")
        print("|---|---|---|")
        for name, pkg, o, h in sorted(g["rows"], key=lambda r: -r[2]):
            if not h:
                print("| %s | %s | %d |" % (name, pkg, o))
        print("\n### %s: packages with types without sample\n" % z)
        print("| Package | Types without sample |")
        print("|---|---|")
        for kv in sorted(g["missing"].items(), key=lambda kv: (-kv[1], kv[0])):
            print("| %s | %d |" % kv)
    if out_json:
        with open(out_json, "w", encoding="utf-8") as f:
            json.dump({"samples": samples, "types": [{"package": p, "name": n, "goal": goal(p, n), "objects": objs.get((p, n), 0),
                                                       "emitted": hit.get((p, n), 0), "samples": sorted(where.get((p, n), ()))}
                                                      for p, n in sorted(alltypes)]}, f, indent=1, ensure_ascii=False)


if __name__ == "__main__":
    main()
