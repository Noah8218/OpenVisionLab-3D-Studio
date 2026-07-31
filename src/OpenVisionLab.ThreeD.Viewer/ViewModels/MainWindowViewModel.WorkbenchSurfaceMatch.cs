using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private SurfaceMatchExecutionArtifact? workbenchSurfaceMatch;
    private SurfaceMatchAssessmentArtifact? workbenchSurfaceMatchAssessment;
    private SurfaceMatchRuntimeReport? workbenchSurfaceMatchRuntime;
    private SurfaceAndEdgeMatchScoreArtifact? workbenchSurfaceEdgeScore;

    public SurfaceMatchExecutionArtifact? WorkbenchSurfaceMatch =>
        workbenchSurfaceMatch;
    public SurfaceMatchAssessmentArtifact?
        WorkbenchSurfaceMatchAssessment =>
        workbenchSurfaceMatchAssessment;
    public SurfaceMatchRuntimeReport? WorkbenchSurfaceMatchRuntime =>
        workbenchSurfaceMatchRuntime;
    public SurfaceAndEdgeMatchScoreArtifact? WorkbenchSurfaceEdgeScore =>
        workbenchSurfaceEdgeScore;
    public bool SurfaceMatchEvidenceVisible =>
        workbenchSurfaceMatch?.Overlay is not null;
    public bool SurfaceMatchDecisionVisible =>
        workbenchSurfaceMatchAssessment is not null;
    public bool SurfaceMatchEdgeScoreVisible =>
        workbenchSurfaceEdgeScore is not null;
    public string SurfaceMatchStateLabel =>
        workbenchSurfaceMatch is null
            ? "Raw unavailable"
            : $"Raw {workbenchSurfaceMatch.PoseResult.State}";
    public string SurfaceMatchDecisionLabel =>
        workbenchSurfaceMatchAssessment is { } assessment
            ? assessment.Decision.ToString().ToUpperInvariant()
            : "NO DECISION";
    public string SurfaceMatchDecisionReasonLabel =>
        workbenchSurfaceMatchAssessment?.Reason switch
        {
            SurfaceMatchDecisionReason.MeetsAuthoredLimits =>
                "Raw evidence meets both authored limits",
            SurfaceMatchDecisionReason.PoseSearchNoMatch =>
                "Pose search rejected the input",
            SurfaceMatchDecisionReason.CoverageBelowMinimum =>
                "Coverage is below the authored minimum",
            SurfaceMatchDecisionReason.InlierRmseUnavailable =>
                "Inlier RMSE is unavailable",
            SurfaceMatchDecisionReason.InlierRmseAboveMaximum =>
                "Inlier RMSE exceeds the authored maximum",
            _ => "Display-only raw evidence"
        };
    public string SurfaceMatchPolicyLabel =>
        workbenchSurfaceMatchAssessment is { } assessment
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Coverage >= {assessment.Policy.MinimumCoverageRatio:P1} · RMSE <= {assessment.Policy.MaximumInlierRmse:G5}")
            : "Acceptance limits not supplied";
    public string SurfaceMatchRuntimeLabel =>
        workbenchSurfaceMatchRuntime is { } runtime
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{runtime.Stages.Length} stages · {runtime.TotalMilliseconds:F3} ms")
            : "Runtime not supplied";
    public string SurfaceMatchDecisionBoundaryLabel =>
        workbenchSurfaceEdgeScore is not null
            ? workbenchSurfaceMatchAssessment is null
                ? "Edge diagnostic only · no Pass/Fail decision"
                : "Decision uses surface limits · edge diagnostic only"
            : workbenchSurfaceMatchAssessment is null
                ? "View only · no Pass/Fail decision"
                : "Decision uses separate authored limits · raw score unchanged";
    public string SurfaceMatchCoverageLabel =>
        workbenchSurfaceMatch is { } execution
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{execution.PoseResult.Coverage.MatchedModelSampleCount}/{execution.PoseResult.Coverage.ModelSampleCount} · {execution.PoseResult.Coverage.CoverageRatio:P1}")
            : "—";
    public string SurfaceMatchRmseLabel =>
        workbenchSurfaceMatch?.PoseResult.Coverage.InlierRmse is { } rmse
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{rmse:G5} {workbenchSurfaceMatch.PoseResult.Pose?.Unit}")
            : "Unavailable";
    public string SurfaceMatchEdgeCoverageLabel =>
        workbenchSurfaceEdgeScore is { } score
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{score.EdgeScore.MatchedModelEdgeCount}/{score.EdgeScore.ModelEdgeCount} · {score.EdgeScore.CoverageRatio:P1}")
            : "Unavailable";
    public string SurfaceMatchEdgeRmseLabel =>
        workbenchSurfaceEdgeScore?.EdgeScore.InlierRmse is { } rmse
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{rmse:G5} {workbenchSurfaceMatch?.PoseResult.Pose?.Unit}")
            : "Unavailable";
    public string SurfaceMatchPoseLabel =>
        workbenchSurfaceMatch?.PoseResult.Pose is { } pose
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"R {pose.RotationAngleDegrees:F1}° · T {pose.TranslationMagnitude:F3} {pose.Unit}")
            : "Unavailable";
    public string SurfaceMatchModelHashLabel =>
        ShortHash(workbenchSurfaceMatch?.ModelContentSha256);
    public string SurfaceMatchOverlayHashLabel =>
        ShortHash(workbenchSurfaceMatch?.Overlay?.ContentSha256);
    public string SurfaceMatchEvidenceToolTip =>
        workbenchSurfaceMatch is { } execution
            ? $"Model SHA-256: {execution.ModelContentSha256}\n"
              + $"Scene SHA-256: {execution.SceneContentSha256}\n"
              + $"Pose SHA-256: {execution.PoseResult.ContentSha256}\n"
              + $"Overlay SHA-256: {execution.Overlay?.ContentSha256 ?? "(none)"}\n"
              + $"Execution SHA-256: {execution.ContentSha256}\n"
              + $"Policy SHA-256: {workbenchSurfaceMatchAssessment?.Policy.ContentSha256 ?? "(none)"}\n"
              + $"Assessment SHA-256: {workbenchSurfaceMatchAssessment?.ContentSha256 ?? "(none)"}\n"
              + $"Model edge SHA-256: {workbenchSurfaceEdgeScore?.ModelEdgeContentSha256 ?? "(none)"}\n"
              + $"Scene edge SHA-256: {workbenchSurfaceEdgeScore?.SceneEdgeContentSha256 ?? "(none)"}\n"
              + $"Surface/edge score SHA-256: {workbenchSurfaceEdgeScore?.ContentSha256 ?? "(none)"}\n"
              + "Runtime is observational and excluded from deterministic identities."
            : "No surface-match evidence.";

    internal void SetWorkbenchSurfaceMatch(
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchAssessmentArtifact? assessment,
        SurfaceMatchRuntimeReport? runtime,
        SurfaceAndEdgeMatchScoreArtifact? edgeScore)
    {
        ArgumentNullException.ThrowIfNull(execution);
        var validity =
            SurfaceMatchExecutionArtifactValidator.Inspect(execution);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Viewer requires valid surface-match execution evidence.");
        }

        if (assessment is not null
            && (!SurfaceMatchAssessmentArtifactValidator
                    .Inspect(assessment).IsValid
                || assessment.ExecutionContentSha256
                    != execution.ContentSha256))
        {
            throw new InvalidDataException(
                "Viewer requires assessment evidence linked to the raw execution.");
        }

        if (runtime is not null
            && (!SurfaceMatchAssessmentArtifactValidator
                    .InspectRuntime(runtime, out _)
                || assessment is null
                || runtime.ExecutionContentSha256
                    != execution.ContentSha256
                || runtime.AssessmentContentSha256
                    != assessment.ContentSha256))
        {
            throw new InvalidDataException(
                "Viewer requires runtime evidence linked to the raw execution and assessment.");
        }

        if (edgeScore is not null
            && !SurfaceEdgeArtifactValidator
                .Inspect(edgeScore, execution).IsValid)
        {
            throw new InvalidDataException(
                "Viewer requires surface/edge score evidence linked to the raw execution.");
        }

        workbenchSurfaceMatch = execution;
        workbenchSurfaceMatchAssessment = assessment;
        workbenchSurfaceMatchRuntime = runtime;
        workbenchSurfaceEdgeScore = edgeScore;
        SelectionSummary =
            $"Surface match {SurfaceMatchDecisionLabel} | {SurfaceMatchCoverageLabel} | overlay {SurfaceMatchOverlayHashLabel}";
        ViewerStatus = workbenchSurfaceMatchAssessment is null
            ? "Identified transformed SurfaceModel overlay · view only · no Pass/Fail"
            : $"Surface match {SurfaceMatchDecisionLabel} · separate authored limits · raw score unchanged";
        RaiseSurfaceMatchProperties();
    }

    internal void ClearWorkbenchSurfaceMatch()
    {
        workbenchSurfaceMatch = null;
        workbenchSurfaceMatchAssessment = null;
        workbenchSurfaceMatchRuntime = null;
        workbenchSurfaceEdgeScore = null;
        RaiseSurfaceMatchProperties();
    }

    private void RaiseSurfaceMatchProperties()
    {
        OnPropertyChanged(nameof(WorkbenchSurfaceMatch));
        OnPropertyChanged(nameof(WorkbenchSurfaceMatchAssessment));
        OnPropertyChanged(nameof(WorkbenchSurfaceMatchRuntime));
        OnPropertyChanged(nameof(WorkbenchSurfaceEdgeScore));
        OnPropertyChanged(nameof(SurfaceMatchEvidenceVisible));
        OnPropertyChanged(nameof(SurfaceMatchDecisionVisible));
        OnPropertyChanged(nameof(SurfaceMatchEdgeScoreVisible));
        OnPropertyChanged(nameof(SurfaceMatchStateLabel));
        OnPropertyChanged(nameof(SurfaceMatchDecisionLabel));
        OnPropertyChanged(nameof(SurfaceMatchDecisionReasonLabel));
        OnPropertyChanged(nameof(SurfaceMatchPolicyLabel));
        OnPropertyChanged(nameof(SurfaceMatchRuntimeLabel));
        OnPropertyChanged(nameof(SurfaceMatchDecisionBoundaryLabel));
        OnPropertyChanged(nameof(SurfaceMatchCoverageLabel));
        OnPropertyChanged(nameof(SurfaceMatchRmseLabel));
        OnPropertyChanged(nameof(SurfaceMatchEdgeCoverageLabel));
        OnPropertyChanged(nameof(SurfaceMatchEdgeRmseLabel));
        OnPropertyChanged(nameof(SurfaceMatchPoseLabel));
        OnPropertyChanged(nameof(SurfaceMatchModelHashLabel));
        OnPropertyChanged(nameof(SurfaceMatchOverlayHashLabel));
        OnPropertyChanged(nameof(SurfaceMatchEvidenceToolTip));
    }

    private static string ShortHash(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "—"
            : value[..Math.Min(10, value.Length)];
}
