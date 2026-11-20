# WinCC Unified: styles and rendering templates in the PEData (Z1.5)

As of 2026-10-31. Plan: [PLAN-Z1.5-hmi-screens.md](../PLAN-Z1.5-hmi-screens.md). Display in the viewer: [hmi-rendering.md](hmi-rendering.md).
Tool: `tools\hmi_styles.py`. Evidenced on F01_hmi_allobjects and 165_hmi_item_defaults.

## Result

Every project contains, in the PEData, the three system styles in full for each HMI device: the style values per object type, the font references and the rendering templates (SVG) with which the runtime draws the objects. The chain **style → object default → file values** can therefore be filled from the project itself without installation files.

## Objects

| Type (metamodel) | Count | Content |
|---|---|---|
| `Siemens.Simatic.Hmi.DL.ILStyles.SystemStyleContainerData` | 1 per device | Name `ILSystemStyleContainer`, relation `Styles` to the three StyleData |
| `Siemens.Simatic.Hmi.DL.ILStyles.StyleData` | 3 per device | one system style (see below) |
| `Siemens.Simatic.Hmi.DL.ILStyles.StyleSettingsData` | 1 per device | `StyleSettingsParent` → HmiDeviceData, `Target` → HmiApplicationData, `IStyleSettingsAttributes_SelectedStyle` → active style, `IStyleSettingsAttributes_SystemStyles` → all three |
| `Siemens.Simatic.Hmi.Utah.Device.HmiStyleAndThemeSettingsData` | 1 per HMI device | core attributes only, on HmiDeviceData and HmiApplicationData |

"Per device" means: the HMI device and every hidden faceplate device (`HiddenHmiRTDeviceFaceplateTypes`, see [hmi-faceplate.md](hmi-faceplate.md)). F01 has 10 records. So far `ExtendedStyleV2101` is selected in all samples.

The system styles (V21): `FlatStyle_BrightV2101`, `ExtendedStyleV2101`, `FlatStyle_DarkV2101`. The expando `IPropertyExpandoAttributes.SystemStyleName` contains the base name without version, e.g. `FlatStyle_Bright`.

Further types in the metamodel, without objects in the samples so far: `Utah.Styles.HmiStyle*` (Style, StyleItem, StyleSheet, type and version), `Utah.Theming.HmiThemeData`, `IHmiStyleSettingsAttributes`, `IHmiThemeSettingsAttributes`. They probably belong to Comfort Panels and library styles.

## StyleData

Attribute sets of a StyleData object:

| Set | Key | Value |
|---|---|---|
| `IPropertyExpandoAttributes` | `SystemStyleName` | Text |
| `IPropertyExpandoAttributes` | Object type (`HmiScreen`, `HmiRectangle`, `HmiButton`, …, 52 types) | Style values of the type, blob in the format below |
| `IPropertyExpandoAttributes` | `IsSystemStyle` (bool), `Version`, `RuntimeVersion` (text) | only for a style from a library (F40: `SiemensStyleLibrary_1_0` with false, "1.0.0", "21.0.0.0"); describe the style itself, parser output `style.info` |
| `IReferenceExpandoAttributesSimple` | 4 characters (e.g. `MeK[`) | 5 bytes: u32 big-endian = index into the links `ExpandoReferences`, then 1 byte 00 |
| `IRenderingTemplateExpandoContainer` | Template name (`style.extended.button.default`, `include.style.extended.scrollbar`, …) | Object with `Content` (SVG) and `_Guid` |
| `ISystemPartPropertyExpandoAttributes` | ? | not yet analyzed |

The links `ExpandoReferences` point to `HmiFontListData` (e.g. `FontList_SiemensSans_14`, `…_22_700`). A font in the style is an object with the single field `Description`; the value is a key from `IReferenceExpandoAttributesSimple`. Thus `MeK[` → index 0 → `FontList_SiemensSans_14`. This matches the Openness default of the text (SiemensSans 14).

## Blob format

A value starts with a varint overall length. It is followed by an **object**:

