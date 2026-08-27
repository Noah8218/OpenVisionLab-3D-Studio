using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private Point? teachingCapturePointerDownPosition;
    private bool teachingCaptureDragExceeded;
    private TeachingGridRectangleEditMode teachingGridRectangleEditMode;
    private TeachingGridRectangleEditMode teachingGridRectangleHoverMode;
    private ToolRecipeGridRectangle? teachingGridRectangleDragStart;
    private int teachingGridRectanglePointerStartRow;
    private int teachingGridRectanglePointerStartColumn;
    private Point teachingGridRectangleHeightPointerStart;
    private double teachingGridRectangleHeightOffsetStart;
    private readonly Dictionary<TeachingGridRectangleDisplayHeightKey, double> teachingGridRectangleAutomaticHeights = [];

    public event EventHandler<TeachingCaptureStateChangedEventArgs>? TeachingCaptureStateChanged;
    public event EventHandler<TeachingSelectionSelectedEventArgs>? TeachingSelectionSelected;
    public event EventHandler<TeachingRoiDisplayHeightChangedEventArgs>? TeachingRoiDisplayHeightChanged;

    public TeachingCaptureState TeachingCaptureSnapshot => viewModel.TeachingCaptureSnapshot;

    public bool BeginC3DTeachingCapture(TeachingCaptureRequest request, out string message)
        => BeginC3DTeachingCapture(request, initialSelection: null, out message);

    public bool BeginC3DTeachingCapture(
        TeachingCaptureRequest request,
        ToolRecipeSelection? initialSelection,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(request);
        var isTransformedHeightField = string.Equals(
            request.SourceBinding.Format,
            "TransformedHeightField",
            StringComparison.Ordinal);
        if (isTransformedHeightField)
        {
            if (regridHeightFieldRenderOutput is null)
            {
                message = "The owned Published TransformedHeightField must be visible before teaching capture.";
                return false;
            }
            var verification = ToolRecipeSelectionSourceBindingVerifier.Verify(
                regridHeightFieldRenderOutput,
                request.SourceBinding);
            if (!verification.IsCurrent)
            {
                message = verification.Message;
                return false;
            }
        }
        else if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            message = "A visible C3D source is required before teaching capture.";
            return false;
        }

        if (!isTransformedHeightField
            && (!string.Equals(request.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || request.SourceBinding.GridWidth != c3dSample!.Width
            || request.SourceBinding.GridHeight != c3dSample.Height))
        {
            message = "Teaching capture source format or grid dimensions do not match the loaded C3D source.";
            return false;
        }

        if (!IsSha256(request.SourceBinding.ContentSha256))
        {
            message = "Teaching capture requires a valid C3D source SHA-256 binding.";
            return false;
        }

        if (!isTransformedHeightField && !string.Equals(
                request.SourceBinding.ContentSha256,
                c3dSample!.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            message = "Teaching capture source SHA-256 does not match the loaded C3D bytes.";
            return false;
        }

        teachingCapturePointerDownPosition = null;
        teachingCaptureDragExceeded = false;
        ClearTeachingGridRectangleEdit();
        HideTeachingCaptureDragOverlay();
        IReadOnlyList<ToolRecipeSelectionPoint>? initialPoints = null;
        ToolRecipeGridCircle? initialGridCircle = null;
        ToolRecipeGridPolygon? initialGridPolygon = null;
        if (!isTransformedHeightField
            && initialSelection?.GridRectangle is { } initialRectangle)
        {
            if (!TryCreateC3DTeachingGridRectanglePoints(initialRectangle, out initialPoints, out message))
            {
                return false;
            }
        }
        else if (!isTransformedHeightField
                 && initialSelection?.GridCircle is { } circle)
        {
            if (!TryCreateC3DTeachingGridCirclePoints(circle, out initialPoints, out message))
            {
                return false;
            }
            initialGridCircle = circle;
        }
        else if (!isTransformedHeightField
                 && initialSelection?.GridPolygon is { } polygon)
        {
            if (!TryCreateC3DTeachingGridPolygonPoints(polygon, out initialPoints, out message))
            {
                return false;
            }
            initialGridPolygon = polygon;
        }
        if (!viewModel.BeginTeachingCapture(request, initialPoints, initialGridCircle, initialGridPolygon, out message))
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
        ClearTeachingGridRectangleEdit();
        HideTeachingCaptureDragOverlay();
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
        ClearTeachingGridRectangleEdit();
        HideTeachingCaptureDragOverlay();
        viewModel.ConfirmTeachingCaptureApplied();
        RaiseTeachingCaptureStateChanged();
        RenderNow();
    }

    public void SetAppliedTeachingSelections(IReadOnlyList<ToolRecipeSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        if (viewModel.AppliedTeachingSelections.SequenceEqual(selections))
        {
            return;
        }

        viewModel.SetAppliedTeachingSelections(selections);
        RaiseTeachingCaptureStateChanged();
        RenderNow();
    }

    public void SetRepeatPreviewTeachingSelections(IReadOnlyList<ToolRecipeSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        if (viewModel.RepeatPreviewTeachingSelections.SequenceEqual(selections))
        {
            return;
        }

        viewModel.SetRepeatPreviewTeachingSelections(selections);
        RenderNow();
    }

    public void SetCompletenessCellOverlays(
        IReadOnlyList<C3DCompletenessCellOverlay> overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        if (viewModel.CompletenessCellOverlays.SequenceEqual(overlays))
        {
            return;
        }

        viewModel.SetCompletenessCellOverlays(overlays);
        RenderNow();
    }

    public void SetSelectedCompletenessCellId(string? cellId)
    {
        viewModel.SetSelectedCompletenessCellId(cellId);
        RenderNow();
    }

    public void SetSelectedTeachingSelection(string? selectionId)
    {
        viewModel.SetSelectedTeachingSelection(selectionId);
        RenderNow();
    }

    private void DecreaseTeachingRoiDisplayHeight_Click(object sender, RoutedEventArgs e) =>
        AdjustTeachingRoiDisplayHeight(-GetTeachingRoiDisplayHeightStep(), "decrease button");

    private void IncreaseTeachingRoiDisplayHeight_Click(object sender, RoutedEventArgs e) =>
        AdjustTeachingRoiDisplayHeight(GetTeachingRoiDisplayHeightStep(), "increase button");

    private void ResetTeachingRoiDisplayHeight_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.SelectedTeachingGridRectangleVisible)
        {
            return;
        }

        viewModel.SelectedTeachingRoiDisplayHeightOffset = 0;
        viewModel.ViewerStatus =
            "Surface ROI overlay returned to its local Y position; ROI size, measurement, and recipe stay unchanged.";
        RaiseTeachingRoiDisplayHeightChanged("reset");
        RenderNow();
    }

    private void AdjustTeachingRoiDisplayHeight(double delta, string source)
    {
        if (!viewModel.SelectedTeachingGridRectangleVisible || !double.IsFinite(delta))
        {
            return;
        }

        viewModel.SelectedTeachingRoiDisplayHeightOffset += delta;
        viewModel.ViewerStatus =
            $"Surface ROI overlay Y position changed by {source}; ROI size, measurement, and recipe stay unchanged.";
        RaiseTeachingRoiDisplayHeightChanged(source);
        RenderNow();
    }

    private double GetTeachingRoiDisplayHeightStep() =>
        c3dSample is null
            ? 1.0
            : Math.Max((c3dSample.Max - c3dSample.Min) * 0.01, 10.0);

    private void TeachingRoiDisplayHeightOffset_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!viewModel.SelectedTeachingGridRectangleVisible)
        {
            return;
        }

        viewModel.ViewerStatus =
            "Surface ROI numeric overlay Y position accepted; ROI size, measurement, and recipe stay unchanged.";
        RaiseTeachingRoiDisplayHeightChanged("numeric input");
        RenderNow();
    }

    private void RaiseTeachingRoiDisplayHeightChanged(string source)
    {
        var selectionId = viewModel.TeachingCaptureSnapshot is
        {
            IsActive: true,
            Kind: ToolRecipeSelectionKinds.GridRectangle
        } capture
            ? capture.SelectionId
            : viewModel.SelectedTeachingSelectionId;
        if (selectionId is null)
        {
            return;
        }

        TeachingRoiDisplayHeightChanged?.Invoke(
            this,
            new TeachingRoiDisplayHeightChangedEventArgs(
                selectionId,
                viewModel.SelectedTeachingRoiAutomaticRawHeight,
                viewModel.SelectedTeachingRoiDisplayHeightOffset,
                viewModel.SelectedTeachingRoiEffectiveRawHeight,
                source));
    }

    public bool TrySetC3DTeachingGridRectangleCandidate(
        ToolRecipeGridRectangle rectangle,
        out string message) =>
        TrySetC3DTeachingGridRectangleCandidate(rectangle, renderNow: true, out message);

    private bool TrySetC3DTeachingGridRectangleCandidate(
        ToolRecipeGridRectangle rectangle,
        bool renderNow,
        out string message)
    {
        if (!TryCreateC3DTeachingGridRectanglePoints(rectangle, out var points, out message)
            || points is not [var first, var second]
            || !viewModel.TrySetTeachingGridRectangleCandidate(first, second, out message))
        {
            return false;
        }

        RaiseTeachingCaptureStateChanged();
        if (renderNow)
        {
            RenderNow();
        }
        return true;
    }

    public bool TrySetC3DTeachingGridCircleCandidate(
        ToolRecipeGridCircle circle,
        out string message)
    {
        if (!TryCreateC3DTeachingGridCirclePoints(circle, out var points, out message)
            || points is not [var center, var boundary]
            || !viewModel.TrySetTeachingGridCircleCandidate(circle, center, boundary, out message))
        {
            return false;
        }

        RaiseTeachingCaptureStateChanged();
        RenderNow();
        return true;
    }

    public bool TrySetC3DTeachingGridPolygonCandidate(
        ToolRecipeGridPolygon polygon,
        out string message)
    {
        if (!TryCreateC3DTeachingGridPolygonPoints(polygon, out var points, out message)
            || points is null
            || !viewModel.TrySetTeachingGridPolygonCandidate(polygon, points, out message))
        {
            return false;
        }

        RaiseTeachingCaptureStateChanged();
        RenderNow();
        return true;
    }

    private bool TryHandleC3DTeachingCapturePick(Point screenPoint)
    {
        if (!viewModel.IsTeachingCaptureActive)
        {
            return false;
        }

        if (string.Equals(
                viewModel.TeachingCaptureSourceBinding?.Format,
                "TransformedHeightField",
                StringComparison.Ordinal))
        {
            if (!TryPickRegridHeightFieldPoint(screenPoint, out var regridPoint))
            {
                const string message = "Teaching capture pick missed the visible TransformedHeightField grid.";
                viewModel.SetTeachingCaptureMessage(message);
                viewModel.PickCoordinate = "(none)";
                viewModel.ViewerStatus = message;
                RaiseTeachingCaptureStateChanged();
                return true;
            }

            var regridSelectionPoint = new ToolRecipeSelectionPoint(
                new ToolRecipeGridCellLocator("grid-cell", regridPoint.Row, regridPoint.Column),
                new ToolRecipeXyz(regridPoint.ReferencePosition.X, regridPoint.ReferencePosition.Y, regridPoint.ReferencePosition.Z),
                regridPoint.Height);
            viewModel.TryAddTeachingCapturePoint(regridSelectionPoint, out var regridCaptureMessage);
            viewModel.SelectedEntity = "TransformedHeightField Teaching Selection Candidate";
            viewModel.PickCoordinate = $"row {regridPoint.Row}, col {regridPoint.Column}, H {regridPoint.Height:G6}";
            viewModel.ViewerStatus = regridCaptureMessage;
            RaiseTeachingCaptureStateChanged();
            return true;
        }

        if (IsTeachingGridFootprintCapture)
        {
            if (!TryMapScreenToC3DGridFootprint(screenPoint, out var row, out var column))
            {
                const string message = "Surface ROI pick missed the C3D X/Z footprint.";
                viewModel.SetTeachingCaptureMessage(message);
                viewModel.PickCoordinate = "(none)";
                viewModel.ViewerStatus = message;
                RaiseTeachingCaptureStateChanged();
                return true;
            }

            var footprintSelectionPoint = CreateC3DTeachingSelectionPoint(row, column);
            viewModel.TryAddTeachingCapturePoint(footprintSelectionPoint, out var footprintCaptureMessage);
            viewModel.SelectedEntity = IsTeachingGridCircleCapture
                ? "Circular Surface ROI Candidate"
                : IsTeachingGridPolygonCapture
                    ? "Polygon Surface ROI Candidate"
                    : "Surface ROI Candidate";
            viewModel.PickCoordinate = $"X/column {column}, Z/row {row}";
            viewModel.ViewerStatus = footprintCaptureMessage;
            RaiseTeachingCaptureStateChanged();
            Viewport.Cursor = viewModel.TeachingCaptureSnapshot.CanApply ? Cursors.Arrow : Cursors.Cross;
            return true;
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
        ClearTeachingGridRectangleEdit();
        teachingOrientedBoxDraft = null;
        ClearTeachingOrientedBoxEdit();
        HideTeachingCaptureDragOverlay();
        viewModel.CancelTeachingCapture("Teaching capture cleared because the C3D source changed.");
        viewModel.SetAppliedTeachingSelections([]);
        viewModel.SetRepeatPreviewTeachingSelections([]);
        viewModel.ResetTeachingRoiDisplayHeights();
        teachingGridRectangleAutomaticHeights.Clear();
        viewModel.ClearWorkbenchHeightDifferenceEdge();
        viewModel.ClearWorkbenchTwoPointLine();
        viewModel.ClearWorkbenchThreePointPlane();
        viewModel.ClearWorkbenchDatumPlaneDeviation();
        viewModel.ClearWorkbenchLineFit();
        viewModel.ClearWorkbenchLineIntersection();
        viewModel.ClearWorkbenchLandmarkCorrespondence();
        viewModel.ClearWorkbenchAffineApply();
        ClearAffineApplyRenderData();
        ClearWorkbenchRegridHeightField();
        RaiseTeachingCaptureStateChanged();
    }

    private bool IsTeachingGridRectangleCapture =>
        viewModel.TeachingCaptureSnapshot is
        {
            IsActive: true,
            Kind: ToolRecipeSelectionKinds.GridRectangle
        };

    private bool IsTeachingGridCircleCapture =>
        viewModel.TeachingCaptureSnapshot is
        {
            IsActive: true,
            Kind: ToolRecipeSelectionKinds.GridCircle
        };

    private bool IsTeachingGridPolygonCapture =>
        viewModel.TeachingCaptureSnapshot is
        {
            IsActive: true,
            Kind: ToolRecipeSelectionKinds.GridPolygon
        };

    private bool IsTeachingGridFootprintCapture =>
        IsTeachingGridRectangleCapture || IsTeachingGridCircleCapture || IsTeachingGridPolygonCapture;

    private bool IsTeachingGridRectangleCandidateReview =>
        viewModel.TeachingCaptureSnapshot is
        {
            IsActive: true,
            Kind: ToolRecipeSelectionKinds.GridRectangle,
            CanApply: true
        } capture
        && capture.CapturedPointCount >= capture.RequiredPointCount;

    private bool TryCreateC3DTeachingGridRectanglePoints(
        ToolRecipeGridRectangle rectangle,
        out IReadOnlyList<ToolRecipeSelectionPoint>? points,
        out string message)
    {
        points = null;
        if (c3dSample is null
            || rectangle.Row < 0
            || rectangle.Column < 0
            || rectangle.RowCount <= 0
            || rectangle.ColumnCount <= 0
            || (long)rectangle.Row + rectangle.RowCount > c3dSample.Height
            || (long)rectangle.Column + rectangle.ColumnCount > c3dSample.Width)
        {
            message = "The Surface ROI must stay inside the loaded C3D source grid.";
            return false;
        }

        points =
        [
            CreateC3DTeachingSelectionPoint(rectangle.Row, rectangle.Column),
            CreateC3DTeachingSelectionPoint(
                rectangle.Row + rectangle.RowCount - 1,
                rectangle.Column + rectangle.ColumnCount - 1)
        ];
        message = string.Empty;
        return true;
    }

    private bool TryCreateC3DTeachingGridCirclePoints(
        ToolRecipeGridCircle circle,
        out IReadOnlyList<ToolRecipeSelectionPoint>? points,
        out string message)
    {
        points = null;
        if (c3dSample is null
            || ToolRecipeGridCircleGeometry.Validate(
                circle,
                c3dSample.Width,
                c3dSample.Height).Count > 0)
        {
            message = "The Circular ROI must stay inside the loaded C3D source grid.";
            return false;
        }

        var boundaryColumn = Math.Clamp(
            circle.CenterColumn + Math.Max(1, (int)Math.Floor(circle.Radius)),
            0,
            c3dSample.Width - 1);
        points =
        [
            CreateC3DTeachingSelectionPoint(circle.CenterRow, circle.CenterColumn),
            CreateC3DTeachingSelectionPoint(circle.CenterRow, boundaryColumn)
        ];
        message = string.Empty;
        return true;
    }

    private bool TryCreateC3DTeachingGridPolygonPoints(
        ToolRecipeGridPolygon polygon,
        out IReadOnlyList<ToolRecipeSelectionPoint>? points,
        out string message)
    {
        points = null;
        if (c3dSample is null
            || ToolRecipeGridPolygonGeometry.Validate(
                   polygon,
                   c3dSample.Width,
                   c3dSample.Height).Count > 0)
        {
            message = "The polygon ROI must stay finite, ordered, non-degenerate, and inside the loaded C3D source grid.";
            return false;
        }

        points = polygon.Vertices
            .Select(vertex => CreateC3DTeachingSelectionPoint(
                Math.Clamp((int)Math.Round(vertex.Row, MidpointRounding.AwayFromZero), 0, c3dSample.Height - 1),
                Math.Clamp((int)Math.Round(vertex.Column, MidpointRounding.AwayFromZero), 0, c3dSample.Width - 1)))
            .ToArray();
        message = string.Empty;
        return true;
    }

    private ToolRecipeSelectionPoint CreateC3DTeachingSelectionPoint(int row, int column)
    {
        var position = new Vector3(
            (column - (c3dSample!.Width - 1) / 2.0f) * c3dSample.HorizontalScale,
            0,
            (row - (c3dSample.Height - 1) / 2.0f) * c3dSample.HorizontalScale);
        return new ToolRecipeSelectionPoint(
            new ToolRecipeGridCellLocator("grid-cell", row, column),
            new ToolRecipeXyz(position.X, position.Y, position.Z),
            c3dSample.Mean);
    }

    private bool TrySelectAppliedTeachingGridRectangle(Point screenPoint)
    {
        if (viewModel.IsTeachingCaptureActive
            || c3dSample is null)
        {
            return false;
        }

        var selection = viewModel.AppliedTeachingSelections
            .Where(IsSelectionForCurrentC3DGrid)
            .Where(item => item.GridRectangle is { } rectangle
                && IsNearTeachingGridRectangleScreenBoundary(item.Id, rectangle, screenPoint))
            .OrderBy(item => item.GridRectangle!.RowCount * (long)item.GridRectangle.ColumnCount)
            .FirstOrDefault();
        if (selection is null)
        {
            return false;
        }

        viewModel.SetSelectedTeachingSelection(selection.Id);
        viewModel.SelectedEntity = $"Selected Surface ROI: {selection.Name}";
        viewModel.ViewerStatus = "Surface ROI selected. Use Replace ROI to move, resize, or edit numeric values.";
        TeachingSelectionSelected?.Invoke(this, new TeachingSelectionSelectedEventArgs(selection.Id));
        return true;
    }

    private bool IsNearTeachingGridRectangleScreenBoundary(
        string selectionId,
        ToolRecipeGridRectangle rectangle,
        Point screenPoint)
    {
        return TryGetTeachingGridRectangleScreenCorners(
                selectionId,
                rectangle,
                out var topLeft,
                out var topRight,
                out var bottomRight,
                out var bottomLeft)
            && Math.Min(
                Math.Min(
                    DistanceToLineSegment(screenPoint, topLeft, topRight),
                    DistanceToLineSegment(screenPoint, topRight, bottomRight)),
                Math.Min(
                    DistanceToLineSegment(screenPoint, bottomRight, bottomLeft),
                    DistanceToLineSegment(screenPoint, bottomLeft, topLeft))) <= 14.0;
    }

    private void UpdateTeachingCaptureDragOverlay(Point start, Point current)
    {
        var left = Math.Min(start.X, current.X);
        var top = Math.Min(start.Y, current.Y);
        Canvas.SetLeft(TeachingCaptureDragRectangle, left);
        Canvas.SetTop(TeachingCaptureDragRectangle, top);
        TeachingCaptureDragRectangle.Width = Math.Abs(current.X - start.X);
        TeachingCaptureDragRectangle.Height = Math.Abs(current.Y - start.Y);
        TeachingCaptureDragRectangle.Visibility = Visibility.Visible;
    }

    private void HideTeachingCaptureDragOverlay()
    {
        TeachingCaptureDragRectangle.Visibility = Visibility.Collapsed;
        TeachingCaptureDragRectangle.Width = 0;
        TeachingCaptureDragRectangle.Height = 0;
    }

    private bool TryBeginTeachingGridRectangleEdit(Point screenPoint)
    {
        if (!TryGetVisibleTeachingGridRectangle(out var selectionId, out var rectangle))
        {
            return false;
        }

        teachingGridRectangleEditMode = GetTeachingGridRectangleEditMode(screenPoint, selectionId, rectangle);
        if (teachingGridRectangleEditMode == TeachingGridRectangleEditMode.None)
        {
            return false;
        }

        if (teachingGridRectangleEditMode != TeachingGridRectangleEditMode.Height
            && !viewModel.IsTeachingCaptureActive)
        {
            teachingGridRectangleEditMode = TeachingGridRectangleEditMode.None;
            return false;
        }

        teachingGridRectangleDragStart = rectangle;
        if (teachingGridRectangleEditMode == TeachingGridRectangleEditMode.Height)
        {
            teachingGridRectangleHeightPointerStart = screenPoint;
            teachingGridRectangleHeightOffsetStart = viewModel.SelectedTeachingRoiDisplayHeightOffset;
        }
        else if (!TryMapScreenToC3DGridFootprint(
            screenPoint,
            GetTeachingGridRectangleDisplayRawHeight(selectionId, rectangle),
            out teachingGridRectanglePointerStartRow,
            out teachingGridRectanglePointerStartColumn))
        {
            ClearTeachingGridRectangleEdit();
            return false;
        }

        teachingCapturePointerDownPosition = screenPoint;
        teachingCaptureDragExceeded = false;
        teachingGridRectangleHoverMode = teachingGridRectangleEditMode;
        Viewport.Cursor = GetTeachingGridRectangleCursor(teachingGridRectangleEditMode);
        viewModel.ViewerStatus = teachingGridRectangleEditMode switch
        {
            TeachingGridRectangleEditMode.Height => "Moving the Surface ROI overlay plane on Y only; ROI size, measurement, and recipe stay unchanged.",
            TeachingGridRectangleEditMode.Move => "Moving Surface ROI candidate; Apply remains explicit.",
            _ => "Resizing Surface ROI candidate; Apply remains explicit."
        };
        return true;
    }

    private bool TryUpdateTeachingGridRectangleEdit(Point screenPoint)
    {
        if (teachingGridRectangleEditMode == TeachingGridRectangleEditMode.None
            || teachingGridRectangleDragStart is not { } original
            || c3dSample is null)
        {
            return false;
        }

        if (teachingGridRectangleEditMode == TeachingGridRectangleEditMode.Height)
        {
            var pointerDelta = screenPoint - teachingGridRectangleHeightPointerStart;
            viewModel.SelectedTeachingRoiDisplayHeightOffset =
                teachingGridRectangleHeightOffsetStart
                - pointerDelta.Y * GetTeachingHeightDragRawPerPixel();
            teachingCaptureDragExceeded = true;
            return true;
        }

        var selectionId = viewModel.TeachingCaptureSnapshot.SelectionId;
        if (!TryMapScreenToC3DGridFootprint(
            screenPoint,
            GetTeachingGridRectangleDisplayRawHeight(selectionId, original),
            out var pointRow,
            out var pointColumn))
        {
            return false;
        }

        ToolRecipeGridRectangle rectangle;
        if (teachingGridRectangleEditMode == TeachingGridRectangleEditMode.Move)
        {
            var row = Math.Clamp(
                original.Row + pointRow - teachingGridRectanglePointerStartRow,
                0,
                c3dSample.Height - original.RowCount);
            var column = Math.Clamp(
                original.Column + pointColumn - teachingGridRectanglePointerStartColumn,
                0,
                c3dSample.Width - original.ColumnCount);
            rectangle = original with { Row = row, Column = column };
        }
        else
        {
            var opposite = teachingGridRectangleEditMode switch
            {
                TeachingGridRectangleEditMode.TopLeft => (Row: original.Row + original.RowCount - 1, Column: original.Column + original.ColumnCount - 1),
                TeachingGridRectangleEditMode.TopRight => (Row: original.Row + original.RowCount - 1, Column: original.Column),
                TeachingGridRectangleEditMode.BottomLeft => (Row: original.Row, Column: original.Column + original.ColumnCount - 1),
                _ => (Row: original.Row, Column: original.Column)
            };
            var row = Math.Clamp(pointRow, 0, c3dSample.Height - 1);
            var column = Math.Clamp(pointColumn, 0, c3dSample.Width - 1);
            rectangle = new ToolRecipeGridRectangle(
                Math.Min(row, opposite.Row),
                Math.Min(column, opposite.Column),
                Math.Abs(row - opposite.Row) + 1,
                Math.Abs(column - opposite.Column) + 1);
        }

        if (!TrySetC3DTeachingGridRectangleCandidate(rectangle, renderNow: false, out _))
        {
            return false;
        }

        teachingCaptureDragExceeded = true;
        return true;
    }

    private void ClearTeachingGridRectangleEdit()
    {
        teachingGridRectangleEditMode = TeachingGridRectangleEditMode.None;
        teachingGridRectangleHoverMode = TeachingGridRectangleEditMode.None;
        teachingGridRectangleDragStart = null;
        teachingGridRectanglePointerStartRow = 0;
        teachingGridRectanglePointerStartColumn = 0;
        teachingGridRectangleHeightPointerStart = default;
        teachingGridRectangleHeightOffsetStart = 0;
        if (Viewport is not null)
        {
            Viewport.Cursor = viewModel.IsTeachingCaptureActive && !IsTeachingGridRectangleCandidateReview
                ? Cursors.Cross
                : Cursors.Arrow;
        }
    }

    private void UpdateTeachingGridRectangleHover(Point screenPoint)
    {
        var mode = TryGetVisibleTeachingGridRectangle(out var selectionId, out var rectangle)
            ? GetTeachingGridRectangleEditMode(screenPoint, selectionId, rectangle)
            : TeachingGridRectangleEditMode.None;
        if (mode != TeachingGridRectangleEditMode.Height && !viewModel.IsTeachingCaptureActive)
        {
            mode = TeachingGridRectangleEditMode.None;
        }

        var cursor = mode == TeachingGridRectangleEditMode.None
            ? viewModel.IsTeachingCaptureActive && !IsTeachingGridRectangleCandidateReview
                ? Cursors.Cross
                : Cursors.Arrow
            : GetTeachingGridRectangleCursor(mode);
        var status = mode switch
        {
            TeachingGridRectangleEditMode.Height => "Surface ROI height handle; drag vertically. Display only—measurement stays unchanged.",
            TeachingGridRectangleEditMode.Move => "Surface ROI move area; drag to move. Apply remains explicit.",
            TeachingGridRectangleEditMode.None => null,
            _ => "Surface ROI corner handle; drag to resize. Apply remains explicit."
        };
        if (mode == teachingGridRectangleHoverMode
            && Viewport.Cursor == cursor
            && (status is null || string.Equals(viewModel.ViewerStatus, status, StringComparison.Ordinal)))
        {
            return;
        }

        teachingGridRectangleHoverMode = mode;
        Viewport.Cursor = cursor;
        if (status is not null)
        {
            viewModel.ViewerStatus = status;
        }
    }

    private TeachingGridRectangleEditMode GetTeachingGridRectangleEditMode(
        Point screenPoint,
        string selectionId,
        ToolRecipeGridRectangle rectangle)
    {
        if (!TryGetTeachingGridRectangleScreenCorners(
                selectionId,
                rectangle,
                out var topLeft,
                out var topRight,
                out var bottomRight,
                out var bottomLeft))
        {
            return TeachingGridRectangleEditMode.None;
        }

        const double handleRadius = 18.0;
        var handleRadiusSquared = handleRadius * handleRadius;
        if (TryGetTeachingHeightHandleScreenPoint(rectangle, out var heightHandle)
            && (screenPoint - heightHandle).LengthSquared <= handleRadiusSquared)
        {
            return TeachingGridRectangleEditMode.Height;
        }

        var topLeftDistanceSquared = (screenPoint - topLeft).LengthSquared;
        var topRightDistanceSquared = (screenPoint - topRight).LengthSquared;
        var bottomLeftDistanceSquared = (screenPoint - bottomLeft).LengthSquared;
        var bottomRightDistanceSquared = (screenPoint - bottomRight).LengthSquared;
        var closestCornerDistanceSquared = Math.Min(
            Math.Min(topLeftDistanceSquared, topRightDistanceSquared),
            Math.Min(bottomLeftDistanceSquared, bottomRightDistanceSquared));
        const double moveHandleRadius = 14.0;
        if (TryGetTeachingGridRectangleCenterScreenPoint(selectionId, rectangle, out var centerHandle)
            && (screenPoint - centerHandle).LengthSquared <= moveHandleRadius * moveHandleRadius
            && (screenPoint - centerHandle).LengthSquared < closestCornerDistanceSquared)
        {
            return TeachingGridRectangleEditMode.Move;
        }
        if (topLeftDistanceSquared <= handleRadiusSquared)
        {
            return TeachingGridRectangleEditMode.TopLeft;
        }
        if (topRightDistanceSquared <= handleRadiusSquared)
        {
            return TeachingGridRectangleEditMode.TopRight;
        }
        if (bottomLeftDistanceSquared <= handleRadiusSquared)
        {
            return TeachingGridRectangleEditMode.BottomLeft;
        }
        if (bottomRightDistanceSquared <= handleRadiusSquared)
        {
            return TeachingGridRectangleEditMode.BottomRight;
        }

        return IsPointInsideConvexQuadrilateral(
            screenPoint,
            topLeft,
            topRight,
            bottomRight,
            bottomLeft)
            ? TeachingGridRectangleEditMode.Move
            : TeachingGridRectangleEditMode.None;
    }

    private bool TryGetTeachingGridRectangleScreenCorners(
        ToolRecipeGridRectangle rectangle,
        out Point topLeft,
        out Point topRight,
        out Point bottomRight,
        out Point bottomLeft)
    {
        var selectionId = viewModel.TeachingCaptureSnapshot is
        {
            IsActive: true,
            Kind: ToolRecipeSelectionKinds.GridRectangle
        } capture
            ? capture.SelectionId
            : viewModel.SelectedTeachingSelectionId ?? string.Empty;
        return TryGetTeachingGridRectangleScreenCorners(
            selectionId,
            rectangle,
            out topLeft,
            out topRight,
            out bottomRight,
            out bottomLeft);
    }

    private bool TryGetTeachingGridRectangleScreenCorners(
        string selectionId,
        ToolRecipeGridRectangle rectangle,
        out Point topLeft,
        out Point topRight,
        out Point bottomRight,
        out Point bottomLeft)
    {
        topLeft = topRight = bottomRight = bottomLeft = default;
        if (c3dSample is null)
        {
            return false;
        }

        var lastRow = rectangle.Row + rectangle.RowCount - 1;
        var lastColumn = rectangle.Column + rectangle.ColumnCount - 1;
        var rawHeight = GetTeachingGridRectangleDisplayRawHeight(selectionId, rectangle);
        return TryProjectWorldPositionToViewport(
                CreateC3DGridDisplayPosition(rectangle.Row, rectangle.Column, rawHeight),
                out topLeft)
            && TryProjectWorldPositionToViewport(
                CreateC3DGridDisplayPosition(rectangle.Row, lastColumn, rawHeight),
                out topRight)
            && TryProjectWorldPositionToViewport(
                CreateC3DGridDisplayPosition(lastRow, lastColumn, rawHeight),
                out bottomRight)
            && TryProjectWorldPositionToViewport(
                CreateC3DGridDisplayPosition(lastRow, rectangle.Column, rawHeight),
                out bottomLeft);
    }

    private bool TryGetTeachingHeightHandleScreenPoint(
        ToolRecipeGridRectangle rectangle,
        out Point screenPoint)
    {
        screenPoint = default;
        var selectionId = viewModel.TeachingCaptureSnapshot is
        {
            IsActive: true,
            Kind: ToolRecipeSelectionKinds.GridRectangle
        } capture
            ? capture.SelectionId
            : viewModel.SelectedTeachingSelectionId;
        if (selectionId is null)
        {
            return false;
        }

        if (!TryGetTeachingGridRectangleCenterScreenPoint(selectionId, rectangle, out var center))
        {
            return false;
        }

        var direction = center.Y >= 140.0 ? -1.0 : 1.0;
        screenPoint = new Point(center.X, center.Y + direction * 46.0);
        return true;
    }

    private bool TryGetTeachingGridRectangleCenterScreenPoint(
        string selectionId,
        ToolRecipeGridRectangle rectangle,
        out Point screenPoint)
    {
        screenPoint = default;
        if (c3dSample is null)
        {
            return false;
        }

        var centerRow = rectangle.Row + (rectangle.RowCount - 1) / 2.0;
        var centerColumn = rectangle.Column + (rectangle.ColumnCount - 1) / 2.0;
        var rawHeight = GetTeachingGridRectangleDisplayRawHeight(selectionId, rectangle);
        return TryProjectWorldPositionToViewport(
            CreateC3DGridDisplayPosition(centerRow, centerColumn, rawHeight),
            out screenPoint);
    }

    private bool TryMapScreenToC3DGridFootprint(
        Point screenPoint,
        out int row,
        out int column) =>
        TryMapScreenToC3DGridFootprint(screenPoint, c3dSample?.Mean ?? 0, out row, out column);

    private bool TryMapScreenToC3DGridFootprint(
        Point screenPoint,
        double rawHeight,
        out int row,
        out int column)
    {
        row = 0;
        column = 0;
        if (c3dSample is null
            || c3dSample.Width < 2
            || c3dSample.Height < 2
            || Viewport.ActualWidth <= 0
            || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var origin = CreateC3DGridDisplayPosition(0, 0, rawHeight);
        var rowSpan = CreateC3DGridDisplayPosition(c3dSample.Height - 1, 0, rawHeight) - origin;
        var columnSpan = CreateC3DGridDisplayPosition(0, c3dSample.Width - 1, rawHeight) - origin;
        var normal = Vector3.Cross(rowSpan, columnSpan);
        var ray = CreatePickRay(screenPoint);
        var denominator = Vector3.Dot(ray.direction, normal);
        if (normal.LengthSquared() < 0.0000001f || Math.Abs(denominator) < 0.000001f)
        {
            return false;
        }

        var distance = Vector3.Dot(origin - ray.origin, normal) / denominator;
        if (!float.IsFinite(distance) || distance < 0.0f)
        {
            return false;
        }

        var offset = ray.origin + ray.direction * distance - origin;
        var rowRow = Vector3.Dot(rowSpan, rowSpan);
        var columnColumn = Vector3.Dot(columnSpan, columnSpan);
        var rowColumn = Vector3.Dot(rowSpan, columnSpan);
        var determinant = rowRow * columnColumn - rowColumn * rowColumn;
        if (Math.Abs(determinant) < 0.0000001f)
        {
            return false;
        }

        var offsetRow = Vector3.Dot(offset, rowSpan);
        var offsetColumn = Vector3.Dot(offset, columnSpan);
        var rowFraction = (offsetRow * columnColumn - offsetColumn * rowColumn) / determinant;
        var columnFraction = (offsetColumn * rowRow - offsetRow * rowColumn) / determinant;
        row = Math.Clamp(
            (int)Math.Round(rowFraction * (c3dSample.Height - 1), MidpointRounding.AwayFromZero),
            0,
            c3dSample.Height - 1);
        column = Math.Clamp(
            (int)Math.Round(columnFraction * (c3dSample.Width - 1), MidpointRounding.AwayFromZero),
            0,
            c3dSample.Width - 1);
        return true;
    }

    private static Cursor GetTeachingGridRectangleCursor(TeachingGridRectangleEditMode mode) =>
        mode switch
        {
            TeachingGridRectangleEditMode.Height => Cursors.SizeNS,
            TeachingGridRectangleEditMode.Move => Cursors.SizeAll,
            TeachingGridRectangleEditMode.TopLeft or TeachingGridRectangleEditMode.BottomRight => Cursors.SizeNWSE,
            TeachingGridRectangleEditMode.TopRight or TeachingGridRectangleEditMode.BottomLeft => Cursors.SizeNESW,
            _ => Cursors.Cross
        };

    private static double DistanceToLineSegment(Point point, Point start, Point end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared;
        if (lengthSquared <= 0.000001)
        {
            return (point - start).Length;
        }

        var fromStart = point - start;
        var projection = Math.Clamp(
            (fromStart.X * segment.X + fromStart.Y * segment.Y) / lengthSquared,
            0.0,
            1.0);
        var nearest = start + segment * projection;
        return (point - nearest).Length;
    }

    private static bool IsPointInsideConvexQuadrilateral(
        Point point,
        Point first,
        Point second,
        Point third,
        Point fourth)
    {
        var signs = new[]
        {
            Cross(first, second, point),
            Cross(second, third, point),
            Cross(third, fourth, point),
            Cross(fourth, first, point)
        };
        return signs.All(value => value >= -0.001)
            || signs.All(value => value <= 0.001);
    }

    private static double Cross(Point start, Point end, Point point) =>
        (end.X - start.X) * (point.Y - start.Y)
        - (end.Y - start.Y) * (point.X - start.X);

    private bool TryGetTeachingGridRectangleCandidate(out ToolRecipeGridRectangle rectangle)
    {
        var capture = viewModel.TeachingCaptureSnapshot;
        if (capture is not
            {
                IsActive: true,
                Kind: ToolRecipeSelectionKinds.GridRectangle,
                Points: { Count: 2 }
            })
        {
            rectangle = default!;
            return false;
        }

        var first = capture.Points[0].Locator;
        var second = capture.Points[1].Locator;
        rectangle = new ToolRecipeGridRectangle(
            Math.Min(first.Row, second.Row),
            Math.Min(first.Column, second.Column),
            Math.Abs(second.Row - first.Row) + 1,
            Math.Abs(second.Column - first.Column) + 1);
        return true;
    }

    private bool TryGetVisibleTeachingGridRectangle(
        out string selectionId,
        out ToolRecipeGridRectangle rectangle)
    {
        if (TryGetTeachingGridRectangleCandidate(out rectangle))
        {
            selectionId = viewModel.TeachingCaptureSnapshot.SelectionId;
            return true;
        }

        var selected = viewModel.AppliedTeachingSelections.FirstOrDefault(selection =>
            string.Equals(selection.Id, viewModel.SelectedTeachingSelectionId, StringComparison.OrdinalIgnoreCase)
            && IsSelectionForCurrentC3DGrid(selection)
            && selection.GridRectangle is not null);
        if (selected?.GridRectangle is not { } appliedRectangle)
        {
            selectionId = string.Empty;
            rectangle = default!;
            return false;
        }

        selectionId = selected.Id;
        rectangle = appliedRectangle;
        return true;
    }

    private double GetTeachingGridRectangleDisplayRawHeight(
        string selectionId,
        ToolRecipeGridRectangle rectangle)
    {
        var automaticHeight = GetTeachingGridRectangleAutomaticRawHeight(rectangle);
        if (string.Equals(selectionId, viewModel.SelectedTeachingSelectionId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(selectionId, viewModel.TeachingCaptureSnapshot.SelectionId, StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SetSelectedTeachingRoiAutomaticRawHeight(automaticHeight);
        }

        return automaticHeight + viewModel.GetTeachingRoiDisplayHeightOffset(selectionId);
    }

    private double GetTeachingGridRectangleAutomaticRawHeight(ToolRecipeGridRectangle rectangle)
    {
        if (c3dSample is null)
        {
            return 0;
        }

        var key = new TeachingGridRectangleDisplayHeightKey(
            c3dSample.ContentSha256,
            rectangle.Row,
            rectangle.Column,
            rectangle.RowCount,
            rectangle.ColumnCount);
        if (teachingGridRectangleAutomaticHeights.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var lastRow = rectangle.Row + rectangle.RowCount - 1;
        var lastColumn = rectangle.Column + rectangle.ColumnCount - 1;
        var samples = c3dSample.Points
            .Where(point => point.Row >= rectangle.Row
                && point.Row <= lastRow
                && point.Column >= rectangle.Column
                && point.Column <= lastColumn
                && float.IsFinite(point.RawValue)
                && point.RawValue != 0)
            .Select(point => (double)point.RawValue)
            .Order()
            .ToArray();
        double height;
        if (samples.Length > 0)
        {
            var middle = samples.Length / 2;
            height = samples.Length % 2 == 0
                ? (samples[middle - 1] + samples[middle]) / 2.0
                : samples[middle];
        }
        else
        {
            var centerRow = rectangle.Row + (rectangle.RowCount - 1) / 2.0;
            var centerColumn = rectangle.Column + (rectangle.ColumnCount - 1) / 2.0;
            height = c3dSample.Points
                .Where(point => float.IsFinite(point.RawValue) && point.RawValue != 0)
                .OrderBy(point =>
                    Math.Abs(point.Row - centerRow)
                    + Math.Abs(point.Column - centerColumn))
                .Select(point => (double)point.RawValue)
                .FirstOrDefault(c3dSample.Mean);
        }

        if (teachingGridRectangleAutomaticHeights.Count >= 64)
        {
            teachingGridRectangleAutomaticHeights.Clear();
        }
        teachingGridRectangleAutomaticHeights[key] = height;
        return height;
    }

    private double GetTeachingHeightDragRawPerPixel() =>
        c3dSample is null
            ? 1.0
            : Math.Max((c3dSample.Max - c3dSample.Min) / 160.0, 1.0);

    private void DrawTeachingSelectionOverlays(OpenGL gl)
    {
        if (c3dSample is null)
        {
            TeachingRoiHeightHandleOverlay.Visibility = Visibility.Collapsed;
            TeachingOrientedBoxHandleOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        if (viewModel.C3DSampleVisible)
        {
            foreach (var selection in viewModel.RepeatPreviewTeachingSelections.Where(IsSelectionForCurrentC3DGrid))
            {
                var isReference = selection.Id.Contains(
                    ".reference-roi",
                    StringComparison.OrdinalIgnoreCase);
                DrawTeachingSelection(
                    gl,
                    selection,
                    isReference ? 0.05 : 1.00,
                    isReference ? 0.92 : 0.58,
                    isReference ? 0.88 : 0.10);
            }

            foreach (var selection in viewModel.AppliedTeachingSelections.Where(IsSelectionForCurrentC3DGrid))
            {
                var selected = string.Equals(
                    selection.Id,
                    viewModel.SelectedTeachingSelectionId,
                    StringComparison.OrdinalIgnoreCase);
                var authoredWhileEditing = selected && viewModel.IsTeachingCaptureActive;
                DrawTeachingSelection(
                    gl,
                    selection,
                    authoredWhileEditing ? 0.32 : selected ? 1.00 : 0.10,
                    authoredWhileEditing ? 0.72 : selected ? 0.82 : 0.90,
                    authoredWhileEditing ? 1.00 : selected ? 0.12 : 0.88,
                    selected && !viewModel.IsTeachingCaptureActive);
            }

            if (TryGetTeachingOrientedBoxDraft(out var orientedBoxDraft)
                && orientedBoxDraft.OrientedBox3D is { } orientedBox)
            {
                DrawTeachingOrientedBox(
                    gl,
                    orientedBox,
                    1.00,
                    0.78,
                    0.08,
                    showHandles: true);
            }

            DrawWorkbenchHeightDifferenceEdge(gl);
            DrawWorkbenchTwoPointLine(gl);
            DrawWorkbenchThreePointPlane(gl);
            DrawWorkbenchDatumPlaneDeviation(gl);
            DrawWorkbenchLineFit(gl);
            DrawWorkbenchLineIntersection(gl);
            DrawWorkbenchLandmarkCorrespondence(gl);
            DrawCompletenessCellOverlays(gl);
            DrawWorkbenchConnectedRegion(gl);

            var capture = viewModel.TeachingCaptureSnapshot;
            if (capture.IsActive)
            {
                DrawTeachingCaptureCandidate(gl, capture, 1.00, 0.82, 0.12);
            }
        }

        DrawWorkbenchAffineApply(gl);
        DrawWorkbenchRegridHeightField(gl);
        DrawRegridTeachingSelectionOverlays(gl);
        UpdateTeachingRoiHeightHandleOverlay();
        UpdateTeachingOrientedBoxHandleOverlay();

        gl.LineWidth(1.0f);
        gl.PointSize(1.0f);
    }

    private void DrawCompletenessCellOverlays(OpenGL gl)
    {
        foreach (var overlay in viewModel.CompletenessCellOverlays)
        {
            var (red, green, blue) = overlay.Status switch
            {
                ResultStatus.Pass => (0.12, 0.92, 0.36),
                ResultStatus.Fail => (1.00, 0.18, 0.16),
                _ => (1.00, 0.72, 0.12)
            };
            var isSelected = string.Equals(
                overlay.CellId,
                viewModel.SelectedCompletenessCellId,
                StringComparison.OrdinalIgnoreCase);
            if (isSelected)
            {
                DrawTeachingGridRectangle(
                    gl,
                    overlay.Region,
                    1.0,
                    1.0,
                    1.0,
                    showHandles: false,
                    overlay.OverlayId,
                    lineWidth: 7.0f);
            }
            DrawTeachingGridRectangle(
                gl,
                overlay.Region,
                red,
                green,
                blue,
                showHandles: false,
                overlay.OverlayId,
                lineWidth: isSelected ? 4.5f : 2.5f);
        }
    }

    private void UpdateTeachingRoiHeightHandleOverlay()
    {
        if (!TryGetVisibleTeachingGridRectangle(out var selectionId, out var rectangle)
            || !TryGetTeachingGridRectangleScreenCorners(
                selectionId,
                rectangle,
                out var topLeft,
                out var topRight,
                out var bottomRight,
                out var bottomLeft)
            || !TryGetTeachingHeightHandleScreenPoint(rectangle, out var handle))
        {
            TeachingRoiHeightHandleOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        var center = new Point(
            (topLeft.X + topRight.X + bottomRight.X + bottomLeft.X) * 0.25,
            (topLeft.Y + topRight.Y + bottomRight.Y + bottomLeft.Y) * 0.25);
        TeachingRoiHeightHandleLine.X1 = center.X;
        TeachingRoiHeightHandleLine.Y1 = center.Y;
        TeachingRoiHeightHandleLine.X2 = handle.X;
        TeachingRoiHeightHandleLine.Y2 = handle.Y;
        Canvas.SetLeft(TeachingRoiHeightHandleGrip, handle.X - 11.0);
        Canvas.SetTop(TeachingRoiHeightHandleGrip, handle.Y - 11.0);
        Canvas.SetLeft(TeachingRoiHeightHandleLabel, handle.X + 14.0);
        Canvas.SetTop(TeachingRoiHeightHandleLabel, handle.Y - 9.0);
        TeachingRoiHeightHandleOverlay.Visibility = Visibility.Visible;
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

    private void DrawWorkbenchThreePointPlane(OpenGL gl)
    {
        var output = viewModel.WorkbenchThreePointPlane;
        if (output is null || c3dSample is null) return;

        var anchor = CreateC3DGridDisplayPosition(output.AnchorZ, output.AnchorX, output.AnchorY);
        var second = CreateC3DGridDisplayPosition(output.SecondZ, output.SecondX, output.SecondY);
        var third = CreateC3DGridDisplayPosition(output.ThirdZ, output.ThirdX, output.ThirdY);
        var published = viewModel.IsWorkbenchThreePointPlanePublished;
        gl.Enable(OpenGL.GL_BLEND);
        gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
        gl.Color(0.95, 0.16, 0.68, 0.32);
        gl.Begin(OpenGL.GL_TRIANGLES);
        gl.Vertex(anchor.X, anchor.Y, anchor.Z);
        gl.Vertex(second.X, second.Y, second.Z);
        gl.Vertex(third.X, third.Y, third.Z);
        gl.End();
        gl.Disable(OpenGL.GL_BLEND);

        gl.LineWidth(3.0f);
        gl.Color(1.0, published ? 0.78 : 0.58, 0.12);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(anchor.X, anchor.Y, anchor.Z);
        gl.Vertex(second.X, second.Y, second.Z);
        gl.Vertex(third.X, third.Y, third.Z);
        gl.End();

        var normalDisplay = new Vector3(
            (float)(output.NormalX * c3dSample.HorizontalScale),
            (float)(output.NormalY * C3DHeightGrid.ViewerHeightScale),
            (float)(output.NormalZ * c3dSample.HorizontalScale));
        var normalLength = normalDisplay.Length();
        if (normalLength > 0.000001f)
        {
            var arrowLength = Math.Clamp(Math.Min(c3dSample.Width, c3dSample.Height) * c3dSample.HorizontalScale * 0.08f, 0.3f, 2.2f);
            var arrowEnd = anchor + Vector3.Normalize(normalDisplay) * arrowLength;
            var arrowDirection = Vector3.Normalize(arrowEnd - anchor);
            var arrowSide = Vector3.Cross(arrowDirection, Vector3.UnitY);
            if (arrowSide.LengthSquared() < 0.000001f) arrowSide = Vector3.Cross(arrowDirection, Vector3.UnitX);
            arrowSide = Vector3.Normalize(arrowSide);
            var arrowHeadBase = arrowEnd - arrowDirection * (arrowLength * 0.20f);
            var arrowHeadOffset = arrowSide * (arrowLength * 0.09f);
            gl.LineWidth(4.0f);
            gl.Color(1.0, 0.24, 0.70);
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(anchor.X, anchor.Y, anchor.Z);
            gl.Vertex(arrowEnd.X, arrowEnd.Y, arrowEnd.Z);
            gl.Vertex(arrowEnd.X, arrowEnd.Y, arrowEnd.Z);
            gl.Vertex((arrowHeadBase + arrowHeadOffset).X, (arrowHeadBase + arrowHeadOffset).Y, (arrowHeadBase + arrowHeadOffset).Z);
            gl.Vertex(arrowEnd.X, arrowEnd.Y, arrowEnd.Z);
            gl.Vertex((arrowHeadBase - arrowHeadOffset).X, (arrowHeadBase - arrowHeadOffset).Y, (arrowHeadBase - arrowHeadOffset).Z);
            gl.End();
        }

        gl.PointSize(12.0f);
        gl.Color(1.0, 0.86, 0.20);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Vertex(anchor.X, anchor.Y, anchor.Z);
        gl.Vertex(second.X, second.Y, second.Z);
        gl.Vertex(third.X, third.Y, third.Z);
        gl.End();
    }

    private void DrawWorkbenchDatumPlaneDeviation(OpenGL gl)
    {
        var plane = viewModel.WorkbenchDatumPlane;
        var selection = viewModel.WorkbenchDatumPlaneMeasurementSelection;
        var output = viewModel.WorkbenchDatumPlaneDeviation;
        if (plane is null || selection?.GridRectangle is not { } rectangle || output is null || c3dSample is null
            || rectangle.Row < 0 || rectangle.Column < 0
            || rectangle.Row > c3dSample.Height - rectangle.RowCount
            || rectangle.Column > c3dSample.Width - rectangle.ColumnCount)
        {
            return;
        }

        var anchor = CreateC3DGridDisplayPosition(plane.AnchorZ, plane.AnchorX, plane.AnchorY);
        var second = CreateC3DGridDisplayPosition(plane.SecondZ, plane.SecondX, plane.SecondY);
        var third = CreateC3DGridDisplayPosition(plane.ThirdZ, plane.ThirdX, plane.ThirdY);
        gl.Enable(OpenGL.GL_BLEND);
        gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
        gl.Color(0.08, 0.78, 0.92, 0.24);
        gl.Begin(OpenGL.GL_TRIANGLES);
        gl.Vertex(anchor.X, anchor.Y, anchor.Z);
        gl.Vertex(second.X, second.Y, second.Z);
        gl.Vertex(third.X, third.Y, third.Z);
        gl.End();
        gl.Disable(OpenGL.GL_BLEND);

        DrawTeachingGridRectangle(gl, rectangle, 1.0, viewModel.IsWorkbenchDatumPlaneDeviationPublished ? 0.78 : 0.48, 0.12);
        var residualScale = Math.Max(Math.Max(Math.Abs(output.MinimumRawHeightResidual), Math.Abs(output.MaximumRawHeightResidual)), 1e-12d);
        gl.PointSize(6.0f);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var sample in output.OverlaySamples)
        {
            var ratio = Math.Clamp(sample.Residual / residualScale, -1d, 1d);
            var red = ratio > 0d ? 0.35 + (0.65 * ratio) : 0.10;
            var blue = ratio < 0d ? 0.35 + (0.65 * -ratio) : 0.10;
            var green = 0.90 - (0.65 * Math.Abs(ratio));
            gl.Color(red, green, blue);
            var position = CreateC3DGridDisplayPosition(sample.Row, sample.Column, sample.RawHeight);
            gl.Vertex(position.X, position.Y, position.Z);
        }
        gl.End();

        var minimumSample = output.OverlaySamples.FirstOrDefault(sample => sample.Row == output.MinimumResidualRow && sample.Column == output.MinimumResidualColumn);
        var maximumSample = output.OverlaySamples.FirstOrDefault(sample => sample.Row == output.MaximumResidualRow && sample.Column == output.MaximumResidualColumn);
        if (minimumSample is not null || maximumSample is not null)
        {
            gl.PointSize(13.0f);
            gl.Begin(OpenGL.GL_POINTS);
            if (minimumSample is not null)
            {
                var minimum = CreateC3DGridDisplayPosition(minimumSample.Row, minimumSample.Column, minimumSample.RawHeight);
                gl.Color(0.10, 0.82, 1.0);
                gl.Vertex(minimum.X, minimum.Y, minimum.Z);
            }
            if (maximumSample is not null)
            {
                var maximum = CreateC3DGridDisplayPosition(maximumSample.Row, maximumSample.Column, maximumSample.RawHeight);
                gl.Color(1.0, 0.30, 0.22);
                gl.Vertex(maximum.X, maximum.Y, maximum.Z);
            }
            gl.End();
        }
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

    private void DrawTeachingSelection(
        OpenGL gl,
        ToolRecipeSelection selection,
        double red,
        double green,
        double blue,
        bool showHandles = false)
    {
        if (selection.GridRectangle is { } rectangle)
        {
            DrawTeachingGridRectangle(
                gl,
                rectangle,
                red,
                green,
                blue,
                showHandles,
                selection.Id);
        }

        if (selection.Points is { Count: > 0 } points)
        {
            DrawTeachingPointSet(gl, points, red, green, blue);
        }

        if (selection.OrientedBox3D is { } orientedBox)
        {
            DrawTeachingOrientedBox(
                gl,
                orientedBox,
                red,
                green,
                blue,
                showHandles);
        }

        if (selection.GridCircle is { } circle)
        {
            DrawTeachingGridCircle(gl, circle, red, green, blue, showHandles);
        }

        if (selection.GridPolygon is { } polygon)
        {
            DrawTeachingGridPolygon(gl, polygon, red, green, blue, showHandles);
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
                blue,
                showHandles: true,
                capture.SelectionId);
        }


        if (capture is { Kind: ToolRecipeSelectionKinds.GridCircle, GridCircle: { } circle })
        {
            DrawTeachingGridCircle(gl, circle, red, green, blue, showHandles: true);
        }

        if (capture is { Kind: ToolRecipeSelectionKinds.GridPolygon, GridPolygon: { } polygon })
        {
            DrawTeachingGridPolygon(gl, polygon, red, green, blue, showHandles: true);
        }

        if (capture.Kind is not (ToolRecipeSelectionKinds.GridRectangle or ToolRecipeSelectionKinds.GridCircle or ToolRecipeSelectionKinds.GridPolygon)
            && capture.Points.Count > 0)
        {
            DrawTeachingPointSet(gl, capture.Points, red, green, blue);
        }
    }

    private void DrawTeachingGridCircle(
        OpenGL gl,
        ToolRecipeGridCircle circle,
        double red,
        double green,
        double blue,
        bool showHandles)
    {
        if (c3dSample is null)
        {
            return;
        }

        gl.Disable(OpenGL.GL_DEPTH_TEST);
        gl.LineWidth(showHandles ? 5.0f : 3.0f);
        gl.Color(red, green, blue, showHandles ? 1.0 : 0.82);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        const int segmentCount = 72;
        for (var index = 0; index < segmentCount; index++)
        {
            var angle = index * Math.PI * 2.0 / segmentCount;
            var row = circle.CenterRow + Math.Sin(angle) * circle.Radius;
            var column = circle.CenterColumn + Math.Cos(angle) * circle.Radius;
            var point = CreateC3DGridDisplayPosition(row, column, c3dSample.Mean);
            gl.Vertex(point.X, point.Y, point.Z);
        }
        gl.End();

        if (showHandles)
        {
            var center = CreateC3DGridDisplayPosition(
                circle.CenterRow,
                circle.CenterColumn,
                c3dSample.Mean);
            gl.PointSize(10.0f);
            gl.Begin(OpenGL.GL_POINTS);
            gl.Vertex(center.X, center.Y, center.Z);
            gl.End();
        }
        gl.Enable(OpenGL.GL_DEPTH_TEST);
    }

    private void DrawTeachingGridPolygon(
        OpenGL gl,
        ToolRecipeGridPolygon polygon,
        double red,
        double green,
        double blue,
        bool showHandles)
    {
        if (c3dSample is null
            || polygon.Vertices is not { Count: >= ToolRecipeGridPolygonGeometry.MinimumVertexCount })
        {
            return;
        }

        gl.Disable(OpenGL.GL_DEPTH_TEST);
        gl.LineWidth(showHandles ? 5.0f : 3.0f);
        gl.Color(red, green, blue, showHandles ? 1.0 : 0.82);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        foreach (var vertex in polygon.Vertices)
        {
            var point = CreateC3DGridDisplayPosition(vertex.Row, vertex.Column, c3dSample.Mean);
            gl.Vertex(point.X, point.Y, point.Z);
        }
        gl.End();

        if (showHandles)
        {
            gl.PointSize(10.0f);
            gl.Begin(OpenGL.GL_POINTS);
            foreach (var vertex in polygon.Vertices)
            {
                var point = CreateC3DGridDisplayPosition(vertex.Row, vertex.Column, c3dSample.Mean);
                gl.Vertex(point.X, point.Y, point.Z);
            }
            gl.End();
        }
        gl.Enable(OpenGL.GL_DEPTH_TEST);
    }

    private void DrawTeachingGridRectangle(
        OpenGL gl,
        ToolRecipeGridRectangle rectangle,
        double red,
        double green,
        double blue,
        bool showHandles = false,
        string? teachingSelectionId = null,
        float? lineWidth = null)
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
        var rawHeight = teachingSelectionId is null
            ? c3dSample.Mean
            : GetTeachingGridRectangleDisplayRawHeight(teachingSelectionId, rectangle);
        var topLeft = CreateC3DGridDisplayPosition(rectangle.Row, rectangle.Column, rawHeight);
        var topRight = CreateC3DGridDisplayPosition(rectangle.Row, lastColumn, rawHeight);
        var bottomRight = CreateC3DGridDisplayPosition(lastRow, lastColumn, rawHeight);
        var bottomLeft = CreateC3DGridDisplayPosition(lastRow, rectangle.Column, rawHeight);

        gl.Disable(OpenGL.GL_DEPTH_TEST);
        if (showHandles && teachingSelectionId is not null)
        {
            var automaticRawHeight = GetTeachingGridRectangleAutomaticRawHeight(rectangle);
            var displayOffset = viewModel.GetTeachingRoiDisplayHeightOffset(teachingSelectionId);
            if (Math.Abs(displayOffset) > 0.000001)
            {
                DrawTeachingGridRectangleOverlayPositionGuide(
                    gl,
                    [
                        CreateC3DGridDisplayPosition(rectangle.Row, rectangle.Column, automaticRawHeight),
                        CreateC3DGridDisplayPosition(rectangle.Row, lastColumn, automaticRawHeight),
                        CreateC3DGridDisplayPosition(lastRow, lastColumn, automaticRawHeight),
                        CreateC3DGridDisplayPosition(lastRow, rectangle.Column, automaticRawHeight)
                    ],
                    [topLeft, topRight, bottomRight, bottomLeft]);
            }
        }

        if (showHandles)
        {
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.Color(red, green, blue, 0.12);
            gl.Begin(OpenGL.GL_QUADS);
            gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
            gl.Vertex(topRight.X, topRight.Y, topRight.Z);
            gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
            gl.Vertex(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
            gl.End();
            gl.Disable(OpenGL.GL_BLEND);
        }

        gl.LineWidth(lineWidth ?? (showHandles ? 4.0f : 2.5f));
        gl.Color(red, green, blue);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(topRight.X, topRight.Y, topRight.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.Vertex(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
        gl.End();

        if (!showHandles)
        {
            gl.Enable(OpenGL.GL_DEPTH_TEST);
            return;
        }

        var center = (topLeft + topRight + bottomRight + bottomLeft) * 0.25f;
        gl.PointSize(20.0f);
        gl.Color(0.08, 0.10, 0.13);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(topRight.X, topRight.Y, topRight.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.Vertex(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
        gl.Vertex(center.X, center.Y, center.Z);
        gl.End();

        gl.PointSize(16.0f);
        gl.Color(red, green, blue);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(topRight.X, topRight.Y, topRight.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.Vertex(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
        gl.End();

        gl.PointSize(10.0f);
        gl.Color(1.0, 1.0, 1.0);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Vertex(center.X, center.Y, center.Z);
        gl.End();
        gl.Enable(OpenGL.GL_DEPTH_TEST);
    }

    private static void DrawTeachingGridRectangleOverlayPositionGuide(
        OpenGL gl,
        IReadOnlyList<Vector3> surfaceCorners,
        IReadOnlyList<Vector3> overlayCorners)
    {
        gl.Enable(OpenGL.GL_BLEND);
        gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

        gl.LineWidth(2.0f);
        gl.Color(0.40, 0.90, 0.96, 0.78);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        foreach (var corner in surfaceCorners)
        {
            gl.Vertex(corner.X, corner.Y, corner.Z);
        }
        gl.End();

        gl.LineWidth(1.5f);
        gl.Color(1.0, 0.78, 0.12, 0.70);
        gl.Begin(OpenGL.GL_LINES);
        for (var cornerIndex = 0; cornerIndex < surfaceCorners.Count; cornerIndex++)
        {
            var surface = surfaceCorners[cornerIndex];
            var overlay = overlayCorners[cornerIndex];
            for (var dashIndex = 0; dashIndex < 8; dashIndex += 2)
            {
                var start = Vector3.Lerp(surface, overlay, dashIndex / 8.0f);
                var end = Vector3.Lerp(surface, overlay, (dashIndex + 1) / 8.0f);
                gl.Vertex(start.X, start.Y, start.Z);
                gl.Vertex(end.X, end.Y, end.Z);
            }
        }
        gl.End();
        gl.Disable(OpenGL.GL_BLEND);
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

    private enum TeachingGridRectangleEditMode
    {
        None,
        Height,
        Move,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private readonly record struct TeachingGridRectangleDisplayHeightKey(
        string SourceSha256,
        int Row,
        int Column,
        int RowCount,
        int ColumnCount);

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
