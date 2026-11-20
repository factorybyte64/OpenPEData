# HMI/Unified in the metamodel (preparation U1, without TIA)

As of 2026-10-19. No TIA calls, no attach. Tools: `tools\unified_metamodel.py` (metamodel record `0000:0007:1` of all samples) and `tools\unified_install_meta.py` (read-only inventory of the `*.mpk` of the TIA installation). Raw outputs are not in the repo; both scripts regenerate them:

```
"%LOCALAPPDATA%\Programs\Python\Python312\python.exe" -I tools\unified_metamodel.py --types
"%LOCALAPPDATA%\Programs\Python\Python312\python.exe" -I tools\unified_install_meta.py "C:\Program Files\Siemens\Automation\Portal V21"
```

Search pattern for names: `Hmi|Unified|Screen|Alarm|Connection|Faceplate|Script` (without "…scription").

## 1. Metamodel of the existing samples

25 samples (000–022, without `_analysis`/`_truth`), in each case the last version of `0000:0007:1`.

| Samples | Metamodel (SHA-256, 12 characters) | Packages | Types |
| --- | --- | --- | --- |
| 000_empty, 000_empty2, 000_empty_resave, 001_devgroup | 797b0ab9016d | 28 | 561 |
| 002_cpu … 022_db_udt (all with CPU) | b57ffbb7a044 | 42 | 980 |

**No sample has a package with Hmi, Unified, Screen, Faceplate or Script in its name.** The metamodel contains only the packages that the project uses: the CPU added 14 packages and 419 types. An HMI device will therefore presumably bring further packages into the project metamodel; this can only be evidenced with U1.

Hits that already exist without an HMI device (76 types, 10 of them with objects):

| Package (package ID) | Types (type ID) | Objects |
| --- | --- | --- |
| Family 0x0040 AlarmServices: `…AlarmServices.ObjectFrame` 0x00400001, `.S7300S7400` 0x00400002, `.S7400` 0x00400003, `.S7500S71200` 0x00400004, `.S71500S71200.V12` 0x00400005, `…Model.V13` 0x00400006, `.V13SP1` 0x0040000a, `.V14` 0x0040000b; from 002 additionally `…Model.LangConnector.TextLists.V14` 0x0040000c and `…LangConnector.V14` 0x0040000d | AlarmClassData 0x00401003 | 2 (from 000) |
| | AlarmTextCategory 0x00401009, AlarmClassDisplayNameCategory 0x0040100a, FormattedUserAlarmTextCategory 0x00401012, SystemAlarmTextCategory 0x00401014, SimotionAlarmTextCategory 0x00401015, FormattedMotionAlarmTextCategory 0x00401027 | 1 each (from 000) |
| | AlarmContainerV2 0x00401010 (package `.S71500S71200.V12`) | 1 per CPU (002: 1, 015_cpu2: 2) |
| | InternalDisplayClassV2Data 0x0040102a (package `Model.V13SP1`) | 57 per CPU (002: 57, 015_cpu2: 114) |
| | without objects: AlarmCoreData 0x00401001 (abstract), AlarmData 0x00401002, AlarmContainer 0x0040100f, ControllerAlarmData 0x0040100b (abstract), SimpleControllerAlarmData 0x0040100c, ControllerSubAlarmData 0x0040100d, StructuredControllerAlarmData 0x0040100e, TypeInstanceConnectorData 0x00401011, AlarmText 0x00401008, text lists 0x0040102b–0x0040102d, AlarmServicesDataContainer 0x0040102f; AttributeSets IAlarmClassData 0x00403001, IAlarmData 0x00403002 and others | 0 |
| `Siemens.Simatic.SystemDiagnosis.Model.V14.Mandatory` 0x00410005 | SysDiagAlarmSettingData 0x0041100e | 4 (from 000) |
| | SysDiagAlarmTextData 0x00411010 | 0 |
| `Siemens.Simatic.HwConfiguration.Model` 0x00100001 (namespaces `…HwConfiguration.Hmi.Model` and `…Connections.Model`) | HmiTargetData 0x00101005, HmiApplicationData 0x00101006, HmiConnectionPointData 0x00101007, AttributeSet IHmiApplicationAttributes 0x00103001, 12 enumerations 0x00105001–0x0010500c (HmiApplicationRtId, PreferredServerType, TransferModeEnum …); ConnectionPointData 0x00101001 (abstract), BlockConnectionPointData 0x00101002, LinkPointData 0x00101003, BehaviourTypes Conn2Conn 0x00107001, Target2RemoteConns 0x00107002, Conn2RemoteNodes 0x00107003 | 0 |
| `Siemens.Simatic.Lang.Idents` 0x00220201 | AlarmData 0x00221204, IAlarmData 0x00223204 | 0 |
| `Siemens.Simatic.OpcUa.Model` 0x0212f000 | IOpcUaClientConnectionData 0x02120003 | 0 |

