using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SdkHeightGridRegion = OpenVisionLab.Vision3D.FeatureExtraction.HeightGridRegion;
using SdkHeightMapRegionStatisticsTool = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapRegionStatisticsTool;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DPresenceCheckInput(
    string OutputEntityId,
    string RootSourceEntityId,
    string InputEntityId,
    string InputContentSha256,
    string Unit,
    string FrameId,
    int GridWidth,
    int GridHeight,
    IReadOnlyList<double> Values,
    ToolRecipeSelection FeatureSelection,
    C3DPresenceCheckPolicy Policy);

public sealed record C3DPresenceCheckEvaluation(
    ToolResult Result,
    C3DPresenceCheckOutput? Output);

/// <summary>
/// Studio adapter for one explicit source-bound Presence Check feature. The
/// SDK owns finite-cell statistics; this adapter owns source/selection
/// identity and the inclusive acceptance policy.
/// </summary>
public static class C3DPresenceCheckRule
{
    public const string ToolName = "Presence Check";

    public static C3DPresenceCheckEvaluation Evaluate(
        C3DPresenceCheckInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateStudioContract(input);
            input.Policy.Validate();

            var region = input.FeatureSelection.GridRectangle!;
            var statistics = new SdkHeightMapRegionStatisticsTool().Execute(
                input.GridHeight,
                input.GridWidth,
                input.Values,
                new SdkHeightGridRegion(
                    region.Row,
                    region.Column,
                    region.RowCount,
                    region.ColumnCount));
            if (!statistics.Success)
            {
                throw new InvalidDataException(statistics.Message);
            }

            var totalCellCount = statistics.TotalCellCount;
            var finiteCellCount = statistics.FiniteCellCount;
            var missingCellCount = checked(totalCellCount - finiteCellCount);
            if (totalCellCount < 1
                || finiteCellCount < 0
                || finiteCellCount > totalCellCount
                || missingCellCount < 0
                || !double.IsFinite(statistics.FiniteCoverageRatio)
                || statistics.FiniteCoverageRatio is < 0d or > 1d)
            {
                throw new InvalidDataException(
                    "Presence Check statistics returned inconsistent cell counts or coverage.");
            }

            var meanRawHeight = statistics.HasFiniteSamples
                && double.IsFinite(statistics.Mean)
                ? statistics.Mean
                : (double?)null;
            var decision = EvaluateDecision(
                input.Policy,
                statistics.FiniteCoverageRatio,
                meanRawHeight,
                out var decisionReason);
            var feature = new C3DPresenceCheckFeatureMetric(
                input.FeatureSelection.Id,
                region,
                totalCellCount,
                finiteCellCount,
                missingCellCount,
                statistics.FiniteCoverageRatio,
                meanRawHeight,
                decision,
                decisionReason);
            var output = new C3DPresenceCheckOutput(
                input.OutputEntityId,
                input.RootSourceEntityId,
                input.InputEntityId,
                input.InputContentSha256,
                input.Unit,
                input.FrameId,
                input.FeatureSelection.Id,
                region,
                input.Policy,
                feature,
                CalculateContentSha256(input, feature),
                new C3DPresenceCheckOverlay(
                    $"overlay.presence.{input.OutputEntityId}.{input.FeatureSelection.Id}",
                    input.FeatureSelection.Id,
                    region,
                    decision));

            var metrics = new List<Metric>();
            if (meanRawHeight is { } mean)
            {
                metrics.Add(new Metric(
                    C3DPresenceCheckMetricNames.MeanRawHeight,
                    MetricKind.Number,
                    mean,
                    input.Unit,
                    decision));
            }
            metrics.Add(new Metric(
                C3DPresenceCheckMetricNames.FiniteCoverage,
                MetricKind.Number,
                feature.FiniteCoverageRatio,
                "ratio",
                feature.FiniteCoverageRatio >= input.Policy.MinimumFiniteCoverageRatio
                    ? ResultStatus.Pass
                    : ResultStatus.Fail));
            metrics.Add(new Metric(
                C3DPresenceCheckMetricNames.Presence,
                MetricKind.Number,
                feature.IsPresent ? 1d : 0d,
                "boolean",
                decision));
            metrics.Add(new Metric(
                C3DPresenceCheckMetricNames.FiniteSamples,
                MetricKind.Count,
                feature.FiniteCellCount,
                "cells"));
            metrics.Add(new Metric(
                C3DPresenceCheckMetricNames.MissingSamples,
                MetricKind.Count,
                feature.MissingCellCount,
                "cells"));

            stopwatch.Stop();
            return new C3DPresenceCheckEvaluation(
                new ToolResult(
                    ToolName,
                    decision,
                    decision == ResultStatus.Pass
                        ? "Presence Check passed: the explicit feature is present."
                        : "Presence Check failed: the explicit feature is missing or outside the inclusive limits.",
                    stopwatch.Elapsed,
                    metrics,
                    [
                        new Overlay(
                            output.Overlay!.OverlayId,
                            OverlayKind.ColorMap,
                            $"{output.FeatureSelectionId} {decision}",
                            decision,
                            input.InputEntityId)
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
            return new C3DPresenceCheckEvaluation(
                new ToolResult(
                    ToolName,
                    ResultStatus.Error,
                    exception.Message,
                    stopwatch.Elapsed,
                    [],
                    []),
                null);
        }
    }

    private static ResultStatus EvaluateDecision(
        C3DPresenceCheckPolicy policy,
        double finiteCoverageRatio,
        double? meanRawHeight,
        out string reason)
    {
        var failures = new List<string>();
        if (meanRawHeight is not { } mean)
        {
            failures.Add("mean raw height is unavailable because no finite samples exist");
        }
        else
        {
            if (finiteCoverageRatio < policy.MinimumFiniteCoverageRatio)
            {
                failures.Add(
                    $"finite coverage {finiteCoverageRatio.ToString("G17", CultureInfo.InvariantCulture)} "
                    + $"is below minimum {policy.MinimumFiniteCoverageRatio.ToString("G17", CultureInfo.InvariantCulture)}");
            }

            if (mean < policy.MinimumMeanRawHeight)
            {
                failures.Add(
                    $"mean raw height {mean.ToString("G17", CultureInfo.InvariantCulture)} "
                    + $"is below minimum {policy.MinimumMeanRawHeight.ToString("G17", CultureInfo.InvariantCulture)}");
            }
            else if (mean > policy.MaximumMeanRawHeight)
            {
                failures.Add(
                    $"mean raw height {mean.ToString("G17", CultureInfo.InvariantCulture)} "
                    + $"is above maximum {policy.MaximumMeanRawHeight.ToString("G17", CultureInfo.InvariantCulture)}");
            }
        }

        if (failures.Count == 0)
        {
            reason = "Pass: finite coverage and mean raw height are within inclusive limits.";
            return ResultStatus.Pass;
        }

        reason = $"Fail: {string.Join("; ", failures)}.";
        return ResultStatus.Fail;
    }

    private static void ValidateStudioContract(C3DPresenceCheckInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.RootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.InputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.InputContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.FrameId);
        ArgumentNullException.ThrowIfNull(input.Values);
        ArgumentNullException.ThrowIfNull(input.FeatureSelection);
        ArgumentNullException.ThrowIfNull(input.Policy);

        if (input.GridWidth < 1
            || input.GridHeight < 1
            || input.Values.Count != checked(input.GridWidth * input.GridHeight))
        {
            throw new InvalidDataException(
                "Presence Check source dimensions and row-major value count must agree.");
        }

        if (input.FeatureSelection.Kind != ToolRecipeSelectionKinds.GridRectangle
            || input.FeatureSelection.GridRectangle is not { } region)
        {
            throw new InvalidDataException(
                "Presence Check v1 requires one ordered GridRectangle feature selection.");
        }

        if (string.IsNullOrWhiteSpace(input.FeatureSelection.Id)
            || !string.Equals(
                input.FeatureSelection.RootSourceId,
                input.RootSourceEntityId,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                input.FeatureSelection.FrameId,
                input.FrameId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Presence Check feature selection is not bound to the declared source and frame.");
        }

        var binding = input.FeatureSelection.SourceBinding;
        if (!string.Equals(binding.ContentSha256, input.InputContentSha256, StringComparison.OrdinalIgnoreCase)
            || binding.GridWidth != input.GridWidth
            || binding.GridHeight != input.GridHeight)
        {
            throw new InvalidDataException(
                "Presence Check feature selection is not bound to the exact input grid content.");
        }

        if (region.Row < 0
            || region.Column < 0
            || region.RowCount < 1
            || region.ColumnCount < 1
            || region.Row + region.RowCount > input.GridHeight
            || region.Column + region.ColumnCount > input.GridWidth)
        {
            throw new InvalidDataException(
                $"Presence Check feature is outside the {input.GridWidth} x {input.GridHeight} source grid.");
        }
    }

    private static string CalculateContentSha256(
        C3DPresenceCheckInput input,
        C3DPresenceCheckFeatureMetric feature)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DPresenceCheckOutput");
            writer.Write(C3DPresenceCheckOutput.ContractVersion);
            writer.Write(input.OutputEntityId);
            writer.Write(input.RootSourceEntityId);
            writer.Write(input.InputEntityId);
            writer.Write(input.InputContentSha256.ToUpperInvariant());
            writer.Write(input.Unit);
            writer.Write(input.FrameId);
            writer.Write(input.FeatureSelection.Id);
            writer.Write(input.FeatureSelection.Kind);
            Write(writer, feature.Region);
            writer.Write(input.Policy.MinimumFiniteCoverageRatio);
            writer.Write(input.Policy.MinimumMeanRawHeight);
            writer.Write(input.Policy.MaximumMeanRawHeight);
            writer.Write(feature.TotalCellCount);
            writer.Write(feature.FiniteCellCount);
            writer.Write(feature.MissingCellCount);
            writer.Write(feature.FiniteCoverageRatio);
            writer.Write(feature.MeanRawHeight.HasValue);
            if (feature.MeanRawHeight is { } mean) writer.Write(mean);
            writer.Write(feature.Decision.ToString());
            writer.Write(feature.DecisionReason);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void Write(BinaryWriter writer, ToolRecipeGridRectangle region)
    {
        writer.Write(region.Row);
        writer.Write(region.Column);
        writer.Write(region.RowCount);
        writer.Write(region.ColumnCount);
    }
}
