using System.IO;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellTeachingSelectionSmoke
{
    public static async Task<bool> RunAsync(
        ShellMainWindowViewModel viewModel,
        OpenVisionThreeDViewerControl viewer,
        string modeValue,
        string? reportPath)
    {
        var mode = modeValue.Trim().ToLowerInvariant();
        var workbench = viewModel.Workbench;
        var step = workbench.SelectedPipelineStep;
        var isDualMeasurementCreate = mode == "dual-measurement-create-applied";
        if (isDualMeasurementCreate)
        {
            if (step is not { ToolId: "thickness" }
                || !workbench.RemovePlaneFlatnessMeasurementRoiCommand.CanExecute(null))
            {
                viewModel.SetViewerSmokeFailed(
                    "Dual Measurement ROI creation smoke requires a complete selected Thickness step.");
                return false;
            }

            workbench.RemovePlaneFlatnessMeasurementRoiCommand.Execute(null);
        }

        var previewBefore = viewer.ViewModel.PreviewToolResult;
        var resultEntitiesBefore = viewer.ViewModel.ResultEntities;
        var dirtyBefore = workbench.IsDirty;
        var schemaBefore = workbench.RecipeSchemaVersion;
        var selectionCountBefore = workbench.Selections.Count;
        var inputIdsBefore = step?.InputEntityIds.ToArray() ?? [];
        var selectionBefore = workbench.SelectedStepTeachingSelection;
        var isReplacementCapture = mode is "replace-capturing" or "replace-drag-ready" or "replace-drag-applied";
        var lines = new List<string>
        {
            "OpenVisionLab 3D generic teaching-selection Shell smoke",
            $"Mode={mode}",
            $"Step={step?.Id ?? "(none)"}",
            $"AuthoredBefore|dirty={dirtyBefore}|schema={schemaBefore}|selections={selectionCountBefore}|inputs={string.Join(';', inputIdsBefore)}",
            $"ExecutionBefore|preview={previewBefore.Status}|results={resultEntitiesBefore.Count}"
        };

        bool Complete(bool passed, string message)
        {
            var state = viewer.TeachingCaptureSnapshot;
            var previewUnchanged = ReferenceEquals(previewBefore, viewer.ViewModel.PreviewToolResult);
            var resultsUnchanged = ReferenceEquals(resultEntitiesBefore, viewer.ViewModel.ResultEntities);
            lines.Add($"CaptureAfter|active={state.IsActive}|progress={state.CapturedPointCount}/{state.RequiredPointCount}|canUndo={state.CanUndo}|canApply={state.CanApply}|message={state.Message}");
            lines.Add($"AuthoredAfter|dirty={workbench.IsDirty}|schema={workbench.RecipeSchemaVersion}|selections={workbench.Selections.Count}|inputs={string.Join(';', step?.InputEntityIds ?? [])}");
            lines.Add($"ExecutionAfter|preview={viewer.ViewModel.PreviewToolResult.Status}|results={viewer.ViewModel.ResultEntities.Count}|previewReferenceUnchanged={previewUnchanged}|resultReferenceUnchanged={resultsUnchanged}");
            lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{message}");
            ShellSmokeArtifacts.WriteTextReport(reportPath, lines, withoutBom: true);
            Console.WriteLine(lines[^1]);
            if (!passed)
            {
                viewModel.SetViewerSmokeFailed(message);
            }

            return passed;
        }

        bool AuthoredStateUnchanged() =>
            workbench.IsDirty == dirtyBefore
            && string.Equals(workbench.RecipeSchemaVersion, schemaBefore, StringComparison.Ordinal)
            && workbench.Selections.Count == selectionCountBefore
            && (step?.InputEntityIds ?? []).SequenceEqual(inputIdsBefore, StringComparer.OrdinalIgnoreCase);

        bool ExecutionStateUnchanged() =>
            ReferenceEquals(previewBefore, viewer.ViewModel.PreviewToolResult)
            && ReferenceEquals(resultEntitiesBefore, viewer.ViewModel.ResultEntities);

        if (step is null)
        {
            return Complete(false, "No teaching pipeline step is selected.");
        }

        if ((!isDualMeasurementCreate && dirtyBefore)
            || (!isReplacementCapture
                && !isDualMeasurementCreate
                && (!string.Equals(schemaBefore, ToolRecipeDocument.LegacySchemaVersion, StringComparison.Ordinal)
                    || selectionCountBefore != 0))
            || (isReplacementCapture && selectionCountBefore == 0))
        {
            return Complete(
                false,
                isReplacementCapture
                    ? "Replacement-capture smoke requires a clean recipe with an existing authored selection."
                    : "Teaching-selection smoke requires a clean legacy 1.0 recipe with no authored selections.");
        }

        if (mode == "inactive")
        {
            var passed = !viewer.TeachingCaptureSnapshot.IsActive
                && AuthoredStateUnchanged()
                && ExecutionStateUnchanged();
            return Complete(
                passed,
                passed
                    ? "Inactive state retained legacy authored data and did not run Preview or Run."
                    : "Inactive state changed capture, authored recipe, or execution evidence.");
        }

        if (mode is not ("capturing" or "replace-capturing" or "replace-drag-ready" or "replace-drag-applied" or "applied" or "dual-measurement-create-applied"))
        {
            return Complete(false, $"Unsupported teaching-selection smoke mode: {modeValue}");
        }

        if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "The selected step cannot begin Viewer teaching capture.");
        }

        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        if (!viewer.TeachingCaptureSnapshot.IsActive)
        {
            return Complete(false, "Viewer teaching capture did not become active.");
        }

        if (mode is "replace-drag-ready" or "replace-drag-applied")
        {
            var dragPointerReportPath = string.IsNullOrWhiteSpace(reportPath)
                ? null
                : Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
                    $"{Path.GetFileNameWithoutExtension(reportPath)}.drag-pointer.txt");
            if (!await viewer.RunTeachingRectangleDragPointerSmokeAsync(dragPointerReportPath))
            {
                return Complete(false, viewer.HostState.ViewerStatus);
            }

            var state = viewer.TeachingCaptureSnapshot;
            var readyPassed = state.CapturedPointCount == 2
                && state.RequiredPointCount == 2
                && state.CanApply
                && AuthoredStateUnchanged()
                && ExecutionStateUnchanged();
            if (mode == "replace-drag-applied")
            {
                if (!readyPassed || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
                {
                    return Complete(false, "The edited replacement candidate was not ready for explicit Apply.");
                }

                workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
                var replacementAppliedSelection = selectionBefore is null
                    ? null
                    : workbench.Selections.FirstOrDefault(selection =>
                        string.Equals(selection.Id, selectionBefore.Id, StringComparison.Ordinal));
                var appliedPassed = !viewer.TeachingCaptureSnapshot.IsActive
                    && workbench.IsDirty
                    && workbench.Selections.Count == selectionCountBefore
                    && replacementAppliedSelection is not null
                    && selectionBefore is not null
                    && string.Equals(replacementAppliedSelection.Id, selectionBefore.Id, StringComparison.Ordinal)
                    && replacementAppliedSelection.GridRectangle != selectionBefore.GridRectangle
                    && ExecutionStateUnchanged();
                return Complete(
                    appliedPassed,
                    appliedPassed
                        ? "Actual left-drag edited the transient ROI and explicit Apply replaced the same selection identity without Preview, Publish, or Run."
                        : "Explicit Apply did not preserve replacement identity and the no-execution boundary.");
            }

            return Complete(
                readyPassed,
                readyPassed
                    ? "Actual left-drag produced a ready 2-corner replacement ROI; Apply remains explicit and authored/execution state is unchanged."
                    : "GridRectangle drag did not retain a ready transient candidate and the authored/execution boundary.");
        }

        if (mode is "capturing" or "replace-capturing")
        {
            var state = viewer.TeachingCaptureSnapshot;
            var expectedPointCount = isReplacementCapture ? 2 : 0;
            var passed = state.CapturedPointCount == expectedPointCount
                && state.RequiredPointCount == 2
                && state.CanApply == isReplacementCapture
                && AuthoredStateUnchanged()
                && ExecutionStateUnchanged();
            return Complete(
                passed,
                passed
                    ? isReplacementCapture
                        ? "Replacement capture is seeded at 2/2 from the authored ROI; the editable candidate did not dirty the recipe or run inspection."
                        : "Capture ribbon is active at 0/2; transient capture did not dirty the recipe or run inspection."
                    : "Capturing state violated the transient authored/execution boundary.");
        }

        var cancelPointerReportPath = string.IsNullOrWhiteSpace(reportPath)
            ? null
            : Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
                $"{Path.GetFileNameWithoutExtension(reportPath)}.cancel-pointer.txt");
        if (!await viewer.RunTeachingCapturePointerSmokeAsync(
                cancelWhenReady: false,
                cancelPointerReportPath))
        {
            return Complete(false, viewer.HostState.ViewerStatus);
        }

        var cancelCandidateState = viewer.TeachingCaptureSnapshot;
        if (!cancelCandidateState.IsActive
            || cancelCandidateState.CapturedPointCount != 2
            || !cancelCandidateState.CanApply
            || !workbench.CancelTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "Actual Viewer pointer capture did not produce a cancellable 2-point candidate.");
        }

        workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var cancelBoundaryPassed = !viewer.TeachingCaptureSnapshot.IsActive
            && !workbench.IsTeachingSelectionCaptureActive
            && AuthoredStateUnchanged()
            && ExecutionStateUnchanged();
        lines.Add($"CancelBoundary|pass={cancelBoundaryPassed}|authoredUnchanged={AuthoredStateUnchanged()}|executionUnchanged={ExecutionStateUnchanged()}");
        if (!cancelBoundaryPassed)
        {
            return Complete(false, "Cancel after two real Viewer picks changed authored or execution state.");
        }

        if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "The selected step could not restart Viewer teaching capture after Cancel.");
        }

        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        if (!viewer.TeachingCaptureSnapshot.IsActive
            || !await viewer.RunTeachingCapturePointerSmokeAsync(exerciseNavigationGestures: false))
        {
            return Complete(false, "Viewer teaching capture could not restart and produce a second candidate after Cancel.");
        }

        var candidateState = viewer.TeachingCaptureSnapshot;
        if (candidateState.CapturedPointCount != 2
            || !candidateState.CanApply
            || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "Second actual Viewer pointer capture did not produce an applicable 2-point candidate.");
        }

        workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        viewer.ResetView();
        viewer.FitAll();
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        lines.Add("EvidenceView|reset=True|fitAll=True|inspectionExecution=False");
        var appliedSelection = workbench.SelectedStepTeachingSelection;
        var applied = !viewer.TeachingCaptureSnapshot.IsActive
            && appliedSelection is not null
            && workbench.Selections.Count == selectionCountBefore + 1
            && string.Equals(workbench.RecipeSchemaVersion, ToolRecipeDocument.CurrentSchemaVersion, StringComparison.Ordinal)
            && workbench.IsDirty
            && step.InputEntityIds.Contains(appliedSelection.Id, StringComparer.OrdinalIgnoreCase)
            && ExecutionStateUnchanged();
        return Complete(
            applied,
            applied
                ? isDualMeasurementCreate
                    ? "The deleted Thickness Measurement ROI was recreated with real Viewer pointer input and explicit Apply; Preview/Run remained untouched."
                    : "Two real Viewer picks were applied, the recipe uses the current structured-selection schema, the step route became dirty, and Preview/Run remained untouched."
                : "Applying the Viewer candidate did not satisfy recipe persistence or execution-boundary checks.");
    }

}
