using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the session-only Displayed Outputs collection and presentation policy.
/// It consumes an artifact snapshot and the neutral renderable C3D catalog;
/// recipe and execution state remain outside this owner.
/// </summary>
internal sealed class ToolWorkbenchDisplayedOutputsOwner : INotifyPropertyChanged
{
    private readonly Func<IReadOnlyList<ToolWorkbenchArtifactItem>>
        getArtifactSnapshot;
    private readonly Func<string?, ToolWorkbenchRenderableC3DTarget?>
        getRenderableTarget;
    private readonly ThreeDLocalization localization;
    private readonly Func<string, string> getComparePins;
    private readonly Func<bool> hasEmptyCompareSlot;
    private readonly Func<string, bool> tryPinToCompare;
    private readonly Action<string> displayInPrimaryViewer;
    private readonly Action<ToolWorkbenchPipelineStepItem> selectPipelineStep;
    private readonly Action refreshSelectedToolWorkspaceProjection;
    private readonly RelayCommand showDisplayedOutputInViewerCommand;
    private readonly RelayCommand pinDisplayedOutputToCompareCommand;
    private readonly RelayCommand focusDisplayedOutputStepCommand;
    private string displayedViewerArtifactId = string.Empty;

    public ToolWorkbenchDisplayedOutputsOwner(
        Func<IReadOnlyList<ToolWorkbenchArtifactItem>> getArtifactSnapshot,
        Func<string?, ToolWorkbenchRenderableC3DTarget?> getRenderableTarget,
        ThreeDLocalization localization,
        Func<string, string> getComparePins,
        Func<bool> hasEmptyCompareSlot,
        Func<string, bool> tryPinToCompare,
        Action<string> displayInPrimaryViewer,
        Action<ToolWorkbenchPipelineStepItem> selectPipelineStep,
        Action refreshSelectedToolWorkspaceProjection)
    {
        this.getArtifactSnapshot = getArtifactSnapshot
            ?? throw new ArgumentNullException(nameof(getArtifactSnapshot));
        this.getRenderableTarget = getRenderableTarget
            ?? throw new ArgumentNullException(nameof(getRenderableTarget));
        this.localization = localization
            ?? throw new ArgumentNullException(nameof(localization));
        this.getComparePins = getComparePins
            ?? throw new ArgumentNullException(nameof(getComparePins));
        this.hasEmptyCompareSlot = hasEmptyCompareSlot
            ?? throw new ArgumentNullException(nameof(hasEmptyCompareSlot));
        this.tryPinToCompare = tryPinToCompare
            ?? throw new ArgumentNullException(nameof(tryPinToCompare));
        this.displayInPrimaryViewer = displayInPrimaryViewer
            ?? throw new ArgumentNullException(nameof(displayInPrimaryViewer));
        this.selectPipelineStep = selectPipelineStep
            ?? throw new ArgumentNullException(nameof(selectPipelineStep));
        this.refreshSelectedToolWorkspaceProjection =
            refreshSelectedToolWorkspaceProjection
            ?? throw new ArgumentNullException(
                nameof(refreshSelectedToolWorkspaceProjection));

        showDisplayedOutputInViewerCommand = new RelayCommand(
            parameter => RequestDisplayedOutputInViewer(
                parameter as ToolWorkbenchDisplayedOutputItem),
            parameter => parameter is ToolWorkbenchDisplayedOutputItem
                { CanShowInViewer: true });
        pinDisplayedOutputToCompareCommand = new RelayCommand(
            parameter => PinDisplayedOutputToCompare(
                parameter as ToolWorkbenchDisplayedOutputItem),
            parameter => parameter is ToolWorkbenchDisplayedOutputItem
                { CanPinToCompare: true });
        focusDisplayedOutputStepCommand = new RelayCommand(
            parameter => FocusDisplayedOutputStep(
                parameter as ToolWorkbenchDisplayedOutputItem),
            parameter => parameter is ToolWorkbenchDisplayedOutputItem
                { CanFocusStep: true });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<ToolWorkbenchArtifactDisplayRequestEventArgs>?
        ViewerArtifactDisplayRequested;

    public ResettableObservableCollection<ToolWorkbenchDisplayedOutputItem>
        DisplayedOutputs { get; } = [];

    public RelayCommand ShowDisplayedOutputInViewerCommand =>
        showDisplayedOutputInViewerCommand;

    public RelayCommand PinDisplayedOutputToCompareCommand =>
        pinDisplayedOutputToCompareCommand;

    public RelayCommand FocusDisplayedOutputStepCommand =>
        focusDisplayedOutputStepCommand;

    public string DisplayedOutputsSummary => DisplayedOutputs.Count == 0
        ? "No typed artifacts are registered."
        : string.Format(
            localization.DisplayedOutputsSummaryFormat,
            DisplayedOutputs.Count(item => item.IsRenderableInViewer),
            DisplayedOutputs.Count(item => item.IsEvidenceOnly));

    public string CurrentViewerOutputSummary =>
        DisplayedOutputs.FirstOrDefault(item => item.IsShownInViewer) is { } item
            ? $"{item.DisplayName} | {item.Contract}"
            : localization.DisplayedOutputsNoViewerSelection;

    public void Rebuild()
    {
        var artifacts = getArtifactSnapshot()
                        ?? throw new InvalidOperationException(
                            "Displayed Outputs artifact snapshot is unavailable.");
        DisplayedOutputs.ReplaceAll(
            artifacts.Select(artifact => new ToolWorkbenchDisplayedOutputItem(
                artifact)));

        if (!DisplayedOutputs.Any(item => string.Equals(
                item.Id,
                displayedViewerArtifactId,
                StringComparison.OrdinalIgnoreCase)))
        {
            displayedViewerArtifactId = string.Empty;
        }

        RefreshPresentation();
        OnPropertyChanged(nameof(DisplayedOutputsSummary));
    }

    public void RequestDisplayedOutputInViewer(
        ToolWorkbenchDisplayedOutputItem? item)
    {
        if (item is null
            || !item.CanShowInViewer
            || TryGetDisplayableTarget(item.Id) is not { } target)
        {
            return;
        }

        var request = new ToolWorkbenchArtifactDisplayRequestEventArgs(
            item.Id,
            target.C3DPath,
            item.DisplayName,
            item.Contract,
            target.State,
            target.IsSource);
        ViewerArtifactDisplayRequested?.Invoke(this, request);
        if (!request.WasDisplayed)
        {
            return;
        }

        displayInPrimaryViewer(item.Id);
        displayedViewerArtifactId = item.Id;
        RefreshPresentation();
        refreshSelectedToolWorkspaceProjection();
    }

    public void PinDisplayedOutputToCompare(
        ToolWorkbenchDisplayedOutputItem? item)
    {
        if (item is null || !item.CanPinToCompare)
        {
            return;
        }

        tryPinToCompare(item.Id);
    }

    public void FocusDisplayedOutputStep(
        ToolWorkbenchDisplayedOutputItem? item)
    {
        if (item?.PipelineStep is not { } step)
        {
            return;
        }

        selectPipelineStep(step);
        refreshSelectedToolWorkspaceProjection();
    }

    public void RefreshPresentation()
    {
        // Source Quality may complete on a worker while the recipe projection
        // is being rebuilt. Refresh against a stable item snapshot so a
        // concurrent registry rebuild cannot invalidate this enumeration.
        foreach (var item in DisplayedOutputs.ToArray())
        {
            var isRenderable = TryGetDisplayableTarget(item.Id) is not null;
            var pins = getComparePins(item.Id);
            item.UpdatePresentation(
                isRenderable,
                string.Equals(
                    item.Id,
                    displayedViewerArtifactId,
                    StringComparison.OrdinalIgnoreCase),
                pins,
                isRenderable && pins.Length == 0 && hasEmptyCompareSlot(),
                isRenderable
                    ? localization.DisplayableC3DData
                    : item.IsEvidenceOnly
                        ? localization.EvidenceOnlyOutput
                        : localization.NoCurrentDisplayableOutput,
                pins.Length == 0
                    ? string.Empty
                    : string.Format(localization.PinnedSlotsFormat, pins));
        }

        OnPropertyChanged(nameof(CurrentViewerOutputSummary));
        OnPropertyChanged(nameof(DisplayedOutputsSummary));
        refreshSelectedToolWorkspaceProjection();
    }

    private ToolWorkbenchRenderableC3DTarget? TryGetDisplayableTarget(string id)
    {
        var target = getRenderableTarget(id);
        return target is { IsDisplayable: true }
               && !string.IsNullOrWhiteSpace(target.C3DPath)
            ? target
            : null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// View boundary request for a verified C3D file already registered by the
/// workbench. The receiving View reports whether it actually displayed it.
/// </summary>
public sealed class ToolWorkbenchArtifactDisplayRequestEventArgs(
    string artifactId,
    string c3DPath,
    string displayName,
    string contract,
    string state,
    bool isSource) : EventArgs
{
    public string ArtifactId { get; } = artifactId;
    public string C3DPath { get; } = c3DPath;
    public string DisplayName { get; } = displayName;
    public string Contract { get; } = contract;
    public string State { get; } = state;
    public bool IsSource { get; } = isSource;
    public bool WasDisplayed { get; set; }
}

public sealed class ToolWorkbenchDisplayedOutputItem : INotifyPropertyChanged
{
    private bool isRenderableInViewer;
    private bool isShownInViewer;
    private string comparePins = string.Empty;
    private string comparePinsSummary = string.Empty;
    private string availability = string.Empty;
    private bool canPinToCompare;

    public ToolWorkbenchDisplayedOutputItem(ToolWorkbenchArtifactItem artifact)
    {
        Artifact = artifact;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ToolWorkbenchArtifactItem Artifact { get; }
    public string Id => Artifact.Id;
    public string DisplayName => Artifact.DisplayName;
    public string Contract => Artifact.Contract;
    public string State => Artifact.State;
    public string Detail => Artifact.Detail;
    public string NodeKind => Artifact.NodeKind;
    public ToolWorkbenchPipelineStepItem? PipelineStep => Artifact.PipelineStep;
    public bool CanFocusStep => PipelineStep is not null;
    public bool IsRenderableInViewer => isRenderableInViewer;
    public bool IsShownInViewer => isShownInViewer;
    public bool CanShowInViewer => isRenderableInViewer;
    public bool CanPinToCompare => canPinToCompare;
    public bool IsPinnedToCompare => comparePins.Length > 0;
    public string ComparePins => comparePins;
    public bool IsEvidenceOnly => !isRenderableInViewer
                                  && Artifact.HasContentHash
                                  && !string.Equals(
                                      Artifact.State,
                                      "Stale",
                                      StringComparison.OrdinalIgnoreCase)
                                  && Artifact.NodeKind is not "Source"
                                      and not "Selection"
                                      and not "DeclaredOutput";
    public bool HasNoCurrentOutput => !isRenderableInViewer && !IsEvidenceOnly;
    public string ComparePinsSummary => comparePinsSummary;
    public string Availability => availability;

    internal void UpdatePresentation(
        bool renderable,
        bool shownInViewer,
        string newComparePins,
        bool mayPinToCompare,
        string newAvailability,
        string newComparePinsSummary)
    {
        SetField(ref isRenderableInViewer, renderable, nameof(IsRenderableInViewer));
        OnPropertyChanged(nameof(CanShowInViewer));
        SetField(ref isShownInViewer, shownInViewer, nameof(IsShownInViewer));
        if (!string.Equals(comparePins, newComparePins, StringComparison.Ordinal))
        {
            comparePins = newComparePins;
            OnPropertyChanged(nameof(ComparePins));
            OnPropertyChanged(nameof(IsPinnedToCompare));
        }

        SetField(ref canPinToCompare, mayPinToCompare, nameof(CanPinToCompare));
        SetField(ref availability, newAvailability, nameof(Availability));
        SetField(
            ref comparePinsSummary,
            newComparePinsSummary,
            nameof(ComparePinsSummary));
        OnPropertyChanged(nameof(IsEvidenceOnly));
        OnPropertyChanged(nameof(HasNoCurrentOutput));
        OnPropertyChanged(nameof(Availability));
    }

    private void SetField(ref bool field, bool value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void SetField(ref string field, string value, string propertyName)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
