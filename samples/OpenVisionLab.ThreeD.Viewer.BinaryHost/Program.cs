using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var args = Environment.GetCommandLineArgs();
        var hostApiReportPath = GetArgumentValue(args, "--host-api-report");
        var hostApiRecipePath = GetArgumentValue(args, "--host-api-save-recipe");
        var application = new Application();
        var viewerControl = new OpenVisionThreeDViewerControl();
        IOpenVisionThreeDViewerHost viewer = viewerControl;
        var hostEventCount = 0;
        string? lastHostProperty = null;
        var window = new Window
        {
            Title = $"OpenVisionLab 3D Viewer Binary Host | API {viewer.HostApiVersion}",
            Width = 1280,
            Height = 800,
            MinWidth = 960,
            MinHeight = 640,
            Content = viewerControl
        };

        viewer.HostStateChanged += (_, eventArgs) =>
        {
            hostEventCount++;
            lastHostProperty = eventArgs.PropertyName;
            window.Title = $"OpenVisionLab 3D Viewer Binary Host | API {viewer.HostApiVersion} | {eventArgs.State.ActiveEntity}";
        };
        viewerControl.EnableSmokeFromCommandLine();
        var recipeSaved = true;
        if (hostApiReportPath is not null)
        {
            viewer.ResetView();
            viewer.FitAll();
            viewer.FitSelection();
            recipeSaved = hostApiRecipePath is not null && viewer.SaveRecipe(hostApiRecipePath);
        }

        var exitCode = application.Run(window);

        if (hostApiReportPath is not null)
        {
            var fullReportPath = Path.GetFullPath(hostApiReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            var state = viewer.HostState;
            File.WriteAllLines(fullReportPath,
            [
                $"HostApi|version={viewer.HostApiVersion}",
                $"HostState|activeEntity={state.ActiveEntity}|selectionMode={state.SelectionMode}|viewerStatus={state.ViewerStatus}",
                $"HostEvents|count={hostEventCount}|lastProperty={lastHostProperty ?? "(none)"}",
                $"HostCommands|invoked=ResetView,FitAll,FitSelection|saveRecipe={recipeSaved}|recipePath={hostApiRecipePath ?? "(not requested)"}"
            ]);
        }

        Environment.ExitCode = recipeSaved ? exitCode : 1;
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
