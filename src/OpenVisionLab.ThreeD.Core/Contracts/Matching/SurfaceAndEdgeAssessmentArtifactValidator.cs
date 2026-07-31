namespace OpenVisionLab.ThreeD.Core;

public sealed record SurfaceAndEdgeAssessmentValidityReport(
    string SchemaVersion,
    bool IsValid,
    bool PolicyIdentityValid,
    bool AssessmentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";
}

public static class SurfaceAndEdgeAssessmentArtifactValidator
{
    public static SurfaceAndEdgeAssessmentValidityReport Inspect(
        SurfaceAndEdgeMatchAssessmentArtifact artifact,
        SurfaceAndEdgeMatchScoreArtifact? score = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        if (artifact.SchemaVersion
                != SurfaceAndEdgeMatchAssessmentArtifact.CurrentSchemaVersion
            || artifact.Semantics
                != SurfaceAndEdgeMatchAssessmentArtifact.CurrentSemantics)
        {
            errors.Add("Surface/edge assessment schema or semantics are unsupported.");
        }

        if (!IsCanonicalSha256(artifact.SurfaceMatchExecutionContentSha256)
            || !IsCanonicalSha256(artifact.ScoreContentSha256))
        {
            errors.Add("Surface/edge assessment requires canonical execution and score identities.");
        }

        var policyIdentityValid = InspectPolicy(artifact.Policy, errors);
        if (artifact.Surface is null || artifact.Edge is null)
        {
            errors.Add("Surface/edge assessment requires both component assessments.");
        }
        else if (artifact.Policy is not null)
        {
            InspectComponent(
                artifact.Surface,
                artifact.Policy.Surface.MinimumCoverageRatio,
                artifact.Policy.Surface.MaximumInlierRmse,
                "Surface",
                errors);
            InspectComponent(
                artifact.Edge,
                artifact.Policy.Edge.MinimumCoverageRatio,
                artifact.Policy.Edge.MaximumInlierRmse,
                "Edge",
                errors);

            var expectedDecision = artifact.Surface.Decision == SurfaceMatchDecision.Pass
                && artifact.Edge.Decision == SurfaceMatchDecision.Pass
                    ? SurfaceMatchDecision.Pass
                    : SurfaceMatchDecision.Fail;
            var expectedReason = ExpectedOverallReason(
                artifact.Surface,
                artifact.Edge);
            if (artifact.Decision != expectedDecision
                || artifact.Reason != expectedReason)
            {
                errors.Add("Surface/edge overall decision does not match its independent component decisions.");
            }
        }

        if (score is not null)
        {
            var scoreValidity = SurfaceEdgeArtifactValidator.Inspect(score);
            if (!scoreValidity.IsValid
                || artifact.ScoreContentSha256 != score.ContentSha256
                || artifact.SurfaceMatchExecutionContentSha256
                    != score.SurfaceMatchExecutionContentSha256
                || artifact.Surface is null
                || artifact.Edge is null
                || !Nearly(artifact.Surface.RawCoverageRatio, score.SurfaceScore.CoverageRatio)
                || !SameNullable(artifact.Surface.RawInlierRmse, score.SurfaceScore.InlierRmse)
                || !Nearly(artifact.Edge.RawCoverageRatio, score.EdgeScore.CoverageRatio)
                || !SameNullable(artifact.Edge.RawInlierRmse, score.EdgeScore.InlierRmse))
            {
                errors.Add("Surface/edge assessment is not linked to the exact raw score evidence.");
            }
        }

        var assessmentIdentityValid = false;
        try
        {
            assessmentIdentityValid = string.Equals(
                artifact.ContentSha256,
                SurfaceAndEdgeMatchAssessmentArtifact
                    .CalculateContentSha256(artifact),
                StringComparison.Ordinal);
        }
        catch
        {
            assessmentIdentityValid = false;
        }

        if (!assessmentIdentityValid)
        {
            errors.Add("Surface/edge assessment content identity is invalid.");
        }

        return new SurfaceAndEdgeAssessmentValidityReport(
            SurfaceAndEdgeAssessmentValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            policyIdentityValid,
            assessmentIdentityValid,
            errors,
            $"decision={artifact.Decision};reason={artifact.Reason};surface={artifact.Surface?.Decision}/{artifact.Surface?.RawCoverageRatio:G17};edge={artifact.Edge?.Decision}/{artifact.Edge?.RawCoverageRatio:G17};policyIdentity={policyIdentityValid};assessmentIdentity={assessmentIdentityValid}");
    }

