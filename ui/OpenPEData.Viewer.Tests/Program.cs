using System.Globalization;
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
