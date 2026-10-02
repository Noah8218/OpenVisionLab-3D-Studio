using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellTeachingSelectionSmoke
{
    private static string GetReportDirectory(string reportPath) =>
        Path.GetDirectoryName(Path.GetFullPath(reportPath))!;

    public static async Task<bool> RunAsync(
        ShellMainWindowViewModel viewModel,
        OpenVisionThreeDViewerControl viewer,
        FrameworkElement visualRoot,
        string modeValue,
        string? reportPath)
    {
        var mode = modeValue.Trim().ToLowerInvariant();
        var workbench = viewModel.Workbench;
        var step = workbench.SelectedPipelineStep;
        if (mode == "coordinate-confidence-dual-target-applied")
        {
            return await RunCoordinateConfidenceDualTargetAsync(
                viewModel,
                viewer,
                visualRoot,
                workbench,
                step,
                reportPath);
        }

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

        var inspectionBefore = viewer.HostState.Inspection;
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
            $"ExecutionBefore|preview={inspectionBefore.PreviewStatus}|results={inspectionBefore.PublishedResultCount}"
        };

        bool Complete(bool passed, string message)
        {
            var state = viewer.TeachingCaptureSnapshot;
            var previewUnchanged = viewer.HostState.Inspection.PreviewReferenceToken == inspectionBefore.PreviewReferenceToken;
            var resultsUnchanged = viewer.HostState.Inspection.PublishedResultReferenceToken == inspectionBefore.PublishedResultReferenceToken;
            lines.Add($"CaptureAfter|active={state.IsActive}|progress={state.CapturedPointCount}/{state.RequiredPointCount}|canUndo={state.CanUndo}|canApply={state.CanApply}|message={state.Message}");
            lines.Add($"AuthoredAfter|dirty={workbench.IsDirty}|schema={workbench.RecipeSchemaVersion}|selections={workbench.Selections.Count}|inputs={string.Join(';', step?.InputEntityIds ?? [])}");
            lines.Add($"ExecutionAfter|preview={viewer.HostState.Inspection.PreviewStatus}|results={viewer.HostState.Inspection.PublishedResultCount}|previewReferenceUnchanged={previewUnchanged}|resultReferenceUnchanged={resultsUnchanged}");
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
            viewer.HostState.Inspection.PreviewReferenceToken == inspectionBefore.PreviewReferenceToken
            && viewer.HostState.Inspection.PublishedResultReferenceToken == inspectionBefore.PublishedResultReferenceToken;

        bool CoordinateConfidenceReady(TeachingCaptureState state, out string detail)
        {
            var rectangle = state.Points is [var first, var second]
                ? new ToolRecipeGridRectangle(
                    Math.Min(first.Locator.Row, second.Locator.Row),
                    Math.Min(first.Locator.Column, second.Locator.Column),
                    Math.Abs(second.Locator.Row - first.Locator.Row) + 1,
                    Math.Abs(second.Locator.Column - first.Locator.Column) + 1)
                : null;
            var exactCandidateVisible = rectangle is not null
                && workbench.IsTeachingGridRectangleEditorEnabled
                && workbench.TeachingGridRectangleRow == rectangle.Row
                && workbench.TeachingGridRectangleColumn == rectangle.Column
                && workbench.TeachingGridRectangleRowCount == rectangle.RowCount
                && workbench.TeachingGridRectangleColumnCount == rectangle.ColumnCount;
            var topGridView = viewer.HostState.Inspection.IsTopOrthographicView;
            detail = $"CoordinateConfidence|topOrthographic={topGridView}|exactCandidateVisible={exactCandidateVisible}|row={workbench.TeachingGridRectangleRow}|column={workbench.TeachingGridRectangleColumn}|rowCount={workbench.TeachingGridRectangleRowCount}|columnCount={workbench.TeachingGridRectangleColumnCount}";
            return topGridView && exactCandidateVisible;
        }

        if (step is null)
        {
            return Complete(false, "No teaching pipeline step is selected.");
        }

        if (mode is "grid-polygon" or "grid-polygon-domain-e2e")
        {
            return await RunGridPolygonAsync(
                viewModel,
                viewer,
                workbench,
                step,
                mode == "grid-polygon-domain-e2e",
                reportPath);
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
                    GetReportDirectory(reportPath),
                    $"{Path.GetFileNameWithoutExtension(reportPath)}.drag-pointer.txt");
            if (!await viewer.RunTeachingRectangleDragPointerSmokeAsync(dragPointerReportPath))
            {
                return Complete(false, viewer.HostState.ViewerStatus);
            }

            var state = viewer.TeachingCaptureSnapshot;
            var coordinateConfidenceReady = CoordinateConfidenceReady(state, out var coordinateDetail);
            lines.Add(coordinateDetail);
            var readyPassed = state.CapturedPointCount == 2
                && state.RequiredPointCount == 2
                && state.CanApply
                && coordinateConfidenceReady
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
                GetReportDirectory(reportPath),
                $"{Path.GetFileNameWithoutExtension(reportPath)}.cancel-pointer.txt");
        if (!await viewer.RunTeachingCapturePointerSmokeAsync(
                cancelWhenReady: false,
                cancelPointerReportPath))
        {
            return Complete(false, viewer.HostState.ViewerStatus);
        }

        var cancelCandidateState = viewer.TeachingCaptureSnapshot;
        var cancelCandidateCoordinatesReady = CoordinateConfidenceReady(
            cancelCandidateState,
            out var cancelCandidateCoordinateDetail);
        lines.Add($"First{cancelCandidateCoordinateDetail}");
        if (!cancelCandidateState.IsActive
            || cancelCandidateState.CapturedPointCount != 2
            || !cancelCandidateState.CanApply
            || !cancelCandidateCoordinatesReady
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
        var candidateCoordinatesReady = CoordinateConfidenceReady(
            candidateState,
            out var candidateCoordinateDetail);
        lines.Add($"Second{candidateCoordinateDetail}");
        if (candidateState.CapturedPointCount != 2
            || !candidateState.CanApply
            || !candidateCoordinatesReady
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

    private static async Task<bool> RunGridPolygonAsync(
        ShellMainWindowViewModel viewModel,
        OpenVisionThreeDViewerControl viewer,
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        bool runDomainExecution,
        string? reportPath)
    {
        var selectionBefore = workbench.SelectedStepTeachingSelection;
        var polygonBefore = selectionBefore?.GridPolygon;
        var inspectionBefore = viewer.HostState.Inspection;
        var dirtyBefore = workbench.IsDirty;
        var schemaBefore = workbench.RecipeSchemaVersion;
        var selectionCountBefore = workbench.Selections.Count;
        var inputIdsBefore = step.InputEntityIds.ToArray();
        var isGridPolygonDomain = string.Equals(step.ToolId, "grid-polygon-domain", StringComparison.Ordinal);
        var lines = new List<string>
        {
            $"OpenVisionLab 3D GridPolygon {(isGridPolygonDomain ? "domain" : "authoring")} Shell smoke",
            "Mode=grid-polygon",
            $"Step={step.Id}",
            $"AuthoredBefore|dirty={dirtyBefore}|schema={schemaBefore}|selections={selectionCountBefore}|inputs={string.Join(';', inputIdsBefore)}",
            $"ExecutionBefore|preview={inspectionBefore.PreviewStatus}|results={inspectionBefore.PublishedResultCount}"
        };

        static bool SameVertices(
            IReadOnlyList<ToolRecipeGridPolygonVertex>? actual,
            IReadOnlyList<ToolRecipeGridPolygonVertex> expected) =>
            actual is not null && actual.SequenceEqual(expected);

        bool AuthoredUnchanged() =>
            workbench.IsDirty == dirtyBefore
            && string.Equals(workbench.RecipeSchemaVersion, schemaBefore, StringComparison.Ordinal)
            && workbench.Selections.Count == selectionCountBefore
            && step.InputEntityIds.SequenceEqual(inputIdsBefore, StringComparer.OrdinalIgnoreCase)
            && selectionBefore is not null
            && workbench.SelectedStepTeachingSelection is { } current
            && string.Equals(current.Id, selectionBefore.Id, StringComparison.Ordinal)
            && SameVertices(current.GridPolygon?.Vertices, polygonBefore?.Vertices ?? []);

        bool ExecutionUnchanged() =>
            viewer.HostState.Inspection.PreviewReferenceToken == inspectionBefore.PreviewReferenceToken
            && viewer.HostState.Inspection.PublishedResultReferenceToken == inspectionBefore.PublishedResultReferenceToken;

        bool Complete(bool passed, string message)
        {
            var state = viewer.TeachingCaptureSnapshot;
            lines.Add($"CaptureAfter|active={state.IsActive}|progress={state.CapturedPointCount}/{state.RequiredPointCount}|canApply={state.CanApply}|topOrthographic={viewer.HostState.Inspection.IsTopOrthographicView}|message={state.Message}");
            lines.Add($"WorkbenchAfter|editorVisible={workbench.IsTeachingGridPolygonEditorVisible}|editorEnabled={workbench.IsTeachingGridPolygonEditorEnabled}|vertices={workbench.TeachingGridPolygonVertices.Count}|dirty={workbench.IsDirty}|schema={workbench.RecipeSchemaVersion}");
            lines.Add($"ExecutionAfter|preview={viewer.HostState.Inspection.PreviewStatus}|results={viewer.HostState.Inspection.PublishedResultCount}|previewReferenceUnchanged={ExecutionUnchanged()}|authoredUnchanged={AuthoredUnchanged()}");
            lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{message}");
            ShellSmokeArtifacts.WriteTextReport(reportPath, lines, withoutBom: true);
            Console.WriteLine(lines[^1]);
            if (!passed)
            {
                viewModel.SetViewerSmokeFailed(message);
            }

            return passed;
        }

        if (step.ToolId is not ("grid-polygon-authoring" or "grid-polygon-domain")
            || selectionBefore is not { GridPolygon: { Vertices.Count: >= ToolRecipeGridPolygonGeometry.MinimumVertexCount } }
            || polygonBefore is null
            || !workbench.IsTeachingGridPolygonEditorVisible
            || workbench.TeachingGridPolygonVertices.Count != polygonBefore.Vertices.Count)
        {
            return Complete(false, "GridPolygon smoke requires a selected persisted polygon and visible Workbench editor.");
        }

        var originalVertices = polygonBefore.Vertices.ToArray();
        if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "The selected GridPolygon step could not begin Viewer teaching capture.");
        }

        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var initialState = viewer.TeachingCaptureSnapshot;
        var initialPassed = initialState is
            {
                IsActive: true,
                Kind: ToolRecipeSelectionKinds.GridPolygon,
                CanApply: true,
                GridPolygon: { Vertices: var initialVertices }
            }
            && SameVertices(initialVertices, originalVertices)
            && viewer.HostState.Inspection.IsTopOrthographicView
            && workbench.IsTeachingGridPolygonEditorEnabled
            && workbench.TeachingGridPolygonVertices.Count == originalVertices.Length
            && AuthoredUnchanged()
            && ExecutionUnchanged();
        lines.Add($"Begin|pass={initialPassed}|active={initialState.IsActive}|kind={initialState.Kind}|vertices={initialState.GridPolygon?.Vertices.Count ?? 0}|topOrthographic={viewer.HostState.Inspection.IsTopOrthographicView}|editorEnabled={workbench.IsTeachingGridPolygonEditorEnabled}|authoredUnchanged={AuthoredUnchanged()}|executionUnchanged={ExecutionUnchanged()}");
        if (!initialPassed)
        {
            return Complete(false, "GridPolygon capture did not restore the ordered persisted candidate without changing authored or execution state.");
        }

        var editedItem = workbench.TeachingGridPolygonVertices[1];
        editedItem.Column += 10.0;
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var editedVertices = workbench.TeachingGridPolygonVertices
            .Select(item => new ToolRecipeGridPolygonVertex(item.Row, item.Column))
            .ToArray();
        var editedState = viewer.TeachingCaptureSnapshot;
        var numericEditPassed = editedState.IsActive
            && editedState.CanApply
            && SameVertices(editedState.GridPolygon?.Vertices, editedVertices)
            && editedVertices[1].Column.Equals(originalVertices[1].Column + 10.0)
            && workbench.TeachingGridPolygonVertices[1].Order == 2
            && AuthoredUnchanged()
            && ExecutionUnchanged();
        lines.Add($"NumericEdit|pass={numericEditPassed}|column={editedVertices[1].Column:R}|candidateVertices={editedState.GridPolygon?.Vertices.Count ?? 0}|authoredUnchanged={AuthoredUnchanged()}|executionUnchanged={ExecutionUnchanged()}");
        if (!numericEditPassed)
        {
            return Complete(false, "Numeric GridPolygon editing did not update the transient Viewer candidate while preserving the authored recipe.");
        }

        var longPolygonVertices = Enumerable.Range(0, ToolRecipeGridPolygonGeometry.MaximumVertexCount)
            .Select(index =>
            {
                var angle = index * Math.PI * 2.0 / ToolRecipeGridPolygonGeometry.MaximumVertexCount;
                return new ToolRecipeGridPolygonVertex(
                    420.0 + 200.0 * Math.Sin(angle),
                    640.0 + 500.0 * Math.Cos(angle));
            })
            .ToArray();
        workbench.UpdateTeachingGridPolygonDraft(new ToolRecipeGridPolygon(longPolygonVertices));
        workbench.TeachingGridPolygonVertices[^1].Column += 0.0001;
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var longCountState = viewer.TeachingCaptureSnapshot;
        var longCountPassed = longCountState.IsActive
            && longCountState.CanApply
            && longCountState.GridPolygon?.Vertices.Count == ToolRecipeGridPolygonGeometry.MaximumVertexCount
            && workbench.TeachingGridPolygonVertices.Count == ToolRecipeGridPolygonGeometry.MaximumVertexCount
            && workbench.IsTeachingGridPolygonDraftValid
            && AuthoredUnchanged()
            && ExecutionUnchanged();
        lines.Add($"LongCount|pass={longCountPassed}|vertices={longCountState.GridPolygon?.Vertices.Count ?? 0}|editorVertices={workbench.TeachingGridPolygonVertices.Count}|draftValid={workbench.IsTeachingGridPolygonDraftValid}|authoredUnchanged={AuthoredUnchanged()}|executionUnchanged={ExecutionUnchanged()}");
        if (!longCountPassed)
        {
            return Complete(false, "GridPolygon maximum-vertex editing did not remain valid and transient.");
        }

        workbench.UpdateTeachingGridPolygonDraft(new ToolRecipeGridPolygon(editedVertices));
        var restoreTrigger = workbench.TeachingGridPolygonVertices[0];
        var restoreRow = restoreTrigger.Row;
        restoreTrigger.Row = restoreRow + 0.0001;
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        workbench.TeachingGridPolygonVertices[0].Row = restoreRow;
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var restoredCountPassed = SameVertices(viewer.TeachingCaptureSnapshot.GridPolygon?.Vertices, editedVertices)
            && workbench.TeachingGridPolygonVertices.Count == editedVertices.Length
            && workbench.IsTeachingGridPolygonDraftValid
            && workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null)
            && AuthoredUnchanged()
            && ExecutionUnchanged();
        lines.Add($"LongCountRestore|pass={restoredCountPassed}|sameVertices={SameVertices(viewer.TeachingCaptureSnapshot.GridPolygon?.Vertices, editedVertices)}|draftValid={workbench.IsTeachingGridPolygonDraftValid}|canApply={workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null)}|vertices={viewer.TeachingCaptureSnapshot.GridPolygon?.Vertices.Count ?? 0}|editorVertices={workbench.TeachingGridPolygonVertices.Count}|authoredUnchanged={AuthoredUnchanged()}|executionUnchanged={ExecutionUnchanged()}");
        if (!restoredCountPassed)
        {
            return Complete(false, "GridPolygon maximum-vertex editing did not restore the six-vertex transient candidate.");
        }

        var reorderItem = workbench.TeachingGridPolygonVertices[1];
        workbench.MoveTeachingGridPolygonVertexDownCommand.Execute(reorderItem);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var reorderedVertices = editedVertices
            .Select((vertex, index) => (vertex, index))
            .OrderBy(entry => entry.index == 1 ? 2 : entry.index == 2 ? 1 : entry.index)
            .Select(entry => entry.vertex)
            .ToArray();
        var reorderedState = viewer.TeachingCaptureSnapshot;
        var reorderRejectedPassed = reorderedState.IsActive
            && SameVertices(reorderedState.GridPolygon?.Vertices, editedVertices)
            && !SameVertices(reorderedState.GridPolygon?.Vertices, reorderedVertices)
            && workbench.TeachingGridPolygonVertices[1].Row.Equals(editedVertices[2].Row)
            && workbench.TeachingGridPolygonVertices[2].Column.Equals(editedVertices[1].Column)
            && !workbench.IsTeachingGridPolygonDraftValid
            && !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null)
            && AuthoredUnchanged()
            && ExecutionUnchanged();
        lines.Add($"ReorderDown|pass={reorderRejectedPassed}|candidateUnchanged={SameVertices(reorderedState.GridPolygon?.Vertices, editedVertices)}|draftValid={workbench.IsTeachingGridPolygonDraftValid}|applyEnabled={workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null)}|authoredUnchanged={AuthoredUnchanged()}|executionUnchanged={ExecutionUnchanged()}");
        if (!reorderRejectedPassed)
        {
            return Complete(false, "GridPolygon reorder did not fail closed when the edited order became self-intersecting.");
        }

        workbench.MoveTeachingGridPolygonVertexUpCommand.Execute(workbench.TeachingGridPolygonVertices[2]);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var restoredOrderState = viewer.TeachingCaptureSnapshot;
        var reorderRestorePassed = SameVertices(restoredOrderState.GridPolygon?.Vertices, editedVertices)
            && workbench.TeachingGridPolygonVertices[1].Column.Equals(editedVertices[1].Column)
            && workbench.TeachingGridPolygonVertices[2].Column.Equals(editedVertices[2].Column)
            && workbench.IsTeachingGridPolygonDraftValid
            && workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null)
            && AuthoredUnchanged()
            && ExecutionUnchanged();
        lines.Add($"ReorderUp|pass={reorderRestorePassed}|vertices={restoredOrderState.GridPolygon?.Vertices.Count ?? 0}|authoredUnchanged={AuthoredUnchanged()}|executionUnchanged={ExecutionUnchanged()}");
        if (!reorderRestorePassed)
        {
            return Complete(false, "GridPolygon reorder restore did not return the transient candidate to its edited order.");
        }

        var addRemovePassed = false;
        if (workbench.AddTeachingGridPolygonVertexCommand.CanExecute(null))
        {
            workbench.AddTeachingGridPolygonVertexCommand.Execute(null);
            await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var addedCount = workbench.TeachingGridPolygonVertices.Count;
            var addedCandidate = viewer.TeachingCaptureSnapshot.GridPolygon?.Vertices.Count == addedCount;
            var lastVertex = workbench.TeachingGridPolygonVertices.LastOrDefault();
            if (addedCandidate
                && lastVertex is not null
                && workbench.RemoveTeachingGridPolygonVertexCommand.CanExecute(lastVertex))
            {
                workbench.RemoveTeachingGridPolygonVertexCommand.Execute(lastVertex);
                await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                addRemovePassed = workbench.TeachingGridPolygonVertices.Count == editedVertices.Length
                    && SameVertices(viewer.TeachingCaptureSnapshot.GridPolygon?.Vertices, editedVertices)
                    && AuthoredUnchanged()
                    && ExecutionUnchanged();
            }
        }
        lines.Add($"AddRemove|pass={addRemovePassed}|vertices={workbench.TeachingGridPolygonVertices.Count}|authoredUnchanged={AuthoredUnchanged()}|executionUnchanged={ExecutionUnchanged()}");
        if (!addRemovePassed)
        {
            return Complete(false, "GridPolygon add/remove did not retain a valid ordered transient candidate and authored boundary.");
        }

        workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var cancelPassed = !viewer.TeachingCaptureSnapshot.IsActive
            && !workbench.IsTeachingSelectionCaptureActive
            && AuthoredUnchanged()
            && ExecutionUnchanged();
        lines.Add($"Cancel|pass={cancelPassed}|captureActive={viewer.TeachingCaptureSnapshot.IsActive}|authoredUnchanged={AuthoredUnchanged()}|executionUnchanged={ExecutionUnchanged()}");
        if (!cancelPassed)
        {
            return Complete(false, "GridPolygon Cancel changed authored or execution state.");
        }

        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var applyItem = workbench.TeachingGridPolygonVertices[1];
        applyItem.Column += 12.0;
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var applyCandidate = viewer.TeachingCaptureSnapshot.GridPolygon;
        var readyToApply = viewer.TeachingCaptureSnapshot.IsActive
            && viewer.TeachingCaptureSnapshot.CanApply
            && applyCandidate is not null
            && !SameVertices(applyCandidate.Vertices, originalVertices)
            && workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null);
        lines.Add($"ApplyReady|pass={readyToApply}|vertices={applyCandidate?.Vertices.Count ?? 0}|canApply={viewer.TeachingCaptureSnapshot.CanApply}");
        if (!readyToApply)
        {
            return Complete(false, "GridPolygon Apply did not receive a valid edited candidate after Cancel and restart.");
        }

        workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var appliedSelection = workbench.SelectedStepTeachingSelection;
        var appliedPassed = !viewer.TeachingCaptureSnapshot.IsActive
            && !workbench.IsTeachingSelectionCaptureActive
            && workbench.IsDirty
            && appliedSelection is not null
            && selectionBefore is not null
            && string.Equals(appliedSelection.Id, selectionBefore.Id, StringComparison.Ordinal)
            && appliedSelection.GridPolygon is not null
            && !SameVertices(appliedSelection.GridPolygon.Vertices, originalVertices)
            && step.InputEntityIds.Contains(appliedSelection.Id, StringComparer.OrdinalIgnoreCase)
            && ExecutionUnchanged();
        lines.Add($"Apply|pass={appliedPassed}|captureActive={viewer.TeachingCaptureSnapshot.IsActive}|dirty={workbench.IsDirty}|sameSelectionId={string.Equals(appliedSelection?.Id, selectionBefore?.Id, StringComparison.Ordinal)}|executionUnchanged={ExecutionUnchanged()}");
        if (appliedPassed && runDomainExecution)
        {
            appliedPassed = await RunGridPolygonDomainExecutionAsync(
                viewer,
                workbench,
                step,
                reportPath,
                lines);
        }

        return Complete(
            appliedPassed,
            appliedPassed
                ? runDomainExecution
                    ? "GridPolygon teaching was followed by explicit Preview, Publish, ordered Run, Run Record creation, and recipe/artifact reopen in the current WPF shell."
                    : "GridPolygon numeric edit, reorder, add/remove, Cancel, restart, and explicit Apply were exercised in the current WPF shell; the same selection identity was preserved and Preview/Run remained untouched."
                : "GridPolygon explicit Apply did not preserve selection identity and the no-execution boundary.");
    }

    private static async Task<bool> RunGridPolygonDomainExecutionAsync(
        OpenVisionThreeDViewerControl viewer,
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        string? reportPath,
        ICollection<string> lines)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            lines.Add("DomainExecution|pass=False|reason=report-path-required");
            return false;
        }

        var recipePath = Path.Combine(
            GetReportDirectory(reportPath),
            $"{Path.GetFileNameWithoutExtension(reportPath)}.e2e.ov3d-teach.json");
        var savePassed = workbench.TrySaveTeachingRecipe(recipePath, out var saveMessage)
            && !workbench.IsDirty
            && File.Exists(recipePath);
        lines.Add($"DomainSave|pass={savePassed}|path={recipePath}|dirty={workbench.IsDirty}|message={saveMessage}");
        if (!savePassed)
        {
            return false;
        }

        var previewCommandReady = workbench.PreviewSelectedStepCommand.CanExecute(null);
        workbench.PreviewSelectedStepCommand.Execute(null);
        var previewCompleted = await WaitForConditionAsync(
            () => !workbench.IsSelectedStepPreviewRunning,
            TimeSpan.FromMinutes(2));
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var previewArtifact = workbench.CurrentGridPolygonDomainArtifact;
        var previewPath = workbench.CurrentGridPolygonDomainArtifactPath;
        var previewPassed = previewCommandReady
            && previewCompleted
            && workbench.HasCurrentGridPolygonDomainPreview
            && !workbench.IsGridPolygonDomainPreviewPublished
            && previewArtifact is not null
            && string.Equals(previewArtifact.ArtifactId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            && previewPath is not null
            && File.Exists(previewPath);
        lines.Add($"DomainPreview|pass={previewPassed}|commandReady={previewCommandReady}|completed={previewCompleted}|hasPreview={workbench.HasCurrentGridPolygonDomainPreview}|published={workbench.IsGridPolygonDomainPreviewPublished}|artifact={previewArtifact?.ArtifactId ?? "(none)"}|cells={previewArtifact?.Cells.Count ?? 0}|path={previewPath ?? "(none)"}");
        if (!previewPassed)
        {
            return false;
        }

        var publishCommandReady = workbench.PublishSelectedStepCommand.CanExecute(null);
        workbench.PublishSelectedStepCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var publishedPath = workbench.CurrentGridPolygonDomainArtifactPath;
        var publishPassed = publishCommandReady
            && workbench.IsGridPolygonDomainPreviewPublished
            && publishedPath is not null
            && File.Exists(publishedPath);
        lines.Add($"DomainPublish|pass={publishPassed}|commandReady={publishCommandReady}|published={workbench.IsGridPolygonDomainPreviewPublished}|path={publishedPath ?? "(none)"}");
        if (!publishPassed)
        {
            return false;
        }

        var runCommandReady = workbench.RunTeachingRecipeCommand.CanExecute(null);
        workbench.RunTeachingRecipeCommand.Execute(null);
        var runCompleted = await WaitForConditionAsync(
            () => !workbench.IsOrderedRunRunning
                && workbench.HasOrderedRunResult
                && workbench.CurrentOrderedRunRecordPath is { } path
                && File.Exists(path),
            TimeSpan.FromMinutes(2));
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var runResult = workbench.CurrentOrderedRunResult;
        var domainStepResult = runResult?.Steps.FirstOrDefault(result =>
            string.Equals(result.ToolId, "grid-polygon-domain", StringComparison.Ordinal));
        var maskStepResult = runResult?.Steps.FirstOrDefault(result =>
            string.Equals(result.ToolId, "domain-mask", StringComparison.Ordinal));
        var runPassed = runCommandReady
            && runCompleted
            && runResult is not null
            && runResult.Status == ResultStatus.Pass
            && domainStepResult?.Result.Status == ResultStatus.Pass
            && maskStepResult?.Result.Status == ResultStatus.Pass;
        lines.Add($"DomainRun|pass={runPassed}|commandReady={runCommandReady}|completed={runCompleted}|status={runResult?.Status.ToString() ?? "(none)"}|domainStatus={domainStepResult?.Result.Status.ToString() ?? "(none)"}|maskStatus={maskStepResult?.Result.Status.ToString() ?? "(none)"}|record={workbench.CurrentOrderedRunRecordPath ?? "(none)"}");
        if (!runPassed)
        {
            return false;
        }

        using var reopened = new ToolWorkbenchViewModel();
        var reopenedRecipe = reopened.TryOpenTeachingRecipe(recipePath, out var reopenMessage);
        var selectedAgain = reopenedRecipe && reopened.SelectPipelineStep(step.Id);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var restoredArtifact = reopened.CurrentGridPolygonDomainArtifact;
        var reopenPassed = reopenedRecipe
            && selectedAgain
            && !reopened.IsDirty
            && reopened.HasCurrentGridPolygonDomainPreview
            && reopened.IsGridPolygonDomainPreviewPublished
            && restoredArtifact is not null
            && string.Equals(restoredArtifact.ArtifactId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            && reopened.CurrentGridPolygonDomainArtifactPath is { } restoredPath
            && File.Exists(restoredPath);
        lines.Add($"DomainReopen|pass={reopenPassed}|recipe={reopenedRecipe}|selected={selectedAgain}|dirty={reopened.IsDirty}|hasPreview={reopened.HasCurrentGridPolygonDomainPreview}|published={reopened.IsGridPolygonDomainPreviewPublished}|artifact={restoredArtifact?.ArtifactId ?? "(none)"}|path={reopened.CurrentGridPolygonDomainArtifactPath ?? "(none)"}|message={reopenMessage}");
        return reopenPassed;
    }

    private static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        return condition();
    }

    private static async Task<bool> RunCoordinateConfidenceDualTargetAsync(
        ShellMainWindowViewModel viewModel,
        OpenVisionThreeDViewerControl viewer,
        FrameworkElement visualRoot,
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem? step,
        string? reportPath)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D coordinate-confident dual-target teaching smoke",
            $"Step={step?.Id ?? "(none)"}"
        };

        bool Complete(bool passed, string message)
        {
            lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{message}");
            ShellSmokeArtifacts.WriteTextReport(reportPath, lines, withoutBom: true);
            Console.WriteLine(lines[^1]);
            if (!passed)
            {
                viewModel.SetViewerSmokeFailed(message);
            }
            return passed;
        }

        if (step is not { ToolId: "thickness" }
            || step.InputEntityIds.Count != 3
            || workbench.Selections.FirstOrDefault(selection =>
                string.Equals(selection.Id, step.InputEntityIds[1], StringComparison.Ordinal))
                is not { GridRectangle: { } referenceTarget } referenceBefore
            || workbench.Selections.FirstOrDefault(selection =>
                string.Equals(selection.Id, step.InputEntityIds[2], StringComparison.Ordinal))
                is not { GridRectangle: { } measurementTarget } measurementBefore)
        {
            return Complete(false, "The dual-target smoke requires one complete selected Thickness step.");
        }

        var startedPerspective = viewer.HostState.Inspection.IsPerspectiveView;
        var inspectionBefore = viewer.HostState.Inspection;
        var referenceId = referenceBefore.Id;
        var measurementId = measurementBefore.Id;
        var referenceRetarget = CreateDisjointVerticalTarget(
            referenceTarget,
            referenceBefore.SourceBinding.GridHeight);
        var measurementRetarget = CreateDisjointVerticalTarget(
            measurementTarget,
            measurementBefore.SourceBinding.GridHeight);
        lines.Add($"Start|perspective={startedPerspective}|reference={referenceTarget}->{referenceRetarget}|measurement={measurementTarget}->{measurementRetarget}");

        if (!workbench.CapturePlaneFlatnessReferenceRoiCommand.CanExecute(null))
        {
            return Complete(false, "The saved Reference ROI could not enter explicit reteach.");
        }

        workbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        for (var index = 0;
             index < 2 && workbench.UndoTeachingSelectionCaptureCommand.CanExecute(null);
             index++)
        {
            workbench.UndoTeachingSelectionCaptureCommand.Execute(null);
        }
        var evidenceDirectory = string.IsNullOrWhiteSpace(reportPath)
            ? null
            : GetReportDirectory(reportPath);
        var referencePointerReport = evidenceDirectory is null
            ? null
            : Path.Combine(evidenceDirectory, "coordinate-confidence-reference-pointer.txt");
        var referenceResult = await viewer.RunTeachingTargetRectanglePointerSmokeAsync(
            referenceRetarget,
            referencePointerReport);
        var referenceCoordinatesVisible = CandidateMatchesWorkbench(
            workbench,
            referenceResult.Candidate);
        lines.Add($"ReferenceCandidate|top={viewer.HostState.Inspection.IsTopOrthographicView}|coordinatesVisible={referenceCoordinatesVisible}|candidate={referenceResult.Candidate}");
        if (!referenceResult.Passed
            || !referenceCoordinatesVisible
            || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, $"Reference ROI one-drag teaching failed: {referenceResult.Failure}");
        }
        workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        if (!workbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null))
        {
            return Complete(false, "Measurement ROI did not become the next explicit teaching role.");
        }
        workbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        for (var index = 0;
             index < 2 && workbench.UndoTeachingSelectionCaptureCommand.CanExecute(null);
             index++)
        {
            workbench.UndoTeachingSelectionCaptureCommand.Execute(null);
        }
        var measurementPointerReport = evidenceDirectory is null
            ? null
            : Path.Combine(evidenceDirectory, "coordinate-confidence-measurement-pointer.txt");
        var measurementResult = await viewer.RunTeachingTargetRectanglePointerSmokeAsync(
            measurementRetarget,
            measurementPointerReport);
        var measurementCoordinatesVisible = CandidateMatchesWorkbench(
            workbench,
            measurementResult.Candidate);
        lines.Add($"MeasurementCandidate|top={viewer.HostState.Inspection.IsTopOrthographicView}|coordinatesVisible={measurementCoordinatesVisible}|candidate={measurementResult.Candidate}");
        if (!measurementResult.Passed
            || !measurementCoordinatesVisible
            || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, $"Measurement ROI one-drag teaching failed: {measurementResult.Failure}");
        }
        if (!await ExerciseGridRectangleKeyboardAsync(
                visualRoot,
                workbench,
                measurementResult.Candidate!,
                lines))
        {
            return Complete(false, "Measurement ROI numeric keyboard editing, focus traversal, Apply, or Cancel failed.");
        }

        var routeRestored = step.InputEntityIds.Count == 3
            && string.Equals(step.InputEntityIds[1], referenceId, StringComparison.Ordinal)
            && string.Equals(step.InputEntityIds[2], measurementId, StringComparison.Ordinal);
        var executionUnchanged = viewer.HostState.Inspection.PreviewReferenceToken == inspectionBefore.PreviewReferenceToken
            && viewer.HostState.Inspection.PublishedResultReferenceToken == inspectionBefore.PublishedResultReferenceToken;
        lines.Add($"Final|routeRestored={routeRestored}|executionUnchanged={executionUnchanged}|captureActive={viewer.TeachingCaptureSnapshot.IsActive}");
        return Complete(
            startedPerspective
            && routeRestored
            && executionUnchanged
            && !viewer.TeachingCaptureSnapshot.IsActive,
            "Reference and Measurement ROIs were each retaught from an initially Perspective workflow with actual Top-view drag, exact visible coordinates, native Tab/Shift+Tab numeric editing, Enter Apply, Escape Cancel, and no Preview or Run.");
    }

    private static async Task<bool> ExerciseGridRectangleKeyboardAsync(
        FrameworkElement visualRoot,
        ToolWorkbenchViewModel workbench,
        ToolRecipeGridRectangle candidate,
        ICollection<string> lines)
    {
        var editors = FindVisualDescendants<TextBox>(visualRoot)
            .Where(editor => editor.IsVisible && editor.IsEnabled)
            .Select(editor => new
            {
                Editor = editor,
                Path = editor.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path
            })
            .ToArray();
        TextBox? FindEditor(string path) => editors
            .FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.Ordinal))
            ?.Editor;

        var columnEditor = FindEditor(nameof(workbench.TeachingGridRectangleColumn));
        var rowEditor = FindEditor(nameof(workbench.TeachingGridRectangleRow));
        var columnCountEditor = FindEditor(nameof(workbench.TeachingGridRectangleColumnCount));
        var rowCountEditor = FindEditor(nameof(workbench.TeachingGridRectangleRowCount));
        var window = Window.GetWindow(visualRoot);
        if (window is null
            || columnEditor is null
            || rowEditor is null
            || columnCountEditor is null
            || rowCountEditor is null)
        {
            lines.Add($"KeyboardNumeric|pass=False|reason=editors-unavailable|visibleBindings={string.Join(',', editors.Select(item => item.Path).Where(path => path is not null))}");
            return false;
        }

        columnEditor.BringIntoView();
        window.UpdateLayout();
        window.Activate();
        var windowHandle = new WindowInteropHelper(window).Handle;
        var foregrounded = SetForegroundWindow(windowHandle);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        if (!foregrounded
            || !IsForegroundWindow(windowHandle)
            || !columnEditor.Focus()
            || !columnEditor.IsKeyboardFocusWithin)
        {
            lines.Add($"KeyboardNumeric|pass=False|reason=foreground-or-focus|foregrounded={foregrounded}|foreground={IsForegroundWindow(windowHandle)}|focused={columnEditor.IsKeyboardFocusWithin}");
            return false;
        }

        const byte controlKey = 0x11;
        const byte shiftKey = 0x10;
        const byte tabKey = 0x09;
        const byte enterKey = 0x0D;
        const byte escapeKey = 0x1B;
        const byte aKey = 0x41;
        void KeyDown(byte key) => SendKeyboardEvent(key, 0, 0, UIntPtr.Zero);
        void KeyUp(byte key) => SendKeyUp(key);
        void SelectAll()
        {
            KeyDown(controlKey);
            KeyDown(aKey);
            KeyUp(aKey);
            KeyUp(controlKey);
        }
        void TypeInteger(int value)
        {
            foreach (var character in value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                var key = (byte)(0x30 + character - '0');
                KeyDown(key);
                KeyUp(key);
            }
        }
        void Press(byte key)
        {
            KeyDown(key);
            KeyUp(key);
        }

        var editedColumn = candidate.Column + 1;
        SelectAll();
        TypeInteger(editedColumn);
        Press(tabKey);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
        var tabPassed = ReferenceEquals(Keyboard.FocusedElement, rowEditor);
        var editedCandidate = candidate with { Column = editedColumn };
        var editPassed = CandidateMatchesWorkbench(workbench, editedCandidate);

        KeyDown(shiftKey);
        Press(tabKey);
        KeyUp(shiftKey);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        var shiftTabPassed = ReferenceEquals(Keyboard.FocusedElement, columnEditor);

        SelectAll();
        TypeInteger(candidate.Column);
        Press(tabKey);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
        var restored = CandidateMatchesWorkbench(workbench, candidate);
        var enterForeground = IsForegroundWindow(windowHandle);
        var enterFocusedElement = Keyboard.FocusedElement?.GetType().Name ?? "(none)";
        var canApplyBeforeEnter = workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null);
        if (enterForeground)
        {
            Press(enterKey);
        }
        await Task.Delay(120);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var enterApplied = !workbench.IsTeachingSelectionCaptureActive;

        var escapeCanceled = false;
        if (!enterApplied && IsForegroundWindow(windowHandle))
        {
            Press(escapeKey);
            await Task.Delay(120);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            escapeCanceled = !workbench.IsTeachingSelectionCaptureActive;
        }
        else if (enterApplied && workbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null))
        {
            workbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (columnEditor.Focus()
                && columnEditor.IsKeyboardFocusWithin
                && IsForegroundWindow(windowHandle))
            {
                Press(escapeKey);
                await Task.Delay(120);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                escapeCanceled = !workbench.IsTeachingSelectionCaptureActive;
            }
        }

        var passed = tabPassed
            && editPassed
            && shiftTabPassed
            && restored
            && enterApplied
            && escapeCanceled;
        lines.Add($"KeyboardNumeric|pass={passed}|foreground=True|tab={tabPassed}|shiftTab={shiftTabPassed}|edit={editPassed}|restored={restored}|enterForeground={enterForeground}|enterFocus={enterFocusedElement}|canApplyBeforeEnter={canApplyBeforeEnter}|enterApply={enterApplied}|escapeCancel={escapeCanceled}|column={candidate.Column}->{editedColumn}->{workbench.TeachingGridRectangleColumn}");
        return passed;
    }

    private static bool CandidateMatchesWorkbench(
        ToolWorkbenchViewModel workbench,
        ToolRecipeGridRectangle? candidate) =>
        candidate is not null
        && workbench.IsTeachingGridRectangleEditorEnabled
        && workbench.TeachingGridRectangleRow == candidate.Row
        && workbench.TeachingGridRectangleColumn == candidate.Column
        && workbench.TeachingGridRectangleRowCount == candidate.RowCount
        && workbench.TeachingGridRectangleColumnCount == candidate.ColumnCount;

    private static ToolRecipeGridRectangle CreateDisjointVerticalTarget(
        ToolRecipeGridRectangle current,
        int gridHeight)
    {
        const int overlayHitTestClearance = 64;
        var after = current.Row + current.RowCount + overlayHitTestClearance;
        if (after + current.RowCount <= gridHeight)
        {
            return current with { Row = after };
        }

        return current with
        {
            Row = Math.Max(0, current.Row - current.RowCount - overlayHitTestClearance)
        };
    }
}
