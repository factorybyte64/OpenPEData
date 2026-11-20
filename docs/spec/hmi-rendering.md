# WinCC Unified: rendering of screens in the viewer (Z1.5)

Status 2026-10-30. Plan: [PLAN-Z1.5-hmi-screens.md](../PLAN-Z1.5-hmi-screens.md).

## Values

**Decision User 2026-10-29:**
- The parser output contains only what is in the file.
- Only the viewer uses the Openness default values, for drawing.

Origin of the values:
- Default values: sample 165_hmi_item_defaults. In it, each of 37 screen item types has its own screen with one object that was created without attributes. Read with `TiaHmiUnified itemGet detail=full`.
- `tools\hmi_defaults.py` generates `ui\OpenPEData.Viewer\HmiDefaults.json` from it, which is embedded in the viewer as a resource.
- Effective properties of an object = default values of its type, overridden by the stored properties. Nested objects such as `Font`, `Padding` and `Corners` are overridden field by field.

Examples of default values (MTP700 Unified Comfort, TIA V21):

| Type | Default values |
|---|---|
| Screen | BackColor 0xFFC0C0C0, 800 × 480 |
| Rectangle | 96 × 48, BackColor 0xFFC8CDD7, BorderColor 0xFF7D7D85, BorderWidth 1, corners 0 |
| Text | 160 × 40, font SiemensSans 14 normal, ForeColor black, centered |
| Text box | like Text, plus BackColor 0x00F2F4FF (transparent), BorderWidth 0, Padding 2 |
| Line | X1/Y1 0/0 to X2/Y2 96/48, LineColor 0xFF7D7D85, LineWidth 1, CapType Round |

For a new object, the file contains only Width, Height, Text, X2/Y2 and the layer. test_hmi checks on sample 165 that every stored value matches the Openness value (257 checks); circles, ellipses and arcs have no Left/Width/Height in Openness, but CenterX/CenterY/Radius.

## Status of rendering (R1)

The following are drawn:

