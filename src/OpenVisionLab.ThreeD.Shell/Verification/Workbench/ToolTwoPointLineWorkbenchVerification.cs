using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolTwoPointLineWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D 2-Point Line Workbench verification" };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "TwoPointLineWorkbench", Guid.NewGuid().ToString("N"));
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

            var workbench = new ToolWorkbenchViewModel();
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var open), open);
            workbench.SelectPipelineStep("step.line.a");
            Check("2-Point Line uses typed WPG adapter", workbench.SelectedStepPropertyDraft is TwoPointLineStepProperties, workbench.SelectedStepAdapterStatus);
            Check("2-Point Line exposes exact ordered selection", workbench.TwoPointLineSelectionSummary.Contains("row 0, column 0", StringComparison.Ordinal) && workbench.TwoPointLineSelectionSummary.Contains("row 0, column 2", StringComparison.Ordinal), workbench.TwoPointLineSelectionSummary);

            C3DTwoPointLineFeature? firstPreview = null;
            C3DTwoPointLineFeature? firstPublished = null;
            workbench.TwoPointLineDisplayRequested += (_, args) =>
            {
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
            Check("second 2-Point Line explicit Preview", workbench.PreviewSelectedTwoPointLineAsync().GetAwaiter().GetResult(), workbench.TwoPointLineExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("second Publish preserves first independent line", workbench.IsTwoPointLinePreviewPublished && workbench.TryGetPublishedTwoPointLineOutput("derived.line.a", out var retainedA) && ReferenceEquals(retainedA, firstPublished) && workbench.TryGetPublishedTwoPointLineOutput("derived.line.b", out var publishedB) && publishedB is not null, workbench.TwoPointLineExecutionSummary);
            Check("published 2-Point Lines appear as typed artifacts", workbench.ArtifactRegistry.Count(item => item.Contract == "LineFeature" && item.State == "Published") == 2, workbench.ArtifactRegistrySummary);

            workbench.SelectPipelineStep("step.corner.01");
            Check("Line Intersection accepts two picked published lines", workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.LineIntersectionUpstreamSummary);
            Check("generic published-line contract retains picked origins", workbench.TryGetCurrentLineIntersectionInputs(out var firstInput, out var secondInput) && firstInput?.OriginKind == C3DLineOriginKind.PickedPoints && secondInput?.OriginKind == C3DLineOriginKind.PickedPoints, $"first={firstInput?.OriginKind};second={secondInput?.OriginKind}");
            Check("explicit picked-line Intersection Preview", workbench.PreviewSelectedLineIntersectionAsync().GetAwaiter().GetResult() && workbench.CurrentLineIntersectionOutput is { } corner && NearlyEqual(corner.CornerAnchorX, 1) && NearlyEqual(corner.CornerAnchorY, 1) && NearlyEqual(corner.CornerAnchorZ, 0), workbench.LineIntersectionEvidenceSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("picked-line Intersection Publish", workbench.IsLineIntersectionPreviewPublished, workbench.LineIntersectionExecutionSummary);

            workbench.SelectPipelineStep("step.line.a");
            workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "OutputRole").Value = "FirstEdgeRevised";
            Check("2-Point Line parameter change stales its Preview and downstream corner", workbench.IsTwoPointLinePreviewStale && !workbench.TryGetPublishedTwoPointLineOutput("derived.line.a", out _) && workbench.IsLineIntersectionPreviewStale && !workbench.TryGetPublishedLineIntersectionOutput("derived.corner.01", out _), workbench.TwoPointLineExecutionSummary);

            var replacementSourcePath = Path.Combine(root, "replacement.c3d");
            C3DHeightFieldSnapshot.CreateForVerification("source.replacement", 2, 2, [1d, 2d, 3d, 4d]).SaveC3D(replacementSourcePath);
            workbench.SetC3DSource(replacementSourcePath);
            Check("source replacement clears every published 2-Point Line", !workbench.TryGetPublishedTwoPointLineOutput("derived.line.b", out _) && !workbench.TryGetPublishedLineGeometry("derived.line.b", out _), workbench.TwoPointLineExecutionSummary);
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        finally
        {
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
