"""Lists the parser inputs of the differential test: every .zap21 / .zal21 archive and every folder with System/PEData.plf
below samples/ and analysis/real/ (one per line, relative to the repository root), without samples/local_gsd."""
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def inputs():
    out = []
    for base in ("samples", os.path.join("analysis", "real")):
        b = os.path.join(ROOT, base)
        if not os.path.isdir(b):
            continue
        for dirpath, dirnames, filenames in os.walk(b):
            # samples/local_gsd: local samples with third-party GSDML, not part of the baseline (see .gitignore)
            dirnames[:] = sorted(d for d in dirnames if d != "local_gsd")
            if os.path.isfile(os.path.join(dirpath, "System", "PEData.plf")):
                out.append(os.path.relpath(dirpath, ROOT).replace("\\", "/"))
                dirnames[:] = []
                continue
            for f in sorted(filenames):
                if f.lower().endswith((".zap21", ".zal21")):
                    out.append(os.path.relpath(os.path.join(dirpath, f), ROOT).replace("\\", "/"))
    return sorted(set(out))


if __name__ == "__main__":
    sys.stdout.write("\n".join(i.replace("\\", "/") for i in inputs()) + "\n")
