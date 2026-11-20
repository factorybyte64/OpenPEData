"""F3 list item 19 (groundwork, not yet part of the parser): WinCC Unified faceplate types of a project.

Types, versions, default, state, author, release time come from parser\\openpedata\\library.py (extract_library,
L5, commit bcf43ee); this module only adds what is specific to faceplates:
  per version  content: ILFaceplateTypeData (VersionToContent) with size and screen items (hmi.py decoders),
               interface placeholder (IFaceplateInterface has no block while empty, A123.md),
               unresolved tag shells (HmiTagData without TagTable in the version's hidden device, F03.md),
               scripts, number of objects in the version's environment (HIDDEN-DEVICE.md)
  instances    typed instances from library.py ("instances", IsInstanceOf -> version) plus faceplate containers
               without a type (ContainedType empty, F01)

Usage: python -I analysis\\F3_faceplate\\faceplates.py <project> [--json out.json] [--txt out.txt]
Read only; hmi.py and library.py are used, not changed.
"""
import argparse
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.hmi import Hmi  # noqa: E402
from openpedata.library import extract_library  # noqa: E402
from openpedata.model import Model  # noqa: E402

FACEPLATE_TYPE = "FaceplateLibraryTypeData"
VERSION_FIELDS = ("version", "state", "isDefault", "finalizationTime", "author", "comment", "minimumTargetDeviceVersion")
TYPE_FIELDS = ("name", "typeGuid", "folderPath", "isObsolete", "defaultVersion", "latestVersion", "instanceCount")


def _short(p, k):
    return (p.type_name(k) or "").rsplit(".", 1)[-1]


def content(m, h, c):
    p = m.p
    props = h.screen_properties(c)
    o = p.objects[c]
    has_iface = any(o.set_name(i) == "IFaceplateInterface" for i in o.blocks)
    items = []
    for k in p.targets(c, "ILScreenItems"):
        ip = h.screen_properties(k)
        items.append({"name": p.name(k), "type": p.attr(k, "ICoreAttributes", "Subtype"),
                      "left": ip.get("Left", ip.get("CenterX")), "top": ip.get("Top", ip.get("CenterY")),
                      "width": ip.get("Width"), "height": ip.get("Height"),
                      "events": [p.name(e) for e in p.targets(k, "EmbeddedScriptChildren")], "key": k})
    items.sort(key=lambda x: x["name"] or "")
    return {"width": props.get("Width"), "height": props.get("Height"), "items": items,
            "interface": {"present": has_iface, "tags": [], "properties": [], "events": []}, "key": c}


def extract_faceplates(m):
    p = m.p
    h = Hmi(m)
    lib = extract_library(m, keys=True)
    out = []
    for L in lib["libraries"]:
        for t in L["types"]:
            if t["type"] != FACEPLATE_TYPE:
                continue
            d = {f: t.get(f) for f in TYPE_FIELDS}
            d["library"] = L["name"]
            d["alwaysDownload"] = p.attr(t["key"], "IHmiByRefTypeObjectBaseAttributes", "AlwaysDownload")
            d["key"] = t["key"]
            d["versions"] = []
            for v in t["versions"]:
                vk = v["key"]
                e = {f: v.get(f) for f in VERSION_FIELDS}
                if (e["finalizationTime"] or "").startswith("0001-"):
                    e["finalizationTime"] = None
                mem = v.get("copiedObjectKeys") or []
                c = m.rel1(vk, "VersionToContent")
                e["content"] = content(m, h, c) if c else None
                e["unresolvedTags"] = sorted(p.name(k) for k in mem if _short(p, k) == "HmiTagData" and not m.rel1(k, "TagTable"))
                e["scripts"] = sum(1 for k in mem if _short(p, k) == "HmiScriptEmbeddedData")
                e["environmentObjects"] = v.get("copiedObjects")
                e["instances"] = v.get("instances")
                e["key"] = vk
                d["versions"].append(e)
            out.append(d)
    return out, lib


def instances(m, lib):
    """Typed instances (library.py) that are faceplate containers, plus containers without a type."""
    p = m.p
    res = []
    typed = set()
    for i in lib.get("instances", []):
        k = i.get("key")
        if k in p.objects and _short(p, k) == "ILFaceplateContainerData":
            typed.add(k)
            scr = m.rel1(k, "ILScreenItemParent")
            res.append({"name": i["name"], "screen": p.name(scr) if scr else None, "instanceOf": i.get("instanceOf"),
                        "versionState": i.get("versionState"), "key": k})
    for k in sorted(p.objects):
        if _short(p, k) != "ILFaceplateContainerData" or k in typed:
            continue
        env = m.rel1(k, "Environment")
        if env and _short(p, env) == "FaceplateLibraryVersionData":
            continue  # container inside a faceplate type (nested faceplate), listed with its version
        scr = m.rel1(k, "ILScreenItemParent")
        res.append({"name": p.name(k), "screen": p.name(scr) if scr else None, "instanceOf": None,
                    "versionState": None, "key": k})
    return res


def text(types, inst):
    out = []
    for t in types:
        out.append("Faceplate type %s  guid %s%s%s  default %s" % (
            t["name"], t["typeGuid"], "  [do not use]" if t["isObsolete"] else "",
            "  [always download]" if t["alwaysDownload"] else "", t["defaultVersion"]))
        for v in t["versions"]:
            out.append("  V %s %s%s  author %s%s  env %s objects" % (
                v["version"], v["state"], " default" if v["isDefault"] else "", v["author"],
                "  released " + v["finalizationTime"] if v["finalizationTime"] else "", v["environmentObjects"]))
            c = v["content"]
            if c:
                out.append("    content %sx%s, interface %s" % (c["width"], c["height"],
                                                                "present" if c["interface"]["present"] else "empty"))
                for it in c["items"]:
                    out.append("      %-16s %-28s %s,%s %sx%s%s" % (
                        it["name"], it["type"], it["left"], it["top"], it["width"], it["height"],
                        "  events " + ",".join(it["events"]) if it["events"] else ""))
            if v["unresolvedTags"]:
                out.append("    unresolved tags (shells): %s" % ", ".join(v["unresolvedTags"]))
    for i in inst:
        of = ", ".join("%s %s" % (x.get("type") or x.get("name"), x.get("version")) for x in (i["instanceOf"] or [])) or "no type"
        out.append("Faceplate instance %s on %s (%s)" % (i["name"], i["screen"], of))
    return "\n".join(out) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("project")
    ap.add_argument("--json")
    ap.add_argument("--txt")
    a = ap.parse_args()
    m = Model(a.project)
    types, lib = extract_faceplates(m)
    inst = instances(m, lib)
    t = text(types, inst)
    if a.json:
        with open(a.json, "w", encoding="utf-8") as f:
            json.dump({"project": a.project, "faceplateTypes": types, "instances": inst}, f, ensure_ascii=False, indent=1,
                      default=str)
    if a.txt:
        with open(a.txt, "w", encoding="utf-8") as f:
            f.write(t)
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stdout.write(t)


if __name__ == "__main__":
    main()
