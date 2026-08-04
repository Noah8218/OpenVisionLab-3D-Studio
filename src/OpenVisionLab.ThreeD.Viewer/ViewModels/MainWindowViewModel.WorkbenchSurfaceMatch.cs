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
    private SurfaceEdgeDiagnosticOverlayArtifact? workbenchSurfaceEdgeDiagnosticOverlay;
    private SurfaceEdgeAcquisitionDirectionArtifact? workbenchSurfaceEdgeAcquisitionDirection;
    private SurfaceAndEdgeMatchAssessmentArtifact? workbenchSurfaceEdgeAssessment;
    private SurfaceMatchFalsePositiveReviewArtifact? workbenchSurfaceMatchFalsePositiveReview;

    public SurfaceMatchExecutionArtifact? WorkbenchSurfaceMatch =>
        workbenchSurfaceMatch;
    public SurfaceMatchAssessmentArtifact?
        WorkbenchSurfaceMatchAssessment =>
        workbenchSurfaceMatchAssessment;
    public SurfaceMatchRuntimeReport? WorkbenchSurfaceMatchRuntime =>
        workbenchSurfaceMatchRuntime;
    public SurfaceAndEdgeMatchScoreArtifact? WorkbenchSurfaceEdgeScore =>
        workbenchSurfaceEdgeScore;
    public SurfaceEdgeDiagnosticOverlayArtifact? WorkbenchSurfaceEdgeDiagnosticOverlay =>
        workbenchSurfaceEdgeDiagnosticOverlay;
    public SurfaceEdgeAcquisitionDirectionArtifact? WorkbenchSurfaceEdgeAcquisitionDirection =>
        workbenchSurfaceEdgeAcquisitionDirection;
    public SurfaceAndEdgeMatchAssessmentArtifact? WorkbenchSurfaceEdgeAssessment =>
        workbenchSurfaceEdgeAssessment;
    public SurfaceMatchFalsePositiveReviewArtifact? WorkbenchSurfaceMatchFalsePositiveReview =>
        workbenchSurfaceMatchFalsePositiveReview;
    public bool SurfaceMatchEvidenceVisible =>
        workbenchSurfaceMatch?.Overlay is not null;
    public bool SurfaceMatchDecisionVisible =>
        workbenchSurfaceMatchAssessment is not null
        || workbenchSurfaceEdgeAssessment is not null;
    public bool SurfaceMatchEdgeScoreVisible =>
        workbenchSurfaceEdgeScore is not null;
    public bool SurfaceMatchEdgeDiagnosticVisible =>
        workbenchSurfaceEdgeDiagnosticOverlay is not null;
    public bool SurfaceMatchAcquisitionDirectionVisible =>
        workbenchSurfaceEdgeAcquisitionDirection is not null;
    public bool SurfaceMatchFalsePositiveReviewVisible =>
        workbenchSurfaceMatchFalsePositiveReview is not null;
    public string SurfaceMatchStateLabel =>
        workbenchSurfaceMatch is null
            ? "Raw unavailable"
            : $"Raw {workbenchSurfaceMatch.PoseResult.State}";
    public string SurfaceMatchDecisionLabel =>
        workbenchSurfaceEdgeAssessment is { } independent
            ? independent.Decision.ToString().ToUpperInvariant()
            : workbenchSurfaceMatchAssessment is { } assessment
                ? assessment.Decision.ToString().ToUpperInvariant()
                : "NO DECISION";
    public string SurfaceMatchDecisionReasonLabel =>
        workbenchSurfaceEdgeAssessment is { } independent
            ? independent.Reason switch
            {
                SurfaceAndEdgeDecisionReason.BothComponentsMeetAuthoredLimits =>
                    "Surface and 3D-edge evidence meet their independent limits",
                SurfaceAndEdgeDecisionReason.SurfaceCoverageBelowMinimum =>
                    "Surface coverage is below its authored minimum",
                SurfaceAndEdgeDecisionReason.SurfaceInlierRmseUnavailable =>
                    "Surface RMSE is unavailable",
                SurfaceAndEdgeDecisionReason.SurfaceInlierRmseAboveMaximum =>
                    "Surface RMSE exceeds its authored maximum",
                SurfaceAndEdgeDecisionReason.EdgeCoverageBelowMinimum =>
                    "3D-edge coverage exposes a surface-only false positive",
                SurfaceAndEdgeDecisionReason.EdgeInlierRmseUnavailable =>
                    "3D-edge RMSE is unavailable",
                SurfaceAndEdgeDecisionReason.EdgeInlierRmseAboveMaximum =>
                    "3D-edge RMSE exceeds its authored maximum",
                _ => "Independent surface and 3D-edge evidence"
            }
            : workbenchSurfaceMatchAssessment?.Reason switch
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
        workbenchSurfaceEdgeAssessment is { } independent
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Surface >= {independent.Policy.Surface.MinimumCoverageRatio:P1}, RMSE <= {independent.Policy.Surface.MaximumInlierRmse:G5} | Edge >= {independent.Policy.Edge.MinimumCoverageRatio:P1}, RMSE <= {independent.Policy.Edge.MaximumInlierRmse:G5}")
            : workbenchSurfaceMatchAssessment is { } assessment
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
        workbenchSurfaceEdgeAssessment is not null
            ? "Independent surface + edge limits · no weighted score"
            : workbenchSurfaceEdgeScore is not null
            ? workbenchSurfaceMatchAssessment is null
                ? "Edge diagnostic only · no Pass/Fail decision"
                : "Decision uses surface limits · edge diagnostic only"
            : workbenchSurfaceMatchAssessment is null
                ? "View only · no Pass/Fail decision"
                : "Decision uses separate authored limits · raw score unchanged";
    public string SurfaceMatchEdgeDiagnosticLabel =>
        workbenchSurfaceEdgeDiagnosticOverlay is { } overlay
            ? workbenchSurfaceEdgeAcquisitionDirection is { } orientation
                ? $"Model edge {overlay.ModelSegments.Length} · scene step {overlay.SceneSegments.Length} · facing {orientation.Items.Count(item => item.Orientation == SurfaceEdgeAcquisitionOrientation.SensorFacing)} · away {orientation.Items.Count(item => item.Orientation == SurfaceEdgeAcquisitionOrientation.AwayFromSensor)} · grazing {orientation.Items.Count(item => item.Orientation == SurfaceEdgeAcquisitionOrientation.Grazing)} · {orientation.FrameId}"
                : $"Model edge {overlay.ModelSegments.Length} · scene step {overlay.SceneSegments.Length} · declared normals · acquisition direction unavailable"
            : "Edge directions unavailable";
    public string SurfaceMatchAcceptedReviewLabel =>
        workbenchSurfaceMatchFalsePositiveReview is { } review
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"ACCEPTED  Surface {review.Accepted.SurfaceCoverageRatio:P0} · Edge {review.Accepted.EdgeCoverageRatio:P0}")
            : "Accepted reference unavailable";
    public string SurfaceMatchRejectedReviewLabel =>
        workbenchSurfaceMatchFalsePositiveReview is { } review
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"REJECTED  Surface {review.Rejected.SurfaceCoverageRatio:P0} · Edge {review.Rejected.EdgeCoverageRatio:P0}")
            : "Rejected candidate unavailable";
    public string SurfaceMatchReviewEvidenceLabel =>
        workbenchSurfaceMatchFalsePositiveReview is { } review
            ? $"Original scenes + samples retained · {ShortHash(review.ContentSha256)}"
            : "Review evidence unavailable";
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
              + $"Edge diagnostic overlay SHA-256: {workbenchSurfaceEdgeDiagnosticOverlay?.ContentSha256 ?? "(none)"}\n"
              + $"Acquisition orientation SHA-256: {workbenchSurfaceEdgeAcquisitionDirection?.ContentSha256 ?? "(none)"}\n"
              + $"Independent surface/edge assessment SHA-256: {workbenchSurfaceEdgeAssessment?.ContentSha256 ?? "(none)"}\n"
              + $"False-positive review SHA-256: {workbenchSurfaceMatchFalsePositiveReview?.ContentSha256 ?? "(none)"}\n"
              + "Runtime is observational and excluded from deterministic identities."
            : "No surface-match evidence.";

    internal void SetWorkbenchSurfaceMatch(
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchAssessmentArtifact? assessment,
        SurfaceMatchRuntimeReport? runtime,
        SurfaceAndEdgeMatchScoreArtifact? edgeScore,
        SurfaceEdgeDiagnosticOverlayArtifact? edgeDiagnosticOverlay,
        SurfaceAndEdgeMatchAssessmentArtifact? edgeAssessment,
        SurfaceMatchFalsePositiveReviewArtifact? falsePositiveReview,
        SurfaceEdgeAcquisitionDirectionArtifact? acquisitionDirectionOrientation = null)
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

        if (edgeDiagnosticOverlay is not null
            && (edgeScore is null
                || !SurfaceEdgeDiagnosticOverlayArtifactValidator
                    .Inspect(edgeDiagnosticOverlay).IsValid
                || edgeDiagnosticOverlay.SurfaceMatchExecutionContentSha256
                    != execution.ContentSha256
                || edgeDiagnosticOverlay.ScoreContentSha256
                    != edgeScore.ContentSha256))
        {
            throw new InvalidDataException(
                "Viewer requires edge diagnostic overlay evidence linked to the raw score.");
        }

        if (edgeAssessment is not null
            && (edgeScore is null
                || !SurfaceAndEdgeAssessmentArtifactValidator
                    .Inspect(edgeAssessment, edgeScore).IsValid))
        {
            throw new InvalidDataException(
                "Viewer requires independent surface/edge assessment evidence linked to the raw score.");
        }

        if (acquisitionDirectionOrientation is not null
            && (edgeDiagnosticOverlay is null
                || !SurfaceEdgeAcquisitionDirectionArtifactValidator
                    .Inspect(acquisitionDirectionOrientation, edgeDiagnosticOverlay).IsValid))
        {
            throw new InvalidDataException(
                "Viewer requires acquisition-direction orientation linked to the displayed edge overlay.");
        }

        if (falsePositiveReview is not null
            && !SurfaceMatchFalsePositiveReviewArtifactValidator
                .Inspect(falsePositiveReview).IsValid)
        {
            throw new InvalidDataException(
                "Viewer requires valid retained false-positive review evidence.");
        }

        workbenchSurfaceMatch = execution;
        workbenchSurfaceMatchAssessment = assessment;
        workbenchSurfaceMatchRuntime = runtime;
        workbenchSurfaceEdgeScore = edgeScore;
        workbenchSurfaceEdgeDiagnosticOverlay = edgeDiagnosticOverlay;
        workbenchSurfaceEdgeAcquisitionDirection = acquisitionDirectionOrientation;
        workbenchSurfaceEdgeAssessment = edgeAssessment;
        workbenchSurfaceMatchFalsePositiveReview = falsePositiveReview;
        SelectionSummary =
            $"Surface match {SurfaceMatchDecisionLabel} | {SurfaceMatchCoverageLabel} | overlay {SurfaceMatchOverlayHashLabel}";
        ViewerStatus = workbenchSurfaceEdgeAssessment is not null
            ? $"Surface/edge {SurfaceMatchDecisionLabel} · independent limits · raw scores unchanged"
            : workbenchSurfaceMatchAssessment is null
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
        workbenchSurfaceEdgeDiagnosticOverlay = null;
        workbenchSurfaceEdgeAcquisitionDirection = null;
        workbenchSurfaceEdgeAssessment = null;
        workbenchSurfaceMatchFalsePositiveReview = null;
        RaiseSurfaceMatchProperties();
    }

    private void RaiseSurfaceMatchProperties()
    {
        OnPropertyChanged(nameof(WorkbenchSurfaceMatch));
        OnPropertyChanged(nameof(WorkbenchSurfaceMatchAssessment));
        OnPropertyChanged(nameof(WorkbenchSurfaceMatchRuntime));
        OnPropertyChanged(nameof(WorkbenchSurfaceEdgeScore));
        OnPropertyChanged(nameof(WorkbenchSurfaceEdgeDiagnosticOverlay));
        OnPropertyChanged(nameof(WorkbenchSurfaceEdgeAcquisitionDirection));
        OnPropertyChanged(nameof(WorkbenchSurfaceEdgeAssessment));
        OnPropertyChanged(nameof(WorkbenchSurfaceMatchFalsePositiveReview));
        OnPropertyChanged(nameof(SurfaceMatchEvidenceVisible));
        OnPropertyChanged(nameof(SurfaceMatchDecisionVisible));
        OnPropertyChanged(nameof(SurfaceMatchEdgeScoreVisible));
        OnPropertyChanged(nameof(SurfaceMatchEdgeDiagnosticVisible));
        OnPropertyChanged(nameof(SurfaceMatchAcquisitionDirectionVisible));
        OnPropertyChanged(nameof(SurfaceMatchFalsePositiveReviewVisible));
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
        OnPropertyChanged(nameof(SurfaceMatchEdgeDiagnosticLabel));
        OnPropertyChanged(nameof(SurfaceMatchAcceptedReviewLabel));
        OnPropertyChanged(nameof(SurfaceMatchRejectedReviewLabel));
        OnPropertyChanged(nameof(SurfaceMatchReviewEvidenceLabel));
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
