"""Audit of the catalog results: does the exported SimaticML of every "ok" block contain the instruction the catalog item is about?
python tools/audit_results.py [--all]   (default: only the suspicious ones)

For each result file docs/catalog/*_results.json (not SCL): block -> samples/<sample>_truth/simaticml/<block>.xml, the names of all
Part/Instruction/CallInfo elements are compared (case and underscores ignored) with the instruction name of the catalog title
('T_COMP: Compare time tags' -> tcomp) and with the SD/SimaticML names from names.json / xml_names.json. A result is suspicious when
none matches, which is how the legacy-part attributions (BLKMOV for MOVE_BLK) were found."""
import glob
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..")
CAT = os.path.join(ROOT, "docs", "catalog")


def norm(s):
    return re.sub(r"[^a-z0-9]", "", s.lower())


def names_of(xml):
    out = set()
    for pat in (r'<Part Name="([^"]+)"', r'<Instruction Name="([^"]+)"', r'<CallInfo Name="([^"]+)"'):
        out.update(re.findall(pat, xml))
    return out


def main(argv):
    show_all = "--all" in argv
    xml_names = json.load(open(os.path.join(HERE, "xml_names.json"), encoding="utf-8"))
    bad = total = 0
    for rf in sorted(glob.glob(os.path.join(CAT, "*_results.json"))):
        base = os.path.basename(rf)[:-13]
        if base.startswith("scl"):
            continue
        cat_name = base.split("_")[0] if base not in ("lad", "fbd") else base
        cat_file = os.path.join(CAT, cat_name + ".json")
        if not os.path.isfile(cat_file):
            continue
        cat = json.load(open(cat_file, encoding="utf-8"))
        names_file = os.path.join(CAT, cat_name + "_names.json")
        sdnames = json.load(open(names_file, encoding="utf-8")) if os.path.isfile(names_file) else {}
        results = json.load(open(rf))
        for key, v in sorted(results.items()):
            if not v.get("ok") or not v.get("block"):
                continue
            tag = re.sub(r"^[a-z]+", "", key)
            if not tag.isdigit():
                continue
            title = v.get("title") or cat[int(tag) - 1]["title"]
            head = norm(title.split(":")[0].replace("----I", "").replace("I----", ""))
            sample = v.get("sample")
            tx = None
            for d in ([sample + "_truth"] if sample else []) + [os.path.basename(x) for x in sorted(glob.glob(os.path.join(ROOT, "samples", "*_truth")))]:
                p = os.path.join(ROOT, "samples", d, "simaticml", v["block"] + ".xml")
                if os.path.isfile(p):
                    tx = open(p, encoding="utf-8-sig").read()
                    break
            total += 1
            if tx is None:
                print("NO TRUTH  %-22s %s %s" % (base, key, v["block"]))
                bad += 1
                continue
            found = {norm(n) for n in names_of(tx)}
            wanted = {head, norm(str((sdnames.get(tag) or {}).get("sdName") or ""))}
            for spec in xml_names.get(cat_name, {}).get(tag, []):
                wanted.add(norm(spec["name"] if isinstance(spec, dict) else spec))
            wanted.discard("")
            ok = bool(found & wanted) or any(w in f for w in wanted for f in found if len(w) > 3)
            if not ok or show_all:
                if not ok:
                    bad += 1
                print("%-8s %-22s %s %-24s title=%s found=%s" % ("" if ok else "SUSPECT", base, key, v["block"], head, sorted(names_of(tx))[:6]))
    print("checked", total, "suspicious/missing", bad)


if __name__ == "__main__":
    main(sys.argv[1:])
