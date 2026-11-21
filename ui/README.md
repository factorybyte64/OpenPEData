# OpenPEData Viewer (WPF)

Windows UI for the parser `OpenPEData.dll` (`dotnet\`). Shows a TIA Portal V21 project (folder, `.ap21`, `.zap21`) or a global
library (`.al21`, `.zal21`) as a project tree like in TIA Portal and, on the right, the details of the selected object.
Plan: `docs\PLAN-WPF-UI.md`.

```
dotnet run --project ui\OpenPEData.Viewer -- samples\031_lad_all.zap21
```

Prerequisites: .NET 10 SDK (Windows). NuGet package:
`Velopack` 1.2.0 (installation hooks and auto-updater, see below).

## How it works

The viewer reads the project in its own process with the library `OpenPEData.dll` (`dotnet\src\OpenPEData`, see
`dotnet\README.md`) and builds the tree from its JSON. It contains no format logic of its own. The project is loaded once with
`TiaProject.Open`; the nodes "Integrity check", "Cross-references" and "Program info" fetch the result of `Verify`,
`GetCrossReferences` or `GetProgramInfo` from the same loaded project when first clicked.

## Tree

Project › device groups › PLC (Program blocks with groups and system blocks, PLC tags with "Show all
tags", PLC data types with system data types) or HMI (HMI tags, Screens, Connections, HMI alarms,
Text and graphic lists, Cycles) › Libraries (Types with versions and folders, faceplate types with versions,
Master copies, Type instances) › Statistics › Integrity check › Cross-references. A
station with exactly one CPU or one HMI appears, as in TIA, as one node `PLC_1 [CPU 1513-1 PN]`.

Right after loading, each PLC has **Program info** as its first child node. When it is first clicked, the
viewer starts `--proginfo` in the background; the detail pane and the status bar show "Loading program info …",
and the UI stays responsive. Then the tabs Call structure, Dependency structure, Assignment list, Resources
and Resources per block appear. One run delivers all PLCs of the project and is valid until the next load; an error is shown in the
detail pane with "Try again".

Likewise, **Integrity check** and **Cross-references** are always in the tree at project level. The first click starts
`--verify --json` or `--xref` in the background with a loading indicator; afterwards the node carries the result in its name
(`Integrity check [OK]`, `Cross-references [436]`) and shows its tabs. The tabs "Used by", "Uses" and
"Usages" on blocks, PLC data types, screens and tag tables are there from the start; opening one of them
before the cross-references have been read starts the same run. All three runs are valid until the next load
(`F5`), switching nodes during a run does no harm, errors are shown in the detail pane with "Try again". If the
project has no XRef.db (e.g. a library), the node is called `Cross-references [no XRef.db]` and the tabs say so.

Since 2026-10-29 (Z1.5 for the hardware and PLC output):

- per station **Device configuration** (device tree like the device view: slot, order number or subtype; per
  module Properties with all stored parameters and Openness names, tables Nodes, Addresses, Certificates)
  and for F-CPUs **Safety administration** (F-program settings without password, F-runtime groups, F-I/O);
- per PLC **PLC alarms** (type and instance alarms, alarm classes), **PLC alarm text lists** (one node per list with
  entries), **Watch and force tables** (one node per table with entries), **Software units**,
  **OPC UA server interfaces**, **ProDiag supervisions**; TO DBs show their interface with the stored
  parameters like instance DBs;
- at project level **Network** (Subnets, IO systems, Sync/MRP domains, Connections, Port interconnections) and
  **User management (UMAC)** (users with roles per device, roles with device rights).

Secrets (passwords, encrypted keys, certificate stores) are already output by the parser only as "(set, n bytes)".

## Detail pane

| Tab | Content |
| --- | --- |
| Content | child nodes of a folder |
| Properties | scalar fields, nested fields with a dot (`comment.en-US`) |
| Interface / Elements | tags with section, data type, start value, comment; Struct/UDT elements indented with path |
| Networks | SCL source text or LAD/FBD parts, operands and wires |
| Preview | screens and faceplate versions to scale with position, size and background color of the objects |
| Lists | every further object list as a sortable table (tags, screen items, alarms, versions, faceplate interface, local faceplate tags …) |
| Used by / Uses | usages of a block, PLC data type or screen from XRef.db (instructions with names such as `R_TRIG`); loads the cross-references when first opened |
| Usages | on a tag table, every usage of its tags; loads the cross-references when first opened |
| JSON | the raw object of the node |

The node "Integrity check" shows header and record hashes, hash chain and RSA signatures of the commits, the check
of `PEData.idx` against the plf (folders and `.ap21` only) and the savepoints of XRef.db and the search index. The node
"Cross-references" lists all usages and the tag addresses; entries for deleted objects that XRef.db still
holds are hidden and only counted.

Operation: open a file/folder or drag it in, `F5` reload, `Ctrl+F` tree filter. Cells can be copied with `Ctrl+C`
including the header.

## Command line

```
OpenPEData.Viewer.exe <project> [--select "Project/PLC_1/Program blocks/Main"] [--tab Networks]
                                [--verify] [--xref] [--proginfo] [--dump file.txt] [--snapshot image.png]
```

`--select` selects a node by the beginnings of the display names, `--tab` a tab. `--verify`, `--xref` and
`--proginfo` start the respective run right after loading, wait for it and, without `--select`, select the node
"Integrity check", "Cross-references" or the first node "Program info". `--dump` writes the visible
tree, the tabs and the content of the selected tab as text and exits the program (check without a screen).
`--snapshot` saves the window as PNG; this only works in a connected Windows session.
`OPENPEDATA_SOFTWARE_RENDER=1` forces software rendering.

## Tests

```
dotnet run --project ui\OpenPEData.Viewer.Tests -c Release
```

`OpenPEData.Viewer.Tests` checks the viewer's drawing logic without a window (template expressions such as
`Converter.Darker`, in the editor and in the runtime mode); exit code 0 means all checks passed. The parser tests are
in `dotnet\tests\OpenPEData.Tests` (see `dotnet\README.md`).

## Installer (Velopack)

Structure as for the TIA MCP server: `OpenPEData.Viewer` with `VelopackApp.Build().Run()` in `Program.cs`, `vpk pack`
(global tool `vpk` 1.2.0, the script installs it if needed) and a setup launcher `OpenPEData.SetupLauncher`
(.NET Framework 4.8), which carries the Velopack setup embedded and always reinstalls silently.

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\ui\scripts\Pack-OpenPEData.ps1
```

- Result: `ui\artifacts\Releases\OpenPEData-Setup-V<version>.exe` (version = `<Version>` in
  `OpenPEData.Viewer.csproj`, channel `win`, no delta packages, no portable variant). Next to it, in
  `ui\artifacts\_vpk-staging\`, the update package for the auto-updater: `releases.win.json` and
  `OpenPEData-<version>-full.nupkg`.
- Per-user installation under `%LocalAppData%\OpenPEData\` with shortcuts on the desktop and in the Start menu;
  uninstall via "Apps & Features". If the .NET 10 Desktop Runtime is missing, the setup installs it
  (`--framework net10-x64-desktop`). Python is not needed; the setup contains no Python code, only
  `OpenPEData.dll` (the pack script checks this).
- Signing: `-SignParams '<signtool arguments>'` or environment variable `OPENPEDATA_SIGN_PARAMS`; without, the
  setup stays unsigned (SmartScreen reports "unknown publisher").
- A Release build in Visual Studio creates the setup automatically; on the command line only with `-p:PackSetup=true`.
- The setup launcher `OpenPEData.SetupLauncher` is not part of `dotnet\OpenPEData.sln` and is built only by the pack
  script: it embeds `Embedded\SetupPayload.exe` (the Velopack setup, git-ignored), which the script writes after
  `vpk pack`, and then runs `dotnet publish` on the launcher. A plain `dotnet build` of the launcher without that file
  stops with error `OPDSETUP001` ("Embedded\SetupPayload.exe is missing"); build the viewer with
  `dotnet build ui\OpenPEData.Viewer -c Release -p:PackSetup=false` instead.

## Auto-updater

The installed viewer updates itself from the GitHub releases of `factorybyte64/OpenPEData` (code in
`OpenPEData.Viewer\Updates\`, Velopack `UpdateManager` with a GitHub source).

- **Automatic check (opt-out):** at startup (after 3 s) and every 12 hours the viewer reads the release list. If a
  newer version is there, the toolbar shows a button "Update V… available"; nothing is installed without a click.
  Turn it off in **Updates…** ("Check for updates automatically") or for a whole machine with the environment
  variable `OPENPEDATA_NO_UPDATE_CHECK=1`. No check in development builds (not installed with the setup) and in
  command line runs (`--dump`, `--snapshot`, `--screen-png`).
- **Updates… dialog:** lists every release with date, kind and release notes. Any release can be installed:
  newer ones as update, older ones as downgrade (with a confirmation). The viewer downloads the full package, closes,
  installs it and starts again with the project that was open. "Skip this version" hides the offered version until
  a newer one comes out; "Include pre-releases" also offers releases flagged as pre-release on GitHub (or with a
  version label such as `v0.3.0-beta.1`).
- **Settings:** `%AppData%\OpenPEData\updates.json` (outside the installation, kept across updates and downgrades).
- **Release assets:** the release workflow (`.github\workflows\release.yml`) attaches the setup, `releases.win.json`
  and `OpenPEData-X.Y.Z-full.nupkg` to every release. Each release carries its own feed, so the updater installs
  exactly the chosen version. Releases without `releases.win.json` (v0.1.0 and v0.1.1, built before the updater)
  are shown as "setup only"; running the release workflow by hand for their tag adds the package. Such old versions
  have no updater themselves: after a downgrade to them, the current setup brings the viewer back.
- **Limits:** GitHub allows 60 API requests per hour and IP without a token; the check uses one request.
