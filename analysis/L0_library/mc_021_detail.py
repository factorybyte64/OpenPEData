"""L0 c): details of the 021 master copy: SharedRawData, CoreObjectData, copied objects vs. originals, store records.
Usage: python mc_021_detail.py <sample_dir>"""
import os, sys, struct
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "parser"))
sys.stdout.reconfigure(encoding="utf-8")
from openpedata.project import Project
p = Project(sys.argv[1], verify=False)
mc = p.of_type("LibraryMasterCopyData")[0]
a = p.attributes(mc)[0]["ICoreLibMasterCopyAttributes"]
print("== SharedRawData (full)\n" + a["SharedRawData"])
for n in ("BinaryData", "CoreObjectData", "NonCoreObjectData"):
    print("== %s raw (%d): %s" % (n, len(a[n]), a[n].hex(" ")))
print("\n== objects whose Environment is the master copy (the copy content), with GUID; same-named originals")
env = []
for k, o in p.objects.items():
    for l in o.links:
        r = p.mm.relations.get(l.relation) if l.relation else None
        if r and r.name == "Environment" and l.key == mc:
            env.append(k)
for k in sorted(env):
    o = p.objects[k]
    nm = p.name(k)
    twins = [k2 for k2, o2 in p.objects.items() if k2 != k and p.objects[k2].rec.type_id == o.rec.type_id and nm and p.name(k2) == nm]
    print("%-22s %-32s %-12r guid=%s originals=%s" % (k, p.type_name(k), nm, o.guid.hex(), [(t, p.objects[t].guid.hex(), [str(p.name(l.key)) for l in p.objects[t].links if (p.mm.relations.get(l.relation) and p.mm.relations[l.relation].name == "Environment")]) for t in twins]))
    for l in o.links:
        r = p.mm.relations.get(l.relation) if l.relation else None
        rn = r.name if r else "slot%s" % l.slot
        print("      %-28s -> %s %s %r" % (rn, l.key, p.type_name(l.key) if l.key in p.objects else "?", p.name(l.key) if l.key in p.objects else ""))
print("\n== MC parent folder and library")
for k in (p.targets(mc, "AggregatingFolder") + p.targets(mc, "Environment")):
    print(k, p.type_name(k), p.name(k), p.attributes(k)[0])
r = p.latest["0014:0007:1400"]
print("\n== store record 0014:0007:1400 (%d bytes)\n%s" % (len(r.data), r.data.hex(" ")))
