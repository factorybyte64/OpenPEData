# Data blocks and UDTs (established, 5b, commit 5e9f55d)

Evidence: samples 017_udt, 018_db_global, 019_db_instance, 020_block_group, 022_db_udt (MANIFEST; SD sources in tools\sources). Module `parser\openpedata\interface.py`. Tests: all samples OK (folder + .zap21), 022 with 109 checks, against SimaticML and SD text (`parser\tests\RESULTS.md`).

## Interface
XML in the object InterfaceVersionRootData (0x0022160c): `<Member ID Name RID Type SubPartIndex>`.
- **RID → type name:** via the data type objects of the project (DataTypeItemData in scope S71500). Evidenced: Bool, Word, Int, DInt, Real.
- **Struct elements and used UDTs:** via SubPartIndex in the link list UsedParts (own sub-objects).

## Start values
Separate "Values" XML, entries `<V p="55:52" v="-5"/>`, path = sequence of member IDs. Array elements individually.

## Comments
Format `expando="simple"` (ICommentsExpandoAttributeSet), now decoded (part of H-019). In the process two errors in the expando code were fixed: type code 4 = inline value; expando blobs have a varint prefix.

## Parser output
DBs as GlobalDB/InstanceDB (instance DB with "of <FB>"), PLC data types; per tag name, data type, start value (incl. array elements), comment per language, sub-elements of Struct/UDT.

## All symbol variants (sample 059_db_symbols, 2026-11-05)
Checked against the SimaticML export: all 12 DBs and 3 UDTs, every tag with data type, start value and array element start values (test `DbSymbolsAgainstSimaticML`). Findings:
- **Elementary types:** all 30 (Bool to LDT, DTL, Char/WChar, String[n], WString[n], S5Time, LTOD) via DataTypeID. `Variant` (63) exists only in scope S71200; missing IDs are therefore supplemented from S71200/S71500.
- **Anonymous structs** have RID 0x92… in the DB, 0x91… in the UDT and 0x93… in the FB, all without `Type` → `Struct`.
- **Sections vs. UDT elements:** the section members of a code block root have the fixed IDs 2..8, tags start at 51. A UDT element named `Temp`, `Input`, `Static` … is therefore not a section (previously its siblings were missing, e.g. `DB_Multiplexing.MotorsComplex` in the Siemens demo project).
- **Start values below array elements:** value path `56[2]:51` (aUdt[2].Temp) or `55:57[1]:51[2]` (data.innerArr[1].Arr[2]). As in Openness (`Subelement Path`) they are on the leaf member, key = all indices separated by commas (`"1,2"`).
- **ARRAY DB** (`Type=ArrayDB`, `OfType=Undef`): root with the hidden system member `Header` (type `ArrayDBHeader`, MFlags 0x1008, `S7_ReadOnly`) and the array `THIS`. Openness shows only the array under the DB name; so does the parser (type `ArrayDB`). The system data type `ArrayDBHeader` appears, like IEC_TIMER or DTL, as a block of type `SDT` in the folder SystemDataTypeFolder.
- **DB of the type of a UDT** (`Type=DBOfType`, `OfType=UDT`): like an instance DB, own root `Kind=Values` with the changed start values, `UsedParts` → UDT root, relation `GeneratedFromMaster` → UDT. Parser type `TypedDB`, `instanceOf` = UDT name, `startValue` own value, `defaultValue` from the UDT.
- **Multi-instances** (TON_TIME, CTU_INT, R_TRIG, own FBs, arrays of them, two levels) and non-optimized DBs are read completely.
- The parser shows the elements of DTL, SimaticML does not.

