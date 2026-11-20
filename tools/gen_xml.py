"""Second way to create catalog instructions: SimaticML import (Openness) instead of SD text.

For every catalog item that gen_lad.py could not create, a block is generated from the SimaticML export of an existing block of the
sample (template FB, e.g. FB_Lad_001.xml: same interface), its network replaced by a FlgNet built from the parameter table:
one <Part Name=..> with pins as NameCon, operands as <Access>. Candidates: part name variants x pin name case x template values.

Usage: python tools/gen_xml.py <LAD|FBD> <template.xml> [--cat lad] [--only 008,009] [--pid 12860]
Result keys/format as gen_lad.py (docs/catalog/<cat>_results.json); entries get "via": "xml"."""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from tiaclient import TiaClient, default_pid  # noqa: E402
import gen_lad  # noqa: E402
import lad_instr  # noqa: E402

NS = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5"
NL = chr(10)

# operand variables of the template interface (same names as gen_lad.INTERFACE)
VAR = {"bool": "a", "int": "ia", "dint": "da", "real": "ra", "word": "wa", "time": "ta", "str": "s1", "byte": "ba", "dword": "dwa"}
INOUTVAR = {"int": "iinout", "dint": "dinout", "real": "lrio"}
OUTVAR = {"bool": "q", "int": "io", "dint": "dout", "real": "rout", "word": "wout", "time": "tout", "str": "sout", "byte": "bout", "dword": "dwout"}


def access(uid, name):
    """name: interface variable (a.b), or '@T:n' / '@C:n' (S5 timer / counter address), '@K:Type:value' (literal constant)."""
    if name.startswith("@T:") or name.startswith("@C:"):
        area, typ = ("Timer", "Timer") if name[1] == "T" else ("Counter", "Counter")
        return '<Access Scope="Address" UId="%d"><Address Area="%s" Type="%s" BitOffset="%s" /></Access>' % (uid, area, typ, name[3:])
    if name.startswith("@Z:"):  # typed constant (value with type prefix)
        return '<Access Scope="TypedConstant" UId="%d"><Constant><ConstantValue>%s</ConstantValue></Constant></Access>' % (uid, name[3:])
    if name.startswith("@A:"):  # element of a local array: @A:arr:0
        _, arr, idx = name.split(":")
        return ('<Access Scope="LocalVariable" UId="%d"><Symbol><Component Name="%s"><Access Scope="LiteralConstant"><Constant><ConstantType>DInt</ConstantType>'
                '<ConstantValue>%s</ConstantValue></Constant></Access></Component></Symbol></Access>' % (uid, arr, idx))
    if name.startswith("@G:"):  # element of an array member of a global (non-optimized) DB: @G:DB:member:idx
        _, db, mem, idx = name.split(":")
        return ('<Access Scope="GlobalVariable" UId="%d"><Symbol><Component Name="%s" /><Component Name="%s"><Access Scope="LiteralConstant"><Constant><ConstantType>DInt</ConstantType>'
                '<ConstantValue>%s</ConstantValue></Constant></Access></Component></Symbol></Access>' % (uid, db, mem, idx))
    if name.startswith("@L:"):  # jump label
        return '<Access Scope="Label" UId="%d"><Label Name="%s" /></Access>' % (uid, name[3:])
    if name.startswith("@K:"):
        _, typ, val = name.split(":", 2)
        return ('<Access Scope="LiteralConstant" UId="%d"><Constant><ConstantType>%s</ConstantType><ConstantValue>%s</ConstantValue></Constant></Access>' % (uid, typ, val))
    comps = "".join('<Component Name="%s" />' % c for c in name.split("."))
    return '<Access Scope="LocalVariable" UId="%d"><Symbol>%s</Symbol></Access>' % (uid, comps)


POST = [False]
SPEC = [None]   # per-item overrides from xml_names.json: {"name", "tv": {...}, "types": {pin: datatype}}
COIL_LIKE = {"jump", "jumpnot", "return"}
INSTANCE = [None]   # type name of a static instance variable 'inst' for FB-type instructions


