using System.Numerics;
using System.Windows;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private const int RegridMaximumRenderedNodes = 90000;
    private C3DTransformedHeightField? regridHeightFieldRenderOutput;
    private Vector3[]? regridHeightFieldPositions;
    private bool[]? regridHeightFieldPopulated;
    private Vector3 regridHeightFieldDisplayCenter;
    private float regridHeightFieldDisplayScale = 1f;

    public void ShowWorkbenchRegridHeightField(C3DTransformedHeightField output, bool isPublished, bool standaloneReferenceDisplay = true)
    {
        ArgumentNullException.ThrowIfNull(output);
        PrepareRegridHeightFieldRenderData(output);
        viewModel.C3DSampleVisible = !standaloneReferenceDisplay;
        viewModel.SetWorkbenchRegridHeightField(output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchRegridHeightField()
    {
        regridHeightFieldRenderOutput = null;
        regridHeightFieldPositions = null;
        regridHeightFieldPopulated = null;
        regridHeightFieldDisplayCenter = default;
        regridHeightFieldDisplayScale = 1f;
        viewModel.ClearWorkbenchRegridHeightField();
        RenderNow();
    }

    private void PrepareRegridHeightFieldRenderData(C3DTransformedHeightField output)
    {
        var profile = output.ReferenceGridProfile;
        var rawPositions = new Vector3[output.Cells.Count];
        var populated = new bool[output.Cells.Count];
        var minimumX = double.PositiveInfinity; var maximumX = double.NegativeInfinity;
        var minimumY = double.PositiveInfinity; var maximumY = double.NegativeInfinity;
        var minimumZ = double.PositiveInfinity; var maximumZ = double.NegativeInfinity;
        foreach (var cell in output.Cells)
        {
            var index = cell.Row * output.ColumnCount + cell.Column;
            if (!cell.HasValue) continue;
            var u = (cell.Column + 0.5) * profile.PitchU;
            var v = (cell.Row + 0.5) * profile.PitchV;
            var x = profile.Origin.X + (u * profile.UAxis.X) + (v * profile.VAxis.X) + (cell.Height * profile.HAxis.X);
            var y = profile.Origin.Y + (u * profile.UAxis.Y) + (v * profile.VAxis.Y) + (cell.Height * profile.HAxis.Y);
            var z = profile.Origin.Z + (u * profile.UAxis.Z) + (v * profile.VAxis.Z) + (cell.Height * profile.HAxis.Z);
            rawPositions[index] = new Vector3((float)x, (float)y, (float)z);
            populated[index] = true;
            minimumX = Math.Min(minimumX, x); maximumX = Math.Max(maximumX, x);
            minimumY = Math.Min(minimumY, y); maximumY = Math.Max(maximumY, y);
            minimumZ = Math.Min(minimumZ, z); maximumZ = Math.Max(maximumZ, z);
        }
        var span = Math.Max(1e-12, Math.Max(maximumX - minimumX, Math.Max(maximumY - minimumY, maximumZ - minimumZ)));
        var center = new Vector3((float)((minimumX + maximumX) * 0.5), (float)((minimumY + maximumY) * 0.5), (float)((minimumZ + maximumZ) * 0.5));
        var scale = (float)(C3DHeightGrid.ViewerHorizontalSpan * 0.78 / span);
        for (var index = 0; index < rawPositions.Length; index++)
        {
            if (populated[index]) rawPositions[index] = (rawPositions[index] - center) * scale;
        }
        regridHeightFieldRenderOutput = output;
        regridHeightFieldPositions = rawPositions;
        regridHeightFieldPopulated = populated;
        regridHeightFieldDisplayCenter = center;
        regridHeightFieldDisplayScale = scale;
    }

    private void DrawWorkbenchRegridHeightField(OpenGL gl)
    {
        var output = regridHeightFieldRenderOutput;
        var positions = regridHeightFieldPositions;
        var populated = regridHeightFieldPopulated;
        if (output is null || positions is null || populated is null || output.PopulatedCellCount == 0) return;
        var stride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1d, output.Cells.Count / (double)RegridMaximumRenderedNodes))));
        gl.LineWidth(viewModel.IsWorkbenchRegridHeightFieldPublished ? 1.8f : 2.2f);
        gl.Color(viewModel.IsWorkbenchRegridHeightFieldPublished ? 0.20 : 0.74, 0.84, viewModel.IsWorkbenchRegridHeightFieldPublished ? 0.48 : 0.96);
        gl.Begin(OpenGL.GL_LINES);
        for (var row = 0; row < output.RowCount; row += stride)
        {
            for (var column = 0; column < output.ColumnCount; column += stride)
            {
                var index = row * output.ColumnCount + column;
                if (!populated[index]) continue;
                DrawRegridNeighbor(gl, positions, populated, output.RowCount, output.ColumnCount, index, row, column + stride);
                DrawRegridNeighbor(gl, positions, populated, output.RowCount, output.ColumnCount, index, row + stride, column);
            }
        }
        gl.End();
        gl.PointSize(3.5f);
        gl.Color(1.0, 0.78, 0.18);
        gl.Begin(OpenGL.GL_POINTS);
        for (var index = 0; index < positions.Length; index += stride)
        {
            if (!populated[index]) continue;
            var point = positions[index];
            gl.Vertex(point.X, point.Y, point.Z);
        }
        gl.End();
    }

    private static void DrawRegridNeighbor(OpenGL gl, IReadOnlyList<Vector3> positions, IReadOnlyList<bool> populated, int rows, int columns, int sourceIndex, int row, int column)
    {
        if (row < 0 || row >= rows || column < 0 || column >= columns) return;
        var targetIndex = row * columns + column;
        if (!populated[targetIndex]) return;
        var start = positions[sourceIndex]; var end = positions[targetIndex];
        gl.Vertex(start.X, start.Y, start.Z); gl.Vertex(end.X, end.Y, end.Z);
    }

    private bool TryPickRegridHeightFieldPoint(Point screenPoint, out RegridHeightFieldPick hit)
    {
        hit = default;
        var output = regridHeightFieldRenderOutput;
        var positions = regridHeightFieldPositions;
        var populated = regridHeightFieldPopulated;
        if (output is null || positions is null || populated is null || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0) return false;

        var ray = CreatePickRay(screenPoint);
        var bestDistance = float.PositiveInfinity;
        var bestIndex = -1;
        var maximumDistance = Math.Max(0.12f, (float)viewModel.CameraDistance * 0.025f);
        for (var index = 0; index < positions.Length; index++)
        {
            if (!populated[index]) continue;
            var toPoint = positions[index] - ray.origin;
            var alongRay = Vector3.Dot(toPoint, ray.direction);
            if (alongRay < 0) continue;
            var distance = Vector3.Distance(positions[index], ray.origin + ray.direction * alongRay);
            if (distance < bestDistance) { bestDistance = distance; bestIndex = index; }
        }
        if (bestIndex < 0 || bestDistance > maximumDistance) return false;
        var cell = output.Cells[bestIndex];
        hit = new RegridHeightFieldPick(cell.Row, cell.Column, cell.Height, CreateRegridReferencePosition(cell.Row, cell.Column, cell.Height));
        return true;
    }

    private void DrawRegridTeachingSelectionOverlays(OpenGL gl)
    {
        var output = regridHeightFieldRenderOutput;
        if (output is null || viewModel.C3DSampleVisible) return;
        foreach (var selection in viewModel.AppliedTeachingSelections.Where(selection =>
                     string.Equals(selection.SourceBinding.Format, "TransformedHeightField", StringComparison.Ordinal)
                     && string.Equals(selection.SourceBinding.OwnerEntityId, output.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(selection.SourceBinding.ContentSha256, output.ContentSha256, StringComparison.OrdinalIgnoreCase)))
        {
            if (selection.GridRectangle is { } rectangle) DrawRegridGridRectangle(gl, rectangle, 0.10, 0.90, 0.88);
            if (selection.Points is { Count: > 0 } points) DrawRegridPointSet(gl, points, 0.10, 0.90, 0.88);
        }

        var capture = viewModel.TeachingCaptureSnapshot;
        if (!capture.IsActive
            || !string.Equals(viewModel.TeachingCaptureSourceBinding?.OwnerEntityId, output.OutputEntityId, StringComparison.OrdinalIgnoreCase)) return;
        if (capture.Kind == ToolRecipeSelectionKinds.GridRectangle && capture.Points.Count == 2)
        {
            var first = capture.Points[0].Locator; var second = capture.Points[1].Locator;
            DrawRegridGridRectangle(gl, new ToolRecipeGridRectangle(
                Math.Min(first.Row, second.Row), Math.Min(first.Column, second.Column),
                Math.Abs(second.Row - first.Row) + 1, Math.Abs(second.Column - first.Column) + 1), 1.0, 0.82, 0.12);
        }
        if (capture.Points.Count > 0) DrawRegridPointSet(gl, capture.Points, 1.0, 0.82, 0.12);
    }

    private void DrawRegridGridRectangle(OpenGL gl, ToolRecipeGridRectangle rectangle, double red, double green, double blue)
    {
        var output = regridHeightFieldRenderOutput;
        if (output is null || rectangle.Row < 0 || rectangle.Column < 0 || rectangle.RowCount <= 0 || rectangle.ColumnCount <= 0
            || rectangle.Row > output.RowCount - rectangle.RowCount || rectangle.Column > output.ColumnCount - rectangle.ColumnCount) return;
        var heights = output.Cells.Where(cell => cell.HasValue && cell.Row >= rectangle.Row && cell.Row < rectangle.Row + rectangle.RowCount
            && cell.Column >= rectangle.Column && cell.Column < rectangle.Column + rectangle.ColumnCount).Select(cell => cell.Height).ToArray();
        var height = heights.Length == 0 ? 0d : heights.Average();
        var lastRow = rectangle.Row + rectangle.RowCount - 1; var lastColumn = rectangle.Column + rectangle.ColumnCount - 1;
        var corners = new[]
        {
            CreateRegridDisplayPosition(rectangle.Row, rectangle.Column, height),
            CreateRegridDisplayPosition(rectangle.Row, lastColumn, height),
            CreateRegridDisplayPosition(lastRow, lastColumn, height),
            CreateRegridDisplayPosition(lastRow, rectangle.Column, height)
        };
        gl.LineWidth(3f); gl.Color(red, green, blue); gl.Begin(OpenGL.GL_LINE_LOOP);
        foreach (var corner in corners) gl.Vertex(corner.X, corner.Y, corner.Z);
        gl.End();
    }

    private void DrawRegridPointSet(
        OpenGL gl,
        IReadOnlyList<ToolRecipeSelectionPoint> points,
        double red,
        double green,
        double blue)
    {
        var positions = points.Select(point =>
            CreateRegridDisplayPosition(point.Locator.Row, point.Locator.Column, point.RawHeight)).ToArray();
        if (positions.Length >= 2)
        {
            gl.LineWidth(3f); gl.Color(red, green, blue); gl.Begin(OpenGL.GL_LINE_STRIP);
            foreach (var position in positions) gl.Vertex(position.X, position.Y, position.Z);
            gl.End();
        }
        gl.PointSize(11f); gl.Color(red, green, blue); gl.Begin(OpenGL.GL_POINTS);
        foreach (var position in positions) gl.Vertex(position.X, position.Y, position.Z);
        gl.End();
    }

    private Vector3 CreateRegridReferencePosition(int row, int column, double height)
    {
        var profile = regridHeightFieldRenderOutput!.ReferenceGridProfile;
        var u = (column + 0.5) * profile.PitchU; var v = (row + 0.5) * profile.PitchV;
        return new Vector3(
            (float)(profile.Origin.X + u * profile.UAxis.X + v * profile.VAxis.X + height * profile.HAxis.X),
            (float)(profile.Origin.Y + u * profile.UAxis.Y + v * profile.VAxis.Y + height * profile.HAxis.Y),
            (float)(profile.Origin.Z + u * profile.UAxis.Z + v * profile.VAxis.Z + height * profile.HAxis.Z));
    }

    private Vector3 CreateRegridDisplayPosition(int row, int column, double height) =>
        (CreateRegridReferencePosition(row, column, height) - regridHeightFieldDisplayCenter) * regridHeightFieldDisplayScale;

    private readonly record struct RegridHeightFieldPick(int Row, int Column, double Height, Vector3 ReferencePosition);
}
