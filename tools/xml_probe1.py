"""One-off probe: import a single part name with given pins and print TIA's raw answer. python tools/xml_probe1.py <LAD|FBD> <Part> [pin:dir:var ...]"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml
from tiaclient import TiaClient, default_pid
lang, nm = sys.argv[1], sys.argv[2]
pins = [tuple(a.split(";")) for a in sys.argv[3:]]
tpl = open(os.path.join(os.path.dirname(__file__), "sources", "FX_T.xml"), encoding="utf-8-sig").read()
c = TiaClient(default_pid())
out = os.path.join(os.path.dirname(__file__), "sources", "dbg"); os.makedirs(out, exist_ok=True)
e, msg = gen_xml.try_import(c, tpl, "FX_PROBE", lang, nm, pins, {}, "in" if not pins or pins[0][0] != "in" else None, None, out, False)
print("ERR" if e else "OK", msg[-700:])
if not e:
    c.call("TiaPlcSoftware", operation="delete", objectKind="block", name="FX_PROBE")
