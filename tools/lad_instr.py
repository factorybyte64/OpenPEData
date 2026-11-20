"""Candidate SD rungs per instruction for tools/gen_lad.py. '{R}' = rung header (LAD: RUNG wire#powerrail, FBD: RUNG)."""


def rung(*lines):
    return "{R}\n" + "\n".join(lines) + "\nEND_RUNG"


def box(call, pre=None):
    """Box in a rung of its own: LAD from the power rail (EN), FBD with open EN."""
    lines = ([pre] if pre else []) + [call]
    return rung(*lines)


def T(tmpl):
    return '{ S7_Templates := "%s" }' % tmpl


INSTR = {}


def add(name, *cands):
    INSTR[name] = list(cands)


# ---- math (S7_Templates SrcType) ----
for nm, sd in (("ADD", "Add"), ("SUB", "Sub"), ("MUL", "Mul"), ("DIV", "Div"), ("MOD", "Mod")):
    add(nm, box("%s( in1 := #ia, in2 := #ib, out => #io )" % sd, T("SrcType := Int")),
        box("%s( in1 := #ia, in2 := #ib, out => #io )" % sd.upper(), T("SrcType := Int")))
add("NEG", box("Neg( in := #ia, out => #io )", T("SrcType := Int")))
add("INC", box("Inc( in_out := #iinout )", T("SrcType := Int")), box("Inc( #iinout )", T("SrcType := Int")))
add("DEC", box("Dec( in_out := #iinout )", T("SrcType := Int")), box("Dec( #iinout )", T("SrcType := Int")))
add("ABS", box("Abs( in := #ia, out => #io )", T("SrcType := Int")))
add("MIN", box("Min( in1 := #ia, in2 := #ib, out => #io )", T("SrcType := Int")))
add("MAX", box("Max( in1 := #ia, in2 := #ib, out => #io )", T("SrcType := Int")))
add("LIMIT", box("Limit( mn := #ia, in := #ib, mx := #ic, out => #io )", T("SrcType := Int")))
add("MOVE", box("Move( in := #ia, out1 => #io )"))
for nm, sd in (("SQR", "Sqr"), ("SQRT", "Sqrt"), ("LN", "Ln"), ("EXP", "Exp"), ("SIN", "Sin"), ("COS", "Cos"),
               ("TAN", "Tan"), ("ASIN", "Asin"), ("ACOS", "Acos"), ("ATAN", "Atan"), ("FRAC", "Frac")):
    add(nm, box("%s( in := #ra, out => #rout )" % sd, T("SrcType := Real")))


# ---------------------------------------------------------------------------------------------------------------------
# automatic candidates from the parameter tables of the catalog (docs/catalog/<lang>.json)
import itertools  # noqa: E402
import json  # noqa: E402
import os  # noqa: E402
import re  # noqa: E402

CATALOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "docs", "catalog")
IN_VAR = {"bool": ["#a", "#b", "#c"], "int": ["#ia", "#ib", "#ic"], "dint": ["#da", "#db"], "real": ["#ra", "#rb"], "word": ["#wa", "#wb"],
          "time": ["#ta", "#tb"], "str": ["#s1", "#s2"], "byte": ["#ba"], "dword": ["#dwa"]}
OUT_VAR = {"bool": "#q", "int": "#io", "dint": "#dout", "real": "#rout", "word": "#wout", "time": "#tout", "str": "#sout",
           "byte": "#bout", "dword": "#dwout"}


def kind_of(types):
    t = types.upper()
    if t.strip() == "BOOL":
        return "bool"
    if "STRING" in t and "INT" not in t:
        return "str"
    if "TIME" in t and "INT" not in t:
        return "time"
    if "FLOATING" in t and "INTEGER" not in t:
        return "real"
    if "INTEGER" in t or t.startswith("INT") or "DINT" in t:
        return "int"
    if "BINARY" in t or "WORD" in t or "BYTE" in t:
        return "word"
    return "int"


