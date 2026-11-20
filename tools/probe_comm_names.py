"""Probes the real instruction names of the communication catalogs (Modbus, PtP, USS, Teleservice, ...): the catalog titles are often
page headings ('Description of MB_MASTER'). python tools/probe_comm_names.py   -> docs/catalog/comm_names.json {name: sdName | null}"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_lad  # noqa: E402
import lad_instr  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

NAMES = ["USS_Port_Scan", "USS_Drive_Control", "USS_Read_Param", "USS_Write_Param", "USS_Port", "USS_Drv", "TM_Mail", "TMAIL_C", "Tm_Mail"]
c = TiaClient(default_pid())
out = os.path.join(HERE, "sources", "names")
os.makedirs(out, exist_ok=True)
res = {}
for nm in NAMES:
    found = next((n for n in [nm] + lad_instr.name_variants(nm) if gen_lad.probe_name(c, out, n)), None)
    res[nm] = found
    print(nm, "->", found, flush=True)
json.dump(res, open(os.path.join(HERE, "..", "docs", "catalog", "comm_names.json"), "w"), indent=1)
