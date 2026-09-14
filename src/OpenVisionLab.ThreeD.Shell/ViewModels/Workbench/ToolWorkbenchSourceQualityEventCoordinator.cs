using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the source-quality workspace notification subscription and forwards
/// changes to the Workbench presentation policy.
/// </summary>
internal sealed class ToolWorkbenchSourceQualityEventCoordinator : IDisposable
{
    private readonly INotifyPropertyChanged owner;
    private readonly Action<PropertyChangedEventArgs> propertyChanged;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchSourceQualityEventCoordinator(
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
