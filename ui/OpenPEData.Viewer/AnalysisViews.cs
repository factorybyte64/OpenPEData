using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace OpenPEData.Viewer;

/// <summary>A project-wide list of one analysis view (all blocks, all PLC tags …): rows of column name and value,
/// built on first use.</summary>
public sealed class ProjectList(string key, string title, string fileName, Func<List<Dictionary<string, string?>>> build)
{
    private List<Dictionary<string, string?>>? _rows;
    public string Key { get; } = key;
    public string Title { get; } = title;
    /// <summary>File name without extension for the CSV export.</summary>
    public string FileName { get; } = fileName;
    public List<Dictionary<string, string?>> Rows => _rows ??= build();
}

/// <summary>The loaded project for the analysis views: the parser JSON, its path, and the project-wide lists.</summary>
public sealed class AnalysisContext
{
    public AnalysisContext(JsonNode json, string path, string projectName)
    {
        Json = json;
        Path = path;
        ProjectName = projectName;
        Lists = AnalysisViews.BuildLists(json);
    }

    public JsonNode Json { get; }
    public string Path { get; }
    public string ProjectName { get; }
    public IReadOnlyList<ProjectList> Lists { get; }
    /// <summary>Warnings and objects the parser could not read (shown in the overview).</summary>
    public string? ParserMessages { get; init; }
    public ProjectList List(string key) => Lists.First(l => l.Key == key);
    /// <summary>Selects the tree node of a view (tiles of the overview).</summary>
    public Action<NodeKind>? Navigate { get; set; }
    /// <summary>Text for the status bar.</summary>
    public Action<string>? Status { get; set; }
}

/// <summary>The OpenPEData views at the top level of the tree next to the project: overview, project-wide lists of
/// blocks, tags and alarms, and the export.</summary>
public static class AnalysisViews
{
    // ---------- tree ----------

    /// <summary>The view nodes shown after the project node (cross-references and the integrity check are added by the
    /// main window, they run the parser in the background).</summary>
    public static List<TreeNode> ViewNodes(AnalysisContext ctx)
    {
        int Count(params string[] keys) => keys.Sum(k => ctx.List(k).Rows.Count);
        return
        [
            new TreeNode("Overview", NodeKind.Overview),
            new TreeNode("Blocks", NodeKind.BlockList, null, Count("blocks").ToString(CultureInfo.InvariantCulture)),
            new TreeNode("Tags", NodeKind.TagList, null, Count("plcTags", "hmiTags").ToString(CultureInfo.InvariantCulture)),
            new TreeNode("Alarms", NodeKind.AlarmList, null, Count("plcAlarms", "hmiDiscreteAlarms", "hmiAnalogAlarms").ToString(CultureInfo.InvariantCulture)),
        ];
    }

    public static TreeNode ExportNode() => new("Export", NodeKind.Export);

    /// <summary>Lists shown by each list view, as tabs.</summary>
    private static readonly Dictionary<NodeKind, string[]> ViewLists = new()
    {
        [NodeKind.BlockList] = ["blocks", "plcTypes"],
        [NodeKind.TagList] = ["plcTags", "hmiTags"],
        [NodeKind.AlarmList] = ["plcAlarms", "hmiDiscreteAlarms", "hmiAnalogAlarms"],
    };

    public static bool IsView(NodeKind k) => k is NodeKind.Overview or NodeKind.BlockList or NodeKind.TagList
        or NodeKind.AlarmList or NodeKind.Export;

    public static List<(string Header, Func<UIElement> Create)> Tabs(TreeNode node, AnalysisContext? ctx)
    {
        var tabs = new List<(string, Func<UIElement>)>();
        if (ctx == null)
        {
            tabs.Add(("Info", () => Message("Open a project first.")));
            return tabs;
        }
        switch (node.Kind)
        {
            case NodeKind.Overview:
                tabs.Add(("Summary", () => Summary(ctx)));
                tabs.Add(($"Devices ({ctx.List("devices").Rows.Count})", () => ListView(ctx, ctx.List("devices"))));
                break;
            case NodeKind.Export:
                tabs.Add(("Export", () => ExportPanel(ctx)));
                break;
            default:
                if (ViewLists.TryGetValue(node.Kind, out var keys))
                {
                    // empty lists only when all are empty (then the first one says so)
                    var lists = keys.Select(ctx.List).ToList();
                    if (lists.Any(l => l.Rows.Count > 0)) lists.RemoveAll(l => l.Rows.Count == 0);
                    else lists = lists.Take(1).ToList();
                    foreach (var l in lists)
                        tabs.Add(($"{l.Title} ({l.Rows.Count})", () => ListView(ctx, l)));
                }
                break;
        }
        return tabs;
    }

