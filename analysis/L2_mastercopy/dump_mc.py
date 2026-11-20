"""Dump of every LibraryMasterCopyData of a sample: attributes, links, CoreObjectData (raw + tentative parse),
SharedRawData (full text), and the copied subtree (objects with Environment -> master copy) incl. IsShell and
the original object of the same type and name outside the master copy.
Usage: python dump_mc.py <sample> [...]"""
import os, sys, struct
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.project import Project


def incoming(p):
    inc = {}
    for k, o in p.objects.items():
        for l in o.links:
            r = p.mm.relations.get(l.relation) if l.relation is not None else None
            inc.setdefault(l.key, []).append((k, r.name if r else "slot%s" % l.slot))
    return inc


def env_of(p, k):
    for l in p.links(k, "Environment"):
        return l.key
    return None


def blob(v):
    return v if isinstance(v, (bytes, bytearray)) else None


def parse_core_object_data(b):
    """Tentative: u32 BE count?, then entries. Returns list of u32 BE words for inspection."""
    if not b:
        return None
    words = [struct.unpack_from(">I", b, i)[0] for i in range(0, len(b) - len(b) % 4, 4)]
    return {"len": len(b), "hex": b.hex(), "u32be": words}


def dump(path, out):
    p = Project(path, verify=False)
    inc = incoming(p)
    mcs = [k for k, o in p.objects.items() if p.mm.short_name(o.rec.type_id) == "LibraryMasterCopyData"]
    out.append("=== %s: %d master copies, %d tombstones" % (path, len(mcs), len(p.deleted)))
    for mc in sorted(mcs):
        a, pr = p.attributes(mc)
        out.append("MC %s %r guid=%s" % (mc, p.name(mc), p.objects[mc].guid.hex()))
        for s, vals in a.items():
            for n, v in vals.items():
                if s == "ICoreLibMasterCopyAttributes" and n in ("SharedRawData",):
                    continue
                if isinstance(v, (bytes, bytearray)):
                    v = "bytes[%d] %s" % (len(v), v.hex()[:160])
                out.append("   %s.%s = %s" % (s, n, v))
        if pr:
            out.append("   PROBLEMS %s" % pr)
        mca = a.get("ICoreLibMasterCopyAttributes", {})
        cod = blob(mca.get("CoreObjectData"))
        out.append("   CoreObjectData: %s" % parse_core_object_data(cod))
        ncd = blob(mca.get("NonCoreObjectData"))
        out.append("   NonCoreObjectData: %s" % (ncd.hex() if ncd is not None else None))
        out.append("   SharedRawData: %s" % mca.get("SharedRawData"))
        for l in p.objects[mc].links:
            r = p.mm.relations.get(l.relation) if l.relation is not None else None
            tn = p.type_name(l.key) if l.key in p.objects else "?"
            out.append("   link %-22s slot%-2s -> %s %s %r" % (r.name if r else "?", l.slot, l.key, tn,
                                                         p.name(l.key) if l.key in p.objects else None))
        for src, rel in inc.get(mc, []):
            if rel != "Environment":
                out.append("   incoming %s <- %s %s %r" % (rel, src, p.type_name(src), p.name(src)))
        copies = sorted(s for s, rel in inc.get(mc, []) if rel == "Environment")
        out.append("   copies (Environment -> MC): %d" % len(copies))
        for c in copies:
            nm = p.name(c)
            orig = [k for k in p.objects if k != c and p.objects[k].rec.type_id == p.objects[c].rec.type_id
                    and nm is not None and p.name(k) == nm and env_of(p, k) not in mcs]
            out.append("     %s %-32s %-40r shell=%s size=%d orig=%s" % (
                c, p.type_name(c), nm, p.attr(c, "ICoreAttributes", "IsShell"), len(p.objects[c].data), orig))
            for l in p.objects[c].links:
                r = p.mm.relations.get(l.relation) if l.relation is not None else None
                rn = r.name if r else "slot%s" % l.slot
                if rn in ("Environment",):
                    continue
                inside = "IN" if (l.key in copies or l.key == mc) else "OUT"
                out.append("         %-3s %-40s -> %s %s %r" % (inside, rn, l.key,
                           p.type_name(l.key) if l.key in p.objects else "?",
                           p.name(l.key) if l.key in p.objects else None))
    return p


if __name__ == "__main__":
    out = []
    for a in sys.argv[1:]:
        dump(a, out)
    print("\n".join(out))
