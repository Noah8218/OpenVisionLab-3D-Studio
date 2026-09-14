using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// The concrete result of the startup Run Record load callback. Keeping the
/// result typed makes the coordinator independent from the ViewModel's out
/// parameter and keeps warning policy at this boundary.
/// </summary>
internal readonly record struct ShellStartupRunRecordLoadResult(
    bool Succeeded,
    string Message);

/// <summary>
/// Explicit Shell composition callbacks used by <see cref="ShellStartupCoordinator"/>.
/// They are ports for the existing View/VM owners, not a service locator.
/// </summary>
internal sealed class ShellStartupCoordinatorCallbacks
{
    public required Action<ShellWorkspaceMode> SelectWorkspace { get; init; }
    public required Func<bool> IsResultsWorkspaceSelected { get; init; }
    public required Action<ResultsWorkspaceSection> SelectResultsSection { get; init; }
    public required Action<ShellInspectionTask> SelectInspectionTask { get; init; }
    public required Action<ShellStartupBottomPane> ActivateBottomPane { get; init; }
    public required Action<string, string, string> SetOutputCompareSlots { get; init; }
    public required Action<double> ApplyC3DSourceLoadProgress { get; init; }
    public required Func<ShellCommandLineArguments, ShellValidationSetSmokeState> ConfigureValidationSet { get; init; }
    public required Func<string, ShellStartupRunRecordLoadResult> TryLoadRunRecord { get; init; }
    public required Action<string> ReportRunRecordRestoreFailure { get; init; }
    public required Action<string?, bool> ApplyCalibration { get; init; }
    public required Func<ShellToolTeachingStartupRequest, ShellToolTeachingStartupResult> ConfigureToolTeaching { get; init; }
    public required Action<string> SetViewerSmokeFailure { get; init; }
    public required Action<ShellStartupConfigurationPlan> ApplyViewerProjection { get; init; }
}

/// <summary>
/// Owns the command-line startup application sequence. The coordinator knows
/// the order and preconditions of startup intent, while MainWindow supplies
/// concrete View/VM callbacks and keeps host lifecycle event handling.
/// </summary>
internal sealed class ShellStartupCoordinator
{
    private readonly ShellStartupCoordinatorCallbacks callbacks;

    public ShellStartupCoordinator(ShellStartupCoordinatorCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public static void ApplyLanguage(
        ShellStartupConfigurationPlan configuration,
        Action<OpenVisionLanguage> applyLanguage)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(applyLanguage);

        if (configuration.RequestedLanguage is { } language)
        {
            applyLanguage(language);
        }
    }

    public void ApplyWorkspaceAndResults(ShellStartupConfigurationPlan configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Workspace is { } workspace)
        {
            callbacks.SelectWorkspace(workspace);
        }

        ApplyResultsSection(configuration);
    }

    public void ApplyResultsSection(ShellStartupConfigurationPlan configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (callbacks.IsResultsWorkspaceSelected()
            && configuration.ResultsSection is { } resultsSection)
        {
            callbacks.SelectResultsSection(resultsSection);
        }
    }

    public void ApplyInspectionTask(ShellStartupConfigurationPlan configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.InspectionTask is { } inspectionTask)
        {
            callbacks.SelectInspectionTask(inspectionTask);
        }
    }

    public void ApplyCalibration(ShellCommandLineArguments commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        callbacks.ApplyCalibration(
            commandLine.GetValue("--calibration-study"),
            commandLine.HasFlag("--smoke-calibration-calculate"));
    }

    public void ApplyToolTeaching(ShellCommandLineArguments commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var result = callbacks.ConfigureToolTeaching(
            new ShellToolTeachingStartupRequest(
                commandLine.GetValue("--plane-flatness-live-a3-fixture"),
                commandLine.GetValue("--tool-teaching-recipe"),
                commandLine.GetValue("--tool-teaching-step")));
        if (result.SmokeFailure is { } failure)
        {
            callbacks.SetViewerSmokeFailure(failure);
        }
    }

    public void RestoreRunRecord(ShellCommandLineArguments commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var requestedRunRecord = commandLine.GetValue("--run-record");
        if (string.IsNullOrWhiteSpace(requestedRunRecord))
        {
            return;
        }

        var result = callbacks.TryLoadRunRecord(requestedRunRecord);
        if (!result.Succeeded)
        {
            callbacks.ReportRunRecordRestoreFailure(result.Message);
        }
    }

    public ShellValidationSetSmokeState ConfigureValidationSet(
        ShellCommandLineArguments commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return callbacks.ConfigureValidationSet(commandLine);
    }

    public void ApplyWorkbenchBottomPane(ShellStartupConfigurationPlan configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        callbacks.ActivateBottomPane(configuration.BottomPane);
    }

    public void ApplyOutputCompare(ShellStartupConfigurationPlan configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        callbacks.SetOutputCompareSlots(
            configuration.CompareSlotAArtifactId,
            configuration.CompareSlotBArtifactId,
            configuration.CompareSlotCArtifactId);
    }

    public void ApplyC3DSourceLoadProgress(ShellStartupConfigurationPlan configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.C3DSourceLoadProgress is { } progress)
        {
            callbacks.ApplyC3DSourceLoadProgress(progress);
        }
    }

    public void ApplyViewerProjection(ShellStartupConfigurationPlan configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        callbacks.ApplyViewerProjection(configuration);
    }
}
