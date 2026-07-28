using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DRemoveOutlierPixelsInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    string OutputEntityId,
    int WindowSize,
    double MaximumAbsoluteDeviation,
    int MinimumValidNeighbors);

public sealed record C3DRemoveOutlierPixelsEvaluation(
    ToolResult Result,
    C3DHeightFieldSnapshot? Output,
    C3DOutlierCellMap? OutlierMask);

/// <summary>
/// Deterministic local-median outlier removal. A finite center cell is removed
/// only when enough finite neighbors exist and its absolute deviation from
/// their median is strictly greater than the authored threshold.
/// </summary>
public static class C3DRemoveOutlierPixelsRule
{
    public const string Rule = "LocalMedianAbsoluteDeviation";
    public const string MissingValuePolicy = "PreserveMask";
    public const string BoundaryPolicy = "AvailableNeighbors";
    public const string OutlierPolicy = "SetMissing";

    public static C3DRemoveOutlierPixelsEvaluation Evaluate(
        C3DRemoveOutlierPixelsInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            var sourceValues = input.Source.Values.Span;
            var outputValues = input.Source.Values.ToArray();
            var outlierIndices = new List<int>();
            var neighbors = new List<double>(checked(input.WindowSize * input.WindowSize - 1));
            var radius = input.WindowSize / 2;

            for (var row = 0; row < input.Source.Height; row++)
            {
                for (var column = 0; column < input.Source.Width; column++)
                {
                    var index = checked(row * input.Source.Width + column);
                    if ((index & 0x3fff) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var center = sourceValues[index];
                    if (!double.IsFinite(center))
                    {
                        continue;
                    }

                    neighbors.Clear();
                    var firstRow = Math.Max(0, row - radius);
                    var lastRow = Math.Min(input.Source.Height - 1, row + radius);
                    var firstColumn = Math.Max(0, column - radius);
                    var lastColumn = Math.Min(input.Source.Width - 1, column + radius);
                    for (var neighborRow = firstRow; neighborRow <= lastRow; neighborRow++)
                    {
                        for (var neighborColumn = firstColumn; neighborColumn <= lastColumn; neighborColumn++)
                        {
                            if (neighborRow == row && neighborColumn == column)
                            {
                                continue;
                            }

                            var value = sourceValues[checked(neighborRow * input.Source.Width + neighborColumn)];
                            if (double.IsFinite(value))
                            {
                                neighbors.Add(value);
                            }
                        }
                    }

                    if (neighbors.Count < input.MinimumValidNeighbors)
                    {
                        continue;
                    }

                    neighbors.Sort();
                    var median = Median(neighbors);
                    if (Math.Abs(center - median) > input.MaximumAbsoluteDeviation)
                    {
                        outputValues[index] = double.NaN;
                        outlierIndices.Add(index);
                    }
                }
            }

            var mask = C3DOutlierCellMap.Create(
                input.Source.Width,
                input.Source.Height,
                outlierIndices);
            var provenance =
                $"{input.StepId}:{Rule}:WindowSize={input.WindowSize}:MaximumAbsoluteDeviation={input.MaximumAbsoluteDeviation:R}:MinimumValidNeighbors={input.MinimumValidNeighbors}:MissingValuePolicy={MissingValuePolicy}:BoundaryPolicy={BoundaryPolicy}:OutlierPolicy={OutlierPolicy}:maskSha256={mask.Sha256}:source={input.Source.ContentSha256}";
            var output = input.Source.CreateDerived(
                input.OutputEntityId,
                outputValues,
                provenance);
            stopwatch.Stop();
            return new C3DRemoveOutlierPixelsEvaluation(
                new ToolResult(
                    "Remove Outlier Pixels",
                    ResultStatus.Pass,
                    "Completed - outliers were set missing; source data remains unchanged.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Input valid sample count", MetricKind.Count, input.Source.ValidCount, "count"),
                        new Metric("Input missing sample count", MetricKind.Count, input.Source.MissingCount, "count"),
                        new Metric("Removed outlier count", MetricKind.Count, mask.OutlierCellCount, "count"),
                        new Metric("Output valid sample count", MetricKind.Count, output.ValidCount, "count"),
                        new Metric("Output missing sample count", MetricKind.Count, output.MissingCount, "count"),
                        new Metric("Maximum absolute deviation", MetricKind.Deviation, input.MaximumAbsoluteDeviation, input.Source.Unit)
                    ],
                    [
                        new Overlay(
                            $"mask.{input.OutputEntityId}.outliers",
                            OverlayKind.ColorMap,
                            $"Removed outliers: {mask.OutlierCellCount:N0} | mask {mask.Sha256[..12]}",
                            SourceEntityId: input.OutputEntityId)
                    ]),
                output,
                mask);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or OverflowException)
        {
            stopwatch.Stop();
            return new C3DRemoveOutlierPixelsEvaluation(
                new ToolResult(
                    "Remove Outlier Pixels",
                    ResultStatus.Error,
                    exception.Message,
                    stopwatch.Elapsed,
                    [],
                    []),
                null,
                null);
        }
    }

    private static double Median(IReadOnlyList<double> sortedValues)
    {
        var middle = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 0
            ? (sortedValues[middle - 1] + sortedValues[middle]) / 2d
            : sortedValues[middle];
    }

    private static void Validate(C3DRemoveOutlierPixelsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (input.WindowSize is not (3 or 5 or 7))
        {
            throw new InvalidDataException("Remove Outlier Pixels WindowSize must be 3, 5, or 7.");
        }

        var maximumNeighbors = checked(input.WindowSize * input.WindowSize - 1);
        if (input.MinimumValidNeighbors < 1 || input.MinimumValidNeighbors > maximumNeighbors)
        {
            throw new InvalidDataException(
                $"MinimumValidNeighbors must be between 1 and {maximumNeighbors}.");
        }

        if (!double.IsFinite(input.MaximumAbsoluteDeviation)
            || input.MaximumAbsoluteDeviation <= 0d)
        {
            throw new InvalidDataException(
                "MaximumAbsoluteDeviation must be finite and greater than zero.");
        }

        if (!string.Equals(input.Source.Unit, "raw-height", StringComparison.Ordinal)
            || !string.Equals(input.Source.ScalarMeaning, "raw-height", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Remove Outlier Pixels v1 accepts raw-height only.");
        }

        if (input.Source.ValidCount == 0)
        {
            throw new InvalidDataException("Remove Outlier Pixels source contains no valid samples.");
        }
    }
}