## Tag attributes and offsets (sample 060_db_attrs, 2026-11-06)
Checked against `TiaPlcSoftware(memberGet)` (1282 values, test `DbMemberAttributesAgainstOpenness`):
- **MFlags** on the `<Member>` (missing = 15): bit 0 accessible from HMI/OPC UA/web server, bit 1 visible, bit 6 write-protected from outside, bit 9 setpoint; bits 0..2 all 0 = hidden system member. As in Openness: not accessible → not visible and not writable. Examples: 14 = not accessible (memberSet), 76 = not accessible (SD `S7_Visibility := "Hidden := External"`), 13 = not visible (`Unlisted`), 79 = write-protected (`S7_Access := "ReadOnly := External"`), 527 = setpoint (`S7_Setpoint`).
- **Setpoint** is reported by Openness only in DBs and in the Static section; members under Input/Output/InOut (also in multi-instances) are never setpoints. For members of type UDT, FB or system type Openness derives the value (true, for DTL false); it is not in the file and is not compared.
- **Retentivity:** optimized `Remanence="Retain"` on the member, applies to all elements below it (also FB `VAR RETAIN` in instance DBs and multi-instances). Non-optimized, the whole DB is retentive: `IExpandoInformationData.NonRetain` = False.
- **Optimized** = `IGeneralBlockSourceData.OnlySymbolicAccess`. For non-optimized blocks `StdO` is the offset in bits, relative to the enclosing structure or to the UDT; the parser outputs `offset` as "Byte.Bit" (DB_NonOpt matches the S7 standard layout, size 82 bytes = `stdSize` 656 bits).
- Parser output per tag: `retain`, `externalAccessible`, `externalVisible`, `externalWritable`, `setpoint`, for non-optimized access `offset`; per DB `optimized`.

## Limits
- Uncompiled UDTs/instance DBs: no SimaticML export possible → ground truth = SD text.
- Single-instance DB of an IEC timer: see below (sample 065).

## Single-instance DB of an IEC timer (sample 065_db_iec_timer, 2026-11-07)
Openness can neither create it (create, SD, SimaticML import) nor list it (not even with system blocks). Created in the TIA editor ("Add new block" → data block → type IEC_TIMER), truth = editor view (`065_db_iec_timer_truth`).
- `DataBlockData` with `IGeneralDataBlockSourceData.Type=IDBofSDT`, `OfType=SDT`, `OfName=IEC_TIMER`, `OfNumber=31`; `IExpandoInformationData.IsSystem=True`; folder `ProgramResourcesFolder` under `SystemBlocksFolder`.
- `GeneratedFromMaster` → `SystemDatatypeData` IEC_TIMER (without `CurrentInterface`); its own root (`Kind=Values`, e.g. `<V p="52" v="T#2S"/>`) points via `UsedParts` to the interface root `_BIVE:IEC_TIMER/1.0.8/…`.
- Parser: type `TypedDB`, `instanceOf` IEC_TIMER, members PT, ET, IN, Q (ST and RU hidden, MFlags 4112), ET and Q write-protected.
- Without usage TIA deletes the DB from the program resources on the next compile; in the sample `FC_Ton` uses it (`"DBTon".TON(IN := #start, PT := T#2S)`).

