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
    public void FitAll()
    {
        if (C3DSampleVisible)
        {
            SetCameraTarget(0.0, 0.0, 0.0);
            CameraDistance = 13.2;
        }
        else if (PointCloudVisible && CubeVisible)
        {
            SetCameraTarget(2.05, -0.25, 0.0);
            CameraDistance = 9.2;
        }
        else if (PointCloudVisible)
        {
            SetCameraTarget(3.2, -0.70, 0.0);
            CameraDistance = 7.2;
        }
        else if (GlbSampleVisible)
        {
            FitGlbCamera();
        }
        else if (LazSampleVisible)
        {
            FitLazCamera();
        }
        else
        {
            SetCameraTarget(0.0, 0.0, 0.0);
            CameraDistance = 5.2;
        }

        ViewerStatus = "Fit all visible entities";
        UpdateCameraStatus();
    }

    public void FitSelection()
    {
        if (SelectedEntity == "C3D Height Grid" && C3DSampleVisible)
        {
            SetCameraTarget(0.0, 0.0, 0.0);
            CameraDistance = 13.2;
            ViewerStatus = "Fit selected C3D height grid";
        }
        else if (SelectedEntity == "Generated Point Cloud" && PointCloudVisible)
        {
            SetCameraTarget(3.2, -0.70, 0.0);
            CameraDistance = 7.2;
            ViewerStatus = "Fit selected point cloud";
        }
        else if ((SelectedEntity == "Public GLB Mesh" || SelectedEntity == $"{ImportedMeshFormat} Mesh") && GlbSampleVisible)
        {
            FitGlbCamera();
            ViewerStatus = $"Fit selected {ImportedMeshFormat} mesh";
        }
        else if ((SelectedEntity == "Public LAZ/LAS Metadata" || SelectedEntity == "Public LAZ/LAS Point Cloud") && LazSampleVisible)
        {
            FitLazCamera();
            ViewerStatus = "Fit selected LAZ bounds";
        }
        else
        {
            SetCameraTarget(0.0, 0.0, 0.0);
            CameraDistance = 5.2;
            ViewerStatus = "Fit selected cube";
        }

        UpdateCameraStatus();
    }

    public void Reset()
    {
        YawDegrees = 38.0;
        PitchDegrees = 24.0;
        CameraDistance = 9.2;
        SetCameraTarget(2.05, -0.25, 0.0);
        SelectedEntity = "Generated Unit Cube";
        PickCoordinate = "(none)";
        ViewerStatus = "Camera reset";
        UpdateCameraStatus();
    }

    public void UsePointCloudSmokeScene()
    {
        HudDetailsVisible = true;
        ClearC3DLinkedViews();
        CubeVisible = false;
        MeasurementVisible = false;
        ResultOverlayVisible = false;
        C3DSampleVisible = false;
        GlbSampleVisible = false;
        LazSampleVisible = false;
        PointCloudVisible = true;
        SelectedColorMode = "Height";
        SelectedEntity = "Generated Point Cloud";
        PickCoordinate = "(none)";
        CameraDistance = 7.2;
        SetCameraTarget(3.2, -0.70, 0.0);
        ViewerStatus = "Smoke scene: generated point cloud";
        UpdateCameraStatus();
    }

    public void UseSelectionSmokeScene(string mode)
    {
        var keepCurrentC3DScene = (mode == "Section Plane" || mode == ThicknessRoiSelectionMode || mode == WarpageRoiSelectionMode || mode == "ROI Step Compare" || mode == "Gap / Flush" || mode == "Cross-section Dimensions") && C3DSampleVisible;
        if (!keepCurrentC3DScene)
        {
            UsePointCloudSmokeScene();
        }

        SelectionOverlayVisible = true;
        SelectedSelectionMode = mode;
        SelectedEntity = mode switch
        {
            "Box ROI" => "Box ROI",
            ThicknessRoiSelectionMode => "C3D Thickness ROI",
            WarpageRoiSelectionMode => "C3D Warpage ROI",
            "ROI Step Compare" => "ROI Step Compare",
            "Gap / Flush" => "C3D Gap / Flush",
            "Cross-section Dimensions" => "C3D Cross-section Dimensions",
            "Section Plane" => "Section Plane",
            _ => "Generated Point Cloud"
        };
        SelectionSummary = mode switch
        {
            "Section Plane" when SectionProfileVisible => SectionProfileSummary,
            "Section Plane" => "Section plane: profile not loaded",
            "Two Point Measure" => TwoPointMeasurementSummary,
            "Plane Distance" => PlaneReferenceMeasurementDetails,
            ThicknessRoiSelectionMode => ThicknessDetails,
            WarpageRoiSelectionMode => WarpageDetails,
            "ROI Step Compare" => RoiStepMeasurementDetails,
            "Gap / Flush" => GapFlushDetails,
            "Cross-section Dimensions" => CrossSectionDetails,
            "Box ROI" => "Box ROI: viewer state only",
            _ => "Point selection: generated point cloud peak"
        };
        ViewerStatus = $"Smoke scene: {mode}";
    }

    public void UseC3DSmokeScene()
    {
        HudDetailsVisible = true;
        CubeVisible = false;
        PointCloudVisible = false;
        GlbSampleVisible = false;
        LazSampleVisible = false;
        C3DSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = "C3D Height Grid";
        PickCoordinate = "(none)";
        MeasurementSummary = "C3D sample loaded; no measurement tool published";
        SelectionSummary = "Selection overlay hidden";
        SelectedColorMode = "Height";
        YawDegrees = 34.0;
        PitchDegrees = 52.0;
        CameraDistance = 13.2;
        SetCameraTarget(0.0, 0.0, 0.0);
        ViewerStatus = "Smoke scene: C3D height grid";
        UpdateCameraStatus();
    }

    public void UseEmptyTeachingScene(string status)
    {
        HudDetailsVisible = true;
        ClearC3DLinkedViews();
        CubeVisible = false;
        PointCloudVisible = false;
        GlbSampleVisible = false;
        LazSampleVisible = false;
        C3DSampleVisible = false;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = "No source";
        PickCoordinate = "(none)";
        TransformSummary = "Transform: not available";
        AlignmentSummary = "Alignment: no source";
        CoordinateMappingSummary = "Mapping: no source";
        MeasurementSummary = "No recipe source is ready.";
        SelectionSummary = "Load or relink a C3D source to begin teaching.";
        SelectedColorMode = "Height";
        YawDegrees = 34.0;
        PitchDegrees = 52.0;
        CameraDistance = 5.2;
        SetCameraTarget(0.0, 0.0, 0.0);
        ViewerStatus = status;
        UpdateCameraStatus();
    }

    public void UseGlbSmokeScene()
    {
        HudDetailsVisible = false;
        var meshLabel = ImportedMeshDisplayName();
        ClearC3DLinkedViews();
        CubeVisible = false;
        PointCloudVisible = false;
        C3DSampleVisible = false;
        LazSampleVisible = false;
        GlbSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = meshLabel;
        PickCoordinate = "(not picked)";
        SelectionSummary = $"Mesh selection: {ImportedMeshFormat} mesh loaded";
        MeasurementSummary = $"{ImportedMeshFormat} mesh loaded; measurement tools pending";
        SelectedColorMode = Display.AvailableColorMaps[0];
        YawDegrees = 38.0;
        PitchDegrees = 26.0;
        FitGlbCamera();
        ViewerStatus = $"Smoke scene: {ImportedMeshFormat} mesh";
        UpdateCameraStatus();
    }

    public void UseGlbFailureScene(string summary)
    {
        HudDetailsVisible = true;
        var meshLabel = ImportedMeshDisplayName();
        ClearC3DLinkedViews();
        CubeVisible = false;
        PointCloudVisible = false;
        C3DSampleVisible = false;
        LazSampleVisible = false;
        GlbSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = meshLabel;
        PickCoordinate = "(load failed)";
        SelectionSummary = summary;
        MeasurementSummary = $"{ImportedMeshFormat} load failed; see Viewer status and contract output.";
        SelectedColorMode = Display.AvailableColorMaps[0];
    }

    public void UseLazSmokeScene()
    {
        HudDetailsVisible = false;
        ClearC3DLinkedViews();
        CubeVisible = false;
        PointCloudVisible = false;
        C3DSampleVisible = false;
        GlbSampleVisible = false;
        LazSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = "Public LAZ/LAS Metadata";
        PickCoordinate = "(metadata only)";
        SelectionSummary = "Point selection: LAZ/LAS metadata loaded";
        MeasurementSummary = "LAZ header loaded; compressed point records not decoded yet";
        SelectedColorMode = "Solid";
        YawDegrees = 34.0;
        PitchDegrees = 34.0;
        FitLazCamera();
        ViewerStatus = "Smoke scene: public LAZ/LAS metadata";
        UpdateCameraStatus();
    }

    public void UseLazFailureScene(string summary)
    {
        HudDetailsVisible = true;
        ClearC3DLinkedViews();
        CubeVisible = false;
        PointCloudVisible = false;
        C3DSampleVisible = false;
        GlbSampleVisible = false;
        LazSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = "Public LAZ/LAS Point Cloud";
        PickCoordinate = "(load failed)";
        SelectionSummary = summary;
        MeasurementSummary = "LAZ/LAS load failed; see Viewer status and contract output.";
        SelectedColorMode = "Solid";
    }

    public void UseLazPointSmokeScene()
    {
        UseLazSmokeScene();
        SelectedEntity = "Public LAZ/LAS Point Cloud";
        PickCoordinate = "(sampled points)";
        SelectionSummary = "Point selection: LAZ/LAS sampled point cloud";
        MeasurementSummary = "LAZ/LAS point decode loaded; XYZ/RGB sampled points rendered.";
        SelectedColorMode = Display.AvailableColorMaps.Contains("RGB", StringComparer.Ordinal)
            ? "RGB"
            : "Height";
        ViewerStatus = "Smoke scene: public LAZ/LAS point cloud";
    }

    private void ClearC3DLinkedViews()
    {
        ClearThicknessPreview();
        ClearPlaneFlatnessPreview();
        ClearPointPairDimensionsPreview();
        ClearGapFlushPreview();
        ClearVolumePreview();
        ClearCrossSectionPreview();
        ClearHeightMap();
        ClearSectionProfile();
    }

    public void UseResultSmokeScene()
    {
        UsePointCloudSmokeScene();
        MeasurementVisible = true;
        SelectionOverlayVisible = true;
        ResultOverlayVisible = true;
        SelectedSelectionMode = "Box ROI";
        SelectedEntity = "Result Overlay";
        PickCoordinate = "(viewer-only sample)";
        ViewerStatus = "Smoke scene: result overlay";
    }

    public void SetTwoPointMeasurementStart(Vector3 point, float rawValue) =>
        SetTwoPointMeasurementStart(point, rawValue, "raw-height");

    public void SetTwoPointMeasurementStart(Vector3 point, float heightValue, string heightUnit)
    {
        TwoPointMeasurementVisible = true;
        TwoPointDistance = double.NaN;
        TwoPointDeltaX = double.NaN;
        TwoPointDeltaY = double.NaN;
        TwoPointDeltaZ = double.NaN;
        TwoPointRawHeightDelta = double.NaN;
        var valueLabel = heightUnit == "raw-height"
            ? $"raw {heightValue:F3}"
            : $"height {heightValue:F3} {heightUnit}";
        TwoPointMeasurementSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"P1: {FormatVector(point)} | {valueLabel}");
        TwoPointMeasurementDetails = "Pick P2 to measure distance and height delta.";
        SelectionSummary = TwoPointMeasurementSummary;
        MeasurementSummary = TwoPointMeasurementDetails;
        ViewerStatus = "Two-point P1 set";
    }

    public void SetTwoPointMeasurement(Vector3 first, float firstRaw, Vector3 second, float secondRaw) =>
        SetTwoPointMeasurement(first, firstRaw, second, secondRaw, "raw-height");

    public void SetTwoPointMeasurement(Vector3 first, float firstHeight, Vector3 second, float secondHeight, string heightUnit)
    {
        var delta = second - first;
        var distance = delta.Length();
        var heightDelta = secondHeight - firstHeight;
        var heightDeltaLabel = heightUnit == "raw-height"
            ? $"height delta {heightDelta:F3} raw-height"
            : $"height delta {heightDelta:F3} {heightUnit}";

        TwoPointMeasurementVisible = true;
        TwoPointDistance = distance;
        TwoPointDeltaX = delta.X;
        TwoPointDeltaY = delta.Y;
        TwoPointDeltaZ = delta.Z;
        TwoPointRawHeightDelta = heightDelta;
        TwoPointMeasurementSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"P1 {FormatVector(first)} -> P2 {FormatVector(second)}");
        TwoPointMeasurementDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Distance {distance:F3} model | dX {delta.X:F3}, dY {delta.Y:F3}, dZ {delta.Z:F3} | {heightDeltaLabel}");
        SelectionSummary = TwoPointMeasurementDetails;
        MeasurementSummary = TwoPointMeasurementDetails;
        LazTwoPointAcceptanceSummary = "LAZ/LAS acceptance: pending";
        ViewerStatus = "Two-point measurement updated";
    }

    public void SetLazTwoPointMeasurementPreview(Vector3 first, Vector3 second, double heightDelta, string heightUnit)
    {
        activePreviewLayerId = "layer.preview.laz-two-point-measurement";
        activePreviewLayerName = "Preview: LAZ/LAS Two Point Measurement";
        activePreviewSourceEntityId = LazEntityId;
        activeResultEntityId = LazTwoPointResultEntityId;
        activeResultEntityName = "Published LAZ/LAS Two Point Measurement";
        lazTwoPointPreviewFirst = first;
        lazTwoPointPreviewSecond = second;
        lazTwoPointPreviewHeightUnit = string.IsNullOrWhiteSpace(heightUnit) ? "source-z-units" : heightUnit;
        SetField(ref resultOverlayVisible, true, nameof(ResultOverlayVisible));
        RefreshLazTwoPointAcceptanceState();
        ViewerStatus = "LAZ/LAS two-point result preview ready";
    }

    public void ClearTwoPointMeasurement()
    {
        TwoPointMeasurementVisible = false;
        TwoPointDistance = double.NaN;
        TwoPointDeltaX = double.NaN;
        TwoPointDeltaY = double.NaN;
        TwoPointDeltaZ = double.NaN;
        TwoPointRawHeightDelta = double.NaN;
        TwoPointMeasurementSummary = "Two-point: pick P1 and P2 on the C3D height grid.";
        TwoPointMeasurementDetails = "Distance and height delta: pending";
        lazTwoPointPreviewFirst = null;
        lazTwoPointPreviewSecond = null;
        LazTwoPointAcceptanceSummary = "LAZ/LAS acceptance: pending";
        if (SelectedSelectionMode == "Two Point Measure")
        {
            SelectionSummary = TwoPointMeasurementSummary;
        }
    }

    public void SetPlaneReferenceMeasurement(HeightFieldPlaneFitResult result, string referenceName)
    {
        PlaneReferenceMeasurementVisible = true;
        PlaneReferenceSignedDistance = result.TargetSignedDistance;
        PlaneReferenceAbsoluteDistance = result.TargetAbsoluteDistance;
        PlaneReferenceY = result.TargetProjection.Y;
        PlaneReferenceTargetY = result.Target.Y;
        PlaneReferenceRawHeightDelta = result.TargetRawHeightDelta;
        PlaneReferenceNormalX = result.Normal.X;
        PlaneReferenceNormalY = result.Normal.Y;
        PlaneReferenceNormalZ = result.Normal.Z;
        PlaneReferenceFitRms = result.RootMeanSquareDistance;
        PlaneReferenceSampleCount = result.SampleCount;
        PlaneReferenceMeasurementSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Fitted plane: {referenceName} | {result.SampleCount:N0} samples | normal {FormatVector(result.Normal)}");
        PlaneReferenceMeasurementDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Orthogonal distance {result.TargetSignedDistance:F3} model | abs {result.TargetAbsoluteDistance:F3} | RMS {result.RootMeanSquareDistance:F3} | target {FormatVector(result.Target)} | raw residual {result.TargetRawHeightDelta:F3} raw-height");
        SelectionSummary = PlaneReferenceMeasurementDetails;
        MeasurementSummary = PlaneReferenceMeasurementDetails;
        ViewerStatus = "Fitted C3D reference plane updated";
    }

    public void ClearPlaneReferenceMeasurement()
    {
        PlaneReferenceMeasurementVisible = false;
        PlaneReferenceSignedDistance = double.NaN;
        PlaneReferenceAbsoluteDistance = double.NaN;
        PlaneReferenceY = double.NaN;
        PlaneReferenceTargetY = double.NaN;
        PlaneReferenceRawHeightDelta = double.NaN;
        PlaneReferenceNormalX = double.NaN;
        PlaneReferenceNormalY = double.NaN;
        PlaneReferenceNormalZ = double.NaN;
        PlaneReferenceFitRms = double.NaN;
        PlaneReferenceSampleCount = 0;
        PlaneReferenceMeasurementSummary = "Plane reference: pending";
        PlaneReferenceMeasurementDetails = "Distance to reference plane: pending";
    }

}
