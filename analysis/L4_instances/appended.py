"""L4: records a sample appended to its predecessor's plf (folder copies: predecessor plf is a byte prefix).

Usage: python appended.py <predecessor> <sample>   (names below samples/)
Lists each appended record in log order: key, metamodel type, length, tombstone / store marker, object name.
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))

from openpedata import plf  # noqa: E402
from openpedata.project import Project  # noqa: E402
from openpedata.record import is_tombstone  # noqa: E402

STORE = {0x00070014: "$$COMMIT", 0x00070015: "CLOSE"}


def main(a, b):
    pa = os.path.join(ROOT, "samples", a, "System", "PEData.plf")
    pb = os.path.join(ROOT, "samples", b, "System", "PEData.plf")
    A, B = open(pa, "rb").read(), open(pb, "rb").read()
    print("prefix: %s (%d of %d bytes)" % (B[:len(A)] == A, len(A), len(B)))
    P = Project(os.path.join(ROOT, "samples", b))
    for r in plf.read_records(B):
        if r.offset < len(A):
            continue
        t = P.mm.types.get(r.type_id)
        if r.is_store:
            tag = STORE.get(r.type_id, "store %08x" % r.type_id)
            print("%8d %-18s %-40s %6d" % (r.offset, r.key, tag, r.length))
            continue
        tomb = is_tombstone(r)
        name = None if tomb or r.key not in P.objects else P.name(r.key)
        live = "" if tomb or r.key in P.objects else " (superseded)"
        print("%8d %-18s %-40s %6d %s%s%s" % (r.offset, r.key, t.name if t else "?", r.length,
                                              "TOMBSTONE" if tomb else "", repr(name) if name else "", live))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main(*sys.argv[1:3])
