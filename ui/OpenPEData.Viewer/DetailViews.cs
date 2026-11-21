using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenPEData.Viewer;

/// <summary>Builds the detail tabs for a tree node. Tab contents are created when the tab is first shown.</summary>
public static class DetailViews
{
    private const int MaxJsonChars = 3_000_000;
    private static readonly JsonSerializerOptions JsonOut = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Lists with their own views; not repeated as generic tables.</summary>
    private static readonly HashSet<string> SpecialLists = ["interface", "networks", "members"];

    public static List<(string Header, Func<UIElement> Create)> Tabs(TreeNode node)
    {
        var tabs = new List<(string, Func<UIElement>)>();
        var data = node.Data;
        var folder = node.Kind is NodeKind.Folder or NodeKind.BlocksFolder or NodeKind.TagsFolder or NodeKind.TypesFolder
            or NodeKind.ScreensFolder or NodeKind.Libraries or NodeKind.Project;
        if (node.Children.Count > 0 && (folder || data is not JsonObject))
            tabs.Add(($"Content ({node.Children.Count})", () => Grid(ChildrenTable(node), true)));
        if (data is not JsonObject obj)
        {
            if (tabs.Count == 0)
                tabs.Add(("Info", () => Text(data?.ToJsonString(JsonOut) ?? "No data for this node.")));
            return tabs;
        }
        // folders share the data object of their PLC/HMI; repeating it there would only confuse
        if (folder && node.Kind != NodeKind.Project && node.Kind != NodeKind.Libraries) return tabs;

        var props = Properties(obj, node.ListsInTree);
        if (props.Rows.Count > 0) tabs.Add(("Properties", () => Grid(props, false)));

        if (obj["interface"] is JsonArray iface && iface.Count > 0)
            tabs.Add(("Interface", () => Grid(InterfaceTable(iface), true)));
        if (obj["members"] is JsonArray members && members.Count > 0)
            tabs.Add(($"Elements ({members.Count})", () => Grid(MembersTable(members), true)));
        if (obj["code"] is JsonValue cv && cv.TryGetValue<string>(out var code) && code.Length > 0)
            tabs.Add(("Code", () => Text(code, mono: true)));
        if (obj["networks"] is JsonArray nets && nets.Count > 0)
            tabs.Add(($"Networks ({nets.Count})", () => Text(NetworksText(nets), mono: true)));
        if (obj["simaticml"] is JsonArray sml && sml.Any(x => x is not null))
            tabs.Add(("SimaticML", () => Text(string.Join("\n\n", sml.Select((x, i) => x is null ? null
                : $"<!-- Network {i + 1} -->\n{TreeBuilder.Str(x)}").Where(x => x is not null)), mono: true)));

        if (node.Kind == NodeKind.Screen)
            tabs.Add(("Preview", () => ScreenRenderer.Preview(obj, Arr(obj["items"]))));
        if (node.Kind == NodeKind.LibVersion && obj["content"] is JsonObject content && Arr(content["items"]).Any())
            tabs.Add(("Preview", () => ScreenRenderer.Preview(content, Arr(content["items"]))));
        // library image types: the image of the version, on the type that of its default version
        if (node.Kind == NodeKind.LibVersion && obj["image"] is JsonObject image)
            tabs.Add(("Preview", () => ScreenRenderer.GraphicPreview(image)));
        if (node.Kind == NodeKind.LibType && ScreenRenderer.TypeImage(obj) is { } typeImage)
            tabs.Add(("Preview", () => ScreenRenderer.GraphicPreview(typeImage)));

        foreach (var (path, arr) in ObjectLists(obj, "", 0))
        {
            if (path.Length == 0) continue;
            var top = path.Split('.')[0];
            // objects shown as their own tree nodes (project: network, umac, plcAlarms ...) also skip their nested lists
            if (node.ListsInTree.Contains(top) || (!path.Contains('.') && SpecialLists.Contains(top))) continue;
            var a = arr;
            tabs.Add(($"{Label(path)} ({a.Count})", () => Grid(ListTable(a), true)));
        }

        tabs.AddRange(CrossRefTabs(node, obj));

        tabs.Add(("JSON", () =>
        {
            var s = obj.ToJsonString(JsonOut);
            if (s.Length > MaxJsonChars)
                s = s[..MaxJsonChars] + $"\n\n… truncated ({s.Length:N0} characters in total). Child nodes show their data individually.";
            return Text(s, mono: true);
        }));
        return tabs;
    }

