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
    int AppliedSelectionCount,
    ToolRecipeGridCircle? GridCircle = null)
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

public sealed class TeachingSelectionSelectedEventArgs(string selectionId) : EventArgs
{
    public string SelectionId { get; } = selectionId;
}

public sealed class TeachingRoiDisplayHeightChangedEventArgs(
    string selectionId,
    double automaticRawHeight,
    double offset,
    double effectiveRawHeight,
    string source) : EventArgs
{
    public string SelectionId { get; } = selectionId;

    public double AutomaticRawHeight { get; } = automaticRawHeight;

    public double Offset { get; } = offset;

    public double EffectiveRawHeight { get; } = effectiveRawHeight;

    public string Source { get; } = source;
}

public sealed class TeachingOrientedBox3DDraftChangedEventArgs(
    ToolRecipeSelection selection,
    string source) : EventArgs
{
    public ToolRecipeSelection Selection { get; } = selection;

    public string Source { get; } = source;
}