    // ---------- lists ----------

    private static IEnumerable<JsonNode> Arr(JsonNode? n) => TreeBuilder.Arr(n);
    private static string? Str(JsonNode? n) => TreeBuilder.Str(n);

    /// <summary>Keys that are tree structures or code, not columns.</summary>
    private static readonly HashSet<string> Skip = ["interface", "networks", "code", "simaticml", "members", "loggingTags", "items",
        "group", "languageInternal"];

    /// <summary>Adds the scalar properties of an object as columns (text dictionaries like comment.en-US as their own
    /// columns, lists of values joined); lists of objects are left out.</summary>
    private static Dictionary<string, string?> AddScalars(Dictionary<string, string?> d, JsonObject o, string prefix = "", int depth = 0)
    {
        foreach (var (k, v) in o)
        {
            if (depth == 0 && Skip.Contains(k)) continue;
            var key = prefix + k;
            switch (v)
            {
                case null:
                    break;
                case JsonValue:
                    d.TryAdd(key, DetailViews.Scalar(v));
                    break;
                case JsonObject sub when depth < 2:
                    AddScalars(d, sub, key + ".", depth + 1);
                    break;
                case JsonArray a when a.All(e => e is null or JsonValue):
                    d.TryAdd(key, string.Join(", ", a.Select(DetailViews.Scalar)));
                    break;
            }
        }
        return d;
    }

    private static Dictionary<string, string?> Row(params (string Key, string? Value)[] first)
    {
        var d = new Dictionary<string, string?>();
        foreach (var (k, v) in first) d[k] = v;
        return d;
    }

    private static IEnumerable<(JsonNode Device, JsonNode Plc)> Plcs(JsonNode json) =>
        Arr(json["project"]?["devices"]).SelectMany(d => Arr(d["plcs"]).Select(p => (d, p)));

    private static IEnumerable<(JsonNode Device, JsonNode Hmi)> Hmis(JsonNode json) =>
        Arr(json["project"]?["devices"]).SelectMany(d => Arr(d["hmi"]).Select(h => (d, h)));

    private static bool IsSystemType(JsonNode b) => (Str(b["group"]) ?? "").StartsWith("SystemDataTypeFolder", StringComparison.Ordinal);

