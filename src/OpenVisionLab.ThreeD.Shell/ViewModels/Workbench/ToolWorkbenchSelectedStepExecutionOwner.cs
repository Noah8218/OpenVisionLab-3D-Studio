using System;
using System.Collections.Generic;
using System.Threading;
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
internal sealed class ToolWorkbenchSelectedStepExecutionOwner : IDisposable
{
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedStep;
    private readonly IReadOnlyDictionary<string, ToolWorkbenchSelectedStepExecutionRoute> routes;
    private readonly Action<string, string> appendLog;
    private Task<bool>? commandPreviewTask;
    private Task? commandPreviewObservationTask;
    private int commandPreviewInFlight;
    private int disposalState;

    public ToolWorkbenchSelectedStepExecutionOwner(
        Func<ToolWorkbenchPipelineStepItem?> getSelectedStep,
        IReadOnlyDictionary<string, ToolWorkbenchSelectedStepExecutionRoute> routes,
        Action<string, string> appendLog)
    {
        this.getSelectedStep = getSelectedStep ?? throw new ArgumentNullException(nameof(getSelectedStep));
        this.routes = routes ?? throw new ArgumentNullException(nameof(routes));
        this.appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));

        PreviewCommand = new RelayCommand(
            _ => StartCommandPreview(),
            _ => CanStartCommandPreview());
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

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool IsRunning => !IsDisposed && TryGetCurrentRoute(out var route) && route.IsRunning();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref commandPreviewTask, null);
        Volatile.Write(ref commandPreviewObservationTask, null);
        Interlocked.Exchange(ref commandPreviewInFlight, 0);
    }

    public Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview() || !TryGetCurrentRoute(out var route))
        {
            return Task.FromResult(false);
        }

        return route.PreviewAsync();
    }

    public bool CanPreview() =>
        !IsDisposed
        && getSelectedStep() is { OutputEnabled: true }
        && TryGetCurrentRoute(out var route)
        && route.CanPreview();

    public void Publish()
    {
        if (!IsDisposed && CanPublish() && TryGetCurrentRoute(out var route))
        {
            route.Publish();
        }
    }

    public bool CanPublish() =>
        !IsDisposed
        && getSelectedStep() is { OutputEnabled: true }
        && TryGetCurrentRoute(out var route)
        && route.CanPublish();

    public void Cancel()
    {
        if (!IsDisposed && TryGetCurrentRoute(out var route))
        {
            route.Cancel();
        }
    }

    public void RefreshSelectedStepState()
    {
        if (IsDisposed)
        {
            return;
        }

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
        if (IsDisposed)
        {
            return;
        }

        PreviewCommand.RaiseCanExecuteChanged();
        PublishCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    private void StartCommandPreview()
    {
        if (IsDisposed
            || Interlocked.CompareExchange(ref commandPreviewInFlight, 1, 0) != 0)
        {
            return;
        }

        if (Volatile.Read(ref commandPreviewObservationTask) is { IsCompleted: true })
        {
            Volatile.Write(ref commandPreviewObservationTask, null);
        }

        RefreshCommandStates();

        Task<bool> task;
        try
        {
            task = PreviewAsync();
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref commandPreviewInFlight, 0);
            RefreshCommandStates();
            ReportCommandPreviewFailure(exception);
            return;
        }

        Volatile.Write(ref commandPreviewTask, task);
        Volatile.Write(ref commandPreviewObservationTask, ObserveCommandPreviewAsync(task));
    }

    private async Task ObserveCommandPreviewAsync(Task<bool> task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Route owners normally consume cancellation, but command
            // observation must keep a defensive cancellation boundary.
        }
        catch (Exception exception)
        {
            ReportCommandPreviewFailure(exception);
        }
        finally
        {
            if (ReferenceEquals(Volatile.Read(ref commandPreviewTask), task))
            {
                Volatile.Write(ref commandPreviewTask, null);
                Volatile.Write(ref commandPreviewObservationTask, null);
                Interlocked.Exchange(ref commandPreviewInFlight, 0);
                RefreshCommandStates();
            }
        }
    }

    private bool CanStartCommandPreview() =>
        !IsDisposed
        && Volatile.Read(ref commandPreviewInFlight) == 0
        && CanPreview();

    private void ReportCommandPreviewFailure(Exception exception)
    {
        if (!IsDisposed)
        {
            appendLog("Error", $"Selected step Preview failed: {exception.Message}");
        }
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
