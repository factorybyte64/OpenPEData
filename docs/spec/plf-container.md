# PEData.plf – container (established)

Evidence: samples 000_empty (folder = status after close), 000_empty.zap21 (compacted), 000_empty2, 000_empty_resave. Parser: `tools\PeData.cs` on the user's machine. All chains verified up to EOF, 0 hash errors.

## File header (plf and idx identical except for DWORD 0)
| Off | Type | Value | Meaning |
|---|---|---|---|
| 0 | u32 | 0x40 plf / 0x80 idx | file kind (not header length) |
| 4 | u32 | 1 | format version (conjectured) |
| 8 | GUID | new on each write, equal in plf+idx of the same status | generation/pairing ID |
| 24 | 4×u32 | 2100, 0, 121, 1 | writing TIA version = FileVersion Portal.exe 2100.0.121.1 (V21 Upd 3) |
| 40 | u32 | 0 | |
| 44 | u32 | 0x578 = 1400 | unknown (schema/store version?) |
| 48 | u32 | 1 | unknown |
| 52..64 | | 0 | |
| 65 | 32 B | SHA-256 over bytes 0..64 | header hash |
| 97 | u8 | FF | separator |
| 98 | | first record | |

## Record
`[u32 len][len-4 bytes, last byte = FF][SHA-256 over the len bytes incl. length field]`, next record at `off + len + 32`. Gapless chain up to EOF.

Record header (structure established, semantics partly conjecture):
| Off | Type | Content |
|---|---|---|
| +0 | u32 | len |
| +4 | u16 | class (e.g. 0x0000, 0x0016, 0x1002, 0x100e, 0x1003) |
| +6 | u16 | range: 7 = system, otherwise 1/2/4/0x10/0x40/0x41 |
| +8 | u64 | object ID (system 1,2,3,0x10,0x18,0x20…; user objects sequential 0x424…) |
| +16 | 12 B | system: `00 00 00 00 03 00 00 00 00 00 00 00`; for class 0x1002/0x1004/0x100e bytes +24..27 = `00 00 06 01` / `07 01` / `09 01` |
| +28 | GUID | object GUID (random per project; system objects constant `6b1aa731-cf2a-684f-b0f0-39e3862e25xx`) |
| +44 | | payload |

Key = (class, range, object ID), notation `KKKK:BBBB:ID` hex.

Example record 0000:0007:1 @2180: `35 3a 00 00 | 00 00 07 00 01 00 … | 00 00 00 00 03 00 … | 6b 1a a7 31 cf 2a 68 4f b0 f0 39 e3 86 2e 25 01 | 04 3a 00 00 (=14852 compressed length) | 78 9c …zlib… | FF | sha256`.

## Transaction log
The folder status is a log since creation: `[##CLOSE## 0015:0007:20] [Tx1, 9 records] [$$COMMIT$ 0014:0007:401] [Tx2, ~355 records] [$$COMMIT$ 0014:0007:405] [##CLOSE##]`. Changed objects appear several times (old and new version), e.g. 0000:0007:1, 0001:0007:2, 0003:0007:3, 000d:0007:10 (3×), 000c:0007:e.
The archive (`.zap21`) contains a **compacted** plf: each key once (362), one COMMIT (0014:0007:0), CLOSE at the end. No prefix relation to the folder status.
Resave without change does not rewrite plf/idx/DBs/.ap21 (byte-identical, same mtime).

## Known record types
| Key | Content |
|---|---|
| 0000:0007:1 | zlib-compressed metamodel XML (`<MetaInfo xmlns="http://www.siemens.com/Automation/2004/04/ObjectFrame/Meta">`, packages/namespaces/enumeration/attribute/relation with IDs 0x0001xxxx). Tx1: only ObjectFrame.Core (221 KB unpacked), Tx2: all packages (1 MB). Compressed length as u32 @+44, stream from +48, ends 1 B before FF. |
| 000f:0007:400/402 | Package registry: name + `8:<40 hex>`, e.g. `Siemens.Automation.ObjectFrame.Core*8:A73DA774…541C` |
| 0016:0007:18 | Project key: CAPI PUBLICKEYBLOB (`06 02 00 00 00 a4 00 00 RSA1`, 2048 bits, e=65537) + ~1.7 KB further material |
| 0014:0007:* | `$$COMMIT$` (389 B), contains ticks + ~288 B random (signature?) |
| 0015:0007:20 | `##CLOSE##` |
| 1028:0002:433 | presumably project object (length changes with name length) |
Other records uncompressed binary.

## Noise sources (hide for diffs)
Object GUID @+28, .NET DateTime ticks UTC (mostly @+64/+76/+86/+88), record 0016:0007:18 entirely, commit records, header GUID.
