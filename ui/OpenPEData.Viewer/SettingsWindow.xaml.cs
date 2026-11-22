using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenPEData.Viewer.Updates;

namespace OpenPEData.Viewer;

/// <summary>Dialog "Settings": the persistent settings of the viewer in categories (General, Display, Export, Updates,
/// About). Saved on OK: <see cref="ViewerSettings"/> in settings.json, the updater in updates.json.</summary>
public partial class SettingsWindow : Window
{
    private readonly UpdateSettings _updates = UpdateSettings.Load();
    private readonly ViewerSettings _settings = ViewerSettings.Current.Clone();
    private readonly (string Glyph, string Title, FrameworkElement Page)[] _pages;

    /// <summary>The user clicked "Show versions and install…": the settings are saved and the caller opens the
    /// update dialog.</summary>
    public bool ShowUpdatesRequested { get; private set; }

    /// <summary>Software rendering changed: the caller tells the user that it applies after a restart.</summary>
    public bool RestartNeeded { get; private set; }

    public SettingsWindow(string? page = null)
    {
        InitializeComponent();
        _pages =
        [
            ("", "General", PageGeneral),
            ("", "Display", PageDisplay),
            ("", "Export", PageExport),
            ("", "Updates", PageUpdates),
            ("", "About", PageAbout),
        ];
        foreach (var (glyph, title, _) in _pages)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15,
                Foreground = (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0),
            });
            row.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
            Nav.Items.Add(new ListBoxItem { Content = row, Tag = title });
        }

        ReopenBox.IsChecked = _settings.ReopenLastProject;
        LastProjectText.Text = string.IsNullOrEmpty(_settings.LastProject) ? "No project opened yet." : "Last project: " + _settings.LastProject;
        RuntimeVisibilityBox.IsChecked = _settings.RuntimeVisibility;
        SoftwareRenderBox.IsChecked = _settings.SoftwareRendering;
        SeparatorBox.SelectedItem = SeparatorBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == _settings.CsvSeparator)
                                    ?? SeparatorBox.Items[0];
        BomBox.IsChecked = _settings.CsvByteOrderMark;

        AutoCheckBox.IsChecked = _updates.AutoCheck;
        PrereleaseBox.IsChecked = _updates.IncludePrereleases;
        UpdateHint.Text = AppUpdater.AutoCheckDisabledByEnvironment
            ? "The automatic check is turned off for this machine by OPENPEDATA_NO_UPDATE_CHECK=1."
            : !AppUpdater.IsInstalled
                ? "Development build (not installed with the setup): the automatic check only runs in an installation."
                : $"Installed version: {AppVersion.Label}.";

        VersionText.Text = AppVersion.Label;
        ParserText.Text = ParserRunner.BackendDescription();
        FolderText.Text = ViewerSettings.Folder;
        FooterHint.Text = "Saved in " + ViewerSettings.Folder;

        Nav.SelectedIndex = Math.Max(0, Array.FindIndex(_pages, p => string.Equals(p.Title, page, StringComparison.OrdinalIgnoreCase)));
    }

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        var title = (Nav.SelectedItem as ListBoxItem)?.Tag as string;
        foreach (var (_, t, p) in _pages) p.Visibility = t == title ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save()
    {
        _settings.ReopenLastProject = ReopenBox.IsChecked == true;
        _settings.RuntimeVisibility = RuntimeVisibilityBox.IsChecked == true;
        RestartNeeded = _settings.SoftwareRendering != (SoftwareRenderBox.IsChecked == true);
        _settings.SoftwareRendering = SoftwareRenderBox.IsChecked == true;
        _settings.CsvSeparator = (SeparatorBox.SelectedItem as ComboBoxItem)?.Tag as string ?? ";";
        _settings.CsvByteOrderMark = BomBox.IsChecked == true;
        _settings.Save();

        _updates.AutoCheck = AutoCheckBox.IsChecked == true;
        _updates.IncludePrereleases = PrereleaseBox.IsChecked == true;
        _updates.Save();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Save();
        DialogResult = true;
    }

    private void OnShowUpdates(object sender, RoutedEventArgs e)
    {
        Save();
        ShowUpdatesRequested = true;
        DialogResult = true;
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(ViewerSettings.Folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ViewerSettings.Folder}\"") { UseShellExecute = true });
    }

    private void OnOpenProjectPage(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(GitHubReleases.RepoUrl) { UseShellExecute = true });
}
