# PLC tag (EAMTZTagData 0x0002105c) – established

Evidence: samples 003_tagtable and 005–009 (each exactly one `TiaPlcSoftware tagSet`), ground truth SimaticML in `<sample>_truth`, layout `samples\003_tagtable_analysis\EAMTZTagData_layout.txt` (tools\layout.py), attribute-level diffs `<sample>_attrdiff.txt`.

## Slots (14)
IDisplayNaming, IFailSafe, IFolderElementAttributes, INamespaceElementAttributes, **ITagAddress** (@60), IUnitElementAttributes, IDefaultStrategyData, **IStructureItem** (@72), IStructureRoot, **ICoreAttributes**, ICoreExpando, ICoreExpandoLink, 2 link blocks. The layout follows completely from the AttributeSet definitions (H-011 confirmed).

## Attributes
- **ITagAddress** (13): AddressMode, DuplicateState, LocalIdentifier, LocalIdentifierIsValid, LogicalAddress, MaxUsedChildLid, Part4, Part6, Part7, Part8, RuntimeIdentifier, RuntimeIdentifierIsValid, Scope.
  - %M10.3: Part4 = 3 (area M, conjectured), Part6 = 1, Part7 = 10 (byte), Part8 = 3 (bit), LogicalAddress = text.
  - Invalid address (Int at %M0.0): AddressMode = Invalid, Part4–8 = -1, LocalIdentifierIsValid = false.
- **IStructureItem** (15): DisplayTypeName ("Bool"/"Int"), HmiReadOnly, HmiVisible, Length (Bool 1, Int 16), Kind, …
- **ICoreAttributes** (6): Comment, CreationTime, IsConstant, IsShell, ModifiedTime, Name.

## Data type
Link DataTypeRef → DataTypeItemData with the name of the type ("Bool", "Int"); additionally DisplayTypeName as text.

## Comment (CoreTextAttributeT, blob) – corrected multilingual layout see record-payload.md, addendum 5a
`[u32 length][u32 offset category reference][i32 -1][u32 count]`, per language `[u16 LCID, e.g. 0x0409][u16 0x16][u16 0][u32 bytes][text]`, followed by a typed reference to TagCommentCategoryData. With the first comment TIA creates a new object TagCommentCategoryData (relation Categories on the project).

## Mapping Openness → attribute
| Openness | Attribute |
|---|---|
| Name | ICoreAttributes.Name |
| DataTypeName | IStructureItem.DisplayTypeName + link DataTypeRef |
| LogicalAddress | ITagAddress.LogicalAddress + Part4/6/7/8 |
| Comment | ICoreAttributes.Comment |
| ExternalWritable | !IStructureItem.HmiReadOnly |
| ExternalVisible | IStructureItem.HmiVisible (evidenced 016) |
| ExternalAccessible | IStructureItem.HmiStructureTypeRelevant (evidenced 016) |
Openness cascades (not accessible → not visible → not writable), only the flag that is set is stored.
Retentivity: cannot be set via MCP, not yet examined.

## Further evidence (5a)
- Comment texts UTF-8 (013: `50 72 c3 bc 66` = "Prüf…").
- I/Q/MW/MD/IW/QB addresses in 012_tags_multi read correctly.
- Tag table in a folder: attached via AggregatingFolder to FolderData; the parser outputs the path "TagGroup_1/TagTable_2".
