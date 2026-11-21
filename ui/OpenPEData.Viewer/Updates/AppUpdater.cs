using Velopack;
using Velopack.Sources;

namespace OpenPEData.Viewer.Updates;

/// <summary>Velopack update/downgrade of the installed viewer from a chosen GitHub release.</summary>
public static class AppUpdater
{
    /// <summary>OPENPEDATA_NO_UPDATE_CHECK=1 turns the automatic check off (e.g. for machines without internet or
    /// managed installations); the dialog "Updates…" still works.</summary>
    public static bool AutoCheckDisabledByEnvironment =>
        Environment.GetEnvironmentVariable("OPENPEDATA_NO_UPDATE_CHECK") == "1";

    private static readonly Lazy<UpdateManager?> Default = new(() =>
    {
        try { return new UpdateManager(new GithubSource(GitHubReleases.RepoUrl, null, false, null)); }
        catch { return null; }
    });

    /// <summary>True when the viewer runs from a Velopack installation (setup); false for dotnet run / a build folder.</summary>
    public static bool IsInstalled => Default.Value?.IsInstalled == true;

    /// <summary>Installed version, or the assembly version in a development build.</summary>
    public static SemanticVersion CurrentVersion =>
        (IsInstalled ? Default.Value?.CurrentVersion : null) ?? SemanticVersion.Parse(AppVersion.Display);

    /// <summary>Downloads <paramref name="release"/> (newer or older than the installed version), then exits the
    /// viewer, installs it and starts it again with <paramref name="restartArgs"/>. Returns only on error.</summary>
    public static async Task InstallAsync(ReleaseEntry release, Action<int> progress, string[] restartArgs, CancellationToken ct)
    {
        if (!release.CanInstall)
            throw new InvalidOperationException($"Release {release.Label} has no update package ({GitHubReleases.FeedFile}). Install it with its setup from GitHub.");
        // the feed of exactly this release; downgrades allowed, so the chosen version is the target either way
        var manager = new UpdateManager(new PinnedGithubSource(release), new UpdateOptions { AllowVersionDowngrade = true });
        if (!manager.IsInstalled)
            throw new InvalidOperationException("This viewer was not installed with the setup (development build). Updates can only be installed into an installation.");
        var info = await manager.CheckForUpdatesAsync()
            ?? throw new InvalidOperationException($"{release.Label} is already installed.");
        if (info.TargetFullRelease.Version != release.Version)
            throw new InvalidOperationException($"The update package of {release.Tag} contains version {info.TargetFullRelease.Version}, not {release.Version}.");
        await manager.DownloadUpdatesAsync(info, progress, ct);
        manager.ApplyUpdatesAndRestart(info.TargetFullRelease, restartArgs);
    }

    /// <summary>GitHub source that only sees one release (the version picked by the user), so that
    /// <see cref="UpdateManager.CheckForUpdatesAsync"/> resolves to exactly that version, up or down.</summary>
    private sealed class PinnedGithubSource(ReleaseEntry release) : GithubSource(GitHubReleases.RepoUrl, null, true, null)
    {
        protected override Task<GithubRelease[]> GetReleases(bool includePrereleases) =>
            Task.FromResult(new[]
            {
                new GithubRelease
                {
                    Name = release.Title,
                    Prerelease = release.Prerelease,
                    PublishedAt = release.PublishedAt?.UtcDateTime,
                    Assets = release.Assets.Select(a => new GithubReleaseAsset
                    {
                        Name = a.Name,
                        Url = a.ApiUrl,
                        BrowserDownloadUrl = a.BrowserDownloadUrl,
                        ContentType = a.ContentType,
                    }).ToArray(),
                },
            });
    }
}
