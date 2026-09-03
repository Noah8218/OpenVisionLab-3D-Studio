using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Volume Preview guards, sample preparation, and rule evaluation.
/// Viewer-specific overlay geometry and rendering remain callbacks owned by the Viewer.
/// </summary>
public static class C3DVolumeRuleCoordinator
{
    public static bool Preview(
        C3DHeightGrid? grid,
        MainWindowViewModel viewModel,
        Action<HeightDeviationRecipeVolume, VolumeEvaluation, double> applyPreviewOverlay,
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
            viewModel.ViewerStatus = "Volume requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreateVolumeRecipeStep();
        C3DHeightGrid measurementGrid;
        try
        {
            measurementGrid = grid.WithMaxRenderedPoints(step.MaxSampledPoints);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Volume sample load failed: {exception.Message}";
            return false;
        }

        var transform = viewModel.C3DModelTransform;
        var samples = measurementGrid.Points
            .Select(point => new HeightFieldPlaneSample(transform.Apply(point.Position), point.RawValue))
            .ToArray();
        var reference = samples.Where(sample => Contains(step.ReferenceRegion, sample.Position)).ToArray();
        var measured = samples.Where(sample => Contains(step.MeasurementRegion, sample.Position)).ToArray();
        var spacing = measurementGrid.HorizontalScale * measurementGrid.PointStride * transform.Scale;
        var evaluation = VolumeRule.Evaluate(new VolumeRuleInput(
            step.SourceEntityId,
            reference,
            measured,
            spacing * spacing,
            step.ExpectedNetVolume,
            step.Tolerance,
            step.Unit));

        var meanY = measured.Length == 0 ? 0.0 : measured.Average(sample => sample.Position.Y);
        applyPreviewOverlay(step, evaluation, meanY);
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        viewModel.SetVolumePreview(evaluation);
        requestPreviewRender();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    private static bool Contains(HeightDeviationRecipeRoiRegion region, Vector3 point) =>
        point.X >= region.CenterX - region.HalfWidth
        && point.X <= region.CenterX + region.HalfWidth
        && point.Z >= region.CenterZ - region.HalfDepth
        && point.Z <= region.CenterZ + region.HalfDepth;
}
