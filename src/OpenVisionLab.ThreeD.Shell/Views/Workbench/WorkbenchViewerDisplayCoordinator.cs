using System.ComponentModel;
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
    private string? displayedEvidenceSamplePath;

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
        workbench.SurfaceMatchDisplayRequested += OnSurfaceMatchDisplayRequested;
        workbench.SurfaceMatchDisplayCleared += OnSurfaceMatchDisplayCleared;
        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        shell.PropertyChanged += OnShellPropertyChanged;
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
        workbench.SurfaceMatchDisplayRequested -= OnSurfaceMatchDisplayRequested;
        workbench.SurfaceMatchDisplayCleared -= OnSurfaceMatchDisplayCleared;
        workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
        shell.PropertyChanged -= OnShellPropertyChanged;
        viewer.WorkbenchLineFitPointSelected -= OnWorkbenchLineFitPointSelected;
    }

    private void OnWorkbenchPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ToolWorkbenchViewModel.SelectedValidationSetSample)
            or nameof(ToolWorkbenchViewModel.SelectedValidationSetStep))
        {
            ShowSelectedValidationEvidence();
        }
    }

    private void OnShellPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ShellMainWindowViewModel.IsValidateWorkspaceSelected)
            or nameof(ShellMainWindowViewModel.IsResultsWorkspaceSelected))
        {
            ShowSelectedValidationEvidence();
            return;
        }

        if (args.PropertyName is nameof(ShellMainWindowViewModel.IsAuthoringWorkspaceSelected)
            && shell.IsAuthoringWorkspaceSelected)
        {
            RestoreAuthoredSourceDisplay();
        }
    }

    private void ShowSelectedValidationEvidence()
    {
        if ((!shell.IsValidateWorkspaceSelected && !shell.IsResultsWorkspaceSelected)
            || workbench.SelectedValidationSetSample is not { } sample
            || !File.Exists(sample.SourcePath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(sample.SourcePath);
        if (string.Equals(
                displayedEvidenceSamplePath,
                fullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var step = workbench.SelectedValidationSetStep;
        var label = step is null
            ? $"Evidence | {sample.Status} | {sample.FileName}"
            : $"Evidence | {sample.Status} | {sample.FileName} | {step.ToolName}";
        if (!viewer.ShowC3DWorkbenchResult(fullPath, label))
        {
            WriteViewerError();
            return;
        }

        displayedEvidenceSamplePath = fullPath;
        RefreshViewerSourceState(syncTeaching: true);
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Info,
            $"Validation evidence displayed | sample={sample.FileName} | status={sample.Status} | recipeChanged=false | previewRun=false | validationRun=false");
    }

    private void RestoreAuthoredSourceDisplay()
    {
        if (displayedEvidenceSamplePath is null
            || string.IsNullOrWhiteSpace(workbench.Source.Path)
            || !File.Exists(workbench.Source.Path))
        {
            return;
        }

        var sourcePath = Path.GetFullPath(workbench.Source.Path);
        if (viewer.ShowC3DWorkbenchResult(sourcePath, $"Taught source | {Path.GetFileName(sourcePath)}"))
        {
            displayedEvidenceSamplePath = null;
            RefreshViewerSourceState(syncTeaching: true);
            return;
        }

        WriteViewerError();
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
        if (args.WasDisplayed && args.ConnectedRegionOutput is { } connectedRegionOutput)
        {
            args.WasDisplayed = viewer.ShowWorkbenchConnectedRegion(
                connectedRegionOutput,
                workbench.SelectedConnectedRegionId);
        }
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

    private void OnSurfaceMatchDisplayRequested(
        object? sender,
        ToolWorkbenchSurfaceMatchDisplayRequestEventArgs args)
    {
        viewer.ShowWorkbenchSurfaceMatch(
            args.Model,
            args.Scene,
            args.Execution,
            args.Assessment,
            args.Runtime,
            args.EdgeScore,
            args.EdgeDiagnosticOverlay,
            args.EdgeAssessment,
            args.FalsePositiveReview,
            args.AcquisitionDirectionOrientation);
        RefreshViewerSourceState();
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Info,
            $"Surface match evidence displayed | executionSha256={args.Execution.ContentSha256} | overlaySha256={args.Execution.Overlay?.ContentSha256 ?? "(none)"} | edgeScoreSha256={args.EdgeScore?.ContentSha256 ?? "(none)"} | edgeOverlaySha256={args.EdgeDiagnosticOverlay?.ContentSha256 ?? "(none)"} | acquisitionOrientationSha256={args.AcquisitionDirectionOrientation?.ContentSha256 ?? "(none)"} | edgeAssessment={args.EdgeAssessment?.Decision.ToString() ?? "none"} | reviewSha256={args.FalsePositiveReview?.ContentSha256 ?? "(none)"} | assessment={args.Assessment?.Decision.ToString() ?? "none"} | recipeChanged=false | previewRun=false | validationRun=false");
    }

    private void OnSurfaceMatchDisplayCleared(
        object? sender,
        EventArgs args)
    {
        viewer.ClearWorkbenchSurfaceMatch();
        RefreshViewerSourceState();
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Info,
            "Surface match evidence cleared | recipeChanged=false | previewRun=false | validationRun=false");
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
