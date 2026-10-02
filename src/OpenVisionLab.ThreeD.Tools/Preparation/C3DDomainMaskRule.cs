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

public sealed record C3DGridDomainMaskInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    C3DGridDomainArtifact Domain,
    string OutputEntityId);

public sealed record C3DRawRegionDomainMaskInput(
    string StepId,
    C3DHeightFieldSnapshot RawSource,
    C3DHeightFieldSnapshot DetectionSource,
    C3DConnectedRegionArtifact Domain,
    int SelectedRegionIndex,
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
        try
        {
            ValidateInput(input);
            var validity = C3DConnectedRegionArtifactValidator.Inspect(input.Domain);
            if (!validity.IsValid)
            {
                throw new InvalidDataException(
                    $"Domain ConnectedRegionArtifact is invalid: {string.Join(" ", validity.Errors)}");
            }

            return C3DDomainMaskProjection.Execute(
                input.Source,
                input.StepId,
                input.OutputEntityId,
                input.Domain.Regions.SelectMany(region => region.Cells)
                    .Select(cell => (cell.Row, cell.Column))
                    .ToArray(),
                input.Domain.Regions.Count,
                "connected-region",
                input.Domain.ArtifactId,
                input.Domain.ContentSha256,
                cancellationToken);
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
            return new(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, TimeSpan.Zero, [], []),
                null);
        }
    }

    public static C3DDomainMaskEvaluation Evaluate(
        C3DRawRegionDomainMaskInput input,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateInput(input);
            var validity = C3DConnectedRegionArtifactValidator.Inspect(input.Domain);
            if (!validity.IsValid)
            {
                throw new InvalidDataException(
                    $"Domain ConnectedRegionArtifact is invalid: {string.Join(" ", validity.Errors)}");
            }

            if (input.SelectedRegionIndex >= input.Domain.Regions.Count)
            {
                throw new InvalidDataException(
                    $"Selected ConnectedRegionArtifact region index {input.SelectedRegionIndex} is outside the published region list.");
            }

            var region = input.Domain.Regions[input.SelectedRegionIndex];
            return C3DDomainMaskProjection.Execute(
                input.RawSource,
                input.StepId,
                input.OutputEntityId,
                region.Cells.Select(cell => (cell.Row, cell.Column)).ToArray(),
                1,
                "connected-region-selected",
                input.Domain.ArtifactId,
                input.Domain.ContentSha256,
                cancellationToken);
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
            return new(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, TimeSpan.Zero, [], []),
                null);
        }
    }

    public static C3DDomainMaskEvaluation Evaluate(
        C3DGridDomainMaskInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentNullException.ThrowIfNull(input.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);

        var validity = C3DGridDomainArtifactValidator.Inspect(input.Domain);
        if (!validity.IsValid)
        {
            return new(
                new ToolResult(
                    ToolName,
                    ResultStatus.Error,
                    $"Grid-domain artifact is invalid: {string.Join(" ", validity.Errors)}",
                    TimeSpan.Zero,
                    [],
                    []),
                null);
        }

        if (string.Equals(input.Domain.SourceEntityId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(input.Domain.SourceContentSha256, input.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            && string.Equals(input.Domain.RootSourceSha256, input.Source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            && input.Domain.GridWidth == input.Source.Width
            && input.Domain.GridHeight == input.Source.Height
            && string.Equals(input.Domain.Unit, input.Source.Unit, StringComparison.Ordinal)
            && string.Equals(input.Domain.FrameId, input.Source.FrameId, StringComparison.Ordinal)
            && string.Equals(input.Domain.CoordinateConvention, C3DGridDomainArtifact.CurrentCoordinateConvention, StringComparison.Ordinal))
        {
            return C3DDomainMaskProjection.Execute(
                input.Source,
                input.StepId,
                input.OutputEntityId,
                input.Domain.Cells.Select(cell => (cell.Row, cell.Column)).ToArray(),
                domainRegionCount: 1,
                "grid-polygon",
                input.Domain.ArtifactId,
                input.Domain.ContentSha256,
                cancellationToken);
        }

        return new(
            new ToolResult(
                ToolName,
                ResultStatus.Error,
                "Domain / Mask requires the exact current authored-domain source entity, content, root, grid, unit, frame, and source-grid convention.",
                TimeSpan.Zero,
                [],
                []),
            null);
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

    public static void ValidateInput(C3DRawRegionDomainMaskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.RawSource);
        ArgumentNullException.ThrowIfNull(input.DetectionSource);
        ArgumentNullException.ThrowIfNull(input.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (input.SelectedRegionIndex < 0)
        {
            throw new InvalidDataException("Domain / Mask v2 SelectedRegionIndex must be non-negative.");
        }

        if (string.Equals(input.OutputEntityId, input.RawSource.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.DetectionSource.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.Domain.ArtifactId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Domain / Mask v2 output identity must be separate from all three inputs.");
        }

        ValidateSourceGrid(input.RawSource, "raw");
        ValidateSourceGrid(input.DetectionSource, "detection");
        if (input.RawSource.Width != input.DetectionSource.Width
            || input.RawSource.Height != input.DetectionSource.Height
            || input.RawSource.GridOriginColumn != input.DetectionSource.GridOriginColumn
            || input.RawSource.GridOriginRow != input.DetectionSource.GridOriginRow
            || !string.Equals(input.RawSource.Unit, input.DetectionSource.Unit, StringComparison.Ordinal)
            || !string.Equals(input.RawSource.FrameId, input.DetectionSource.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.RawSource.RootSourceSha256, input.DetectionSource.RootSourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Domain / Mask v2 raw and detection sources must share the exact root, grid, unit, and frame identity.");
        }

        if (!string.Equals(input.Domain.SourceEntityId, input.DetectionSource.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Domain.SourceContentSha256, input.DetectionSource.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Domain.RootSourceSha256, input.RawSource.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || input.Domain.GridWidth != input.RawSource.Width
            || input.Domain.GridHeight != input.RawSource.Height
            || !string.Equals(input.Domain.Unit, input.RawSource.Unit, StringComparison.Ordinal)
            || !string.Equals(input.Domain.FrameId, input.RawSource.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.Domain.CoordinateConvention, C3DConnectedRegionArtifact.CurrentCoordinateConvention, StringComparison.Ordinal)
            || input.Domain.OriginX != input.RawSource.GridOriginColumn
            || input.Domain.OriginY != input.RawSource.GridOriginRow
            || input.Domain.ColumnPitch != 1d
            || input.Domain.RowPitch != 1d)
        {
            throw new InvalidDataException(
                "Domain / Mask v2 requires the exact raw source, filtered detection source, same-grid region artifact, unit, frame, and grid-index geometry.");
        }
    }

    private static void ValidateSourceGrid(C3DHeightFieldSnapshot source, string role)
    {
        if (source.Width <= 0
            || source.Height <= 0
            || source.Values.Length != checked(source.Width * source.Height))
        {
            throw new InvalidDataException($"Domain / Mask v2 {role} source grid is invalid.");
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

internal static class C3DDomainMaskProjection
{
    public static C3DDomainMaskEvaluation Execute(
        C3DHeightFieldSnapshot source,
        string stepId,
        string outputEntityId,
        IReadOnlyList<(int Row, int Column)> cells,
        int domainRegionCount,
        string domainKind,
        string domainArtifactId,
        string domainContentSha256,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
            ArgumentNullException.ThrowIfNull(cells);
            if (string.Equals(outputEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(outputEntityId, domainArtifactId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Domain / Mask output identity must be separate from both inputs.");
            }
            if (source.Width <= 0
                || source.Height <= 0
                || source.Values.Length != checked(source.Width * source.Height))
            {
                throw new InvalidDataException("Domain / Mask source grid is invalid.");
            }

            var foreground = new bool[checked(source.Width * source.Height)];
            foreach (var cell in cells)
            {
                if (cell.Row < 0 || cell.Row >= source.Height
                    || cell.Column < 0 || cell.Column >= source.Width)
                {
                    throw new InvalidDataException(
                        $"Domain / Mask cell ({cell.Row}, {cell.Column}) is outside the source grid.");
                }

                foreground[checked(cell.Row * source.Width + cell.Column)] = true;
            }
            if (!foreground.Any(value => value))
            {
                throw new InvalidDataException("Domain / Mask requires at least one foreground cell.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var sdkSource = new HeightMap3D(
                source.Height,
                source.Width,
                source.GridOriginColumn,
                source.GridOriginRow,
                1d,
                1d,
                source.Values.ToArray(),
                "grid-index",
                source.Unit,
                source.FrameId,
                source.EntityId);
            var reduction = new HeightMapDomainMaskTool().Execute(
                sdkSource,
                new HeightGridMask(source.Height, source.Width, foreground),
                cancellationToken);
            if (!reduction.Success || reduction.Output is null)
            {
                throw new InvalidDataException(reduction.Message);
            }

            var output = source.CreateDerived(
                outputEntityId,
                reduction.Output.CopyValues(),
                $"{stepId}:DomainMask:kind={domainKind}:regions={domainRegionCount}:cells={reduction.ForegroundCellCount}:source={source.ContentSha256}:domain={domainContentSha256}");
            stopwatch.Stop();
            return new(
                new ToolResult(
                    C3DDomainMaskRule.ToolName,
                    ResultStatus.Pass,
                    $"The validated {domainKind} domain was reduced into a separate same-grid height field; source data remains unchanged.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Domain region count", MetricKind.Count, domainRegionCount, "count"),
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
                            source.EntityId)
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
                new ToolResult(C3DDomainMaskRule.ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }
}
