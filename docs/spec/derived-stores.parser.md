# Derived stores: PEData.idx, XRef.db, Vci.db, IM\SearchIndex, savepoint link

Status: V21 Update 3. Evidence: 80 folder samples and their .zap21 (`parser/tests/test_samples.py`, check `check_stores`;
implementation `parser/openpedata/pidx.py`, `xref.py`). Everything here is **derived** (can be rebuilt from the plf). The plf
(`System\PEData.plf`) alone remains authoritative; the parser output does not depend on any of these stores.

## System\PEData.idx

Header: 98 bytes with the structure of the plf header (same project GUID, magic `0x80` instead of `0x40`; SHA-256 over bytes 0..64 in
bytes 65..96, byte 97 = 0xFF). After that, all little endian:

| Part | Size | Content |
| --- | --- | --- |
| Entry | 38 | `u16 typeLo, u16 typeHi, u64 object ID` (= record key), `16 bytes record GUID` (plf record bytes 28..43), `u8 0`, `u32 plf offset of the record`, `i32 class code`, `u8 flags` |
| Commit copy | 421 | `u32 421` (size including this field), copy of the `$$COMMIT$` record (0014:0007:*), see below; follows the entry of the commit record |
| Session marker | 38 | Entry `0015:0007:20` with a session GUID (not the GUID of the plf record), offset `0xFFFFFFFF`, class code 0; after every commit copy and at the end of the file |

* **One entry per plf record** (every version, not only the current one), except the session headers `0015:0007:20` of the plf.
  Confirmed: number of entries = number of records − session headers in all folder samples (759/759 in 005, 114 108/114 108 in 031); only exception sample 039_fw41: five store records (`0013:0007:*`) that were written when closing after the last save do not have an entry yet.
* **Order ≠ log order.** Per commit interval, the store records (type-hi 0007) come first, then the other records; the
  mapping entry ↔ record therefore goes via the offset field, never via the position.
* **Class code** = plf record field bytes 16..19 (`u32` after the key); for store records it is 0 there, in the incrementally written idx -1 (only 0 for the commit record). An idx rebuilt by TIA (after `Archive` + close, see below) also carries 0 for store records.
* **Flags:** 0; 0x40 or 0x80 respectively for two record classes whose plf field bytes 20..23 has the value 7 or 11 respectively (instead of 2 or 3);
  **0x04 = deletion.** A deleted object (49-byte tombstone in the plf) gets an entry with flag 4 and the GUID of the
  deleted object; offset `0xFFFFFFFF` (created and deleted in the same session) or the offset of the last
  live version (copy of its entry with flag 4).
* **Commit copy:** same header as the commit record of the plf (key, GUID, chain hash `field` at bytes 96..127), both
  length fields +32; after the chain hash 32 additional bytes (offset 128) and then 256 bytes (offset 160..415), which are **a real
  RSA signature with the project key**: decrypted with the public key of the plf, the result is a PKCS#1 v1.5 block
  with SHA-256 DigestInfo (in all checked folders, `check()["commitCopySignatures"]`, test `idx commit copies carry an RSA block`).
  **Which bytes are signed is open:** SHA-256 over the first 96/128/160 bytes of the copy or of the original (with old or
  enlarged length fields), chain hash, additional hash and their combinations (up to three parts) do not match the decrypted
  digest (sample 002_cpu); the 32 additional bytes are neither a SHA-256 of the original header nor a chain hash over the
  plf record hashes. Probably the idx's own integrity information; not needed for reading, open for writing (4e) in case TIA
  checks it when opening (so far TIA silently rebuilds a mismatching idx).
* **After Archive + close** the plf of the folder is compacted (different offsets, tombstones gone) and the idx is completely rewritten
  (commits then as `0014:0007:0`, `0014:0007:<oid>`; sample 037_lad_xml). An idx whose offsets do not match the plf (`check()["stale"]`)
  is rebuilt by TIA on the next open/save.
* **In the .zap21** the idx of the folder from which the archive was made lies next to the **compacted** plf (different offsets);
  the idx does not match the plf there, TIA rebuilds it on retrieve. `--verify` therefore checks the idx only in the folder.

`openpedata <project> --verify` reports the consistency (`PEData.idx: consistent with the plf …`), but does not affect the exit code
(derived cache).

## Savepoint link plf → external stores

