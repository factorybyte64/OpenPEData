"""Unit tests with synthetic byte strings. python parser/tests/test_units.py"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from openpedata import attrs, interface  # noqa: E402

# WebApplicationData.IWebApplicationExpandoData (simple expando) with one bool stored in the value table itself
WEB_APP = bytes.fromhex(
    "4e0000008a0000000100000001004a000000010000801a00000049007300440065006600610075006c0074005700650062004100"
    "700070006c0069006300610074006900"
    "6f006e00000001000000" + "00" * 60)

# IIdentPartData.PayLoad of a large block (synthetic header): kind 4, 159721 bytes in 39 pages, pages in the
# store records 0013:0007:100 (pages 0-31) and 0013:0007:101 (pages 32-38)
KIND4_EXTERNAL = bytes.fromhex(
    "04e96f0200000000000000000000104000000003ffffffff7f00000000000000"
    "130007000001000000000000130007000101000000000000")


def _paged(content, page=4096):
    """Kind 1 blob (zlib pages) of content."""
    import struct
    import zlib
    pages = (len(content) + page - 1) // page
    words = [0] * (pages // 32 + 1)
    for i in range(pages):
        words[i // 32] |= 1 << (i % 32)
    out = bytearray(b"\x01" + struct.pack("<QIHI", len(content), 0, page, pages) + bytes([len(words)]))
    out += struct.pack("<%dI" % len(words), *words)
    for i in range(pages):
        z = zlib.compress(content[i * page:(i + 1) * page].ljust(page, b"\0"))
        n = len(z)
        out += bytes([(n & 0x7F) | 0x80, n >> 7]) + z  # varint, n < 16384
    return bytes(out)


def _store_record(oid, chunk):
    """BLOB_STORE_TYPE store record: u32 length, key, u32 0, u32 3, u32 0, GUID, u32 n, chunk blob, 0xFF."""
    import struct
    body = struct.pack("<HHQIII", 0x13, 7, oid, 0, 3, 0) + b"\x11" * 16 + struct.pack("<I", len(chunk)) + chunk + b"\xff"
    return struct.pack("<I", len(body) + 4) + body


def check_kind4(fails):
    import struct
    keys = attrs.external_refs(KIND4_EXTERNAL)
    if keys != ["0013:0007:100", "0013:0007:101"]:
        fails.append("kind 4 external refs: %r" % keys)
    total = struct.unpack_from("<Q", KIND4_EXTERNAL, 1)[0]
    content = bytes((i * 7 + i // 4096) & 0xFF for i in range(total))
    chunk = 32 * 4096
    store = {"0013:0007:100": _store_record(0x100, _paged(content[:chunk])),
             "0013:0007:101": _store_record(0x101, _paged(content[chunk:]))}
    if attrs.decode_blob(KIND4_EXTERNAL, store.get) != content:
        fails.append("kind 4 external content")
    if attrs.decode_blob(attrs.StoredBlob(KIND4_EXTERNAL, store.get)) != content:
        fails.append("kind 4 external content through StoredBlob")
    for name, st in (("no store", None), ("missing record", {k: v for k, v in store.items() if k.endswith("b2")}.get)):
        try:
            attrs.decode_blob(KIND4_EXTERNAL, st)
            fails.append("kind 4 external, %s: no BlobError" % name)
        except attrs.BlobError:
            pass
    if attrs.decode_blob(b"\x04\x03abc") != b"abc" or attrs.decode_blob(b"\x04\x09abc") is not None:
        fails.append("kind 4 varint + bytes")


def main():
    fails = []
    vals, problems = attrs.decode_simple_expando(WEB_APP)
    if vals != {"IsDefaultWebApplication": True} or problems:
        fails.append("inline bool in simple expando: %r %r" % (vals, problems))
    t = interface.Interface._drop_neutral({"en-US": "x", "65535": "x"})
    if t != {"en-US": "x"} or interface.Interface._drop_neutral({"65535": "y"}) != {"65535": "y"}:
        fails.append("neutral culture of interface comments: %r" % t)
    if attrs.expando_inline(0x12, 11135) != 11135 or attrs.expando_inline(7, 0x41700000) != 15.0:
        fails.append("inline expando codes 0x12 / 7")
    check_kind4(fails)
    print("test_units: %s" % ("OK" if not fails else "FAIL"))
    for f in fails:
        print("  FAIL", f)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
