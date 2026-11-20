# Record payload (established, step 4a)

Evidence: all 7 samples 000–004, reader `parser\openpedata\` (Python) on the user's machine, output `samples\<sample>_analysis\graph.json`.

## Object record layout
| Off | Content |
|---|---|
| +0 | u32 length |
| +4 | Key (type ID, object ID), see plf-container.md |
| +16, +20 | Characteristic values; u32@20 = 3 normal case, 2 or 11 for types with typed lists (e.g. S7ControllerTargetData) |
| +26 | Number of slots |
| +28 | Object GUID |
| +44 | Slot table: per slot u32 = absolute offset of the block, 0 = empty |

**Slot order:** first all AttributeSets that the type implements (incl. inherited ones), in declaration order in the metamodel XML; then link slots.
- u32@20 = 3: exactly 2 tagged link blocks.
- u32@20 = 2/11: additionally typed lists per relation, entries 12 B `[target type ID][target OID]` without relation ID; relation can be derived via back links.

H-010 resolved: @60/@72 are simply slot 4 and slot 7, meaning per type (FolderData: ICoreAttributes / 1st link block; EAMTZTagData: ITagAddress / IStructureItem; EAMTZTagTableData: ICoreExpandoAttributes / 2nd link block with relation "Tags").

## Link block
`[u16 size][u16 count]` + entries 16 B `[u32 relation ID][u32 target type ID][u64 target OID]`. Gaps (relation 0) possible, not counted.
Validation: 100 % of the slot structures consistent, 100 % of the links with known relation and existing target, types match according to the metamodel; a brute-force scan finds no links outside. Back direction 000_empty 959/959, 003_tagtable 1950/2027 (rest = relations stored on one side only, DataTypeRef, Target).

## Attribute block
- Only attributes that are not intrinsic, have no `relation=` and are not constant are stored. Merge flags from the AttributeSet + all Implements overrides; the most derived type wins per flag.
- Order = ordinal. Fixed types inline: bool 1 B, int/uint 4 B, enum according to base size, dateTime 8 B .NET ticks (UTC).
- String/blob: u32 offset relative to the block start → varint with **overall size including itself** + bytes (for short values this looks like n+1). pe:GuidT inline 16 B.
- Hit rate (5a, 19 folders, 33 391 blocks): 99.25 % complete, 99.95 % top level consistent, 0 errors. Rest: 235 blocks with nested array/structure data (e.g. IConfigBaseData.AdditionalInfos), 16 blocks with expando="simple" (ICommentsExpandoAttributeSet).

## Expando sets (dynamic attributes)
`[used][alloc][count][u16][type ID][IDs][slots][values]`; Bool/Byte/Int directly in the slot, larger values in the value area. Names and type codes in dictionary records store type 0x00070011 per object type. Type codes: 1 Bool, 2 Byte, 5 Int, 6 Long, 0xa DateTime, 0xb String, 0xc Guid, 0xd Blob, 0xe XML.

## Deletion (H-018)
TIA appends a new version with the same key: tombstone 49 B, u32@16 = 0xFFFFFFFF, @24 = 4, GUID 0, no slots. Links on the opposite side are removed (table: Content; CPU: typed list). The parser hides deleted objects. Matches TIAconv/ERNW (flag 0x04).

## Addendum 5a (commit 31e2163), comparison with TIAconv/ERNW on our own samples
- **Varint:** LSB first (e.g. `80 03` = 384), counts its own length.
- **Blobs:** kind 0/1 = pages (kind 1 zlib; compressed pages decompress to 4096 B, truncate to overall size). 1324/1324 BlobT/XmlT/expando blobs of all samples decode to the stated size (tools\blobcheck.py). Kind 4 in no sample (see addendum 2026-10-30).
- **0x70011 expando dictionary:** `u32 len, u32 len, u32 type ID, u32 count, u32 maxId`, then `{u32 key, string, u32 type code | 0x80000000}` (length twice, differs from TIAconv).
- **0x70002:** table type → dictionary record (13/13 entries point to matching 0x70011 records).
- **CoreTextAttributeT:** layout according to TIAconv confirmed: `varint; u32 total; u32 used; u32 FFFFFFFF; u32 count; count×u16 LCID; count×u32 offsets; per entry u32 length + UTF-8`, LCID 0xFFFF = without language (evidence: Main comment 2 languages 0904/ffff). Replaces the single-language description in tag.md.
- **Slot order name vs. declaration:** identical in all samples, not decidable; the only differing type in the metamodel is LibraryMasterCopyData (sample 021_mastercopy, affected slots empty).
- **PEData-NN.plf:** the parser reads continuation files in order, untested.
- 0x70009 NamedObjects, 0x7000d Modify, 0x7000f Packages: present, content unchecked.

## Addendum 2026-10-30: blob kind 4 = pages in store records
Check tool: `tools\blobcheck.py <path>`. Large blobs (seen: `IIdentPartData.PayLoad`, `IInterfacePartData.Payload` and expando blobs of large blocks, 150–170 KB XML) are not in the object record:
- **Value in the attribute:** `u8 4`, then the header like kind 0/1 (`u64 overall size, u32 ?, u16 page size 0x1000, u32 page count, varint n + n×u32 bitmap`); the page count is the capacity (multiple of 32), n can be larger than needed. Then, per bitmap word ≠ 0, one link12 `[u16 0x0013][u16 0x0007][u64 OID]`; the value ends exactly there.
- **Store record 0x00070013** (`0013:0007:<OID>`, store area like 0x70011): 44 B record header (length, key, u32 0, u32 3, u32 0, GUID), `u32 n`, n bytes = ordinary kind 0/1 blob with the 32 pages of the word (word i = pages 32·i … 32·i+31), `0xFF`. Overall size per record 0x20000, the last one the remainder. Here the bitmap in the record has one word per page (n = page count); the page format is otherwise the same.
- **Content** = the chunk blobs concatenated. Result: 147/147 kind 4 blobs decode to the stated size, XmlT with BOM; 587 of the 589 store records 0x70013 are referenced (rest probably orphaned).
- **Parser:** `attrs.decode_blob(raw, store)`; `Project.attributes` returns such values as `attrs.StoredBlob`, which knows the store records of the project, so no caller needs a change. If a store record is missing, `decode_blob` raises `attrs.BlobError`; `Model` catches errors per block or PLC data type (`error` on the object, list `statistics.errors`, message on stderr) instead of aborting the whole run.
- Kind 4 as `varint n + n bytes` (earlier work) is only assumed if the length works out exactly; seen in no file.

## Addendum U4: inheritance order of the overrides (commit dfd18a5, 2026-10-20)
With diamond-shaped inheritance (e.g. HmiReusableConfiguration and CoreObject), overrides of attribute flags must be applied topologically (base first, derived type last), not in breadth-first order. Breadth-first let the override `Comment constant=true` from CoreObject win over `constant=false` from HmiReusableConfiguration; ICoreAttributes was decoded shifted (name missing, timestamp wrong). Affected: 24 ObjectTypes (metamodel 114): HMI (among others HmiSystemEventData, HmiInternalSystemTagData, ILScreenData, HmiDiscreteAlarmData, HmiAnalogAlarmData, tag member types, Scripting-Global, wireless zones), plus DataObjectData 0x0002103a, MemberTagData 0x00021049, ReferenceMemberTagData 0x00021053, HasCoreMemberTagData 0x0002105f, IECPLLibraryTargetData 0x0022101d, Sivarc RuleTableInstance(Object). Nothing changes in the existing non-HMI samples (no objects of these types); regression comparison over 73 sample folders: only HMI objects change, JSON otherwise identical.
