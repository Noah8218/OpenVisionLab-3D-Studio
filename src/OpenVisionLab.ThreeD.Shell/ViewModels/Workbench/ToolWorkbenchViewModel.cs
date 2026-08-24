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
public sealed partial class ToolWorkbenchViewModel : INotifyPropertyChanged
{
    internal const int MaximumRunLogEntries = 3000;

    private readonly ToolWorkbenchFilterExecutionOwner filterExecutionOwner;
    private readonly ToolWorkbenchRemoveOutlierExecutionOwner removeOutlierExecutionOwner;
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
    private readonly RelayCommand addSelectedToolCommand;
    private readonly RelayCommand removeSelectedStepCommand;
    private readonly RelayCommand moveSelectedStepUpCommand;
    private readonly RelayCommand moveSelectedStepDownCommand;
    private readonly RelayCommand selectPipelineStepCommand;
    private readonly RelayCommand addReferenceCommand;
    private readonly RelayCommand removeSelectedReferenceCommand;
    private readonly RelayCommand beginTeachingSelectionCaptureCommand;
    private readonly RelayCommand beginAdditionalLevelSurfaceReferenceCommand;
    private readonly RelayCommand undoTeachingSelectionCaptureCommand;
    private readonly RelayCommand cancelTeachingSelectionCaptureCommand;
    private readonly RelayCommand applyTeachingSelectionCaptureCommand;
    private readonly RelayCommand addTeachingGridPolygonVertexCommand;
    private readonly RelayCommand removeTeachingGridPolygonVertexCommand;
    private readonly RelayCommand moveTeachingGridPolygonVertexUpCommand;
    private readonly RelayCommand moveTeachingGridPolygonVertexDownCommand;
    private readonly RelayCommand removeSelectedTeachingSelectionCommand;
    private readonly RelayCommand useExistingTeachingSelectionCommand;
    private readonly RelayCommand addOrUpdateCorrespondenceRowCommand;
    private readonly RelayCommand removeSelectedCorrespondenceRowCommand;
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
    private ToolWorkbenchReferenceItem? selectedReference;
    private ToolRecipeSelection? selectedCompatibleSelection;
    private ToolRecipeLandmarkCorrespondence? selectedCorrespondenceRow;
    private bool suppressRecipeRefresh;
    private bool deferSelectedStepStateRefresh;
    private string newReferenceId = "reference.fixture-landmarks";
    private string newReferenceName = "Fixture landmarks";
    private string newReferenceKind = "Landmark set";
    private bool suppressTeachingGridRectangleDraftChanged;
    private bool suppressTeachingGridCircleDraftChanged;
    private bool suppressTeachingGridPolygonDraftChanged;
    private string correspondenceSourceEntityId = string.Empty;
    private string correspondenceReferenceLandmarkId = "fixture.landmark.01";
    private double correspondenceReferenceX;
    private double correspondenceReferenceY;
    private double correspondenceReferenceZ;
    private string correspondenceReferenceFrameId = "frame.fixture";
    private string correspondenceReferenceUnit = string.Empty;
    private string correspondenceReferenceProvenance = string.Empty;
    private string correspondenceReferenceRevision = string.Empty;
    private double correspondenceMinimumNormalizedTetrahedronVolume;
    private int selectedReviewTabIndex;

    public ToolWorkbenchViewModel(string? recentRecipesPath = null)
    {
        validationSetExecutionOwner = new ToolWorkbenchValidationSetExecutionOwner(
            RefreshValidationSetExecutionState);
        surfaceMatchExperiment = new SurfaceMatchExperimentSession(
            () => string.Equals(
                SelectedPipelineStep?.ToolId,
                "surface-match",
                StringComparison.Ordinal),
            () => HasPendingStepParameterChanges,
            () => SelectedPipelineStep,
            ApplyPublishedSurfaceMatchEvidence,
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
        filterExecutionOwner = new ToolWorkbenchFilterExecutionOwner(
            () => PreviewSelectedStepAsync(),
            CanPreviewSelectedStep,
            () => RunTeachingRecipeAsync(),
            CanRunTeachingRecipe,
            PublishSelectedStep,
            CanPublishSelectedStep,
            CancelSelectedPreview,
            () => IsSelectedStepPreviewRunning,
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
            () => AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty),
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
            outputEntityId => PipelineSteps.FirstOrDefault(item => string.Equals(
                item.OutputEntityId,
                outputEntityId,
                StringComparison.OrdinalIgnoreCase)),
            CreateDocument,
            (category, message) => AppendLog(category, message),
            UpdateMeasurementCompletenessPresentation,
            RefreshHeightMeasurementStateFromOwner);
        InitializeSourceQualityWorkspace();
        OrientedBoxEditor = new OrientedBox3DEditorViewModel();
        InitializeOrientedBox3DEditing();

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
        addReferenceCommand = new RelayCommand(_ => AddReference());
        removeSelectedReferenceCommand = new RelayCommand(_ => RemoveSelectedReference(), _ => SelectedReference is not null);
        beginTeachingSelectionCaptureCommand = new RelayCommand(
            _ => BeginTeachingSelectionCapture(),
            _ => CanBeginTeachingSelectionCapture);
        beginAdditionalLevelSurfaceReferenceCommand = new RelayCommand(
            _ => BeginAdditionalLevelSurfaceReferenceCapture(),
            _ => IsSelectedStepLevelSurface
                 && IsSourceReadyForRecipe
                 && !IsTeachingSelectionCaptureActive);
        undoTeachingSelectionCaptureCommand = new RelayCommand(
            _ => UndoTeachingSelectionCaptureRequested?.Invoke(this, EventArgs.Empty),
            _ => IsTeachingSelectionCaptureActive && TeachingSelectionCapturedPointCount > 0);
        cancelTeachingSelectionCaptureCommand = new RelayCommand(
            _ => CancelActiveSelectionCandidate(),
            _ => IsTeachingSelectionCaptureActive
                || OrientedBoxEditor.CancelCommand.CanExecute(null));
        applyTeachingSelectionCaptureCommand = new RelayCommand(
            _ => ApplyActiveSelectionCandidate(),
            _ => IsTeachingSelectionCaptureActive
                ? CanApplyTeachingSelectionCapture
                  && (SelectedStepSelectionRequirement?.Kind != ToolRecipeSelectionKinds.GridRectangle
                      || IsTeachingGridRectangleDraftValid)
                  && (SelectedStepSelectionRequirement?.Kind != ToolRecipeSelectionKinds.GridCircle
                      || IsTeachingGridCircleDraftValid)
                  && (SelectedStepSelectionRequirement?.Kind != ToolRecipeSelectionKinds.GridPolygon
                      || IsTeachingGridPolygonDraftValid)
                : OrientedBoxEditor.ApplyCommand.CanExecute(null));
        addTeachingGridPolygonVertexCommand = new RelayCommand(
            _ => AddTeachingGridPolygonVertex(),
            _ => IsTeachingGridPolygonEditorEnabled
                && TeachingGridPolygonVertices.Count < ToolRecipeGridPolygonGeometry.MaximumVertexCount);
        removeTeachingGridPolygonVertexCommand = new RelayCommand(
            parameter => RemoveTeachingGridPolygonVertex(parameter as ToolWorkbenchGridPolygonVertexItem),
            parameter => IsTeachingGridPolygonEditorEnabled
                && parameter is ToolWorkbenchGridPolygonVertexItem item
                && TeachingGridPolygonVertices.Contains(item));
        moveTeachingGridPolygonVertexUpCommand = new RelayCommand(
            parameter => MoveTeachingGridPolygonVertex(parameter as ToolWorkbenchGridPolygonVertexItem, -1),
            parameter => CanMoveTeachingGridPolygonVertex(parameter as ToolWorkbenchGridPolygonVertexItem, -1));
        moveTeachingGridPolygonVertexDownCommand = new RelayCommand(
            parameter => MoveTeachingGridPolygonVertex(parameter as ToolWorkbenchGridPolygonVertexItem, 1),
            parameter => CanMoveTeachingGridPolygonVertex(parameter as ToolWorkbenchGridPolygonVertexItem, 1));
        removeSelectedTeachingSelectionCommand = new RelayCommand(
            _ => RemoveSelectedTeachingSelection(),
            _ => SelectedStepTeachingSelection is not null);
        useExistingTeachingSelectionCommand = new RelayCommand(
            _ => UseExistingTeachingSelection(),
            _ => SelectedCompatibleSelection is not null && IsSelectedStepViewerCaptureSupported);
        addOrUpdateCorrespondenceRowCommand = new RelayCommand(
            _ => AddOrUpdateCorrespondenceRow(),
            _ => CanEditCorrespondenceRows);
        removeSelectedCorrespondenceRowCommand = new RelayCommand(
            _ => RemoveSelectedCorrespondenceRow(),
            _ => SelectedCorrespondenceRow is not null && IsSelectedStepCorrespondence);

        InitializePropertyGridEditing();
        InitializeFirstRecipeUx();
        InitializePlaneFlatnessTeaching();
        NewTeachingRecipeCommand = new RelayCommand(_ => BeginFirstRecipeSetup());
        AddSelectedToolCommand = addSelectedToolCommand;
        RemoveSelectedStepCommand = removeSelectedStepCommand;
        MoveSelectedStepUpCommand = moveSelectedStepUpCommand;
        MoveSelectedStepDownCommand = moveSelectedStepDownCommand;
        SelectPipelineStepCommand = selectPipelineStepCommand;
        AddReferenceCommand = addReferenceCommand;
        RemoveSelectedReferenceCommand = removeSelectedReferenceCommand;
        BeginTeachingSelectionCaptureCommand = beginTeachingSelectionCaptureCommand;
        BeginAdditionalLevelSurfaceReferenceCommand =
            beginAdditionalLevelSurfaceReferenceCommand;
        UndoTeachingSelectionCaptureCommand = undoTeachingSelectionCaptureCommand;
        CancelTeachingSelectionCaptureCommand = cancelTeachingSelectionCaptureCommand;
        ApplyTeachingSelectionCaptureCommand = applyTeachingSelectionCaptureCommand;
        AddTeachingGridPolygonVertexCommand = addTeachingGridPolygonVertexCommand;
        RemoveTeachingGridPolygonVertexCommand = removeTeachingGridPolygonVertexCommand;
        MoveTeachingGridPolygonVertexUpCommand = moveTeachingGridPolygonVertexUpCommand;
        MoveTeachingGridPolygonVertexDownCommand = moveTeachingGridPolygonVertexDownCommand;
        RemoveSelectedTeachingSelectionCommand = removeSelectedTeachingSelectionCommand;
        UseExistingTeachingSelectionCommand = useExistingTeachingSelectionCommand;
        AddOrUpdateCorrespondenceRowCommand = addOrUpdateCorrespondenceRowCommand;
        RemoveSelectedCorrespondenceRowCommand = removeSelectedCorrespondenceRowCommand;
        InitializeHeightImageRoiEditing();
        SelectNavigatorItemCommand = new RelayCommand(parameter => SelectNavigatorItem(parameter as ToolWorkbenchNavigatorItem));
        openSelectedToolLabCommand = new RelayCommand(_ => RequestSelectedToolLab(), _ => IsSelectedToolLabAvailable);
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
        InitializeC3DSourceLoading();
        InitializeFilterExecution();
        InitializeOutputCompareSession();
        InitializeViewerWorkspace();
        InitializeSurfaceMatchCollectionNavigation();
        InitializeDisplayedOutputs();
        Localization.PropertyChanged += OnDisplayedOutputsLocalizationChanged;
        InitializeFlowDiagnostics();
        Localization.PropertyChanged += OnFlowDiagnosticsLocalizationChanged;
        InitializeCompatibleToolCatalog();
        Localization.PropertyChanged += OnCompatibleToolCatalogLocalizationChanged;
        Localization.PropertyChanged += OnTeachingLocalizationChanged;
        InitializeValidationSet();
        ValidationWorkspace = new RecipePipelineReviewValidationViewModel(this);
        AppendLog("System", "Tool recipe teaching is ready. Source, routing, parameters, and save/reopen are explicit.");
        SelectedTool = Tools[0];
        RefreshRecipeState();
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
    public event EventHandler? LoadC3DSourceRequested;
    public event EventHandler<ToolWorkbenchTeachingCaptureRequestEventArgs>? BeginTeachingSelectionCaptureRequested;
    public event EventHandler? UndoTeachingSelectionCaptureRequested;
    public event EventHandler? CancelTeachingSelectionCaptureRequested;
    public event EventHandler? ApplyTeachingSelectionCaptureRequested;
    public event EventHandler? AppliedTeachingSelectionsChanged;
    public event EventHandler<ToolWorkbenchGridRectangleDraftChangedEventArgs>? TeachingGridRectangleDraftChanged;
    public event EventHandler<ToolWorkbenchGridCircleDraftChangedEventArgs>? TeachingGridCircleDraftChanged;
    public event EventHandler<ToolWorkbenchGridPolygonDraftChangedEventArgs>? TeachingGridPolygonDraftChanged;
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

    public ObservableCollection<ToolWorkbenchReferenceItem> References { get; } = [];

    public ResettableObservableCollection<ToolWorkbenchEntityItem> Entities { get; } = [];

    public ResettableObservableCollection<ToolWorkbenchArtifactItem> ArtifactRegistry { get; } = [];

    public ResettableObservableCollection<ToolWorkbenchNavigatorItem> NavigatorRoots { get; } = [];

    public ObservableCollection<ToolWorkbenchPipelineStepItem> PipelineSteps { get; } = [];

    public ObservableCollection<ToolRecipeSelection> Selections { get; } = [];

    public ObservableCollection<ToolRecipeSelection> AvailableCompatibleSelections { get; } = [];

    public ObservableCollection<ToolRecipeLandmarkCorrespondence> SelectedCorrespondenceRows { get; } = [];

    public ObservableCollection<ToolWorkbenchGridPolygonVertexItem> TeachingGridPolygonVertices { get; } = [];

    public ObservableCollection<string> AvailableCorrespondenceSourceEntityIds { get; } = [];

    public ObservableCollection<ToolWorkbenchValidationItem> ValidationMessages { get; } = [];

    public ObservableCollection<ToolWorkbenchLogItem> RunLog { get; } = [];

    public ICommand NewTeachingRecipeCommand { get; }
    public ICommand AddSelectedToolCommand { get; }
    public ICommand RemoveSelectedStepCommand { get; }
    public ICommand MoveSelectedStepUpCommand { get; }
    public ICommand MoveSelectedStepDownCommand { get; }
    public ICommand SelectPipelineStepCommand { get; }
    public ICommand AddReferenceCommand { get; }
    public ICommand RemoveSelectedReferenceCommand { get; }
    public ICommand BeginTeachingSelectionCaptureCommand { get; }
    public ICommand BeginAdditionalLevelSurfaceReferenceCommand { get; }
    public ICommand UndoTeachingSelectionCaptureCommand { get; }
    public ICommand CancelTeachingSelectionCaptureCommand { get; }
    public ICommand ApplyTeachingSelectionCaptureCommand { get; }
    public ICommand AddTeachingGridPolygonVertexCommand { get; }
    public ICommand RemoveTeachingGridPolygonVertexCommand { get; }
    public ICommand MoveTeachingGridPolygonVertexUpCommand { get; }
    public ICommand MoveTeachingGridPolygonVertexDownCommand { get; }
    public ICommand RemoveSelectedTeachingSelectionCommand { get; }
    public ICommand UseExistingTeachingSelectionCommand { get; }
    public ICommand AddOrUpdateCorrespondenceRowCommand { get; }
    public ICommand RemoveSelectedCorrespondenceRowCommand { get; }
    public ICommand SelectNavigatorItemCommand { get; }
    public ICommand OpenSelectedToolLabCommand { get; }
    public ICommand ValidateTeachingRecipeCommand { get; }
    public ICommand SaveTeachingRecipeCommand { get; }
    public ICommand SaveTeachingRecipeAsCommand { get; }
    public ICommand OpenToolLibraryCommand { get; }
    public ICommand OpenTeachingRecipeCommand { get; }
    public ICommand LoadC3DSourceCommand { get; private set; } = null!;

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
        get => selectedReference;
        set
        {
            if (ReferenceEquals(selectedReference, value))
            {
                return;
            }

            selectedReference = value;
            OnPropertyChanged();
            removeSelectedReferenceCommand.RaiseCanExecuteChanged();
        }
    }

