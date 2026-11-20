# Local TIA help API (TIA Portal V21)

User's rule (2026-10-20): Siemens documentation is read primarily via the locally installed help, entry point `https://localhost:5112/en-us/home?api=PortalV21`. Read only.

## Service

- Process `Siemens.Automation.Help.Viewer.Web.Service` (port 5112, `127.0.0.1` and `::1`, HTTPS with a local certificate); plus `Siemens.Automation.Help.Viewer.WebApi.SelfHost`.
- The web UI is an Angular app ("TIA Help Viewer"); the data comes from `https://localhost:5112/PortalV21/HelpViewer/<Method>`.
- The server only answers over HTTP/2. HTTP/1.1 clients fail: `curl` (Schannel, "SEC_E_UNSUPPORTED_FUNCTION"), PowerShell `Invoke-WebRequest` ("connection was closed") and Python `urllib` ("Remote end closed connection"). A browser is reliable, e.g. a browser with `fetch()` in the page context of the help (`javascript_tool`).

## Methods (GET)

| Method | Parameters | Result |
| --- | --- | --- |
| `GetInstalledLanguages` | – | installed languages |
| `Toc` | `webApiParamsString={"Culture":"en-us","RequestUrl":""}` | entire table of contents as a JSON map (approx. 110 MB, 80 000 nodes). Key `"<Package>/<Folder>/<Page>.htm|<Title>"`, value `{id, hasChildren, children[]}` |
| `ScopeFilters` | like Toc | `SoftwareDocumentation`, `HardwareManual` |
| `DeviceFilters` | like Toc | e.g. `S7-1500`, `S7-1200`, `RUNTIME_UNIFIED`, `RUNTIME_ADVANCED`, `COMFORT_PANEL_RUNTIME` … |
| `SearchV2` | `culture=en-us&&searchText=<url-encoded>&&device='<Device>'&&scope='<Scope>'` (double `&&` as in the app; `scope` is mandatory) | `{"SearchResults":[{"Title","TopicId","TopicIcon","HintText"}]}` |
| `Html` | `webApiParamsString={"Culture":"en-us","RequestUrl":"https://localhost:5112/PortalV21"}&&helpReferenceString=<url-encoded JSON>` | page as a JSON string with HTML (images embedded) |
| others | `GetHelpPackage`, `GetHelpCacheStatus`, `TileData`, `GetFirstStepsHtml`, `PrintHtml`, `ReadTheme` | for the UI |

`helpReferenceString` is a serialized `Siemens.Automation.Help.Viewer.BL.HelpReference`. It works with `{"ItemId":"<Package>/<Path>","PackageName":"<Package>","InternalFileName":"<Path>","IsPackagedFileReference":true,"IsHtmlFileReference":true}`; `ItemId` alone returns `null`.

## Example (run in the browser context of the help)

```js
const base = 'https://localhost:5112/PortalV21/HelpViewer';
// Search
const s = await (await fetch(`${base}/SearchV2?culture=en-us&&searchText=${encodeURIComponent('faceplate interface event')}&&device='RUNTIME_UNIFIED'&&scope='SoftwareDocumentation'`)).json();
// Fetch page (TopicId from the search)
const id = s.SearchResults[0].TopicId;            // e.g. ScreensWCCUenUS/100655884811/152006285451.htm
const [pkg, ...rest] = id.split('/');
const ref = {ItemId: id, PackageName: pkg, InternalFileName: rest.join('/'), IsPackagedFileReference: true, IsHtmlFileReference: true};
const p = JSON.stringify({Culture: 'en-us', RequestUrl: 'https://localhost:5112/PortalV21'});
const html = JSON.parse(await (await fetch(`${base}/Html?webApiParamsString=${p}&&helpReferenceString=${encodeURIComponent(JSON.stringify(ref))}`)).text());
```

## Relation to the help files

The contents are also available as ZIP packages in `C:\Program Files\Siemens\Automation\Portal V21\Help\<Language>\<Package>.zip` (table of contents `Toc/Default.xml`, attributes `title`, `itemid`; pages under `<Folder>/<Page>.htm`). Checked 2026-10-20: all 81 faceplate pages of `ScreensWCCUenUS` have the same text via API and ZIP (SHA-256 of the normalized text, scripts and styles removed, whitespace collapsed). Differences exist only in the HTML frame and in embedded images. The ZIPs are therefore suitable for bulk evaluations without a browser; the API is more convenient for search and navigation. The API's `TopicId` corresponds to the `itemid` in the ZIP table of contents.

Do not copy help text into the repository, only our own summaries with source reference (package and page title).
