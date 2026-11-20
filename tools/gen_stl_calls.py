"""STL (AWL) sources for the remaining STL catalog pages (C3 rest): calls of system instructions (TON, MIN, MOVE_BLK ...).
For each page of docs/catalog/stl.json with parameters, the LAD/FBD block of the same instruction from the LAD/FBD coverage
(docs/spec/instruction-coverage.md, SimaticML truth of samples 031/032/037/038/039) gives the interface, the template
values and the operand of each pin; the network becomes one STL CALL:

    CALL #inst  |  CALL NAME
    {type_template := 'Type'}            (only templates of kind Type)
    (  PIN := operand , ...
    );

Writes tools/sources/stl_calls/FB_StlC_<nnn>.awl (one FB per instruction) and stl_calls.txt (page -> source block).
Usage: python -I tools/gen_stl_calls.py"""
import json
import os
import re
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "tools", "sources", "stl_calls")
IF = "{http://www.siemens.com/automation/Openness/SW/Interface/v5}"
RESERVED = ["db", "ra"]
# STL needs the exact data type where LAD converted implicitly (TIA messages of the first import, sample 075)
FIX = {62: {"HI_LIM": "Real", "LO_LIM": "Real", "OUT": "Real"}, 63: {"IN": "Real", "HI_LIM": "Real", "LO_LIM": "Real"},
       75: {"LST_STEP": "Byte"}, 78: {"CMP_STEP": "Byte"}, 79: {"OUT_STEP": "Byte"}, 80: {"IN": "Real", "OUT": "Real"}, 83: {"IN": "DWord"}}
SKIP = {39: "DB_ANY parameter, interface types DBHeader/VAREF unknown in a source", 40: "as 039",
        55: "needs the global DB DB_Arr", 56: "needs the global DB DB_Arr",
        36: "OUT must be an array element of Word (TIA: Only array elements are permitted)", 75: "catalog parameter list of DRUM not usable as pin names"}


def loc(t):
    return t.rsplit("}", 1)[-1]


def coverage_rows():
    """[(title, block, sample)] of the ok rows of the LAD/FBD tables."""
    rows, sample = [], None
    for line in open(os.path.join(ROOT, "docs", "spec", "instruction-coverage.md"), encoding="utf-8"):
        m = re.match(r"## \w+ \((LAD|FBD|SCL), sample (\S+)\)", line)
        if m:
            sample = m.group(2) if m.group(1) != "SCL" else None
            continue
        if line.startswith("## "):
            sample = None
        if sample is None or not line.startswith("| ") or "| ok" not in line:
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 5:
            continue
        blk = cells[4]
        m = re.match(r"(\S+)(?: \((\S+)\))?", blk)
        if m:
            rows.append((cells[2], m.group(1), m.group(2) or sample))
    return rows


def find_xml(sample, block):
    for dp, _, fs in os.walk(os.path.join(ROOT, "samples", sample + "_truth")):
        if block + ".xml" in fs:
            return os.path.join(dp, block + ".xml")
    return None


def decl(m, ind):
    """STL source declaration of an interface member (nested structs written out)."""
    t = m.get("Datatype")
    subs = [x for x in m if loc(x.tag) == "Member"]
    secs = [x for x in m if loc(x.tag) == "Sections"]
    if t == "Struct" or (subs and not t.startswith('"')):
        inner = subs or [y for s in secs for sec in s for y in sec if loc(y.tag) == "Member"]
        o = [ind + '%s : Struct' % m.get("Name")]
        for x in inner:
            o += decl(x, ind + "   ")
        return o + [ind + "END_STRUCT;"]
    ver = m.get("Version")
    return [ind + "%s : %s%s;" % (m.get("Name"), t, (" " if False else "") )]


def operand(acc):
    """STL operand text of a SimaticML Access."""
    sc = acc.get("Scope")
    if sc in ("LiteralConstant", "TypedConstant"):
        typ = acc.findtext(".//{*}ConstantType")
        val = acc.findtext(".//{*}ConstantValue")
        return ("%s#%s" % (typ.upper(), val) if typ and "#" not in val and typ not in ("Bool",) else val)
    if sc in ("LocalConstant", "GlobalConstant"):
        c = acc.find("{*}Constant")
        return ("#" if sc == "LocalConstant" else "") + '"%s"' % c.get("Name") if sc == "GlobalConstant" else "#" + c.get("Name")
    sym = acc.find("{*}Symbol")
    parts = []
    for c in sym:
        if loc(c.tag) != "Component":
            continue
        t = c.get("Name")
        idx = [operand(a) for a in c if loc(a.tag) == "Access"]
        if idx:
            t += "[%s]" % ",".join(i.lstrip("#") if False else i for i in idx)
        parts.append(t)
    if sc == "LocalVariable":
        return "#" + ".".join(parts)
    return '"%s"' % parts[0] + ("." + ".".join(parts[1:]) if len(parts) > 1 else "")


