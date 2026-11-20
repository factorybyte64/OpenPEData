# Faceplate features according to the TIA V21 documentation, with storage location in the file format

As of 2026-10-20 18:20, thread "Unified Faceplates". Copy of the analysis `analysis\F3_faceplate\DOC-FEATURES.md` on the user's machine (local 73dae3b). Plan and work list for it: [PLAN-faceplate.md](../PLAN-faceplate.md).

**Sources.** Locally installed TIA V21 help (rule from the user 18:13: Siemens documentation primarily via the local help API `https://localhost:5112/en-us/home?api=PortalV21`; the ZIPs under `C:\Program Files\Siemens\Automation\Portal V21\Help\en-US` are the same content):
- **[S]** `ScreensWCCUenUS.zip`, chapter "Configuring faceplates (RT Unified)" (81 pages)
- **[O]** `OpennessWCCUenUS.zip`, chapter "Faceplate Container"
- **[T]** `TIAPortalOpennessenUS.zip`, "Exporting/Importing a screen with a faceplate instance" (Classic, comparison)
- **[B]** `BasicsWCCUenUS.zip`, "Generating a faceplate as a type"
- **[R]** `ObjectModelWCCUenUS.zip`, runtime object model: FaceplateContainer, FaceplateType, UI.OpenFaceplateInPopup, Resources.FaceplateTypes

In addition, the metamodel from F01 and the findings from `samples\F02_hmi_faceplates_crash` (see [hmi-faceplate.md](hmi-faceplate.md)). Documentation summarized in our own words, page titles in quotation marks. Siemens document 109812366 (tips and tricks) not yet analyzed.

**Legend.** Storage location: **evidenced** = seen in sample F02; **conjectured** = derived from the metamodel. Testable: **H** = by hand in TIA, **M** = via TiaMcpXE, **–** = not testable.

## 1. Storage locations (metamodel, short version)

| Object | Type (ID) | Content | Status |
|---|---|---|---|
| Faceplate type | `FaceplateLibraryTypeData` (0x00d30002), base `HmiByRefTypeObjectBase` | Name, TypeGuid, IsObsolete, IsSetAsNonUpdateable, MinimumTargetDeviceVersion, TypeNamespace, AlwaysDownload; relations `TypeObjectToVersionObject`, `TypeToDefaultVersion`, folder `AggregatingFolder` | evidenced |
| Faceplate version | `FaceplateLibraryVersionData` (0x00d30003) | State (InWork/Committed), Version (VersionData blob), VersionGuid, FinalizationTime, MinimumTargetDeviceVersion, Author, Comment, LastModifiedBy, DependencyIdentifiers; relations `VersionToContent` → content, `Parts` → hidden device, fonts, styles, `RelatedPackage` (SW.HMI.FaceplateVersion_21.0.1.0) | evidenced |
| Faceplate content | `ILFaceplateTypeData` (0x00cd1004), base `ILScreenData` | like a screen: screen items via `ILScreenItems`; AttributeSet `IFaceplateInterface` with InterfaceEvents, InterfaceProperties, TagInterfaceProperties (each `CoreMapArray`); relations `ILFaceplateToTagTable` → TagTableData, `ILFaceplateType_UsedTag` → HmiCoreTagData | evidenced (interface still empty) |
| Hidden device per version | HmiApplicationData `HiddenHmiRTDeviceFaceplateTypes`, DeviceData/DeviceItemData `FaceplateDevice`, AddressManager, cycles, system tags, fonts, styles, color palette, HmiTagTableData, folders `HmiTagFolder`, `ILFaceplateTypeFolder` | Runtime environment of the type; probably home of the interface and local tags | evidenced (table empty) |
| Faceplate instance | `ILFaceplateContainerData` (0x00d30006), base `ILScreenItemData` + `HmiByRefInstanceObjectBase` | screen item (subtype HmiFaceplateContainer); relation `ILFaceplateContainer_UsedTag`; override `ICoreAttributes.InstanceOf` = probably reference to the version (ContainedType) | evidenced (only typeless) |
| Mediator | `FaceplateInstanceMediatorData` (0x00d30007) | Instance object of the library, purpose unknown (probably instance ↔ version per device) | conjectured |
| Event/property interface (faceplate model, older) | `HmiPropertyInterface*` (ScreenModules, 0x00b71003–08) | Categories, properties (Type, PropertyDirection), connections to inner objects | probably only Classic/Comfort; unchecked in Unified |
| HMI data type | `HmiUdtTypeData`/`HmiUdtVersionData`, `HmiStructureType*` | for interface tags of type HMIUDT | conjectured |
| Property values of all screen items | `IPropertyExpandoAttributes`, `IReferenceExpandoAttributes`, `IPropertyDynamic*`, `IEventExpandoAttributes` | Values, layer, dynamizations, events; not yet decoded by the parser | evidenced, format open |
| Scripts | `HmiScriptEmbeddedData`, `HmiScriptModuleContainerData` (DL.Scripting) | one object per script, one container per screen | evidenced (F01) |

