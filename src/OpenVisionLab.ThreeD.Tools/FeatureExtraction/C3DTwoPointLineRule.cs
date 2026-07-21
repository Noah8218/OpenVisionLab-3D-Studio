using System.Diagnostics;
using NoahPoint = Lib.ThreeD.FeatureExtraction.ThreeDPoint;
using NoahTwoPointLineInput = Lib.ThreeD.FeatureExtraction.TwoPointLineInput;
using NoahTwoPointLineTool = Lib.ThreeD.FeatureExtraction.TwoPointLineTool;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DTwoPointLineInput(
    string StepId,
    C3DHeightFieldSnapshot RawSource,
    ToolRecipeSelection PointSelection,
    string OutputEntityId,
    string OutputRole);

public sealed record C3DTwoPointLineEvaluation(ToolResult Result, C3DTwoPointLineFeature? Output);

/// <summary>
/// Typed Studio adapter for Library-Noah's ordered two-point construction.
/// It resolves current raw C3D values only; it never trusts the captured
/// height as current geometry, fits a line, or creates a measurement result.
/// </summary>
public static class C3DTwoPointLineRule
{
    public static C3DTwoPointLineEvaluation Evaluate(C3DTwoPointLineInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var points = input.PointSelection.Points!;
            var first = ResolvePoint(input.RawSource, points[0].Locator);
            var second = ResolvePoint(input.RawSource, points[1].Locator);
            var noahResult = new NoahTwoPointLineTool().Execute(
                new NoahTwoPointLineInput(first, second),
                cancellationToken);
            if (!noahResult.Success || noahResult.Anchor is null || noahResult.Direction is null || noahResult.SegmentEnd is null)
            {
                throw new InvalidDataException(noahResult.Message);
            }

            var selectionHash = C3DTwoPointLineFeature.CalculateSelectionContentSha256(input.PointSelection);
            var output = C3DTwoPointLineFeature.Create(
                input.OutputEntityId,
                input.RawSource.EntityId,
                input.RawSource.RootSourceSha256,
                input.RawSource.Unit,
                input.RawSource.FrameId,
                input.PointSelection.Id,
                selectionHash,
                points[0].Locator.Row,
                points[0].Locator.Column,
                points[1].Locator.Row,
                points[1].Locator.Column,
                noahResult.Anchor.X,
                noahResult.Anchor.Y,
                noahResult.Anchor.Z,
                noahResult.Direction.X,
                noahResult.Direction.Y,
                noahResult.Direction.Z,
                noahResult.SegmentEnd.X,
                noahResult.SegmentEnd.Y,
                noahResult.SegmentEnd.Z,
                noahResult.SegmentLength,
                input.OutputRole,
                $"{input.StepId}:TwoPointLine:{C3DTwoPointLineFeature.ContractVersion}:policy={C3DTwoPointLineFeature.ConstructionPolicyName}:selection={selectionHash}:source={input.RawSource.RootSourceSha256}");
            stopwatch.Stop();
            return new C3DTwoPointLineEvaluation(
                new ToolResult(
                    "2-Point Line",
                    ResultStatus.Pass,
                    "Completed - ordered raw-C3D source-coordinate line evidence; no fitting or acceptance rule was evaluated.",
                    stopwatch.Elapsed,
                    [new Metric("Segment length", MetricKind.Deviation, output.SegmentLength, "source-coordinate")],
                    [new Overlay(input.OutputEntityId, OverlayKind.Polyline, "Ordered two-point full-XYZ segment", SourceEntityId: input.RawSource.EntityId)]),
                output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or OverflowException)
        {
            stopwatch.Stop();
            return new C3DTwoPointLineEvaluation(
                new ToolResult("2-Point Line", ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }

    private static NoahPoint ResolvePoint(C3DHeightFieldSnapshot source, ToolRecipeGridCellLocator locator)
    {
        var height = source.Values.Span[checked(locator.Row * source.Width + locator.Column)];
        if (!double.IsFinite(height))
        {
            throw new InvalidDataException($"2-Point Line selected cell ({locator.Row}, {locator.Column}) has no current finite raw-height value.");
        }
        return new NoahPoint(locator.Column, height, locator.Row);
    }

    private static void Validate(C3DTwoPointLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.RawSource);
        ArgumentNullException.ThrowIfNull(input.PointSelection);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputRole);
        if (input.RawSource.IsDerived)
        {
            throw new InvalidDataException("2-Point Line v1 accepts only the recipe-bound raw C3D height field.");
        }
        var selection = input.PointSelection;
        var points = selection.Points;
        if (!string.Equals(selection.Kind, ToolRecipeSelectionKinds.PointSet, StringComparison.Ordinal)
            || points is null
            || points.Count != 2)
        {
            throw new InvalidDataException("2-Point Line v1 requires exactly one recipe-owned PointSet with exactly two points.");
        }
        if (!string.Equals(selection.RootSourceId, input.RawSource.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(selection.FrameId, input.RawSource.FrameId, StringComparison.Ordinal)
            || !string.Equals(selection.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(selection.SourceBinding.ContentSha256, input.RawSource.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || selection.SourceBinding.GridWidth != input.RawSource.Width
            || selection.SourceBinding.GridHeight != input.RawSource.Height)
        {
            throw new InvalidDataException("2-Point Line PointSet source identity does not match the current raw C3D source.");
        }
        foreach (var point in points)
        {
            if (point?.Locator is null
                || !string.Equals(point.Locator.Kind, "grid-cell", StringComparison.Ordinal)
                || point.Locator.Row < 0 || point.Locator.Row >= input.RawSource.Height
                || point.Locator.Column < 0 || point.Locator.Column >= input.RawSource.Width)
            {
                throw new InvalidDataException("2-Point Line PointSet contains an invalid current C3D grid-cell locator.");
            }
        }
        if (points[0].Locator.Row == points[1].Locator.Row && points[0].Locator.Column == points[1].Locator.Column)
        {
            throw new InvalidDataException("2-Point Line PointSet requires two distinct grid cells.");
        }
        if (string.Equals(input.OutputEntityId, input.RawSource.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, selection.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("2-Point Line output ID must differ from its source and PointSet inputs.");
        }
    }
}
