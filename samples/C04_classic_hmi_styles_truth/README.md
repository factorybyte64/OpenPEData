# C04_classic_hmi_styles – classic WinCC (Comfort / RT Advanced), the C01 and C02 screens in every WinCC style

The screens of C01 (`All_Objects` and the control screens `Ctl_*`) and of C02 (the property variants `P_*`) in the
runtime simulation with each of the other WinCC styles of TIA Portal V21. TIA Portal V21 Update 3, WinCC
Comfort/Advanced device version 17.0.0.0. Archive: `samples/C04_classic_hmi_styles.zap21`. It is the C06 project, and
only the default style of the project and the start screens differ.

## Styles

The project has four styles, all of them library types of the TIA Portal installation (Common data > Styles, read-only):
WinCC Dark, WinCC Fresh, WinCC Light and WinCC Wireframe. Every device uses "Default style of the project" (runtime
settings), so the default style decides the look of all devices.

* WinCC Dark is the default style of C01 … C06. Its runtime captures are in the truth folders of C01 (`All_Objects`,
  `Ctl_*`) and C02 (`P_*`).
* This sample adds WinCC Fresh, WinCC Light and WinCC Wireframe. In the archive the default style is WinCC Wireframe.

## Runtime captures

`runtime/<style>/<device>_<screen>_runtime.png`: runtime simulation (WinCC Runtime Advanced Simulator V21), screen area
at native size, scrolled and stitched as in C01 … C03. The `Ctl_*` screens contain the navigation template `Nav` of
C01.

| Style | HMI_TP900 (800×480) | HMI_TP1200 (1280×800) | HMI_PCADV (1024×768) |
|---|---|---|---|
| WinCC Fresh | `All_Objects`, 11 `Ctl_*`, 32 `P_*` | `All_Objects`, 11 `Ctl_*`, 21 `P_*` | `All_Objects`, 15 `Ctl_*`, 18 `P_*` (no `P_Ellipse_1`, `P_Polygon_1`, `P_Gauge_3`, `P_Clock_1`, `P_Clock_2`) |
| WinCC Light | `All_Objects`, 11 `Ctl_*`, 32 `P_*` | `All_Objects`, 11 `Ctl_*`, 21 `P_*` | – |
| WinCC Wireframe | `All_Objects`, 11 `Ctl_*`, 32 `P_*` | `All_Objects`, 11 `Ctl_*`, 21 `P_*` | – |

The screens are the same as in C01 and C02, so the instances and Openness exports of those samples apply. The
`Ctl_*` screens show the controls without data (as in C01). The `P_*` screens show the property variants of each
object type (as in C02).

HMI_PCADV is covered for WinCC Fresh only. After the start of the RT Advanced simulator, the runtime often ignored
clicks on the screen-change buttons for minutes, and screen changes arrived late. The captures of WinCC Light and
WinCC Wireframe were not reliable and are not included. Of the WinCC Fresh `P_*` captures, those that showed a
neighbouring screen of the chain were removed.

## How it was built

1. Starting point: the C06 project.
2. TIA Portal UI: in Common data > Styles, the default style was set to WinCC Fresh, then WinCC Light, then WinCC
   Wireframe. For each style the HMI devices were compiled.
3. For each style and device, the simulation was started from the TIA Portal with the start screen `Ctl_01_AlarmView`.
   The screens were captured through the buttons of the template `Nav` (button n activates `Ctl_n`, the last one
   `All_Objects`).
4. Then the simulation was started again with the start screen `P_Line_1` (HMI_PCADV: `P_Polyline_1`), and the `P_*`
   screens were captured through their `Next` buttons.

The archive was made with the default style WinCC Wireframe. Its start screens are `P_Line_1` on HMI_TP900 and
HMI_TP1200, and `Ctl_01_AlarmView` on HMI_PCADV.
