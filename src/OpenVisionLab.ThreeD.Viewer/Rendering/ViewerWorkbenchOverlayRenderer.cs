using System.Numerics;
using System.Windows;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Models;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Explicit input ports for the workbench overlay renderer. The renderer reads
/// snapshots through these callbacks and never reaches into the WPF control or
/// its ViewModel directly.
/// </summary>
internal sealed class ViewerWorkbenchOverlayCallbacks
{
    public ViewerWorkbenchOverlayCallbacks(
        Func<bool> isAffineApplyPublished,
        Func<bool> isRegridHeightFieldPublished,
        Func<bool> c3DSampleVisible,
        Func<double> cameraDistance,
        Func<double> viewportWidth,
        Func<double> viewportHeight,
        Func<Point, (Vector3 origin, Vector3 direction)> createPickRay,
        Func<IReadOnlyList<ToolRecipeSelection>> appliedTeachingSelections,
        Func<TeachingCaptureState> teachingCaptureSnapshot,
        Func<ToolRecipeSelectionSourceBinding?> teachingCaptureSourceBinding)
    {
        IsAffineApplyPublished = isAffineApplyPublished ?? throw new ArgumentNullException(nameof(isAffineApplyPublished));
        IsRegridHeightFieldPublished = isRegridHeightFieldPublished ?? throw new ArgumentNullException(nameof(isRegridHeightFieldPublished));
        C3DSampleVisible = c3DSampleVisible ?? throw new ArgumentNullException(nameof(c3DSampleVisible));
        CameraDistance = cameraDistance ?? throw new ArgumentNullException(nameof(cameraDistance));
        ViewportWidth = viewportWidth ?? throw new ArgumentNullException(nameof(viewportWidth));
        ViewportHeight = viewportHeight ?? throw new ArgumentNullException(nameof(viewportHeight));
        CreatePickRay = createPickRay ?? throw new ArgumentNullException(nameof(createPickRay));
        AppliedTeachingSelections = appliedTeachingSelections ?? throw new ArgumentNullException(nameof(appliedTeachingSelections));
        TeachingCaptureSnapshot = teachingCaptureSnapshot ?? throw new ArgumentNullException(nameof(teachingCaptureSnapshot));
        TeachingCaptureSourceBinding = teachingCaptureSourceBinding ?? throw new ArgumentNullException(nameof(teachingCaptureSourceBinding));
    }

    public Func<bool> IsAffineApplyPublished { get; }
    public Func<bool> IsRegridHeightFieldPublished { get; }
    public Func<bool> C3DSampleVisible { get; }
    public Func<double> CameraDistance { get; }
    public Func<double> ViewportWidth { get; }
    public Func<double> ViewportHeight { get; }
    public Func<Point, (Vector3 origin, Vector3 direction)> CreatePickRay { get; }
    public Func<IReadOnlyList<ToolRecipeSelection>> AppliedTeachingSelections { get; }
    public Func<TeachingCaptureState> TeachingCaptureSnapshot { get; }
    public Func<ToolRecipeSelectionSourceBinding?> TeachingCaptureSourceBinding { get; }
}

/// <summary>
/// Owns the render cache and drawing policy for the Affine Apply and Re-grid
/// Height Field workbench overlays. ViewModel state and WPF lifetime remain
/// outside this owner and are supplied through <see cref="ViewerWorkbenchOverlayCallbacks"/>.
/// </summary>
internal sealed class ViewerWorkbenchOverlayRenderer
{
    private const int AffineApplyMaximumRenderedNodes = 90000;
    private const int RegridMaximumRenderedNodes = 90000;

    private readonly ViewerWorkbenchOverlayCallbacks callbacks;
    private C3DTransformedPointCloud? affineApplyRenderOutput;
    private int[]? affineApplyLocatorToPointIndex;
    private int[]? affineApplyRenderedPointIndexes;
    private AffineApplyDisplayFrame affineApplyDisplayFrame;
    private C3DTransformedHeightField? regridHeightFieldRenderOutput;
    private Vector3[]? regridHeightFieldPositions;
    private bool[]? regridHeightFieldPopulated;
    private Vector3 regridHeightFieldDisplayCenter;
    private float regridHeightFieldDisplayScale = 1f;

