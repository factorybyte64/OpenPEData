"""One-off probe: SimaticML import of one part with candidate TemplateValue values, TIA's answer per value.
python tools/xml_probe_tv.py <LAD|FBD> <Part> <tvName> <pin;dir;declaredType> ... -- value1 value2 ...  (declaredType 'x' = ready variable, else added as InOut member)"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml
from tiaclient import TiaClient, default_pid
a = sys.argv[1:]
lang, nm, tvk = a[:3]
sep = a.index("--")
pinspec, values = a[3:sep], a[sep + 1:]
tpl = open(os.path.join(os.path.dirname(__file__), "sources", "FX_T.xml"), encoding="utf-8-sig").read()
c = TiaClient(default_pid())
out = os.path.join(os.path.dirname(__file__), "sources", "dbg")
os.makedirs(out, exist_ok=True)
gen_xml.EXTRA.clear()
pins = []
for k, s in enumerate(pinspec):
    pin, d, typ = s.split(";")
    v = "p%d" % k
    gen_xml.EXTRA[v] = typ
    pins.append((pin, d, v))
for val in values:
    e, msg = gen_xml.try_import(c, tpl, "FX_PROBE", lang, nm, pins, dict([x.split("=", 1) for x in os.environ.get("TV_FIXED", "").split(";") if x], **{tvk: val}), "en", None, out, False)
    print(val, "OK" if not e else msg[-300:], flush=True)
    if not e:
        comp = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
        print("   compile:", "; ".join(m["description"] for m in (comp.get("messages") or []) if m.get("state") == "Error" and "FX_PROBE" in m.get("path", ""))[:300])
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name="FX_PROBE")
