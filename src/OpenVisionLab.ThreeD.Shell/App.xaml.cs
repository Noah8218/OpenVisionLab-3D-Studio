using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Shell.Verification;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (ShellVerificationCommandRouter.IsVerificationRequest(e.Args))
        {
            ShellVerificationCommandRouter.Run(e.Args);
            return;
        }

        if (ShellSmokeCommandLineOptions.Parse(e.Args).SoftwareRendering)
        {
            OpenVisionThreeDViewerControl.UseSoftwareRenderingForProcess();
        }

        LiveCharts.Configure(settings => settings.UseDefaults());
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            OVLog.Flush();
            OVLog.Shutdown();
        }
        finally
        {
            base.OnExit(e);
        }
    }
}
