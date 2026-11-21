# OpenPEData for .NET

The OpenPEData parser: a library `OpenPEData.dll`, a command-line program and a test program (.NET 10).
Goals and non-goals: `docs\GOALS.md`.

It started as a literal port of the Python parser `parser\openpedata`; up to 2026-11-01 the output of both was
**byte-identical** for every sample and every output mode. Format findings made after that date were implemented here
only (among others: stored PROFINET device names, used products, OPC UA server interface nodes, TO hardware
connections, force tables, plant object types, HMI start screen, graphic list entries, HMI library types), so the
Python parser is behind for the samples that show them. The differential test below therefore compares against
`baseline.json`, the stored output of this parser for every sample (a regression baseline). The Python parser remains
in the repository **only for test and analysis purposes** (truth tests in `parser\tests`, format research with
`tools\` and `analysis\`); only `OpenPEData.dll` is shipped and used.

No NuGet packages, no installation: SQLite (`XRef.db`) is read by our own reader (`Format\SqliteReader.cs`), zlib by our own
decoder (`Runtime\Zlib.cs`), XML by `System.Xml.Linq`, RSA by `System.Numerics.BigInteger`.

## Structure

| Folder | Content |
| --- | --- |
| `src\OpenPEData` | Library. Only the facade is public: `TiaProject`, `TiaVersion`, `TiaProjectInfo`, `OpenOptions`, `TiaContent`, `ParseIssue`, the typed results (`Results\`) and the exceptions (`OpenPEDataException` …). Everything below (`Runtime`, `Format`, `Model`, `TextTree` = text tree of the CLI) is `internal`; tests and CLI see it via `InternalsVisibleTo` |
| `src\OpenPEData\Runtime` | the Python semantics of the original output: `PyDict` (ordered dict, keys as in Python), `Py` (truth value, comparison, `sorted`, `repr`/`str`, float `repr`, `round`, slicing, decoding with replacement), `PyJson` (`json.dumps` with `indent`, `ensure_ascii`), `Bin` (`struct.unpack_from` with `struct.error`), `Zlib`, `Et` (ElementTree on `XElement`) |
| `src\OpenPEData\Format` | plf, object record, metamodel, attributes/expandos/blobs, project loading, `--verify`, PEData.idx, SQLite, XRef |
| `src\OpenPEData\Model` | Project model, interfaces, code (SCL/LAD/FBD), PLC alarms and tables, program info, hardware, HMI, styles, library, faceplates |
| `src\OpenPEData\Results` | Typed results of the facade: `TiaProjectContent` (devices, PLC software, HMI software, libraries), `IntegrityReport`, `MultilingualText`; read from the JSON structure, each object keeps its JSON node |
| `src\OpenPEData\Data` | `unified_enums.json` (Openness enum names of the Unified style values) |
| `src\OpenPEData.Cli` | `OpenPEData.Cli.exe` (UTF-8, CRLF) |
| `tests\OpenPEData.Tests` | Test program without a test framework (no packages) |
| `tools` | `run_cli.py` (run a CLI for all samples and modes, Python reference or .NET), `compare.py` (compare two runs byte by byte), `make_golden.py` (expected values of the Python semantics), `truth_on_dotnet.py` / `truth_compare.py` (Python truth tests on the .NET results), `sample_inputs.py`; test and analysis only |

Origin of the files (Python module → C#): `plf.py` → `Format\Plf.cs`, `record.py` → `ObjectRecord.cs`,
`metamodel.py` → `Metamodel.cs`, `attrs.py` → `Attrs.cs`, `project.py` → `Project.cs`, `verify.py`/`pidx.py` →
`Verify.cs`, `xref.py` → `Xref.cs`, `model.py` → `Model\Model.cs`, `plc_alarms.py`/`plc_tables.py` → `PlcTables.cs`,
`proginfo.py` → `ProgInfo.cs`, `interface.py`, `code.py`, `hardware.py`, `styles.py`, `hmi.py`, `library.py`,
`faceplates.py` → files of the same name, `OpenPEData.py` → `OpenPEData.Cli\Program.cs` + `TextTree.cs`.

## Build and use

```
dotnet build dotnet\OpenPEData.sln -c Release
dotnet\src\OpenPEData.Cli\bin\Release\net10.0\OpenPEData.Cli.exe samples\031_lad_all.zap21 --json
```

Switches: `--json`, `--keys`, `--verify`, `--xref`, `--proginfo`. Inputs: project folder,
`.ap21`, `.zap21`, library folder, `.zal21`.

Library:

```csharp
using OpenPEData;

using var p = TiaProject.Open(@"C:\Projects\Plant.zap21");   // load once: read plf, hashes, metamodel
TiaVersion v = p.Version;                                    // V21 (from .ap21/.zap21/.al21/.zal21)
TiaProjectContent content = p.GetContent();                 // typed: Devices, Plcs (Blocks, Tags, DataTypes), Hmis (Tags, Screens), Libraries
IntegrityReport check = p.GetIntegrityReport();             // typed --verify: files, commits (chain, signature), index
JsonObject structure = p.GetStructure();                     // --json as a System.Text.Json tree (includeKeys: --keys)
IReadOnlyList<ParseIssue> issues = p.GetIssues();            // unreadable objects (statistics.errors)
JsonObject report = p.Verify();                              // --verify --json; p.IsIntact() = exit code 0
JsonObject? xref = p.GetCrossReferences();                   // --xref (null without XRef.db)
JsonObject info = p.GetProgramInfo();                        // --proginfo
string json = p.ToJson(TiaContent.Structure);                // exactly the CLI output with --json
string text = p.ToText();                                    // exactly the output without --json (text tree)
TiaProjectInfo? what = TiaProject.Probe(path);               // detect without loading (version, kind, archive)
```

- Each partial result is computed on the first call and kept; every call returns a new `JsonObject`. The typed results
  (`GetContent`, `GetIntegrityReport`) are computed once and the same object is returned on every call. Each typed object
  has the typed properties of the common content and its JSON node (`Json`) with all further details.
- Calls on one instance run sequentially (one lock), different instances in parallel. Every method takes a
  `CancellationToken`; building the structure checks it before every block and data type.
- Errors: `NotATiaProjectException` (no folder/archive with `System\PEData.plf`, no metamodel),
  `UnsupportedVersionException` (different TIA version, property `Version`), `UnsupportedFormatException` (version not
  recognizable), `CorruptProjectException` (not readable, original error as `InnerException`), all derived from
  `OpenPEDataException`. With `OpenOptions { AllowUnknownVersion = true }` an unsupported version is read with the
  reader of the newest supported one; the notice is in `Warnings` (the CLI always does this, like the Python parser).
- Supported versions: `TiaVersion.Supported` (today V21), derived from the version profiles
  (`Format\VersionProfile.cs`: metamodel record, embedded data files). A profile is only added together with test projects
  of that version; tables only move into the profile once a version demonstrably differs.
- Limits for foreign files (`OpenOptions`): `MaxEntryBytes` (one archive entry, default 1 GiB), `MaxDecompressedBytes`
  (one decompressed blob, default 256 MiB); XML entities at most 10 million characters, no external entities.
- Errors in the core: only `struct.error`/`IndexError` count as "not decodable", as in Python; .NET errors such as
  `IndexOutOfRangeException` are errors of the port and appear as such. Cancellation, `OutOfMemoryException` and too deep
  recursion never go into `statistics.errors`.

## NuGet package

```
dotnet pack dotnet\src\OpenPEData -c Release -o <dir>
```

creates `OpenPEData.<version>.nupkg` (library, XML documentation, `PACKAGE.md` as package readme, `NOTICE`, license
expression MPL-2.0, no dependencies) and `OpenPEData.<version>.snupkg` (symbols). The metadata is in
`src\OpenPEData\OpenPEData.csproj`; Source Link of the .NET SDK embeds the repository URL and the commit. The package
version is the `<Version>` of the library.

Publishing on nuget.org: push a tag `lib-vX.Y.Z` (`X.Y.Z` = `<Version>` of the library). The workflow
`.github/workflows/nuget.yml` runs the full CI, packs the library and pushes `.nupkg` and `.snupkg` to nuget.org. The
library tags are independent of the viewer tags `vX.Y.Z`. Login is nuget.org Trusted Publishing (repository variable
`NUGET_USER` = nuget.org profile name, policy on nuget.org for `factorybyte64/OpenPEData`, workflow `nuget.yml`);
alternatively a repository secret `NUGET_API_KEY`.

## Tests

```
dotnet build dotnet\OpenPEData.sln -c Release
dotnet\tests\OpenPEData.Tests\bin\Release\net10.0\OpenPEData.Tests.exe
```

- `DifferentialTests`: for all inputs (`samples\`, `analysis\real\`) and all seven modes (`--json --keys`, `--json`,
  text, `--verify --json`, `--verify`, `--xref`, `--proginfo`) the in-process library against `baseline.json`
  (SHA-256 of stdout and stderr, exit code); plus the CLI as a process (byte for byte incl. CRLF, usage, exit codes)
  and the public API (`TiaProject`: results, version detection, exceptions, cancellation, Dispose). `SessionReuse`
  computes all seven modes with one `TiaProject` instance per input (reverse order) and also checks against
  the baseline. Inputs that are missing locally (e.g. the demo copy in `analysis\real`) are skipped.
- `ProjectTests`: integrity of all samples, tampering test, idx and XRef consistency, tag addresses, metamodel.
- `UnitTests`: simple expando, neutral culture, inline codes, kind-4 blobs with store records, value types, blob kinds,
  varint, CoreText, addresses, maps, dict/sorting/slicing semantics, ElementTree.
- `TypedResultTests`: the typed results against the JSON structure on every input (devices, PLCs, blocks, tags, data
  types, HMI tags, screens, items, libraries, integrity report) and against the Openness truth on selected samples.
- `GoldenTests`: the Python semantics of the output against `golden.json` (generated with Python): float `repr`, `round`,
  `repr`, `json.dumps`, UTF-8/UTF-16/ASCII with replacement, zlib incl. error messages, SQLite tables, timestamps.

Filter: `OpenPEData.Tests.exe Golden`, `--samples 031` (only inputs containing this text).

New baseline after a deliberate change of the output or of the samples (check the differences first, for example
with `dotnet\tools\compare.py` against a run of the previous build):

```
python dotnet\tools\run_cli.py <temp>\OpenPEData-ref dotnet\src\OpenPEData.Cli\bin\Release\net10.0\OpenPEData.Cli.exe
OpenPEData.Tests.exe --write-baseline <temp>\OpenPEData-ref           # baseline.json from this run
python dotnet\tools\make_golden.py                                   # golden.json (Python semantics, XRef.db tables of samples)
python dotnet\tools\truth_compare.py <temp>\OpenPEData-ref <temp>\opd-truth   # truth tests on .NET results
```

`--ref <dir>` compares against a stored run instead of `baseline.json`; `run_cli.py <dir> python` stores a run of the
Python parser (equal to .NET only for the samples without the later findings listed above).

## Status (2026-11-20)

- Release build of the solution and of the viewer: 0 warnings, 0 errors.
- Test program: 72 of 72 tests green (no filter, 264 inputs from `samples\`; `analysis\real` and `samples\local_gsd`
  are local only and skipped when missing). `baseline.json` was regenerated on 2026-11-20 after the samples 039, 065,
  068 and 072 were rebuilt (2026-11-10/11) and after the fix of the Singlebit mapping masks (`HmiMappingTests`).
- CLI over all 264 inputs × 7 modes: exit code 0, no stderr output, no `statistics.errors`, `--verify` ok everywhere.
- The eight Python truth tests in `parser\tests` pass, and `tools\truth_compare.py` gives the same results on the .NET
  results as on the Python results (exit code 0; it ignores the run time column of the faceplate and library tables).
- History: up to 2026-11-01 the output of the Python parser (commit 49e3508) and of this parser was byte-identical for
  444 inputs × 7 modes; the Python parser is behind for later findings.
