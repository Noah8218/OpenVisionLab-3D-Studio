using System.ComponentModel;
using System.IO;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

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
    private bool isInspectionDisplayActive;
    private bool disposed;
    private bool orderedRunWasRunning;

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
        workbench.OutputCompareViewerRequested += OnOutputCompareViewerRequested;
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
        workbench.InspectionQueue.SelectedSampleChanged += OnInspectionSampleSelected;
        shell.PropertyChanged += OnShellPropertyChanged;
        viewer.WorkbenchLineFitPointSelected += OnWorkbenchLineFitPointSelected;
        shell.ResultsWorkspace.PropertyChanged += OnResultsWorkspacePropertyChanged;
        RefreshTeachingResultLabels();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        shell.ResultsWorkspace.PropertyChanged -= OnResultsWorkspacePropertyChanged;
        workbench.FilterDisplayRequested -= OnFilterDisplayRequested;
        workbench.ViewerArtifactDisplayRequested -= OnArtifactDisplayRequested;
        workbench.OutputComparePaneRequested -= OnOutputComparePaneRequested;
        workbench.OutputCompareViewerRequested -= OnOutputCompareViewerRequested;
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
        workbench.InspectionQueue.SelectedSampleChanged -= OnInspectionSampleSelected;
        shell.PropertyChanged -= OnShellPropertyChanged;
        viewer.WorkbenchLineFitPointSelected -= OnWorkbenchLineFitPointSelected;
    }

    private void RefreshTeachingResultLabels()
    {
        if (disposed) return;
        if (!viewer.Dispatcher.CheckAccess())
        {
            viewer.Dispatcher.BeginInvoke(new Action(RefreshTeachingResultLabels));
            return;
        }
        shell.ResultsWorkspace.RefreshTeachingResultLabels(workbench.TeachingResults.ToArray(),
            workbench.GetCurrentAppliedTeachingSelections(), workbench.RecipePath ?? string.Empty);
    }

    private void OnResultsWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!disposed && args.PropertyName == nameof(ResultsWorkspaceViewModel.TeachingResultLabels)) SyncTeachingResultLabels();
    }

    private void SyncTeachingResultLabels()
    {
        if (disposed) return;
        if (!viewer.Dispatcher.CheckAccess())
        {
            viewer.Dispatcher.BeginInvoke(new Action(SyncTeachingResultLabels));
            return;
        }
        viewer.TrySetTeachingResultLabels(shell.IsAuthoringWorkspaceSelected ? shell.ResultsWorkspace.TeachingResultLabels : []);
    }

    private void OnWorkbenchPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ToolWorkbenchViewModel.IsOrderedRunRunning))
        {
            var completed = orderedRunWasRunning && !workbench.IsOrderedRunRunning;
            orderedRunWasRunning = workbench.IsOrderedRunRunning;
            if (completed && !workbench.IsReadOnlySampleView && workbench.CurrentOrderedRunResult is { } execution)
            {
                var output = execution.Steps.LastOrDefault(step => step.RuntimeOutput is OpenVisionLab.ThreeD.Data.C3DHeightFieldSnapshot);
                if (output is not null && workbench.TryGetPublishedTeachingHeightField(output.OutputEntityId, out var map, out var path)
                    && map is not null && path is not null)
                    OnFilterDisplayRequested(workbench, new ToolWorkbenchFilterDisplayRequestEventArgs(path, map.ContentSha256, false, output.ToolName));
            }
        }
        if (args.PropertyName == nameof(ToolWorkbenchViewModel.SelectedPipelineStep) && shell.IsInspectWorkspaceSelected)
            ShowSelectedInspectionSample(workbench.InspectionQueue.SelectedSample, loadRecord: false);
        if (args.PropertyName == nameof(ToolWorkbenchViewModel.HasTeachingResults)) RefreshTeachingResultLabels();
        if (args.PropertyName is nameof(ToolWorkbenchViewModel.SelectedValidationSetSample)
            or nameof(ToolWorkbenchViewModel.SelectedValidationSetStep))
        {
            ShowSelectedValidationEvidence();
        }
    }

    private void OnInspectionSampleSelected(
        object? sender,
        InspectionQueueSelectionChangedEventArgs args)
    {
        if (shell.IsInspectWorkspaceSelected)
        {
            ShowSelectedInspectionSample(args.Sample);
        }
    }

    private void OnShellPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellMainWindowViewModel.IsAuthoringWorkspaceSelected)) SyncTeachingResultLabels();
        if (args.PropertyName != nameof(ShellMainWindowViewModel.IsInspectionWorkspaceSelected)) return;
        if (shell.IsInspectWorkspaceSelected)
        {
            isInspectionDisplayActive = true;
            ShowSelectedInspectionSample(workbench.InspectionQueue.SelectedSample);
            return;
        }

        if (shell.IsValidateWorkspaceSelected || shell.IsResultsWorkspaceSelected)
        {
            ShowSelectedValidationEvidence();
            return;
        }

        if (shell.IsAuthoringWorkspaceSelected)
        {
            RestoreAuthoredSourceDisplay();
        }
    }

    private void ShowSelectedInspectionSample(InspectionQueueSampleItem? sample, bool loadRecord = true)
    {
        if (sample is null)
        {
            shell.ClearCurrentRunEvidenceForRecipeContext();
            displayedEvidenceSamplePath = null;
            viewer.ClearC3DTeachingSource(workbench.Localization.Resolve("ThreeD.Inspection.NoInput", "검사할 파일을 선택하세요.", "Select an inspection file."));
            workbench.HeightImageViewer.ClearSource();
            RefreshViewerSourceState();
            return;
        }

        if (!File.Exists(sample.SourcePath))
        {
            viewer.ClearC3DTeachingSource(workbench.Localization.SourceMissing);
            displayedEvidenceSamplePath = null;
            workbench.HeightImageViewer.ClearSource();
            shell.ClearCurrentRunEvidenceForRecipeContext();
            RefreshViewerSourceState();
            return;
        }

        workbench.BeginHeightImageSourceLoad();
        var fullPath = Path.GetFullPath(sample.SourcePath);
        var label = $"Inspection input | {sample.FileName}";
        if (sample.Record is { } record && workbench.SelectedPipelineStep is { } selected)
        {
            var id = selected.InputEntityIds.FirstOrDefault();
            if (id is not null && sample.HeightMaps.TryGetValue(id, out var map))
            {
                try
                {
                    var identity = OpenVisionLab.ThreeD.Data.ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(map.Path);
                    if (!string.Equals(record.Source.Sha256, map.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(identity.ContentSha256, map.ContentSha256, StringComparison.OrdinalIgnoreCase)
                        || identity.GridWidth != map.Width || identity.GridHeight != map.Height)
                        throw new InvalidDataException(workbench.Localization.Resolve("ThreeD.Inspection.OutputMapChanged", "검사 결과 맵이 기록과 다릅니다. 다시 검사하세요.", "The output map differs from its Run Record. Run the inspection again."));
                    fullPath = map.Path;
                    label = $"{selected.ToolName} | {sample.FileName}";
                    InheritHeightMapTexture(map.ContentSha256, map.RootSourceSha256, map.Width, map.Height, map.OriginColumn, map.OriginRow);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                {
                    displayedEvidenceSamplePath = null;
                    viewer.ClearC3DTeachingSource(exception.Message);
                    workbench.HeightImageViewer.ClearSource();
                    RefreshViewerSourceState();
                    return;
                }
            }
            else if (id is not null && !string.Equals(id, workbench.Source.Id, StringComparison.OrdinalIgnoreCase)
                && workbench.PipelineSteps.Any(step => step.OutputEntityId == id
                    && step.ToolId is "nine-point-surface-compensation" or "filter" or "height-threshold-background-removal"
                        or "remove-outlier-pixels" or "domain-mask" or "level-surface" or "roi-crop"))
            {
                displayedEvidenceSamplePath = null;
                viewer.ClearC3DTeachingSource(workbench.Localization.NoCurrentDisplayableOutput);
                workbench.HeightImageViewer.ClearSource();
                RefreshViewerSourceState();
                return;
            }
        }
        if (!string.Equals(displayedEvidenceSamplePath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            if (!viewer.ShowC3DWorkbenchResult(fullPath, label))
            {
                viewer.ClearC3DTeachingSource(workbench.Localization.SourceUnreadable);
                displayedEvidenceSamplePath = null;
                workbench.HeightImageViewer.ClearSource();
                shell.ClearCurrentRunEvidenceForRecipeContext();
                RefreshViewerSourceState();
                WriteViewerError();
                return;
            }

            displayedEvidenceSamplePath = fullPath;
            RefreshViewerSourceState();
        }

        if (!loadRecord) return;
        if (sample.CanOpenCurrentRunRecord
            && shell.LoadRunRecord(sample.RunRecordPath!, out _))
        {
            return;
        }

        shell.ClearCurrentRunEvidenceForRecipeContext();
    }

    private void ShowSelectedValidationEvidence()
    {
        if (!shell.IsValidateWorkspaceSelected && !shell.IsResultsWorkspaceSelected) return;

        var sample = workbench.SelectedValidationSetSample;
        if (shell.IsValidateWorkspaceSelected)
        {
            isInspectionDisplayActive = true;
            shell.ClearCurrentRunEvidenceForRecipeContext();
            workbench.RefreshValidationSampleView();
            if (sample is null || !File.Exists(sample.SourcePath))
            {
                displayedEvidenceSamplePath = null;
                viewer.ClearC3DTeachingSource(sample is null ? workbench.Localization.ValidationSetNoSelection : workbench.Localization.SourceMissing);
                workbench.HeightImageViewer.ClearSource();
                RefreshViewerSourceState();
                return;
            }
        }
        if (sample is null || !File.Exists(sample.SourcePath)) return;

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
        workbench.BeginHeightImageSourceLoad();
        if (!viewer.ShowC3DWorkbenchResult(fullPath, label))
        {
            displayedEvidenceSamplePath = null;
            viewer.ClearC3DTeachingSource(workbench.Localization.SourceUnreadable);
            workbench.HeightImageViewer.ClearSource();
            RefreshViewerSourceState();
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
        if (!isInspectionDisplayActive && displayedEvidenceSamplePath is null) return;
        if (workbench.CurrentOrderedRunRecordPath is not { } teachingRecordPath || !shell.LoadRunRecord(teachingRecordPath, out _))
            shell.ClearCurrentRunEvidenceForRecipeContext();
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path))
        {
            displayedEvidenceSamplePath = null;
            isInspectionDisplayActive = false;
            viewer.ClearC3DTeachingSource(workbench.Localization.SourceMissing);
            workbench.HeightImageViewer.ClearSource();
            RefreshViewerSourceState();
            return;
        }

        var sourcePath = Path.GetFullPath(workbench.Source.Path);
        if (viewer.ShowC3DWorkbenchResult(sourcePath, $"Taught source | {Path.GetFileName(sourcePath)}"))
        {
            displayedEvidenceSamplePath = null;
            isInspectionDisplayActive = false;
            workbench.BeginHeightImageSourceLoad();
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

        if (!args.IsSource) InheritHeightMapTexture(args.ContentSha256);
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

    private void InheritHeightMapTexture(string contentSha256)
    {
        foreach (var step in workbench.PipelineSteps)
        {
            if (!workbench.TryGetPublishedTeachingHeightField(step.OutputEntityId, out var output, out _) || output is null
                || !string.Equals(output.ContentSha256, contentSha256, StringComparison.OrdinalIgnoreCase)) continue;
            InheritHeightMapTexture(contentSha256, output.RootSourceSha256, output.Width, output.Height,
                output.GridOriginColumn, output.GridOriginRow);
            return;
        }
    }

    private void InheritHeightMapTexture(string contentSha256, string rootSha256, int width, int height, int originColumn, int originRow)
    {
        // These C3D preparation outputs retain XY; cropped or regridded maps do not inherit a full-source texture.
        var settings = viewer.CaptureC3DTextureSettings();
        if (settings.ContainsKey(contentSha256) || !settings.TryGetValue(rootSha256, out var original)
            || original.GridWidth != width || original.GridHeight != height || originColumn != 0 || originRow != 0) return;
        var derived = new Dictionary<string, OpenVisionLab.ThreeD.Data.HeightTextureImageSettings>(settings, StringComparer.OrdinalIgnoreCase)
        {
            [contentSha256] = original
        };
        viewer.RestoreC3DTextureSettings(derived);
    }

    private void OnArtifactDisplayRequested(
        object? sender,
        ToolWorkbenchArtifactDisplayRequestEventArgs args)
    {
        if (args.SignedDelta is { } signedDelta)
        {
            args.WasDisplayed = workbench.HeightImageViewer.ShowSignedDeltaArtifact(signedDelta);
            if (args.WasDisplayed)
            {
                RefreshViewerSourceState(syncTeaching: true);
                OVLog.Write(
                    LogCategory.UI,
                    LogLevel.Info,
                    $"Signed-delta viewer display | artifact={signedDelta.ContentSha256} | values={signedDelta.OutputContentSha256} | finiteZero={signedDelta.ZeroDeltaSampleCount} | missing={signedDelta.MissingEitherSampleCount} | c3dEncoding=false");
                return;
            }

            WriteViewerError();
            return;
        }

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

    private void OnOutputCompareViewerRequested(object? sender, EventArgs args)
    {
        if (shell.IsExpertWorkspaceSelected)
        {
            expertView.ActivateViewerPane();
            return;
        }

        workbenchView.ActivateViewerPane();
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

    private void OnDatumPlaneDeviationDisplayCleared(object? sender, EventArgs args)
    {
        viewer.ClearWorkbenchDatumPlaneDeviation();
        if (toolLabs.DatumPlaneDeviation is { IsVisible: true } datumPlaneDeviation)
            datumPlaneDeviation.ClearDatumPlaneDeviationResult();
    }

    private void OnLineFitDisplayCleared(object? sender, EventArgs args) =>
        viewer.ClearWorkbenchLineFit();

    private void OnLineIntersectionDisplayCleared(object? sender, EventArgs args)
    {
        viewer.ClearWorkbenchLineIntersection();
        if (toolLabs.LineIntersection is { IsVisible: true } lineIntersection)
            lineIntersection.ClearLineIntersectionResult();
    }

    private void OnLandmarkCorrespondenceDisplayCleared(object? sender, EventArgs args) =>
        viewer.ClearWorkbenchLandmarkCorrespondence();

    private void OnWorkbenchLineFitPointSelected(
        object? sender,
        WorkbenchLineFitPointSelectedEventArgs args) =>
        workbench.SelectLineFitDiagnostic(args.InputPointIndex);
}
