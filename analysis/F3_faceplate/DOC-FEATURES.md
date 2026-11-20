# F3: faceplate functions according to the TIA docs, storage location in the file format, plan and work list

As of 2026-10-20. Goal: reliably recognize and read every faceplate function of WinCC Unified (TIA V21) in the file format (PEData.plf), and later also write it.

**Sources.** Locally installed TIA V21 help (`C:\Program Files\Siemens\Automation\Portal V21\Help\en-US`), packages:
- **[S]** `ScreensWCCUenUS.zip`, chapter "Configuring faceplates (RT Unified)" (81 pages)
- **[O]** `OpennessWCCUenUS.zip`, chapter "Faceplate Container"
- **[T]** `TIAPortalOpennessenUS.zip`, "Exporting/Importing a screen with a faceplate instance" (Classic HmiTarget, for comparison)
- **[B]** `BasicsWCCUenUS.zip`, "Generating a faceplate as a type"
- **[R]** `ObjectModelWCCUenUS.zip`, runtime object model: FaceplateContainer, FaceplateType, UI.OpenFaceplateInPopup, Resources.FaceplateTypes

Since 2026-10-20 primarily via the local help API `https://localhost:5112/en-us/home?api=PortalV21` (access: `docs\TIA-HELP-API.md`). Checked: the 81 faceplate pages are textually identical via API and ZIP. Additionally read via the API search: **[BU]** `BasicsWCCUenUS` "Using type in type", "Always load default version"; **[TG]** `TagsWCCUenUS` "Basics of user data types", "Managing versions of user data types"; **[R]** FaceplateType (description, Properties, RaiseEvent, Suspendable), FaceplateContainer.Properties, FaceplateContainer_OnInterfaceEvent, UI.OpenFaceplateInPopup, FaceplateTypeName.DefaultName.

