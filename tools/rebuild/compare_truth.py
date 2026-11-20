"""Compare the regenerated ground truth of rebuilt samples with the old one, ignoring volatile values (timings, dates,
GUIDs, paths, export timestamps). Prints per sample OK or the differing files with the first differences.

    python -I tools\\rebuild\\compare_truth.py <stage>\\out [sample ...]"""
import difflib
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SAMPLES = os.path.join(ROOT, "samples")
VOLATILE_KEYS = {"durationMs", "queueMs", "defaulted", "exportMs", "parseMs", "cache", "generatedUtc", "fullResultPath",
                 "elapsedMs", "path", "projectPath", "targetPath", "targetDirectory", "files", "jobId"}
RX = [
    (re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z?"), "<date>"),
    (re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"), "<guid>"),
    (re.compile(r"[A-Za-z]:\\\\?[^\"<>|\r\n]*"), "<path>"),
]


def norm_json(o):
    if isinstance(o, dict):
        return {k: norm_json(v) for k, v in o.items() if k not in VOLATILE_KEYS}
    if isinstance(o, list):
        return [norm_json(x) for x in o]
    if isinstance(o, str):
        for rx, rep in RX:
            o = rx.sub(rep, o)
    return o


def lines(path):
    with open(path, "rb") as f:
        raw = f.read()
    if raw[:3] == b"\xef\xbb\xbf":
        raw = raw[3:]
    try:
        txt = raw.decode("utf-8")
    except UnicodeDecodeError:
        return ["<binary %d bytes>" % len(raw)]
    if path.endswith(".json"):
        try:
            return json.dumps(norm_json(json.loads(txt)), indent=1, sort_keys=True, ensure_ascii=False).splitlines()
        except ValueError:
            pass
    out = []
    for ln in txt.replace("\r\n", "\n").split("\n"):
        for rx, rep in RX:
            ln = rx.sub(rep, ln)
        out.append(ln)
    return out


def compare(name, newroot, verbose=3):
    old, new = os.path.join(SAMPLES, name + "_truth"), os.path.join(newroot, name + "_truth")
    if not os.path.isdir(old):
        return []
    rel = lambda r: sorted(os.path.relpath(os.path.join(dp, f), r).replace("\\", "/") for dp, _, fs in os.walk(r) for f in fs)  # noqa: E731
    a, b = set(rel(old)), set(rel(new)) if os.path.isdir(new) else set()
    res = ["missing: " + x for x in sorted(a - b)] + ["extra: " + x for x in sorted(b - a)]
    for f in sorted(a & b):
        if f.endswith((".xlsx", ".jpg", ".png", ".dat")):
            continue
        la, lb = lines(os.path.join(old, f)), lines(os.path.join(new, f))
        if la != lb:
            d = [x for x in difflib.unified_diff(la, lb, lineterm="", n=0) if not x.startswith(("---", "+++", "@@"))]
            res.append("differs: %s (%d lines) %s" % (f, len(d), " | ".join(x[:160] for x in d[:verbose])))
    return res


def main():
    newroot = sys.argv[1]
    names = sys.argv[2:] or sorted(x[:-6] for x in os.listdir(newroot) if x.endswith("_truth"))
    bad = 0
    for n in names:
        r = compare(n, newroot)
        print("%-32s %s" % (n, "OK" if not r else "%d differences" % len(r)))
        for x in r:
            print("    " + x)
        bad += bool(r)
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