def flgnet(lang, part_name, version, template_values, pins, eno_off=True, en_pin="en", pre=False):
    """pins: [(pin name, 'in'|'out', variable name)]"""
    uid = 21
    accesses, wires = [], []
    part_uid = 21 + len(pins)
    for pin, direction, var in pins:
        accesses.append(access(uid, var))
        wid = part_uid + 1 + len(wires)
        if direction == "in":
            wires.append('<Wire UId="%d"><IdentCon UId="%d" /><NameCon UId="%d" Name="%s" /></Wire>' % (wid, uid, part_uid, pin))
        else:
            wires.append('<Wire UId="%d"><NameCon UId="%d" Name="%s" /><IdentCon UId="%d" /></Wire>' % (wid, part_uid, pin, uid))
        uid += 1
    wid = part_uid + 1 + len(wires)
    if pre and en_pin and lang == "LAD":  # a contact in front: coils need a preceding Boolean logic operation
        cu, au = part_uid + 50, part_uid + 51
        accesses.append(access(au, "a"))
        accesses.append('<Part Name="Contact" UId="%d" />' % cu)
        wires.insert(0, '<Wire UId="%d"><Powerrail /><NameCon UId="%d" Name="in" /></Wire>' % (wid + 60, cu))
        wires.insert(1, '<Wire UId="%d"><IdentCon UId="%d" /><NameCon UId="%d" Name="operand" /></Wire>' % (wid + 61, au, cu))
        wires.insert(2, '<Wire UId="%d"><NameCon UId="%d" Name="out" /><NameCon UId="%d" Name="%s" /></Wire>' % (wid + 62, cu, part_uid, en_pin))
    elif pre and en_pin and lang == "FBD":  # coil in FBD: the RLO comes from an operand
        au = part_uid + 51
        accesses.append(access(au, "a"))
        wires.insert(0, '<Wire UId="%d"><IdentCon UId="%d" /><NameCon UId="%d" Name="%s" /></Wire>' % (wid + 60, au, part_uid, en_pin))
    elif en_pin is None:
        pass
    elif lang == "LAD":
        wires.insert(0, '<Wire UId="%d"><Powerrail /><NameCon UId="%d" Name="%s" /></Wire>' % (wid, part_uid, en_pin))
    else:
        wires.insert(0, '<Wire UId="%d"><OpenCon UId="%d" /><NameCon UId="%d" Name="%s" /></Wire>' % (wid, wid + 1, part_uid, en_pin))
    if POST[0]:  # contact-like part: terminate the rung with a coil
        cu, qu = part_uid + 70, part_uid + 71
        accesses.append(access(qu, "q"))
        accesses.append('<Part Name="Coil" UId="%d" />' % cu)
        wires.append('<Wire UId="%d"><NameCon UId="%d" Name="out" /><NameCon UId="%d" Name="in" /></Wire>' % (wid + 80, part_uid, cu))
        wires.append('<Wire UId="%d"><IdentCon UId="%d" /><NameCon UId="%d" Name="operand" /></Wire>' % (wid + 81, qu, cu))
    tv = "".join('<TemplateValue Name="%s" Type="%s">%s</TemplateValue>' % (k, "Cardinality" if k.lower() == "card" else ("Operation" if k.upper().startswith("CASE") or k.lower() == "relation" else "Type"), v) for k, v in template_values.items())
    inst = ('<Instance Scope="LocalVariable" UId="%d"><Component Name="inst" /></Instance>' % (part_uid + 90)) if INSTANCE[0] else ""
    part = '<Part Name="%s"%s UId="%d"%s>%s%s</Part>' % (part_name, (' Version="%s"' % version) if version else "", part_uid,
                                                    ' DisabledENO="true"' if eno_off else "", inst, tv)
    return '<FlgNet xmlns="%s"><Parts>%s%s</Parts><Wires>%s</Wires></FlgNet>' % (NS, "".join(accesses), part, "".join(wires))


EXTRA = {}   # variable name -> datatype, injected into the InOut section of the template interface
TYPEOF = {"a": "Bool", "b": "Bool", "c": "Bool", "q": "Bool", "q2": "Bool", "ia": "Int", "ib": "Int", "ic": "Int", "io": "Int", "io2": "Int", "da": "DInt", "db": "DInt",
          "dout": "DInt", "ra": "Real", "rb": "Real", "rout": "Real", "wa": "Word", "wb": "Word", "wout": "Word", "dwa": "DWord", "dwout": "DWord", "ba": "Byte",
          "bout": "Byte", "ta": "Time", "tb": "Time", "tout": "Time", "s1": "String", "s2": "String", "sout": "String", "iinout": "Int"}


def build_block(template_xml, name, lang, net):
    x = template_xml
    if INSTANCE[0]:
        x = x.replace('<Section Name="Static">', '<Section Name="Static"><Member Name="inst" Datatype="%s" Version="1.0" Accessibility="Public" />' % INSTANCE[0], 1)
    if EXTRA:
        members = "".join('<Member Name="%s" Datatype="%s" Accessibility="Public" />' % kv for kv in EXTRA.items())
        x = x.replace('<Section Name="InOut">', '<Section Name="InOut">' + members, 1)
    x = re.sub(r"<NetworkSource>.*?</NetworkSource>", lambda m: "<NetworkSource>" + net + "</NetworkSource>", x, flags=re.S)
    x = re.sub(r"<Name>(?:FB|FX)_\w+</Name>", "<Name>%s</Name>" % name, x, count=1)
    x = re.sub(r"<Number>\d+</Number>", "", x)
    x = x.replace("<ProgrammingLanguage>LAD</ProgrammingLanguage>", "<ProgrammingLanguage>%s</ProgrammingLanguage>" % lang)
    return x


