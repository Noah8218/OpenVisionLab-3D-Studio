using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the session-only Displayed Outputs collection and presentation policy.
/// It consumes an artifact snapshot, the neutral renderable C3D catalog, and
/// the zero-preserving signed-delta viewer artifact; recipe and execution state
/// remain outside this owner.
/// </summary>
internal sealed class ToolWorkbenchDisplayedOutputsOwner : INotifyPropertyChanged
{
    private readonly Func<IReadOnlyList<ToolWorkbenchArtifactItem>>
        getArtifactSnapshot;
    private readonly Func<string?, ToolWorkbenchRenderableC3DTarget?>
        getRenderableTarget;
    private readonly Func<string?, C3DSignedHeightDeltaArtifact?>
        getSignedDeltaArtifact;
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
    private readonly Func<ToolWorkbenchPipelineStepItem?>? getSelectedStep;
    private readonly Func<string, bool>? isArtifactShown;
    private readonly Func<string, string?, bool>? showGalleryArtifact;
    private readonly Func<bool>? isInputOutputView;
    private readonly Action? restoreInputOutputView;
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
        : this(
            getArtifactSnapshot,
            getRenderableTarget,
            _ => null,
            localization,
            getComparePins,
            hasEmptyCompareSlot,
            tryPinToCompare,
            displayInPrimaryViewer,
            selectPipelineStep,
            refreshSelectedToolWorkspaceProjection)
    {
    }

    public ToolWorkbenchDisplayedOutputsOwner(
        Func<IReadOnlyList<ToolWorkbenchArtifactItem>> getArtifactSnapshot,
        Func<string?, ToolWorkbenchRenderableC3DTarget?> getRenderableTarget,
        Func<string?, C3DSignedHeightDeltaArtifact?> getSignedDeltaArtifact,
        ThreeDLocalization localization,
        Func<string, string> getComparePins,
        Func<bool> hasEmptyCompareSlot,
        Func<string, bool> tryPinToCompare,
        Action<string> displayInPrimaryViewer,
        Action<ToolWorkbenchPipelineStepItem> selectPipelineStep,
        Action refreshSelectedToolWorkspaceProjection,
        Func<ToolWorkbenchPipelineStepItem?>? getSelectedStep = null,
        Func<string, bool>? isArtifactShown = null,
        Func<string, string?, bool>? showGalleryArtifact = null,
        Func<bool>? isInputOutputView = null,
        Action? restoreInputOutputView = null)
    {
        this.getArtifactSnapshot = getArtifactSnapshot
            ?? throw new ArgumentNullException(nameof(getArtifactSnapshot));
        this.getRenderableTarget = getRenderableTarget
            ?? throw new ArgumentNullException(nameof(getRenderableTarget));
        this.getSignedDeltaArtifact = getSignedDeltaArtifact
            ?? throw new ArgumentNullException(nameof(getSignedDeltaArtifact));
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

        this.getSelectedStep = getSelectedStep;
        this.isArtifactShown = isArtifactShown;
        this.showGalleryArtifact = showGalleryArtifact;
        this.isInputOutputView = isInputOutputView;
        this.restoreInputOutputView = restoreInputOutputView;
        ShowGalleryInActivePaneCommand = CreateGalleryCommand(null);
        ShowGalleryInOtherPaneCommand = CreateGalleryCommand("other");
        ShowGalleryInLeftPaneCommand = CreateGalleryCommand(ViewerWorkspaceSession.MainSlotId);
        ShowGalleryInRightPaneCommand = CreateGalleryCommand(ViewerWorkspaceSession.AuxiliarySlotId);

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

    public RelayCommand ShowGalleryInActivePaneCommand { get; }
    public RelayCommand ShowGalleryInOtherPaneCommand { get; }
    public RelayCommand ShowGalleryInLeftPaneCommand { get; }
    public RelayCommand ShowGalleryInRightPaneCommand { get; }
    public int GalleryDataCount => DisplayedOutputs.Count(item => item.IsGalleryData);

    public string DisplayedOutputsSummary => DisplayedOutputs.Count == 0
        ? "No typed artifacts are registered."
        : string.Format(
            localization.DisplayedOutputsSummaryFormat,
            DisplayedOutputs.Count(item => item.IsRenderableInViewer),
            DisplayedOutputs.Count(item => item.IsEvidenceOnly));

    public string CurrentViewerOutputSummary =>
        DisplayedOutputs.FirstOrDefault(item => item.IsShownInViewer) is { } item
            ? $"{item.DisplayName} | {item.ContractDisplay}"
            : localization.DisplayedOutputsNoViewerSelection;

    private RelayCommand CreateGalleryCommand(string? slot) => new(parameter =>
    {
        if (parameter is not ToolWorkbenchDisplayedOutputItem { CanShowInViewer: true } item) return;
        if (TryGetDisplayableTarget(item.Id) is null && getSignedDeltaArtifact(item.Id) is null) return;
        if (slot is null && isInputOutputView?.Invoke() == true)
        {
            var step = item.PipelineStep ?? getArtifactSnapshot().Select(artifact => artifact.PipelineStep)
                .FirstOrDefault(candidate => candidate?.InputEntityIds.Contains(item.Id, StringComparer.OrdinalIgnoreCase) == true);
            if (step is not null && !ReferenceEquals(step, getSelectedStep?.Invoke())) selectPipelineStep(step);
            restoreInputOutputView?.Invoke();
        }
        if (TryGetDisplayableTarget(item.Id) is not null && showGalleryArtifact?.Invoke(item.Id, slot) == true)
        {
            RefreshPresentation();
        }
        else if (slot is null or "other" && getSignedDeltaArtifact(item.Id) is not null)
        {
            RequestDisplayedOutputInViewer(item);
        }
    }, parameter => parameter is ToolWorkbenchDisplayedOutputItem { CanShowInViewer: true } item
        && (showGalleryArtifact is not null && TryGetDisplayableTarget(item.Id) is not null
            || slot is null or "other" && getSignedDeltaArtifact(item.Id) is not null));

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
            || (TryGetDisplayableTarget(item.Id) is null
                && getSignedDeltaArtifact(item.Id) is null))
        {
            return;
        }

