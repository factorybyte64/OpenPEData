using System.Text.Json.Nodes;

namespace OpenPEData.Tests;

/// <summary>Hardware connection of a technology object (sample 079) against Openness hardwareConnectionGet: the connected
/// interface and its output tag.</summary>
public static class PlcToConnectionTests
{
    [Test]
    public static void ToHardwareConnectionAgainstOpenness()
    {
        var c = new Check("079 TO hardware connection = Openness");
        var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", "079_plc_to_hw_connect_truth", "hardwareConnection.json")))!;
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", "079_plc_to_hw_connect.zap21"));
        JsonObject? blk = null;
        void Walk(JsonNode? n)
        {
            if (n is JsonObject o)
            {
                if (o["hardwareConnections"] is not null && o["name"]?.ToString() == truth["to"]!.GetValue<string>())
                    blk = o;
                foreach (var kv in o)
                    Walk(kv.Value);
            }
            else if (n is JsonArray a)
                foreach (var x in a)
                    Walk(x);
        }
        Walk(p.GetStructure());
        c.True(blk is not null, "TO DB with hardwareConnections");
        foreach (var t in truth["connections"]!.AsArray())
        {
            var got = blk?["hardwareConnections"]!.AsArray().FirstOrDefault(x =>
                string.Equals(x!["interface"]!.GetValue<string>(), t!["interface"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase));
            c.True(got is not null, $"{t!["interface"]} connected");
            c.Eq(t!["outputTag"]!.GetValue<string>(), got?["operands"]?[0]?.GetValue<string>(), $"{t["interface"]} output tag");
        }
        c.Done();
    }
}
