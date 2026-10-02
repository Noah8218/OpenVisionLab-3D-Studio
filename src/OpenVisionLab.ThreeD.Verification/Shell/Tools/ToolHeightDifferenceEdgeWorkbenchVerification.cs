using System.IO;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

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

        ToolWorkbenchViewModel? workbench = null;

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
                        [new("ComparisonAxis", "AcrossColumns"), new("Polarity", "Rising"), new("MinimumDelta", "5"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")]),
                    new ToolRecipeStep(
                        "step.filter.02", "filter", "Independent Filter", 1, [source.EntityId], "derived.filtered.02",
                        [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]),
                    new ToolRecipeStep(
                        "step.edge.02", "height-difference-edge", "Independent Edge", 1,
                        ["derived.filtered.02", selection.Id], "derived.edgepoints.02",
                        [new("ComparisonAxis", "AcrossColumns"), new("Polarity", "Rising"), new("MinimumDelta", "1"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")])
                ],
                [selection]);
            var recipePath = Path.Combine(rootDirectory, "edge.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, document);
            if (Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT") is { Length: > 0 } fixtureRoot)
            {
                var fixtureDirectory = Path.Combine(Path.GetFullPath(fixtureRoot), "t04-native");
                Directory.CreateDirectory(fixtureDirectory);
                var fixtureSource = Path.Combine(fixtureDirectory, "edge-source.c3d");
                source.SaveC3D(fixtureSource);
                ToolRecipeDocumentStore.Save(Path.Combine(fixtureDirectory, "independent-branches.ov3d-recipe.json"),
                    document with { Source = document.Source with { Path = fixtureSource } });
            }

            workbench = new ToolWorkbenchViewModel();
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var openMessage), openMessage);
            workbench.SelectPipelineStep("step.edge.01");
            Check("edge refuses absent upstream", !workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.HeightDifferenceEdgeUpstreamSummary);
            workbench.SelectPipelineStep("step.filter.01");
            var filterPreview = workbench.PreviewSelectedFilterAsync().GetAwaiter().GetResult();
            Check("explicit Filter Preview", filterPreview && workbench.HasCurrentFilterPreview, workbench.FilterExecutionSummary);
            var filterPreviewPath = workbench.CurrentFilterPreviewPath;
            var filterPreviewBytes = filterPreviewPath is not null
                ? File.ReadAllBytes(filterPreviewPath)
                : [];
            Check(
                "Filter Preview publishes complete C3D bytes",
                filterPreviewPath is not null
                && filterPreviewBytes.Length > 0,
                filterPreviewPath ?? "missing preview path");
            Check(
                "Filter Preview cleans temporary siblings",
                filterPreviewPath is not null
                && !Directory.GetFiles(
                    Path.GetDirectoryName(filterPreviewPath)!,
                    "*.tmp.*").Any(),
                filterPreviewPath ?? "missing preview path");
            var artifactRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
            Check("Filter Preview respects configured artifact root", string.IsNullOrWhiteSpace(artifactRoot)
                || filterPreviewPath is not null && filterPreviewPath.StartsWith(Path.Combine(Path.GetFullPath(artifactRoot), "Preview") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), filterPreviewPath ?? "missing preview path");
            Check(
                "repeated Filter Preview remains byte-stable",
                workbench.PreviewSelectedFilterAsync().GetAwaiter().GetResult()
                && filterPreviewPath is not null
                && filterPreviewBytes.SequenceEqual(File.ReadAllBytes(filterPreviewPath))
                && !Directory.GetFiles(
                    Path.GetDirectoryName(filterPreviewPath)!,
                    "*.tmp.*").Any(),
                filterPreviewPath ?? "missing preview path");
            workbench.SelectPipelineStep("step.edge.01");
            Check("edge refuses unpublished upstream", !workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.HeightDifferenceEdgeUpstreamSummary);
            workbench.SelectPipelineStep("step.filter.02");
            Check("unexecuted Filter cannot publish another Filter preview", !workbench.PublishSelectedStepCommand.CanExecute(null), workbench.SelectedPipelineStep!.State);
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
            workbench.SelectPipelineStep("step.edge.02");
            Check("unexecuted Edge cannot publish another Edge preview", !workbench.PublishSelectedStepCommand.CanExecute(null), workbench.SelectedPipelineStep!.State);
            workbench.SelectPipelineStep("step.edge.01");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Publish reuses exact Preview", workbench.IsEdgePreviewPublished && ReferenceEquals(previewOutput, publishedOutput), $"preview={previewOutput?.ContentSha256};published={publishedOutput?.ContentSha256}");
            var oldHash = previewOutput?.ContentSha256;
            var firstPublishedOutput = publishedOutput;
            workbench.SelectPipelineStep("step.filter.01");
            workbench.SelectedPipelineStep!.ToolName = "Renamed Filter";
            Check("display name preserves Filter and dependent Edge", workbench.HasCurrentFilterPreview
                && workbench.IsFilterPreviewPublished && workbench.HasCurrentEdgePreview
                && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out var renamedEdge)
                && ReferenceEquals(renamedEdge, firstPublishedOutput), workbench.FilterExecutionSummary);
            var firstEdgeResult = workbench.TeachingResults.FirstOrDefault(item => item.Step.Id == "step.edge.01");
            Check("Edge result presents point count and excludes execution provenance", firstEdgeResult is { Value: "4", Unit: "count", IsCurrent: true }
                && !firstEdgeResult.Reason.Contains("step.edge", StringComparison.Ordinal)
                && !firstEdgeResult.Reason.Contains(firstPublishedOutput!.ContentSha256, StringComparison.Ordinal)
                && workbench.ArtifactRegistry.Any(item => item.Id == "derived.edgepoints.01" && item.Detail.Contains(firstPublishedOutput.Provenance, StringComparison.Ordinal)),
                $"value={firstEdgeResult?.Value};unit={firstEdgeResult?.Unit};reason={firstEdgeResult?.Reason}");
            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
            var actionsBeforeLanguageChange = workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
            workbench.SelectPipelineStep("step.edge.01");
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                Check("calculated feature results switch to Korean without losing their values", workbench.SelectedToolWorkspace.CurrentOutput is { Value: "4", Unit: "count", ResultStatus: "계산됨", ValueLabel: "검출 포인트", Reason: "에지 포인트를 계산했습니다." }
                    && workbench.TeachingResults.Where(item => item.Step.Id is "step.filter.01" or "step.edge.01").All(item => item.Status == "계산됨" && item.IsCurrent), workbench.SelectedToolWorkspace.CurrentOutput?.ResultStatus ?? "missing output");
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                Check("calculated feature results switch to English without rerunning", workbench.SelectedToolWorkspace.CurrentOutput is { Value: "4", Unit: "count", ResultStatus: "Calculated", ValueLabel: "Detected points", Reason: "Edge points calculated." }
                    && workbench.TeachingResults.Where(item => item.Step.Id is "step.filter.01" or "step.edge.01").All(item => item.Status == "Calculated" && item.IsCurrent)
                    && actionsBeforeLanguageChange == workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run")
                    && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out var localizedEdge)
                    && ReferenceEquals(localizedEdge, firstPublishedOutput), workbench.SelectedToolWorkspace.CurrentOutput?.ResultStatus ?? "missing output");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            }
            workbench.SelectPipelineStep("step.filter.02");
            workbench.SetFilterKernel5Command.Execute(null);
            Check("independent Filter edit preserves published branch", workbench.HasCurrentFilterPreview
                && workbench.IsFilterPreviewPublished
                && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out var independentEdge)
                && ReferenceEquals(independentEdge, firstPublishedOutput), workbench.HeightDifferenceEdgeExecutionSummary);
            Check("second Filter explicit Preview", workbench.PreviewSelectedFilterAsync().GetAwaiter().GetResult(), workbench.FilterExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectPipelineStep("step.edge.01");
            Check("retained first Filter remains available to its routed Edge", workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.HeightDifferenceEdgeUpstreamSummary);
            Check("both Filter artifacts retain their own render targets", workbench.ArtifactRegistry.Count(item => item.Contract == "FilteredHeightField" && item.State == "Published") == 2
                && workbench.GetRenderableC3DTarget("derived.filtered.01") is { State: "Published" }
                && workbench.GetRenderableC3DTarget("derived.filtered.02") is { State: "Published" }, workbench.ArtifactRegistrySummary);
            workbench.SelectPipelineStep("step.edge.02");
            Check("second Edge explicit Preview", workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult(), workbench.HeightDifferenceEdgeExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("both Edge artifacts retain their exact published identity", workbench.ArtifactRegistry.Any(item => item.Id == "derived.edgepoints.01" && item.State == "Published" && item.ContentSha256 == firstPublishedOutput?.ContentSha256)
                && workbench.ArtifactRegistry.Any(item => item.Id == "derived.edgepoints.02" && item.State == "Published" && item.ContentSha256 == workbench.CurrentHeightDifferenceEdgeOutput?.ContentSha256), workbench.ArtifactRegistrySummary);
            workbench.SelectPipelineStep("step.edge.01");
            Check("first Edge result keeps its own count after the second Edge runs", workbench.SelectedToolWorkspace.CurrentOutput is { Value: "4", Unit: "count", HasExecutionResult: true }
                && workbench.TeachingResults.Any(item => item.Step.Id == "step.edge.01" && item.Value == "4" && item.Unit == "count" && item.IsCurrent), workbench.SelectedToolWorkspace.CurrentOutput?.Reason ?? "no selected output");
            workbench.SelectPipelineStep("step.edge.02");
            workbench.SelectedPipelineStep!.ToolName = "Renamed Edge";
            Check("Edge display name preserves both published outputs", workbench.IsEdgePreviewPublished
                && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out _)
                && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.02", out _), workbench.HeightDifferenceEdgeExecutionSummary);
            workbench.HeightDifferenceEdgeMinimumDelta = "20";
            Check("Edge condition invalidates only affected branch", workbench.IsEdgePreviewStale
                && !workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.02", out _)
                && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out var retainedEdge)
                && ReferenceEquals(retainedEdge, firstPublishedOutput), workbench.HeightDifferenceEdgeExecutionSummary);
            workbench.HeightDifferenceEdgeMinimumDelta = "1";
            Check("second Edge explicit recovery", workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult(), workbench.HeightDifferenceEdgeExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("remove current Edge preserves independent published output", workbench.ConfirmSelectedStepRemoval("step.edge.02")
                && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out var afterRemoval)
                && ReferenceEquals(afterRemoval, firstPublishedOutput)
                && !workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.02", out _), "isolated generated recipe only");
            Check("restore Edge settings does not restore output", workbench.TryRestoreLastRemovedStep(out var restoredEdge)
                && !workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.02", out _)
                && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out _), restoredEdge);
            Check("restored Edge explicit recovery", workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult(), workbench.HeightDifferenceEdgeExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectPipelineStep("step.filter.02");
            Check("remove upstream invalidates only connected Edge", workbench.ConfirmSelectedStepRemoval("step.filter.02")
                && !workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.02", out _)
                && workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out var afterUpstreamRemoval)
                && ReferenceEquals(afterUpstreamRemoval, firstPublishedOutput), "removed second Filter only");
            Check("restore upstream keeps dependent output unexecuted", workbench.TryRestoreLastRemovedStep(out var restoredFilter)
                && !workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.02", out _), restoredFilter);
            workbench.SelectPipelineStep("step.filter.01");
            Check("first branch explicit recovery", workbench.PreviewSelectedFilterAsync().GetAwaiter().GetResult(), workbench.FilterExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectPipelineStep("step.edge.01");
            Check("first Edge explicit recovery", workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult(), workbench.HeightDifferenceEdgeExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            workbench.SelectPipelineStep("step.filter.01");
            workbench.SetFilterKernel5Command.Execute(null);
            Check(
                "Filter parameter edit invalidates Filter and downstream Edge",
                workbench.IsFilterPreviewStale
                && !workbench.IsFilterPreviewPublished
                && workbench.IsEdgePreviewStale
                && !workbench.IsEdgePreviewPublished
                && !workbench.PublishSelectedStepCommand.CanExecute(null),
                $"filter={workbench.IsFilterPreviewStale};edge={workbench.IsEdgePreviewStale};state={workbench.SelectedPipelineStep?.State}");
            workbench.SelectPipelineStep("step.edge.01");
            workbench.HeightDifferenceEdgeMinimumDelta = "20";
            Check("parameter edit only marks stale", workbench.IsEdgePreviewStale && !workbench.IsEdgePreviewPublished && !workbench.PublishSelectedStepCommand.CanExecute(null), $"hash={oldHash};state={workbench.SelectedPipelineStep?.State}");
            Check("whole recipe Run stays blocked", !workbench.RunTeachingRecipeCommand.CanExecute(null), "No partial Filter + Edge recipe success is reported.");

            workbench.Dispose();
            Check(
                "Workbench disposal clears Edge state",
                workbench.CurrentHeightDifferenceEdgeOutput is null
                    && !workbench.TryGetPublishedHeightDifferenceEdgeOutput("derived.edgepoints.01", out _)
                    && !workbench.IsEdgePreviewRunning
                    && !workbench.IsEdgePreviewStale
                    && !workbench.IsEdgePreviewPublished,
                workbench.HeightDifferenceEdgeExecutionSummary);
            var previewAfterDispose = workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult();
            Check(
                "post-disposal Edge routes reject work",
                !previewAfterDispose && !workbench.PreviewSelectedStepCommand.CanExecute(null),
                workbench.HeightDifferenceEdgeExecutionSummary);
            workbench.Dispose();
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception}");
            total++;
        }
        finally
        {
            workbench?.Dispose();
            if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, true);
        }

        summary = $"Height Difference Edge Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }
}
