"""L4: typed instances, their version links and interface roots, versions and tombstones of library objects.

Usage: python dump_instances.py [sample ...]   (default: L01-L21 folders; .zap21 with suffix .zap21)
Writes to stdout.
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
sys.path.insert(0, os.path.join(ROOT, "analysis", "L5_library"))

from openpedata.project import Project  # noqa: E402
from openpedata.record import is_tombstone  # noqa: E402
from library import decode_version_data, key_type_id  # noqa: E402

DEFAULT = ["L01_udt_type_inwork", "L02_udt_type_released", "L03_udt_type_v2", "L04_default_changed",
           "L05_fb_type_inwork", "L06_fb_type_released", "L07_fc_type", "L09_type_depends",
           "L16_inst_two_devices", "L17_inst_outdated", "L18_inst_edit_outdated", "L19_version_discarded",
           "L20_default_two_devices", "L21_fb_inst_outdated"]


def rel(p, l):
    r = p.mm.relations.get(l.relation)
    return r.name if r else "slot%d" % l.slot


def vtxt(p, v):
    if v not in p.objects:
        return "%s(missing)" % v
    return "%s %s %s" % (decode_version_data(p.attr(v, "IVersionInfo", "Version"))[0],
                         p.attr(v, "ICoreLibObjectAttributes", "State"), p.attr(v, "IVersionInfo", "VersionGuid"))


def dump(path):
    p = Project(path)
    out = ["=" * 100, "%s  (%d objects, %d tombstones)" % (os.path.basename(path), len(p.objects), len(p.deleted))]
    inc = {}
    for k, o in p.objects.items():
        for l in o.links:
            inc.setdefault(l.key, []).append((rel(p, l), k))
    vers = [k for k in p.objects if p.is_a(k, "VersionObjectBase")]
    types = [k for k in p.objects if p.is_a(k, "TypeObjectBase")]
    for t in types:
        dflt = p.targets(t, "TypeToDefaultVersion")
        out.append("TYPE %s %s guid=%s default=%s" % (t, p.name(t), p.attr(t, "ITypeInfo", "TypeGuid"),
                                                     [vtxt(p, d) for d in dflt]))
        tb = p.attributes(t)[0].get("ITypeBaseAttributesInfo", {})
        out.append("     ITypeBaseAttributesInfo %s" % tb)
        out.append("     links: %s" % ", ".join("%s->%s" % (rel(p, l), l.key) for l in p.objects[t].links))
    for v in sorted(vers):
        a = p.attributes(v)[0]
        out.append("VERSION %s %s finalized=%s modified=%s" % (v, vtxt(p, v), a.get("IVersionInfo", {}).get("FinalizationTime"),
                                                              a.get("ICoreAttributes", {}).get("ModifiedTime")))
        out.append("     links: %s" % ", ".join("%s->%s" % (rel(p, l), l.key) for l in p.objects[v].links
                                                if rel(p, l) not in ("EditingCulture", "ReferenceCulture", "UsedCultures")))
        out.append("     copies with Environment->version: %d" % sum(1 for r, s in inc.get(v, []) if r == "Environment"))
    for k in sorted(p.objects):
        if not p.is_a(k, "InstanceObject"):
            continue
        io = p.targets(k, "IsInstanceOf")
        env = p.targets(k, "Environment")
        if not io and not any(r == "IsTypeOf" for r, _ in inc.get(k, [])):
            continue
        if env and (p.is_a(env[0], "VersionObjectBase") or p.is_a(env[0], "CoreLibraryMasterCopy")):
            continue
        tgt = p.targets(k, "Target")
        a = p.attributes(k)[0]
        out.append("INSTANCE %s %s %r device=%s created=%s modified=%s" % (
            k, p.type_name(k), p.name(k), p.name(tgt[0]) if tgt else None,
            a.get("ICoreAttributes", {}).get("CreationTime"), a.get("ICoreAttributes", {}).get("ModifiedTime")))
        out.append("     IsInstanceOf -> %s" % [vtxt(p, x) for x in io])
        for r in ("CurrentLibraryInterface", "CurrentInterface", "BlockToVersionElements", "CurrentLibraryVersion"):
            for x in p.targets(k, r):
                xa = p.attributes(x)[0]
                exp = xa.get("ICoreExpandoAttributes", {})
                out.append("     %s -> %s %r UserTypeVersionGUID=%s TrackingGuid=%s SourceObjectID=%s parent=%s" % (
                    r, x, p.name(x), exp.get("UserTypeVersionGUID"),
                    xa.get("IInterfaceVersionData", {}).get("TrackingGuid"),
                    xa.get("IInterfaceVersionData", {}).get("SourceObjectID"),
                    [p.name(y) for y in p.targets(x, "Parent")]))
        other = [(rel(p, l), l.key) for l in p.objects[k].links]
        out.append("     all links: %s" % ", ".join("%s->%s" % x for x in other))
        exp = a.get("ICoreExpandoAttributes", {})
        lib_like = {n: v for n, v in exp.items() if "Lib" in n or "Version" in n or "Type" in n}
        if lib_like:
            out.append("     expando: %s" % lib_like)
        ct = a.get("ICoreTypeObjectAttributes")
        if ct:
            out.append("     ICoreTypeObjectAttributes %s" % ct)
    # tombstones: type of the deleted object from its last live record
    hist = {}
    for r in p.records:
        if r.is_store:
            continue
        if not is_tombstone(r):
            hist[r.key] = r
    for k in sorted(p.deleted):
        t = p.mm.types.get(key_type_id(k))
        last = hist.get(k)
        out.append("TOMBSTONE %s %s (last live record %s bytes)" % (k, t.name if t else "?", len(last.data) if last else None))
    return "\n".join(out)


def main(argv):
    names = argv or DEFAULT
    sys.stdout.reconfigure(encoding="utf-8")
    for n in names:
        path = n if os.path.isabs(n) else os.path.join(ROOT, "samples", n)
        print(dump(path))


if __name__ == "__main__":
    main(sys.argv[1:])