    public static bool InspectPolicy(
        SurfaceAndEdgeMatchAcceptancePolicy? policy,
        out string evidence)
    {
        var errors = new List<string>();
        var valid = InspectPolicy(policy, errors);
        evidence = errors.Count == 0
            ? $"surface={policy!.Surface.MinimumCoverageRatio:G17}/{policy.Surface.MaximumInlierRmse:G17};edge={policy.Edge.MinimumCoverageRatio:G17}/{policy.Edge.MaximumInlierRmse:G17};identity={policy.ContentSha256}"
            : string.Join(" ", errors);
        return valid;
    }

    public static (
        SurfaceMatchDecision Decision,
        SurfaceAndEdgeComponentDecisionReason Reason) ExpectedComponent(
        double coverage,
        double? rmse,
        double minimumCoverage,
        double maximumRmse)
    {
        if (coverage < minimumCoverage)
        {
            return (
                SurfaceMatchDecision.Fail,
                SurfaceAndEdgeComponentDecisionReason.CoverageBelowMinimum);
        }

        if (rmse is null)
        {
            return (
                SurfaceMatchDecision.Fail,
                SurfaceAndEdgeComponentDecisionReason.InlierRmseUnavailable);
        }

        if (rmse.Value > maximumRmse)
        {
            return (
                SurfaceMatchDecision.Fail,
                SurfaceAndEdgeComponentDecisionReason.InlierRmseAboveMaximum);
        }

        return (
            SurfaceMatchDecision.Pass,
            SurfaceAndEdgeComponentDecisionReason.MeetsAuthoredLimits);
    }

    private static SurfaceAndEdgeDecisionReason ExpectedOverallReason(
        SurfaceAndEdgeComponentAssessment surface,
        SurfaceAndEdgeComponentAssessment edge)
    {
        if (surface.Decision != SurfaceMatchDecision.Pass)
        {
            return surface.Reason switch
            {
                SurfaceAndEdgeComponentDecisionReason.CoverageBelowMinimum =>
                    SurfaceAndEdgeDecisionReason.SurfaceCoverageBelowMinimum,
                SurfaceAndEdgeComponentDecisionReason.InlierRmseUnavailable =>
                    SurfaceAndEdgeDecisionReason.SurfaceInlierRmseUnavailable,
                _ => SurfaceAndEdgeDecisionReason.SurfaceInlierRmseAboveMaximum
            };
        }

        if (edge.Decision != SurfaceMatchDecision.Pass)
        {
            return edge.Reason switch
            {
                SurfaceAndEdgeComponentDecisionReason.CoverageBelowMinimum =>
                    SurfaceAndEdgeDecisionReason.EdgeCoverageBelowMinimum,
                SurfaceAndEdgeComponentDecisionReason.InlierRmseUnavailable =>
                    SurfaceAndEdgeDecisionReason.EdgeInlierRmseUnavailable,
                _ => SurfaceAndEdgeDecisionReason.EdgeInlierRmseAboveMaximum
            };
        }

        return SurfaceAndEdgeDecisionReason.BothComponentsMeetAuthoredLimits;
    }

