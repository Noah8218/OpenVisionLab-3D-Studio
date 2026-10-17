using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Workbench facade for recipe authoring and explicit Preview/Publish orchestration.
/// Deterministic algorithms remain owned by the Tools execution adapters; this
/// ViewModel composes teach-time state, execution owners, and evidence routing.
/// </summary>
public sealed partial class ToolWorkbenchViewModel : INotifyPropertyChanged, IDisposable
{
    internal const int MaximumRunLogEntries = ToolWorkbenchRunLogOwner.MaximumEntries;

    private enum FlowConnectionFeedback
    {
        Idle,
        SelectInput,
        Saved,
        AlreadyConnected,
        Rejected,
        Cancelled
    }

    private bool isRecipeSaving;
    private string recipeSaveFailure = string.Empty;
    private readonly ToolWorkbenchRunLogOwner runLogOwner;
    private readonly ToolWorkbenchFilterExecutionOwner filterExecutionOwner;
    private readonly ToolWorkbenchHeightThresholdExecutionOwner heightThresholdExecutionOwner;
    private readonly ToolWorkbenchSignedDeltaExecutionOwner signedDeltaBackgroundExecutionOwner;
    private readonly ToolWorkbenchSelectedStepExecutionOwner selectedStepExecutionOwner;
    private readonly ToolWorkbenchRemoveOutlierExecutionOwner removeOutlierExecutionOwner;
    private readonly ToolWorkbenchConnectedRegionExecutionOwner connectedRegionExecutionOwner;
    private readonly ToolWorkbenchGridPolygonDomainExecutionOwner gridPolygonDomainExecutionOwner;
    private readonly ToolWorkbenchDomainMaskExecutionOwner domainMaskExecutionOwner;
    private readonly ToolWorkbenchEditableRegionExecutionOwner editableRegionExecutionOwner;
    private readonly ToolWorkbenchLevelSurfaceExecutionOwner levelSurfaceExecutionOwner;
    private readonly ToolWorkbenchRoiCropExecutionOwner roiCropExecutionOwner;
    private readonly ToolWorkbenchTwoPointLineExecutionOwner twoPointLineExecutionOwner;
    private readonly ToolWorkbenchHeightDifferenceEdgeExecutionOwner heightDifferenceEdgeExecutionOwner;
    private readonly ToolWorkbenchLineFitExecutionOwner lineFitExecutionOwner;
    private readonly ToolWorkbenchPhysicalEdgeGapFlushExecutionOwner physicalEdgeGapFlushExecutionOwner;
    private readonly ToolWorkbenchLineIntersectionExecutionOwner lineIntersectionExecutionOwner;
    private readonly ToolWorkbenchLandmarkCorrespondenceExecutionOwner landmarkCorrespondenceExecutionOwner;
    private readonly ToolWorkbenchThreePointPlaneExecutionOwner threePointPlaneExecutionOwner;
    private readonly ToolWorkbenchDatumPlaneDeviationExecutionOwner datumPlaneDeviationExecutionOwner;
    private readonly ToolWorkbenchXyzAffineExecutionOwner xyzAffineExecutionOwner;
    private readonly ToolWorkbenchRegridHeightFieldExecutionOwner regridHeightFieldExecutionOwner;
    private readonly ToolWorkbenchHeightMeasurementExecutionOwner heightMeasurementExecutionOwner;
    private readonly ToolWorkbenchSourceLoadOwner sourceLoadOwner;
    private readonly ToolWorkbenchLocalizationSubscriptionOwner localizationSubscriptionOwner;
    private readonly ToolWorkbenchOrderedRunExecutionOwner orderedRunExecutionOwner;
    private InspectionQueueWorkspaceViewModel? inspectionQueueWorkspace;
    private readonly ToolWorkbenchTeachingSelectionStoreOwner teachingSelectionStoreOwner;
    private readonly ToolWorkbenchTeachingSelectionCaptureOwner teachingSelectionCaptureOwner;
    private readonly ToolWorkbenchLandmarkCorrespondenceEditorOwner
        landmarkCorrespondenceEditorOwner;
    private readonly ToolWorkbenchReferenceCatalogOwner referenceCatalogOwner;
    private readonly ToolWorkbenchRecipePartEventCoordinator recipePartEventCoordinator;
    private readonly ToolWorkbenchEditorSessionEventCoordinator editorSessionEventCoordinator;
    private ToolWorkbenchTeachingSelectionEventCoordinator teachingSelectionEventCoordinator = null!;
    private ToolWorkbenchReferenceCatalogEventCoordinator referenceCatalogEventCoordinator = null!;
    private readonly ToolWorkbenchArtifactNavigatorEventCoordinator artifactNavigatorEventCoordinator;
    private ToolWorkbenchCompatibleToolCatalogEventCoordinator compatibleToolCatalogEventCoordinator = null!;
    private ToolWorkbenchCompletenessReviewEventCoordinator completenessReviewEventCoordinator = null!;
    private ToolWorkbenchDisplayedOutputsEventCoordinator displayedOutputsEventCoordinator = null!;
    private ToolWorkbenchPreparationPresetEventCoordinator preparationPresetEventCoordinator = null!;
    private ToolWorkbenchSurfaceMatchCollectionEventCoordinator surfaceMatchCollectionEventCoordinator = null!;
    private ToolWorkbenchSourceQualityEventCoordinator sourceQualityEventCoordinator = null!;
    private ToolWorkbenchThicknessRepeatGridEventCoordinator thicknessRepeatGridEventCoordinator = null!;
    private ToolWorkbenchViewerWorkspaceEventCoordinator viewerWorkspaceEventCoordinator = null!;
    private ViewerWorkspaceViewModel viewerWorkspaceViewModel = null!;
    private ToolWorkbenchFlowDiagnosticsEventCoordinator flowDiagnosticsEventCoordinator = null!;
    private readonly ToolWorkbenchFirstRecipeEventCoordinator firstRecipeEventCoordinator;
    private readonly ToolWorkbenchOutputCompareEventCoordinator outputCompareEventCoordinator;
    private readonly ToolWorkbenchHeightImageRoiCoordinator heightImageRoiCoordinator;
    private readonly ToolWorkbenchOrientedBoxEventCoordinator orientedBoxEventCoordinator;
    private ToolWorkbenchLifetimeCoordinator? lifetimeCoordinator;
    private readonly RelayCommand addSelectedToolCommand;
    private readonly RelayCommand removeSelectedStepCommand;
    private readonly RelayCommand restoreLastRemovedStepCommand;
    private readonly RelayCommand moveSelectedStepUpCommand;
    private readonly RelayCommand moveSelectedStepDownCommand;
    private readonly RelayCommand selectPipelineStepCommand;
    private readonly RelayCommand beginFlowConnectionCommand;
    private readonly RelayCommand connectFlowOutputCommand;
    private readonly RelayCommand cancelFlowConnectionCommand;
    private readonly RelayCommand openSelectedToolLabCommand;
    private readonly RelayCommand openRecipeInfoEditorCommand;
    private readonly RelayCommand applyRecipeInfoCommand;
    private readonly RelayCommand cancelRecipeInfoEditorCommand;
    private string toolSearchText = string.Empty;
    private ToolWorkbenchToolItem? selectedTool;
    private ToolWorkbenchPipelineStepItem? selectedPipelineStep;
    private double lastToolSelectionMilliseconds;
    private double lastStepSelectionMilliseconds;
    private double lastToolAddMilliseconds;
    private double lastRecipeValidationMilliseconds;
    private double lastRecipeEntityRebuildMilliseconds;
    private double lastRecipeExecutionStateMilliseconds;
    private double lastRecipeNotificationMilliseconds;
    private double lastRecipeRefreshMilliseconds;
    private bool suppressRecipeRefresh;
    private bool deferSelectedStepStateRefresh;
    private int workspaceProjectionUpdateDepth;
    private bool workspaceProjectionPending;
    private bool isRecipeInfoEditorVisible;
    private string recipeInfoCategoryDraft = string.Empty;
    private string recipeInfoInspectionTargetDraft = string.Empty;
    private string recipeInfoDescriptionDraft = string.Empty;
    private string recipeInfoEditError = string.Empty;
    private string? pendingFlowOutputEntityId;
    private FlowConnectionFeedback flowConnectionFeedback;
    private int selectedReviewTabIndex;
    private (ToolRecipeStep Step, ToolWorkbenchToolItem Tool, int Index, ToolRecipeSelection[] Selections)? lastRemovedStep;

