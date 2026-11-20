"""Typed generation of catalog instructions (extended, communication, ...): one FB per instruction whose interface declares one variable
per pin with the data type taken from the parameter table of the catalog (docs/catalog/<cat>.json), the call uses these variables.

Usage: python tools/gen_typed.py <LAD|FBD> <cat> [all | numbers...] [--tag ex] [--pid N] [--out results.json] [--sample <sample name>]
Names: docs/catalog/<cat>_names.json (tools/lad_names.py) gives the SD name; template pragmas/pins are repaired from TIA's messages.
Results: docs/catalog/<cat>_<lang>_results.json (same format as gen_lad.py, plus "typed": true)."""
import itertools
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_lad  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

NL = gen_lad.NL
SIMPLE = {"BOOL": "Bool", "BYTE": "Byte", "WORD": "Word", "DWORD": "DWord", "LWORD": "LWord", "SINT": "SInt", "INT": "Int", "DINT": "DInt",
          "LINT": "LInt", "USINT": "USInt", "UINT": "UInt", "UDINT": "UDInt", "ULINT": "ULInt", "REAL": "Real", "LREAL": "LReal",
          "TIME": "Time", "LTIME": "LTime", "DATE": "Date", "TIME_OF_DAY": "Time_Of_Day", "TOD": "Time_Of_Day", "LTIME_OF_DAY": "LTime_Of_Day",
          "LTOD": "LTime_Of_Day", "DTL": "DTL", "LDT": "LDT", "DATE_AND_TIME": "Date_And_Time", "DT": "Date_And_Time", "CHAR": "Char",
          "WCHAR": "WChar", "STRING": "String", "WSTRING": "WString", "VARIANT": "Variant", "DB_ANY": "DB_ANY", "HW_ANY": "HW_ANY",
          "ANY": "Any", "POINTER": "Pointer", "TIMER": "Timer", "COUNTER": "Counter", "S5TIME": "S5Time", "TYPE_ID": "Type_Id"}
TEMPLATE_VALUES = {"date_type": ["DTL", "Date", "LDT"], "time_type": ["Time", "LTime"], "src_type": ["Time", "DTL", "Int", "Word", "Real"],
                   "dest_type": ["Time", "DTL", "Int", "Word", "Real"], "relation": ["=="], "value_type": ["Int", "Real", "Word"], "return_type": ["Int", "Word"]}
CATEGORY = [("BIT STRING", "Word"), ("BINARY", "Word"), ("FLOATING", "Real"), ("INTEGER", "Int"), ("ARRAY", "Array[0..9] of Byte")]


def pin_type(types):
    """Concrete SD data type for the type column of the parameter table."""
    types = re.sub(r"_\s+", "_", types.replace('"', ""))  # quoted system types ("AlarmData"); help text breaks lines inside names ("HW_ SUBMODULE")
    m_arr = re.match(r"\s*ARRAY\s*(?:\[[^\]]*\])?\s*(?:OF\s+)?(\w+)?", types, re.I)
    if m_arr and re.match(r"\s*ARRAY", types, re.I):
        el = SIMPLE.get((m_arr.group(1) or "BYTE").upper(), "Byte")
        bounds = re.search(r"\[\s*(\d+)\s*\.\.\s*(\d+)\s*\]", types)  # explicit bounds ("ARRAY[0..31] of UINT")
        lo, hi = bounds.groups() if bounds else ("0", "15")
        return "Array[%s..%s] of %s" % (lo, hi, el)
    t = types.replace("(", " ").replace(")", " ")
    for tok in re.split(r"[,; /]+| or ", t):
        up = tok.strip().upper()
        if up in SIMPLE:
            return SIMPLE[up]
    up = t.upper()
    for key, typ in CATEGORY:
        if key in up:
            return typ
    m = re.match(r"\s*([A-Za-z_][A-Za-z0-9_]*)", t)
    return m.group(1) if m else "Int"