def pascal(name):
    parts = re.split(r"[_ ]+", name)
    return "".join(p[:1].upper() + p[1:].lower() if p.isupper() or p.islower() else p for p in parts if p)


def pascal_acronym(name):
    """SetBF style: first parts capitalised, a trailing 1-3 letter part upper case."""
    parts = [p for p in re.split(r"[_ ]+", name) if p]
    return "".join(p.upper() if (i == len(parts) - 1 and len(p) <= 3 and i > 0) else p[:1].upper() + p[1:].lower() for i, p in enumerate(parts))


def name_variants(name):
    parts = [x for x in re.split(r"[_ ]+", name) if x]
    nous = "".join(parts)
    v = [pascal(name), pascal_acronym(name), name.title().replace(" ", "_") if "_" in name else name.capitalize(), name,
         name.upper(), name.lower(), nous.capitalize(), nous.upper(), "_".join(x.upper() for x in parts), "".join(x.capitalize() for x in parts),
         parts[0].upper() + "".join(x.capitalize() for x in parts[1:]), "".join(x[:3].capitalize() for x in parts)]
    seen = []
    for x in v:
        if x.lower() not in [y.lower() for y in seen]:  # instruction names are case-insensitive in SD and SimaticML import
            seen.append(x)
    return seen


def auto(item, instance_pins=False):
    """Candidate rungs for one catalog item (dict with name and params); [] when no parameter table is known."""
    ps = item.get("params")
    if not ps or not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", item["name"]):
        return []
    pinsets = []
    for style in (str.lower, str):
        pins, used = [], {}
        for p in ps:
            n = p["name"]
            if n in ("EN", "ENO") or n.startswith("<") or n.endswith("n") and n[:-1].upper() == "IN":
                continue
            k = kind_of(p["types"])
            if p["decl"] == "Output":
                pins.append("%s => %s" % (style(n), OUT_VAR[k]))
            else:
                i = used.get(k, 0)
                used[k] = i + 1
                pins.append("%s := %s" % (style(n), IN_VAR[k][min(i, len(IN_VAR[k]) - 1)]))
        if pins not in pinsets:
            pinsets.append(pins)
    kinds = {kind_of(p["types"]) for p in ps if p["name"] not in ("EN", "ENO") and not p["name"].startswith("<")}
    pragmas = [None]
    if any(("," in p["types"] or "numbers" in p["types"].lower()) for p in ps):
        pragmas = [T("SrcType := Int"), T("[SrcType := Int, DestType := Real]"), T("SrcType := Real"), None]
    cands = []
    for pins in pinsets:
        for pr, nm in itertools.product(pragmas, name_variants(item["name"])):
            cands.append(box("%s( %s )" % (nm, ", ".join(pins)), pr))
    return cands


# ---------------------------------------------------------------------------------------------------------------------
# hand-written candidates (prefix of the catalog title -> rungs); LAD and FBD share boxes, symbolic contacts/coils differ
MANUAL = []


def man(prefix, *cands, lang=None):
    MANUAL.append((prefix, lang, list(cands)))


def to_fbd(body):
    """LAD rung with a leading Contact( X ) -> FBD rung with X as first input."""
    NLC = chr(10)
    lines = body.split(NLC)
    if len(lines) > 1 and lines[0] == "{R}" and lines[1].startswith("Contact( ") and lines[1].endswith(" )"):
        first = lines[1][len("Contact( "):-2]
        return "RUNG %s" % first + NLC + NLC.join(lines[2:])
    return body


def manual(title, lang):
    out = []
    for prefix, lg, cands in MANUAL:
        if not title.startswith(prefix):
            continue
        if lg is None or lg == lang:
            out += [to_fbd(c) if lang == "FBD" else c for c in cands]
        elif lang == "FBD" and lg == "LAD" and prefix[0].isalpha():
            out += [to_fbd(c) for c in cands]  # box instructions defined for LAD: first input moves into the RUNG line
    return out


def lad(*lines):
    return "{R}\n" + "\n".join(lines) + "\nEND_RUNG"


