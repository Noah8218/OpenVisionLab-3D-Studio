using System.Diagnostics;
using OpenVisionLab.Vision3D.FeatureExtraction;
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
    double OutputReferenceSlopeZ,
    C3DLevelFrameArtifact? LevelFrame = null,
    C3DLevelFrameQualityEvidence? QualityEvidence = null,
    C3DLevelSurfaceCoordinateFrameChain? FrameChain = null);

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
            var numerical = new LevelSurfaceTool().Execute(
                input.Source.Height,
                input.Source.Width,
                input.Source.Values.ToArray(),
                input.ReferenceSelections.Select(selection =>
                {
                    var rectangle = selection.GridRectangle!;
                    return new LevelSurfaceRegion(
                        rectangle.Row,
                        rectangle.Column,
                        rectangle.RowCount,
                        rectangle.ColumnCount);
                }).ToArray(),
                new LevelSurfaceOptions
                {
                    MinimumValidSampleCount = input.MinimumValidSampleCount
                },
                cancellationToken);
            if (!numerical.Success)
            {
                throw new InvalidDataException(numerical.Message);
            }

            var regions = input.ReferenceSelections.Select((selection, index) =>
            {
                var rectangle = selection.GridRectangle!;
                return new C3DLevelingReferenceRegion(
                    selection.Id,
                    rectangle.Row,
                    rectangle.Column,
                    rectangle.RowCount,
                    rectangle.ColumnCount,
                    numerical.RegionEvidence[index].ValidSampleCount);
            }).ToArray();
            var transform = C3DLevelingTransform.Create(
                $"{input.OutputEntityId}.transform",
                input.Source.EntityId,
                input.Source.RootSourceSha256,
                input.Source.Unit,
                input.Source.FrameId,
                input.Source.Width,
                input.Source.Height,
                numerical.FittedSlopeX,
                numerical.FittedSlopeZ,
                numerical.FittedIntercept,
                numerical.TargetHeight,
                numerical.ReferenceSampleCount,
                numerical.ReferenceResidualRms,
                numerical.ReferenceResidualPeakToValley,
                regions,
                $"{input.StepId}:{C3DLevelingTransform.ReferenceFitPolicy}:{C3DLevelingTransform.LevelingPolicy}:source={input.Source.ContentSha256}");

            var frameNumerical = new LevelFrameTool().Execute(
                new LevelFramePlane(
                    numerical.FittedSlopeX,
                    numerical.FittedSlopeZ,
                    numerical.FittedIntercept));
            if (!frameNumerical.Success
                || frameNumerical.SourceToFrameValues.Count != 12)
            {
                throw new InvalidDataException(frameNumerical.Message);
            }

            var frameValues = frameNumerical.SourceToFrameValues;
            var levelFrame = C3DLevelFrameArtifact.Create(
                $"{input.OutputEntityId}.level-frame",
                $"{input.Source.FrameId}.level-frame",
                transform,
                new C3DAffineMatrix3x4(
                    frameValues[0], frameValues[1], frameValues[2], frameValues[3],
                    frameValues[4], frameValues[5], frameValues[6], frameValues[7],
                    frameValues[8], frameValues[9], frameValues[10], frameValues[11]),
                new C3DReferenceGridVector(
                    frameNumerical.Origin.X,
                    frameNumerical.Origin.Y,
                    frameNumerical.Origin.Z),
                new C3DReferenceGridVector(
                    frameNumerical.UAxis.X,
                    frameNumerical.UAxis.Y,
                    frameNumerical.UAxis.Z),
                new C3DReferenceGridVector(
                    frameNumerical.VAxis.X,
                    frameNumerical.VAxis.Y,
                    frameNumerical.VAxis.Z),
                new C3DReferenceGridVector(
                    frameNumerical.HAxis.X,
                    frameNumerical.HAxis.Y,
                    frameNumerical.HAxis.Z),
                $"{input.StepId}:{C3DLevelFrameArtifact.FramePolicy}:transform={transform.ContentSha256}:source={input.Source.ContentSha256}");
            var qualityEvidence = C3DLevelFrameQualityEvidence.Create(
                levelFrame,
                transform,
                C3DLevelFrameQualityPolicy.CompleteCoverage(
                    input.MaximumReferenceRmsResidual),
                $"{input.StepId}:{C3DLevelFrameQualityEvidence.ConfidenceSemantics}:frame={levelFrame.ContentSha256}:transform={transform.ContentSha256}");

            if (numerical.ReferenceResidualRms > input.MaximumReferenceRmsResidual)
            {
                var gateFrameChain = CreateFrameChain(input.Source, null, transform, levelFrame, input.StepId);
                stopwatch.Stop();
                return new C3DLevelSurfaceEvaluation(
                    CreateResult(
                        ResultStatus.Fail,
                        "Reference-plane residual exceeds the authored gate; no leveled height field was produced.",
                        stopwatch.Elapsed,
                        input,
                        transform,
                        levelFrame,
                        qualityEvidence,
                        input.Source.ValidCount,
                        input.Source.MissingCount,
                        double.NaN,
                        double.NaN),
                    null,
                    transform,
                    double.NaN,
                    double.NaN,
                    levelFrame,
                    qualityEvidence,
                    gateFrameChain);
            }

            var output = input.Source.CreateDerived(
                input.OutputEntityId,
                numerical.Values,
                $"{input.StepId}:levelingTransform={transform.ContentSha256}:source={input.Source.ContentSha256}");
            var outputFrameChain = CreateFrameChain(input.Source, output, transform, levelFrame, input.StepId);
            stopwatch.Stop();
            return new C3DLevelSurfaceEvaluation(
                CreateResult(
                    ResultStatus.Pass,
                    "Reference surface was leveled into a derived C3D; source bytes, grid, and missing mask remain unchanged.",
                    stopwatch.Elapsed,
                    input,
                    transform,
                    levelFrame,
                    qualityEvidence,
                    output.ValidCount,
                    output.MissingCount,
                    numerical.OutputReferenceSlopeX,
                    numerical.OutputReferenceSlopeZ),
                output,
                transform,
                numerical.OutputReferenceSlopeX,
                numerical.OutputReferenceSlopeZ,
                levelFrame,
                qualityEvidence,
                outputFrameChain);
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

    public static C3DLevelSurfaceCoordinateFrameChain CreateFrameChain(
        C3DHeightFieldSnapshot source,
        C3DHeightFieldSnapshot? result,
        C3DLevelingTransform transform,
        C3DLevelFrameArtifact levelFrame,
        string stepId)
    {
        if (!string.Equals(transform.RootSourceEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(transform.RootSourceSha256, source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(transform.SourceUnit, source.Unit, StringComparison.Ordinal)
            || !string.Equals(transform.SourceFrameId, source.FrameId, StringComparison.Ordinal)
            || !string.Equals(levelFrame.RootSourceEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(levelFrame.RootSourceSha256, source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(levelFrame.SourceUnit, source.Unit, StringComparison.Ordinal)
            || !string.Equals(levelFrame.SourceFrameId, source.FrameId, StringComparison.Ordinal)
            || !string.Equals(levelFrame.LevelingTransformEntityId, transform.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(levelFrame.LevelingTransformContentSha256, transform.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Level Surface frame-chain source and transform identities are inconsistent.");
        }

        if (result is not null
            && (!result.IsDerived
                || !string.Equals(result.RootSourceSha256, source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(result.Unit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(result.FrameId, source.FrameId, StringComparison.Ordinal)
                || result.Width != source.Width
                || result.Height != source.Height))
        {
            throw new InvalidDataException("Level Surface frame-chain result must be a source-preserving derived C3D.");
        }

        var sourceNode = new C3DCoordinateFrameNode(
            C3DCoordinateFrameRole.Source,
            source.FrameId,
            source.Unit,
            C3DLevelFrameArtifact.SourceCoordinateConvention,
            source.EntityId,
            source.ContentSha256);
        var referenceNode = new C3DCoordinateFrameNode(
            C3DCoordinateFrameRole.Reference,
            source.FrameId,
            source.Unit,
            C3DLevelFrameArtifact.SourceCoordinateConvention,
            source.EntityId,
            source.ContentSha256,
            transform.ReferenceRegions.Select(region => region.SelectionId));
        var levelNode = new C3DCoordinateFrameNode(
            C3DCoordinateFrameRole.Level,
            levelFrame.LevelFrameId,
            source.Unit,
            C3DLevelFrameArtifact.FrameCoordinateConvention,
            levelFrame.OutputEntityId,
            levelFrame.ContentSha256);
        var resultNode = result is null
            ? null
            : new C3DCoordinateFrameNode(
                C3DCoordinateFrameRole.Result,
                result.FrameId,
                result.Unit,
                C3DLevelFrameArtifact.SourceCoordinateConvention,
                result.EntityId,
                result.ContentSha256);
        var links = new List<C3DCoordinateFrameLink>
        {
            new(
                C3DCoordinateFrameRole.Source,
                C3DCoordinateFrameRole.Reference,
                C3DLevelSurfaceCoordinateFrameChain.SourceToReferenceRelation,
                null,
                null),
            new(
                C3DCoordinateFrameRole.Source,
                C3DCoordinateFrameRole.Level,
                C3DLevelSurfaceCoordinateFrameChain.SourceToLevelRelation,
                levelFrame.OutputEntityId,
                levelFrame.ContentSha256)
        };
        if (result is not null)
        {
            links.Add(new C3DCoordinateFrameLink(
                C3DCoordinateFrameRole.Source,
                C3DCoordinateFrameRole.Result,
                C3DLevelSurfaceCoordinateFrameChain.SourceToResultRelation,
                transform.OutputEntityId,
                transform.ContentSha256));
        }

        return C3DLevelSurfaceCoordinateFrameChain.Create(
            $"{levelFrame.OutputEntityId}.frame-chain",
            sourceNode,
            referenceNode,
            resultNode,
            levelNode,
            links,
            source.EntityId,
            source.RootSourceSha256,
            source.Unit,
            source.FrameId,
            $"{stepId}:{C3DLevelSurfaceCoordinateFrameChain.ChainSemantics}:source={source.ContentSha256}:transform={transform.ContentSha256}:levelFrame={levelFrame.ContentSha256}");
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

    private static ToolResult CreateResult(
        ResultStatus status,
        string message,
        TimeSpan elapsed,
        C3DLevelSurfaceInput input,
        C3DLevelingTransform transform,
        C3DLevelFrameArtifact levelFrame,
        C3DLevelFrameQualityEvidence qualityEvidence,
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
                new Metric("Minimum reference coverage", MetricKind.Deviation, qualityEvidence.MinimumObservedCoverageRatio, "ratio", status),
                new Metric("Target reference height", MetricKind.Length, transform.TargetHeight, input.Source.Unit, status),
                new Metric("Output reference slope X", MetricKind.Deviation, outputSlopeX, $"{input.Source.Unit}/column", status),
                new Metric("Output reference slope Z", MetricKind.Deviation, outputSlopeZ, $"{input.Source.Unit}/row", status),
                new Metric("Output valid sample count", MetricKind.Count, outputValid, "count", status),
                new Metric("Output missing sample count", MetricKind.Count, outputMissing, "count", status),
                new Metric("Level frame determinant", MetricKind.Deviation, Determinant(levelFrame.SourceToFrame), "unitless", status)
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
                    input.OutputEntityId),
                new Overlay(
                    $"overlay.{input.OutputEntityId}.level-frame",
                    OverlayKind.Marker,
                    $"Level Frame {levelFrame.ContentSha256[..12]}",
                    status,
                    levelFrame.OutputEntityId),
                new Overlay(
                    $"overlay.{input.OutputEntityId}.level-frame-quality",
                    OverlayKind.Marker,
                    $"Level Frame quality {qualityEvidence.State} | coverage {qualityEvidence.MinimumObservedCoverageRatio:P1}",
                    status,
                    levelFrame.OutputEntityId)
            ]);

    private static double Determinant(C3DAffineMatrix3x4 matrix) =>
        matrix.M11 * ((matrix.M22 * matrix.M33) - (matrix.M23 * matrix.M32))
        - matrix.M12 * ((matrix.M21 * matrix.M33) - (matrix.M23 * matrix.M31))
        + matrix.M13 * ((matrix.M21 * matrix.M32) - (matrix.M22 * matrix.M31));

}
