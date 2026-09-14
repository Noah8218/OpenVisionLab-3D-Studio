using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Flow Diagnostics owner notification subscription and forwards
/// presentation changes to the Workbench facade. Flow projection and
/// navigation policy remain in <see cref="ToolWorkbenchFlowDiagnosticsOwner"/>.
/// </summary>
internal sealed class ToolWorkbenchFlowDiagnosticsEventCoordinator : IDisposable
{
    private readonly INotifyPropertyChanged owner;
    private readonly Action<PropertyChangedEventArgs> propertyChanged;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchFlowDiagnosticsEventCoordinator(
        INotifyPropertyChanged owner,
        Action<PropertyChangedEventArgs> propertyChanged)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.propertyChanged = propertyChanged ?? throw new ArgumentNullException(nameof(propertyChanged));

        owner.PropertyChanged += OnPropertyChanged;
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
            owner.PropertyChanged -= OnPropertyChanged;
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
}
