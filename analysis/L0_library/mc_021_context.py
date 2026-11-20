"""L0 c): how the master copy folder hangs on the project library; size of copied vs original objects.
Usage: python mc_021_context.py <sample_dir>"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "parser"))
sys.stdout.reconfigure(encoding="utf-8")
from openpedata.project import Project
p = Project(sys.argv[1], verify=False)
def show(k, depth=0, seen=set()):
    rn = lambda l: (p.mm.relations[l.relation].name if l.relation in p.mm.relations else "slot%s" % l.slot)
    print("  " * depth + "%s %s %r" % (k, p.type_name(k), p.name(k)))
    for l in p.objects[k].links:
        print("  " * depth + "    %-30s -> %s %s %r" % (rn(l), l.key, p.type_name(l.key) if l.key in p.objects else "?", p.name(l.key) if l.key in p.objects else ""))
lib = p.of_type("LibraryData")
for k in lib: show(k)
for k in p.of_type("FolderData"):
    if "Library" in (p.name(k) or "") or "Library" in str(p.attr(k, "ICoreAttributes", "Subtype")):
        show(k)
for a, b in (("101c:0010:101b", "101c:0010:813"), ("100e:0010:1019", "100e:0010:808"), ("1001:0022:101a", "1001:0022:c10"), ("160c:0022:1020", "160c:0022:c12")):
    print("size copy %s=%d original %s=%d" % (a, len(p.latest[a].data), b, len(p.latest[b].data)))
# attribute differences copy vs original
for a, b in (("1001:0022:101a", "1001:0022:c10"), ("100e:0010:1019", "100e:0010:808")):
    A, B = p.attributes(a)[0], p.attributes(b)[0]
    print("attr diff", a, b)
    for s in sorted(set(A) | set(B)):
        for n in sorted(set(A.get(s, {})) | set(B.get(s, {}))):
            va, vb = A.get(s, {}).get(n), B.get(s, {}).get(n)
            if va != vb:
                print("   %s.%s: copy=%r orig=%r" % (s, n, str(va)[:120], str(vb)[:120]))
