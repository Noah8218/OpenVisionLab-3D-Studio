using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Gap / Flush Preview guards and transform-aware ROI statistics,
/// then delegates the decision to the existing Tools rule. ROI geometry and
/// rendering remain callbacks owned by the Viewer.
/// </summary>
public static class C3DGapFlushRuleCoordinator
{
    public static bool Preview(
        C3DHeightGrid? grid,
        MainWindowViewModel viewModel,
        Action<C3DGapFlushStep, GapFlushRegionStats, GapFlushRegionStats> applyPreviewOverlay,
        Action requestPreviewRender)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(applyPreviewOverlay);
        ArgumentNullException.ThrowIfNull(requestPreviewRender);

        if (!viewModel.RecipeOutputEnabled)
        {
            viewModel.ViewerStatus = "Recipe output is disabled; Preview and Publish did not run";
            return false;
        }

        if (grid is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Gap / Flush requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreateGapFlushRecipeStep();
        C3DHeightGrid measurementSample;
        try
        {
            measurementSample = grid.WithMaxRenderedPoints(step.MaxSampledPoints);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Gap / Flush sample load failed: {exception.Message}";
            return false;
        }

        var transform = viewModel.C3DModelTransform;
        var left = CalculateStats(measurementSample.Points, step.LeftRegion, transform);
        var right = CalculateStats(measurementSample.Points, step.RightRegion, transform);
        var evaluation = GapFlushRule.Evaluate(new GapFlushInput(
            step.SourceEntityId,
            step.LeftRegion,
            step.RightRegion,
            left,
            right,
            step.Acceptance,
            step.GapUnit,
            step.FlushUnit));

        applyPreviewOverlay(step, left, right);
        viewModel.SetRoiStepMeasurement(
            left.PointCount,
            left.RawMean,
            left.ModelYMean,
            right.PointCount,
            right.RawMean,
            right.ModelYMean,
            "GapFlush");
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        viewModel.SetGapFlushPreview(evaluation);
        requestPreviewRender();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    public static GapFlushRegionStats CalculateStats(
        IReadOnlyList<HeightGridPoint> points,
        HeightDeviationRecipeRoiRegion region,
        ModelTransform transform)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(region);

        var count = 0;
        var rawSum = 0.0;
        var modelYSum = 0.0;
        foreach (var point in points)
        {
            var position = transform.Apply(point.Position);
            if (!Contains(region, position))
            {
                continue;
            }

            count++;
            rawSum += point.RawValue;
            modelYSum += position.Y;
        }

        return count == 0
            ? new GapFlushRegionStats(0, double.NaN, double.NaN)
            : new GapFlushRegionStats(count, rawSum / count, modelYSum / count);
    }

    private static bool Contains(HeightDeviationRecipeRoiRegion region, Vector3 point) =>
        point.X >= region.CenterX - region.HalfWidth
        && point.X <= region.CenterX + region.HalfWidth
        && point.Z >= region.CenterZ - region.HalfDepth
        && point.Z <= region.CenterZ + region.HalfDepth;
}
