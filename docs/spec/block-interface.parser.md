# Interface of blocks (draft)

Status: samples 017–024, TIA V21 Update 3. Applies to DBs, UDTs and code blocks (FB, FC, OB). Implementation:
`parser\openpedata\interface.py`, test: `parser\tests\test_samples.py` (`check_interfaces`).

## Objects

```
Block (CodeBlockData / DataBlockData / UDT) --CurrentInterface--> InterfaceVersionRootData
                                            --InterfaceComments--> InterfaceCommentsPartData
InterfaceVersionRootData --UsedParts--> ordered list of InterfacePartData | InterfaceVersionRootData | null
```

- `IInterfacePartData.Payload` of the root is an XML `<Root InterfaceGuid RIdSlots>`.
- `InterfaceComments` is a "simple expando" (`ICommentsExpandoAttributeSet`): member path → CoreText per culture.

## UsedParts is an index list with null links

Unused slots are links to `0000:0000:0` (no object). The index in the list is the target of `SubPartIndex`
(sections and UDT members, also `<ExternalType SubPartIndex>`). A reader must not discard null links, otherwise
the indices shift (FB_Iface: index 5 is null, the UDT part has index 7; FC_Iface: index 4 and 5 are null,
the UDT part has index 6).

| Block | UsedParts (index: content) |
| --- | --- |
| FB_Iface (023) | 0 InputSection, 1 OutputSection, 2 InOutSection, 3 StaticSection, 4 TempSection, 5 null, 6 Values, 7 DataTypeSource (UDT_Motor) |
| FC_Iface (024) | 0 InputSection, 1 OutputSection, 2 InOutSection, 3 TempSection, 4 null, 5 null, 6 DataTypeSource (UDT_Motor) |

The Kind values of the parts: `InputSection`, `OutputSection`, `InOutSection`, `StaticSection`, `TempSection`, `Values`,
`Structure` (struct members of a DB), `DataTypeSource` (= `InterfaceVersionRootData` of a used UDT).

## Root payload of a code block

```
<Root>
  <Externals><ExternalType SubPartIndex Name BlockClass CRC OffsetCRC><Usage Path Name Section/></ExternalType></Externals>
  <Member ID=2 Name=Input    SubPartIndex=0/>
  <Member ID=3 Name=Output   SubPartIndex=1/>
  <Member ID=4 Name=InOut    SubPartIndex=2/>
  <Member ID=5 Name=Static   SubPartIndex=3/>     (FB only)
  <Member ID=6 Name=Temp     SubPartIndex=4/>
  <Member ID=7 Name=Return   SubPartIndex=… n=1 ph=1> <Member ID Name RID LID MFlags/> </Member>   (FC only, inline)
  <Member ID=8 Name=Constant SubPartIndex=… n=1 ph=1> <Member ID Name RID LID/> </Member>         (inline)
  <Offsets …/>
</Root>
```

- The section IDs are fixed: 2 Input, 3 Output, 4 InOut, 5 Static, 6 Temp, 7 Return, 8 Constant.
- Sections with a part: the payload of the part is a wrapper `<Member>` with the members of the section (+ `<Offsets>`).
- Sections Return and Constant hold their members inline in the root (the SubPartIndex point to null links).
- Member IDs are unique per block (from 51); they do not repeat the section. Paths for comments, start values and
  array elements are therefore only the ID (`60`, `55[1]`), for nested members `ID:ID`.
- Display order (Openness, TIA editor): Input, Output, InOut, Static, Temp, Constant, Return. The payload
  names Return (ID 7) before Constant (ID 8).

## Member

`<Member ID Name RID [Type] [SubPartIndex] StdO LID [MFlags]/>`, `RID` and `Type` as in `interface.py`:
`0x02…` elementary type (name from `DataTypeItemData`), `0x0201….` array (declaration in `Type`, e.g.
`Array[0..3] of Int`), `0x91…` UDT (`Type` = `"UDT_Motor"`, `SubPartIndex` → part of type `InterfaceVersionRootData`
with the elements of the UDT), `0x92…` struct.

