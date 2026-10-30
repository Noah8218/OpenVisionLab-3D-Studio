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
    private bool isInspectionFileView;
    private bool isValidationSampleView;

    public bool IsInspectionFileView => isInspectionFileView;
    public bool IsValidationSampleView => isValidationSampleView;
    public bool IsReadOnlySampleView => IsInspectionFileView || IsValidationSampleView;
    public ICommand RunCurrentWorkspaceCommand => IsInspectionFileView ? InspectionQueue.RunCheckedCommand : IsValidationSampleView ? RunValidationSetCommand : RunSelectedStepCommand;
    public ICommand RunCurrentRecipeCommand => IsInspectionFileView ? InspectionQueue.RunCheckedCommand : RunTeachingRecipeCommand;
    public ICommand RunCurrentValidationSetCommand => IsInspectionFileView ? InspectionQueue.RunCheckedCommand : RunValidationSetCommand;
    public ICommand CancelCurrentWorkspaceCommand => IsInspectionFileView ? InspectionQueue.StopCommand : IsValidationSampleView ? CancelValidationSetCommand : CancelActiveWorkbenchEditCommand;

    internal bool SetInspectionFileView(bool value)
    {
        if (isInspectionFileView == value) return false;
        isInspectionFileView = value;
        var restoredTeachingRun = !value && orderedRunExecutionOwner.RestoreTeachingRun();
        OnPropertyChanged(nameof(IsInspectionFileView));
        OnPropertyChanged(nameof(IsReadOnlySampleView));
        OnPropertyChanged(nameof(IsCurrentSourceHeaderStatusVisible));
        OnPropertyChanged(nameof(RunCurrentWorkspaceCommand));
        OnPropertyChanged(nameof(RunCurrentRecipeCommand));
        OnPropertyChanged(nameof(RunCurrentValidationSetCommand));
        OnPropertyChanged(nameof(CancelCurrentWorkspaceCommand));
        selectedStepExecutionOwner.RefreshSelectedStepState();
        RefreshHeightImageRoiProjection();
        viewerWorkspaceViewModel.RefreshInspectionFileContext();
        return restoredTeachingRun;
    }

    internal void SetValidationSampleView(bool value)
    {
        if (isValidationSampleView == value) return;
        isValidationSampleView = value;
        OnPropertyChanged(nameof(IsValidationSampleView));
        OnPropertyChanged(nameof(IsReadOnlySampleView));
        OnPropertyChanged(nameof(IsCurrentSourceHeaderStatusVisible));
        OnPropertyChanged(nameof(RunCurrentWorkspaceCommand));
        OnPropertyChanged(nameof(CancelCurrentWorkspaceCommand));
        selectedStepExecutionOwner.RefreshSelectedStepState();
        RefreshHeightImageRoiProjection();
        viewerWorkspaceViewModel.RefreshInspectionFileContext();
    }

    internal void RefreshValidationSampleView() => viewerWorkspaceViewModel.RefreshInspectionFileContext();

    public ViewerWorkspaceSession ViewerWorkspace { get; }
    public ViewerPaneViewModel MainViewerPane => viewerWorkspaceViewModel.MainPane;
    public ViewerPaneViewModel AuxiliaryViewerPane => viewerWorkspaceViewModel.AuxiliaryPane;
    public ViewerPaneViewModel SectionViewerPane => viewerWorkspaceViewModel.SectionPane;
    public ViewerPaneViewModel HeightViewerPane => viewerWorkspaceViewModel.HeightPane;
    public ICommand SetSingleViewerLayoutCommand => viewerWorkspaceViewModel.SetSingleViewerLayoutCommand;
    public ICommand SplitViewerVerticallyCommand => viewerWorkspaceViewModel.SplitViewerVerticallyCommand;
    public ICommand SplitViewerHorizontallyCommand => viewerWorkspaceViewModel.SplitViewerHorizontallyCommand;
    public ICommand SetFourViewerLayoutCommand => viewerWorkspaceViewModel.SetFourViewerLayoutCommand;
    public ICommand PopOutViewerCommand => viewerWorkspaceViewModel.PopOutViewerCommand;
    public ICommand FocusViewerWorkspaceSlotCommand => viewerWorkspaceViewModel.FocusViewerWorkspaceSlotCommand;
    public ICommand OpenHeightImageCommand => viewerWorkspaceViewModel.OpenHeightImageCommand;
    public ICommand ShowHeightImageOnlyCommand => viewerWorkspaceViewModel.ShowHeightImageOnlyCommand;
    public ICommand ClearMainViewerPinCommand => viewerWorkspaceViewModel.ClearMainViewerPinCommand;
    public ICommand ClearAuxiliaryViewerPinCommand => viewerWorkspaceViewModel.ClearAuxiliaryViewerPinCommand;
    public ICommand ToggleViewerCameraLinkCommand => viewerWorkspaceViewModel.ToggleViewerCameraLinkCommand;
    public ICommand SetInspectionDataViewCommand => viewerWorkspaceViewModel.SetInspectionDataViewCommand;
    public ICommand EndOutputCompareViewerCommand => viewerWorkspaceViewModel.EndOutputCompareViewerCommand;
    public ICommand ShowOutputCompareViewerCommand => viewerWorkspaceViewModel.ShowOutputCompareViewerCommand;
    public ICommand SetViewerRoiScopeCommand => viewerWorkspaceViewModel.SetViewerRoiScopeCommand;
    public ICommand ToggleViewerHelpCommand => viewerWorkspaceViewModel.ToggleViewerHelpCommand;
    public string ViewerRoiScope => viewerWorkspaceViewModel.ViewerRoiScope;
    public bool IsViewerRoiAll => viewerWorkspaceViewModel.IsViewerRoiAll;
    public bool IsViewerRoiSelected => viewerWorkspaceViewModel.IsViewerRoiSelected;
    public bool IsViewerRoiHidden => viewerWorkspaceViewModel.IsViewerRoiHidden;
    public bool IsViewerHelpOpen { get => viewerWorkspaceViewModel.IsViewerHelpOpen; set => viewerWorkspaceViewModel.IsViewerHelpOpen = value; }
    public IReadOnlyList<string>? ViewerDisplayedRoiIds => ViewerRoiScope switch
    {
        "none" => [],
        "selected" => SelectedPipelineStep?.InputEntityIds ?? [],
        _ => null
    };
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
    public event EventHandler? OutputCompareViewerRequested
    {
        add => viewerWorkspaceViewModel.OutputCompareViewerRequested += value;
        remove => viewerWorkspaceViewModel.OutputCompareViewerRequested -= value;
    }
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
    public bool IsHeightImageOnlyViewerLayout => viewerWorkspaceViewModel.IsHeightImageOnlyViewerLayout;
    public bool IsSplitVerticalViewerLayout => viewerWorkspaceViewModel.IsSplitVerticalViewerLayout;
    public bool IsSplitHorizontalViewerLayout => viewerWorkspaceViewModel.IsSplitHorizontalViewerLayout;
    public bool IsFourViewerLayout => viewerWorkspaceViewModel.IsFourViewerLayout;
    public bool IsPopOutViewerLayout => viewerWorkspaceViewModel.IsPopOutViewerLayout;
    public string AuxiliaryViewerSummary => viewerWorkspaceViewModel.AuxiliaryViewerSummary;
    public string MainViewerSummary => viewerWorkspaceViewModel.MainViewerSummary;
    public bool IsInspectionInputView => viewerWorkspaceViewModel.IsInspectionInputView;
    public bool IsInspectionOutputView => viewerWorkspaceViewModel.IsInspectionOutputView;
    public bool IsInspectionInputOutputView => viewerWorkspaceViewModel.IsInspectionInputOutputView;
    public bool IsOutputCompareViewerActive => viewerWorkspaceViewModel.IsOutputCompareViewerActive;
    public bool CanShowOutputCompareViewer => viewerWorkspaceViewModel.CanShowOutputCompareViewer;
    public string MainViewerSlotLabel => viewerWorkspaceViewModel.MainViewerSlotLabel;
    public string AuxiliaryViewerSlotLabel => viewerWorkspaceViewModel.AuxiliaryViewerSlotLabel;
    public bool HasInspectionStep => viewerWorkspaceViewModel.HasInspectionStep;
    public bool CanEditMainViewerPin => viewerWorkspaceViewModel.CanEditMainViewerPin;
    public string MainViewerDisplayedContentId { get => viewerWorkspaceViewModel.MainViewerDisplayedContentId; set => viewerWorkspaceViewModel.MainViewerDisplayedContentId = value; }
    public string MaximizedViewerSlotId => viewerWorkspaceViewModel.MaximizedViewerSlotId;
    public ICommand ToggleViewerPaneMaximizedCommand => viewerWorkspaceViewModel.ToggleViewerPaneMaximizedCommand;
    public bool CanEditAuxiliaryViewerPin => viewerWorkspaceViewModel.CanEditAuxiliaryViewerPin;
    public bool HasEffectiveMainGeometry => viewerWorkspaceViewModel.HasEffectiveMainGeometry;
    public bool HasEffectiveOutputGeometry => viewerWorkspaceViewModel.HasEffectiveOutputGeometry;
    public bool HasEffectiveSignedDeltaOutput => viewerWorkspaceViewModel.HasEffectiveSignedDeltaOutput;
    public ViewerWorkspaceLayout EffectiveViewerLayout => viewerWorkspaceViewModel.EffectiveViewerLayout;
    public string EffectiveMainViewerContentId => viewerWorkspaceViewModel.EffectiveMainViewerContentId;
    public string EffectiveAuxiliaryViewerContentId => viewerWorkspaceViewModel.EffectiveAuxiliaryViewerContentId;
    public string EffectiveMainViewerSummary => viewerWorkspaceViewModel.EffectiveMainViewerSummary;
    public string EffectiveAuxiliaryViewerSummary => viewerWorkspaceViewModel.EffectiveAuxiliaryViewerSummary;

    public ViewerWorkspaceCandidateItem? GetViewerWorkspaceCandidate(string? contentId) =>
        viewerWorkspaceViewModel.GetViewerWorkspaceCandidate(contentId);

    public ViewerWorkspaceCandidateItem? GetMainViewerCandidate(string? contentId) =>
        viewerWorkspaceViewModel.GetMainViewerCandidate(contentId);

    internal OpenVisionLab.ThreeD.Data.HeightMeasurementColorSnapshot? GetDisplayedMeasurementColor()
    {
        if (SelectedPipelineStep is not { } step) return null;
        if (IsInspectionFileView)
            return InspectionQueue.SelectedSample?.MeasurementColors.GetValueOrDefault(step.Id);
        if (IsValidationSampleView)
            return SelectedValidationSetSample?.Steps.FirstOrDefault(result => result.StepId == step.Id)?.ColorSnapshot;
        return HasCurrentMeasurementPreview ? CurrentMeasurementOutput?.ColorSnapshot : null;
    }

    public Task EnsureHeightImageSourceAsync()
    {
        if (IsReadOnlySampleView)
        {
            var samplePath = IsValidationSampleView ? SelectedValidationSetSample?.SourcePath : InspectionQueue.SelectedSample?.SourcePath;
            if (string.IsNullOrWhiteSpace(samplePath))
            {
                HeightImageViewer.ClearSource();
                return Task.CompletedTask;
            }
            return HeightImageViewer.EnsureSourceAsync(samplePath, Source.Id, Source.Unit, Source.FrameId);
        }
        return HeightImageViewer.EnsureSourceAsync(
            Source.Path,
            Source.Id,
            Source.Unit,
            Source.FrameId,
            GetOrLoadDecodedC3DSourceAsync);
    }

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

    private Task EnsurePaneHeightImageSourceAsync(HeightImageViewerViewModel image, ViewerWorkspaceCandidateItem candidate)
    {
        if (ReferenceEquals(image, HeightImageViewer)) return EnsureHeightImageSourceAsync();
        var displayedInputPath = IsReadOnlySampleView
            ? IsValidationSampleView ? SelectedValidationSetSample?.SourcePath : InspectionQueue.SelectedSample?.SourcePath
            : Source.Path;
        if (ReferenceEquals(image.RoiWorkspace, HeightImageViewer.RoiWorkspace)
            && string.Equals(candidate.SourcePath, displayedInputPath, StringComparison.OrdinalIgnoreCase))
        {
            return image.EnsureSharedSourceAsync(candidate.SourcePath, async () =>
            {
                await EnsureHeightImageSourceAsync();
                return (HeightImageViewer.Frame, HeightImageViewer.Error);
            });
        }
        return string.Equals(candidate.SourcePath, Source.Path, StringComparison.OrdinalIgnoreCase)
            ? image.EnsureSourceAsync(candidate.SourcePath, Source.Id, Source.Unit, Source.FrameId, GetOrLoadDecodedC3DSourceAsync)
            : image.EnsureSourceAsync(candidate.SourcePath, candidate.Id, Source.Unit, Source.FrameId);
    }

    private void InitializeViewerWorkspace()
    {
        viewerWorkspaceViewModel = new ViewerWorkspaceViewModel(
            ViewerWorkspace,
            WorkspaceSelection,
            Localization,
            () => (IsSourceReadyForRecipe, Source.Path),
            () => RenderableC3DCatalog,
            () => (CompareSlotAArtifactId, CompareSlotBArtifactId, CompareSlotCArtifactId),
            GetRenderableSignedDeltaArtifact,
            output => HeightImageViewer.ShowSignedDeltaArtifactIfChanged(output),
            () => HeightImageViewer.ClearSignedDeltaArtifact(),
            () => (IsReadOnlySampleView, IsValidationSampleView ? SelectedValidationSetSample?.SourcePath : InspectionQueue.SelectedSample?.SourcePath),
            id => renderableC3DCatalogOwner.GetInputBackgroundTarget(id, ArtifactRegistry)?.Id,
            BeginHeightImageSourceLoad,
            HeightImageViewer,
            SharedHeightCursor,
            EnsurePaneHeightImageSourceAsync);
        viewerWorkspaceEventCoordinator = new ToolWorkbenchViewerWorkspaceEventCoordinator(
            viewerWorkspaceViewModel,
            args =>
            {
                OnPropertyChanged(args.PropertyName);
                if (displayedOutputsOwner is not null && args.PropertyName is nameof(EffectiveMainViewerContentId)
                    or nameof(EffectiveAuxiliaryViewerContentId) or nameof(EffectiveViewerLayout))
                    displayedOutputsOwner.RefreshPresentation(refreshWorkspace: false);
                if (args.PropertyName == nameof(ViewerRoiScope)) RefreshHeightImageRoiProjection();
            });
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