## 2. Features according to the documentation

### 2.1 Type, version, library

| # | Feature | Documentation says (source) | Storage location | Testable |
|---|---|---|---|---|
| 1 | Create type (library or "Create faceplate" from screen items) | New type with version 0.0.1 "In progress"; name max. 128 characters, unique, without `# $ +% . / : [ ] ' ~ " \``; device type (Unified/Advanced) cannot be changed later. When creating from a screen, graphic references, function lists and Dynamic SVG are not carried over; tag references are resolved via interface tags of the same name. [S] "Creating a faceplate type in the project library", "Creating a faceplate type from a screen", [B] | TypeData + VersionData + ILFaceplateTypeData + hidden device (evidenced) | H |
| 2 | Lowest device version | Set at creation, later only via "Upgrade device version" (new version). Table "Available functions depending on the device version" (16.0 to 21.0). [S] "Lowest device version of a faceplate type" | `MinimumTargetDeviceVersion` in type and version (evidenced: '' and '21.0.1.0' respectively), `RelatedPackage` | H |
| 3 | Versions, status, release | Editing creates a new version "In progress"; release with name, version number, author, comment; options: update instances, delete unused versions, set dependent types to in work; consistency check. Only released versions can be instantiated. [S] "Releasing a faceplate version of a type", "Editing a faceplate type" | `State`, `Version` (blob), `FinalizationTime`, Author/Comment (evidenced) | H, M (typeVersionRelease only tested for PLC) |
| 4 | Default version | Highest released version becomes "Default", can be changed manually; dragging into the screen uses the default. [S] "Defining a version as the "default" version" | `TypeToDefaultVersion`/`DefaultVersionToType` (evidenced) | H |
| 5 | Assign version number (several types) | New versions with a common number; log under "Common data > Logs". [S] "Assigning a faceplate version number" | new VersionData; log object (conjectured) | H |
| 6 | Duplicate | Copy in the same folder, released version, no instance. [S] "Duplicating a faceplate type" | new TypeData, new TypeGuid; possibly `OriginalTypeGuid` (conjectured) | H |
| 7 | Rename | References in scripts are updated accordingly. [S] "Renaming a faceplate type" | `ICoreAttributes.Name`; script texts (conjectured) | H |
| 8 | Delete type/version | Only without dependencies; deleting a version deletes its instances; script references remain empty. [S] "Deleting a faceplate type or a version" | Tombstones | H |
| 9 | Update, replace | All instances to a new version or another type; single instance via "Faceplate type" on the container. [S] "Updating a faceplate type", "Replacing a faceplate type" | Instance reference on the container (conjectured `InstanceOf`) | H |
| 10 | "Do not use" | Prevents instantiation; existing instances warn. [S] | `IsObsolete` or `IsSetAsNonUpdateable` (conjectured) | H |
| 11 | Consistency | Status column; "Adapt inconsistent type" or set default. [S] | derived from default relations and `DependencyIdentifiers` (conjectured) | H |
| 12 | Folders in the library | Types in folders; the Classic export calls them "Folder@$@Type V x.y.z". [T] | `AggregatingFolder` → FolderData (evidenced) | H |
| 13 | Copying to other projects, global library | Order PLC UDT, type, instances; a name conflict results in "Name_1". [S] | same objects, `OriginalLibrary` (evidenced: '000_empty') | H |
| 14 | Open read-only, toolbar | Does not change any data. [S] | – | – |

### 2.2 Visualization (content of the type)

