using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Velopack;

namespace OpenPEData.Viewer.Updates;

/// <summary>Dialog "Updates…": settings of the automatic check, the list of all GitHub releases, and installing any
/// of them (update or downgrade) with Velopack.</summary>
public partial class UpdateWindow : Window
{
    /// <summary>One row of the release list.</summary>
    public sealed class ReleaseRow(ReleaseEntry release, string status)
    {
        public ReleaseEntry Release { get; } = release;
        public string Version => Release.Label;
        public string Published => Release.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
        public string Kind => Release.Prerelease ? "Pre-release" : "Release";
        public string Status { get; } = status;
    }

    private readonly string? _preselectTag;
    private readonly string[] _restartArgs;
    private readonly SemanticVersion _current = AppUpdater.CurrentVersion;
    private readonly bool _installed = AppUpdater.IsInstalled;
    private UpdateSettings _settings = UpdateSettings.Load();
    private IReadOnlyList<ReleaseEntry>? _releases;
    private CancellationTokenSource? _installCts;
    private bool _initializing = true;
    private DateTime _checkedAt;

    /// <summary>True after the release list was read from GitHub in this dialog.</summary>
    public bool Fetched => _releases != null;

    /// <summary>The update to offer in the main window after the dialog (null: none, or not fetched).</summary>
    public ReleaseEntry? AvailableUpdate => _releases == null ? null :
        GitHubReleases.FindUpdate(_releases, _current, _settings.IncludePrereleases, _settings.SkippedVersion);

    /// <param name="preselectTag">Release to select first (the update shown in the main window).</param>
    /// <param name="restartArgs">Arguments for the viewer after the update (the open project).</param>
    public UpdateWindow(string? preselectTag, string[] restartArgs)
    {
        InitializeComponent();
        _preselectTag = preselectTag;
        _restartArgs = restartArgs;
        InstalledText.Text = "Installed: V" + _current.ToNormalizedString();
        if (!_installed)
        {
            InstallHint.Text = "Development build (not installed with the setup): the versions are listed, but can only be installed into an installation.";
            InstallHint.Visibility = Visibility.Visible;
        }
        else if (AppUpdater.AutoCheckDisabledByEnvironment)
        {
            InstallHint.Text = "The automatic check is turned off by the environment variable OPENPEDATA_NO_UPDATE_CHECK=1.";
            InstallHint.Visibility = Visibility.Visible;
        }
        AutoCheckBox.IsChecked = _settings.AutoCheck;
        PrereleaseBox.IsChecked = _settings.IncludePrereleases;
        _initializing = false;
        Loaded += async (_, _) => await CheckAsync();
        Closing += (_, _) => _installCts?.Cancel();
    }

    private async Task CheckAsync()
    {
        CheckButton.IsEnabled = false;
        CheckStatus.Text = "Reading the releases from GitHub …";
        try
        {
            _releases = await GitHubReleases.FetchAsync(CancellationToken.None);
            _checkedAt = DateTime.Now;
            ShowCheckResult();
            Refresh(_preselectTag ?? AvailableUpdate?.Tag);
        }
        catch (Exception ex)
        {
            CheckStatus.Text = "Check failed: " + ex.Message;
        }
        finally
        {
            CheckButton.IsEnabled = _installCts == null;
        }
    }

    /// <summary>Result line of the last check; follows "Skip this version" and the pre-release setting.</summary>
    private void ShowCheckResult()
    {
        if (_releases == null) return;
        var update = AvailableUpdate;
        CheckStatus.Text = $"Checked at {_checkedAt:HH:mm}: " +
            (update != null ? $"{update.Label} is available." : "you have the newest version.");
    }

    private void Refresh(string? selectTag)
    {
        if (_releases == null) return;
        selectTag ??= (ReleaseList.SelectedItem as ReleaseRow)?.Release.Tag;
        var rows = _releases
            .Where(r => _settings.IncludePrereleases || !r.Prerelease || r.Version == _current)
            .Select(r => new ReleaseRow(r, StatusOf(r)))
            .ToList();
        ReleaseList.ItemsSource = rows;
        ReleaseList.SelectedItem = rows.FirstOrDefault(r => r.Release.Tag == selectTag)
            ?? rows.FirstOrDefault(r => r.Release.Version == _current)
            ?? rows.FirstOrDefault();
        if (ReleaseList.SelectedItem != null) ReleaseList.ScrollIntoView(ReleaseList.SelectedItem);
        ShowCheckResult();
        UpdateActions();
    }

