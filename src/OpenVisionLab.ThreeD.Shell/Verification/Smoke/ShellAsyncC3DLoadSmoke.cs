using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellAsyncC3DLoadSmoke
{
    public static async Task<bool> RunAsync(
        OpenVisionThreeDViewerControl viewer,
        ToolWorkbenchViewModel workbench,
        Dispatcher dispatcher,
        string sourcePath,
        string? reportPath,
        double? cancelAtPercent,
        bool expectFailure,
        string? expectedViewerStatusFragment,
        Func<string, Task> loadSourceAsync,
        Func<string, bool> isViewerSourceAlreadyLoaded,
        Func<double> getWorkbenchSourceBindingMilliseconds)
    {
        var dispatcherTicks = 0;
        var cancelIssued = false;
        var previousPath = viewer.CurrentC3DSourcePath;
        var timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(1),
            DispatcherPriority.Input,
            (_, _) =>
            {
                dispatcherTicks++;
                if (!cancelIssued
                    && cancelAtPercent is { } cancelAt
                    && workbench.IsC3DSourceLoading
                    && workbench.C3DSourceLoadProgressPercent >= cancelAt)
                {
                    cancelIssued = true;
                    workbench.CancelC3DSourceLoadCommand.Execute(null);
                }
            },
            dispatcher);
        var stopwatch = Stopwatch.StartNew();
        timer.Start();
        await loadSourceAsync(sourcePath);
        timer.Stop();
        stopwatch.Stop();

        var expectedPath = Path.GetFullPath(sourcePath);
        var loadPerformance = viewer.LastC3DSourceLoadPerformance;
        var sourceStatePerformance = workbench.LastC3DSourceStatePerformance;
        var loadStateCleared = !workbench.IsC3DSourceLoading;
        var viewerStatus = viewer.HostState.ViewerStatus ?? string.Empty;
        var viewerStatusMatched = !expectFailure
            || !string.IsNullOrWhiteSpace(expectedViewerStatusFragment)
                && viewerStatus.Contains(
                    expectedViewerStatusFragment,
                    StringComparison.Ordinal);
        var passed = expectFailure
            ? !cancelIssued
              && previousPath is not null
              && isViewerSourceAlreadyLoaded(previousPath)
              && loadStateCleared
              && viewerStatusMatched
            : cancelAtPercent is null
                ? isViewerSourceAlreadyLoaded(expectedPath)
                  && loadStateCleared
                  && dispatcherTicks > 0
                : cancelIssued
                  && previousPath is not null
                  && isViewerSourceAlreadyLoaded(previousPath)
                  && loadStateCleared;
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath,
            [
                "OpenVisionLab 3D actual EXE asynchronous C3D load smoke",
                $"Result: {(passed ? "Pass" : "Fail")}",
                $"Mode: {(expectFailure ? "Failure" : cancelAtPercent is null ? "Complete" : "Cancel")}",
                $"PreviousPath: {previousPath}",
                $"TargetPath: {expectedPath}",
                $"CurrentPath: {viewer.CurrentC3DSourcePath}",
                $"ViewerStatus: {viewerStatus}",
                $"ExpectedViewerStatusFragment: {expectedViewerStatusFragment ?? "n/a"}",
                $"ViewerStatusMatched: {viewerStatusMatched}",
                $"ElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}",
                $"GridReadAndStatisticsMilliseconds: {loadPerformance?.Grid.ReadAndStatisticsMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"GridDistributionMilliseconds: {loadPerformance?.Grid.DistributionMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"GridRenderPointsMilliseconds: {loadPerformance?.Grid.RenderPointsMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"GridHashMilliseconds: {loadPerformance?.Grid.HashMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"GridTotalMilliseconds: {loadPerformance?.Grid.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"RenderTopologyMilliseconds: {loadPerformance?.TopologyMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"RenderPositionsMilliseconds: {loadPerformance?.PositionsMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"WorkerTotalMilliseconds: {loadPerformance?.WorkerMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyAndFirstRenderMilliseconds: {loadPerformance?.ApplyMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"WorkbenchSourceBindingMilliseconds: {getWorkbenchSourceBindingMilliseconds().ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}",
                $"WorkbenchCaptureMilliseconds: {sourceStatePerformance?.CaptureMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"WorkbenchClearPreviewMilliseconds: {sourceStatePerformance?.ClearPreviewMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"WorkbenchIdentityMilliseconds: {sourceStatePerformance?.IdentityMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"WorkbenchRecipeStateMilliseconds: {sourceStatePerformance?.RecipeStateMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"WorkbenchSelectionSyncMilliseconds: {sourceStatePerformance?.SelectionSyncMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"WorkbenchLoggingMilliseconds: {sourceStatePerformance?.LoggingMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"WorkbenchMeasuredTotalMilliseconds: {sourceStatePerformance?.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplySourceStateMilliseconds: {loadPerformance?.ApplyDetail?.SourceStateMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyClearStateMilliseconds: {loadPerformance?.ApplyDetail?.ClearStateMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplySampleStatusMilliseconds: {loadPerformance?.ApplyDetail?.SampleStatusMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplySceneMilliseconds: {loadPerformance?.ApplyDetail?.SceneMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyDisplayMilliseconds: {loadPerformance?.ApplyDetail?.DisplayMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyAlignmentMilliseconds: {loadPerformance?.ApplyDetail?.AlignmentMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyStatusMilliseconds: {loadPerformance?.ApplyDetail?.StatusMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyFinalRenderMilliseconds: {loadPerformance?.ApplyDetail?.FinalRenderMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyMeasuredTotalMilliseconds: {loadPerformance?.ApplyDetail?.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyRenderRequests: {loadPerformance?.ApplyDetail?.RenderRequestCount.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplySuppressedRenderRequests: {loadPerformance?.ApplyDetail?.SuppressedRenderRequestCount.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyRenderExecutions: {loadPerformance?.ApplyDetail?.RenderExecutionCount.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyRenderExecutionMilliseconds: {loadPerformance?.ApplyDetail?.RenderExecutionMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyDisplayListBuilds: {loadPerformance?.ApplyDetail?.DisplayListBuildCount.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"UiApplyLastDisplayListBuildReason: {loadPerformance?.ApplyDetail?.LastDisplayListBuildReason ?? "n/a"}",
                $"DispatcherTicksDuringLoad: {dispatcherTicks}",
                $"CancelAtPercent: {cancelAtPercent?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                $"CancelIssued: {cancelIssued}",
                $"LoadStateCleared: {loadStateCleared}",
                $"FinalProgressPercent: {workbench.C3DSourceLoadProgressPercent:F1}"
            ]);
        }

        return passed;
    }
}
