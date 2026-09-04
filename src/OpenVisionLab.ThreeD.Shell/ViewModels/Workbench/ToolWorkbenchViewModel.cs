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

    private readonly ToolWorkbenchRunLogOwner runLogOwner;
    private readonly ToolWorkbenchFilterExecutionOwner filterExecutionOwner;
    private readonly ToolWorkbenchSelectedStepExecutionOwner selectedStepExecutionOwner;
    private readonly ToolWorkbenchRemoveOutlierExecutionOwner removeOutlierExecutionOwner;
    private readonly ToolWorkbenchConnectedRegionExecutionOwner connectedRegionExecutionOwner;
    private readonly ToolWorkbenchDomainMaskExecutionOwner domainMaskExecutionOwner;
    private readonly ToolWorkbenchEditableRegionExecutionOwner editableRegionExecutionOwner;
    private readonly ToolWorkbenchLevelSurfaceExecutionOwner levelSurfaceExecutionOwner;
    private readonly ToolWorkbenchRoiCropExecutionOwner roiCropExecutionOwner;
    private readonly ToolWorkbenchTwoPointLineExecutionOwner twoPointLineExecutionOwner;
    private readonly ToolWorkbenchHeightDifferenceEdgeExecutionOwner heightDifferenceEdgeExecutionOwner;
    private readonly ToolWorkbenchLineFitExecutionOwner lineFitExecutionOwner;
    private readonly ToolWorkbenchLineIntersectionExecutionOwner lineIntersectionExecutionOwner;
    private readonly ToolWorkbenchLandmarkCorrespondenceExecutionOwner landmarkCorrespondenceExecutionOwner;
    private readonly ToolWorkbenchThreePointPlaneExecutionOwner threePointPlaneExecutionOwner;
    private readonly ToolWorkbenchDatumPlaneDeviationExecutionOwner datumPlaneDeviationExecutionOwner;
    private readonly ToolWorkbenchXyzAffineExecutionOwner xyzAffineExecutionOwner;
    private readonly ToolWorkbenchRegridHeightFieldExecutionOwner regridHeightFieldExecutionOwner;
    private readonly ToolWorkbenchHeightMeasurementExecutionOwner heightMeasurementExecutionOwner;
    private readonly ToolWorkbenchValidationSetExecutionOwner validationSetExecutionOwner;
    private readonly ToolWorkbenchSourceLoadOwner sourceLoadOwner;
    private readonly ToolWorkbenchLocalizationSubscriptionOwner localizationSubscriptionOwner;
    private readonly ToolWorkbenchOrderedRunExecutionOwner orderedRunExecutionOwner;
    private readonly ToolWorkbenchTeachingSelectionStoreOwner teachingSelectionStoreOwner;
    private readonly ToolWorkbenchTeachingSelectionCaptureOwner teachingSelectionCaptureOwner;
    private readonly ToolWorkbenchLandmarkCorrespondenceEditorOwner
        landmarkCorrespondenceEditorOwner;
    private readonly ToolWorkbenchReferenceCatalogOwner referenceCatalogOwner;
    private readonly RelayCommand addSelectedToolCommand;
    private readonly RelayCommand removeSelectedStepCommand;
    private readonly RelayCommand moveSelectedStepUpCommand;
    private readonly RelayCommand moveSelectedStepDownCommand;
    private readonly RelayCommand selectPipelineStepCommand;
    private readonly RelayCommand openSelectedToolLabCommand;
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
    private int selectedReviewTabIndex;
    private int disposalState;

    public ToolWorkbenchViewModel(string? recentRecipesPath = null)
    {
        runLogOwner = new ToolWorkbenchRunLogOwner();
        sourceQualityUiDispatcher =
            System.Threading.SynchronizationContext.Current
                is System.Windows.Threading.DispatcherSynchronizationContext
                    ? System.Windows.Threading.Dispatcher.FromThread(
                        System.Threading.Thread.CurrentThread)
                    : null;
        validationSetExecutionOwner = new ToolWorkbenchValidationSetExecutionOwner(
            RefreshValidationSetExecutionState);
        sourceLoadOwner = new ToolWorkbenchSourceLoadOwner(
            Localization,
            propertyName => OnPropertyChanged(propertyName),
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
        ThicknessRepeatGrid = new ThicknessRepeatGridAuthoringSession();
        SelectedToolWorkspace = new SelectedToolWorkspaceViewModel(WorkspaceSelection);
        WorkspaceSelection.SelectionChanged += OnInspectionWorkspaceSelectionChanged;
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
        Source.PropertyChanged += OnRecipePartChanged;
        referenceCatalogOwner = new ToolWorkbenchReferenceCatalogOwner(NormalizeId);
        referenceCatalogOwner.PropertyChanged += OnReferenceCatalogPropertyChanged;
        referenceCatalogOwner.ReferencePropertyChanged += OnRecipePartChanged;
        referenceCatalogOwner.Mutated += OnReferenceCatalogMutated;
        teachingSelectionStoreOwner = new ToolWorkbenchTeachingSelectionStoreOwner(
            () => Source.Id,
            () => Source.FrameId,
            () => SourceSession.SourceBinding,
            GetPublishedSelectionBindingState,
            () => SelectedStepTeachingSelection,
            () => IsSelectedStepViewerCaptureSupported,
            RemoveTeachingSelection,
            UseExistingTeachingSelection);
        teachingSelectionStoreOwner.PropertyChanged +=
            OnTeachingSelectionStoreOwnerPropertyChanged;
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
            () => OrderedRunInvalidated?.Invoke(this, EventArgs.Empty));
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
            sender => ReferenceEquals(sender, Source),
            (category, message) => AppendLog(category, message),
            args => FilterDisplayRequested?.Invoke(this, args),
            summary => MarkHeightDifferenceEdgePreviewStale(summary),
            summary => ClearHeightDifferenceEdgePreview(summary),
            RefreshFilterStateFromOwner);
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
            entityId => connectedRegionExecutionOwner.TryGetPublishedArtifact(entityId),
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
            () => filterPreviewOutput,
            () => filterPreviewPath,
            () => isFilterPreviewStale,
            () => isFilterPreviewPublished,
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
            RefreshHeightMeasurementStateFromOwner);
        selectedStepExecutionOwner = new ToolWorkbenchSelectedStepExecutionOwner(
            () => SelectedPipelineStep,
            CreateSelectedStepExecutionRoutes());
        InitializeSourceQualityWorkspace();
        OrientedBoxEditor = new OrientedBox3DEditorViewModel();
        InitializeOrientedBox3DEditing();
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
        teachingSelectionCaptureOwner.PropertyChanged +=
            OnTeachingSelectionCaptureOwnerPropertyChanged;
        teachingSelectionCaptureOwner.StateChanged +=
            OnTeachingSelectionCaptureOwnerStateChanged;
        landmarkCorrespondenceEditorOwner =
            new ToolWorkbenchLandmarkCorrespondenceEditorOwner(
                CreateLandmarkCorrespondenceEditorContext,
                (step, requirement) => CreateSelectionId(step, requirement),
                PersistSelectionForSelectedStep,
                RemoveTeachingSelection,
                teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged,
                AppendLog);
        landmarkCorrespondenceEditorOwner.PropertyChanged +=
            OnLandmarkCorrespondenceEditorOwnerPropertyChanged;

        addSelectedToolCommand = new RelayCommand(
            parameter => AddSelectedTool(parameter as ToolWorkbenchToolItem),
            parameter => CanAddTool(parameter as ToolWorkbenchToolItem));
        removeSelectedStepCommand = new RelayCommand(
            _ => RequestSelectedStepRemoval(),
            _ => SelectedPipelineStep is not null && !IsRecipeMutationBlocked);
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

        InitializePropertyGridEditing();
        InitializePreparationPresetAssistant();
        InitializeFirstRecipeUx();
        InitializePlaneFlatnessTeaching();
        NewTeachingRecipeCommand = new RelayCommand(_ => BeginFirstRecipeSetup());
        AddSelectedToolCommand = addSelectedToolCommand;
        RemoveSelectedStepCommand = removeSelectedStepCommand;
        MoveSelectedStepUpCommand = moveSelectedStepUpCommand;
        MoveSelectedStepDownCommand = moveSelectedStepDownCommand;
        SelectPipelineStepCommand = selectPipelineStepCommand;
        InitializeHeightImageRoiEditing();
        InitializeArtifactRegistryAndNavigator();
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
        OpenToolLibraryCommand = new RelayCommand(_ => OpenToolLibraryRequested?.Invoke(this, EventArgs.Empty));
        OpenTeachingRecipeCommand = new RelayCommand(_ => OpenTeachingRecipeRequested?.Invoke(this, EventArgs.Empty));
        InitializeFilterExecution();
        InitializeOutputCompareSession();
        InitializeViewerWorkspace();
        InitializeSurfaceMatchCollectionOwner();
        InitializeDisplayedOutputs();
        InitializeFlowDiagnostics();
        InitializeCompatibleToolCatalog();
        InitializeValidationSet();
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
            OnFirstRecipeLanguageChanged);
        ValidationWorkspace = new RecipePipelineReviewValidationViewModel(this);
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
        if (System.Threading.Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        localizationSubscriptionOwner.Dispose();
        sourceLoadOwner.Dispose();
        firstRecipeSetupOwner.Dispose();
        flowDiagnosticsOwner.Dispose();
        orderedRunExecutionOwner.Dispose();
        validationThresholdWorkflowOwner.Dispose();
        validationSetExecutionOwner.Dispose();
        filterExecutionOwner.Dispose();
        heightMeasurementExecutionOwner.Dispose();
        editableRegionExecutionOwner.Dispose();
        regridHeightFieldExecutionOwner.Dispose();
        xyzAffineExecutionOwner.Dispose();
        landmarkCorrespondenceExecutionOwner.Dispose();
        lineIntersectionExecutionOwner.Dispose();
        lineFitExecutionOwner.Dispose();
        twoPointLineExecutionOwner.Dispose();
        heightDifferenceEdgeExecutionOwner.Dispose();
        levelSurfaceExecutionOwner.Dispose();
        datumPlaneDeviationExecutionOwner.Dispose();
        threePointPlaneExecutionOwner.Dispose();
        roiCropExecutionOwner.Dispose();
        domainMaskExecutionOwner.Dispose();
        connectedRegionExecutionOwner.Dispose();
        removeOutlierExecutionOwner.Dispose();
        CancelSourceQualityUiNotification();
        HeightImageViewer.Dispose();
        SourceQuality.Dispose();
        SourceSession.Dispose();
    }

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
    public OrientedBox3DEditorViewModel OrientedBoxEditor { get; }
    public ThreeDLocalization Localization => ThreeDLocalization.Shared;

    public int SelectedReviewTabIndex
    {
        get => selectedReviewTabIndex;
        set => SetField(ref selectedReviewTabIndex, Math.Clamp(value, 0, 4));
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

    public ICommand NewTeachingRecipeCommand { get; }
    public ICommand AddSelectedToolCommand { get; }
    public ICommand RemoveSelectedStepCommand { get; }
    public ICommand MoveSelectedStepUpCommand { get; }
    public ICommand MoveSelectedStepDownCommand { get; }
    public ICommand SelectPipelineStepCommand { get; }
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

            if (HasPendingStepParameterChanges)
            {
                SetParameterDraftStatus("Apply or discard the current parameter draft before selecting another step.");
                OnPropertyChanged();
                return;
            }

            if (IsTeachingSelectionCaptureActive)
            {
                CancelTeachingSelectionCapture();
            }

            CancelThicknessRepeatGridForSelectionChange();
            var started = Stopwatch.GetTimestamp();
            selectedPipelineStep = value;
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
                "레시피가 바뀌어 이전 실행 증거가 현재 컨텍스트에서 해제되었습니다.",
                "The recipe changed, so the previous Run evidence was detached from the current context."));
            OnPropertyChanged();
            OnPropertyChanged(nameof(RecipePathSummary));
            OnPropertyChanged(nameof(RecipeStateSummary));
            OnPropertyChanged(nameof(HasRecipeIdentity));
            OnPropertyChanged(nameof(LocalizedRecipePathSummary));
            OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
        }
    }

    public bool IsDirty => RecipeSession.IsDirty;

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

    public bool IsRecipeMutationBlocked =>
        IsOrderedRunRunning
        || IsValidationSetRunning
        || IsSurfaceMatchExperimentRunning
        || IsFilterPreviewRunning
        || IsRemoveOutlierPreviewRunning
        || IsConnectedRegionPreviewRunning
        || IsLevelSurfacePreviewRunning
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
        || IsMeasurementPreviewRunning;

    public bool CanSaveTeachingRecipe => RecipeSession.StorageValidation.IsValid
        && RecipeSession.SourceBindingErrors.Count == 0;

    public bool IsTeachingRecipeExecutionReady => RecipeSession.Validation.IsValid
        && RecipeSession.SourceBindingErrors.Count == 0
        && SourceSession.SourceIdentityErrors.Count == 0;

    public bool IsRecipeSaveBlocked => !CanSaveTeachingRecipe;

    public string SelectedToolTitle => SelectedTool is null
        ? "No tool selected"
        : $"{SelectedTool.Category} / {SelectedTool.Name}";

    public string SelectedToolHint => SelectedTool is null
        ? "Select a tool to inspect its typed input, output, and required parameters."
        : "Add this tool to the teaching recipe, then set its entity IDs and parameters. Adding or editing never runs inspection.";

    public string SelectedPipelineStepTitle => SelectedPipelineStep is null
        ? "No taught step selected"
        : $"Step {SelectedPipelineStep.Order}: {SelectedPipelineStep.ToolName}";

    public string ValidationSummary => SourceSession.SourceIdentityErrors.Count > 0
            ? $"Recipe source needs {SourceSession.SourceIdentityErrors.Count} correction(s) before Preview or Run."
        : RecipeSession.SourceBindingErrors.Count > 0
        ? $"Teaching has {RecipeSession.SourceBindingErrors.Count} stale source selection(s); recapture or replace them before saving."
        : RecipeSession.Validation.IsValid
        ? RecipeSession.Validation.Warnings.Count == 0
            ? "Inspection recipe is structurally valid. Typed tool rows support explicit Preview/Publish; whole-recipe Run stays blocked until every routed step has an executor."
            : $"Inspection recipe is valid with {RecipeSession.Validation.Warnings.Count} warning(s). Typed tool rows support explicit Preview/Publish; whole-recipe Run stays blocked until every routed step has an executor."
        : RecipeSession.StorageValidation.IsValid
            ? $"Teaching needs {RecipeSession.Validation.Errors.Count} correction(s) before Preview or Run. The draft can still be saved."
            : $"Teaching needs {RecipeSession.StorageValidation.Errors.Count + RecipeSession.SourceBindingErrors.Count} structural correction(s) before it can be saved.";

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
        ? "Source not loaded"
        : $"{Source.Format} | {Source.Unit} | {Source.FrameId}";

    public string AlignmentStatusSummary =>
        PipelineSteps.LastOrDefault(step => string.Equals(step.ToolId, "re-grid-height-map", StringComparison.OrdinalIgnoreCase)) is { } regrid
            ? $"A3 Re-grid Height Map | {regrid.State}"
            : PipelineSteps.LastOrDefault(step => string.Equals(step.ToolId, "xyz-affine-apply", StringComparison.OrdinalIgnoreCase)) is { } apply
                ? $"A2 Apply XYZ Affine | {apply.State}"
                : PipelineSteps.LastOrDefault(step => string.Equals(step.ToolId, "xyz-affine-solve", StringComparison.OrdinalIgnoreCase)) is { } solve
                    ? $"A1 XYZ Affine Solve | {solve.State}"
                    : PipelineSteps.LastOrDefault(step => string.Equals(step.ToolId, "xyz-affine-transform", StringComparison.OrdinalIgnoreCase)) is { } legacy
                        ? $"Legacy XYZ Affine Transform | {legacy.State}"
                        : "Alignment not taught";

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
        null => "No Viewer selection required",
        _ when IsSelectedStepThickness => $"{(IsPlaneFlatnessMeasurementRoleActive ? Localization.MeasurementRoi : Localization.ReferenceRoi)} \u00B7 {Localization.TwoGridCorners}",
        { Kind: ToolRecipeSelectionKinds.GridPolygon, UsesViewerCapture: true } requirement =>
            $"{requirement.Name} - {requirement.RequiredPointCount}+ ordered grid vertex(es)",
        { UsesViewerCapture: true } requirement => $"{requirement.Name} - {requirement.RequiredPointCount} C3D grid pick(s)",
        { Kind: ToolRecipeSelectionKinds.LandmarkCorrespondenceSet } => "Landmark correspondence rows",
        var requirement => requirement.Name
    };

    public string SelectedStepSelectionRequirementSummary => SelectedStepSelectionRequirement switch
    {
        null => "This step consumes the source or earlier typed entities. Selecting or editing it never starts Viewer capture.",
        _ when IsSelectedStepThickness => Localization.ThicknessRoiTeachingDetail,
        { UsesViewerCapture: true } requirement => $"{requirement.Description} Capture stores geometry only; it never runs an inspection algorithm.",
        { Kind: ToolRecipeSelectionKinds.LandmarkCorrespondenceSet } => "Enter four Published CornerAnchor mappings, reference frame/unit/provenance/revision, and an explicit non-planarity threshold. Editing never runs the tool.",
        var requirement => requirement.Description
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
            : "No recipe-owned selection is routed to this step.")
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
            : "Polygon vertices must be finite, unique, ordered, non-degenerate, and inside the source grid.";

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
            ? $"Center X/column {TeachingGridCircleCenterColumn}, Z/row {TeachingGridCircleCenterRow} | radius {TeachingGridCircleRadius:G6} cells | {SelectedStepTeachingSelection?.FrameId ?? Source.FrameId}"
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

    public bool IsTeachingGridRectangleDraftValid =>
        teachingSelectionCaptureOwner.IsGridRectangleDraftValid;

    public string TeachingGridRectangleValidationSummary
    {
        get
        {
            return teachingSelectionCaptureOwner.GridRectangleValidationSummary;
        }
    }

    public string TeachingGridRectangleSourceFrameSummary =>
        IsTeachingGridRectangleDraftValid
            ? $"X columns {TeachingGridRectangleColumn}..{TeachingGridRectangleColumn + TeachingGridRectangleColumnCount - 1} | Z rows {TeachingGridRectangleRow}..{TeachingGridRectangleRow + TeachingGridRectangleRowCount - 1} | {SelectedStepTeachingSelection?.FrameId ?? Source.FrameId}"
            : "X/Z source-frame footprint unavailable until the ROI values are valid.";

    public string TeachingSelectionCaptureTitle => !IsTeachingSelectionCaptureActive
        && OrientedBoxEditor.IsDraftOpen
            ? $"{OrientedBoxEditor.Name} · 3D Box Review"
        : SelectedStepSelectionRequirement is null
        ? Localization.SelectionCapture
        : SelectedStepSelectionRequirement.Kind == ToolRecipeSelectionKinds.GridRectangle
            ? $"{SelectedStepSelectionRequirement.Name} \u00B7 {(CanApplyTeachingSelectionCapture ? Localization.RoiReview : Localization.RoiDrawing)}"
            : SelectedStepSelectionRequirement.Kind == ToolRecipeSelectionKinds.GridPolygon
                ? $"{Localization.SelectionCapture}: {SelectedStepSelectionRequirement.Name} · ordered vertices"
            : $"{Localization.SelectionCapture}: {SelectedStepSelectionRequirement.Name}";

    public string TeachingSelectionCaptureProgress => IsTeachingSelectionCaptureActive
        ? CanApplyTeachingSelectionCapture
            && SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
                ? Localization.RoiCaptureReadyProgress
            : CanApplyTeachingSelectionCapture
                && SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridPolygon
                    ? $"{TeachingSelectionCapturedPointCount} ordered polygon vertices ready · Enter applies · Esc cancels"
                : string.Format(
                    Localization.SelectionCaptureProgressFormat,
                    TeachingSelectionCapturedPointCount,
                    TeachingSelectionRequiredPointCount)
        : OrientedBoxEditor.IsDraftOpen
            ? "Viewer handles and numeric values edit one transient candidate. Enter applies; Esc cancels."
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
                    ? "Pick or edit three or more ordered vertices. Apply/Enter commits the outline; Cancel/Escape discards it."
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
        OnPropertyChanged(nameof(SelectedStepTeachingSelectionSummary));
        OnPropertyChanged(nameof(SelectionCaptureActionText));
        OnPropertyChanged(nameof(ThicknessRoiTeachingDetail));
        OnPropertyChanged(nameof(TeachingSelectionCaptureTitle));
        OnPropertyChanged(nameof(TeachingSelectionCaptureProgress));
        OnPropertyChanged(nameof(TeachingSelectionCaptureInstruction));
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
        ? "No taught tools yet. Select a Toolbox item and add it to this recipe."
        : string.Empty;

    public bool IsPipelineEmpty => PipelineSteps.Count == 0;

    public bool HasPipelineSteps => PipelineSteps.Count > 0;

    private void RefreshFilteredTools()
    {
        var query = toolSearchText.Trim();
        FilteredTools.ReplaceAll(Tools.Where(tool =>
            query.Length == 0
            || tool.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || tool.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || tool.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
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
        if (sourcePathChanged)
        {
            ClearFilterPreview("Source changed; Preview is required.");
            ClearRemoveOutlierPreview(
                "Source changed; Remove Outlier Pixels Preview is required.");
            ClearConnectedRegionPreview(
                "Source changed; Connected Region Preview is required.");
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
        }
        clearPreviewMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

        stageStart = Stopwatch.GetTimestamp();
        var sourceBindingChanged = SourceSession.SetSourceBinding(sourceBinding);
        if (sourcePathChanged || sourceBindingChanged)
        {
            HeightImageViewer.ClearSource();
        }
        AcceptCurrentSourceIdentity();
        identityMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
        if (!sourcePathChanged)
        {
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
            SourceSession.SetSourceAcquisitionProvenance(CreateUnavailableSourceAcquisitionProvenance());
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

    public bool TrySaveTeachingRecipe(string path, out string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        AppendLog(
            "Save",
            $"Recipe save requested | path={Path.GetFullPath(path)} | dirty={IsDirty} | steps={PipelineSteps.Count} | selections={Selections.Count}.");
        if (HasPendingStepParameterChanges)
        {
            message = "Apply or discard the selected step parameter draft before saving.";
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

        try
        {
            var fullPath = Path.GetFullPath(path);
            ToolRecipeDocumentStore.Save(fullPath, CreateDocument());
            SaveValidationSetDefinition(fullPath);
            SaveValidationThresholdCorrectionEvidence(fullPath);
            RecipePath = fullPath;
            connectedRegionExecutionOwner.PersistPublishedArtifactIfPossible();
            domainMaskExecutionOwner.PersistPublishedArtifactIfPossible();
            editableRegionExecutionOwner.PersistPublishedArtifactIfPossible();
            levelSurfaceExecutionOwner.PersistPublishedArtifactIfPossible();
            SetDirty(false);
            RecordRecentRecipe(fullPath);
            message = $"Teaching recipe saved: {Path.GetFileName(fullPath)}";
            AppendLog("Teach", message);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            AppendLog("Error", $"Teaching recipe save failed: {message}");
            return false;
        }
    }

    public bool TryOpenTeachingRecipe(string path, out string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var fullPath = Path.GetFullPath(path);
            var document = ResolveRelativeSourcePath(ToolRecipeDocumentStore.Load(fullPath), fullPath);
            ApplyDocument(document);
            // ApplyDocument performs an initial recipe refresh while the
            // source binding still belongs to the previous context. Reset only
            // after the document has been applied, then rebuild against the
            // new source binding. Ordinary candidate refreshes still retain
            // stale pins and never rebind them silently.
            ViewerWorkspace.ResetContentPins();
            SourceSession.SetSourceBinding(TryReadSourceBinding(document.Source.Path));
            RefreshRecipeState();
            // Finish applying and validating the recipe before the asynchronous
            // Source Quality callback can rebuild the presentation collections.
            // This keeps non-UI callers deterministic while the UI dispatcher
            // still receives the same eventual report.
            BeginSourceQualityLoad();
            RecipePath = fullPath;
            connectedRegionExecutionOwner.RestorePublishedConnectedRegionArtifact();
            domainMaskExecutionOwner.RestorePublishedArtifact();
            editableRegionExecutionOwner.RestorePublishedArtifact();
            levelSurfaceExecutionOwner.RestorePublishedArtifact();
            LoadValidationSetDefinition(fullPath, document);
            LoadValidationThresholdCorrectionEvidence(fullPath, document);
            SetDirty(false);
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
        if (IsTeachingSelectionCaptureActive)
        {
            CancelTeachingSelectionCapture();
        }

        MutateRecipe(() =>
        {
            ViewerWorkspace.ResetContentPins();
            SourceSession.SetSourceBinding(null);
            SourceSession.ClearDecodedSource();
            HeightImageViewer.ClearSource();
            AcceptCurrentSourceIdentity();
            SourceQuality.Clear();
            SourceSession.SetSourceAcquisitionProvenance(CreateUnavailableSourceAcquisitionProvenance());
            RecipeSession.SetSchemaVersion(ToolRecipeDocument.CurrentSchemaVersion);
            RecipeName = string.IsNullOrWhiteSpace(name)
                ? "Untitled 3D Inspection"
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
        }, markDirty: false);
        SourceQuality.LoadAcquisitionProvenance(SourceSession.SourceAcquisitionProvenance, Source.FrameId);
        OnPropertyChanged(nameof(SourceAcquisitionProvenance));
        SetDirty(false);
        ClearValidationSet();
        SetValidationSetDefinitionDirty(false);
        ToolSearchText = string.Empty;
        OnPropertyChanged(nameof(RecipeSchemaVersion));
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        AppendLog("Teach", "New empty teaching recipe created. Select a C3D source before adding an inspection step.");
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

        var step = new ToolWorkbenchPipelineStepItem(
            CreateUniqueStepId(tool.Id),
            tool,
            input ?? string.Empty,
            CreateUniqueOutputId(tool.OutputContract));
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
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepFilter));
        OnPropertyChanged(nameof(FilterKernelSummary));
        OnPropertyChanged(nameof(IsFilterPreviewRunning));
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

    private void RefreshRemoveOutlierStateFromOwner()
    {
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

    private void RefreshDomainMaskStateFromOwner()
    {
        domainMaskExecutionOwner.MarkStaleIfUpstreamChanged();
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
        UnsubscribeStep(step);
        PipelineSteps.Remove(step);
        teachingSelectionStoreOwner.RemoveRange(orphanedSelections);
        SelectedPipelineStep = PipelineSteps.LastOrDefault();
        RefreshAuthoredRecipeState();
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        AppendLog("Teach", $"Removed taught step: {step.ToolName}.");
        return true;
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

    private void RemoveSelectedTeachingSelection()
    {
        if (SelectedStepTeachingSelection is not { } selection)
        {
            AppendLog("Warning", $"Selection delete rejected | role={GetActiveTeachingRoleName()} | reason=no applied selection in the active role.");
            return;
        }

        RemoveTeachingSelection(selection);
    }

    private void RemoveTeachingSelection(ToolRecipeSelection selection)
    {
        var step = SelectedPipelineStep;
        var wasDualRoi = IsSelectedStepDualRoiMeasurement && !IsSelectedStepCompletenessGridUsingEditableRegion;
        var removedMeasurementRole = wasDualRoi && isPlaneFlatnessMeasurementRole;
        var otherRoleSelection = removedMeasurementRole
            ? PlaneFlatnessReferenceSelection
            : PlaneFlatnessMeasurementSelection;
        var referenceSelectionId = PlaneFlatnessReferenceSelection?.Id;
        var measurementSelectionId = PlaneFlatnessMeasurementSelection?.Id;
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
                if (!removedMeasurementRole && measurementSelectionId is not null)
                {
                    // A Reference delete leaves a storage-valid but execution-incomplete
                    // dual-ROI draft. Keep the document at the schema that introduced
                    // DualRoiRouting so the surviving Measurement role remains explicit
                    // without claiming the draft has newer output-policy semantics.
                    SetRecipeSchemaVersion(ToolRecipeDocument.DualRoiRoutingSchemaVersion);
                }
                else
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
        RefreshTeachingSelectionContext();
        AdvancePlaneFlatnessTeachingRole();
        AppendLog("Teach", $"Routed existing selection '{selection.Name}' to {SelectedPipelineStep.ToolName}.");
    }

    private void PersistSelectionForSelectedStep(ToolRecipeSelection selection)
    {
        if (SelectedPipelineStep is null)
        {
            return;
        }

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
        if (string.Equals(
            selection.Kind,
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
            StringComparison.Ordinal))
        {
            MarkLandmarkCorrespondencePreviewStaleIfNeeded();
        }
        RefreshTeachingSelectionContext();
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
        ClearFilterPreview(previewStatus);
        ClearRemoveOutlierPreview(previewStatus);
        ClearConnectedRegionPreview(previewStatus);
        ClearDomainMaskPreview(previewStatus);
        ClearEditableRegionPreview(previewStatus);
        ClearLevelSurfacePreview(previewStatus);
        ClearRoiCropPreview(previewStatus);
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
            Source.Id = document.Source.Id;
            Source.Name = document.Source.Name;
            Source.Format = document.Source.Format;
            Source.Unit = document.Source.Unit;
            Source.FrameId = document.Source.FrameId;
            Source.Path = document.Source.Path;
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
                    sourceStep.OutputEnabled);
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

    private ToolRecipeDocument CreateDocument() => new(
        RecipeSession.SchemaVersion,
        RecipeName.Trim(),
        new ToolRecipeSource(
            Source.Id.Trim(),
            Source.Name.Trim(),
            Source.Format.Trim(),
            Source.Unit.Trim(),
            Source.FrameId.Trim(),
            Source.Path.Trim(),
            SourceSession.SourceBinding is null ? null : new FileInfo(Source.Path.Trim()).Length,
            SourceSession.SourceBinding?.ContentSha256,
            SourceSession.SourceBinding?.GridWidth,
            SourceSession.SourceBinding?.GridHeight,
            SourceSession.SourceAcquisitionProvenance),
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
            step.OutputEnabled)).ToArray(),
        string.Equals(RecipeSession.SchemaVersion, ToolRecipeDocument.LegacySchemaVersion, StringComparison.Ordinal)
            && Selections.Count == 0
                ? null
                : Selections.ToArray());

    private static ToolRecipeDocument ResolveRelativeSourcePath(ToolRecipeDocument document, string documentPath)
    {
        if (string.IsNullOrWhiteSpace(document.Source.Path)
            || Path.IsPathFullyQualified(document.Source.Path))
        {
            return document;
        }

        var documentDirectory = Path.GetDirectoryName(documentPath)
            ?? Environment.CurrentDirectory;
        return document with
        {
            Source = document.Source with
            {
                Path = Path.GetFullPath(Path.Combine(documentDirectory, document.Source.Path))
            }
        };
    }

    private void RefreshRecipeState()
    {
        if (suppressRecipeRefresh)
        {
            return;
        }

        var refreshStarted = Stopwatch.GetTimestamp();
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
        RefreshRemoveOutlierExecutionState();
        RefreshConnectedRegionExecutionState();
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
        NotifyProposedToolRouteChanged();
        addSelectedToolCommand.RaiseCanExecuteChanged();
        RefreshStepCommands();
        lastRecipeNotificationMilliseconds = Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds;
        lastRecipeRefreshMilliseconds = Stopwatch.GetElapsedTime(refreshStarted).TotalMilliseconds;
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
    {
        step.PropertyChanged += OnRecipePartChanged;
        foreach (var parameter in step.Parameters)
        {
            parameter.PropertyChanged += OnRecipePartChanged;
        }
    }

    private void UnsubscribeStep(ToolWorkbenchPipelineStepItem step)
    {
        step.PropertyChanged -= OnRecipePartChanged;
        foreach (var parameter in step.Parameters)
        {
            parameter.PropertyChanged -= OnRecipePartChanged;
        }
    }

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
            or nameof(ToolWorkbenchPipelineStepItem.InputPortState)
            or nameof(ToolWorkbenchPipelineStepItem.InputPortDetail)
            or nameof(ToolWorkbenchPipelineStepItem.InputPortHasIssue)
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
        MarkRemoveOutlierPreviewStaleIfNeeded(sender);
        MarkConnectedRegionPreviewStaleIfNeeded(sender);
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

    private void SetDirty(bool value)
    {
        if (!RecipeSession.SetDirty(value))
        {
            return;
        }

        if (value)
        {
            InvalidateOrderedRun(Localize(
                "레시피가 변경되었습니다. 저장한 뒤 명시적으로 다시 실행하세요.",
                "The recipe changed. Save it, then Run explicitly again."));
        }
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasUncommittedRecipeChanges));
        OnPropertyChanged(nameof(RecipeStateSummary));
        OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
    }

    private void RefreshStepCommands()
    {
        removeSelectedStepCommand.RaiseCanExecuteChanged();
        moveSelectedStepUpCommand.RaiseCanExecuteChanged();
        moveSelectedStepDownCommand.RaiseCanExecuteChanged();
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        runTeachingRecipeCommand?.RaiseCanExecuteChanged();
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
            or nameof(IsOrderedRunRunning))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRecipeMutationBlocked)));
            removeSelectedStepCommand?.RaiseCanExecuteChanged();
            NotifyRecipeHealthSelectionChanged();
        }
    }
}

