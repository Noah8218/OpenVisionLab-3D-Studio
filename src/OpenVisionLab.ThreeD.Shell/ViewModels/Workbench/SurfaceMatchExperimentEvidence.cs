using System.IO;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Immutable published/candidate evidence. Display admission validates linked artifacts
/// before the caller changes the visible selection or published state.
/// </summary>
internal sealed record SurfaceMatchExperimentEvidence(
    SurfaceModelArtifact Model,
    PreparedSceneArtifact Scene,
    SurfaceMatchExecutionArtifact Execution,
    SurfaceMatchAssessmentArtifact? Assessment,
    SurfaceMatchRuntimeReport? Runtime,
    SurfaceAndEdgeMatchScoreArtifact? EdgeScore,
    SurfaceEdgeDiagnosticOverlayArtifact? EdgeDiagnosticOverlay,
    SurfaceAndEdgeMatchAssessmentArtifact? EdgeAssessment,
    SurfaceMatchFalsePositiveReviewArtifact? FalsePositiveReview,
    SurfaceEdgeAcquisitionDirectionArtifact? AcquisitionDirectionOrientation = null)
{
    public static SurfaceMatchExperimentEvidence CreateForDisplay(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchAssessmentArtifact? assessment = null,
        SurfaceMatchRuntimeReport? runtime = null,
        SurfaceAndEdgeMatchScoreArtifact? edgeScore = null,
        SurfaceEdgeDiagnosticOverlayArtifact? edgeDiagnosticOverlay = null,
        SurfaceAndEdgeMatchAssessmentArtifact? edgeAssessment = null,
        SurfaceMatchFalsePositiveReviewArtifact? falsePositiveReview = null,
        SurfaceEdgeAcquisitionDirectionArtifact? acquisitionDirectionOrientation = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(execution);
        var validity =
            SurfaceMatchExecutionArtifactValidator.Inspect(execution);
        if (!validity.IsValid
            || !string.Equals(
                model.ContentSha256,
                execution.ModelContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                scene.ContentSha256,
                execution.SceneContentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Workbench surface-match evidence is invalid or does not match the supplied model and scene.");
        }

        if (assessment is not null)
        {
            var assessmentValidity =
                SurfaceMatchAssessmentArtifactValidator.Inspect(
                    assessment);
            if (!assessmentValidity.IsValid
                || !string.Equals(
                    assessment.ExecutionContentSha256,
                    execution.ContentSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Workbench surface-match assessment is invalid or linked to a different raw execution.");
            }
        }

        if (runtime is not null
            && (!SurfaceMatchAssessmentArtifactValidator
                    .InspectRuntime(runtime, out _)
                || assessment is null
                || !string.Equals(
                    runtime.ExecutionContentSha256,
                    execution.ContentSha256,
                    StringComparison.Ordinal)
                || !string.Equals(
                    runtime.AssessmentContentSha256,
                    assessment.ContentSha256,
                    StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Workbench surface-match runtime is invalid or linked to different execution evidence.");
        }

        if (edgeScore is not null
            && !SurfaceEdgeArtifactValidator
                .Inspect(edgeScore, execution).IsValid)
        {
            throw new InvalidDataException(
                "Workbench surface/edge score is invalid or linked to a different raw execution.");
        }

        if (edgeDiagnosticOverlay is not null
            && (edgeScore is null
                || !SurfaceEdgeDiagnosticOverlayArtifactValidator
                    .Inspect(edgeDiagnosticOverlay).IsValid
                || edgeDiagnosticOverlay.SurfaceMatchExecutionContentSha256
                    != execution.ContentSha256
                || edgeDiagnosticOverlay.ModelContentSha256
                    != model.ContentSha256
                || edgeDiagnosticOverlay.SceneContentSha256
                    != scene.ContentSha256
                || edgeDiagnosticOverlay.ScoreContentSha256
                    != edgeScore.ContentSha256))
        {
            throw new InvalidDataException(
                "Workbench edge diagnostic overlay is invalid or linked to different evidence.");
        }

        if (edgeAssessment is not null
            && (edgeScore is null
                || !SurfaceAndEdgeAssessmentArtifactValidator
                    .Inspect(edgeAssessment, edgeScore).IsValid))
        {
            throw new InvalidDataException(
                "Workbench independent surface/edge assessment is invalid or linked to a different score.");
        }

        if (acquisitionDirectionOrientation is not null
            && (edgeDiagnosticOverlay is null
                || !SurfaceEdgeAcquisitionDirectionArtifactValidator
                    .Inspect(acquisitionDirectionOrientation, edgeDiagnosticOverlay).IsValid))
        {
            throw new InvalidDataException(
                "Workbench acquisition-direction orientation is invalid or linked to a different edge overlay.");
        }

        if (falsePositiveReview is not null
            && (!SurfaceMatchFalsePositiveReviewArtifactValidator
                    .Inspect(falsePositiveReview).IsValid
                || falsePositiveReview.ModelContentSha256
                    != model.ContentSha256
                || !ReviewContains(
                    falsePositiveReview,
                    scene,
                    execution,
                    edgeScore,
                    edgeAssessment)))
        {
            throw new InvalidDataException(
                "Workbench false-positive review is invalid or does not contain the displayed case.");
        }

        return new SurfaceMatchExperimentEvidence(
            model, scene, execution, assessment, runtime, edgeScore, edgeDiagnosticOverlay,
            edgeAssessment, falsePositiveReview, acquisitionDirectionOrientation);
    }

    private static bool ReviewContains(
        SurfaceMatchFalsePositiveReviewArtifact review,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceAndEdgeMatchScoreArtifact? score,
        SurfaceAndEdgeMatchAssessmentArtifact? assessment)
    {
        if (score is null || assessment is null)
        {
            return false;
        }

        return new[] { review.Accepted, review.Rejected }.Any(item =>
            item.SceneContentSha256 == scene.ContentSha256
            && item.SurfaceMatchExecutionContentSha256 == execution.ContentSha256
            && item.ScoreContentSha256 == score.ContentSha256
            && item.AssessmentContentSha256 == assessment.ContentSha256);
    }

}
