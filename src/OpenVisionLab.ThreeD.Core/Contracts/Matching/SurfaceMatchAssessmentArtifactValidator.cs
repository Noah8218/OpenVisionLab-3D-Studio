namespace OpenVisionLab.ThreeD.Core;

public sealed record SurfaceMatchAssessmentValidityReport(
    string SchemaVersion,
    bool IsValid,
    bool PolicyIdentityValid,
    bool AssessmentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";
}

public static class SurfaceMatchAssessmentArtifactValidator
{
    public static SurfaceMatchAssessmentValidityReport Inspect(
        SurfaceMatchAssessmentArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        if (artifact.SchemaVersion
                != SurfaceMatchAssessmentArtifact.CurrentSchemaVersion
            || artifact.Semantics
                != SurfaceMatchAssessmentArtifact.CurrentSemantics)
        {
            errors.Add(
                "Surface match assessment schema or semantics are unsupported.");
        }

        if (!IsCanonicalSha256(artifact.ExecutionContentSha256))
        {
            errors.Add(
                "Surface match assessment requires a canonical execution SHA-256.");
        }

        var policyIdentityValid = InspectPolicy(
            artifact.Policy,
            errors);
        if (!double.IsFinite(artifact.RawCoverageRatio)
            || artifact.RawCoverageRatio < 0.0
            || artifact.RawCoverageRatio > 1.0
            || artifact.RawInlierRmse is { } rmse
                && (!double.IsFinite(rmse) || rmse < 0.0))
        {
            errors.Add(
                "Surface match assessment raw coverage or RMSE is invalid.");
        }

        if (!Enum.IsDefined(artifact.RawPoseState)
            || !Enum.IsDefined(artifact.Decision)
            || !Enum.IsDefined(artifact.Reason))
        {
            errors.Add(
                "Surface match assessment contains an undefined state, decision, or reason.");
        }
        else if (artifact.Policy is not null)
        {
            var expected = ExpectedDecision(
                artifact.RawPoseState,
                artifact.RawCoverageRatio,
                artifact.RawInlierRmse,
                artifact.Policy);
            if (artifact.Decision != expected.Decision
                || artifact.Reason != expected.Reason)
            {
                errors.Add(
                    "Surface match assessment decision does not match its raw evidence and authored policy.");
            }

            if (artifact.RawPoseState
                    == RigidSurfacePoseSearchState.NoMatch
                && string.IsNullOrWhiteSpace(
                    artifact.RawSearchRejectionReason)
                || artifact.RawPoseState
                    == RigidSurfacePoseSearchState.Matched
                && !string.IsNullOrWhiteSpace(
                    artifact.RawSearchRejectionReason))
            {
                errors.Add(
                    "Surface match assessment search rejection evidence does not match the raw pose state.");
            }
        }

        var assessmentIdentityValid = false;
        try
        {
            assessmentIdentityValid = string.Equals(
                artifact.ContentSha256,
                SurfaceMatchAssessmentArtifact
                    .CalculateContentSha256(artifact),
                StringComparison.Ordinal);
        }
        catch
        {
            assessmentIdentityValid = false;
        }

        if (!assessmentIdentityValid)
        {
            errors.Add(
                "Surface match assessment content identity is invalid.");
        }

        var evidence =
            $"decision={artifact.Decision};reason={artifact.Reason};"
            + $"rawState={artifact.RawPoseState};"
            + $"coverage={artifact.RawCoverageRatio:G17};"
            + $"rmse={(artifact.RawInlierRmse?.ToString("G17") ?? "unavailable")};"
            + $"minimumCoverage={(artifact.Policy is null ? "unavailable" : artifact.Policy.MinimumCoverageRatio.ToString("G17"))};"
            + $"maximumRmse={(artifact.Policy is null ? "unavailable" : artifact.Policy.MaximumInlierRmse.ToString("G17"))};"
            + $"policyIdentity={policyIdentityValid};"
            + $"assessmentIdentity={assessmentIdentityValid}";
        return new SurfaceMatchAssessmentValidityReport(
            SurfaceMatchAssessmentValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            policyIdentityValid,
            assessmentIdentityValid,
            errors,
            evidence);
    }

