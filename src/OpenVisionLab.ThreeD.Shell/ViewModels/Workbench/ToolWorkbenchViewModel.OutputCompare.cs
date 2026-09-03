using System.ComponentModel;
using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;

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
    public string CompareSlotAQualitySummary => GetCompareSlotQualitySummary(CompareSlotAArtifactId);
    public string CompareSlotBQualitySummary => GetCompareSlotQualitySummary(CompareSlotBArtifactId);
    public string CompareSlotCQualitySummary => GetCompareSlotQualitySummary(CompareSlotCArtifactId);
    public bool HasCompareSlotAQualitySummary => HasCompareSlotQualitySummary(CompareSlotAArtifactId);
    public bool HasCompareSlotBQualitySummary => HasCompareSlotQualitySummary(CompareSlotBArtifactId);
    public bool HasCompareSlotCQualitySummary => HasCompareSlotQualitySummary(CompareSlotCArtifactId);
    public string PreparationQualityComparisonSummary =>
        TryGetCurrentPreparationQualityComparison(out var source, out var prepared, out _)
            ? string.Format(
                CultureInfo.InvariantCulture,
                Localization.OutputComparePreparationQualitySummaryFormat,
                source.DisplayName,
                prepared.DisplayName,
                GetCompareSlotQualitySummary(prepared.Id))
            : string.Empty;
    public bool HasPreparationQualityComparisonSummary =>
        TryGetCurrentPreparationQualityComparison(out _, out _, out _);

    public ToolWorkbenchCompareCandidateItem? GetCompareCandidate(string? artifactId) =>
        outputCompareSession.GetCompareCandidate(artifactId);

    private void InitializeOutputCompareSession()
    {
        outputCompareSession.PropertyChanged += OnOutputCompareSessionPropertyChanged;
        outputCompareSession.PinsChanged += (_, _) => RefreshDisplayedOutputPresentation();
    }

    private void OnOutputCompareSessionPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(args.PropertyName);
        if (args.PropertyName is nameof(CompareSlotAArtifactId)
            or nameof(CompareSlotBArtifactId)
            or nameof(CompareSlotCArtifactId))
        {
            NotifyCompareSlotQualityPresentation();
        }
    }

    private void OnOutputCompareLocalizationChanged(object? sender, PropertyChangedEventArgs args)
    {
        RefreshCompareSlotSummaries();
        NotifyCompareSlotQualityPresentation();
    }

    private void RebuildOutputCompareCandidates()
    {
        // The empty option makes every slot independently reversible without
        // adding a command that could affect the authored recipe.
        var candidates = new List<ToolWorkbenchCompareCandidateItem>
        {
            new(string.Empty, "—", string.Empty, string.Empty, string.Empty, string.Empty, false),
        };
        candidates.AddRange(RenderableC3DCatalog
            .Where(target => target.IsDisplayable)
            .Select(target => new ToolWorkbenchCompareCandidateItem(
                target.Id,
                target.DisplayName,
                target.Contract,
                target.State,
                target.C3DPath,
                target.Detail,
                target.IsSource,
                target.PreparationQualityDelta)));

        outputCompareSession.ReplaceCandidates(candidates, Localization.OutputCompareNoSelection);
    }

    private void RefreshCompareSlotSummaries() =>
        outputCompareSession.RefreshSummaries(Localization.OutputCompareNoSelection);

    /// <summary>
    /// Normalizes one current preparation artifact into the existing read-only
    /// source/output comparison surface. No authored recipe state is changed.
    /// </summary>
    internal bool TryOpenPreparationQualityComparison(ToolWorkbenchDisplayedOutputItem? item)
    {
        if (item is null
            || GetCompareCandidate(item.Id) is not { } prepared
            || !TryGetCurrentPreparationQualityDelta(prepared, out _)
            || GetCompareCandidate(Source.Id) is not { IsSource: true } source)
        {
            return false;
        }

        if (!ViewerWorkspace.TrySetLayout(
                ViewerWorkspaceLayout.SplitVertical,
                ViewerWorkspaceCandidates.Select(candidate => candidate.Id),
                source.Id))
        {
            return false;
        }

        MainViewerContentId = source.Id;
        AuxiliaryViewerContentId = prepared.Id;
        CompareSlotAArtifactId = source.Id;
        CompareSlotBArtifactId = prepared.Id;
        CompareSlotCArtifactId = string.Empty;
        OutputComparePaneRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private string GetCompareSlotQualitySummary(string? artifactId)
    {
        var candidate = GetCompareCandidate(artifactId);
        if (candidate is null)
        {
            return string.Empty;
        }

        if (candidate.IsSource)
        {
            return Localization.OutputCompareSourceBaseline;
        }

        return TryGetCurrentPreparationQualityDelta(candidate, out var delta)
            ? string.Format(
                CultureInfo.InvariantCulture,
                Localization.OutputCompareQualityDeltaSummaryFormat,
                delta.BeforeValidSampleCount,
                delta.BeforeMissingSampleCount,
                delta.AfterValidSampleCount,
                FormatSigned(delta.ValidSampleDelta),
                delta.AfterMissingSampleCount,
                FormatSigned(delta.MissingSampleDelta),
                DescribeOutliers(delta),
                Localization.OutputCompareSourceIdentityRetained)
            : string.Empty;
    }

    private bool HasCompareSlotQualitySummary(string? artifactId)
    {
        var candidate = GetCompareCandidate(artifactId);
        return candidate?.IsSource == true
            || TryGetCurrentPreparationQualityDelta(candidate, out _);
    }

    private bool TryGetCurrentPreparationQualityComparison(
        out ToolWorkbenchCompareCandidateItem source,
        out ToolWorkbenchCompareCandidateItem prepared,
        out SourceQualityDelta delta)
    {
        source = null!;
        prepared = null!;
        delta = null!;
        if (GetCompareCandidate(CompareSlotAArtifactId) is not { IsSource: true } sourceCandidate
            || !string.Equals(sourceCandidate.Id, Source.Id, StringComparison.OrdinalIgnoreCase)
            || GetCompareCandidate(CompareSlotBArtifactId) is not { IsSource: false } preparedCandidate
            || !TryGetCurrentPreparationQualityDelta(preparedCandidate, out var currentDelta)
            || !string.Equals(currentDelta.SourceEntityId, sourceCandidate.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        source = sourceCandidate;
        prepared = preparedCandidate;
        delta = currentDelta;
        return true;
    }

    private bool TryGetCurrentPreparationQualityDelta(
        ToolWorkbenchCompareCandidateItem? candidate,
        out SourceQualityDelta delta)
    {
        delta = null!;
        if (candidate is not { IsSource: false, PreparationQualityDelta: { } candidateDelta }
            || GetRenderableC3DTarget(candidate.Id) is not
            {
                IsSource: false,
                IsDisplayable: true,
                C3DPath: var preparedPath,
                State: var preparedState
            }
            || string.Equals(candidate.State, "Stale", StringComparison.OrdinalIgnoreCase)
            || string.Equals(preparedState, "Stale", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(preparedPath, candidate.C3DPath, StringComparison.OrdinalIgnoreCase)
            || !IsSourceReadyForRecipe
            || SourceSession.SourceBinding is not { ContentSha256: { Length: > 0 } sourceContentSha256 }
            || GetRenderableC3DTarget(Source.Id) is not
            {
                IsSource: true,
                IsDisplayable: true,
                C3DPath: var sourcePath
            }
            || !string.Equals(sourcePath, Source.Path, StringComparison.OrdinalIgnoreCase)
            )
        {
            return false;
        }

        var artifact = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Id, candidate.Id, StringComparison.OrdinalIgnoreCase));
        if (artifact is null
            || string.Equals(artifact.State, "Stale", StringComparison.OrdinalIgnoreCase)
            || !Equals(artifact.PreparationQualityDelta, candidateDelta)
            || !string.Equals(candidateDelta.SourceEntityId, Source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(candidateDelta.SourceContentSha256, sourceContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(candidateDelta.DerivedEntityId, artifact.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(candidateDelta.DerivedContentSha256, artifact.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.RootSourceId, Source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.Unit, Source.Unit, StringComparison.Ordinal)
            || !string.Equals(artifact.FrameId, Source.FrameId, StringComparison.Ordinal)
            || !candidateDelta.SourceIdentityRetained)
        {
            return false;
        }

        delta = candidateDelta;
        return true;
    }

    private string DescribeOutliers(SourceQualityDelta delta) =>
        delta.DetectedOutlierCount is { } count
            ? count.ToString("N0", CultureInfo.InvariantCulture)
            : Localization.OutputCompareOutliersNotEvaluated;

    private static string FormatSigned(long value) => value > 0
        ? $"+{value.ToString("N0", CultureInfo.InvariantCulture)}"
        : value.ToString("N0", CultureInfo.InvariantCulture);

    private void NotifyCompareSlotQualityPresentation()
    {
        OnPropertyChanged(nameof(CompareSlotAQualitySummary));
        OnPropertyChanged(nameof(CompareSlotBQualitySummary));
        OnPropertyChanged(nameof(CompareSlotCQualitySummary));
        OnPropertyChanged(nameof(HasCompareSlotAQualitySummary));
        OnPropertyChanged(nameof(HasCompareSlotBQualitySummary));
        OnPropertyChanged(nameof(HasCompareSlotCQualitySummary));
        OnPropertyChanged(nameof(PreparationQualityComparisonSummary));
        OnPropertyChanged(nameof(HasPreparationQualityComparisonSummary));
    }

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
    bool IsSource,
    SourceQualityDelta? PreparationQualityDelta = null);
