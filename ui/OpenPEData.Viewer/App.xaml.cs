using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OpenPEData.Viewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Snapshots and sessions without a GPU desktop (Remote Desktop, locked console) need software rendering.
        if (e.Args.Contains("--snapshot") || Environment.GetEnvironmentVariable("OPENPEDATA_SOFTWARE_RENDER") == "1"
            || ViewerSettings.Current.SoftwareRendering)
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        base.OnStartup(e);
    }
}
