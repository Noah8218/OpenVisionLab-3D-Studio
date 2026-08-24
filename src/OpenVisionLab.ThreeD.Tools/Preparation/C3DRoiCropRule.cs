using System.Diagnostics;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Geometry;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DRoiCropInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    ToolRecipeSelection Selection,
    string OutputEntityId);

public sealed record C3DRoiCropEvaluation(
    ToolResult Result,
    C3DHeightFieldSnapshot? Output,
    ToolRecipeGridRectangle? SourceRegion);

/// <summary>
/// Adapts one exact recipe-owned GridRectangle to the source-neutral Vision SDK crop tool.
/// </summary>
public static class C3DRoiCropRule
{
    public const string ToolName = "ROI / Crop";

    public static C3DRoiCropEvaluation Evaluate(
        C3DRoiCropInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ValidateInput(input);
            var rectangle = input.Selection.GridRectangle!;
            var source = new HeightMap3D(
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
            var crop = new HeightMapCropTool().Execute(
                source,
                new HeightMapRoi(
                    rectangle.Row,
                    rectangle.Column,
                    rectangle.RowCount,
                    rectangle.ColumnCount),
                cancellationToken);
            if (!crop.Success || crop.Output is null)
            {
                throw new InvalidDataException(crop.Message);
            }

            var output = input.Source.CreateCrop(
                input.OutputEntityId,
                crop,
                $"{input.StepId}:roi={input.Selection.Id}:region={rectangle.Row},{rectangle.Column},{rectangle.RowCount},{rectangle.ColumnCount}:source={input.Source.ContentSha256}");
            stopwatch.Stop();
            return new C3DRoiCropEvaluation(
                CreateResult(ResultStatus.Pass, "The selected ROI was copied into a separate smaller HeightField; source bytes and selection remain unchanged.", stopwatch.Elapsed, input, output),
                output,
                rectangle);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            stopwatch.Stop();
            return new C3DRoiCropEvaluation(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null,
                input?.Selection?.GridRectangle);
        }
    }

    public static void ValidateInput(C3DRoiCropInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentNullException.ThrowIfNull(input.Selection);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        var rectangle = input.Selection.GridRectangle
            ?? throw new InvalidDataException("ROI / Crop requires one explicit GridRectangle.");
        if (!string.Equals(input.Selection.Kind, ToolRecipeSelectionKinds.GridRectangle, StringComparison.Ordinal)
            || !string.Equals(input.Selection.RootSourceId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Selection.FrameId, input.Source.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.Selection.SourceBinding.ContentSha256, input.Source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || input.Selection.SourceBinding.GridWidth != input.Source.Width
            || input.Selection.SourceBinding.GridHeight != input.Source.Height)
        {
            throw new InvalidDataException("ROI / Crop selection must share the exact current source identity, frame, and grid.");
        }
        if (rectangle.Row < 0
            || rectangle.Column < 0
            || rectangle.RowCount <= 0
            || rectangle.ColumnCount <= 0
            || rectangle.Row > input.Source.Height - rectangle.RowCount
            || rectangle.Column > input.Source.Width - rectangle.ColumnCount)
        {
            throw new InvalidDataException("ROI / Crop selection is outside the current source grid.");
        }
    }

    private static ToolResult CreateResult(
        ResultStatus status,
        string message,
        TimeSpan elapsed,
        C3DRoiCropInput input,
        C3DHeightFieldSnapshot output)
    {
        var region = input.Selection.GridRectangle!;
        return new ToolResult(
            ToolName,
            status,
            message,
            elapsed,
            [
                new Metric("Source ROI row", MetricKind.Count, region.Row, "index", status),
                new Metric("Source ROI column", MetricKind.Count, region.Column, "index", status),
                new Metric("Output rows", MetricKind.Count, output.Height, "count", status),
                new Metric("Output columns", MetricKind.Count, output.Width, "count", status),
                new Metric("Output valid sample count", MetricKind.Count, output.ValidCount, "count", status),
                new Metric("Output missing sample count", MetricKind.Count, output.MissingCount, "count", status)
            ],
            [
                new Overlay(
                    $"overlay.{input.OutputEntityId}.source-roi",
                    OverlayKind.Box,
                    $"Crop ROI row {region.Row}, column {region.Column}, size {region.RowCount} x {region.ColumnCount}",
                    status,
                    input.Source.EntityId)
            ]);
    }
}
