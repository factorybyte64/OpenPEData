using System.Text.Json.Nodes;

namespace OpenPEData.Tests;

/// <summary>HMI library types (script module type, dynamic SVG type; sample L37) against Openness typesList.</summary>
public static class LibraryHmiTypeTests
{
    [Test]
    public static void HmiTypesAgainstOpenness()
    {
        var c = new Check("L37 HMI library types = Openness typesList");
        const string sample = "L37_hmi_lib_types";
        var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", sample + "_truth", "typesList.json")))!;
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", sample + ".zap21"));
        var types = p.GetStructure()["library"]!["libraries"]![0]!["types"]!.AsArray();
        foreach (var t in truth["items"]!.AsArray())
        {
            var name = t!["name"]!.GetValue<string>();
            var g = types.FirstOrDefault(x => x!["name"]!.GetValue<string>() == name);
            c.True(g is not null, name + " present");
            if (g is null)
                continue;
            c.Eq(t["guid"]!.GetValue<string>(), g["typeGuid"]?.GetValue<string>(), name + " guid");
            foreach (var v in t["versions"]!.AsArray())
            {
                var gv = g["versions"]!.AsArray().FirstOrDefault(x => x!["version"]!.GetValue<string>() == v!["version"]!.GetValue<string>());
                c.True(gv is not null, $"{name} {v!["version"]} present");
                if (gv is null)
                    continue;
                c.Eq(v!["state"]!.GetValue<string>(), gv["state"]?.GetValue<string>(), $"{name} state");
                c.Eq(v["isDefault"]!.GetValue<bool>(), gv["isDefault"]?.GetValue<bool>(), $"{name} default");
            }
        }
        c.Done();
    }

    /// <summary>Content of HMI type versions (sample L38 = L37 plus code in the script module type): the global definition area
    /// of the script module type as typed in TIA, the dynamic SVG file byte for byte = the default graphic of TIA.</summary>
    [Test]
    public static void HmiTypeVersionContent()
    {
        var c = new Check("L38 HMI type version content = TIA");
        const string sample = "L38_hmi_lib_type_content";
        var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", sample + "_truth", "content.json")))!;
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", sample + ".zap21"));
        var types = p.GetStructure()["library"]!["libraries"]![0]!["types"]!.AsArray();
        JsonNode? Version(string type) => types.FirstOrDefault(x => x!["name"]!.GetValue<string>() == type)?["versions"]?.AsArray().FirstOrDefault();
        var sm = truth["scriptModuleType"]!;
        var code = Version(sm["type"]!.GetValue<string>())?["scriptModule"]?["code"]?.GetValue<string>();
        c.True(code?.StartsWith(sm["globalDefinition"]!.GetValue<string>(), StringComparison.Ordinal) == true, "script module type: global definition area");
        var svg = truth["dynamicSvgType"]!;
        var ds = Version(svg["type"]!.GetValue<string>())?["dynamicSvg"];
        c.Eq(svg["file"]!.GetValue<string>(), ds?["file"]?.GetValue<string>(), "dynamic SVG file");
        c.Eq(svg["bytes"]!.GetValue<long>(), ds?["bytes"]?.GetValue<long>(), "dynamic SVG bytes");
        c.Eq(svg["sha256"]!.GetValue<string>(), ds?["sha256"]?.GetValue<string>(), "dynamic SVG sha256");
        c.Done();
    }

