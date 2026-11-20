"""Writes tests/OpenPEData.Tests/golden.json: expected results of the Python semantics the .NET port reproduces
(float repr, round, repr, json.dumps, UTF-8 / UTF-16 decoding with replacement, zlib results and error messages,
SQLite table dumps of XRef.db files, datetime ticks). Run with the Python the parser uses:
  python dotnet/tools/make_golden.py"""
import base64
import datetime
import hashlib
import json
import math
import os
import random
import sqlite3
import struct
import sys
import tempfile
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "..", "tests", "OpenPEData.Tests", "golden.json")
rnd = random.Random(20261101)


def b64(b):
    return base64.b64encode(b).decode("ascii")


def floats():
    vals = [0.0, -0.0, 0.1, 0.2, 0.30000000000000004, 1.0, -1.5, 100.0, 1e-05, 0.0001, 0.001, 123.456, 1e15, 1e16, 1.5e16,
            9999999999999998.0, 1e22, 1e100, 5e-324, 2.2250738585072014e-308, 1.7976931348623157e308, 3.14159, 2.5, 1 / 3,
            12345678.9, 0.000123456, float("inf"), float("-inf")]
    for _ in range(300):
        vals.append(struct.unpack("<d", struct.pack("<Q", rnd.getrandbits(64)))[0])
        vals.append(struct.unpack("<f", struct.pack("<I", rnd.getrandbits(32)))[0])
        vals.append(rnd.uniform(-1e6, 1e6))
        vals.append(round(rnd.uniform(0, 100), rnd.randint(0, 8)))
    out = []
    for v in vals:
        if math.isnan(v):
            continue
        out.append({"bits": struct.unpack("<q", struct.pack("<d", v))[0], "repr": repr(v), "json": json.dumps(v)})
    return out


def rounds():
    out = []
    vals = [0.5, 1.5, 2.5, 0.0078125, 0.1, 0.123456789, 1 / 128, 3 / 1024, 2.675, 1e-7, 123456.7890125]
    for _ in range(300):
        vals.append(struct.unpack("<f", struct.pack("<I", rnd.getrandbits(32)))[0])
        vals.append(rnd.uniform(-10, 10))
    for v in vals:
        if math.isnan(v) or math.isinf(v):
            continue
        out.append({"bits": struct.unpack("<q", struct.pack("<d", v))[0], "n": 6, "result": repr(round(v, 6))})
    return out


def reprs():
    strs = ["", "abc", "it's", 'say "hi"', "both ' and \"", "tab\tnl\nret\r", "back" + chr(92) + "slash", "\x00\x01\x1f\x7f",
            "\x80\x9f\xa0\xad", "umlaut äöü ß", "  ", "​﻿", "", "\U0001F600 emoji",
            "中文", "́ combining", "�"]
    return [{"s": s, "repr": repr(s)} for s in strs] + [{"b": b64(b), "repr": repr(b)} for b in
                                                         (b"", b"abc", b"it's", b"\x00\xff'\"\\t\n\r", bytes(range(256)))]


def jsons():
    vals = [None, True, False, 0, -5, 2 ** 64 - 1, 1.0, 1e-7, "x\"" + chr(92) + "\n\r\t\b\f\x01\x7f\u00e4\U0001F600", [], {}, [1, [2, []], {}],
            {"a": {"b": [1, 2, {"c": None}]}, "d": "e"}, {"1": 1, "k": [True, False]}]
    out = []
    for v in vals:
        out.append({"value": v, "indent2": json.dumps(v, indent=2, ensure_ascii=False), "indent2ascii": json.dumps(v, indent=2),
                    "flat": json.dumps(v, ensure_ascii=False)})
    out.append({"float": [float("nan"), float("inf"), float("-inf")],
                "flat": json.dumps([float("nan"), float("inf"), float("-inf")])})
    return out


