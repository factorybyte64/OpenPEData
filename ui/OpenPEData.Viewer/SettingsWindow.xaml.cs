using System.Windows;
using OpenPEData.Viewer.Updates;

namespace OpenPEData.Viewer;

/// <summary>Dialog "Settings…": the viewer's persistent settings (for now the auto-updater), saved on OK in
/// %AppData%\OpenPEData\updates.json.</summary>
public partial class SettingsWindow : Window
{
    private readonly UpdateSettings _updates = UpdateSettings.Load();

    public SettingsWindow()
    {
        InitializeComponent();
        AutoCheckBox.IsChecked = _updates.AutoCheck;
        PrereleaseBox.IsChecked = _updates.IncludePrereleases;
        UpdateHint.Text = AppUpdater.AutoCheckDisabledByEnvironment
            ? "The automatic check is turned off for this machine by OPENPEDATA_NO_UPDATE_CHECK=1."
            : !AppUpdater.IsInstalled
                ? "Development build (not installed with the setup): the automatic check only runs in an installation."
                : "Updates are only installed after a click. Any version can be installed with \"Updates…\".";
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        _updates.AutoCheck = AutoCheckBox.IsChecked == true;
        _updates.IncludePrereleases = PrereleaseBox.IsChecked == true;
        _updates.Save();
        DialogResult = true;
    }
}
