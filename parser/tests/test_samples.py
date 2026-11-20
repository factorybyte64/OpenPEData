"""Parser test against Openness ground truth for every sample (project folder and, if present, .zap21).

Ground truth: parser/tests/truth/<sample>/ (tools/Make-TruthSet.ps1, taken from copies of the samples), else
samples/<sample>_truth/ when it was written by Save-Truth during sample creation (tools/Make-Samples.ps1), and
samples/002_cpu_truth/device_PLC_1_details.json (CPU order number / firmware of PLC_1, unchanged in all
samples derived from 002_cpu).
Compared: metamodel packages (id and name of every Package element of the embedded metamodel XML); project name; device groups; devices; per PLC: CPU order number + firmware, tag tables (name, group,
tag count), every tag (name, data type, address, comment per culture, HMI accessible/visible/writable
against ExternalAccessible/ExternalVisible/ExternalWritable), blocks (name, type, number, language), PLC data
types (name, group), members of data blocks and PLC data types (path, data type, start value, array element start
values, comment) against the SimaticML export (<device>/blocks/<name>.xml) and the SIMATIC SD text
(<name>.sd.json, the only truth for objects TIA does not export because they are not compiled), instanceOf of
instance DBs.
Interfaces and network code of FB/FC and instance DBs (samples 023-029, truth in samples/<sample>_truth/<block>.xml and <block>.get.json): per section
(Input, Output, InOut, Static, Temp, Constant, Return; instance DBs: Input, Output, InOut, Static) the member paths, data types, start values and comments
against the SimaticML export and against the Openness interface read (TiaPlcSoftware get detail=interface).
Networks of code blocks: count, language, title, comment, SCL source text (get detail=full) and for LAD/FBD the parts and wires
(canonical net list: part names with index, operand names, powerrail/open connections, template values) against SimaticML.

Usage: python parser/tests/test_samples.py     exit code 0 = all checks passed; result tables also in tests/RESULTS.md
"""
import contextlib
import io
import json
import os
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.dirname(HERE))
from openpedata.metamodel import NS as MM_NS  # noqa: E402
from openpedata.model import Model  # noqa: E402


