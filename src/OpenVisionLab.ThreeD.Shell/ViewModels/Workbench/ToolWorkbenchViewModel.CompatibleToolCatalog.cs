using System.ComponentModel;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Public Workbench facade for the compatible-tool catalog owner.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private ToolWorkbenchCompatibleToolCatalogOwner compatibleToolCatalogOwner = null!;

    public ResettableObservableCollection<ToolWorkbenchCompatibleToolItem>
        CompatibleToolSuggestions => compatibleToolCatalogOwner.CompatibleToolSuggestions;

    public ICommand SelectCompatibleToolCommand =>
        compatibleToolCatalogOwner.SelectCompatibleToolCommand;

    public ICommand AddCompatibleToolCommand =>
        compatibleToolCatalogOwner.AddCompatibleToolCommand;

    public string CompatibleToolCatalogSummary =>
        compatibleToolCatalogOwner.CompatibleToolCatalogSummary;

    public bool HasCompatibleToolBlocker =>
        compatibleToolCatalogOwner.HasCompatibleToolBlocker;

    public string CompatibleToolBlockerTitle =>
        compatibleToolCatalogOwner.CompatibleToolBlockerTitle;

    public string CompatibleToolBlockerDetail =>
        compatibleToolCatalogOwner.CompatibleToolBlockerDetail;

    public bool IsSelectedToolProposedRouteCompatible =>
        compatibleToolCatalogOwner.IsSelectedToolProposedRouteCompatible;

    public string SelectedToolProposedRouteTitle =>
        compatibleToolCatalogOwner.SelectedToolProposedRouteTitle;

    public string SelectedToolProposedRouteDetail =>
        compatibleToolCatalogOwner.SelectedToolProposedRouteDetail;

    private void InitializeCompatibleToolCatalog()
    {
        compatibleToolCatalogOwner = new ToolWorkbenchCompatibleToolCatalogOwner(
            Localization,
            CreateCompatibleToolCatalogSnapshot,
            tool => SelectedTool = tool,
            AddToolToRecipe);
        compatibleToolCatalogOwner.PropertyChanged +=
            OnCompatibleToolCatalogOwnerPropertyChanged;
    }

    private ToolWorkbenchCompatibleToolCatalogSnapshot
        CreateCompatibleToolCatalogSnapshot() => new(
            Tools.ToArray(),
            ArtifactRegistry.ToArray(),
            PipelineSteps.ToArray(),
            new ToolWorkbenchCompatibleSourceSnapshot(Source.Id),
            SelectedTool,
            IsSourceReadyForRecipe,
            SourceQuality.Report,
            SourceQuality.Error);

    private void OnCompatibleToolCatalogOwnerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    private void OnCompatibleToolCatalogLocalizationChanged(
        object? sender,
        PropertyChangedEventArgs args) =>
        compatibleToolCatalogOwner.Rebuild();

    private void RebuildCompatibleToolCatalog() =>
        compatibleToolCatalogOwner.Rebuild();

    private bool CanAddTool(ToolWorkbenchToolItem? requestedTool) =>
        compatibleToolCatalogOwner.CanAddTool(requestedTool);

    private ToolWorkbenchInputRouteProposal GetProposedInputRoute(
        ToolWorkbenchToolItem? tool) =>
        compatibleToolCatalogOwner.GetProposedInputRoute(tool);

    private void NotifyProposedToolRouteChanged() =>
        compatibleToolCatalogOwner.NotifySelectedToolChanged();
}
