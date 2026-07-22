using System.Diagnostics;
using System.Numerics;
using NoahPointPairOptions = Lib.ThreeD.Inspection.PointPairDimensionsInspectionOptions;
using NoahPointPairResult = Lib.ThreeD.Inspection.PointPairDimensionsInspectionResult;
using NoahPointPairTool = Lib.ThreeD.Inspection.PointPairDimensionsInspectionTool;
using NoahPoint = Lib.ThreeD.FeatureExtraction.ThreeDPoint;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record PointPairDimensionsInput(
    string SourceEntityId,
    Vector3 First,
    Vector3 Second,
    double FirstRawHeight,
    double SecondRawHeight,
    C3DPointPairDimensionsAcceptance Acceptance,
    string Unit,
    string RawHeightUnit,
    Vector3? HeightAxis = null);

public sealed record PointPairDimensionsEvaluation(
    ToolResult Result,
    Vector3 Delta,
    double Distance,
    double PlanarWidth,
    double ElevationAngleDegrees,
    double RawHeightDelta);

public static class PointPairDimensionsRule
{
    public const string ToolName = "C3D Point Pair Dimensions";

    public static PointPairDimensionsEvaluation Evaluate(PointPairDimensionsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(input.SourceEntityId)
            || string.IsNullOrWhiteSpace(input.Unit)
            || string.IsNullOrWhiteSpace(input.RawHeightUnit))
        {
            return Error(input, "Source entity ID and units are required.", stopwatch.Elapsed);
        }

        if (!IsFinite(input.First)
            || !IsFinite(input.Second)
            || !double.IsFinite(input.FirstRawHeight)
            || !double.IsFinite(input.SecondRawHeight))
        {
            return Error(input, "Point pair coordinates and raw heights must be finite.", stopwatch.Elapsed);
        }

        if (!IsValid(input.Acceptance))
        {
            return Error(input, "Expected values and tolerances are invalid.", stopwatch.Elapsed);
        }

