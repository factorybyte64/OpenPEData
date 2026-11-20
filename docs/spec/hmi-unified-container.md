# WinCC Unified: where the data lives (established, U1)

Evidence: sample 100_hmi_device (copy of 002_cpu + HMI_1 = MTP700 Unified Comfort, MLFB 6AV2 128-3GB06-0AXx, FW 21.0.1.0, HMI connection to PLC_1), commit 2d254c1 on the user's machine, diff `samples\100_hmi_device_diff.txt`, truth `samples\100_hmi_device_truth\`. Only device and connection, **no screen and no tag yet**.

## Two storage locations
1. **PEData.plf (engineering model):** records 674 → 1996 (+1322), 64 types with changed count; plf 641 kB → 2.94 MB (zap 537 kB → 2.62 MB), idx 29 → 82 kB. Metamodel 42 → 100 packages, 980 → 1785 types; the 58 new ones are `Siemens.Simatic.Hmi.*` (ID range 0x00b0…0x00ed) or Sivarc. Note: always take the package name from the metamodel XML, not from `id>>16` (several packages share the value).
2. **Separate files outside the plf, contained in the .zap21** (correction to 00-overview.md: IM\* is in the archive for HMI devices):
   - `IM\HMI\…`: presumably compiled runtime configuration. 21 `.rdf` (magic `52 44 46 00 15 00 02 01`, cds.rdf 2.57 MB, config_AlertClass, config_AlertHandler, textLibrary_*, style_*, ES2RTID), 2 zips (web controls: js/json/fonts), fonts, DownloadTask.xml, UMCData.json, TiaUmcConfig.json, *.pbb, config.level, CompileTimestamp.txt, `AdditionalFiles\0_Chrom_21.0.1.0_Rdf_21.0.1.0\{CAI,HIDS,IAH,LCSI}.bin`, BrokerInfo.dat, DeviceFolders.dat.
   - `AdditionalFiles\RtIdFiles\<GUID>\*.rtid` (8 files, ValueTable.rtid 42 kB), `AdditionalFiles\ValidationFiles\…`.
   - New Lucene segments in `IM\SearchIndex`; .ap21, ProjectInfo.txt changed; XRef.db same size, different content.
   Open: whether the RDF/RTID files are a source or only derived (compile result). The engineering content (tags, alarms, screens) is primarily expected in the plf (confirmation in U2–U5).

## Unified types (type ID, objects in 100)
| Type | Type ID | Package | n |
|---|---|---|---|
| HmiDeviceData | 0x00b51001 | Hmi.Device | 1 |
| HmiConnectionData | 0x00b21002 | Hmi.Communication | 1 |
| HmiTagTableData | 0x00e51004 | Hmi.Tag | 1 |
| HmiTagData | 0x00e51008 | Hmi.Tag | 0 |
| HmiSystemTagData | 0x00e51007 | Hmi.Tag | 29 |
| HmiInternalSystemTagData | 0x00e51607 | Hmi.InternalSystemTag | 59 |
| HmiAlarmClassData | 0x00c01002 | Hmi.Alarm | 18 |
| HmiDiscreteAlarmData | 0x00c01009 | Hmi.Alarm | 0 |
| HmiAnalogAlarmData | 0x00c01006 | Hmi.Alarm | 0 |
| HmiSystemEventData | 0x00c0100c | Hmi.Alarm | 145 |
| ILScreenData | 0x00cd1002 | DL.ModernUI | 0 |
| ILScreenItemData | 0x00cd1003 | DL.ModernUI | 0 |
| ILFaceplateTypeData | 0x00cd1004 | DL.ModernUI | 0 |
| ILFaceplateContainerData | 0x00d30006 | DL.FaceplateLib | 0 |
| HmiScriptModuleContainerData | 0x00e31003 | DL.Scripting | 0 |
| HmiScriptGlobalModuleData | 0x00e31007 | DL.Scripting | 0 |
| HmiScriptEmbeddedData | 0x00e31002 | DL.Scripting | 0 |
| HmiTextListData | 0x00e81005 | TextAndGraphicLists | 25 |
| HmiScreenData / HmiScreenItemData (Classic) | 0x00cb1005 / 0x00cb100a | GraphX | 0 |
Large new quantities: HmiTextData 0x00c81005 (452), HmiTextListEntryData 0x00e81007 (285), ILDeviceLayerData 0x00cd1006 (32), HmiILSettingsData 0x00b51019 (15), HmiAreaPointerData 0x00b21001 (11), cycles (7).
HW/project side: HmiApplicationData 0x00101006 and HmiConnectionPointData 0x00101007 1 each (HmiTargetData 0x00101005 stays 0), NonProgrammableTargetData 0x00101018 +2, DeviceItemData 14 → 20, AlarmServices.AlarmClassData 2 → 20 (mirrors 18 HMI alarm classes), UMAC SystemRoleProxy 0 → 4.

## Ground truth (MCP)
HMI software: 0 screens, 0 tags, 1 tag table, 29 system tags, 1 connection, 18 alarm classes, 25 system text lists. According to Openness `export connection` is not available; the truth is the `get` queries.

## Anomalies
- After `hmiConnectionIntegrate`, `hwIsValid:false`, both interfaces without subnet (despite connectSubnet=true), partners/addresses are correct.
- Parser: `attrdiff.py` aborts on 2 of 1980 objects ReportStorageLocationData 0x00d91002: expando block `IAdditionalAttributes` only 6 bytes (`06 00 00 00 01 00`), `decode_expando` expects ≥ 8. New task for the parser (H-023).
- Unified screens presumably run via IL/DL types (DL.ModernUI); only evidenced with a screen in U2/U4.
