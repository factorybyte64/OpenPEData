# Program info (resources, call structure, dependency structure, assignment list)

TIA shows four tabs under PLC > Program info. All values can be derived from the plf and `XRef\XRef.db`;
module `parser\openpedata\proginfo.py`, option `--proginfo`, test `parser\tests\test_proginfo.py`.

Evidence (2026-10-31): screenshots by the user from TIA V21 for the samples 006_tag_addr, 025_fb_idb, 031_lad_all, 036_tech,
039_fw41 (CPU 1516-3 PN/DP) and 040_s71200 (CPU 1214C). Stored with the computed values in `<shared folder>\proginfo\<project>\`.

## Objects of the PLC

Only objects with `Target` → ControllerTargetData of the PLC count. The plf additionally contains copies in libraries and
older ControllerTargetData with the same name.

System blocks and system UDTs appear several times in the plf, once per target family (`R_TRIG_1200`, `R_TRIG_1500`, `R_TRIG`,
`MOVE_BLK_VARIANT_FC_900_S71200` …). TIA counts only the object with `IExpandoInformationData.TargetFamily` (name without
suffix, `ILoadObject.LoadType = PLUSBLOCK`). Instructions with `LoadType = S7BLOCK` (MIN, CONCAT …) do not count.

## Resources

| Value | Source | Evidence |
| --- | --- | --- |
| Load memory code block | `IGeneralBlockResultData.LoadMemoryRequired` of the `LoadableBinaries` target | all samples |
| Load memory system block | `IExpandoInformationData.LoadMemorySize` (LoadMemoryRequired is 0) | 036 FB +54529 / FC +9302, 031 FB +55117 / FC +46265 |
| Load memory DB | `IExpandoInformationData.LoadMemorySize` of the DB | 036, 039 |
| Code work memory | `WorkMemoryRequired` of the loadable, without blocks with `IGeneralBlockSourceData.ResourceHandling = NotCounted` | 036 PID_Compact, High_Speed_Counter, PID_3Step, PID_Temp, MC_POWER (5439) |
| Data work memory | `WorkMemoryRequired` of the DBs with LoadMemorySize | 036, 039 |
| Retentive memory | `IExpandoInformationData.RetentiveMemoryLength` of the DBs (`RetainMemoryRequired` on the loadable is always 0) | 036, 039 |
| Data types | `LoadMemorySize` of the UDTs with TargetFamily | 036 115674 = UDT_Motor 1151 + 114 system UDTs |
| PLC tags | sum of `TagLoadMemory_Input/Output/Marker/Timer/Counter/Constant` (ICoreExpandoAttributes of the ControllerTarget) | 036 2856, 040 655 |
| ">" | one block of the row has no value (not compiled, `CompileArtifacts = Binary`, protected OB without loadable); "?" if none has one | 036 OB/FC/DB, 031 FC, 006 OB |
| Total | not in the project (catalog value of the CPU; `IControllerTarget.MemorySizePerRange` does not contain the numbers) | – |

An S7-1200 shows one column "Work memory" = code + data (040: 178).

## Call structure

XRef relations of the PLC with `usage 1`: `flags 12/15` = call of an FB/FC, `flags 16` = instance DB of a call.
Calls of an instruction (`Instruction`) whose system block is in the PLC count as a call of this block
(031: FC_SclVar → MOVE_BLK_VARIANT FC900). Used DBs (DBMember, InstanceDB) appear as children, as in TIA.
Roots: the OBs, then every block that is not reached from any OB, even if another such block calls it
(036: FC_SclOps appears as a root and under FB_SclCalls). Order OB, DB, FC, FB by number. TIA does not list SFB/SFC (040:
CTRL_PWM_SFB). Local data = `ILoadablePlusCodeBlockData.LocalNenaDataLength`, summed up along the path.

## Dependency structure

The same relations reversed: per object (UDTs, DBs, FCs, FBs, OBs in this order) the objects using it.

## Assignment list

Addresses from `XRef.addrs` of the tags used in the program (TIA does not list unused ones; 036: tDInt,
tByte, tTime, tUInt are missing). Bit for 1 bit, otherwise B/W/D/L by width.

## Open deviations

- 039 local data Ack_Alarms (TIA 2, plf 0) and RT_INFO (TIA 18, plf 16).
- 006 (Main not compiled): TIA shows PLC tags 1232 bytes, the plf has no TagLoadMemory values.
- 040: in the assignment list TIA lists the hardware addresses without a tag (IB0–1 onboard DI, IB64–67 AI, IB1000–1023 HSC);
  the output contains only tag addresses.