    // ---------- tables ----------

    private static DataTable ChildrenTable(TreeNode node)
    {
        var rows = node.Children.Select(c =>
        {
            var d = new Dictionary<string, string?> { ["Name"] = c.Name, ["Info"] = c.Suffix };
            if (c.Data is JsonObject o)
                foreach (var k in new[] { "type", "number", "language", "dataType", "address", "group" })
                    if (o[k] is JsonValue v) d[k] = Scalar(v);
            return d;
        }).ToList();
        return ToTable(rows);
    }

    /// <summary>Tab titles for the list properties of the parser JSON.</summary>
    public static readonly Dictionary<string, string> Labels = new()
    {
        ["tags"] = "Tags", ["items"] = "Screen items", ["alarmClasses"] = "Alarm classes",
        ["discreteAlarms"] = "Discrete alarms", ["analogAlarms"] = "Analog alarms", ["textLists"] = "Text lists",
        ["cycles"] = "Cycles", ["connections"] = "Connections", ["versions"] = "Versions", ["instances"] = "Instances",
        ["entries"] = "Entries", ["events"] = "Events", ["dynamizations"] = "Dynamizations",
        ["dependsOn"] = "Dependencies", ["contents"] = "Content", ["context"] = "Context", ["usedBy"] = "Used by",
        ["masterCopies"] = "Master copies", ["types"] = "Types", ["deviceGroups"] = "Device groups",
        ["referencedObjects"] = "Referenced objects", ["parts"] = "Parts", ["categories"] = "Categories",
        ["unresolvedTags"] = "Unresolved tags", ["instanceOf"] = "Instance of",
        ["localTags"] = "Local tags", ["parameters"] = "Parameters", ["properties"] = "Property list",
        ["referencedBy"] = "Referenced by", ["elements"] = "Elements", ["folders"] = "Folders",
        ["systemFolders"] = "System folders", ["versionsWithoutType"] = "Versions without type",
        ["environmentObjects"] = "Objects of the hidden device", ["standardObjects"] = "Standard objects",
        ["references"] = "Usages", ["addresses"] = "Addresses", ["files"] = "Files", ["commits"] = "Commits",
        ["problems"] = "Problems", ["recordHashErrors"] = "Record hash errors", ["SelectionItems"] = "Selection items",
        ["Points"] = "Points", ["Entries"] = "Entries", ["screens"] = "Screens", ["tagTables"] = "Tag tables",
        ["interface"] = "Interface", ["content"] = "Content",
        ["subnets"] = "Subnets", ["ioSystems"] = "IO systems", ["syncDomains"] = "Domains", ["portLinks"] = "Port interconnections",
        ["nodes"] = "Nodes", ["certificates"] = "Certificates", ["users"] = "Users", ["roles"] = "Roles",
        ["alarms"] = "Alarms", ["units"] = "Software units", ["supervisions"] = "Supervisions",
        ["runtimeGroups"] = "F-runtime groups", ["fModules"] = "F-I/O", ["interfaces"] = "Interfaces",
        ["dataLogs"] = "Data logs", ["alarmLogs"] = "Alarm logs", ["auditTrails"] = "Audit trails",
        ["loggingTags"] = "Logging tags", ["runtimeSettings"] = "Runtime settings",
        ["runtimeLanguages"] = "Runtime languages", ["functions"] = "Functions", ["graphicLists"] = "Graphic lists",
        ["opcUaAlarmTypes"] = "OPC UA alarm types", ["nodes"] = "Nodes",
    };

    /// <summary>Tab title of a (possibly dotted) list path, e.g. "interface.tags" = "Interface: Tags".</summary>
    private static string Label(string path) =>
        Labels.TryGetValue(path, out var l) ? l
            : string.Join(": ", path.Split('.').Where(s => s != "content").DefaultIfEmpty("content").Select(s => Labels.GetValueOrDefault(s, s)));

    /// <summary>XRef.db content once the parser run --xref is done; null before and for projects without XRef.db.</summary>
    public static JsonObject? CrossRefs { get; set; }

    /// <summary>Content of a cross reference tab while <see cref="CrossRefs"/> is null (run pending or failed, or no
    /// XRef.db); set by the main window, which starts the parser run --xref on first use.</summary>
    public static Func<TreeNode, UIElement>? CrossRefsPending { get; set; }

