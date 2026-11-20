# Plan Z1.5: Display HMI screens in the viewer true to the original

Goals and non-goals: see [GOALS.md](GOALS.md) (the only valid source), here Z1.5: "HMI screens are displayed true to the original as in TIA Portal." As of 2026-10-31 (progress in the section "Status" at the end).

## Starting point

- The parser returns per screen the size, background color and per screen item the type, name, position and the properties **stored** in the project (`properties`), plus dynamizations and events (samples 110–135, F01, F40; test_hmi).
- The viewer (`ui\OpenPEData.Viewer`, .NET 10 WPF) shows only labeled frames in the "Preview" tab (`DetailViews.ScreenPreview`).
- Only what deviates from the default is stored. For a display true to the original, the **default values per object type and style** are therefore missing: font, border width, style colors, corners, text alignment. Openness, by contrast, returns all values, including the default values (`TiaHmiUnified itemGet screen`).
- Frequency of the object types in the samples (24 samples with screen items, mainly F40 with 1263 objects):

  | Type | Count |
  |---|---|
  | Text | 263 |
  | Text box | 238 |
  | Rectangle | 226 |
  | Graphic view | 183 |
  | Line | 169 |
  | I/O field | 94 |
  | Screen window | 48 |
  | Circle | 24 |
  | Custom Widget | 24 |
  | Button | 23 |
  | Faceplate container | 23 |
  | Toggle switch | 22 |
  | Slider | 11 |
  | Bar | 10 |
  | Group | 10 |

  Less frequent (5–9): alarm controls, symbolic I/O field, polyline/polygon, ellipse, arcs and segments, lists, clock, gauge, controls.

## Reference

1. **Runtime (main reference):**
   - A copy of a sample gets a Unified PC station (WinCC Unified PC RT) via MCP. The screens are taken over from the sample via screen XML import (`TiaHmiUnified exportXml`/`importXml`).
   - The copy is compiled and loaded into the locally installed runtime (`TiaHmiUnified compile`/`downloadInfo`/`download software`).
   - The screen is opened in the browser via the web client and captured by screenshot (built-in browser, same resolution as the screen).
   - Only on copies; shut down our own TIA instance and runtime cleanly afterwards.
   - Possible hurdle: when compiling, Unified requires a runtime/transfer password that Openness cannot set (TiaMcpXE docs). Then it is a one-time manual step by the user on the copy (test password). I report the exact step and continue with the second reference in the meantime.
2. **TIA screen editor (second reference):** screenshot of the opened screen in TIA, if the runtime is not sufficient for a type or editor and runtime differ (e.g. placeholders of controls). The goal per Z1.5 is "as in TIA Portal". Where editor and runtime differ, the editor applies; the difference is noted.
3. **Value truth:** `itemGet` per screen item with all properties (`<sample>_truth\HMI_1\screenItems\<Screen>.json`). From this, a **default value table** per type and style (`StyleItemClass`) is created in the viewer. Stored values from the parser override it. The table is tested against itemGet (same approach as test_hmi).

**User decision 2026-10-29:** default values from Openness are used only in the viewer for drawing. The parser output remains unchanged; it contains only what is in the file. The default value table is a separate file (`ui\OpenPEData.Viewer\HmiDefaults.json`) that only the viewer reads.

## Comparison method

- Viewer: `--select <Screen> --tab Preview --snapshot` renders the screen at a 1:1 scale (without Viewbox scaling, new switch `--scale 1`).
- Crop the runtime screenshot to the screen area.
- Pillow and numpy are not installed and will not be installed. The comparison therefore runs in .NET in the viewer: a new switch `--compare <ref.png>` writes an image "reference | viewer | difference" and outputs the share of differing pixels (tolerance for anti-aliasing).
- Results under `docs\img\z15\<sample>_<screen>_{rt,viewer,cmp}.png`; the percentage per screen is in a table in `docs\spec\hmi-rendering.md`.
- Acceptance per type:
  - position and size pixel-exact;
  - colors exact;
  - text with the same font, size and alignment;
  - remaining difference only due to anti-aliasing.

## Steps (order by frequency)

