using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum SurfaceAndEdgeComponentDecisionReason
{
    MeetsAuthoredLimits,
    CoverageBelowMinimum,
    InlierRmseUnavailable,
    InlierRmseAboveMaximum
}

public enum SurfaceAndEdgeDecisionReason
{
    BothComponentsMeetAuthoredLimits,
    SurfaceCoverageBelowMinimum,
    SurfaceInlierRmseUnavailable,
    SurfaceInlierRmseAboveMaximum,
    EdgeCoverageBelowMinimum,
    EdgeInlierRmseUnavailable,
    EdgeInlierRmseAboveMaximum
}

public sealed record SurfaceEdgeAcceptancePolicy(
    string SchemaVersion,
    string Semantics,
    double MinimumCoverageRatio,
    double MaximumInlierRmse,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "minimum-edge-coverage-and-maximum-edge-inlier-rmse-v1";

    public static SurfaceEdgeAcceptancePolicy Create(
        double minimumCoverageRatio,
        double maximumInlierRmse)
    {
        if (!double.IsFinite(minimumCoverageRatio)
            || minimumCoverageRatio < 0.0
            || minimumCoverageRatio > 1.0
            || !double.IsFinite(maximumInlierRmse)
            || maximumInlierRmse < 0.0)
        {
            throw new InvalidDataException(
                "Edge acceptance requires finite coverage in [0,1] and non-negative RMSE.");
        }

        var policy = new SurfaceEdgeAcceptancePolicy(
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
        SurfaceEdgeAcceptancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.SurfaceEdgeAcceptancePolicy");
            writer.Write(policy.SchemaVersion ?? string.Empty);
            writer.Write(policy.Semantics ?? string.Empty);
            writer.Write(policy.MinimumCoverageRatio);
            writer.Write(policy.MaximumInlierRmse);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public sealed record SurfaceAndEdgeMatchAcceptancePolicy(
    string SchemaVersion,
    string Semantics,
    SurfaceMatchAcceptancePolicy Surface,
    SurfaceEdgeAcceptancePolicy Edge,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "independent-surface-and-edge-acceptance-no-weighted-score-v1";

    public static SurfaceAndEdgeMatchAcceptancePolicy Create(
        SurfaceMatchAcceptancePolicy surface,
        SurfaceEdgeAcceptancePolicy edge)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(edge);
        var policy = new SurfaceAndEdgeMatchAcceptancePolicy(
            CurrentSchemaVersion,
            CurrentSemantics,
            surface,
            edge,
            string.Empty);
        return policy with
        {
            ContentSha256 = CalculateContentSha256(policy)
        };
    }

    public static string CalculateContentSha256(
        SurfaceAndEdgeMatchAcceptancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(policy.Surface);
        ArgumentNullException.ThrowIfNull(policy.Edge);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.SurfaceAndEdgeMatchAcceptancePolicy");
            writer.Write(policy.SchemaVersion ?? string.Empty);
            writer.Write(policy.Semantics ?? string.Empty);
            writer.Write(policy.Surface.ContentSha256 ?? string.Empty);
            writer.Write(policy.Edge.ContentSha256 ?? string.Empty);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public sealed record SurfaceAndEdgeComponentAssessment(
    double RawCoverageRatio,
    double? RawInlierRmse,
    double MinimumCoverageRatio,
    double MaximumInlierRmse,
    SurfaceMatchDecision Decision,
    SurfaceAndEdgeComponentDecisionReason Reason);

public sealed record SurfaceAndEdgeMatchAssessmentArtifact(
    string SchemaVersion,
    string Semantics,
    string SurfaceMatchExecutionContentSha256,
    string ScoreContentSha256,
    SurfaceAndEdgeMatchAcceptancePolicy Policy,
    SurfaceAndEdgeComponentAssessment Surface,
    SurfaceAndEdgeComponentAssessment Edge,
    SurfaceMatchDecision Decision,
    SurfaceAndEdgeDecisionReason Reason,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "independent-surface-and-edge-assessment-no-weighted-score-v1";

    public static string CalculateContentSha256(
        SurfaceAndEdgeMatchAssessmentArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.Policy);
        ArgumentNullException.ThrowIfNull(artifact.Surface);
        ArgumentNullException.ThrowIfNull(artifact.Edge);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.SurfaceAndEdgeMatchAssessmentArtifact");
            writer.Write(artifact.SchemaVersion ?? string.Empty);
            writer.Write(artifact.Semantics ?? string.Empty);
            writer.Write(artifact.SurfaceMatchExecutionContentSha256 ?? string.Empty);
            writer.Write(artifact.ScoreContentSha256 ?? string.Empty);
            writer.Write(artifact.Policy.ContentSha256 ?? string.Empty);
            WriteComponent(writer, artifact.Surface);
            WriteComponent(writer, artifact.Edge);
            writer.Write((int)artifact.Decision);
            writer.Write((int)artifact.Reason);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteComponent(
        BinaryWriter writer,
        SurfaceAndEdgeComponentAssessment component)
    {
        writer.Write(component.RawCoverageRatio);
        writer.Write(component.RawInlierRmse.HasValue);
        if (component.RawInlierRmse.HasValue)
        {
            writer.Write(component.RawInlierRmse.Value);
        }

        writer.Write(component.MinimumCoverageRatio);
        writer.Write(component.MaximumInlierRmse);
        writer.Write((int)component.Decision);
        writer.Write((int)component.Reason);
    }
}

public enum SurfaceMatchReviewCaseRole
{
    AcceptedReference,
    RejectedCandidate
}

public sealed record SurfaceMatchFalsePositiveReviewCase(
    SurfaceMatchReviewCaseRole Role,
    string Label,
    string SceneContentSha256,
    int SceneSampleCount,
    string SurfaceMatchExecutionContentSha256,
    string PoseResultContentSha256,
    string ScoreContentSha256,
    string AssessmentContentSha256,
    double SurfaceCoverageRatio,
    double EdgeCoverageRatio,
    SurfaceMatchDecision Decision,
    SurfaceAndEdgeDecisionReason Reason);

/// <summary>
/// Retained accepted/rejected comparison references. The identified source
/// model, original Prepared Scenes and samples, poses, scores, and assessments
/// remain separate immutable artifacts and are never copied or rewritten.
/// </summary>
public sealed record SurfaceMatchFalsePositiveReviewArtifact(
    string SchemaVersion,
    string Semantics,
    string ModelContentSha256,
    int ModelSampleCount,
    SurfaceMatchFalsePositiveReviewCase Accepted,
    SurfaceMatchFalsePositiveReviewCase Rejected,
    string Evidence,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "retained-surface-only-false-positive-accepted-rejected-review-v1";

    public static string CalculateContentSha256(
        SurfaceMatchFalsePositiveReviewArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.Accepted);
        ArgumentNullException.ThrowIfNull(artifact.Rejected);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.SurfaceMatchFalsePositiveReviewArtifact");
            writer.Write(artifact.SchemaVersion ?? string.Empty);
            writer.Write(artifact.Semantics ?? string.Empty);
            writer.Write(artifact.ModelContentSha256 ?? string.Empty);
            writer.Write(artifact.ModelSampleCount);
            WriteCase(writer, artifact.Accepted);
            WriteCase(writer, artifact.Rejected);
            writer.Write(artifact.Evidence ?? string.Empty);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCase(
        BinaryWriter writer,
        SurfaceMatchFalsePositiveReviewCase item)
    {
        writer.Write((int)item.Role);
        writer.Write(item.Label ?? string.Empty);
        writer.Write(item.SceneContentSha256 ?? string.Empty);
        writer.Write(item.SceneSampleCount);
        writer.Write(item.SurfaceMatchExecutionContentSha256 ?? string.Empty);
        writer.Write(item.PoseResultContentSha256 ?? string.Empty);
        writer.Write(item.ScoreContentSha256 ?? string.Empty);
        writer.Write(item.AssessmentContentSha256 ?? string.Empty);
        writer.Write(item.SurfaceCoverageRatio);
        writer.Write(item.EdgeCoverageRatio);
        writer.Write((int)item.Decision);
        writer.Write((int)item.Reason);
    }
}
