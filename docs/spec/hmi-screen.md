# WinCC Unified: screens (ILScreenData 0x00cd1002) – established, U4

Evidence: samples 110_hmi_screen (Screen_1 new), 111 (rename), 112 (BackColor), 113 (screen group ScreenGroup_1 with Screen_G1), 114 (second screen Screen_2); base 101_hmi_base; commit f001ca7 on the user's machine. Defaults according to Openness: 800×480, BackColor 0xFFC0C0C0, AlternateBackColor 0xFFEBEBEB, BackFillPattern Solid, Enabled true.

## Record
Package DL.ModernUI, 16 slots, about 7 KB. Occupied: ICoreAttributes(1), IPropertyDynamicExpandoAttributes(9, 6510 B), IPropertyDynamicLinkExpandoAttributes(10, 136 B), IQualityChangedExpandoAttributes(11, 58 B), Links(14/15). **No document, no XML/JSON blob:** the screen properties are expando entries in the record.

## Properties
| Property | Location |
|---|---|
| Name (also Author, Comment, CreationTime, ModifiedTime) | ICoreAttributes |
| Screen number | none in the record; Openness reports ScreenNumber 0 everywhere |
| Width / height | expando property ID 5 = Width, 6 = Height (inline); `OriginalScreenResolution` (IQualityChanged…, 0x320/0x1E0) |
| Background color | expando property ID 0x66 = 102 (BackColor), code 90125, value: varint 8 + `00 00 00` + u32 LE ARGB (0xFF00FF00 = `00 ff 00 ff`); same 7-byte format as the colors of the alarm classes |
| Layer | expando ID (name bytes `4b 02`), code 0x00cd4001: blob ~3 KB with u32 length, count 32, offset table, Layer_0 … Layer_31; plus `_#ActiveLayer` (code 11) |
**Only changed properties are stored** (the default BackColor is not in the record; only 112 has the entry).

## Links
AggregatingFolder → FolderData "ILScreenFolder" or, for a screen group, FolderData "ScreenGroup_1" (Subtype 'ILScreen.Subfolder'); Target → HmiApplicationData; Environment; ExpandoReferencesInverse → HmiGraphXSettingsData.
**Start screen:** the first screen created becomes the start screen: HmiGraphXSettingsData 0x00cb1012 gets `ICoreExpandoLinkAttributes.IlStartScreen` (6 B `06 00 00 00 00 00`) and link ExpandoReferences → screen.
Per screen only this one record is created (the 32 ILDeviceLayerData 0x00cd1006 already exist with the device). Rename and BackColor change only the screen record.

## Expando dictionary for Unified (new)
In the dictionary 0x00070011, Unified properties have a **numeric property ID with length prefix** instead of a text name (`01 05`, `01 06`, `01 66`, `02 4b 02`); the reader returns them as characters. Evidenced: 5 Width, 6 Height, 102 BackColor. Other entries have plain-text names (`OriginalScreenResolution`, `_#ActiveLayer`).

## Files outside the plf
Only PEData.idx and XRef.db change; IM\HMI and RtIdFiles remain unchanged (H-025 deferred).

## Open
Screen items (U5), events, dynamizations, complete property ID table (derive from the metamodel or samples).
