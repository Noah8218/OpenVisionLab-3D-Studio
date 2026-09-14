using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns Artifact Navigator event subscriptions and forwards them to the
/// Workbench. Projection, selection, and dependent rebuild policy remain with
/// <see cref="ToolWorkbenchArtifactNavigatorOwner"/> and the Workbench.
/// </summary>
internal sealed class ToolWorkbenchArtifactNavigatorEventCoordinator : IDisposable
{
    private readonly ToolWorkbenchArtifactNavigatorOwner owner;
    private readonly Action<PropertyChangedEventArgs> propertyChanged;
    private readonly Action rebuilt;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchArtifactNavigatorEventCoordinator(
        ToolWorkbenchArtifactNavigatorOwner owner,
        Action<PropertyChangedEventArgs> propertyChanged,
        Action rebuilt)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.propertyChanged = propertyChanged ?? throw new ArgumentNullException(nameof(propertyChanged));
        this.rebuilt = rebuilt ?? throw new ArgumentNullException(nameof(rebuilt));

        owner.PropertyChanged += OnPropertyChanged;
        owner.Rebuilt += OnRebuilt;
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
            owner.Rebuilt -= OnRebuilt;
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

    private void OnRebuilt(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                rebuilt();
            }
        }
    }
}