## Instance DBs of system FBs (sample 066_sys_fb_instances, 2026-11-07)
Checked against SimaticML (45 DBs, 8187 values together with 059/060) and memberGet (38 DBs, 9322 values together with 060): 64 system FBs as multi-instance in `FB_SysMulti` (IEC timers/counters of all types, R_TRIG/F_TRIG, TSEND_C, TRCV_C, TCON, TSEND, TRCV, TUSEND, TURCV, TMAIL_C, T_CONFIG, GET, PUT, USEND, URCV, BSEND, BRCV, MB_CLIENT, MB_SERVER, RDREC, WRREC, RALRM, GETIO, SETIO, GETIO_PART, SETIO_PART, Program_Alarm, DRUM, DCAT, MCAT, IMC, SMC, LEAD_LAG, PID_Compact, PID_3Step, PID_Temp, CONT_C, CONT_S, PULSEGEN, TCONT_CP, TCONT_S), single-instance DBs of 29 system FBs (used in `FC_SysUse`), 10 IEC timer/counter DBs (IDBofSDT, used in `FC_IecUse`) and 7 technology DBs.
- **Interface of system and library FBs:** relation `CurrentLibraryInterface` instead of `CurrentInterface` (previously e.g. the PID, HSC and F instance DBs were empty).
- **Typed link lists:** long link lists (e.g. `UsedParts` of an FB with many multi-instances) are stored in their own slot without relation ID (link12). The relation follows from the opposite side: the target references back tagged with the inverse relation (`UsedByVersions`). `Project.Links` now evaluates this for all relations.
- **Types of the single-instance DBs:** `IDBofFB` (e.g. TSEND_C, MB_CLIENT, DRUM), `IDBofSFB` (GETIO, GET_SFB, RDREC_SFB …), `IDBofSDT` (IEC_TIMER, IEC_COUNTER …), technology DBs `TechnologicalDataBlockData` with `IDBofFB`. Parser: `InstanceDB` or `TypedDB`.
- **Visibility:** members with `MFlags & 7 == 0` (e.g. ResetOld of PID_Compact) and AT views (`AtMemberID`, e.g. FC1_4_Req of MB_CLIENT) are not shown by Openness. Members with `Accessibility="Private"` are shown by Openness, the parser marks them with `accessibility: "Private"`.
- **Accessibility:** a `Variant` is never accessible, visible or writable. Below a non-accessible member nothing is accessible (e.g. private timers in MB_CLIENT).
- **System data types** (DataTypeKind 33: Conn_Ouc, Hw_Any, OB_Any …) are shown by TIA in upper case (CONN_OUC, HW_ANY); the OPC UA helper types keep their name.
- **TIA deletes unused single-instance DBs of system FBs** on compiling, including those created via Openness or the editor.
- **TSEND/TRCV as single instance** (sample 067_tsend_trcv_single): the SCL call `"DB_TSEND"(...)` fails with "Invalid function name", the call with instruction name `"DB_TSEND".TSEND(REQ := …, ID := …, LEN := …, DATA := …)` works (like `"DBTon".TON`). DB_TSEND and DB_TRCV match SimaticML and memberGet. TUSEND/TURCV can be created as a single DB neither via `create` nor via SD.
- Test `AllSampleDbsRead`: every DB of all samples has a known type, every instance, typed and technology DB has members, no member has an unresolved type.
- Open outside the DBs: in code block interfaces of some system FBs there are type IDs without `DataTypeItemData` in the project (AID of GETATTR_SFB, RID 0x84; ProDiag Temp, RID 0x4F; `Base` of the TO types, RID 0). F_SystemInfo_DB has an empty interface in the file.

## Motion technology objects and safety DBs (samples 068, 069; 2026-11-07)
- **068_motion_tos:** TO_SpeedAxis, TO_PositioningAxis, TO_SynchronousAxis (each 6.0, virtual axis `VirtualAxis.Mode = 1`) and TO_ExternalEncoder 6.0, connected in the editor to the data block `DB_Enc.tel` (PD_TEL81). All four TO DBs (`TechnologyDB`, 443 tags) and DB_Enc match SimaticML and memberGet. TO_MeasuringInput, TO_OutputCam, TO_CamTrack, TO_Cam and TO_Kinematics cannot be created on the CPU 1513-1 PN. A TO DB can only be exported if it compiles; the axes in 036_tech do not (missing drive telegrams).
- **069_safety_compiled:** 249_hw_fdi_module compiled and saved. Main_Safety_RTG1_DB = SimaticML (empty). F_SystemInfo_DB (signatures as start values), RTG1SysInfo (instance of F_CTRL_1 with F_SYSINFO) and the F-I/O DB F00000_FDI_1 (instance of F_PS_IN…) match the editor view (`editor/system_dbs.json`, test `SafetySystemDbsAgainstEditor`); they are not reachable via Openness (MCP-109). The DBs of the compiler blocks (`*_C`, `*_GCTX_DB`) have only hidden members (MFlags 0) and appear empty, as in TIA (know-how protected).
- In projects compiled but not saved (e.g. 246) F_SystemInfo_DB is empty in the file; TIA fills it on compiling.

## Setpoint, know-how protection, software units, S7-1200/300 (samples 060-071; 2026-11-07)
- **Setpoint of structured tags:** Openness reports a tag of a named structured type (UDT, FB, system type, array of them) as setpoint, anonymous structs and DTL not; under Input/Output/InOut nothing is a setpoint. Derived from 226 values (060, 066-068); the parser outputs it this way, the test now compares all setpoints.
- **Know-how protection (070_db_khp_units):** `ICoreAttributes.IsKnowHowProtected`/`Protection=KnowHowProtection` (key and salt in the expando). The interface stays readable in the file; a protected DB and the instance DB of a protected FB match the export before protection. As in Openness, the instance DB also counts as protected. Parser: `knowHowProtected: true`.
- **Software units (070):** a block of a unit references `UnitData` with `SWObjectData_IUnitElementAttributes_BaseUnitData`. Parser: `unit`. Openness (MCP) does not reach blocks in units (MCP-109); truth = copy of DB_Global plus editor view.
- **S7-1200 and S7-300 (071_db_1200_300):** CPU 1214C V4.7 (SD sources: optimized and standard DB, UDT, FB with IEC multi-instances and instance DB) and CPU 315-2 PN/DP V3.2 (classic SCL source via `sourceImport`: UDT, DB, FB, instance DB). All 5 DBs = SimaticML and memberGet. S7-300/400 blocks have `TargetFamily` S7300 or S7400, their data types are in scope `S7300400` (previously e.g. DATE_AND_TIME remained as RID).

