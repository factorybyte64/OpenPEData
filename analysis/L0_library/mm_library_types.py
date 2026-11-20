"""L0 b): list metamodel types related to library / master copy / types / versions / faceplates / HMI Unified.
Usage: python mm_library_types.py <out.md> <sample>...   (read-only use of parser/openpedata)"""
import os, re, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "parser"))
from openpedata.project import Project
from openpedata.metamodel import NS
import xml.etree.ElementTree as ET
sys.stdout.reconfigure(encoding="utf-8")

def type_packages(mm):
    """type id -> name of the Package element declaring it (parser mm.packages collapses packages by id>>16)."""
    out = {}
    for pkg in ET.fromstring(mm.xml).iter(NS + "Package"):
        for ns in pkg.iter(NS + "Namespace"):
            for el in ns:
                if el.get("id"):
                    out[int(el.get("id"), 16)] = pkg.get("name")
    return out

PAT = re.compile(r"Librar|MasterCopy|Faceplate|ReleaseManag|Version|Unified|Hmi|TypeInstance|LibraryType|Typ(e)?Folder", re.I)

def main(out, samples):
    lines = []
    sets = {}
    for s in samples:
        p = Project(s, verify=False)
        mm = p.mm
        pk_hits = {pid: n for pid, n in mm.packages.items() if PAT.search(n or "")}
        types = [t for t in mm.types.values() if PAT.search(t.full_name)]
        sets[s] = (mm, pk_hits, types, p)
    # metamodel identical across samples?
    xs = {s: v[0].xml for s, v in sets.items()}
    base = samples[0]
    lines.append("# L0 b) Metamodel: library/type/HMI-related types\n")
    lines.append("Samples: " + ", ".join(os.path.basename(s) for s in samples) + "\n")
    for s in samples:
        mm = sets[s][0]
        lines.append("- %s: %d types, %d packages, XML %d bytes, identical to %s: %s" % (
            os.path.basename(s), len(mm.types), len(mm.packages), len(mm.xml), os.path.basename(base), xs[s] == xs[base]))
    mm, pk_hits, types, p = sets[samples[-1]]
    tpk = type_packages(mm)
    lines.append("\n## All packages (%d)\n" % len(mm.packages))
    lines.append(", ".join("%04x=%s" % (k, v) for k, v in sorted(mm.packages.items())))
    lines.append("\n## Packages with a match in the name\n")
    for k, v in sorted(pk_hits.items()):
        lines.append("- %04x %s" % (k, v))
    lines.append("\n## Types with a match (%d)\n" % len(types))
    lines.append("| Type ID | Kind | Package | Full name | Bases | AttributeSets (impl.) | Relations |")
    lines.append("|---|---|---|---|---|---|---|")
    for t in sorted(types, key=lambda t: t.id):
        pk = tpk.get(t.id, "?")
        rels = "; ".join("%s→%s [%s]" % (r.name, (r.target or "").split(".")[-1], r.cardinality or "") for r in t.relations)
        attrs = ""
        if t.kind == "AttributeSet":
            attrs = "Attr: " + ", ".join(a[1] for a in sorted(t.attributes))
        elif t.kind == "ObjectType":
            attrs = ", ".join(x.split(".")[-1] for x in t.implements)
        elif t.kind == "Enumeration":
            attrs = "Enum: " + ", ".join("%s=%s" % (k, v) for k, v in list(t.constants.items())[:12])
        lines.append("| %08x | %s | %s | %s | %s | %s | %s |" % (t.id, t.kind, pk, t.full_name,
                     ", ".join(b.split(".")[-1] for b in t.bases), attrs.replace("|", "/"), rels.replace("|", "/")))
    # object counts per matching type per sample
    lines.append("\n## Objects of these types per sample\n")
    tids = {t.id for t in types if t.kind == "ObjectType"}
    for s in samples:
        p = sets[s][3]
        cnt = {}
        for k, o in p.objects.items():
            tid = o.rec.type_id
            if tid in tids:
                cnt[tid] = cnt.get(tid, 0) + 1
        lines.append("- %s: %s" % (os.path.basename(s), ", ".join("%s×%d" % (p.mm.short_name(t), n) for t, n in sorted(cnt.items())) or "—"))
    open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print("\n".join(lines[:40]))

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:])
