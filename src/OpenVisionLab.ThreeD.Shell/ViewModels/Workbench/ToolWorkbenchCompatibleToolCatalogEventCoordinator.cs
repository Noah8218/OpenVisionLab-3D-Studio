using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the compatible-tool catalog notification subscription and forwards
/// presentation changes to the Workbench facade.
/// </summary>
internal sealed class ToolWorkbenchCompatibleToolCatalogEventCoordinator : IDisposable
{
    private readonly INotifyPropertyChanged owner;
    private readonly Action<PropertyChangedEventArgs> propertyChanged;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchCompatibleToolCatalogEventCoordinator(
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
