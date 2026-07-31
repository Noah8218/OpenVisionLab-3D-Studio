using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Applies independent surface and edge limits to an immutable raw score.
/// No weighted or blended score is created.
/// </summary>
public static class SurfaceAndEdgeMatchAssessmentEvaluator
{
    public static SurfaceAndEdgeMatchAssessmentArtifact Evaluate(
        SurfaceAndEdgeMatchScoreArtifact score,
        SurfaceAndEdgeMatchAcceptancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(policy);
        var scoreValid = SurfaceEdgeArtifactValidator.Inspect(score).IsValid;
        var policyValid = SurfaceAndEdgeAssessmentArtifactValidator
            .InspectPolicy(policy, out var policyEvidence);
        if (!scoreValid || !policyValid)
        {
            throw new InvalidDataException(
                $"Surface/edge assessment requires valid score and policy evidence: {policyEvidence}");
        }

        var surfaceExpected =
            SurfaceAndEdgeAssessmentArtifactValidator.ExpectedComponent(
                score.SurfaceScore.CoverageRatio,
                score.SurfaceScore.InlierRmse,
                policy.Surface.MinimumCoverageRatio,
                policy.Surface.MaximumInlierRmse);
        var edgeExpected =
            SurfaceAndEdgeAssessmentArtifactValidator.ExpectedComponent(
                score.EdgeScore.CoverageRatio,
                score.EdgeScore.InlierRmse,
                policy.Edge.MinimumCoverageRatio,
                policy.Edge.MaximumInlierRmse);
        var surface = new SurfaceAndEdgeComponentAssessment(
            score.SurfaceScore.CoverageRatio,
            score.SurfaceScore.InlierRmse,
            policy.Surface.MinimumCoverageRatio,
            policy.Surface.MaximumInlierRmse,
            surfaceExpected.Decision,
            surfaceExpected.Reason);
        var edge = new SurfaceAndEdgeComponentAssessment(
            score.EdgeScore.CoverageRatio,
            score.EdgeScore.InlierRmse,
            policy.Edge.MinimumCoverageRatio,
            policy.Edge.MaximumInlierRmse,
            edgeExpected.Decision,
            edgeExpected.Reason);
        var decision = surface.Decision == SurfaceMatchDecision.Pass
            && edge.Decision == SurfaceMatchDecision.Pass
                ? SurfaceMatchDecision.Pass
                : SurfaceMatchDecision.Fail;
        var reason = surface.Decision != SurfaceMatchDecision.Pass
            ? surface.Reason switch
            {
                SurfaceAndEdgeComponentDecisionReason.CoverageBelowMinimum =>
                    SurfaceAndEdgeDecisionReason.SurfaceCoverageBelowMinimum,
                SurfaceAndEdgeComponentDecisionReason.InlierRmseUnavailable =>
                    SurfaceAndEdgeDecisionReason.SurfaceInlierRmseUnavailable,
                _ => SurfaceAndEdgeDecisionReason.SurfaceInlierRmseAboveMaximum
            }
            : edge.Decision != SurfaceMatchDecision.Pass
                ? edge.Reason switch
                {
                    SurfaceAndEdgeComponentDecisionReason.CoverageBelowMinimum =>
                        SurfaceAndEdgeDecisionReason.EdgeCoverageBelowMinimum,
                    SurfaceAndEdgeComponentDecisionReason.InlierRmseUnavailable =>
                        SurfaceAndEdgeDecisionReason.EdgeInlierRmseUnavailable,
                    _ => SurfaceAndEdgeDecisionReason.EdgeInlierRmseAboveMaximum
                }
                : SurfaceAndEdgeDecisionReason.BothComponentsMeetAuthoredLimits;
        var artifact = new SurfaceAndEdgeMatchAssessmentArtifact(
            SurfaceAndEdgeMatchAssessmentArtifact.CurrentSchemaVersion,
            SurfaceAndEdgeMatchAssessmentArtifact.CurrentSemantics,
            score.SurfaceMatchExecutionContentSha256,
            score.ContentSha256,
            policy,
            surface,
            edge,
            decision,
            reason,
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = SurfaceAndEdgeMatchAssessmentArtifact
                .CalculateContentSha256(artifact)
        };
        var validity = SurfaceAndEdgeAssessmentArtifactValidator
            .Inspect(artifact, score);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface/edge assessment is invalid: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }
}
