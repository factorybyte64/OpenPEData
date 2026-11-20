"""F3 list item 4: objects that belong to a faceplate version (Environment -> FaceplateLibraryVersionData), counted
per type, names of the constant parts, and the link structure (which relation connects each object to a parent
inside the version or to the version itself).

Usage: python -I analysis\\F3_faceplate\\hidden_device.py <project> [...]   (read only)
"""
import collections
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
sys.path.insert(0, os.path.join(ROOT, "tools"))
import unified_tagdiff  # noqa: E402,F401  (H-023)
from openpedata.model import Model  # noqa: E402

# relations that express ownership (child -> parent); others are references
PARENT_RELATIONS = None


def rel_name(P, l):
    r = P.mm.relations.get(l.relation) if l.relation else None
    return r.name if r else "typed slot %d" % l.slot


def version_members(M, v):
    P = M.p
    out = []
    for k, o in P.objects.items():
        for l in o.links:
            if l.key == v and rel_name(P, l) == "Environment":
                out.append(k)
                break
    return out


def structure(M, v, members):
    """For every member: links from the version or from another member that point to it (relation, source type)."""
    P = M.p
    inside = set(members) | {v}
    res = collections.defaultdict(collections.Counter)
    for src in inside:
        for l in P.objects[src].links:
            if l.key in inside and l.key != src:
                rn = rel_name(P, l)
                if rn in ("Environment", "Target"):
                    continue
                res[P.type_name(l.key)][(P.type_name(src) if src != v else "VERSION", rn)] += 1
    return res


def main():
    for path in sys.argv[1:]:
        M = Model(path)
        P = M.p
        print("=" * 100)
        print(path)
        for v in sorted(k for k in P.objects if P.type_name(k) == "FaceplateLibraryVersionData"):
            t = M.rel1(v, "VersionObjectToTypeObject")
            mem = version_members(M, v)
            print("-" * 100)
            print("VERSION %s of %s: %d members" % (v, P.name(t), len(mem)))
            by = collections.defaultdict(list)
            for k in mem:
                by[P.type_name(k)].append(P.name(k))
            for ty in sorted(by):
                names = sorted(str(n) for n in by[ty])
                print("  %3d %-34s %s" % (len(names), ty, ", ".join(names)[:220]))
            print("  links into members (target type <- source type via relation, count):")
            st = structure(M, v, mem)
            for ty in sorted(st):
                print("    %-34s <- %s" % (ty, "; ".join("%s.%s x%d" % (s, r, n) for (s, r), n in sorted(st[ty].items()))))


if __name__ == "__main__":
    main()
