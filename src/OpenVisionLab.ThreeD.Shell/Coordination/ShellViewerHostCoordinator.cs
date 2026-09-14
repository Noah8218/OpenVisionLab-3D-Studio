using System.Windows.Threading;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Describes the View host that should own the shared Viewer for a Shell mode.
/// The policy is WPF-neutral; MainWindow supplies the concrete View mutations.
/// </summary>
internal enum ShellViewerHostTarget
{
    None,
    Workbench,
    Expert,
    Task
}

internal readonly record struct ShellViewerHostPlan(
    ShellViewerHostTarget Target,
    bool CancelTeachingCapture);

internal static class ShellViewerHostPolicy
{
    public static ShellViewerHostPlan Resolve(
        ShellWorkspaceMode mode,
        bool teachingCaptureActive)
    {
        var target = mode switch
        {
            ShellWorkspaceMode.Workbench
                or ShellWorkspaceMode.Teach
                or ShellWorkspaceMode.Inspect
                or ShellWorkspaceMode.Review => ShellViewerHostTarget.Workbench,
            ShellWorkspaceMode.Expert => ShellViewerHostTarget.Expert,
            _ => ShellViewerHostTarget.None
        };

        return new ShellViewerHostPlan(
            target,
            target != ShellViewerHostTarget.Workbench && teachingCaptureActive);
    }
}

/// <summary>
/// Owns Shell workspace-to-Viewer host transitions and the deferred Expert
/// activation operation. It does not own business state or construct Views;
/// callbacks keep the concrete WPF content mutations at the View boundary.
/// </summary>
internal sealed class ShellViewerHostCoordinator : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly ShellViewerHostCoordinatorCallbacks callbacks;
    private DispatcherOperation? expertViewerActivationOperation;
    private int disposalState;

    public ShellViewerHostCoordinator(
        Dispatcher dispatcher,
        ShellViewerHostCoordinatorCallbacks callbacks)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public void Apply()
    {
        if (Volatile.Read(ref disposalState) != 0 || callbacks.IsClosed())
        {
            return;
        }

        CancelExpertViewerActivation();
        var plan = ShellViewerHostPolicy.Resolve(
            callbacks.GetWorkspaceMode(),
            callbacks.IsTeachingCaptureActive());
        if (plan.CancelTeachingCapture)
        {
            callbacks.CancelTeachingCapture();
        }

        switch (plan.Target)
        {
            case ShellViewerHostTarget.Workbench:
                callbacks.ShowWorkbenchViewer();
                break;
            case ShellViewerHostTarget.Expert:
                callbacks.ShowExpertViewer();
                QueueExpertViewerActivation();
                break;
            case ShellViewerHostTarget.Task:
                callbacks.ShowTaskViewer();
                break;
            default:
                callbacks.ClearViewerHosts();
                break;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        CancelExpertViewerActivation();
    }

    private void QueueExpertViewerActivation()
    {
        if (Volatile.Read(ref disposalState) != 0
            || callbacks.IsClosed()
            || dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (expertViewerActivationOperation?.Status is DispatcherOperationStatus.Pending
            or DispatcherOperationStatus.Executing)
        {
            return;
        }

        try
        {
            expertViewerActivationOperation = dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(ApplyExpertViewerActivation));
        }
        catch (InvalidOperationException)
        {
            expertViewerActivationOperation = null;
        }
    }

    private void ApplyExpertViewerActivation()
    {
        expertViewerActivationOperation = null;
        if (Volatile.Read(ref disposalState) != 0
            || callbacks.IsClosed()
            || !callbacks.IsExpertViewerAttached())
        {
            return;
        }

        callbacks.UpdateExpertViewerLayout();
        callbacks.RequestVisibleFrame();
    }

    private void CancelExpertViewerActivation()
    {
        var operation = expertViewerActivationOperation;
        expertViewerActivationOperation = null;
        if (operation?.Status == DispatcherOperationStatus.Pending)
        {
            operation.Abort();
        }
    }
}

internal sealed class ShellViewerHostCoordinatorCallbacks
{
    public required Func<ShellWorkspaceMode> GetWorkspaceMode { get; init; }
    public required Func<bool> IsTeachingCaptureActive { get; init; }
    public required Action CancelTeachingCapture { get; init; }
    public required Action ShowWorkbenchViewer { get; init; }
    public required Action ShowExpertViewer { get; init; }
    public required Action ShowTaskViewer { get; init; }
    public required Action ClearViewerHosts { get; init; }
    public required Func<bool> IsExpertViewerAttached { get; init; }
    public required Action UpdateExpertViewerLayout { get; init; }
    public required Action RequestVisibleFrame { get; init; }
    public required Func<bool> IsClosed { get; init; }
}
