using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Output Compare session event subscriptions and forwards typed
/// presentation callbacks. Compare state remains in the session and recipe
/// execution/persistence remain outside this coordinator.
/// </summary>
internal sealed class ToolWorkbenchOutputCompareEventCoordinator : IDisposable
{
    private readonly ToolWorkbenchOutputCompareSession session;
    private readonly Action<PropertyChangedEventArgs> propertyChanged;
    private readonly Action pinsChanged;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchOutputCompareEventCoordinator(
        ToolWorkbenchOutputCompareSession session,
        Action<PropertyChangedEventArgs> propertyChanged,
        Action pinsChanged)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.propertyChanged = propertyChanged ?? throw new ArgumentNullException(nameof(propertyChanged));
        this.pinsChanged = pinsChanged ?? throw new ArgumentNullException(nameof(pinsChanged));

        session.PropertyChanged += OnPropertyChanged;
        session.PinsChanged += OnPinsChanged;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            session.PropertyChanged -= OnPropertyChanged;
            session.PinsChanged -= OnPinsChanged;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                propertyChanged(args);
            }
        }
    }

    private void OnPinsChanged(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                pinsChanged();
            }
        }
    }
}
