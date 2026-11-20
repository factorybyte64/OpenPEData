"""Instruction catalog of TIA V21 for S7-1200/1500 from the table of contents of the installed help (only the titles of the
reference pages are read; no help text is copied). Writes docs/catalog/<name>.txt: one line per instruction page
`<group path> | <title>`. Usage: python tools/instr_catalog.py [help dir]"""
import html
import json
import os
import re
import sys
import zipfile
import xml.etree.ElementTree as ET

NS = "{http://www.siemens.com/2014/09/Automation/HelpPrototype/TocStorage}"
HELP = sys.argv[1] if len(sys.argv) > 1 else r"C:\Program Files\Siemens\Automation\Portal V21\Help\en-US"
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "docs", "catalog")
ZIPS = {
    "lad": "ProgKOP2MenUS.zip", "fbd": "ProgFUP2MenUS.zip", "scl": "ProgSCL2MenUS.zip", "extended": "ProgExtInstr2MenUS.zip",
    "technology": "ProgTech2MenUS.zip", "tecom": "ProgTecCom2MenUS.zip", "modbus": "ProgKomMODBUS2MenUS.zip",
    "modbustcp": "ProgKomModbTCP2MenUS.zip", "openuc": "ProgKomOUC2MenUS.zip", "ptp": "ProgKomPtP2MenUS.zip",
    "s7comm": "ProgKomS7Com2MenUS.zip", "uss": "ProgKomUSS2MenUS.zip", "webserver": "ProgKomWEBServ2MenUS.zip",
    "teleservice": "ProgInstTeleServ2MenUS.zip", "basic": "ProgBasInstr2MenUS.zip", "optional": "ProgOptInstr2MMainenUS.zip",
    "pid_1200": "ProgRef1200PIDenUS.zip", "block_lad": "ProgBlockKOPenUS.zip",
    "motion": "ProgAXenUS.zip", "pidcompact": "ProgFBPIDCompenUS.zip", "pid3step": "ProgFBPID3StepenUS.zip", "pidtemp": "ProgFBPIDTempenUS.zip",
    "hsc": "ProgFBHSCenUS.zip", "count1500": "ProgRef1500CountenUS.zip", "time1500": "ProgRef1500TimebasedenUS.zip", "ssi": "ProgFBSSIenUS.zip",
    "tio": "ProgFBTIOenUS.zip", "smc": "ProgFBSMCenUS.zip", "emc": "ProgFBEMCenUS.zip", "pidstd": "ProgFBPIDStdenUS.zip",
    "alarm": "ProgAlarm34enUS.zip", "stl": "ProgAWL15enUS.zip",
}


def walk(e, path, out):
    for c in e.findall(NS + "TocEntry"):
        t = c.get("title") or ""
        kids = c.findall(NS + "TocEntry")
        if c.get("imagecategory") == "Reference":
            out.append((" / ".join(path), t + (" [+%d]" % len(kids) if kids else ""), c.get("itemid")))
        walk(c, path + [t.split(" (S7")[0]], out)


def params(z, itemid):
    """Parameter table of an instruction page: [{name, decl, types}] (S7-1500 column); only the parameter facts are read."""
    member = itemid.split("/", 1)[1]
    if member not in z.namelist():
        return None
    b = z.read(member).decode("utf-8", "replace")
    for tb in re.findall(r"<table.*?</table>", b, re.S):
        rows = [[re.sub(r"\s+", " ", html.unescape(re.sub(r"<[^>]+>", " ", c))).strip() for c in re.findall(r"<t[dh].*?</t[dh]>", r, re.S)]
                for r in re.findall(r"<tr.*?</tr>", tb, re.S)]
        if not rows or not rows[0] or not rows[0][0].startswith("Parameter") or "Declaration" not in rows[0]:
            continue
        out = []
        for r in rows[1:]:
            if len(r) >= 5 and r[1] in ("Input", "Output", "InOut", "Return"):
                name = re.sub(r"\s*\d\)$", "", r[0])
                types = r[2]
                out.append({"name": name, "decl": r[1], "types": types})
        return out
    return None


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, f in ZIPS.items():
        p = os.path.join(HELP, f)
        if not os.path.isfile(p):
            print(name, "missing")
            continue
        z = zipfile.ZipFile(p)
        if "Toc/Default.xml" not in z.namelist():
            print(name, "no toc")
            continue
        out = []
        walk(ET.fromstring(z.read("Toc/Default.xml")), [], out)
        with open(os.path.join(OUT, name + ".txt"), "w", encoding="utf-8") as fh:
            for g, t, _ in out:
                fh.write("%s | %s\n" % (g, t))
        items = [{"group": g, "title": t, "name": re.split(r":| \(", t)[0].strip(), "params": params(z, i)} for g, t, i in out]
        with open(os.path.join(OUT, name + ".json"), "w", encoding="utf-8") as fh:
            json.dump(items, fh, indent=1, ensure_ascii=False)
        print("%-12s %5d reference pages" % (name, len(out)))


if __name__ == "__main__":
    main()
