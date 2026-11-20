# Object model and type resolution (established)

Evidence: samples 000_empty, 001_devgroup, 002_cpu, 003_tagtable, 004_fb (recipes in the MANIFEST on the user's machine). Tools: `Export-Metamodel.ps1`, `Diff-PeData.ps1`.

## Type ID from the record key
`TypeId = (u16 @+6 << 16) | u16 @+4`. Resolution via the embedded metamodel (record 0000:0007:1, latest version in the log in each case): 100 % hits for all keys with range ≠ 7 (000: 344/344 … 004: 678/678). Range 7 = store-internal (0x0007xxxx, not in the metamodel).

High part = package family: 0x0001 ObjectFrame.Core, 0x0002 DomainModel, 0x0004 ReleaseManagement, 0x0010 HwConfiguration, 0x0022 Simatic.Lang, 0x0040 AlarmServices, 0x0041 SystemDiagnosis, 0x0214 Umac.
Low part: 0x1xxx ObjectType, 0x2xxx Relation, 0x3xxx AttributeSet (conjectured), 0x4xxx Array, 0x5xxx Enumeration.

Examples: 0x00021002 DataTypeItemData, 0x0002100e FolderData, 0x00021028 ProjectData, 0x00011006 CorePersistenceInfo, 0x0010101c S7ControllerTargetData, 0x0002105c EAMTZTagData, 0x00021062 EAMTZTagTableData, 0x00221001 Lang.Blocks.CodeBlockData, 0x00221016 CompileUnitData, 0x0022160c InterfaceVersionRootData, 0x00221202 IdentContainerData.

## The metamodel grows with the content
| Status | Size unpacked | IDs | Packages |
|---|---|---|---|
| Tx1 empty | 221 KB | – | only ObjectFrame.Core |
| v2 (000, 001) | 1 024 558 B | 1070 | 28 |
| v3 (002–004, after CPU) | 1 768 363 B | 1926 (434 ObjectType, 702 Relation, 1236 Attribute, 277 AttributeSet, 94 Enum, 71 Structure, 50 Array) | – |
Consequence: the parser must always read the metamodel from the respective file, no fixed type table.

## Confirmed objects
- 1028:0002:433 = ProjectData (project name, author = Windows user). H-009 confirmed.
- Object IDs allocated in blocks (002: 0x808–0x951, 003: 0xc10–c12, 004: 0xc10–c13) → ID allocator with reservation blocks.
- The object GUID remains stable within a copy lineage, random only on new creation.

## What a change produces (record diff without noise)
- **Device group** (001): new FolderData "Group_1"; ProjectData gets a link (relation 0x00022011 FolderParentData.Folders → 0x0002100e#0x808); CorePersistenceInfo +SaveCommit entry.
- **CPU 1513-1 PN V2.9** (002): 346 new records (among others 75 DataTypeItemData, 47 SimaticConstantTagData, 14 DeviceItemData, S7ControllerTargetData 29 KB, CodeBlockData OB Main, Default tag table), metamodel v3, new packages.
- **Tag table + Bool tag** (003): new EAMTZTagTableData "TagTable_1", Lang.Blocks.EAMTZTagTableFingerprintContainer, EAMTZTagData "Tag_Bool_1" (%M0.0, Bool).
- **Empty SCL FB** (004): new CodeBlockData "FB_Empty" (with `<CompilerSettingsDocument>` XML), IdentContainerData, InterfaceVersionRootData (`<Root InterfaceGuid=…>` XML with sections Input…Constant), CompileUnitData (`<SCLSource Version="3.4.0.0">`).
→ Blocks contain embedded XML fragments in plain text.
