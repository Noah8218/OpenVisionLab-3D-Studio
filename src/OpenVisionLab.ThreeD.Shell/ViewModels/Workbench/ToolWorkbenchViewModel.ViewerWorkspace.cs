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
    private SurfaceEdgeDiagnosticOverlayArtifact? surfaceEdgeDiagnosticOverlay;
    private SurfaceEdgeAcquisitionDirectionArtifact? surfaceEdgeAcquisitionDirection;
    private bool isSurfaceEdgeAcquisitionDirectionStale;
    private SurfaceAndEdgeMatchAssessmentArtifact? surfaceEdgeAssessment;
    private SurfaceMatchFalsePositiveReviewArtifact? surfaceMatchFalsePositiveReview;
    private RelayCommand setSingleViewerLayoutCommand = null!;
    private RelayCommand splitViewerVerticallyCommand = null!;
    private RelayCommand splitViewerHorizontallyCommand = null!;
    private RelayCommand popOutViewerCommand = null!;
    private RelayCommand focusViewerWorkspaceSlotCommand = null!;
    private RelayCommand openHeightImageCommand = null!;
    private RelayCommand clearMainViewerPinCommand = null!;
    private RelayCommand clearAuxiliaryViewerPinCommand = null!;
    private RelayCommand toggleViewerCameraLinkCommand = null!;

    public ViewerWorkspaceSession ViewerWorkspace { get; }
    public ICommand SetSingleViewerLayoutCommand => setSingleViewerLayoutCommand;
    public ICommand SplitViewerVerticallyCommand => splitViewerVerticallyCommand;
    public ICommand SplitViewerHorizontallyCommand => splitViewerHorizontallyCommand;
    public ICommand PopOutViewerCommand => popOutViewerCommand;
    public ICommand FocusViewerWorkspaceSlotCommand => focusViewerWorkspaceSlotCommand;
    public ICommand OpenHeightImageCommand => openHeightImageCommand;
    public ICommand ClearMainViewerPinCommand => clearMainViewerPinCommand;
    public ICommand ClearAuxiliaryViewerPinCommand => clearAuxiliaryViewerPinCommand;
    public ICommand ToggleViewerCameraLinkCommand => toggleViewerCameraLinkCommand;
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
    public SurfaceEdgeDiagnosticOverlayArtifact? SurfaceEdgeDiagnosticOverlay =>
        surfaceEdgeDiagnosticOverlay;
    public SurfaceEdgeAcquisitionDirectionArtifact? SurfaceEdgeAcquisitionDirection =>
        surfaceEdgeAcquisitionDirection;
    public bool IsSurfaceEdgeAcquisitionDirectionStale =>
        isSurfaceEdgeAcquisitionDirectionStale;
    public SurfaceAndEdgeMatchAssessmentArtifact? SurfaceEdgeAssessment =>
        surfaceEdgeAssessment;
    public SurfaceMatchFalsePositiveReviewArtifact? SurfaceMatchFalsePositiveReview =>
        surfaceMatchFalsePositiveReview;
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

    public IReadOnlyList<ViewerWorkspaceCandidateItem> MainViewerCandidates =>
        ViewerWorkspaceCandidates
            .Where(candidate => candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact)
            .ToArray();

    public bool IsViewerCameraLinked => ViewerWorkspace.IsCameraLinked;

    public bool CanLinkViewerCameras =>
        ViewerWorkspace.HasAuxiliarySlot
        && GetViewerWorkspaceCandidate(ViewerWorkspace.AuxiliaryContentId)?.Kind
            == ViewerWorkspaceCandidateKind.ThreeDArtifact;

    public string ViewerCameraLinkLabel =>
        ViewerWorkspace.IsCameraLinked
            ? Localization.ViewerCameraUnlink
            : Localization.ViewerCameraLink;

    public string ViewerCameraLinkSummary =>
        ViewerWorkspace.IsCameraLinked
            ? Localization.ViewerCameraLinked
            : CanLinkViewerCameras
                ? Localization.ViewerCameraLinkSummary
                : Localization.ViewerCameraLinkUnavailable;

    public string MainViewerContentId
    {
        get => ViewerWorkspace.MainContentId;
        set
        {
            var candidate = GetMainViewerCandidate(value);
            if (candidate is null)
            {
                return;
            }

            ViewerWorkspace.PinMainContent(candidate.Id);
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

            ViewerWorkspace.PinAuxiliaryContent(candidate.Id);
            if (candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact
                && !candidate.IsSource)
            {
                WorkspaceSelection.SelectOutput(candidate.Id);
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
            : ViewerWorkspace.IsAuxiliaryContentPinned
                ? $"{Localization.ViewerPinnedUnavailable} | {ViewerWorkspace.AuxiliaryContentId}"
            : Localization.ViewerAuxiliaryNoOutput;

    public string MainViewerSummary =>
        GetMainViewerCandidate(ViewerWorkspace.MainContentId) is { } candidate
            ? $"{candidate.DisplayName} | {candidate.Contract}"
            : ViewerWorkspace.IsMainContentPinned
                ? $"{Localization.ViewerPinnedUnavailable} | {ViewerWorkspace.MainContentId}"
                : Localization.ViewerMainNoOutput;

    public ViewerWorkspaceCandidateItem? GetViewerWorkspaceCandidate(string? contentId) =>
        ViewerWorkspaceCandidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, contentId, StringComparison.OrdinalIgnoreCase));

    public ViewerWorkspaceCandidateItem? GetMainViewerCandidate(string? contentId) =>
        MainViewerCandidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, contentId, StringComparison.OrdinalIgnoreCase));

    public Task EnsureHeightImageSourceAsync() =>
        HeightImageViewer.EnsureSourceAsync(
            Source.Path,
            Source.Id,
            Source.Unit,
            Source.FrameId,
            GetOrLoadDecodedC3DSourceAsync);

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
        SurfaceAndEdgeMatchScoreArtifact? edgeScore = null,
        SurfaceEdgeDiagnosticOverlayArtifact? edgeDiagnosticOverlay = null,
        SurfaceAndEdgeMatchAssessmentArtifact? edgeAssessment = null,
        SurfaceMatchFalsePositiveReviewArtifact? falsePositiveReview = null,
        SurfaceEdgeAcquisitionDirectionArtifact? acquisitionDirectionOrientation = null)
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

        if (edgeDiagnosticOverlay is not null
            && (edgeScore is null
                || !SurfaceEdgeDiagnosticOverlayArtifactValidator
                    .Inspect(edgeDiagnosticOverlay).IsValid
                || edgeDiagnosticOverlay.SurfaceMatchExecutionContentSha256
                    != execution.ContentSha256
                || edgeDiagnosticOverlay.ModelContentSha256
                    != model.ContentSha256
                || edgeDiagnosticOverlay.SceneContentSha256
                    != scene.ContentSha256
                || edgeDiagnosticOverlay.ScoreContentSha256
                    != edgeScore.ContentSha256))
        {
            throw new InvalidDataException(
                "Workbench edge diagnostic overlay is invalid or linked to different evidence.");
        }

        if (edgeAssessment is not null
            && (edgeScore is null
                || !SurfaceAndEdgeAssessmentArtifactValidator
                    .Inspect(edgeAssessment, edgeScore).IsValid))
        {
            throw new InvalidDataException(
                "Workbench independent surface/edge assessment is invalid or linked to a different score.");
        }

        if (acquisitionDirectionOrientation is not null
            && (edgeDiagnosticOverlay is null
                || !SurfaceEdgeAcquisitionDirectionArtifactValidator
                    .Inspect(acquisitionDirectionOrientation, edgeDiagnosticOverlay).IsValid))
        {
            throw new InvalidDataException(
                "Workbench acquisition-direction orientation is invalid or linked to a different edge overlay.");
        }

        if (falsePositiveReview is not null
            && (!SurfaceMatchFalsePositiveReviewArtifactValidator
                    .Inspect(falsePositiveReview).IsValid
                || falsePositiveReview.ModelContentSha256
                    != model.ContentSha256
                || !ReviewContains(
                    falsePositiveReview,
                    scene,
                    execution,
                    edgeScore,
                    edgeAssessment)))
        {
            throw new InvalidDataException(
                "Workbench false-positive review is invalid or does not contain the displayed case.");
        }

        ClearSurfaceMatchCollection();
        var evidence = new SurfaceMatchExperimentEvidence(
            model,
            scene,
            execution,
            assessment,
            runtime,
            edgeScore,
            edgeDiagnosticOverlay,
            edgeAssessment,
            falsePositiveReview,
            acquisitionDirectionOrientation);
        isSurfaceEdgeAcquisitionDirectionStale = false;
        LoadPublishedSurfaceMatchExperiment(evidence);
        RaiseSurfaceMatchExperimentDisplay(evidence);
    }

    public void ClearSurfaceMatchEvidence()
    {
        if (surfaceMatchEvidence is null)
        {
            return;
        }

        ClearSurfaceMatchExperiment();
        ClearSurfaceMatchCollection();
        surfaceMatchEvidence = null;
        surfaceMatchAssessment = null;
        surfaceMatchRuntime = null;
        surfaceEdgeScore = null;
        surfaceEdgeDiagnosticOverlay = null;
        surfaceEdgeAcquisitionDirection = null;
        isSurfaceEdgeAcquisitionDirectionStale = false;
        surfaceEdgeAssessment = null;
        surfaceMatchFalsePositiveReview = null;
        RaisePublishedSurfaceMatchProperties();
        SurfaceMatchDisplayCleared?.Invoke(this, EventArgs.Empty);
    }

    private static bool ReviewContains(
        SurfaceMatchFalsePositiveReviewArtifact review,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceAndEdgeMatchScoreArtifact? score,
        SurfaceAndEdgeMatchAssessmentArtifact? assessment)
    {
        if (score is null || assessment is null)
        {
            return false;
        }

        return new[] { review.Accepted, review.Rejected }.Any(item =>
            item.SceneContentSha256 == scene.ContentSha256
            && item.SurfaceMatchExecutionContentSha256 == execution.ContentSha256
            && item.ScoreContentSha256 == score.ContentSha256
            && item.AssessmentContentSha256 == assessment.ContentSha256);
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
        clearMainViewerPinCommand = new RelayCommand(
            _ => ViewerWorkspace.ClearMainContent(),
            _ => ViewerWorkspace.IsMainContentPinned);
        clearAuxiliaryViewerPinCommand = new RelayCommand(
            _ => ViewerWorkspace.ClearAuxiliaryContent(),
            _ => ViewerWorkspace.IsAuxiliaryContentPinned);
        toggleViewerCameraLinkCommand = new RelayCommand(
            _ => ToggleViewerCameraLink(),
            _ => ViewerWorkspace.IsCameraLinked || CanLinkViewerCameras);
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
        var available = ViewerWorkspaceCandidates;
        var preferred = GetPreferredViewerWorkspaceContentId(
            available,
            preferHeightImage: ViewerWorkspace.Layout == ViewerWorkspaceLayout.Single);
        if (!ViewerWorkspace.TrySetLayout(
                layout,
                available.Select(candidate => candidate.Id),
                preferred))
        {
            return;
        }

        WorkspaceSelection.FocusViewerSlot(ViewerWorkspace.FocusedSlotId);

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
        if (!ViewerWorkspace.TryOpenAuxiliaryContent(
                HeightImageViewerContentId,
                ViewerWorkspaceCandidates.Select(candidate => candidate.Id)))
        {
            return;
        }

        WorkspaceSelection.FocusViewerSlot(ViewerWorkspace.FocusedSlotId);
        RaiseViewerWorkspaceCanExecuteChanged();
    }

    private void ToggleViewerCameraLink()
    {
        if (ViewerWorkspace.IsCameraLinked)
        {
            ViewerWorkspace.SetCameraLinked(false);
        }
        else if (CanLinkViewerCameras)
        {
            ViewerWorkspace.SetCameraLinked(true);
        }
    }

    private string? GetPreferredViewerWorkspaceContentId(
        IReadOnlyList<ViewerWorkspaceCandidateItem> available,
        bool preferHeightImage)
    {
        var preferredCompareId = new[]
        {
            CompareSlotBArtifactId,
            CompareSlotAArtifactId,
            CompareSlotCArtifactId
        }.FirstOrDefault(id => available.Any(candidate =>
            candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact
            && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase)));
        return preferHeightImage
               && available.Any(candidate => candidate.Kind == ViewerWorkspaceCandidateKind.HeightImage)
            ? HeightImageViewerContentId
            : preferredCompareId ?? available.FirstOrDefault()?.Id;
    }

    private void ReconcileViewerWorkspaceContents(bool preferHeightImage = false)
    {
        var available = ViewerWorkspaceCandidates;
        var preferred = GetPreferredViewerWorkspaceContentId(available, preferHeightImage);
        var mainCandidates = available
            .Where(candidate => candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact)
            .ToArray();
        ViewerWorkspace.ReconcileMainContent(
            mainCandidates.Select(candidate => candidate.Id),
            GetPreferredMainViewerContentId(mainCandidates));

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
        OnPropertyChanged(nameof(MainViewerCandidates));
        OnPropertyChanged(nameof(MainViewerSummary));
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
            clearMainViewerPinCommand?.RaiseCanExecuteChanged();
            clearAuxiliaryViewerPinCommand?.RaiseCanExecuteChanged();
            toggleViewerCameraLinkCommand?.RaiseCanExecuteChanged();
        }
    }

    private void OnViewerWorkspaceLocalizationChanged(object? sender, PropertyChangedEventArgs args)
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
        splitViewerVerticallyCommand?.RaiseCanExecuteChanged();
        splitViewerHorizontallyCommand?.RaiseCanExecuteChanged();
        popOutViewerCommand?.RaiseCanExecuteChanged();
        openHeightImageCommand?.RaiseCanExecuteChanged();
        focusViewerWorkspaceSlotCommand?.RaiseCanExecuteChanged();
        clearMainViewerPinCommand?.RaiseCanExecuteChanged();
        clearAuxiliaryViewerPinCommand?.RaiseCanExecuteChanged();
        toggleViewerCameraLinkCommand?.RaiseCanExecuteChanged();
    }

    private static string? GetPreferredMainViewerContentId(
        IReadOnlyList<ViewerWorkspaceCandidateItem> available)
    {
        return available.FirstOrDefault(candidate => candidate.IsSource)?.Id
            ?? available.FirstOrDefault()?.Id;
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
    SurfaceAndEdgeMatchScoreArtifact? EdgeScore,
    SurfaceEdgeDiagnosticOverlayArtifact? EdgeDiagnosticOverlay,
    SurfaceAndEdgeMatchAssessmentArtifact? EdgeAssessment,
    SurfaceMatchFalsePositiveReviewArtifact? FalsePositiveReview,
    SurfaceEdgeAcquisitionDirectionArtifact? AcquisitionDirectionOrientation = null);
