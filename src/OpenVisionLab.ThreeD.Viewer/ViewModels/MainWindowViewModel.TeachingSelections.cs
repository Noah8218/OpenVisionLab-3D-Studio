using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private TeachingCaptureRequest? teachingCaptureRequest;
    private IReadOnlyList<ToolRecipeSelectionPoint> teachingCapturePoints = [];
    private IReadOnlyList<ToolRecipeSelection> appliedTeachingSelections = [];
    private string teachingCaptureMessage = "No active teaching capture.";

    public bool IsTeachingCaptureActive => teachingCaptureRequest is not null;

    public IReadOnlyList<ToolRecipeSelection> AppliedTeachingSelections => appliedTeachingSelections;

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
                    appliedTeachingSelections.Count);
        }
    }

    internal bool BeginTeachingCapture(TeachingCaptureRequest request, out string message)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryValidateTeachingCaptureRequest(request, out message))
        {
            return false;
        }

        teachingCaptureRequest = request;
        teachingCapturePoints = [];
        teachingCaptureMessage = $"Capture started: {request.SelectionName}.";
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

        if (teachingCapturePoints.Count >= request.RequiredPointCount)
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
        var ready = IsTeachingCaptureCandidateValid(request, teachingCapturePoints);
        teachingCaptureMessage = ready
            ? $"Captured {teachingCapturePoints.Count}/{request.RequiredPointCount}; candidate ready to apply."
            : teachingCapturePoints.Count >= request.RequiredPointCount
                ? $"Captured {teachingCapturePoints.Count}/{request.RequiredPointCount}; points must be non-collinear. Undo the last point or cancel."
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
        teachingCaptureMessage = teachingCapturePoints.Count == 0
            ? "Last point removed; capture the first point."
            : $"Last point removed; {teachingCapturePoints.Count}/{teachingCaptureRequest.RequiredPointCount} remain.";
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
            null);
        message = $"Teaching selection candidate ready: {request.SelectionName}.";
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
        teachingCaptureMessage = "Teaching selection applied to the recipe.";
        RefreshTeachingCaptureState();
    }

    internal void SetAppliedTeachingSelections(IReadOnlyList<ToolRecipeSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        appliedTeachingSelections = selections.ToArray();
        OnPropertyChanged(nameof(AppliedTeachingSelections));
        OnPropertyChanged(nameof(TeachingCaptureSnapshot));
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
            ToolRecipeSelectionKinds.PointSet => request.RequiredPointCount is 2 or 3,
            _ => false
        };
        if (!validKindAndCount)
        {
            message = "Viewer capture supports a two-corner grid rectangle or a two/three-point set.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool IsTeachingCaptureCandidateValid(
        TeachingCaptureRequest request,
        IReadOnlyList<ToolRecipeSelectionPoint> points)
    {
        if (points.Count != request.RequiredPointCount)
        {
            return false;
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
    }
}
