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
    public void SetRoiStepSelectionPending(string summary, string details, string selectionMode)
    {
        RoiStepMeasurementVisible = true;
        RoiStepSelectionMode = selectionMode;
        RoiStepLeftPointCount = 0;
        RoiStepRightPointCount = 0;
        RoiStepLeftRawMean = double.NaN;
        RoiStepRightRawMean = double.NaN;
        RoiStepRawHeightDelta = double.NaN;
        RoiStepModelHeightDelta = double.NaN;
        RoiStepMeasurementSummary = summary;
        RoiStepMeasurementDetails = details;
        RoiStepEditSummary = "ROI edit: click right ROI center to finish comparison.";
        SelectionSummary = details;
        MeasurementSummary = details;
        ViewerStatus = "ROI step left ROI set";
    }

    public void SetRoiStepMeasurement(int leftPointCount, double leftRawMean, double leftModelMeanY, int rightPointCount, double rightRawMean, double rightModelMeanY, string selectionMode)
    {
        var rawDelta = rightRawMean - leftRawMean;
        var modelDelta = rightModelMeanY - leftModelMeanY;

        RoiStepMeasurementVisible = true;
        RoiStepSelectionMode = selectionMode;
        RoiStepLeftPointCount = leftPointCount;
        RoiStepRightPointCount = rightPointCount;
        RoiStepLeftRawMean = leftRawMean;
        RoiStepRightRawMean = rightRawMean;
        RoiStepRawHeightDelta = rawDelta;
        RoiStepModelHeightDelta = modelDelta;
        RoiStepMeasurementSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"ROI step: L {leftPointCount:N0} pts vs R {rightPointCount:N0} pts");
        RoiStepMeasurementDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Mean raw L {leftRawMean:F3}, R {rightRawMean:F3} | step {rawDelta:F3} raw-height | model dY {modelDelta:F3}");
        RoiStepEditSummary = selectionMode == "Interactive"
            ? "ROI edit: interactive L/R centers selected; click again to start a new pair."
            : "ROI edit: auto regions; click left ROI then right ROI.";
        SelectionSummary = RoiStepMeasurementDetails;
        MeasurementSummary = RoiStepMeasurementDetails;
        ViewerStatus = "ROI step comparison updated";
    }

    public void ClearRoiStepMeasurement(string details = "Left/right ROI height delta: pending")
    {
        RoiStepMeasurementVisible = false;
        RoiStepLeftPointCount = 0;
        RoiStepRightPointCount = 0;
        RoiStepLeftRawMean = double.NaN;
        RoiStepRightRawMean = double.NaN;
        RoiStepRawHeightDelta = double.NaN;
        RoiStepModelHeightDelta = double.NaN;
        RoiStepMeasurementSummary = "ROI step: compare two C3D regions.";
        RoiStepMeasurementDetails = details;
        RoiStepEditSummary = "ROI edit: auto regions; click left ROI then right ROI.";
        RoiStepSelectionMode = "Auto";
        if (SelectedSelectionMode == "ROI Step Compare")
        {
            SelectionSummary = RoiStepMeasurementDetails;
        }
    }

    public void SetC3DAlignment(ModelTransform transform, string alignmentName, string referenceName)
    {
        if (transform != C3DModelTransform)
        {
            InvalidatePointPairDimensionsPreview("Alignment changed; run Preview Dimensions again");
            InvalidateGapFlushPreview("Alignment changed; run Preview Gap / Flush again");
            InvalidateVolumePreview("Alignment changed; run Preview Volume again");
            InvalidateCrossSectionPreview("Alignment changed; run Preview Cross-section again");
        }

        C3DModelTransform = transform;
        RefreshSourceEntities();
        TransformSummary = $"Transform: {FormatModelTransform(transform)}";
        AlignmentSummary = $"Alignment: {alignmentName} | reference {referenceName}";
        CoordinateMappingSummary = ModelTransformIsIdentity(transform)
            ? "Mapping: source = aligned | raw-height retained"
            : "Mapping: source -> aligned display coordinates | raw-height retained";
        ViewerStatus = $"Alignment state: {alignmentName}";
        RefreshSceneContracts();
        NotifyRecipeTransformProperties();
        RefreshRecipeParameterSummary();
    }

    public void SetGlbSampleSource(string sourcePath, string sourceName, string format = "GLB")
    {
        ImportedMeshFormat = string.IsNullOrWhiteSpace(format) ? "GLB" : format.Trim().ToUpperInvariant();
        GlbSampleSourcePath = sourcePath;
        GlbSampleName = string.IsNullOrWhiteSpace(sourceName) ? ImportedMeshDisplayName() : sourceName;
        OnPropertyChanged(nameof(ImportedMeshLayerLabel));
        RefreshSourceEntities();
        RefreshSceneContracts();
    }

    internal void SetImportedMeshDisplayCapabilities(bool sourceColorAvailable)
    {
        if (importedMeshSourceColorAvailable == sourceColorAvailable)
        {
            return;
        }

        importedMeshSourceColorAvailable = sourceColorAvailable;
        if (GlbSampleVisible)
        {
            RefreshSceneContracts();
        }
    }

    internal void SetC3DDisplayCapabilities(bool surfaceGeometryAvailable)
    {
        if (c3dSurfaceGeometryAvailable == surfaceGeometryAvailable)
        {
            return;
        }

        c3dSurfaceGeometryAvailable = surfaceGeometryAvailable;
        if (C3DSampleVisible)
        {
            RefreshSceneContracts();
        }
    }

    public void SetGlbSampleBounds(Vector3 min, Vector3 max)
    {
        importedMeshFitCenter = (min + max) * 0.5f;
        var radius = Math.Max(0.001, Vector3.Distance(min, max) * 0.5);
        importedMeshFitDistance = Math.Clamp(radius / Math.Tan(Math.PI / 8.0) * 1.7, 0.35, 12000.0);
    }

    public void SetLazSampleSource(string sourcePath, string sourceName)
    {
        LazSampleSourcePath = sourcePath;
        LazSampleName = string.IsNullOrWhiteSpace(sourceName) ? "Public LAZ/LAS Point Cloud" : sourceName;
        RefreshSourceEntities();
        RefreshSceneContracts();
    }

    internal void SetLazDisplayCapabilities(bool sourceColorAvailable)
    {
        if (lazSourceColorAvailable == sourceColorAvailable)
        {
            return;
        }

        lazSourceColorAvailable = sourceColorAvailable;
        if (LazSampleVisible)
        {
            RefreshSceneContracts();
        }
    }

    public void SetLazSampleBounds(Vector3 min, Vector3 max)
    {
        lazFitCenter = (min + max) * 0.5f;
        var radius = Math.Max(1.0, Vector3.Distance(min, max) * 0.5);
        lazFitDistance = Math.Clamp(radius / Math.Tan(Math.PI / 8.0) * 1.35, 80.0, 12000.0);
    }

    public void SetLazHeightRange(double minimum, double maximum, string unit)
    {
        lazHeightMinimum = CoerceFinite(minimum, double.NaN);
        lazHeightMaximum = CoerceFinite(maximum, double.NaN);
        lazHeightUnit = string.IsNullOrWhiteSpace(unit) ? "source-z" : unit;
        PointCloudColorLegendTitle = "Point Cloud Height Scale";
        PointCloudColorLegendLow = double.IsFinite(lazHeightMinimum)
            ? string.Create(CultureInfo.InvariantCulture, $"Low: {lazHeightMinimum:F3} {lazHeightUnit}")
            : "Low: not loaded";
        PointCloudColorLegendHigh = double.IsFinite(lazHeightMaximum)
            ? string.Create(CultureInfo.InvariantCulture, $"High: {lazHeightMaximum:F3} {lazHeightUnit}")
            : "High: not loaded";
        PointCloudColorLegendScale = "Scale: source Z min to max";
        RefreshPointCloudColorLegend();
    }

    public void SetRecipeRoiStepEdit(
        string mode,
        double leftCenterX,
        double leftCenterZ,
        double leftHalfWidth,
        double leftHalfDepth,
        double rightCenterX,
        double rightCenterZ,
        double rightHalfWidth,
        double rightHalfDepth,
        int maxSampledPoints)
    {
        InvalidateGapFlushPreview("ROI parameters changed; run Preview Gap / Flush again");
        RecipeRoiMode = mode;
        SetField(ref recipeRoiLeftCenterX, CoerceFinite(leftCenterX, recipeRoiLeftCenterX), nameof(RecipeRoiLeftCenterX));
        SetField(ref recipeRoiLeftCenterZ, CoerceFinite(leftCenterZ, recipeRoiLeftCenterZ), nameof(RecipeRoiLeftCenterZ));
        SetField(ref recipeRoiLeftHalfWidth, Math.Max(0.0001, CoerceFinite(leftHalfWidth, recipeRoiLeftHalfWidth)), nameof(RecipeRoiLeftHalfWidth));
        SetField(ref recipeRoiLeftHalfDepth, Math.Max(0.0001, CoerceFinite(leftHalfDepth, recipeRoiLeftHalfDepth)), nameof(RecipeRoiLeftHalfDepth));
        SetField(ref recipeRoiRightCenterX, CoerceFinite(rightCenterX, recipeRoiRightCenterX), nameof(RecipeRoiRightCenterX));
        SetField(ref recipeRoiRightCenterZ, CoerceFinite(rightCenterZ, recipeRoiRightCenterZ), nameof(RecipeRoiRightCenterZ));
        SetField(ref recipeRoiRightHalfWidth, Math.Max(0.0001, CoerceFinite(rightHalfWidth, recipeRoiRightHalfWidth)), nameof(RecipeRoiRightHalfWidth));
        SetField(ref recipeRoiRightHalfDepth, Math.Max(0.0001, CoerceFinite(rightHalfDepth, recipeRoiRightHalfDepth)), nameof(RecipeRoiRightHalfDepth));
        RecipeRoiMaxSampledPoints = maxSampledPoints;
        RefreshRecipeParameterSummary();
    }

    public void SetAlignmentWorkflowSummary(string summary)
    {
        AlignmentWorkflowSummary = string.IsNullOrWhiteSpace(summary) ? "ROI alignment: not applied" : summary;
    }

    public void SetRecipeValidationSummary(string summary)
    {
        RecipeValidationSummary = string.IsNullOrWhiteSpace(summary) || summary == "Validation: OK" ? string.Empty : summary;
    }

    private void SetRecipeTransform(ModelTransform transform)
    {
        SetC3DAlignment(transform, "Manual recipe alignment", RecipeSourceName);
    }

    public void SetRenderPerformance(double fps, double drawMilliseconds)
    {
        ViewportFps = fps;
        ViewportDrawMilliseconds = drawMilliseconds;
        PerformanceSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Performance: {fps:F1} fps | draw {drawMilliseconds:F2} ms | C3D points {C3DSamplePointCount}");
    }

    internal void ResetRenderPerformance()
    {
        ViewportFps = double.NaN;
        ViewportDrawMilliseconds = double.NaN;
        PerformanceSummary = "Performance: waiting for first frame";
    }

    public void SetLazSamplingTelemetry(ulong decodedPointCount, int sampledPointCount, int sampleStride, double loadMilliseconds)
    {
        var percent = decodedPointCount > 0
            ? sampledPointCount / (double)decodedPointCount * 100.0
            : 0.0;
        LazLoadMilliseconds = CoerceFinite(loadMilliseconds, double.NaN);
        LazSamplePercent = percent;
        LazSampleStride = Math.Max(0, sampleStride);
        LazSamplingSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"LAZ/LAS sampling: load {LazLoadMilliseconds:F0} ms | sampled {sampledPointCount:N0}/{decodedPointCount:N0} ({percent:F2}%) | stride {LazSampleStride} | density {SelectedRenderDensity}");
    }

    public void ClearLazSamplingTelemetry(string summary = "LAZ/LAS sampling: not loaded")
    {
        LazLoadMilliseconds = double.NaN;
        LazSamplePercent = double.NaN;
        LazSampleStride = 0;
        LazSamplingSummary = string.IsNullOrWhiteSpace(summary) ? "LAZ/LAS sampling: not loaded" : summary;
    }

}