| # | Feature | Documentation says | Storage location | Testable |
|---|---|---|---|---|
| 15 | Screen items in the type | Basic objects, elements; controls only alarm control, trend control, faceplate container, parameter set control. [S] ""Faceplate types" editor" | `ILScreenItems` of the ILFaceplateTypeData (evidenced) | H |
| 16 | Properties of the type | Display name, size, background; "Resize screen to content". [S], [R] FaceplateType.* (BackColor, DisplayName, Width, Height, Layers, Suspendable, EnableExplicitUnlock …) | Expandos of the ILFaceplateTypeData (evidenced: Core, Property, Reference) | H |
| 17 | Layers | As in the screen; "Runtime visible" separately. [S] | `_#ActiveLayer` and layer GUID in the expandos (evidenced, see MCP-059) | H |
| 18 | Suspendable | Type property from 19.0; suspends scripts of invisible instances. [S] | Expando on the ILFaceplateTypeData (conjectured) | H |
| 19 | Multilingual texts | Texts per project language; "language-compatible fonts for faceplates". [S] | `ITextAttributes`/HmiTextData (conjectured), fonts of the version (evidenced) | H (2nd language only by hand) |
| 20 | Style preview | Preview only. [S] | Style objects of the version (evidenced) | H |
| 21 | Dynamic SVG, graphic list types, color palettes | from 18.0 / 21.0 / 20.0. [S] | Library types DynamicSVGLib, ResourceListLib, ColorPalette (`*InstanceData`) | H |
| 22 | Controls in the type | Alarm control, trend control from 18.0; trend with logging tags via configuration string "HMI-Tag:LoggingTag". [S] | Screen items + script (evidenced: C_AlarmControl1, C_Trend1) | H |

### 2.3 Tags

| # | Feature | Documentation says | Storage location | Testable |
|---|---|---|---|---|
| 23 | Interface tags (tag interface) | Connected to project tags only via the container; name ≤128, starts with a letter/underscore; order can be changed; renaming resets connected values. [S] "Configuring interface tags in the faceplate type" | `TagInterfaceProperties` + HMI tags in the table of the hidden device, `ILFaceplateToTagTable` (conjectured) | H |
| 24 | Data types | Simple types, arrays (from 0), LReal, DateTime/LTime, PLCUDT, HMIUDT; no arrays of WChar/WString/HMIUDT. [S] "Overview" (Tags) | Tag data type (`IHmiTagStructureAttributes_HmiDataType`, `IStructureItem`) | H |
| 25 | PLC data type (PLCUDT) | Link a version of the library type; up to 26 levels, arrays with constants; version change not automatic. [S] | Reference to PLC type version (`DependencyIdentifiers`) (conjectured) | H |
| 26 | HMI data type (HMIUDT) | From the library. [S] | HmiUdtVersionData reference (conjectured) | H |
| 27 | Local tags | Only visible in the type; from 21.0 with limits (not for Array, Bool, Byte, Char, DateTime, Word types, WString). [S] "Configuring local tags…" | Tags in the table of the hidden device (conjectured), `Thresholds` | H |
| 28 | OPC UA data types | Mapping OPC UA → faceplate types. [S] | as 24 | – |
| 29 | Unique names | A name only once across all tabs of a type. [S] | Check rule | H |

### 2.4 Property interface

| # | Feature | Documentation says | Storage location | Testable |
|---|---|---|---|---|
| 30 | Interface properties | 11 data types: 64-bit integer, Authorization (read-only), Boolean, Color, Floating-point, Graphic, Configuration string, Multilingual text, Parameter set type, Resource list, Unsigned 64-bit integer; minimum versions 16.0 to 21.0. [S] | `InterfaceProperties` (CoreMapArray) (conjectured) | H |
| 31 | Dynamization "Property interface" in the type | Object property to interface property; optional formula (operators, conversions) and evaluation type (None, Range, Multiple bits, Single bit). [S] "Configure interface property" | Dynamization in the expando of the inner object (type "PropertyInterface", not in Openness `DynamizationType`) (conjectured) | H |
| 32 | Value on the instance | Static (several values with `;`) or dynamization Tag, Script, Flashing, Resource list. [S] | Expando/interface part of the container (conjectured) | H, M |
| 33 | Configuration string in scripts | `Faceplate.Properties.<Name>`. [S] | Script text | H |

### 2.5 Event interface

