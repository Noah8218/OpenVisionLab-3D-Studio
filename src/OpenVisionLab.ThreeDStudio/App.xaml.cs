using System.Configuration;
using System.Data;
using System.Windows;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeDStudio;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains(
                "--smoke-software-rendering",
                StringComparer.OrdinalIgnoreCase))
        {
            OpenVisionThreeDViewerControl.UseSoftwareRenderingForProcess();
        }

        base.OnStartup(e);
    }
}
