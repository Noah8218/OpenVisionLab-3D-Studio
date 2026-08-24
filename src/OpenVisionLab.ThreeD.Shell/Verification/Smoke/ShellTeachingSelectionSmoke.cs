using System.IO;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;

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
        if (mode == "coordinate-confidence-dual-target-applied")
        {
            return await RunCoordinateConfidenceDualTargetAsync(
                viewModel,
                viewer,
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
            var topGridView = viewer.ViewModel.IsTopOrthographicView;
            detail = $"CoordinateConfidence|topOrthographic={topGridView}|exactCandidateVisible={exactCandidateVisible}|row={workbench.TeachingGridRectangleRow}|column={workbench.TeachingGridRectangleColumn}|rowCount={workbench.TeachingGridRectangleRowCount}|columnCount={workbench.TeachingGridRectangleColumnCount}";
            return topGridView && exactCandidateVisible;
        }

        if (step is null)
        {
            return Complete(false, "No teaching pipeline step is selected.");
        }

        if (mode == "grid-polygon")
        {
            return await RunGridPolygonAsync(
                viewModel,
                viewer,
                workbench,
                step,
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
                    Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
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
                Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
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
        string? reportPath)
    {
        var selectionBefore = workbench.SelectedStepTeachingSelection;
        var polygonBefore = selectionBefore?.GridPolygon;
        var previewBefore = viewer.ViewModel.PreviewToolResult;
        var resultEntitiesBefore = viewer.ViewModel.ResultEntities;
        var dirtyBefore = workbench.IsDirty;
        var schemaBefore = workbench.RecipeSchemaVersion;
        var selectionCountBefore = workbench.Selections.Count;
        var inputIdsBefore = step.InputEntityIds.ToArray();
        var lines = new List<string>
        {
            "OpenVisionLab 3D GridPolygon authoring Shell smoke",
            "Mode=grid-polygon",
            $"Step={step.Id}",
            $"AuthoredBefore|dirty={dirtyBefore}|schema={schemaBefore}|selections={selectionCountBefore}|inputs={string.Join(';', inputIdsBefore)}",
            $"ExecutionBefore|preview={previewBefore.Status}|results={resultEntitiesBefore.Count}"
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
            ReferenceEquals(previewBefore, viewer.ViewModel.PreviewToolResult)
            && ReferenceEquals(resultEntitiesBefore, viewer.ViewModel.ResultEntities);

        bool Complete(bool passed, string message)
        {
            var state = viewer.TeachingCaptureSnapshot;
            lines.Add($"CaptureAfter|active={state.IsActive}|progress={state.CapturedPointCount}/{state.RequiredPointCount}|canApply={state.CanApply}|topOrthographic={viewer.ViewModel.IsTopOrthographicView}|message={state.Message}");
            lines.Add($"WorkbenchAfter|editorVisible={workbench.IsTeachingGridPolygonEditorVisible}|editorEnabled={workbench.IsTeachingGridPolygonEditorEnabled}|vertices={workbench.TeachingGridPolygonVertices.Count}|dirty={workbench.IsDirty}|schema={workbench.RecipeSchemaVersion}");
            lines.Add($"ExecutionAfter|preview={viewer.ViewModel.PreviewToolResult.Status}|results={viewer.ViewModel.ResultEntities.Count}|previewReferenceUnchanged={ExecutionUnchanged()}|authoredUnchanged={AuthoredUnchanged()}");
            lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{message}");
            ShellSmokeArtifacts.WriteTextReport(reportPath, lines, withoutBom: true);
            Console.WriteLine(lines[^1]);
            if (!passed)
            {
                viewModel.SetViewerSmokeFailed(message);
            }

            return passed;
        }

        if (step.ToolId != "grid-polygon-authoring"
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
            && viewer.ViewModel.IsTopOrthographicView
            && workbench.IsTeachingGridPolygonEditorEnabled
            && workbench.TeachingGridPolygonVertices.Count == originalVertices.Length
            && AuthoredUnchanged()
            && ExecutionUnchanged();
        lines.Add($"Begin|pass={initialPassed}|active={initialState.IsActive}|kind={initialState.Kind}|vertices={initialState.GridPolygon?.Vertices.Count ?? 0}|topOrthographic={viewer.ViewModel.IsTopOrthographicView}|editorEnabled={workbench.IsTeachingGridPolygonEditorEnabled}|authoredUnchanged={AuthoredUnchanged()}|executionUnchanged={ExecutionUnchanged()}");
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
        return Complete(
            appliedPassed,
            appliedPassed
                ? "GridPolygon numeric edit, reorder, add/remove, Cancel, restart, and explicit Apply were exercised in the current WPF shell; the same selection identity was preserved and Preview/Run remained untouched."
                : "GridPolygon explicit Apply did not preserve selection identity and the no-execution boundary.");
    }

    private static async Task<bool> RunCoordinateConfidenceDualTargetAsync(
        ShellMainWindowViewModel viewModel,
        OpenVisionThreeDViewerControl viewer,
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

        var startedPerspective = viewer.ViewModel.IsPerspectiveView;
        var previewBefore = viewer.ViewModel.PreviewToolResult;
        var resultsBefore = viewer.ViewModel.ResultEntities;
        var referenceId = referenceBefore.Id;
        var measurementId = measurementBefore.Id;
        lines.Add($"Start|perspective={startedPerspective}|reference={referenceTarget}|measurement={measurementTarget}");

        if (!workbench.RemovePlaneFlatnessMeasurementRoiCommand.CanExecute(null)
            || !workbench.RemovePlaneFlatnessReferenceRoiCommand.CanExecute(null))
        {
            return Complete(false, "The saved Thickness ROIs could not enter a clean dual-target reteach.");
        }
        workbench.RemovePlaneFlatnessMeasurementRoiCommand.Execute(null);
        workbench.RemovePlaneFlatnessReferenceRoiCommand.Execute(null);
        if (workbench.Selections.Any(selection =>
                string.Equals(selection.Id, referenceId, StringComparison.Ordinal)
                || string.Equals(selection.Id, measurementId, StringComparison.Ordinal))
            || !workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "The saved Thickness ROIs were not removed without changing the selected step.");
        }

        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var evidenceDirectory = string.IsNullOrWhiteSpace(reportPath)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(reportPath));
        var referencePointerReport = evidenceDirectory is null
            ? null
            : Path.Combine(evidenceDirectory, "coordinate-confidence-reference-pointer.txt");
        var referenceResult = await viewer.RunTeachingTargetRectanglePointerSmokeAsync(
            referenceTarget,
            referencePointerReport);
        var referenceCoordinatesVisible = CandidateMatchesWorkbench(
            workbench,
            referenceResult.Candidate);
        lines.Add($"ReferenceCandidate|top={viewer.ViewModel.IsTopOrthographicView}|coordinatesVisible={referenceCoordinatesVisible}|candidate={referenceResult.Candidate}");
        if (!referenceResult.Passed
            || !referenceCoordinatesVisible
            || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, $"Reference ROI one-drag teaching failed: {referenceResult.Failure}");
        }
        workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "Measurement ROI did not become the next explicit teaching role.");
        }
        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var measurementPointerReport = evidenceDirectory is null
            ? null
            : Path.Combine(evidenceDirectory, "coordinate-confidence-measurement-pointer.txt");
        var measurementResult = await viewer.RunTeachingTargetRectanglePointerSmokeAsync(
            measurementTarget,
            measurementPointerReport);
        var measurementCoordinatesVisible = CandidateMatchesWorkbench(
            workbench,
            measurementResult.Candidate);
        lines.Add($"MeasurementCandidate|top={viewer.ViewModel.IsTopOrthographicView}|coordinatesVisible={measurementCoordinatesVisible}|candidate={measurementResult.Candidate}");
        if (!measurementResult.Passed
            || !measurementCoordinatesVisible
            || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, $"Measurement ROI one-drag teaching failed: {measurementResult.Failure}");
        }
        workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        var routeRestored = step.InputEntityIds.Count == 3
            && string.Equals(step.InputEntityIds[1], referenceId, StringComparison.Ordinal)
            && string.Equals(step.InputEntityIds[2], measurementId, StringComparison.Ordinal);
        var executionUnchanged = ReferenceEquals(previewBefore, viewer.ViewModel.PreviewToolResult)
            && ReferenceEquals(resultsBefore, viewer.ViewModel.ResultEntities);
        lines.Add($"Final|routeRestored={routeRestored}|executionUnchanged={executionUnchanged}|captureActive={viewer.TeachingCaptureSnapshot.IsActive}");
        return Complete(
            startedPerspective
            && routeRestored
            && executionUnchanged
            && !viewer.TeachingCaptureSnapshot.IsActive,
            "Reference and Measurement ROIs were each retaught from an initially Perspective workflow with one actual Top-view drag, exact visible coordinates, explicit Apply, and no Preview or Run.");
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
}
