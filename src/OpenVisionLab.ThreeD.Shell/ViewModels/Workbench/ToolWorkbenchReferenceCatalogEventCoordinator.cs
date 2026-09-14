using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the reference-catalog notification subscriptions and forwards the
/// existing Workbench recipe and presentation callbacks.
/// </summary>
internal sealed class ToolWorkbenchReferenceCatalogEventCoordinator : IDisposable
{
    private readonly ToolWorkbenchReferenceCatalogOwner owner;
    private readonly PropertyChangedEventHandler propertyChanged;
    private readonly PropertyChangedEventHandler referencePropertyChanged;
    private readonly EventHandler<ToolWorkbenchReferenceMutationEventArgs> mutated;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchReferenceCatalogEventCoordinator(
        ToolWorkbenchReferenceCatalogOwner owner,
        PropertyChangedEventHandler propertyChanged,
        PropertyChangedEventHandler referencePropertyChanged,
        EventHandler<ToolWorkbenchReferenceMutationEventArgs> mutated)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.propertyChanged = propertyChanged
            ?? throw new ArgumentNullException(nameof(propertyChanged));
        this.referencePropertyChanged = referencePropertyChanged
            ?? throw new ArgumentNullException(nameof(referencePropertyChanged));
        this.mutated = mutated ?? throw new ArgumentNullException(nameof(mutated));

        owner.PropertyChanged += OnPropertyChanged;
        owner.ReferencePropertyChanged += OnReferencePropertyChanged;
        owner.Mutated += OnMutated;
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
            owner.ReferencePropertyChanged -= OnReferencePropertyChanged;
            owner.Mutated -= OnMutated;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                propertyChanged(sender, args);
            }
        }
    }

    private void OnReferencePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                referencePropertyChanged(sender, args);
            }
        }
    }

    private void OnMutated(object? sender, ToolWorkbenchReferenceMutationEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                mutated(sender, args);
            }
        }
    }
}
