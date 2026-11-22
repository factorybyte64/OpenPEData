# C05_classic_hmi_graphics – classic WinCC (Comfort / RT Advanced), graphics, lists, template, pop-up and slide-in

Project graphics in every format, graphic views with size and stretch variants, text and graphic lists on symbolic and
graphic I/O fields, button and switch modes with graphics and lists, a screen template, nested groups, screen layers, a
pop-up screen and a slide-in screen. TIA Portal V21 Update 3, WinCC Comfort/Advanced device version 17.0.0.0. Archive:
`samples/C05_classic_hmi_graphics.zap21`; it is the C03 project plus the content described here (the start screens of
the three devices are `C5_GraphicView_1`).

Faceplates are not part of this sample (a faceplate type can only be created in the TIA Portal UI; follows separately).

## Devices

| Device | Screen size | `C5_*` screens |
|---|---|---|
| HMI_TP900 (TP900 Comfort) | 800×480 | 7 (`C5_GraphicView_2` split into `_a`, `_b`, `_c`) |
| HMI_TP1200 (TP1200 Comfort) | 1280×800 | 5 |
| HMI_PCADV (PC station, WinCC RT Advanced, `HMI_RT_1`, window mode as in C03) | 1024×768 | 5 |

## Content

* **Project graphics** (`graphics/`, Openness export of every graphic with its image file): `G_PNG` (PNG with alpha),
  `G_JPG`, `G_BMP`, `G_GIF` (transparent background), `G_TIF`, `G_EMF`, `G_WMF`, `G_SVG` – all 120×80 with a frame,
  a diagonal, a circle and the format name – and `G_TALL` (40×100) and `G_WIDE` (160×40) for the stretch variants.
  Created by `_scripts/mkimg.ps1` and `_scripts/mkwmf.py`, imported with TIA Portal Openness.
* **Graphic lists** (`graphiclists/`, the SimaticML files that were imported): `GL_Value` (value/range: −100..−1
  `G_GIF`, 0 `G_PNG`, 1..9 `G_JPG`, 10 `G_BMP`, default `G_SVG`), `GL_Bit` (bit: 0 `G_EMF`, 1 `G_WMF`), `GL_BitNo`
  (bit number: 0 `G_PNG`, 1 `G_JPG`, 2 `G_BMP`, 3 `G_TIF`). The text lists `TL_*` are those of C03.
* **Screen template** `T_C05`: header bar (rectangle 0,70,140), title text field, date/time field. Every `C5_*` screen
  uses it.
* **Screens** (every screen has the bottom bar of C03: `State`, `S1` … `S4` set the C03 states, `Next`):
  * `C5_GraphicView_1`: one graphic view per graphic, 120×80, FitToLargest false.
  * `C5_GraphicView_2`: `G_PNG`, `G_TALL`, `G_WIDE` in objects of 160×60, 60×120 and 80×80, each with FitToLargest
    false/true (`<graphic>_<w>x<h>_FitF/FitT`), `G_BMP` with transparent color 255,255,240, `G_PNG` with back fill
    transparent.
  * `C5_Lists`: symbolic I/O fields with `TL_Value` (`D_TL`), `TL_Bit` (`D_Bool`), `TL_BitNo` (`D_Bits`) and one in
    output mode; graphic I/O fields with `GL_Value`, `GL_Bit`, `GL_BitNo` and one in output mode.
  * `C5_Buttons`: buttons in mode Text, Graphic (off `G_PNG`, on `G_JPG`), GraphicAndText, TextList (`TL_Value`,
    `D_TL`), GraphicList (`GL_Value`, `D_TL`), Invisible; switches in mode Switch, TextSwitchButton and
    GraphicSwitchButton (off `G_BMP`, on `G_GIF`), all switches on `D_Bool`.
  * `C5_Structure`: nested groups (`Group_A` with rectangle, circle and `Group_B` with ellipse and text), four layers
    with overlapping rectangles (layer 2 hidden in the engineering, VisibleES false), buttons `Popup` (ShowPopupScreen
    `Popup_C05` at 200,120, display mode toggle) and `Slide` (VB script `C5_ToggleSlideIn`: ShowSlideInScreen
    "Slide-in screen top", toggle).
* **Pop-up screen** `Popup_C05` (300×200): background rectangle, title, graphic view `G_PNG`, I/O field `D_Int`,
  button `Close`.
* **Slide-in screen** `Top` (Activate true, Dimension 120): title, bar `D_Pct`, I/O field `D_Int`. WinCC RT Advanced
  has no slide-in screens; on HMI_PCADV the VB script is a comment (a ShowSlideInScreen call there breaks the
  compilation of all VB scripts of the device).
* `instances/<device>.json`: screen, object, type, configuration and position of every instance.

## Truth files

* `xml/<device>/screen`, `screenTemplate`, `screenPopup`, `screenSlidein`, `vbScript`: TIA Portal Openness export
  (ExportOptions WithDefaults). Graphic lists are not exported (the Openness export of a classic graphic list ended
  the TIA Portal V21 process); `graphiclists/` holds the imported definition.
* `runtime/<device>_<screen>_S<n>_runtime.png`: runtime simulation (WinCC Runtime Advanced Simulator V21) after `S<n>`
  (states of C03), screen area at native size, scrolled and stitched as in C03.
* `runtime/<device>_C5_Structure_Popup_runtime.png`: the pop-up screen is a separate window of the simulator; it was
  captured separately and placed at its configured position (200,120) on the screen capture.
* `runtime/<device>_C5_Structure_SlideIn_runtime.png`: slide-in screen Top opened (TP900, TP1200; the slide-in screen
  of HMI_PCADV is imported as well but has no effect there).

## How it was built

1. Graphics generated and imported with TIA Portal Openness.
2. `python _scripts/gen_c05.py <dir> <width> <height> [rt]` per device; import of graphic lists, template, VB script,
   pop-up screen, slide-in screen and screens with TIA Portal Openness (SimaticML import), a second screen import pass
   links the `Next` buttons.
3. Start screen `C5_GraphicView_1` per device in the TIA Portal UI, simulation started from the TIA Portal, every
   screen captured after `S1` … `S4`, then pop-up and slide-in on `C5_Structure`.
