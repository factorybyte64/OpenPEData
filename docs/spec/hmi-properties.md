# WinCC Unified: property IDs of the screen items (partly established, U6)

Evidence: `tools\unified_propmap.py` on the user's machine, table `samples\UNIFIED-PROPERTIES.md`, derived from F01_hmi_allobjects (47 objects) and samples 115–124 against the Openness truth. **20 IDs with 100 % hits, 157 of 207 stored values (76 %).** Only what deviates from the type default is stored; the default is not in the plf but in the type (Openness).

## Encoding (name bytes → property, type code)
| Encoding | IDs |
|---|---|
| Inline int (code 5/19) | 05 Width, 06 Height, 15 CenterX, 16 CenterY, 2d AngleRange, 2e StartAngle, 63 Left, 64 Top, 78 Radius, 7c RadiusY, 7d RadiusX, 7f X2, 0202 Y2, 0502 X1, 0702 Y1 |
| Enum inline (code 5) | 0102 EndType, 0602 StartType (HmiLineEndType: 0 Line, 1 EmptyArrow, 2 Arrow, 3 ReversedArrow, 4 EmptyCircle, 5 Circle) |
| Color (code 90125: varint 8 + 3 bytes + u32 LE ARGB) | 66 BackColor, 69 BorderColor, 2302 ForeColor, 2b21 LineColor, 0403 FlashingColor |
| String (code 11: varint n+1 + UTF-8) | 4302 OutputFormat, 4902 ProcessValue (static only, without tag dynamization); a static numeric value on bar/gauge is stored as code 5 directly in the offset word (Int32) |

## Addition from the faceplate thread (58d0fa0, checked on 64 objects in F02_hmi_faceplates_crash, see hmi-faceplate.md)
| 08 | Layer: the value is the name of an IReferenceExpandoAttributes entry whose value is the layer GUID |
|---|---|
| 1f | CaptionColor (ARGB like the other colors) |
| 7508 | StyleItemClass |

## Addendum U10 (commit 072d50c): 37 IDs, 438 values, 99.5 % covered
| ID | Property | Encoding |
|---|---|---|
| 0402 | LineWidth | inline, code 2 |
| 0e | TabIndex | inline, code 18 |
| 0d | Opacity | inline, code 7 (float32; Openness 0.5 -> 0x3F000000) |
| 7a08 / 7908 | HorizontalTextAlignment / VerticalTextAlignment | inline, code 5, Openness enum (Left 0, Center 1, Right 2 / Top 0, Center 1, Bottom 2) |
| 021c | Font (part) | 0x054e = name of the FontList; its font element '<family>;<size>;...;<weight>' |
| 0d02 | Points (polyline/polygon) | list: u32 overall, u32 count, offsets, per entry u32 12, i32 X, i32 Y (sample 128_hmi_item_polyline) |
| 4f02 | Text | reference: varint 5 + 4 bytes = name of an entry in IReferenceExpandoAttributes whose value is CoreText |
| 3409 | SelectionItems | list of nested expandos (`_TypeId`, text reference 4f02, `_Guid`) (sample 129_hmi_item_listbox) |
Open only 0103 (code 21) and 7904 (code 81931) on HmiAlarmIndicator/alarm controls (2 of 438 values; presumably references to nested states AlarmState/NoAlarmState).
Texts: per HMI language (`HmiLanguageAndFontSettingsData.OnlineLanguageOrder`) its own text, otherwise default text (LCID 0xFFFF); CoreCulture objects are not the project languages.
Reference expandos are "simple" expandos (names UTF-16LE, codes 0x8000001f/0x8000000b). Fixed error (d224666): `decode_simple_expando` read strings with a double length prefix (layer GUID lost its first character); baseline over 97 samples, JSON unchanged.

## Parser output (hmi.py)
Per screen the objects with type (Subtype), all decoded properties, text (CoreText), tag dynamizations and events with script code (token table in HmiScriptEmbeddedData, tokens are joined). Gaps: control texts (first CoreText instead of column headers), spaces/line breaks in script code (only single-line scripts evidenced), faceplate containers without interface (faceplate thread).

U10 (e615c08): the parser additionally outputs text lists with entries (From/To/type/default/text), cycles (time/unit/number) and counters for system tags/internal system tags/system events; test_hmi 59/59.

Addendum U11: 7904 (code 0x1400b) presumably filter, 0103 (code 21) presumably nested AlarmState; both still open (2/438 values).
Addendum F40 (286bb93): 6e = BorderWidth, 6e24 = IsAlternateState (confirmed); value table code 0x02 = 1 byte inline. Open: 050c (presumably Screen), 7402 (presumably Graphic), 71 (script on I/O field/rectangle, unknown).
Addendum 031a7e5: 050c = Screen (HmiScreenWindow), 7402 = Graphic (HmiGraphicView), 71 = AlternateBackColor, confirmed against Openness. Strings ≥127 bytes: the length prefix counts its own bytes (like attrs.read_var).
