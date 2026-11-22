using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using OpenPEData.Viewer;
using OpenPEData.Viewer.Updates;
using Velopack;

namespace OpenPEData.Viewer.Tests;

/// <summary>Checks of the viewer's template expressions (docs\spec\hmi-rendering.md). Runs every check in both
/// display modes (TIA screen editor and "Like runtime") and reports all failures; exit code 1 when any failed.</summary>
public static class Program
{
    static readonly List<string> Failures = [];
    static int _checks;

    static void Eq(object? expected, object? actual, string what)
    {
        _checks++;
        if (!Equals(expected, actual))
            Failures.Add($"{what}: expected {expected ?? "null"}, got {actual ?? "null"}");
    }

    static object? Eval(string expr) => TemplateExpr.Eval(expr, new TemplateScope());

    static string Hex(object? v) => v is double d ? "0x" + ((uint)d).ToString("X8", CultureInfo.InvariantCulture) : v?.ToString() ?? "null";

    /// <summary>Converter.Darker/Lighter keep the alpha of the color in the editor and in the runtime: a button
    /// with a fully transparent BackColor stays transparent (both gradient stops), a half transparent one half
    /// transparent (TIA V21 screen editor, 2026-11-20).</summary>
    static void DarkerLighterKeepAlpha(string mode)
    {
        const uint transparent = 0x00F2F4FF, half = 0x80F2F4FF, opaque = 0xFFF2F4FF;
        Eq("0x00919299", Hex(Eval($"Converter.Darker({transparent}, 0.4)")), $"{mode}: Darker of a transparent color");
        Eq("0x80919299", Hex(Eval($"Converter.Darker({half}, 0.4)")), $"{mode}: Darker of a half transparent color");
        Eq("0xFF919299", Hex(Eval($"Converter.Darker({opaque}, 0.4)")), $"{mode}: Darker of an opaque color");
        Eq("0x00F7F8FF", Hex(Eval($"Converter.Lighter({transparent}, 0.4)")), $"{mode}: Lighter of a transparent color");
        Eq(0.0, Eval($"Converter.Alpha(Converter.Darker({transparent}, 0.4))"), $"{mode}: Alpha of the darker transparent color");
        Eq("rgba(145,146,153,0)", Eval($"Converter.RGBA(Converter.Darker({transparent}, 0.4))"), $"{mode}: RGBA of the darker transparent color");
        Eq("rgba(145,146,153,0.502)", Eval($"Converter.RGBA(Converter.Darker({half}, 0.4))"), $"{mode}: RGBA of the darker half transparent color");
    }

    /// <summary>Release list of the updater: drafts and non-version tags are left out, pre-releases recognized,
    /// newest version first; the offered update skips pre-releases (unless wanted), releases without update package
    /// and the skipped version, but offers a newer one than the skipped version.</summary>
    static void UpdaterReleases()
    {
        static string Rel(string tag, bool pre = false, bool draft = false, bool feed = true)
        {
            var assets = new System.Text.Json.Nodes.JsonArray();
            if (feed)
                assets.Add(new System.Text.Json.Nodes.JsonObject { ["name"] = "releases.win.json", ["browser_download_url"] = "https://example.invalid/f", ["url"] = "https://example.invalid/a", ["size"] = 10 });
            assets.Add(new System.Text.Json.Nodes.JsonObject { ["name"] = "OpenPEData-Setup.exe", ["browser_download_url"] = "https://example.invalid/s", ["url"] = "https://example.invalid/b", ["size"] = 20 });
            return new System.Text.Json.Nodes.JsonObject
            {
                ["tag_name"] = tag, ["name"] = "OpenPEData " + tag, ["draft"] = draft, ["prerelease"] = pre,
                ["published_at"] = "2026-11-20T10:00:00Z", ["body"] = "notes", ["html_url"] = "https://example.invalid/" + tag, ["assets"] = assets,
            }.ToJsonString();
        }
        var json = "[" + string.Join(",", Rel("v0.1.0", feed: false), Rel("v0.3.0", pre: true), Rel("v0.2.0"), Rel("v0.2.1"),
            Rel("v0.4.0", draft: true), Rel("nightly"), Rel("v0.3.1-beta.1")) + "]";
        var list = GitHubReleases.Parse(json);
        Eq("v0.3.1-beta.1,v0.3.0,v0.2.1,v0.2.0,v0.1.0", string.Join(",", list.Select(r => r.Tag)), "updater: releases newest first, no draft, no non-version tag");
        Eq(true, list.Single(r => r.Tag == "v0.3.1-beta.1").Prerelease, "updater: version label means pre-release");
        Eq(false, list.Single(r => r.Tag == "v0.1.0").CanInstall, "updater: release without releases.win.json is not installable");
        var current = SemanticVersion.Parse("0.2.0");
        Eq("v0.2.1", GitHubReleases.FindUpdate(list, current, false, null)?.Tag, "updater: newest stable update");
        Eq("v0.3.1-beta.1", GitHubReleases.FindUpdate(list, current, true, null)?.Tag, "updater: with pre-releases");
        Eq(null, GitHubReleases.FindUpdate(list, current, false, "0.2.1")?.Tag, "updater: skipped version not offered");
        Eq("v0.2.1", GitHubReleases.FindUpdate(list, SemanticVersion.Parse("0.1.0"), false, "0.2.0")?.Tag, "updater: newer than the skipped version is offered");
        Eq(null, GitHubReleases.FindUpdate(list, SemanticVersion.Parse("0.2.1"), false, null)?.Tag, "updater: no update on the newest version");
    }

