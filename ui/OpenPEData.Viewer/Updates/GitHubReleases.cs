using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Velopack;

namespace OpenPEData.Viewer.Updates;

/// <summary>A file attached to a GitHub release.</summary>
public sealed record ReleaseAsset(string Name, string BrowserDownloadUrl, string ApiUrl, string ContentType, long Size);

/// <summary>One GitHub release of the viewer (tag vX.Y.Z).</summary>
public sealed record ReleaseEntry(
    string Tag,
    SemanticVersion Version,
    string Title,
    bool Prerelease,
    DateTimeOffset? PublishedAt,
    string Notes,
    string HtmlUrl,
    IReadOnlyList<ReleaseAsset> Assets)
{
    /// <summary>"V0.2.0", like <see cref="AppVersion.Label"/>.</summary>
    public string Label => "V" + Version.ToNormalizedString();

    /// <summary>True when the release carries the Velopack feed (releases.win.json) and can be installed by the
    /// updater. Releases built before the updater only have the setup and are installed with it.</summary>
    public bool CanInstall => Assets.Any(a => string.Equals(a.Name, GitHubReleases.FeedFile, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The releases of the repository on GitHub (REST API, no token: 60 requests per hour and IP).</summary>
public static class GitHubReleases
{
    public const string Owner = "factorybyte64";
    public const string Repo = "OpenPEData";
    public static string RepoUrl => $"https://github.com/{Owner}/{Repo}";
    public static string ReleasesPage => RepoUrl + "/releases";

    /// <summary>Velopack feed of the channel "win" (vpk pack on Windows), one per release.</summary>
    public const string FeedFile = "releases.win.json";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenPEData-Viewer", AppVersion.Display));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return http;
    }

    /// <summary>Release list URL. OPENPEDATA_UPDATE_API replaces it for tests (a local HTTP server serving a JSON file in
    /// the format of the GitHub API whose asset URLs point to the same server).</summary>
    public static string ApiUrl =>
        Environment.GetEnvironmentVariable("OPENPEDATA_UPDATE_API") is { Length: > 0 } url
            ? url
            : $"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page=100";

    /// <summary>All published releases, newest version first.</summary>
    public static async Task<IReadOnlyList<ReleaseEntry>> FetchAsync(CancellationToken ct)
    {
        using var response = await Http.GetAsync(ApiUrl, ct);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("GitHub refused the request (rate limit of 60 requests per hour reached?). Please try again later.");
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Releases from the JSON of GET /repos/{owner}/{repo}/releases; drafts and tags that are no version
    /// are left out. Pre-release: flagged on GitHub or a version with a label (v1.2.0-beta.1).</summary>
    public static IReadOnlyList<ReleaseEntry> Parse(string json)
    {
        var list = new List<ReleaseEntry>();
        if (JsonNode.Parse(json) is not JsonArray releases) return list;
        foreach (var r in releases.OfType<JsonObject>())
        {
            if (r["draft"]?.GetValue<bool>() == true) continue;
            var tag = r["tag_name"]?.GetValue<string>() ?? "";
            if (!SemanticVersion.TryParse(tag.TrimStart('v', 'V'), out var version) || version == null) continue;
            DateTimeOffset? published = DateTimeOffset.TryParse(r["published_at"]?.GetValue<string>(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var p) ? p : null;
            var assets = (r["assets"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
                .Select(a => new ReleaseAsset(
                    a["name"]?.GetValue<string>() ?? "",
                    a["browser_download_url"]?.GetValue<string>() ?? "",
                    a["url"]?.GetValue<string>() ?? "",
                    a["content_type"]?.GetValue<string>() ?? "",
                    a["size"]?.GetValue<long>() ?? 0))
                .ToList();
            list.Add(new ReleaseEntry(
                tag,
                version,
                r["name"]?.GetValue<string>() is { Length: > 0 } name ? name : tag,
                r["prerelease"]?.GetValue<bool>() == true || version.IsPrerelease,
                published,
                r["body"]?.GetValue<string>() ?? "",
                r["html_url"]?.GetValue<string>() ?? ReleasesPage,
                assets));
        }
        list.Sort((a, b) => b.Version.CompareTo(a.Version));
        return list;
    }

    /// <summary>The newest installable release above <paramref name="current"/>; pre-releases only when wanted, and
    /// not the version the user chose to skip (a newer one than that is offered again).</summary>
    public static ReleaseEntry? FindUpdate(IEnumerable<ReleaseEntry> releases, SemanticVersion current, bool includePrereleases, string? skippedVersion) =>
        releases
            .Where(r => r.CanInstall && (includePrereleases || !r.Prerelease) && r.Version > current)
            .Where(r => !string.Equals(r.Version.ToNormalizedString(), skippedVersion, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Version)
            .FirstOrDefault();
}
