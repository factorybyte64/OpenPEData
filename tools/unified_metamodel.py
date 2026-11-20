"""HMI/Unified-related packages and types in the metamodel (record 0000:0007:1) of every sample.

Usage: python -I tools\\unified_metamodel.py [samples-dir] [--types]
Prints per sample: metamodel hash, matching packages; then the union of matching types (id, kind, full name,
package) with the number of objects of that type in each sample.
"""
import collections
import hashlib
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "parser"))
from openpedata import plf  # noqa: E402
from openpedata.metamodel import NS, Metamodel  # noqa: E402
from openpedata.project import METAMODEL_KEY, _read_plf  # noqa: E402

PAT = re.compile(r"Hmi|Unified|Screen|Alarm|Connection|Faceplate|Script(?!ion)", re.I)


def load(path):
    parts = _read_plf(path)
    recs = plf.read_records(parts[0][1], verify=False)
    latest = plf.latest(recs)
    mm_rec = latest[METAMODEL_KEY]
    counts = collections.Counter(r.type_id for k, r in latest.items() if not r.is_store)
    mm = Metamodel.from_record(mm_rec.data)
    # Metamodel.packages is keyed by id >> 16, which several packages share (e.g. 0x0040 = 10 AlarmServices
    # packages); take the owning Package element from the XML instead.
    owner, pkgs = {}, []
    for pkg in ET.fromstring(mm.xml).iter(NS + "Package"):
        pkgs.append((pkg.get("id"), pkg.get("name")))
        for el in pkg.iter():
            if el.get("id") and el is not pkg:
                owner.setdefault(int(el.get("id"), 16), pkg.get("name"))
    return mm, hashlib.sha256(mm_rec.data).hexdigest()[:12], counts, owner, pkgs


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    root = args[0] if args else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "samples")
    samples = sorted(d for d in os.listdir(root)
                     if re.match(r"^\d{3}_[a-z0-9_]+$", d) and not re.search(r"_(analysis|truth)$", d)
                     and os.path.isdir(os.path.join(root, d)))
    types, per = {}, {}
    for s in samples:
        try:
            mm, h, counts, owner, pkgs = load(os.path.join(root, s))
        except Exception as e:  # noqa: BLE001
            print(f"{s}: ERROR {e}")
            continue
        pk = {n for _, n in pkgs if PAT.search(n)}
        print(f"{s}: metamodel {h}, {len(pkgs)} packages, {len(mm.types)} types; matching packages: "
              + ", ".join(f"{i} {n}" for i, n in sorted(pkgs) if n in pk))
        per[s] = counts
        for t in mm.types.values():
            if PAT.search(t.full_name) or owner.get(t.id) in pk:
                types[t.id] = (t.kind, t.full_name, owner.get(t.id, "?"), t.abstract)
    if "--types" in sys.argv:
        print("\nid\tkind\tabstract\tpackage\tfullName\t" + "\t".join(per))
        for tid in sorted(types):
            kind, full, pkg, ab = types[tid]
            print(f"0x{tid:08x}\t{kind}\t{int(ab)}\t{pkg}\t{full}\t" + "\t".join(str(per[s].get(tid, 0)) for s in per))


if __name__ == "__main__":
    main()
