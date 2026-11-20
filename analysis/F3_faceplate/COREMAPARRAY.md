# F3 list item 2: CoreMapArray and pe:MapT

As of 2026-10-20, read-only. Tool `analysis\F3_faceplate\coremap.py` (`decode_map`), run over all sample folders, output `coremap_out.txt`.

## Metamodel

- `CoreMapArray` (0x00cd4001, package DL.ModernUI) is an `Array` with element type **`pe:MapT`**:
  `<Array name="CoreMapArray" id="0x00cd4001" private="false" type="pe:MapT" />`.
- Used only by `IFaceplateInterface` (InterfaceEvents, InterfaceProperties, TagInterfaceProperties; on ILFaceplateTypeData) and `IParameterSetTypeElements.Members` (ParameterSetTypeData). No sample has a value for it (no faceplate interface, no parameter set type).
- `pe:MapT` itself is a base type and occurs in 13 further attributes: IDependencyInfo.DependencyIdentifiers, IControllerTarget.MaxUsedLidForRange / MemorySizePerRange / RetainMemorySizePerRange, IStructureItem.VendorData, IStructureRootWithHiddenSupport.HiddenPathes, ICoreIdentity.ApplicationIds, IFilter.TypeFilter, IUmacRootDataAttributes.NormalizedUmacMeta, IHmiAlarmAttributes.TextIndexMap, IHmiConnectionAttributes.MaxAreaPointerCountPerType, IHmiRuntimeSettingsAttributes.PartnerIdCoreMap, IFailsafeCompilationInfo.UsedLibraryGuids.

## Format of pe:MapT

1. **Envelope = blob** (`attrs.decode_blob`, like pe:BlobT): kind 0 = one uncompressed page with a 24-byte header (u8 kind, u64 overall size, u32, u16 page size, u32 page count, varint 1, u32 bitmap); kind 1 = zlib-compressed page (e.g. large TypeFilter). This is the "24-byte blob header" from the L1 report (DependencyIdentifiers).
2. **Content = map:**

| Offset | Content |
| --- | --- |
| 0 | u32 overall length of the content |
| 4 | u32 number of entries n |
| 8 | u32 offset of the value area (relative to the start of the content) |
| 12 | n × u32 type code, bit 31 set (`0x80000005` …) |
| … | n × key as varint(length+1) + UTF-8 |
| value area | n values in key order, size per type code |

**Type codes** (low bits; the same codes as in the Unified expandos, cf. `hmi.py`):

| Code | Value | Evidence |
| --- | --- | --- |
| 0x05 | i32 (4 bytes) | 2409 values, e.g. MemorySizePerRange, MaxAreaPointerCountPerType, PartnerIdCoreMap, `_TypeId` |
| 0x13 | i32 (4 bytes), likely a different integer kind | 208, e.g. MaxUsedLidForRange `Constant` = 55 |
| 0x0c | GUID (16 bytes, .NET order) | `_Guid` in faceplate maps |
| 0x0b | string: u32 offset (relative to the value area) to varint(n+1) + UTF-8 (F05.md); 4 bytes in the value area | all TypeFilter, faceplate interface (Name, Tag, _BlDataType) |
| 0x0e | variable, nested structure | 129, only NormalizedUmacMeta |
| 0x01 | bool, 1 byte | column and toolbar lists of the controls (CONTROLS.md) |
| 0x15 | nested map: u32 offset (value area) to a blob with MapT | Caption, Font, toolbars (CONTROLS.md) |
| 0xcd4001 | metamodel type ID CoreMapArray: u32 offset to an array of maps | column lists (CONTROLS.md) |

**Check over all samples:** decoded cleanly (values end exactly at the end of the content) for IStructureItem.VendorData (8492), IControllerTarget.* (3 × 139), IHmiConnectionAttributes.MaxAreaPointerCountPerType (36), IHmiRuntimeSettingsAttributes.PartnerIdCoreMap (36), IHmiAlarmAttributes.TextIndexMap (1), IFilter.TypeFilter (17 of 53). Open: TypeFilter (36, list values code 0x0b), NormalizedUmacMeta (93, code 0x0e), DependencyIdentifiers (1).

**Addendum 2026-10-21 (F05.md):** With 0x0b as string offset, `openpedata.faceplates.decode_map` decodes all 55 `IFilter.TypeFilter` cleanly; only NormalizedUmacMeta (105, code 0x0e) and one DependencyIdentifiers remain open. The first real CoreMapArray element (TagInterfaceProperties of an interface tag) is in F05.md.

## Hex examples

**Empty map** (IHmiAlarmAttributes.TextIndexMap, sample 108):

```
00 0c 00 00 00 00 00 00 00 00 00 00 00 00 10 01 00 00 00 01 01 00 00 00   blob kind 0, overall size 12, page 0x1000, 1 page, bitmap 1
0c 00 00 00  00 00 00 00  0c 00 00 00                                      length 12, 0 entries, value area at 12
```

**One entry** (IHmiRuntimeSettingsAttributes.PartnerIdCoreMap, sample 100): `{"{1052700-2067}": 256}`

```
23 00 00 00  01 00 00 00  1f 00 00 00    length 35, 1 entry, values from 31
05 00 00 80                              type 0x05 (i32)
0f 7b 31 30 35 32 37 30 30 2d 32 30 36 37 7d   varint 15 → 14 characters "{1052700-2067}"
00 01 00 00                              value 256
```

**Faceplate** (expando `Properties` on ILFaceplateTypeData, F02/F03; the expando is itself a MapT): `{_TypeId: 900, _Guid: 637a8242-38db-46bc-a823-9d6479b2123f}`

```
36 00 00 00  02 00 00 00  22 00 00 00    length 54, 2 entries, values from 34
05 00 00 80  0c 00 00 80                 i32, GUID
08 5f 54 79 70 65 49 64                  "_TypeId"
06 5f 47 75 69 64                        "_Guid"
84 03 00 00                              900
42 82 7a 63 db 38 bc 46 a8 23 9d 64 79 b2 12 3f   GUID
```

The container expando `51` (C_Faceplate1 in faceplate FP_Controls) has the same form with three entries: `_TypeId` (i32 40009), a key `1c 02` (property ID as in the expando dictionaries, type 0x15) and `_Guid`. So maps can carry property IDs as keys and contain nested parts (type 0x15).

## Conclusion for IFaceplateInterface

`CoreMapArray` = array of MapT. Per H-L2-1 (L2 report), an array value is u32 length, u32 count, count × u32 offset, then the elements; here each element would be a MapT as above, likely with keys such as name, data type and default value per interface entry. Can only be checked with F11 (interface tag), F16 (interface properties), F18 (interface event). `decode_map` is prepared for it; open are the type codes 0x0b (list/string), 0x0e and 0x15.
