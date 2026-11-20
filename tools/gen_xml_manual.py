"""LAD/FBD instructions of the extended catalog whose SimaticML network is hand-written (compare boxes T_Comp / S_Comp: power flow at EN,
data pins IN1/IN2, Boolean OUT wired straight to the variable, no DisabledENO).
python tools/gen_xml_manual.py <LAD|FBD> [001 016] [--sample 039_fw41]
Results: docs/catalog/extended_<lang>_results.json (key ex<NNN>, "via": "xml")."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_xml  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

# catalog number -> (part, element type, template name of the type, relation)
COMPARE = {"001": ("T_Comp", "DTL", "date_type", "EQ"), "016": ("S_Comp", "String", "src_type", "EQ")}


def network(part, typ, tvk, rel, lang="LAD"):
    net = ('<FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5"><Parts>'
            '<Access Scope="LocalVariable" UId="21"><Symbol><Component Name="p0" /></Symbol></Access>'
            '<Access Scope="LocalVariable" UId="22"><Symbol><Component Name="p1" /></Symbol></Access>'
            '<Access Scope="LocalVariable" UId="23"><Symbol><Component Name="q" /></Symbol></Access>'
            '<Part Name="%s" UId="24"><TemplateValue Name="%s" Type="Type">%s</TemplateValue><TemplateValue Name="relation" Type="Operation">%s</TemplateValue></Part>'
            '</Parts><Wires>'
            '<Wire UId="30"><Powerrail /><NameCon UId="24" Name="en" /></Wire>'
            '<Wire UId="31"><IdentCon UId="21" /><NameCon UId="24" Name="IN1" /></Wire>'
            '<Wire UId="32"><IdentCon UId="22" /><NameCon UId="24" Name="IN2" /></Wire>'
            '<Wire UId="33"><NameCon UId="24" Name="OUT" /><IdentCon UId="23" /></Wire>'
            '</Wires></FlgNet>') % (part, tvk, typ, rel)
    return net if lang == "LAD" else net.replace("<Powerrail />", '<OpenCon UId="27" />')  # FBD: EN open


def main(argv):
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d  # noqa: E731
    lang = argv[0].upper()
    sel = [a for a in argv[1:] if a.isdigit()]
    c = TiaClient(default_pid())
    tpl = open(os.path.join(HERE, "sources", "FX_T.xml"), encoding="utf-8-sig").read()
    rp = os.path.join(HERE, "..", "docs", "catalog", "extended_%s_results.json" % lang.lower())
    res = json.load(open(rp))
    cat = json.load(open(os.path.join(HERE, "..", "docs", "catalog", "extended.json"), encoding="utf-8"))
    out = os.path.join(HERE, "sources", "xml_extended_" + lang.lower())
    os.makedirs(out, exist_ok=True)
    for num, (part, typ, tvk, rel) in sorted(COMPARE.items()):
        if sel and num not in sel:
            continue
        key = "ex" + num
        name = "FX_%s_ex%s" % (lang.title(), num)
        gen_xml.EXTRA.clear()
        gen_xml.EXTRA["p0"] = typ
        gen_xml.EXTRA["p1"] = typ
        xml_ = gen_xml.build_block(tpl, name, lang, network(part, typ, tvk, rel, lang))
        fp = os.path.join(out, name + ".xml")
        open(fp, "w", encoding="utf-8").write(xml_)
        e, r = c.json("TiaPlcSoftware", operation="import", objectKind="block", sourcePath=fp, importMode="Override", waitMs="45000")
        msg = (r if isinstance(r, str) else r.get("message", "")) if e else ""
        ok = not e
        if ok:
            comp = c.json("TiaPlcSoftware", operation="compile", objectKind="block", waitMs="50000")[1]
            errs = [m["description"] for m in (comp.get("messages") if isinstance(comp, dict) else []) if m.get("state") == "Error" and name in m.get("path", "")]
            ok = not errs
            msg = "; ".join(errs)
            if not ok:
                c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=name)
        title = cat[int(num) - 1]["title"].split(" (S7")[0]
        if ok:
            res[key] = {"title": title, "ok": True, "via": "xml", "block": name, "xml": {"name": part, "templates": {tvk: typ, "relation": rel}}, "sample": opt("--sample", "039_fw41")}
        else:
            res.setdefault(key, {"title": title, "ok": False})["xmlLast"] = msg[-400:]
        print(key, title[:30], "ok" if ok else "FAIL " + msg[-200:], flush=True)
        json.dump(res, open(rp, "w"), indent=1, sort_keys=True)


if __name__ == "__main__":
    main(sys.argv[1:])
