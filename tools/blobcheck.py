"""Checks the blob layout (attrs.decode_blob) on every BlobT/XmlT attribute and blob expando value of the given
samples: decoded size must equal the declared total size; XmlT content must start with an XML declaration/BOM.
Kind 4 blobs with pages in store records are counted as "kind 4 external" (and their store records resolved).
Usage: python tools/blobcheck.py [sample or path to a project ...]"""
import collections
import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import attrs  # noqa: E402
from openpedata.project import Project  # noqa: E402


def ok_struct(d, v):
    if v[0] == 4 and attrs.external_refs(v) is None:
        return True  # varint + bytes: no declared total
    return len(d) == struct.unpack_from("<Q", v, 1)[0]


def main(names):
    st = collections.Counter()
    bad = []
    for s in names:
        p = Project(s if os.path.exists(s) else os.path.join(ROOT, "samples", s))
        used = set()
        for k, o in p.objects.items():
            vals, _ = p.attributes(k)
            for i in range(o.n_sets):
                sn = o.set_name(i)
                if o.is_expando(i) and p.mm.by_name[o.set_names[i]].expando == "simple":
                    continue  # values are CoreText, not blobs (attrs.decode_simple_expando)
                typ = {n: t for _, n, t, _ in p.mm.stored_attributes(o.rec.type_id, o.set_names[i])} if i < o.n_sets else {}
                for n, v in vals.get(sn, {}).items():
                    t = typ.get(n)
                    if o.is_expando(i) and isinstance(v, bytes) and v:
                        b, _ = attrs.read_var(v, 0)  # expando blob values carry their own varint
                        v, t = b, "expando"
                    if t not in ("pe:BlobT", "pe:XmlT", "expando") or not isinstance(v, bytes) or not v:
                        continue
                    try:
                        d = p.decode_blob(v)
                    except Exception as ex:  # noqa: BLE001
                        st["%s kind %d EXCEPTION %s" % (t, v[0], type(ex).__name__)] += 1
                        if len(bad) < 3:
                            bad.append((s, k, n, v[:48].hex()))
                        continue
                    kind = "4 external" if attrs.external_refs(v) is not None else str(v[0])
                    if d is None:
                        st["unknown kind %s" % kind] += 1
                        continue
                    if not ok_struct(d, v):
                        st["%s kind %s size mismatch" % (t, kind)] += 1
                        if len(bad) < 6:
                            bad.append((s, k, n, len(d), v[:48].hex()))
                        continue
                    ok = True
                    used.update(attrs.external_refs(v) or [])
                    if t == "pe:XmlT" and d:
                        ok &= d[:3] == b"\xef\xbb\xbf" or d.lstrip()[:1] == b"<"
                    st["%s kind %s %s" % (t, kind, "ok" if ok else "BAD")] += 1
        stores = {k for k, r in p.latest.items() if r.type_id == attrs.BLOB_STORE_TYPE and r.is_store}
        if stores or used:
            st["blob store records referenced %d of %d" % (len(used & stores), len(stores))] += 1
    for k, c in sorted(st.items()):
        print("%6d  %s" % (c, k))
    for b in bad:
        print("example", b)


if __name__ == "__main__":
    sd = os.path.join(ROOT, "samples")
    main(sys.argv[1:] or sorted(x for x in os.listdir(sd) if os.path.isdir(os.path.join(sd, x, "System"))))
