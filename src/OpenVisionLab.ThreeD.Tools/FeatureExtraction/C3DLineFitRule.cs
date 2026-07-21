using System.Diagnostics;
using Lib.ThreeD.FeatureExtraction;
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

/// <summary>
/// C3D lineage/result adapter. Deterministic consensus/TLS math lives only in
/// Library-Noah; Studio owns the typed artifact, UI lifecycle, and overlays.
/// </summary>
public static class C3DLineFitRule
{
    public static C3DLineFitEvaluation Evaluate(C3DLineFitInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ValidateAdapterInput(input);
            var edge = input.PublishedEdgePointSet;
            var numerical = new DeterministicLineFitTool().Execute(
                edge.Points
                    .Select(point => new DeterministicLineFitPoint(
                        point.ScanlineIndex,
                        new ThreeDPoint(point.X, point.Y, point.Z)))
                    .ToArray(),
                new DeterministicLineFitOptions
                {
                    InputHash = edge.ContentSha256,
                    MaximumOrthogonalResidual = input.MaximumOrthogonalResidual,
                    MinimumInlierCount = input.MinimumInlierCount,
                    MinimumInlierRatio = input.MinimumInlierRatio,
                    MinimumInlierScanlineSpan = input.MinimumInlierScanlineSpan,
                    PositiveScanlineAxis = edge.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
                        ? DeterministicLineFitPositiveAxis.Z
                        : DeterministicLineFitPositiveAxis.X
                },
                cancellationToken);
            if (!numerical.Success)
            {
                throw new InvalidDataException(numerical.Message);
            }

            var geometry = numerical.Geometry;
            var fitDiagnostics = numerical.Diagnostics;
            var pointDiagnostics = numerical.PointDiagnostics
                .Select(point => new C3DLineFeaturePointDiagnostic(
                    point.InputPointIndex,
                    point.ScanlineIndex,
                    point.SourcePoint.X,
                    point.SourcePoint.Y,
                    point.SourcePoint.Z,
                    point.ProjectedPoint.X,
                    point.ProjectedPoint.Y,
                    point.ProjectedPoint.Z,
                    point.OrthogonalResidual,
                    point.IsInlier))
                .ToArray();
            var diagnostics = new C3DLineFeatureDiagnostics(
                fitDiagnostics.InputPointCount,
                fitDiagnostics.InlierCount,
                fitDiagnostics.OutlierCount,
                fitDiagnostics.InlierRatio,
                fitDiagnostics.InlierScanlineMinimum,
                fitDiagnostics.InlierScanlineMaximum,
                fitDiagnostics.InlierScanlineSpan,
                fitDiagnostics.ResidualRms,
                fitDiagnostics.ResidualMaximum,
                fitDiagnostics.ResidualMedian,
                fitDiagnostics.ProjectedSegmentLength,
                fitDiagnostics.HypothesisCount,
                fitDiagnostics.RefinementIterationCount);
            var provenance = $"{input.StepId}:LineFit:{C3DLineFeature.ContractVersion}:method=DeterministicConsensusOrthogonalTls:hypotheses=Sha256PairSchedule/{DeterministicLineFitOptions.MaximumHypotheses}:refinement=OrthogonalTlsUntilStable10:direction=PositiveScanlineAxis:endpoints=InlierProjectionExtents:input={edge.ContentSha256}";
            var output = C3DLineFeature.Create(
                input.OutputEntityId,
                edge,
                input.MaximumOrthogonalResidual,
                input.MinimumInlierCount,
                input.MinimumInlierRatio,
                input.MinimumInlierScanlineSpan,
                geometry.Anchor.X,
                geometry.Anchor.Y,
                geometry.Anchor.Z,
                geometry.Direction.X,
                geometry.Direction.Y,
                geometry.Direction.Z,
                geometry.SegmentStart.X,
                geometry.SegmentStart.Y,
                geometry.SegmentStart.Z,
                geometry.SegmentEnd.X,
                geometry.SegmentEnd.Y,
                geometry.SegmentEnd.Z,
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
                        new Metric("Input point count", MetricKind.Count, edge.Points.Count, "count"),
                        new Metric("Inlier count", MetricKind.Count, diagnostics.InlierCount, "count"),
                        new Metric("Inlier ratio", MetricKind.Deviation, diagnostics.InlierRatio, "ratio"),
                        new Metric("Residual RMS", MetricKind.Deviation, diagnostics.ResidualRms, "source-coordinate"),
                        new Metric("Residual maximum", MetricKind.Deviation, diagnostics.ResidualMaximum, "source-coordinate"),
                        new Metric("Inlier scanline span", MetricKind.Count, diagnostics.InlierScanlineSpan, "grid-index")
                    ],
                    [new Overlay(input.OutputEntityId, OverlayKind.Polyline, "Full-XYZ fitted line segment", SourceEntityId: edge.OutputEntityId)]),
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

    private static void ValidateAdapterInput(C3DLineFitInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.PublishedEdgePointSet);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (string.Equals(input.OutputEntityId, input.PublishedEdgePointSet.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Line Fit output must differ from its EdgePointSet input.");
        }
    }
}
