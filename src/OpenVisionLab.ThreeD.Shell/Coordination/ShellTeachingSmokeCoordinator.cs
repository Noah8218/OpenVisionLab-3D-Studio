namespace OpenVisionLab.ThreeD.Shell.Coordination;

internal sealed record ShellTeachingSmokeRequest(
    string? TeachingSelectionMode,
    string? TeachingSelectionReportPath,
    bool PlaneFlatnessLiveA3,
    string? PlaneFlatnessReportPath,
    string? PlaneFlatnessSavePath,
    string? TeachingRecipeSavePath);

internal sealed class ShellTeachingSmokeCallbacks
{
    public required Func<string, string?, Task<bool>> RunTeachingSelection { get; init; }
    public required Func<string?, string?, Task<bool>> RunPlaneFlatnessLiveA3 { get; init; }
    public required Func<string, (bool Succeeded, string? Failure)> SaveTeachingRecipe { get; init; }
}

internal sealed record ShellTeachingSmokeResult(bool Succeeded, string? Failure);

/// <summary>
/// Owns the command-line Teaching Smoke order. Viewer pointer behavior,
/// Workbench state, and recipe serialization remain callback-owned.
/// </summary>
internal sealed class ShellTeachingSmokeCoordinator
{
    private readonly ShellTeachingSmokeCallbacks callbacks;

    public ShellTeachingSmokeCoordinator(ShellTeachingSmokeCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public async Task<ShellTeachingSmokeResult> RunAsync(
        ShellTeachingSmokeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TeachingSelectionMode is not null
            && !await callbacks.RunTeachingSelection(
                request.TeachingSelectionMode,
                request.TeachingSelectionReportPath))
        {
            return new ShellTeachingSmokeResult(false, null);
        }

        if (request.PlaneFlatnessLiveA3
            && !await callbacks.RunPlaneFlatnessLiveA3(
                request.PlaneFlatnessReportPath,
                request.PlaneFlatnessSavePath))
        {
            return new ShellTeachingSmokeResult(false, null);
        }

        if (request.TeachingRecipeSavePath is { } teachingRecipeSavePath)
        {
            var saveResult = callbacks.SaveTeachingRecipe(teachingRecipeSavePath);
            if (!saveResult.Succeeded)
            {
                return new ShellTeachingSmokeResult(false, saveResult.Failure);
            }
        }

        return new ShellTeachingSmokeResult(true, null);
    }
}
