using System.Text.Json.Nodes;
using OpenPEData.Runtime;

namespace OpenPEData.Tests;

/// <summary>Static numeric ProcessValue of bar and gauge (sample 172) against Openness itemSet: stored inline with type
/// code 5 (Int32 in the offset word), together with the segmented bar modes.</summary>
public static class HmiStaticProcessValueTests
{
    [Test]
    public static void StaticProcessValueAgainstOpenness()
    {
        var c = new Check("172 static ProcessValue = Openness");
        var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", "172_hmi_bar_segmented_truth", "items.json")))!;
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", "172_hmi_bar_segmented.zap21"));
        var proj = (PyDict)((PyDict)p.Raw(TiaContent.Structure, default)!)["project"]!;
        var items = new Dictionary<string, PyDict>();
        foreach (var d in ((List<object?>)proj["devices"]!).Cast<PyDict>())
            foreach (var h in ((d.Get("hmi") as List<object?>) ?? []).Cast<PyDict>())
                foreach (var s in ((h.Get("screens") as List<object?>) ?? []).Cast<PyDict>())
                    foreach (var i in ((s.Get("items") as List<object?>) ?? []).Cast<PyDict>())
                        items[(string)i["name"]!] = i;
        foreach (var t in truth["items"]!.AsArray())
        {
            var name = t!["name"]!.GetValue<string>();
            var props = items.TryGetValue(name, out var it) ? it.Get("properties") as PyDict : null;
            c.True(props is not null, name);
            c.Eq((long)t["ProcessValue"]!.GetValue<int>(), props?.Get("ProcessValue"), name + " ProcessValue");
            c.Eq(t["BarMode"]!.GetValue<string>(), props?.Get("BarMode"), name + " BarMode");
        }
        c.Done();
    }
}
