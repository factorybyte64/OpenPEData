# Block interface (FB, FC, OB)

Status: confirmed on samples 023_fb_iface (FB with all sections and UDT parameter) and 024_fc_iface (FC with Return), checked against SimaticML and Openness get JSON (5c, 2026-10-20). Supplements db-udt.md. Hypotheses H-022 to H-026.

## Location
Block --CurrentInterface--> `InterfaceVersionRootData` (payload XML). Comments are not in the payload but in `InterfaceComments` (path = member ID).

## Sections
The root payload names the sections as members with fixed IDs:

| ID | Section | Storage |
|---|---|---|
| 2 | Input | SubPartIndex → `InterfacePartData` (InputSection) |
| 3 | Output | SubPartIndex → OutputSection |
| 4 | InOut | SubPartIndex → InOutSection |
| 5 | Static | SubPartIndex → StaticSection (FB only) |
| 6 | Temp | SubPartIndex → TempSection |
| 7 | Return | Members inline in the root |
| 8 | Constant | Members inline in the root |

The part payload is a wrapper `<Member>` around the members of the section. In the payload, Return (7) comes before Constant (8); Openness and TIA show Constant before Return. The parser sorts Input, Output, InOut, Static, Temp, Constant, Return.

## UsedParts
`UsedParts` is a position-stable index list. Unused slots are null links `0000:0000:0`. `SubPartIndex` (also for UDT members and `<ExternalType>`) counts in this list including nulls.
- FB_Iface: 0 Input, 1 Output, 2 InOut, 3 Static, 4 Temp, 5 empty (Return), 6 Values, 7 DataTypeSource (UDT).
- FC_Iface: 0 Input, 1 Output, 2 InOut, 3 Temp, 4 empty, 5 empty, 6 DataTypeSource.

## Member
- IDs are unique block-wide, from 51. The comment and start value path is only the ID, for nesting `ID:ID`, for array elements `55[1]`.
- Types: array via `Member@Type` ("Array[0..3] of Int", RID 0x0201…); UDT via `Type="\"UDT_Motor\""` with RID 0x91…, elements from the part `InterfaceVersionRootData` (SubPartIndex); elementary types via RID 0x02… plus DataTypeID. The root additionally contains `<Externals><ExternalType Name BlockClass CRC OffsetCRC><Usage Path Name Section/>`.
- Start values: FB in the Values part (`<V p="60" v="42"/>`, constant `p=51 v=7`); FC in the root expando `Values` (ICoreExpandoAttributes). Constants have start values like tags.

## Parser output
For FB/FC/OB the field `interface` = `[{section, members:[{name, dataType, startValue, comment, elementStartValues?, members?}]}]` (text and `--json`); empty sections are omitted. DBs unchanged (`members`).

## Open
Offsets, CRC, StdO, LID, MFlags, Flags, SlotCounts (memory layout, checksums) not decoded. Not varied: non-optimized access, multi-instance members (FB as type), String/WString, Array of UDT, multi-dimensional arrays, Variant, Pointer, retentivity, member attributes (S7_SetPoint, ExternalAccessible), LAD/FBD blocks. OB Main: culture key `65535` for system comments unexplained (H-026).

## SD pitfalls when creating
Tag name `sInt` is invalid (type SInt); Static = `VAR`, constants = `VAR CONSTANT`, constant in the code as `#cInt`.

## Instance DB (sample 025_fb_idb, 2026-10-20)
- `DataBlockData` with `IGeneralDataBlockSourceData.Type=IDBofFB`, `OfType=FB`, `OfNumber` = FB number (`OfName` empty), `BlockType=DB`, `BlockLanguage=DB`.
- Link: relation `IsGeneratedFrom` from the DB to the FB (`CodeBlockData`), on the FB `GeneratedByThis`. Otherwise only ModifiedTime and `IMakeData.Level` (Interface → None) change on the FB, and `PreferablyRenumbered` is dropped.
- Unlike the global DB, `CurrentInterface` of the IDB is an `InterfaceVersionRootData` of its own with `Kind=Values` and empty payload (20-byte skeleton, no XML). Its only `UsedParts` target is the interface root of the FB (`Kind=BlockSource`, the same object as `CurrentInterface` of the FB). The IDB has no structure of its own and no `InterfaceComments` object; members, types and sections come from the FB.
- Openness (SimaticML, get) shows only Input, Output, InOut, Static for the IDB (no Temp, no Constant), with name and data type, but without start values and without comments.
- Parser: the instance DB block has `interface` (from the FB interface), `members` = Static section, `startValue` = the DB's own value (so far null), `defaultValue` = start value in the FB.
- Changed IDB start value (029_fb_idb_value, H-028): appears as `<Values>…<V p="60" v="99"/>…` in the payload of the IDB root (Kind=Values), p = member ID of the FB; in the parser `startValue` = own value, `defaultValue` = FB start value. documentWrite replaces the DB object (new OID).
- Open: array element values in the IDB (path `55[1]`, provided for in the code, not varied), multi-instance (FB as a member of an FB), `OfName`/`OfNumber` for a renamed FB, IDB in a group.

## Addenda 5d part A
- `: Void` for an FC results in the interface member `Ret_Val` with RID 0x02000000 (DataTypeID 0), type name `Void`. SimaticML also lists empty sections, get JSON and the parser do not.
- SD syntax V21 (for creating samples): SCL network = pure SCL code in the `NETWORK` block (line comments remain), FB call `"FB_Iface_DB"(iBool := TRUE, …)`; LAD `RUNG wire#powerrail / Contact / Coil`; FBD `RUNG #start / Coil(#running)`; `{ S7_Templates := "SrcType := Int" } Add( in1 := #a, in2 := #b, out => #sum )` works in LAD and FBD; network title and comment via `S7_NetworkTitle/S7_NetworkComment := "MLC_…"` plus .s7res; block language via `S7_PreferredLanguage` in the header. SD export quotes tags like `"a"`. get with detail=full returns networks as SCL text or `statements` respectively.
