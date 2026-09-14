using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns one Viewer workspace notification subscription, used for the session
/// to presentation ViewModel and presentation ViewModel to Workbench facade.
/// </summary>
internal sealed class ToolWorkbenchViewerWorkspaceEventCoordinator : IDisposable
{
    private readonly INotifyPropertyChanged owner;
    private readonly Action<PropertyChangedEventArgs> propertyChanged;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchViewerWorkspaceEventCoordinator(
        INotifyPropertyChanged owner,
        Action<PropertyChangedEventArgs> propertyChanged)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.propertyChanged = propertyChanged
            ?? throw new ArgumentNullException(nameof(propertyChanged));

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
