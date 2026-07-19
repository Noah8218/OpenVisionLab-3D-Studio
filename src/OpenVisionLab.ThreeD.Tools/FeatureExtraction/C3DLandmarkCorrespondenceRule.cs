using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DLandmarkCorrespondenceInput(
    string StepId,
    string OutputEntityId,
    IReadOnlyList<C3DLineIntersectionFeature> PublishedCornerAnchors,
    IReadOnlyList<C3DReferenceLandmark> ReferenceLandmarks,
    string ReferenceFrameId,
    string ReferenceUnit,
    string ReferenceProvenance,
    string ReferenceRevision,
    double MinimumNormalizedTetrahedronVolume);

public sealed record C3DReferenceLandmark(string Id, double X, double Y, double Z);

public sealed record C3DLandmarkCorrespondenceEvaluation(
    ToolResult Result,
    C3DLandmarkCorrespondenceSet? Output);

public static class C3DLandmarkCorrespondenceRule
{
    private const int RequiredPairCount = 4;
    private const double RankRelativeTolerance = 1e-12;

    public static C3DLandmarkCorrespondenceEvaluation Evaluate(
        C3DLandmarkCorrespondenceInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var anchors = input.PublishedCornerAnchors;
            var references = input.ReferenceLandmarks;
            var pairs = Enumerable.Range(0, RequiredPairCount)
                .Select(index => new C3DLandmarkCorrespondencePair(
                    anchors[index].OutputEntityId,
                    anchors[index].OutputRole,
                    anchors[index].ContentSha256,
                    anchors[index].CornerAnchorX,
                    anchors[index].CornerAnchorY,
                    anchors[index].CornerAnchorZ,
                    references[index].Id,
                    references[index].X,
                    references[index].Y,
                    references[index].Z))
                .ToArray();
            var sourceCoordinates = pairs.Select(pair => new Point(pair.SourceX, pair.SourceY, pair.SourceZ)).ToArray();
            var referenceCoordinates = pairs.Select(pair => new Point(pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ)).ToArray();
            var sourceRank = GetAugmentedRank(sourceCoordinates);
            var referenceRank = GetAugmentedRank(referenceCoordinates);
            var sourceVolume = GetNormalizedTetrahedronVolume(sourceCoordinates);
            var referenceVolume = GetNormalizedTetrahedronVolume(referenceCoordinates);
            if (sourceRank < RequiredPairCount || sourceVolume <= input.MinimumNormalizedTetrahedronVolume)
            {
                throw new InvalidDataException($"Source landmark tetrahedron is not affine-independent (rank {sourceRank}/4, normalized volume {sourceVolume:G8}, taught minimum {input.MinimumNormalizedTetrahedronVolume:G8}).");
            }
            if (referenceRank < RequiredPairCount || referenceVolume <= input.MinimumNormalizedTetrahedronVolume)
            {
                throw new InvalidDataException($"Reference landmark tetrahedron is not affine-independent (rank {referenceRank}/4, normalized volume {referenceVolume:G8}, taught minimum {input.MinimumNormalizedTetrahedronVolume:G8}).");
            }

            var first = anchors[0];
            var provenance = $"{input.StepId}:LandmarkCorrespondence:{C3DLandmarkCorrespondenceSet.ContractVersion}:pairs=ExactlyFour:source=CurrentPublishedCornerAnchor:reference={input.ReferenceProvenance}@{input.ReferenceRevision}";
            var output = C3DLandmarkCorrespondenceSet.Create(
                input.OutputEntityId, pairs, first.RootSourceEntityId, first.RootSourceSha256,
                first.Unit, first.FrameId, input.ReferenceFrameId, input.ReferenceUnit,
                input.ReferenceProvenance, input.ReferenceRevision,
                input.MinimumNormalizedTetrahedronVolume, sourceRank, referenceRank,
                sourceVolume, referenceVolume, provenance);
            stopwatch.Stop();
            return new C3DLandmarkCorrespondenceEvaluation(
                new ToolResult(
                    "Landmark Correspondence", ResultStatus.Pass,
                    "Completed - source/reference correspondence evidence only; no affine transform or acceptance rule evaluated.", stopwatch.Elapsed,
                    [
                        new Metric("Correspondence count", MetricKind.Count, RequiredPairCount, "count"),
                        new Metric("Source normalized tetrahedron volume", MetricKind.Number, sourceVolume, "ratio"),
                        new Metric("Reference normalized tetrahedron volume", MetricKind.Number, referenceVolume, "ratio")
                    ],
                    [new Overlay(input.OutputEntityId, OverlayKind.Point, "Published CornerAnchor correspondence set", SourceEntityId: first.RootSourceEntityId)]),
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
            return new C3DLandmarkCorrespondenceEvaluation(
                new ToolResult("Landmark Correspondence", ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []), null);
        }
    }

