using System.Text.Json.Nodes;

namespace OpenPEData.Tests;

/// <summary>Channel parameters stored as sparse arrays (samples 254/255) against Openness channelGet.</summary>
public static class HardwareChannelTests
{
    static readonly Dictionary<string, string> ArrayOf = new()
    {
        ["HardwareInterruptRisingEdgeActive"] = "EnableRisingEdgeArray",
        ["HardwareInterruptFallingEdgeActive"] = "EnableFallingEdgeArray",
    };

    /// <summary>The channels with the attribute true (truth channels.json) are exactly the entries of the sparse array,
    /// all with value 1; the array length is the channel count.</summary>
    [Test]
    public static void ChannelArraysAgainstOpenness()
    {
        var c = new Check("254/255 channel parameters = Openness channelGet");
        foreach (var sample in new[] { "254_hw_channel_param", "255_hw_channel_param2" })
        {
            var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", sample + "_truth", "channels.json")))!;
            using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", sample + ".zap21"));
            JsonObject? found = null;
            void Walk(JsonNode? n)
            {
                if (n is JsonObject o)
                {
                    if (o["EnableRisingEdgeArray"] is not null && found is null)
                        found = o;
                    foreach (var kv in o)
                        Walk(kv.Value);
                }
                else if (n is JsonArray a)
                    foreach (var x in a)
                        Walk(x);
            }
            Walk(p.GetStructure());
            c.True(found is not null, sample + " parameters with EnableRisingEdgeArray");
            if (found is null)
                continue;
            foreach (var (attr, arr) in ArrayOf)
            {
                var want = truth["channels"]![attr]!.AsArray().Select(x => x!.GetValue<int>().ToString()).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var got = found[arr] as JsonObject;
                var entries = got?["entries"] as JsonObject;
                var keys = entries?.Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToList() ?? [];
                c.Eq(string.Join(",", want), string.Join(",", keys), $"{sample} {arr} channels");
                c.True(entries?.All(kv => kv.Value!.GetValue<long>() == 1) ?? want.Count == 0, $"{sample} {arr} values 1");
                if (got is not null)
                    c.Eq(truth["channelCount"]!.GetValue<int>(), got["arrayLength"]!.GetValue<int>(), $"{sample} {arr} length");
            }
        }
        c.Done();
    }
}
