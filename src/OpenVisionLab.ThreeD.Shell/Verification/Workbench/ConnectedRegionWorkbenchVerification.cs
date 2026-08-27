using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ConnectedRegionWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Connected Region Workbench verification"
        };
        var passed = 0;
        var total = 0;
        var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))
            ?? throw new InvalidOperationException("The verification report directory is unavailable.");
        var fixtureRoot = Path.Combine(
            reportDirectory,
            "ConnectedRegionWorkbenchFixture",
            Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.connected-region-workbench",
                6,
                5,
                [
                    1, 1, 2, 2, 3, 3,
                    1, 1, 2, 2, 3, 3,
                    4, 4, 5, 5, 6, 6,
                    4, 4, 5, 5, 6, 6,
                    7, 7, 8, 8, 9, 9
                ]);
            var sourcePath = Path.Combine(fixtureRoot, "source.c3d");
            source.SaveC3D(sourcePath);

            var workbench = new ToolWorkbenchViewModel(
                Path.Combine(fixtureRoot, "recent.json"));
            workbench.SetC3DSource(sourcePath, markDirty: false);
            var sourceBound = C3DHeightFieldSnapshot.LoadIdentified(
                sourcePath,
                workbench.Source.Id,
                workbench.Source.Unit,
                workbench.Source.FrameId);
            var mask = new C3DConnectedRegionMask(
                "mask.connected-region-workbench",
                sourceBound.EntityId,
                sourceBound.ContentSha256,
                sourceBound.Width,
                sourceBound.Height,
                [
                    true, true, false, false, false, false,
                    true, true, false, false, false, false,
                    false, false, false, false, true, true,
                    false, false, false, false, true, true,
                    false, false, false, false, false, false
                ]);
            var evaluation = C3DConnectedRegionRule.Evaluate(
                new C3DConnectedRegionInput(
                    "derived.connected-region.metrics.01",
                    sourceBound.EntityId,
                    sourceBound,
                    mask));
            var pipelineIdsBefore = workbench.PipelineSteps.Select(step => step.Id).ToArray();
            var recipeNameBefore = workbench.RecipeName;

            Check(
                "G-11 evaluation is successful before presentation",
                evaluation.Result.Status == ResultStatus.Pass
                && evaluation.Output is { RegionCount: 2, ForegroundCellCount: 8 },
                evaluation.Result.Message);
            Check(
                "Workbench accepts exact source-bound typed output without rerunning detection",
                workbench.SetConnectedRegionPreview(evaluation, out var setMessage)
                && workbench.HasConnectedRegionOutput
                && workbench.CurrentConnectedRegionOutput is { OutputEntityId: "derived.connected-region.metrics.01" }
                && workbench.CurrentConnectedRegionResult?.Status == ResultStatus.Pass,
                setMessage);
            Check(
                "typed output is visible in Displayed Outputs as source-bound evidence",
                workbench.ArtifactRegistry.Any(item =>
                    item.NodeKind == "ConnectedRegionOutput"
                    && item.Contract == "ConnectedRegionMetrics"
                    && item.State == "Preview"
                    && item.ContentSha256.Length == 64)
                && workbench.DisplayedOutputs.Any(item =>
                    item.NodeKind == "ConnectedRegionOutput"
                    && item.CanShowInViewer
                    && !item.CanPinToCompare),
                workbench.ConnectedRegionSummary);
            Check(
                "review items expose region metrics and exact cell counts",
                workbench.ConnectedRegionReviewItems.Count == 2
                && workbench.ConnectedRegionReviewItems.Sum(item => item.CellCount) == 8
                && workbench.ConnectedRegionReviewItems.All(item =>
                    item.Area > 0
                    && item.CenterText.Contains("grid-index", StringComparison.Ordinal)
                    && item.BoundsText.Contains("grid-index", StringComparison.Ordinal)),
                workbench.SelectedConnectedRegionSummary);

            var firstRegion = workbench.ConnectedRegionReviewItems[0];
            var secondRegion = workbench.ConnectedRegionReviewItems[1];
            workbench.SelectConnectedRegionCommand.Execute(secondRegion);
            Check(
                "selection changes only the selected region identity",
                workbench.SelectedConnectedRegionId == secondRegion.RegionId
                && secondRegion.IsSelected
                && !firstRegion.IsSelected
                && workbench.PipelineSteps.Select(step => step.Id).SequenceEqual(pipelineIdsBefore)
                && workbench.RecipeName == recipeNameBefore,
                workbench.SelectedConnectedRegionSummary);

            var displayRequestCount = 0;
            ToolWorkbenchArtifactDisplayRequestEventArgs? displayRequest = null;
            workbench.ViewerArtifactDisplayRequested += (_, request) =>
            {
                displayRequestCount++;
                displayRequest = request;
                request.WasDisplayed = File.Exists(request.C3DPath);
            };
            var connectedRegionItem = workbench.DisplayedOutputs.Single(item =>
                item.NodeKind == "ConnectedRegionOutput");
            workbench.ShowConnectedRegionOutputCommand.Execute(secondRegion);
            Check(
                "Show overlay requests the existing source C3D with typed output",
                displayRequestCount == 1
                && displayRequest is not null
                && displayRequest.ConnectedRegionOutput is not null
                && displayRequest.C3DPath == Path.GetFullPath(sourcePath)
                && connectedRegionItem.IsShownInViewer
                && workbench.WorkspaceSelection.SelectedOutputEntityId == "derived.connected-region.metrics.01",
                $"requests={displayRequestCount};path={displayRequest?.C3DPath};shown={connectedRegionItem.IsShownInViewer}");
            Check(
                "presentation commands do not mutate the authored recipe",
                workbench.PipelineSteps.Select(step => step.Id).SequenceEqual(pipelineIdsBefore)
                && workbench.RecipeName == recipeNameBefore
                && workbench.IsDirty == false,
                $"steps={workbench.PipelineSteps.Count};dirty={workbench.IsDirty}");

            var replacementPath = Path.Combine(fixtureRoot, "replacement.c3d");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.connected-region-replacement",
                2,
                2,
                [1, 2, 3, 4]).SaveC3D(replacementPath);
            workbench.SetC3DSource(replacementPath, markDirty: false);
            Check(
                "source change clears stale region output and selection",
                !workbench.HasConnectedRegionOutput
                && workbench.CurrentConnectedRegionOutput is null
                && workbench.ConnectedRegionReviewItems.Count == 0
                && workbench.SelectedConnectedRegionId is null
                && !workbench.ArtifactRegistry.Any(item => item.NodeKind == "ConnectedRegionOutput"),
                workbench.ConnectedRegionSummary);

            workbench.CreateNewTeachingRecipe();
            Check(
                "new recipe keeps connected-region presentation empty",
                !workbench.HasConnectedRegionOutput
                && workbench.CurrentConnectedRegionOutput is null
                && workbench.SelectedConnectedRegionId is null,
                workbench.ConnectedRegionSummary);
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
                if (Directory.Exists(fixtureRoot))
                {
                    Directory.Delete(fixtureRoot, recursive: true);
                }
            }
            catch
            {
                // A locked viewer/test artifact does not invalidate the assertions.
            }
        }

        var success = total > 0 && passed == total;
        summary = $"ConnectedRegionWorkbench|pass={success}|checks={passed}/{total}|report={Path.GetFullPath(reportPath)}";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return success;
    }
}
