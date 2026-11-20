"""Byte layout of one object record, derived from the metamodel: header, slot table, every AttributeSet with
its stored attributes (offset, size, type, decoded value), link blocks. Compares the derived layout with the
AttributeSet definitions (which attributes of the set are stored, which are constant/relation/intrinsic).

Usage: python tools/layout.py <sample> <record key>
"""
import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import attrs  # noqa: E402
from openpedata.project import Project  # noqa: E402


def layout(sample, key):
    p = Project(os.path.join(ROOT, "samples", sample))
    o = p.objects[key]
    mm, d = p.mm, o.data
    out = ["%s %s  %s  len %d" % (sample, key, mm.type_name(o.rec.type_id), len(d)),
           "  +0 len, +4 key, +16 u32 %d, +20 u32 %d, +26 slots %d, +27 %d, +28 guid %s" % (
               o.flags16, o.flags20, o.slot_count, o.b27, o.guid.hex()),
           "  slot table @44..%d" % o.table_end]
    for i in range(o.slot_count):
        a_e = o.blocks.get(i)
        out.append("  slot %2d @%-3d %-34s %s" % (i, 44 + 4 * i, o.set_name(i), "[%d..%d)" % a_e if a_e else "empty"))
        if i >= o.n_sets or o.is_expando(i):
            continue
        full = o.set_names[i]
        st = mm.by_name[full]
        stored = {n for _, n, _, _ in mm.stored_attributes(o.rec.type_id, full)}
        if a_e:
            blk = d[a_e[0]:a_e[1]]
            vals, probs = attrs.decode_set(mm, o.rec.type_id, full, blk)
            pos = 4
            for ordinal, n, typ, fl in mm.stored_attributes(o.rec.type_id, full):
                sz = attrs.type_size(mm, typ)
                if sz is None:
                    (off,) = struct.unpack_from("<I", blk, pos)
                    out.append("      +%-3d %-28s %-26s off %-4s = %r" % (pos, n, typ, off, vals.get(n)))
                    pos += 4
                else:
                    out.append("      +%-3d %-28s %-26s %d B     = %r" % (pos, n, typ, sz, vals.get(n)))
                    pos += sz
            for pr in probs:
                out.append("      PROBLEM " + pr)
        skipped = []
        for ordinal, n, typ, _ in sorted(st.attributes):
            if n in stored:
                continue
            fl = dict(st.attr_flags.get(n, {}))
            for a in reversed(mm.ancestors(mm.types[o.rec.type_id].full_name)):
                at = mm.by_name.get(a)
                ov = at.overrides.get(full, {}).get(n) if at else None
                if ov:
                    fl.update(ov)
            why = "intrinsic" if fl.get("intrinsic") == "true" else ("relation " + fl["relation"]) if fl.get("relation") \
                else "constant" + (" (default %s)" % fl.get("DefaultValue") if fl.get("DefaultValue") else "")
            skipped.append("%s=%s" % (n, why))
        if skipped:
            out.append("      not stored: " + ", ".join(skipped))
    for l in o.links:
        r = mm.relations.get(l.relation)
        out.append("  link @%d slot %d %-32s -> %s %s %r" % (l.pos, l.slot, r.name if r else "(typed)", l.key,
                                                           p.type_name(l.key) if l.key in p.objects else "?",
                                                           p.name(l.key) if l.key in p.objects else None))
    return out


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    print("\n".join(layout(sys.argv[1], sys.argv[2])))
