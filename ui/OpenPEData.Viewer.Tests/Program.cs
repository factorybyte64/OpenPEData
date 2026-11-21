using System.Globalization;
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
        UpdaterReleases();
        Console.WriteLine($"OpenPEData viewer tests: {_checks - Failures.Count}/{_checks} checks passed");
        foreach (var f in Failures) Console.WriteLine("  FAIL " + f);
        return Failures.Count == 0 ? 0 : 1;
    }
}