def build(lang, tag, title, name, params, tv=None, pragma=None, extra_pins=(), over=None, inst=None, sclopt=None):
    """FB source with one variable per pin and a single call network."""
    secs = {"Input": [], "Output": [], "InOut": []}
    pins = []
    rets = []
    seen = set()
    for p in params:
        n = re.split(r"\s+[(*]", p["name"])[0].strip() if re.search(r"\s+[(*]", p["name"]) and not p["name"].startswith("EN") else p["name"]  # 'DispClassNr (Display class number)'
        if lang == "SCL" and n.startswith("<"):
            n = "IN"
        if lang == "SCL" and n == "INn":
            continue
        if n in ("EN", "ENO") or n.startswith("<") or re.search(r"[^A-Za-z0-9_]", n) or n.lower() in seen:
            continue
        seen.add(n.lower())
        decl = p["decl"] if p["decl"] in secs else ("Output" if p["decl"] == "Return" else "Input")
        if p["decl"] == "Return":
            rets.append(n)
        typ = pin_type(p["types"])
        if over and n.lower() in over:
            decl, typ = over[n.lower()]
        var = "v_" + n.lower()
        if decl == "Lit":  # the pin takes a constant only: no variable
            pins.append((n, "Input", "=" + typ))
            continue
        if typ.startswith("@db:"):  # element of an array member of the non-optimized global DB_Arr
            pins.append((n, "InOut" if decl == "InOut" else decl, '="DB_Arr".%s[0]' % typ[4:]))
            continue
        if typ.startswith("@elem:"):  # the pin takes an array element: declare an array, pass element 0
            secs[decl].append((var, "Array[0..9] of " + typ[6:]))
            pins.append((n, decl, var + "[0]"))
            continue
        secs[decl].append((var, typ))
        pins.append((n, decl, var))
    for n, decl, typ in extra_pins:
        if n.lower() not in seen:
            seen.add(n.lower())
            if over and n.lower() in over:
                decl, typ = over[n.lower()]
            if decl == "Lit":
                pins.append((n, "Input", "=" + typ))
                continue
            secs[decl].append(("v_" + n.lower(), typ))
            pins.append((n, decl, "v_" + n.lower()))
    if lang == "SCL" and not inst and not any(d == "Output" for _, d, _ in pins) and not rets:  # a function without declared result
        first = next(((v, t) for sec_ in ("Input", "InOut") for v, t in secs[sec_]), ("v_in", "Int"))
        secs["Output"].append(("v_ret", first[1]))
        pins.append(("RET", "Output", "v_ret"))
        rets.append("RET")
    iface = ""
    for sec, kw in (("Input", "VAR_INPUT"), ("Output", "VAR_OUTPUT"), ("InOut", "VAR_IN_OUT")):
        if secs[sec]:
            iface += "    %s%s%s    END_VAR%s" % (kw, NL, "".join("        %s : %s;%s" % (v, t, NL) for v, t in secs[sec]), NL)
    args = ", ".join(("%s %s %s" % (n, "=>" if d == "Output" else ":=", v[1:] if v.startswith("=") else "#" + v)) for n, d, v in pins)
    call = ("#inst.%s( %s )" if inst else "%s( %s )") % (name, args)
    if inst:
        iface += "    VAR%s        inst : %s;%s    END_VAR%s" % (NL, inst, NL, NL)
    pre = ("{ S7_Templates := \"%s\" }" % pragma + NL) if pragma else ""
    head = "RUNG wire#powerrail" if lang == "LAD" else "RUNG"
    fb = "FB_%s_%s" % (lang.title(), tag)
    if lang == "SCL":
        src = '{' + NL + '    S7_EditorMode := "SCL";' + NL + '    S7_Optimized := "TRUE";' + NL + '    S7_Version := "0.1"' + NL + '}' + NL
        src += 'FUNCTION_BLOCK "%s"' % fb + NL + iface + '    { S7_Language := "SCL" }' + NL
    else:
        src = '{' + NL + '    S7_Optimized := "TRUE";' + NL + '    S7_PreferredLanguage := "%s"' % lang + NL + '}' + NL
        src += 'FUNCTION_BLOCK "%s"' % fb + NL + iface + '    {' + NL + '        S7_Language := "%s";' % lang + NL + '        S7_NetworkTitle := "MLC_N1"' + NL + '    }' + NL
    if lang == "SCL":
        sclopt = sclopt or {}
        if sclopt.get("ret") and not rets:
            rets = [sclopt["ret"]]
        arg = lambda n, d, v: (v[1:] if v.startswith("=") else "#" + v) if sclopt.get("positional") and d != "Output" else "%s %s %s" % (n, "=>" if d == "Output" else ":=", v[1:] if v.startswith("=") else "#" + v)  # noqa: E731
        sargs = ", ".join(arg(n, d, v) for n, d, v in pins if n not in rets)
        retv = next((v for n, d, v in pins if n in rets), None)
        stmt = ("#inst(%s);" % sargs) if inst else (("#%s := " % retv if retv else "") + "%s(%s);" % (name, sargs))
        src += "    NETWORK" + NL + "        " + stmt + NL + "    END_NETWORK" + NL + "END_FUNCTION_BLOCK" + NL
    else:
        src += "    NETWORK" + NL + "        " + head + NL + ("            " + pre.strip() + NL if pre else "") + "            " + call + NL + "        END_RUNG" + NL + "    END_NETWORK" + NL + "END_FUNCTION_BLOCK" + NL
    clean = title.replace(":", " -").replace("'", "").replace('"', "")
    res = "MultiLingualTexts:" + NL + "  - id: MLC_N1" + NL + "    en-US: '%s'" % clean + NL
    return fb, src, res, pins