def jload(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def simaticml_tags(path):
    root = ET.parse(path).getroot()
    tags = {}
    for t in root.iter("SW.Tags.PlcTag"):
        al = t.find("AttributeList")
        name = al.findtext("Name")
        comment = {}
        for item in t.iter("MultilingualTextItem"):
            ia = item.find("AttributeList")
            if ia.findtext("Text"):
                comment[ia.findtext("Culture")] = ia.findtext("Text")
        flag = lambda n: None if al.findtext(n) is None else al.findtext(n) == "true"
        tags[name] = {"dataType": al.findtext("DataTypeName"), "address": al.findtext("LogicalAddress"), "comment": comment,
                      "hmiWritable": flag("ExternalWritable"), "hmiVisible": flag("ExternalVisible"),
                      "hmiAccessible": flag("ExternalAccessible")}
    return tags


def xml_members(parent):
    """Members of a SimaticML interface element: name, data type, start value, comment, array element start values,
    sub-members (Struct members directly, UDT members below Sections/Section)."""
    out = []
    for m in parent:
        if m.tag.rsplit("}", 1)[-1] != "Member":
            continue
        ch = lambda n: [x for x in m if x.tag.rsplit("}", 1)[-1] == n]  # noqa: E731
        sv = ch("StartValue")
        com = {t.get("Lang"): t.text for c in ch("Comment") for t in c if t.text}
        e = {"name": m.get("Name"), "dataType": m.get("Datatype"), "startValue": sv[0].text if sv else None,
             "comment": com}
        el = {s.get("Path"): s[0].text for s in ch("Subelement") if len(s) and s[0].tag.rsplit("}", 1)[-1] == "StartValue"}
        if el:
            e["elementStartValues"] = el
        kids = xml_members(m)
        for secs in ch("Sections"):
            for sec in secs:
                kids += xml_members(sec)
        if kids:
            e["members"] = kids
        out.append(e)
    return out


def simaticml_members(path):
    """Static section of a data block export, or the None section of a PLC data type export."""
    root = ET.parse(path).getroot()
    for sec in root.iter():
        if sec.tag.rsplit("}", 1)[-1] == "Section" and sec.get("Name") in ("Static", "None"):
            return xml_members(sec)
    return []


def sd_truth(path):
    """(members, instanceOf, complete) from a SIMATIC SD source (documentGet JSON): declarations 'name : Type [:= v];',
    'name : STRUCT' ... 'END_STRUCT;', comments via { S7_MLC := "id" } + resources, data block start values as
    assignments 'a.b := v;' / 'a[1] := v;' after END_VAR. complete is False when an assignment addresses an element
    the source does not declare (element of a UDT-typed member)."""
    import re
    j = jload(path)
    texts, cur = {}, None
    for line in (j.get("resources") or "").splitlines():
        m = re.match(r"\s*- id: (\S+)", line)
        if m:
            cur = texts.setdefault(m.group(1), {})
        elif cur is not None and re.match(r"\s+[a-z]{2}-[A-Z]{2}: ", line):
            k, v = line.strip().split(": ", 1)
            cur[k] = v
    unq = lambda s: s.strip().strip('"')  # noqa: E731
    top, stack, mlc, inst, body, complete = [], None, None, None, False, True
    for line in j["source"].splitlines():
        s = line.strip()
        m = re.match(r'DATA_BLOCK\s+"?([^":\s]+)"?\s*(?::\s*"?([^"\s]+)"?)?', s)
        if m:
            inst, stack = m.group(2), [top]
            continue
        if s == "TYPE":
            stack = [[]]
            continue
        if stack is None:
            continue
        if s.startswith("{") and "S7_MLC" in s:
            mlc = re.search(r'S7_MLC := "([^"]+)"', s).group(1)
            continue
        if s == "END_VAR":
            body = True
            continue
        if body:
            m = re.match(r"(.+?)\s*:=\s*(.+?);$", s)
            if m:
                path = [unq(x) for x in m.group(1).split(".")]
                lst, mem = top, None
                for part in path:
                    nm, idx = re.match(r"([^\[]+)(?:\[(.+)\])?$", part).groups()
                    mem = next((x for x in lst if x["name"] == nm), None)
                    if mem is None:
                        break
                    lst = mem.get("members", [])
                if mem is None:
                    complete = False
                elif idx:
                    mem.setdefault("elementStartValues", {})[idx.replace(" ", "")] = m.group(2)
                else:
                    mem["startValue"] = m.group(2)
            continue
        m = re.match(r'("?[^":\s]+"?)\s*:\s*STRUCT$', s)
        if m:
            e = {"name": unq(m.group(1)), "dataType": "Struct", "startValue": None, "comment": texts.get(mlc, {}),
                 "members": []}
            mlc = None
            stack[-1].append(e)
            stack.append(e["members"])
            continue
        if s == "END_STRUCT;":
            stack.pop()
            continue
        m = re.match(r'("?[^":\s]+"?)\s*:\s*(.+?)(?:\s*:=\s*(.+?))?;$', s)
        if m:
            typ = m.group(2)
            if typ.startswith("_."):
                typ = '"%s"' % typ[2:]
            stack[-1].append({"name": unq(m.group(1)), "dataType": typ, "startValue": m.group(3),
                              "comment": texts.get(mlc, {})})
            mlc = None
    if not top and stack and stack[0] and stack[0][0].get("dataType") == "Struct":
        top = stack[0][0]["members"]  # TYPE <name> : STRUCT ... END_STRUCT;
    return top, inst, complete


def flat(members, prefix=""):
    out = {}
    for m in members:
        p = prefix + m["name"]
        out[p] = {k: m.get(k) for k in ("dataType", "startValue", "comment", "elementStartValues")}
        out.update(flat(m.get("members", []), p + "."))
    return out


def check_members(c, what, got, exp):
    g, e = flat(got), flat(exp)
    c.eq("%s member paths" % what, list(g), list(e))
    for p in e:
        if p in g:
            for f in e[p]:
                c.eq("%s.%s %s" % (what, p, f), g[p][f], e[p][f])


class Checker:
    def __init__(self):
        self.n = 0
        self.fail = []

    def eq(self, what, got, exp):
        self.n += 1
        if got != exp:
            self.fail.append("%s: parsed %r, truth %r" % (what, got, exp))


# Samples whose Save-Truth folder has the layout of the generic device check below, but whose content that check was
# not written for (array DBs, system data types and system blocks in the block list, nested multi-instances, several
# PLCs with a CP). Their DB symbols are checked by the .NET test program (dotnet\tests\OpenPEData.Tests:
# ProjectTests.DbSymbolsAgainstSimaticML, ProjectTests.DbMemberAttributesAgainstOpenness, MultiPlcTests).
DOTNET_ONLY = {"059_db_symbols", "060_db_attrs", "063_multi_plc", "066_sys_fb_instances"}


def truth_dir(sample):
    if sample in DOTNET_ONLY:
        return None
    t = os.path.join(HERE, "truth", sample)
    if os.path.isdir(t):
        return t
    t = os.path.join(ROOT, "samples", sample + "_truth")
    return t if os.path.isfile(os.path.join(t, "project.json")) else None


def check(sample, source):
    truth = truth_dir(sample)
    model = Model(source)
    d = model.to_dict()["project"]
    c = Checker()
    counts = {"devices": 0, "tagTables": 0, "tags": 0, "blocks": 0, "types": 0, "members": 0}
    mm = model.p.mm
    pkgs = [(int(p.get("id"), 16), p.get("name")) for p in ET.fromstring(mm.xml).iter(MM_NS + "Package")]
    c.eq("metamodel package count", len(mm.packages), len(pkgs))
    c.eq("metamodel packages", sorted(mm.packages.items()), sorted(pkgs))
    proj = jload(os.path.join(truth, "project.json"))["items"][0]
    c.eq("project name", d["name"], proj["name"])
    groups = sorted(g["path"] for g in jload(os.path.join(truth, "groups.json"))["items"])
    c.eq("device groups", sorted(g["path"] for g in d["deviceGroups"]), groups)
    devs = jload(os.path.join(truth, "devices.json"))["items"]
    c.eq("devices", sorted(x["name"] for x in d["devices"]), sorted(x["name"] for x in devs))
    cpu_truth = jload(os.path.join(ROOT, "samples", "002_cpu_truth", "device_PLC_1_details.json"))
    for dev in d["devices"]:
        counts["devices"] += 1
        for plc in dev["plcs"]:
            if dev["name"] == cpu_truth["name"]:
                ti = next(i["typeIdentifier"] for i in cpu_truth["deviceItems"] if i["name"] == cpu_truth["headItemName"])
                c.eq("%s CPU" % plc["name"], "OrderNumber:%s/%s" % (plc["orderNumber"], plc["firmware"]), ti)
            pdir = os.path.join(truth, dev["name"])
            tts = jload(os.path.join(pdir, "tagTables.json"))["items"]
            c.eq("%s tag tables" % dev["name"], sorted((t["name"], t["group"]) for t in plc["tagTables"]),
                 sorted((t["name"], t.get("groupPath") or "") for t in tts))
            for t in plc["tagTables"]:
                counts["tagTables"] += 1
                xt = next((x for x in tts if x["name"] == t["name"]), None)
                if xt:
                    c.eq("%s tag count" % t["name"], len(t["tags"]), xt["tagCount"])
                exp = simaticml_tags(os.path.join(pdir, "tagtables", t["name"] + ".xml"))
                c.eq("%s tag names" % t["name"], sorted(x["name"] for x in t["tags"]), sorted(exp))
                for tag in t["tags"]:
                    counts["tags"] += 1
                    e = exp.get(tag["name"])
                    if e:
                        for f in ("dataType", "address", "comment", "hmiAccessible", "hmiVisible", "hmiWritable"):
                            c.eq("%s.%s %s" % (t["name"], tag["name"], f), tag[f], e[f])
            bl = jload(os.path.join(pdir, "blocks.json"))["items"]
            counts["types"] += len(plc["types"])
            c.eq("%s blocks" % dev["name"], sorted((b["name"], b["type"], b["number"], b["language"]) for b in plc["blocks"]),
                 sorted((b["name"], b["blockType"], b["number"], b["programmingLanguage"]) for b in bl))
            counts["blocks"] += len(plc["blocks"])
            if os.path.isfile(os.path.join(pdir, "types.json")):
                ty = jload(os.path.join(pdir, "types.json"))["items"]
                c.eq("%s types" % dev["name"], sorted((t["name"], t["group"]) for t in plc["types"]),
                     sorted((t["name"], t.get("groupPath") or "") for t in ty))
            bdir = os.path.join(pdir, "blocks")
            for b in [x for x in plc["blocks"] if "members" in x] + plc["types"]:
                counts["members"] += len(flat(b["members"]))
                xml, sd = os.path.join(bdir, b["name"] + ".xml"), os.path.join(bdir, b["name"] + ".sd.json")
                if os.path.isfile(xml):
                    check_members(c, b["name"] + " (SimaticML)", b["members"], simaticml_members(xml))
                if os.path.isfile(sd):
                    exp, inst, complete = sd_truth(sd)
                    if complete:
                        check_members(c, b["name"] + " (SD)", b["members"], exp)
                    elif not os.path.isfile(xml):
                        c.fail.append("%s: SD ground truth incomplete, no SimaticML" % b["name"])
                    if "instanceOf" in b or inst:
                        c.eq("%s instanceOf" % b["name"], b.get("instanceOf"), inst)
                if not os.path.isfile(xml) and not os.path.isfile(sd):
                    c.fail.append("%s: no member ground truth" % b["name"])
    return c, counts


def simaticml_sections(path):
    """{section name: members} of the <Interface> of a code block export (top-level sections only)."""
    root = ET.parse(path).getroot()
    out = {}
    for itf in root.iter("Interface"):
        for secs in itf:
            for sec in secs:
                if sec.tag.rsplit("}", 1)[-1] == "Section":
                    out[sec.get("Name")] = xml_members(sec)
    return out


# instruction names that differ between the internal ident table and the Openness (SimaticML) export
INSTRUCTION_EXPORT_NAME = {"Scale": "Scale_X"}


def netlist(parts, wires):
    """Canonical network: wires as sorted tuples of endpoints. parts: [(uid, name | None, desc)] in document order,
    operands are resolved by the caller; wires: [[endpoint]] with endpoint ("powerrail",) | ("open",) |
    ("operand", name) | ("part", uid, pin)."""
    idx, seen = {}, {}
    for uid, name, desc in parts:
        if name is not None:
            idx[uid] = "%s/%d%s" % (name, seen.setdefault(name, 0), desc)
            seen[name] += 1
    out = []
    for w in wires:
        ends = [e[0] if len(e) == 1 else "operand:%s" % e[1] if e[0] == "operand" else "%s.%s" % (idx[e[1]], e[2]) for e in w]
        out.append(tuple(sorted(ends)))
    return sorted(out), sorted(idx.values())


def simaticml_networks(path):
    """[(language, title, comment, netlist | None)] of the compile units of a code block export."""
    root = ET.parse(path).getroot()
    res = []
    loc = lambda e: e.tag.rsplit("}", 1)[-1]  # noqa: E731
    for cu in root.iter("SW.Blocks.CompileUnit"):
        lang = cu.findtext("AttributeList/ProgrammingLanguage")
        texts = {}
        for ml in cu.findall("ObjectList/MultilingualText"):
            texts[ml.get("CompositionName")] = {i.findtext("AttributeList/Culture"): i.findtext("AttributeList/Text")
                                                for i in ml.iter("MultilingualTextItem") if i.findtext("AttributeList/Text")}
        flg = next((e for e in cu.iter() if loc(e) == "FlgNet"), None)
        nl = None
        if flg is not None:
            parts, names, wires = [], {}, []
            def sym(e):
                """Operand text of a Symbol: components joined by '.', array indices in brackets, slice access as .%X3."""
                out = []
                for comp_ in e:
                    if loc(comp_) != "Component":
                        continue
                    txt = comp_.get("Name")
                    idx = []
                    for acc in comp_:
                        if loc(acc) == "Access":
                            if acc.get("Scope") in ("LiteralConstant", "TypedConstant"):
                                idx.append(acc.findtext(".//{*}ConstantValue") or "")
                            else:
                                idx.append(sym(next(x for x in acc if loc(x) == "Symbol")))
                    if idx:
                        txt += "[%s]" % ",".join(idx)
                    if comp_.get("SliceAccessModifier"):
                        txt += ".%" + comp_.get("SliceAccessModifier")[0].upper() + comp_.get("SliceAccessModifier")[1:]
                    out.append(txt)
                return ".".join(out)
            comp = lambda e: sym(next(x for x in e if loc(x) == "Symbol"))  # noqa: E731
            for sec in flg:
                if loc(sec) == "Parts":
                    for e in sec:
                        uid = e.get("UId")
                        if loc(e) == "Access":
                            if e.get("Scope") in ("LiteralConstant", "TypedConstant"):
                                names[uid] = "const:" + (e.findtext(".//{*}ConstantValue") or "")
                            elif e.get("Scope") in ("LocalConstant", "GlobalConstant"):
                                names[uid] = "const:" + next((c.get("Name") for c in e.iter() if loc(c) == "Constant"), "")
                            elif e.get("Scope") == "Label":
                                names[uid] = "label:" + next(x for x in e.iter() if loc(x) == "Label").get("Name")
                            elif e.get("Scope") == "Address":
                                ad = next(x for x in e.iter() if loc(x) == "Address")
                                area = ad.get("Area")
                                size = {"Bool": 1, "Byte": 8, "Char": 8, "SInt": 8, "USInt": 8, "Word": 16, "Int": 16, "UInt": 16, "DWord": 32, "DInt": 32, "UDInt": 32,
                                        "Real": 32, "LWord": 64}.get(ad.get("Type"), 1)
                                names[uid] = __import__("openpedata.code", fromlist=["x"]).address_text(area.replace("Periphery", ""), int(ad.get("BitOffset")), size, area.startswith("Periphery"))
                            else:
                                names[uid] = comp(e)
                        elif loc(e) in ("Part", "Call"):
                            tv = ",".join(sorted("%s=%s" % (t.get("Name"), (t.text or "").upper() if t.get("Type") == "Operation" else t.text) for t in e if loc(t) == "TemplateValue"))
                            neg = ",".join(sorted(t.get("Name") for t in e if loc(t) == "Negated"))
                            inst = next((".".join(c.get("Name") for c in t if loc(c) == "Component") for t in e.iter() if loc(t) == "Instance"), "")
                            name = e.get("Name") if loc(e) == "Part" else "Call:" + next(t for t in e.iter() if loc(t) == "CallInfo").get("Name")
                            parts.append((uid, name, "{%s}%s%s%s" % (tv, "!eno" if e.get("DisabledENO") == "true" else "",
                                                                    "!neg(%s)" % neg if neg else "", "@" + inst if inst else "")))
                elif loc(sec) == "Wires":
                    for w in sec:
                        ends = []
                        for c in w:
                            t = loc(c)
                            ends.append(("powerrail",) if t == "Powerrail" else ("open",) if t == "OpenCon" else
                                        ("operand", names[c.get("UId")]) if t == "IdentCon" else ("part", c.get("UId"), c.get("Name")))
                        wires.append(ends)
            nl = netlist(parts, wires)
        res.append((lang, texts.get("Title", {}), texts.get("Comment", {}), nl))
    return res


def parsed_netlist(n):
    parts = []
    for x in n.get("parts", []):  # an empty network (new OB) has no parts
        if x["kind"] not in ("part", "instruction", "call"):
            parts.append((x["uid"], None, ""))
            continue
        tv = ",".join(sorted("%s=%s" % ((k, v.upper() if k.upper().startswith("CASE") or k.lower() == "relation" else v)) for k, v in x.get("templateValues", {}).items()))  # operations: the plf keeps the imported spelling (Eq), the export prints EQ
        inst = x.get("instance") or {}
        parts.append((x["uid"], ("Call:" + x["name"]) if x["kind"] == "call" else INSTRUCTION_EXPORT_NAME.get(x["name"], x["name"]),
                      "{%s}%s%s%s" % (tv, "!eno" if x.get("disableEno") else "",
                                      "!neg(%s)" % ",".join(sorted(x["negated"])) if x.get("negated") else "",
                                      "@" + inst["name"] if inst.get("name") else "")))
    byuid = {x["uid"]: x for x in n.get("parts", [])}
    wires = []
    for w in n.get("wires", []):
        ends = []
        for e in w["ends"]:
            if e.get("powerrail"):
                ends.append(("powerrail",))
            elif e.get("open"):
                ends.append(("open",))
            elif "operand" in e:
                ends.append(("operand", e["operand"]))
            else:
                ends.append(("part", e["part"], e["pin"]))
        wires.append(ends)
    return netlist(parts, wires)


def json_members(ms):
    out = []
    for m in ms:
        e = {"name": m["name"], "dataType": m["datatype"], "startValue": m.get("startValue"),
             "comment": m.get("comment") or {}}
        if m.get("members"):
            e["members"] = json_members(m["members"])
        out.append(e)
    return out


def check_interfaces(sample, source):
    """FB/FC interface sections against SimaticML export and Openness get JSON; None when the sample has none."""
    tdir = os.path.join(ROOT, "samples", sample + "_truth")
    gets = sorted(x for x in os.listdir(tdir) if x.endswith(".get.json")) if os.path.isdir(tdir) else []
    gets = [x for x in gets if jload(os.path.join(tdir, x)).get("blockType") in ("FB", "FC", "InstanceDB")]
    if not gets:
        return None
    c, n = Checker(), 0
    d = Model(source).to_dict()["project"]
    blocks = {b["name"]: b for dev in d["devices"] for plc in dev["plcs"] for b in plc["blocks"]}
    for g in gets:
        name = g[:-len(".get.json")]
        j = jload(os.path.join(tdir, g))
        b = blocks.get(name)
        c.eq("%s present" % name, b is not None, True)
        if b is None:
            continue
        got = {s["section"]: s["members"] for s in b.get("interface", [])}
        if j["blockType"] == "InstanceDB":
            c.eq("%s instanceOf" % name, b.get("instanceOf"), j["instanceOf"])
            c.eq("%s type" % name, b["type"], "InstanceDB")
            c.eq("%s members = Static section" % name, [m["name"] for m in b["members"]],
                 [m["name"] for s in j["interface"] if s["section"] == "Static" for m in s["members"]])
        for exp, what in ((simaticml_sections(os.path.join(tdir, name + ".xml")), "SimaticML"),
                          ({s["section"]: json_members(s["members"]) for s in j["interface"]}, "get")):
            exp = {k: v for k, v in exp.items() if v}  # SimaticML also lists empty sections
            c.eq("%s sections (%s)" % (name, what), list(got), list(exp))
            for sec in exp:
                if sec in got:
                    check_members(c, "%s.%s (%s)" % (name, sec, what), got[sec], exp[sec])
                    n += len(flat(exp[sec]))
        nets = b.get("networks", [])
        jn = j.get("networks", [])
        if j["blockType"] != "InstanceDB":
            c.eq("%s network count" % name, len(nets), j["networkCount"])
            for pn, tn in zip(nets, jn):
                w = "%s network %d" % (name, pn["index"])
                c.eq(w + " language", pn["language"], tn["language"])
                c.eq(w + " title", pn["title"], tn.get("title", {}))
                c.eq(w + " comment", pn["comment"], tn.get("comment", {}))
                if "scl" in tn:
                    c.eq(w + " SCL text", pn.get("scl"), tn["scl"].replace(chr(13), ""))
                    c.eq(w + " unknown tokens", pn.get("unknownTokens", []), [])
                n += 1
            sd = os.path.join(tdir, name + ".s7dcl")
            if os.path.isfile(sd) and len(nets) == 1 and nets[0].get("scl") is not None:  # SD export: NETWORK body
                lines = open(sd, encoding="utf-8-sig").read().splitlines()
                body = lines[lines.index("    NETWORK") + 1:lines.index("    END_NETWORK")]
                c.eq("%s SCL text (SD export)" % name, [x.rstrip() for x in nets[0]["scl"].splitlines()],
                     [x[8:].rstrip() for x in body])
                n += 1
            xml = os.path.join(tdir, name + ".xml")
            if os.path.isfile(xml):
                for pn, (lang, title, comment, nl) in zip(nets, simaticml_networks(xml)):
                    w = "%s network %d (SimaticML)" % (name, pn["index"])
                    c.eq(w + " title", pn["title"], title)
                    c.eq(w + " comment", pn["comment"], comment)
                    if nl is not None:
                        c.eq(w + " wires and parts", parsed_netlist(pn), nl)
                        n += len(nl[0])
    return c, n


def sd_networks(path):
    """Network bodies of an SD export (de-indented by 8)."""
    lines = open(path, encoding="utf-8-sig").read().splitlines()
    nets, cur = [], None
    for ln in lines:
        if ln == "    NETWORK":
            cur = []
        elif ln == "    END_NETWORK":
            nets.append(cur)
            cur = None
        elif cur is not None:
            cur.append(ln[8:].rstrip() if ln.startswith("        ") else ln.rstrip())
    return nets


def check_flg_xml(sample, source, inv):
    """LAD/FBD networks against the SimaticML export (<sample>_truth/simaticml/<block>.xml): language, title, comment, net list
    (part names with template values/instances/negation, operands, wires) and the inventory of FlgNet / ident elements."""
    xdir = os.path.join(ROOT, "samples", sample + "_truth", "simaticml")
    if not os.path.isdir(xdir):
        return None
    from openpedata import code  # noqa: E402
    c, n = Checker(), 0
    model = Model(source)
    d = model.to_dict(keys=True)["project"]
    for dev in d["devices"]:
        for plc in dev["plcs"]:
            for b in plc["blocks"]:
                f = os.path.join(xdir, b["name"] + ".xml")
                nets = [x for x in b.get("networks", []) if x["language"] in ("LAD", "FBD")]
                if not nets or not os.path.isfile(f):
                    continue
                exp = [e for e in simaticml_networks(f) if e[0] in ("LAD", "FBD")]
                c.eq("%s network count" % b["name"], len(nets), len(exp))
                for pn, (lang, title, comment, nl) in zip(nets, exp):
                    w = "%s network %d" % (b["name"], pn["index"])
                    c.eq(w + " language", pn["language"], lang)
                    c.eq(w + " title", pn["title"], title)
                    c.eq(w + " comment", pn["comment"], comment)
                    if nl is None:  # network without FlgNet (new OB): the parser reports it as empty
                        c.eq(w + " empty network", bool(pn.get("empty")), True)
                        continue
                    c.eq(w + " wires and parts", parsed_netlist(pn), nl)
                    c.eq(w + " unhandled elements", pn.get("unhandled", []), [])
                    c.eq(w + " unknown ident kinds", pn.get("unknownIdentKinds", []), [])
                    n += len(nl[0])
                    root = code._xml(model.p.attr(pn["key"], "ICompileUnitData", "Data"))
                    for e in (root.iter() if root is not None else []):
                        t = code._local(e.tag)
                        inv["flg"][t] = inv["flg"].get(t, 0) + 1
                        for a in e.attrib:
                            inv["flgattr"][t + "@" + a] = inv["flgattr"].get(t + "@" + a, 0) + 1
                for blk in [k for k in model.p.of_type("CodeBlockData") if model.p.name(k) == b["name"]]:
                    ic = model.rel1(blk, "CoreObject2IdentContainer")
                    for part in model.p.targets(ic, "IdentParts") if ic else []:
                        root = code._xml(model.p.attr(part, "IIdentPartData", "PayLoad"))
                        for el in list(root) if root is not None else []:
                            inv["ident"][code._local(el.tag)] = inv["ident"].get(code._local(el.tag), 0) + 1
                            for sub in el.iter():
                                t = code._local(sub.tag)
                                inv["identsub"][t] = inv["identsub"].get(t, 0) + 1
    return c, n


def check_stores(sample, source):
    """Derived stores: PEData.idx against the plf (folders), XRef.db parts/objects/relations against the plf and its tag
    addresses against the LogicalAddress of the tags, savepoint link plf -> XRef.db. Returns (Checker, xref relations)."""
    from openpedata import pidx, xref  # noqa: E402
    from openpedata.project import Project  # noqa: E402
    c = Checker()
    ix = pidx.check_project(source)
    stale = bool(ix and ix.get("stale"))  # compacted plf (archive + close) with the old idx: TIA rebuilds it later
    if ix is not None and not stale:
        c.eq("idx consistent with plf", ix["problems"], [])
        sg = ix["commitCopySignatures"]
        c.eq("idx commit copies carry an RSA block of the project key", sg["padded"], sg["total"])
    x = xref.read(source)
    if x is None:
        return c, 0
    p = Project(source)
    keys = set(p.latest)
    # XRef.db also names objects that were deleted before the plf was compacted (archive/close drops their tombstones): tolerate those
    missing = sorted(k for k in x["parts"].values() if k and k not in keys)
    if not source.lower().endswith(".zap21") and len(missing) * 2 < len(x["parts"]):  # a scratch project with thousands of created and deleted blocks keeps most of them only in XRef.db
        c.eq("xref parts resolve to plf records", missing, [])
    c.eq("xref objs use known parts", [o for o in x["objs"].values() if o["scope"] is None or o["primary"] is None], [])
    c.eq("xref rels use known objs", [r for r in x["rels"] if r["left"] not in x["objs"] or r["right"] not in x["objs"]], [])
    st = pidx.external_stores(source)
    if st.get("xref", {}).get("file"):
        c.eq("xref savepoint equals plf expectation", st["xref"]["file"], st["xref"]["plf"])
    area = {1: "I", 2: "Q", 3: "M"}
    for a in x["addrs"]:
        k = x["objs"][a["obj"]]["primary"]
        if k not in p.objects:
            continue
        width, bit = a["lastBit"] - a["firstBit"] + 1, a["firstBit"]
        # ranges 4/5 = S5 timer/counter (sample 064): firstBit = number * 8, one bit wide
        text = "%%%s%d" % ("T" if a["range"] == 4 else "C", bit // 8) if a["range"] in (4, 5) \
            else "%%%s%d.%d" % (area[a["range"]], bit // 8, bit % 8) if width == 1 else "%%%s%s%d" % (
            area[a["range"]], {8: "B", 16: "W", 32: "D"}.get(width, "?"), bit // 8)
        c.eq("xref address of tag %s" % p.name(k), text, p.attr(k, "ITagAddress", "LogicalAddress"))
    return c, len(x["rels"])


def check_verify(sample, source):
    """--verify on the sample: header, record hashes, chain hash and RSA signature of every commit must be intact."""
    from openpedata import verify  # noqa: E402
    rep = verify.verify_project(source)
    c = Checker()
    c.eq("verify ok", rep["ok"], True)
    for f in rep["files"]:
        c.eq("%s problems" % f["file"], f["problems"], [])
    commits = sum(len(f["commits"]) for f in rep["files"])
    return c, commits


def check_verify_tamper():
    """Negative test: a flipped byte in a record body, in the chain hash and in the signature of a commit must be detected."""
    from openpedata import plf, verify  # noqa: E402
    c = Checker()
    import zipfile
    with zipfile.ZipFile(os.path.join(ROOT, "samples", "005_tag_int.zap21")) as z:
        buf = bytearray(z.read("System/PEData.plf"))
    c.eq("tamper: untouched file ok", verify.verify_plf(bytes(buf))["ok"], True)
    recs = plf.read_records(bytes(buf))
    plain = next(r for r in recs if r.type_id not in (verify.COMMIT, verify.CLOSE, verify.KEY) and r.length > 60)
    commit = next(r for r in recs if r.type_id == verify.COMMIT)
    for what, off in (("record body", plain.offset + 40), ("chain hash", commit.offset + 100), ("signature", commit.offset + 200)):
        b2 = bytearray(buf)
        b2[off] ^= 0x01
        rep = verify.verify_plf(bytes(b2))
        c.eq("tamper: %s detected" % what, rep["ok"], False)
    b3 = bytes(buf) + bytes([0, 1])
    c.eq("tamper: trailing bytes detected", verify.verify_plf(b3)["ok"], False)
    return c, 5


def check_scl_sd(sample, source, tags):
    """SCL networks against the SD export of every block (samples/<sample>_truth/sd/*.s7dcl); counts the tags seen."""
    sdir = os.path.join(ROOT, "samples", sample + "_truth", "sd")
    if not os.path.isdir(sdir):
        return None
    from openpedata import code  # noqa: E402
    c, n = Checker(), 0
    model = Model(source)
    d = model.to_dict(keys=True)["project"]
    for dev in d["devices"]:
        for plc in dev["plcs"]:
            for b in plc["blocks"]:
                f = os.path.join(sdir, b["name"] + ".s7dcl")
                if "networks" not in b or not os.path.isfile(f):
                    continue
                exp = sd_networks(f)
                nets = [x for x in b["networks"] if x["language"] == "SCL"]
                if not nets:
                    continue
                c.eq("%s SCL network count" % b["name"], len(nets), len(exp))
                for x, e in zip(nets, exp):
                    got = [t.rstrip() for t in x.get("scl", "").splitlines()]
                    while e and not e[-1].strip():
                        e = e[:-1]
                    while e and not e[0].strip():  # leading empty line of the SD export (UseCase_Textlist of the demo project)
                        e = e[1:]
                    c.eq("%s SCL text (SD export)" % b["name"], got, e)
                    c.eq("%s unknown tokens" % b["name"], x.get("unknownTokens", []), [])
                    n += 1
                    root = code._xml(model.p.attr(x["key"], "ICompileUnitData", "Data"))
                    code.tag_counts(root, tags)
    return c, n


def run(argv):
    rows, ok = [], True
    sd = os.path.join(ROOT, "samples")
    # samples: unpacked project folders and .zap21 archives (most samples exist only as archive)
    names = sorted({x for x in os.listdir(sd) if os.path.isdir(os.path.join(sd, x, "System"))}
                   | {x[:-len(".zap21")] for x in os.listdir(sd) if x.endswith(".zap21")})

    def sources(s):
        return [p for p in (os.path.join(sd, s), os.path.join(sd, s + ".zap21")) if os.path.exists(p)]

    for s in (x for x in names if truth_dir(x)):
        for src in sources(s):
            c, n = check(s, src)
            ok &= not c.fail
            rows.append((s, "zap21" if src.endswith(".zap21") else "folder", n, c))
    irows = []
    for s in names:
        for src in sources(s):
            r = check_interfaces(s, src)
            if r:
                ok &= not r[0].fail
                irows.append((s, "zap21" if src.endswith(".zap21") else "folder") + r)
    vrows = []
    for s in names:
        for src in sources(s):
            if True:
                r = check_verify(s, src)
                ok &= not r[0].fail
                vrows.append((s, "zap21" if src.endswith(".zap21") else "folder") + r)
    tam = check_verify_tamper()
    ok &= not tam[0].fail
    strows = []
    for s in names:
        for src in sources(s):
            if True:
                r = check_stores(s, src)
                ok &= not r[0].fail
                strows.append((s, "zap21" if src.endswith(".zap21") else "folder") + r)
    srows, tags = [], {}
    for s in names:
        for src in sources(s):
            r = check_scl_sd(s, src, tags if src == sources(s)[0] else {})
            if r:
                ok &= not r[0].fail
                srows.append((s, "zap21" if src.endswith(".zap21") else "folder") + r)
    frows, inv = [], {"flg": {}, "flgattr": {}, "ident": {}, "identsub": {}}
    for s in names:
        for src in sources(s):
            r = check_flg_xml(s, src, inv if src == sources(s)[0] else {"flg": {}, "flgattr": {}, "ident": {}, "identsub": {}})
            if r:
                ok &= not r[0].fail
                frows.append((s, "zap21" if src.endswith(".zap21") else "folder") + r)
    print("| Sample | Source | Devices | Tag tables | Tags | Blocks | UDTs | Tags | Checks | Result |")
    print("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for s, kind, n, c in rows:
        print("| %s | %s | %d | %d | %d | %d | %d | %d | %d | %s |" % (s, kind, n["devices"], n["tagTables"], n["tags"],
                                                                     n["blocks"], n["types"], n["members"], c.n,
                                                            "OK" if not c.fail else "%d errors" % len(c.fail)))
    if irows:
        print()
        print("| Sample (interfaces FB/FC/IDB) | Source | Interface tags | Checks | Result |")
        print("| --- | --- | --- | --- | --- |")
        for s, kind, c, n in irows:
            print("| %s | %s | %d | %d | %s |" % (s, kind, n, c.n, "OK" if not c.fail else "%d errors" % len(c.fail)))
    print()
    print("Integrity (`--verify`): header hash, record hashes, hash chain and RSA signature of all $$COMMIT$ records; tamper test (%d checks): %s" % (
        tam[1], "OK" if not tam[0].fail else "ERROR"))
    print()
    print("| Sample | Source | Commits (chain+signature) | Checks | Result |")
    print("| --- | --- | --- | --- | --- |")
    for s, kind, c, n in vrows:
        print("| %s | %s | %d | %d | %s |" % (s, kind, n, c.n, "OK" if not c.fail else "%d errors" % len(c.fail)))
    for f in tam[0].fail:
        print("FAIL tamper: " + f)
    print()
    print("Derived stores: `PEData.idx` against PEData.plf (folders only), `XRef.db` (parts -> plf keys, relations, tag addresses against LogicalAddress, savepoint)")
    print()
    print("| Sample | Source | XRef relations | Checks | Result |")
    print("| --- | --- | --- | --- | --- |")
    for s, kind, c, n in strows:
        print("| %s | %s | %d | %d | %s |" % (s, kind, n, c.n, "OK" if not c.fail else "%d errors" % len(c.fail)))
    if srows:
        from openpedata import code  # noqa: E402
        print()
        print("| Sample (SCL vs. SD export) | Source | Networks | Checks | Result |")
        print("| --- | --- | --- | --- | --- |")
        for s, kind, c, n in srows:
            print("| %s | %s | %d | %d | %s |" % (s, kind, n, c.n, "OK" if not c.fail else "%d errors" % len(c.fail)))
        print()
        print("SCL tokens in the source text (count over all samples), status: text/attribute/container/special = handled, - = not handled")
        print()
        print("| Tag | Count | Status |")
        print("| --- | --- | --- |")
        for t in sorted(tags):
            print("| %s | %d | %s |" % (t, tags[t], code.token_status(t) or "-"))
        ok &= all(code.token_status(t) for t in tags)
    if frows:
        from openpedata import code  # noqa: E402
        print()
        print("| Sample (LAD/FBD vs. SimaticML) | Source | Wires | Checks | Result |")
        print("| --- | --- | --- | --- | --- |")
        for s, kind, c, n in frows:
            print("| %s | %s | %d | %d | %s |" % (s, kind, n, c.n, "OK" if not c.fail else "%d errors" % len(c.fail)))
        print()
        print("FlgNet elements in the plf (count over all LAD/FBD samples), status: handled = element and attributes are evaluated")
        print()
        print("| Element | Count | Attributes (count) | Status |")
        print("| --- | --- | --- | --- |")
        for t in sorted(inv["flg"]):
            attrs_seen = {k.split("@")[1]: v for k, v in inv["flgattr"].items() if k.startswith(t + "@")}
            known = code.FLG_HANDLED.get(t)
            status = "handled" if known is not None and set(attrs_seen) <= known else "NOT HANDLED"
            ok &= status == "handled"
            print("| %s | %d | %s | %s |" % (t, inv["flg"][t], ", ".join("%s %d" % kv for kv in sorted(attrs_seen.items())), status))
        print()
        print("Ident table (IdentXmlPart): object kinds and sub-elements. read = value goes into the output, metadata = timestamps, memory layout or compiler aids (not evaluated)")
        print()
        print("| Object kind | Count | Status |")
        print("| --- | --- | --- |")
        for t in sorted(inv["ident"]):
            st = "handled" if t in code.IDENT_KINDS else "NOT HANDLED"
            ok &= st == "handled"
            print("| %s | %d | %s |" % (t, inv["ident"][t], st))
        print()
        print("| Sub-element | Count | Status |")
        print("| --- | --- | --- |")
        for t in sorted(inv["identsub"]):
            if t in code.IDENT_KINDS:
                continue
            print("| %s | %d | %s |" % (t, inv["identsub"][t], "read" if t in code.IDENT_READ else "metadata"))
    for s, kind, n, c in rows:
        for f in c.fail:
            print("FAIL %s (%s): %s" % (s, kind, f))
    for s, kind, c, n in frows:
        for f in c.fail:
            print("FAIL %s (%s): %s" % (s, kind, f))
    for s, kind, c, n in vrows:
        for f in c.fail:
            print("FAIL %s (%s): %s" % (s, kind, f))
    for s, kind, c, n in srows:
        for f in c.fail:
            print("FAIL %s (%s): %s" % (s, kind, f))
    for s, kind, c, n in irows:
        for f in c.fail:
            print("FAIL %s (%s): %s" % (s, kind, f))
    return 0 if ok else 1


def main(argv):
    """Runs all checks, prints the result tables and writes them to tests/RESULTS.md (like the other suites)."""
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        rc = run(argv)
    text = buf.getvalue()
    with open(os.path.join(HERE, "RESULTS.md"), "w", encoding="utf-8") as f:
        f.write(text)
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stdout.write(text)
    return rc


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
