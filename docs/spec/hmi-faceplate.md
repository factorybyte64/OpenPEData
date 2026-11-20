# WinCC Unified faceplates (in work, F3)

Plan: [PLAN-faceplate.md](../PLAN-faceplate.md). Evidence so far: embedded metamodel in sample F01_hmi_allobjects (local fde7074). Generator `tools\fp_metamodel.py`, output `analysis\F3_faceplate\METAMODEL.md` (97 types, 67 AttributeSets, commit f34f5b1) on User's machine. The part on type, version and instance (records, links, values) follows from the archive chain F02a–F02h.

## Summary (as of 2026-10-31)
In detail: `analysis\F3_faceplate\SUMMARY.md`. Evidenced with F01–F40, parser `faceplates.py`, tests 436 samples / 796 values.
- **Type → version → content:** FaceplateLibraryTypeData → FaceplateLibraryVersionData (version encoded as for PLC, State, VersionGuid, Default) → ILFaceplateTypeData (a screen). All objects with Environment → version form the hidden device (fixed core 79 + content-dependent objects). Releasing changes the version in place; “Edit type” creates a complete copy.
- **Interface** on the content (IFaceplateInterface, CoreMapArrays of pe:MapT): tags (tags in their own interface table), properties (11 data types, default = null value), events (parameter type as a name). Data type codes 1 Bool, 3 Int, 5 LInt, 9 ULInt, 10 Real, 11 LReal, 13 DateTime, 14 WString (property in TIA "WString (Configuration string)"), 30 Struct, +0x40 Array, 1282 Color, 65542 Graphic, 65544 Text, 65555 Authorization, 65575 Parameter set, 65585 Resource list. Local tags in the Default tag table.
- **Instance:** ILFaceplateContainerData -IsInstanceOf-> version, values and dynamizations of the interface, UsedTag, event handler `On<Event>`; mediator only for type references from scripts.
- **Content:** screen items as on screens (scope Faceplate.*), 4 permitted controls with column/trend lists as map expandos, layers 0x024b (0x07 = invisible in runtime), Suspendable 0x0263, background 0x66; nested faceplates via IsInstanceOf, version -DependsOn-> inner version.
- **Gaps:** display name not stored, limits of local tags (F15b), event parameters beyond Bool/Int, type contents without TIA truth (only typesList), import of library texts with version increment, F17/F21–F24/F29/F31/F34 not sampled. Addenda 2026-10-31: function list as handler (F18c), AlwaysDownload/popup reference (F36), multilingualism (F32).

## Metamodel (established: type definitions, no objects yet)
| Type | Type ID | Package | inherits from | Essentials |
|---|---|---|---|---|
| FaceplateLibraryTypeData | 0x00d30002 | DL.FaceplateLib | HmiByRefTypeObjectBase | TypeObjectToVersionObject, TypeToDefaultVersion |
| FaceplateLibraryVersionData | 0x00d30003 | DL.FaceplateLib | HmiByRefVersionObjectBase | **VersionToContent 0x00d30004 → ILFaceplateTypeData** (behavesDeep) |
| ILFaceplateContainerData | 0x00d30006 | DL.FaceplateLib | ILScreenItemData + HmiByRefInstanceObjectBase | Relation ILFaceplateContainer_UsedTag 0x00d32006 → HmiCoreTagData; override ICoreAttributes.InstanceOf (constant=false) |
| FaceplateInstanceMediatorData | 0x00d30007 | DL.FaceplateLib | HmiByRefInstanceObjectBase | Mediator instance ↔ type version (role open) |
| ILFaceplateTypeData | 0x00cd1004 | DL.ModernUI | ILScreenData | AttributeSet **IFaceplateInterface 0x00cd3003**: InterfaceEvents, InterfaceProperties, TagInterfaceProperties (each a CoreMapArray); relations ILFaceplateToTagTable 0x00cd2003 → TagTableData, ILFaceplateType_UsedTag 0x00cd2012 |

