# Prior art: public information and tools on the TIA project format

Research status: 2026-10-19. Sources = web, GitHub, Gitee, technical articles. None of it was run against our samples; all statements from third-party sources are **unchecked** until we evidence them on our samples (then via hypotheses.md → doku/).

## Short conclusion
1. **TIAconv** (GPL-3.0) has the most detailed public format description (`docs/FORMAT.md`), tested among others with 17 V21 test projects. Best comparison for us.
2. **ERNW tia-parser** (BSD-3-Clause, Python) is the original source of the new layout (Nils Emmerich, blog Aug. 2026) and the only tool with a permissive license.
3. **jfk-solutions TiaFileFormat: EXCLUDED** (the user's rule, prio 1+). Commercial product; OpenPEData is independent of it and was created without its code. Read neither code nor documentation, adopt nothing, do not quote.
4. Our results on **chain hash, RSA commit signature, key record and .idx** are not in any source found. This is new.

## Sources in detail

| Source | Kind | License | Versions | Usefulness |
|---|---|---|---|---|
| [TIAconv](https://github.com/jkobierczynski/TIAconv) (J. Kobierczynski), [FORMAT.md](https://github.com/jkobierczynski/TIAconv/blob/main/docs/FORMAT.md) | C++17 CLI, read-only, format documentation + Python exploration scripts in `tools/` | GPL-3.0 (miniz MIT) | tested V13, V15.1, V16, V17, V19, V21; not V11/12/14/18/20 | **high**: documentation as reference, tool as independent oracle against our samples. Only adopt code if our parser becomes GPL. |
| [ernw/tia-parser](https://github.com/ernw/tia-parser), [docs/file-format.md](https://github.com/ernw/tia-parser/blob/main/docs/file-format.md), [Blog insinuator.net 08/2026](https://insinuator.net/2026/08/tia-project-parser/) | Python, forensics (timestamps → JSON / Sleuth Kit body) | BSD-3-Clause | not stated | **medium-high**: type table of the base types (IDs 0x8000000x), system record names, blob/MapT/expando layout. Code reusable with attribution. Relations not implemented. |
| jfk-solutions/TiaFileFormat and TiaProjectBrowser | **excluded** | commercial | – | **none**: the user's rule prio 1+: read neither code nor documentation, adopt nothing. Our project has therefore been called `OpenPEData` since 2026-10-19. |
| [lircy/TiaProjectParser](https://gitee.com/lircy/TiaProjectParser.git) (Gitee) | C#/.NET 4.0, reads symbol/tag tree from .apXX/.zapXX | Apache-2.0 | V13–V20 | **medium**: second independent implementation (own library, called "TiaFileFormat", origin independent according to README); no byte documentation. Code reusable with attribution. |
| [NVISO: Investigating an engineering workstation, part 3](https://blog.nviso.eu/2022/04/20/investigating-an-engineering-workstation-part-3/) | Forensics blog, V15.1 | – | V15.1 | low: markers PLUSBLOCK, $$COMMIT$, ##CLOSE##, DownloadLog XML (timestamps in ns since Unix epoch). |
| Chan & Chow, "Forensic Analysis of a Siemens PLC", IFIP CIP X, 2016 ([Springer PDF](https://link.springer.com/content/pdf/10.1007/978-3-319-48737-3_7.pdf)) | Paper, V13 | – | V13 | low: PEData.plf as change log with PLUSBLOCK/COMMIT. |
| [Detection of project file manipulation on EWS (ScienceDirect 2022)](https://www.sciencedirect.com/science/article/pii/S2666281722000233), [Anomalous values in TIA project data history (ACM 2021)](https://dl.acm.org/doi/fullHtml/10.1145/3487405.3487662) | Forensics papers | – | not read (access blocked) | unknown; possibly later via a library. |
| [CVE-2025-40759](https://www.sentinelone.com/vulnerability-database/cve-2025-40759/) (deserialization in project files), [ICSA-25-191-05](https://www.cisa.gov/news-events/ics-advisories/icsa-25-191-05) | Advisories | – | V20 and others | Note: TIA deserializes project contents; for us only security context. |

Not relevant (only Openness/XML, no file format): TiaExportBlocks, TIA-writer, Git add-ins for V21.

## Comparison with our status

### Confirmed by third-party sources
- Header 98 B, @65 SHA-256 over 0..64, @97 FF; record `[u32 len]…FF + SHA-256` (TIAconv, ERNW).
- Key = type ID (u32 @+4) + object ID (u64 @+8), object GUID @+28, payload from +44, u8 slot count @+26.
- 0x70000 = zlib MetaInfo XML; 0x70014 commit, 0x70015 close (ERNW; matches our 0014:0007 / 0015:0007).
- Slot table with absolute offsets, 0 = empty; link block `[u16 size][u16 count]` + 16-B entries; typed lists without relation ID (TIAconv: `u32 size, u32 0x7FFFFFFF, u32 count, count×{u32 type, u64 oid}`).
- Not stored: constant and intrinsic; strings with varint length that counts itself.
- .zapXX = normal ZIP; archive/Save As writes compacted.

### New for us (adopt as hypotheses and check on samples)
| Proposal | Source | Relation |
|---|---|---|
| u16 @+24 = flags, **0x04 = deleted**; tombstone = header only, 49 B in the new layout | TIAconv, ERNW | probably answers **H-018** |
| Blob kinds: kind 4 = varint + bytes (refuted for V21: kind 4 = pages in store records 0x70013, record-payload.md addendum 2026-10-30); kind 0/1 = sparse pages (`u64 overall size, u32 ?, u16 page size, u32 page count, varint n + n×u32 bitmap`), kind 1 pages zlib | TIAconv, ERNW | candidate for **H-013** (off-by-one for large blobs) |
| Expando: 0x70011 key table (`u32 len, u32 type ID, u32 count ×2, {u32 key, string, u32 value type}`), 0x70002 expando object table, expando segment with kind 1/2 | TIAconv, ERNW | **H-012**, H-013 (expando sets) |
| 0x70009 NamedObjects, 0x7000d Modify (creation/modification time), 0x7000f Packages (name + signature) | ERNW | our 000d:0007:10 (3×) = Modify, 000f:0007 = Packages |
| DateTime: 62 bits ticks + 2 bits kind (1 = UTC) | TIAconv | ticks decoding |
| CoreTextAttributeT: `varint; u32 total; u32 used; u32 FFFFFFFF; u32 count; count×u16 LCID; count×u32 offsets; per entry u32 length + UTF-8` (LCID 0xFFFF = without language) | TIAconv | differs in detail from tag.md, reconcile |
| Large projects: `PEData-01.plf`, `PEData-02.plf` … (unconfirmed) | TIAconv | parser must detect this |
| Setting know-how protection → TIA rewrites the plf, old file as `<Project>.backup/<Date>.<Time>/….zip` (plf+idx) | TIAconv | sample variant for later |
| Save history in CorePersistenceInfo (`History`/`HistoryEvent`, `ProjectSavePoint`) | TIAconv | parser feature "history" |
| Domain objects: IP as expando `xs:long`, `ProtectionLevel` 1–4, `EnablePutGetConnections`, SCL token tree, LAD/FBD `FlgNet`, IdentContainer/IdentXmlPart, HMI tags | TIAconv | semantics phase, saves a lot of diff analysis |
| Old layout (V13 and older): header 46 B, `40 00 01 00`, record header 28 B without GUID and without SHA-256, bare `$$COMMIT$$`/`##CLOSE###` records; V13 different relation names (`Source` instead of `CurrentInterface`, `BlockInterfaceXmlPartData`) | TIAconv | in case older versions are to be supported |

### Contradictions (to be clarified)
- **Slot order:** TIAconv and ERNW say "AttributeSets sorted by qualified name"; our record-payload.md says "declaration order in the metamodel". Possibly both coincide in our samples; a type for which the orders differ will decide it.
- **Commit type:** TIAconv names 0x7000C as save object with counter, ERNW and we 0x70014. Possibly TIAconv means another system object (we have 000c:0007:e several times in the log).
- **Varint bit order:** ERNW blog LSB first, ERNW documentation MSB first. Check on large strings.
- Header @24..39: "unknown" in TIAconv, established by us as the writing TIA version.

### Documented nowhere (our lead)
.idx entries, chain hash per commit, RSA-2048 signature over commit[0,128), key record 0016:0007:18 and new key pair per .zap21. Also, no source describes writing/signing.

## Recommendation for use
- Use **knowledge** from the TIAconv/ERNW documentation: facts are not protected by copyright; evidence every adoption on our samples first, then put it with a source reference in docs\spec\.
- **Code:** ERNW (BSD-3) and lircy (Apache-2.0) may be adopted with attribution; TIAconv (GPL-3.0) only read it or run it as an external check tool, unless the parser itself is to become GPL.
- **jfk-solutions:** excluded (the user's rule, prio 1+). Read neither code nor documentation, adopt nothing, do not install TiaProjectBrowser either.
- Possible next step (needs the user's approval, runs on his machine): run the TIAconv binary against samples 000–009 and compare the output with OpenPEData/Openness truth; specifically check H-018 (tombstone), H-013 (blob kinds) and the slot sorting.