Record **1006:0001:<oid>** (oid 449, in the Siemens demo project DemoProject_BestPractices_V4 446; found via the content `XRef%`/`SaveCommit_`; project history, "ProjectHistory…", text `V21 Update 3`) contains the savepoints of the stores outside the plf:
`IM\SearchIndex … SaveCommit_<GUID>` and `XRef%<GUID>`. They must match the files:

* `XRef\XRef.db` table `info.savepointID` = the GUID `XRef%…` (equal in 70 of 81 samples).
* `IM\SearchIndex\segments_N` (Lucene, commit user data `CommitType/SaveCommit_<GUID>`) = the GUID `SaveCommit_…` (equal in 64 of 81 folder samples).
* Deviations occur when TIA no longer updates the store after the last plf commit (e.g. sample 005:
  SearchIndex is one version older than the plf); presumably TIA then rebuilds the store when opening. Not evidenced.

## XRef\XRef.db (cross-references)

SQLite 3 (schema in `parser/openpedata/xref.py`). `parts.binary` = `"OG"` + record key (`u16 typeLo, u16 typeHi, u64 oid`,
14 bytes) or `00`; **all 191 parts of 036_tech resolve to plf records** (16 of them to deleted objects; in the archive and after Archive + close their
tombstones are missing, XRef.db continues to name deleted objects; a scratch project like 037_lad_xml with thousands of created and deleted blocks has the majority of its parts only in XRef.db). `objs` = (scope = PLC, primary object, sub-object `00` or `u32` member ID). `rels` = "left uses right"
with type codes, location (`location`) and `relationInfo`. `addrs` = absolute addresses of the tags: `range` 1 = input, 2 = output,
3 = bit memory, `firstBit`/`lastBit` = bit range (width 1/8/16/32 → `%M10.3`, `%MB`, `%MW`, `%MD`); **matches
`ITagAddress.LogicalAddress` in all samples** (test).

Type codes: 0x1000 OB, 0x1010 FC, 0x1020 FB, 0x1060 instance/global DB, 0x1065 instruction (IECPLInstructionProxy),
0x1070 technology object, 0x1080 PLC tag, 0x10a0 constant, 0x10b0 PLC data type, 0x1100 jump label, 0x1110 DB member,
0x1120 FB/FC interface tag, 0x1125 UDT member (the mapping to the record types is confirmed over all relations of samples 006, 018,
022, 030, 031, 036). Examples (036_tech): `FB_Iface → UDT_Motor` (0x1020 → 0x10b0, member 0x38), `FC_SclOps` calls
instructions (0x1010 → 0x1065), `FB_SclCalls → FB_Iface_DB` (0x1020 → 0x1060), `FB_LadTech → PID_Comp_2_3` (0x1020 → 0x1070).
`relationInfo`: lower 16 bits = kind of use (1 read/reference, 2 …), upper 16 bits = flags; **not fully decoded**.
`location`: `u32 depth`, per level `u32 element ID` + `u16 type code` (e.g. `02000000 61030000 6510` = element 0x361 of type 0x1065)
or `00000000 <u32 member> 01 <u32 member> 0000` for member uses. The element IDs are probably the UId counts
of the compile units (not checked against the code → open).

## Vci\Vci.db

SQLite 3 with the tables `BookKeepingRecord(BookkeepingId, Counter)`, `FileRecord(FileRecordId, BookkeepingId, RelativeFilePath,
FileHash, …)`, `FingerprintRecord(FingerprintRecordID, BookkeepingId, Name, Value, …)` (probably Version Control Interface). **In
all 82 samples the three tables are empty**; it is probably only populated with a connected version control system
(not created, open).

## IM\SearchIndex

Lucene.NET index (classic 2.x/3.x format: `segments.gen`, `segments_N`, per segment `.fdt/.fdx/.fnm/.frq/.prx/.tii/.tis/.tvd/.tvf/.tvx`,
compound segments `.cfs`, deletion lists `.del`). `segments_N` carries `CommitType/SaveCommit_<GUID>` as user data (link
see above). The stored fields (`.fnm`: `###ReconstitutionDataID`, `AssociatedCultureFieldId`, `AssociatedProjectVersion`,
`###id`, `###TargetId`, `Name`, `DisplayName`, `InterfaceMember_Name/_Datatype/_Value/_Comment`, `Properties`, `Language`, `Version`,
`Constant name`, `Event class`, …) contain Base64 payload per object (object IDs, names, interface texts). Pure
search index for the project search, can be rebuilt from the plf; not needed for the parser and not decoded.
The .zap21 does not contain it.

## .ap21, ProjectInfo.txt

See `container.md`. The descriptor contains no project contents.
