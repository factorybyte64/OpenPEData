using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;

namespace OpenPEData.Viewer;

/// <summary>Detail tabs of the node "Program info" of a PLC (parser --proginfo): call structure, dependency
/// structure, assignment list and resources as in TIA Portal.</summary>
public static class ProgramInfoViews
{
    public static List<(string Header, Func<UIElement> Create)> Tabs(JsonObject plc)
    {
        var calls = CallRows(TreeBuilder.Arr(plc["callStructure"]), 0, []).ToList();
        var deps = DependencyRows(TreeBuilder.Arr(plc["dependencyStructure"])).ToList();
        var assign = AssignmentRows(TreeBuilder.Arr(plc["assignmentList"])).ToList();
        var res = ResourceRows(plc["resources"] as JsonObject).ToList();
        var blocks = TreeBuilder.Arr(plc["resources"]?["blocks"]).Select(BlockRow).ToList();
        return
        [
            ($"Call structure ({TreeBuilder.Arr(plc["callStructure"]).Count()})", () => DetailViews.Grid(DetailViews.ToTable(calls), true)),
            ($"Dependency structure ({deps.Count(r => r["Level"] == "0")})", () => DetailViews.Grid(DetailViews.ToTable(deps), true)),
            ($"Assignment list ({assign.Count})", () => DetailViews.Grid(DetailViews.ToTable(assign), true)),
            ("Resources", () => DetailViews.Grid(DetailViews.ToTable(res), true)),
            ($"Resources per block ({blocks.Count})", () => DetailViews.Grid(DetailViews.ToTable(blocks), true)),
        ];
    }

    private static string? S(JsonNode? n) => n is JsonValue v ? Convert.ToString(v.GetValue<object>(), CultureInfo.InvariantCulture) : null;

    /// <summary>Tree rows with indentation; a call path is not followed again inside itself.</summary>
    private static IEnumerable<Dictionary<string, string?>> CallRows(IEnumerable<JsonNode> nodes, int depth, HashSet<string> path)
    {
        foreach (var n in nodes)
        {
            var name = TreeBuilder.Str(n["name"]) ?? "?";
            var type = TreeBuilder.Str(n["type"]);
            yield return new Dictionary<string, string?>
            {
                ["Call structure"] = new string(' ', depth) + name + (type == "DB" && depth == 0 ? " (DB)" : ""),
                ["Address"] = TreeBuilder.Str(n["address"]),
                ["Details"] = n["recursive"]?.GetValue<bool>() == true ? "recursive" : null,
                ["Local data (in path)"] = S(n["localDataInPath"]),
                ["Local data (for blocks)"] = S(n["localData"]),
            };
            if (path.Add(name))
            {
                foreach (var r in CallRows(TreeBuilder.Arr(n["children"]), depth + 1, path)) yield return r;
                path.Remove(name);
            }
        }
    }

    private static IEnumerable<Dictionary<string, string?>> DependencyRows(IEnumerable<JsonNode> items)
    {
        foreach (var d in items)
        {
            yield return new Dictionary<string, string?>
            {
                ["Dependency structure"] = TreeBuilder.Str(d["name"]),
                ["Address"] = TreeBuilder.Str(d["address"]),
                ["Kind"] = TreeBuilder.Str(d["type"]),
                ["Level"] = "0",
            };
            foreach (var u in TreeBuilder.Arr(d["usedBy"]))
                yield return new Dictionary<string, string?>
                {
                    ["Dependency structure"] = " " + TreeBuilder.Str(u["name"]),
                    ["Address"] = TreeBuilder.Str(u["address"]),
                    ["Kind"] = TreeBuilder.Str(u["type"]),
                    ["Level"] = "1",
                };
        }
    }

    private static readonly Dictionary<string, string> AreaPrefix = new() { ["Input"] = "IB", ["Output"] = "QB", ["Memory"] = "MB" };

    private static IEnumerable<Dictionary<string, string?>> AssignmentRows(IEnumerable<JsonNode> items) =>
        items.Select(a =>
        {
            var area = TreeBuilder.Str(a["area"]) ?? "";
            return new Dictionary<string, string?>
            {
                ["Address"] = AreaPrefix.GetValueOrDefault(area, area) + S(a["byte"]),
                ["Bit"] = S(a["bit"]),
                ["Width"] = a["bit"] is JsonValue ? null : TreeBuilder.Str(a["size"]),
                ["Tag"] = TreeBuilder.Str(a["tag"]),
                ["Area"] = area,
            };
        });

    private static readonly Dictionary<string, string> RowNames = new()
    {
        ["DataTypes"] = "Data types", ["PlcTags"] = "PLC tags",
    };

    /// <summary>TIA layout: "Used" row, then one row per object kind; ">" marks rows with a block without value, "?"
    /// rows without any value.</summary>
    private static IEnumerable<Dictionary<string, string?>> ResourceRows(JsonObject? res)
    {
        if (res == null) yield break;
        string? Cell(JsonObject r, string key)
        {
            var v = S(r[key]);
            if (v == null) return null;
            if (r["noValue"]?.GetValue<bool>() == true) return "?";
            // TIA marks load and work memory with ">", not the retain memory
            return (r["incomplete"]?.GetValue<bool>() == true && key != "retainMemory" ? ">" : "") + v + " Byte";
        }
        var used = res["used"] as JsonObject;
        yield return new Dictionary<string, string?>
        {
            ["Objects"] = "Used",
            ["Load memory"] = S(used?["loadMemory"]) + " Byte",
            ["Code work memory"] = S(used?["codeWorkMemory"]) + " Byte",
            ["Data work memory"] = S(used?["dataWorkMemory"]) + " Byte",
            ["Retain memory"] = S(used?["retainMemory"]) + " Byte",
        };
        foreach (var r in TreeBuilder.Arr(res["rows"]).OfType<JsonObject>())
        {
            var name = TreeBuilder.Str(r["objects"]) ?? "?";
            yield return new Dictionary<string, string?>
            {
                ["Objects"] = RowNames.GetValueOrDefault(name, name),
                ["Load memory"] = Cell(r, "loadMemory"),
                ["Code work memory"] = Cell(r, "codeWorkMemory"),
                ["Data work memory"] = Cell(r, "dataWorkMemory"),
                ["Retain memory"] = Cell(r, "retainMemory"),
            };
        }
    }

    private static Dictionary<string, string?> BlockRow(JsonNode b) => new()
    {
        ["Name"] = TreeBuilder.Str(b["name"]),
        ["Kind"] = TreeBuilder.Str(b["type"]),
        ["Number"] = S(b["number"]),
        ["System"] = b["system"]?.GetValue<bool>() == true ? "yes" : null,
        ["Load memory"] = S(b["loadMemory"]),
        ["Code work memory"] = S(b["codeWorkMemory"]),
        ["counted"] = b["codeWorkMemoryCounted"]?.GetValue<bool>() == false ? "no (NotCounted)" : null,
        ["Data work memory"] = S(b["dataWorkMemory"]),
        ["Retain memory"] = S(b["retainMemory"]),
        ["Local data"] = S(b["localData"]),
    };
}