    public ToolRecipeSelection? SelectedCompatibleSelection
    {
        get => selectedCompatibleSelection;
        set
        {
            if (ReferenceEquals(selectedCompatibleSelection, value))
            {
                return;
            }

            selectedCompatibleSelection = value;
            OnPropertyChanged();
            useExistingTeachingSelectionCommand.RaiseCanExecuteChanged();
            NotifyPlaneFlatnessTeachingState();
        }
    }

    public ToolRecipeLandmarkCorrespondence? SelectedCorrespondenceRow
    {
        get => selectedCorrespondenceRow;
        set
        {
            if (Equals(selectedCorrespondenceRow, value))
            {
                return;
            }

            selectedCorrespondenceRow = value;
            OnPropertyChanged();
            if (value is not null)
            {
                CorrespondenceSourceEntityId = value.SourceEntityId;
                CorrespondenceReferenceLandmarkId = value.ReferenceLandmarkId;
                CorrespondenceReferenceX = value.ReferencePosition.X;
                CorrespondenceReferenceY = value.ReferencePosition.Y;
                CorrespondenceReferenceZ = value.ReferencePosition.Z;
                CorrespondenceReferenceFrameId = value.ReferenceFrameId;
            }

            OnPropertyChanged(nameof(CorrespondenceCommitActionText));
            removeSelectedCorrespondenceRowCommand.RaiseCanExecuteChanged();
        }
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
        get => newReferenceId;
        set => SetField(ref newReferenceId, value ?? string.Empty);
    }

    public string NewReferenceName
    {
        get => newReferenceName;
        set => SetField(ref newReferenceName, value ?? string.Empty);
    }

    public string NewReferenceKind
    {
        get => newReferenceKind;
        set => SetField(ref newReferenceKind, value ?? string.Empty);
    }

