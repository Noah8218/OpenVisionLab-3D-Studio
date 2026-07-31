using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record SurfaceMatchEvaluationResult(
    SurfaceMatchExecutionArtifact Execution,
    SurfaceMatchAssessmentArtifact Assessment,
    SurfaceMatchRuntimeReport Runtime);

/// <summary>
/// Shared Workbench/Runner boundary for a raw match followed by a separate
/// authored acceptance decision. Runtime is observational and is never part
/// of a deterministic hash or decision.
/// </summary>
public static class SurfaceMatchEvaluationExecutor
{
    public static SurfaceMatchEvaluationResult Execute(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidSurfacePoseSearchParameters parameters,
        SurfaceMatchAcceptancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(policy);

        var stages = new List<SurfaceMatchRuntimeStage>(3);
        var started = Stopwatch.GetTimestamp();
        var pose = RigidSurfacePoseSearch.Execute(
            model,
            scene,
            parameters);
        stages.Add(Stage(
            SurfaceMatchRuntimeReport.PoseSearchStage,
            started));

        started = Stopwatch.GetTimestamp();
        var execution = SurfaceMatchExecutionArtifact.Create(
            model,
            scene,
            pose);
        stages.Add(Stage(
            SurfaceMatchRuntimeReport.ExecutionArtifactStage,
            started));

        started = Stopwatch.GetTimestamp();
        var expected =
            SurfaceMatchAssessmentArtifactValidator
                .ExpectedDecision(
                    execution.PoseResult.State,
                    execution.PoseResult.Coverage.CoverageRatio,
                    execution.PoseResult.Coverage.InlierRmse,
                    policy);
        var assessment = SurfaceMatchAssessmentArtifact.Create(
            execution,
            policy,
            expected.Decision,
            expected.Reason);
        stages.Add(Stage(
            SurfaceMatchRuntimeReport.AcceptanceEvaluationStage,
            started));

        var runtime = new SurfaceMatchRuntimeReport(
            SurfaceMatchRuntimeReport.CurrentSchemaVersion,
            SurfaceMatchRuntimeReport.CurrentClock,
            execution.ContentSha256,
            assessment.ContentSha256,
            stages.ToArray(),
            stages.Sum(stage => stage.ElapsedTicks),
            DateTimeOffset.UtcNow);
        if (!SurfaceMatchAssessmentArtifactValidator
                .InspectRuntime(runtime, out var runtimeEvidence))
        {
            throw new InvalidDataException(
                $"Surface match runtime report is invalid: {runtimeEvidence}");
        }

        return new SurfaceMatchEvaluationResult(
            execution,
            assessment,
            runtime);
    }

    private static SurfaceMatchRuntimeStage Stage(
        string stageId,
        long started) =>
        new(
            stageId,
            Stopwatch.GetElapsedTime(started).Ticks);
}
