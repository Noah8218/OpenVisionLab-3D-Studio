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

    public bool FitC3DReferencePlane()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Plane fit requires a visible C3D height grid";
            return false;
        }

        C3DHeightGrid fitSample;
        try
        {
            fitSample = C3DHeightGrid.Load(c3dSample.SourcePath, PlaneFitMaxSampledPoints);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Plane fit sample load failed: {ex.Message}";
            return false;
        }

        var transformed = fitSample.Points
            .Select(point => (Point: point, Position: TransformC3DPosition(point.Position)))
            .ToArray();
        HeightFieldPlaneFitResult result;
        try
        {
            result = HeightFieldPlaneFit.Fit(
                transformed
                    .Select(item => new HeightFieldPlaneSample(item.Position, item.Point.RawValue))
                    .ToArray());
        }
        catch (ArgumentException ex)
        {
            viewModel.ViewerStatus = $"Plane fit failed: {ex.Message}";
            return false;
        }

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
        viewModel.ClearTwoPointMeasurement();
        viewModel.ClearRoiStepMeasurement();
        viewModel.SelectedSelectionMode = "Plane Distance";
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;

        var bounds = (
            MinX: transformed.Min(item => item.Position.X),
            MaxX: transformed.Max(item => item.Position.X),
            MinZ: transformed.Min(item => item.Position.Z),
            MaxZ: transformed.Max(item => item.Position.Z));
        planeReferenceMeasurement = (
            CreatePlaneCorner(result, bounds.MinX, bounds.MinZ),
            CreatePlaneCorner(result, bounds.MaxX, bounds.MinZ),
            CreatePlaneCorner(result, bounds.MaxX, bounds.MaxZ),
            CreatePlaneCorner(result, bounds.MinX, bounds.MaxZ),
            result.Target,
            result.TargetProjection);
        var target = transformed.MinBy(item => Vector3.DistanceSquared(item.Position, result.Target));
        viewModel.SetPlaneReferenceMeasurement(result, "C3D least-squares height field / fixed sample");
        viewModel.SelectedEntity = "Plane Distance Measurement";
        viewModel.PickCoordinate = FormatC3DPoint(target.Point);
        viewModel.ViewerStatus = "Fitted C3D plane and maximum residual measured";
        RenderNow();
        return true;
    }

    public bool PreviewC3DPlaneFlatness()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Plane flatness requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreatePlaneFlatnessRecipeStep();
        C3DHeightGrid measurementSample;
        try
        {
            measurementSample = C3DHeightGrid.Load(c3dSample.SourcePath, step.MaxSampledPoints);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Plane flatness sample load failed: {ex.Message}";
            return false;
        }

        var measurementSamples = measurementSample.Points
            .Select(point => new HeightFieldPlaneSample(TransformC3DPosition(point.Position), point.RawValue))
            .ToArray();
        var referenceSamples = measurementSamples
            .Where(sample => Contains(step.ReferenceRegion, sample.Position))
            .ToArray();
        var evaluation = PlaneFlatnessRule.Evaluate(new PlaneFlatnessRuleInput(
            step.SourceEntityId,
            referenceSamples,
            measurementSamples,
            step.Tolerance,
            step.Unit));

        twoPointFirst = null;
        twoPointSecond = null;
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;
        viewModel.ClearTwoPointMeasurement();
        viewModel.ClearPlaneReferenceMeasurement();
        viewModel.ClearRoiStepMeasurement();
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
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

        viewModel.SetPlaneFlatnessPreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    public bool PreviewC3DThickness()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Thickness requires a visible C3D height grid";
            return false;
        }

        if (!viewModel.ThicknessConfigured)
        {
            viewModel.ViewerStatus = "Thickness requires one taught C3D grid ROI";
            return false;
        }

        var step = viewModel.CreateThicknessRecipeStep();
        C3DThicknessEvaluation evaluation;
        try
        {
            evaluation = C3DThicknessRule.Evaluate(new C3DThicknessInput(
                step.SourceEntityId,
                c3dSample.Height,
                c3dSample.Width,
                c3dSample.ReadHeightMapValues(),
                step.Roi,
                step.Acceptance,
                step.Unit,
                step.FrameId,
                step.MinimumValidSamples));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Thickness sample load failed: {ex.Message}";
            return false;
        }

        viewModel.SetThicknessPreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    public bool PreviewC3DPointPairDimensions()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Point pair dimensions require a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreatePointPairDimensionsRecipeStep();
        if (step is null)
        {
            viewModel.ViewerStatus = "Point pair dimensions require two selected C3D source cells";
            return false;
        }

        HeightGridPoint first;
        HeightGridPoint second;
        try
        {
            first = c3dSample.ReadPoint(step.First.Row, step.First.Column);
            second = c3dSample.ReadPoint(step.Second.Row, step.Second.Column);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException)
        {
            viewModel.ViewerStatus = $"Point pair dimensions failed: {ex.Message}";
            return false;
        }

        SetTwoPointMeasurement(first, second, updatePointPairReferences: false);
        var evaluation = PointPairDimensionsRule.Evaluate(new PointPairDimensionsInput(
            step.SourceEntityId,
            TransformC3DPosition(first.Position),
            TransformC3DPosition(second.Position),
            first.RawValue,
            second.RawValue,
            step.Acceptance,
            step.Unit,
            viewModel.RecipeSourceUnit));
        viewModel.SetPointPairDimensionsPreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    public bool PreviewC3DGapFlush()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Gap / Flush requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreateGapFlushRecipeStep();
        C3DHeightGrid measurementSample;
        try
        {
            measurementSample = C3DHeightGrid.Load(c3dSample.SourcePath, step.MaxSampledPoints);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Gap / Flush sample load failed: {ex.Message}";
            return false;
        }

        TryCalculateGapFlushStats(measurementSample.Points, step.LeftRegion, out var left);
        TryCalculateGapFlushStats(measurementSample.Points, step.RightRegion, out var right);
        var evaluation = GapFlushRule.Evaluate(new GapFlushInput(
            step.SourceEntityId,
            step.LeftRegion,
            step.RightRegion,
            left,
            right,
            step.Acceptance,
            step.GapUnit,
            step.FlushUnit));

        roiStepLeftRecipeRegion = step.LeftRegion;
        roiStepRightRecipeRegion = step.RightRegion;
        roiStepInteractiveSelection = false;
        roiStepNextPickSetsRight = false;
        roiStepLeftBounds = (
            (float)(step.LeftRegion.CenterX - step.LeftRegion.HalfWidth),
            (float)(step.LeftRegion.CenterX + step.LeftRegion.HalfWidth),
            (float)(step.LeftRegion.CenterZ - step.LeftRegion.HalfDepth),
            (float)(step.LeftRegion.CenterZ + step.LeftRegion.HalfDepth),
            (float)left.ModelYMean);
        roiStepRightBounds = (
            (float)(step.RightRegion.CenterX - step.RightRegion.HalfWidth),
            (float)(step.RightRegion.CenterX + step.RightRegion.HalfWidth),
            (float)(step.RightRegion.CenterZ - step.RightRegion.HalfDepth),
            (float)(step.RightRegion.CenterZ + step.RightRegion.HalfDepth),
            (float)right.ModelYMean);
        roiStepLeftCenter = new Vector3((float)step.LeftRegion.CenterX, (float)left.ModelYMean, (float)step.LeftRegion.CenterZ);
        roiStepRightCenter = new Vector3((float)step.RightRegion.CenterX, (float)right.ModelYMean, (float)step.RightRegion.CenterZ);
        viewModel.SetRoiStepMeasurement(
            left.PointCount,
            left.RawMean,
            left.ModelYMean,
            right.PointCount,
            right.RawMean,
            right.ModelYMean,
            "GapFlush");
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        viewModel.SetGapFlushPreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    private bool TryCalculateGapFlushStats(
        IReadOnlyList<HeightGridPoint> points,
        HeightDeviationRecipeRoiRegion region,
        out GapFlushRegionStats stats)
    {
        var count = 0;
        var rawSum = 0.0;
        var modelYSum = 0.0;
        foreach (var point in points)
        {
            var position = TransformC3DPosition(point.Position);
            if (!Contains(region, position))
            {
                continue;
            }

            count++;
            rawSum += point.RawValue;
            modelYSum += position.Y;
        }

        if (count == 0)
        {
            stats = new GapFlushRegionStats(0, double.NaN, double.NaN);
            return false;
        }

        stats = new GapFlushRegionStats(count, rawSum / count, modelYSum / count);
        return true;
    }

    public bool PreviewC3DVolume()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Volume requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreateVolumeRecipeStep();
        C3DHeightGrid measurementGrid;
        try { measurementGrid = C3DHeightGrid.Load(c3dSample.SourcePath, step.MaxSampledPoints); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Volume sample load failed: {ex.Message}";
            return false;
        }

        var samples = measurementGrid.Points
            .Select(point => new HeightFieldPlaneSample(TransformC3DPosition(point.Position), point.RawValue))
            .ToArray();
        var reference = samples.Where(sample => Contains(step.ReferenceRegion, sample.Position)).ToArray();
        var measured = samples.Where(sample => Contains(step.MeasurementRegion, sample.Position)).ToArray();
        var spacing = measurementGrid.HorizontalScale * measurementGrid.PointStride * viewModel.C3DModelTransform.Scale;
        var evaluation = VolumeRule.Evaluate(new VolumeRuleInput(
            step.SourceEntityId, reference, measured, spacing * spacing,
            step.ExpectedNetVolume, step.Tolerance, step.Unit));

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

        var meanY = measured.Length == 0 ? 0.0 : measured.Average(sample => sample.Position.Y);
        roiStepLeftBounds = (
            (float)(step.MeasurementRegion.CenterX - step.MeasurementRegion.HalfWidth),
            (float)(step.MeasurementRegion.CenterX + step.MeasurementRegion.HalfWidth),
            (float)(step.MeasurementRegion.CenterZ - step.MeasurementRegion.HalfDepth),
            (float)(step.MeasurementRegion.CenterZ + step.MeasurementRegion.HalfDepth),
            (float)meanY);
        roiStepRightBounds = null;
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        viewModel.SetVolumePreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    public bool PreviewC3DCrossSection()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Cross-section Dimensions requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreateCrossSectionRecipeStep();
        HeightGridPoint[] sourcePoints;
        try
        {
            sourcePoints = c3dSample.ReadRowRange(step.Row, step.StartColumn, step.EndColumn);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            viewModel.ViewerStatus = $"Cross-section source read failed: {ex.Message}";
            return false;
        }

        var samples = sourcePoints
            .Select(point => new CrossSectionSample(point.Column, TransformC3DPosition(point.Position), point.RawValue))
            .ToArray();
        var evaluation = CrossSectionDimensionsRule.Evaluate(new CrossSectionDimensionsInput(
            step.SourceEntityId,
            step.Row,
            step.StartColumn,
            step.EndColumn,
            samples,
            step.ExpectedWidth,
            step.WidthTolerance,
            step.ExpectedHeightRange,
            step.HeightTolerance,
            step.WidthUnit,
            step.HeightUnit));

        if (sourcePoints.Length >= 2)
        {
            var minimum = sourcePoints.Min(point => point.RawValue);
            var maximum = sourcePoints.Max(point => point.RawValue);
            var mean = sourcePoints.Average(point => point.RawValue);
            viewModel.SetSectionProfile(
                viewModel.RecipeSourceName,
                step.Row,
                sourcePoints.Length,
                minimum,
                maximum,
                mean,
                BuildSectionProfilePath(sourcePoints, minimum, maximum));
        }

        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        viewModel.SetCrossSectionPreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    private static bool Contains(HeightDeviationRecipeRoiRegion region, Vector3 point) =>
        point.X >= region.CenterX - region.HalfWidth
        && point.X <= region.CenterX + region.HalfWidth
        && point.Z >= region.CenterZ - region.HalfDepth
        && point.Z <= region.CenterZ + region.HalfDepth;

    private static Vector3 CreatePlaneCorner(HeightFieldPlaneFitResult result, float x, float z) =>
        new(x, (float)result.EvaluateY(x, z), z);

}
