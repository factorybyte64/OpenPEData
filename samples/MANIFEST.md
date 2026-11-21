# Samples

Do not modify samples after they have been created. Created via TiaMcpXE (`tools\McpCall.ps1`).

| Sample | Predecessor | Change | TIA | Created | Files |
| --- | --- | --- | --- | --- | --- |
| 000_empty | – | New empty project, no device. create → save → archive → close. | V21 Update 3 (2100.0.121.1) | 2026-10-19 | `000_empty\`, `000_empty.zap21` (state after save, before close), `000_empty_inventory.json` |
| 000_empty_resave | 000_empty | Copy of the folder 000_empty (state after close), open → save (without change) → close. | V21 Update 3 | 2026-10-19 | `000_empty_resave\` (project file is still named 000_empty.ap21) |
| 000_empty2 | – | Second empty project, no device, name 000_empty2. create → save → close (no archive). | V21 Update 3 | 2026-10-19 | `000_empty2\` |

Analysis outputs: `000_empty_analysis\` (see the README there).

## Derived samples (phase 3)

All with `tools\New-Sample.ps1` (copy of the predecessor → open → change → save → ground truth → archive → close). The project file is still named `000_empty.ap21` in all copies. Ground truth in `<sample>_truth\`, record diff to the predecessor in `<sample>_diff.txt` (`tools\Diff-PeData.ps1`).

| Sample | Predecessor | Change (MCP call) | Ground truth |
| --- | --- | --- | --- |
| 001_devgroup | 000_empty | `TiaDevice(groupCreate, name=Group_1)` – replacement for 001_comment: TiaMcpXE has no operation for project comment/author | `groupList.json` |
| 002_cpu | 000_empty | `TiaDevice(create, name=PLC_1, deviceType=OrderNumber:6ES7 513-1AL02-0AB0/V2.9)` (AL03 not in the catalog) | `device_PLC_1_details.json`, `hardware.aml` (TiaCax export) |
| 003_tagtable | 002_cpu | `TiaPlcSoftware(create/tagTable, TagTable_1)` + `tagCreate(Tag_Bool_1, Bool, %M0.0)` | SimaticML export `TagTable_1.xml`, `Default tag table.xml` |
| 004_fb | 002_cpu | `TiaPlcSoftware(documentWrite/block, compileAfter=false)` with SD `S7_EditorMode := "SCL"`, `FUNCTION_BLOCK "FB_Empty"` / `END_FUNCTION_BLOCK` (FB1, not compiled) | `FB_Empty.input.s7dcl`, `FB_Empty.sd.json`, `FB_Empty.attributes.json`, `blocks.json` (no SimaticML: export requires a compiled block). Archive created afterwards via open → archive → close (plf hash checked unchanged). |

## Tag variants (Step 4b)

All from 003_tagtable with `tools\Make-TagSamples.ps1` (calls `New-Sample.ps1`: copy → open → change → save → ground truth → archive → close), TIA V21 Update 3, created 2026-10-19. Exactly one call each `TiaPlcSoftware(tagSet, objectKind=tagTable, name=TagTable_1, tagName=Tag_Bool_1, …)`. Ground truth in `<sample>_truth\`: SimaticML export `TagTable_1.xml`, `Default tag table.xml`, `TagTable_1.get.json`. Record diff to 003: `<sample>_diff.txt` (`tools\Diff-PeData.ps1`), attribute-level diff: `<sample>_attrdiff.txt` (`tools\attrdiff.py`).

| Sample | Predecessor | Change (tagSet parameter) | Result according to Openness |
| --- | --- | --- | --- |
| 005_tag_int | 003_tagtable | `dataType=Int` | DataTypeName Int, address stays %M0.0 (invalid for Int, TIA accepts it) |
| 006_tag_addr | 003_tagtable | `logicalAddress=%M10.3` | LogicalAddress %M10.3 |
| 007_tag_comment | 003_tagtable | `comment=Test` | Comment en-US “Test” |
| 008_tag_flags | 003_tagtable | `externalWritable=false` (retentivity cannot be set via tagSet) | ExternalWritable false |
| 009_tag_rename | 003_tagtable | `newName=Tag_Renamed` | Name Tag_Renamed |

## Gap samples (Step 5a)

All with `tools\Make-Samples.ps1` (recipes there, calls `New-Sample.ps1`), TIA V21 Update 3, created 2026-10-19. Ground truth from `Save-Truth` (`tools\Truth.ps1`) in `<sample>_truth\` (project/devices/groups.json, per PLC tagTables.json + SimaticML per tag table, blocks.json, types.json). Attribute-level diff to the predecessor: `<sample>_attrdiff.txt`.

| Sample | Predecessor | Change (MCP) | Finding |
| --- | --- | --- | --- |
| 010_dev_in_group | 001_devgroup | `TiaDevice(create, name=PLC_G, deviceType=OrderNumber:6ES7 513-1AL02-0AB0/V2.9, parentGroup=Group_1)` | Device is attached to the group via AggregatingFolder |
| 011_tagtable_folder | 003_tagtable | `TiaPlcSoftware(groupCreate tagTable TagGroup_1)` + `create tagTable TagTable_2 groupPath=TagGroup_1` (2 calls, necessary) | Folder = FolderData under ControllerTagsFolder |
| 012_tags_multi | 003_tagtable | `tagCreate` with 8 tags: In_Bool %I0.1, Out_Bool %Q2.0, M_Int %MW20, M_Real %MD24, In_Word %IW4, Out_Byte %QB3, M_DInt %MD28 (comment “DInt Kommentar”), M_Time %MD32 | |
| 013_tag_umlaut | 003_tagtable | `tagSet Tag_Bool_1 comment="Prüfung äöüß"` (first attempt discarded because of ANSI reading of the script, and created again) | Comment text UTF-8 |
| 014_tag_delete | 003_tagtable | `tagDelete Tag_Bool_1` | H-018: delete = new record version (49 B, u32@16 = 0xFFFFFFFF, @24 = 4, GUID 0, no slots), links of the opposite side removed |
| 015_cpu2 | 002_cpu | `TiaDevice(create, name=PLC_2, cloneTypeFromDevice=PLC_1)` | |
| 016_tag_accessible | 003_tagtable | `tagSet Tag_Bool_1 externalAccessible=false` | ExternalAccessible = IStructureItem.HmiStructureTypeRelevant; Openness cascades Visible/Writable |

Retentivity of tags cannot be set via TiaMcpXE (no tagSet parameter) – no sample.
| 021_mastercopy | 004_fb | `TiaProjectLibrary(masterCopyCreate, device=PLC_1, blockName=FB_Empty)` | Check of the slot order (LibraryMasterCopyData is the only type for which declaration order and name order differ; the affected slots 0–3 are empty → not decidable). The master copy contains copies of objects (e.g. DeviceData) with Environment → LibraryMasterCopyData. |

## Data blocks and PLC data types (Step 5b)

With `tools\Make-Samples.ps1` (sources in `tools\sources\*.s7dcl`, comments in `*.s7res`), TIA V21 Update 3, created 2026-10-19. The SD sources were tried out beforehand on a throwaway copy of 004_fb (`TMP\try5b`). Ground truth from `Save-Truth`: additionally per DB/UDT `blocks\<name>.xml` (SimaticML) and `blocks\<name>.sd.json` (`documentGet`). UDT and instance DB are not compiled, so there is no SimaticML export of them (`blocks\_export_errors.txt`); for them the SD text is the truth. Attribute-level diff to the predecessor: `<sample>_attrdiff.txt`.

| Sample | Predecessor | Change (MCP) | Content according to Openness |
| --- | --- | --- | --- |
| 017_udt | 004_fb | `documentWrite type UDT_Motor.s7dcl` | UDT_Motor: Running Bool (“Motor on”), Speed Real := 1500.0 (“Speed setpoint”), Count Int := 7. Import reports “comments ignored if any”, the comments are saved nevertheless |
| 018_db_global | 017_udt | `documentWrite block DB_Global.s7dcl` (compiled) | DB1: xBool Bool := TRUE (“Bool comment”), iInt Int := 42, rReal Real := 3.5 (“Real comment”), aInt Array[0..3] of Int with aInt[1] := 11, sData Struct (a Bool, b DInt := -5) |
| 019_db_instance | 018_db_global | `create block FB_Empty_DB blockType=InstanceDB instanceOf=FB_Empty` | DB2, instance DB of FB_Empty, no tags |
| 020_block_group | 019_db_instance | `groupCreate block BlockGroup_1` + `documentWrite block DB_InGroup.s7dcl groupPath=BlockGroup_1` | DB3 in BlockGroup_1: wWord Word := 16#00FF |
| 022_db_udt | 020_block_group | `documentWrite block DB_Motor.s7dcl` | DB4: motor "UDT_Motor" with motor.Speed := 750.0 |

Note: `On` is a reserved word in SD (syntax error), so the UDT element is named `Running`.

## Interfaces of FB and FC (Step 5c, parts a and c)

TIA V21 Update 3, created 2026-10-20 with TiaMcpXE directly (not via `New-Sample.ps1`: copy of the predecessor folder → `TiaProject(open)` → `TiaPlcSoftware(documentWrite)` → save → archive → close). Sources in `tools\sources\FB_Iface.s7dcl/.s7res` and `FC_Iface.s7dcl/.s7res`. Ground truth in `<sample>_truth\`: SimaticML export `FB_Iface.xml` / `FC_Iface.xml`, SD export `.s7dcl/.s7res`, `*.get.json` (`get detail=interface`). Attribute-level diff to the predecessor: `<sample>_attrdiff.txt`. Both blocks are SCL, optimized, compiled (0 errors, 0 warnings), every tag with an en-US comment. Not created: truth lists (project/devices/blocks.json) as with `Save-Truth`.

| Sample | Predecessor | Change (MCP) | Content according to Openness |
| --- | --- | --- | --- |
| 023_fb_iface | 022_db_udt | `documentWrite block FB_Iface.s7dcl` | FB2 `FB_Iface`: Input iBool Bool, iInt Int, iReal Real, iArr Array[0..3] of Int, iMotor "UDT_Motor"; Output oBool Bool, oInt Int; InOut ioReal Real; Static stInt Int := 42, stBool Bool; Temp tInt Int; Constant cInt Int := 7. Code: 4 assignments |
| 024_fc_iface | 023_fb_iface | `documentWrite block FC_Iface.s7dcl` | FC1 `FC_Iface` : Int, sections like the FB without Static, plus Return `Ret_Val` Int |
| 025_fb_idb | 023_fb_iface | `create block FB_Iface_DB blockType=InstanceDB instanceOf=FB_Iface`, then `compile` (0 errors) | DB5 `FB_Iface_DB`, instance DB of FB_Iface. Openness exports (SimaticML, get) only name/data type of the sections Input, Output, InOut, Static – neither start values (stInt := 42) nor comments, Temp/Constant are missing. Ground truth: `FB_Iface_DB.xml`, `.get.json`, `.sd.txt` (SD text, two lines: `DATA_BLOCK FB_Iface_DB : FB_Iface`) |

Notes: `sInt` is invalid as a tag name (SD type name SInt) → `stInt`/`stBool`. Constants are addressed in the code as `#cInt`. Static is called `VAR` in SD, constants `VAR CONSTANT`. In the FC the return value was set in the code as `#FC_Iface := …`, the interface reports it as `Ret_Val`.

## Block code (Step 5d, part a)

TIA V21 Update 3, created 2026-10-20 like the samples 023–025 (directly via TiaMcpXE: copy → open → `documentWrite` → compile → save → archive → close). Sources in `tools\sources\FC_Scl.*`, `FC_Lad.*`, `FC_Fbd.*`. Ground truth in `<sample>_truth\`: SimaticML (`FC_*.xml`), SD export (`.s7dcl/.s7res`, as TIA writes it), `*.get.json` (`get detail=full`: networks as SCL text or instructions). Attribute-level diff to the predecessor: `<sample>_attrdiff.txt`. All compile with 0 errors and 0 warnings (the first version of FC_Scl warned “#result might not be initialized”, hence `#result := 0;` before the IF).

| Sample | Predecessor | Change (MCP) | Content |
| --- | --- | --- | --- |
| 026_fc_scl | 025_fb_idb (instead of 023: additionally contains the instance DB FB_Iface_DB) | `documentWrite block FC_Scl.s7dcl` | FC1 `FC_Scl` : Void, SCL, 1 network: assignment, `IF/ELSIF/ELSE`, `FOR` loop, line comments `//`, call `"FB_Iface_DB"(iBool := TRUE, iInt := #result, iReal := 1.5, ioReal := #gain)`. Interface: Input a, b Int; Output result, total Int; InOut gain Real; Temp tmp, i Int |
| 027_fc_lad | 026_fc_scl | `documentWrite block FC_Lad.s7dcl` | FC2 `FC_Lad` : Void, LAD, 2 networks: (1) title “Start latch”, comment, `Contact(#start)` → `Coil(#running)`; (2) title “Addition”, `Add(in1 := #a, in2 := #b, out => #sum)` with `{ S7_Templates := "SrcType := Int" }`. Input start Bool, a, b Int; Output running Bool, sum Int |
| 028_fc_fbd | 027_fc_lad | `documentWrite block FC_Fbd.s7dcl` | FC3 `FC_Fbd` : Void, FBD, the same logic: (1) `RUNG #start` / `Coil(#running)`, title “Start latch FBD”; (2) `Add` box |

Notes: TIA writes the tags `a` and `b` in the SD export as `"a"`/`#"a"` (quoting). The return value `: Void` appears in the interface as member `Ret_Val` of type `Void` (RID `0x02000000`, DataTypeID 0). SimaticML also lists empty sections.

## Instance DB with changed start value (Addendum to 5c part c)

| Sample | Predecessor | Change (MCP) | Content |
| --- | --- | --- | --- |
| 029_fb_idb_value | 025_fb_idb | `documentWrite block tools\sources\FB_Iface_DB_value.s7dcl` (`DATA_BLOCK FB_Iface_DB : FB_Iface` with `stInt := 99; stBool := TRUE;`), compile 0/0 | IDB FB_Iface_DB with start values stInt 99 (FB: 42), stBool TRUE. Unlike 025, Openness reports the values (SimaticML and get). `documentWrite` replaces the DB object (new OID, interface root and loadable are created anew; DB5 stays). Ground truth `029_fb_idb_value_truth\FB_Iface_DB.xml/.get.json/.sd.txt`, attrdiff to 025 |

## SCL coverage (Step 5g, part 1)