public sealed record ToolWorkbenchStepRemovalRequestEventArgs(
    string StepId,
    string StepName,
    IReadOnlyList<string> OrphanedSelectionNames);

public sealed record ToolWorkbenchToolItem(
    string Category,
    string Name,
    string Id,
    int MinimumInputCount,
    string InputContract,
    string OutputContract,
    string Description,
    IReadOnlyList<ToolWorkbenchParameterSeed> Parameters);

public sealed record ToolWorkbenchParameterSeed(string Name, string DefaultValue);

public sealed record ToolWorkbenchTeachingSelectionRequirement(
    string Name,
    string Kind,
    int RequiredPointCount,
    bool UsesViewerCapture,
    string Description);

public sealed class ToolWorkbenchTeachingCaptureRequestEventArgs(
    string stepId,
    string selectionId,
    string selectionName,
    string kind,
    int requiredPointCount,
    string rootSourceId,
    string frameId,
    ToolRecipeSelectionSourceBinding sourceBinding,
    ToolRecipeSelection? existingSelection) : EventArgs
{
    public string StepId { get; } = stepId;
    public string SelectionId { get; } = selectionId;
    public string SelectionName { get; } = selectionName;
    public string Kind { get; } = kind;
    public int RequiredPointCount { get; } = requiredPointCount;
    public string RootSourceId { get; } = rootSourceId;
    public string FrameId { get; } = frameId;
    public ToolRecipeSelectionSourceBinding SourceBinding { get; } = sourceBinding;
    public ToolRecipeSelection? ExistingSelection { get; } = existingSelection;
}

