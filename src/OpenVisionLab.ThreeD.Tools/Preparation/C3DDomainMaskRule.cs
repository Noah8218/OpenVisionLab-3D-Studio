using System.Diagnostics;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Geometry;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DDomainMaskInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    C3DConnectedRegionArtifact Domain,
    string OutputEntityId);

public sealed record C3DDomainMaskEvaluation(
    ToolResult Result,
    C3DHeightFieldSnapshot? Output);

/// <summary>
/// Studio adapter for the D-07 same-grid domain reduction. Region detection
/// and mask arithmetic remain owned by the SDK; Studio owns source identity,
/// artifact validation, C3D missing encoding, and result lineage.
/// </summary>
public static class C3DDomainMaskRule
{
    public const string ToolName = "Domain / Mask";

    public static C3DDomainMaskEvaluation Evaluate(
        C3DDomainMaskInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ValidateInput(input);
            var validity = C3DConnectedRegionArtifactValidator.Inspect(input.Domain);
            if (!validity.IsValid)
            {
                throw new InvalidDataException(
                    $"Domain ConnectedRegionArtifact is invalid: {string.Join(" ", validity.Errors)}");
            }

            var foreground = BuildForegroundMask(input.Source, input.Domain);
            var sdkSource = new HeightMap3D(
                input.Source.Height,
                input.Source.Width,
                input.Source.GridOriginColumn,
                input.Source.GridOriginRow,
                1d,
                1d,
                input.Source.Values.ToArray(),
                "grid-index",
                input.Source.Unit,
                input.Source.FrameId,
                input.Source.EntityId);
            var reduction = new HeightMapDomainMaskTool().Execute(
                sdkSource,
                new HeightGridMask(input.Source.Height, input.Source.Width, foreground),
                cancellationToken);
            if (!reduction.Success || reduction.Output is null)
            {
                throw new InvalidDataException(reduction.Message);
            }

            var output = input.Source.CreateDerived(
                input.OutputEntityId,
                reduction.Output.CopyValues(),
                $"{input.StepId}:DomainMask:regions={input.Domain.Regions.Count}:cells={reduction.ForegroundCellCount}:source={input.Source.ContentSha256}:domain={input.Domain.ContentSha256}");
            stopwatch.Stop();
            return new(
                new ToolResult(
                    ToolName,
                    ResultStatus.Pass,
                    "The validated connected-region domain was reduced into a separate same-grid height field; source data remains unchanged.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Domain region count", MetricKind.Count, input.Domain.Regions.Count, "count"),
                        new Metric("Domain cell count", MetricKind.Count, reduction.ForegroundCellCount, "cells"),
                        new Metric("Preserved valid sample count", MetricKind.Count, reduction.PreservedValidSampleCount, "count"),
                        new Metric("Preserved missing sample count", MetricKind.Count, reduction.PreservedMissingSampleCount, "count"),
                        new Metric("Reduced to missing cell count", MetricKind.Count, reduction.ReducedToMissingCellCount, "count"),
                        new Metric("Output valid sample count", MetricKind.Count, output.ValidCount, "count"),
                        new Metric("Output missing sample count", MetricKind.Count, output.MissingCount, "count")
                    ],
                    [
                        new Overlay(
                            $"domain-mask.{output.EntityId}",
                            OverlayKind.ColorMap,
                            $"Domain / Mask: {reduction.ForegroundCellCount:N0} foreground cell(s)",
                            ResultStatus.Pass,
                            input.Source.EntityId)
                    ]),
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
            return new(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }

    public static void ValidateInput(C3DDomainMaskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentNullException.ThrowIfNull(input.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (string.Equals(input.OutputEntityId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.Domain.ArtifactId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Domain / Mask output identity must be separate from both inputs.");
        }

        if (input.Source.Width <= 0
            || input.Source.Height <= 0
            || input.Source.Values.Length != checked(input.Source.Width * input.Source.Height))
        {
            throw new InvalidDataException("Domain / Mask source grid is invalid.");
        }

        if (!string.Equals(input.Domain.SourceEntityId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Domain.SourceContentSha256, input.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Domain.RootSourceSha256, input.Source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || input.Domain.GridWidth != input.Source.Width
            || input.Domain.GridHeight != input.Source.Height
            || !string.Equals(input.Domain.Unit, input.Source.Unit, StringComparison.Ordinal)
            || !string.Equals(input.Domain.FrameId, input.Source.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.Domain.CoordinateConvention, C3DConnectedRegionArtifact.CurrentCoordinateConvention, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Domain / Mask requires the exact current source entity, content, root, grid, unit, frame, and source-grid convention.");
        }
    }

    private static bool[] BuildForegroundMask(
        C3DHeightFieldSnapshot source,
        C3DConnectedRegionArtifact domain)
    {
        var foreground = new bool[checked(source.Width * source.Height)];
        foreach (var region in domain.Regions ?? [])
        {
            foreach (var cell in region.Cells ?? [])
            {
                var index = checked(cell.Row * source.Width + cell.Column);
                foreground[index] = true;
            }
        }

        if (!foreground.Any(value => value))
        {
            throw new InvalidDataException("Domain / Mask requires at least one foreground cell.");
        }

        return foreground;
    }
}
