# Coverage of the parser output per goal

Reference: [GOALS.md](GOALS.md) Z1.1–Z1.4. As of 2026-11-07. Generated with `tools\coverage_matrix.py` (read-only, no TIA action).

## What is measured

- **Object types in the metamodel:** all concrete `ObjectType`s (not abstract) from the embedded metamodels of the evaluated samples (643 types). The metamodel also contains types that never occur in a project (e.g. `Hmi.DL.Reserve`). The column "without sample" is therefore a list of candidates, not a target.
- **In samples:** at least one object of this type exists in a sample.
- **Emitted by the parser:** at least one object of this type appears with its record key in `OpenPEData --json --keys`. This is a **lower bound**: objects that the parser only processes inline (texts, idents, interface parts, data type items) do not count as emitted. An emitted type also does not mean that all its attributes are emitted (example: `DeviceItemData` counts as emitted, but the blobs `AdditionalInfos` and `AddressMap` are not decoded yet).
- **Basis of this run:** all 245 `.zap21` samples in `samples\` (list below), run 2026-11-07 on the user's machine with the product parser (.NET CLI; previously 212 on 2026-10-29 with the Python reference, 181 on 2026-10-25): `python -I tools\coverage_matrix.py samples --out docs\COVERAGE-data.json --cli dotnet\src\OpenPEData.Cli\bin\Release\net10.0\OpenPEData.Cli.exe`.

## Result

| Goal | concrete object types in the metamodel | of which in samples | of which emitted by the parser | in samples, not emitted | without sample |
|---|---|---|---|---|---|
| Z1.1 Devices and networks | 89 | 40 | 26 | 14 | 49 |
| Z1.2 PLC | 183 | 83 | 43 | 40 | 100 |
| Z1.3 WinCC Unified | 282 | 73 | 46 | 27 | 209 |
| Z1.4 Libraries | 36 | 18 | 15 | 3 | 18 |
| Infrastructure | 53 | 12 | 5 | 7 | 41 |

## Assessment

- **Z1.1:** largest gap. The hardware objects can already be decoded completely generically (0 decoding problems in 126, 038, 039, 040, 036; attributes with names, IP addresses, device names, slots). Since H1 the parser emits the device tree, expando parameters, nodes, subnets, sync domains and HMI connections; not yet emitted are system diagnostics settings, UMAC roles and `NotAssignedObjectsData`. Samples are also missing for subnet kinds, IO systems, distributed I/O, topology, GSD, Safety and Security (packages `HwConfiguration.Model` 17, `Safety` 17, `OpcUa.Model` 8, `SecurityNg` 6 types without sample). Plan: [PLAN-Z1.1-hardware.md](PLAN-Z1.1-hardware.md). Note 2026-10-25: since the constraint in GOALS.md section 4, only what Openness can read or write counts for Z1.1; types without an Openness counterpart are then no longer a gap. The matrix was regenerated on 2026-10-25 with samples 200–242 (Z1.1 previously 24/16/8/65 of 89, now in the table above); comparison with Openness see section H8.
- **Z1.2:** code, interfaces and tags are complete. Not emitted are PLC alarms and alarm texts (`AlarmClassData`, `AlarmText`, `SimpleControllerAlarmData`), text lists (`SystemTextRangeData`/`…ListData`) and technology events (`IpoEventData`, `CyclicServoEventData`). Without sample: ProDiag (12), TechTrace/Trace (19), software units (`Lang.ProgUnit`), STL/GRAPH/CEM, watch and force tables (parts in `Simatic.Lang`). Note 2026-10-25 (plan [PLAN-Z1.2-plc.md](PLAN-Z1.2-plc.md)): newly emitted are PLC alarms and alarm classes (`plcAlarms`), watch/force tables (`plcWatchTables`), software units (`plcUnits`), OPC UA server interfaces (`plcOpcUaInterfaces`), ProDiag supervisions (`plcSupervisions`) and the TO parameters as start values of the TO DB, all tested against Openness (test_plc, samples 041–057). `AlarmText` and text categories are emitted inline with the alarm and therefore do not count as emitted in the matrix. Open: user text lists (P2, template missing), nodes of the OPC UA interfaces, TO hardware connection, force table entries.
- **Z1.3:** screens, objects, tags and alarms are done. Not emitted are, among others, layers (`ILDeviceLayerData`), system events, area pointers, graphics (`HmiImageData`) and the runtime settings. Without sample: logging (12), recipes (5), user management/RTSecurity (5), task scheduling, reports (5), styles (9), script modules (`Hmi.Scripting` 13), controls (20). Note 2026-10-29 (plan [PLAN-Z1.3-unified.md](PLAN-Z1.3-unified.md), samples 136–163): newly emitted and tested against Openness are data/alarm logs, audit trail and logging tags (`dataLogs`, `alarmLogs`, `auditTrails`, `loggingTags`), runtime settings (`runtimeSettings`), project and runtime languages (`project.languages`, `runtimeLanguages`), global script modules (`scriptModules`), user text lists and graphic lists (`graphicLists`), plant view (`project.plantViews`), OPC UA alarm types (`opcUaAlarmTypes`) and system tag names. Inline without record key and therefore still counted as "not emitted" in the matrix: `HmiILSettingsData`/`HmiRuntimeSettingsData` (in `runtimeSettings`/`runtimeLanguages`), `HmiBackupStrategyData` (backup of the logs), `HmiScriptGlobal*` (in `scriptModules`), `ILOpcUaAlarmData` (automatic MultiInstance alarm). Out of scope per GOALS.md section 4 (no Openness API): recipes, task scheduling, system events, styles/designs, graphics as objects, reports as objects, layers (`ILDeviceLayerData`, findings remain). Open within scope: graphic list entries (need a project graphic), plant objects with plant object type, StartScreen.
- **Z1.5 (note 2026-10-31, plan [PLAN-Z1.5-hmi-screens.md](PLAN-Z1.5-hmi-screens.md)):**
  - For screen rendering the parser additionally emits values stored in the file: style metadata (`style.info`), partial expandos `StraightScale`, `CurvedScale` (with `ScalingType`), `Padding` and `Thresholds`, plus `BarMode`, `Visible`, `WindowFlags` (None), the static screen window `Screen`, the interface property of bound faceplate objects (`interfaceProperty`) and the widget package name (`Widget`). A dynamized ProcessValue is dropped.
  - Confirmed via Openness in working copy 164 (Screen_R8): StraightScale, CurvedScale, ScalingType, Padding, Thresholds and BarMode. Visible and WindowFlags remain to be confirmed.
  - Viewer against the Unified PC runtime: Screen_1 2.91 %, Screen_R8 1.79 % differing pixels. All screens of all HMI samples render without crashing.
- **Z1.2 (note 2026-11-07):** LAD/FBD additionally as SimaticML (`--simaticml`, `TiaProject.GetLadFbdSimaticMl`), all 1512 networks of the truth exports equal; GRAPH with sample 074 (sequence, sub-networks, SimaticML `<Graph>` equal to the export; actions with content open); STL calls of system instructions with sample 075 (42 of 83 catalog pages, code equal to the .awl export); CEM out of scope per section 4 (Openness does not export CEM). GRAPH actions confirmed with sample 076 (created in the editor); STL calls meanwhile 66 of 83. Z1.1: channel parameters (hardware interrupt per channel) with samples 254/255 against Openness channelGet. Z1.3: graphic list entries with graphic (sample 168) against the YAML export. Used products (`ProductVersionInfoData`, `PackageContainerData`) are emitted as `project.usedProducts`; the matrix counts them under Z1.4, but they belong to the project.
- **Note 2026-11-08 (working through the open items):**
  - Z1.1: PROFINET device name with automatic assignment by rule (23/23 interfaces 210–228 and 063 = Openness). GSD device Leuze CML 730i checked locally; not in the repo because of the vendor GSDML.
  - Z1.2: TO hardware connection (079), force table entries (078), all 80 STL system calls with parameter list (075, 4344/4344 checks = .awl export), GRAPH branches and permanent operations (077), action events and interlock (080), nodes and tag mapping of an OPC UA server interface (081).
  - Z1.3: start screen (169), graphic list entries (168), plant object with plant object type (170).
  - Z1.4: HMI library types (L37) with content of the versions: script code and SVG file (L38), text list type with entries (L39).
  - Still open: Z1.5 runtime comparison of further screens, graphic list type, reference namespaces, members of a plant object type; Z2 stays open as planned.
- **Z1.4:** 4 types are not emitted; without sample mainly library types for script modules, resource lists and dynamic SVGs (`Hmi.DL.Library.*`, 12 types) and further PLC type kinds (`Lang.Libraries`, 9).

## H8: Z1.1 hardware parameters against Openness (as of 2026-10-25, extended by 243–253)

Generated with `python -I tools\hw_openness_coverage.py` (read-only, no TIA action) over the H samples 200–253 and their Openness truth (attributes of the DeviceItems and modules, service properties, node attributes). Only values stored in the file are compared (decision 2026-10-24), and only within the Openness scope (GOALS.md section 4).

- **2556 occurrences** of stored parameters in 49 samples, of which **236 values compared with Openness: 236 equal, 0 differing.**
- **Mapped to Openness:** 25 parameter pairs (`OPENNESS_NAMES`, including service properties), 4 node fields (IP, mask, PROFINET name, PROFIBUS address); 1 parameter with the same name but a number instead of the enum name (`PnDnsConfigNameResolve` 0 = Openness "Deactivated", not compared).
- **Without Openness counterpart:** 211 stored parameters (subtype, name): internal management values (`IsAutocreated`, `BackPackRequired`, `SharedIoAssignment`, `PnIoArUuid` …), parameters of hidden objects (IO controller/IO device modules, `OpcUaEnableServer`/`OpcUaPortNumber` on submodule OPC UA_1, cable expandos of the ports, `PnIoSendClockFactor`), containers and blobs (`DnsAddressContainer`, `WebServerLanguageContainer`; `PkiContainerCache` is emitted as a certificate list and tested against `plcCertificateList`), encrypted passwords, and values that Openness stores differently (`EnablePutGetConnections`, `ProtectionLevel`, `AccessControlAtRuntime` as enum, daylight saving time block). They remain in the parser output but, under the constraint, do not count toward the criterion. Full list in `COVERAGE-data.json` (`z11Openness.pairs`, kind `none`).
- This confirms the Z1.1 criterion for the existing samples: every value stored in the file that Openness shows as an attribute, service property or node attribute is emitted and matches Openness. Additionally tested against Openness, but not in this table: I/O addresses (addressList), HW identifiers, device tree and type identifiers (including GSD with interfaces/ports from the GSDML), subnets, IO systems, connections, port interconnection, sync domain, certificates, UMAC users and roles, Safety (F settings, F runtime group, F I/O, F password as set). Still open are settings without sample (channel parameters, further F modules).

| Subtype | Parameter (file) | Openness | Occurrences | equal |
|---|---|---|---|---|
| S71500.CPU | `Failsafe_Central_F_WD_Time` | `Failsafe_DefaultFmonitoringTimeCentral` | 2 | 2 |
| S71500.CPU | `SelectedOpcUaLicense` | `OpcUaPurchasedLicense` | 12 | 12 |
| S71500.CPU | `SuppressDeactivatingSystemDiagnosticsAlarms` | `SuppressDeactivatingSystemDiagnosticsAlarms` | 8 | 8 |
| S71500.CPU | `WebServerActive` | `WebserverActivate` | 4 | 4 |
| S71500.CPU.CPUexecUnit | `MaxCycleConfig` | `CycleMaximumCycleTime` | 1 | 1 |
| S71500.CPU.CPUexecUnit | `PowerOnAction` | `StartupActionAfterPowerOn` | 1 | 1 |
| S71500.CPU.CpuCommon | `AccountLockedFor` | `PlcAccountLockingAtRuntimeFeature.LockAccountForDuration` | 7 | 7 |
| S71500.CPU.CpuCommon | `ClockFlags_Local` | `ClockMemoryByteAddress` | 1 | 1 |
| S71500.CPU.CpuCommon | `EnableDaylightSavingsTime` | `TimeOfDayActivateDaylightSavingTime` | 9 | 9 |
| S71500.CPU.CpuCommon | `EnableLockUserAccountAtRunTime` | `PlcAccountLockingAtRuntimeFeature.EnableLockUserAccountAtRunTime` | 7 | 7 |
| S71500.CPU.CpuCommon | `LocalTimeZone` | `TimeOfDayLocalTimeZone` | 9 | 9 |
| S71500.CPU.CpuCommon | `NoOfFailedLoginAttempts` | `PlcAccountLockingAtRuntimeFeature.NumberOfFailedLoginAttempts` | 7 | 7 |
| S71500.CPU.CpuCommon | `TimeBetweenFailedLoginAttempts` | `PlcAccountLockingAtRuntimeFeature.LoginRetryInterval` | 7 | 7 |
| S71500.CPU.CpuCommon | `UseClockFlags` | `ClockMemoryByte` | 1 | 1 |
| S71500.CPU.Interface.IE | `EnableWebServerAccess4IE` | `UseWebServerForInterface` | 4 | 4 |
| S71500.CPU.Interface.IE | `TimeSyncInterval` | `TimeSynchronizationUpdateInterval` | 10 | 10 |
| S71500.CPU.Interface.IE | `TimeSyncNtpServer1` | `TimeSynchronizationServer1` | 20 | 20 |
| S71500.CPU.Interface.IE | `TimeSyncNtpServer2` | `TimeSynchronizationServer2` | 10 | 10 |
| S71500.CPU.Interface.IE | `TimeSyncNtpServer3` | `TimeSynchronizationServer3` | 10 | 10 |
| S71500.CPU.Interface.IE | `TimeSyncNtpServer4` | `TimeSynchronizationServer4` | 10 | 10 |
| S71500.CPU.Interface.IE | `TimeSyncRole` | `TimeSynchronizationNtpV2` | 20 | 20 |
| S71500.SM.AI.4AI2AO | `QualityInformation` | `EnableValueStatus` | 1 | 1 |
| S71500.SM.DI.DI16 | `Failsafe_CRC_3` | `Failsafe_FParameterSignatureIndividualParameters` | 1 | 1 |
| S71500.SM.DI.DI16 | `Failsafe_F_Par_CRC` | `Failsafe_FParameterSignatureWithAddresses` | 1 | 1 |
| S71500.SM.DI.DI16 | `Failsafe_F_Par_CRC_WithoutAddresses` | `Failsafe_FParameterSignatureWithoutAddresses` | 1 | 1 |
| Node | `ipAddress` | `Address` | 31 | 31 |
| Node | `profibusAddress` | `Address` | 9 | 9 |
| Node | `profinetDeviceName` | `PnDeviceName` | 1 | 1 |
| Node | `subnetMask` | `SubnetMask` | 31 | 31 |
| S71500.CPU | `PnDnsConfigNameResolve` | `PnDnsConfigNameResolve` | 21 | 0 |

## Details (output of tools\coverage_matrix.py)

Matrix as of: 2026-11-07. Sample 229_hw_gsd is no longer in the repo (it contained vendor GSDML from SEW-EURODRIVE and Danfoss) and is therefore missing in this run.

Samples: 245 (000_empty, 001_devgroup, 002_cpu, 003_tagtable, 004_fb, 005_tag_int, 006_tag_addr, 007_tag_comment, 008_tag_flags, 009_tag_rename, 010_dev_in_group, 011_tagtable_folder, 012_tags_multi, 013_tag_umlaut, 014_tag_delete, 015_cpu2, 016_tag_accessible, 017_udt, 018_db_global, 019_db_instance, 020_block_group, 021_mastercopy, 022_db_udt, 023_fb_iface, 024_fc_iface, 025_fb_idb, 026_fc_scl, 027_fc_lad, 028_fc_fbd, 029_fb_idb_value, 030_scl_all, 031_lad_all, 032_fbd_all, 034_flg_misc, 035_scl_rest, 036_tech, 037_lad_xml, 038_comm_hw, 039_fw41, 040_s71200, 041_plc_alarm_fb, 042_plc_alarm_idb, 043_plc_alarm_text, 044_plc_alarm_class, 045_plc_textlist, 046_plc_textlist_entry, 047_plc_watch_table, 048_plc_watch_entry, 049_plc_unit, 050_plc_unit_b, 051_plc_unit_relation, 052_plc_to_param, 053_plc_to_inverse, 054_plc_opcua_interface, 055_plc_opcua_disabled, 056_plc_prodiag_fb, 057_plc_prodiag_supervision, 058_plc_textlist_range, 059_db_symbols, 060_db_attrs, 061_fc_stl, 062_fb_stl, 063_multi_plc, 064_stl_all, 065_db_iec_timer, 066_sys_fb_instances, 067_tsend_trcv_single, 068_motion_tos, 069_safety_compiled, 070_db_khp_units, 071_db_1200_300, 072_motion_tcpu, 073_code_stress, 074_fb_graph, 075_stl_calls, 076_graph_actions, 077_graph_branches, 078_plc_force_entry, 079_plc_to_hw_connect, 100_hmi_device, 101_hmi_base, 102_hmi_tag_int, 103_hmi_tag_ext, 104_hmi_tag_cycle, 105_hmi_tag_comment, 106_hmi_tagtable_group, 107_hmi_alarmclass, 108_hmi_alarm_discrete, 109_hmi_alarm_analog, 110_hmi_screen, 111_hmi_screen_rename, 112_hmi_screen_backcolor, 113_hmi_screen_group, 114_hmi_screen2, 115_hmi_item_rect, 116_hmi_item_rect_pos, 117_hmi_item_rect_color, 118_hmi_item_text, 119_hmi_screen_tag, 120_hmi_item_iofield, 121_hmi_item_iofield_pvtext, 122_hmi_item_button, 123_hmi_button_event, 124_hmi_item_iofield_dyn, 126_hmi_subnet, 128_hmi_item_polyline, 129_hmi_item_listbox, 130_hmi_script_multiline, 131_hmi_event_down, 132_hmi_dyn_color, 133_hmi_dyn_script, 134_hmi_dyn_range3, 135_hmi_item_alarmind, 136_hmi_datalog, 137_hmi_datalog_size, 138_hmi_logging_tag, 139_hmi_alarmlog, 140_hmi_logging_cyclic, 141_hmi_datalog_period, 142_hmi_alarmlog_size, 143_hmi_logging_cycle, 144_hmi_datalog_backup, 145_hmi_rt_gmp, 146_hmi_audittrail_size, 147_hmi_rt_panning, 148_hmi_rt_maxlogin, 149_hmi_rt_maxlogin_errors, 150_hmi_rt_prodiag, 151_hmi_rt_opcua, 152_hmi_rt_reporting, 153_hmi_lang_de, 154_hmi_lang_enable, 155_hmi_lang_logging, 156_hmi_script_module, 157_hmi_script_globaldef, 158_hmi_textlist, 159_hmi_graphiclist, 160_hmi_plantview, 161_hmi_plantview_node, 162_hmi_opcua_connection, 163_hmi_opcua_alarmtype, 164_hmi_pc_base, 165_hmi_item_defaults, 168_hmi_graphiclist_entry, 169_hmi_start_screen, 200_hw_cycle_time, 201_hw_clock_memory, 202_hw_startup, 203_hw_webserver, 204_hw_access_level, 205_hw_di_module, 206_hw_io_start, 207_hw_ob_cyclic, 208_hw_process_image, 209_hw_dq_module, 210_hw_ip_address, 211_hw_pn_name, 212_hw_subnet, 213_hw_io_system, 214_hw_et200sp_station, 215_hw_io_subnet, 216_hw_io_device, 217_hw_io_module, 218_hw_s7_connection, 219_hw_tcp_connection, 220_hw_cm_dp, 221_hw_profibus, 222_hw_dp_master, 223_hw_dp_station, 224_hw_dp_subnet, 225_hw_dp_slave, 226_hw_port_link, 227_hw_send_clock, 228_hw_sync_domain, 230_hw_opcua_on, 231_hw_opcua_port, 232_hw_ntp, 233_hw_timezone, 234_hw_sysdiag, 236_hw_access_control, 237_hw_confidential, 238_hw_certificate, 239_hw_webserver, 240_hw_umac_user, 241_hw_umac_role, 242_hw_umac_user_role, 243_hw_aiaq_module, 244_hw_aiaq_value_status, 245_hw_tm_count, 246_hw_fcpu, 247_hw_f_settings, 248_hw_f_monitoring, 249_hw_fdi_module, 250_hw_fdi_dest_address, 251_hw_fdi_shortcircuit, 252_hw_f_runtime_group, 253_hw_f_password, 254_hw_channel_param, 255_hw_channel_param2, F01_hmi_allobjects, F05_fp_iface_instance, F12_iface_tags, F15_local_tag, F16_iface_props, F18_iface_event, F18b_event_handler, F18c_event_funclist, F20_type_props, F30_nested, F32_multilingual, F33_control_columns, F36_popup_alwaysdownload, L01_udt_type_inwork, L02_udt_type_released, L03_udt_type_v2, L04_default_changed, L05_fb_type_inwork, L06_fb_type_released, L07_fc_type, L08_type_in_folder, L09_type_depends, L10_mc_fc, L11_mc_fb_udt, L12_mc_two, L13_mc_instantiate, L14_mc_delete, L15_mc_db, L16_inst_two_devices, L17_inst_outdated, L18_inst_edit_outdated, L19_version_discarded, L20_default_two_devices, L21_fb_inst_outdated, L22_update_removes_unused, L23_inst_called, L24_inst_update_fb, L25_version_deleted, L26_type_obsolete, L27_inst_update_udt, L28_mc_in_folder, L29_version_content, L33_proj_update_from_glib, L34_proj_mc_from_glib, L35_device_from_mc, L37_hmi_lib_types)

| Goal | concrete object types in the metamodel | of which in samples | of which emitted by the parser | in samples, not emitted | without sample |
|---|---|---|---|---|---|
| Z1.1 | 89 | 40 | 26 | 14 | 49 |
| Z1.2 | 183 | 83 | 43 | 40 | 100 |
| Z1.3 | 282 | 73 | 46 | 27 | 209 |
| Z1.4 | 36 | 18 | 15 | 3 | 18 |
| Infrastructure | 53 | 12 | 5 | 7 | 41 |

### Z1.1: present in samples, not emitted

| Type | Package | Objects |
|---|---|---|
| SysDiagAlarmSettingData | Simatic.SystemDiagnosis.Model.V14.Mandatory | 980 |
| NotAssignedObjectsData | Simatic.HwConfiguration.Model | 245 |
| SysDiagSettingsContainerData | Simatic.SystemDiagnosis.Model.V14.Mandatory | 245 |
| UmacRootData | Automation.Umac | 243 |
| FBlockInfo | Simatic.Safety | 135 |
| HwcnCommentTextCategoryData | Simatic.HwConfiguration.Model | 16 |
| WebApplicationData | Simatic.Tech.WebApplication.DataModel | 14 |
| FChannelInfo | Simatic.Safety | 12 |
| FailsafeInfo | Simatic.Safety | 9 |
| FailsafeProgramSettings | Simatic.Safety | 9 |
| HwProcessEvent | Simatic.HwConfiguration.Model | 4 |
| RoleToUserAssociation | Automation.Umac | 2 |
| HwcnProjectData | Simatic.HwConfiguration.Model | 2 |
| FailsafeCompilationInfo | Simatic.Safety | 1 |

### Z1.1: packages with types without sample

| Package | Types without sample |
|---|---|
| Simatic.HwConfiguration.Model | 12 |
| Simatic.Safety | 10 |
| Simatic.OpcUa.Model | 8 |
| Simatic.HwConfiguration.SecurityNg.Model | 6 |
| Automation.Umac | 5 |
| Simatic.SystemDiagnosis.Model | 3 |
| Simatic.SystemDiagnosis.Model.V14.Mandatory | 2 |
| Simatic.HwConfiguration.CirModel | 1 |
| Simatic.HwConfiguration.IndustrialRc.Model | 1 |
| Simatic.Rse.Model | 1 |

### Z1.2: present in samples, not emitted

| Type | Package | Objects |
|---|---|---|
| DataTypeItemData | Automation.DomainModel | 84417 |
| SystemStructureData | Automation.DomainModel | 5346 |
| IECPLInstructionProxyData | Simatic.Lang | 3439 |
| AlarmText | Simatic.AlarmServices.ObjectFrame | 3244 |
| SystemTextRangeData | Automation.DomainModel | 3153 |
| LoadableCodeBlockData | Simatic.Lang | 1628 |
| AlarmTextCategory | Simatic.AlarmServices.ObjectFrame | 417 |
| LibraryBlockInterfaceContainer | Simatic.Lang | 379 |
| TextListFingerprintContainer | Simatic.AlarmServices.Model.LangConnector.TextLists.V14 | 274 |
| ElementaryDataTypesContainerData | Automation.DomainModel | 245 |
| FormattedMotionAlarmTextCategory | Simatic.AlarmServices.Model.V13 | 245 |
| FormattedUserAlarmTextCategory | Simatic.AlarmServices.Model.V13 | 245 |
| SimotionAlarmTextCategory | Simatic.AlarmServices.Model.V13 | 245 |
| SystemAlarmTextCategory | Simatic.AlarmServices.Model.V13 | 245 |
| AlarmClassDisplayNameCategory | Simatic.AlarmServices.ObjectFrame | 245 |
| LibraryBlockInterfaceRootContainer | Simatic.Lang | 243 |
| MddCache | Simatic.Lang.Global.MddCache | 243 |
| SupervisionSubCategoryData | Simatic.Lang.ProDiag | 160 |
| SupervisionTypeData | Simatic.Lang.ProDiag | 50 |
| StandardDataBlockDataBagData | Simatic.Lang | 47 |
| SupervisionCategoryData | Simatic.Lang.ProDiag | 40 |
| SupervisionSettingData | Simatic.Lang.ProDiag | 35 |
| TechnologicalDataBlockProxyData | Simatic.Lang.TOSupport | 28 |
| GlobalProxyTextListData | Simatic.AlarmServices.Model.V14 | 20 |
| LoadableHWConfigurationS7Data | Automation.DomainModel | 19 |
| TextListTextCategoryData | Automation.DomainModel | 14 |
| CyclicInterruptEventData | Simatic.Lang | 11 |
| UserTextRangeData | Automation.DomainModel | 7 |
| CyclicServoEventData | Simatic.Lang | 6 |
| IpoEventData | Simatic.Lang | 6 |
| ProDiagSettingsContainerData | Simatic.Lang.ProDiag | 5 |
| ProDiagTextCategory | Simatic.Lang.ProDiag | 5 |
| TagCommentCategoryData | Automation.DomainModel | 3 |
| AlarmServicesDataContainer | Simatic.AlarmServices.Model.LangConnector.V14 | 2 |
| BracketGroupForSWObjects | Simatic.Lang | 2 |
| DebugInformationData | Simatic.Lang | 2 |
| LoadableDataBlockData | Simatic.Lang | 2 |
| SpecificText | Simatic.Lang.ProDiag | 1 |
| SupervisionTextCategoryData | Simatic.Lang.ProDiag | 1 |
| LookAheadEventData | Simatic.Lang.S7Block_V16 | 1 |

### Z1.2: packages with types without sample

| Package | Types without sample |
|---|---|
| Simatic.Lang | 26 |
| Simatic.Lang.Idents | 25 |
| TechTrace.Editor.Model | 17 |
| Automation.DomainModel | 8 |
| Automation.DomainModel.TextLists | 3 |
| Simatic.AlarmServices.Model.V13 | 3 |
| Simatic.AlarmServices.ObjectFrame | 3 |
| Simatic.Lang.ProDiag | 3 |
| Automation.DomainModel.OnlineBackup | 2 |
| Simatic.AlarmServices.ObjectFrame.S7400 | 2 |
| TechTrace.Device.Plc.Model.V14 | 2 |
| Simatic.AlarmServices.Model.V14 | 1 |
| Simatic.AlarmServices.ObjectFrame.S71500S71200.V12 | 1 |
| Simatic.AlarmServices.ObjectFrame.S7500S71200 | 1 |
| Simatic.Lang.FingerprintContainers | 1 |
| Simatic.Lang.S7Block_V16 | 1 |
| Simatic.Lang.SiVArcSupport | 1 |

### Z1.3: present in samples, not emitted

| Type | Package | Objects |
|---|---|---|
| HmiTextData | Simatic.Hmi.Globalization | 36021 |
| HmiTextListEntryData | Simatic.Hmi.TextAndGraphicLists | 23091 |
| HmiSystemEventData | Simatic.Hmi.Alarm | 12529 |
| ILDeviceLayerData | Simatic.Hmi.DL.ModernUI | 2592 |
| HmiILSettingsData | Simatic.Hmi.Device | 1214 |
| HmiAreaPointerData | Simatic.Hmi.Communication | 902 |
| HmiAlarmTextBlockData | Simatic.Hmi.Alarm | 810 |
| HmiInternalImageData | Simatic.Hmi.Globalization | 402 |
| ReportStorageLocationData | Simatic.Hmi.DL.Reporting | 162 |
| HmiAlarmAuditClassData | Simatic.Hmi.DL.Audit | 88 |
| HmiParameterSetTypeAuditClassData | Simatic.Hmi.DL.Audit | 88 |
| HmiAlarmSettingsData | Simatic.Hmi.Alarm | 81 |
| ILDeviceLayerSettingsData | Simatic.Hmi.DL.ModernUI | 81 |
| HmiRtCollabExportData | Simatic.Hmi.DL.RtCollab | 81 |
| HmiRuntimeSettingsData | Simatic.Hmi.Device | 81 |
| HmiStyleAndThemeSettingsData | Simatic.Hmi.Device | 81 |
| ImageSettingsData | Simatic.Hmi.Device | 81 |
| HMI_runtime | Simatic.Hmi.Globalization | 81 |
| HMI_system_event | Simatic.Hmi.Globalization | 81 |
| HmiGraphXSettingsData | Simatic.Hmi.GraphX | 81 |
| HmiTagSettingsData | Simatic.Hmi.Tag | 81 |
| HmiMobileSettingsData | Simatic.Hmi.Wireless | 81 |
| ControlPanelAccessControlConfigData | Simatic.Hmi.Umac | 80 |
| HmiBackupStrategyData | Simatic.Hmi.DL.Logging | 23 |
| HmiScriptGlobalData | Simatic.Hmi.DL.Scripting | 3 |
| HmiFunctionListEmbeddedData | Simatic.Hmi.DL.SystemFunctions | 3 |
| ILOpcUaAlarmData | Simatic.Hmi.DL.Alarm | 1 |

### Z1.3: packages with types without sample

| Package | Types without sample |
|---|---|
| Simatic.Hmi.DL.Reserve | 46 |
| Simatic.Hmi.Controls | 20 |
| Simatic.Hmi.GraphX | 17 |
| Simatic.Hmi.Scripting | 13 |
| Simatic.Hmi.ScreenModules | 10 |
| Simatic.Hmi.Common | 9 |
| Simatic.Hmi.Style | 9 |
| Simatic.Hmi.Logging | 7 |
| Simatic.Hmi.Wireless | 6 |
| Simatic.Hmi.Alarm | 5 |
| Simatic.Hmi.DL.SystemFunctions | 5 |
| Simatic.Hmi.Globalization | 5 |
| Simatic.Hmi.RTSecurity | 5 |
| Simatic.Hmi.Recipe | 5 |
| Simatic.Hmi.DL.ColorPalette | 4 |
| Simatic.Hmi.DL.ModernUI | 4 |
| Simatic.Hmi.DL.ImageLib | 3 |
| Simatic.Hmi.DL.Scripting | 3 |
| Simatic.Hmi.ParameterControl | 3 |
| Simatic.Hmi.Report | 3 |
| Simatic.Hmi.TextAndGraphicLists | 3 |
| Simatic.Hmi.DL.Audit | 2 |
| Simatic.Hmi.DL.Reporting | 2 |
| Simatic.Hmi.Device | 2 |
| Simatic.Hmi.Globalization_V15.1 | 2 |
| Simatic.Hmi.Convert | 1 |
| Simatic.Hmi.Curve | 1 |
| Simatic.Hmi.DL.Alarm | 1 |
| Simatic.Hmi.DL.Common | 1 |
| Simatic.Hmi.DL.Dynamics | 1 |
| Simatic.Hmi.DL.Logging | 1 |
| Simatic.Hmi.DL.Recurrence | 1 |
| Simatic.Hmi.DL.RtCollab | 1 |
| Simatic.Hmi.DL.Scheduler | 1 |
| Simatic.Hmi.DL.SmtpServerConfiguration | 1 |
| Simatic.Hmi.DL.StorageMedia | 1 |
| Simatic.Hmi.Job | 1 |
| Simatic.Hmi.SettingsEditor | 1 |
| Simatic.Hmi.Tag | 1 |
| Simatic.Hmi.Tag.HmiProxyForPLCStructureType | 1 |
| Simatic.Hmi.Theme | 1 |

### Z1.4: present in samples, not emitted

| Type | Package | Objects |
|---|---|---|
| PackageContainerData | Automation.CommonModel.ReleaseManagement | 12188 |
| PackageContainerDatas | Automation.CommonModel.ReleaseManagement | 245 |
| ProductVersionInfoContainer | Automation.CommonModel.ReleaseManagement | 245 |

### Z1.4: packages with types without sample

| Package | Types without sample |
|---|---|
| Simatic.Lang.Libraries | 7 |
| Simatic.Hmi.DL.Library.ResourceListLib | 6 |
| Automation.CommonModel.ReleaseManagement | 1 |
| Automation.CommonServices.Library.SystemTypes | 1 |
| Automation.CommonServices.Library.UserTypes | 1 |
| Simatic.Hmi.DL.Library.DynamicSVGLib | 1 |
| Simatic.Hmi.DL.Library.ScriptModuleLib | 1 |

### Infrastructure: present in samples, not emitted

| Type | Package | Objects |
|---|---|---|
| CoreText | Automation.ObjectFrame.Core | 6369 |
| CoreCulture | Automation.ObjectFrame.Core | 1460 |
| CoreCultureContainer | Automation.ObjectFrame.Core | 245 |
| CorePersistenceInfo | Automation.ObjectFrame.Core | 245 |
| ChangeListData | Automation.ObjectFrame.Core | 81 |
| ScopedLinkMaintainerData | Automation.DomainModel.LinkMaintainer | 19 |
| CpmData | Simatic.CPM.Model | 4 |

### Infrastructure: packages with types without sample

| Package | Types without sample |
|---|---|
| Simatic.Sivarc.DL | 16 |
| Simatic.Sivarc.V14 | 11 |
| Automation.CommonModel.Printing | 7 |
| Automation.ObjectFrame.Core | 3 |
| Simatic.CPM.Model | 2 |
| Automation.CommonServices.IPI.Core | 1 |
| Simatic.Sivarc.V15 | 1 |
