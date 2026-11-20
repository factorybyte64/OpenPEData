"""Code blocks of one sample against the TIA truth in samples/<sample>_truth/<device>/blocks (analysis tool, uses the Python
parser and the comparison functions of parser/tests/test_samples.py):
  - <block>.xml (SimaticML): interface sections and members, network count, language, title, comment per network,
    LAD/FBD net list (parts, template values, operands, wires);
  - <block>.s7dcl (SIMATIC SD): SCL network bodies;
  - <block>.awl (STL source): STL network bodies; for blocks without own .awl the STL networks are looked up by title in
    the .awl files of the sample (FB_MixedStl takes its STL networks from FB_BigStl).
Usage: python tools/check_code_truth.py <sample> [block-prefix]"""
import glob
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
sys.path.insert(0, os.path.join(ROOT, "parser", "tests"))
import test_samples as T  # noqa: E402
from openpedata.model import Model  # noqa: E402


def awl_networks(path):
    lines = open(path, encoding="utf-8-sig").read().replace("\r\n", "\n").split("\n")
    out, i = [], next(n for n, x in enumerate(lines) if x.strip() == "BEGIN") + 1
    while i < len(lines) and lines[i].strip() == "NETWORK":
        i += 1
        title = lines[i][len("TITLE ="):].strip() if lines[i].startswith("TITLE =") else ""
        i += 1 if lines[i].startswith("TITLE =") else 0
        com = []
        while lines[i].startswith("//"):
            com.append(lines[i][2:])
            i += 1
        body = []
        while lines[i].strip() != "NETWORK" and not lines[i].startswith("END_"):
            body.append(lines[i])
            i += 1
        while body and body[-1] == "":
            body.pop()
        out.append((title, "\n".join(com), "\n".join(body)))
    return out


def main():
    sample = sys.argv[1]
    pre = sys.argv[2] if len(sys.argv) > 2 else ""
    c, n = T.Checker(), 0
    d = Model(os.path.join(ROOT, "samples", sample + ".zap21")).to_dict(keys=True)["project"]
    tdir = os.path.join(ROOT, "samples", sample + "_truth")
    stl_by_title = {}
    for f in glob.glob(os.path.join(tdir, "*", "blocks", "*.awl")):
        for title, com, body in awl_networks(f):
            stl_by_title.setdefault(title, body)
    for dev in d["devices"]:
        for plc in dev["plcs"]:
            bdir = os.path.join(tdir, plc["name"], "blocks")
            for b in plc["blocks"]:
                xml = os.path.join(bdir, b["name"] + ".xml")
                if not b["name"].startswith(pre) or not os.path.isfile(xml) or b["type"] not in ("FB", "FC", "OB"):
                    continue
                nets = b.get("networks", [])
                got = {s["section"]: s["members"] for s in b.get("interface", [])}
                exp = {k: v for k, v in T.simaticml_sections(xml).items() if v}
                c.eq("%s sections" % b["name"], list(got), list(exp))
                for sec in exp:
                    if sec in got:
                        T.check_members(c, "%s.%s" % (b["name"], sec), got[sec], exp[sec])
                        n += len(T.flat(exp[sec]))
                own = os.path.join(bdir, b["name"] + ".awl")
                own = awl_networks(own) if os.path.isfile(own) else None
                xn = T.simaticml_networks(xml)
                c.eq("%s network count" % b["name"], len(nets), len(xn))
                for pn, (lang, title, comment, nl) in zip(nets, xn):
                    w = "%s network %d" % (b["name"], pn["index"])
                    c.eq(w + " language", pn["language"], lang)
                    c.eq(w + " title", pn["title"], title)
                    c.eq(w + " comment", pn["comment"], comment)
                    for k in ("error", "unknownTokens", "unhandled", "unknownIdentKinds"):
                        c.eq(w + " " + k, pn.get(k) or [], [])
                    n += 4
                    if nl is not None:
                        c.eq(w + " net list", T.parsed_netlist(pn), nl)
                        n += len(nl[0])
                    if pn["language"] == "STL":
                        t = (title or {}).get("en-US", "")
                        c.eq(w + " STL text", pn.get("stl"), own[pn["index"] - 1][2] if own else stl_by_title.get(t))
                        n += 1
                sd = os.path.join(bdir, b["name"] + ".s7dcl")
                if os.path.isfile(sd):
                    scl = [x for x in nets if x["language"] == "SCL"]
                    sdn = [x for x, pn in zip(T.sd_networks(sd), nets) if pn["language"] == "SCL"]
                    c.eq("%s SCL network count (SD)" % b["name"], len(scl), len(sdn))
                    for pn, body in zip(scl, sdn):
                        while body and body[0] == "":  # the parser drops empty lines at the start and end of an SCL network
                            body = body[1:]
                        while body and body[-1] == "":
                            body = body[:-1]
                        c.eq("%s network %d SCL text (SD)" % (b["name"], pn["index"]), [x.rstrip() for x in pn["scl"].splitlines()], body)
                        n += 1
    print("%s: %d checks, %d failures" % (sample, n, len(c.fail)))
    for f in c.fail[:40]:
        print("  " + str(f)[:400])
    return 1 if c.fail else 0


if __name__ == "__main__":
    sys.exit(main())