| No | What | Types / properties |
|---|---|---|
| R0 | **Tools:** move the renderer into its own file `ScreenRenderer.cs` (WPF, same structure), switches `--scale`/`--compare`, try out the runtime reference path on one sample (PC station, compile, download, web client screenshot) | – |
| R1 | **Basic shapes** | Rectangle, text, text box, line: fill and border color, border width and style, corner radius, font (family, size, bold/italic), text alignment, margins, line ends |
| R2 | **Input/output** | I/O field (format, alignment, unit), symbolic I/O field, button (text, graphic, style), toggle switch |
| R3 | **Graphics** | Graphic view: decode image data from `HmiImageData`/`HmiInternalImageData` (SVG/PNG), scaling (stretch), graphic lists |
| R4 | **Geometry** | Circle, ellipse, arcs, segments, polyline, polygon: angles, radii, points, fill |
| R5 | **Cross-cutting** | Layer order (layer, then order within the layer), rotation (`RotationAngle`, pivot point), visibility (`Visibility`, layer hidden), transparency, styles/designs (`StyleItemClass`) |
| R6 | **Containers** | Screen window (embedded screen recursively), faceplate container (faceplate screen from the library), group, custom widget (placeholder with frame) |
| R7 | **Operating elements and controls** | Slider, bar, gauge, clock, lists, alarm controls, trends, diagnostics controls: first as placeholders with frame, background and title bar as in the editor; level of detail by frequency |
| R8 | **Comparison:** all screens from F01 and F40 against the reference; results table in `docs\spec\hmi-rendering.md` | – |

Each step comprises:

- if needed, new samples via MCP on copies (number range **164–199**, base 110_hmi_screen; one screen per type with variation of the core properties);
- itemGet truth, default value table, renderer, comparison images, specification, commit.

## Status 2026-10-31

**Reference path (implemented, differently than planned above):**
- Openness does not create the Unified PC station (MCP-098). The user created it by hand (template 164).
- Loading via Openness does not work for PC station V1.0 (MCP-099, TIA limit); the user loads by hand.
- Login to WebRH with a test user (UMAC project user). The screenshot is taken by headless Edge via a CDP script at `https://<computer name>/WebRH`; the UMC login is under the computer name.
- Reference screen: working copy 164, Screen_1 with the 37 object types from 165 (default values), 1920 × 2300.

| No | Status | Result / reason |
|---|---|---|
| R0 | done | ScreenRenderer, `--screen-png`, `--compare`; runtime reference path as above |
| R1 | done | Border centered on the outline and anti-aliased (as in the runtime), text/text box, line |
| R2 | done | Template interpreter for button, I/O field, text box, toggle switch; I/O field without tag empty; drop-down button of the symbolic I/O field after measurement |
| R3 | done | Graphic view with project and library graphics |
| R4 | done | Circle, ellipse, arcs, segments, polyline, polygon |
| R5 | done | Layers, rotation, visibility, styles (Extended/Flat), gradients ±1 step |
| R6 | largely | Group, screen window frame; faceplate container with content of the type version and static interface values of the instance. Open: content of custom widgets (package format not interpreted, widget content outside what Openness reads) and embedded screen in the screen window |
| R7 | largely | Lists and groups, window frames of the controls with tables, toolbar buttons and row headers, process diagnostics grid, scales (arcDial/straightDial) with marker and value knob. Open: icons and column names (runtime-internal, not in the file), trend contents. Thresholds and segmented bar modes since 2026-11-08 (sample 171, runtime comparison) |
| R8 | done | F01 (7 screens) and F40 (126 screens) render without crashing; 164 against runtime 27.33 % → 2.91 % differing pixels (remainder: icons, font smoothing, time of day) |

**Parser extensions along the way** (only values stored in the file):
- style metadata `style.info`;
- `properties.StraightScale` and `properties.Padding` from partial expandos (confirmed via Openness);
- interface property of bound faceplate objects (`interfaceProperty`);
- widget package name;
- dynamized ProcessValue is dropped (Openness: not set).

**Open:**
- Runtime comparison of small gauges, bars and clocks as well as the horizontal scale: done (164 Screen_R8, 1.79 % differing pixels, see COVERAGE.md).
- Screen window content: done (commit 77d6794d, the screen window shows its static screen).
- Custom widget content.
- Thresholds: drawn since 2026-11-08 and compared against the WinCC Unified runtime (sample 171, marks and range color, 1.7 % differing pixels); segmented bar modes also compared (1.8 %).

## Framework

- Constraint GOALS.md section 4: what is displayed is what Openness can read or write on screen items. Graphics of the image library count as property values (`Graphic`, `BackGraphic`); they are drawn as far as the image data can be decoded.
- Existing viewer structure (.NET/WPF), no new framework, no installation without approval.
- TIA only on copies, shut down our own instances cleanly.
- Issues with TiaMcpXE go into the backlog `<shared folder>`.
