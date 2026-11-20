# Unified property IDs (H-029), derived

As of 2026-10-20 (U10). Generated with `tools\unified_propmap.py` from F01_hmi_allobjects (faceplate thread, read only) and the samples 115–129: all stored entries of `IPropertyExpandoAttributes` (slot name according to H-020; previously `IPropertyDynamicExpandoAttributes`) of the ILScreenItemData objects, value decoded per type code and compared with all truth attributes of the same object (`itemGetFull`, `screens_full.json`, `partGet` for Points/SelectionItems). Columns: name bytes of the expando dictionary (0x00070011), type code, occurrences, hits of the best candidate, candidates (bool candidates such as Enabled/Visible are chance hits).

## In `parser\openpedata\hmi.py` (PROPERTY_NAMES)

| Name bytes | Property | Value kind (code) | Evidence |
| --- | --- | --- | --- |
| 05, 06 | Width, Height | inline (19) | 100 % |
| 15, 16 | CenterX, CenterY | inline (5) | 100 % |
| 2d, 2e | AngleRange, StartAngle | inline (5) | 100 % |
| 63, 64 | Left, Top | inline (5) | 100 % |
| 78, 7c, 7d | Radius, RadiusY, RadiusX | inline (19) | 100 % |
| 7f, 0202 | X2, Y2 | inline (5) | 100 % |
| 0402 | LineWidth | inline (2) | 4/4 (U10) |
| 0e | TabIndex | inline (18) | 1/1, E_TouchArea1 TabIndex 7 (U10) |
| 66, 69, 2302, 2b21, 0403 | BackColor, BorderColor, ForeColor, LineColor, FlashingColor | Color: varint 8 + 3 bytes + u32 ARGB (90125) | 100 % |
| 1f | CaptionColor | Color | faceplate thread |
| 4302, 4902 | OutputFormat, ProcessValue (static) | varint(n+1) + UTF-8 (11); ProcessValue as a number on bar/gauge: code 5, Int32 in the offset word (Openness 30 → 0x1E, 55 → 0x37, 2026-11-08) | 100 % |
| 7508 | StyleItemClass | Text | faceplate thread |
| 0d02 | Points | List (0x00cd4002): u32 overall, u32 count, offsets, each u32 12, i32 X, i32 Y | 128: 3 points = `partGet Points` (U10) |
| 4f02 | Text | Reference (11): varint 5 + 4 bytes = name of an entry in `IReferenceExpandoAttributes` whose value is CoreText (LCID 127 = GUID, 0xFFFF = default text) | 118, 122, F01 (U10) |
| 3409 | SelectionItems | List (0x00cd4001) of nested expandos (`_TypeId`, text reference 4f02, `_Guid`) | 129: 3 entries = `partGet SelectionItems` (U10) |
| 08 | Layer | Reference: value is the layer GUID (text) | faceplate thread; on every object |

Openness shows texts per HMI language (`HmiLanguageAndFontSettingsData.OnlineLanguageOrder`): own text, otherwise default text (LCID 0xFFFF). Only what deviates from the type default is stored.

## Open

- 7904 (code 0x1400b = 81931, value 7 bytes 0): on every HmiAlarmIndicator, HmiAlarmControl, HmiAlarmLineControl, also with defaults (135_hmi_item_alarmind). Conjecture: `Filter` (Openness reports it as `unsetProperties`), not evidenced.
- 0103 (code 21 = structure, inline 13): only on F01 E_AlarmInd1, whose AlarmState deviates from the default (BackColor 0xFFFF9900), not in 135 (defaults). Conjecture: nested state `AlarmState`, not evidenced. Evidence would require a sample with `partSet` per nested part (AlarmState/NoAlarmState) – U11 left as open.

## Dynamizations (U10/U11)

