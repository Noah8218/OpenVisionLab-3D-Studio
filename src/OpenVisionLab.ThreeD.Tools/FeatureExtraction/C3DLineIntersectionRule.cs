using System.Diagnostics;
using SdkLineGeometry = OpenVisionLab.Vision3D.FeatureExtraction.ThreeDLineGeometry;
using SdkLineIntersectionOptions = OpenVisionLab.Vision3D.FeatureExtraction.LineIntersectionOptions;
using SdkLineIntersectionTool = OpenVisionLab.Vision3D.FeatureExtraction.LineIntersectionTool;
using SdkPoint = OpenVisionLab.Vision3D.FeatureExtraction.ThreeDPoint;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DLineIntersectionInput(
    string StepId,
    IC3DLineGeometry FirstPublishedLine,
    IC3DLineGeometry SecondPublishedLine,
    string OutputEntityId,
    double MaximumClosestApproachDistance,
    double MinimumAcuteAngleDegrees,
    double MaximumSupportExtension,
    string OutputRole);

public sealed record C3DLineIntersectionEvaluation(ToolResult Result, C3DLineIntersectionFeature? Output);

/// <summary>
/// Studio typed adapter for OpenVisionLab Vision SDK's source-neutral full-XYZ line
/// intersection geometry. Studio retains Published C3D lineage, recipe roles,
/// artifact identity, metrics, overlay, and explicit lifecycle evidence.
/// </summary>
public static class C3DLineIntersectionRule
{
    public static C3DLineIntersectionEvaluation Evaluate(C3DLineIntersectionInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var first = input.FirstPublishedLine;
            var second = input.SecondPublishedLine;
            var sdkResult = new SdkLineIntersectionTool().Execute(
                ToSdkGeometry(first),
                ToSdkGeometry(second),
                new SdkLineIntersectionOptions
                {
                    MaximumClosestApproachDistance = input.MaximumClosestApproachDistance,
                    MinimumAcuteAngleDegrees = input.MinimumAcuteAngleDegrees,
                    MaximumSupportExtension = input.MaximumSupportExtension
                },
                cancellationToken);
            if (!sdkResult.Success)
            {
                throw new InvalidDataException(sdkResult.Message);
            }
            if (sdkResult.CornerAnchor is null || sdkResult.FirstClosestPoint is null || sdkResult.SecondClosestPoint is null)
            {
                throw new InvalidDataException("OpenVisionLab Vision SDK line intersection returned incomplete geometry evidence.");
            }

            var provenance = $"{input.StepId}:LineIntersection:{C3DLineIntersectionFeature.ContractVersion}:closest=MidpointOfClosestPoints:parallel=RejectBelowMinimumAcuteAngle:support=WithinInlierProjectionExtentsWithMaximumExtension:first={first.ContentSha256}:second={second.ContentSha256}";
            var output = C3DLineIntersectionFeature.Create(
                input.OutputEntityId, first, second,
                input.MaximumClosestApproachDistance, input.MinimumAcuteAngleDegrees,
                input.MaximumSupportExtension, input.OutputRole,
                sdkResult.CornerAnchor.X, sdkResult.CornerAnchor.Y, sdkResult.CornerAnchor.Z,
                sdkResult.FirstClosestPoint.X, sdkResult.FirstClosestPoint.Y, sdkResult.FirstClosestPoint.Z,
                sdkResult.SecondClosestPoint.X, sdkResult.SecondClosestPoint.Y, sdkResult.SecondClosestPoint.Z,
                sdkResult.FirstLineParameter, sdkResult.SecondLineParameter,
                sdkResult.AcuteAngleDegrees, sdkResult.ClosestApproachDistance,
                sdkResult.FirstSupportMinimum, sdkResult.FirstSupportMaximum, sdkResult.FirstSupportExtension,
                sdkResult.SecondSupportMinimum, sdkResult.SecondSupportMaximum, sdkResult.SecondSupportExtension,
                provenance);
            stopwatch.Stop();
            return new C3DLineIntersectionEvaluation(
                new ToolResult(
                    "Line Intersection", ResultStatus.Pass,
                    "Completed - corner feature extraction; no acceptance rule evaluated.", stopwatch.Elapsed,
                    [
                        new Metric("Closest approach gap", MetricKind.Deviation, sdkResult.ClosestApproachDistance, "source-coordinate"),
                        new Metric("Acute angle", MetricKind.Deviation, sdkResult.AcuteAngleDegrees, "degrees"),
                        new Metric("First support extension", MetricKind.Deviation, sdkResult.FirstSupportExtension, "source-coordinate"),
                        new Metric("Second support extension", MetricKind.Deviation, sdkResult.SecondSupportExtension, "source-coordinate")
                    ],
                    [new Overlay(input.OutputEntityId, OverlayKind.Point, "Full-XYZ closest-approach corner anchor", SourceEntityId: first.RootSourceEntityId)]),
                output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or OverflowException)
        {
            stopwatch.Stop();
            return new C3DLineIntersectionEvaluation(
                new ToolResult("Line Intersection", ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []), null);
        }
    }

    private static SdkLineGeometry ToSdkGeometry(IC3DLineGeometry line) => new(
        new SdkPoint(line.AnchorX, line.AnchorY, line.AnchorZ),
        new SdkPoint(line.DirectionX, line.DirectionY, line.DirectionZ),
        new SdkPoint(line.SegmentStartX, line.SegmentStartY, line.SegmentStartZ),
        new SdkPoint(line.SegmentEndX, line.SegmentEndY, line.SegmentEndZ));

    private static void Validate(C3DLineIntersectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.FirstPublishedLine);
        ArgumentNullException.ThrowIfNull(input.SecondPublishedLine);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputRole);
        var first = input.FirstPublishedLine;
        var second = input.SecondPublishedLine;
        if (string.Equals(first.OutputEntityId, second.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(first.ContentSha256, second.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Line Intersection requires two distinct published LineFeature inputs.");
        }
        if (string.Equals(input.OutputEntityId, first.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, second.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Line Intersection output must differ from both LineFeature inputs.");
        }
        if (!Same(first.RootSourceEntityId, second.RootSourceEntityId)
            || !Same(first.RootSourceSha256, second.RootSourceSha256)
            || !Same(first.Unit, second.Unit)
            || !Same(first.FrameId, second.FrameId)
            || !Same(first.CoordinateConvention, second.CoordinateConvention))
        {
            throw new InvalidDataException("Published LineFeature root source, unit, frame, or coordinate convention does not match.");
        }
        if (!double.IsFinite(input.MaximumClosestApproachDistance) || input.MaximumClosestApproachDistance <= 0)
        {
            throw new InvalidDataException("MaximumClosestApproachDistance must be an explicit finite number greater than zero.");
        }
        if (!double.IsFinite(input.MinimumAcuteAngleDegrees) || input.MinimumAcuteAngleDegrees <= 0 || input.MinimumAcuteAngleDegrees > 90)
        {
            throw new InvalidDataException("MinimumAcuteAngleDegrees must be an explicit finite number greater than zero and no greater than 90.");
        }
        if (!double.IsFinite(input.MaximumSupportExtension) || input.MaximumSupportExtension < 0)
        {
            throw new InvalidDataException("MaximumSupportExtension must be an explicit finite number no less than zero.");
        }
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
}
