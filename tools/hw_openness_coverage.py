"""Z1.1 step H8: stored hardware parameters of the H samples (200-242) against the Openness truth of those samples.

For every expando parameter the parser outputs at a device item (and the address node fields), look for the Openness
value in the sample's truth: Openness attributes of the item (attributes*.json, item_*.json, iface_*.json; hidden
submodules such as CpuCommon, CPUexecUnit, OPC UA_1 are shown by Openness at the CPU item), Openness service properties
(services_PLC_1.json) and node attributes (node_*.json). A parameter counts as
  mapped     - pair confirmed in hardware.OPENNESS_NAMES (Openness attribute or Service.Property),
  same name  - Openness has an attribute of the same name at the item,
  node       - address node field (IP, mask, PROFINET name, PROFIBUS address) against TiaNetworkNode attributes,
  none       - no Openness counterpart found (stored value outside the Openness scope, GOALS.md 4.).
Only values stored in the file are compared (decision 2026-10-24). Nothing is changed in TIA.

Usage: python -I tools/hw_openness_coverage.py [--json out.json] [--md out.md]
"""
import glob
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.model import Model  # noqa: E402
from openpedata.hardware import OPENNESS_NAMES  # noqa: E402

SAMPLES = os.path.join(ROOT, "samples")


def load(p):
    with open(p, encoding="utf-8-sig") as f:
        return json.load(f)


def flat(items, parent=""):
    out = {}
    for it in items:
        p = parent + "/" + (it["name"] or "")
        out.setdefault(p, it)
        out.update(flat(it["items"], p))
    return out


def same(a, b):
    if a == b:
        return True
    if isinstance(a, bool) or isinstance(b, bool):
        return str(a).lower() == str(b).lower()
    return isinstance(a, (int, float, str)) and isinstance(b, (int, float, str)) and str(a) == str(b)


def openness_items(truth):
    """{(station, deviceItemPath): {attribute: value}} from the truth files of one sample."""
    res = {}
    for f in glob.glob(os.path.join(truth, "attributes*.json")):  # 200-209: {path: {attr: {value}}}, station PLC_1
        if os.path.basename(f) == "attributes_before.json":
            continue
        for path, attrs in load(f).items():
            if isinstance(attrs, dict):
                res[("PLC_1", path)] = {n: (v.get("value") if isinstance(v, dict) else v) for n, v in attrs.items()}
    for f in (glob.glob(os.path.join(truth, "item_*.json")) + glob.glob(os.path.join(truth, "iface_*.json"))
              + glob.glob(os.path.join(truth, "module_*.json"))):
        t = load(f)
        if "attributes" in t and "deviceItemPath" in t:
            res[(t.get("deviceKey") or "PLC_1", t["deviceItemPath"])] = {a["name"]: a["value"] for a in t["attributes"]}
    return res


def services(truth):
    f = os.path.join(truth, "services_PLC_1.json")
    return {k: (v or {}).get("properties") or {} for k, v in load(f).items()} if os.path.exists(f) else {}


def nodes(truth):
    res = {}
    for f in glob.glob(os.path.join(truth, "node_*.json")):
        t = load(f)
        res[(t["deviceKey"], t["deviceItemPath"])] = t["attributes"]
    return res


def lookup(oi, station, path):
    """Openness attributes for a parser item path (/A/B/C): the item itself, else the nearest visible ancestor."""
    parts = path.strip("/").split("/")
    while parts:
        a = oi.get((station, "/".join(parts)))
        if a is not None:
            return a
        parts = parts[:-1]
    return None


def find_up(oi, station, path, att):
    parts = path.strip("/").split("/")
    while parts:
        a = oi.get((station, "/".join(parts)))
        if a is not None and att in a:
            return a[att]
        parts = parts[:-1]
    return None


