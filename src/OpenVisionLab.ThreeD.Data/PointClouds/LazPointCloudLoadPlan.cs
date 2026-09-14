using System.Globalization;
using System.Runtime.InteropServices;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Metadata-only capacity information for a LAS/LAZ load request. The
/// estimates describe logical decoded records and the managed sampled-point
/// payload; they do not predict decoder, native, GPU, or process overhead.
/// </summary>
public sealed record LazPointCloudLoadPlan(
    string SourcePath,
    long SourceFileBytes,
    string LasVersion,
    byte PointDataFormat,
    bool IsCompressed,
    ushort PointDataRecordLength,
    ulong PointCount,
    int RequestedSampleLimit,
    ulong SampledPointCount,
    int SampledPointSizeBytes,
    ulong EstimatedDecodedRecordBytes,
    ulong EstimatedManagedSampleBytes,
    bool EstimatesSaturated,
    bool FullPointStreamDecodeRequired)
{
    public const string EstimateScope = "logical managed estimates; decoder/native/GPU/process overhead excluded";

    public static LazPointCloudLoadPlan Create(
        LazPointCloudMetadata metadata,
        int maxSampledPoints)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.SourcePath);
        if (maxSampledPoints < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSampledPoints),
                "Sample point limit must be positive.");
        }

        var fullPath = Path.GetFullPath(metadata.SourcePath);
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("The LAS/LAZ source does not exist.", fullPath);
        }

        var sampledPointCount = LazPointCloudSampling.GetSampleCount(
            metadata.PointCount,
            maxSampledPoints);
        var sampledPointSizeBytes = Marshal.SizeOf<LazPointCloudPoint>();
        var (decodedRecordBytes, decodedSaturated) = MultiplySaturating(
            metadata.PointCount,
            metadata.PointDataRecordLength);
        var (managedSampleBytes, sampleSaturated) = MultiplySaturating(
            sampledPointCount,
            (ulong)sampledPointSizeBytes);

        return new(
            fullPath,
            fileInfo.Length,
            metadata.Version,
            metadata.PointDataFormat,
            metadata.IsCompressed,
            metadata.PointDataRecordLength,
            metadata.PointCount,
            maxSampledPoints,
            sampledPointCount,
            sampledPointSizeBytes,
            decodedRecordBytes,
            managedSampleBytes,
            decodedSaturated || sampleSaturated,
            FullPointStreamDecodeRequired: true);
    }

    public string FormatContractLine() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"LAZ-LOAD-PLAN|source={SourcePath}|lasVersion={LasVersion}|compressed={IsCompressed}|pointFormat={PointDataFormat}|pointDataRecordLength={PointDataRecordLength}|pointCount={PointCount}|requestedSampleLimit={RequestedSampleLimit}|sampledPointCount={SampledPointCount}|sourceFileBytes={SourceFileBytes}|sampledPointSizeBytes={SampledPointSizeBytes}|estimatedDecodedRecordBytes={EstimatedDecodedRecordBytes}|estimatedManagedSampleBytes={EstimatedManagedSampleBytes}|estimatesSaturated={EstimatesSaturated}|fullPointStreamDecodeRequired={FullPointStreamDecodeRequired}");

    public string FormatSummary() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Path.GetFileName(SourcePath)} | points {PointCount:N0} | requested sample limit {RequestedSampleLimit:N0} | sampled {SampledPointCount:N0} | source {SourceFileBytes:N0} bytes | logical decoded records {EstimatedDecodedRecordBytes:N0} bytes | managed sampled payload {EstimatedManagedSampleBytes:N0} bytes | full stream decode {FullPointStreamDecodeRequired}");

    private static (ulong Value, bool Saturated) MultiplySaturating(ulong left, ulong right)
    {
        if (left == 0 || right == 0)
        {
            return (0, false);
        }

        if (left > ulong.MaxValue / right)
        {
            return (ulong.MaxValue, true);
        }

        return (left * right, false);
    }
}