- `IPropertyExpandoAttributes._#Dynamics` = nested expando: name = property ID (as above; additionally 07 = Visible), value = key of an entry in `IPropertyDynamicExpandoAttributes`; this has `Type` (Tag, Script, Flashing), `Tag`, `TwoWay`, `ValueConverter` → `MappingTable` → `Entries` (array) + `ConditionType` (0 None, 1 Range, 5 Singlebit).
- Range entry: `Value` (encoding of the property: color varint 8 + 3 bytes + ARGB; number code 0x13 inline), `From`, `To`, `RangeType`; single-bit entry: `BitmaskType`, `Value`, `Relevant`, `Condition` (bitmasks raw). Evidence: 132 (2 color ranges), 134 (3 number ranges), F01 (Range, Singlebit).
- Nested expando: value table with scalars inline (bool 1, int 4, GUID 16, code 0x13 4 bytes), otherwise u32 offset from the start of the table.
- Script tag references (H-031): `Tags("<id>")` – the number is the name of an `IScriptLinkExpandoAttributes` entry, value (u32 big endian) = index into the `ExpandoReferences` links of the script (0 = the object itself). Openness shows the tag name; the parser substitutes likewise (F01: all 10 scripts equal to the truth).
- F40 (Siemens demo project, 2026-10-22): further types `ResourceList` and `Expression`, `ConditionType` 6 = Expression. Reference fields (`Tag`, `ResourceList`, `Script`, elements of `TagRefs`) = varint 5 + 4-byte key = name of an `IPropertyDynamicLinkExpandoAttributes` entry; its value (u32 big endian) = index into the `ExpandoReferences` links of the item in stored order incl. empty entries (as H-031). Example Text_3: ResourceList → 2 → HmiTextListData `Units`, Tag → 3 → HmiTagData `UnitIndex`.
- Expression: `TagRefs` = array (u32 overall, u32 count, offsets, each 5 bytes reference); mapping entry `Condition` = string (varint n+1), tags in it as `$<4-byte key>$`, operators without spaces (`$k1$AND$k2$`). The parser shows `'tagname'`, e.g. `AND8('Word1',48) == 16 -> 0xFF5ED1AD`.
- Value table code 0x02 = 1 byte inline (bool/enum, e.g. Visible, BorderWidth ranges). Previously read as an offset → ranges like `256..0->259` instead of `1..1->3`.
- Tag names of structure members (`HmiStructureMemberTagData`) via `MembersInverse` with a path as in Openness (`HeaterControl1.actualTemperature`).
- Cause of "ResourceList None"/"Expression None": `dynamizations()` only knew Tag/Script and took the tag from the link list instead of from the reference field.
- Comparison with Openness (2026-10-22, copy of the demo project, 12 further screens in `F40_..._truth\screen_dyn1_*.full.json` and `screen_dyn2_*.full.json`): ResourceList (Text/Graphic, list + index tag), Expression (conditions and values per entry), Script, Flashing and Range entries match; `test_hmi` F40 538/538.
- Flashing: `Value`/`AlternateValue` u32 ARGB (= Color/AlternateColor), `FlashingRate`, `FlashingCondition` (strings). Range/Expression entries can additionally carry `Flashing` (bool) and `AlternateValue`.
- Range values of reference properties (Screen of a screen window) = varint 5 + 4-byte key → name of the screen (`1116_swScreen`); an empty value is missing in Openness (no `value`).
- Property IDs only dynamic (all evidenced against Openness): 6e = BorderWidth, 6e24 = IsAlternateState, 71 = AlternateBackColor, 050c = Screen, 7402 = Graphic.
- A `_#Dynamics` name without an entry in `IPropertyDynamicExpandoAttributes` is not a dynamization but belongs to a property event (`propertyEvents`, F40 txtAlarm_MachineUnitAssyPartColumn Text); the parser leaves it out.
- Length prefix (varint) of strings = overall size incl. the varint itself (`attrcodec.read_var`): with 1 byte n+1, with 2 bytes n+2. `hmi.py` read n+1 → strings from 127 bytes on (script tokens, texts, conditions) took along one byte of the following token (`Screen.<0x0f>Items`).
- Fixed (d224666): `attrs.decode_simple_expando` read string values with a double length prefix and lost the first character (layer GUID). The reference expandos are "simple" expandos (names UTF-16LE, type codes 0x8000001f CoreText / 0x8000000b String).

