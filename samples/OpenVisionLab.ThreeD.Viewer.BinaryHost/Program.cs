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
        if (args.Any(argument => string.Equals(
                argument,
                "--verify-consumer-boundaries",
                StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = ViewerConsumerBoundaryVerification.Run();
            return;
        }

        var consumerLifecycleReportPath = GetArgumentValue(args, "--consumer-lifecycle-report");
        if (consumerLifecycleReportPath is not null)
        {
            if (args.Any(argument => string.Equals(
                    argument,
                    "--smoke-software-rendering",
                    StringComparison.OrdinalIgnoreCase)))
            {
                OpenVisionThreeDViewerControl.UseSoftwareRenderingForProcess();
            }

            try
            {
                Environment.ExitCode = ViewerConsumerLifecycleRunner.Run(
                    ViewerConsumerLifecycleOptions.Parse(args, consumerLifecycleReportPath));
            }
            catch (ArgumentException exception)
            {
                Console.Error.WriteLine(exception.Message);
                Environment.ExitCode = 2;
            }

            return;
        }

        var hostApiReportPath = GetArgumentValue(args, "--host-api-report");
        var hostApiRecipePath = GetArgumentValue(args, "--host-api-save-recipe");
        if (args.Any(argument => string.Equals(
                argument,
                "--smoke-software-rendering",
                StringComparison.OrdinalIgnoreCase)))
        {
            OpenVisionThreeDViewerControl.UseSoftwareRenderingForProcess();
        }

        var application = new Application();
        var viewerControl = new OpenVisionThreeDViewerControl();
        IOpenVisionThreeDViewerHost viewer = viewerControl;
        var hostApiReportCoordinator = hostApiReportPath is null
            ? null
            : new ViewerConsumerHostApiReportCoordinator(hostApiReportPath, viewer);
        var window = new Window
        {
            Title = $"OpenVisionLab 3D Viewer Binary Host | API {viewer.HostApiVersion}",
            Width = 1280,
            Height = 800,
            MinWidth = 960,
            MinHeight = 640,
            Content = viewerControl
        };

        EventHandler<ViewerHostStateChangedEventArgs> hostStateChangedHandler = (_, eventArgs) =>
        {
            hostApiReportCoordinator?.ObserveStateChanged(eventArgs);
            window.Title = $"OpenVisionLab 3D Viewer Binary Host | API {viewer.HostApiVersion} | {eventArgs.State.ActiveEntity}";
        };
        viewer.HostStateChanged += hostStateChangedHandler;
        viewerControl.EnableSmokeFromCommandLine();
        var recipeSaved = true;
        if (hostApiReportPath is not null)
        {
            viewer.ResetView();
            viewer.FitAll();
            viewer.FitSelection();
            recipeSaved = hostApiRecipePath is not null && viewer.SaveRecipe(hostApiRecipePath);
        }

        var exitCode = 0;
        try
        {
            exitCode = application.Run(window);
        }
        finally
        {
            viewer.HostStateChanged -= hostStateChangedHandler;
            // The binary consumer owns the concrete control lifetime. The
            // compatibility host interface remains unchanged for existing
            // consumers that only need commands and state.
            viewerControl.Dispose();
        }

        if (hostApiReportCoordinator is not null)
        {
            hostApiReportCoordinator.WriteReport(recipeSaved, hostApiRecipePath);
        }

        Environment.ExitCode = recipeSaved ? exitCode : 1;
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
