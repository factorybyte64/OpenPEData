"""Which Part names does the SimaticML importer know? python tools/xml_namecheck.py LAD Name1 Name2 ... -> known / unknown"""
import os, sys, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml
from tiaclient import TiaClient, default_pid
lang = sys.argv[1]
tpl = open(os.path.join(os.path.dirname(__file__), "sources", "FX_T.xml"), encoding="utf-8-sig").read()
c = TiaClient(default_pid())
out = os.path.join(os.path.dirname(__file__), "sources", "dbg"); os.makedirs(out, exist_ok=True)
for nm in sys.argv[2:]:
    e, msg = gen_xml.try_import(c, tpl, "FX_NAMECHK", lang, nm, [("in", "in", "a")], {}, None, None, out, False)
    known = "cannot be found" not in msg
    print("%-22s %s %s" % (nm, "KNOWN  " if known else "unknown", "" if not known else msg[-110:].replace("\n", " ") if False else ""), flush=True)
    if not e:
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name="FX_NAMECHK")
