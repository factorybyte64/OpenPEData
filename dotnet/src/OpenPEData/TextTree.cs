using OpenPEData.Model;
using OpenPEData.Runtime;

namespace OpenPEData;

/// <summary>Indented text tree of a project structure (text_tree of parser\OpenPEData.py).</summary>
internal static class TextTree
{
    static List<PyDict> Dicts(object? l) => ((List<object?>?)l ?? []).Cast<PyDict>().ToList();

    static string KV(object? d) => string.Join("; ", ((PyDict)d!).Select(kv => Py.Str(kv.Key) + ": " + Py.Str(kv.Value)));

    static string NeedStr(object? s) => s as string ?? throw new PyTypeError("sequence item: expected str instance, " + Py.TypeName(s) + " found");

    public static List<string> MemberLines(object? members, int indent)
    {
        var o = new List<string>();
        foreach (var m in Dicts(members))
        {
            var start = m["startValue"];
            if (start is null && m.Get("defaultValue") is not null)
                start = "(" + Py.Str(m["defaultValue"]) + ")";
            if (Py.Bool(m.Get("elementStartValues")))
                start = string.Join(", ", ((PyDict)m["elementStartValues"]!).Select(kv => $"[{Py.Str(kv.Key)}] {Py.Str(kv.Value)}"));
            o.Add($"{new string(' ', indent)}{Py.Ljust(m["name"], 24)} {Py.Ljust(m["dataType"], 20)} {Py.Ljust(start is null ? "" : start, 12)} {KV(m["comment"])}");
            o.AddRange(MemberLines(m.Get("members", new List<object?>()), indent + 2));
        }
        return o;
    }

    static string End(PyDict e) => Py.Bool(e.Get("powerrail")) ? "powerrail" : Py.Bool(e.Get("open")) ? "open"
        : e.ContainsKey("operand") ? NeedStr(e["operand"]) : $"{Py.Str(e["name"])}#{Py.Str(e["part"])}.{Py.Str(e["pin"])}";

    public static List<string> NetworkLines(IEnumerable<PyDict> nets, int indent)
    {
        var o = new List<string>();
        var pad = new string(' ', indent);
        foreach (var n in nets)
        {
            var head = $"{pad}Network {Py.Str(n["index"])} [{Py.Str(n["language"])}]";
            if (Py.Bool(n.Get("title")))
                head += "  " + KV(n["title"]);
            o.Add(head);
            if (Py.Bool(n.Get("comment")))
                o.Add($"{pad}  // {KV(n["comment"])}");
            if (Py.Bool(n.Get("error")))
                o.Add($"{pad}  ({Py.Str(n["error"])})");
            foreach (var line in Py.SplitLines(Py.Or(n.Get("scl"), "") as string ?? ""))
                o.Add($"{pad}  | {line}");
            foreach (var line in Py.SplitLines(Py.Or(n.Get("stl"), "") as string ?? ""))
                o.Add($"{pad}  | {line}");
            if (Py.Bool(n.Get("parts")))
            {
                var names = Dicts(n["parts"]).Where(x => Py.Eq(x["kind"], "part")).Select(x => NeedStr(x["name"]));
                o.Add($"{pad}  parts: {string.Join(", ", names)}");
                foreach (var w in Dicts(n["wires"]))
                    o.Add($"{pad}  wire {Py.Str(w["uid"])}: {string.Join(" -- ", Dicts(w["ends"]).Select(End))}");
            }
        }
        return o;
    }

    public static string Of(PyDict d)
    {
        var pr = (PyDict)d["project"]!;
        var o = new List<string> { $"{(Py.Eq(pr.Get("kind"), "GlobalLibrary") ? "Global library" : "Project")} {Py.Str(pr["name"])}" };
        foreach (var g in Dicts(pr["deviceGroups"]))
            o.Add($"  Group {Py.Str(g["path"])}");
        foreach (var dev in Dicts(pr["devices"]))
        {
            o.Add($"  Device {Py.Str(dev["name"])} ({Py.Str(dev["typeName"])}){(Py.Bool(dev["group"]) ? "  group " + Py.Str(dev["group"]) : "")}");
            o.AddRange(Hardware.TextLines((PyDict)dev["hardware"]!, 4));
            foreach (var plc in Dicts(dev["plcs"]))
            {
                o.Add($"    PLC {Py.Str(plc["name"])}  {Py.Str(plc["typeName"])} {Py.Str(plc["orderNumber"])} {Py.Str(plc["firmware"])}");
                foreach (var t in Dicts(plc["tagTables"]))
                {
                    o.Add($"      Tag table {(Py.Bool(t["group"]) ? Py.Str(t["group"]) + "/" : "")}{Py.Str(t["name"])}");
                    foreach (var tag in Dicts(t["tags"]))
                        o.Add($"        {Py.Ljust(tag["name"], 24)} {Py.Ljust(tag["dataType"], 10)} {Py.Ljust(Py.Or(tag["address"], ""), 10)} {KV(tag["comment"])}");
                }
                foreach (var b in Dicts(plc["blocks"]))
                {
                    o.Add($"      Block {Py.Ljust(b["name"], 20)} {Py.Ljust(b["type"], 10)} {Py.Ljust(b["number"], 5)} {Py.Ljust(b["language"], 5)}" +
                          (Py.Bool(b.Get("instanceOf")) ? "  of " + Py.Str(b["instanceOf"]) : "") + (Py.Bool(b["group"]) ? "  group " + Py.Str(b["group"]) : ""));
                    if (!Py.Bool(b.Get("interface")))
                        o.AddRange(MemberLines(b.Get("members", new List<object?>()), 8));
                    foreach (var sec in Dicts(b.Get("interface", new List<object?>())))
                    {
                        o.Add($"        {Py.Str(sec["section"])}");
                        o.AddRange(MemberLines(sec["members"], 10));
                    }
                    o.AddRange(NetworkLines(Dicts(b.Get("networks", new List<object?>())).Where(n => Py.Bool(n.Get("scl")) || Py.Bool(n.Get("stl")) || Py.Bool(n.Get("parts")) ||
                        Py.Bool(n.Get("title")) || Py.Bool(n.Get("comment"))), 8));
                }
                foreach (var t in Dicts(plc["types"]))
                {
                    o.Add($"      Type  {Py.Ljust(t["name"], 20)}{(Py.Bool(t["group"]) ? "  group " + Py.Str(t["group"]) : "")}");
                    o.AddRange(MemberLines(t["members"], 8));
                }
            }
            foreach (var sw in Dicts(dev.Get("hmi", new List<object?>())))
                o.AddRange(Hmi.TextLines(sw, 4));
        }
        o.AddRange(Hardware.NetworkText((PyDict)pr["network"]!, 2));
        var lib = d.Get("library") as PyDict ?? new PyDict();
        if (Dicts(lib.Get("libraries", new List<object?>())).Any(L => Py.Bool(L["masterCopies"]) || Py.Bool(L["types"])) || Py.Bool(lib.Get("instances")))
            o.AddRange(Py.SplitLines(Library.ToText(lib)).Where(line => !line.StartsWith("Statistics:", StringComparison.Ordinal)).Select(line => "  " + line));
        if (Py.Bool(d.Get("faceplates")))
            o.AddRange(Py.SplitLines(Faceplates.ToText((List<object?>)d["faceplates"]!)).Select(line => "  " + line));
        return string.Join("\n", o);
    }
}
