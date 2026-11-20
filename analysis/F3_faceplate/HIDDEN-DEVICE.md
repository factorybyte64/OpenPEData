# F3 list item 4: objects of a faceplate version ("hidden device")

As of 2026-10-20, read-only: `samples\F02_hmi_faceplates_crash` (2 versions) and `samples\F03_hmi_faceplates_user` (5 versions). Tool: `analysis\F3_faceplate\hidden_device.py`, raw output `hidden_device_out.txt`.

**Delimitation.** All objects with the link `Environment` → `FaceplateLibraryVersionData` belong to a version. The project objects (screens, HMI_1, project tags) instead have `Environment` → `ProjectData`. The version is thus a separate environment space like a project.

## Count per version (F03)

| | FP_Basic | FP_Controls | FP_Elements | FP_Interface | FP_Script |
| --- | --- | --- | --- | --- | --- |
| Objects with Environment → version | 92 | 88 | 97 | 88 | 87 |
| of which fixed core | 79 | 79 | 79 | 79 | 79 |
| Screen items (ILScreenItemData) | 12 | 3 | 13 | 4 | 2 |
| Faceplate containers (ILFaceplateContainerData) | – | 1 | – | – | – |
| Content-dependent font lists `FontList_C<number>` | 1 | 5 | 5 | 2 | 1 |
| Scripts (HmiScriptEmbeddedData) | – | – | – | 1 | 3 |
| ScriptModuleContainer | – | – | – | 1 | 1 |
| Tag shells (HmiTagData without content) | – | – | – | 1 | 1 |

F02 (FP_Basic, FP_Controls) has the same numbers; the objects are identical.

## Fixed core (79 objects, the same for all 5 versions)

| Count | Type | Names / remark |
| --- | --- | --- |
| 1 | ILFaceplateTypeData | "Faceplate type" (the content; subtype ILFaceplateType) |
| 1 | DeviceData | "FaceplateDevice" |
| 1 | DeviceItemData | "FaceplateDevice" |
| 1 | AddressManagerV2Data | "FaceplateDeviceIL LAddressManager of type: Main " |
| 1 | HmiApplicationData | "HiddenHmiRTDeviceFaceplateTypes" (the hidden HMI software) |
| 1 | HmiDeviceData | "Device" |
| 1 | HmiDeviceSettingsData | – |
| 1 | HmiSettingsData | – |
| 2 | FolderData | "HmiTagFolder", "ILFaceplateTypeFolder" |
| 1 | HmiTagTableData | "Default tag table" |
| 9 | HmiSystemTagData | @CurrentLanguage, @DeltaActivationState, @DiagnosticsIndicatorTag, @LocalMachineName, @ServerMachineName, @SystemActivationState, @SystemHealthIndex, @UserDomain, @UserName |
| 9 | HmiInternalSystemTagData | the same 9 as `InvisibleInternalSystemTag.@…` |
| 7 | HmiCycleData | T100ms, T250ms, T500ms, T1s, T2s, T5s, T10s |
| 10 | HmiFontItemData | Siemens Sans 9, 10, 11, 12, 14, 14/700, 18, 22, 22/700, 44 |
| 11 | HmiFontListData | DefaultFontList and FontList_SiemensSans_* (10) |
| 1 | HmiFontLocatorData | – |
| 1 | HmiLanguageAndFontSettingsData | – |
| 3 | StyleData | ExtendedStyleV2101, FlatStyle_BrightV2101, FlatStyle_DarkV2101 |
| 1 | StyleSettingsData | – |
| 1 | SystemStyleContainerData | "ILSystemStyleContainer" |
| 1 | HmiCentralColorPaletteData | "HmiCentralColorPalette" |
| 1 | HmiRuntimeUmacData | – |
| 1 | HmiUmcConfigurationData | – |
| 1 | DeviceFunctionRightSet | – |
| 9 | SystemDeviceFunctionRightProxy | HMI.ActivateRemoteControl, HMI.Observe, HMI.Operate, HMI.UserManagement, HMI.WebAccessObserveOnly, ODKRT_GlobalAccess, OPCUAServer_GlobalAccess, UMC_ImportExport, UM_RESETPWD |
| 1 | HMI_comment | "HMI comment" (text category) |
| 1 | HMI_screen | "HMI IL screen TextCategory" |

