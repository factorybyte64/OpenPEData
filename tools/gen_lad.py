"""Generator and driver for the LAD/FBD coverage samples: one FB per catalog instruction (own interface with all operand kinds),
tried with candidate SD spellings until TIA accepts it (import + compile without error).

Usage: python tools/gen_lad.py <LAD|FBD> [all | <number> | <name> ...] [--cat <catalog json name>] [--pid 12860] [--tag <prefix>]
Candidates: tools/lad_instr.py (INSTR[title] manual, then auto() from the parameter table in docs/catalog/<cat>.json).
Result per instruction: docs/catalog/<lang>_results.json (ok + candidate, or the last error)."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from tiaclient import TiaClient, default_pid  # noqa: E402

INTERFACE = """    VAR_INPUT
        a : Bool;
        b : Bool;
        c : Bool;
        ia : Int;
        ib : Int;
        ic : Int;
        da : DInt;
        db : DInt;
        ra : Real;
        rb : Real;
        wa : Word;
        wb : Word;
        dwa : DWord;
        ba : Byte;
        ta : Time;
        tb : Time;
        s1 : String;
        s2 : String;
        tmr : Timer;
        cntr : Counter;
        s5a : S5Time;
        uia : UInt;
        udia : UDInt;
        usia : USInt;
        lra : LReal;
        dba : DB_ANY;
        bytes : Array[0..7] of Byte;
        v1 : Variant;
    END_VAR
    VAR_OUTPUT
        q : Bool;
        q2 : Bool;
        io : Int;
        io2 : Int;
        dout : DInt;
        rout : Real;
        wout : Word;
        dwout : DWord;
        bout : Byte;
        tout : Time;
        sout : String;
        wout2 : Word;
        lout : LReal;
        eout : ErrorStruct;
    END_VAR
    VAR_IN_OUT
        arr : Array[0..9] of Int;
        barr : Array[0..7] of Bool;
        iinout : Int;
        dinout : DInt;
        bytes2 : Array[0..7] of Byte;
        reals : Array[0..3] of Real;
        arrs : Array[*] of Int;
        lrio : LReal;
        bits16 : Array[0..15] of Bool;
        bits32 : Array[0..31] of Bool;
        wordarr : Array[0..3] of Word;
    END_VAR
    VAR
        ton1 : TON_TIME;
        tof1 : TOF_TIME;
        tp1 : TP_TIME;
        tonr1 : TONR_TIME;
        ctu1 : CTU_INT;
        ctd1 : CTD_INT;
        ctud1 : CTUD_INT;
        rtrig1 : R_TRIG;
        ftrig1 : F_TRIG;
        edge1 : Bool;
        edge2 : Bool;
        stat1 : Bool;
        stat2 : Bool;
        stati : Int;
    END_VAR
    VAR_TEMP
        t1 : Bool;
        t2 : Bool;
        ti : Int;
        td : DInt;
        tr : Real;
        tw : Word;
        tt : Time;
        var1 : Variant;
    END_VAR
