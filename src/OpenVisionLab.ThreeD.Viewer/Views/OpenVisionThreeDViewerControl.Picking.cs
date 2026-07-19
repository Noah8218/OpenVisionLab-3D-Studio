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
            var toPoint = sample.Position - ray.origin;
            var alongRay = Vector3.Dot(toPoint, ray.direction);
            if (alongRay < 0.0f)
            {
                continue;
            }

            var closestOnRay = ray.origin + ray.direction * alongRay;
            var rayDistance = Vector3.Distance(sample.Position, closestOnRay);
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
            var toPoint = position - ray.origin;
            var alongRay = Vector3.Dot(toPoint, ray.direction);
            if (alongRay < 0)
            {
                continue;
            }

            var closest = ray.origin + ray.direction * alongRay;
            var distance = Vector3.Distance(position, closest);
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
            if (!TryIntersectRayTriangle(rayOrigin, rayDirection, first, second, third, out var distance, out var candidate))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = candidate;
                bestTriangleIndex = i / 3;
                bestNormal = CalculateTriangleNormal(first, second, third);
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
            var toPoint = position - rayOrigin;
            var alongRay = Vector3.Dot(toPoint, rayDirection);
            if (alongRay < 0)
            {
                continue;
            }

            var closest = rayOrigin + rayDirection * alongRay;
            var distance = Vector3.Distance(position, closest);
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

    private static Vector3 CalculateTriangleNormal(Vector3 first, Vector3 second, Vector3 third)
    {
        var normal = Vector3.Cross(second - first, third - first);
        return normal.LengthSquared() <= 0.000000000001f
            ? Vector3.Zero
            : Vector3.Normalize(normal);
    }

    private float GetImportedMeshSurfaceOverlayScale()
    {
        if (importedMesh is null)
        {
            return 0.05f;
        }

        var diagonal = Vector3.Distance(importedMesh.Min, importedMesh.Max);
        return Math.Clamp(diagonal * 0.35f, 0.02f, 1.0f);
    }

    private static bool TryIntersectRayTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 first,
        Vector3 second,
        Vector3 third,
        out float distance,
        out Vector3 hit)
    {
        const float Epsilon = 0.0000001f;

        distance = 0.0f;
        hit = default;

        var edge1 = second - first;
        var edge2 = third - first;
        var p = Vector3.Cross(rayDirection, edge2);
        var determinant = Vector3.Dot(edge1, p);
        if (Math.Abs(determinant) < Epsilon)
        {
            return false;
        }

        var inverseDeterminant = 1.0f / determinant;
        var t = rayOrigin - first;
        var u = Vector3.Dot(t, p) * inverseDeterminant;
        if (u < -Epsilon || u > 1.0f + Epsilon)
        {
            return false;
        }

        var q = Vector3.Cross(t, edge1);
        var v = Vector3.Dot(rayDirection, q) * inverseDeterminant;
        if (v < -Epsilon || u + v > 1.0f + Epsilon)
        {
            return false;
        }

        distance = Vector3.Dot(edge2, q) * inverseDeterminant;
        if (distance < 0.0f)
        {
            return false;
        }

        hit = rayOrigin + rayDirection * distance;
        return true;
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

        foreach (var point in lazPointCloud.SampledPoints)
        {
            var position = MapLazPosition(point.Position);
            var toPoint = position - ray.origin;
            var alongRay = Vector3.Dot(toPoint, ray.direction);
            if (alongRay < 0)
            {
                continue;
            }

            var closest = ray.origin + ray.direction * alongRay;
            var distance = Vector3.Distance(position, closest);
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
        viewModel.PickCoordinate = FormatImportedMeshPoint(point, pickKind);
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
            var position = MapLazPosition(point.Position);
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

        var anchor = TransformC3DPosition(point.Position);
        roiStepInteractiveSelection = true;
        ClearRecipeRoiStep();
        if (!roiStepNextPickSetsRight || roiStepLeftAnchor is null || roiStepRightAnchor is not null)
        {
            roiStepLeftAnchor = anchor;
            roiStepRightAnchor = null;
            roiStepNextPickSetsRight = true;
        }
        else
        {
            roiStepRightAnchor = anchor;
            roiStepNextPickSetsRight = false;
        }

        UpdateRoiStepMeasurement();
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

        var firstPosition = MapLazPosition(first.Position);
        var secondPosition = MapLazPosition(second.Position);
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

    private bool UpdateRoiStepMeasurement()
    {
        ClearPlaneReferenceMeasurement();
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;

        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ClearRoiStepMeasurement("ROI step requires a visible C3D height grid.");
            viewModel.SelectedEntity = "ROI Step Compare";
            return false;
        }

        var bounds = GetTransformedC3DBounds();
        var width = Math.Max(0.001f, bounds.MaxX - bounds.MinX);
        var depth = Math.Max(0.001f, bounds.MaxZ - bounds.MinZ);
        var halfWidth = width * 0.15f;
        var halfDepth = depth * 0.25f;
        var zMin = bounds.MinZ + depth * 0.25f;
        var zMax = bounds.MinZ + depth * 0.75f;
        var leftBounds = roiStepLeftRecipeRegion is { } leftRegion
            ? CreateRoiBounds(leftRegion, bounds)
            : roiStepInteractiveSelection && roiStepLeftAnchor is { } leftAnchor
                ? CreateRoiBounds(leftAnchor, halfWidth, halfDepth, bounds)
                : (MinX: bounds.MinX + width * 0.10f, MaxX: bounds.MinX + width * 0.40f, MinZ: zMin, MaxZ: zMax, MeanY: 0.0f);
        var rightBounds = roiStepRightRecipeRegion is { } rightRegion
            ? CreateRoiBounds(rightRegion, bounds)
            : roiStepInteractiveSelection && roiStepRightAnchor is { } rightAnchor
                ? CreateRoiBounds(rightAnchor, halfWidth, halfDepth, bounds)
                : (MinX: bounds.MinX + width * 0.60f, MaxX: bounds.MinX + width * 0.90f, MinZ: zMin, MaxZ: zMax, MeanY: 0.0f);

        if (!TryCalculateRoiStats(leftBounds, out var left))
        {
            viewModel.ClearRoiStepMeasurement("ROI step found no C3D points in the left region.");
            viewModel.SelectedEntity = "ROI Step Compare";
            return false;
        }

        roiStepLeftBounds = (leftBounds.MinX, leftBounds.MaxX, leftBounds.MinZ, leftBounds.MaxZ, (float)left.ModelYMean);
        roiStepLeftCenter = left.Center;

        if (roiStepInteractiveSelection && roiStepRightAnchor is null)
        {
            viewModel.SetRoiStepSelectionPending(
                string.Create(CultureInfo.InvariantCulture, $"ROI step: L {left.Count:N0} pts, pick R"),
                string.Create(CultureInfo.InvariantCulture, $"Left mean raw {left.RawMean:F3}; click right ROI center."),
                "Interactive");
            viewModel.SelectedEntity = "ROI Step Compare";
            return true;
        }

        if (!TryCalculateRoiStats(rightBounds, out var right))
        {
            viewModel.ClearRoiStepMeasurement("ROI step found no C3D points in the right region.");
            viewModel.SelectedEntity = "ROI Step Compare";
            return false;
        }

        roiStepRightBounds = (rightBounds.MinX, rightBounds.MaxX, rightBounds.MinZ, rightBounds.MaxZ, (float)right.ModelYMean);
        roiStepRightCenter = right.Center;

        viewModel.SetRoiStepMeasurement(
            left.Count,
            left.RawMean,
            left.ModelYMean,
            right.Count,
            right.RawMean,
            right.ModelYMean,
            roiStepInteractiveSelection ? "Interactive" : "Auto");
        SyncRecipeRoiEditFromBounds(roiStepInteractiveSelection ? "Interactive" : "Auto", leftBounds, rightBounds);
        viewModel.SelectedEntity = "ROI Step Compare";
        viewModel.PickCoordinate = string.Create(
            CultureInfo.InvariantCulture,
            $"ROI centers: L {CameraMath.FormatPoint(left.Center)} | R {CameraMath.FormatPoint(right.Center)}");
        return true;
    }

    private (float MinX, float MaxX, float MinZ, float MaxZ) GetTransformedC3DBounds()
    {
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var minZ = float.PositiveInfinity;
        var maxZ = float.NegativeInfinity;

        foreach (var point in c3dSample!.Points)
        {
            var position = TransformC3DPosition(point.Position);
            minX = Math.Min(minX, position.X);
            maxX = Math.Max(maxX, position.X);
            minZ = Math.Min(minZ, position.Z);
            maxZ = Math.Max(maxZ, position.Z);
        }

        return (minX, maxX, minZ, maxZ);
    }

    private static (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) CreateRoiBounds(
        Vector3 center,
        float halfWidth,
        float halfDepth,
        (float MinX, float MaxX, float MinZ, float MaxZ) sceneBounds) =>
        (
            Math.Clamp(center.X - halfWidth, sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp(center.X + halfWidth, sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp(center.Z - halfDepth, sceneBounds.MinZ, sceneBounds.MaxZ),
            Math.Clamp(center.Z + halfDepth, sceneBounds.MinZ, sceneBounds.MaxZ),
            center.Y);

    private static (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) CreateRoiBounds(
        HeightDeviationRecipeRoiRegion region,
        (float MinX, float MaxX, float MinZ, float MaxZ) sceneBounds) =>
        (
            Math.Clamp((float)(region.CenterX - region.HalfWidth), sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp((float)(region.CenterX + region.HalfWidth), sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp((float)(region.CenterZ - region.HalfDepth), sceneBounds.MinZ, sceneBounds.MaxZ),
            Math.Clamp((float)(region.CenterZ + region.HalfDepth), sceneBounds.MinZ, sceneBounds.MaxZ),
            0.0f);

    private bool TryCalculateRoiStats(
        (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) bounds,
        out (int Count, double RawMean, double ModelYMean, Vector3 Center) stats)
    {
        var count = 0;
        var rawSum = 0.0;
        var xSum = 0.0;
        var ySum = 0.0;
        var zSum = 0.0;

        foreach (var point in c3dSample!.Points)
        {
            var position = TransformC3DPosition(point.Position);
            if (position.X < bounds.MinX || position.X > bounds.MaxX
                || position.Z < bounds.MinZ || position.Z > bounds.MaxZ)
            {
                continue;
            }

            count++;
            rawSum += point.RawValue;
            xSum += position.X;
            ySum += position.Y;
            zSum += position.Z;
        }

        if (count == 0)
        {
            stats = default;
            return false;
        }

        var inverse = 1.0 / count;
        stats = (
            count,
            rawSum * inverse,
            ySum * inverse,
            new Vector3((float)(xSum * inverse), (float)(ySum * inverse), (float)(zSum * inverse)));
        return true;
    }

    private (Vector3 origin, Vector3 direction) CreatePickRay(Point screenPoint)
    {
        return CameraMath.CreatePickRay(
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
            eye);

        viewModel.Pan(movement.X, movement.Y, movement.Z);
    }

}
