# C03_classic_hmi_dyn – classic WinCC (Comfort / RT Advanced), dynamics with VBS-simulated values

Dynamics of every screen object type of C01 (basic objects and elements): animations and tag connections, driven by
internal tags whose values are set by VB scripts. TIA Portal V21 Update 3, WinCC Comfort/Advanced device version
17.0.0.0. Archive: `samples/C03_classic_hmi_dyn.zap21`; it is the C02 project plus the content described here (only
the start screens of the three devices changed to `D_Bar_1`, and full-screen mode of HMI_PCADV is off so that the
runtime window can be scrolled on a desktop smaller than 1024×768).

## Devices

| Device | Screen size | `D_*` screens | Start screen |
|---|---|---|---|
| HMI_TP900 (TP900 Comfort) | 800×480 | 26 | `D_Bar_1` |
| HMI_TP1200 (TP1200 Comfort) | 1280×800 | 19 | `D_Bar_1` |
| HMI_PCADV (PC station, WinCC RT Advanced, `HMI_RT_1`) | 1024×768 | 20 | `D_Bar_1` |

## Content (same on every device)

* Tag table `C03_Tags` (internal tags, acquisition cycle 1 s): `D_State` Int, `D_Bool` Bool, `D_SInt` SInt, `D_Int` Int,
  `D_DInt` DInt, `D_USInt` USInt, `D_UInt` UInt, `D_UDInt` UDInt, `D_Real` Real, `D_LReal` LReal, `D_String` WString,
  `D_DateTime` DateTime, `D_Pct` Int, `D_Range` Int, `D_Bits` Int, `D_Vis` Bool, `D_En` Bool, `D_X` Int, `D_Y` Int,
  `D_Max` Real, `D_Min` Real, `D_TL` Int, `D_BarR` Real.
* Text lists: `TL_Value` (value/range, ListRange `Decimal`): −100..−1 "Negative (-100..-1)", 0 "Zero", 1..9 "One to
  nine", 10 "Ten: Ünïcödé äöüß ÄÖÜ €", default entry "Undefined value (default entry)"; `TL_Bit` (bit, ListRange
  `Bit`): 0 "Off (0)", 1 "On (1)"; `TL_BitNo` (bit number, ListRange `Binary`): 0–3 "Bit n set" (a bit number list has
  no default entry).
* VB scripts `SetS1` … `SetS4`: each writes one fixed set of values into all `D_*` tags (`SmartTags("…") = …`).
* Screens `D_<Type>_<n>`: per object type one instance per dynamic, object name = dynamic, a text field
  `Label_<object>` below the cell. A bottom bar on every screen: I/O field `State` (output of `D_State`), buttons
  `S1` … `S4` (call `SetS1` … `SetS4`) and `Next` (ActivateScreen of the next `D_*` screen, the last one leads back to
  the first). `instances/<device>.json` lists screen, object, type, dynamic, tag, details and position of every
  instance.

Dynamics per instance (all types, where TIA accepts them):

| Object | Dynamic | Tag | Configuration |
|---|---|---|---|
| `Static` | none | – | reference without dynamics |
| `A_Range` | RangeAppearanceAnimation | `D_Range` | 0..9 back 255,0,0 / fore 255,255,255; 10..49 back 0,160,0 / fore 0,0,0; 50..99 back 0,0,255 / fore 255,255,0 |
| `A_Flash` | RangeAppearanceAnimation | `D_Range` | 50..99 back 255,0,0 / fore 255,255,255, flashing Fast |
| `A_Bit` | SingleBitAppearanceAnimation | `D_Bool` | bit 0 = 0: back 128,128,128 / fore 255,255,255; = 1: back 255,255,0 / fore 0,0,0 |
| `A_Bits` | MultipleBitsAppearanceAnimation | `D_Bits` | bit 0 red, bit 1 green, bit 2 blue, bit 3 orange (back) |
| `V_Range` | VisibilityAnimation | `D_Range` | visible for 10..49 |
| `V_Bit` | SingleBitVisibilityAnimation | `D_Vis` | visible when bit 0 is 1 |
| `M_H`, `M_V`, `M_D` | Horizontal/Vertical/DiagonalMovementAnimation | `D_Pct` | range 0..100, delta x 40 / y 30 |
| `M_Direct` | DirectMovementAnimation | `D_X`, `D_Y` | offset from the configured position |
| `E_Range` | ObjectEnablingAnimation | `D_Range` | disabled for 10..49 |
| `E_Bit` | SingleBitObjectEnablingAnimation | `D_En` | enabled when bit 0 is 1 |
| `PV_*` | tag connection of ProcessValue (and MaximumValue/MinimumValue) | see `instances` | I/O field: every integer and floating point type, string, shifted decimal point, narrow and wide format patterns, output mode; symbolic I/O field with each text list; date/time field; bar, slider, gauge (default range, fixed range, range from tags); switch; symbol library |

