using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
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

    private SurfaceMatchExecutionArtifact? surfaceMatchEvidence;
    private SurfaceMatchAssessmentArtifact? surfaceMatchAssessment;
    private SurfaceMatchRuntimeReport? surfaceMatchRuntime;
    private SurfaceAndEdgeMatchScoreArtifact? surfaceEdgeScore;
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
    public SurfaceMatchExecutionArtifact? SurfaceMatchEvidence =>
        surfaceMatchEvidence;
    public bool HasSurfaceMatchEvidence =>
        surfaceMatchEvidence is not null;
    public SurfaceMatchAssessmentArtifact? SurfaceMatchAssessment =>
        surfaceMatchAssessment;
    public SurfaceMatchRuntimeReport? SurfaceMatchRuntime =>
        surfaceMatchRuntime;
    public SurfaceAndEdgeMatchScoreArtifact? SurfaceEdgeScore =>
        surfaceEdgeScore;
    public event EventHandler<ToolWorkbenchSurfaceMatchDisplayRequestEventArgs>?
        SurfaceMatchDisplayRequested;
    public event EventHandler?
        SurfaceMatchDisplayCleared;
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
    public bool IsSingleViewerLayout => ViewerWorkspace.Layout == ViewerWorkspaceLayout.Single;
    public bool IsSplitVerticalViewerLayout => ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitVertical;
    public bool IsSplitHorizontalViewerLayout => ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitHorizontal;
    public bool IsPopOutViewerLayout => ViewerWorkspace.Layout == ViewerWorkspaceLayout.PopOut;

    public string AuxiliaryViewerSummary =>
        GetViewerWorkspaceCandidate(ViewerWorkspace.AuxiliaryContentId) is { } candidate
            ? $"{candidate.DisplayName} | {candidate.Contract}"
            : Localization.ViewerAuxiliaryNoOutput;

    public ViewerWorkspaceCandidateItem? GetViewerWorkspaceCandidate(string? contentId) =>
        ViewerWorkspaceCandidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, contentId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Routes already-executed surface-match evidence to the Viewer. This is
    /// presentation-only and does not execute Preview, Publish, Run, or
    /// Validation and does not edit recipe, source, or ROI state.
    /// </summary>
    public void ShowSurfaceMatchEvidence(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchAssessmentArtifact? assessment = null,
        SurfaceMatchRuntimeReport? runtime = null,
        SurfaceAndEdgeMatchScoreArtifact? edgeScore = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(execution);
        var validity =
            SurfaceMatchExecutionArtifactValidator.Inspect(execution);
        if (!validity.IsValid
            || !string.Equals(
                model.ContentSha256,
                execution.ModelContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                scene.ContentSha256,
                execution.SceneContentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Workbench surface-match evidence is invalid or does not match the supplied model and scene.");
        }

        if (assessment is not null)
        {
            var assessmentValidity =
                SurfaceMatchAssessmentArtifactValidator.Inspect(
                    assessment);
            if (!assessmentValidity.IsValid
                || !string.Equals(
                    assessment.ExecutionContentSha256,
                    execution.ContentSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Workbench surface-match assessment is invalid or linked to a different raw execution.");
            }
        }

        if (runtime is not null
            && (!SurfaceMatchAssessmentArtifactValidator
                    .InspectRuntime(runtime, out _)
                || assessment is null
                || !string.Equals(
                    runtime.ExecutionContentSha256,
                    execution.ContentSha256,
                    StringComparison.Ordinal)
                || !string.Equals(
                    runtime.AssessmentContentSha256,
                    assessment.ContentSha256,
                    StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Workbench surface-match runtime is invalid or linked to different execution evidence.");
        }

        if (edgeScore is not null
            && !SurfaceEdgeArtifactValidator
                .Inspect(edgeScore, execution).IsValid)
        {
            throw new InvalidDataException(
                "Workbench surface/edge score is invalid or linked to a different raw execution.");
        }

        surfaceMatchEvidence = execution;
        surfaceMatchAssessment = assessment;
        surfaceMatchRuntime = runtime;
        surfaceEdgeScore = edgeScore;
        OnPropertyChanged(nameof(SurfaceMatchEvidence));
        OnPropertyChanged(nameof(HasSurfaceMatchEvidence));
        OnPropertyChanged(nameof(SurfaceMatchAssessment));
        OnPropertyChanged(nameof(SurfaceMatchRuntime));
        OnPropertyChanged(nameof(SurfaceEdgeScore));
        SurfaceMatchDisplayRequested?.Invoke(
            this,
            new ToolWorkbenchSurfaceMatchDisplayRequestEventArgs(
                model,
                scene,
                execution,
                assessment,
                runtime,
                edgeScore));
    }

    public void ClearSurfaceMatchEvidence()
    {
        if (surfaceMatchEvidence is null)
        {
            return;
        }

        surfaceMatchEvidence = null;
        surfaceMatchAssessment = null;
        surfaceMatchRuntime = null;
        surfaceEdgeScore = null;
        OnPropertyChanged(nameof(SurfaceMatchEvidence));
        OnPropertyChanged(nameof(HasSurfaceMatchEvidence));
        OnPropertyChanged(nameof(SurfaceMatchAssessment));
        OnPropertyChanged(nameof(SurfaceMatchRuntime));
        OnPropertyChanged(nameof(SurfaceEdgeScore));
        SurfaceMatchDisplayCleared?.Invoke(this, EventArgs.Empty);
    }

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
        OnPropertyChanged(nameof(IsSingleViewerLayout));
        OnPropertyChanged(nameof(IsSplitVerticalViewerLayout));
        OnPropertyChanged(nameof(IsSplitHorizontalViewerLayout));
        OnPropertyChanged(nameof(IsPopOutViewerLayout));
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

public sealed record ToolWorkbenchSurfaceMatchDisplayRequestEventArgs(
    SurfaceModelArtifact Model,
    PreparedSceneArtifact Scene,
    SurfaceMatchExecutionArtifact Execution,
    SurfaceMatchAssessmentArtifact? Assessment,
    SurfaceMatchRuntimeReport? Runtime,
    SurfaceAndEdgeMatchScoreArtifact? EdgeScore);
