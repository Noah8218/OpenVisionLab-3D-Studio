using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Plane Flatness Preview guards, sample preparation, and rule evaluation.
/// Viewer-specific extrema/plane geometry and rendering remain a View callback.
/// </summary>
public static class C3DPlaneFlatnessRuleCoordinator
{
    public static bool Preview(
        C3DHeightGrid? grid,
        MainWindowViewModel viewModel,
        Action<HeightDeviationRecipePlaneFlatness, PlaneFlatnessEvaluation> applyPreviewOverlay,
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
            viewModel.ViewerStatus = "Plane flatness requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreatePlaneFlatnessRecipeStep();
        C3DHeightGrid measurementSample;
        try
        {
            measurementSample = grid.WithMaxRenderedPoints(step.MaxSampledPoints);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Plane flatness sample load failed: {exception.Message}";
            return false;
        }

        var transform = viewModel.C3DModelTransform;
        var measurementSamples = measurementSample.Points
            .Select(point => new HeightFieldPlaneSample(transform.Apply(point.Position), point.RawValue))
            .ToArray();
        var referenceSamples = measurementSamples
            .Where(sample => Contains(step.ReferenceRegion, sample.Position))
            .ToArray();
        var evaluation = PlaneFlatnessRule.Evaluate(new PlaneFlatnessRuleInput(
            step.SourceEntityId,
            referenceSamples,
            measurementSamples,
            step.Tolerance,
            step.Unit));

        viewModel.ClearTwoPointMeasurement();
        viewModel.ClearPlaneReferenceMeasurement();
        viewModel.ClearRoiStepMeasurement();
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        applyPreviewOverlay(step, evaluation);
        viewModel.SetPlaneFlatnessPreview(evaluation);
        requestPreviewRender();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    private static bool Contains(HeightDeviationRecipeRoiRegion region, Vector3 point) =>
        point.X >= region.CenterX - region.HalfWidth
        && point.X <= region.CenterX + region.HalfWidth
        && point.Z >= region.CenterZ - region.HalfDepth
        && point.Z <= region.CenterZ + region.HalfDepth;
}
