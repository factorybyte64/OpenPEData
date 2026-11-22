using System.IO;
using System.Text.Json;

namespace OpenPEData.Viewer;

/// <summary>General settings of the viewer, in %AppData%\OpenPEData\settings.json (next to the updater settings in
/// updates.json, outside the installation folder, so they survive updates and a reinstall).</summary>
public sealed class ViewerSettings
{
    /// <summary>Open the last project again when the viewer starts without a project argument.</summary>
    public bool ReopenLastProject { get; set; }

    /// <summary>Path of the last project that was read successfully.</summary>
    public string? LastProject { get; set; }

    /// <summary>Start value of "Like runtime (respect Visible)".</summary>
    public bool RuntimeVisibility { get; set; }

    /// <summary>Software rendering instead of the GPU (Remote Desktop, virtual machines); applies after a restart.</summary>
    public bool SoftwareRendering { get; set; }

    /// <summary>Field separator of CSV exports: ";" (Excel with a German or other comma-decimal locale), "," or "\t".</summary>
    public string CsvSeparator { get; set; } = ";";

    /// <summary>Write CSV files with a UTF-8 byte order mark, so that Excel recognizes umlauts and other characters.</summary>
    public bool CsvByteOrderMark { get; set; } = true;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenPEData");

    public static string FilePath => Path.Combine(Folder, "settings.json");

    /// <summary>The settings of this session (loaded once, replaced by <see cref="Save"/>).</summary>
    public static ViewerSettings Current { get; private set; } = Load();

    public static ViewerSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<ViewerSettings>(File.ReadAllText(FilePath), Json) ?? new ViewerSettings();
        }
        catch
        {
            // damaged file: defaults
        }
        return new ViewerSettings();
    }

    public void Save()
    {
        Current = this;
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // not writable: the settings apply to this session only
        }
    }

    public ViewerSettings Clone() => (ViewerSettings)MemberwiseClone();
}
