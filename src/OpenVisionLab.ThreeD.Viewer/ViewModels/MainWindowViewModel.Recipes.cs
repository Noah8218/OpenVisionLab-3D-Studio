using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    public void SetRecipeLoaded(string recipePath, string sourceName, string sourcePath, string unit, double peakTolerance)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSourceName = sourceName;
        RecipeSourcePath = sourcePath;
        RecipeSourceUnit = unit;
        RecipePeakTolerance = peakTolerance;
        RecipeSaveSummary = $"Recipe loaded: {Path.GetFileName(recipePath)}";
        RefreshRecipeSummary();
    }

    public void SetNominalActualRecipeLoaded(string recipePath)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSaveSummary = $"Recipe loaded: {Path.GetFileName(recipePath)}";
        RefreshNominalActualRecipeSummary();
    }

    public void SetNominalActualRecipeSaved(string recipePath)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSaveSummary = $"Recipe saved: {Path.GetFullPath(recipePath)}";
        RefreshNominalActualRecipeSummary();
    }

    public void SetLazRecipeLoaded(string recipePath, string sourceName, string sourcePath)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Recipe: {Path.GetFileName(recipePath)}\nSource: {sourceName}\nLAZ/LAS acceptance: editable");
        RecipeSaveSummary = $"Recipe loaded: {Path.GetFileName(recipePath)}";
        SetLazSampleSource(sourcePath, sourceName);
    }

    public void SetPointPairRecipeLoaded(string recipePath, string sourceName, string sourcePath, string sourceUnit)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSourceName = sourceName;
        RecipeSourcePath = sourcePath;
        RecipeSourceUnit = sourceUnit;
        RecipeSaveSummary = $"Recipe loaded: {Path.GetFileName(recipePath)}";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void SetThicknessRecipeLoaded(string recipePath, string sourceName, string sourcePath, string sourceUnit)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSourceName = sourceName;
        RecipeSourcePath = sourcePath;
        RecipeSourceUnit = sourceUnit;
        RecipeSaveSummary = $"Recipe loaded: {Path.GetFileName(recipePath)}";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void SetRecipeSaved(string recipePath)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSaveSummary = $"Recipe saved: {Path.GetFullPath(recipePath)}";
        RefreshRecipeSummary();
    }

    public void SetSectionProfile(string sourceName, int rowIndex, int sampleCount, double min, double max, double mean, string pathData)
    {
        SectionProfileVisible = sampleCount > 1;
        SectionProfileSampleCount = sampleCount;
        SectionProfileSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Profile: {sourceName} section | row {rowIndex} | samples {sampleCount}");
        SectionProfileRange = string.Create(
            CultureInfo.InvariantCulture,
            $"Range: min {min:F3}, max {max:F3}, mean {mean:F3} raw-height");
        SectionProfilePathData = string.IsNullOrWhiteSpace(pathData) ? "M 0,30 L 240,30" : pathData;

        if (SelectedSelectionMode == "Section Plane")
        {
            SelectionSummary = SectionProfileSummary;
        }
    }

    public void SetHeightMap(ImageSource imageSource, int sourceWidth, int sourceHeight, int renderedPoints, double min, double max, double mean, int pixelWidth, int pixelHeight)
    {
        HeightMapVisible = true;
        HeightMapImageSource = imageSource;
        HeightMapPixelWidth = pixelWidth;
        HeightMapPixelHeight = pixelHeight;
        HeightMapSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Height map: {sourceWidth} x {sourceHeight} C3D | rendered {renderedPoints:N0} points");
        HeightMapRange = string.Create(
            CultureInfo.InvariantCulture,
            $"Range: min {min:F3}, max {max:F3}, mean {mean:F3} raw-height");
    }

    public void ClearHeightMap()
    {
        HeightMapVisible = false;
        HeightMapImageSource = null;
        HeightMapPixelWidth = 0;
        HeightMapPixelHeight = 0;
        HeightMapSummary = "Height map: not loaded";
        HeightMapRange = "Range: not loaded";
    }

    public void ClearSectionProfile()
    {
        SectionProfileVisible = false;
        SectionProfileSampleCount = 0;
        SectionProfileSummary = "Profile: not loaded";
        SectionProfileRange = "Range: not loaded";
        SectionProfilePathData = "M 0,30 L 240,30";

        if (SelectedSelectionMode == "Section Plane")
        {
            SelectionSummary = "Section plane: profile not loaded";
        }
    }

    private static string FormatRenderDensitySummary(string mode) => mode switch
    {
        "Fast" => "Fast: up to 25,000 C3D points / 25,000 LAZ/LAS points / 25,000 mesh triangles",
        "Detailed" => "Detailed: up to 140,000 C3D points / 150,000 LAZ/LAS points / 180,000 mesh triangles",
        _ => "Balanced: up to 55,000 C3D points / 50,000 LAZ/LAS points / 60,000 mesh triangles"
    };

    private static IReadOnlyList<SourceEntity> CreateSourceEntities(
        ModelTransform c3DTransform,
        string glbName,
        string glbSourcePath,
        string lazName,
        string lazSourcePath) =>
    [
        new SourceEntity(CubeEntityId, "Generated Unit Cube", EntityKind.Mesh, "unitless", null, ModelTransform.Identity),
        new SourceEntity(PointCloudEntityId, "Generated Point Cloud", EntityKind.PointCloud, "unitless", null, ModelTransform.Identity),
        new SourceEntity(C3DEntityId, "C3D Thickness Sample", EntityKind.HeightGrid, "raw-height", @"3D\Thickness\Ori_20240116_094414.C3D", c3DTransform),
        new SourceEntity(C3DWarpageEntityId, "C3D Warpage Sample", EntityKind.HeightGrid, "raw-height", @"3D\Warpage\Ori_20240116_094430.C3D", c3DTransform),
        new SourceEntity(GlbEntityId, glbName, EntityKind.Mesh, "unitless", glbSourcePath, ModelTransform.Identity),
        new SourceEntity(LazEntityId, lazName, EntityKind.PointCloud, "source-units", lazSourcePath, ModelTransform.Identity)
    ];

    private void RefreshSourceEntities()
    {
        var entities = CreateSourceEntities(
            C3DModelTransform,
            GlbSampleName,
            GlbSampleSourcePath,
            LazSampleName,
            LazSampleSourcePath).ToList();
        if (NominalActualInput is { } input)
        {
            entities.Add(new SourceEntity(
                input.ActualSource.Id,
                input.ActualSource.Name,
                EntityKind.Mesh,
                input.Unit,
                input.ActualSource.Path,
                ModelTransform.Identity));
            entities.Add(new SourceEntity(
                input.NominalSource.Id,
                input.NominalSource.Name,
                EntityKind.Mesh,
                input.Unit,
                input.NominalSource.Path,
                ModelTransform.Identity));
            entities.Add(new SourceEntity(
                input.QuerySource.Id,
                input.QuerySource.Name,
                EntityKind.PointCloud,
                input.Unit,
                input.QuerySource.Path,
                ModelTransform.Identity));
        }

        SourceEntities = entities;
    }

    private void RefreshNominalActualRecipeSummary()
    {
        if (NominalActualInput is not { } input)
        {
            return;
        }

        RecipeSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Recipe: {recipeFileName}\nActual: {input.ActualSource.Name}\nNominal: {input.NominalSource.Name}\nDirection: {NominalActualComparisonInput.Direction}\nTolerance: [{NominalActual.LowerTolerance:G6}, {NominalActual.UpperTolerance:G6}] {input.Unit}\nFrame: {input.FrameId}\nAlignment: {input.AlignmentId}");
    }

    private static string FormatNominalActualSource(
        string role,
        NominalActualFileIdentity identity) =>
        $"{role}: {Path.GetFileName(identity.Path)} | {identity.Id} | sha256 {identity.Sha256[..8]}";

    private static string FormatModelTransform(ModelTransform transform) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"T({transform.TranslateX:F3}, {transform.TranslateY:F3}, {transform.TranslateZ:F3}) | R({transform.RotateXDegrees:F1}, {transform.RotateYDegrees:F1}, {transform.RotateZDegrees:F1}) | S {transform.Scale:F3}");

    private static bool ModelTransformIsIdentity(ModelTransform transform) =>
        transform.TranslateX == 0.0
        && transform.TranslateY == 0.0
        && transform.TranslateZ == 0.0
        && transform.RotateXDegrees == 0.0
        && transform.RotateYDegrees == 0.0
        && transform.RotateZDegrees == 0.0
        && transform.Scale == 1.0;

    private void RefreshRecipeSummary()
    {
        var thicknessLine = ThicknessConfigured
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\nThickness limits: [{ThicknessMinimum:F3}, {ThicknessMaximum:F3}] {ThicknessUnit}")
            : string.Empty;
        var warpageLine = WarpageConfigured
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\nWarpage P2V limit: {WarpageMaximumPeakToValley:F3} {WarpageUnit}")
            : string.Empty;
        var flatnessLine = PlaneFlatnessConfigured
            ? string.Create(CultureInfo.InvariantCulture, $"\nFlatness tolerance: {PlaneFlatnessTolerance:F3} {PlaneFlatnessUnit}")
            : string.Empty;
        var pointPairLine = PointPairDimensionsConfigured
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\nDimensions expected: D {PointPairExpectedDistance:F3}, W {PointPairExpectedWidth:F3} {PointPairDimensionsUnit}, A {PointPairExpectedAngleDegrees:F3} deg")
            : string.Empty;
        var gapFlushLine = GapFlushConfigured
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\nGap / Flush expected: {GapFlushExpectedGap:F3} {GapFlushGapUnit}, {GapFlushExpectedFlush:F3} {GapFlushFlushUnit}")
            : string.Empty;
        var volumeLine = VolumeConfigured
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\nVolume expected net: {VolumeExpectedNet:F3} +/- {VolumeTolerance:F3} {VolumeUnit}")
            : string.Empty;
        var crossSectionLine = CrossSectionConfigured
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\nCross-section expected: width {CrossSectionExpectedWidth:F3} {CrossSectionWidthUnit}, height {CrossSectionExpectedHeightRange:F3} {CrossSectionHeightUnit}")
            : string.Empty;
        RecipeSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Recipe: {recipeFileName}\nSource: {RecipeSourceName}\nTolerance: {RecipePeakTolerance:F3} {RecipeSourceUnit}{thicknessLine}{warpageLine}{flatnessLine}{pointPairLine}{gapFlushLine}{volumeLine}{crossSectionLine}");
    }

    private void RefreshRecipeParameterSummary()
    {
        var thicknessLine = ThicknessConfigured
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\nThickness grid ROI row {ThicknessRoiRow}, column {ThicknessRoiColumn}, size {ThicknessRoiRowCount} x {ThicknessRoiColumnCount}, min samples {ThicknessMinimumValidSamples}, frame {ThicknessFrameId}")
            : string.Empty;
        var warpageLine = WarpageConfigured
            ? $"\nWarpage best-fit ROI row {WarpageRoiRow}, column {WarpageRoiColumn}, size {WarpageRoiRowCount} x {WarpageRoiColumnCount}, min samples {WarpageMinimumValidSamples}, frame {WarpageFrameId}"
            : string.Empty;
        var flatnessLine = PlaneFlatnessConfigured
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\nReference ROI ({PlaneFlatnessReferenceCenterX:F3}, {PlaneFlatnessReferenceCenterZ:F3}) half ({PlaneFlatnessReferenceHalfWidth:F3}, {PlaneFlatnessReferenceHalfDepth:F3})")
            : string.Empty;
        var pointPairLine = pointPairFirstReference is not null && pointPairSecondReference is not null
            ? $"\nPoint pair {pointPairFirstReference.Id} ({pointPairFirstReference.Row}, {pointPairFirstReference.Column}) -> {pointPairSecondReference.Id} ({pointPairSecondReference.Row}, {pointPairSecondReference.Column})"
            : string.Empty;
        var volumeLine = VolumeConfigured
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\nVolume reference ({PlaneFlatnessReferenceCenterX:F3}, {PlaneFlatnessReferenceCenterZ:F3}); measurement ({RecipeRoiLeftCenterX:F3}, {RecipeRoiLeftCenterZ:F3})")
            : string.Empty;
        var crossSectionLine = CrossSectionConfigured
            ? $"\nCross-section row {CrossSectionRow}, columns {CrossSectionStartColumn}..{CrossSectionEndColumn}"
            : string.Empty;
        RecipeParameterSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Transform T({RecipeTransformTranslateX:F3}, {RecipeTransformTranslateY:F3}, {RecipeTransformTranslateZ:F3}) R({RecipeTransformRotateXDegrees:F1}, {RecipeTransformRotateYDegrees:F1}, {RecipeTransformRotateZDegrees:F1}) S {RecipeTransformScale:F3}\nROI {RecipeRoiMode}: L({RecipeRoiLeftCenterX:F3}, {RecipeRoiLeftCenterZ:F3}) R({RecipeRoiRightCenterX:F3}, {RecipeRoiRightCenterZ:F3}){thicknessLine}{warpageLine}{flatnessLine}{pointPairLine}{volumeLine}{crossSectionLine}");
    }

    private void SetThicknessParameter(ref int storage, int value, string propertyName, string invalidationReason)
    {
        if (!SetField(ref storage, value, propertyName))
        {
            return;
        }

        MarkThicknessConfigurationChanged(invalidationReason);
    }

    private void SetThicknessParameter(ref double storage, double value, string propertyName, string invalidationReason)
    {
        if (!SetField(ref storage, value, propertyName))
        {
            return;
        }

        MarkThicknessConfigurationChanged(invalidationReason);
    }

    private void MarkThicknessConfigurationChanged(string invalidationReason)
    {
        ThicknessConfigured = true;
        InvalidateThicknessPreview(invalidationReason);
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshCommandCanExecute();
    }

    private void SetPlaneFlatnessParameter(ref double storage, double value, string propertyName)
    {
        if (!SetField(ref storage, value, propertyName))
        {
            return;
        }

        PlaneFlatnessConfigured = true;
        InvalidatePlaneFlatnessPreview("Flatness parameters changed; run Preview Flatness again");
        InvalidateVolumePreview("Reference ROI changed; run Preview Volume again");

        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    private void SetPointPairParameter(ref double storage, double value, string propertyName)
    {
        if (!SetField(ref storage, value, propertyName))
        {
            return;
        }

        PointPairDimensionsConfigured = true;
        InvalidatePointPairDimensionsPreview("Dimension parameters changed; run Preview Dimensions again");
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    private void SetGapFlushParameter(ref double storage, double value, string propertyName)
    {
        if (!SetField(ref storage, value, propertyName))
        {
            return;
        }

        GapFlushConfigured = true;
        InvalidateGapFlushPreview("Gap / Flush parameters changed; run Preview Gap / Flush again");
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    private void SetVolumeParameter(ref double storage, double value, string propertyName)
    {
        if (!SetField(ref storage, value, propertyName)) return;
        VolumeConfigured = true;
        InvalidateVolumePreview("Volume parameters changed; run Preview Volume again");
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    private void SetCrossSectionParameter(ref int storage, int value, string propertyName)
    {
        if (!SetField(ref storage, value, propertyName)) return;
        CrossSectionConfigured = true;
        InvalidateCrossSectionPreview("Cross-section selectors changed; run Preview Cross-section again");
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    private void SetCrossSectionParameter(ref double storage, double value, string propertyName)
    {
        if (!SetField(ref storage, value, propertyName)) return;
        CrossSectionConfigured = true;
        InvalidateCrossSectionPreview("Cross-section acceptance changed; run Preview Cross-section again");
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    private void OnRecipeRoiChanged()
    {
        InvalidateGapFlushPreview("ROI parameters changed; run Preview Gap / Flush again");
        InvalidateVolumePreview("Measurement ROI changed; run Preview Volume again");
        RefreshRecipeParameterSummary();
    }

    private static string FormatVector(Vector3 point) =>
        string.Create(CultureInfo.InvariantCulture, $"({point.X:F3}, {point.Y:F3}, {point.Z:F3})");

    private static double CoerceFinite(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;

    private void NotifyRecipeTransformProperties()
    {
        OnPropertyChanged(nameof(RecipeTransformTranslateX));
        OnPropertyChanged(nameof(RecipeTransformTranslateY));
        OnPropertyChanged(nameof(RecipeTransformTranslateZ));
        OnPropertyChanged(nameof(RecipeTransformRotateXDegrees));
        OnPropertyChanged(nameof(RecipeTransformRotateYDegrees));
        OnPropertyChanged(nameof(RecipeTransformRotateZDegrees));
        OnPropertyChanged(nameof(RecipeTransformScale));
    }

}