| Field | Length | Meaning |
|---|---|---|
| Flag | 1 | 0 = uncompressed, 1 = zlib in chunks |
| Length | u32 | Length of the payload (uncompressed) |
| — | 9 | Zero bytes |
| Marker | 1 | 0x10 |
| n | u32 | Number of chunks |
| Chunk table | 1 + 4n | Meaning open (n = 1: `01 01 00 00 00`, n = 2: `02 03 00 …`, n = 3: `03 07 00 …`) |
| Payload | | Flag 0: direct. Flag 1: n × (varint length + zlib stream); each chunk decompresses to 4096 bytes, the last one with padding bytes |

The payload of an object (**body**):

| Field | Meaning |
|---|---|
| u32 | Length of the body |
| u32 n | Number of fields |
| u32 | Offset of the data part from the start of the body |
| n × u32 | Type code per field |
| n × name | Length + 1, then UTF-8 (0 = null) |
| Data part | per field in name order: fixed-length value or u32 offset (from the data part) into the variable part, which follows directly |

Type codes:

| Code | Type | in the data part |
|---|---|---|
| 0x80000001 | bool | 1 byte |
| 0x80000002 | uint8 | 1 byte (e.g. BorderWidth) |
| 0x80000005 | int32 | 4 bytes (also enums as a number, e.g. `Content.TextPosition` 1 = Right) |
| 0x80000007 | float | 4 bytes (`OverviewDetailRatio` 0.5) |
| 0x80000008 | double | 8 bytes |
| 0x80000012 | uint16 | 2 bytes (`TileWidthMax`) |
| 0x80000013 | uint32 | 4 bytes (Width, Height) |
| 0x8000000C | GUID | 16 bytes |
| 0x8000000B | Text | Offset → length + 1, UTF-8 |
| 0x8000000D | Bytes | Offset → object envelope as above, raw payload (SVG of the templates) |
| 0x80000015 | Object | Offset → object envelope as above, recursive (Font, Padding, Margin, Content, parts of controls) |
| 0x0001600D | Color | Offset → u32 8, then u32 ARGB little-endian (`d7 cd c8 ff` = 0xFFC8CDD7) |

Every object carries meta fields: `_Guid`, `_TypeId` (int32, e.g. rectangle 10011, screen 20001, font 20034), `IsDefault` (bool) and, at the top level, `StyleItemType` (int32) as well as `StyleItemClass` (text = object type).

In F01 (1590 values, 10 devices × 3 styles × 53 entries) and in 165 (159 values) all blobs can be decoded without remainder. The check verifies that the names end exactly at the data part and that the variable part starts directly after the fixed part.

## Content: only the deviations of the style

For each type, a style contains only the properties it defines. Everything else comes from the object default.

| Type | FlatStyle_Bright | ExtendedStyle | FlatStyle_Dark |
|---|---|---|---|
| Screen | BackColor 0xFFFFFFFF | BackColor 0xFFC0C0C0 | BackColor 0xFF282D3A |
| Rectangle | BackColor 0xFFC8CDD7, BorderColor 0xFF7D7D85 | same | same |
| Text | ForeColor 0xFF21292C, Font SiemensSans 14 | Font SiemensSans 14 | ForeColor 0xFFFFFFFF, Font SiemensSans 14 |
| Button | BackColor 0xFF7B92A2, ForeColor white, BorderWidth 2, template `style.flat.button.default` | BackColor 0xFFF2F4FF, BorderColor 0xFF64646A, template `style.extended.button.default` | BackColor 0xFF495265, template `style.flat.button.default` |
| I/O field | BackColor white, BorderColor 0xFF8197A6, BorderWidth 2 | BackColor white, BorderColor 0xFF64646A | BackColor 0xFF20242D, ForeColor white |

In addition, every type has a `FocusColor`, usually Bright 0xFF265CFF, Extended 0xFF7DCCF6 and Dark 0xFF53A9FF. For the I/O field, Dark uses 0xFF265CFF. The controls (alarm control, trends, diagnostics) have deeply nested parts such as `ToolBar.Elements.HmiControlBarButtonPart.Margin.Top`.

`RenderingTemplate` (text) names the SVG template with which the runtime draws the type.