`030_scl_all` (from 029_fb_idb_value, TIA V21 Update 3, 2026-10-20): 13 PLC tags (`tBool` … `tLReal`, `Default tag table`), 3 constants (`MAX_COUNT`, `GAIN`, `TXT`), non-optimized DB `DB_Std` (DB6) and 15 SCL blocks; sources `tools\sources\scl\*.s7dcl`. Created via `documentWrite`; compiler findings are part of the truth. Truth: `030_scl_all_truth\sd\<block>.s7dcl` (SD export, authoritative for the source text) and `simaticml\<block>.xml`. The tool `tools\scl_cov.py <sample> [block]` compares parser text and SD export (`parser\tests\test_samples.py` does it for all samples with `_truth\sd\`).

| Block | Content | Compiler |
| --- | --- | --- |
| FC_SclOps | + - * / MOD ** unary +/-, NOT AND OR XOR &, = <> < <= > >=, := += -= *= /=, parentheses, literals hex/binary/octal/Real/exponent | 1 warning (precision) |
| FC_SclCtl | IF/ELSIF/ELSE, CASE (list, range, ELSE, empty statement), FOR (BY, EXIT, CONTINUE, nested), WHILE, REPEAT/UNTIL, REGION, GOTO + label, RETURN, `(* *)` over several lines, `//` | ok |
| FC_SclLit | Bool, Byte…LWord (16#, `_`), SInt…ULInt, Real/LReal, Time/LTime, Date, TOD/LTOD, DTL/LDT, Char/WChar, String/WString (`$` escapes), typed literals (`INT#5`, `WORD#16#FF` …) | 1 warning |
| FB_SclLocal | local/static accesses: Array (1D, 2D constant), Struct, UDT member, InOut | ok |
| FC_SclGlobal | access to DB (Array, Struct, UDT), instance DB, PLC tags | ok |
| FC_SclAbs, FC_SclAbs2 | absolute addresses %M %I %Q (bit/byte/word/dword), `%DB6.DBX0.0` … in the non-optimized DB | ok |
| FC_SclSlice | slices `.%X3`, `.%B1`, `.%W1`, `.%D1`, also on tags | ok |
| FB_SclCalls | multi-instances TON/TOF/TP/CTU/R_TRIG/F_TRIG/FB, call with instance DB, FC calls with/without return value, `=>` | ok |
| FC_SclMath | ABS SQRT SQR LN EXP SIN COS TAN ASIN ACOS ATAN, MIN/MAX/LIMIT, SEL, MUX, conversions, TRUNC ROUND CEIL FLOOR, SHL SHR ROL ROR, NORM_X, SCALE_X, FRAC | ok |
| FC_SclStr | LEN CONCAT LEFT RIGHT MID FIND INSERT DELETE REPLACE, string comparison, character access, conversions | ok |
| FC_SclMisc | PEEK/POKE (+WORD/BOOL), `REF()`, Variant, multi-dimensional arrays (constant index), CountOfElements | 1 warning |
| FC_SclArr | multi-dimensional arrays with variable index (InOut) | error “Indirect access exceeds the addressable range” (truth only via SD export; SimaticML is not exported) |
| FC_SclVar | Variant, TypeOf, VariantGet/Put, MOVE_BLK_VARIANT, `ENO`, tab and empty lines | ok |
| FC_SclMore | `#"my var"`, user constants, several statements per line, comments (end of line, `(* *)` inline), time/date arithmetic, UPPER/LOWER_BOUND, peripheral `%IW0:P`, Unicode, long expressions, deeply nested structures | ok |

Rejected by TIA (not in the sample): `LOG`, `EXPT` (use `**` instead), `TO_REAL`/`TO_INT`/`TO_LREAL`/`TO_STRING`, `MOD()` as a function, `ODD`, `POKE_WORD`, positional MIN/MAX/LIMIT calls, `String + String`, assignment `Variant := Tag` without `REF`, assignment of `TypeOf` to Int.

## WinCC Unified (Step U1)

Created 2026-10-20 with the built-in TiaMcpXE tools (no scripts via `McpCall.ps1`: its new MCP session per call would bind to a foreign instance via auto-attach), own TIA instance PID 10084 (`TiaInstance(acquire, withUserInterface)`, `lock` 7200 s), TIA V21. Ground truth in `100_hmi_device_truth\` (device, HMI software, connection, alarm class, system tag, runtime settings queries; `export connection` not supported by Openness). File, metamodel and record type comparison to 002_cpu: `100_hmi_device_diff.txt` (no attrdiff, see there).

| Sample | Predecessor | Change (MCP, all with `instance=10084`) | Result according to Openness |
| --- | --- | --- | --- |
| 100_hmi_device | 002_cpu | folder copy → `TiaProject(open)` → `TiaDevice(typeSearch, "MTP700 Unified Comfort")` → `TiaDevice(create, name=HMI_1, deviceType=OrderNumber:6AV2 128-3GB06-0AXx/21.0.1.0)` → `TiaNetwork(interfacesDiscover)` HMI_1 and PLC_1 → `TiaNetwork(hmiConnectionIntegrate, hmiDevice=HMI_1, plcDevice=PLC_1, connectionName=HMI_Connection_1, hmiInterfacePath=HMI_1.IE_CP_1/PROFINET Interface_1, plcInterfacePath=PLC_1/PROFINET interface_1)` → `save` → ground truth → `archive` → `close` | MLFB 6AV2 128-3GB06-0AXx, firmware 21.0.1.0 (newest in the catalog). TiaMcpXE renames the HMI software from HMI_RT_1 to HMI_1. Connection HMI_Connection_1, driver SIMATIC S7 1200/1500, 192.168.0.2 → 192.168.0.1; `hwIsValid false`, both interfaces without subnet (despite `connectSubnet` default true). HMI software: 0 screens, 0 tags, 1 tag table, 29 system tags, 1 connection, 18 alarm classes, 25 system text lists |

## WinCC Unified: HMI tags (Step U2)

Created 2026-10-20, own TIA instance PID 10084 (after idle loss re-bound via `TiaInstance(use)`, `lock` 10800 s), all calls with `instance=10084`. Procedure per sample: folder copy of the predecessor → `open` → change → `save` → ground truth (`TiaHmiUnified get/list tag`, `list tagTable`/`tagTableGroup`, `partGet`) → `archive` → `close`. Ground truth in `<sample>_truth\HMI_1\`. Diff to the predecessor: `<sample>_attrdiff.txt` (`tools\unified_tagdiff.py`: attrdiff with a local workaround for H-023, dump of the HmiTag/tag table/folder objects, H-022 file comparison and name search). Note on numbering: 101 is the new base, the variants are named 102–106.

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| 101_hmi_base | 003_tagtable | `TiaDevice(create, HMI_1, OrderNumber:6AV2 128-3GB06-0AXx/21.0.1.0)` + `TiaNetwork(hmiConnectionIntegrate)` as with 100 (2 calls, base) | like 100, additionally PLC tag table TagTable_1 with Tag_Bool_1 (%M0.0) |
| 102_hmi_tag_int | 101_hmi_base | `TiaHmiUnified(create, tag, name=HmiTag_Int1, dataType=Bool)` without `tagTable` | internal tag in **Default tag table**, Connection “<Internal tag>”, AcquisitionCycle T1s, AcquisitionMode CyclicOnUse, Persistent false |
| 103_hmi_tag_ext | 101_hmi_base | `TiaHmiUnified(create, tag, name=HmiTag_Ext1, connection=HMI_Connection_1, plcTag=Tag_Bool_1)` | Default tag table, DataType Bool (from the PLC tag), PlcName PLC_1, PlcTag Tag_Bool_1, AcquisitionCycle T1s |
| 104_hmi_tag_cycle | 103_hmi_tag_ext | `TiaHmiUnified(set, tag, HmiTag_Ext1, attribute=AcquisitionCycle, value=T100ms)` | AcquisitionCycle T1s → T100ms (default was already 1 s) |
| 105_hmi_tag_comment | 103_hmi_tag_ext | `TiaHmiUnified(partSet, tag, HmiTag_Ext1, partPath="", attribute=Comment, value=Test, language=en-US)` (`set` with `Comment` is rejected: “not supported”) | Comment en-US “Test”. Second language not possible: `language=de-DE` reported `updated`, but overwrote en-US (project has only en-US); afterwards en-US set back to “Test”, only then `save` |
| 106_hmi_tagtable_group | 101_hmi_base | `TiaHmiUnified(create, tagTableGroup, HmiTagGroup_1)` + `create, tagTable, HmiTagTable_1, groupPath=HmiTagGroup_1` (2 calls, necessary) | tag table HmiTagTable_1 in group HmiTagGroup_1 |

## WinCC Unified: alarms (Step U3)

Created 2026-10-20, own TIA instance PID 10084, all calls with `instance=10084`. Procedure and tools as in U2 (folder copy → `open` → change → `save` → ground truth → `archive` → `close`; diff `<sample>_attrdiff.txt` with `tools\unified_tagdiff.py`). Multilingual alarm text (U3 c) is dropped: the project has only en-US, and TiaMcpXE cannot create a project language.

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| 107_hmi_alarmclass | 103_hmi_tag_ext | `TiaHmiUnified(create, alarmClass, name=AlarmClass_Test)` (create takes only the name) | Id 64, Priority 0, IsSystem false, StateMachine RaiseClearRequiresAcknowledgement, CommonAlarmClass “<No alarm class>”, colors per state; AlarmClasses 18 → 19 |
| 108_hmi_alarm_discrete | 103_hmi_tag_ext | `TiaHmiUnified(create, discreteAlarm, name=Alarm_1, attributes={RaisedStateTag:HmiTag_Ext1, RaisedStateTagBitNumber:0, AlarmClass:Alarm, EventText:"Test alarm"}, language=en-US)` | Id 1, TriggerMode OnRisingEdge, TriggerBitAddress Tag_Bool_1.x0, Area HMI_1::Alarming, EventText en-US “Test alarm” (raw `<body><p>Test alarm</p></body>`) |
| 109_hmi_alarm_analog | 103_hmi_tag_ext | `TiaHmiUnified(create, tag, HmiTag_Int2, dataType=Int)` (internal Int tag, necessary) + `create, analogAlarm, name=AnalogAlarm_1, attributes={RaisedStateTag:HmiTag_Int2, AlarmClass:Warning, Condition:UpperLimit, ConditionValue:100, EventText:"Test analog alarm"}, language=en-US` | Id 1, Condition UpperLimit, ConditionValue 100, class Warning |

## Project library: PLC types and versions (Step L1)

Created 2026-10-20 with the built-in TiaMcpXE tools, own TIA instance PID 33032 (`TiaInstance(acquire)`, `lock` 10800 s), all calls with `instance=33032`, TIA V21 Update 3. Procedure per sample: folder copy of the predecessor (plf hash checked equal) → `open` → change → `save` → ground truth → `archive` → `close`. Ground truth in `<sample>_truth\`: `typesList.json` (`TiaProjectLibrary(typesList)`), `instancesList*.json` (`instancesList device=PLC_1`, partly per version), `typeExport_<type>_<version>.xml` (only possible for Committed), for L01/L05 additionally `attributeGet`/`documentGet` of the instantiated object, input documents in `input\`, calls with result in `calls.json` or `*.result.json`. Attribute-level diff to the predecessor: `<sample>_attrdiff.txt` (`tools\attrdiff.py`). Evaluation: `analysis\L1_types\REPORT.md`.

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| L01_udt_type_inwork | 017_udt | `TiaPlcSoftware(delete, type, UDT_Motor)` + `TiaProjectLibrary(typeCreateFromDocument, device=PLC_1, documentDirectory=L01_udt_type_inwork_truth\input, fileNameWithoutExtension=UDT_Motor)` (2 calls, necessary: CreateFromDocuments also creates the instance in the PLC and fails if UDT_Motor already exists). Input = `tools\sources\UDT_Motor.s7dcl/.s7res`; the SD export of TIA (`TYPE` alone in the first line) is not recognized as a UDT by the bridge (“block type 'UDT' is not supported under 'ProgramBlocksFolder'”) | Type UDT_Motor (PlcTypeLibraryType, GUID b0605f73-…), version 0.0.1 InWork, no default version; instance UDT_Motor (PlcStruct) |
| L02_udt_type_released | L01_udt_type_inwork | `typeVersionRelease(typeName=UDT_Motor)` | 0.0.1 Committed, default (Openness assigns 0.0.1, not 1.0.0) |
| L03_udt_type_v2 | L02_udt_type_released | `typeVersionEdit(UDT_Motor, device=PLC_1)` → 0.0.2 InWork; changing the content failed (`documentWrite type` and `documentImport type importMode=Override`: “object with the name 'UDT_Motor' already exists”); `typeVersionRelease(versionNumber=1.0.0, comment="L03 release")` without content change | 0.0.1 and 1.0.0 Committed, default 1.0.0, instance at 1.0.0 (`instancesList version=0.0.1`: 0) |
| L04_default_changed | L03_udt_type_v2 | `typeVersionSetDefault(UDT_Motor, version=0.0.1)` | default 0.0.1, instance stays at 1.0.0 |
| L05_fb_type_inwork | 004_fb | `typeCreateFromDocument(device=PLC_1, input\FB_Typed.s7dcl)` (new FB, SCL: Input in1 Int, Output out1 Int, `#out1 := #in1 + 1;`) | Type FB_Typed (CodeBlockLibraryType), 0.0.1 InWork; instance FB_Typed FB2 |
| L06_fb_type_released | L05_fb_type_inwork | `typeVersionRelease(FB_Typed)` (compiles the FB in the process) | 0.0.1 Committed, default; FB “library-conformant” |
| L07_fc_type | L06_fb_type_released | `typeCreateFromDocument(device=PLC_1, input\FC_Typed.s7dcl)` (FC : Int, `#FC_Typed := #a * #b;`) + `typeVersionRelease(FC_Typed, versionNumber=2.3.4, comment="L07 FC release")` | FC_Typed 2.3.4 Committed, default (version number with three different digits to check the encoding) |
| L09_type_depends | L02_udt_type_released | `typeCreateFromDocument(device=PLC_1, input\FB_Motor.s7dcl)` (FB with Input `motor : "UDT_Motor"`) + `typeVersionRelease(FB_Motor, versionNumber=1.2.3)` | FB_Motor 1.2.3 Committed, depends on UDT_Motor 0.0.1 (typesList does not show dependencies; evidence: PEData DependsOn) |

L08 (type in a subfolder of the types library) does not exist: TiaMcpXE has no operation for creating folders in the project library (`typeCreateFromDocument` always creates in the root folder).

## Project library: master copies (Step L2)

Created 2026-10-20 with the built-in TiaMcpXE tools, own TIA instance PID 44260 (`TiaInstance(acquire)`, `lock` 10800 s; the L1 instance 33032 was still leased to the old L1 client), TIA V21 Update 3. Procedure per sample: folder copy of the predecessor (plf hash checked equal) → `open` → change → `save` → ground truth → `archive` → `close`. Ground truth in `<sample>_truth\`: `masterCopiesList.json` (`TiaProjectLibrary(masterCopiesList)`), calls with result in `calls.json`, for L10 additionally `libraryGet.json`, for L13 block lists of both PLCs, attributes and SD export of the instantiated blocks. Attribute-level diff to the predecessor: `<sample>_attrdiff.txt` (`tools\attrdiff.py`). Evaluation: `analysis\L2_mastercopy\REPORT.md`.

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| L10_mc_fc | 026_fc_scl | `TiaProjectLibrary(masterCopyCreate, device=PLC_1, blockName=FC_Scl)` (FC_Scl calls `"FB_Iface_DB"`) | master copy FC_Scl, content FC_Scl (FC) |
| L11_mc_fb_udt | 023_fb_iface | `masterCopyCreate(PLC_1, FB_Iface)` (FB_Iface has Input `iMotor : "UDT_Motor"`) | master copy FB_Iface, content FB_Iface (FB) |
| L12_mc_two | 024_fc_iface | `masterCopyCreate(PLC_1, FC_Iface)` + `masterCopyCreate(PLC_1, FB_Iface)` (deliberately FC first) | two master copies in the order FC_Iface, FB_Iface |
| L13_mc_instantiate | L11_mc_fb_udt | `masterCopyInstantiate(FB_Iface, device=PLC_1, newName=FB_Iface_Copy)` + `TiaDevice(create, PLC_2, cloneTypeFromDevice=PLC_1)` + `masterCopyInstantiate(FB_Iface, device=PLC_2)` (3 calls) | PLC_1: new block **FB_Iface_1** (newName was not applied; number 2 like FB_Iface, not consistent); PLC_2: FB_Iface (FB2), **no** UDT_Motor (type list empty); SD export of both identical to FB_Iface from 023 |
| L14_mc_delete | L12_mc_two | `masterCopyDelete(FC_Iface)` | one master copy FB_Iface |
| L15_mc_db | 022_db_udt | `masterCopyCreate(PLC_1, DB_Motor)` (global DB with `motor : "UDT_Motor"`) | master copy DB_Motor, content DB_Motor (GlobalDB) |

Not created: master copy in a subfolder (`masterCopyCreate masterCopyFolderPath=MC_Group/MC_Sub` → `NOT_FOUND "Target master copy folder not found"`; TiaMcpXE cannot create master copy folders; the throwaway copy of 024 was closed unchanged and deleted), master copies with multiple selection, from a UDT, tag table or device (masterCopyCreate only knows `sourceKind=PlcCodeBlock`). The .zap21 archives contain the compacted store without tombstones (L14: the 26 tombstones are only in the folder).

## Project library: version status of type instances (Step L4)

Created 2026-10-20 with the built-in TiaMcpXE tools, own TIA instance PID 45856 (`TiaInstance(acquire)`, newly started, `lock` 14400 s), TIA V21 Update 3. Procedure per sample: folder copy of the predecessor (plf hash checked equal) → `open` → change → `save` → ground truth → `archive` → `close`. Ground truth in `<sample>_truth\`: `typesList.json`, `instancesList_<type>.json` (key `<device> version <x>`, `instancesList device=… version=…`), `<type>.<device>.attributes.json` (`TiaPlcSoftware(attributeGet)` of the instance, among others CreationDate and LibraryConformanceStatus), calls with result in `calls.json`, input documents in `input\`. Attribute-level diff to the predecessor: `<sample>_attrdiff.txt` (`tools\attrdiff.py`). Evaluation: `analysis\L4_instances\REPORT.md`.

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| L16_inst_two_devices | L02_udt_type_released | `TiaDevice(create, PLC_2, cloneTypeFromDevice=PLC_1)` + `typeCreateFromDocument(addVersionTypeName=UDT_Motor, device=PLC_2, input\UDT_Motor)` (InWork 0.0.2 with instance in PLC_2) + `typeVersionDiscard(UDT_Motor)` (3 calls, necessary: Openness cannot instantiate a PLC data type directly from a type version) | UDT_Motor 0.0.1 Committed, default; instances in PLC_1 and PLC_2 at 0.0.1 |
| L17_inst_outdated | L16_inst_two_devices | `typeVersionEdit(UDT_Motor, device=PLC_1)` + `typeVersionRelease(versionNumber=1.0.0, comment="L17 release")` | 0.0.1 + 1.0.0, default 1.0.0; PLC_1 at 1.0.0, PLC_2 stays at 0.0.1 (outdated); both “library-conformant” |
| L18_inst_edit_outdated | L17_inst_outdated | `typeVersionEdit(UDT_Motor, version=0.0.1, device=PLC_2)` (InWork **1.0.1**) + `typeVersionRelease(versionNumber=1.1.0, comment="L18 release from PLC_2")` | 0.0.1, 1.0.0, 1.1.0, default 1.1.0; PLC_1 at 1.0.0, PLC_2 at 1.1.0 |
| L19_version_discarded | L18_inst_edit_outdated | `typeVersionEdit(UDT_Motor, version=1.0.0, device=PLC_1)` (InWork 1.1.1) + `typeVersionDiscard(UDT_Motor)` | like L18; the instance in PLC_1 afterwards has a new CreationDate (created anew) |
| L20_default_two_devices | L18_inst_edit_outdated | `typeVersionSetDefault(UDT_Motor, version=1.0.0)` | default 1.0.0; PLC_1 at 1.0.0, PLC_2 at 1.1.0 |
| L21_fb_inst_outdated | L06_fb_type_released | `TiaDevice(create, PLC_2, cloneTypeFromDevice=PLC_1)` + `typeCreateFromDocument(addVersionTypeName=FB_Typed, device=PLC_2, input\FB_Typed)` + `typeVersionDiscard` + `typeVersionEdit(FB_Typed, device=PLC_1)` + `networkWrite` (fails, see calls.json) + `typeVersionRelease(versionNumber=1.0.0, comment="L21 release")` | FB_Typed 0.0.1 + 1.0.0, default 1.0.0; PLC_1 at 1.0.0, PLC_2 (FB1) at 0.0.1 (outdated) |

Not created (missing MCP operations, not made up for by hand): a real update of an outdated instance (`ProjectLibrary.UpdateProject`/`LibraryType.UpdateProject` is missing; the attempt `typeVersionEdit` via the PLC_2 instance + `typeVersionDiscard` only restores 0.0.1, throwaway copy discarded unchanged), deleting an unused Committed version (`LibraryTypeVersion.Delete` is missing; `TiaProjectLibrary(cleanup)` → NOT_FOUND “CleanupLibraryMode enum not found”), marking a type as non-updateable or obsolete (no operation for `IsSetAsNonUpdateable`/`IsObsolete`).

### Addendum L4 with the updated MCP server (2026-10-21)

Own TIA instance PID 55072 (`acquire`, `lock` 14400 s). Procedure and ground truth as above, additionally `blocks.json` (L22), `masterCopiesList.json` (L28) and `typeExport_*.xml` (L29). The previously missing operations now exist: `updateProject`, `typeVersionDelete`, `typeSet`, `folderCreate`, `masterCopyMove`, and `typeCreateFromDocument` with `folderPath` or `createOptions=Override`.

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| L22_update_removes_unused | L21_fb_inst_outdated | `TiaProjectLibrary(updateProject, typeNameList=FB_Typed)` | FB_Typed is not called by anything: TIA **deletes** the typed FBs in PLC_1 and PLC_2; both versions stay, FB_Empty stays |
| L23_inst_called | L21_fb_inst_outdated | per PLC `TiaPlcSoftware(create block, FB_Typed_DB, InstanceDB, instanceOf=FB_Typed)` + `networkWrite(Main, append, SCL "FB_Typed_DB"(in1 := 1);)` (4 calls, with compiling) | PLC_1 at 1.0.0, PLC_2 at 0.0.1 (outdated), both called |
| L24_inst_update_fb | L23_inst_called | `updateProject(typeNameList=FB_Typed)` | PLC_2 at 1.0.0, FB_Typed in PLC_2 created anew (CreationDate = update time) |
| L25_version_deleted | L24_inst_update_fb | `typeVersionDelete(FB_Typed, 0.0.1)` | only 1.0.0 left |
| L26_type_obsolete | L25_version_deleted | `typeSet(FB_Typed, doNotUse=true)` | typesList `doNotUse: true` |
| L27_inst_update_udt | L17_inst_outdated | per PLC `documentWrite block` with `input\DB_Motor.s7dcl` (global DB with `motor : "UDT_Motor"`, so that the UDT is used) + `updateProject(typeNameList=UDT_Motor)` (3 calls) | PLC_2 UDT 0.0.1 → 1.0.0, created anew |
| L08_type_in_folder | L02_udt_type_released | `folderCreate(types, Motors)` + `typeCreateFromDocument(device=PLC_1, input\FC_Typed, folderPath=Motors/Pumps)` + `typeVersionRelease(FC_Typed, 1.0.0, "L08 release in folder")` | FC_Typed 1.0.0 in type folder Motors/Pumps, UDT_Motor in the root folder |
| L28_mc_in_folder | L10_mc_fc | `folderCreate(masterCopies, MC_Group/MC_Sub)` + `masterCopyMove(FC_Scl, targetFolder=MC_Group/MC_Sub)` | master copy FC_Scl in “Master copies/MC_Group/MC_Sub” (move = copy + deletion of the original) |
| L29_version_content | L02_udt_type_released | `typeVersionEdit(UDT_Motor, PLC_1)` + `typeCreateFromDocument(addVersionTypeName=UDT_Motor, createOptions=Override, device=PLC_1, input\UDT_Motor)` (additional element `Temp : Real := 20.5`) + `typeVersionRelease(1.0.0, "L29 Temp added")` | 0.0.1 without, 1.0.0 with Temp (typeExport of both versions); UDT instance created anew |

## Global library (Step L6)

Created 2026-10-21, own TIA instance PID 55072. Global libraries exist as folders with `<Name>.al21`, `LibraryInfo.txt`, `System\PEData.plf/.idx` and `Vci\Vci.db`, plus as `.zal21` (`TiaLibrary(archive)`). Procedure per library: folder copy of the predecessor (plf hash checked equal) → `TiaLibrary(open)` → change → `save` → ground truth → `archive` → `close`. The library name stays `L30_glib_empty` in all copies. Project samples as before. The source for L31/L32 was a scratch copy of L17_inst_outdated that was not saved. Ground truth in `<sample>_truth\`: `typesList.json` (merged from `TiaLibraryContent(typesList)` and `typeVersionsList`, or `TiaProjectLibrary(typesList)`), `masterCopiesList.json`, `libraryGet.json`/`foldersList.json` (L30), for projects `instancesList_*.json`, `*.attributes.json` and `devices.json`. Diff: `<sample>_attrdiff.txt` (`tools\attrdiff.py`; L30 against 000_empty). Evaluation: `analysis\L6_global\REPORT.md`.

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| L30_glib_empty (global) | – | `TiaLibrary(create, directory=samples, name=L30_glib_empty)` + `save` | empty global library (folders Types, Master copies) |
| L31_glib_type (global) | L30_glib_empty | `TiaLibraryContent(update, sourceScope=project, targetScope=global, typeNameList=UDT_Motor)` from the scratch copy of L17 | UDT_Motor 0.0.1 + 1.0.0 (default), same type and version GUIDs |
| L32_glib_mc_device (global) | L31_glib_type | `TiaLibraryContent(masterCopyCreateFromDevice, device=PLC_1)` from the scratch copy of L17 | additionally the master copy PLC_1 (DeviceImpl) |
| L36_glib_mc_deleted (global) | L32_glib_mc_device | `TiaLibraryContent(masterCopyDelete, PLC_1)` | no master copy any more, type stays |
| L33_proj_update_from_glib | L02_udt_type_released | `TiaLibraryContent(update, sourceScope=global (L32), targetScope=project, typeNameList=UDT_Motor)` | project library 0.0.1 + 1.0.0 (default), instance in PLC_1 stays at 0.0.1 |
| L34_proj_mc_from_glib | L33_proj_update_from_glib | `TiaLibraryContent(masterCopyInstantiate, library=L32, masterCopyName=PLC_1)` | master copy PLC_1 (DeviceImpl) in the project library |
| L35_device_from_mc | L34_proj_mc_from_glib | `TiaProjectLibrary(masterCopyInstantiate, PLC_1, targetKind=Device, newName=PLC_3)` | new device **PLC_2** (newName ignored) with Main, FB_Empty and typed UDT_Motor at 1.0.0 |

Not possible: creating a type directly in the global library from documents (`TiaLibraryContent(typeCreateFromDocument)` → OPENNESS_ERROR “It is not currently supported to modify global libraries.”; throwaway copy discarded unchanged). Folders and type versions in the global library cannot be edited via MCP.

## WinCC Unified: screens (Step U4)

Created 2026-10-20, own TIA instance PID 10084 (bound via `TiaInstance(use)` after the MCP connection was re-established, lease only after release of the dead old session, `lock` 14400 s), all calls with `instance=10084`. Base 101_hmi_base (smallest difference: without HMI tags). Procedure as in U2/U3; ground truth `screens.json` (list), `screens_full.json` (`get detail=full`), for 110 additionally `Screen_1.xml` (`exportXml`, TiaMcpXE's own XML). Diff: `<sample>_attrdiff.txt` (`tools\unified_tagdiff.py`).

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| 110_hmi_screen | 101_hmi_base | `TiaHmiUnified(create, screen, name=Screen_1)` | Defaults: Width 800, Height 480, BackColor 0xFFC0C0C0, AlternateBackColor 0xFFEBEBEB, BackFillPattern Solid, ScreenNumber 0, Enabled true, no objects |
| 111_hmi_screen_rename | 110_hmi_screen | `TiaHmiUnified(partSet, screen, screenName=Screen_1, partPath="", attribute=Name, value=Screen_Renamed)` | Name Screen_Renamed, ScreenNumber stays 0 |
| 112_hmi_screen_backcolor | 110_hmi_screen | `partSet, screen, Screen_1, partPath="", attribute=BackColor, value=0xFF00FF00` | BackColor 0xFF00FF00 |
| 113_hmi_screen_group | 101_hmi_base | `groupCreate, screen, ScreenGroup_1` + `create, screen, Screen_G1, groupPath=ScreenGroup_1` (2 calls, necessary) | Screen_G1 in ScreenGroup_1 |
| 114_hmi_screen2 | 110_hmi_screen | `create, screen, name=Screen_2` | two screens, both ScreenNumber 0 |

## Faceplates F

Created 2026-10-20 with the built-in TiaMcpXE tools, own TIA instance PID 40268 (`TiaInstance(acquire, withUserInterface)`, `lock` 14400 s), all calls with `instance=40268`, TIA V21. Type list and error messages: `docs\spec\unified-screenitem-types.md`. Ground truth in `F01_hmi_allobjects_truth\HMI_1\`: `screen_<screen>.full.json` (`get screen detail=full`; for FP_Basic and FP_Elements this fails with “Ambiguous match found.”, there `itemList` + `itemGet detail=full` per object instead), `xml\<screen>.xml` (`exportXml`, TiaMcpXE's own XML, not for FP_Basic/FP_Elements), `scriptSearch.json` (FP_Script, FP_Interface), `screens.json`, `tags*.json`, `tagTables.json`, `scriptModules.json`, `software.json`, `_truth_calls.json`. Read with `_scripts\truth_f01.py` (`tools\tiaclient.py`, own MCP session, explicitly bound to PID 40268, read accesses only). Type count against 103: `F01_hmi_allobjects_diff.txt` (`tools\unified_typecount.py`).

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| F01_hmi_allobjects | 103_hmi_tag_ext | folder copy (project file stays `000_empty.ap21`) → `open` → calls below → `save` → truth → `archive` → `close` | 7 screens, 48 screen items (47 ILScreenItemData + 1 ILFaceplateContainerData), 6 scripts |

Calls (all `TiaHmiUnified`, `device=HMI_1`; `itemCreate` always with `attributes`, colors `0xAARRGGBB`):

1. `create screen` FP_Basic, FP_Elements, FP_Controls, FP_Interface, FP_Script, Main, FP_Flash.
2. FP_Basic: `itemCreate` B_Rect1 HmiRectangle {Left 20, Top 20, Width 120, Height 60, BackColor 0xFFFF0000}; B_Line1 HmiLine {160,20,120,60, LineColor 0xFF0000FF, LineWidth 3}; B_Polyline1 HmiPolyline {300,20,120,60, LineColor 0xFF00AA00, LineWidth 2} + `partCreate Points value=110,5`; B_Polygon1 HmiPolygon {440,20,120,60, BackColor 0xFFFFCC00, BorderColor 0xFF800080} + `partCreate Points value=100,55`; B_Ellipse1 HmiEllipse {BackColor 0xFF00CCCC} (Left/Top/Width/Height: “Unknown property”) + `itemSet` {CenterX 80, CenterY 150, RadiusX 60, RadiusY 30}; B_EllipseSeg1 HmiEllipseSegment {CenterX 220, CenterY 150, RadiusX 60, RadiusY 30, BackColor 0xFFCC6600, StartAngle 30, AngleRange 240}; B_EllArc1 HmiEllipticalArc {360,150,60,30, LineColor 0xFF0066CC, LineWidth 4, StartAngle 45, AngleRange 200}; B_Circle1 HmiCircle {CenterX 80, CenterY 260, Radius 40, BackColor 0xFF66CC00}; B_CircleSeg1 HmiCircleSegment {200,260,40, BackColor 0xFFCC0066, StartAngle 60, AngleRange 270}; B_CircArc1 HmiCircularArc {320,260,40, LineColor 0xFF990000, LineWidth 5, StartAngle 10, AngleRange 180}; B_Text1 HmiText {400,230,160,40, Text “F01 Basic Text”, ForeColor 0xFF0000CC} (`language=en-US`; BackColor unknown); B_GraphicView1 HmiGraphicView {600,20,150,100, BackColor 0xFFDDEEFF} (BorderColor/BorderWidth unknown).
3. FP_Elements: E_Button1 HmiButton {20,20,140,50, Text “F01 Button”, BackColor 0xFF3366CC, ForeColor 0xFFFFFF00}; E_IOField1 HmiIOField {180,20,140,40, BackColor 0xFFCCFFCC, OutputFormat {F2}}; E_SymIOField1 HmiSymbolicIOField {340,20,140,40, BackColor 0xFFFFE0C0}; E_TextBox1 HmiTextBox {500,20,160,60, BackColor 0xFFF0F0FF, Text “F01 TextBox”}; E_Bar1 HmiBar {20,100,60,180, BackColor 0xFFEEEEEE}; E_Slider1 HmiSlider {100,100,80,180, BackColor 0xFFDDFFDD}; E_Gauge1 HmiGauge {200,100,180,180, BackColor 0xFFFFF0E0}; E_Clock1 HmiClock {400,100,160,160, BackColor 0xFFE0E0FF}; E_Switch1 HmiToggleSwitch {580,100,120,50, BackColor 0xFFFFCCCC}; E_CheckBox1 HmiCheckBoxGroup {20,300,160,100, BackColor 0xFFFFFFCC} + `partCreate SelectionItems` + `partSet SelectionItems/#3 Text=“F01 Check 4” language=en-US`; E_Radio1 HmiRadioButtonGroup {200,300,160,100, BackColor 0xFFCCFFFF}; E_ListBox1 HmiListBox {380,300,160,100, BackColor 0xFFFFDDFF}; E_TouchArea1 HmiTouchArea {560,300,140,100} + `itemSet` {BackColor 0x40FF0000, TabIndex 7}; E_AlarmInd1 HmiAlarmIndicator {720,100,80,80} + `itemSet` {AlarmState.BackColor 0xFFFF9900, FlashingColor 0xFF00FF99}. Rejected: E_Label1 HmiLabel, E_ComboBox1 HmiComboBox.
4. FP_Controls: C_AlarmControl1 HmiAlarmControl {10,10,380,200, BackColor 0xFFE8F0FF}; C_AlarmLine1 HmiAlarmLineControl {10,220,380,40, BackColor 0xFFFFF0F0}; C_Trend1 HmiTrendControl {400,10,380,200, BackColor 0xFFF0FFF0}; C_TrendCompanion1 HmiTrendCompanion {400,220,380,120, BackColor 0xFFF0F0E0}; C_FTrend1 HmiFunctionTrendControl {10,280,380,200, BackColor 0xFFE0F8FF}; C_DetParam1 HmiDetailedParameterControl {400,350,190,120, BackColor 0xFFFFF8E0}; C_Web1 HmiWebControl {600,350,190,120, Url https://example.invalid/f01}; C_Media1 HmiMediaControl {10,490,190,120, BackColor 0xFF202040}; C_SysDiag1 HmiSystemDiagnosisControl {210,490,190,120, BackColor 0xFFF4F4D0}; C_ProDiagOv1 HmiProcessDiagnosisOverviewControl {410,490,190,120, BackColor 0xFFE0FFE8}; C_GraphOv1 HmiProcessDiagnosisGraphOverviewControl {610,490,190,120, BackColor 0xFFE8E0FF}; C_CritAnalysis1 HmiProcessDiagnosisCriteriaAnalysisControl {10,620,190,120, BackColor 0xFFFFE8E8}; C_PlcCode1 HmiProcessDiagnosisPlcCodeViewerControl {210,620,190,120, BackColor 0xFFE8FFFF}; C_ScreenWin1 HmiScreenWindow {410,620,390,200, Screen Main} + `itemSet CaptionColor 0xFF884400`; C_Faceplate1 HmiFaceplateContainer {10,750,190,120} + `itemSet CaptionColor 0xFF008844`. Rejected: HmiProcessControl, HmiCustomWebControlContainer, HmiCustomWidgetContainer (short and full name).
5. FP_Interface: I_Rect1 HmiRectangle {20,20,120,80, BackColor 0xFF808080, BorderColor 0xFF000080, BorderWidth 2}; I_Text1 HmiText {160,20,200,40, Text “F01 Interface Text”, ForeColor 0xFF006600}; I_IOField1 HmiIOField {20,120,140,40, BackColor 0xFFFFFFE0, OutputFormat {F0}}; I_Bar1 HmiBar {200,100,60,160, BackColor 0xFFE0E0FF}. `itemBindTag` I_IOField1 binding=processValue tagName=HmiTag_Ext1; I_Bar1 binding=dynamization propertyName=ProcessValue evaluationType=direct; I_Rect1 dynamization BackColor singleBit colorFalse 0xFF808080 colorTrue 0xFF00AA00; I_Rect1 dynamization BorderColor range rangeEntries [{0,0,0xFF0000FF},{1,1,0xFFFF0000}]. `itemBindScript` I_Text1 ForeColor script `let v = Tags("HmiTag_Ext1").Read(); return v ? 0xFFFF0000 : 0xFF006600;` globalDefinition `// F01 global definition I_Text1`; screen FP_Interface (without name) BackColor script `return Tags("HmiTag_Ext1").Read() ? 0xFFFFF0F0 : 0xFFF0F0FF;` (both SyntaxCheck ok). **Correction (MCP-025):** `itemBindTag binding=processValue` only wrote the static text “HmiTag_Ext1” into ProcessValue on I_IOField1 and S_IOField1, no tag dynamization (truth: `"ProcessValue":"HmiTag_Ext1"`, `dynamizations` empty). The only real ProcessValue binding in the sample is on I_Bar1 (binding=dynamization, direct).
6. FP_Script: S_Button1 HmiButton {20,20,160,50, Text “F01 Set Tag”, BackColor 0xFF006699}; `itemEventCreate` S_Button1 Tapped (`Tags("HmiTag_Ext1").Write(true);`) and Down (`HMIRuntime.Trace("F01 button down");`); S_IOField1 HmiIOField {20,100,160,40, BackColor 0xFFE0FFE0} + `itemBindTag processValue HmiTag_Ext1` + `itemEventCreate propertyName=ProcessValue eventType=Change` (`HMIRuntime.Trace("F01 value changed: " + value);`); screen event Loaded (`HMIRuntime.Trace("F01 screen loaded");`). Every script starts with a comment line `// F01 …`. S_Switch1 HmiToggleSwitch was created, events on it failed (“Ambiguous match found.”); afterwards `itemDelete` (it blocked reading FP_Script). `import scriptModule` (F01_Module.js, with and without a guessed .yml) failed.
7. FP_Flash (extra, flashing dynamization): X_Rect1 HmiRectangle {20,20,120,80, BackColor 0xFF00CC66} + `itemBindTag` dynamization Visible direct HmiTag_Ext1 → `exportXml` → added a `<Dynamization>` with DynamizationType Flashing, PropertyName BackColor, AlternateColor 0xFFFF00FF, Color 0xFF00CC66, FlashingCondition Always, FlashingRate Fast in the XML (`_scripts\FP_Flash.import.xml`) → `importXml` (dynamizationCount 2, no warnings).
8. Main: empty.

Further faceplate samples (by hand in TIA, user):

| Sample | Predecessor | Change | Result according to Openness |
| --- | --- | --- | --- |
| F05_fp_iface_instance (.zap21 only) | F03 (user's working copy F01_hmi_allobjects) | by hand in TIA, 2026-10-21 06:3x: dragged FP_Basic from the project library onto Main (no interconnection); FP_Interface: interface tag HmiTag_Ext1 (Bool), compiled, released 0.0.1. User's archive `000_empty_20261021_0640.zap21`, copy unchanged | instance “Faceplate container_1” ContainedType `V0.0.1\FP_Basic`; FP_Interface 0.0.1 Committed/Default, interface 1 element HmiTag_Ext1 (via a helper instance in the working copy). Truth `F05_fp_iface_instance_truth\`, evaluation `analysis\F3_faceplate\F05.md` |
| F16_iface_props (.zap21, copy of the user's F16_iface_props_20261023_1629.zap21) | F05 (user's working copy) | by hand in TIA, 2026-10-23: FP_Interface Property Interface with one property each of all 11 data types, released 0.0.2 (P_ULInt as LInt) and 0.0.3 (ULInt) | typesList FP_Interface 0.0.1/0.0.2/0.0.3 (Default); interface of a helper instance V0.0.3; truth `F16_iface_props_truth\`, evaluation `analysis\F3_faceplate\F16.md` |
| F18_iface_event (.zap21, copy of the user's F18_iface_event_20261023_1653.zap21) | F16 (user's working copy) | by hand in TIA, 2026-10-23: FP_Interface interface event EvTest(Nr Int, Flag Bool), Button_1 OnTapped RaiseEvent, released 0.0.4 and 0.0.5 | evaluation `analysis\F3_faceplate\F18.md` (part A, truth follows with part B) |
| F18b_event_handler (.zap21, copy of the user's F18b_event_handler_20261023_1700.zap21) | F18 | by hand: instance Faceplate container_2 of FP_Interface 0.0.5 on Main, handler OnEvTest, interface values (Bool, LInt 42, ULInt 89, LReal 3.5, color, WString, text, graphic, text list) | truth `F18b_event_handler_truth` (itemGet, interface, EventHandlers), evaluation `analysis/F3_faceplate/F18.md` |
| F12_iface_tags (.zap21, copy of the user's F12_iface_tags_20261023_1718.zap21) | F18b | by hand: FP_Interface Tag Interface T_Real, T_DateTime, T_WString (Bool), T_Array Array[0..3] of Int, T_Udt (HMI UDT User_data_type_1), released 0.0.6 | evaluation `analysis/F3_faceplate/F12.md` |
| F40_siemens_demo_bestpractices (local only, .gitignore) | Siemens demo project DemoProject_BestPractices_V4 (V21 Update 3), provided by the user under <temp>\SiemensDemoProjects | copied unchanged (without lock file) | 13 faceplate types, 14 versions, 14 instances; truth and evaluation local only, not in the repo (Siemens demo project) |

## WinCC Unified: screen items (Step U5)

Created 2026-10-20, own TIA instance PID 25328 (new via `acquire` after 10084 was terminated from outside at about 16:12 UTC; `lock` 14400 s), all calls with `instance=25328`. Procedure as in U4; ground truth `screens.json`, `screens_full.json` (`get detail=full`) or `screens_items.json` (`get` with dynamizations/events). Type map of all objects that can be created via MCP: F01_hmi_allobjects (faceplate thread, read only). Script modules: no create operation (import only, format undocumented) – no sample. Faceplates: faceplate thread.

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| 115_hmi_item_rect | 110_hmi_screen | `itemCreate(Screen_1, Rectangle_1, itemType=HmiRectangle)` | 96×48 at 0/0, BackColor 0xFFC8CDD7, BorderColor 0xFF7D7D85 |
| 116_hmi_item_rect_pos | 115_hmi_item_rect | `itemSet(Rectangle_1, Left=100)` | Left 100 |
| 117_hmi_item_rect_color | 115_hmi_item_rect | `itemSet(Rectangle_1, BackColor=0xFFFF0000)` | BackColor 0xFFFF0000 |
| 118_hmi_item_text | 110_hmi_screen | `itemCreate(Text_1, HmiText, attributes={Text:"Hello"}, language=en-US)` | 160×40, Text en-US “Hello” |
| 119_hmi_screen_tag | 110_hmi_screen | `create, tag, HmiTag_IO, dataType=Int` (base for 120/121/124) | internal Int tag |
| 120_hmi_item_iofield | 119_hmi_screen_tag | `itemCreate(IOField_1, HmiIOField)` | 160×40, InputOutput, without ProcessValue |
| 121_hmi_item_iofield_pvtext | 120_hmi_item_iofield | `itemBindTag(IOField_1, tagName=HmiTag_IO, binding=processValue)` (without `binding`: error “args.name is required”) | ProcessValue = **static text** “HmiTag_IO”, no dynamization |
| 124_hmi_item_iofield_dyn | 120_hmi_item_iofield | `itemBindTag(IOField_1, tagName=HmiTag_IO, binding=dynamization, propertyName=ProcessValue, evaluationType=direct)` | tag dynamization ProcessValue ← HmiTag_IO (DataType Int) |
| 122_hmi_item_button | 110_hmi_screen | `itemCreate(Button_1, HmiButton)` | 160×40, Text en-US “Text” |
| 123_hmi_button_event | 122_hmi_item_button | `itemEventCreate(Button_1, eventType=Tapped, script='HMIRuntime.Trace("Button_1 tapped");')` | event Tapped with ScriptCode |

## WinCC Unified: runtime files (Step U8)

Own TIA instance PID 25328, evaluation in `docs\spec\unified-runtime.md`.

| Sample | Predecessor | Change (MCP) | Result |
| --- | --- | --- | --- |
| 126_hmi_subnet | 124_hmi_item_iofield_dyn | `TiaNetworkSubnet(nodeCreateAndConnect, PLC_1, PLC_1/PROFINET interface_1, subnetType=Profinet)` + `nodeConnect(HMI_1, HMI_1.IE_CP_1/PROFINET Interface_1, subnet=PN/IE_1)` (2 calls, necessary) | PN/IE_1 with PLC_1 (192.168.0.1) and HMI_1 (192.168.0.2); `IM\HMI\I\0\Saved` is discarded |

127_hmi_compiled: aborted (compiling requires the runtime password of the panel, see U8), not kept.

## WinCC Unified: gaps in the hmi parser (Step U10)

Created 2026-10-20, own TIA instance PID 25328 (128, 129) or 15092 (130–133, free instance via `acquire`), all calls with `instance=<PID>`. Ground truth `screens_full.json` (with `partGet Points`/`SelectionItems`) or `screens_items.json`. Diff: `<sample>_attrdiff.txt` (130–133).

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| 128_hmi_item_polyline | 110_hmi_screen | `itemCreate(Polyline_1, HmiPolyline)` | Points (0,0), (96,0), (96,48); LineWidth 1 |
| 129_hmi_item_listbox | 110_hmi_screen | `itemCreate(ListBox_1, HmiListBox)` | SelectionItems “Text 1”, “Text 2”, “Text 3” |
| 130_hmi_script_multiline | 122_hmi_item_button | `itemEventCreate(Button_1, Tapped, script=<7 lines with line/block comment, tab, empty line, indentation>)` | ScriptCode exactly with CRLF |
| 131_hmi_event_down | 122_hmi_item_button | `itemEventCreate(Button_1, Down, script='HMIRuntime.Trace("down");')` | event Down |
| 132_hmi_dyn_color | 124_hmi_item_iofield_dyn | `itemBindTag(IOField_1, HmiTag_IO, propertyName=BackColor, threshold=100, valueAbove=0xFFFF0000, valueBelow=0xFFFFFFFF)` | Range: -32768..100 → 0xFFFFFFFF, 101..32767 → 0xFFFF0000 |
| 133_hmi_dyn_script | 115_hmi_item_rect | `itemBindScript(Rectangle_1, propertyName=Visible, script='return true;')` | script dynamization Visible, trigger AutomaticTags |

## WinCC Unified: last remainders (Step U11)

Own TIA instance PID 15844, all calls with `instance=15844`.

| Sample | Predecessor | Change (MCP) | Result according to Openness |
| --- | --- | --- | --- |
| 134_hmi_dyn_range3 | 124_hmi_item_iofield_dyn | `itemBindTag(IOField_1, HmiTag_IO, propertyName=Width, rangeEntries=[0..10→100, 11..20→150, 21..30→200])` | Range with 3 ranges (numeric values) |
| 135_hmi_item_alarmind | 110_hmi_screen | `itemCreate(AlarmInd_1, HmiAlarmIndicator)` | Defaults; `unsetProperties` filter |

## Hardware: module parameters of the CPU (Step H2, H-HW-01)

TIA V21 Update 3, created 2026-10-24 with TiaMcpXE directly: own instance PID 18204 (`TiaInstance(acquire)` + `lock`, terminated at the end), per sample copy of the folder `002_cpu` → `TiaProject(open)` → truth before → exactly one change to `PLC_1` → save → truth after → archive (`<sample>.zap21`) → close. The project file is still named `000_empty.ap21`. Ground truth in `<sample>_truth\`: `attributes_before.json`/`attributes_after.json` (all Openness attributes, name/value/writable, of Rail_0, PLC_1, Card reader/writer_1, CPU display_1, OPC UA_1, PROFINET interface_1, Port_1, Port_2 via `TiaDeviceItem(getDetails)`), `accessLevel_before/after.json` (`serviceGet PlcAccessLevelProvider`), `device_PLC_1_details.json` (`TiaDevice(getDetails)`). Attribute-level diff to 002: `<sample>_attrdiff.txt`.

| Sample | Predecessor | Change (MCP) | Openness before → after | in the file (new expando) |
| --- | --- | --- | --- | --- |
| 200_hw_cycle_time | 002_cpu | `TiaDeviceItem(setDetails, PLC_1, {"CycleMaximumCycleTime":200})` | CycleMaximumCycleTime 150 → 200 | `CPU exec unit_1`: `MaxCycleConfig` = 200 |
| 201_hw_clock_memory | 002_cpu | `setDetails {"ClockMemoryByte":true}`, then `{"ClockMemoryByteAddress":100}` (the attribute only exists after activation) | ClockMemoryByte False → True, ClockMemoryByteAddress – → 100 | CpuCommon `PLC_1`: `UseClockFlags` = True, `ClockFlags_Local` = 100, `ClockFlags_CreatedAt` = 100; 9 new PLC tags `Clock_Byte`, `Clock_10Hz` … `Clock_0.5Hz` |
| 202_hw_startup | 002_cpu | `setDetails {"StartupActionAfterPowerOn":1}` | StartupActionAfterPowerOn 3 → 1 | `CPU exec unit_1`: `PowerOnAction` = 1 |
| 203_hw_webserver | 002_cpu | `setDetails {"WebserverActivate":true}` | WebserverActivate False → True; newly visible 11 Webserver* attributes (DBNumber 333, HttpsOnly True …); PROFINET interface_1 UseWebServerForInterface False → True | CPU (S7ControllerTargetData): `WebServerActive` = True; `PROFINET interface_1`: `EnableWebServerAccess4IE` = True |
| 204_hw_access_level | 002_cpu | `TiaDeviceItem(serviceSet, PLC_1, PlcAccessLevelProvider, {"PlcProtectionAccessLevel":"FullAccess"})` | PlcProtectionAccessLevel NoAccess → FullAccess; newly visible ProtectionEnablePutGetCommunication False | CpuCommon `PLC_1`: `ProtectionLevel` = 1 |

Probe beforehand on a throwaway copy `TMP\h2probe` (discarded). All changes were done via MCP.

## Hardware: I/O addresses, HW identifiers and process image (Step H3, H-HW-02)

TIA V21 Update 3, created 2026-10-24 with TiaMcpXE directly: own instance PID 3740 (`acquire` + `lock`, terminated at the end), procedure as in H2 (folder copy of the predecessor → open → exactly one change → save → truth → archive → close). Ground truth in `<sample>_truth\`: `addressList.json` and `addressSpaceList.json` (`TiaDeviceItem`), `device_PLC_1_details.json`, `hardware.aml` (`TiaCax(export)`, project), `attributes.json` (Openness attributes of the new module), for 207/208 `OB30.attributes.json` (`TiaPlcSoftware(attributeGet)`), for 209 additionally `hwIdentifierList.json` (afterwards: folder 209 opened again, read, closed without saving). Attribute-level diff to the predecessor: `<sample>_attrdiff.txt`. OB source: `tools\sources\OB_CyclicInterrupt.xml` (SimaticML, derived from the export of Main; SIMATIC SD cannot specify the OB type).

| Sample | Predecessor | Change (MCP) | Openness after |
| --- | --- | --- | --- |
| 205_hw_di_module | 002_cpu | `TiaDeviceItem(plugNew, typeIdentifier=OrderNumber:6ES7 521-1BL00-0AB0/V2.2, name=DI_1, positionNumber=2, parentDeviceItemPath=Rail_0)` | DI_1/DI_1 Input 0..3 (32 bit), HW identifier 257 |
| 206_hw_io_start | 205_hw_di_module | `addressSetStart(DI_1/DI_1, startAddress=10)` | Input 10..13 |
| 207_hw_ob_cyclic | 205_hw_di_module | `TiaPlcSoftware(import, block, OB_CyclicInterrupt.xml, importMode=None)` | OB30 “Cyclic interrupt”, CyclicTime 100000 µs, ProcessImagePartNumber 65535 |
| 208_hw_process_image | 207_hw_ob_cyclic | `processImageAssign(DI_1/DI_1, plcDeviceItemPath=PLC_1, obName=Cyclic interrupt, processImage=1)` | address in PIP 1, OB30 ProcessImagePartNumber 1 |
| 209_hw_dq_module | 205_hw_di_module | `plugNew(OrderNumber:6ES7 522-1BL01-0AB0/V1.1, DQ_1, positionNumber=3)` | DQ_1/DQ_1 Output 0..3, HW identifier 258; 17 HW identifiers |

Probe beforehand on `TMP\h3probe` (discarded). Deviation from the plan: 207 creates the cyclic interrupt OB because the process image partition needs an OB; H4/H5 therefore move to 210 ff.

## Hardware: networks, IO systems, distributed I/O, connections (Step H4)

TIA V21 Update 3, created 2026-10-24 with TiaMcpXE directly, own instance PID 3740, procedure as in H2 (folder copy of the predecessor → open → exactly one change → save → truth → archive → close). Ground truth in `<sample>_truth\`: per station `device_<station>_details.json`, `interfaces_<station>.json` (`TiaNetwork(interfacesDiscover)`), `ports_<station>.json`; per interface `node_<path>.json` (`TiaNetworkNode(getAttributes)`); `subnets.json` (`TiaNetworkSubnet(list)`); `iosystem_<path>.json` (`TiaNetworkIoSystem(get)`); `connections_PLC_1.json` (`TiaNetwork(connectionList)`); `hardware.aml` (`TiaCax(export)`); for 217 additionally `addressList_IO_1.json`, `addressSpaceList_PLC_1.json`, `hwIdentifierList_IO_1.json`. Attribute-level diff to the predecessor: `<sample>_attrdiff.txt`. Probe beforehand on `TMP\h4probe` (discarded).

| Sample | Predecessor | Change (MCP) | Openness after |
| --- | --- | --- | --- |
| 210_hw_ip_address | 002_cpu | `TiaNetworkNode(setIpv4, PLC_1/PROFINET interface_1, address=192.168.0.10)` | Address 192.168.0.10 |
| 211_hw_pn_name | 002_cpu | `setProfinetName(PLC_1/PROFINET interface_1, plc-test)` (switches off PnDeviceNameAutoGeneration) | PnDeviceName plc-test |
| 212_hw_subnet | 002_cpu | `TiaNetworkSubnet(nodeCreateAndConnect, PLC_1/PROFINET interface_1, subnetName=PN/IE_1, subnetType=Profinet)` | subnet PN/IE_1 with X1 |
| 213_hw_io_system | 212_hw_subnet | `TiaNetworkIoSystem(create, PLC_1/PROFINET interface_1)` | PROFINET IO system number 100 |
| 214_hw_et200sp_station | 213_hw_io_system | `TiaDevice(create, name=IO_1, deviceType=OrderNumber:6ES7 155-6AU01-0BN0/V4.2, deviceItem=IM_1)` | station IO_1 (IM 155-6 PN ST), not networked |
| 215_hw_io_subnet | 214_hw_et200sp_station | `TiaNetworkSubnet(nodeConnect, IO_1, IM_1/PROFINET interface, PN/IE_1)` | IE1 192.168.0.2 in PN/IE_1 |
| 216_hw_io_device | 215_hw_io_subnet | `TiaNetworkIoSystem(ioConnect, IO_1, IM_1/PROFINET interface → PLC_1/PROFINET interface_1)` | IO system with 1 IO device, device number 1 |
| 217_hw_io_module | 216_hw_io_device | `TiaDeviceItem(plugNew, IO_1, OrderNumber:6ES7 131-6BF01-0BA0/V0.0, DI_8, positionNumber=1, parentDeviceItemPath=Rack_0)` (without server module) | DI_8/DI_8 Input 0 (8 bit) |
| 218_hw_s7_connection | 212_hw_subnet | `TiaNetwork(connectionCreate, PLC_1, connectionType=S7, localInterfacePath=PLC_1/PROFINET interface_1)`, partner unspecified | S7_Connection_1, ID 100 |
| 219_hw_tcp_connection | 212_hw_subnet | `connectionCreate(TCP, …)`, partner unspecified | TCP_Connection_1, ID 100, local port 2000 |
| 220_hw_cm_dp | 002_cpu | `plugNew(PLC_1, OrderNumber:6GK7 542-5DX00-0XE0/V2.0, CM_DP, positionNumber=2)` | CM 1542-5, PROFIBUS address 2 |
| 221_hw_profibus | 220_hw_cm_dp | `nodeCreateAndConnect(CM_DP/PROFIBUS interface, subnetType=Profibus, subnetName=PROFIBUS_1)` | subnet PROFIBUS_1 |
| 222_hw_dp_master | 221_hw_profibus | `TiaNetworkIoSystem(create, CM_DP/PROFIBUS interface)` | DP master system number 1 (called “PROFINET IO-System” by TiaMcpXE, MCP-084) |
| 223_hw_dp_station | 222_hw_dp_master | `TiaDevice(create, DP_1, OrderNumber:6ES7 155-6BU01-0CN0/V4.2, deviceItem=IM_DP)` | station DP_1 (IM 155-6 DP HF) |
| 224_hw_dp_subnet | 223_hw_dp_station | `nodeConnect(DP_1, IM_DP, PROFIBUS_1)` | PB1 address 3 |
| 225_hw_dp_slave | 224_hw_dp_subnet | `ioConnect(DP_1, IM_DP → PLC_1/CM_DP/PROFIBUS interface)` | DP master system with 1 slave |

Deviation from the plan: the chain is finer than planned (station, subnet, IO system each a separate sample), PROFIBUS needs the CM 1542-5 (the CPU 1513-1 PN has no DP interface). H5 moves to 226 ff.

## Hardware: topology and synchronization (Step H5)

TIA V21 Update 3, created 2026-10-25 via the HTTP interface of TiaMcpXE (tools\tiaclient.py, bound to the instance PID 1968 started by the server), procedure as in H4 (folder copy of the predecessor → open → exactly one change → save → truth → archive → close). Ground truth in `<sample>_truth\` as in H4, plus `change.json` (call and result), `portDetails_<station>.json` (getDetails per port + `NetworkPort.ConnectedPorts`), `iface_<path>.json` (getDetails of the interface), `syncDomains_<subnet>.json` (`TiaNetworkSubnet(syncDomainList)`). Attribute-level diff to the predecessor: `<sample>_attrdiff.txt`. Probe beforehand on `TMP\h5probe` (discarded).

| Sample | Predecessor | Change (MCP) | Openness after |
| --- | --- | --- | --- |
| 226_hw_port_link | 217_hw_io_module | `TiaNetworkNode(portConnect, PLC_1 PLC_1/PROFINET interface_1/Port_1 ↔ IO_1 BA 2xRJ45/Port_1)` | both ports ConnectedPorts 1 |
| 227_hw_send_clock | 226_hw_port_link | `TiaDeviceItem(setDetails, PLC_1/PROFINET interface_1, {"PnSendClock":2000000})` | PnSendClock 2000000 ns |
| 228_hw_sync_domain | 226_hw_port_link | `TiaNetworkSubnet(syncDomainSet, PN/IE_1, Sync-Domain_1, {"HighPerformanceActive":true})` | HighPerformanceActive True |

Deviation from the plan: sync role/IRT and isochronous mode could not be set (MCP-086); instead send clock and sync domain attribute. H6 stays at 229.

## Hardware: GSD devices (Step H6)

Sample 229_hw_gsd (created by hand by the user, 2026-10-25) is not in the repo: the folder and `.zap21` contained the GSDML files of the manufacturers SEW-EURODRIVE (MOVI-C UHX45) and Danfoss (FC), which TIA copies to `AdditionalFiles\GSD`. Findings in docs\spec\hw-config.md (GSD devices).

## Hardware: services and protection (Step H7)

**By hand by the user** (2026-10-25, TIA V21 Update 3) as a chain in one project with the state of `039_fw41` (CPU 1516-3 PN/DP V4.1; the attrdiffs show only the respective change against `039_fw41.zap21`; the work was done in the folder `samples\039_fw41` itself), saved after each step and archived to `<temp>\H7\<name>_<timestamp>.zap21`. Taken over as copy `<sample>.zap21` (originals unchanged), folder created via `TiaProject(retrieve)` in an own instance (PID 14864), then open → truth → close without saving. Ground truth: `device_PLC_1_details.json`, `interfaces_PLC_1.json`, `item_<path>.json` (getDetails of the CPU and all interfaces), `services_PLC_1.json` (all available Openness services of the CPU). Attribute-level diff to the predecessor: `<sample>_attrdiff.txt`.

| Sample | Predecessor | Change (TIA user interface) | File |
| --- | --- | --- | --- |
| 230_hw_opcua_on | 039_fw41 | OPC UA → Server → “Activate OPC UA server”, runtime license selected | `OpcUaEnableServer` True, `SelectedOpcUaLicense` 2 |
| 231_hw_opcua_port | 230_hw_opcua_on | OPC UA server port 4840 → 4841 | `OpcUaPortNumber` 4841 |
| 232_hw_ntp | 231_hw_opcua_port | time synchronization “Set NTP server in the project”, server 1 = 192.168.0.100, interval 10 s | `TimeSyncRole` 2, `TimeSyncNtpServer1` |
| 233_hw_timezone | 232_hw_ntp | time zone “(UTC +02:00) Beirut”, daylight saving time deactivated | `LocalTimeZone` 82, `EnableDaylightSavingsTime` False |

Continuation of H7 via MCP (2026-10-25, TiaMcpXE over HTTP with tools\tiaclient.py, own instance PID 7388), procedure as in H5 (folder copy of the predecessor → open → exactly one change → save → truth → archive → close). Ground truth as in 230–233, plus `change.json` (call and result, passwords replaced) and `security_PLC_1.json` (plcMasterSecretGet, plcCertificateList, webserverCertificateGet, UMAC users and roles). Test passwords: `OpenPEData-Test-237` (confidential data), `OpenPEData-Test-240` (user testuser), only for these samples.

| Sample | Predecessor | Change (MCP) | File |
| --- | --- | --- | --- |
| 234_hw_sysdiag | 233_hw_timezone | `TiaDeviceItem(setDetails, PLC_1, {"SuppressDeactivatingSystemDiagnosticsAlarms":true})` | expando of the same name |
| 236_hw_access_control | 234_hw_sysdiag | `setDetails(PLC_1, {"PlcAccessControlConfiguration":"Disabled"})` | `AccessControlAtRuntime` 0 + account lockout |
| 237_hw_confidential | 236_hw_access_control | `TiaProjectSecurity(plcMasterSecretProtect, PLC_1)` | `IsMasterSecretConfigured`, `PkiContainerCache` |
| 238_hw_certificate | 237_hw_confidential | `TiaProjectSecurity(plcCertificateCreate, PLC_1, subjectCommonName=PLC_1-Test)` → ID 2, Usage WebServer | `PkiContainerCache` larger |
| 240_hw_umac_user | 238_hw_certificate | `TiaUmacUser(userAdd, testuser)` | `CustomProjectUser` |
| 239_hw_webserver | 240_hw_umac_user | `setDetails(PLC_1, {"WebserverActivate":true})` | `WebServerActive`, `WebServerCertificateId` 3 |
| 241_hw_umac_role | 239_hw_webserver | `TiaUmacRole(create, WebDiag)` + `deviceRightAssign(WebDiag, PLC_1, WebAllowDiagnostics)` | `CustomRole` |
| 242_hw_umac_user_role | 241_hw_umac_role | `TiaUmacUser(userRoleAssign, testuser, WebDiag)` | `RoleToUserAssociation` |

235 (PUT/GET) is dropped: Openness does not know the setting (constraint GOALS.md 4.).

## Hardware: further modules (rounding off Z1.1)

Via MCP (2026-10-25, TiaMcpXE over HTTP, own instance PID 16096), procedure as in H5. Ground truth: `device_PLC_1_details.json`, `addressList_PLC_1.json`, `hwIdentifierList_PLC_1.json`, `channelList_PLC_1.json`, `module_<path>.json` (getDetails of the new module), `change.json`.

| Sample | Predecessor | Change (MCP) | Openness after |
| --- | --- | --- | --- |
| 243_hw_aiaq_module | 209_hw_dq_module | `TiaDeviceItem(plugNew, OrderNumber:6ES7 534-7QE00-0AB0/V1.0, AIAQ_1, positionNumber=4, parentDeviceItemPath=Rail_0)` | AIAQ_1/AIAQ_1 Input 4..11 (64 bit), Output 4..7 (32 bit), 6 channels |
| 244_hw_aiaq_value_status | 243_hw_aiaq_module | `setDetails(AIAQ_1, {"EnableValueStatus":true})` | Input 4, 80 bit |
| 245_hw_tm_count | 243_hw_aiaq_module | `plugNew(OrderNumber:6ES7 550-1AA01-0AB0/V2.0, TM_Count_1, positionNumber=5)` | Input 32 (96 bit), Output 24 (64 bit) |

## Hardware: Safety

Via MCP (2026-10-25, TiaMcpXE over HTTP, own instance PID 19608, Safety license available), procedure as in H5. Ground truth: `device_PLC_1_details.json`, `addressList_PLC_1.json`, `item_PLC_1.json` (getDetails of the CPU), `module_<path>.json` (getDetails of the F-module), `safety_PLC_1.json` (safetySettingsGet, safetyPasswordIsSet, safetyRuntimeGroupList, safetySignatureGet, safetyGlobalSettingsGet), `change.json`. Test password for the F-program: `OpenPEData-Test-253`, only for this sample.

| Sample | Predecessor | Change (MCP) | File |
| --- | --- | --- | --- |
| 246_hw_fcpu | 000_empty | `TiaDevice(create, PLC_1, OrderNumber:6ES7 513-1FL02-0AB0/V2.9)` | F-CPU with Safety Administration |
| 247_hw_f_settings | 246_hw_fcpu | `TiaProjectSecurity(safetySettingsSet, {"ActivationOfFChangeHistory":true})` | `ActivateFChangelog` |
| 248_hw_f_monitoring | 247_hw_f_settings | `setDetails(PLC_1, {"Failsafe_DefaultFmonitoringTimeCentral":200})` | `Failsafe_Central_F_WD_Time` |
| 249_hw_fdi_module | 248_hw_f_monitoring | `plugNew(OrderNumber:6ES7 526-1BH00-0AB0/V1.0, FDI_1, positionNumber=2)` | F-DI, `FModuleInfo`, F-I/O DB |
| 250_hw_fdi_dest_address | 249_hw_fdi_module | `setDetails(FDI_1/FDI_1, {"Failsafe_FDestinationAddress":10})` | `Failsafe_F_Dest_Add` |
| 251_hw_fdi_shortcircuit | 250_hw_fdi_dest_address | `setDetails(FDI_1/FDI_1, {"Failsafe_ShortCircuitTest_0":false})` | `Failsafe_Short_Circuit_Test_0` |
| 252_hw_f_runtime_group | 251_hw_fdi_shortcircuit | `safetyRuntimeGroupSet(F-runtime group 1, {"MaximumCycleTime":200000})` | `MaxCycleTime` |
| 253_hw_f_password | 252_hw_f_runtime_group | `safetyPasswordSet(PLC_1)` | `FProgramPasswordPlus` (encrypted) |

## PLC: alarms (Z1.2 P1)

Via MCP (2026-10-25, TiaMcpXE over HTTP, own instance PID 18800), procedure as in H5. Ground truth: `blocks.json`, `simaticml\`, `alarmTexts.xlsx` (`TiaPlcSoftware(alarmTextExport)`), `alarmClasses.dat` (`alarmClassExport`), `units.json`, `list_watchTable.json`, `list_technologyObject.json`, `change.json`.

| Sample | Predecessor | Change (MCP) | Openness after |
| --- | --- | --- | --- |
| 041_plc_alarm_fb | 002_cpu | `TiaPlcSoftware(sourceImport, FB_Alarm.scl)`: FB with `Alarm_1 : Program_Alarm` and call `#Alarm_1(SIG := #Sig)` | FB_Alarm (no export of the instance texts: “no exportable alarm”) |
| 042_plc_alarm_idb | 041_plc_alarm_fb | `TiaPlcSoftware(create/block, FB_Alarm_DB, InstanceDB, instanceOf=FB_Alarm)` | 1 instance alarm, everything “Inherited from Type” |
| 043_plc_alarm_text | 042_plc_alarm_idb | `alarmTextImport` (export of 042, D2 “Testmeldung Pumpe”, F2 “Infotext Pumpe”) | alarm text and info text en-US |
| 044_plc_alarm_class | 043_plc_alarm_text | `alarmTextImport` as in 043 plus C2 “Acknowledgement” | alarm class Acknowledgement |

## PLC: watch tables and software units (Z1.2 P3/P4)

Via MCP (2026-10-25, own instance PID 18800), ground truth as in 041–044, plus `watchtables\` (SimaticML), `watchTable_<name>.json` (`get/watchTable`).

| Sample | Predecessor | Change (MCP) | Openness after |
| --- | --- | --- | --- |
| 047_plc_watch_table | 003_tagtable | `TiaPlcSoftware(create/watchTable, WT_Test)` | 1 watch table, empty |
| 048_plc_watch_entry | 047_plc_watch_table | `entryAdd(WT_Test, {"Name":"\"Tag_Bool_1\"","DisplayFormat":"Bool","ModifyValue":"TRUE"})` | 1 entry %M0.0 Bool TRUE |
| 049_plc_unit | 048_plc_watch_entry | `unitCreate(Unit_A)` | 1 unit |
| 050_plc_unit_b | 049_plc_unit | `unitCreate(Unit_B)` | 2 units |
| 051_plc_unit_relation | 050_plc_unit_b | `unitRelationCreate(Unit_A, relatedObject=Unit_B)` | Unit_A → Unit_B (SoftwareUnit) |

## PLC: technology objects (Z1.2 P5)

Via MCP (2026-10-25, own instance PID 18800), ground truth as in 041 ff., plus `to_<name>.json` (`get/technologyObject` per TO).

| Sample | Predecessor | Change (MCP) | Openness after |
| --- | --- | --- | --- |
| 052_plc_to_param | 036_tech | `TiaPlcSoftware(parameterSet, TO_Speed_5_0, {"DynamicDefaults.Velocity":750})` | 750 |
| 053_plc_to_inverse | 052_plc_to_param | `parameterSet(TO_Speed_5_0, {"Actor.InverseDirection":true})` | true |

## PLC: OPC UA and ProDiag (Z1.2 P6/P7)

Via MCP (2026-10-25, own instance PID 18800), ground truth as in 041 ff., plus `opcUaInterfaces.json`, `supervisions.xlsx` (`supervisionExport`).

| Sample | Predecessor | Change (MCP) | Openness after |
| --- | --- | --- | --- |
| 054_plc_opcua_interface | 003_tagtable | `TiaPlcSoftware(opcUaInterfaceCreate, server, SI_Test)` | 1 server interface, active |
| 055_plc_opcua_disabled | 054_plc_opcua_interface | `opcUaInterfaceSet(server, SI_Test, {"Enabled":false})` | inactive |
| 056_plc_prodiag_fb | 003_tagtable | `TiaPlcSoftware(create/block, ProDiag_FB, FB, programmingLanguage=ProDiag)` | ProDiag FB, no supervision |
| 057_plc_prodiag_supervision | 056_plc_prodiag_fb | `supervisionImport` (export of 056 + row: ProDiag_FB, 1, "Tag_Bool_1", Sup_1, Operand, category 1, text “Testueberwachung”) | 1 operand supervision |

## PLC: text lists (Z1.2 P2)

Ground truth as in 041 ff. (`alarmTextLists.xlsx` = `TiaPlcSoftware(alarmTextListExport)`).

| Sample | Predecessor | Change | Openness after |
| --- | --- | --- | --- |
| 045_plc_textlist | 044_plc_alarm_class | **by hand by the user** (2026-10-27, TIA V21 Update 3, in `<temp>\Z12\work`): user text list TL_Test, decimal, 0 = “Aus”, 1 = “Ein”; archive `<temp>\Z12%_plc_textlist.zap21` taken over as a copy, folder via `TiaProject(retrieve)` in an own instance (PID 14664), truth on the copy, close without saving | 1 text list, 2 entries |
| 046_plc_textlist_entry | 045_plc_textlist | `alarmTextListImport` (export of 045, TextListEntry D3 “An”) | entry 1 “An” |
| 058_plc_textlist_range | 046_plc_textlist_entry | `alarmTextListImport` (export of 046 + row TL_Test, 2, 5, “Stoerung”) | 3 entries |
| 059_db_symbols | 029_fb_idb_value | `documentWrite` of the sources `tools\sources\dbsym\*.s7dcl` (UDT_Inner with elements Temp/Input/Static/Return, UDT_Outer, DB_AllTypes, DB_NonOpt, DB_OfUdt, DB_ArrayDb, DB_ArrayInt, FB_Multi, FB_Multi_DB, FB_Top, FB_Top_DB), `memberSet` DB_AllTypes (vInt not accessible, vDInt not writable, vReal not visible, vWord retentive), compile 0/0, TIA V21, 2026-11-05 | 12 DBs, 7 of them new (all elementary types, Struct 3 levels, arrays 1D/2D/3D with negative bounds, Array of Struct/UDT/String, non-optimized, ARRAY DB of Int and of UDT, DB of type UDT_Outer, IDBs with multi-instances TON/CTU/R_TRIG/FB on two levels, start values under array elements). Truth `059_db_symbols_truth` (Save-Truth: SimaticML + SD per DB/UDT). Cannot be created: single-instance DB of an IEC timer (`TON_TIME` not in the project via Openness) |
| 060_db_attrs | 059_db_symbols | `memberSet` DB_AllTypes (vUSInt write-protected, vUInt setpoint, vUDInt not accessible, vLInt not visible and write-protected, vULInt write-protected and setpoint, vSetpoint and deep.l1.l2.v setpoint, aStruct not visible, aUdt retentive) and DB_NonOpt (i retentive, and with it the whole DB; st.y write-protected); `documentWrite` `tools\sources\dbattr\UDT_Inner.s7dcl` and `FB_Multi.s7dcl` (attributes via S7_Access/S7_Visibility/S7_Setpoint, VAR RETAIN r1/r2), compile 0/0, TIA V21, 2026-11-05 | Truth `060_db_attrs_truth` (Save-Truth plus `<DB>.members.json` from memberGet with retain/externalAccessible/externalVisible/externalWritable/setpoint per tag) |
| 065_db_iec_timer | 060_db_attrs | TIA editor via VNC: “Add new block” → “Data block” “DBTon”, type IEC_TIMER (DB13, ends up under system blocks/program resources), PT start value T#2S; `documentWrite` `tools\sources\dbattr\FC_Ton.s7dcl` (calls `"DBTon".TON`, otherwise TIA removes the DB when compiling); compile 0/0, saved in TIA with Ctrl+S, TIA V21, 2026-11-07 | Truth `065_db_iec_timer_truth`: editor view (DBTon_editor.jpg, DBTon.txt) and SimaticML of FC_Ton; Openness does not list DBTon |
| 066_sys_fb_instances | 029_fb_idb_value | Via MCP (TIA V21, 2026-11-07): `documentWrite` `tools\sources\sysfb\FB_SysMulti.s7dcl` (64 system FBs as multi-instance, plus Array of TON_TIME and Array of TSEND_C), `create InstanceDB` FB_SysMulti_DB and 33 single-instance DBs of system FBs (TSEND/TRCV are dropped when compiling), `DB_Buf` and `FC_SysUse` (calls 29 single DBs), 10 IEC DBs via SD (`DB_IEC_*.s7dcl`, compileAfter false) and `FC_IecUse`, 7 technology objects (PID_Compact in the editor, PID_3Step 2.3, PID_Temp/CONT_C/CONT_S/TCONT_CP/TCONT_S 1.1 via `create technologyObject`); compile 0/0 | Truth `066_sys_fb_instances_truth`: SimaticML of all 38 DBs and 7 TO DBs, memberGet (`<DB>.members.json`; omits some members, e.g. the PID multi-instances) |
| 067_tsend_trcv_single | 066_sys_fb_instances | Via MCP (TIA V21, 2026-11-07): `create InstanceDB` DB_TSEND (TSEND) and DB_TRCV (TRCV), FC_TsendB via `documentWrite` with `"DB_TSEND".TSEND(...)` and `"DB_TRCV".TRCV(...)`; compile 0/0 | Truth `067_tsend_trcv_single_truth`: SimaticML and memberGet of DB_TSEND/DB_TRCV, SimaticML of FC_TsendB |
| 068_motion_tos | 029_fb_idb_value | Via MCP (TIA V21, 2026-11-07): `create technologyObject` mTO_SpeedAxis, mTO_PositioningAxis, mTO_SynchronousAxis, mTO_ExternalEncoder (each 6.0), `parameterSet VirtualAxis.Mode=1` for the axes, TM_Count_1 (6ES7 550-1AA01-0AB0 V2.0, slot 2), DB_Enc with `tel : PD_TEL81` (non-optimized); in the editor (VNC) encoder data connection “Data block” to DB_Enc.tel; compile without errors | Truth `068_motion_tos_truth` (tools\Save-DbTruth.ps1): SimaticML of the DBs and TOs, memberGet of the DBs |
| 069_safety_compiled | 249_hw_fdi_module | Via MCP: open, `compile` (0 errors), save, archive (TIA V21, 2026-11-07) | Truth `069_safety_compiled_truth`: SimaticML/memberGet Main_Safety_RTG1_DB, SimaticML Main_Safety_RTG1; editor view F_SystemInfo_DB, RTG1SysInfo, F00000_FDI_1 (`editor/*.jpg`, `editor/system_dbs.json`) |
| 070_db_khp_units | 029_fb_idb_value | Via MCP (TIA V21, 2026-11-07): `documentWrite` `tools\sources\khp\DB_Khp.s7dcl`, `FB_Khp.s7dcl`, `create InstanceDB` FB_Khp_DB, compile, truth exported, then `protection protect` DB_Khp and FB_Khp (test password Khp!Test2026), `unitCreate` Unit_A, in the editor (VNC) DB_Global via copy/paste to Unit_A (DB_Global_1); compile 0 | Truth `070_db_khp_units_truth`: SimaticML/memberGet of DB_Khp and FB_Khp_DB before the protection, exports after the protection (`*.protected.xml`), blocks.json/units.json, DB_Global_1.xml = export of DB_Global (059), editor view `editor/DB_Global_1_Unit_A.jpg` |
| 071_db_1200_300 | new project | Via MCP (TIA V21, 2026-11-07): `TiaProject(create)`, PLC_1200 (6ES7 214-1AG40-0XB0 V4.7) with `documentWrite` `tools\sources\s71200_300\UDT_12/DB_12Opt/DB_12Std/FB_12.s7dcl` and `create InstanceDB` FB_12_DB; PLC_300 (6ES7 315-2EH14-0AB0 V3.2) with `sourceImport` `S7300.scl` (UDT_300, DB_300, FB_300, FB_300_DB); compile of both PLCs 0 errors | Truth `071_db_1200_300_truth\<PLC>\blocks`: SimaticML and memberGet of all DBs, SimaticML of the UDTs |
| 072_motion_tcpu | new project | TIA V21, 2026-11-07: CPU 1515T-2 PN (6ES7 515-2TN03-0AB0 V3.1); via MCP `create technologyObject` axes (VirtualAxis.Mode=1), encoder, Cam, Cam_10k, LeadingAxisProxy, Kinematics, Interpreter*, TM_Timer_1 (6ES7 552-1AA00-0AB0, slot 2), DB_Enc (from 068); via VNC: OutputCam_1, CamTrack_1, MeasuringInput_1 under mTO_PositioningAxis, kinematics axes A1/A2, encoder on DB_Enc.tel.Sensor_1, TM Timer DI0 Timer DI, measuring input on channel 0, I-device with transfer area; compile: only mTO_LeadingAxisProxy has errors (no leading CPU) | Truth `072_motion_tcpu_truth`: SimaticML of all other TOs and DB_Enc (Save-DbTruth, `tools\Export-SubTos.ps1` for output cam/cam track/measuring input), memberGet DB_Enc, editor view mTO_LeadingAxisProxy (`editor/`) |
| 063_multi_plc | new project | Via MCP (2026-11-06, TIA V21, own instance, only `.zap21` in the repo): `TiaDevice(create)` PLC_A CPU 1516-3 PN/DP V2.9, PLC_B CPU 1214C DC/DC/DC V4.6, PLC_C CPU 1513-1 PN V2.9 with CP 1543-1 V2.2 (CP_1, slot 2), PLC_D CPU 1512SP-1 PN V4.1; master secret off, protection level full access, Anonymous with PLCAdministrator (test only). Subnets PN/IE_1 (PLC_A X1 192.168.10.1/24 with router 192.168.10.254, PLC_B X1 192.168.10.2/24 with PROFINET name “plc-b-manuell”), PN/IE_Werk (PLC_C X1 172.16.0.5/16), IE_Leitebene (CP_1 10.10.10.10/255.255.255.240, router 10.10.10.1), PROFIBUS_1 (PLC_A X3 address 2); PLC_A X2 10.0.0.1/8 without subnet; PLC_D X1 `IpProtocolSelection` OtherPath (set IP address on the device). `documentWrite` DB_Common in PLC_A, PLC_B and PLC_C with different content, DB_A_Std (non-optimized), FB_Motor + Motor1_DB (PLC_C), DB_D (PLC_D); compile 0 errors | Truth `063_multi_plc_truth` (`tools\Truth-MultiPlc.ps1`: Save-Truth per PLC, `<DB>.members.json` from memberGet, `network.json` with getAttributes of all network interfaces). Cross-check Dhcp (PLC_A X2) only in the working project: NodeIPConfiguration 1 |

## HMI Unified: logging (Z1.3 U1)

Via MCP (2026-10-29, own instance PID 5000), chain starting at 102_hmi_tag_int. Ground truth `<sample>_truth\HMI_1\`: `dataLog*.json`, `alarmLog*.json`, `auditTrail*.json`, `loggingTag*.json` (`TiaHmiUnified` list/get), plus runtimeSetting, languageAndFont, scriptModule, graphicList, textList, opcUaAlarmType, plantView, software; `change.json` with the MCP call.

| Sample | Predecessor | Change (MCP, `TiaHmiUnified`, device HMI_1) | Openness after |
| --- | --- | --- | --- |
| 136_hmi_datalog | 102_hmi_tag_int | `create dataLog DataLog_1` | 1 data log (LogMaxSize 1000, 7 days, SDX51, NoBackup) |
| 137_hmi_datalog_size | 136_hmi_datalog | `set dataLog DataLog_1 Settings.LogMaxSize 200` | LogMaxSize 200 |
| 138_hmi_logging_tag | 137_hmi_datalog_size | `create loggingTag HmiTag_Int1_Log` on HmiTag_Int1, `set DataLog DataLog_1` | 1 logging tag, OnChange |
| 139_hmi_alarmlog | 138_hmi_logging_tag | `create alarmLog AlarmLog_1` | 1 alarm log |
| 140_hmi_logging_cyclic | 139_hmi_alarmlog | `set loggingTag LoggingMode Cyclic` | Cyclic (Cycle empty in Openness) |
| 141_hmi_datalog_period | 140_hmi_logging_cyclic | `set dataLog Settings.LogTimePeriod.Days 14` | 14 days |
| 142_hmi_alarmlog_size | 141_hmi_datalog_period | `set alarmLog AlarmLog_1 Settings.LogMaxSize 300` | LogMaxSize 300 |
| 143_hmi_logging_cycle | 142_hmi_alarmlog_size | `set loggingTag Cycle T10s` | cycle T10s |
| 144_hmi_datalog_backup | 143_hmi_logging_cycle | `set dataLog Backup.BackupMode PrimaryPath` | PrimaryPath |

## HMI Unified: runtime settings (Z1.3 U2)

Via MCP (2026-10-29, own instance PID 5000), chain starting at 102_hmi_tag_int. Ground truth as in 136 ff., plus `runtimeSetting_<scope>.json` (opcUa, maxLogin, processDiagnostics, reporting, telemetry).

| Sample | Predecessor | Change (MCP, `TiaHmiUnified set`, device HMI_1) | Openness after |
| --- | --- | --- | --- |
| 145_hmi_rt_gmp | 102_hmi_tag_int | `runtimeSetting` root `GMPEnabled` true | GMP on, audit trail “Audit trail_1” created |
| 146_hmi_audittrail_size | 145_hmi_rt_gmp | `auditTrail` “Audit trail_1” `Settings.LogMaxSize` 20000 (first attempt with 500 rejected by TIA, folder discarded and created again) | LogMaxSize 20000 |
| 147_hmi_rt_panning | 145_hmi_rt_gmp (copied from the discarded first 146 folder, content = 145; attrdiff against 145) | `runtimeSetting` root `CentralPanning` Enabled | CentralPanning Enabled |
| 148_hmi_rt_maxlogin | 147_hmi_rt_panning | `runtimeSetting` maxLogin `EnableLockAfterNumberOfAttempts` true | lockout on |
| 149_hmi_rt_maxlogin_errors | 148_hmi_rt_maxlogin | `runtimeSetting` maxLogin `MaxLoginErrors` 5 | 5 failed attempts |
| 150_hmi_rt_prodiag | 149_hmi_rt_maxlogin_errors | `runtimeSetting` processDiagnostics `EnableProcessDiagnostics` true | ProDiag on |
| 151_hmi_rt_opcua | 150_hmi_rt_prodiag | `runtimeSetting` opcUa `ActAsOPCServer` true | OPC UA server on |
| 152_hmi_rt_reporting | 151_hmi_rt_opcua | `runtimeSetting` reporting `IsReportingEnabled` true | reporting on |

## HMI Unified: languages and fonts (Z1.3 U3)

Via MCP (2026-10-29, own instance PID 7084), chain starting at 102_hmi_tag_int. Ground truth as in 145 ff., plus `projectLanguages.json` (`TiaProject languageGet`).

| Sample | Predecessor | Change (MCP) | Openness after |
| --- | --- | --- | --- |
| 153_hmi_lang_de | 102_hmi_tag_int | `TiaProject languageSet add=de-DE` | project languages en-US, de-DE; runtime language de-DE not activated |
| 154_hmi_lang_enable | 153_hmi_lang_de | `TiaHmiUnified set languageAndFont index=1 Enable true` (with `language=de-DE` NOT_FOUND, MCP-095; folder discarded and created again) | de-DE activated, order 1 |
| 155_hmi_lang_logging | 154_hmi_lang_enable | `TiaHmiUnified set languageAndFont index=1 EnableForLogging true` | de-DE for logging |

## HMI Unified: global script modules (Z1.3 U4)

Via MCP (2026-10-29, own instance PID 7084), chain starting at 102_hmi_tag_int. Ground truth as in 153 ff., plus `scriptModuleList.json` and the Openness export `scriptModules\Utils.hmi.js`/`.hmi.yml`.

| Sample | Predecessor | Change (MCP, `TiaHmiUnified create scriptModule`, device HMI_1) | Openness after |
| --- | --- | --- | --- |
| 156_hmi_script_module | 102_hmi_tag_int | name Utils, code `export function Add(a, b) { return a + b; }` | 1 module, 1 function |
| 157_hmi_script_globaldef | 156_hmi_script_module | replace true, globalDefinitions `const FACTOR = 10;`, functions Add and `Scale(x) { return x * FACTOR; }` | 1 module, 2 functions, global area |

## HMI Unified: text and graphic lists (Z1.3 U5)

Via MCP (2026-10-29, own instance PID 4748), chain starting at 102_hmi_tag_int. Ground truth as in 156 ff., plus the Openness exports `textLists\HmiTextLists.hmi.yml` (+ `.TextLibrary.hmi.yml`) and `graphicLists\HmiGraphicLists.hmi.yml`.

| Sample | Predecessor | Change (MCP, `TiaHmiUnified`, device HMI_1) | Openness after |
| --- | --- | --- | --- |
| 158_hmi_textlist | 102_hmi_tag_int | `create textList` TL_State, entries 0 “Stopped”, 1 “Running” | 1 user text list, 2 entries |
| 159_hmi_graphiclist | 158_hmi_textlist | `import graphicList` from a self-written `GL.hmi.yml` (`GraphicListContainers` / `DeviceGraphicList` / `ResourceListType: GraphicList` / `ResourceLists: GL_State: Entries: {}`); the first call failed because of the path specification in the shell call, folder discarded and created again | 1 graphic list without entries |

## HMI Unified: plant view, OPC UA alarm types, system tags (Z1.3 U6)

Via MCP (2026-10-29, own instance PID 4748), chain starting at 102_hmi_tag_int. Ground truth as in 158 ff., plus `plantViewNode.json`, `systemTags.json` (names), `connections.json`.

| Sample | Predecessor | Change (MCP, `TiaHmiUnified create`) | Openness after |
| --- | --- | --- | --- |
| 160_hmi_plantview | 102_hmi_tag_int | `plantView` PV_Plant | plant view without assignment, 0 nodes |
| 161_hmi_plantview_node | 160_hmi_plantview | `plantViewNode` Area1 | 1 node |
| 162_hmi_opcua_connection | 161_hmi_plantview_node | `connection` OPCUA_Conn, communicationDriver “OPC UA” | 2 connections |
| 163_hmi_opcua_alarmtype | 162_hmi_opcua_connection | `opcUaAlarmType` AT_Test, nodeId `ns=3;s=MyAlarmType`, connection OPCUA_Conn | 1 OPC UA alarm type (class Alarm) |

## HMI Unified: default values of the screen items (Z1.5 R1)

Via MCP (2026-10-30, own instance, script `z15defaults.py`). 164 is reserved for the template with Unified PC runtime by the user.

| Sample | Predecessor | Change (MCP, `TiaHmiUnified`, device HMI_1) | Openness after |
| --- | --- | --- | --- |
| 165_hmi_item_defaults | 110_hmi_screen | per type `create screen <type without Hmi>` and `itemCreate <type without Hmi>_1` without attributes (37 types from HmiText to HmiProcessDiagnosisOverviewControl) | 37 further screens with one object each; truth `screenItems\<screen>.json` = screen get + itemGet detail=full |
| 166_hmi_style_dark | 165_hmi_item_defaults | copy, style switched by hand by the user (2026-10-31, TIA user interface: HMI_1 > “Runtime settings” > “Screens” > “Style” = FlatStyle_Dark, save; Openness V21 cannot set the style) | SelectedStyle FlatStyle_DarkV2101; truth `screens_full.json` = screen get detail=full (38 screens) |
| 167_hmi_style_bright | 165_hmi_item_defaults | like 166 with style FlatStyle_Bright | SelectedStyle FlatStyle_BrightV2101; truth `screens_full.json` = screen get detail=full |

## HMI Unified: template with WinCC Unified PC RT (Z1.5 R0)

| Sample | Predecessor | Change | Openness after |
| --- | --- | --- | --- |
| 164_hmi_pc_base | 110_hmi_screen | **by hand by the user** (2026-10-30, TIA V21, in `<temp>\Z15\work`): PC station `PC-System_1` with “SIMATIC WinCC Unified PC RT” V21 (software `HMI_RT_1`, screen Screen_1), because Openness cannot create the PC runtime (MCP-098); archive `<temp>\Z15\164_hmi_pc_base.zap21` taken over as a copy (SHA-256 equal), folder via `TiaProject(retrieve)` in an own instance, closed without saving; folder and archive yield the same parser output | 3 devices: PLC_1, HMI_1, PC-System_1 (HMI_RT_1) |

## PLC: STL (Z1.2 C1)

Via MCP (2026-11-06, TIA V21 Update 3, own instance PID 15412), working folder <temp>\C1\work. Ground truth `<sample>_truth\PLC_1\blocks\`: SimaticML export (`*.xml`) and STL source export (`*.awl`, `sourceExport`).

| Sample | Predecessor | Change (MCP) | Content |
| --- | --- | --- | --- |
| 061_fc_stl | 029_fb_idb_value | `TiaPlcSoftware(sourceImport, tools\sources\stl\FC_Stl.awl)`, compile 0/0 | FC1 `FC_Stl`, STL, 2 networks: (1) title and comment, `A #a` with line comment, `AN`, `=`; (2) `L`/`T`, `+I`, `>I`, empty line, `JC M001` with label |
| 062_fb_stl | 061_fc_stl | `sourceImport tools\sources\stl\FB_Stl.awl`, compile 0/0 | FB3 `FB_Stl`, STL: `A(`/`O`/`)`, `FP`, `JCN END1`, `+ 1`, `SET`, `SAVE`; Static cnt, edge |
| 064_stl_all | 062_fb_stl | `python tools\gen_stl.py`, `sourceImport tools\sources\stl_all` (without FC_StlNoPar.awl, which came as a standard FC via single import and whose `ParameterPassing` was set to true via SimaticML export/import), compile 0/0, 2026-11-07, instance PID 1888, working folder <temp>\C3\work | 157 STL networks (one per catalog mnemonic) in FB_StlBit/Timer/Math/Load/Jump/Db/Call/Be, plus FC_StlNoPar, FC_StlPar, FB_StlInner, DB_StlInner, DB_StlStd (standard DB); TIA creates tags `Tag__<number>` for absolute addresses. Truth `064_stl_all_truth\PLC_1\blocks` (SimaticML + .awl/.db) |
| 073_code_stress | 064_stl_all | `python tools\gen_stress.py`; `documentWrite` FB_Mixed, FB_BigLad, FC_BigFbd (.s7dcl/.s7res); `sourceImport` FB_BigStl.awl, FB_BigScl.scl; FB_MixedStl via `python tools\make_mixed_stl.py` from the SimaticML exports of FB_Mixed and FB_BigStl and `import`; compile 0/0, 2026-11-07, instance PID 11352, working folder <temp>\C4\work | Stress: LAD 300, FBD 300 (FC), STL 600, SCL 3024 lines, mixed 120 (LAD/FBD/SCL) and 160 (+STL) networks; 785 interface tags per FB. Truth `073_code_stress_truth\PLC_1\blocks`: SimaticML of all 6, SD (.s7dcl/.s7res) for FB_BigLad, FC_BigFbd, FB_Mixed, FB_BigScl, sources FB_BigStl.awl, FB_BigScl.scl |
| 074_fb_graph | 064_stl_all | `import` of `tools\sources\graph\FB_Graph.xml` (hand-written SimaticML, Graph v6), compile 0 errors/3 warnings (steps without actions), 2026-11-07, instance PID 13720, working folder <temp>\Z12\work< | FB74 `FB_Graph` in GRAPH: steps Step1 (Init) to Step3 with T_MAX T#10S, T_WARN T#7S, each with supervision (contact #sv on SvCoil) and interlock (IlCoil on the power rail), transitions T1 to T3 (contact #go/#a/#b on TrCoil), T3 jumps to Step1. Actions could not be created via import (operand missing). Truth `074_fb_graph_truth\PLC_1\blocks\FB_Graph.xml` |
| 075_stl_calls | 064_stl_all | `python -I tools\gen_stl_calls.py` (one FB per system instruction from the interface and wiring of the LAD/FBD block of the same instruction) and `python -I tools\gen_stl_calls2.py` (38 hand-written calls), `sourceImport tools\sources\stl_calls`, `stl_calls2` and `stl_calls3` (BLKMOV/UBLKMOV, VARIANT_TO_DB_ANY/DB_ANY_TO_VARIANT, ReadFromArrayDB/WriteToArrayDB, MOVE_BLK_VARIANT), compile 0/0, 2026-11-07, instance PID 13720, working folder <temp>\Z12\work\075; extension 2026-11-08 (working folder <temp>\Z12\work\080): notation determined via VNC in the STL editor, then `sourceImport tools\sources\stl_calls4` (PRESET_TIMER, CompType, EQ/NE_TypeOfDB, ReadFromArrayDBL/WriteToArrayDBL, DRUM as multi-instance), compile 0/0 | 70 FBs `FB_StlC_<catalog no.>` (73 catalog pages) plus `DB_Arr` and array DB `ArrDB`, each with one STL `CALL` of a system instruction (timers, counters, MIN/MAX/LIMIT, Serialize, SCATTER/GATHER, SCALE/UNSCALE, ENDIS_PW, INIT_RD, WAIT, SEL, DCAT/MCAT/IMC/SMC, LEAD_LAG, BITSUM, R_TRIG/F_TRIG, MOVE, MOVE_BLK, UMOVE_BLK, FILL_BLK, UFILL_BLK, SCATTER_BLK, GATHER_BLK, SCALE_X, NORM_X, GET_ERROR, GET_ERR_ID, RUNTIME, DECO, ENCO, BCDCPL, RESET_TIMER, EQ/NE_Type, EQ/NE_ElemType, IS_NULL, NOT_NULL, IS_ARRAY, PEEK/POKE (5), READ/WRITE_LITTLE/BIG, VariantGet/Put, CountOfElements, LOWER/UPPER_BOUND, FILL) with type template `{value_type := 'Int'}`; since 2026-11-08 77 FBs (all 80 catalog pages with a parameter list). Truth `075_stl_calls_truth\PLC_1\blocks`: .awl and SimaticML |
| 076_graph_actions | 074_fb_graph | by hand in TIA (VNC, Claude): FB_Graph in the GRAPH editor, single step view, actions Step1 `N #q2`, Step2 `S #q3`, Step3 `R #q3` (operand via clipboard, because `#` arrives incorrectly via VNC), compile 0/0, 2026-11-07, instance PID 13720 | actions with content; truth `076_graph_actions_truth\PLC_1\blocks\FB_Graph.xml` |
| 168_hmi_graphiclist_entry | 159_hmi_graphiclist | `graphicImport` Gfx_Red/Gfx_Green (own 16x16 PNG), `import graphicList` (two entries, graphic reference via YAML not taken over), then by hand in TIA (VNC, Claude) the project graphic selected per entry, 2026-11-07 | GL_State: value 0 Gfx_Red, value 1 Gfx_Green; truth `168_hmi_graphiclist_entry_truth\HMI_1\HmiGraphicLists.hmi.yml` (`Graphic: GraphicLibrary.<Name>`) |
| 254_hw_channel_param | 205_hw_di_module | `TiaDeviceItem(channelSet)` DI_1/DI_1 channel 0 `HardwareInterruptRisingEdgeActive` true, 2026-11-07 | hardware interrupt rising edge channel 0; truth `channels.json` (channelGet) |
| 255_hw_channel_param2 | 254_hw_channel_param | `channelSet` channel 5 `HardwareInterruptFallingEdgeActive` true, channel 17 `HardwareInterruptRisingEdgeActive` true | channels 0/17 rising, 5 falling; truth `channels.json` |
| L37_hmi_lib_types | 101_hmi_base | by hand in TIA (VNC, Claude): Project library > Types > Add new type: script module type “Script module type_1” (V0.0.1 in work), dynamic SVG “DynamicSVG type_1” (default graphic, V0.0.1 released), 2026-11-07 | HMI library types ScriptModuleLib and DynamicSVGLib; truth `typesList.json` |
| L38_hmi_lib_type_content | L37_hmi_lib_types | via VNC in the script module type “Script module type_1” V0.0.1 (in work), inserted `export const OPD_TEST = 42;` in the global definition area, 2026-11-08, working folder <temp>\Z12\work\L37b | content of the HMI type versions; truth `content.json` (code as entered, SVG file with size and SHA-256 of the default graphic from the TIA installation) |
| L39_hmi_lib_textlist_type | L38_hmi_lib_type_content | via VNC: Project library > Types > Add new type > “Text list / Graphics list”, HMI text list “Text list type_1”; in the editor entry 0–1 text “Motor an”, 2026-11-08 | text list type with content; truth `typesList.json` (Openness) and `textlist.json` (as entered) |
| L40_hmi_lib_graphiclist_type | L39_hmi_lib_textlist_type | via VNC graphic type “Graphic type_1” (default graphic), via MCP `typeVersionRelease`, then graphic list type “Graphic list type_1” with entry 0–1 = Graphic type_1 V0.0.1, 2026-11-08, working folder <temp>\Z12\work\L40b | graphic list type with graphic type; truth `typesList.json`, `graphiclist.json` |
| 077_graph_branches | 076_graph_actions | `import` of `<temp>\Z12\src\FB_GraphBranch.xml` and `FB_GraphSim.xml` (derived from the export of 076, actions as tokens), compile 0/0, 2026-11-07 | FB75 `FB_GraphBranch`: alternative branch after Step1 (T1→Step2→T2 / T4→Step3→T3), merge to Step4; FB76 `FB_GraphSim`: simultaneous branch after T1 (Step2 / Step3), permanent pre-instruction (contact #a on coil #q3); truth SimaticML of all three GRAPH FBs |
| 078_plc_force_entry | 048_plc_watch_entry | `TiaPlcSoftware(entryAdd, watchTable)` on “Force table”: `%Q0.0` Bool, ForceValue TRUE, 2026-11-07 | entry in the force table; truth `forceTable.json` (return of entryAdd) |
| 169_hmi_start_screen | 114_hmi_screen2 | `TiaHmiUnified(set, runtimeSetting)` root StartScreen = Screen_2, 2026-11-07 | start screen; truth `runtimeSetting.json` |
| 170_hmi_plant_object_type | 161_hmi_plantview_node | via VNC plant object type “Mixer” (Plant objects → Show plant object types → new type), then `TiaHmiUnified(create, plantViewNode, Mixer1, parentPath Area1, plantObjectType Mixer)`, 2026-11-08, working folder <temp>\Z12\work\170 | plant object with type; truth `plantView.json` (list/get plantViewNode) |
| 171_hmi_thresholds | 164_hmi_pc_base | on PC-System_1/Screen_1 via MCP `itemCreate` HmiBar “Bar_T” (40/40, 120×300) and HmiGauge “Gauge_T” (240/40, 300×300), `partSet Thresholds/#n Enable true` (bar: Maximum, Upper; gauge: all five), gauge `CurvedScale.EndValue` 100, 2026-11-08, working folder <temp>\Z12\work\z15thr | thresholds; truth `thresholds.json` (Openness partGet) and editor image `docs/img/z15/171_thresholds_editor.jpg` |
| 172_hmi_bar_segmented | 171_hmi_thresholds | via MCP `itemSet`: Bar_T BarMode Segmented, ProcessValue 30; Gauge_T BarMode SegmentedStatic, ProcessValue 55; saved via `TiaProject archive`, 2026-11-08, working folder <temp>\Z12\work\z15thr | static numeric ProcessValue (code 5) and segmented bar modes; truth `items.json`, runtime image `docs/img/z15/171_segmented_runtime.png` |
| 173_hmi_trend_control | 164_hmi_pc_base | on HMI_1/Screen_1 via MCP `itemCreate` HmiTrendControl “Trend_1” (10/10, 380×300) and HmiFunctionTrendControl “FTrend_1” (410/10, 380×300), all other values default; created on HMI_1 because creating items on PC-System_1 needs the “WinCC Unified PC ES” license, which was not available; saved via `TiaProject archive`, TIA V21, 2026-11-20, working folder <temp>\Z15T\work | trend and function trend control with default trend areas, axes and trends (not stored in the file); truth `trend_parts.json` (Openness itemGet and partGet of the trend areas, axes and trend) |
| 079_plc_to_hw_connect | 053_plc_to_inverse | `parameterSet` TO_Speed_5_0 Actor.Type 0 (analog), `tagCreate` AxisOut Int %QW100, `hardwareConnect` plcTag AxisOut, 2026-11-07 | TO hardware connection; truth `hardwareConnection.json` |
| 080_graph_events | 077_graph_branches | `import` of `tools\sources\graph\FB_GraphEvt.xml` (FB_GraphBranch with action events S1/S0/V1 and `Interlock="true"`), compile 0/0, 2026-11-08, working folder <temp>\Z12\work\080b | FB_GraphEvt; truth `080_graph_events_truth\PLC_1\FB_GraphEvt.xml` |
| 081_plc_opcua_nodes | 054_plc_opcua_interface | `opcUaInterfaceImport` of `si_nodes_import.xml` (object Machine, variables Speed Int16 RD/WR and Running Boolean RD) into SI_Test, global DB `DB_Ua` via `sourceImport`, then via VNC in the interface editor “Generate local data” → new DB `SI_Test_Data`, compile 0/0, 2026-11-08, working folder <temp>\Z12\work\081 | server interface with nodes and variable mapping; truth `SI_Test.xml` (opcUaInterfaceExport) and the imported nodeset |
| 082_plc_opcua_refns | 081_plc_opcua_nodes | `opcUaInterfaceCreate(interfaceKind reference, name RN_Types, sourcePath ref_types_import.xml)`, 2026-11-08, working folder <temp>\Z12\work\082 | reference namespace; truth `RN_Types.xml` (opcUaInterfaceExport), `opcUaInterfaceList.json`, imported nodeset |

## Cleanup for publication (2026-11-08)

The HMI editor cache `IM/HMI/**/Saved/` was removed from all archives and project folders (it contained, among other
things, the Siemens font SISAN03). All other files of the archives are byte-identical; the parser does not read the cache.
The extracted project folders of the samples that exist as `.zap21` were deleted.

## Rebuild 2026-11-09 (owner rename)

The Windows user that created the samples was renamed from the previous owner name to `factorybyte`. TIA stores the
Windows user in every project and global library (`System\PEData.plf`: author / LastModifiedBy), so all samples,
archives, project folders and `*_truth\` folders were recreated once with `tools\rebuild\` (TIA V21, stage folder
`<stage>`). This is the only deliberate exception to the rule above. The tables in this file still describe how
each sample was made; the rebuild replays them with the recipes in `tools\rebuild\recipes_*.py`
(core, plc, hw, hmi, lib, fp). The UI steps are listed in `MANUAL` of each recipe module.

Deviations from the descriptions above:

- 030–040: rebuilt with `rhelp.transfer` from inventories of the old archives (`tools\rebuild\old_inventory.py`)
  instead of the original generator runs; block numbers kept.
- 068: technology objects reference `TO_*Axis` / `TO_ExternalEncoder` instead of `TO_*_2` (all parameters equal).
- 218/219: `connectionCreate` with `deviceItemPath=PLC_1`. 222: extra `setName` of the master system.
- 230–233: made with MCP `setDetails` instead of the UI.
- 168: graphic references taken over from the YAML import, no UI step.
- L13/L35: `newName` left out so TIA chooses the original names.
- F03: languages added one at a time after a reopen (otherwise the hidden faceplate devices get a different
  OnlineLanguageOrder). F32: "Activate encrypted transfer" unchecked in the UI.
- Truth files that hold hand-written notes or editor screenshots were copied unchanged.
- `*_diff.txt` / `*_attrdiff.txt` were not regenerated; they describe the original archives.

Remaining harmless truth differences are newer TiaMcpXE output fields, ordering, random text/GUID ids and
`LoadMemoryLength` of compiled blocks (varies by a few bytes between runs).

### Rebuilt samples with open points (2026-11-10)

The samples below are marked because they differ measurably from the original archives, or because a cause is only
partly proven. All parser tests give the same result as with the original samples. 059, 060, 063, 066 and L37–L40
fail with the same errors as before (parser gaps that are older than the rebuild). The original archives are kept
outside the repository, so the comparison can be repeated.

| Sample | Mark | What differs | What may still be a problem |
| --- | --- | --- | --- |
| 025_fb_idb, 031_lad_all, 036_tech, 039_fw41, 040_s71200 | ⚠ load memory | Load memory of single blocks differs from the original by a few bytes (031: FB row +412 bytes over 120 FBs, 039: FB row +158 bytes, DB_Arr 1385 instead of 1370). `test_proginfo` expects the values of TIA "Program info" of the **rebuilt** archives (measured 2026-11-10); the screenshots of 2026-10-31 belong to the original archives. | TIA compiles a block to a slightly different size each time. Proven for DB_Arr and DB_Std: the same source written and compiled repeatedly gave 1362–1366 / 1127–1129 bytes; the original 025 imported three times gave FB_Iface 5718/5712/5713 bytes. **Not proven per block:** blocks written from SD are on average 2–3 bytes larger than the original. Candidate cause: the original blocks carry a later recompile history (`IMakeData.Level` = `Interface`, `PreferablyRenumbered` = true, e.g. FB_Lad_013 `IdentESLength` 385 instead of 388), and the rebuild does not repeat that history. If this matters, a test that compares absolute block sizes with the original fails. |
| 030_scl_all, 031_lad_all, 032_fbd_all, 034_flg_misc, 035_scl_rest, 036_tech, 037_lad_xml, 052_plc_to_param, 053_plc_to_inverse, 079_plc_to_hw_connect | ⚠ build route | Content taken over from inventories of the old archives (`rhelp.transfer`) instead of the original generator runs. DB_Std and DB_Arr are written from `tools\sources\scl` as in the original (a SimaticML import made them 21 and 14 bytes smaller). | Creation order, creation times and compile history of the blocks are not those of the original. Anything that depends on that order (for example object order in the plf, CreationId) may differ from the original. |
| 031_lad_all, 032_fbd_all | ⚠ block numbers | The system blocks DCAT, MCAT, IMC, SMC and LEAD_LAG have other numbers than in the original. | TIA assigns these numbers itself, and Openness cannot set them for system blocks. A check against fixed numbers of these blocks would fail. |
| 034_flg_misc, 037_lad_xml, 039_fw41 | ℹ text ids | SD truth: only the text ids (`MLC_…`) of network titles and comments differ (4 / 68 / 134 files); text and code are equal. | Nothing, as long as no test compares the text ids of the old truth. The ids are random per write. |
| 039_fw41 | ⚠ load memory | Main 2411 instead of 2414 bytes, DB_Arr 1385 instead of 1370 bytes (+15). | +15 bytes is more than the spread measured in 031 (1362–1375 bytes). Its cause is not proven separately. |
| 230–242 (039 chain) | ℹ rebuilt | Rebuilt because they derive from 039. The truth equals the original except `validUntil` of the certificates (offset +01:00 instead of +02:00) in 237–242. | The certificates were created in November (winter time) instead of October. Expiry dates move with the build date, so a rebuild at another time gives other dates. `*_attrdiff.txt` still describes the original archives. |
| 052_plc_to_param, 053_plc_to_inverse | ℹ truth fields | `blocks.json` and `list_*.json` contain newer TiaMcpXE fields. `*_attrdiff.txt` was not regenerated. | `*_attrdiff.txt` describes the original archives, not the rebuilt ones. |
| 068_motion_tos, 069_safety_compiled, F02_hmi_faceplates_crash, L13_mc_instantiate, L35_device_from_mc | ⚠ content | See the deviations above: TO names `TO_*Axis`; 069 based on the old 249 folder; F02 without the crash session file `000_empty.info`; L13/L35 without `newName`. | Tests pass, but the content is not identical to the original. |

## TIA Portal V19 and V20 (docs\PLAN-V19-V20.md)

Created 2026-11-21 via TiaMcpXE on TIA Portal V19 and V20 instances (installed next to V21), working folders `<temp>\Z19\work`, `<temp>\Z20\work`, each sample an archive of the same working project after the step. CPU 1513-1 PN `6ES7 513-1AL02-0AB0/V2.9`, security settings left at their defaults (only the PLC software is compiled). Classic sources in `tools\sources\classic` (V19 has no SIMATIC SD); LAD/FBD in V20 via `documentWrite` of `tools\sources\FC_Lad.s7dcl`/`FC_Fbd.s7dcl`, in V19 via SimaticML import of the V20 export with `<Engineering version="V19" />` (TIA refuses files of a newer version otherwise).

| Sample | Predecessor | Change (MCP) | Content / truth |
| --- | --- | --- | --- |
| v20_000_empty | – | `TiaProject(create)` | empty V20 project |
| v19_002_cpu, v20_002_cpu | empty project | `TiaDevice(create, PLC_1, OrderNumber:6ES7 513-1AL02-0AB0/V2.9)` | CPU as in 002_cpu |
| v19_001_basic, v20_001_basic | 002 of the version | `create/tagTable TagTable_1`, `tagCreate Tag_Bool_1 Bool %M0.0`, `sourceImport FB_Empty.scl` | as 003_tagtable + 004_fb |
| v19_003_plc, v20_003_plc | 001 of the version | `sourceImport` of `classic\UDT_Motor.udt`, `DB_Global.db`, `FB_FC_Iface.scl`, compile 0/0 | UDT_Motor, DB_Global, FB_Iface, FC_Iface as 017/018/023/024; truth SimaticML of blocks, types and tag tables |
| v19_004_plc_code, v20_004_plc_code | 003 of the version | FC_Lad and FC_Fbd (see above), `sourceImport` of `tools\sources\stl\FC_Stl.awl` and `FB_Stl.awl`, compile 0/0 | LAD, FBD and STL as 027/028/061; truth SimaticML of all blocks |
| v19_005_plc_more, v20_005_plc_more | 004 of the version | `sourceImport FB_Alarm.scl` (`Alarm_1 : Program_Alarm`, call `#Alarm_1(SIG := #Sig)`), `create/block FB_Alarm_DB` and `FB_Iface_DB` (InstanceDB), `create/watchTable WT_Test` + `entryAdd "Tag_Bool_1"` Bool TRUE, `unitCreate Unit_A`, `Unit_B`, `unitRelationCreate Unit_A -> Unit_B`, compile 0/0 | as 041/042/025/047/048/049-051; truth SimaticML of all blocks and the watch table |
| v19_006_plc_texts, v20_006_plc_texts | 005 of the version | `alarmTextWrite FB_Alarm_DB/Alarm_1` (alarm text "Pump overload", info text "Check the motor protection", en-US), `alarmTextListWrite TL_Test` (Decimal, 0 = "Off", 1 = "On") | as 043/045/046; texts checked in the test OlderVersionSamples |
| v19_007_plc_tech, v20_007_plc_tech | 006 of the version | `create/technologyObject TO_Speed TO_SpeedAxis 6.0` (7.0 not offered) + `parameterSet DynamicDefaults.Velocity 750`, `opcUaInterfaceCreate server SI_Test`, `create/block ProDiag_FB FB ProDiag`; not compiled (TO without drive, empty server interface, as in V21) | as 036/052/054/056; `supervisionWrite` fails on V20 (export sheet without column Name), so no supervision |
| v19_008_plc_force, v20_008_plc_force | 007 of the version | `entryAdd` on "Force table": `%Q0.0` Bool, ForceValue TRUE (after a reboot of the machine the working projects were opened again; 2026-11-21) | as 078 |
| v19_101_hmi_basic, v20_101_hmi_basic | 008 of the version | `TiaDevice(create, HMI_1, OrderNumber:6AV2 128-3GB06-0AXx/19.0.0.2` resp. `/20.0.0.0)` (MTP700 Unified Comfort, no HMI connection), `create/screen Screen_1`, `itemCreate` HmiButton Button_1 (20/20, 160x50, Text "Start"), HmiIOField IOField_1 (200/20, 120x40), HmiRectangle Rect_1 (20/100, 200x100, BackColor 0xFF3366CC), HmiText Text_1 (240/100, 200x30, "Hello V19"/"Hello V20"), HmiBar Bar_1 (460/20, 80x250), HMI tag HmiTag_Int Int bound to ProcessValue of IOField_1, discrete alarm Alarm_Disc_1 (RaisedStateTag HmiTag_Int, "Tank level high") | as 101/110/U5; values checked in the test OlderVersionSamples, the viewer draws the screen |
| v19_102_hmi_controls, v20_102_hmi_controls | 101 of the version | `itemCreate` HmiAlarmControl AlarmControl_1 (20/290, 420x180), HmiGauge Gauge_1 (560/20, 200x200), HmiTrendControl Trend_1 (460/240, 330x230); `create/dataLog DataLog_1`, `create/loggingTag HmiTag_Int_Log` of HmiTag_Int, `create/scriptModule Module_1` (`export function Add(a, b)`). An HMI text list could not be created (V19: HmiTextLists not in Openness V19; V20: internal error of the tool) | as U5/Z1.3; values checked in the test OlderVersionSamples |
| v19_201_lib_project, v20_201_lib_project | 102 of the version | `TiaProjectLibrary(masterCopyCreate)` of FB_Iface, UDT_Motor (PlcType) and TagTable_1 (PlcTagTable); V20 also `documentExport FC_Iface` + `typeCreateFromDocument` (replaceExisting) + `typeVersionRelease 1.0.0` (V19 has no SIMATIC SD, so no type) | as L-samples; checked in the test OlderVersionSamples |
| v19_202_global_lib, v20_202_global_lib | – | `TiaLibrary(create)` (.al19/.al20), `TiaLibraryContent(masterCopyCreate)` of UDT_Motor from the 201 project; V20 also type FC_Iface 1.0.0 copied from the project library with `TiaLibraryContent(update, sourceScope project, targetScope global)` (Openness cannot create types from documents in a global library); save, archive .zal19/.zal20 | as L06; checked in the test OlderVersionSamples |
| v19_301_hw_network, v20_301_hw_network | 201 of the version | `TiaDevice(create, IO_1, 6ES7 155-6AU02-0BN0)` (V20: V6.2; V19: V6.1, V6.2 not in the V19 catalog), `nodeCreateAndConnect` PLC_1 X1 → PN/IE_1, IO system `PROFINET IO-System` on PLC_1, `nodeConnect` IO_1 → PN/IE_1, `ioConnect` IO_1 → PLC_1. The integrated HMI connection needs Openness V21 (CommunicationManagement), so HMI_1 stays without connection | as H4; checked in the test OlderVersionSamples |
| v19_103_hmi_style_dark, v20_103_hmi_style_dark | 301 of the version | via VNC in the runtime settings of HMI_1: Style / Selected style "Dark style" (Openness has no attribute for it) | selected style FlatStyle_DarkV1902 (V19) / FlatStyle_DarkV2000 (V20); the viewer draws the screen in the dark style |
| v19_009_plc_prodiag, v20_009_plc_prodiag | 103 of the version (PLC part from 008) | `supervisionExport`, one row added with `tools\New-ProDiagSupervisionXlsx.ps1` (ProDiag_FB, ID 1, "Tag_Bool_1", operand supervision O, category 1, text "Test supervision"; V19/V20 sheets have no Name/Label columns, so `supervisionWrite` of the tool fails), `supervisionImport` | as 057; checked in the test OlderVersionSamples |
| v19_104_hmi_faceplate, v20_104_hmi_faceplate | 009 of the version | via VNC: project library, Types, Add new type, Faceplate for Unified Panels (lowest device version 19.0.0.2 / 20.0.0.2), name FPBasic; V19: a rectangle Rectangle_1 placed in the faceplate and the version released in the editor (Openness V19 has no Release); V20: released empty with `typeVersionRelease`. Then `itemCreate` HmiFaceplateContainer Faceplate_1 (250/150, 180x120, ContainedType `V0.0.1\FPBasic`) | as F05; checked in the test OlderVersionSamples |
| v19_105_hmi_conn_textlist, v20_105_hmi_conn_textlist | 104 of the version | HMI_1 X1 connected to PN/IE_1 with 192.168.0.3 (Openness); via VNC in the network view, Connections mode, PLC_1 context menu "Add new connection", type HMI connection, partner HMI_1 → integrated HMI_Connection_1; HMI text list via the editor "Text and graphic lists" (V19: new list TLHmi, V20: TL_Hmi left by the failed Openness call), entries 0 "Stopped", 1 "Running" | as 100 (connection) and Z1.3 U5 (text list); checked in the test OlderVersionSamples |
| v19_203_lib_type | v19_105 | via VNC: PLC data type UDT_Motor copied (Ctrl+C) and pasted into the project library Types (Ctrl+V), dialog "Add type" confirmed → type UDT_Motor 0.0.1 committed (V19 has no SIMATIC SD for typeCreateFromDocument) | as L01/L02; checked in the test OlderVersionSamples |
