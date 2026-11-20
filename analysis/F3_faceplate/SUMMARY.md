# F3 summary: WinCC Unified faceplates in the PEData format

As of 2026-10-31. Overall picture from the samples F01–F40 (evidence and details in the linked files of this folder, confirmed version in `docs\spec\hmi-faceplate.md`, plan and LIST in `tia-format\PLAN-faceplate.md`). Parser: `parser\openpedata\faceplates.py` (key `faceplates` in `Model.to_dict()`, text section "Faceplates"), test: `parser\tests\test_faceplates.py` (436 samples, 796 values against Openness truth and fixed expectations, 0 deviations).

## 1. Structure

```
FaceplateLibraryTypeData (type, 0x00d30002)        Name, TypeGuid, folder, IsObsolete, AlwaysDownload
 └─ TypeObjectToVersionObject → FaceplateLibraryVersionData (0x00d30003) per version
      IVersionInfo: Version (20 bytes as PLC: 0.0.1), VersionGuid, FinalizationTime; State InWork/Committed;
      default via TypeToDefaultVersion; DependsOn → other versions
      ├─ VersionToContent → ILFaceplateTypeData (content, an ILScreenData = screen)
      │     Expandos: 05/06 size, 0x024b layer list, _#ActiveLayer, Properties {_TypeId 900}, 0x0263 Suspendable,
      │     0x66 BackColor; IFaceplateInterface: TagInterfaceProperties, InterfaceProperties, InterfaceEvents
      │     (each CoreMapArray of pe:MapT); ILScreenItems → screen items; script module container
      └─ all objects with Environment → version = "hidden device" (FaceplateDevice / HiddenHmiRTDeviceFaceplateTypes)
ILFaceplateContainerData (0x00d30006) = instance on a screen or in the content of another type
      IsInstanceOf → version; without IsInstanceOf = empty container
FaceplateInstanceMediatorData = typed reference from a script (popup), not part of an instance
```

- **Hidden device:** fixed core of 79 objects per version (device, HMI software, Default tag table, 9 system tags, 9 internal system tags, 7 cycles, fonts, 11 fixed font lists, styles, color palette, UMAC, rights, text categories); plus content-dependent screen items, `FontList_C<n>` per used font, scripts, interface tag table with tags, element tags of arrays/UDTs, local tags, graphic/text list instances, mediators (HIDDEN-DEVICE.md, evidence F40: Siemens demo project, not in the repo).
- **Versions:** release changes the version in place (State, FinalizationTime, new VersionGuid, default link); "Edit type" creates a **complete copy** (own content, own hidden device, own VersionGuid). RELEASE (71c2b68), F16, F20, F33.

## 2. Interface

**Data type codes** (one enumeration for interface tags and properties):

| Code | Type | Tags | Properties |
| --- | --- | --- | --- |
| 1 | Bool | yes | yes |
| 3 | Int | yes | – |
| 5 | LInt | – | yes |
| 9 | ULInt | – | yes |
| 10 | Real | yes | – |
| 11 | LReal | – | yes |
| 13 | DateTime | yes | – |
| 14 | WString (for properties in TIA "Configuration string") | (F40) | yes |
| 30 | Struct (PLC UDT `PLCUDT` or HMI UDT `HMIUDT`, reference to the library version) | yes | – |
| element code + 0x40 | Array (bounds `_BLDataTypeBounds` "0:3", one tag per element) | yes | – |
| 1282 | Color | – | yes |
| 65542 | Graphic | – | yes |
| 65544 | Multilingual text | – | yes |
| 65555 | Authorization | – | yes |
| 65575 | Parameter set type | – | yes |
| 65585 | Resource list | – | yes |

- **Interface tags** (TagInterfaceProperties): MapT per tag (Name, DataType, tag reference, data type reference); the tag sits in a separate, GUID-named tag table of the hidden device (`ILFaceplateToTagTable`). F05, F12, F40.
- **Interface properties** (InterfaceProperties): MapT `{Name, DataType, _Guid, _TypeId, Default}`; Default = fixed zero value per type (False, 0, 0.0, "", color 0xFF0000FF), reference types without default; not editable in TIA. F16, F40.
- **Interface events** (InterfaceEvents): name and parameter list; parameter data type as a **name**, Int is omitted. Raised in the type via `Faceplate.RaiseEvent(...)` in a normal event script. F18.
- **Local tags:** HmiTagData in the Default tag table of the hidden device, without interface entry; limits as `Limit*Type/Constant` on the tag. A tag bound in the type but not created as interface (trend in F33) is likewise created by TIA as a local tag. F15, F33.

## 3. Instance

- Container on the screen: `IsInstanceOf` → version; Openness shows `ContainedType = V<version>\<type>` (no attribute in the file). Position/size like other screen items, size = content size. F05.
- **Values** of the interface on the container: static per data type (Bool, LInt i64, ULInt u64, LReal double, color, WString, text as reference to a text record, graphic/text list as reference), dynamized via tag (also range), text list, TagParameter with prefix; **UsedTag** = list of all used tags. F18b, F40.
- **Event handling:** script `On<event>` on the container like a screen item event (Openness shows EventType "None"). F18b.
- **Mediator:** only for type references from scripts (popup), not for screen instances; in the script code the type appears as a placeholder whose reference (bit 0x80) points via ExpandoChildren to the mediator; Openness shows it as `<type>_V_<version>` (F36).
- **Function list** as handler: `HmiFunctionListEmbeddedData` with generated SysFct code instead of a script (F18c).
- **AlwaysDownload:** attribute on the type (F36).

