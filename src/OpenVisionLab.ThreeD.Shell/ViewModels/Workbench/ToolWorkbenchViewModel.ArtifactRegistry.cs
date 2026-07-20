using System.Collections.ObjectModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Builds the read-first artifact and recipe navigation presentation from the existing
/// recipe session. It never executes a tool or mutates a route.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private ToolWorkbenchNavigatorItem? selectedNavigatorItem;

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

    public string SelectedRouteInputIds => SelectedPipelineStep is null
        ? string.Empty
        : string.Join("; ", SelectedPipelineStep.InputEntityIds);

    public string SelectedRouteOutputId => SelectedPipelineStep?.OutputEntityId ?? string.Empty;

    public bool IsSelectedToolLabAvailable => HasToolLab(SelectedPipelineStep?.ToolId);

    public string ArtifactRegistrySummary => ArtifactRegistry.Count == 0
        ? "No typed artifacts are registered."
        : $"{ArtifactRegistry.Count} typed entities | {ArtifactRegistry.Count(item => item.HasContentHash)} with current output identity";

    private void RebuildArtifactRegistryAndNavigator()
    {
        ArtifactRegistry.Clear();
        ArtifactRegistry.Add(CreateSourceArtifact());

        foreach (var selection in Selections)
        {
            ArtifactRegistry.Add(new ToolWorkbenchArtifactItem(
                selection.Id,
                selection.Name,
                selection.Kind,
                IsSelectionCurrent(selection) ? "Current selection" : "Stale",
                selection.RootSourceId,
                selection.RootSourceId,
                Source.Unit,
                selection.FrameId,
                selection.SourceBinding.ContentSha256,
                IsSelectionCurrent(selection)
                    ? "Recipe-owned teaching selection."
                    : "Recapture is required because the source binding changed.",
                null,
                "Selection"));
        }

        foreach (var step in PipelineSteps)
        {
            ArtifactRegistry.Add(CreateStepArtifact(step));
        }

        NavigatorRoots.Clear();

        var sourceRoot = new ToolWorkbenchNavigatorItem(
            "SourceRoot",
            "Source & references",
            SourceContextSummary,
            null);
        sourceRoot.Children.Add(CreateArtifactNode(ArtifactRegistry[0], null, "Source"));
        foreach (var reference in References)
        {
            sourceRoot.Children.Add(new ToolWorkbenchNavigatorItem(
                "Reference",
                reference.Name,
                $"{reference.Id} | {reference.Kind}",
                null));
        }
        NavigatorRoots.Add(sourceRoot);

        var pipelineRoot = new ToolWorkbenchNavigatorItem(
            "Pipeline",
            $"Recipe pipeline ({PipelineSteps.Count} steps)",
            "Ordered, read-first INPUT → OUTPUT teaching structure.",
            null);
        foreach (var step in PipelineSteps)
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
        NavigatorRoots.Add(pipelineRoot);

        if (Selections.Count > 0)
        {
            var selectionRoot = new ToolWorkbenchNavigatorItem(
                "Selections",
                $"Teaching selections ({Selections.Count})",
                "Recipe-owned source-bound captures.",
                null);
            foreach (var selection in ArtifactRegistry.Where(item => item.NodeKind == "Selection"))
            {
                selectionRoot.Children.Add(CreateArtifactNode(selection, null, "Selection"));
            }
            NavigatorRoots.Add(selectionRoot);
        }

        RefreshNavigatorSelection();
        RebuildOutputCompareCandidates();
        RebuildDisplayedOutputs();
        RebuildFlowPortDiagnostics();
        RebuildCompatibleToolCatalog();
        OnPropertyChanged(nameof(ArtifactRegistrySummary));
    }

    private ToolWorkbenchArtifactItem CreateSourceArtifact()
    {
        var sourceReady = IsSourceReadyForRecipe;
        return new ToolWorkbenchArtifactItem(
            Source.Id,
            Source.Name,
            "SourceC3D / RawHeightField",
            sourceReady ? "Ready" : string.IsNullOrWhiteSpace(Source.Path) ? "Source required" : "Needs repair",
            Source.Id,
            string.Empty,
            Source.Unit,
            Source.FrameId,
            loadedSourceBinding?.ContentSha256 ?? string.Empty,
            sourceReady
                ? $"{loadedSourceBinding!.GridWidth} × {loadedSourceBinding.GridHeight} verified C3D source."
                : SourceReadinessSummary,
            null,
            "Source");
    }

    private ToolWorkbenchArtifactItem CreateStepArtifact(ToolWorkbenchPipelineStepItem step)
    {
        if (string.Equals(step.ToolId, "filter", StringComparison.Ordinal)
            && filterPreviewOutput is not null)
        {
            return new ToolWorkbenchArtifactItem(
                filterPreviewOutput.EntityId,
                step.ToolName,
                "FilteredHeightField",
                isFilterPreviewStale ? "Stale" : isFilterPreviewPublished ? "Published" : "Preview",
                Source.Id,
                Source.Id,
                filterPreviewOutput.Unit,
                filterPreviewOutput.FrameId,
                filterPreviewOutput.ContentSha256,
                $"{filterPreviewOutput.Width} × {filterPreviewOutput.Height} | {filterPreviewOutput.Provenance}",
                step,
                "FilteredHeightField");
        }

        if (string.Equals(step.ToolId, "height-difference-edge", StringComparison.Ordinal)
            && edgePreviewOutput is not null
            && string.Equals(edgePreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                edgePreviewOutput.OutputEntityId,
                step.ToolName,
                "EdgePointSet",
                isEdgePreviewStale ? "Stale" : isEdgePreviewPublished ? "Published" : "Preview",
                edgePreviewOutput.RootSourceEntityId,
                edgePreviewOutput.InputEntityId,
                edgePreviewOutput.Unit,
                edgePreviewOutput.FrameId,
                edgePreviewOutput.ContentSha256,
                $"{edgePreviewOutput.Points.Count:N0} points | {edgePreviewOutput.Provenance}",
                step,
                "EdgePointSet");
        }

        if (string.Equals(step.ToolId, "three-d-line-fit", StringComparison.Ordinal)
            && TryGetPublishedLineFitOutput(step.OutputEntityId, out var publishedLine)
            && publishedLine is not null)
        {
            return new ToolWorkbenchArtifactItem(
                publishedLine.OutputEntityId,
                step.ToolName,
                "LineFeature",
                "Published",
                publishedLine.RootSourceEntityId,
                publishedLine.InputEdgePointSetEntityId,
                publishedLine.Unit,
                publishedLine.FrameId,
                publishedLine.ContentSha256,
                $"{publishedLine.Diagnostics.InlierCount:N0}/{publishedLine.Diagnostics.InputPointCount:N0} inliers | {publishedLine.Provenance}",
                step,
                "LineFeature");
        }

        if (string.Equals(step.ToolId, "line-intersection", StringComparison.Ordinal)
            && TryGetPublishedLineIntersectionOutput(step.OutputEntityId, out var publishedIntersection)
            && publishedIntersection is not null)
        {
            return new ToolWorkbenchArtifactItem(
                publishedIntersection.OutputEntityId,
                step.ToolName,
                "CornerAnchor",
                "Published",
                publishedIntersection.RootSourceEntityId,
                $"{publishedIntersection.FirstLineEntityId}; {publishedIntersection.SecondLineEntityId}",
                publishedIntersection.Unit,
                publishedIntersection.FrameId,
                publishedIntersection.ContentSha256,
                $"{publishedIntersection.OutputRole} | gap {publishedIntersection.ClosestApproachDistance:G6} | acute {publishedIntersection.AcuteAngleDegrees:G6} degrees",
                step,
                "CornerAnchor");
        }

        return new ToolWorkbenchArtifactItem(
            step.OutputEntityId,
            step.ToolName,
            step.OutputContract,
            "Declared",
            Source.Id,
            string.Join("; ", step.InputEntityIds),
            Source.Unit,
            Source.FrameId,
            string.Empty,
            $"Declared by {step.Id}. No Preview or Published output exists yet.",
            step,
            "DeclaredOutput");
    }

    private ToolWorkbenchNavigatorItem CreateArtifactNode(
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
            && !ReferenceEquals(SelectedPipelineStep, item.PipelineStep))
        {
            SelectedPipelineStep = item.PipelineStep;
            if (!ReferenceEquals(SelectedPipelineStep, item.PipelineStep))
            {
                return;
            }
        }

        SelectedNavigatorItem = item;
        RefreshNavigatorSelection();
    }

    private void RequestSelectedToolLab()
    {
        if (SelectedPipelineStep is { } step && HasToolLab(step.ToolId))
        {
            ToolLabRequested?.Invoke(this, new ToolWorkbenchToolLabRequestEventArgs(step.ToolId));
        }
    }

    private static bool HasToolLab(string? toolId) => toolId is "filter"
        or "height-difference-edge"
        or "line-intersection"
        or "landmark-correspondence";

    private void RefreshNavigatorSelection()
    {
        if (SelectedPipelineStep is not null
            && (SelectedNavigatorItem is null
                || !ReferenceEquals(SelectedNavigatorItem.PipelineStep, SelectedPipelineStep)))
        {
            SelectedNavigatorItem = EnumerateNavigatorItems(NavigatorRoots)
                .FirstOrDefault(item => item.NodeKind == "Step" && ReferenceEquals(item.PipelineStep, SelectedPipelineStep));
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
}

public sealed record ToolWorkbenchArtifactItem(
    string Id,
    string DisplayName,
    string Contract,
    string State,
    string RootSourceId,
    string InputEntityIds,
    string Unit,
    string FrameId,
    string ContentSha256,
    string Detail,
    ToolWorkbenchPipelineStepItem? PipelineStep,
    string NodeKind)
{
    public bool HasContentHash => ContentSha256.Length == 64;
    public string HashShortSuffix => HasContentHash ? $" | SHA {ContentSha256[..12]}" : string.Empty;
}

public sealed class ToolWorkbenchNavigatorItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool isCurrent;
    private bool isExpanded;

    public ToolWorkbenchNavigatorItem(
        string nodeKind,
        string title,
        string detail,
        ToolWorkbenchPipelineStepItem? pipelineStep)
    {
        NodeKind = nodeKind;
        Title = title;
        Detail = detail;
        PipelineStep = pipelineStep;
        isExpanded = nodeKind == "Pipeline"
            || nodeKind == "Step" && pipelineStep?.Order == "01";
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string NodeKind { get; }
    public string Title { get; }
    public string Detail { get; }
    public ToolWorkbenchPipelineStepItem? PipelineStep { get; }
    public ObservableCollection<ToolWorkbenchNavigatorItem> Children { get; } = [];
    public string AccessibleName => $"{Title}. {Detail}";
    public bool IsCurrent
    {
        get => isCurrent;
        internal set
        {
            if (isCurrent == value)
            {
                return;
            }

            isCurrent = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsCurrent)));
        }
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
            {
                return;
            }

            isExpanded = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }
}

public sealed class ToolWorkbenchToolLabRequestEventArgs(string toolId) : EventArgs
{
    public string ToolId { get; } = toolId;
}
