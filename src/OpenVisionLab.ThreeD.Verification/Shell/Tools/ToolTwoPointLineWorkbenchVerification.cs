using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

internal static class ToolTwoPointLineWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D 2-Point Line Workbench verification" };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "TwoPointLineWorkbench", Guid.NewGuid().ToString("N"));
        ToolWorkbenchViewModel? workbench = null;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", 3, 3, Enumerable.Repeat(1d, 9).ToArray());
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var firstSelection = CreateSelection(source, "selection.line-a", (0, 0), (0, 2));
            var secondSelection = CreateSelection(source, "selection.line-b", (0, 1), (2, 1));
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "2-Point Line Workbench",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [
                    TwoPointLineStep("step.line.a", firstSelection.Id, "derived.line.a", "FirstEdge"),
                    TwoPointLineStep("step.line.b", secondSelection.Id, "derived.line.b", "SecondEdge"),
                    IntersectionStep()
                ],
                [firstSelection, secondSelection]);
            var recipePath = Path.Combine(root, "two-point-line.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            workbench = new ToolWorkbenchViewModel();
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var open), open);
            workbench.SelectPipelineStep("step.line.a");
            Check("2-Point Line uses typed WPG adapter", workbench.SelectedStepPropertyDraft is TwoPointLineStepProperties, workbench.SelectedStepAdapterStatus);
            Check("2-Point Line exposes exact ordered selection", workbench.TwoPointLineSelectionSummary.Contains("row 0, column 0", StringComparison.Ordinal) && workbench.TwoPointLineSelectionSummary.Contains("row 0, column 2", StringComparison.Ordinal), workbench.TwoPointLineSelectionSummary);

            var firstStep = workbench.SelectedPipelineStep!;
            var secondStep = workbench.PipelineSteps.Single(step => step.Id == "step.line.b");
            var secondRouteBefore = secondStep.InputEntityIdsText;
            workbench.BeginFlowConnectionCommand.Execute(secondSelection.Id);
            workbench.ConnectFlowOutputCommand.Execute(firstStep.FlowInputs[1]);
            Check("point-set port changes only input slot 1 and never runs inspection",
                firstStep.InputEntityIds.SequenceEqual([source.EntityId, secondSelection.Id])
                && secondStep.InputEntityIdsText == secondRouteBefore && !workbench.HasPendingFlowConnection
                && workbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")),
                string.Join(";", firstStep.InputEntityIds));
            workbench.BeginFlowConnectionCommand.Execute(firstSelection.Id);
            workbench.ConnectFlowOutputCommand.Execute(firstStep.FlowInputs[1]);
            Check("selection output restores the same slot without changing the height input",
                firstStep.InputEntityIds.SequenceEqual([source.EntityId, firstSelection.Id]) && !workbench.HasPendingFlowConnection,
                string.Join(";", firstStep.InputEntityIds));
            workbench.BeginFlowConnectionCommand.Execute(source.EntityId);
            workbench.ConnectFlowOutputCommand.Execute(firstStep.FlowInputs[1]);
            Check("wrong-type connection preserves both slots and keeps cancel available",
                firstStep.InputEntityIds.SequenceEqual([source.EntityId, firstSelection.Id])
                && workbench.HasPendingFlowConnection && workbench.FlowConnectionMessage == workbench.Localization.FlowConnectionRejected,
                workbench.FlowConnectionMessage);
            workbench.CancelFlowConnectionCommand.Execute(null);
            workbench.BeginFlowConnectionCommand.Execute(secondSelection.Id);
            var invalidPort = new ToolWorkbenchFlowInputItem(firstStep, 99, "PointSet(2)", firstSelection.Id);
            Check("out-of-range input slot is disabled and retains the stored route",
                !workbench.ConnectFlowOutputCommand.CanExecute(invalidPort) && firstStep.InputEntityIds.SequenceEqual([source.EntityId, firstSelection.Id]),
                string.Join(";", firstStep.InputEntityIds));
            workbench.CancelFlowConnectionCommand.Execute(null);

            var displayCount = 0;
            C3DTwoPointLineFeature? lastDisplayed = null;
            C3DTwoPointLineFeature? firstPreview = null;
            C3DTwoPointLineFeature? firstPublished = null;
            workbench.TwoPointLineDisplayRequested += (_, args) =>
            {
                displayCount++;
                lastDisplayed = args.Output;
                if (args.Output.OutputEntityId == "derived.line.a")
                {
                    if (args.IsPublished) firstPublished = args.Output;
                    else firstPreview = args.Output;
                }
            };
            Check("first 2-Point Line becomes preview-ready from source plus PointSet", workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.TwoPointLineExecutionSummary);
            Check("explicit first 2-Point Line Preview", workbench.PreviewSelectedTwoPointLineAsync().GetAwaiter().GetResult() && firstPreview is not null && firstPreview.OriginKind == C3DLineOriginKind.PickedPoints && NearlyEqual(firstPreview.SegmentLength, 2), workbench.TwoPointLineExecutionSummary);
            var expectedFirst = ToolRecipeTwoPointLineExecution.Execute(document, "step.line.a", root);
            Check("Workbench and headless share first line identity", firstPreview?.ContentSha256 == expectedFirst.Output?.ContentSha256, $"workbench={firstPreview?.ContentSha256};headless={expectedFirst.Output?.ContentSha256}");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("first Publish reuses exact Preview", workbench.IsTwoPointLinePreviewPublished && ReferenceEquals(firstPreview, firstPublished) && workbench.TryGetPublishedTwoPointLineOutput("derived.line.a", out var publishedA) && ReferenceEquals(firstPublished, publishedA), workbench.TwoPointLineExecutionSummary);

            workbench.SelectPipelineStep("step.line.b");
            Check("unexecuted picked-line sibling has no selected output", !workbench.HasCurrentTwoPointLinePreview && workbench.CurrentTwoPointLineOutput is null && !workbench.IsTwoPointLinePreviewPublished, workbench.TwoPointLineExecutionSummary);
            Check("second 2-Point Line explicit Preview", workbench.PreviewSelectedTwoPointLineAsync().GetAwaiter().GetResult(), workbench.TwoPointLineExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("second Publish preserves first independent line", workbench.IsTwoPointLinePreviewPublished && workbench.TryGetPublishedTwoPointLineOutput("derived.line.a", out var retainedA) && ReferenceEquals(retainedA, firstPublished) && workbench.TryGetPublishedTwoPointLineOutput("derived.line.b", out var publishedB) && publishedB is not null, workbench.TwoPointLineExecutionSummary);
            Check("published 2-Point Lines appear as typed artifacts", workbench.ArtifactRegistry.Count(item => item.Contract == "LineFeature" && item.State == "Published") == 2, workbench.ArtifactRegistrySummary);

            workbench.TryGetPublishedTwoPointLineOutput("derived.line.b", out publishedB);
            var logsBeforeRestore = workbench.RunLog.Count;
            workbench.SelectPipelineStep("step.line.a");
            Check("picked-line reselection restores exact A without execution", workbench.HasCurrentTwoPointLinePreview
                && workbench.IsTwoPointLinePreviewPublished && ReferenceEquals(workbench.CurrentTwoPointLineOutput, firstPublished)
                && ReferenceEquals(lastDisplayed, firstPublished) && workbench.RunLog.Count == logsBeforeRestore, workbench.TwoPointLineExecutionSummary);
            workbench.SelectPipelineStep("step.line.b");
            Check("picked-line reselection restores exact B without execution", workbench.HasCurrentTwoPointLinePreview
                && workbench.IsTwoPointLinePreviewPublished && ReferenceEquals(workbench.CurrentTwoPointLineOutput, publishedB)
                && ReferenceEquals(lastDisplayed, publishedB) && workbench.RunLog.Count == logsBeforeRestore, workbench.TwoPointLineExecutionSummary);
            var displaysBeforeMetadata = displayCount;
            workbench.SelectedPipelineStep!.ToolName = "Renamed picked line";
            workbench.RecipeName = "Renamed picked-line recipe";
            Check("picked-line metadata preserves both published outputs", workbench.TryGetPublishedTwoPointLineOutput("derived.line.a", out _)
                && workbench.TryGetPublishedTwoPointLineOutput("derived.line.b", out _) && displayCount == displaysBeforeMetadata
                && workbench.RunLog.Count == logsBeforeRestore, workbench.TwoPointLineExecutionSummary);
            Check("explicit B rerun after metadata prepares independent case", workbench.PreviewSelectedTwoPointLineAsync().GetAwaiter().GetResult(), workbench.TwoPointLineExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectPipelineStep("step.line.a");
            workbench.PipelineSteps.Single(step => step.Id == "step.line.b").Parameters.Single(parameter => parameter.Name == "OutputRole").Value = "SecondEdgeRevised";
            Check("inactive picked-line edit invalidates only B", workbench.TryGetPublishedTwoPointLineOutput("derived.line.a", out var currentA)
                && ReferenceEquals(currentA, firstPublished) && ReferenceEquals(workbench.CurrentTwoPointLineOutput, firstPublished)
                && !workbench.TryGetPublishedTwoPointLineOutput("derived.line.b", out _), workbench.TwoPointLineExecutionSummary);
            workbench.SelectPipelineStep("step.line.b");
            Check("changed picked line recovers by explicit rerun", workbench.PreviewSelectedTwoPointLineAsync().GetAwaiter().GetResult(), workbench.TwoPointLineExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);

            var lateDocument = document;
            var lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.line.a");
            var changeOnStart = 1;
            var lateDisplays = 0;
            var disposeOnDisplay = false;
            var stateCallbacksAfterDispose = 0;
            ToolWorkbenchTwoPointLineExecutionOwner? ownerForCallback = null;
            using (var lateOwner = new ToolWorkbenchTwoPointLineExecutionOwner(
                () => true, () => lateSelected, () => true, () => false, () => firstSelection,
                id => workbench.PipelineSteps.FirstOrDefault(step => step.OutputEntityId == id),
                () => lateDocument, () => recipePath,
                (_, message) =>
                {
                    if (!message.Contains("Preview started", StringComparison.Ordinal)) return;
                    if (changeOnStart == 1)
                        lateDocument = document with { Steps = document.Steps.Select(step => step.Id == "step.line.a"
                            ? step with { Parameters = step.Parameters.Select(parameter => parameter.Name == "OutputRole" ? parameter with { Value = "ChangedDuringRun" } : parameter).ToArray() }
                            : step).ToArray() };
                    else if (changeOnStart == 2)
                        lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.line.b");
                }, _ => { lateDisplays++; if (disposeOnDisplay) ownerForCallback!.Dispose(); }, () => { }, () => { }, () => { }, () => { },
                () => { if (ownerForCallback?.IsDisposed == true) stateCallbacksAfterDispose++; }))
            {
                ownerForCallback = lateOwner;
                Check("late picked-line condition change rejects completion", !lateOwner.PreviewAsync().GetAwaiter().GetResult()
                    && lateOwner.CurrentOutput is null && !lateOwner.IsPreviewRunning && lateDisplays == 0, lateOwner.ExecutionSummary);
                lateDocument = document;
                lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.line.a");
                changeOnStart = 2;
                Check("late picked-line selection change rejects completion", !lateOwner.PreviewAsync().GetAwaiter().GetResult()
                    && lateOwner.CurrentOutput is null && !lateOwner.IsPreviewRunning && lateDisplays == 0, lateOwner.ExecutionSummary);
                lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.line.a");
                changeOnStart = 0;
                var recovered = lateOwner.PreviewAsync().GetAwaiter().GetResult();
                var recoveredOutput = lateOwner.CurrentOutput;
                lateOwner.Publish();
                Check("picked-line rejection recovers exact captured output", recovered && recoveredOutput?.ContentSha256 == expectedFirst.Output?.ContentSha256
                    && lateOwner.TryGetPublishedOutput("derived.line.a", out var recoveredLine)
                    && ReferenceEquals(recoveredOutput, recoveredLine) && lateDisplays == 2, lateOwner.ExecutionSummary);
                lateDocument = document with { Steps = document.Steps.Where(step => step.Id != "step.line.a").ToArray() };
                Check("picked-line lookup rejects removed execution context", !lateOwner.TryGetPublishedOutput("derived.line.a", out _), lateOwner.ExecutionSummary);
                lateDocument = document;
                lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.line.b");
                lateOwner.RefreshState();
                lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.line.a");
                disposeOnDisplay = true;
                lateOwner.RefreshState();
                Check("picked-line display restoration honors callback disposal", lateOwner.IsDisposed && lateOwner.CurrentOutput is null
                    && !lateOwner.TryGetPublishedOutput("derived.line.a", out _) && stateCallbacksAfterDispose == 0, $"disposed={lateOwner.IsDisposed};lateStateCallbacks={stateCallbacksAfterDispose}");
            }

            workbench.SelectPipelineStep("step.corner.01");
            Check("Line Intersection accepts two picked published lines", workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.LineIntersectionUpstreamSummary);
            Check("generic published-line contract retains picked origins", workbench.TryGetCurrentLineIntersectionInputs(out var firstInput, out var secondInput) && firstInput?.OriginKind == C3DLineOriginKind.PickedPoints && secondInput?.OriginKind == C3DLineOriginKind.PickedPoints, $"first={firstInput?.OriginKind};second={secondInput?.OriginKind}");
            Check("explicit picked-line Intersection Preview", workbench.PreviewSelectedLineIntersectionAsync().GetAwaiter().GetResult() && workbench.CurrentLineIntersectionOutput is { } corner && NearlyEqual(corner.CornerAnchorX, 1) && NearlyEqual(corner.CornerAnchorY, 1) && NearlyEqual(corner.CornerAnchorZ, 0), workbench.LineIntersectionEvidenceSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("picked-line Intersection Publish", workbench.IsLineIntersectionPreviewPublished, workbench.LineIntersectionExecutionSummary);
            Check("Corner keeps the verified source background without treating a line as a height field",
                workbench.EffectiveMainViewerContentId == source.EntityId && workbench.HasEffectiveMainGeometry
                && workbench.GetMainViewerCandidate("derived.line.a") is null && workbench.GetMainViewerCandidate("derived.corner.01") is null,
                $"input={workbench.WorkspaceSelection.SelectedInputEntityId};background={workbench.EffectiveMainViewerContentId}");
            var displaysBeforeCornerReturn = displayCount;
            var logsBeforeCornerReturn = workbench.RunLog.Count;
            workbench.SelectPipelineStep("step.line.b");
            Check("Corner to B restores the exact published line without another execution",
                displayCount == displaysBeforeCornerReturn + 1 && workbench.RunLog.Count == logsBeforeCornerReturn
                && workbench.CurrentTwoPointLineOutput?.OutputEntityId == "derived.line.b"
                && workbench.EffectiveMainViewerContentId == source.EntityId,
                $"displays={displayCount}/{displaysBeforeCornerReturn};logs={workbench.RunLog.Count}/{logsBeforeCornerReturn}");
            workbench.SelectPipelineStep("step.line.b");
            Check("same selected B does not repeat its display restoration", displayCount == displaysBeforeCornerReturn + 1, $"displays={displayCount}");


            workbench.SelectPipelineStep("step.line.a");
            workbench.SelectPipelineStep("step.line.b");
            var removedB = workbench.ConfirmSelectedStepRemoval("step.line.b");
            var restoredB = workbench.TryRestoreLastRemovedStep(out var restoreMessage);
            Check("picked-line remove restore retains A without restoring B calculation", removedB && restoredB
                && workbench.TryGetPublishedTwoPointLineOutput("derived.line.a", out var afterRemoveA) && ReferenceEquals(afterRemoveA, firstPublished)
                && !workbench.TryGetPublishedTwoPointLineOutput("derived.line.b", out _)
                && !workbench.TryGetPublishedLineIntersectionOutput("derived.corner.01", out _), restoreMessage);
            workbench.SelectPipelineStep("step.line.b");
            Check("restored picked line requires explicit rerun", workbench.PreviewSelectedTwoPointLineAsync().GetAwaiter().GetResult(), workbench.TwoPointLineExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectPipelineStep("step.corner.01");
            Check("picked-line restored route recovers intersection", workbench.PreviewSelectedLineIntersectionAsync().GetAwaiter().GetResult(), workbench.LineIntersectionExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectPipelineStep("step.line.a");
            workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "OutputRole").Value = "FirstEdgeRevised";
            Check("2-Point Line parameter change stales its Preview and downstream corner", workbench.IsTwoPointLinePreviewStale && !workbench.TryGetPublishedTwoPointLineOutput("derived.line.a", out _) && workbench.IsLineIntersectionPreviewStale && !workbench.TryGetPublishedLineIntersectionOutput("derived.corner.01", out _), workbench.TwoPointLineExecutionSummary);

            var replacementSourcePath = Path.Combine(root, "replacement.c3d");
            C3DHeightFieldSnapshot.CreateForVerification("source.replacement", 2, 2, [1d, 2d, 3d, 4d]).SaveC3D(replacementSourcePath);
            workbench.SetC3DSource(replacementSourcePath);
            Check("source replacement clears every published 2-Point Line", !workbench.TryGetPublishedTwoPointLineOutput("derived.line.b", out _) && !workbench.TryGetPublishedLineGeometry("derived.line.b", out _), workbench.TwoPointLineExecutionSummary);

            workbench.Dispose();
            Check(
                "Workbench disposal clears 2-Point Line state",
                workbench.CurrentTwoPointLineOutput is null
                    && !workbench.TryGetPublishedTwoPointLineOutput("derived.line.a", out _)
                    && !workbench.TryGetPublishedTwoPointLineOutput("derived.line.b", out _)
                    && !workbench.IsTwoPointLinePreviewRunning
                    && !workbench.IsTwoPointLinePreviewStale
                    && !workbench.IsTwoPointLinePreviewPublished,
                workbench.TwoPointLineExecutionSummary);
            var previewAfterDispose = workbench.PreviewSelectedTwoPointLineAsync().GetAwaiter().GetResult();
            Check(
                "post-disposal 2-Point Line routes reject work",
                !previewAfterDispose && !workbench.PreviewSelectedStepCommand.CanExecute(null),
                workbench.TwoPointLineExecutionSummary);
            workbench.Dispose();
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        finally
        {
            workbench?.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        summary = $"3D 2-Point Line Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static ToolRecipeStep TwoPointLineStep(string id, string selectionId, string outputId, string outputRole) =>
        new(id, "two-point-line", "2-Point Line", 1, ["source.synthetic", selectionId], outputId, [new("OutputRole", outputRole), new("ConstructionPolicy", "OrderedPointsDefineSegment")]);

    private static ToolRecipeStep IntersectionStep() =>
        new("step.corner.01", "line-intersection", "Line Intersection", 2, ["derived.line.a", "derived.line.b"], "derived.corner.01", [new("MaximumClosestApproachDistance", "0.001"), new("MinimumAcuteAngleDegrees", "45"), new("MaximumSupportExtension", "0"), new("OutputRole", "SyntheticCorner"), new("ClosestApproachPolicy", "MidpointOfClosestPoints"), new("ParallelPolicy", "RejectBelowMinimumAcuteAngle"), new("SupportPolicy", "WithinInlierProjectionExtentsWithMaximumExtension")]);

    private static ToolRecipeSelection CreateSelection(C3DHeightFieldSnapshot source, string id, (int Row, int Column) first, (int Row, int Column) second) =>
        new(id, id, ToolRecipeSelectionKinds.PointSet, source.EntityId, source.FrameId, new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), null,
            [CreatePoint(source, first.Row, first.Column), CreatePoint(source, second.Row, second.Column)], null);

    private static ToolRecipeSelectionPoint CreatePoint(C3DHeightFieldSnapshot source, int row, int column)
    {
        var height = source.Values.Span[row * source.Width + column];
        return new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", row, column), new ToolRecipeXyz(column, height, row), height);
    }

    private static bool NearlyEqual(double actual, double expected) => Math.Abs(actual - expected) <= 0.000001;
}
