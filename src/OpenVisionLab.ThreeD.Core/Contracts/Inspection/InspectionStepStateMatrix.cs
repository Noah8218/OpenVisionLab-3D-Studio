namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Shared lifecycle vocabulary for authored and executed inspection steps.
/// These states describe operator-relevant readiness, not a fabricated result.
/// </summary>
public enum InspectionStepState
{
    Empty,
    Incomplete,
    Stale,
    Ready,
    Running,
    Pass,
    Fail,
    Error
}

public sealed record InspectionStepStateDescriptor(
    InspectionStepState State,
    string Key,
    bool IsActionable,
    bool HasResultEvidence);

public static class InspectionStepStateMatrix
{
    public static IReadOnlyList<InspectionStepStateDescriptor> All { get; } =
    [
        new(InspectionStepState.Empty, "empty", true, false),
        new(InspectionStepState.Incomplete, "incomplete", true, false),
        new(InspectionStepState.Stale, "stale", true, false),
        new(InspectionStepState.Ready, "ready", true, false),
        new(InspectionStepState.Running, "running", false, false),
        new(InspectionStepState.Pass, "pass", false, true),
        new(InspectionStepState.Fail, "fail", false, true),
        new(InspectionStepState.Error, "error", true, false)
    ];

    public static InspectionStepState Classify(
        string? authoredState,
        ResultStatus? resultStatus = null)
    {
        if (resultStatus is ResultStatus.Pass) return InspectionStepState.Pass;
        if (resultStatus is ResultStatus.Fail) return InspectionStepState.Fail;
        if (resultStatus is ResultStatus.Error) return InspectionStepState.Error;

        var value = authoredState?.Trim() ?? string.Empty;
        if (value.Length == 0) return InspectionStepState.Empty;
        if (value.Contains("running", StringComparison.OrdinalIgnoreCase)) return InspectionStepState.Running;
        if (value.Contains("stale", StringComparison.OrdinalIgnoreCase)) return InspectionStepState.Stale;
        if (value.Contains("error", StringComparison.OrdinalIgnoreCase)) return InspectionStepState.Error;
        if (value.Contains("incomplete", StringComparison.OrdinalIgnoreCase)
            || value.Contains("needs correction", StringComparison.OrdinalIgnoreCase)
            || value.Contains("blocked", StringComparison.OrdinalIgnoreCase))
        {
            return InspectionStepState.Incomplete;
        }
        if (value.Contains("pass", StringComparison.OrdinalIgnoreCase)) return InspectionStepState.Pass;
        if (value.Contains("fail", StringComparison.OrdinalIgnoreCase)) return InspectionStepState.Fail;
        if (value.Contains("ready", StringComparison.OrdinalIgnoreCase)
            || value.Contains("published", StringComparison.OrdinalIgnoreCase))
        {
            return InspectionStepState.Ready;
        }
        if (value.Contains("pending", StringComparison.OrdinalIgnoreCase)
            || value.Contains("empty", StringComparison.OrdinalIgnoreCase)
            || value.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return InspectionStepState.Empty;
        }

        return InspectionStepState.Incomplete;
    }

    public static InspectionStepStateDescriptor Describe(
        string? authoredState,
        ResultStatus? resultStatus = null) =>
        All.Single(item => item.State == Classify(authoredState, resultStatus));
}
