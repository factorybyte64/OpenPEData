# F01: creatable WinCC Unified screen items (step F0)

As of 2026-10-20. Sources: reflection over `Siemens.Engineering.WinCCUnified.dll` (TIA V21, `PublicAPI\V21\net48`), the XML documentation next to it, `TiaHmiUnified(help)` for itemCreate/itemBindTag/itemBindScript/itemEventCreate, MCP resource `tia-mcp://docs/hmi-unified`, TiaMcpXE sources (read only).

`TiaHmiUnified(itemCreate)` calls `ScreenItems.Create<T>(name)` with any type under `Siemens.Engineering.HmiUnified.UI.*` (`ResolveScreenItemType`, `InvokeGenericCreate`). TiaMcpXE therefore does not restrict the types; Openness decides whether a type can be created. The column "Result F1" is filled in by run F1 (see `MANIFEST.md`, section "Faceplates F").

## 1. Screen item types (concrete derivations of `HmiScreenItemBase`)

### Basic objects (`UI.Shapes`) → screen FP_Basic

| Type | TIA name | Events (enum) |
| --- | --- | --- |
| HmiLine | Line | Activated, Deactivated, Tapped, KeyDown, KeyUp, ContextTapped |
| HmiPolyline | Polyline | as Line |
| HmiPolygon | Polygon | as Line |
| HmiEllipse | Ellipse | as Line |
| HmiEllipseSegment | Ellipse segment | as Line |
| HmiEllipticalArc | Elliptical arc | as Line |
| HmiCircle | Circle | as Line |
| HmiCircleSegment | Circle segment | as Line |
| HmiCircularArc | Circular arc | as Line |
| HmiRectangle | Rectangle | as Line |
| HmiText | Text | as Line |
| HmiGraphicView | Graphic view | as Line |

Abstract base types (not creatable): HmiShapeBase, HmiSurfaceShapeBase, HmiPointBasedShapeBase, HmiCentricShapeBase, HmiCircularShapeBase, HmiEllipticalShapeBase.

### Elements (`UI.Widgets`) → screen FP_Elements

| Type | TIA name | Events |
| --- | --- | --- |
| HmiButton | Button | Activated, Deactivated, Tapped, KeyDown, KeyUp, Down, Up, ContextTapped |
| HmiIOField | I/O field | Activated, Deactivated, Tapped, KeyDown, KeyUp, ContextTapped |
| HmiSymbolicIOField | Symbolic I/O field | as I/O field |
| HmiTextBox | Text box (text input) | as I/O field |
| HmiLabel | Label | no event handler type of its own |
| HmiBar | Bar | as I/O field |
| HmiSlider | Slider | as I/O field |
| HmiGauge | Gauge | as I/O field |
| HmiClock | Clock | as I/O field |
| HmiToggleSwitch | Toggle switch | as Button + StateChanged |
| HmiCheckBoxGroup | Check box (group) | as I/O field |
| HmiRadioButtonGroup | Radio button (group) | as I/O field |
| HmiListBox | List box | as I/O field |
| HmiTouchArea | Touch area | GestureDetected |
| HmiAlarmIndicator | Alarm indicator | AlarmStateTapped |

No Openness type in V21: combo box (`HmiComboBox`: only `HmiComboBoxEventHandler` and `HmiComboBoxEventType` exist, no object class), graphic I/O field (replaced in Unified by graphic view + dynamization), symbol library/Dynamic SVG (no class of its own; SVG goes via HmiGraphicView). Abstract: HmiWidgetBase, HmiTextWidgetBase, HmiScaleWidgetBase, HmiSelectionGroupBase.

### Controls (`UI.Controls`, `UI.Screens`, `UI.Base`) → screen FP_Controls

