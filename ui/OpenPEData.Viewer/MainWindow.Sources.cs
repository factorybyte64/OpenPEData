using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace OpenPEData.Viewer;

/// <summary>"Export sources…" in the toolbar: every block and PLC data type of the open project as source files into a
/// folder, as TIA Portal "Generate source from blocks" (TiaProject.ExportSources): .scl, .awl, .db, .udt, LAD/FBD/GRAPH
/// as SimaticML .xml. The button is added next to Reload and is enabled together with it.</summary>
public partial class MainWindow
{
    private Button? _sourcesButton;

    private void InitSourceExport()
    {
        if (ReloadButton.Parent is not Panel panel) return;
        _sourcesButton = new Button
        {
            Content = "Export sources…",
            ToolTip = "Write all blocks and PLC data types as source files into a folder, like TIA \"Generate source from blocks\" " +
                      "(.scl, .awl, .db, .udt; LAD/FBD/GRAPH as SimaticML .xml)",
            IsEnabled = ReloadButton.IsEnabled,
        };
        if (TryFindResource("ToolButton") is Style st) _sourcesButton.Style = st;
        _sourcesButton.Click += async (_, _) => await ExportSourcesAsync();
        panel.Children.Insert(panel.Children.IndexOf(ReloadButton) + 1, _sourcesButton);
        ReloadButton.IsEnabledChanged += (_, _) => _sourcesButton.IsEnabled = ReloadButton.IsEnabled;
    }

    private async Task ExportSourcesAsync()
    {
        var project = _project;
        if (project == null) return;
        var dlg = new OpenFolderDialog { Title = "Folder for the source files" };
        if (_path != null)
            dlg.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (dlg.ShowDialog(this) != true) return;
        var folder = Path.Combine(dlg.FolderName, Path.GetFileNameWithoutExtension(project.Path.TrimEnd('\\', '/')) + " sources");
        SetBusy(true, "Exporting sources…");
        try
        {
            var r = await Task.Run(() => project.ExportSources(folder));
            var incomplete = r.Incomplete.Count();
            SetBusy(false, $"{r.Sources.Count} source files in {r.Folder}");
            var text = $"{r.Sources.Count} source files written to\n{r.Folder}";
            if (r.Skipped.Count > 0)
                text += $"\n\n{r.Skipped.Count} blocks skipped (know-how protected or system blocks; TIA generates no source for them either).";
            if (incomplete > 0)
                text += $"\n\n{incomplete} sources are incomplete:\n" + string.Join("\n",
                    r.Incomplete.Take(15).Select(s => $"{s.RelativePath}: {string.Join("; ", s.Notes)}")) + (incomplete > 15 ? "\n…" : "");
            text += "\n\nOpen the folder?";
            if (MessageBox.Show(this, text, "Export sources", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{r.Folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetBusy(false, "Export failed");
            ShowError("The sources could not be exported.", ex.ToString());
        }
    }
}
