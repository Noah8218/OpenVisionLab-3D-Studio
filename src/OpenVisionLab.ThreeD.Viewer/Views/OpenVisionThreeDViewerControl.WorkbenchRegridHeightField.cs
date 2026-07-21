using System.Numerics;
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
}
