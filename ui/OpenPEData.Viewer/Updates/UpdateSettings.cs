using System.IO;
using System.Text.Json;

namespace OpenPEData.Viewer.Updates;

/// <summary>Updater settings of the user, in %AppData%\OpenPEData\updates.json (outside the installation folder, so
/// they survive updates, downgrades and a reinstall).</summary>
public sealed class UpdateSettings
{
    /// <summary>Check GitHub for a newer version at startup and every 12 hours (default on, opt-out).</summary>
    public bool AutoCheck { get; set; } = true;

    /// <summary>Also offer pre-releases as updates.</summary>
    public bool IncludePrereleases { get; set; }

    /// <summary>Version the user chose to skip ("0.2.0"); a newer one is offered again.</summary>
    public string? SkippedVersion { get; set; }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenPEData", "updates.json");

    public static UpdateSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<UpdateSettings>(File.ReadAllText(FilePath), Json) ?? new UpdateSettings();
        }
        catch
        {
            // damaged file: defaults
        }
        return new UpdateSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // not writable: the setting applies to this session only
        }
    }
}