    public static List<ProjectList> BuildLists(JsonNode json)
    {
        var project = json["project"];
        return
        [
            new("devices", "Devices", "devices", () => DeviceRows(json)),
            new("blocks", "Program blocks", "program-blocks", () =>
                Plcs(json).SelectMany(x => Arr(x.Plc["blocks"]).Where(b => !IsSystemType(b)).Select(b => AddScalars(Row(
                    ("plc", Str(x.Plc["name"])), ("name", Str(b["name"])), ("type", Str(b["type"])), ("number", Str(b["number"])),
                    ("language", Str(b["language"])), ("folder", TreeBuilder.FolderDisplay(Str(b["group"])))), (JsonObject)b))).ToList()),
            new("plcTypes", "PLC data types", "plc-data-types", () =>
                Plcs(json).SelectMany(x => Arr(x.Plc["types"]).Concat(Arr(x.Plc["blocks"]).Where(IsSystemType)).Select(t => AddScalars(Row(
                    ("plc", Str(x.Plc["name"])), ("name", Str(t["name"])), ("folder", TreeBuilder.FolderDisplay(Str(t["group"])))), (JsonObject)t))).ToList()),
            new("plcTags", "PLC tags", "plc-tags", () =>
                Plcs(json).SelectMany(x => Arr(x.Plc["tagTables"]).SelectMany(t => Arr(t["tags"]).Select(tag => AddScalars(Row(
                    ("plc", Str(x.Plc["name"])), ("tagTable", Str(t["name"])), ("name", Str(tag["name"])), ("dataType", Str(tag["dataType"]))),
                    (JsonObject)tag)))).ToList()),
            new("hmiTags", "HMI tags", "hmi-tags", () =>
                Hmis(json).SelectMany(x => Arr(x.Hmi["tagTables"]).SelectMany(t => Arr(t["tags"]).Select(tag => AddScalars(Row(
                    ("hmi", Str(x.Hmi["name"])), ("tagTable", Str(t["name"])), ("name", Str(tag["name"])), ("dataType", Str(tag["dataType"]))),
                    (JsonObject)tag)))).ToList()),
            new("plcAlarms", "PLC alarms", "plc-alarms", () =>
                Arr(project?["plcAlarms"]?["alarms"]).Select(a => AddScalars(Row(("plc", Str(a["plc"])), ("block", Str(a["block"])),
                    ("name", Str(a["name"]))), (JsonObject)a)).ToList()),
            new("hmiDiscreteAlarms", "HMI discrete alarms", "hmi-discrete-alarms", () =>
                Hmis(json).SelectMany(x => Arr(x.Hmi["discreteAlarms"]).Select(a => AddScalars(Row(("hmi", Str(x.Hmi["name"])),
                    ("name", Str(a["name"]))), (JsonObject)a))).ToList()),
            new("hmiAnalogAlarms", "HMI analog alarms", "hmi-analog-alarms", () =>
                Hmis(json).SelectMany(x => Arr(x.Hmi["analogAlarms"]).Select(a => AddScalars(Row(("hmi", Str(x.Hmi["name"])),
                    ("name", Str(a["name"]))), (JsonObject)a))).ToList()),
            new("screens", "HMI screens", "hmi-screens", () =>
                Hmis(json).SelectMany(x => Arr(x.Hmi["screens"]).Select(s => Row(("hmi", Str(x.Hmi["name"])), ("name", Str(s["name"])),
                    ("folder", Str(s["group"])), ("width", Str(s["width"])), ("height", Str(s["height"])),
                    ("objects", Arr(s["items"]).Count().ToString(CultureInfo.InvariantCulture))))).ToList()),
        ];
    }

    /// <summary>One row per PLC and HMI (a station without either: one row for the station).</summary>
    private static List<Dictionary<string, string?>> DeviceRows(JsonNode json)
    {
        var rows = new List<Dictionary<string, string?>>();
        string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        foreach (var dev in Arr(json["project"]?["devices"]))
        {
            var station = Str(dev["name"]);
            var any = false;
            foreach (var p in Arr(dev["plcs"]))
            {
                any = true;
                rows.Add(Row(("station", station), ("name", Str(p["name"])), ("kind", "PLC"), ("type", Str(p["typeName"])),
                    ("orderNumber", Str(p["orderNumber"])), ("firmware", Str(p["firmware"])),
                    ("blocks", N(Arr(p["blocks"]).Count(b => !IsSystemType(b)))),
                    ("tags", N(Arr(p["tagTables"]).Sum(t => Arr(t["tags"]).Count()))), ("screens", null), ("folder", Str(dev["group"]))));
            }
            foreach (var h in Arr(dev["hmi"]))
            {
                any = true;
                rows.Add(Row(("station", station), ("name", Str(h["name"])), ("kind", "HMI"), ("type", Str(h["typeName"])),
                    ("orderNumber", Str(h["orderNumber"])), ("firmware", Str(h["firmware"])), ("blocks", null),
                    ("tags", N(Arr(h["tagTables"]).Sum(t => Arr(t["tags"]).Count()))), ("screens", N(Arr(h["screens"]).Count())),
                    ("folder", Str(dev["group"]))));
            }
            if (!any)
                rows.Add(Row(("station", station), ("name", station), ("kind", "Device"), ("type", Str(dev["typeName"])),
                    ("folder", Str(dev["group"]))));
        }
        return rows;
    }