LITERAL = {"bool": "TRUE", "usint": "1", "uint": "1", "int": "1", "udint": "1", "dint": "1", "word": "W#16#1", "byte": "B#16#1", "dword": "DW#16#1",
           "real": "1.0", "time": "T#1S", "string": "'a'", "char": "'a'", "sint": "1", "lint": "1", "ulint": "1"}
ALT_VARIANT = ["Array[0..99] of Byte", "Word", "Byte", "String", "DInt"]


def repair(msg, pins, over, tried):
    """Adjust the pin declaration/type overrides from TIA's message. Returns True if something changed."""
    changed = False
    names = [n.lower() for n, _, _ in pins]
    decl = {n.lower(): d for n, d, _ in pins}

    def cur(pin):
        o = over.get(pin)
        return (o[0] if o else decl[pin], (o[1] if o and o[1] else pin_types.get(pin) or "Int"))

    def setp(pin, d=None, t=None):
        nonlocal changed
        c = cur(pin)
        new = (d or c[0], t or c[1])
        if new != c and (pin, new) not in tried:
            tried.add((pin, new))
            over[pin] = new
            changed = True

    for m in re.finditer(r"'#v_(\w+)' tag is declared as an input", msg):
        if m.group(1) in names:
            setp(m.group(1), d="InOut")
    for m in re.finditer(r"data type (.+?) of the actual parameter does not match the data type (\w+) of the formal", msg):
        x, y = m.group(1).lower(), m.group(2)
        for n in names:
            if cur(n)[1].lower() == x and (x != "variant" or y.lower() != "variant"):
                setp(n, t=y)
    for m in re.finditer(r"[Dd]ata type (\w+) cannot be implicitly converted to data type (\w+)", msg):
        x, y = m.group(1).lower(), m.group(2)
        for n in names:
            if cur(n)[1].lower() == x:
                setp(n, t=y)
    for m in re.finditer(r"[Dd]ata type (Variant|Any|Parameter|Pointer) is (?:not allowed here|unknown)", msg):
        x = m.group(1).lower()
        for n in names:
            d, t = cur(n)
            if t.lower() == x:
                if d != "InOut":
                    setp(n, d="InOut")
                else:
                    alt = [a for a in ALT_VARIANT if (n, (d, a)) not in tried]
                    if alt:
                        setp(n, t=alt[0])
    for m in re.finditer(r"Data type (\w+) is unknown", msg):
        x = m.group(1).lower()
        for n in names:
            d, t = cur(n)
            if t.lower() == x:
                alt = [a for a in ("Variant", "DB_ANY", "UInt", "Word") if (n, (d, a)) not in tried]
                if alt:
                    setp(n, t=alt[0])
    for m in re.finditer(r"Operand '#v_(\w+)' is not valid for this instruction", msg):
        n = m.group(1)
        if n in names:
            d, t = cur(n)
            lit = LITERAL.get(t.lower(), "0")
            setp(n, d="Lit", t=lit)
    if not changed and "missing or has an incorrect data type" in msg:
        for n in names:
            d, t = cur(n)
            if t.lower() == "variant":
                alt = [a for a in ALT_VARIANT if (n, (d, a)) not in tried]
                if alt:
                    setp(n, t=alt[0])
                    break
    return changed


