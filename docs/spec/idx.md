# PEData.idx (established)

Header like plf (DWORD 0 = 0x80), 98 B incl. hash and FF. After that a sequence of:
- entry 38 B: `[Key 12 B = record key][object GUID 16][u8 0][u32 plf offset to record start][i32: folder -1, archive 0][u8 0]`
- unhashed commit entries (len 421, with `$$COMMIT$`), per transaction like plf
- final entry key 0015:0007:20 with offset FFFFFFFF.

Example @98: `16 00 07 00 18 00 00 00 00 00 00 00 | 6b 1a a7 31 … 25 15 | 00 | e7 00 00 00 (=231) | ff ff ff ff | 00`.
000_empty folder: 368 entries = all plf records except the two ##CLOSE##; old and new versions both referenced.

> Status 5f part 1 (2026-10-20): complete description and correction of the i32 field (class code) in [derived-stores.md](derived-stores.md).
