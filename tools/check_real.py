"""Runs the sample checks of parser/tests/test_samples.py against ONE project with Openness truth (<dir>_truth/simaticml, /sd).
python tools/check_real.py <project folder> <truth dir>   (scratch root with junctions is built under TMP/realroot; not committed)"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
sys.path.insert(0, os.path.join(ROOT, "parser", "tests"))
sys.path.insert(0, os.path.join(ROOT, "parser"))
import test_samples as T  # noqa: E402


def junction(link, target):
    if not os.path.exists(link):
        subprocess.run(["cmd", "/c", "mklink", "/J", link, target], check=True, capture_output=True)


def interface_checks(root, src):
    """Interface sections of FB/FC/OB/InstanceDB against the SimaticML export, data block members against the SD export."""
    import json
    sdir = os.path.join(root, "samples", "REAL_truth")
    c, n = T.Checker(), 0
    d = T.Model(src).to_dict()["project"]
    for dev in d["devices"]:
        for plc in dev["plcs"]:
            for b in plc["blocks"]:
                xml = os.path.join(sdir, "simaticml", b["name"] + ".xml")
                sd = os.path.join(sdir, "sd", b["name"] + ".s7dcl")
                if b.get("interface") is not None and os.path.isfile(xml) and b["type"] != "SDT":
                    got = {s["section"]: s["members"] for s in b["interface"]}
                    exp = {k: v for k, v in T.simaticml_sections(xml).items() if v}
                    c.eq("%s sections" % b["name"], list(got), list(exp))
                    for sec in exp:
                        if sec in got:
                            T.check_members(c, "%s.%s" % (b["name"], sec), got[sec], exp[sec])
                            n += len(T.flat(exp[sec]))
                if b["type"] in ("GlobalDB", "InstanceDB") and os.path.isfile(sd):
                    res = os.path.splitext(sd)[0] + ".s7res"
                    j = {"source": open(sd, encoding="utf-8-sig").read(), "resources": open(res, encoding="utf-8-sig").read() if os.path.isfile(res) else ""}
                    jp = os.path.join(root, "TMP_get.json")
                    json.dump(j, open(jp, "w", encoding="utf-8"))
                    members, inst, complete = T.sd_truth(jp)
                    if b["type"] == "GlobalDB" and complete:
                        T.check_members(c, "%s members" % b["name"], b["members"], members)
                        n += len(T.flat(members))
    return c, n


def main(argv):
    proj, truth = os.path.abspath(argv[0]), os.path.abspath(argv[1])
    root = os.path.join(ROOT, "TMP", "realroot")
    sd = os.path.join(root, "samples")
    os.makedirs(sd, exist_ok=True)
    junction(os.path.join(sd, "REAL"), proj)
    junction(os.path.join(sd, "REAL_truth"), truth)
    junction(os.path.join(sd, "002_cpu_truth"), os.path.join(ROOT, "samples", "002_cpu_truth"))
    T.ROOT = root
    src = os.path.join(sd, "REAL")
    res = []
    for name, fn in (("interfaces", lambda: interface_checks(root, src)),
                     ("flg", lambda: T.check_flg_xml("REAL", src, {"flg": {}, "flgattr": {}, "ident": {}, "identsub": {}})),
                     ("scl", lambda: T.check_scl_sd("REAL", src, {})), ("verify", lambda: T.check_verify("REAL", src)),
                     ("stores", lambda: T.check_stores("REAL", src))):
        try:
            r = fn()
        except Exception as e:  # noqa: BLE001
            import traceback
            traceback.print_exc()
            res.append((name, "EXC", [repr(e)]))
            continue
        c = r[0] if isinstance(r, tuple) else r
        if c is None:
            res.append((name, "no truth", []))
            continue
        res.append((name, c.n, c.fail[:30]))
    for name, n, fail in res:
        print("%-10s checks=%s failures=%d" % (name, n, len(fail)))
        for f in fail:
            print("    ", str(f)[:300])


if __name__ == "__main__":
    main(sys.argv[1:])
