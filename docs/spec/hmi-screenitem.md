# WinCC Unified: screen items, dynamization, events – established, U5

Evidence: samples 115 (rectangle), 116 (Left=100), 117 (BackColor red), 118 (text "Hello"), 119 (Int tag base), 120 (I/O field without binding), 121 (I/O field, ProcessValue static text), 122 (button), 123 (Tapped event with script), 124 (I/O field with tag dynamization); commit 5f90d73 on the user's machine; cross-check on F01_hmi_allobjects of the faceplate thread (40 subtypes, read-only). Script modules and faceplate instance not via sample (TiaMcpXE has no Create operation; faceplates are done by the faceplate thread).

## One type for all objects
**ILScreenItemData 0x00cd1003** (package DL.ModernUI). Object type in `ICoreAttributes.Subtype` (HmiRectangle, HmiText, HmiIOField, HmiButton …), name in `ICoreAttributes.Name`. The only type of its own: faceplate container ILFaceplateContainerData 0x00d30006.
Linking: screen → object link `ILScreenItems`, object → screen `ILScreenItemParent`, Target → HmiApplicationData.

## Properties
Expando entries in IPropertyDynamicExpandoAttributes with numeric property ID (as for the screen, hmi-screen.md); **only deviations from the type default are stored**.
| ID | Property | Encoding |
|---|---|---|
| 5 / 6 | Width / Height | inline, code 19 |
| 0x63 = 99 | Left | inline, code 5 (only stored if ≠ 0) |
| 0x66 = 102 | BackColor | code 90125, u32 LE ARGB as for the screen (117: 0xFFFF0000) |
| 08 | unknown (code 11), always present | open |
| 0x4902 | I/O field ProcessValue as static text (121) | code 11 |
| 0x4f02 | text field Text (presumably text reference) | code 11 |
Text content: CoreText in IPropertyDynamicLinkExpandoAttributes (156 B, LCID 0x0409/0xffff). Top was 0 everywhere and is therefore stored nowhere; further IDs need further samples.

## Tag dynamization (124)
Expando `_#Dynamics` (code 21) in IPropertyDynamicExpandoAttributes, new expandos in ITextAttributes (113 B) and IEventExpandoAttributes. Link **`ExpandoReferences` → HmiTagData**, back link `ExpandoReferencesInverse`. Openness: DynamizationType Tag, PropertyName ProcessValue.

## Event with script (123)
- New **HmiScriptModuleContainerData 0x00e31003** ("ScriptModuleContainer"): `ScriptContainerParent` → screen, `EventElements` → script; the screen gets link `ScriptModuleContainer`.
- New **HmiScriptEmbeddedData 0x00e31002**, name "OnTapped": `EmbeddedScriptParent` and `ExpandoReferences` → button, `EventModule` → container.
- Button: expando `IAdditionalAttributes.Tapped` (6 B) and link `EmbeddedScriptChildren`.
- **Script text** in plain UTF-8 in `IAdditionalAttributes` of HmiScriptEmbeddedData. This block has a different format (no 8-byte header used/alloc); the expando reader decodes it wrongly, open.

## Files outside the plf
Only .idx, XRef and for 115 the .ap21 change; IM\HMI unchanged (H-025 deferred).

## Open
Text content and script code in the parser, further property IDs (Top, colors, font, border), script modules, faceplate container (F thread).

## Addendum U10 (commits 072d50c, d224666, 5a1ecab, e615c08, f41f6a1)
- **Scripts:** code exactly reconstructable incl. line breaks, tabs, indentation, comments; the token format stores whitespace and comments as separate tokens (sample 130). Events: scope Screen.Event, name `On<event>` (Tapped, Down …). H-030 confirmed.
- **Dynamizations** from `_#Dynamics` (nested expando): property, type (Tag, Script, Flashing), tag, script code (scope Screen.Trigger, name `<property>Trigger`), mapping table present yes/no. Evidence 124 (ProcessValue), 132 (BackColor as range), 133 (Visible via script), F01.
- **Texts** of objects and controls via the text reference (hmi-properties.md), per HMI language.
- Tests: test_hmi 67/67 (samples 100–133 + F01, each folder and zap).
- **Open:** ranges of the mapping table (from/to/value); tag references in scripts via ID (`Tags("1285367001")`) → tag name; property 0103/7904 on the alarm control; global script modules (only import via MCP).

## Addendum U11: mapping tables and tag references in scripts
- Dynamization with value mapping: `ValueConverter` → `MappingTable` → `Entries` + `ConditionType` (0 None, 1 Range, 5 Singlebit).
- Range entry: From, To, Value, RangeType; Value in the encoding of the target property (color: varint 8 + 3 bytes + ARGB; number: code 0x13 inline). Singlebit: relevant/condition bit masks.
- In nested expandos scalars are stored inline in the value table (bool 1 byte, int 4, GUID 16, code 0x13 4), everything else as a u32 offset.
- Script tag reference: `Tags("1285367001")` → entry "1285367001" in IScriptLinkExpandoAttributes → value u32 BE = index into ExpandoReferences of the HmiScriptEmbeddedData (0 = the object itself, 1.. = HmiTagData). Evidence samples 134/135, F01.

## Addendum F40 (286bb93): dynamization kinds
Types Tag, Script, ResourceList, Expression. Reference fields are resolved via IPropertyDynamicLinkExpandoAttributes to the ExpandoReferences of the object. ConditionType 6 = Expression. Structure members as Tag.member.
