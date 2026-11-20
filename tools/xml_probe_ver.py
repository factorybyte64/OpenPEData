"""One-off probe: SimaticML import of a typed 2-input compare instruction over version/template candidates.
python tools/xml_probe_ver.py <LAD|FBD> <Part> <type> [relation ...]  (versions 1.0..2.1 tried)"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml
from tiaclient import TiaClient, default_pid
lang, nm, typ = sys.argv[1], sys.argv[2], sys.argv[3]
rels = sys.argv[4:] or ["Eq"]
tpl = open(os.path.join(os.path.dirname(__file__), "sources", "FX_T.xml"), encoding="utf-8-sig").read()
c = TiaClient(default_pid())
out = os.path.join(os.path.dirname(__file__), "sources", "dbg")
os.makedirs(out, exist_ok=True)
gen_xml.EXTRA.clear()
gen_xml.EXTRA["s_in1"] = typ
gen_xml.EXTRA["s_in2"] = typ
pins = [("IN1", "in", "s_in1"), ("IN2", "in", "s_in2"), ("OUT", "out", "b")]
for ver in (None, "1.0", "1.1", "1.2", "2.0", "2.1"):
    for tvk in ("date_type", "src_type"):
        for rel in rels:
            e, msg = gen_xml.try_import(c, tpl, "FX_PROBE", lang, nm, pins, {tvk: typ, "relation": rel}, "en", ver, out, False)
            print(ver, tvk, rel, "OK" if not e else msg[-260:], flush=True)
            if not e:
                c.call("TiaPlcSoftware", operation="delete", objectKind="block", name="FX_PROBE")