        var target = TryGetDisplayableTarget(item.Id);
        var signedDelta = getSignedDeltaArtifact(item.Id);
        var request = new ToolWorkbenchArtifactDisplayRequestEventArgs(
            item.Id,
            target?.C3DPath ?? string.Empty,
            item.DisplayName,
            item.Contract,
            target?.State ?? item.State,
            target?.IsSource ?? false,
            signedDelta);
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

    public void RefreshPresentation(bool refreshWorkspace = true)
    {
        // Source Quality may complete on a worker while the recipe projection
        // is being rebuilt. Refresh against a stable item snapshot so a
        // concurrent registry rebuild cannot invalidate this enumeration.
        foreach (var item in DisplayedOutputs.ToArray())
        {
            var c3DRenderable = TryGetDisplayableTarget(item.Id) is not null;
            var signedDelta = getSignedDeltaArtifact(item.Id) is not null;
            var isRenderable = c3DRenderable || signedDelta;
            var pins = getComparePins(item.Id);
            item.UpdatePresentation(
                isRenderable,
                string.Equals(item.Id, displayedViewerArtifactId, StringComparison.OrdinalIgnoreCase),
                pins,
                c3DRenderable && pins.Length == 0 && hasEmptyCompareSlot(),
                isRenderable
                    ? signedDelta
                        ? localization.DisplayableHeightImageData
                        : localization.DisplayableC3DData
                    : item.IsEvidenceOnly
                        ? localization.EvidenceOnlyOutput
                        : localization.NoCurrentDisplayableOutput,
                pins.Length == 0
                    ? string.Empty
                    : string.Format(localization.PinnedSlotsFormat, pins));
            item.UpdateGalleryRole(getSelectedStep?.Invoke(), isArtifactShown?.Invoke(item.Id) ?? item.IsShownInViewer);
        }

        OnPropertyChanged(nameof(GalleryDataCount));
        ShowGalleryInActivePaneCommand.RaiseCanExecuteChanged();
        ShowGalleryInOtherPaneCommand.RaiseCanExecuteChanged();
        ShowGalleryInLeftPaneCommand.RaiseCanExecuteChanged();
        ShowGalleryInRightPaneCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CurrentViewerOutputSummary));
        OnPropertyChanged(nameof(DisplayedOutputsSummary));
        if (refreshWorkspace) refreshSelectedToolWorkspaceProjection();
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
    bool isSource,
    C3DSignedHeightDeltaArtifact? signedDelta = null) : EventArgs
{
    public string ArtifactId { get; } = artifactId;
    public string C3DPath { get; } = c3DPath;
    public string DisplayName { get; } = displayName;
    public string Contract { get; } = contract;
    public string State { get; } = state;
    public bool IsSource { get; } = isSource;
    public C3DSignedHeightDeltaArtifact? SignedDelta { get; } = signedDelta;
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
    private string galleryRole = string.Empty;
    private bool isShownInGallery;

    public ToolWorkbenchDisplayedOutputItem(ToolWorkbenchArtifactItem artifact)
    {
        Artifact = artifact;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ToolWorkbenchArtifactItem Artifact { get; }
    public string Id => Artifact.Id;
    public string DisplayName => Artifact.DisplayName;
    public bool IsGalleryData => IsImageData || Contract is "ConnectedRegionArtifact" or "EditableRegionArtifact";
    public string GalleryName => NodeKind == "Source" ? Label("원본 높이 맵", "Original height map") : PipelineStep?.ToolId switch
    {
        "remove-outlier-pixels" => Label("정리된 높이 맵", "Cleaned height map"),
        "level-surface" => Label("수평 높이 맵", "Leveled height map"),
        "datum-plane-raw-height-deviation" => Label("평면 편차 맵", "Plane deviation map"),
        "filter" => Label("필터 높이 맵", "Filtered height map"),
        "height-threshold-background-removal" => Label("남은 높이 맵", "Remaining height map"),
        "signed-delta-background-subtraction" => Label("높이 차이 맵", "Height difference map"),
        "connected-region" or "editable-region" => Label("영역", "Region"),
        _ => DisplayName
    };
    public string GalleryProducer => PipelineStep is { } step ? $"{step.Order} {(step.ToolName == step.Tool.Name ? step.Tool.DisplayName : step.ToolName)}" : Label("3D 입력", "3D input");
    public string GalleryMetadata => Artifact.ValidSampleCount is { } valid && Artifact.MissingSampleCount is { } missing && valid + missing > 0
        ? $"{GalleryProducer} · {Label("결측", "Missing")} {100.0 * missing / (valid + missing):0.0}%"
        : $"{GalleryProducer} · {StateDisplay}";
    public string GalleryRole => galleryRole;
    public bool IsShownInGallery => isShownInGallery;
    public bool IsRelatedToSelectedStep => galleryRole.Length > 0;
    public string GalleryLeftLabel => $"{GalleryName} {Label("왼쪽 창에 표시", "Show in left pane")}";
    public string GalleryRightLabel => $"{GalleryName} {Label("오른쪽 창에 표시", "Show in right pane")}";
    public string GalleryPreviewSummary => $"{(Artifact.GridWidth is { } width && Artifact.GridHeight is { } height ? $"{width} × {height} · " : string.Empty)}{Artifact.Unit}" + Environment.NewLine +
        $"{Label("높이", "Height")} {FormatValue(Artifact.Minimum)} – {FormatValue(Artifact.Maximum)} · {Label("유효", "Valid")} {FormatCount(Artifact.ValidSampleCount)} · {Label("결측", "Missing")} {FormatCount(Artifact.MissingSampleCount)}" + Environment.NewLine +
        $"{StateDisplay} · {Availability}";
    public string GalleryIdentitySummary => $"{GalleryMetadata}{Environment.NewLine}{DisplayName}{Environment.NewLine}{Id} · {ContractDisplay} · {Artifact.Unit}";
    public string Contract => Artifact.Contract;
    public string ContractDisplay => ThreeDLocalization.Shared.ContractLabel(Contract);
    public string State => Artifact.State;
    public string StateDisplay => ThreeDLocalization.Shared.SelectedToolOutputStateLabel(State);
    public string Detail => Artifact.Detail;
    public string NodeKind => Artifact.NodeKind;
    public bool IsImageData => NodeKind is "Source" or "HeightField" or "FilteredHeightField" or "LeveledHeightField"
        || Contract is "HeightField" or "FilteredHeightField" or "SignedDeltaHeightField";
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

    private static string Label(string korean, string english) => ThreeDLocalization.Shared.Resolve("ThreeD.Data." + english, korean, english);
    private static string FormatValue(double? value) => value is { } number && double.IsFinite(number) ? number.ToString("0.##") : "—";
    private static string FormatCount(long? value) => value?.ToString("N0") ?? "—";

    internal void UpdateGalleryRole(ToolWorkbenchPipelineStepItem? selectedStep, bool? shown = null)
    {
        SetField(ref isShownInGallery, shown ?? isShownInViewer, nameof(IsShownInGallery));
        var role = selectedStep?.InputEntityIds.Contains(Id, StringComparer.OrdinalIgnoreCase) == true ? Label("입력", "Input")
            : string.Equals(selectedStep?.OutputEntityId, Id, StringComparison.OrdinalIgnoreCase) ? Label("출력", "Output") : string.Empty;
        SetField(ref galleryRole, role, nameof(GalleryRole));
        OnPropertyChanged(nameof(IsRelatedToSelectedStep));
        OnPropertyChanged(nameof(GalleryName));
        OnPropertyChanged(nameof(GalleryProducer));
        OnPropertyChanged(nameof(GalleryMetadata));
        OnPropertyChanged(nameof(GalleryPreviewSummary));
        OnPropertyChanged(nameof(GalleryLeftLabel));
        OnPropertyChanged(nameof(GalleryRightLabel));
    }

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
        SetField(ref isShownInGallery, shownInViewer, nameof(IsShownInGallery));
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
        OnPropertyChanged(nameof(ContractDisplay));
        OnPropertyChanged(nameof(StateDisplay));
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
