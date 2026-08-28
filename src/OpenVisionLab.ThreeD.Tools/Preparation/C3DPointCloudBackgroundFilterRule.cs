using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.Vision3D.FeatureExtraction;
using SdkMode = OpenVisionLab.Vision3D.FeatureExtraction.PointCloudBackgroundFilterMode;
using SdkOptions = OpenVisionLab.Vision3D.FeatureExtraction.PointCloudBackgroundFilterOptions;
using SdkTool = OpenVisionLab.Vision3D.FeatureExtraction.PointCloudBackgroundFilterTool;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DPointCloudBackgroundFilterInput(
    string StepId,
    C3DPointCloudSnapshot Current,
    C3DPointCloudSnapshot SavedBackground,
    string OutputEntityId,
    double MaximumBackgroundDistance);

public sealed record C3DPointCloudBackgroundFilterEvaluation(
    ToolResult Result,
    C3DPointCloudSnapshot? Output,
    C3DPointCloudBackgroundFilterEvidence? Evidence);

/// <summary>
/// Strict Studio adapter for the SDK-owned nearest-background-distance
/// projection. Studio owns point-cloud identity, matching metadata, lineage,
/// warning semantics, and evidence; the distance scan remains in the SDK.
/// </summary>
public static class C3DPointCloudBackgroundFilterRule
{
    public const string ToolName = "Point-Cloud Background Distance Filter";

