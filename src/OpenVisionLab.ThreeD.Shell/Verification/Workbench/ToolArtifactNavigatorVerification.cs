using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolArtifactNavigatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D Typed Artifact Registry + Recipe Navigator verification" };
        var passed = 0;
        var total = 0;
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "ArtifactNavigator", Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.navigator",
                4,
                4,
                [1, 1, 10, 10, 1, 1, 10, 10, 1, 1, 10, 10, 1, 1, 10, 10]);
            var sourcePath = Path.Combine(fixtureRoot, "source.c3d");
            source.SaveC3D(sourcePath);
            var selection = new ToolRecipeSelection(
                "selection.navigator.edge-band",
                "Navigator edge band",
                ToolRecipeSelectionKinds.GridRectangle,
                source.EntityId,
                source.FrameId,
                new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height),
                new ToolRecipeGridRectangle(0, 0, 4, 4),
                null,
                null);
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Artifact Navigator Fixture",
                new ToolRecipeSource(source.EntityId, "Navigator source", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
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
            var recipePath = Path.Combine(fixtureRoot, "artifact-navigator.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent.json"));
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var openMessage), openMessage);
            Check(
                "tree construction does not execute a tool",
                !workbench.HasCurrentFilterPreview && !workbench.HasCurrentEdgePreview,
                $"filterPreview={workbench.HasCurrentFilterPreview};edgePreview={workbench.HasCurrentEdgePreview}");
            Check(
                "registry records source, selection, and declared outputs",
                workbench.ArtifactRegistry.Count == 4
                && workbench.ArtifactRegistry.Single(item => item.Id == source.EntityId).State == "Ready"
                && workbench.ArtifactRegistry.Single(item => item.Id == selection.Id).State == "Current selection"
                && workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01").State == "Declared"
                && workbench.ArtifactRegistry.Single(item => item.Id == "derived.edgepoints.01").State == "Declared",
                workbench.ArtifactRegistrySummary);

            workbench.CompareSlotAArtifactId = source.EntityId;
            Check(
                "Output Compare exposes only the ready source before Preview",
                workbench.GetCompareCandidate(source.EntityId) is { IsSource: true, C3DPath: var sourceComparePath }
                && File.Exists(sourceComparePath)
                && workbench.GetCompareCandidate("derived.filtered.01") is null
                && workbench.CompareSlotASummary.Contains(source.EntityId, StringComparison.Ordinal),
                $"candidates={string.Join(',', workbench.CompareCandidates.Select(item => item.Id))};slotA={workbench.CompareSlotASummary}");

            var displayRequests = 0;
            workbench.ViewerArtifactDisplayRequested += (_, request) =>
            {
                displayRequests++;
                request.WasDisplayed = File.Exists(request.C3DPath);
            };
            var displayedSource = workbench.DisplayedOutputs.Single(item => item.Id == source.EntityId);
            workbench.ShowDisplayedOutputInViewerCommand.Execute(displayedSource);
            Check(
                "Displayed Outputs shows only an existing C3D source without execution",
                displayRequests == 1
                && displayedSource.IsRenderableInViewer
                && displayedSource.IsShownInViewer
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                $"requests={displayRequests};summary={workbench.CurrentViewerOutputSummary}");

            var pipelineRoot = workbench.NavigatorRoots.Single(item => item.NodeKind == "Pipeline");
            Check(
                "navigator preserves ordered input-output tree",
                pipelineRoot.Children.Count == 2
                && pipelineRoot.Children[0].Children.Any(item => item.Title.StartsWith("Input:", StringComparison.Ordinal))
                && pipelineRoot.Children[0].Children.Any(item => item.Title.StartsWith("Output:", StringComparison.Ordinal)),
                $"roots={workbench.NavigatorRoots.Count};steps={pipelineRoot.Children.Count}");

            var edgeStepNode = pipelineRoot.Children.Single(item => item.PipelineStep?.ToolId == "height-difference-edge");
            workbench.SelectNavigatorItemCommand.Execute(edgeStepNode);
            Check(
                "tree selection focuses the corresponding step without execution",
                workbench.SelectedPipelineStep?.ToolId == "height-difference-edge"
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                workbench.SelectedPipelineStepTitle);

            var filterStepNode = pipelineRoot.Children.Single(item => item.PipelineStep?.ToolId == "filter");
            workbench.SelectNavigatorItemCommand.Execute(filterStepNode);
            var filterPreviewed = workbench.PreviewSelectedFilterAsync().GetAwaiter().GetResult();
            var filterArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01");
            Check(
                "Filter Preview registers typed current output identity",
                filterPreviewed
                && filterArtifact.Contract == "FilteredHeightField"
                && filterArtifact.State == "Preview"
                && filterArtifact.RootSourceId == source.EntityId
                && filterArtifact.InputEntityIds == source.EntityId
                && filterArtifact.HasContentHash,
                $"state={filterArtifact.State};hash={filterArtifact.ContentSha256};input={filterArtifact.InputEntityIds}");

            workbench.CompareSlotBArtifactId = filterArtifact.Id;
            Check(
                "Output Compare accepts current C3D source and Filter output without routing or running Edge",
                workbench.GetCompareCandidate(source.EntityId) is { IsSource: true, C3DPath: var sourcePathForCompare }
                && File.Exists(sourcePathForCompare)
                && workbench.GetCompareCandidate(filterArtifact.Id) is { IsSource: false, State: "Preview", C3DPath: var filterPathForCompare }
                && File.Exists(filterPathForCompare)
                && workbench.GetCompareCandidate("derived.edgepoints.01") is null
                && !workbench.HasCurrentEdgePreview
                && workbench.CompareSlotASummary.Contains(source.EntityId, StringComparison.Ordinal)
                && workbench.CompareSlotBSummary.Contains(filterArtifact.Id, StringComparison.Ordinal),
                $"candidates={string.Join(',', workbench.CompareCandidates.Select(item => item.Id))};slotA={workbench.CompareSlotASummary};slotB={workbench.CompareSlotBSummary}");

            workbench.CompareSlotBArtifactId = string.Empty;
            var displayedFilter = workbench.DisplayedOutputs.Single(item => item.Id == filterArtifact.Id);
            workbench.PinDisplayedOutputToCompareCommand.Execute(displayedFilter);
            Check(
                "Displayed Outputs pins an existing Filter C3D to Compare without routing or execution",
                string.Equals(workbench.CompareSlotBArtifactId, filterArtifact.Id, StringComparison.Ordinal)
                && displayedFilter.IsPinnedToCompare
                && !workbench.HasCurrentEdgePreview,
                $"slotB={workbench.CompareSlotBArtifactId};pins={displayedFilter.ComparePins}");

            workbench.PublishSelectedStepCommand.Execute(null);
            filterArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01");
            Check("Filter Publish updates only artifact state", filterArtifact.State == "Published" && filterArtifact.HasContentHash, filterArtifact.Detail);

            // Publishing rebuilds the read-only navigator projection. Resolve the live
            // node as the WPF tree does, rather than interacting with a removed item.
            var currentEdgeStepNode = workbench.NavigatorRoots
                .Single(item => item.NodeKind == "Pipeline")
                .Children.Single(item => item.PipelineStep?.ToolId == "height-difference-edge");
            workbench.SelectNavigatorItemCommand.Execute(currentEdgeStepNode);
            var edgePreviewed = workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult();
            var edgeArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.edgepoints.01");
            Check(
                "Edge Preview registers typed downstream identity",
                edgePreviewed
                && edgeArtifact.Contract == "EdgePointSet"
                && edgeArtifact.State == "Preview"
                && edgeArtifact.InputEntityIds == "derived.filtered.01"
                && edgeArtifact.RootSourceId == source.EntityId
                && edgeArtifact.HasContentHash,
                $"state={edgeArtifact.State};hash={edgeArtifact.ContentSha256};input={edgeArtifact.InputEntityIds}");

            var displayedEdge = workbench.DisplayedOutputs.Single(item => item.Id == edgeArtifact.Id);
            workbench.FocusDisplayedOutputStepCommand.Execute(displayedEdge);
            Check(
                "Displayed Outputs keeps feature output evidence-only and focuses its authored step",
                displayedEdge.IsEvidenceOnly
                && !displayedEdge.CanShowInViewer
                && !displayedEdge.CanPinToCompare
                && workbench.SelectedPipelineStep?.ToolId == "height-difference-edge",
                $"availability={displayedEdge.Availability};selected={workbench.SelectedPipelineStepTitle}");

            workbench.HeightDifferenceEdgeMinimumDelta = "20";
            edgeArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.edgepoints.01");
            Check(
                "parameter edits retain identity but mark output stale",
                edgeArtifact.State == "Stale" && edgeArtifact.HasContentHash && workbench.IsEdgePreviewStale
                && workbench.DisplayedOutputs.Single(item => item.Id == edgeArtifact.Id).HasNoCurrentOutput,
                $"state={edgeArtifact.State};hash={edgeArtifact.ContentSha256}");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            }
            catch
            {
                // Functional checks remain valid if a temporary viewer lock delays cleanup.
            }
        }

        var success = total > 0 && passed == total;
        summary = $"ArtifactNavigator|pass={success}|checks={passed}/{total}|report={Path.GetFullPath(reportPath)}";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return success;
    }
}
