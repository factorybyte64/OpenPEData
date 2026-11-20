"""SCL coverage: renders every SCL network of a sample with the parser and compares it with the SD export of the
ground truth (samples/<sample>_truth/sd/<block>.s7dcl). Prints the differing lines and the unknown tokens.
Usage: python tools/scl_cov.py <sample> [block ...]"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.model import Model  # noqa: E402


def sd_body(path):
    lines = open(path, encoding="utf-8-sig").read().splitlines()
    out, nets, cur = [], [], None
    for ln in lines:
        if ln == "    NETWORK":
            cur = []
        elif ln == "    END_NETWORK":
            nets.append(cur)
            cur = None
        elif cur is not None:
            cur.append(ln[8:].rstrip() if ln.startswith("        ") else ln.rstrip())
    return nets


def main():
    sample, only = sys.argv[1], set(sys.argv[2:])
    m = Model(os.path.join(ROOT, "samples", sample))
    d = m.to_dict()["project"]
    bad = 0
    for dev in d["devices"]:
        for plc in dev["plcs"]:
            for b in plc["blocks"]:
                if only and b["name"] not in only:
                    continue
                f = os.path.join(ROOT, "samples", sample + "_truth", "sd", b["name"] + ".s7dcl")
                if "networks" not in b or not os.path.isfile(f):
                    continue
                nets = [n for n in b["networks"] if n["language"] == "SCL"]
                exp = sd_body(f)
                for n, e in zip(nets, exp):
                    got = [x.rstrip() for x in n.get("scl", "").splitlines()]
                    unk = n.get("unknownTokens")
                    ok = got == e and not unk
                    print("%-14s %s  lines %d/%d  unknown %s" % (b["name"], "OK  " if ok else "DIFF", len(got), len(e), unk or ""))
                    if got != e:
                        bad += 1
                        for i in range(max(len(got), len(e))):
                            g = got[i] if i < len(got) else "<none>"
                            x = e[i] if i < len(e) else "<none>"
                            if g != x:
                                print("   %3d  got: %r\n        exp: %r" % (i + 1, g, x))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
