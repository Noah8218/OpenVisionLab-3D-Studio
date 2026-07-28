using System.IO;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

internal sealed class WorkbenchViewerDisplayCoordinator : IDisposable
{
    private readonly ShellMainWindowViewModel shell;
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl viewer;
    private readonly ToolLabWindowManager toolLabs;
    private readonly ToolRecipeWorkbenchView workbenchView;
    private readonly OpenVisionDockWorkspaceView expertView;
    private readonly WorkbenchViewerTeachingCoordinator teaching;

    public WorkbenchViewerDisplayCoordinator(
        ShellMainWindowViewModel shell,
        OpenVisionThreeDViewerControl viewer,
        ToolLabWindowManager toolLabs,
        ToolRecipeWorkbenchView workbenchView,
        OpenVisionDockWorkspaceView expertView,
        WorkbenchViewerTeachingCoordinator teaching)
    {
        this.shell = shell;
        workbench = shell.Workbench;
        this.viewer = viewer;
        this.toolLabs = toolLabs;
        this.workbenchView = workbenchView;
        this.expertView = expertView;
        this.teaching = teaching;

        workbench.FilterDisplayRequested += OnFilterDisplayRequested;
        workbench.ViewerArtifactDisplayRequested += OnArtifactDisplayRequested;
        workbench.OutputComparePaneRequested += OnOutputComparePaneRequested;
        workbench.HeightDifferenceEdgeDisplayRequested += OnHeightDifferenceEdgeDisplayRequested;
        workbench.TwoPointLineDisplayRequested += OnTwoPointLineDisplayRequested;
        workbench.TwoPointLineDisplayCleared += OnTwoPointLineDisplayCleared;
        workbench.ThreePointPlaneDisplayRequested += OnThreePointPlaneDisplayRequested;
        workbench.ThreePointPlaneDisplayCleared += OnThreePointPlaneDisplayCleared;
        workbench.DatumPlaneDeviationDisplayRequested += OnDatumPlaneDeviationDisplayRequested;
        workbench.DatumPlaneDeviationDisplayCleared += OnDatumPlaneDeviationDisplayCleared;
        workbench.LineFitDisplayRequested += OnLineFitDisplayRequested;
        workbench.LineFitDisplayCleared += OnLineFitDisplayCleared;
        workbench.LineIntersectionDisplayRequested += OnLineIntersectionDisplayRequested;
        workbench.LineIntersectionDisplayCleared += OnLineIntersectionDisplayCleared;
        workbench.LandmarkCorrespondenceDisplayRequested += OnLandmarkCorrespondenceDisplayRequested;
        workbench.LandmarkCorrespondenceDisplayCleared += OnLandmarkCorrespondenceDisplayCleared;
        viewer.WorkbenchLineFitPointSelected += OnWorkbenchLineFitPointSelected;
    }

    public void Dispose()
    {
        workbench.FilterDisplayRequested -= OnFilterDisplayRequested;
        workbench.ViewerArtifactDisplayRequested -= OnArtifactDisplayRequested;
        workbench.OutputComparePaneRequested -= OnOutputComparePaneRequested;
        workbench.HeightDifferenceEdgeDisplayRequested -= OnHeightDifferenceEdgeDisplayRequested;
        workbench.TwoPointLineDisplayRequested -= OnTwoPointLineDisplayRequested;
        workbench.TwoPointLineDisplayCleared -= OnTwoPointLineDisplayCleared;
        workbench.ThreePointPlaneDisplayRequested -= OnThreePointPlaneDisplayRequested;
        workbench.ThreePointPlaneDisplayCleared -= OnThreePointPlaneDisplayCleared;
        workbench.DatumPlaneDeviationDisplayRequested -= OnDatumPlaneDeviationDisplayRequested;
        workbench.DatumPlaneDeviationDisplayCleared -= OnDatumPlaneDeviationDisplayCleared;
        workbench.LineFitDisplayRequested -= OnLineFitDisplayRequested;
        workbench.LineFitDisplayCleared -= OnLineFitDisplayCleared;
        workbench.LineIntersectionDisplayRequested -= OnLineIntersectionDisplayRequested;
        workbench.LineIntersectionDisplayCleared -= OnLineIntersectionDisplayCleared;
        workbench.LandmarkCorrespondenceDisplayRequested -= OnLandmarkCorrespondenceDisplayRequested;
        workbench.LandmarkCorrespondenceDisplayCleared -= OnLandmarkCorrespondenceDisplayCleared;
        viewer.WorkbenchLineFitPointSelected -= OnWorkbenchLineFitPointSelected;
    }

    private void OnFilterDisplayRequested(
        object? sender,
        ToolWorkbenchFilterDisplayRequestEventArgs args)
    {
        if (toolLabs.Filter is { IsVisible: true } filter)
        {
            filter.ShowFilterResult(args);
            return;
        }

        var hashLabel = args.ContentSha256.Length >= 12
            ? args.ContentSha256[..12]
            : args.ContentSha256;
        var label = args.IsSource
            ? $"Source | {Path.GetFileName(args.C3DPath)}"
            : $"{args.DisplayLabel} | {hashLabel}";
        if (viewer.ShowC3DWorkbenchResult(args.C3DPath, label))
        {
            RefreshViewerSourceState(syncTeaching: true);
            return;
        }

        WriteViewerError();
    }

    private void OnArtifactDisplayRequested(
        object? sender,
        ToolWorkbenchArtifactDisplayRequestEventArgs args)
    {
        var label = $"{args.DisplayName} | {args.Contract} | {args.State}";
        args.WasDisplayed = viewer.ShowC3DWorkbenchResult(args.C3DPath, label);
        if (args.WasDisplayed)
        {
            RefreshViewerSourceState(syncTeaching: true);
            return;
        }

        WriteViewerError();
    }

