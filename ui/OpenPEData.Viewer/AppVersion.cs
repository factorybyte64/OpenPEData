using System.Reflection;

namespace OpenPEData.Viewer;

/// <summary>Display version from the assembly metadata (csproj Version), as the TiaMcpXE server shows it: "V0.2.3".</summary>
internal static class AppVersion
{
    public static string Label => "V" + Display;

    public static string Display
    {
        get
        {
            var assembly = typeof(AppVersion).Assembly;
            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');  // build metadata (commit) is not shown
                return plus >= 0 ? info[..plus] : info;
            }
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    /// <summary>Window title: "OpenPEData Viewer V0.2.3", with a project "&lt;project&gt; – OpenPEData Viewer V0.2.3".</summary>
    public static string Title(string? project = null) =>
        (string.IsNullOrEmpty(project) ? "" : project + " – ") + "OpenPEData Viewer " + Label;
}
