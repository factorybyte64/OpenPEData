# PLC alarms (Z1.2 P1; samples 041–044)

As of 2026-10-25. Truth: Openness export of the alarm texts (`PlcAlarmTextProvider.ExportInstanceTextsToXlsx`, `alarmTexts.xlsx`), alarm classes (`alarmClasses.dat`), SimaticML of the FB. Samples created via MCP on copies of 002_cpu (CPU 1513-1 PN V2.9).

| Sample | Change | Finding |
|---|---|---|
| 041_plc_alarm_fb | FB_Alarm from SCL source with static variable `Alarm_1 : Program_Alarm` and call | new `SimpleControllerAlarmData` "Alarm_1", Subtype `TypeAlarm`, `PhysicalParent` → FB, link `AlarmClass` → "No Acknowledgement" (class of the type alarm), 11 `AlarmText` via `Texts`; plus system blocks Program_Alarm/ALARM_BASE and data types Event_ID/AssocValue_0 |
| 042_plc_alarm_idb | Instance DB FB_Alarm_DB | second `SimpleControllerAlarmData` Subtype `InstanceAlarm`, `PhysicalParent` → DB, `TypeAlarm` → type alarm (conversely `InstanceAlarms`), 11 own, empty `AlarmText` (= "taken over from type"), no class of its own |
| 043_plc_alarm_text | Alarm text "Testmeldung Pumpe" (test alarm pump) and info text "Infotext Pumpe" (info text pump) on the instance (import xlsx) | the first two `AlarmText` of the instance receive the text (en-US); `TextLocks` of the type alarm changes; `IAlarmDataExpando.AlarmServices_TextLanguages` (XML) |
| 044_plc_alarm_class | Alarm class of the instance "Acknowledgement" (import xlsx) | link `AlarmClass` on the instance alarm, `AlarmClassIsSet` True |

- **Text mapping:** the n-th `Texts` link has the text index `IAlarmData.TextIndexes[n]` (u32 size, u32 count, count × u16). Index 0 = info text, 1 = alarm text, 2–10 = additional text 1–9 (evidenced by 043 against the Openness export). The texts themselves are normal multilingual texts (`ICoreTextRepository.Text`).
- **Alarm class:** on the alarm via `AlarmClass`; for instance alarms without a class of their own, the class of the type alarm applies (Openness export "### Inherited from Type ###").
- **Alarm classes** (`AlarmClassData`): name, `Acknowledgement`, `IsReadOnly`, `Priority`, `GlobalId`; the system classes exist in every project.

Parser: `project.plcAlarms` (`openpedata\plc_alarms.py`): alarms (name, kind, alarm type, block, PLC, alarm class, whether set there, type alarm, texts per language) and alarmClasses. Test: `parser\tests\test_plc.py` compares instance alarms, classes (including inherited ones) and all texts per language with the Openness export and checks the type alarm in the FB.

Open: associated values (`SD_1…`), alarms with `Program_Alarm` in several instances, own alarm classes (can only be created via the .dat import), text lists (P2).
