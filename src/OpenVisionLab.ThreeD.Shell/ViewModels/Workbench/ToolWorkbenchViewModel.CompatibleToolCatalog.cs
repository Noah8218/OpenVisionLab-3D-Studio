using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
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

    public ObservableCollection<ToolWorkbenchCompatibleToolItem> CompatibleToolSuggestions { get; } = [];

    public ICommand SelectCompatibleToolCommand { get; private set; } = null!;
    public ICommand AddCompatibleToolCommand { get; private set; } = null!;

    public string CompatibleToolCatalogSummary => CompatibleToolSuggestions.Count == 0
        ? Localization.CompatibleToolCatalogEmpty
        : string.Format(Localization.CompatibleToolCatalogSummaryFormat, CompatibleToolSuggestions.Count);

    public bool HasCompatibleToolBlocker => !string.IsNullOrWhiteSpace(CompatibleToolBlockerDetail);
    public string CompatibleToolBlockerTitle => compatibleToolBlockerTitle;
    public string CompatibleToolBlockerDetail => compatibleToolBlockerDetail;

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
        CompatibleToolSuggestions.Clear();

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

        AddCompatibleTool("three-d-line-fit", publishedEdge is null ? [] : [publishedEdge]);
        AddCompatibleTool("height-difference-edge", publishedFilter is not null && gridSelection is not null
            ? [publishedFilter, gridSelection]
            : []);
        AddCompatibleTool("filter", source is null ? [] : [source]);
        AddCompatibleTool("roi-crop", source is null ? [] : [source]);
        AddCompatibleTool("two-point-line", source is null ? [] : [source]);
        AddCompatibleTool("three-point-plane", source is null ? [] : [source]);
        AddCompatibleTool("datum-plane-raw-height-deviation", source is not null && publishedPlane is not null && gridSelection is not null
            ? [source, publishedPlane, gridSelection]
            : []);

        SetCompatibleToolBlocker(source, gridSelection, publishedFilter, publishedEdge);

        OnPropertyChanged(nameof(CompatibleToolCatalogSummary));
        if (AddCompatibleToolCommand is RelayCommand addCompatibleToolCommand)
        {
            addCompatibleToolCommand.RaiseCanExecuteChanged();
        }
    }

    private void AddCompatibleTool(string toolId, IReadOnlyList<ToolWorkbenchArtifactItem> inputArtifacts)
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

        CompatibleToolSuggestions.Add(new ToolWorkbenchCompatibleToolItem(
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
