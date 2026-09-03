using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the read-only compatible-tool catalog, source-quality gate, and proposed
/// input-route policy. Recipe mutation is limited to the injected explicit Add callback.
/// </summary>
internal sealed class ToolWorkbenchCompatibleToolCatalogOwner : INotifyPropertyChanged
{
    private readonly ThreeDLocalization localization;
    private readonly Func<ToolWorkbenchCompatibleToolCatalogSnapshot> getSnapshot;
    private readonly Action<ToolWorkbenchToolItem> selectTool;
    private readonly Action<ToolWorkbenchToolItem, string> addToolToRecipe;
    private readonly RelayCommand selectCompatibleToolCommand;
    private readonly RelayCommand addCompatibleToolCommand;
    private string compatibleToolBlockerTitle = string.Empty;
    private string compatibleToolBlockerDetail = string.Empty;

    public ToolWorkbenchCompatibleToolCatalogOwner(
        ThreeDLocalization localization,
        Func<ToolWorkbenchCompatibleToolCatalogSnapshot> getSnapshot,
        Action<ToolWorkbenchToolItem> selectTool,
        Action<ToolWorkbenchToolItem, string> addToolToRecipe)
    {
        this.localization = localization
            ?? throw new ArgumentNullException(nameof(localization));
        this.getSnapshot = getSnapshot
            ?? throw new ArgumentNullException(nameof(getSnapshot));
        this.selectTool = selectTool
            ?? throw new ArgumentNullException(nameof(selectTool));
        this.addToolToRecipe = addToolToRecipe
            ?? throw new ArgumentNullException(nameof(addToolToRecipe));

        selectCompatibleToolCommand = new RelayCommand(
            parameter => SelectCompatibleTool(parameter as ToolWorkbenchCompatibleToolItem),
            parameter => parameter is ToolWorkbenchCompatibleToolItem);
        addCompatibleToolCommand = new RelayCommand(
            parameter => AddCompatibleTool(parameter as ToolWorkbenchCompatibleToolItem),
            parameter => parameter is ToolWorkbenchCompatibleToolItem suggestion
                && CanAddCompatibleTool(suggestion));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ResettableObservableCollection<ToolWorkbenchCompatibleToolItem>
        CompatibleToolSuggestions { get; } = [];

    public RelayCommand SelectCompatibleToolCommand => selectCompatibleToolCommand;

    public RelayCommand AddCompatibleToolCommand => addCompatibleToolCommand;

    public string CompatibleToolCatalogSummary => CompatibleToolSuggestions.Count == 0
        ? localization.CompatibleToolCatalogEmpty
        : string.Format(
            localization.CompatibleToolCatalogSummaryFormat,
            CompatibleToolSuggestions.Count(item => item.IsAvailable),
            CompatibleToolSuggestions.Count(item => !item.IsAvailable));

    public bool HasCompatibleToolBlocker =>
        !string.IsNullOrWhiteSpace(compatibleToolBlockerDetail);

    public string CompatibleToolBlockerTitle => compatibleToolBlockerTitle;

    public string CompatibleToolBlockerDetail => compatibleToolBlockerDetail;

    public bool IsSelectedToolProposedRouteCompatible =>
        GetProposedInputRoute(getSnapshot()).IsCompatible;

    public string SelectedToolProposedRouteTitle => localization.ProposedToolRoute;

    public string SelectedToolProposedRouteDetail =>
        GetProposedInputRoute(getSnapshot()).Detail;

    public void Rebuild()
    {
        var snapshot = getSnapshot();
        var source = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            item.NodeKind == "Source"
            && string.Equals(item.State, "Ready", StringComparison.Ordinal));
        var gridSelection = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            item.NodeKind == "Selection"
            && ToolRecipeSelectionContract.IsSupported(
                "height-difference-edge",
                1,
                item.Contract)
            && string.Equals(item.State, "Current selection", StringComparison.Ordinal));
        var publishedFilter = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "FilteredHeightField", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedOutlier = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            item.PipelineStep?.ToolId == "remove-outlier-pixels"
            && string.Equals(item.Contract, "FilteredHeightField", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedConnectedRegion = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "ConnectedRegionArtifact", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedEditableRegion = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "EditableRegionArtifact", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedEdge = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "EdgePointSet", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedPlane = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "PlaneFeature", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedAffine = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "AffineTransform3D", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));
        var publishedTransformed = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Contract, "TransformedPointCloud", StringComparison.Ordinal)
            && string.Equals(item.State, "Published", StringComparison.Ordinal));

        var suggestions = new List<ToolWorkbenchCompatibleToolItem>();
        AddCompatibleTool(snapshot, suggestions, "three-d-line-fit", publishedEdge is null ? [] : [publishedEdge]);
        AddCompatibleTool(
            snapshot,
            suggestions,
            "height-difference-edge",
            publishedFilter is not null && gridSelection is not null
                ? [publishedFilter, gridSelection]
                : []);
        AddCompatibleTool(snapshot, suggestions, "filter", source is null ? [] : [source]);
        AddCompatibleTool(snapshot, suggestions, "level-surface", source is null ? [] : [source]);
        AddCompatibleTool(
            snapshot,
            suggestions,
            "remove-outlier-pixels",
            source is null ? [] : [source]);
        AddCompatibleTool(
            snapshot,
            suggestions,
            "connected-region",
            publishedOutlier is null ? [] : [publishedOutlier]);
        IReadOnlyList<ToolWorkbenchArtifactItem> publishedDomainMask =
            publishedConnectedRegion is not null
            && publishedOutlier is not null
            && string.Equals(
                publishedConnectedRegion.InputEntityIds,
                publishedOutlier.Id,
                StringComparison.OrdinalIgnoreCase)
                ? [publishedOutlier, publishedConnectedRegion]
                : [];
        AddCompatibleTool(snapshot, suggestions, "domain-mask", publishedDomainMask);
        AddCompatibleTool(
            snapshot,
            suggestions,
            "editable-region",
            publishedConnectedRegion is null ? [] : [publishedConnectedRegion]);
        AddCompatibleTool(
            snapshot,
            suggestions,
            "completeness-grid",
            source is not null && gridSelection is not null && publishedEditableRegion is not null
                ? [source, gridSelection, publishedEditableRegion]
                : []);
        AddCompatibleTool(snapshot, suggestions, "roi-crop", source is null ? [] : [source]);
        AddCompatibleTool(snapshot, suggestions, "two-point-line", source is null ? [] : [source]);
        AddCompatibleTool(snapshot, suggestions, "three-point-plane", source is null ? [] : [source]);
        AddCompatibleTool(
            snapshot,
            suggestions,
            "datum-plane-raw-height-deviation",
            source is not null && publishedPlane is not null && gridSelection is not null
                ? [source, publishedPlane, gridSelection]
                : []);
        AddCompatibleTool(
            snapshot,
            suggestions,
            "xyz-affine-apply",
            source is not null && publishedAffine is not null
                ? [source, publishedAffine]
                : []);
        AddCompatibleTool(
            snapshot,
            suggestions,
            "re-grid-height-map",
            publishedTransformed is null ? [] : [publishedTransformed]);
        CompatibleToolSuggestions.ReplaceAll(suggestions);

        SetCompatibleToolBlocker(
            snapshot,
            source,
            gridSelection,
            publishedFilter,
            publishedEdge);
        OnPropertyChanged(nameof(CompatibleToolCatalogSummary));
        NotifySelectedToolChanged();
        addCompatibleToolCommand.RaiseCanExecuteChanged();
    }

    public bool CanAddTool(ToolWorkbenchToolItem? requestedTool)
    {
        var snapshot = getSnapshot();
        var tool = requestedTool ?? snapshot.SelectedTool;
        return snapshot.IsSourceReadyForRecipe
            && tool is not null
            && GetProposedInputRoute(snapshot, tool).IsCompatible;
    }

    public ToolWorkbenchInputRouteProposal GetProposedInputRoute(
        ToolWorkbenchToolItem? tool)
    {
        var snapshot = getSnapshot();
        return GetProposedInputRoute(snapshot, tool);
    }

    public void NotifySelectedToolChanged()
    {
        OnPropertyChanged(nameof(IsSelectedToolProposedRouteCompatible));
        OnPropertyChanged(nameof(SelectedToolProposedRouteTitle));
        OnPropertyChanged(nameof(SelectedToolProposedRouteDetail));
    }

    private bool CanAddCompatibleTool(ToolWorkbenchCompatibleToolItem suggestion)
    {
        var snapshot = getSnapshot();
        return snapshot.IsSourceReadyForRecipe
            && suggestion.IsAvailable
            && CompatibleToolSuggestions.Contains(suggestion);
    }

    private void AddCompatibleTool(
        ToolWorkbenchCompatibleToolCatalogSnapshot snapshot,
        ICollection<ToolWorkbenchCompatibleToolItem> suggestions,
        string toolId,
        IReadOnlyList<ToolWorkbenchArtifactItem> inputArtifacts)
    {
        if (inputArtifacts.Count == 0)
        {
            return;
        }

        var tool = snapshot.Tools.FirstOrDefault(item =>
            string.Equals(item.Id, toolId, StringComparison.Ordinal));
        if (tool is null)
        {
            return;
        }

        var qualityGate = EvaluateSourceQualityGate(snapshot, tool, inputArtifacts);
        suggestions.Add(new ToolWorkbenchCompatibleToolItem(
            tool,
            string.Join("; ", inputArtifacts.Select(item => item.Id)),
            qualityGate.IsAllowed
                ? localization.FlowPortReady
                : localization.CompatibleToolBlocked,
            ReferenceEquals(tool, snapshot.SelectedTool))
        {
            IsAvailable = qualityGate.IsAllowed,
            BlockerReason = qualityGate.IsAllowed ? string.Empty : qualityGate.Detail
        });
    }

    private void SelectCompatibleTool(ToolWorkbenchCompatibleToolItem? suggestion)
    {
        if (suggestion is not null)
        {
            selectTool(suggestion.Tool);
        }
    }

    private void AddCompatibleTool(ToolWorkbenchCompatibleToolItem? suggestion)
    {
        if (suggestion is null
            || !suggestion.IsAvailable
            || !CompatibleToolSuggestions.Contains(suggestion))
        {
            return;
        }

        selectTool(suggestion.Tool);
        addToolToRecipe(suggestion.Tool, suggestion.InputArtifactIds);
    }

    private void SetCompatibleToolBlocker(
        ToolWorkbenchCompatibleToolCatalogSnapshot snapshot,
        ToolWorkbenchArtifactItem? source,
        ToolWorkbenchArtifactItem? gridSelection,
        ToolWorkbenchArtifactItem? publishedFilter,
        ToolWorkbenchArtifactItem? publishedEdge)
    {
        ToolWorkbenchToolItem? missingTool = null;
        if (source is not null)
        {
            missingTool = publishedFilter is null || gridSelection is null
                ? snapshot.Tools.FirstOrDefault(item =>
                    string.Equals(item.Id, "height-difference-edge", StringComparison.Ordinal))
                : publishedEdge is null
                    ? snapshot.Tools.FirstOrDefault(item =>
                        string.Equals(item.Id, "three-d-line-fit", StringComparison.Ordinal))
                    : null;
        }

        compatibleToolBlockerTitle = missingTool?.Name ?? string.Empty;
        compatibleToolBlockerDetail = missingTool is null
            ? string.Empty
            : string.Format(
                localization.CompatibleToolBlockerDetailFormat,
                missingTool.Name,
                missingTool.InputContract);
        OnPropertyChanged(nameof(HasCompatibleToolBlocker));
        OnPropertyChanged(nameof(CompatibleToolBlockerTitle));
        OnPropertyChanged(nameof(CompatibleToolBlockerDetail));
    }

    private ToolWorkbenchInputRouteProposal GetProposedInputRoute(
        ToolWorkbenchCompatibleToolCatalogSnapshot snapshot) =>
        GetProposedInputRoute(snapshot, snapshot.SelectedTool);

    private ToolWorkbenchInputRouteProposal GetProposedInputRoute(
        ToolWorkbenchCompatibleToolCatalogSnapshot snapshot,
        ToolWorkbenchToolItem? tool)
    {
        if (tool is null)
        {
            return new ToolWorkbenchInputRouteProposal(false, string.Empty, string.Empty);
        }

        if (string.Equals(tool.Id, "domain-mask", StringComparison.Ordinal))
        {
            var primary = FindCompatiblePrimaryInput(snapshot, tool);
            var domain = snapshot.ArtifactRegistry.FirstOrDefault(item =>
                string.Equals(item.Contract, "ConnectedRegionArtifact", StringComparison.Ordinal)
                && string.Equals(item.State, "Published", StringComparison.Ordinal)
                && primary is not null
                && string.Equals(
                    item.InputEntityIds,
                    primary.Id,
                    StringComparison.OrdinalIgnoreCase));
            if (primary is null || domain is null)
            {
                return new ToolWorkbenchInputRouteProposal(
                    false,
                    string.Empty,
                    "Domain / Mask requires a Published HeightField followed by its matching Published ConnectedRegionArtifact.");
            }

            var sourceGate = EvaluateSourceQualityGate(snapshot, tool, primary);
            var route = string.Join("; ", primary.Id, domain.Id);
            return new ToolWorkbenchInputRouteProposal(
                sourceGate.IsAllowed,
                route,
                sourceGate.IsAllowed
                    ? string.Format(
                        localization.ProposedToolRouteFormat,
                        route,
                        tool.InputContract,
                        tool.Name,
                        tool.OutputContract)
                    : sourceGate.Detail);
        }

        var sourceSuggestion = CompatibleToolSuggestions.FirstOrDefault(suggestion =>
            ReferenceEquals(suggestion.Tool, tool)
            && suggestion.InputArtifactIds
                .Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .Any(id => string.Equals(
                    id,
                    snapshot.Source.Id,
                    StringComparison.OrdinalIgnoreCase)));
        if (sourceSuggestion is not null
            && snapshot.ArtifactRegistry.FirstOrDefault(item =>
                string.Equals(
                    item.Id,
                    snapshot.Source.Id,
                    StringComparison.OrdinalIgnoreCase)) is { } sourceArtifact)
        {
            var sourceGate = EvaluateSourceQualityGate(snapshot, tool, sourceArtifact);
            return new ToolWorkbenchInputRouteProposal(
                sourceGate.IsAllowed,
                sourceArtifact.Id,
                sourceGate.IsAllowed
                    ? string.Format(
                        localization.ProposedToolRouteFormat,
                        sourceArtifact.Id,
                        sourceArtifact.Contract,
                        tool.Name,
                        tool.OutputContract)
                    : sourceGate.Detail);
        }

        if (ToolRecipePrimaryInputContract.TryGetRequiredContract(
            tool.Id,
            out var requiredContract))
        {
            var candidate = FindCompatiblePrimaryInput(snapshot, tool);
            if (candidate is null)
            {
                return new ToolWorkbenchInputRouteProposal(
                    false,
                    string.Empty,
                    string.Format(
                        localization.ProposedToolRouteUnavailableFormat,
                        requiredContract,
                        tool.OutputContract));
            }

            var sourceGate = EvaluateSourceQualityGate(snapshot, tool, candidate);
            return new ToolWorkbenchInputRouteProposal(
                sourceGate.IsAllowed,
                candidate.Id,
                sourceGate.IsAllowed
                    ? string.Format(
                        localization.ProposedToolRouteFormat,
                        candidate.Id,
                        candidate.Contract,
                        tool.Name,
                        tool.OutputContract)
                    : sourceGate.Detail);
        }

        var inputId = tool.Id == "landmark-correspondence"
            ? string.Empty
            : snapshot.PipelineSteps.LastOrDefault()?.OutputEntityId;
        if (tool.Id != "landmark-correspondence" && string.IsNullOrWhiteSpace(inputId))
        {
            inputId = snapshot.Source.Id;
        }

        var artifact = snapshot.ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Id, inputId, StringComparison.OrdinalIgnoreCase));
        var inputContract = artifact?.Contract ?? tool.InputContract;
        var qualityGate = EvaluateSourceQualityGate(snapshot, tool, artifact);
        return new ToolWorkbenchInputRouteProposal(
            qualityGate.IsAllowed,
            inputId ?? string.Empty,
            qualityGate.IsAllowed
                ? string.Format(
                    localization.ProposedToolRouteFormat,
                    string.IsNullOrWhiteSpace(inputId) ? localization.Input : inputId,
                    inputContract,
                    tool.Name,
                    tool.OutputContract)
                : qualityGate.Detail);
    }

    private static SourceQualityToolGateResult EvaluateSourceQualityGate(
        ToolWorkbenchCompatibleToolCatalogSnapshot snapshot,
        ToolWorkbenchToolItem tool,
        IReadOnlyList<ToolWorkbenchArtifactItem> inputArtifacts)
    {
        var source = inputArtifacts.FirstOrDefault(item => item.NodeKind == "Source");
        return EvaluateSourceQualityGate(snapshot, tool, source);
    }

    private static SourceQualityToolGateResult EvaluateSourceQualityGate(
        ToolWorkbenchCompatibleToolCatalogSnapshot snapshot,
        ToolWorkbenchToolItem tool,
        ToolWorkbenchArtifactItem? inputArtifact)
    {
        if (inputArtifact is null || inputArtifact.NodeKind != "Source")
        {
            return new SourceQualityToolGateResult(
                true,
                SourceQualityToolGateReason.NotApplicable,
                "Source Quality gate is not required for this typed input route.");
        }

        return SourceQualityToolGate.Evaluate(
            tool.Id,
            inputArtifact.Contract,
            snapshot.SourceQualityReport,
            snapshot.SourceQualityError,
            snapshot.Source.Id,
            inputArtifact.ContentSha256);
    }

    private static ToolWorkbenchArtifactItem? FindCompatiblePrimaryInput(
        ToolWorkbenchCompatibleToolCatalogSnapshot snapshot,
        ToolWorkbenchToolItem tool) =>
        snapshot.ArtifactRegistry
            .Where(item => item.NodeKind == "Source" || item.PipelineStep is not null)
            .Where(item => !string.Equals(
                    tool.Id,
                    "connected-region",
                    StringComparison.Ordinal)
                || item.PipelineStep?.ToolId == "remove-outlier-pixels"
                    && string.Equals(item.State, "Published", StringComparison.Ordinal))
            .Reverse()
            .FirstOrDefault(item =>
                ToolRecipePrimaryInputContract.IsCompatible(tool.Id, item.Contract));

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

}

