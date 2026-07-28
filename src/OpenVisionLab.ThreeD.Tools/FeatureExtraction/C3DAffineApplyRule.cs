using System.Diagnostics;
using NoahAffineMatrix = Lib.ThreeD.FeatureExtraction.FullXyzAffineMatrix;
using NoahApplyInputPoint = Lib.ThreeD.FeatureExtraction.AffinePointCloudInputPoint;
using NoahApplyTool = Lib.ThreeD.FeatureExtraction.AffinePointCloudApplyTool;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DAffineApplyInput(
    string StepId,
    C3DHeightFieldSnapshot RawSource,
    C3DAffineTransform3D PublishedAffineTransform,
    string OutputEntityId);

public sealed record C3DAffineApplyEvaluation(ToolResult Result, C3DTransformedPointCloud? Output);

/// <summary>
/// Strict Studio adapter for Library-Noah's finite ordered-point transform.
/// It applies an already-published A1 matrix once and deliberately produces no
/// re-gridded height field, mesh, or measurement result.
/// </summary>
public static class C3DAffineApplyRule
{
    public const string ToolName = "Apply XYZ Affine";
    public const string SourceCoordinateConvention = "column-rawHeight-row";

    public static C3DAffineApplyEvaluation Evaluate(C3DAffineApplyInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            var sourcePoints = new List<NoahApplyInputPoint>(input.RawSource.ValidCount);
            var values = input.RawSource.Values.Span;
            for (var row = 0; row < input.RawSource.Height; row++)
            {
                for (var column = 0; column < input.RawSource.Width; column++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var height = values[checked(row * input.RawSource.Width + column)];
                    if (!double.IsFinite(height)) continue;
                    sourcePoints.Add(new NoahApplyInputPoint(
                        row,
                        column,
                        height,
                        column,
                        height,
                        row));
                }
            }

            var noahResult = new NoahApplyTool().Execute(sourcePoints, ToNoahMatrix(input.PublishedAffineTransform.Matrix), cancellationToken);
            if (!noahResult.Success)
            {
                throw new InvalidDataException(noahResult.Message);
            }

            var points = noahResult.Points
                .Select(point => new C3DTransformedPoint(
                    point.Row,
                    point.Column,
                    point.RawHeight,
                    point.TransformedX,
                    point.TransformedY,
                    point.TransformedZ))
                .ToArray();
            var output = C3DTransformedPointCloud.Create(
                input.OutputEntityId,
                input.RawSource.EntityId,
                input.RawSource.RootSourceSha256,
                input.RawSource.Unit,
                input.RawSource.FrameId,
                SourceCoordinateConvention,
                input.RawSource.Width,
                input.RawSource.Height,
                input.PublishedAffineTransform,
                points,
                $"{input.StepId}:ApplyXYZAffine:{C3DTransformedPointCloud.ContractVersion}:source={input.RawSource.RootSourceSha256}:affine={input.PublishedAffineTransform.ContentSha256}");
            stopwatch.Stop();
            return new C3DAffineApplyEvaluation(
                new ToolResult(
                    ToolName,
                    ResultStatus.Pass,
                    "Completed full-XYZ point application to finite raw C3D points. The output is not re-gridded and no measurement was evaluated.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Finite transformed points", MetricKind.Number, output.FinitePointCount, "points"),
                        new Metric("Missing source cells", MetricKind.Number, output.MissingPointCount, "cells")
                    ],
                    [new Overlay(output.OutputEntityId, OverlayKind.Polyline, "Transformed source-grid edge evidence", SourceEntityId: input.RawSource.EntityId)]),
                output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            stopwatch.Stop();
            return new C3DAffineApplyEvaluation(new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []), null);
        }
    }

    private static void Validate(C3DAffineApplyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentNullException.ThrowIfNull(input.RawSource);
        ArgumentNullException.ThrowIfNull(input.PublishedAffineTransform);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        if (input.RawSource.IsDerived)
        {
            throw new InvalidDataException("Apply XYZ Affine v1 accepts only the verified recipe-bound raw C3D source.");
        }
        if (!string.Equals(input.RawSource.EntityId, input.PublishedAffineTransform.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.RawSource.RootSourceSha256, input.PublishedAffineTransform.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.RawSource.Unit, input.PublishedAffineTransform.SourceUnit, StringComparison.Ordinal)
            || !string.Equals(input.RawSource.FrameId, input.PublishedAffineTransform.SourceFrameId, StringComparison.Ordinal)
            || !string.Equals(input.PublishedAffineTransform.SourceCoordinateConvention, SourceCoordinateConvention, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Apply XYZ Affine source identity/frame/unit/convention does not match the current Published AffineTransform3D.");
        }
    }

    private static NoahAffineMatrix ToNoahMatrix(C3DAffineMatrix3x4 matrix) => new(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34);
}
