using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

internal readonly record struct ViewerRecipeValidationResult(bool IsValid, string Warning);

/// <summary>
/// Owns the Viewer recipe-save route, capability precedence, and persistence
/// dispatch. The WPF control supplies the current state and validation/render
/// adapters; this owner keeps the Host SaveRecipe contract away from View code.
/// </summary>
internal sealed class ViewerRecipeSaveWorkflow
{
    private readonly MainWindowViewModel viewModel;
    private readonly Func<C3DHeightGrid?> c3dSample;
    private readonly Func<LazPointCloud?> lazPointCloud;
    private readonly Func<bool> canSaveLazTwoPointRecipe;
    private readonly Func<bool> hasLazTwoPointMeasurement;
    private readonly Func<bool> requiresRoiForCurrentRecipe;
    private readonly Func<bool, ViewerRecipeValidationResult> validateRecipeState;
    private readonly Func<ViewerRecipeValidationResult> validatePlaneFlatnessRecipeState;
    private readonly Func<HeightDeviationRecipeRoiStep?> createCurrentRoiStepRecipe;
    private readonly Func<string> resolveCurrentRecipeSourcePath;
    private readonly Action setRecipeValidationOk;
    private readonly Action<string> setRecipeValidationWarning;

    public ViewerRecipeSaveWorkflow(
        MainWindowViewModel viewModel,
        Func<C3DHeightGrid?> c3dSample,
        Func<LazPointCloud?> lazPointCloud,
        Func<bool> canSaveLazTwoPointRecipe,
        Func<bool> hasLazTwoPointMeasurement,
        Func<bool> requiresRoiForCurrentRecipe,
        Func<bool, ViewerRecipeValidationResult> validateRecipeState,
        Func<ViewerRecipeValidationResult> validatePlaneFlatnessRecipeState,
        Func<HeightDeviationRecipeRoiStep?> createCurrentRoiStepRecipe,
        Func<string> resolveCurrentRecipeSourcePath,
        Action setRecipeValidationOk,
        Action<string> setRecipeValidationWarning)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.c3dSample = c3dSample ?? throw new ArgumentNullException(nameof(c3dSample));
        this.lazPointCloud = lazPointCloud ?? throw new ArgumentNullException(nameof(lazPointCloud));
        this.canSaveLazTwoPointRecipe = canSaveLazTwoPointRecipe ?? throw new ArgumentNullException(nameof(canSaveLazTwoPointRecipe));
        this.hasLazTwoPointMeasurement = hasLazTwoPointMeasurement ?? throw new ArgumentNullException(nameof(hasLazTwoPointMeasurement));
        this.requiresRoiForCurrentRecipe = requiresRoiForCurrentRecipe ?? throw new ArgumentNullException(nameof(requiresRoiForCurrentRecipe));
        this.validateRecipeState = validateRecipeState ?? throw new ArgumentNullException(nameof(validateRecipeState));
        this.validatePlaneFlatnessRecipeState = validatePlaneFlatnessRecipeState ?? throw new ArgumentNullException(nameof(validatePlaneFlatnessRecipeState));
        this.createCurrentRoiStepRecipe = createCurrentRoiStepRecipe ?? throw new ArgumentNullException(nameof(createCurrentRoiStepRecipe));
        this.resolveCurrentRecipeSourcePath = resolveCurrentRecipeSourcePath ?? throw new ArgumentNullException(nameof(resolveCurrentRecipeSourcePath));
        this.setRecipeValidationOk = setRecipeValidationOk ?? throw new ArgumentNullException(nameof(setRecipeValidationOk));
        this.setRecipeValidationWarning = setRecipeValidationWarning ?? throw new ArgumentNullException(nameof(setRecipeValidationWarning));
    }

    public ViewerRecipeSavePlan ResolveCurrentRecipeSavePlan() =>
        ViewerRecipeSavePlan.Resolve(
            ShouldSaveCurrentNominalActualRecipe(),
            ShouldSaveCurrentLazTwoPointRecipe(),
            C3DWarpageRecipeSaveCoordinator.CanSave(c3dSample(), viewModel),
            C3DThicknessRecipeSaveCoordinator.CanSave(c3dSample(), viewModel),
            C3DGapFlushRecipeSaveCoordinator.CanSave(c3dSample(), viewModel),
            C3DPointPairDimensionsRecipeSaveCoordinator.CanSave(c3dSample(), viewModel));

    public bool Save(string path, bool isSmoke)
    {
        var savePlan = ResolveCurrentRecipeSavePlan();
        return savePlan.Route switch
        {
            ViewerRecipeSaveRoute.NominalActual => SaveCurrentNominalActualRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.LazTwoPoint => SaveCurrentLazTwoPointRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.Warpage => SaveCurrentWarpageRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.Thickness => SaveCurrentThicknessRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.GapFlush => SaveCurrentGapFlushRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.PointPairDimensions => SaveCurrentPointPairDimensionsRecipe(path, isSmoke),
            _ => SaveCurrentHeightDeviationRecipe(path, isSmoke)
        };
    }

    private bool ShouldSaveCurrentNominalActualRecipe() =>
        viewModel.NominalActualInput is not null
        && (!viewModel.RecipeOutputEnabled
            || (viewModel.NominalActual.PreviewResult is not null
                && viewModel.NominalActual.State is NominalActualComparisonState.PreviewReady
                    or NominalActualComparisonState.Published));

    private bool ShouldSaveCurrentLazTwoPointRecipe() =>
        canSaveLazTwoPointRecipe();

    private bool SaveCurrentNominalActualRecipe(string path, bool isSmoke) =>
        NominalActualComparisonRecipeSaveCoordinator.Save(path, isSmoke, viewModel);

    private bool SaveCurrentHeightDeviationRecipe(string path, bool isSmoke)
    {
        var validation = validateRecipeState(requiresRoiForCurrentRecipe());
        if (!validation.IsValid)
        {
            setRecipeValidationWarning(validation.Warning);
            viewModel.ViewerStatus = validation.Warning;
            return false;
        }

        if (viewModel.PlaneFlatnessConfigured)
        {
            var planeValidation = validatePlaneFlatnessRecipeState();
            if (!planeValidation.IsValid)
            {
                setRecipeValidationWarning(planeValidation.Warning);
                viewModel.ViewerStatus = planeValidation.Warning;
                return false;
            }
        }

        try
        {
            var saved = HeightDeviationRecipeSaveCoordinator.Save(
                path,
                isSmoke,
                viewModel,
                resolveCurrentRecipeSourcePath(),
                createCurrentRoiStepRecipe(),
                viewModel.RecipeOutputEnabled);
            if (saved)
            {
                setRecipeValidationOk();
            }

            return saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke recipe save" : "Recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentLazTwoPointRecipe(string path, bool isSmoke) =>
        LazTwoPointRecipeSaveCoordinator.Save(
            path,
            isSmoke,
            viewModel,
            lazPointCloud(),
            hasLazTwoPointMeasurement(),
            setRecipeValidationOk);

    private bool SaveCurrentWarpageRecipe(string path, bool isSmoke) =>
        C3DWarpageRecipeSaveCoordinator.Save(path, isSmoke, viewModel, c3dSample());

    private bool SaveCurrentThicknessRecipe(string path, bool isSmoke) =>
        C3DThicknessRecipeSaveCoordinator.Save(path, isSmoke, viewModel, c3dSample());

    private bool SaveCurrentGapFlushRecipe(string path, bool isSmoke) =>
        C3DGapFlushRecipeSaveCoordinator.Save(path, isSmoke, viewModel, c3dSample());

    private bool SaveCurrentPointPairDimensionsRecipe(string path, bool isSmoke) =>
        C3DPointPairDimensionsRecipeSaveCoordinator.Save(path, isSmoke, viewModel, c3dSample());
}
