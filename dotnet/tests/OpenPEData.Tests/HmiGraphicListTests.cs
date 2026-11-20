using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenPEData.Tests;

/// <summary>Graphic list entries (sample 168) against the Openness YAML export (HmiGraphicLists.hmi.yml).</summary>
public static class HmiGraphicListTests
{
    /// <summary>Per list: entries in value order with their graphic ("GraphicLibrary.&lt;name&gt;" in the export) and value
    /// range (FromValue/ToValue, 0 when the export leaves them out).</summary>
    [Test]
    public static void GraphicListEntriesAgainstExport()
    {
        var c = new Check("168 graphic list entries = Openness YAML export");
        const string sample = "168_hmi_graphiclist_entry";
        var yml = File.ReadAllLines(Path.Combine(Program.Root, "samples", sample + "_truth", "HMI_1", "HmiGraphicLists.hmi.yml"));
        var want = new Dictionary<string, List<(long From, long To, string? Graphic)>>();
        string? list = null;
        (long From, long To, string? Graphic)? cur = null;
        void Flush()
        {
            if (list is not null && cur is { } e)
                want[list].Add(e);
            cur = null;
        }
        foreach (var line in yml)
        {
            var m = Regex.Match(line, @"^      (\w+):\s*$");
            if (m.Success && !line.Contains("Entries"))
            {
                Flush();
                list = m.Groups[1].Value;
                want[list] = [];
                continue;
            }
            if (Regex.IsMatch(line, @"^          \w+:"))
            {
                Flush();
                cur = (0, 0, null);
                continue;
            }
            if (cur is { } e2 && Regex.Match(line, @"^            (\w+): (.*)$") is { Success: true } kv)
            {
                var v = kv.Groups[2].Value.Trim();
                cur = kv.Groups[1].Value switch
                {
                    "FromValue" => (long.Parse(v), e2.To, e2.Graphic),
                    "ToValue" => (e2.From, long.Parse(v), e2.Graphic),
                    "Graphic" => (e2.From, e2.To, v.StartsWith("GraphicLibrary.", StringComparison.Ordinal) ? v["GraphicLibrary.".Length..] : v),
                    _ => e2,
                };
            }
        }
        Flush();
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", sample + ".zap21"));
        var got = new Dictionary<string, JsonArray>();
        void Walk(JsonNode? n)
        {
            if (n is JsonObject o)
            {
                if (o["graphicLists"] is JsonArray gl)
                    foreach (var l in gl)
                        got[l!["name"]!.GetValue<string>()] = l["entries"]!.AsArray();
                foreach (var kv in o)
                    Walk(kv.Value);
            }
            else if (n is JsonArray a)
                foreach (var x in a)
                    Walk(x);
        }
        Walk(p.GetStructure());
        foreach (var (name, entries) in want)
        {
            c.True(got.ContainsKey(name), $"{name} present");
            if (!got.TryGetValue(name, out var ge))
                continue;
            c.Eq(entries.Count, ge.Count, $"{name} entry count");
            for (var i = 0; i < Math.Min(entries.Count, ge.Count); i++)
            {
                c.Eq(entries[i].From, ge[i]!["from"]!.GetValue<long>(), $"{name}/{i} from");
                c.Eq(entries[i].To, ge[i]!["to"]!.GetValue<long>(), $"{name}/{i} to");
                c.Eq(entries[i].Graphic, ge[i]!["graphic"]?.GetValue<string>(), $"{name}/{i} graphic");
            }
        }
        c.Done();
    }
}
