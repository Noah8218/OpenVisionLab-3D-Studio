using System.Globalization;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal sealed class ShellSmokeCommandLineOptions
{
    private readonly string[] arguments;

    private ShellSmokeCommandLineOptions(string[] arguments)
    {
        this.arguments = arguments;
    }

    public static ShellSmokeCommandLineOptions Parse(string[] arguments) =>
        new(arguments);

    public string? ShellScreenshotPath => GetValue("--shell-smoke-screenshot");
    public string? ScreenshotQualityReportPath => GetValue("--shell-screenshot-quality-report");
    public string? ViewerLayoutSmoke => GetValue("--smoke-viewer-layout");
    public string? ThicknessRepeatGridSmoke => GetValue("--smoke-thickness-repeat-grid");
    public string? ViewerPopoutScreenshotPath => GetValue("--viewer-popout-screenshot");
    public string? ViewerPopoutScreenshotQualityReportPath => GetValue("--viewer-popout-screenshot-quality-report");
    public string? RecipeManagerScreenshotPath => GetValue("--recipe-manager-screenshot");
    public string? RecipeManagerScreenshotQualityReportPath => GetValue("--recipe-manager-screenshot-quality-report");
    public string? MessageDialogScreenshotPath => GetValue("--message-dialog-screenshot");
    public string? MessageDialogScreenshotQualityReportPath => GetValue("--message-dialog-screenshot-quality-report");
    public string? FilterToolLabScreenshotPath => GetValue("--filter-tool-lab-screenshot");
    public string? FilterToolLabScreenshotQualityReportPath => GetValue("--filter-tool-lab-screenshot-quality-report");
    public string? EdgeToolLabScreenshotPath => GetValue("--edge-tool-lab-screenshot");
    public string? EdgeToolLabScreenshotQualityReportPath => GetValue("--edge-tool-lab-screenshot-quality-report");
    public string? TwoPointLineToolLabScreenshotPath => GetValue("--two-point-line-tool-lab-screenshot");
    public string? TwoPointLineToolLabScreenshotQualityReportPath => GetValue("--two-point-line-tool-lab-screenshot-quality-report");
    public string? ThreePointPlaneToolLabScreenshotPath => GetValue("--three-point-plane-tool-lab-screenshot");
    public string? ThreePointPlaneToolLabScreenshotQualityReportPath => GetValue("--three-point-plane-tool-lab-screenshot-quality-report");
    public string? DatumPlaneDeviationToolLabScreenshotPath => GetValue("--datum-plane-deviation-tool-lab-screenshot");
    public string? DatumPlaneDeviationToolLabScreenshotQualityReportPath => GetValue("--datum-plane-deviation-tool-lab-screenshot-quality-report");
    public string? LineIntersectionToolLabScreenshotPath => GetValue("--line-intersection-tool-lab-screenshot");
    public string? LineIntersectionToolLabScreenshotQualityReportPath => GetValue("--line-intersection-tool-lab-screenshot-quality-report");
    public string? LandmarkCorrespondenceToolLabScreenshotPath => GetValue("--landmark-correspondence-tool-lab-screenshot");
    public string? LandmarkCorrespondenceToolLabScreenshotQualityReportPath => GetValue("--landmark-correspondence-tool-lab-screenshot-quality-report");
    public string? XYZAffineSolveToolLabScreenshotPath => GetValue("--xyz-affine-solve-tool-lab-screenshot");
    public string? XYZAffineSolveToolLabScreenshotQualityReportPath => GetValue("--xyz-affine-solve-tool-lab-screenshot-quality-report");
    public string? XYZAffineApplyToolLabScreenshotPath => GetValue("--xyz-affine-apply-tool-lab-screenshot");
    public string? XYZAffineApplyToolLabScreenshotQualityReportPath => GetValue("--xyz-affine-apply-tool-lab-screenshot-quality-report");
    public string? RegridHeightMapToolLabScreenshotPath => GetValue("--regrid-height-map-tool-lab-screenshot");
    public string? RegridHeightMapToolLabScreenshotQualityReportPath => GetValue("--regrid-height-map-tool-lab-screenshot-quality-report");
    public string? SmokeSaveRecipePath => GetValue("--smoke-save-recipe");
    public string? TeachingSelectionSmokeMode => GetValue("--smoke-tool-teaching-selection");
    public string? TeachingSelectionSmokeReportPath => GetValue("--smoke-tool-teaching-selection-report");
    public string? TeachingRecipeSmokeSavePath => GetValue("--smoke-save-tool-teaching-recipe");
    public string? NewRecipeLifecycleSmokePath => GetValue("--smoke-new-recipe-lifecycle");
    public string? NewRecipeLifecycleSmokeReportPath => GetValue("--smoke-new-recipe-lifecycle-report");
    public string? OpenRecipeLifecycleSmokePath => GetValue("--smoke-open-recipe-lifecycle");
    public string? OpenRecipeLifecycleSmokeReportPath => GetValue("--smoke-open-recipe-lifecycle-report");
    public string? AsyncC3DLoadSmokePath => GetValue("--smoke-async-c3d-load");
    public string? AsyncC3DLoadSmokeReportPath => GetValue("--smoke-async-c3d-load-report");
    public string? SourceQualitySmokeReportPath => GetValue("--smoke-source-quality-report");
    public string? HeightImagePaletteSmoke => GetValue("--smoke-height-image-palette");
    public string? HeightImageDisplayRangeSmokeReportPath =>
        GetValue("--smoke-height-image-display-range-report");
    public string? SharedHeightHoverSmokeReportPath =>
        GetValue("--smoke-shared-height-hover-report");
    public string? HeightImageRoiPointerSmoke =>
        GetValue("--smoke-height-image-roi-pointer");
    public string? HeightImageRoiPointerSmokeReportPath =>
        GetValue("--smoke-height-image-roi-pointer-report");
    public string? HeightImageRoiPointerSmokeSavePath =>
        GetValue("--smoke-height-image-roi-pointer-save");
    public string? PlaneFlatnessLiveA3PointerReportPath => GetValue("--smoke-plane-flatness-live-a3-pointer-report");
    public string? PlaneFlatnessLiveA3PointerSavePath => GetValue("--smoke-plane-flatness-live-a3-pointer-save");
    public string? ProfilePointerSmokeReportPath => GetValue("--smoke-profile-pointer-report");
    public string? OrientedBoxPointerSmokeReportPath =>
        GetValue("--smoke-oriented-box-pointer-report");
    public string? SmokeSelectToolId => GetValue("--smoke-select-tool");
    public string? WorkbenchInteractionReportPath => GetValue("--smoke-workbench-interaction-report");
    public string? EdgeStepId => GetValue("--tool-teaching-step");
    public string? EdgeSmokeReportPath => GetValue("--smoke-tool-edge-report");
    public string? LineFitSmokeReportPath => GetValue("--smoke-tool-line-fit-report");

    public double? AsyncC3DLoadCancelAt =>
        GetInvariantDouble("--smoke-async-c3d-load-cancel-at");

    public double? HeightImageRangeMinimumSmoke =>
        GetInvariantDouble("--smoke-height-image-range-min");

    public double? HeightImageRangeMaximumSmoke =>
        GetInvariantDouble("--smoke-height-image-range-max");

    public int? SharedHeightHoverRow =>
        GetInvariantInt("--smoke-shared-height-hover-row");

    public int? SharedHeightHoverColumn =>
        GetInvariantInt("--smoke-shared-height-hover-column");

    public bool AsyncC3DLoadExpectFailure => HasFlag("--smoke-async-c3d-load-expect-failure");
    public bool SourceQualitySmoke => HasFlag("--smoke-source-quality");
    public bool HeightImageDisplayRangeSmoke =>
        HasFlag("--smoke-height-image-display-range");
    public bool SharedHeightHoverSmoke =>
        HasFlag("--smoke-shared-height-hover");
    public bool PlaneFlatnessLiveA3PointerSmoke => HasFlag("--smoke-plane-flatness-live-a3-pointer");
    public bool FilterPublishSmoke => HasFlag("--smoke-tool-filter-publish");
    public bool FilterPreviewSmoke => FilterPublishSmoke || HasFlag("--smoke-tool-filter-preview");
    public bool RemoveOutlierPreviewSmoke =>
        HasFlag("--smoke-tool-remove-outlier-preview");
    public bool LevelSurfacePreviewSmoke =>
        HasFlag("--smoke-tool-level-surface-preview");
    public bool MeasurementPreviewSmoke => HasFlag("--smoke-tool-measurement-preview");
    public bool TwoPointLinePublishSmoke => HasFlag("--smoke-tool-two-point-line-publish");
    public bool TwoPointLinePreviewSmoke => TwoPointLinePublishSmoke || HasFlag("--smoke-tool-two-point-line-preview");
    public bool ThreePointPlanePublishSmoke => HasFlag("--smoke-tool-three-point-plane-publish");
    public bool ThreePointPlanePreviewSmoke => ThreePointPlanePublishSmoke || HasFlag("--smoke-tool-three-point-plane-preview");
    public bool DatumPlaneDeviationPublishSmoke => HasFlag("--smoke-tool-datum-plane-deviation-publish");
    public bool DatumPlaneDeviationPreviewSmoke =>
        DatumPlaneDeviationPublishSmoke || HasFlag("--smoke-tool-datum-plane-deviation-preview");
    public bool EdgePublishSmoke => HasFlag("--smoke-tool-edge-publish");
    public bool LineFitPreviewSmoke => HasFlag("--smoke-tool-line-fit-preview");
    public bool EdgePreviewSmoke => EdgePublishSmoke || LineFitPreviewSmoke || HasFlag("--smoke-tool-edge-preview");
    public bool InvalidEdgeDraftSmoke => HasFlag("--smoke-wpg-invalid-edge");
    public bool SmokePublishResult => HasFlag("--smoke-publish-result");
    public bool ExpandSelectedToolParametersSmoke =>
        HasFlag("--smoke-expand-selected-tool-parameters");
    public bool FocusSelectedToolParameterSearchSmoke =>
        HasFlag("--smoke-focus-selected-tool-parameter-search");
    public bool WaitForNominalActualPreview => HasFlag("--smoke-nominal-actual");

    public (int Width, int Height)? WindowSize =>
        int.TryParse(GetValue("--shell-smoke-width"), out var width)
        && int.TryParse(GetValue("--shell-smoke-height"), out var height)
            ? (width, height)
            : null;

    public bool NeedsCompactWorkbench =>
        TeachingSelectionSmokeMode is not null
        || PlaneFlatnessLiveA3PointerSmoke
        || ProfilePointerSmokeReportPath is not null
        || OrientedBoxPointerSmokeReportPath is not null
        || EdgePreviewSmoke
        || RemoveOutlierPreviewSmoke
        || LevelSurfacePreviewSmoke
        || LineFitPreviewSmoke
        || TwoPointLinePreviewSmoke
        || ThreePointPlanePreviewSmoke
        || DatumPlaneDeviationPreviewSmoke;

    public bool ShouldAttachLoadedHandler(bool hasViewerSmokeScreenshot) =>
        ShellScreenshotPath is not null
        || RecipeManagerScreenshotPath is not null
        || MessageDialogScreenshotPath is not null
        || FilterToolLabScreenshotPath is not null
        || EdgeToolLabScreenshotPath is not null
        || TwoPointLineToolLabScreenshotPath is not null
        || ThreePointPlaneToolLabScreenshotPath is not null
        || DatumPlaneDeviationToolLabScreenshotPath is not null
        || LineIntersectionToolLabScreenshotPath is not null
        || LandmarkCorrespondenceToolLabScreenshotPath is not null
        || XYZAffineSolveToolLabScreenshotPath is not null
        || XYZAffineApplyToolLabScreenshotPath is not null
        || RegridHeightMapToolLabScreenshotPath is not null
        || hasViewerSmokeScreenshot
        || NeedsCompactWorkbench
        || FilterPreviewSmoke
        || RemoveOutlierPreviewSmoke
        || LevelSurfacePreviewSmoke
        || MeasurementPreviewSmoke
        || ViewerLayoutSmoke is not null
        || ThicknessRepeatGridSmoke is not null
        || ViewerPopoutScreenshotPath is not null
        || NewRecipeLifecycleSmokePath is not null
        || OpenRecipeLifecycleSmokePath is not null
        || AsyncC3DLoadSmokePath is not null
        || SourceQualitySmoke
        || HeightImageDisplayRangeSmoke
        || SharedHeightHoverSmoke
        || HeightImageRoiPointerSmoke is not null
        || OrientedBoxPointerSmokeReportPath is not null
        || ExpandSelectedToolParametersSmoke
        || FocusSelectedToolParameterSearchSmoke
        || WorkbenchInteractionReportPath is not null;

    private bool HasFlag(string name) =>
        arguments.Contains(name, StringComparer.OrdinalIgnoreCase);

    private string? GetValue(string name)
    {
        var index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }

    private double? GetInvariantDouble(string name) =>
        double.TryParse(
            GetValue(name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;

    private int? GetInvariantInt(string name) =>
        int.TryParse(
            GetValue(name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
}