    public static bool InspectRuntime(
        SurfaceMatchRuntimeReport report,
        out string evidence)
    {
        ArgumentNullException.ThrowIfNull(report);
        var expectedStages = new[]
        {
            SurfaceMatchRuntimeReport.PoseSearchStage,
            SurfaceMatchRuntimeReport.ExecutionArtifactStage,
            SurfaceMatchRuntimeReport.AcceptanceEvaluationStage
        };
        var valid = report.SchemaVersion
                == SurfaceMatchRuntimeReport.CurrentSchemaVersion
            && report.Clock == SurfaceMatchRuntimeReport.CurrentClock
            && IsCanonicalSha256(report.ExecutionContentSha256)
            && IsCanonicalSha256(report.AssessmentContentSha256)
            && report.Stages is not null
            && report.Stages.Length == expectedStages.Length
            && report.Stages.Select(stage => stage.StageId)
                .SequenceEqual(expectedStages)
            && report.Stages.All(stage => stage.ElapsedTicks >= 0)
            && report.TotalElapsedTicks >= 0
            && report.TotalElapsedTicks
                == report.Stages.Sum(stage => stage.ElapsedTicks)
            && report.ObservedAtUtc.Offset == TimeSpan.Zero;
        evidence = report.Stages is null
            ? "stages=missing"
            : $"stages={string.Join(',', report.Stages.Select(stage => $"{stage.StageId}:{stage.ElapsedTicks}"))};totalTicks={report.TotalElapsedTicks};clock={report.Clock}";
        return valid;
    }

    public static (
        SurfaceMatchDecision Decision,
        SurfaceMatchDecisionReason Reason) ExpectedDecision(
        RigidSurfacePoseSearchState rawState,
        double rawCoverageRatio,
        double? rawInlierRmse,
        SurfaceMatchAcceptancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (rawState == RigidSurfacePoseSearchState.NoMatch)
        {
            return (
                SurfaceMatchDecision.Rejected,
                SurfaceMatchDecisionReason.PoseSearchNoMatch);
        }

        if (rawInlierRmse is null)
        {
            return (
                SurfaceMatchDecision.Rejected,
                SurfaceMatchDecisionReason.InlierRmseUnavailable);
        }

        if (rawCoverageRatio < policy.MinimumCoverageRatio)
        {
            return (
                SurfaceMatchDecision.Fail,
                SurfaceMatchDecisionReason.CoverageBelowMinimum);
        }

        if (rawInlierRmse.Value > policy.MaximumInlierRmse)
        {
            return (
                SurfaceMatchDecision.Fail,
                SurfaceMatchDecisionReason.InlierRmseAboveMaximum);
        }

        return (
            SurfaceMatchDecision.Pass,
            SurfaceMatchDecisionReason.MeetsAuthoredLimits);
    }

    private static bool InspectPolicy(
        SurfaceMatchAcceptancePolicy? policy,
        List<string> errors)
    {
        if (policy is null
            || policy.SchemaVersion
                != SurfaceMatchAcceptancePolicy.CurrentSchemaVersion
            || policy.Semantics
                != SurfaceMatchAcceptancePolicy.CurrentSemantics
            || !double.IsFinite(policy.MinimumCoverageRatio)
            || policy.MinimumCoverageRatio < 0.0
            || policy.MinimumCoverageRatio > 1.0
            || !double.IsFinite(policy.MaximumInlierRmse)
            || policy.MaximumInlierRmse < 0.0)
        {
            errors.Add(
                "Surface match acceptance policy is missing or invalid.");
            return false;
        }

        var identityValid = string.Equals(
            policy.ContentSha256,
            SurfaceMatchAcceptancePolicy
                .CalculateContentSha256(policy),
            StringComparison.Ordinal);
        if (!identityValid)
        {
            errors.Add(
                "Surface match acceptance policy identity is invalid.");
        }

        return identityValid;
    }

    private static bool IsCanonicalSha256(string? value) =>
        value?.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
