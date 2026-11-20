"""OpenPEData - reads a TIA Portal V21 project without TIA Portal.

ONLY FOR TEST AND ANALYSIS PURPOSES: this Python parser is not the product parser and must not be shipped or used by
the viewer, the setup or other programs. The parser of OpenPEData is the .NET library OpenPEData.dll (dotnet\\src\\OpenPEData).
This one is the reference for the byte comparison with the .NET port, the truth tests (parser\\tests) and format research.

Usage: OpenPEData <project folder | .ap21 | .zap21> [--json] [--keys] [--verify] [--xref] [--proginfo]
  --json     JSON output (default: indented text tree)
  --keys     include record keys (typeLo:typeHi:oid) in the JSON output
  --verify   integrity check only (header, record hashes, hash chain and RSA signatures of the commits); exit code 1 if anything fails
  --xref     cross references from XRef/XRef.db with object names (always JSON; null without XRef.db)
  --proginfo program information per PLC: resources, call structure, dependency structure, assignment list (always JSON)
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from openpedata.faceplates import to_text as faceplates_text  # noqa: E402
from openpedata.hardware import network_text, text_lines as hardware_lines  # noqa: E402
from openpedata.hmi import text_lines as hmi_lines  # noqa: E402
from openpedata.library import to_text as library_text  # noqa: E402
from openpedata.model import Model  # noqa: E402


def member_lines(members, indent):
    out = []
    for m in members:
        start = m["startValue"]
        if start is None and m.get("defaultValue") is not None:
            start = "(%s)" % m["defaultValue"]  # instance DB: start value inherited from the FB
        if m.get("elementStartValues"):
            start = ", ".join("[%s] %s" % kv for kv in m["elementStartValues"].items())
        out.append("%s%-24s %-20s %-12s %s" % (" " * indent, m["name"], m["dataType"], "" if start is None else start,
                                             "; ".join("%s: %s" % kv for kv in m["comment"].items())))
        out += member_lines(m.get("members", []), indent + 2)
    return out


def network_lines(nets, indent):
    out = []
    pad = " " * indent
    for n in nets:
        head = "%sNetwork %s [%s]" % (pad, n["index"], n["language"])
        if n.get("title"):
            head += "  " + "; ".join("%s: %s" % kv for kv in n["title"].items())
        out.append(head)
        if n.get("comment"):
            out.append("%s  // %s" % (pad, "; ".join("%s: %s" % kv for kv in n["comment"].items())))
        if n.get("error"):
            out.append("%s  (%s)" % (pad, n["error"]))
        for line in (n.get("scl") or "").splitlines():
            out.append("%s  | %s" % (pad, line))
        for line in (n.get("stl") or "").splitlines():
            out.append("%s  | %s" % (pad, line))
        if n.get("parts"):
            names = [x["name"] for x in n["parts"] if x["kind"] == "part"]
            out.append("%s  parts: %s" % (pad, ", ".join(names)))
            end = lambda e: ("powerrail" if e.get("powerrail") else "open" if e.get("open") else
                             e["operand"] if "operand" in e else "%s#%s.%s" % (e["name"], e["part"], e["pin"]))  # noqa: E731
            for w in n["wires"]:
                out.append("%s  wire %s: %s" % (pad, w["uid"], " -- ".join(end(e) for e in w["ends"])))
    return out


def text_tree(d):
    pr = d["project"]
    out = ["%s %s" % ("Global library" if pr.get("kind") == "GlobalLibrary" else "Project", pr["name"])]
    for g in pr["deviceGroups"]:
        out.append("  Group %s" % g["path"])
    for dev in pr["devices"]:
        out.append("  Device %s (%s)%s" % (dev["name"], dev["typeName"], "  group " + dev["group"] if dev["group"] else ""))
        out += hardware_lines(dev["hardware"], 4)
        for plc in dev["plcs"]:
            out.append("    PLC %s  %s %s %s" % (plc["name"], plc["typeName"], plc["orderNumber"], plc["firmware"]))
            for t in plc["tagTables"]:
                out.append("      Tag table %s%s" % ((t["group"] + "/") if t["group"] else "", t["name"]))
                for tag in t["tags"]:
                    out.append("        %-24s %-10s %-10s %s" % (tag["name"], tag["dataType"], tag["address"] or "",
                                                              "; ".join("%s: %s" % kv for kv in tag["comment"].items())))
            for b in plc["blocks"]:
                out.append("      Block %-20s %-10s %-5s %-5s%s%s" % (
                    b["name"], b["type"], b["number"], b["language"], "  of " + b["instanceOf"] if b.get("instanceOf") else "",
                    "  group " + b["group"] if b["group"] else ""))
                if not b.get("interface"):
                    out += member_lines(b.get("members", []), 8)
                for sec in b.get("interface", []):
                    out.append("        %s" % sec["section"])
                    out += member_lines(sec["members"], 10)
                out += network_lines([n for n in b.get("networks", []) if n.get("scl") or n.get("stl") or n.get("parts") or n.get("title")
                                      or n.get("comment")], 8)
            for t in plc["types"]:
                out.append("      Type  %-20s%s" % (t["name"], "  group " + t["group"] if t["group"] else ""))
                out += member_lines(t["members"], 8)
        for sw in dev.get("hmi", []):
            out += hmi_lines(sw, 4)
    out += network_text(pr["network"], 2)
    lib = d.get("library") or {}
    if any(L["masterCopies"] or L["types"] for L in lib.get("libraries", [])) or lib.get("instances"):
        out += ["  " + line for line in library_text(lib).splitlines() if not line.startswith("Statistics:")]
    if d.get("faceplates"):
        out += ["  " + line for line in faceplates_text(d["faceplates"]).splitlines()]
    return "\n".join(out)


def _json_default(o):
    """Raw values the model passes through (bytes) are written as hex strings instead of aborting the output."""
    if isinstance(o, (bytes, bytearray)):
        return "hex:" + bytes(o).hex()
    raise TypeError("Object of type %s is not JSON serializable" % type(o).__name__)


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        print(__doc__, file=sys.stderr)
        return 2
    if "--verify" in argv:
        from openpedata import verify
        rep = verify.verify_project(args[0])
        sys.stdout.reconfigure(encoding="utf-8")
        print(json.dumps(rep, indent=2) if "--json" in argv else verify.text_report(rep))
        return 0 if rep["ok"] else 1
    if "--xref" in argv:
        from openpedata import xref
        sys.stdout.reconfigure(encoding="utf-8")
        print(json.dumps(xref.resolved(args[0], Model(args[0]).p), indent=2, ensure_ascii=False))
        return 0
    if "--proginfo" in argv:
        from openpedata import proginfo
        sys.stdout.reconfigure(encoding="utf-8")
        print(json.dumps(proginfo.extract(Model(args[0]), args[0]), indent=2, ensure_ascii=False))
        return 0
    d = Model(args[0]).to_dict(keys="--keys" in argv)
    sys.stdout.reconfigure(encoding="utf-8")
    print(json.dumps(d, indent=2, ensure_ascii=False, default=_json_default) if "--json" in argv else text_tree(d))
    for e in d["statistics"].get("errors", []):  # unreadable objects: the output is complete apart from these
        sys.stderr.reconfigure(errors="replace")
        print("not readable: %s: %s" % (e["object"], e["error"]), file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