In addition there are the metamodel (`METAMODEL.md`, from F01) and the findings from `samples\F02_hmi_faceplates_crash` (`CRASH.md`, `crash_*.txt`). Page titles are in quotation marks; the docs are summarized in my own words. The Siemens document 109812366 (tips and tricks) is not available locally; the download is still pending (needs the user's direct approval).

**Legend.**
- Storage location: **confirmed** = seen in sample F02; **conjectured** = derived from the metamodel, not yet checked.
- Testable: **H** = by hand in TIA, **M** = via TiaMcpXE (Openness), **–** = not testable (device/version).

## 1. Inventory of storage locations (metamodel, short version)

| Object | Type (ID) | Content | Status |
| --- | --- | --- | --- |
| Faceplate type | `FaceplateLibraryTypeData` (0x00d30002), base `HmiByRefTypeObjectBase` | Name, TypeGuid, IsObsolete, IsSetAsNonUpdateable, MinimumTargetDeviceVersion, TypeNamespace, AlwaysDownload; relations `TypeObjectToVersionObject`, `TypeToDefaultVersion`, folder `AggregatingFolder` | confirmed (F02) |
| Faceplate version | `FaceplateLibraryVersionData` (0x00d30003) | State (InWork/Committed), Version (VersionData blob), VersionGuid, FinalizationTime, MinimumTargetDeviceVersion, Author, Comment, LastModifiedBy, DependencyIdentifiers (IDependencyInfo); relations `VersionToContent` → content, `Parts` → hidden device, fonts, styles, `RelatedPackage` (SW.HMI.FaceplateVersion_21.0.1.0) | confirmed |
| Faceplate content | `ILFaceplateTypeData` (0x00cd1004), base `ILScreenData` | like a screen: screen items via `ILScreenItems`; additionally AttributeSet `IFaceplateInterface` with InterfaceEvents, InterfaceProperties, TagInterfaceProperties (each `CoreMapArray`); relations `ILFaceplateToTagTable` → TagTableData, `ILFaceplateType_UsedTag` → HmiCoreTagData | confirmed (interface still empty) |
| Hidden device per version | HmiApplicationData `HiddenHmiRTDeviceFaceplateTypes`, DeviceData/DeviceItemData `FaceplateDevice`, AddressManager, cycles, system tags, fonts, styles, color palette, HmiTagTableData, folders `HmiTagFolder`, `ILFaceplateTypeFolder` | runtime environment of the type; likely home of the interface and local tags | confirmed (table still empty) |
| Faceplate instance | `ILFaceplateContainerData` (0x00d30006), base `ILScreenItemData` + `HmiByRefInstanceObjectBase` | screen item (subtype HmiFaceplateContainer); relation `ILFaceplateContainer_UsedTag`; override `ICoreAttributes.InstanceOf` (constant=false) = likely reference to the version (ContainedType) | confirmed (typeless only) |
| Mediator | `FaceplateInstanceMediatorData` (0x00d30007) | instance object of the library, purpose unknown (likely instance ↔ version in a device) | conjectured |
| Event/property interface (faceplate model, older) | `HmiPropertyInterface*` (ScreenModules, 0x00b71003–08) | categories, properties (Type, PropertyDirection), connections to inner objects (InnerObjectPropertyName) | likely Classic/Comfort only; unchecked in Unified |
| HMI data type | `HmiUdtTypeData`/`HmiUdtVersionData` (library), `HmiStructureType*` | for interface tags of type HMIUDT | conjectured |
| Property values of all screen items | `IPropertyExpandoAttributes`, `IReferenceExpandoAttributes`, `IPropertyDynamic*`, `IEventExpandoAttributes` (expandos) | values, layer, dynamizations, events; not yet decoded by the parser | confirmed, format open |
| Scripts | `HmiScriptEmbeddedData`, `HmiScriptModuleContainerData` (DL.Scripting) | one object per script, one container per screen | confirmed (F01) |

### 1a. HmiPropertyInterface* (list item 20, clarified)

- In all 97 sample folders (000–126, L*, F*) there is **no** object of the types `HmiPropertyInterface*` (ScreenModules 0x00b71003–08) and no `FaceplateInstanceMediatorData`.
- In the metamodel, the `HmiPropertyInterface*` types are only referenced from the package ScreenModules (`HmiScreenModuleVersionData.IHmiScreenModuleTypeAttributes_PropertyInterface2/EventInterface2`, `HmiScreenModuleTagData.IHmiScreenModuleTagAttributes_RelatedProperty`) and from Classic GraphX (`HmiScreenItemData`), never from DL.ModernUI or DL.FaceplateLib.
- Conclusion: this is the interface model of the Classic "Bildbausteine" (faceplates, Comfort/Advanced). Unified faceplates store their interface in `IFaceplateInterface` (CoreMapArray, see `COREMAPARRAY.md`). Item 20 of the work list is thus done.

## 2. Functions according to the docs

### 2.1 Type, version, library

| # | Function | Docs say (source) | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 1 | Create type (library or "Create faceplate" from screen items) | New type with version 0.0.1 "In progress"; name max. 128 characters, unique, without `# $ +% . / : [ ] ' ~ " \``; device type (Unified/Advanced) cannot be changed later. When creating from a screen, graphic references, function lists and Dynamic SVG are not taken over; tag references resolve via interface tags of the same name. [S] "Creating a faceplate type in the project library", "Creating a faceplate type from a screen", [B] | TypeData + VersionData + ILFaceplateTypeData + hidden device (confirmed) | H |
| 2 | Lowest device version | Set on creation, later only via "Upgrade device version" (creates a new version). The table "Available functions depending on the device version" assigns a minimum version to each function (16.0 to 21.0). [S] "Lowest device version of a faceplate type" | `MinimumTargetDeviceVersion` in type and version (confirmed: '' and '21.0.1.0' respectively), `RelatedPackage` | H |
| 3 | Versions, state, release | Editing creates a new version "In progress"; release with name, version number (major and minor number), author, comment; options: update instances, delete unused versions, set dependent types to in work; automatic consistency check. Only released versions can be instantiated. [S] "Releasing a faceplate version of a type", "Editing a faceplate type" | `State`, `Version` (blob, encoding open), `FinalizationTime`, Author/Comment (confirmed) | H, M (TiaProjectLibrary typeVersionRelease only tested for PLC) |
| 4 | Default version | The highest released version becomes "Default", can be changed manually; dragging the type into the screen takes the default version. [S] "Defining a version as the "default" version" | relation `TypeToDefaultVersion`/`DefaultVersionToType` (confirmed) | H |
| 5 | Assign version number (several types) | Creates new versions with a common number; log under "Common data > Logs". [S] "Assigning a faceplate version number" | new VersionData; log object (conjectured) | H |
| 6 | Duplicate | Copy in the same folder, released version, no instance. [S] "Duplicating a faceplate type" | new TypeData with new TypeGuid; possibly `OriginalTypeGuid` (conjectured) | H |
| 7 | Rename | References in scripts are renamed automatically. [S] "Renaming a faceplate type" | `ICoreAttributes.Name` on the type; script texts (conjectured) | H |
| 8 | Delete type/version | Only without dependencies; deleting a version deletes its instances; script references remain empty. [S] "Deleting a faceplate type or a version" | tombstones | H |
| 9 | Update, replace | Switch all instances to a new version or a different type; single instance via "Faceplate type" on the container. [S] "Updating a faceplate type", "Replacing a faceplate type" | instance reference on the container (conjectured `InstanceOf`) | H |
| 10 | "Do not use" | Marking prevents instantiation; existing instances give warnings. [S] "Use the marking "Do not use"…" | `IsObsolete` or `IsSetAsNonUpdateable` (conjectured) | H |
| 11 | Consistency, fix inconsistency | Status column; "Adapt inconsistent type" or set default. [S] "Checking the consistency…", "Checking the version consistency…" | derived from default relations and `DependencyIdentifiers` (conjectured) | H |
| 12 | Folders in the library | Types can be in folders; Classic export calls them "<folder>@$@<type> V x.y.z" (German UI: folder and type name). [T] | `AggregatingFolder` → FolderData (confirmed: LibraryTypeSystemFolder) | H |
| 13 | Copying into other projects, global library | Order: PLC UDT, type, instances; a name conflict results in "Name_1". [S] "Copying faceplate types and faceplate instances…" | same objects, `OriginalLibrary` (confirmed: '000_empty') | H |
| 14 | Open read-only, toolbar | Changes no data. [S] | – | – |

### 2.2 Visualization (content of the type)

| # | Function | Docs say | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 15 | Screen items in the type | Basic objects, elements; controls only alarm control, trend control, faceplate container, parameter set control. [S] ""Faceplate types" editor" | `ILScreenItems` of the ILFaceplateTypeData (confirmed: 12 and 4 objects respectively) | H |
| 16 | Properties of the type (like screen) | Display name, size, background; "Resize screen to content". [S] "Editing the visualization…", [R] FaceplateType.* (BackColor, DisplayName, Width, Height, Layers, Suspendable, EnableExplicitUnlock …) | expandos of the ILFaceplateTypeData (confirmed: only Core, Property, Reference) | H |
| 17 | Layers | As in the screen; visibility in runtime separate ("Runtime visible"). [S] | `_#ActiveLayer` and layer GUID in the expandos (confirmed on the screen, see MCP-059) | H |
| 18 | Suspendable | Type property from 19.0; suspends scripts of invisible instances. [S] "Performance improvement…" | expando on the ILFaceplateTypeData (conjectured) | H |
| 19 | Multilingual texts | Texts per project language; option "language-compatible fonts for faceplates". [S] "Configuring multilingualism…" | `ITextAttributes`/HmiTextData (conjectured), fonts of the version (confirmed) | H (2nd language by hand only) |
| 20 | Style preview | Preview only, runtime uses the device style. [S] | style objects of the version (confirmed: ILSystemStyleContainer) | H |
| 21 | Dynamic SVG, graphic list types, color palettes | from 18.0 / 21.0 / 20.0. [S] device version table | library types DynamicSVGLib, ResourceListLib, ColorPalette (instance objects `*InstanceData`) | H |
| 22 | Controls in the type | Alarm control, trend control from 18.0; trend with logging tags via configuration string "HMI-Tag:LoggingTag". [S] example "trend control with logging tags" | screen items + script (confirmed: C_AlarmControl1, C_Trend1) | H |

### 2.3 Tags

| # | Function | Docs say | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 23 | Interface tags (tag interface) | Standard interface to the controller; connected to project tags only via the container; name ≤128, starts with letter/underscore, no special characters; order can be changed; renaming resets connected values. [S] "Configuring interface tags in the faceplate type" | `TagInterfaceProperties` (CoreMapArray) + HMI tags in the table of the hidden device, `ILFaceplateToTagTable` (conjectured) | H |
| 24 | Data types | Simple types, arrays (index from 0, upper bound), LReal (bindable to any simple type, with exceptions), DateTime/LTime (mapping to S7-1500 types), PLCUDT, HMIUDT; no arrays of WChar/WString/HMIUDT. [S] "Overview" (Tags) | tag data type (`IHmiTagStructureAttributes_HmiDataType`, `IStructureItem`) | H |
| 25 | PLC data type (PLCUDT) | Link the version of the library type; nested up to 26 levels, arrays with constants; version changes are not followed automatically. [S] "Using a PLC user data type", "Defining arrays … through constants" | reference to PLC type version (`DependencyIdentifiers`, PlcStruct library type) (conjectured) | H |
| 26 | HMI data type (HMIUDT) | From the library. [S] "Using an HMI user data type" | HmiUdtVersionData reference (conjectured) | H |
| 27 | Local tags | Only visible in the type, not on the instance; from 21.0 with limits (not for Array, Bool, Byte, Char, DateTime, Word types, WString); also WString. [S] "Configuring local tags…" | tags in the table of the hidden device (conjectured), limits as `Thresholds` | H |
| 28 | OPC UA data types | Mapping OPC UA → faceplate types (Int32→DInt etc.). [S] "Connecting faceplate types to OPC UA" | as 24 | – (OPC UA not in the test project) |
| 29 | Unique names | A name may occur only once across all tabs of a type. [S] editor | check rule | H |

### 2.4 Property interface

| # | Function | Docs say | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 30 | Interface properties | Data types: 64-bit integer, Authorization (read-only), Boolean, Color, Floating-point, Graphic, Configuration string, Multilingual text, Parameter set type, Resource list, Unsigned 64-bit integer; minimum versions 16.0 to 21.0. [S] "Overview" (Interface properties) | `InterfaceProperties` (CoreMapArray) on the ILFaceplateTypeData (conjectured) | H |
| 31 | Dynamization "Property interface" in the type | Object property to interface property; optional formula (operators, conversions color/length/mass/…) and evaluation type (None, Range, Multiple bits, Single bit) with limits (32/64 bit, 15 decimal places). [S] "Configure interface property" | dynamization in the expando of the inner object (type "PropertyInterface", not in Openness `DynamizationType`) (conjectured) | H |
| 32 | Value on the instance | Static value (several values with `;`) or dynamization Tag, Script, Flashing, Resource list. [S] | expando/interface part of the container (`Interface` composition in Openness) (conjectured) | H, M (itemBindTag Interface implemented per MCP-053) |
| 33 | Configuration string in scripts | `Faceplate.Properties.<Name>`; values via script; popup data. [S] "Defining a configuration string…" | script text (HmiScriptEmbeddedData) | H |

### 2.5 Event interface

| # | Function | Docs say | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 34 | Interface events with parameters | Event with any number of parameters; 24 parameter data types (Bool … Word, Color, DateTime, HmiEventTrigger, HmiGesture, HmiKeyboardModifier, LString). Raised in the type via `Faceplate.RaiseEvent(name, {param:value})`. [S] "Configuring an interface event…", "Example: Configuring and using an interface event" | `InterfaceEvents` (CoreMapArray) (conjectured) | H |
| 35 | Handling on the instance | Function list or script; function name `<Container>_On<Event>(item, <Parameter…>)`; renames are followed in the script header. [S] | `EventHandlers`/`IEventExpandoAttributes` of the container, script object (conjectured) | H |

### 2.6 Dynamization and scripts in the type

| # | Function | Docs say | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 36 | Dynamization kinds in the type | Tag, Script, Resource list, Expression, Property interface, Flashing (Flashing not supported in runtime); only tags of the type. [S] "Basics for the dynamization of faceplates", "Dynamizing a faceplate type" | `IPropertyDynamicExpandoAttributes`/`…LinkExpando…` of the inner object (confirmed for screens) | H |
| 37 | Events in the type | Function list (restricted: CreateOperatorInputInformation, CreateSystemInformation, CreateSystemAlarm, LookUpText, Update/Increase/Decrease/SetTagValue, bit functions, ShiftAndMask, ChangeDBName) or script; event "Cleared" without tag access. [S] | EventHandler expandos, script objects | H |
| 38 | Global definition area | Applies to all instances (shared values). [S] | `GlobalDefinitionAreaScriptCode` or ScriptModuleContainer of the type (conjectured) | H |
| 39 | Overview of the dynamizations per version | View only. [S] "Showing the overview of the dynamizations in faceplate versions" | – | – |

### 2.7 Instance (faceplate container)

| # | Function | Docs say | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 40 | Create instance | Drag type/version into the screen or create an empty container and select "Faceplate type"; only released versions. [S] "Creating a faceplate instance"; Openness: `ContainedType` as a string, e.g. "Faceplate_1_V_0_0_1". [O] | `InstanceOf`/reference to FaceplateLibraryVersionData, possibly `FaceplateInstanceMediatorData`; empty container confirmed (F01) | H, M (itemCreate with containedType implemented per SOURCE-OF-TRUTH) |
| 41 | Container properties | Window flags (border, title, movable), zoom 0.01–8, size fit (None, Fit window to screen, Fit screen to window), scroll bars (default "Collapsed"), icon can be dynamized. [S] "Faceplate container", [O] property table, [R] FaceplateContainer.* | expandos of the ILFaceplateContainerData | H, M |
| 42 | Connect interface tags | Statically with project tag/UDT instance; tag parameter (dynamic tag name from static parts and tag references, numeric/WString only). [S] "Dynamizing an object property using a tag parameter", example "dynamic tag as an interface property" | `ILFaceplateContainer_UsedTag` + interface values in the expando; TagParameterDynamization (conjectured) | H, M (partially) |
| 43 | Dynamization of the instance | Tag, Script, Expression, Resource list, Tag parameter, Property interface, Flashing. [S] "Dynamizing a faceplate instance" | expandos of the container | H |
| 44 | Events of the instance | Activated/Deactivated (border and title required), interface events. [S], [R] FaceplateContainer_On* | EventHandler expandos | H |
| 45 | Change version of an instance | Via "Faceplate type" on the container. [S] | instance reference | H |
| 46 | Access from the type to the container | `Faceplate.Parent.<property>`, `Faceplate.Parent.Properties.<Name>.Tag`. [S] "Accessing properties of the faceplate container with a script" | script text | H |

### 2.8 Nesting and popups

| # | Function | Docs say | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 47 | Faceplate inside a faceplate | From 18.0; bind inner interface tags/properties to outer ones (not local tags); no other version of the same type; device version outer ≥ inner; versioning of the inner type affects the outer one. [S] "Using a faceplate type in another faceplate type" | container in the ILFaceplateTypeData (confirmed, typeless), `DependencyIdentifiers` of the outer version (conjectured) | H |
| 48 | Popup | `UI.OpenFaceplateInPopup("Type_V_x_y_z", title, data, screen, invisible)` outside, `Faceplate.OpenFaceplateInPopup(...)` inside; only versions used in the device; multilingual texts cannot be passed from outside; text lists with "@Default.". [S] "Configure faceplate as pop-up", [R] UI.OpenFaceplateInPopup | script text; use of the version in the device (reference from the script, conjectured `CodeRelations`) | H |
| 49 | Default version in scripts | From 20.0.0.2 via script method. [S], [R] Resources.FaceplateTypes | script text | H |

### 2.9 Openness and export

| # | Function | Docs say | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 50 | Openness Unified | Container only: Create/Delete, properties (Name, ContainedType, Enabled, size, Icon, Caption, TabIndex, Visible, WindowFlags, Interface, EventHandlers, ShowFocusVisual, RotationAngle, InteractiveZooming/Panning), assign UDT to interface. Types can neither be created nor exported. [O] | – | M |
| 51 | Classic export (comparison) | Instance export with FaceplateTypeName "Typ V 0.0.2" (type V 0.0.2) and folder path "@$@", interface attributes, events. Applies to HmiTarget, not Unified. [T] | – | – |

### 2.10 Additions from the API search

| # | Function | Docs say | Storage location | Testable |
| --- | --- | --- | --- | --- |
| 52 | "Always download" (user tag) | For faceplates, graphics and text lists: the default version is always compiled and downloaded, even if it is not directly referenced; needed for `HMIRuntime.Resources.FaceplateTypes("X").DefaultName`; Unified only, from V20 Update 2; duplicates do not inherit the tag; cross-references are lost in the process. [BU] "Always load default version" | `IHmiByRefTypeObjectBaseAttributes.AlwaysDownload` on FaceplateLibraryTypeData (metamodel, name matches) | H |
| 53 | Compatible/incompatible changes with type in type | Incompatible (new default version of the referencing type): device version, all interface changes (tags, properties, events), changed assignment of interface tags, size of the visualization and of screen items, text/graphic lists, process value properties, script module signatures and parameter count. Compatible: new screen items, texts/graphics/languages/comments, parameter names, script changes. Release option "Set dependent types to editing mode". [BU] "Using type in type" | versions and `DependencyIdentifiers` of the referencing version (conjectured) | H |
| 54 | Interface access at runtime | `Faceplate.Properties` and `Faceplate.Parent.Properties` are `HmiDynamicPropertyPart`; interface tags only have `.Tag` (name of the connected project tag, writable with the same data type); Authorization read-only. [R] FaceplateType.Properties, FaceplateContainer.Properties | interface values on the container (conjectured) | H |
| 55 | Interface data in the popup | Literal object: `{Tag_1:{Tag:"HMI_Tag"}, Farbe:0xff00ff00, ResourceList:"@Default.Text_list_1", GraphicType:"GraphicCollection.Graphic_Type_1_V_0_0_1", TextListType:"@Default.TextList_Type_1_V_0_0_1"}`; type name with `_V_x_y_z`, spaces and dots as `_`; optional parameters popupWindowName, adaptWindow, left/top/width/height. [R] UI.OpenFaceplateInPopup | script text (shows the runtime naming scheme `<type>_V_<a>_<b>_<c>`) | H |
| 56 | FaceplateType runtime object | Properties incl. HotKeys, Layers, Items, EnableExplicitUnlock, Suspendable; events OnLoaded, OnUnloaded, OnTapped, OnContextTapped, OnHotKey. [R] Description FaceplateType | expandos/events of the ILFaceplateTypeData (conjectured) | H |
| 57 | HMI data types: versions | Version 0.0.1 "in work" on creation, release with major/minor number, "Edit type" → next version, "Discard changes and delete version"; deleting deletes all instances. [TG] "Managing versions of user data types" | HmiUdtTypeData/HmiUdtVersionData (conjectured, like faceplate type) | H |

## 3. Plan

Principle as before: exactly one change per sample, the user builds by hand in TIA, I save, diff (`tools\fp_chain_diff.py`) and dump (`tools\fp_dump.py`), then decode. The samples build on a clean base, not on F01 (F01 contains the defective FP_Flash, MCP-059, and typeless containers).

**Phase A – decode basics (without new samples).**
- A1 expando format of the screen items (`IPropertyExpandoAttributes`, `IReferenceExpandoAttributes`, `IPropertyDynamic*`, `IEventExpandoAttributes`): keys (property IDs), value types, layer GUID. Material: F01 with Openness truth (all attributes known), F02 copies of the screen items in the faceplate. Without A1, interface values and dynamizations cannot be read.
- A2 `CoreMapArray` (format of the three interface lists).
- A3 `VersionData` blob (version number) and `State`, `FinalizationTime`, default relation.

**Phase B – base sample F10 (by hand).** New project from 103 (HMI_1 + PLC_1 + HmiTag_Ext1), one faceplate type `FP_T` with one rectangle, released as 1.0.0, not instantiated.

**Phase C – variation series, one change each versus F10** (proposal, names free):

| Sample | Change | Clarifies |
| --- | --- | --- |
| F11_iface_tag_bool | 1 interface tag Bool | #23, TagInterfaceProperties, tag table of the hidden device |
| F12_iface_tag_types | interface tags Int, Real, LReal, WString, DateTime, Array[0..3] of Int | #24 |
| F13_iface_tag_plcudt | interface tag PLCUDT (UDT_Motor as library type) | #25, dependencies |
| F14_iface_tag_hmiudt | interface tag HMIUDT | #26 |
| F15_local_tag | local tag Int with limit | #27 |
| F16_iface_prop_types | one interface property of each of the 11 data types | #30 |
| F17_prop_dyn | rectangle color via property interface, once with formula, once with evaluation Range | #31 |
| F18_iface_event | interface event with 3 parameters (Int, Bool, Color), RaiseEvent script on a button | #34, scripts in the type |
| F19_type_dyn_script | in the type: tag, script, expression, resource list dynamization, Loaded event, global definition area | #36–38 |
| F20_type_props | Suspendable, display name, background, second layer with "Runtime visible" off | #16–18 |
| F21_version2 | "Edit type" → 1.0.1 InWork (nothing changed), then a second sample released | #3, #4 |
| F22_do_not_use | marking "Do not use" | #10 |
| F23_rename_dup | rename; then duplicate | #6, #7 |
| F24_folder | type in library folder | #12 |
| F25_instance | one instance on Main, no interconnection | #40, mediator |
| F26_instance_bind | interface tag connected statically, interface property static | #32, #42 |
| F27_instance_dyn | instance: interface property via tag/script, interface tag via tag parameter, interface event script | #42–44 |
| F28_two_instances | second instance, different values | repeatability |
| F29_update | release type as 1.1.0 with "Update instances" | #9 |
| F30_nested | inner type in outer type, interface passed through | #47 |
| F31_popup | popup script (inside and outside) | #48 |
| F32_language | second project language, texts in the type | #19 |
| F33_controls | alarm control and trend control in the type, trend via configuration string | #22 |
| F34_delete | delete version or type | #8 |

**Phase D – comparison with the truth.** Per sample, Openness truth of the instances (`get screen detail=full`, interface values) and, for the type, whatever Openness does not provide, the TIA UI (screenshots or transcription by the user). The parser gets a section "Faceplates" in the text output; tests in `parser\tests`.

**Phase E – writing (later).** Only when A to D are green for reading: change instances via file, then types.

## 4. Work list (check off)

1. [ ] A1 expando decoding for screen items (values, layer, dynamizations, events); comparison against F01 truth.
2. [x] A2 `CoreMapArray` = array of pe:MapT, MapT format clarified (`COREMAPARRAY.md`); element of an interface tag in F05.md (0x0b = string via offset).
3. [ ] A3 version number (`VersionData`), state, default, FinalizationTime, author/comment of the version.
4. [ ] Fully assign the hidden device per version (which objects are always created, which per content).
5. [ ] Interface tags: storage location, order, data type, array bounds, UDT reference (F11–F14). Partially: storage location, Bool, tag table of the interface (F05.md); open data type codes, arrays, UDTs (F12–F14). F40: struct tags (PLC UDT, version), codes 1/3/14/30 (evidence F40: Siemens demo project, not in the repo). F12: Real 10, DateTime 13, Array = 0x40|element with bounds, HMI UDT (F12.md).
6. [ ] Local tags incl. limits (F15).
7. [ ] Interface properties: all 11 data types, default values (F16). F40: InterfaceProperties decoded (codes 14, 1282, 65544, 65585, 65555, Default) (evidence F40: Siemens demo project, not in the repo). F16: all 11 data type codes and standard defaults against Openness (F16.md).
8. [ ] Dynamization "Property interface" incl. formula and evaluation type (F17).
9. [ ] Interface events and parameters (24 data types) (F18). F18 part A: InterfaceEvents with parameters (data type as name, Int missing), RaiseEvent in the script (F18.md); open handler on the instance. F18b: handler on the instance (On<event>, EmbeddedScriptChildren) and instance values of all property types against Openness.
10. [ ] Dynamizations and events in the type, global definition area, script objects (F19). Partially: storage location, scope Faceplate.Event/Trigger, global definition area on the module container, what "Create faceplate" takes over (SCRIPTS.md); open faceplate events created in the editor (F19).
11. [ ] Type properties: Suspendable, layers, display name (F20). Partially: stored attributes of type/version/content, size, 32 layers, active layer, Properties map (TYPE-PROPERTIES.md); Suspendable/display name/background are default and not stored, open until F20. F40: Suspendable = expando 0x0263 bool (evidence F40: Siemens demo project, not in the repo).
12. [ ] Versioning, default, Do not use, rename, duplicate, folders, delete (F21–F24, F34). Partially: release changes the version in place (State, FinalizationTime, new VersionGuid, DefaultVersionToType), no copy as for PLC types (RELEASE.md); open second version, default switch etc. (F21–F24). F16: new version = complete copy (F16.md).
13. [ ] Instance: type reference (InstanceOf/ContainedType), mediator, UsedTag, interface values static/dynamic, tag parameter, events (F25–F28). Partially: type reference `IsInstanceOf` → version, ContainedType `V<version>\<type>`, no mediator without interconnection (F05.md). F40: interface values static/dynamic, UsedTag, mediator from script (evidence F40: Siemens demo project, not in the repo).
14. [ ] Updating/replacing instances (F29).
15. [ ] Nesting and dependencies (`DependencyIdentifiers`) (F30). Partially: no dependencies in F03/F05, empty container without IsInstanceOf, MapT type codes = expando codes or metamodel type IDs (DEPENDENCIES.md); open typed faceplate inside a faceplate (F30).
16. [ ] Popups and type references in scripts (F31).
17. [ ] Multilingualism, fonts (F32).
18. [ ] Controls in the type (F33). Partially: only alarm control, trend control, parameter set control, faceplate container are taken over; Caption/Font/column lists as map expandos, fonts via FontList_C references (CONTROLS.md); open trend and list codes (F33).
19. [ ] Parser output "Faceplates" (types, versions, content, interfaces, instances) and tests against all samples.
20. [x] Clarification `HmiPropertyInterface*` (ScreenModules): Classic only, not used in Unified (section 1a).
21. [x] Help API checked and documented (`docs\TIA-HELP-API.md`), gaps filled (#52–57). Open: Siemens 109812366 "Tips and Tricks Faceplates Unified" (download needs the user's direct approval).

Not checkable with the test setup: OPC UA data types (#28), runtime behavior (Suspendable effect, Flashing), parameter set control without recipe/parameter configuration.
