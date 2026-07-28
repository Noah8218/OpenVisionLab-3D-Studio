using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private const int AffineApplyMaximumRenderedNodes = 90000;
    private C3DTransformedPointCloud? affineApplyRenderOutput;
    private int[]? affineApplyLocatorToPointIndex;
    private int[]? affineApplyRenderedPointIndexes;
    private AffineApplyDisplayFrame affineApplyDisplayFrame;

    public void ShowWorkbenchAffineApply(C3DTransformedPointCloud output, bool isPublished, bool standaloneReferenceDisplay = true)
    {
        ArgumentNullException.ThrowIfNull(output);
        PrepareAffineApplyRenderData(output);
        viewModel.C3DSampleVisible = !standaloneReferenceDisplay;
        viewModel.SetWorkbenchAffineApply(output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchAffineApply()
    {
        ClearAffineApplyRenderData();
        viewModel.ClearWorkbenchAffineApply();
        RenderNow();
    }

    private void PrepareAffineApplyRenderData(C3DTransformedPointCloud output)
    {
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

    private void ClearAffineApplyRenderData()
    {
        affineApplyRenderOutput = null;
        affineApplyLocatorToPointIndex = null;
        affineApplyRenderedPointIndexes = null;
        affineApplyDisplayFrame = default;
    }

    private void DrawWorkbenchAffineApply(OpenGL gl)
    {
        var output = affineApplyRenderOutput;
        var locators = affineApplyLocatorToPointIndex;
        var rendered = affineApplyRenderedPointIndexes;
        if (output is null || locators is null || rendered is null || rendered.Length == 0) return;

        var points = output.Points;
        var stride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1d, points.Count / (double)AffineApplyMaximumRenderedNodes))));
        gl.LineWidth(viewModel.IsWorkbenchAffineApplyPublished ? 1.8f : 2.2f);
        gl.Color(viewModel.IsWorkbenchAffineApplyPublished ? 0.20 : 0.72, 0.92, viewModel.IsWorkbenchAffineApplyPublished ? 0.78 : 1.0);
        gl.Begin(OpenGL.GL_LINES);
        foreach (var index in rendered)
        {
            var point = points[index];
            var start = affineApplyDisplayFrame.Map(point);
            DrawAffineApplyNeighbor(gl, points, locators, output.SourceGridWidth, output.SourceGridHeight, point, start, point.Row, point.Column + stride);
            DrawAffineApplyNeighbor(gl, points, locators, output.SourceGridWidth, output.SourceGridHeight, point, start, point.Row + stride, point.Column);
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
        C3DTransformedPoint source,
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
