using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Deterministic software-quality state for a reusable Level Frame. These
/// states are acceptance guidance over recorded evidence, not calibrated
/// measurement confidence or production approval.
/// </summary>
public enum C3DLevelFrameQualityState
{
    Accepted,
    Review,
    Rejected
}

public enum C3DLevelFrameQualityReason
{
    MeetsPolicy,
    ReferenceCoverageBelowMinimum,
    ReferenceResidualAboveMaximum
}

/// <summary>
/// Explicit policy applied to existing Level Surface evidence. Complete
/// reference coverage is intentionally a fixed software policy for F-11;
/// future authored alternatives require a separate recipe contract.
/// </summary>
public sealed record C3DLevelFrameQualityPolicy(
    double MinimumReferenceCoverageRatio,
    double MaximumReferenceRmsResidual)
{
    public const string PolicyId = "CompleteReferenceCoverageAndMaximumReferenceRms";

    public static C3DLevelFrameQualityPolicy CompleteCoverage(
        double maximumReferenceRmsResidual) =>
        new(1.0, maximumReferenceRmsResidual);

    public void Validate()
    {
        if (!double.IsFinite(MinimumReferenceCoverageRatio)
            || MinimumReferenceCoverageRatio is < 0.0 or > 1.0
            || !double.IsFinite(MaximumReferenceRmsResidual)
            || MaximumReferenceRmsResidual <= 0.0)
        {
            throw new InvalidDataException(
                "Level Frame quality policy requires a coverage ratio between zero and one and a positive finite RMS limit.");
        }
    }
}

public sealed record C3DLevelFrameReferenceCoverage(
    string SelectionId,
    int Row,
    int Column,
    int RowCount,
    int ColumnCount,
    long DeclaredCellCount,
    int ValidSampleCount,
    double CoverageRatio);

/// <summary>
/// Immutable, source-bound alignment-quality evidence for one Level Frame.
/// Coverage is calculated independently for each authored rectangle; if
/// rectangles overlap, a source cell is counted once per rectangle rather
/// than silently converted into a unique-union metric.
/// </summary>
public sealed class C3DLevelFrameQualityEvidence
{
    public const string ContractVersion = "1.0";
    public const string CoverageSemantics =
        "PerReferenceRectangleValidFiniteSamplesOverDeclaredCells;OverlapCountedPerRegion";
    public const string ConfidenceSemantics =
        "SoftwarePolicyStateOnly;NotCalibratedMeasurementConfidence";

