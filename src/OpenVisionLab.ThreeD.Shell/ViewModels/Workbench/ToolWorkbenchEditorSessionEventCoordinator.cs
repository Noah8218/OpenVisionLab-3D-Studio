using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Workbench editor-session event subscriptions that bridge selection
/// and detached PropertyGrid draft state into the existing ViewModel
/// projections. The sessions retain their state; this type owns only lifetime
/// and callback forwarding.
/// </summary>
internal sealed class ToolWorkbenchEditorSessionEventCoordinator : IDisposable
{
    private readonly InspectionWorkspaceSelectionSession workspaceSelection;
    private readonly ToolWorkbenchStepPropertySession stepPropertySession;
    private readonly Action<InspectionWorkspaceSelectionChangedEventArgs> selectionChanged;
    private readonly Action<PropertyChangedEventArgs> propertyChanged;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchEditorSessionEventCoordinator(
        InspectionWorkspaceSelectionSession workspaceSelection,
        Action<InspectionWorkspaceSelectionChangedEventArgs> selectionChanged,
        ToolWorkbenchStepPropertySession stepPropertySession,
        Action<PropertyChangedEventArgs> propertyChanged)
    {
        this.workspaceSelection = workspaceSelection
            ?? throw new ArgumentNullException(nameof(workspaceSelection));
        this.selectionChanged = selectionChanged
            ?? throw new ArgumentNullException(nameof(selectionChanged));
        this.stepPropertySession = stepPropertySession
            ?? throw new ArgumentNullException(nameof(stepPropertySession));
        this.propertyChanged = propertyChanged
            ?? throw new ArgumentNullException(nameof(propertyChanged));

        workspaceSelection.SelectionChanged += OnSelectionChanged;
        stepPropertySession.PropertyChanged += OnPropertyChanged;
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
            workspaceSelection.SelectionChanged -= OnSelectionChanged;
            stepPropertySession.PropertyChanged -= OnPropertyChanged;
        }
    }

    private void OnSelectionChanged(
        object? sender,
        InspectionWorkspaceSelectionChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                selectionChanged(args);
            }
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