def network(xmlfile, name):
    """(interface xml, instance, template values, [(pin, operand)]) of the first network with the instruction."""
    root = ET.parse(xmlfile).getroot()
    itf = root.find(".//{*}Interface")
    for flg in root.iter():
        if loc(flg.tag) != "FlgNet":
            continue
        acc = {a.get("UId"): a for a in flg.iter() if loc(a.tag) == "Access" and a.get("UId")}
        parts = [p for p in flg.iter() if loc(p.tag) == "Part" and p.get("Version")]
        if not parts:
            continue
        part = parts[0]
        inst = part.find("{*}Instance")
        tvs = [(tv.get("Name"), tv.text) for tv in part if loc(tv.tag) == "TemplateValue" and tv.get("Type") == "Type"]
        pins = []
        for w in flg.iter():
            if loc(w.tag) != "Wire":
                continue
            ends = list(w)
            nc = [e for e in ends if loc(e.tag) == "NameCon" and e.get("UId") == part.get("UId")]
            ic = [e for e in ends if loc(e.tag) == "IdentCon"]
            if nc and ic and nc[0].get("Name") not in ("en", "eno"):
                pins.append((nc[0].get("Name"), operand(acc[ic[0].get("UId")])))
        return itf, inst, tvs, pins, part.get("Name")
    return None


def interface_text(itf):
    o = []
    kw = {"Input": "VAR_INPUT", "Output": "VAR_OUTPUT", "InOut": "VAR_IN_OUT", "Static": "VAR", "Temp": "VAR_TEMP", "Constant": "VAR CONSTANT"}
    for sec in itf.find(IF + "Sections").findall(IF + "Section"):
        name = sec.get("Name")
        if name not in kw:
            continue
        ms = [m for m in sec if loc(m.tag) == "Member"]
        if not ms:
            continue
        o.append("   " + kw[name] + " ")
        for m in ms:
            if name == "Constant":
                o.append("      %s : %s := %s;" % (m.get("Name"), m.get("Datatype"), m.findtext("{*}StartValue")))
            else:
                o += decl(m, "      ")
        o.append("   END_VAR")
    return o


def main():
    cat = json.load(open(os.path.join(ROOT, "docs", "catalog", "stl.json"), encoding="utf-8"))
    rows = coverage_rows()
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        os.remove(os.path.join(OUT, f))
    report = []
    for no, page in enumerate(cat, 1):
        if not page.get("params"):
            continue
        name = page["name"]
        hit = None
        for title, blk, sample in rows:
            if title.split(":")[0].strip() == name:
                xml = find_xml(sample, blk)
                r = network(xml, name) if xml else None
                if r:
                    hit = (blk, sample, r)
                    break
        if not hit:
            report.append("%03d %s: no LAD/FBD template" % (no, name))
            continue
        blk, sample, (itf, inst, tvs, pins, part) = hit
        if no in SKIP:
            report.append("%03d %s: skipped (%s)" % (no, name, SKIP[no]))
            continue
        # members per section and type, for pins that LAD drives by power flow (IN, Q) or converts implicitly
        members = {}
        for sec in itf.find(IF + "Sections").findall(IF + "Section"):
            for m in sec:
                if loc(m.tag) == "Member":
                    members.setdefault((sec.get("Name"), m.get("Datatype")), "#" + m.get("Name"))

        def pick(direction, typ):
            secs = ["Input", "InOut", "Static"] if direction == "Input" else ["Output", "InOut", "Static"]
            for s in secs:
                if (s, typ) in members:
                    return members[(s, typ)]
            return None
        wired = dict(pins)
        for p in page["params"]:
            pn = p["name"]
            if pn in wired or pn.startswith("<") or p["decl"] not in ("Input", "Output", "InOut"):
                continue
            fix = FIX.get(no, {}).get(pn)
            typ = fix or p["types"].split(",")[0].strip().replace("BOOL", "Bool").replace("TIME", "Time")
            m = re.match(r"IN(\d+)$", pn)
            if m and "IN1" in wired:
                wired[pn] = wired["IN1"]
            elif pick(p["decl"], typ):
                wired[pn] = pick(p["decl"], typ)
        for pn, typ in FIX.get(no, {}).items():
            direction = next((p["decl"] for p in page["params"] if p["name"] == pn), "Input")
            if pick(direction, typ):
                wired[pn] = pick(direction, typ)
        order = [p["name"] for p in page["params"]]
        pins = sorted(wired.items(), key=lambda kv: order.index(kv[0]) if kv[0] in order else len(order))
        fb = "FB_StlC_%03d" % no
        lines = ['FUNCTION_BLOCK "%s"' % fb, "TITLE = STL call %s" % name, "{ S7_Optimized_Access := 'TRUE' }", "VERSION : 0.1"]
        lines += interface_text(itf)
        lines += ["", "BEGIN", "NETWORK", "TITLE = %03d %s" % (no, name)]
        if inst is not None and inst.get("Scope") == "LocalVariable":
            lines.append("      CALL #" + ".".join(c.get("Name") for c in inst if loc(c.tag) == "Component"))
        else:
            lines.append("      CALL " + name)
        if tvs:
            lines.append("      {" + ", ".join("%s := '%s'" % t for t in tvs) + "}")
        if pins:
            lines.append("      (  " + " , \n         ".join("%-27s := %s" % p for p in pins))
            lines.append("      );")
        lines.append("END_FUNCTION_BLOCK")
        text = "\n".join(lines) + "\n"
        for word in RESERVED:  # interface names that are STL keywords in a source (db, ra)
            text = re.sub(r"(?<![\w\"])(#?)%s\b(?= *:|[ ,\n.\[)])" % word, lambda m, w=word: m.group(1) + w + "_v", text)
        open(os.path.join(OUT, fb + ".awl"), "w", encoding="utf-8").write(text)
        report.append("%03d %s: %s from %s/%s" % (no, name, fb, sample, blk))
    open(os.path.join(OUT, "stl_calls.txt"), "w", encoding="utf-8").write("\n".join(report) + "\n")
    print("\n".join(report))


if __name__ == "__main__":
    main()
