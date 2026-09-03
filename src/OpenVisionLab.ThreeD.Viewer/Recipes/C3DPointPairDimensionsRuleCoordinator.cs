using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Point Pair Dimensions Preview guards and delegates dimension
/// evaluation to the existing Tools rule. Measurement mutation and rendering
/// remain callbacks owned by the Viewer.
/// </summary>
internal static class C3DPointPairDimensionsRuleCoordinator
{
    public static bool Preview(
        C3DHeightGrid? grid,
        MainWindowViewModel viewModel,
        Action<HeightGridPoint, HeightGridPoint> applyPointPairMeasurement,
        Action requestPreviewRender)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(applyPointPairMeasurement);
        ArgumentNullException.ThrowIfNull(requestPreviewRender);

        if (!viewModel.RecipeOutputEnabled)
        {
            viewModel.ViewerStatus = "Recipe output is disabled; Preview and Publish did not run";
            return false;
        }

        if (grid is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Point pair dimensions require a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreatePointPairDimensionsRecipeStep();
        if (step is null)
        {
            viewModel.ViewerStatus = "Point pair dimensions require two selected C3D source cells";
            return false;
        }

        HeightGridPoint first;
        HeightGridPoint second;
        try
        {
            first = grid.ReadPoint(step.First.Row, step.First.Column);
            second = grid.ReadPoint(step.Second.Row, step.Second.Column);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException)
        {
            viewModel.ViewerStatus = $"Point pair dimensions failed: {ex.Message}";
            return false;
        }

        applyPointPairMeasurement(first, second);
        var transform = viewModel.C3DModelTransform;
        var evaluation = Evaluate(
            step,
            first,
            second,
            transform,
            viewModel.RecipeSourceUnit);
        viewModel.SetPointPairDimensionsPreview(evaluation);
        requestPreviewRender();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    internal static PointPairDimensionsEvaluation Evaluate(
        C3DPointPairDimensionsStep step,
        HeightGridPoint first,
        HeightGridPoint second,
        ModelTransform transform,
        string rawHeightUnit)
    {
        ArgumentNullException.ThrowIfNull(step);

        return PointPairDimensionsRule.Evaluate(new PointPairDimensionsInput(
            step.SourceEntityId,
            transform.Apply(first.Position),
            transform.Apply(second.Position),
            first.RawValue,
            second.RawValue,
            step.Acceptance,
            step.Unit,
            rawHeightUnit));
    }
}