pin_types = {}


def attempt(c, lang, tag, item, name, outdir, init_over=None, verbose=False, init_inst=None):
    """Import with repairs; returns result dict."""
    errs, tv, pragma, combos = [], None, None, []
    params = item["params"]
    extra = []
    over, tried = dict(init_over or {}), set()
    inst = init_inst
    sclopt = {}
    pin_types.clear()
    for p in params:
        pin_types[p["name"].lower()] = pin_type(p["types"])
    def evaluate(ov):
        """Import with the overrides ov; (error count, message); the block is deleted again."""
        fb_, src_, res_, _p = build(lang, tag, item["title"].split(" (S7")[0], name, params, tv, pragma, extra, ov, inst, sclopt)
        pth = os.path.join(outdir, fb_ + ".s7dcl")
        open(pth, "w", encoding="utf-8").write(src_)
        open(pth[:-6] + ".s7res", "w", encoding="utf-8").write(res_)
        e_, r_ = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=pth, waitMs="45000")
        if e_:
            return 99, gen_lad.condense(r_ if isinstance(r_, str) else json.dumps(r_))
        comp_ = r_.get("compile") or {}
        m_ = "; ".join(x["description"] for x in comp_.get("messages", []) if x.get("state") == "Error")
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb_)
        return comp_.get("errorCount", 0), m_

    for _ in range(40):
        fb, src, res, pins = build(lang, tag, item["title"].split(" (S7")[0], name, params, tv, pragma, extra, over, inst, sclopt)
        path = os.path.join(outdir, fb + ".s7dcl")
        open(path, "w", encoding="utf-8").write(src)
        open(path[:-6] + ".s7res", "w", encoding="utf-8").write(res)
        e, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=path, waitMs="45000")
        if verbose:
            print("  try over=%s pragma=%s ->" % (over, pragma), (gen_lad.condense(r if isinstance(r, str) else json.dumps(r)) if e else "imported"), flush=True)
        if e:
            msg = gen_lad.condense(r if isinstance(r, str) else json.dumps(r))
            errs.append(msg)
            m = re.search(r"Pin '(\w+)' connection is missing", msg)
            if m and not any(p[0].lower() == m.group(1).lower() for p in pins):
                extra.append((m.group(1), "InOut", "Variant"))
                continue
            m = re.search(r"Allowed Template Names: '([^']*)'", msg)
            if m and not combos:
                names = [x.strip() for x in m.group(1).split(",") if x.strip()]
                lists = [TEMPLATE_VALUES.get(n.lower(), ["Int", "Word", "Real", "Time", "DTL"]) for n in names]
                combos = ["[" + ", ".join("%s := %s" % (n, v) for n, v in zip(names, vals)) + "]" if len(names) > 1 else "%s := %s" % (names[0], vals[0])
                          for vals in itertools.islice(itertools.product(*lists), 24)]
            if combos and "Invalid Template Types" in msg or (combos and "Template" in msg):
                if combos:
                    pragma = combos.pop(0)
                    continue
            if repair(msg, pins, over, tried):
                continue
            break
        comp = r.get("compile") or {}
        if comp.get("errorCount", 0) == 0:
            return {"ok": True, "typed": True, "block": fb, "body": [name + " pins=" + ",".join(p[0] for p in pins)]}
        msg = "; ".join(m["description"] for m in comp.get("messages", []) if m.get("state") == "Error")
        if verbose:
            print("  compile:", msg[:300], flush=True)
        errs.append(msg[:300])
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
        if "Please select a data type" in msg and not pragma:
            pragma = "SrcType := Int"
            continue
        if repair(msg, pins, over, tried):
            continue
        if ("Only array elements are permitted" in msg or "optimized memory area" in msg) and not sclopt.get("elem_tried"):
            sclopt["elem_tried"] = True  # which pins? try them one by one, keep a variant when the error count drops
            base_n = len(msg.split(";")) - 1
            variants = (lambda t: ["@elem:" + t, "@elem:Int", "@elem:Byte"]) if "Only array" in msg else (lambda t: ["@db:a", "@db:b", "@db:w", "@db:by"])
            cur_n = comp.get("errorCount", 99)
            for n_, d_, v_ in list(pins):
                if v_.startswith("=") or v_.startswith("@"):
                    continue
                for var_ in variants(pin_types.get(n_.lower(), "Int")):
                    trial = dict(over)
                    trial[n_.lower()] = (d_, var_)
                    cnt, mm = evaluate(trial)
                    if cnt < cur_n:
                        over, cur_n, msg = trial, cnt, mm
                        break
                if cur_n == 0:
                    break
            if cur_n == 0:
                continue  # the final build below succeeds
        if lang == "SCL" and "do not match the transferred formal parameters" in msg and sclopt.get("positional") and not sclopt.get("named_tried"):
            sclopt["positional"], sclopt["named_tried"] = False, True  # positional guess was wrong: named call
            continue
        if lang == "SCL" and "has to be used" in msg and not sclopt.get("positional") and not sclopt.get("named_tried"):  # functions take their inputs positionally
            sclopt["positional"] = True
            continue
        if lang == "SCL" and "The function returns a value" in msg and not sclopt.get("ret"):
            outs = [n for n, d, v in pins if d == "Output"]
            if outs:
                sclopt["ret"] = outs[-1]
                continue
        if lang == "SCL" and "Invalid function name" in msg:  # an FB: call it on a static instance of the instruction's type
            cands = [name, name + "_TIME", name + "_INT", name + "_DINT"]
            nxt = cands[0] if inst is None else (cands[cands.index(inst) + 1] if inst in cands and cands.index(inst) + 1 < len(cands) else None)
            if nxt:
                inst = nxt
                continue
        if inst is None and "missing or has an incorrect data type" in msg:
            inst = name  # an FB instruction: call it on a static instance variable
            continue
        break
    return {"ok": False, "typed": True, "errors": errs[-4:], "last": {"message": errs[-1] if errs else ""}}


