# OpenPEData

Reads TIA Portal V19, V20 and V21 projects (project folder, `.ap19`/`.ap20`/`.ap21`, `.zap19`/`.zap20`/`.zap21`) and
global libraries (library folder, `.zal19`/`.zal20`/`.zal21`) without TIA Portal. Results are available as typed .NET
objects or as JSON.

## Parts

| Folder | Content |
| --- | --- |
| `dotnet/src/OpenPEData` | Library `OpenPEData.dll` (.NET 10, no dependencies), NuGet package `OpenPEData` |
| `dotnet/src/OpenPEData.Cli` | Command-line program |
| `dotnet/tests/OpenPEData.Tests` | Tests of library and command-line program |
| `ui/OpenPEData.Viewer` | Windows viewer (WPF) with setup and auto-updater |
| `ui/OpenPEData.Viewer.Tests` | Viewer tests |

Details: [`dotnet/README.md`](dotnet/README.md), [`ui/README.md`](ui/README.md).

## Quick start

```
dotnet build dotnet/OpenPEData.sln -c Release
dotnet/src/OpenPEData.Cli/bin/Release/net10.0/OpenPEData.Cli.exe C:\Projects\Plant.zap21 --json
```

```csharp
using OpenPEData;

using var project = TiaProject.Open(@"C:\Projects\Plant.zap21");
TiaProjectContent content = project.GetContent();
```

## License

Mozilla Public License 2.0 (`LICENSE`, `NOTICE`).

OpenPEData is not affiliated with Siemens AG. SIMATIC, STEP 7, TIA Portal and WinCC are trademarks of Siemens AG.
The software is provided without warranty.