## Technology objects of the T-CPU (sample 072; 2026-11-07)
- **072_motion_tcpu:** CPU 1515T-2 PN V3.1 with all TO kinds: speed, positioning and synchronous axis (virtual), external encoder (DB_Enc.tel.Sensor_1), cam (TO_Cam, TO_Cam_10k), leading axis proxy, kinematics (gantry 2D, A1/A2 = positioning/synchronous axis), interpreter, InterpreterMapping, InterpreterProgram, output cam, cam track and measuring input (on TM Timer DIDQ, DI0 Timer DI). Output cam, cam track and measuring input hang under the axis (`OutputCamMeasuringInputContainer`) and can only be created via VNC; exported with `tools\Export-SubTos.ps1` (MCP-116).
- **Inheritance of TO types:** outside the sections the type interface has the member `Base` (ID 49, `SubPartIndex` to the root of the base type): TO_SynchronousAxis → TO_PositioningAxis → TO_SpeedAxis → TO_Axis → TO_Object. The members of the base type come before the own ones in each section; value paths get the prefix `49:` per level (`49:49:73:51`). SimaticML nests this as `Section Name="Base"`, Openness and the parser show the members flat. The parser had so far omitted the inherited members (positioning axis 201 instead of 341, synchronous axis 22 instead of 363 paths), the test did not count the nested sections; both corrected, 068 was affected as well. The same mechanism applies to Program_Alarm : ALARM_BASE (Alarm_ID, HANDLE before SD_0; 066 `DB_Program_Alarm` = SimaticML); so far a wrong section "Base" appeared there.
- **Comparison:** all 14 exportable TO DBs and DB_Enc = SimaticML (together with 059-071: 13 593 check values). The leading axis proxy cannot be compiled without a leading CPU (I-device transfer area with partner) and therefore not exported; it matches the DB editor (25 members with data type, access, writable, visible, setpoint; `editor/system_dbs.json`).

## Status of DB coverage (2026-11-07, commit 3bef6bd6)
Estimated at about 95 % (assessment, not a measured value). Evidenced against TIA V21:
- **DB kinds:** global DB, instance DB (FB, system FB/SFB), technology DB, array DB, DB of a UDT/system type; all 1 285 DBs of all samples are recognised and read without unresolved types (`AllSampleDbsRead`).
- **Content:** all data types, structs, UDTs, multi-dimensional arrays, element start values, multi-instances on several levels, 31 system FBs as single and 64 as multi-instance, IEC timers, all TO kinds of the T-CPU incl. inherited members, safety DBs, know-how protected DBs, software units, S7-1200 and S7-300.
- **Truth:** 13 593 SimaticML values (`DbSymbolsAgainstSimaticML`), a good 10 500 memberGet attributes (retentivity, access/visible/writable, setpoint; `DbMemberAttributesAgainstOpenness`), around 280 values from the editor view (`SafetySystemDbsAgainstEditor`, `DbIecTimerSingleInstance`).

Open:
- TUSEND/TURCV as single instance cannot be created in TIA (only evidenced as multi-instance).
- Offsets of non-optimized DBs only checked on DB_NonOpt (060); further standard DBs with offset check are missing.
- Attributes of the technology DBs: no memberGet for TOs, only the leading axis proxy against the editor.
- S7-400 and older firmware versions not tested; multilingual comments on DBs only spot-checked.
- Three type IDs in interfaces of system code blocks (not DBs) unresolved.
- MCP bridge: sub-TOs (output cam, cam track, measuring input) and system/unit blocks not reachable (MCP-109, MCP-116); workaround `tools\Export-SubTos.ps1` or the editor via VNC.
- Known unrelated error in the test run: `samples/F18_iface_event` (untracked, idx check).

