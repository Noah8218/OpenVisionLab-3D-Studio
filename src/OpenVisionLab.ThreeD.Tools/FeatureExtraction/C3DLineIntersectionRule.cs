using System.Diagnostics;
using NoahLineGeometry = Lib.ThreeD.FeatureExtraction.ThreeDLineGeometry;
using NoahLineIntersectionOptions = Lib.ThreeD.FeatureExtraction.LineIntersectionOptions;
using NoahLineIntersectionTool = Lib.ThreeD.FeatureExtraction.LineIntersectionTool;
using NoahPoint = Lib.ThreeD.FeatureExtraction.ThreeDPoint;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DLineIntersectionInput(
    string StepId,
    C3DLineFeature FirstPublishedLine,
    C3DLineFeature SecondPublishedLine,
    string OutputEntityId,
    double MaximumClosestApproachDistance,
    double MinimumAcuteAngleDegrees,
    double MaximumSupportExtension,
    string OutputRole);

public sealed record C3DLineIntersectionEvaluation(ToolResult Result, C3DLineIntersectionFeature? Output);

/// <summary>
/// Studio typed adapter for Library-Noah's source-neutral full-XYZ line
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
            var noahResult = new NoahLineIntersectionTool().Execute(
                ToNoahGeometry(first),
                ToNoahGeometry(second),
                new NoahLineIntersectionOptions
                {
                    MaximumClosestApproachDistance = input.MaximumClosestApproachDistance,
                    MinimumAcuteAngleDegrees = input.MinimumAcuteAngleDegrees,
                    MaximumSupportExtension = input.MaximumSupportExtension
                },
                cancellationToken);
            if (!noahResult.Success)
            {
                throw new InvalidDataException(noahResult.Message);
            }
            if (noahResult.CornerAnchor is null || noahResult.FirstClosestPoint is null || noahResult.SecondClosestPoint is null)
            {
                throw new InvalidDataException("Library-Noah line intersection returned incomplete geometry evidence.");
            }

            var provenance = $"{input.StepId}:LineIntersection:{C3DLineIntersectionFeature.ContractVersion}:closest=MidpointOfClosestPoints:parallel=RejectBelowMinimumAcuteAngle:support=WithinInlierProjectionExtentsWithMaximumExtension:first={first.ContentSha256}:second={second.ContentSha256}";
            var output = C3DLineIntersectionFeature.Create(
                input.OutputEntityId, first, second,
                input.MaximumClosestApproachDistance, input.MinimumAcuteAngleDegrees,
                input.MaximumSupportExtension, input.OutputRole,
                noahResult.CornerAnchor.X, noahResult.CornerAnchor.Y, noahResult.CornerAnchor.Z,
                noahResult.FirstClosestPoint.X, noahResult.FirstClosestPoint.Y, noahResult.FirstClosestPoint.Z,
                noahResult.SecondClosestPoint.X, noahResult.SecondClosestPoint.Y, noahResult.SecondClosestPoint.Z,
                noahResult.FirstLineParameter, noahResult.SecondLineParameter,
                noahResult.AcuteAngleDegrees, noahResult.ClosestApproachDistance,
                noahResult.FirstSupportMinimum, noahResult.FirstSupportMaximum, noahResult.FirstSupportExtension,
                noahResult.SecondSupportMinimum, noahResult.SecondSupportMaximum, noahResult.SecondSupportExtension,
                provenance);
            stopwatch.Stop();
            return new C3DLineIntersectionEvaluation(
                new ToolResult(
                    "Line Intersection", ResultStatus.Pass,
                    "Completed - corner feature extraction; no acceptance rule evaluated.", stopwatch.Elapsed,
                    [
                        new Metric("Closest approach gap", MetricKind.Deviation, noahResult.ClosestApproachDistance, "source-coordinate"),
                        new Metric("Acute angle", MetricKind.Deviation, noahResult.AcuteAngleDegrees, "degrees"),
                        new Metric("First support extension", MetricKind.Deviation, noahResult.FirstSupportExtension, "source-coordinate"),
                        new Metric("Second support extension", MetricKind.Deviation, noahResult.SecondSupportExtension, "source-coordinate")
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

    private static NoahLineGeometry ToNoahGeometry(C3DLineFeature line) => new(
        new NoahPoint(line.AnchorX, line.AnchorY, line.AnchorZ),
        new NoahPoint(line.DirectionX, line.DirectionY, line.DirectionZ),
        new NoahPoint(line.SegmentStartX, line.SegmentStartY, line.SegmentStartZ),
        new NoahPoint(line.SegmentEndX, line.SegmentEndY, line.SegmentEndZ));

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
