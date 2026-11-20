"""H-020: slot order of LibraryMasterCopyData - declaration order (parser) vs. alphabetical by qualified name.
Prints per master copy record: slot table, both candidate orders, payload length and first bytes per slot."""
import os, sys, struct
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.project import Project

def main(path):
    p = Project(path)
    mm = p.mm
    for k, o in p.objects.items():
        if mm.short_name(o.rec.type_id) != "LibraryMasterCopyData":
            continue
        decl = mm.attribute_sets_ordered(o.rec.type_id)
        alpha = sorted(mm.attribute_sets(o.rec.type_id))
        print(k, p.name(k), "slots", o.slot_count, "flags20", o.flags20, "nsets", len(decl))
        for i, off in enumerate(o.slots):
            if i < len(decl):
                d, a = decl[i].split(".")[-1], alpha[i].split(".")[-1]
                mark = "  " if d == a else "<>"
            else:
                d = a = "<links>"; mark = "  "
            blk = o.blocks.get(i)
            info = ""
            if blk:
                ln = struct.unpack_from("<I", o.data, blk[0])[0]
                info = "len=%d span=%d %s" % (ln, blk[1] - blk[0], o.data[blk[0]:blk[0] + 24].hex())
            print("  %2d %s decl=%-34s alpha=%-34s %s" % (i, mark, d, a, info))

if __name__ == "__main__":
    for a in sys.argv[1:]:
        print("=== ", a); main(a)
