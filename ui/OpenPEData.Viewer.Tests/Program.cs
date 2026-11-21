using System.Globalization;
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
        AnalysisLists();
        Console.WriteLine($"OpenPEData viewer tests: {_checks - Failures.Count}/{_checks} checks passed");
        foreach (var f in Failures) Console.WriteLine("  FAIL " + f);
        return Failures.Count == 0 ? 0 : 1;
    }
}