    private static void Validate(C3DLandmarkCorrespondenceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentNullException.ThrowIfNull(input.PublishedCornerAnchors);
        ArgumentNullException.ThrowIfNull(input.ReferenceLandmarks);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ReferenceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ReferenceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ReferenceProvenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ReferenceRevision);
        if (!double.IsFinite(input.MinimumNormalizedTetrahedronVolume)
            || input.MinimumNormalizedTetrahedronVolume <= 0d
            || input.MinimumNormalizedTetrahedronVolume >= 1d)
        {
            throw new InvalidDataException("MinimumNormalizedTetrahedronVolume must be an explicit finite number greater than zero and less than one.");
        }
        if (input.PublishedCornerAnchors.Count != RequiredPairCount || input.ReferenceLandmarks.Count != RequiredPairCount)
        {
            throw new InvalidDataException("Landmark Correspondence v1 requires exactly four published CornerAnchor/reference pairs.");
        }

        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceCoordinates = new HashSet<(double X, double Y, double Z)>();
        var referenceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenceCoordinates = new HashSet<(double X, double Y, double Z)>();
        var first = input.PublishedCornerAnchors[0] ?? throw new InvalidDataException("Published CornerAnchor 1 is required.");
        foreach (var anchor in input.PublishedCornerAnchors)
        {
            if (anchor is null) throw new InvalidDataException("Each Published CornerAnchor is required.");
            if (!sourceIds.Add(anchor.OutputEntityId)
                || !sourceHashes.Add(anchor.ContentSha256)
                || !sourceRoles.Add(anchor.OutputRole)
                || !sourceCoordinates.Add((anchor.CornerAnchorX, anchor.CornerAnchorY, anchor.CornerAnchorZ)))
            {
                throw new InvalidDataException("Landmark Correspondence requires four distinct published CornerAnchor identities, roles, and coordinates.");
            }
            if (!Same(anchor.RootSourceEntityId, first.RootSourceEntityId)
                || !Same(anchor.RootSourceSha256, first.RootSourceSha256)
                || !Same(anchor.Unit, first.Unit)
                || !Same(anchor.FrameId, first.FrameId)
                || !Same(anchor.CoordinateConvention, first.CoordinateConvention))
            {
                throw new InvalidDataException("Published CornerAnchor root source, unit, frame, or coordinate convention does not match.");
            }
            if (!Finite(anchor.CornerAnchorX, anchor.CornerAnchorY, anchor.CornerAnchorZ))
            {
                throw new InvalidDataException("Published CornerAnchor coordinate must be finite.");
            }
        }
        foreach (var reference in input.ReferenceLandmarks)
        {
            if (reference is null || string.IsNullOrWhiteSpace(reference.Id) || !Finite(reference.X, reference.Y, reference.Z))
            {
                throw new InvalidDataException("Each reference landmark requires an ID and finite XYZ coordinate.");
            }
            if (!referenceIds.Add(reference.Id) || !referenceCoordinates.Add((reference.X, reference.Y, reference.Z)))
            {
                throw new InvalidDataException("Landmark Correspondence requires four distinct reference landmark IDs and coordinates.");
            }
        }
        if (string.Equals(input.OutputEntityId, first.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Landmark Correspondence output ID must differ from all CornerAnchor input IDs.");
        }
    }

    private static int GetAugmentedRank(IReadOnlyList<Point> points)
    {
        var matrix = points.Select(point => new[] { point.X, point.Y, point.Z, 1d }).ToArray();
        var maximum = matrix.SelectMany(row => row).Select(Math.Abs).DefaultIfEmpty(0d).Max();
        var tolerance = Math.Max(1d, maximum) * RankRelativeTolerance;
        var rank = 0;
        for (var column = 0; column < 4 && rank < matrix.Length; column++)
        {
            var pivot = Enumerable.Range(rank, matrix.Length - rank)
                .Select(row => (Row: row, Absolute: Math.Abs(matrix[row][column])))
                .OrderByDescending(item => item.Absolute)
                .First();
            if (pivot.Absolute <= tolerance) continue;
            (matrix[rank], matrix[pivot.Row]) = (matrix[pivot.Row], matrix[rank]);
            var divisor = matrix[rank][column];
            for (var target = rank + 1; target < matrix.Length; target++)
            {
                var factor = matrix[target][column] / divisor;
                for (var entry = column; entry < 4; entry++) matrix[target][entry] -= factor * matrix[rank][entry];
            }
            rank++;
        }
        return rank;
    }

    private static double GetNormalizedTetrahedronVolume(IReadOnlyList<Point> points)
    {
        var a = Subtract(points[1], points[0]);
        var b = Subtract(points[2], points[0]);
        var c = Subtract(points[3], points[0]);
        var volume6 = Math.Abs(Dot(a, Cross(b, c)));
        var span = 0d;
        for (var first = 0; first < points.Count; first++)
        {
            for (var second = first + 1; second < points.Count; second++)
            {
                span = Math.Max(span, Length(Subtract(points[second], points[first])));
            }
        }
        return span <= 0d || !double.IsFinite(span) ? 0d : volume6 / (span * span * span);
    }

    private static bool Finite(params double[] values) => values.All(double.IsFinite);
    private static bool Same(string first, string second) => string.Equals(first, second, StringComparison.Ordinal);
    private static Point Subtract(Point left, Point right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    private static Point Cross(Point left, Point right) => new(left.Y * right.Z - left.Z * right.Y, left.Z * right.X - left.X * right.Z, left.X * right.Y - left.Y * right.X);
    private static double Dot(Point left, Point right) => left.X * right.X + left.Y * right.Y + left.Z * right.Z;
    private static double Length(Point point) => Math.Sqrt(Dot(point, point));
    private readonly record struct Point(double X, double Y, double Z);
}

