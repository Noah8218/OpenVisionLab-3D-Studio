using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Cross-section Dimensions Preview guards, source-row preparation,
/// and rule evaluation. Profile path formatting remains a Viewer callback.
/// </summary>
public static class C3DCrossSectionRuleCoordinator
{
    public static bool Preview(
        C3DHeightGrid? grid,
        MainWindowViewModel viewModel,
        Action<HeightDeviationRecipeCrossSection, HeightGridPoint[], double, double, double> applySectionProfile,
        Action requestPreviewRender)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(applySectionProfile);
        ArgumentNullException.ThrowIfNull(requestPreviewRender);

        if (!viewModel.RecipeOutputEnabled)
        {
            viewModel.ViewerStatus = "Recipe output is disabled; Preview and Publish did not run";
            return false;
        }

        if (grid is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Cross-section Dimensions requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreateCrossSectionRecipeStep();
        HeightGridPoint[] sourcePoints;
        try
        {
            sourcePoints = grid.ReadRowRange(step.Row, step.StartColumn, step.EndColumn);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            viewModel.ViewerStatus = $"Cross-section source read failed: {exception.Message}";
            return false;
        }

        var transform = viewModel.C3DModelTransform;
        var samples = sourcePoints
            .Select(point => new CrossSectionSample(point.Column, transform.Apply(point.Position), point.RawValue))
            .ToArray();
        var evaluation = CrossSectionDimensionsRule.Evaluate(new CrossSectionDimensionsInput(
            step.SourceEntityId,
            step.Row,
            step.StartColumn,
            step.EndColumn,
            samples,
            step.ExpectedWidth,
            step.WidthTolerance,
            step.ExpectedHeightRange,
            step.HeightTolerance,
            step.WidthUnit,
            step.HeightUnit));

        if (sourcePoints.Length >= 2)
        {
            var minimum = sourcePoints.Min(point => point.RawValue);
            var maximum = sourcePoints.Max(point => point.RawValue);
            var mean = sourcePoints.Average(point => point.RawValue);
            applySectionProfile(step, sourcePoints, minimum, maximum, mean);
        }

        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        viewModel.SetCrossSectionPreview(evaluation);
        requestPreviewRender();
        return evaluation.Result.Status != ResultStatus.Error;
    }
}
