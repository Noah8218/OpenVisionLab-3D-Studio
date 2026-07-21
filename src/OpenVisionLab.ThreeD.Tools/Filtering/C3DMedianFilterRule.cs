using System.Diagnostics;
using Lib.ThreeD.FeatureExtraction;
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

/// <summary>
/// C3D lineage/result adapter. Finite/NaN median window arithmetic lives only
/// in Library-Noah; Studio owns C3D zero/missing policy, typed artifacts,
/// recipe identity, and explicit lifecycle evidence.
/// </summary>
public static class C3DMedianFilterRule
{
    public static C3DMedianFilterEvaluation Evaluate(
        C3DMedianFilterInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ValidateAdapterInput(input);
            var numerical = new DeterministicMedianFilterTool().Execute(
                input.Source.Height,
                input.Source.Width,
                input.Source.Values.ToArray(),
                new DeterministicMedianFilterOptions { KernelSize = input.KernelSize },
                cancellationToken);
            if (!numerical.Success)
            {
                throw new InvalidDataException(numerical.Message);
            }

            var provenance = $"{input.StepId}:Median:KernelSize={input.KernelSize}:MissingValuePolicy=PreserveMask:BoundaryPolicy=AvailableNeighbors:source={input.Source.ContentSha256}";
            var derived = input.Source.CreateDerived(input.OutputEntityId, numerical.Values, provenance);
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
                        new Metric("Changed sample count", MetricKind.Count, numerical.ChangedCount, "count"),
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

    private static void ValidateAdapterInput(C3DMedianFilterInput input)
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
    }
}
