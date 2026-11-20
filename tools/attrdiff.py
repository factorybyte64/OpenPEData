"""Attribute-level diff of two samples (current object versions): new/removed objects, changed decoded
attributes per AttributeSet, changed links, and set blocks whose bytes differ although nothing decoded
differs (= not yet understood). Timestamps (xs:dateTime) are reported but marked as noise.

Usage: python tools/attrdiff.py <sampleA> <sampleB> [--out file]
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.project import Project  # noqa: E402


def short(v, n=80):
    if isinstance(v, bytes):
        return "bytes[%d] %s" % (len(v), v[:n // 2].hex())
    s = repr(v)
    return s if len(s) <= n else s[:n] + "..."


def diff(a_name, b_name):
    A = Project(os.path.join(ROOT, "samples", a_name))
    B = Project(os.path.join(ROOT, "samples", b_name))
    out = ["A: %s  B: %s" % (a_name, b_name)]
    for k in sorted(set(A.objects) | set(B.objects)):
        if k not in A.objects:
            out.append("NEW     %s %s name=%r" % (k, B.type_name(k), B.name(k)))
            for s, vals in B.attributes(k)[0].items():
                for n, v in vals.items():
                    out.append("          %s.%s = %s" % (s, n, short(v)))
            for l in B.objects[k].links:
                r = B.mm.relations.get(l.relation)
                out.append("          link %s -> %s" % (r.name if r else "typed slot %d" % l.slot, l.key))
            continue
        if k not in B.objects:
            out.append("REMOVED %s %s name=%r" % (k, A.type_name(k), A.name(k)))
            continue
        oa, ob = A.objects[k], B.objects[k]
        if oa.data == ob.data:
            continue
        lines = []
        va, vb = A.attributes(k)[0], B.attributes(k)[0]
        for s in sorted(set(va) | set(vb)):
            da, db = va.get(s, {}), vb.get(s, {})
            for n in sorted(set(da) | set(db)):
                if da.get(n, "<absent>") != db.get(n, "<absent>"):
                    typ = next((t for _, nm, t, _ in B.mm.stored_attributes(ob.rec.type_id, next(
                        (x for x in ob.set_names if x.endswith("." + s)), "")) if nm == n), "")
                    noise = "  (timestamp)" if typ == "xs:dateTime" else ""
                    lines.append("          %s.%s: %s -> %s%s" % (s, n, short(da.get(n, "<absent>")), short(db.get(n, "<absent>")), noise))
        # raw compare of every slot block, to find changes in parts that are not decoded
        for i in range(max(oa.slot_count, ob.slot_count)):
            ba = oa.blocks.get(i)
            bb = ob.blocks.get(i)
            ra = oa.data[ba[0]:ba[1]] if ba else b""
            rb = ob.data[bb[0]:bb[1]] if bb else b""
            if ra != rb:
                name = ob.set_name(i) if i < ob.slot_count else "slot%d" % i
                decoded = name in va or name in vb
                if not decoded or i >= ob.n_sets or ob.is_expando(i):
                    lines.append("          raw %s: %d -> %d bytes" % (name, len(ra), len(rb)))
        la = {(l.relation, l.slot, l.key) for l in oa.links}
        lb = {(l.relation, l.slot, l.key) for l in ob.links}
        for rel, slot, key in sorted(lb - la, key=str):
            r = B.mm.relations.get(rel)
            lines.append("          +link %s -> %s" % (r.name if r else "typed slot %d" % slot, key))
        for rel, slot, key in sorted(la - lb, key=str):
            r = A.mm.relations.get(rel)
            lines.append("          -link %s -> %s" % (r.name if r else "typed slot %d" % slot, key))
        if oa.slots != ob.slots:
            lines.append("          slot table %s -> %s" % (oa.slots, ob.slots))
        out.append("CHANGED %s %s name=%r" % (k, B.type_name(k), B.name(k)))
        out.extend(lines or ["          (only header bytes differ)"])
    store = [k for k in set(A.latest) | set(B.latest) if (A.latest.get(k) or B.latest.get(k)).is_store
             and (k not in A.latest or k not in B.latest or A.latest[k].data != B.latest[k].data)]
    out.append("store records changed/new: %s" % ", ".join(sorted(store)))
    return out


if __name__ == "__main__":
    args = sys.argv[1:]
    outf = None
    if "--out" in args:
        i = args.index("--out")
        outf = args[i + 1]
        del args[i:i + 2]
    lines = diff(args[0], args[1])
    text = "\n".join(lines) + "\n"
    if outf:
        with open(outf, "w", encoding="utf-8") as f:
            f.write(text)
    sys.stdout.reconfigure(encoding="utf-8")
    print(text)