    private string StatusOf(ReleaseEntry r)
    {
        if (r.Version == _current) return "installed";
        if (!r.CanInstall) return "setup only (released before the updater)";
        var skipped = string.Equals(r.Version.ToNormalizedString(), _settings.SkippedVersion, StringComparison.OrdinalIgnoreCase);
        return (r.Version > _current ? "newer: update" : "older: downgrade") + (skipped ? " (skipped)" : "");
    }

    private void OnReleaseSelected(object sender, SelectionChangedEventArgs e)
    {
        var row = ReleaseList.SelectedItem as ReleaseRow;
        NotesBox.Text = row == null ? "" : string.IsNullOrWhiteSpace(row.Release.Notes) ? "(no release notes)" : row.Release.Notes.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        UpdateActions();
    }

    private void UpdateActions()
    {
        var r = (ReleaseList.SelectedItem as ReleaseRow)?.Release;
        var busy = _installCts != null;
        InstallButton.Content = r == null || r.Version == _current ? "Install"
            : r.Version > _current ? $"Update to {r.Label}" : $"Downgrade to {r.Label}";
        InstallButton.IsEnabled = !busy && _installed && r != null && r.CanInstall && r.Version != _current;
        InstallButton.ToolTip = !_installed ? "Only possible in a viewer installed with the setup"
            : r is { CanInstall: false } ? "This release has no update package; install it with its setup from GitHub"
            : null;
        // "Skip" for the version that would be offered automatically
        var offered = _releases == null ? null : GitHubReleases.FindUpdate(_releases, _current, _settings.IncludePrereleases, null);
        SkipButton.Visibility = r != null && offered != null && r.Tag == offered.Tag && !busy ? Visibility.Visible : Visibility.Collapsed;
        SkipButton.IsEnabled = r != null && !string.Equals(r.Version.ToNormalizedString(), _settings.SkippedVersion, StringComparison.OrdinalIgnoreCase);
        ReleaseLink.IsEnabled = r != null;
        PrereleaseBox.IsEnabled = !busy;
    }

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if ((ReleaseList.SelectedItem as ReleaseRow)?.Release is not { } r) return;
        if (r.Version < _current && MessageBox.Show(this,
                $"Downgrade from V{_current.ToNormalizedString()} to {r.Label}?\n\n" +
                "The viewer closes, installs the older version and starts again. Versions released before the updater " +
                "cannot update themselves: to come back, run the current setup from the GitHub releases page.",
                "Downgrade – OpenPEData Viewer", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        _installCts = new CancellationTokenSource();
        Progress.Value = 0;
        Progress.Visibility = Visibility.Visible;
        CheckButton.IsEnabled = false;
        UpdateActions();
        CheckStatus.Text = $"Downloading {r.Label} …";
        try
        {
            // returns only on error: on success Velopack exits the viewer, installs and restarts it
            await AppUpdater.InstallAsync(r, p => Dispatcher.InvokeAsync(() => { Progress.Value = p; }), _restartArgs, _installCts.Token);
        }
        catch (OperationCanceledException)
        {
            CheckStatus.Text = "Download cancelled.";
        }
        catch (Exception ex)
        {
            CheckStatus.Text = "Installation failed.";
            MessageBox.Show(this, ex.Message, "Updates – OpenPEData Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _installCts?.Dispose();
            _installCts = null;
            Progress.Visibility = Visibility.Collapsed;
            CheckButton.IsEnabled = true;
            UpdateActions();
        }
    }

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        if ((ReleaseList.SelectedItem as ReleaseRow)?.Release is not { } r) return;
        _settings.SkippedVersion = r.Version.ToNormalizedString();
        _settings.Save();
        Refresh(r.Tag);
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings = UpdateSettings.Load();
        _settings.AutoCheck = AutoCheckBox.IsChecked == true;
        _settings.Save();
    }

    private void OnPrereleaseChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings = UpdateSettings.Load();
        _settings.IncludePrereleases = PrereleaseBox.IsChecked == true;
        _settings.Save();
        Refresh(null);
    }

    private async void OnCheck(object sender, RoutedEventArgs e) => await CheckAsync();

    private void OnOpenReleasePage(object sender, RoutedEventArgs e)
    {
        var url = (ReleaseList.SelectedItem as ReleaseRow)?.Release.HtmlUrl ?? GitHubReleases.ReleasesPage;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* no browser */ }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
