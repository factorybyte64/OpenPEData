# Plan: Read WinCC Unified faceplates from the PEData.plf at 100 %

Status 2026-10-20 20:15 (User 17:59: "Read the docs on everything faceplates can do, and create a list of what you have to work through to be able to break down a faceplate in the file format 100% reliably … make a PLAN and a LIST!"). Thread "Unified Faceplates". Basis: 51 faceplate functions from the TIA V21 help with storage location, see `docs\spec\faceplate-doc-features.md`. Established findings: `docs\spec\hmi-faceplate.md`. Docs rule (User 18:13): Siemens docs first via the local help API `https://localhost:5112/en-us/home?api=PortalV21`.

Goals and non-goals: see ..\docs\GOALS.md (the only authoritative source).

## PLAN (phases A–E)

**Phase A – Decode the basics, without new samples** (material: F01, F02_hmi_faceplates_crash, Openness truth of F01)
- A1 Expando format of the screen items: property IDs, value types, layer GUID, dynamizations, events. The Unified thread has already established 20 IDs (`docs\spec\hmi-properties.md`, `docs\spec\hmi-screenitem.md`); here the faceplate objects and the container are added, no duplicate code.
- A2 `CoreMapArray` (format of the three interface lists InterfaceEvents, InterfaceProperties, TagInterfaceProperties).
- A3 Version number (`VersionData` blob), State, FinalizationTime, default relation, author/comment; comparison with docs\spec\library.md (PLC types).

**Phase B – Base sample F10 (User, by hand).** Copy of `samples\103_hmi_tag_ext`, one faceplate type `FP_T` with a rectangle, released as 1.0.0, not instantiated, save, close, archive. Do not build on F01 (defective FP_Flash, typeless containers).

**Phase C – Variation series F11–F34 (User by hand, each with exactly one change relative to F10, archive after every step):**

