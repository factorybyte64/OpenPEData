"""Object graph of a sample (step 4a): decodes every current object record (header, slots, attribute sets,
link blocks), validates links against the metamodel and writes samples/<sample>_analysis/graph.json.

Link validation (tagged links: relation id known; typed list links: relation implicit):
  rel_known       relation id exists in the metamodel
  target_exists   target key is a current record
  target_type_ok  target type is-a relation target type
  owner_ok        source type is-a the type declaring the relation
  inverse_ok      target links back with the inverse relation id, or from a typed list (only if an inverse is
                  defined); typed back links give the slot -> relation mapping of typed lists
Detection recall: brute-force scan of every record for 16-byte link patterns (known relation id, known type,
existing target) and 12-byte typed refs (known type, existing target); hits outside parsed link blocks are
listed as "unexplained" (e.g. references inside attribute values).

Usage: python tools/graph.py <sample> [<sample> ...]     (sample = folder name under samples/)
"""
import json
import os
import struct
import sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.project import Project  # noqa: E402


def jsonable(v, cap=64):
    if isinstance(v, bytes):
        return {"hex": v[:cap].hex(), "len": len(v)}
    return v


def analyze(sample):
    p = Project(os.path.join(ROOT, "samples", sample))
    mm, objs = p.mm, p.objects
    stats = Counter()
    nodes, edges = [], []
    link_pos = {}  # key -> set of byte offsets covered by parsed links
    typed_slots = {}  # (type, slot) -> Counter(inverse relation names inferred from tagged back links)
    for k, o in objs.items():
        r = o.rec
        vals, probs = p.attributes(k)
        stats["objects"] += 1
        stats["layout_ok"] += o.layout_ok
        stats["slot_errors"] += bool(o.errors)
        for i in range(o.n_sets):
            if i in o.blocks:
                stats["attr_blocks"] += 1
                stats["attr_blocks_ok"] += o.set_name(i) not in probs
        nodes.append({
            "key": k, "type": mm.type_name(r.type_id), "guid": o.guid.hex(), "name": p.name(k),
            "header": {"u32@16": o.flags16, "u32@20": o.flags20, "slots": o.slot_count, "b27": o.b27},
            "slots": {o.set_name(i): [a, e] for i, (a, e) in sorted(o.blocks.items())},
            "attributes": {s: {n: jsonable(v) for n, v in d.items()} for s, d in vals.items()},
            "problems": o.errors + ["%s: %s" % (s, "; ".join(x)) for s, x in probs.items()],
        })
        cov = link_pos.setdefault(k, set())
        src_full = mm.types[r.type_id].full_name
        for l in o.links:
            cov.add(l.pos)
            e = {"from": k, "to": l.key, "slot": l.slot, "kind": "tagged" if l.relation is not None else "typed"}
            stats["links_" + e["kind"]] += 1
            exists = l.key in objs
            e["target_exists"] = exists
            stats["target_exists"] += exists
            if l.relation is not None:
                rel = mm.relations.get(l.relation)
                e["relation"] = rel.name if rel else None
                e["relation_id"] = "0x%08x" % l.relation
                stats["rel_known"] += rel is not None
                if rel:
                    tok = exists and (not rel.target or p.is_a(l.key, rel.target))
                    ook = rel.owner in mm.ancestors(src_full)
                    e["target_type_ok"], e["owner_ok"] = tok, ook
                    stats["target_type_ok"] += tok
                    stats["owner_ok"] += ook
                    if rel.inverse_id is not None and exists:
                        back = [x for x in objs[l.key].links if x.key == k and x.relation in (rel.inverse_id, None)]
                        e["inverse_ok"] = bool(back)
                        stats["inverse_checked"] += 1
                        stats["inverse_ok"] += bool(back)
                        for x in back:
                            if x.relation is None:  # typed list slot of the target holds the inverse relation
                                inv = mm.relations.get(rel.inverse_id)
                                typed_slots.setdefault((mm.type_name(objs[l.key].rec.type_id), x.slot), Counter())[
                                    inv.name if inv else "0x%08x" % rel.inverse_id] += 1
            edges.append(e)
    # brute-force pattern scan
    rel_ids, type_ids = set(mm.relations), set(mm.types)
    unexplained = []
    for k, o in objs.items():
        d = o.data
        cov = link_pos[k]
        for pos in range(o.table_end, len(d) - 16):
            rel, lo, hi, oid = struct.unpack_from("<IHHQ", d, pos)
            tid = (hi << 16) | lo
            if rel in rel_ids and tid in type_ids and "%04x:%04x:%x" % (lo, hi, oid) in objs:
                stats["scan16_hits"] += 1
                if pos in cov:
                    stats["scan16_in_blocks"] += 1
                else:
                    unexplained.append({"key": k, "pos": pos, "pattern": "link16", "relation": mm.relations[rel].name,
                                        "to": "%04x:%04x:%x" % (lo, hi, oid)})
        for pos in range(o.table_end, len(d) - 12):
            lo, hi, oid = struct.unpack_from("<HHQ", d, pos)
            tid = (hi << 16) | lo
            if tid in type_ids and oid and "%04x:%04x:%x" % (lo, hi, oid) in objs:
                if pos - 4 in cov:
                    continue  # type+oid part of a tagged link
                stats["scan12_hits"] += 1
                if pos in cov:
                    stats["scan12_in_lists"] += 1
                else:
                    unexplained.append({"key": k, "pos": pos, "pattern": "ref12", "to": "%04x:%04x:%x" % (lo, hi, oid),
                                        "in_slot": next((o.set_name(i) for i, (a, e) in o.blocks.items() if a <= pos < e), None)})
    stats["store_records"] = sum(1 for r in p.latest.values() if r.is_store)
    stats["records_total"] = len(p.records)
    stats["hash_errors"] = sum(not r.hash_ok for r in p.records)
    out_dir = os.path.join(ROOT, "samples", sample + "_analysis")
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, "graph.json"), "w", encoding="utf-8") as f:
        json.dump({"sample": sample, "stats": dict(stats), "nodes": nodes, "edges": edges,
                   "typed_slot_relations": [{"type": t, "slot": sl, "relations": dict(c)}
                                            for (t, sl), c in sorted(typed_slots.items())],
                   "unexplained_refs": unexplained}, f, indent=1, ensure_ascii=False)
    return stats, unexplained


def main(argv):
    for s in argv:
        st, un = analyze(s)
        links = st["links_tagged"] + st["links_typed"]
        print("%-22s objects %4d slots ok %4d attr blocks %4d/%4d | links %5d (tagged %d, typed %d) exist %d rel %d "
              "ttype %d owner %d inverse %d/%d | scan16 %d/%d scan12 %d/%d unexplained %d" % (
                  s, st["objects"], st["objects"] - st["slot_errors"], st["attr_blocks_ok"], st["attr_blocks"], links,
                  st["links_tagged"], st["links_typed"], st["target_exists"], st["rel_known"], st["target_type_ok"],
                  st["owner_ok"], st["inverse_ok"], st["inverse_checked"], st["scan16_in_blocks"], st["scan16_hits"],
                  st["scan12_in_lists"], st["scan12_hits"], len(un)))


if __name__ == "__main__":
    main(sys.argv[1:] or sorted(d for d in os.listdir(os.path.join(ROOT, "samples"))
                                 if os.path.isdir(os.path.join(ROOT, "samples", d, "System"))))
