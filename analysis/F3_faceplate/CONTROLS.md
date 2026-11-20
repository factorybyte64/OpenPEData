# F3 list item 18: controls in the faceplate type (FP_Controls)

As of 2026-10-21, read-only: `samples\F05_fp_iface_instance.zap21` (identical to F03 for FP_Controls). Comparison: screen FP_Controls of HMI_1 in the same project, Openness truth `F01_hmi_allobjects_truth\HMI_1\screen_FP_Controls.full.json`.

## What is taken over

The screen FP_Controls has 15 controls. 4 remain in the type FP_Controls:

| taken over | not taken over |
| --- | --- |
| C_AlarmControl1 (HmiAlarmControl), C_Trend1 (HmiTrendControl), C_DetParam1 (HmiDetailedParameterControl = Parameter set control), C_Faceplate1 (HmiFaceplateContainer) | C_AlarmLine1, C_TrendCompanion1, C_FTrend1, C_Web1, C_Media1, C_SysDiag1, C_ProDiagOv1, C_GraphOv1, C_CritAnalysis1, C_PlcCode1, C_ScreenWin1 |

This matches the TIA help (DOC-FEATURES #15): in the faceplate type, the only controls allowed are alarm control, trend control, faceplate container and parameter set control. TIA discards the rest on "Create faceplate" without a trace in the type. Likewise, the HmiAlarmIndicator (E_AlarmInd1) is missing in FP_Elements.

## Storage: same object kind, more explicit values

In the type, the controls are `ILScreenItemData` as on the screen (the container is `ILFaceplateContainerData`) with `ICoreAttributes`, `IPropertyExpandoAttributes`, `IReferenceExpandoAttributes`. Differences:

1. **StyleItemClass** (expando 0x0875) is always set in the type (= subtype), not on the screen (SCRIPTS.md).
2. **Sub-objects as map expandos.** In the type, the controls carry expandos whose value is a MapT (with varint length prefix = overall length incl. prefix). On the screen they are missing; the default values apply there.
   - **0x0051 Caption** on all four: `{_TypeId 40009, 0x021c Font, _Guid}`. Openness shows `Caption` with `Font` on every control; 40009 is the same identifier as `_TypeId` in the container expando 51 from F02.
   - **0x021c Font**: `{_TypeId 20034, _Guid, 0x054e → font list}`. 0x054e names an entry in `IReferenceExpandoAttributes`; its index points in `ExpandoReferences` to a **HmiFontListData `FontList_C<number>`** of the hidden device. This clarifies what the content-dependent font lists are for (HIDDEN-DEVICE.md): every font used in the type is created as a separate list and attached via this reference. Basic objects and elements in the type (text, button, I/O field …) also have 0x021c directly.
   - **Column lists**: e.g. alarm control 0x3832 → 0x0011 = CoreMapArray of the columns `{0x07 visible (bool), 0x05 width, 0x3833 column ID, 0x3811 header text {_TypeId 40019, 0x024f → text}, _TypeId 40017, 0x03 name}`: ID 40 visible, Name 80 visible, Alarm class 100, Priority 100, Group 100, … Parameter set control 0x401a → 0x0011: Name 270, Value 120, Unit of measurement 180. Toolbars 0x214b → 0x243e: list `{0x12 button ID, 0x07 bool}`.
   - The header texts are stored in the type as separate text values (GenericFormattedText "ID", "Name", "Alarm class" …) in `IReferenceExpandoAttributes`.
3. **New MapT type codes:** `0x01` bool (1 byte), `0x15` nested map (u32 offset in the value area to a blob with MapT), `0xcd4001` = metamodel type ID of CoreMapArray (u32 offset to an array of maps). Open: `0x31d00` (C_Trend1 0x3839), `0x72` (ListBox, RadioButtons), `0x76` (CheckBox).

The names Caption / Font / FontList are derived from Openness and not yet individually checked against a modified sample. Openness does not output the column lists in `get detail=full` (only AlarmView/ParameterView settings), so the truth for them is still missing.

## Parser and test

- `faceplates.py`: `decode_map` understands 0x01, 0x15, 0xcd4001 and the length prefix; per screen item, new `parts` = all map expandos that hmi.py does not name, with keys "0x0051 Caption", "0x021c Font", "0x054e FontList" etc.; references to `IReferenceExpandoAttributes` are resolved to object names via `ExpandoReferences` (FontList_C…).
- `test_faceplates.py`: FP_Controls has exactly C_AlarmControl1, C_DetParam1, C_Faceplate1, C_Trend1, each with Caption (40009) and a resolved font list; in all types, every FontList reference points to a FontList_C object.

## Open

- F33: alarm control and trend control with modified configuration (columns, trends, configuration string), to check the column and trend lists against the TIA UI; codes 0x31d00, 0x72, 0x76.
