using System.Diagnostics;
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

public static class C3DLineIntersectionRule
{
    private const double DirectionTolerance = 1e-8;
    private const double ParallelDenominatorEpsilon = 1e-12;

    public static C3DLineIntersectionEvaluation Evaluate(C3DLineIntersectionInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var first = input.FirstPublishedLine;
            var second = input.SecondPublishedLine;
            var a = new Point(first.AnchorX, first.AnchorY, first.AnchorZ);
            var b = new Point(second.AnchorX, second.AnchorY, second.AnchorZ);
            var u = new Point(first.DirectionX, first.DirectionY, first.DirectionZ);
            var v = new Point(second.DirectionX, second.DirectionY, second.DirectionZ);
            var dot = Math.Clamp(Dot(u, v), -1d, 1d);
            var acuteAngleDegrees = Math.Acos(Math.Abs(dot)) * 180d / Math.PI;
            if (!double.IsFinite(acuteAngleDegrees) || acuteAngleDegrees < input.MinimumAcuteAngleDegrees)
            {
                throw new InvalidDataException($"Line acute angle {acuteAngleDegrees:G8} degrees is below taught minimum {input.MinimumAcuteAngleDegrees:G8} degrees.");
            }

            var denominator = 1d - dot * dot;
            if (!double.IsFinite(denominator) || denominator <= ParallelDenominatorEpsilon)
            {
                throw new InvalidDataException("Line Intersection rejects parallel or numerically near-parallel lines.");
            }

            var w = Subtract(a, b);
            var d = Dot(u, w);
            var e = Dot(v, w);
            var firstParameter = (dot * e - d) / denominator;
            var secondParameter = (e - dot * d) / denominator;
            var firstClosest = Add(a, Scale(u, firstParameter));
            var secondClosest = Add(b, Scale(v, secondParameter));
            var gap = Length(Subtract(firstClosest, secondClosest));
            RequireFinite(firstParameter, "First line closest parameter");
            RequireFinite(secondParameter, "Second line closest parameter");
            RequireFinite(firstClosest, "First line closest point");
            RequireFinite(secondClosest, "Second line closest point");
            if (!double.IsFinite(gap) || gap > input.MaximumClosestApproachDistance)
            {
                throw new InvalidDataException($"Line closest-approach gap {gap:G8} source-coordinate exceeds taught maximum {input.MaximumClosestApproachDistance:G8}.");
            }

            var (firstMinimum, firstMaximum, firstExtension) = GetSupport(first, firstParameter);
            var (secondMinimum, secondMaximum, secondExtension) = GetSupport(second, secondParameter);
            if (firstExtension > input.MaximumSupportExtension || secondExtension > input.MaximumSupportExtension)
            {
                throw new InvalidDataException($"Line closest approach is outside taught inlier support extension {input.MaximumSupportExtension:G8} source-coordinate.");
            }

            var corner = Scale(Add(firstClosest, secondClosest), 0.5d);
            RequireFinite(corner, "Corner anchor");
            var provenance = $"{input.StepId}:LineIntersection:{C3DLineIntersectionFeature.ContractVersion}:closest=MidpointOfClosestPoints:parallel=RejectBelowMinimumAcuteAngle:support=WithinInlierProjectionExtentsWithMaximumExtension:first={first.ContentSha256}:second={second.ContentSha256}";
            var output = C3DLineIntersectionFeature.Create(
                input.OutputEntityId, first, second,
                input.MaximumClosestApproachDistance, input.MinimumAcuteAngleDegrees,
                input.MaximumSupportExtension, input.OutputRole,
                corner.X, corner.Y, corner.Z,
                firstClosest.X, firstClosest.Y, firstClosest.Z,
                secondClosest.X, secondClosest.Y, secondClosest.Z,
                firstParameter, secondParameter, acuteAngleDegrees, gap,
                firstMinimum, firstMaximum, firstExtension,
                secondMinimum, secondMaximum, secondExtension, provenance);
            stopwatch.Stop();
            return new C3DLineIntersectionEvaluation(
                new ToolResult(
                    "Line Intersection", ResultStatus.Pass,
                    "Completed - corner feature extraction; no acceptance rule evaluated.", stopwatch.Elapsed,
                    [
                        new Metric("Closest approach gap", MetricKind.Deviation, gap, "source-coordinate"),
                        new Metric("Acute angle", MetricKind.Deviation, acuteAngleDegrees, "degrees"),
                        new Metric("First support extension", MetricKind.Deviation, firstExtension, "source-coordinate"),
                        new Metric("Second support extension", MetricKind.Deviation, secondExtension, "source-coordinate")
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
        ValidateLine(first, "First");
        ValidateLine(second, "Second");
    }

    private static void ValidateLine(C3DLineFeature line, string label)
    {
        var values = new[]
        {
            line.AnchorX, line.AnchorY, line.AnchorZ,
            line.DirectionX, line.DirectionY, line.DirectionZ,
            line.SegmentStartX, line.SegmentStartY, line.SegmentStartZ,
            line.SegmentEndX, line.SegmentEndY, line.SegmentEndZ
        };
        if (values.Any(value => !double.IsFinite(value))) throw new InvalidDataException($"{label} LineFeature contains non-finite geometry.");
        var length = Math.Sqrt(line.DirectionX * line.DirectionX + line.DirectionY * line.DirectionY + line.DirectionZ * line.DirectionZ);
        if (!double.IsFinite(length) || Math.Abs(length - 1d) > DirectionTolerance)
        {
            throw new InvalidDataException($"{label} LineFeature direction must be finite and normalized.");
        }
    }

    private static (double Minimum, double Maximum, double Extension) GetSupport(C3DLineFeature line, double parameter)
    {
        var anchor = new Point(line.AnchorX, line.AnchorY, line.AnchorZ);
        var direction = new Point(line.DirectionX, line.DirectionY, line.DirectionZ);
        var start = Dot(Subtract(new Point(line.SegmentStartX, line.SegmentStartY, line.SegmentStartZ), anchor), direction);
        var end = Dot(Subtract(new Point(line.SegmentEndX, line.SegmentEndY, line.SegmentEndZ), anchor), direction);
        var minimum = Math.Min(start, end);
        var maximum = Math.Max(start, end);
        RequireFinite(minimum, "Line support minimum");
        RequireFinite(maximum, "Line support maximum");
        var extension = parameter < minimum ? minimum - parameter : parameter > maximum ? parameter - maximum : 0d;
        return (minimum, maximum, extension);
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
    private static double Dot(Point left, Point right) => left.X * right.X + left.Y * right.Y + left.Z * right.Z;
    private static Point Add(Point left, Point right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    private static Point Subtract(Point left, Point right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    private static Point Scale(Point point, double scale) => new(point.X * scale, point.Y * scale, point.Z * scale);
    private static double Length(Point point) => Math.Sqrt(Dot(point, point));
    private static void RequireFinite(double value, string label)
    {
        if (!double.IsFinite(value)) throw new InvalidDataException($"{label} is non-finite.");
    }
    private static void RequireFinite(Point point, string label)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z)) throw new InvalidDataException($"{label} is non-finite.");
    }
    private readonly record struct Point(double X, double Y, double Z);
}
