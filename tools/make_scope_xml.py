"""Imports SimaticML-only networks that SD cannot express (absolute addresses, open branch, invisible pins, part comments) into the
open project, one block per variant, so that the plf shows how TIA stores them.
python tools/make_scope_xml.py <template.xml> [variant ...]   (template: tools/sources/FX_T.xml, a SimaticML export of a FB with the
standard interface a, b, q, ia, ib, io)"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

NS = gen_xml.NS
tpl = open(sys.argv[1], encoding="utf-8-sig").read()


def local(uid, name):
    return '<Access Scope="LocalVariable" UId="%d"><Symbol><Component Name="%s" /></Symbol></Access>' % (uid, name)


def addr(uid, area, typ, bit, block=None):
    return '<Access Scope="Address" UId="%d"><Address Area="%s" Type="%s"%s BitOffset="%d" /></Access>' % (
        uid, area, typ, (' BlockNumber="%d"' % block) if block is not None else "", bit)


def net(parts, wires, extra=""):
    return '<FlgNet xmlns="%s"><Parts>%s</Parts><Wires>%s</Wires></FlgNet>' % (NS, "".join(parts), "".join(wires))


def n_net(tplx, nets, name, lang):
    """Block with len(nets) networks from the one-network template."""
    x = gen_xml.build_block(tplx, name, lang, nets[0])
    cu = re.search(r'<SW\.Blocks\.CompileUnit ID="3".*?</SW\.Blocks\.CompileUnit>', x, flags=re.S).group(0)
    out = cu
    for k, n in enumerate(nets[1:], 1):
        c = re.sub(r"<NetworkSource>.*?</NetworkSource>", lambda m: "<NetworkSource>" + n + "</NetworkSource>", cu, flags=re.S)
        c = c.replace('ID="3"', 'ID="%d"' % (20 + 10 * k)).replace('ID="4"', 'ID="%d"' % (21 + 10 * k)).replace(
            'ID="5"', 'ID="%d"' % (22 + 10 * k)).replace('ID="6"', 'ID="%d"' % (23 + 10 * k)).replace('ID="7"', 'ID="%d"' % (24 + 10 * k))
        out += c
    return x.replace(cu, out)


def contact_coil(contact, coil, lang):
    """a --| |-- coil"""
    parts = [contact, coil, '<Part Name="Contact" UId="23" />', '<Part Name="Coil" UId="24" />']
    wires = ['<Wire UId="25"><Powerrail /><NameCon UId="23" Name="in" /></Wire>',
             '<Wire UId="26"><IdentCon UId="21" /><NameCon UId="23" Name="operand" /></Wire>',
             '<Wire UId="27"><NameCon UId="23" Name="out" /><NameCon UId="24" Name="in" /></Wire>',
             '<Wire UId="28"><IdentCon UId="22" /><NameCon UId="24" Name="operand" /></Wire>']
    return net(parts, wires)


def move_net(src, dst, typ):
    parts = [src, dst, '<Part Name="Move" UId="23" DisabledENO="true"><TemplateValue Name="Card" Type="Cardinality">1</TemplateValue><TemplateValue Name="SrcType" Type="Type">%s</TemplateValue></Part>' % typ]
    wires = ['<Wire UId="25"><Powerrail /><NameCon UId="23" Name="en" /></Wire>',
             '<Wire UId="26"><IdentCon UId="21" /><NameCon UId="23" Name="in" /></Wire>',
             '<Wire UId="27"><NameCon UId="23" Name="out1" /><IdentCon UId="22" /></Wire>']
    return net(parts, wires)


def variants(lang):
    v = {}
    # absolute addresses of all areas as contact/coil and move operands
    v["Addr"] = [
        contact_coil(addr(21, "Memory", "Bool", 0), addr(22, "Memory", "Bool", 83), lang),
        contact_coil(addr(21, "Input", "Bool", 9), addr(22, "Output", "Bool", 1), lang),
        contact_coil(addr(21, "PeripheryInput", "Bool", 0), addr(22, "PeripheryOutput", "Bool", 0), lang),
        contact_coil(addr(21, "Memory", "Bool", 1000), addr(22, "Memory", "Bool", 1001), lang),
    ]
    # open branch: contact with a parallel branch that ends open
    v["Open"] = [net(
        [local(21, "a"), local(22, "b"), local(23, "q"), local(28, "c"), '<Part Name="Contact" UId="24" />', '<Part Name="Contact" UId="25" />', '<Part Name="Contact" UId="27" />', '<Part Name="Coil" UId="26" />'],
        ['<Wire UId="30"><Powerrail /><NameCon UId="24" Name="in" /><NameCon UId="25" Name="in" /><NameCon UId="27" Name="in" /></Wire>',
         '<Wire UId="31"><IdentCon UId="21" /><NameCon UId="24" Name="operand" /></Wire>',
         '<Wire UId="32"><IdentCon UId="22" /><NameCon UId="25" Name="operand" /></Wire>',
         '<Wire UId="36"><IdentCon UId="28" /><NameCon UId="27" Name="operand" /></Wire>',
         '<Wire UId="33"><NameCon UId="24" Name="out" /><NameCon UId="25" Name="out" /><NameCon UId="26" Name="in" /></Wire>',
         '<Wire UId="34"><NameCon UId="27" Name="out" /><Openbranch /></Wire>',
         '<Wire UId="35"><IdentCon UId="23" /><NameCon UId="26" Name="operand" /></Wire>'])]
    # part comment and invisible pin
    cm = '<Comment><MultiLanguageText Lang="en-US">part comment</MultiLanguageText></Comment>'
    cm = '<Comment><MultiLanguageText Lang="en-US">part comment</MultiLanguageText></Comment>'
    v["Cmt"] = [net(
        [local(21, "ia"), local(22, "io"), '<Part Name="Move" UId="23" DisabledENO="true"><TemplateValue Name="Card" Type="Cardinality">1</TemplateValue><TemplateValue Name="SrcType" Type="Type">Int</TemplateValue>%s</Part>' % cm],
        ['<Wire UId="25"><Powerrail /><NameCon UId="23" Name="en" /></Wire>',
         '<Wire UId="26"><IdentCon UId="21" /><NameCon UId="23" Name="in" /></Wire>',
         '<Wire UId="27"><NameCon UId="23" Name="out1" /><IdentCon UId="22" /></Wire>'])]
    call = ('<Call UId="23"><CallInfo Name="FC_SclCtl" BlockType="FC"><Parameter Name="n" Section="Input" Type="Int" /><Parameter Name="flag" Section="Input" Type="Bool" />'
            '<Parameter Name="Ret_Val" Section="Return" Type="Int" /><Parameter Name="total" Section="Output" Type="Int" /></CallInfo><Invisible Name="flag" /></Call>')
    v["Invis"] = [net(
        [local(21, "ia"), local(22, "io"), local(29, "ib"), call],
        ['<Wire UId="25"><Powerrail /><NameCon UId="23" Name="en" /></Wire>',
         '<Wire UId="26"><IdentCon UId="21" /><NameCon UId="23" Name="n" /></Wire>',
         '<Wire UId="27"><NameCon UId="23" Name="Ret_Val" /><IdentCon UId="22" /></Wire>',
         '<Wire UId="28"><NameCon UId="23" Name="total" /><IdentCon UId="29" /></Wire>'])]
    return v


def main():
    c = TiaClient(default_pid())
    want = sys.argv[2:]
    for lang in ("LAD",):
        for vn, nets in variants(lang).items():
            if want and vn not in want:
                continue
            name = "FB_%sScope%s" % (lang.title(), vn)
            xml = n_net(tpl, nets, name, lang)
            p = os.path.abspath(os.path.join(os.path.dirname(__file__), "sources", name + ".xml"))
            open(p, "w", encoding="utf-8").write(xml)
            e, r = c.json("TiaPlcSoftware", operation="import", objectKind="block", sourcePath=p, importMode="Override", waitMs="45000")
            if e:
                print(name, "IMPORT FAILED", (r if isinstance(r, str) else r.get("message", ""))[-400:].replace(chr(13) + chr(10), " "), flush=True)
                continue
            comp = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
            errs = [m["path"].split(">")[-1].strip() + ": " + m["description"] for m in comp.get("messages", []) if m.get("state") == "Error"]
            print(name, "OK" if not errs else "COMPILE " + "; ".join(errs)[:300], flush=True)


main()
