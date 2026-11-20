"""LAD/FBD catalog instructions whose rung is hand-written (the generic candidate search does not find the box form), one FB each.
python tools/gen_lad_manual.py [LAD|FBD] [num ...] [--sample 039_fw41]
Interface = the operand FB of the usual generator (tools/sources/lad/FB_Lad_<num>.s7dcl, plus a Ref_To variable), rung text from MANUAL.
Results docs/catalog/<lang>_results.json (key = number, "manual": true)."""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_lad  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

# (language, catalog number) -> rung lines (the first starts with RUNG). Findings: S5 boxes are the SD/SimaticML gates Sp, Se, Sd, Ss, Sf (timers,
# pins timer s tv r rt rtbcd q) and Cu, Cd, Cud (counters, pins counter s pv r cv cvbcd q [cu cd]); in LAD the S/CU pin is the power flow
# (Contact before the box), in FBD the first operand (RUNG #a).
def _s5(lang, name, cnt=False):
    pins = "counter := #cntr, " if cnt else "timer := #tmr, "
    mid = ("cd := #b, " if name == "cud" else "") + ("s := #b, pv := #wa, r := #c, cv => #wb, cvbcd => #tw" if cnt else "tv := #s5a, r := #c, rt => #wa, rtbcd => #wb")
    call = "%s( %s%s, q => #q )" % (name, pins, mid)
    if lang == "LAD":
        return ["RUNG wire#powerrail", "Contact( #a )", call]
    return ["RUNG #a", call]


MANUAL = {
    ("LAD", 104): ["RUNG wire#powerrail", '{ S7_Templates := "[src_type := Variant, dest_type := Int]" }', "assignmentattempt( IN := #var1, OUT => #ref1 )"],
    ("FBD", 62): ["RUNG", "ok( operand := #ra )", "Coil( #q )"],
    ("FBD", 63): ["RUNG", "nok( operand := #ra )", "Coil( #q )"],  # NOT_OK is "nok" in SD (and in the NOKGate of the part table)
    ("LAD", 58): ["RUNG wire#powerrail", "nok( operand := #ra )", "Coil( #q )"],
}
for _lang, _nums in (("LAD", (30, 31, 32, 33, 34, 43, 44, 45)), ("FBD", (35, 36, 37, 38, 39, 48, 49, 50))):
    for _n, _nm in zip(_nums, ("sp", "se", "sd", "ss", "sf", "cu", "cd", "cud")):
        MANUAL[(_lang, _n)] = _s5(_lang, _nm, _nm.startswith("c"))


def main(argv):
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d  # noqa: E731
    lang = argv[0].upper() if argv and argv[0].upper() in ("LAD", "FBD") else "LAD"
    sel = [int(a) for a in argv if a.isdigit()]
    c = TiaClient(default_pid())
    rp = os.path.join(HERE, "..", "docs", "catalog", lang.lower() + "_results.json")
    res = json.load(open(rp))
    cat = json.load(open(os.path.join(HERE, "..", "docs", "catalog", lang.lower() + ".json"), encoding="utf-8"))
    for (lg, num), rung in sorted(MANUAL.items()):
        if lg != lang or (sel and num not in sel):
            continue
        key = "%03d" % num
        if not sel and res.get(key, {}).get("ok"):
            continue
        fb = "FB_%s_%03d" % (lang.title(), num)
        base = os.path.join(HERE, "sources", lang.lower(), fb + ".s7dcl")
        tmpl = fb if os.path.isfile(base) else "FB_%s_001" % lang.title()  # operand FB of the generic generator, any item's will do
        base = os.path.join(HERE, "sources", lang.lower(), tmpl + ".s7dcl")
        src = open(base, encoding="utf-8").read().replace("\r\n", "\n").replace(tmpl, fb)
        head = re.split(r"\n        RUNG", src)[0].replace("    VAR_TEMP\n", "    VAR_TEMP\n        ref1 : Ref_To Int;\n") + "\n"
        src = head + "".join("        %s\n" % ln for ln in rung) + "        END_RUNG\n    END_NETWORK\nEND_FUNCTION_BLOCK\n"
        path = os.path.join(HERE, "sources", "manual_" + lang.lower())
        os.makedirs(path, exist_ok=True)
        fp = os.path.join(path, fb + ".s7dcl")
        open(fp, "w", encoding="utf-8", newline="").write(src.replace("\n", "\r\n"))
        resfile = open(base[:-6] + ".s7res", encoding="utf-8").read().replace(tmpl, fb)
        open(fp[:-6] + ".s7res", "w", encoding="utf-8", newline="").write(resfile)
        e, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=fp, waitMs="45000")
        if e:
            msg, ok = gen_lad.condense(r if isinstance(r, str) else json.dumps(r)), False
        else:
            comp = r.get("compile") or {}
            msg = "; ".join(m["description"] for m in comp.get("messages", []) if m.get("state") == "Error")
            ok = comp.get("errorCount", 1) == 0
            if not ok:
                c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
        title = cat[num - 1]["title"].split(" (S7")[0]
        if ok:
            res[key] = {"title": title, "ok": True, "block": fb, "sample": opt("--sample", "039_fw41"), "manual": True}
        else:
            res.setdefault(key, {"title": title, "ok": False})["manualLast"] = msg[:300]
        print(key, title[:30], "ok" if ok else "FAIL " + msg[:200], flush=True)
        json.dump(res, open(rp, "w"), indent=1, sort_keys=True)


if __name__ == "__main__":
    main(sys.argv[1:])
