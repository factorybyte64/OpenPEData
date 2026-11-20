"""L0 c): master copy in 021_mastercopy vs predecessor 004_fb. Read-only use of parser/openpedata.
Usage: python mc_021.py <sample_dir> <predecessor_dir> <outdir>"""
import os, sys, struct, zlib, binascii
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "parser"))
sys.stdout.reconfigure(encoding="utf-8")
from openpedata.project import Project

cur, prev, outdir = sys.argv[1:4]
p, q = Project(cur, verify=False), Project(prev, verify=False)

def rel(l):
    r = p.mm.relations.get(l.relation) if l.relation else None
    return r.name if r else ("slot%d" % l.slot if l.relation is None else "?%08x" % l.relation)

def desc(proj, k):
    return "%s %s '%s'" % (k, proj.type_name(k) if k in proj.objects else "?", proj.name(k) if k in proj.objects else "")

print("== Records: new/changed/deleted compared to predecessor (latest versions)")
new = [k for k in p.latest if k not in q.latest]
chg = [k for k in p.latest if k in q.latest and p.latest[k].data != q.latest[k].data]
gone = [k for k in q.latest if k not in p.latest]
for tag, ks in (("NEW", new), ("CHANGED", chg), ("GONE", gone)):
    for k in sorted(ks):
        r = p.latest.get(k) or q.latest.get(k)
        tn = p.mm.short_name(r.type_id) if r.type_id in p.mm.types else ("store" if r.is_store else "?")
        nm = p.name(k) if k in p.objects else ""
        print("%-9s %-24s %-40s len=%-6d %s" % (tag, k, tn, len(r.data), nm))
print("tombstones in cur:", len(p.deleted), "prev:", len(q.deleted))

mcs = p.of_type("LibraryMasterCopyData")
print("\n== LibraryMasterCopyData:", mcs)
for k in mcs:
    o = p.objects[k]
    print("GUID", o.guid.hex(), "slots", o.slot_count, "flags", o.flags16, o.flags20, "sets", [o.set_name(i) for i in range(o.n_sets)])
    print("slot errors:", o.errors)
    a, prob = p.attributes(k)
    for sn, vals in a.items():
        for n, v in vals.items():
            s = repr(v)
            print("  %s.%s = %s%s" % (sn, n, s[:300], " ...(%d)" % len(s) if len(s) > 300 else ""))
    print("  problems:", prob)
    print("  outgoing links:")
    for l in o.links:
        print("    %-24s -> %s" % (rel(l), desc(p, l.key) if l.key in p.objects else l.key))
    print("  incoming links:")
    for k2, o2 in p.objects.items():
        for l in o2.links:
            if l.key == k:
                print("    %s  via %s" % (desc(p, k2), rel(l)))
    # raw blocks of the master copy sets
    for i in range(o.n_sets):
        pl = o.payload(i)
        if pl:
            fn = os.path.join(outdir, "021_mc_slot%d_%s.bin" % (i, o.set_name(i)))
            open(fn, "wb").write(pl)
            print("  slot %d %s payload %d bytes -> %s" % (i, o.set_name(i), len(pl), os.path.basename(fn)))
