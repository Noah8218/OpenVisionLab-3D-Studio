using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using SdkHeightMap3D = OpenVisionLab.Vision3D.Geometry.HeightMap3D;
using SdkOptions = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapNormalPreparationOptions;
using SdkPoint = OpenVisionLab.Vision3D.FeatureExtraction.ThreeDPoint;
using SdkTool = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapNormalPreparationTool;
using SdkValidationState = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapNormalValidationState;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DHeightMapNormalPreparationInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    string OutputEntityId,
    C3DHeightMapNormalValidationOptions? Validation = null);

public sealed record C3DHeightMapNormalPreparationEvaluation(
    ToolResult Result,
    C3DHeightMapNormalPreparationEvidence? Evidence);

/// <summary>
/// Strict Dev adapter for SDK-owned regular height-map normal preparation.
/// The SDK owns finite-difference arithmetic; Studio owns source identity,
/// units/frame/convention, separate derived identity, and immutable evidence.
/// </summary>
public static class C3DHeightMapNormalPreparationRule
{
    public const string ToolName = "Height-Map Normal Preparation";

    public static C3DHeightMapNormalPreparationEvaluation Evaluate(
        C3DHeightMapNormalPreparationInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var sdkSource = new SdkHeightMap3D(
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
            var sdkOptions = input.Validation is null
                ? null
                : new SdkOptions
                {
                    ExpectedNormal = new SdkPoint(
                        input.Validation.ExpectedNormalX,
                        input.Validation.ExpectedNormalY,
                        input.Validation.ExpectedNormalZ),
                    MinimumAlignmentCosine = input.Validation.MinimumAlignmentCosine
                };
            var sdkResult = new SdkTool().Execute(sdkSource, sdkOptions, cancellationToken);
            if (!sdkResult.Success)
            {
                throw new InvalidDataException(sdkResult.Message);
            }

            ValidateSdkResult(sdkResult, input.Source);
            var samples = sdkResult.Samples
                .Select(sample => new C3DHeightMapNormalSample(
                    sample.Row,
                    sample.Column,
                    sample.Position.X,
                    sample.Position.Y,
                    sample.Position.Z,
                    sample.Normal.X,
                    sample.Normal.Y,
                    sample.Normal.Z,
                    sample.CentralColumnDerivative,
                    sample.CentralRowDerivative))
                .ToArray();
            var provenance =
                $"{input.StepId}:HeightMapNormalPreparation:{C3DHeightMapNormalPreparationEvidence.ContractVersion}:semantics=height-map-normal-finite-difference-with-explicit-validation-v1:derivative={C3DHeightMapNormalPreparationEvidence.DerivativePolicyName}:missing={C3DHeightMapNormalPreparationEvidence.MissingPolicyName}:source={input.Source.ContentSha256}:root={input.Source.RootSourceSha256}";
            var evidence = C3DHeightMapNormalPreparationEvidence.Create(
                input.StepId,
                input.Source.EntityId,
                input.Source.ContentSha256,
                input.Source.RootSourceSha256,
                input.Source.ByteLength,
                input.OutputEntityId,
                input.Source.Unit,
                input.Source.FrameId,
                sdkResult.RowCount,
                sdkResult.ColumnCount,
                sdkResult.InputFiniteSampleCount,
                sdkResult.CalculatedNormalCount,
                sdkResult.UnavailableNormalCount,
                sdkResult.CentralDerivativeCount,
                sdkResult.OneSidedDerivativeCount,
                sdkResult.MissingDerivativeCount,
                samples,
                MapValidationState(sdkResult.ValidationState),
                input.Validation,
                sdkResult.ValidatedNormalCount,
                sdkResult.ConsistentNormalCount,
                sdkResult.ReversedNormalCount,
                input.Validation is null ? null : sdkResult.MinimumAlignment,
                input.Validation is null ? null : sdkResult.MeanAlignment,
                input.Validation is null ? null : sdkResult.MaximumAngularErrorDegrees,
                provenance);
            stopwatch.Stop();
            var status = evidence.ValidationState == C3DHeightMapNormalValidationState.Failed
                ? ResultStatus.Fail
                : evidence.UnavailableNormalCount > 0
                    ? ResultStatus.Warning
                    : ResultStatus.Pass;
            var message = status switch
            {
                ResultStatus.Fail => "Completed normal preparation, but explicit expected-normal validation failed.",
                ResultStatus.Warning => "Completed normal preparation with unavailable cells caused by missing finite neighbors.",
                _ => "Completed deterministic regular-height-map normal preparation; the source remains unchanged and the normal artifact is separate."
            };
            var metrics = new List<Metric>
            {
                new("Calculated normal count", MetricKind.Count, evidence.CalculatedNormalCount, "count"),
                new("Unavailable normal count", MetricKind.Count, evidence.UnavailableNormalCount, "count"),
                new("Central derivative count", MetricKind.Count, evidence.CentralDerivativeCount, "axis"),
                new("One-sided derivative count", MetricKind.Count, evidence.OneSidedDerivativeCount, "axis"),
                new("Missing derivative count", MetricKind.Count, evidence.MissingDerivativeCount, "axis")
            };
            if (evidence.MinimumAlignment.HasValue)
            {
                metrics.Add(new Metric("Minimum normal alignment", MetricKind.Number, evidence.MinimumAlignment.Value, "cosine", status));
                metrics.Add(new Metric("Mean normal alignment", MetricKind.Number, evidence.MeanAlignment!.Value, "cosine", status));
                metrics.Add(new Metric("Maximum angular error", MetricKind.Angle, evidence.MaximumAngularErrorDegrees!.Value, "degree", status));
            }
            return new C3DHeightMapNormalPreparationEvaluation(
                new ToolResult(
                    ToolName,
                    status,
                    message,
                    stopwatch.Elapsed,
                    metrics,
                    [new Overlay(
                        $"height-map-normal.{evidence.OutputEntityId}",
                        OverlayKind.Point,
                        $"Normal samples: {evidence.CalculatedNormalCount:N0}",
                        status,
                        evidence.OutputEntityId)]),
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
                null);
        }
    }

