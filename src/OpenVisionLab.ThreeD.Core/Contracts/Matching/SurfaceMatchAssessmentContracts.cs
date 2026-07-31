using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum SurfaceMatchDecision
{
    Pass,
    Fail,
    Rejected
}

public enum SurfaceMatchDecisionReason
{
    MeetsAuthoredLimits,
    PoseSearchNoMatch,
    CoverageBelowMinimum,
    InlierRmseUnavailable,
    InlierRmseAboveMaximum
}

/// <summary>
/// Recipe-owned acceptance limits applied after the decision-free surface
/// match execution. These values never alter pose search, raw coverage, or
/// the transformed-model overlay.
/// </summary>
public sealed record SurfaceMatchAcceptancePolicy(
    string SchemaVersion,
    string Semantics,
    double MinimumCoverageRatio,
    double MaximumInlierRmse,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "minimum-one-way-coverage-and-maximum-inlier-rmse-v1";

    public static SurfaceMatchAcceptancePolicy Create(
        double minimumCoverageRatio,
        double maximumInlierRmse)
    {
        if (!double.IsFinite(minimumCoverageRatio)
            || minimumCoverageRatio < 0.0
            || minimumCoverageRatio > 1.0)
        {
            throw new InvalidDataException(
                "Surface match minimum coverage ratio must be finite in [0,1].");
        }

        if (!double.IsFinite(maximumInlierRmse)
            || maximumInlierRmse < 0.0)
        {
            throw new InvalidDataException(
                "Surface match maximum inlier RMSE must be finite and non-negative.");
        }

        var policy = new SurfaceMatchAcceptancePolicy(
            CurrentSchemaVersion,
            CurrentSemantics,
            minimumCoverageRatio,
            maximumInlierRmse,
            string.Empty);
        return policy with
        {
            ContentSha256 = CalculateContentSha256(policy)
        };
    }

    public static string CalculateContentSha256(
        SurfaceMatchAcceptancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("OpenVisionLab.SurfaceMatchAcceptancePolicy");
            writer.Write(policy.SchemaVersion ?? string.Empty);
            writer.Write(policy.Semantics ?? string.Empty);
            writer.Write(policy.MinimumCoverageRatio);
            writer.Write(policy.MaximumInlierRmse);
        }

        return Convert.ToHexString(
            SHA256.HashData(stream.ToArray()));
    }
}

/// <summary>
/// Identified Pass/Fail/Rejected evidence linked to one immutable raw
/// execution. The raw score remains visible and unchanged; this artifact only
/// records how a separate authored policy interpreted it.
/// </summary>
public sealed record SurfaceMatchAssessmentArtifact(
    string SchemaVersion,
    string Semantics,
    string ExecutionContentSha256,
    SurfaceMatchAcceptancePolicy Policy,
    RigidSurfacePoseSearchState RawPoseState,
    double RawCoverageRatio,
    double? RawInlierRmse,
    SurfaceMatchDecision Decision,
    SurfaceMatchDecisionReason Reason,
    string RawSearchRejectionReason,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "separate-authored-policy-over-identified-raw-execution-v1";

    public static SurfaceMatchAssessmentArtifact Create(
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchAcceptancePolicy policy,
        SurfaceMatchDecision decision,
        SurfaceMatchDecisionReason reason)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(policy);
        var executionValidity =
            SurfaceMatchExecutionArtifactValidator.Inspect(
                execution);
        if (!executionValidity.IsValid)
        {
            throw new InvalidDataException(
                "Surface match assessment requires a valid identified raw execution.");
        }

        var artifact = new SurfaceMatchAssessmentArtifact(
            CurrentSchemaVersion,
            CurrentSemantics,
            execution.ContentSha256,
            policy,
            execution.PoseResult.State,
            execution.PoseResult.Coverage.CoverageRatio,
            execution.PoseResult.Coverage.InlierRmse,
            decision,
            reason,
            execution.PoseResult.RejectionReason?.Trim()
                ?? string.Empty,
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };
        var validity =
            SurfaceMatchAssessmentArtifactValidator.Inspect(
                artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface match assessment validation failed: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        SurfaceMatchAssessmentArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.Policy);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("OpenVisionLab.SurfaceMatchAssessmentArtifact");
            writer.Write(artifact.SchemaVersion ?? string.Empty);
            writer.Write(artifact.Semantics ?? string.Empty);
            writer.Write(artifact.ExecutionContentSha256 ?? string.Empty);
            writer.Write(artifact.Policy.ContentSha256 ?? string.Empty);
            writer.Write((int)artifact.RawPoseState);
            writer.Write(artifact.RawCoverageRatio);
            writer.Write(artifact.RawInlierRmse.HasValue);
            if (artifact.RawInlierRmse.HasValue)
            {
                writer.Write(artifact.RawInlierRmse.Value);
            }

            writer.Write((int)artifact.Decision);
            writer.Write((int)artifact.Reason);
            writer.Write(artifact.RawSearchRejectionReason ?? string.Empty);
        }

        return Convert.ToHexString(
            SHA256.HashData(stream.ToArray()));
    }
}

public sealed record SurfaceMatchRuntimeStage(
    string StageId,
    long ElapsedTicks);

/// <summary>
/// Observational timing for one execution. Wall-clock measurements are kept
/// outside every deterministic identity and acceptance decision.
/// </summary>
public sealed record SurfaceMatchRuntimeReport(
    string SchemaVersion,
    string Clock,
    string ExecutionContentSha256,
    string AssessmentContentSha256,
    SurfaceMatchRuntimeStage[] Stages,
    long TotalElapsedTicks,
    DateTimeOffset ObservedAtUtc)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentClock = "System.Diagnostics.Stopwatch";
    public const string PoseSearchStage = "pose-search";
    public const string ExecutionArtifactStage = "execution-artifact";
    public const string AcceptanceEvaluationStage =
        "acceptance-evaluation";

    public double TotalMilliseconds =>
        TimeSpan.FromTicks(TotalElapsedTicks).TotalMilliseconds;
}
