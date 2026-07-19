using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DLineFitInput(
    string StepId,
    C3DHeightDifferenceEdgePointSet PublishedEdgePointSet,
    string OutputEntityId,
    double MaximumOrthogonalResidual,
    int MinimumInlierCount,
    double MinimumInlierRatio,
    int MinimumInlierScanlineSpan);

public sealed record C3DLineFitEvaluation(ToolResult Result, C3DLineFeature? Output);

public static class C3DLineFitRule
{
    private const int MaximumHypotheses = 256;
    private const int MaximumRefinementIterations = 10;
    private const double DirectionEpsilon = 1e-10;

    public static C3DLineFitEvaluation Evaluate(C3DLineFitInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            var points = input.PublishedEdgePointSet.Points;
            var pairs = CreatePairs(input.PublishedEdgePointSet.ContentSha256, points, cancellationToken);
            var winner = FindBestCandidate(points, pairs, input, cancellationToken)
                ?? throw new InvalidDataException("No non-degenerate Line Fit hypothesis satisfies the taught inlier support gates.");

            var membership = winner.Inliers;
            FittedLine fitted = default;
            var refinementIterations = 0;
            for (var iteration = 1; iteration <= MaximumRefinementIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                fitted = FitTls(points, membership, input.PublishedEdgePointSet.ComparisonAxis);
                var reclassified = Classify(points, fitted, input.MaximumOrthogonalResidual, cancellationToken);
                RequireSupport(points, reclassified, input, fitted);
                refinementIterations = iteration;
                if (SameMembership(membership, reclassified))
                {
                    membership = reclassified;
                    break;
                }

                membership = reclassified;
                if (iteration == MaximumRefinementIterations)
                {
                    throw new InvalidDataException("Line Fit TLS refinement did not stabilize within the fixed limit of 10 iterations.");
                }
            }

            fitted = FitTls(points, membership, input.PublishedEdgePointSet.ComparisonAxis);
            var finalMembership = Classify(points, fitted, input.MaximumOrthogonalResidual, cancellationToken);
            RequireSupport(points, finalMembership, input, fitted);
            if (!SameMembership(membership, finalMembership))
            {
                throw new InvalidDataException("Line Fit TLS refinement did not stabilize within the fixed limit of 10 iterations.");
            }

            var pointDiagnostics = CreateDiagnostics(points, fitted, finalMembership, cancellationToken, out var minimumProjection, out var maximumProjection);
            var inlierDiagnostics = pointDiagnostics.Where(point => point.IsInlier).ToArray();
            var residuals = inlierDiagnostics.Select(point => point.OrthogonalResidual).OrderBy(value => value).ToArray();
            var segmentStart = fitted.At(minimumProjection);
            var segmentEnd = fitted.At(maximumProjection);
            RequireFinite(segmentStart, "Line Fit segment start");
            RequireFinite(segmentEnd, "Line Fit segment end");
            var scanlines = inlierDiagnostics.Select(point => point.ScanlineIndex).ToArray();
            var diagnostics = new C3DLineFeatureDiagnostics(
                points.Count,
                inlierDiagnostics.Length,
                points.Count - inlierDiagnostics.Length,
                (double)inlierDiagnostics.Length / points.Count,
                scanlines.Min(),
                scanlines.Max(),
                scanlines.Max() - scanlines.Min(),
                Math.Sqrt(inlierDiagnostics.Average(point => point.OrthogonalResidual * point.OrthogonalResidual)),
                residuals[^1],
                Median(residuals),
                maximumProjection - minimumProjection,
                pairs.Count,
                refinementIterations);
            var provenance = $"{input.StepId}:LineFit:{C3DLineFeature.ContractVersion}:method=DeterministicConsensusOrthogonalTls:hypotheses=Sha256PairSchedule/{MaximumHypotheses}:refinement=OrthogonalTlsUntilStable10:direction=PositiveScanlineAxis:endpoints=InlierProjectionExtents:input={input.PublishedEdgePointSet.ContentSha256}";
            var output = C3DLineFeature.Create(
                input.OutputEntityId,
                input.PublishedEdgePointSet,
                input.MaximumOrthogonalResidual,
                input.MinimumInlierCount,
                input.MinimumInlierRatio,
                input.MinimumInlierScanlineSpan,
                fitted.Anchor.X,
                fitted.Anchor.Y,
                fitted.Anchor.Z,
                fitted.Direction.X,
                fitted.Direction.Y,
                fitted.Direction.Z,
                segmentStart.X,
                segmentStart.Y,
                segmentStart.Z,
                segmentEnd.X,
                segmentEnd.Y,
                segmentEnd.Z,
                diagnostics,
                pointDiagnostics,
                provenance);
            stopwatch.Stop();
            return new C3DLineFitEvaluation(
                new ToolResult(
                    "3D Line Fit",
                    ResultStatus.Pass,
                    "Completed - feature extraction; no acceptance rule evaluated.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Input point count", MetricKind.Count, points.Count, "count"),
                        new Metric("Inlier count", MetricKind.Count, diagnostics.InlierCount, "count"),
                        new Metric("Inlier ratio", MetricKind.Deviation, diagnostics.InlierRatio, "ratio"),
                        new Metric("Residual RMS", MetricKind.Deviation, diagnostics.ResidualRms, "source-coordinate"),
                        new Metric("Residual maximum", MetricKind.Deviation, diagnostics.ResidualMaximum, "source-coordinate"),
                        new Metric("Inlier scanline span", MetricKind.Count, diagnostics.InlierScanlineSpan, "grid-index")
                    ],
                    [new Overlay(input.OutputEntityId, OverlayKind.Polyline, "Full-XYZ fitted line segment", SourceEntityId: input.PublishedEdgePointSet.OutputEntityId)]),
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
            return new C3DLineFitEvaluation(
                new ToolResult("3D Line Fit", ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }

    private static void Validate(C3DLineFitInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.PublishedEdgePointSet);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        var edge = input.PublishedEdgePointSet;
        if (string.Equals(input.OutputEntityId, edge.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Line Fit output must differ from its EdgePointSet input.");
        }
        if (edge.Points.Count < 3)
        {
            throw new InvalidDataException($"Line Fit requires at least three EdgePointSet points; received {edge.Points.Count}.");
        }
        if (!double.IsFinite(input.MaximumOrthogonalResidual) || input.MaximumOrthogonalResidual <= 0)
        {
            throw new InvalidDataException("MaximumOrthogonalResidual must be an explicit finite number greater than zero.");
        }
        if (input.MinimumInlierCount < 3 || input.MinimumInlierCount > edge.Points.Count)
        {
            throw new InvalidDataException($"MinimumInlierCount must be an integer from 3 through {edge.Points.Count}.");
        }
        if (!double.IsFinite(input.MinimumInlierRatio) || input.MinimumInlierRatio <= 0 || input.MinimumInlierRatio > 1)
        {
            throw new InvalidDataException("MinimumInlierRatio must be an explicit finite number greater than zero and no greater than one.");
        }
        if (input.MinimumInlierScanlineSpan < 2)
        {
            throw new InvalidDataException("MinimumInlierScanlineSpan must be an integer of at least two grid-index intervals.");
        }
        if (edge.Points.Max(point => point.ScanlineIndex) - edge.Points.Min(point => point.ScanlineIndex) < input.MinimumInlierScanlineSpan)
        {
            throw new InvalidDataException("MinimumInlierScanlineSpan cannot be reached by the available EdgePointSet points.");
        }
        var previousScanline = int.MinValue;
        foreach (var point in edge.Points)
        {
            if (point.ScanlineIndex <= previousScanline)
            {
                throw new InvalidDataException("Line Fit requires finite EdgePointSet points ordered by unique ascending ScanlineIndex.");
            }
            previousScanline = point.ScanlineIndex;
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
            {
                throw new InvalidDataException("Line Fit rejects non-finite EdgePointSet coordinates.");
            }
        }
    }

    private static List<Pair> CreatePairs(string inputHash, IReadOnlyList<C3DHeightDifferenceEdgePoint> points, CancellationToken cancellationToken)
    {
        var pairCount = points.Count * (points.Count - 1) / 2;
        if (pairCount <= MaximumHypotheses)
        {
            var all = new List<Pair>(pairCount);
            for (var first = 0; first < points.Count - 1; first++)
            {
                for (var second = first + 1; second < points.Count; second++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsDegeneratePair(points[first], points[second])) all.Add(new Pair(first, second));
                }
            }
            return all;
        }

        var pairs = new List<Pair>(MaximumHypotheses);
        var unique = new HashSet<Pair>();
        for (var attempt = 0; pairs.Count < MaximumHypotheses && attempt < points.Count * points.Count * 32; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{inputHash.ToUpperInvariant()}|{attempt}"));
            // The byte order is explicit so the SHA-256 schedule is stable on every supported runtime.
            var first = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0, sizeof(uint))) % (uint)points.Count);
            var second = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(sizeof(uint), sizeof(uint))) % (uint)points.Count);
            if (first == second) continue;
            if (first > second) (first, second) = (second, first);
            var pair = new Pair(first, second);
            if (unique.Add(pair) && !IsDegeneratePair(points[first], points[second])) pairs.Add(pair);
        }
        return pairs;
    }

    private static Candidate? FindBestCandidate(
        IReadOnlyList<C3DHeightDifferenceEdgePoint> points,
        IReadOnlyList<Pair> pairs,
        C3DLineFitInput input,
        CancellationToken cancellationToken)
    {
        Candidate? best = null;
        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = FittedLine.FromPair(points[pair.First], points[pair.Second]);
            var membership = Classify(points, line, input.MaximumOrthogonalResidual, cancellationToken);
            if (!HasSupport(points, membership, input, line, out var count, out var rms, out var span)) continue;
            var candidate = new Candidate(pair, membership, count, rms, span);
            if (best is null || candidate.IsBetterThan(best)) best = candidate;
        }
        return best;
    }

    private static bool[] Classify(
        IReadOnlyList<C3DHeightDifferenceEdgePoint> points,
        FittedLine line,
        double maximumResidual,
        CancellationToken cancellationToken)
    {
        var membership = new bool[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var residual = line.Residual(points[index]);
            if (!double.IsFinite(residual)) throw new InvalidDataException("Line Fit produced a non-finite orthogonal residual.");
            membership[index] = residual <= maximumResidual;
        }
        return membership;
    }

    private static void RequireSupport(
        IReadOnlyList<C3DHeightDifferenceEdgePoint> points,
        bool[] membership,
        C3DLineFitInput input,
        FittedLine line)
    {
        if (!HasSupport(points, membership, input, line, out _, out _, out _))
        {
            throw new InvalidDataException("Line Fit final inliers do not satisfy the taught count, ratio, and scanline-span support gates.");
        }
    }

    private static bool HasSupport(
        IReadOnlyList<C3DHeightDifferenceEdgePoint> points,
        bool[] membership,
        C3DLineFitInput input,
        FittedLine line,
        out int count,
        out double rms,
        out int span)
    {
        var inliers = Enumerable.Range(0, points.Count).Where(index => membership[index]).ToArray();
        count = inliers.Length;
        if (count == 0)
        {
            rms = double.PositiveInfinity;
            span = 0;
            return false;
        }
        var first = inliers.Min(index => points[index].ScanlineIndex);
        var last = inliers.Max(index => points[index].ScanlineIndex);
        span = last - first;
        rms = Math.Sqrt(inliers.Average(index => Math.Pow(line.Residual(points[index]), 2)));
        if (!double.IsFinite(rms))
        {
            throw new InvalidDataException("Line Fit candidate residual RMS is non-finite.");
        }
        return count >= input.MinimumInlierCount
            && (double)count / points.Count >= input.MinimumInlierRatio
            && span >= input.MinimumInlierScanlineSpan;
    }

    private static FittedLine FitTls(IReadOnlyList<C3DHeightDifferenceEdgePoint> points, bool[] membership, C3DHeightDifferenceComparisonAxis axis)
    {
        var inliers = Enumerable.Range(0, points.Count).Where(index => membership[index]).ToArray();
        if (inliers.Length < 3) throw new InvalidDataException("Line Fit TLS requires at least three inliers.");
        var anchor = new Vector3d(
            inliers.Average(index => points[index].X),
            inliers.Average(index => points[index].Y),
            inliers.Average(index => points[index].Z));
        var covariance = new double[3, 3];
        foreach (var index in inliers)
        {
            var delta = new Vector3d(points[index].X - anchor.X, points[index].Y - anchor.Y, points[index].Z - anchor.Z);
            covariance[0, 0] += delta.X * delta.X; covariance[0, 1] += delta.X * delta.Y; covariance[0, 2] += delta.X * delta.Z;
            covariance[1, 1] += delta.Y * delta.Y; covariance[1, 2] += delta.Y * delta.Z;
            covariance[2, 2] += delta.Z * delta.Z;
        }
        covariance[1, 0] = covariance[0, 1]; covariance[2, 0] = covariance[0, 2]; covariance[2, 1] = covariance[1, 2];
        var eigen = SymmetricEigen.Decompose(covariance);
        if (!double.IsFinite(eigen.Values[0]) || eigen.Values[0] <= DirectionEpsilon || eigen.Values[0] - eigen.Values[1] <= Math.Max(1, eigen.Values[0]) * 1e-12)
        {
            throw new InvalidDataException("Line Fit TLS covariance is degenerate and has no stable dominant direction.");
        }
        var direction = eigen.Vectors[0].Normalize();
        var requiredComponent = axis == C3DHeightDifferenceComparisonAxis.AcrossColumns ? direction.Z : direction.X;
        if (!double.IsFinite(requiredComponent) || Math.Abs(requiredComponent) <= DirectionEpsilon)
        {
            throw new InvalidDataException("Line Fit direction does not advance along the required source scanline axis.");
        }
        if (requiredComponent < 0) direction = direction.Negate();
        return new FittedLine(anchor, direction);
    }

    private static C3DLineFeaturePointDiagnostic[] CreateDiagnostics(
        IReadOnlyList<C3DHeightDifferenceEdgePoint> points,
        FittedLine line,
        bool[] membership,
        CancellationToken cancellationToken,
        out double minimumProjection,
        out double maximumProjection)
    {
        minimumProjection = double.PositiveInfinity;
        maximumProjection = double.NegativeInfinity;
        var diagnostics = new C3DLineFeaturePointDiagnostic[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var point = points[index];
            var projection = line.Project(point);
            var residual = line.Residual(point);
            RequireFinite(projection, "Line Fit projection");
            if (!double.IsFinite(residual)) throw new InvalidDataException("Line Fit produced a non-finite final residual.");
            var scalar = line.ProjectionScalar(point);
            if (membership[index])
            {
                minimumProjection = Math.Min(minimumProjection, scalar);
                maximumProjection = Math.Max(maximumProjection, scalar);
            }
            diagnostics[index] = new C3DLineFeaturePointDiagnostic(index, point.ScanlineIndex, point.X, point.Y, point.Z, projection.X, projection.Y, projection.Z, residual, membership[index]);
        }
        if (!double.IsFinite(minimumProjection) || !double.IsFinite(maximumProjection) || maximumProjection < minimumProjection)
        {
            throw new InvalidDataException("Line Fit could not determine finite inlier projection extents.");
        }
        return diagnostics;
    }

    private static double Median(double[] sorted) => sorted.Length % 2 == 1
        ? sorted[sorted.Length / 2]
        : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;

    private static bool SameMembership(bool[] first, bool[] second) => first.Length == second.Length && first.AsSpan().SequenceEqual(second);
    private static bool IsDegeneratePair(C3DHeightDifferenceEdgePoint first, C3DHeightDifferenceEdgePoint second) =>
        (first.X - second.X) * (first.X - second.X) + (first.Y - second.Y) * (first.Y - second.Y) + (first.Z - second.Z) * (first.Z - second.Z) <= DirectionEpsilon * DirectionEpsilon;
    private static void RequireFinite(Vector3d value, string label)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || !double.IsFinite(value.Z)) throw new InvalidDataException($"{label} is non-finite.");
    }

    private sealed record Pair(int First, int Second);
    private sealed record Candidate(Pair Pair, bool[] Inliers, int Count, double Rms, int Span)
    {
        public bool IsBetterThan(Candidate other) =>
            Count != other.Count ? Count > other.Count :
            Rms != other.Rms ? Rms < other.Rms :
            Span != other.Span ? Span > other.Span :
            Pair.First != other.Pair.First ? Pair.First < other.Pair.First : Pair.Second < other.Pair.Second;
    }

    private readonly record struct Vector3d(double X, double Y, double Z)
    {
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public Vector3d Normalize()
        {
            var length = Length;
            if (!double.IsFinite(length) || length <= DirectionEpsilon) throw new InvalidDataException("Line Fit direction length is degenerate.");
            return new Vector3d(X / length, Y / length, Z / length);
        }
        public Vector3d Negate() => new(-X, -Y, -Z);
    }

    private readonly record struct FittedLine(Vector3d Anchor, Vector3d Direction)
    {
        public static FittedLine FromPair(C3DHeightDifferenceEdgePoint first, C3DHeightDifferenceEdgePoint second) => new(
            new Vector3d(first.X, first.Y, first.Z),
            new Vector3d(second.X - first.X, second.Y - first.Y, second.Z - first.Z).Normalize());
        public double ProjectionScalar(C3DHeightDifferenceEdgePoint point) =>
            (point.X - Anchor.X) * Direction.X + (point.Y - Anchor.Y) * Direction.Y + (point.Z - Anchor.Z) * Direction.Z;
        public Vector3d Project(C3DHeightDifferenceEdgePoint point) => At(ProjectionScalar(point));
        public Vector3d At(double scalar) => new(Anchor.X + scalar * Direction.X, Anchor.Y + scalar * Direction.Y, Anchor.Z + scalar * Direction.Z);
        public double Residual(C3DHeightDifferenceEdgePoint point)
        {
            var projection = Project(point);
            var dx = point.X - projection.X; var dy = point.Y - projection.Y; var dz = point.Z - projection.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    private static class SymmetricEigen
    {
        public static (double[] Values, Vector3d[] Vectors) Decompose(double[,] source)
        {
            var matrix = (double[,])source.Clone();
            var vectors = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            for (var iteration = 0; iteration < 32; iteration++)
            {
                var (p, q) = LargestOffDiagonal(matrix);
                if (Math.Abs(matrix[p, q]) <= 1e-14) break;
                var angle = 0.5 * Math.Atan2(2 * matrix[p, q], matrix[q, q] - matrix[p, p]);
                var cosine = Math.Cos(angle); var sine = Math.Sin(angle);
                for (var index = 0; index < 3; index++)
                {
                    var mp = matrix[index, p]; var mq = matrix[index, q];
                    matrix[index, p] = cosine * mp - sine * mq;
                    matrix[index, q] = sine * mp + cosine * mq;
                }
                for (var index = 0; index < 3; index++)
                {
                    var mp = matrix[p, index]; var mq = matrix[q, index];
                    matrix[p, index] = cosine * mp - sine * mq;
                    matrix[q, index] = sine * mp + cosine * mq;
                }
                for (var index = 0; index < 3; index++)
                {
                    var vp = vectors[index, p]; var vq = vectors[index, q];
                    vectors[index, p] = cosine * vp - sine * vq;
                    vectors[index, q] = sine * vp + cosine * vq;
                }
            }
            var result = Enumerable.Range(0, 3)
                .Select(index => (Value: matrix[index, index], Vector: new Vector3d(vectors[0, index], vectors[1, index], vectors[2, index]).Normalize(), Index: index))
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Index)
                .ToArray();
            return (result.Select(item => item.Value).ToArray(), result.Select(item => item.Vector).ToArray());
        }

        private static (int P, int Q) LargestOffDiagonal(double[,] matrix)
        {
            var result = (P: 0, Q: 1);
            var largest = Math.Abs(matrix[0, 1]);
            foreach (var candidate in new[] { (0, 2), (1, 2) })
            {
                var value = Math.Abs(matrix[candidate.Item1, candidate.Item2]);
                if (value > largest) { largest = value; result = candidate; }
            }
            return result;
        }
    }
}
