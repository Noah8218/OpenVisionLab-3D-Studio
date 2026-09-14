namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the OrientedBox3D editor event subscriptions and forwards typed
/// requests to Workbench policy callbacks. Draft and recipe state stay in
/// their existing owners.
/// </summary>
internal sealed class ToolWorkbenchOrientedBoxEventCoordinator : IDisposable
{
    private readonly OrientedBox3DEditorViewModel editor;
    private readonly Action<OrientedBox3DDraftChangedEventArgs> draftChanged;
    private readonly Action<OrientedBox3DApplyRequestedEventArgs> applyRequested;
    private readonly Action<OrientedBox3DDeleteRequestedEventArgs> deleteRequested;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchOrientedBoxEventCoordinator(
        OrientedBox3DEditorViewModel editor,
        Action<OrientedBox3DDraftChangedEventArgs> draftChanged,
        Action<OrientedBox3DApplyRequestedEventArgs> applyRequested,
        Action<OrientedBox3DDeleteRequestedEventArgs> deleteRequested)
    {
        this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
        this.draftChanged = draftChanged ?? throw new ArgumentNullException(nameof(draftChanged));
        this.applyRequested = applyRequested ?? throw new ArgumentNullException(nameof(applyRequested));
        this.deleteRequested = deleteRequested ?? throw new ArgumentNullException(nameof(deleteRequested));

        editor.DraftChanged += OnDraftChanged;
        editor.ApplyRequested += OnApplyRequested;
        editor.DeleteRequested += OnDeleteRequested;
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
            editor.DraftChanged -= OnDraftChanged;
            editor.ApplyRequested -= OnApplyRequested;
            editor.DeleteRequested -= OnDeleteRequested;
        }
    }

    private void OnDraftChanged(object? sender, OrientedBox3DDraftChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                draftChanged(args);
            }
        }
    }

    private void OnApplyRequested(object? sender, OrientedBox3DApplyRequestedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                applyRequested(args);
            }
        }
    }

    private void OnDeleteRequested(object? sender, OrientedBox3DDeleteRequestedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                deleteRequested(args);
            }
        }
    }
}
