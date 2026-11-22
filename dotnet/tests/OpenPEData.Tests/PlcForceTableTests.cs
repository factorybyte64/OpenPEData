using System.Text.Json.Nodes;

namespace OpenPEData.Tests;

/// <summary>Force table entries (sample 078) against the Openness entry (address, format, force value, intention).
/// The monitor trigger is not compared: Openness reports Permanent for a force table, the plf keeps Smart.</summary>
public static class PlcForceTableTests
{
    [Test]
    public static void ForceEntriesAgainstOpenness()
    {
        var c = new Check("078 force table entries = Openness");
        const string sample = "078_plc_force_entry";
        var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", sample + "_truth", "forceTable.json")))!;
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", sample + ".zap21"));
        var table = p.GetStructure()["project"]!["plcWatchTables"]!.AsArray()
            .FirstOrDefault(t => t!["kind"]!.GetValue<string>() == "force" && t["name"]!.GetValue<string>() == truth["table"]!.GetValue<string>());
        c.True(table is not null, "force table present");
        var got = table?["entries"]!.AsArray().Where(e => e!["isComment"]!.GetValue<bool>() == false).ToList() ?? [];
        var want = truth["entries"]!.AsArray();
        c.Eq(want.Count, got.Count, "entry count");
        for (var i = 0; i < Math.Min(want.Count, got.Count); i++)
        {
            c.Eq(want[i]!["address"]!.GetValue<string>(), got[i]!["address"]?.GetValue<string>(), $"{i} address");
            c.Eq(want[i]!["displayFormat"]!.GetValue<string>(), got[i]!["displayFormat"]?.GetValue<string>(), $"{i} format");
            c.Eq(want[i]!["forceValue"]!.GetValue<string>(), got[i]!["forceValue"]?.GetValue<string>(), $"{i} force value");
            c.Eq(want[i]!["forceIntention"]!.GetValue<bool>(), got[i]!["forceIntention"]?.GetValue<bool>(), $"{i} force intention");
        }
        c.Done();
    }
}
