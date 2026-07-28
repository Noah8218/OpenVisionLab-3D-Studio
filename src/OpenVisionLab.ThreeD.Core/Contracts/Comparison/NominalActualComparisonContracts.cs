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

public sealed record NominalActualComparisonInput(
    string StepId,
    NominalActualFileIdentity ActualSource,
    NominalActualFileIdentity NominalSource,
    NominalActualFileIdentity QuerySource,
    string Unit,
    string FrameId,
    string AlignmentId,
    double LowerTolerance,
    double UpperTolerance)
{
    public const string Direction = "ActualToNominal";

    public string SourceFingerprint => ComputeFingerprint(string.Join(
        '\n',
        StepId,
        Direction,
        CanonicalIdentity(ActualSource),
        CanonicalIdentity(NominalSource),
        CanonicalIdentity(QuerySource),
        Unit,
        FrameId,
        AlignmentId));

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
    TimeSpan TotalElapsed)
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

    private static IReadOnlyList<Metric> CreateMetrics(NominalActualComparisonResult result) =>
    [
        new Metric("Signed mean deviation", MetricKind.Deviation, result.Signed.Mean, result.Input.Unit),
        new Metric("Signed standard deviation", MetricKind.Deviation, result.Signed.StandardDeviationPopulation, result.Input.Unit),
        new Metric("Signed RMS deviation", MetricKind.Deviation, result.Signed.RootMeanSquare, result.Input.Unit),
        new Metric("Signed minimum deviation", MetricKind.Deviation, result.Signed.Minimum, result.Input.Unit,
            result.Signed.Minimum < result.Input.LowerTolerance ? ResultStatus.Fail : ResultStatus.Pass),
        new Metric("Signed maximum deviation", MetricKind.Deviation, result.Signed.Maximum, result.Input.Unit,
            result.Signed.Maximum > result.Input.UpperTolerance ? ResultStatus.Fail : ResultStatus.Pass),
        new Metric("Unsigned mean deviation", MetricKind.Deviation, result.Unsigned.Mean, result.Input.Unit),
        new Metric("Unsigned standard deviation", MetricKind.Deviation, result.Unsigned.StandardDeviationPopulation, result.Input.Unit),
        new Metric("Unsigned RMS deviation", MetricKind.Deviation, result.Unsigned.RootMeanSquare, result.Input.Unit),
        new Metric("Compared point count", MetricKind.Count, result.ComparedPointCount, "count"),
        new Metric("Below lower tolerance count", MetricKind.Count, result.BelowLowerToleranceCount, "count",
            result.BelowLowerToleranceCount == 0 ? ResultStatus.Pass : ResultStatus.Fail),
        new Metric("Within tolerance count", MetricKind.Count, result.WithinToleranceCount, "count"),
        new Metric("Above upper tolerance count", MetricKind.Count, result.AboveUpperToleranceCount, "count",
            result.AboveUpperToleranceCount == 0 ? ResultStatus.Pass : ResultStatus.Fail),
        new Metric("Out-of-tolerance point count", MetricKind.Count, result.OutOfToleranceCount, "count", result.Status)
    ];

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
