using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.Vision3D.FeatureExtraction;
using SdkOptions = OpenVisionLab.Vision3D.FeatureExtraction.PointCloudVoxelDownsampleOptions;
using SdkTool = OpenVisionLab.Vision3D.FeatureExtraction.PointCloudVoxelDownsampleTool;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DPointCloudVoxelDownsampleInput(
    string StepId,
    C3DPointCloudSnapshot Source,
    string OutputEntityId,
    double VoxelEdgeLength,
    double OriginX,
    double OriginY,
    double OriginZ);

public sealed record C3DPointCloudVoxelDownsampleEvaluation(
    ToolResult Result,
    C3DPointCloudSnapshot? Output,
    C3DPointCloudVoxelDownsampleEvidence? Evidence);

/// <summary>
/// Strict Studio adapter for the SDK-owned deterministic voxel reduction.
/// Studio owns source identity, units/frame/convention, separate lineage,
/// and immutable evidence; voxel assignment remains in the SDK.
/// </summary>
public static class C3DPointCloudVoxelDownsampleRule
{
    public const string ToolName = "Point-Cloud Voxel Downsample";

    public static C3DPointCloudVoxelDownsampleEvaluation Evaluate(
        C3DPointCloudVoxelDownsampleInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var sdkPoints = input.Source.Points
                .Select(point => new ThreeDPoint(point.X, point.Y, point.Z))
                .ToArray();
            var sdkResult = new SdkTool().Execute(
                sdkPoints,
                new SdkOptions
                {
                    VoxelEdgeLength = input.VoxelEdgeLength,
                    OriginX = input.OriginX,
                    OriginY = input.OriginY,
                    OriginZ = input.OriginZ
                },
                cancellationToken);
            if (!sdkResult.Success)
            {
                throw new InvalidDataException(sdkResult.Message);
            }

            ValidateSdkOutput(sdkResult, input.Source, input);
            var retainedPoints = sdkResult.Representatives
                .Select(item => new C3DPoint3(item.Point.X, item.Point.Y, item.Point.Z))
                .ToArray();
            var output = input.Source.CreateDerived(
                input.OutputEntityId,
                retainedPoints,
                $"{input.StepId}:PointCloudVoxelDownsample:{C3DPointCloudVoxelDownsampleEvidence.ContractVersion}:edge={input.VoxelEdgeLength:R}:origin={input.OriginX:R},{input.OriginY:R},{input.OriginZ:R}:index-policy={C3DPointCloudVoxelDownsampleEvidence.VoxelIndexPolicyName}:representative={C3DPointCloudVoxelDownsampleEvidence.RepresentativePolicyName}:order={C3DPointCloudVoxelDownsampleEvidence.OutputOrderPolicyName}:source={input.Source.ContentSha256}");
            var evidence = C3DPointCloudVoxelDownsampleEvidence.Create(
                input.StepId,
                input.Source.EntityId,
                input.Source.ContentSha256,
                input.Source.RootSourceSha256,
                input.Source.ByteLength,
                output.EntityId,
                output.ContentSha256,
                output.RootSourceSha256,
                output.ByteLength,
                input.Source.Unit,
                input.Source.FrameId,
                input.Source.CoordinateConvention,
                sdkResult.VoxelEdgeLength,
                sdkResult.OriginX,
                sdkResult.OriginY,
                sdkResult.OriginZ,
                sdkResult.InputPointCount,
                sdkResult.OutputPointCount,
                sdkResult.ReducedPointCount,
                sdkResult.Representatives.Select(item => item.SourceIndex).ToArray(),
                sdkResult.InputBounds.MinimumX,
                sdkResult.InputBounds.MinimumY,
                sdkResult.InputBounds.MinimumZ,
                sdkResult.InputBounds.MaximumX,
                sdkResult.InputBounds.MaximumY,
                sdkResult.InputBounds.MaximumZ,
                sdkResult.OutputBounds.MinimumX,
                sdkResult.OutputBounds.MinimumY,
                sdkResult.OutputBounds.MinimumZ,
                sdkResult.OutputBounds.MaximumX,
                sdkResult.OutputBounds.MaximumY,
                sdkResult.OutputBounds.MaximumZ,
                output.Provenance);
            stopwatch.Stop();
            return new C3DPointCloudVoxelDownsampleEvaluation(
                new ToolResult(
                    ToolName,
                    ResultStatus.Pass,
                    "Completed deterministic point-cloud voxel reduction; the source remains unchanged and the output is a separate derived point cloud.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Voxel edge length", MetricKind.Length, evidence.VoxelEdgeLength, input.Source.Unit),
                        new Metric("Input point count", MetricKind.Count, evidence.InputPointCount, "count"),
                        new Metric("Output point count", MetricKind.Count, evidence.OutputPointCount, "count"),
                        new Metric("Reduced point count", MetricKind.Count, evidence.ReducedPointCount, "count")
                    ],
                    [
                        new Overlay(
                            $"point-cloud-voxel-downsample.{output.EntityId}",
                            OverlayKind.Point,
                            $"Voxel reduction: {evidence.ReducedPointCount:N0} point(s) reduced",
                            ResultStatus.Pass,
                            output.EntityId)
                    ]),
                output,
                evidence);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            stopwatch.Stop();
            return new(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null,
                null);
        }
    }

    public static void Validate(C3DPointCloudVoxelDownsampleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (string.Equals(input.Source.EntityId, input.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Point-cloud voxel source and output identities must be distinct.");
        }

        ValidateSource(input.Source);
        if (!double.IsFinite(input.VoxelEdgeLength) || input.VoxelEdgeLength <= 0d)
        {
            throw new InvalidDataException("Point-cloud voxel edge length must be finite and positive.");
        }

        if (!double.IsFinite(input.OriginX)
            || !double.IsFinite(input.OriginY)
            || !double.IsFinite(input.OriginZ))
        {
            throw new InvalidDataException("Point-cloud voxel origin must contain finite XYZ coordinates.");
        }
    }

    private static void ValidateSource(C3DPointCloudSnapshot source)
    {
        if (string.IsNullOrWhiteSpace(source.SourceFormat)
            || string.IsNullOrWhiteSpace(source.Unit)
            || string.IsNullOrWhiteSpace(source.FrameId)
            || string.IsNullOrWhiteSpace(source.CoordinateConvention)
            || source.ByteLength <= 0
            || !IsSha256(source.ContentSha256)
            || !IsSha256(source.RootSourceSha256)
            || source.Points.Count == 0
            || source.Points.Any(point => !point.IsFinite))
        {
            throw new InvalidDataException("Point-cloud voxel source identity or finite point payload is invalid.");
        }
    }

    private static void ValidateSdkOutput(
        PointCloudVoxelDownsampleResult result,
        C3DPointCloudSnapshot source,
        C3DPointCloudVoxelDownsampleInput input)
    {
        if (result.InputPointCount != source.Points.Count
            || result.OutputPointCount <= 0
            || result.ReducedPointCount < 0
            || result.OutputPointCount + result.ReducedPointCount != result.InputPointCount
            || result.Representatives.Count != result.OutputPointCount
            || result.InputBounds is null
            || result.OutputBounds is null
            || result.VoxelEdgeLength != input.VoxelEdgeLength
            || result.OriginX != input.OriginX
            || result.OriginY != input.OriginY
            || result.OriginZ != input.OriginZ)
        {
            throw new InvalidDataException("Point-cloud voxel SDK counts, options, or bounds do not preserve the source contract.");
        }

        var expectedSourceIndex = -1;
        foreach (var representative in result.Representatives)
        {
            var representativePoint = representative.Point;
            if (representative.SourceIndex <= expectedSourceIndex
                || representative.SourceIndex < 0
                || representative.SourceIndex >= source.Points.Count
                || representativePoint is null
                || !double.IsFinite(representativePoint.X)
                || !double.IsFinite(representativePoint.Y)
                || !double.IsFinite(representativePoint.Z))
            {
                throw new InvalidDataException("Point-cloud voxel SDK output must preserve strictly increasing source order and finite representatives.");
            }

            var sourcePoint = source.Points[representative.SourceIndex];
            if (sourcePoint.X != representativePoint.X
                || sourcePoint.Y != representativePoint.Y
                || sourcePoint.Z != representativePoint.Z)
            {
                throw new InvalidDataException("Point-cloud voxel SDK output changed a representative source coordinate.");
            }

            expectedSourceIndex = representative.SourceIndex;
        }

        ValidateBounds(result.InputBounds, "input");
        ValidateBounds(result.OutputBounds, "output");
        if (result.OutputBounds.MinimumX < result.InputBounds.MinimumX
            || result.OutputBounds.MinimumY < result.InputBounds.MinimumY
            || result.OutputBounds.MinimumZ < result.InputBounds.MinimumZ
            || result.OutputBounds.MaximumX > result.InputBounds.MaximumX
            || result.OutputBounds.MaximumY > result.InputBounds.MaximumY
            || result.OutputBounds.MaximumZ > result.InputBounds.MaximumZ)
        {
            throw new InvalidDataException("Point-cloud voxel SDK output bounds must remain within the source bounds.");
        }
    }

    private static void ValidateBounds(
        PointCloudVoxelDownsampleBounds bounds,
        string label)
    {
        var values = new[]
        {
            bounds.MinimumX,
            bounds.MinimumY,
            bounds.MinimumZ,
            bounds.MaximumX,
            bounds.MaximumY,
            bounds.MaximumZ
        };
        if (values.Any(value => !double.IsFinite(value))
            || bounds.MinimumX > bounds.MaximumX
            || bounds.MinimumY > bounds.MaximumY
            || bounds.MinimumZ > bounds.MaximumZ)
        {
            throw new InvalidDataException($"Point-cloud voxel SDK {label} bounds are invalid.");
        }
    }

    private static bool IsSha256(string value) =>
        value is not null
        && value.Length == 64
        && value.All(Uri.IsHexDigit);
}
