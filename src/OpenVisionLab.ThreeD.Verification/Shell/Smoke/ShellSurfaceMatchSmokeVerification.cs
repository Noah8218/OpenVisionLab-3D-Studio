using System.IO;
using System.Text;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Smoke;

internal static class ShellSurfaceMatchSmokeVerification
{
    public static bool Verify(
        string artifactDirectory,
        string reportPath,
        out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var directory = Path.GetFullPath(artifactDirectory);
        var modelPath = Path.Combine(directory, "known-pose.surface-model.json");
        var scenePath = Path.Combine(directory, "known-pose-full.prepared-scene.json");
        var executionPath = Path.Combine(directory, "accepted-known-pose.surface-match-execution.json");
        var assessmentPath = Path.Combine(directory, "known-pose.surface-match-assessment.json");
        var runtimePath = Path.Combine(directory, "known-pose.surface-match-runtime.json");
        var collectionModelPath = Path.Combine(directory, "known-two-object.surface-model.json");
        var collectionScenePath = Path.Combine(directory, "known-two-object.prepared-scene.json");
        var collectionPath = Path.Combine(directory, "known-two-object.surface-match-collection.json");

        var lines = new List<string>
        {
            "OpenVisionLab 3D Shell Surface Match Smoke owner verification",
            "Boundary|Command-line evidence projection only; no Preview, Publish, Run, save, recipe mutation, source mutation, or physical metrology claim."
        };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string evidence)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {evidence}");
        }

        try
        {
            var noOpWorkbench = new ToolWorkbenchViewModel();
            var noOpDirty = noOpWorkbench.IsDirty;
            var noOpSteps = noOpWorkbench.PipelineSteps.Count;
            var noOpSelections = noOpWorkbench.Selections.Count;
            var noOpLogs = noOpWorkbench.RunLog.Count;
            var noOpResult = ShellSurfaceMatchSmoke.TryConfigureEvidenceFromCommandLine(
                ["shell"],
                noOpWorkbench,
                out var noOpFailure);
            Check(
                "no-surface-match-flags-are-no-op",
                noOpResult
                && string.IsNullOrEmpty(noOpFailure)
                && noOpWorkbench.IsDirty == noOpDirty
                && noOpWorkbench.PipelineSteps.Count == noOpSteps
                && noOpWorkbench.Selections.Count == noOpSelections
                && noOpWorkbench.RunLog.Count == noOpLogs
                && !noOpWorkbench.HasSurfaceMatchEvidence,
                $"result={noOpResult};failure={noOpFailure};dirty={noOpDirty}->{noOpWorkbench.IsDirty};steps={noOpSteps}->{noOpWorkbench.PipelineSteps.Count};selections={noOpSelections}->{noOpWorkbench.Selections.Count};logs={noOpLogs}->{noOpWorkbench.RunLog.Count}");

            var invalidWorkbench = new ToolWorkbenchViewModel();
            var invalidResult = ShellSurfaceMatchSmoke.TryConfigureEvidenceFromCommandLine(
                ["shell", "--smoke-surface-match-model", modelPath],
                invalidWorkbench,
                out var invalidFailure);
            Check(
                "incomplete-single-result-paths-fail-closed",
                !invalidResult
                && invalidFailure.Contains(
                    "requires model, scene, and execution paths",
                    StringComparison.Ordinal),
                $"result={invalidResult};failure={invalidFailure}");

            var collectionModel = SurfaceModelArtifactStore.Load(collectionModelPath);
            var collectionScene = PreparedSceneArtifactStore.Load(collectionScenePath);
            var collection = SurfaceMatchCollectionArtifactStore.Load(collectionPath);
            var invalidCollectionWorkbench = new ToolWorkbenchViewModel();
            var invalidCollectionResult = ShellSurfaceMatchSmoke.TryConfigureEvidenceFromCommandLine(
                [
                    "shell",
                    "--smoke-surface-match-model", collectionModelPath,
                    "--smoke-surface-match-scene", collectionScenePath,
                    "--smoke-surface-match-collection", collectionPath,
                    "--smoke-surface-match-execution", executionPath
                ],
                invalidCollectionWorkbench,
                out var invalidCollectionFailure);
            Check(
                "collection-and-single-result-paths-are-exclusive",
                !invalidCollectionResult
                && invalidCollectionFailure.Contains(
                    "without single-result evidence paths",
                    StringComparison.Ordinal),
                $"result={invalidCollectionResult};failure={invalidCollectionFailure}");

            var singleWorkbench = new ToolWorkbenchViewModel();
            var singleDisplayRequests = 0;
            singleWorkbench.SurfaceMatchDisplayRequested += (_, _) =>
                singleDisplayRequests++;
            var singleDirty = singleWorkbench.IsDirty;
            var singleSteps = singleWorkbench.PipelineSteps.Count;
            var singleSelections = singleWorkbench.Selections.Count;
            var singleLogs = singleWorkbench.RunLog.Count;
            var singleResult = ShellSurfaceMatchSmoke.TryConfigureEvidenceFromCommandLine(
                [
                    "shell",
                    "--smoke-surface-match-model", modelPath,
                    "--smoke-surface-match-scene", scenePath,
                    "--smoke-surface-match-execution", executionPath,
                    "--smoke-surface-match-assessment", assessmentPath,
                    "--smoke-surface-match-runtime", runtimePath
                ],
                singleWorkbench,
                out var singleFailure);
            Check(
                "single-evidence-is-projected-without-authoring-mutation",
                singleResult
                && string.IsNullOrEmpty(singleFailure)
                && singleWorkbench.HasSurfaceMatchEvidence
                && singleDisplayRequests == 1
                && singleWorkbench.IsDirty == singleDirty
                && singleWorkbench.PipelineSteps.Count == singleSteps
                && singleWorkbench.Selections.Count == singleSelections
                && !singleWorkbench.IsSurfaceMatchExperimentRunning
                && singleWorkbench.RunLog.Count >= singleLogs,
                $"result={singleResult};failure={singleFailure};evidence={singleWorkbench.HasSurfaceMatchEvidence};displayRequests={singleDisplayRequests};dirty={singleDirty}->{singleWorkbench.IsDirty};steps={singleSteps}->{singleWorkbench.PipelineSteps.Count};selections={singleSelections}->{singleWorkbench.Selections.Count};logs={singleLogs}->{singleWorkbench.RunLog.Count}");

            var collectionWorkbench = new ToolWorkbenchViewModel();
            var collectionDisplayRequests = 0;
            collectionWorkbench.SurfaceMatchDisplayRequested += (_, _) =>
                collectionDisplayRequests++;
            var collectionDirty = collectionWorkbench.IsDirty;
            var collectionSteps = collectionWorkbench.PipelineSteps.Count;
            var collectionSelections = collectionWorkbench.Selections.Count;
            var collectionLogs = collectionWorkbench.RunLog.Count;
            var collectionResult = ShellSurfaceMatchSmoke.TryConfigureEvidenceFromCommandLine(
                [
                    "shell",
                    "--smoke-surface-match-model", collectionModelPath,
                    "--smoke-surface-match-scene", collectionScenePath,
                    "--smoke-surface-match-collection", collectionPath,
                    "--smoke-surface-match-select-index", "1"
                ],
                collectionWorkbench,
                out var collectionFailure);
            var selectedMatchId = collection.Items[1].MatchId;
            Check(
                "collection-evidence-loads-and-selects-requested-item",
                collectionResult
                && string.IsNullOrEmpty(collectionFailure)
                && collectionWorkbench.SurfaceMatchCollection?.ContentSha256
                    == collection.ContentSha256
                && collectionWorkbench.SurfaceMatchCollectionItems.Count
                    == collection.Items.Length
                && collectionWorkbench.SelectedSurfaceMatchCollectionItem?.MatchId
                    == selectedMatchId
                && collectionWorkbench.HasSurfaceMatchEvidence
                && collectionDisplayRequests >= 1
                && collectionWorkbench.IsDirty == collectionDirty
                && collectionWorkbench.PipelineSteps.Count == collectionSteps
                && collectionWorkbench.Selections.Count == collectionSelections
                && !collectionWorkbench.IsSurfaceMatchExperimentRunning
                && collectionWorkbench.RunLog.Count >= collectionLogs,
                $"result={collectionResult};failure={collectionFailure};collection={collectionWorkbench.SurfaceMatchCollection?.ContentSha256};expected={collection.ContentSha256};items={collectionWorkbench.SurfaceMatchCollectionItems.Count}/{collection.Items.Length};selected={collectionWorkbench.SelectedSurfaceMatchCollectionItem?.MatchId};expectedSelected={selectedMatchId};displayRequests={collectionDisplayRequests};dirty={collectionDirty}->{collectionWorkbench.IsDirty};steps={collectionSteps}->{collectionWorkbench.PipelineSteps.Count};selections={collectionSelections}->{collectionWorkbench.Selections.Count};logs={collectionLogs}->{collectionWorkbench.RunLog.Count}");
        }
        catch (Exception exception)
        {
            lines.Add(
                $"FAIL | unexpected-exception | {exception.GetType().Name}: {exception.Message}");
        }

        var allPassed = total > 0 && passed == total;
        lines.Insert(
            0,
            $"ShellSurfaceMatchSmokeVerification|{(allPassed ? "PASS" : "FAIL")}|checks={passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory);
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        summary =
            $"Shell Surface Match Smoke owner: {(allPassed ? "PASS" : "FAIL")} ({passed}/{total})";
        return allPassed;
    }
}