| Type | TIA name | Events |
| --- | --- | --- |
| HmiAlarmControl | Alarm control | Activated, Deactivated, Initialized, CommandFired |
| HmiAlarmLineControl | Alarm line | Activated, Deactivated |
| HmiTrendControl | Trend control | Activated, Deactivated, Initialized, CommandFired |
| HmiTrendCompanion | Trend companion (value table) | as Trend control |
| HmiFunctionTrendControl | f(x) trend control | as Trend control |
| HmiProcessControl | Process control (table view) | as Trend control |
| HmiDetailedParameterControl | Parameter control (detailed) | as Trend control |
| HmiWebControl | Web control | as Trend control |
| HmiMediaControl | Media player | as Trend control + Paused, Playing, Stopped |
| HmiSystemDiagnosisControl | System diagnostics view | as Trend control |
| HmiProcessDiagnosisOverviewControl | ProDiag overview | as Trend control + AlarmViewButtonTapped |
| HmiProcessDiagnosisGraphOverviewControl | GRAPH overview | as ProDiag overview + PlcCodeViewerButtonTapped, TIAPortalButtonTapped |
| HmiProcessDiagnosisCriteriaAnalysisControl | Criteria analysis | as Trend control |
| HmiProcessDiagnosisPlcCodeViewerControl | PLC code view | as Trend control |
| HmiScreenWindow | Screen window | Activated, Deactivated |
| HmiFaceplateContainer | Faceplate instance | Activated, Deactivated (creating requires a faceplate type, which Openness V21 does not create) |
| HmiCustomWebControlContainer | Custom Web Control | handler type of its own (creating requires an installed custom control type) |
| HmiCustomWidgetContainer | Custom Widget | handler type of its own |

Abstract: HmiScreenItemBase, HmiSimpleScreenItemBase, HmiContainerBase, HmiControlWindowBase, HmiWindowBase, HmiTrendControlBase, HmiCompanionBase. Only event handler, no object class: HmiDotNetControlContainer.

Recipe view: no type (Openness V21 does not offer recipes for Unified). Screen item groups (grouping in the editor): no class in the UI namespace; `HmiGroupBase` (`HmiUnified.Common`) is the base of tag/screen groups, not of screen item groups. Popup/slide-in screens: not available according to the documentation.

## 2. Screen itself (`HmiScreen`)

Properties: AlternateBackColor, BackColor, BackFillPattern, BackGraphic, BackGraphicStretchMode, BackgroundFillMode, Enabled, Height, HorizontalAlignment, ScreenNumber, VerticalAlignment, Width, DisplayName, Name; compositions ScreenItems, EventHandlers, Dynamizations, PropertyEventHandlers. Events (`HmiScreenEventType`): Tapped, ContextTapped, Loaded, Unloaded. Screen groups: `HmiScreenGroup` (`groupCreate`).

## 3. Dynamization kinds (`DynamizationType`)

| Kind | Openness class | via TiaMcpXE |
| --- | --- | --- |
| Tag | TagDynamization (+ MappingTable: SingleBit, MultiBit, Range) | `itemBindTag` binding=dynamization, evaluationType direct/singleBit/range; binding=processValue (I/O field); binding=pressedState (button) |
| Script | ScriptDynamization (ScriptCode, GlobalDefinitionAreaScriptCode, Trigger) | `itemBindScript` (element or screen), with SyntaxCheck |
| Flashing | FlashingDynamization | only via `importXml screen` (element `<DynamizationType>Flashing</DynamizationType>`) |
| Expression | ExpressionDynamization | not offered |
| ResourceList | ResourceListDynamization | not offered |
| TagParameter | TagParameterDynamization (faceplate interface) | not offered |

## 4. Event kinds

- Element events: `EventHandlers.Create(<Type>EventType)`, via `itemEventCreate(eventType, script)`; aliases Click→Tapped, Press→Down, Release→Up. Values per type see tables above.
- Property events: `PropertyEventHandlers.Create(propertyName, PropertyEventType)` with `Change` or `QualityCodeChange`, via `itemEventCreate(propertyName=…, eventType=Change)` (e.g. I/O field ProcessValue).
- Screen events: `itemEventCreate` without `name` (Loaded, Unloaded, Tapped, ContextTapped).

## 5. Global script modules

`HmiScriptModuleComposition` has no `Create`, only `Import(DirectoryInfo[, name])` and `Export`. TiaMcpXE offers `scriptModule list/get/export/import`; according to the documentation, the file format (.js + .yml) is undocumented. Creating is only possible via import.

