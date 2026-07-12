using System.Windows;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new Application();
        var viewerControl = new OpenVisionThreeDViewerControl();
        IOpenVisionThreeDViewerHost viewer = viewerControl;
        var window = new Window
        {
            Title = $"OpenVisionLab 3D Viewer Binary Host | API {viewer.HostApiVersion}",
            Width = 1280,
            Height = 800,
            MinWidth = 960,
            MinHeight = 640,
            Content = viewerControl
        };

        viewer.HostStateChanged += (_, args) =>
            window.Title = $"OpenVisionLab 3D Viewer Binary Host | API {viewer.HostApiVersion} | {args.State.ActiveEntity}";
        viewerControl.EnableSmokeFromCommandLine();
        application.Run(window);
    }
}
