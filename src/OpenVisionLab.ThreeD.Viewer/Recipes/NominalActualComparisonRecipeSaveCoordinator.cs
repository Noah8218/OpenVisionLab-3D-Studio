using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns Nominal/Actual recipe save eligibility, construction, persistence, and
/// saved-state projection without depending on the WPF Viewer control.
/// </summary>
public static class NominalActualComparisonRecipeSaveCoordinator
{
    public static bool CanSave(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var input = viewModel.NominalActualInput;
        return input is not null
            && (!viewModel.RecipeOutputEnabled
                || (viewModel.NominalActual.PreviewResult is { } result
                    && viewModel.NominalActual.State is (NominalActualComparisonState.PreviewReady
                        or NominalActualComparisonState.Published)
                    && result.Input.ExecutionFingerprint.Equals(
                        viewModel.NominalActual.CompletedPreviewFingerprint,
                        StringComparison.Ordinal)));
    }

    public static bool Save(
        string path,
        bool isSmoke,
        MainWindowViewModel viewModel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(viewModel);

        try
        {
            if (!CanSave(viewModel))
            {
                viewModel.ViewerStatus = viewModel.RecipeOutputEnabled
                    ? "Nominal/actual recipe save requires a current completed Preview"
                    : "Nominal/actual recipe save requires connected comparison inputs when output is disabled";
                return false;
            }

            var input = viewModel.NominalActualInput!;
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = NominalActualComparisonRecipe.FromInput(input, fullRecipePath) with
            {
                OutputEnabled = viewModel.RecipeOutputEnabled
            };
            recipe.Save(fullRecipePath);
            viewModel.SetNominalActualRecipeSaved(fullRecipePath);
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke nominal/actual recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Nominal/actual recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus =
                $"{(isSmoke ? "Smoke nominal/actual recipe save" : "Nominal/actual recipe save")} failed: {ex.Message}";
            return false;
        }
    }
}
