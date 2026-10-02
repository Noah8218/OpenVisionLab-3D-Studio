using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public sealed record NominalActualFileIdentity(
    string Id,
    string Name,
    string Path,
    long ByteLength,
    string Sha256);

public sealed record NominalActualArtifactReference(
    string Path,
    string ContentSha256);

public sealed record NominalActualAlignmentReference(
    NominalActualArtifactReference Model,
    NominalActualArtifactReference Scene,
    NominalActualArtifactReference Execution,
    NominalActualArtifactReference Assessment);

/// <summary>
/// Read-only projection of the exact accepted Surface Match evidence used to
/// place nominal geometry in the measured scene frame. Nominal/Actual does not
/// recompute pose, coverage, or acceptance.
/// </summary>
public sealed record NominalActualAlignmentEvidence(
    string Semantics,
    string ModelArtifactId,
    string ModelContentSha256,
    string SceneArtifactId,
    string SceneContentSha256,
    string ExecutionContentSha256,
    string PoseResultContentSha256,
    string AssessmentContentSha256,
    string PolicyContentSha256,
    string CoverageSemantics,
    double CoverageRatio,
    double MinimumCoverageRatio,
    double InlierRmse,
    double MaximumInlierRmse,
    string Unit,
    string SourceFrameId,
    string TargetFrameId)
{
    public const string CurrentSemantics =
        "accepted-surface-match-model-to-scene-alignment-v1";

    public bool TryValidate(out string validationMessage)
    {
        var hashes = new[]
        {
            ModelContentSha256,
            SceneContentSha256,
            ExecutionContentSha256,
            PoseResultContentSha256,
            AssessmentContentSha256,
            PolicyContentSha256
        };
        var valid = Semantics == CurrentSemantics
            && !string.IsNullOrWhiteSpace(ModelArtifactId)
            && !string.IsNullOrWhiteSpace(SceneArtifactId)
            && hashes.All(IsCanonicalSha256)
            && CoverageSemantics == SurfaceCoverageEvaluation.CurrentSemantics
            && double.IsFinite(CoverageRatio)
            && CoverageRatio is >= 0.0 and <= 1.0
            && double.IsFinite(MinimumCoverageRatio)
            && MinimumCoverageRatio is >= 0.0 and <= 1.0
            && CoverageRatio >= MinimumCoverageRatio
            && double.IsFinite(InlierRmse)
            && InlierRmse >= 0.0
            && double.IsFinite(MaximumInlierRmse)
            && MaximumInlierRmse >= 0.0
            && InlierRmse <= MaximumInlierRmse
            && !string.IsNullOrWhiteSpace(Unit)
            && !string.IsNullOrWhiteSpace(SourceFrameId)
            && !string.IsNullOrWhiteSpace(TargetFrameId);
        validationMessage = valid
            ? "Nominal/Actual alignment evidence is valid."
            : "Nominal/Actual alignment evidence is missing an accepted identity, coverage policy, unit, or frame.";
        return valid;
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}

public sealed record NominalActualComparisonInput(
    string StepId,
    NominalActualFileIdentity ActualSource,
    NominalActualFileIdentity NominalSource,
    NominalActualFileIdentity QuerySource,
    string Unit,
    string FrameId,
    string AlignmentId,
    double LowerTolerance,
    double UpperTolerance,
    NominalActualAlignmentReference? Alignment = null)
{
    public const string Direction = "ActualToNominal";
    public const string IdentityAlignmentPrefix = "alignment.identity";

    public string SourceFingerprint => ComputeFingerprint(string.Join(
        '\n',
        StepId,
        Direction,
        CanonicalIdentity(ActualSource),
        CanonicalIdentity(NominalSource),
        CanonicalIdentity(QuerySource),
        Unit,
        FrameId,
        AlignmentId,
        CanonicalAlignment(Alignment)));

    public string ExecutionFingerprint => BuildExecutionFingerprint(
        SourceFingerprint,
        LowerTolerance,
        UpperTolerance);

    public static string BuildExecutionFingerprint(
        string sourceFingerprint,
        double lowerTolerance,
        double upperTolerance) =>
        ComputeFingerprint(string.Create(
            CultureInfo.InvariantCulture,
            $"{sourceFingerprint}\nlower={lowerTolerance:R}\nupper={upperTolerance:R}"));

    private static string CanonicalIdentity(NominalActualFileIdentity identity) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{identity.Id}|{identity.ByteLength}|{identity.Sha256.Trim().ToUpperInvariant()}");

    private static string CanonicalAlignment(NominalActualAlignmentReference? alignment) =>
        alignment is null
            ? "alignment-evidence=(identity)"
            : string.Join(
                '|',
                CanonicalArtifactReference(alignment.Model),
                CanonicalArtifactReference(alignment.Scene),
                CanonicalArtifactReference(alignment.Execution),
                CanonicalArtifactReference(alignment.Assessment));

    private static string CanonicalArtifactReference(NominalActualArtifactReference reference) =>
        $"{Path.GetFullPath(reference.Path)}|{reference.ContentSha256.Trim().ToUpperInvariant()}";

    public static bool IsIdentityAlignmentId(string? alignmentId) =>
        alignmentId?.StartsWith(IdentityAlignmentPrefix, StringComparison.Ordinal) == true;

    private static string ComputeFingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed record NominalActualComparisonProgress(
    string Stage,
    long ProcessedPointCount,
    long TotalPointCount,
    TimeSpan Elapsed);

public sealed record NominalActualDeviationStatistics(
    long Count,
    double Minimum,
    double Maximum,
    double Mean,
    double StandardDeviationPopulation,
    double RootMeanSquare);

public readonly record struct NominalActualDeviationSample(
    long QueryPointIndex,
    Vector3 Position,
    Vector3 ClosestNominalPoint,
    long NominalTriangleIndex,
    double UnsignedDeviation,
    double SignedDeviation,
    bool RobustSignRecovered);

public sealed record NominalActualComparisonResult(
    NominalActualComparisonInput Input,
    ResultStatus Status,
    string Message,
    long ComparedPointCount,
    NominalActualDeviationStatistics Unsigned,
    NominalActualDeviationStatistics Signed,
    long BelowLowerToleranceCount,
    long WithinToleranceCount,
    long AboveUpperToleranceCount,
    long DirectSignResolvedCount,
    long RobustSignRecoveredCount,
    int DisplaySampleStride,
    IReadOnlyList<NominalActualDeviationSample> DisplaySamples,
    TimeSpan IndexElapsed,
    TimeSpan CalculationElapsed,
    TimeSpan TotalElapsed,
    NominalActualAlignmentEvidence? AlignmentEvidence = null)
{
    public long OutOfToleranceCount =>
        BelowLowerToleranceCount + AboveUpperToleranceCount;
}

public static class NominalActualComparisonContract
{
    public const string ToolName = "Nominal / Actual Surface Deviation";
    public const string ResultEntityId = "result.nominal-actual-surface-deviation";
    public const string ResultEntityName = "Published Nominal / Actual Surface Deviation";
    public const string ResultLayerId = "layer.result.nominal-actual-surface-deviation";
    public const string ResultLayerName = "Published Nominal / Actual Surface Deviation";

    public static ToolResult CreateToolResult(NominalActualComparisonResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new ToolResult(
            ToolName,
            result.Status,
            result.Message,
            result.TotalElapsed,
            CreateMetrics(result),
            CreateOverlays(result));
    }

    public static ResultEntity CreateResultEntity(NominalActualComparisonResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new ResultEntity(
            ResultEntityId,
            ResultEntityName,
            result.Input.ActualSource.Id,
            result.Status,
            $"Published from Preview {result.Input.ExecutionFingerprint}; source geometry remains unchanged.",
            CreateMetrics(result),
            CreateOverlays(result));
    }

    public static InspectionStep CreateInspectionStep(NominalActualComparisonInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new InspectionStep(
            input.StepId,
            ToolName,
            input.ActualSource.Id,
            input.NominalSource.Id,
            true);
    }

    private static IReadOnlyList<Metric> CreateMetrics(NominalActualComparisonResult result)
    {
        var metrics = new List<Metric>
        {
            new("Signed mean deviation", MetricKind.Deviation, result.Signed.Mean, result.Input.Unit),
            new("Signed standard deviation", MetricKind.Deviation, result.Signed.StandardDeviationPopulation, result.Input.Unit),
            new("Signed RMS deviation", MetricKind.Deviation, result.Signed.RootMeanSquare, result.Input.Unit),
            new("Signed minimum deviation", MetricKind.Deviation, result.Signed.Minimum, result.Input.Unit,
                result.Signed.Minimum < result.Input.LowerTolerance ? ResultStatus.Fail : ResultStatus.Pass),
            new("Signed maximum deviation", MetricKind.Deviation, result.Signed.Maximum, result.Input.Unit,
                result.Signed.Maximum > result.Input.UpperTolerance ? ResultStatus.Fail : ResultStatus.Pass),
            new("Unsigned mean deviation", MetricKind.Deviation, result.Unsigned.Mean, result.Input.Unit),
            new("Unsigned standard deviation", MetricKind.Deviation, result.Unsigned.StandardDeviationPopulation, result.Input.Unit),
            new("Unsigned RMS deviation", MetricKind.Deviation, result.Unsigned.RootMeanSquare, result.Input.Unit),
            new("Compared point count", MetricKind.Count, result.ComparedPointCount, "count"),
            new("Below lower tolerance count", MetricKind.Count, result.BelowLowerToleranceCount, "count",
                result.BelowLowerToleranceCount == 0 ? ResultStatus.Pass : ResultStatus.Fail),
            new("Within tolerance count", MetricKind.Count, result.WithinToleranceCount, "count"),
            new("Above upper tolerance count", MetricKind.Count, result.AboveUpperToleranceCount, "count",
                result.AboveUpperToleranceCount == 0 ? ResultStatus.Pass : ResultStatus.Fail),
            new("Out-of-tolerance point count", MetricKind.Count, result.OutOfToleranceCount, "count", result.Status)
        };
        if (result.AlignmentEvidence is { } alignment)
        {
            metrics.Add(new Metric("Alignment spatial coverage", MetricKind.Number, alignment.CoverageRatio, "ratio", ResultStatus.Pass));
            metrics.Add(new Metric("Minimum alignment spatial coverage", MetricKind.Number, alignment.MinimumCoverageRatio, "ratio"));
            metrics.Add(new Metric("Alignment inlier RMSE", MetricKind.Deviation, alignment.InlierRmse, alignment.Unit, ResultStatus.Pass));
            metrics.Add(new Metric("Maximum alignment inlier RMSE", MetricKind.Deviation, alignment.MaximumInlierRmse, alignment.Unit));
        }

        return metrics;
    }

    private static IReadOnlyList<Overlay> CreateOverlays(NominalActualComparisonResult result) =>
    [
        new Overlay(
            "overlay.nominal-actual-signed-deviation",
            OverlayKind.ColorMap,
            "Signed actual-to-nominal deviation",
            result.Status,
            result.Input.ActualSource.Id)
    ];
}