    /// <summary>Text list type (sample L39 = L38 plus a text list type with one entry, typed in TIA): type against Openness
    /// typesList, content (list name, range, text) against what was typed.</summary>
    [Test]
    public static void TextListTypeContent()
    {
        var c = new Check("L39 text list type = Openness / TIA");
        const string sample = "L39_hmi_lib_textlist_type";
        var dir = Path.Combine(Program.Root, "samples", sample + "_truth");
        var list = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "textlist.json")))!;
        var tl = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "typesList.json")))!["items"]!.AsArray()
            .First(x => x!["name"]!.GetValue<string>() == list["type"]!.GetValue<string>())!;
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", sample + ".zap21"));
        var t = p.GetStructure()["library"]!["libraries"]![0]!["types"]!.AsArray().FirstOrDefault(x => x!["name"]!.GetValue<string>() == list["type"]!.GetValue<string>());
        c.Eq(tl["guid"]!.GetValue<string>(), t?["typeGuid"]?.GetValue<string>(), "type guid");
        var v = t?["versions"]?.AsArray().FirstOrDefault();
        c.Eq(tl["versions"]![0]!["state"]!.GetValue<string>(), v?["state"]?.GetValue<string>(), "state");
        var got = v?["textList"];
        c.Eq(list["list"]!.GetValue<string>(), got?["name"]?.GetValue<string>(), "list name");
        var want = list["entries"]!.AsArray();
        var entries = got?["entries"]?.AsArray();
        c.Eq(want.Count, entries?.Count, "entries");
        for (var i = 0; i < want.Count && entries is not null && i < entries.Count; i++)
        {
            c.Eq(want[i]!["from"]!.GetValue<long>(), entries[i]!["from"]?.GetValue<long>(), $"entry {i} from");
            c.Eq(want[i]!["to"]!.GetValue<long>(), entries[i]!["to"]?.GetValue<long>(), $"entry {i} to");
            c.Eq(want[i]!["text"]!["en-US"]!.GetValue<string>(), entries[i]!["text"]?["en-US"]?.GetValue<string>(), $"entry {i} text");
        }
        c.Done();
    }

    /// <summary>Graphic list type (sample L40 = L39 plus a graphic type and a graphic list type whose entry shows the graphic
    /// type): types against Openness typesList, entry range and graphic type against what was set in TIA.</summary>
    [Test]
    public static void GraphicListTypeContent()
    {
        var c = new Check("L40 graphic list type = Openness / TIA");
        const string sample = "L40_hmi_lib_graphiclist_type";
        var dir = Path.Combine(Program.Root, "samples", sample + "_truth");
        var gl = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "graphiclist.json")))!;
        var list = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "typesList.json")))!["items"]!.AsArray();
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", sample + ".zap21"));
        var types = p.GetStructure()["library"]!["libraries"]![0]!["types"]!.AsArray();
        foreach (var t in list)
        {
            var name = t!["name"]!.GetValue<string>();
            var g = types.FirstOrDefault(x => x!["name"]!.GetValue<string>() == name);
            c.Eq(t["guid"]!.GetValue<string>(), g?["typeGuid"]?.GetValue<string>(), name + " guid");
            c.Eq(t["versions"]![0]!["state"]!.GetValue<string>(), g?["versions"]?[0]?["state"]?.GetValue<string>(), name + " state");
        }
        var img = types.FirstOrDefault(x => x!["name"]!.GetValue<string>() == gl["graphicType"]!["type"]!.GetValue<string>())?["versions"]?[0]?["image"];
        c.True(img?["bytes"]?.GetValue<long>() > 0, "graphic type has an image");
        var got = types.FirstOrDefault(x => x!["name"]!.GetValue<string>() == gl["type"]!.GetValue<string>())?["versions"]?[0]?["graphicList"]?["entries"]?.AsArray();
        var want = gl["entries"]!.AsArray();
        c.Eq(want.Count, got?.Count, "entries");
        for (var i = 0; i < want.Count && got is not null && i < got.Count; i++)
        {
            c.Eq(want[i]!["from"]!.GetValue<long>(), got[i]!["from"]?.GetValue<long>(), $"entry {i} from");
            c.Eq(want[i]!["to"]!.GetValue<long>(), got[i]!["to"]?.GetValue<long>(), $"entry {i} to");
            c.Eq(want[i]!["graphicType"]!.GetValue<string>(), got[i]!["graphicType"]?.GetValue<string>(), $"entry {i} graphic type");
        }
        c.Done();
    }
}
