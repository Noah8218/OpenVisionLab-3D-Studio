using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal sealed class ShellToolLabSmoke
{
    private readonly ToolLabWindowManager windows;
    private readonly ToolWorkbenchViewModel workbench;

    public ShellToolLabSmoke(ToolLabWindowManager windows, ToolWorkbenchViewModel workbench)
    {
        this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
    }

    public bool Prepare(ShellSmokeCommandLineOptions options, out string failure)
    {
        if (!ShowAndVerifySingleInstance(
                options.FilterToolLabScreenshotPath,
                () => windows.ShowForTool("filter", showMissing: false),
                () => windows.Filter,
                "Filter Tool Lab smoke requires a Filter recipe step.",
                "Filter Tool Lab smoke could not reuse its single window instance.",
                out failure)
            || !ShowAndVerifySingleInstance(
                options.EdgeToolLabScreenshotPath,
                () => windows.ShowForTool("height-difference-edge", showMissing: false),
                () => windows.HeightDifferenceEdge,
                "Edge Tool Lab smoke requires a Height Difference Edge recipe step.",
                "Edge Tool Lab smoke could not reuse its single window instance.",
                out failure)
            || !ShowAndVerifySingleInstance(
                options.TwoPointLineToolLabScreenshotPath,
                () => windows.ShowForTool("two-point-line", showMissing: false),
                () => windows.TwoPointLine,
                "2-Point Line Tool Lab smoke requires a 2-Point Line recipe step.",
                "2-Point Line Tool Lab smoke could not reuse its single window instance.",
                out failure)
            || !ShowAndVerifySingleInstance(
                options.ThreePointPlaneToolLabScreenshotPath,
                () => windows.ShowForTool("three-point-plane", showMissing: false),
                () => windows.ThreePointPlane,
                "3-Point Plane Tool Lab smoke requires a 3-Point Plane recipe step.",
                "3-Point Plane Tool Lab smoke could not reuse its single window instance.",
                out failure)
            || !ShowAndVerifySingleInstance(
                options.DatumPlaneDeviationToolLabScreenshotPath,
                () => windows.ShowForTool("datum-plane-raw-height-deviation", showMissing: false),
                () => windows.DatumPlaneDeviation,
                "Datum Plane Deviation Tool Lab smoke requires a Datum Plane Raw-Height Deviation recipe step.",
                "Datum Plane Deviation Tool Lab smoke could not reuse its single window instance.",
                out failure)
            || !ShowAndVerifySingleInstance(
                options.LineIntersectionToolLabScreenshotPath,
                () => windows.ShowForTool("line-intersection", showMissing: false),
                () => windows.LineIntersection,
                "Line Intersection Tool Lab smoke requires a Line Intersection recipe step.",
                "Line Intersection Tool Lab smoke could not reuse its single window instance.",
                out failure)
            || !ShowAndVerifySingleInstance(
                options.LandmarkCorrespondenceToolLabScreenshotPath,
                () => windows.ShowForTool("landmark-correspondence", showMissing: false),
                () => windows.LandmarkCorrespondence,
                "Landmark Correspondence Tool Lab smoke requires a Landmark Correspondence recipe step.",
                "Landmark Correspondence Tool Lab smoke could not reuse its single window instance.",
                out failure))
        {
            return false;
        }

        if (!EnsureWaitingStep(
                options.XYZAffineSolveToolLabScreenshotPath,
                "xyz-affine-solve",
                "derived.correspondences.01",
                "XYZ Affine Solve smoke could not author its isolated waiting step.",
                out failure)
            || !ShowAndVerifySingleInstance(
                options.XYZAffineSolveToolLabScreenshotPath,
                () => windows.ShowForTool("xyz-affine-solve", showMissing: false),
                () => windows.XYZAffineSolve,
                "XYZ Affine Solve Tool Lab smoke requires an XYZ Affine Solve recipe step.",
                "XYZ Affine Solve Tool Lab smoke could not reuse its single window instance.",
                out failure)
            || !EnsureWaitingStep(
                options.XYZAffineApplyToolLabScreenshotPath,
                "xyz-affine-apply",
                "source.c3d.height-map;derived.affine-transform.01",
                "XYZ Affine Apply smoke could not author its isolated waiting step.",
                out failure)
            || !ShowAndVerifySingleInstance(
                options.XYZAffineApplyToolLabScreenshotPath,
                () => windows.ShowForTool("xyz-affine-apply", showMissing: false),
                () => windows.XYZAffineApply,
                "XYZ Affine Apply Tool Lab smoke requires an Apply XYZ Affine recipe step.",
                "XYZ Affine Apply Tool Lab smoke could not reuse its single window instance.",
                out failure)
            || !EnsureWaitingStep(
                options.RegridHeightMapToolLabScreenshotPath,
                "re-grid-height-map",
                "derived.affine-point-cloud.01",
                "Re-grid Height Map smoke could not author its isolated waiting step.",
                out failure)
            || !ShowAndVerifySingleInstance(
                options.RegridHeightMapToolLabScreenshotPath,
                () => windows.ShowForTool("re-grid-height-map", showMissing: false),
                () => windows.RegridHeightMap,
                "Re-grid Height Map Tool Lab smoke requires a Re-grid Height Map recipe step.",
                "Re-grid Height Map Tool Lab smoke could not reuse its single window instance.",
                out failure))
        {
            return false;
        }

        failure = string.Empty;
        return true;
    }

    public async Task<(bool Passed, string Failure)> CaptureAsync(ShellSmokeCommandLineOptions options)
    {
        var captures = new (string? Path, string? QualityPath, Window? Window, string Scope, string Failure)[]
        {
            (options.FilterToolLabScreenshotPath, options.FilterToolLabScreenshotQualityReportPath, windows.Filter, "FilterToolLab", "Filter Tool Lab screenshot remained blank or invalid after 3 attempts."),
            (options.EdgeToolLabScreenshotPath, options.EdgeToolLabScreenshotQualityReportPath, windows.HeightDifferenceEdge, "EdgeToolLab", "Edge Tool Lab screenshot remained blank or invalid after 3 attempts."),
            (options.TwoPointLineToolLabScreenshotPath, options.TwoPointLineToolLabScreenshotQualityReportPath, windows.TwoPointLine, "TwoPointLineToolLab", "2-Point Line Tool Lab screenshot remained blank or invalid after 3 attempts."),
            (options.ThreePointPlaneToolLabScreenshotPath, options.ThreePointPlaneToolLabScreenshotQualityReportPath, windows.ThreePointPlane, "ThreePointPlaneToolLab", "3-Point Plane Tool Lab screenshot remained blank or invalid after 3 attempts."),
            (options.DatumPlaneDeviationToolLabScreenshotPath, options.DatumPlaneDeviationToolLabScreenshotQualityReportPath, windows.DatumPlaneDeviation, "DatumPlaneDeviationToolLab", "Datum Plane Deviation Tool Lab screenshot remained blank or invalid after 3 attempts."),
            (options.LineIntersectionToolLabScreenshotPath, options.LineIntersectionToolLabScreenshotQualityReportPath, windows.LineIntersection, "LineIntersectionToolLab", "Line Intersection Tool Lab screenshot remained blank or invalid after 3 attempts."),
            (options.LandmarkCorrespondenceToolLabScreenshotPath, options.LandmarkCorrespondenceToolLabScreenshotQualityReportPath, windows.LandmarkCorrespondence, "LandmarkCorrespondenceToolLab", "Landmark Correspondence Tool Lab screenshot remained blank or invalid after 3 attempts."),
            (options.XYZAffineSolveToolLabScreenshotPath, options.XYZAffineSolveToolLabScreenshotQualityReportPath, windows.XYZAffineSolve, "XYZAffineSolveToolLab", "XYZ Affine Solve Tool Lab screenshot remained blank or invalid after 3 attempts."),
            (options.XYZAffineApplyToolLabScreenshotPath, options.XYZAffineApplyToolLabScreenshotQualityReportPath, windows.XYZAffineApply, "XYZAffineApplyToolLab", "XYZ Affine Apply Tool Lab screenshot remained blank or invalid after 3 attempts."),
            (options.RegridHeightMapToolLabScreenshotPath, options.RegridHeightMapToolLabScreenshotQualityReportPath, windows.RegridHeightMap, "RegridHeightMapToolLab", "Re-grid Height Map Tool Lab screenshot remained blank or invalid after 3 attempts.")
        };

        foreach (var capture in captures)
        {
            if (capture.Path is null)
            {
                continue;
            }

            if (capture.Window is null
                || !await ShellSmokeArtifacts.CaptureWindowWithRetryAsync(
                    ShellSmokeArtifacts.RefreshToolLabForCapture(capture.Window),
                    capture.Path,
                    capture.QualityPath,
                    capture.Scope))
            {
                return (false, capture.Failure);
            }
        }

        return (true, string.Empty);
    }

    public void CloseTemporaryWindows(ShellSmokeCommandLineOptions options)
    {
        CloseIfRequested(options.XYZAffineSolveToolLabScreenshotPath, windows.XYZAffineSolve);
        CloseIfRequested(options.XYZAffineApplyToolLabScreenshotPath, windows.XYZAffineApply);
        CloseIfRequested(options.RegridHeightMapToolLabScreenshotPath, windows.RegridHeightMap);
        CloseIfRequested(options.DatumPlaneDeviationToolLabScreenshotPath, windows.DatumPlaneDeviation);
    }

    private bool EnsureWaitingStep(
        string? screenshotPath,
        string toolId,
        string inputEntityIds,
        string failureMessage,
        out string failure)
    {
        if (screenshotPath is null || windows.EnsureStepSelected(toolId, preserveSelectedStep: false))
        {
            failure = string.Empty;
            return true;
        }

        workbench.SelectedTool = workbench.Tools.Single(tool => tool.Id == toolId);
        workbench.AddSelectedToolCommand.Execute(null);
        if (workbench.SelectedPipelineStep is not { } step)
        {
            failure = failureMessage;
            return false;
        }

        step.InputEntityIdsText = inputEntityIds;
        failure = string.Empty;
        return true;
    }

    private static bool ShowAndVerifySingleInstance<TWindow>(
        string? screenshotPath,
        Func<bool> show,
        Func<TWindow?> getWindow,
        string missingStepFailure,
        string reuseFailure,
        out string failure)
        where TWindow : Window
    {
        if (screenshotPath is null)
        {
            failure = string.Empty;
            return true;
        }

        if (!show())
        {
            failure = missingStepFailure;
            return false;
        }

        var firstWindow = getWindow();
        if (firstWindow is null || !show() || !ReferenceEquals(firstWindow, getWindow()))
        {
            failure = reuseFailure;
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static void CloseIfRequested(string? screenshotPath, Window? window)
    {
        if (screenshotPath is not null && window is { IsVisible: true })
        {
            window.Close();
        }
    }
}
