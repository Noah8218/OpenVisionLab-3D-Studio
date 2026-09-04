using System.IO;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Artifacts;

internal static class SurfaceEdgeWorkbenchParityVerification
{
    public static bool Verify(
        string modelPath,
        string scenePath,
        string executionPath,
        string modelEdgePath,
        string sceneEdgePath,
        string runnerScorePath,
        string reportPath,
        out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Surface Edge Workbench/Runner parity verification",
            "Boundary|Deterministic local fixture; edge score is diagnostic only; no automatic Preview, Publish, Run, Validation, acceptance-policy, or physical-metrology claim."
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
            var model = SurfaceModelArtifactStore.Load(modelPath);
            var scene = PreparedSceneArtifactStore.Load(scenePath);
            var execution = SurfaceMatchExecutionArtifactStore.Load(executionPath);
            var modelEdges = SurfaceEdgeArtifactStore.LoadModel(modelEdgePath);
            var sceneEdges = SurfaceEdgeArtifactStore.LoadScene(sceneEdgePath);
            var runnerScore = SurfaceEdgeArtifactStore.LoadScore(runnerScorePath);
            var workbenchScore = SurfaceAndEdgeMatchScorer.Evaluate(
                execution,
                modelEdges,
                sceneEdges,
                runnerScore.EdgeScore.MaximumCorrespondenceDistance);
            var workbench = new ToolWorkbenchViewModel();
            var initialDirty = workbench.IsDirty;
            var initialStepCount = workbench.PipelineSteps.Count;
            ToolWorkbenchSurfaceMatchDisplayRequestEventArgs? request = null;
            var requestCount = 0;
            var clearRequestCount = 0;
            workbench.SurfaceMatchDisplayRequested += (_, args) =>
            {
                request = args;
                requestCount++;
            };
            workbench.SurfaceMatchDisplayCleared += (_, _) =>
                clearRequestCount++;

            workbench.ShowSurfaceMatchEvidence(
                model,
                scene,
                execution,
                edgeScore: workbenchScore);

            Check(
                "runner-and-workbench-score-hash-match",
                runnerScore.ContentSha256 == workbenchScore.ContentSha256,
                $"runner={runnerScore.ContentSha256};workbench={workbenchScore.ContentSha256}");
            Check(
                "runner-and-workbench-surface-score-match",
                runnerScore.SurfaceScore == workbenchScore.SurfaceScore,
                $"runner={runnerScore.SurfaceScore.CoverageRatio:G17};workbench={workbenchScore.SurfaceScore.CoverageRatio:G17}");
            Check(
                "runner-and-workbench-edge-score-match",
                runnerScore.EdgeScore.CoverageRatio == workbenchScore.EdgeScore.CoverageRatio
                && runnerScore.EdgeScore.InlierRmse == workbenchScore.EdgeScore.InlierRmse
                && runnerScore.EdgeScore.MatchedModelEdgeCount == workbenchScore.EdgeScore.MatchedModelEdgeCount
                && runnerScore.EdgeScore.Matches.SequenceEqual(workbenchScore.EdgeScore.Matches),
                $"runner={runnerScore.EdgeScore.Evidence};workbench={workbenchScore.EdgeScore.Evidence}");
            Check(
                "score-links-immutable-execution",
                runnerScore.SurfaceMatchExecutionContentSha256 == execution.ContentSha256
                && workbenchScore.SurfaceMatchExecutionContentSha256 == execution.ContentSha256,
                execution.ContentSha256);
            Check(
                "score-links-identified-edge-artifacts",
                runnerScore.ModelEdgeContentSha256 == modelEdges.ContentSha256
                && runnerScore.SceneEdgeContentSha256 == sceneEdges.ContentSha256,
                $"modelEdges={modelEdges.ContentSha256};sceneEdges={sceneEdges.ContentSha256}");
            Check(
                "workbench-owns-edge-score-evidence",
                ReferenceEquals(workbench.SurfaceEdgeScore, workbenchScore)
                && workbench.HasSurfaceMatchEvidence,
                $"hasSurface={workbench.HasSurfaceMatchEvidence};score={workbench.SurfaceEdgeScore?.ContentSha256}");
            Check(
                "workbench-routes-edge-score-once",
                requestCount == 1
                && request is not null
                && ReferenceEquals(request.EdgeScore, workbenchScore)
                && ReferenceEquals(request.Execution, execution),
                $"requestCount={requestCount}");
            Check(
                "edge-score-display-is-presentation-only",
                workbench.IsDirty == initialDirty
                && workbench.PipelineSteps.Count == initialStepCount
                && workbench.SelectedPipelineStep is null,
                $"dirty={workbench.IsDirty};steps={workbench.PipelineSteps.Count}");
            Check(
                "edge-score-remains-decision-free",
                runnerScore.Semantics == SurfaceAndEdgeMatchScoreArtifact.CurrentSemantics
                && runnerScore.Semantics.Contains("no-acceptance", StringComparison.Ordinal),
                runnerScore.Semantics);
            Check(
                "surface-and-edge-values-remain-separate",
                runnerScore.SurfaceScore.ModelSampleCount == 2
                && runnerScore.EdgeScore.ModelEdgeCount == 4
                && !ReferenceEquals(runnerScore.SurfaceScore, runnerScore.EdgeScore),
                $"surface={runnerScore.SurfaceScore.ModelSampleCount};edge={runnerScore.EdgeScore.ModelEdgeCount}");

            workbench.ClearSurfaceMatchEvidence();
            Check(
                "clear-removes-edge-score-without-editing",
                workbench.SurfaceEdgeScore is null
                && !workbench.HasSurfaceMatchEvidence
                && clearRequestCount == 1
                && workbench.IsDirty == initialDirty
                && workbench.PipelineSteps.Count == initialStepCount,
                $"clearRequests={clearRequestCount};dirty={workbench.IsDirty}");
            Check(
                "score-validates-after-parity",
                SurfaceEdgeArtifactValidator
                    .Inspect(workbenchScore, execution).IsValid,
                SurfaceEdgeArtifactValidator
                    .Inspect(workbenchScore, execution).Evidence);
        }
        catch (Exception exception)
        {
            lines.Add(
                $"FAIL | unexpected-exception | {exception.GetType().Name}: {exception.Message}");
        }

        var allPassed = passed == total && total > 0;
        lines.Insert(
            0,
            $"SurfaceEdgeWorkbenchParityVerification|{(allPassed ? "PASS" : "FAIL")}|cases={total}|passed={passed}|failed={total - passed}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
        summary =
            $"Surface edge Workbench/Runner parity: {(allPassed ? "PASS" : "FAIL")} ({passed}/{total})";
        return allPassed;
    }
}
