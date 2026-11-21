# OpenPEData

Open reader for TIA Portal project files. It reads a project folder, an `.apNN` file, a `.zapNN` archive, a global
library folder or a `.zalNN` archive directly from disk, without TIA Portal and without Openness. No dependencies.

Supported versions: `TiaVersion.Supported` (today TIA Portal V21). The format was worked out independently from our own
test projects; see the repository for the format description and the test samples.

## Typed results

```csharp
using OpenPEData;

using var project = TiaProject.Open(@"C:\Projects\Plant.zap21");
TiaProjectContent content = project.GetContent();

foreach (PlcSoftware plc in content.Plcs)
{
    Console.WriteLine($"{plc.Name}: {plc.TypeName} {plc.Firmware}");
    foreach (PlcBlock block in plc.Blocks)
        Console.WriteLine($"  {block}  {block.Language}");
    foreach (PlcTag tag in plc.Tags)
        Console.WriteLine($"  {tag.Name} : {tag.DataType} {tag.Address}  {tag.Comment.Get("en-US")}");
}

foreach (HmiSoftware hmi in content.Hmis)
    foreach (HmiScreen screen in hmi.Screens)
        Console.WriteLine($"{hmi.Name}/{screen.Name}: {screen.Items.Count} items");

IntegrityReport report = project.GetIntegrityReport();   // hashes, hash chain and signatures of the records
Console.WriteLine(report.IsIntact ? "intact" : string.Join("\n", report.Problems));
```

Every typed object also carries its JSON node (`Json`) with all details the reader emits, for example the hardware
configuration of a device, the interface and code of a block, or the properties and dynamizations of a screen item.

## JSON results

```csharp
JsonObject structure = project.GetStructure();          // whole project as a System.Text.Json tree
JsonObject? xref = project.GetCrossReferences();         // cross references (null without XRef.db)
JsonObject info = project.GetProgramInfo();              // resources, call structure, assignment list
string json = project.ToJson(TiaContent.Structure);     // same text as the command line program with --json
TiaProjectInfo? what = TiaProject.Probe(path);           // detect version and kind without loading
```

Errors derive from `OpenPEDataException` (`NotATiaProjectException`, `UnsupportedVersionException`,
`UnsupportedFormatException`, `CorruptProjectException`). Objects that cannot be read do not stop the reading; they are
listed by `GetIssues()`.

## License and legal

Mozilla Public License 2.0. OpenPEData is not affiliated with Siemens AG and is neither endorsed nor reviewed by it.
SIMATIC, STEP 7, TIA Portal and WinCC are trademarks of Siemens AG. The package is provided without warranty.
