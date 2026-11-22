using System.Text.Json.Nodes;
using OpenPEData.Runtime;

namespace OpenPEData.Tests;

/// <summary>Mapping tables of tag dynamizations (F01, F03) against the Openness screen exports (mappingEntries):
/// Range entries with from/to and Singlebit entries whose Relevant/Condition bit masks are stored as u64 inline
/// (nested type code 0x14) and are output as integers.</summary>
public static class HmiMappingTests
{
    static readonly (string Sample, string Truth)[] Inputs =
    [
        ("F01_hmi_allobjects.zap21", Path.Combine("F01_hmi_allobjects_truth", "HMI_1", "screen_FP_Interface.full.json")),
        ("F03_hmi_faceplates_user", Path.Combine("F03_hmi_faceplates_user_truth", "screen_FP_Interface.full.json")),
    ];

    [Test]
    public static void MappingEntriesAgainstOpenness()
    {
        var c = new Check("F01/F03 dynamization mapping entries = Openness");
        foreach (var (sample, truthFile) in Inputs)
        {
            var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", truthFile)))!;
            using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", sample));
            var items = new Dictionary<string, List<PyDict>>();
            Collect(p.Raw(TiaContent.Structure, default), items);
            var compared = 0;
            foreach (var screen in truth["objects"]!.AsArray())
                foreach (var ti in (screen!["items"] as JsonArray) ?? [])
                    foreach (var td in (ti!["dynamizations"] as JsonArray) ?? [])
                    {
                        if (td!["mappingEntries"] is not JsonArray exp)
                            continue;
                        var name = ti["name"]!.GetValue<string>();
                        var prop = td["PropertyName"]!.GetValue<string>();
                        var what = $"{sample} {name} {prop}";
                        var dyn = items.GetValueOrDefault(name, [])
                            .SelectMany(i => ((i.Get("dynamizations") as List<object?>) ?? []).Cast<PyDict>())
                            .FirstOrDefault(d => (string?)d.Get("property") == prop && (string?)d.Get("type") == "Tag");
                        c.True(dyn is not null, what + " dynamization");
                        var got = ((dyn?.Get("mapping") as PyDict)?.Get("entries") as List<object?>) ?? [];
                        c.Eq((long)exp.Count, (long)got.Count, what + " entry count");
                        for (var j = 0; j < Math.Min(exp.Count, got.Count); j++)
                            foreach (var (key, value) in exp[j]!.AsObject())
                                c.Eq(Value(value), ((PyDict)got[j]!).Get(key), $"{what} entry {j} {key}");
                        compared++;
                    }
            c.True(compared > 0, sample + " has mapping entries in the truth");
        }
        c.Done();
    }

    static object? Value(JsonNode? v) => v switch
    {
        null => null,
        JsonValue jv when jv.TryGetValue<long>(out var l) => l,
        JsonValue jv => jv.GetValue<string>(),
        _ => v.ToJsonString(),
    };

    // screen items: dictionaries with a name and a dynamizations list (in HMI screens and faceplate type versions)
    static void Collect(object? o, Dictionary<string, List<PyDict>> items)
    {
        switch (o)
        {
            case PyDict d:
                if (d.Get("name") is string n && d.Get("dynamizations") is List<object?>)
                {
                    if (!items.TryGetValue(n, out var l))
                        items[n] = l = [];
                    l.Add(d);
                }
                foreach (var v in d.Values)
                    Collect(v, items);
                break;
            case List<object?> list:
                foreach (var v in list)
                    Collect(v, items);
                break;
        }
    }
}
