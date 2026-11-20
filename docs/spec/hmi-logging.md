# WinCC Unified: logging (Z1.3 U1; samples 136–144, audit trail 145–146)

As of 2026-10-29. Truth: Openness via TiaMcpXE `TiaHmiUnified` list/get with `objectKind` `dataLog`, `alarmLog`, `auditTrail`, `loggingTag` (files `<sample>_truth\HMI_1\*.json`). Chain from 102_hmi_tag_int (internal tag `HmiTag_Int1`), one change per sample via MCP, differences in `<sample>_attrdiff.txt`.

## Findings

| Sample | Change (Openness) | Finding in the store |
|---|---|---|
| 136_hmi_datalog | Data log `DataLog_1` created | new record `HmiDataLogData` on the HMI device; settings in `IHmiLogAttributes` (`LogMaxSize` 1000, `LogTimePeriod` 7 days, `SegmentMaxSize`, `SegmentTimePeriod`, `SegmentStartTime`, `StorageDevice` "SDX51", `StoragePath`); separate record `HmiBackupStrategyData` via link `IHmiLogAttributes_BackupStrategy` |
| 137_hmi_datalog_size | `Settings.LogMaxSize` 200 | `IHmiLogAttributes.LogMaxSize` 200 (MB) |
| 138_hmi_logging_tag | Logging tag `HmiTag_Int1_Log` on `HmiTag_Int1`, `DataLog` = DataLog_1 | no record of its own: nested expando `LoggingTag_n` in `IHmiLoggingTagExpandoAttributes` of the HMI tag (name, mode, cycle factor …); data log as link attribute `LoggingTag_n#LogConfiguration` in `IHmiLoggingTagExpandoLinkAttributes`, whose first 4 bytes (big endian) index the links `ExpandoReferences` of the tag |
| 139_hmi_alarmlog | Alarm log `AlarmLog_1` created | record `HmiAlarmLogData`, same `IHmiLogAttributes` as the data log |
| 140_hmi_logging_cyclic | `LoggingMode` Cyclic | field `LoggingMode` = 1 in the expando (missing for OnChange = 0) |
| 141_hmi_datalog_period | `Settings.LogTimePeriod.Days` 14 | `LogTimePeriod` in 100-ns ticks (14 days = 12 096 000 000 000) |
| 142_hmi_alarmlog_size | Alarm log `LogMaxSize` 300 | like 137 on `HmiAlarmLogData` |
| 143_hmi_logging_cycle | `Cycle` T10s | link attribute `LoggingTag_n#LoggingCycle` → `HmiCycleData` "T10s" (via `ExpandoReferences`) |
| 144_hmi_datalog_backup | `Backup.BackupMode` PrimaryPath | `IHmiBackupStrategyAttributes.BackupMode` 0 → 1 in the record `HmiBackupStrategyData`; only NoBackup and PrimaryPath are valid |

Not varied and therefore not output: the further properties of the logging tag (`AggregationMode`, `AggregationDelay`, `Smoothing*`, `LimitScope`, `TriggerMode`, `TriggerTagBitNumber`); in the samples they are at their default value.

`IHmiCentralLogAttributes` (central logging) changes along with it, but has no Openness property and is not output.

Audit trail: Openness only knows enumerate/find (`HmiAuditTrail`, no Create). TIA creates it when GMP is switched on (runtime setting `GMPEnabled`, sample 145, see [hmi-runtime-settings.md](hmi-runtime-settings.md)): record `HmiAuditLogData` "Audit trail_1" with the same `IHmiLogAttributes` (default LogMaxSize 10000, 365 days, segment 1000 / 30 days) and its own `HmiBackupStrategyData`. Sample 146 changes LogMaxSize to 20000.

## Parser

`openpedata\hmi.py`:

- per HMI software `dataLogs`, `alarmLogs`, `auditTrails` (`Hmi.logs`): name, `logMaxSize`, `logTimePeriod` and `segmentTimePeriod` as days/hours/minutes/seconds, `storageDevice`, `storagePath`, `segmentMaxSize`, `segmentStartTime`, `backupMode`, `backupPrimaryPath`, `backupSecondaryPath`;
- per HMI tag `loggingTags` (`Hmi.logging_tags`): name, `loggingMode` (OnChange/Cyclic), `cycleFactor`, `dataLog`, `cycle`.

Test: test_hmi compares count and properties (LogMaxSize, LogTimePeriod, StorageDevice, SegmentMaxSize, SegmentTimePeriod, SegmentStartTime, BackupMode; for logging tags DataLog, LoggingMode, CycleFactor and in cyclic mode Cycle) with the Openness truth; 136–146 green.

Viewer: node "Logging" under the HMI with the tabs Data logs, Alarm logs, Audit trails and Logging tags (all logging tags of the device with tag and table).
