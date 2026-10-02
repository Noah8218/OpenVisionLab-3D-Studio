using System.Diagnostics;
using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DPhysicalEdgeGapFlushEvaluation(
    ToolResult Result,
    C3DPhysicalEdgeGapFlushResult? Output);

/// <summary>
/// Evaluates the approved physical-edge Gap/Flush contract from already
/// published EdgePointSet and LineFeature evidence. The rule owns only pairing,
/// support aggregation, validity, and tolerance policy; edge extraction and
/// line fitting remain with their existing owners.
/// </summary>
public static class PhysicalEdgeGapFlushRule
{
    public const string ToolName = "Physical Edge Gap / Flush";

    public static C3DPhysicalEdgeGapFlushEvaluation Evaluate(
        C3DPhysicalEdgeGapFlushInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(input.Profile);
            ArgumentNullException.ThrowIfNull(input.FirstLine);
            ArgumentNullException.ThrowIfNull(input.SecondLine);
            ArgumentNullException.ThrowIfNull(input.FirstEdge);
            ArgumentNullException.ThrowIfNull(input.SecondEdge);
            ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
            ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
            cancellationToken.ThrowIfCancellationRequested();

            var structuralErrors = ValidateLineage(input);
            var profile = input.Profile;
            var signedGap = structuralErrors.Count == 0
                ? SignedGap(input.FirstLine, input.SecondLine, profile.ComparisonAxis)
                : double.NaN;
            var firstPoints = structuralErrors.Count == 0
                ? GetInlierSurfacePoints(input.FirstLine, input.FirstEdge)
                : new Dictionary<int, C3DHeightDifferenceEdgePoint>();
            var secondPoints = structuralErrors.Count == 0
                ? GetInlierSurfacePoints(input.SecondLine, input.SecondEdge)
                : new Dictionary<int, C3DHeightDifferenceEdgePoint>();
            var paired = structuralErrors.Count == 0
                ? PairSurfacePoints(firstPoints, secondPoints, profile)
                : new PairSupport(double.NaN, double.NaN, 0);
            cancellationToken.ThrowIfCancellationRequested();

            var supportFailure = string.Empty;
            var state = C3DPhysicalEdgeGapFlushMeasurementState.Valid;
            if (structuralErrors.Count > 0)
            {
                state = C3DPhysicalEdgeGapFlushMeasurementState.Invalid;
                supportFailure = string.Join(" ", structuralErrors);
            }
            else if (IsDuplicateEvidence(input))
            {
                state = C3DPhysicalEdgeGapFlushMeasurementState.Rejected;
                supportFailure = "The two authored roles reuse the same edge evidence; distinct support is required.";
            }
            else if (!double.IsFinite(paired.FirstSupport) || !double.IsFinite(paired.SecondSupport))
            {
                state = C3DPhysicalEdgeGapFlushMeasurementState.Limited;
                supportFailure = "No finite paired surface support is available for the authored roles.";
            }
            else if (Math.Abs(signedGap) <= profile.AmbiguityEpsilon
                && !(profile.ContactAllowed && signedGap == 0d))
            {
                state = C3DPhysicalEdgeGapFlushMeasurementState.Rejected;
                supportFailure = profile.ContactAllowed
                    ? "Near-tied edge coordinates are rejected unless they are an exact zero-gap contact."
                    : "Near-tied or zero-gap edge coordinates are rejected because ContactAllowed is disabled.";
            }
            else if (!PassesSupportGates(input, paired.ValidPairCount, paired.CoverageRatio, out supportFailure))
            {
                state = C3DPhysicalEdgeGapFlushMeasurementState.Limited;
            }
            else if (!input.FirstEdge.CandidateEvidence.HasNumericTopAndSecond
                || !input.SecondEdge.CandidateEvidence.HasNumericTopAndSecond)
            {
                state = C3DPhysicalEdgeGapFlushMeasurementState.Limited;
                supportFailure = "Candidate ambiguity cannot be evaluated: the edge extractor retains only the strongest candidate per scanline. Gap and flush are provisional; tolerance judgement was not performed.";
            }

            var signedFlush = double.IsFinite(paired.FirstSupport) && double.IsFinite(paired.SecondSupport)
                ? paired.SecondSupport - paired.FirstSupport
                : double.NaN;
            var toleranceState = profile.ToleranceEnabled
                ? C3DPhysicalEdgeGapFlushToleranceState.NotEvaluated
                : C3DPhysicalEdgeGapFlushToleranceState.NotConfigured;
            if (state == C3DPhysicalEdgeGapFlushMeasurementState.Valid && profile.ToleranceEnabled)
            {
                toleranceState = IsWithinInclusiveTolerance(signedGap, profile.ExpectedGap, profile.GapTolerance)
                    && IsWithinInclusiveTolerance(signedFlush, profile.ExpectedFlush, profile.FlushTolerance)
                    ? C3DPhysicalEdgeGapFlushToleranceState.Pass
                    : C3DPhysicalEdgeGapFlushToleranceState.Fail;
            }

            var topCandidateEvidence =
                $"first={input.FirstEdge.CandidateEvidence.Summary};second={input.SecondEdge.CandidateEvidence.Summary}";
            var secondCandidateEvidence = input.FirstEdge.CandidateEvidence.HasNumericTopAndSecond
                || input.SecondEdge.CandidateEvidence.HasNumericTopAndSecond
                ? "Numeric top/second candidate evidence is available and must be evaluated by the approved ambiguity policy."
                : "Numeric top/second candidate evidence is unavailable in the upstream EdgePointSet contract; candidate ambiguity is unevaluated and tolerance judgement is withheld.";
            var provenance =
                $"{input.StepId}:PhysicalEdgeGapFlush:{C3DPhysicalEdgeGapFlushResult.ContractVersion}:axis={profile.ComparisonAxis}:gap=SecondMinusFirst:flush=SecondSurfaceMinusFirstSurface:surface={profile.SurfaceAggregation}:contactAllowed={profile.ContactAllowed}:ambiguity={profile.AmbiguityPolicy}:candidateEvidence={topCandidateEvidence}:firstPolarity={input.FirstEdge.Polarity}:secondPolarity={input.SecondEdge.Polarity}";
            var output = C3DPhysicalEdgeGapFlushResult.Create(
                input.OutputEntityId,
                input,
                signedGap,
                signedFlush,
                paired.FirstSupport,
                paired.SecondSupport,
                paired.ValidPairCount,
                paired.ValidPairCount,
                paired.ValidPairCount,
                paired.CoverageRatio,
                state,
                toleranceState,
                topCandidateEvidence,
                secondCandidateEvidence,
                supportFailure,
                provenance);

            stopwatch.Stop();
            var resultStatus = ToResultStatus(state, toleranceState);
            var metrics = CreateMetrics(output, resultStatus);
            var overlays =
                new[]
                {
                    new Overlay($"{output.OutputEntityId}:first-line", OverlayKind.Polyline, "Physical Gap / Flush first fitted edge", SourceEntityId: output.FirstLineEntityId),
                    new Overlay($"{output.OutputEntityId}:second-line", OverlayKind.Polyline, "Physical Gap / Flush second fitted edge", SourceEntityId: output.SecondLineEntityId),
                    new Overlay($"{output.OutputEntityId}:support", OverlayKind.Marker, "Physical Gap / Flush paired surface support", SourceEntityId: output.RootSourceEntityId)
                };
            return new C3DPhysicalEdgeGapFlushEvaluation(
                new ToolResult(ToolName, resultStatus, output.EvidenceSummary, stopwatch.Elapsed, metrics, overlays),
                output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            stopwatch.Stop();
            return new C3DPhysicalEdgeGapFlushEvaluation(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }

    private static List<string> ValidateLineage(C3DPhysicalEdgeGapFlushInput input)
    {
        var errors = new List<string>();
        var firstLine = input.FirstLine;
        var secondLine = input.SecondLine;
        var firstEdge = input.FirstEdge;
        var secondEdge = input.SecondEdge;
        var profile = input.Profile;
        if (string.Equals(input.OutputEntityId, firstLine.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, secondLine.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Physical Edge Gap / Flush output ID must differ from both LineFeature inputs.");
        }
        if (!string.Equals(firstLine.InputEdgePointSetEntityId, firstEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(secondLine.InputEdgePointSetEntityId, secondEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Each LineFeature must reference the exact authored EdgePointSet input.");
        }
        if (!string.Equals(firstLine.InputContentSha256, firstEdge.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(secondLine.InputContentSha256, secondEdge.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("LineFeature and EdgePointSet content hashes do not match their authored lineage.");
        }
        if (!string.Equals(firstLine.RootSourceEntityId, secondLine.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(firstLine.RootSourceSha256, secondLine.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(firstLine.RootSourceEntityId, firstEdge.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(secondLine.RootSourceEntityId, secondEdge.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(firstEdge.RootSourceEntityId, secondEdge.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(firstLine.RootSourceSha256, firstEdge.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(secondLine.RootSourceSha256, secondEdge.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(firstEdge.RootSourceSha256, secondEdge.RootSourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Both edges must share one root source ID and SHA-256.");
        }
        if (!string.Equals(firstLine.FrameId, secondLine.FrameId, StringComparison.Ordinal)
            || !string.Equals(firstLine.FrameId, firstEdge.FrameId, StringComparison.Ordinal)
            || !string.Equals(secondLine.FrameId, secondEdge.FrameId, StringComparison.Ordinal)
            || !string.Equals(firstLine.Unit, secondLine.Unit, StringComparison.Ordinal)
            || !string.Equals(firstLine.Unit, firstEdge.Unit, StringComparison.Ordinal)
            || !string.Equals(secondLine.Unit, secondEdge.Unit, StringComparison.Ordinal))
        {
            errors.Add("Both edges must share one frame and declared source unit.");
        }
        if (firstLine.InputComparisonAxis != profile.ComparisonAxis
            || secondLine.InputComparisonAxis != profile.ComparisonAxis
            || firstEdge.ComparisonAxis != profile.ComparisonAxis
            || secondEdge.ComparisonAxis != profile.ComparisonAxis)
        {
            errors.Add("Both edge evidences must use the authored source-grid comparison axis.");
        }
        if (string.IsNullOrWhiteSpace(profile.SectionId))
        {
            errors.Add("SectionId is required for a physical-edge measurement.");
        }
        if (!Enum.IsDefined(profile.FirstSurfaceSide)
            || !Enum.IsDefined(profile.SecondSurfaceSide)
            || !Enum.IsDefined(profile.SurfaceAggregation)
            || !Enum.IsDefined(profile.AmbiguityPolicy))
        {
            errors.Add("Physical Edge Gap / Flush profile contains an unsupported policy value.");
        }

        return errors;
    }

    private static bool IsDuplicateEvidence(C3DPhysicalEdgeGapFlushInput input) =>
        string.Equals(input.FirstLine.OutputEntityId, input.SecondLine.OutputEntityId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(input.FirstLine.ContentSha256, input.SecondLine.ContentSha256, StringComparison.OrdinalIgnoreCase)
        || string.Equals(input.FirstEdge.OutputEntityId, input.SecondEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(input.FirstEdge.ContentSha256, input.SecondEdge.ContentSha256, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<int, C3DHeightDifferenceEdgePoint> GetInlierSurfacePoints(
        C3DLineFeature line,
        C3DHeightDifferenceEdgePointSet edge)
    {
        var points = new Dictionary<int, C3DHeightDifferenceEdgePoint>();
        foreach (var diagnostic in line.PointDiagnostics
                     .Where(point => point.IsInlier)
                     .OrderBy(point => point.InputPointIndex))
        {
            if (diagnostic.InputPointIndex < 0 || diagnostic.InputPointIndex >= edge.Points.Count)
            {
                continue;
            }

            var point = edge.Points[diagnostic.InputPointIndex];
            if (point.ScanlineIndex != diagnostic.ScanlineIndex)
            {
                continue;
            }

            points.TryAdd(point.ScanlineIndex, point);
        }

        return points;
    }

    private static PairSupport PairSurfacePoints(
        IReadOnlyDictionary<int, C3DHeightDifferenceEdgePoint> firstPoints,
        IReadOnlyDictionary<int, C3DHeightDifferenceEdgePoint> secondPoints,
        C3DPhysicalEdgeGapFlushProfile profile)
    {
        var firstValues = new List<double>();
        var secondValues = new List<double>();
        foreach (var scanline in firstPoints.Keys.Intersect(secondPoints.Keys).OrderBy(index => index))
        {
            var first = SurfaceValue(firstPoints[scanline], profile.FirstSurfaceSide);
            var second = SurfaceValue(secondPoints[scanline], profile.SecondSurfaceSide);
            if (!double.IsFinite(first) || !double.IsFinite(second))
            {
                continue;
            }

            firstValues.Add(first);
            secondValues.Add(second);
        }

        var count = firstValues.Count;
        var denominator = Math.Max(1, Math.Max(firstPoints.Count, secondPoints.Count));
        return count == 0
            ? new PairSupport(double.NaN, double.NaN, 0, 0d)
            : new PairSupport(firstValues.Average(), secondValues.Average(), count, (double)count / denominator);
    }

    private static double SurfaceValue(C3DHeightDifferenceEdgePoint point, C3DPhysicalEdgeSurfaceSide side) =>
        side == C3DPhysicalEdgeSurfaceSide.First ? point.FirstHeight : point.SecondHeight;

    private static bool PassesSupportGates(
        C3DPhysicalEdgeGapFlushInput input,
        int validPairCount,
        double coverageRatio,
        out string message)
    {
        var profile = input.Profile;
        var failures = new List<string>();
        if (validPairCount < profile.MinimumValidPairCount)
        {
            failures.Add($"valid paired support {validPairCount} is below minimum {profile.MinimumValidPairCount}");
        }
        if (!double.IsFinite(coverageRatio) || coverageRatio < profile.MinimumCoverageRatio)
        {
            failures.Add($"coverage {Format(coverageRatio)} is below minimum {Format(profile.MinimumCoverageRatio)}");
        }
        if (input.FirstLine.Diagnostics.InlierRatio < profile.MinimumInlierRatio
            || input.SecondLine.Diagnostics.InlierRatio < profile.MinimumInlierRatio)
        {
            failures.Add("one or both fitted lines are below the minimum inlier ratio");
        }
        if (input.FirstLine.Diagnostics.InlierScanlineSpan < profile.MinimumInlierScanlineSpan
            || input.SecondLine.Diagnostics.InlierScanlineSpan < profile.MinimumInlierScanlineSpan)
        {
            failures.Add("one or both fitted lines are below the minimum inlier scanline span");
        }
        if (!double.IsFinite(input.FirstLine.Diagnostics.ResidualMaximum)
            || !double.IsFinite(input.SecondLine.Diagnostics.ResidualMaximum)
            || input.FirstLine.Diagnostics.ResidualMaximum > profile.MaximumResidual
            || input.SecondLine.Diagnostics.ResidualMaximum > profile.MaximumResidual)
        {
            failures.Add("one or both fitted lines exceed the maximum residual gate");
        }

        message = failures.Count == 0
            ? string.Empty
            : string.Join("; ", failures);
        return failures.Count == 0;
    }

    private static double SignedGap(
        C3DLineFeature first,
        C3DLineFeature second,
        C3DHeightDifferenceComparisonAxis axis) =>
        axis == C3DHeightDifferenceComparisonAxis.AcrossColumns
            ? second.AnchorX - first.AnchorX
            : second.AnchorZ - first.AnchorZ;

    private static bool IsWithinInclusiveTolerance(double actual, double expected, double tolerance) =>
        double.IsFinite(actual)
        && double.IsFinite(expected)
        && double.IsFinite(tolerance)
        && tolerance >= 0d
        && Math.Abs(actual - expected) <= tolerance;

    private static ResultStatus ToResultStatus(
        C3DPhysicalEdgeGapFlushMeasurementState state,
        C3DPhysicalEdgeGapFlushToleranceState toleranceState) =>
        state switch
        {
            C3DPhysicalEdgeGapFlushMeasurementState.Invalid => ResultStatus.Error,
            C3DPhysicalEdgeGapFlushMeasurementState.Rejected => ResultStatus.Warning,
            C3DPhysicalEdgeGapFlushMeasurementState.Limited => ResultStatus.Warning,
            _ => toleranceState switch
            {
                C3DPhysicalEdgeGapFlushToleranceState.Fail => ResultStatus.Fail,
                C3DPhysicalEdgeGapFlushToleranceState.Pass => ResultStatus.Pass,
                _ => ResultStatus.Warning
            }
        };

    private static IReadOnlyList<Metric> CreateMetrics(
        C3DPhysicalEdgeGapFlushResult output,
        ResultStatus status) =>
    [
        new("Signed gap", MetricKind.Length, output.SignedGap, output.Unit, status),
        new("Gap magnitude", MetricKind.Length, output.GapMagnitude, output.Unit, status),
        new("Signed flush", MetricKind.Length, output.SignedFlush, output.Unit, status),
        new("Flush magnitude", MetricKind.Length, output.FlushMagnitude, output.Unit, status),
        new("First surface support", MetricKind.Length, output.FirstSurfaceSupport, output.Unit, status),
        new("Second surface support", MetricKind.Length, output.SecondSurfaceSupport, output.Unit, status),
        new("First surface support count", MetricKind.Count, output.FirstSurfaceSupportCount, "count", status),
        new("Second surface support count", MetricKind.Count, output.SecondSurfaceSupportCount, "count", status),
        new("Valid paired support", MetricKind.Count, output.ValidPairCount, "count", status),
        new("Support coverage", MetricKind.Deviation, output.CoverageRatio, "ratio", status),
        new("First line inlier ratio", MetricKind.Deviation, output.FirstInlierRatio, "ratio", status),
        new("Second line inlier ratio", MetricKind.Deviation, output.SecondInlierRatio, "ratio", status),
        new("First line residual maximum", MetricKind.Deviation, output.FirstResidualMaximum, output.Unit, status),
        new("Second line residual maximum", MetricKind.Deviation, output.SecondResidualMaximum, output.Unit, status),
        new("Expected signed gap", MetricKind.Length, output.ExpectedGap, output.Unit),
        new("Gap tolerance", MetricKind.Length, output.GapTolerance, output.Unit),
        new("Expected signed flush", MetricKind.Length, output.ExpectedFlush, output.Unit),
        new("Flush tolerance", MetricKind.Length, output.FlushTolerance, output.Unit)
    ];

    private static string Format(double value) =>
        double.IsFinite(value) ? value.ToString("G17", CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);

    private sealed record PairSupport(
        double FirstSupport,
        double SecondSupport,
        int ValidPairCount,
        double CoverageRatio = 0d);
}
