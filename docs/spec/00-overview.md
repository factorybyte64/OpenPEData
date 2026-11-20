# Format overview (established)

Status: TIA Portal V21 Update 3 (Portal.exe 2100.0.121.1, Openness 21.0.0.0). Evidence: sample 000_empty, inventory `samples/000_empty_inventory.json` (on the user's machine).

## Project folder (empty project, no device)
| Path | Size | Format | Role |
|---|---|---|---|
| `<Name>.ap21` | 151372 | UTF-8 BOM + XML, root `<Data Type="Project" BrowsableType="Siemens.Automation.DomainModel.ProjectData" ProjectCompatibilityVersion="21.0.0.0">`, ns `http://www.siemens.com/2007/07/Automation/CommonServices/DataInfoValueData`; children only `<Icon>` (Base64 ICO, ~99 % of the file) and `<ProjectVersion>21.0.0.0` | pure descriptor, no content |
| `ProjectInfo.txt` | 274 | YAML-like text: MinimumRequired V21.0 / 21.00.00.00, LastUsed V21 Update 3 / 21.00.03.00, UsedProducts | version info |
| `System/PEData.plf` | 247043 | proprietary binary, mixed entropy (4K blocks 2.8–8.0), internal zlib streams | **main object store** |
| `System/PEData.idx` | 14962 | proprietary binary, header like plf | index for the plf |
| `Vci/Vci.db` | 32768 | SQLite 3: FingerprintRecord, FileRecord, BookKeepingRecord | version control/fingerprints |
| `XRef/XRef.db` | 61440 | SQLite 3: objs, rels, addrs, parts, info | cross-references |
| `IM/SearchIndex/*` | small | Lucene.NET index (segments.gen, segments_2) | search, not in the archive |
| `IM/SPL/options.json` | 31 | Json.NET with `$type` | options, not in the archive |
| `AdditionalFiles/PLCM/plcmArchive.pma15_0` | 4 | `00 00 00 00` | not in the archive |
| `Logs, src, TMP, UserFiles` | – | empty | – |

## `.zap21`
Plain ZIP (version 45, ZIP64-capable, flags 0x0802 = UTF-8 names + data descriptor). Contains .ap21, ProjectInfo.txt, System/*, Vci/*, XRef/* and empty folders, **not** IM/* and AdditionalFiles/PLCM/*.

## PEData.plf / .idx header (first bytes, 000_empty)
```
plf: 40 00 00 00 01 00 00 00 <GUID df7bf935-4cc2-47b8-82dc-854fcdec4b7b, LE> 34 08 00 00 00 00 00 00 79 00 00 00 01 00 00 00 00 00 00 00 78 05 00 00 01 00 …
idx: 80 00 00 00 … otherwise identical, same GUID
```
zlib streams (78xx, inflate ok) in the plf at offsets 2228, 166083, 241510. Structure see [plf-container.md](plf-container.md) and [idx.md](idx.md).
