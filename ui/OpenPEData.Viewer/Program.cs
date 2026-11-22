using Velopack;

namespace OpenPEData.Viewer;

/// <summary>Entry point with the Velopack install/uninstall hooks, then the WPF application. Updates are not applied
/// on startup: the updater (Updates\, dialog "Updates…") downloads a chosen version and restarts into it.</summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
