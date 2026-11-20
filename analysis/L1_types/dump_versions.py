"""L1: dump type / version objects (attributes and links) of the L samples.
Usage: python dump_versions.py [sample ...]"""
import os
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.project import Project  # noqa: E402

SAMPLES = ["L01_udt_type_inwork", "L02_udt_type_released", "L03_udt_type_v2", "L04_default_changed",
           "L05_fb_type_inwork", "L06_fb_type_released", "L07_fc_type", "L09_type_depends"]
BASES = ("TypeObjectBase", "VersionObjectBase")


def main(names):
    sys.stdout.reconfigure(encoding="utf-8")
    for s in names or SAMPLES:
        p = Project(os.path.join(ROOT, "samples", s))
        print("=" * 20, s)
        for k in sorted(p.objects):
            if not any(p.is_a(k, b) for b in BASES):
                continue
            attrs, probs = p.attributes(k)
            print("%s %s name=%r" % (k, p.type_name(k), p.name(k)))
            for st, vals in attrs.items():
                for n, v in vals.items():
                    if st in ("IVersionInfo", "ICoreLibObjectAttributes", "ITypeInfo", "ICoreLibTypeAttributes",
                              "IDependencyInfo", "ISystemVersionOrderInfo", "IVersionManufacturingInfo",
                              "ITypeBaseAttributesInfo") or n in ("Name", "Comment", "Author"):
                        if n in ("IconData", "IconResource"):
                            continue
                        print("    %s.%s = %s" % (st, n, v.hex() if isinstance(v, bytes) else repr(v)))
            for l in p.objects[k].links:
                r = p.mm.relations.get(l.relation)
                print("    link %s -> %s %s %r" % (r.name if r else "slot%d" % l.slot, l.key,
                                                   p.type_name(l.key) if l.key in p.objects else "?",
                                                   p.name(l.key) if l.key in p.objects else None))
            if probs:
                print("    problems:", probs)


if __name__ == "__main__":
    main(sys.argv[1:])
