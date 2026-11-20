# Project folder, .ap21, .zap21, ProjectInfo.txt

Status: V21 Update 3. Evidence from sample 000_empty.

## Folder structure (confirmed, 000_empty)

| Path | Kind |
| --- | --- |
| `<Name>.ap21` | UTF-8 XML with BOM, project descriptor (see below) |
| `ProjectInfo.txt` | YAML-like text, version info |
| `System\PEData.plf` | Own binary format, presumably object store (H-001), see `pedata.md` (still open) |
| `System\PEData.idx` | Index for PEData.plf: one 38-byte entry per plf record, commit copies (`derived-stores.md`) |
| `Vci\Vci.db` | SQLite 3: FingerprintRecord, FileRecord, BookKeepingRecord (empty in all samples, `derived-stores.md`) |
| `XRef\XRef.db` | SQLite 3: objs, rels, addrs, parts, info (cross-references, `derived-stores.md`) |
| `IM\SearchIndex\` | Lucene(.NET) index (`segments.gen`, `segments_N`) |
| `IM\SPL\options.json` | Json.NET with `$type` |
| `AdditionalFiles\PLCM\plcmArchive.pma15_0` | 4 bytes 0 in the empty project |
| `Logs\`, `src\`, `TMP\`, `UserFiles\` | empty |

## .ap21 (confirmed)

Root `<Data Type="Project" Name="…" NameOfName="Siemens.Automation.ObjectFrame.ICoreAttributes.Name"
BrowsableType="Siemens.Automation.DomainModel.ProjectData" ProjectCompatibilityVersion="21.0.0.0"
xmlns="http://www.siemens.com/2007/07/Automation/CommonServices/DataInfoValueData">` with the children
`<Icon Key="…ProjectData.$this.Icon">` (Base64 of an .ico, ~150 KB) and `<ProjectVersion>21.0.0.0</ProjectVersion>`.
Contains no project content.

## ProjectInfo.txt (confirmed)

`TiaPortal: MinimumRequired (Version, TechnicalVersion), LastUsed (Version, TechnicalVersion)`,
`UsedProducts: Name, Version, TechnicalVersion`. CRLF.

## .zap21 (confirmed)

Standard ZIP (version needed 45, flags 0x0802: UTF-8 names + data descriptor). Contains .ap21, ProjectInfo.txt,
System\*, Vci\*, XRef\* and empty folder entries; **not** IM\* and AdditionalFiles\PLCM\*.
