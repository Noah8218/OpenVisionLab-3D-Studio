using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Recipes;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private void ApplySmokeC3D()
    {
        if (c3dSample is null)
        {
            SetSmokeFailure("Smoke C3D failed: sample missing or unsupported");
            return;
        }

        viewModel.UseC3DSmokeScene();
    }

    private void ApplySmokeGlb(string? path)
    {
        selectedImportedMeshPoint = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            importedMesh = LoadGlbSample(path);
            if (importedMesh is not null)
            {
                SetGlbSampleStatus();
            }
        }

        if (importedMesh is null)
        {
            viewModel.UseGlbFailureScene(viewModel.GlbSampleSummary);
            SetSmokeFailure(CreateSmokeFailureMessage("Smoke GLB failed", viewModel.GlbSampleSummary));
            return;
        }

        viewModel.UseGlbSmokeScene();
    }

    private void ApplySmokeStl(string? path)
    {
        selectedImportedMeshPoint = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            importedMesh = null;
            viewModel.SetGlbSampleSource("(none)", "STL Mesh", "STL");
            viewModel.GlbSampleTriangleCount = "(missing)";
            viewModel.GlbSampleSummary = "Missing STL sample path";
        }
        else
        {
            importedMesh = LoadStlSample(path);
            if (importedMesh is not null)
            {
                SetGlbSampleStatus();
            }
        }

        if (importedMesh is null)
        {
            viewModel.UseGlbFailureScene(viewModel.GlbSampleSummary);
            SetSmokeFailure(CreateSmokeFailureMessage("Smoke STL failed", viewModel.GlbSampleSummary));
            return;
        }

        viewModel.UseGlbSmokeScene();
    }

    private void ApplySmokeLaz(string? path)
    {
        selectedImportedMeshPoint = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedLazPoint = null;
        twoPointFirst = null;
        twoPointSecond = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        lazPointCloud = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            lazSample = LoadLazSample(path);
            if (lazSample is not null)
            {
                SetLazSampleStatus();
            }
        }

        if (lazSample is null)
        {
            viewModel.UseLazFailureScene(viewModel.LazSampleSummary);
            SetSmokeFailure(CreateSmokeFailureMessage("Smoke LAZ failed", viewModel.LazSampleSummary));
            return;
        }

        viewModel.UseLazSmokeScene();
    }

    private void ApplySmokeLazPoints(string? path)
    {
        selectedImportedMeshPoint = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedLazPoint = null;
        twoPointFirst = null;
        twoPointSecond = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            lazPointCloud = LoadLazPointCloud(path);
            lazSample = lazPointCloud?.Metadata;
            if (lazPointCloud is not null && lazSample is not null)
            {
                SetLazSampleStatus();
            }
        }
        else if (lazPointCloud is null && lazSample is not null)
        {
            lazPointCloud = LoadLazPointCloud(lazSample.SourcePath);
            lazSample = lazPointCloud?.Metadata ?? lazSample;
            if (lazPointCloud is not null)
            {
                SetLazSampleStatus();
            }
        }

        if (lazPointCloud is null || lazSample is null)
        {
            viewModel.UseLazFailureScene(viewModel.LazSampleSummary);
            SetSmokeFailure(CreateSmokeFailureMessage("Smoke LAZ/LAS points failed", viewModel.LazSampleSummary));
            return;
        }

        viewModel.UseLazPointSmokeScene();
    }

    private void SetSmokeFailure(string message)
    {
        smokeExitCode = 1;
        viewModel.ViewerStatus = message;
    }

    private static string CreateSmokeFailureMessage(string prefix, string detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? $"{prefix}: sample missing or unsupported"
            : $"{prefix}: {detail}";

    private void ApplySmokePickLaz()
    {
        if (lazPointCloud is null)
        {
            ApplySmokeLazPoints(null);
        }

        if (lazPointCloud is null || lazPointCloud.SampledPoints.Length == 0)
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Smoke pick failed: LAZ/LAS point cloud missing";
            return;
        }

        viewModel.UseLazPointSmokeScene();
        var target = FindLazSmokePickTarget();
        var viewerPosition = MapLazPosition(target.Position);
        viewModel.CameraTargetX = viewerPosition.X;
        viewModel.CameraTargetY = viewerPosition.Y;
        viewModel.CameraTargetZ = viewerPosition.Z;
        viewModel.UpdateCameraStatus();

        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickLazPoint(center, out var point))
        {
            SetLazPick(point, "Smoke pick: LAZ/LAS sampled point");
        }
        else
        {
            SetLazPick(target, "Smoke pick: LAZ/LAS sampled point fallback");
        }
    }

    private void ApplySmokePickGlb()
    {
        if (importedMesh is null)
        {
            ApplySmokeGlb(null);
        }

        if (importedMesh is null || importedMesh.Positions.Length == 0)
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            SetSmokeFailure($"Smoke pick failed: {viewModel.ImportedMeshFormat} mesh missing");
            return;
        }

        viewModel.UseGlbSmokeScene();
        var target = FindImportedMeshSmokeSurfacePickTarget();
        viewModel.CameraTargetX = target.X;
        viewModel.CameraTargetY = target.Y;
        viewModel.CameraTargetZ = target.Z;
        viewModel.UpdateCameraStatus();

        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickImportedMesh(center, out var point, out var pickKind, out var triangleIndex, out var surfaceNormal))
        {
            SetImportedMeshPick(point, $"Smoke pick: {viewModel.ImportedMeshFormat} {pickKind}", pickKind, triangleIndex, surfaceNormal);
        }
        else
        {
            SetImportedMeshPick(target, $"Smoke pick: {viewModel.ImportedMeshFormat} mesh point fallback", "mesh point fallback");
        }
    }

    private void ApplySmokePickNominalActual()
    {
        var comparison = viewModel.NominalActual;
        if (!comparison.ActualVisible || comparison.PreviewResult is null)
        {
            SetSmokeFailure("Smoke pick failed: nominal/actual Preview result is unavailable");
            return;
        }

        viewModel.SelectedSelectionMode = "Point";
        var center = new Point(
            Math.Max(1.0, Viewport.ActualWidth) / 2.0,
            Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickNominalActualDeviation(center, out var sample))
        {
            SetNominalActualDeviationPick(sample, "Smoke pick: nominal/actual deviation point");
            return;
        }

        SetSmokeFailure("Smoke pick failed: no rendered nominal/actual point under the viewport center");
    }

    private void ApplySmokePickCube()
    {
        viewModel.Reset();
        viewModel.CubeVisible = true;
        viewModel.PointCloudVisible = false;
        viewModel.SelectionOverlayVisible = false;
        viewModel.ResultOverlayVisible = false;
        viewModel.MeasurementVisible = true;
        viewModel.SelectedEntity = "Generated Unit Cube";
        viewModel.FitSelection();

        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickCube(center, out var hit))
        {
            var summary = CameraMath.FormatPoint(hit);
            viewModel.SelectedEntity = "Generated Unit Cube";
            viewModel.PickCoordinate = summary;
            viewModel.SelectionSummary = $"Cube pick: {summary}";
            viewModel.ViewerStatus = "Smoke pick: generated cube";
        }
        else
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Smoke pick failed";
        }
    }

    private void ApplySmokePickC3D()
    {
        if (c3dSample is null)
        {
            viewModel.ViewerStatus = "Smoke pick failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickC3DPoint(center, out var point))
        {
            viewModel.SelectedEntity = "C3D Height Grid";
            viewModel.PickCoordinate = FormatC3DPoint(point);
            viewModel.ViewerStatus = "Smoke pick: C3D height grid";
        }
        else
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Smoke pick failed: C3D height grid";
        }
    }

    private void ApplySmokeTwoPointMeasurement()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        viewModel.SelectedSelectionMode = TwoPointSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        var first = c3dSample.Points.MinBy(point => point.RawValue);
        var second = c3dSample.Points.MaxBy(point => point.RawValue);
        SetTwoPointMeasurement(first, second);
        viewModel.SelectedEntity = "Two Point Measurement";
        viewModel.PickCoordinate = FormatC3DPoint(second);
        viewModel.ViewerStatus = "Smoke measure: two-point distance and height delta";
    }

    private void ApplySmokePointPairDimensions()
    {
        viewModel.UseC3DSmokeScene();
        ApplySmokeTwoPointMeasurement();
        if (twoPointFirst is null || twoPointSecond is null)
        {
            SetSmokeFailure("Smoke dimensions failed: C3D point pair missing");
            return;
        }

        var delta = TransformC3DPosition(twoPointSecond.Value.Position)
            - TransformC3DPosition(twoPointFirst.Value.Position);
        var width = Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        viewModel.PointPairExpectedDistance = delta.Length();
        viewModel.PointPairDistanceTolerance = 0.001;
        viewModel.PointPairExpectedWidth = width;
        viewModel.PointPairWidthTolerance = 0.001;
        viewModel.PointPairExpectedAngleDegrees = Math.Atan2(delta.Y, width) * 180.0 / Math.PI;
        viewModel.PointPairAngleToleranceDegrees = 0.01;
        if (PreviewC3DPointPairDimensions())
        {
            viewModel.ViewerStatus = "Smoke measure: C3D point pair width, distance, and angle";
        }
        else
        {
            smokeExitCode = 1;
        }
    }

    private void ApplySmokeLazTwoPointMeasurement(string heightUnit = "source-z-units")
    {
        if (lazPointCloud is null)
        {
            ApplySmokeLazPoints(null);
        }

        if (lazPointCloud is null || lazPointCloud.SampledPoints.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: LAZ/LAS point cloud missing";
            return;
        }

        viewModel.UseLazPointSmokeScene();
        viewModel.SelectedSelectionMode = TwoPointSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;

        var first = lazPointCloud.SampledPoints.MinBy(point => MapLazPosition(point.Position).X);
        var second = lazPointCloud.SampledPoints.MaxBy(point => MapLazPosition(point.Position).X);
        SetLazTwoPointMeasurement(first, second, heightUnit);

        var firstPosition = MapLazPosition(first.Position);
        var secondPosition = MapLazPosition(second.Position);
        var midpoint = (firstPosition + secondPosition) * 0.5f;
        viewModel.CameraTargetX = midpoint.X;
        viewModel.CameraTargetY = midpoint.Y;
        viewModel.CameraTargetZ = midpoint.Z;
        viewModel.UpdateCameraStatus();
        viewModel.SelectedEntity = "LAZ/LAS Two Point Measurement";
        viewModel.PickCoordinate = FormatLazPoint(second);
        viewModel.ViewerStatus = "Smoke measure: LAZ/LAS two-point distance and height delta";
    }

    private void ApplySmokeImportedMeshTwoPointMeasurement()
    {
        if (importedMesh is null)
        {
            ApplySmokeGlb(null);
        }

        if (importedMesh is null || importedMesh.Positions.Length < 2)
        {
            viewModel.SelectedEntity = $"{viewModel.ImportedMeshFormat} Two Point Measurement";
            viewModel.PickCoordinate = "(none)";
            SetSmokeFailure($"Smoke measure failed: {viewModel.ImportedMeshFormat} mesh missing");
            return;
        }

        viewModel.UseGlbSmokeScene();
        viewModel.SelectedSelectionMode = TwoPointSelectionMode;
        viewModel.MeasurementVisible = true;
        viewModel.SelectionOverlayVisible = true;

        var (first, second) = FindImportedMeshSmokeMeasurementPair();

        SetImportedMeshTwoPointMeasurement(first, second);
        viewModel.SelectedEntity = $"{viewModel.ImportedMeshFormat} Two Point Measurement";
        viewModel.PickCoordinate = FormatImportedMeshPoint(second);
        viewModel.ViewerStatus = $"Smoke measure: {viewModel.ImportedMeshFormat} two-point distance";
    }

    private void ApplySmokeRoiStepMeasurement()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        roiStepInteractiveSelection = false;
        roiStepLeftAnchor = null;
        roiStepRightAnchor = null;
        ClearRecipeRoiStep();
        roiStepNextPickSetsRight = false;

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Smoke measure: ROI step-height comparison";
        }
    }

    private void ApplySmokeInteractiveRoiStepMeasurement()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        roiStepInteractiveSelection = true;
        roiStepNextPickSetsRight = false;
        ClearRecipeRoiStep();

        var bounds = GetTransformedC3DBounds();
        var centerZ = (bounds.MinZ + bounds.MaxZ) * 0.5f;
        roiStepLeftAnchor = new Vector3(bounds.MinX + (bounds.MaxX - bounds.MinX) * 0.30f, 0.0f, centerZ);
        roiStepRightAnchor = new Vector3(bounds.MinX + (bounds.MaxX - bounds.MinX) * 0.70f, 0.0f, centerZ);

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Smoke measure: interactive ROI step-height comparison";
        }
    }

    private void ApplySmokePlaneReferenceMeasurement()
    {
        viewModel.UseC3DSmokeScene();
        if (FitC3DReferencePlane())
        {
            viewModel.ViewerStatus = "Smoke measure: distance to fitted C3D plane";
        }
    }

    private void ApplySmokePlaneFlatness()
    {
        viewModel.UseC3DSmokeScene();
        if (PreviewC3DPlaneFlatness())
        {
            viewModel.ViewerStatus = "Smoke measure: reference ROI plane flatness";
        }
    }

    private void ApplySmokeGapFlush()
    {
        if (!viewModel.C3DSampleVisible)
        {
            ApplySmokeC3D();
        }

        if (!PreviewC3DGapFlush())
        {
            SetSmokeFailure("Smoke Gap / Flush preview failed");
        }
    }

    private void ApplySmokeVolume()
    {
        if (!viewModel.C3DSampleVisible) ApplySmokeC3D();
        if (!PreviewC3DVolume()) SetSmokeFailure("Smoke Volume preview failed");
    }

    private void ApplySmokeCrossSection()
    {
        if (!viewModel.C3DSampleVisible) ApplySmokeC3D();
        if (!PreviewC3DCrossSection()) SetSmokeFailure("Smoke Cross-section Dimensions preview failed");
    }

    public bool FitC3DReferencePlane() =>
        C3DReferencePlaneFitCoordinator.Fit(
            c3dSample,
            viewModel,
            ClearC3DReferencePlaneFitViewState,
            ApplyC3DReferencePlaneFitOverlay,
            RenderNow);

    private void ClearC3DReferencePlaneFitViewState()
    {
        planeReferenceMeasurement = null;
        twoPointFirst = null;
        twoPointSecond = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedImportedMeshPoint = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        selectedLazPoint = null;
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;
    }

    private void ApplyC3DReferencePlaneFitOverlay(
        HeightFieldPlaneFitResult result,
        HeightGridPoint targetPoint,
        (float MinX, float MaxX, float MinZ, float MaxZ) bounds)
    {
        planeReferenceMeasurement = (
            CreatePlaneCorner(result, bounds.MinX, bounds.MinZ),
            CreatePlaneCorner(result, bounds.MaxX, bounds.MinZ),
            CreatePlaneCorner(result, bounds.MaxX, bounds.MaxZ),
            CreatePlaneCorner(result, bounds.MinX, bounds.MaxZ),
            result.Target,
            result.TargetProjection);
        viewModel.PickCoordinate = FormatC3DPoint(targetPoint);
    }

    public bool PreviewC3DPlaneFlatness() =>
        C3DPlaneFlatnessRuleCoordinator.Preview(
            c3dSample,
            viewModel,
            ApplyC3DPlaneFlatnessPreviewOverlay,
            RenderNow);

    private void ApplyC3DPlaneFlatnessPreviewOverlay(
        HeightDeviationRecipePlaneFlatness step,
        PlaneFlatnessEvaluation evaluation)
    {
        twoPointFirst = null;
        twoPointSecond = null;
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;
        planeFlatnessEvaluation = evaluation;

        if (evaluation.ReferencePlane is { } plane)
        {
            var region = step.ReferenceRegion;
            planeReferenceMeasurement = (
                CreatePlaneCorner(plane, (float)(region.CenterX - region.HalfWidth), (float)(region.CenterZ - region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX + region.HalfWidth), (float)(region.CenterZ - region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX + region.HalfWidth), (float)(region.CenterZ + region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX - region.HalfWidth), (float)(region.CenterZ + region.HalfDepth)),
                evaluation.MaximumPoint,
                evaluation.MaximumProjection);
            viewModel.PickCoordinate = string.Create(
                CultureInfo.InvariantCulture,
                $"Maximum deviation point {CameraMath.FormatPoint(evaluation.MaximumPoint)}");
        }
        else
        {
            planeReferenceMeasurement = null;
            viewModel.PickCoordinate = "(invalid reference ROI)";
        }
    }

    public bool PreviewC3DThickness()
        => C3DThicknessRuleCoordinator.Preview(c3dSample, viewModel, RenderNow);

    public bool PreviewC3DWarpage()
        => C3DWarpageRuleCoordinator.Preview(c3dSample, viewModel, RenderNow);

    public bool PreviewC3DPointPairDimensions()
        => C3DPointPairDimensionsRuleCoordinator.Preview(
            c3dSample,
            viewModel,
            (first, second) => SetTwoPointMeasurement(first, second, updatePointPairReferences: false),
            RenderNow);

    public bool PreviewC3DVolume() =>
        C3DVolumeRuleCoordinator.Preview(
            c3dSample,
            viewModel,
            ApplyC3DVolumePreviewOverlay,
            RenderNow);

    private void ApplyC3DVolumePreviewOverlay(
        HeightDeviationRecipeVolume step,
        VolumeEvaluation evaluation,
        double meanY)
    {
        if (evaluation.ReferencePlane is { } plane)
        {
            var region = step.ReferenceRegion;
            planeReferenceMeasurement = (
                CreatePlaneCorner(plane, (float)(region.CenterX - region.HalfWidth), (float)(region.CenterZ - region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX + region.HalfWidth), (float)(region.CenterZ - region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX + region.HalfWidth), (float)(region.CenterZ + region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX - region.HalfWidth), (float)(region.CenterZ + region.HalfDepth)),
                plane.Target,
                plane.TargetProjection);
        }

        roiStepLeftBounds = (
            (float)(step.MeasurementRegion.CenterX - step.MeasurementRegion.HalfWidth),
            (float)(step.MeasurementRegion.CenterX + step.MeasurementRegion.HalfWidth),
            (float)(step.MeasurementRegion.CenterZ - step.MeasurementRegion.HalfDepth),
            (float)(step.MeasurementRegion.CenterZ + step.MeasurementRegion.HalfDepth),
            (float)meanY);
        roiStepRightBounds = null;
    }

    public bool PreviewC3DCrossSection() =>
        C3DCrossSectionRuleCoordinator.Preview(
            c3dSample,
            viewModel,
            ApplyC3DCrossSectionProfile,
            RenderNow);

    private void ApplyC3DCrossSectionProfile(
        HeightDeviationRecipeCrossSection step,
        HeightGridPoint[] sourcePoints,
        double minimum,
        double maximum,
        double mean)
    {
        viewModel.SetSectionProfile(
            viewModel.RecipeSourceName,
            step.Row,
            sourcePoints.Length,
            minimum,
            maximum,
            mean,
            BuildSectionProfilePath(sourcePoints, minimum, maximum));
    }

    private static bool Contains(HeightDeviationRecipeRoiRegion region, Vector3 point) =>
        point.X >= region.CenterX - region.HalfWidth
        && point.X <= region.CenterX + region.HalfWidth
        && point.Z >= region.CenterZ - region.HalfDepth
        && point.Z <= region.CenterZ + region.HalfDepth;

    private static Vector3 CreatePlaneCorner(HeightFieldPlaneFitResult result, float x, float z) =>
        new(x, (float)result.EvaluateY(x, z), z);

}
