using System.IO;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellPlaneFlatnessLiveA3Smoke
{
    public static async Task<bool> RunAsync(
        ShellMainWindowViewModel viewModel,
        OpenVisionThreeDViewerControl viewer,
        string? reportPath,
        string? savePath,
        Action syncAppliedTeachingSelections)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Plane Flatness live A3 pointer Shell smoke",
            "Boundary|Deterministic synthetic display-frame evidence only; this is not physical calibration, Gauge R&R, or metrology evidence."
        };
        var workbench = viewModel.Workbench;
        var previewBefore = viewer.ViewModel.PreviewToolResult;
        var resultsBefore = viewer.ViewModel.ResultEntities;

        bool Complete(bool passed, string message)
        {
            lines.Add($"InspectionBoundary|previewReferenceUnchanged={ReferenceEquals(previewBefore, viewer.ViewModel.PreviewToolResult)}|resultReferenceUnchanged={ReferenceEquals(resultsBefore, viewer.ViewModel.ResultEntities)}");
            lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{message}");
            ShellSmokeArtifacts.WriteTextReport(reportPath, lines, withoutBom: true);
            Console.WriteLine(lines[^1]);
            if (!passed)
            {
                viewModel.SetViewerSmokeFailed(message);
            }
            return passed;
        }

        string? PointerReport(string role)
        {
            if (string.IsNullOrWhiteSpace(reportPath)) return null;
            var fullReportPath = Path.GetFullPath(reportPath);
            return Path.Combine(
                Path.GetDirectoryName(fullReportPath)!,
                $"{Path.GetFileNameWithoutExtension(fullReportPath)}.{role}-pointer.txt");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(reportPath) || string.IsNullOrWhiteSpace(savePath))
            {
                return Complete(false, "Live A3 pointer smoke requires explicit report and saved-recipe paths.");
            }
            if (string.IsNullOrWhiteSpace(workbench.RecipePath))
            {
                return Complete(false, "Live A3 pointer smoke requires the prepared fixture recipe to be opened by --tool-teaching-recipe.");
            }

            var regridStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.RegridStepId, StringComparison.OrdinalIgnoreCase));
            var planeStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PlaneFlatnessStepId, StringComparison.OrdinalIgnoreCase));
            var pointPairStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PointPairStepId, StringComparison.OrdinalIgnoreCase));
            var gapFlushStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.GapFlushStepId, StringComparison.OrdinalIgnoreCase));
            var volumeStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.VolumeStepId, StringComparison.OrdinalIgnoreCase));
            var crossSectionStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.CrossSectionStepId, StringComparison.OrdinalIgnoreCase));
            if (regridStep is null || planeStep is null || pointPairStep is null || gapFlushStep is null || volumeStep is null || crossSectionStep is null)
            {
                return Complete(false, "Prepared fixture recipe is missing its Re-grid, Plane Flatness, Point Pair, Gap / Flush, Volume, or Cross-section step.");
            }

            workbench.SelectedPipelineStep = regridStep;
            var publishedA2 = PlaneFlatnessLiveA3PointerSmokeFixture.CreatePublishedA2(workbench.RecipePath);
            if (!workbench.TryRegisterSyntheticPublishedAffineApplyOutputForSmoke(publishedA2, out var a2Message))
            {
                return Complete(false, a2Message);
            }
            lines.Add($"A2|entity={publishedA2.OutputEntityId}|sha256={publishedA2.ContentSha256}|finite={publishedA2.FinitePointCount}|registration={a2Message}");

            if (!await workbench.PreviewSelectedRegridHeightFieldAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Normal Re-grid Preview was not publishable: {workbench.RegridHeightFieldExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsRegridHeightFieldPreviewPublished
                || !workbench.TryGetPublishedRegridHeightFieldOutput(
                    PlaneFlatnessLiveA3PointerSmokeFixture.HeightFieldEntityId,
                    out var publishedA3)
                || publishedA3 is null)
            {
                return Complete(false, "Normal Re-grid Publish did not register the exact Preview A3 output.");
            }
            var expectedBinding = ToolRecipeSelectionSourceBindingVerifier.FromTransformedHeightField(publishedA3);
            lines.Add($"A3|entity={publishedA3.OutputEntityId}|sha256={publishedA3.ContentSha256}|populated={publishedA3.PopulatedCellCount}/{publishedA3.Cells.Count}|coverage={publishedA3.CoverageRatio:R}|published=True");

            workbench.SelectedPipelineStep = planeStep;
            if (!workbench.CapturePlaneFlatnessReferenceRoiCommand.CanExecute(null))
            {
                return Complete(false, "Plane Flatness reference ROI capture was not enabled after A3 Publish.");
            }
            workbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (!viewer.TeachingCaptureSnapshot.IsActive
                || !await viewer.RunTeachingCapturePointerSmokeAsync(
                    cancelWhenReady: false,
                    PointerReport("reference"),
                    exerciseNavigationGestures: false)
                || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
            {
                return Complete(false, "Two real Viewer pointer picks did not produce an applicable reference ROI candidate.");
            }
            viewer.TryGetC3DTeachingCandidate(out var referenceCandidate, out var referenceCandidateMessage);
            lines.Add($"ReferenceCandidate|id={referenceCandidate?.Id}|rectangle={referenceCandidate?.GridRectangle}|owner={referenceCandidate?.SourceBinding.OwnerEntityId}|sha256={referenceCandidate?.SourceBinding.ContentSha256}|message={referenceCandidateMessage}");
            workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
            await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            lines.Add($"ReferenceApplyState|workbenchActive={workbench.IsTeachingSelectionCaptureActive}|viewerActive={viewer.TeachingCaptureSnapshot.IsActive}|progress={workbench.TeachingSelectionCaptureProgress}");
            if (workbench.IsTeachingSelectionCaptureActive || viewer.TeachingCaptureSnapshot.IsActive)
            {
                return Complete(false, "Reference ROI candidate was not accepted by the workbench.");
            }
            var referenceSelection = workbench.PlaneFlatnessReferenceSelection;
            if (referenceSelection?.GridRectangle is null)
            {
                return Complete(false, "Applied reference ROI was not routed into the Plane Flatness step.");
            }
            lines.Add($"ReferenceROI|id={referenceSelection.Id}|rectangle={referenceSelection.GridRectangle.Row},{referenceSelection.GridRectangle.Column},{referenceSelection.GridRectangle.RowCount},{referenceSelection.GridRectangle.ColumnCount}|owner={referenceSelection.SourceBinding.OwnerEntityId}|sha256={referenceSelection.SourceBinding.ContentSha256}");

            if (!workbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null))
            {
                return Complete(false, "Plane Flatness measurement ROI capture was not enabled after the reference ROI was applied.");
            }
            workbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
            await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (!viewer.TeachingCaptureSnapshot.IsActive
                || !await viewer.RunTeachingCapturePointerSmokeAsync(
                    cancelWhenReady: false,
                    PointerReport("measurement"),
                    exerciseNavigationGestures: false)
                || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
            {
                return Complete(false, "Two real Viewer pointer picks did not produce an applicable measurement ROI candidate.");
            }
            viewer.TryGetC3DTeachingCandidate(out var measurementCandidate, out var measurementCandidateMessage);
            lines.Add($"MeasurementCandidate|id={measurementCandidate?.Id}|rectangle={measurementCandidate?.GridRectangle}|owner={measurementCandidate?.SourceBinding.OwnerEntityId}|sha256={measurementCandidate?.SourceBinding.ContentSha256}|message={measurementCandidateMessage}");
            workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
            await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            lines.Add($"MeasurementApplyState|workbenchActive={workbench.IsTeachingSelectionCaptureActive}|viewerActive={viewer.TeachingCaptureSnapshot.IsActive}|progress={workbench.TeachingSelectionCaptureProgress}");
            if (workbench.IsTeachingSelectionCaptureActive || viewer.TeachingCaptureSnapshot.IsActive)
            {
                return Complete(false, "Measurement ROI candidate was not accepted by the workbench.");
            }
            var measurementSelection = workbench.PlaneFlatnessMeasurementSelection;
            if (measurementSelection?.GridRectangle is null
                || string.Equals(referenceSelection.Id, measurementSelection.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Complete(false, "Applied measurement ROI was not routed as a distinct Plane Flatness role.");
            }
            lines.Add($"MeasurementROI|id={measurementSelection.Id}|rectangle={measurementSelection.GridRectangle.Row},{measurementSelection.GridRectangle.Column},{measurementSelection.GridRectangle.RowCount},{measurementSelection.GridRectangle.ColumnCount}|owner={measurementSelection.SourceBinding.OwnerEntityId}|sha256={measurementSelection.SourceBinding.ContentSha256}");

            var executionUnchanged = ReferenceEquals(previewBefore, viewer.ViewModel.PreviewToolResult)
                && ReferenceEquals(resultsBefore, viewer.ViewModel.ResultEntities);
            if (!executionUnchanged)
            {
                return Complete(false, "ROI teaching changed inspection Preview/Run evidence before an explicit measurement Preview.");
            }

            workbench.SelectedPipelineStep = pointPairStep;
            if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
            {
                return Complete(false, "Point Pair capture was not enabled against the Published A3 output.");
            }
            workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (!viewer.TeachingCaptureSnapshot.IsActive
                || !await viewer.RunTeachingCapturePointerSmokeAsync(
                    cancelWhenReady: false,
                    PointerReport("point-pair"),
                    exerciseNavigationGestures: false)
                || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
            {
                return Complete(false, "Two real Viewer pointer picks did not produce an applicable PointSet(2) candidate.");
            }
            viewer.TryGetC3DTeachingCandidate(out var pointPairCandidate, out var pointPairCandidateMessage);
            lines.Add($"PointPairCandidate|id={pointPairCandidate?.Id}|points={pointPairCandidate?.Points?.Count}|owner={pointPairCandidate?.SourceBinding.OwnerEntityId}|sha256={pointPairCandidate?.SourceBinding.ContentSha256}|message={pointPairCandidateMessage}");
            workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
            await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var pointPairSelection = workbench.SelectedStepTeachingSelection;
            if (pointPairSelection?.Points?.Count != 2
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, pointPairSelection.SourceBinding))
            {
                return Complete(false, "Applied PointSet(2) was not routed with the exact A3 binding.");
            }
            lines.Add($"PointPair|id={pointPairSelection.Id}|first={pointPairSelection.Points[0].Locator.Row},{pointPairSelection.Points[0].Locator.Column}|second={pointPairSelection.Points[1].Locator.Row},{pointPairSelection.Points[1].Locator.Column}|owner={pointPairSelection.SourceBinding.OwnerEntityId}|sha256={pointPairSelection.SourceBinding.ContentSha256}");

            if (!await workbench.PreviewSelectedMeasurementAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Point Pair Preview was not publishable: {workbench.MeasurementExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsMeasurementPreviewPublished || workbench.CurrentMeasurementOutput is null)
            {
                return Complete(false, "Point Pair Publish did not preserve the exact Preview output.");
            }
            lines.Add($"PointPairResult|status={workbench.CurrentMeasurementOutput.Result.Status}|sha256={workbench.CurrentMeasurementOutput.ContentSha256}|evidence={workbench.CurrentMeasurementOutput.EvidenceSummary}|published=True");

            workbench.SelectedPipelineStep = gapFlushStep;
            var gapFirst = workbench.PlaneFlatnessReferenceSelection;
            var gapSecond = workbench.PlaneFlatnessMeasurementSelection;
            if (gapFirst?.GridRectangle is null || gapSecond?.GridRectangle is null
                || string.Equals(gapFirst.Id, gapSecond.Id, StringComparison.OrdinalIgnoreCase)
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, gapFirst.SourceBinding)
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, gapSecond.SourceBinding))
            {
                return Complete(false, "Gap / Flush did not expose two distinct ordered ROIs with the exact A3 binding.");
            }
            if (!await workbench.PreviewSelectedMeasurementAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Gap / Flush Preview was not publishable: {workbench.MeasurementExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsMeasurementPreviewPublished || workbench.CurrentMeasurementOutput is null)
            {
                return Complete(false, "Gap / Flush Publish did not preserve the exact Preview output.");
            }
            lines.Add($"GapFlush|first={gapFirst.Id}:{gapFirst.GridRectangle.Row},{gapFirst.GridRectangle.Column},{gapFirst.GridRectangle.RowCount},{gapFirst.GridRectangle.ColumnCount}|second={gapSecond.Id}:{gapSecond.GridRectangle.Row},{gapSecond.GridRectangle.Column},{gapSecond.GridRectangle.RowCount},{gapSecond.GridRectangle.ColumnCount}|status={workbench.CurrentMeasurementOutput.Result.Status}|sha256={workbench.CurrentMeasurementOutput.ContentSha256}|evidence={workbench.CurrentMeasurementOutput.EvidenceSummary}|published=True");

            workbench.SelectedPipelineStep = volumeStep;
            var volumeReference = workbench.PlaneFlatnessReferenceSelection;
            var volumeMeasurement = workbench.PlaneFlatnessMeasurementSelection;
            if (volumeReference?.GridRectangle is null || volumeMeasurement?.GridRectangle is null
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, volumeReference.SourceBinding)
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, volumeMeasurement.SourceBinding))
            {
                return Complete(false, "Volume did not expose ordered reference and measurement ROIs with the exact A3 binding.");
            }
            if (!await workbench.PreviewSelectedMeasurementAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Volume Preview was not publishable: {workbench.MeasurementExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsMeasurementPreviewPublished || workbench.CurrentMeasurementOutput is null)
            {
                return Complete(false, "Volume Publish did not preserve the exact Preview output.");
            }
            lines.Add($"Volume|reference={volumeReference.Id}:{volumeReference.GridRectangle.Row},{volumeReference.GridRectangle.Column},{volumeReference.GridRectangle.RowCount},{volumeReference.GridRectangle.ColumnCount}|measurement={volumeMeasurement.Id}:{volumeMeasurement.GridRectangle.Row},{volumeMeasurement.GridRectangle.Column},{volumeMeasurement.GridRectangle.RowCount},{volumeMeasurement.GridRectangle.ColumnCount}|status={workbench.CurrentMeasurementOutput.Result.Status}|sha256={workbench.CurrentMeasurementOutput.ContentSha256}|evidence={workbench.CurrentMeasurementOutput.EvidenceSummary}|published=True");

            workbench.SelectedPipelineStep = crossSectionStep;
            var crossSectionSelection = workbench.SelectedStepTeachingSelection;
            if (crossSectionSelection?.GridRectangle is not { RowCount: 1, ColumnCount: >= 2 }
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, crossSectionSelection.SourceBinding))
            {
                return Complete(false, "Cross-section did not expose one A3 row segment with the exact Published A3 binding.");
            }
            if (!await workbench.PreviewSelectedMeasurementAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Cross-section Preview was not publishable: {workbench.MeasurementExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsMeasurementPreviewPublished || workbench.CurrentMeasurementOutput is null)
            {
                return Complete(false, "Cross-section Publish did not preserve the exact Preview output.");
            }
            lines.Add($"CrossSection|selection={crossSectionSelection.Id}:{crossSectionSelection.GridRectangle.Row},{crossSectionSelection.GridRectangle.Column},{crossSectionSelection.GridRectangle.RowCount},{crossSectionSelection.GridRectangle.ColumnCount}|status={workbench.CurrentMeasurementOutput.Result.Status}|sha256={workbench.CurrentMeasurementOutput.ContentSha256}|evidence={workbench.CurrentMeasurementOutput.EvidenceSummary}|published=True");

            var fullSavePath = Path.GetFullPath(savePath);
            if (!workbench.TrySaveTeachingRecipe(fullSavePath, out var saveMessage))
            {
                return Complete(false, saveMessage);
            }
            lines.Add($"Save|path={fullSavePath}|message={saveMessage}");

            if (!workbench.TryOpenTeachingRecipe(fullSavePath, out var reopenMessage))
            {
                return Complete(false, reopenMessage);
            }
            var reopenedStep = workbench.PipelineSteps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PlaneFlatnessStepId, StringComparison.OrdinalIgnoreCase));
            workbench.SelectedPipelineStep = reopenedStep;
            var reopenedReference = workbench.Selections.Single(selection =>
                string.Equals(selection.Id, reopenedStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenedMeasurement = workbench.Selections.Single(selection =>
                string.Equals(selection.Id, reopenedStep.InputEntityIds[2], StringComparison.OrdinalIgnoreCase));
            var reopenedDocument = ToolRecipeDocumentStore.Load(fullSavePath);
            var reopenedDocumentStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PlaneFlatnessStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedPointPairStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PointPairStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedPointPair = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedPointPairStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenedGapStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.GapFlushStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedGapFirst = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedGapStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenedGapSecond = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedGapStep.InputEntityIds[2], StringComparison.OrdinalIgnoreCase));
            var reopenedVolumeStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.VolumeStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedVolumeReference = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedVolumeStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenedVolumeMeasurement = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedVolumeStep.InputEntityIds[2], StringComparison.OrdinalIgnoreCase));
            var reopenedCrossSectionStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.CrossSectionStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedCrossSection = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedCrossSectionStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenPassed = reopenedDocument.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && reopenedStep.InputEntityIds.Count == 3
                && reopenedDocumentStep.InputEntityIds.SequenceEqual(reopenedStep.InputEntityIds, StringComparer.OrdinalIgnoreCase)
                && reopenedReference.GridRectangle == referenceSelection.GridRectangle
                && reopenedMeasurement.GridRectangle == measurementSelection.GridRectangle
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedReference.SourceBinding)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedMeasurement.SourceBinding)
                && reopenedPointPair.Points?.Count == 2
                && reopenedPointPair.Points.SequenceEqual(pointPairSelection.Points)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedPointPair.SourceBinding)
                && reopenedGapFirst.GridRectangle == gapFirst.GridRectangle
                && reopenedGapSecond.GridRectangle == gapSecond.GridRectangle
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedGapFirst.SourceBinding)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedGapSecond.SourceBinding)
                && reopenedVolumeReference.GridRectangle == volumeReference.GridRectangle
                && reopenedVolumeMeasurement.GridRectangle == volumeMeasurement.GridRectangle
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedVolumeReference.SourceBinding)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedVolumeMeasurement.SourceBinding)
                && reopenedCrossSection.GridRectangle == crossSectionSelection.GridRectangle
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedCrossSection.SourceBinding)
                && !workbench.IsDirty
                && ReferenceEquals(previewBefore, viewer.ViewModel.PreviewToolResult)
                && ReferenceEquals(resultsBefore, viewer.ViewModel.ResultEntities);
            lines.Add($"Reopen|schema={reopenedDocument.SchemaVersion}|stepInputs={string.Join(';', reopenedStep.InputEntityIds)}|reference={reopenedReference.Id}|measurement={reopenedMeasurement.Id}|pointPair={reopenedPointPair.Id}|gapFirst={reopenedGapFirst.Id}|gapSecond={reopenedGapSecond.Id}|volumeReference={reopenedVolumeReference.Id}|volumeMeasurement={reopenedVolumeMeasurement.Id}|crossSection={reopenedCrossSection.Id}|dirty={workbench.IsDirty}|message={reopenMessage}");
            workbench.SelectedPipelineStep = workbench.PipelineSteps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.CrossSectionStepId, StringComparison.OrdinalIgnoreCase));
            viewer.ShowWorkbenchRegridHeightField(publishedA3, isPublished: true);
            syncAppliedTeachingSelections();
            await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            return Complete(
                reopenPassed,
                reopenPassed
                    ? "One live Shell session published synthetic A3, taught Plane Flatness and Point Pair through real Viewer pointer input, explicitly Previewed/Published Point Pair, Gap / Flush, Volume, and Cross-section Dimensions, then saved and reopened exact geometry/binding evidence."
                    : "Saved/reopened Plane Flatness, Point Pair, Gap / Flush, Volume, or Cross-section geometry, A3 binding, or explicit-execution boundary did not match.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or OverflowException)
        {
            return Complete(false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
