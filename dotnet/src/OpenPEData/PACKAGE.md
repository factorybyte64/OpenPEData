# OpenPEData

Reads TIA Portal V21 projects (project folder, `.apNN`, `.zapNN`) and global libraries (library folder, `.zalNN`)
without TIA Portal. No dependencies.

```csharp
using OpenPEData;

using var project = TiaProject.Open(@"C:\Projects\Plant.zap21");
TiaProjectContent content = project.GetContent();

foreach (PlcSoftware plc in content.Plcs)
    foreach (PlcBlock block in plc.Blocks)
        Console.WriteLine($"{plc.Name}: {block} {block.Language}");

IntegrityReport report = project.GetIntegrityReport();
string json = project.ToJson(TiaContent.Structure);
project.ExportSources(@"C:\Export");   // blocks, PLC data types and tag tables as source files
TiaCommunicationInfo comm = project.GetCommunication();   // per PLC: interfaces, access, OPC UA, symbols
```

Every typed object also has its JSON node (`Json`) with all details. Errors derive from `OpenPEDataException`.

## License

Mozilla Public License 2.0. Not affiliated with Siemens AG. SIMATIC, STEP 7, TIA Portal and WinCC are trademarks of
Siemens AG. Provided without warranty.