    /// <summary>"Used by" / "Uses" for blocks, PLC data types and screens, "Usages" for tag tables,
    /// taken from <see cref="CrossRefs"/> by object name within the PLC or HMI software.</summary>
    private static IEnumerable<(string, Func<UIElement>)> CrossRefTabs(TreeNode node, JsonObject obj)
    {
        if (node.Kind is not (NodeKind.Block or NodeKind.PlcType or NodeKind.Screen or NodeKind.TagTable or NodeKind.AllTags)) yield break;
        var objectTabs = node.Kind is NodeKind.Block or NodeKind.PlcType or NodeKind.Screen;
        if (CrossRefs?["references"] is not JsonArray refs)
        {
            if (CrossRefsPending is not { } pending) yield break;
            foreach (var h in objectTabs ? ["Used by", "Uses"] : new[] { "Usages" })
                yield return (h, () => pending(node));
            yield break;
        }
        static (string, Func<UIElement>) Tab(string header, List<Dictionary<string, string?>> rows)
        {
            var merged = Merge(rows);
            return ($"{header} ({merged.Count})",
                () => merged.Count == 0 ? Text("No entries in XRef.db.") : Grid(ToTable(merged), true));
        }
        // scope = the PLC or HMI software the object belongs to
        var scopeNode = node.Parent;
        while (scopeNode != null && scopeNode.Kind is not (NodeKind.Plc or NodeKind.Hmi)) scopeNode = scopeNode.Parent;
        var scope = TreeBuilder.Str(scopeNode?.Data?["name"]);
        bool SameScope(JsonNode r) => scope == null || TreeBuilder.Str(r["scope"]) == scope;

        Dictionary<string, string?> Row(JsonNode r, string otherSide)
        {
            var o = otherSide == "user" ? "used" : "user";
            return new Dictionary<string, string?>
            {
                [otherSide == "user" ? "Used by" : "Uses"] = TreeBuilder.Str(r[otherSide]),
                ["Kind"] = TreeBuilder.Str(r[otherSide + "Kind"]),
                ["Member-ID"] = Scalar(r[otherSide + "Member"]),
                ["Own element"] = TreeBuilder.Str(r[o + "Kind"]) + (r[o + "Member"] is JsonValue m ? " #" + Scalar(m) : ""),
                ["Count"] = Scalar(r["count"]),
                ["Usage"] = Scalar(r["usage"]),
                ["Flags"] = Scalar(r["flags"]),
            };
        }

        if (objectTabs)
        {
            var name = TreeBuilder.Str(obj["name"]);
            if (name == null) yield break;
            var by = refs.OfType<JsonNode>().Where(r => SameScope(r) && TreeBuilder.Str(r["used"]) == name
                                                       && TreeBuilder.Str(r["user"]) != name).Select(r => Row(r, "user")).ToList();
            var uses = refs.OfType<JsonNode>().Where(r => SameScope(r) && TreeBuilder.Str(r["user"]) == name
                                                         && TreeBuilder.Str(r["used"]) != name).Select(r => Row(r, "used")).ToList();
            yield return Tab("Used by", by);
            yield return Tab("Uses", uses);
            yield break;
        }

        // tag table: every use of one of its tags
        var tags = Arr(obj["tags"]).Select(t => TreeBuilder.Str(t["name"])).Where(n => n != null).ToHashSet();
        var rows = refs.OfType<JsonNode>()
            .Where(r => SameScope(r) && TreeBuilder.Str(r["usedKind"]) is "Tag" or "HmiTagData" && tags.Contains(TreeBuilder.Str(r["used"])))
            .Select(r => new Dictionary<string, string?>
            {
                ["Tag"] = TreeBuilder.Str(r["used"]),
                ["Used by"] = TreeBuilder.Str(r["user"]),
                ["Kind"] = TreeBuilder.Str(r["userKind"]),
                ["Count"] = Scalar(r["count"]),
                ["Usage"] = Scalar(r["usage"]),
                ["Flags"] = Scalar(r["flags"]),
            })
            .OrderBy(d => d["Tag"], StringComparer.OrdinalIgnoreCase).ToList();
        yield return Tab("Usages", rows);
    }

