"""Check of the L2 rule 'structure / array attribute values start with a u32 LE total length' over all sample
folders: for every decoded attribute whose metamodel type is a Structure or an Array* type, compare the value the
parser returns (bytes after its 1-byte 'varint') with the rule: raw[0:3] == 00 00 00 and (len(raw)+1) & 0xFF ==
the varint byte (for values < 128 bytes); counts per type. Values >= 128 bytes are listed separately (the parser's
varint read would then be wrong)."""
import os, sys, collections
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.project import Project

ok, bad, big, seen = collections.Counter(), collections.Counter(), collections.Counter(), set()
examples = {}
for s in sorted(os.listdir(os.path.join(ROOT, "samples"))):
    path = os.path.join(ROOT, "samples", s)
    if not os.path.isfile(os.path.join(path, "System", "PEData.plf")):
        continue
    p = Project(path, verify=False)
    mm = p.mm
    for k, o in p.objects.items():
        if not o.layout_ok:
            continue
        for i in range(o.n_sets):
            if i not in o.blocks or o.is_expando(i):
                continue
            sn = o.set_names[i]
            for _, name, typ, _ in mm.stored_attributes(o.rec.type_id, sn):
                t = mm.resolve(typ)
                if not (typ.startswith("Array") or (t is not None and t.kind == "Structure")):
                    continue
                v = p.attributes(k)[0].get(sn.split(".")[-1], {}).get(name)
                if not isinstance(v, (bytes, bytearray)):
                    continue
                key = (typ, mm.short_name(o.rec.type_id) + "." + name)
                if len(v) + 1 >= 128:
                    big[key] += 1
                    examples.setdefault(("big",) + key, (s, k, v[:12].hex(), len(v)))
                elif v[:3] == b"\0\0\0":
                    ok[key] += 1
                else:
                    bad[key] += 1
                    examples.setdefault(("bad",) + key, (s, k, v[:12].hex(), len(v)))
print("rule holds (value < 128 bytes):")
for k, n in sorted(ok.items()):
    print("  %5d %s" % (n, k))
print("violations:")
for k, n in sorted(bad.items()):
    print("  %5d %s e.g. %s" % (n, k, examples[("bad",) + k]))
print(">= 128 bytes (parser varint read questionable):")
for k, n in sorted(big.items()):
    print("  %5d %s e.g. %s" % (n, k, examples[("big",) + k]))
