# WinCC Unified: text and graphic lists (Z1.3 U5; samples 158–159)

As of 2026-10-29. Truth:

- `TiaHmiUnified get textList`/`graphicList`;
- Openness export `HmiTextLists.Export`/`HmiGraphicLists.Export` (`textLists\HmiTextLists.hmi.yml` with `HmiTextLists.TextLibrary.hmi.yml`, `graphicLists\HmiGraphicLists.hmi.yml`).

Openness V21 has no Create for either list kind, only Import. TiaMcpXE `create textList` writes the import file and sets the texts via the project texts. Chain from 102_hmi_tag_int.

## Findings

| Sample | Change | Finding in the store |
|---|---|---|
| 158_hmi_textlist | User text list `TL_State`: 0 = "Stopped", 1 = "Running" | `HmiTextListData` "TL_State" (link `Target` = HMI software), two `HmiTextListEntryData` (`IHmiListEntryAttributes` From/To/EntryType SingleValue/DefaultEntry), texts as `HmiTextData` – like the system text lists already read (parser `textLists`, `system` = `IAdditionalAttributes.IsSystemTextList`) |
| 159_hmi_graphiclist | Graphic list `GL_State` without entries (import of a self-written `GraphicListContainers` file, structured like the text list export) | `HmiGraphicListData` "GL_State" with `IHmiGraphicListAttributes.Mode` "Enhanced", link `HmiGraphicListDataParent` = device, `Target` = HMI software |

In the export there is an entry with value 0 without `Value`/`FromValue`/`ToValue`; missing values are 0. The texts are in the text library (`MyTextLibrary.Text_n`, per language in the order of `Languages`).

Graphic list entries need project graphics. Openness cannot create these via TiaMcpXE (only `TiaProject graphicList` for listing). A sample with entries would need a graphic that the user creates by hand; this is open and optional. Openness itself shows only the name on the graphic list.

The parser already reads system text lists (25 on the Comfort Panel) (names tested against `systemTextLists.json`). Openness does not export them together with the user text lists.

## Parser

`openpedata\hmi.py`:

- `textLists` (unchanged; entries with from/to, type, default, text per language);
- new `graphicLists` (name, Mode).

Test:

- test_hmi compares the user text lists with the YAML export: names, per entry from/to and text per language (small YAML reader `simple_yaml`, because pyyaml is not installed).
- It compares the graphic lists with `graphicList.json`.
- 158–159 green.

Viewer: node "Text and graphic lists" with the tabs Text lists and Graphic lists.

## Graphic list entries (sample 168, 2026-11-07)

- `HmiGraphicListEntryData` hangs on the list (without a named relation); `IHmiListEntryAttributes` From/To/EntryType/
  DefaultEntry, `IHmiGraphicListEntryAttributes` flashing and transparency color; the graphic is a link to the
  project graphic (`HmiImageData`).
- YAML export: `Graphic: GraphicLibrary.<Name>`. An import with `Graphic: <Name>` creates the entries without a graphic.
- Parser: `graphicLists[].entries` with from, to, type, default, graphic; test `HmiGraphicListTests`.
