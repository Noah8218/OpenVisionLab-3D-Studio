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
    private bool TryPickNominalActualDeviation(
        Point screenPoint,
        out NominalActualDeviationSample hit)
    {
        hit = default;
        var comparison = viewModel.NominalActual;
        if (viewModel.SelectedSelectionMode != "Point"
            || !comparison.ActualVisible
            || comparison.PreviewResult is not { } result
            || Viewport.ActualWidth <= 0
            || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        var maximumDistance = Math.Max(0.035f, (float)viewModel.CameraDistance * 0.006f);
        var nearestDepth = float.PositiveInfinity;
        var nearestRayDistance = float.PositiveInfinity;
        foreach (var sample in result.DisplaySamples)
        {
            if (!ViewerRayGeometry.TryProjectPoint(
                    ray.origin,
                    ray.direction,
                    sample.Position,
                    out var alongRay,
                    out var rayDistance))
            {
                continue;
            }

            if (rayDistance > maximumDistance
                || alongRay > nearestDepth
                || (alongRay == nearestDepth && rayDistance >= nearestRayDistance))
            {
                continue;
            }

            nearestDepth = alongRay;
            nearestRayDistance = rayDistance;
            hit = sample;
        }

        return float.IsFinite(nearestDepth);
    }

    private bool TryPickCube(Point screenPoint, out Vector3 hit)
    {
        hit = default;

        if (!viewModel.CubeVisible || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        if (!CameraMath.IntersectUnitCube(ray.origin, ray.direction, 1.0f, out var distance))
        {
            return false;
        }

        hit = ray.origin + ray.direction * distance;
        return true;
    }

    private bool TryPickC3DPoint(Point screenPoint, out HeightGridPoint hit)
    {
        hit = default;

        if (!viewModel.C3DSampleVisible || c3dSample is null || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        var bestDistance = float.PositiveInfinity;
        var maxDistance = Math.Max(0.12f, (float)viewModel.CameraDistance * 0.025f);

        foreach (var point in c3dSample.Points)
        {
            var position = TransformC3DPosition(point.Position);
            if (!ViewerRayGeometry.TryProjectPoint(
                    ray.origin,
                    ray.direction,
                    position,
                    out _,
                    out var distance))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = point;
            }
        }

        return bestDistance <= maxDistance;
    }

    private bool TryPickImportedMesh(
        Point screenPoint,
        out Vector3 hit,
        out string hitKind,
        out int? triangleIndex,
        out Vector3? surfaceNormal)
    {
        hit = default;
        hitKind = "mesh point";
        triangleIndex = null;
        surfaceNormal = null;

        if (!viewModel.GlbSampleVisible || importedMesh is null || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        if (TryPickImportedMeshTriangle(ray.origin, ray.direction, out hit, out var pickedTriangleIndex, out var pickedSurfaceNormal))
        {
            hitKind = "mesh surface";
            triangleIndex = pickedTriangleIndex;
            surfaceNormal = pickedSurfaceNormal;
            return true;
        }

        if (TryPickImportedMeshNearestVertex(ray.origin, ray.direction, out hit))
        {
            hitKind = "mesh vertex fallback";
            return true;
        }

        return false;
    }

    private bool TryPickImportedMeshTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out Vector3 hit,
        out int? triangleIndex,
        out Vector3? surfaceNormal)
    {
        hit = default;
        triangleIndex = null;
        surfaceNormal = null;

        var mesh = importedMesh!;
        var bestDistance = float.PositiveInfinity;
        var bestTriangleIndex = -1;
        var bestNormal = Vector3.Zero;
        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3)
        {
            var firstIndex = mesh.Indices[i];
            var secondIndex = mesh.Indices[i + 1];
            var thirdIndex = mesh.Indices[i + 2];
            if (!ImportedMeshIndexInRange(mesh, firstIndex) || !ImportedMeshIndexInRange(mesh, secondIndex) || !ImportedMeshIndexInRange(mesh, thirdIndex))
            {
                continue;
            }

            var first = mesh.Positions[firstIndex];
            var second = mesh.Positions[secondIndex];
            var third = mesh.Positions[thirdIndex];
            if (!ViewerRayGeometry.TryIntersectTriangle(rayOrigin, rayDirection, first, second, third, out var distance, out var candidate))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = candidate;
                bestTriangleIndex = i / 3;
                bestNormal = ViewerRayGeometry.CalculateTriangleNormal(first, second, third);
            }
        }

        if (!float.IsFinite(bestDistance))
        {
            return false;
        }

        triangleIndex = bestTriangleIndex;
        surfaceNormal = bestNormal;
        return true;
    }

    private bool TryPickImportedMeshNearestVertex(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 hit)
    {
        hit = default;
        var bestDistance = float.PositiveInfinity;
        var maxDistance = Math.Max(0.02f, (float)viewModel.CameraDistance * 0.025f);
        var positions = importedMesh!.Positions;

        foreach (var position in positions)
        {
            if (!ViewerRayGeometry.TryProjectPoint(
                    rayOrigin,
                    rayDirection,
                    position,
                    out _,
                    out var distance))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = position;
            }
        }

        return bestDistance <= maxDistance;
    }

    private static bool ImportedMeshIndexInRange(ImportedMesh mesh, int index) =>
        (uint)index < (uint)mesh.Positions.Length;

    private float GetImportedMeshSurfaceOverlayScale()
    {
        if (importedMesh is null)
        {
            return 0.05f;
        }

        var diagonal = Vector3.Distance(importedMesh.Min, importedMesh.Max);
        return Math.Clamp(diagonal * 0.35f, 0.02f, 1.0f);
    }

    private bool TryPickLazPoint(Point screenPoint, out LazPointCloudPoint hit)
    {
        hit = default;

        if (!viewModel.LazSampleVisible || lazPointCloud is null || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        var bestDistance = float.PositiveInfinity;
        var maxDistance = Math.Max(1.0f, (float)viewModel.CameraDistance * 0.025f);

        foreach (var point in lazPointCloud.SampledPointView)
        {
            var position = MapLazPosition(point);
            if (!ViewerRayGeometry.TryProjectPoint(
                    ray.origin,
                    ray.direction,
                    position,
                    out _,
                    out var distance))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = point;
            }
        }

        return bestDistance <= maxDistance;
    }

    private bool TryHandleTwoPointPick(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != TwoPointSelectionMode)
        {
            return false;
        }

        if (viewModel.LazSampleVisible && lazPointCloud is not null)
        {
            return TryHandleLazTwoPointPick(screenPoint);
        }

        if (viewModel.GlbSampleVisible && importedMesh is not null)
        {
            return TryHandleGlbTwoPointPick(screenPoint);
        }

        if (!TryPickC3DPoint(screenPoint, out var point))
        {
            viewModel.SelectedEntity = "Two Point Measurement";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Two-point pick missed C3D height grid";
            return true;
        }

        if (twoPointFirst is null || twoPointSecond is not null)
        {
            twoPointFirst = point;
            twoPointSecond = null;
            viewModel.SetTwoPointMeasurementStart(TransformC3DPosition(point.Position), point.RawValue);
            viewModel.SetPointPairFirstReference(point.Row, point.Column);
        }
        else
        {
            SetTwoPointMeasurement(twoPointFirst.Value, point);
        }

        viewModel.SelectedEntity = "Two Point Measurement";
        viewModel.PickCoordinate = FormatC3DPoint(point);
        return true;
    }

    private bool TryHandleGlbTwoPointPick(Point screenPoint)
    {
        if (!TryPickImportedMesh(screenPoint, out var point, out var pickKind, out var triangleIndex, out var surfaceNormal))
        {
            viewModel.SelectedEntity = "GLB Two Point Measurement";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = $"Two-point pick missed {viewModel.ImportedMeshFormat} mesh";
            return true;
        }

        if (importedMeshTwoPointFirst is null || importedMeshTwoPointSecond is not null)
        {
            importedMeshTwoPointFirst = point;
            importedMeshTwoPointSecond = null;
            twoPointFirst = null;
            twoPointSecond = null;
            lazTwoPointFirst = null;
            lazTwoPointSecond = null;
            selectedImportedMeshPoint = point;
            selectedImportedMeshPickKind = pickKind;
            selectedImportedMeshTriangleIndex = triangleIndex;
            selectedImportedMeshSurfaceNormal = surfaceNormal;
            viewModel.SetTwoPointMeasurementStart(point, point.Y, "model-y");
        }
        else
        {
            SetImportedMeshTwoPointMeasurement(importedMeshTwoPointFirst.Value, point);
        }

        viewModel.SelectedEntity = "GLB Two Point Measurement";
        viewModel.PickCoordinate = FormatImportedMeshPoint(point, pickKind, surfaceNormal);
        return true;
    }

    private bool TryHandleLazTwoPointPick(Point screenPoint)
    {
        if (!TryPickLazPoint(screenPoint, out var point))
        {
            viewModel.SelectedEntity = "LAZ/LAS Two Point Measurement";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Two-point pick missed LAZ/LAS point cloud";
            return true;
        }

        if (lazTwoPointFirst is null || lazTwoPointSecond is not null)
        {
            lazTwoPointFirst = point;
            lazTwoPointSecond = null;
            twoPointFirst = null;
            twoPointSecond = null;
            selectedLazPoint = point;
            var position = MapLazPosition(point);
            viewModel.SetTwoPointMeasurementStart(position, position.Y, "source-z-units");
        }
        else
        {
            SetLazTwoPointMeasurement(lazTwoPointFirst.Value, point);
        }

        viewModel.SelectedEntity = "LAZ/LAS Two Point Measurement";
        viewModel.PickCoordinate = FormatLazPoint(point);
        return true;
    }

    private bool TryHandleRoiStepPick(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != RoiStepSelectionMode)
        {
            return false;
        }

        if (!TryPickC3DPoint(screenPoint, out var point))
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "ROI pick missed C3D height grid";
            return true;
        }

        roiEditingSession.Pick(TransformC3DPosition(point.Position));
        viewModel.SelectedEntity = "ROI Step Compare";
        viewModel.PickCoordinate = FormatC3DPoint(point);
        return true;
    }

    private bool TryHandleThicknessRoiPick(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != MainWindowViewModel.ThicknessRoiSelectionMode)
        {
            return false;
        }

        if (!TryPickC3DPoint(screenPoint, out var point) || c3dSample is null)
        {
            viewModel.SelectedEntity = "C3D Thickness ROI";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Thickness ROI pick missed C3D height grid";
            return true;
        }

        viewModel.SetThicknessRoiFromCenter(point.Row, point.Column, c3dSample.Height, c3dSample.Width);
        viewModel.PickCoordinate = FormatC3DPoint(point);
        return true;
    }

    private void SetTwoPointMeasurement(HeightGridPoint first, HeightGridPoint second, bool updatePointPairReferences = true)
    {
        ClearPlaneReferenceMeasurement();
        twoPointFirst = first;
        twoPointSecond = second;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedImportedMeshPoint = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        viewModel.SetTwoPointMeasurement(TransformC3DPosition(first.Position), first.RawValue, TransformC3DPosition(second.Position), second.RawValue);
        if (updatePointPairReferences)
        {
            viewModel.SetPointPairReferences(first.Row, first.Column, second.Row, second.Column);
        }
    }

    private void SetLazTwoPointMeasurement(LazPointCloudPoint first, LazPointCloudPoint second, string heightUnit = "source-z-units")
    {
        ClearPlaneReferenceMeasurement();
        lazTwoPointFirst = first;
        lazTwoPointSecond = second;
        selectedLazPoint = second;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedImportedMeshPoint = null;
        twoPointFirst = null;
        twoPointSecond = null;

        var firstPosition = MapLazPosition(first);
        var secondPosition = MapLazPosition(second);
        viewModel.SetTwoPointMeasurement(firstPosition, firstPosition.Y, secondPosition, secondPosition.Y, heightUnit);
        viewModel.SetLazTwoPointMeasurementPreview(firstPosition, secondPosition, secondPosition.Y - firstPosition.Y, heightUnit);
    }

    private void SetImportedMeshTwoPointMeasurement(Vector3 first, Vector3 second)
    {
        ClearPlaneReferenceMeasurement();
        importedMeshTwoPointFirst = first;
        importedMeshTwoPointSecond = second;
        selectedImportedMeshPoint = second;
        selectedImportedMeshPickKind = "mesh measurement point";
        selectedImportedMeshTriangleIndex = null;
        selectedImportedMeshSurfaceNormal = null;
        twoPointFirst = null;
        twoPointSecond = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        selectedLazPoint = null;
        viewModel.SetTwoPointMeasurement(first, first.Y, second, second.Y, "model-y");
        viewModel.SelectionSummary = $"GLB measurement: {viewModel.TwoPointMeasurementDetails}";
        viewModel.MeasurementSummary = $"GLB measurement: {viewModel.TwoPointMeasurementDetails}";
    }

    private void ClearPlaneReferenceMeasurement()
    {
        planeReferenceMeasurement = null;
        viewModel.ClearPlaneReferenceMeasurement();
    }

    private (Vector3 origin, Vector3 direction) CreatePickRay(Point screenPoint)
    {
        return viewModel.IsTopOrthographicView
            ? CameraMath.CreateOrthographicPickRay(
                screenPoint,
                Viewport.ActualWidth,
                Viewport.ActualHeight,
                viewModel.OrthographicHeight,
                GetCameraPosition(),
                GetCameraTarget())
            : CameraMath.CreatePickRay(
                screenPoint,
                Viewport.ActualWidth,
                Viewport.ActualHeight,
                FieldOfViewDegrees,
                GetCameraPosition(),
                GetCameraTarget());
    }

    private void PanCamera(System.Windows.Vector delta)
    {
        var target = GetCameraTarget();
        var eye = GetCameraPosition();
        var movement = CameraMath.PanDelta(
            delta,
            Viewport.ActualHeight,
            FieldOfViewDegrees,
            viewModel.CameraDistance,
            target,
            eye,
            viewModel.IsTopOrthographicView ? viewModel.OrthographicHeight : null);

        viewModel.Pan(movement.X, movement.Y, movement.Z);
    }

}
