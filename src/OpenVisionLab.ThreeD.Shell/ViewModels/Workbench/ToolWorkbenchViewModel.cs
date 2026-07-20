using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns teach-time recipe composition only. It never invokes an inspection
/// algorithm; executable adapters remain a later Tools/Runner concern.
/// </summary>
public sealed partial class ToolWorkbenchViewModel : INotifyPropertyChanged
{
    private readonly RelayCommand addSelectedToolCommand;
    private readonly RelayCommand removeSelectedStepCommand;
    private readonly RelayCommand moveSelectedStepUpCommand;
    private readonly RelayCommand moveSelectedStepDownCommand;
    private readonly RelayCommand addReferenceCommand;
    private readonly RelayCommand removeSelectedReferenceCommand;
    private readonly RelayCommand beginTeachingSelectionCaptureCommand;
    private readonly RelayCommand undoTeachingSelectionCaptureCommand;
    private readonly RelayCommand cancelTeachingSelectionCaptureCommand;
    private readonly RelayCommand applyTeachingSelectionCaptureCommand;
    private readonly RelayCommand removeSelectedTeachingSelectionCommand;
    private readonly RelayCommand useExistingTeachingSelectionCommand;
    private readonly RelayCommand addOrUpdateCorrespondenceRowCommand;
    private readonly RelayCommand removeSelectedCorrespondenceRowCommand;
    private readonly RelayCommand openSelectedToolLabCommand;
    private ToolWorkbenchToolItem? selectedTool;
    private ToolWorkbenchPipelineStepItem? selectedPipelineStep;
    private ToolWorkbenchReferenceItem? selectedReference;
    private ToolRecipeSelection? selectedCompatibleSelection;
    private ToolRecipeLandmarkCorrespondence? selectedCorrespondenceRow;
    private ToolRecipeValidationResult validation = new([], []);
    private IReadOnlyList<string> sourceBindingErrors = [];
    private bool suppressRecipeRefresh;
    private string recipeSchemaVersion = ToolRecipeDocument.CurrentSchemaVersion;
    private string recipeName = "Untitled 3D Inspection";
    private string? recipePath;
    private string newReferenceId = "reference.fixture-landmarks";
    private string newReferenceName = "Fixture landmarks";
    private string newReferenceKind = "Landmark set";
    private bool isDirty = true;
    private ToolRecipeSelectionSourceBinding? loadedSourceBinding;
    private bool isTeachingSelectionCaptureActive;
    private string? teachingSelectionCaptureStepId;
    private int teachingSelectionCapturedPointCount;
    private int teachingSelectionRequiredPointCount;
    private bool canApplyTeachingSelectionCapture;
    private string teachingSelectionCaptureMessage = "Capture is inactive.";
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

