# C01_classic_hmi – classic WinCC (Comfort / RT Advanced), all objects and controls with default values

Base sample for the screen preview of classic WinCC devices (HmiTarget). TIA Portal V21 Update 3, WinCC
Comfort/Advanced device version 17.0.0.0. Archive: `samples/C01_classic_hmi.zap21` (project name inside the archive:
`000_empty`, as for the other samples derived from `002_cpu`).

Follow-up samples (planned): C02 static property variants per object type, C03 dynamics (tags, animations, scripts,
VBS value simulation), C04 every style/design.

## Devices

| Device | Type | Screen | PLC connection | Notes |
|---|---|---|---|---|
| PLC_1 | CPU 1513-1 PN (from 002_cpu) | – | – | changed for the HMI link, see below |
| HMI_TP900 | TP900 Comfort 6AV2 124-0JC01-0AX0, V17.0.0.0 | 800×480 | HMI_Connection_1 (integrated) | without screens 08, 11–14 (see below) |
| HMI_TP900S | TP900 Comfort, V17.0.0.0 | 800×480 | none | all screens except 08; internal tags only |
| HMI_TP1200 | TP1200 Comfort 6AV2 124-0MC01-0AX0, V17.0.0.0 | 1280×800 | HMI_Connection_4 (integrated) | without screens 08, 11–14 |
| HMI_PCADV | SIMATIC PC station + WinCC RT Advanced 6AV2 104-0xxxx-xxxx V17.0.0.0 (HMI_RT_1), IE general | 1024×768 | HMI_Connection_3 (integrated) | all screens except 08; full-screen mode (default) |

All HMI devices keep the default runtime settings (style "WinCC Dark" = default style of the project, adapt font size
on). Start screen: `Ctl_01_AlarmView` (needed for the navigation, see below).

WinCC RT Professional is not part of this sample.

## Screens

* `All_Objects` (number 2): one instance of every object of the toolbox groups *Basic objects* and *Elements*, default
  values, created by Openness XML import (`_scripts/gen_c01.py`): Line, Polyline, Polygon, Ellipse, Circle, Rectangle,
  TextField, GraphicView, IOField, Button, SymbolicIOField, GraphicIOField, DateTimeField, Bar, Switch, SymbolLibrary,
  Slider, Gauge, Clock. Object name `<Type>_1`, only Left/Top/Width/Height set. Layout per screen size by the script.
* `Ctl_01` … `Ctl_16` (numbers 10–25): one control of the toolbox group *Controls* per screen, placed in the screen
  editor by clicking the toolbox entry and then the screen at (2,2) – default size and default values:

| No. | Screen | Control | TP900 | TP900S | TP1200 | PCADV |
|---|---|---|---|---|---|---|
| 01 | Ctl_01_AlarmView | Alarm view | ✓ | ✓ | ✓ | ✓ |
| 02 | Ctl_02_TrendView | f(t) trend view | ✓ | ✓ | ✓ | ✓ |
| 03 | Ctl_03_UserView | User view | ✓ | ✓ | ✓ | ✓ |
| 04 | Ctl_04_HtmlBrowser | HTML browser | ✓ | ✓ | ✓ | ✓ |
| 05 | Ctl_05_WatchTable | Watch table (status/force) | ✓ | ✓ | ✓ | ✓ |
| 06 | Ctl_06_SmartClientView | Sm@rtClient view | ✓ | ✓ | ✓ | ✓ |
| 07 | Ctl_07_RecipeView | Recipe view | ✓ | ✓ | ✓ | ✓ |
| 08 | Ctl_08_FxTrendView | f(x) trend view | – | – | – | – |
| 09 | Ctl_09_SysDiagView | System diagnostics view | ✓ | ✓ | ✓ | ✓ |
| 10 | Ctl_10_MediaPlayer | Media player | ✓ | ✓ | ✓ | ✓ |
| 11 | Ctl_11_PlcCodeView | PLC code view | – | ✓ | – | ✓ |
| 12 | Ctl_12_GraphOverview | GRAPH overview | – | ✓ | – | ✓ |
| 13 | Ctl_13_ProDiagOverview | ProDiag overview | – | ✓ | – | ✓ |
| 14 | Ctl_14_CriteriaAnalysisView | Criteria analysis view | – | ✓ | – | ✓ |
| 15 | Ctl_15_CameraView | Camera view | ✓ | ✓ | ✓ | ✓ |
| 16 | Ctl_16_PdfView | PDF view | ✓ | ✓ | ✓ | ✓ |

