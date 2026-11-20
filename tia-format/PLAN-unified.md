# Plan: Read WinCC Unified data from TIA V21

Status 2026-10-20 (U1–U6, U8, U10 done; U11 running; U7 and U9 need User). Original status 2026-10-19 19:48. **U0 + U1 approved** (User 19:47 "as it makes sense"), 19:55 User allows up to 3 TIA instances (1 approach, 2 Unified, 3 library) → U0/U1 started 19:56. **U0 done** (commit 5246ab0, note not in the repo). **U1 paused:** TiaMcpXE can only serve one TIA instance for all clients (one bridge connection, attach switches the whole server, no instance argument, second server locked by mutex). 20:02 User: TiaMcpXE is made multi-instance capable in the TiaMcpXE project (up to 4), requirements in TiaMcpXE-multiinstance-requirements.md; until then one instance, one after the other: approach (4e, 5c, 5d) → **U1** → library L1. Device: **MTP700 Unified Comfort**, WinCC Unified panel license available. U2–U7 not approved. Every step is approved individually by User (see [PLAN.md](PLAN.md) for the main plan; this plan replaces item 6 "HMI" there for the WinCC Unified part).

Goals and non-goals: see ..\docs\GOALS.md (the only authoritative source).

**Delimitation from [PLAN-library.md](PLAN-library.md):** faceplate *types* and HMI data types as library types belong there (step L3). This plan only covers faceplate *instances* in screens (placement, connection to HMI tags, in U5). The library plan also uses the inventory from U1.

## Approach
As before: one-change samples via TiaMcpXE (Openness), difference analysis with the existing tools (object graph, attribute diff), ground truth from Openness. Since we read the embedded metamodel, the type names alone (e.g. Hmi* packages) already provide a map before diffing. Samples get their own number range **100–199** so that nothing collides with 5c/5d (023 ff.).

## Open risks (settled in U0/U1)
- Unified contents could be stored **outside** `PEData.plf` (additional files/DBs in the project folder) or in a different format (e.g. JSON/binary blob) inside a record.
- Openness/MCP may not supply a SimaticML export for screens for Unified; then ground truth = property query via MCP (attributeGet or similar).
- For Unified devices TIA needs a WinCC Unified engineering license.

## Steps (each to be approved individually)

