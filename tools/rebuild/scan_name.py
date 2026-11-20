"""Search project folders and TIA archives (.zap21/.zal21, ZIP) for a name, case-insensitive, as ASCII/UTF-8 and UTF-16LE,
also inside zlib streams of the files.
Archive entries are decompressed; nested ZIPs are searched too.

    python -I tools\\rebuild\\scan_name.py <old-user-name> samples <stage>\\out
Exit code 1 when the name was found."""
import io
import os
import sys
import zipfile
import zlib


ZLIB_HEADS = tuple(bytes((0x78, x)) for x in (0x01, 0x5E, 0x9C, 0xDA))


def inflated(data):
    """Every zlib stream found in the data, decompressed (TIA stores object data of the .plf files compressed)."""
    for head in ZLIB_HEADS:
        i = data.find(head)
        while i >= 0:
            try:
                d = zlib.decompressobj().decompress(data[i:i + (64 << 20)], 256 << 20)
            except zlib.error:
                d = b""
            if d:
                yield d
            i = data.find(head, i + 1)


def hit(data, pats):
    low = data.lower()
    if any(p in low for p in pats):
        return True
    return any(p in d.lower() for d in inflated(data) for p in pats)


def scan_zip(data, pats, label, found):
    try:
        z = zipfile.ZipFile(io.BytesIO(data))
    except zipfile.BadZipFile:
        return
    for n in z.namelist():
        d = z.read(n)
        if hit(n.encode("utf-8", "replace"), pats) or hit(d, pats):
            found.append("%s!%s" % (label, n))
        elif d[:2] == b"PK":
            scan_zip(d, pats, "%s!%s" % (label, n), found)


def main():
    name = sys.argv[1].lower()
    pats = [name.encode("ascii"), name.encode("utf-16-le")]
    found = []
    for root in sys.argv[2:]:
        if os.path.isfile(root):
            walk = [(os.path.dirname(root), None, [os.path.basename(root)])]
        else:
            walk = os.walk(root)
        for dp, _, fns in walk:
            if ".git" in dp.split(os.sep):
                continue
            for fn in fns:
                p = os.path.join(dp, fn)
                try:
                    with open(p, "rb") as f:
                        d = f.read()
                except OSError:
                    continue
                if hit(fn.encode("utf-8", "replace"), pats) or hit(d, pats):
                    found.append(p)
                elif d[:2] == b"PK":
                    scan_zip(d, pats, p, found)
    for f in found:
        print(f)
    print("%d hits" % len(found))
    sys.exit(1 if found else 0)


if __name__ == "__main__":
    main()
