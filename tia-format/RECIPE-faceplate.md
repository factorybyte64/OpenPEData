# Manual recipe: build Unified faceplates from the test screens (F2)

Status 2026-10-20 16:50, thread "Unified Faceplates", plan [PLAN-faceplate.md](PLAN-faceplate.md). For User, TIA V21 with WinCC Unified. Openness V21 can neither create nor change faceplate types, so they are created by hand. Afterwards I read them from the `PEData.plf`.

**Only one thing matters:** after every step, do **Project → Archive** once with the given name to the `samples\` folder of this repository. That way I can compare every step individually. I am writing the menu names from memory. If something is called differently or does not work, do it the way TIA offers it, and write me briefly what was different.

## Starting project
`samples\F01_hmi_allobjects` (project file `000_empty.ap21`), panel HMI_1 (MTP700 Unified Comfort), HMI tag `HmiTag_Ext1`. Screens:

| Screen | Objects |
|---|---|
| FP_Basic | B_Rect1, B_Line1, B_Polyline1, B_Polygon1, B_Ellipse1, B_EllipseSeg1, B_EllArc1, B_Circle1, B_CircleSeg1, B_CircArc1, B_Text1, B_GraphicView1 (without dynamization) |
| FP_Elements | E_Button1, E_IOField1, E_SymIOField1, E_TextBox1, E_Bar1, E_Slider1, E_Gauge1, E_Clock1, E_Switch1, E_CheckBox1, E_Radio1, E_ListBox1, E_TouchArea1, E_AlarmInd1 |
| FP_Controls | C_AlarmControl1, C_AlarmLine1, C_Trend1, C_TrendCompanion1, C_FTrend1, C_DetParam1, C_Web1, C_Media1, C_SysDiag1, C_ProDiagOv1, C_GraphOv1, C_CritAnalysis1, C_PlcCode1, C_ScreenWin1, C_Faceplate1 (empty faceplate container) |
| FP_Interface | I_IOField1 (process value still without tag, text only), I_Bar1 (process value via tag HmiTag_Ext1), I_Rect1 (color via bit and range), I_Text1 (color via script) |
| FP_Script | S_Button1 (scripts on Tapped and Down), S_IOField1 (script on value change, process value without tag), screen event Loaded |
| FP_Flash | X_Rect1 (visibility via tag, flashing) |
| Main | empty, for the instances |

## Preparation
1. In Explorer, copy `samples\F01_hmi_allobjects` as `samples\F02_hmi_faceplates`. Please do not open the original.
2. Start TIA or take a TIA window without an open project. At most four instances; mine is PID 40268, please do not take that one. Then open `F02_hmi_faceplates\000_empty.ap21`.

## Steps (archive after every step)
| No | What you do | Archive name |
|---|---|---|
| a | Change nothing, just **Save**. | `F02a_open` |
| b | **Project library → Types → right-click "Add new type" → Faceplate (WinCC Unified)**, name `FP_Empty`. Put nothing in it, close the editor. | `F02b_fp_empty` |
| c | New faceplate type `FP_Basic`. In the screen FP_Basic select all objects (Ctrl+A), copy them and paste them into the faceplate editor. | `F02c_fp_basic` |
| d | New faceplate type `FP_Interface`, copy the objects from the screen FP_Interface into it. In the **interface** create: properties `P_Bool` (Bool), `P_Int` (Int), `P_String` (String/WString), plus a tag property `P_Tag` (Int) if TIA offers this, and an event `E_Click`. Connect the process value of I_IOField1 (so far without tag) and of I_Bar1 to `P_Tag` (or `P_Int`) and the text color of I_Text1 to `P_Bool`. If TIA removes or converts the tag connections on pasting, leave it that way and tell me what happened. | `F02d_fp_interface` |
| e | New faceplate types `FP_Elements`, `FP_Controls` and `FP_Script`, each with the objects from the screen of the same name. Leave out and note what TIA does not allow in the faceplate (e.g. C_Faceplate1, C_ScreenWin1). For FP_Script check whether the scripts came along. | `F02e_fp_rest` |
| f | **Release** `FP_Basic` and `FP_Interface` (right-click → Release version), version **1.0.0**. The others stay in work. | `F02f_release` |
| g | **Edit** `FP_Basic` (new version), change the background color of B_Rect1, release as **1.0.1**. | `F02g_v101` |
| h | In the screen **Main** place one instance each of `FP_Basic` and `FP_Interface`. For FP_Interface: `P_Bool` = true (constant), `P_Int` = 42, `P_String` = `Hallo`, `P_Tag` to `HmiTag_Ext1`. | `F02h_instances` |

At the end save, close the project and write me "Faceplates done" in the thread, plus everything that was called differently or did not work. If you are short of time, a to d and f to h are enough. Step e is the bonus.

## Phase C, status 2026-10-22 18:30 (everything in `samples\F01_hmi_allobjects`, save + archive to `samples\` after each step)
| Archive | What |
|---|---|
| `F16_iface_props` | FP_Interface, tab "Property Interface": all 11 types: P_Bool Bool, P_Int LInt (default 42), P_Real LReal, P_ULInt ULInt, P_Color Color, P_Graphic Graphic, P_Text Multilingual text, P_String WString, P_Auth Authorization, P_List Resource list, P_ParamSet Parameter set type |
| `F18_iface_event` | FP_Interface (edit type → 0.0.4), tab "Event Interface": event `EvTest` with parameters `Nr` (Int), `Flag` (Bool); button `I_Button1`, Tapped script `Faceplate.RaiseEvent("EvTest", { Nr: 5, Flag: true });`; compile, release (default values do not exist in the Property Interface) |
| `F27_instance_event` (or F18b) | Screen Main: instance of FP_Interface, on the container event EvTest with script `HMIRuntime.Trace("EvTest");`; plus interface values different from the default: P_Bool True, P_Int 42, P_Real 3,5, P_ULInt 7, P_Color red, P_String "abc", P_Text "Hallo", P_Graphic, P_List text list, P_Auth right |
| `F12_iface_types` | FP_Interface: interface tags Real, DateTime, WString, Array[0..3] of Int, HMI UDT if possible |
| `F15_local_tag` | done: LocalInt ended up in FP_Interface 0.0.7, without limits, without binding (sufficient for storage) |
| `F15b_local_limits` (optional, later) | FP_Basic: local tag with limits 0..100, bound to an I/O field |
| `F20_type_props` | FP_Elements (edit type): faceplate properties display name `Elemente`, background yellow; Layer_1 → `Ebene2`, E_Button1 on Ebene2, "Visible in runtime" off; compile, release |
| `F21_second_version` | FP_Basic via "Edit type": move the rectangle, release as 0.0.2 (0.0.1 remains) |
| `F30_nested` | FP_Controls: set the empty container C_Faceplate1 to FP_Basic; compile, release |
| `F33_control_columns` | FP_Controls: hide a column of the alarm control/change its width, add a trend to the trend control |
| `F18c_event_funclist` | Main: on the FP_Interface instance switch the handler for EvTest from script to **function list** (SetTagValue HmiTag_Ext1 = 1, ActivateScreen, if possible with the event parameter Nr as value); a second instance keeps the script |
| `F36_popup_alwaysdownload` | FP_Basic: "Mark as AlwaysDownload" (User Tags), DefaultName "PopupBasic", release a new version; on Main a button with script `UI.OpenFaceplateInPopup("FP_Basic", "PopupBasic")` |
| `F32_multilingual` (last) | second project language German (also in the runtime settings), enter translated text in FP_Interface; export library texts as Excel to `samples\F32_texts.xlsx` |
