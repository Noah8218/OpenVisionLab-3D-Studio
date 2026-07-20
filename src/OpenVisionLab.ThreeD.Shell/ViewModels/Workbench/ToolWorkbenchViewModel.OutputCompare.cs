using System.Collections.ObjectModel;
using System.IO;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Projects existing renderable artifacts into explicit, session-only compare slots.
/// This never changes a recipe route or invokes Preview/Publish.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private string compareSlotAArtifactId = string.Empty;
    private string compareSlotBArtifactId = string.Empty;
    private string compareSlotCArtifactId = string.Empty;

    public ObservableCollection<ToolWorkbenchCompareCandidateItem> CompareCandidates { get; } = [];

    public string CompareSlotAArtifactId
    {
        get => compareSlotAArtifactId;
        set => SetCompareSlot(ref compareSlotAArtifactId, value);
    }

    public string CompareSlotBArtifactId
    {
        get => compareSlotBArtifactId;
        set => SetCompareSlot(ref compareSlotBArtifactId, value);
    }

    public string CompareSlotCArtifactId
    {
        get => compareSlotCArtifactId;
        set => SetCompareSlot(ref compareSlotCArtifactId, value);
    }

    public string CompareSlotASummary => DescribeCompareSlot(CompareSlotAArtifactId);
    public string CompareSlotBSummary => DescribeCompareSlot(CompareSlotBArtifactId);
    public string CompareSlotCSummary => DescribeCompareSlot(CompareSlotCArtifactId);

    public ToolWorkbenchCompareCandidateItem? GetCompareCandidate(string? artifactId) =>
        string.IsNullOrWhiteSpace(artifactId)
            ? null
            : CompareCandidates.FirstOrDefault(item =>
                string.Equals(item.Id, artifactId, StringComparison.OrdinalIgnoreCase));

    private void RebuildOutputCompareCandidates()
    {
        var pinnedA = compareSlotAArtifactId;
        var pinnedB = compareSlotBArtifactId;
        var pinnedC = compareSlotCArtifactId;

        // The empty option makes every slot independently reversible without
        // adding a command that could affect the authored recipe.
        var candidates = new List<ToolWorkbenchCompareCandidateItem>
        {
            new(string.Empty, "—", string.Empty, string.Empty, string.Empty, string.Empty, false),
        };
        if (ArtifactRegistry.FirstOrDefault() is { } source
            && IsSourceReadyForRecipe
            && File.Exists(Source.Path))
        {
            candidates.Add(new ToolWorkbenchCompareCandidateItem(
                source.Id,
                source.DisplayName,
                source.Contract,
                source.State,
                Source.Path,
                source.Detail,
                true));
        }

        var filterPreviewPath = CurrentFilterPreviewPath;
        if (HasCurrentFilterPreview
            && !string.IsNullOrWhiteSpace(filterPreviewPath)
            && File.Exists(filterPreviewPath)
            && ArtifactRegistry.FirstOrDefault(item => string.Equals(
                item.Id,
                SelectedFilterOutputEntityId,
                StringComparison.OrdinalIgnoreCase)) is { } filter)
        {
            candidates.Add(new ToolWorkbenchCompareCandidateItem(
                filter.Id,
                filter.DisplayName,
                filter.Contract,
                filter.State,
                filterPreviewPath,
                filter.Detail,
                false));
        }

        CompareCandidates.Clear();
        foreach (var candidate in candidates)
        {
            CompareCandidates.Add(candidate);
        }

        // Replacing the item list temporarily clears WPF SelectedValue. Restore
        // explicit session pins after the new candidates are available.
        compareSlotAArtifactId = pinnedA;
        compareSlotBArtifactId = pinnedB;
        compareSlotCArtifactId = pinnedC;
        RefreshCompareSlotSummaries();
    }

    private string SelectedFilterOutputEntityId => PipelineSteps
        .FirstOrDefault(step => string.Equals(step.ToolId, "filter", StringComparison.OrdinalIgnoreCase))
        ?.OutputEntityId ?? string.Empty;

    private void SetCompareSlot(ref string field, string? value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(field, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        field = normalized;
        OnPropertyChanged(propertyName);
        RefreshCompareSlotSummaries();
        RefreshDisplayedOutputPresentation();
    }

    private void RefreshCompareSlotSummaries()
    {
        // Candidates are rebuilt after an explicit Preview. Re-notify the slot
        // values so WPF resolves a preselected ID against the new item list.
        OnPropertyChanged(nameof(CompareSlotAArtifactId));
        OnPropertyChanged(nameof(CompareSlotBArtifactId));
        OnPropertyChanged(nameof(CompareSlotCArtifactId));
        OnPropertyChanged(nameof(CompareSlotASummary));
        OnPropertyChanged(nameof(CompareSlotBSummary));
        OnPropertyChanged(nameof(CompareSlotCSummary));
    }

    private string DescribeCompareSlot(string artifactId) => GetCompareCandidate(artifactId) is { } candidate
        ? $"{candidate.Contract} | {candidate.State} | {candidate.Id}"
        : Localization.OutputCompareNoSelection;
}

public sealed record ToolWorkbenchCompareCandidateItem(
    string Id,
    string DisplayName,
    string Contract,
    string State,
    string C3DPath,
    string Detail,
    bool IsSource);
