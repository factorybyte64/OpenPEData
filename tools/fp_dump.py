"""F3 preparation: complete dump of single objects (e.g. a faceplate container) of a TIA V21 project.

Usage: python -I tools\\fp_dump.py <project folder | .ap21 | .zap21> <name or key> [...] [--out file]

Per object: record header (key, type, flags, slot table, GUID, size, all record versions of the key in the plf),
every slot with AttributeSet name, span, decoded attributes (incl. expandos), decode problems and a full hex dump,
outgoing links (relation name, target key, type, name), incoming links (every object whose link points to it),
and every other record that contains the object's GUID or its 12-byte link reference (typeLo, typeHi, oid) in raw
bytes (references outside link slots, e.g. inside expando blobs).
"""
import argparse
import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
sys.path.insert(0, os.path.join(ROOT, "tools"))
import unified_tagdiff  # noqa: E402,F401  (H-023 workaround for short expando blocks)
from openpedata import project as projmod  # noqa: E402


def hexdump(b, base, indent="      "):
    out = []
    for i in range(0, len(b), 32):
        chunk = b[i:i + 32]
        txt = "".join(chr(c) if 32 <= c < 127 else "." for c in chunk)
        out.append("%s%06x  %-96s %s" % (indent, base + i, chunk.hex(" "), txt))
    return out


def find_key(P, s):
    if s in P.objects:
        return s
    hits = [k for k in P.objects if P.name(k) == s]
    return hits[0] if len(hits) == 1 else (hits or None)


def rel_name(P, l):
    r = P.mm.relations.get(l.relation) if l.relation else None
    return r.name if r else ("typed slot %d" % l.slot if l.relation is None else "?0x%08x" % l.relation)


def obj_desc(P, k):
    if k in P.objects:
        return "%s %s name=%r" % (k, P.type_name(k), P.name(k))
    return "%s (no current object)" % k


def dump(P, k):
    o = P.objects[k]
    r = o.rec
    out = []
    w = out.append
    w("=" * 100)
    w("OBJECT %s  type %s (0x%08x)  name=%r" % (k, P.mm.type_name(r.type_id), r.type_id, P.name(k)))
    w("  record @%d length %d, u32@16=%d u32@20=%d slot count %d b27=%d GUID %s" % (
        r.offset, len(o.data), o.flags16, o.flags20, o.slot_count, o.b27, o.guid.hex()))
    w("  layout_ok=%s, sets %d, errors %s" % (o.layout_ok, o.n_sets, o.errors or "-"))
    vers = [x for x in P.records if x.key == r.key]
    w("  record versions of this key: %s" % ", ".join("@%d len %d" % (x.offset, len(x.data)) for x in vers))
    w("  ancestors: %s" % " > ".join(P.mm.ancestors(P.mm.type_name(r.type_id))))
    w("  header bytes:")
    out.extend(hexdump(o.data[:o.table_end], 0))
    vals, probs = P.attributes(k)
    for i in range(o.slot_count):
        name = o.set_name(i)
        b = o.blocks.get(i)
        w("  slot %2d @%-6s %-45s %s" % (i, o.slots[i] or "-", name, "%d bytes" % (b[1] - b[0]) if b else "empty"))
        if i < o.n_sets:
            sv = vals.get(name, {})
            for n, v in sv.items():
                w("      %s = %r" % (n, v))
            if name in probs:
                w("      PROBLEM: %s" % probs[name])
        if b:
            out.extend(hexdump(o.data[b[0]:b[1]], b[0]))
    w("  links out (%d):" % len(o.links))
    for l in o.links:
        w("    slot %d %-45s -> %s" % (l.slot, rel_name(P, l), obj_desc(P, l.key)))
    w("  links in:")
    n_in = 0
    for k2, o2 in P.objects.items():
        for l in o2.links:
            if l.key == k:
                n_in += 1
                w("    %s  via %s (slot %d)" % (obj_desc(P, k2), rel_name(P, l), l.slot))
    if not n_in:
        w("    (none)")
    ref12 = struct.pack("<HHQ", r.type_id & 0xFFFF, r.type_id >> 16, int(k.split(":")[2], 16))
    w("  raw references in other records (GUID %s / link ref %s):" % (o.guid.hex(), ref12.hex()))
    n_raw = 0
    for k2, o2 in P.objects.items():
        if k2 == k:
            continue
        for what, pat in (("GUID", o.guid), ("ref12", ref12)):
            if pat != bytes(16) and pat in o2.data:
                pos = o2.data.find(pat)
                slot = next((i for i, (a, e) in o2.blocks.items() if a <= pos < e), None)
                n_raw += 1
                w("    %s at +%d (slot %s %s) in %s" % (what, pos, slot, o2.set_name(slot) if slot is not None else "-",
                                                      obj_desc(P, k2)))
    if not n_raw:
        w("    (none)")
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("project")
    ap.add_argument("objects", nargs="+")
    ap.add_argument("--out")
    a = ap.parse_args()
    P = projmod.Project(a.project)
    out = ["fp_dump %s" % a.project]
    for s in a.objects:
        k = find_key(P, s)
        if not isinstance(k, str):
            out.append("NOT FOUND or ambiguous: %s -> %s" % (s, k))
            continue
        out.extend(dump(P, k))
    text = "\n".join(out) + "\n"
    if a.out:
        os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
        with open(a.out, "w", encoding="utf-8") as f:
            f.write(text)
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stdout.write(text if not a.out else "%s: %d lines\n" % (a.out, len(out)))


if __name__ == "__main__":
    main()