def main(argv):
    lang, cat = argv[0].upper(), argv[1]
    sel = [a for a in argv[2:] if not a.startswith("--") and (argv.index(a) < 3 or not argv[argv.index(a) - 1].startswith("--") or argv[argv.index(a) - 1] == "--verbose")]
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d  # noqa: E731
    prefix = opt("--tag", cat[:2])
    docs = os.path.join(HERE, "..", "docs", "catalog")
    items = json.load(open(os.path.join(docs, cat + ".json"), encoding="utf-8"))
    names = json.load(open(os.path.join(docs, cat + "_names.json")))
    rp = opt("--out") or os.path.join(docs, "%s_%s_results.json" % (cat, lang.lower()))
    results = json.load(open(rp)) if os.path.isfile(rp) else {}
    outdir = os.path.join(HERE, "sources", "typed_%s_%s" % (cat, lang.lower()))
    os.makedirs(outdir, exist_ok=True)
    c = TiaClient(int(opt("--pid", default_pid())))
    init_over = {}
    for kv in (opt("--over") or "").split(","):  # pin=Decl:Type[,pin=...]
        if "=" in kv:
            k, v = kv.split("=", 1)
            d, t = v.split(":", 1)
            init_over[k.lower()] = (d, t)
    for i, it in enumerate(items):
        num = "%03d" % (i + 1)
        if sel and sel != ["all"] and num not in sel:
            continue
        key = prefix + num
        if results.get(key, {}).get("ok"):
            continue
        sd = names.get(num, {}).get("sdName")
        if not sd or (not it.get("params") and not names.get(num, {}).get("sdName")):
            results[key] = {"title": it["title"].split(" (S7")[0], "ok": False, "last": {"message": "no SD name" if not sd else "no parameter table"}}
            continue
        r = attempt(c, lang, key, it, sd, outdir, init_over, "--verbose" in argv, opt("--inst"))
        r["title"] = it["title"].split(" (S7")[0]
        if opt("--sample") and r.get("ok"):
            r["sample"] = opt("--sample")
        results[key] = r
        print(key, r["title"][:40], "ok" if r["ok"] else "FAIL " + (r["errors"][-1][:160] if r.get("errors") else ""), flush=True)
        json.dump(results, open(rp, "w"), indent=1, sort_keys=True)
    json.dump(results, open(rp, "w"), indent=1, sort_keys=True)


if __name__ == "__main__":
    main(sys.argv[1:])