public sealed class ToolWorkbenchGridRectangleDraftChangedEventArgs(
    ToolRecipeGridRectangle rectangle) : EventArgs
{
    public ToolRecipeGridRectangle Rectangle { get; } = rectangle;
}

public sealed class ToolWorkbenchGridCircleDraftChangedEventArgs(
    ToolRecipeGridCircle circle) : EventArgs
{
    public ToolRecipeGridCircle Circle { get; } = circle;
}

public sealed class ToolWorkbenchGridPolygonDraftChangedEventArgs(
    ToolRecipeGridPolygon polygon) : EventArgs
{
    public ToolRecipeGridPolygon Polygon { get; } = polygon;
}

public sealed class ToolWorkbenchGridPolygonVertexItem : INotifyPropertyChanged
{
    private double row;
    private double column;

    public ToolWorkbenchGridPolygonVertexItem(
        int order,
        double row,
        double column,
        Action<ToolWorkbenchGridPolygonVertexItem> changed)
    {
        Order = order;
        this.row = row;
        this.column = column;
        Changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal Action<ToolWorkbenchGridPolygonVertexItem>? Changed { get; set; }

    public int Order { get; private set; }

    public double Row
    {
        get => row;
        set
        {
            if (row.Equals(value))
            {
                return;
            }

            row = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Row)));
            Changed?.Invoke(this);
        }
    }

    public double Column
    {
        get => column;
        set
        {
            if (column.Equals(value))
            {
                return;
            }

            column = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Column)));
            Changed?.Invoke(this);
        }
    }

    internal void SetOrder(int order)
    {
        if (Order == order)
        {
            return;
        }

        Order = order;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Order)));
    }
}