| # | Feature | Documentation says | Storage location | Testable |
|---|---|---|---|---|
| 34 | Interface events with parameters | Any number of parameters; 24 data types (Bool … Word, Color, DateTime, HmiEventTrigger, HmiGesture, HmiKeyboardModifier, LString). Raised via `Faceplate.RaiseEvent(name, {param:value})`. [S] | `InterfaceEvents` (CoreMapArray) (conjectured) | H |
| 35 | Handling on the instance | Function list or script; function name `<Container>_On<Event>(item, <Parameter…>)`. [S] | `EventHandlers`/`IEventExpandoAttributes` of the container, script object (conjectured) | H |

### 2.6 Dynamization and scripts in the type

| # | Feature | Documentation says | Storage location | Testable |
|---|---|---|---|---|
| 36 | Dynamization kinds in the type | Tag, Script, Resource list, Expression, Property interface, Flashing (Flashing not supported in runtime); only tags of the type. [S] | `IPropertyDynamicExpandoAttributes`/`…LinkExpando…` of the inner object (evidenced for screens) | H |
| 37 | Events in the type | Function list (restricted) or script; "Cleared" without tag access. [S] | EventHandler expandos, script objects | H |
| 38 | Global definition area | Applies to all instances. [S] | `GlobalDefinitionAreaScriptCode` or ScriptModuleContainer of the type (conjectured) | H |
| 39 | Overview of dynamizations per version | View only. [S] | – | – |

### 2.7 Instance (faceplate container)

| # | Feature | Documentation says | Storage location | Testable |
|---|---|---|---|---|
| 40 | Create instance | Drag type/version into the screen or empty container + "Faceplate type"; only released versions. Openness: `ContainedType` as string "Faceplate_1_V_0_0_1". [S], [O] | `InstanceOf`/reference to FaceplateLibraryVersionData, possibly `FaceplateInstanceMediatorData`; empty container evidenced (F01) | H, M (itemCreate with containedType) |
| 41 | Container properties | Window flags, zoom 0.01–8, size-fit, scroll bars, icon. [S], [O], [R] | Expandos of the ILFaceplateContainerData | H, M |
| 42 | Connect interface tags | Statically with project tag/UDT instance; tag parameter (dynamic tag name). [S] | `ILFaceplateContainer_UsedTag` + interface values in the expando; TagParameterDynamization (conjectured) | H, M (partially) |
| 43 | Dynamization of the instance | Tag, Script, Expression, Resource list, Tag parameter, Property interface, Flashing. [S] | Expandos of the container | H |
| 44 | Events of the instance | Activated/Deactivated (frame and title required), interface events. [S], [R] | EventHandler expandos | H |
| 45 | Change the version of an instance | Via "Faceplate type" on the container. [S] | Instance reference | H |
| 46 | Access to the container from the type | `Faceplate.Parent.<Property>`, `Faceplate.Parent.Properties.<Name>.Tag`. [S] | Script text | H |

### 2.8 Nesting and popups

| # | Feature | Documentation says | Storage location | Testable |
|---|---|---|---|---|
| 47 | Faceplate in faceplate | From 18.0; bind inner interface tags/properties to outer ones (not local ones); no other version of the same type; device version outer ≥ inner. [S] | Container in the ILFaceplateTypeData (evidenced, typeless), `DependencyIdentifiers` of the outer version (conjectured) | H |
| 48 | Popup | `UI.OpenFaceplateInPopup("Type_V_x_y_z", …)` outside, `Faceplate.OpenFaceplateInPopup(...)` inside; only versions used in the device. [S], [R] | Script text; use of the version in the device (conjectured `CodeRelations`) | H |
| 49 | Default version in scripts | From 20.0.0.2 via script method. [S], [R] Resources.FaceplateTypes | Script text | H |

### 2.9 Openness and export

| # | Feature | Documentation says | Storage location | Testable |
|---|---|---|---|---|
| 50 | Openness Unified | Container only: Create/Delete, properties (Name, ContainedType, Enabled, size, Icon, Caption, TabIndex, Visible, WindowFlags, Interface, EventHandlers, ShowFocusVisual, RotationAngle, InteractiveZooming/Panning), UDT on interface. Types can be neither created nor exported. [O] | – | M |
| 51 | Classic export (comparison) | FaceplateTypeName "Type V 0.0.2", folder path "@$@". HmiTarget only. [T] | – | – |

Not checkable with the test setup: OPC UA data types (#28), runtime behavior (Suspendable, Flashing), parameter set control without recipe configuration.