def analyse(sample):
    src = os.path.join(SAMPLES, sample + ".zap21")
    truth = os.path.join(SAMPLES, sample + "_truth")
    d = Model(src).to_dict()
    oi, sv, nd = openness_items(truth), services(truth), nodes(truth)
    rows = []
    for dev in d["project"]["devices"]:
        st = dev["name"]
        for path, it in flat(dev["hardware"]["items"]).items():
            for n in it["nodes"]:
                na = nd.get((st, path.strip("/")))
                if na is None:
                    continue
                for field, att in (("ipAddress", "Address"), ("subnetMask", "SubnetMask"), ("profibusAddress", "Address")):
                    if n.get(field) is not None and att in na and (field != "profibusAddress" or na.get("NodeType") == "Profibus") \
                            and (field == "profibusAddress" or na.get("NodeType") == "Ethernet"):
                        rows.append({"sample": sample, "station": st, "item": path + "/" + (n["name"] or ""), "subtype": "Node",
                                     "parameter": field, "kind": "node", "openness": att, "equal": same(n[field], na[att])})
                if n["parameters"].get("PnPnNoSAutoGenerate") is False and "PnDeviceName" in na:
                    rows.append({"sample": sample, "station": st, "item": path + "/" + (n["name"] or ""), "subtype": "Node",
                                 "parameter": "profinetDeviceName", "kind": "node", "openness": "PnDeviceName",
                                 "equal": same(n["profinetDeviceName"], na["PnDeviceName"])})
            oa = lookup(oi, st, path)
            if oa is None:
                continue  # no Openness attributes of this station in the truth of this sample
            for par, val in it["parameters"].items():
                att = OPENNESS_NAMES.get((it["subtype"], par))
                kind, exp = "none", None
                if att and "." in att:
                    s, prop = att.split(".", 1)
                    if prop in sv.get(s, {}):
                        kind, exp = "mapped", sv[s][prop]
                elif att:
                    hit = find_up(oi, st, path, att)  # e.g. TimeSyncRole at the interface, Openness NtpV2 at the CPU
                    if hit is not None:
                        kind, exp = "mapped", hit
                elif par in oa:
                    kind, att, exp = "same name", par, oa[par]
                eqv = same(val, exp) if kind != "none" else None
                if kind == "same name" and isinstance(val, int) and isinstance(exp, str) and not exp.lstrip("-").isdigit():
                    kind, eqv = "same name (enum)", None  # stored number, Openness enum name (PnDnsConfigNameResolve)
                rows.append({"sample": sample, "station": st, "item": path, "subtype": it["subtype"], "parameter": par,
                             "kind": kind, "openness": att if kind != "none" else None, "equal": eqv})
    return rows


def main(argv):
    out_json = argv[argv.index("--json") + 1] if "--json" in argv else None
    out_md = argv[argv.index("--md") + 1] if "--md" in argv else None
    samples = sorted(os.path.basename(p)[:-6] for p in glob.glob(os.path.join(SAMPLES, "2[0-4][0-9]_hw_*_truth")))
    rows = []
    for s in samples:
        if os.path.exists(os.path.join(SAMPLES, s + ".zap21")):
            rows += analyse(s)
    pairs = {}
    for r in rows:
        k = (r["subtype"], r["parameter"])
        p = pairs.setdefault(k, {"subtype": k[0], "parameter": k[1], "kind": r["kind"], "openness": r["openness"],
                                 "occurrences": 0, "equal": 0, "different": 0, "samples": set()})
        if r["kind"] != "none" and p["kind"] == "none":
            p["kind"], p["openness"] = r["kind"], r["openness"]
        p["occurrences"] += 1
        p["samples"].add(r["sample"])
        if r["equal"] is True:
            p["equal"] += 1
        elif r["equal"] is False:
            p["different"] += 1
    pl = sorted(pairs.values(), key=lambda p: (p["kind"], p["subtype"] or "", p["parameter"]))
    for p in pl:
        p["samples"] = sorted(p["samples"])
    summary = {"samples": samples, "occurrences": len(rows),
               "byKind": {k: sum(1 for p in pl if p["kind"] == k) for k in ("mapped", "same name", "same name (enum)", "node", "none")},
               "valuesEqual": sum(p["equal"] for p in pl), "valuesDifferent": sum(p["different"] for p in pl)}
    result = {"summary": summary, "pairs": pl}
    if out_json:
        with open(out_json, "w", encoding="utf-8") as f:
            json.dump(result, f, ensure_ascii=False, indent=1)
    lines = ["| Kind | Subtype | Parameter (file) | Openness | Occurrences | equal | different |", "|---|---|---|---|---|---|---|"]
    for p in pl:
        lines.append("| %s | %s | `%s` | %s | %d | %d | %d |" % (p["kind"], p["subtype"], p["parameter"],
                                                             "`%s`" % p["openness"] if p["openness"] else "–",
                                                             p["occurrences"], p["equal"], p["different"]))
    text = "\n".join(lines)
    if out_md:
        with open(out_md, "w", encoding="utf-8") as f:
            f.write(text + "\n")
    sys.stdout.reconfigure(encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))
    if not out_md:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
