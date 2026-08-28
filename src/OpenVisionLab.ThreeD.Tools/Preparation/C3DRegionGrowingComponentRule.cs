using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Geometry;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DRegionGrowingComponentInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    C3DConnectedRegionArtifact ConnectedRegion,
    int SelectedRegionIndex,
    string OutputEntityId);

public sealed record C3DRegionGrowingComponentEvaluation(
    ToolResult Result,
    C3DHeightFieldSnapshot? Output,
    C3DRegionGrowingComponentEvidence? Evidence);

/// <summary>
/// Strict Studio adapter for selecting one validated G-11 connected region.
/// The existing SDK domain-mask projection owns value masking; Studio owns
/// source/artifact identity, component selection, lineage, and evidence.
/// </summary>
public static class C3DRegionGrowingComponentRule
{
    public const string ToolName = "Region-Growing Component";

    public static C3DRegionGrowingComponentEvaluation Evaluate(
        C3DRegionGrowingComponentInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ValidateInput(input);
            var validity = C3DConnectedRegionArtifactValidator.Inspect(input.ConnectedRegion);
            if (!validity.IsValid)
            {
                throw new InvalidDataException(
                    $"Connected-region artifact is invalid: {string.Join(" ", validity.Errors)}");
            }

            var selectedRegion = input.ConnectedRegion.Regions[input.SelectedRegionIndex];
            var foreground = BuildSelectedForegroundMask(input.Source, selectedRegion);
            cancellationToken.ThrowIfCancellationRequested();
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
            var sdkResult = new HeightMapDomainMaskTool().Execute(
                sdkSource,
                new HeightGridMask(input.Source.Height, input.Source.Width, foreground),
                cancellationToken);
            if (!sdkResult.Success || sdkResult.Output is null)
            {
                throw new InvalidDataException(sdkResult.Message);
            }

            ValidateSdkOutput(sdkResult.Output, input.Source);
            var output = input.Source.CreateDerived(
                input.OutputEntityId,
                sdkResult.Output.CopyValues(),
                $"{input.StepId}:RegionGrowingComponent:{C3DRegionGrowingComponentEvidence.ContractVersion}:region={input.SelectedRegionIndex}:connectivity={input.ConnectedRegion.Connectivity}:projection={C3DRegionGrowingComponentEvidence.ProjectionPolicyName}:missing={C3DRegionGrowingComponentEvidence.MissingValuePolicyName}:source={input.Source.ContentSha256}:connected={input.ConnectedRegion.ContentSha256}");
            var evidence = C3DRegionGrowingComponentEvidence.Create(
                input.StepId,
                input.Source.EntityId,
                input.Source.ContentSha256,
                input.Source.RootSourceSha256,
                input.Source.ByteLength,
                input.ConnectedRegion.ArtifactId,
                input.ConnectedRegion.ContentSha256,
                input.ConnectedRegion.MaskContentSha256,
                input.SelectedRegionIndex,
                input.ConnectedRegion.Connectivity,
                output.EntityId,
                output.ContentSha256,
                output.RootSourceSha256,
                input.Source.Unit,
                input.Source.FrameId,
                input.Source.Width,
                input.Source.Height,
                selectedRegion.CellCount,
                input.Source.ValidCount,
                input.Source.MissingCount,
                output.ValidCount,
                sdkResult.ReducedToMissingCellCount,
                C3DRegionGrowingComponentMode.SelectConnectedRegion,
                output.Provenance);
            stopwatch.Stop();
            var status = evidence.HasFiniteComponent
                ? ResultStatus.Pass
                : ResultStatus.Warning;
            var message = evidence.HasFiniteComponent
                ? "Completed selected connected-region component preparation; source data remains unchanged."
                : "The selected connected region contains no finite source samples; the separate component output contains only missing cells.";
            return new C3DRegionGrowingComponentEvaluation(
                new ToolResult(
                    ToolName,
                    status,
                    message,
                    stopwatch.Elapsed,
                    [
                        new Metric("Selected region index", MetricKind.Count, evidence.SelectedRegionIndex, "index"),
                        new Metric("Selected component cell count", MetricKind.Count, evidence.SelectedCellCount, "cells"),
                        new Metric("Input valid sample count", MetricKind.Count, evidence.InputValidSampleCount, "count"),
                        new Metric("Input missing sample count", MetricKind.Count, evidence.InputMissingSampleCount, "count"),
                        new Metric("Retained component sample count", MetricKind.Count, evidence.RetainedValidSampleCount, "count", status),
                        new Metric("Reduced background sample count", MetricKind.Count, evidence.ReducedBackgroundSampleCount, "count"),
                        new Metric("Output valid sample count", MetricKind.Count, output.ValidCount, "count", status),
                        new Metric("Output missing sample count", MetricKind.Count, output.MissingCount, "count", status)
                    ],
                    [
                        new Overlay(
                            $"region-growing-component.{output.EntityId}",
                            OverlayKind.ColorMap,
                            $"Region-growing component: region {evidence.SelectedRegionIndex} / {evidence.SelectedCellCount:N0} cell(s)",
                            status,
                            input.Source.EntityId)
                    ]),
                output,
                evidence);
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
                null,
                null);
        }
    }

    public static void ValidateInput(C3DRegionGrowingComponentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentNullException.ThrowIfNull(input.ConnectedRegion);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (string.Equals(input.OutputEntityId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.ConnectedRegion.ArtifactId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Region-growing component output identity must differ from the source and connected-region artifact.");
        }

        if (!string.Equals(input.Source.Unit, "raw-height", StringComparison.Ordinal)
            || !string.Equals(input.Source.ScalarMeaning, "raw-height", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Region-growing component preparation accepts raw-height sources only.");
        }

        if (input.Source.ByteLength <= 0
            || !IsSha256(input.Source.ContentSha256)
            || !IsSha256(input.Source.RootSourceSha256)
            || input.Source.Width <= 0
            || input.Source.Height <= 0
            || input.Source.Values.Length != checked(input.Source.Width * input.Source.Height)
            || input.Source.ValidCount == 0)
        {
            throw new InvalidDataException(
                "Region-growing component source identity or grid is invalid.");
        }

        if (!string.Equals(input.ConnectedRegion.SourceEntityId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.ConnectedRegion.SourceContentSha256, input.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.ConnectedRegion.RootSourceSha256, input.Source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || input.ConnectedRegion.GridWidth != input.Source.Width
            || input.ConnectedRegion.GridHeight != input.Source.Height
            || !string.Equals(input.ConnectedRegion.Unit, input.Source.Unit, StringComparison.Ordinal)
            || !string.Equals(input.ConnectedRegion.FrameId, input.Source.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.ConnectedRegion.CoordinateConvention, C3DConnectedRegionArtifact.CurrentCoordinateConvention, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Region-growing component requires the exact source entity, content, root, grid, unit, frame, and source-grid convention.");
        }

        if (input.SelectedRegionIndex < 0
            || input.SelectedRegionIndex >= input.ConnectedRegion.Regions.Count)
        {
            throw new InvalidDataException(
                "Region-growing component selected region index is outside the connected-region artifact.");
        }
    }

    private static bool[] BuildSelectedForegroundMask(
        C3DHeightFieldSnapshot source,
        C3DConnectedRegionArtifactRegion region)
    {
        var foreground = new bool[checked(source.Width * source.Height)];
        foreach (var cell in region.Cells ?? [])
        {
            var index = checked(cell.Row * source.Width + cell.Column);
            if (foreground[index])
            {
                throw new InvalidDataException(
                    $"Connected-region {region.Index} contains a duplicate cell.");
            }

            foreground[index] = true;
        }

        if (!foreground.Any(value => value))
        {
            throw new InvalidDataException(
                "Region-growing component requires at least one selected cell.");
        }

        return foreground;
    }

    private static void ValidateSdkOutput(
        HeightMap3D output,
        C3DHeightFieldSnapshot source)
    {
        if (output.Rows != source.Height
            || output.Columns != source.Width
            || output.OriginX != source.GridOriginColumn
            || output.OriginY != source.GridOriginRow
            || output.ColumnPitch != 1d
            || output.RowPitch != 1d
            || !string.Equals(output.PlanarUnit, "grid-index", StringComparison.Ordinal)
            || !string.Equals(output.HeightUnit, source.Unit, StringComparison.Ordinal)
            || !string.Equals(output.FrameId, source.FrameId, StringComparison.Ordinal)
            || !string.Equals(output.SourceId, source.EntityId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Region-growing component output does not preserve the source grid, unit, frame, or identity.");
        }
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!((character >= '0' && character <= '9')
                || (character >= 'A' && character <= 'F')
                || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
