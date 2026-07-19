using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private void OpenProfileView()
    {
        viewModel.SelectedSelectionMode = MainWindowViewModel.ProfileSelectionMode;
        ResetProfileIfSourceChanged();
        if (profileFirst is null || profileSecond is null)
        {
            InitializeDefaultProfile();
        }
        else
        {
            UpdateProfileState();
        }

        ProfileViewRequested?.Invoke(this, EventArgs.Empty);
        RenderNow();
    }

    private void InitializeDefaultProfile()
    {
        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length < 2)
        {
            profileFirst = null;
            profileSecond = null;
            viewModel.ClearProfile();
            viewModel.ViewerStatus = "Profile requires a visible C3D height grid";
            return;
        }

        var row = c3dSample.Points
            .GroupBy(point => point.Row)
            .OrderBy(group => Math.Abs(group.Key - c3dSample.Height / 2))
            .First()
            .OrderBy(point => point.Column)
            .ToArray();
        if (row.Length < 2)
        {
            profileFirst = null;
            profileSecond = null;
            viewModel.ClearProfile();
            viewModel.ViewerStatus = "Profile requires two valid C3D cells";
            return;
        }

        profileFirst = row[Math.Max(0, row.Length / 4)];
        profileSecond = row[Math.Min(row.Length - 1, row.Length * 3 / 4)];
        profileSourceSha256 = c3dSample.ContentSha256;
        if (SameGridCell(profileFirst.Value, profileSecond.Value))
        {
            profileFirst = row[0];
            profileSecond = row[^1];
        }

        UpdateProfileState();
    }

    private bool TryHandleProfilePick(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != MainWindowViewModel.ProfileSelectionMode)
        {
            return false;
        }

        ResetProfileIfSourceChanged();

        if (!TryPickC3DPoint(screenPoint, out var point))
        {
            viewModel.ViewerStatus = "Profile pick missed the C3D height grid";
            return true;
        }

        if (profileFirst is null || profileSecond is not null)
        {
            profileFirst = point;
            profileSecond = null;
            profileSourceSha256 = c3dSample?.ContentSha256;
            viewModel.SetProfileStart(point.Row, point.Column, TransformC3DPosition(point.Position), point.RawValue);
            viewModel.ViewerStatus = "Profile P1 set; choose P2";
        }
        else if (SameGridCell(profileFirst.Value, point))
        {
            viewModel.ViewerStatus = "Profile P2 must be different from P1";
        }
        else
        {
            profileSecond = point;
            UpdateProfileState();
        }

        viewModel.SelectedEntity = "C3D Height Profile";
        viewModel.PickCoordinate = FormatC3DPoint(point);
        return true;
    }

    private bool TryBeginProfileEndpointDrag(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != MainWindowViewModel.ProfileSelectionMode
            || profileFirst is null)
        {
            return false;
        }

        var firstHit = ProfileEndpointHit(screenPoint, profileFirst.Value);
        var secondHit = profileSecond is { } second && ProfileEndpointHit(screenPoint, second);
        if (!firstHit && !secondHit)
        {
            return false;
        }

        profileDraggedEndpoint = secondHit && !firstHit ? 2 : 1;
        viewModel.ViewerStatus = $"Dragging profile P{profileDraggedEndpoint}";
        return true;
    }

    private bool ProfileEndpointHit(Point screenPoint, HeightGridPoint endpoint)
    {
        var ray = CreatePickRay(screenPoint);
        var position = TransformC3DPosition(endpoint.Position);
        var alongRay = Vector3.Dot(position - ray.origin, ray.direction);
        if (alongRay < 0.0f)
        {
            return false;
        }

        var nearest = ray.origin + ray.direction * alongRay;
        var threshold = Math.Max(0.18f, (float)viewModel.CameraDistance * 0.03f);
        return Vector3.Distance(position, nearest) <= threshold;
    }

    private bool TryMoveProfileEndpoint(Point screenPoint)
    {
        if (profileDraggedEndpoint == 0 || !TryPickC3DPoint(screenPoint, out var point))
        {
            return false;
        }

        if (profileDraggedEndpoint == 1)
        {
            if (profileFirst is { } first && SameGridCell(first, point)
                || profileSecond is { } second && SameGridCell(second, point))
            {
                return false;
            }

            profileFirst = point;
        }
        else
        {
            if (profileSecond is { } second && SameGridCell(second, point)
                || profileFirst is { } first && SameGridCell(first, point))
            {
                return false;
            }

            profileSecond = point;
        }

        UpdateProfileState();
        viewModel.PickCoordinate = FormatC3DPoint(point);
        return true;
    }

    private void UpdateProfileState()
    {
        if (c3dSample is null || profileFirst is not { } first || profileSecond is not { } second)
        {
            return;
        }

        try
        {
            var samples = c3dSample.ReadLineProfile(first.Row, first.Column, second.Row, second.Column);
            if (samples.Length == 0)
            {
                viewModel.ClearProfile();
                viewModel.ViewerStatus = "Profile line contains no valid C3D samples";
                return;
            }

            var minimum = samples.Min(point => (double)point.RawValue);
            var maximum = samples.Max(point => (double)point.RawValue);
            var mean = samples.Average(point => (double)point.RawValue);
            var expected = Math.Max(Math.Abs(second.Row - first.Row), Math.Abs(second.Column - first.Column)) + 1;
            viewModel.SetProfile(
                first.Row,
                first.Column,
                TransformC3DPosition(first.Position),
                first.RawValue,
                second.Row,
                second.Column,
                TransformC3DPosition(second.Position),
                second.RawValue,
                samples.Length,
                Math.Max(0, expected - samples.Length),
                minimum,
                maximum,
                mean,
                BuildSectionProfilePath(samples, minimum, maximum));
            viewModel.SelectedEntity = "C3D Height Profile";
            viewModel.SelectionSummary = viewModel.ProfileSummary;
            viewModel.MeasurementSummary = viewModel.ProfileRange;
            viewModel.ViewerStatus = string.Create(
                CultureInfo.InvariantCulture,
                $"Profile updated: {samples.Length:N0} valid samples");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            viewModel.ViewerStatus = $"Profile update failed: {exception.Message}";
        }
    }

    private void DrawProfileLine(OpenGL gl)
    {
        if (profileFirst is not { } first)
        {
            return;
        }

        if (profileSourceSha256 is null
            || c3dSample is null
            || !profileSourceSha256.Equals(c3dSample.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var firstPosition = TransformC3DPosition(first.Position);
        var secondPosition = profileSecond is { } second
            ? TransformC3DPosition(second.Position)
            : firstPosition;

        if (profileSecond is not null)
        {
            gl.LineWidth(3.5f);
            gl.Begin(OpenGL.GL_LINES);
            gl.Color(1.0, 0.72, 0.10);
            gl.Vertex(firstPosition.X, firstPosition.Y, firstPosition.Z);
            gl.Vertex(secondPosition.X, secondPosition.Y, secondPosition.Z);
            gl.End();
        }

        gl.PointSize(11.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(0.20, 0.90, 1.0);
        gl.Vertex(firstPosition.X, firstPosition.Y, firstPosition.Z);
        if (profileSecond is not null)
        {
            gl.Color(1.0, 0.45, 0.22);
            gl.Vertex(secondPosition.X, secondPosition.Y, secondPosition.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
    }

    private static bool SameGridCell(HeightGridPoint first, HeightGridPoint second) =>
        first.Row == second.Row && first.Column == second.Column;

    private void ResetProfileIfSourceChanged()
    {
        var currentSha256 = c3dSample?.ContentSha256;
        if (profileSourceSha256 is null
            || currentSha256 is not null
                && profileSourceSha256.Equals(currentSha256, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        profileFirst = null;
        profileSecond = null;
        profileDraggedEndpoint = 0;
        profileSourceSha256 = null;
        viewModel.ClearProfile();
    }
}