| No | What is done | What is created/changed on User's machine | What for |
|---|---|---|---|
| U0 | Determine the MCP capabilities for Unified: query `TiaHmiUnified` (and related tools) with `operation:"help"`, note the list of possible operations and ground truth paths (export, reading properties). | Nothing except a note (not in the repo). **No project is opened or changed.** | Know which variants we can create and check via MCP at all, before samples are planned. |
| U1 | Base sample `100_hmi_device`: copy of 002_cpu, add a Unified device (MTP700 Unified Comfort, User's choice), HMI connection to the CPU. Inventory: new files in the folder/`.zap21`? New packages/types in the metamodel? | 1 new test project in `samples\` (folder + `.zap21` + truth + analysis). Code only if inventory/analysis shows a new file kind. | Clarify **where** Unified data is stored and which object types exist. Decides whether the rest builds on the existing record reader. |
| U2 | HMI tags: one sample each for tag table + internal tag, external tag on PLC tag `Tag_Bool_1` (base 003_tagtable line), change data type/acquisition cycle, comment. | approx. 4 test projects `samples\101…104`. | Layout of the HMI tag and of the link HMI → PLC tag. |
| U3 | Alarms: alarm class, discrete alarm (trigger tag + bit, text), analog alarm (limit), alarm text in two languages. | approx. 4 test projects `samples\105…108`. | Output alarms incl. multilingual texts. |
| U4 | Screens: new screen, rename screen, change size/background color, screen in screen folder. | approx. 3–4 test projects `samples\109…112`. | Screen list with properties. |
| U5 | Screen items: one sample each with rectangle, text field, I/O field with tag dynamization, button with event script; one sample with changed position/color; as soon as L3 supplies a faceplate type, one sample with a faceplate instance. | approx. 5–6 test projects `samples\113…118`. | Output screen items, properties, dynamization and scripts; probably the most laborious part. |
| U6 | Extend the parser: section `hmi` per Unified device (tags, alarms, screens, screen items) in text and JSON output, tests against the ground truth of all samples 100 ff. | Code in `parser\openpedata\` + `parser\tests\`, local git commit. | Usable result. Pulled along step by step after U2–U5, not only at the end. |
| U7 | Optional: parse a real Unified project from User read-only. | Possibly a copy in `samples\`. | Robustness, find gaps. |

After every step: result in `docs\spec\hmi-unified-*.md`, `docs\spec\hypotheses.md` (new IDs from H-021), `docs\spec\LOG.md` and a short message in the thread.

## Order and dependency on the main plan
- Runs **after** 4e/5c (and possibly 5d) from PLAN.md, because TIA and the MCP server should only be served by one session at a time. Alternatively between two main plan steps, if User prioritizes it that way.
- U0 and U1 are the switches: if everything is stored as normal records in the plf, U2–U6 are diligent work with known tools. If Unified is in its own files/formats, a container step (U1b) comes before U2, which is then re-planned and approved.

## Rules (unchanged)
- **Prio 1+: Do not read, run, install or adopt anything from jfk-solutions "TiaFileFormat" / TiaProjectBrowser.**
- Writing only in this repository; `C:\GitHub\factorybyte\TiaMcpXE` read only.
- Every TIA change on a copy, originals stay unchanged. Install nothing, no git push.

## Answers from User (19:47)
1. U0 + U1 approved.
2. MTP700 Unified Comfort.
3. WinCC Unified panel license available.

## Result U0 (2026-10-19, commit 5246ab0)
- TiaHmiUnified: 139 operations (objectKind). Device: TiaDevice typeSearch/create (MLFB MTP700 still to be determined). HMI connection: TiaNetwork hmiConnectionIntegrate.
- Coverage: tagTable/tag (+tagCreateFromPlc), alarmClass, discreteAlarm, analogAlarm, screen, itemCreate/itemSet/itemGet, itemBindTag/itemBindScript, itemEventCreate, scriptModule.
- Not via MCP: creating faceplate instances (Openness V21 offers no faceplate types), recipes, cycles, task scheduler, alarm groups, popup screens; text/graphic lists only import.
- Ground truth: `get` queries per object kind, `screen get detail=full` (JSON). exportXml is a TiaMcpXE-specific XML (schemaVersion 1), **not SimaticML**.
- Consequence for U5: faceplate instances only if User creates them by hand.

## Result U1 preparation without TIA (commit 928a1aa, samples\UNIFIED-METAMODEL.md on User's machine)
- No sample contains Hmi/Unified/Screen/Script packages. The metamodel pulls in packages along with the content (000/001: 28 packages/561 types; from 002: 42/980) → Unified types only visible with U1 (H-021).
- Already present without HMI: AlarmServices 0x0040 (AlarmClassData 0x00401003, text categories; per CPU AlarmContainerV2 0x00401010, InternalDisplayClassV2Data 0x0040102a), SysDiagAlarmSettingData 0x0041100e; without objects: HmiTargetData 0x00101005, HmiApplicationData 0x00101006, HmiConnectionPointData 0x00101007.
- TIA installation: metamodel definitions as `Meta\Model\*.mpk` (ZIP, entries in a proprietary encoding, not decoded, no attempt). Only entry names readable; `WinCC.BCA.Code.mpk` contains 218 `Simatic-Hmi-<Name>.xml`, among them HmiTag, HmiAlarmClass, HmiDiscreteAlarm, HmiAnalogAlarm, HmiScreen, HmiScreenItem, Dynamics-Model, ScriptModuleContainer, FaceplateLib → presumed package names.
- MTP700 MLFB still open (typeSearch needs an attached session) → in U1.
- Parser finding: `Metamodel.packages` (metamodel.py) is keyed by `id >> 16`, several packages share the value → only 9 instead of 28 or 15 instead of 42 packages in the dictionary. Not changed, reported to the thread "Approach".

## Result U1 (2026-10-20, commit 2d254c1)
See `docs\spec\hmi-unified-container.md`. In short: Unified data is stored **in both**: engineering model as normal records in the plf (+1322 records, 58 new Hmi packages, type IDs with table) and runtime files in `IM\HMI\`, `AdditionalFiles\RtIdFiles`. So the existing record reading technique still holds; the files next to it are an additional work area (H-022). MLFB MTP700: 6AV2 128-3GB06-0AXx, FW 21.0.1.0. Parser runs without errors on 100, tests 64/64 green; a decoding error in the expando (H-023) blocks attrdiff.
**Instance:** TIA instance 10084 (server-owned) keeps running empty; terminate only with User's approval.

## Result U2 (2026-10-20, commit 4a9bfca)
See `docs\spec\hmi-tag.md`. Samples 101_hmi_base, 102–106 (numbers shifted by 1 relative to the plan because 100 has no PLC tag). HmiTagData 0x00e51008 completely mapped; the cycle is a link to HmiCycleData, link HMI→PLC via ScopedLinkMaintainerData 0x00025021; comment with HTML body. H-022 confirmed for saving: IM\HMI unchanged, names only in the plf. Open: parser output of the HMI tags (building block in U6), TypeSafeAddress (H-026).

## Result U3 (2026-10-20, commit a6155fc)
See `docs\spec\hmi-alarm.md`. Samples 107–109 (class, discrete alarm, analog alarm). Alarm class with mirror object AlarmServices.AlarmClassData, alarms without name (Subtype, IsShell), trigger tag as direct link, alarm text in its own HmiTextData (UTF-8 plain text), limit as string. Open: multilingual texts (needs a second project language, cannot be created via MCP), alarm groups/system events.

## Way of working from 2026-10-20 15:52 (User)
No more approval per step: U5, U6 etc. run through without asking. Still valid: TiaMcpXE folder read only, never touch other TIA instances, panel runtime files (IM\HMI, RtIdFiles; H-025) only after the PEData evaluation, multilinguality (H-028) at the very end. Ask only about things that only User can do. New MCP problems: MCP list (not in the repo).

## Faceplates (UF): separate thread
User 2026-10-20 15:57: The faceplate topic is taken over by a separate new thread ("Unified Faceplates"), not this one. This plan continues with U4 to U6. Preliminary work that already exists and can be taken over by the new thread: draft of a manual recipe [RECIPE-faceplate.md](RECIPE-faceplate.md) (menu names unchecked) and the proposal to create screens with as many Unified object types as possible via MCP in a test project (copy of 103_hmi_tag_ext), from which User builds faceplates by hand (version 1.0.0/1.0.1, one instance), and then to analyze the diff in the plf. For the library side (type, version) see PLAN-library.md L3.

Continued in [PLAN-faceplate.md](PLAN-faceplate.md) (thread "Unified Faceplates", from 2026-10-20 16:05); samples there with prefix `F` instead of 120/121.

## Result U4 (2026-10-20, commit f001ca7)
See `docs\spec\hmi-screen.md`. Screens = ILScreenData with expando properties (only changed values, numeric property IDs), no screen number, start screen via HmiGraphXSettingsData. Correction to U3: the alarm name is in Name (parser error Metamodel.ancestors, 24 types).

## Result U5 (2026-10-20, commit 5f90d73)
See `docs\spec\hmi-screenitem.md`. All screen items are ILScreenItemData (type in Subtype), properties as expando with property ID (only deviations), tag dynamization as link ExpandoReferences → HmiTagData, events as HmiScriptEmbeddedData with their own container; script text as plain text in a proprietary blob format (H-030). Parser outputs the objects per screen; test_hmi 50/50.

## Result U6 (2026-10-20, commit f321f63)
Parser section `hmi` (hmi.py) supplies per device software, connections, tag tables with tags, alarm classes, discrete/analog alarms, screens with objects (properties, text, tag dynamization, event script code). Tests: test_hmi 52/52 (samples 100–124 + F01, each folder and zap), test_samples exit 0. Shared fixes: H-023 (short expando block), metamodel inheritance topological (dfd18a5). Property IDs: docs\spec\hmi-properties.md. Open: 24 % of the values (8 IDs), control texts, script whitespace.

## Remaining steps (without individual approval, except manual work)
| No | What | Who |
|---|---|---|
| U8 | Panel runtime files (`IM\HMI`, `RtIdFiles`, H-022/H-025): are they derived (created on compiling/creating) or do they carry their own content? Comparison 101 → compiled project (compile via MCP, if possible) → file changes; RDF header, structure, whether tags/screens/alarms can be found in them; decision whether the parser needs them. | Assistant, MCP compile |
| U7 | Parse a real Unified project from User read-only (find gaps: 8 open property IDs, controls, scripts with whitespace, script modules, faceplate instances). | **User** supplies project/archive |
| U9 | Multilinguality: second project language (by hand), texts in tags, alarms, screen items, text lists. | **User** creates the language |
Faceplates: separate thread "Unified Faceplates" (PLAN-faceplate.md).

## Result U8 (2026-10-20, preliminary)
Question: does the parser need `IM\HMI` and `AdditionalFiles\RtIdFiles` as source data? **No:** both are created when the HMI device is created and stay byte-identical in all samples 101–124. Tags, alarms, screens, objects, scripts are only in `PEData.plf` (names in plain text only there). A failed compile changes no file. Open (deferred, User): what a successful compile writes and what the panel runtime format looks like. For that, User has to set the runtime password of the panel in TIA once (Openness V21 cannot do it); afterwards an assistant can compile and compare.

## Result U8 (2026-10-20, commit 42b0e7a)
See `docs\spec\hmi-runtime-files.md`: IM\HMI, RtIdFiles, ValidationFiles are derived, the parser does not need them. Compiling and runtime format deferred (runtime password, MCP-027).

## U10 (running, without approval): close gaps in the hmi parser
The 8 open property IDs (08, 0e, 0103, 0402, 0d02 polygon points, 3409 selection entries, 4f02 text reference, 7904), control texts (column headers, table/trend columns), multi-line scripts (whitespace), text lists (HmiTextListData), cycles, further script events. Method: targeted single-object samples, truth, parser tests; coverage of the stored values from 76 % towards 100 %.

## Result U10 (2026-10-20, commits 072d50c … f41f6a1)
Screen item properties 99.5 % (37 IDs), texts per language, scripts exact, dynamizations, text lists, cycles, counters; test_hmi 67/67. Shared fix decode_simple_expando.

## U11 (done): last remainders
Mapping tables of the dynamization (ranges from/to/value), tag ID in scripts → tag name (H-031), property 0103/7904 on the alarm control.

## Result U11 (2026-10-20, commits 3b96820, 1c3dae3, 03b04a0)
- **Mapping tables:** ValueConverter → MappingTable → Entries + ConditionType (0 None, 1 Range, 5 Singlebit). Range: From/To/Value/RangeType, Value in the encoding of the property (color varint 8 + 3 bytes + ARGB, number code 0x13 inline). Singlebit: relevant/condition bit masks, output raw. Example: `dyn Width <- Tag HmiTag_IO Range: 0..10->100, 11..20->150, 21..30->200` (samples 134/135).
- **Tag references in scripts (H-031 solved):** the number in `Tags("…")` is the name of an entry in IScriptLinkExpandoAttributes; its value (u32 BE) is an index into the ExpandoReferences of the script (0 = the object itself, 1.. = tags). 10/10 F01 scripts = Openness.
- **Open:** 0103 (code 21, presumably nested AlarmState) and 7904 (code 0x1400b, presumably filter) on the alarm control, 2 of 438 values.
- test_hmi 69/69. Unified is thus done except for U9 (second project language, manual work, last), both only at User's request.

## Addendum F40 dynamizations (2026-10-22, commit 286bb93)
Reported by the faceplates thread: "ResourceList None"/"Expression None" in the text output for F40_siemens_demo_bestpractices.
- Cause: dynamizations() only knew Tag and Script; Tag came from the link list of the object instead of from its own reference field.
- Fix: reference fields (Tag, ResourceList, Script, TagRefs) via IPropertyDynamicLinkExpandoAttributes → ExpandoReferences of the object. ResourceList = list + index tag; Expression = tags + conditions (ConditionType 6 = Expression).
- Further errors: value table code 0x02 = 1 byte inline (was offset); structure members as Tag.member as in Openness.
- New property IDs: 6e = BorderWidth, 6e24 = IsAlternateState (confirmed against the truth).
- test_hmi 72/72 (F40: 36/36 dynamizations = truth), test_samples/test_faceplates green.
- Not checked against Openness: object dynamizations of type ResourceList/Expression (none in F40). Open without truth: 050c presumably Screen (HmiScreenWindow), 7402 presumably Graphic (HmiGraphicView), 71 unknown.
- **Comparison (2026-10-22, commit 031a7e5):** 12 screens of the F40 copy exported via Openness (1001, 1013, 1015, 1023, 1031, 1033, 012, 1016, 232, 1135, 1032, 1113), truth only local (Siemens demo project, not in the repo). test_hmi F40 538/538. ResourceList/Expression confirmed. 050c = Screen, 7402 = Graphic, 71 = AlternateBackColor confirmed. New: Flashing (color, alternate color, rate, condition), screen window areas show screen names. Error fixed: the length prefix of long strings (≥127 bytes) counts its own bytes (as attrs.read_var); affected script code, texts, expressions. Property events are not dynamizations.
