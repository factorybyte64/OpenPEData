# WinCC Unified: runtime settings (Z1.3 U2; samples 145–152)

As of 2026-10-29. Truth: Openness via TiaMcpXE `TiaHmiUnified get runtimeSetting` per scope (`runtimeSetting.json` = root, `runtimeSetting_<scope>.json` for opcUa, maxLogin, processDiagnostics, reporting, telemetry). Chain from 102_hmi_tag_int, one change per sample via MCP, differences in `<sample>_attrdiff.txt`.

According to the TiaMcpXE docs (`tia-mcp://docs/hmi-unified-runtime-settings`) the following are writable:

| Scope | Properties |
|---|---|
| root | StartScreen, GMPEnabled, BitSelectionStrategyForResourceLists, AutoLogOffURL, CentralPanning, CentralZooming, EnableLanguageCompatibleFontFamilies |
| opcUa | ActAsOPCServer |
| maxLogin | EnableLockAfterNumberOfAttempts, MaxLoginErrors |
| processDiagnostics | EnableProcessDiagnostics |
| reporting, telemetry | Openness attributes of the device |

## Storage location

- **GMPEnabled** = `IHmiApplicationAttributes.IsRegulatedApplication` on the HMI software (`HmiApplicationData`).
- **All other settings** are expandos in `IHmiILSettingsExpandoAttributes`. Per area there is one `HmiILSettingsData` record (link `Target` = HMI software) whose name corresponds to the Openness scope:

| Record | Openness scope | Example |
|---|---|---|
| ILScreenSettings | root (screen settings) | `CentralPanning` |
| MaxLogin | maxLogin | `EnableLockAfterNumberOfAttempts`, `MaxLoginErrors` |
| OpcUaServer | opcUa | `ActAsOPCServer` |
| ProcessDiagnostics | processDiagnostics | `EnableProcessDiagnostics` (`CriteriaAnalysisSymbol` is already in the base project) |
| Reporting | reporting | `IsReportingEnabled`, storage locations `ReportingMainStorage`/`ReportingMainStoragePath`, `ReportingDatabaseStorage`/`ReportingDatabaseStoragePath` |
| Telemetry | telemetry | not set in the samples |
| UnifiedTagSettings | unifiedTag (Unified PC only) | `TagChangeNotification`, `FirstValueQuality` (no Openness names, Comfort Panel) |
| Environment, Runtime, UmacRuntimeSettings, AdditionalRuntimeSettings, SmtpServerConfiguration, ResourceSettings, ExclusiveOperationSettings | – | empty |
| DomainFunctionConfigurations | – | binary record `DomainFunctionsRecord` of the runtime functions; grows with each enabled function; not decoded |

Only what deviates from the default or was set once is stored. A missing value means the default value.

## Findings per sample

| Sample | Change (Openness) | Finding in the store |
|---|---|---|
| 145_hmi_rt_gmp | root `GMPEnabled` true | `IsRegulatedApplication` False → True. TIA additionally creates: the **audit trail** `Audit trail_1` (`HmiAuditLogData`, link `HmiAuditLogDataChildren` on the device, its own `HmiBackupStrategyData`), 11 `HmiAlarmAuditClassData` and 11 `HmiParameterSetTypeAuditClassData` (GMP audit classes), 8 internal system tags (among others `AuditTrailHealth`) and 36 system events with texts |
| 146_hmi_audittrail_size | Audit trail `Settings.LogMaxSize` 20000 | `IHmiLogAttributes.LogMaxSize` 10000 → 20000 (like data/alarm log). TIA rejected a first attempt with 500 (message "The maximu…"), presumably because LogMaxSize must not be smaller than SegmentMaxSize (1000) |
| 147_hmi_rt_panning | root `CentralPanning` Enabled | `ILScreenSettings.CentralPanning` new, as text "Enabled" |
| 148_hmi_rt_maxlogin | maxLogin `EnableLockAfterNumberOfAttempts` true | `MaxLogin.EnableLockAfterNumberOfAttempts` new |
| 149_hmi_rt_maxlogin_errors | maxLogin `MaxLoginErrors` 5 | `MaxLogin.MaxLoginErrors` new (the default 20 was not stored) |
| 150_hmi_rt_prodiag | processDiagnostics `EnableProcessDiagnostics` true | `ProcessDiagnostics.EnableProcessDiagnostics` new; plus 3 system events, 2 system tags, `DomainFunctionsRecord` grows |
| 151_hmi_rt_opcua | opcUa `ActAsOPCServer` true | `OpcUaServer.ActAsOPCServer` new |
| 152_hmi_rt_reporting | reporting `IsReportingEnabled` true | `Reporting.IsReportingEnabled` new; 9 system events, system tag `ReportingSystemAlarms` |

## Parser

`openpedata\hmi.py` outputs `runtimeSettings` per HMI software:

- `gmpEnabled`;
- `sections`: record name → stored values, without `DomainFunctionConfigurations`.

Test: test_hmi compares GMPEnabled and every stored value whose name occurs in the Openness scope with the truth. In addition, the value changed in the sample (`change.json`) must be stored. 145–152 green.

Viewer: node "Runtime settings" under the HMI, one row per stored setting (area, setting, value).

Not covered are StartScreen (the base project has no screen), CentralZooming, AutoLogOffURL, BitSelectionStrategyForResourceLists and telemetry. Following the pattern they are presumably also in `ILScreenSettings` or `Telemetry`. How StartScreen is stored as a reference to a screen is open.

## Start screen (sample 169, 2026-11-07)

- `HmiGraphXSettingsData`, expando link `IlStartScreen` (6 bytes): u16 at byte 4 = index into the expando links of the object; the link points to the screen (`ILScreenData`), free entries at 0000:0000:0. Parser `runtimeSettings.startScreen`, test `HmiStartScreenTests` (114 Screen_1, 169 Screen_2).
