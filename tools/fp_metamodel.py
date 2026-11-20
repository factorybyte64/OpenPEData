"""F3 preparation: metamodel excerpt for WinCC Unified faceplates, read from a project's PEData.plf.

Usage: python -I tools\\fp_metamodel.py <project folder | .ap21 | .zap21> [--out analysis\\F3_faceplate\\METAMODEL.md]

Selection (full detail): every type of the packages DL.FaceplateLib, DL.ModernUI and Hmi.Library; every type of an HMI
package (package or full name contains "Hmi") whose name contains Faceplate, Interface, Contained or Instance; every
HMI type derived from a library TypeObject/VersionObject base (Library.UserTypes); every type of the package
CommonServices.Library.UserTypes whose name matches. Name matches outside HMI are listed briefly (id, package).
Per type: id, kind, package, abstract, bases (direct and all ancestors), implemented AttributeSets (own and inherited),
relations declared on the type and its ancestors, relations from other types that target it or an ancestor.
AttributeSets used by the selected types are listed once with attribute ordinal, name, type, default and flags.
"""
import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import project as projmod  # noqa: E402

PKGS = ("Siemens.Simatic.Hmi.DL.FaceplateLib", "Siemens.Simatic.Hmi.DL.ModernUI", "Siemens.Simatic.Hmi.Library")
NAME_RE = re.compile("Faceplate|Interface|Contained|Instance")
LIB_RE = re.compile(r"TypeObject|VersionObject|Library\.Model\.")
# generic bases whose relations every object has; left out of the per-type relation lists (listed once at the end)
GENERIC = re.compile(r"^Siemens\.Automation\.(ObjectFrame|DomainModel)\.")


def is_hmi(t):
    return "Hmi" in (t.package or "") or ".Hmi." in t.full_name


def select(mm):
    full, brief = [], []
    for t in mm.types.values():
        anc = mm.ancestors(t.full_name)[1:]
        lib = any(LIB_RE.search(a) for a in anc) or LIB_RE.search(t.full_name)
        if t.package in PKGS:
            full.append(t)
        elif NAME_RE.search(t.name) and (is_hmi(t) or t.package == "Siemens.Automation.CommonServices.Library.UserTypes"):
            full.append(t)
        elif lib and is_hmi(t):
            full.append(t)
        elif NAME_RE.search(t.name):
            brief.append(t)
    key = lambda t: (t.package or "", t.full_name)  # noqa: E731
    return sorted(full, key=key), sorted(brief, key=key)


def hx(i):
    return "0x%08x" % i if i is not None else "-"