- The faceplate content is thus a **screen** (ILFaceplateTypeData inherits from ILScreenData). It is attached to the version via VersionToContent, not to the type. Expectation: the screen items in it are normal ILScreenItemData as in screens (see [hmi-screen.md](hmi-screen.md)).
- HMI library types use the **ByRef** base classes from Hmi.Library (Hmi{ByRef,ByCopy}{Type,Version,Instance}ObjectBase, IHmiByRefTypeObjectBaseAttributes.AlwaysDownload), PLC types the ByCopy classes (see [library.md](library.md)).
- Further HMI library kinds with TypeData/VersionData (some with InstanceData/MediatorData): ColorPalette, ImageLib, DynamicSVGLib, ScriptModuleLib, ResourceListLib (text/graphic lists), ScreenModules (HmiScreenModuleTypeObject/Version/Instance, HmiPropertyInterface*), Styles, HmiUdt, GraphX (HmiScreenTypeData, HmiScreenInstanceVersion).

## Faceplate container without type (F01, C_Faceplate1)
Via MCP an ILFaceplateContainerData can be created without a type (Subtype `HmiFaceplateContainer`, record 548 B). Only ICoreAttributes, IPropertyDynamicExpandoAttributes (108 B, 6 values) and IPropertyDynamicLinkExpandoAttributes (136 B) are populated. Links: ILScreenItemParent → screen, Environment → project, Target → HMI device; incoming only ILScreenItems of the screen. No connection to type, version, InstanceOf or mediator.

## First real faceplates (status F02_hmi_faceplates_crash, 2026-10-20 17:55 UTC)
Evidence: User built faceplates by hand in F01; 1:1 copy `samples\F02_hmi_faceplates_crash`, evaluation `analysis\F3_faceplate\CRASH.md`, crash_*.txt, diff_F01_F02crash.txt (local f5e4f0f). `--verify` OK (2478 records, hash chain, signatures, idx). Changed compared to F01 only PEData.plf (3.26 → 5.86 MB), PEData.idx, search index, session files.
- FP_Basic: version **Committed** (text “V 0.0.1“, release 17:49:59 UTC, default version), content = 12 basic objects B_*.
- FP_Controls: version **InWork**, no default version, content C_AlarmControl1, C_Trend1, C_DetParam1, C_Faceplate1.
- FP_Elements: created and discarded (DiscardVersion), not in the file.
- Per version a hidden device `HiddenHmiRTDeviceFaceplateTypes/FaceplateDevice` with cycles, system tags, fonts, styles (context like the shell device for PLC types, see library.md).
- Interface empty in both, no FaceplateInstanceMediatorData, no typed instance.
- Compile error FP_Controls: ValidationMessages contain for C_Faceplate1/ContainedType `ILFaceplateContainerValidator/Error_NonExistingFaceplateType` (typeless container in the faceplate).
- Layers: every screen item carries a layer GUID in IReferenceExpandoAttributes; if it is missing (X_Rect1 from exportXml/importXml), the screen editor crashes (ArgumentNullException in ScreenZOrderService.CanMoveUpOneLayer). For later writing: the layer reference is mandatory.

## Phase A (established, local 58d0fa0, `analysis\F3_faceplate\A123.md`)
- **Content = normal screen items:** The objects in the faceplate are ILScreenItemData and are read with the decoders from [hmi-properties.md](hmi-properties.md)/[hmi-screenitem.md](hmi-screenitem.md). "Create faceplate" moves all objects by −10/−10 (B_Rect1 Left 20 → 10). Faceplate size without unit: FP_Basic 750×300, FP_Controls 810×880.
- **New property IDs:** 08 = layer (value = name of an IReferenceExpandoAttributes entry whose value is the layer GUID), 1f = CaptionColor (ARGB), 7508 = StyleItemClass. 63 of 64 objects have the layer reference; X_Rect1 (importXml) does not → MCP-059 evidenced at file level.
- Only on the faceplate content (ILFaceplateTypeData) is there an expando entry "Properties" (structure with _TypeId/_Guid), presumably the interface part (still empty).
- **IFaceplateInterface:** with an empty interface the set block (slot 9) is missing entirely. Expected format of the CoreMapArrays as in H-L2-1 (u32 length, count, offsets, elements); check with F11/F16/F18.
- **Version:** VersionData `14 00 00 00 | 01 00 00 00 | 00 00 00 00 | 00 00 00 00 | ff ff ff ff` = length 20, build 1, major 0, minor 0, revision −1 → **0.0.1**, same encoding as PLC types (library.md). FP_Basic Committed, FinalizationTime 17:49:59 UTC, default relation present; FP_Controls InWork, FinalizationTime 0001-01-01, no default relation. Author in ICoreAttributes.

