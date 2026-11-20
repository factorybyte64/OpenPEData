"""One-off probe: OK contact with <Negated Name=..> variants (is NOT_OK a negated OK?). python tools/xml_probe_neg.py <LAD|FBD>"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml
from tiaclient import TiaClient, default_pid
lang = sys.argv[1]
tpl = open(os.path.join(os.path.dirname(__file__), "sources", "FX_T.xml"), encoding="utf-8-sig").read()
c = TiaClient(default_pid())
out = os.path.join(os.path.dirname(__file__), "sources", "dbg")
os.makedirs(out, exist_ok=True)
gen_xml.EXTRA.clear()
gen_xml.EXTRA["p0"] = "Real"
pins = [("operand", "in", "p0")]
for pin in ("operand", "out", "pre"):
    xml_ = gen_xml.build_block(tpl, "FX_PROBE", lang, gen_xml.flgnet(lang, "Ok", None, {}, pins, False, "pre", False))
    xml_ = xml_.replace('<Part Name="Ok" UId="22"></Part>', '<Part Name="Ok" UId="22"><Negated Name="%s" /></Part>' % pin)
    fp = os.path.join(out, "FX_PROBE.xml")
    open(fp, "w", encoding="utf-8").write(xml_)
    e, r = c.json("TiaPlcSoftware", operation="import", objectKind="block", sourcePath=fp, importMode="Override", waitMs="45000")
    print(pin, "ERR" if e else "OK", (r if isinstance(r, str) else r.get("message", ""))[-300:] if e else "")
    if not e:
        comp = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
        print("  ", "; ".join(m["description"] for m in (comp.get("messages") or []) if m.get("state") == "Error" and "FX_PROBE" in m.get("path", ""))[:300])
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name="FX_PROBE")