    public ViewerWorkbenchOverlayRenderer(ViewerWorkbenchOverlayCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    internal C3DTransformedHeightField? RegridHeightFieldRenderOutput => regridHeightFieldRenderOutput;

    internal bool HasManagedData =>
        affineApplyRenderOutput is not null
        || affineApplyLocatorToPointIndex is not null
        || affineApplyRenderedPointIndexes is not null
        || regridHeightFieldRenderOutput is not null
        || regridHeightFieldPositions is not null
        || regridHeightFieldPopulated is not null;

    internal void PrepareAffineApply(C3DTransformedPointCloud output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var points = output.Points;
        var locators = new int[checked(output.SourceGridWidth * output.SourceGridHeight)];
        Array.Fill(locators, -1);
        var minimumX = double.PositiveInfinity;
        var maximumX = double.NegativeInfinity;
        var minimumY = double.PositiveInfinity;
        var maximumY = double.NegativeInfinity;
        var minimumZ = double.PositiveInfinity;
        var maximumZ = double.NegativeInfinity;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            locators[checked(point.Row * output.SourceGridWidth + point.Column)] = index;
            minimumX = Math.Min(minimumX, point.X); maximumX = Math.Max(maximumX, point.X);
            minimumY = Math.Min(minimumY, point.Y); maximumY = Math.Max(maximumY, point.Y);
            minimumZ = Math.Min(minimumZ, point.Z); maximumZ = Math.Max(maximumZ, point.Z);
        }

        var targetStride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1d, points.Count / (double)AffineApplyMaximumRenderedNodes))));
        var rendered = new List<int>(Math.Min(points.Count, AffineApplyMaximumRenderedNodes));
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            if (point.Row % targetStride == 0 && point.Column % targetStride == 0)
            {
                rendered.Add(index);
            }
        }

        affineApplyRenderOutput = output;
        affineApplyLocatorToPointIndex = locators;
        affineApplyRenderedPointIndexes = rendered.ToArray();
        affineApplyDisplayFrame = AffineApplyDisplayFrame.Create(minimumX, maximumX, minimumY, maximumY, minimumZ, maximumZ);
    }

    internal void ClearAffineApply()
    {
        affineApplyRenderOutput = null;
        affineApplyLocatorToPointIndex = null;
        affineApplyRenderedPointIndexes = null;
        affineApplyDisplayFrame = default;
    }

    internal void PrepareRegridHeightField(C3DTransformedHeightField output)
    {
        ArgumentNullException.ThrowIfNull(output);

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

    internal void ClearRegridHeightField()
    {
        regridHeightFieldRenderOutput = null;
        regridHeightFieldPositions = null;
        regridHeightFieldPopulated = null;
        regridHeightFieldDisplayCenter = default;
        regridHeightFieldDisplayScale = 1f;
    }

    internal void Clear()
    {
        ClearAffineApply();
        ClearRegridHeightField();
    }

    internal void DrawAffineApply(OpenGL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);
        var output = affineApplyRenderOutput;
        var locators = affineApplyLocatorToPointIndex;
        var rendered = affineApplyRenderedPointIndexes;
        if (output is null || locators is null || rendered is null || rendered.Length == 0) return;

        var points = output.Points;
        var stride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1d, points.Count / (double)AffineApplyMaximumRenderedNodes))));
        var isPublished = callbacks.IsAffineApplyPublished();
        gl.LineWidth(isPublished ? 1.8f : 2.2f);
        gl.Color(isPublished ? 0.20 : 0.72, 0.92, isPublished ? 0.78 : 1.0);
        gl.Begin(OpenGL.GL_LINES);
        foreach (var index in rendered)
        {
            var point = points[index];
            var start = affineApplyDisplayFrame.Map(point);
            DrawAffineApplyNeighbor(gl, points, locators, output.SourceGridWidth, output.SourceGridHeight, start, point.Row, point.Column + stride);
            DrawAffineApplyNeighbor(gl, points, locators, output.SourceGridWidth, output.SourceGridHeight, start, point.Row + stride, point.Column);
        }
        gl.End();

        gl.PointSize(3.5f);
        gl.Color(1.0, 0.78, 0.18);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var index in rendered)
        {
            var point = affineApplyDisplayFrame.Map(points[index]);
            gl.Vertex(point.X, point.Y, point.Z);
        }
        gl.End();
    }

    private void DrawAffineApplyNeighbor(
        OpenGL gl,
        IReadOnlyList<C3DTransformedPoint> points,
        IReadOnlyList<int> locators,
        int width,
        int height,
        Vector3 start,
        int row,
        int column)
    {
        if (row < 0 || row >= height || column < 0 || column >= width) return;
        var index = locators[checked(row * width + column)];
        if (index < 0) return;
        var end = affineApplyDisplayFrame.Map(points[index]);
        gl.Vertex(start.X, start.Y, start.Z);
        gl.Vertex(end.X, end.Y, end.Z);
    }

    internal void DrawRegridHeightField(OpenGL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);
        var output = regridHeightFieldRenderOutput;
        var positions = regridHeightFieldPositions;
        var populated = regridHeightFieldPopulated;
        if (output is null || positions is null || populated is null || output.PopulatedCellCount == 0) return;
        var stride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1d, output.Cells.Count / (double)RegridMaximumRenderedNodes))));
        var isPublished = callbacks.IsRegridHeightFieldPublished();
        gl.LineWidth(isPublished ? 1.8f : 2.2f);
        gl.Color(isPublished ? 0.20 : 0.74, 0.84, isPublished ? 0.48 : 0.96);
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

    internal bool TryPickRegridHeightFieldPoint(Point screenPoint, out ViewerRegridHeightFieldPick hit)
    {
        hit = default;
        var output = regridHeightFieldRenderOutput;
        var positions = regridHeightFieldPositions;
        var populated = regridHeightFieldPopulated;
        if (output is null || positions is null || populated is null || callbacks.ViewportWidth() <= 0 || callbacks.ViewportHeight() <= 0) return false;

        var ray = callbacks.CreatePickRay(screenPoint);
        var bestDistance = float.PositiveInfinity;
        var bestIndex = -1;
        var maximumDistance = Math.Max(0.12f, (float)callbacks.CameraDistance() * 0.025f);
        for (var index = 0; index < positions.Length; index++)
        {
            if (!populated[index]) continue;
            if (!ViewerRayGeometry.TryProjectPoint(
                    ray.origin,
                    ray.direction,
                    positions[index],
                    out _,
                    out var distance)) continue;
            if (distance < bestDistance) { bestDistance = distance; bestIndex = index; }
        }
        if (bestIndex < 0 || bestDistance > maximumDistance) return false;
        var cell = output.Cells[bestIndex];
        hit = new ViewerRegridHeightFieldPick(cell.Row, cell.Column, cell.Height, CreateRegridReferencePosition(cell.Row, cell.Column, cell.Height));
        return true;
    }

    internal void DrawRegridTeachingSelectionOverlays(OpenGL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);
        var output = regridHeightFieldRenderOutput;
        if (output is null || callbacks.C3DSampleVisible()) return;
        foreach (var selection in callbacks.AppliedTeachingSelections().Where(selection =>
                     string.Equals(selection.SourceBinding.Format, "TransformedHeightField", StringComparison.Ordinal)
                     && string.Equals(selection.SourceBinding.OwnerEntityId, output.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(selection.SourceBinding.ContentSha256, output.ContentSha256, StringComparison.OrdinalIgnoreCase)))
        {
            if (selection.GridRectangle is { } rectangle) DrawRegridGridRectangle(gl, rectangle, 0.10, 0.90, 0.88);
            if (selection.Points is { Count: > 0 } points) DrawRegridPointSet(gl, points, 0.10, 0.90, 0.88);
        }

        var capture = callbacks.TeachingCaptureSnapshot();
        if (!capture.IsActive
            || !string.Equals(callbacks.TeachingCaptureSourceBinding()?.OwnerEntityId, output.OutputEntityId, StringComparison.OrdinalIgnoreCase)) return;
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

    private readonly record struct AffineApplyDisplayFrame(
        double CenterX,
        double CenterY,
        double CenterZ,
        double Scale)
    {
        public static AffineApplyDisplayFrame Create(
            double minimumX,
            double maximumX,
            double minimumY,
            double maximumY,
            double minimumZ,
            double maximumZ)
        {
            var maximumSpan = Math.Max(1e-12, Math.Max(maximumX - minimumX, Math.Max(maximumY - minimumY, maximumZ - minimumZ)));
            return new AffineApplyDisplayFrame(
                (minimumX + maximumX) * 0.5,
                (minimumY + maximumY) * 0.5,
                (minimumZ + maximumZ) * 0.5,
                C3DHeightGrid.ViewerHorizontalSpan * 0.78 / maximumSpan);
        }

        public Vector3 Map(C3DTransformedPoint point) => new(
            (float)((point.X - CenterX) * Scale),
            (float)((point.Y - CenterY) * Scale),
            (float)((point.Z - CenterZ) * Scale));
    }
}

internal readonly record struct ViewerRegridHeightFieldPick(int Row, int Column, double Height, Vector3 ReferencePosition);
