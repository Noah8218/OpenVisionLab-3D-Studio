using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the typed artifact registry, read-first navigator state, selection, and
/// navigator commands. Tool execution and recipe mutation stay with their
/// established owners.
/// </summary>
internal sealed class ToolWorkbenchArtifactNavigatorOwner : INotifyPropertyChanged
{
    private readonly object gate = new();
    private readonly ToolWorkbenchArtifactProjection projection;
    private readonly Func<ToolWorkbenchArtifactProjectionSnapshot> getSnapshot;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Action<ToolWorkbenchPipelineStepItem?> selectPipelineStep;
    private readonly Action<string> requestToolLab;
    private ToolWorkbenchNavigatorItem? selectedNavigatorItem;

    public ToolWorkbenchArtifactNavigatorOwner(
        ToolWorkbenchArtifactProjection projection,
        Func<ToolWorkbenchArtifactProjectionSnapshot> getSnapshot,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Action<ToolWorkbenchPipelineStepItem?> selectPipelineStep,
        Action<string> requestToolLab)
    {
        this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
        this.getSnapshot = getSnapshot ?? throw new ArgumentNullException(nameof(getSnapshot));
        this.getSelectedPipelineStep = getSelectedPipelineStep
            ?? throw new ArgumentNullException(nameof(getSelectedPipelineStep));
        this.selectPipelineStep = selectPipelineStep
            ?? throw new ArgumentNullException(nameof(selectPipelineStep));
        this.requestToolLab = requestToolLab
            ?? throw new ArgumentNullException(nameof(requestToolLab));

        SelectNavigatorItemCommand = new RelayCommand(
            parameter => SelectNavigatorItem(parameter as ToolWorkbenchNavigatorItem));
        OpenSelectedToolLabCommand = new RelayCommand(
            _ => RequestSelectedToolLab(),
            _ => IsSelectedToolLabAvailable);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? Rebuilt;

    public ResettableObservableCollection<ToolWorkbenchArtifactItem> ArtifactRegistry { get; } = [];

    public ResettableObservableCollection<ToolWorkbenchNavigatorItem> NavigatorRoots { get; } = [];

    public RelayCommand SelectNavigatorItemCommand { get; }

    public RelayCommand OpenSelectedToolLabCommand { get; }

    public ToolWorkbenchNavigatorItem? SelectedNavigatorItem
    {
        get => selectedNavigatorItem;
        private set
        {
            if (ReferenceEquals(selectedNavigatorItem, value))
            {
                return;
            }

            selectedNavigatorItem = value;
            OnPropertyChanged();
        }
    }

    public string SelectedRouteInputIds => getSelectedPipelineStep() is { } step
        ? string.Join("; ", step.InputEntityIds)
        : string.Empty;

    public string SelectedRouteOutputId => getSelectedPipelineStep()?.OutputEntityId ?? string.Empty;

    public bool IsSelectedToolLabAvailable => HasToolLab(getSelectedPipelineStep()?.ToolId);

    public string ArtifactRegistrySummary => ArtifactRegistry.Count == 0
        ? "No typed artifacts are registered."
        : $"{ArtifactRegistry.Count} typed entities | {ArtifactRegistry.Count(item => item.HasContentHash)} with current output identity";

    public void Rebuild()
    {
        lock (gate)
        {
            var snapshot = getSnapshot();
            ArtifactRegistry.ReplaceAll(projection.Project(snapshot));
            NavigatorRoots.ReplaceAll(CreateNavigatorRoots(snapshot));
            RefreshNavigatorSelection();
            Rebuilt?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged(nameof(ArtifactRegistrySummary));
        }
    }

    public void RefreshNavigatorSelection()
    {
        var selectedPipelineStep = getSelectedPipelineStep();
        if (selectedPipelineStep is not null
            && (SelectedNavigatorItem is null
                || !ReferenceEquals(SelectedNavigatorItem.PipelineStep, selectedPipelineStep)))
        {
            SelectedNavigatorItem = EnumerateNavigatorItems(NavigatorRoots)
                .FirstOrDefault(item => item.NodeKind == "Step" && ReferenceEquals(item.PipelineStep, selectedPipelineStep));
        }

        foreach (var item in EnumerateNavigatorItems(NavigatorRoots))
        {
            item.IsCurrent = ReferenceEquals(item, SelectedNavigatorItem);
        }

        if (SelectedNavigatorItem is not null)
        {
            SelectedNavigatorItem.IsExpanded = true;
        }
    }

    private IReadOnlyList<ToolWorkbenchNavigatorItem> CreateNavigatorRoots(
        ToolWorkbenchArtifactProjectionSnapshot snapshot)
    {
        var navigatorRoots = new List<ToolWorkbenchNavigatorItem>();

        var sourceRoot = new ToolWorkbenchNavigatorItem(
            "SourceRoot",
            "Source & references",
            snapshot.SourceContextSummary,
            null);
        sourceRoot.Children.Add(CreateArtifactNode(ArtifactRegistry[0], null, "Source"));
        foreach (var reference in snapshot.References)
        {
            sourceRoot.Children.Add(new ToolWorkbenchNavigatorItem(
                "Reference",
                reference.Name,
                $"{reference.Id} | {reference.Kind}",
                null));
        }
        navigatorRoots.Add(sourceRoot);

        var pipelineRoot = new ToolWorkbenchNavigatorItem(
            "Pipeline",
            $"Recipe pipeline ({snapshot.PipelineSteps.Count} steps)",
            "Ordered, read-first INPUT → OUTPUT teaching structure.",
            null);
        foreach (var step in snapshot.PipelineSteps)
        {
            var stepNode = new ToolWorkbenchNavigatorItem(
                "Step",
                $"{step.Order}  {step.ToolName}",
                $"{step.State} | {step.Id}",
                step);

            foreach (var inputId in step.InputEntityIds)
            {
                var input = ArtifactRegistry.FirstOrDefault(item =>
                    string.Equals(item.Id, inputId, StringComparison.OrdinalIgnoreCase));
                stepNode.Children.Add(input is null
                    ? new ToolWorkbenchNavigatorItem(
                        "Input",
                        $"Input: {inputId}",
                        "Unresolved input entity ID.",
                        step)
                    : CreateArtifactNode(input, step, "Input"));
            }

            var output = ArtifactRegistry.First(item =>
                string.Equals(item.Id, step.OutputEntityId, StringComparison.OrdinalIgnoreCase));
            stepNode.Children.Add(CreateArtifactNode(output, step, "Output"));
            pipelineRoot.Children.Add(stepNode);
        }
        navigatorRoots.Add(pipelineRoot);

        if (snapshot.Selections.Count > 0)
        {
            var selectionRoot = new ToolWorkbenchNavigatorItem(
                "Selections",
                $"Teaching selections ({snapshot.Selections.Count})",
                "Recipe-owned source-bound captures.",
                null);
            foreach (var selection in ArtifactRegistry.Where(item => item.NodeKind == "Selection"))
            {
                selectionRoot.Children.Add(CreateArtifactNode(selection, null, "Selection"));
            }
            navigatorRoots.Add(selectionRoot);
        }

        return navigatorRoots;
    }

    private static ToolWorkbenchNavigatorItem CreateArtifactNode(
        ToolWorkbenchArtifactItem artifact,
        ToolWorkbenchPipelineStepItem? pipelineStep,
        string role) => new(
            artifact.NodeKind,
            $"{role}: {artifact.DisplayName}",
            $"{artifact.Id} | {artifact.Contract} | {artifact.State}{artifact.HashShortSuffix}",
            pipelineStep ?? artifact.PipelineStep);

    private void SelectNavigatorItem(ToolWorkbenchNavigatorItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.PipelineStep is not null
            && !ReferenceEquals(getSelectedPipelineStep(), item.PipelineStep))
        {
            selectPipelineStep(item.PipelineStep);
            if (!ReferenceEquals(getSelectedPipelineStep(), item.PipelineStep))
            {
                return;
            }
        }

        SelectedNavigatorItem = item;
        RefreshNavigatorSelection();
    }

    private void RequestSelectedToolLab()
    {
        if (getSelectedPipelineStep() is { } step && HasToolLab(step.ToolId))
        {
            requestToolLab(step.ToolId);
        }
    }

    private static bool HasToolLab(string? toolId) => toolId is "filter"
        or "height-difference-edge"
        or "two-point-line"
        or "three-point-plane"
        or "datum-plane-raw-height-deviation"
        or "line-intersection"
        or "landmark-correspondence"
        or "xyz-affine-solve"
        or "xyz-affine-apply";

    private static IEnumerable<ToolWorkbenchNavigatorItem> EnumerateNavigatorItems(
        IEnumerable<ToolWorkbenchNavigatorItem> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in EnumerateNavigatorItems(root.Children))
            {
                yield return child;
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
