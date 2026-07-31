using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record SurfaceMatchReviewEvidenceSet(
    string Label,
    PreparedSceneArtifact Scene,
    SurfaceMatchExecutionArtifact Execution,
    SurfaceAndEdgeMatchScoreArtifact Score,
    SurfaceAndEdgeMatchAssessmentArtifact Assessment);

/// <summary>
/// Retains a controlled accepted reference and surface-only false-positive
/// candidate without copying or mutating their source artifacts.
/// </summary>
public static class SurfaceMatchFalsePositiveReviewBuilder
{
    public static SurfaceMatchFalsePositiveReviewArtifact Build(
        SurfaceModelArtifact model,
        SurfaceMatchReviewEvidenceSet accepted,
        SurfaceMatchReviewEvidenceSet rejected)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(rejected);
        if (!SurfaceModelArtifactValidator.Inspect(model).IsValid)
        {
            throw new InvalidDataException(
                "Surface-match review requires a valid identified model.");
        }

        ValidateCase(model, accepted);
        ValidateCase(model, rejected);
        if (accepted.Assessment.Decision != SurfaceMatchDecision.Pass
            || rejected.Assessment.Decision != SurfaceMatchDecision.Fail
            || accepted.Assessment.Surface.Decision != SurfaceMatchDecision.Pass
            || rejected.Assessment.Surface.Decision != SurfaceMatchDecision.Pass
            || accepted.Assessment.Edge.Decision != SurfaceMatchDecision.Pass
            || rejected.Assessment.Edge.Decision != SurfaceMatchDecision.Fail)
        {
            throw new InvalidDataException(
                "False-positive review requires surface-pass cases separated by edge Pass versus Fail evidence.");
        }

        var artifact = new SurfaceMatchFalsePositiveReviewArtifact(
            SurfaceMatchFalsePositiveReviewArtifact.CurrentSchemaVersion,
            SurfaceMatchFalsePositiveReviewArtifact.CurrentSemantics,
            model.ContentSha256,
            model.Samples.Length,
            CreateCase(SurfaceMatchReviewCaseRole.AcceptedReference, accepted),
            CreateCase(SurfaceMatchReviewCaseRole.RejectedCandidate, rejected),
            "Original model, Prepared Scene samples, pose, and separate scores retained; no weighted score and no source mutation.",
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = SurfaceMatchFalsePositiveReviewArtifact
                .CalculateContentSha256(artifact)
        };
        var validity =
            SurfaceMatchFalsePositiveReviewArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface-match false-positive review is invalid: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }

    private static SurfaceMatchFalsePositiveReviewCase CreateCase(
        SurfaceMatchReviewCaseRole role,
        SurfaceMatchReviewEvidenceSet evidence) =>
        new(
            role,
            evidence.Label.Trim(),
            evidence.Scene.ContentSha256,
            evidence.Scene.Samples.Length,
            evidence.Execution.ContentSha256,
            evidence.Execution.PoseResult.ContentSha256,
            evidence.Score.ContentSha256,
            evidence.Assessment.ContentSha256,
            evidence.Score.SurfaceScore.CoverageRatio,
            evidence.Score.EdgeScore.CoverageRatio,
            evidence.Assessment.Decision,
            evidence.Assessment.Reason);

    private static void ValidateCase(
        SurfaceModelArtifact model,
        SurfaceMatchReviewEvidenceSet evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence.Label);
        if (!PreparedSceneArtifactValidator.Inspect(evidence.Scene).IsValid
            || !SurfaceMatchExecutionArtifactValidator
                .Inspect(evidence.Execution).IsValid
            || !SurfaceEdgeArtifactValidator
                .Inspect(evidence.Score, evidence.Execution).IsValid
            || !SurfaceAndEdgeAssessmentArtifactValidator
                .Inspect(evidence.Assessment, evidence.Score).IsValid
            || evidence.Execution.ModelContentSha256 != model.ContentSha256
            || evidence.Execution.SceneContentSha256
                != evidence.Scene.ContentSha256)
        {
            throw new InvalidDataException(
                "Surface-match review case has invalid or mismatched model, scene, execution, score, or assessment evidence.");
        }
    }
}
