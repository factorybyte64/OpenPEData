# OpenPEData for .NET

Library `OpenPEData.dll`, command-line program and test program (.NET 10, no NuGet dependencies).

## Build

```
dotnet build dotnet\OpenPEData.sln -c Release
```

## Command line

```
OpenPEData.Cli.exe <project folder | .ap21 | .zap21 | library folder | .zal21> [switches]
```

| Switch | Output |
| --- | --- |
| (none) | project structure as text |
| `--json` | project structure as JSON |
| `--keys` | with `--json`: include record keys |
| `--verify` | integrity check; exit code 1 if it fails |
| `--xref` | cross references (JSON) |
| `--proginfo` | program information per PLC (JSON) |
| `--simaticml` | LAD/FBD networks as SimaticML (JSON) |
| `--sources=<folder>` | blocks, PLC data types and PLC tag tables as source files (`.scl`, `.awl`, `.db`, `.udt`; LAD/FBD/GRAPH and tag tables as SimaticML `.xml`) |
| `--comm` | communication data per PLC: network interfaces, access settings, OPC UA, symbols (JSON) |

Exit codes: 0 ok, 1 not readable or integrity check failed, 2 wrong arguments.

## Library

```csharp
using OpenPEData;

using var p = TiaProject.Open(@"C:\Projects\Plant.zap21");
TiaProjectContent content = p.GetContent();          // typed: devices, PLC and HMI software, libraries
IntegrityReport check = p.GetIntegrityReport();      // typed integrity check
JsonObject structure = p.GetStructure();             // same as --json
JsonObject? xref = p.GetCrossReferences();           // same as --xref (null without XRef.db)
JsonObject info = p.GetProgramInfo();                // same as --proginfo
SourceExportResult src = p.ExportSources(@"C:\Export"); // same as --sources
TiaCommunicationInfo comm = p.GetCommunication();    // same as --comm
IReadOnlyList<ParseIssue> issues = p.GetIssues();    // objects that could not be read
TiaProjectInfo? what = TiaProject.Probe(path);       // version and kind without loading
```

- Supported versions: `TiaVersion.Supported` (V19, V20, V21). Other versions throw `UnsupportedVersionException` unless
  `OpenOptions.AllowUnknownVersion` is set.
- All exceptions derive from `OpenPEDataException`.
- One instance handles calls one after another; separate instances can run in parallel. Every method takes a
  `CancellationToken`.

## NuGet package

`dotnet pack dotnet\src\OpenPEData -c Release` creates the package. A tag `lib-vX.Y.Z` (`X.Y.Z` = `<Version>` in
`OpenPEData.csproj`) publishes it on nuget.org via `.github/workflows/nuget.yml`.

## Tests

```
dotnet\tests\OpenPEData.Tests\bin\Release\net10.0\OpenPEData.Tests.exe [name filter] [--samples text]
```

The tests run against the test projects in `samples\` and the expected results in `baseline.json` and `golden.json`.
