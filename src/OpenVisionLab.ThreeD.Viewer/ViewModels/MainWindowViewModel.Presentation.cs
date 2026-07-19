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
    public void UseC3DHeightDeviationRuleSmokeScene()
    {
        UseC3DSmokeScene();
        MeasurementVisible = false;
        MeasurementSummary = "C3D rule preview uses raw height statistics.";
        ResultOverlayVisible = true;
        SelectedColorMode = "Deviation";
        SelectedEntity = "C3D Height Deviation Rule";
        PickCoordinate = "(sample-backed rule)";
        ViewerStatus = "Smoke scene: C3D height deviation rule";
    }

    public void SetC3DHeightDeviationPreview(ToolResult result)
    {
        ClearPlaneFlatnessPreview();
        ClearPointPairDimensionsPreview();
        c3dHeightDeviationPreview = result;
        if (ResultOverlayVisible && C3DSampleVisible)
        {
            ApplyActivePreviewResult();
        }
    }

    public bool PublishPreviewResult()
    {
        if (PreviewToolResult.Status == ResultStatus.NotRun)
        {
            ViewerStatus = "No preview result to publish";
            return false;
        }

        var resultEntity = CreatePublishedResultEntity(PreviewToolResult);
        ResultEntities = [resultEntity];
        PublishedResultSummary = FormatPublishedResult(resultEntity);
        ViewerStatus = $"Result published: {resultEntity.Name}";
        RefreshSceneContracts();
        return true;
    }

    public void ConfigureNominalActualComparison(NominalActualComparisonInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        NominalActualInput = input;
        RefreshSourceEntities();
        NominalActual.ApplyInputValidation(
            FormatNominalActualSource("Actual", input.ActualSource),
            FormatNominalActualSource("Nominal", input.NominalSource),
            FormatNominalActualSource("Validation query", input.QuerySource),
            $"Frame: {input.FrameId} | Units: {input.Unit}",
            $"Alignment: {input.AlignmentId}",
            input.Unit,
            input.SourceFingerprint);
        NominalActual.ActualVisible = true;
        NominalActual.NominalVisible = false;
        SelectedEntity = "Nominal / Actual Surface Deviation";
        MeasurementSummary = "Inputs validated; explicit Preview requested.";
        RefreshSceneContracts();
    }

    public void ClearNominalActualComparison(string validationIssue)
    {
        NominalActualInput = null;
        RefreshSourceEntities();
        NominalActual.ApplyInputValidation(
            "Actual: not loaded",
            "Nominal: not loaded",
            "Validation query: not loaded",
            "Frame: not set | Units: not set",
            "Alignment: not set",
            "(not set)",
            "(none)",
            validationIssue);
        RefreshSceneContracts();
    }

    public bool PublishNominalActualComparison(NominalActualComparisonResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (NominalActualInput is null
            || NominalActual.PreviewResult is null
            || !result.Input.ExecutionFingerprint.Equals(
                NominalActual.CompletedPreviewFingerprint,
                StringComparison.Ordinal)
            || !result.Input.SourceFingerprint.Equals(
                NominalActualInput.SourceFingerprint,
                StringComparison.Ordinal))
        {
            ViewerStatus = "Nominal/actual Publish rejected: Preview evidence is stale or missing";
            return false;
        }

        var resultEntity = NominalActualComparisonContract.CreateResultEntity(result);
        ResultEntities = [resultEntity];
        PublishedResultSummary = FormatPublishedResult(resultEntity);
        ViewerStatus = $"Result published: {resultEntity.Name}";
        RefreshSceneContracts();
        return true;
    }

    public void Pan(double deltaX, double deltaY, double deltaZ)
    {
        CameraTargetX += deltaX;
        CameraTargetY += deltaY;
        CameraTargetZ += deltaZ;
        ViewerStatus = "Camera panned";
        UpdateCameraStatus();
    }

    public void ZoomCamera(double zoomScale)
    {
        var minimumDistance = GlbSampleVisible
            ? Math.Max(0.05, importedMeshFitDistance * 0.02)
            : 2.4;
        var maximumDistance = GlbSampleVisible
            ? Math.Max(20.0, importedMeshFitDistance * 2.5)
            : LazSampleVisible
                ? Math.Max(20.0, lazFitDistance * 2.5)
                : 20.0;
        CameraDistance = Math.Clamp(CameraDistance * zoomScale, minimumDistance, maximumDistance);
        UpdateCameraStatus();
    }

    public void UpdateCameraStatus()
    {
        var modelUnit = NominalActual.InputsReady ? NominalActual.Unit : "unitless";
        BottomStatus = $"Model units: {modelUnit} | Camera: yaw {YawDegrees:F1}, pitch {PitchDegrees:F1}, distance {CameraDistance:F2}, target ({CameraTargetX:F2}, {CameraTargetY:F2}, {CameraTargetZ:F2})";
    }

    private void SetCameraTarget(double x, double y, double z)
    {
        CameraTargetX = x;
        CameraTargetY = y;
        CameraTargetZ = z;
    }

    private void FitGlbCamera()
    {
        SetCameraTarget(importedMeshFitCenter.X, importedMeshFitCenter.Y, importedMeshFitCenter.Z);
        CameraDistance = importedMeshFitDistance;
    }

    private void FitLazCamera()
    {
        SetCameraTarget(lazFitCenter.X, lazFitCenter.Y, lazFitCenter.Z);
        CameraDistance = lazFitDistance;
    }

    private void RefreshSceneContracts()
    {
        RefreshDisplaySettingsContext();

        var layers = new List<EntityLayer>
        {
            new EntityLayer("layer.source.generated-cube", "Generated Unit Cube", LayerKind.Source, CubeVisible, [CubeEntityId]),
            new EntityLayer("layer.source.generated-point-cloud", "Generated Point Cloud", LayerKind.Source, PointCloudVisible, [PointCloudEntityId]),
            new EntityLayer("layer.source.c3d-thickness", "C3D Thickness Sample", LayerKind.Source, C3DSampleVisible && activePreviewSourceEntityId != C3DWarpageEntityId, [C3DEntityId]),
            new EntityLayer("layer.source.c3d-warpage", "C3D Warpage Sample", LayerKind.Source, C3DSampleVisible && activePreviewSourceEntityId == C3DWarpageEntityId, [C3DWarpageEntityId]),
            new EntityLayer("layer.source.imported-mesh", GlbSampleName, LayerKind.Source, GlbSampleVisible, [GlbEntityId]),
            new EntityLayer("layer.source.public-laz-manuscript", "Public LAZ/LAS Point Cloud", LayerKind.Source, LazSampleVisible, [LazEntityId])
        };

        if (NominalActualInput is { } comparisonInput)
        {
            layers.Add(new EntityLayer(
                "layer.source.nominal-actual-measured",
                "Nominal / Actual Measured Source",
                LayerKind.Source,
                NominalActual.ActualVisible,
                [comparisonInput.ActualSource.Id, comparisonInput.QuerySource.Id]));
            layers.Add(new EntityLayer(
                "layer.source.nominal-actual-nominal",
                "Nominal / Actual Nominal Source",
                LayerKind.Source,
                NominalActual.NominalVisible,
                [comparisonInput.NominalSource.Id]));
            if (NominalActual.PreviewResult is not null)
            {
                layers.Add(new EntityLayer(
                    "layer.preview.nominal-actual-surface-deviation",
                    "Preview: Nominal / Actual Surface Deviation",
                    LayerKind.Preview,
                    NominalActual.ActualVisible,
                    [comparisonInput.ActualSource.Id, comparisonInput.QuerySource.Id]));
            }
        }

        if (PreviewToolResult.Status != ResultStatus.NotRun)
        {
            layers.Add(new EntityLayer(
                activePreviewLayerId,
                activePreviewLayerName,
                LayerKind.Preview,
                ResultOverlayVisible,
                [activePreviewSourceEntityId]));
        }

        if (ResultEntities.Count > 0)
        {
            var resultLayer = CreatePublishedResultLayer(ResultEntities);
            layers.Add(new EntityLayer(
                resultLayer.Id,
                resultLayer.Name,
                LayerKind.Result,
                true,
                ResultEntities.Select(entity => entity.Id).ToArray()));
        }

        EntityLayers = layers;

        var sourceLayerCount = EntityLayers.Count(layer => layer.Kind == LayerKind.Source);
        var visibleSourceLayerCount = EntityLayers.Count(layer => layer.Kind == LayerKind.Source && layer.IsVisible);
        var previewLayerCount = EntityLayers.Count(layer => layer.Kind == LayerKind.Preview);
        SceneContractSummary =
            $"Source entities: {SourceEntities.Count} | Source layers: {sourceLayerCount} | Visible source layers: {visibleSourceLayerCount} | Preview layers: {previewLayerCount} | Published results: {ResultEntities.Count}";
    }

    private void RefreshDisplaySettingsContext()
    {
        if (NominalActualInput is not null && NominalActual.ActualVisible)
        {
            Display.ConfigureNominalActualComparison(NominalActual.PreviewResult is not null);
        }
        else if (LazSampleVisible)
        {
            Display.ConfigurePointCloud(lazSourceColorAvailable);
        }
        else if (GlbSampleVisible)
        {
            Display.ConfigureImportedMesh(importedMeshSourceColorAvailable);
        }
        else if (C3DSampleVisible)
        {
            Display.ConfigureC3DHeightGrid(
                ResultOverlayVisible && (activePreviewSourceEntityId == C3DEntityId || activePreviewSourceEntityId == C3DWarpageEntityId),
                c3dSurfaceGeometryAvailable);
        }
        else
        {
            Display.ConfigureGeneratedGeometry(
                ResultOverlayVisible && activePreviewSourceEntityId == PointCloudEntityId);
        }

        RefreshC3DHeightDistributionLegend();
    }

    private static ToolResult CreateNotRunToolResult() =>
        new(
            "Synthetic Height Deviation Preview",
            ResultStatus.NotRun,
            "No preview result is active.",
            TimeSpan.Zero,
            [],
            []);

    private static ToolResult CreateSyntheticHeightDeviationPreview() =>
        new(
            "Synthetic Height Deviation Preview",
            ResultStatus.Warning,
            "Preview only; source geometry is unchanged and no result is published.",
            TimeSpan.Zero,
            [
                new Metric("Synthetic peak deviation", MetricKind.Deviation, 0.42, "unitless", ResultStatus.Warning),
                new Metric("Preview overlay count", MetricKind.Count, 3, "count", ResultStatus.Warning)
            ],
            [
                new Overlay("overlay.synthetic-pass-band", OverlayKind.Box, "PASS tolerance band", ResultStatus.Pass, PointCloudEntityId),
                new Overlay("overlay.synthetic-profile", OverlayKind.Polyline, "Preview profile line", ResultStatus.Warning, PointCloudEntityId),
                new Overlay("overlay.synthetic-fail-markers", OverlayKind.Marker, "FAIL marker cluster", ResultStatus.Fail, PointCloudEntityId)
            ]);

    private void RefreshLazTwoPointAcceptanceState()
    {
        if (activeResultEntityId != LazTwoPointResultEntityId
            || !TwoPointMeasurementVisible
            || !double.IsFinite(TwoPointDistance)
            || !double.IsFinite(TwoPointRawHeightDelta))
        {
            return;
        }

        var distanceStatus = Math.Abs(TwoPointDistance - LazTwoPointExpectedDistance) <= LazTwoPointDistanceTolerance
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        var heightStatus = Math.Abs(TwoPointRawHeightDelta - LazTwoPointExpectedHeightDelta) <= LazTwoPointHeightDeltaTolerance
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        var status = distanceStatus == ResultStatus.Pass && heightStatus == ResultStatus.Pass
            ? ResultStatus.Pass
            : ResultStatus.Fail;

        LazTwoPointAcceptanceSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"LAZ/LAS acceptance: {status} | distance {TwoPointDistance:F3} vs {LazTwoPointExpectedDistance:F3} +/- {LazTwoPointDistanceTolerance:F3} model | height delta {TwoPointRawHeightDelta:F3} vs {LazTwoPointExpectedHeightDelta:F3} +/- {LazTwoPointHeightDeltaTolerance:F3} {lazTwoPointPreviewHeightUnit}");

        if (lazTwoPointPreviewFirst is { } first && lazTwoPointPreviewSecond is { } second)
        {
            PreviewToolResult = CreateLazTwoPointMeasurementPreview(
                first,
                second,
                TwoPointRawHeightDelta,
                lazTwoPointPreviewHeightUnit,
                distanceStatus,
                heightStatus,
                status);
            ResultSummary = FormatToolResult(PreviewToolResult);
        }

        RefreshSceneContracts();
    }

    private ToolResult CreateLazTwoPointMeasurementPreview(
        Vector3 first,
        Vector3 second,
        double heightDelta,
        string heightUnit,
        ResultStatus distanceStatus,
        ResultStatus heightStatus,
        ResultStatus status)
    {
        var delta = second - first;
        var distance = delta.Length();

        return new ToolResult(
            "LAZ/LAS Two Point Measurement",
            status,
            status == ResultStatus.Pass
                ? "Preview within configured tolerance; source point cloud is unchanged and no result is published."
                : "Preview exceeds configured tolerance; source point cloud is unchanged and no result is published.",
            TimeSpan.Zero,
            [
                new Metric("Distance", MetricKind.Length, distance, "model", distanceStatus),
                new Metric("Delta X", MetricKind.Length, delta.X, "model", ResultStatus.Pass),
                new Metric("Delta Y", MetricKind.Length, delta.Y, "model", ResultStatus.Pass),
                new Metric("Delta Z", MetricKind.Length, delta.Z, "model", ResultStatus.Pass),
                new Metric("Source Z height delta", MetricKind.Length, heightDelta, heightUnit, heightStatus)
            ],
            [
                new Overlay("overlay.laz-two-point-line", OverlayKind.Polyline, "LAZ/LAS two-point distance line", distanceStatus, LazEntityId),
                new Overlay("overlay.laz-two-point-height-marker", OverlayKind.Marker, "LAZ/LAS source-Z height delta marker", heightStatus, LazEntityId)
            ]);
    }

    private ResultEntity CreatePublishedResultEntity(ToolResult result) =>
        new(
            activeResultEntityId,
            activeResultEntityName,
            activePreviewSourceEntityId,
            result.Status,
            "Published from preview; source geometry remains unchanged.",
            result.Metrics,
            result.Overlays);

    private static (string Id, string Name) CreatePublishedResultLayer(IReadOnlyList<ResultEntity> results)
    {
        var firstResult = results[0];
        return firstResult.Id switch
        {
            NominalActualComparisonContract.ResultEntityId =>
                (NominalActualComparisonContract.ResultLayerId, NominalActualComparisonContract.ResultLayerName),
            C3DHeightDeviationResultEntityId => ("layer.result.c3d-height-deviation", "Published C3D Height Deviation"),
            C3DThicknessResultEntityId => ("layer.result.c3d-thickness", "Published C3D Thickness"),
            C3DWarpageResultEntityId => ("layer.result.c3d-warpage", "Published C3D Warpage"),
            C3DPlaneFlatnessResultEntityId => ("layer.result.c3d-plane-flatness", "Published C3D Plane Flatness"),
            C3DPointPairDimensionsResultEntityId => ("layer.result.c3d-point-pair-dimensions", "Published C3D Point Pair Dimensions"),
            LazTwoPointResultEntityId => ("layer.result.laz-two-point-measurement", "Published LAZ/LAS Two Point Measurement"),
            SyntheticResultEntityId => ("layer.result.synthetic-height-deviation", "Published Synthetic Height Deviation"),
            _ => ($"layer.{firstResult.Id}", firstResult.Name)
        };
    }

    private string ImportedMeshDisplayName() =>
        ImportedMeshFormat == "GLB" ? "Public GLB Mesh" : $"{ImportedMeshFormat} Mesh";

    private void ApplyActivePreviewResult()
    {
        if (C3DSampleVisible && c3dWarpagePreviewActive && c3dWarpagePreview is not null)
        {
            activePreviewLayerId = "layer.preview.c3d-warpage";
            activePreviewLayerName = "Preview: C3D Warpage";
            activePreviewSourceEntityId = C3DWarpageEntityId;
            activeResultEntityId = C3DWarpageResultEntityId;
            activeResultEntityName = "Published C3D Warpage";
            PreviewToolResult = c3dWarpagePreview;
        }
        else if (C3DSampleVisible && c3dThicknessPreviewActive && c3dThicknessPreview is not null)
        {
            activePreviewLayerId = "layer.preview.c3d-thickness";
            activePreviewLayerName = "Preview: C3D Thickness";
            activePreviewSourceEntityId = C3DEntityId;
            activeResultEntityId = C3DThicknessResultEntityId;
            activeResultEntityName = "Published C3D Thickness";
            PreviewToolResult = c3dThicknessPreview;
        }
        else if (C3DSampleVisible && c3dPointPairDimensionsPreviewActive && c3dPointPairDimensionsPreview is not null)
        {
            activePreviewLayerId = "layer.preview.c3d-point-pair-dimensions";
            activePreviewLayerName = "Preview: C3D Point Pair Dimensions";
            activePreviewSourceEntityId = C3DEntityId;
            activeResultEntityId = C3DPointPairDimensionsResultEntityId;
            activeResultEntityName = "Published C3D Point Pair Dimensions";
            PreviewToolResult = c3dPointPairDimensionsPreview;
        }
        else if (C3DSampleVisible && c3dPlaneFlatnessPreviewActive && c3dPlaneFlatnessPreview is not null)
        {
            activePreviewLayerId = "layer.preview.c3d-plane-flatness";
            activePreviewLayerName = "Preview: C3D Plane Flatness";
            activePreviewSourceEntityId = C3DEntityId;
            activeResultEntityId = C3DPlaneFlatnessResultEntityId;
            activeResultEntityName = "Published C3D Plane Flatness";
            PreviewToolResult = c3dPlaneFlatnessPreview;
        }
        else if (C3DSampleVisible && c3dHeightDeviationPreview is not null)
        {
            activePreviewLayerId = "layer.preview.c3d-height-deviation";
            activePreviewLayerName = "Preview: C3D Height Deviation Rule";
            activePreviewSourceEntityId = C3DEntityId;
            activeResultEntityId = C3DHeightDeviationResultEntityId;
            activeResultEntityName = "Published C3D Height Deviation";
            PreviewToolResult = c3dHeightDeviationPreview;
        }
        else
        {
            ResetActivePreviewIdentity();
            PreviewToolResult = CreateSyntheticHeightDeviationPreview();
        }

        ResultSummary = FormatToolResult(PreviewToolResult);
        RefreshSceneContracts();
    }

    private void ResetActivePreviewIdentity()
    {
        activePreviewLayerId = "layer.preview.synthetic-height-deviation";
        activePreviewLayerName = "Preview: Synthetic Height Deviation";
        activePreviewSourceEntityId = PointCloudEntityId;
        activeResultEntityId = SyntheticResultEntityId;
        activeResultEntityName = "Published Synthetic Height Deviation";
    }

    private void RefreshDeviationLegend(ToolResult result)
    {
        if ((activePreviewSourceEntityId != C3DEntityId && activePreviewSourceEntityId != C3DWarpageEntityId)
            || result.Status == ResultStatus.NotRun)
        {
            HideDeviationLegend();
            return;
        }

        var flatness = result.Metrics.FirstOrDefault(metric => metric.Name == "Flatness");
        var peak = flatness
            ?? result.Metrics.FirstOrDefault(metric => metric.Name == "PeakToValley")
            ?? result.Metrics.FirstOrDefault(metric => metric.Name == "Peak absolute deviation");
        var tolerance = flatness is not null
            ? result.Metrics.FirstOrDefault(metric => metric.Name == "Flatness tolerance")
            : result.Metrics.FirstOrDefault(metric => metric.Name == "MaximumPeakToValley")
                ?? result.Metrics.FirstOrDefault(metric => metric.Name == "Peak tolerance");
        if (peak is null || tolerance is null)
        {
            HideDeviationLegend();
            return;
        }

        var unit = peak?.Unit ?? tolerance?.Unit ?? "raw-height";
        var statusText = result.Status switch
        {
            ResultStatus.Pass => "Status: Pass | within tolerance",
            ResultStatus.Fail => "Status: Fail | above tolerance",
            ResultStatus.Warning => "Status: Warning | review tolerance",
            ResultStatus.Error => "Status: Error | invalid result",
            _ => $"Status: {result.Status}"
        };

        DeviationLegendStatus = statusText;
        DeviationLegendPeak = peak is null
            ? "Peak: none"
            : string.Create(CultureInfo.InvariantCulture, $"{(flatness is null ? "Peak" : "Flatness")}: {peak.Value:F3} {unit}");
        DeviationLegendTolerance = tolerance is null
            ? "Tolerance: none"
            : string.Create(CultureInfo.InvariantCulture, $"Tolerance: +/- {tolerance.Value:F3} {unit}");
        DeviationLegendScale = flatness is null
            ? "Scale: 0 = mean, 1 = peak deviation"
            : "Scale: signed deviation to ROI reference plane";
        DeviationLegendLowLabel = flatness is null ? "Mean" : "Negative";
        DeviationLegendMiddleLabel = flatness is null ? "Tolerance" : "Zero";
        DeviationLegendHighLabel = flatness is null ? "Peak" : "Positive";
        DeviationLegendVisible = true;
    }

    private void HideDeviationLegend()
    {
        DeviationLegendVisible = false;
        DeviationLegendStatus = "Status: inactive";
        DeviationLegendPeak = "Peak: none";
        DeviationLegendTolerance = "Tolerance: none";
        DeviationLegendScale = "Scale: mean to peak deviation";
        DeviationLegendLowLabel = "Mean";
        DeviationLegendMiddleLabel = "Tolerance";
        DeviationLegendHighLabel = "Peak";
    }

    private void RefreshPointCloudColorLegend()
    {
        PointCloudColorLegendVisible = LazSampleVisible
            && SelectedColorMode == "Height"
            && double.IsFinite(lazHeightMinimum)
            && double.IsFinite(lazHeightMaximum)
            && lazHeightMaximum > lazHeightMinimum;
    }

    private static string FormatToolResult(ToolResult result)
    {
        if (result.Status == ResultStatus.NotRun)
        {
            return "Result overlay hidden";
        }

        var metric = result.Metrics.FirstOrDefault(metric => metric.Status is not null) ?? result.Metrics.First();
        return $"Preview: {result.ToolName}: {result.Status}\n{result.Message}\nMetric: {metric.Name} = {metric.Value:F3} {metric.Unit}\nOverlays: {result.Overlays.Count}";
    }

    private static string FormatPublishedResult(ResultEntity result)
    {
        var metric = result.Metrics.First();
        return $"{result.Name}: {result.Status}\nSource: {result.SourceEntityId}\nMetric: {metric.Name} = {metric.Value:F3} {metric.Unit}\nLayer: published result";
    }

}