Missing controls and why:

* **08 f(x) trend view**: with default values it does not compile ("The property 'Logging tag X' of 'f(x) trend view_1'
  is invalid"); it needs a data log, which Openness cannot create. It follows in C03 with a data log.
* **11–14 PLC code view, GRAPH overview, ProDiag overview, criteria analysis view** on the Comfort panels **with** a PLC
  connection: the runtime simulation (HmiRTm V17.0 Upd9) ends while loading `pdata.fwc` ("configuration file could not
  be loaded", access violation in FwCombinedBase.dll). Without a PLC connection (HMI_TP900S) and on WinCC RT Advanced
  (HMI_PCADV) they run. Compiling also reports "Required license: ProDiag for SIMATIC Comfort/Mobile Panels".

Screen template `Nav` (all `Ctl_*` screens use it): a column of small buttons (28×22, default text "Text") at the right
edge, button n (from the top, 26 px pitch) calls the system function ActivateScreen with `Ctl_n`, button 17 activates
`All_Objects`; buttons of screens a device does not have are left out, the others keep their position. It exists only
to navigate in the simulation and is part of every `Ctl_*` screenshot.

## Tags (Default tag table, every HMI device)

Internal: `Hmi_Bool` (Bool), `Hmi_Int` (Int), `Hmi_Real` (Real), `Hmi_String` (WString). External (not on HMI_TP900S):
`Ext_Bool`→`Plc_Bool` %M100.0, `Ext_Int`→`Plc_Int` %MW102, `Ext_Real`→`Plc_Real` %MD104, `Ext_Byte`→`Plc_Byte` %MB108,
acquisition cycle 1 s. No object of C01 is connected to a tag.

## Changes to PLC_1 (needed for the Comfort runtime)

With the settings of 002_cpu the Comfort simulation with an integrated connection did not start ("Invalid certificate
configuration, please recompile your PLC", then the runtime crashed). Changed: CommunicationMode 1 → 0 (legacy PG/PC and
HMI communication allowed), access level FullAccess, protection of confidential PLC configuration data off. PLC tags
`Plc_Bool`, `Plc_Int`, `Plc_Real`, `Plc_Byte` added.

## Truth files

* `xml/<device>/screen`, `screenTemplate`, `tagTable`, `screenOverview`, `screenGlobalElements`, `cycle`: Openness
  export (ExportOptions WithDefaults) of the final project. **Openness V21 drops the controls of the `Ctl_*` screens
  from the export without an error** (only the User view is exported); the screen files of those screens are therefore
  empty apart from the template link – the control types and their positions are given by the table above.
* `runtime/<device>_<screen>_runtime.png`: screenshot of the runtime simulation (WinCC Runtime Advanced Simulator V21,
  HmiRTm V17.0 Upd9) at native size, screen area only (800×480, 1280×800, 1024×768), taken with PrintWindow from the
  simulator window. Desktop size during capture: 1470×824. TP1200: the simulator window is taller than the desktop, so
  the screen was captured scrolled to the top and to the bottom and stitched (rows 0–599 and 600–799). PC RT Advanced
  1024×768: the full-screen runtime window covers the screen area completely.
* Time-dependent content in the screenshots: date/time field, clock, f(t) trend view time axis (captured 2026-11-22
  around 00:00–00:20 local time). HTML browser shows "403 ERROR" (default URL http://www.siemens.com, fetched from the
  build machine). Sm@rtClient view shows "Unable to resolve host MyPanel" (default). Camera and PDF views show their
  empty default state, media player its placeholder (TP) or nothing (PC).
* Missing: screenshots of the screen editor (not taken in this pass), runtime screenshots of TP900/TP1200 for 11–14
  (runtime crash, see above).

## How it was built

1. `002_cpu.zap21` retrieved; PLC tags added; HMI devices and integrated connections created with TIA Portal Openness
   (the PC station with WinCC RT Advanced via "Add new device" in the TIA Portal UI).
2. `_scripts/gen_c01.py <dir>` writes the tag tables, `All_Objects` per screen size, the `Nav` templates and the empty
   `Ctl_*` screens; imported with TIA Portal Openness (SimaticML import). HMI_TP900S got `_scripts/tags_internal.xml`
   (internal tags only).
3. Controls placed in the TP900 screen editor, screens copied in the project tree to the other devices.
4. Start screen and simulation start in the TIA Portal UI.