**Comparison with Openness** (sample 165, ExtendedStyle, `itemGet detail=full`, 37 types):
- 338 style values are equal to the Openness value, among them all colors.
- 54 are enums that appear as a number in the style and as a name in Openness (e.g. `GridLineVisibility` 1 = Vertical, `WindowFlags` 27 = ShowCaption, ShowBorder, CanSize, CanMove). They match in meaning.
- No real deviation.
- 1530 style values are not in the Openness output. These are mainly parts of controls, `RenderingTemplate` and font references.

**Other styles** (samples 166_hmi_style_dark and 167_hmi_style_bright): these are copies of 165 in which the user switched the style in the TIA UI. Openness V21 can neither read nor set the selected style; the API only knows `StyleItemClass` per object.
- After switching, `IStyleSettingsAttributes_SelectedStyle` points to FlatStyle_DarkV2101 or FlatStyle_BrightV2101 respectively. The three StyleData themselves remain unchanged.
- Openness then returns the values of the new style, e.g. button in Dark BackColor 0xFF495265 and AlternateBackColor 0xFF4B6384, screen BackColor 0xFF282D3A.
- Comparison: Dark has 478 equal values and 67 enums, Bright 463 equal values and 67 enums. The only deviation is the height of the alarm line (style 30, Openness 28), because the object stored 28 itself.
- Hence: **Openness value = stored value, otherwise value of the selected style, otherwise object default.**

`parser\tests\test_hmi.py` checks this on 165, 166 and 167. It checks the selected style and every style value that the object does not store itself.

## Enums, parser output and viewer

**Enums:** style values store enums as a number. The numbers are the values of the Openness enums (`Siemens.Engineering.WinCCUnified.dll`).
- `tools\unified_enums.ps1` reads property name → enum type → values via reflection (metadata only) and writes `parser\openpedata\unified_enums.json`: 77 properties, 65 enum types, 10 of them flags.
- Properties whose name belongs to several enum types are left out, e.g. `EventType`.
- Flags are output as in Openness as "A, B" in value order (`WindowFlags` 27 = ShowCaption, ShowBorder, CanSize, CanMove).
- Checked against 165, 166 and 167: all 13 enum properties occurring there match (among others `TextPosition` 1 = Right, 3 = Bottom, 5 = InFront; `GridLineVisibility` 1 = Vertical via `HmiSimpleGridLine`).

**Parser output:** every HMI software has `style` with:
- `name` (selected style) and `systemStyle`,
- `values` per style entry (meta fields removed, enums as Openness names),
- `templates` (SVG text per template name).

Fonts are resolved to `{Name, Size, Weight}`. Path: key → font list (`HmiFontListData`) → its first `HmiFontItemData`. Its name reads "family;size;…;weight", e.g. "Siemens Sans;18;0;0;0;400".

**Viewer:**
- Effective properties = object default (`HmiDefaults.json`) → style values of the selected style for the `StyleItemClass` → stored values.
- `HmiDefaults.json` previously contained the Openness values from 165 and thus ExtendedStyle values, e.g. font 14 of the process diagnostics (object default 16).
- `tools\hmi_defaults.py` replaces values that the style of 165 sets with the values from 166/167 where their style does not set them (3). Otherwise it leaves them out (555), because every style supplies them.
- test_hmi checks this layering on 165/166/167 against all Openness values: 2190, 2412 and 2397 checks, all equal.

## Rendering templates

In ExtendedStyle, `IRenderingTemplateExpandoContainer` contains 103 templates: 43 `style.extended.*` and 60 `include.style.extended.*`. A value is an object with `Content` (bytes) and `_Guid`. `Content` is an SVG document with the Siemens DTD "SVG 1.0 TIA-HMI" and its own namespaces `hmi`, `hmi-bind`, `hmi-element`, `hmi-feature`, `hmi-event`.

Structure (element names, counted over all 103 templates):
- `hmi:self`: object type, name, version, performance class.
- `hmi:paramDef`, `hmi:localDef`: parameters and local variables, often with binding `hmi-bind:value="{{ … HmiProps.… }}"` to the object properties.
- `hmi:include`, `hmi:master-include`, `hmi:template`: reuse of the `include.*` templates.
- `hmi:if`, `hmi:action`, `hmi:setProperty`, `hmi:sendCommand`: states (pressed, hover, focus) and events.

