using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Composes the independent Viewer workspace session with current renderable
/// artifacts. Commands change presentation only and never edit or execute the
/// recipe.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    public const string HeightImageViewerContentId = "viewer.height-image";

    private RelayCommand setSingleViewerLayoutCommand = null!;
    private RelayCommand splitViewerVerticallyCommand = null!;
    private RelayCommand splitViewerHorizontallyCommand = null!;
    private RelayCommand popOutViewerCommand = null!;
    private RelayCommand focusViewerWorkspaceSlotCommand = null!;
    private RelayCommand openHeightImageCommand = null!;

    public ViewerWorkspaceSession ViewerWorkspace { get; }
    public ICommand SetSingleViewerLayoutCommand => setSingleViewerLayoutCommand;
    public ICommand SplitViewerVerticallyCommand => splitViewerVerticallyCommand;
    public ICommand SplitViewerHorizontallyCommand => splitViewerHorizontallyCommand;
    public ICommand PopOutViewerCommand => popOutViewerCommand;
    public ICommand FocusViewerWorkspaceSlotCommand => focusViewerWorkspaceSlotCommand;
    public ICommand OpenHeightImageCommand => openHeightImageCommand;
    public IReadOnlyList<ViewerWorkspaceCandidateItem> ViewerWorkspaceCandidates
    {
        get
        {
            var candidates = new List<ViewerWorkspaceCandidateItem>();
            if (IsSourceReadyForRecipe && File.Exists(Source.Path))
            {
                candidates.Add(new ViewerWorkspaceCandidateItem(
                    HeightImageViewerContentId,
                    Localization.HeightImage,
                    ViewerWorkspaceCandidateKind.HeightImage,
                    Source.Path,
                    "HeightField / native grid",
                    "Ready",
                    true));
            }

            candidates.AddRange(
                CompareCandidates
                    .Where(candidate =>
                        !string.IsNullOrWhiteSpace(candidate.Id)
                        && File.Exists(candidate.C3DPath))
                    .Select(candidate => new ViewerWorkspaceCandidateItem(
                        candidate.Id,
                        candidate.DisplayName,
                        ViewerWorkspaceCandidateKind.ThreeDArtifact,
                        candidate.C3DPath,
                        candidate.Contract,
                        candidate.State,
                        candidate.IsSource)));
            return candidates;
        }
    }

    public string AuxiliaryViewerContentId
    {
        get => ViewerWorkspace.AuxiliaryContentId;
        set
        {
            var candidate = GetViewerWorkspaceCandidate(value);
            if (candidate is null)
            {
                return;
            }

            ViewerWorkspace.PinAuxiliaryContent(value);
            if (candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact)
            {
                WorkspaceSelection.SelectOutput(value);
            }
            FocusViewerWorkspaceSlot(ViewerWorkspaceSession.AuxiliarySlotId);
        }
    }

    public string ViewerWorkspaceLayoutSummary => ViewerWorkspace.Layout switch
    {
        ViewerWorkspaceLayout.SplitVertical => Localization.ViewerSplitVertical,
        ViewerWorkspaceLayout.SplitHorizontal => Localization.ViewerSplitHorizontal,
        ViewerWorkspaceLayout.PopOut => Localization.ViewerPopOut,
        _ => Localization.ViewerSingle
    };

    public string AuxiliaryViewerSummary =>
        GetViewerWorkspaceCandidate(ViewerWorkspace.AuxiliaryContentId) is { } candidate
            ? $"{candidate.DisplayName} | {candidate.Contract}"
            : Localization.ViewerAuxiliaryNoOutput;

    public ViewerWorkspaceCandidateItem? GetViewerWorkspaceCandidate(string? contentId) =>
        ViewerWorkspaceCandidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, contentId, StringComparison.OrdinalIgnoreCase));

    private void InitializeViewerWorkspace()
    {
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
        focusViewerWorkspaceSlotCommand = new RelayCommand(
            parameter => FocusViewerWorkspaceSlot(parameter as string),
            parameter => parameter is string slotId
                         && (string.Equals(
                                 slotId,
                                 ViewerWorkspaceSession.MainSlotId,
                                 StringComparison.OrdinalIgnoreCase)
                             || ViewerWorkspace.HasAuxiliarySlot
                             && string.Equals(
                                 slotId,
                                 ViewerWorkspaceSession.AuxiliarySlotId,
                                 StringComparison.OrdinalIgnoreCase)));
        ViewerWorkspace.PropertyChanged += OnViewerWorkspacePropertyChanged;
        Localization.PropertyChanged += OnViewerWorkspaceLocalizationChanged;
    }

    private void SetViewerWorkspaceLayout(ViewerWorkspaceLayout layout)
    {
        if (layout != ViewerWorkspaceLayout.Single)
        {
            ReconcileViewerWorkspaceContents(
                preferHeightImage: ViewerWorkspace.Layout == ViewerWorkspaceLayout.Single);
            if (string.IsNullOrWhiteSpace(ViewerWorkspace.AuxiliaryContentId))
            {
                return;
            }
        }

        ViewerWorkspace.SetLayout(layout);
        if (layout == ViewerWorkspaceLayout.Single)
        {
            FocusViewerWorkspaceSlot(ViewerWorkspaceSession.MainSlotId);
        }

        RaiseViewerWorkspaceCanExecuteChanged();
    }

    private void FocusViewerWorkspaceSlot(string? slotId)
    {
        ViewerWorkspace.FocusSlot(slotId);
        WorkspaceSelection.FocusViewerSlot(ViewerWorkspace.FocusedSlotId);
        focusViewerWorkspaceSlotCommand.RaiseCanExecuteChanged();
    }

    private void SynchronizeViewerWorkspaceFocus(string? slotId)
    {
        ViewerWorkspace.FocusSlot(slotId);
        focusViewerWorkspaceSlotCommand?.RaiseCanExecuteChanged();
    }

    private bool CanOpenAuxiliaryViewer() =>
        ViewerWorkspaceCandidates.Count > 0;

    private void OpenHeightImage()
    {
        if (GetViewerWorkspaceCandidate(HeightImageViewerContentId) is null)
        {
            return;
        }

        ViewerWorkspace.PinAuxiliaryContent(HeightImageViewerContentId);
        ViewerWorkspace.SetLayout(ViewerWorkspaceLayout.SplitVertical);
        FocusViewerWorkspaceSlot(ViewerWorkspaceSession.AuxiliarySlotId);
        RaiseViewerWorkspaceCanExecuteChanged();
    }

    private void ReconcileViewerWorkspaceContents(bool preferHeightImage = false)
    {
        var available = ViewerWorkspaceCandidates;
        var preferredCompareId = new[]
        {
            CompareSlotBArtifactId,
            CompareSlotAArtifactId,
            CompareSlotCArtifactId
        }.FirstOrDefault(id => available.Any(candidate =>
            candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact
            && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase)));
        var preferred = preferHeightImage
                        && available.Any(candidate => candidate.Kind == ViewerWorkspaceCandidateKind.HeightImage)
            ? HeightImageViewerContentId
            : preferredCompareId ?? available.FirstOrDefault()?.Id;

        if (preferHeightImage && !string.IsNullOrWhiteSpace(preferred))
        {
            ViewerWorkspace.PinAuxiliaryContent(preferred);
        }
        else
        {
            ViewerWorkspace.ReconcileContents(
                available.Select(candidate => candidate.Id),
                preferred);
        }
        OnPropertyChanged(nameof(ViewerWorkspaceCandidates));
        OnPropertyChanged(nameof(AuxiliaryViewerSummary));
        RaiseViewerWorkspaceCanExecuteChanged();
    }

    private void OnViewerWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(ViewerWorkspaceLayoutSummary));
        if (args.PropertyName == nameof(ViewerWorkspaceSession.AuxiliaryContentId))
        {
            OnPropertyChanged(nameof(AuxiliaryViewerContentId));
            OnPropertyChanged(nameof(AuxiliaryViewerSummary));
        }
    }

    private void OnViewerWorkspaceLocalizationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ThreeDLocalization.ViewerSingle)
            or nameof(ThreeDLocalization.ViewerSplitVertical)
            or nameof(ThreeDLocalization.ViewerSplitHorizontal)
            or nameof(ThreeDLocalization.ViewerPopOut)
            or nameof(ThreeDLocalization.ViewerAuxiliaryNoOutput)
            or nameof(ThreeDLocalization.HeightImage))
        {
            OnPropertyChanged(nameof(ViewerWorkspaceLayoutSummary));
            OnPropertyChanged(nameof(AuxiliaryViewerSummary));
            OnPropertyChanged(nameof(ViewerWorkspaceCandidates));
        }
    }

    private void RaiseViewerWorkspaceCanExecuteChanged()
    {
        splitViewerVerticallyCommand?.RaiseCanExecuteChanged();
        splitViewerHorizontallyCommand?.RaiseCanExecuteChanged();
        popOutViewerCommand?.RaiseCanExecuteChanged();
        openHeightImageCommand?.RaiseCanExecuteChanged();
        focusViewerWorkspaceSlotCommand?.RaiseCanExecuteChanged();
    }
}

public sealed record ViewerWorkspaceCandidateItem(
    string Id,
    string DisplayName,
    ViewerWorkspaceCandidateKind Kind,
    string SourcePath,
    string Contract,
    string State,
    bool IsSource);

public enum ViewerWorkspaceCandidateKind
{
    HeightImage,
    ThreeDArtifact
}
