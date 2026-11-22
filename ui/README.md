# OpenPEData Viewer

Windows viewer (WPF, .NET 10) for TIA Portal projects and global libraries. It uses the library `OpenPEData.dll`.

- Project tree in the order of TIA Portal, details of the selected object, HMI screen preview
- **Overview**: key figures, devices, integrity check, cross references
- **Export**: project report (HTML), all lists as CSV, parser result as JSON

## Run

```
dotnet run --project ui\OpenPEData.Viewer -- C:\Projects\Plant.zap21
```

Operation: `Ctrl+O` open, drag a file or folder into the window, `F5` reload, `Ctrl+F` search in project, `Ctrl+C`
copies table cells. Settings: gear button in the toolbar.

Command line: `OpenPEData.Viewer.exe <project> [--select "Project/PLC_1/Program blocks/Main"] [--tab <name>]
[--verify] [--xref] [--proginfo] [--dump <file.txt>] [--snapshot <image.png>]`.

## Tests

```
dotnet run --project ui\OpenPEData.Viewer.Tests -c Release
```

## Setup

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\ui\scripts\Pack-OpenPEData.ps1
```

Creates `ui\artifacts\Releases\OpenPEData-Setup-V<version>.exe` (Velopack, per-user installation, installs the .NET 10
Desktop Runtime if missing). A tag `vX.Y.Z` (`X.Y.Z` = `<Version>` in `OpenPEData.Viewer.csproj`) builds the setup and
attaches it to the GitHub release via `.github/workflows/release.yml`.

## Updates

The installed viewer checks the GitHub releases of this repository at startup and every 12 hours and shows a button
when a newer version exists; nothing is installed without a click. **Settings › Updates** turns the check off and
lists all releases for installation. The environment variable `OPENPEDATA_NO_UPDATE_CHECK=1` turns it off for a
machine.
