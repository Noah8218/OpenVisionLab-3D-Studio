using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed record ToolWorkbenchSelectedStepExecutionRoute(
    Func<Task<bool>> PreviewAsync,
    Func<bool> CanPreview,
    Action Publish,
    Func<bool> CanPublish,
    Action Cancel,
    Func<bool> IsRunning,
    Action RefreshState);

/// <summary>
/// Routes the selected step's explicit Preview, Publish, Cancel, and refresh
/// operations to the established tool-family execution owner.
/// </summary>
internal sealed class ToolWorkbenchSelectedStepExecutionOwner
{
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedStep;
    private readonly IReadOnlyDictionary<string, ToolWorkbenchSelectedStepExecutionRoute> routes;

    public ToolWorkbenchSelectedStepExecutionOwner(
        Func<ToolWorkbenchPipelineStepItem?> getSelectedStep,
        IReadOnlyDictionary<string, ToolWorkbenchSelectedStepExecutionRoute> routes)
    {
        this.getSelectedStep = getSelectedStep;
        this.routes = routes;

        PreviewCommand = new RelayCommand(
            _ => _ = PreviewAsync(),
            _ => CanPreview());
        PublishCommand = new RelayCommand(
            _ => Publish(),
            _ => CanPublish());
        CancelCommand = new RelayCommand(
            _ => Cancel(),
            _ => IsRunning);
    }

    public RelayCommand PreviewCommand { get; }
    public RelayCommand PublishCommand { get; }
    public RelayCommand CancelCommand { get; }

    public bool IsRunning => TryGetCurrentRoute(out var route) && route.IsRunning();

    public Task<bool> PreviewAsync()
    {
        if (!CanPreview() || !TryGetCurrentRoute(out var route))
        {
            return Task.FromResult(false);
        }

        return route.PreviewAsync();
    }

    public bool CanPreview() =>
        getSelectedStep() is { OutputEnabled: true }
        && TryGetCurrentRoute(out var route)
        && route.CanPreview();

    public void Publish()
    {
        if (CanPublish() && TryGetCurrentRoute(out var route))
        {
            route.Publish();
        }
    }

    public bool CanPublish() =>
        getSelectedStep() is { OutputEnabled: true }
        && TryGetCurrentRoute(out var route)
        && route.CanPublish();

    public void Cancel()
    {
        if (TryGetCurrentRoute(out var route))
        {
            route.Cancel();
        }
    }

    public void RefreshSelectedStepState()
    {
        if (getSelectedStep() is not { } step)
        {
            RefreshCommandStates();
            return;
        }

        if (!step.OutputEnabled)
        {
            step.State = "Disabled";
            RefreshCommandStates();
            return;
        }

        if (TryGetRoute(step.ToolId, out var route))
        {
            route.RefreshState();
        }

        RefreshCommandStates();
    }

    public void RefreshCommandStates()
    {
        PreviewCommand.RaiseCanExecuteChanged();
        PublishCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    private bool TryGetCurrentRoute(out ToolWorkbenchSelectedStepExecutionRoute route)
    {
        var step = getSelectedStep();
        if (step is null)
        {
            route = null!;
            return false;
        }

        return TryGetRoute(step.ToolId, out route);
    }

    private bool TryGetRoute(
        string toolId,
        out ToolWorkbenchSelectedStepExecutionRoute route) =>
        routes.TryGetValue(toolId, out route!);
}
