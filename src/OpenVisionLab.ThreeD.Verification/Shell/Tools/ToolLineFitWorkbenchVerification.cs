using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

internal static class ToolLineFitWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D Line Fit Workbench verification" };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "LineFitWorkbench", Guid.NewGuid().ToString("N"));
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
            var source = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", 4, 4, [1, 1, 10, 10, 1, 1, 10, 10, 1, 1, 10, 10, 1, 1, 10, 10]);
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var selection = new ToolRecipeSelection("selection.edge.01", "Synthetic edge band", ToolRecipeSelectionKinds.GridRectangle, source.EntityId, source.FrameId, new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), new ToolRecipeGridRectangle(0, 0, 4, 4), null, null);
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Line Fit Workbench",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [
                    new ToolRecipeStep("step.filter.01", "filter", "Filter", 1, [source.EntityId], "derived.filtered.01", [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]),
                    new ToolRecipeStep("step.edge.01", "height-difference-edge", "Height Difference Edge", 1, ["derived.filtered.01", selection.Id], "derived.edgepoints.01", [new("ComparisonAxis", "AcrossColumns"), new("Polarity", "Rising"), new("MinimumDelta", "5"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")]),
                    new ToolRecipeStep("step.line.01", "three-d-line-fit", "3D Line Fit", 1, ["derived.edgepoints.01"], "derived.line.01", [new("FitMethod", "DeterministicConsensusOrthogonalTls"), new("MaximumOrthogonalResidual", "0.01"), new("MinimumInlierCount", "3"), new("MinimumInlierRatio", "0.75"), new("MinimumInlierScanlineSpan", "2"), new("HypothesisPolicy", "Sha256PairSchedule"), new("MaximumHypotheses", "256"), new("RefinementPolicy", "OrthogonalTlsUntilStable10"), new("DirectionPolicy", "PositiveScanlineAxis"), new("EndpointPolicy", "InlierProjectionExtents")])
                ],
                [selection]);
            var firstLine = document.Steps.Single(step => step.Id == "step.line.01");
            var firstFilter = document.Steps.Single(step => step.Id == "step.filter.01");
            document = document with { Steps = [.. document.Steps,
                firstLine with { Id = "step.line.02", ToolName = "Second Line Fit", OutputEntityId = "derived.line.02" },
                firstFilter with { Id = "step.filter.02", ToolName = "Independent Filter", OutputEntityId = "derived.filtered.02" }] };
            var recipePath = Path.Combine(root, "line-fit.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, document);
            workbench = new ToolWorkbenchViewModel();
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var open), open);
            workbench.SelectPipelineStep("step.line.01");
            Check("line fit refuses absent upstream", !workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.LineFitUpstreamSummary);
            workbench.SelectPipelineStep("step.filter.01");
            Check("explicit Filter Preview", workbench.PreviewSelectedFilterAsync().GetAwaiter().GetResult(), workbench.FilterExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("explicit Filter Publish", workbench.IsFilterPreviewPublished, workbench.FilterExecutionSummary);
            workbench.SelectPipelineStep("step.edge.01");
            Check("explicit Edge Preview", workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult(), workbench.HeightDifferenceEdgeExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            var publishedEdge = workbench.CurrentHeightDifferenceEdgeOutput;
            Check("explicit Edge Publish", workbench.IsEdgePreviewPublished && publishedEdge is not null, workbench.HeightDifferenceEdgeExecutionSummary);

            C3DLineFeature? preview = null;
            C3DLineFeature? published = null;
            workbench.LineFitDisplayRequested += (_, args) =>
            {
                if (args.IsPublished) published = args.Output;
                else preview = args.Output;
            };
            workbench.SelectPipelineStep("step.line.01");
            Check("line fit becomes ready only from published edge", workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.LineFitUpstreamSummary);
            var linePreview = workbench.PreviewSelectedLineFitAsync().GetAwaiter().GetResult();
            Check("explicit Line Fit Preview", linePreview && preview?.Diagnostics.InlierCount == 4 && workbench.LineFitResidualPlotPoints.Count == 4, workbench.LineFitExecutionSummary);
            var headless = ToolRecipeLineFitExecution.Execute(document, "step.line.01", publishedEdge!);
            Check("Workbench and headless share exact output hash", preview?.ContentSha256 == headless.Output?.ContentSha256, $"workbench={preview?.ContentSha256};headless={headless.Output?.ContentSha256}");
            Check("Line Fit does not rerun Edge", ReferenceEquals(publishedEdge, workbench.CurrentHeightDifferenceEdgeOutput), $"edge={publishedEdge?.ContentSha256}");
            workbench.SelectPipelineStep("step.line.02");
            Check("unexecuted Line Fit cannot publish another Line Fit preview", !workbench.PublishSelectedStepCommand.CanExecute(null), workbench.SelectedPipelineStep!.State);
            workbench.SelectPipelineStep("step.line.01");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Publish reuses exact Preview", workbench.IsLineFitPreviewPublished && ReferenceEquals(preview, published), $"preview={preview?.ContentSha256};published={published?.ContentSha256}");
            var firstPublishedLine = published;
            workbench.SelectedPipelineStep!.ToolName = "Renamed Line Fit";
            Check("Line Fit display name preserves result and diagnostics", workbench.IsLineFitPreviewPublished
                && workbench.LineFitResidualPlotPoints.Count == 4
                && workbench.TryGetPublishedLineFitOutput("derived.line.01", out var namedLine)
                && ReferenceEquals(namedLine, firstPublishedLine), workbench.LineFitExecutionSummary);
            workbench.SelectPipelineStep("step.filter.02");
            workbench.SetFilterKernel5Command.Execute(null);
            Check("independent Filter edit preserves downstream Line Fit", workbench.TryGetPublishedLineFitOutput("derived.line.01", out var independentLine)
                && ReferenceEquals(independentLine, firstPublishedLine), workbench.LineFitExecutionSummary);
            workbench.SelectPipelineStep("step.line.02");
            Check("second Line Fit explicit Preview", workbench.PreviewSelectedLineFitAsync().GetAwaiter().GetResult(), workbench.LineFitExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "MaximumOrthogonalResidual").Value = "0.02";
            Check("Line Fit edit invalidates only affected published output", !workbench.TryGetPublishedLineFitOutput("derived.line.02", out _)
                && workbench.TryGetPublishedLineFitOutput("derived.line.01", out var retainedLine)
                && ReferenceEquals(retainedLine, firstPublishedLine), workbench.LineFitExecutionSummary);
            Check("second Line Fit explicit recovery", workbench.PreviewSelectedLineFitAsync().GetAwaiter().GetResult(), workbench.LineFitExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("published recovery preserves first output", workbench.TryGetPublishedLineFitOutput("derived.line.01", out var recoveredFirstLine)
                && ReferenceEquals(recoveredFirstLine, firstPublishedLine)
                && workbench.TryGetPublishedLineFitOutput("derived.line.02", out _), workbench.LineFitExecutionSummary);
            Check("remove current Line Fit preserves independent output", workbench.ConfirmSelectedStepRemoval("step.line.02")
                && workbench.TryGetPublishedLineFitOutput("derived.line.01", out var afterRemoval)
                && ReferenceEquals(afterRemoval, firstPublishedLine)
                && !workbench.TryGetPublishedLineFitOutput("derived.line.02", out _), "isolated generated recipe only");
            Check("restore Line Fit settings keeps output unexecuted", workbench.TryRestoreLastRemovedStep(out var restoredLine)
                && !workbench.TryGetPublishedLineFitOutput("derived.line.02", out _), restoredLine);
            Check("restored Line Fit explicit recovery", workbench.PreviewSelectedLineFitAsync().GetAwaiter().GetResult(), workbench.LineFitExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectPipelineStep("step.line.01");
            Check("first Line Fit explicit reselect recovery", workbench.PreviewSelectedLineFitAsync().GetAwaiter().GetResult(), workbench.LineFitExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectLineFitDiagnostic(2);
            Check("diagnostic selection is presentation only", workbench.SelectedLineFitDiagnostic?.InputPointIndex == 2 && ReferenceEquals(preview, workbench.CurrentLineFitOutput), workbench.LineFitSelectedDiagnosticSummary);
            var oldHash = preview?.ContentSha256;
            workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "MaximumOrthogonalResidual").Value = "0.02";
            Check("parameter change marks Line Fit stale without rerun", workbench.IsLineFitPreviewStale && !workbench.IsLineFitPreviewPublished && workbench.LineFitResidualPlotPoints.Count == 0 && oldHash == preview?.ContentSha256, $"hash={oldHash};state={workbench.SelectedPipelineStep.State}");
            Check("second Line Fit remains current after first Line Fit edit", workbench.TryGetPublishedLineFitOutput("derived.line.02", out _)
                && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out _), "second line and Edge are current before upstream edit");
            workbench.SelectPipelineStep("step.filter.01");
            workbench.SetFilterKernel5Command.Execute(null);
            Check("changed upstream rejects both Line Fit outputs", !workbench.TryGetPublishedLineFitOutput("derived.line.01", out _)
                && !workbench.TryGetPublishedLineFitOutput("derived.line.02", out _)
                && !workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out _), workbench.LineFitExecutionSummary);
            Check("whole recipe Run remains blocked", !workbench.RunTeachingRecipeCommand.CanExecute(null), "No partial Filter + Edge + Line Fit recipe success is reported.");

            workbench.Dispose();
            Check(
                "Line Fit disposal clears output, diagnostics, residual plot, and published registry",
                workbench.CurrentLineFitOutput is null
                    && workbench.SelectedLineFitDiagnostic is null
                    && workbench.LineFitPointDiagnostics.Count == 0
                    && workbench.LineFitResidualPlotPoints.Count == 0
                    && !workbench.TryGetPublishedLineFitOutput("derived.line.01", out _)
                    && !workbench.IsLineFitPreviewRunning,
                "disposed Line Fit owner is empty and fail-closed");
            var previewAfterDispose = workbench.PreviewSelectedLineFitAsync()
                .GetAwaiter()
                .GetResult();
            workbench.Dispose();
            Check(
                "repeated Workbench disposal rejects new Line Fit execution",
                !previewAfterDispose
                    && !workbench.PreviewSelectedStepCommand.CanExecute(null),
                "second Dispose is safe; Preview and command CanExecute remain false");
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

        summary = $"3D Line Fit Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }
}
