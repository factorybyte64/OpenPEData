using Velopack;

namespace OpenPEData.Viewer;

/// <summary>Entry point with the Velopack install/uninstall hooks (no update checks), then the WPF application.</summary>
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
