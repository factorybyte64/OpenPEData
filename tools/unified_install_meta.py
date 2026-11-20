"""Read-only inventory of the metamodel package files (*.mpk) of a TIA Portal installation, HMI/Unified part.

Usage: python -I tools\\unified_install_meta.py "C:\\Program Files\\Siemens\\Automation\\Portal V21" [--all]
Every *.mpk below the installation is a zip (entries stored, compress_type 0). The entries are not plain
XML and not a zlib/deflate stream at any offset; they start with 78 7d 03 ee 00 ef and have high entropy, so
only file names, entry names, sizes, header bytes and entropy are reported. Nothing is copied or decoded.
Output (TSV): mpk path relative to the installation, entry name, size, first 6 bytes hex, entropy bits/byte.
Without --all only mpk files or entries whose names match Hmi/Unified/Screen/Faceplate/WinCC are listed.
"""
import collections
import math
import os
import re
import sys
import zipfile

PAT = re.compile(r"Hmi|Unified|Screen|Faceplate|WinCC", re.I)


def entropy(b):
    if not b:
        return 0.0
    c = collections.Counter(b)
    return -sum(n / len(b) * math.log2(n / len(b)) for n in c.values())


def main():
    root = sys.argv[1]
    show_all = "--all" in sys.argv
    headers = collections.Counter()
    n_mpk = n_entries = 0
    for dirpath, _, files in os.walk(root):
        for f in files:
            if not f.lower().endswith(".mpk"):
                continue
            path = os.path.join(dirpath, f)
            rel = os.path.relpath(path, root)
            try:
                z = zipfile.ZipFile(path)
            except (zipfile.BadZipFile, OSError):
                print(f"{rel}\t-\tnot a zip")
                continue
            n_mpk += 1
            with z:
                for info in z.infolist():
                    data = z.read(info)
                    n_entries += 1
                    headers[data[:6].hex()] += 1
                    if show_all or PAT.search(f) or PAT.search(info.filename):
                        print(f"{rel}\t{info.filename}\t{info.file_size}\t{data[:6].hex()}\t{entropy(data):.2f}")
    print(f"# mpk files: {n_mpk}, entries: {n_entries}; entry headers: "
          + ", ".join(f"{h} x{n}" for h, n in headers.most_common(5)))


if __name__ == "__main__":
    main()
