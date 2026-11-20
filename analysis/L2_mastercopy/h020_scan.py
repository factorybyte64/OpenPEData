"""H-020 scan over all sample folders: types whose AttributeSet slot order differs between metamodel declaration
order and alphabetical order of the qualified set name; for every object of such a type, which contested slots are
filled and whether decoding works under each order (decode problems / exceptions per order)."""
import os, sys, hashlib
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.project import Project
from openpedata import attrs as ac

def try_decode(p, o, names, i):
    a, e = o.blocks[i]
    n = names[i]
    t = p.mm.by_name.get(n)
    try:
        if t and t.expando == "simple":
            v, pr = ac.decode_simple_expando(o.data[a:e])
        elif t and t.expando:
            v, pr = ac.decode_expando(o.data[a:e], p.expando_dicts)
        else:
            v, pr = ac.decode_set(p.mm, o.rec.type_id, n, o.data[a:e])
        return "ok" if not pr else "problems:%s" % (pr if len(str(pr)) < 120 else str(pr)[:120])
    except Exception as ex:
        return "EXC %s" % ex

seen_mm = {}
samples = sorted(d for d in os.listdir(os.path.join(ROOT, "samples"))
                 if os.path.isdir(os.path.join(ROOT, "samples", d, "System")))
for s in samples:
    try:
        p = Project(os.path.join(ROOT, "samples", s), verify=False)
    except Exception as ex:
        print(s, "load error", ex); continue
    h = hashlib.sha1(p.mm.xml if isinstance(p.mm.xml, bytes) else p.mm.xml.encode()).hexdigest()[:10]
    if h not in seen_mm:
        diff = []
        for tid, t in p.mm.types.items():
            sets = p.mm.attribute_sets(tid)
            if not sets:
                continue
            if p.mm.attribute_sets_ordered(tid) != sorted(sets):
                diff.append(t.full_name)
        seen_mm[h] = diff
        print("metamodel %s (first: %s, %d types): %d types with decl != alpha: %s" % (h, s, len(p.mm.types), len(diff), diff))
    difftypes = set(seen_mm[h])
    for k, o in p.objects.items():
        fn = p.mm.types[o.rec.type_id].full_name
        if fn not in difftypes or not o.layout_ok:
            continue
        decl = p.mm.attribute_sets_ordered(o.rec.type_id)
        alpha = sorted(p.mm.attribute_sets(o.rec.type_id))
        contested = [i for i in range(len(decl)) if decl[i] != alpha[i]]
        filled = [i for i in contested if i in o.blocks]
        if filled:
            print("  %s %s %s filled contested slots %s" % (s, k, p.name(k), filled))
            for i in filled:
                print("     slot %d decl=%s -> %s | alpha=%s -> %s" % (i, decl[i], try_decode(p, o, decl, i), alpha[i], try_decode(p, o, alpha, i)))
