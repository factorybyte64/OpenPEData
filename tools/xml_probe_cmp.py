"""One-off probe: hand-built compare network (T_Comp / S_Comp, wired like the Eq comparator: pre, IN1, IN2, OUT -> Coil).
python tools/xml_probe_cmp.py <Part> <type> <tvTypeName> [version]"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml
from tiaclient import TiaClient, default_pid
nm, typ, tvk = sys.argv[1:4]
ver = sys.argv[4] if len(sys.argv) > 4 else None
tpl = open(os.path.join(os.path.dirname(__file__), "sources", "FX_T.xml"), encoding="utf-8-sig").read()
c = TiaClient(default_pid())
out = os.path.join(os.path.dirname(__file__), "sources", "dbg")
os.makedirs(out, exist_ok=True)
gen_xml.EXTRA.clear()
gen_xml.EXTRA["p0"] = typ
gen_xml.EXTRA["p1"] = typ
V = ' Version="%s"' % ver if ver else ""
for rel in ("EQ", "Eq", "GE"):
    net = ('<FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5"><Parts>'
           '<Access Scope="LocalVariable" UId="21"><Symbol><Component Name="p0" /></Symbol></Access>'
           '<Access Scope="LocalVariable" UId="22"><Symbol><Component Name="p1" /></Symbol></Access>'
           '<Access Scope="LocalVariable" UId="23"><Symbol><Component Name="q" /></Symbol></Access>'
           '<Part Name="%s" UId="24"%s><TemplateValue Name="%s" Type="Type">%s</TemplateValue><TemplateValue Name="relation" Type="Operation">%s</TemplateValue></Part>'
           '</Parts><Wires>'
           '<Wire UId="30"><Powerrail /><NameCon UId="24" Name="en" /></Wire>'
           '<Wire UId="31"><IdentCon UId="21" /><NameCon UId="24" Name="IN1" /></Wire>'
           '<Wire UId="32"><IdentCon UId="22" /><NameCon UId="24" Name="IN2" /></Wire>'
           '<Wire UId="33"><NameCon UId="24" Name="OUT" /><IdentCon UId="23" /></Wire>'
           '</Wires></FlgNet>') % (nm, V, tvk, typ, rel)
    xml_ = gen_xml.build_block(tpl, "FX_PROBE", "LAD", net)
    fp = os.path.join(out, "FX_PROBE.xml")
    open(fp, "w", encoding="utf-8").write(xml_)
    e, r = c.json("TiaPlcSoftware", operation="import", objectKind="block", sourcePath=fp, importMode="Override", waitMs="45000")
    print(rel, "ERR" if e else "OK", (r if isinstance(r, str) else r.get("message", ""))[-350:].replace("\n", " ") if e else "")
    if not e:
        comp = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
        print("  ", "; ".join(m["description"] for m in (comp.get("messages") or []) if m.get("state") == "Error" and "FX_PROBE" in m.get("path", ""))[:300])
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name="FX_PROBE")
        break