def fbd(first, *lines):
    return "RUNG %s\n" % first + "\n".join(lines) + "\nEND_RUNG"


TIMEPRAGMA = T("time_type := Time")
# --- bit logic (LAD)
man("---|   |---", lad("Contact( #a )", "Coil( #q )"), lang="LAD")
man("---| / |---", lad("I_Contact( #a )", "Coil( #q )"), lang="LAD")
man("--|NOT|--", lad("Contact( #a )", "Not()", "Coil( #q )"), lad("Contact( #a )", "NOT()", "Coil( #q )"), lad("Contact( #a )", "Inv()", "Coil( #q )"), lang="LAD")
man("---(   )---", lad("Contact( #a )", "Coil( #q )"), lang="LAD")
man("--( / )--", lad("Contact( #a )", "I_Coil( #q )"), lad("Contact( #a )", "Coil_N( #q )"), lad("Contact( #a )", "NCoil( #q )"), lang="LAD")
man("---( R )---", lad("Contact( #a )", "R_Coil( #q )"), lang="LAD")
man("---( S )---", lad("Contact( #a )", "S_Coil( #q )"), lang="LAD")
man("SET_BF", lad("Contact( #a )", "SetBF( #barr[0], n := 4 )"), lad("Contact( #a )", "SetBF( #barr[0], 4 )"), lad("Contact( #a )", "Set_BF( #barr[0], 4 )"))
man("RESET_BF", lad("Contact( #a )", "ResetBF( #barr[0], n := 4 )"), lad("Contact( #a )", "ResetBF( #barr[0], 4 )"))
man("SR:", lad("Contact( #a )", "Sr( r1 := #b, operand := #stat1 )", "Coil( #q )"), lad("Contact( #a )", "S_SR( r1 := #b, operand := #stat1 )", "Coil( #q )"))
man("RS:", lad("Contact( #a )", "Rs( s1 := #b, operand := #stat1 )", "Coil( #q )"), lad("Contact( #a )", "S_RS( s1 := #b, operand := #stat1 )", "Coil( #q )"),
    lad("Contact( #a )", "R_Rs( s1 := #b, operand := #stat1 )", "Coil( #q )"))
man("--|P|--", lad("P_Contact( #a, bit := #edge1 )", "Coil( #q )"), lang="LAD")
man("--|N|--", lad("N_Contact( #a, bit := #edge1 )", "Coil( #q )"), lang="LAD")
man("--(P)--", lad("Contact( #a )", "P_Coil( #q, bit := #edge1 )"), lad("Contact( #a )", "P_Coil( operand := #q, bit := #edge1 )"), lang="LAD")
man("--(N)--", lad("Contact( #a )", "N_Coil( #q, bit := #edge1 )"), lang="LAD")
man("P_TRIG", lad("Contact( #a )", "P_Trig( #edge1 )", "Coil( #q )"), lang="LAD")
man("N_TRIG", lad("Contact( #a )", "N_Trig( #edge1 )", "Coil( #q )"), lang="LAD")
man("R_TRIG", lad("#rtrig1.R_TRIG( clk := #a, q => #q )"), lad("#rtrig1( clk := #a, q => #q )"), lad("Contact( #a )", "#rtrig1.R_TRIG()", "Coil( #q )"))
man("F_TRIG", lad("#ftrig1.F_TRIG( clk := #a, q => #q )"), lad("#ftrig1( clk := #a, q => #q )"))
# --- timers
for nm, inst in (("TP", "tp1"), ("TON", "ton1"), ("TOF", "tof1")):
    man(nm + ":", lad("Contact( #a )", TIMEPRAGMA, "#%s.%s( pt := #ta, et => #tout )" % (inst, nm), "Coil( #q )"), lang="LAD")
