# F3 list item 10: scripts and dynamizations in the faceplate type

As of 2026-10-21, read-only: `samples\F03_hmi_faceplates_user`, `samples\F05_fp_iface_instance.zap21`. The source of the types are the screens FP_Interface and FP_Script from F01 ("Create faceplate" by hand, user); their Openness truth `samples\F01_hmi_allobjects_truth\HMI_1\screen_FP_*.full.json` serves as comparison.

## Storage location

Scripts in the type are the same objects as on screens, only in the environment of the version (`Environment` → FaceplateLibraryVersionData, `Target` → HiddenHmiRTDeviceFaceplateTypes):

```
ILFaceplateTypeData ("Faceplate type")
 ├─ ILScreenItems → ILScreenItemData ─ EmbeddedScriptChildren → HmiScriptEmbeddedData (EmbeddedScriptParent back)
 └─ ScriptModuleContainer → HmiScriptModuleContainerData "ScriptModuleContainer" (ScriptContainerParent back)
                              ├─ EventElements   → event scripts  (on the script: EventModule)
                              └─ TriggerElements → script dynamizations (on the script: TriggerModule)
```

| | Screen (HMI_1) | Faceplate type |
| --- | --- | --- |
| `IScriptAttributes.Scope` of the events | `Screen.Event` | **`Faceplate.Event`** |
| Scope of the script dynamizations | `Screen.Trigger` | **`Faceplate.Trigger`** |
| `IScriptModuleContainerAttributes.Scope` | `Screen` | **`Faceplate`** |
| Script text | `IScriptAttributes.Code` (token table, Hmi.script) | same |
| References in the script (`Tags("…")`) | `IScriptLinkExpandoAttributes` → index into `ExpandoReferences` (H-031) | same; from F05 on with empty entries (F05.md, hmi.py fix 4b03b90) |
| Tag dynamizations | expandos on the screen item, `ExpandoReferences` → HmiTagData | same; target is the tag in the hidden device (F03: shell, F05: interface tag) |

**Global definition area.** It sits on the module container: `IScriptModuleContainerAttributes.Event_GlobalSectionCode`, `Trigger_GlobalSectionCode`, `FunctionList_GlobalSectionCode` (token tables like `Code`). In the type, after "Create faceplate", it contains the default text with the addition "Note: The definitions in this global definition area will be shared between all faceplate container instances of this type." The content of the screen (`// F01 global definition I_Text1` in the trigger area of FP_Interface) was **not** taken over.

## What "Create faceplate" takes over

| Screen | taken over | not taken over |
| --- | --- | --- |
| FP_Interface | I_Text1 ForeColor (script dynamization), I_Rect1 BackColor/BorderColor, I_Bar1 ProcessValue (tag) | screen dynamization BackColor (script on the screen), content of the global definition area |
| FP_Script | S_Button1 Tapped/Down, S_IOField1 ProcessValue change | screen event Loaded |
| FP_Controls | C_AlarmControl1, C_Trend1, C_DetParam1, C_Faceplate1 | **C_AlarmLine1** (HmiAlarmLineControl) |
| FP_Elements | 13 elements | **E_AlarmInd1** (HmiAlarmIndicator) |

Screen events and screen dynamizations belong to the screen, not to the selected objects; the faceplate has its own events (can only be checked with F19). Why AlarmLine and AlarmIndicator are missing belongs to list item 18 (controls in the type).

**Further differences type ↔ screen** (315 values compared, 304 equal):

- Position: the objects are shifted by −10/−10, except in FP_Controls, whose objects already started at 10/10. Likely the selection is shifted so that its frame has a 10 px margin.
- `StyleItemClass` (expando 7508) is stored explicitly on every object in the type (= subtype); on the screen it is missing (default).
- I_Bar1: in the type an empty mapping (`conditionType None`), on the screen none.

## Parser and test

- `faceplates.py`: screen items of the content now via `Hmi.items` with properties, text, tags, dynamizations (incl. script code) and events (name, scope, code; `Faceplate.Trigger` counts as dynamization). New per content: `scriptModule` with scope and the three global definition areas. The text output shows dynamizations, events with code and non-empty global definitions.
- `test_faceplates.py`, `check_scripts`: dynamizations (property, kind, tag or script code) and events/property events (code) of FP_Interface and FP_Script against the truth of the source screens; F03 and F05 7 values each, all equal.

## Open

- F19: dynamizations and events created in the faceplate editor (events of the faceplate itself, Loaded/Cleared), own global definition area, expression and resource list dynamizations.
