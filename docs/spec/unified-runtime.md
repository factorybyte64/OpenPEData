# WinCC Unified: panel runtime files (U8, H-022/H-025)

As of 2026-10-20. Question: are `IM\HMI\*` and `AdditionalFiles\RtIdFiles` source data that the parser needs? **Answer: no, derived compile/download data (cache).** The parser does not need them; they belong only in the file inventory. Their format is a **later task** (deferred by the user).

## Evidence

1. **Creation:** the files are created when the Unified device is created (U1: 100_hmi_device vs. 002_cpu, 22 new files under `IM\HMI`, 8 `RtIdFiles`, 2 `ValidationFiles`; `config\CompileTimestamp.txt` with the time of creation).
2. **Unchanged on project changes:** in all samples 102–124 (HMI tags, alarm classes, discrete/analog alarms, screens, screen groups, screen items, dynamization, event script) all 48 files under `IM\HMI`, `AdditionalFiles\RtIdFiles` and `AdditionalFiles\ValidationFiles` are **byte-identical** to 101_hmi_base (SHA-256). All this content is only in `System\PEData.plf`.
3. **Names only in the plf:** HmiTag_Ext1, HmiTag_IO, Screen_1, IOField_1, Alarm_1, HmiTagTable_1, AnalogAlarm_1 are (UTF-8/UTF-16LE) only in `PEData.plf`. The runtime files contain only names from the device creation (HMI_1, HMI_Connection_1, PLC_1, see table).
4. **Discarded on hardware change:** 126_hmi_subnet (124 + subnet PN/IE_1 for PLC and HMI) **deletes the whole snapshot** `IM\HMI\I\0\Saved\*` (31 files: all RDF, both zips, pbb, json, fonts, DownloadTask.xml, CompileTimestamp.txt); `CAI.bin`, `HIDS.bin`, `LCSI.bin` and one `ValidationMessages` change. The project remains fully readable (parser and tests unchanged green).
5. **A failed compile changes nothing:** compiling HMI_1 on a copy of 124 (error: connection without subnet) → project folder byte-identical to 124 (discarded).

## Compiling (not performed)

An error-free compile of HMI_1 needs the **runtime password of the panel** (HMI_1 → Runtime settings → Security/Panel; Openness V21 cannot set it, the user must set it once in TIA) and on the PLC side the password for confidential PLC configuration data or its deactivation, and the access level. Attempt 127_hmi_compiled aborted and discarded (`TiaProjectSecurity(plcMasterSecretUnprotect)` rejected by the permission check of the MCP client). Later task: after setting the passwords, compile and compare the files before/after (is `IM\HMI\I\0\Saved` regenerated, does `cds.rdf` change?).

## Rough structure (project folder 100_hmi_device, directly after creating the device)

- RDF files: magic `52 44 46 00 15 00 02 01` (`RDF\0`), 84 B to 2.57 MB (`cds.rdf`), entropy 2.4–6.8 bits/byte → uncompressed, tabular. Not decoded.
- Two zips (`{GUID}.zip`, 165 kB / 3.0 MB) with web controls (CPM plant model, ContextDialog: js, json, fonts).
- `*.pbb` (protobuf-like), `*.json` (UMC), `DownloadTask.xml`, `config.level`, fonts SISAN03.
- `RtIdFiles\<GUID>\*.rtid`: 7 × 44 B (header `00 00 00 00 05 00 00 00`, empty tables) and `ValueTable.rtid` (42 kB, entropy 7.4). According to the name runtime ID mappings; relation to plf OIDs not examined.
- `0_Chrom_21.0.1.0_Rdf_21.0.1.0\{CAI,HIDS,IAH,LCSI}.bin`.