internal sealed record ToolWorkbenchCompatibleToolCatalogSnapshot(
    IReadOnlyList<ToolWorkbenchToolItem> Tools,
    IReadOnlyList<ToolWorkbenchArtifactItem> ArtifactRegistry,
    IReadOnlyList<ToolWorkbenchPipelineStepItem> PipelineSteps,
    ToolWorkbenchCompatibleSourceSnapshot Source,
    ToolWorkbenchToolItem? SelectedTool,
    bool IsSourceReadyForRecipe,
    SourceQualityReport? SourceQualityReport,
    string SourceQualityError);

internal sealed record ToolWorkbenchCompatibleSourceSnapshot(string Id);

internal sealed record ToolWorkbenchInputRouteProposal(
    bool IsCompatible,
    string InputEntityIds,
    string Detail);

public sealed record ToolWorkbenchCompatibleToolItem(
    ToolWorkbenchToolItem Tool,
    string InputArtifactIds,
    string State,
    bool IsSelected)
{
    public bool IsAvailable { get; init; } = true;
    public string BlockerReason { get; init; } = string.Empty;
    public string Title => Tool.Name;
    public string InputContract => Tool.InputContract;
    public string Detail => IsAvailable
        ? $"{State} | {InputContract} ← {InputArtifactIds}"
        : $"{State} | {InputContract} ← {InputArtifactIds} | {BlockerReason}";
    public string AccessibleName => $"{Title}. {State}. {Detail}";
}