| Type | Properties |
|---|---|
| Rectangle | fill, border color and width, corner radii |
| Text | text of the first language, font (family, size, weight, italic, underline/strikethrough), foreground color, alignment |
| Text box | like Text, plus background, border, padding, trimming, wrapping |
| Line | X1/Y1/X2/Y2 relative to Left/Top, color, width, line caps, dash style |
| all types | opacity, visibility (invisible = not drawn, as in the runtime), rotation about the center + RotationCenterX/Y |
| Circle, ellipse | center CenterX/CenterY and Radius or RadiusX/RadiusY (screen coordinates, no Left/Top), fill, border |
| Circular/elliptical arc, segment | StartAngle (0° = 3 o'clock), AngleRange clockwise (help ObjectModelWCCU, CircleSegment.StartAngle/AngleRange); segment = arc closed through the center |
| Polyline, polygon | Points (X/Y relative to Left/Top), fill, line join |
| Rotation | RotationCenterPlacement AbsoluteFromCenter (center + X/Y), NormedFromCenter (center + X/Y × half size), AbsoluteToContainer (screen coordinates) |
| I/O field | like Text box, text = stored ProcessValue (editor view), otherwise "0" |
| Button | like Text box; alignment and trimming from `Content`; graphic follows with R3 |
| Symbolic I/O field | like Text box with a drop-down arrow on the right |
| Toggle switch | rounded track (BackColor, in state IsAlternateState AlternateBackColor), knob left or right in ForeColor; whether the knob has ForeColor or a style color in the runtime is open without a reference image |
| Graphic view | background, border, project graphic from `Graphic` (GraphicStretchMode); PNG/JPG directly, SVG via `SvgRenderer.cs` (own small reader without third-party library: path, rect, circle, ellipse, line, polyline, polygon, g, transform, fill/stroke/opacity, linear and radial gradients, clip-path, use, embedded images; text omitted); EMF not yet. Until 2026-10-31 every SVG with `path` threw an error on loading (frozen geometry) and was not drawn |
| Group | draws nothing itself; members (parser `parent`, link `ContainerParent`) are positioned relative to the group, nesting possible |
| other types | placeholder in the default colors of their type |

## Drawing order (R5)

- Layer before order in the screen, layer 0 at the bottom.
- The layers of a screen are in the screen expando `K` (property ID 0x24b):
  - structure: u32 overall size, u32 count (32), offset table;
  - per layer a nested expando with `_Guid` (.NET GUID, code 12), name `Layer_n` in field 3 and, if set, `_#ESVisible`.
- The property `Layer` of a screen item is this GUID.
- The order within the layer is the link order `ILScreenItems`. It is equal to the Openness order `ScreenItems` (checked on all F01 screens).
- Parser:
  - per screen `layers` (index, name, guid, esVisible);
  - per object `layerIndex` and `zIndex`. Both are values from the file.

Previously the list was sorted by name and objects were stacked incorrectly, e.g. in F40 211 the status circle under the white rectangle. Status after R5: (viewer image not in the repo; evidence: Siemens demo project)

Status after R2 (I/O field, toggle switch): (viewer image not in the repo; evidence: Siemens demo project)

The font `SiemensSans` is mapped to "Siemens Sans" (installed with TIA); without this font, Segoe UI is used.

Open: comparison with reference images from the runtime. Openness cannot create a WinCC Unified PC runtime (MCP-098), so template 164 from User is missing.

Example F40, screen 211_PropertyChangeEvent, viewer status R1: (viewer image not in the repo; evidence: Siemens demo project)

## Tools

- `OpenPEData.Viewer.exe <sample> --select <path to the screen> --screen-png screen.png` writes the screen at a scale of 1:1.
- With `--compare reference.png`, `screen_cmp.png` (reference | viewer | difference, deviating pixels red) and `screen.png.txt` with the share of deviating pixels (tolerance 24 per color channel) are created in addition.

## Graphics (R3)

- `Graphic` (property ID 0x274) is a reference. Its value (u32 big endian + 1 byte) points to one of the `ExpandoReferences` links of the object, which leads to `HmiImageData`. Applies to graphic view and button.
- Graphic:
  - `HmiImageData` (name) → `IHmiImageAttributes_DefaultInternalImage` → `HmiInternalImageData`;
  - there `FileExtension`, `ImageSize` (u32 size, u32 width, u32 height) and `GenuineContent`;
  - `GenuineContent` is the original file as a paged blob (`attrs.decode_blob`).
- Parser:
  - `project.graphics` with name, extension, width, height, bytes and data (base64);
  - on the object `Graphic` with the name of the graphic.
- F40: all 211 graphics decoded (123 PNG, 77 SVG, 9 JPG, 2 EMF), 6 of them library graphics (see below).

## Library graphics (graphic types, 2026-10-31)

- A graphic type is `ImageLibraryTypeData` with versions `ImageLibraryVersionData`. The image of a version is an `HmiImageData` with link `ContentToVersion` → version, otherwise structured like a project graphic. The name of this `HmiImageData` is usually just "Graphic".
- A reference to a library graphic has bit 0x80 in the last byte of the reference value. The index then points to the `ExpandoChildren` links of the object (not `ExpandoReferences`). Chain: `ImageInstanceData` → `IsInstanceOf` → `ImageLibraryVersionData` → `VersionObjectToTypeObject` → type. Applies in screens and in faceplate types.
- Parser:
  - per version of a graphic type `image` (structure like `project.graphics`); `project.graphics` now only contains `HmiImageData` without `ContentToVersion`;
  - on the object `Graphic` with the name of the graphic type;
  - faceplate screen items with `layerIndex`, `zIndex` and `parent` as in screens.
- Evidenced on F40 (gt_Motor, gt_robot, gt_forward, gt_backward from the type list). An Openness truth for the value of `Graphic` in faceplate types is missing; screens in F40 remain 538/538.
- Viewer: tab "Preview" on the graphic type (default version) and version; graphic views first search project graphics, then graphic types of the same name.

Example F40 1029 (pattern as an SVG graphic on the left, made of rectangles in a group on the right): (viewer image not in the repo; evidence: Siemens demo project)

## Style and rendering templates (2026-10-31)

Formats: [hmi-styles.md](hmi-styles.md).

**Values:** effective properties = object default (`HmiDefaults.json`, without style) → values of the selected style (parser `style.values`) → stored values. With this, the screen background and all objects appear in the style of the project (Extended, Flat Bright or Flat Dark).

**Template interpreter (prototype):** The viewer draws button, I/O field, text box and toggle switch from the rendering template of the selected style (`TemplateRenderer.cs`, `TemplateExpr.cs`). It reads the templates at runtime from the parser output; they are not in the repo.
- Expressions: `{{ … }}`/`{ … }`, also embedded as in `translate( {{ x }} , {{ y }} )`; operators, `? :`, functions `and/or/not/gt/…`, `Converter.RGBA/RGB/Alpha/Darker/Lighter/TextWidth/TextEllipsis/…`.
- Values come from HmiProps (effective properties), LocalProps, ParamProps, SysProps (focus off) and Layout.
- Runtime values that Openness does not name have fixed assumptions: `Operability` = Operable, no graphic. No `ProcessValue`: an I/O field without a tag stays empty in the runtime.
- Procedure:
  - `hmi:localDef` is evaluated in order, `hmi:if` applied.
  - `hmi:include` is expanded with its own scope: HmiProps from `hmi-bind:for`, ParamProps from `hmi:param`, Layout from the size. It is inserted as a nested `svg` with unique ids.
  - `hmi:text` becomes text.
  - `hmi-element:textField` is drawn as single-line text (alignment, padding, font from `hmi-feature:font`).
- Idle state: no hover, not pressed, no focus; actions and events are not executed.
- Not supported (omitted): `hmi-element:table/chart/listContainer/…`, `master-include`, thresholds (`ThresholdColorForValue` returns the fallback color).
- `Converter.Darker(c, f)` = channel × (1 − f), `Lighter` = channel + (255 − channel) × f, rounded. The end colors of the gradients of button and toggle switch match the runtime exactly. Whether the runtime rounds or truncates is open (no difference at the measuring points). Gradients match the runtime to ±1 step over the whole height (toggle switch x = 726, button x = 800, 164): linear interpolation in sRGB between the two stops of the template. The earlier observation "1 to 5 steps darker in the middle" was a measurement error, because the measuring column x = 740 grazed the grip grooves of the toggle switch. Both converters keep the alpha of the color, in the runtime and in the TIA screen editor alike (confirmed 2026-11-20, TIA V21 screen editor: buttons with BackColor `0x00F2F4FF`, `0x80F2F4FF` and `0xFFF2F4FF` over a red rectangle are drawn fully transparent with only the frame visible, half transparent and opaque). A button with a transparent BackColor therefore shows whatever lies below it, for example the graphic views of a faceplate. The earlier assumption that the editor draws the result opaque was wrong.

**Comparison with the runtime (164, 2026-10-31):** Screen_1 of the Unified PC runtime with the 37 object types from 165 (default values, arranged side by side), screenshot from WebRH against `--screen-png --compare`. The same screen with FlatStyle_Dark (copy 166) and FlatStyle_Bright (copy 167): 6.57 % and 6.87 % deviating pixels, Extended 2.91 %.

**Flat styles compared to Extended (runtime 166/167):** toolbar buttons flat in `HmiControlBarButtonPart.BackColor`, border in BorderColor/BorderWidth, spacing from Margin, without operability with Opacity 0.6 (template `include.style.flat.controlbarbutton`); option circles and check boxes without gradient with a 2 px border; alarm line in `AlarmLineView.BackColor`; fields of the diagnostics overview in `PDiagElements.AlternateBackColor`; web control with toolbar at the top and address field up to the right edge.
- Rectangle and ellipse: the border lies centered on the outline (half outside, half inside) and is anti-aliased. The viewer therefore draws them as a path instead of as Border/Ellipse.
- Symbolic I/O field: the template draws the drop-down button with a ComboBox runtime element. The viewer draws it according to the measured values: 30 px wide inside the border, top gradient BackColor darker 0.1 → darker 1/3, bottom darker 0.4, arrow centered.
- Alarm line (AlarmLineControl): white with black border, BackColor is not displayed.
- Alarm indicator (AlarmIndicator) without alarms and touch area: invisible in the runtime, the viewer does not draw it.
- List box, option button group and check box group with their `SelectionItems` (nothing selected): list rows 2 × font size high, every second one in `AlternateBackColor`, text without indentation. Group rows 3 × font size high within `Padding`. The selection symbol (circle 22 px, box 28 px) is centered in a square cell of the row height, the text follows the cell. The symbol has a gradient BackColor → darker 0.4, the group rounded corners. The dimensions are measured at the default values; `SelectionItemHeight` > 0 replaces the row height.
- Controls with `WindowFlags` (alarm, trend, web, media, diagnostics, parameter control, screen window) get a generic window frame. The colors come from properties and style: `CaptionColor` for frame and title bar, maximize/close according to `WindowFlags`, `ToolBar.BackColor`, `StatusBar.BackColor`, `BackColor` in between. The dimensions are measured: frame 2 px, title bar 32 px including frame, toolbar 70 px, status bar 38 px (upper half flat, lower half gradient to darker 0.2). A stored `WindowFlags` (0409, bits of HmiWindowFlag, evidenced via Openness) is output by the parser as names; without `ShowBorder` the frame is dropped, without `ShowCaption` the title bar.
- Media and web control show no status bar, although `StatusBar.Visible` is true. The screen window without `BackColor` is white.
- Toolbars: size and spacing of the buttons come from the style part `HmiControlBarButtonPart` (42 × 38, 16 px spacing at the top). Measured are the first button 11 px from the left edge and the step width 48 px; the border is `BorderColor` at half opacity. Openness does not name the elements of the default toolbar. Their number per type is taken over from the runtime: trends 10, trend companion 2, alarms 14, function trends 7, parameters 10, system diagnostics 4, media 6, web 2 + address field 180 px. The viewer does not draw icons. The template `include.style.extended.toolbar.default` creates the buttons via `hmi-bind:forEach="{ HmiProps.Elements }"`. For the default toolbars, these elements including graphics are not in the project file; the icons (also the network icon and the button of the process diagnostics) are runtime-internal.
- Process diagnostics overview: without toolbar and status bar, although both are set to visible. Two rows with the labels from `PDiagCategories.Label` and `PDiagSupervisionTypes.Label`. Six fields each with the fixed abbreviations of the runtime (E W I C4 C5 C6 or O I R A P M; not in Openness), font `SymbolFont`, on the right a line in `LineColor`. Dimensions measured: fields 43 × 40 from x = 80 at a spacing of 50.6, rows 14 and 62 px below the title bar. Network icon and button are missing.
- Tables with `RowHeaderType` Index have a 52 px wide header column with row numbers. Column names and widths are not in the Openness default values and are therefore missing.
- Alarm control and trend companion show their table in the initial state with the style values of the table view: header row `HeaderBackColor` between 2 px lines `HeaderGridLineColor`, rows `RowHeight` alternately white and `AlternateBackColor`. The viewer does not draw columns, buttons and trends.
- Scales (`hmi-element:arcDial`, `straightDial`, `TemplateRenderer.Dial.cs`):
  - `hmi-feature:scale` with begin/end-value and maximum-sections; `range` with bounds as a value or as `begin-surcharge`, `end-surcharge`, `scale-begin`, `scale-end`, plus `ribbons`.
  - `division` (count, nested) and `autoMeasure` (step 1/2/2.5/5 × 10ⁿ, at most maximum-sections) with `ticks` and `labels`; `position-filter` begin/end/inner/regular, `output-format` {I}/{F<n>}.
  - Position: an offset counts along the normal of the scale. For the arc it points away from the center, for a vertical scale (growing upward) to the right, for a horizontal one downward. A tick mark extends from offset to offset + length, a label sits centered on the offset, surcharge counts in pixels along the scale.
  - A bound value without a runtime value (ProcessValue) counts as 0.
  - `marker` (value, without runtime value 0) with `ticks` and `poleMarkers`. The content of a pole marker comes from `hmi:master-inline` (embedded template) or `hmi:master-include` (named template), with ParamProps from `hmi:param`, size content-width × content-height. Alignment: alignment −1 = content begins at the point (right or below), 0 = centered, 1 = ends at the point; `angle` rotates about the point. Nested `svg` with `overflow="visible"` are not clipped.
  - Without a tag, bar, slider and gauge show the value 0 (ProcessValue = 0), the I/O field stays empty.
  - `type` Logarithmic (both values > 0): positions and labels in log10 space. `extend` both (AutoScaling): begin and end move outward to a multiple of the round step width (1/2/2.5/5 × 10ⁿ), e.g. 7 to 33 → 0 to 40 in steps of ten. Neither is used in any sample and therefore not checked against the runtime.
  - Thresholds: every bar/gauge object has 5 default thresholds (Maximum, Upper, Normal, Lower, Minimum; deactivated). The file stores them as a list in part 0x0915 as soon as one is changed; the parser outputs `properties.Thresholds`. Threshold.Value is read-only in Openness and is not in the file without input. **Drawn since 2026-11-08 (sample 171, reference WinCC Unified runtime):** Without a stored value the threshold is 0 in the runtime, all markers lie at 0 (Minimum on top). The 90/80/20/10 % of the scale that the TIA screen editor shows are only an editor preview and are not stored. Normal has no marker. The marker shape has its tip at the origin of the content: on the arc (gauge) this origin lies on the value and the marker rotates with the scale; on the straight bar it rotates about the corner of the content frame. On the bar (straight scale) the pivot of the marker is the corner of the content frame, otherwise it sits 16 px too low; on the gauge the marker rotates with the scale (angle relative to the normal at 12 o'clock). `Converter.ThresholdColorForValue`: color of the range in which the value lies (> Maximum/Upper, < Minimum/Lower, Normal only strictly within the active bounds, otherwise fallback color); value 0 with Minimum at 10 shows the Minimum color as in the editor, value 0 with all thresholds at 0 the fallback color as in the runtime. Segmented modes (Segmented, SegmentedStatic) against the runtime (2026-11-08, Bar_T value 30, Gauge_T value 55, all thresholds at 0): for the range color only active thresholds count, in ascending order Minimum, Lower, Normal, Upper, Maximum; one whose value is not above the last one kept is dropped (Maximum+Upper at 0: value 30 shows Upper; all at 0: fallback color). `has(Flags, Flag)` checks the single flag (PeakIndicators None draws no peak value marker). 1.8 % deviating pixels (`171_segmented_runtime.png`, `171_segmented_viewer.png`); remainder: the overhang ribbons at the beginning and end of the scale are somewhat longer in the runtime. The value of a threshold cannot be entered statically in the TIA editor either (only via dynamization). Images `docs/img/z15/171_thresholds_editor.jpg` (editor), `171_thresholds_runtime.png` (runtime in the browser) and `171_thresholds_viewer.png` (viewer); viewer against runtime: 1.7 % deviating pixels in the 560×340 section (anti-aliasing, marker on the gauge about 4 px further inside). Not yet: the ribbons of the bar modes Segmented/SegmentedStatic and a comparison with the runtime.
  - Logarithmic scales: `ScalingType` is in StraightScale (0x2338) or CurvedScale (0x2337) under 0x232d (1 = Logarithmic). Runtime (164 Screen_R8): the automatic division labels linear steps (0, 200, …, 1000 for 1 to 1000) at their logarithmic positions; values below the beginning are placed at the beginning. The viewer does the same.
  - Clock: the hands in the viewer always show 12:00. The runtime shows the current time, and the file contains no time.

**Comparison F01 and F40 (R8, 2026-10-31):**
- F01: all 7 screens rendered and inspected against the truth (colors, shapes, texts), consistent. Open: whether the runtime shows fewer scale divisions for very small gauges, clocks and bars; in the viewer the labels are crowded there.
- Fixed crashes:
  - Controls that are lower than title bar, toolbar and status bar together (F01 FP_Controls): bars without space are dropped.
  - Texts with control characters (F40: NUL) in the template XML: control characters are removed.
  - An object that cannot be drawn becomes a placeholder with an error message instead of aborting the screen.
- The viewer does not display a ProcessValue that is bound to a tag or a script (without runtime, no value). The stored "value" of such objects is not a value but a wrapped expando object `{4902: "<4-character key>"}` (type 0x8000000B Text). The key refers to the dynamization link (like `$key$` in expressions). Openness reports ProcessValue there as not set, so the parser omits it. Three further cases in F40 are also link keys on dynamized fields (`3b0e330b` = 4-byte key without envelope, plus two fragments). Rule: if ProcessValue is dynamized, the parser omits it, regardless of how it is stored. The symbolic I/O field in F40 1040 (not dynamized) was not a framing error. The entry 4902 there covers the Padding sub-object (wrapped, TypeId 40013); there is no value. A ProcessValue with a wrapped object in it is now dropped. Padding is in the part `0x094d` (0x63 Left, 0x64 Top, 0x094b Right, 0x094c Bottom), evidenced via Openness with Padding 2/3/4/5 in the working copy 164 (Screen_R8/SymIO_P). The parser outputs it as `properties.Padding` (F40: 72 objects). In F40 no `ProcessValueRaw` remains.
- Faceplate container: the viewer draws the content of the type version from the project library. The basis is the parser output `faceplates`: the instance (screen, container name) points via `instanceOf.versionGuid` to `versions[].content` (size, objects). The content appears 1:1 from the top left and is clipped to the container size (runtime DOM, no scaling), in the style of the screen, with the background of the type version (backColor, usually transparent).
  - F40: all 14 containers; F01: Main/Faceplate container_1 (FP_Basic).
  - F01 FP_Controls/C_Faceplate1 has no type (ContainedType empty, no IsInstanceOf) and therefore remains a placeholder.
  - Interface values per instance: in the type content, a property that feeds an interface property is stored as dynamization `FaceplateInterface`. The name of the interface property is supplied by the field `PropertyName` (parser `interfaceProperty`). The viewer inserts the static value of the instance (`instances[].interface`, for multilingual texts the first language), e.g. in F40 1024 fp_Interface_do: “Multilingual Text“, “InterfaceText1“ to “InterfaceText3“, “Title Button“.
  - If the instance value is dynamized (F40 1036/2036: text list via index tag), there is no value without a runtime, so the type value remains. Values that are only set by a script (F40 fp_Interface_dont, Loaded event) do not appear, because the viewer does not execute scripts.
- Custom widget container (F40: 24 objects named DynamicSVG_n): each refers via ExpandoReferences to an `HmiDynamicScreenItemResourceData`, the widget package (15 in the project, e.g. `extended.NatureFlame` with file `NatureFlame.svghmi`, two as `{GUID}.zip`). `IHmiDynamicScreenItemResourceAttributes` contains Content, FilePath, HashCode, ContentType (3 = svghmi, 1 = zip) and BinaryType. Content is a paged blob; `decode_blob` delivers a 4096-byte page that begins with `00 01 00 00`, the actual package format is not yet interpreted. The parser outputs the package name as `properties.Widget`, the viewer shows it in the placeholder. Widget contents (Siemens) do not go into the repo.
- Horizontal scales (F40 1116): the labels overlapped because the parser did not read the orientation and the default "Vertical" applied. StraightScale is in the part expando 0x2338: 0x2273 BeginValue, 0x2274 EndValue, 0x3869 Orientation (0 = Horizontal). Now the slider is drawn horizontally with scale 20 to 100. Evidenced via Openness (working copy 164, screen Screen_R8, 2026-10-31): Slider_H with BeginValue 20, EndValue 100, Orientation Horizontal yields 0x2273 = 20, 0x2274 = 100, 0x3869 = 0; Slider_V with 7/33 (vertical) yields 0x2273 = 7, 0x2274 = 33 and no 0x3869.
- Small gauges, bars and clocks (F01 FP_Elements): whether the runtime shows fewer scale divisions is open and needs a runtime comparison (loading by User).
- Process diagnostics controls CriteriaAnalysis, GraphOverview and PlcCodeViewer: the object defaults come via Openness (itemGet full) from the working copy 164 and are in HmiDefaults.json. Size 600 × 280, 450 × 200 or 800 × 600; WindowFlags with maximize and close only for CriteriaAnalysis; CaptionColor 91939A, BackColor white, toolbar and status bar, table view CriteriaAnalysisView or GraphOverview. Comparison with runtime (164 Screen_R8): CriteriaAnalysis shows only the table (header, rows F2F2F5/white, lines GridLineWidth 1) without toolbar and status bar. GraphOverview has no toolbar. PlcCodeViewer has a separator line (10 px, 7C7D85) at OverviewDetailRatio, below it the symbol row (50 px, SymbolLineBackColor), then a toolbar with 6 buttons and the status bar. Column names and icons are runtime-internal. In F01 the controls are at y = 620, outside the 800 × 480 screen.
- The viewer draws bar, slider, gauge and clock from their templates. With scales: overall image 5.39 % → 4.25 %. Missing are the value knob of bar and slider (hmi:master) and the gauge markers. Deviation per object: clock 3.7 %, bar 15 %, slider 19 %, gauge 50 %. Missing are scales, value displays, the scale arc of the gauge and the digits of the clock (runtime elements that the interpreter does not know yet). The clock hands are at 12:00, the runtime shows the current time.
- Deviating pixels (screen 1920 × 1080): 26.41 % → 25.42 % → 25.10 %. Whole screen (height 2300): 27.33 % → 7.76 % (window frames) → 5.39 % (templates for bar, slider, gauge, clock). The remainder is scales and values, the process diagnostics overview, contents of the controls and font smoothing.
- New in SvgRenderer for this: single-line `text`, `rgba()`, nested `svg` (x/y/clip), `visibility="hidden"`.
- Troubleshooting: with the environment variable `OPENPEDATA_TEMPLATE_DUMP=<folder>` the viewer writes the expanded SVG per template.

**Visibility:** Like the TIA screen editor, the preview also draws objects with Visible=false (evidenced in the editor). The option --runtime-visibility or the switch 'Like runtime (respect Visible)' hides them as the runtime does; the runtime comparisons (--screen-png --compare) use this option.
