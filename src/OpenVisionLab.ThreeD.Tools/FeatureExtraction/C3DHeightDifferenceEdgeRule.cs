using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DHeightDifferenceEdgeInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    string RootSourceEntityId,
    string SelectionId,
    ToolRecipeGridRectangle Selection,
    string OutputEntityId,
    C3DHeightDifferenceComparisonAxis ComparisonAxis,
    C3DHeightDifferencePolarity Polarity,
    double MinimumDelta);

public sealed record C3DHeightDifferenceEdgeEvaluation(
    ToolResult Result,
    C3DHeightDifferenceEdgePointSet? Output);

public static class C3DHeightDifferenceEdgeRule
{
    public static C3DHeightDifferenceEdgeEvaluation Evaluate(
        C3DHeightDifferenceEdgeInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            var rectangle = input.Selection;
            var scanlineCount = input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
                ? rectangle.RowCount
                : rectangle.ColumnCount;
            var pairCountPerScanline = input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
                ? rectangle.ColumnCount - 1
                : rectangle.RowCount - 1;
            var source = input.Source.Values.Span;
            var points = new List<C3DHeightDifferenceEdgePoint>(scanlineCount);
            long eligiblePairCount = 0;
            long skippedMissingPairCount = 0;

            for (var scanlineOffset = 0; scanlineOffset < scanlineCount; scanlineOffset++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Candidate? winner = null;
                for (var pairOffset = 0; pairOffset < pairCountPerScanline; pairOffset++)
                {
                    var firstRow = input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
                        ? rectangle.Row + scanlineOffset
                        : rectangle.Row + pairOffset;
                    var firstColumn = input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
                        ? rectangle.Column + pairOffset
                        : rectangle.Column + scanlineOffset;
                    var secondRow = firstRow + (input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossRows ? 1 : 0);
                    var secondColumn = firstColumn + (input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns ? 1 : 0);
                    var firstHeight = source[firstRow * input.Source.Width + firstColumn];
                    var secondHeight = source[secondRow * input.Source.Width + secondColumn];
                    if (!double.IsFinite(firstHeight) || !double.IsFinite(secondHeight))
                    {
                        skippedMissingPairCount++;
                        continue;
                    }

                    eligiblePairCount++;
                    var delta = secondHeight - firstHeight;
                    var magnitude = Math.Abs(delta);
                    if (!Passes(input.Polarity, delta, input.MinimumDelta)
                        || winner is not null && magnitude <= winner.Magnitude)
                    {
                        continue;
                    }

                    winner = new Candidate(
                        firstRow,
                        firstColumn,
                        firstHeight,
                        secondRow,
                        secondColumn,
                        secondHeight,
                        delta,
                        magnitude);
                }

                if (winner is null)
                {
                    continue;
                }

                var candidate = winner;
                points.Add(new C3DHeightDifferenceEdgePoint(
                    input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
                        ? rectangle.Row + scanlineOffset
                        : rectangle.Column + scanlineOffset,
                    candidate.FirstRow,
                    candidate.FirstColumn,
                    candidate.FirstHeight,
                    candidate.SecondRow,
                    candidate.SecondColumn,
                    candidate.SecondHeight,
                    candidate.SignedDelta,
                    candidate.Magnitude,
                    (candidate.FirstColumn + candidate.SecondColumn) / 2.0,
                    (candidate.FirstHeight + candidate.SecondHeight) / 2.0,
                    (candidate.FirstRow + candidate.SecondRow) / 2.0));
            }

            if (points.Count < 2)
            {
                throw new InvalidDataException(
                    $"Height Difference Edge requires at least two accepted scanlines; accepted {points.Count} of {scanlineCount}.");
            }

            var diagnostics = new C3DHeightDifferenceEdgeDiagnostics(
                scanlineCount,
                eligiblePairCount,
                skippedMissingPairCount,
                points.Count,
                scanlineCount - points.Count,
                points.Min(point => point.Magnitude),
                points.Max(point => point.Magnitude),
                points.Average(point => point.Magnitude));
            var provenance = $"{input.StepId}:HeightDifferenceEdge:{C3DHeightDifferenceEdgePointSet.ContractVersion}:axis={input.ComparisonAxis}:polarity={input.Polarity}:minimumDelta={input.MinimumDelta:R}:candidate=StrongestPerScanline:point=PairMidpoint:missing=SkipPair:boundary=WithinSelection:root={input.Source.RootSourceSha256}:input={input.Source.ContentSha256}:selection={input.SelectionId}";
            var output = C3DHeightDifferenceEdgePointSet.Create(
                input.OutputEntityId,
                input.RootSourceEntityId,
                input.Source.RootSourceSha256,
                input.Source.EntityId,
                input.Source.ContentSha256,
                input.SelectionId,
                input.Selection,
                input.Source.Unit,
                input.Source.FrameId,
                input.ComparisonAxis,
                input.Polarity,
                input.MinimumDelta,
                points,
                diagnostics,
                provenance);
            stopwatch.Stop();
            return new C3DHeightDifferenceEdgeEvaluation(
                new ToolResult(
                    "C3D Height Difference Edge",
                    ResultStatus.Pass,
                    "Completed - feature extraction; no acceptance rule evaluated.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Accepted point count", MetricKind.Count, points.Count, "count"),
                        new Metric("Eligible pair count", MetricKind.Count, eligiblePairCount, "count"),
                        new Metric("Skipped missing pair count", MetricKind.Count, skippedMissingPairCount, "count"),
                        new Metric("No-candidate scanline count", MetricKind.Count, scanlineCount - points.Count, "count"),
                        new Metric("Accepted magnitude minimum", MetricKind.Deviation, diagnostics.AcceptedMagnitudeMinimum, input.Source.Unit),
                        new Metric("Accepted magnitude maximum", MetricKind.Deviation, diagnostics.AcceptedMagnitudeMaximum, input.Source.Unit),
                        new Metric("Accepted magnitude mean", MetricKind.Deviation, diagnostics.AcceptedMagnitudeMean, input.Source.Unit)
                    ],
                    [new Overlay(input.OutputEntityId, OverlayKind.Point, "Height-difference edge points", SourceEntityId: input.Source.EntityId)]),
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
            return new C3DHeightDifferenceEdgeEvaluation(
                new ToolResult("C3D Height Difference Edge", ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }

    private static void Validate(C3DHeightDifferenceEdgeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.RootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SelectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentNullException.ThrowIfNull(input.Selection);
        if (!double.IsFinite(input.MinimumDelta) || input.MinimumDelta <= 0)
        {
            throw new InvalidDataException("Height Difference Edge MinimumDelta must be finite and greater than zero.");
        }
        if (!string.Equals(input.Source.Unit, "raw-height", StringComparison.Ordinal)
            || !string.Equals(input.Source.ScalarMeaning, "raw-height", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Height Difference Edge v1 accepts raw-height only.");
        }
        if (!input.Source.IsDerived)
        {
            throw new InvalidDataException("Height Difference Edge v1 requires a published derived height field.");
        }
        if (input.Selection.Row < 0 || input.Selection.Column < 0
            || input.Selection.RowCount <= 0 || input.Selection.ColumnCount <= 0
            || input.Selection.Row > input.Source.Height - input.Selection.RowCount
            || input.Selection.Column > input.Source.Width - input.Selection.ColumnCount)
        {
            throw new InvalidDataException("Height Difference Edge selection is outside the input grid.");
        }
        if (input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
            && input.Selection.ColumnCount < 2)
        {
            throw new InvalidDataException("AcrossColumns requires at least two selected columns.");
        }
        if (input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossRows
            && input.Selection.RowCount < 2)
        {
            throw new InvalidDataException("AcrossRows requires at least two selected rows.");
        }
        var scanlines = input.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
            ? input.Selection.RowCount
            : input.Selection.ColumnCount;
        if (scanlines < 2)
        {
            throw new InvalidDataException("Height Difference Edge requires at least two scanlines in the selection.");
        }
    }

    private static bool Passes(C3DHeightDifferencePolarity polarity, double delta, double minimumDelta) => polarity switch
    {
        C3DHeightDifferencePolarity.Rising => delta >= minimumDelta,
        C3DHeightDifferencePolarity.Falling => delta <= -minimumDelta,
        C3DHeightDifferencePolarity.Absolute => Math.Abs(delta) >= minimumDelta,
        _ => throw new InvalidDataException($"Unsupported Height Difference Edge polarity: {polarity}.")
    };

    private sealed record Candidate(
        int FirstRow,
        int FirstColumn,
        double FirstHeight,
        int SecondRow,
        int SecondColumn,
        double SecondHeight,
        double SignedDelta,
        double Magnitude);
}
