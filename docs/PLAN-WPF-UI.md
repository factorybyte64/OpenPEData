# Plan: WPF UI for OpenPEData

Goals and non-goals: see GOALS.md (the only valid source).

> **Status note (2026-11-20):** the decisions below describe the first version of the UI and are kept as history.
> Two of them no longer hold. The viewer does not start the Python parser as a process: it references the .NET library
> `OpenPEData.dll` (`dotnet\src\OpenPEData`) and runs it in process (`ui\OpenPEData.Viewer\ParserRunner.cs`,
> `TiaProject.Open`), so Python is not needed and `OPENPEDATA_PYTHON` / `OPENPEDATA_PARSER` are not used. The viewer
> uses one NuGet package, `Velopack` 1.2.0, for the installer (see `ui\README.md`, section Installer). The Python parser
> is for test and analysis only (`parser\README.md`).

## Decisions

- **No second parser.** The UI starts `parser\OpenPEData.py <path> --json` (or `--verify --json`) as a
  process and reads the JSON from stdout (UTF-8). This way every extension of the parser flows into the UI without change.
- **.NET 10, WPF, built-in means only** (`System.Text.Json`, `System.Data.DataTable`). No NuGet packages.
- Location: `ui\OpenPEData.Viewer\` (own `.gitignore` for `bin/`, `obj/`).
- Python: `%LOCALAPPDATA%\Programs\Python\Python312\python.exe`, otherwise `python` in the PATH; can be overridden via the
  environment variable `OPENPEDATA_PYTHON`. The parser is searched upward from the program folder
  (`parser\OpenPEData.py`), can be overridden with `OPENPEDATA_PARSER`.

## Tree (modeled on TIA Portal)

- Project
  - Device groups as folders, devices in them or directly under the project
    - PLC: program blocks (groups as folders), PLC tags (tables, groups), PLC data types
    - HMI: HMI tags, screens (groups), connections, HMI alarms (classes, discrete, analog alarms),
      text and graphic lists, cycles
  - Libraries: project library with types (versions), master copies, faceplate types, instances
  - Statistics (records, objects, hash errors)

## Detail area

- Properties: all scalar fields of the object (comments per language as separate rows).
- Tables: one table per list of objects (tags, elements, screen items, alarms …); nested
  interface elements indented with path.
- Blocks: interface (section, name, data type, start value, comment), networks (SCL source text or
  LAD/FBD parts and wiring as text).
- Screens: simple to-scale preview of the screen items (rectangles with name, background color).
- JSON: the raw object of the node.
- A search field filters the tree; "Check integrity" shows the result of `--verify`.

## Steps

1. Create project, parser call, load JSON
2. Build tree from the JSON
3. Detail area (properties, tables, code, preview, JSON)
4. Search, check, drag & drop, command-line argument
5. Test with samples (031_lad_all, F01_hmi_allobjects, L13, F05), README

## Status 2026-10-21

Steps 1-5 done (`ui\OpenPEData.Viewer`, instructions `ui\README.md`). Checked via `--dump` with 031_lad_all,
030_scl_all, 022_db_udt, 039_fw41, F01, F05, L17, L28, 010, 013 and `--verify` with 018. Visual check via screenshot
is pending: the Windows session was disconnected, WPF does not render then.

## Status 2026-10-29

Tree extended by the hardware and PLC output (device configuration, Safety, network, UMAC, PLC alarms, text lists, watch/force tables, software units, OPC UA, ProDiag; ui\README.md). Checked via `--dump` and `--snapshot` with 031 (unchanged), 044, 048, 057, 058, 126, 217, 221, 226, 229, 233, 239, 242, 253; screenshots rendered.
