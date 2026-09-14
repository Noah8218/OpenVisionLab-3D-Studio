using System.IO;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Loading;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns Viewer recipe-load routing and application policy. The WPF control
/// supplies state, rendering, and source-loading bridges; this owner keeps
/// file-type dispatch and normal/smoke application semantics out of the View.
/// </summary>
internal sealed class ViewerRecipeLoadWorkflow
{
    private readonly ViewerRecipeLoadWorkflowCallbacks callbacks;
    private Task<bool>? openRecipeTask;
    private Task? openRecipeObservationTask;

    public ViewerRecipeLoadWorkflow(ViewerRecipeLoadWorkflowCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public bool CanStartOpen() =>
        !callbacks.IsDisposed()
        && Volatile.Read(ref openRecipeTask) is not { IsCompleted: false };

    public void StartOpen(string path, bool isSmoke, CancellationToken cancellationToken)
    {
        if (!CanStartOpen())
        {
            return;
        }

        var task = ApplyAsync(path, isSmoke, cancellationToken);
        Volatile.Write(ref openRecipeTask, task);
        Volatile.Write(
            ref openRecipeObservationTask,
            ObserveOpenRecipeAsync(task, cancellationToken));
    }

    public void Dispose()
    {
        Volatile.Write(ref openRecipeTask, null);
        Volatile.Write(ref openRecipeObservationTask, null);
    }

    public bool Apply(string path, bool isSmoke) =>
        ViewerRecipeLoadCoordinator.Apply(
            path,
            isSmoke,
            CreateRoutes(),
            callbacks.HandleFailure);

    public Task<bool> ApplyAsync(
        string path,
        bool isSmoke,
        CancellationToken cancellationToken) =>
        ViewerRecipeLoadCoordinator.ApplyAsync(
            path,
            isSmoke,
            CreateRoutes(),
            callbacks.HandleFailure,
            cancellationToken);

    private async Task ObserveOpenRecipeAsync(
        Task<bool> task,
        CancellationToken cancellationToken)
    {
        try
        {
            await task.ConfigureAwait(true);
            if (!callbacks.IsDisposed() && !cancellationToken.IsCancellationRequested)
            {
                callbacks.RenderNow();
            }
        }
        catch (OperationCanceledException) when (
            callbacks.IsDisposed()
            || cancellationToken.IsCancellationRequested)
        {
            // The control lifetime owns the open request after the dialog closes.
        }
        catch (Exception exception)
        {
            callbacks.HandleFailure("Recipe", exception);
        }
        finally
        {
            if (ReferenceEquals(Volatile.Read(ref openRecipeTask), task))
            {
                Volatile.Write(ref openRecipeTask, null);
            }
        }
    }

    private ViewerRecipeLoadRoutes CreateRoutes() =>
        new(
            ApplyNominalActualRecipe,
            ApplyLazTwoPointRecipe,
            ApplyC3DThicknessRecipe,
            ApplyC3DWarpageRecipe,
            ApplyC3DGapFlushRecipe,
            ApplyC3DPointPairDimensionsRecipe,
            ApplyHeightDeviationRecipe,
            ApplyLazTwoPointRecipeAsync);

    private bool ApplyNominalActualRecipe(
        ViewerRecipeFile recipeFile,
        NominalActualComparisonRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = NominalActualComparisonRecipeLoadPlan.Create(recipeFile, recipe);
            return NominalActualComparisonRecipeApplyCoordinator.Apply(
                plan,
                callbacks.ViewModel,
                isSmoke,
                path =>
                {
                    callbacks.ApplySmokeStl(path);
                    return callbacks.HasImportedMesh();
                },
                () =>
                {
                    callbacks.MarkSmokeNominalActualPreview();
                    callbacks.ViewModel.NominalActual.PreviewCommand.Execute(null);
                });
        }
        catch (Exception exception) when (IsRecipeApplyFailure(exception))
        {
            callbacks.ViewModel.ClearNominalActualComparison(exception.Message);
            return callbacks.HandleFailure(
                isSmoke ? "Smoke nominal/actual recipe" : "Nominal/actual recipe",
                exception);
        }
    }

