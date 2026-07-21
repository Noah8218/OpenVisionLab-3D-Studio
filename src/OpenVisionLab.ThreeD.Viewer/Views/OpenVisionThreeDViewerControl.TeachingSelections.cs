using System.Numerics;
using System.Windows;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private Point? teachingCapturePointerDownPosition;
    private bool teachingCaptureDragExceeded;

    public event EventHandler<TeachingCaptureStateChangedEventArgs>? TeachingCaptureStateChanged;

    public TeachingCaptureState TeachingCaptureSnapshot => viewModel.TeachingCaptureSnapshot;

    public bool BeginC3DTeachingCapture(TeachingCaptureRequest request, out string message)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            message = "A visible C3D source is required before teaching capture.";
            return false;
        }

        if (!string.Equals(request.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || request.SourceBinding.GridWidth != c3dSample.Width
            || request.SourceBinding.GridHeight != c3dSample.Height)
        {
            message = "Teaching capture source format or grid dimensions do not match the loaded C3D source.";
            return false;
        }

        if (!IsSha256(request.SourceBinding.ContentSha256))
        {
            message = "Teaching capture requires a valid C3D source SHA-256 binding.";
            return false;
        }

        if (!string.Equals(
                request.SourceBinding.ContentSha256,
                c3dSample.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            message = "Teaching capture source SHA-256 does not match the loaded C3D bytes.";
            return false;
        }

        teachingCapturePointerDownPosition = null;
        teachingCaptureDragExceeded = false;
        if (!viewModel.BeginTeachingCapture(request, out message))
        {
            return false;
        }

        RaiseTeachingCaptureStateChanged();
        RenderNow();
        return true;
    }

    public bool UndoC3DTeachingCapture()
    {
        if (!viewModel.UndoTeachingCapture())
        {
            return false;
        }

        RaiseTeachingCaptureStateChanged();
        RenderNow();
        return true;
    }

    public void CancelC3DTeachingCapture()
    {
        teachingCapturePointerDownPosition = null;
        teachingCaptureDragExceeded = false;
        if (!viewModel.CancelTeachingCapture())
        {
            return;
        }

        RaiseTeachingCaptureStateChanged();
        RenderNow();
    }

    public bool TryGetC3DTeachingCandidate(out ToolRecipeSelection? selection, out string message) =>
        viewModel.TryGetTeachingCaptureCandidate(out selection, out message);

    public void ConfirmC3DTeachingCaptureApplied()
    {
        teachingCapturePointerDownPosition = null;
        teachingCaptureDragExceeded = false;
        viewModel.ConfirmTeachingCaptureApplied();
        RaiseTeachingCaptureStateChanged();
        RenderNow();
    }

    public void SetAppliedTeachingSelections(IReadOnlyList<ToolRecipeSelection> selections)
    {
        viewModel.SetAppliedTeachingSelections(selections);
        RaiseTeachingCaptureStateChanged();
        RenderNow();
    }

    private bool TryHandleC3DTeachingCapturePick(Point screenPoint)
    {
        if (!viewModel.IsTeachingCaptureActive)
        {
            return false;
        }

        if (!TryPickC3DPoint(screenPoint, out var point))
        {
            const string message = "Teaching capture pick missed the visible C3D grid.";
            viewModel.SetTeachingCaptureMessage(message);
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = message;
            RaiseTeachingCaptureStateChanged();
            return true;
        }

        var sourcePosition = point.Position;
        var selectionPoint = new ToolRecipeSelectionPoint(
            new ToolRecipeGridCellLocator("grid-cell", point.Row, point.Column),
            new ToolRecipeXyz(sourcePosition.X, sourcePosition.Y, sourcePosition.Z),
            point.RawValue);
        viewModel.TryAddTeachingCapturePoint(selectionPoint, out var captureMessage);
        viewModel.SelectedEntity = "Teaching Selection Candidate";
        viewModel.PickCoordinate = FormatC3DPoint(point);
        viewModel.ViewerStatus = captureMessage;
        RaiseTeachingCaptureStateChanged();
        return true;
    }

    private void ClearTeachingSelectionsForSourceChange()
    {
        teachingCapturePointerDownPosition = null;
        teachingCaptureDragExceeded = false;
        viewModel.CancelTeachingCapture("Teaching capture cleared because the C3D source changed.");
        viewModel.SetAppliedTeachingSelections([]);
        viewModel.ClearWorkbenchHeightDifferenceEdge();
        viewModel.ClearWorkbenchTwoPointLine();
        viewModel.ClearWorkbenchLineFit();
        viewModel.ClearWorkbenchLineIntersection();
        viewModel.ClearWorkbenchLandmarkCorrespondence();
        RaiseTeachingCaptureStateChanged();
    }

    private void DrawTeachingSelectionOverlays(OpenGL gl)
    {
        if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            return;
        }

        foreach (var selection in viewModel.AppliedTeachingSelections.Where(IsSelectionForCurrentC3DGrid))
        {
            DrawTeachingSelection(gl, selection, 0.10, 0.90, 0.88);
        }

        DrawWorkbenchHeightDifferenceEdge(gl);
        DrawWorkbenchTwoPointLine(gl);
        DrawWorkbenchLineFit(gl);
        DrawWorkbenchLineIntersection(gl);
        DrawWorkbenchLandmarkCorrespondence(gl);

        var capture = viewModel.TeachingCaptureSnapshot;
        if (capture.IsActive)
        {
            DrawTeachingCaptureCandidate(gl, capture, 1.00, 0.82, 0.12);
        }

        gl.LineWidth(1.0f);
        gl.PointSize(1.0f);
    }

    private void DrawWorkbenchHeightDifferenceEdge(OpenGL gl)
    {
        var output = viewModel.WorkbenchHeightDifferenceEdge;
        if (output is null || c3dSample is null
            || output.Selection.Row < 0 || output.Selection.Column < 0
            || output.Selection.Row > c3dSample.Height - output.Selection.RowCount
            || output.Selection.Column > c3dSample.Width - output.Selection.ColumnCount)
        {
            return;
        }

        DrawTeachingGridRectangle(gl, output.Selection, 1.0, 0.67, 0.12);
        var centerRow = output.Selection.Row + (output.Selection.RowCount - 1) / 2.0;
        var centerColumn = output.Selection.Column + (output.Selection.ColumnCount - 1) / 2.0;
        var arrowLength = Math.Max(2.0, output.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
            ? output.Selection.ColumnCount * 0.22
            : output.Selection.RowCount * 0.22);
        var arrowEndRow = centerRow + (output.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossRows ? arrowLength : 0.0);
        var arrowEndColumn = centerColumn + (output.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns ? arrowLength : 0.0);
        var arrowStart = CreateC3DGridDisplayPosition(centerRow, centerColumn, c3dSample.Mean);
        var arrowEnd = CreateC3DGridDisplayPosition(arrowEndRow, arrowEndColumn, c3dSample.Mean);
        gl.LineWidth(4.0f);
        gl.Color(1.0, 0.82, 0.18);
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(arrowStart.X, arrowStart.Y, arrowStart.Z);
        gl.Vertex(arrowEnd.X, arrowEnd.Y, arrowEnd.Z);
        gl.End();

        gl.PointSize(8.0f);
        gl.Color(viewModel.IsWorkbenchHeightDifferenceEdgePublished ? 0.30 : 1.0, 0.92, 0.28);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var point in output.Points)
        {
            var position = CreateC3DGridDisplayPosition(point.Z, point.X, point.Y);
            gl.Vertex(position.X, position.Y, position.Z);
        }
        gl.End();

        if (viewModel.SelectedWorkbenchHeightDifferenceEdgePoint is { } selected)
        {
            var position = CreateC3DGridDisplayPosition(selected.Z, selected.X, selected.Y);
            gl.PointSize(14.0f);
            gl.Color(1.0, 0.25, 0.18);
            gl.Begin(OpenGL.GL_POINTS);
            gl.Vertex(position.X, position.Y, position.Z);
            gl.End();
        }
    }

    private void DrawWorkbenchLineFit(OpenGL gl)
    {
        var output = viewModel.WorkbenchLineFit;
        if (output is null || c3dSample is null) return;

        if (viewModel.LineFitInliersVisible || viewModel.LineFitOutliersVisible)
        {
            gl.PointSize(7.0f);
            gl.Begin(OpenGL.GL_POINTS);
            foreach (var point in output.PointDiagnostics.Where(point => point.IsInlier ? viewModel.LineFitInliersVisible : viewModel.LineFitOutliersVisible))
            {
                gl.Color(point.IsInlier ? 0.10 : 1.0, point.IsInlier ? 0.90 : 0.67, point.IsInlier ? 0.82 : 0.12);
                var position = CreateC3DGridDisplayPosition(point.Z, point.X, point.Y);
                gl.Vertex(position.X, position.Y, position.Z);
            }
            gl.End();
        }

        if (viewModel.LineFitSegmentVisible)
        {
            var start = CreateC3DGridDisplayPosition(output.SegmentStartZ, output.SegmentStartX, output.SegmentStartY);
            var end = CreateC3DGridDisplayPosition(output.SegmentEndZ, output.SegmentEndX, output.SegmentEndY);
            gl.LineWidth(4.0f);
            gl.Color(0.10, 0.90, 0.82);
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(start.X, start.Y, start.Z);
            gl.Vertex(end.X, end.Y, end.Z);
            gl.End();

            var arrowEnd = CreateC3DGridDisplayPosition(
                output.SegmentStartZ + (output.SegmentEndZ - output.SegmentStartZ) * 0.16,
                output.SegmentStartX + (output.SegmentEndX - output.SegmentStartX) * 0.16,
                output.SegmentStartY + (output.SegmentEndY - output.SegmentStartY) * 0.16);
            gl.LineWidth(2.5f);
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(start.X, start.Y, start.Z);
            gl.Vertex(arrowEnd.X, arrowEnd.Y, arrowEnd.Z);
            gl.End();
        }

        if (viewModel.LineFitSelectedResidualVisible && viewModel.SelectedWorkbenchLineFitPoint is { } selected)
        {
            var position = CreateC3DGridDisplayPosition(selected.Z, selected.X, selected.Y);
            var projected = CreateC3DGridDisplayPosition(selected.ProjectedZ, selected.ProjectedX, selected.ProjectedY);
            gl.LineWidth(2.0f);
            gl.Color(1.0, 0.82, 0.12);
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(position.X, position.Y, position.Z);
            gl.Vertex(projected.X, projected.Y, projected.Z);
            gl.End();
            gl.PointSize(13.0f);
            gl.Begin(OpenGL.GL_POINTS);
            gl.Vertex(position.X, position.Y, position.Z);
            gl.End();
        }
    }

    private void DrawWorkbenchTwoPointLine(OpenGL gl)
    {
        var output = viewModel.WorkbenchTwoPointLine;
        if (output is null || c3dSample is null) return;
        DrawWorkbenchLineSegment(gl, output,
            viewModel.IsWorkbenchTwoPointLinePublished ? 0.18 : 1.0,
            0.86,
            viewModel.IsWorkbenchTwoPointLinePublished ? 0.76 : 0.16);
        var start = CreateC3DGridDisplayPosition(output.SegmentStartZ, output.SegmentStartX, output.SegmentStartY);
        var end = CreateC3DGridDisplayPosition(output.SegmentEndZ, output.SegmentEndX, output.SegmentEndY);
        gl.PointSize(11.0f);
        gl.Color(1.0, 0.86, 0.20);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Vertex(start.X, start.Y, start.Z);
        gl.Vertex(end.X, end.Y, end.Z);
        gl.End();
    }

    private void DrawWorkbenchLineIntersection(OpenGL gl)
    {
        var output = viewModel.WorkbenchLineIntersection;
        var firstLine = viewModel.WorkbenchFirstIntersectionLine;
        var secondLine = viewModel.WorkbenchSecondIntersectionLine;
        if (firstLine is null || secondLine is null || c3dSample is null) return;

        if (viewModel.LineIntersectionFirstLineVisible)
        {
            DrawWorkbenchLineSegment(gl, firstLine, 0.10, 0.90, 0.82);
        }

        if (viewModel.LineIntersectionSecondLineVisible)
        {
            DrawWorkbenchLineSegment(gl, secondLine, 0.72, 0.45, 1.00);
        }

        if (output is not null && viewModel.LineIntersectionClosestConnectorVisible)
        {
            var firstClosest = CreateC3DGridDisplayPosition(output.FirstClosestZ, output.FirstClosestX, output.FirstClosestY);
            var secondClosest = CreateC3DGridDisplayPosition(output.SecondClosestZ, output.SecondClosestX, output.SecondClosestY);
            gl.LineWidth(3.0f);
            gl.Color(1.0, 0.74, 0.16);
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(firstClosest.X, firstClosest.Y, firstClosest.Z);
            gl.Vertex(secondClosest.X, secondClosest.Y, secondClosest.Z);
            gl.End();
        }

        if (output is not null && viewModel.LineIntersectionCornerAnchorVisible)
        {
            var corner = CreateC3DGridDisplayPosition(output.CornerAnchorZ, output.CornerAnchorX, output.CornerAnchorY);
            gl.PointSize(15.0f);
            gl.Color(1.0, 0.20, 0.65);
            gl.Begin(OpenGL.GL_POINTS);
            gl.Vertex(corner.X, corner.Y, corner.Z);
            gl.End();
        }
    }

    private void DrawWorkbenchLandmarkCorrespondence(OpenGL gl)
    {
        var output = viewModel.WorkbenchLandmarkCorrespondence;
        var anchors = viewModel.WorkbenchLandmarkCorrespondenceAnchors;
        if (output is null || anchors.Count != 4 || c3dSample is null) return;

        var positions = anchors
            .Select(anchor => CreateC3DGridDisplayPosition(anchor.CornerAnchorZ, anchor.CornerAnchorX, anchor.CornerAnchorY))
            .ToArray();

        gl.LineWidth(2.5f);
        gl.Color(viewModel.IsWorkbenchLandmarkCorrespondencePublished ? 0.18 : 1.0, 0.86, 0.76);
        gl.Begin(OpenGL.GL_LINES);
        foreach (var (first, second) in new[] { (0, 1), (0, 2), (0, 3), (1, 2), (1, 3), (2, 3) })
        {
            gl.Vertex(positions[first].X, positions[first].Y, positions[first].Z);
            gl.Vertex(positions[second].X, positions[second].Y, positions[second].Z);
        }
        gl.End();

        gl.PointSize(16.0f);
        gl.Color(1.0, 0.18, 0.62);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var position in positions)
        {
            gl.Vertex(position.X, position.Y, position.Z);
        }
        gl.End();
    }

    private void DrawWorkbenchLineSegment(OpenGL gl, IC3DLineGeometry line, double red, double green, double blue)
    {
        var start = CreateC3DGridDisplayPosition(line.SegmentStartZ, line.SegmentStartX, line.SegmentStartY);
        var end = CreateC3DGridDisplayPosition(line.SegmentEndZ, line.SegmentEndX, line.SegmentEndY);
        gl.LineWidth(4.0f);
        gl.Color(red, green, blue);
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(start.X, start.Y, start.Z);
        gl.Vertex(end.X, end.Y, end.Z);
        gl.End();
    }

    private bool IsSelectionForCurrentC3DGrid(ToolRecipeSelection selection) =>
        c3dSample is not null
        && string.Equals(selection.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
        && string.Equals(selection.SourceBinding.ContentSha256, c3dSample.ContentSha256, StringComparison.OrdinalIgnoreCase)
        && selection.SourceBinding.GridWidth == c3dSample.Width
        && selection.SourceBinding.GridHeight == c3dSample.Height;

    private void DrawTeachingSelection(OpenGL gl, ToolRecipeSelection selection, double red, double green, double blue)
    {
        if (selection.GridRectangle is { } rectangle)
        {
            DrawTeachingGridRectangle(gl, rectangle, red, green, blue);
        }

        if (selection.Points is { Count: > 0 } points)
        {
            DrawTeachingPointSet(gl, points, red, green, blue);
        }
    }

    private void DrawTeachingCaptureCandidate(
        OpenGL gl,
        TeachingCaptureState capture,
        double red,
        double green,
        double blue)
    {
        if (capture.Kind == ToolRecipeSelectionKinds.GridRectangle && capture.Points.Count == 2)
        {
            var first = capture.Points[0].Locator;
            var second = capture.Points[1].Locator;
            DrawTeachingGridRectangle(
                gl,
                new ToolRecipeGridRectangle(
                    Math.Min(first.Row, second.Row),
                    Math.Min(first.Column, second.Column),
                    Math.Abs(second.Row - first.Row) + 1,
                    Math.Abs(second.Column - first.Column) + 1),
                red,
                green,
                blue);
        }

        if (capture.Points.Count > 0)
        {
            DrawTeachingPointSet(gl, capture.Points, red, green, blue);
        }
    }

    private void DrawTeachingGridRectangle(
        OpenGL gl,
        ToolRecipeGridRectangle rectangle,
        double red,
        double green,
        double blue)
    {
        if (c3dSample is null
            || rectangle.Row < 0
            || rectangle.Column < 0
            || rectangle.RowCount <= 0
            || rectangle.ColumnCount <= 0
            || rectangle.Row > c3dSample.Height - rectangle.RowCount
            || rectangle.Column > c3dSample.Width - rectangle.ColumnCount)
        {
            return;
        }

        var lastRow = rectangle.Row + rectangle.RowCount - 1;
        var lastColumn = rectangle.Column + rectangle.ColumnCount - 1;
        var topLeft = CreateC3DGridDisplayPosition(rectangle.Row, rectangle.Column, c3dSample.Mean);
        var topRight = CreateC3DGridDisplayPosition(rectangle.Row, lastColumn, c3dSample.Mean);
        var bottomRight = CreateC3DGridDisplayPosition(lastRow, lastColumn, c3dSample.Mean);
        var bottomLeft = CreateC3DGridDisplayPosition(lastRow, rectangle.Column, c3dSample.Mean);

        gl.LineWidth(3.0f);
        gl.Color(red, green, blue);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(topRight.X, topRight.Y, topRight.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.Vertex(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
        gl.End();
    }

    private void DrawTeachingPointSet(
        OpenGL gl,
        IReadOnlyList<ToolRecipeSelectionPoint> points,
        double red,
        double green,
        double blue)
    {
        var positions = points
            .Select(point => TransformC3DPosition(new Vector3(
                (float)point.CapturedPosition.X,
                (float)point.CapturedPosition.Y,
                (float)point.CapturedPosition.Z)))
            .ToArray();

        if (positions.Length >= 2)
        {
            gl.LineWidth(2.5f);
            gl.Color(red, green, blue);
            gl.Begin(positions.Length == 3 ? OpenGL.GL_LINE_LOOP : OpenGL.GL_LINE_STRIP);
            foreach (var position in positions)
            {
                gl.Vertex(position.X, position.Y, position.Z);
            }
            gl.End();
        }

        gl.PointSize(10.0f);
        gl.Color(red, green, blue);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var position in positions)
        {
            gl.Vertex(position.X, position.Y, position.Z);
        }
        gl.End();
    }

    private void RaiseTeachingCaptureStateChanged() =>
        TeachingCaptureStateChanged?.Invoke(this, new TeachingCaptureStateChangedEventArgs(TeachingCaptureSnapshot));

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private void ApplyTeachingCaptureViewModelVerification(string[] args)
    {
        var verificationIndex = Array.IndexOf(args, "--verify-teaching-capture-viewmodel");
        if (verificationIndex < 0)
        {
            return;
        }

        if (verificationIndex + 1 >= args.Length
            || args[verificationIndex + 1].StartsWith("--", StringComparison.Ordinal))
        {
            SetSmokeFailure("Teaching-capture ViewModel verification requires a report path.");
            return;
        }

        if (!TeachingCaptureViewModelVerification.Verify(args[verificationIndex + 1], out var summary))
        {
            SetSmokeFailure(summary);
        }
    }
}