| Sample | Change | settles (# from docs\spec\faceplate-doc-features.md) |
|---|---|---|
| F11_iface_tag_bool | 1 interface tag Bool | #23, TagInterfaceProperties, tag table of the hidden device |
| F12_iface_tag_types | Interface tags Int, Real, LReal, WString, DateTime, Array[0..3] of Int | #24 |
| F13_iface_tag_plcudt | Interface tag PLCUDT (UDT_Motor as library type) | #25, dependencies |
| F14_iface_tag_hmiudt | Interface tag HMIUDT | #26 |
| F15_local_tag | local tag Int with limit | #27 |
| F16_iface_prop_types | one interface property each of all 11 data types | #30 |
| F17_prop_dyn | rectangle color via Property interface, once with formula, once with evaluation Range | #31 |
| F18_iface_event | Interface event with 3 parameters (Int, Bool, Color), RaiseEvent script on a button | #34, scripts in the type |
| F19_type_dyn_script | in the type: tag, script, expression, resource list dynamization, Loaded event, global definition area | #36–38 |
| F20_type_props | Suspendable, display name, background, second layer "Runtime visible" off | #16–18 |
| F21_version2 | "Edit type" → 1.0.1 InWork (nothing changed); second archive after release | #3, #4 |
| F22_do_not_use | marking "Do not use" | #10 |
| F23_rename_dup | rename; then duplicate | #6, #7 |
| F24_folder | type in library folder | #12 |
| F25_instance | one instance on Main, no interconnection | #40, mediator |
| F26_instance_bind | interface tag statically connected, interface property static | #32, #42 |
| F27_instance_dyn | Instance: interface property via tag/script, interface tag via tag parameter, interface event script | #42–44 |
| F28_two_instances | second instance, other values | repeatability |
| F29_update | release type as 1.1.0 with "Update instances" | #9 |
| F30_nested | inner type in outer type, interface passed through | #47 |
| F31_popup | popup script (inside and outside) | #48 |
| F32_language | second project language, texts in the type | #19 (at the very end, H-028) |
| F33_controls | alarm control and trend control in the type, trend via configuration string | #22 |
| F34_delete | delete version or type | #8 |

The already existing states F02_hmi_faceplates_crash (FP_Basic 0.0.1 Committed, FP_Controls InWork) and User's further faceplates in F01 are evaluated beforehand with `tools\fp_chain_diff.py`; they do not replace F10/F21, but provide first evidence for A3 and LIST 4.

**Phase D – Comparison with the truth.** Per sample Openness truth of the instances (`get screen detail=full`, interface values); for the type, whatever Openness does not supply, the TIA user interface (screenshot/transcription by User). Parser section "Faceplates" in text and JSON, tests in `parser\tests` against all F samples. Runs along step by step after every sample.

**Phase E – Writing (later).** Only when A–D are green for reading: change instances via the file, then types (layer reference, hash chain, signature, see docs\spec\integrity.md).

## LIST (to tick off, 21 items)
1. [x] A1 (20:40, 58d0fa0) Expando decoding for screen items in the faceplate and for the container (values, layer, dynamizations, events); comparison against F01 truth; addition to docs\spec\hmi-properties.md.
2. [x] A2 (f5e68f7) `CoreMapArray` = array of pe:MapT, MapT format decoded on 13 other attributes (coremap.py); confirm faceplate entries with F11/F16/F18.
3. [x] A3 (58d0fa0, 0.0.1 as for PLC) Version number (`VersionData`), status, default, FinalizationTime, author/comment of the version.
4. [x] (8f20efb, 79 core objects) Assign the hidden device per version completely (what is always created, what depends on the content).
5. [x] (ef73eb9, 1d8bd62, 2416777; F05/F40/F12) Interface tags: storage location, data type codes complete, array (+0x40, bounds, element tags), PLC UDT and HMI UDT as library reference.
6. [~] (50a50b0, F15) Local tag = HMI tag in the Default tag table of the hidden device, without interface entry; limits = attributes on the tag (type + constant), still NoLimit in the sample; binding to an I/O field was missing in the sample. Rest: limits with stored value and binding to a screen item = optional sample F15b; F33 shows that TIA itself creates a tag bound in the type as a local tag.
7. [x] (4c67876, F16) Interface properties: all 11 data types with codes and default values; editable default values do not exist in TIA (User 2026-10-23), values are set on the instance (F40).
8. [ ] Dynamization "Property interface" incl. formula and evaluation type (F17).
9. [x] (24d1011, dfb9587, F18/F18b) Interface events: definition (type as name, Int not stored), RaiseEvent script, handler OnEvTest on the container; further parameter types only when needed.
10. [x] (be949fa, SCRIPTS.md, from F03/F05) Dynamizations and events in the type, global definition area, script objects: same objects as in the screen, scope Faceplate.*; F19 only for script access to interface properties.
11. [x] (22fa21e, d79371d, TYPE-PROPERTIES.md, F20.md) Type properties: layers (32, expando 0x024b, "Visible in runtime" off = field 0x07 False), size, type/version attributes established; Suspendable = expando 0x0263; background = 0x66 BackColor; display name is not stored (F20, asked User).
12. [~] (71c2b68) Release changes the version in place (status, time, new VersionGuid, default link), no copy as with PLC types; new version via "Edit type" = complete copy with its own device/VersionGuid (F16, 4c67876); open: Do not use, rename, duplicate, folder, delete (F21–F24, F34).
13. [~] (ef73eb9, F05) Instance: type reference IsInstanceOf → version, ContainedType "V0.0.1\FP_Basic", no mediator without interconnection; F40: UsedTag, static values, dynamizations, TagParameter established, mediator = script reference to type (no instance part); open: interface values static/dynamic, tag parameters, events (F25–F28).
14. [ ] Updating/replacing instances (F29).
15. [x] (10f2106, d79371d, DEPENDENCIES.md, F30.md) Empty container = ILFaceplateContainerData without IsInstanceOf; typed container in the type -IsInstanceOf-> inner version, outer version -DependsOn-> inner (link only, no DependencyIdentifiers), no mediator. Passed-through interface only when needed (FP_Basic has none).
16. [ ] Popups and type references in scripts (F31).
17. [x] (F32, F32.md; texts per language on the screen item, languages of the version, runtime languages; import with version increment not sampled) Multilinguality, fonts: sample **F32_multilingual** (second project language German, translated text in FP_Interface, Excel export of the library texts as reference samples\F32_texts.xlsx); expectation: text records with a second language. Fonts already clarified via FontList_C.
18. [x] (3dc4517, d79371d, CONTROLS.md, F33.md) Controls in the type: only 4 allowed (alarm control, trends, Parameter set, container), sub-objects as map expandos, FontList_C clarified; column lists 0x3815 (alarm view) / 0x3832 (statistics), trend areas 0x3839 with trends and data source; 0x31d00/0x72/0x76 = CoreMapArray with blob elements.
19. [x] (33c04c6, parser\openpedata\faceplates.py, test_faceplates 182 sources, 56/56) Parser output; interface/instances are added with F11/F25. Parser output "Faceplates" (types, versions, content, interfaces, instances) and tests against all samples.
20. [x] (f5e68f7, Classic only, 0 objects) Clarification `HmiPropertyInterface*` (ScreenModules): used in Unified or only Classic?
21. [x] Help API explored (docs\TIA-HELP-API.md, #52–57 added); Siemens 109812366 V2.0 (01/2026) evaluated: analysis\F3_faceplate\SIEMENS-109812366.md. Rest from it: data type "Configuration string", Upgrade device version, Interface Inheritance, event handler via function list, AlwaysDownload (sample proposals there).
22. [x] (F18c, F18c.md) Event handler on the instance via **function list** instead of script (Siemens 109812366 p. 31), sample **F18c_event_funclist**. Prepared: function lists are `HmiFunctionListEmbeddedData` (0x00e61003) with the same script attributes (IScriptAttributes.Code = generated JS code with HMIRuntime.*.SysFct, scope, signature) and the same relation EmbeddedScriptChildren as scripts; hmi.py already reads them as an event, but does not distinguish them (only F40 has some, 226).
23. [x] (F36, F36.md; AlwaysDownload on the type, popup mediator via ExpandoChildren, DefaultName not used in the sample) **AlwaysDownload / DefaultName / Popup** (Siemens 109812366 pp. 46–52), sample **F36_popup_alwaysdownload**: FP_Basic with AlwaysDownload, new version, button on Main with `UI.OpenFaceplateInPopup(...)`. Expectation: `IHmiByRefTypeObjectBaseAttributes.AlwaysDownload` True, storage of DefaultName, FaceplateInstanceMediatorData on the script (as in F40).

Not checkable with the test setup: OPC UA data types (#28), runtime behaviour (Suspendable, Flashing), Parameter set control.

## What only User can do
Phases B and C are manual work in TIA (Openness V21 does not know faceplate types). The recipe per sample follows in [RECIPE-faceplate.md](RECIPE-faceplate.md). Everything else runs without asking.

---

## Original plan (F0–F4, 16:05) and status
Goals and non-goals: see ..\docs\GOALS.md (the only authoritative source).

### Delimitation
- **PLAN-library.md (L3/L4):** general type/version mechanics (TypeObject/VersionObject, release = copy, links). This plan uses them and coordinates findings there (thread "Plan read project library"). L3 is done by this plan; L3 samples are called `F…` here.
- **PLAN-unified.md (U4/U5):** screens and screen items outside of faceplates. The object records of the screens in F01 also serve U5 as evidence.
- Panel runtime files (`IM\HMI`, RtIdFiles; H-025) only after PEData.
- Multilinguality (H-028) at the very end.

### Samples
Prefix **`F`** (`samples\F01_…`) so that it does not collide with 100–199 (Unified) and `L…` (library).

### Steps F0–F4
| No | What | Who | Result |
|---|---|---|---|
| F0 | MCP inventory: which Unified object types (`itemCreate`), dynamizations (`itemBindTag`, `itemBindScript`), events (`itemEventCreate`), script modules can be created via TiaMcpXE; list from `TiaHmiUnified(help)`. | Assistant | Object list, gaps in the MCP list (not in the repo) |
| F1 | Test project `F01_hmi_allobjects` (copy of 103_hmi_tag_ext): screens `FP_Basic` (basic objects), `FP_Elements` (elements), `FP_Controls` (controls), `FP_Interface` (a few objects with tag dynamization), `FP_Script` (events with script), `Main` (empty, room for instances). Every object with a unique name, at least one changed property per object. Save, archive, truth = `screen get detail=full` per screen. | Assistant (MCP) | Sample + truth + object list for User |
| F2 | Manual recipe: User builds faceplate types from the screens, creates interfaces, releases 1.0.0, makes 1.0.1, places an instance; after every step **archive the project** (`F02a…F02g.zap21`) so that we have one-change states. | User (by hand) | 6–7 archives |
| F3 | Difference analysis over the archive chain: new types/packages in the metamodel (ILFaceplateTypeData 0x00cd1004, ILFaceplateContainerData 0x00d30006 etc.), type/version objects, storage of the content (live objects or blob), interface, scripts, instance → version, comparison faceplate content ↔ screen items from F01. Truth additionally `typesList`/`typeExport`, as far as TiaMcpXE knows faceplates (MCP-022). | Assistant | docs\spec\hmi-faceplate.md, hypotheses H-F…, LOG |
| F4 | Parser: section Faceplates (in the library part and per instance in the screen), text + JSON, tests against F01/F02*. | Assistant | Code + tests, local commit |

### Rules
- Prio 1+: nothing from jfk-solutions "TiaFileFormat" / TiaProjectBrowser.
- `C:\GitHub\factorybyte\TiaMcpXE` read only. Writing only in this repository.
- Own TIA instance via `TiaInstance(acquire)`, no other tool before that; never touch other instances (approach, Unified 10084, library).
- Python packages only after announcement (package + purpose).
- New MCP problems only in `<shared folder>\TiaMcpXE-backlog.md` on User's machine (the only source, User 2026-10-20 16:58; the MCP developer writes along: re-read, add, write immediately, never keep open, overwrite nothing of others). Do not write to the copy here anymore.
- Format knowledge only in `/mnt/project-files/tia-format/`.

### Status
- 16:05 Plan created, F0/F1 start on User's machine.
- 16:45 **F0/F1 done** (local fde7074): `samples\F01_hmi_allobjects` (+ .zap21, truth `F01_hmi_allobjects_truth\HMI_1\`, object list `F01_hmi_allobjects_objects.md`, diff `F01_hmi_allobjects_diff.txt`, tool `tools\unified_typecount.py`). 7 screens, 47 objects; cannot be created: HmiLabel, HmiProcessControl, HmiComboBox, CustomWebControl/Widget, script module import, events on HmiToggleSwitch (MCP-044/045). Finding H-F01 (dynamizations in the expando). Own TIA instance PID 40268.
- 16:50 **F2 waits for User**: recipe [RECIPE-faceplate.md](RECIPE-faceplate.md), archive chain F02a…F02h. In parallel F3 preparation without TIA (metamodel of the faceplate types).
- 16:44 Note from the library thread: parser commit f3444f4 (metamodel.attribute_sets_ordered = sorted ordinal, hmi.py set names corrected). F4 builds on it.
- 17:52–18:00 User built faceplates directly in F01 (FP_Basic 0.0.1 released, FP_Controls InWork, more followed); TIA crash caused by FP_Flash (MCP-059). Backup `samples\F02_hmi_faceplates_crash` (f5e4f0f), findings in docs\spec\hmi-faceplate.md.
- 18:13 User: Siemens docs primarily via `https://localhost:5112/en-us/home?api=PortalV21`.
- 18:16 TIA docs evaluated (local 73dae3b, 51 functions): docs\spec\faceplate-doc-features.md; from this PLAN A–E and LIST above (20:15).
- 2026-10-21 06:40 User: instance FP_Basic on Main, interface tag HmiTag_Ext1 Bool, FP_Interface 0.0.1 released (one archive `000_empty_20261021_0640.zap21`, copy `samples\F05_…`). Evaluated (ef73eb9, F05.md): H-F02/H-F03 confirmed, MCP-060 solved (`V0.0.1\Name`). Next samples: F16, F18, F12, F26.
- 2026-10-23 F12 (2416777, b6de3e6): codes Real 10, DateTime 13, Array +0x40, HMIUDT; LIST 5 done. Next step F15_local_tag.
- 2026-10-23 F18/F18b (24d1011, dfb9587): interface events complete, instance values of all property types encoded and confirmed (536 truth values). Next step F12_iface_tags.
- 2026-10-23 F16 (User 16:29, 4c67876): all 11 Property Interface types with codes, defaults; new version = copy. 556 truth values.
- 2026-10-22 F40 Siemens demo project (User 16:15, copy gitignored, 1d8bd62): interface properties, UDT tags, instance values/UsedTag, mediator meaning, Suspendable clarified; 464 truth values.
- 2026-10-21 ~11:30 without new samples: items 10 (be949fa), 11 (22fa21e), 18 (3dc4517), 15 (10f2106), 12 partly (71c2b68) worked through, all tests green, 117 truth values. Samples requested from User: F16, F18, F12, F20, F21, F26, F33.
- 2026-10-24 F15 (User 23:58, 50a50b0, ed78f1c): local tag LocalInt = HMI tag in the Default tag table of the hidden device (without interface entry), limits = Limit* attributes on the tag (NoLimit in the sample), binding was missing; ended up in FP_Interface 0.0.7 instead of FP_Basic. LIST 6 [~]. 562 truth values. Next step F20_type_props (optional F15b: limits + binding in FP_Basic).
- 2026-10-31 F20/F30/F33 (User 11:57–12:00, base F15 in the copy F18_iface_event; replaced a first series from F01_hmi_allobjects; d79371d): F20 Suspendable 0x0263, background 0x66, layer "Visible in runtime" off = 0x07 False, display name not stored; F30 container in the type -IsInstanceOf->, version -DependsOn-> (without DependencyIdentifiers), FP_Controls 0.0.1 released in place; F33 column lists 0x3815/0x3832, trend areas 0x3839 (CoreMapArray with blob elements = old codes 0x31d00/0x72/0x76), bound tag in the type = local tag. LIST 11, 15, 18 done. Tests 436 samples, 796 values. Next step optional F15b, then F21/F22–F29.
- 2026-10-31 Completion F3 (User: "all done"): overall picture in analysis\F3_faceplate\SUMMARY.md, short version at the top of docs\spec\hmi-faceplate.md. Done: LIST 1–5, 7, 9–11, 15, 18–20; partly 6 (F15b), 12, 13, 21; open 8 (F17), 14 (F29), 16 (F31), 17 (second project language). All MapT values in the faceplate samples decoded (also F40 0x3e300). Tests 436 samples, 796 values.