## User's status F03 (local 0939120, `samples\F03_hmi_faceplates_user`, truth `…_truth\`)
- 5 faceplate types, all version 0.0.1: FP_Basic, FP_Elements **Committed + Default**; FP_Controls, FP_Interface, FP_Script **InWork**. FP_Elements without alarm indicator (presumably not permitted in the faceplate).
- **Project tag in the faceplate = empty shell:** FP_Interface/FP_Script were created from screens with `HmiTag_Ext1`; in the tag table of the hidden FaceplateDevice there is one tag shell “HmiTag_Ext1“ each without data type (textual reference). Presumably the reason why neither compiles; remedy: create an interface tag of the same name (doc #1: tag references are resolved via interface tags of the same name).
- **Openness/MCP after server update (527 operations):** `typesList` sees faceplate types; `typeExport` delivers only version metadata (author, number, comment), no content; `instancesList` for faceplates fails (MCP-023). `get screen detail=full` works again for all screens (MCP-044 fixed). New: itemCreate containedType, itemBindTag on interface properties, partCreate for Tag/Script/Flashing/Expression/ResourceList/TagParameter.

## Hidden device per version (established, local 8f20efb, `analysis\F3_faceplate\HIDDEN-DEVICE.md`)
- Assignment: all objects with `Environment → FaceplateLibraryVersionData` belong to the version; the version is a separate environment space like a project (analogous to the shell device for PLC types, library.md).
- **Fixed core: 79 objects, the same for all 5 versions:** device “FaceplateDevice“ (DeviceData, DeviceItemData, AddressManager), HMI software “HiddenHmiRTDeviceFaceplateTypes“ with HmiDeviceData “Device“ and settings, folders HmiTagFolder and ILFaceplateTypeFolder, “Default tag table“, 9 system tags, 9 internal system tags, 7 cycles T100ms–T10s, 10 font items, 11 fixed font lists, 3 styles with settings/container, color palette, UMAC/UMC, rights set (9 rights), 2 text categories.
- **Content-dependent:** screen items, containers, font lists `FontList_C<n>` per font used, scripts with ScriptModuleContainer, tag shells. Count overall/screen items: FP_Basic 92/12, FP_Controls 88/3+1 container, FP_Elements 97/13, FP_Interface 88/4, FP_Script 87/2.
- Link tree: version → Parts → device → …; version → VersionToContent → ILFaceplateTypeData → ILScreenItems.

## Parser
`parser\openpedata\faceplates.py` (local 33c04c6; also standalone: `python -m openpedata.faceplates <project> [--json] [--keys]`), key `"faceplates"` in `Model.to_dict()`, section "Faceplates" in the text output; tests `parser\tests\test_faceplates.py` (182 sources, 56 values against typesList, 0 deviations; fixed expectations for F02/F03). Groundwork `analysis\F3_faceplate\faceplates.py` built on `parser\openpedata\library.py` (type, version, state, default, author, release time) and adds per version the content (size, screen items via hmi.py decoders), interface placeholder, tag shells, script count; instances (typed from library.py, typeless containers). Comparison against typesList (F03): 30 values, 0 deviations.

## MapT / CoreMapArray (established, local f5e68f7, `analysis\F3_faceplate\COREMAPARRAY.md`, decoder coremap.py)
- CoreMapArray = array of **pe:MapT**; used only by IFaceplateInterface and ParameterSetTypeData, not evidenced in any sample. pe:MapT itself occurs in 13 further attributes (VendorData 8492 values, ControllerTarget maps, MaxAreaPointerCountPerType, PartnerIdCoreMap, TextIndexMap), checked against those.
- **Envelope:** normal pe blob (attrs.decode_blob); the "24-byte blob header" from L1 is the page header of the blob, kind 1 = zlib.
- **Content:** u32 length, u32 count n, u32 offset of value area, n × u32 type code (bit 31 set), n keys as varint(n+1) strings, then the values. Type codes 0x05/0x13 = i32, 0x0c = GUID, 0x0b/0x0e/0x15 variable. Partly open: TypeFilter (list values), NormalizedUmacMeta (code 0x0e).
- Faceplate: expando "Properties" on ILFaceplateTypeData is a MapT `{_TypeId: 900, _Guid: 637a8242-38db-46bc-a823-9d6479b2123f}`; container expando 51 is also a MapT with the property ID as key. Interface entries expected as arrays of such maps (F11/F16/F18).
- This also partly resolves H-L2-1 (structure/array values with u32 length).

## Instance mechanics (metamodel, before F25; confirmed by F05, see below)
FaceplateInstanceMediatorData and HmiByRefInstanceObjectBase have no relations of their own, only CoreObject relations (IsInstanceOf/IsTypeOf, Environment, Target, Parent) and IsShell; no relation points to them. The instance reference is therefore generic via IsInstanceOf as for PLC types. Open until F25: container → version directly or mediator in between. HmiPropertyInterface* (ScreenModules) is the Classic faceplate model: 0 objects in 97 samples, referenced only by ScreenModules/GraphX.

## Interface and instance (established, F05 = User's status 2026-10-21 06:40, local ef73eb9, `analysis\F3_faceplate\F05.md`)
- **Interface tag** (tab "Tag interface"): entry in `IFaceplateInterface.TagInterfaceProperties` on the faceplate content (ILScreenData of the version), one MapT per tag with name (`HmiTag_Ext1`), data type code (Bool = 1), GUID, reference to the tag and reference to the data type (Bool).
- The tag itself is a complete HMI tag (Bool, cycle T1s) in a **separate tag table of the interface** in the hidden FaceplateDevice of the version; table name = GUID. The earlier tag shell (F03) disappears with this, and so does the compile error. The interface table does not count toward the fixed core (79 core objects remain).
- MapT side finding: the value type "Text" in these lists is not an embedded string but a reference to a text record. With this, all 55 TypeFilter values in all samples are now readable (previously 17/53).
- **Instance** (FP_Basic on Main, not interconnected): `ILFaceplateContainerData` points with `IsInstanceOf` directly to FaceplateLibraryVersionData (FP_Basic 0.0.1); position/size like other screen items (130/73, 750×300). **No mediator object** without interconnection. The type name is not stored in the instance; Openness shows `ContainedType = "V0.0.1\FP_Basic"`.
- Openness: `itemCreate` with `containedType = "V0.0.1\FP_Interface"` creates an instance (MCP-060 solved; the server's help text still mentions "FP_Motor V0.0.1"). Openness delivers the interface as an element list (name), without data type.
- Interface properties and events are read the same way (InterfaceProperties/InterfaceEvents), output still raw until F16/F18.
- Parser: `faceplates.py` outputs per version the interface tags (name, data type, tag, cycle, tag table) and per instance `containedType`; test_faceplates 219 sources, 99 values against TIA truth (F05: 43), 0 deviations.

## Scripts and dynamizations in the type (established, local be949fa, `analysis\F3_faceplate\SCRIPTS.md`)
- Scripts in the type are the same objects as on screens, only in the environment of the version: HmiScriptEmbeddedData on the screen item, above it the HmiScriptModuleContainer of the content (events and triggers linked separately). The scope is called `Faceplate.Event` / `Faceplate.Trigger` (container `Faceplate`) instead of `Screen.*`.
- The global definition area is on the module container (events, triggers, function list). When the faceplate is created, TIA resets it to the default text ("shared between all faceplate container instances"); the screen content is lost.
- Not taken over when copying screen → faceplate: screen event Loaded, screen dynamization BackColor, HmiAlarmLineControl (C_AlarmLine1), HmiAlarmIndicator (E_AlarmInd1).
- Type against source screen: 315 values, 304 equal. Deviations: position −10/−10 (frame normalized to a 10 px margin), `StyleItemClass` stored explicitly in the type, I_Bar1 with an empty assignment instead of none.
- Parser: faceplates.py delivers screen items of the content via Hmi.items (properties, text, tags, dynamizations with code, events with code) and `scriptModule` with the three global areas; check_scripts checks FP_Interface/FP_Script against the Openness truth of the F01 source screens (113 truth values overall).

## Type and version properties (established, local 22fa21e, `analysis\F3_faceplate\TYPE-PROPERTIES.md`)
- Type/version: stored attributes are the same in all 5 types except for name, GUID, times (e.g. IsSetAsNonUpdateable False, OriginalLibrary = project name). Openness delivers only typesList and typeExport (author, version, comment).
- Content: Width/Height (expandos 05/06); layer list expando 0x024b = CoreMapArray with 32 entries Layer_0…31 `{_TypeId 20002, _Guid, Name}` as on every HMI screen, the Layer_0 GUID is the target of all screen items; `_#ActiveLayer` → Layer_0; expando `Properties` = MapT with varint prefix `{_TypeId 900, _Guid 637a8242…}`, the same in all types (class identifier, not a property).
- Content size = instance size (FP_Basic 750×300 = itemGet of the instance).
- Not stored in the default case: Suspendable, display name, background (no expandos; resolves F20).
- Parser: `layers`, `activeLayer`, `properties` on the content, left/top/width/height on instances; decode_map accepts a length prefix. 117 truth values.

## Controls in the type (established, local 3dc4517, `analysis\F3_faceplate\CONTROLS.md`)
- Of 15 controls of the screen FP_Controls, 4 remain in the type: alarm control, trend control, parameter set control (HmiDetailedParameterControl), faceplate container; this matches the TIA help, TIA silently discards the other 11 (likewise AlarmIndicator in FP_Elements, AlarmLine).
- Storage: same object kind as in the screen, but in the type with explicit sub-objects as map expandos (in the screen default values): 0x0051 Caption `{_TypeId 40009, Font}` (= Openness Caption.Font); 0x021c Font `{_TypeId 20034, 0x054e → FontList}`, reference via ExpandoReferences to the `FontList_C<n>` objects of the hidden device (whose purpose is thereby clarified); column lists (alarm control ID 40, Name 80, Alarm class 100 …; parameter set Name 270, Value 120, Unit 180); toolbar button lists and header texts as separate texts.
- New MapT type codes: 0x01 bool, 0x15 nested map, 0xcd4001 CoreMapArray, length prefix before expando maps. Open: 0x31d00 (trend), 0x72/0x76 (lists in ListBox, Radio, CheckBox).
- Openness `get detail=full` does not deliver column lists; truth only with F33.
- Parser: `parts` per screen item (all map expandos that hmi.py does not name, font lists resolved). Tests green, 117 truth values.

## Dependencies and empty container (established, local 10f2106, `analysis\F3_faceplate\DEPENDENCIES.md`)
- No faceplate version in F03/F05 has dependencies (DependsOn, usedBy, DependencyIdentifiers empty), because no type contains a typed faceplate; whether faceplates get dependencies like PLC types will only be shown by F30.
- Empty container C_Faceplate1: ILFaceplateContainerData **without IsInstanceOf**, in the screen as in the type. The type reference exists only as this link; there is no ContainedType attribute (Openness also shows none for this container). Cause of Error_NonExistingFaceplateType in FP_Controls.
- pe:MapT in general: type codes are expando codes (bool, byte, uint, int, long, float, double, dateTime, string, GUID, blob, xml, nullable) or metamodel type IDs (0x66002 = DependencyIdentifierValue, 0xcd4001 = CoreMapArray); variable values as an offset into the value area. All 108 NormalizedUmacMeta decode structurally. Open: 0x72, 0x76, 0x31d00.

## Release (established, local 71c2b68)
- Releasing changes the FaceplateLibraryVersionData in place: status, release time, **new VersionGuid**, default link. Unlike PLC types, no copy is created.

## Practice sample F40: Siemens DemoProject_BestPractices_V4 (evidence: Siemens demo project, not in the repo; sample, truth and evaluation only local)
- 13 types, 14 versions, 14 instances; type folders; one type with two Committed versions (0.0.6, default 0.0.7) read correctly; no interface events in the project.
- **Interface properties** (InterfaceProperties): MapT `{Name, DataType, _Guid, _TypeId, Default}`. DataType codes: 14 WString, 1282 color (default structure 0x1600d, 8 bytes: type 8 + ARGB, output 0xAARRGGBB), 65544 multilingual text, 65585 text list, 65555 presumably user rights.
- **Interface tags**: codes 1 Bool, 3 Int, 14 WString, 30 Struct; Struct = PLC UDT with `_SelectedStructureType PLCUDT`, `_UDTStructure`, `_UDTStructureInLib` → library version of the UDT (UDT_Motor 0.0.1, typeHeatControl 0.0.1).
- **Instance values** (all confirmed against TIA): hard-wired tags incl. structure elements, static texts/colors, text lists, multilingual texts, dynamizations of the interface values (tag with range, text list, TagParameter with prefix), **UsedTag** = list of all tags used per instance.
- **Mediator** (FaceplateInstanceMediatorData) is not part of a screen instance, but the typed reference of a script to a faceplate type (popup opening in OnTapped). DynamicSVGInstanceMediatorData analogously. H-F03 thereby refined.
- **Suspendable** = Bool expando 0x0263 on the faceplate content (LIST 11 supplemented).
- Colors in expando slots 0x214b/0x0066/0x243e (controls) also 8-byte color. Open: code 0x3e300 in the trend control.
- Hidden device: no fixed core of 79; additionally content-dependent are UDT images, graphic list/text list instances, function lists, mediators; remainder 77–88 because of themes, styles, fonts; the demo project has 8 instead of 9 system tags.
- Tests: test_faceplates 226 sources, 464 truth values (F40: 347). test_library red for F40 because of a gap in library.py (OPENNESS_TYPE_KIND does not know TextList/Image/DynamicSVGLibraryTypeData; library thread).

## Interface properties, all 11 types (established, F16, local 4c67876, `analysis\F3_faceplate\F16.md`)
- DataType codes (same enumeration as for interface tags): Bool 1, Int 3, LInt 5, ULInt 9, LReal 11, WString 14, Struct 30, Color 1282, Graphic 65542, Multilingual text 65544, Authorization 65555, Parameter set type 65575, Resource list 65585.
- Default values in the map: False, 0, 0.0, "", color 0xFF0000FF (confirmed against TIA). Reference types (graphic, text, rights, parameter set, text list) without a stored default. Properties do not create objects of their own, only list entries on the content.
- **“Edit type” / new version** (LIST 12): every new version is a complete copy with its own content, its own hidden device and its own VersionGuid (FP_Interface 0.0.1/0.0.2/0.0.3, default 0.0.3). The release itself changes the version in place (71c2b68).
- There are no editable default values in the Property Interface (User 2026-10-23); the stored default is the null value of the type. Values other than the default only arise on the instance (F40).
- Tests: test_faceplates 229 sources, 556 truth values; test_library now also green for F40.

## Interface events (established, F18 part A, local 24d1011, `analysis\F3_faceplate\F18.md`)
- `InterfaceEvents` on the faceplate content contains per event the name and parameter list (EvTest: Nr, Flag). The parameter data type is stored **as a name** ("Bool"), not as a code as for tags/properties; **Int is not stored** (default type). An event does not create objects of its own.
- Raising in the type: normal Tapped script on the button with `Faceplate.RaiseEvent("EvTest", {Nr: 5, Flag: true})`, as HmiScriptEmbeddedData like other scripts. Intermediate version 0.0.4 with a different script visible (versions are copies).
- Parser: `interface event EvTest(Nr: Int, Flag: Bool)`. test_faceplates 232 sources.

## Event handling and instance values (established, F18b, local dfb9587, `analysis\F3_faceplate\F18.md`)
- Handler on the instance: script "OnEvTest" on the container, stored like a normal screen item event (script container on the screen). Openness shows the script but cannot name the event type for interface events ("None").
- Instance values per data type (all confirmed against TIA): Bool True; LInt 42 (i64); ULInt 89 (u64); LReal 3.5 (double); Color 0xFFFF0000; WString “abc“; Multilingual text = reference to a text record (“Hallo“); Graphic and Resource list = references to a graphic or text list.
- Interface events are thus complete: definition in the type (F18), raising via RaiseEvent, handler on the instance (F18b). LIST 9 and 13 (values) done.
- Tests: 234 sources, 536 truth values.

## Interface tags, data types complete (established, F12, local 2416777, `analysis\F3_faceplate\F12.md`)
- Data type codes (common enumeration for tags and properties): 1 Bool, 3 Int, 5 LInt, 9 ULInt, 10 Real, 11 LReal, 13 DateTime, 14 WString, 30 Struct (PLC UDT), **Array = element code + 0x40** (Array[0..3] of Int = 67) with bounds "0:3" and one tag per element in the hidden device; properties additionally 1282 Color and the reference types 65542/65544/65555/65575/65585.
- HMI UDT: `HMIUDT` with reference to the library version of the UDT (User_data_type_1 0.0.1), element tags in the hidden device; like PLC UDTs.
- In the Tag Interface, TIA V21 does not offer WString (User 2026-10-23); the demo project F40 nevertheless has a WString interface tag (code 14), presumably created differently.
- The fixed core of the hidden device remains 79 (element tags are content-dependent). Parser: `T_Array Array[0..3] of Int`. Tests: 236 sources, 556 truth values; test_library allows HMI UDT library types (b6de3e6).

## Local tags (established, F15, local 50a50b0, `analysis\F3_faceplate\F15.md`)
- Local tag = `HmiTagData` in the hidden device of the version in the **Default tag table** (`TagTable` → table, there `Content`); no entry in `TagInterfaceProperties`, no link from the content. Interface tags, by contrast, are in the GUID-named interface table (`ILFaceplateToTagTable`).
- Limits of an HMI tag are attributes on the tag (`IHmiTagStructureAttributes.Limit{Low,LowHigh,HighLow,High}Type` + `…Constant`), not set in the sample (NoLimit); LocalInt was not bound to any screen item.
- User's F15 concerns FP_Interface 0.0.7 (not FP_Basic); new version = complete copy (103 objects). Local tags count as content-dependent, the fixed core remains 79. Parser: `localTags` per version. Tests: 238 sources, 562 truth values.

## Properties of the faceplate type (established, F20, local d79371d, `analysis\F3_faceplate\F20.md`)
- On the content (`ILFaceplateTypeData`) only when deviating from the default: **Suspendable** = Bool expando 0x0263; **background color** = expando 0x66 BackColor as on screens (u32 ARGB, e.g. 0xFFFFFF99).
- **Layer "Visible in runtime" off** = field **0x07 = False** in the entry of the layer list 0x024b (missing for visible layers); layer name in field 0x03 (renamed “Hilfsebene“). Screen items refer via their Layer property to the GUID of the layer.
- The **display name** of the type is not stored anywhere (no attribute, no expando, no text object); open whether TIA takes it over.
- New version via “Edit type” = complete copy (as in F16). Parser: `backColor`, `suspendable`, `hiddenLayers` on the content.

## Faceplate in faceplate (established, F30, local d79371d, `analysis\F3_faceplate\F30.md`)
- Typed container in the type content: `ILFaceplateContainerData` **-IsInstanceOf->** version of the inner type (reverse direction IsTypeOf), as for instances on screens; no ContainedType attribute, no mediator.
- Dependency: outer version **-DependsOn->** inner version (reverse direction DependsOnInverse), **without** `IDependencyInfo.DependencyIdentifiers` (unlike PLC types). Faceplate versions with an HMI UDT interface tag likewise depend on the UDT version (FP_Interface 0.0.6/0.0.7).
- An InWork version that previously could not be compiled (empty container) is released in place after the type is set (new VersionGuid, no copy).
- Parser: `containedType` on the container in the content, `dependsOn`/`usedBy` per version.

## Columns and trends of the controls in the type (established, F33, local d79371d, `analysis\F3_faceplate\F33.md`)
- Alarm control: column list of the alarm view **0x3815** (`0x0011` = CoreMapArray of the columns `{0x03 name, 0x07 visible, 0x05 width, 0x0523 column ID, 0x3811 header text}`, _TypeId 40016); column list of the alarm statistics **0x3832** (column ID 0x3833, _TypeId 40017). Hide = 0x07 False, width = 0x05.
- Trend control: trend areas **0x3839** = CoreMapArray whose elements are **pe blobs with a MapT**; per area name, value axes 0x3855, time axes 0x3858, **trends 0x2150** (axes via GUID 0x385a/0x382b, **data source 0x0440** `{_TypeId 33307, 0x0b1f → tag}`).
- TIA creates a tag bound in the type as a **local tag** of the hidden device (HmiTag_Ext1 as Int in the Default tag table), independently of the HMI tag of the same name.
- The previously open MapT "codes" 0x31d00, 0x72, 0x76 were blob headers of such arrays (also 0x0934 SelectionItems on ListBox/Radio/CheckBox). No MapT value in the faceplate samples remains undecoded. Tests: 436 samples, 796 values.

## Event handlers via function list (established, F18c, `analysis\F3_faceplate\F18c.md`)
- Function list on the container = `HmiFunctionListEmbeddedData` (0x00e61003) instead of `HmiScriptEmbeddedData`, same script AttributeSets; code = JavaScript generated by TIA, one line `HMIRuntime.<Area>.SysFct.<Function>(...)` per function, IsAsync True. Linkage container -EmbeddedScriptChildren-> as for the script, on the script module container via `FunctionListElements`/`FunctionListModule` instead of `EventElements`/`EventModule`. Openness: same ScriptCode, global area `// [FunctionListModule]`.
- Parser: instance events with `handler` (functionList/script), `async`, `functions`.

## AlwaysDownload and popup reference (established, F36, `analysis\F3_faceplate\F36.md`)
- "Mark as AlwaysDownload" = `IHmiByRefTypeObjectBaseAttributes.AlwaysDownload` True on the **type**, no new version.
- `UI.OpenFaceplateInPopup` in a screen script: the type appears in the code as a placeholder; its `IScriptLinkExpandoAttributes` entry has bit 0x80 and points via **ExpandoChildren** to a `FaceplateInstanceMediatorData` -IsInstanceOf-> version (here FP_Basic 0.0.1). Openness shows the placeholder as `FP_Basic_V_0_0_1` (fixed version, no DefaultName in the sample).
- Parser: `referencedBy` per version with screen, script, object, placeholder and Openness name.

## Multilingual texts in the type (established, F32, `analysis\F3_faceplate\F32.md`)
- Texts of screen items in the faceplate content are stored as on screens as a multilingual text value (one entry per language/culture) on the screen item; no separate text objects. New version via “Edit type” = copy with the new texts.
- Languages of the version: `ReferenceCulture`, `EditingCulture`, `UsedCultures` → CoreCulture (e.g. de-DE(1031)); changing the reference language changes it on the project, project library and version.
- Runtime languages of HMI_1: `OnlineLanguageOrder` (“en-US;de-DE“) and `LoggingLanguages` (LCIDs). The Excel export "Export library texts" (default version, one column per language, reference language marked with *) matches the file.
- Parser: `cultures` per version; texts per language via hmi.py.

## Open (F3)
See the summary above and `analysis\F3_faceplate\SUMMARY.md` section 7.