    private C3DLevelFrameQualityEvidence(
        string qualityEvidenceId,
        string levelFrameId,
        string levelFrameContentSha256,
        string levelingTransformEntityId,
        string levelingTransformContentSha256,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        int referenceSampleCount,
        double referenceResidualRms,
        double referenceResidualPeakToValley,
        C3DLevelFrameQualityPolicy policy,
        C3DLevelFrameReferenceCoverage[] referenceCoverage,
        double minimumObservedCoverageRatio,
        C3DLevelFrameQualityState state,
        C3DLevelFrameQualityReason reason,
        string provenance,
        string contentSha256)
    {
        QualityEvidenceId = qualityEvidenceId;
        LevelFrameId = levelFrameId;
        LevelFrameContentSha256 = levelFrameContentSha256;
        LevelingTransformEntityId = levelingTransformEntityId;
        LevelingTransformContentSha256 = levelingTransformContentSha256;
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        SourceUnit = sourceUnit;
        SourceFrameId = sourceFrameId;
        ReferenceSampleCount = referenceSampleCount;
        ReferenceResidualRms = referenceResidualRms;
        ReferenceResidualPeakToValley = referenceResidualPeakToValley;
        Policy = policy;
        this.referenceCoverage = referenceCoverage;
        MinimumObservedCoverageRatio = minimumObservedCoverageRatio;
        State = state;
        Reason = reason;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    private readonly C3DLevelFrameReferenceCoverage[] referenceCoverage;

    public string QualityEvidenceId { get; }
    public string LevelFrameId { get; }
    public string LevelFrameContentSha256 { get; }
    public string LevelingTransformEntityId { get; }
    public string LevelingTransformContentSha256 { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string SourceUnit { get; }
    public string SourceFrameId { get; }
    public int ReferenceSampleCount { get; }
    public double ReferenceResidualRms { get; }
    public double ReferenceResidualPeakToValley { get; }
    public C3DLevelFrameQualityPolicy Policy { get; }
    public IReadOnlyList<C3DLevelFrameReferenceCoverage> ReferenceCoverage => referenceCoverage;
    public double MinimumObservedCoverageRatio { get; }
    public C3DLevelFrameQualityState State { get; }
    public C3DLevelFrameQualityReason Reason { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public bool IsAccepted => State == C3DLevelFrameQualityState.Accepted;

    public static C3DLevelFrameQualityEvidence Create(
        C3DLevelFrameArtifact levelFrame,
        C3DLevelingTransform levelingTransform,
        C3DLevelFrameQualityPolicy policy,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(levelFrame);
        ArgumentNullException.ThrowIfNull(levelingTransform);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        policy.Validate();

        if (!string.Equals(
                levelFrame.LevelingTransformEntityId,
                levelingTransform.OutputEntityId,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                levelFrame.LevelingTransformContentSha256,
                levelingTransform.ContentSha256,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                levelFrame.RootSourceEntityId,
                levelingTransform.RootSourceEntityId,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                levelFrame.RootSourceSha256,
                levelingTransform.RootSourceSha256,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(levelFrame.SourceUnit, levelingTransform.SourceUnit, StringComparison.Ordinal)
            || !string.Equals(levelFrame.SourceFrameId, levelingTransform.SourceFrameId, StringComparison.Ordinal)
            || levelFrame.ReferenceRegions.Count != levelingTransform.ReferenceRegions.Count
            || !levelFrame.ReferenceRegions.SequenceEqual(levelingTransform.ReferenceRegions))
        {
            throw new InvalidDataException(
                "Level Frame quality evidence requires the exact linked LevelingTransform and reference-region identities.");
        }

        var coverage = levelingTransform.ReferenceRegions
            .Select(CreateCoverage)
            .ToArray();
        if (coverage.Length == 0)
        {
            throw new InvalidDataException(
                "Level Frame quality evidence requires at least one reference region.");
        }

        var minimumObservedCoverageRatio = coverage.Min(item => item.CoverageRatio);
        var residualWithinPolicy = levelingTransform.ReferenceResidualRms
            <= policy.MaximumReferenceRmsResidual;
        var coverageWithinPolicy = minimumObservedCoverageRatio
            >= policy.MinimumReferenceCoverageRatio;
        var (state, reason) = !residualWithinPolicy
            ? (C3DLevelFrameQualityState.Rejected, C3DLevelFrameQualityReason.ReferenceResidualAboveMaximum)
            : !coverageWithinPolicy
                ? (C3DLevelFrameQualityState.Review, C3DLevelFrameQualityReason.ReferenceCoverageBelowMinimum)
                : (C3DLevelFrameQualityState.Accepted, C3DLevelFrameQualityReason.MeetsPolicy);
        var qualityEvidenceId = $"{levelFrame.OutputEntityId}.quality";
        var hash = CalculateContentSha256(
            qualityEvidenceId,
            levelFrame,
            levelingTransform,
            policy,
            coverage,
            minimumObservedCoverageRatio,
            state,
            reason);
        return new C3DLevelFrameQualityEvidence(
            qualityEvidenceId,
            levelFrame.LevelFrameId,
            levelFrame.ContentSha256.ToUpperInvariant(),
            levelingTransform.OutputEntityId,
            levelingTransform.ContentSha256.ToUpperInvariant(),
            levelingTransform.RootSourceEntityId,
            levelingTransform.RootSourceSha256.ToUpperInvariant(),
            levelingTransform.SourceUnit,
            levelingTransform.SourceFrameId,
            levelingTransform.ReferenceSampleCount,
            levelingTransform.ReferenceResidualRms,
            levelingTransform.ReferenceResidualPeakToValley,
            policy,
            coverage,
            minimumObservedCoverageRatio,
            state,
            reason,
            provenance,
            hash);
    }

    public static string CalculateContentSha256(
        string qualityEvidenceId,
        C3DLevelFrameArtifact levelFrame,
        C3DLevelingTransform levelingTransform,
        C3DLevelFrameQualityPolicy policy,
        IReadOnlyList<C3DLevelFrameReferenceCoverage> coverage,
        double minimumObservedCoverageRatio,
        C3DLevelFrameQualityState state,
        C3DLevelFrameQualityReason reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qualityEvidenceId);
        ArgumentNullException.ThrowIfNull(levelFrame);
        ArgumentNullException.ThrowIfNull(levelingTransform);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(coverage);
        policy.Validate();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DLevelFrameQualityEvidence");
        writer.Write(ContractVersion);
        writer.Write(CoverageSemantics);
        writer.Write(ConfidenceSemantics);
        writer.Write(C3DLevelFrameQualityPolicy.PolicyId);
        writer.Write(qualityEvidenceId);
        writer.Write(levelFrame.LevelFrameId);
        writer.Write(levelFrame.ContentSha256.ToUpperInvariant());
        writer.Write(levelingTransform.OutputEntityId);
        writer.Write(levelingTransform.ContentSha256.ToUpperInvariant());
        writer.Write(levelingTransform.RootSourceEntityId);
        writer.Write(levelingTransform.RootSourceSha256.ToUpperInvariant());
        writer.Write(levelingTransform.SourceUnit);
        writer.Write(levelingTransform.SourceFrameId);
        writer.Write(levelingTransform.ReferenceSampleCount);
        writer.Write(levelingTransform.ReferenceResidualRms);
        writer.Write(levelingTransform.ReferenceResidualPeakToValley);
        writer.Write(policy.MinimumReferenceCoverageRatio);
        writer.Write(policy.MaximumReferenceRmsResidual);
        writer.Write(coverage.Count);
        foreach (var item in coverage)
        {
            ArgumentNullException.ThrowIfNull(item);
            writer.Write(item.SelectionId);
            writer.Write(item.Row);
            writer.Write(item.Column);
            writer.Write(item.RowCount);
            writer.Write(item.ColumnCount);
            writer.Write(item.DeclaredCellCount);
            writer.Write(item.ValidSampleCount);
            writer.Write(item.CoverageRatio);
        }

        writer.Write(minimumObservedCoverageRatio);
        writer.Write((int)state);
        writer.Write((int)reason);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static C3DLevelFrameReferenceCoverage CreateCoverage(
        C3DLevelingReferenceRegion region)
    {
        var declaredCellCount = checked((long)region.RowCount * region.ColumnCount);
        if (declaredCellCount <= 0
            || region.ValidSampleCount < 0
            || region.ValidSampleCount > declaredCellCount)
        {
            throw new InvalidDataException(
                $"Level Frame reference region '{region.SelectionId}' has invalid declared or finite sample counts.");
        }

        return new C3DLevelFrameReferenceCoverage(
            region.SelectionId,
            region.Row,
            region.Column,
            region.RowCount,
            region.ColumnCount,
            declaredCellCount,
            region.ValidSampleCount,
            region.ValidSampleCount / (double)declaredCellCount);
    }
}