    /// <summary>Key figures of the project for the overview tiles and the report.</summary>
    public static List<(string Label, int Value, NodeKind? Target)> Figures(AnalysisContext ctx)
    {
        int C(string key) => ctx.List(key).Rows.Count;
        var json = ctx.Json;
        var libTypes = Arr(json["library"]?["libraries"]).Sum(l => Arr(l["types"]).Count(t => Str(t["category"]) != "system"));
        return
        [
            ("Devices", Arr(json["project"]?["devices"]).Count(), NodeKind.Overview),
            ("PLCs", Plcs(json).Count(), NodeKind.Overview),
            ("HMI devices", Hmis(json).Count(), NodeKind.Overview),
            ("Program blocks", C("blocks"), NodeKind.BlockList),
            ("PLC data types", C("plcTypes"), NodeKind.BlockList),
            ("PLC tags", C("plcTags"), NodeKind.TagList),
            ("HMI tags", C("hmiTags"), NodeKind.TagList),
            ("HMI screens", C("screens"), null),
            ("PLC alarms", C("plcAlarms"), NodeKind.AlarmList),
            ("HMI alarms", C("hmiDiscreteAlarms") + C("hmiAnalogAlarms"), NodeKind.AlarmList),
            ("Library types", libTypes, null),
        ];
    }

    // ---------- views ----------

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private static UIElement Message(string text) => new TextBlock
    {
        Text = text, Margin = new Thickness(18), FontSize = 14, Foreground = Res("Muted"), TextWrapping = TextWrapping.Wrap,
    };

    private static Border Card(UIElement child, Thickness margin) => new()
    {
        Background = Brushes.White, BorderBrush = Res("Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
        Padding = new Thickness(16, 12, 16, 12), Margin = margin, Child = child,
    };

    /// <summary>Overview: project name and file, tiles with the key figures (a click opens the matching view), and the
    /// file statistics of the parser.</summary>
    private static UIElement Summary(AnalysisContext ctx)
    {
        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 18) };
        root.Children.Add(new TextBlock { Text = ctx.ProjectName, FontSize = 22, FontWeight = FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = ctx.Path, Foreground = Res("Muted"), Margin = new Thickness(0, 2, 0, 14), TextTrimming = TextTrimming.CharacterEllipsis });

