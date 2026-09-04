using System.Diagnostics;
using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the non-pointer composition around Workbench interaction Smoke. The
/// existing Surface Match interaction owner still owns WPF interaction and native
/// pointer work; MainWindow supplies only layout/render and visible-text
/// callbacks.
/// </summary>
internal sealed record ShellWorkbenchInteractionSmokeRequest
{
    public bool SurfaceMatchExperimentPreview { get; init; }
    public bool CollectionNavigationFocusHover { get; init; }
    public bool CollectionDisabled { get; init; }
    public bool ExperimentFocusHover { get; init; }
    public bool CollectionPopup { get; init; }
    public string? CollectionPopupScreenshotPath { get; init; }
    public string? WorkbenchInteractionReportPath { get; init; }
    public string? SelectedToolId { get; init; }
}

/// <summary>
/// Coordinates the Workbench interaction Smoke request and timing evidence
/// without owning desktop interaction objects or pointer state.
/// </summary>
internal sealed class ShellWorkbenchInteractionSmokeCoordinator
{
    private readonly ToolWorkbenchViewModel workbench;

    public ShellWorkbenchInteractionSmokeCoordinator(ToolWorkbenchViewModel workbench)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
    }

    public async Task<string?> RunAsync(
        ShellWorkbenchInteractionSmokeRequest request,
        Action updateLayout,
        Func<Task> yieldRenderAsync,
        Func<Task<ShellSurfaceMatchInteractionSmokeResult>> runInteractionAsync,
        Func<IReadOnlyList<string>> getSelectedToolVisibleTextLayout)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(updateLayout);
        ArgumentNullException.ThrowIfNull(yieldRenderAsync);
        ArgumentNullException.ThrowIfNull(runInteractionAsync);
        ArgumentNullException.ThrowIfNull(getSelectedToolVisibleTextLayout);

        if (request.SurfaceMatchExperimentPreview
            && !await workbench.PreviewSelectedSurfaceMatchExperimentAsync())
        {
            return "Surface Match experiment Preview did not produce a temporary candidate.";
        }

        var workbenchUiApplyStarted = Stopwatch.GetTimestamp();
        updateLayout();
        await yieldRenderAsync();
        if (request.CollectionNavigationFocusHover
            || request.CollectionDisabled
            || request.ExperimentFocusHover
            || request.CollectionPopup)
        {
            var interaction = await runInteractionAsync();
            if (!interaction.Succeeded)
            {
                return interaction.Failure!;
            }
        }

        var workbenchUiApplyMilliseconds =
            Stopwatch.GetElapsedTime(workbenchUiApplyStarted).TotalMilliseconds;
        if (request.WorkbenchInteractionReportPath is not null)
        {
            WriteInteractionReport(
                request,
                workbenchUiApplyMilliseconds,
                getSelectedToolVisibleTextLayout());
        }

        return null;
    }

    private void WriteInteractionReport(
        ShellWorkbenchInteractionSmokeRequest request,
        double workbenchUiApplyMilliseconds,
        IReadOnlyList<string> visibleTextLayout)
    {
        var fullReportPath = Path.GetFullPath(request.WorkbenchInteractionReportPath!);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        var reportLines = new List<string>
        {
            "OpenVisionLab 3D Workbench interaction timing",
            "Boundary|Local Release EXE smoke timing; this is not a broad hardware benchmark.",
            $"Tool|id={request.SelectedToolId ?? "(none)"}|selected={workbench.SelectedTool?.Id ?? "(none)"}|step={workbench.SelectedPipelineStep?.Id ?? "(none)"}",
            $"Timing|toolSelectionMs={workbench.LastToolSelectionMilliseconds:F3}|toolAddMs={workbench.LastToolAddMilliseconds:F3}|stepSelectionMs={workbench.LastStepSelectionMilliseconds:F3}|uiApplyMs={workbenchUiApplyMilliseconds:F3}",
            $"RecipeRefresh|totalMs={workbench.LastRecipeRefreshMilliseconds:F3}|validationMs={workbench.LastRecipeValidationMilliseconds:F3}|entityRebuildMs={workbench.LastRecipeEntityRebuildMilliseconds:F3}|executionStateMs={workbench.LastRecipeExecutionStateMilliseconds:F3}|notificationMs={workbench.LastRecipeNotificationMilliseconds:F3}",
            $"Budget|toolSelection50ms={workbench.LastToolSelectionMilliseconds <= 50.0}|toolAdd150ms={workbench.LastToolAddMilliseconds <= 150.0}|stepSelection150ms={workbench.LastStepSelectionMilliseconds <= 150.0}|uiApply150ms={workbenchUiApplyMilliseconds <= 150.0}",
            $"Recipe|steps={workbench.PipelineSteps.Count}|state={workbench.SelectedPipelineStep?.State ?? "(none)"}|publishAvailable={workbench.PublishSelectedStepCommand.CanExecute(null)}"
        };
        reportLines.AddRange(visibleTextLayout);
        File.WriteAllLines(fullReportPath, reportLines);
    }
}