    private bool ApplyHeightDeviationRecipe(
        ViewerRecipeFile recipeFile,
        HeightDeviationRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = HeightDeviationRecipeLoadPlan.Create(
                recipeFile,
                recipe,
                callbacks.ViewModel.C3DMaxRenderedPoints);
            callbacks.SetC3DSource(plan.Grid);
            callbacks.ViewModel.RecipeOutputEnabled = recipe.OutputEnabled;
            return HeightDeviationRecipeApplyCoordinator.Apply(
                plan,
                callbacks.ViewModel,
                isSmoke,
                callbacks.SetC3DSampleStatus,
                callbacks.ApplyRecipeRoiStep,
                callbacks.PreviewC3DPlaneFlatness,
                callbacks.PreviewC3DVolume,
                callbacks.PreviewC3DCrossSection);
        }
        catch (Exception exception) when (IsRecipeApplyFailure(exception))
        {
            return callbacks.HandleFailure(isSmoke ? "Smoke recipe" : "Recipe", exception);
        }
    }

    private bool ApplyC3DThicknessRecipe(
        ViewerRecipeFile recipeFile,
        C3DThicknessRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = C3DThicknessRecipeLoadPlan.Create(
                recipeFile,
                recipe,
                callbacks.ViewModel.C3DMaxRenderedPoints);
            callbacks.SetC3DSource(plan.Grid);
            return C3DThicknessRecipeApplyCoordinator.Apply(
                plan,
                callbacks.ViewModel,
                isSmoke,
                callbacks.SetC3DSampleStatus,
                callbacks.ClearPlaneReferenceState,
                callbacks.RenderNow);
        }
        catch (Exception exception) when (IsRecipeApplyFailure(exception))
        {
            return callbacks.HandleFailure(
                isSmoke ? "Smoke Thickness recipe" : "Thickness recipe",
                exception);
        }
    }

    private bool ApplyC3DWarpageRecipe(
        ViewerRecipeFile recipeFile,
        C3DWarpageRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = C3DWarpageRecipeLoadPlan.Create(
                recipeFile,
                recipe,
                callbacks.ViewModel.C3DMaxRenderedPoints);
            callbacks.SetC3DSource(plan.Grid);
            return C3DWarpageRecipeApplyCoordinator.Apply(
                plan,
                callbacks.ViewModel,
                isSmoke,
                callbacks.SetC3DSampleStatus,
                callbacks.ClearWarpageTransientInspectionState,
                callbacks.RenderNow);
        }
        catch (Exception exception) when (IsRecipeApplyFailure(exception))
        {
            return callbacks.HandleFailure(
                isSmoke ? "Smoke Warpage recipe" : "Warpage recipe",
                exception);
        }
    }

    private bool ApplyC3DGapFlushRecipe(
        ViewerRecipeFile recipeFile,
        C3DGapFlushRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = C3DGapFlushRecipeLoadPlan.Create(
                recipeFile,
                recipe,
                callbacks.ViewModel.C3DMaxRenderedPoints);
            callbacks.SetC3DSource(plan.Grid);
            return C3DGapFlushRecipeApplyCoordinator.Apply(
                plan,
                callbacks.ViewModel,
                isSmoke,
                callbacks.SetC3DSampleStatus,
                callbacks.ClearPlaneReferenceState,
                callbacks.ApplyGapFlushRecipeRoiState,
                callbacks.ApplyGapFlushPreviewOverlay,
                callbacks.RenderNow);
        }
        catch (Exception exception) when (IsRecipeApplyFailure(exception))
        {
            return callbacks.HandleFailure(
                isSmoke ? "Smoke Gap / Flush recipe" : "Gap / Flush recipe",
                exception);
        }
    }

    private bool ApplyC3DPointPairDimensionsRecipe(
        ViewerRecipeFile recipeFile,
        C3DPointPairDimensionsRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = C3DPointPairDimensionsRecipeLoadPlan.Create(
                recipeFile,
                recipe,
                callbacks.ViewModel.C3DMaxRenderedPoints);
            callbacks.SetC3DSource(plan.Grid);
            return C3DPointPairDimensionsRecipeApplyCoordinator.Apply(
                plan,
                callbacks.ViewModel,
                isSmoke,
                callbacks.SetC3DSampleStatus,
                callbacks.ClearPlaneReferenceState,
                () => callbacks.ApplyRecipeRoiStep(null),
                callbacks.SetTwoPointMeasurement,
                callbacks.RenderNow);
        }
        catch (Exception exception) when (IsRecipeApplyFailure(exception))
        {
            return callbacks.HandleFailure(
                isSmoke ? "Smoke point pair recipe" : "Point pair recipe",
                exception);
        }
    }

    private bool ApplyLazTwoPointRecipe(
        ViewerRecipeFile recipeFile,
        LazTwoPointMeasurementRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = LazTwoPointRecipeLoadPlan.Create(recipeFile, recipe);
            var pointCloud = callbacks.LoadLazPointCloud(
                plan.SourcePath,
                recipe.Measurement.MaxSampledPoints);
            if (pointCloud is null)
            {
                throw new InvalidDataException("LAZ/LAS two-point recipe source could not be decoded.");
            }

            callbacks.SetLazPointCloud(pointCloud);
            return LazTwoPointRecipeApplyCoordinator.Apply(
                plan,
                pointCloud,
                callbacks.ViewModel,
                isSmoke,
                callbacks.ClearLazTransientMeasurement,
                callbacks.ApplySmokeLazTwoPointMeasurement);
        }
        catch (Exception exception) when (IsRecipeApplyFailure(exception))
        {
            return callbacks.HandleFailure(
                isSmoke ? "Smoke LAZ/LAS recipe" : "LAZ/LAS recipe",
                exception);
        }
    }

    private async Task<bool> ApplyLazTwoPointRecipeAsync(
        ViewerRecipeFile recipeFile,
        LazTwoPointMeasurementRecipe recipe,
        bool isSmoke,
        CancellationToken cancellationToken)
    {
        try
        {
            var plan = LazTwoPointRecipeLoadPlan.Create(recipeFile, recipe);
            var loadResult = await callbacks.LoadLazPointCloudAsync(
                plan.SourcePath,
                recipe.Measurement.MaxSampledPoints,
                cancellationToken,
                null,
                () => !callbacks.IsDisposed() && !cancellationToken.IsCancellationRequested);
            cancellationToken.ThrowIfCancellationRequested();
            if (loadResult is not { PointCloud: { } pointCloud } || callbacks.IsDisposed())
            {
                return false;
            }

            callbacks.SetLazPointCloud(pointCloud);
            callbacks.SetLazPointCloudTelemetry(
                pointCloud,
                loadResult.Value.LoadMilliseconds,
                loadResult.Value.Reused);
            return LazTwoPointRecipeApplyCoordinator.Apply(
                plan,
                pointCloud,
                callbacks.ViewModel,
                isSmoke,
                callbacks.ClearLazTransientMeasurement,
                callbacks.ApplySmokeLazTwoPointMeasurement);
        }
        catch (Exception exception) when (IsRecipeApplyFailure(exception))
        {
            return callbacks.HandleFailure(
                isSmoke ? "Smoke LAZ/LAS recipe" : "LAZ/LAS recipe",
                exception);
        }
    }

    private static bool IsRecipeApplyFailure(Exception exception) =>
        exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or NotSupportedException;
}

