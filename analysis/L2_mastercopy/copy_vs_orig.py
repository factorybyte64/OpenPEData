"""Attribute comparison of every master copy content copy with its original (same type and name outside the
master copy): which decoded attributes differ, plus GUID equality. Usage: python copy_vs_orig.py <sample>"""
import os, sys
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.project import Project

p = Project(os.path.join(ROOT, "samples", sys.argv[1]) if not os.path.exists(sys.argv[1]) else sys.argv[1], verify=False)
mcs = [k for k, o in p.objects.items() if p.mm.short_name(o.rec.type_id) == "LibraryMasterCopyData"]
for mc in mcs:
    print("MC", mc, p.name(mc))
    copies = [k for k in p.objects if any(l.key == mc for l in p.links(k, "Environment"))]
    for c in sorted(copies):
        nm = p.name(c)
        orig = [k for k in p.objects if k != c and k not in copies and p.objects[k].rec.type_id == p.objects[c].rec.type_id
                and nm is not None and p.name(k) == nm and not any(l.key in mcs for l in p.links(k, "Environment"))]
        if not orig:
            continue
        o = orig[0]
        A, B = p.attributes(o)[0], p.attributes(c)[0]
        diffs, missing = [], []
        for s in sorted(set(A) | set(B)):
            for n in sorted(set(A.get(s, {})) | set(B.get(s, {}))):
                a, b = A.get(s, {}).get(n, "<absent>"), B.get(s, {}).get(n, "<absent>")
                if a != b:
                    if b == "<absent>":
                        missing.append("%s.%s" % (s, n))
                    else:
                        diffs.append("%s.%s: %s -> %s" % (s, n, str(a)[:50], str(b)[:50]))
        print("  %s %s %r orig %s size %d->%d guidSame=%s" % (c, p.type_name(c), nm, o, len(p.objects[o].data),
              len(p.objects[c].data), p.objects[c].guid == p.objects[o].guid))
        for d in diffs:
            print("      ~", d)
        if missing:
            print("      missing in copy (%d): %s" % (len(missing), ", ".join(missing[:12]) + (" ..." if len(missing) > 12 else "")))
