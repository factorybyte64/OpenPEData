using System.Windows;
using System.Windows.Threading;
using OpenPEData.Viewer.Updates;

namespace OpenPEData.Viewer;

/// <summary>Auto-updater of the main window: background check against the GitHub releases at startup and every
/// 12 hours (opt-out in "Updates…"), a notice button in the toolbar, and the dialog for any version up or down.</summary>
public partial class MainWindow
{
    private ReleaseEntry? _availableUpdate;
    private DispatcherTimer? _updateTimer;

    private void InitUpdates()
    {
        // no network checks for command line runs (--dump, --snapshot, --screen-png), development builds or when
        // turned off for the machine
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => a is "--dump" or "--snapshot" or "--screen-png")) return;
        if (AppUpdater.AutoCheckDisabledByEnvironment || !AppUpdater.IsInstalled) return;
        Loaded += async (_, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3));  // the project opens first
            await AutoCheckUpdatesAsync();
        };
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(12) };
        _updateTimer.Tick += async (_, _) => await AutoCheckUpdatesAsync();
        _updateTimer.Start();
    }

    private async Task AutoCheckUpdatesAsync()
    {
        var settings = UpdateSettings.Load();  // the dialog may have changed it
        if (!settings.AutoCheck) return;
        try
        {
            var releases = await GitHubReleases.FetchAsync(CancellationToken.None);
            ShowUpdateNotice(GitHubReleases.FindUpdate(releases, AppUpdater.CurrentVersion, settings.IncludePrereleases, settings.SkippedVersion));
        }
        catch
        {
            // offline, proxy, rate limit: silent; "Updates…" shows the error
        }
    }

    private void ShowUpdateNotice(ReleaseEntry? update)
    {
        _availableUpdate = update;
        UpdateNotice.Visibility = update == null ? Visibility.Collapsed : Visibility.Visible;
        if (update == null) return;
        UpdateNotice.Content = $"Update {update.Label} available";
        UpdateNotice.ToolTip = $"{update.Title}: show the release notes and install";
    }

    private void OnUpdates(object sender, RoutedEventArgs e)
    {
        var restartArgs = _path != null ? new[] { _path } : Array.Empty<string>();
        var dialog = new UpdateWindow(_availableUpdate?.Tag, restartArgs) { Owner = this };
        dialog.ShowDialog();
        if (dialog.Fetched) ShowUpdateNotice(dialog.AvailableUpdate);
    }
}
