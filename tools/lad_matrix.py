"""Coverage matrix of the LAD/FBD instruction catalog: docs/catalog/<cat>.json + results -> docs/spec/instruction-coverage.md.
Usage: python tools/lad_matrix.py"""
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CAT = os.path.join(ROOT, "docs", "catalog")
SETS = [("lad", "lad", "LAD", "031_lad_all"), ("fbd", "fbd", "FBD", "032_fbd_all"), ("extended", "extended_lad", "LAD", "037_lad_xml"),
        ("extended", "extended_fbd", "FBD", "039_fw41"), ("scl", "scl_scl", "SCL", "039_fw41"), ("openuc", "openuc_fbd", "FBD", "039_fw41"), ("s7comm", "s7comm_fbd", "FBD", "039_fw41"), ("modbus", "modbus_lad", "LAD", "038_comm_hw"),
        ("modbustcp", "modbustcp_lad", "LAD", "038_comm_hw"), ("openuc", "openuc_lad", "LAD", "037_lad_xml"),
        ("ptp", "ptp_lad", "LAD", "038_comm_hw"), ("s7comm", "s7comm_lad", "LAD", "037_lad_xml"), ("uss", "uss_lad", "LAD", "038_comm_hw"),
        ("webserver", "webserver_lad", "LAD", "037_lad_xml"), ("teleservice", "teleservice_lad", "LAD", "038_comm_hw")]


def main():
    out = ["# Coverage LAD/FBD (instruction catalog TIA V21, S7-1200/1500)", "",
           "Source of the list: `docs/catalog/*.txt|json` (table of contents of the installed Siemens help, `tools/instr_catalog.py`). ",
           "Creation: `tools/gen_lad.py` (SD candidates, import + compile in TIA). Status: ok = created and compiled without errors; ",
           "missing = not created so far (reason = last TIA message).", ""]
    total = {}
    fails = []  # (set, tag, title, reason)
    for cat, resname, lang, sample in SETS:
        cp = os.path.join(CAT, cat + ".json")
        rp = os.path.join(CAT, resname + "_results.json")
        if not os.path.isfile(cp) or not os.path.isfile(rp):
            continue
        items = json.load(open(cp, encoding="utf-8"))
        res = json.load(open(rp))
        ok = sum(1 for i in range(len(items)) if (res.get("%03d" % (i + 1)) or next((v for k, v in res.items() if k[-3:] == "%03d" % (i + 1) and not k[:-3].isdigit()), {})).get("ok"))
        names = {}
        np_ = os.path.join(CAT, cat + "_names.json")
        if os.path.isfile(np_):
            names = json.load(open(np_))
        pages = {"%03d" % (i + 1) for i, it in enumerate(items) if not it.get("params") and (
            (names and not (names.get("%03d" % (i + 1)) or {}).get("sdName")) or re.search(r"truth table", it["title"], re.I))}
        items_n = len(items) - len(pages)  # help pages that describe a parameter or an example are not instructions
        ok -= sum(1 for t_ in pages if (res.get(t_) or next((v for k, v in res.items() if k[-3:] == t_ and not k[:-3].isdigit()), {})).get("ok"))  # help pages marked ok are not counted
        total[resname] = (items_n, ok)
        out += ["## %s (%s, sample %s): %d instructions, %d ok%s" % (cat, lang, sample, items_n, ok, (", %d help pages (parameter/example, not an instruction)" % len(pages)) if pages else ""), "", "| No | Group | Instruction | Status | Block | Remark |", "| --- | --- | --- | --- | --- | --- |"]
        for i, it in enumerate(items):
            tag = "%03d" % (i + 1)
            r = res.get(tag) or next((v for k, v in res.items() if k[-3:] == tag and not k[:-3].isdigit()), {})
            title = it["title"].split(" (S7")[0]
            if tag in pages:
                out.append("| %s | %s | %s | help page | | not an instruction of its own |" % (tag, it["group"].split(" / ", 1)[-1], title))
            elif r.get("ok"):
                out.append("| %s | %s | %s | ok%s | %s%s | |" % (tag, it["group"].split(" / ", 1)[-1], title, " (SimaticML-Import)" if r.get("via") == "xml" else "", r.get("block", ""),
                                                                      (" (%s)" % r["sample"]) if r.get("sample") else ""))
            else:
                last = (r.get("last") or {}).get("message", "") if isinstance(r.get("last"), dict) else ""
                why = r.get("xmlLast") or ((r.get("errors") or [last or "no candidates (name/syntax unknown)"])[-1]) or ""
                why = re.sub(r"from file '[^']*'.*", "", why)[:120].replace("|", "/").replace("\n", " ")
                out.append("| %s | %s | %s | missing | | %s |" % (tag, it["group"].split(" / ", 1)[-1], title, why))
                fails.append((resname, tag, title.split(":")[0], why))
        out.append("")
    cats = [("SimaticML/SD name unknown", r"unknown instruction name|no candidate|cannot be found|no SD name"),
            ("Operand must be an array element or a DB access", r"array elements|optimized memory|variable limits"),
            ("Operand type / system data type does not match", r"does not match the data type|is unknown|not allowed here|not permitted here|implicitly|cannot be converted|both data types"),
            ("Call form (SCL) / pins", r"has to be used|do not match the transferred|returns a value|Invalid function|Unknown instruction|Invalid assignment"),
            ("Constant/pin format", r"constant|Operand '|BCD|ConstantValue"),
            ("TIA rejects the structure", r"not supported|is invalid|contains an invalid|cannot be deactivated|Wires")]
    rest = ["", "## Remaining list (generated from the result files)", "", "Grouped by the kind of the last TIA message; the individual messages are in the tables above.", ""]
    grouped = {}
    for resname, tag, title, why in fails:
        cat_name = next((c for c, rx in cats if re.search(rx, why, re.I)), "Other / not evaluated")
        grouped.setdefault(cat_name, []).append("%s %s%s" % (resname.replace("_lad", "").replace("_fbd", "").replace("_scl", ""), tag, " " + title[:28]))
    for c in [x[0] for x in cats] + ["Other / not evaluated"]:
        if c in grouped:
            rest += ["- **%s** (%d): %s" % (c, len(grouped[c]), "; ".join(grouped[c]))]
    out += rest
    out.insert(5, "Summary: " + "; ".join("%s %d/%d" % (k, v[1], v[0]) for k, v in total.items()))
    open(os.path.join(ROOT, "docs", "spec", "instruction-coverage.md"), "w", encoding="utf-8").write("\n".join(out) + "\n")
    print(total)


if __name__ == "__main__":
    main()
