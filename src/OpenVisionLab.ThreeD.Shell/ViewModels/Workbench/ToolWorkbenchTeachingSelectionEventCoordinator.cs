using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Workbench teaching-selection owner notifications until the
/// Workbench is disposed. Teaching policy remains in the existing callbacks.
/// </summary>
internal sealed class ToolWorkbenchTeachingSelectionEventCoordinator : IDisposable
{
    private readonly ToolWorkbenchTeachingSelectionStoreOwner selectionStoreOwner;
    private readonly PropertyChangedEventHandler selectionStoreChanged;
    private readonly ToolWorkbenchTeachingSelectionCaptureOwner selectionCaptureOwner;
    private readonly PropertyChangedEventHandler selectionCaptureChanged;
    private readonly EventHandler selectionCaptureStateChanged;
    private readonly ToolWorkbenchLandmarkCorrespondenceEditorOwner landmarkOwner;
    private readonly PropertyChangedEventHandler landmarkChanged;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchTeachingSelectionEventCoordinator(
        ToolWorkbenchTeachingSelectionStoreOwner selectionStoreOwner,
        PropertyChangedEventHandler selectionStoreChanged,
        ToolWorkbenchTeachingSelectionCaptureOwner selectionCaptureOwner,
        PropertyChangedEventHandler selectionCaptureChanged,
        EventHandler selectionCaptureStateChanged,
        ToolWorkbenchLandmarkCorrespondenceEditorOwner landmarkOwner,
        PropertyChangedEventHandler landmarkChanged)
    {
        this.selectionStoreOwner = selectionStoreOwner
            ?? throw new ArgumentNullException(nameof(selectionStoreOwner));
        this.selectionStoreChanged = selectionStoreChanged
            ?? throw new ArgumentNullException(nameof(selectionStoreChanged));
        this.selectionCaptureOwner = selectionCaptureOwner
            ?? throw new ArgumentNullException(nameof(selectionCaptureOwner));
        this.selectionCaptureChanged = selectionCaptureChanged
            ?? throw new ArgumentNullException(nameof(selectionCaptureChanged));
        this.selectionCaptureStateChanged = selectionCaptureStateChanged
            ?? throw new ArgumentNullException(nameof(selectionCaptureStateChanged));
        this.landmarkOwner = landmarkOwner
            ?? throw new ArgumentNullException(nameof(landmarkOwner));
        this.landmarkChanged = landmarkChanged
            ?? throw new ArgumentNullException(nameof(landmarkChanged));

        selectionStoreOwner.PropertyChanged += OnSelectionStoreChanged;
        selectionCaptureOwner.PropertyChanged += OnSelectionCaptureChanged;
        selectionCaptureOwner.StateChanged += OnSelectionCaptureStateChanged;
        landmarkOwner.PropertyChanged += OnLandmarkChanged;
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
            selectionStoreOwner.PropertyChanged -= OnSelectionStoreChanged;
            selectionCaptureOwner.PropertyChanged -= OnSelectionCaptureChanged;
            selectionCaptureOwner.StateChanged -= OnSelectionCaptureStateChanged;
            landmarkOwner.PropertyChanged -= OnLandmarkChanged;
        }
    }

    private void OnSelectionStoreChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                selectionStoreChanged(sender, args);
            }
        }
    }

    private void OnSelectionCaptureChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                selectionCaptureChanged(sender, args);
            }
        }
    }

    private void OnSelectionCaptureStateChanged(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                selectionCaptureStateChanged(sender, args);
            }
        }
    }

    private void OnLandmarkChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                landmarkChanged(sender, args);
            }
        }
    }
}