        try
        {
            var heightAxis = input.HeightAxis ?? Vector3.UnitY;
            NoahPointPairResult evaluation = new NoahPointPairTool().Execute(
                ToNoah(input.First),
                ToNoah(input.Second),
                ToNoah(heightAxis),
                input.FirstRawHeight,
                input.SecondRawHeight,
                new NoahPointPairOptions
                {
                    ExpectedDistance = input.Acceptance.ExpectedDistance,
                    DistanceTolerance = input.Acceptance.DistanceTolerance,
                    ExpectedPlanarWidth = input.Acceptance.ExpectedWidth,
                    PlanarWidthTolerance = input.Acceptance.WidthTolerance,
                    ExpectedElevationAngleDegrees = input.Acceptance.ExpectedElevationAngleDegrees,
                    ElevationAngleToleranceDegrees = input.Acceptance.ElevationAngleToleranceDegrees
                });

            var delta = new Vector3((float)evaluation.Delta.X, (float)evaluation.Delta.Y, (float)evaluation.Delta.Z);
            var distance = evaluation.Distance;
            var width = evaluation.PlanarWidth;
            var angle = evaluation.ElevationAngleDegrees;
            var rawHeightDelta = evaluation.ScalarHeightDelta;
            var usesLegacyYAxis = input.HeightAxis is null
                || Vector3.DistanceSquared(input.HeightAxis.Value, Vector3.UnitY) <= 1e-12f;
            var planarWidthName = usesLegacyYAxis ? "XZ planar width" : "Planar width";
            var expectedPlanarWidthName = usesLegacyYAxis ? "Expected XZ planar width" : "Expected planar width";
            var distanceStatus = evaluation.DistancePassed ? ResultStatus.Pass : ResultStatus.Fail;
            var widthStatus = evaluation.PlanarWidthPassed ? ResultStatus.Pass : ResultStatus.Fail;
            var angleStatus = evaluation.ElevationAnglePassed ? ResultStatus.Pass : ResultStatus.Fail;
            var status = evaluation.Passed ? ResultStatus.Pass : ResultStatus.Fail;
            stopwatch.Stop();

            var result = new ToolResult(
                ToolName,
                status,
                status == ResultStatus.Pass
                    ? "Point pair dimensions are within configured tolerances. Source geometry is unchanged."
                    : "One or more point pair dimensions exceed configured tolerances. Source geometry is unchanged.",
                stopwatch.Elapsed,
                [
                    new Metric("3D distance", MetricKind.Length, distance, input.Unit, distanceStatus),
                    new Metric(planarWidthName, MetricKind.Length, width, input.Unit, widthStatus),
                    new Metric("Elevation angle", MetricKind.Angle, angle, "degree", angleStatus),
                    new Metric("Delta X", MetricKind.Length, delta.X, input.Unit),
                    new Metric("Delta Y", MetricKind.Length, delta.Y, input.Unit),
                    new Metric("Delta Z", MetricKind.Length, delta.Z, input.Unit),
                    new Metric("Height-axis delta", MetricKind.Length, evaluation.AxialHeightDelta, input.Unit),
                    new Metric("Raw height delta", MetricKind.Length, rawHeightDelta, input.RawHeightUnit),
                    new Metric("Expected 3D distance", MetricKind.Length, input.Acceptance.ExpectedDistance, input.Unit),
                    new Metric("Distance tolerance", MetricKind.Length, input.Acceptance.DistanceTolerance, input.Unit),
                    new Metric(expectedPlanarWidthName, MetricKind.Length, input.Acceptance.ExpectedWidth, input.Unit),
                    new Metric("Width tolerance", MetricKind.Length, input.Acceptance.WidthTolerance, input.Unit),
                    new Metric("Expected elevation angle", MetricKind.Angle, input.Acceptance.ExpectedElevationAngleDegrees, "degree"),
                    new Metric("Elevation angle tolerance", MetricKind.Angle, input.Acceptance.ElevationAngleToleranceDegrees, "degree")
                ],
                [
                    new Overlay("overlay.c3d-point-pair-line", OverlayKind.Polyline, "Point pair measurement line", status, input.SourceEntityId),
                    new Overlay("overlay.c3d-point-pair-endpoints", OverlayKind.Marker, "Point pair endpoint markers", status, input.SourceEntityId)
                ]);

            return new PointPairDimensionsEvaluation(result, delta, distance, width, angle, rawHeightDelta);
        }
        catch (ArgumentException exception)
        {
            var message = exception.Message.Contains("distinct", StringComparison.OrdinalIgnoreCase)
                ? "Point pair references must resolve to different positions."
                : exception.Message;
            return Error(input, message, stopwatch.Elapsed);
        }
    }

    private static PointPairDimensionsEvaluation Error(PointPairDimensionsInput input, string message, TimeSpan elapsed) =>
        new(
            new ToolResult(
                ToolName,
                ResultStatus.Error,
                message,
                elapsed,
                [new Metric("3D distance", MetricKind.Length, double.NaN, input.Unit ?? string.Empty, ResultStatus.Error)],
                []),
            Vector3.Zero,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN);

    private static bool IsValid(C3DPointPairDimensionsAcceptance? acceptance) =>
        acceptance is not null
        && double.IsFinite(acceptance.ExpectedDistance)
        && acceptance.ExpectedDistance >= 0.0
        && double.IsFinite(acceptance.DistanceTolerance)
        && acceptance.DistanceTolerance >= 0.0
        && double.IsFinite(acceptance.ExpectedWidth)
        && acceptance.ExpectedWidth >= 0.0
        && double.IsFinite(acceptance.WidthTolerance)
        && acceptance.WidthTolerance >= 0.0
        && double.IsFinite(acceptance.ExpectedElevationAngleDegrees)
        && acceptance.ExpectedElevationAngleDegrees >= -90.0
        && acceptance.ExpectedElevationAngleDegrees <= 90.0
        && double.IsFinite(acceptance.ElevationAngleToleranceDegrees)
        && acceptance.ElevationAngleToleranceDegrees >= 0.0;

    private static bool IsFinite(Vector3 point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);

    private static NoahPoint ToNoah(Vector3 point) => new(point.X, point.Y, point.Z);
}
