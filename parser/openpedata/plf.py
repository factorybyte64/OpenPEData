"""System\\PEData.plf: append-only record log.

File = 64-byte header + 1 byte, SHA-256 over those 65 bytes, 0xFF (98 bytes), then records to EOF:
  [u32 len][len-4 bytes, last byte 0xFF][SHA-256 over the len bytes]
Every record starts with a 12-byte key: u16 typeLo, u16 typeHi, u64 object id.
The current state of an object is the last record with its key (log order).
"""
import hashlib
import struct

RECORDS_START = 98


class Record:
    __slots__ = ("offset", "length", "hash_ok", "type_lo", "type_hi", "oid", "data")

    def __init__(self, offset, length, hash_ok, data):
        self.offset, self.length, self.hash_ok, self.data = offset, length, hash_ok, data
        self.type_lo, self.type_hi, self.oid = struct.unpack_from("<HHQ", data, 4)

    @property
    def type_id(self):
        """Metamodel id of the record's class: (typeHi << 16) | typeLo."""
        return (self.type_hi << 16) | self.type_lo

    @property
    def key(self):
        return "%04x:%04x:%x" % (self.type_lo, self.type_hi, self.oid)

    @property
    def is_store(self):
        """Store bookkeeping records (type hi 0x0007: metamodel, commit, close, ...)."""
        return self.type_hi == 0x0007


def read_records(buf, verify=True):
    recs = []
    o = RECORDS_START
    n = len(buf)
    while o + 4 <= n:
        (ln,) = struct.unpack_from("<I", buf, o)
        if ln < 17 or o + ln + 32 > n:
            break
        data = bytes(buf[o:o + ln])
        ok = (not verify) or hashlib.sha256(data).digest() == buf[o + ln:o + ln + 32]
        recs.append(Record(o, ln, ok, data))
        o += ln + 32
    return recs


def latest(recs):
    """key -> last record with that key, in log order of first appearance."""
    d = {}
    for r in recs:
        d[r.key] = r
    return d