    /// <summary>Rows that differ only in their count (XRef groups by member ids not shown here) become one row.</summary>
    private static List<Dictionary<string, string?>> Merge(List<Dictionary<string, string?>> rows) =>
        rows.GroupBy(r => string.Join("\u0001", r.Where(kv => kv.Key != "Count").Select(kv => kv.Value)))
            .Select(g =>
            {
                var r = new Dictionary<string, string?>(g.First());
                r["Count"] = g.Sum(x => int.TryParse(x["Count"], out var n) ? n : 0).ToString(CultureInfo.InvariantCulture);
                return r;
            })
            .ToList();

    private static DataTable Properties(JsonObject obj, HashSet<string> listsInTree)
    {
        var t = new DataTable();
        t.Columns.Add("Property");
        t.Columns.Add("Value");
        void Walk(JsonObject o, string prefix)
        {
            foreach (var (k, v) in o)
            {
                var key = prefix + k;
                switch (v)
                {
                    case JsonObject sub when sub.Count == 0 || (prefix.Length == 0 && listsInTree.Contains(k)):
                        break;
                    case JsonObject sub:
                        Walk(sub, key + ".");
                        break;
                    case JsonArray { Count: 0 }:
                        break;
                    case JsonArray a when a.All(x => x is null or JsonValue):
                        t.Rows.Add(key, string.Join(", ", a.Select(Scalar)));
                        break;
                    case JsonArray a when prefix.Length == 0 && (listsInTree.Contains(k) || SpecialLists.Contains(k) || a.Any(x => x is JsonObject)):
                        break;
                    case JsonArray a:
                        t.Rows.Add(key, $"[{a.Count} entries]");
                        break;
                    default:
                        t.Rows.Add(key, Scalar(v));
                        break;
                }
            }
        }
        Walk(obj, "");
        return t;
    }

    /// <summary>Arrays of objects in the object and its nested dictionaries (depth ≤ 2), with dotted paths.</summary>
    private static IEnumerable<(string, JsonArray)> ObjectLists(JsonObject o, string prefix, int depth)
    {
        foreach (var (k, v) in o)
        {
            if (v is JsonArray a && a.Count > 0 && a.Any(x => x is JsonObject))
                yield return (prefix + k, a);
            else if (v is JsonObject sub && depth < 2)
                foreach (var x in ObjectLists(sub, prefix + k + ".", depth + 1))
                    yield return x;
        }
    }

    private static DataTable ListTable(JsonArray arr)
    {
        var rows = arr.Select(x => x is JsonObject o ? Flatten(o) : new Dictionary<string, string?> { ["Value"] = Scalar(x) }).ToList();
        return ToTable(rows);
    }

    private static DataTable InterfaceTable(JsonArray sections)
    {
        var rows = new List<Dictionary<string, string?>>();
        foreach (var sec in Arr(sections))
            foreach (var r in MemberRows(Arr(sec["members"]), 0, ""))
            {
                var d = new Dictionary<string, string?> { ["Section"] = TreeBuilder.Str(sec["section"]) };
                foreach (var kv in r) d[kv.Key] = kv.Value;
                rows.Add(d);
            }
        return ToTable(DropFlatPath(rows));
    }

    private static DataTable MembersTable(JsonArray members) => ToTable(DropFlatPath(MemberRows(Arr(members), 0, "").ToList()));

    /// <summary>The path column only helps when there are nested members.</summary>
    private static List<Dictionary<string, string?>> DropFlatPath(List<Dictionary<string, string?>> rows)
    {
        if (rows.All(r => !(r.GetValueOrDefault("Path") ?? "").Contains('.')))
            foreach (var r in rows) r.Remove("Path");
        return rows;
    }

    /// <summary>Struct/UDT members flattened: name indented by depth, plus the full path.</summary>
    private static IEnumerable<Dictionary<string, string?>> MemberRows(IEnumerable<JsonNode> members, int depth, string path)
    {
        foreach (var m in members)
        {
            if (m is not JsonObject o) continue;
            var name = TreeBuilder.Str(o["name"]) ?? "";
            var full = path.Length == 0 ? name : path + "." + name;
            var flat = Flatten(o, skip: "members");
            var row = new Dictionary<string, string?> { ["name"] = new string(' ', depth * 4) + name, ["Path"] = full };
            foreach (var kv in flat) if (kv.Key != "name") row[kv.Key] = kv.Value;
            yield return row;
            foreach (var c in MemberRows(Arr(o["members"]), depth + 1, full)) yield return c;
        }
    }

