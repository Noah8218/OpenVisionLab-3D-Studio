using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Thickness Preview guards and delegates scalar evaluation to the
/// existing Tools rule. Rendering remains a callback owned by the Viewer.
/// </summary>
internal static class C3DThicknessRuleCoordinator
{
    public static bool Preview(
        C3DHeightGrid? grid,
        MainWindowViewModel viewModel,
        Action requestPreviewRender)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(requestPreviewRender);

        if (!viewModel.RecipeOutputEnabled)
        {
            viewModel.ViewerStatus = "Recipe output is disabled; Preview and Publish did not run";
            return false;
        }

        if (grid is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Thickness requires a visible C3D height grid";
            return false;
        }

        if (!viewModel.ThicknessConfigured)
        {
            viewModel.ViewerStatus = "Thickness requires one taught C3D grid ROI";
            return false;
        }

        var step = viewModel.CreateThicknessRecipeStep();
        C3DThicknessEvaluation evaluation;
        try
        {
            evaluation = Evaluate(grid, step);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Thickness sample load failed: {ex.Message}";
            return false;
        }

        viewModel.SetThicknessPreview(evaluation);
        requestPreviewRender();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    internal static C3DThicknessEvaluation Evaluate(
        C3DHeightGrid grid,
        C3DThicknessStep step)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(step);

        return C3DThicknessRule.Evaluate(new C3DThicknessInput(
            step.SourceEntityId,
            grid.Height,
            grid.Width,
            grid.ReadHeightMapValues(),
            step.Roi,
            step.Acceptance,
            step.Unit,
            step.FrameId,
            step.MinimumValidSamples));
    }
}
