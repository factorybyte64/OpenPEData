# WinCC Unified: HMI tag (HmiTagData 0x00e51008) – established, U2

Evidence: samples 101_hmi_base (003_tagtable + MTP700 + HMI_Connection_1), 102_hmi_tag_int, 103_hmi_tag_ext, 104_hmi_tag_cycle, 105_hmi_tag_comment, 106_hmi_tagtable_group; commit 4a9bfca on the user's machine; diffs `<sample>_attrdiff.txt`, truth `<sample>_truth\HMI_1\`, tool `tools\unified_tagdiff.py`. Recipes in MANIFEST.md.

## Record
Package Siemens.Simatic.Hmi.Tag, base ReferenceTagData; record 885 B, 26 slots. Occupied: ITagAddress(2), IStructureItem(4), ICoreAttributes(6), IAdditionalAttributes(9), ISoftlinkAttributes(12), IControllerTagAttributes(14), IHmiHandshakeTagAttributes(15), IHmiTagAttributes(19), IHmiTagStructureAttributes(23), link blocks(24/25).

## Mapping property → storage location
| Property | Location |
|---|---|
| Name | ICoreAttributes.Name |
| Comment | ICoreAttributes.Comment (blob). Empty: 32 B `20 00 00 00 10 00 00 00 ff ff ff ff 00…`. With "Test": 66 B, count 1, LCID 0x0409, text as `<body><p>Test</p></body>` (UTF-8), no CoreText record of its own. Unlike the PLC tag (CoreTextAttributeT) **with HTML body**. |
| Data type | link `DataTypeRef` → DataTypeItemData (external: PLC type in scope S71500, e.g. 1002:0002:888 "Bool"; internal: HMI_ILRT, 1002:0002:1075 "Bool"); `IHmiTagStructureAttributes_HmiDataType` → always HMI_ILRT; `IStructureItem.DisplayTypeName`; `DataTypeShape` = IsElementary |
| Acquisition cycle | **link** `IHmiTagAttributes_AcquisitionCycle` → HmiCycleData 0x00b41001 (7 predefined: T100ms 1001:00b4:139b, T250ms 139c, T500ms 139d, T1s 139e, T2s 139f, T5s 13a0, T10s 13a1); no attribute value |
| Acquisition mode, Persistent, trigger, confirmation type, GmpRelevant | IHmiTagAttributes (defaults: AcquisitionCycle T1s, AcquisitionMode CyclicOnUse, Persistent false) |
| Scaling, limits, start/substitute value | IHmiTagStructureAttributes |
| Connection | link `IHmiTagAttributes_Connection` → HmiConnectionData (1002:00b2:1550) |
| Table | HmiTagTableData 0x00e51004 holds the tag as `Content` link (like the 29 system tags); link `TagTable` on the tag |
| Device | link `Target` → HmiApplicationData 1006:0010:1028; `Environment` → ProjectData |
| Group | FolderData 0x0002100e, Subtype 'HmiTag.Subfolder' under FolderData "HmiTagFolder" (AggregatedElements); table attached to it via AggregatingFolder (106) |

## Internal vs. external
| | internal | external |
|---|---|---|
| ITagAddress.Scope | 'HMI_ILRT' | 'S71500' |
| AddressMode | Invalid | Fixed |
| LogicalAddress | '' | copy of the PLC address (e.g. '%M0.0') |
| Part4/6/7/8 | -1 | 3/1/0/0 |
| IAdditionalAttributes, ISoftlinkAttributes, IControllerTagAttributes | missing | occupied |
| Connection link | none | present, additionally link InverseConsumer |

## Linking HMI tag → PLC tag
No direct link. New object **ScopedLinkMaintainerData 0x00025021** (package DomainModel.LinkMaintainer):
- Links: Consumer → HmiTagData, Provider → EAMTZTagData (PLC tag), InverseScopedLinkMaintainer → HmiTagTableData, Target → HmiApplicationData.
- Attribute `IScopedLinkMaintainerData.Links`: 96-B blob with handle (`dc ee 03 00` = 0x0003eedc, equal to `ISoftlinkAttributes.LinkHandle` of the HMI tag) and at the end the PLC tag name with length prefix (`0b "Tag_Bool_1"`).
- Counterparts: EAMTZTagData gets link `InverseProvider` → SLM (new slot 13), HmiTagTableData link `ScopedLinkMaintainer`.
- `IAdditionalAttributes.TypeSafeAddress`: 28-B blob `1c000000 0810c772 10000000 52000000 0c000000 01000000 09000040`, not yet interpreted.
- The PLC tag name is in the plf only in EAMTZTagData and in the SLM blob.

## H-022 (confirmed for saving)
After saving without compiling, `IM\HMI\*.rdf`, the zips and `AdditionalFiles\RtIdFiles` remain byte-identical; only PEData.plf/.idx, XRef.db and the Lucene segments change. Tag/table names are (UTF-8 and UTF-16LE) only in PEData.plf. IM\HMI is created when the device is created/compiled. For reading the engineering data the plf suffices.

## Anomalies
- Openness: `set tag Comment` is rejected, only `partSet` (partPath "", attribute=Comment). An unknown language (de-DE) reports `updated` but writes to en-US.
- Every tag has `compositions.Thresholds: 2` in Openness.
- The ChangeListData record grows with every change.
- The parser does not output HMI tags yet (only device HMI_1).
