using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Warpage Preview guards and delegates scalar evaluation to the
/// existing Tools rule. Rendering remains a callback owned by the Viewer.
/// </summary>
internal static class C3DWarpageRuleCoordinator
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
            viewModel.ViewerStatus = "Warpage requires a visible C3D height grid";
            return false;
        }

        if (!viewModel.WarpageConfigured)
        {
            viewModel.ViewerStatus = "Warpage requires one taught C3D grid ROI";
            return false;
        }

        var step = viewModel.CreateWarpageRecipeStep();
        C3DWarpageEvaluation evaluation;
        try
        {
            evaluation = Evaluate(grid, step);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Warpage sample load failed: {exception.Message}";
            return false;
        }

        viewModel.SetWarpagePreview(evaluation);
        requestPreviewRender();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    internal static C3DWarpageEvaluation Evaluate(
        C3DHeightGrid grid,
        C3DWarpageStep step)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(step);

        return C3DWarpageRule.Evaluate(new C3DWarpageInput(
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
