using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

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
    private readonly Func<(bool IsActive, string? Path)> getInspectionInput;
    private readonly Func<string?, string?> getInputBackgroundId;
    private readonly Func<IReadOnlyList<ToolWorkbenchRenderableC3DTarget>> getRenderableTargets;
    private readonly Func<(string A, string B, string C)> getCompareSlots;
    private readonly Func<string?, C3DSignedHeightDeltaArtifact?> getSignedDeltaOutput;
    private readonly Func<C3DSignedHeightDeltaArtifact, bool> showSignedDeltaOutput;
    private readonly Action clearSignedDeltaOutput;
    private readonly ToolWorkbenchViewerWorkspaceEventCoordinator sessionEventCoordinator;
    private readonly RelayCommand setSingleViewerLayoutCommand;
    private readonly RelayCommand splitViewerVerticallyCommand;
    private readonly RelayCommand splitViewerHorizontallyCommand;
    private readonly RelayCommand popOutViewerCommand;
    private readonly RelayCommand setFourViewerLayoutCommand;
    private readonly Action? prepareHeightImage;
    private readonly HeightImageViewerViewModel? ownedInputHeightImage;
    private readonly RelayCommand focusViewerWorkspaceSlotCommand;
    private readonly RelayCommand openHeightImageCommand;
    private readonly RelayCommand showHeightImageOnlyCommand;
    private readonly RelayCommand clearMainViewerPinCommand;
    private readonly RelayCommand clearAuxiliaryViewerPinCommand;
    private readonly RelayCommand toggleViewerCameraLinkCommand;
    private readonly RelayCommand setInspectionDataViewCommand;
    private readonly RelayCommand showOutputCompareViewerCommand;
    private readonly RelayCommand endOutputCompareViewerCommand;
    private readonly RelayCommand setViewerRoiScopeCommand;
    private readonly RelayCommand toggleViewerHelpCommand;
    private readonly RelayCommand toggleViewerPaneMaximizedCommand;
    private bool hasExplicitMainViewerChoice;
    private string maximizedViewerSlotId = string.Empty;
    private string viewerRoiScope = "all";
    private bool isViewerHelpOpen;
    private ViewerInspectionDataView inspectionDataView = ViewerInspectionDataView.Input;
    private bool isOutputCompareViewerActive;
    private ViewerWorkspaceLayout? layoutBeforeOutputCompare;

    public ViewerWorkspaceViewModel(
        ViewerWorkspaceSession viewerWorkspace,
        InspectionWorkspaceSelectionSession workspaceSelection,
        ThreeDLocalization localization,
        Func<(bool IsReady, string Path)> getSource,
        Func<IReadOnlyList<ToolWorkbenchRenderableC3DTarget>> getRenderableTargets,
        Func<(string A, string B, string C)> getCompareSlots,
        Func<string?, C3DSignedHeightDeltaArtifact?> getSignedDeltaOutput,
        Func<C3DSignedHeightDeltaArtifact, bool> showSignedDeltaOutput,
        Action clearSignedDeltaOutput,
        Func<(bool IsActive, string? Path)>? getInspectionInput = null,
        Func<string?, string?>? getInputBackgroundId = null,
        Action? prepareHeightImage = null,
        HeightImageViewerViewModel? inputHeightImage = null,
        SharedHeightCursorSession? sharedHeightCursor = null,
        Func<HeightImageViewerViewModel, ViewerWorkspaceCandidateItem, Task>? loadPaneHeightImage = null)
    {
        this.viewerWorkspace = viewerWorkspace ?? throw new ArgumentNullException(nameof(viewerWorkspace));
        this.workspaceSelection = workspaceSelection ?? throw new ArgumentNullException(nameof(workspaceSelection));
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        this.getSource = getSource ?? throw new ArgumentNullException(nameof(getSource));
        this.getInspectionInput = getInspectionInput ?? (() => (false, null));
        this.getInputBackgroundId = getInputBackgroundId ?? (_ => null);
        this.prepareHeightImage = prepareHeightImage;
        var cursor = sharedHeightCursor ?? new SharedHeightCursorSession();
        ownedInputHeightImage = inputHeightImage is null ? new HeightImageViewerViewModel(localization, cursor) : null;
        var heightImage = inputHeightImage ?? ownedInputHeightImage!;
        bool IsInput(ViewerWorkspaceCandidateItem candidate) => candidate.IsSource
            || string.Equals(candidate.SourcePath, this.getInspectionInput().Path, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.SourcePath, this.getSource().Path, StringComparison.OrdinalIgnoreCase);
        var loadImage = loadPaneHeightImage ?? ((HeightImageViewerViewModel image, ViewerWorkspaceCandidateItem candidate) => Task.CompletedTask);
        MainPane = new ViewerPaneViewModel(localization, cursor, heightImage, ViewerPaneKind.ThreeD, IsInput, loadImage);
        AuxiliaryPane = new ViewerPaneViewModel(localization, cursor, heightImage, ViewerPaneKind.ThreeD, IsInput, loadImage);
        SectionPane = new ViewerPaneViewModel(localization, cursor, heightImage, ViewerPaneKind.Section, IsInput, loadImage);
        HeightPane = new ViewerPaneViewModel(localization, cursor, heightImage, ViewerPaneKind.Height, IsInput, loadImage);
        MainPane.PropertyChanged += OnPaneKindChanged;
        AuxiliaryPane.PropertyChanged += OnPaneKindChanged;
        this.getRenderableTargets = getRenderableTargets ?? throw new ArgumentNullException(nameof(getRenderableTargets));
        this.getCompareSlots = getCompareSlots ?? throw new ArgumentNullException(nameof(getCompareSlots));
        this.getSignedDeltaOutput = getSignedDeltaOutput ?? throw new ArgumentNullException(nameof(getSignedDeltaOutput));
        this.showSignedDeltaOutput = showSignedDeltaOutput ?? throw new ArgumentNullException(nameof(showSignedDeltaOutput));
        this.clearSignedDeltaOutput = clearSignedDeltaOutput ?? throw new ArgumentNullException(nameof(clearSignedDeltaOutput));

        setSingleViewerLayoutCommand = new RelayCommand(
            _ => SetViewerWorkspaceLayout(ViewerWorkspaceLayout.Single));
        splitViewerVerticallyCommand = new RelayCommand(
            _ => SetViewerWorkspaceLayout(ViewerWorkspaceLayout.SplitVertical),
            _ => CanOpenAuxiliaryViewer());
        splitViewerHorizontallyCommand = new RelayCommand(
            _ => SetViewerWorkspaceLayout(ViewerWorkspaceLayout.SplitHorizontal),
            _ => CanOpenAuxiliaryViewer());
        setFourViewerLayoutCommand = new RelayCommand(_ =>
        {
            prepareHeightImage?.Invoke();
            SetViewerWorkspaceLayout(ViewerWorkspaceLayout.Four);
        }, _ => CanOpenAuxiliaryViewer());
        popOutViewerCommand = new RelayCommand(
            _ => SetViewerWorkspaceLayout(ViewerWorkspaceLayout.PopOut),
            _ => CanOpenAuxiliaryViewer());
        openHeightImageCommand = new RelayCommand(
            _ => OpenHeightImage(),
            _ => GetViewerWorkspaceCandidate(HeightImageViewerContentId) is not null);
        showHeightImageOnlyCommand = new RelayCommand(
            _ => ShowHeightImageOnly(),
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
                             && EffectiveViewerLayout is (ViewerWorkspaceLayout.SplitVertical or ViewerWorkspaceLayout.SplitHorizontal
                                 or ViewerWorkspaceLayout.PopOut or ViewerWorkspaceLayout.Four)
                             && string.Equals(
                                 slotId,
                                 ViewerWorkspaceSession.AuxiliarySlotId,
                                 StringComparison.OrdinalIgnoreCase)));
        setInspectionDataViewCommand = new RelayCommand(
            parameter => SetInspectionDataView((ViewerInspectionDataView)parameter!),
            parameter => parameter is ViewerInspectionDataView
                && workspaceSelection.SelectedStepId is not null);
        endOutputCompareViewerCommand = new RelayCommand(_ =>
        {
            LeaveOutputCompareViewer();
            NotifyInspectionDataViewChanged();
            RaiseViewerWorkspaceCanExecuteChanged();
        }, _ => IsOutputCompareViewerActive);
        showOutputCompareViewerCommand = new RelayCommand(
            _ => ShowOutputCompareViewer(),
            _ => CanShowOutputCompareViewer);
        setViewerRoiScopeCommand = new RelayCommand(value =>
        {
            if (value is not string scope || scope == viewerRoiScope) return;
            viewerRoiScope = scope;
            OnPropertyChanged(nameof(ViewerRoiScope));
            OnPropertyChanged(nameof(IsViewerRoiAll));
            OnPropertyChanged(nameof(IsViewerRoiSelected));
            OnPropertyChanged(nameof(IsViewerRoiHidden));
        }, value => value is "all" or "selected" or "none");
        toggleViewerHelpCommand = new RelayCommand(_ => IsViewerHelpOpen = !IsViewerHelpOpen);
        toggleViewerPaneMaximizedCommand = new RelayCommand(parameter =>
        {
            var slot = (string)parameter!;
            maximizedViewerSlotId = maximizedViewerSlotId == slot ? string.Empty : slot;
            OnPropertyChanged(nameof(MaximizedViewerSlotId));
        }, parameter => parameter is string slot && (slot == ViewerWorkspaceSession.MainSlotId
            || slot == ViewerWorkspaceSession.AuxiliarySlotId && EffectiveViewerLayout is (ViewerWorkspaceLayout.SplitVertical or ViewerWorkspaceLayout.SplitHorizontal or ViewerWorkspaceLayout.Four or ViewerWorkspaceLayout.HeightImageOnly)
            || slot is ("viewer.section" or "viewer.height") && IsFourViewerLayout));
        sessionEventCoordinator = new ToolWorkbenchViewerWorkspaceEventCoordinator(
            viewerWorkspace,
            OnViewerWorkspacePropertyChanged);
        workspaceSelection.PropertyChanged += OnWorkspaceSelectionPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? OutputCompareViewerRequested;

    public IReadOnlyList<ViewerWorkspaceCandidateItem> ViewerWorkspaceCandidates
    {
        get
        {
            var candidates = new List<ViewerWorkspaceCandidateItem>();
            if (getInspectionInput() is { IsActive: true } inspection)
            {
                if (!string.IsNullOrWhiteSpace(inspection.Path) && File.Exists(inspection.Path))
                {
                    candidates.Add(new("inspection.input", Path.GetFileName(inspection.Path), ViewerWorkspaceCandidateKind.ThreeDArtifact, inspection.Path, "C3D", localization.Resolve("ThreeD.Inspection.Input", "검사 입력", "Inspection input"), false));
                    candidates.Add(new(HeightImageViewerContentId, localization.HeightImage, ViewerWorkspaceCandidateKind.HeightImage, inspection.Path, "C3D", localization.Resolve("ThreeD.Inspection.Input", "검사 입력", "Inspection input"), false));
                }
                return candidates;
            }
            var source = getSource();
            if (source.IsReady && File.Exists(source.Path) || HasEffectiveSignedDeltaOutput)
            {
                var signedDeltaOutput = inspectionDataView != ViewerInspectionDataView.Input
                    && HasEffectiveSignedDeltaOutput;
                candidates.Add(new ViewerWorkspaceCandidateItem(
                    HeightImageViewerContentId,
                    signedDeltaOutput ? localization.Resolve("ThreeD.Viewer.SignedDeltaMap", "부호 있는 높이 차이", "Signed height delta") : localization.HeightImage,
                    ViewerWorkspaceCandidateKind.HeightImage,
                    source.Path,
                    signedDeltaOutput ? "SignedDeltaHeightField" : "HeightField / native grid",
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

    public bool IsViewerCameraLinked => !isOutputCompareViewerActive
        && inspectionDataView == ViewerInspectionDataView.Input
        && viewerWorkspace.IsCameraLinked;

    public bool CanLinkViewerCameras => !isOutputCompareViewerActive
        && inspectionDataView == ViewerInspectionDataView.Input
        && viewerWorkspace.HasAuxiliarySlot
        && MainPane.IsThreeD && AuxiliaryPane.IsThreeD
        && GetViewerWorkspaceCandidate(EffectiveAuxiliaryViewerContentId)?.Kind
            == ViewerWorkspaceCandidateKind.ThreeDArtifact;

    public string ViewerCameraLinkLabel =>
        viewerWorkspace.IsCameraLinked
            ? localization.ViewerCameraUnlink
            : localization.ViewerCameraLink;

    public bool CanShowOutputCompareViewer
    {
        get
        {
            var (slotAId, slotBId, _) = getCompareSlots();
            if (!string.IsNullOrWhiteSpace(slotAId)
                && string.Equals(slotAId, slotBId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var slotA = GetMainViewerCandidate(slotAId);
            var slotB = GetMainViewerCandidate(slotBId);
            if (slotA is not null && slotB is not null
                && string.Equals(slotA.Id, slotB.Id, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return isOutputCompareViewerActive
                || HasInspectionStep
                || slotA is not null && slotB is not null;
        }
    }

    public bool IsOutputCompareViewerActive => !getInspectionInput().IsActive && isOutputCompareViewerActive;
    public string MainViewerSlotLabel => IsOutputCompareViewerActive ? "A" : localization.ViewerMainSlot;
    public string AuxiliaryViewerSlotLabel => IsOutputCompareViewerActive ? "B" : localization.ViewerAuxiliarySlot;

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
            if (candidate is null || !CanEditMainViewerPin)
            {
                return;
            }

            hasExplicitMainViewerChoice = true;
            viewerWorkspace.PinMainContent(candidate.Id);
            NotifyInspectionDataViewChanged();
        }
    }

    public string MainViewerDisplayedContentId { get => EffectiveMainViewerContentId; set => MainViewerContentId = value; }
    public string MaximizedViewerSlotId => maximizedViewerSlotId;
    public ICommand ToggleViewerPaneMaximizedCommand => toggleViewerPaneMaximizedCommand;

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

    public string ViewerWorkspaceLayoutSummary => EffectiveViewerLayout switch
    {
        ViewerWorkspaceLayout.HeightImageOnly => localization.HeightImage,
        ViewerWorkspaceLayout.SplitVertical => localization.ViewerSplitVertical,
        ViewerWorkspaceLayout.SplitHorizontal => localization.ViewerSplitHorizontal,
        ViewerWorkspaceLayout.PopOut => localization.ViewerPopOut,
        ViewerWorkspaceLayout.Four => localization.Resolve("ThreeD.Viewer.Four", "4개", "Four panes"),
        _ => localization.ViewerSingle
    };
    public bool IsSingleViewerLayout => EffectiveViewerLayout == ViewerWorkspaceLayout.Single;
    public bool IsHeightImageOnlyViewerLayout => EffectiveViewerLayout == ViewerWorkspaceLayout.HeightImageOnly;
    public bool IsSplitVerticalViewerLayout => EffectiveViewerLayout == ViewerWorkspaceLayout.SplitVertical
        && !IsOutputCompareViewerActive
        && inspectionDataView == ViewerInspectionDataView.Input;
    public bool IsSplitHorizontalViewerLayout => EffectiveViewerLayout == ViewerWorkspaceLayout.SplitHorizontal;
    public bool IsFourViewerLayout => EffectiveViewerLayout == ViewerWorkspaceLayout.Four;
    public bool IsPopOutViewerLayout => EffectiveViewerLayout == ViewerWorkspaceLayout.PopOut;

    public string AuxiliaryViewerSummary =>
        GetViewerWorkspaceCandidate(viewerWorkspace.AuxiliaryContentId) is { } candidate
            ? $"{candidate.DisplayName} | {localization.ContractLabel(candidate.Contract)}"
            : viewerWorkspace.IsAuxiliaryContentPinned
                ? $"{localization.ViewerPinnedUnavailable} | {viewerWorkspace.AuxiliaryContentId}"
            : localization.ViewerAuxiliaryNoOutput;

    public string MainViewerSummary =>
        GetMainViewerCandidate(viewerWorkspace.MainContentId) is { } candidate
            ? $"{candidate.DisplayName} | {localization.ContractLabel(candidate.Contract)}"
            : viewerWorkspace.IsMainContentPinned
                ? $"{localization.ViewerPinnedUnavailable} | {viewerWorkspace.MainContentId}"
                : localization.ViewerMainNoOutput;

    public ViewerInspectionDataView InspectionDataView => inspectionDataView;
    public bool IsInspectionInputView => getInspectionInput().IsActive || !isOutputCompareViewerActive && inspectionDataView == ViewerInspectionDataView.Input;
    public bool IsInspectionOutputView => !getInspectionInput().IsActive && !isOutputCompareViewerActive && inspectionDataView == ViewerInspectionDataView.Output;
    public bool IsInspectionInputOutputView => !getInspectionInput().IsActive && !isOutputCompareViewerActive && inspectionDataView == ViewerInspectionDataView.InputOutput;
    public bool HasInspectionStep => !getInspectionInput().IsActive && workspaceSelection.SelectedStepId is not null;
    public bool HasEffectiveSignedDeltaOutput => !getInspectionInput().IsActive && !isOutputCompareViewerActive
        && getSignedDeltaOutput(workspaceSelection.SelectedOutputEntityId) is not null;
    public bool CanEditMainViewerPin => !getInspectionInput().IsActive && !isOutputCompareViewerActive;
    public bool CanEditAuxiliaryViewerPin => !getInspectionInput().IsActive && !isOutputCompareViewerActive
        && inspectionDataView != ViewerInspectionDataView.InputOutput;
    public ViewerWorkspaceLayout EffectiveViewerLayout => getInspectionInput().IsActive ? layoutBeforeOutputCompare ?? viewerWorkspace.Layout : isOutputCompareViewerActive
        ? ViewerWorkspaceLayout.SplitVertical
        : inspectionDataView switch
    {
        ViewerInspectionDataView.Output => HasEffectiveSignedDeltaOutput
            ? ViewerWorkspaceLayout.HeightImageOnly
            : ViewerWorkspaceLayout.Single,
        ViewerInspectionDataView.InputOutput => ViewerWorkspaceLayout.SplitVertical,
        _ => viewerWorkspace.Layout
    };
    public string EffectiveMainViewerContentId => getInspectionInput().IsActive
        ? GetMainViewerCandidate("inspection.input")?.Id ?? string.Empty
        : isOutputCompareViewerActive
        ? GetMainViewerCandidate(getCompareSlots().A)?.Id ?? string.Empty
        : hasExplicitMainViewerChoice && viewerWorkspace.IsMainContentPinned
        ? viewerWorkspace.MainContentId
        : HasInspectionStep
        ? inspectionDataView == ViewerInspectionDataView.Output
            ? GetMainViewerCandidate(workspaceSelection.SelectedOutputEntityId)?.Id ?? string.Empty
            : GetMainViewerCandidate(workspaceSelection.SelectedInputEntityId)?.Id
                ?? GetMainViewerCandidate(getInputBackgroundId(workspaceSelection.SelectedInputEntityId))?.Id ?? string.Empty
        : viewerWorkspace.MainContentId;
    public string EffectiveAuxiliaryViewerContentId => getInspectionInput().IsActive
        ? GetViewerWorkspaceCandidate(HeightImageViewerContentId)?.Id ?? string.Empty
        : isOutputCompareViewerActive
        ? GetMainViewerCandidate(getCompareSlots().B)?.Id ?? string.Empty
        : inspectionDataView == ViewerInspectionDataView.InputOutput
        ? HasEffectiveSignedDeltaOutput
            ? HeightImageViewerContentId
            : GetMainViewerCandidate(workspaceSelection.SelectedOutputEntityId)?.Id ?? string.Empty
        : inspectionDataView == ViewerInspectionDataView.Output
            ? HasEffectiveSignedDeltaOutput ? HeightImageViewerContentId : string.Empty
            : viewerWorkspace.Layout == ViewerWorkspaceLayout.Four
                && viewerWorkspace.AuxiliaryContentId == HeightImageViewerContentId
                    ? EffectiveMainViewerContentId : viewerWorkspace.AuxiliaryContentId;
    public bool HasEffectiveMainGeometry => EffectiveMainViewerContentId.Length > 0;
    public bool HasEffectiveOutputGeometry =>
        HasEffectiveSignedDeltaOutput
        || GetMainViewerCandidate(workspaceSelection.SelectedOutputEntityId) is not null;
    public string EffectiveMainViewerSummary =>
        GetMainViewerCandidate(EffectiveMainViewerContentId) is { } candidate
            ? isOutputCompareViewerActive
                ? $"{candidate.DisplayName} | {localization.ContractLabel(candidate.Contract)} | {localization.SelectedToolOutputStateLabel(candidate.State)}"
                : $"{candidate.DisplayName} | {localization.ContractLabel(candidate.Contract)}"
            : inspectionDataView == ViewerInspectionDataView.Output
                ? localization.NoCurrentDisplayableOutput
                : localization.ViewerMainNoOutput;
    public string EffectiveAuxiliaryViewerSummary =>
        GetViewerWorkspaceCandidate(EffectiveAuxiliaryViewerContentId) is { } candidate
            ? isOutputCompareViewerActive
                ? $"{candidate.DisplayName} | {localization.ContractLabel(candidate.Contract)} | {localization.SelectedToolOutputStateLabel(candidate.State)}"
                : $"{candidate.DisplayName} | {localization.ContractLabel(candidate.Contract)}"
            : localization.NoCurrentDisplayableOutput;

    public ViewerPaneViewModel MainPane { get; }
    public ViewerPaneViewModel AuxiliaryPane { get; }
    public ViewerPaneViewModel SectionPane { get; }
    public ViewerPaneViewModel HeightPane { get; }
    public string ViewerRoiScope => viewerRoiScope;
    public bool IsViewerRoiAll => viewerRoiScope == "all";
    public bool IsViewerRoiSelected => viewerRoiScope == "selected";
    public bool IsViewerRoiHidden => viewerRoiScope == "none";
    public bool IsViewerHelpOpen
    {
        get => isViewerHelpOpen;
        set
        {
            if (isViewerHelpOpen == value) return;
            isViewerHelpOpen = value;
            OnPropertyChanged(nameof(IsViewerHelpOpen));
        }
    }

    public ICommand SetSingleViewerLayoutCommand => setSingleViewerLayoutCommand;
    public ICommand SplitViewerVerticallyCommand => splitViewerVerticallyCommand;
    public ICommand SplitViewerHorizontallyCommand => splitViewerHorizontallyCommand;
    public ICommand SetFourViewerLayoutCommand => setFourViewerLayoutCommand;
    public ICommand PopOutViewerCommand => popOutViewerCommand;
    public ICommand FocusViewerWorkspaceSlotCommand => focusViewerWorkspaceSlotCommand;
    public ICommand OpenHeightImageCommand => openHeightImageCommand;
    public ICommand ShowHeightImageOnlyCommand => showHeightImageOnlyCommand;
    public ICommand ClearMainViewerPinCommand => clearMainViewerPinCommand;
    public ICommand ClearAuxiliaryViewerPinCommand => clearAuxiliaryViewerPinCommand;
    public ICommand ToggleViewerCameraLinkCommand => toggleViewerCameraLinkCommand;
    public ICommand SetInspectionDataViewCommand => setInspectionDataViewCommand;
    public ICommand EndOutputCompareViewerCommand => endOutputCompareViewerCommand;
    public ICommand ShowOutputCompareViewerCommand => showOutputCompareViewerCommand;
    public ICommand SetViewerRoiScopeCommand => setViewerRoiScopeCommand;
    public ICommand ToggleViewerHelpCommand => toggleViewerHelpCommand;

    public void Dispose()
    {
        workspaceSelection.PropertyChanged -= OnWorkspaceSelectionPropertyChanged;
        sessionEventCoordinator.Dispose();
        MainPane.PropertyChanged -= OnPaneKindChanged;
        AuxiliaryPane.PropertyChanged -= OnPaneKindChanged;
        MainPane.Dispose();
        AuxiliaryPane.Dispose();
        SectionPane.Dispose();
        HeightPane.Dispose();
        ownedInputHeightImage?.Dispose();
    }

    private void SetInspectionDataView(ViewerInspectionDataView next)
    {
        var wasCompareActive = isOutputCompareViewerActive;
        var hadExplicitMainChoice = hasExplicitMainViewerChoice;
        hasExplicitMainViewerChoice = false;
        if (inspectionDataView == next && !wasCompareActive && !hadExplicitMainChoice)
        {
            return;
        }

        LeaveOutputCompareViewer();
        inspectionDataView = next;
        SynchronizeSignedDeltaOutput();
        if (next != ViewerInspectionDataView.Input)
        {
            viewerWorkspace.SetCameraLinked(false);
        }
        NotifyInspectionDataViewChanged();
    }

    public void RefreshOutputCompareSlots()
    {
        OnPropertyChanged(nameof(CanShowOutputCompareViewer));
        if (isOutputCompareViewerActive && !CanShowOutputCompareViewer)
        {
            LeaveOutputCompareViewer();
            NotifyInspectionDataViewChanged();
            return;
        }

        showOutputCompareViewerCommand.RaiseCanExecuteChanged();
        if (isOutputCompareViewerActive)
        {
            NotifyInspectionDataViewChanged();
        }
    }

    private void ShowOutputCompareViewer()
    {
        if (!CanShowOutputCompareViewer)
        {
            return;
        }

        if (isOutputCompareViewerActive)
        {
            LeaveOutputCompareViewer();
            NotifyInspectionDataViewChanged();
            RaiseViewerWorkspaceCanExecuteChanged();
            return;
        }

        SetInspectionDataView(ViewerInspectionDataView.Input);
        layoutBeforeOutputCompare = viewerWorkspace.Layout;
        isOutputCompareViewerActive = true;
        viewerWorkspace.SetLayout(ViewerWorkspaceLayout.SplitVertical);
        NotifyInspectionDataViewChanged();
        RaiseViewerWorkspaceCanExecuteChanged();

        OutputCompareViewerRequested?.Invoke(this, EventArgs.Empty);
    }

    private void LeaveOutputCompareViewer()
    {
        if (!isOutputCompareViewerActive)
        {
            return;
        }

        isOutputCompareViewerActive = false;
        if (layoutBeforeOutputCompare is { } layout)
        {
            viewerWorkspace.SetLayout(layout);
        }
        layoutBeforeOutputCompare = null;
    }

    private void OnWorkspaceSelectionPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(InspectionWorkspaceSelectionSession.SelectedStepId))
        {
            hasExplicitMainViewerChoice = false;
            LeaveOutputCompareViewer();
            inspectionDataView = ViewerInspectionDataView.Input;
            clearSignedDeltaOutput();
            setInspectionDataViewCommand.RaiseCanExecuteChanged();
        }
        else if (args.PropertyName == nameof(InspectionWorkspaceSelectionSession.SelectedInputEntityId)
                 && isOutputCompareViewerActive)
        {
            LeaveOutputCompareViewer();
            inspectionDataView = ViewerInspectionDataView.Input;
            clearSignedDeltaOutput();
        }

        if (args.PropertyName == nameof(InspectionWorkspaceSelectionSession.SelectedOutputEntityId)
            && inspectionDataView != ViewerInspectionDataView.Input)
        {
            SynchronizeSignedDeltaOutput();
        }

        if (args.PropertyName is nameof(InspectionWorkspaceSelectionSession.SelectedStepId)
            or nameof(InspectionWorkspaceSelectionSession.SelectedInputEntityId)
            or nameof(InspectionWorkspaceSelectionSession.SelectedOutputEntityId))
        {
            NotifyInspectionDataViewChanged();
        }
    }

    public void RefreshInspectionFileContext()
    {
        if (!getInspectionInput().IsActive)
        {
            ReconcileViewerWorkspaceContents();
            return;
        }
        NotifyInspectionDataViewChanged();
        OnPropertyChanged(nameof(MainViewerCandidates));
        RaiseViewerWorkspaceCanExecuteChanged();
        showOutputCompareViewerCommand.RaiseCanExecuteChanged();
    }

    public void RefreshInspectionDataView()
    {
        if (inspectionDataView != ViewerInspectionDataView.Input)
        {
            SynchronizeSignedDeltaOutput();
        }
        else if (!HasEffectiveSignedDeltaOutput)
        {
            clearSignedDeltaOutput();
        }
        NotifyInspectionDataViewChanged();
    }

    private void SynchronizeSignedDeltaOutput()
    {
        if (inspectionDataView != ViewerInspectionDataView.Input
            && getSignedDeltaOutput(workspaceSelection.SelectedOutputEntityId) is { } output
            && showSignedDeltaOutput(output))
        {
            return;
        }
        clearSignedDeltaOutput();
    }

    private void NotifyInspectionDataViewChanged()
    {
        RefreshPaneSources();
        OnPropertyChanged(nameof(ViewerWorkspaceCandidates));
        OnPropertyChanged(nameof(AuxiliaryViewerContentId));
        OnPropertyChanged(nameof(InspectionDataView));
        OnPropertyChanged(nameof(CanShowOutputCompareViewer));
        OnPropertyChanged(nameof(IsInspectionInputView));
        OnPropertyChanged(nameof(IsInspectionOutputView));
        OnPropertyChanged(nameof(IsInspectionInputOutputView));
        OnPropertyChanged(nameof(IsOutputCompareViewerActive));
        OnPropertyChanged(nameof(MainViewerSlotLabel));
        OnPropertyChanged(nameof(AuxiliaryViewerSlotLabel));
        OnPropertyChanged(nameof(HasInspectionStep));
        OnPropertyChanged(nameof(HasEffectiveSignedDeltaOutput));
        OnPropertyChanged(nameof(CanEditMainViewerPin));
        OnPropertyChanged(nameof(CanEditAuxiliaryViewerPin));
        OnPropertyChanged(nameof(EffectiveViewerLayout));
        OnPropertyChanged(nameof(EffectiveMainViewerContentId));
        OnPropertyChanged(nameof(MainViewerDisplayedContentId));
        OnPropertyChanged(nameof(EffectiveAuxiliaryViewerContentId));
        OnPropertyChanged(nameof(HasEffectiveMainGeometry));
        OnPropertyChanged(nameof(HasEffectiveOutputGeometry));
        OnPropertyChanged(nameof(EffectiveMainViewerSummary));
        OnPropertyChanged(nameof(EffectiveAuxiliaryViewerSummary));
        OnPropertyChanged(nameof(AuxiliaryViewerSummary));
        OnPropertyChanged(nameof(ViewerWorkspaceLayoutSummary));
        OnPropertyChanged(nameof(IsSingleViewerLayout));
        OnPropertyChanged(nameof(IsHeightImageOnlyViewerLayout));
        OnPropertyChanged(nameof(IsSplitVerticalViewerLayout));
        OnPropertyChanged(nameof(IsSplitHorizontalViewerLayout));
        OnPropertyChanged(nameof(IsPopOutViewerLayout));
        OnPropertyChanged(nameof(IsFourViewerLayout));
        OnPropertyChanged(nameof(IsViewerCameraLinked));
        OnPropertyChanged(nameof(CanLinkViewerCameras));
        OnPropertyChanged(nameof(ViewerCameraLinkLabel));
        OnPropertyChanged(nameof(ViewerCameraLinkSummary));
        toggleViewerCameraLinkCommand.RaiseCanExecuteChanged();
        showOutputCompareViewerCommand.RaiseCanExecuteChanged();
    }

    public ViewerWorkspaceCandidateItem? GetViewerWorkspaceCandidate(string? contentId) =>
        ViewerWorkspaceCandidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, contentId, StringComparison.OrdinalIgnoreCase));

    public ViewerWorkspaceCandidateItem? GetMainViewerCandidate(string? contentId) =>
        MainViewerCandidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, contentId, StringComparison.OrdinalIgnoreCase));

    private void OnPaneKindChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ViewerPaneViewModel.Kind)) return;
        if (!isOutputCompareViewerActive && inspectionDataView == ViewerInspectionDataView.Input && !CanLinkViewerCameras) viewerWorkspace.SetCameraLinked(false);
        OnPropertyChanged(nameof(CanLinkViewerCameras));
        toggleViewerCameraLinkCommand.RaiseCanExecuteChanged();
    }

    private void RefreshPaneSources()
    {
        var layout = EffectiveViewerLayout;
        var main = GetMainViewerCandidate(EffectiveMainViewerContentId);
        var auxiliary = GetViewerWorkspaceCandidate(EffectiveAuxiliaryViewerContentId);
        MainPane.UpdateSource(main, ViewerPaneKind.ThreeD, layout != ViewerWorkspaceLayout.HeightImageOnly);
        AuxiliaryPane.UpdateSource(auxiliary,
            auxiliary?.Kind == ViewerWorkspaceCandidateKind.HeightImage ? ViewerPaneKind.Height : ViewerPaneKind.ThreeD,
            layout != ViewerWorkspaceLayout.Single, layout != ViewerWorkspaceLayout.Four && auxiliary?.Kind == ViewerWorkspaceCandidateKind.HeightImage);
        SectionPane.UpdateSource(main, ViewerPaneKind.Section, layout == ViewerWorkspaceLayout.Four);
        HeightPane.UpdateSource(main, ViewerPaneKind.Height, layout == ViewerWorkspaceLayout.Four);
    }

    public bool IsGalleryArtifactShown(string artifactId) =>
        EffectiveViewerLayout != ViewerWorkspaceLayout.HeightImageOnly && string.Equals(EffectiveMainViewerContentId, artifactId, StringComparison.OrdinalIgnoreCase)
        || EffectiveViewerLayout != ViewerWorkspaceLayout.Single && string.Equals(EffectiveAuxiliaryViewerContentId, artifactId, StringComparison.OrdinalIgnoreCase);

    public bool TryShowGalleryArtifact(string artifactId, string? requestedSlot)
    {
        if (getInspectionInput().IsActive || GetMainViewerCandidate(artifactId) is not { } candidate) return false;
        if (requestedSlot is null && IsInspectionInputOutputView)
        {
            if (candidate.Id == EffectiveMainViewerContentId || candidate.Id == EffectiveAuxiliaryViewerContentId)
            {
                FocusViewerWorkspaceSlot(candidate.Id == EffectiveMainViewerContentId
                    ? ViewerWorkspaceSession.MainSlotId : ViewerWorkspaceSession.AuxiliarySlotId);
                return true;
            }
        }
        var slot = requestedSlot switch
        {
            "other" => viewerWorkspace.IsMainFocused ? ViewerWorkspaceSession.AuxiliarySlotId : ViewerWorkspaceSession.MainSlotId,
            ViewerWorkspaceSession.MainSlotId => ViewerWorkspaceSession.MainSlotId,
            ViewerWorkspaceSession.AuxiliarySlotId => ViewerWorkspaceSession.AuxiliarySlotId,
            _ => EffectiveViewerLayout == ViewerWorkspaceLayout.Single ? ViewerWorkspaceSession.MainSlotId : viewerWorkspace.FocusedSlotId
        };
        SetInspectionDataView(ViewerInspectionDataView.Input);
        if (requestedSlot is ViewerWorkspaceSession.MainSlotId or ViewerWorkspaceSession.AuxiliarySlotId or "other"
            && viewerWorkspace.Layout is ViewerWorkspaceLayout.Single or ViewerWorkspaceLayout.HeightImageOnly)
        {
            SetViewerWorkspaceLayout(ViewerWorkspaceLayout.SplitVertical);
        }
        maximizedViewerSlotId = string.Empty;
        OnPropertyChanged(nameof(MaximizedViewerSlotId));
        if (slot == ViewerWorkspaceSession.AuxiliarySlotId)
        {
            viewerWorkspace.PinAuxiliaryContent(candidate.Id);
            if (AuxiliaryPane.IsSection) AuxiliaryPane.SetKindCommand.Execute(ViewerPaneKind.ThreeD);
        }
        else
        {
            hasExplicitMainViewerChoice = true;
            viewerWorkspace.PinMainContent(candidate.Id);
            if (MainPane.IsSection) MainPane.SetKindCommand.Execute(ViewerPaneKind.ThreeD);
        }
        FocusViewerWorkspaceSlot(slot);
        NotifyInspectionDataViewChanged();
        return true;
    }

    private void SetViewerWorkspaceLayout(ViewerWorkspaceLayout layout)
    {
        SetInspectionDataView(ViewerInspectionDataView.Input);
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

        if (maximizedViewerSlotId.Length > 0)
        {
            maximizedViewerSlotId = string.Empty;
            OnPropertyChanged(nameof(MaximizedViewerSlotId));
        }
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
        AuxiliaryPane.RestoreDefaultKind();
        SetInspectionDataView(ViewerInspectionDataView.Input);
        if (!viewerWorkspace.TryOpenAuxiliaryContent(
                HeightImageViewerContentId,
                ViewerWorkspaceCandidates.Select(candidate => candidate.Id)))
        {
            return;
        }

        workspaceSelection.FocusViewerSlot(viewerWorkspace.FocusedSlotId);
        RaiseViewerWorkspaceCanExecuteChanged();
    }

    private void ShowHeightImageOnly()
    {
        AuxiliaryPane.RestoreDefaultKind();
        SetInspectionDataView(ViewerInspectionDataView.Input);
        if (GetViewerWorkspaceCandidate(HeightImageViewerContentId) is null)
        {
            return;
        }

        viewerWorkspace.PinAuxiliaryContent(HeightImageViewerContentId);
        viewerWorkspace.SetLayout(ViewerWorkspaceLayout.HeightImageOnly);
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
        // Read-only file/sample candidates never become persistent teaching pane pins.
        if (!getInspectionInput().IsActive)
        {
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
        }
        OnPropertyChanged(nameof(ViewerWorkspaceCandidates));
        OnPropertyChanged(nameof(MainViewerCandidates));
        OnPropertyChanged(nameof(MainViewerSummary));
        OnPropertyChanged(nameof(AuxiliaryViewerSummary));
        NotifyInspectionDataViewChanged();
        RaiseViewerWorkspaceCanExecuteChanged();
    }

    private void OnViewerWorkspacePropertyChanged(PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewerWorkspaceSession.Layout) && maximizedViewerSlotId.Length > 0)
        {
            maximizedViewerSlotId = string.Empty;
            OnPropertyChanged(nameof(MaximizedViewerSlotId));
        }
        toggleViewerPaneMaximizedCommand.RaiseCanExecuteChanged();
        NotifyInspectionDataViewChanged();
        OnPropertyChanged(nameof(ViewerWorkspaceLayoutSummary));
        OnPropertyChanged(nameof(IsSingleViewerLayout));
        OnPropertyChanged(nameof(IsHeightImageOnlyViewerLayout));
        OnPropertyChanged(nameof(IsSplitVerticalViewerLayout));
        OnPropertyChanged(nameof(IsSplitHorizontalViewerLayout));
        OnPropertyChanged(nameof(IsPopOutViewerLayout));
        OnPropertyChanged(nameof(IsFourViewerLayout));
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
            OnPropertyChanged(nameof(MainViewerContentId));
            OnPropertyChanged(nameof(AuxiliaryViewerContentId));
            OnPropertyChanged(nameof(ViewerCameraLinkLabel));
            OnPropertyChanged(nameof(ViewerCameraLinkSummary));
            NotifyInspectionDataViewChanged();
        }
    }

    private void RaiseViewerWorkspaceCanExecuteChanged()
    {
        endOutputCompareViewerCommand.RaiseCanExecuteChanged();
        splitViewerVerticallyCommand.RaiseCanExecuteChanged();
        splitViewerHorizontallyCommand.RaiseCanExecuteChanged();
        popOutViewerCommand.RaiseCanExecuteChanged();
        setFourViewerLayoutCommand.RaiseCanExecuteChanged();
        openHeightImageCommand.RaiseCanExecuteChanged();
        showHeightImageOnlyCommand.RaiseCanExecuteChanged();
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

public enum ViewerInspectionDataView
{
    Input,
    Output,
    InputOutput
}