def timer_coil_net(nm):
    """IEC timer in coil position (---( TP )---): the timer part with instance, Q unconnected (ET connected)."""
    parts = [access(21, "a"), access(23, "ta"), access(24, "tout"), '<Part Name="Contact" UId="25" />',
             '<Part Name="%s" Version="1.0" UId="26"><Instance Scope="LocalVariable" UId="27"><Component Name="tp1" /></Instance>'
             '<TemplateValue Name="time_type" Type="Type">Time</TemplateValue></Part>' % nm]
    wires = ['<Wire UId="30"><Powerrail /><NameCon UId="25" Name="in" /></Wire>', '<Wire UId="31"><IdentCon UId="21" /><NameCon UId="25" Name="operand" /></Wire>',
             '<Wire UId="32"><NameCon UId="25" Name="out" /><NameCon UId="26" Name="IN" /></Wire>', '<Wire UId="33"><IdentCon UId="23" /><NameCon UId="26" Name="PT" /></Wire>',
             '<Wire UId="34"><NameCon UId="26" Name="ET" /><IdentCon UId="24" /></Wire>']
    if nm == "TONR":  # accumulating timer: reset input
        parts.append(access(28, "b"))
        wires.append('<Wire UId="35"><IdentCon UId="28" /><NameCon UId="26" Name="R" /></Wire>')
    return '<FlgNet xmlns="%s"><Parts>%s</Parts><Wires>%s</Wires></FlgNet>' % (NS, "".join(parts), "".join(wires))


def timer_coil(c, template, name, nm, outdir):
    path = os.path.join(outdir, name + ".xml")
    open(path, "w", encoding="utf-8").write(build_block(template, name, "LAD", timer_coil_net(nm)))
    e, r = c.json("TiaPlcSoftware", operation="import", objectKind="block", sourcePath=path, importMode="Override", waitMs="45000")
    if e:
        return False, (r if isinstance(r, str) else str(r))[-300:]
    comp = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
    errs = [m["description"] for m in (comp.get("messages") if isinstance(comp, dict) else []) if m.get("state") == "Error" and name in m.get("path", "")]
    return (not errs), ("compile: " + "; ".join(errs)[:300]) if errs else {"name": nm, "pins": ["IN", "PT", "ET"]}


def kind_pins(item):
    ps = item.get("params") or []
    pins = []
    used = {}
    for p in ps:
        n = p["name"]
        if n in ("EN", "ENO"):
            continue
        k = lad_instr.kind_of(p["types"])
        if n.startswith("<"):  # positional operands of coil-like instructions
            n = "operand" if not re.search(r"\d", n) or n.endswith("1>") else "operand%s" % re.search(r"\d+", n).group(0)
        if p["decl"] == "Output":
            pins.append((n, "out", OUTVAR[k]))
        elif p["decl"] == "InOut":
            pins.append((n, "in", INOUTVAR.get(k, "iinout")))
        else:
            used[k] = used.get(k, 0)
            pins.append((n, "in", VAR[k]))
    return pins


def candidates(item):
    base = item["name"]
    m = re.search(r"\(\s*(\w+)\s*\)", base)
    if m:
        base = m.group(1)
    names, seen = [], set()
    nous = base.replace("_", "")
    for v in [base, nous, lad_instr.pascal(base), lad_instr.pascal_acronym(base)] + lad_instr.name_variants(base):
        if re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", v) and v.lower() not in seen:
            seen.add(v.lower())
            names.append(v)
    return names


COMMON_PINS = ["en", "in", "in1", "in2", "out", "out1", "operand", "operand1", "operand2", "bit", "clk", "q", "r", "s", "s1", "r1", "pt", "et", "tv",
               "timer", "counter", "value", "min", "max", "ret_val", "k", "else", "n", "cu", "cd", "ld", "pv", "cv", "qu", "qd", "bi", "bcd"]


LABEL_NET = ('<FlgNet xmlns="%s"><Labels><LabelDeclaration UId="29"><Label Name="lbl1" /></LabelDeclaration></Labels><Parts>%s%s<Part Name="Contact" UId="23" /><Part Name="Coil" UId="24" /></Parts>'
             '<Wires><Wire UId="25"><Powerrail /><NameCon UId="23" Name="in" /></Wire><Wire UId="26"><IdentCon UId="21" /><NameCon UId="23" Name="operand" /></Wire>'
             '<Wire UId="27"><NameCon UId="23" Name="out" /><NameCon UId="24" Name="in" /></Wire><Wire UId="28"><IdentCon UId="22" /><NameCon UId="24" Name="operand" /></Wire></Wires></FlgNet>')


LABEL_NET_FBD = ('<FlgNet xmlns="%s"><Labels><LabelDeclaration UId="29"><Label Name="lbl1" /></LabelDeclaration></Labels><Parts>%s%s<Part Name="Coil" UId="24" /></Parts>'
                 '<Wires><Wire UId="26"><IdentCon UId="21" /><NameCon UId="24" Name="in" /></Wire><Wire UId="28"><IdentCon UId="22" /><NameCon UId="24" Name="operand" /></Wire></Wires></FlgNet>')


def with_label_network(x, lang="LAD"):
    """Second network that declares the label lbl1 (jump targets need a declaration in the same block)."""
    cu = re.search(r'<SW\.Blocks\.CompileUnit ID="3".*?</SW\.Blocks\.CompileUnit>', x, flags=re.S).group(0)
    net = (LABEL_NET if lang == "LAD" else LABEL_NET_FBD) % (NS, access(21, "a"), access(22, "q"))
    cu2 = re.sub(r"<NetworkSource>.*?</NetworkSource>", lambda m: "<NetworkSource>" + net + "</NetworkSource>", cu, flags=re.S)
    for old_, new_ in (('ID="3"', 'ID="20"'), ('ID="4"', 'ID="21"'), ('ID="5"', 'ID="22"'), ('ID="6"', 'ID="23"'), ('ID="7"', 'ID="24"')):
        cu2 = cu2.replace(old_, new_)
    return x.replace(cu, cu + cu2)


