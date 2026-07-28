using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolHeightDifferenceEdgeWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D Height Difference Edge Workbench verification" };
        var passed = 0;
        var total = 0;
        var rootDirectory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "EdgeWorkbench", Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(rootDirectory);
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.synthetic",
                4,
                4,
                [1, 1, 10, 10, 1, 1, 10, 10, 1, 1, 10, 10, 1, 1, 10, 10]);
            var sourcePath = Path.Combine(rootDirectory, "source.c3d");
            source.SaveC3D(sourcePath);
            var selection = new ToolRecipeSelection(
                "selection.edge.01",
                "Synthetic edge band",
                ToolRecipeSelectionKinds.GridRectangle,
                source.EntityId,
                source.FrameId,
                new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height),
                new ToolRecipeGridRectangle(0, 0, 4, 4),
                null,
                null);
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Edge Workbench",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [
                    new ToolRecipeStep(
                        "step.filter.01", "filter", "Filter", 1, [source.EntityId], "derived.filtered.01",
                        [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]),
                    new ToolRecipeStep(
                        "step.edge.01", "height-difference-edge", "Height Difference Edge", 1,
                        ["derived.filtered.01", selection.Id], "derived.edgepoints.01",
                        [new("ComparisonAxis", "AcrossColumns"), new("Polarity", "Rising"), new("MinimumDelta", "5"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")])
                ],
                [selection]);
            var recipePath = Path.Combine(rootDirectory, "edge.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel();
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var openMessage), openMessage);
            workbench.SelectPipelineStep("step.edge.01");
            Check("edge refuses absent upstream", !workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.HeightDifferenceEdgeUpstreamSummary);
            workbench.SelectPipelineStep("step.filter.01");
            var filterPreview = workbench.PreviewSelectedFilterAsync().GetAwaiter().GetResult();
            Check("explicit Filter Preview", filterPreview && workbench.HasCurrentFilterPreview, workbench.FilterExecutionSummary);
            workbench.SelectPipelineStep("step.edge.01");
            Check("edge refuses unpublished upstream", !workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.HeightDifferenceEdgeUpstreamSummary);
            workbench.SelectPipelineStep("step.filter.01");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("explicit Filter Publish", workbench.IsFilterPreviewPublished, workbench.FilterExecutionSummary);

            C3DHeightDifferenceEdgePointSet? previewOutput = null;
            C3DHeightDifferenceEdgePointSet? publishedOutput = null;
            string? previewInputPath = null;
            workbench.HeightDifferenceEdgeDisplayRequested += (_, args) =>
            {
                if (args.IsPublished)
                {
                    publishedOutput = args.Output;
                }
                else
                {
                    previewOutput = args.Output;
                    previewInputPath = args.C3DPath;
                }
            };
            workbench.SelectPipelineStep("step.edge.01");
            var edgePreview = workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult();
            Check("explicit Edge Preview", edgePreview && previewOutput?.Points.Count == 4, workbench.HeightDifferenceEdgeExecutionSummary);
            Check(
                "Edge display uses exact Published Filter output",
                string.Equals(previewInputPath, workbench.CurrentFilterPreviewPath, StringComparison.OrdinalIgnoreCase)
                && previewInputPath is not null
                && File.Exists(previewInputPath),
                $"display={previewInputPath}; published={workbench.CurrentFilterPreviewPath}");
            var headlessFilter = ToolRecipeFilterExecution.Execute(document, "step.filter.01", rootDirectory);
            var headlessEdge = ToolRecipeHeightDifferenceEdgeExecution.Execute(document, "step.edge.01", headlessFilter.Output!);
            Check("Workbench and headless share exact output hash", previewOutput?.ContentSha256 == headlessEdge.Output?.ContentSha256, $"workbench={previewOutput?.ContentSha256};headless={headlessEdge.Output?.ContentSha256}");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Publish reuses exact Preview", workbench.IsEdgePreviewPublished && ReferenceEquals(previewOutput, publishedOutput), $"preview={previewOutput?.ContentSha256};published={publishedOutput?.ContentSha256}");
            var oldHash = previewOutput?.ContentSha256;
            workbench.HeightDifferenceEdgeMinimumDelta = "20";
            Check("parameter edit only marks stale", workbench.IsEdgePreviewStale && !workbench.IsEdgePreviewPublished && !workbench.PublishSelectedStepCommand.CanExecute(null), $"hash={oldHash};state={workbench.SelectedPipelineStep?.State}");
            Check("whole recipe Run stays blocked", !workbench.RunTeachingRecipeCommand.CanExecute(null), "No partial Filter + Edge recipe success is reported.");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception}");
            total++;
        }
        finally
        {
            if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, true);
        }

        summary = $"Height Difference Edge Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }
}
