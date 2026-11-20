using System.Text.Json.Nodes;
using OpenPEData.Runtime;

namespace OpenPEData.Tests;

/// <summary>Plant object with a plant object type (sample 170) against Openness TiaHmiUnified(list/get plantViewNode): node
/// paths, plant object type and interface tag; the hidden PlantModel device is marked (Openness lists only PLC_1 and HMI_1).</summary>
public static class HmiPlantObjectTypeTests
{
    [Test]
    public static void PlantObjectTypeAgainstOpenness()
    {
        var c = new Check("170 plant object type = Openness");
        var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", "170_hmi_plant_object_type_truth", "plantView.json")))!;
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", "170_hmi_plant_object_type.zap21"));
        var proj = (PyDict)((PyDict)p.Raw(TiaContent.Structure, default)!)["project"]!;
        var pv = ((List<object?>)proj["plantViews"]!).Cast<PyDict>().FirstOrDefault(x => (string?)x["name"] == truth["plantView"]!.GetValue<string>());
        c.True(pv is not null, "plant view");
        var nodes = ((pv?["nodes"] as List<object?>) ?? []).Cast<PyDict>().ToDictionary(n => (string)n["path"]!);
        foreach (var t in truth["nodes"]!.AsArray())
        {
            var path = t!["path"]!.GetValue<string>();
            if (!nodes.TryGetValue(path, out var n))
            {
                c.Fail(path + ": missing");
                continue;
            }
            c.Eq(t["plantObjectType"]?.GetValue<string>(), n.Get("plantObjectType"), path + " plantObjectType");
            if (t["interfaceTags"] is JsonArray tags)
                c.Eq(tags[0]!["Name"]!.GetValue<string>(), n.Get("interfaceTag"), path + " interface tag");
        }
        var types = ((proj.Get("plantObjectTypes") as List<object?>) ?? []).Cast<PyDict>().ToList();
        c.Eq(1, types.Count, "plant object types");
        c.Eq("Mixer", types.FirstOrDefault()?["name"], "type name");
        var visible = ((List<object?>)proj["devices"]!).Cast<PyDict>().Where(d => d.Get("hiddenModuleDevice") is not true).Select(d => (string)d["name"]!).Order().ToList();
        c.Eq("HMI_1,PLC_1", string.Join(",", visible), "devices visible in Openness");
        c.Done();
    }
}
