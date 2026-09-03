using System.IO;
using System.Windows;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the Height Image actual-pointer ROI Smoke scenario. The low-level
/// Windows pointer movement and WPF hit-testing remain in HeightImageViewerView;
/// this owner keeps lifecycle policy, state boundaries, save/reopen checks, and
/// report composition out of MainWindow.
/// </summary>
internal static class ShellHeightImageRoiPointerSmoke
{
    public static async Task<bool> RunAsync(
        string mode,
        string? reportPath,
        string? savePath,
        ToolWorkbenchViewModel workbench,
        ToolRecipeWorkbenchView workbenchView,
        OpenVisionThreeDViewerControl viewer,
        Dispatcher dispatcher)
    {
        var normalizedMode = mode.Trim().ToLowerInvariant();
        if (normalizedMode is not ("review" or "cancel" or "apply")
            || workbench.SelectedPipelineStep is not { } step
            || !workbench.IsSelectedStepThickness
            || workbench.PlaneFlatnessMeasurementSelection?.GridRectangle is not { } beforeGeometry)
        {
            return false;
        }

        var measurement = workbench.PlaneFlatnessMeasurementSelection;
        var beforeSelectionId = measurement.Id;
        var beforeRoute = step.InputEntityIdsText;
        var beforeDirty = workbench.IsDirty;
        var beforeStepCount = workbench.PipelineSteps.Count;
        var beforeSelectionCount = workbench.Selections.Count;
        var beforeOutput = workbench.CurrentMeasurementOutput;
        await workbench.HeightImageViewer.EnsureSourceAsync(
            workbench.Source.Path,
            workbench.Source.Id,
            workbench.Source.Unit,
            workbench.Source.FrameId);
        workbench.WorkspaceSelection.SelectRegion(
            InspectionWorkspaceRegionRole.Measurement,
            beforeSelectionId);
        if (!workbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null))
        {
            return false;
        }

        workbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(200);
        // Entering ROI capture may legitimately resize the linked Viewer to
        // focus the Height Image. Establish the camera baseline after that
        // presentation-only preparation, before the actual pointer gesture.
        var beforeCamera = (
            viewer.ViewModel.YawDegrees,
            viewer.ViewModel.PitchDegrees,
            viewer.ViewModel.CameraDistance,
            viewer.ViewModel.CameraTargetX,
            viewer.ViewModel.CameraTargetY,
            viewer.ViewModel.CameraTargetZ);
        var pointer = await workbenchView.RunHeightImageRoiPointerSmokeAsync();
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        ToolRecipeSelection? viewerCandidate = null;
        var viewerCandidateMessage = "Viewer candidate was not queried.";
        var candidateSynchronized =
            pointer.Passed
            && pointer.After is { } pointerCandidate
            && workbench.HeightImageViewer.RoiWorkspace.Candidate == pointerCandidate
            && viewer.TryGetC3DTeachingCandidate(
                out viewerCandidate,
                out viewerCandidateMessage)
            && viewerCandidate?.GridRectangle == pointerCandidate
            && workbench.HeightImageViewer.RoiWorkspace.Lifecycle
                == InspectionWorkspaceRegionLifecycleState.Review;
        var transientBoundary =
            workbench.IsDirty == beforeDirty
            && workbench.PipelineSteps.Count == beforeStepCount
            && workbench.Selections.Count == beforeSelectionCount
            && step.InputEntityIdsText == beforeRoute
            && workbench.Selections.Single(item => item.Id == beforeSelectionId).GridRectangle
                == beforeGeometry
            && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput);

        var actionPassed = candidateSynchronized && transientBoundary;
        var saved = false;
        var reopened = false;
        var actionMessage = normalizedMode;
        if (actionPassed && normalizedMode == "cancel")
        {
            workbench.HeightImageViewer.RoiWorkspace.CancelCommand.Execute(null);
            actionPassed =
                !workbench.IsTeachingSelectionCaptureActive
                && workbench.Selections.Single(item => item.Id == beforeSelectionId).GridRectangle
                    == beforeGeometry
                && workbench.IsDirty == beforeDirty
                && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput);
        }
        else if (actionPassed && normalizedMode == "apply")
        {
            workbench.HeightImageViewer.RoiWorkspace.ApplyCommand.Execute(null);
            var applied = workbench.Selections.SingleOrDefault(item =>
                item.Id == beforeSelectionId);
            actionPassed =
                applied?.GridRectangle == pointer.After
                && workbench.Selections.Count(item => item.Id == beforeSelectionId) == 1
                && !workbench.IsTeachingSelectionCaptureActive
                && workbench.PipelineSteps.Count == beforeStepCount
                && step.InputEntityIdsText == beforeRoute
                && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput);
            if (actionPassed && !string.IsNullOrWhiteSpace(savePath))
            {
                var fullSavePath = Path.GetFullPath(savePath);
                saved = workbench.TrySaveTeachingRecipe(fullSavePath, out actionMessage);
                var reopenedWorkbench = new ToolWorkbenchViewModel(
                    Path.Combine(
                        Path.GetDirectoryName(fullSavePath)!,
                        $"height-image-roi-reopen-{Environment.ProcessId}.json"));
                reopened = saved
                    && reopenedWorkbench.TryOpenTeachingRecipe(
                        fullSavePath,
                        out actionMessage)
                    && reopenedWorkbench.SelectPipelineStep(step.Id)
                    && reopenedWorkbench.Selections.SingleOrDefault(item =>
                        item.Id == beforeSelectionId)?.GridRectangle == pointer.After
                    && reopenedWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Any(item =>
                        item.SelectionId == beforeSelectionId
                        && item.Rectangle == pointer.After);
                actionPassed &= saved && reopened;
            }
        }

        var afterCamera = (
            viewer.ViewModel.YawDegrees,
            viewer.ViewModel.PitchDegrees,
            viewer.ViewModel.CameraDistance,
            viewer.ViewModel.CameraTargetX,
            viewer.ViewModel.CameraTargetY,
            viewer.ViewModel.CameraTargetZ);
        var cameraPassed = beforeCamera == afterCamera;
        var passed = actionPassed && cameraPassed;
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath)
                ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"HeightImageRoiPointerSmoke|{(passed ? "Pass" : "Fail")}|mode={normalizedMode}|actualWindowsPointer=true",
                    $"Pointer|pass={pointer.Passed}|start={pointer.StartScreenPoint}|end={pointer.EndScreenPoint}|failure={pointer.Failure}|target={pointer.TargetDiagnostic}",
                    $"Candidate|before={pointer.Before}|after={pointer.After}|heightImage={workbench.HeightImageViewer.RoiWorkspace.Candidate}|viewer={viewerCandidate?.GridRectangle}|viewerMessage={viewerCandidateMessage}|synchronized={candidateSynchronized}",
                    $"ReviewBoundary|pass={transientBoundary}|dirty={beforeDirty}->{workbench.IsDirty}|steps={beforeStepCount}->{workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{workbench.Selections.Count}|routeSame={step.InputEntityIdsText == beforeRoute}|appliedBeforeReview={beforeGeometry}|inspectionOutputSame={ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput)}",
                    $"Action|mode={normalizedMode}|pass={actionPassed}|selectionId={beforeSelectionId}|saved={saved}|reopened={reopened}|message={actionMessage}",
                    $"Camera|pass={cameraPassed}|before={beforeCamera}|after={afterCamera}",
                    $"Result={(passed ? "PASS" : "FAIL")}"
                ]);
        }

        return passed;
    }
}