    public static C3DPointCloudBackgroundFilterEvaluation Evaluate(
        C3DPointCloudBackgroundFilterInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var sdkCurrent = input.Current.Points
                .Select(point => new ThreeDPoint(point.X, point.Y, point.Z))
                .ToArray();
            var sdkBackground = input.SavedBackground.Points
                .Select(point => new ThreeDPoint(point.X, point.Y, point.Z))
                .ToArray();
            var sdkResult = new SdkTool().Execute(
                sdkCurrent,
                sdkBackground,
                new SdkOptions
                {
                    Mode = SdkMode.RemoveAtOrBelowDistance,
                    MaximumBackgroundDistance = input.MaximumBackgroundDistance
                },
                cancellationToken);
            if (!sdkResult.Success)
            {
                throw new InvalidDataException(sdkResult.Message);
            }

            ValidateSdkOutput(sdkResult, input.Current);
            var retainedPoints = sdkResult.RetainedPoints
                .Select(item => new C3DPoint3(item.Point.X, item.Point.Y, item.Point.Z))
                .ToArray();
            var output = input.Current.CreateDerived(
                input.OutputEntityId,
                retainedPoints,
                $"{input.StepId}:PointCloudBackgroundFilter:{C3DPointCloudBackgroundFilterEvidence.ContractVersion}:distance={input.MaximumBackgroundDistance:R}:distance-policy={C3DPointCloudBackgroundFilterEvidence.DistancePolicyName}:removal={C3DPointCloudBackgroundFilterEvidence.RemovalPolicyName}:matching={C3DPointCloudBackgroundFilterEvidence.MatchingPolicyName}:current={input.Current.ContentSha256}:background={input.SavedBackground.ContentSha256}");
            var evidence = C3DPointCloudBackgroundFilterEvidence.Create(
                input.StepId,
                input.Current.EntityId,
                input.Current.ContentSha256,
                input.Current.RootSourceSha256,
                input.Current.ByteLength,
                input.SavedBackground.EntityId,
                input.SavedBackground.ContentSha256,
                input.SavedBackground.RootSourceSha256,
                input.SavedBackground.ByteLength,
                output.EntityId,
                output.ContentSha256,
                output.RootSourceSha256,
                output.ByteLength,
                input.Current.Unit,
                input.Current.FrameId,
                input.Current.CoordinateConvention,
                sdkResult.MaximumBackgroundDistance,
                C3DPointCloudBackgroundFilterMode.RemoveAtOrBelowDistance,
                sdkResult.InputPointCount,
                sdkResult.BackgroundPointCount,
                sdkResult.RetainedPointCount,
                sdkResult.RemovedPointCount,
                sdkResult.MinimumNearestBackgroundDistance,
                sdkResult.MaximumNearestBackgroundDistance,
                sdkResult.MeanNearestBackgroundDistance,
                output.Provenance);
            stopwatch.Stop();
            var status = evidence.HasRetainedPoints ? ResultStatus.Pass : ResultStatus.Warning;
            var message = evidence.HasRetainedPoints
                ? "Completed deterministic nearest-background-distance filtering; current and saved-background inputs remain unchanged."
                : "The distance threshold removed every current point; the separate derived point-cloud output is empty.";
            return new C3DPointCloudBackgroundFilterEvaluation(
                new ToolResult(
                    ToolName,
                    status,
                    message,
                    stopwatch.Elapsed,
                    [
                        new Metric("Maximum background distance", MetricKind.Length, evidence.MaximumBackgroundDistance, input.Current.Unit),
                        new Metric("Input point count", MetricKind.Count, evidence.InputPointCount, "count"),
                        new Metric("Saved-background point count", MetricKind.Count, evidence.BackgroundPointCount, "count"),
                        new Metric("Retained point count", MetricKind.Count, evidence.RetainedPointCount, "count", status),
                        new Metric("Removed point count", MetricKind.Count, evidence.RemovedPointCount, "count"),
                        new Metric("Minimum nearest-background distance", MetricKind.Length, evidence.MinimumNearestBackgroundDistance, input.Current.Unit),
                        new Metric("Maximum nearest-background distance", MetricKind.Length, evidence.MaximumNearestBackgroundDistance, input.Current.Unit),
                        new Metric("Mean nearest-background distance", MetricKind.Length, evidence.MeanNearestBackgroundDistance, input.Current.Unit)
                    ],
                    [
                        new Overlay(
                            $"point-cloud-background-filter.{output.EntityId}",
                            OverlayKind.Point,
                            $"Nearest background distance: {evidence.RemovedPointCount:N0} removed point(s)",
                            status,
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

    public static void Validate(C3DPointCloudBackgroundFilterInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Current);
        ArgumentNullException.ThrowIfNull(input.SavedBackground);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (string.Equals(input.Current.EntityId, input.SavedBackground.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.Current.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.SavedBackground.EntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Point-cloud background-filter current, saved-background, and output identities must be distinct.");
        }

        ValidateSource(input.Current, "Current");
        ValidateSource(input.SavedBackground, "Saved background");
        if (!string.Equals(input.Current.Unit, input.SavedBackground.Unit, StringComparison.Ordinal)
            || !string.Equals(input.Current.FrameId, input.SavedBackground.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.Current.CoordinateConvention, input.SavedBackground.CoordinateConvention, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Current and saved-background point clouds must have identical units, frame, and coordinate convention; automatic alignment is not supported.");
        }

        if (!double.IsFinite(input.MaximumBackgroundDistance)
            || input.MaximumBackgroundDistance < 0d)
        {
            throw new InvalidDataException("Maximum point-cloud background distance must be finite and non-negative.");
        }
    }

    private static void ValidateSource(C3DPointCloudSnapshot source, string label)
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
            throw new InvalidDataException($"{label} point-cloud source identity or finite point payload is invalid.");
        }
    }

    private static void ValidateSdkOutput(
        OpenVisionLab.Vision3D.FeatureExtraction.PointCloudBackgroundFilterResult result,
        C3DPointCloudSnapshot current)
    {
        if (result.InputPointCount != current.Points.Count
            || result.BackgroundPointCount <= 0
            || result.RetainedPointCount < 0
            || result.RemovedPointCount < 0
            || result.RetainedPointCount + result.RemovedPointCount != result.InputPointCount
            || result.RetainedPoints.Count != result.RetainedPointCount)
        {
            throw new InvalidDataException("Point-cloud background-filter SDK counts do not preserve the current point-cloud cardinality.");
        }

        var expectedSourceIndex = -1;
        foreach (var retained in result.RetainedPoints)
        {
            if (retained.SourceIndex <= expectedSourceIndex
                || retained.SourceIndex < 0
                || retained.SourceIndex >= current.Points.Count
                || !double.IsFinite(retained.Point.X)
                || !double.IsFinite(retained.Point.Y)
                || !double.IsFinite(retained.Point.Z)
                || !double.IsFinite(retained.NearestBackgroundDistance)
                || retained.NearestBackgroundDistance < 0d)
            {
                throw new InvalidDataException("Point-cloud background-filter SDK output must preserve strictly increasing source order and finite distances.");
            }

            var sourcePoint = current.Points[retained.SourceIndex];
            if (sourcePoint.X != retained.Point.X
                || sourcePoint.Y != retained.Point.Y
                || sourcePoint.Z != retained.Point.Z)
            {
                throw new InvalidDataException("Point-cloud background-filter SDK output changed a retained point coordinate.");
            }

            expectedSourceIndex = retained.SourceIndex;
        }

        if (!double.IsFinite(result.MinimumNearestBackgroundDistance)
            || !double.IsFinite(result.MaximumNearestBackgroundDistance)
            || !double.IsFinite(result.MeanNearestBackgroundDistance)
            || result.MinimumNearestBackgroundDistance < 0d
            || result.MaximumNearestBackgroundDistance < result.MinimumNearestBackgroundDistance
            || result.MeanNearestBackgroundDistance < result.MinimumNearestBackgroundDistance
            || result.MeanNearestBackgroundDistance > result.MaximumNearestBackgroundDistance)
        {
            throw new InvalidDataException("Point-cloud background-filter SDK distance statistics are invalid.");
        }
    }

    private static bool IsSha256(string value) =>
        value is not null
        && value.Length == 64
        && value.All(Uri.IsHexDigit);
}
