using System.Diagnostics;
using NoahPoint = Lib.ThreeD.FeatureExtraction.ThreeDPoint;
using NoahThreePointPlaneInput = Lib.ThreeD.FeatureExtraction.ThreePointPlaneInput;
using NoahThreePointPlaneTool = Lib.ThreeD.FeatureExtraction.ThreePointPlaneTool;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DThreePointPlaneInput(
    string StepId,
    C3DHeightFieldSnapshot RawSource,
    ToolRecipeSelection PointSelection,
    string OutputEntityId,
    string OutputRole);

public sealed record C3DThreePointPlaneEvaluation(ToolResult Result, C3DThreePointPlaneFeature? Output);

/// <summary>
/// Typed Studio adapter for Library-Noah's ordered three-point construction.
/// It resolves current raw C3D values only; it never fits a region, applies a
/// plane, or creates a measurement/acceptance result.
/// </summary>
public static class C3DThreePointPlaneRule
{
    public static C3DThreePointPlaneEvaluation Evaluate(C3DThreePointPlaneInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var points = input.PointSelection.Points!;
            var first = ResolvePoint(input.RawSource, points[0].Locator);
            var second = ResolvePoint(input.RawSource, points[1].Locator);
            var third = ResolvePoint(input.RawSource, points[2].Locator);
            var noahResult = new NoahThreePointPlaneTool().Execute(
                new NoahThreePointPlaneInput(first, second, third),
                cancellationToken);
            if (!noahResult.Success || noahResult.Anchor is null || noahResult.Normal is null
                || noahResult.SupportSecond is null || noahResult.SupportThird is null)
            {
                throw new InvalidDataException(noahResult.Message);
            }

            var selectionHash = C3DThreePointPlaneFeature.CalculateSelectionContentSha256(input.PointSelection);
            var output = C3DThreePointPlaneFeature.Create(
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
                points[2].Locator.Row,
                points[2].Locator.Column,
                noahResult.Anchor.X,
                noahResult.Anchor.Y,
                noahResult.Anchor.Z,
                noahResult.Normal.X,
                noahResult.Normal.Y,
                noahResult.Normal.Z,
                noahResult.PlaneOffset,
                noahResult.SupportSecond.X,
                noahResult.SupportSecond.Y,
                noahResult.SupportSecond.Z,
                noahResult.SupportThird.X,
                noahResult.SupportThird.Y,
                noahResult.SupportThird.Z,
                noahResult.NormalizedCrossMagnitude,
                input.OutputRole,
                $"{input.StepId}:ThreePointPlane:{C3DThreePointPlaneFeature.ContractVersion}:policy={C3DThreePointPlaneFeature.ConstructionPolicyName}:selection={selectionHash}:source={input.RawSource.RootSourceSha256}");
            stopwatch.Stop();
            return new C3DThreePointPlaneEvaluation(
                new ToolResult(
                    "3-Point Plane",
                    ResultStatus.Pass,
                    "Completed - ordered raw-C3D source-coordinate datum plane evidence; no fit, measurement, or OK/NG was evaluated.",
                    stopwatch.Elapsed,
                    [new Metric("Normalized support area", MetricKind.Number, output.NormalizedCrossMagnitude, "unitless")],
                    [new Overlay(input.OutputEntityId, OverlayKind.Plane, "Ordered three-point support triangle and normal", SourceEntityId: input.RawSource.EntityId)]),
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
            return new C3DThreePointPlaneEvaluation(
                new ToolResult("3-Point Plane", ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }

    private static NoahPoint ResolvePoint(C3DHeightFieldSnapshot source, ToolRecipeGridCellLocator locator)
    {
        var height = source.Values.Span[checked(locator.Row * source.Width + locator.Column)];
        if (!double.IsFinite(height))
        {
            throw new InvalidDataException($"3-Point Plane selected cell ({locator.Row}, {locator.Column}) has no current finite raw-height value.");
        }
        return new NoahPoint(locator.Column, height, locator.Row);
    }

    private static void Validate(C3DThreePointPlaneInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.RawSource);
        ArgumentNullException.ThrowIfNull(input.PointSelection);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputRole);
        if (input.RawSource.IsDerived)
        {
            throw new InvalidDataException("3-Point Plane v1 accepts only the recipe-bound raw C3D height field.");
        }
        var selection = input.PointSelection;
        var points = selection.Points;
        if (!string.Equals(selection.Kind, ToolRecipeSelectionKinds.PointSet, StringComparison.Ordinal)
            || points is null
            || points.Count != 3)
        {
            throw new InvalidDataException("3-Point Plane v1 requires exactly one recipe-owned PointSet with exactly three points.");
        }
        if (!string.Equals(selection.RootSourceId, input.RawSource.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(selection.FrameId, input.RawSource.FrameId, StringComparison.Ordinal)
            || !string.Equals(selection.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(selection.SourceBinding.ContentSha256, input.RawSource.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || selection.SourceBinding.GridWidth != input.RawSource.Width
            || selection.SourceBinding.GridHeight != input.RawSource.Height)
        {
            throw new InvalidDataException("3-Point Plane PointSet source identity does not match the current raw C3D source.");
        }
        var locators = new HashSet<(int Row, int Column)>();
        foreach (var point in points)
        {
            if (point?.Locator is null
                || !string.Equals(point.Locator.Kind, "grid-cell", StringComparison.Ordinal)
                || point.Locator.Row < 0 || point.Locator.Row >= input.RawSource.Height
                || point.Locator.Column < 0 || point.Locator.Column >= input.RawSource.Width
                || !locators.Add((point.Locator.Row, point.Locator.Column)))
            {
                throw new InvalidDataException("3-Point Plane PointSet contains an invalid or duplicate current C3D grid-cell locator.");
            }
        }
        if (string.Equals(input.OutputEntityId, input.RawSource.EntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, selection.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("3-Point Plane output ID must differ from its source and PointSet inputs.");
        }
    }
}
