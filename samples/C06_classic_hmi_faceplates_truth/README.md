# C06_classic_hmi_faceplates – classic WinCC (Comfort / RT Advanced), faceplate type with inner dynamics and instances

A faceplate type in the project library with dynamized inner objects and an interface (tag properties, a text property
and a color property). Instances of it on all three devices are bound to the C03 tags, have static values, or are moved
by an instance animation. TIA Portal V21 Update 3, WinCC Comfort/Advanced device version 17.0.0.0. Archive:
`samples/C06_classic_hmi_faceplates.zap21`. It is the C05 project plus the content described here. The start screens
of the three devices are `C6_Faceplates`.

## Devices

| Device | Screen size | Screen |
|---|---|---|
| HMI_TP900 (TP900 Comfort) | 800×480 | `C6_Faceplates` |
| HMI_TP1200 (TP1200 Comfort) | 1280×800 | `C6_Faceplates` |
| HMI_PCADV (PC station, WinCC RT Advanced, `HMI_RT_1`, window mode as in C03) | 1024×768 | `C6_Faceplates` |

## Faceplate type `FP-C05` V 1.1.0 (project library, default version)

The type was created in the TIA Portal UI with "Create faceplate" from six objects on a TP900 screen. It was then
edited in the faceplate editor and released as V 1.1.0. Size 300×150.

| Object | Type | Position (x, y, w, h) | Configuration inside the type |
|---|---|---|---|
| `FP_Frame` | Rectangle | 0, 0, 300, 150 | corner radius, back color = interface property `Background_color` (type default 230,230,230) |
| `FP_Status` | Rectangle | 10, 10, 60, 40 | RangeAppearanceAnimation on interface property `FRange`: 0–9 back 255,0,0; 10–49 back 0,160,0; 50–99 back 0,0,255 (border 24,28,49, no flashing) |
| `FP_Value` | I/O field | 90, 10, 100, 30 | process value = interface property `FValue`, format pattern s99999 |
| `FP_Bar` | Bar | 210, 10, 30, 130 | process value = interface property `FPct`, range 0..100 |
| `FP_Lamp` | Circle | 250, 10, 40, 40 | back 0,200,0, SingleBitVisibilityAnimation on interface property `FVis`, bit 0, visible when set |
| `FP_Title` | Text field | 10, 110, 180, 20 | text = interface property `Text` (type default "Faceplate FP_C05") |

Positions are relative to the faceplate. The source objects were placed at an offset of 10,10 on the source screen
(`_scripts/FP_Source.xml`).

Interface (category `Properties_Faceplate`): `Text` (String, from `FP_Title.Text`), `Background_color` (Color, from
`FP_Frame.BackColor`), `FValue` (Int), `FRange` (Int), `FPct` (Int), `FVis` (Bool). The four Int/Bool properties are
dynamic interface properties: inside the type the objects use them in place of a tag, and on an instance each one gets an
HMI tag or a static value.

The TIA Portal Openness export of the type version (`library/FP-C05_V1.1.0.xml`) contains only the version number and
the author. The objects, the dynamics and the interface of a classic faceplate type are not part of any Openness export.
They are documented here and are contained in the archive.

## Screen `C6_Faceplates` (number 600)

Four instances of `FP-C05 V 1.1.0`, 300×150, each with a text field `Label_<instance>` below it, and the bottom bar of
C03 (`State`, `S1` … `S4` call the VB scripts `SetS1` … `SetS4` of C03, `Next` activates `C6_Faceplates` again):

| Instance | Position | Interface |
|---|---|---|
| `FP_Dyn` | 10, 10 | `FValue` = `D_Int`, `FRange` = `D_Range`, `FPct` = `D_Pct`, `FVis` = `D_Vis`, `Text` "Tags D_Int D_Range D_Pct" |
| `FP_Static` | 330, 10 | static values `FValue` 1234, `FRange` 25, `FPct` 75; `Text` "Static values", `Background_color` 255,255,200 |
| `FP_Alt` | 10, 186 | `FValue` = `D_SInt`, `FRange` = `D_TL`, `FPct` = `D_UInt`, `FVis` = `D_En`, `Text` "Tags D_SInt D_TL D_UInt", `Background_color` 200,230,255 |
| `FP_Move` | 330, 186 | as `FP_Dyn`, `Text` "Moved by D_Pct", plus a HorizontalMovementAnimation of the instance on `D_Pct` (range 0..100, delta x 40) |

`instances/<device>.json` lists the instances as generated. The tag values of the states S1 … S4 are those of C03 (see
the README of C03).

## Runtime behavior seen in the simulation

* Static values of dynamic interface properties: only `FPct` (bar) takes the static value 75. `FValue` shows +0. The
  rectangle of `FRange` keeps its configured back color, because no range matches without a tag. The lamp of `FVis`
  stays visible, because the visibility animation has no tag.
* `FP_Move`: the whole instance moves by `D_Pct` × 40 / 100 px. `D_Pct` = 150 (S4) is clamped to the end position (+40).
* `FRange` = `D_TL` (FP_Alt): −5 (S1) matches no range (configured back color), 5 (S2) red, 10 (S3) green, 50 (S4) blue.
* `FPct` = `D_UInt` above 100 (S2 40000, S3 65535) fills the bar completely.

## Truth files

* `xml/<device>/screen/C6_Faceplates.xml`: TIA Portal Openness export (ExportOptions WithDefaults). Faceplate instances
  are `Hmi.Screen.FaceplateInstance` with `FaceplateTypeName` "FP-C05 V 1.1.0". Interface values are
  `Hmi.Screen.InterfacePropertySimple` (tag connection or `Value`) and `Hmi.Screen.InterfacePropertyText`. Interface
  properties that keep the type default are not exported.
* `library/FP-C05_V1.1.0.xml`: TIA Portal Openness export of the type version (metadata only, see above).
* `runtime/<device>_C6_Faceplates_S<n>_runtime.png`: runtime simulation (WinCC Runtime Advanced Simulator V21) after
  `S<n>`. Screen area at native size, scrolled and stitched as in C03.

## How it was built

1. Source screen with the six objects imported into HMI_TP900 (TIA Portal Openness, SimaticML import).
2. TIA Portal UI: all objects selected, "Create faceplate", type `FP-C05`. In the faceplate editor:
   * interface properties `Text` and `Background_color` created by drag and drop of the object properties;
   * properties `FValue`, `FRange`, `FPct` and `FVis` added;
   * the process values and animations of the objects set to these properties.

   The type was released as V 1.1.0 and older versions were deleted.
3. `python _scripts/gen_c06.py <dir> <width> <height>` per device (uses the helpers of the C05 generator). Import of the
   screen with TIA Portal Openness. Start screen per device set in the TIA Portal UI.
4. Every HMI device compiled (software, rebuild all). Simulation started from the TIA Portal, `C6_Faceplates` captured
   after `S1` … `S4`.
