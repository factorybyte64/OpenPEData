"""FB_MixedStl (C4): SimaticML of FB_Mixed with an STL network from FB_BigStl inserted after every third network, so that one
block holds LAD, FBD, SCL and STL networks (SIMATIC SD has no STL networks). Both blocks come from tools/gen_stress.py and
share the interface. Text splice (the Openness import rejects re-serialized namespace prefixes); all ID attributes are
renumbered in document order. Usage: python tools/make_mixed_stl.py <FB_Mixed.xml> <FB_BigStl.xml> <out.xml>"""
import re
import sys

mixed, stl, out = sys.argv[1:4]
m = open(mixed, encoding="utf-8-sig").read()
s = open(stl, encoding="utf-8-sig").read()
CU = re.compile(r"[ \t]*<SW\.Blocks\.CompileUnit .*?</SW\.Blocks\.CompileUnit>\r?\n", re.S)
stl_cus = CU.findall(s)
pieces, last, k = [], 0, 0
for n, mo in enumerate(CU.finditer(m)):
    pieces.append(m[last:mo.end()])
    last = mo.end()
    if n % 3 == 2:
        pieces.append(stl_cus[k % len(stl_cus)])
        k += 1
pieces.append(m[last:])
t = "".join(pieces)
t = t.replace("<Name>FB_Mixed</Name>", "<Name>FB_MixedStl</Name>", 1)
t = re.sub(r"<AutoNumber>true</AutoNumber>", "<AutoNumber>false</AutoNumber>", t, count=1)
t = re.sub(r"<Number>\d+</Number>", "<Number>900</Number>", t, count=1)
cnt = iter(range(1 << 30))
t = re.sub(r' ID="[0-9A-F]+"', lambda mo: ' ID="%X"' % next(cnt), t)
open(out, "w", encoding="utf-8-sig", newline="").write(t)
print("STL networks inserted:", k, "compile units:", len(CU.findall(t)))
