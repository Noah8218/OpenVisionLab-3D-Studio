using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable evidence for one deterministic point-cloud voxel reduction.
/// Source and derived output identities remain separate; units, frame, and
/// coordinate convention are carried through without calibration inference.
/// </summary>
public sealed class C3DPointCloudVoxelDownsampleEvidence
{
    public const string ContractVersion = "1.0";
    public const string VoxelIndexPolicyName = "FloorFromExplicitOrigin";
    public const string RepresentativePolicyName = "FirstSourcePoint";
    public const string OutputOrderPolicyName = "FirstSourceAppearance";
    public const string LineagePolicyName = "SeparateSourceDerivedPointCloud";

    private C3DPointCloudVoxelDownsampleEvidence(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceRootSourceSha256,
        long sourceByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        long outputByteLength,
        string unit,
        string frameId,
        string coordinateConvention,
        double voxelEdgeLength,
        double originX,
        double originY,
        double originZ,
        int inputPointCount,
        int outputPointCount,
        int reducedPointCount,
        IReadOnlyList<int> representativeSourceIndexes,
        double inputMinimumX,
        double inputMinimumY,
        double inputMinimumZ,
        double inputMaximumX,
        double inputMaximumY,
        double inputMaximumZ,
        double outputMinimumX,
        double outputMinimumY,
        double outputMinimumZ,
        double outputMaximumX,
        double outputMaximumY,
        double outputMaximumZ,
        string provenance,
        string contentSha256)
    {
        StepId = stepId;
        SourceEntityId = sourceEntityId;
        SourceContentSha256 = sourceContentSha256;
        SourceRootSourceSha256 = sourceRootSourceSha256;
        SourceByteLength = sourceByteLength;
        OutputEntityId = outputEntityId;
        OutputContentSha256 = outputContentSha256;
        OutputRootSourceSha256 = outputRootSourceSha256;
        OutputByteLength = outputByteLength;
        Unit = unit;
        FrameId = frameId;
        CoordinateConvention = coordinateConvention;
        VoxelEdgeLength = voxelEdgeLength;
        OriginX = originX;
        OriginY = originY;
        OriginZ = originZ;
        InputPointCount = inputPointCount;
        OutputPointCount = outputPointCount;
        ReducedPointCount = reducedPointCount;
        RepresentativeSourceIndexes = Array.AsReadOnly(representativeSourceIndexes.ToArray());
        InputMinimumX = inputMinimumX;
        InputMinimumY = inputMinimumY;
        InputMinimumZ = inputMinimumZ;
        InputMaximumX = inputMaximumX;
        InputMaximumY = inputMaximumY;
        InputMaximumZ = inputMaximumZ;
        OutputMinimumX = outputMinimumX;
        OutputMinimumY = outputMinimumY;
        OutputMinimumZ = outputMinimumZ;
        OutputMaximumX = outputMaximumX;
        OutputMaximumY = outputMaximumY;
        OutputMaximumZ = outputMaximumZ;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string StepId { get; }
    public string SourceEntityId { get; }
    public string SourceContentSha256 { get; }
    public string SourceRootSourceSha256 { get; }
    public long SourceByteLength { get; }
    public string OutputEntityId { get; }
    public string OutputContentSha256 { get; }
    public string OutputRootSourceSha256 { get; }
    public long OutputByteLength { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string CoordinateConvention { get; }
    public double VoxelEdgeLength { get; }
    public double OriginX { get; }
    public double OriginY { get; }
    public double OriginZ { get; }
    public int InputPointCount { get; }
    public int OutputPointCount { get; }
    public int ReducedPointCount { get; }
    public IReadOnlyList<int> RepresentativeSourceIndexes { get; }
    public double InputMinimumX { get; }
    public double InputMinimumY { get; }
    public double InputMinimumZ { get; }
    public double InputMaximumX { get; }
    public double InputMaximumY { get; }
    public double InputMaximumZ { get; }
    public double OutputMinimumX { get; }
    public double OutputMinimumY { get; }
    public double OutputMinimumZ { get; }
    public double OutputMaximumX { get; }
    public double OutputMaximumY { get; }
    public double OutputMaximumZ { get; }
    public string VoxelIndexPolicy => VoxelIndexPolicyName;
    public string RepresentativePolicy => RepresentativePolicyName;
    public string OutputOrderPolicy => OutputOrderPolicyName;
    public string LineagePolicy => LineagePolicyName;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DPointCloudVoxelDownsampleEvidence Create(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceRootSourceSha256,
        long sourceByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        long outputByteLength,
        string unit,
        string frameId,
        string coordinateConvention,
        double voxelEdgeLength,
        double originX,
        double originY,
        double originZ,
        int inputPointCount,
        int outputPointCount,
        int reducedPointCount,
        IReadOnlyList<int> representativeSourceIndexes,
        double inputMinimumX,
        double inputMinimumY,
        double inputMinimumZ,
        double inputMaximumX,
        double inputMaximumY,
        double inputMaximumZ,
        double outputMinimumX,
        double outputMinimumY,
        double outputMinimumZ,
        double outputMaximumX,
        double outputMaximumY,
        double outputMaximumZ,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinateConvention);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!IsSha256(sourceContentSha256)
            || !IsSha256(sourceRootSourceSha256)
            || !IsSha256(outputContentSha256)
            || !IsSha256(outputRootSourceSha256))
        {
            throw new ArgumentException("Point-cloud voxel evidence requires SHA-256 identities.");
        }

        if (string.Equals(sourceEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Point-cloud voxel source and output identities must be distinct.");
        }

        ArgumentNullException.ThrowIfNull(representativeSourceIndexes);
        if (representativeSourceIndexes.Count != outputPointCount)
        {
            throw new ArgumentException("Point-cloud voxel evidence representative lineage must match the output count.");
        }

        var previousSourceIndex = -1;
        foreach (var sourceIndex in representativeSourceIndexes)
        {
            if (sourceIndex <= previousSourceIndex || sourceIndex < 0 || sourceIndex >= inputPointCount)
            {
                throw new ArgumentException("Point-cloud voxel evidence representative lineage must be strictly increasing and source-bounded.");
            }

            previousSourceIndex = sourceIndex;
        }

        var values = new[]
        {
            voxelEdgeLength,
            originX,
            originY,
            originZ,
            inputMinimumX,
            inputMinimumY,
            inputMinimumZ,
            inputMaximumX,
            inputMaximumY,
            inputMaximumZ,
            outputMinimumX,
            outputMinimumY,
            outputMinimumZ,
            outputMaximumX,
            outputMaximumY,
            outputMaximumZ
        };
        if (sourceByteLength <= 0
            || outputByteLength <= 0
            || !double.IsFinite(voxelEdgeLength)
            || voxelEdgeLength <= 0d
            || values.Any(value => !double.IsFinite(value))
            || inputMinimumX > inputMaximumX
            || inputMinimumY > inputMaximumY
            || inputMinimumZ > inputMaximumZ
            || outputMinimumX > outputMaximumX
            || outputMinimumY > outputMaximumY
            || outputMinimumZ > outputMaximumZ
            || outputMinimumX < inputMinimumX
            || outputMinimumY < inputMinimumY
            || outputMinimumZ < inputMinimumZ
            || outputMaximumX > inputMaximumX
            || outputMaximumY > inputMaximumY
            || outputMaximumZ > inputMaximumZ
            || inputPointCount <= 0
            || outputPointCount <= 0
            || reducedPointCount < 0
            || outputPointCount + reducedPointCount != inputPointCount)
        {
            throw new ArgumentException("Point-cloud voxel evidence contains an invalid identity, bounds, option, or count.");
        }

        var contentSha256 = CalculateContentSha256(
            stepId,
            sourceEntityId,
            sourceContentSha256,
            sourceRootSourceSha256,
            sourceByteLength,
            outputEntityId,
            outputContentSha256,
            outputRootSourceSha256,
            outputByteLength,
            unit,
            frameId,
            coordinateConvention,
            voxelEdgeLength,
            originX,
            originY,
            originZ,
            inputPointCount,
            outputPointCount,
            reducedPointCount,
            representativeSourceIndexes,
            inputMinimumX,
            inputMinimumY,
            inputMinimumZ,
            inputMaximumX,
            inputMaximumY,
            inputMaximumZ,
            outputMinimumX,
            outputMinimumY,
            outputMinimumZ,
            outputMaximumX,
            outputMaximumY,
            outputMaximumZ);
        return new C3DPointCloudVoxelDownsampleEvidence(
            stepId,
            sourceEntityId,
            sourceContentSha256.ToUpperInvariant(),
            sourceRootSourceSha256.ToUpperInvariant(),
            sourceByteLength,
            outputEntityId,
            outputContentSha256.ToUpperInvariant(),
            outputRootSourceSha256.ToUpperInvariant(),
            outputByteLength,
            unit,
            frameId,
            coordinateConvention,
            voxelEdgeLength,
            originX,
            originY,
            originZ,
            inputPointCount,
            outputPointCount,
            reducedPointCount,
            representativeSourceIndexes,
            inputMinimumX,
            inputMinimumY,
            inputMinimumZ,
            inputMaximumX,
            inputMaximumY,
            inputMaximumZ,
            outputMinimumX,
            outputMinimumY,
            outputMinimumZ,
            outputMaximumX,
            outputMaximumY,
            outputMaximumZ,
            provenance,
            contentSha256);
    }

    private static string CalculateContentSha256(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceRootSourceSha256,
        long sourceByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        long outputByteLength,
        string unit,
        string frameId,
        string coordinateConvention,
        double voxelEdgeLength,
        double originX,
        double originY,
        double originZ,
        int inputPointCount,
        int outputPointCount,
        int reducedPointCount,
        IReadOnlyList<int> representativeSourceIndexes,
        double inputMinimumX,
        double inputMinimumY,
        double inputMinimumZ,
        double inputMaximumX,
        double inputMaximumY,
        double inputMaximumZ,
        double outputMinimumX,
        double outputMinimumY,
        double outputMinimumZ,
        double outputMaximumX,
        double outputMaximumY,
        double outputMaximumZ)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DPointCloudVoxelDownsampleEvidence");
        writer.Write(ContractVersion);
        writer.Write(stepId);
        writer.Write(sourceEntityId);
        writer.Write(sourceContentSha256.ToUpperInvariant());
        writer.Write(sourceRootSourceSha256.ToUpperInvariant());
        writer.Write(sourceByteLength);
        writer.Write(outputEntityId);
        writer.Write(outputContentSha256.ToUpperInvariant());
        writer.Write(outputRootSourceSha256.ToUpperInvariant());
        writer.Write(outputByteLength);
        writer.Write(unit);
        writer.Write(frameId);
        writer.Write(coordinateConvention);
        writer.Write(voxelEdgeLength);
        writer.Write(originX);
        writer.Write(originY);
        writer.Write(originZ);
        writer.Write(VoxelIndexPolicyName);
        writer.Write(RepresentativePolicyName);
        writer.Write(OutputOrderPolicyName);
        writer.Write(LineagePolicyName);
        writer.Write(inputPointCount);
        writer.Write(outputPointCount);
        writer.Write(reducedPointCount);
        writer.Write(representativeSourceIndexes.Count);
        foreach (var sourceIndex in representativeSourceIndexes)
        {
            writer.Write(sourceIndex);
        }
        writer.Write(inputMinimumX);
        writer.Write(inputMinimumY);
        writer.Write(inputMinimumZ);
        writer.Write(inputMaximumX);
        writer.Write(inputMaximumY);
        writer.Write(inputMaximumZ);
        writer.Write(outputMinimumX);
        writer.Write(outputMinimumY);
        writer.Write(outputMinimumZ);
        writer.Write(outputMaximumX);
        writer.Write(outputMaximumY);
        writer.Write(outputMaximumZ);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool IsSha256(string value) =>
        value is not null
        && value.Length == 64
        && value.All(Uri.IsHexDigit);
}