    private static Dictionary<string, string?> Flatten(JsonObject o, string? skip = null)
    {
        var d = new Dictionary<string, string?>();
        void Walk(JsonObject x, string prefix)
        {
            foreach (var (k, v) in x)
            {
                if (prefix.Length == 0 && k == skip) continue;
                switch (v)
                {
                    case JsonObject sub:
                        Walk(sub, prefix + k + ".");
                        break;
                    case JsonArray a when a.All(e => e is null or JsonValue):
                        d[prefix + k] = string.Join(", ", a.Select(Scalar));
                        break;
                    case JsonArray a:
                        d[prefix + k] = a.Count == 0 ? "" : $"[{a.Count}]";
                        break;
                    default:
                        d[prefix + k] = Scalar(v);
                        break;
                }
            }
        }
        Walk(o, "");
        return d;
    }

    public static DataTable ToTable(List<Dictionary<string, string?>> rows)
    {
        var cols = new List<string>();
        var seen = new HashSet<string>();
        foreach (var r in rows)
            foreach (var k in r.Keys)
                if (seen.Add(k)) cols.Add(k);
        // columns that are empty in every row only add noise
        cols = cols.Where(c => rows.Any(r => !string.IsNullOrEmpty(r.GetValueOrDefault(c)))).ToList();

        var t = new DataTable();
        for (var i = 0; i < cols.Count; i++)
        {
            var c = cols[i];
            var numeric = rows.All(r => r.GetValueOrDefault(c) is not { Length: > 0 } s
                                        || double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
            var col = t.Columns.Add("c" + i, numeric ? typeof(double) : typeof(string));
            col.Caption = c;
            col.AllowDBNull = true;
        }
        foreach (var r in rows)
        {
            var row = t.NewRow();
            for (var i = 0; i < cols.Count; i++)
            {
                var v = r.GetValueOrDefault(cols[i]);
                if (string.IsNullOrEmpty(v)) continue;
                row[i] = t.Columns[i].DataType == typeof(double)
                    ? double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture)
                    : v;
            }
            t.Rows.Add(row);
        }
        return t;
    }

    // ---------- controls ----------

