"""H-L2-1 parser fix check: decode every non-expando AttributeSet block of every object in all sample folders with
the old attrs.py (HEAD before the fix, path as argument) and the new one. Reports:
  - values of NON-structured attributes that differ (must be 0: strings, blobs, XML untouched)
  - Structure / Array values: count per type, old == new[1:] (< 128 B, only the length head byte added),
    changed (>= 128 B, old was cut), decode_structure result (dict / list) or bytes
  - objects with decode problems old / new
Usage: python struct_scan.py <attrs_old.py>"""
import importlib.util
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import attrs as new  # noqa: E402
from openpedata.project import Project  # noqa: E402

spec = importlib.util.spec_from_file_location("attrs_old", sys.argv[1])
old = importlib.util.module_from_spec(spec)
spec.loader.exec_module(old)

stats = {}
other_diff = []
prob = {"old": 0, "new": 0}
examples = {}
for n in sorted(os.listdir(os.path.join(ROOT, "samples"))):
    p = os.path.join(ROOT, "samples", n)
    if not os.path.isfile(os.path.join(p, "System", "PEData.plf")):
        continue
    P = Project(p, verify=False)
    mm = P.mm
    for k, o in P.objects.items():
        po = pn = False
        for i in range(o.n_sets if o.layout_ok else 0):
            if i not in o.blocks or o.is_expando(i):
                continue
            a, e = o.blocks[i]
            blk = o.data[a:e]
            vo, qo = old.decode_set(mm, o.rec.type_id, o.set_names[i], blk)
            vn, qn = new.decode_set(mm, o.rec.type_id, o.set_names[i], blk)
            po |= bool(qo)
            pn |= bool(qn)
            typs = {nm: ty for _, nm, ty, _ in mm.stored_attributes(o.rec.type_id, o.set_names[i])}
            for nm in set(vo) | set(vn):
                ty = typs.get(nm)
                if ty and new.is_structured(mm, ty):
                    s = stats.setdefault(ty, {"n": 0, "same": 0, "longer": 0, "other": 0, "decoded": 0, "null": 0})
                    x, y = vo.get(nm), vn.get(nm)
                    s["n"] += 1
                    if y is None:
                        s["null"] += 1
                        continue
                    if isinstance(x, bytes) and x == y[1:]:
                        s["same"] += 1
                    elif len(y) >= 128:
                        s["longer"] += 1
                        examples.setdefault(ty, (n, k, nm, len(x) if isinstance(x, bytes) else x, len(y)))
                    else:
                        s["other"] += 1
                        examples.setdefault("other " + ty, (n, k, nm, x, y))
                    d = new.decode_structure(mm, ty, y)
                    if isinstance(d, (dict, list)) and not any(
                            isinstance(v, bytes) and new.is_structured(mm, getattr(mm.resolve(ty), "item_type", None) or "")
                            for v in (d if isinstance(d, list) else [])):
                        s["decoded"] += 1
                    else:
                        examples.setdefault("undecoded " + ty, (n, k, nm, y.hex()[:120]))
                elif vo.get(nm) != vn.get(nm):
                    other_diff.append((n, k, o.set_names[i], nm))
        prob["old"] += po
        prob["new"] += pn
print("non-structured attribute values that differ: %d %s" % (len(other_diff), other_diff[:10]))
print("objects with set decode problems (non-expando sets): old %d, new %d" % (prob["old"], prob["new"]))
print("%-80s %7s %7s %7s %7s %7s %7s" % ("structured type", "values", "null", "same", ">=128", "other", "decoded"))
for ty, s in sorted(stats.items()):
    print("%-80s %7d %7d %7d %7d %7d %7d" % (ty[-80:], s["n"], s["null"], s["same"], s["longer"], s["other"], s["decoded"]))
print("examples:")
for k, v in examples.items():
    print("  %s: %s" % (k, v))