def try_import(c, template, name, lang, nm, pins, tv=None, en_pin="en", ver=None, outdir=".", eno=True, pre=False):
    path = os.path.join(outdir, name + ".xml")
    xml_ = build_block(template, name, lang, flgnet(lang, nm, ver, tv or {}, pins, eno, en_pin, pre))
    if any(p_[2].startswith("@L:") for p_ in pins):
        xml_ = with_label_network(xml_, lang)
    open(path, "w", encoding="utf-8").write(xml_)
    e, r = c.json("TiaPlcSoftware", operation="import", objectKind="block", sourcePath=path, importMode="Override", waitMs="45000")
    msg = (r if isinstance(r, str) else r.get("message", "")).replace(chr(13) + chr(10), " ") if e else ""
    return e, msg


def discover_pins(c, template, name, lang, nm, vocab, outdir):
    """Pin names TIA knows for the instruction nm: a pin counts as existing when importing a network that wires only this pin does not
    report 'references a connection named <pin>'."""
    found, tv = [], {}
    for pin in vocab:
        for _ in range(4):
            e, msg = try_import(c, template, name, lang, nm, [(pin, "in", "ia")], tv, None, None, outdir)
            m = re.search(r"TemplateValue' with the name '(\w+)' and the type '(\w+)' is missing", msg)
            if not m:
                break
            tv[m.group(1)] = "2" if m.group(2).lower() == "cardinality" else "Int"
        if "cannot be found" in msg:
            return None
        if not re.search(r"connection named '%s'" % re.escape(pin), msg):
            found.append(pin)
        if not e:
            c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=name)
    return found


OUTPUT_PINS = {"q", "et", "cv", "qu", "qd", "bi", "bcd", "ret_val", "out", "out1", "out2", "eno", "rt"}
PIN_VARS = {"tv": "s5a", "timer": "tmr", "counter": "cntr", "bi": "wout", "bcd": "wout2", "pv": "ia", "cv": "io", "ret_val": "io", "et": "tout",
            "pt": "ta", "value": "ia", "min": "ia", "max": "ib", "k": "ia", "n": "ia"}


WIDE = ["Int", "Word", "DInt", "DWord", "Byte", "UInt", "UDInt", "SInt", "USInt", "LInt", "ULInt", "LWord", "Real", "LReal", "Bool", "Time", "Char", "String", "Variant", "DTL", "LDT", "LTime", "Date", "Time_Of_Day", "S5Time", "HW_ANY", "HW_IO", "HW_SUBMODULE", "HW_DEVICE", "DB_ANY"]
TV_FIRST = {"id_type": ["HW_ANY", "HW_IO", "HW_SUBMODULE", "HW_DEVICE"], "laddr_type": ["HW_ANY", "HW_IO", "HW_SUBMODULE"], "ptr_type": ["DB_ANY", "Variant", "Byte", "Word"], "time_type": ["Time", "LTime"], "blk_type": ["Variant", "Byte", "Word"], "ptr_type": ["Variant", "Byte", "Word"], "src_type": ["Int", "Word", "DInt", "Real", "Byte", "DWord"],
            "dest_type": ["Int", "Word", "DInt", "Real", "Byte", "DWord", "Bool"], "value_type": ["Int", "Word", "DInt", "Byte", "DWord"]}


def tv_candidates(name):
    first = TV_FIRST.get(name.lower(), [])
    return first + [w for w in WIDE if w not in first]


OUT_OF = {"a": "q", "b": "q2", "ia": "io", "ib": "io2", "da": "dout", "ra": "rout", "wa": "wout", "dwa": "dwout", "ba": "bout", "ta": "tout", "s1": "sout"}
IN_OF = {v: k for k, v in OUT_OF.items()}
CYCLE = ["a", "wa", "dwa", "ba", "ra", "da", "ta", "s1"]


def fix_direction(msg, pins):
    """'#x tag is declared as an input, write access not permitted' / '... output, read access ...': use the variable of the other direction."""
    out, changed = list(pins), False
    if "exactly 16 elements" in msg:
        EXTRA["x_barr16"] = "Array[0..15] of Bool"
        for k, p in enumerate(out):
            if p[2] == "barr":
                out[k] = (p[0], p[1], "x_barr16")
                changed = True
    for m in re.finditer(r"'#(\w+)' tag is declared as an (input|output)", msg):
        v, kind = m.group(1), m.group(2)
        order = list(reversed(list(enumerate(out)))) if kind == "input" else list(enumerate(out))  # results are written by the later pins
        for k, p in order:
            if p[2] == v and ((kind == "input" and v in OUT_OF) or (kind == "output" and v in IN_OF)):
                out[k] = (p[0], p[1], OUT_OF[v] if kind == "input" else IN_OF[v])
                changed = True
                break
    for m in re.finditer(r"Data type (\w+) is not permitted here", msg):
        t = m.group(1).lower()
        for k, p in enumerate(out):
            if TYPEOF.get(p[2], "").lower() == t:
                nxt = "barr" if p[2] == "a" and t == "bool" and not any(q[2] == "barr" for q in out) else (CYCLE[(CYCLE.index(p[2]) + 1) % len(CYCLE)] if p[2] in CYCLE else (("wout" if t != "word" else "q") if p[1] == "out" else ("wa" if t != "word" else "a")))
                out[k] = (p[0], p[1], nxt)
                changed = True
                break
    return out if changed else None


