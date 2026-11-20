"""Replaces non-ASCII characters in C# sources by backslash-x escapes (sources stay ASCII)."""
import pathlib
import sys

BS = chr(92)
for p in pathlib.Path(sys.argv[1]).rglob("*.cs"):
    t = p.read_text(encoding="utf-8")
    n = "".join(c if ord(c) < 128 else BS + "x%04x" % ord(c) for c in t)
    if n != t:
        p.write_text(n, encoding="utf-8")
        print("escaped", p)
