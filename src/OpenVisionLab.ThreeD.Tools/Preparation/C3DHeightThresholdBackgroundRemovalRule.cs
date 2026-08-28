using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Geometry;
using SdkMode = OpenVisionLab.Vision3D.FeatureExtraction.HeightThresholdBackgroundRemovalMode;
using SdkOptions = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapThresholdBackgroundRemovalOptions;
using SdkTool = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapThresholdBackgroundRemovalTool;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DHeightThresholdBackgroundRemovalInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    string OutputEntityId,
    double Threshold,
    C3DHeightThresholdBackgroundRemovalMode Mode);

public sealed record C3DHeightThresholdBackgroundRemovalEvaluation(
    ToolResult Result,
    C3DHeightFieldSnapshot? Output,
    C3DHeightThresholdBackgroundRemovalEvidence? Evidence);

/// <summary>
/// Strict Studio adapter for the SDK-owned height-threshold projection. It
/// owns raw-height identity/policy, derived lineage, and presentation evidence;
/// the threshold comparison and same-grid value projection remain in the SDK.
/// </summary>
public static class C3DHeightThresholdBackgroundRemovalRule
{
    public const string ToolName = "Height Threshold Background Removal";

    public static C3DHeightThresholdBackgroundRemovalEvaluation Evaluate(
        C3DHeightThresholdBackgroundRemovalInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
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
            var sdkResult = new SdkTool().Execute(
                sdkSource,
                new SdkOptions
                {
                    Threshold = input.Threshold,
                    Mode = input.Mode == C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold
                        ? SdkMode.KeepAtOrAboveThreshold
                        : SdkMode.KeepAtOrBelowThreshold
                },
                cancellationToken);
            if (!sdkResult.Success || sdkResult.Output is null)
            {
                throw new InvalidDataException(sdkResult.Message);
            }

            var sdkOutput = sdkResult.Output;
            if (sdkOutput.Rows != input.Source.Height
                || sdkOutput.Columns != input.Source.Width
                || sdkOutput.OriginX != input.Source.GridOriginColumn
                || sdkOutput.OriginY != input.Source.GridOriginRow
                || sdkOutput.ColumnPitch != 1d
                || sdkOutput.RowPitch != 1d
                || !string.Equals(sdkOutput.PlanarUnit, "grid-index", StringComparison.Ordinal)
                || !string.Equals(sdkOutput.HeightUnit, input.Source.Unit, StringComparison.Ordinal)
                || !string.Equals(sdkOutput.FrameId, input.Source.FrameId, StringComparison.Ordinal)
                || !string.Equals(sdkOutput.SourceId, input.Source.EntityId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Height threshold background-removal output does not preserve the source grid, unit, frame, or identity.");
            }

            var output = input.Source.CreateDerived(
                input.OutputEntityId,
                sdkResult.Output.CopyValues(),
                $"{input.StepId}:HeightThresholdBackgroundRemoval:{C3DHeightThresholdBackgroundRemovalEvidence.ContractVersion}:threshold={input.Threshold:R}:mode={input.Mode}:comparison={C3DHeightThresholdBackgroundRemovalEvidence.ComparisonPolicyName}:missing={C3DHeightThresholdBackgroundRemovalEvidence.MissingValuePolicyName}:background={C3DHeightThresholdBackgroundRemovalEvidence.BackgroundPolicyName}:source={input.Source.ContentSha256}");
            var evidence = C3DHeightThresholdBackgroundRemovalEvidence.Create(
                input.StepId,
                input.Source.EntityId,
                input.Source.ContentSha256,
                input.Source.RootSourceSha256,
                output.EntityId,
                output.ContentSha256,
                input.Source.Unit,
                input.Source.FrameId,
                input.Source.Width,
                input.Source.Height,
                sdkResult.Threshold,
                input.Mode,
                sdkResult.InputValidSampleCount,
                sdkResult.InputMissingSampleCount,
                sdkResult.RetainedValidSampleCount,
                sdkResult.RemovedBackgroundSampleCount,
                output.Provenance);
            stopwatch.Stop();
            var warning = !evidence.HasForeground;
            var status = warning ? ResultStatus.Warning : ResultStatus.Pass;
            var message = warning
                ? "The threshold removed every finite sample; the separate same-grid output contains only missing cells."
                : "Completed inclusive height-threshold background removal; source data remains unchanged.";
            return new C3DHeightThresholdBackgroundRemovalEvaluation(
                new ToolResult(
                    ToolName,
                    status,
                    message,
                    stopwatch.Elapsed,
                    [
                        new Metric("Threshold", MetricKind.Deviation, evidence.Threshold, input.Source.Unit),
                        new Metric("Input valid sample count", MetricKind.Count, evidence.InputValidSampleCount, "count"),
                        new Metric("Input missing sample count", MetricKind.Count, evidence.InputMissingSampleCount, "count"),
                        new Metric("Retained foreground sample count", MetricKind.Count, evidence.RetainedValidSampleCount, "count", status),
                        new Metric("Removed background sample count", MetricKind.Count, evidence.RemovedBackgroundSampleCount, "count"),
                        new Metric("Output valid sample count", MetricKind.Count, output.ValidCount, "count", status),
                        new Metric("Output missing sample count", MetricKind.Count, output.MissingCount, "count", status)
                    ],
                    [
                        new Overlay(
                            $"height-threshold.{output.EntityId}",
                            OverlayKind.ColorMap,
                            $"Height threshold: {evidence.RemovedBackgroundSampleCount:N0} background cell(s)",
                            status,
                            output.EntityId)
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

    public static void Validate(C3DHeightThresholdBackgroundRemovalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (string.Equals(input.OutputEntityId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Height threshold background-removal output identity must differ from the source.");
        }

        if (!string.Equals(input.Source.Unit, "raw-height", StringComparison.Ordinal)
            || !string.Equals(input.Source.ScalarMeaning, "raw-height", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Height threshold background removal v1 accepts raw-height sources only.");
        }

        if (input.Source.Width <= 0
            || input.Source.Height <= 0
            || input.Source.Values.Length != checked(input.Source.Width * input.Source.Height))
        {
            throw new InvalidDataException("Height threshold background-removal source grid is invalid.");
        }

        if (input.Source.ValidCount == 0)
        {
            throw new InvalidDataException("Height threshold background removal requires at least one finite source sample.");
        }

        if (!double.IsFinite(input.Threshold))
        {
            throw new InvalidDataException("Height threshold must be finite.");
        }

        if (!Enum.IsDefined(input.Mode))
        {
            throw new InvalidDataException("Height threshold background-removal mode is invalid.");
        }
    }
}