    public static void Validate(C3DHeightMapNormalPreparationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (string.Equals(input.Source.EntityId, input.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Height-map normal source and output identities must be distinct.");
        }
        if (input.Source.IsDerived
            || !string.Equals(input.Source.ScalarMeaning, "raw-height", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Height-map normal preparation v1 accepts only a raw-height source.");
        }
        if (input.Source.Width <= 0
            || input.Source.Height <= 0
            || input.Source.ByteLength <= 0
            || string.IsNullOrWhiteSpace(input.Source.Unit)
            || string.IsNullOrWhiteSpace(input.Source.FrameId)
            || !IsSha256(input.Source.ContentSha256)
            || !IsSha256(input.Source.RootSourceSha256)
            || input.Source.Values.Length != checked(input.Source.Width * input.Source.Height)
            || ContainsInfinity(input.Source.Values.Span))
        {
            throw new InvalidDataException("Height-map normal source identity, dimensions, or payload is invalid.");
        }
        if (input.Validation is not null)
        {
            var expected = new[]
            {
                input.Validation.ExpectedNormalX,
                input.Validation.ExpectedNormalY,
                input.Validation.ExpectedNormalZ
            };
            if (expected.Any(value => !double.IsFinite(value))
                || !double.IsFinite(input.Validation.MinimumAlignmentCosine)
                || input.Validation.MinimumAlignmentCosine < -1d
                || input.Validation.MinimumAlignmentCosine > 1d)
            {
                throw new InvalidDataException("Height-map normal validation options must be finite and bounded.");
            }
            var length = Math.Sqrt(expected.Sum(value => value * value));
            if (!double.IsFinite(length) || length <= 0d)
            {
                throw new InvalidDataException("Height-map normal expected vector must have positive finite length.");
            }
        }
    }

    private static void ValidateSdkResult(
        OpenVisionLab.Vision3D.FeatureExtraction.HeightMapNormalPreparationResult result,
        C3DHeightFieldSnapshot source)
    {
        var finiteCount = CountFinite(source.Values.Span);
        if (result.RowCount != source.Height
            || result.ColumnCount != source.Width
            || result.InputFiniteSampleCount != finiteCount
            || result.CalculatedNormalCount <= 0
            || result.UnavailableNormalCount < 0
            || result.CalculatedNormalCount + result.UnavailableNormalCount > finiteCount
            || result.Samples.Count != result.CalculatedNormalCount
            || result.CentralDerivativeCount + result.OneSidedDerivativeCount + result.MissingDerivativeCount
                != checked(finiteCount * 2))
        {
            throw new InvalidDataException("Height-map normal SDK counts or dimensions do not preserve the source contract.");
        }

        var expectedRow = 0;
        var expectedColumn = -1;
        foreach (var sample in result.Samples)
        {
            if (sample.Row < expectedRow
                || sample.Row >= source.Height
                || sample.Column < 0
                || sample.Column >= source.Width
                || (sample.Row == expectedRow && sample.Column <= expectedColumn)
                || !double.IsFinite(sample.Position.X)
                || !double.IsFinite(sample.Position.Y)
                || !double.IsFinite(sample.Position.Z)
                || !double.IsFinite(sample.Normal.X)
                || !double.IsFinite(sample.Normal.Y)
                || !double.IsFinite(sample.Normal.Z))
            {
                throw new InvalidDataException("Height-map normal SDK samples must be finite and row-major.");
            }
            var sourceHeight = source.Values.Span[(sample.Row * source.Width) + sample.Column];
            var expectedX = source.GridOriginColumn + sample.Column;
            var expectedZ = source.GridOriginRow + sample.Row;
            if (!double.IsFinite(sourceHeight)
                || sample.Position.X != expectedX
                || sample.Position.Y != sourceHeight
                || sample.Position.Z != expectedZ)
            {
                throw new InvalidDataException("Height-map normal SDK sample positions do not preserve source grid coordinates.");
            }
            var length = Math.Sqrt(
                sample.Normal.X * sample.Normal.X
                + sample.Normal.Y * sample.Normal.Y
                + sample.Normal.Z * sample.Normal.Z);
            if (!double.IsFinite(length) || Math.Abs(length - 1d) > 1e-9)
            {
                throw new InvalidDataException("Height-map normal SDK samples must contain unit normals.");
            }
            expectedRow = sample.Row;
            expectedColumn = sample.Column;
        }
    }

    private static C3DHeightMapNormalValidationState MapValidationState(SdkValidationState state) =>
        state switch
        {
            SdkValidationState.NotRequested => C3DHeightMapNormalValidationState.NotRequested,
            SdkValidationState.Passed => C3DHeightMapNormalValidationState.Passed,
            SdkValidationState.Failed => C3DHeightMapNormalValidationState.Failed,
            _ => throw new InvalidDataException("Unknown height-map normal validation state.")
        };

    private static int CountFinite(ReadOnlySpan<double> values)
    {
        var count = 0;
        foreach (var value in values)
        {
            if (double.IsFinite(value))
            {
                count++;
            }
        }

        return count;
    }

    private static bool ContainsInfinity(ReadOnlySpan<double> values)
    {
        foreach (var value in values)
        {
            if (double.IsInfinity(value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSha256(string value) =>
        value is not null
        && value.Length == 64
        && value.All(Uri.IsHexDigit);
}
