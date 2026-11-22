using System.Text.Json.Nodes;

namespace OpenPEData.Tests;

/// <summary>Start screen of the Unified runtime settings (samples 114 and 169) against Openness RuntimeSettings.StartScreen.</summary>
public static class HmiStartScreenTests
{
    [Test]
    public static void StartScreenAgainstOpenness()
    {
        var c = new Check("114/169 start screen = Openness");
        var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", "169_hmi_start_screen_truth", "runtimeSetting.json")))!;
        foreach (var part in new[] { "before", "after" })
        {
            var sample = truth[part]!["sample"]!.GetValue<string>();
            using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", sample + ".zap21"));
            string? got = null;
            void Walk(JsonNode? n)
            {
                if (n is JsonObject o)
                {
                    if (o["runtimeSettings"] is JsonObject rs && rs["startScreen"] is JsonValue v)
                        got = v.GetValue<string>();
                    foreach (var kv in o)
                        Walk(kv.Value);
                }
                else if (n is JsonArray a)
                    foreach (var x in a)
                        Walk(x);
            }
            Walk(p.GetStructure());
            c.Eq(truth[part]!["StartScreen"]!.GetValue<string>(), got, sample + " startScreen");
        }
        c.Done();
    }
}