    private static bool InspectPolicy(
        SurfaceAndEdgeMatchAcceptancePolicy? policy,
        List<string> errors)
    {
        if (policy is null
            || policy.SchemaVersion
                != SurfaceAndEdgeMatchAcceptancePolicy.CurrentSchemaVersion
            || policy.Semantics
                != SurfaceAndEdgeMatchAcceptancePolicy.CurrentSemantics
            || policy.Surface is null
            || policy.Edge is null
            || policy.Surface.SchemaVersion
                != SurfaceMatchAcceptancePolicy.CurrentSchemaVersion
            || policy.Surface.Semantics
                != SurfaceMatchAcceptancePolicy.CurrentSemantics
            || policy.Edge.SchemaVersion
                != SurfaceEdgeAcceptancePolicy.CurrentSchemaVersion
            || policy.Edge.Semantics
                != SurfaceEdgeAcceptancePolicy.CurrentSemantics)
        {
            errors.Add("Surface/edge acceptance policy is missing or unsupported.");
            return false;
        }

        var surfaceIdentityValid = string.Equals(
            policy.Surface.ContentSha256,
            SurfaceMatchAcceptancePolicy.CalculateContentSha256(policy.Surface),
            StringComparison.Ordinal);
        var edgeIdentityValid = string.Equals(
            policy.Edge.ContentSha256,
            SurfaceEdgeAcceptancePolicy.CalculateContentSha256(policy.Edge),
            StringComparison.Ordinal);
        var policyIdentityValid = surfaceIdentityValid
            && edgeIdentityValid
            && string.Equals(
                policy.ContentSha256,
                SurfaceAndEdgeMatchAcceptancePolicy
                    .CalculateContentSha256(policy),
                StringComparison.Ordinal);
        if (!policyIdentityValid)
        {
            errors.Add("Surface/edge acceptance policy identity is invalid.");
        }

        if (!FiniteLimit(
                policy.Surface.MinimumCoverageRatio,
                policy.Surface.MaximumInlierRmse)
            || !FiniteLimit(
                policy.Edge.MinimumCoverageRatio,
                policy.Edge.MaximumInlierRmse))
        {
            errors.Add("Surface/edge acceptance limits are invalid.");
        }

        return policyIdentityValid && errors.Count == 0;
    }

    private static void InspectComponent(
        SurfaceAndEdgeComponentAssessment component,
        double expectedMinimumCoverage,
        double expectedMaximumRmse,
        string name,
        List<string> errors)
    {
        if (!double.IsFinite(component.RawCoverageRatio)
            || component.RawCoverageRatio < 0.0
            || component.RawCoverageRatio > 1.0
            || component.RawInlierRmse is { } rmse
                && (!double.IsFinite(rmse) || rmse < 0.0)
            || !Nearly(component.MinimumCoverageRatio, expectedMinimumCoverage)
            || !Nearly(component.MaximumInlierRmse, expectedMaximumRmse))
        {
            errors.Add($"{name} component raw evidence or independent limits are invalid.");
            return;
        }

        var expected = ExpectedComponent(
            component.RawCoverageRatio,
            component.RawInlierRmse,
            component.MinimumCoverageRatio,
            component.MaximumInlierRmse);
        if (component.Decision != expected.Decision
            || component.Reason != expected.Reason)
        {
            errors.Add($"{name} component decision does not match its raw evidence and limit.");
        }
    }

    private static bool FiniteLimit(double coverage, double rmse) =>
        double.IsFinite(coverage)
        && coverage >= 0.0
        && coverage <= 1.0
        && double.IsFinite(rmse)
        && rmse >= 0.0;

    private static bool SameNullable(double? first, double? second) =>
        first.HasValue == second.HasValue
        && (!first.HasValue || Nearly(first.Value, second!.Value));

    private static bool Nearly(double first, double second) =>
        double.IsFinite(first)
        && double.IsFinite(second)
        && Math.Abs(first - second) <= 1e-12 * Math.Max(1.0, Math.Max(Math.Abs(first), Math.Abs(second)));

    private static bool IsCanonicalSha256(string? value) =>
        value?.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
}

public sealed record SurfaceMatchFalsePositiveReviewValidityReport(
    string SchemaVersion,
    bool IsValid,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";
}