    private void OnOutputComparePaneRequested(object? sender, EventArgs args)
    {
        if (shell.IsExpertWorkspaceSelected)
        {
            expertView.ActivateOutputComparePane();
            return;
        }

        workbenchView.ActivateOutputComparePane();
    }

    private void OnHeightDifferenceEdgeDisplayRequested(
        object? sender,
        ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs args)
    {
        if (toolLabs.HeightDifferenceEdge is { IsVisible: true } edge)
        {
            edge.ShowEdgeResult(args);
            return;
        }

        var state = args.IsPublished ? "Published" : "Preview - not published";
        var label = $"Height Difference Edge {state} | {args.Output.ContentSha256[..12]}";
        if (viewer.ShowC3DWorkbenchResult(args.C3DPath, label))
        {
            viewer.ShowWorkbenchHeightDifferenceEdge(args.Output, args.IsPublished);
            RefreshViewerSourceState(syncTeaching: true);
            return;
        }

        WriteViewerError();
    }

    private void OnLineFitDisplayRequested(
        object? sender,
        ToolWorkbenchLineFitDisplayRequestEventArgs args)
    {
        viewer.ShowWorkbenchLineFit(args.Output, args.IsPublished);
        RefreshViewerSourceState();
        workbenchView.ActivateFitDiagnosticsPane();
    }

    private void OnTwoPointLineDisplayRequested(
        object? sender,
        ToolWorkbenchTwoPointLineDisplayRequestEventArgs args)
    {
        if (toolLabs.TwoPointLine is { IsVisible: true } twoPointLine)
        {
            twoPointLine.ShowTwoPointLineResult(args);
        }
        viewer.ShowWorkbenchTwoPointLine(args.Output, args.IsPublished);
        RefreshViewerSourceState();
    }

    private void OnThreePointPlaneDisplayRequested(
        object? sender,
        ToolWorkbenchThreePointPlaneDisplayRequestEventArgs args)
    {
        if (toolLabs.ThreePointPlane is { IsVisible: true } threePointPlane)
        {
            threePointPlane.ShowThreePointPlaneResult(args);
        }
        viewer.ShowWorkbenchThreePointPlane(args.Output, args.IsPublished);
        RefreshViewerSourceState();
    }

    private void OnDatumPlaneDeviationDisplayRequested(
        object? sender,
        ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs args)
    {
        if (toolLabs.DatumPlaneDeviation is { IsVisible: true } datumPlaneDeviation)
        {
            datumPlaneDeviation.ShowDatumPlaneDeviationResult(args);
        }
        viewer.ShowWorkbenchDatumPlaneDeviation(
            args.Plane,
            args.MeasurementSelection,
            args.Output,
            args.IsPublished);
        RefreshViewerSourceState();
    }

    private void OnLineIntersectionDisplayRequested(
        object? sender,
        ToolWorkbenchLineIntersectionDisplayRequestEventArgs args)
    {
        if (toolLabs.LineIntersection is { IsVisible: true } lineIntersection)
        {
            lineIntersection.ShowLineIntersectionResult(args);
        }
        viewer.ShowWorkbenchLineIntersection(
            args.FirstLine,
            args.SecondLine,
            args.Output,
            args.IsPublished);
        RefreshViewerSourceState();
        if (shell.IsExpertWorkspaceSelected)
        {
            expertView.ActivateIntersectionEvidencePane();
        }
        else
        {
            workbenchView.ActivateIntersectionEvidencePane();
        }
    }

    private void OnLandmarkCorrespondenceDisplayRequested(
        object? sender,
        ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs args)
    {
        if (toolLabs.LandmarkCorrespondence is { IsVisible: true } correspondence)
        {
            correspondence.ShowLandmarkCorrespondenceResult(args);
        }
        viewer.ShowWorkbenchLandmarkCorrespondence(args.Anchors, args.Output, args.IsPublished);
        RefreshViewerSourceState();
        if (shell.IsExpertWorkspaceSelected)
        {
            expertView.ActivateCorrespondenceEvidencePane();
        }
        else
        {
            workbenchView.ActivateCorrespondenceEvidencePane();
        }
    }

    private void RefreshViewerSourceState(bool syncTeaching = false)
    {
        shell.UpdateC3DSampleVisible(viewer.HostState.C3DSampleVisible);
        if (syncTeaching)
        {
            teaching.SyncAppliedSelections();
        }
    }

    private void WriteViewerError() =>
        OVLog.Write(LogCategory.UI, LogLevel.Error, viewer.HostState.ViewerStatus);

    private void OnTwoPointLineDisplayCleared(object? sender, EventArgs args) =>
        viewer.ClearWorkbenchTwoPointLine();

    private void OnThreePointPlaneDisplayCleared(object? sender, EventArgs args) =>
        viewer.ClearWorkbenchThreePointPlane();

    private void OnDatumPlaneDeviationDisplayCleared(object? sender, EventArgs args) =>
        viewer.ClearWorkbenchDatumPlaneDeviation();

    private void OnLineFitDisplayCleared(object? sender, EventArgs args) =>
        viewer.ClearWorkbenchLineFit();

    private void OnLineIntersectionDisplayCleared(object? sender, EventArgs args) =>
        viewer.ClearWorkbenchLineIntersection();

    private void OnLandmarkCorrespondenceDisplayCleared(object? sender, EventArgs args) =>
        viewer.ClearWorkbenchLandmarkCorrespondence();

    private void OnWorkbenchLineFitPointSelected(
        object? sender,
        WorkbenchLineFitPointSelectedEventArgs args) =>
        workbench.SelectLineFitDiagnostic(args.InputPointIndex);
}