**Which types have a template** (165, all three styles the same):
- 39 of 53 style entries name a `RenderingTemplate`; these are all operating and display objects and controls.
- Without a template are screen, faceplate type, rectangle, text, line, polyline, polygon, circle, ellipse, segments, arcs and graphic view. The runtime draws these basic shapes directly from the properties.
- Bright and Dark each have 99 templates `style.flat.*` and `include.style.flat.*`. They are identical byte for byte; the two styles differ only in the style values. ExtendedStyle has 103 templates of its own, `style.extended.*`.
- The templates are versioned, e.g. button version 2.9 in Flat and Extended.

**Binding language** (analysis over all 103 ExtendedStyle templates; numbers = occurrences):
- Expressions are in attributes `hmi-bind:<attr>` as `{{ … }}` (computed) or `{ … }` (direct binding). Mainly bound are `value` (3072), geometry (`x`, `y`, `width`, `height`, `rx`, `transform`), colors (`fill`, `stroke`, `stop-color`, `stop-opacity`), font (`font-family`, `font-size`, `font-weight`, `font-style`, `text-decoration`), plus `condition`, `for`, `scope`.
- Namespaces of the values:
  - `HmiProps`: properties of the screen item, i.e. file value over style over default.
  - `LocalProps`: local variables of the template.
  - `ParamProps`: parameters of an include.
  - `SysProps`: system state, e.g. focus.
- Operators: arithmetic, comparison (`==`, `gt`, `ge`, `lt`, `le`, `eq`, `ne`), logic (`and`, `or`, `not`), `? :`, `has`, rounding (`floor`, `ceil`, `abs`).
- Converters `Converter.*`: color (`RGBA`, `RGB`, `Alpha`, `Darker`, `Lighter`), text (`TextWidth`, `TextHeight`, `TextEllipsis`, `TextDecoration`, `FormatPattern`, `IsMultiline`, `GetTextByID`), lists (`CountItems`, `CountVisibleItems`), values (`Min`, `Max`, `Bounds`, `GetDefaultValue`, `IsNumber`, `ThresholdColorForValue`).
- Enums are referenced by name, e.g. `HmiContentMode.GraphicOrText` or `HmiOperability.Operable`. From this, the enum names of the style values can be derived.

**States and events:**
- `hmi:action` with `hmi:setProperty` on LocalProps, triggered via `hmi-event:down`, `up`, `mouseEnter`, `mouseHover`, `mouseLeave`, `tapped`, `pan`, `wheel` and `focusLeave`.
- Example button (ExtendedStyle):
  - Local states "pressed" and "hover".
  - Focus frame: border width + 2 in `FocusColor` if `ShowFocusVisual` is set and the object has the focus.
  - Background: vertical gradient from BackColor to a color 40 % darker; on hover 60 %, reversed when pressed.
  - Corners with radius 5.
  - Content (graphic and text) via `include.style.extended.content`.
  - Not operable: opacity 0.5.

**Building blocks:**
- `hmi:include template="include.…"` with `hmi:param`.
- `hmi:master-include` and `hmi:template`.
- Special runtime elements `hmi-element:*`: `textField`, `table`, `listContainer`, `straightDial`, `arcDial`, `chart`, `flowDiagram`, `content`, `scrollableArea`. They are not expressed in SVG; the viewer must implement them itself.

This is the description according to which the runtime and the web client draw. For the viewer this means:
- The shape and state logic of an object can be derived from the template of the active style instead of being reimplemented.
- The templates belong to Siemens. Only the format and analyses go into the repo, no template contents. The viewer reads them at runtime from the respective project.

Extraction: `python -I tools\hmi_styles.py <project> --templates <folder outside the repo>`.

## Open

- `ISystemPartPropertyExpandoAttributes` of the StyleData.
- Meaning of the chunk table.
- Mapping of `_TypeId` and `StyleItemType`.
- Whether user styles or modified system styles are possible and where they are stored.
- Templates of the Flat styles.
