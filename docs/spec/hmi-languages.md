# WinCC Unified: languages and fonts (Z1.3 U3; samples 153–155)

As of 2026-10-29. Truth:

- `TiaProject languageGet` (`projectLanguages.json`: active project languages, editing and reference language);
- `TiaHmiUnified list languageAndFont` (`languageAndFont.json`: per project language index, order, enable, enableForLogging, fonts).

Chain from 102_hmi_tag_int, one change per sample via MCP, differences in `<sample>_attrdiff.txt`. Openness can neither create nor delete runtime languages; they follow the project languages. `Enable` and `EnableForLogging` are writable. TIA V21 rejects DefaultFont on the Comfort Panel (TiaMcpXE docs).

## Findings

| Sample | Change (Openness) | Finding in the store |
|---|---|---|
| 153_hmi_lang_de | Project language de-DE added (`languageSet add=de-DE`) | `CoreCulture` "de-DE(1031)" gets links `CultureIsUsedBy` → ProjectData and project library; ProjectData link `UsedCultures` (active languages in order, alongside `EditingCulture`, `ReferenceCulture`). 97 `HmiTextData` and 10 `HmiFontListData` get an empty entry for 1031 (`ICoreMultiCultureObjectInternalData.1031`). In Openness the runtime language exists but is not enabled (order empty) |
| 154_hmi_lang_enable | de-DE `Enable` true | `HmiLanguageAndFontSettingsData.IHmiLanguageAndFontSettingsAttributes.OnlineLanguageOrder` "en-US" → "en-US;de-DE": enabled languages as cultures, the position is Openness `order` |
| 155_hmi_lang_logging | de-DE `EnableForLogging` true | `HmiRuntimeSettingsData.IAdditionalAttributes.LoggingLanguages`: u32 size, u32 count, count × u32 LCID (0x409 en-US → 0x409, 0x407) |

Fonts: `HmiLanguageAndFontSettingsData` refers via `IHmiLanguageAndFontSettingsAttributes_DefaultFontList` to an `HmiFontListData` "DefaultFontList" without per-language data. The other `HmiFontListData` (FontList_SiemensSans_<size>[_<weight>]) are the font lists used in the project, one entry per culture. The font "Siemens Sans", which Openness reports as DefaultFont and FixedFont1, is not stored as text. It is the default and is not output.

## Parser

- `project.languages` (`model.project_languages`): `active`, `editing`, `reference`.
- Per HMI software `runtimeLanguages` (`Hmi.runtime_languages`): per project language `language`, `order`, `enable`, `enableForLogging`.

Test: test_hmi compares project languages, editing and reference language, and per runtime language enable, enableForLogging and order with the truth; 153–155 green.

Viewer: tab "Runtime languages" in the node "Runtime settings"; the project languages are in the properties of the project.
