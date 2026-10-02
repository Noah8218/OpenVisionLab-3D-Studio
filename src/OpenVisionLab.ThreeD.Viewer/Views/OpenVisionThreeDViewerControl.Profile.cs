using System.Numerics;
using System.Windows;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private void OpenProfileView()
    {
        viewModel.ProfileSourceCommand.Execute(c3dSample);
        viewModel.ProfileActionCommand.Execute(ViewerHeightProfileAction.Begin);
        ProfileViewRequested?.Invoke(this, EventArgs.Empty);
        RenderNow();
    }

    private bool TryHandleProfilePick(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != MainWindowViewModel.ProfileSelectionMode) return false;
        ResetProfileIfSourceChanged();
        if (TryPickC3DPoint(screenPoint, out var point) && viewModel.ProfilePickCommand.CanExecute(point))
            viewModel.ProfilePickCommand.Execute(point);
        return true;
    }

    private bool TryBeginProfileEndpointDrag(Point screenPoint)
    {
        var profile = viewModel.HeightProfile;
        if (viewModel.SelectedSelectionMode != MainWindowViewModel.ProfileSelectionMode || profile.IsPicking || profile.First is not { } first) return false;
        var firstHit = ProfileEndpointHit(screenPoint, first);
        var secondHit = profile.Second is { } second && ProfileEndpointHit(screenPoint, second);
        if (!firstHit && !secondHit) return false;
        profileDraggedEndpoint = secondHit && !firstHit ? 2 : 1;
        viewModel.ProfileActionCommand.Execute(profileDraggedEndpoint == 1 ? ViewerHeightProfileAction.SelectA : ViewerHeightProfileAction.SelectB);
        return true;
    }

    private bool ProfileEndpointHit(Point screenPoint, HeightGridPoint endpoint)
    {
        var ray = CreatePickRay(screenPoint);
        if (!ViewerRayGeometry.TryProjectPoint(ray.origin, ray.direction, TransformC3DPosition(endpoint.Position), out _, out var distance)) return false;
        return distance <= Math.Max(0.18f, (float)viewModel.CameraDistance * 0.03f);
    }

    private bool TryMoveProfileEndpoint(Point screenPoint)
    {
        if (profileDraggedEndpoint == 0 || !TryPickC3DPoint(screenPoint, out var point)) return false;
        var move = (profileDraggedEndpoint, point);
        if (!viewModel.ProfileMoveCommand.CanExecute(move)) return false;
        var previous = profileDraggedEndpoint == 1 ? viewModel.HeightProfile.First : viewModel.HeightProfile.Second;
        viewModel.ProfileMoveCommand.Execute(move);
        UpdateLinkedProfileCursor(linkedHeightCursor);
        return previous != (profileDraggedEndpoint == 1 ? viewModel.HeightProfile.First : viewModel.HeightProfile.Second);
    }

    private void DrawProfileLine(OpenGL gl)
    {
        var profile = viewModel.HeightProfile;
        if (!profile.IsVisible || profile.First is not { } first || c3dSample is null
            || !string.Equals(profile.SourceSha256, c3dSample.ContentSha256, StringComparison.OrdinalIgnoreCase)) return;
        var lift = (float)viewModel.CameraDistance * 0.001f;
        var barHalfWidth = (float)viewModel.CameraDistance * 0.025f;
        var firstPosition = TransformC3DPosition(first.Position);
        var secondPosition = profile.Second is { } second ? TransformC3DPosition(second.Position) : firstPosition;
        var direction = secondPosition - firstPosition;
        direction.Y = 0;
        var side = direction.LengthSquared() < 0.000001f ? Vector3.UnitX : Vector3.Normalize(new Vector3(-direction.Z, 0, direction.X));
        var bandHalfWidth = barHalfWidth * 0.16f;
        gl.PushAttrib(OpenGL.GL_ENABLE_BIT | OpenGL.GL_CURRENT_BIT | OpenGL.GL_LINE_BIT | OpenGL.GL_POINT_BIT);
        gl.Disable(OpenGL.GL_LIGHTING);
        gl.LineWidth(3.5f);
        gl.Color(1.0, 0.72, 0.10);
        gl.Begin(OpenGL.GL_QUADS);
        for (var index = 1; index < profile.Samples.Length; index++)
        {
            if (profile.GetSampleStep(profile.Samples[index]) != profile.GetSampleStep(profile.Samples[index - 1]) + 1) continue;
            var a = TransformC3DPosition(profile.Samples[index - 1].Position) + Vector3.UnitY * lift;
            var b = TransformC3DPosition(profile.Samples[index].Position) + Vector3.UnitY * lift;
            foreach (var vertex in new[] { a - side * bandHalfWidth, a + side * bandHalfWidth, b + side * bandHalfWidth, b - side * bandHalfWidth })
                gl.Vertex(vertex.X, vertex.Y, vertex.Z);
        }
        gl.End();
        DrawBar(firstPosition, true);
        if (profile.Second is not null) DrawBar(secondPosition, false);
        gl.LineWidth(1f);
        gl.PointSize(1f);
        gl.PopAttrib();

        void DrawBar(Vector3 position, bool isFirst)
        {
            gl.Color(isFirst ? 0.20 : 1.0, isFirst ? 0.90 : 0.45, isFirst ? 1.0 : 0.22);
            var start = position - side * barHalfWidth + Vector3.UnitY * lift;
            var end = position + side * barHalfWidth + Vector3.UnitY * lift;
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(start.X, start.Y, start.Z);
            gl.Vertex(end.X, end.Y, end.Z);
            gl.End();
            if (TryProjectWorldPositionToViewport(end, out var screen))
                gl.DrawText((int)screen.X + 4, (int)(Viewport.ActualHeight - screen.Y) + 4, 1, 1, 1, "Segoe UI", 13, isFirst ? "A" : "B");
        }
    }

    private static bool SameGridCell(HeightGridPoint first, HeightGridPoint second) => first.Row == second.Row && first.Column == second.Column;

    private void UpdateLinkedProfileCursor(C3DGridCursor? cursor)
    {
        var profile = viewModel.HeightProfile;
        if (cursor is not { IsValid: true } value || !profile.HasEndpoints || c3dSample is null
            || !string.Equals(value.SourceContentSha256, profile.SourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(c3dSample.ContentSha256, profile.SourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            viewModel.ClearProfileLinkedCursor();
            return;
        }
        var index = Array.FindIndex(profile.Samples, point => point.Row == value.Row && point.Column == value.Column);
        if (index < 0) { viewModel.SetProfileLinkedCursorUnavailable(); return; }
        var sample = profile.Samples[index];
        if (sample.RawValue != value.RawHeight) { viewModel.ClearProfileLinkedCursor(); return; }
        viewModel.SetProfileLinkedCursor(value.Row, value.Column, sample.RawValue, profile.GetSampleStep(sample),
            profile.ExpectedSampleCount, profile.Minimum, profile.Maximum);
    }

    private void ResetProfileIfSourceChanged() => viewModel.ProfileSourceCommand.Execute(c3dSample);
}
