using System.Diagnostics;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DLevelSurfaceInput(
    string StepId,
    C3DHeightFieldSnapshot Source,
    IReadOnlyList<ToolRecipeSelection> ReferenceSelections,
    string OutputEntityId,
    int MinimumValidSampleCount,
    double MaximumReferenceRmsResidual);

public sealed record C3DLevelSurfaceEvaluation(
    ToolResult Result,
    C3DHeightFieldSnapshot? Output,
    C3DLevelingTransform? Transform,
    double OutputReferenceSlopeX,
    double OutputReferenceSlopeZ);

/// <summary>
/// Fits one least-squares height plane to the unique finite cells in one or
/// more explicit reference rectangles, then detrends Y while preserving the
/// source X/Z grid and missing mask.
/// </summary>
public static class C3DLevelSurfaceRule
{
    public const string ToolName = "C3D Level Surface";

    public static C3DLevelSurfaceEvaluation Evaluate(
        C3DLevelSurfaceInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ValidateInput(input);
            var (samples, regions) = CollectReferenceSamples(input, cancellationToken);
            if (samples.Count < input.MinimumValidSampleCount)
            {
                throw new InvalidDataException(
                    $"Level Surface requires at least {input.MinimumValidSampleCount} unique finite reference samples; found {samples.Count}.");
            }

            var fit = HeightFieldPlaneFit.Fit(samples);
            var residuals = samples
                .Select(sample => sample.RawHeight - fit.EvaluateY(sample.Position.X, sample.Position.Z))
                .ToArray();
            var residualRms = RootMeanSquare(residuals);
            var residualPeakToValley = residuals.Max() - residuals.Min();
            var targetHeight = samples.Average(sample => sample.RawHeight);
            var transform = C3DLevelingTransform.Create(
                $"{input.OutputEntityId}.transform",
                input.Source.EntityId,
                input.Source.RootSourceSha256,
                input.Source.Unit,
                input.Source.FrameId,
                input.Source.Width,
                input.Source.Height,
                fit.SlopeX,
                fit.SlopeZ,
                fit.Intercept,
                targetHeight,
                samples.Count,
                residualRms,
                residualPeakToValley,
                regions,
                $"{input.StepId}:{C3DLevelingTransform.ReferenceFitPolicy}:{C3DLevelingTransform.LevelingPolicy}:source={input.Source.ContentSha256}");

            if (residualRms > input.MaximumReferenceRmsResidual)
            {
                stopwatch.Stop();
                return new C3DLevelSurfaceEvaluation(
                    CreateResult(
                        ResultStatus.Fail,
                        "Reference-plane residual exceeds the authored gate; no leveled height field was produced.",
                        stopwatch.Elapsed,
                        input,
                        transform,
                        input.Source.ValidCount,
                        input.Source.MissingCount,
                        double.NaN,
                        double.NaN),
                    null,
                    transform,
                    double.NaN,
                    double.NaN);
            }

            var sourceValues = input.Source.Values.Span;
            var outputValues = new double[sourceValues.Length];
            for (var index = 0; index < sourceValues.Length; index++)
            {
                if ((index & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
                var value = sourceValues[index];
                outputValues[index] = double.IsFinite(value)
                    ? transform.TransformHeight(
                        index / input.Source.Width,
                        index % input.Source.Width,
                        value)
                    : double.NaN;
            }

            var output = input.Source.CreateDerived(
                input.OutputEntityId,
                outputValues,
                $"{input.StepId}:levelingTransform={transform.ContentSha256}:source={input.Source.ContentSha256}");
            var outputSamples = samples.Select(sample =>
            {
                var row = (int)sample.Position.Z;
                var column = (int)sample.Position.X;
                var height = outputValues[checked(row * input.Source.Width + column)];
                return new HeightFieldPlaneSample(new Vector3(column, (float)height, row), height);
            }).ToArray();
            var outputFit = HeightFieldPlaneFit.Fit(outputSamples);
            stopwatch.Stop();
            return new C3DLevelSurfaceEvaluation(
                CreateResult(
                    ResultStatus.Pass,
                    "Reference surface was leveled into a derived C3D; source bytes, grid, and missing mask remain unchanged.",
                    stopwatch.Elapsed,
                    input,
                    transform,
                    output.ValidCount,
                    output.MissingCount,
                    outputFit.SlopeX,
                    outputFit.SlopeZ),
                output,
                transform,
                outputFit.SlopeX,
                outputFit.SlopeZ);
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
            return new C3DLevelSurfaceEvaluation(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null,
                null,
                double.NaN,
                double.NaN);
        }
    }

    public static void ValidateInput(C3DLevelSurfaceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentNullException.ThrowIfNull(input.ReferenceSelections);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (input.ReferenceSelections.Count == 0)
        {
            throw new InvalidDataException("Level Surface requires one or more explicit reference GridRectangles.");
        }
        if (input.MinimumValidSampleCount < 3)
        {
            throw new InvalidDataException("MinimumValidSampleCount must be at least three.");
        }
        if (!double.IsFinite(input.MaximumReferenceRmsResidual)
            || input.MaximumReferenceRmsResidual <= 0)
        {
            throw new InvalidDataException("MaximumReferenceRmsResidual must be finite and greater than zero.");
        }
        if (!string.Equals(input.Source.Unit, "raw-height", StringComparison.Ordinal)
            || !string.Equals(input.Source.ScalarMeaning, "raw-height", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Level Surface v1 accepts raw-height C3D only.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in input.ReferenceSelections)
        {
            var rectangle = selection.GridRectangle
                ?? throw new InvalidDataException("Every Level Surface reference input must be a GridRectangle.");
            if (!ids.Add(selection.Id))
            {
                throw new InvalidDataException("Level Surface reference selection IDs must be unique.");
            }
            if (!string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridRectangle, StringComparison.Ordinal)
                || !string.Equals(selection.RootSourceId, input.Source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.FrameId, input.Source.FrameId, StringComparison.Ordinal)
                || !string.Equals(selection.SourceBinding.ContentSha256, input.Source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || selection.SourceBinding.GridWidth != input.Source.Width
                || selection.SourceBinding.GridHeight != input.Source.Height)
            {
                throw new InvalidDataException("Level Surface reference regions must share the exact current source identity.");
            }
            if (rectangle.Row < 0
                || rectangle.Column < 0
                || rectangle.RowCount <= 0
                || rectangle.ColumnCount <= 0
                || rectangle.Row > input.Source.Height - rectangle.RowCount
                || rectangle.Column > input.Source.Width - rectangle.ColumnCount)
            {
                throw new InvalidDataException("A Level Surface reference rectangle is outside the current source grid.");
            }
        }
    }

    private static (IReadOnlyList<HeightFieldPlaneSample> Samples, IReadOnlyList<C3DLevelingReferenceRegion> Regions)
        CollectReferenceSamples(C3DLevelSurfaceInput input, CancellationToken cancellationToken)
    {
        var unique = new HashSet<int>();
        var samples = new List<HeightFieldPlaneSample>();
        var regions = new List<C3DLevelingReferenceRegion>();
        var values = input.Source.Values.Span;
        foreach (var selection in input.ReferenceSelections)
        {
            var rectangle = selection.GridRectangle!;
            var validInRegion = 0;
            for (var row = rectangle.Row; row < rectangle.Row + rectangle.RowCount; row++)
            {
                for (var column = rectangle.Column; column < rectangle.Column + rectangle.ColumnCount; column++)
                {
                    var index = checked(row * input.Source.Width + column);
                    if ((index & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
                    var value = values[index];
                    if (!double.IsFinite(value))
                    {
                        continue;
                    }
                    validInRegion++;
                    if (unique.Add(index))
                    {
                        samples.Add(new HeightFieldPlaneSample(
                            new Vector3(column, (float)value, row),
                            value));
                    }
                }
            }
            regions.Add(new C3DLevelingReferenceRegion(
                selection.Id,
                rectangle.Row,
                rectangle.Column,
                rectangle.RowCount,
                rectangle.ColumnCount,
                validInRegion));
        }
        return (samples, regions);
    }

    private static ToolResult CreateResult(
        ResultStatus status,
        string message,
        TimeSpan elapsed,
        C3DLevelSurfaceInput input,
        C3DLevelingTransform transform,
        int outputValid,
        int outputMissing,
        double outputSlopeX,
        double outputSlopeZ) =>
        new(
            ToolName,
            status,
            message,
            elapsed,
            [
                new Metric("Reference region count", MetricKind.Count, transform.ReferenceRegions.Count, "count", status),
                new Metric("Reference valid sample count", MetricKind.Count, transform.ReferenceSampleCount, "count", status),
                new Metric("Input reference slope X", MetricKind.Deviation, transform.FittedSlopeX, $"{input.Source.Unit}/column", status),
                new Metric("Input reference slope Z", MetricKind.Deviation, transform.FittedSlopeZ, $"{input.Source.Unit}/row", status),
                new Metric("Reference residual RMS", MetricKind.Deviation, transform.ReferenceResidualRms, input.Source.Unit, status),
                new Metric("Reference residual P2V", MetricKind.Deviation, transform.ReferenceResidualPeakToValley, input.Source.Unit, status),
                new Metric("Maximum reference RMS", MetricKind.Deviation, input.MaximumReferenceRmsResidual, input.Source.Unit, status),
                new Metric("Target reference height", MetricKind.Length, transform.TargetHeight, input.Source.Unit, status),
                new Metric("Output reference slope X", MetricKind.Deviation, outputSlopeX, $"{input.Source.Unit}/column", status),
                new Metric("Output reference slope Z", MetricKind.Deviation, outputSlopeZ, $"{input.Source.Unit}/row", status),
                new Metric("Output valid sample count", MetricKind.Count, outputValid, "count", status),
                new Metric("Output missing sample count", MetricKind.Count, outputMissing, "count", status)
            ],
            [
                new Overlay(
                    $"overlay.{input.OutputEntityId}.reference-regions",
                    OverlayKind.Box,
                    $"{transform.ReferenceRegions.Count} explicit leveling reference region(s)",
                    status,
                    input.Source.EntityId),
                new Overlay(
                    $"overlay.{input.OutputEntityId}.reference-plane",
                    OverlayKind.Plane,
                    $"Leveling transform {transform.ContentSha256[..12]}",
                    status,
                    input.OutputEntityId)
            ]);

    private static double RootMeanSquare(IReadOnlyList<double> values) =>
        Math.Sqrt(values.Sum(value => value * value) / values.Count);
}
