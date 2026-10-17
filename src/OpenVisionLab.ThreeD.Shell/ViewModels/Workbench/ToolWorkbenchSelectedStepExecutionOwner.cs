using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;

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
    private readonly Func<ToolWorkbenchPipelineStepItem, string> captureContext;
    private readonly Func<SelectedToolOutputEvidence> captureOutput;
    private readonly Action<ToolWorkbenchPipelineStepItem, bool, ResultStatus?, string, string, bool>? recordExecution;
    private readonly Dictionary<ToolWorkbenchPipelineStepItem, (SelectedToolOutputEvidence Output, string Context)> completedResults = [];
    private readonly Action notifyRunningChanged;
    private readonly Func<bool> canStartExecution;
    private ToolWorkbenchSelectedStepExecutionRoute? activeRoute;
    private bool commandCanceled;
    private Task<bool>? commandPreviewTask;
    private Task? commandPreviewObservationTask;
    private int commandPreviewInFlight;
    private int disposalState;

    public ToolWorkbenchSelectedStepExecutionOwner(
        Func<ToolWorkbenchPipelineStepItem?> getSelectedStep,
        IReadOnlyDictionary<string, ToolWorkbenchSelectedStepExecutionRoute> routes,
        Action<string, string> appendLog,
        Func<ToolWorkbenchPipelineStepItem, string>? captureContext = null,
        Action? notifyRunningChanged = null,
        Func<bool>? canStartExecution = null,
        Func<SelectedToolOutputEvidence>? captureOutput = null,
        Action<ToolWorkbenchPipelineStepItem, bool, ResultStatus?, string, string, bool>? recordExecution = null)
    {
        this.getSelectedStep = getSelectedStep ?? throw new ArgumentNullException(nameof(getSelectedStep));
        this.routes = routes ?? throw new ArgumentNullException(nameof(routes));
        this.appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));
        this.captureContext = captureContext ?? (_ => string.Empty);
        this.notifyRunningChanged = notifyRunningChanged ?? (() => { });
        this.canStartExecution = canStartExecution ?? (() => true);
        this.captureOutput = captureOutput ?? (() => SelectedToolOutputEvidence.Empty);
        this.recordExecution = recordExecution;

        RunCommand = new RelayCommand(_ => StartCommandPreview(publish: true), _ => CanStartCommandPreview());
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
    public RelayCommand RunCommand { get; }
    public RelayCommand PublishCommand { get; }
    public RelayCommand CancelCommand { get; }
    public event EventHandler? RunCompleted;

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool IsCommandRunning => !IsDisposed && Volatile.Read(ref commandPreviewInFlight) != 0;
    public bool IsRunning => IsCommandRunning || !IsDisposed && TryGetCurrentRoute(out var route) && route.IsRunning();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        completedResults.Clear();
        commandCanceled = true;
        activeRoute?.Cancel();
        activeRoute = null;
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
            if (getSelectedStep() is { } step) RememberCompletedResult(step);
        }
    }

    public bool CanPublish() =>
        !IsDisposed
        && getSelectedStep() is { OutputEnabled: true }
        && TryGetCurrentRoute(out var route)
        && route.CanPublish();

    public void Cancel()
    {
        if (IsDisposed)
        {
            return;
        }

        commandCanceled = true;
        if (activeRoute is { } runningRoute)
        {
            runningRoute.Cancel();
        }
        else if (TryGetCurrentRoute(out var route))
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
        RunCommand.RaiseCanExecuteChanged();
        PublishCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    private void StartCommandPreview(bool publish = false)
    {
        if (!CanStartCommandPreview()
            || Interlocked.CompareExchange(ref commandPreviewInFlight, 1, 0) != 0)
        {
            return;
        }

        if (Volatile.Read(ref commandPreviewObservationTask) is { IsCompleted: true })
        {
            Volatile.Write(ref commandPreviewObservationTask, null);
        }

        Task<bool> task;
        try
        {
            commandCanceled = false;
            activeRoute = TryGetCurrentRoute(out var route) ? route : null;
            var step = getSelectedStep();
            var context = step is null ? string.Empty : captureContext(step);
            if (step is not null && completedResults.TryGetValue(step, out var previous))
                completedResults[step] = (previous.Output, string.Empty);
            RefreshCommandStates();
            notifyRunningChanged();
            task = ExecuteCommandAsync(step, context, publish);
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref commandPreviewInFlight, 0);
            activeRoute = null;
            RefreshCommandStates();
            notifyRunningChanged();
            ReportCommandPreviewFailure(exception);
            return;
        }

        Volatile.Write(ref commandPreviewTask, task);
        Volatile.Write(ref commandPreviewObservationTask, ObserveCommandPreviewAsync(task));
    }

    private async Task<bool> ExecuteCommandAsync(ToolWorkbenchPipelineStepItem? step, string context, bool publish)
    {
        var route = activeRoute;
        if (route is null || step is null) return false;
        try
        {
            var previewed = await route.PreviewAsync();
            if (IsDisposed) return false;
            if (commandCanceled)
            {
                recordExecution?.Invoke(step, publish, null, string.Empty, string.Empty, true);
                return false;
            }
            if (!previewed || !ReferenceEquals(step, getSelectedStep()) || !step.OutputEnabled
                || !string.Equals(context, captureContext(step), StringComparison.Ordinal))
            {
                recordExecution?.Invoke(step, publish, ResultStatus.NotRun, string.Empty, string.Empty, false);
                return false;
            }

            var output = captureOutput();
            if (publish && route.CanPublish())
            {
                route.Publish();
                RememberCompletedResult(step);
                RecordCommandExecution(step, output, publish);
                RunCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                RecordCommandExecution(step, output, publish);
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed) recordExecution?.Invoke(step, publish, null, string.Empty, string.Empty, true);
            throw;
        }
        catch (Exception exception)
        {
            if (!IsDisposed) recordExecution?.Invoke(step, publish, ResultStatus.Error, string.Empty, exception.Message, false);
            throw;
        }
    }

    private void RecordCommandExecution(ToolWorkbenchPipelineStepItem step, SelectedToolOutputEvidence output, bool publish)
    {
        var detail = output.HasExecutionResult ? $"{output.ValueLabel} {output.Value} {output.Unit}".Trim() : string.Empty;
        recordExecution?.Invoke(step, publish, output.HasExecutionResult ? output.ExecutionStatus : ResultStatus.NotRun, detail, output.Reason, false);
    }

    public void ClearCompletedResults() => completedResults.Clear();
    public void ForgetCompletedResult(ToolWorkbenchPipelineStepItem step) => completedResults.Remove(step);

    public bool TryGetCompletedResult(ToolWorkbenchPipelineStepItem step, out SelectedToolOutputEvidence output, out bool isCurrent)
    {
        output = SelectedToolOutputEvidence.Empty;
        isCurrent = false;
        if (IsDisposed || !completedResults.TryGetValue(step, out var result)) return false;
        output = result.Output;
        isCurrent = !IsRunning && !string.IsNullOrEmpty(result.Context)
            && string.Equals(result.Context, captureContext(step), StringComparison.Ordinal);
        return true;
    }

    private void RememberCompletedResult(ToolWorkbenchPipelineStepItem step)
    {
        var output = captureOutput();
        if (output.HasExecutionResult) completedResults[step] = (output, captureContext(step));
        notifyRunningChanged();
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
                activeRoute = null;
                RefreshCommandStates();
                notifyRunningChanged();
            }
        }
    }

    private bool CanStartCommandPreview() =>
        !IsDisposed
        && Volatile.Read(ref commandPreviewInFlight) == 0
        && canStartExecution()
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
