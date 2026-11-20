"""Probe Openness (SimaticML) instruction names: python tools/lad_xprobe.py <LAD|FBD> <template.xml> Name1 Name2 ...
Imports a block with a single <Part Name=..> and reports 'cannot be found' (unknown) or the other message / OK."""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

lang = sys.argv[1].upper()
tpl = open(sys.argv[2], encoding="utf-8-sig").read()
c = TiaClient(default_pid())
p = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sources", "xml_probe.xml")
for nm in sys.argv[3:]:
    open(p, "w", encoding="utf-8").write(gen_xml.build_block(tpl, "FX_PROBE", lang, gen_xml.flgnet(lang, nm, None, {}, [("in", "in", "ia"), ("out", "out", "io")])))
    e, r = c.json("TiaPlcSoftware", operation="import", objectKind="block", sourcePath=os.path.abspath(p), importMode="Override", waitMs="45000")
    if e:
        m = (r if isinstance(r, str) else r.get("message", "")).replace("\r\n", " ")
        known = "cannot be found" not in m
        print("%-26s %s %s" % (nm, "KNOWN?" if known else "unknown", m[-170:] if known else ""))
    else:
        print("%-26s IMPORTED" % nm)
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name="FX_PROBE")
