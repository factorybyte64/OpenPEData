"""WinCC Unified styles from the PEData: system styles per HMI device and their values per object type.

Usage: python -I tools\\hmi_styles.py <project> [--style Name] [--type HmiButton] [--json out.json]
<project>: project folder, .ap21 or .zap21. Prints the style settings per device (selected style) and, with --style or
--type, the decoded style values. Format: docs\\spec\\hmi-styles.md.
"""
import argparse
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata import project as projmod  # noqa: E402
from openpedata.styles import META, settings, strip_meta, styles, templates  # noqa: E402,F401
from openpedata.styles import decode_style_value, unwrap  # noqa: E402,F401  (re-exported for scripts)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("project")
    ap.add_argument("--style", help="style name prefix, e.g. FlatStyle_Dark")
    ap.add_argument("--type", help="object type, e.g. HmiButton")
    ap.add_argument("--meta", action="store_true", help="keep _Guid/_TypeId/IsDefault/StyleItemType")
    ap.add_argument("--json")
    ap.add_argument("--templates", metavar="DIR", help="write the rendering templates (SVG) of the selected style of "
                    "the first device to DIR (Siemens content: keep outside the repository)")
    a = ap.parse_args()
    P = projmod.Project(a.project)
    st = styles(P)
    first_sel = None
    for k, dev, sel, sys_ in settings(P):
        target = P.targets(k, "Target")
        first_sel = first_sel or sel
        print(f"{k} device {dev} ({P.name(target[0]) if target else '?'}): selected {st[sel][0] if sel in st else sel}, "
              f"system styles {', '.join(st[s][0] for s in sys_ if s in st)}")
    if a.templates and first_sel:
        os.makedirs(a.templates, exist_ok=True)
        for name, svg in templates(P, first_sel).items():
            with open(os.path.join(a.templates, name + ".svg"), "wb") as f:
                f.write(svg or b"")
            print(f"  {name}: {len(svg or b'')} bytes")
    if not (a.style or a.type or a.json):
        return
    dump = {}
    for k, (name, sysname, values) in st.items():
        if a.style and not name.startswith(a.style):
            continue
        sel = {t: (v if a.meta else strip_meta(v)) for t, v in values.items() if not a.type or t == a.type}
        dump.setdefault(name, sel)  # one copy per name (devices share the same system styles)
    if a.json:
        with open(a.json, "w", encoding="utf-8") as f:
            json.dump(dump, f, indent=1, ensure_ascii=False)
    else:
        print(json.dumps(dump, indent=1, ensure_ascii=False))


if __name__ == "__main__":
    main()
