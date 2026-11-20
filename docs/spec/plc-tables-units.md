# Watch/force tables and software units (Z1.2 P3/P4; samples 047–051)

As of 2026-10-25. Truth: `TiaPlcSoftware(list/get/export watchTable)`, `unitList`. Samples via MCP, chain from 003_tagtable (tag `Tag_Bool_1` %M0.0).

## Watch and force tables

| Sample | Change | Finding |
|---|---|---|
| 047_plc_watch_table | Create watch table WT_Test | `VarTableData` "WT_Test" (link `AggregatingFolder` → WatchtablesFolder), `VarItemContainerData`, `VarItemData` with empty `ItemData`, its own `IdentContainerData` |
| 048_plc_watch_entry | Entry `"Tag_Bool_1"`, format Bool, modify value TRUE (TiaMcpXE: SimaticML round trip, the table is created anew) | `VarItemData.ItemData` = XML `<VarItems><VarLines><VarLine Format="Bool" …><Address>%M0.0</Address><Name>"Tag_Bool_1"</Name><MonitorValue TriggerName="Smart"/><UserValue …>TRUE</UserValue></VarLine>…`; the tag receives an ident container for simple access |

- Every PLC has a force table "Force table" (`VarTableForceData`, already in 002_cpu). Openness does not list it among the watch tables.
- The XML is stored in the usual header format (u32 length at offset 1, data from offset 24).
- Trigger: the file contains `TriggerName="Smart"`, Openness reports `Permanent` for monitoring and modifying; the mapping is open.

## Software units

| Sample | Change | Finding |
|---|---|---|
| 049_plc_unit | Create Unit_A | `UnitData` "Unit_A" with its own folders (ProgramBlocksFolder, ExternalSourceFolder, ControllerDataTypeFolder, ControllerTagsFolder …) |
| 050_plc_unit_b | Create Unit_B | second `UnitData` |
| 051_plc_unit_relation | Relation Unit_A → Unit_B (type SoftwareUnit) | link `References` on Unit_A, `ReferencedBy` on Unit_B; fingerprint of Unit_A new |

Parser: `project.plcWatchTables` (name, kind watch/force, PLC, folder, comment, entries with name, address, format, modify/force value, triggers, comment lines) and `project.plcUnits` (name, PLC, relations, referenced by) in `openpedata\plc_tables.py`. Test: test_plc compares the list of watch tables, per entry name, address, format and modify value, as well as the units with their relations, with Openness.

Force table (sample 078): an entry is stored as a `VarLine` like in the watch table; the force value is the `UserValue`, whose `Checked` = Openness ForceIntention. Openness reports Permanent as the monitor trigger, the plf contains Smart. Parser: for `kind` force `forceValue`, `forceIntention`; test `PlcForceTableTests`.

Open: blocks in a unit, relations to global DBs and TO DBs, namespaces.