## 6. Nested parts

`partGet/partCreate/partSet/partDelete` (e.g. `Points` of polyline/polygon, `TrendAreas`, `PressedStateTags`, columns of controls). The documentation calls everything except Trend Areas "not tested live".

## 7. Result F1 (run 2026-10-20, MTP700 Unified Comfort, TIA V21)

| Type | Result | Object in F01 |
| --- | --- | --- |
| all 12 basic objects | created | FP_Basic: B_Rect1, B_Line1, B_Polyline1 (+1 point), B_Polygon1 (+1 point), B_Ellipse1, B_EllipseSeg1, B_EllArc1, B_Circle1, B_CircleSeg1, B_CircArc1, B_Text1, B_GraphicView1 |
| 14 elements | created | FP_Elements: E_Button1, E_IOField1, E_SymIOField1, E_TextBox1, E_Bar1, E_Slider1, E_Gauge1, E_Clock1, E_Switch1, E_CheckBox1 (+1 SelectionItem), E_Radio1, E_ListBox1, E_TouchArea1, E_AlarmInd1 |
| HmiLabel | not creatable | "Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'. Not supported" |
| HmiComboBox | no type | "Unknown screen item type 'HmiComboBox'. Use a short name (e.g. HmiLine, HmiButton) or full Openness type name." |
| 14 controls + faceplate container | created | FP_Controls: C_AlarmControl1, C_AlarmLine1, C_Trend1, C_TrendCompanion1, C_FTrend1, C_DetParam1, C_Web1, C_Media1, C_SysDiag1, C_ProDiagOv1, C_GraphOv1, C_CritAnalysis1, C_PlcCode1, C_ScreenWin1 (Screen=Main), C_Faceplate1 (without type: ContainedType empty, Interface 0) |
| HmiProcessControl | not creatable (device) | "HmiProcessControl object is not supported for this device." |
| HmiCustomWebControlContainer, HmiCustomWidgetContainer | not creatable | Short name: "Unknown screen item type …" (namespace UI.Base is not searched); full name: "'ContainedTypeValue' parameter is missing. Please use correct method for object creation." |

Dynamizations: Tag direct (I_Bar1 ProcessValue, X_Rect1 Visible), Tag SingleBit (I_Rect1 BackColor), Tag Range (I_Rect1 BorderColor), processValue (I_IOField1, S_IOField1; has no effect, see correction), script (I_Text1 ForeColor with global definition area, screen FP_Interface BackColor), Flashing (X_Rect1 BackColor, only via exportXml → extend XML → importXml). Expression, ResourceList and TagParameter: not offered. **Correction (MCP-025):** `itemBindTag binding=processValue` only wrote the static text "HmiTag_Ext1" into ProcessValue on I_IOField1 and S_IOField1, no tag dynamization (truth: `"ProcessValue":"HmiTag_Ext1"`, `dynamizations` empty). Real ProcessValue binding in the sample only on I_Bar1 (binding=dynamization, direct).

Events: S_Button1 Tapped and Down, S_IOField1 ProcessValue/Change (property event), screen FP_Script Loaded. Events on HmiToggleSwitch fail (StateChanged and Tapped): "Ambiguous match found."

Script module: not creatable. `import scriptModule` with only .js: "Couldn't find any YAML files to import in folder …"; with a guessed .yml (`Name: F01_Module`): "Could not perform import operation: Couldn't find any YAML files to import."

TiaMcpXE error "Ambiguous match found." (AmbiguousMatchException): in Openness V21, HmiSlider (inherits from HmiBar), HmiToggleSwitch (from HmiButton), HmiCircleSegment (from HmiCircle) and HmiEllipseSegment (from HmiEllipse) redeclare the property `EventHandlers`. TiaMcpXE looks it up via `GetProperty("EventHandlers")` and fails. Affected: `itemEventCreate` on these types, `get screen` (detail items and full), `scriptSearch` and `exportXml` for every screen that contains one of these types (FP_Basic, FP_Elements). `itemGet` per object works.
