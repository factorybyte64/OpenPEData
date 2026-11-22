# C02_classic_hmi_props – classic WinCC (Comfort / RT Advanced), one changed appearance property per instance

Static property variants of every screen object type of C01 (basic objects and elements). TIA Portal V21 Update 3,
WinCC Comfort/Advanced device version 17.0.0.0. Archive: `samples/C02_classic_hmi_props.zap21`; it is the C01 project
(all C01 screens unchanged) plus the `P_*` screens described here.

## Devices

| Device | Screen size | `P_*` screens |
|---|---|---|
| HMI_TP900 (TP900 Comfort) | 800×480 | 32 |
| HMI_TP1200 (TP1200 Comfort) | 1280×800 | 21 |
| HMI_PCADV (PC station, WinCC RT Advanced, `HMI_RT_1`) | 1024×768 | 23 |

HMI_TP900S of C01 has no `P_*` screens.

## Screens

`P_<Type>_<n>`: for each type one default instance (object name `Default`) and one instance per appearance attribute
in which exactly this attribute differs from its default (object name = attribute name; further values of an
enumeration as `<Attribute>_2`, `_3`, …). A text field `Label_<object>` below every instance shows the object name (it
is cut off for narrow objects; `variants/<device>/variants.json` gives the mapping). The instances keep the default size
of the type, a grid fills the screen, further instances continue on `P_<Type>_2`, … . A button `Next` (40×22, default
text "Text") in the bottom right corner activates the next screen; the last screen leads back to the first.

538 instances per device (Line 22, Polyline 22, Polygon 11, Ellipse 11, Circle 11, Rectangle 13, TextField 23,
GraphicView 13, IOField 33, Button 42, SymbolicIOField 34, GraphicIOField 21, DateTimeField 28, Bar 50, Switch 50,
SymbolLibrary 18, Slider 53, Gauge 53, Clock 30).

Rules of the generator (`_scripts/gen_c02.py`, input: the C01 export of `All_Objects`, which supplies attribute list and
defaults):

* Not varied: position and size, object name, tab order, points of polylines/polygons, OCX state of the symbol library,
  operator settings without appearance (hot key, bit number, on value, enabled, two-hand operation, hidden input,
  continuous change, compatibility mode) and the radii of circle/ellipse (they follow width and height; a changed radius
  ended TIA during the import).
* Colors: one of 255,0,0 / 0,0,255 / 0,160,0 / 255,128,0 / 160,0,160 / 0,160,160 / 255,255,0 (the first that differs
  from the default). Booleans: inverted. Numbers: fixed values per attribute (list in the script), margins 8, other
  integers +5, fractions ×0.6. Enumerations: every candidate value of the list in the script that differs from the
  default.
* Line: the start and end points of a line are 0 by default (zero-length line, invisible in the runtime – see C01). Every
  line instance of C02 gets start (Left,Top) and end (Left+Width,Top+Height) points so that the variants are visible.
* `UseDesignColorSchema` is true by default for most types; with it the runtime uses the colors of the style for many
  color attributes, so several color variants look like `Default`. The instance `UseDesignColorSchema` shows the type
  without the design colors.

Values TIA refused are not in the screens: `variants/<device>/skip.json` lists them (108, same on every device):
enumeration candidates that do not exist for the type, `IOField.DataFormat` Hexadecimal and Binary (compile error "The
format pattern of I/O field … is invalid" with the default format pattern) and `Ellipse.RadiusHeight`.

## Truth files

* `xml/<device>/P_*.xml`: TIA Portal Openness export (ExportOptions WithDefaults) of every `P_*` screen.
* `runtime/<device>_P_*_runtime.png`: screenshot of the runtime simulation (WinCC Runtime Advanced Simulator V21) at
  native size, screen area only (800×480, 1280×800, 1024×768). TP1200 is stitched from two parts as in C01; PC RT
  Advanced: bottom rows 764–767 are outside the desktop and appear as background.
* `variants/<device>/variants.json`: screen, object, type, changed attribute, value (attribute `null` = Default);
  `screens.json`: screen order of the Next chain; `skip.json`: refused variants.
* Time-dependent and blinking content: clocks and date/time fields show the capture time (2026-11-22 00:35–01:30 local
  time); instances with `Flashing` or `FlashingOnLimitViolation` are captured in whatever flash phase the runtime had.

## How it was built

1. The C01 project was extended: `python _scripts/gen_c02.py All_Objects.xml <dir> <width> <height> [skip.json]` per
   device, import of the screens with TIA Portal Openness (SimaticML import); refused values went to `skip.json` and the
   screens were generated and imported again; a second import pass links the Next buttons.
2. Start screen `P_Bar_1` per device in the TIA Portal UI, simulation started from the TIA Portal, screens captured one
   after the other via the Next button.
