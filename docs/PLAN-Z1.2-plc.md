# Plan Z1.2: Fully understand the PLC as a device (gaps per COVERAGE.md)

Goals and non-goals: see [GOALS.md](GOALS.md) (the only valid source). As of 2026-10-25.

## Starting point

- Done and tested: blocks (OB, FB, FC, DB, instance DBs) with interfaces, attributes and code (LAD, FBD, SCL), instruction catalog, PLC tags, tag tables, constants, UDTs; CPU properties go via Z1.1 (H2, H7).
- Not emitted (docs\COVERAGE.md, run 2026-10-25): PLC alarms and alarm texts (`AlarmClassData`, `AlarmText`, `SimpleControllerAlarmData`), text lists (`SystemTextRangeData`/`…ListData`), technology events (`IpoEventData`, `CyclicServoEventData`).
- Without sample: watch and force tables, software units (`Lang.ProgUnit`), ProDiag, OPC UA server interfaces of the PLC, STL/GRAPH/CEM.
- Constraint (GOALS.md section 4): only what Openness can read or write. All steps below have an Openness counterpart that TiaMcpXE offers (`TiaPlcSoftware`: `alarmTextExport/Import`, `alarmTextListExport/Import`, `alarmClassExport/Import`, `watchTable` create/entryAdd/export, `unitCreate`/`unitRelationCreate`, `technologyObject` get/parameterSet, `opcUaInterface*`, `supervisionExport/Import`).

## Steps

Samples get the number range **041–099** (PLC). Every change on a copy, recipe into the MANIFEST, truth via Openness (SimaticML/text export, attributeGet, get), result in docs\spec\, hypotheses.md, LOG.md, test in parser\tests.

| No | What | Result | Who |
|---|---|---|---|
| P1 | **Done 2026-10-25** (041–044, docs\spec\plc-alarms.md, test_plc). **PLC alarms:** FB with `Program_Alarm` (041), instance DB (042), alarm text (043), alarm class of the instance (044). Truth: `alarmTextExport`, `alarmClassExport`, SimaticML of the FB. | Alarms with text, class, associated values | Assistant |
| P2 | **Done 2026-10-27** (045 by hand by the user, 046 entry, 058 range; docs\spec\plc-text-lists.md). **Text lists:** create user text list (045), change entry (046). Truth: `alarmTextListExport`. | Text lists with ranges and texts | Assistant |
| P3 | **Done 2026-10-25** (047 table, 048 entry; force table entries open; docs\spec\plc-tables-units.md). **Watch and force tables:** create table and entries (047), force table (048). Truth: SimaticML export, `get/watchTable`. | Tables with entries | Assistant |
| P4 | **Done 2026-10-25** (049 Unit_A, 050 Unit_B, 051 relation; docs\spec\plc-tables-units.md). **Software units:** create unit (049), block/relation in the unit (050). Truth: `unitList`, export. | Units, assignment, relations | Assistant |
| P5 | **Partly done 2026-10-25** (052, 053 parameters; hardware connection open; docs\spec\plc-technology-objects.md). **Technology objects:** change parameters of a TO (052, base 036_tech), TO hardware connection (053). Truth: `get/technologyObject`, `hardwareConnectionGet`. | TO parameters as in Openness | Assistant |
| P6 | **Done 2026-10-25** (054 interface, 055 disabled; nodes open; docs\spec\plc-opcua-prodiag.md). **OPC UA server interface of the PLC** (054). Truth: `opcUaInterfaceList/Export`. | Interfaces and nodes | Assistant |
| P7 | **Done 2026-10-25** (056 ProDiag FB, 057 operand supervision; docs\spec\plc-opcua-prodiag.md). **ProDiag supervisions** (055), if creatable without a license. Truth: `supervisionExport`. | Supervisions | Assistant |
| P8 | **Done 2026-10-25** (matrix regenerated with 181 samples: Z1.2 75 types in samples, 41 emitted; note in docs\COVERAGE.md). **Comparison and coverage:** regenerate the coverage matrix, note in docs\COVERAGE.md. | Z1.2 criterion confirmed | Assistant |

Order: P1 to P4 first (largest gaps), then P5 to P8.
