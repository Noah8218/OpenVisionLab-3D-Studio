using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

[Flags]
internal enum ViewerViewModelPropertyChangeEffects
{
    None = 0,
    UpdateDeviationLegendVisibility = 1 << 0,
    UpdatePointCloudColorLegendVisibility = 1 << 1,
    Render = 1 << 2,
    ApplyHeightDeviationRule = 1 << 3,
    UpdateRoiStepMeasurement = 1 << 4,
    FitReferencePlane = 1 << 5,
    InvalidatePlaneFlatness = 1 << 6,
    ReloadRenderDensity = 1 << 7,
    SyncRecipeRoiParameters = 1 << 8
}

/// <summary>
/// Maps ViewModel notifications to the View effects that are already owned by
/// <see cref="OpenVisionThreeDViewerControl"/>. The policy contains no WPF or
/// rendering calls, so property coverage can be checked without creating a
/// control or an OpenGL context.
/// </summary>
internal static class ViewerViewModelPropertyChangePolicy
{
    public static ViewerViewModelPropertyChangeEffects Classify(string? propertyName)
    {
        var effects = ViewerViewModelPropertyChangeEffects.None;

        if (propertyName == nameof(MainWindowViewModel.DeviationLegendVisible))
        {
            effects |= ViewerViewModelPropertyChangeEffects.UpdateDeviationLegendVisibility;
        }

        if (propertyName == nameof(MainWindowViewModel.PointCloudColorLegendVisible))
        {
            effects |= ViewerViewModelPropertyChangeEffects.UpdatePointCloudColorLegendVisibility;
        }

        if (IsDisplayProperty(propertyName))
        {
            effects |= ViewerViewModelPropertyChangeEffects.Render;
        }

        if (propertyName == nameof(MainWindowViewModel.RecipePeakTolerance))
        {
            effects |= ViewerViewModelPropertyChangeEffects.ApplyHeightDeviationRule;
        }

        if (propertyName is nameof(MainWindowViewModel.SelectedSelectionMode)
            or nameof(MainWindowViewModel.C3DSampleVisible)
            or nameof(MainWindowViewModel.C3DModelTransform))
        {
            effects |= ViewerViewModelPropertyChangeEffects.UpdateRoiStepMeasurement;
        }

        if (propertyName == nameof(MainWindowViewModel.C3DModelTransform))
        {
            effects |= ViewerViewModelPropertyChangeEffects.FitReferencePlane
                | ViewerViewModelPropertyChangeEffects.InvalidatePlaneFlatness;
        }

        if (propertyName == nameof(MainWindowViewModel.SelectedRenderDensity))
        {
            effects |= ViewerViewModelPropertyChangeEffects.ReloadRenderDensity;
        }
        else if (IsRecipeRoiEditProperty(propertyName))
        {
            effects |= ViewerViewModelPropertyChangeEffects.SyncRecipeRoiParameters
                | ViewerViewModelPropertyChangeEffects.Render;
        }

        return effects;
    }

    public static bool IsDisplayProperty(string? propertyName) =>
        propertyName is nameof(MainWindowViewModel.CubeVisible)
            or nameof(MainWindowViewModel.PointCloudVisible)
            or nameof(MainWindowViewModel.C3DSampleVisible)
            or nameof(MainWindowViewModel.GlbSampleVisible)
            or nameof(MainWindowViewModel.LazSampleVisible)
            or nameof(MainWindowViewModel.MeasurementVisible)
            or nameof(MainWindowViewModel.DisplaySettingsRevision)
            or nameof(MainWindowViewModel.C3DHeightColorRangeRevision)
            or nameof(MainWindowViewModel.PointSize)
            or nameof(MainWindowViewModel.RecipePeakTolerance)
            or nameof(MainWindowViewModel.C3DModelTransform)
            or nameof(MainWindowViewModel.ProjectionMode)
            or nameof(MainWindowViewModel.OrthographicHeight)
            or nameof(MainWindowViewModel.SelectedTeachingRoiDisplayHeightOffset)
            or nameof(MainWindowViewModel.SelectedSelectionMode)
            or nameof(MainWindowViewModel.SelectionOverlayVisible)
            or nameof(MainWindowViewModel.ResultOverlayVisible)
            or nameof(MainWindowViewModel.WorkbenchTwoPointLine)
            or nameof(MainWindowViewModel.IsWorkbenchTwoPointLinePublished)
            or nameof(MainWindowViewModel.WorkbenchThreePointPlane)
            or nameof(MainWindowViewModel.IsWorkbenchThreePointPlanePublished)
            or nameof(MainWindowViewModel.WorkbenchLineFit)
            or nameof(MainWindowViewModel.SelectedWorkbenchLineFitPoint)
            or nameof(MainWindowViewModel.LineFitInliersVisible)
            or nameof(MainWindowViewModel.LineFitOutliersVisible)
            or nameof(MainWindowViewModel.LineFitSegmentVisible)
            or nameof(MainWindowViewModel.LineFitSelectedResidualVisible)
            or nameof(MainWindowViewModel.WorkbenchFirstIntersectionLine)
            or nameof(MainWindowViewModel.WorkbenchSecondIntersectionLine)
            or nameof(MainWindowViewModel.WorkbenchLineIntersection)
            or nameof(MainWindowViewModel.LineIntersectionFirstLineVisible)
            or nameof(MainWindowViewModel.LineIntersectionSecondLineVisible)
            or nameof(MainWindowViewModel.LineIntersectionClosestConnectorVisible)
            or nameof(MainWindowViewModel.LineIntersectionCornerAnchorVisible)
            or nameof(MainWindowViewModel.WorkbenchLandmarkCorrespondenceAnchors)
            or nameof(MainWindowViewModel.WorkbenchLandmarkCorrespondence)
            or nameof(MainWindowViewModel.WorkbenchAffineApply)
            or nameof(MainWindowViewModel.IsWorkbenchAffineApplyPublished)
            or nameof(MainWindowViewModel.WorkbenchRegridHeightField)
            or nameof(MainWindowViewModel.IsWorkbenchRegridHeightFieldPublished)
            or nameof(MainWindowViewModel.WorkbenchSurfaceMatch)
            or nameof(MainWindowViewModel.ResultEntities);

    public static bool IsRecipeRoiEditProperty(string? propertyName) =>
        propertyName is nameof(MainWindowViewModel.RecipeRoiLeftCenterX)
            or nameof(MainWindowViewModel.RecipeRoiLeftCenterZ)
            or nameof(MainWindowViewModel.RecipeRoiLeftHalfWidth)
            or nameof(MainWindowViewModel.RecipeRoiLeftHalfDepth)
            or nameof(MainWindowViewModel.RecipeRoiRightCenterX)
            or nameof(MainWindowViewModel.RecipeRoiRightCenterZ)
            or nameof(MainWindowViewModel.RecipeRoiRightHalfWidth)
            or nameof(MainWindowViewModel.RecipeRoiRightHalfDepth);
}