        var tiles = new WrapPanel();
        foreach (var (label, value, target) in Figures(ctx))
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = value.ToString("N0", CultureInfo.InvariantCulture), FontSize = 26, FontWeight = FontWeights.SemiBold,
                Foreground = value > 0 ? Res("Accent") : Res("Muted") });
            content.Children.Add(new TextBlock { Text = label, Foreground = Res("Muted") });
            var tile = Card(content, new Thickness(0, 0, 10, 10));
            tile.Width = 168;
            if (target is { } t && t != NodeKind.Overview && ctx.Navigate != null)
            {
                tile.Cursor = Cursors.Hand;
                tile.ToolTip = "Open the view";
                tile.MouseLeftButtonUp += (_, _) => ctx.Navigate(t);
                tile.MouseEnter += (_, _) => tile.BorderBrush = Res("Accent");
                tile.MouseLeave += (_, _) => tile.BorderBrush = Res("Line");
            }
            tiles.Children.Add(tile);
        }
        root.Children.Add(tiles);

        if (ctx.Json["statistics"] is JsonObject st)
        {
            root.Children.Add(new TextBlock { Text = "Project file", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 10, 0, 6) });
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var facts = new List<(string, string?)>
            {
                ("Objects", Str(st["objects"])), ("Records", Str(st["records"])), ("Deleted objects", Str(st["deletedObjects"])),
                ("Hash errors", Str(st["hashErrors"])), ("Data files", string.Join(", ", Arr(st["plfFiles"]).Select(Str))),
            };
            foreach (var (k, v) in facts.Where(f => !string.IsNullOrEmpty(f.Item2)))
            {
                grid.RowDefinitions.Add(new RowDefinition());
                var r = grid.RowDefinitions.Count - 1;
                var a = new TextBlock { Text = k, Foreground = Res("Muted"), Margin = new Thickness(0, 2, 0, 2) };
                var b = new TextBlock { Text = v, Margin = new Thickness(0, 2, 0, 2), TextWrapping = TextWrapping.Wrap };
                if (k == "Hash errors" && v != "0") b.Foreground = Brushes.Firebrick;
                Grid.SetRow(a, r);
                Grid.SetRow(b, r);
                Grid.SetColumn(b, 1);
                grid.Children.Add(a);
                grid.Children.Add(b);
            }
            root.Children.Add(Card(grid, new Thickness(0, 0, 10, 0)));
        }
        if (ctx.ParserMessages is { } pm)
        {
            root.Children.Add(new TextBlock { Text = "Parser messages", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 14, 0, 6) });
            root.Children.Add(Card(new TextBlock { Text = pm, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12 }, new Thickness(0, 0, 10, 0)));
        }
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>A list with a filter box (all columns, contains, case ignored), the row count, copying, and the CSV
    /// export of the rows shown.</summary>
    private static UIElement ListView(AnalysisContext ctx, ProjectList list)
    {
        if (list.Rows.Count == 0) return Message($"The project has no {list.Title.ToLowerInvariant()}.");
        var table = DetailViews.ToTable(list.Rows);
        var grid = DetailViews.Grid(table, true);
        if (grid is DataGrid dg) dg.Background = Brushes.White;
        var view = table.DefaultView;

        var filter = new TextBox { Width = 280, Padding = new Thickness(6, 4, 6, 4), VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Shows the rows that contain the text in any column" };
        var placeholder = new TextBlock { Text = "Filter rows…", IsHitTestVisible = false, Margin = new Thickness(9, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center, Foreground = Res("Muted") };
        var filterBox = new Grid();
        filterBox.Children.Add(filter);
        filterBox.Children.Add(placeholder);
        var count = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Foreground = Res("Muted") };
        void UpdateCount() => count.Text = view.Count == table.Rows.Count
            ? (table.Rows.Count == 1 ? "1 row" : $"{table.Rows.Count:N0} rows") : $"{view.Count:N0} of {table.Rows.Count:N0} rows";
        filter.TextChanged += (_, _) =>
        {
            placeholder.Visibility = filter.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            view.RowFilter = RowFilter(table, filter.Text.Trim());
            UpdateCount();
        };
        UpdateCount();

        var export = new Button { Content = "Export CSV…", Padding = new Thickness(12, 4, 12, 4), Background = Brushes.Transparent,
            BorderBrush = Res("Line"), ToolTip = "Saves the rows shown (with the filter) as a CSV file" };
        export.Click += (_, _) =>
        {
            var dlg = new SaveFileDialog { Title = "Export " + list.Title, Filter = "CSV file (*.csv)|*.csv",
                FileName = $"{SafeName(ctx.ProjectName)}_{list.FileName}.csv" };
            if (dlg.ShowDialog() != true) return;
            Csv.Write(view, dlg.FileName);
            ctx.Status?.Invoke($"{view.Count:N0} rows exported to {dlg.FileName}");
        };

        var bar = new DockPanel { Margin = new Thickness(10, 8, 10, 8), LastChildFill = false };
        DockPanel.SetDock(export, Dock.Right);
        bar.Children.Add(export);
        bar.Children.Add(filterBox);
        bar.Children.Add(count);
        var barBorder = new Border { Child = bar, BorderBrush = Res("Line"), BorderThickness = new Thickness(0, 0, 0, 1), Background = Res("PaneBg") };

        var panel = new DockPanel();
        DockPanel.SetDock(barBorder, Dock.Top);
        panel.Children.Add(barBorder);
        panel.Children.Add(grid);
        return panel;
    }

    /// <summary>DataView row filter: rows with the text in any column (LIKE, case ignored by the DataTable).</summary>
    public static string RowFilter(DataTable table, string text)
    {
        if (text.Length == 0) return "";
        var sb = new StringBuilder();
        foreach (var ch in text)
            sb.Append(ch switch { '\'' => "''", '*' or '%' or '[' or ']' => "[" + ch + "]", _ => ch.ToString() });
        var pattern = "'%" + sb + "%'";
        return string.Join(" OR ", table.Columns.Cast<DataColumn>().Select(c => c.DataType == typeof(string)
            ? $"{c.ColumnName} LIKE {pattern}" : $"Convert({c.ColumnName}, 'System.String') LIKE {pattern}"));
    }

    private static string SafeName(string s) => string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    // ---------- export ----------

    /// <summary>View "Export": project report (HTML), all lists as CSV files, the parser result as JSON.</summary>
    private static UIElement ExportPanel(AnalysisContext ctx)
    {
        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 18), MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(new TextBlock { Text = "Export project information", FontSize = 20, FontWeight = FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = "Writes what the viewer read from the project to files. CSV format (separator, byte order mark): Settings › Export.",
            Foreground = Res("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 14) });

        root.Children.Add(Action(ctx, "", "Project report (HTML)",
            "One page with the key figures, the devices and all lists (blocks, tags, alarms, screens). Opens in any browser and can be printed as PDF.",
            "Save report…", () =>
            {
                var dlg = new SaveFileDialog { Title = "Save project report", Filter = "HTML page (*.html)|*.html",
                    FileName = SafeName(ctx.ProjectName) + "_report.html" };
                if (dlg.ShowDialog() != true) return null;
                File.WriteAllText(dlg.FileName, Report.Html(ctx), new UTF8Encoding(false));
                return dlg.FileName;
            }));
        root.Children.Add(Action(ctx, "", "All lists as CSV",
            "One CSV file per list with all rows: " + string.Join(", ", ctx.Lists.Where(l => l.Rows.Count > 0).Select(l => l.Title)) + ".",
            "Choose folder…", () =>
            {
                var dlg = new OpenFolderDialog { Title = "Folder for the CSV files" };
                if (dlg.ShowDialog() != true) return null;
                string? first = null;
                foreach (var l in ctx.Lists.Where(l => l.Rows.Count > 0))
                {
                    var file = Path.Combine(dlg.FolderName, $"{SafeName(ctx.ProjectName)}_{l.FileName}.csv");
                    Csv.Write(DetailViews.ToTable(l.Rows).DefaultView, file);
                    first ??= file;
                }
                return first;
            }));
        root.Children.Add(Action(ctx, "", "Parser result (JSON)",
            "The complete structure the parser read, the same as the command line output with --json. For own scripts and tools.",
            "Save JSON…", () =>
            {
                var dlg = new SaveFileDialog { Title = "Save parser result", Filter = "JSON file (*.json)|*.json",
                    FileName = SafeName(ctx.ProjectName) + ".json" };
                if (dlg.ShowDialog() != true) return null;
                File.WriteAllText(dlg.FileName, ctx.Json.ToJsonString(new JsonSerializerOptions
                    { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), new UTF8Encoding(false));
                return dlg.FileName;
            }));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>A card of the export view: icon, title, text, a button that writes a file (returns its path or null when
    /// cancelled), and the result line with "Show in Explorer".</summary>
    private static UIElement Action(AnalysisContext ctx, string glyph, string title, string text, string button, Func<string?> run)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 22,
            Foreground = Res("Accent"), Margin = new Thickness(0, 2, 16, 0), VerticalAlignment = VerticalAlignment.Top };
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        body.Children.Add(new TextBlock { Text = text, Foreground = Res("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        var result = new TextBlock { Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        body.Children.Add(result);
        var b = new Button { Content = button, Padding = new Thickness(14, 5, 14, 5), MinWidth = 130, Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center };
        b.Click += (_, _) =>
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var file = run();
                if (file == null) return;
                result.Inlines.Clear();
                result.Foreground = Res("Muted");
                result.Inlines.Add(new System.Windows.Documents.Run("Saved: " + file + "  "));
                var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Show in Explorer"));
                link.Click += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
                result.Inlines.Add(link);
                result.Visibility = Visibility.Visible;
                ctx.Status?.Invoke($"{title}: saved {file}");
            }
            catch (Exception ex)
            {
                result.Text = "Error: " + ex.Message;
                result.Foreground = Brushes.Firebrick;
                result.Visibility = Visibility.Visible;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        };
        Grid.SetColumn(body, 1);
        Grid.SetColumn(b, 2);
        grid.Children.Add(icon);
        grid.Children.Add(body);
        grid.Children.Add(b);
        return Card(grid, new Thickness(0, 0, 0, 10));
    }

    /// <summary>CSV files in the format of the settings (separator, byte order mark), with the column captions.</summary>
    public static class Csv
    {
        public static void Write(DataView view, string file)
        {
            var s = ViewerSettings.Current;
            var sep = string.IsNullOrEmpty(s.CsvSeparator) ? ";" : s.CsvSeparator;
            var cols = view.Table!.Columns.Cast<DataColumn>().ToList();
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(sep, cols.Select(c => Quote(DetailViews.Caption(c.Caption), sep))));
            foreach (DataRowView r in view)
                sb.AppendLine(string.Join(sep, cols.Select(c => Quote(Format(r.Row[c]), sep))));
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(s.CsvByteOrderMark));
        }

        public static string Format(object? v) => v switch
        {
            null or DBNull => "",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            _ => v.ToString() ?? "",
        };

        public static string Quote(string v, string sep) =>
            v.Contains(sep) || v.Contains('"') || v.Contains('\n') || v.Contains('\r') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    /// <summary>The project report as one HTML page.</summary>
    public static class Report
    {
        private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

        public static string Html(AnalysisContext ctx)
        {
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>").Append(E(ctx.ProjectName)).Append(" – project report</title>");
            sb.Append("""
                <style>
                body{font-family:"Segoe UI",system-ui,sans-serif;margin:32px;color:#1f2933;background:#fff}
                h1{margin:0 0 4px;font-size:26px}h2{margin:32px 0 8px;font-size:18px;border-bottom:2px solid #00808b;padding-bottom:4px}
                .muted{color:#5f6b7a}.tiles{display:flex;flex-wrap:wrap;gap:10px;margin:18px 0}
                .tile{border:1px solid #d5dae1;border-radius:6px;padding:10px 14px;min-width:130px}
                .tile b{display:block;font-size:24px;color:#00808b}
                table{border-collapse:collapse;font-size:12px;width:100%}th,td{border-bottom:1px solid #e4e7eb;padding:4px 8px;text-align:left;vertical-align:top}
                th{background:#eef1f5;position:sticky;top:0}tr:nth-child(even) td{background:#f7f9fb}
                nav a{margin-right:14px;color:#00808b}
                @media print{th{position:static}h2{break-after:avoid}}
                </style></head><body>
                """);
            sb.Append("<h1>").Append(E(ctx.ProjectName)).Append("</h1>");
            sb.Append("<div class=\"muted\">").Append(E(ctx.Path)).Append(" · report of ")
              .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
              .Append(" · OpenPEData Viewer ").Append(E(AppVersion.Label)).Append("</div>");
            sb.Append("<div class=\"tiles\">");
            foreach (var (label, value, _) in Figures(ctx))
                sb.Append("<div class=\"tile\"><b>").Append(value.ToString("N0", CultureInfo.InvariantCulture)).Append("</b>").Append(E(label)).Append("</div>");
            sb.Append("</div>");
            var lists = ctx.Lists.Where(l => l.Rows.Count > 0).ToList();
            sb.Append("<nav>");
            foreach (var l in lists) sb.Append("<a href=\"#").Append(l.Key).Append("\">").Append(E(l.Title)).Append("</a>");
            sb.Append("</nav>");
            foreach (var l in lists)
            {
                var t = DetailViews.ToTable(l.Rows);
                sb.Append("<h2 id=\"").Append(l.Key).Append("\">").Append(E(l.Title)).Append(" <span class=\"muted\">(")
                  .Append(t.Rows.Count.ToString(CultureInfo.InvariantCulture)).Append(")</span></h2><table><thead><tr>");
                foreach (DataColumn c in t.Columns) sb.Append("<th>").Append(E(DetailViews.Caption(c.Caption))).Append("</th>");
                sb.Append("</tr></thead><tbody>");
                foreach (DataRow r in t.Rows)
                {
                    sb.Append("<tr>");
                    foreach (DataColumn c in t.Columns) sb.Append("<td>").Append(E(Csv.Format(r[c]))).Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table>");
            }
            sb.Append("</body></html>");
            return sb.ToString();
        }
    }
}
