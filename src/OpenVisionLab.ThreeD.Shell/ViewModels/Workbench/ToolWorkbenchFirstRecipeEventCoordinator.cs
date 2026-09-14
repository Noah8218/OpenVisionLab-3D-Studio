using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns first-use recipe setup event subscriptions and forwards them to
/// Workbench policy callbacks. Draft state, validation, and persistence remain
/// in <see cref="ToolWorkbenchFirstRecipeSetupOwner"/>.
/// </summary>
internal sealed class ToolWorkbenchFirstRecipeEventCoordinator : IDisposable
{
    private readonly ToolWorkbenchFirstRecipeSetupOwner owner;
    private readonly Action<PropertyChangedEventArgs> propertyChanged;
    private readonly Action createRequested;
    private readonly Action browseFolderRequested;
    private readonly Action browseSourceRequested;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchFirstRecipeEventCoordinator(
        ToolWorkbenchFirstRecipeSetupOwner owner,
        Action<PropertyChangedEventArgs> propertyChanged,
        Action createRequested,
        Action browseFolderRequested,
        Action browseSourceRequested)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.propertyChanged = propertyChanged ?? throw new ArgumentNullException(nameof(propertyChanged));
        this.createRequested = createRequested ?? throw new ArgumentNullException(nameof(createRequested));
        this.browseFolderRequested = browseFolderRequested ?? throw new ArgumentNullException(nameof(browseFolderRequested));
        this.browseSourceRequested = browseSourceRequested ?? throw new ArgumentNullException(nameof(browseSourceRequested));

        owner.PropertyChanged += OnPropertyChanged;
        owner.CreateRequested += OnCreateRequested;
        owner.BrowseFirstRecipeFolderRequested += OnBrowseFolderRequested;
        owner.BrowseFirstRecipeSourceRequested += OnBrowseSourceRequested;
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
            owner.CreateRequested -= OnCreateRequested;
            owner.BrowseFirstRecipeFolderRequested -= OnBrowseFolderRequested;
            owner.BrowseFirstRecipeSourceRequested -= OnBrowseSourceRequested;
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

    private void OnCreateRequested(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                createRequested();
            }
        }
    }

    private void OnBrowseFolderRequested(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                browseFolderRequested();
            }
        }
    }

    private void OnBrowseSourceRequested(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                browseSourceRequested();
            }
        }
    }
}
