namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Workbench shutdown order without owning any Workbench behavior.
/// Resource-specific owners remain responsible for their own state; this type
/// only coordinates the deterministic boundary and its idempotence.
/// </summary>
internal sealed class ToolWorkbenchLifetimeCoordinator : IDisposable
{
    private readonly IReadOnlyList<IDisposable> orderedResourcesBeforeCancellation;
    private readonly Action cancelSourceQualityUiNotification;
    private readonly IReadOnlyList<IDisposable> orderedResourcesAfterCancellation;
    private int disposalState;

    public ToolWorkbenchLifetimeCoordinator(
        IReadOnlyList<IDisposable> orderedResourcesBeforeCancellation,
        Action cancelSourceQualityUiNotification,
        IReadOnlyList<IDisposable> orderedResourcesAfterCancellation)
    {
        ArgumentNullException.ThrowIfNull(orderedResourcesBeforeCancellation);
        ArgumentNullException.ThrowIfNull(cancelSourceQualityUiNotification);
        ArgumentNullException.ThrowIfNull(orderedResourcesAfterCancellation);

        this.orderedResourcesBeforeCancellation =
            orderedResourcesBeforeCancellation.ToArray();
        this.cancelSourceQualityUiNotification = cancelSourceQualityUiNotification;
        this.orderedResourcesAfterCancellation = orderedResourcesAfterCancellation.ToArray();
    }

    internal bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        foreach (var resource in orderedResourcesBeforeCancellation)
        {
            resource.Dispose();
        }

        cancelSourceQualityUiNotification();

        foreach (var resource in orderedResourcesAfterCancellation)
        {
            resource.Dispose();
        }
    }
}