## Derivation table
| name bytes | code | n | hits | best candidates (attribute:value kind) | items (first) | first value |
| --- | --- | --- | --- | --- | --- | --- |
| 05 | 19 | 48 | 48 | Visible:bool, Width:int-signed, Enabled:bool, Width:int | B_GraphicView1,B_Line1,B_Polygon1 | int=120; int-signed=120 |
| 06 | 19 | 48 | 48 | Height:int-signed, Height:int, Visible:bool, Enabled:bool | B_GraphicView1,B_Line1,B_Polygon1 | int=60; int-signed=60 |
| 08 | 11 | 60 | 0 | - | B_CircArc1,B_Circle1,B_CircleSeg1 | i32=1662003535; raw=4f291063 |
| 0e | 18 | 2 | 2 | TabIndex:int, Visible:bool, TabIndex:int-signed, Enabled:bool | E_TouchArea1 | int=7; int-signed=7 |
| 15 | 5 | 12 | 12 | Visible:bool, ShowFocusVisual:bool, Enabled:bool | B_CircArc1,B_Circle1,B_CircleSeg1 | int=80; int-signed=80 |
| 16 | 5 | 12 | 12 | Visible:bool, ShowFocusVisual:bool, Enabled:bool | B_CircArc1,B_Circle1,B_CircleSeg1 | int=150; int-signed=150 |
| 2d | 5 | 8 | 8 | AngleRange:int-signed, AngleRange:int, Visible:bool, ShowFocusVisual:bool | B_CircArc1,B_CircleSeg1,B_EllArc1 | int=240; int-signed=240 |
| 2e | 5 | 8 | 8 | Visible:bool, ShowFocusVisual:bool, StartAngle:int-signed, StartAngle:int | B_CircArc1,B_CircleSeg1,B_EllArc1 | int=30; int-signed=30 |
| 63 | 5 | 40 | 40 | Visible:bool, Enabled:bool | B_GraphicView1,B_Line1,B_Polygon1 | int=20; int-signed=20 |
| 64 | 5 | 39 | 39 | Visible:bool, Enabled:bool | B_GraphicView1,B_Line1,B_Polygon1 | int=20; int-signed=20 |
| 66 | 90125 | 41 | 41 | BackColor:color | B_Circle1,B_CircleSeg1,B_Ellipse1 | color=0xFFFF0000; i32=0 |
| 69 | 90125 | 2 | 2 | BorderColor:color | B_Polygon1 | color=0xFF800080; i32=-2147483648 |
| 78 | 19 | 6 | 6 | Visible:bool, ShowFocusVisual:bool, Enabled:bool, Radius:int-signed | B_CircArc1,B_Circle1,B_CircleSeg1 | int=40; int-signed=40 |
| 7c | 19 | 6 | 6 | RadiusY:int-signed, RadiusY:int, Visible:bool, ShowFocusVisual:bool | B_EllArc1,B_Ellipse1,B_EllipseSeg1 | int=30; int-signed=30 |
| 7d | 19 | 6 | 6 | RadiusX:int-signed, RadiusX:int, Visible:bool, ShowFocusVisual:bool | B_EllArc1,B_Ellipse1,B_EllipseSeg1 | int=60; int-signed=60 |
| 7f | 5 | 2 | 2 | X2:int-signed, Visible:bool, X2:int, ShowFocusVisual:bool | B_Line1 | int=96; int-signed=96 |
| 0103 | 21 | 1 | 1 | IsFlashingRequired:bool, Enabled:bool, Visible:bool, ShowFocusVisual:bool | E_AlarmInd1 | int=13; int-signed=13 |
| 0202 | 5 | 2 | 2 | Y2:int-signed, Visible:bool, Y2:int, ShowFocusVisual:bool | B_Line1 | int=48; int-signed=48 |
| 0402 | 2 | 8 | 8 | Visible:bool, ShowFocusVisual:bool, LineWidth:int-signed, Enabled:bool | B_CircArc1,B_EllArc1,B_Line1 | int=3; int-signed=3 |
| 0403 | 90125 | 1 | 1 | FlashingColor:color | E_AlarmInd1 | color=0xFF00FF99; i32=-1728053248 |
| 0502 | 5 | 1 | 1 | RotationAngle:int-signed, X1:int, RotationCenterX:int-signed, TabIndex:int-signed | B_Line1 | int=0; int-signed=0 |
| 0702 | 5 | 1 | 1 | RotationAngle:int-signed, X1:int, RotationCenterX:int-signed, TabIndex:int-signed | B_Line1 | int=0; int-signed=0 |
| 0d02 | 13451266 | 5 | 1 | Points:points | B_Polygon1,B_Polyline1,Polyline_1 | points=[{"X":0,"Y":0},{"X; f64=1.0609979009e-313 |
| 1c02 | 21 | 12 | 12 | Visible:bool, Enabled:bool, ShowFocusVisual:bool | B_Text1,E_Bar1,E_Button1 | int=26; int-signed=26 |
| 1d09 | 21 | 4 | 4 | Visible:bool, VisualizeQuality:bool, ShowFocusVisual:bool, Enabled:bool | E_Bar1,E_Clock1,E_Gauge1 | int=287; int-signed=287 |
| 2302 | 90125 | 4 | 4 | ForeColor:color | B_Text1,E_Button1 | color=0xFF0000CC; i32=-872415232 |
| 2609 | 21 | 3 | 3 | Visible:bool, VisualizeQuality:bool, ShowFocusVisual:bool, Enabled:bool | E_Bar1,E_Gauge1,E_Slider1 | int=472; int-signed=472 |
| 2b21 | 90125 | 8 | 8 | LineColor:color | B_CircArc1,B_EllArc1,B_Line1 | color=0xFF0000FF; i32=-16777216 |
| 3409 | 13451265 | 7 | 0 | - | E_CheckBox1,E_ListBox1,E_Radio1 | f64=3.80261645742247e-; raw=000004000000180000 |
| 3723 | 21 | 1 | 1 | CurvedScale.AutoScaling:bool, Visible:bool, VisualizeQuality:bool, ShowFocusVisual:bool | E_Gauge1 | int=9; int-signed=9 |
| 3823 | 21 | 2 | 2 | StraightScale.AutoScaling:bool, Visible:bool, VisualizeQuality:bool, ShowFocusVisual:bool | E_Bar1,E_Slider1 | int=7; int-signed=7 |
| 4302 | 11 | 2 | 2 | OutputFormat:str | E_IOField1 | i32=2100446843; str={F2} |
| 4809 | 21 | 1 | 1 | Visible:bool, ShowMinutes:bool, VisualizeQuality:bool, ShowFocusVisual:bool | E_Clock1 | int=194; int-signed=194 |
| 4902 | 11 | 1 | 1 | ProcessValue:str | IOField_1 | f64=8.965925929385746e; str=HmiTag_IO |
| 4f02 | 11 | 8 | 0 | - | B_Text1,Button_1,E_Button1 | i32=1446449478; raw=46113756 |
| 7508 | 11 | 25 | 24 | itemType:str, type:str, StyleItemClass:str | B_CircArc1,B_Circle1,B_CircleSeg1 | f64=1.3808062555647622; str=HmiGraphicView |
| 7904 | 81931 | 1 | 1 | TabIndex:i32 | E_AlarmInd1 | color=0x00000000; i32=0 |