The alarm-related objects (AlarmClassData, text categories, SysDiagAlarmSettingData, from CPU on AlarmContainerV2 and InternalDisplayClassV2Data) belong to the project or the PLC, not to an HMI device. Whether the HMI alarm classes of WinCC Unified are mapped onto them or get their own types will be shown by U1.

Observation on the parser: `Metamodel.packages` (`parser\openpedata\metamodel.py`) is keyed by `id >> 16`. Several packages share this value (family 0x0040 has 10 packages, 0x0010 has 6). The dictionary therefore contains only 9 instead of 28 or 15 instead of 42 packages, and the name belongs to the package read last. `tools\unified_metamodel.py` therefore determines the package of a type via the enclosing `Package` element in the XML. I did not change the parser.

## 2. Metamodel files of the TIA installation

`C:\Program Files\Siemens\Automation\Portal V21`, read only.

- The package definitions are present as **`*.mpk`**: 2903 files, zip archives with a total of 33004 entries. The entries are stored uncompressed (compress_type 0).
- 27932 entries start with the bytes `78 7d 03 ee 00 ef`; their entropy is 7.2–8.0 bits/byte.
  - They are not plain-text XML and are not a zlib or deflate stream at any offset from 0 to 23; I suspect a custom encoding or encryption.
  - Package, ObjectType and AttributeSet names can **not** be read from them without further analysis. I did not attempt to decode them.
- The remaining entries are normal zlib streams (`78 5e …`). They mainly contain `.xsd` and `.rdf` files, for example in `Data\Hmi\ItLikeEs\…\Siemens.Simatic.Hmi.Common.ChromDataExchange.mpk`, and no metamodel packages.
- Only the file and entry names are therefore readable. The entry names follow the pattern `Simatic-Hmi-<Name>[-<Version>].xml` and thus probably correspond to the package names.