internal sealed record ViewerRecipeLoadWorkflowCallbacks(
    MainWindowViewModel ViewModel,
    Action<C3DHeightGrid> SetC3DSource,
    Action<LazPointCloud> SetLazPointCloud,
    Func<string, int, LazPointCloud?> LoadLazPointCloud,
    Func<string, int, CancellationToken, IProgress<double>?, Func<bool>?, Task<LazPointCloudLoadResult?>> LoadLazPointCloudAsync,
    Action<LazPointCloud, double, bool> SetLazPointCloudTelemetry,
    Action ClearLazTransientMeasurement,
    Action<string> ApplySmokeLazTwoPointMeasurement,
    Action<string> ApplySmokeStl,
    Func<bool> HasImportedMesh,
    Action MarkSmokeNominalActualPreview,
    Action SetC3DSampleStatus,
    Action<HeightDeviationRecipeRoiStep?> ApplyRecipeRoiStep,
    Func<bool> PreviewC3DPlaneFlatness,
    Func<bool> PreviewC3DVolume,
    Func<bool> PreviewC3DCrossSection,
    Action ClearPlaneReferenceState,
    Action ClearWarpageTransientInspectionState,
    Action<C3DGapFlushStep> ApplyGapFlushRecipeRoiState,
    Action<C3DGapFlushStep, GapFlushRegionStats, GapFlushRegionStats> ApplyGapFlushPreviewOverlay,
    Action<HeightGridPoint, HeightGridPoint> SetTwoPointMeasurement,
    Action RenderNow,
    Func<bool> IsDisposed,
    Func<string, Exception, bool> HandleFailure);