def fix_types(msg, pins):
    """Compile messages 'data type A of the actual parameter does not match the data type B of the formal parameter': give the pins that use a
    variable of type A a new InOut variable of type B. Returns the new pin list or None."""
    out, changed = list(pins), False
    for m in re.finditer(r"data type ([\w\[\]\.]+) of the actual parameter does not match the data type ([\w]+)", msg):
        a, b = m.group(1).lower(), m.group(2)
        for k, p in enumerate(out):
            if TYPEOF.get(p[2], EXTRA.get(p[2], "")).lower() == a and not p[2].startswith("x_"):
                v = "x_" + b.lower()
                EXTRA[v] = b
                out[k] = (p[0], p[1], v)
                changed = True
                break
    return out if changed else None


def count_errors(c, template, name, lang, nm, pins, tv, en_pin, ver, outdir, eno, pre):
    """Import + compile with the given pins; (number of error messages of the block, list)."""
    e, msg = try_import(c, template, name, lang, nm, pins, tv, en_pin, ver, outdir, eno, pre)
    if e:
        return 999, [msg]
    comp = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
    errs = [m["description"] for m in (comp.get("messages") if isinstance(comp, dict) else []) if m.get("state") == "Error" and name in m.get("path", "")]
    c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=name)
    return len(errs), errs


def trial_types(c, template, name, lang, nm, pins, tv, en_pin, ver, outdir, eno, pre, errs):
    """For every 'data type A of the actual parameter does not match the data type B of the formal parameter' try the pins of type A one by one."""
    best, best_n = None, len(errs)
    for m in re.finditer(r"data type (.+?) of the actual parameter does not match the data type (\w+) of the formal", "; ".join(errs)):
        a, b = m.group(1).lower(), m.group(2)
        for k, p in enumerate(pins):
            cur_t = EXTRA.get(p[2]) or TYPEOF.get(p[2], "")
            if cur_t.lower() != a or p[2] == "x_" + b.lower():
                continue
            v = "x_" + b.lower()
            EXTRA[v] = b
            trial = list(best or pins)
            trial[k] = (p[0], p[1], v)
            n, e2 = count_errors(c, template, name, lang, nm, trial, tv, en_pin, ver, outdir, eno, pre)
            if n < best_n:
                best, best_n = trial, n
                if n == 0:
                    return best
    return best


def forced_var(nm, pin):
    """Operands for S5 timers/counters (address T1/C1, S5Time/Word constants) when the part name is known."""
    low, pl = nm.lower(), pin.lower()
    if pl == "label" and low in COIL_LIKE:
        return "@L:lbl1"
    if low in ("jmplist", "switch") and (re.fullmatch(r"dest\d*", pl) or pl == "else"):
        return "@L:lbl1"
    timer = low in ("spcoil", "secoil", "sdcoil", "sscoil", "sfcoil", "s_pulse", "s_pext", "s_odt", "s_odts", "s_offdt")
    counter = low in ("sccoil", "cucoil", "cdcoil", "s_cu", "s_cd", "s_cud")
    if low in ("sbitfield", "rbitfield") and pl == "n":
        return "@K:UInt:4"
    if low in ("readfromarraydbl", "writetoarraydbl", "readfromarraydb", "writetoarraydb") and pl == "value":
        return "iinout"
    if low == "sccoil" and pl == "value":
        return "wa"
    if pl in ("operand", "timer", "t_no") and timer:
        return "@T:1"
    if pl in ("operand", "counter", "c_no") and counter:
        return "@C:1"
    if pl in ("value", "tv") and timer:
        return "@Z:S5T#2S"
    if pl in ("value", "pv", "cv") and counter:
        return "@Z:W#16#10" if pl != "cv" else None
    return None


def guess_pin(pn):
    low = pn.lower()
    direction = "out" if low in OUTPUT_PINS or low.startswith("out") else "in"
    var = PIN_VARS.get(low) or ("q" if direction == "out" else "a")
    return direction, var


