# PLC text lists (Z1.2 P2; samples 045, 046, 058)

As of 2026-10-27. Truth: Openness export of the PLC alarm text lists (`PlcAlarmTextListProvider.ExportToXlsx`, `alarmTextLists.xlsx`, sheets "TextList" with Name, ListRange, Comment [language] and "TextListEntry" with Parent, From, To, Text [language]; custom property `FileContent` "Alarm text lists").

| Sample | Change | Finding |
|---|---|---|
| 045_plc_textlist | **by hand by the user** (copy of 044): user text list TL_Test, decimal, 0 = "Aus" (off), 1 = "Ein" (on) | `UserTextRangeListData` "TL_Test" (`IObjectRangeListData.ListRange` Decimal, `Index` 512), link `Entries` → two `UserTextRangeData` "Range 0"/"Range 1" (`IRangeData.RangeFrom`/`RangeTo`, `Text` multilingual), `TextListFingerprintContainer`, text category `TextListTextCategoryData`; link from the CPU target |
| 046_plc_textlist_entry | Text of entry 1 "Ein" → "An" (on) (xlsx import) | The import creates all entries anew (new keys); `ChangeCounter` 4 → 5, fingerprint new |
| 058_plc_textlist_range | Range 2–5 "Stoerung" (fault) (xlsx import) | third entry "Range 2 - 5" with `RangeFrom` 2, `RangeTo` 5 |

Parser: `project.plcTextLists` (name, kind user/system, PLC, ListRange, index, comment, entries with from/to and text per language) in `openpedata\plc_tables.py`. Test: test_plc compares lists, ListRange, each entry (from, to, text per language) and the number of entries with the Openness export.

For creating further samples the export suffices as a template: change cells in the sheet TextListEntry or append rows and import (`alarmTextListImport`).
