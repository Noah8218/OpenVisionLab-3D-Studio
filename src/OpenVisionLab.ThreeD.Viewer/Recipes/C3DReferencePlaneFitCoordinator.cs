using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns the C3D reference-plane fit policy. Viewer-specific overlay geometry
/// and coordinate formatting remain callbacks owned by the View.
/// </summary>
public static class C3DReferencePlaneFitCoordinator
{
    internal const int MaxSampledPoints = 140000;

    public static bool Fit(
        C3DHeightGrid? grid,
        MainWindowViewModel viewModel,
        Action clearViewState,
        Action<HeightFieldPlaneFitResult, HeightGridPoint, (float MinX, float MaxX, float MinZ, float MaxZ)> applyPreviewOverlay,
        Action requestPreviewRender)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(clearViewState);
        ArgumentNullException.ThrowIfNull(applyPreviewOverlay);
        ArgumentNullException.ThrowIfNull(requestPreviewRender);

        if (grid is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Plane fit requires a visible C3D height grid";
            return false;
        }

        C3DHeightGrid fitSample;
        try
        {
            fitSample = grid.WithMaxRenderedPoints(MaxSampledPoints);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Plane fit sample load failed: {exception.Message}";
            return false;
        }

        var transform = viewModel.C3DModelTransform;
        var transformed = fitSample.Points
            .Select(point => (Point: point, Position: transform.Apply(point.Position)))
            .ToArray();

        HeightFieldPlaneFitResult result;
        try
        {
            result = HeightFieldPlaneFit.Fit(
                transformed
                    .Select(item => new HeightFieldPlaneSample(item.Position, item.Point.RawValue))
                    .ToArray());
        }
        catch (ArgumentException exception)
        {
            viewModel.ViewerStatus = $"Plane fit failed: {exception.Message}";
            return false;
        }

        clearViewState();
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
        var target = transformed.MinBy(item => Vector3.DistanceSquared(item.Position, result.Target));
        applyPreviewOverlay(result, target.Point, bounds);
        viewModel.SetPlaneReferenceMeasurement(result, "C3D least-squares height field / fixed sample");
        viewModel.SelectedEntity = "Plane Distance Measurement";
        viewModel.ViewerStatus = "Fitted C3D plane and maximum residual measured";
        requestPreviewRender();
        return true;
    }
}