    public string CorrespondenceSourceEntityId
    {
        get => correspondenceSourceEntityId;
        set
        {
            if (SetField(ref correspondenceSourceEntityId, value ?? string.Empty))
            {
                addOrUpdateCorrespondenceRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CorrespondenceReferenceLandmarkId
    {
        get => correspondenceReferenceLandmarkId;
        set
        {
            if (SetField(ref correspondenceReferenceLandmarkId, value ?? string.Empty))
            {
                addOrUpdateCorrespondenceRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double CorrespondenceReferenceX
    {
        get => correspondenceReferenceX;
        set => SetField(ref correspondenceReferenceX, value);
    }

    public double CorrespondenceReferenceY
    {
        get => correspondenceReferenceY;
        set => SetField(ref correspondenceReferenceY, value);
    }

    public double CorrespondenceReferenceZ
    {
        get => correspondenceReferenceZ;
        set => SetField(ref correspondenceReferenceZ, value);
    }

    public string CorrespondenceReferenceFrameId
    {
        get => correspondenceReferenceFrameId;
        set
        {
            if (SetField(ref correspondenceReferenceFrameId, value ?? string.Empty))
            {
                addOrUpdateCorrespondenceRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CorrespondenceReferenceUnit
    {
        get => correspondenceReferenceUnit;
        set
        {
            if (SetField(ref correspondenceReferenceUnit, value ?? string.Empty))
            {
                addOrUpdateCorrespondenceRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CorrespondenceReferenceProvenance
    {
        get => correspondenceReferenceProvenance;
        set
        {
            if (SetField(ref correspondenceReferenceProvenance, value ?? string.Empty))
            {
                addOrUpdateCorrespondenceRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CorrespondenceReferenceRevision
    {
        get => correspondenceReferenceRevision;
        set
        {
            if (SetField(ref correspondenceReferenceRevision, value ?? string.Empty))
            {
                addOrUpdateCorrespondenceRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double CorrespondenceMinimumNormalizedTetrahedronVolume
    {
        get => correspondenceMinimumNormalizedTetrahedronVolume;
        set
        {
            if (SetField(ref correspondenceMinimumNormalizedTetrahedronVolume, value))
            {
                addOrUpdateCorrespondenceRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedPipelineStep => SelectedPipelineStep is not null;

    public bool IsRecipeMutationBlocked =>
        IsOrderedRunRunning
        || IsValidationSetRunning
        || IsSurfaceMatchExperimentRunning
        || IsFilterPreviewRunning
        || IsRemoveOutlierPreviewRunning
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
                && SelectionMatchesRequirement(selection, SelectedStepSelectionRequirement));

    public string SelectedStepTeachingSelectionSummary => SelectedStepTeachingSelection is null
        ? (SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
            ? Localization.NoRoiTaught
            : "No recipe-owned selection is routed to this step.")
        : FormatTeachingSelection(SelectedStepTeachingSelection);

    public string SelectionCaptureActionText => SelectedStepTeachingSelection is null
        ? (SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
            ? Localization.CaptureRoi
            : Localization.CaptureSelection)
        : (SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
            ? Localization.ReplaceRoi
            : Localization.ReplaceSelection);

    public string ThicknessRoiTeachingDetail => Localization.ThicknessRoiTeachingDetail;

    public bool IsTeachingSelectionCaptureActive => TeachingCaptureSession.IsActive;

    public bool IsSelectionCandidateActive =>
        IsTeachingSelectionCaptureActive || OrientedBoxEditor.IsDraftOpen;

    public bool IsPipelineReviewExpanded => !IsSelectionCandidateActive && HasPipelineSteps;

    public int TeachingSelectionCapturedPointCount => TeachingCaptureSession.CapturedPointCount;

    public int TeachingSelectionRequiredPointCount => TeachingCaptureSession.RequiredPointCount;

    public bool CanApplyTeachingSelectionCapture => TeachingCaptureSession.CanApply;

    public bool IsTeachingGridRectangleEditorVisible =>
        SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
        && SelectedStepTeachingSelection?.GridRectangle is not null;

    public bool IsTeachingGridRectangleEditorEnabled =>
        IsTeachingSelectionCaptureActive
        && SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
        && TeachingSelectionCapturedPointCount == 2;

    public bool IsTeachingGridCircleEditorVisible =>
        SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridCircle
        && SelectedStepTeachingSelection?.GridCircle is not null;

    public bool IsTeachingGridCircleEditorEnabled =>
        IsTeachingSelectionCaptureActive
        && SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridCircle
        && TeachingSelectionCapturedPointCount == 2;

    public bool IsTeachingGridPolygonEditorVisible =>
        SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridPolygon
        && (IsTeachingSelectionCaptureActive
            || SelectedStepTeachingSelection?.GridPolygon is not null);

    public bool IsTeachingGridPolygonEditorEnabled =>
        IsTeachingSelectionCaptureActive
        && SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridPolygon;

    public bool IsTeachingGridPolygonDraftValid =>
        TryValidateTeachingGridPolygonDraft(out _);

    public string TeachingGridPolygonValidationSummary
    {
        get
        {
            TryValidateTeachingGridPolygonDraft(out var message);
            return message;
        }
    }

    public string TeachingGridPolygonSourceFrameSummary =>
        IsTeachingGridPolygonDraftValid
            ? $"{TeachingGridPolygonVertices.Count} ordered vertex(es) | X=column, Z=row | {SelectedStepTeachingSelection?.FrameId ?? Source.FrameId}"
            : "Polygon vertices must be finite, unique, ordered, non-degenerate, and inside the source grid.";

    public int TeachingGridCircleCenterRow
    {
        get => TeachingCaptureSession.GridCircleDraft.CenterRow;
        set => SetTeachingGridCircleDraftValue(
            TeachingCaptureSession.GridCircleDraft with { CenterRow = value },
            nameof(TeachingGridCircleCenterRow));
    }

    public int TeachingGridCircleCenterColumn
    {
        get => TeachingCaptureSession.GridCircleDraft.CenterColumn;
        set => SetTeachingGridCircleDraftValue(
            TeachingCaptureSession.GridCircleDraft with { CenterColumn = value },
            nameof(TeachingGridCircleCenterColumn));
    }

    public double TeachingGridCircleRadius
    {
        get => TeachingCaptureSession.GridCircleDraft.Radius;
        set => SetTeachingGridCircleDraftValue(
            TeachingCaptureSession.GridCircleDraft with { Radius = value },
            nameof(TeachingGridCircleRadius));
    }

    public bool IsTeachingGridCircleDraftValid =>
        TryValidateTeachingGridCircleDraft(out _);

    public string TeachingGridCircleValidationSummary
    {
        get
        {
            TryValidateTeachingGridCircleDraft(out var message);
            return message;
        }
    }

    public string TeachingGridCircleSourceFrameSummary =>
        IsTeachingGridCircleDraftValid
            ? $"Center X/column {TeachingGridCircleCenterColumn}, Z/row {TeachingGridCircleCenterRow} | radius {TeachingGridCircleRadius:G6} cells | {SelectedStepTeachingSelection?.FrameId ?? Source.FrameId}"
            : "Circular source-grid footprint unavailable until the center and radius are valid.";

    public int TeachingGridRectangleRow
    {
        get => TeachingCaptureSession.GridRectangleDraft.Row;
        set => SetTeachingGridRectangleDraftValue(
            TeachingCaptureSession.GridRectangleDraft with { Row = value },
            nameof(TeachingGridRectangleRow));
    }

    public int TeachingGridRectangleColumn
    {
        get => TeachingCaptureSession.GridRectangleDraft.Column;
        set => SetTeachingGridRectangleDraftValue(
            TeachingCaptureSession.GridRectangleDraft with { Column = value },
            nameof(TeachingGridRectangleColumn));
    }

    public int TeachingGridRectangleRowCount
    {
        get => TeachingCaptureSession.GridRectangleDraft.RowCount;
        set => SetTeachingGridRectangleDraftValue(
            TeachingCaptureSession.GridRectangleDraft with { RowCount = value },
            nameof(TeachingGridRectangleRowCount));
    }

    public int TeachingGridRectangleColumnCount
    {
        get => TeachingCaptureSession.GridRectangleDraft.ColumnCount;
        set => SetTeachingGridRectangleDraftValue(
            TeachingCaptureSession.GridRectangleDraft with { ColumnCount = value },
            nameof(TeachingGridRectangleColumnCount));
    }

    public bool IsTeachingGridRectangleDraftValid =>
        TryValidateTeachingGridRectangleDraft(out _);

    public string TeachingGridRectangleValidationSummary
    {
        get
        {
            TryValidateTeachingGridRectangleDraft(out var message);
            return message;
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
        RefreshSelectedToolWorkspaceProjection();
    }

    public string CorrespondenceCommitActionText => SelectedCorrespondenceRow is null
        ? "Add row"
        : "Update row";

    public string CorrespondenceSelectionSummary => SelectedCorrespondenceRows.Count switch
    {
        0 => "No correspondence rows. Teach exactly four Published CornerAnchor/reference mappings before Preview.",
        < 4 => $"{SelectedCorrespondenceRows.Count}/4 rows taught. Correspondence Preview remains blocked.",
        > 4 => $"{SelectedCorrespondenceRows.Count}/4 rows taught. v1 accepts exactly four; remove the extra rows.",
        _ when string.IsNullOrWhiteSpace(CorrespondenceReferenceUnit)
            || string.IsNullOrWhiteSpace(CorrespondenceReferenceProvenance)
            || string.IsNullOrWhiteSpace(CorrespondenceReferenceRevision)
            || !double.IsFinite(CorrespondenceMinimumNormalizedTetrahedronVolume)
            || CorrespondenceMinimumNormalizedTetrahedronVolume <= 0
            || CorrespondenceMinimumNormalizedTetrahedronVolume >= 1
            => "Four rows exist, but reference unit/provenance/revision and a normalized tetrahedron-volume threshold are required.",
        _ => "Four correspondence rows and reference descriptor are taught. Preview validates only current Published anchors; no affine matrix is calculated."
    };

    private bool CanBeginTeachingSelectionCapture =>
        IsSelectedStepViewerCaptureSupported
        && !IsTeachingSelectionCaptureActive
        && CanUseActivePlaneFlatnessRole()
        && !string.IsNullOrWhiteSpace(Source.Path)
        && SelectedPipelineStep is { } step
        && TryGetSelectionCaptureContext(step, out _, out _);

    private bool CanEditCorrespondenceRows =>
        IsSelectedStepCorrespondence
        && SourceSession.SourceBinding is not null
        && !string.IsNullOrWhiteSpace(CorrespondenceSourceEntityId)
        && !string.IsNullOrWhiteSpace(CorrespondenceReferenceLandmarkId)
        && !string.IsNullOrWhiteSpace(CorrespondenceReferenceFrameId)
        && !string.IsNullOrWhiteSpace(CorrespondenceReferenceUnit)
        && !string.IsNullOrWhiteSpace(CorrespondenceReferenceProvenance)
        && !string.IsNullOrWhiteSpace(CorrespondenceReferenceRevision)
        && double.IsFinite(CorrespondenceMinimumNormalizedTetrahedronVolume)
        && CorrespondenceMinimumNormalizedTetrahedronVolume > 0
        && CorrespondenceMinimumNormalizedTetrahedronVolume < 1
        && double.IsFinite(CorrespondenceReferenceX)
        && double.IsFinite(CorrespondenceReferenceY)
        && double.IsFinite(CorrespondenceReferenceZ);

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
            AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
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
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
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
            SourceSession.SetSourceBinding(TryReadSourceBinding(document.Source.Path));
            BeginSourceQualityLoad();
            RefreshRecipeState();
            RecipePath = fullPath;
            LoadValidationSetDefinition(fullPath, document);
            LoadValidationThresholdCorrectionEvidence(fullPath, document);
            SetDirty(false);
            RecordRecentRecipe(fullPath);
            ToolSearchText = string.Empty;
            AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
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
            References.Clear();
            Selections.Clear();
            PipelineSteps.Clear();
            SelectedPipelineStep = null;
            SelectedReference = null;
            RecipePath = null;
        }, markDirty: false);
        SourceQuality.LoadAcquisitionProvenance(SourceSession.SourceAcquisitionProvenance, Source.FrameId);
        OnPropertyChanged(nameof(SourceAcquisitionProvenance));
        SetDirty(false);
        ClearValidationSet();
        SetValidationSetDefinitionDirty(false);
        ToolSearchText = string.Empty;
        OnPropertyChanged(nameof(RecipeSchemaVersion));
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
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
        switch (SelectedPipelineStep?.ToolId)
        {
            case "filter":
                RefreshFilterExecutionState();
                break;
            case "remove-outlier-pixels":
                RefreshRemoveOutlierExecutionState();
                break;
            case "level-surface":
                RefreshLevelSurfaceExecutionState();
                break;
            case "roi-crop":
                RefreshRoiCropExecutionState();
                break;
            case "height-difference-edge":
                RefreshHeightDifferenceEdgeExecutionState();
                break;
            case "two-point-line":
                RefreshTwoPointLineExecutionState();
                break;
            case "three-point-plane":
                RefreshThreePointPlaneExecutionState();
                break;
            case "datum-plane-raw-height-deviation":
                RefreshDatumPlaneDeviationExecutionState();
                break;
            case "three-d-line-fit":
                RefreshLineFitExecutionState();
                break;
            case "line-intersection":
                RefreshLineIntersectionExecutionState();
                break;
            case "landmark-correspondence":
                RefreshLandmarkCorrespondenceExecutionState();
                break;
            case "xyz-affine-solve":
                RefreshXYZAffineSolveExecutionState();
                break;
            case "xyz-affine-apply":
                RefreshXYZAffineApplyExecutionState();
                break;
            case "re-grid-height-map":
                RefreshRegridHeightFieldExecutionState();
                break;
            case "surface-match":
                RefreshSurfaceMatchExperimentState();
                break;
            case "thickness":
            case "warpage":
            case "plane-flatness":
            case "point-pair-dimensions":
            case "gap-flush":
            case "volume":
            case "cross-section-dimensions":
            case "completeness-grid":
                RefreshMeasurementExecutionState();
                break;
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

    private void RefreshLevelSurfaceStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepLevelSurface));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsLevelSurfacePreviewRunning));
        OnPropertyChanged(nameof(LevelSurfaceExecutionSummary));
        OnPropertyChanged(nameof(LevelSurfaceReferenceSummary));
        OnPropertyChanged(nameof(LevelSurfaceTransformSummary));
        OnPropertyChanged(nameof(LevelSurfaceResidualSummary));
        OnPropertyChanged(nameof(LevelSurfaceOutputSummary));
        OnPropertyChanged(nameof(CurrentLevelSurfacePreviewOutput));
        OnPropertyChanged(nameof(CurrentLevelSurfaceTransform));
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
            SetSelectedCompletenessCellId(null);
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
        foreach (var orphan in orphanedSelections)
        {
            Selections.Remove(orphan);
        }
        SelectedPipelineStep = PipelineSteps.LastOrDefault();
        RefreshAuthoredRecipeState();
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
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

    private void AddReference()
    {
        var id = string.IsNullOrWhiteSpace(NewReferenceId)
            ? $"reference.{NormalizeId(NewReferenceName)}"
            : NewReferenceId.Trim();
        var reference = new ToolWorkbenchReferenceItem(
            id,
            string.IsNullOrWhiteSpace(NewReferenceName) ? id : NewReferenceName.Trim(),
            string.IsNullOrWhiteSpace(NewReferenceKind) ? "Reference" : NewReferenceKind.Trim());
        reference.PropertyChanged += OnRecipePartChanged;
        References.Add(reference);
        SelectedReference = reference;
        NewReferenceId = $"reference.{NormalizeId(NewReferenceName)}";
        RefreshAuthoredRecipeState();
        AppendLog("Teach", $"Declared reference: {reference.Id}.");
    }

    private void RemoveSelectedReference()
    {
        if (SelectedReference is null)
        {
            return;
        }

        var reference = SelectedReference;
        reference.PropertyChanged -= OnRecipePartChanged;
        References.Remove(reference);
        SelectedReference = References.LastOrDefault();
        RefreshAuthoredRecipeState();
        AppendLog("Teach", $"Removed reference: {reference.Id}.");
    }

    private void BeginTeachingSelectionCapture()
    {
        var step = SelectedPipelineStep;
        var requirement = SelectedStepSelectionRequirement;
        if (step is null
            || requirement is not { UsesViewerCapture: true }
            || !TryGetSelectionCaptureContext(step, out var captureBinding, out var captureFrameId))
        {
            AppendLog(
                "Warning",
                $"Selection capture rejected | step={step?.Id ?? "(none)"} | role={GetActiveTeachingRoleName()} | reason=missing step, Viewer requirement, or current source binding.");
            return;
        }

        var existing = TeachingCaptureSession.IsAdditionalLevelSurfaceReference
            ? null
            : SelectedStepTeachingSelection;
        TeachingCaptureSession.SetOwningStep(step.Id);
        UpdateTeachingGridRectangleDraft(existing?.GridRectangle);
        UpdateTeachingGridPolygonDraft(existing?.GridPolygon);
        SetTeachingSelectionCaptureState(
            active: true,
            capturedPointCount: 0,
            requiredPointCount: requirement.RequiredPointCount,
            canApply: false,
            message: $"Pick the first {captureBinding.Format} grid cell.");
        BeginTeachingSelectionCaptureRequested?.Invoke(
            this,
            new ToolWorkbenchTeachingCaptureRequestEventArgs(
                step.Id,
                existing?.Id ?? CreateSelectionId(step, requirement),
                existing?.Name ?? (TeachingCaptureSession.IsAdditionalLevelSurfaceReference
                    ? $"Level Surface reference {Math.Max(2, step.InputEntityIds.Count)}"
                    : IsSelectedStepDualRoiMeasurement
                        ? CreatePlaneFlatnessSelectionName(step)
                        : $"{step.ToolName} selection"),
                requirement.Kind,
                requirement.RequiredPointCount,
                Source.Id,
                captureFrameId,
                captureBinding,
                existing));
        if (IsTeachingSelectionCaptureActive)
        {
            // The Viewer may switch the displayed artifact before it starts the new
            // capture. That transition clears the preceding Viewer state, so commit
            // the owning step only after the synchronous begin request has settled.
            TeachingCaptureSession.SetOwningStep(step.Id);
        }
        AppendLog(
            "Teach",
            $"Selection capture started | step={step.Id} | tool={step.ToolId} | role={GetActiveTeachingRoleName()} | selection={existing?.Id ?? CreateSelectionId(step, requirement)} | kind={requirement.Kind} | requiredPoints={requirement.RequiredPointCount} | existing={existing is not null} | inspectionRun=false.");
    }

    private void CancelTeachingSelectionCapture()
    {
        if (!IsTeachingSelectionCaptureActive)
        {
            return;
        }

        CancelTeachingSelectionCaptureRequested?.Invoke(this, EventArgs.Empty);
        ClearTeachingSelectionCaptureState("Capture cancelled; no recipe geometry changed.");
        AppendLog("Teach", "Selection capture cancelled; authored recipe unchanged.");
    }

    private void BeginAdditionalLevelSurfaceReferenceCapture()
    {
        TeachingCaptureSession.BeginAdditionalLevelSurfaceReference();
        BeginTeachingSelectionCapture();
    }

    public void UpdateTeachingSelectionCaptureState(
        bool active,
        int capturedPointCount,
        int requiredPointCount,
        bool canApply,
        string message)
    {
        if (!active)
        {
            ClearTeachingSelectionCaptureState(message);
            return;
        }

        SetTeachingSelectionCaptureState(
            active,
            Math.Max(0, capturedPointCount),
            Math.Max(1, requiredPointCount),
            canApply,
            string.IsNullOrWhiteSpace(message) ? "Capture in progress." : message);
    }

    public void UpdateTeachingGridRectangleDraft(ToolRecipeGridRectangle? rectangle)
    {
        suppressTeachingGridRectangleDraftChanged = true;
        try
        {
            TeachingCaptureSession.SetGridRectangleDraft(rectangle);
            OnPropertyChanged(nameof(TeachingGridRectangleRow));
            OnPropertyChanged(nameof(TeachingGridRectangleColumn));
            OnPropertyChanged(nameof(TeachingGridRectangleRowCount));
            OnPropertyChanged(nameof(TeachingGridRectangleColumnCount));
            RefreshTeachingGridRectangleDraftState();
        }
        finally
        {
            suppressTeachingGridRectangleDraftChanged = false;
        }

        RefreshHeightImageRoiProjection();
    }

    public void UpdateTeachingGridCircleDraft(ToolRecipeGridCircle? circle)
    {
        suppressTeachingGridCircleDraftChanged = true;
        try
        {
            TeachingCaptureSession.SetGridCircleDraft(circle);
            OnPropertyChanged(nameof(TeachingGridCircleCenterRow));
            OnPropertyChanged(nameof(TeachingGridCircleCenterColumn));
            OnPropertyChanged(nameof(TeachingGridCircleRadius));
            RefreshTeachingGridCircleDraftState();
        }
        finally
        {
            suppressTeachingGridCircleDraftChanged = false;
        }
    }

    public void UpdateTeachingGridPolygonDraft(ToolRecipeGridPolygon? polygon)
    {
        suppressTeachingGridPolygonDraftChanged = true;
        try
        {
            TeachingCaptureSession.SetGridPolygonDraft(polygon);
            foreach (var vertex in TeachingGridPolygonVertices)
            {
                vertex.Changed = null;
            }

            TeachingGridPolygonVertices.Clear();
            foreach (var (vertex, index) in (polygon?.Vertices ?? []).Select((vertex, index) => (vertex, index)))
            {
                var item = new ToolWorkbenchGridPolygonVertexItem(
                    index + 1,
                    vertex.Row,
                    vertex.Column,
                    OnTeachingGridPolygonVertexChanged);
                TeachingGridPolygonVertices.Add(item);
            }
            RefreshTeachingGridPolygonDraftState();
        }
        finally
        {
            suppressTeachingGridPolygonDraftChanged = false;
        }
    }

    public void RejectTeachingSelectionCapture(string message)
    {
        ClearTeachingSelectionCaptureState(message);
        AppendLog("Warning", message);
    }

    public bool TryApplyCapturedTeachingSelection(ToolRecipeSelection? selection, out string message)
    {
        var step = SelectedPipelineStep;
        var requirement = SelectedStepSelectionRequirement;
        if (!IsTeachingSelectionCaptureActive)
        {
            message = "The teaching capture is no longer active.";
            AppendLog("Warning", $"Selection apply rejected | role={GetActiveTeachingRoleName()} | reason={message}");
            return false;
        }
        if (step is null || requirement is not { UsesViewerCapture: true })
        {
            message = "The selected recipe step no longer supports Viewer teaching capture.";
            AppendLog("Warning", $"Selection apply rejected | role={GetActiveTeachingRoleName()} | reason={message}");
            return false;
        }
        if (!string.Equals(TeachingCaptureSession.OwningStepId, step.Id, StringComparison.OrdinalIgnoreCase))
        {
            message = $"The teaching capture belongs to '{TeachingCaptureSession.OwningStepId ?? "(none)"}', not the selected step '{step.Id}'.";
            AppendLog("Warning", $"Selection apply rejected | role={GetActiveTeachingRoleName()} | reason={message}");
            return false;
        }

        if (selection is null
            || !SelectionMatchesRequirement(selection, requirement)
            || !string.Equals(selection.RootSourceId, Source.Id, StringComparison.OrdinalIgnoreCase)
            || !TryGetSelectionCaptureContext(step, out var expectedBinding, out var expectedFrameId)
            || !string.Equals(selection.FrameId, expectedFrameId, StringComparison.Ordinal)
            || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(selection.SourceBinding, expectedBinding))
        {
            message = "The captured selection kind, owner artifact, bytes, grid, or frame does not match the selected step.";
            AppendLog("Warning", $"Selection apply rejected | step={step.Id} | role={GetActiveTeachingRoleName()} | selection={selection?.Id ?? "(none)"} | reason={message}");
            return false;
        }

        PersistSelectionForSelectedStep(selection);
        ClearTeachingSelectionCaptureState("Selection applied to the authored recipe.");
        AdvancePlaneFlatnessTeachingRole();
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
        message = $"Selection applied: {selection.Name}";
        AppendLog(
            "Teach",
            $"{message} | step={step.Id} | role={GetAppliedTeachingRoleName(step, selection.Id)} | geometry={FormatSelectionGeometryForLog(selection)} | route={string.Join(';', step.InputEntityIds)} | inspectionRun=false.");
        return true;
    }

    public IReadOnlyList<ToolRecipeSelection> GetCurrentAppliedTeachingSelections() => Selections
        .Where(IsSelectionCurrent)
        .ToArray();

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
        if (IsSelectedStepDualRoiMeasurement)
        {
            SetPlaneFlatnessTeachingRole(IsMeasurementRoleSelection(step, selectionId));
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

        var step = SelectedPipelineStep;
        var wasDualRoi = IsSelectedStepDualRoiMeasurement;
        var removedMeasurementRole = wasDualRoi && isPlaneFlatnessMeasurementRole;
        var otherRoleSelection = removedMeasurementRole
            ? PlaneFlatnessReferenceSelection
            : PlaneFlatnessMeasurementSelection;
        var referenceSelectionId = PlaneFlatnessReferenceSelection?.Id;
        var measurementSelectionId = PlaneFlatnessMeasurementSelection?.Id;
        MutateRecipe(() =>
        {
            Selections.Remove(selection);
            foreach (var step in PipelineSteps)
            {
                RemoveInputEntity(step, selection.Id);
            }

            if (wasDualRoi && step is not null)
            {
                step.DualRoiRouting = new ToolRecipeDualRoiRouting(
                    removedMeasurementRole ? referenceSelectionId : null,
                    removedMeasurementRole ? null : measurementSelectionId);
                PromoteRecipeSchemaForSelection();
            }
        });
        if (wasDualRoi)
        {
            SetPlaneFlatnessTeachingRole(removedMeasurementRole && otherRoleSelection is not null);
        }
        MarkHeightDifferenceEdgePreviewStaleIfNeeded();
        MarkMeasurementPreviewStaleIfNeeded();
        RefreshTeachingSelectionContext();
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
        AppendLog(
            "Teach",
            $"Selection deleted | step={step?.Id ?? "(none)"} | role={(removedMeasurementRole ? "measurement" : wasDualRoi ? "reference" : "selection")} | selection={selection.Id} | geometry={FormatSelectionGeometryForLog(selection)} | route={string.Join(';', step?.InputEntityIds ?? [])}.");
    }

    private void UseExistingTeachingSelection()
    {
        if (SelectedCompatibleSelection is not { } selection
            || SelectedPipelineStep is null
            || !SelectionMatchesRequirement(selection, SelectedStepSelectionRequirement))
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

    private void AddOrUpdateCorrespondenceRow()
    {
        if (!CanEditCorrespondenceRows || SelectedPipelineStep is null || SourceSession.SourceBinding is null)
        {
            return;
        }

        var row = new ToolRecipeLandmarkCorrespondence(
            CorrespondenceSourceEntityId.Trim(),
            CorrespondenceReferenceLandmarkId.Trim(),
            new ToolRecipeXyz(
                CorrespondenceReferenceX,
                CorrespondenceReferenceY,
                CorrespondenceReferenceZ),
            CorrespondenceReferenceFrameId.Trim());
        var existingSelection = SelectedStepTeachingSelection;
        var rows = existingSelection?.Rows?.ToList() ?? [];
        if (SelectedCorrespondenceRow is { } selectedRow)
        {
            var index = rows.FindIndex(item => Equals(item, selectedRow));
            if (index >= 0)
            {
                rows[index] = row;
            }
            else
            {
                rows.Add(row);
            }
        }
        else
        {
            rows.Add(row);
        }

        var requirement = SelectedStepSelectionRequirement!;
        var descriptor = new ToolRecipeLandmarkCorrespondenceDescriptor(
            CorrespondenceReferenceFrameId.Trim(),
            CorrespondenceReferenceUnit.Trim(),
            CorrespondenceReferenceProvenance.Trim(),
            CorrespondenceReferenceRevision.Trim(),
            "ExactlyFour",
            "CurrentPublishedCornerAnchor",
            "RequireNonDegenerateTetrahedra",
            CorrespondenceMinimumNormalizedTetrahedronVolume);
        var selection = new ToolRecipeSelection(
            existingSelection?.Id ?? CreateSelectionId(SelectedPipelineStep, requirement),
            existingSelection?.Name ?? $"{SelectedPipelineStep.ToolName} correspondences",
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
            Source.Id,
            Source.FrameId,
            SourceSession.SourceBinding,
            null,
            null,
            rows,
            descriptor);
        PersistSelectionForSelectedStep(selection);
        SelectedCorrespondenceRow = null;
        ResetCorrespondenceEditor();
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
        AppendLog("Teach", $"Correspondence row authored for {SelectedPipelineStep.ToolName}; no affine calculation was run.");
    }

    private void RemoveSelectedCorrespondenceRow()
    {
        if (SelectedCorrespondenceRow is not { } selectedRow
            || SelectedStepTeachingSelection is not { } selection)
        {
            return;
        }

        var rows = (selection.Rows ?? [])
            .Where(row => !Equals(row, selectedRow))
            .ToArray();
        if (rows.Length == 0)
        {
            RemoveSelectedTeachingSelection();
        }
        else
        {
            PersistSelectionForSelectedStep(selection with { Rows = rows });
            AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
        }

        SelectedCorrespondenceRow = null;
        ResetCorrespondenceEditor();
    }

    private void PersistSelectionForSelectedStep(ToolRecipeSelection selection)
    {
        if (SelectedPipelineStep is null)
        {
            return;
        }

        MutateRecipe(() =>
        {
            var existing = Selections.FirstOrDefault(item =>
                string.Equals(item.Id, selection.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var index = Selections.IndexOf(existing);
                Selections[index] = selection;
            }
            else
            {
                Selections.Add(selection);
            }

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

            foreach (var existing in References)
            {
                existing.PropertyChanged -= OnRecipePartChanged;
            }

            References.Clear();
            foreach (var reference in document.References)
            {
                var item = new ToolWorkbenchReferenceItem(reference.Id, reference.Name, reference.Kind);
                item.PropertyChanged += OnRecipePartChanged;
                References.Add(item);
            }

            Selections.Clear();
            foreach (var selection in document.Selections ?? [])
            {
                Selections.Add(selection);
            }

            foreach (var existing in PipelineSteps)
            {
                UnsubscribeStep(existing);
            }

            PipelineSteps.Clear();
            foreach (var sourceStep in document.Steps)
            {
                var definition = Tools.FirstOrDefault(tool => string.Equals(tool.Id, sourceStep.ToolId, StringComparison.OrdinalIgnoreCase))
                    ?? new ToolWorkbenchToolItem("Imported", sourceStep.ToolName, sourceStep.ToolId, sourceStep.MinimumInputCount, "Imported input", "Imported output", "Imported teaching step with no local catalog adapter.", []);
                var item = new ToolWorkbenchPipelineStepItem(
                    sourceStep.Id,
                    definition,
                    string.Join("; ", sourceStep.InputEntityIds),
                    sourceStep.OutputEntityId,
                    sourceStep.Parameters,
                    sourceStep.ToolName,
                    sourceStep.DualRoiRouting);
                SubscribeStep(item);
                PipelineSteps.Add(item);
            }

            SelectedPipelineStep = string.IsNullOrWhiteSpace(selectedStepId)
                ? PipelineSteps.FirstOrDefault()
                : PipelineSteps.FirstOrDefault(step =>
                    string.Equals(step.Id, selectedStepId, StringComparison.OrdinalIgnoreCase))
                  ?? PipelineSteps.FirstOrDefault();
            SelectedReference = References.FirstOrDefault();
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
        References.Select(reference => new ToolRecipeReference(reference.Id.Trim(), reference.Name.Trim(), reference.Kind.Trim())).ToArray(),
        PipelineSteps.Select(step => new ToolRecipeStep(
            step.Id.Trim(),
            step.ToolId,
            step.ToolName,
            step.MinimumInputCount,
            step.InputEntityIds.ToArray(),
            step.OutputEntityId.Trim(),
            step.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Value)).ToArray(),
            step.DualRoiRouting)).ToArray(),
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
        AvailableCompatibleSelections.Clear();
        var requirement = SelectedStepSelectionRequirement;
        foreach (var selection in Selections.Where(selection =>
                     SelectionMatchesRequirement(selection, requirement)
                     && IsSelectionCurrent(selection)))
        {
            AvailableCompatibleSelections.Add(selection);
        }

        if (SelectedCompatibleSelection is null
            || !AvailableCompatibleSelections.Contains(SelectedCompatibleSelection))
        {
            SelectedCompatibleSelection = AvailableCompatibleSelections.FirstOrDefault();
        }

        SelectedCorrespondenceRows.Clear();
        foreach (var row in SelectedStepTeachingSelection?.Rows ?? [])
        {
            SelectedCorrespondenceRows.Add(row);
        }

        if (SelectedCorrespondenceRow is not null
            && !SelectedCorrespondenceRows.Contains(SelectedCorrespondenceRow))
        {
            SelectedCorrespondenceRow = null;
        }

        AvailableCorrespondenceSourceEntityIds.Clear();
        if (SelectedPipelineStep is not null)
        {
            foreach (var step in PipelineSteps)
            {
                if (ReferenceEquals(step, SelectedPipelineStep))
                {
                    break;
                }

                if (string.Equals(step.ToolId, "line-intersection", StringComparison.Ordinal))
                {
                    AvailableCorrespondenceSourceEntityIds.Add(step.OutputEntityId);
                }
            }
        }

        if (SelectedStepTeachingSelection?.CorrespondenceDescriptor is { } descriptor)
        {
            CorrespondenceReferenceFrameId = descriptor.ReferenceFrameId;
            CorrespondenceReferenceUnit = descriptor.ReferenceUnit;
            CorrespondenceReferenceProvenance = descriptor.ReferenceProvenance;
            CorrespondenceReferenceRevision = descriptor.ReferenceRevision;
            CorrespondenceMinimumNormalizedTetrahedronVolume = descriptor.MinimumNormalizedTetrahedronVolume ?? 0;
        }

        if (!IsTeachingSelectionCaptureActive)
        {
            UpdateTeachingGridRectangleDraft(SelectedStepTeachingSelection?.GridRectangle);
            UpdateTeachingGridCircleDraft(SelectedStepTeachingSelection?.GridCircle);
            UpdateTeachingGridPolygonDraft(SelectedStepTeachingSelection?.GridPolygon);
        }

        if (string.IsNullOrWhiteSpace(CorrespondenceSourceEntityId)
            || !AvailableCorrespondenceSourceEntityIds.Contains(CorrespondenceSourceEntityId, StringComparer.OrdinalIgnoreCase))
        {
            CorrespondenceSourceEntityId = AvailableCorrespondenceSourceEntityIds.FirstOrDefault() ?? string.Empty;
        }

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
        beginTeachingSelectionCaptureCommand.RaiseCanExecuteChanged();
        beginAdditionalLevelSurfaceReferenceCommand.RaiseCanExecuteChanged();
        removeSelectedTeachingSelectionCommand.RaiseCanExecuteChanged();
        useExistingTeachingSelectionCommand.RaiseCanExecuteChanged();
        addOrUpdateCorrespondenceRowCommand.RaiseCanExecuteChanged();
        removeSelectedCorrespondenceRowCommand.RaiseCanExecuteChanged();
        SynchronizeInspectionWorkspace();
    }

    private IReadOnlyList<string> ValidateSelectionSourceBindings()
    {
        if (Selections.Count == 0)
        {
            return [];
        }

        var errors = new List<string>();
        foreach (var selection in Selections)
        {
            if (!string.Equals(selection.RootSourceId, Source.Id, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Selection '{selection.Id}' does not match root source '{Source.Id}'.");
                continue;
            }

            if (string.Equals(selection.SourceBinding.Format, "TransformedHeightField", StringComparison.Ordinal))
            {
                if (TryGetPublishedRegridHeightFieldOutput(selection.SourceBinding.OwnerEntityId ?? string.Empty, out var output)
                    && output is not null
                    && !ToolRecipeSelectionSourceBindingVerifier.Verify(output, selection.SourceBinding).IsCurrent)
                {
                    errors.Add($"Selection '{selection.Id}' is stale because its Published TransformedHeightField identity changed.");
                }
                continue;
            }

            if (string.Equals(selection.SourceBinding.Format, "HeightField", StringComparison.Ordinal))
            {
                if (!TryGetPublishedRoiCropOutput(selection.SourceBinding.OwnerEntityId ?? string.Empty, out var output)
                    || output is null
                    || !ToolRecipeSelectionSourceBindingVerifier.Verify(output, selection.SourceBinding).IsCurrent)
                {
                    errors.Add($"Selection '{selection.Id}' is stale because its Published HeightField identity is unavailable or changed.");
                }
                continue;
            }

            if (SourceSession.SourceBinding is null)
            {
                errors.Add($"Selection '{selection.Id}' cannot be verified because the C3D source identity is unavailable.");
                continue;
            }

            if (!string.Equals(selection.FrameId, Source.FrameId, StringComparison.OrdinalIgnoreCase)
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(selection.SourceBinding, SourceSession.SourceBinding))
            {
                errors.Add($"Selection '{selection.Id}' is stale because the C3D source bytes or grid dimensions changed.");
            }
        }

        return errors;
    }

    private bool IsSelectionCurrent(ToolRecipeSelection selection)
    {
        if (!string.Equals(selection.RootSourceId, Source.Id, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(selection.SourceBinding.Format, "TransformedHeightField", StringComparison.Ordinal))
        {
            return TryGetPublishedRegridHeightFieldOutput(selection.SourceBinding.OwnerEntityId ?? string.Empty, out var output)
                && output is not null
                && ToolRecipeSelectionSourceBindingVerifier.Verify(output, selection.SourceBinding).IsCurrent;
        }
        if (string.Equals(selection.SourceBinding.Format, "HeightField", StringComparison.Ordinal))
        {
            return TryGetPublishedRoiCropOutput(selection.SourceBinding.OwnerEntityId ?? string.Empty, out var output)
                && output is not null
                && ToolRecipeSelectionSourceBindingVerifier.Verify(output, selection.SourceBinding).IsCurrent;
        }
        return SourceSession.SourceBinding is not null
            && string.Equals(selection.FrameId, Source.FrameId, StringComparison.OrdinalIgnoreCase)
            && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(selection.SourceBinding, SourceSession.SourceBinding);
    }

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

    private void SetTeachingSelectionCaptureState(
        bool active,
        int capturedPointCount,
        int requiredPointCount,
        bool canApply,
        string message)
    {
        TeachingCaptureSession.SetState(active, capturedPointCount, requiredPointCount, canApply);
        NotifyTeachingSelectionCaptureStateChanged();
    }

    private void NotifyTeachingSelectionCaptureStateChanged()
    {
        OnPropertyChanged(nameof(IsTeachingSelectionCaptureActive));
        OnPropertyChanged(nameof(IsSelectionCandidateActive));
        OnPropertyChanged(nameof(IsPipelineReviewExpanded));
        OnPropertyChanged(nameof(TeachingSelectionCapturedPointCount));
        OnPropertyChanged(nameof(TeachingSelectionRequiredPointCount));
        OnPropertyChanged(nameof(CanApplyTeachingSelectionCapture));
        OnPropertyChanged(nameof(IsTeachingGridRectangleEditorEnabled));
        OnPropertyChanged(nameof(IsTeachingGridCircleEditorVisible));
        OnPropertyChanged(nameof(IsTeachingGridCircleEditorEnabled));
        OnPropertyChanged(nameof(IsTeachingGridPolygonEditorVisible));
        OnPropertyChanged(nameof(IsTeachingGridPolygonEditorEnabled));
        OnPropertyChanged(nameof(TeachingSelectionCaptureTitle));
        OnPropertyChanged(nameof(TeachingSelectionCaptureProgress));
        OnPropertyChanged(nameof(TeachingSelectionCaptureInstruction));
        RefreshTeachingSelectionCaptureCommands();
        beginAdditionalLevelSurfaceReferenceCommand.RaiseCanExecuteChanged();
        RefreshSelectedToolWorkspaceProjection();
    }

    private void ClearTeachingSelectionCaptureState(string message)
    {
        TeachingCaptureSession.Clear();
        NotifyTeachingSelectionCaptureStateChanged();
        UpdateTeachingGridRectangleDraft(SelectedStepTeachingSelection?.GridRectangle);
        UpdateTeachingGridCircleDraft(SelectedStepTeachingSelection?.GridCircle);
        UpdateTeachingGridPolygonDraft(SelectedStepTeachingSelection?.GridPolygon);
    }

    private void SetTeachingGridRectangleDraftValue(
        ToolRecipeGridRectangle rectangle,
        string propertyName)
    {
        if (TeachingCaptureSession.GridRectangleDraft == rectangle)
        {
            return;
        }

        TeachingCaptureSession.SetGridRectangleDraft(rectangle);
        OnPropertyChanged(propertyName);
        RefreshTeachingGridRectangleDraftState();
        if (suppressTeachingGridRectangleDraftChanged
            || !IsTeachingGridRectangleEditorEnabled
            || !TryValidateTeachingGridRectangleDraft(out _))
        {
            return;
        }

        TeachingGridRectangleDraftChanged?.Invoke(
            this,
            new ToolWorkbenchGridRectangleDraftChangedEventArgs(rectangle));
    }

    private void RefreshTeachingGridRectangleDraftState()
    {
        OnPropertyChanged(nameof(IsTeachingGridRectangleDraftValid));
        OnPropertyChanged(nameof(TeachingGridRectangleValidationSummary));
        OnPropertyChanged(nameof(TeachingGridRectangleSourceFrameSummary));
        RefreshTeachingSelectionCaptureCommands();
    }

    private void SetTeachingGridCircleDraftValue(
        ToolRecipeGridCircle circle,
        string propertyName)
    {
        if (TeachingCaptureSession.GridCircleDraft == circle)
        {
            return;
        }

        TeachingCaptureSession.SetGridCircleDraft(circle);
        OnPropertyChanged(propertyName);
        RefreshTeachingGridCircleDraftState();
        if (suppressTeachingGridCircleDraftChanged
            || !IsTeachingGridCircleEditorEnabled
            || !TryValidateTeachingGridCircleDraft(out _))
        {
            return;
        }

        TeachingGridCircleDraftChanged?.Invoke(
            this,
            new ToolWorkbenchGridCircleDraftChangedEventArgs(circle));
    }

    private void RefreshTeachingGridCircleDraftState()
    {
        OnPropertyChanged(nameof(IsTeachingGridCircleDraftValid));
        OnPropertyChanged(nameof(TeachingGridCircleValidationSummary));
        OnPropertyChanged(nameof(TeachingGridCircleSourceFrameSummary));
        RefreshTeachingSelectionCaptureCommands();
    }

    private void OnTeachingGridPolygonVertexChanged(ToolWorkbenchGridPolygonVertexItem item)
    {
        if (suppressTeachingGridPolygonDraftChanged)
        {
            return;
        }

        UpdateTeachingGridPolygonDraftFromItems();
    }

    private void AddTeachingGridPolygonVertex()
    {
        if (!IsTeachingGridPolygonEditorEnabled
            || TeachingGridPolygonVertices.Count >= ToolRecipeGridPolygonGeometry.MaximumVertexCount)
        {
            return;
        }

        var binding = SelectedStepTeachingSelection?.SourceBinding ?? SourceSession.SourceBinding;
        var last = TeachingGridPolygonVertices.LastOrDefault();
        var row = last?.Row ?? (binding is null ? 0 : Math.Max(0, (binding.GridHeight - 1) / 2.0));
        var column = last?.Column ?? (binding is null ? 0 : Math.Max(0, (binding.GridWidth - 1) / 2.0));
        if (last is not null)
        {
            var maxRow = binding is null ? row + 1 : Math.Max(0, binding.GridHeight - 1);
            var maxColumn = binding is null ? column + 1 : Math.Max(0, binding.GridWidth - 1);
            row = Math.Min(maxRow, row + 1);
            column = Math.Min(maxColumn, column + 1);
        }

        var item = new ToolWorkbenchGridPolygonVertexItem(
            TeachingGridPolygonVertices.Count + 1,
            row,
            column,
            OnTeachingGridPolygonVertexChanged);
        TeachingGridPolygonVertices.Add(item);
        UpdateTeachingGridPolygonDraftFromItems();
    }

    private void RemoveTeachingGridPolygonVertex(ToolWorkbenchGridPolygonVertexItem? item)
    {
        if (!IsTeachingGridPolygonEditorEnabled
            || item is null
            || !TeachingGridPolygonVertices.Remove(item))
        {
            return;
        }

        ReindexTeachingGridPolygonVertices();
        UpdateTeachingGridPolygonDraftFromItems();
    }

    private bool CanMoveTeachingGridPolygonVertex(
        ToolWorkbenchGridPolygonVertexItem? item,
        int offset)
    {
        if (!IsTeachingGridPolygonEditorEnabled || item is null)
        {
            return false;
        }

        var index = TeachingGridPolygonVertices.IndexOf(item);
        var target = index + offset;
        return index >= 0 && target >= 0 && target < TeachingGridPolygonVertices.Count;
    }

    private void MoveTeachingGridPolygonVertex(
        ToolWorkbenchGridPolygonVertexItem? item,
        int offset)
    {
        if (!CanMoveTeachingGridPolygonVertex(item, offset) || item is null)
        {
            return;
        }

        TeachingGridPolygonVertices.Move(
            TeachingGridPolygonVertices.IndexOf(item),
            TeachingGridPolygonVertices.IndexOf(item) + offset);
        ReindexTeachingGridPolygonVertices();
        UpdateTeachingGridPolygonDraftFromItems();
    }

    private void ReindexTeachingGridPolygonVertices()
    {
        for (var index = 0; index < TeachingGridPolygonVertices.Count; index++)
        {
            TeachingGridPolygonVertices[index].SetOrder(index + 1);
        }
    }

    private void UpdateTeachingGridPolygonDraftFromItems()
    {
        TeachingCaptureSession.SetGridPolygonDraft(CreateGridPolygonDraftFromItems());
        RefreshTeachingGridPolygonDraftState();
        if (!IsTeachingGridPolygonEditorEnabled
            || !TryValidateTeachingGridPolygonDraft(out _))
        {
            return;
        }

        TeachingGridPolygonDraftChanged?.Invoke(
            this,
            new ToolWorkbenchGridPolygonDraftChangedEventArgs(
                TeachingCaptureSession.GridPolygonDraft));
    }

    private ToolRecipeGridPolygon CreateGridPolygonDraftFromItems() =>
        new(TeachingGridPolygonVertices
            .Select(vertex => new ToolRecipeGridPolygonVertex(vertex.Row, vertex.Column))
            .ToArray());

    private void RefreshTeachingGridPolygonDraftState()
    {
        OnPropertyChanged(nameof(IsTeachingGridPolygonDraftValid));
        OnPropertyChanged(nameof(TeachingGridPolygonValidationSummary));
        OnPropertyChanged(nameof(TeachingGridPolygonSourceFrameSummary));
        RefreshTeachingSelectionCaptureCommands();
        addTeachingGridPolygonVertexCommand.RaiseCanExecuteChanged();
        removeTeachingGridPolygonVertexCommand.RaiseCanExecuteChanged();
        moveTeachingGridPolygonVertexUpCommand.RaiseCanExecuteChanged();
        moveTeachingGridPolygonVertexDownCommand.RaiseCanExecuteChanged();
    }

    private bool TryValidateTeachingGridPolygonDraft(out string message)
    {
        var binding = SelectedStepTeachingSelection?.SourceBinding ?? SourceSession.SourceBinding;
        if (binding is null)
        {
            message = "The selected polygon has no current source-grid identity.";
            return false;
        }

        var errors = ToolRecipeGridPolygonGeometry.Validate(
            TeachingCaptureSession.GridPolygonDraft,
            binding.GridWidth,
            binding.GridHeight);
        if (errors.Count > 0)
        {
            message = string.Join(" ", errors.Select(error => $"{error}."));
            return false;
        }

        message = "Valid ordered source-grid polygon. Apply remains explicit and does not run inspection.";
        return true;
    }

    private bool TryValidateTeachingGridCircleDraft(out string message)
    {
        var binding = SelectedStepTeachingSelection?.SourceBinding ?? SourceSession.SourceBinding;
        if (binding is null)
        {
            message = "The selected circle has no current source-grid identity.";
            return false;
        }

        var errors = ToolRecipeGridCircleGeometry.Validate(
            TeachingCaptureSession.GridCircleDraft,
            binding.GridWidth,
            binding.GridHeight);
        if (errors.Count > 0)
        {
            message = string.Join(" ", errors.Select(error => $"{error}."));
            return false;
        }

        message = "Valid circular source-grid footprint. Apply remains explicit and does not run inspection.";
        return true;
    }

    private bool TryValidateTeachingGridRectangleDraft(out string message)
    {
        var binding = SelectedStepTeachingSelection?.SourceBinding ?? SourceSession.SourceBinding;
        if (binding is null || binding.GridWidth <= 0 || binding.GridHeight <= 0)
        {
            message = "The selected ROI has no current source-grid identity.";
            return false;
        }
        if (TeachingGridRectangleRow < 0
            || TeachingGridRectangleColumn < 0
            || TeachingGridRectangleRowCount <= 0
            || TeachingGridRectangleColumnCount <= 0)
        {
            message = "Row and column must be zero or greater; width and height must be greater than zero.";
            return false;
        }
        if ((long)TeachingGridRectangleRow + TeachingGridRectangleRowCount > binding.GridHeight
            || (long)TeachingGridRectangleColumn + TeachingGridRectangleColumnCount > binding.GridWidth)
        {
            message = $"ROI must stay inside rows 0..{binding.GridHeight - 1} and columns 0..{binding.GridWidth - 1}.";
            return false;
        }
        if (IsSelectedStepCrossSectionDimensions
            && (TeachingGridRectangleRowCount != 1 || TeachingGridRectangleColumnCount < 2))
        {
            message = "Cross-section Dimensions requires one row and at least two columns.";
            return false;
        }

        message = "Valid source-grid footprint. Apply remains explicit and does not run inspection.";
        return true;
    }

    private void RefreshTeachingSelectionCaptureCommands()
    {
        beginTeachingSelectionCaptureCommand.RaiseCanExecuteChanged();
        undoTeachingSelectionCaptureCommand.RaiseCanExecuteChanged();
        cancelTeachingSelectionCaptureCommand.RaiseCanExecuteChanged();
        applyTeachingSelectionCaptureCommand.RaiseCanExecuteChanged();
    }

    private void PromoteRecipeSchemaForSelection()
    {
        if (string.Equals(RecipeSession.SchemaVersion, ToolRecipeDocument.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            return;
        }

        RecipeSession.SetSchemaVersion(ToolRecipeDocument.CurrentSchemaVersion);
        OnPropertyChanged(nameof(RecipeSchemaVersion));
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
        ToolWorkbenchPipelineStepItem? step)
    {
        if (step is null)
        {
            return null;
        }

        var presentation = step.ToolId switch
        {
            "roi-crop" => new ToolWorkbenchTeachingSelectionRequirement("Grid rectangle", string.Empty, 0, true, "Pick two opposite grid-cell corners for the crop ROI."),
            "height-difference-edge" => new("Edge search band", string.Empty, 0, true, "Pick two opposite grid-cell corners for the explicit edge search band."),
            "level-surface" => new("Level reference ROI", string.Empty, 0, true, "Pick two opposite grid-cell corners on a stable reference surface. Additional reference ROIs may be routed to the same step."),
            "thickness" => CreatePlaneFlatnessSelectionRequirement(),
            "warpage" => new("Warpage measurement ROI", string.Empty, 0, true, "Pick two opposite grid-cell corners for the measurement ROI."),
            "plane-flatness" => CreatePlaneFlatnessSelectionRequirement(),
            "point-pair-dimensions" => new("Point pair", string.Empty, 0, true, "Pick exactly two distinct cells in the Published TransformedHeightField."),
            "gap-flush" => CreatePlaneFlatnessSelectionRequirement(),
            "volume" => CreatePlaneFlatnessSelectionRequirement(),
            "cross-section-dimensions" => new(Localization.CrossSectionSelection, string.Empty, 0, true, Localization.CrossSectionSelectionDetail),
            "completeness-grid" => CreatePlaneFlatnessSelectionRequirement(),
            "two-point-line" => new("Line points", string.Empty, 0, true, "Pick exactly two distinct C3D grid cells."),
            "three-point-plane" => new("Plane points", string.Empty, 0, true, "Pick exactly three distinct, non-collinear C3D grid cells."),
            "datum-plane-raw-height-deviation" => new("Datum measurement ROI", string.Empty, 0, true, "Pick two opposite grid-cell corners for raw-height residual measurement."),
            "grid-circle-authoring" => new("Circular surface ROI", string.Empty, 0, true, "Pick the center cell, then one boundary cell. Radius is measured between grid-cell centers."),
            "grid-polygon-authoring" => new("Irregular surface region", string.Empty, 3, true, "Pick three or more ordered grid vertices. This slice stores the outline only; no mask or inspection is generated."),
            "landmark-correspondence" => new("Landmark correspondences", string.Empty, 0, false, "Enter explicit source entities and fixture coordinates."),
            _ => null
        };
        if (presentation is null)
        {
            return null;
        }

        var inputIndex = step.ToolId switch
        {
            "landmark-correspondence" => 0,
            "datum-plane-raw-height-deviation" => 2,
            "level-surface" => Math.Max(1, step.InputEntityIds.Count),
            "thickness" or "plane-flatness" or "gap-flush" or "volume" or "completeness-grid"
                => isPlaneFlatnessMeasurementRole ? 2 : 1,
            _ => 1
        };
        return ToolRecipeSelectionContract.TryGetRequirement(step.ToolId, inputIndex, out var requirement)
            ? presentation with
            {
                Kind = requirement.Kind,
                    RequiredPointCount = requirement.RequiredPointCount > 0
                    ? requirement.RequiredPointCount
                    : requirement.Kind is ToolRecipeSelectionKinds.GridRectangle or ToolRecipeSelectionKinds.GridCircle ? 2
                        : requirement.Kind == ToolRecipeSelectionKinds.GridPolygon ? 3 : 0
            }
            : null;
    }

    private static bool SelectionMatchesRequirement(
        ToolRecipeSelection selection,
        ToolWorkbenchTeachingSelectionRequirement? requirement)
    {
        if (requirement is null
            || !string.Equals(selection.Kind, requirement.Kind, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return requirement.Kind switch
        {
            ToolRecipeSelectionKinds.GridRectangle => selection.GridRectangle is not null,
            ToolRecipeSelectionKinds.GridCircle => selection.GridCircle is not null,
            ToolRecipeSelectionKinds.GridPolygon => selection.GridPolygon is not null,
            ToolRecipeSelectionKinds.PointSet => selection.Points?.Count == requirement.RequiredPointCount,
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet => selection.Rows is not null,
            _ => false
        };
    }

    private string CreateSelectionId(
        ToolWorkbenchPipelineStepItem step,
        ToolWorkbenchTeachingSelectionRequirement requirement)
    {
        var suffix = IsSelectedStepDualRoiMeasurement
            ? IsSelectedStepGapFlush
                ? (isPlaneFlatnessMeasurementRole ? "second-roi" : "first-roi")
                : IsSelectedStepCompletenessGrid
                    ? (isPlaneFlatnessMeasurementRole ? "inspection-grid-roi" : "reference-roi")
                    : (isPlaneFlatnessMeasurementRole ? "measurement-roi" : "reference-roi")
            : requirement.Kind switch
        {
            ToolRecipeSelectionKinds.GridRectangle => "roi",
            ToolRecipeSelectionKinds.GridPolygon => "polygon",
            ToolRecipeSelectionKinds.PointSet => "points",
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet => "correspondences",
            _ => "selection"
        };
        var baseId =
            $"selection.{NormalizeId(step.Id.StartsWith("step.", StringComparison.OrdinalIgnoreCase) ? step.Id[5..] : step.Id)}.{suffix}";
        if (!TeachingCaptureSession.IsAdditionalLevelSurfaceReference
            || !string.Equals(step.ToolId, "level-surface", StringComparison.Ordinal))
        {
            return baseId;
        }

        var ordinal = 2;
        var candidate = $"{baseId}.{ordinal:D2}";
        while (Selections.Any(selection => string.Equals(
                   selection.Id,
                   candidate,
                   StringComparison.OrdinalIgnoreCase)))
        {
            ordinal++;
            candidate = $"{baseId}.{ordinal:D2}";
        }

        return candidate;
    }

    private static string FormatTeachingSelection(ToolRecipeSelection selection)
    {
        var geometry = selection.GridRectangle is { } rectangle
            ? $"row {rectangle.Row}..{rectangle.Row + rectangle.RowCount - 1}, column {rectangle.Column}..{rectangle.Column + rectangle.ColumnCount - 1}"
            : selection.GridCircle is { } circle
                ? $"center row {circle.CenterRow}, column {circle.CenterColumn}, radius {circle.Radius:G6} cells"
            : selection.GridPolygon is { Vertices: { } vertices }
                ? $"{vertices.Count} ordered vertices ({FormatGridPolygonVertex(vertices.FirstOrDefault())} → {FormatGridPolygonVertex(vertices.LastOrDefault())})"
            : selection.Points is { } points
                ? $"{points.Count} grid point(s)"
                : selection.Rows is { } rows
                    ? $"{rows.Count} correspondence row(s)"
                    : "geometry unavailable";
        var hash = selection.SourceBinding.ContentSha256.Length >= 8
            ? selection.SourceBinding.ContentSha256[..8]
            : selection.SourceBinding.ContentSha256;
        return $"{selection.Name} | {geometry} | {selection.FrameId} | sha256 {hash}";
    }

    private static string FormatGridPolygonVertex(ToolRecipeGridPolygonVertex? vertex) =>
        vertex is null ? "(none)" : $"X {vertex.Column:G6}, Z {vertex.Row:G6}";

    private void ResetCorrespondenceEditor()
    {
        CorrespondenceSourceEntityId = AvailableCorrespondenceSourceEntityIds.FirstOrDefault() ?? string.Empty;
        CorrespondenceReferenceLandmarkId = "fixture.landmark.01";
        CorrespondenceReferenceX = 0;
        CorrespondenceReferenceY = 0;
        CorrespondenceReferenceZ = 0;
    }

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

    private void OnRecipePartChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ToolWorkbenchPipelineStepItem.State))
        {
            OnPropertyChanged(nameof(AlignmentStatusSummary));
            RebuildRecipeHealthProjection();
            return;
        }

        if (args.PropertyName is nameof(ToolWorkbenchPipelineStepItem.InputPortState)
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
    {
        var level = category.Equals("Error", StringComparison.OrdinalIgnoreCase)
            ? LogLevel.Error
            : category.Equals("Warning", StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Warning
                : LogLevel.Info;
        OVLog.Write(LogCategory.UI, level, $"Workbench[{category}] {message}");
        RunLog.Insert(0, new ToolWorkbenchLogItem(DateTime.Now.ToString("HH:mm:ss"), category, message));
        while (RunLog.Count > MaximumRunLogEntries)
        {
            RunLog.RemoveAt(RunLog.Count - 1);
        }
    }

    private string GetActiveTeachingRoleName() =>
        IsSelectedStepDualRoiMeasurement
            ? isPlaneFlatnessMeasurementRole ? "measurement" : "reference"
            : "selection";

    private static string FormatGridRectangleForLog(ToolRecipeGridRectangle? rectangle) =>
        rectangle is null
            ? "(none)"
            : $"row={rectangle.Row},column={rectangle.Column},rowCount={rectangle.RowCount},columnCount={rectangle.ColumnCount}";

    private static string FormatSelectionGeometryForLog(ToolRecipeSelection selection) =>
        selection.GridPolygon is { Vertices: { } vertices }
            ? $"polygonVertices={vertices.Count};first={FormatGridPolygonVertex(vertices.FirstOrDefault())};last={FormatGridPolygonVertex(vertices.LastOrDefault())}"
            : selection.GridCircle is { } circle
                ? $"circleCenter=({circle.CenterRow},{circle.CenterColumn});radius={circle.Radius:G6}"
                : $"rectangle={FormatGridRectangleForLog(selection.GridRectangle)}";

    private static string GetAppliedTeachingRoleName(ToolWorkbenchPipelineStepItem step, string selectionId)
    {
        if (step.DualRoiRouting is { } routing)
        {
            if (string.Equals(routing.FirstRegionSelectionId, selectionId, StringComparison.OrdinalIgnoreCase))
            {
                return "reference";
            }
            if (string.Equals(routing.SecondRegionSelectionId, selectionId, StringComparison.OrdinalIgnoreCase))
            {
                return "measurement";
            }
        }

        var index = step.InputEntityIds
            .Select((id, inputIndex) => (id, inputIndex))
            .FirstOrDefault(item => string.Equals(item.id, selectionId, StringComparison.OrdinalIgnoreCase))
            .inputIndex;
        return index == 1 ? "reference" : index == 2 ? "measurement" : "selection";
    }

    private bool IsMeasurementRoleSelection(ToolWorkbenchPipelineStepItem step, string selectionId)
    {
        if (step.DualRoiRouting is { } routing)
        {
            return string.Equals(
                routing.SecondRegionSelectionId,
                selectionId,
                StringComparison.OrdinalIgnoreCase);
        }

        var inputIndex = step.InputEntityIds
            .Select((id, index) => (id, index))
            .FirstOrDefault(item => string.Equals(item.id, selectionId, StringComparison.OrdinalIgnoreCase))
            .index;
        return inputIndex == 2
            || (IsSelectedStepThickness
                && step.InputEntityIds.Count == 2
                && !ToolRecipeDocument.SupportsArtifactOwnedSelections(RecipeSchemaVersion)
                && inputIndex == 1);
    }

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

    public ToolWorkbenchPipelineStepItem(
        string id,
        ToolWorkbenchToolItem tool,
        string inputEntityIdsText,
        string outputEntityId,
        IReadOnlyList<ToolRecipeParameter>? parameters = null,
        string? toolName = null,
        ToolRecipeDualRoiRouting? dualRoiRouting = null)
    {
        this.id = id;
        Tool = tool;
        this.toolName = string.IsNullOrWhiteSpace(toolName) ? tool.Name : toolName.Trim();
        this.inputEntityIdsText = inputEntityIdsText;
        this.outputEntityId = outputEntityId;
        this.dualRoiRouting = dualRoiRouting;
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
    public string State { get => state; internal set => SetField(ref state, value); }
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

public sealed record ToolWorkbenchLogItem(string Time, string Category, string Message);

public sealed record ToolWorkbenchC3DSourceStatePerformance(
    double CaptureMilliseconds,
    double ClearPreviewMilliseconds,
    double IdentityMilliseconds,
    double RecipeStateMilliseconds,
    double SelectionSyncMilliseconds,
    double LoggingMilliseconds,
    double TotalMilliseconds);
