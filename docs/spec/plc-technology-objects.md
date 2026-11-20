# Technology objects (Z1.2 P5; samples 052–053)

As of 2026-10-25. Truth: `TiaPlcSoftware(get/technologyObject)` (Openness `TechnologicalParameter`), base 036_tech (eight TOs: speed axis, positioning axis, synchronous axis, external encoder, PID_Compact, PID_3Step, PID_Temp, High_Speed_Counter).

| Sample | Change | Finding |
|---|---|---|
| 052_plc_to_param | `TO_Speed_5_0` (TO_SpeedAxis V6.0): `DynamicDefaults.Velocity` 600 → 750 | only the TO DB changes: its own interface (`InterfaceVersionRootData` 'BIVE:TO_Speed_5_0/…', Kind Values) has as payload `<Values>` with `<V p="59:51" v="750.0"/>`; fingerprint and timestamp new |
| 053_plc_to_inverse | `Actor.InverseDirection` false → true | further entry `<V p="55:52" v="true"/>` |

- **Storage:** a TO is a DB (`TechnologicalDataBlockData`, `IGeneralDataBlockSourceData.Type` IDBofFBT, `OfName` = TO type). Its parameters are start values of this DB. The paths `p` are member IDs of the interface of the TO type, to which `UsedParts` of its own interface root points ('_BIVE:TO_SpeedAxis/5.0.0/<guid>' in a `LibraryBlockInterfaceContainer`). Only values that deviate from the type or were set once are stored (H-HW-01 analogously).
- **Parser:** the TO DB gets `interface` like an instance DB (section Static with the members of the TO type, `startValue` = stored value, otherwise null). The Openness parameter names are the member paths with dots (`DynamicDefaults.Velocity`).
- **Test:** for 052/053 test_plc compares each Openness parameter whose member path has a stored start value in the DB with this value (number, Bool). The PID TOs are instance DBs of know-how-protected FBs and do not go this way.

Open: TO hardware connection (`hardwareConnect`), parameters with Openness names without member path (`_Units.*`, `_Actor.Interface.*`, probably from expandos on the TO), technology events (`IpoEventData`, `CyclicServoEventData`).

## Hardware connection (sample 079, 2026-11-07)

- The connection is stored as a separate CompileUnit on the TO DB: ProgrammingLanguage `Motion_DB`, ProgrammingContext `ioif`, Subtype Invisible. Content `<section>` with one `item` per connected interface (`first` = interface Actor/Sensor/Input, `second/dst` = target, `second/ids/item/refId` = operand in the ident table of the DB, e.g. SimpleAccess to the tag AxisOut, `props` e.g. `vm`). Without a connection the unit is missing.
- Parser: `hardwareConnections` on the TechnologyDB (also in 068/072: encoder on DB_Enc.tel, measuring input); test `PlcToConnectionTests`.