    public ToolWorkbenchViewModel(string? recentRecipesPath = null)
    {
        runLogOwner = new ToolWorkbenchRunLogOwner();
        sourceQualityUiDispatcher =
            System.Threading.SynchronizationContext.Current
                is System.Windows.Threading.DispatcherSynchronizationContext
                    ? System.Windows.Threading.Dispatcher.FromThread(
                        System.Threading.Thread.CurrentThread)
                    : null;
        sourceLoadOwner = new ToolWorkbenchSourceLoadOwner(
            Localization,
            () => IsRecipeMutationBlocked,
            propertyName =>
            {
                var viewModelPropertyName = propertyName switch
                {
                    nameof(ToolWorkbenchSourceLoadOwner.IsLoading) => nameof(IsC3DSourceLoading),
                    nameof(ToolWorkbenchSourceLoadOwner.ProgressPercent) => nameof(C3DSourceLoadProgressPercent),
                    nameof(ToolWorkbenchSourceLoadOwner.LoadStatus) => nameof(C3DSourceLoadStatus),
                    _ => propertyName
                };
                OnPropertyChanged(viewModelPropertyName);
                if (propertyName == nameof(ToolWorkbenchSourceLoadOwner.IsLoading))
                {
                    inspectionQueueWorkspace?.RefreshTeachingInputCommand();
                    (RunTeachingRecipeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            },
            (category, message) => AppendLog(category, message));
        surfaceMatchExperiment = new SurfaceMatchExperimentSession(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "surface-match",
                StringComparison.Ordinal),
            () => HasPendingStepParameterChanges,
            () => SelectedPipelineStep,
            RaisePublishedSurfaceMatchProperties,
            RaiseSurfaceMatchExperimentDisplay,
            (category, message) => AppendLog(category, message),
            RefreshSurfaceMatchExperimentState);
        WorkspaceSelection = new InspectionWorkspaceSelectionSession();
        ViewerWorkspace = new ViewerWorkspaceSession();
        SharedHeightCursor = new SharedHeightCursorSession();
        HeightImageViewer = new HeightImageViewerViewModel(
            ThreeDLocalization.Shared,
            SharedHeightCursor);
        InitializeViewerWorkspace();
        RecipePreviewViewer = new HeightImageViewerViewModel(
            ThreeDLocalization.Shared,
            new SharedHeightCursorSession());
        ThicknessRepeatGrid = new ThicknessRepeatGridAuthoringSession();
        SelectedToolWorkspace = new SelectedToolWorkspaceViewModel(WorkspaceSelection, EnsureSelectedToolPreviewAsync, IsFlowOutputAvailable, IsFlowInputCompatible);
        editorSessionEventCoordinator = new ToolWorkbenchEditorSessionEventCoordinator(
            WorkspaceSelection,
            OnInspectionWorkspaceSelectionChanged,
            stepPropertySession,
            OnStepPropertySessionChanged);
        InitializeInspectionWorkspace();
        InitializeCompletenessReview();
        InitializeThicknessRepeatGrid();
        this.recentRecipesPath = recentRecipesPath ?? Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            $"Session-{Environment.ProcessId}",
            "recent-recipes.json");
        Tools = new(ToolWorkbenchToolCatalog.Create());
        RefreshFilteredTools();

        Source = new ToolWorkbenchSourceItem(
            "source.c3d.height-map",
            "No C3D source selected",
            "C3D",
            "raw-height",
            "frame.c3d-grid-index",
            string.Empty);
        Source.MeasurementEvidence = HeightMeasurementEvidence.RawHeight();
        recipePartEventCoordinator = new ToolWorkbenchRecipePartEventCoordinator(
            Source,
            OnRecipePartChanged);
        referenceCatalogOwner = new ToolWorkbenchReferenceCatalogOwner(NormalizeId);
        referenceCatalogEventCoordinator = new ToolWorkbenchReferenceCatalogEventCoordinator(
            referenceCatalogOwner,
            OnReferenceCatalogPropertyChanged,
            OnRecipePartChanged,
            OnReferenceCatalogMutated);
        teachingSelectionStoreOwner = new ToolWorkbenchTeachingSelectionStoreOwner(
            () => Source.Id,
            () => Source.FrameId,
            () => SourceSession.SourceBinding,
            GetPublishedSelectionBindingState,
            () => SelectedStepTeachingSelection,
            () => IsSelectedStepViewerCaptureSupported,
            _ => RequestSelectedTeachingSelectionRemoval(),
            UseExistingTeachingSelection,
            () => IsRecipeMutationBlocked);
        orderedRunExecutionOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            CreateDocument,
            document =>
            {
                var canRun = TryGetOrderedRunCapability(document, out var message);
                return (canRun, message);
            },
            () => RecipePath,
            () => Source.Path,
            () => SourceQuality.Report,
            () => PipelineSteps,
            CreateOrderedRunSummary,
            Localize,
            (category, message) => AppendLog(category, message),
            NotifyOrderedRunState,
            args => OrderedRunCompleted?.Invoke(this, args),
            () => OrderedRunInvalidated?.Invoke(this, EventArgs.Empty),
            (document, sourcePath) => TryGetInspectionInputCapability(document, sourcePath),
            () => RecipeSession.SavedSha256,
            runLogOwner.AppendExecution);
        filterExecutionOwner = new ToolWorkbenchFilterExecutionOwner(
            CanShowFilterSource,
            ShowFilterSource,
            () => SetFilterKernel(3),
            () => CanSetFilterKernel(3),
            () => SetFilterKernel(5),
            () => CanSetFilterKernel(5),
            () => SetFilterKernel(7),
            () => CanSetFilterKernel(7),
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "filter",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            () => IsEdgePreviewRunning,
            CreateDocument,
            () => RecipePath,
            (category, message) => AppendLog(category, message),
            args => FilterDisplayRequested?.Invoke(this, args),
            summary => MarkHeightDifferenceEdgePreviewStale(summary),
            RefreshFilterStateFromOwner);
        heightThresholdExecutionOwner = new ToolWorkbenchHeightThresholdExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "height-threshold-background-removal",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            CreateDocument,
            () => RecipePath,
            sender => ReferenceEquals(sender, Source),
            (category, message) => AppendLog(category, message),
            args => FilterDisplayRequested?.Invoke(this, args),
            RefreshHeightThresholdStateFromOwner);
        signedDeltaBackgroundExecutionOwner = new ToolWorkbenchSignedDeltaExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                C3DSignedHeightDeltaRecipePolicy.ToolId,
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            stepId => PipelineSteps.FirstOrDefault(step =>
                string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase)),
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            CreateDocument,
            () => RecipePath,
            sender => ReferenceEquals(sender, Source),
            (category, message) => AppendLog(category, message),
            RefreshSignedDeltaBackgroundSubtractionStateFromOwner);
        removeOutlierExecutionOwner = new ToolWorkbenchRemoveOutlierExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "remove-outlier-pixels",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            CreateDocument,
            () => RecipePath,
            sender => ReferenceEquals(sender, Source),
            (category, message) => AppendLog(category, message),
            args => FilterDisplayRequested?.Invoke(this, args),
            RefreshRemoveOutlierStateFromOwner);
        connectedRegionExecutionOwner = new ToolWorkbenchConnectedRegionExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "connected-region",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            CreateDocument,
            () => RecipePath,
            entityId => removeOutlierExecutionOwner.TryGetPublishedInput(entityId),
            sender => ReferenceEquals(sender, Source),
            (category, message) => AppendLog(category, message),
            RefreshConnectedRegionStateFromOwner);
        gridPolygonDomainExecutionOwner = new ToolWorkbenchGridPolygonDomainExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "grid-polygon-domain",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            CreateDocument,
            () => RecipePath,
            TryGetCurrentPublishedHeightField,
            sender => ReferenceEquals(sender, Source),
            (category, message) => AppendLog(category, message),
            RefreshGridPolygonDomainStateFromOwner);
        domainMaskExecutionOwner = new ToolWorkbenchDomainMaskExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "domain-mask",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            CreateDocument,
            () => RecipePath,
            TryGetCurrentPublishedHeightField,
            entityId => (object?)connectedRegionExecutionOwner.TryGetPublishedArtifact(entityId)
                ?? gridPolygonDomainExecutionOwner.TryGetPublishedArtifact(entityId),
            sender => ReferenceEquals(sender, Source),
            (category, message) => AppendLog(category, message),
            args => FilterDisplayRequested?.Invoke(this, args),
            RefreshDomainMaskStateFromOwner);
        editableRegionExecutionOwner = new ToolWorkbenchEditableRegionExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "editable-region",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            CreateDocument,
            () => RecipePath,
            entityId => connectedRegionExecutionOwner.TryGetPublishedArtifact(entityId),
            (category, message) => AppendLog(category, message),
            RefreshEditableRegionStateFromOwner);
        levelSurfaceExecutionOwner = new ToolWorkbenchLevelSurfaceExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "level-surface",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            CreateDocument,
            () => RecipePath,
            sender => ReferenceEquals(sender, Source),
            (category, message) => AppendLog(category, message),
            args => FilterDisplayRequested?.Invoke(this, args),
            RefreshLevelSurfaceStateFromOwner);
        roiCropExecutionOwner = new ToolWorkbenchRoiCropExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "roi-crop",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            CreateDocument,
            () => RecipePath,
            sender => ReferenceEquals(sender, Source),
            (category, message) => AppendLog(category, message),
            args => FilterDisplayRequested?.Invoke(this, args),
            RefreshRoiCropStateFromOwner);
        twoPointLineExecutionOwner = new ToolWorkbenchTwoPointLineExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "two-point-line",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            () => SelectedStepTeachingSelection,
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            () => RecipePath,
            (category, message) => AppendLog(category, message),
            args => TwoPointLineDisplayRequested?.Invoke(this, args),
            () => TwoPointLineDisplayCleared?.Invoke(this, EventArgs.Empty),
            RefreshLineIntersectionExecutionState,
            () => MarkLineIntersectionPreviewStaleIfNeeded(),
            () => ClearLineIntersectionPreview(
                "Upstream LineFeature was cleared. Line Intersection Preview was cleared without execution."),
            RefreshTwoPointLineStateFromOwner);
        heightDifferenceEdgeExecutionOwner = new ToolWorkbenchHeightDifferenceEdgeExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "height-difference-edge",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            () => isFilterPreviewRunning,
            () => filterExecutionOwner.GetPublishedOutput(SelectedPipelineStep?.InputEntityIds.FirstOrDefault())?.Output,
            () => filterExecutionOwner.GetPublishedOutput(SelectedPipelineStep?.InputEntityIds.FirstOrDefault())?.Path,
            () => filterExecutionOwner.GetPublishedOutput(SelectedPipelineStep?.InputEntityIds.FirstOrDefault()) is null,
            () => filterExecutionOwner.GetPublishedOutput(SelectedPipelineStep?.InputEntityIds.FirstOrDefault()) is not null,
            () => SelectedStepTeachingSelection,
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            (category, message) => AppendLog(category, message),
            args => HeightDifferenceEdgeDisplayRequested?.Invoke(this, args),
            RefreshLineFitExecutionState,
            () => MarkLineFitPreviewStaleIfNeeded(),
            () => ClearLineFitPreview(
                "Upstream EdgePointSet was cleared. Line Fit Preview was cleared without execution."),
            RefreshHeightDifferenceEdgeStateFromOwner);
        lineFitExecutionOwner = new ToolWorkbenchLineFitExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "three-d-line-fit",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            () => IsEdgePreviewRunning,
            outputEntityId => TryGetPublishedHeightDifferenceEdgeOutput(
                outputEntityId,
                out var edge)
                ? edge
                : null,
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            (category, message) => AppendLog(category, message),
            args => LineFitDisplayRequested?.Invoke(this, args),
            () => LineFitDisplayCleared?.Invoke(this, EventArgs.Empty),
            RefreshLineIntersectionExecutionState,
            () => MarkLineIntersectionPreviewStaleIfNeeded(),
            () => ClearLineIntersectionPreview(
                "Upstream LineFeature was cleared. Line Intersection Preview was cleared without execution."),
            RefreshLineFitStateFromOwner,
            RefreshLineFitDiagnosticStateFromOwner);
        physicalEdgeGapFlushExecutionOwner = new ToolWorkbenchPhysicalEdgeGapFlushExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "physical-edge-gap-flush",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            outputEntityId => TryGetPublishedLineFitOutput(
                outputEntityId,
                out var line)
                ? line
                : null,
            outputEntityId => TryGetPublishedHeightDifferenceEdgeOutput(
                outputEntityId,
                out var edge)
                ? edge
                : null,
            CreateDocument,
            (category, message) => AppendLog(category, message),
            RefreshPhysicalEdgeGapFlushStateFromOwner);
        lineIntersectionExecutionOwner = new ToolWorkbenchLineIntersectionExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "line-intersection",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            outputEntityId => TryGetPublishedLineGeometry(outputEntityId, out var line)
                ? line
                : null,
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            (category, message) => AppendLog(category, message),
            args => LineIntersectionDisplayRequested?.Invoke(this, args),
            () => LineIntersectionDisplayCleared?.Invoke(this, EventArgs.Empty),
            RefreshLandmarkCorrespondenceExecutionState,
            RefreshLineIntersectionStateFromOwner);
        landmarkCorrespondenceExecutionOwner = new ToolWorkbenchLandmarkCorrespondenceExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "landmark-correspondence",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            () => Source.Id,
            () => Source.Unit,
            () => Source.FrameId,
            () => SourceSession.SourceBinding,
            selectionId => Selections.FirstOrDefault(item => string.Equals(
                item.Id,
                selectionId,
                StringComparison.OrdinalIgnoreCase)),
            outputEntityId => TryGetPublishedLineIntersectionOutput(
                outputEntityId,
                out var anchor)
                ? anchor
                : null,
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            (category, message) => AppendLog(category, message),
            args => LandmarkCorrespondenceDisplayRequested?.Invoke(this, args),
            () => LandmarkCorrespondenceDisplayCleared?.Invoke(this, EventArgs.Empty),
            () => MarkAffineSolvePreviewStaleIfNeeded(),
            ClearXYZAffineSolvePreview,
            RefreshXyzAffineStateFromOwner,
            RefreshLandmarkCorrespondenceStateFromOwner);
        threePointPlaneExecutionOwner = new ToolWorkbenchThreePointPlaneExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "three-point-plane",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            () => SelectedStepTeachingSelection,
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            () => RecipePath,
            (category, message) => AppendLog(category, message),
            args => ThreePointPlaneDisplayRequested?.Invoke(this, args),
            () => ThreePointPlaneDisplayCleared?.Invoke(this, EventArgs.Empty),
            outputEntityId => MarkDatumPlaneDeviationPreviewStaleIfNeeded(
                upstreamPlaneOutputId: outputEntityId),
            () => ClearDatumPlaneDeviationPreview(
                "Published 3-Point Plane source cleared. Datum-plane residual preview is unavailable until a new plane is Published."),
            RefreshThreePointPlaneStateFromOwner);
        datumPlaneDeviationExecutionOwner = new ToolWorkbenchDatumPlaneDeviationExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "datum-plane-raw-height-deviation",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            outputEntityId => TryGetPublishedThreePointPlaneOutput(outputEntityId, out var plane)
                ? plane
                : null,
            selectionId => Selections.FirstOrDefault(item => string.Equals(
                item.Id,
                selectionId,
                StringComparison.OrdinalIgnoreCase)),
            IsSelectionCurrent,
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            () => RecipePath,
            (category, message) => AppendLog(category, message),
            args => DatumPlaneDeviationDisplayRequested?.Invoke(this, args),
            () => DatumPlaneDeviationDisplayCleared?.Invoke(this, EventArgs.Empty),
            RefreshDatumPlaneDeviationStateFromOwner);
        xyzAffineExecutionOwner = new ToolWorkbenchXyzAffineExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "xyz-affine-solve",
                StringComparison.Ordinal),
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "xyz-affine-apply",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => IsSourceReadyForRecipe,
            () => HasPendingStepParameterChanges,
            () => Source.Id,
            () => SourceSession.SourceBinding,
            outputEntityId => TryGetPublishedLandmarkCorrespondenceOutput(
                outputEntityId,
                out var correspondence)
                ? correspondence
                : null,
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            (category, message) => AppendLog(category, message),
            ClearRegridHeightFieldPreview,
            RefreshRegridHeightFieldExecutionState,
            RefreshXyzAffineStateFromOwner);
        regridHeightFieldExecutionOwner = new ToolWorkbenchRegridHeightFieldExecutionOwner(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "re-grid-height-map",
                StringComparison.Ordinal),
            () => SelectedPipelineStep,
            () => HasPendingStepParameterChanges,
            outputEntityId => TryGetPublishedAffineApplyOutput(outputEntityId, out var cloud)
                ? cloud
                : null,
            CreateDocument,
            (category, message) => AppendLog(category, message),
            teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged,
            RefreshRegridHeightFieldStateFromOwner);
        heightMeasurementExecutionOwner = new ToolWorkbenchHeightMeasurementExecutionOwner(
            () => IsSelectedStepMeasurement,
            () => SelectedPipelineStep,
            () => HasPendingStepParameterChanges,
            () => RecipePath,
            () => Source.Id,
            outputEntityId => TryGetPublishedRoiCropOutput(
                outputEntityId,
                out var croppedOutput)
                ? croppedOutput
                : null,
            outputEntityId => TryGetPublishedRegridHeightFieldOutput(
                outputEntityId,
                out var output)
                ? output
                : null,
            entityId => editableRegionExecutionOwner.TryGetPublishedArtifact(entityId),
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            (category, message) => AppendLog(category, message),
            UpdateMeasurementCompletenessPresentation,
            RefreshHeightMeasurementStateFromOwner,
            entityId => TryGetCurrentPublishedHeightField(entityId),
            entityId => connectedRegionExecutionOwner.TryGetPublishedArtifact(entityId));
        selectedStepExecutionOwner = new ToolWorkbenchSelectedStepExecutionOwner(
            () => SelectedPipelineStep,
            CreateSelectedStepExecutionRoutes(),
            AppendLog,
            step => CreateDocument().CreateStepExecutionContext(step.Id),
            () => { OnPropertyChanged(nameof(IsSelectedStepPreviewRunning)); RefreshSelectedToolWorkspaceProjection(); },
            () => !IsRecipeMutationBlocked && !IsReadOnlySampleView && !IsTeachingSelectionCaptureActive,
            CaptureSelectedToolOutputEvidence,
            RecordSelectedExecution);
        InitializeSourceQualityWorkspace();
        OrientedBoxEditor = new OrientedBox3DEditorViewModel();
        orientedBoxEventCoordinator = CreateOrientedBoxEventCoordinator();
        teachingSelectionCaptureOwner = new ToolWorkbenchTeachingSelectionCaptureOwner(
            TeachingCaptureSession,
            CreateTeachingCaptureContext,
            () => SelectedStepSelectionRequirement,
            () => SelectedStepTeachingSelection,
            () => SelectedStepTeachingSelection?.SourceBinding
                  ?? SourceSession.SourceBinding,
            () => IsSelectedStepCrossSectionDimensions,
            PersistSelectionForSelectedStep,
            AdvancePlaneFlatnessTeachingRole,
            teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged,
            AppendLog,
            () => OrientedBoxEditor.IsDraftOpen,
            () => OrientedBoxEditor.ApplyCommand.CanExecute(null),
            () => OrientedBoxEditor.ApplyCommand.Execute(null),
            () => OrientedBoxEditor.CancelCommand.CanExecute(null),
            () => OrientedBoxEditor.CancelCommand.Execute(null));
        landmarkCorrespondenceEditorOwner =
            new ToolWorkbenchLandmarkCorrespondenceEditorOwner(
                CreateLandmarkCorrespondenceEditorContext,
                (step, requirement) => CreateSelectionId(step, requirement),
                PersistSelectionForSelectedStep,
                RemoveTeachingSelection,
                teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged,
                AppendLog);
        teachingSelectionEventCoordinator = new ToolWorkbenchTeachingSelectionEventCoordinator(
            teachingSelectionStoreOwner,
            OnTeachingSelectionStoreOwnerPropertyChanged,
            teachingSelectionCaptureOwner,
            OnTeachingSelectionCaptureOwnerPropertyChanged,
            OnTeachingSelectionCaptureOwnerStateChanged,
            landmarkCorrespondenceEditorOwner,
            OnLandmarkCorrespondenceEditorOwnerPropertyChanged);

        addSelectedToolCommand = new RelayCommand(
            parameter => AddSelectedTool(parameter as ToolWorkbenchToolItem),
            parameter => CanAddTool(parameter as ToolWorkbenchToolItem));
        removeSelectedStepCommand = new RelayCommand(
            _ => RequestSelectedStepRemoval(),
            _ => SelectedPipelineStep is not null && !IsRecipeMutationBlocked);
        restoreLastRemovedStepCommand = new RelayCommand(parameter => TryRestoreLastRemovedStep(out _), _ => CanRestoreLastRemovedStep);
        moveSelectedStepUpCommand = new RelayCommand(_ => MoveSelectedStep(-1), _ => CanMoveSelectedStep(-1));
        moveSelectedStepDownCommand = new RelayCommand(_ => MoveSelectedStep(1), _ => CanMoveSelectedStep(1));
        selectPipelineStepCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is string stepId)
                {
                    SelectPipelineStep(stepId);
                }
            },
            parameter => parameter is string stepId
                         && PipelineSteps.Any(step => string.Equals(step.Id, stepId, StringComparison.Ordinal)));
        beginFlowConnectionCommand = new RelayCommand(BeginFlowConnection, CanBeginFlowConnection);
        connectFlowOutputCommand = new RelayCommand(ConnectFlowOutput, CanConnectFlowOutput);
        cancelFlowConnectionCommand = new RelayCommand(_ => CancelFlowConnection(), _ => HasPendingFlowConnection);

        InitializePropertyGridEditing();
        InitializePreparationPresetAssistant();
        InitializeFirstRecipeUx();
        firstRecipeEventCoordinator = CreateFirstRecipeEventCoordinator();
        InitializePlaneFlatnessTeaching();
        NewTeachingRecipeCommand = new RelayCommand(_ => BeginFirstRecipeSetup());
        AddSelectedToolCommand = addSelectedToolCommand;
        RemoveSelectedStepCommand = removeSelectedStepCommand;
        MoveSelectedStepUpCommand = moveSelectedStepUpCommand;
        MoveSelectedStepDownCommand = moveSelectedStepDownCommand;
        SelectPipelineStepCommand = selectPipelineStepCommand;
        BeginFlowConnectionCommand = beginFlowConnectionCommand;
        ConnectFlowOutputCommand = connectFlowOutputCommand;
        CancelFlowConnectionCommand = cancelFlowConnectionCommand;
        heightImageRoiCoordinator = CreateHeightImageRoiCoordinator();
        InitializeHeightImageRoiProjection();
        InitializeArtifactRegistryAndNavigator();
        artifactNavigatorEventCoordinator = CreateArtifactNavigatorEventCoordinator();
        SelectNavigatorItemCommand = artifactNavigatorOwner.SelectNavigatorItemCommand;
        openSelectedToolLabCommand = artifactNavigatorOwner.OpenSelectedToolLabCommand;
        OpenSelectedToolLabCommand = openSelectedToolLabCommand;
        ValidateTeachingRecipeCommand = new RelayCommand(_ => ValidateTeachingRecipe());
        SaveTeachingRecipeCommand = new RelayCommand(
            _ => SaveTeachingRecipeRequested?.Invoke(this, EventArgs.Empty),
            _ => CanSaveTeachingRecipe);
        SaveTeachingRecipeAsCommand = new RelayCommand(
            _ => SaveTeachingRecipeAsRequested?.Invoke(this, EventArgs.Empty),
            _ => CanSaveTeachingRecipe);
        openRecipeInfoEditorCommand = new RelayCommand(_ => OpenRecipeInfoEditor(), _ => CanOpenRecipeInfoEditor);
        applyRecipeInfoCommand = new RelayCommand(_ => ApplyRecipeInfoEditor());
        cancelRecipeInfoEditorCommand = new RelayCommand(_ => CancelRecipeInfoEditor());
        CancelActiveWorkbenchEditCommand = new RelayCommand(
            _ =>
            {
                if (IsRecipeInfoEditorVisible)
                {
                    CancelRecipeInfoEditor();
                }
                else if (CancelTeachingSelectionCaptureCommand.CanExecute(null))
                {
                    CancelTeachingSelectionCaptureCommand.Execute(null);
                }
            },
            _ => IsRecipeInfoEditorVisible || IsTeachingSelectionCaptureActive);
        OpenToolLibraryCommand = new RelayCommand(_ => OpenToolLibraryRequested?.Invoke(this, EventArgs.Empty));
        OpenTeachingRecipeCommand = new RelayCommand(_ => OpenTeachingRecipeRequested?.Invoke(this, EventArgs.Empty));
        InitializeFilterExecution();
        outputCompareEventCoordinator = CreateOutputCompareEventCoordinator();
        InitializeSurfaceMatchCollectionOwner();
        InitializeDisplayedOutputs();
        InitializeFlowDiagnostics();
        InitializeCompatibleToolCatalog();
        InitializeValidationSet();
        InitializeInspectionQueue();
        localizationSubscriptionOwner = new ToolWorkbenchLocalizationSubscriptionOwner(
            Localization,
            OnCompletenessLocalizationChanged,
            OnPlaneFlatnessLocalizationChanged,
            OnValidationSetLocalizationChanged,
            OnDisplayedOutputsLocalizationChanged,
            OnCompatibleToolCatalogLocalizationChanged,
            OnTeachingLocalizationChanged,
            OnOutputCompareLocalizationChanged,
            OnThicknessRepeatGridLocalizationChanged,
            OnViewerWorkspaceLocalizationChanged,
            OnRecipeBrowserLocalizationChanged,
            OnFirstRecipeLanguageChanged);
        ValidationWorkspace = new RecipePipelineReviewValidationViewModel(this);
        lifetimeCoordinator = new ToolWorkbenchLifetimeCoordinator(
            new IDisposable[]
            {
                surfaceMatchExperiment,
                editorSessionEventCoordinator,
                localizationSubscriptionOwner,
                sourceLoadOwner,
                firstRecipeEventCoordinator,
                artifactNavigatorEventCoordinator,
                validationSetWorkspace,
                flowDiagnosticsEventCoordinator,
                compatibleToolCatalogEventCoordinator,
                completenessReviewEventCoordinator,
                displayedOutputsEventCoordinator,
                preparationPresetEventCoordinator,
                surfaceMatchCollectionEventCoordinator,
                sourceQualityEventCoordinator,
                thicknessRepeatGridEventCoordinator,
                viewerWorkspaceEventCoordinator,
                viewerWorkspaceViewModel,
                referenceCatalogEventCoordinator,
                recipePartEventCoordinator,
                teachingSelectionEventCoordinator,
                firstRecipeSetupOwner,
                flowDiagnosticsOwner,
                inspectionQueueWorkspace!,
                orderedRunExecutionOwner,
                selectedStepExecutionOwner,
                filterExecutionOwner,
                heightThresholdExecutionOwner,
                signedDeltaBackgroundExecutionOwner,
                heightMeasurementExecutionOwner,
                editableRegionExecutionOwner,
                regridHeightFieldExecutionOwner,
                xyzAffineExecutionOwner,
                landmarkCorrespondenceExecutionOwner,
                lineIntersectionExecutionOwner,
                lineFitExecutionOwner,
                physicalEdgeGapFlushExecutionOwner,
                twoPointLineExecutionOwner,
                heightDifferenceEdgeExecutionOwner,
                levelSurfaceExecutionOwner,
                datumPlaneDeviationExecutionOwner,
                threePointPlaneExecutionOwner,
                roiCropExecutionOwner,
                domainMaskExecutionOwner,
                gridPolygonDomainExecutionOwner,
                connectedRegionExecutionOwner,
                removeOutlierExecutionOwner,
                outputCompareEventCoordinator,
                heightImageRoiCoordinator,
                orientedBoxEventCoordinator
            },
            CancelSourceQualityUiNotification,
            new IDisposable[]
            {
                SelectedToolWorkspace,
                HeightImageViewer,
                RecipePreviewViewer,
                SourceQuality,
                SourceSession
            });
        AppendLog("System", "Tool recipe teaching is ready. Source, routing, parameters, and save/reopen are explicit.");
        SelectedTool = Tools[0];
        RefreshRecipeState();
    }

    /// <summary>
    /// Releases Workbench-owned subscriptions, source-viewer load resources,
    /// and active execution resources. The Shell owns this boundary and
    /// invokes it during Window shutdown; repeated calls are safe.
    /// </summary>
    public void Dispose()
    {
        if (lifetimeCoordinator?.IsDisposed == true) return;
        lastRemovedStep = null;
        appliedStepParameters.Clear();
        teachingResultComparisons.Clear();
        if (inspectionQueueWorkspace is not null)
        {
            inspectionQueueWorkspace.PropertyChanged -= OnInspectionQueuePropertyChanged;
        }
        lifetimeCoordinator?.Dispose();
    }

    internal bool IsDisposed => lifetimeCoordinator?.IsDisposed ?? false;

    internal ToolWorkbenchTeachingCaptureSession TeachingCaptureSession { get; } = new();

    internal ToolWorkbenchRecipeSession RecipeSession { get; } = new();
    internal ToolWorkbenchSourceSession SourceSession { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? NewTeachingRecipeRequested;
    public event EventHandler? SaveTeachingRecipeRequested;
    public event EventHandler? SaveTeachingRecipeAsRequested;
    public event EventHandler? OpenToolLibraryRequested;
    public event EventHandler? SelectedStepSetupRequested;
    public event EventHandler? SourceQualityWorkspaceRequested;
    public event EventHandler? OpenTeachingRecipeRequested;
    public event EventHandler? LoadC3DSourceRequested
    {
        add => sourceLoadOwner.LoadC3DSourceRequested += value;
        remove => sourceLoadOwner.LoadC3DSourceRequested -= value;
    }
    public event EventHandler<ToolWorkbenchTeachingCaptureRequestEventArgs>?
        BeginTeachingSelectionCaptureRequested
    {
        add => teachingSelectionCaptureOwner.BeginRequested += value;
        remove => teachingSelectionCaptureOwner.BeginRequested -= value;
    }
    public event EventHandler? UndoTeachingSelectionCaptureRequested
    {
        add => teachingSelectionCaptureOwner.UndoRequested += value;
        remove => teachingSelectionCaptureOwner.UndoRequested -= value;
    }
    public event EventHandler? CancelTeachingSelectionCaptureRequested
    {
        add => teachingSelectionCaptureOwner.CancelRequested += value;
        remove => teachingSelectionCaptureOwner.CancelRequested -= value;
    }
    public event EventHandler? ApplyTeachingSelectionCaptureRequested
    {
        add => teachingSelectionCaptureOwner.ApplyRequested += value;
        remove => teachingSelectionCaptureOwner.ApplyRequested -= value;
    }
    public event EventHandler? AppliedTeachingSelectionsChanged
    {
        add => teachingSelectionStoreOwner.AppliedSelectionsChanged += value;
        remove => teachingSelectionStoreOwner.AppliedSelectionsChanged -= value;
    }
    public event EventHandler<ToolWorkbenchGridRectangleDraftChangedEventArgs>?
        TeachingGridRectangleDraftChanged
    {
        add => teachingSelectionCaptureOwner.GridRectangleDraftChanged += value;
        remove => teachingSelectionCaptureOwner.GridRectangleDraftChanged -= value;
    }
    public event EventHandler<ToolWorkbenchGridCircleDraftChangedEventArgs>?
        TeachingGridCircleDraftChanged
    {
        add => teachingSelectionCaptureOwner.GridCircleDraftChanged += value;
        remove => teachingSelectionCaptureOwner.GridCircleDraftChanged -= value;
    }
    public event EventHandler<ToolWorkbenchGridPolygonDraftChangedEventArgs>?
        TeachingGridPolygonDraftChanged
    {
        add => teachingSelectionCaptureOwner.GridPolygonDraftChanged += value;
        remove => teachingSelectionCaptureOwner.GridPolygonDraftChanged -= value;
    }
    public event EventHandler<ToolWorkbenchToolLabRequestEventArgs>? ToolLabRequested;
    public event EventHandler<ToolWorkbenchStepRemovalRequestEventArgs>? RemoveSelectedStepRequested;
    public event EventHandler<ToolWorkbenchTeachingSelectionRemovalRequestEventArgs>?
        RemoveSelectedTeachingSelectionRequested;

    public ObservableCollection<ToolWorkbenchToolItem> Tools { get; }

    public ResettableObservableCollection<ToolWorkbenchToolItem> FilteredTools { get; } = [];

    public string ToolSearchText
    {
        get => toolSearchText;
        set
        {
            if (SetField(ref toolSearchText, value ?? string.Empty))
            {
                RefreshFilteredTools();
            }
        }
    }

    public ToolWorkbenchSourceItem Source { get; }
    public RecipePipelineReviewValidationViewModel ValidationWorkspace { get; }
    public ToolRecipeAcquisitionProvenance? SourceAcquisitionProvenance =>
        SourceSession.SourceAcquisitionProvenance;
    public SharedHeightCursorSession SharedHeightCursor { get; }
    public HeightImageViewerViewModel HeightImageViewer { get; }
    public HeightImageViewerViewModel RecipePreviewViewer { get; }
    public OrientedBox3DEditorViewModel OrientedBoxEditor { get; }
    public ThreeDLocalization Localization => ThreeDLocalization.Shared;

    public int SelectedReviewTabIndex
    {
        get => selectedReviewTabIndex;
        set => SetField(ref selectedReviewTabIndex, Math.Clamp(value, 0, 5));
    }

    public ObservableCollection<ToolWorkbenchReferenceItem> References =>
        referenceCatalogOwner.References;

    public ResettableObservableCollection<ToolWorkbenchEntityItem> Entities { get; } = [];

    public ResettableObservableCollection<ToolWorkbenchArtifactItem> ArtifactRegistry =>
        artifactNavigatorOwner.ArtifactRegistry;

    public ResettableObservableCollection<ToolWorkbenchNavigatorItem> NavigatorRoots =>
        artifactNavigatorOwner.NavigatorRoots;

    public ObservableCollection<ToolWorkbenchPipelineStepItem> PipelineSteps { get; } = [];

    public ObservableCollection<ToolRecipeSelection> Selections =>
        teachingSelectionStoreOwner.Selections;

    public ObservableCollection<ToolRecipeSelection> AvailableCompatibleSelections =>
        teachingSelectionStoreOwner.AvailableCompatibleSelections;

    public ObservableCollection<ToolRecipeLandmarkCorrespondence> SelectedCorrespondenceRows =>
        landmarkCorrespondenceEditorOwner.Rows;

    public ObservableCollection<ToolWorkbenchGridPolygonVertexItem> TeachingGridPolygonVertices =>
        teachingSelectionCaptureOwner.GridPolygonVertices;

    public ObservableCollection<string> AvailableCorrespondenceSourceEntityIds =>
        landmarkCorrespondenceEditorOwner.AvailableSourceEntityIds;

    public ObservableCollection<ToolWorkbenchValidationItem> ValidationMessages { get; } = [];

    public ObservableCollection<ToolWorkbenchLogItem> RunLog => runLogOwner.Entries;
    public ReadOnlyObservableCollection<ToolWorkbenchExecutionLogItem> ExecutionLog => runLogOwner.ExecutionEntries;

    public ICommand NewTeachingRecipeCommand { get; }
    public ICommand AddSelectedToolCommand { get; }
    public ICommand RemoveSelectedStepCommand { get; }
    public ICommand RestoreLastRemovedStepCommand => restoreLastRemovedStepCommand;
    public bool CanRestoreLastRemovedStep => lastRemovedStep is not null && !IsDisposed && !IsRecipeMutationBlocked && !HasPendingStepParameterChanges;
    public ICommand MoveSelectedStepUpCommand { get; }
    public ICommand MoveSelectedStepDownCommand { get; }
    public ICommand SelectPipelineStepCommand { get; }
    public ICommand BeginFlowConnectionCommand { get; }
    public ICommand ConnectFlowOutputCommand { get; }
    public ICommand CancelFlowConnectionCommand { get; }
    public ICommand AddReferenceCommand => referenceCatalogOwner.AddReferenceCommand;
    public ICommand RemoveSelectedReferenceCommand =>
        referenceCatalogOwner.RemoveSelectedReferenceCommand;
    public ICommand BeginTeachingSelectionCaptureCommand =>
        teachingSelectionCaptureOwner.BeginCommand;
    public ICommand BeginAdditionalLevelSurfaceReferenceCommand =>
        teachingSelectionCaptureOwner.BeginAdditionalLevelSurfaceReferenceCommand;
    public ICommand UndoTeachingSelectionCaptureCommand =>
        teachingSelectionCaptureOwner.UndoCommand;
    public ICommand CancelTeachingSelectionCaptureCommand =>
        teachingSelectionCaptureOwner.CancelCommand;
    public ICommand ApplyTeachingSelectionCaptureCommand =>
        teachingSelectionCaptureOwner.ApplyCommand;
    public ICommand AddTeachingGridPolygonVertexCommand =>
        teachingSelectionCaptureOwner.AddPolygonVertexCommand;
    public ICommand RemoveTeachingGridPolygonVertexCommand =>
        teachingSelectionCaptureOwner.RemovePolygonVertexCommand;
    public ICommand MoveTeachingGridPolygonVertexUpCommand =>
        teachingSelectionCaptureOwner.MovePolygonVertexUpCommand;
    public ICommand MoveTeachingGridPolygonVertexDownCommand =>
        teachingSelectionCaptureOwner.MovePolygonVertexDownCommand;
    public ICommand RemoveSelectedTeachingSelectionCommand =>
        teachingSelectionStoreOwner.RemoveSelectedTeachingSelectionCommand;
    public ICommand UseExistingTeachingSelectionCommand =>
        teachingSelectionStoreOwner.UseExistingTeachingSelectionCommand;
    public ICommand AddOrUpdateCorrespondenceRowCommand =>
        landmarkCorrespondenceEditorOwner.AddOrUpdateRowCommand;
    public ICommand RemoveSelectedCorrespondenceRowCommand =>
        landmarkCorrespondenceEditorOwner.RemoveSelectedRowCommand;
    public ICommand SelectNavigatorItemCommand { get; }
    public ICommand OpenSelectedToolLabCommand { get; }
    public ICommand ValidateTeachingRecipeCommand { get; }
    public ICommand SaveTeachingRecipeCommand { get; }
    public ICommand SaveTeachingRecipeAsCommand { get; }
    public ICommand OpenRecipeInfoEditorCommand => openRecipeInfoEditorCommand;
    public ICommand ApplyRecipeInfoCommand => applyRecipeInfoCommand;
    public ICommand CancelRecipeInfoEditorCommand => cancelRecipeInfoEditorCommand;
    public ICommand CancelActiveWorkbenchEditCommand { get; private set; } = null!;
    public ICommand OpenToolLibraryCommand { get; }
    public ICommand OpenTeachingRecipeCommand { get; }
    public ICommand LoadC3DSourceCommand => sourceLoadOwner.LoadC3DSourceCommand;

    public ToolWorkbenchToolItem? SelectedTool
    {
        get => selectedTool;
        set
        {
            if (ReferenceEquals(selectedTool, value))
            {
                return;
            }

            var started = Stopwatch.GetTimestamp();
            selectedTool = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedToolTitle));
            OnPropertyChanged(nameof(SelectedToolHint));
            NotifyProposedToolRouteChanged();
            addSelectedToolCommand.RaiseCanExecuteChanged();
            lastToolSelectionMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            OnPropertyChanged(nameof(LastToolSelectionMilliseconds));
        }
    }

    public ToolWorkbenchPipelineStepItem? SelectedPipelineStep
    {
        get => selectedPipelineStep;
        set
        {
            if (ReferenceEquals(selectedPipelineStep, value))
            {
                return;
            }

            if (selectedStepExecutionOwner?.IsCommandRunning == true)
            {
                OnPropertyChanged();
                return;
            }

            if (HasPendingStepParameterChanges)
            {
                SetParameterDraftStatus(Localize("다른 도구를 선택하기 전에 설정값을 적용하거나 변경을 취소하세요.", "Apply or discard parameters before selecting another tool."));
                OnPropertyChanged();
                return;
            }

            if (IsTeachingSelectionCaptureActive)
            {
                CancelTeachingSelectionCapture();
            }

            CancelThicknessRepeatGridForSelectionChange();
            MarkPhysicalEdgeGapFlushPreviewStaleIfNeeded();
            var started = Stopwatch.GetTimestamp();
            workspaceProjectionUpdateDepth++;
            try
            {
                selectedPipelineStep = value;
                twoPointLineExecutionOwner?.OnStepSelectionChanged();
                heightMeasurementExecutionOwner?.RestoreSelectedResult();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedPipelineStep));
                OnPropertyChanged(nameof(SelectedPipelineStepTitle));
                NotifySelectedStepFlowProblemChanged();
                OnPropertyChanged(nameof(AvailableInputEntitiesSummary));
                OnPropertyChanged(nameof(SelectedRouteInputIds));
                OnPropertyChanged(nameof(SelectedRouteOutputId));
                OnPropertyChanged(nameof(IsSelectedToolLabAvailable));
                OnPropertyChanged(nameof(IsOrientedBoxEditorContextVisible));
                OnPropertyChanged(nameof(IsSelectedStepRegionSurfaceVisible));
                OnPropertyChanged(nameof(IsSelectedStepSurfaceMatch));
                OnPropertyChanged(nameof(IsSelectedStepGridPolygonDomain));
                OnPropertyChanged(nameof(IsSurfaceMatchExperimentVisible));
                NotifySourceQualityWorkspaceState();
                openSelectedToolLabCommand.RaiseCanExecuteChanged();
                RefreshSelectedStepPropertyDraft();
                RefreshPreparationPresetCommands();
                if (!deferSelectedStepStateRefresh)
                {
                    RefreshTeachingSelectionContext();
                    RefreshSelectedStepExecutionState();
                    RefreshStepCommands();
                    RefreshNavigatorSelection();
                }
            }
            finally
            {
                CompleteWorkspaceProjectionUpdate();
            }
            lastStepSelectionMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            OnPropertyChanged(nameof(LastStepSelectionMilliseconds));
        }
    }

    public double LastToolSelectionMilliseconds => lastToolSelectionMilliseconds;

    public double LastStepSelectionMilliseconds => lastStepSelectionMilliseconds;

    public double LastToolAddMilliseconds => lastToolAddMilliseconds;

    public double LastRecipeValidationMilliseconds => lastRecipeValidationMilliseconds;

    public double LastRecipeEntityRebuildMilliseconds => lastRecipeEntityRebuildMilliseconds;

    public double LastRecipeExecutionStateMilliseconds => lastRecipeExecutionStateMilliseconds;

    public double LastRecipeNotificationMilliseconds => lastRecipeNotificationMilliseconds;

    public double LastRecipeRefreshMilliseconds => lastRecipeRefreshMilliseconds;

    public ToolWorkbenchReferenceItem? SelectedReference
    {
        get => referenceCatalogOwner.SelectedReference;
        set => referenceCatalogOwner.SelectedReference = value;
    }

    public ToolRecipeSelection? SelectedCompatibleSelection
    {
        get => teachingSelectionStoreOwner.SelectedCompatibleSelection;
        set => teachingSelectionStoreOwner.SelectedCompatibleSelection = value;
    }

    public ToolRecipeLandmarkCorrespondence? SelectedCorrespondenceRow
    {
        get => landmarkCorrespondenceEditorOwner.SelectedRow;
        set => landmarkCorrespondenceEditorOwner.SelectedRow = value;
    }

    public string RecipeName
    {
        get => RecipeSession.Name;
        set
        {
            var normalized = value ?? string.Empty;
            if (!RecipeSession.SetName(normalized))
            {
                return;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HeaderRecipeText));
            if (!suppressRecipeRefresh)
            {
                SetDirty(true);
            }

            RefreshRecipeState();
        }
    }

    public string? RecipePath
    {
        get => RecipeSession.Path;
        private set
        {
            if (!RecipeSession.SetPath(value))
            {
                return;
            }

            InvalidateOrderedRun(Localize(
                "레시피가 변경되어 이전 검사 결과 표시를 초기화했습니다.",
                "The recipe changed, so the previous Run evidence was detached from the current context."));
            OnPropertyChanged();
            OnPropertyChanged(nameof(RecipePathSummary));
            OnPropertyChanged(nameof(RecipeStateSummary));
            OnPropertyChanged(nameof(HasRecipeIdentity));
            OnPropertyChanged(nameof(LocalizedRecipePathSummary));
            openRecipeInfoEditorCommand.RaiseCanExecuteChanged();
            recipeSaveFailure = string.Empty;
            OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
            NotifyHeaderRecipeState();
        }
    }

    public bool IsDirty => RecipeSession.IsDirty;
    public bool IsRecipeSaving => isRecipeSaving;
    public bool HasRecipeSaveFailure => recipeSaveFailure.Length > 0;
    public bool IsHeaderRecipeModified => IsDirty || IsValidationSetDefinitionDirty;
    public string HeaderRecipeMetaText => Localize($"도구 {PipelineSteps.Count}", $"{PipelineSteps.Count} tools");
    public string HeaderRecipeSaveStateText => IsRecipeSaving ? Localize("저장 중", "Saving") : HasRecipeSaveFailure ? Localize("저장 실패", "Save failed") : IsDirty ? Localize("조건 미저장", "Conditions unsaved") : IsValidationSetDefinitionDirty ? Localize("샘플 미저장", "Samples unsaved") : HasRecipeIdentity ? Localization.Saved : Localization.Unsaved;
    public string HeaderRecipeDetailText => $"{HeaderRecipeText}\n{HeaderRecipeMetaText}\n{LocalizedRecipeStateSummary}" + (HasRecipeSaveFailure ? $"\n{recipeSaveFailure}" : string.Empty);

    public bool IsRecipeInfoEditorVisible
    {
        get => isRecipeInfoEditorVisible;
        private set => SetField(ref isRecipeInfoEditorVisible, value);
    }

    public string RecipeInfoCategoryDraft
    {
        get => recipeInfoCategoryDraft;
        set => SetField(ref recipeInfoCategoryDraft, value ?? string.Empty);
    }

    public string RecipeInfoInspectionTargetDraft
    {
        get => recipeInfoInspectionTargetDraft;
        set => SetField(ref recipeInfoInspectionTargetDraft, value ?? string.Empty);
    }

    public string RecipeInfoDescriptionDraft
    {
        get => recipeInfoDescriptionDraft;
        set => SetField(ref recipeInfoDescriptionDraft, value ?? string.Empty);
    }

    public string RecipeInfoEditError
    {
        get => recipeInfoEditError;
        private set => SetField(ref recipeInfoEditError, value ?? string.Empty);
    }

    public string RecipeSchemaVersion => RecipeSession.SchemaVersion;

    public string NewReferenceId
    {
        get => referenceCatalogOwner.NewReferenceId;
        set => referenceCatalogOwner.NewReferenceId = value;
    }

    public string NewReferenceName
    {
        get => referenceCatalogOwner.NewReferenceName;
        set => referenceCatalogOwner.NewReferenceName = value;
    }

    public string NewReferenceKind
    {
        get => referenceCatalogOwner.NewReferenceKind;
        set => referenceCatalogOwner.NewReferenceKind = value;
    }

    public string CorrespondenceSourceEntityId
    {
        get => landmarkCorrespondenceEditorOwner.SourceEntityId;
        set => landmarkCorrespondenceEditorOwner.SourceEntityId = value;
    }

    public string CorrespondenceReferenceLandmarkId
    {
        get => landmarkCorrespondenceEditorOwner.ReferenceLandmarkId;
        set => landmarkCorrespondenceEditorOwner.ReferenceLandmarkId = value;
    }

    public double CorrespondenceReferenceX
    {
        get => landmarkCorrespondenceEditorOwner.ReferenceX;
        set => landmarkCorrespondenceEditorOwner.ReferenceX = value;
    }

    public double CorrespondenceReferenceY
    {
        get => landmarkCorrespondenceEditorOwner.ReferenceY;
        set => landmarkCorrespondenceEditorOwner.ReferenceY = value;
    }

    public double CorrespondenceReferenceZ
    {
        get => landmarkCorrespondenceEditorOwner.ReferenceZ;
        set => landmarkCorrespondenceEditorOwner.ReferenceZ = value;
    }

    public string CorrespondenceReferenceFrameId
    {
        get => landmarkCorrespondenceEditorOwner.ReferenceFrameId;
        set => landmarkCorrespondenceEditorOwner.ReferenceFrameId = value;
    }

    public string CorrespondenceReferenceUnit
    {
        get => landmarkCorrespondenceEditorOwner.ReferenceUnit;
        set => landmarkCorrespondenceEditorOwner.ReferenceUnit = value;
    }

    public string CorrespondenceReferenceProvenance
    {
        get => landmarkCorrespondenceEditorOwner.ReferenceProvenance;
        set => landmarkCorrespondenceEditorOwner.ReferenceProvenance = value;
    }

    public string CorrespondenceReferenceRevision
    {
        get => landmarkCorrespondenceEditorOwner.ReferenceRevision;
        set => landmarkCorrespondenceEditorOwner.ReferenceRevision = value;
    }

    public double CorrespondenceMinimumNormalizedTetrahedronVolume
    {
        get => landmarkCorrespondenceEditorOwner.MinimumNormalizedTetrahedronVolume;
        set => landmarkCorrespondenceEditorOwner.MinimumNormalizedTetrahedronVolume = value;
    }

    public bool HasSelectedPipelineStep => SelectedPipelineStep is not null;
    public bool CanEditRecipe => !IsRecipeMutationBlocked;
    public bool CanEditFlowConnection => !IsDisposed
        && !IsRecipeMutationBlocked
        && !HasPendingStepParameterChanges
        && !IsTeachingSelectionCaptureActive
        && !ThicknessRepeatGrid.IsActive
        && !OrientedBoxEditor.IsDraftOpen;
    public bool HasPendingFlowConnection => pendingFlowOutputEntityId is not null;
    public string FlowConnectionMessage => pendingFlowOutputEntityId is { } outputId
        ? flowConnectionFeedback == FlowConnectionFeedback.Rejected
            ? Localization.FlowConnectionRejected
            : string.Format(Localization.FlowConnectionChooseInputFormat, GetFlowOutputName(outputId))
        : flowConnectionFeedback switch
        {
            FlowConnectionFeedback.Saved => Localization.FlowConnectionSaved,
            FlowConnectionFeedback.AlreadyConnected => Localization.FlowConnectionAlreadyConnected,
            FlowConnectionFeedback.Rejected => Localization.FlowConnectionRejected,
            FlowConnectionFeedback.Cancelled => Localization.FlowConnectionCancelled,
            _ => PipelineSteps.Count == 0
                ? Localization.FlowConnectionEmpty
                : Localization.FlowConnectionIdle
        };
    public bool CanOpenRecipeInfoEditor => !string.IsNullOrWhiteSpace(RecipePath)
        && !IsRecipeMutationBlocked
        && !HasPendingStepParameterChanges;

    public bool IsRecipeMutationBlocked =>
        IsNonQueueRecipeMutationBlocked
        || IsInspectionQueueRunning;

    private bool IsNonQueueRecipeMutationBlocked =>
        selectedStepExecutionOwner?.IsCommandRunning == true
        || HasPendingOrderedRunRecordSave
        || IsOrderedRunRunning
        || IsValidationSetRunning
        || IsSurfaceMatchExperimentRunning
        || IsFilterPreviewRunning
        || IsHeightThresholdPreviewRunning
        || IsSignedDeltaPreviewRunning
        || IsRemoveOutlierPreviewRunning
        || IsConnectedRegionPreviewRunning
        || IsGridPolygonDomainPreviewRunning
        || IsDomainMaskPreviewRunning
        || IsEditableRegionPreviewRunning
        || IsLevelSurfacePreviewRunning
        || IsRoiCropPreviewRunning
        || IsEdgePreviewRunning
        || IsTwoPointLinePreviewRunning
        || IsThreePointPlanePreviewRunning
        || IsDatumPlaneDeviationPreviewRunning
        || IsLineFitPreviewRunning
        || IsLineIntersectionPreviewRunning
        || IsLandmarkCorrespondencePreviewRunning
        || IsAffineSolvePreviewRunning
        || IsAffineApplyPreviewRunning
        || IsRegridHeightFieldPreviewRunning
        || IsMeasurementPreviewRunning
        || IsPhysicalEdgeGapFlushPreviewRunning;

    public bool CanSaveTeachingRecipe => !IsRecipeSaving && !HasPendingOrderedRunRecordSave && RecipeSession.StorageValidation.IsValid
        && RecipeSession.SourceBindingErrors.Count == 0;

    public bool IsTeachingRecipeExecutionReady => RecipeSession.Validation.IsValid
        && RecipeSession.SourceBindingErrors.Count == 0
        && SourceSession.SourceIdentityErrors.Count == 0;

    public bool IsRecipeSaveBlocked => !CanSaveTeachingRecipe;

    public string SelectedToolTitle => SelectedTool is null
        ? Localization.NoToolSelected
        : string.Format(Localization.SelectedToolTitleFormat, SelectedTool.Category, SelectedTool.DisplayName);

    public string SelectedToolHint => SelectedTool is null
        ? "Select a tool to inspect its typed input, output, and required parameters."
        : "Add this tool to the teaching recipe, then set its entity IDs and parameters. Adding or editing never runs inspection.";

    public string SelectedPipelineStepTitle => SelectedPipelineStep is null
        ? Localization.NoTaughtStepSelected
        : string.Format(Localization.SelectedPipelineStepTitleFormat, SelectedPipelineStep.Order, SelectedPipelineStep.ToolName);

    public string ValidationSummary => SourceSession.SourceIdentityErrors.Count > 0
            ? string.Format(Localization.ValidationSourceCorrectionsFormat, SourceSession.SourceIdentityErrors.Count)
        : RecipeSession.SourceBindingErrors.Count > 0
        ? string.Format(Localization.ValidationStaleSourceSelectionsFormat, RecipeSession.SourceBindingErrors.Count)
        : RecipeSession.Validation.IsValid
        ? RecipeSession.Validation.Warnings.Count == 0
            ? Localization.ValidationRecipeStructurallyValid
            : string.Format(Localization.ValidationRecipeValidWithWarningsFormat, RecipeSession.Validation.Warnings.Count)
        : RecipeSession.StorageValidation.IsValid
            ? string.Format(Localization.ValidationTeachingCorrectionsFormat, RecipeSession.Validation.Errors.Count)
            : string.Format(
                Localization.ValidationTeachingStructuralCorrectionsFormat,
                RecipeSession.StorageValidation.Errors.Count + RecipeSession.SourceBindingErrors.Count);

    public string RecipePathSummary => string.IsNullOrWhiteSpace(RecipePath)
        ? "Not saved yet"
        : RecipePath;

    public string RecipeStateSummary
    {
        get
        {
            var validationState = SourceSession.SourceIdentityErrors.Count > 0
                ? $"Source needs {SourceSession.SourceIdentityErrors.Count} correction(s)"
                : RecipeSession.SourceBindingErrors.Count > 0
                ? $"{RecipeSession.SourceBindingErrors.Count} stale selection(s)"
                : RecipeSession.Validation.IsValid
                ? RecipeSession.Validation.Warnings.Count == 0 ? "Valid" : $"Valid, {RecipeSession.Validation.Warnings.Count} warning(s)"
                : RecipeSession.StorageValidation.IsValid
                    ? $"{RecipeSession.Validation.Errors.Count} execution requirement(s)"
                    : $"{RecipeSession.StorageValidation.Errors.Count} structural correction(s)";
            var saveState = IsDirty || IsValidationSetDefinitionDirty
                ? "Modified"
                : string.IsNullOrWhiteSpace(RecipePath) ? "Unsaved" : "Saved";
            return $"{validationState} | {saveState}";
        }
    }

    public string SourceContextSummary => string.IsNullOrWhiteSpace(Source.Path)
        ? Localization.SourceNotSelected
        : $"{Source.Format} | {Source.Unit} | {Source.FrameId}";

    public string AlignmentStatusSummary =>
        PipelineSteps.LastOrDefault(step => string.Equals(step.ToolId, "re-grid-height-map", StringComparison.OrdinalIgnoreCase)) is { } regrid
            ? string.Format(Localization.AlignmentStatusFormat, "A3", Localization.RegridHeightMap, regrid.State)
            : PipelineSteps.LastOrDefault(step => string.Equals(step.ToolId, "xyz-affine-apply", StringComparison.OrdinalIgnoreCase)) is { } apply
                ? string.Format(Localization.AlignmentStatusFormat, "A2", Localization.XYZAffineApply, apply.State)
                : PipelineSteps.LastOrDefault(step => string.Equals(step.ToolId, "xyz-affine-solve", StringComparison.OrdinalIgnoreCase)) is { } solve
                    ? string.Format(Localization.AlignmentStatusFormat, "A1", Localization.XYZAffineSolve, solve.State)
                    : PipelineSteps.LastOrDefault(step => string.Equals(step.ToolId, "xyz-affine-transform", StringComparison.OrdinalIgnoreCase)) is { } legacy
                        ? string.Format(Localization.AlignmentLegacyStatusFormat, legacy.State)
                        : Localization.AlignmentNotTaught;

    public ToolWorkbenchTeachingSelectionRequirement? SelectedStepSelectionRequirement =>
        CreateSelectionRequirement(SelectedPipelineStep);

    public bool IsSelectedStepViewerCaptureSupported =>
        SelectedStepSelectionRequirement is { UsesViewerCapture: true };

    public bool IsOrientedBoxEditorContextVisible =>
        OrientedBoxEditor.HasSourceContext
        && (OrientedBoxEditor.IsDraftOpen
            || string.Equals(
                SelectedPipelineStep?.ToolId,
                "oriented-box-authoring",
                StringComparison.Ordinal)
            || SelectedPipelineStep?.InputEntityIds.Any(
                input => Selections.Any(
                    selection =>
                        selection.Kind == ToolRecipeSelectionKinds.OrientedBox3D
                        && string.Equals(
                            selection.Id,
                            input,
                            StringComparison.OrdinalIgnoreCase))) == true);

    public bool IsSelectedStepRegionSurfaceVisible =>
        IsSelectedStepDualRoiMeasurement
        || IsSelectedStepViewerCaptureSupported
        || IsOrientedBoxEditorContextVisible;

    public bool IsSelectedStepCorrespondence =>
        SelectedStepSelectionRequirement is { Kind: ToolRecipeSelectionKinds.LandmarkCorrespondenceSet };

    public string SelectedStepSelectionRequirementTitle => SelectedStepSelectionRequirement switch
    {
        null => Localization.NoViewerSelectionRequired,
        _ when IsSelectedStepThickness => $"{(IsPlaneFlatnessMeasurementRoleActive ? Localization.MeasurementRoi : Localization.ReferenceRoi)} \u00B7 {Localization.TwoGridCorners}",
        { Kind: ToolRecipeSelectionKinds.GridPolygon, UsesViewerCapture: true } requirement =>
            string.Format(Localization.OrderedGridVerticesFormat, Localization.TeachingSelectionPresentation(requirement).Name, requirement.RequiredPointCount),
        { UsesViewerCapture: true } requirement => string.Format(Localization.C3DGridPicksFormat, Localization.TeachingSelectionPresentation(requirement).Name, requirement.RequiredPointCount),
        { Kind: ToolRecipeSelectionKinds.LandmarkCorrespondenceSet } => Localization.LandmarkCorrespondenceRows,
        var requirement => Localization.TeachingSelectionPresentation(requirement).Name
    };

    public string SelectedStepSelectionRequirementSummary => SelectedStepSelectionRequirement switch
    {
        null => Localization.SelectionRequirementUsesExistingEntities,
        _ when IsSelectedStepThickness => Localization.ThicknessRoiTeachingDetail,
        { UsesViewerCapture: true } requirement => $"{Localization.TeachingSelectionPresentation(requirement).Description}{Localization.SelectionCaptureGeometryOnly}",
        { Kind: ToolRecipeSelectionKinds.LandmarkCorrespondenceSet } => Localization.LandmarkCorrespondenceRequirementDetail,
        var requirement => Localization.TeachingSelectionPresentation(requirement).Description
    };

    public ToolRecipeSelection? SelectedStepTeachingSelection => SelectedPipelineStep is null
        ? null
        : IsSelectedStepDualRoiMeasurement
            ? (isPlaneFlatnessMeasurementRole ? PlaneFlatnessMeasurementSelection : PlaneFlatnessReferenceSelection)
            : Selections.FirstOrDefault(selection =>
                SelectedPipelineStep.InputEntityIds.Contains(selection.Id, StringComparer.OrdinalIgnoreCase)
                && ToolWorkbenchTeachingSelectionPolicy.MatchesRequirement(
                    selection,
                    SelectedStepSelectionRequirement));

    public string SelectedStepTeachingSelectionSummary => SelectedStepTeachingSelection is null
        ? (SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
            ? Localization.NoRoiTaught
            : Localization.NoRecipeOwnedSelectionRouted)
        : ToolWorkbenchTeachingSelectionPolicy.FormatSelection(
            SelectedStepTeachingSelection);

    public string SelectionCaptureActionText => SelectedStepTeachingSelection is null
        ? (SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
            ? Localization.CaptureRoi
            : Localization.CaptureSelection)
        : (SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
            ? Localization.ReplaceRoi
            : Localization.ReplaceSelection);

    public string ThicknessRoiTeachingDetail => Localization.ThicknessRoiTeachingDetail;

    public bool IsTeachingSelectionCaptureActive => teachingSelectionCaptureOwner.IsActive;

    public bool IsSelectionCandidateActive =>
        teachingSelectionCaptureOwner.IsCandidateActive;

    public bool IsPipelineReviewExpanded => !IsSelectionCandidateActive && HasPipelineSteps;

    public int TeachingSelectionCapturedPointCount =>
        teachingSelectionCaptureOwner.CapturedPointCount;

    public int TeachingSelectionRequiredPointCount =>
        teachingSelectionCaptureOwner.RequiredPointCount;

    public bool CanApplyTeachingSelectionCapture => teachingSelectionCaptureOwner.CanApply;

    public bool IsTeachingGridRectangleEditorVisible =>
        teachingSelectionCaptureOwner.IsGridRectangleEditorVisible;

    public bool IsTeachingGridRectangleEditorEnabled =>
        teachingSelectionCaptureOwner.IsGridRectangleEditorEnabled;

    public bool IsTeachingGridCircleEditorVisible =>
        teachingSelectionCaptureOwner.IsGridCircleEditorVisible;

    public bool IsTeachingGridCircleEditorEnabled =>
        teachingSelectionCaptureOwner.IsGridCircleEditorEnabled;

    public bool IsTeachingGridPolygonEditorVisible =>
        teachingSelectionCaptureOwner.IsGridPolygonEditorVisible;

    public bool IsTeachingGridPolygonEditorEnabled =>
        teachingSelectionCaptureOwner.IsGridPolygonEditorEnabled;

    public bool IsTeachingGridPolygonDraftValid =>
        teachingSelectionCaptureOwner.IsGridPolygonDraftValid;

    public string TeachingGridPolygonValidationSummary
    {
        get
        {
            return teachingSelectionCaptureOwner.GridPolygonValidationSummary;
        }
    }

    public string TeachingGridPolygonSourceFrameSummary =>
        IsTeachingGridPolygonDraftValid
            ? $"{TeachingGridPolygonVertices.Count} ordered vertex(es) | X=column, Z=row | {SelectedStepTeachingSelection?.FrameId ?? Source.FrameId}"
            : Localization.GridPolygonSourceFrameInvalid;

    public int TeachingGridCircleCenterRow
    {
        get => teachingSelectionCaptureOwner.GridCircleCenterRow;
        set => teachingSelectionCaptureOwner.GridCircleCenterRow = value;
    }

    public int TeachingGridCircleCenterColumn
    {
        get => teachingSelectionCaptureOwner.GridCircleCenterColumn;
        set => teachingSelectionCaptureOwner.GridCircleCenterColumn = value;
    }

    public double TeachingGridCircleRadius
    {
        get => teachingSelectionCaptureOwner.GridCircleRadius;
        set => teachingSelectionCaptureOwner.GridCircleRadius = value;
    }

    public bool IsTeachingGridCircleDraftValid =>
        teachingSelectionCaptureOwner.IsGridCircleDraftValid;

    public string TeachingGridCircleValidationSummary
    {
        get
        {
            return teachingSelectionCaptureOwner.GridCircleValidationSummary;
        }
    }

    public string TeachingGridCircleSourceFrameSummary =>
        IsTeachingGridCircleDraftValid
            ? $"Center X/column {TeachingGridCircleCenterColumn}, Z/row {TeachingGridCircleCenterRow} | radius {TeachingGridCircleRadius:0.##} cells | {SelectedStepTeachingSelection?.FrameId ?? Source.FrameId}"
            : "Circular source-grid footprint unavailable until the center and radius are valid.";

    public int TeachingGridRectangleRow
    {
        get => teachingSelectionCaptureOwner.GridRectangleRow;
        set => teachingSelectionCaptureOwner.GridRectangleRow = value;
    }

    public int TeachingGridRectangleColumn
    {
        get => teachingSelectionCaptureOwner.GridRectangleColumn;
        set => teachingSelectionCaptureOwner.GridRectangleColumn = value;
    }

    public int TeachingGridRectangleRowCount
    {
        get => teachingSelectionCaptureOwner.GridRectangleRowCount;
        set => teachingSelectionCaptureOwner.GridRectangleRowCount = value;
    }

    public int TeachingGridRectangleColumnCount
    {
        get => teachingSelectionCaptureOwner.GridRectangleColumnCount;
        set => teachingSelectionCaptureOwner.GridRectangleColumnCount = value;
    }

    public string TeachingGridRectangleColumnText
    {
        get => teachingSelectionCaptureOwner.GridRectangleColumnText;
        set => teachingSelectionCaptureOwner.GridRectangleColumnText = value;
    }

    public string TeachingGridRectangleRowText
    {
        get => teachingSelectionCaptureOwner.GridRectangleRowText;
        set => teachingSelectionCaptureOwner.GridRectangleRowText = value;
    }

    public string TeachingGridRectangleColumnCountText
    {
        get => teachingSelectionCaptureOwner.GridRectangleColumnCountText;
        set => teachingSelectionCaptureOwner.GridRectangleColumnCountText = value;
    }

    public string TeachingGridRectangleRowCountText
    {
        get => teachingSelectionCaptureOwner.GridRectangleRowCountText;
        set => teachingSelectionCaptureOwner.GridRectangleRowCountText = value;
    }

    public bool IsTeachingGridRectangleDraftValid =>
        teachingSelectionCaptureOwner.IsGridRectangleDraftValid;

    public string TeachingGridRectangleValidationSummary
    {
        get
        {
            return teachingSelectionCaptureOwner.GetGridRectangleValidationSummary(OpenVisionLanguageService.CurrentLanguage != OpenVisionLanguage.English);
        }
    }

    public string TeachingGridRectangleSourceFrameSummary =>
        IsTeachingGridRectangleDraftValid
            ? $"X columns {TeachingGridRectangleColumn}..{TeachingGridRectangleColumn + TeachingGridRectangleColumnCount - 1} | Z rows {TeachingGridRectangleRow}..{TeachingGridRectangleRow + TeachingGridRectangleRowCount - 1} | {SelectedStepTeachingSelection?.FrameId ?? Source.FrameId}"
            : Localization.GridRectangleSourceFrameInvalid;

    public string TeachingSelectionCaptureTitle => !IsTeachingSelectionCaptureActive
        && OrientedBoxEditor.IsDraftOpen
            ? $"{OrientedBoxEditor.Name} · {Localization.ThreeDBoxReview}"
        : SelectedStepSelectionRequirement is null
        ? Localization.SelectionCapture
        : SelectedStepSelectionRequirement.Kind == ToolRecipeSelectionKinds.GridRectangle
            ? $"{Localization.TeachingSelectionPresentation(SelectedStepSelectionRequirement).Name} \u00B7 {(CanApplyTeachingSelectionCapture ? Localization.RoiReview : Localization.RoiDrawing)}"
            : SelectedStepSelectionRequirement.Kind == ToolRecipeSelectionKinds.GridPolygon
                ? $"{Localization.SelectionCapture}: {Localization.TeachingSelectionPresentation(SelectedStepSelectionRequirement).Name} · {Localization.OrderedVertices}"
            : $"{Localization.SelectionCapture}: {Localization.TeachingSelectionPresentation(SelectedStepSelectionRequirement).Name}";

    public string TeachingSelectionCaptureProgress => IsTeachingSelectionCaptureActive
        ? CanApplyTeachingSelectionCapture
            && SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
                ? Localization.RoiCaptureReadyProgress
            : CanApplyTeachingSelectionCapture
                && SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridPolygon
                    ? string.Format(
                        Localization.OrderedPolygonVerticesReadyFormat,
                        TeachingSelectionCapturedPointCount)
                : string.Format(
                    Localization.SelectionCaptureProgressFormat,
                    TeachingSelectionCapturedPointCount,
                    TeachingSelectionRequiredPointCount)
        : OrientedBoxEditor.IsDraftOpen
            ? Localization.TransientCandidateEditProgress
        : Localization.SelectionCaptureInactive;

    public string TeachingSelectionCaptureInstruction =>
        !IsTeachingSelectionCaptureActive
            ? OrientedBoxEditor.IsDraftOpen
                ? OrientedBoxEditor.ValidationSummary
                : string.Empty
            : SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
                ? CanApplyTeachingSelectionCapture
                    ? Localization.RoiCaptureReadyInstruction
                    : TeachingSelectionCapturedPointCount == 0
                        ? Localization.RoiCaptureStartInstruction
                        : Localization.RoiCaptureSecondInstruction
                : SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridPolygon
                    ? Localization.GridPolygonCaptureInstruction
                : TeachingSelectionCaptureProgress;

    private void OnTeachingLocalizationChanged(object? sender, PropertyChangedEventArgs args)
    {
        foreach (var step in PipelineSteps)
        {
            step.RefreshLocalizedStatePresentation();
        }

        OnPropertyChanged(nameof(SelectedStepSelectionRequirement));
        OnPropertyChanged(nameof(SelectedStepSelectionRequirementTitle));
        OnPropertyChanged(nameof(SelectedStepSelectionRequirementSummary));
        OnPropertyChanged(nameof(SelectedToolTitle));
        OnPropertyChanged(nameof(SelectedPipelineStepTitle));
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(FlowConnectionMessage));
        OnPropertyChanged(nameof(PipelineEmptyHint));
        OnPropertyChanged(nameof(SourceContextSummary));
        OnPropertyChanged(nameof(AlignmentStatusSummary));
        OnPropertyChanged(nameof(CurrentSourceHeaderStatusText));
        OnPropertyChanged(nameof(CurrentSourceHeaderStatusDetail));
        OnPropertyChanged(nameof(SelectedStepAdapterStatus));
        stepPropertySession.RefreshLocalizedStatus();
        OnPropertyChanged(nameof(StepParameterEditStatus));
        OnPropertyChanged(nameof(SelectedStepTeachingSelectionSummary));
        OnPropertyChanged(nameof(SelectionCaptureActionText));
        OnPropertyChanged(nameof(ThicknessRoiTeachingDetail));
        OnPropertyChanged(nameof(TeachingSelectionCaptureTitle));
        OnPropertyChanged(nameof(TeachingSelectionCaptureProgress));
        OnPropertyChanged(nameof(TeachingSelectionCaptureInstruction));
        OnPropertyChanged(nameof(TeachingGridRectangleSourceFrameSummary));
        OnPropertyChanged(nameof(TeachingGridRectangleValidationSummary));
        OnPropertyChanged(nameof(TeachingGridPolygonSourceFrameSummary));
        OnPropertyChanged(nameof(OrderedRunStatus));
        OnPropertyChanged(nameof(OrderedRunCapabilitySummary));
        OnPropertyChanged(nameof(OrderedRunEvidenceSummary));
        OnPropertyChanged(nameof(PreparationPresetAssistantStageText));
        OnPropertyChanged(nameof(PreparationPresetAssistantSummary));
        RefreshPreparationPresetOptions();
        RefreshSelectedToolWorkspaceProjection();
    }

    public string CorrespondenceCommitActionText =>
        landmarkCorrespondenceEditorOwner.CommitActionText;

    public string CorrespondenceSelectionSummary =>
        landmarkCorrespondenceEditorOwner.SelectionSummary;

    public string PipelineEmptyHint => PipelineSteps.Count == 0
        ? Localization.PipelineEmptyHint
        : string.Empty;

    public bool IsPipelineEmpty => PipelineSteps.Count == 0;

    public bool HasPipelineSteps => PipelineSteps.Count > 0;

    private void RefreshFilteredTools()
    {
        var query = toolSearchText.Trim();
        FilteredTools.ReplaceAll(Tools.Where(tool =>
            query.Length == 0
            || tool.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || tool.Id.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || tool.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || tool.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || tool.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || tool.SearchAliases?.Any(alias =>
                alias.Contains(query, StringComparison.CurrentCultureIgnoreCase)) == true));
    }

    public string AvailableInputEntitiesSummary => string.Join(
        ", ",
        EnumerateAvailableEntitiesBefore(SelectedPipelineStep).Select(entity => entity.Id));

    public ToolWorkbenchC3DSourceStatePerformance? LastC3DSourceStatePerformance { get; private set; }

    public void SetC3DSource(string path, bool markDirty = true)
        => SetC3DSourceCore(path, TryReadSourceBinding(path), markDirty);

    internal void SetC3DSourceFromLoadedViewer(
        string path,
        ToolRecipeSelectionSourceBinding sourceBinding,
        bool markDirty = true)
    {
        ArgumentNullException.ThrowIfNull(sourceBinding);
        if (!string.Equals(sourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || sourceBinding.ContentSha256.Length != 64
            || sourceBinding.GridWidth <= 0
            || sourceBinding.GridHeight <= 0)
        {
            throw new ArgumentException(
                "The loaded Viewer source binding must contain a C3D SHA-256 and positive grid dimensions.",
                nameof(sourceBinding));
        }

        SetC3DSourceCore(path, sourceBinding, markDirty);
    }

    private void SetC3DSourceCore(
        string path,
        ToolRecipeSelectionSourceBinding? sourceBinding,
        bool markDirty)
    {
        if (HasPendingOrderedRunRecordSave) return;
        var totalStart = Stopwatch.GetTimestamp();
        var captureMilliseconds = 0.0;
        var clearPreviewMilliseconds = 0.0;
        var identityMilliseconds = 0.0;
        var recipeStateMilliseconds = 0.0;
        var selectionSyncMilliseconds = 0.0;
        var loggingMilliseconds = 0.0;

        void RecordPerformance() =>
            LastC3DSourceStatePerformance = new ToolWorkbenchC3DSourceStatePerformance(
                captureMilliseconds,
                clearPreviewMilliseconds,
                identityMilliseconds,
                recipeStateMilliseconds,
                selectionSyncMilliseconds,
                loggingMilliseconds,
                Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds);

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var stageStart = Stopwatch.GetTimestamp();
        if (IsTeachingSelectionCaptureActive)
        {
            CancelTeachingSelectionCapture();
        }
        captureMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        var sourcePathChanged = !string.Equals(Source.Path, fullPath, StringComparison.OrdinalIgnoreCase);
        stageStart = Stopwatch.GetTimestamp();
        workspaceProjectionUpdateDepth++;
        try
        {
            if (sourcePathChanged)
            {
                ClearFilterPreview("Source changed; Preview is required.");
                ClearHeightThresholdPreview(
                    "Source changed; Height Threshold Background Removal Preview is required.");
                ClearSignedDeltaBackgroundSubtractionPreview(
                    "Source changed; Signed Delta Background Subtraction Preview is required.");
                ClearRemoveOutlierPreview(
                    "Source changed; Remove Outlier Pixels Preview is required.");
                ClearConnectedRegionPreview(
                    "Source changed; Connected Region Preview is required.");
                ClearGridPolygonDomainPreview(
                    "Source changed; Grid Polygon Domain Preview is required.");
                ClearDomainMaskPreview(
                    "Source changed; Domain / Mask Preview is required.");
                ClearEditableRegionPreview(
                    "Source changed; Editable Region Preview is required.");
                ClearLevelSurfacePreview(
                    "Source changed; Level Surface Preview is required.");
                ClearRoiCropPreview(
                    "Source changed; ROI / Crop Preview is required.");
                ClearTwoPointLinePreview("Source changed; 2-Point Line Preview is required.");
                ClearThreePointPlanePreview("Source changed; 3-Point Plane Preview is required.");
                ClearMeasurementPreview("Source changed; measurement Preview is required.");
                ClearPhysicalEdgeGapFlushPreview("Source changed; physical-edge Gap / Flush Preview is required.");
            }
        }
        finally
        {
            CompleteWorkspaceProjectionUpdate();
        }
        clearPreviewMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        var sourceBindingChanged = SourceSession.SetSourceBinding(sourceBinding);
        if (sourcePathChanged || sourceBindingChanged)
        {
            EndStepRemovalHistory();
            HeightImageViewer.ClearSource();
            InvalidateOrderedRun(Localize(
                "3D 입력이 변경되었습니다. 현재 레시피 결과는 더 이상 현재 입력의 결과가 아닙니다.",
                "The 3D input changed. The current recipe result no longer belongs to the current input."));
        }
        AcceptCurrentSourceIdentity();
        identityMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
        if (!sourcePathChanged)
        {
            if (sourceBindingChanged && markDirty)
            {
                SetDirty(true);
            }

            stageStart = Stopwatch.GetTimestamp();
            RefreshRecipeState();
            recipeStateMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
            stageStart = Stopwatch.GetTimestamp();
            teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
            selectionSyncMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
            BeginSourceQualityLoad();
            RecordPerformance();
            return;
        }

        stageStart = Stopwatch.GetTimestamp();
        MutateRecipe(() =>
        {
            Source.Id = "source.c3d.height-map";
            Source.Name = Path.GetFileNameWithoutExtension(fullPath);
            Source.Format = "C3D";
            Source.Unit = "raw-height";
            Source.FrameId = "frame.c3d-grid-index";
            Source.Path = fullPath;
            SourceSession.SetSourceAcquisitionProvenance(CreateUnavailableSourceAcquisitionProvenance("frame.c3d-grid-index"));
        }, markDirty);
        SourceQuality.LoadAcquisitionProvenance(SourceSession.SourceAcquisitionProvenance, Source.FrameId);
        InvalidateSurfaceEdgeAcquisitionDirectionEvidence();
        OnPropertyChanged(nameof(SourceAcquisitionProvenance));
        recipeStateMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
        stageStart = Stopwatch.GetTimestamp();
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        selectionSyncMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
        stageStart = Stopwatch.GetTimestamp();
        AppendLog("Source", $"C3D source taught: {Path.GetFileName(fullPath)}.");
        OVLog.Write(LogCategory.UI, LogLevel.Info, $"Tool recipe C3D source selected: {fullPath}");
        loggingMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
        BeginSourceQualityLoad();
        RecordPerformance();
    }

    private void OpenRecipeInfoEditor()
    {
        if (!CanOpenRecipeInfoEditor)
        {
            return;
        }

        RecipeInfoCategoryDraft = RecipeSession.Category ?? string.Empty;
        RecipeInfoInspectionTargetDraft = RecipeSession.InspectionTarget ?? string.Empty;
        RecipeInfoDescriptionDraft = RecipeSession.Description ?? string.Empty;
        RecipeInfoEditError = string.Empty;
        IsRecipeInfoEditorVisible = true;
    }

    private void ApplyRecipeInfoEditor()
    {
        if (RecipeInfoCategoryDraft.Length > ToolRecipeDocument.MaximumMetadataFieldLength
            || RecipeInfoInspectionTargetDraft.Length > ToolRecipeDocument.MaximumMetadataFieldLength
            || RecipeInfoDescriptionDraft.Length > ToolRecipeDocument.MaximumMetadataFieldLength)
        {
            RecipeInfoEditError = Localization.RecipeInfoFieldTooLong;
            return;
        }

        var category = NormalizeRecipeInfoValue(RecipeInfoCategoryDraft);
        var target = NormalizeRecipeInfoValue(RecipeInfoInspectionTargetDraft);
        var description = NormalizeRecipeInfoValue(RecipeInfoDescriptionDraft);
        var metadataChanged = RecipeSession.SetMetadata(category, target, description);
        var schemaChanged = (category is not null || target is not null || description is not null)
            && RecipeSession.SchemaVersion != ToolRecipeDocument.CurrentSchemaVersion
            && RecipeSession.SchemaVersion != ToolRecipeDocument.AcceptanceLimitsSchemaVersion
            && RecipeSession.SetSchemaVersion(ToolRecipeDocument.RecipeMetadataSchemaVersion);
        if (metadataChanged || schemaChanged)
        {
            if (schemaChanged)
            {
                OnPropertyChanged(nameof(RecipeSchemaVersion));
            }

            SetDirty(true, invalidateOrderedRun: false);
            RefreshRecipeState();
        }

        RecipeInfoEditError = string.Empty;
        IsRecipeInfoEditorVisible = false;
    }

    private void CancelRecipeInfoEditor()
    {
        RecipeInfoEditError = string.Empty;
        IsRecipeInfoEditorVisible = false;
    }

    private static string? NormalizeRecipeInfoValue(string value)
    {
        var normalized = value.Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    public bool TrySaveTeachingRecipe(string path, out string message)
    {
        if (IsRecipeSaving)
        {
            message = Localize("레시피를 저장 중입니다.", "The recipe is being saved.");
            return false;
        }
        if (HasPendingOrderedRunRecordSave)
        {
            message = Localize("검사 기록 저장을 재시도한 뒤 레시피를 저장하세요.", "Retry saving the inspection Run Record before saving the recipe.");
            return false;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        AppendLog(
            "Save",
            $"Recipe save requested | path={Path.GetFullPath(path)} | dirty={IsDirty} | steps={PipelineSteps.Count} | selections={Selections.Count}.");
        if (HasPendingStepParameterChanges)
        {
            message = Localize("변경한 설정값을 적용하거나 취소한 뒤 저장하세요.", "Apply or cancel the selected tool settings before saving.");
            AppendLog("Warning", $"Recipe save rejected | reason={message}");
            return false;
        }

        RefreshRecipeState();
        if (!CanSaveTeachingRecipe)
        {
            message = string.Join(
                Environment.NewLine,
                RecipeSession.StorageValidation.Errors.Concat(RecipeSession.SourceBindingErrors));
            AppendLog("Warning", $"Recipe save rejected | errors={message.Replace(Environment.NewLine, " | ", StringComparison.Ordinal)}");
            return false;
        }

        isRecipeSaving = true;
        recipeSaveFailure = string.Empty;
        NotifyHeaderRecipeState();
        var saveBackups = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var primarySaveStarted = false;
        var primarySaveCommitted = false;
        var retainSaveBackups = false;
        try
        {
            var fullPath = Path.GetFullPath(path);
            // A later sidecar failure must not leave a new recipe with old sample definitions.
            foreach (var savePath in new[] { fullPath, ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(fullPath), ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(fullPath) })
            {
                var backupPath = File.Exists(savePath) ? savePath + ".save-backup-" + Guid.NewGuid().ToString("N") : null;
                saveBackups.Add(savePath, backupPath);
                if (backupPath is not null) File.Copy(savePath, backupPath);
            }
            primarySaveStarted = true;
            string savedSha256;
            if (!IsDirty && RecipeSession.SavedSha256 is { } currentSha && string.Equals(RecipePath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                using var savedRecipe = File.OpenRead(fullPath);
                if (!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(savedRecipe)).Equals(currentSha, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(Localize("저장된 레시피 파일이 변경되었습니다. 다시 연 뒤 저장하세요.", "The saved recipe changed outside this session. Reopen it before saving."));
                savedSha256 = currentSha;
            }
            else
            {
                var signedDeltaSidecar = signedDeltaBackgroundExecutionOwner.PersistPublishedSidecar(fullPath);
                ToolRecipeDocumentStore.Save(fullPath, CreateDocument(fullPath, signedDeltaSidecar), out savedSha256);
            }
            SaveValidationSetDefinition(fullPath, markClean: false);
            SaveValidationThresholdCorrectionEvidence(fullPath);
            primarySaveCommitted = true;
            if (RecipePath is { } previousPath && !string.Equals(previousPath, fullPath, StringComparison.OrdinalIgnoreCase)) EndStepRemovalHistory();
            RecipePath = fullPath;
            connectedRegionExecutionOwner.PersistPublishedArtifactIfPossible();
            gridPolygonDomainExecutionOwner.PersistPublishedArtifactIfPossible();
            domainMaskExecutionOwner.PersistPublishedArtifactIfPossible();
            editableRegionExecutionOwner.PersistPublishedArtifactIfPossible();
            levelSurfaceExecutionOwner.PersistPublishedArtifactIfPossible();
            roiCropExecutionOwner.PersistPublishedArtifactIfPossible();
            RecipeSession.AcceptSavedIdentity(savedSha256);
            SetValidationSetDefinitionDirty(false);
            SetDirty(false);
            RecordRecentRecipe(fullPath);
            message = $"Teaching recipe saved: {Path.GetFileName(fullPath)}";
            AppendLog("Teach", message);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            if (primarySaveStarted && !primarySaveCommitted)
            {
                foreach (var (savePath, backupPath) in saveBackups)
                {
                    try
                    {
                        if (backupPath is null)
                        {
                            if (File.Exists(savePath)) File.Delete(savePath);
                        }
                        else if (!File.Exists(savePath) || !File.ReadAllBytes(savePath).SequenceEqual(File.ReadAllBytes(backupPath)))
                        {
                            File.Move(backupPath, savePath, overwrite: true);
                        }
                    }
                    catch (Exception rollbackException) when (rollbackException is IOException or UnauthorizedAccessException)
                    {
                        retainSaveBackups = true;
                        message += Environment.NewLine + Localize("저장 파일을 복원하지 못했습니다: ", "Could not restore the save file: ") + savePath
                            + Environment.NewLine + Localize("복구 사본: ", "Recovery copy: ") + (backupPath ?? "—");
                        AppendLog("Error", $"Recipe save rollback failed | path={savePath} | backup={backupPath} | error={rollbackException.Message}");
                    }
                }
            }
            recipeSaveFailure = message;
            AppendLog("Error", $"Teaching recipe save failed: {message}");
            return false;
        }
        finally
        {
            if (!retainSaveBackups)
            {
                foreach (var backupPath in saveBackups.Values.OfType<string>())
                {
                    try { if (File.Exists(backupPath)) File.Delete(backupPath); }
                    catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
                    {
                        AppendLog("Warning", $"Recipe save backup cleanup failed | path={backupPath} | error={cleanupException.Message}");
                    }
                }
            }
            isRecipeSaving = false;
            NotifyHeaderRecipeState();
        }
    }

    public bool TryOpenTeachingRecipe(string path, out string message)
    {
        if (HasPendingOrderedRunRecordSave)
        {
            message = Localize("검사 기록 저장을 재시도한 뒤 다른 레시피를 여세요.", "Retry saving the inspection Run Record before opening another recipe.");
            return false;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var fullPath = Path.GetFullPath(path);
            var document = ResolveRelativeSourcePath(ToolRecipeDocumentStore.Load(fullPath, out var savedSha256), fullPath);
            // Read and validate every required sidecar before replacing the current session.
            var validationDefinition = ToolRecipeValidationSetDefinitionStore.LoadForRecipe(fullPath);
            var correctionEvidence = ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(fullPath);
            if (correctionEvidence is not null
                && string.Equals(correctionEvidence.RecipeSourceSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                && string.Equals(correctionEvidence.RecipeName, document.Name, StringComparison.Ordinal))
            {
                _ = ToolRecipeThresholdCandidateParameterMapper.ApplyProposal(document, correctionEvidence.Proposal);
            }
            if (!signedDeltaBackgroundExecutionOwner.TryLoadPublishedSidecar(
                    fullPath,
                    document,
                    out var signedDeltaSidecar,
                    out var signedDeltaSidecarMessage))
            {
                message = signedDeltaSidecarMessage;
                AppendLog("Error", $"Teaching recipe open rejected: {message}");
                return false;
            }

            // ApplyDocument refreshes execution state synchronously. Bind the
            // incoming C3D identity first so that a prior recipe's source
            // hash cannot be used while the new document is being applied.
            SourceSession.SetSourceBinding(TryReadSourceBinding(document.Source.Path));
            ApplyDocument(document);
            ViewerWorkspace.ResetContentPins();
            RefreshRecipeState();
            // Finish applying and validating the recipe before the asynchronous
            // Source Quality callback can rebuild the presentation collections.
            // This keeps non-UI callers deterministic while the UI dispatcher
            // still receives the same eventual report.
            BeginSourceQualityLoad();
            RecipePath = fullPath;
            signedDeltaBackgroundExecutionOwner.RestorePublishedSidecar(document, signedDeltaSidecar);
            connectedRegionExecutionOwner.RestorePublishedConnectedRegionArtifact();
            gridPolygonDomainExecutionOwner.RestorePublishedArtifact();
            domainMaskExecutionOwner.RestorePublishedArtifact();
            editableRegionExecutionOwner.RestorePublishedArtifact();
            levelSurfaceExecutionOwner.RestorePublishedArtifact();
            roiCropExecutionOwner.RestorePublishedArtifact();
            LoadValidationSetDefinition(document, validationDefinition);
            LoadValidationThresholdCorrectionEvidence(document, correctionEvidence);
            RecipeSession.AcceptSavedIdentity(savedSha256);
            SetDirty(false);
            recipeSaveFailure = string.Empty;
            NotifyHeaderRecipeState();
            RecordRecentRecipe(fullPath);
            ToolSearchText = string.Empty;
            teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
            message = $"Teaching recipe opened: {Path.GetFileName(fullPath)}";
            AppendLog("Teach", message);
            OVLog.Write(LogCategory.UI, LogLevel.Info, message);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            AppendLog("Error", $"Teaching recipe open failed: {message}");
            OVLog.Write(LogCategory.UI, LogLevel.Error, $"Teaching recipe open failed: {message}");
            return false;
        }
    }

    public void CreateNewTeachingRecipe(string? name = null)
    {
        if (HasPendingOrderedRunRecordSave) return;
        EndStepRemovalHistory();
        if (IsTeachingSelectionCaptureActive)
        {
            CancelTeachingSelectionCapture();
        }

        ClearPhysicalEdgeGapFlushPreview("New recipe created; physical-edge Gap / Flush Preview is required.");

        MutateRecipe(() =>
        {
            ViewerWorkspace.ResetContentPins();
            SourceSession.SetSourceBinding(null);
            SourceSession.ClearDecodedSource();
            HeightImageViewer.ClearSource();
            AcceptCurrentSourceIdentity();
            SourceQuality.Clear();
            SourceSession.SetSourceAcquisitionProvenance(CreateUnavailableSourceAcquisitionProvenance("frame.c3d-grid-index"));
            RecipeSession.SetSchemaVersion(ToolRecipeDocument.CurrentSchemaVersion);
            RecipeSession.SetMetadata(null, null, null);
            RecipeName = string.IsNullOrWhiteSpace(name)
                ? ToolWorkbenchRecipeSession.DefaultName
                : name.Trim();
            Source.Id = "source.c3d.height-map";
            Source.Name = "No C3D source selected";
            Source.Format = "C3D";
            Source.Unit = "raw-height";
            Source.FrameId = "frame.c3d-grid-index";
            Source.Path = string.Empty;
            referenceCatalogOwner.Clear();
            teachingSelectionStoreOwner.Clear();
            PipelineSteps.Clear();
            SelectedPipelineStep = null;
            RecipePath = null;
            recipeSaveFailure = string.Empty;
        }, markDirty: false);
        SourceQuality.LoadAcquisitionProvenance(SourceSession.SourceAcquisitionProvenance, Source.FrameId);
        OnPropertyChanged(nameof(SourceAcquisitionProvenance));
        SetDirty(false);
        ClearValidationSet();
        SetValidationSetDefinitionDirty(false);
        ToolSearchText = string.Empty;
        OnPropertyChanged(nameof(RecipeSchemaVersion));
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        runLogOwner.ClearExecutionEntries();
        AppendLog("Teach", "New empty teaching recipe created. Select a C3D source before adding an inspection step.");
    }

    public bool TryCreateEmptyTeachingRecipe(ToolWorkbenchFirstRecipeSetup setup, out string message)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentException.ThrowIfNullOrWhiteSpace(setup.RecipeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(setup.RecipePath);
        if (IsDisposed || IsRecipeMutationBlocked || HasPendingStepParameterChanges)
        {
            message = HasPendingStepParameterChanges
                ? "Apply or discard the selected step parameter draft before creating a recipe."
                : Localization.FirstRecipeCreateBusy;
            return false;
        }

        var current = CreateDocument();
        var document = current with
        {
            SchemaVersion = ToolRecipeDocument.CurrentSchemaVersion,
            Name = setup.RecipeName.Trim(),
            Category = setup.Category,
            InspectionTarget = setup.InspectionTarget,
            Description = setup.Description,
            Source = current.Source with
            {
                Id = "source.c3d.height-map",
                Name = "No C3D source selected",
                Format = "C3D",
                Unit = "raw-height",
                FrameId = "frame.c3d-grid-index",
                Path = string.Empty,
                ByteLength = null,
                ContentSha256 = null,
                GridWidth = null,
                GridHeight = null,
                AcquisitionProvenance = CreateUnavailableSourceAcquisitionProvenance("frame.c3d-grid-index"),
                MeasurementEvidence = null,
                SensorId = null
            },
            References = [],
            Steps = [],
            Selections = []
        };
        var storageValidation = ToolRecipeValidator.ValidateForStorage(document);
        if (!storageValidation.IsValid)
        {
            message = string.Join(Environment.NewLine, storageValidation.Errors);
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(setup.RecipePath);
            ToolRecipeDocumentStore.SaveNew(fullPath, document);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            AppendLog("Error", $"New teaching recipe creation failed: {message}");
            return false;
        }

        return TryOpenTeachingRecipe(fullPath, out message);
    }

    private void AddSelectedTool(ToolWorkbenchToolItem? requestedTool = null)
    {
        var tool = requestedTool ?? SelectedTool;
        if (tool is not null)
        {
            SelectedTool = tool;
        }

        AddToolToRecipe(tool, explicitInputIds: null);
    }

    private void AddToolToRecipe(ToolWorkbenchToolItem? tool, string? explicitInputIds)
    {
        if (tool is null)
        {
            return;
        }

        if (tool.RouteKind == ToolWorkbenchToolRouteKind.ReviewOnly)
        {
            AppendLog("Warning", $"Tool add rejected: {tool.Name} | {Localization.ReviewOnlyToolRouteDetail}");
            return;
        }

        var started = Stopwatch.GetTimestamp();
        var input = explicitInputIds;
        if (input is null)
        {
            var proposal = GetProposedInputRoute(tool);
            if (!proposal.IsCompatible)
            {
                AppendLog("Warning", $"Tool add rejected: {tool.Name} | {proposal.Detail}");
                return;
            }

            input = proposal.InputEntityIds;
        }

        IReadOnlyList<ToolRecipeParameter>? parameters = null;
        if (string.Equals(tool.Id, "domain-mask", StringComparison.Ordinal)
            && input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length == 3)
        {
            parameters = [new ToolRecipeParameter("SelectedRegionIndex", "0")];
        }

        var step = new ToolWorkbenchPipelineStepItem(
            CreateUniqueStepId(tool.Id),
            tool,
            input ?? string.Empty,
            CreateUniqueOutputId(tool.OutputContract),
            parameters);
        if (ToolRecipeStep.RequiresAcceptanceLimits(step.ToolId))
        {
            step.AcceptanceLimitsConfigured = false;
            RecipeSession.SetSchemaVersion(ToolRecipeDocument.CurrentSchemaVersion);
        }
        SubscribeStep(step);
        PipelineSteps.Add(step);
        deferSelectedStepStateRefresh = true;
        try
        {
            SelectedPipelineStep = step;
        }
        finally
        {
            deferSelectedStepStateRefresh = false;
        }
        RefreshAuthoredRecipeState();
        ToolSearchText = string.Empty;
        lastToolAddMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        OnPropertyChanged(nameof(LastToolAddMilliseconds));
        AppendLog("Teach", $"Added taught step: {tool.Name}.");
        SelectedStepSetupRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshSelectedStepExecutionState()
    {
        selectedStepExecutionOwner.RefreshSelectedStepState();
        if (SelectedPipelineStep is { OutputEnabled: false })
        {
            RefreshSelectedToolWorkspaceProjection();
        }
    }

    private void RefreshFilterStateFromOwner()
    {
        MarkGridPolygonDomainPreviewStaleIfUpstreamChanged();
        MarkDomainMaskPreviewStaleIfUpstreamChanged();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepFilter));
        OnPropertyChanged(nameof(IsSelectedStepHeightThresholdBackgroundRemoval));
        OnPropertyChanged(nameof(FilterKernelSummary));
        OnPropertyChanged(nameof(IsFilterPreviewRunning));
        OnPropertyChanged(nameof(IsHeightThresholdPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsRecipeMutationBlocked));
        OnPropertyChanged(nameof(CurrentFilterPreviewPath));
        OnPropertyChanged(nameof(CurrentFilterPreviewOutputSummary));
        OnPropertyChanged(nameof(FilterExecutionSummary));
        OnPropertyChanged(nameof(FilterOutputHashSummary));
        OnPropertyChanged(nameof(HasCurrentFilterPreview));
        OnPropertyChanged(nameof(IsFilterPreviewStale));
        OnPropertyChanged(nameof(IsFilterPreviewPublished));
        RefreshFilterCommands();
        RefreshHeightDifferenceEdgeExecutionState();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshHeightThresholdExecutionState()
    {
        heightThresholdExecutionOwner.RefreshState();
    }

    private void RefreshHeightThresholdStateFromOwner()
    {
        MarkGridPolygonDomainPreviewStaleIfUpstreamChanged();
        MarkDomainMaskPreviewStaleIfUpstreamChanged();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepHeightThresholdBackgroundRemoval));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsHeightThresholdPreviewRunning));
        OnPropertyChanged(nameof(HeightThresholdExecutionSummary));
        OnPropertyChanged(nameof(HeightThresholdRuleSummary));
        OnPropertyChanged(nameof(HeightThresholdOutputSummary));
        OnPropertyChanged(nameof(HeightThresholdEvidenceSummary));
        OnPropertyChanged(nameof(CurrentHeightThresholdPreviewOutput));
        OnPropertyChanged(nameof(CurrentHeightThresholdPreviewPath));
        OnPropertyChanged(nameof(HasCurrentHeightThresholdPreview));
        OnPropertyChanged(nameof(IsHeightThresholdPreviewStale));
        OnPropertyChanged(nameof(IsHeightThresholdPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshRemoveOutlierStateFromOwner()
    {
        MarkGridPolygonDomainPreviewStaleIfUpstreamChanged();
        MarkDomainMaskPreviewStaleIfUpstreamChanged();
        connectedRegionExecutionOwner.MarkStaleIfUpstreamChanged();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepRemoveOutlierPixels));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsRemoveOutlierPreviewRunning));
        OnPropertyChanged(nameof(RemoveOutlierExecutionSummary));
        OnPropertyChanged(nameof(RemoveOutlierRuleSummary));
        OnPropertyChanged(nameof(RemoveOutlierOutputSummary));
        OnPropertyChanged(nameof(RemoveOutlierMaskSummary));
        OnPropertyChanged(nameof(CurrentRemoveOutlierPreviewOutput));
        OnPropertyChanged(nameof(CurrentRemoveOutlierMask));
        OnPropertyChanged(nameof(CurrentRemoveOutlierPreviewPath));
        OnPropertyChanged(nameof(HasCurrentRemoveOutlierPreview));
        OnPropertyChanged(nameof(IsRemoveOutlierPreviewStale));
        OnPropertyChanged(nameof(IsRemoveOutlierPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshConnectedRegionStateFromOwner()
    {
        domainMaskExecutionOwner.MarkStaleIfUpstreamChanged();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepConnectedRegion));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsRecipeMutationBlocked));
        OnPropertyChanged(nameof(IsConnectedRegionPreviewRunning));
        OnPropertyChanged(nameof(ConnectedRegionExecutionSummary));
        OnPropertyChanged(nameof(CurrentConnectedRegionArtifact));
        OnPropertyChanged(nameof(CurrentConnectedRegionArtifactPath));
        OnPropertyChanged(nameof(HasCurrentConnectedRegionPreview));
        OnPropertyChanged(nameof(IsConnectedRegionPreviewStale));
        OnPropertyChanged(nameof(IsConnectedRegionPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshGridPolygonDomainStateFromOwner()
    {
        MarkGridPolygonDomainPreviewStaleIfUpstreamChanged();
        domainMaskExecutionOwner.MarkStaleIfUpstreamChanged();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepGridPolygonDomain));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsRecipeMutationBlocked));
        OnPropertyChanged(nameof(IsGridPolygonDomainPreviewRunning));
        OnPropertyChanged(nameof(GridPolygonDomainExecutionSummary));
        OnPropertyChanged(nameof(GridPolygonDomainOutputSummary));
        OnPropertyChanged(nameof(CurrentGridPolygonDomainArtifact));
        OnPropertyChanged(nameof(CurrentGridPolygonDomainArtifactPath));
        OnPropertyChanged(nameof(HasCurrentGridPolygonDomainPreview));
        OnPropertyChanged(nameof(IsGridPolygonDomainPreviewStale));
        OnPropertyChanged(nameof(IsGridPolygonDomainPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshDomainMaskStateFromOwner()
    {
        domainMaskExecutionOwner.MarkStaleIfUpstreamChanged();
        MarkGridPolygonDomainPreviewStaleIfUpstreamChanged();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepDomainMask));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsRecipeMutationBlocked));
        OnPropertyChanged(nameof(IsDomainMaskPreviewRunning));
        OnPropertyChanged(nameof(DomainMaskExecutionSummary));
        OnPropertyChanged(nameof(DomainMaskOutputSummary));
        OnPropertyChanged(nameof(CurrentDomainMaskPreviewOutput));
        OnPropertyChanged(nameof(CurrentDomainMaskPreviewPath));
        OnPropertyChanged(nameof(HasCurrentDomainMaskPreview));
        OnPropertyChanged(nameof(IsDomainMaskPreviewStale));
        OnPropertyChanged(nameof(IsDomainMaskPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshEditableRegionStateFromOwner()
    {
        MarkEditableRegionPreviewStaleIfUpstreamChanged();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepEditableRegion));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsRecipeMutationBlocked));
        OnPropertyChanged(nameof(IsEditableRegionPreviewRunning));
        OnPropertyChanged(nameof(EditableRegionExecutionSummary));
        OnPropertyChanged(nameof(CurrentEditableRegionArtifact));
        OnPropertyChanged(nameof(CurrentEditableRegionArtifactPath));
        OnPropertyChanged(nameof(HasCurrentEditableRegionPreview));
        OnPropertyChanged(nameof(IsEditableRegionPreviewStale));
        OnPropertyChanged(nameof(IsEditableRegionPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshLevelSurfaceStateFromOwner()
    {
        MarkGridPolygonDomainPreviewStaleIfUpstreamChanged();
        MarkDomainMaskPreviewStaleIfUpstreamChanged();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepLevelSurface));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsLevelSurfacePreviewRunning));
        OnPropertyChanged(nameof(LevelSurfaceExecutionSummary));
        OnPropertyChanged(nameof(LevelSurfaceReferenceSummary));
        OnPropertyChanged(nameof(LevelSurfaceTransformSummary));
        OnPropertyChanged(nameof(LevelSurfaceFrameSummary));
        OnPropertyChanged(nameof(LevelSurfaceFrameChainSummary));
        OnPropertyChanged(nameof(LevelSurfaceResidualSummary));
        OnPropertyChanged(nameof(LevelSurfaceOutputSummary));
        OnPropertyChanged(nameof(CurrentLevelSurfacePreviewOutput));
        OnPropertyChanged(nameof(CurrentLevelSurfaceTransform));
        OnPropertyChanged(nameof(CurrentLevelSurfaceLevelFrame));
        OnPropertyChanged(nameof(CurrentLevelSurfaceQualityEvidence));
        OnPropertyChanged(nameof(CurrentLevelSurfaceFrameChain));
        OnPropertyChanged(nameof(CurrentLevelSurfaceOutputSlopeX));
        OnPropertyChanged(nameof(CurrentLevelSurfaceOutputSlopeZ));
        OnPropertyChanged(nameof(CurrentLevelSurfacePreviewPath));
        OnPropertyChanged(nameof(HasCurrentLevelSurfacePreview));
        OnPropertyChanged(nameof(IsLevelSurfacePreviewStale));
        OnPropertyChanged(nameof(IsLevelSurfacePreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshRoiCropStateFromOwner()
    {
        MarkGridPolygonDomainPreviewStaleIfUpstreamChanged();
        MarkDomainMaskPreviewStaleIfUpstreamChanged();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepRoiCrop));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsRoiCropPreviewRunning));
        OnPropertyChanged(nameof(RoiCropExecutionSummary));
        OnPropertyChanged(nameof(RoiCropRegionSummary));
        OnPropertyChanged(nameof(RoiCropOutputSummary));
        OnPropertyChanged(nameof(CurrentRoiCropPreviewOutput));
        OnPropertyChanged(nameof(CurrentRoiCropRegion));
        OnPropertyChanged(nameof(CurrentRoiCropPreviewPath));
        OnPropertyChanged(nameof(HasCurrentRoiCropPreview));
        OnPropertyChanged(nameof(IsRoiCropPreviewStale));
        OnPropertyChanged(nameof(IsRoiCropPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshDatumPlaneDeviationStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepDatumPlaneDeviation));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsDatumPlaneDeviationPreviewRunning));
        OnPropertyChanged(nameof(DatumPlaneDeviationExecutionSummary));
        OnPropertyChanged(nameof(DatumPlaneDeviationOutputHashSummary));
        OnPropertyChanged(nameof(DatumPlaneDeviationUpstreamSummary));
        OnPropertyChanged(nameof(DatumPlaneDeviationEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentDatumPlaneDeviationPreview));
        OnPropertyChanged(nameof(IsDatumPlaneDeviationPreviewStale));
        OnPropertyChanged(nameof(IsDatumPlaneDeviationPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshThreePointPlaneStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepThreePointPlane));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsThreePointPlanePreviewRunning));
        OnPropertyChanged(nameof(ThreePointPlaneExecutionSummary));
        OnPropertyChanged(nameof(ThreePointPlaneOutputHashSummary));
        OnPropertyChanged(nameof(ThreePointPlaneSelectionSummary));
        OnPropertyChanged(nameof(CurrentThreePointPlaneOutput));
        OnPropertyChanged(nameof(HasCurrentThreePointPlanePreview));
        OnPropertyChanged(nameof(IsThreePointPlanePreviewStale));
        OnPropertyChanged(nameof(IsThreePointPlanePreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshTwoPointLineStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepTwoPointLine));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsTwoPointLinePreviewRunning));
        OnPropertyChanged(nameof(TwoPointLineExecutionSummary));
        OnPropertyChanged(nameof(TwoPointLineOutputHashSummary));
        OnPropertyChanged(nameof(TwoPointLineSelectionSummary));
        OnPropertyChanged(nameof(CurrentTwoPointLineOutput));
        OnPropertyChanged(nameof(HasCurrentTwoPointLinePreview));
        OnPropertyChanged(nameof(IsTwoPointLinePreviewStale));
        OnPropertyChanged(nameof(IsTwoPointLinePreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshHeightDifferenceEdgeStateFromOwner()
    {
        MarkPhysicalEdgeGapFlushPreviewStaleIfNeeded();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepHeightDifferenceEdge));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsEdgePreviewRunning));
        OnPropertyChanged(nameof(SelectedHeightDifferenceEdgeComparisonAxis));
        OnPropertyChanged(nameof(SelectedHeightDifferenceEdgePolarity));
        OnPropertyChanged(nameof(HeightDifferenceEdgeMinimumDelta));
        OnPropertyChanged(nameof(HeightDifferenceEdgeExpectedOrientation));
        OnPropertyChanged(nameof(HeightDifferenceEdgeUpstreamSummary));
        OnPropertyChanged(nameof(HeightDifferenceEdgeBandSummary));
        OnPropertyChanged(nameof(HeightDifferenceEdgeExecutionSummary));
        OnPropertyChanged(nameof(HeightDifferenceEdgeOutputHashSummary));
        OnPropertyChanged(nameof(CurrentHeightDifferenceEdgeOutput));
        OnPropertyChanged(nameof(HasCurrentEdgePreview));
        OnPropertyChanged(nameof(IsEdgePreviewStale));
        OnPropertyChanged(nameof(IsEdgePreviewPublished));
        RefreshFilterCommands();
        RefreshHeightDifferenceEdgeCommands();
        RefreshLineFitCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshLineIntersectionStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepLineIntersection));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsLineIntersectionPreviewRunning));
        OnPropertyChanged(nameof(LineIntersectionExecutionSummary));
        OnPropertyChanged(nameof(LineIntersectionOutputHashSummary));
        OnPropertyChanged(nameof(LineIntersectionUpstreamSummary));
        OnPropertyChanged(nameof(LineIntersectionEvidenceSummary));
        OnPropertyChanged(nameof(CurrentLineIntersectionOutput));
        OnPropertyChanged(nameof(HasCurrentLineIntersectionPreview));
        OnPropertyChanged(nameof(IsLineIntersectionPreviewStale));
        OnPropertyChanged(nameof(IsLineIntersectionPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshLandmarkCorrespondenceStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepLandmarkCorrespondence));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsLandmarkCorrespondencePreviewRunning));
        OnPropertyChanged(nameof(LandmarkCorrespondenceExecutionSummary));
        OnPropertyChanged(nameof(LandmarkCorrespondenceOutputHashSummary));
        OnPropertyChanged(nameof(LandmarkCorrespondenceUpstreamSummary));
        OnPropertyChanged(nameof(LandmarkCorrespondenceEvidenceSummary));
        OnPropertyChanged(nameof(CurrentLandmarkCorrespondenceOutput));
        OnPropertyChanged(nameof(HasCurrentLandmarkCorrespondencePreview));
        OnPropertyChanged(nameof(IsLandmarkCorrespondencePreviewStale));
        OnPropertyChanged(nameof(IsLandmarkCorrespondencePreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshLineFitStateFromOwner()
    {
        MarkPhysicalEdgeGapFlushPreviewStaleIfNeeded();
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepLineFit));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsLineFitPreviewRunning));
        OnPropertyChanged(nameof(LineFitExecutionSummary));
        OnPropertyChanged(nameof(LineFitOutputHashSummary));
        OnPropertyChanged(nameof(LineFitUpstreamSummary));
        OnPropertyChanged(nameof(LineFitSelectedDiagnosticSummary));
        OnPropertyChanged(nameof(LineFitPointDiagnostics));
        OnPropertyChanged(nameof(LineFitResidualPlotPoints));
        OnPropertyChanged(nameof(SelectedLineFitDiagnostic));
        OnPropertyChanged(nameof(CurrentLineFitOutput));
        OnPropertyChanged(nameof(HasCurrentLineFitPreview));
        OnPropertyChanged(nameof(IsLineFitPreviewStale));
        OnPropertyChanged(nameof(IsLineFitPreviewPublished));
        RefreshLineFitCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshLineFitDiagnosticStateFromOwner()
    {
        OnPropertyChanged(nameof(SelectedLineFitDiagnostic));
        OnPropertyChanged(nameof(LineFitSelectedDiagnosticSummary));
    }

    private void RefreshRegridHeightFieldStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepRegridHeightField));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsRegridHeightFieldPreviewRunning));
        OnPropertyChanged(nameof(RegridHeightFieldExecutionSummary));
        OnPropertyChanged(nameof(RegridHeightFieldOutputHashSummary));
        OnPropertyChanged(nameof(RegridHeightFieldUpstreamSummary));
        OnPropertyChanged(nameof(RegridHeightFieldEvidenceSummary));
        OnPropertyChanged(nameof(CurrentRegridHeightFieldOutput));
        OnPropertyChanged(nameof(HasCurrentRegridHeightFieldPreview));
        OnPropertyChanged(nameof(IsRegridHeightFieldPreviewStale));
        OnPropertyChanged(nameof(IsRegridHeightFieldPreviewPublished));
        OnPropertyChanged(nameof(AlignmentStatusSummary));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshXyzAffineStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepXYZAffineSolve));
        OnPropertyChanged(nameof(IsAffineSolvePreviewRunning));
        OnPropertyChanged(nameof(HasCurrentAffineSolvePreview));
        OnPropertyChanged(nameof(IsAffineSolvePreviewStale));
        OnPropertyChanged(nameof(IsAffineSolvePreviewPublished));
        OnPropertyChanged(nameof(CurrentAffineSolveOutput));
        OnPropertyChanged(nameof(AffineSolveExecutionSummary));
        OnPropertyChanged(nameof(AffineSolveOutputHashSummary));
        OnPropertyChanged(nameof(AffineSolveUpstreamSummary));
        OnPropertyChanged(nameof(AffineSolveEvidenceSummary));
        OnPropertyChanged(nameof(AffineSolveMatrixSummary));
        OnPropertyChanged(nameof(IsSelectedStepXYZAffineApply));
        OnPropertyChanged(nameof(IsAffineApplyPreviewRunning));
        OnPropertyChanged(nameof(HasCurrentAffineApplyPreview));
        OnPropertyChanged(nameof(IsAffineApplyPreviewStale));
        OnPropertyChanged(nameof(IsAffineApplyPreviewPublished));
        OnPropertyChanged(nameof(CurrentAffineApplyOutput));
        OnPropertyChanged(nameof(AffineApplyExecutionSummary));
        OnPropertyChanged(nameof(AffineApplyOutputHashSummary));
        OnPropertyChanged(nameof(AffineApplyUpstreamSummary));
        OnPropertyChanged(nameof(AffineApplyEvidenceSummary));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(AlignmentStatusSummary));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void UpdateMeasurementCompletenessPresentation(
        ToolRecipeHeightMeasurementOutput? output)
    {
        HeightImageViewer.SetCompletenessCellOverlays(
            output?.CompletenessGrid?.CellOverlays ?? []);
        if (output is not null)
        {
            completenessReviewOwner.ClearSelection();
        }

        RefreshCompletenessCellReview();
    }

    private void RefreshHeightMeasurementStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsMeasurementPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(MeasurementExecutionSummary));
        OnPropertyChanged(nameof(MeasurementEvidenceSummary));
        OnPropertyChanged(nameof(CurrentMeasurementOutput));
        OnPropertyChanged(nameof(HasCurrentMeasurementPreview));
        OnPropertyChanged(nameof(IsMeasurementPreviewPublished));
        RefreshCompletenessCellReview();
        RefreshMeasurementCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void RequestSelectedStepRemoval()
    {
        var request = CreateSelectedStepRemovalRequest();
        if (request is null)
        {
            return;
        }

        RemoveSelectedStepRequested?.Invoke(this, request);
    }

    private bool CanBeginFlowConnection(object? parameter) =>
        CanEditFlowConnection
        && parameter is string outputId
        && IsFlowOutputAvailable(outputId);

    private bool CanConnectFlowOutput(object? parameter) =>
        CanEditFlowConnection
        && HasPendingFlowConnection
        && (parameter is ToolWorkbenchPipelineStepItem step && PipelineSteps.Contains(step)
            || parameter is ToolWorkbenchFlowInputItem port && PipelineSteps.Contains(port.Step) && port.Index >= 0 && port.Index < port.Step.FlowInputs.Count);

    private void BeginFlowConnection(object? parameter)
    {
        if (parameter is not string outputId || !CanBeginFlowConnection(outputId))
        {
            return;
        }

        if (string.Equals(pendingFlowOutputEntityId, outputId, StringComparison.OrdinalIgnoreCase))
        {
            CancelFlowConnection();
            return;
        }

        pendingFlowOutputEntityId = outputId;
        flowConnectionFeedback = FlowConnectionFeedback.SelectInput;
        RefreshFlowConnectionCommands();
    }

    private void ConnectFlowOutput(object? parameter)
    {
        var step = parameter is ToolWorkbenchFlowInputItem port ? port.Step : parameter as ToolWorkbenchPipelineStepItem;
        var inputIndex = parameter is ToolWorkbenchFlowInputItem selectedPort ? selectedPort.Index : 0;
        if (step is null || pendingFlowOutputEntityId is not { } outputId || !CanConnectFlowOutput(parameter))
        {
            return;
        }

        var existingInputs = step.InputEntityIds.ToArray();
        if (inputIndex < existingInputs.Length && string.Equals(existingInputs[inputIndex], outputId, StringComparison.OrdinalIgnoreCase))
        {
            pendingFlowOutputEntityId = null;
            flowConnectionFeedback = FlowConnectionFeedback.AlreadyConnected;
            RefreshFlowConnectionCommands();
            return;
        }

        // Input slots are ordered by the tool contract; a click changes only that slot.
        var candidateInputs = existingInputs.ToList();
        while (candidateInputs.Count <= inputIndex) candidateInputs.Add(string.Empty);
        candidateInputs[inputIndex] = outputId;
        var document = CreateDocument();
        var targetIndex = PipelineSteps.IndexOf(step);
        if (targetIndex < 0 || targetIndex >= document.Steps.Count)
        {
            return;
        }

        if (!IsFlowInputCompatible(document, targetIndex, candidateInputs))
        {
            flowConnectionFeedback = FlowConnectionFeedback.Rejected;
            RefreshFlowConnectionCommands();
            return;
        }

        step.InputEntityIdsText = string.Join("; ", candidateInputs);
        RememberAppliedStepChange(step, document, Localize("입력 연결", "Input connection") + $" {string.Join("; ", existingInputs)} → {string.Join("; ", candidateInputs)}");
        SelectedPipelineStep = step;
        pendingFlowOutputEntityId = null;
        flowConnectionFeedback = FlowConnectionFeedback.Saved;
        RefreshFlowConnectionCommands();
    }

    private bool IsFlowInputCompatible(ToolWorkbenchFlowInputItem port, string outputId)
    {
        var index = PipelineSteps.IndexOf(port.Step);
        if (index < 0 || port.Index < 0 || port.Index >= port.Step.FlowInputs.Count) return false;
        var inputs = port.Step.InputEntityIds.ToList();
        while (inputs.Count <= port.Index) inputs.Add(string.Empty);
        inputs[port.Index] = outputId;
        return IsFlowInputCompatible(CreateDocument(), index, inputs);
    }

    private static bool IsFlowInputCompatible(ToolRecipeDocument document, int stepIndex, IReadOnlyList<string> inputs)
    {
        var steps = document.Steps.ToArray();
        steps[stepIndex] = steps[stepIndex] with { InputEntityIds = inputs.ToArray() };
        return !HasAdditionalValidationErrors(ToolRecipeValidator.Validate(document).Errors,
            ToolRecipeValidator.Validate(document with { Steps = steps }).Errors);
    }

    private void CancelFlowConnection()
    {
        if (!HasPendingFlowConnection)
        {
            return;
        }

        pendingFlowOutputEntityId = null;
        flowConnectionFeedback = FlowConnectionFeedback.Cancelled;
        RefreshFlowConnectionCommands();
    }

    private bool IsFlowOutputAvailable(string outputId)
    {
        if (string.Equals(outputId, Source.Id, StringComparison.OrdinalIgnoreCase))
        {
            return IsSourceReadyForRecipe;
        }

        if (Selections.Any(selection => string.Equals(selection.Id, outputId, StringComparison.OrdinalIgnoreCase) && IsSelectionCurrent(selection))) return true;

        var producers = PipelineSteps
            .Where(step => string.Equals(step.OutputEntityId, outputId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return producers.Length == 1 && producers[0].OutputEnabled;
    }

    private string GetFlowOutputName(string outputId) =>
        string.Equals(outputId, Source.Id, StringComparison.OrdinalIgnoreCase)
            ? Source.Name
            : PipelineSteps.FirstOrDefault(step =>
                string.Equals(step.OutputEntityId, outputId, StringComparison.OrdinalIgnoreCase))?.ToolName
                ?? outputId;

    private static bool HasAdditionalValidationErrors(
        IReadOnlyList<string> baselineErrors,
        IReadOnlyList<string> candidateErrors)
    {
        var remaining = baselineErrors
            .GroupBy(error => error, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        foreach (var error in candidateErrors)
        {
            if (!remaining.TryGetValue(error, out var count) || count == 0)
            {
                return true;
            }

            remaining[error] = count - 1;
        }

        return false;
    }

    private void RefreshFlowConnectionCommands()
    {
        OnPropertyChanged(nameof(CanEditFlowConnection));
        OnPropertyChanged(nameof(HasPendingFlowConnection));
        OnPropertyChanged(nameof(FlowConnectionMessage));
        beginFlowConnectionCommand?.RaiseCanExecuteChanged();
        connectFlowOutputCommand?.RaiseCanExecuteChanged();
        cancelFlowConnectionCommand?.RaiseCanExecuteChanged();
    }

    internal ToolWorkbenchStepRemovalRequestEventArgs? CreateSelectedStepRemovalRequest()
    {
        if (SelectedPipelineStep is not { } step || IsRecipeMutationBlocked)
        {
            return null;
        }

        var orphanedSelections = GetOrphanedSelections(step);
        return new ToolWorkbenchStepRemovalRequestEventArgs(
            step.Id,
            step.ToolName,
            orphanedSelections.Select(selection => selection.Name).ToArray());
    }

    internal bool ConfirmSelectedStepRemoval(string stepId)
    {
        if (IsRecipeMutationBlocked
            || SelectedPipelineStep is not { } step
            || !string.Equals(step.Id, stepId, StringComparison.Ordinal))
        {
            return false;
        }

        var orphanedSelections = GetOrphanedSelections(step);
        lastRemovedStep = (CreateDocument().Steps.First(item => string.Equals(item.Id, step.Id.Trim(), StringComparison.Ordinal)), step.Tool, PipelineSteps.IndexOf(step), orphanedSelections);
        selectedStepExecutionOwner.ForgetCompletedResult(step);
        ForgetRemovedStepOutput(step);
        UnsubscribeStep(step);
        PipelineSteps.Remove(step);
        teachingSelectionStoreOwner.RemoveRange(orphanedSelections);
        SelectedPipelineStep = PipelineSteps.LastOrDefault();
        RefreshAuthoredRecipeState();
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        AppendLog("Teach", $"Removed taught step: {step.ToolName}.");
        return true;
    }

    public bool TryRestoreLastRemovedStep(out string message)
    {
        if (!CanRestoreLastRemovedStep || lastRemovedStep is not { } removed)
        {
            message = Localize("복구할 도구가 없거나 현재 편집을 마쳐야 합니다.", "No tool can be restored, or the current edit must finish first.");
            return false;
        }

        var identities = new HashSet<string>(PipelineSteps.SelectMany(step => new[] { step.Id, step.OutputEntityId })
            .Concat(Selections.Select(selection => selection.Id)).Concat(References.Select(reference => reference.Id)).Append(Source.Id), StringComparer.OrdinalIgnoreCase);
        if (!identities.Add(removed.Step.Id) || !identities.Add(removed.Step.OutputEntityId) || removed.Selections.Any(selection => !identities.Add(selection.Id)))
        {
            message = Localize("같은 ID를 사용하는 도구·출력·ROI가 있습니다. 충돌을 해소한 뒤 복구하세요.", "A tool, output, or ROI uses the same ID. Resolve the collision before restoring.");
            SetParameterDraftStatus(message);
            return false;
        }

        var snapshot = removed.Step;
        var restored = new ToolWorkbenchPipelineStepItem(snapshot.Id, removed.Tool, string.Join("; ", snapshot.InputEntityIds), snapshot.OutputEntityId,
            snapshot.Parameters, snapshot.ToolName, snapshot.DualRoiRouting, snapshot.OutputEnabled, acceptanceLimitsConfigured: snapshot.AcceptanceLimitsConfigured, group: snapshot.Group);
        foreach (var selection in removed.Selections) teachingSelectionStoreOwner.Upsert(selection);
        SubscribeStep(restored);
        PipelineSteps.Insert(Math.Min(removed.Index, PipelineSteps.Count), restored);
        EndStepRemovalHistory();
        SelectedPipelineStep = restored;
        RefreshAuthoredRecipeState();
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        message = Localize("도구 설정과 입력을 복구했습니다. 결과를 확인하려면 다시 실행하세요.", "Tool settings and inputs were restored. Run again to obtain results.");
        SetParameterDraftStatus(message);
        AppendLog("Teach", $"Restored removed step without execution: {restored.ToolName}.");
        return true;
    }

    private void EndStepRemovalHistory()
    {
        lastRemovedStep = null;
        restoreLastRemovedStepCommand?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanRestoreLastRemovedStep));
    }

    private void ForgetRemovedStepOutput(ToolWorkbenchPipelineStepItem step)
    {
        const string summary = "Tool removed; Run again after restoring its settings.";
        bool Matches(string? outputId) => string.Equals(outputId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase);
        heightMeasurementExecutionOwner.ForgetCompletedResult(step);
        filterExecutionOwner.ForgetOutput(step.OutputEntityId);
        if (Matches(CurrentHeightThresholdPreviewOutput?.EntityId)) ClearHeightThresholdPreview(summary);
        if (Matches(CurrentSignedDeltaPreviewOutput?.OutputEntityId)) ClearSignedDeltaBackgroundSubtractionPreview(summary);
        if (Matches(CurrentRemoveOutlierPreviewOutput?.EntityId)) ClearRemoveOutlierPreview(summary);
        if (Matches(CurrentConnectedRegionArtifact?.ArtifactId)) ClearConnectedRegionPreview(summary);
        if (Matches(CurrentGridPolygonDomainArtifact?.ArtifactId)) ClearGridPolygonDomainPreview(summary);
        if (Matches(CurrentDomainMaskPreviewOutput?.EntityId)) ClearDomainMaskPreview(summary);
        if (Matches(CurrentEditableRegionArtifact?.ArtifactId)) ClearEditableRegionPreview(summary);
        if (Matches(CurrentLevelSurfacePreviewOutput?.EntityId)) ClearLevelSurfacePreview(summary);
        if (Matches(CurrentRoiCropPreviewOutput?.EntityId)) ClearRoiCropPreview(summary);
        twoPointLineExecutionOwner.ForgetOutput(step.OutputEntityId);
        heightDifferenceEdgeExecutionOwner.ForgetOutput(step.OutputEntityId);
        lineFitExecutionOwner.ForgetOutput(step.OutputEntityId);
        if (Matches(CurrentPhysicalEdgeGapFlushOutput?.OutputEntityId)) ClearPhysicalEdgeGapFlushPreview(summary);
        lineIntersectionExecutionOwner.ForgetOutput(step.OutputEntityId);
        if (Matches(CurrentLandmarkCorrespondenceOutput?.OutputEntityId)) ClearLandmarkCorrespondencePreview(summary);
        threePointPlaneExecutionOwner.ForgetOutput(step.OutputEntityId);
        datumPlaneDeviationExecutionOwner.ForgetOutput(step.OutputEntityId);
        if (Matches(CurrentAffineSolveOutput?.OutputEntityId)) ClearXYZAffineSolvePreview(summary);
        if (Matches(CurrentAffineApplyOutput?.OutputEntityId)) ClearXYZAffineApplyPreview(summary);
        if (Matches(CurrentRegridHeightFieldOutput?.OutputEntityId)) ClearRegridHeightFieldPreview(summary);
        if (step.ToolId == "surface-match") surfaceMatchExperiment.Clear();
    }

    private ToolRecipeSelection[] GetOrphanedSelections(ToolWorkbenchPipelineStepItem step)
    {
        var routedSelectionIds = step.InputEntityIds
            .Where(input => Selections.Any(selection => string.Equals(selection.Id, input, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Selections
            .Where(selection => routedSelectionIds.Contains(selection.Id, StringComparer.OrdinalIgnoreCase))
            .Where(selection => !PipelineSteps
                .Where(item => !ReferenceEquals(item, step))
                .Any(item => item.InputEntityIds.Contains(selection.Id, StringComparer.OrdinalIgnoreCase)))
            .ToArray();
    }

    private void MoveSelectedStep(int offset)
    {
        if (SelectedPipelineStep is null)
        {
            return;
        }

        var index = PipelineSteps.IndexOf(SelectedPipelineStep);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= PipelineSteps.Count)
        {
            return;
        }

        PipelineSteps.Move(index, target);
        RefreshAuthoredRecipeState();
        AppendLog("Teach", $"Moved taught step: {SelectedPipelineStep.ToolName}.");
    }

    private bool CanMoveSelectedStep(int offset)
    {
        var index = SelectedPipelineStep is null ? -1 : PipelineSteps.IndexOf(SelectedPipelineStep);
        var target = index + offset;
        return index >= 0 && target >= 0 && target < PipelineSteps.Count;
    }

    private void BeginTeachingSelectionCapture() => teachingSelectionCaptureOwner.Begin();

    private void CancelTeachingSelectionCapture() => teachingSelectionCaptureOwner.Cancel();

    private void BeginAdditionalLevelSurfaceReferenceCapture() =>
        teachingSelectionCaptureOwner.BeginAdditionalLevelSurfaceReference();

    public void UpdateTeachingSelectionCaptureState(
        bool active,
        int capturedPointCount,
        int requiredPointCount,
        bool canApply,
        string message)
        => teachingSelectionCaptureOwner.UpdateState(
            active,
            capturedPointCount,
            requiredPointCount,
            canApply,
            message);

    public void UpdateTeachingGridRectangleDraft(ToolRecipeGridRectangle? rectangle)
    {
        teachingSelectionCaptureOwner.UpdateGridRectangleDraft(rectangle);
        RefreshHeightImageRoiProjection();
    }

    public void UpdateTeachingGridCircleDraft(ToolRecipeGridCircle? circle)
        => teachingSelectionCaptureOwner.UpdateGridCircleDraft(circle);

    public void UpdateTeachingGridPolygonDraft(ToolRecipeGridPolygon? polygon)
        => teachingSelectionCaptureOwner.UpdateGridPolygonDraft(polygon);

    public void RejectTeachingSelectionCapture(string message)
        => teachingSelectionCaptureOwner.Reject(message);

    public bool TryApplyCapturedTeachingSelection(ToolRecipeSelection? selection, out string message)
        => teachingSelectionCaptureOwner.TryApplyCapturedSelection(selection, out message);

    public IReadOnlyList<ToolRecipeSelection> GetCurrentAppliedTeachingSelections() =>
        teachingSelectionStoreOwner.GetCurrent();

    public bool SelectPipelineStep(string stepId)
    {
        var step = PipelineSteps.FirstOrDefault(item =>
            string.Equals(item.Id, stepId, StringComparison.OrdinalIgnoreCase));
        if (step is null)
        {
            return false;
        }

        SelectedPipelineStep = step;
        return true;
    }

    public bool SelectFirstPipelineStepForTool(string toolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        var step = PipelineSteps.FirstOrDefault(item =>
            string.Equals(item.ToolId, toolId, StringComparison.OrdinalIgnoreCase));
        if (step is null)
        {
            return false;
        }

        SelectedPipelineStep = step;
        return true;
    }

    public bool SelectPipelineStepForSelection(string selectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionId);
        var step = PipelineSteps.FirstOrDefault(item =>
            item.InputEntityIds.Contains(selectionId, StringComparer.OrdinalIgnoreCase));
        if (step is null)
        {
            return false;
        }

        SelectedPipelineStep = step;
        if (IsSelectedStepDualRoiMeasurement && !IsSelectedStepCompletenessGridUsingEditableRegion)
        {
            SetPlaneFlatnessTeachingRole(
                ToolWorkbenchTeachingSelectionPolicy.IsMeasurementRoleSelection(
                    step,
                    selectionId,
                    IsSelectedStepThickness,
                    RecipeSchemaVersion));
        }
        return true;
    }

    private void RequestSelectedTeachingSelectionRemoval()
    {
        if (IsRecipeMutationBlocked)
        {
            AppendLog(
                "Warning",
                $"Selection delete rejected | role={GetActiveTeachingRoleName()} | reason=recipe mutation is active.");
            return;
        }

        if (SelectedStepTeachingSelection is not { } selection)
        {
            AppendLog("Warning", $"Selection delete rejected | role={GetActiveTeachingRoleName()} | reason=no applied selection in the active role.");
            return;
        }

        var affectedStepNames = PipelineSteps
            .Where(step => step.InputEntityIds.Contains(selection.Id, StringComparer.OrdinalIgnoreCase))
            .Select(step => step.ToolName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        RemoveSelectedTeachingSelectionRequested?.Invoke(
            this,
            new ToolWorkbenchTeachingSelectionRemovalRequestEventArgs(
                selection,
                ToolWorkbenchTeachingSelectionPolicy.FormatSelection(selection),
                affectedStepNames));
    }

    internal bool ConfirmSelectedTeachingSelectionRemoval(
        ToolWorkbenchTeachingSelectionRemovalRequestEventArgs request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsRecipeMutationBlocked
            || SelectedStepTeachingSelection is not { } selected
            || !string.Equals(selected.Id, request.Selection.Id, StringComparison.OrdinalIgnoreCase))
        {
            AppendLog(
                "Warning",
                $"Selection delete rejected | selection={request.Selection.Id} | reason=recipe state or active selection changed.");
            return false;
        }

        var current = Selections.FirstOrDefault(selection =>
            string.Equals(selection.Id, request.Selection.Id, StringComparison.OrdinalIgnoreCase));
        if (current is null || !Equals(current, request.Selection))
        {
            AppendLog(
                "Warning",
                $"Selection delete rejected | selection={request.Selection.Id} | reason=selection changed while confirmation was open.");
            return false;
        }

        RemoveTeachingSelection(current);
        return true;
    }

    private void RemoveTeachingSelection(ToolRecipeSelection selection)
    {
        var before = CreateDocument();
        var step = SelectedPipelineStep;
        var wasDualRoi = IsSelectedStepDualRoiMeasurement && !IsSelectedStepCompletenessGridUsingEditableRegion;
        var removedMeasurementRole = wasDualRoi && isPlaneFlatnessMeasurementRole;
        var otherRoleSelection = removedMeasurementRole
            ? PlaneFlatnessReferenceSelection
            : PlaneFlatnessMeasurementSelection;
        var referenceSelectionId = PlaneFlatnessReferenceSelection?.Id;
        var measurementSelectionId = PlaneFlatnessMeasurementSelection?.Id;
        MarkGridPolygonDomainPreviewStaleIfNeeded(selection);
        MarkDomainMaskPreviewStaleIfUpstreamChanged();
        MutateRecipe(() =>
        {
            teachingSelectionStoreOwner.Remove(selection);
            foreach (var step in PipelineSteps)
            {
                RemoveInputEntity(step, selection.Id);
            }

            if (wasDualRoi && step is not null)
            {
                step.DualRoiRouting = new ToolRecipeDualRoiRouting(
                    removedMeasurementRole ? referenceSelectionId : null,
                    removedMeasurementRole ? null : measurementSelectionId);
                if (removedMeasurementRole || measurementSelectionId is null)
                {
                    PromoteRecipeSchemaForSelection();
                }
            }
        });
        if (wasDualRoi)
        {
            SetPlaneFlatnessTeachingRole(removedMeasurementRole && otherRoleSelection is not null);
        }
        MarkHeightDifferenceEdgePreviewStaleIfNeeded();
        MarkMeasurementPreviewStaleIfNeeded();
        RefreshTeachingSelectionContext();
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        if (step is not null) RememberAppliedStepChange(step, before, Localize("영역 삭제", "Region removed") + " · " + AppliedSelectionSummary(selection));
        AppendLog(
            "Teach",
            $"Selection deleted | step={step?.Id ?? "(none)"} | role={(removedMeasurementRole ? "measurement" : wasDualRoi ? "reference" : "selection")} | selection={selection.Id} | geometry={ToolWorkbenchTeachingSelectionPolicy.FormatSelectionGeometryForLog(selection)} | route={string.Join(';', step?.InputEntityIds ?? [])}.");
    }

    private void UseExistingTeachingSelection()
    {
        if (SelectedCompatibleSelection is { } selection)
        {
            UseExistingTeachingSelection(selection);
        }
    }

    private void UseExistingTeachingSelection(ToolRecipeSelection selection)
    {
        if (SelectedPipelineStep is null
            || !ToolWorkbenchTeachingSelectionPolicy.MatchesRequirement(
                selection,
                SelectedStepSelectionRequirement))
        {
            return;
        }

        var before = CreateDocument();
        MutateRecipe(() =>
        {
            var isDualRoi = IsSelectedStepDualRoiMeasurement;
            if (IsSelectedStepDualRoiMeasurement)
            {
                RoutePlaneFlatnessRoleSelection(SelectedPipelineStep, selection.Id);
            }
            else
            {
                AddInputEntity(SelectedPipelineStep, selection.Id);
            }
            if (isDualRoi)
            {
                PromoteRecipeSchemaForSelection();
            }
        });
        MarkHeightDifferenceEdgePreviewStaleIfNeeded();
        MarkGridPolygonDomainPreviewStaleIfNeeded(selection);
        MarkDomainMaskPreviewStaleIfUpstreamChanged();
        RefreshTeachingSelectionContext();
        AdvancePlaneFlatnessTeachingRole();
        RememberAppliedStepChange(SelectedPipelineStep, before, Localize("기존 영역 연결", "Existing region linked") + " · " + AppliedSelectionSummary(selection));
        AppendLog("Teach", $"Routed existing selection '{selection.Name}' to {SelectedPipelineStep.ToolName}.");
    }

    private void PersistSelectionForSelectedStep(ToolRecipeSelection selection)
    {
        if (SelectedPipelineStep is null)
        {
            return;
        }

        var before = CreateDocument();
        var previous = teachingSelectionStoreOwner.Find(selection.Id);
        MutateRecipe(() =>
        {
            teachingSelectionStoreOwner.Upsert(selection);

            if (string.Equals(SelectedPipelineStep.ToolId, "landmark-correspondence", StringComparison.Ordinal))
            {
                SelectedPipelineStep.InputEntityIdsText = selection.Id;
            }
            else if (IsSelectedStepDualRoiMeasurement)
            {
                RoutePlaneFlatnessRoleSelection(SelectedPipelineStep, selection.Id);
            }
            else
            {
                AddInputEntity(SelectedPipelineStep, selection.Id);
            }
            PromoteRecipeSchemaForSelection();
        });
        MarkHeightDifferenceEdgePreviewStaleIfNeeded();
        MarkRoiCropPreviewStaleIfNeeded(selection);
        MarkGridPolygonDomainPreviewStaleIfNeeded(selection);
        MarkDomainMaskPreviewStaleIfUpstreamChanged();
        if (string.Equals(
            selection.Kind,
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
            StringComparison.Ordinal))
        {
            MarkLandmarkCorrespondencePreviewStaleIfNeeded();
        }
        RefreshTeachingSelectionContext();
        RememberAppliedStepChange(SelectedPipelineStep, before, Localize("영역", "Region") + " "
            + (previous is null ? Localize("없음", "None") : AppliedSelectionSummary(previous))
            + " → " + AppliedSelectionSummary(selection));
    }

    private void ValidateTeachingRecipe()
    {
        RefreshRecipeState();
        var message = IsTeachingRecipeExecutionReady
            ? "Teaching recipe validation passed."
            : $"Teaching recipe validation found {RecipeSession.Validation.Errors.Count + RecipeSession.SourceBindingErrors.Count} error(s).";
        AppendLog("Validate", message);
        OVLog.Write(LogCategory.UI, IsTeachingRecipeExecutionReady ? LogLevel.Info : LogLevel.Warning, message);
    }

    private void ApplyDocument(ToolRecipeDocument document) =>
        ApplyDocumentCore(
            document,
            markDirty: false,
            selectedStepId: null,
            "Recipe opened; Preview is required.",
            captureOpenedSourceIdentity: true);

    private void ApplyAuthoredDocument(
        ToolRecipeDocument document,
        string selectedStepId,
        string previewStatus) =>
        ApplyDocumentCore(
            document,
            markDirty: true,
            selectedStepId,
            previewStatus,
            captureOpenedSourceIdentity: false);

    private void ApplyDocumentCore(
        ToolRecipeDocument document,
        bool markDirty,
        string? selectedStepId,
        string previewStatus,
        bool captureOpenedSourceIdentity)
    {
        if (HasPendingOrderedRunRecordSave) return;
        workspaceProjectionUpdateDepth++;
        try
        {
            if (captureOpenedSourceIdentity) EndStepRemovalHistory();
            appliedStepParameters.Clear();
            teachingResultComparisons.Clear();
            NotifyStepParameterHistory();
            selectedStepExecutionOwner.ClearCompletedResults();
            ClearFilterPreview(previewStatus);
            ClearHeightThresholdPreview(previewStatus);
            ClearRemoveOutlierPreview(previewStatus);
            ClearConnectedRegionPreview(previewStatus);
            ClearGridPolygonDomainPreview(previewStatus);
            ClearDomainMaskPreview(previewStatus);
            ClearEditableRegionPreview(previewStatus);
            ClearLevelSurfacePreview(previewStatus);
            ClearRoiCropPreview(previewStatus);
            ClearPhysicalEdgeGapFlushPreview(previewStatus);
            if (markDirty)
            {
                ClearMeasurementPreview(previewStatus);
            }
            if (captureOpenedSourceIdentity)
            {
                CaptureOpenedSourceIdentity(document.Source);
            }
            MutateRecipe(() =>
            {
                RecipeSession.SetSchemaVersion(document.SchemaVersion);
                RecipeName = document.Name;
                RecipeSession.SetMetadata(document.Category, document.InspectionTarget, document.Description);
                Source.Id = document.Source.Id;
                Source.Name = document.Source.Name;
                Source.Format = document.Source.Format;
                Source.Unit = document.Source.Unit;
                Source.FrameId = document.Source.FrameId;
                Source.Path = document.Source.Path;
                Source.MeasurementEvidence = document.Source.MeasurementEvidence;
                Source.SensorId = document.Source.SensorId;
                SourceSession.SetSourceAcquisitionProvenance(document.Source.AcquisitionProvenance);

                referenceCatalogOwner.ReplaceAll(document.References);

                teachingSelectionStoreOwner.ReplaceAll(document.Selections ?? []);

                foreach (var existing in PipelineSteps)
                {
                    UnsubscribeStep(existing);
                }

                PipelineSteps.Clear();
                foreach (var sourceStep in document.Steps)
                {
                    var importedOutputContract = ToolRecipePrimaryInputContract.GetProducedContract(sourceStep.ToolId);
                    var definition = Tools.FirstOrDefault(tool => string.Equals(tool.Id, sourceStep.ToolId, StringComparison.OrdinalIgnoreCase))
                        ?? new ToolWorkbenchToolItem(
                            "Imported",
                            sourceStep.ToolName,
                            sourceStep.ToolId,
                            sourceStep.MinimumInputCount,
                            "Imported input",
                            string.IsNullOrWhiteSpace(importedOutputContract)
                                ? "Imported output"
                                : importedOutputContract,
                            "Imported teaching step with no local catalog adapter.",
                            []);
                    var item = new ToolWorkbenchPipelineStepItem(
                        sourceStep.Id,
                        definition,
                        string.Join("; ", sourceStep.InputEntityIds),
                        sourceStep.OutputEntityId,
                        sourceStep.Parameters,
                        sourceStep.ToolName,
                        sourceStep.DualRoiRouting,
                        sourceStep.OutputEnabled,
                        sourceStep.SignedDeltaSidecar,
                        sourceStep.AcceptanceLimitsConfigured,
                        sourceStep.Group);
                    SubscribeStep(item);
                    PipelineSteps.Add(item);
                }

                SelectedPipelineStep = string.IsNullOrWhiteSpace(selectedStepId)
                    ? PipelineSteps.FirstOrDefault()
                    : PipelineSteps.FirstOrDefault(step =>
                        string.Equals(step.Id, selectedStepId, StringComparison.OrdinalIgnoreCase))
                      ?? PipelineSteps.FirstOrDefault();
            }, markDirty);
            SourceQuality.LoadAcquisitionProvenance(SourceSession.SourceAcquisitionProvenance, Source.FrameId);
            OnPropertyChanged(nameof(SourceAcquisitionProvenance));
            OnPropertyChanged(nameof(RecipeSchemaVersion));
        }
        finally
        {
            CompleteWorkspaceProjectionUpdate();
        }
    }

    private ToolRecipeDocument CreateDocument() => CreateDocument(documentPath: null);

    private ToolRecipeDocument CreateDocument(
        string? documentPath,
        ToolRecipeSignedDeltaSidecarReference? signedDeltaSidecar = null)
    {
        var currentSourcePath = Source.Path.Trim();
        var persistedSourcePath = string.IsNullOrWhiteSpace(documentPath)
            ? currentSourcePath
            : PersistSourcePath(documentPath, currentSourcePath);
        var currentSourceByteLength = ResolveCurrentSourceByteLength(currentSourcePath);
        return new ToolRecipeDocument(
            RecipeSession.SchemaVersion,
            RecipeName.Trim(),
            new ToolRecipeSource(
                Source.Id.Trim(),
                Source.Name.Trim(),
                Source.Format.Trim(),
                Source.Unit.Trim(),
                Source.FrameId.Trim(),
                persistedSourcePath,
                currentSourceByteLength,
                SourceSession.SourceBinding?.ContentSha256,
                SourceSession.SourceBinding?.GridWidth,
                SourceSession.SourceBinding?.GridHeight,
                SourceSession.SourceAcquisitionProvenance,
                Source.MeasurementEvidence,
                Source.SensorId),
            referenceCatalogOwner.CreateSnapshot(),
            PipelineSteps.Select(step => new ToolRecipeStep(
                step.Id.Trim(),
                step.ToolId,
                step.ToolName,
                step.MinimumInputCount,
                step.InputEntityIds.ToArray(),
                step.OutputEntityId.Trim(),
                step.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Value)).ToArray(),
                step.DualRoiRouting,
                step.OutputEnabled,
                (signedDeltaSidecar ?? step.SignedDeltaSidecar) is { } persistedSignedDeltaSidecar
                && string.Equals(step.ToolId, C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(step.Id, persistedSignedDeltaSidecar.StepId, StringComparison.OrdinalIgnoreCase)
                    ? persistedSignedDeltaSidecar
                    : null, step.AcceptanceLimitsConfigured, step.Group)).ToArray(),
            string.Equals(RecipeSession.SchemaVersion, ToolRecipeDocument.LegacySchemaVersion, StringComparison.Ordinal)
                && Selections.Count == 0
                    ? null
                    : Selections.ToArray(),
            RecipeSession.Category,
            RecipeSession.InspectionTarget,
            RecipeSession.Description);
    }

    private long? ResolveCurrentSourceByteLength(string currentSourcePath)
    {
        if (SourceSession.SourceBinding is null)
        {
            return null;
        }

        if (File.Exists(currentSourcePath))
        {
            try
            {
                return new FileInfo(currentSourcePath).Length;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                // Preserve the last opened identity when the source disappears
                // or becomes temporarily inaccessible during a draft refresh.
            }
        }

        return SourceSession.OpenedSourceIdentity?.ByteLength;
    }

    private static ToolRecipeDocument ResolveRelativeSourcePath(ToolRecipeDocument document, string documentPath)
    {
        if (string.IsNullOrWhiteSpace(document.Source.Path)
            || !Path.IsPathFullyQualified(document.Source.Path))
        {
            if (string.IsNullOrWhiteSpace(document.Source.Path))
            {
                return document;
            }

            var relativeDocumentDirectory = Path.GetDirectoryName(documentPath)
                ?? Environment.CurrentDirectory;
            return document with
            {
                Source = document.Source with
                {
                    Path = Path.GetFullPath(Path.Combine(relativeDocumentDirectory, document.Source.Path))
                }
            };
        }

        var sourcePath = Path.GetFullPath(document.Source.Path);
        if (File.Exists(sourcePath))
        {
            return document;
        }

        var documentDirectory = Path.GetDirectoryName(documentPath)
            ?? Environment.CurrentDirectory;
        var movedSourcePath = Path.Combine(documentDirectory, Path.GetFileName(sourcePath));
        return HasRecordedSourceIdentity(movedSourcePath, document.Source)
            ? document with { Source = document.Source with { Path = Path.GetFullPath(movedSourcePath) } }
            : document;
    }

    private static string PersistSourcePath(string documentPath, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return sourcePath;
        }

        var sourceFullPath = Path.GetFullPath(sourcePath);
        var documentDirectory = Path.GetDirectoryName(Path.GetFullPath(documentPath))
            ?? Environment.CurrentDirectory;
        var relativePath = Path.GetRelativePath(documentDirectory, sourceFullPath);
        return IsContainedPath(relativePath)
            ? relativePath.Replace(Path.DirectorySeparatorChar, '/')
            : sourceFullPath;
    }

    private static bool IsContainedPath(string relativePath) =>
        !Path.IsPathRooted(relativePath)
        && !string.Equals(relativePath, "..", StringComparison.Ordinal)
        && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

    private static bool HasRecordedSourceIdentity(string path, ToolRecipeSource source)
    {
        if (!File.Exists(path)
            || source.ByteLength is not > 0
            || string.IsNullOrWhiteSpace(source.ContentSha256))
        {
            return false;
        }

        try
        {
            if (new FileInfo(path).Length != source.ByteLength)
            {
                return false;
            }

            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(path);
            return string.Equals(binding.ContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException
            or OverflowException)
        {
            return false;
        }
    }

    private void RefreshRecipeState()
    {
        if (suppressRecipeRefresh)
        {
            return;
        }

        var refreshStarted = Stopwatch.GetTimestamp();
        workspaceProjectionUpdateDepth++;
        try
        {
            suppressRecipeRefresh = true;
            try
            {
                for (var index = 0; index < PipelineSteps.Count; index++)
                {
                    PipelineSteps[index].Order = (index + 1).ToString("00");
                }
            }
            finally
            {
                suppressRecipeRefresh = false;
            }

            var stageStarted = Stopwatch.GetTimestamp();
            RefreshSourceIdentityState();
            var document = CreateDocument();
            RecipeSession.SetValidation(
                ToolRecipeValidator.Validate(document),
                ToolRecipeValidator.ValidateForStorage(document),
                ValidateSelectionSourceBindings());
            lastRecipeValidationMilliseconds = Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds;

            stageStarted = Stopwatch.GetTimestamp();
            ValidationMessages.Clear();
            foreach (var error in RecipeSession.Validation.Errors)
            {
                ValidationMessages.Add(new ToolWorkbenchValidationItem("Error", error));
            }

            foreach (var warning in RecipeSession.Validation.Warnings)
            {
                ValidationMessages.Add(new ToolWorkbenchValidationItem("Warning", warning));
            }

            foreach (var error in RecipeSession.SourceBindingErrors)
            {
                ValidationMessages.Add(new ToolWorkbenchValidationItem("Error", error));
            }

            foreach (var error in SourceSession.SourceIdentityErrors)
            {
                ValidationMessages.Add(new ToolWorkbenchValidationItem("Error", error));
            }

            RebuildEntities();
            RefreshTeachingSelectionContext();
            OrientedBoxEditor.Synchronize(document.Source, SourceSession.SourceBinding, Selections);
            lastRecipeEntityRebuildMilliseconds = Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds;

            stageStarted = Stopwatch.GetTimestamp();
            RefreshFilterExecutionState();
            RefreshHeightThresholdExecutionState();
            RefreshSignedDeltaBackgroundSubtractionExecutionState();
            RefreshRemoveOutlierExecutionState();
            RefreshConnectedRegionExecutionState();
            RefreshGridPolygonDomainExecutionState();
            RefreshEditableRegionExecutionState();
            RefreshLevelSurfaceExecutionState();
            RefreshRoiCropExecutionState();
            RefreshHeightDifferenceEdgeExecutionState();
            RefreshTwoPointLineExecutionState();
            RefreshThreePointPlaneExecutionState();
            RefreshDatumPlaneDeviationExecutionState();
            RefreshLineFitExecutionState();
            RefreshLineIntersectionExecutionState();
            RefreshLandmarkCorrespondenceExecutionState();
            RefreshXYZAffineSolveExecutionState();
            RefreshXYZAffineApplyExecutionState();
            RefreshRegridHeightFieldExecutionState();
            RefreshSurfaceMatchExperimentState();
            RefreshMeasurementExecutionState();
            RefreshAdapterCoverage();
            RefreshValidationSetCapability();
            lastRecipeExecutionStateMilliseconds = Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds;

            stageStarted = Stopwatch.GetTimestamp();
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(CanSaveTeachingRecipe));
            OnPropertyChanged(nameof(IsTeachingRecipeExecutionReady));
            OnPropertyChanged(nameof(IsRecipeSaveBlocked));
            OnPropertyChanged(nameof(CanOpenRecipeInfoEditor));
            OnPropertyChanged(nameof(AvailableInputEntitiesSummary));
            OnPropertyChanged(nameof(PipelineEmptyHint));
            OnPropertyChanged(nameof(IsPipelineEmpty));
            OnPropertyChanged(nameof(HasPipelineSteps));
            OnPropertyChanged(nameof(IsPipelineReviewExpanded));
            OnPropertyChanged(nameof(RecipeStateSummary));
            NotifyFirstRecipeUx();
            OnPropertyChanged(nameof(SourceContextSummary));
            OnPropertyChanged(nameof(AlignmentStatusSummary));
            RefreshThicknessRepeatGroupPresentation();
            ((RelayCommand)SaveTeachingRecipeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SaveTeachingRecipeAsCommand).RaiseCanExecuteChanged();
            openRecipeInfoEditorCommand.RaiseCanExecuteChanged();
            NotifyProposedToolRouteChanged();
            addSelectedToolCommand.RaiseCanExecuteChanged();
            RefreshStepCommands();
            inspectionQueueWorkspace?.RefreshCommands();
            lastRecipeNotificationMilliseconds = Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds;
        }
        finally
        {
            var projectionStarted = Stopwatch.GetTimestamp();
            CompleteWorkspaceProjectionUpdate();
            lastRecipeNotificationMilliseconds += Stopwatch.GetElapsedTime(projectionStarted).TotalMilliseconds;
            lastRecipeRefreshMilliseconds = Stopwatch.GetElapsedTime(refreshStarted).TotalMilliseconds;
        }
    }

    private void RebuildEntities()
    {
        var entities = new List<ToolWorkbenchEntityItem>
        {
            new(
            Source.Id,
            Source.Format,
            string.IsNullOrWhiteSpace(Source.Path) ? "Source required" : "Selected for teaching",
            string.IsNullOrWhiteSpace(Source.Path) ? "Load a C3D source before adding a tool." : $"{Source.Name} | {Source.Unit} | {Source.FrameId}")
        };
        foreach (var reference in References)
        {
            entities.Add(new ToolWorkbenchEntityItem(reference.Id, reference.Kind, "Declared reference", reference.Name));
        }

        foreach (var selection in Selections)
        {
            entities.Add(new ToolWorkbenchEntityItem(
                selection.Id,
                selection.Kind,
                IsSelectionCurrent(selection) ? "Applied teaching selection" : "Stale - recapture required",
                selection.Name));
        }

        foreach (var step in PipelineSteps)
        {
            entities.Add(new ToolWorkbenchEntityItem(step.OutputEntityId, step.OutputContract, step.State, step.ToolName));
        }

        Entities.ReplaceAll(entities);
        RebuildArtifactRegistryAndNavigator();
        RefreshSelectedToolWorkspaceProjection();
    }

    private IEnumerable<ToolWorkbenchEntityItem> EnumerateAvailableEntitiesBefore(ToolWorkbenchPipelineStepItem? selectedStep)
    {
        yield return new ToolWorkbenchEntityItem(Source.Id, Source.Format, "Source", Source.Name);
        foreach (var reference in References)
        {
            yield return new ToolWorkbenchEntityItem(reference.Id, reference.Kind, "Reference", reference.Name);
        }

        foreach (var selection in Selections)
        {
            yield return new ToolWorkbenchEntityItem(selection.Id, selection.Kind, "Teaching selection", selection.Name);
        }

        foreach (var step in PipelineSteps)
        {
            if (ReferenceEquals(step, selectedStep))
            {
                yield break;
            }

            yield return new ToolWorkbenchEntityItem(step.OutputEntityId, step.OutputContract, "Earlier output", step.ToolName);
        }
    }

    private void RefreshTeachingSelectionContext()
    {
        teachingSelectionStoreOwner.RefreshCompatibleSelections(
            SelectedStepSelectionRequirement);
        landmarkCorrespondenceEditorOwner.Refresh();
        teachingSelectionCaptureOwner.RefreshContext();

        RefreshPlaneFlatnessTeachingState();
        OnPropertyChanged(nameof(SelectedStepSelectionRequirement));
        OnPropertyChanged(nameof(IsSelectedStepViewerCaptureSupported));
        OnPropertyChanged(nameof(IsSelectedStepCorrespondence));
        OnPropertyChanged(nameof(SelectedStepSelectionRequirementTitle));
        OnPropertyChanged(nameof(SelectedStepSelectionRequirementSummary));
        OnPropertyChanged(nameof(SelectedStepTeachingSelection));
        OnPropertyChanged(nameof(SelectedStepTeachingSelectionSummary));
        OnPropertyChanged(nameof(IsTeachingGridRectangleEditorVisible));
        OnPropertyChanged(nameof(IsTeachingGridRectangleEditorEnabled));
        OnPropertyChanged(nameof(IsTeachingGridCircleEditorVisible));
        OnPropertyChanged(nameof(IsTeachingGridCircleEditorEnabled));
        OnPropertyChanged(nameof(IsTeachingGridPolygonEditorVisible));
        OnPropertyChanged(nameof(IsTeachingGridPolygonEditorEnabled));
        OnPropertyChanged(nameof(HeightDifferenceEdgeBandSummary));
        OnPropertyChanged(nameof(SelectionCaptureActionText));
        OnPropertyChanged(nameof(ThicknessRoiTeachingDetail));
        OnPropertyChanged(nameof(TeachingSelectionCaptureTitle));
        OnPropertyChanged(nameof(CorrespondenceSelectionSummary));
        teachingSelectionStoreOwner.RefreshCommandStates();
        teachingSelectionCaptureOwner.RefreshCommandStates();
        landmarkCorrespondenceEditorOwner.RefreshCommandStates();
        SynchronizeInspectionWorkspace();
    }

    private IReadOnlyList<string> ValidateSelectionSourceBindings() =>
        teachingSelectionStoreOwner.ValidateSourceBindings();

    private bool IsSelectionCurrent(ToolRecipeSelection selection) =>
        teachingSelectionStoreOwner.IsCurrent(selection);

    private ToolWorkbenchPublishedSelectionBindingState GetPublishedSelectionBindingState(
        ToolRecipeSelection selection)
    {
        if (string.Equals(
            selection.SourceBinding.Format,
            "TransformedHeightField",
            StringComparison.Ordinal))
        {
            if (!TryGetPublishedRegridHeightFieldOutput(
                    selection.SourceBinding.OwnerEntityId ?? string.Empty,
                    out var output)
                || output is null)
            {
                return ToolWorkbenchPublishedSelectionBindingState.Unavailable;
            }

            return ToolRecipeSelectionSourceBindingVerifier
                    .Verify(output, selection.SourceBinding)
                    .IsCurrent
                ? ToolWorkbenchPublishedSelectionBindingState.Current
                : ToolWorkbenchPublishedSelectionBindingState.Stale;
        }

        if (string.Equals(
            selection.SourceBinding.Format,
            "HeightField",
            StringComparison.Ordinal))
        {
            if (!TryGetPublishedRoiCropOutput(
                    selection.SourceBinding.OwnerEntityId ?? string.Empty,
                    out var output)
                || output is null)
            {
                return ToolWorkbenchPublishedSelectionBindingState.Unavailable;
            }

            return ToolRecipeSelectionSourceBindingVerifier
                    .Verify(output, selection.SourceBinding)
                    .IsCurrent
                ? ToolWorkbenchPublishedSelectionBindingState.Current
                : ToolWorkbenchPublishedSelectionBindingState.Stale;
        }

        return ToolWorkbenchPublishedSelectionBindingState.Unavailable;
    }

    private ToolWorkbenchTeachingCaptureContext? CreateTeachingCaptureContext(
        bool additionalLevelSurfaceReference)
    {
        var step = SelectedPipelineStep;
        var requirement = SelectedStepSelectionRequirement;
        if (step is null
            || requirement is not { UsesViewerCapture: true }
            || !CanUseActivePlaneFlatnessRole()
            || string.IsNullOrWhiteSpace(Source.Path)
            || !TryGetSelectionCaptureContext(
                step,
                out var captureBinding,
                out var captureFrameId))
        {
            return null;
        }

        var existing = additionalLevelSurfaceReference
            ? null
            : SelectedStepTeachingSelection;
        var selectionId = existing?.Id ?? CreateSelectionId(step, requirement);
        var selectionName = existing?.Name
            ?? (additionalLevelSurfaceReference
                ? $"Level Surface reference {Math.Max(2, step.InputEntityIds.Count)}"
                : IsSelectedStepDualRoiMeasurement
                    ? CreatePlaneFlatnessSelectionName(step)
                    : $"{step.ToolName} selection");
        return new ToolWorkbenchTeachingCaptureContext(
            step,
            requirement,
            existing,
            selectionId,
            selectionName,
            Source.Id,
            captureFrameId,
            captureBinding,
            GetActiveTeachingRoleName());
    }

    private ToolWorkbenchLandmarkCorrespondenceEditorContext
        CreateLandmarkCorrespondenceEditorContext() =>
        new(
            SelectedPipelineStep,
            IsSelectedStepCorrespondence,
            SelectedStepTeachingSelection,
            SourceSession.SourceBinding,
            Source.Id,
            Source.FrameId,
            SelectedStepSelectionRequirement,
            PipelineSteps);

    private bool TryGetSelectionCaptureContext(
        ToolWorkbenchPipelineStepItem step,
        out ToolRecipeSelectionSourceBinding binding,
        out string frameId)
    {
        if (step.ToolId is "thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume" or "cross-section-dimensions" or "completeness-grid")
        {
            if (step.InputEntityIds.Count == 0)
            {
                binding = null!;
                frameId = string.Empty;
                return false;
            }
            if (!string.Equals(step.InputEntityIds[0], Source.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetPublishedRegridHeightFieldOutput(step.InputEntityIds[0], out var transformed)
                    && transformed is not null)
                {
                    binding = ToolRecipeSelectionSourceBindingVerifier.FromTransformedHeightField(transformed);
                    frameId = transformed.ReferenceFrameId;
                    return true;
                }
                if (TryGetPublishedRoiCropOutput(step.InputEntityIds[0], out var cropped)
                    && cropped is not null)
                {
                    binding = ToolRecipeSelectionSourceBindingVerifier.FromHeightField(cropped);
                    frameId = cropped.FrameId;
                    return true;
                }
                binding = null!;
                frameId = string.Empty;
                return false;
            }
        }

        binding = SourceSession.SourceBinding!;
        frameId = Source.FrameId;
        return SourceSession.SourceBinding is not null;
    }

    private static ToolRecipeSelectionSourceBinding? TryReadSourceBinding(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private void OnTeachingSelectionStoreOwnerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        OnPropertyChanged(args.PropertyName);
        if (args.PropertyName is nameof(
                ToolWorkbenchTeachingSelectionStoreOwner.SelectedCompatibleSelection))
        {
            NotifyPlaneFlatnessTeachingState();
        }
    }

    private void OnTeachingSelectionCaptureOwnerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        var propertyName = args.PropertyName switch
        {
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleRow) =>
                nameof(TeachingGridRectangleRow),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleColumn) =>
                nameof(TeachingGridRectangleColumn),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleRowCount) =>
                nameof(TeachingGridRectangleRowCount),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleColumnCount) =>
                nameof(TeachingGridRectangleColumnCount),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleColumnText) =>
                nameof(TeachingGridRectangleColumnText),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleRowText) =>
                nameof(TeachingGridRectangleRowText),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleColumnCountText) =>
                nameof(TeachingGridRectangleColumnCountText),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleRowCountText) =>
                nameof(TeachingGridRectangleRowCountText),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.IsGridRectangleDraftValid) =>
                nameof(IsTeachingGridRectangleDraftValid),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleValidationSummary) =>
                nameof(TeachingGridRectangleValidationSummary),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridCircleCenterRow) =>
                nameof(TeachingGridCircleCenterRow),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridCircleCenterColumn) =>
                nameof(TeachingGridCircleCenterColumn),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridCircleRadius) =>
                nameof(TeachingGridCircleRadius),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.IsGridCircleDraftValid) =>
                nameof(IsTeachingGridCircleDraftValid),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridCircleValidationSummary) =>
                nameof(TeachingGridCircleValidationSummary),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.IsGridPolygonDraftValid) =>
                nameof(IsTeachingGridPolygonDraftValid),
            nameof(ToolWorkbenchTeachingSelectionCaptureOwner.GridPolygonValidationSummary) =>
                nameof(TeachingGridPolygonValidationSummary),
            _ => null
        };
        if (propertyName is not null)
        {
            OnPropertyChanged(propertyName);
        }

        if (args.PropertyName is nameof(
                ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleRow)
            or nameof(
                ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleColumn)
            or nameof(
                ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleRowCount)
            or nameof(
                ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleColumnCount)
            or nameof(
                ToolWorkbenchTeachingSelectionCaptureOwner.IsGridRectangleDraftValid)
            or nameof(
                ToolWorkbenchTeachingSelectionCaptureOwner.GridRectangleValidationSummary))
        {
            OnPropertyChanged(nameof(TeachingGridRectangleSourceFrameSummary));
        }
        else if (args.PropertyName is nameof(
                     ToolWorkbenchTeachingSelectionCaptureOwner.GridCircleCenterRow)
                 or nameof(
                     ToolWorkbenchTeachingSelectionCaptureOwner.GridCircleCenterColumn)
                 or nameof(
                     ToolWorkbenchTeachingSelectionCaptureOwner.GridCircleRadius)
                 or nameof(
                     ToolWorkbenchTeachingSelectionCaptureOwner.IsGridCircleDraftValid)
                 or nameof(
                     ToolWorkbenchTeachingSelectionCaptureOwner.GridCircleValidationSummary))
        {
            OnPropertyChanged(nameof(TeachingGridCircleSourceFrameSummary));
        }
        else if (args.PropertyName is nameof(
                     ToolWorkbenchTeachingSelectionCaptureOwner.IsGridPolygonDraftValid)
                 or nameof(
                     ToolWorkbenchTeachingSelectionCaptureOwner.GridPolygonValidationSummary))
        {
            OnPropertyChanged(nameof(TeachingGridPolygonSourceFrameSummary));
        }
    }

    private void OnTeachingSelectionCaptureOwnerStateChanged(
        object? sender,
        EventArgs args)
    {
        OnPropertyChanged(nameof(IsTeachingSelectionCaptureActive));
        OnPropertyChanged(nameof(IsSelectionCandidateActive));
        OnPropertyChanged(nameof(IsPipelineReviewExpanded));
        OnPropertyChanged(nameof(TeachingSelectionCapturedPointCount));
        OnPropertyChanged(nameof(TeachingSelectionRequiredPointCount));
        OnPropertyChanged(nameof(CanApplyTeachingSelectionCapture));
        OnPropertyChanged(nameof(IsTeachingGridRectangleEditorVisible));
        OnPropertyChanged(nameof(IsTeachingGridRectangleEditorEnabled));
        OnPropertyChanged(nameof(IsTeachingGridCircleEditorVisible));
        OnPropertyChanged(nameof(IsTeachingGridCircleEditorEnabled));
        OnPropertyChanged(nameof(IsTeachingGridPolygonEditorVisible));
        OnPropertyChanged(nameof(IsTeachingGridPolygonEditorEnabled));
        OnPropertyChanged(nameof(TeachingSelectionCaptureTitle));
        OnPropertyChanged(nameof(TeachingSelectionCaptureProgress));
        OnPropertyChanged(nameof(TeachingSelectionCaptureInstruction));
        OnPropertyChanged(nameof(IsOrientedBoxEditorContextVisible));
        OnPropertyChanged(nameof(IsSelectedStepRegionSurfaceVisible));
        NotifyPlaneFlatnessTeachingState();
        RefreshStepCommands();
        RefreshSelectedToolWorkspaceProjection();
        RefreshHeightImageRoiProjection();
    }

    private void OnLandmarkCorrespondenceEditorOwnerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        var propertyName = args.PropertyName switch
        {
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.SelectedRow) =>
                nameof(SelectedCorrespondenceRow),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.SourceEntityId) =>
                nameof(CorrespondenceSourceEntityId),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.ReferenceLandmarkId) =>
                nameof(CorrespondenceReferenceLandmarkId),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.ReferenceX) =>
                nameof(CorrespondenceReferenceX),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.ReferenceY) =>
                nameof(CorrespondenceReferenceY),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.ReferenceZ) =>
                nameof(CorrespondenceReferenceZ),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.ReferenceFrameId) =>
                nameof(CorrespondenceReferenceFrameId),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.ReferenceUnit) =>
                nameof(CorrespondenceReferenceUnit),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.ReferenceProvenance) =>
                nameof(CorrespondenceReferenceProvenance),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.ReferenceRevision) =>
                nameof(CorrespondenceReferenceRevision),
            nameof(
                ToolWorkbenchLandmarkCorrespondenceEditorOwner
                    .MinimumNormalizedTetrahedronVolume) =>
                nameof(CorrespondenceMinimumNormalizedTetrahedronVolume),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.CommitActionText) =>
                nameof(CorrespondenceCommitActionText),
            nameof(ToolWorkbenchLandmarkCorrespondenceEditorOwner.SelectionSummary) =>
                nameof(CorrespondenceSelectionSummary),
            _ => null
        };
        if (propertyName is not null)
        {
            OnPropertyChanged(propertyName);
        }
    }
    private void PromoteRecipeSchemaForSelection()
    {
        SetRecipeSchemaVersion(ToolRecipeDocument.CurrentSchemaVersion);
    }

    private void SetRecipeSchemaVersion(string schemaVersion)
    {
        if (RecipeSession.SetSchemaVersion(schemaVersion))
        {
            OnPropertyChanged(nameof(RecipeSchemaVersion));
        }
    }

    private static void AddInputEntity(ToolWorkbenchPipelineStepItem step, string entityId)
    {
        if (step.InputEntityIds.Contains(entityId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        step.InputEntityIdsText = string.Join("; ", step.InputEntityIds.Append(entityId));
    }

    private static void RemoveInputEntity(ToolWorkbenchPipelineStepItem step, string entityId)
    {
        step.InputEntityIdsText = string.Join(
            "; ",
            step.InputEntityIds.Where(input => !string.Equals(input, entityId, StringComparison.OrdinalIgnoreCase)));
        if (step.DualRoiRouting is { } routing)
        {
            step.DualRoiRouting = routing with
            {
                FirstRegionSelectionId = string.Equals(
                    routing.FirstRegionSelectionId,
                    entityId,
                    StringComparison.OrdinalIgnoreCase)
                        ? null
                        : routing.FirstRegionSelectionId,
                SecondRegionSelectionId = string.Equals(
                    routing.SecondRegionSelectionId,
                    entityId,
                    StringComparison.OrdinalIgnoreCase)
                        ? null
                        : routing.SecondRegionSelectionId
            };
        }
    }

    private ToolWorkbenchTeachingSelectionRequirement? CreateSelectionRequirement(
        ToolWorkbenchPipelineStepItem? step) =>
        ToolWorkbenchTeachingSelectionPolicy.CreateRequirement(
            step,
            CreatePlaneFlatnessSelectionRequirement(),
            Localization.CrossSectionSelection,
            Localization.CrossSectionSelectionDetail,
            isPlaneFlatnessMeasurementRole);

    private string CreateSelectionId(
        ToolWorkbenchPipelineStepItem step,
        ToolWorkbenchTeachingSelectionRequirement requirement) =>
        ToolWorkbenchTeachingSelectionPolicy.CreateSelectionId(
            step,
            requirement,
            IsSelectedStepDualRoiMeasurement,
            IsSelectedStepGapFlush,
            IsSelectedStepCompletenessGrid,
            isPlaneFlatnessMeasurementRole,
            TeachingCaptureSession.IsAdditionalLevelSurfaceReference,
            Selections);
    private void SubscribeStep(ToolWorkbenchPipelineStepItem step)
        => recipePartEventCoordinator.SubscribeStep(step);

    private void UnsubscribeStep(ToolWorkbenchPipelineStepItem step)
        => recipePartEventCoordinator.UnsubscribeStep(step);

    private void OnReferenceCatalogPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        OnPropertyChanged(args.PropertyName);
    }

    private void OnReferenceCatalogMutated(
        object? sender,
        ToolWorkbenchReferenceMutationEventArgs args)
    {
        RefreshAuthoredRecipeState();
        AppendLog(
            "Teach",
            args.Added
                ? $"Declared reference: {args.Reference.Id}."
                : $"Removed reference: {args.Reference.Id}.");
    }

    private void OnRecipePartChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ToolWorkbenchPipelineStepItem.State))
        {
            OnPropertyChanged(nameof(AlignmentStatusSummary));
            RebuildRecipeHealthProjection();
            return;
        }

        if (args.PropertyName is nameof(ToolWorkbenchPipelineStepItem.CanonicalState)
            or nameof(ToolWorkbenchPipelineStepItem.CanonicalStateKey)
            or nameof(ToolWorkbenchPipelineStepItem.CanonicalStateLabel)
            or nameof(ToolWorkbenchPipelineStepItem.CanonicalStateAccessibleName)
            or nameof(ToolWorkbenchPipelineStepItem.FlowNodeStatusLabel)
            or nameof(ToolWorkbenchPipelineStepItem.FlowNodeStatusHasIssue)
            or nameof(ToolWorkbenchPipelineStepItem.FlowNodeStatusIsFailure)
            or nameof(ToolWorkbenchPipelineStepItem.InputPortState)
            or nameof(ToolWorkbenchPipelineStepItem.InputPortDetail)
            or nameof(ToolWorkbenchPipelineStepItem.InputPortHasIssue)
            or nameof(ToolWorkbenchPipelineStepItem.InputContractDisplay)
            or nameof(ToolWorkbenchPipelineStepItem.OutputContractDisplay)
            or nameof(ToolWorkbenchPipelineStepItem.FlowInputs)
            or nameof(ToolWorkbenchPipelineStepItem.PrimaryFlowInput)
            or nameof(ToolWorkbenchPipelineStepItem.SecondaryFlowInputs)
            or nameof(ToolWorkbenchPipelineStepItem.CollapsedPrimaryInputLabel)
            or nameof(ToolWorkbenchPipelineStepItem.CollapsedOutputSummary)
            or nameof(ToolWorkbenchPipelineStepItem.FlowRepeatGroupAnchor)
            or nameof(ToolWorkbenchPipelineStepItem.IsFlowRepeatGroupMember)
            or nameof(ToolWorkbenchPipelineStepItem.IsFlowRepeatGroupLead)
            or nameof(ToolWorkbenchPipelineStepItem.IsFlowRepeatGroupLast)
            or nameof(ToolWorkbenchPipelineStepItem.FlowRepeatGroupCount)
            or nameof(ToolWorkbenchPipelineStepItem.FlowRepeatGroupOrderRange)
            or nameof(ToolWorkbenchPipelineStepItem.FlowRepeatGroupLabel)
            or nameof(ToolWorkbenchPipelineStepItem.HasLongFlowName)
            or nameof(ToolWorkbenchPipelineStepItem.CollapsedInputSummary)
            or nameof(ToolWorkbenchPipelineStepItem.OutputPreviewFrame)
            or nameof(ToolWorkbenchPipelineStepItem.HasOutputPreview)
            or nameof(ToolWorkbenchPipelineStepItem.InputSummary)
            or nameof(ToolWorkbenchPipelineStepItem.ResultValueSummary)
            or nameof(ToolWorkbenchPipelineStepItem.OutputPortState)
            or nameof(ToolWorkbenchPipelineStepItem.OutputPortDetail)
            or nameof(ToolWorkbenchPipelineStepItem.OutputPortHasIssue))
        {
            return;
        }

        if (suppressRecipeRefresh)
        {
            return;
        }

        if (!HasPendingStepParameterChanges
            && sender is ToolWorkbenchParameterItem parameter
            && (SelectedPipelineStep?.Parameters.Contains(parameter) ?? false))
        {
            RefreshSelectedStepPropertyDraft();
        }

        MarkFilterPreviewStaleIfNeeded(sender);
        MarkHeightThresholdPreviewStaleIfNeeded(sender);
        MarkSignedDeltaBackgroundSubtractionPreviewStaleIfNeeded(sender);
        MarkRemoveOutlierPreviewStaleIfNeeded(sender);
        MarkConnectedRegionPreviewStaleIfNeeded(sender);
        MarkGridPolygonDomainPreviewStaleIfNeeded(sender);
        MarkDomainMaskPreviewStaleIfNeeded(sender);
        MarkEditableRegionPreviewStaleIfNeeded(sender);
        MarkLevelSurfacePreviewStaleIfNeeded(sender);
        MarkRoiCropPreviewStaleIfNeeded(sender);
        MarkHeightDifferenceEdgePreviewStaleIfNeeded(sender);
        MarkTwoPointLinePreviewStaleIfNeeded(sender);
        MarkThreePointPlanePreviewStaleIfNeeded(sender);
        MarkDatumPlaneDeviationPreviewStaleIfNeeded(sender);
        MarkLineFitPreviewStaleIfNeeded(sender);
        MarkLineIntersectionPreviewStaleIfNeeded(sender);
        MarkLandmarkCorrespondencePreviewStaleIfNeeded(sender);
        MarkAffineSolvePreviewStaleIfNeeded(sender);
        MarkMeasurementPreviewStaleIfNeeded(sender);
        MarkPhysicalEdgeGapFlushPreviewStaleIfNeeded(sender);
        if (ReferenceEquals(sender, SelectedPipelineStep))
        {
            OnPropertyChanged(nameof(SelectedPipelineStepTitle));
            OnPropertyChanged(nameof(SelectedRouteInputIds));
            OnPropertyChanged(nameof(SelectedRouteOutputId));
            OnPropertyChanged(nameof(AvailableInputEntitiesSummary));
        }
        SetDirty(true);
        RefreshRecipeState();
    }

    private void MutateRecipe(Action action, bool markDirty = true)
    {
        if (HasPendingOrderedRunRecordSave) return;
        suppressRecipeRefresh = true;
        try
        {
            action();
        }
        finally
        {
            suppressRecipeRefresh = false;
        }

        if (markDirty)
        {
            SetDirty(true);
        }

        RefreshRecipeState();
    }

    private void RefreshAuthoredRecipeState()
    {
        SetDirty(true);
        RefreshRecipeState();
    }

    private void SetDirty(bool value, bool invalidateOrderedRun = true)
    {
        if (!RecipeSession.SetDirty(value))
        {
            return;
        }

        if (value && invalidateOrderedRun)
        {
            InvalidateOrderedRun(Localize(
                "레시피가 변경되었습니다. 저장한 뒤 직접 다시 실행하세요.",
                "The recipe changed. Save it, then Run explicitly again."));
        }
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasUncommittedRecipeChanges));
        OnPropertyChanged(nameof(RecipeStateSummary));
        OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
        NotifyHeaderRecipeState();
        NotifyOrderedRunState();
    }

    private void NotifyHeaderRecipeState()
    {
        ((RelayCommand)SaveTeachingRecipeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SaveTeachingRecipeAsCommand).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsRecipeSaving));
        OnPropertyChanged(nameof(HasRecipeSaveFailure));
        OnPropertyChanged(nameof(IsHeaderRecipeModified));
        OnPropertyChanged(nameof(HeaderRecipeMetaText));
        OnPropertyChanged(nameof(HeaderRecipeSaveStateText));
        OnPropertyChanged(nameof(HeaderRecipeDetailText));
    }

    private void RefreshStepCommands()
    {
        NotifyStepParameterHistory();
        inspectionQueueWorkspace?.RefreshTeachingInputCommand();
        removeSelectedStepCommand.RaiseCanExecuteChanged();
        restoreLastRemovedStepCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanRestoreLastRemovedStep));
        teachingSelectionStoreOwner.RefreshCommandStates();
        moveSelectedStepUpCommand.RaiseCanExecuteChanged();
        moveSelectedStepDownCommand.RaiseCanExecuteChanged();
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        selectedStepExecutionOwner?.RunCommand.RaiseCanExecuteChanged();
        NotifyOrderedRunState();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
        NotifyRecipeHealthSelectionChanged();
        if (string.Equals(SelectedPipelineStep?.ToolId, "filter", StringComparison.Ordinal))
        {
            showFilterSourceCommand?.RaiseCanExecuteChanged();
            setFilterKernel3Command?.RaiseCanExecuteChanged();
            setFilterKernel5Command?.RaiseCanExecuteChanged();
            setFilterKernel7Command?.RaiseCanExecuteChanged();
        }
        RefreshFlowConnectionCommands();
    }

    private string CreateUniqueStepId(string toolId)
    {
        var root = $"step.{NormalizeId(toolId)}";
        var index = 1;
        while (PipelineSteps.Any(step => string.Equals(step.Id, $"{root}.{index:00}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"{root}.{index:00}";
    }

    private string CreateUniqueOutputId(string outputContract)
    {
        var root = $"derived.{NormalizeId(outputContract)}";
        var index = 1;
        var existing = new HashSet<string>(
            PipelineSteps.Select(step => step.OutputEntityId)
                .Append(Source.Id)
                .Concat(References.Select(reference => reference.Id))
                .Concat(Selections.Select(selection => selection.Id)),
            StringComparer.OrdinalIgnoreCase);
        while (!existing.Add($"{root}.{index:00}"))
        {
            index++;
        }

        return $"{root}.{index:00}";
    }

    private static string NormalizeId(string? value)
    {
        var parts = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var normalized = new string(parts).Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(normalized) ? "entity" : normalized;
    }

    internal void AppendLog(string category, string message)
        => runLogOwner.Append(category, message);

    private void RecordSelectedExecution(ToolWorkbenchPipelineStepItem step, bool publish, ResultStatus? status, string detail, string reason, bool wasCanceled)
    {
        if (!PipelineSteps.Contains(step)) return;
        var action = publish ? Localize("선택 도구 실행", "Run selected tool") : Localize("미리보기", "Preview");
        var unjudged = status is null && (CurrentMeasurementOutput is { } measurement
            && string.Equals(measurement.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            && measurement.Result.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage
            || step.ToolId == "datum-plane-raw-height-deviation" && CurrentDatumPlaneDeviationResult?.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage);
        var statusText = wasCanceled ? Localize("취소", "Canceled")
            : status is { } resultStatus ? Localization.ResultStatusLabel(resultStatus)
                : unjudged ? Localize("미판정", "Unjudged") : Localize("완료", "Completed");
        if (status == ResultStatus.Pass) statusText = "OK";
        else if (status == ResultStatus.Fail) statusText = "NG";
        var hasReason = !string.IsNullOrWhiteSpace(reason) && reason != "—";
        var resultDetail = status == ResultStatus.Error && hasReason ? reason : detail;
        var message = wasCanceled || status is null && !unjudged || status == ResultStatus.Error
            ? $"{step.ToolName} {statusText}" + (string.IsNullOrWhiteSpace(resultDetail) ? string.Empty : $" · {resultDetail}")
            : $"{step.ToolName}" + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}") + $" · {statusText}";
        if (status is ResultStatus.Fail or ResultStatus.Warning or ResultStatus.NotRun && hasReason) message += $" · {reason}";
        if (!publish) message = $"{action} · {message}";
        runLogOwner.AppendExecution(message, status, wasCanceled);
    }

    private string GetActiveTeachingRoleName() =>
        IsSelectedStepDualRoiMeasurement
            ? isPlaneFlatnessMeasurementRole ? "measurement" : "reference"
            : "selection";

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is nameof(IsSelectedStepPreviewRunning)
            or nameof(IsValidationSetRunning)
            or nameof(IsOrderedRunRunning)
            or nameof(IsSurfaceMatchExperimentRunning)
            or nameof(IsFilterPreviewRunning)
            or nameof(IsHeightThresholdPreviewRunning)
            or nameof(IsSignedDeltaPreviewRunning)
            or nameof(IsRemoveOutlierPreviewRunning)
            or nameof(IsConnectedRegionPreviewRunning)
            or nameof(IsGridPolygonDomainPreviewRunning)
            or nameof(IsDomainMaskPreviewRunning)
            or nameof(IsEditableRegionPreviewRunning)
            or nameof(IsLevelSurfacePreviewRunning)
            or nameof(IsRoiCropPreviewRunning)
            or nameof(IsEdgePreviewRunning)
            or nameof(IsTwoPointLinePreviewRunning)
            or nameof(IsThreePointPlanePreviewRunning)
            or nameof(IsDatumPlaneDeviationPreviewRunning)
            or nameof(IsLineFitPreviewRunning)
            or nameof(IsLineIntersectionPreviewRunning)
            or nameof(IsLandmarkCorrespondencePreviewRunning)
            or nameof(IsAffineSolvePreviewRunning)
            or nameof(IsAffineApplyPreviewRunning)
            or nameof(IsRegridHeightFieldPreviewRunning)
            or nameof(IsMeasurementPreviewRunning)
            or nameof(IsPhysicalEdgeGapFlushPreviewRunning))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRecipeMutationBlocked)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanEditRecipe)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanOpenRecipeInfoEditor)));
            openRecipeInfoEditorCommand?.RaiseCanExecuteChanged();
            (OpenRecentTeachingRecipeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            removeSelectedStepCommand?.RaiseCanExecuteChanged();
            RefreshFlowConnectionCommands();
            teachingSelectionStoreOwner?.RefreshCommandStates();
            sourceLoadOwner?.RefreshCommandStates();
            RefreshHeightImageRoiProjection();
            NotifyRecipeHealthSelectionChanged();
        }
    }
}