def decodes():
    cases = [b"abc", b"\xc3\xa4", b"\xc3", b"\xc3\x28", b"\xe2\x82", b"\xe2\x82\x28", b"\xf0\x9f\x98", b"\xf0\x9f\x98\x80",
             b"\xed\xa0\x80", b"\xc0\xaf", b"\xf5\x80\x80\x80", b"\xff\xfe", b"\x80abc\xbf", b"\xef\xbb\xbfBOM",
             b"a\xe0\x80\xafb", b"\xf4\x90\x80\x80", b"\xe0\xa0"]
    for _ in range(200):
        cases.append(bytes(rnd.getrandbits(8) for _ in range(rnd.randint(1, 12))))
    out = []
    for c in cases:
        try:
            strict = c.decode("utf-8")
        except UnicodeDecodeError:
            strict = None
        out.append({"b": b64(c), "utf8": c.decode("utf-8", errors="replace"), "utf8strict": strict,
                    "utf8sig": c.decode("utf-8-sig", errors="replace"), "utf16le": c.decode("utf-16-le", errors="replace"),
                    "ascii": c.decode("ascii", errors="replace")})
    return out


def zlibs():
    out = []
    for level in range(10):
        for size in (0, 1, 100, 4096, 70000):
            data = bytes(rnd.getrandbits(8) if rnd.random() < 0.3 else 65 + (i % 7) for i in range(size))
            out.append({"z": b64(zlib.compress(data, level)), "sha256": hashlib.sha256(data).hexdigest(), "error": None})
    good = zlib.compress(b"hello world " * 50)
    bad = [good[:-3], good[:5], b"xx" + good, good[:-1] + bytes([good[-1] ^ 1]), bytes([good[0], good[1]]) + b"\xff" * 20,
           b"\x78\x9c\x07\x00", b"\x78\x9c" + bytes([0b00000110]) + b"\x00" * 8, good + b"trailing"]
    for _ in range(100):
        b = bytearray(good)
        for _ in range(rnd.randint(1, 4)):
            b[rnd.randrange(2, len(b))] = rnd.getrandbits(8)
        bad.append(bytes(b))
    for b in bad:
        try:
            r = zlib.decompress(b)
            out.append({"z": b64(b), "sha256": hashlib.sha256(r).hexdigest(), "error": None})
        except zlib.error as e:
            out.append({"z": b64(b), "sha256": None, "error": str(e)})
    return out


def sqlites():
    out = []
    import zipfile
    for rel in ("samples/F01_hmi_allobjects.zap21", "samples/031_lad_all.zap21", "samples/037_lad_xml.zap21", "samples/025_fb_idb.zap21"):
        p = os.path.join(ROOT, rel)
        if not os.path.exists(p):
            continue
        with tempfile.TemporaryDirectory() as t:
            fn = os.path.join(t, "x.db")
            open(fn, "wb").write(zipfile.ZipFile(p).read("XRef/XRef.db"))
            con = sqlite3.connect(fn)
            for table in ("info", "parts", "objs", "rels", "addrs"):
                rows = con.execute("select * from %s" % table).fetchall()
                canon = json.dumps([[("hex:" + v.hex()) if isinstance(v, bytes) else v for v in r] for r in rows])
                out.append({"db": rel, "table": table, "rows": len(rows), "sha256": hashlib.sha256(canon.encode()).hexdigest()})
            con.close()
    return out


def ticks():
    out = []
    vals = [0, 1, 10, 9999999, 630822816000000000, 638000000000000000 | (1 << 62), 3155378975999999999,
            3155378975999999999 + 10, (1 << 62) - 1, -1]
    for _ in range(100):
        vals.append(rnd.getrandbits(63) - (1 << 62))
        vals.append(rnd.randrange(0, 3155378975999999999))
    for v in vals:
        kind = v >> 62
        t = v & 0x3FFFFFFFFFFFFFFF
        try:
            dt = datetime.datetime(1, 1, 1) + datetime.timedelta(microseconds=t // 10)
            iso = dt.isoformat() + ("Z" if kind == 1 else "")
        except OverflowError:
            iso = None
        out.append({"v": v, "iso": iso})
    return out


def main():
    g = {"floats": floats(), "rounds": rounds(), "reprs": reprs(), "jsons": jsons(), "decodes": decodes(), "zlib": zlibs(),
         "sqlite": sqlites(), "ticks": ticks(), "python": sys.version}
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(g, f, indent=1, ensure_ascii=True)
    print("wrote", OUT, {k: len(v) for k, v in g.items() if isinstance(v, list)})


if __name__ == "__main__":
    main()
