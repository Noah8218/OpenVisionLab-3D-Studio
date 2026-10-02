using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DConnectedRegionGateInput(
    string StepId,
    string OutputEntityId,
    string RootSourceEntityId,
    C3DHeightFieldSnapshot RawSource,
    C3DHeightFieldSnapshot DetectionSource,
    C3DConnectedRegionArtifact RegionArtifact,
    C3DConnectedRegionGatePolicy Policy);

public sealed record C3DConnectedRegionGateEvaluation(
    ToolResult Result,
    C3DConnectedRegionGateOutput? Output);

/// <summary>
/// Consumes one exact source-bound raw-region route. Region membership and source
/// lineage are validated here; G-13/G-14 arithmetic remains in the existing
/// C3DConnectedRegionAnalyzer/SDK adapter.
/// </summary>
public static class C3DConnectedRegionGateRule
{
    public static C3DConnectedRegionGateEvaluation Evaluate(
        C3DConnectedRegionGateInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateInput(input);
            var artifactValidity = C3DConnectedRegionArtifactValidator.Inspect(input.RegionArtifact);
            if (!artifactValidity.IsValid)
            {
                return NotEvaluated(
                    input,
                    "InvalidRegionArtifact",
                    string.Join(" ", artifactValidity.Errors),
                    stopwatch.Elapsed);
            }

            var region = input.RegionArtifact.Regions
                .SingleOrDefault(candidate => candidate.Index == input.Policy.SelectedRegionIndex);
            if (region is null)
            {
                stopwatch.Stop();
                var missingOutput = CreateOutput(
                    input,
                    0,
                    0,
                    0d,
                    null,
                    null,
                    null,
                    C3DConnectedRegionGateDecision.Missing,
                    "SelectedRegionNotDetected",
                    $"Selected connected region {input.Policy.SelectedRegionIndex} was not detected. Expected-object absence is not inferred.");
                return new(
                    new ToolResult(
                        "Connected Region Coverage/Height Gate",
                        ResultStatus.Warning,
                        missingOutput.Message,
                        stopwatch.Elapsed,
                        [],
                        CreateOverlays(input, missingOutput, ResultStatus.Warning)),
                    missingOutput);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var indices = region.Cells
                .Select(cell => checked(cell.Row * input.RawSource.Width + cell.Column))
                .ToArray();
            var selectedMask = C3DOutlierCellMap.Create(
                input.RawSource.Width,
                input.RawSource.Height,
                indices);
            var connectivity = input.RegionArtifact.Connectivity switch
            {
                C3DConnectedRegionArtifact.FourConnectivity => C3DConnectedRegionConnectivity.Four,
                C3DConnectedRegionArtifact.EightConnectivity => C3DConnectedRegionConnectivity.Eight,
                _ => throw new InvalidDataException("Connected Region Coverage/Height Gate connectivity is unsupported.")
            };

            var presenceOptions = new C3DConnectedRegionPresenceOptions
            {
                MinimumFiniteCoverageRatio = input.Policy.MinimumFiniteCoverageRatio,
                MinimumMeanHeight = input.Policy.MinimumMeanHeight,
                MaximumMeanHeight = input.Policy.MaximumMeanHeight,
                HeightUnit = input.Policy.HeightUnit
            };
            var fillOptions = new C3DConnectedRegionFillHeightOptions
            {
                ReferenceSurface = new C3DConnectedRegionFillHeightReferenceSurface(
                    input.Policy.ReferenceSlopeX,
                    input.Policy.ReferenceSlopeZ,
                    input.Policy.ReferenceIntercept),
                MinimumFiniteCoverageRatio = input.Policy.MinimumFiniteCoverageRatio,
                MinimumMeanFillHeight = input.Policy.MinimumMeanHeight,
                MaximumMeanFillHeight = input.Policy.MaximumMeanHeight,
                HeightUnit = input.Policy.HeightUnit
            };

            C3DConnectedRegionGateOutput output;
            ResultStatus status;
            string message;
            IReadOnlyList<Overlay> overlays;
            if (input.Policy.Mode == C3DConnectedRegionGateMode.Presence)
            {
                var analysis = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskPresence(
                    selectedMask,
                    input.RawSource.Values.ToArray(),
                    connectivity,
                    presenceOptions,
                    cancellationToken);
                var metric = RequireSingleRegion(
                    analysis.Success,
                    analysis.Message,
                    analysis.Regions,
                    region,
                    input);
                var decision = metric.Decision == C3DConnectedRegionPresenceDecision.Present
                    ? C3DConnectedRegionGateDecision.Accepted
                    : C3DConnectedRegionGateDecision.Rejected;
                message = decision == C3DConnectedRegionGateDecision.Accepted
                    ? $"Selected detected region {input.Policy.SelectedRegionIndex} passed the raw-height coverage/height gate."
                    : $"Selected detected region {input.Policy.SelectedRegionIndex} failed the raw-height coverage/height gate; this is not an expected-object absence decision.";
                output = CreateOutput(
                    input,
                    metric.TotalCellCount,
                    metric.FiniteCellCount,
                    metric.FiniteCoverageRatio,
                    metric.MeanHeight,
                    null,
                    null,
                    decision,
                    decision == C3DConnectedRegionGateDecision.Accepted ? "Accepted" : "CoverageOrMeanHeightRejected",
                    message);
                status = decision == C3DConnectedRegionGateDecision.Accepted
                    ? ResultStatus.Pass
                    : ResultStatus.Fail;
                overlays = CreateOverlays(input, output, status);
                stopwatch.Stop();
                return new(
                    CreateResult(output, status, stopwatch.Elapsed, overlays),
                    output);
            }

            var fillAnalysis = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskFillHeight(
                selectedMask,
                input.RawSource.Values.ToArray(),
                connectivity,
                fillOptions,
                cancellationToken);
            var fillMetric = RequireSingleFillRegion(
                fillAnalysis.Success,
                fillAnalysis.Message,
                fillAnalysis.Regions,
                region,
                input);
            var fillDecision = fillMetric.Decision == C3DConnectedRegionFillHeightDecision.Accepted
                ? C3DConnectedRegionGateDecision.Accepted
                : C3DConnectedRegionGateDecision.Rejected;
            message = fillDecision == C3DConnectedRegionGateDecision.Accepted
                ? $"Selected detected region {input.Policy.SelectedRegionIndex} passed the fill-height coverage/height gate."
                : $"Selected detected region {input.Policy.SelectedRegionIndex} failed the fill-height coverage/height gate; this is not an expected-object absence decision.";
            output = CreateOutput(
                input,
                fillMetric.TotalCellCount,
                fillMetric.FiniteCellCount,
                fillMetric.FiniteCoverageRatio,
                fillMetric.MeanFillHeight,
                fillMetric.MinimumFillHeight,
                fillMetric.MaximumFillHeight,
                fillDecision,
                fillDecision == C3DConnectedRegionGateDecision.Accepted ? "Accepted" : "CoverageOrMeanFillHeightRejected",
                message);
            status = fillDecision == C3DConnectedRegionGateDecision.Accepted
                ? ResultStatus.Pass
                : ResultStatus.Fail;
            overlays = CreateOverlays(input, output, status);
            stopwatch.Stop();
            return new(
                CreateResult(output, status, stopwatch.Elapsed, overlays),
                output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            stopwatch.Stop();
            return NotEvaluated(
                input,
                "InvalidOrStaleProvenance",
                exception.Message,
                stopwatch.Elapsed);
        }
    }

    private static void ValidateInput(C3DConnectedRegionGateInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.RootSourceEntityId);
        ArgumentNullException.ThrowIfNull(input.RawSource);
        ArgumentNullException.ThrowIfNull(input.DetectionSource);
        ArgumentNullException.ThrowIfNull(input.RegionArtifact);
        ArgumentNullException.ThrowIfNull(input.Policy);
        if (input.RawSource.Width != input.DetectionSource.Width
            || input.RawSource.Height != input.DetectionSource.Height
            || input.RawSource.Width != input.RegionArtifact.GridWidth
            || input.RawSource.Height != input.RegionArtifact.GridHeight)
        {
            throw new InvalidDataException("Connected Region Coverage/Height Gate source and detection grids do not match the region artifact.");
        }
        if (!string.Equals(input.DetectionSource.EntityId, input.RegionArtifact.SourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.DetectionSource.ContentSha256, input.RegionArtifact.SourceContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.DetectionSource.RootSourceSha256, input.RegionArtifact.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.RawSource.RootSourceSha256, input.RegionArtifact.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.RawSource.Unit, input.DetectionSource.Unit, StringComparison.Ordinal)
            || !string.Equals(input.RawSource.Unit, input.RegionArtifact.Unit, StringComparison.Ordinal)
            || !string.Equals(input.RawSource.FrameId, input.DetectionSource.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.RawSource.FrameId, input.RegionArtifact.FrameId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Connected Region Coverage/Height Gate raw source, detection snapshot, and region provenance do not match.");
        }
        if (!string.Equals(input.Policy.HeightUnit, input.RawSource.Unit, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Connected Region Coverage/Height Gate height unit does not match the raw source unit.");
        }
    }

    private static C3DConnectedRegionPresenceMetric RequireSingleRegion(
        bool success,
        string message,
        IReadOnlyList<C3DConnectedRegionPresenceMetric> metrics,
        C3DConnectedRegionArtifactRegion expected,
        C3DConnectedRegionGateInput input)
    {
        if (!success)
        {
            throw new InvalidDataException(message);
        }
        var metric = metrics.SingleOrDefault()
            ?? throw new InvalidDataException("Connected Region Coverage/Height Gate did not produce one selected-region presence metric.");
        if (metric.TotalCellCount != expected.CellCount)
        {
            throw new InvalidDataException("Connected Region Coverage/Height Gate changed the selected region membership.");
        }
        return metric;
    }

    private static C3DConnectedRegionFillHeightMetric RequireSingleFillRegion(
        bool success,
        string message,
        IReadOnlyList<C3DConnectedRegionFillHeightMetric> metrics,
        C3DConnectedRegionArtifactRegion expected,
        C3DConnectedRegionGateInput input)
    {
        if (!success)
        {
            throw new InvalidDataException(message);
        }
        var metric = metrics.SingleOrDefault()
            ?? throw new InvalidDataException("Connected Region Coverage/Height Gate did not produce one selected-region fill-height metric.");
        if (metric.TotalCellCount != expected.CellCount)
        {
            throw new InvalidDataException("Connected Region Coverage/Height Gate changed the selected region membership.");
        }
        return metric;
    }

    private static ToolResult CreateResult(
        C3DConnectedRegionGateOutput output,
        ResultStatus status,
        TimeSpan elapsed,
        IReadOnlyList<Overlay> overlays) =>
        new(
            "Connected Region Coverage/Height Gate",
            status,
            output.Message,
            elapsed,
            [
                new Metric("Region total cells", MetricKind.Count, output.TotalCellCount, "count"),
                new Metric("Region finite cells", MetricKind.Count, output.FiniteCellCount, "count"),
                new Metric("Region finite coverage", MetricKind.Deviation, output.FiniteCoverageRatio, "ratio", status),
                new Metric(
                    output.Policy.Mode == C3DConnectedRegionGateMode.FillHeight
                        ? "Region mean fill height"
                        : "Region mean height",
                    MetricKind.Deviation,
                    output.MeanHeight ?? double.NaN,
                    output.Unit,
                    status)
            ],
            overlays);

    private static IReadOnlyList<Overlay> CreateOverlays(
        C3DConnectedRegionGateInput input,
        C3DConnectedRegionGateOutput output,
        ResultStatus status) =>
    [
        new Overlay(
            $"connected-region-gate.{output.OutputEntityId}",
            OverlayKind.ColorMap,
            $"Selected detected region {output.SelectedRegionIndex} | {output.Decision} | raw/detection/region provenance retained",
            status,
            input.RawSource.EntityId)
    ];

    private static C3DConnectedRegionGateOutput CreateOutput(
        C3DConnectedRegionGateInput input,
        int totalCellCount,
        int finiteCellCount,
        double finiteCoverageRatio,
        double? meanHeight,
        double? minimumHeight,
        double? maximumHeight,
        C3DConnectedRegionGateDecision decision,
        string reasonCode,
        string message) =>
        C3DConnectedRegionGateOutput.Create(
            input.OutputEntityId,
            input.RootSourceEntityId,
            input.RawSource.EntityId,
            input.RawSource.ContentSha256,
            input.DetectionSource.EntityId,
            input.DetectionSource.ContentSha256,
            input.DetectionSource.RootSourceSha256,
            input.RegionArtifact.ArtifactId,
            input.RegionArtifact.ContentSha256,
            input.RawSource.Unit,
            input.RawSource.FrameId,
            input.RawSource.Width,
            input.RawSource.Height,
            input.Policy,
            totalCellCount,
            finiteCellCount,
            finiteCoverageRatio,
            meanHeight,
            minimumHeight,
            maximumHeight,
            decision,
            reasonCode,
            message);

    private static C3DConnectedRegionGateEvaluation NotEvaluated(
        C3DConnectedRegionGateInput input,
        string reasonCode,
        string message,
        TimeSpan elapsed)
    {
        var output = CreateOutput(
            input,
            0,
            0,
            0d,
            null,
            null,
            null,
            C3DConnectedRegionGateDecision.NotEvaluated,
            reasonCode,
            $"Connected Region Coverage/Height Gate was not evaluated: {message}");
        return new(
            new ToolResult(
                "Connected Region Coverage/Height Gate",
                ResultStatus.Error,
                output.Message,
                elapsed,
                [],
                CreateOverlays(input, output, ResultStatus.Error)),
            output);
    }
}
