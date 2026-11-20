using System.Text;
using OpenPEData;

namespace OpenPEData.Cli;

/// <summary>OpenPEData command line, same arguments and output as parser\OpenPEData.py.</summary>
public static class Program
{
    const string Usage = """
        OpenPEData - reads a TIA Portal V21 project without TIA Portal.

        Usage: OpenPEData <project folder | .ap21 | .zap21> [--json] [--keys] [--verify] [--xref] [--proginfo] [--simaticml]
          --json     JSON output (default: indented text tree)
          --keys     include record keys (typeLo:typeHi:oid) in the JSON output
          --verify   integrity check only (header, record hashes, hash chain and RSA signatures of the commits); exit code 1 if anything fails
          --xref     cross references from XRef/XRef.db with object names (always JSON; null without XRef.db)
          --proginfo program information per PLC: resources, call structure, dependency structure, assignment list (always JSON)
          --simaticml LAD/FBD networks of every code block as SimaticML <FlgNet> of the Openness export (always JSON:
                     block name -> one XML text per network, null for networks in other languages)

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
            // like the Python parser: a project of another or unknown version is read as far as it goes
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
