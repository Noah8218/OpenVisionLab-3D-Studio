using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DRegionTransformPropagationInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    C3DConnectedRegionArtifact RegionArtifact,
    int RegionIndex,
    C3DAffineTransform3D PublishedAffineTransform,
    string OutputEntityId);

public sealed record C3DRegionTransformPropagationEvaluation(
    ToolResult Result,
    C3DTransformedRegionArtifact? Output);

/// <summary>
/// Propagates one exact connected-region cell set through an already-published
/// full-XYZ affine transform. The source grid and region membership remain
/// unchanged; this adapter only joins existing typed identities and applies
/// the published matrix to finite source samples.
/// </summary>
public static class C3DRegionTransformPropagationRule
{
    public const string ToolName = "Region Transform Propagation";

    public static C3DRegionTransformPropagationEvaluation Evaluate(
        C3DRegionTransformPropagationInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            var region = input.RegionArtifact.Regions[input.RegionIndex];
            var values = input.Source.Values.Span;
            var cells = new C3DTransformedRegionCell[region.Cells.Count];
            var finiteCount = 0;
            for (var index = 0; index < region.Cells.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceCell = region.Cells[index];
                var rawHeight = values[checked(sourceCell.Row * input.Source.Width + sourceCell.Column)];
                if (!double.IsFinite(rawHeight))
                {
                    cells[index] = new C3DTransformedRegionCell(
                        sourceCell.Row,
                        sourceCell.Column,
                        null,
                        null,
                        null,
                        null);
                    continue;
                }

                var transformed = input.PublishedAffineTransform.Transform(
                    sourceCell.Column,
                    rawHeight,
                    sourceCell.Row);
                if (!double.IsFinite(transformed.X)
                    || !double.IsFinite(transformed.Y)
                    || !double.IsFinite(transformed.Z))
                {
                    throw new InvalidDataException(
                        "Region transform propagation produced a non-finite reference point.");
                }

                cells[index] = new C3DTransformedRegionCell(
                    sourceCell.Row,
                    sourceCell.Column,
                    rawHeight,
                    transformed.X,
                    transformed.Y,
                    transformed.Z);
                finiteCount++;
            }

            var output = C3DTransformedRegionArtifact.Create(
                input.OutputEntityId,
                input.RegionArtifact,
                input.RegionIndex,
                input.Source.EntityId,
                input.Source.ContentSha256,
                input.Source.RootSourceSha256,
                input.Source.Unit,
                input.Source.FrameId,
                C3DAffineApplyRule.SourceCoordinateConvention,
                input.Source.Width,
                input.Source.Height,
                input.PublishedAffineTransform,
                cells,
                $"{input.StepId}:RegionTransformPropagation:{C3DTransformedRegionArtifact.CurrentSchemaVersion}:region={input.RegionArtifact.ContentSha256}:transform={input.PublishedAffineTransform.ContentSha256}");

            stopwatch.Stop();
            var status = finiteCount == 0
                ? ResultStatus.Warning
                : ResultStatus.Pass;
            var message = finiteCount == 0
                ? "The typed region relationship is preserved, but no finite source sample produced a transformed point."
                : "Propagated the exact typed region cells through the Published AffineTransform3D; source and region artifacts remain unchanged.";
            return new C3DRegionTransformPropagationEvaluation(
                new ToolResult(
                    ToolName,
                    status,
                    message,
                    stopwatch.Elapsed,
                    [
                        new Metric("Region cells", MetricKind.Count, output.CellCount, "cells"),
                        new Metric("Finite transformed points", MetricKind.Count, output.FiniteCellCount, "points", status),
                        new Metric("Missing source cells", MetricKind.Count, output.MissingCellCount, "cells")
                    ],
                    [new Overlay(
                        output.OutputEntityId,
                        OverlayKind.Point,
                        $"Transformed region {output.SourceRegionIndex}: {output.FiniteCellCount:N0}/{output.CellCount:N0} finite cells",
                        status,
                        input.Source.EntityId)]),
                output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            stopwatch.Stop();
            return new C3DRegionTransformPropagationEvaluation(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }

    private static void Validate(C3DRegionTransformPropagationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentNullException.ThrowIfNull(input.RegionArtifact);
        ArgumentNullException.ThrowIfNull(input.PublishedAffineTransform);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);

        if (input.Source.IsDerived)
        {
            throw new InvalidDataException(
                "Region transform propagation v1 accepts only the verified raw C3D source used by the typed region artifact.");
        }

        var regionValidity = C3DConnectedRegionArtifactValidator.Inspect(input.RegionArtifact);
        if (!regionValidity.IsValid)
        {
            throw new InvalidDataException(
                "Region transform propagation requires a valid ConnectedRegionArtifact: "
                + string.Join(" ", regionValidity.Errors));
        }

        if (input.RegionIndex < 0
            || input.RegionIndex >= input.RegionArtifact.Regions.Count)
        {
            throw new InvalidDataException(
                $"Connected-region index {input.RegionIndex} is outside the Published ConnectedRegionArtifact.");
        }

        if (!string.Equals(input.Source.EntityId, input.RegionArtifact.SourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Source.ContentSha256, input.RegionArtifact.SourceContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Source.RootSourceSha256, input.RegionArtifact.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Source.Unit, input.RegionArtifact.Unit, StringComparison.Ordinal)
            || !string.Equals(input.Source.FrameId, input.RegionArtifact.FrameId, StringComparison.Ordinal)
            || input.Source.Width != input.RegionArtifact.GridWidth
            || input.Source.Height != input.RegionArtifact.GridHeight)
        {
            throw new InvalidDataException(
                "Region transform propagation source identity, grid, unit, or frame does not match the region artifact.");
        }

        if (!string.Equals(input.PublishedAffineTransform.RootSourceEntityId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.PublishedAffineTransform.RootSourceSha256, input.Source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.PublishedAffineTransform.SourceUnit, input.Source.Unit, StringComparison.Ordinal)
            || !string.Equals(input.PublishedAffineTransform.SourceFrameId, input.Source.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.PublishedAffineTransform.SourceCoordinateConvention, C3DAffineApplyRule.SourceCoordinateConvention, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Region transform propagation affine identity/frame/unit/convention does not match the raw source.");
        }

        if (string.Equals(input.OutputEntityId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.RegionArtifact.ArtifactId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.PublishedAffineTransform.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Region transform propagation output ID must differ from every input identity.");
        }
    }
}