man("TONR:", lad("Contact( #a )", TIMEPRAGMA, "#tonr1.TONR( r := #b, pt := #ta, et => #tout )", "Coil( #q )"), lang="LAD")
# --- counters
man("CTU:", lad("Contact( #a )", T("value_type := Int"), "#ctu1.CTU( r := #b, pv := #ia, cv => #io )", "Coil( #q )"), lang="LAD")
man("CTD:", lad("Contact( #a )", T("value_type := Int"), "#ctd1.CTD( ld := #b, pv := #ia, cv => #io )", "Coil( #q )"), lang="LAD")
man("CTUD:", lad("Contact( #a )", T("value_type := Int"), "#ctud1.CTUD( cd := #b, r := #c, ld := #q2, pv := #ia, cv => #io )", "Coil( #q )"), lang="LAD")
# --- comparators
for sym, nm in (("==", "EQ"), ("<>", "NE"), (">=", "GE"), ("<=", "LE"), (">", "GT"), ("<", "LT")):
    man("CMP " + sym + ":", lad(T("SrcType := Int"), "%s_Contact( in1 := #ia, in2 := #ib )" % nm, "Coil( #q )"), lang="LAD")
man("IN_RANGE", lad(T("SrcType := Int"), "InRange( min := #ia, val := #ib, max := #ic )", "Coil( #q )"), lad(T("SrcType := Int"), "In_Range( min := #ia, val := #ib, max := #ic )", "Coil( #q )"))
man("OUT_RANGE", lad(T("SrcType := Int"), "OutRange( min := #ia, val := #ib, max := #ic )", "Coil( #q )"), lad(T("SrcType := Int"), "Out_Range( min := #ia, val := #ib, max := #ic )", "Coil( #q )"))
# --- math / conversion with templates
for nm in ("MIN", "MAX"):
    man(nm + ":", box("%s( in1 := #ia, in2 := #ib, out => #io )" % nm.capitalize(), T("SrcType := Int")),
        box("%s( in1 := #ia, in2 := #ib, out => #io )" % nm.capitalize(), T("[SrcType := Int, Card := 2]")))
man("INC:", box("Inc( #iinout )", T("SrcType := Int")), box("Inc( in_out := #iinout )", T("SrcType := Int")), box("Inc( inout := #iinout )", T("SrcType := Int")))
man("DEC:", box("Dec( #iinout )", T("SrcType := Int")), box("Dec( in_out := #iinout )", T("SrcType := Int")))
for nm, sd in (("ROUND", "Round"), ("CEIL", "Ceil"), ("FLOOR", "Floor"), ("TRUNC", "Trunc")):
    man(nm + ":", box("%s( in := #ra, out => #dout )" % sd, T("[SrcType := Real, DestType := DInt]")),
        box("%s( in := #ra, out => #io )" % sd, T("[SrcType := Real, DestType := Int]")))
man("NORM_X", box("Normalize( min := 0, value := #ia, max := 27648, out => #rout )", T("[SrcType := Int, DestType := Real]")))
man("SCALE_X", box("Scale( min := 0.0, value := #ra, max := 100.0, out => #io )", T("[SrcType := Real, DestType := Int]")), box("Scale( min := 0, value := #ra, max := 100, out => #rout )", T("[SrcType := Real, DestType := Real]")))

# --- FBD bit logic (symbolic names of the FBD catalog)
man("&", fbd("#a", "A( in2 := #b )", "Coil( #q )"), fbd("#a", "A( in2 := #b, in3 := #c )", "Coil( #q )"), lang="FBD")
man(">=1", fbd("#a", "O( in2 := #b )", "Coil( #q )"), lang="FBD")
man("X:", fbd("#a", "X( in2 := #b )", "Coil( #q )"), fbd("#a", "Xor( in2 := #b )", "Coil( #q )"), lang="FBD")
man("Insert input", fbd("#a", "A( in2 := #b, in3 := #c )", "Coil( #q )"), lang="FBD")
man("Invert RLO", fbd("#a", "Not()", "Coil( #q )"), fbd("#a", "NOT()", "Coil( #q )"), fbd("NOT #a", "Coil( #q )"), lang="FBD")
man("=:", fbd("#a", "Coil( #q )"), lang="FBD")
man("/=", fbd("#a", "I_Coil( #q )"), fbd("NOT #a", "Coil( #q )"), lang="FBD")
man("R:", fbd("#a", "R_Coil( #q )"), lang="FBD")
man("S:", fbd("#a", "S_Coil( #q )"), lang="FBD")
man("P:", fbd("#a", "P_Contact( bit := #edge1 )", "Coil( #q )"), fbd("P_Contact( #a, bit := #edge1 )", "Coil( #q )"), lang="FBD")
man("N:", fbd("#a", "N_Contact( bit := #edge1 )", "Coil( #q )"), fbd("N_Contact( #a, bit := #edge1 )", "Coil( #q )"), lang="FBD")

