"""Builds ui\\OpenPEData.Viewer\\HmiDefaults.json, the default values of WinCC Unified screen items for drawing in the
viewer (Z1.5; decision User 2026-10-29: Openness defaults only in the viewer, the parser output stays what the file
holds). Source: sample 165_hmi_item_defaults, one screen per item type with one item created without attributes, read
with TiaHmiUnified itemGet detail=full (Openness values incl. defaults) and screen get.

Openness values contain the values of the selected style (165: ExtendedStyle). The file holds the object defaults
without style: a value that the selected style of 165 sets is taken from 166_hmi_style_dark or 167_hmi_style_bright
(same items, other style) where that style does not set it, and left out when every style sets it. The viewer
draws defaults -> selected style (parser hmi style) -> stored values (docs\\spec\\hmi-styles.md).

Usage: python -I tools\\hmi_defaults.py [samples folder]
"""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "parser"))
from openpedata.model import Model  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SAMPLES = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "samples")
SRC = os.path.join(SAMPLES, "165_hmi_item_defaults_truth", "HMI_1", "screenItems")
OUT = os.path.join(ROOT, "ui", "OpenPEData.Viewer", "HmiDefaults.json")
# itemGet bookkeeping and identity, not presentation
DROP = {"Name", "Left", "Top", "screenName", "type", "itemType", "compositions", "unsetProperties", "durationMs",
        "queueMs", "defaulted", "dynamizations", "dynamizationSummary", "events", "ToolTipText", "AlternateText",
        "Authorization", "TabIndex", "RequireExplicitUnlock", "CurrentQuality", "Enabled", "ShowFocusVisual"}


def style_of(sample):
    """Selected style values {type: values} of HMI_1 of a sample (parser output)."""
    def find(o):
        if isinstance(o, dict):
            if "style" in o and "screens" in o and o.get("name") == "HMI_1":
                return o
            o = list(o.values())
        if isinstance(o, list):
            for v in o:
                r = find(v)
                if r:
                    return r
        return None
    sw = find(Model(os.path.join(SAMPLES, sample)).to_dict())
    return ((sw or {}).get("style") or {}).get("values") or {}


def paths(o, p=()):
    for k, v in o.items():
        if isinstance(v, dict) and k != "Text":
            yield from paths(v, p + (k,))
        else:
            yield p + (k,)


def get(o, path):
    for k in path:
        if not isinstance(o, dict) or k not in o:
            return KeyError
        o = o[k]
    return o


def put(o, path, v):
    for k in path[:-1]:
        o = o.setdefault(k, {})
    if v is KeyError:
        o.pop(path[-1], None)
    else:
        o[path[-1]] = v


def others():
    """[(style values, {type: Openness attributes, "screen": screen attributes})] of 166 and 167."""
    out = []
    for sample in ("166_hmi_style_dark", "167_hmi_style_bright"):
        f = os.path.join(SAMPLES, sample + "_truth", "HMI_1", "screens_full.json")
        if not os.path.exists(f):
            continue
        with open(f, encoding="utf-8-sig") as fh:
            objs = json.load(fh)["objects"]
        attrs = {"screen": objs[0]["attributes"]}
        for o in objs:
            for i in o.get("items", []):
                attrs.setdefault(i["type"], i["attributes"])
        out.append((style_of(sample), attrs))
    return out


def unstyle(values, typ, cls, base, alts):
    """Replace the values the base style sets (style entry cls) by those of a sample whose style leaves them unset;
    left out when no such sample exists. Returns the number of replaced and removed values."""
    styled = [pth for pth in paths(base.get(cls, {})) if pth[0] not in ("StyleItemClass", "RenderingTemplate")]
    n = [0, 0]
    for pth in sorted(styled):
        if get(values, pth) is KeyError:
            continue
        v = KeyError
        for st, attrs in alts:
            if get(st.get(cls, {}), pth) is KeyError and typ in attrs:
                v = get(attrs[typ], pth)
                if v is not KeyError:
                    break
        put(values, pth, v)
        n[v is KeyError] += 1
    return n


def main():
    items, screen = {}, None
    for fn in sorted(os.listdir(SRC)):
        with open(os.path.join(SRC, fn), encoding="utf-8-sig") as f:
            d = json.load(f)
        it = d.get("item") or {}
        t = it.get("itemType") or it.get("type")
        if not t:
            continue
        items[t] = {k: v for k, v in sorted(it.items()) if k not in DROP}
        if screen is None:
            screen = {k: v for k, v in sorted(((d.get("screen") or {}).get("attributes") or {}).items())}
    base, alts = style_of("165_hmi_item_defaults"), others()
    n = [0, 0]
    for t, v in [("screen", screen)] + list(items.items()):
        r = unstyle(v, t, "HmiScreen" if t == "screen" else v.get("StyleItemClass") or t, base, alts)
        n = [n[0] + r[0], n[1] + r[1]]
    print("style values of 165: %d replaced from 166/167, %d left out" % tuple(n))
    out = {"source": "samples\\165_hmi_item_defaults (TiaHmiUnified itemGet detail=full, MTP700 Unified Comfort, TIA V21); "
                     "values of its style (ExtendedStyle) replaced from 166/167 or left out: object defaults without style",
           "screen": screen, "items": items}
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("%d item types -> %s" % (len(items), OUT))


if __name__ == "__main__":
    main()