"""

NL = chr(10)


def source(lang, tag, title, bodies):
    """Source text of FB_<lang>_<tag> with one network per body; '{R}' is replaced by the rung header."""
    head = "RUNG wire#powerrail" if lang == "LAD" else "RUNG"
    nets = ""
    for i, b in enumerate(bodies):
        nets += '    {' + NL + '        S7_Language := "%s";' % lang + NL + '        S7_NetworkTitle := "MLC_N%d"' % (i + 1) + NL + '    }' + NL + '    NETWORK' + NL
        nets += "".join("        " + ln + NL for ln in b.replace("{R}", head).strip(NL).split(NL))
        nets += "    END_NETWORK" + NL
    fbname = "FB_%s_%s" % (lang.title(), tag)
    src = '{' + NL + '    S7_Optimized := "TRUE";' + NL + '    S7_PreferredLanguage := "%s"' % lang + NL + '}' + NL
    src += 'FUNCTION_BLOCK "%s"' % fbname + NL + INTERFACE + nets + "END_FUNCTION_BLOCK" + NL
    clean = title.replace(":", " -").replace("'", "").replace('"', "")
    res = "MultiLingualTexts:" + NL + "".join("  - id: MLC_N%d" % (i + 1) + NL + "    en-US: '%s'" % clean + NL for i in range(len(bodies)))
    return fbname, src, res


def condense(msg):
    import re
    m = msg.replace("\r\n", " ").replace("\n", " ").replace('\\"', '"')
    m = re.sub(r"\s+", " ", m)
    m = re.sub(r"SIMATIC SD import of '\w+' ended with state Failure: ", "", m)
    m = re.sub(r"Error in (Line Number: \d+, in )?Network:.*?(\| Importing|$)", "|", m)
    m = re.sub(r"\| Importing.*", "", m)
    return m[:260]


TYPES = ["Int", "Word", "DWord", "Real", "DInt", "Byte", "Bool"]


def retemplate(body, names):
    """Bodies with the S7_Templates pragma replaced by one using the template names reported by TIA."""
    import itertools
    import re
    out = []
    if len(names) == 2:
        combos = [("Int", "Int"), ("Word", "Bool"), ("DWord", "Bool"), ("Int", "Real"), ("Real", "Int"), ("Real", "DInt"), ("Word", "Word"), ("Real", "Real")]
    else:
        combos = [(t,) * len(names) for t in TYPES[:4]]
    for combo in combos:
        pr = "{ S7_Templates := \"[%s]\" }" % ", ".join("%s := %s" % (n, t) for n, t in zip(names, combo))
        if "S7_Templates" in body:
            out.append(re.sub(r"\{ S7_Templates := [^}]*\}", lambda m: pr, body))
        else:
            lines = body.split(NL)
            for i, ln in enumerate(lines):  # insert before the first box line (after the header and optional contacts)
                if "(" in ln and not ln.strip().startswith(("Contact", "Coil", "I_Contact")):
                    lines.insert(i, pr)
                    break
            out.append(NL.join(lines))
    return out


NAMES_FILE = os.path.join(HERE, "..", "docs", "catalog", "names.json")
_names = json.load(open(NAMES_FILE)) if os.path.isfile(NAMES_FILE) else {}


def call_name(body):
    import re
    for ln in body.split(NL):
        t = ln.strip()
        if not t or t.startswith(("{", "RUNG", "END_RUNG", "Contact", "Coil", "I_Contact", "#")):
            continue
        m = re.match(r"([A-Za-z_][A-Za-z0-9_]*)\(", t)
        if m:
            return m.group(1)
    return None


def probe_name(c, outdir, nm):
    """True when TIA knows nm as an instruction (neither 'Not a Valid instruction' nor a call of an unknown block)."""
    if nm in _names:
        return _names[nm]
    fb, src, res = source("LAD", "PRB", "probe", ["{R}" + NL + nm + "( in := #ia, out => #io )" + NL + "END_RUNG"])
    path = os.path.join(outdir, fb + ".s7dcl")
    open(path, "w", encoding="utf-8").write(src)
    open(path[:-6] + ".s7res", "w", encoding="utf-8").write(res)
    err, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=path, waitMs="45000")
    if err:
        ok = "Not a Valid instruction" not in str(r)
    else:
        msgs = " ".join(x["description"] for x in (r.get("compile") or {}).get("messages", []) if x.get("state") == "Error")
        ok = "_Callee" not in msgs
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
    _names[nm] = ok
    json.dump(_names, open(NAMES_FILE, "w"), indent=0, sort_keys=True)
    return ok


def try_one(c, lang, tag, title, candidates, outdir, n_manual=0):
    import re
    last = None
    seen = []
    bad_names, no_pragma_names = set(), set()
    tried_templates = set()
    queue = list(candidates)
    k = -1
    while queue and k < 45:
        cand = queue.pop(0)
        k += 1
        bodies = cand if isinstance(cand, list) else [cand]
        text = NL.join(bodies)
        if k >= n_manual:  # auto candidates: skip names TIA does not know as instruction
            cn = call_name(text)
            if cn and not probe_name(c, outdir, cn):
                continue
        names = {m.lower() for m in re.findall(r"(?:^|[\s.#])([A-Za-z_][A-Za-z0-9_]*)\(", text)}
        if names & bad_names or (no_pragma_names & {x for x in names}) and "S7_Templates" in text:
            continue
        fb, src, res = source(lang, tag, title, bodies)
        path = os.path.join(outdir, fb + ".s7dcl")
        open(path, "w", encoding="utf-8").write(src)
        open(path[:-6] + ".s7res", "w", encoding="utf-8").write(res)
        err, out = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=path, waitMs="45000")
        if err:
            last = {"candidate": k, "stage": "import", "message": (out if isinstance(out, str) else json.dumps(out))[:500]}
            cm = condense(last["message"])
            if cm not in seen:
                seen.append(cm)
            tm = re.search(r"Allowed Template Names: '([^']*)'", cm)
            if tm and "S7_Templates" in text and not any(x.get("retemplated") for x in [{}]):
                names = [n.strip() for n in tm.group(1).split(",") if n.strip()]
                key = tuple(names)
                if key not in tried_templates:
                    tried_templates.add(key)
                    queue[:0] = [[b] for b in retemplate(bodies[0], names)]
            mm = re.search(r"Not a Valid instruction: (\w+)", cm)
            if mm:
                bad_names.add(mm.group(1).lower())
            pm = re.search(r"not supported with TextLad element '(\w+)'", cm)
            if pm and "Pragmas are not supported" in cm:
                no_pragma_names.add(pm.group(1).lower())
            continue
        comp = out.get("compile") or {}
        if comp.get("errorCount", 0) == 0:
            return {"ok": True, "candidate": k, "block": fb, "body": bodies}
        last = {"candidate": k, "stage": "compile",
                "message": "; ".join("%s: %s" % (m["path"].split(">")[-1].strip(), m["description"]) for m in comp.get("messages", [])
                                      if m.get("state") == "Error")[:500]}
        cm = condense(last["message"])
        if cm not in seen:
            seen.append(cm)
        # a block with compile errors is kept out of the project
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
    return {"ok": False, "last": last, "tried": len(candidates), "errors": seen[:8]}


def short(title):
    return title.split(" (S7")[0]


def main(argv):
    import lad_instr  # noqa: E402
    lang = argv[0].upper()
    sel = [a for a in argv[1:] if not a.startswith("--") and (argv.index(a) < 2 or not argv[argv.index(a) - 1].startswith("--"))]
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d  # noqa: E731
    pid = int(opt("--pid", default_pid()))
    cat_name = opt("--cat", lang.lower())
    prefix = opt("--tag", "")
    outdir = os.path.join(HERE, "sources", cat_name if cat_name == lang.lower() else cat_name + "_" + lang.lower())
    os.makedirs(outdir, exist_ok=True)
    cat = json.load(open(os.path.join(HERE, "..", "docs", "catalog", cat_name + ".json"), encoding="utf-8"))
    c = TiaClient(pid)
    rp = os.path.join(HERE, "..", "docs", "catalog", ("%s_results.json" % cat_name) if cat_name == lang.lower() else ("%s_%s_results.json" % (cat_name, lang.lower())))
    results = json.load(open(rp)) if os.path.isfile(rp) else {}
    for idx, item in enumerate(cat):
        t = short(item["title"])
        tag = prefix + "%03d" % (idx + 1)
        if sel and sel != ["all"] and sel != ["failed"] and not any(x == "%03d" % (idx + 1) or x == item["name"] for x in sel):
            continue
        if sel == ["failed"] and results.get(tag, {}).get("ok", False):
            continue
        man_c = lad_instr.manual(t, lang)
        cands = man_c + lad_instr.auto(item)
        if not cands:
            results[tag] = {"title": t, "ok": False, "last": {"message": "no candidate"}}
            print(tag, t[:50], "NO CANDIDATE", flush=True)
            continue
        r = try_one(c, lang, tag, t, cands, outdir, len(man_c))
        r["title"] = t
        results[tag] = r
        print("%s %-44s %s" % (tag, t[:44], ("ok cand %d" % r["candidate"]) if r["ok"] else "FAIL " + json.dumps(r["last"])[:240]), flush=True)
        json.dump(results, open(rp, "w"), indent=1, sort_keys=True)


if __name__ == "__main__":
    main(sys.argv[1:])
