using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns Viewer candidate projection, layout and pin commands, camera linking,
/// and binding notifications. ViewerWorkspaceSession stores presentation state;
/// supplied source, artifact, and compare snapshots remain read-only inputs.
/// </summary>
internal sealed class ViewerWorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    public const string HeightImageViewerContentId = "viewer.height-image";

    private readonly ViewerWorkspaceSession viewerWorkspace;
    private readonly InspectionWorkspaceSelectionSession workspaceSelection;
    private readonly ThreeDLocalization localization;
    private readonly Func<(bool IsReady, string Path)> getSource;
    private readonly Func<IReadOnlyList<ToolWorkbenchRenderableC3DTarget>> getRenderableTargets;
    private readonly Func<(string A, string B, string C)> getCompareSlots;
    private readonly ToolWorkbenchViewerWorkspaceEventCoordinator sessionEventCoordinator;
    private readonly RelayCommand setSingleViewerLayoutCommand;
    private readonly RelayCommand splitViewerVerticallyCommand;
    private readonly RelayCommand splitViewerHorizontallyCommand;
    private readonly RelayCommand popOutViewerCommand;
    private readonly RelayCommand focusViewerWorkspaceSlotCommand;
    private readonly RelayCommand openHeightImageCommand;
    private readonly RelayCommand clearMainViewerPinCommand;
    private readonly RelayCommand clearAuxiliaryViewerPinCommand;
    private readonly RelayCommand toggleViewerCameraLinkCommand;

    public ViewerWorkspaceViewModel(
        ViewerWorkspaceSession viewerWorkspace,
        InspectionWorkspaceSelectionSession workspaceSelection,
        ThreeDLocalization localization,
        Func<(bool IsReady, string Path)> getSource,
        Func<IReadOnlyList<ToolWorkbenchRenderableC3DTarget>> getRenderableTargets,
        Func<(string A, string B, string C)> getCompareSlots)
    {
        this.viewerWorkspace = viewerWorkspace ?? throw new ArgumentNullException(nameof(viewerWorkspace));
        this.workspaceSelection = workspaceSelection ?? throw new ArgumentNullException(nameof(workspaceSelection));
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        this.getSource = getSource ?? throw new ArgumentNullException(nameof(getSource));
        this.getRenderableTargets = getRenderableTargets ?? throw new ArgumentNullException(nameof(getRenderableTargets));
        this.getCompareSlots = getCompareSlots ?? throw new ArgumentNullException(nameof(getCompareSlots));

        setSingleViewerLayoutCommand = new RelayCommand(
            _ => SetViewerWorkspaceLayout(ViewerWorkspaceLayout.Single));
        splitViewerVerticallyCommand = new RelayCommand(
            _ => SetViewerWorkspaceLayout(ViewerWorkspaceLayout.SplitVertical),
            _ => CanOpenAuxiliaryViewer());
        splitViewerHorizontallyCommand = new RelayCommand(
            _ => SetViewerWorkspaceLayout(ViewerWorkspaceLayout.SplitHorizontal),
            _ => CanOpenAuxiliaryViewer());
        popOutViewerCommand = new RelayCommand(
            _ => SetViewerWorkspaceLayout(ViewerWorkspaceLayout.PopOut),
            _ => CanOpenAuxiliaryViewer());
        openHeightImageCommand = new RelayCommand(
            _ => OpenHeightImage(),
            _ => GetViewerWorkspaceCandidate(HeightImageViewerContentId) is not null);
        clearMainViewerPinCommand = new RelayCommand(
            _ => viewerWorkspace.ClearMainContent(),
            _ => viewerWorkspace.IsMainContentPinned);
        clearAuxiliaryViewerPinCommand = new RelayCommand(
            _ => viewerWorkspace.ClearAuxiliaryContent(),
            _ => viewerWorkspace.IsAuxiliaryContentPinned);
        toggleViewerCameraLinkCommand = new RelayCommand(
            _ => ToggleViewerCameraLink(),
            _ => viewerWorkspace.IsCameraLinked || CanLinkViewerCameras);
        focusViewerWorkspaceSlotCommand = new RelayCommand(
            parameter => FocusViewerWorkspaceSlot(parameter as string),
            parameter => parameter is string slotId
                         && (string.Equals(
                                 slotId,
                                 ViewerWorkspaceSession.MainSlotId,
                                 StringComparison.OrdinalIgnoreCase)
                             || viewerWorkspace.HasAuxiliarySlot
                             && string.Equals(
                                 slotId,
                                 ViewerWorkspaceSession.AuxiliarySlotId,
                                 StringComparison.OrdinalIgnoreCase)));
        sessionEventCoordinator = new ToolWorkbenchViewerWorkspaceEventCoordinator(
            viewerWorkspace,
            OnViewerWorkspacePropertyChanged);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<ViewerWorkspaceCandidateItem> ViewerWorkspaceCandidates
    {
        get
        {
            var candidates = new List<ViewerWorkspaceCandidateItem>();
            var source = getSource();
            if (source.IsReady && File.Exists(source.Path))
            {
                candidates.Add(new ViewerWorkspaceCandidateItem(
                    HeightImageViewerContentId,
                    localization.HeightImage,
                    ViewerWorkspaceCandidateKind.HeightImage,
                    source.Path,
                    "HeightField / native grid",
                    "Ready",
                    true));
            }

            candidates.AddRange(
                getRenderableTargets()
                    .Where(target => target.IsDisplayable)
                    .Select(target => new ViewerWorkspaceCandidateItem(
                        target.Id,
                        target.DisplayName,
                        ViewerWorkspaceCandidateKind.ThreeDArtifact,
                        target.C3DPath,
                        target.Contract,
                        target.State,
                        target.IsSource)));
            return candidates;
        }
    }

    public IReadOnlyList<ViewerWorkspaceCandidateItem> MainViewerCandidates =>
        ViewerWorkspaceCandidates
            .Where(candidate => candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact)
            .ToArray();

    public bool IsViewerCameraLinked => viewerWorkspace.IsCameraLinked;

    public bool CanLinkViewerCameras =>
        viewerWorkspace.HasAuxiliarySlot
        && GetViewerWorkspaceCandidate(viewerWorkspace.AuxiliaryContentId)?.Kind
            == ViewerWorkspaceCandidateKind.ThreeDArtifact;

    public string ViewerCameraLinkLabel =>
        viewerWorkspace.IsCameraLinked
            ? localization.ViewerCameraUnlink
            : localization.ViewerCameraLink;

    public string ViewerCameraLinkSummary =>
        viewerWorkspace.IsCameraLinked
            ? localization.ViewerCameraLinked
            : CanLinkViewerCameras
                ? localization.ViewerCameraLinkSummary
                : localization.ViewerCameraLinkUnavailable;

    public string MainViewerContentId
    {
        get => viewerWorkspace.MainContentId;
        set
        {
            var candidate = GetMainViewerCandidate(value);
            if (candidate is null)
            {
                return;
            }

            viewerWorkspace.PinMainContent(candidate.Id);
        }
    }

    public string AuxiliaryViewerContentId
    {
        get => viewerWorkspace.AuxiliaryContentId;
        set
        {
            var candidate = GetViewerWorkspaceCandidate(value);
            if (candidate is null)
            {
                return;
            }

            viewerWorkspace.PinAuxiliaryContent(candidate.Id);
            if (candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact
                && !candidate.IsSource)
            {
                workspaceSelection.SelectOutput(candidate.Id);
            }
            FocusViewerWorkspaceSlot(ViewerWorkspaceSession.AuxiliarySlotId);
        }
    }

    public string ViewerWorkspaceLayoutSummary => viewerWorkspace.Layout switch
    {
        ViewerWorkspaceLayout.SplitVertical => localization.ViewerSplitVertical,
        ViewerWorkspaceLayout.SplitHorizontal => localization.ViewerSplitHorizontal,
        ViewerWorkspaceLayout.PopOut => localization.ViewerPopOut,
        _ => localization.ViewerSingle
    };
    public bool IsSingleViewerLayout => viewerWorkspace.Layout == ViewerWorkspaceLayout.Single;
    public bool IsSplitVerticalViewerLayout => viewerWorkspace.Layout == ViewerWorkspaceLayout.SplitVertical;
    public bool IsSplitHorizontalViewerLayout => viewerWorkspace.Layout == ViewerWorkspaceLayout.SplitHorizontal;
    public bool IsPopOutViewerLayout => viewerWorkspace.Layout == ViewerWorkspaceLayout.PopOut;

    public string AuxiliaryViewerSummary =>
        GetViewerWorkspaceCandidate(viewerWorkspace.AuxiliaryContentId) is { } candidate
            ? $"{candidate.DisplayName} | {candidate.Contract}"
            : viewerWorkspace.IsAuxiliaryContentPinned
                ? $"{localization.ViewerPinnedUnavailable} | {viewerWorkspace.AuxiliaryContentId}"
            : localization.ViewerAuxiliaryNoOutput;

    public string MainViewerSummary =>
        GetMainViewerCandidate(viewerWorkspace.MainContentId) is { } candidate
            ? $"{candidate.DisplayName} | {candidate.Contract}"
            : viewerWorkspace.IsMainContentPinned
                ? $"{localization.ViewerPinnedUnavailable} | {viewerWorkspace.MainContentId}"
                : localization.ViewerMainNoOutput;

    public ICommand SetSingleViewerLayoutCommand => setSingleViewerLayoutCommand;
    public ICommand SplitViewerVerticallyCommand => splitViewerVerticallyCommand;
    public ICommand SplitViewerHorizontallyCommand => splitViewerHorizontallyCommand;
    public ICommand PopOutViewerCommand => popOutViewerCommand;
    public ICommand FocusViewerWorkspaceSlotCommand => focusViewerWorkspaceSlotCommand;
    public ICommand OpenHeightImageCommand => openHeightImageCommand;
    public ICommand ClearMainViewerPinCommand => clearMainViewerPinCommand;
    public ICommand ClearAuxiliaryViewerPinCommand => clearAuxiliaryViewerPinCommand;
    public ICommand ToggleViewerCameraLinkCommand => toggleViewerCameraLinkCommand;

    public void Dispose() => sessionEventCoordinator.Dispose();

    public ViewerWorkspaceCandidateItem? GetViewerWorkspaceCandidate(string? contentId) =>
        ViewerWorkspaceCandidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, contentId, StringComparison.OrdinalIgnoreCase));

    public ViewerWorkspaceCandidateItem? GetMainViewerCandidate(string? contentId) =>
        MainViewerCandidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, contentId, StringComparison.OrdinalIgnoreCase));

    private void SetViewerWorkspaceLayout(ViewerWorkspaceLayout layout)
    {
        var available = ViewerWorkspaceCandidates;
        var preferred = GetPreferredViewerWorkspaceContentId(
            available,
            preferHeightImage: viewerWorkspace.Layout == ViewerWorkspaceLayout.Single);
        if (!viewerWorkspace.TrySetLayout(
                layout,
                available.Select(candidate => candidate.Id),
                preferred))
        {
            return;
        }

        workspaceSelection.FocusViewerSlot(viewerWorkspace.FocusedSlotId);

        RaiseViewerWorkspaceCanExecuteChanged();
    }

    private void FocusViewerWorkspaceSlot(string? slotId)
    {
        viewerWorkspace.FocusSlot(slotId);
        workspaceSelection.FocusViewerSlot(viewerWorkspace.FocusedSlotId);
        focusViewerWorkspaceSlotCommand.RaiseCanExecuteChanged();
    }

    public void SynchronizeViewerWorkspaceFocus(string? slotId)
    {
        viewerWorkspace.FocusSlot(slotId);
        focusViewerWorkspaceSlotCommand.RaiseCanExecuteChanged();
    }

    private bool CanOpenAuxiliaryViewer() =>
        ViewerWorkspaceCandidates.Count > 0;

    private void OpenHeightImage()
    {
        if (!viewerWorkspace.TryOpenAuxiliaryContent(
                HeightImageViewerContentId,
                ViewerWorkspaceCandidates.Select(candidate => candidate.Id)))
        {
            return;
        }

        workspaceSelection.FocusViewerSlot(viewerWorkspace.FocusedSlotId);
        RaiseViewerWorkspaceCanExecuteChanged();
    }

    private void ToggleViewerCameraLink()
    {
        if (viewerWorkspace.IsCameraLinked)
        {
            viewerWorkspace.SetCameraLinked(false);
        }
        else if (CanLinkViewerCameras)
        {
            viewerWorkspace.SetCameraLinked(true);
        }
    }

    private string? GetPreferredViewerWorkspaceContentId(
        IReadOnlyList<ViewerWorkspaceCandidateItem> available,
        bool preferHeightImage)
    {
        var compareSlots = getCompareSlots();
        var preferredCompareId = new[]
        {
            compareSlots.B,
            compareSlots.A,
            compareSlots.C
        }.FirstOrDefault(id => available.Any(candidate =>
            candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact
            && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase)));
        return preferHeightImage
               && available.Any(candidate => candidate.Kind == ViewerWorkspaceCandidateKind.HeightImage)
            ? HeightImageViewerContentId
            : preferredCompareId ?? available.FirstOrDefault()?.Id;
    }

    public void ReconcileViewerWorkspaceContents(bool preferHeightImage = false)
    {
        var available = ViewerWorkspaceCandidates;
        var preferred = GetPreferredViewerWorkspaceContentId(available, preferHeightImage);
        var mainCandidates = available
            .Where(candidate => candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact)
            .ToArray();
        viewerWorkspace.ReconcileMainContent(
            mainCandidates.Select(candidate => candidate.Id),
            GetPreferredMainViewerContentId(mainCandidates));

        if (preferHeightImage && !string.IsNullOrWhiteSpace(preferred))
        {
            viewerWorkspace.PinAuxiliaryContent(preferred);
        }
        else
        {
            viewerWorkspace.ReconcileContents(
                available.Select(candidate => candidate.Id),
                preferred);
        }
        OnPropertyChanged(nameof(ViewerWorkspaceCandidates));
        OnPropertyChanged(nameof(MainViewerCandidates));
        OnPropertyChanged(nameof(MainViewerSummary));
        OnPropertyChanged(nameof(AuxiliaryViewerSummary));
        RaiseViewerWorkspaceCanExecuteChanged();
    }

    private void OnViewerWorkspacePropertyChanged(PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(ViewerWorkspaceLayoutSummary));
        OnPropertyChanged(nameof(IsSingleViewerLayout));
        OnPropertyChanged(nameof(IsSplitVerticalViewerLayout));
        OnPropertyChanged(nameof(IsSplitHorizontalViewerLayout));
        OnPropertyChanged(nameof(IsPopOutViewerLayout));
        if (args.PropertyName is nameof(ViewerWorkspaceSession.MainContentId)
            or nameof(ViewerWorkspaceSession.IsMainContentPinned)
            or nameof(ViewerWorkspaceSession.IsMainContentExplicitlyCleared)
            or nameof(ViewerWorkspaceSession.AuxiliaryContentId)
            or nameof(ViewerWorkspaceSession.IsAuxiliaryContentPinned)
            or nameof(ViewerWorkspaceSession.IsAuxiliaryContentExplicitlyCleared)
            or nameof(ViewerWorkspaceSession.IsCameraLinked))
        {
            OnPropertyChanged(nameof(MainViewerContentId));
            OnPropertyChanged(nameof(MainViewerSummary));
            OnPropertyChanged(nameof(AuxiliaryViewerContentId));
            OnPropertyChanged(nameof(AuxiliaryViewerSummary));
            OnPropertyChanged(nameof(IsViewerCameraLinked));
            OnPropertyChanged(nameof(CanLinkViewerCameras));
            OnPropertyChanged(nameof(ViewerCameraLinkLabel));
            OnPropertyChanged(nameof(ViewerCameraLinkSummary));
            clearMainViewerPinCommand.RaiseCanExecuteChanged();
            clearAuxiliaryViewerPinCommand.RaiseCanExecuteChanged();
            toggleViewerCameraLinkCommand.RaiseCanExecuteChanged();
        }
    }

    public void RefreshLocalization(PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName)
            || args.PropertyName is nameof(ThreeDLocalization.ViewerSingle)
            or nameof(ThreeDLocalization.ViewerSplitVertical)
            or nameof(ThreeDLocalization.ViewerSplitHorizontal)
            or nameof(ThreeDLocalization.ViewerPopOut)
            or nameof(ThreeDLocalization.ViewerAuxiliaryNoOutput)
            or nameof(ThreeDLocalization.ViewerMainNoOutput)
            or nameof(ThreeDLocalization.ViewerPinnedUnavailable)
            or nameof(ThreeDLocalization.HeightImage)
            or nameof(ThreeDLocalization.ViewerCameraLink)
            or nameof(ThreeDLocalization.ViewerCameraUnlink)
            or nameof(ThreeDLocalization.ViewerCameraLinked)
            or nameof(ThreeDLocalization.ViewerCameraLinkSummary)
            or nameof(ThreeDLocalization.ViewerCameraLinkUnavailable))
        {
            OnPropertyChanged(nameof(ViewerWorkspaceLayoutSummary));
            OnPropertyChanged(nameof(MainViewerSummary));
            OnPropertyChanged(nameof(AuxiliaryViewerSummary));
            OnPropertyChanged(nameof(MainViewerCandidates));
            OnPropertyChanged(nameof(ViewerWorkspaceCandidates));
            OnPropertyChanged(nameof(ViewerCameraLinkLabel));
            OnPropertyChanged(nameof(ViewerCameraLinkSummary));
        }
    }

    private void RaiseViewerWorkspaceCanExecuteChanged()
    {
        splitViewerVerticallyCommand.RaiseCanExecuteChanged();
        splitViewerHorizontallyCommand.RaiseCanExecuteChanged();
        popOutViewerCommand.RaiseCanExecuteChanged();
        openHeightImageCommand.RaiseCanExecuteChanged();
        focusViewerWorkspaceSlotCommand.RaiseCanExecuteChanged();
        clearMainViewerPinCommand.RaiseCanExecuteChanged();
        clearAuxiliaryViewerPinCommand.RaiseCanExecuteChanged();
        toggleViewerCameraLinkCommand.RaiseCanExecuteChanged();
    }

    private static string? GetPreferredMainViewerContentId(
        IReadOnlyList<ViewerWorkspaceCandidateItem> available)
    {
        return available.FirstOrDefault(candidate => candidate.IsSource)?.Id
            ?? available.FirstOrDefault()?.Id;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
