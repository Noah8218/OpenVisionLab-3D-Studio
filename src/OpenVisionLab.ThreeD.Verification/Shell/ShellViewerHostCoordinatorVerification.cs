using System.IO;
using System.Windows.Threading;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell;

internal static class ShellViewerHostCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Shell Viewer host coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        var dispatcher = Dispatcher.CurrentDispatcher;
        var mode = ShellWorkspaceMode.Expert;
        var closed = false;
        var expertShown = 0;
        var workbenchShown = 0;
        var layoutUpdates = 0;
        var visibleFrames = 0;
        var callbacks = new ShellViewerHostCoordinatorCallbacks
        {
            GetWorkspaceMode = () => mode,
            IsTeachingCaptureActive = () => true,
            CancelTeachingCapture = () => { },
            ShowWorkbenchViewer = () => workbenchShown++,
            ShowExpertViewer = () => expertShown++,
            ShowTaskViewer = () => { },
            ClearViewerHosts = () => { },
            IsExpertViewerAttached = () => mode == ShellWorkspaceMode.Expert,
            UpdateExpertViewerLayout = () => layoutUpdates++,
            RequestVisibleFrame = () => visibleFrames++,
            IsClosed = () => closed
        };

        using (var cancelled = new ShellViewerHostCoordinator(dispatcher, callbacks))
        {
            cancelled.Apply();
            mode = ShellWorkspaceMode.Workbench;
            cancelled.Apply();
            closed = true;
            cancelled.Apply();
        }

        PumpDispatcher(dispatcher);
        Check(
            "superseded Expert activation is cancelled",
            expertShown == 1
            && workbenchShown == 1
            && layoutUpdates == 0
            && visibleFrames == 0,
            $"expert={expertShown};workbench={workbenchShown};layout={layoutUpdates};frames={visibleFrames}");

        closed = false;
        mode = ShellWorkspaceMode.Expert;
        using (var completed = new ShellViewerHostCoordinator(dispatcher, callbacks))
        {
            completed.Apply();
            PumpDispatcher(dispatcher);
            Check(
                "current Expert activation updates layout and requests a frame",
                expertShown == 2 && layoutUpdates == 1 && visibleFrames == 1,
                $"expert={expertShown};layout={layoutUpdates};frames={visibleFrames}");

            completed.Apply();
            completed.Dispose();
            completed.Apply();
        }

        Check(
            "Dispose blocks later host applies",
            expertShown == 3 && layoutUpdates == 1 && visibleFrames == 1,
            $"expert={expertShown};layout={layoutUpdates};frames={visibleFrames}");

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ShellViewerHostCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static void PumpDispatcher(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