def adapt(c, lang, template, name, nm, pins0, outdir, log, forced=False, ptypes=None):
    """Import one part name: pin names are discovered first, then template values and operand wiring are repaired from TIA's messages.
    Returns (ok, info | message)."""
    pins0 = [(("operand" if p[0] == "IN/OUT" else re.sub(r"\s+", "", re.split(r"\s+\(", p[0])[0])),) + tuple(p[1:]) for p in pins0 if not re.search(r"[,*]", p[0]) and not p[0].startswith("EN")]
    vocab = []
    for p in pins0:
        for v in (p[0], p[0].lower()):
            if v not in vocab:
                vocab.append(v)
    vocab += [v for v in COMMON_PINS if v not in vocab]
    POST[0] = bool((forced and nm.lower() in ("ok", "notok")) or (SPEC[0] or {}).get("post"))
    if forced:  # internal name known from the DLL: skip the pin discovery, repair the wiring from TIA's messages
        found = [p[0] for p in pins0] + ["in"]
    else:
        found = discover_pins(c, template, name, lang, nm, vocab, outdir)
    if found is None:
        return False, "unknown instruction name"
    if not found:
        return False, "no pin of the vocabulary exists"
    by = {p[0].lower(): p for p in pins0}
    pins = []
    for f in found:
        if f.lower() in ("en", "eno") or (forced and f.lower() == "in" and f == found[-1]) or (forced and nm.lower() in ("ok", "notok") and f.lower() == "pre"):
            continue
        base = by.get(f.lower())
        var = PIN_VARS.get(f.lower()) or (base[2] if base else guess_pin(f)[1])
        if forced:
            var = forced_var(nm, f) or var
        pins.append((f, base[1] if base else guess_pin(f)[0], var))
    seen_pins, dedup = set(), []
    for p_ in pins:
        if p_[0].lower() not in seen_pins:
            seen_pins.add(p_[0].lower())
            dedup.append(p_)
    pins = dedup
    if ptypes:  # typed mode: one InOut variable per pin with the data type of the parameter table
        import gen_typed
        for k_, (pn_, dn_, vn_) in enumerate(pins):
            tt = ptypes.get(pn_.lower())
            if tt:
                v_ = "p_" + re.sub(r"\W", "", pn_.lower())
                EXTRA[v_] = gen_typed.pin_type(tt)
                pins[k_] = (pn_, dn_, v_)
    en_pin = "en" if "en" in found else ("in" if "in" in found and not any(p[0] == "in" for p in pins) else None)
    if SPEC[0] and SPEC[0].get("en"):
        en_pin = SPEC[0]["en"]
    if forced:
        en_pin = "pre" if nm.lower() in ("ok", "notok") else ("in" if (nm.lower().endswith("coil") or nm.lower() in COIL_LIKE) else "en")
    if en_pin == "in":
        pass
    tv, ver, msg, eno = dict((SPEC[0] or {}).get("tv", {})), (SPEC[0] or {}).get("ver"), "", True
    if (SPEC[0] or {}).get("post"):
        pins = [p_ for p_ in pins if p_[0].lower() != "out"]
    for pn_, tt_ in ((SPEC[0] or {}).get("types", {})).items():  # explicit pin types: one InOut variable each
        for k_, p_ in enumerate(pins):
            if p_[0].lower() == pn_.lower():
                v_ = "s_" + re.sub(r"\W", "", pn_.lower())
                if tt_.startswith("@A:"):  # pin takes element 0 of an array of this element type
                    EXTRA[v_] = "Array[0..9] of " + tt_[3:]
                    pins[k_] = (p_[0], p_[1], "@A:%s:0" % v_)
                    continue
                EXTRA[v_] = tt_
                pins[k_] = (p_[0], p_[1], v_)
    seen_cfg = set()
    for _ in range(100):
        e, msg = try_import(c, template, name, lang, nm, pins, tv, en_pin, ver, outdir, eno, forced and (nm.lower().endswith("coil") or nm.lower() in COIL_LIKE))
        if not e:
            comp = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
            errs = [m["description"] for m in (comp.get("messages") if isinstance(comp, dict) else []) if m.get("state") == "Error" and name in m.get("path", "")]
            if isinstance(comp, dict) and not errs:  # only messages of the new block count (other blocks of the scratch project keep known errors)
                return True, {"name": nm, "pins": [p[0] for p in pins], "enPin": en_pin, "templates": tv}
            msg = "compile: " + "; ".join(errs)[:300]
            c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=name)
            if os.environ.get("GEN_DEBUG"):
                print("   [%s] pins=%s tv=%s -> %s" % (nm, [(p_[0], p_[2]) for p_ in pins], tv, "; ".join(errs)[:160]), flush=True)
            if True:  # find the pin of every type mismatch by trial (the message does not name it)
                pins_new = trial_types(c, template, name, lang, nm, pins, tv, en_pin, ver, outdir, eno, forced and nm.lower().endswith("coil"), errs)
                if pins_new:
                    pins = pins_new
                    continue
            fixed = fix_types("; ".join(errs), pins) or fix_direction("; ".join(errs), pins)
            cfg = (tuple(p_[2] for p_ in fixed), tuple(sorted(tv.items()))) if fixed else None
            if fixed and cfg not in seen_cfg:
                seen_cfg.add(cfg)
                pins = fixed
                continue
            if "Only array elements are permitted" in msg or "optimized memory area" in msg:  # which pin? try them one by one, keep the change when the message shrinks
                base = len(errs)
                variants = ["@A:arr:0", "@A:barr:0"] if "Only array elements" in msg else ["@G:DB_Arr:a:0", "@G:DB_Arr:b:0"]
                for k, p_ in enumerate(pins):
                    if p_[2].startswith("@"):
                        continue
                    for arr in variants:
                        trial = list(pins)
                        trial[k] = (p_[0], p_[1], arr)
                        e2, m2 = try_import(c, template, name, lang, nm, trial, tv, en_pin, ver, outdir, eno, forced and (nm.lower().endswith("coil") or nm.lower() in COIL_LIKE))
                        if e2:
                            continue
                        comp2 = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
                        n2 = [m_["description"] for m_ in (comp2.get("messages") if isinstance(comp2, dict) else []) if m_.get("state") == "Error" and name in m_.get("path", "")]
                        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=name)
                        if len(n2) < base:
                            pins, base, errs = trial, len(n2), n2
                            break
                if base == 0:
                    e3, m3 = try_import(c, template, name, lang, nm, pins, tv, en_pin, ver, outdir, eno, forced and (nm.lower().endswith("coil") or nm.lower() in COIL_LIKE))
                    comp3 = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
                    return True, {"name": nm, "pins": [p[0] for p in pins], "enPin": en_pin, "templates": tv}
                if base < len(errs) or fixed is None and base != len(errs):
                    pass
            if forced and INSTANCE[0] is None and "operand required at the input or output is missing" in msg:
                INSTANCE[0] = nm
                continue
            if "Please select a data type" in msg and not tv:
                tv = {"SrcType": "Int"}
                continue
            if "Indirect access" in msg and not any(p[2] == "iinout" for p in pins):
                pins = [(p[0], p[1], "iinout" if p[2] == "ia" else p[2]) for p in pins]
                continue
            return False, msg
        m = re.search(r"TemplateValue' with the name '(\w+)' and the type '(\w+)' is missing", msg)
        if m:
            tv[m.group(1)] = "2" if m.group(2).lower() == "cardinality" else ("Eq" if m.group(2).lower() == "operation" else tv_candidates(m.group(1))[0])
            continue
        m = re.search(r"TemplateValue' with the name '(\w+)' and the type 'cardinality' has an invalid value '(\d+)'", msg)
        if m and m.group(2) == "2":
            tv[m.group(1)] = "1"
            continue
        m = re.search(r"TemplateValue' with the name '(\w+)' and the type 'operation' has an invalid value", msg)
        if m:
            cur = tv.get(m.group(1))
            nxt = {"Eq": "Ne", "Ne": "Lt", "Lt": "Gt"}.get(cur)
            if nxt:
                tv[m.group(1)] = nxt
                continue
            continue
        m = re.search(r"TemplateValue' with the name '(\w+)' and the type '\w+' has an invalid value '(\w+)'", msg)
        if m:
            cands = tv_candidates(m.group(1))
            nxt = cands[cands.index(m.group(2)) + 1] if m.group(2) in cands and cands.index(m.group(2)) + 1 < len(cands) else None
            if nxt:
                tv[m.group(1)] = nxt
                continue
        if forced and INSTANCE[0] is None and "operand required at the input or output is missing" in msg:
            INSTANCE[0] = nm  # FB-type instruction: call it on a static instance
            continue
        m_np = re.search(r"Data type (\w+) is not permitted here", msg)
        if m_np:  # the template type itself may be the culprit: next candidate
            for k_, v_ in list(tv.items()):
                if v_.lower() == m_np.group(1).lower():
                    cands_ = tv_candidates(k_)
                    nx_ = cands_[cands_.index(v_) + 1] if v_ in cands_ and cands_.index(v_) + 1 < len(cands_) else None
                    if nx_ and (k_, nx_) not in seen_cfg:
                        seen_cfg.add((k_, nx_))
                        tv[k_] = nx_
                        break
            else:
                m_np = None
            if m_np:
                continue
        if "is not permitted here" in msg:  # import-time type check: try another variable type for the pin
            fx_ = fix_direction(msg, pins)
            cfg_ = (tuple(p_[2] for p_ in fx_), tuple(sorted(tv.items()))) if fx_ else None
            if fx_ and cfg_ not in seen_cfg:
                seen_cfg.add(cfg_)
                pins = fx_
                continue
        if "both data types are the same" in msg:  # conversion: make the destination type differ from the source type
            for k_ in ("dest_type", "DestType", "dst_type"):
                if k_ in tv or True:
                    cur_ = tv.get(k_) or tv.get("SrcType") or "Int"
                    cands_ = tv_candidates(k_)
                    nx_ = cands_[(cands_.index(cur_) + 1) % len(cands_)] if cur_ in cands_ else cands_[0]
                    if (k_, nx_) not in seen_cfg:
                        seen_cfg.add((k_, nx_))
                        tv[k_] = nx_
                        break
            else:
                nx_ = None
            if nx_:
                continue
        if "exactly 1 extendable" in msg:
            for k_ in list(tv):
                if k_.lower() == "card" and tv[k_] != "1":
                    tv[k_] = "1"
                    break
            else:
                tv["card"] = "1"
            continue
        m_cf = re.search(r"Cannot find an instruction with name '\w+' and version '([\d.]+)'", msg)
        if m_cf:
            nxt = {"1.0": "2.0", "2.0": "1.1", "1.1": "3.0", "3.0": "1.2"}.get(m_cf.group(1))
            if nxt:
                ver = nxt
                continue
        if "is not supported by the CPU or library version" in msg:  # the part needs an explicit instruction version
            nxt = {None: "1.0", "1.0": "2.0", "2.0": "1.1", "1.1": "3.0"}.get(ver)
            if nxt:
                ver = nxt
                continue
        if ("DisableENO' is not supported" in msg or "ENO cannot be deactivated" in msg) and eno:
            eno = False
            continue
        m = re.search(r"The connection with the name '(\w+)' is not connected", msg)
        if m:
            pn = m.group(1)
            if any(p[0].lower() == pn.lower() for p in pins):
                return False, msg[-300:]
            if pn.lower() == "out" and (SPEC[0] or {}).get("post"):
                continue
            gp = guess_pin(pn)
            var_ = (forced and forced_var(nm, pn)) or gp[1]
            sty_ = {k_.lower(): v_ for k_, v_ in ((SPEC[0] or {}).get("types", {})).items()}.get(pn.lower())
            if sty_:
                var_ = "s_" + re.sub(r"\W", "", pn.lower())
                EXTRA[var_] = sty_
            pins.append((pn, gp[0], var_))
            continue
        m = re.search(r"connection named '(\w+)',\s+at part", msg)
        if m and forced and m.group(1).lower() in ("in", "en") and en_pin == m.group(1).lower():
            en_pin = "en" if m.group(1).lower() == "in" else None
            continue
        if m:
            pins = [p for p in pins if p[0].lower() != m.group(1).lower()]
            if not pins and en_pin is None:
                return False, msg[-300:]
            continue
        return False, msg[-300:]
    return False, msg[-300:]


