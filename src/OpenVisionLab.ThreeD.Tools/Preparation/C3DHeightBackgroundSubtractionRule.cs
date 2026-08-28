using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Geometry;
using SdkMode = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapBackgroundSubtractionMode;
using SdkOptions = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapBackgroundSubtractionOptions;
using SdkTool = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapBackgroundSubtractionTool;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DHeightBackgroundSubtractionInput(
    string StepId,
    C3DHeightFieldSnapshot Current,
    C3DHeightFieldSnapshot SavedBackground,
    string OutputEntityId);

public sealed record C3DHeightBackgroundSubtractionEvaluation(
    ToolResult Result,
    C3DHeightFieldSnapshot? Output,
    C3DHeightBackgroundSubtractionEvidence? Evidence);

/// <summary>
/// Strict Studio adapter for SDK-owned current-minus-saved-background
/// subtraction. Studio owns C3D identity, raw-height policy, derived lineage,
/// evidence, and the finite-zero encoding guard.
/// </summary>
public static class C3DHeightBackgroundSubtractionRule
{
    public const string ToolName = "C3D Saved Background Subtraction";

    public static C3DHeightBackgroundSubtractionEvaluation Evaluate(
        C3DHeightBackgroundSubtractionInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var sdkCurrent = ToSdk(input.Current);
            var sdkBackground = ToSdk(input.SavedBackground);
            var sdkResult = new SdkTool().Execute(
                sdkCurrent,
                sdkBackground,
                new SdkOptions
                {
                    Mode = SdkMode.CurrentMinusSavedBackground
                },
                cancellationToken);
            if (!sdkResult.Success || sdkResult.Output is null)
            {
                throw new InvalidDataException(sdkResult.Message);
            }

            ValidateSdkOutput(sdkResult.Output, input.Current);
            if (sdkResult.ZeroDeltaSampleCount > 0)
            {
                throw new InvalidDataException(
                    $"Background subtraction produced {sdkResult.ZeroDeltaSampleCount} exact zero delta cell(s); C3D reserves finite zero for missing data, so no output was produced.");
            }

            var output = input.Current.CreateDerived(
                input.OutputEntityId,
                sdkResult.Output.CopyValues(),
                $"{input.StepId}:C3DHeightBackgroundSubtraction:{C3DHeightBackgroundSubtractionEvidence.ContractVersion}:policy={C3DHeightBackgroundSubtractionEvidence.SubtractionPolicyName}:grid={C3DHeightBackgroundSubtractionEvidence.GridPolicyName}:missing={C3DHeightBackgroundSubtractionEvidence.MissingValuePolicyName}:zero={C3DHeightBackgroundSubtractionEvidence.ZeroDeltaPolicyName}:current={input.Current.ContentSha256}:background={input.SavedBackground.ContentSha256}");
            var evidence = C3DHeightBackgroundSubtractionEvidence.Create(
                input.StepId,
                input.Current.EntityId,
                input.Current.ContentSha256,
                input.Current.RootSourceSha256,
                input.Current.ByteLength,
                input.SavedBackground.EntityId,
                input.SavedBackground.ContentSha256,
                input.SavedBackground.RootSourceSha256,
                input.SavedBackground.ByteLength,
                output.EntityId,
                output.ContentSha256,
                output.RootSourceSha256,
                input.Current.Unit,
                input.Current.FrameId,
                input.Current.Width,
                input.Current.Height,
                C3DHeightBackgroundSubtractionMode.CurrentMinusSavedBackground,
                sdkResult.CurrentValidSampleCount,
                sdkResult.BackgroundValidSampleCount,
                sdkResult.PairedValidSampleCount,
                sdkResult.MissingEitherSampleCount,
                sdkResult.ZeroDeltaSampleCount,
                sdkResult.PositiveDeltaSampleCount,
                sdkResult.NegativeDeltaSampleCount,
                output.Provenance);
            stopwatch.Stop();
            return new C3DHeightBackgroundSubtractionEvaluation(
                new ToolResult(
                    ToolName,
                    ResultStatus.Pass,
                    "Completed current-minus-saved-background subtraction on an exactly aligned grid; both inputs remain unchanged.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Current valid sample count", MetricKind.Count, evidence.CurrentValidSampleCount, "count"),
                        new Metric("Background valid sample count", MetricKind.Count, evidence.BackgroundValidSampleCount, "count"),
                        new Metric("Paired valid sample count", MetricKind.Count, evidence.PairedValidSampleCount, "count"),
                        new Metric("Missing either-input sample count", MetricKind.Count, evidence.MissingEitherSampleCount, "count"),
                        new Metric("Positive delta sample count", MetricKind.Count, evidence.PositiveDeltaSampleCount, "count"),
                        new Metric("Negative delta sample count", MetricKind.Count, evidence.NegativeDeltaSampleCount, "count"),
                        new Metric("Output valid sample count", MetricKind.Count, output.ValidCount, "count", ResultStatus.Pass),
                        new Metric("Output missing sample count", MetricKind.Count, output.MissingCount, "count")
                    ],
                    [
                        new Overlay(
                            $"background-subtraction.{output.EntityId}",
                            OverlayKind.ColorMap,
                            $"Saved-background delta: {evidence.PairedValidSampleCount:N0} paired cell(s)",
                            ResultStatus.Pass,
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

    public static void Validate(C3DHeightBackgroundSubtractionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Current);
        ArgumentNullException.ThrowIfNull(input.SavedBackground);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (string.Equals(input.OutputEntityId, input.Current.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.SavedBackground.EntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Background subtraction output identity must differ from both inputs.");
        }

        ValidateSource(input.Current, "Current");
        ValidateSource(input.SavedBackground, "Saved background");
        if (input.Current.Width != input.SavedBackground.Width
            || input.Current.Height != input.SavedBackground.Height
            || input.Current.GridOriginColumn != input.SavedBackground.GridOriginColumn
            || input.Current.GridOriginRow != input.SavedBackground.GridOriginRow
            || !string.Equals(input.Current.Unit, input.SavedBackground.Unit, StringComparison.Ordinal)
            || !string.Equals(input.Current.FrameId, input.SavedBackground.FrameId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Current and saved-background C3D grids must have identical dimensions, origin, units, and frame; automatic alignment is not supported.");
        }
    }

    private static void ValidateSource(C3DHeightFieldSnapshot source, string label)
    {
        if (!string.Equals(source.Unit, "raw-height", StringComparison.Ordinal)
            || !string.Equals(source.ScalarMeaning, "raw-height", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{label} background subtraction input must be raw-height.");
        }

        if (source.ByteLength <= 0
            || !IsSha256(source.ContentSha256)
            || !IsSha256(source.RootSourceSha256)
            || source.Width <= 0
            || source.Height <= 0
            || source.Values.Length != checked(source.Width * source.Height)
            || source.ValidCount == 0)
        {
            throw new InvalidDataException($"{label} background subtraction source identity or grid is invalid.");
        }
    }

    private static HeightMap3D ToSdk(C3DHeightFieldSnapshot source) =>
        new(
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

    private static void ValidateSdkOutput(HeightMap3D output, C3DHeightFieldSnapshot source)
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
                "Background subtraction output does not preserve the current source grid, unit, frame, or identity.");
        }
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
