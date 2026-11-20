"""Creates the SimaticML-only blocks of sample 034_flg_misc (jump/label, Calculate with equation) in the open project.
python tools/make_misc_xml.py <template.xml>   (template: SimaticML export of a generated FB with the standard interface)"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml  # noqa: E402
import xml_net2  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

NS = gen_xml.NS
tpl = open(sys.argv[1], encoding="utf-8-sig").read()
c = TiaClient(default_pid())


def access(uid, name):
    return '<Access Scope="LocalVariable" UId="%d"><Symbol><Component Name="%s" /></Symbol></Access>' % (uid, name)


def jump_net(lang):
    src = '<Wire UId="25"><Powerrail /><NameCon UId="23" Name="in" /></Wire><Wire UId="26"><IdentCon UId="21" /><NameCon UId="23" Name="operand" /></Wire>'
    if lang == "FBD":
        return ('<FlgNet xmlns="%s"><Parts>%s%s<Access Scope="Label" UId="22"><Label Name="lbl1" /></Access><Part Name="A" UId="23"><TemplateValue Name="Card" Type="Cardinality">2</TemplateValue></Part>'
                '<Part Name="Jump" UId="24" /></Parts><Wires><Wire UId="26"><IdentCon UId="21" /><NameCon UId="23" Name="in1" /></Wire>'
                '<Wire UId="27"><IdentCon UId="30" /><NameCon UId="23" Name="in2" /></Wire><Wire UId="31"><NameCon UId="23" Name="out" /><NameCon UId="24" Name="in" /></Wire>'
                '<Wire UId="28"><IdentCon UId="22" /><NameCon UId="24" Name="label" /></Wire></Wires></FlgNet>') % (NS, access(21, "a"), access(30, "b"))
    return ('<FlgNet xmlns="%s"><Parts>%s<Access Scope="Label" UId="22"><Label Name="lbl1" /></Access><Part Name="Contact" UId="23" /><Part Name="Jump" UId="24" /></Parts>'
            '<Wires>%s<Wire UId="27"><NameCon UId="23" Name="out" /><NameCon UId="24" Name="in" /></Wire>'
            '<Wire UId="28"><IdentCon UId="22" /><NameCon UId="24" Name="label" /></Wire></Wires></FlgNet>') % (NS, access(21, "a"), src)


def label_net(lang):
    if lang == "FBD":
        return ('<FlgNet xmlns="%s"><Labels><LabelDeclaration UId="29"><Label Name="lbl1" /></LabelDeclaration></Labels><Parts>%s%s<Part Name="Coil" UId="24" /></Parts>'
                '<Wires><Wire UId="26"><IdentCon UId="21" /><NameCon UId="24" Name="in" /></Wire><Wire UId="28"><IdentCon UId="22" /><NameCon UId="24" Name="operand" /></Wire></Wires></FlgNet>') % (NS, access(21, "a"), access(22, "q"))
    return ('<FlgNet xmlns="%s"><Labels><LabelDeclaration UId="29"><Label Name="lbl1" /></LabelDeclaration></Labels><Parts>%s%s<Part Name="Contact" UId="23" /><Part Name="Coil" UId="24" /></Parts>'
            '<Wires><Wire UId="25"><Powerrail /><NameCon UId="23" Name="in" /></Wire><Wire UId="26"><IdentCon UId="21" /><NameCon UId="23" Name="operand" /></Wire>'
            '<Wire UId="27"><NameCon UId="23" Name="out" /><NameCon UId="24" Name="in" /></Wire><Wire UId="28"><IdentCon UId="22" /><NameCon UId="24" Name="operand" /></Wire></Wires></FlgNet>') % (NS, access(21, "a"), access(22, "q"))


def calc_net(lang, eq):
    wen = '<Wire UId="29"><Powerrail /><NameCon UId="25" Name="en" /></Wire>' if lang == "LAD" else '<Wire UId="29"><OpenCon UId="30" /><NameCon UId="25" Name="en" /></Wire>'
    return ('<FlgNet xmlns="%s"><Parts>%s%s%s<Part Name="Calc" UId="25"><Equation>%s</Equation><TemplateValue Name="SrcType" Type="Type">Int</TemplateValue>'
            '<TemplateValue Name="Card" Type="Cardinality">2</TemplateValue></Part></Parts><Wires>%s'
            '<Wire UId="26"><IdentCon UId="21" /><NameCon UId="25" Name="in1" /></Wire><Wire UId="27"><IdentCon UId="22" /><NameCon UId="25" Name="in2" /></Wire>'
            '<Wire UId="28"><NameCon UId="25" Name="out" /><IdentCon UId="23" /></Wire></Wires></FlgNet>') % (NS, access(21, "ia"), access(22, "ib"), access(23, "io"), eq, wen)


def run(name, xml):
    p = os.path.abspath(os.path.join(os.path.dirname(__file__), "sources", name + ".xml"))
    open(p, "w", encoding="utf-8").write(xml)
    e, r = c.json("TiaPlcSoftware", operation="import", objectKind="block", sourcePath=p, importMode="Override", waitMs="45000")
    if e:
        print(name, "IMPORT FAILED", (r if isinstance(r, str) else r.get("message", ""))[-300:].replace(chr(13) + chr(10), " "))
        return False
    comp = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
    errs = [m["path"].split(">")[-1].strip() + ": " + m["description"] for m in comp.get("messages", []) if m.get("state") == "Error"]
    print(name, "OK" if not errs else "COMPILE " + "; ".join(errs)[:300])
    return not errs


for lang in ("LAD", "FBD"):
    run("FB_%sJump" % lang.title(), xml_net2.two_net(tpl, jump_net(lang), label_net(lang), "FB_%sJump" % lang.title(), lang))
    run("FB_%sCalc" % lang.title(), gen_xml.build_block(tpl, "FB_%sCalc" % lang.title(), lang, calc_net(lang, "IN1 + IN2")))