## 4. Content of the type

- **Screen items** as on screens (decoder from hmi.py), position on creation −10/−10, `StyleItemClass` stored explicitly. Scope of the scripts `Faceplate.Event` / `Faceplate.Trigger`, global definition area on the module container. SCRIPTS.md.
- **Controls:** only alarm control, trend control, parameter set control and faceplate container are taken over. Sub-objects as map expandos (Caption 0x0051, Font 0x021c → `FontList_C<n>`); alarm control column list of the view **0x3815** and of the statistics **0x3832** (`0x0011` columns with 0x03 name, 0x07 visible, 0x05 width, column ID); trend control trend areas **0x3839** (axes 0x3855/0x3858, trends 0x2150 with data source 0x0440 → tag); toolbars 0x214b/0x214c. CONTROLS.md, F33.
- **Layers:** list 0x024b with 32 maps `{_TypeId 20002, _Guid, 0x03 Name}`, "Visible in runtime" off = **0x07 False**; screen items reference the layer GUID via Layer (mandatory, otherwise the editor crashes). F02, TYPE-PROPERTIES.md, F20.
- **Type properties:** Suspendable **0x0263**, background **0x66 BackColor**, only stored if different from default. Display name not stored. F20.
- **Multilingual texts:** text values per language on the screen item as on screens; languages of the version via ReferenceCulture/EditingCulture/UsedCultures (F32).
- **Nesting:** container in the content with `IsInstanceOf` → inner version; outer version **DependsOn** inner (link only, no `DependencyIdentifiers`); no mediator. Likewise, versions with an HMI UDT tag depend on the UDT version. F30.

## 5. MapT / CoreMapArray

pe:MapT: u32 length, u32 count, u32 offset of value area, type codes (bit 31), keys as varint strings, values. Type codes = expando codes (0x01 bool, 0x02–0x08 numbers, 0x0a dateTime, 0x0b string, 0x0c GUID, 0x0d blob, 0x0e xml, 0x13/0x14 nullable, 0x15 nested map) or metamodel type IDs (0xcd4001 CoreMapArray, 0x66002 DependencyIdentifierValue, 0x1600d color, …). Expando values carry a varint length prefix. CoreMapArray elements can be **pe blobs** (trend areas, SelectionItems); the formerly open "codes" 0x31d00, 0x72, 0x76 and 0x3e300 were such blob headers. In all faceplate samples (including F40), no MapT value remains undecoded. COREMAPARRAY.md, DEPENDENCIES.md, F33.

## 6. Samples

| Sample | Commit | Clarified |
| --- | --- | --- |
| F01_hmi_allobjects | fde7074, f34f5b1 | source screens via MCP, empty container, metamodel |
| F02_hmi_faceplates_crash | f5e4f0f | first real faceplates, hidden device, layer reference mandatory (crash) |
| F03_hmi_faceplates_user | 0939120, 8f20efb, be949fa, 22fa21e, 3dc4517, 10f2106 | 5 types, core 79, scripts, type properties, controls, empty dependencies |
| F05_fp_iface_instance | ef73eb9, 71c2b68 | first interface tag, first instance (IsInstanceOf), release in place |
| F40_siemens_demo_bestpractices (local) | 1d8bd62 | real-world project: properties, UDT tags, instance values, UsedTag, mediator, Suspendable |
| F16_iface_props | 4c67876, fac34ec | all 11 property data types, new version = copy |
| F18_iface_event | 24d1011 | interface events, RaiseEvent |
| F18b_event_handler | dfb9587 | handler on the instance, instance values of all property types |
| F12_iface_tags | 2416777 | tag data types Real/DateTime/Array/HMI UDT |
| F15_local_tag | 50a50b0 | local tag |
| F20_type_props | d79371d | Suspendable, background, layer visibility, display name not stored |
| F30_nested | d79371d | faceplate inside a faceplate, DependsOn |
| F33_control_columns | d79371d | column and trend lists, blob elements in CoreMapArray |
| F18c_event_funclist | 9796175 | event handler on the instance as function list (HmiFunctionListEmbeddedData) |
| F36_popup_alwaysdownload | 9796175 | AlwaysDownload on the type, popup reference in the screen script via mediator (bit 0x80, ExpandoChildren) |
| F32_multilingual | (this commit) | texts in the type per language, languages of the version, runtime languages |

## 7. Remaining gaps

- **Display name** of the type: stored nowhere in F20; open whether TIA applies it or where it is set (user asked).
- **Limits and binding of local tags** with a stored value (F15b, optional).
- **Event parameters** beyond Bool/Int (other data types only if needed).
- **Truth for type contents:** Openness only provides typesList for faceplate types; columns, trends, layers and type properties are checked against the user's assignment values, not against a TIA output.
- **Multilingualism:** read with F32 (texts per language on the screen item, languages of the version); import of library texts with version increment not sampled.
- Not handled from the plan: dynamization "Property interface" with formula (F17), Do not use / rename / duplicate / folders / delete (F21–F24, F34), updating instances (F29), popups from within a faceplate (F31; from a screen: F36), passed-through interface with nesting.
- Siemens 109812366 evaluated (SIEMENS-109812366.md); implemented from it: function list (F18c), AlwaysDownload/popup (F36), multilingualism (F32).
- Names of the property IDs of the control sub-objects (0x3815, 0x3839, 0x2150 …) are derived from the content, not from Siemens names.
