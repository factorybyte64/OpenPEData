using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using OpenPEData.Viewer;

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
        Console.WriteLine($"OpenPEData viewer tests: {_checks - Failures.Count}/{_checks} checks passed");
        foreach (var f in Failures) Console.WriteLine("  FAIL " + f);
        return Failures.Count == 0 ? 0 : 1;
    }
}