    public ToolWorkbenchViewModel(string? recentRecipesPath = null)
    {
        this.recentRecipesPath = recentRecipesPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "ThreeDStudio",
            "recent-recipes.json");
        Tools =
        [
            new("Prepare", "Filter", "filter", 1, "HeightField", "FilteredHeightField", "Denoise the C3D raw-height source with the approved Median contract.", [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]),
            new("Prepare", "ROI / Crop", "roi-crop", 1, "HeightField", "HeightField", "Restrict a later tool to an explicit source region.", [new("ROI", "Select in Viewer"), new("Output frame", "Keep source frame")]),
            new("Feature & Datum", "Height Difference Edge", "height-difference-edge", 1, "Published FilteredHeightField + GridRectangle", "EdgePointSet", "Extract one deterministic adjacent-height candidate per scanline in an explicit search band.", [new("ComparisonAxis", "AcrossColumns"), new("Polarity", "Rising"), new("MinimumDelta", "Set explicitly"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")]),
            new("Feature & Datum", "2-Point Line", "two-point-line", 1, "HeightField", "LineFeature", "Construct a named line from two recipe-owned point picks.", [new("Point A", "Pick during teaching"), new("Point B", "Pick during teaching")]),
            new("Feature & Datum", "3-Point Plane", "three-point-plane", 1, "HeightField", "PlaneFeature", "Construct a datum plane from three recipe-owned point picks.", [new("Point A/B/C", "Pick during teaching"), new("Degeneracy", "Reject collinear")]),
            new("Feature & Datum", "3D Line Fit", "three-d-line-fit", 1, "Published EdgePointSet", "LineFeature", "Fit one deterministic full-XYZ line to an explicit published edge-point entity.", [new("FitMethod", "DeterministicConsensusOrthogonalTls"), new("MaximumOrthogonalResidual", "Set explicitly"), new("MinimumInlierCount", "Set explicitly"), new("MinimumInlierRatio", "Set explicitly"), new("MinimumInlierScanlineSpan", "Set explicitly"), new("HypothesisPolicy", "Sha256PairSchedule"), new("MaximumHypotheses", "256"), new("RefinementPolicy", "OrthogonalTlsUntilStable10"), new("DirectionPolicy", "PositiveScanlineAxis"), new("EndpointPolicy", "InlierProjectionExtents")]),
            new("Feature & Datum", "Line Intersection", "line-intersection", 2, "Published LineFeature + LineFeature", "CornerAnchor", "Create a corner anchor only after full-XYZ closest-approach, angle, and bounded-support gates pass.", [new("MaximumClosestApproachDistance", "Set explicitly"), new("MinimumAcuteAngleDegrees", "Set explicitly"), new("MaximumSupportExtension", "Set explicitly"), new("OutputRole", "Set explicitly"), new("ClosestApproachPolicy", "MidpointOfClosestPoints"), new("ParallelPolicy", "RejectBelowMinimumAcuteAngle"), new("SupportPolicy", "WithinInlierProjectionExtentsWithMaximumExtension")]),
            new("Feature & Datum", "Landmark Correspondence", "landmark-correspondence", 1, "Correspondence selection", "CorrespondenceSet", "Validate four named Published CornerAnchors against explicit reference coordinates before a later transform.", [new("PairCountPolicy", "ExactlyFour"), new("SourceArtifactPolicy", "CurrentPublishedCornerAnchor"), new("AffineIndependencePolicy", "RequireNonDegenerateTetrahedra")]),
            new("Transform", "XYZ Affine Transform", "xyz-affine-transform", 1, "CorrespondenceSet", "TransformedPointCloud", "Apply a full XYZ affine transform only after valid source/reference landmarks exist.", [new("Minimum landmarks", "4 affine-independent"), new("Residual", "Review before Run")]),
            new("Transform", "Re-grid Height Map", "re-grid-height-map", 1, "TransformedPointCloud", "TransformedHeightField", "Resample transformed XYZ data into an explicit inspection grid.", [new("Grid frame", "Selected transform"), new("Hole policy", "Explicit")]),
            new("Measure", "Thickness", "thickness", 1, "TransformedHeightField", "MeasurementResult", "Measure thickness from the transformed inspection surface.", [new("ROI", "Recipe-owned"), new("Tolerance", "Set during teaching")]),
            new("Measure", "Warpage", "warpage", 1, "TransformedHeightField", "MeasurementResult", "Measure warpage from the transformed inspection surface.", [new("ROI", "Recipe-owned"), new("P2V limit", "Set during teaching")]),
            new("Review", "Overlay / Control Review", "overlay-control-review", 1, "MeasurementResult", "ReviewOverlay", "Group overlays, controls, acceptance, and run evidence without changing the source.", [new("Overlay", "Selected results"), new("Publish", "Explicit")])
        ];

        Source = new ToolWorkbenchSourceItem(
            "source.c3d.height-map",
            "No C3D source selected",
            "C3D",
            "raw-height",
            "frame.c3d-grid-index",
            string.Empty);
        Source.PropertyChanged += OnRecipePartChanged;

        addSelectedToolCommand = new RelayCommand(_ => AddSelectedTool(), _ => SelectedTool is not null && !string.IsNullOrWhiteSpace(Source.Path));
        removeSelectedStepCommand = new RelayCommand(_ => RemoveSelectedStep(), _ => SelectedPipelineStep is not null);
        moveSelectedStepUpCommand = new RelayCommand(_ => MoveSelectedStep(-1), _ => CanMoveSelectedStep(-1));
        moveSelectedStepDownCommand = new RelayCommand(_ => MoveSelectedStep(1), _ => CanMoveSelectedStep(1));
        addReferenceCommand = new RelayCommand(_ => AddReference());
        removeSelectedReferenceCommand = new RelayCommand(_ => RemoveSelectedReference(), _ => SelectedReference is not null);
        beginTeachingSelectionCaptureCommand = new RelayCommand(
            _ => BeginTeachingSelectionCapture(),
            _ => CanBeginTeachingSelectionCapture);
        undoTeachingSelectionCaptureCommand = new RelayCommand(
            _ => UndoTeachingSelectionCaptureRequested?.Invoke(this, EventArgs.Empty),
            _ => IsTeachingSelectionCaptureActive && TeachingSelectionCapturedPointCount > 0);
        cancelTeachingSelectionCaptureCommand = new RelayCommand(
            _ => CancelTeachingSelectionCapture(),
            _ => IsTeachingSelectionCaptureActive);
        applyTeachingSelectionCaptureCommand = new RelayCommand(
            _ => ApplyTeachingSelectionCaptureRequested?.Invoke(this, EventArgs.Empty),
            _ => IsTeachingSelectionCaptureActive && CanApplyTeachingSelectionCapture);
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
        NewTeachingRecipeCommand = new RelayCommand(_ => NewTeachingRecipeRequested?.Invoke(this, EventArgs.Empty));
        AddSelectedToolCommand = addSelectedToolCommand;
        RemoveSelectedStepCommand = removeSelectedStepCommand;
        MoveSelectedStepUpCommand = moveSelectedStepUpCommand;
        MoveSelectedStepDownCommand = moveSelectedStepDownCommand;
        AddReferenceCommand = addReferenceCommand;
        RemoveSelectedReferenceCommand = removeSelectedReferenceCommand;
        BeginTeachingSelectionCaptureCommand = beginTeachingSelectionCaptureCommand;
        UndoTeachingSelectionCaptureCommand = undoTeachingSelectionCaptureCommand;
        CancelTeachingSelectionCaptureCommand = cancelTeachingSelectionCaptureCommand;
        ApplyTeachingSelectionCaptureCommand = applyTeachingSelectionCaptureCommand;
        RemoveSelectedTeachingSelectionCommand = removeSelectedTeachingSelectionCommand;
        UseExistingTeachingSelectionCommand = useExistingTeachingSelectionCommand;
        AddOrUpdateCorrespondenceRowCommand = addOrUpdateCorrespondenceRowCommand;
        RemoveSelectedCorrespondenceRowCommand = removeSelectedCorrespondenceRowCommand;
        SelectNavigatorItemCommand = new RelayCommand(parameter => SelectNavigatorItem(parameter as ToolWorkbenchNavigatorItem));
        openSelectedToolLabCommand = new RelayCommand(_ => RequestSelectedToolLab(), _ => IsSelectedToolLabAvailable);
        OpenSelectedToolLabCommand = openSelectedToolLabCommand;
        ValidateTeachingRecipeCommand = new RelayCommand(_ => ValidateTeachingRecipe());
        SaveTeachingRecipeCommand = new RelayCommand(_ => SaveTeachingRecipeRequested?.Invoke(this, EventArgs.Empty));
        SaveTeachingRecipeAsCommand = new RelayCommand(_ => SaveTeachingRecipeAsRequested?.Invoke(this, EventArgs.Empty));
        OpenTeachingRecipeCommand = new RelayCommand(_ => OpenTeachingRecipeRequested?.Invoke(this, EventArgs.Empty));
        LoadC3DSourceCommand = new RelayCommand(_ => LoadC3DSourceRequested?.Invoke(this, EventArgs.Empty));
        InitializeFilterExecution();
        InitializeDisplayedOutputs();
        Localization.PropertyChanged += OnDisplayedOutputsLocalizationChanged;
        InitializeLineFitDiagnostics();

        AppendLog("System", "Tool recipe teaching is ready. Source, routing, parameters, and save/reopen are explicit.");
        SelectedTool = Tools[0];
        RefreshRecipeState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? NewTeachingRecipeRequested;
    public event EventHandler? SaveTeachingRecipeRequested;
    public event EventHandler? SaveTeachingRecipeAsRequested;
    public event EventHandler? OpenTeachingRecipeRequested;
    public event EventHandler? LoadC3DSourceRequested;
    public event EventHandler<ToolWorkbenchTeachingCaptureRequestEventArgs>? BeginTeachingSelectionCaptureRequested;
    public event EventHandler? UndoTeachingSelectionCaptureRequested;
    public event EventHandler? CancelTeachingSelectionCaptureRequested;
    public event EventHandler? ApplyTeachingSelectionCaptureRequested;
    public event EventHandler? AppliedTeachingSelectionsChanged;
    public event EventHandler<ToolWorkbenchToolLabRequestEventArgs>? ToolLabRequested;

    public ObservableCollection<ToolWorkbenchToolItem> Tools { get; }

    public ToolWorkbenchSourceItem Source { get; }
    public ThreeDLocalization Localization => ThreeDLocalization.Shared;

    public ObservableCollection<ToolWorkbenchReferenceItem> References { get; } = [];

    public ObservableCollection<ToolWorkbenchEntityItem> Entities { get; } = [];

    public ObservableCollection<ToolWorkbenchArtifactItem> ArtifactRegistry { get; } = [];

    public ObservableCollection<ToolWorkbenchNavigatorItem> NavigatorRoots { get; } = [];

    public ObservableCollection<ToolWorkbenchPipelineStepItem> PipelineSteps { get; } = [];

    public ObservableCollection<ToolRecipeSelection> Selections { get; } = [];

    public ObservableCollection<ToolRecipeSelection> AvailableCompatibleSelections { get; } = [];

    public ObservableCollection<ToolRecipeLandmarkCorrespondence> SelectedCorrespondenceRows { get; } = [];

    public ObservableCollection<string> AvailableCorrespondenceSourceEntityIds { get; } = [];

    public ObservableCollection<ToolWorkbenchValidationItem> ValidationMessages { get; } = [];

    public ObservableCollection<ToolWorkbenchLogItem> RunLog { get; } = [];

    public ICommand NewTeachingRecipeCommand { get; }
    public ICommand AddSelectedToolCommand { get; }
    public ICommand RemoveSelectedStepCommand { get; }
    public ICommand MoveSelectedStepUpCommand { get; }
    public ICommand MoveSelectedStepDownCommand { get; }
    public ICommand AddReferenceCommand { get; }
    public ICommand RemoveSelectedReferenceCommand { get; }
    public ICommand BeginTeachingSelectionCaptureCommand { get; }
    public ICommand UndoTeachingSelectionCaptureCommand { get; }
    public ICommand CancelTeachingSelectionCaptureCommand { get; }
    public ICommand ApplyTeachingSelectionCaptureCommand { get; }
    public ICommand RemoveSelectedTeachingSelectionCommand { get; }
    public ICommand UseExistingTeachingSelectionCommand { get; }
    public ICommand AddOrUpdateCorrespondenceRowCommand { get; }
    public ICommand RemoveSelectedCorrespondenceRowCommand { get; }
    public ICommand SelectNavigatorItemCommand { get; }
    public ICommand OpenSelectedToolLabCommand { get; }
    public ICommand ValidateTeachingRecipeCommand { get; }
    public ICommand SaveTeachingRecipeCommand { get; }
    public ICommand SaveTeachingRecipeAsCommand { get; }
    public ICommand OpenTeachingRecipeCommand { get; }
    public ICommand LoadC3DSourceCommand { get; }

    public ToolWorkbenchToolItem? SelectedTool
    {
        get => selectedTool;
        set
        {
            if (ReferenceEquals(selectedTool, value))
            {
                return;
            }

            selectedTool = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedToolTitle));
            OnPropertyChanged(nameof(SelectedToolHint));
            addSelectedToolCommand.RaiseCanExecuteChanged();

            if (value is not null)
            {
                var message = $"Tool selected: {value.Category} / {value.Name}.";
                AppendLog("UI", message);
                OVLog.Write(LogCategory.UI, LogLevel.Info, message);
            }
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

            selectedPipelineStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedPipelineStep));
            OnPropertyChanged(nameof(SelectedPipelineStepTitle));
            OnPropertyChanged(nameof(AvailableInputEntitiesSummary));
            OnPropertyChanged(nameof(SelectedRouteInputIds));
            OnPropertyChanged(nameof(SelectedRouteOutputId));
            OnPropertyChanged(nameof(IsSelectedToolLabAvailable));
            openSelectedToolLabCommand.RaiseCanExecuteChanged();
            RefreshSelectedStepPropertyDraft();
            RefreshTeachingSelectionContext();
            RefreshFilterExecutionState();
            RefreshHeightDifferenceEdgeExecutionState();
            RefreshLineFitExecutionState();
            RefreshLineIntersectionExecutionState();
            RefreshLandmarkCorrespondenceExecutionState();
            RefreshStepCommands();
            RefreshNavigatorSelection();
        }
    }

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
        get => recipeName;
        set
        {
            var normalized = value ?? string.Empty;
            if (recipeName == normalized)
            {
                return;
            }

            recipeName = normalized;
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
        get => recipePath;
        private set
        {
            if (recipePath == value)
            {
                return;
            }

            recipePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RecipePathSummary));
            OnPropertyChanged(nameof(RecipeStateSummary));
        }
    }

    public bool IsDirty => isDirty;

    public string RecipeSchemaVersion => recipeSchemaVersion;

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

    public bool CanSaveTeachingRecipe => validation.IsValid
        && sourceBindingErrors.Count == 0
        && sourceIdentityErrors.Count == 0;

    public string SelectedToolTitle => SelectedTool is null
        ? "No tool selected"
        : $"{SelectedTool.Category} / {SelectedTool.Name}";

    public string SelectedToolHint => SelectedTool is null
        ? "Select a tool to inspect its typed input, output, and required parameters."
        : "Add this tool to the teaching recipe, then set its entity IDs and parameters. Adding or editing never runs inspection.";

    public string SelectedPipelineStepTitle => SelectedPipelineStep is null
        ? "No taught step selected"
        : $"Step {SelectedPipelineStep.Order}: {SelectedPipelineStep.ToolName}";

    public string ValidationSummary => sourceIdentityErrors.Count > 0
        ? $"Recipe source needs {sourceIdentityErrors.Count} correction(s) before Preview or save."
        : sourceBindingErrors.Count > 0
        ? $"Teaching has {sourceBindingErrors.Count} stale source selection(s); recapture or replace them before saving."
        : validation.IsValid
        ? validation.Warnings.Count == 0
            ? "Teaching recipe is structurally valid. Filter and ready Height Difference Edge rows support explicit Preview; unsupported rows stay blocked."
            : $"Teaching recipe is valid with {validation.Warnings.Count} warning(s). Filter and ready Height Difference Edge rows support explicit Preview; unsupported rows stay blocked."
        : $"Teaching needs {validation.Errors.Count} correction(s) before it can be saved.";

    public string RecipePathSummary => string.IsNullOrWhiteSpace(RecipePath)
        ? "Not saved yet"
        : RecipePath;

    public string RecipeStateSummary
    {
        get
        {
            var validationState = sourceIdentityErrors.Count > 0
                ? $"Source needs {sourceIdentityErrors.Count} correction(s)"
                : sourceBindingErrors.Count > 0
                ? $"{sourceBindingErrors.Count} stale selection(s)"
                : validation.IsValid
                ? validation.Warnings.Count == 0 ? "Valid" : $"Valid, {validation.Warnings.Count} warning(s)"
                : $"{validation.Errors.Count} correction(s)";
            var saveState = IsDirty
                ? "Modified"
                : string.IsNullOrWhiteSpace(RecipePath) ? "Unsaved" : "Saved";
            return $"{validationState} | {saveState}";
        }
    }

    public string SourceContextSummary => string.IsNullOrWhiteSpace(Source.Path)
        ? "Source not loaded"
        : $"{Source.Format} | {Source.Unit} | {Source.FrameId}";

    public string AlignmentStatusSummary => PipelineSteps.Any(step =>
        string.Equals(step.ToolId, "xyz-affine-transform", StringComparison.OrdinalIgnoreCase))
        ? "Alignment taught, not calculated"
        : "Alignment not taught";

    public ToolWorkbenchTeachingSelectionRequirement? SelectedStepSelectionRequirement =>
        CreateSelectionRequirement(SelectedPipelineStep);

    public bool IsSelectedStepViewerCaptureSupported =>
        SelectedStepSelectionRequirement is { UsesViewerCapture: true };

    public bool IsSelectedStepCorrespondence =>
        SelectedStepSelectionRequirement is { Kind: ToolRecipeSelectionKinds.LandmarkCorrespondenceSet };

    public string SelectedStepSelectionRequirementTitle => SelectedStepSelectionRequirement switch
    {
        null => "No Viewer selection required",
        { UsesViewerCapture: true } requirement => $"{requirement.Name} - {requirement.RequiredPointCount} C3D grid pick(s)",
        { Kind: ToolRecipeSelectionKinds.LandmarkCorrespondenceSet } => "Landmark correspondence rows",
        var requirement => requirement.Name
    };

    public string SelectedStepSelectionRequirementSummary => SelectedStepSelectionRequirement switch
    {
        null => "This step consumes the source or earlier typed entities. Selecting or editing it never starts Viewer capture.",
        { UsesViewerCapture: true } requirement => $"{requirement.Description} Capture stores geometry only; it never runs an inspection algorithm.",
        { Kind: ToolRecipeSelectionKinds.LandmarkCorrespondenceSet } => "Enter four Published CornerAnchor mappings, reference frame/unit/provenance/revision, and an explicit non-planarity threshold. Editing never runs the tool.",
        var requirement => requirement.Description
    };

    public ToolRecipeSelection? SelectedStepTeachingSelection => SelectedPipelineStep is null
        ? null
        : Selections.FirstOrDefault(selection =>
            SelectedPipelineStep.InputEntityIds.Contains(selection.Id, StringComparer.OrdinalIgnoreCase)
            && SelectionMatchesRequirement(selection, SelectedStepSelectionRequirement));

    public string SelectedStepTeachingSelectionSummary => SelectedStepTeachingSelection is null
        ? "No recipe-owned selection is routed to this step."
        : FormatTeachingSelection(SelectedStepTeachingSelection);

    public string SelectionCaptureActionText => SelectedStepTeachingSelection is null
        ? "Capture selection"
        : "Replace selection";

    public bool IsTeachingSelectionCaptureActive => isTeachingSelectionCaptureActive;

    public bool IsPipelineReviewExpanded => !IsTeachingSelectionCaptureActive;

    public int TeachingSelectionCapturedPointCount => teachingSelectionCapturedPointCount;

    public int TeachingSelectionRequiredPointCount => teachingSelectionRequiredPointCount;

    public bool CanApplyTeachingSelectionCapture => canApplyTeachingSelectionCapture;

    public string TeachingSelectionCaptureTitle => SelectedStepSelectionRequirement is null
        ? "Teaching selection capture"
        : $"Capture: {SelectedStepSelectionRequirement.Name}";

    public string TeachingSelectionCaptureProgress => IsTeachingSelectionCaptureActive
        ? $"{teachingSelectionCaptureMessage} | {TeachingSelectionCapturedPointCount}/{TeachingSelectionRequiredPointCount} picked | Esc cancels"
        : "Capture is inactive.";

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
        && !string.IsNullOrWhiteSpace(Source.Path)
        && loadedSourceBinding is not null;

    private bool CanEditCorrespondenceRows =>
        IsSelectedStepCorrespondence
        && loadedSourceBinding is not null
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

    public string AvailableInputEntitiesSummary => string.Join(
        ", ",
        EnumerateAvailableEntitiesBefore(SelectedPipelineStep).Select(entity => entity.Id));

    public void SetC3DSource(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (IsTeachingSelectionCaptureActive)
        {
            CancelTeachingSelectionCapture();
        }

        var sourcePathChanged = !string.Equals(Source.Path, fullPath, StringComparison.OrdinalIgnoreCase);
        if (sourcePathChanged)
        {
            ClearFilterPreview("Source changed; Preview is required.");
        }
        loadedSourceBinding = TryReadSourceBinding(fullPath);
        AcceptCurrentSourceIdentity();
        if (!sourcePathChanged)
        {
            RefreshRecipeState();
            AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        MutateRecipe(() =>
        {
            Source.Id = "source.c3d.height-map";
            Source.Name = Path.GetFileNameWithoutExtension(fullPath);
            Source.Format = "C3D";
            Source.Unit = "raw-height";
            Source.FrameId = "frame.c3d-grid-index";
            Source.Path = fullPath;
        });
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
        AppendLog("Source", $"C3D source taught: {Path.GetFileName(fullPath)}.");
        OVLog.Write(LogCategory.UI, LogLevel.Info, $"Tool recipe C3D source selected: {fullPath}");
    }

    public bool TrySaveTeachingRecipe(string path, out string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (HasPendingStepParameterChanges)
        {
            message = "Apply or discard the selected step parameter draft before saving.";
            return false;
        }

        ValidateTeachingRecipe();
        if (!CanSaveTeachingRecipe)
        {
            message = string.Join(Environment.NewLine, validation.Errors.Concat(sourceBindingErrors));
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            ToolRecipeDocumentStore.Save(fullPath, CreateDocument());
            RecipePath = fullPath;
            SetDirty(false);
            RecordRecentRecipe(fullPath);
            message = $"Teaching recipe saved: {Path.GetFileName(fullPath)}";
            AppendLog("Teach", message);
            OVLog.Write(LogCategory.UI, LogLevel.Info, message);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            AppendLog("Error", $"Teaching recipe save failed: {message}");
            OVLog.Write(LogCategory.UI, LogLevel.Error, $"Teaching recipe save failed: {message}");
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
            loadedSourceBinding = TryReadSourceBinding(document.Source.Path);
            RefreshRecipeState();
            RecipePath = fullPath;
            SetDirty(false);
            RecordRecentRecipe(fullPath);
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

    public void CreateNewTeachingRecipe()
    {
        if (IsTeachingSelectionCaptureActive)
        {
            CancelTeachingSelectionCapture();
        }

        MutateRecipe(() =>
        {
            AcceptCurrentSourceIdentity();
            recipeSchemaVersion = ToolRecipeDocument.CurrentSchemaVersion;
            RecipeName = "Untitled 3D Inspection";
            References.Clear();
            Selections.Clear();
            PipelineSteps.Clear();
            SelectedPipelineStep = null;
            SelectedReference = null;
            RecipePath = null;
        });
        OnPropertyChanged(nameof(RecipeSchemaVersion));
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
        AppendLog("Teach", "New teaching recipe created. The selected C3D source was retained.");
    }

    private void AddSelectedTool()
    {
        if (SelectedTool is null)
        {
            return;
        }

        var input = SelectedTool.Id == "landmark-correspondence"
            ? string.Empty
            : PipelineSteps.LastOrDefault()?.OutputEntityId;
        if (SelectedTool.Id != "landmark-correspondence" && string.IsNullOrWhiteSpace(input))
        {
            input = Source.Id;
        }

        var step = new ToolWorkbenchPipelineStepItem(
            CreateUniqueStepId(SelectedTool.Id),
            SelectedTool,
            input ?? string.Empty,
            CreateUniqueOutputId(SelectedTool.OutputContract));
        SubscribeStep(step);
        PipelineSteps.Add(step);
        SelectedPipelineStep = step;
        RefreshAuthoredRecipeState();
        AppendLog("Teach", $"Added taught step: {SelectedTool.Name}.");
    }

    private void RemoveSelectedStep()
    {
        if (SelectedPipelineStep is null)
        {
            return;
        }

        var step = SelectedPipelineStep;
        var routedSelectionIds = step.InputEntityIds
            .Where(input => Selections.Any(selection => string.Equals(selection.Id, input, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        UnsubscribeStep(step);
        PipelineSteps.Remove(step);
        foreach (var selectionId in routedSelectionIds)
        {
            if (!PipelineSteps.Any(item => item.InputEntityIds.Contains(selectionId, StringComparer.OrdinalIgnoreCase)))
            {
                var orphan = Selections.FirstOrDefault(selection =>
                    string.Equals(selection.Id, selectionId, StringComparison.OrdinalIgnoreCase));
                if (orphan is not null)
                {
                    Selections.Remove(orphan);
                }
            }
        }
        SelectedPipelineStep = PipelineSteps.LastOrDefault();
        RefreshAuthoredRecipeState();
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
        AppendLog("Teach", $"Removed taught step: {step.ToolName}.");
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
        if (step is null || requirement is not { UsesViewerCapture: true } || loadedSourceBinding is null)
        {
            return;
        }

        teachingSelectionCaptureStepId = step.Id;
        SetTeachingSelectionCaptureState(
            active: true,
            capturedPointCount: 0,
            requiredPointCount: requirement.RequiredPointCount,
            canApply: false,
            message: "Pick the first C3D grid cell.");
        var existing = SelectedStepTeachingSelection;
        BeginTeachingSelectionCaptureRequested?.Invoke(
            this,
            new ToolWorkbenchTeachingCaptureRequestEventArgs(
                step.Id,
                existing?.Id ?? CreateSelectionId(step, requirement),
                existing?.Name ?? $"{step.ToolName} selection",
                requirement.Kind,
                requirement.RequiredPointCount,
                Source.Id,
                Source.FrameId,
                loadedSourceBinding));
        AppendLog("Teach", $"Selection capture started for {step.ToolName}; no inspection was run.");
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

    public void RejectTeachingSelectionCapture(string message)
    {
        ClearTeachingSelectionCaptureState(message);
        AppendLog("Warning", message);
    }

    public bool TryApplyCapturedTeachingSelection(ToolRecipeSelection? selection, out string message)
    {
        var step = SelectedPipelineStep;
        var requirement = SelectedStepSelectionRequirement;
        if (!IsTeachingSelectionCaptureActive
            || step is null
            || requirement is not { UsesViewerCapture: true }
            || !string.Equals(teachingSelectionCaptureStepId, step.Id, StringComparison.OrdinalIgnoreCase))
        {
            message = "The teaching capture no longer belongs to the selected recipe step.";
            return false;
        }

        if (selection is null
            || !SelectionMatchesRequirement(selection, requirement)
            || !string.Equals(selection.RootSourceId, Source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(selection.FrameId, Source.FrameId, StringComparison.OrdinalIgnoreCase))
        {
            message = "The captured selection kind, source, or frame does not match the selected step.";
            return false;
        }

        var verification = ToolRecipeSelectionSourceBindingVerifier.Verify(Source.Path, selection.SourceBinding);
        if (!verification.IsCurrent)
        {
            message = verification.Message;
            return false;
        }

        PersistSelectionForSelectedStep(selection);
        ClearTeachingSelectionCaptureState("Selection applied to the authored recipe.");
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
        message = $"Selection applied: {selection.Name}";
        AppendLog("Teach", $"{message}; no inspection was run.");
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

    private void RemoveSelectedTeachingSelection()
    {
        if (SelectedStepTeachingSelection is not { } selection)
        {
            return;
        }

        MutateRecipe(() =>
        {
            Selections.Remove(selection);
            foreach (var step in PipelineSteps)
            {
                RemoveInputEntity(step, selection.Id);
            }
        });
        MarkHeightDifferenceEdgePreviewStaleIfNeeded();
        RefreshTeachingSelectionContext();
        AppliedTeachingSelectionsChanged?.Invoke(this, EventArgs.Empty);
        AppendLog("Teach", $"Removed recipe-owned selection: {selection.Name}.");
    }

    private void UseExistingTeachingSelection()
    {
        if (SelectedCompatibleSelection is not { } selection
            || SelectedPipelineStep is null
            || !SelectionMatchesRequirement(selection, SelectedStepSelectionRequirement))
        {
            return;
        }

        MutateRecipe(() => AddInputEntity(SelectedPipelineStep, selection.Id));
        MarkHeightDifferenceEdgePreviewStaleIfNeeded();
        RefreshTeachingSelectionContext();
        AppendLog("Teach", $"Routed existing selection '{selection.Name}' to {SelectedPipelineStep.ToolName}.");
    }

    private void AddOrUpdateCorrespondenceRow()
    {
        if (!CanEditCorrespondenceRows || SelectedPipelineStep is null || loadedSourceBinding is null)
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
            loadedSourceBinding,
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
            PromoteRecipeSchemaForSelection();
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
            else
            {
                AddInputEntity(SelectedPipelineStep, selection.Id);
            }
        });
        MarkHeightDifferenceEdgePreviewStaleIfNeeded();
        RefreshTeachingSelectionContext();
    }

    private void ValidateTeachingRecipe()
    {
        RefreshRecipeState();
        var message = CanSaveTeachingRecipe
            ? "Teaching recipe validation passed."
            : $"Teaching recipe validation found {validation.Errors.Count + sourceBindingErrors.Count} error(s).";
        AppendLog("Validate", message);
        OVLog.Write(LogCategory.UI, CanSaveTeachingRecipe ? LogLevel.Info : LogLevel.Warning, message);
    }

    private void ApplyDocument(ToolRecipeDocument document)
    {
        ClearFilterPreview("Recipe opened; Preview is required.");
        CaptureOpenedSourceIdentity(document.Source);
        MutateRecipe(() =>
        {
            recipeSchemaVersion = document.SchemaVersion;
            RecipeName = document.Name;
            Source.Id = document.Source.Id;
            Source.Name = document.Source.Name;
            Source.Format = document.Source.Format;
            Source.Unit = document.Source.Unit;
            Source.FrameId = document.Source.FrameId;
            Source.Path = document.Source.Path;

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
                    sourceStep.Parameters);
                SubscribeStep(item);
                PipelineSteps.Add(item);
            }

            SelectedPipelineStep = PipelineSteps.FirstOrDefault();
            SelectedReference = References.FirstOrDefault();
        }, markDirty: false);
        OnPropertyChanged(nameof(RecipeSchemaVersion));
    }

    private ToolRecipeDocument CreateDocument() => new(
        recipeSchemaVersion,
        RecipeName.Trim(),
        new ToolRecipeSource(
            Source.Id.Trim(),
            Source.Name.Trim(),
            Source.Format.Trim(),
            Source.Unit.Trim(),
            Source.FrameId.Trim(),
            Source.Path.Trim(),
            loadedSourceBinding is null ? null : new FileInfo(Source.Path.Trim()).Length,
            loadedSourceBinding?.ContentSha256,
            loadedSourceBinding?.GridWidth,
            loadedSourceBinding?.GridHeight),
        References.Select(reference => new ToolRecipeReference(reference.Id.Trim(), reference.Name.Trim(), reference.Kind.Trim())).ToArray(),
        PipelineSteps.Select(step => new ToolRecipeStep(
            step.Id.Trim(),
            step.ToolId,
            step.ToolName,
            step.MinimumInputCount,
            step.InputEntityIds.ToArray(),
            step.OutputEntityId.Trim(),
            step.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Value)).ToArray())).ToArray(),
        string.Equals(recipeSchemaVersion, ToolRecipeDocument.LegacySchemaVersion, StringComparison.Ordinal)
            && Selections.Count == 0
                ? null
                : Selections.ToArray());

    private static ToolRecipeDocument ResolveRelativeSourcePath(ToolRecipeDocument document, string documentPath)
    {
        if (Path.IsPathFullyQualified(document.Source.Path))
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

        for (var index = 0; index < PipelineSteps.Count; index++)
        {
            PipelineSteps[index].Order = (index + 1).ToString("00");
        }

        RefreshSourceIdentityState();
        validation = ToolRecipeValidator.Validate(CreateDocument());
        sourceBindingErrors = ValidateSelectionSourceBindings();
        ValidationMessages.Clear();
        foreach (var error in validation.Errors)
        {
            ValidationMessages.Add(new ToolWorkbenchValidationItem("Error", error));
        }

        foreach (var warning in validation.Warnings)
        {
            ValidationMessages.Add(new ToolWorkbenchValidationItem("Warning", warning));
        }

        foreach (var error in sourceBindingErrors)
        {
            ValidationMessages.Add(new ToolWorkbenchValidationItem("Error", error));
        }

        foreach (var error in sourceIdentityErrors)
        {
            ValidationMessages.Add(new ToolWorkbenchValidationItem("Error", error));
        }

        RebuildEntities();
        RefreshTeachingSelectionContext();
        RefreshFilterExecutionState();
        RefreshHeightDifferenceEdgeExecutionState();
        RefreshLineFitExecutionState();
        RefreshLineIntersectionExecutionState();
        RefreshLandmarkCorrespondenceExecutionState();
        RefreshAdapterCoverage();
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(CanSaveTeachingRecipe));
        OnPropertyChanged(nameof(AvailableInputEntitiesSummary));
        OnPropertyChanged(nameof(PipelineEmptyHint));
        OnPropertyChanged(nameof(IsPipelineEmpty));
        OnPropertyChanged(nameof(RecipeStateSummary));
        OnPropertyChanged(nameof(SourceContextSummary));
        OnPropertyChanged(nameof(AlignmentStatusSummary));
        ((RelayCommand)SaveTeachingRecipeCommand).RaiseCanExecuteChanged();
        addSelectedToolCommand.RaiseCanExecuteChanged();
        RefreshStepCommands();
    }

    private void RebuildEntities()
    {
        Entities.Clear();
        Entities.Add(new ToolWorkbenchEntityItem(
            Source.Id,
            Source.Format,
            string.IsNullOrWhiteSpace(Source.Path) ? "Source required" : "Selected for teaching",
            string.IsNullOrWhiteSpace(Source.Path) ? "Load a C3D source before adding a tool." : $"{Source.Name} | {Source.Unit} | {Source.FrameId}"));
        foreach (var reference in References)
        {
            Entities.Add(new ToolWorkbenchEntityItem(reference.Id, reference.Kind, "Declared reference", reference.Name));
        }

        foreach (var selection in Selections)
        {
            Entities.Add(new ToolWorkbenchEntityItem(
                selection.Id,
                selection.Kind,
                IsSelectionCurrent(selection) ? "Applied teaching selection" : "Stale - recapture required",
                selection.Name));
        }

        foreach (var step in PipelineSteps)
        {
            Entities.Add(new ToolWorkbenchEntityItem(step.OutputEntityId, step.OutputContract, step.State, step.ToolName));
        }

        RebuildArtifactRegistryAndNavigator();
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

        if (string.IsNullOrWhiteSpace(CorrespondenceSourceEntityId)
            || !AvailableCorrespondenceSourceEntityIds.Contains(CorrespondenceSourceEntityId, StringComparer.OrdinalIgnoreCase))
        {
            CorrespondenceSourceEntityId = AvailableCorrespondenceSourceEntityIds.FirstOrDefault() ?? string.Empty;
        }

        OnPropertyChanged(nameof(SelectedStepSelectionRequirement));
        OnPropertyChanged(nameof(IsSelectedStepViewerCaptureSupported));
        OnPropertyChanged(nameof(IsSelectedStepCorrespondence));
        OnPropertyChanged(nameof(SelectedStepSelectionRequirementTitle));
        OnPropertyChanged(nameof(SelectedStepSelectionRequirementSummary));
        OnPropertyChanged(nameof(SelectedStepTeachingSelection));
        OnPropertyChanged(nameof(SelectedStepTeachingSelectionSummary));
        OnPropertyChanged(nameof(HeightDifferenceEdgeBandSummary));
        OnPropertyChanged(nameof(SelectionCaptureActionText));
        OnPropertyChanged(nameof(TeachingSelectionCaptureTitle));
        OnPropertyChanged(nameof(CorrespondenceSelectionSummary));
        beginTeachingSelectionCaptureCommand.RaiseCanExecuteChanged();
        removeSelectedTeachingSelectionCommand.RaiseCanExecuteChanged();
        useExistingTeachingSelectionCommand.RaiseCanExecuteChanged();
        addOrUpdateCorrespondenceRowCommand.RaiseCanExecuteChanged();
        removeSelectedCorrespondenceRowCommand.RaiseCanExecuteChanged();
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
            if (!string.Equals(selection.RootSourceId, Source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.FrameId, Source.FrameId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Selection '{selection.Id}' does not match source '{Source.Id}' and frame '{Source.FrameId}'.");
                continue;
            }

            if (loadedSourceBinding is null)
            {
                errors.Add($"Selection '{selection.Id}' cannot be verified because the C3D source identity is unavailable.");
                continue;
            }

            if (!SourceBindingsEqual(selection.SourceBinding, loadedSourceBinding))
            {
                errors.Add($"Selection '{selection.Id}' is stale because the C3D source bytes or grid dimensions changed.");
            }
        }

        return errors;
    }

    private bool IsSelectionCurrent(ToolRecipeSelection selection) =>
        loadedSourceBinding is not null
        && string.Equals(selection.RootSourceId, Source.Id, StringComparison.OrdinalIgnoreCase)
        && string.Equals(selection.FrameId, Source.FrameId, StringComparison.OrdinalIgnoreCase)
        && SourceBindingsEqual(selection.SourceBinding, loadedSourceBinding);

    private static bool SourceBindingsEqual(
        ToolRecipeSelectionSourceBinding first,
        ToolRecipeSelectionSourceBinding second) =>
        string.Equals(first.Format, second.Format, StringComparison.OrdinalIgnoreCase)
        && string.Equals(first.ContentSha256, second.ContentSha256, StringComparison.OrdinalIgnoreCase)
        && first.GridWidth == second.GridWidth
        && first.GridHeight == second.GridHeight;

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
        isTeachingSelectionCaptureActive = active;
        teachingSelectionCapturedPointCount = capturedPointCount;
        teachingSelectionRequiredPointCount = requiredPointCount;
        canApplyTeachingSelectionCapture = canApply;
        teachingSelectionCaptureMessage = message;
        OnPropertyChanged(nameof(IsTeachingSelectionCaptureActive));
        OnPropertyChanged(nameof(IsPipelineReviewExpanded));
        OnPropertyChanged(nameof(TeachingSelectionCapturedPointCount));
        OnPropertyChanged(nameof(TeachingSelectionRequiredPointCount));
        OnPropertyChanged(nameof(CanApplyTeachingSelectionCapture));
        OnPropertyChanged(nameof(TeachingSelectionCaptureProgress));
        RefreshTeachingSelectionCaptureCommands();
    }

    private void ClearTeachingSelectionCaptureState(string message)
    {
        teachingSelectionCaptureStepId = null;
        SetTeachingSelectionCaptureState(false, 0, 0, false, message);
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
        if (string.Equals(recipeSchemaVersion, ToolRecipeDocument.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            return;
        }

        recipeSchemaVersion = ToolRecipeDocument.CurrentSchemaVersion;
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

    private static void RemoveInputEntity(ToolWorkbenchPipelineStepItem step, string entityId) =>
        step.InputEntityIdsText = string.Join(
            "; ",
            step.InputEntityIds.Where(input => !string.Equals(input, entityId, StringComparison.OrdinalIgnoreCase)));

    private static ToolWorkbenchTeachingSelectionRequirement? CreateSelectionRequirement(
        ToolWorkbenchPipelineStepItem? step) => step?.ToolId switch
    {
        "roi-crop" => new("Grid rectangle", ToolRecipeSelectionKinds.GridRectangle, 2, true, "Pick two opposite grid-cell corners for the crop ROI."),
        "height-difference-edge" => new("Edge search band", ToolRecipeSelectionKinds.GridRectangle, 2, true, "Pick two opposite grid-cell corners for the explicit edge search band."),
        "thickness" => new("Thickness measurement ROI", ToolRecipeSelectionKinds.GridRectangle, 2, true, "Pick two opposite grid-cell corners for the measurement ROI."),
        "warpage" => new("Warpage measurement ROI", ToolRecipeSelectionKinds.GridRectangle, 2, true, "Pick two opposite grid-cell corners for the measurement ROI."),
        "two-point-line" => new("Line points", ToolRecipeSelectionKinds.PointSet, 2, true, "Pick exactly two distinct C3D grid cells."),
        "three-point-plane" => new("Plane points", ToolRecipeSelectionKinds.PointSet, 3, true, "Pick exactly three distinct, non-collinear C3D grid cells."),
        "landmark-correspondence" => new("Landmark correspondences", ToolRecipeSelectionKinds.LandmarkCorrespondenceSet, 0, false, "Enter explicit source entities and fixture coordinates."),
        _ => null
    };

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
            ToolRecipeSelectionKinds.PointSet => selection.Points?.Count == requirement.RequiredPointCount,
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet => selection.Rows is not null,
            _ => false
        };
    }

    private string CreateSelectionId(
        ToolWorkbenchPipelineStepItem step,
        ToolWorkbenchTeachingSelectionRequirement requirement)
    {
        var suffix = requirement.Kind switch
        {
            ToolRecipeSelectionKinds.GridRectangle => "roi",
            ToolRecipeSelectionKinds.PointSet => "points",
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet => "correspondences",
            _ => "selection"
        };
        return $"selection.{NormalizeId(step.Id.StartsWith("step.", StringComparison.OrdinalIgnoreCase) ? step.Id[5..] : step.Id)}.{suffix}";
    }

    private static string FormatTeachingSelection(ToolRecipeSelection selection)
    {
        var geometry = selection.GridRectangle is { } rectangle
            ? $"row {rectangle.Row}..{rectangle.Row + rectangle.RowCount - 1}, column {rectangle.Column}..{rectangle.Column + rectangle.ColumnCount - 1}"
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
        if (args.PropertyName == nameof(ToolWorkbenchPipelineStepItem.State))
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
        MarkHeightDifferenceEdgePreviewStaleIfNeeded(sender);
        MarkLineFitPreviewStaleIfNeeded(sender);
        MarkLineIntersectionPreviewStaleIfNeeded(sender);
        MarkLandmarkCorrespondencePreviewStaleIfNeeded(sender);
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
        if (isDirty == value)
        {
            return;
        }

        isDirty = value;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasUncommittedRecipeChanges));
        OnPropertyChanged(nameof(RecipeStateSummary));
    }

    private void RefreshStepCommands()
    {
        removeSelectedStepCommand.RaiseCanExecuteChanged();
        moveSelectedStepUpCommand.RaiseCanExecuteChanged();
        moveSelectedStepDownCommand.RaiseCanExecuteChanged();
        RefreshFilterCommands();
        RefreshLineFitCommands();
        RefreshLineIntersectionCommands();
        RefreshLandmarkCorrespondenceCommands();
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

    private void AppendLog(string category, string message) =>
        RunLog.Insert(0, new ToolWorkbenchLogItem(DateTime.Now.ToString("HH:mm:ss"), category, message));

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

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
    ToolRecipeSelectionSourceBinding sourceBinding) : EventArgs
{
    public string StepId { get; } = stepId;
    public string SelectionId { get; } = selectionId;
    public string SelectionName { get; } = selectionName;
    public string Kind { get; } = kind;
    public int RequiredPointCount { get; } = requiredPointCount;
    public string RootSourceId { get; } = rootSourceId;
    public string FrameId { get; } = frameId;
    public ToolRecipeSelectionSourceBinding SourceBinding { get; } = sourceBinding;
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
    private string inputEntityIdsText;
    private string outputEntityId;
    private string order = "00";
    private string state = "Taught / pending";

    public ToolWorkbenchPipelineStepItem(string id, ToolWorkbenchToolItem tool, string inputEntityIdsText, string outputEntityId, IReadOnlyList<ToolRecipeParameter>? parameters = null)
    {
        this.id = id;
        Tool = tool;
        this.inputEntityIdsText = inputEntityIdsText;
        this.outputEntityId = outputEntityId;
        Parameters = new ObservableCollection<ToolWorkbenchParameterItem>(
            parameters is null
                ? tool.Parameters.Select(parameter => new ToolWorkbenchParameterItem(parameter.Name, parameter.DefaultValue))
                : parameters.Select(parameter => new ToolWorkbenchParameterItem(parameter.Name, parameter.Value)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ToolWorkbenchToolItem Tool { get; }
    public string ToolId => Tool.Id;
    public string ToolName => Tool.Name;
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
    public string State { get => state; internal set => SetField(ref state, value); }

    private bool SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
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
