"""Write G_WMF.wmf: a placeable Windows metafile (120x80) with the same drawing as the other C05 test graphics.

    python mkwmf.py <out_dir>
"""
import struct
import sys
from pathlib import Path

recs = []


def rec(func, *params):
    data = b''.join(struct.pack('<h', p) for p in params)
    recs.append(struct.pack('<IH', (6 + len(data)) // 2, func) + data)


def brush(r, g, b):
    data = struct.pack('<HBBBBH', 0, r, g, b, 0, 0)
    recs.append(struct.pack('<IH', (6 + len(data)) // 2, 0x02FC) + data)


def pen(w, r, g, b):
    data = struct.pack('<HhhBBBB', 0, w, 0, r, g, b, 0)
    recs.append(struct.pack('<IH', (6 + len(data)) // 2, 0x02FA) + data)


rec(0x020B, 0, 0)          # SETWINDOWORG
rec(0x020C, 80, 120)       # SETWINDOWEXT
brush(255, 255, 240); rec(0x012D, 0)
pen(3, 139, 0, 0); rec(0x012D, 1)
rec(0x041B, 78, 118, 2, 2)     # RECTANGLE frame
brush(70, 130, 180); rec(0x012D, 2)
pen(1, 70, 130, 180); rec(0x012D, 3)
rec(0x041B, 14, 120, 0, 0)     # RECTANGLE header bar
brush(255, 165, 0); rec(0x012D, 4)
pen(1, 255, 165, 0); rec(0x012D, 5)
rec(0x0418, 70, 112, 34, 76)   # ELLIPSE
pen(2, 0, 128, 0); rec(0x012D, 6)
rec(0x0214, 76, 4)             # MOVETO
rec(0x0213, 16, 116)           # LINETO
recs.append(struct.pack('<IH', 3, 0))  # EOF
body = b''.join(recs)
hdr = struct.pack('<HHHIHIH', 1, 9, 0x300, (18 + len(body)) // 2, 7, max(len(r) // 2 for r in recs), 0)
ph = struct.pack('<IHhhhhHI', 0x9AC6CDD7, 0, 0, 0, 120, 80, 96, 0)
cs = 0
for i in range(0, 20, 2):
    cs ^= struct.unpack('<H', ph[i:i + 2])[0]
ph += struct.pack('<H', cs)
Path(sys.argv[1], 'G_WMF.wmf').write_bytes(ph + hdr + body)
