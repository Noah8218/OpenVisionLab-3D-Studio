using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Preserves Viewer workspace bindings while delegating presentation to
/// ViewerWorkspaceViewModel. Height Image loading and Surface Match evidence
/// remain explicit composition seams to their existing owners.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    public const string HeightImageViewerContentId = ViewerWorkspaceViewModel.HeightImageViewerContentId;

    private bool isSurfaceEdgeAcquisitionDirectionStale;

    public ViewerWorkspaceSession ViewerWorkspace { get; }
    public ICommand SetSingleViewerLayoutCommand => viewerWorkspaceViewModel.SetSingleViewerLayoutCommand;
    public ICommand SplitViewerVerticallyCommand => viewerWorkspaceViewModel.SplitViewerVerticallyCommand;
    public ICommand SplitViewerHorizontallyCommand => viewerWorkspaceViewModel.SplitViewerHorizontallyCommand;
    public ICommand PopOutViewerCommand => viewerWorkspaceViewModel.PopOutViewerCommand;
    public ICommand FocusViewerWorkspaceSlotCommand => viewerWorkspaceViewModel.FocusViewerWorkspaceSlotCommand;
    public ICommand OpenHeightImageCommand => viewerWorkspaceViewModel.OpenHeightImageCommand;
    public ICommand ClearMainViewerPinCommand => viewerWorkspaceViewModel.ClearMainViewerPinCommand;
    public ICommand ClearAuxiliaryViewerPinCommand => viewerWorkspaceViewModel.ClearAuxiliaryViewerPinCommand;
    public ICommand ToggleViewerCameraLinkCommand => viewerWorkspaceViewModel.ToggleViewerCameraLinkCommand;
    public SurfaceMatchExecutionArtifact? SurfaceMatchEvidence =>
        surfaceMatchExperiment.Published?.Execution;
    public bool HasSurfaceMatchEvidence =>
        surfaceMatchExperiment.Published is not null;
    public SurfaceMatchAssessmentArtifact? SurfaceMatchAssessment =>
        surfaceMatchExperiment.Published?.Assessment;
    public SurfaceMatchRuntimeReport? SurfaceMatchRuntime =>
        surfaceMatchExperiment.Published?.Runtime;
    public SurfaceAndEdgeMatchScoreArtifact? SurfaceEdgeScore =>
        surfaceMatchExperiment.Published?.EdgeScore;
    public SurfaceEdgeDiagnosticOverlayArtifact? SurfaceEdgeDiagnosticOverlay =>
        surfaceMatchExperiment.Published?.EdgeDiagnosticOverlay;
    public SurfaceEdgeAcquisitionDirectionArtifact? SurfaceEdgeAcquisitionDirection =>
        surfaceMatchExperiment.Published?.AcquisitionDirectionOrientation;
    public bool IsSurfaceEdgeAcquisitionDirectionStale =>
        isSurfaceEdgeAcquisitionDirectionStale;
    public SurfaceAndEdgeMatchAssessmentArtifact? SurfaceEdgeAssessment =>
        surfaceMatchExperiment.Published?.EdgeAssessment;
    public SurfaceMatchFalsePositiveReviewArtifact? SurfaceMatchFalsePositiveReview =>
        surfaceMatchExperiment.Published?.FalsePositiveReview;
    public event EventHandler<ToolWorkbenchSurfaceMatchDisplayRequestEventArgs>?
        SurfaceMatchDisplayRequested;
    public event EventHandler?
        SurfaceMatchDisplayCleared;
    public IReadOnlyList<ViewerWorkspaceCandidateItem> ViewerWorkspaceCandidates => viewerWorkspaceViewModel.ViewerWorkspaceCandidates;
    public IReadOnlyList<ViewerWorkspaceCandidateItem> MainViewerCandidates => viewerWorkspaceViewModel.MainViewerCandidates;
    public bool IsViewerCameraLinked => viewerWorkspaceViewModel.IsViewerCameraLinked;
    public bool CanLinkViewerCameras => viewerWorkspaceViewModel.CanLinkViewerCameras;
    public string ViewerCameraLinkLabel => viewerWorkspaceViewModel.ViewerCameraLinkLabel;
    public string ViewerCameraLinkSummary => viewerWorkspaceViewModel.ViewerCameraLinkSummary;

    public string MainViewerContentId
    {
        get => viewerWorkspaceViewModel.MainViewerContentId;
        set => viewerWorkspaceViewModel.MainViewerContentId = value;
    }

    public string AuxiliaryViewerContentId
    {
        get => viewerWorkspaceViewModel.AuxiliaryViewerContentId;
        set => viewerWorkspaceViewModel.AuxiliaryViewerContentId = value;
    }

    public string ViewerWorkspaceLayoutSummary => viewerWorkspaceViewModel.ViewerWorkspaceLayoutSummary;
    public bool IsSingleViewerLayout => viewerWorkspaceViewModel.IsSingleViewerLayout;
    public bool IsSplitVerticalViewerLayout => viewerWorkspaceViewModel.IsSplitVerticalViewerLayout;
    public bool IsSplitHorizontalViewerLayout => viewerWorkspaceViewModel.IsSplitHorizontalViewerLayout;
    public bool IsPopOutViewerLayout => viewerWorkspaceViewModel.IsPopOutViewerLayout;
    public string AuxiliaryViewerSummary => viewerWorkspaceViewModel.AuxiliaryViewerSummary;
    public string MainViewerSummary => viewerWorkspaceViewModel.MainViewerSummary;

    public ViewerWorkspaceCandidateItem? GetViewerWorkspaceCandidate(string? contentId) =>
        viewerWorkspaceViewModel.GetViewerWorkspaceCandidate(contentId);

    public ViewerWorkspaceCandidateItem? GetMainViewerCandidate(string? contentId) =>
        viewerWorkspaceViewModel.GetMainViewerCandidate(contentId);

    public Task EnsureHeightImageSourceAsync() =>
        HeightImageViewer.EnsureSourceAsync(
            Source.Path,
            Source.Id,
            Source.Unit,
            Source.FrameId,
            GetOrLoadDecodedC3DSourceAsync);

    internal void BeginHeightImageSourceLoad()
    {
        HeightImageViewer.StartObservedLoad(
            EnsureHeightImageSourceAsync,
            ReportHeightImageSourceLoadFailure);
    }

    private void ReportHeightImageSourceLoadFailure(Exception exception)
    {
        if (!IsDisposed)
        {
            AppendLog("Viewer", $"Height Image load failed: {exception.Message}");
        }
    }

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
        var evidence = SurfaceMatchExperimentEvidence.CreateForDisplay(
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

        ClearSurfaceMatchCollection();
        isSurfaceEdgeAcquisitionDirectionStale = false;
        LoadPublishedSurfaceMatchExperiment(evidence);
        RaiseSurfaceMatchExperimentDisplay(evidence);
    }

    public void ClearSurfaceMatchEvidence()
    {
        if (surfaceMatchExperiment.Published is null)
        {
            return;
        }

        isSurfaceEdgeAcquisitionDirectionStale = false;
        ClearSurfaceMatchExperiment();
        ClearSurfaceMatchCollection();
        SurfaceMatchDisplayCleared?.Invoke(this, EventArgs.Empty);
    }

    private void InitializeViewerWorkspace()
    {
        viewerWorkspaceViewModel = new ViewerWorkspaceViewModel(
            ViewerWorkspace,
            WorkspaceSelection,
            Localization,
            () => (IsSourceReadyForRecipe, Source.Path),
            () => RenderableC3DCatalog,
            () => (CompareSlotAArtifactId, CompareSlotBArtifactId, CompareSlotCArtifactId));
        viewerWorkspaceEventCoordinator = new ToolWorkbenchViewerWorkspaceEventCoordinator(
            viewerWorkspaceViewModel,
            args => OnPropertyChanged(args.PropertyName));
    }

    private void SynchronizeViewerWorkspaceFocus(string? slotId) =>
        viewerWorkspaceViewModel.SynchronizeViewerWorkspaceFocus(slotId);

    private void ReconcileViewerWorkspaceContents(bool preferHeightImage = false) =>
        viewerWorkspaceViewModel.ReconcileViewerWorkspaceContents(preferHeightImage);

    private void OnViewerWorkspaceLocalizationChanged(object? sender, PropertyChangedEventArgs args) =>
        viewerWorkspaceViewModel.RefreshLocalization(args);
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