    /// <summary>Renders the screens of the classic samples that have runtime screenshots and compares them with the
    /// screenshots (report only, no failure): render-compare/*.png and render-compare/summary.md. C01 (all objects and
    /// controls) and C02 (property variants): one screenshot per screen. C03 (dynamics): one screenshot per state S1–S4,
    /// rendered with the state's tag values (states/&lt;device&gt;.json) and the focus on the pressed state button. The
    /// TIA test projects are not part of the build; without them the comparison is skipped.</summary>
    static void ClassicRenderCompare()
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "samples", "C01_classic_hmi.zap21"))) root = Path.GetDirectoryName(root);
        if (root == null)
        {
            Console.WriteLine("classic render compare: sample C01 not found, skipped");
            return;
        }
        var outDir = Path.Combine(root, "render-compare");
        Directory.CreateDirectory(outDir);
        var lines = new List<string>();
        foreach (var sample in new[] { "C01_classic_hmi", "C02_classic_hmi_props", "C03_classic_hmi_dyn", "C03b_classic_hmi_alarms_trends_recipes", "C04_classic_hmi_styles", "C05_classic_hmi_graphics", "C06_classic_hmi_faceplates" })
        {
            var zap = Path.Combine(root, "samples", sample + ".zap21");
            var truth = Path.Combine(root, "samples", sample + "_truth");
            if (!File.Exists(zap)) continue;
            // C04: one folder of screenshots per project style (runtime/<style>/), rendered with that style's values
            var rt = Path.Combine(truth, "runtime");
            var styles = Directory.Exists(rt) && !Directory.EnumerateFiles(rt, "*.png").Any()
                ? Directory.GetDirectories(rt).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToList()
                : new List<string?> { null };
            foreach (var style in styles)
            {
                lines.Add("");
                lines.Add("## " + sample + (style != null ? $" ({style})" : ""));
                lines.Add("");
                lines.Add("| Screen | differing pixels |");
                lines.Add("|---|---:|");
                var total = new List<double>();
                ClassicCompareSample(zap, truth, Path.Combine(outDir, sample + (style != null ? "_" + style.Replace(' ', '_') : "")), lines, total, style);
                if (total.Count > 0)
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "| **mean of {0}** | **{1:P2}** |", total.Count, total.Average()));
            }
        }
        File.WriteAllLines(Path.Combine(outDir, "summary.md"), lines);
        Console.WriteLine("classic render compare:");
        foreach (var l in lines.Where(l => l.StartsWith("## ", StringComparison.Ordinal) || l.Contains("mean of", StringComparison.Ordinal)))
            Console.WriteLine("  " + l);
    }

    /// <summary>VB literal of a state file (C03 states/&lt;device&gt;.json) as a tag value.</summary>
    static object? VbValue(string vb)
    {
        vb = vb.Trim();
        if (vb.Length >= 2 && vb[0] == '"' && vb[^1] == '"') return vb[1..^1].Replace("\"\"", "\"");
        if (vb.Equals("True", StringComparison.OrdinalIgnoreCase)) return true;
        if (vb.Equals("False", StringComparison.OrdinalIgnoreCase)) return false;
        var m = System.Text.RegularExpressions.Regex.Match(vb, @"DateSerial\((\d+),\s*(\d+),\s*(\d+)\)\s*\+\s*TimeSerial\((\d+),\s*(\d+),\s*(\d+)\)");
        if (m.Success)
        {
            int G(int i) => int.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
            return new DateTime(G(1), G(2), G(3), G(4), G(5), G(6));
        }
        return double.TryParse(vb, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : vb;
    }

    /// <summary>The device style with the values of a project style (style.library, sample C04): colors, borders and
    /// the other values of the project style, the fonts of the device (the device copy scales them to the device).</summary>
    static JsonObject? StyleFor(JsonObject? device, string? name)
    {
        // the folder names write the style names with '_' (runtime/WinCC_Fresh)
        if (device == null || name == null || device["library"] is not JsonObject all) return device;
        if ((all[name] ?? all[name.Replace('_', ' ')]) is not JsonObject lib) return device;
        var o = (JsonObject)device.DeepClone();
        var values = new JsonObject();
        foreach (var (type, lv) in lib)
        {
            if (lv is not JsonObject l) continue;
            var v = (JsonObject)l.DeepClone();
            if (device["values"]?[type] is JsonObject dv)
                foreach (var (k, x) in dv)
                    if (k.Contains("Font", StringComparison.Ordinal)) v[k] = x?.DeepClone();
            values[type] = v;
        }
        o["values"] = values;
        o["name"] = name;
        return o;
    }

    /// <summary>Tag values of a recipe data record (element tag -> value).</summary>
    static Dictionary<string, object?>? RecordValues(JsonNode? hmi, string recipe, int number)
    {
        var r = (hmi?["recipes"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => x["name"]?.ToString() == recipe);
        var rec = (r?["records"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => x["number"]?.ToString() == number.ToString(CultureInfo.InvariantCulture));
        if (r == null || rec == null) return null;
        var o = new Dictionary<string, object?>();
        foreach (var e in (r["elements"] as JsonArray ?? []).OfType<JsonObject>())
            if (e["tag"]?.ToString() is { } tag && rec["values"]?[e["index"]?.ToString() ?? ""]?.ToString() is { } v)
                o[tag] = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : v;
        return o;
    }

    static void ClassicCompareSample(string zap, string truth, string outDir, List<string> lines, List<double> total, string? styleName = null)
    {
        var runtimeDir = styleName != null ? Path.Combine(truth, "runtime", styleName) : Path.Combine(truth, "runtime");
        Directory.CreateDirectory(outDir);
        var (project, res) = ParserRunner.OpenAsync(zap, CancellationToken.None).GetAwaiter().GetResult();
        using var _ = project;
        if (res.Json == null)
        {
            lines.Add("| parser returned no JSON | " + res.StdErr.Replace('|', '/').Replace('\n', ' ') + " |");
            return;
        }
        // project graphics (graphic views, graphic lists); C05 and C06 use the states of C03 (they are the C03 project plus their content)
        ScreenRenderer.SetGraphics(res.Json["project"]?["graphics"]);
        var statesDir = Directory.Exists(Path.Combine(truth, "states")) ? Path.Combine(truth, "states")
            : Path.Combine(Path.GetDirectoryName(truth)!, "C03_classic_hmi_dyn_truth", "states");
        void One(string dev, JsonObject s, string label, string refFile, string outFile, Action<Canvas>? overlay = null)
        {
            try
            {
                var canvas = ScreenRenderer.Render(s, (s["items"] as JsonArray ?? []).OfType<JsonNode>());
                overlay?.Invoke(canvas);
                var bmp = ScreenRenderer.ToBitmap(canvas);
                var share = ScreenRenderer.Compare(bmp, refFile, outFile);
                total.Add(share);
                lines.Add(string.Format(CultureInfo.InvariantCulture, "| {0}/{1} | {2:P2} |", dev, label, share));
            }
            catch (Exception ex)
            {
                lines.Add($"| {dev}/{label} | error: {ex.GetType().Name}: {ex.Message.Replace('|', '/').Replace('\n', ' ')} |");
            }
        }
        void Walk(JsonNode? n)
        {
            if (n is JsonArray a) { foreach (var c in a) Walk(c); return; }
            if (n is not JsonObject o) return;
            if (o["hmi"] is JsonArray hmis)
            {
                var dev = o["name"]?.ToString() ?? "?";
                foreach (var hmi in hmis)
                {
                    ScreenRenderer.SetTextLists(hmi?["textLists"]);
                    ScreenRenderer.SetGraphicLists(hmi?["graphicLists"]);
                    ScreenRenderer.SetRecipes(hmi?["recipes"]);
                    ScreenRenderer.SetAlarms(hmi);
                    var screens = (hmi?["screens"] as JsonArray ?? []).OfType<JsonObject>().ToList();
                    var style = StyleFor(hmi?["style"] as JsonObject, styleName);
                    foreach (var s in screens)
                        ScreenRenderer.SetStyle(s, style);
                    var statesFile = Path.Combine(statesDir, dev + ".json");
                    var states = File.Exists(statesFile) ? JsonNode.Parse(File.ReadAllText(statesFile)) as JsonObject : null;
                    foreach (var s in screens)
                    {
                        var name = s["name"]?.ToString() ?? "?";
                        var single = Path.Combine(runtimeDir, $"{dev}_{name}_runtime.png");
                        if (File.Exists(single))
                        {
                            // C03b: the runtime shows the values of data record 1 of Recipe_1, written last (README)
                            ClassicScreenRenderer.TagValues = truth.Contains("C03b", StringComparison.Ordinal) ? RecordValues(hmi, "Recipe_1", 1) : null;
                            try { One(dev, s, name, single, Path.Combine(outDir, $"{dev}_{name}_cmp.png")); }
                            finally { ClassicScreenRenderer.TagValues = null; }
                        }
                        if (states == null) continue;
                        // C05: pop-up screen at its configured position (200,120) and the top slide-in screen opened, in the
                        // state S4 (README of the sample)
                        if (states["4"] is JsonObject s4)
                        {
                            void Overlay(string suffix, Action<Canvas> add, string? focus)
                            {
                                var refFile = Path.Combine(runtimeDir, $"{dev}_{name}_{suffix}_runtime.png");
                                if (!File.Exists(refFile)) return;
                                ClassicScreenRenderer.TagValues = s4.ToDictionary(kv => kv.Key, kv => VbValue(kv.Value?.ToString() ?? ""));
                                ClassicScreenRenderer.FocusItem = focus;
                                try { One(dev, s, $"{name}_{suffix}", refFile, Path.Combine(outDir, $"{dev}_{name}_{suffix}_cmp.png"), add); }
                                finally
                                {
                                    ClassicScreenRenderer.TagValues = null;
                                    ClassicScreenRenderer.FocusItem = null;
                                }
                            }
                            JsonObject? Kind(string kind) => screens.FirstOrDefault(x => x["kind"]?.ToString() == kind && (kind != "SlideinScreen" || x["properties"]?["Activate"]?.ToString() == "true"));
                            IEnumerable<JsonNode> ItemsOf(JsonObject x) => (x["items"] as JsonArray ?? []).OfType<JsonNode>();
                            if (Kind("PopupScreen") is { } pop)
                                Overlay("Popup", c =>
                                {
                                    var pc = ScreenRenderer.Render(pop, ItemsOf(pop));
                                    Canvas.SetLeft(pc, 200);
                                    Canvas.SetTop(pc, 120);
                                    c.Children.Add(pc);
                                }, "Popup"); // the focus stays on the Popup button of the screen: the pop-up window has none
                            if (Kind("SlideinScreen") is { } slide)
                                Overlay("SlideIn", c => c.Children.Add(ClassicScreenRenderer.RenderSlideIn(slide, ItemsOf(slide), c.Width)), null);
                        }
                        foreach (var (sn, vals) in states)
                        {
                            var refFile = Path.Combine(runtimeDir, $"{dev}_{name}_S{sn}_runtime.png");
                            if (!File.Exists(refFile) || vals is not JsonObject vo) continue;
                            ClassicScreenRenderer.TagValues = vo.ToDictionary(kv => kv.Key, kv => VbValue(kv.Value?.ToString() ?? ""));
                            ClassicScreenRenderer.FocusItem = "S" + sn;
                            try { One(dev, s, $"{name}_S{sn}", refFile, Path.Combine(outDir, $"{dev}_{name}_S{sn}_cmp.png")); }
                            finally
                            {
                                ClassicScreenRenderer.TagValues = null;
                                ClassicScreenRenderer.FocusItem = null;
                            }
                        }
                    }
                }
            }
            foreach (var kv in o) if (kv.Key != "hmi") Walk(kv.Value);
        }
        Walk(res.Json);
    }

    /// <summary>Trend and function trend control without stored parts (sample 173): the default trend area is drawn
    /// with value axis labels 0..100, time or value labels below, and every tool bar button that fits carries a symbol.</summary>
    static void TrendControls(string mode)
    {
        var screen = JsonNode.Parse("""{"name":"Screen_1","width":800,"height":480}""")!.AsObject();
        var items = JsonNode.Parse("""
            [{"name":"Trend_1","type":"HmiTrendControl","left":10,"width":380,"height":300,"properties":{"Left":10,"Top":10,"Width":380,"Height":300}},
             {"name":"FTrend_1","type":"HmiFunctionTrendControl","left":410,"width":380,"height":300,"properties":{"Left":410,"Top":10,"Width":380,"Height":300}}]
            """)!.AsArray().Select(n => n!).ToList();
        var canvas = ScreenRenderer.Render(screen, items);
        var texts = new List<string>();
        var symbols = 0;
        void Walk(object o)
        {
            if (o is TextBlock tb) texts.Add(tb.Text);
            if (o is Viewbox { Child: Canvas { Children.Count: 1 } c } && c.Children[0] is System.Windows.Shapes.Path) symbols++;
            if (o is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d)) Walk(child);
        }
        Walk(canvas);
        Eq(true, texts.Contains("100") && texts.Contains("0"), $"{mode}: value axis labels 0 and 100");
        Eq(true, texts.Count(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d{1,2}/\d{1,2}/\d{4}\n\d{1,2}:\d{2}:\d{2} [AP]M$")) >= 2,
            $"{mode}: time axis labels date and time (M/d/yyyy, h:mm:ss tt)");
        Eq(true, texts.Contains("Legend:"), $"{mode}: legend heading");
        Eq(true, texts.Contains("50"), $"{mode}: function trend bottom axis label 50");
        Eq(14, symbols, $"{mode}: tool bar symbols (the 7 buttons that fit into 380 px per control)");
    }

    /// <summary>Analysis views: project-wide lists over all PLCs and HMIs (system data types with the PLC data types,
    /// system folder names readable, comment texts as own columns), the list filter over all columns, CSV quoting and
    /// the HTML report.</summary>
    static void AnalysisLists()
    {
        var json = JsonNode.Parse("""
            {"project":{"name":"P","devices":[
              {"name":"S1","plcs":[{"name":"PLC_1","typeName":"CPU","blocks":[
                 {"name":"Main","type":"OB","number":1,"language":"LAD","group":"","networks":[{}]},
                 {"name":"Program_Alarm","type":"FB","number":700,"language":"STL","group":"SystemBlocksFolder/ProgramResourcesFolder"},
                 {"name":"ALARM_BASE","type":"FBT","group":"SystemDataTypeFolder"}],
               "tagTables":[{"name":"T1","tags":[{"name":"Start","dataType":"Bool","address":"%I0.0","comment":{"en-US":"Start; \"button\""}}]}],
               "types":[{"name":"UDT_A","group":"Types","members":[{}]}]}]},
              {"name":"S2","hmi":[{"name":"HMI_1","tagTables":[{"name":"HT","tags":[{"name":"H1","dataType":"Int","loggingTags":[{}]}]}],
               "discreteAlarms":[{"name":"A1"}],"analogAlarms":[],"screens":[{"name":"Main","items":[{},{}]}]}]}],
             "plcAlarms":{"alarms":[{"name":"Alarm_1","plc":"PLC_1","block":"FB_A","texts":{"alarmText":{"en-US":"Pump"}}}]}},
             "statistics":{"objects":10}}
            """)!;
        var ctx = new AnalysisContext(json, @"C:\p.ap21", "P");
        int Rows(string key) => ctx.List(key).Rows.Count;
        Eq("2,2,2,1,1,1,1,0,1", string.Join(",", new[] { "devices", "blocks", "plcTypes", "plcTags", "hmiTags", "plcAlarms",
            "hmiDiscreteAlarms", "hmiAnalogAlarms", "screens" }.Select(Rows)), "analysis: row counts of the lists");
        var fb = ctx.List("blocks").Rows[1];
        Eq("System blocks/Program resources", fb["folder"], "analysis: system folder names in the block list");
        Eq(false, ctx.List("blocks").Rows[0].ContainsKey("networks"), "analysis: code structures are no columns");
        Eq("Start; \"button\"", ctx.List("plcTags").Rows[0].GetValueOrDefault("comment.en-US"), "analysis: comment text as column");
        Eq("Pump", ctx.List("plcAlarms").Rows[0].GetValueOrDefault("texts.alarmText.en-US"), "analysis: alarm text as column");
        Eq("2", ctx.List("screens").Rows[0]["objects"], "analysis: screen objects counted");

        var table = DetailViews.ToTable(ctx.List("plcTags").Rows);
        table.DefaultView.RowFilter = AnalysisViews.RowFilter(table, "i0.0");
        Eq(1, table.DefaultView.Count, "analysis: filter finds the address, case ignored");
        table.DefaultView.RowFilter = AnalysisViews.RowFilter(table, "x'[*%]");
        Eq(0, table.DefaultView.Count, "analysis: filter with quote, brackets and wildcards is valid");
        Eq("\"Start; \"\"button\"\"\"", AnalysisViews.Csv.Quote("Start; \"button\"", ";"), "analysis: CSV quoting");
        Eq("Data type|Tag table|PLC|Comment (en-US)|Alarm text (en-US)|Know how protected|OPC UA server",
            string.Join("|", new[] { "dataType", "tagTable", "plc", "comment.en-US", "texts.alarmText.en-US", "knowHowProtected", "opcUaServer" }
                .Select(DetailViews.Caption)), "analysis: column captions in plain words");
        var filter = MainWindow.TiaFileFilter();
        Eq(true, new[] { "*.ap21", "*.zap21", "*.al21", "*.zal21" }.All(filter.Split('|')[1].Split(';').Contains),
            "open dialog: projects, archives and libraries of the supported versions");
        var html = AnalysisViews.Report.Html(ctx);
        Eq(true, html.Contains("<h2 id=\"plcTags\">PLC tags") && html.Contains("Start; &quot;button&quot;"), "analysis: report has lists, HTML encoded");
    }

    [STAThread]
    public static int Main()
    {
        var saved = ScreenRenderer.RuntimeVisibility;
        try
        {
            foreach (var runtime in new[] { false, true })
            {
                ScreenRenderer.RuntimeVisibility = runtime;
                DarkerLighterKeepAlpha(runtime ? "runtime" : "editor");
                TrendControls(runtime ? "runtime" : "editor");
            }
        }
        finally
        {
            ScreenRenderer.RuntimeVisibility = saved;
        }
        UpdaterReleases();
        ClassicRenderCompare();
        AnalysisLists();
        Console.WriteLine($"OpenPEData viewer tests: {_checks - Failures.Count}/{_checks} checks passed");
        foreach (var f in Failures) Console.WriteLine("  FAIL " + f);
        return Failures.Count == 0 ? 0 : 1;
    }
}