    internal static UIElement Grid(DataTable table, bool captions)
    {
        var g = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = true,
            CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xE7, 0xEB)),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(0xF7, 0xF9, 0xFB)),
            BorderThickness = new Thickness(0),
            Background = Brushes.White,
            FontFamily = new FontFamily("Segoe UI"),
            EnableRowVirtualization = true,
            EnableColumnVirtualization = true,
            ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader,
            SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
        };
        VirtualizingPanel.SetIsVirtualizingWhenGrouping(g, true);
        g.AutoGeneratingColumn += (_, e) =>
        {
            var col = table.Columns[e.PropertyName];
            if (col == null) return;
            if (captions || col.Caption != col.ColumnName) e.Column.Header = captions ? Caption(col.Caption) : col.Caption;
            if (e.Column is DataGridTextColumn tc)
            {
                tc.MaxWidth = 600;
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,
                    new System.Windows.Data.Binding(col.ColumnName)));
                tc.ElementStyle = style;
            }
        };
        g.ItemsSource = table.DefaultView;
        return g;
    }

    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["plc"] = "PLC", ["hmi"] = "HMI", ["db"] = "DB", ["fb"] = "FB", ["fc"] = "FC", ["ob"] = "OB", ["id"] = "ID",
        ["ip"] = "IP", ["opc"] = "OPC", ["ua"] = "UA", ["udt"] = "UDT", ["url"] = "URL", ["hw"] = "HW",
    };

    /// <summary>Column caption in plain words: "dataType" = "Data type", "tagTable" = "Tag table", "plc" = "PLC",
    /// "comment.en-US" = "Comment (en-US)", "texts.alarmText.en-US" = "Alarm text (en-US)".</summary>
    public static string Caption(string key)
    {
        if (key.Length == 0 || key.Contains(' ')) return key;
        var parts = key.Split('.');
        string? culture = null;
        if (parts.Length > 1 && System.Text.RegularExpressions.Regex.IsMatch(parts[^1], "^[a-z]{2,3}(-[A-Za-z0-9]+)*$") && parts[^1].Contains('-'))
        {
            culture = parts[^1];
            parts = parts[..^1];
        }
        // the last meaningful part names the column ("texts.alarmText" = "Alarm text"); other parent parts as prefix
        var name = parts.Length > 1 && parts[0] is "texts" ? parts[^1] : string.Join(" ", parts);
        var words = System.Text.RegularExpressions.Regex.Matches(name, "[A-Z]{2,}(?=[A-Z][a-z]|\\b|[0-9])|[A-Z]?[a-z]+|[A-Z]+|[0-9]+")
            .Select(m => Words.TryGetValue(m.Value, out var w) ? w : m.Value.ToLowerInvariant()).ToList();
        if (words.Count == 0) return key;
        if (words[0].Length > 0 && char.IsLower(words[0][0])) words[0] = char.ToUpperInvariant(words[0][0]) + words[0][1..];
        var text = string.Join(" ", words);
        return culture == null ? text : $"{text} ({culture})";
    }

    private static UIElement Text(string text, bool mono = false) => new TextBox
    {
        Text = text,
        IsReadOnly = true,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(8),
        FontFamily = new FontFamily(mono ? "Cascadia Mono, Consolas" : "Segoe UI"),
        FontSize = mono ? 12.5 : 13,
        TextWrapping = mono ? TextWrapping.NoWrap : TextWrapping.Wrap,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        AcceptsReturn = true,
    };

    // ---------- code ----------

    private static string Texts(JsonNode? n) =>
        n is JsonObject o ? string.Join("; ", o.Select(kv => $"{kv.Key}: {Scalar(kv.Value)}")) : "";

    /// <summary>Same layout as TextTree.NetworkLines of OpenPEData.dll (text output of the CLI).</summary>
    private static string NetworksText(JsonArray nets)
    {
        var sb = new StringBuilder();
        foreach (var n in Arr(nets))
        {
            sb.Append($"Network {Scalar(n["index"])} [{Scalar(n["language"])}]");
            var title = Texts(n["title"]);
            if (title.Length > 0) sb.Append("  ").Append(title);
            sb.AppendLine();
            var comment = Texts(n["comment"]);
            if (comment.Length > 0) sb.AppendLine("  // " + comment);
            if (n["error"] != null) sb.AppendLine($"  ({Scalar(n["error"])})");
            var scl = TreeBuilder.Str(n["scl"]) ?? TreeBuilder.Str(n["stl"]);
            if (!string.IsNullOrEmpty(scl))
                foreach (var line in scl.Replace("\r\n", "\n").Split('\n'))
                    sb.AppendLine("  | " + line);
            var parts = Arr(n["parts"]).ToList();
            if (parts.Count > 0)
            {
                sb.AppendLine("  parts:");
                foreach (var p in parts)
                {
                    var kind = TreeBuilder.Str(p["kind"]);
                    var label = kind == "part" ? TreeBuilder.Str(p["name"]) : kind == "operand" || p["operand"] != null
                        ? TreeBuilder.Str(p["operand"]) ?? TreeBuilder.Str(p["name"]) : TreeBuilder.Str(p["name"]);
                    var extra = p is JsonObject po
                        ? string.Join(" ", po.Where(kv => kv.Key is not ("kind" or "name") && kv.Value is JsonValue)
                            .Select(kv => $"{kv.Key}={Scalar(kv.Value)}"))
                        : "";
                    sb.AppendLine($"    {kind,-8} {label}  {extra}");
                }
                foreach (var w in Arr(n["wires"]))
                {
                    var ends = Arr(w["ends"]).Select(End);
                    sb.AppendLine($"  wire {Scalar(w["uid"])}: {string.Join(" -- ", ends)}");
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string End(JsonNode e)
    {
        if (e["powerrail"]?.GetValue<bool>() == true) return "Powerrail";
        if (e["open"]?.GetValue<bool>() == true) return "open";
        if (e["operand"] != null) return Scalar(e["operand"]) ?? "";
        return $"{Scalar(e["name"])}#{Scalar(e["part"])}.{Scalar(e["pin"])}";
    }

    // ---------- screen preview ----------

    // ---------- helpers ----------


    internal static string? Scalar(JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
        JsonValue v => v.ToJsonString(),
        _ => n.ToJsonString(),
    };

    private static IEnumerable<JsonNode> Arr(JsonNode? n) => TreeBuilder.Arr(n);
}