public sealed class ToolWorkbenchSourceItem : INotifyPropertyChanged
{
    private string id;
    private string name;
    private string format;
    private string unit;
    private string frameId;
    private string path;

    public ToolWorkbenchSourceItem(string id, string name, string format, string unit, string frameId, string path)
    {
        this.id = id;
        this.name = name;
        this.format = format;
        this.unit = unit;
        this.frameId = frameId;
        this.path = path;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get => id; set => SetField(ref id, value ?? string.Empty); }
    public string Name { get => name; set => SetField(ref name, value ?? string.Empty); }
    public string Format { get => format; set => SetField(ref format, value ?? string.Empty); }
    public string Unit { get => unit; set => SetField(ref unit, value ?? string.Empty); }
    public string FrameId { get => frameId; set => SetField(ref frameId, value ?? string.Empty); }
    public string Path { get => path; set => SetField(ref path, value ?? string.Empty); }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ToolWorkbenchReferenceItem : INotifyPropertyChanged
{
    private string id;
    private string name;
    private string kind;

    public ToolWorkbenchReferenceItem(string id, string name, string kind)
    {
        this.id = id;
        this.name = name;
        this.kind = kind;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get => id; set => SetField(ref id, value ?? string.Empty); }
    public string Name { get => name; set => SetField(ref name, value ?? string.Empty); }
    public string Kind { get => kind; set => SetField(ref kind, value ?? string.Empty); }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ToolWorkbenchPipelineStepItem : INotifyPropertyChanged
{
    private string id;
    private string toolName;
    private string inputEntityIdsText;
    private string outputEntityId;
    private string order = "00";
    private string state = "Taught / pending";
    private string inputPortState = string.Empty;
    private string inputPortDetail = string.Empty;
    private bool inputPortHasIssue;
    private string outputPortState = string.Empty;
    private string outputPortDetail = string.Empty;
    private bool outputPortHasIssue;
    private ToolRecipeDualRoiRouting? dualRoiRouting;
    private bool outputEnabled = true;

    public ToolWorkbenchPipelineStepItem(
        string id,
        ToolWorkbenchToolItem tool,
        string inputEntityIdsText,
        string outputEntityId,
        IReadOnlyList<ToolRecipeParameter>? parameters = null,
        string? toolName = null,
        ToolRecipeDualRoiRouting? dualRoiRouting = null,
        bool outputEnabled = true)
    {
        this.id = id;
        Tool = tool;
        this.toolName = string.IsNullOrWhiteSpace(toolName) ? tool.Name : toolName.Trim();
        this.inputEntityIdsText = inputEntityIdsText;
        this.outputEntityId = outputEntityId;
        this.dualRoiRouting = dualRoiRouting;
        this.outputEnabled = outputEnabled;
        Parameters = new ObservableCollection<ToolWorkbenchParameterItem>(
            parameters is null
                ? tool.Parameters.Select(parameter => new ToolWorkbenchParameterItem(parameter.Name, parameter.DefaultValue))
                : parameters.Select(parameter => new ToolWorkbenchParameterItem(parameter.Name, parameter.Value)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ToolWorkbenchToolItem Tool { get; }
    public string ToolId => Tool.Id;
    public string ToolName
    {
        get => toolName;
        set => SetField(ref toolName, string.IsNullOrWhiteSpace(value) ? Tool.Name : value.Trim());
    }
    public int MinimumInputCount => Tool.MinimumInputCount;
    public string InputContract => Tool.InputContract;
    public string OutputContract => Tool.OutputContract;
    public ObservableCollection<ToolWorkbenchParameterItem> Parameters { get; }

    public string Id { get => id; set => SetField(ref id, value ?? string.Empty); }
    public string Order { get => order; internal set => SetField(ref order, value); }
    public string InputEntityIdsText
    {
        get => inputEntityIdsText;
        set
        {
            if (!SetField(ref inputEntityIdsText, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(InputEntityIds));
            OnPropertyChanged(nameof(InputSummary));
        }
    }

    public IReadOnlyList<string> InputEntityIds => inputEntityIdsText
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToArray();
    public string InputSummary => string.IsNullOrWhiteSpace(InputEntityIdsText) ? "(set input entity IDs)" : InputEntityIdsText;
    public string OutputEntityId { get => outputEntityId; set => SetField(ref outputEntityId, value ?? string.Empty); }
    public bool OutputEnabled
    {
        get => outputEnabled;
        set
        {
            if (outputEnabled == value) return;
            outputEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputEnabled)));
            RaiseCanonicalStatePresentationChanged();
        }
    }
    public ToolRecipeDualRoiRouting? DualRoiRouting
    {
        get => dualRoiRouting;
        set
        {
            if (Equals(dualRoiRouting, value)) return;
            dualRoiRouting = value;
            OnPropertyChanged();
        }
    }
    public string State
    {
        get => state;
        internal set
        {
            if (!SetField(ref state, value)) return;
            RaiseCanonicalStatePresentationChanged();
        }
    }
    public InspectionStepState CanonicalState =>
        OutputEnabled
            ? InspectionStepStateMatrix.Classify(State)
            : InspectionStepState.Incomplete;
    public string CanonicalStateKey =>
        OutputEnabled ? InspectionStepStateMatrix.Describe(State).Key : "disabled";
    public string CanonicalStateLabel =>
        OutputEnabled
            ? ThreeDLocalization.Shared.StateLabel(CanonicalState)
            : ThreeDLocalization.Shared.OutputDisabled;
    public string CanonicalStateAccessibleName =>
        $"{CanonicalStateLabel} ({CanonicalStateKey})";
    public string InputPortState => inputPortState;
    public string InputPortDetail => inputPortDetail;
    public bool InputPortHasIssue => inputPortHasIssue;
    public string OutputPortState => outputPortState;
    public string OutputPortDetail => outputPortDetail;
    public bool OutputPortHasIssue => outputPortHasIssue;

    internal void UpdateFlowPortPresentation(
        string newInputPortState,
        string newInputPortDetail,
        bool newInputPortHasIssue,
        string newOutputPortState,
        string newOutputPortDetail,
        bool newOutputPortHasIssue)
    {
        SetField(ref inputPortState, newInputPortState, nameof(InputPortState));
        SetField(ref inputPortDetail, newInputPortDetail, nameof(InputPortDetail));
        SetField(ref inputPortHasIssue, newInputPortHasIssue, nameof(InputPortHasIssue));
        SetField(ref outputPortState, newOutputPortState, nameof(OutputPortState));
        SetField(ref outputPortDetail, newOutputPortDetail, nameof(OutputPortDetail));
        SetField(ref outputPortHasIssue, newOutputPortHasIssue, nameof(OutputPortHasIssue));
    }

    private bool SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private bool SetField(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void RaiseCanonicalStatePresentationChanged()
    {
        OnPropertyChanged(nameof(CanonicalState));
        OnPropertyChanged(nameof(CanonicalStateKey));
        OnPropertyChanged(nameof(CanonicalStateLabel));
        OnPropertyChanged(nameof(CanonicalStateAccessibleName));
    }

    internal void RefreshLocalizedStatePresentation()
    {
        OnPropertyChanged(nameof(CanonicalStateLabel));
        OnPropertyChanged(nameof(CanonicalStateAccessibleName));
    }
}

public sealed class ToolWorkbenchParameterItem : INotifyPropertyChanged
{
    private string value;

    public ToolWorkbenchParameterItem(string name, string value)
    {
        Name = name;
        this.value = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; }
    public string Value
    {
        get => value;
        set
        {
            var normalized = value ?? string.Empty;
            if (this.value == normalized) return;
            this.value = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
}

public sealed record ToolWorkbenchEntityItem(string Id, string Kind, string State, string Detail);

public sealed record ToolWorkbenchValidationItem(string Level, string Message);

public sealed record ToolWorkbenchC3DSourceStatePerformance(
    double CaptureMilliseconds,
    double ClearPreviewMilliseconds,
    double IdentityMilliseconds,
    double RecipeStateMilliseconds,
    double SelectionSyncMilliseconds,
    double LoggingMilliseconds,
    double TotalMilliseconds);
