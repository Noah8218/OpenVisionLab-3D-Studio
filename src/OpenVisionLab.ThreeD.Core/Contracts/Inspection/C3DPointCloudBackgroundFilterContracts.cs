using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DPointCloudBackgroundFilterMode
{
    RemoveAtOrBelowDistance
}

/// <summary>
/// Immutable evidence for one nearest-background-distance point-cloud
/// projection. The current and saved-background identities are preserved, and
/// the output remains a separate derived point cloud.
/// </summary>
public sealed class C3DPointCloudBackgroundFilterEvidence
{
    public const string ContractVersion = "1.0";
    public const string DistancePolicyName = "NearestEuclideanXYZ";
    public const string RemovalPolicyName = "RemoveAtOrBelowMaximumDistance";
    public const string LineagePolicyName = "SeparateCurrentDerivedPointCloud";
    public const string MatchingPolicyName = "ExactUnitFrameCoordinateConvention";

    private C3DPointCloudBackgroundFilterEvidence(
        string stepId,
        string currentSourceEntityId,
        string currentSourceContentSha256,
        string currentRootSourceSha256,
        long currentByteLength,
        string backgroundEntityId,
        string backgroundContentSha256,
        string backgroundRootSourceSha256,
        long backgroundByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        long outputByteLength,
        string unit,
        string frameId,
        string coordinateConvention,
        double maximumBackgroundDistance,
        C3DPointCloudBackgroundFilterMode mode,
        int inputPointCount,
        int backgroundPointCount,
        int retainedPointCount,
        int removedPointCount,
        double minimumNearestBackgroundDistance,
        double maximumNearestBackgroundDistance,
        double meanNearestBackgroundDistance,
        string provenance,
        string contentSha256)
    {
        StepId = stepId;
        CurrentSourceEntityId = currentSourceEntityId;
        CurrentSourceContentSha256 = currentSourceContentSha256;
        CurrentRootSourceSha256 = currentRootSourceSha256;
        CurrentByteLength = currentByteLength;
        BackgroundEntityId = backgroundEntityId;
        BackgroundContentSha256 = backgroundContentSha256;
        BackgroundRootSourceSha256 = backgroundRootSourceSha256;
        BackgroundByteLength = backgroundByteLength;
        OutputEntityId = outputEntityId;
        OutputContentSha256 = outputContentSha256;
        OutputRootSourceSha256 = outputRootSourceSha256;
        OutputByteLength = outputByteLength;
        Unit = unit;
        FrameId = frameId;
        CoordinateConvention = coordinateConvention;
        MaximumBackgroundDistance = maximumBackgroundDistance;
        Mode = mode;
        InputPointCount = inputPointCount;
        BackgroundPointCount = backgroundPointCount;
        RetainedPointCount = retainedPointCount;
        RemovedPointCount = removedPointCount;
        MinimumNearestBackgroundDistance = minimumNearestBackgroundDistance;
        MaximumNearestBackgroundDistance = maximumNearestBackgroundDistance;
        MeanNearestBackgroundDistance = meanNearestBackgroundDistance;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string StepId { get; }
    public string CurrentSourceEntityId { get; }
    public string CurrentSourceContentSha256 { get; }
    public string CurrentRootSourceSha256 { get; }
    public long CurrentByteLength { get; }
    public string BackgroundEntityId { get; }
    public string BackgroundContentSha256 { get; }
    public string BackgroundRootSourceSha256 { get; }
    public long BackgroundByteLength { get; }
    public string OutputEntityId { get; }
    public string OutputContentSha256 { get; }
    public string OutputRootSourceSha256 { get; }
    public long OutputByteLength { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string CoordinateConvention { get; }
    public double MaximumBackgroundDistance { get; }
    public C3DPointCloudBackgroundFilterMode Mode { get; }
    public string DistancePolicy => DistancePolicyName;
    public string RemovalPolicy => RemovalPolicyName;
    public string LineagePolicy => LineagePolicyName;
    public string MatchingPolicy => MatchingPolicyName;
    public int InputPointCount { get; }
    public int BackgroundPointCount { get; }
    public int RetainedPointCount { get; }
    public int RemovedPointCount { get; }
    public int OutputPointCount => RetainedPointCount;
    public bool HasRetainedPoints => RetainedPointCount > 0;
    public double MinimumNearestBackgroundDistance { get; }
    public double MaximumNearestBackgroundDistance { get; }
    public double MeanNearestBackgroundDistance { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DPointCloudBackgroundFilterEvidence Create(
        string stepId,
        string currentSourceEntityId,
        string currentSourceContentSha256,
        string currentRootSourceSha256,
        long currentByteLength,
        string backgroundEntityId,
        string backgroundContentSha256,
        string backgroundRootSourceSha256,
        long backgroundByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        long outputByteLength,
        string unit,
        string frameId,
        string coordinateConvention,
        double maximumBackgroundDistance,
        C3DPointCloudBackgroundFilterMode mode,
        int inputPointCount,
        int backgroundPointCount,
        int retainedPointCount,
        int removedPointCount,
        double minimumNearestBackgroundDistance,
        double maximumNearestBackgroundDistance,
        double meanNearestBackgroundDistance,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinateConvention);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!IsSha256(currentSourceContentSha256)
            || !IsSha256(currentRootSourceSha256)
            || !IsSha256(backgroundContentSha256)
            || !IsSha256(backgroundRootSourceSha256)
            || !IsSha256(outputContentSha256)
            || !IsSha256(outputRootSourceSha256))
        {
            throw new ArgumentException("Point-cloud background-filter evidence requires SHA-256 identities.");
        }

        if (string.Equals(currentSourceEntityId, backgroundEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputEntityId, currentSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputEntityId, backgroundEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Point-cloud background-filter identities must be distinct.");
        }

        var statistics = new[]
        {
            maximumBackgroundDistance,
            minimumNearestBackgroundDistance,
            maximumNearestBackgroundDistance,
            meanNearestBackgroundDistance
        };
        if (currentByteLength <= 0
            || backgroundByteLength <= 0
            || outputByteLength <= 0
            || !double.IsFinite(maximumBackgroundDistance)
            || maximumBackgroundDistance < 0d
            || statistics.Any(value => !double.IsFinite(value) || value < 0d)
            || minimumNearestBackgroundDistance > maximumNearestBackgroundDistance
            || meanNearestBackgroundDistance < minimumNearestBackgroundDistance
            || meanNearestBackgroundDistance > maximumNearestBackgroundDistance
            || !Enum.IsDefined(mode)
            || inputPointCount <= 0
            || backgroundPointCount <= 0
            || retainedPointCount < 0
            || removedPointCount < 0
            || retainedPointCount + removedPointCount != inputPointCount)
        {
            throw new ArgumentException("Point-cloud background-filter evidence contains an invalid identity, threshold, statistic, mode, or count.");
        }

        var contentSha256 = CalculateContentSha256(
            stepId,
            currentSourceEntityId,
            currentSourceContentSha256,
            currentRootSourceSha256,
            currentByteLength,
            backgroundEntityId,
            backgroundContentSha256,
            backgroundRootSourceSha256,
            backgroundByteLength,
            outputEntityId,
            outputContentSha256,
            outputRootSourceSha256,
            outputByteLength,
            unit,
            frameId,
            coordinateConvention,
            maximumBackgroundDistance,
            mode,
            inputPointCount,
            backgroundPointCount,
            retainedPointCount,
            removedPointCount,
            minimumNearestBackgroundDistance,
            maximumNearestBackgroundDistance,
            meanNearestBackgroundDistance);
        return new C3DPointCloudBackgroundFilterEvidence(
            stepId,
            currentSourceEntityId,
            currentSourceContentSha256.ToUpperInvariant(),
            currentRootSourceSha256.ToUpperInvariant(),
            currentByteLength,
            backgroundEntityId,
            backgroundContentSha256.ToUpperInvariant(),
            backgroundRootSourceSha256.ToUpperInvariant(),
            backgroundByteLength,
            outputEntityId,
            outputContentSha256.ToUpperInvariant(),
            outputRootSourceSha256.ToUpperInvariant(),
            outputByteLength,
            unit,
            frameId,
            coordinateConvention,
            maximumBackgroundDistance,
            mode,
            inputPointCount,
            backgroundPointCount,
            retainedPointCount,
            removedPointCount,
            minimumNearestBackgroundDistance,
            maximumNearestBackgroundDistance,
            meanNearestBackgroundDistance,
            provenance,
            contentSha256);
    }

    private static string CalculateContentSha256(
        string stepId,
        string currentSourceEntityId,
        string currentSourceContentSha256,
        string currentRootSourceSha256,
        long currentByteLength,
        string backgroundEntityId,
        string backgroundContentSha256,
        string backgroundRootSourceSha256,
        long backgroundByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        long outputByteLength,
        string unit,
        string frameId,
        string coordinateConvention,
        double maximumBackgroundDistance,
        C3DPointCloudBackgroundFilterMode mode,
        int inputPointCount,
        int backgroundPointCount,
        int retainedPointCount,
        int removedPointCount,
        double minimumNearestBackgroundDistance,
        double maximumNearestBackgroundDistance,
        double meanNearestBackgroundDistance)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DPointCloudBackgroundFilterEvidence");
        writer.Write(ContractVersion);
        writer.Write(stepId);
        writer.Write(currentSourceEntityId);
        writer.Write(currentSourceContentSha256.ToUpperInvariant());
        writer.Write(currentRootSourceSha256.ToUpperInvariant());
        writer.Write(currentByteLength);
        writer.Write(backgroundEntityId);
        writer.Write(backgroundContentSha256.ToUpperInvariant());
        writer.Write(backgroundRootSourceSha256.ToUpperInvariant());
        writer.Write(backgroundByteLength);
        writer.Write(outputEntityId);
        writer.Write(outputContentSha256.ToUpperInvariant());
        writer.Write(outputRootSourceSha256.ToUpperInvariant());
        writer.Write(outputByteLength);
        writer.Write(unit);
        writer.Write(frameId);
        writer.Write(coordinateConvention);
        writer.Write(maximumBackgroundDistance);
        writer.Write((int)mode);
        writer.Write(DistancePolicyName);
        writer.Write(RemovalPolicyName);
        writer.Write(LineagePolicyName);
        writer.Write(MatchingPolicyName);
        writer.Write(inputPointCount);
        writer.Write(backgroundPointCount);
        writer.Write(retainedPointCount);
        writer.Write(removedPointCount);
        writer.Write(minimumNearestBackgroundDistance);
        writer.Write(maximumNearestBackgroundDistance);
        writer.Write(meanNearestBackgroundDistance);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool IsSha256(string value) =>
        value is not null
        && value.Length == 64
        && value.All(Uri.IsHexDigit);
}
