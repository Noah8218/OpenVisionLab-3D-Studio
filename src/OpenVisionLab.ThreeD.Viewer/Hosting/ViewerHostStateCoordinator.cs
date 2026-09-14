namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// Coordinates disposal-aware host notifications from a supplied immutable
/// state snapshot and translates only known notifications.
///
/// ViewModel projection belongs to <see cref="ViewerHostStateProjection"/>;
/// this type owns notification admission and public-property routing only.
/// </summary>
internal sealed class ViewerHostStateCoordinator
{
    private readonly Func<ViewerHostState> snapshot;
    private readonly Func<bool> isDisposed;
    private readonly Action<ViewerHostStateChangedEventArgs> publish;

    public ViewerHostStateCoordinator(
        Func<ViewerHostState> snapshot,
        Func<bool> isDisposed,
        Action<ViewerHostStateChangedEventArgs> publish)
    {
        this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        this.publish = publish ?? throw new ArgumentNullException(nameof(publish));
    }

    public ViewerHostState Current => snapshot();

    public void Notify(string? viewModelPropertyName)
    {
        if (isDisposed())
        {
            return;
        }

        var hostPropertyName = ViewerHostStatePropertyMap.Map(viewModelPropertyName);
        if (hostPropertyName is not null)
        {
            publish(new ViewerHostStateChangedEventArgs(Current, hostPropertyName));
        }
    }
}
