using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Scans existing typed artifacts for tools that can be selected next.
/// Selection is read-only. An explicitly invoked add command may create one taught step
/// from the visible candidate inputs, but never invokes Preview, Run, or Publish.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private string compatibleToolBlockerTitle = string.Empty;
    private string compatibleToolBlockerDetail = string.Empty;

    public ResettableObservableCollection<ToolWorkbenchCompatibleToolItem> CompatibleToolSuggestions { get; } = [];

    public ICommand SelectCompatibleToolCommand { get; private set; } = null!;
    public ICommand AddCompatibleToolCommand { get; private set; } = null!;

    public string CompatibleToolCatalogSummary => CompatibleToolSuggestions.Count == 0
        ? Localization.CompatibleToolCatalogEmpty
        : string.Format(Localization.CompatibleToolCatalogSummaryFormat, CompatibleToolSuggestions.Count);

    public bool HasCompatibleToolBlocker => !string.IsNullOrWhiteSpace(CompatibleToolBlockerDetail);
    public string CompatibleToolBlockerTitle => compatibleToolBlockerTitle;
    public string CompatibleToolBlockerDetail => compatibleToolBlockerDetail;
    public bool IsSelectedToolProposedRouteCompatible => GetProposedInputRoute(SelectedTool).IsCompatible;
    public string SelectedToolProposedRouteTitle => Localization.ProposedToolRoute;
    public string SelectedToolProposedRouteDetail => GetProposedInputRoute(SelectedTool).Detail;

    private void InitializeCompatibleToolCatalog()
    {
        SelectCompatibleToolCommand = new RelayCommand(
            parameter => SelectCompatibleTool(parameter as ToolWorkbenchCompatibleToolItem),
            parameter => parameter is ToolWorkbenchCompatibleToolItem);
        AddCompatibleToolCommand = new RelayCommand(
            parameter => AddCompatibleTool(parameter as ToolWorkbenchCompatibleToolItem),
            parameter => parameter is ToolWorkbenchCompatibleToolItem suggestion
                && IsSourceReadyForRecipe
                && CompatibleToolSuggestions.Contains(suggestion));
    }

    private void OnCompatibleToolCatalogLocalizationChanged(object? sender, PropertyChangedEventArgs args) =>
        RebuildCompatibleToolCatalog();

    private void RebuildCompatibleToolCatalog()
    {
        var source = ArtifactRegistry.FirstOrDefault(item =>
            item.NodeKind == "Source" && string.Equals(item.State, "Ready", StringComparison.Ordinal));
        var gridSelection = ArtifactRegistry.FirstOrDefault(item =>
            item.NodeKind == "Selection"
            && string.Equals(item.Contract, "grid-rectangle", StringComparison.Ordinal)
            && string.Equals(item.State, "Current selection", StringComparison.Ordinal));
        var publishedFilter = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "FilteredHeightField", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedEdge = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "EdgePointSet", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedPlane = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "PlaneFeature", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedAffine = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "AffineTransform3D", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedTransformed = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "TransformedPointCloud", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));

        var suggestions = new List<ToolWorkbenchCompatibleToolItem>();
        AddCompatibleTool(suggestions, "three-d-line-fit", publishedEdge is null ? [] : [publishedEdge]);
        AddCompatibleTool(suggestions, "height-difference-edge", publishedFilter is not null && gridSelection is not null
            ? [publishedFilter, gridSelection]
            : []);
        AddCompatibleTool(suggestions, "filter", source is null ? [] : [source]);
        AddCompatibleTool(suggestions, "level-surface", source is null ? [] : [source]);
        AddCompatibleTool(
            suggestions,
            "remove-outlier-pixels",
            source is null ? [] : [source]);
        AddCompatibleTool(suggestions, "roi-crop", source is null ? [] : [source]);
        AddCompatibleTool(suggestions, "two-point-line", source is null ? [] : [source]);
        AddCompatibleTool(suggestions, "three-point-plane", source is null ? [] : [source]);
        AddCompatibleTool(suggestions, "datum-plane-raw-height-deviation", source is not null && publishedPlane is not null && gridSelection is not null
            ? [source, publishedPlane, gridSelection]
            : []);
        AddCompatibleTool(suggestions, "xyz-affine-apply", source is not null && publishedAffine is not null
            ? [source, publishedAffine]
            : []);
        AddCompatibleTool(suggestions, "re-grid-height-map", publishedTransformed is null ? [] : [publishedTransformed]);
        CompatibleToolSuggestions.ReplaceAll(suggestions);

        SetCompatibleToolBlocker(source, gridSelection, publishedFilter, publishedEdge);

        OnPropertyChanged(nameof(CompatibleToolCatalogSummary));
        NotifyProposedToolRouteChanged();
        if (AddCompatibleToolCommand is RelayCommand addCompatibleToolCommand)
        {
            addCompatibleToolCommand.RaiseCanExecuteChanged();
        }
    }

    private void AddCompatibleTool(
        ICollection<ToolWorkbenchCompatibleToolItem> suggestions,
        string toolId,
        IReadOnlyList<ToolWorkbenchArtifactItem> inputArtifacts)
    {
        if (inputArtifacts.Count == 0)
        {
            return;
        }

        var tool = Tools.FirstOrDefault(item => string.Equals(item.Id, toolId, StringComparison.Ordinal));
        if (tool is null)
        {
            return;
        }

        suggestions.Add(new ToolWorkbenchCompatibleToolItem(
            tool,
            string.Join("; ", inputArtifacts.Select(item => item.Id)),
            Localization.FlowPortReady,
            ReferenceEquals(tool, SelectedTool)));
    }

    private void SelectCompatibleTool(ToolWorkbenchCompatibleToolItem? suggestion)
    {
        if (suggestion is null)
        {
            return;
        }

        SelectedTool = suggestion.Tool;
    }

    private void AddCompatibleTool(ToolWorkbenchCompatibleToolItem? suggestion)
    {
        if (suggestion is null || !CompatibleToolSuggestions.Contains(suggestion))
        {
            return;
        }

        SelectedTool = suggestion.Tool;
        AddToolToRecipe(suggestion.Tool, suggestion.InputArtifactIds);
    }

    private void SetCompatibleToolBlocker(
        ToolWorkbenchArtifactItem? source,
        ToolWorkbenchArtifactItem? gridSelection,
        ToolWorkbenchArtifactItem? publishedFilter,
        ToolWorkbenchArtifactItem? publishedEdge)
    {
        ToolWorkbenchToolItem? missingTool = null;
        if (source is not null)
        {
            missingTool = publishedFilter is null || gridSelection is null
                ? Tools.FirstOrDefault(item => string.Equals(item.Id, "height-difference-edge", StringComparison.Ordinal))
                : publishedEdge is null
                    ? Tools.FirstOrDefault(item => string.Equals(item.Id, "three-d-line-fit", StringComparison.Ordinal))
                    : null;
        }

        compatibleToolBlockerTitle = missingTool?.Name ?? string.Empty;
        compatibleToolBlockerDetail = missingTool is null
            ? string.Empty
            : string.Format(Localization.CompatibleToolBlockerDetailFormat, missingTool.Name, missingTool.InputContract);
        OnPropertyChanged(nameof(HasCompatibleToolBlocker));
        OnPropertyChanged(nameof(CompatibleToolBlockerTitle));
        OnPropertyChanged(nameof(CompatibleToolBlockerDetail));
    }

    private bool CanAddTool(ToolWorkbenchToolItem? requestedTool)
    {
        var tool = requestedTool ?? SelectedTool;
        return IsSourceReadyForRecipe
            && tool is not null
            && GetProposedInputRoute(tool).IsCompatible;
    }

    private ToolWorkbenchInputRouteProposal GetProposedInputRoute(ToolWorkbenchToolItem? tool)
    {
        if (tool is null)
        {
            return new ToolWorkbenchInputRouteProposal(false, string.Empty, string.Empty);
        }

        if (ToolRecipePrimaryInputContract.TryGetRequiredContract(tool.Id, out var requiredContract))
        {
            var candidate = FindCompatiblePrimaryInput(tool);
            return candidate is null
                ? new ToolWorkbenchInputRouteProposal(
                    false,
                    string.Empty,
                    string.Format(
                        Localization.ProposedToolRouteUnavailableFormat,
                        requiredContract,
                        tool.OutputContract))
                : new ToolWorkbenchInputRouteProposal(
                    true,
                    candidate.Id,
                    string.Format(
                        Localization.ProposedToolRouteFormat,
                        candidate.Id,
                        candidate.Contract,
                        tool.Name,
                        tool.OutputContract));
        }

        var inputId = tool.Id == "landmark-correspondence"
            ? string.Empty
            : PipelineSteps.LastOrDefault()?.OutputEntityId;
        if (tool.Id != "landmark-correspondence" && string.IsNullOrWhiteSpace(inputId))
        {
            inputId = Source.Id;
        }

        var artifact = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Id, inputId, StringComparison.OrdinalIgnoreCase));
        var inputContract = artifact?.Contract ?? tool.InputContract;
        return new ToolWorkbenchInputRouteProposal(
            true,
            inputId ?? string.Empty,
            string.Format(
                Localization.ProposedToolRouteFormat,
                string.IsNullOrWhiteSpace(inputId) ? Localization.Input : inputId,
                inputContract,
                tool.Name,
                tool.OutputContract));
    }

    private ToolWorkbenchArtifactItem? FindCompatiblePrimaryInput(ToolWorkbenchToolItem tool) =>
        ArtifactRegistry
            .Where(item => item.NodeKind == "Source" || item.PipelineStep is not null)
            .Reverse()
            .FirstOrDefault(item => ToolRecipePrimaryInputContract.IsCompatible(tool.Id, item.Contract));

    private void NotifyProposedToolRouteChanged()
    {
        OnPropertyChanged(nameof(IsSelectedToolProposedRouteCompatible));
        OnPropertyChanged(nameof(SelectedToolProposedRouteTitle));
        OnPropertyChanged(nameof(SelectedToolProposedRouteDetail));
    }

    private sealed record ToolWorkbenchInputRouteProposal(
        bool IsCompatible,
        string InputEntityIds,
        string Detail);
}

public sealed record ToolWorkbenchCompatibleToolItem(
    ToolWorkbenchToolItem Tool,
    string InputArtifactIds,
    string State,
    bool IsSelected)
{
    public string Title => Tool.Name;
    public string InputContract => Tool.InputContract;
    public string Detail => $"{InputContract} ← {InputArtifactIds}";
    public string AccessibleName => $"{Title}. {State}. {Detail}";
}
