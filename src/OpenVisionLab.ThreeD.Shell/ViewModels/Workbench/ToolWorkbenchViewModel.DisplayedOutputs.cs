using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Compatibility facade for the independently owned Displayed Outputs
/// presentation state. Renderability remains supplied by the neutral catalog.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private ToolWorkbenchDisplayedOutputsOwner displayedOutputsOwner = null!;

    public event EventHandler<ToolWorkbenchArtifactDisplayRequestEventArgs>?
        ViewerArtifactDisplayRequested
    {
        add => displayedOutputsOwner.ViewerArtifactDisplayRequested += value;
        remove => displayedOutputsOwner.ViewerArtifactDisplayRequested -= value;
    }

    public ResettableObservableCollection<ToolWorkbenchDisplayedOutputItem>
        DisplayedOutputs => displayedOutputsOwner.DisplayedOutputs;

    public ICommand ShowDisplayedOutputInViewerCommand =>
        displayedOutputsOwner.ShowDisplayedOutputInViewerCommand;

    public ICommand PinDisplayedOutputToCompareCommand =>
        displayedOutputsOwner.PinDisplayedOutputToCompareCommand;

    public ICommand FocusDisplayedOutputStepCommand =>
        displayedOutputsOwner.FocusDisplayedOutputStepCommand;

    public string DisplayedOutputsSummary =>
        displayedOutputsOwner.DisplayedOutputsSummary;

    public string CurrentViewerOutputSummary =>
        displayedOutputsOwner.CurrentViewerOutputSummary;

    private void InitializeDisplayedOutputs()
    {
        displayedOutputsOwner = new ToolWorkbenchDisplayedOutputsOwner(
            () => ArtifactRegistry.ToArray(),
            GetRenderableC3DTarget,
            Localization,
            outputCompareSession.GetComparePins,
            () => outputCompareSession.HasEmptyCompareSlot,
            outputCompareSession.TryPin,
            DisplayDisplayedOutputInPrimaryViewer,
            step =>
            {
                SelectedPipelineStep = step;
                RefreshNavigatorSelection();
            },
            RefreshSelectedToolWorkspaceProjection);
        displayedOutputsOwner.PropertyChanged +=
            OnDisplayedOutputsOwnerPropertyChanged;
    }

    private void OnDisplayedOutputsOwnerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    private void OnDisplayedOutputsLocalizationChanged(
        object? sender,
        PropertyChangedEventArgs args) =>
        RefreshDisplayedOutputPresentation();

    private void RebuildDisplayedOutputs() =>
        displayedOutputsOwner.Rebuild();

    private void RequestDisplayedOutputInViewer(
        ToolWorkbenchDisplayedOutputItem? item) =>
        displayedOutputsOwner.RequestDisplayedOutputInViewer(item);

    private void PinDisplayedOutputToCompare(
        ToolWorkbenchDisplayedOutputItem? item) =>
        displayedOutputsOwner.PinDisplayedOutputToCompare(item);

    private void FocusDisplayedOutputStep(
        ToolWorkbenchDisplayedOutputItem? item) =>
        displayedOutputsOwner.FocusDisplayedOutputStep(item);

    private void RefreshDisplayedOutputPresentation() =>
        displayedOutputsOwner.RefreshPresentation();

    private void DisplayDisplayedOutputInPrimaryViewer(string artifactId)
    {
        // Showing an existing typed artifact in the primary Viewer is an
        // explicit presentation action. It does not change recipe routing or
        // execute work.
        ViewerWorkspace.PinMainContent(artifactId);
        ViewerWorkspace.FocusSlot(ViewerWorkspaceSession.MainSlotId);
        WorkspaceSelection.SelectOutput(artifactId);
        WorkspaceSelection.FocusViewerSlot(ViewerWorkspaceSession.MainSlotId);
    }
}
