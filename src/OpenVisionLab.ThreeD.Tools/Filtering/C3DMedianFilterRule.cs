using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DMedianFilterInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    string OutputEntityId,
    int KernelSize);

public sealed record C3DMedianFilterEvaluation(
    ToolResult Result,
    C3DHeightFieldSnapshot? Output);

public static class C3DMedianFilterRule
{
    public static C3DMedianFilterEvaluation Evaluate(
        C3DMedianFilterInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(input.Source);
            ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
            ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
            if (input.KernelSize is not (3 or 5 or 7))
            {
                throw new InvalidDataException("Median Filter KernelSize must be 3, 5, or 7.");
            }
            if (input.Source.Width <= 0 || input.Source.Height <= 0
                || input.Source.Values.Length != checked(input.Source.Width * input.Source.Height))
            {
                throw new InvalidDataException("Median Filter source grid is invalid.");
            }
            if (!string.Equals(input.Source.Unit, "raw-height", StringComparison.Ordinal)
                || !string.Equals(input.Source.ScalarMeaning, "raw-height", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Median Filter v1 accepts raw-height only.");
            }
            if (input.Source.ValidCount == 0)
            {
                throw new InvalidDataException("Median Filter source contains no valid raw-height samples.");
            }

            var source = input.Source.Values.Span;
            var output = new double[source.Length];
            var radius = input.KernelSize / 2;
            var changed = 0;
            Span<double> neighbors = stackalloc double[49];
            for (var row = 0; row < input.Source.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var column = 0; column < input.Source.Width; column++)
                {
                    var sourceIndex = row * input.Source.Width + column;
                    if (!double.IsFinite(source[sourceIndex]))
                    {
                        output[sourceIndex] = double.NaN;
                        continue;
                    }

                    var count = 0;
                    for (var neighborRow = Math.Max(0, row - radius);
                         neighborRow <= Math.Min(input.Source.Height - 1, row + radius);
                         neighborRow++)
                    {
                        for (var neighborColumn = Math.Max(0, column - radius);
                             neighborColumn <= Math.Min(input.Source.Width - 1, column + radius);
                             neighborColumn++)
                        {
                            var value = source[neighborRow * input.Source.Width + neighborColumn];
                            if (double.IsFinite(value))
                            {
                                neighbors[count++] = value;
                            }
                        }
                    }

                    neighbors[..count].Sort();
                    var median = (count & 1) == 1
                        ? neighbors[count / 2]
                        : (neighbors[count / 2 - 1] + neighbors[count / 2]) / 2.0;
                    output[sourceIndex] = median;
                    if (median != source[sourceIndex])
                    {
                        changed++;
                    }
                }
            }

            var provenance = $"{input.StepId}:Median:KernelSize={input.KernelSize}:MissingValuePolicy=PreserveMask:BoundaryPolicy=AvailableNeighbors:source={input.Source.ContentSha256}";
            var derived = input.Source.CreateDerived(input.OutputEntityId, output, provenance);
            stopwatch.Stop();
            return new C3DMedianFilterEvaluation(
                new ToolResult(
                    "C3D Median Filter",
                    ResultStatus.Pass,
                    "Completed - preprocessing; no acceptance rule evaluated.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Valid sample count", MetricKind.Count, derived.ValidCount, "count"),
                        new Metric("Missing sample count", MetricKind.Count, derived.MissingCount, "count"),
                        new Metric("Changed sample count", MetricKind.Count, changed, "count"),
                        new Metric("Kernel size", MetricKind.Count, input.KernelSize, "cells")
                    ],
                    []),
                derived);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or OverflowException)
        {
            stopwatch.Stop();
            return new C3DMedianFilterEvaluation(
                new ToolResult(
                    "C3D Median Filter",
                    ResultStatus.Error,
                    exception.Message,
                    stopwatch.Elapsed,
                    [],
                    []),
                null);
        }
    }
}
