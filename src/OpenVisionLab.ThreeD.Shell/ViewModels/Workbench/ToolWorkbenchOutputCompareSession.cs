using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns session-only Output Compare candidates, pins, and summaries.
/// Candidate discovery remains with the Workbench because it depends on the
/// current recipe, validation samples, and artifact registry.
/// </summary>
internal sealed class ToolWorkbenchOutputCompareSession : INotifyPropertyChanged
{
    private string compareSlotAArtifactId = string.Empty;
    private string compareSlotBArtifactId = string.Empty;
    private string compareSlotCArtifactId = string.Empty;
    private string noSelectionText = "No output pinned";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? PinsChanged;

    public ResettableObservableCollection<ToolWorkbenchCompareCandidateItem> CompareCandidates { get; } = [];

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

    public bool HasEmptyCompareSlot =>
        string.IsNullOrWhiteSpace(CompareSlotAArtifactId)
        || string.IsNullOrWhiteSpace(CompareSlotBArtifactId)
        || string.IsNullOrWhiteSpace(CompareSlotCArtifactId);

    public string GetComparePins(string artifactId)
    {
        var slots = new List<string>(3);
        if (string.Equals(
                CompareSlotAArtifactId,
                artifactId,
                StringComparison.OrdinalIgnoreCase))
        {
            slots.Add("A");
        }

        if (string.Equals(
                CompareSlotBArtifactId,
                artifactId,
                StringComparison.OrdinalIgnoreCase))
        {
            slots.Add("B");
        }

        if (string.Equals(
                CompareSlotCArtifactId,
                artifactId,
                StringComparison.OrdinalIgnoreCase))
        {
            slots.Add("C");
        }

        return slots.Count == 0 ? string.Empty : string.Join(", ", slots);
    }

    public bool TryPin(string artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(CompareSlotAArtifactId))
        {
            CompareSlotAArtifactId = artifactId;
            return true;
        }

        if (string.IsNullOrWhiteSpace(CompareSlotBArtifactId))
        {
            CompareSlotBArtifactId = artifactId;
            return true;
        }

        if (string.IsNullOrWhiteSpace(CompareSlotCArtifactId))
        {
            CompareSlotCArtifactId = artifactId;
            return true;
        }

        return false;
    }

    public ToolWorkbenchCompareCandidateItem? GetCompareCandidate(string? artifactId) =>
        string.IsNullOrWhiteSpace(artifactId)
            ? null
            : CompareCandidates.FirstOrDefault(item =>
                string.Equals(item.Id, artifactId, StringComparison.OrdinalIgnoreCase));

    public void ReplaceCandidates(
        IEnumerable<ToolWorkbenchCompareCandidateItem> candidates,
        string localizedNoSelectionText)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var pinnedA = compareSlotAArtifactId;
        var pinnedB = compareSlotBArtifactId;
        var pinnedC = compareSlotCArtifactId;

        noSelectionText = localizedNoSelectionText;
        CompareCandidates.ReplaceAll(candidates);

        // WPF SelectedValue may clear during the Reset notification. Restore
        // the explicit session pins after the replacement candidates exist.
        compareSlotAArtifactId = pinnedA;
        compareSlotBArtifactId = pinnedB;
        compareSlotCArtifactId = pinnedC;
        NotifySlotValuesAndSummaries();
    }

    public void RefreshSummaries(string localizedNoSelectionText)
    {
        noSelectionText = localizedNoSelectionText;
        OnPropertyChanged(nameof(CompareSlotASummary));
        OnPropertyChanged(nameof(CompareSlotBSummary));
        OnPropertyChanged(nameof(CompareSlotCSummary));
    }

    private void SetCompareSlot(
        ref string field,
        string? value,
        [CallerMemberName] string? propertyName = null)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(field, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        field = normalized;
        OnPropertyChanged(propertyName);
        RefreshSummaries(noSelectionText);
        PinsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifySlotValuesAndSummaries()
    {
        OnPropertyChanged(nameof(CompareSlotAArtifactId));
        OnPropertyChanged(nameof(CompareSlotBArtifactId));
        OnPropertyChanged(nameof(CompareSlotCArtifactId));
        RefreshSummaries(noSelectionText);
    }

    private string DescribeCompareSlot(string artifactId) => GetCompareCandidate(artifactId) is { } candidate
        ? $"{candidate.Contract} | {candidate.State} | {candidate.Id}"
        : noSelectionText;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
