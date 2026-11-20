# F3 list item 15: dependencies of the faceplate versions and the empty container C_Faceplate1

As of 2026-10-21, read-only: `samples\F03_hmi_faceplates_user`, `samples\F05_fp_iface_instance.zap21`; for comparison `samples\L09_type_depends` (PLC type with a dependency, library thread).

## Dependencies

No faceplate version of F03 or F05 has a dependency: `DependsOn` and `usedBy` are empty (library.py), `IDependencyInfo.DependencyIdentifiers` is not set. This fits the fact that no type contains another **typed** type: the only nested container C_Faceplate1 in FP_Controls has no type.

`DependencyIdentifiers` occurs only once in all samples (L09, `CodeBlockDataByCopyVersionObject`, dependency on UDT_Motor). It is a pe:MapT with the key "UDT_Motor" and the type code **0x80066002**. In the metamodel, 0x00066002 is the structure `Siemens.Automation.CommonServices.Library.Model.DependencyIdentifierValue`. The value is a u32 offset to a block (u32 length 89, u32 24, GUID 765f65f7-…, strings: identifier, version GUID `fe4bcc…`, "0", "0", name UDT_Motor). library.py already decodes this content (versionGuids, strings).

**General finding on MapT (COREMAPARRAY.md):** the type code of a map value is either an expando code (`attrs.EXPANDO_CODES`: 1 bool, 2 byte, 4 uint, 5 int, 6 long, 7 float, 8 double, 0x0A dateTime, 0x0B string, 0x0C GUID, 0x0D blob, 0x0E xml, 0x13/0x14 nullable) or, above that, the **ID of a metamodel type**: 0xcd4001 CoreMapArray, 0x66002 DependencyIdentifierValue. Fixed-size values are inline, variable ones (string, xml, blob, map, array, structure) as a u32 offset relative to the value area. With 0x0E as an offset to varint text, all 108 `IUmacRootDataAttributes.NormalizedUmacMeta` now also decode structurally cleanly (content binary, not examined further). Open: 0x15, 0x72, 0x76, 0x31d00 are neither expando codes nor metamodel types of this project; 0x15 behaves like a nested map (CONTROLS.md), the others remain marked.

Only a sample with a typed faceplate inside a faceplate (F30) will show whether faceplate versions get `DependsOn` and `DependencyIdentifiers` like PLC types.

## Empty container C_Faceplate1

| | on the screen FP_Controls (HMI_1) | in the type FP_Controls |
| --- | --- | --- |
| Object | `0006:00d3:1c3e` ILFaceplateContainerData | `0006:00d3:20dd` ILFaceplateContainerData |
| Environment | ProjectData | FaceplateLibraryVersionData (FP_Controls 0.0.1) |
| Links | ILScreenItemParent, Environment, Target | plus ExpandoReferences → FontList_C1939294067 |
| `IsInstanceOf` | **missing** | **missing** |
| Expandos | Width 190, Height 120, Left 10, Top 750, Layer, CaptionColor 0xFF008844 | the same + StyleItemClass, 0x0051 Caption (Font → FontList) |

For comparison, the real instance "Faceplate container_1" (F05) has `IsInstanceOf` → FP_Basic 0.0.1. A type reference exists only via this link; there is no attribute `ContainedType`. Openness shows no `ContainedType` for C_Faceplate1 (F01 truth: key missing), and `V0.0.1\FP_Basic` for the instance.

**Consequence:** a container without `IsInstanceOf` is an "empty" faceplate container. In the type FP_Controls it is the reason for the compile error Error_NonExistingFaceplateType (F03.md), which prevents the release of FP_Controls. The parser lists it with the content of the version (screen item type HmiFaceplateContainer) and the container on the screen as an instance without a type (`name` null).

## Parser

- `decode_map`: all fixed-size expando codes, 0x0D/0x0E via offset, metamodel structures as `("type 0x…", bytes)`; non-matching codes remain marked as `("code 0x…", None)`.
- There is no data yet for dependencies of the faceplate versions; the output for that comes with F30.