## States

The states S1 … S4 are set by the buttons; after the start of the runtime all tags are 0 / empty (not captured).
Values (`D_X`, `D_Y` depend on the screen size, see `states/<device>.json`):

| Tag | S1 (minimum, negative) | S2 (typical) | S3 (maximum, overflow) | S4 (special) |
|---|---|---|---|---|
| D_Bool | False | True | True | False |
| D_SInt | -128 | 12 | 127 | -1 |
| D_Int | -32768 | 1234 | 32767 | -1 |
| D_DInt | -2147483648 | 123456 | 2147483647 | 0 |
| D_USInt | 0 | 200 | 255 | 1 |
| D_UInt | 0 | 40000 | 65535 | 1 |
| D_UDInt | 0 | 3000000000 | 4294967295 | 1 |
| D_Real | -999.999 | 3.14159 | 123456.789 | 0.0005 |
| D_LReal | -0.00000015 | 2.718281828 | 1E+300 | -0.0 |
| D_String | (empty) | Hello World | Ünïcödé äöüß ÄÖÜ € – long text that is wider than the field 0123456789 ABCDEFGHIJ | äöü ÄÖÜ ß |
| D_DateTime | 1990-01-01 00:00:00 | 2026-11-22 13:45:30 | 2099-12-31 23:59:59 | 1970-01-01 00:00:00 |
| D_Pct | 0 | 50 | 100 | 150 |
| D_Range | 5 | 25 | 75 | 200 (no range) |
| D_Bits | 1 | 2 | 4 | 9 (bits 0 and 3) |
| D_Vis | False | True | True | False |
| D_En | False | True | False | True |
| D_X / D_Y (TP900) | 10 / 10 | 200 / 120 | 533 / 240 | -20 / 0 |
| D_Max / D_Min | 100 / 0 | 200 / -100 | 50 / -50 | 100 / 0 |
| D_TL | -5 | 5 | 10 | 50 (undefined) |
| D_BarR | -5 | 0 | 5 | 2.5 |

## Truth files

* `xml/<device>/screen/D_*.xml`, `tagTable/C03_Tags.xml`, `textList/*.xml`, `vbScript/SetS*.xml`: TIA Portal Openness
  export (ExportOptions WithDefaults).
* `runtime/<device>_<screen>_S<n>_runtime.png`: screenshot of the runtime simulation (WinCC Runtime Advanced Simulator
  V21) at native size, screen area only, after pressing `S<n>`. The simulator window was larger than the desktop
  (1204×664 during capture); the screen area was captured scrolled and stitched. The text fields `Label_*` are 16 px
  high; with the larger fonts of TP1200 and PC RT Advanced the underscores of the labels are cut off.
* `instances/<device>.json`, `states/<device>.json`: instances and state values as generated.
* Flashing (`A_Flash` in S3) is captured in whatever flash phase the runtime had.

## Findings while building

* Slider: a Real process tag is a compile error; tags on MaximumValue/MinimumValue end in an internal compiler error
  ("Linker: data of a value object referenced by a value relation is not the specified type"), so the slider has a
  fixed range instead. Bar and gauge accept Real tags on MaximumValue/MinimumValue.
* I/O fields with integer tags need a format pattern without decimal places (the default `999.999` is invalid), also
  with ShiftDecimalPoint.
* SimulateTag cannot be used on a button of a Comfort panel ("not allowed at this usage location"); the values are
  set by the VB scripts only.
* The Comfort runtime simulation often ends on the first start after a compile ("configuration file could not be
  loaded"); the second start works.

## How it was built

1. `python _scripts/gen_c03.py All_Objects.xml <dir> <width> <height>` per device (input: the C01 export of
   `All_Objects`), import of tag table, text lists, VB scripts and screens with TIA Portal Openness (SimaticML import),
   a second screen import pass links the `Next` buttons.
2. Start screen `D_Bar_1` per device in the TIA Portal UI, simulation started from the TIA Portal, every screen
   captured after `S1` … `S4`, then `Next`.