public static class SurfaceMatchFalsePositiveReviewArtifactValidator
{
    public static SurfaceMatchFalsePositiveReviewValidityReport Inspect(
        SurfaceMatchFalsePositiveReviewArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        if (artifact.SchemaVersion
                != SurfaceMatchFalsePositiveReviewArtifact.CurrentSchemaVersion
            || artifact.Semantics
                != SurfaceMatchFalsePositiveReviewArtifact.CurrentSemantics)
        {
            errors.Add("Surface-match false-positive review schema or semantics are unsupported.");
        }

        if (!IsCanonicalSha256(artifact.ModelContentSha256)
            || artifact.ModelSampleCount <= 0
            || artifact.Accepted is null
            || artifact.Rejected is null)
        {
            errors.Add("Surface-match review requires an identified model and two cases.");
        }
        else
        {
            InspectCase(artifact.Accepted, SurfaceMatchReviewCaseRole.AcceptedReference, errors);
            InspectCase(artifact.Rejected, SurfaceMatchReviewCaseRole.RejectedCandidate, errors);
            if (artifact.Accepted.Decision != SurfaceMatchDecision.Pass
                || artifact.Rejected.Decision != SurfaceMatchDecision.Fail
                || artifact.Accepted.SurfaceCoverageRatio < 1.0
                || artifact.Rejected.SurfaceCoverageRatio < 1.0
                || artifact.Accepted.EdgeCoverageRatio <= artifact.Rejected.EdgeCoverageRatio
                || artifact.Accepted.SceneContentSha256 == artifact.Rejected.SceneContentSha256)
            {
                errors.Add("Surface-match review must retain a full-surface accepted reference and an edge-rejected false-positive candidate.");
            }
        }

        if (string.IsNullOrWhiteSpace(artifact.Evidence)
            || !artifact.Evidence.Contains("no weighted score", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Surface-match review must state the independent-score boundary.");
        }

        var identityValid = false;
        try
        {
            identityValid = string.Equals(
                artifact.ContentSha256,
                SurfaceMatchFalsePositiveReviewArtifact
                    .CalculateContentSha256(artifact),
                StringComparison.Ordinal);
        }
        catch
        {
            identityValid = false;
        }

        if (!identityValid)
        {
            errors.Add("Surface-match false-positive review identity is invalid.");
        }

        return new SurfaceMatchFalsePositiveReviewValidityReport(
            SurfaceMatchFalsePositiveReviewValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            identityValid,
            errors,
            $"modelSamples={artifact.ModelSampleCount};accepted={artifact.Accepted?.Decision}/{artifact.Accepted?.SurfaceCoverageRatio:G17}/{artifact.Accepted?.EdgeCoverageRatio:G17};rejected={artifact.Rejected?.Decision}/{artifact.Rejected?.SurfaceCoverageRatio:G17}/{artifact.Rejected?.EdgeCoverageRatio:G17};identity={identityValid}");
    }

    private static void InspectCase(
        SurfaceMatchFalsePositiveReviewCase item,
        SurfaceMatchReviewCaseRole role,
        List<string> errors)
    {
        if (item.Role != role
            || string.IsNullOrWhiteSpace(item.Label)
            || item.SceneSampleCount <= 0
            || !Enum.IsDefined(item.Decision)
            || !Enum.IsDefined(item.Reason)
            || !double.IsFinite(item.SurfaceCoverageRatio)
            || item.SurfaceCoverageRatio < 0.0
            || item.SurfaceCoverageRatio > 1.0
            || !double.IsFinite(item.EdgeCoverageRatio)
            || item.EdgeCoverageRatio < 0.0
            || item.EdgeCoverageRatio > 1.0
            || new[]
            {
                item.SceneContentSha256,
                item.SurfaceMatchExecutionContentSha256,
                item.PoseResultContentSha256,
                item.ScoreContentSha256,
                item.AssessmentContentSha256
            }.Any(value => !IsCanonicalSha256(value)))
        {
            errors.Add($"Surface-match review {role} case is invalid.");
        }
    }

    private static bool IsCanonicalSha256(string? value) =>
        value?.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
}
