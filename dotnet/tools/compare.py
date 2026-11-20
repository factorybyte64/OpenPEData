"""Compares two output trees of run_cli.py (reference = Python, candidate = .NET) byte for byte.

  python compare.py <reference dir> <candidate dir> [--details N]
Prints per mode the number of equal / different inputs and, for the first N differences, the first differing line.
Exit code 0 only if every stdout, stderr and exit code is identical."""
import os
import sys


def first_diff(a, b):
    la, lb = a.split(b"\n"), b.split(b"\n")
    for i, (x, y) in enumerate(zip(la, lb)):
        if x != y:
            return i + 1, x[:300], y[:300]
    return min(len(la), len(lb)) + 1, b"<len %d>" % len(la), b"<len %d>" % len(lb)


def main(argv):
    ref, cand = argv[0], argv[1]
    details = int(argv[argv.index("--details") + 1]) if "--details" in argv else 5
    total_bad = 0
    for mode in sorted(os.listdir(ref)):
        if not os.path.isdir(os.path.join(ref, mode)) or not os.path.isdir(os.path.join(cand, mode)):
            continue
        names = sorted({n.rsplit(".", 1)[0] for n in os.listdir(os.path.join(cand, mode))})
        bad = []
        for n in names:
            for ext in (".out", ".err", ".rc"):
                pa, pb = os.path.join(ref, mode, n + ext), os.path.join(cand, mode, n + ext)
                a = open(pa, "rb").read() if os.path.exists(pa) else b"<missing>"
                b = open(pb, "rb").read() if os.path.exists(pb) else b"<missing>"
                if a != b:
                    bad.append((n, ext, a, b))
                    break
        total_bad += len(bad)
        print("%-12s %4d equal, %4d different" % (mode, len(names) - len(bad), len(bad)))
        for n, ext, a, b in bad[:details]:
            line, x, y = first_diff(a, b)
            print("   %s%s line %d\n     py:  %r\n     net: %r" % (n, ext, line, x, y))
    return 1 if total_bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
