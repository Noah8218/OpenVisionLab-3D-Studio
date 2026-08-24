using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private TeachingCaptureRequest? teachingCaptureRequest;
    private IReadOnlyList<ToolRecipeSelectionPoint> teachingCapturePoints = [];
    private ToolRecipeGridCircle? teachingCaptureGridCircle;
    private ToolRecipeGridPolygon? teachingCaptureGridPolygon;
    private IReadOnlyList<ToolRecipeSelection> appliedTeachingSelections = [];
    private IReadOnlyList<ToolRecipeSelection> repeatPreviewTeachingSelections = [];
    private IReadOnlyList<C3DCompletenessCellOverlay> completenessCellOverlays = [];
    private string? selectedCompletenessCellId;
    private string? selectedTeachingSelectionId;
    private string teachingCaptureMessage = "No active teaching capture.";
    private readonly Dictionary<string, double> teachingRoiDisplayHeightOffsets = new(StringComparer.OrdinalIgnoreCase);
    private double selectedTeachingRoiAutomaticRawHeight = double.NaN;

    public bool IsTeachingCaptureActive => teachingCaptureRequest is not null;
    internal ToolRecipeSelectionSourceBinding? TeachingCaptureSourceBinding => teachingCaptureRequest?.SourceBinding;

    public IReadOnlyList<ToolRecipeSelection> AppliedTeachingSelections => appliedTeachingSelections;
    public IReadOnlyList<ToolRecipeSelection> RepeatPreviewTeachingSelections =>
        repeatPreviewTeachingSelections;
    public IReadOnlyList<C3DCompletenessCellOverlay> CompletenessCellOverlays =>
        completenessCellOverlays;
    public string? SelectedCompletenessCellId => selectedCompletenessCellId;

    public string? SelectedTeachingSelectionId => selectedTeachingSelectionId;

    public bool SelectedTeachingGridRectangleVisible =>
        GetVisibleTeachingGridRectangle() is not null;

    public double SelectedTeachingRoiDisplayHeightOffset
    {
        get => GetTeachingRoiDisplayHeightOffset(GetVisibleTeachingSelectionId());
        set
        {
            var selectionId = GetVisibleTeachingSelectionId();
            if (selectionId is null || !double.IsFinite(value))
            {
                return;
            }

            var current = GetTeachingRoiDisplayHeightOffset(selectionId);
            if (Math.Abs(current - value) <= 1e-9)
            {
                return;
            }

            if (Math.Abs(value) <= 1e-9)
            {
                teachingRoiDisplayHeightOffsets.Remove(selectionId);
            }
            else
            {
                teachingRoiDisplayHeightOffsets[selectionId] = value;
            }

            OnPropertyChanged(nameof(SelectedTeachingRoiDisplayHeightOffset));
            OnPropertyChanged(nameof(SelectedTeachingRoiEffectiveRawHeight));
            OnPropertyChanged(nameof(SelectedTeachingRoiDisplayHeightSummary));
        }
    }

    public double SelectedTeachingRoiAutomaticRawHeight =>
        selectedTeachingRoiAutomaticRawHeight;

    public double SelectedTeachingRoiEffectiveRawHeight =>
        double.IsFinite(selectedTeachingRoiAutomaticRawHeight)
            ? selectedTeachingRoiAutomaticRawHeight + SelectedTeachingRoiDisplayHeightOffset
            : double.NaN;

    public string SelectedTeachingRoiDisplayHeightSummary =>
        double.IsFinite(selectedTeachingRoiAutomaticRawHeight)
            ? $"surface {selectedTeachingRoiAutomaticRawHeight:F3} → overlay {SelectedTeachingRoiEffectiveRawHeight:F3} | ΔY {SelectedTeachingRoiDisplayHeightOffset:+0.###;-0.###;0}"
            : $"surface → overlay | ΔY {SelectedTeachingRoiDisplayHeightOffset:+0.###;-0.###;0}";

    public string SelectedTeachingGridRectangleSummary
    {
        get
        {
            var rectangle = GetVisibleTeachingGridRectangle();
            return rectangle is null
                ? string.Empty
                : $"row {rectangle.Row}..{rectangle.Row + rectangle.RowCount - 1} | column {rectangle.Column}..{rectangle.Column + rectangle.ColumnCount - 1} | X=column, Z=row";
        }
    }

    public TeachingCaptureState TeachingCaptureSnapshot
    {
        get
        {
            var request = teachingCaptureRequest;
            return request is null
                ? new TeachingCaptureState(
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    [],
                    false,
                    false,
                    teachingCaptureMessage,
                    appliedTeachingSelections.Count)
                : new TeachingCaptureState(
                    true,
                    request.SelectionId,
                    request.SelectionName,
                    request.Kind,
                    request.RequiredPointCount,
                    teachingCapturePoints.ToArray(),
                    teachingCapturePoints.Count > 0,
                    IsTeachingCaptureCandidateValid(request, teachingCapturePoints),
                    teachingCaptureMessage,
                    appliedTeachingSelections.Count,
                    teachingCaptureGridCircle,
                    teachingCaptureGridPolygon);
        }
    }

    internal bool BeginTeachingCapture(TeachingCaptureRequest request, out string message)
        => BeginTeachingCapture(request, initialPoints: null, initialGridCircle: null, initialGridPolygon: null, out message);

    internal bool BeginTeachingCapture(
        TeachingCaptureRequest request,
        IReadOnlyList<ToolRecipeSelectionPoint>? initialPoints,
        out string message) =>
        BeginTeachingCapture(request, initialPoints, initialGridCircle: null, initialGridPolygon: null, out message);

    internal bool BeginTeachingCapture(
        TeachingCaptureRequest request,
        IReadOnlyList<ToolRecipeSelectionPoint>? initialPoints,
        ToolRecipeGridCircle? initialGridCircle,
        out string message)
        => BeginTeachingCapture(request, initialPoints, initialGridCircle, initialGridPolygon: null, out message);

    internal bool BeginTeachingCapture(
        TeachingCaptureRequest request,
        IReadOnlyList<ToolRecipeSelectionPoint>? initialPoints,
        ToolRecipeGridCircle? initialGridCircle,
        ToolRecipeGridPolygon? initialGridPolygon,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryValidateTeachingCaptureRequest(request, out message))
        {
            return false;
        }

        teachingCaptureRequest = request;
        teachingCapturePoints = initialPoints?.ToArray() ?? [];
        teachingCaptureGridCircle = initialGridCircle;
        teachingCaptureGridPolygon = initialGridPolygon;
        teachingCaptureMessage = IsTeachingCaptureCandidateValid(request, teachingCapturePoints)
            ? $"Edit started: {request.SelectionName}. Move, resize, or enter values, then Apply."
            : $"Capture started: {request.SelectionName}.";
        selectedTeachingSelectionId = request.SelectionId;
        RefreshTeachingCaptureState();
        message = teachingCaptureMessage;
        return true;
    }

    internal bool TryAddTeachingCapturePoint(ToolRecipeSelectionPoint point, out string message)
    {
        var request = teachingCaptureRequest;
        if (request is null)
        {
            message = "No active teaching capture.";
            return false;
        }

        if (request.Kind != ToolRecipeSelectionKinds.GridPolygon
            && teachingCapturePoints.Count >= request.RequiredPointCount)
        {
            teachingCaptureMessage = "The capture candidate is complete. Apply, undo, or cancel it.";
            RefreshTeachingCaptureState();
            message = teachingCaptureMessage;
            return false;
        }

        if (teachingCapturePoints.Any(existing =>
                existing.Locator.Row == point.Locator.Row
                && existing.Locator.Column == point.Locator.Column))
        {
            teachingCaptureMessage = $"Grid cell ({point.Locator.Row}, {point.Locator.Column}) is already captured.";
            RefreshTeachingCaptureState();
            message = teachingCaptureMessage;
            return false;
        }

        teachingCapturePoints = [.. teachingCapturePoints, point];
        if (request.Kind == ToolRecipeSelectionKinds.GridCircle
            && teachingCapturePoints is [var center, var boundary])
        {
            var deltaRow = boundary.Locator.Row - center.Locator.Row;
            var deltaColumn = boundary.Locator.Column - center.Locator.Column;
            teachingCaptureGridCircle = new ToolRecipeGridCircle(
                center.Locator.Row,
                center.Locator.Column,
                Math.Sqrt((double)deltaRow * deltaRow + (double)deltaColumn * deltaColumn));
        }
        else if (request.Kind == ToolRecipeSelectionKinds.GridPolygon)
        {
            teachingCaptureGridPolygon = new ToolRecipeGridPolygon(
                teachingCapturePoints
                    .Select(captured => new ToolRecipeGridPolygonVertex(
                        captured.Locator.Row,
                        captured.Locator.Column))
                    .ToArray());
        }
        var ready = IsTeachingCaptureCandidateValid(request, teachingCapturePoints);
        teachingCaptureMessage = ready
            ? $"Captured {teachingCapturePoints.Count}/{request.RequiredPointCount}; candidate ready to apply."
            : teachingCapturePoints.Count >= request.RequiredPointCount
                ? request.Kind == ToolRecipeSelectionKinds.GridPolygon
                    ? $"Captured {teachingCapturePoints.Count} polygon vertices; the outline is invalid. Edit, undo, or cancel."
                    : $"Captured {teachingCapturePoints.Count}/{request.RequiredPointCount}; points must be non-collinear. Undo the last point or cancel."
            : $"Captured {teachingCapturePoints.Count}/{request.RequiredPointCount}.";
        RefreshTeachingCaptureState();
        message = teachingCaptureMessage;
        return true;
    }

    internal bool UndoTeachingCapture()
    {
        if (teachingCaptureRequest is null || teachingCapturePoints.Count == 0)
        {
            return false;
        }

        teachingCapturePoints = teachingCapturePoints.Take(teachingCapturePoints.Count - 1).ToArray();
        teachingCaptureGridCircle = null;
        if (teachingCaptureRequest?.Kind == ToolRecipeSelectionKinds.GridPolygon)
        {
            teachingCaptureGridPolygon = new ToolRecipeGridPolygon(
                teachingCapturePoints
                    .Select(captured => new ToolRecipeGridPolygonVertex(
                        captured.Locator.Row,
                        captured.Locator.Column))
                    .ToArray());
        }
        teachingCaptureMessage = teachingCapturePoints.Count == 0
            ? "Last point removed; capture the first point."
            : $"Last point removed; {teachingCapturePoints.Count}/{teachingCaptureRequest!.RequiredPointCount} remain.";
        RefreshTeachingCaptureState();
        return true;
    }

    internal bool CancelTeachingCapture(string message = "Teaching capture cancelled.")
    {
        if (teachingCaptureRequest is null)
        {
            return false;
        }

        teachingCaptureRequest = null;
        teachingCapturePoints = [];
        teachingCaptureGridCircle = null;
        teachingCaptureGridPolygon = null;
        teachingCaptureMessage = message;
        RefreshTeachingCaptureState();
        return true;
    }

    internal void SetTeachingCaptureMessage(string message)
    {
        teachingCaptureMessage = message;
        RefreshTeachingCaptureState();
    }

    internal bool TryGetTeachingCaptureCandidate(out ToolRecipeSelection? selection, out string message)
    {
        selection = null;
        var request = teachingCaptureRequest;
        if (request is null)
        {
            message = "No active teaching capture.";
            return false;
        }

        if (!IsTeachingCaptureCandidateValid(request, teachingCapturePoints))
        {
            message = $"Capture needs a valid {request.RequiredPointCount}-point candidate before Apply.";
            return false;
        }

        ToolRecipeGridRectangle? rectangle = null;
        ToolRecipeGridCircle? gridCircle = null;
        ToolRecipeGridPolygon? gridPolygon = null;
        IReadOnlyList<ToolRecipeSelectionPoint>? points = null;
        if (request.Kind == ToolRecipeSelectionKinds.GridRectangle)
        {
            var first = teachingCapturePoints[0].Locator;
            var second = teachingCapturePoints[1].Locator;
            var row = Math.Min(first.Row, second.Row);
            var column = Math.Min(first.Column, second.Column);
            rectangle = new ToolRecipeGridRectangle(
                row,
                column,
                Math.Abs(second.Row - first.Row) + 1,
                Math.Abs(second.Column - first.Column) + 1);
        }
        else if (request.Kind == ToolRecipeSelectionKinds.GridCircle)
        {
            gridCircle = teachingCaptureGridCircle;
        }
        else if (request.Kind == ToolRecipeSelectionKinds.GridPolygon)
        {
            gridPolygon = teachingCaptureGridPolygon;
        }
        else
        {
            points = teachingCapturePoints.ToArray();
        }

        selection = new ToolRecipeSelection(
            request.SelectionId,
            request.SelectionName,
            request.Kind,
            request.RootSourceId,
            request.FrameId,
            request.SourceBinding,
            rectangle,
            points,
            null,
            GridCircle: gridCircle,
            GridPolygon: gridPolygon);
        message = $"Teaching selection candidate ready: {request.SelectionName}.";
        return true;
    }

    internal bool TrySetTeachingGridRectangleCandidate(
        ToolRecipeSelectionPoint first,
        ToolRecipeSelectionPoint second,
        out string message)
    {
        var request = teachingCaptureRequest;
        if (request is not
            {
                Kind: ToolRecipeSelectionKinds.GridRectangle,
                RequiredPointCount: 2
            })
        {
            message = "No active GridRectangle edit candidate.";
            return false;
        }

        teachingCapturePoints = [first, second];
        teachingCaptureMessage = "ROI edit candidate ready. Apply is still required; inspection has not run.";
        RefreshTeachingCaptureState();
        message = teachingCaptureMessage;
        return true;
    }

    internal bool TrySetTeachingGridCircleCandidate(
        ToolRecipeGridCircle circle,
        ToolRecipeSelectionPoint center,
        ToolRecipeSelectionPoint boundary,
        out string message)
    {
        var request = teachingCaptureRequest;
        if (request is not
            {
                Kind: ToolRecipeSelectionKinds.GridCircle,
                RequiredPointCount: 2
            })
        {
            message = "No active GridCircle edit candidate.";
            return false;
        }

        if (ToolRecipeGridCircleGeometry.Validate(
                circle,
                request.SourceBinding.GridWidth,
                request.SourceBinding.GridHeight).Count > 0)
        {
            message = "GridCircle center and radius must stay inside the bound source grid.";
            return false;
        }

        teachingCapturePoints = [center, boundary];
        teachingCaptureGridCircle = circle;
        teachingCaptureMessage = "Circular ROI edit candidate ready. Apply is still required; inspection has not run.";
        RefreshTeachingCaptureState();
        message = teachingCaptureMessage;
        return true;
    }

    internal bool TrySetTeachingGridPolygonCandidate(
        ToolRecipeGridPolygon polygon,
        IReadOnlyList<ToolRecipeSelectionPoint> points,
        out string message)
    {
        var request = teachingCaptureRequest;
        if (request is not
            {
                Kind: ToolRecipeSelectionKinds.GridPolygon,
                RequiredPointCount: 3
            })
        {
            message = "No active GridPolygon edit candidate.";
            return false;
        }

        if (polygon.Vertices is null
            || polygon.Vertices.Count != points.Count
            || polygon.Vertices.Count > ToolRecipeGridPolygonGeometry.MaximumVertexCount
            || ToolRecipeGridPolygonGeometry.Validate(
                   polygon,
                   request.SourceBinding.GridWidth,
                   request.SourceBinding.GridHeight).Count > 0)
        {
            message = "GridPolygon vertices must stay finite, ordered, unique, non-degenerate, and inside the bound source grid.";
            return false;
        }

        teachingCapturePoints = points.ToArray();
        teachingCaptureGridPolygon = polygon;
        teachingCaptureMessage = "Polygon edit candidate ready. Apply is still required; inspection has not run.";
        RefreshTeachingCaptureState();
        message = teachingCaptureMessage;
        return true;
    }

    internal void ConfirmTeachingCaptureApplied()
    {
        if (teachingCaptureRequest is null)
        {
            return;
        }

        teachingCaptureRequest = null;
        teachingCapturePoints = [];
        teachingCaptureGridCircle = null;
        teachingCaptureGridPolygon = null;
        teachingCaptureMessage = "Teaching selection applied to the recipe.";
        RefreshTeachingCaptureState();
    }

    internal void SetAppliedTeachingSelections(IReadOnlyList<ToolRecipeSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        appliedTeachingSelections = selections.ToArray();
        OnPropertyChanged(nameof(AppliedTeachingSelections));
        OnPropertyChanged(nameof(TeachingCaptureSnapshot));
        RefreshSelectedTeachingGridRectangleState();
    }

    internal void SetRepeatPreviewTeachingSelections(IReadOnlyList<ToolRecipeSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        repeatPreviewTeachingSelections = selections.ToArray();
        OnPropertyChanged(nameof(RepeatPreviewTeachingSelections));
    }

    internal void SetCompletenessCellOverlays(
        IReadOnlyList<C3DCompletenessCellOverlay> overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        completenessCellOverlays = overlays.ToArray();
        OnPropertyChanged(nameof(CompletenessCellOverlays));
    }

    internal void SetSelectedCompletenessCellId(string? cellId)
    {
        if (string.Equals(
            selectedCompletenessCellId,
            cellId,
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        selectedCompletenessCellId = cellId;
        OnPropertyChanged(nameof(SelectedCompletenessCellId));
    }

    internal void SetSelectedTeachingSelection(string? selectionId)
    {
        if (string.Equals(selectedTeachingSelectionId, selectionId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        selectedTeachingSelectionId = selectionId;
        OnPropertyChanged(nameof(SelectedTeachingSelectionId));
        RefreshSelectedTeachingGridRectangleState();
    }

    internal double GetTeachingRoiDisplayHeightOffset(string? selectionId) =>
        selectionId is not null
        && teachingRoiDisplayHeightOffsets.TryGetValue(selectionId, out var offset)
            ? offset
            : 0.0;

    internal void SetSelectedTeachingRoiAutomaticRawHeight(double value)
    {
        value = double.IsFinite(value) ? value : double.NaN;
        if ((double.IsNaN(selectedTeachingRoiAutomaticRawHeight) && double.IsNaN(value))
            || Math.Abs(selectedTeachingRoiAutomaticRawHeight - value) <= 1e-9)
        {
            return;
        }

        selectedTeachingRoiAutomaticRawHeight = value;
        OnPropertyChanged(nameof(SelectedTeachingRoiAutomaticRawHeight));
        OnPropertyChanged(nameof(SelectedTeachingRoiEffectiveRawHeight));
        OnPropertyChanged(nameof(SelectedTeachingRoiDisplayHeightSummary));
    }

    internal void ResetTeachingRoiDisplayHeights()
    {
        teachingRoiDisplayHeightOffsets.Clear();
        selectedTeachingRoiAutomaticRawHeight = double.NaN;
        OnPropertyChanged(nameof(SelectedTeachingRoiDisplayHeightOffset));
        OnPropertyChanged(nameof(SelectedTeachingRoiAutomaticRawHeight));
        OnPropertyChanged(nameof(SelectedTeachingRoiEffectiveRawHeight));
        OnPropertyChanged(nameof(SelectedTeachingRoiDisplayHeightSummary));
    }

    private static bool TryValidateTeachingCaptureRequest(TeachingCaptureRequest request, out string message)
    {
        if (string.IsNullOrWhiteSpace(request.SelectionId)
            || string.IsNullOrWhiteSpace(request.SelectionName)
            || string.IsNullOrWhiteSpace(request.RootSourceId)
            || string.IsNullOrWhiteSpace(request.FrameId))
        {
            message = "Teaching capture identity, source, and frame are required.";
            return false;
        }

        var validKindAndCount = request.Kind switch
        {
            ToolRecipeSelectionKinds.GridRectangle => request.RequiredPointCount == 2,
            ToolRecipeSelectionKinds.GridCircle => request.RequiredPointCount == 2,
            ToolRecipeSelectionKinds.GridPolygon => request.RequiredPointCount == 3,
            ToolRecipeSelectionKinds.PointSet => request.RequiredPointCount is 2 or 3,
            _ => false
        };
        if (!validKindAndCount)
        {
            message = "Viewer capture supports a two-corner rectangle, center-and-boundary circle, ordered polygon, or a two/three-point set.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool IsTeachingCaptureCandidateValid(
        TeachingCaptureRequest request,
        IReadOnlyList<ToolRecipeSelectionPoint> points)
    {
        if (request.Kind == ToolRecipeSelectionKinds.GridPolygon)
        {
            return points.Count >= request.RequiredPointCount
                && ToolRecipeGridPolygonGeometry.Validate(
                    teachingCaptureGridPolygon,
                    request.SourceBinding.GridWidth,
                    request.SourceBinding.GridHeight).Count == 0;
        }

        if (points.Count != request.RequiredPointCount)
        {
            return false;
        }

        if (request.Kind == ToolRecipeSelectionKinds.GridCircle)
        {
            return ToolRecipeGridCircleGeometry.Validate(
                teachingCaptureGridCircle,
                request.SourceBinding.GridWidth,
                request.SourceBinding.GridHeight).Count == 0;
        }

        if (request.Kind != ToolRecipeSelectionKinds.PointSet || request.RequiredPointCount != 3)
        {
            return true;
        }

        var a = points[0].CapturedPosition;
        var b = points[1].CapturedPosition;
        var c = points[2].CapturedPosition;
        var ab = (X: b.X - a.X, Y: b.Y - a.Y, Z: b.Z - a.Z);
        var ac = (X: c.X - a.X, Y: c.Y - a.Y, Z: c.Z - a.Z);
        var cross = (
            X: ab.Y * ac.Z - ab.Z * ac.Y,
            Y: ab.Z * ac.X - ab.X * ac.Z,
            Z: ab.X * ac.Y - ab.Y * ac.X);
        var abLengthSquared = ab.X * ab.X + ab.Y * ab.Y + ab.Z * ab.Z;
        var acLengthSquared = ac.X * ac.X + ac.Y * ac.Y + ac.Z * ac.Z;
        var crossLengthSquared = cross.X * cross.X + cross.Y * cross.Y + cross.Z * cross.Z;
        return abLengthSquared > 0.0
            && acLengthSquared > 0.0
            && crossLengthSquared > abLengthSquared * acLengthSquared * 1e-12;
    }

    private void RefreshTeachingCaptureState()
    {
        OnPropertyChanged(nameof(IsTeachingCaptureActive));
        OnPropertyChanged(nameof(TeachingCaptureSnapshot));
        RefreshSelectedTeachingGridRectangleState();
    }

    private ToolRecipeGridRectangle? GetVisibleTeachingGridRectangle()
    {
        if (teachingCaptureRequest is { Kind: ToolRecipeSelectionKinds.GridRectangle }
            && teachingCapturePoints.Count == 2)
        {
            var first = teachingCapturePoints[0].Locator;
            var second = teachingCapturePoints[1].Locator;
            return new ToolRecipeGridRectangle(
                Math.Min(first.Row, second.Row),
                Math.Min(first.Column, second.Column),
                Math.Abs(second.Row - first.Row) + 1,
                Math.Abs(second.Column - first.Column) + 1);
        }

        return appliedTeachingSelections.FirstOrDefault(selection =>
            string.Equals(selection.Id, selectedTeachingSelectionId, StringComparison.OrdinalIgnoreCase))?.GridRectangle;
    }

    private string? GetVisibleTeachingSelectionId() =>
        teachingCaptureRequest is { Kind: ToolRecipeSelectionKinds.GridRectangle }
            ? teachingCaptureRequest.SelectionId
            : selectedTeachingSelectionId;

    private void RefreshSelectedTeachingGridRectangleState()
    {
        OnPropertyChanged(nameof(SelectedTeachingGridRectangleVisible));
        OnPropertyChanged(nameof(SelectedTeachingGridRectangleSummary));
        OnPropertyChanged(nameof(SelectedTeachingRoiDisplayHeightOffset));
        OnPropertyChanged(nameof(SelectedTeachingRoiAutomaticRawHeight));
        OnPropertyChanged(nameof(SelectedTeachingRoiEffectiveRawHeight));
        OnPropertyChanged(nameof(SelectedTeachingRoiDisplayHeightSummary));
        fitRoiCommand.RaiseCanExecuteChanged();
    }
}
