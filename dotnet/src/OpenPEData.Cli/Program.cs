using System.Text;
using OpenPEData;

namespace OpenPEData.Cli;

/// <summary>OpenPEData command line.</summary>
public static class Program
{
    const string Usage = """
        OpenPEData - reads a TIA Portal project (V19, V20, V21) without TIA Portal.

        Usage: OpenPEData <project folder | .ap21 | .zap21> [--json] [--keys] [--verify] [--xref] [--proginfo] [--simaticml] [--sources=<folder>] [--comm]
          --json     JSON output (default: indented text tree)
          --keys     include record keys (typeLo:typeHi:oid) in the JSON output
          --verify   integrity check only (header, record hashes, hash chain and RSA signatures of the commits); exit code 1 if anything fails
          --xref     cross references from XRef/XRef.db with object names (always JSON; null without XRef.db)
          --proginfo program information per PLC: resources, call structure, dependency structure, assignment list (always JSON)
          --simaticml LAD/FBD networks of every code block as SimaticML <FlgNet> of the Openness export (always JSON:
                     block name -> one XML text per network, null for networks in other languages)
          --sources=<folder> write every block and PLC data type as a source file into the folder, as TIA "Generate source
                     from blocks": .scl, .awl, .db, .udt; LAD/FBD/GRAPH as SimaticML .xml. Prints one line per file
          --comm     communication data of every PLC: network interfaces, access settings, OPC UA server and
                     interfaces, symbols (PLC tags, data block members) (always JSON)

        """;

    /// <summary>print() of Python on Windows: UTF-8, "\n" written as "\r\n".</summary>
    static void Print(Stream s, string text)
    {
        var b = new UTF8Encoding(false).GetBytes((text + "\n").Replace("\n", Environment.NewLine));
        s.Write(b);
        s.Flush();
    }

    public static int Main(string[] argv)
    {
        var args = argv.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        using var stdout = Console.OpenStandardOutput();
        using var stderr = Console.OpenStandardError();
        if (args.Count != 1)
        {
            Print(stderr, Usage);
            return 2;
        }
        var content = argv.Contains("--verify") ? TiaContent.Integrity
            : argv.Contains("--xref") ? TiaContent.CrossReferences
            : argv.Contains("--proginfo") ? TiaContent.ProgramInfo
            : argv.Contains("--keys") ? TiaContent.StructureWithKeys : TiaContent.Structure;
        TiaProject p;
        try
        {
            // a project of another or unknown version is read as far as it goes
            p = TiaProject.Open(args[0], new OpenOptions { AllowUnknownVersion = true });
        }
        catch (OpenPEDataException e)
        {
            Print(stderr, "error: " + e.Message);
            return 1;
        }
        using (p)
        {
            foreach (var w in p.Warnings)
                Print(stderr, "warning: " + w);
            if (argv.Contains("--comm"))
            {
                Print(stdout, p.GetCommunication().ToJsonString());
                return 0;
            }
            if (argv.FirstOrDefault(a => a.StartsWith("--sources=", StringComparison.Ordinal)) is { } so)
            {
                var dir = so["--sources=".Length..];
                if (dir.Length == 0)
                {
                    Print(stderr, Usage);
                    return 2;
                }
                var r = p.ExportSources(dir);
                for (var i = 0; i < r.Sources.Count; i++)
                {
                    Print(stdout, r.Paths[i]);
                    foreach (var n in r.Sources[i].Notes)
                        Print(stderr, $"note: {r.Sources[i].RelativePath}: {n}");
                }
                foreach (var k in r.Skipped)
                    Print(stderr, $"skipped: {k.Plc}/{k.Folder}/{k.Name}: {k.Reason}");
                Print(stderr, $"{r.Sources.Count} source files in {r.Folder}, {r.Skipped.Count} blocks skipped");
                return 0;
            }
            if (argv.Contains("--simaticml"))
            {
                var o = new System.Text.Json.Nodes.JsonObject();
                foreach (var (block, nets) in p.GetLadFbdSimaticMl().OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    o[block] = new System.Text.Json.Nodes.JsonArray(nets.Select(n => (System.Text.Json.Nodes.JsonNode?)n).ToArray());
                Print(stdout, o.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                return 0;
            }
            Print(stdout, argv.Contains("--json") ? p.ToJson(content) : p.ToText(content));
            if (content == TiaContent.Integrity)
                return p.IsIntact() ? 0 : 1;
            if (content is TiaContent.Structure or TiaContent.StructureWithKeys)
                foreach (var e in p.GetIssues())
                    Print(stderr, $"not readable: {e.Object}: {e.Message}");
            return 0;
        }
    }
}