def rel_line(mm, r, via=None):
    inv = mm.relations.get(r.inverse_id) if r.inverse_id else None
    s = "`%s` %s → `%s` card=%s behaviour=%s" % (r.name, hx(r.id), r.target, r.cardinality, r.behaviour)
    if inv:
        s += ", inverse `%s` %s" % (inv.name, hx(inv.id))
    if r.inverse_of:
        s += " (inverse of %s)" % hx(r.inverse_of)
    if via:
        s += " (declared on `%s`)" % via
    return s


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("project")
    ap.add_argument("--out", default=os.path.join(ROOT, "analysis", "F3_faceplate", "METAMODEL.md"))
    a = ap.parse_args()
    P = projmod.Project(a.project)
    mm = P.mm
    full, brief = select(mm)
    counts = {}
    for o in P.objects.values():
        counts[o.rec.type_id] = counts.get(o.rec.type_id, 0) + 1
    incoming = {}
    for r in mm.relations.values():
        if r.target:
            incoming.setdefault(r.target, []).append(r)
    out = []
    w = out.append
    w("# F3: Metamodel excerpt faceplates (WinCC Unified)")
    w("")
    w("Generated with `tools\\fp_metamodel.py %s` (metamodel record 0000:0007:1 from PEData.plf). Selection: see the head of the script. "
      "`Objects` = number of objects of this type in the project (latest version, without tombstones). Package from the XML nesting." % os.path.basename(a.project))
    w("")
    w("## Overview (%d types in detail)" % len(full))
    w("")
    w("| Type | ID | Kind | Package | abstract | Objects | Base types |")
    w("| --- | --- | --- | --- | --- | --- | --- |")
    for t in full:
        w("| `%s` | %s | %s | %s | %s | %d | %s |" % (t.full_name, hx(t.id), t.kind, t.package, "yes" if t.abstract else "",
                                                 counts.get(t.id, 0), ", ".join("`%s`" % b for b in t.bases)))
    w("")
    w("## Further name matches outside HMI (list only)")
    w("")
    for t in brief:
        w("- `%s` %s %s (%s)" % (t.full_name, hx(t.id), t.kind, t.package))
    w("")
    w("## Types in detail")
    used_sets = []
    for t in full:
        w("")
        w("### %s" % t.full_name)
        w("")
        w("- ID %s, %s, Package %s%s, objects in the project: %d" % (hx(t.id), t.kind, t.package, ", abstract" if t.abstract else "",
                                                             counts.get(t.id, 0)))
        anc = mm.ancestors(t.full_name)
        if len(anc) > 1:
            w("- Base types (all): %s" % " → ".join("`%s`" % x for x in anc[1:]))
        if t.kind == "Enumeration":
            w("- Constants: %s" % ", ".join("%s=%s" % (v, n) for v, n in t.constants.items()))
        if t.kind in ("Structure", "AttributeSet"):
            for o, n, ty, d in sorted(t.attributes):
                w("  - %d `%s`: %s%s" % (o, n, ty, " = %s" % d if d is not None else ""))
            if t.kind == "AttributeSet" and t.full_name not in used_sets:
                used_sets.append(t.full_name)
        if t.kind == "ObjectType":
            sets = mm.attribute_sets_ordered(t.id)
            if sets:
                w("- AttributeSets (slot order): %s" % ", ".join("`%s`" % s.split(".")[-1] for s in sets))
                for s in sets:
                    if s not in used_sets:
                        used_sets.append(s)
                    stored = mm.stored_attributes(t.id, s)
                    if stored:
                        w("  - %s stored: %s" % (s.split(".")[-1], ", ".join("%s:%s" % (n, ty) for _, n, ty, _ in stored)))
            ov = {s: v for s, v in t.overrides.items() if v}
            for s, v in ov.items():
                w("  - Overrides on %s: %s" % (s.split(".")[-1], "; ".join("%s %s" % (n, {k: x for k, x in f.items() if k != "name"})
                                                                         for n, f in v.items())))
        rels = []
        for x in [x for x in anc if not GENERIC.match(x)]:
            xt = mm.by_name.get(x)
            for r in (xt.relations if xt else []):
                rels.append(rel_line(mm, r, None if x == t.full_name else x))
        if rels:
            w("- Relations (own and inherited):")
            for r in rels:
                w("  - " + r)
        inc = []
        for x in [x for x in anc if not GENERIC.match(x)]:
            for r in incoming.get(x, []):
                if r.inverse_of is None:
                    inc.append("`%s`.%s" % (r.owner, rel_line(mm, r, None if x == t.full_name else "target " + x)))
        if inc:
            w("- Incoming relations (target is this type or a base type):")
            for r in inc[:60]:
                w("  - " + r)
            if len(inc) > 60:
                w("  - … %d more" % (len(inc) - 60))
    w("")
    w("## Relations of the generic base types (ObjectFrame, DomainModel)")
    w("")
    w("Left out of the type sections; they apply to all types that inherit from them.")
    gen = sorted({x for t in full for x in mm.ancestors(t.full_name) if GENERIC.match(x)})
    for x in gen:
        xt = mm.by_name.get(x)
        for r in (xt.relations if xt else []):
            w("- `%s`: %s" % (x, rel_line(mm, r)))
    w("")
    w("## AttributeSets of the selected types")
    for s in used_sets:
        st = mm.by_name.get(s)
        if not st:
            continue
        w("")
        w("### %s" % s)
        w("")
        w("- ID %s, Package %s%s" % (hx(st.id), st.package, ", expando=%s" % st.expando if st.expando else ""))
        for o, n, ty, d in sorted(st.attributes):
            fl = {k: v for k, v in st.attr_flags.get(n, {}).items() if k not in ("name", "id", "type")}
            w("  - %d `%s`: %s%s%s" % (o, n, ty, " = %s" % d if d is not None else "", " %s" % fl if fl else ""))
    os.makedirs(os.path.dirname(a.out), exist_ok=True)
    with open(a.out, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")
    print("%s: %d types in detail, %d brief, %d attribute sets" % (a.out, len(full), len(brief), len(used_sets)))


if __name__ == "__main__":
    main()