KEY_PREFIX = {"extended": "ex", "modbus": "mb", "modbustcp": "mt", "openuc": "ou", "ptp": "pt", "s7comm": "sc", "uss": "us", "webserver": "ws", "teleservice": "ts"}
XML_NAMES = json.load(open(os.path.join(HERE, "xml_names.json"))) if os.path.isfile(os.path.join(HERE, "xml_names.json")) else {}


def main(argv):
    lang = argv[0].upper()
    template = open(argv[1], encoding="utf-8-sig").read()
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d  # noqa: E731
    cat_name = opt("--cat", lang.lower())
    only = set((opt("--only", "") or "").split(",")) - {""}
    cat = json.load(open(os.path.join(HERE, "..", "docs", "catalog", cat_name + ".json"), encoding="utf-8"))
    rp = os.path.join(HERE, "..", "docs", "catalog", ("%s_results.json" % cat_name) if cat_name == lang.lower() else ("%s_%s_results.json" % (cat_name, lang.lower())))
    results = json.load(open(rp))
    outdir = os.path.join(HERE, "sources", "xml_" + cat_name + "_" + lang.lower())
    os.makedirs(outdir, exist_ok=True)
    c = TiaClient(int(opt("--pid", default_pid())))
    for idx, item in enumerate(cat):
        tag = "%03d" % (idx + 1)
        rkey = ("" if cat_name == lang.lower() else KEY_PREFIX.get(cat_name, cat_name[:2])) + tag
        if (only and tag not in only) or (not only and results.get(rkey, {}).get("ok")):
            continue
        forced = XML_NAMES.get(cat_name, {}).get(tag)
        if not item.get("params") and not forced:
            continue
        name = "FX_%s_%s%s" % (lang.title(), "" if cat_name == lang.lower() else cat_name[:2], tag)
        done, last = False, ""
        EXTRA.clear()
        INSTANCE[0] = None
        if only and results.get(rkey, {}).get("via") == "xml":  # a repeated run decides again
            results[rkey]["ok"] = False
            results[rkey].pop("via", None)
        pins0 = kind_pins(item) if item.get("params") else []
        SPEC[0] = None
        if forced and isinstance(forced[0], dict):
            SPEC[0], forced = forced[0], [forced[0]["name"]]
        for nm in (forced or candidates(item)[:7]):
            if forced and lang == "LAD" and nm in ("TP", "TON", "TOF", "TONR"):
                ok, info = timer_coil(c, template, name, nm, outdir)
            else:
                if forced and item.get("params"):  # typed mode first: one variable per pin with the catalog type
                    ok, info = adapt(c, lang, template, name, nm, pins0, outdir, None, True, {p_["name"].lower(): p_["types"] for p_ in item["params"]})
                    if not ok:
                        EXTRA.clear()
                        ok, info = adapt(c, lang, template, name, nm, pins0, outdir, None, True)
                else:
                    ok, info = adapt(c, lang, template, name, nm, pins0, outdir, None, bool(forced))
            if ok:
                done = True
                results[rkey] = {"title": results.get(rkey, {}).get("title", item["title"]), "ok": True, "via": "xml", "block": name, "xml": info}
                if opt("--sample"):
                    results[rkey]["sample"] = opt("--sample")
                break
            last = "%s: %s" % (nm, info)
        print("%s %-40s %s" % (tag, item["title"][:40], "ok via xml" if done else "FAIL " + last[:170]), flush=True)
        if not done:
            results.setdefault(rkey, {"title": item["title"].split(" (S7")[0], "ok": False})["xmlLast"] = last[:400]
        json.dump(results, open(rp, "w"), indent=1, sort_keys=True)


if __name__ == "__main__":
    main(sys.argv[1:])