| Location | Content (entry names) |
| --- | --- |
| `Meta\Model\WinCC.BCA.Code.mpk` | 218 entries `Simatic-Hmi-*.xml`, without version number, so probably the current status (list below) |
| `Meta\Model\<14.0.0.0 … 20.0.0.0>\Siemens.Simatic.Hmi.Global.mpk` | 125–134 entries per version, e.g. `Simatic-Hmi-HmiAlarmClass-14.0.0.0.xml`; probably older model versions for upgrading |
| `Meta\Model\<15.0.0.0 … 20.0.0.0>\Siemens.Simatic.Hmi.DL.*.mpk` | Alarm, ColorPalette, Dynamics, DynamicSVGLib, FaceplateLib, Globalization, ILStyles, ImageLib, Logging, ModernUI, ParameterControl, Recurrence, Reserve, ResourceListLib, RtCollab, Scheduler, Scripting (9 entries), ScriptModuleLib, SystemFunctions, Umac |
| `Meta\Model\21.0.0.0\` | only `Siemens.Simatic.Hmi.DL.ParameterControl.mpk` (ParameterSet, ParameterSetType, ParameterSetTypeUdtVersionInstanceData) |
| `Meta\Model\Platform.All.mpk` | `Hwcn-Itf-Hmi-Model.xml`, `Hwcn-Itf-Online-Hmi-Model.xml` |
| `Meta\Model\<14…20>\Siemens.Automation.Simatic.CPM.HMI.Model.mpk` (from 16.0) | CPM HMI model (plant objects) |
| `Meta\EngineeringObjectModel\21.0.0.0\Siemens.Engineering.HmiMeta.21.0.0.0.mpk` | `WinCCflexible.XML` (122 kB), `WinCCflexible_extended.XML` (314 kB) |
| `Meta\EngineeringObjectModel\21.0.0.0\Siemens.Engineering.HmiMeta.UA.21.0.0.0.mpk` | 25 entries (Openness object model for Unified) |
| `Meta\CommonTypeSystemTypeInfo\Platform.All.mpk`, `…ParserInfo\…`, `Meta\PackageContainer\Platform.All.mpk` | `Automation-DomainServices-HMI_TypeInfo.xml`, `…HMI_ParserInfo.xml`, `Simatic-DataTypes-HMI.xml` |

Entry names in `Meta\Model\WinCC.BCA.Code.mpk`, without the prefix `Simatic-Hmi-` and the extension `.xml`:

AuditClass, ColorPalette, Dynamics-Model, DynamicSVGLib, Es2rt-Target-Es2rtCoreObject, FaceplateLib, FunctionList, GlobalSection, GlobalSectionSimple, HmiAddressData, HmiAlarm, HmiAlarmClass, HmiAlarmGroup, HmiAlarmLog, HmiAlarmSettings, HmiAlarmTextBlock, HmiAnalogAlarm, HmiAnalogAlarmLimit, HmiArrayMemberTag, HmiAuditTrailLog, HmiBackupStrategy, HmiBase, HmiBinaryResource, HmiBoxBasedZone_V13.0.1, HmiCommunication, HmiCompressedDataLog, HmiCompressedDataLoggingTag, HmiControlBlock, HmiControlFilter, HmiControllerAlarm, HmiControlOperatorAlarm, HmiControlStatusbarElement, HmiControlToolbarButton, HmiConvert, HmiCoreTag, HmiCurve, HmiCycle, HmiDataLog, HmiDataLoggingTag, HmiDevice, HmiDevice_V13SP1, HmiDeviceSettings, HmiDiscreteAlarm, HmiDynamicScreenItemResource, HmiEffectiveRangeObject, HmiFontItem, HmiFontList, HmiFontLocator, HmiFreeTextList, HmiFreeTextListEntry, HmiGraphicList, HmiGraphicListEntry, HmiGraphXSettings, HmiILSettings, HmiImage, HmiInternalImage, HmiInternalSystemTag, HmiIWLANConnector, HmiJob, HmiLanguageAndFontSettings, HmiLog, HmiLoggingSetting, HmiMemberTag, HmiMenu, HmiMenuHolder, HmiMenuItem, HmiMenuToolBarConfiguration, HmiMobileSettings, HmiMultiplexEntry, HmiOnlineTableColumn, HmiOnlyAlarm, HmiOverviewControl, HmiPrintConfiguration, HmiProcessValue, HmiProxyForPLCStructureType, HmiRawData, HmiRecipe, HmiRecipeData, HmiRecipeEntry, HmiRecipeView, HmiRecipeViewElement, HmiRecurrence, HmiReport, HmiReportSection, HmiRootTag, HmiRTAuthorization, HmiRTDeviceTagAssignment, HmiRTGroup, HmiRTSecuritySettings, HmiRTUser, HmiRuntimeSettings, HmiSchedulerTask, HmiScreen, HmiScreenItem, HmiScreenItem-Connector, HmiScreenItem-S7GraphOverview, HmiScreenItem-Softkey, HmiScreen-Library, HmiScreenModuleInstance, HmiScreenModuleType, HmiScreenModuleVersion, HmiScreenUdtVersionInstanceData, HmiSettingsEditor, HmiStorageDevice, HmiStructureMemberTag, HmiStructureType, HmiStructureTypeMember, HmiStyle, HmiStyleAndThemeSettings, HmiStyleFontList, HmiStyleItem, HmiStyleItem_V13.0.1, HmiStyleSheet, HmiStyleSheetType, HmiStyleSheetVersion, HmiStyleType, HmiStyleVersion, HmiSystemEvent, HmiSystemFunction, HmiSystemTag, HmiTag, HmiTagSettings, HmiTagTypeSafeAddress, HmiText, HmiTextList, HmiTextListEntry, HmiTheme, HmiToolbar, HmiToolBarHolder, HmiToolBarItem, HmiTrend, HmiTrendAxis, HmiTrendWindow, HmiUdtType, HmiUdtVersion, HmiUserEvent, HmiUserScript, HmiUserScript-Library, HmiValueAxisSegment, HmiZone, ILControllerAlarm, ILDynamics, ILFunctionList, IlHmiBase, ILScreen, ILStyle, ILStyleSettings, ILSystemFunction, ImageLib, Library-Instances, Library-Types, Library-Versions, Obsolete-* (37 entries), ParameterSet, ParameterSetType, ParameterSetTypeUdtVersionInstanceData, ReportStorageLocation, ReportTask, ReportTemplate, Reserve, ResourceListLib, RtCollab-Model, ScriptBase, ScriptEmbedded, ScriptGlobal, ScriptGlobalModule, ScriptModuleContainer, ScriptModuleLib, ScriptSimple, ScriptSimpleModule, Simatic-CPMHMI-Alarm-Model, Simatic-CPMHMI-Model, Simatic-Sivarc-Dl, Simatic-Sivarc-Model, Simatic-Sivarc-Model-RuleHistory, Simatic-Sivarc-Model-TextlistRules, Simatic-Sivarc-ModelV1200, Simatic-Sivarc-V14, Simatic-Sivarc-V15, SmtpServerConfiguration, ToolbarButtonDefinitions, Umac

Package IDs, ObjectType and AttributeSet names as well as type IDs of the HMI packages are thus **still unknown**. They can only be read from the metamodel record of a project with an HMI device (U1), there as zlib XML like the other packages.

## 3. MLFB of the MTP700 Unified Comfort

`TiaDevice(typeSearch)` runs via the hardware catalog of an attached TIA session (`TiaMcpXE.Bridge\HardwareCatalogQuery.cs`); without attach it does not work. At the time of the check (approx. 20:20 UTC), the MCP server was also not reachable ("Unable to connect"). I therefore did not call `typeSearch`.

The installation contains `Data\Hmi\Transfer\21.0\DeviceMapping_MTP700.xml` with `HWInfoName="MTP700 Unified Comfort"`, `transfername="MTP700_Unified_Comfort"`, `imagename="UCP_7_22"` and firmware image `UCP_7_22_V21_0.fwf`, but without an order number. Searching for `6AV2 128-3GB` in `Data\Hmi` returned nothing. The MLFB is to be determined in U1 with `typeSearch`.
