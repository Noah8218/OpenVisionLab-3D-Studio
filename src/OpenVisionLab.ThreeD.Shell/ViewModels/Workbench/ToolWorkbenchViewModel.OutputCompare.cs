using System.ComponentModel;
using System.IO;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Projects existing renderable artifacts into explicit, session-only compare slots.
/// This never changes a recipe route or invokes Preview/Publish.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private readonly ToolWorkbenchOutputCompareSession outputCompareSession = new();

    public ResettableObservableCollection<ToolWorkbenchCompareCandidateItem> CompareCandidates =>
        outputCompareSession.CompareCandidates;

    public string CompareSlotAArtifactId
    {
        get => outputCompareSession.CompareSlotAArtifactId;
        set => outputCompareSession.CompareSlotAArtifactId = value;
    }

    public string CompareSlotBArtifactId
    {
        get => outputCompareSession.CompareSlotBArtifactId;
        set => outputCompareSession.CompareSlotBArtifactId = value;
    }

    public string CompareSlotCArtifactId
    {
        get => outputCompareSession.CompareSlotCArtifactId;
        set => outputCompareSession.CompareSlotCArtifactId = value;
    }

    public string CompareSlotASummary => outputCompareSession.CompareSlotASummary;
    public string CompareSlotBSummary => outputCompareSession.CompareSlotBSummary;
    public string CompareSlotCSummary => outputCompareSession.CompareSlotCSummary;

    public ToolWorkbenchCompareCandidateItem? GetCompareCandidate(string? artifactId) =>
        outputCompareSession.GetCompareCandidate(artifactId);

    private void InitializeOutputCompareSession()
    {
        outputCompareSession.PropertyChanged += OnOutputCompareSessionPropertyChanged;
        outputCompareSession.PinsChanged += (_, _) => RefreshDisplayedOutputPresentation();
    }

    private void OnOutputCompareSessionPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    private void RebuildOutputCompareCandidates()
    {
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

        foreach (var sample in validationSetSamples.Where(sample => File.Exists(sample.SourcePath)))
        {
            candidates.Add(new ToolWorkbenchCompareCandidateItem(
                GetValidationSetCompareArtifactId(sample),
                $"{Localization.ValidationSet} #{sample.Order} · {sample.FileName}",
                "ValidationSample / C3D",
                sample.StatusText,
                sample.SourcePath,
                sample.Message,
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

        var outlierPreviewPath = CurrentRemoveOutlierPreviewPath;
        if (HasCurrentRemoveOutlierPreview
            && !string.IsNullOrWhiteSpace(outlierPreviewPath)
            && File.Exists(outlierPreviewPath)
            && CurrentRemoveOutlierPreviewOutput is { } outlierOutput
            && ArtifactRegistry.FirstOrDefault(item => string.Equals(
                item.Id,
                outlierOutput.EntityId,
                StringComparison.OrdinalIgnoreCase)) is { } outlierArtifact)
        {
            candidates.Add(new ToolWorkbenchCompareCandidateItem(
                outlierArtifact.Id,
                outlierArtifact.DisplayName,
                outlierArtifact.Contract,
                outlierArtifact.State,
                outlierPreviewPath,
                outlierArtifact.Detail,
                false));
        }

        outputCompareSession.ReplaceCandidates(candidates, Localization.OutputCompareNoSelection);
        ReconcileViewerWorkspaceContents();
    }

    private string SelectedFilterOutputEntityId => PipelineSteps
        .FirstOrDefault(step => string.Equals(step.ToolId, "filter", StringComparison.OrdinalIgnoreCase))
        ?.OutputEntityId ?? string.Empty;

    private void RefreshCompareSlotSummaries() =>
        outputCompareSession.RefreshSummaries(Localization.OutputCompareNoSelection);

    private static string GetValidationSetCompareArtifactId(ValidationSetSampleRow sample) =>
        $"validation.sample.{sample.Order}";

    private static bool IsValidationSetCompareArtifactId(string artifactId) =>
        artifactId.StartsWith("validation.sample.", StringComparison.OrdinalIgnoreCase);
}

public sealed record ToolWorkbenchCompareCandidateItem(
    string Id,
    string DisplayName,
    string Contract,
    string State,
    string C3DPath,
    string Detail,
    bool IsSource);