This corresponds to a minimal Unified HMI software (cf. HMI_1 in sample 100, which has 29 system tags and 18 alarm classes; the faceplate device has no alarm classes and no connection).

## Content-dependent objects

- **Screen items, containers:** `ILFaceplateTypeData.ILScreenItems` / `ILScreenItemParent`, as for screens.
- **Font lists `FontList_C<number>`:** one list per used font combination, used via `ExpandoReferences` from the screen items (e.g. FP_Basic: B_Text1; FP_Elements: 11 elements share FontList_C1481594083). The same number occurs in several versions (e.g. FontList_C1939294067 in FP_Controls, FP_Interface, FP_Script); so it depends on the font, not on the faceplate.
- **Scripts:** HmiScriptEmbeddedData (EmbeddedScriptParent → screen item) and one HmiScriptModuleContainerData per faceplate with scripts (`ILFaceplateTypeData.ScriptModuleContainer`, `ScriptContainerParent`; `TriggerElements`/`TriggerModule` between container and script).
- **Tag shells:** HmiTagData with only ICoreAttributes, `TRefParent` → Default tag table, `TextualReferences` there; references `ExpandoReferences` from screen items and scripts (see F03.md).

## Link structure

```
FaceplateLibraryVersionData
 ├─ VersionToContent ─────────────── ILFaceplateTypeData ("Faceplate type")
 │                                     ├─ ILScreenItems → ILScreenItemData / ILFaceplateContainerData
 │                                     ├─ ScriptModuleContainer → HmiScriptModuleContainerData → TriggerElements → HmiScriptEmbeddedData
 │                                     └─ AggregatingFolder → FolderData "ILFaceplateTypeFolder"
 ├─ Parts ────────────────────────── DeviceData "FaceplateDevice"   (DeviceData.PartInverse → version)
 │                                     ├─ DeviceItems/Elements → DeviceItemData "FaceplateDevice"
 │                                     ├─ DeviceItems/LogicalElements → HmiApplicationData "HiddenHmiRTDeviceFaceplateTypes"
 │                                     │     ├─ HmiDevice → HmiDeviceData "Device"
 │                                     │     │     ├─ HmiCycleDataChildren → 7 × HmiCycleData
 │                                     │     │     ├─ InternalSystemTags → 9 × HmiInternalSystemTagData
 │                                     │     │     ├─ Folders → "HmiTagFolder" (AggregatedElements → "Default tag table" → Content → 9 × HmiSystemTagData),
 │                                     │     │     │            "ILFaceplateTypeFolder" (AggregatedElements → ILFaceplateTypeData)
 │                                     │     │     ├─ DeviceToHmiFontList, DeviceToHmiFontLocator, StyleSettingsParent, UmacChild, UmcConfiguration,
 │                                     │     │     │  HmiCentralColorPaletteParent, Children → HmiDeviceSettingsData
 │                                     │     ├─ TextCategories → HMI_screen
 │                                     │     └─ AvailableDeviceFunctionRightSetOfDevice / …RightsOfDevice → rights
 │                                     └─ AddressManagers → AddressManagerV2Data
 ├─ ParentToHmiFontList / ParentToHmiFontItem ─ 10 font items, font lists
 ├─ ProjectToSystemStyleContainer ──── SystemStyleContainerData → Styles → 3 × StyleData
 ├─ HmiSettingsData_HmiSettingsParent_Inverse → HmiSettingsData
 ├─ Categories ──────────────────────── HMI_comment
 ├─ RelatedPackage ──────────────────── PackageContainerData "SW.HMI.FaceplateVersion_21.0.1.0"
 └─ VersionObjectToTypeObject / DefaultVersionToType → FaceplateLibraryTypeData
```

Conclusion for the parser: a faceplate version is a self-contained mini HMI. For the output, `VersionToContent` (content) suffices, and later the tag table of the hidden device (interface and local tags, once F11/F15 are available). The fixed core can be summarized as "standard equipment"; when writing a new version it must be created completely.
