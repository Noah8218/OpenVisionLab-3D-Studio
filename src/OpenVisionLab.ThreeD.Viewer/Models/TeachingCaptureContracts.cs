using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.Models;

public sealed record TeachingCaptureRequest(
    string SelectionId,
    string SelectionName,
    string Kind,
    int RequiredPointCount,
    string RootSourceId,
    string FrameId,
    ToolRecipeSelectionSourceBinding SourceBinding);

public sealed record TeachingCaptureState(
    bool IsActive,
    string SelectionId,
    string SelectionName,
    string Kind,
    int RequiredPointCount,
    IReadOnlyList<ToolRecipeSelectionPoint> Points,
    bool CanUndo,
    bool CanApply,
    string Message,
    int AppliedSelectionCount)
{
    public int CapturedPointCount => Points.Count;

    public string ProgressText => !IsActive
        ? Message
        : CanApply
            ? $"Capture: {SelectionName} | {Kind} | {CapturedPointCount}/{RequiredPointCount} ready"
            : CapturedPointCount >= RequiredPointCount
                ? $"Capture: {SelectionName} | {Kind} | {CapturedPointCount}/{RequiredPointCount} invalid; undo or cancel"
            : $"Capture: {SelectionName} | {Kind} | pick {CapturedPointCount + 1} of {RequiredPointCount}";
}

public sealed class TeachingCaptureStateChangedEventArgs(TeachingCaptureState state) : EventArgs
{
    public TeachingCaptureState State { get; } = state;
}
