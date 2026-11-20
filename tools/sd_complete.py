"""Completes the pin list of an SD instruction call from TIA's 'Pin X connection is missing' messages.
python tools/sd_complete.py <LAD|FBD> "<Name>" "<pin := operand>" ...   prints the final accepted SD rung."""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_lad  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402


def complete(c, lang, name, pins, pre=None, outdir=None, tries=40):
    outdir = outdir or os.path.join(os.path.dirname(os.path.abspath(__file__)), "sources", "dbg")
    os.makedirs(outdir, exist_ok=True)
    pins = list(pins)
    for _ in range(tries):
        body = "{R}" + gen_lad.NL + ((pre + gen_lad.NL) if pre else "") + name + "(" + ", ".join(pins) + ")" + gen_lad.NL + "END_RUNG"
        fb, src, res = gen_lad.source(lang, "CMP", "complete", [body])
        path = os.path.join(outdir, fb + ".s7dcl")
        open(path, "w", encoding="utf-8").write(src)
        open(path[:-6] + ".s7res", "w", encoding="utf-8").write(res)
        e, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=path, waitMs="45000")
        if e:
            m = gen_lad.condense(r if isinstance(r, str) else json.dumps(r))
            mm = re.search(r"Pin '(\w+)' connection is missing", m)
            if mm and not any(p.split(":=")[0].split("=>")[0].strip().lower() == mm.group(1).lower() for p in pins):
                pins.append("%s := " % mm.group(1))
                continue
            return False, pins, m[:300]
        msgs = [x["description"] for x in (r.get("compile") or {}).get("messages", []) if x.get("state") == "Error"]
        c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
        return not msgs, pins, "; ".join(msgs)[:300]
    return False, pins, "too many tries"


if __name__ == "__main__":
    c = TiaClient(default_pid())
    ok, pins, msg = complete(c, sys.argv[1].upper(), sys.argv[2], sys.argv[3:])
    print(ok, pins, msg)
