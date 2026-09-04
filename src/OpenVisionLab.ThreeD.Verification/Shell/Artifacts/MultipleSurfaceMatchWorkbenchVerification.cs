using System.IO;
using System.Text;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Artifacts;

internal static class MultipleSurfaceMatchWorkbenchVerification
{
    public static bool Verify(
        string modelPath,
        string scenePath,
        string collectionPath,
        string reportPath,
        out string summary)
    {
        var cases = new List<Case>();
        void Check(string name, bool passed, string evidence) =>
            cases.Add(new Case(name, passed, evidence));
        try
        {
            var model = SurfaceModelArtifactStore.Load(modelPath);
            var scene = PreparedSceneArtifactStore.Load(scenePath);
            var collection = SurfaceMatchCollectionArtifactStore.Load(
                collectionPath);
            var workbench = new ToolWorkbenchViewModel();
            workbench.SetC3DSource(
                Path.GetFullPath(Path.Combine(
                    "3D",
                    "Samples",
                    "ThicknessCouponV1",
                    "thickness-coupon-v1.C3D")),
                markDirty: false);
            var tool = workbench.Tools.Single(item =>
                item.Id == "surface-match");
            workbench.AddSelectedToolCommand.Execute(tool);
            var dirtyBeforeLoad = workbench.IsDirty;
            var stepCountBeforeLoad = workbench.PipelineSteps.Count;
            var stepStateBeforeLoad = workbench.SelectedPipelineStep!.State;
            var parametersBeforeLoad = string.Join(
                "|",
                workbench.SelectedPipelineStep.Parameters.Select(parameter =>
                    $"{parameter.Name}={parameter.Value}"));
            var outputBeforeLoad = workbench.CurrentMeasurementOutput;
            var displayRequests = new List<string>();
            workbench.SurfaceMatchDisplayRequested += (_, args) =>
                displayRequests.Add(args.Execution.ContentSha256);

            workbench.ShowSurfaceMatchCollectionEvidence(
                model,
                scene,
                collection);
            var first = collection.Items[0];
            var second = collection.Items[1];
            Check(
                "collection-load-selects-first-result",
                workbench.SurfaceMatchCollection?.ContentSha256
                    == collection.ContentSha256
                && workbench.SurfaceMatchCollectionItems.Count == 2
                && workbench.SelectedSurfaceMatchCollectionItem?.MatchId
                    == first.MatchId
                && workbench.SurfaceMatchEvidence?.ContentSha256
                    == first.Execution.ContentSha256
                && displayRequests.SequenceEqual(
                    new[] { first.Execution.ContentSha256 }),
                $"selected={workbench.SelectedSurfaceMatchCollectionItem?.MatchId};displayRequests={displayRequests.Count}");
            Check(
                "collection-selector-visible-and-enabled",
                workbench.IsSurfaceMatchCollectionVisible
                && workbench.CanSelectSurfaceMatchCollectionItem,
                $"visible={workbench.IsSurfaceMatchCollectionVisible};enabled={workbench.CanSelectSurfaceMatchCollectionItem}");
            Check(
                "initial-navigation-stops-at-first-result",
                !workbench.PreviousSurfaceMatchCollectionItemCommand
                    .CanExecute(null)
                && workbench.NextSurfaceMatchCollectionItemCommand
                    .CanExecute(null),
                $"previous={workbench.PreviousSurfaceMatchCollectionItemCommand.CanExecute(null)};next={workbench.NextSurfaceMatchCollectionItemCommand.CanExecute(null)}");

            workbench.NextSurfaceMatchCollectionItemCommand.Execute(null);
            Check(
                "next-navigation-routes-linked-evidence",
                workbench.SelectedSurfaceMatchCollectionItem?.MatchId
                    == second.MatchId
                && workbench.SurfaceMatchEvidence?.ContentSha256
                    == second.Execution.ContentSha256
                && workbench.SurfaceMatchAssessment?.ContentSha256
                    == second.Assessment.ContentSha256
                && displayRequests.SequenceEqual(new[]
                {
                    first.Execution.ContentSha256,
                    second.Execution.ContentSha256
                }),
                $"selected={workbench.SelectedSurfaceMatchCollectionItem?.MatchId};displayRequests={displayRequests.Count}");
            var displayCountAtLast = displayRequests.Count;
            workbench.NextSurfaceMatchCollectionItemCommand.Execute(null);
            Check(
                "navigation-stops-at-last-result",
                workbench.PreviousSurfaceMatchCollectionItemCommand
                    .CanExecute(null)
                && !workbench.NextSurfaceMatchCollectionItemCommand
                    .CanExecute(null)
                && displayRequests.Count == displayCountAtLast
                && workbench.SelectedSurfaceMatchCollectionItem?.MatchId
                    == second.MatchId,
                $"previous={workbench.PreviousSurfaceMatchCollectionItemCommand.CanExecute(null)};next={workbench.NextSurfaceMatchCollectionItemCommand.CanExecute(null)};displayRequests={displayRequests.Count}");
            workbench.PreviousSurfaceMatchCollectionItemCommand.Execute(null);
            Check(
                "previous-navigation-returns-linked-evidence",
                workbench.SelectedSurfaceMatchCollectionItem?.MatchId
                    == first.MatchId
                && workbench.SurfaceMatchEvidence?.ContentSha256
                    == first.Execution.ContentSha256
                && displayRequests.Count == displayCountAtLast + 1,
                $"selected={workbench.SelectedSurfaceMatchCollectionItem?.MatchId};displayRequests={displayRequests.Count}");
            var selected = workbench.SelectSurfaceMatchCollectionItem(
                second.MatchId);
            Check(
                "selector-and-navigation-share-one-selection",
                selected
                && workbench.SelectedSurfaceMatchCollectionItem?.MatchId
                    == second.MatchId
                && workbench.PreviousSurfaceMatchCollectionItemCommand
                    .CanExecute(null)
                && !workbench.NextSurfaceMatchCollectionItemCommand
                    .CanExecute(null),
                $"selected={workbench.SelectedSurfaceMatchCollectionItem?.MatchId};previous={workbench.PreviousSurfaceMatchCollectionItemCommand.CanExecute(null)};next={workbench.NextSurfaceMatchCollectionItemCommand.CanExecute(null)}");
            Check(
                "selection-preserves-collection-and-recipe",
                workbench.SurfaceMatchCollection?.ContentSha256
                    == collection.ContentSha256
                && workbench.IsDirty == dirtyBeforeLoad
                && workbench.PipelineSteps.Count == stepCountBeforeLoad
                && workbench.SelectedPipelineStep.State == stepStateBeforeLoad
                && string.Join(
                    "|",
                    workbench.SelectedPipelineStep.Parameters.Select(parameter =>
                        $"{parameter.Name}={parameter.Value}"))
                    == parametersBeforeLoad
                && ReferenceEquals(
                    workbench.CurrentMeasurementOutput,
                    outputBeforeLoad)
                && !workbench.HasSurfaceMatchExperimentCandidate,
                $"collection={workbench.SurfaceMatchCollection?.ContentSha256};dirty={workbench.IsDirty};steps={workbench.PipelineSteps.Count};candidate={workbench.HasSurfaceMatchExperimentCandidate}");
            var displayCountBeforeInvalid = displayRequests.Count;
            Check(
                "unknown-match-id-fails-without-display-change",
                !workbench.SelectSurfaceMatchCollectionItem(
                    "match.surface.unknown")
                && displayRequests.Count == displayCountBeforeInvalid
                && workbench.SelectedSurfaceMatchCollectionItem?.MatchId
                    == second.MatchId,
                $"displayRequests={displayRequests.Count};selected={workbench.SelectedSurfaceMatchCollectionItem?.MatchId}");
            workbench.ClearSurfaceMatchEvidence();
            Check(
                "clear-removes-collection-and-selection",
                workbench.SurfaceMatchCollection is null
                && workbench.SurfaceMatchCollectionItems.Count == 0
                && workbench.SelectedSurfaceMatchCollectionItem is null
                && !workbench.IsSurfaceMatchCollectionVisible
                && !workbench.PreviousSurfaceMatchCollectionItemCommand
                    .CanExecute(null)
                && !workbench.NextSurfaceMatchCollectionItemCommand
                    .CanExecute(null),
                $"collection={(workbench.SurfaceMatchCollection is null ? "none" : "unexpected")};items={workbench.SurfaceMatchCollectionItems.Count}");
        }
        catch (Exception exception)
        {
            Check(
                "verification-exception",
                false,
                exception.ToString());
        }

        var passedCount = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"MultipleSurfaceMatchWorkbenchVerification|{(passedCount == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passedCount}|failed={cases.Count - passedCount}",
            "Boundary|Collection load, selector choice, and non-wrapping previous/next navigation are presentation-only; no Preview, Publish, Run, Validation, recipe mutation, matching execution, or persistence of selected item."
        };
        lines.AddRange(cases.Select(item =>
            $"{(item.Passed ? "PASS" : "FAIL")} | {item.Name} | {item.Evidence}"));
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory);
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(false));
        summary =
            $"Multiple Surface Match Workbench verification: "
            + $"{(passedCount == cases.Count ? "PASS" : "FAIL")} "
            + $"({passedCount}/{cases.Count})";
        return passedCount == cases.Count;
    }

    private sealed record Case(
        string Name,
        bool Passed,
        string Evidence);
}