## Start values

- FB: part `Values` (`<Values><V p=ID v=value/></Values>`), e.g. `p=60 v=42` (Static `stInt`), `p=51 v=7` (Constant).
- FC: expando `Values` on the root (`ICoreExpandoAttributes.Values`, same XML), e.g. `p=52 v=7` for the constant.
- Comments are always in `InterfaceComments` (not in the part). For elements of a used UDT there is no
  comment of their own on the block (SimaticML shows them without comment).

## Instance DB (sample 025_fb_idb, draft)

- The DB is a `DataBlockData` with `IGeneralDataBlockSourceData.Type = IDBofFB`, `OfType = FB`, `OfNumber` = number of the FB
  and a relation `IsGeneratedFrom` → FB (`CodeBlockData`). The FB has the opposite direction `GeneratedByThis` → DB.
  `BlockType` is `DB`, `BlockLanguage` `DB`. The parser field `type` = `InstanceDB`, `instanceOf` = name of the FB.
- `CurrentInterface` of the DB is an `InterfaceVersionRootData` of its own with `Kind=Values` and empty payload (20-byte
  blob skeleton, no XML). Its only `UsedParts` target is the interface root of the FB (`Kind=BlockSource`, i.e.
  the same object as `CurrentInterface` of the FB, and `Name` `BIVE:FB_Iface/…`). The DB stores no structure of its own:
  members, types, sections come from the FB. The DB has no `InterfaceComments` object.
- Sections: only Input, Output, InOut, Static (Temp and Constant of the FB do not exist in the DB). Openness (SimaticML, get) shows
  name and data type, but no start values and no comments, not even the FB start values (`stInt := 42`). The
  start values of the FB count as the default. The parser outputs them as `defaultValue`; `startValue` is the DB's own value
  (unchanged: `null`).
- Changed start values (sample 029_fb_idb_value, stInt 99, stBool TRUE): they are in the payload of the DB's own
  `InterfaceVersionRootData` (Kind=Values) as `<Values xmlns=…Values/V11_01.xsd><V p="60" v="99"/><V p="61" v="TRUE"/></Values>`
  (UTF-8 BOM, like the `Values` parts of the FB). Path `p` = member ID of the FB (60 = stInt, 61 = stBool); the counter block
  (`Counter`) and `PayloadCRC` change along with it. In the unchanged DB the payload is the empty 20-byte skeleton. Array elements
  would have to appear as `55[1]` (not varied). The parser reads the values from the root payload.
- The block parameter `members` of the instance DB is the Static section; `interface` has all four sections.
- When creating it, only the following changes on the FB: `ModifiedTime`, `IMakeData.Level` Interface → None,
  `PreferablyRenumbered` is dropped, plus the relations `GeneratedByThis` and `BlockToVersionElements` (→ interface root
  of the DB).

## Open

- `Offsets`, `CRC`, `StdO`, `LID`, `MFlags`, `Flags`, `SlotCounts` (memory layout and checksums) are not decoded.
- Retentivity, access rights (`ExternalAccessible` etc. per member) and attributes per member (`S7_SetPoint` …) were not
  varied; SimaticML only reports default values.
- Instance DB: change array element and UDT element values in the DB, multi-instance (FB as a member of an FB), instance DB of a group; relationship
  `OfNumber`/`OfName` for a renamed FB.
- Not varied: non-optimized access (`S7_Optimized := FALSE`), multi-instances (`FB` type as member), types such as
  String/WString/Array of UDT/multi-dimensional arrays/Variant/Pointer, LAD/FBD blocks, OB interfaces
  other than the predefined `Main`.
- OB `Main`: the comments of the system-predefined input tags (`Initial_Call`, `Remanence`) have, in addition to
  `en-US`, a culture key `65535` with the same text (probably the culture-neutral variant of the system text).
  Not checked against an Openness truth; the tests do not check OBs.