# --- found by interactive probing (tools/lad_dbg.py)
man("IN_RANGE", lad(T("SrcType := Int"), "InRange( min := #ia, in := #ib, max := #ic )", "Coil( #q )"))
man("OUT_RANGE", lad(T("SrcType := Int"), "OutRange( min := #ia, in := #ib, max := #ic )", "Coil( #q )"))
man("EQ_Type", lad("EQ_Type( in1 := #v1, in2 := #ia )", "Coil( #q )"), lad("EQ_TypeContact( in1 := #v1, in2 := #ia )", "Coil( #q )"))
man("NE_Type", lad("NE_Type( in1 := #v1, in2 := #ia )", "Coil( #q )"), lad("NE_TypeContact( in1 := #v1, in2 := #ia )", "Coil( #q )"))
man("EQ_ElemType", lad("EQ_ElemTypeContact( in1 := #v1, in2 := #ia )", "Coil( #q )"), lad("EQ_ElemType( in1 := #v1, in2 := #ia )", "Coil( #q )"))
man("NE_ElemType", lad("NE_ElemTypeContact( in1 := #v1, in2 := #ia )", "Coil( #q )"), lad("NE_ElemType( in1 := #v1, in2 := #ia )", "Coil( #q )"))
man("IS_NULL", lad("IS_NULL( #v1 )", "Coil( #q )"))
man("NOT_NULL", lad("NOT_NULL( #v1 )", "Coil( #q )"))
man("IS_ARRAY", lad("IS_ARRAY( #v1 )", "Coil( #q )"), lad("Is_Array( #v1 )", "Coil( #q )"))
for nm in ("And", "Or", "Xor"):
    man(nm.upper() + ":", box("%s( in1 := #wa, in2 := #wb, out => #wout )" % nm, T("SrcType := Word")))
man("DEMUX", box("Demux( k := #ia, in := #ib, out0 => #io, out1 => #io2, out2 => #io, else => #io2 )", T("SrcType := Int")))
man("SCATTER:", box("SCATTER( in := #wa, out => #bits16 )", T("src_type := Word")))
man("GATHER:", box("GATHER( in => #bits16, out => #wout )", T("src_type := Word")), box("GATHER( in := #bits16, out => #wout )", T("src_type := Word")))
man("GET_ERROR", box("GetError( error => #eout )"))

man("CTUD:", lad("Contact( #a )", T("value_type := Int"), "#ctud1.CTUD( cd := #b, r := #c, ld := #q2, pv := #ia, qu => #q, qd => #q2, cv => #io )"), lang="LAD")

man("BLKMOV", box('Blkmov( SRCBLK := "DB_Arr".a, DSTBLK => "DB_Arr".b, RET_VAL => #io )'))
man("UBLKMOV", box('Ublkmov( SRCBLK := "DB_Arr".a, DSTBLK => "DB_Arr".b, RET_VAL => #io )'))
man("CountOfElements", box("CountOfElements( in := #v1, ret_val => #udia )"))
man("LOWER_BOUND", box("Lower_Bound( arr := #arrs, dim := 1, out => #dinout )"))
man("UPPER_BOUND", box("Upper_Bound( arr := #arrs, dim := 1, out => #dinout )"))
man("VariantGet", box("VariantGet( src := #v1, dst => #ia )"))
man("VariantPut", box("VariantPut( src := #ia, dst => #v1 )"))
