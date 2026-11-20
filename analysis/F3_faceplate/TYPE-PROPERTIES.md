# F3 list item 11: type and version properties

As of 2026-10-21, read-only: `samples\F05_fp_iface_instance.zap21` (5 types, version 0.0.1 each), comparison with F03. Openness truth: `F05_fp_iface_instance_truth\typesList.json`, `itemGet_Main_Faceplate_container_1.json`, `F03_hmi_faceplates_user_truth\typeExport_*.xml`.

## What is stored

| Object | Attributes (values the same in all 5 types, except name/GUID/size/times) |
| --- | --- |
| FaceplateLibraryTypeData | `ICoreAttributes` Name, Author, Comment (empty), Subtype ''; `ITypeInfo.TypeGuid`; `ITypeBaseAttributesInfo` IsObsolete False, IsSetAsNonUpdateable False, OriginalTypeGuid 0…0, TypeNamespace None, MinimumTargetDeviceVersion '' |
| FaceplateLibraryVersionData | `IVersionInfo` Version (20 bytes, A3), VersionGuid, FinalizationTime, MinimumTargetDeviceVersion 21.0.1.0; `ICoreAttributes` Creation/ModifiedTime, Author, LastModifiedBy, Comment, Version ''; `ICoreLibObjectAttributes` State, IsSystemElement False, OriginalLibrary "000_empty" (project name), DetailHelp/ShortHelp/OriginalName '', IconData/IconResource empty |
| ILFaceplateTypeData (content) | `ICoreAttributes` Name "Faceplate type", Subtype ILFaceplateType; expandos: **05 Width, 06 Height**, **0x024b layer list**, **_#ActiveLayer**, **Properties**; `IFaceplateInterface` (F05.md) |

**Layers.** Expando 0x024b (`K\x02`) is a CoreMapArray with 32 maps `{_TypeId 20002, _Guid, 0x03 (name)}`, names Layer_0 … Layer_31, 3230 bytes, byte-identical in all types and all screens of HMI_1 except for the GUIDs. Layer_0 has the GUID `1c130bbc-f35a-4068-aaab-1eba6135e5b0`, which every screen item references with its property Layer (F01 crash, CRASH.md). `_#ActiveLayer` names an entry in `IReferenceExpandoAttributes` whose value is this GUID → active layer Layer_0. Visibility and other layer settings are not in the list (default; only F20 with "Runtime visible" off shows where they go).

**Properties.** Expando `Properties` = MapT with its own varint(n+1) length prefix before the blob: `{_TypeId 900, _Guid 637a8242-38db-46bc-a823-9d6479b2123f}`, the same in all types, so a fixed class identifier of the faceplate type, not a type property.

**Not stored (default values):** Suspendable, display name, background color/image of the faceplate. Expandos only hold changed values; the type contains no property IDs other than 05, 06, 0x024b. Only sample F20 (Suspendable, display name, background, second layer with "Runtime visible" off) shows their IDs.

**Size.** Width/Height of the content = size of the instance: Faceplate container_1 (FP_Basic) is 750×300 (Openness `itemGet` Width/Height), FP_Basic content 750×300. The size results from the selection on creation (objects shifted by −10/−10, SCRIPTS.md); FP_Basic 750×300, FP_Controls 810×880, FP_Elements 800×400, FP_Interface 360×260, FP_Script 180×140.

## Openness

- `typesList`: name, folder, GUID, kind, doNotUse, setForUpdate; per version number, State, isDefault, author, release time. All in the parser, compared in test_faceplates (F03 40 values, F05 40 values, F02 16).
- `typeExport` (F03 truth): only Author, VersionNumber, Comment (MultilingualText, en-US empty). The parser returns author and comment ({} = empty).
- The instance shows Left/Top/Width/Height, ContainedType; size = content size of the type.
- Openness does not expose properties such as Suspendable for the type; can only be checked via the TIA UI (user) or instance properties.

## Parser and test

- `faceplates.py`: content gets `layers` (names), `activeLayer`, `properties` (map); instances `left`/`top`/`width`/`height`. `decode_map` accepts expando values with a varint(n+1) prefix.
- `test_faceplates.py`: per version size (fixed per type), 32 layers Layer_0…31, active layer Layer_0, `_TypeId` 900; F05 instance against `itemGet` with screen, ContainedType, Left, Top, Width, Height (6 values).

## Open

- F20: Suspendable, display name, background, layer visibility.