| File (relative to the project folder) | Size | first 8 bytes | Entropy | Names in plain text |
| --- | --- | --- | --- | --- |
| `AdditionalFiles/RtIdFiles/a8d341bb-43d1-43c5-ab0b-5a25ab91ae1a/AlarmScope.rtid` | 44 | `00 00 00 00 05 00 00 00` | 4.55 |  |
| `AdditionalFiles/RtIdFiles/a8d341bb-43d1-43c5-ab0b-5a25ab91ae1a/GeneralScope.rtid` | 275 | `00 00 00 00 05 00 00 00` | 5.99 |  |
| `AdditionalFiles/RtIdFiles/a8d341bb-43d1-43c5-ab0b-5a25ab91ae1a/KeyMap.rtid` | 44 | `00 00 00 00 05 00 00 00` | 4.55 |  |
| `AdditionalFiles/RtIdFiles/a8d341bb-43d1-43c5-ab0b-5a25ab91ae1a/RtIdRecordKeyAdditionalAttributeTable.rtid` | 44 | `00 00 00 00 06 00 00 00` | 4.55 |  |
| `AdditionalFiles/RtIdFiles/a8d341bb-43d1-43c5-ab0b-5a25ab91ae1a/TagLoggingScope.rtid` | 44 | `00 00 00 00 05 00 00 00` | 4.55 |  |
| `AdditionalFiles/RtIdFiles/a8d341bb-43d1-43c5-ab0b-5a25ab91ae1a/ThresholdScope.rtid` | 44 | `00 00 00 00 05 00 00 00` | 4.55 |  |
| `AdditionalFiles/RtIdFiles/a8d341bb-43d1-43c5-ab0b-5a25ab91ae1a/ValueTable.rtid` | 42462 | `00 00 00 00 05 00 00 00` | 7.37 |  |
| `AdditionalFiles/RtIdFiles/a8d341bb-43d1-43c5-ab0b-5a25ab91ae1a/ValueTable4MovedObjects.rtid` | 44 | `00 00 00 00 05 00 00 00` | 4.55 |  |
| `AdditionalFiles/ValidationFiles/B3C21002100F000110B5005A0C000000000000/ValidationMessages` | 12 | `ff ff ff ff 03 00 00 00` | 1.28 |  |
| `AdditionalFiles/ValidationFiles/B3C21002100F0006101000200C000000000000/ValidationMessages` | 2933 | `ff ff ff ff 03 00 00 00` | 5.12 |  |
| `IM/HMI/BrokerInfo.dat` | 84 | `01 31 1c 53 74 72 69 6e` | 5.18 |  |
| `IM/HMI/DeviceFolders.dat` | 40 | `01 00 00 00 01 00 00 00` | 3.01 |  |
| `IM/HMI/I/0/Saved/DownloadTask.xml` | 2479 | `3c 3f 78 6d 6c 20 76 65` | 5.33 | HMI_1 |
| `IM/HMI/I/0/Saved/TiaUmcConfig.json` | 139 | `7b 0d 0a 20 20 22 76 65` | 4.44 |  |
| `IM/HMI/I/0/Saved/UMCData.json` | 1446 | `7b 0d 0a 20 20 22 76 65` | 4.13 |  |
| `IM/HMI/I/0/Saved/User.pbb` | 142 | `0a 8b 01 12 4f 0a 11 48` | 4.93 |  |
| `IM/HMI/I/0/Saved/config/CompileTimestamp.txt` | 28 | `32 30 32 36 2d 31 30 2d` | 3.38 |  |
| `IM/HMI/I/0/Saved/config/config.level` | 166 | `23 54 68 69 73 20 66 69` | 4.83 |  |
| `IM/HMI/I/0/Saved/device/devicesettings.pbb` | 167 | `12 42 0a 02 58 32 12 3c` | 5.25 |  |
| `IM/HMI/I/0/Saved/device/networkshare.pbb` | 2 | `0a 00` | 1.0 |  |
| `IM/HMI/I/0/Saved/fonts/SISAN03.ttf` | 125768 | `00 01 00 00 00 15 01 00` | 6.09 |  |
| `IM/HMI/I/0/Saved/fonts/SISAN03.woff2` | 54224 | `77 4f 46 32 00 01 00 00` | 8.0 |  |
| `IM/HMI/I/0/Saved/general/ChromIdentification.rdf` | 21764 | `52 44 46 00 15 00 02 01` | 4.88 | Alarm_1,HMI_Connection_1 |
| `IM/HMI/I/0/Saved/general/cds.rdf` | 2568058 | `52 44 46 00 15 00 02 01` | 4.7 |  |
| `IM/HMI/I/0/Saved/general/config_AlertClass.rdf` | 1594 | `52 44 46 00 15 00 02 01` | 2.41 |  |
| `IM/HMI/I/0/Saved/general/config_AlertHandler.rdf` | 24292 | `52 44 46 00 15 00 02 01` | 2.81 | HMI_Connection_1 |
| `IM/HMI/I/0/Saved/general/config_DistributionInfo.rdf` | 172 | `52 44 46 00 15 00 02 01` | 2.6 |  |
| `IM/HMI/I/0/Saved/general/config_DpValue.rdf` | 151 | `52 44 46 00 15 00 02 01` | 2.59 |  |
| `IM/HMI/I/0/Saved/general/config_HmiRuntime.rdf` | 84 | `52 44 46 00 15 00 02 01` | 2.5 |  |
| `IM/HMI/I/0/Saved/general/config_InitialValue.rdf` | 793 | `52 44 46 00 15 00 02 01` | 4.6 |  |
| `IM/HMI/I/0/Saved/general/config_PeripheralAddress.rdf` | 349 | `52 44 46 00 15 00 02 01` | 4.18 |  |
| `IM/HMI/I/0/Saved/general/dpIdentification.rdf` | 2792 | `52 44 46 00 15 00 02 01` | 5.37 | HMI_Connection_1 |
| `IM/HMI/I/0/Saved/general/fontLocatorList.rdf` | 125 | `52 44 46 00 15 00 02 01` | 3.42 |  |
| `IM/HMI/I/0/Saved/general/main.rdf` | 985 | `52 44 46 00 15 00 02 01` | 4.32 | HMI_1 |
| `IM/HMI/I/0/Saved/general/style_167.rdf` | 60767 | `52 44 46 00 15 00 02 01` | 2.81 |  |
| `IM/HMI/I/0/Saved/general/style_271.rdf` | 66301 | `52 44 46 00 15 00 02 01` | 2.85 |  |
| `IM/HMI/I/0/Saved/general/style_66.rdf` | 66021 | `52 44 46 00 15 00 02 01` | 2.83 |  |
| `IM/HMI/I/0/Saved/general/textLibrary_166_1033.rdf` | 397 | `52 44 46 00 15 00 02 01` | 4.13 |  |
| `IM/HMI/I/0/Saved/general/textLibrary_2_1033.rdf` | 27974 | `52 44 46 00 15 00 02 01` | 5.12 |  |
| `IM/HMI/I/0/Saved/general/textListLibrary.rdf` | 5202 | `52 44 46 00 15 00 02 01` | 2.6 |  |
| `IM/HMI/I/0/Saved/system/ES2RTID.rdf` | 36028 | `52 44 46 00 15 00 02 01` | 6.81 |  |
| `IM/HMI/I/0/Saved/system/ProjectCharacteristics.rdf` | 240 | `52 44 46 00 15 00 02 01` | 4.29 | HMI_1 |
| `IM/HMI/I/0/Saved/{894AD6A7-6155-424B-AA9D-7E77FD174D24}.zip` | 165116 | `50 4b 03 04 14 00 00 00` | 7.99 |  |
| `IM/HMI/I/0/Saved/{9BA2ACF0-39B3-465B-8E10-28301ED605C5}.zip` | 3049552 | `50 4b 03 04 14 00 00 00` | 8.0 |  |
| `IM/HMI/I/AdditionalFiles/0_Chrom_21.0.1.0_Rdf_21.0.1.0/CAI/CAI.bin` | 503 | `01 00 00 00 1b 00 00 00` | 7.34 |  |
| `IM/HMI/I/AdditionalFiles/0_Chrom_21.0.1.0_Rdf_21.0.1.0/HIDS/HIDS.bin` | 13958 | `05 00 00 00 c3 00 00 00` | 6.85 |  |
| `IM/HMI/I/AdditionalFiles/0_Chrom_21.0.1.0_Rdf_21.0.1.0/IAH/IAH.bin` | 8 | `01 00 00 00 00 00 00 00` | 0.54 |  |
| `IM/HMI/I/AdditionalFiles/0_Chrom_21.0.1.0_Rdf_21.0.1.0/LCSI/LCSI.bin` | 55168 | `42 00 00 00 10 02 00 00` | 4.47 |  |