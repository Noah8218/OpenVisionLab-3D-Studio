using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolLineIntersectionWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D Line Intersection Workbench verification" };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "LineIntersectionWorkbench", Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.synthetic",
                10,
                10,
                Enumerable.Range(0, 100)
                    .Select(index => index / 10 < 5 && index % 10 < 5 ? 1d : 10d)
                    .ToArray());
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var firstSelection = CreateSelection("selection.line-a", "Horizontal line band", source, 1, 0, 3, 10);
            var secondSelection = CreateSelection("selection.line-b", "Vertical line band", source, 0, 1, 10, 3);
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Line Intersection Workbench",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [
                    FilterStep(source.EntityId),
                    EdgeStep("step.edge.a", firstSelection.Id, "AcrossColumns", "derived.edge.a"),
                    LineFitStep("step.line.a", "derived.edge.a", "derived.line.a"),
                    EdgeStep("step.edge.b", secondSelection.Id, "AcrossRows", "derived.edge.b"),
                    LineFitStep("step.line.b", "derived.edge.b", "derived.line.b"),
                    IntersectionStep(),
                ],
                [firstSelection, secondSelection]);
            var recipePath = Path.Combine(root, "line-intersection.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, document);
            var workbench = new ToolWorkbenchViewModel();
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var open), open);

            workbench.SelectPipelineStep("step.corner.01");
            Check("intersection refuses absent published lines", !workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.LineIntersectionUpstreamSummary);

            workbench.SelectPipelineStep("step.filter.01");
            Check("explicit Filter Preview", workbench.PreviewSelectedFilterAsync().GetAwaiter().GetResult(), workbench.FilterExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("explicit Filter Publish", workbench.IsFilterPreviewPublished, workbench.FilterExecutionSummary);

            RunAndPublishEdge(workbench, "step.edge.a", Check);
            var firstLine = RunAndPublishLine(workbench, "step.line.a", Check);
            Check("first LineFeature is published", firstLine is not null && workbench.TryGetPublishedLineFitOutput("derived.line.a", out var publishedA) && ReferenceEquals(firstLine, publishedA), workbench.LineFitExecutionSummary);

            RunAndPublishEdge(workbench, "step.edge.b", Check);
            Check("second Edge does not discard first published LineFeature", workbench.TryGetPublishedLineFitOutput("derived.line.a", out var retainedA) && ReferenceEquals(firstLine, retainedA), "Independent published branch remains available.");
            var secondLine = RunAndPublishLine(workbench, "step.line.b", Check);
            Check("second LineFeature is published", secondLine is not null && workbench.TryGetPublishedLineFitOutput("derived.line.b", out var publishedB) && ReferenceEquals(secondLine, publishedB), workbench.LineFitExecutionSummary);

            C3DLineIntersectionFeature? preview = null;
            C3DLineIntersectionFeature? published = null;
            workbench.LineIntersectionDisplayRequested += (_, args) =>
            {
                if (args.IsPublished) published = args.Output;
                else preview = args.Output;
            };
            workbench.SelectPipelineStep("step.corner.01");
            Check("intersection typed WPG adapter is selected", workbench.SelectedStepPropertyDraft is LineIntersectionStepProperties, workbench.SelectedStepAdapterStatus);
            Check("intersection becomes ready only from two published lines", workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.LineIntersectionUpstreamSummary);
            var previewSucceeded = workbench.PreviewSelectedLineIntersectionAsync().GetAwaiter().GetResult();
            Check("explicit Line Intersection Preview", previewSucceeded && preview is not null && preview.ClosestApproachDistance == 0 && NearlyEqual(preview.CornerAnchorX, 4.5) && NearlyEqual(preview.CornerAnchorZ, 4.5), workbench.LineIntersectionEvidenceSummary);
            var headless = ToolRecipeLineIntersectionExecution.Execute(document, "step.corner.01", firstLine!, secondLine!);
            Check("Workbench and headless share exact output hash", preview?.ContentSha256 == headless.Output?.ContentSha256, $"workbench={preview?.ContentSha256};headless={headless.Output?.ContentSha256}");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Publish reuses exact Intersection Preview", workbench.IsLineIntersectionPreviewPublished && ReferenceEquals(preview, published) && workbench.TryGetPublishedLineIntersectionOutput("derived.corner.01", out var corner) && ReferenceEquals(corner, published), workbench.LineIntersectionExecutionSummary);
            Check("published corner appears as typed artifact", workbench.ArtifactRegistry.Any(item => item.Id == "derived.corner.01" && item.Contract == "CornerAnchor" && item.State == "Published"), workbench.ArtifactRegistrySummary);

            var gap = workbench.SelectedPipelineStep!.Parameters.Single(item => item.Name == "MaximumClosestApproachDistance");
            gap.Value = "0.01";
            Check("intersection parameter change stales only the current corner Preview", workbench.IsLineIntersectionPreviewStale && !workbench.IsLineIntersectionPreviewPublished && !workbench.TryGetPublishedLineIntersectionOutput("derived.corner.01", out _), workbench.LineIntersectionExecutionSummary);
            Check("published input lines remain available after corner stale", workbench.TryGetPublishedLineFitOutput("derived.line.a", out _) && workbench.TryGetPublishedLineFitOutput("derived.line.b", out _), workbench.LineIntersectionUpstreamSummary);
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

        summary = $"3D Line Intersection Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static void RunAndPublishEdge(ToolWorkbenchViewModel workbench, string stepId, Action<string, bool, string> check)
    {
        workbench.SelectPipelineStep(stepId);
        var preview = workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult();
        check($"explicit Edge Preview {stepId}", preview, workbench.HeightDifferenceEdgeExecutionSummary);
        workbench.PublishSelectedStepCommand.Execute(null);
        var points = workbench.CurrentHeightDifferenceEdgeOutput?.Points ?? [];
        var pointSummary = string.Join(";", points.Select(point => $"{point.ScanlineIndex}:({point.X:G3},{point.Y:G3},{point.Z:G3})"));
        check($"explicit Edge Publish {stepId}", workbench.IsEdgePreviewPublished, $"{workbench.HeightDifferenceEdgeExecutionSummary} | {pointSummary}");
    }

    private static C3DLineFeature? RunAndPublishLine(ToolWorkbenchViewModel workbench, string stepId, Action<string, bool, string> check)
    {
        workbench.SelectPipelineStep(stepId);
        var preview = workbench.PreviewSelectedLineFitAsync().GetAwaiter().GetResult();
        check($"explicit Line Fit Preview {stepId}", preview, workbench.LineFitExecutionSummary);
        var output = workbench.CurrentLineFitOutput;
        workbench.PublishSelectedStepCommand.Execute(null);
        check($"explicit Line Fit Publish {stepId}", workbench.IsLineFitPreviewPublished, workbench.LineFitExecutionSummary);
        return output;
    }

    private static ToolRecipeSelection CreateSelection(string id, string name, C3DHeightFieldSnapshot source, int row, int column, int rowCount, int columnCount) =>
        new(id, name, ToolRecipeSelectionKinds.GridRectangle, source.EntityId, source.FrameId, new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), new ToolRecipeGridRectangle(row, column, rowCount, columnCount), null, null);

    private static ToolRecipeStep FilterStep(string sourceId) =>
        new("step.filter.01", "filter", "Filter", 1, [sourceId], "derived.filtered.01", [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]);

    private static ToolRecipeStep EdgeStep(string id, string selectionId, string axis, string output) =>
        new(id, "height-difference-edge", "Height Difference Edge", 1, ["derived.filtered.01", selectionId], output, [new("ComparisonAxis", axis), new("Polarity", "Rising"), new("MinimumDelta", "5"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")]);

    private static ToolRecipeStep LineFitStep(string id, string input, string output) =>
        new(id, "three-d-line-fit", "3D Line Fit", 1, [input], output, [new("FitMethod", "DeterministicConsensusOrthogonalTls"), new("MaximumOrthogonalResidual", "0.001"), new("MinimumInlierCount", "3"), new("MinimumInlierRatio", "1"), new("MinimumInlierScanlineSpan", "2"), new("HypothesisPolicy", "Sha256PairSchedule"), new("MaximumHypotheses", "256"), new("RefinementPolicy", "OrthogonalTlsUntilStable10"), new("DirectionPolicy", "PositiveScanlineAxis"), new("EndpointPolicy", "InlierProjectionExtents")]);

    private static ToolRecipeStep IntersectionStep() =>
        new("step.corner.01", "line-intersection", "Line Intersection", 2, ["derived.line.a", "derived.line.b"], "derived.corner.01", [new("MaximumClosestApproachDistance", "0.001"), new("MinimumAcuteAngleDegrees", "45"), new("MaximumSupportExtension", "1.5"), new("OutputRole", "SyntheticCorner"), new("ClosestApproachPolicy", "MidpointOfClosestPoints"), new("ParallelPolicy", "RejectBelowMinimumAcuteAngle"), new("SupportPolicy", "WithinInlierProjectionExtentsWithMaximumExtension")]);

    private static bool NearlyEqual(double actual, double expected) => Math.Abs(actual - expected) <= 0.000001;
}
