using System.Diagnostics;
using Lib.ThreeD.FeatureExtraction;
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

/// <summary>
/// C3D lineage/result adapter. Adjacent-pair scan and strongest-per-scanline
/// selection live only in Library-Noah; Studio owns the typed artifact, UI
/// lifecycle, recipe identity, and overlays.
/// </summary>
public static class C3DHeightDifferenceEdgeRule
{
    public static C3DHeightDifferenceEdgeEvaluation Evaluate(
        C3DHeightDifferenceEdgeInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ValidateAdapterInput(input);
            var numerical = new DeterministicHeightDifferenceEdgeTool().Execute(
                input.Source.Height,
                input.Source.Width,
                input.Source.Values.ToArray(),
                new HeightDifferenceEdgeOptions
                {
                    Selection = new HeightDifferenceEdgeSelection(
                        input.Selection.Row,
                        input.Selection.Column,
                        input.Selection.RowCount,
                        input.Selection.ColumnCount),
                    ComparisonAxis = (HeightDifferenceEdgeComparisonAxis)input.ComparisonAxis,
                    Polarity = (HeightDifferenceEdgePolarity)input.Polarity,
                    MinimumDelta = input.MinimumDelta
                },
                cancellationToken);
            if (!numerical.Success)
            {
                throw new InvalidDataException(numerical.Message);
            }

            var points = numerical.Points
                .Select(point => new C3DHeightDifferenceEdgePoint(
                    point.ScanlineIndex,
                    point.FirstRow,
                    point.FirstColumn,
                    point.FirstHeight,
                    point.SecondRow,
                    point.SecondColumn,
                    point.SecondHeight,
                    point.SignedDelta,
                    point.Magnitude,
                    (point.FirstColumn + point.SecondColumn) / 2.0,
                    (point.FirstHeight + point.SecondHeight) / 2.0,
                    (point.FirstRow + point.SecondRow) / 2.0))
                .ToArray();
            var numericalDiagnostics = numerical.Diagnostics;
            var diagnostics = new C3DHeightDifferenceEdgeDiagnostics(
                numericalDiagnostics.ScanlineCount,
                numericalDiagnostics.EligiblePairCount,
                numericalDiagnostics.SkippedMissingPairCount,
                numericalDiagnostics.AcceptedScanlineCount,
                numericalDiagnostics.NoCandidateScanlineCount,
                numericalDiagnostics.AcceptedMagnitudeMinimum,
                numericalDiagnostics.AcceptedMagnitudeMaximum,
                numericalDiagnostics.AcceptedMagnitudeMean);
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
                        new Metric("Accepted point count", MetricKind.Count, points.Length, "count"),
                        new Metric("Eligible pair count", MetricKind.Count, numericalDiagnostics.EligiblePairCount, "count"),
                        new Metric("Skipped missing pair count", MetricKind.Count, numericalDiagnostics.SkippedMissingPairCount, "count"),
                        new Metric("No-candidate scanline count", MetricKind.Count, numericalDiagnostics.NoCandidateScanlineCount, "count"),
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

    private static void ValidateAdapterInput(C3DHeightDifferenceEdgeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.RootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SelectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentNullException.ThrowIfNull(input.Selection);
        if (!string.Equals(input.Source.Unit, "raw-height", StringComparison.Ordinal)
            || !string.Equals(input.Source.ScalarMeaning, "raw-height", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Height Difference Edge v1 accepts raw-height only.");
        }

        if (!input.Source.IsDerived)
        {
            throw new InvalidDataException("Height Difference Edge v1 requires a published derived height field.");
        }
    }
}
