# Derived stores: PEData.idx, XRef.db, Vci.db, IM\SearchIndex (5f part 1)

Source: device session, commit c5723ad (`docs/spec/derived-stores.md`, reader `parser/openpedata/pidx.py`, `parser/openpedata/xref.py`), as of 2026-10-20 18:30. Checked on all 80 folder samples.

## PEData.idx
- Header = plf header (DWORD 0 = 0x80, same GUID, SHA-256 over 65 B).
- Per plf record **exactly one** 38-byte entry: `Key 12 B | record GUID 16 B | plf offset u32 | class code i32 (= plf record byte 16..19) | Flags`. Flag 4 = deletion: tombstone GUID, offset 0xFFFFFFFF or copy of the last live entry.
- The order is not the log order (per commit interval, store records first); mapping to the plf record is therefore via the offset.
- After each commit a 421-byte copy of the `$$COMMIT$` record: chain hash identical, +32 bytes, 256 bytes = RSA signature with the project key (correction 5f remainder, commit 73757e5: decrypted with the public plf key it yields PKCS#1 v1.5 with SHA-256 DigestInfo, 100 % in 000, 002, 037, 039; which bytes are signed is open, only relevant for writing/4e). After that, session marker `0015:0007:20`.
- Hits: entry count = record count; all offsets, GUIDs and classes match (e.g. 759/759 in 005, 114108/114108 in 031).
- In the `.zap21` the folder's idx sits next to the **compacted** plf; the offsets do not match there (TIA rebuilds the idx on open). `--verify` therefore checks the idx only in the folder.
- Correction to [idx.md](idx.md): the i32 field is the class code, not "folder -1 / archive 0".

## Savepoints
plf record `1006:0001:449` (project history) holds the GUIDs `XRef%<guid>` and `IM\SearchIndex … SaveCommit_<guid>`. They correspond to `XRef.db info.savepointID` and to the user data of Lucene `segments_N` respectively (70/81 and 64/81 equal; otherwise the store is one version behind the plf).

## XRef.db (SQLite)
- `parts` = `'OG'` + plf record key (in 036_tech all 191 parts resolve to plf records, 16 of them to deleted objects).
- `objs`/`rels` with type codes: 0x1000 OB, 0x1010 FC, 0x1020 FB, 0x1060 DB, 0x1065 instruction, 0x1070 TO, 0x1080 tag, 0x10a0 constant, 0x10b0 UDT, 0x1100 label, 0x1110/0x1120/0x1125 member.
- `addrs` = absolute tag addresses (range 1 = I, 2 = Q, 3 = M, plus bit range); matches `ITagAddress.LogicalAddress` in all samples (test).
- Open: `relationInfo`, `location` only partially decoded.

## Vci.db, IM\SearchIndex
- Vci.db: empty in all samples, schema only; the MCP server has no VCI operations. Clarification only if the user attaches a VCI workspace in TIA.
- IM\SearchIndex: Lucene.NET, fields Base64, derived search index, not decoded.

## Parser
`--verify` additionally reports idx consistency and savepoints (exit code unchanged).
