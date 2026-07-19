using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel : INotifyPropertyChanged
{
    public const string CubeEntityId = "source.generated-cube";
    public const string PointCloudEntityId = "source.generated-point-cloud";
    public const string C3DEntityId = "source.c3d-thickness";
    public const string GlbEntityId = "source.imported-mesh";
    public const string LazEntityId = "source.public-laz-manuscript";
    public const string SyntheticResultEntityId = "result.synthetic-height-deviation";
    public const string C3DHeightDeviationResultEntityId = "result.c3d-height-deviation";
    public const string C3DThicknessResultEntityId = "result.c3d-thickness";
    public const string C3DPlaneFlatnessResultEntityId = "result.c3d-plane-flatness";
    public const string C3DPointPairDimensionsResultEntityId = "result.c3d-point-pair-dimensions";
    public const string C3DGapFlushResultEntityId = "result.c3d-gap-flush";
    public const string C3DVolumeResultEntityId = "result.c3d-volume";
    public const string C3DCrossSectionResultEntityId = "result.c3d-cross-section-dimensions";
    public const string LazTwoPointResultEntityId = "result.laz-two-point-measurement";
    private const string PlaneFlatnessStepId = "step.c3d-plane-flatness";
    private const string PlaneFlatnessReferenceId = "reference.roi-plane";
    private const int PlaneFlatnessMaxSampledPoints = 140000;
    private const string ThicknessStepId = "step.c3d-thickness";
    private const string ThicknessRoiReferenceId = "reference.c3d-thickness-roi";
    private const string ThicknessDefaultFrameId = "frame.c3d-grid-index";
    public const string ThicknessRoiSelectionMode = "Thickness ROI Teach";
    public const string ProfileSelectionMode = "Profile";
    private const string PointPairDimensionsStepId = "step.c3d-point-pair-dimensions";
    private const string PointPairFirstReferenceId = "reference.point-a";
    private const string PointPairSecondReferenceId = "reference.point-b";
    private const string GapFlushStepId = "step.c3d-gap-flush";
    private const string GapFlushLeftReferenceId = "reference.roi-left";
    private const string GapFlushRightReferenceId = "reference.roi-right";
    private const int GapFlushMaxSampledPoints = 140000;
    private const string VolumeStepId = "step.c3d-volume";
    private const string VolumeReferenceId = "reference.roi-volume-plane";
    private const string VolumeMeasurementId = "measurement.roi-volume";
    private const int VolumeMaxSampledPoints = 140000;
    private const string CrossSectionStepId = "step.c3d-cross-section-dimensions";
    private const string CrossSectionReferenceId = "reference.c3d-row-range";
    private const double DefaultLazExpectedDistance = 116.919;
    private const double DefaultLazDistanceTolerance = 0.010;
    private const double DefaultLazExpectedHeightDelta = -0.624;
    private const double DefaultLazHeightDeltaTolerance = 0.010;

    private bool cubeVisible = true;
    private bool pointCloudVisible = true;
    private bool c3DSampleVisible;
    private bool glbSampleVisible;
    private bool syncingNominalMeshVisibility;
    private bool lazSampleVisible;
    private bool measurementVisible = true;
    private string selectedEntity = "Generated Unit Cube";
    private string pickCoordinate = "(none)";
    private string lastScreenshotPath = "(none)";
    private string viewerStatus = "Ready: generated cube and point cloud loaded";
    private string bottomStatus = "Model units: unitless | Camera: orbit | Source/result separation: source only";
    private string measurementSummary = "Cube width: 2.000 model units\nExpected center: (0.000, 0.000, 0.000)";
    private int displaySettingsRevision;
    private double pointSize = 2.0;
    private string selectedRenderDensity = "Balanced";
    private string renderDensitySummary = FormatRenderDensitySummary("Balanced");
    private string pointCloudPointCount = "(pending)";
    private string c3DSamplePointCount = "(not loaded)";
    private string c3DSampleSummary = "C3D sample hidden";
    private string glbSampleTriangleCount = "(not loaded)";
    private string glbSampleSummary = "Imported mesh hidden";
    private string glbSampleName = "Public GLB Box";
    private string glbSampleSourcePath = @"3D\PublicSamples\glTF\Box.glb";
    private string importedMeshFormat = "GLB";
    private bool importedMeshSourceColorAvailable;
    private bool c3dSurfaceGeometryAvailable = true;
    private Vector3 importedMeshFitCenter = Vector3.Zero;
    private double importedMeshFitDistance = 5.2;
    private string lazSamplePointCount = "(not loaded)";
    private string lazSampleSummary = "LAZ/LAS metadata hidden";
    private string lazSampleName = "Public LAZ/LAS Point Cloud";
    private string lazSampleSourcePath = @"3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz";
    private bool lazSourceColorAvailable;
    private Vector3 lazFitCenter = Vector3.Zero;
    private double lazFitDistance = 220.0;
    private string selectedSelectionMode = "Point";
    private string selectionSummary = "Point selection: generated point cloud peak";
    private bool selectionOverlayVisible = true;
    private bool resultOverlayVisible;
    private string resultSummary = "Result overlay hidden";
    private string recipeSummary = "Recipe: current C3D height deviation\nSource: C3D Thickness Sample\nTolerance: 1200.000 raw-height";
    private string recipeFileName = "(current)";
    private string recipeSourceName = "C3D Thickness Sample";
    private string recipeSourcePath = @"3D\Thickness\Ori_20240116_094414.C3D";
    private string recipeSourceUnit = "raw-height";
    private double recipePeakTolerance = 1200.0;
    private string recipeSaveSummary = "Recipe save: not saved";
    private string recipeParameterSummary = "Transform: identity | ROI: auto regions";
    private string recipeValidationSummary = string.Empty;
    private string recipeRoiMode = "Auto";
    private double recipeRoiLeftCenterX = -1.653;
    private double recipeRoiLeftCenterZ;
    private double recipeRoiLeftHalfWidth = 0.992;
    private double recipeRoiLeftHalfDepth = 2.500;
    private double recipeRoiRightCenterX = 1.653;
    private double recipeRoiRightCenterZ;
    private double recipeRoiRightHalfWidth = 0.992;
    private double recipeRoiRightHalfDepth = 2.500;
    private int recipeRoiMaxSampledPoints = 55000;
    private ToolResult previewToolResult = CreateNotRunToolResult();
    private ToolResult? c3dHeightDeviationPreview;
    private ToolResult? c3dThicknessPreview;
    private bool c3dThicknessPreviewActive;
    private bool thicknessConfigured;
    private string thicknessStepId = ThicknessStepId;
    private string thicknessSourceEntityId = C3DEntityId;
    private string thicknessRoiReferenceId = ThicknessRoiReferenceId;
    private int thicknessRoiRow = 900;
    private int thicknessRoiColumn = 570;
    private int thicknessRoiRowCount = 160;
    private int thicknessRoiColumnCount = 160;
    private double thicknessMinimum = -100.0;
    private double thicknessMaximum = 2500.0;
    private int thicknessMinimumValidSamples = 1;
    private string thicknessUnit = "raw-height";
    private string thicknessFrameId = ThicknessDefaultFrameId;
    private bool thicknessVisible;
    private string thicknessSummary = "Thickness: teach one C3D ROI, then run Preview.";
    private string thicknessDetails = "Declared scalar raw-height only; calibration is not inferred.";
    private double thicknessMean = double.NaN;
    private double thicknessMinimumMeasured = double.NaN;
    private double thicknessMaximumMeasured = double.NaN;
    private double thicknessRange = double.NaN;
    private int thicknessValidSampleCount;
    private int thicknessBelowLowerLimitCount;
    private int thicknessAboveUpperLimitCount;
    private ToolResult? c3dPlaneFlatnessPreview;
    private bool c3dPlaneFlatnessPreviewActive;
    private bool planeFlatnessConfigured;
    private string planeFlatnessStepId = PlaneFlatnessStepId;
    private string planeFlatnessSourceEntityId = C3DEntityId;
    private string planeFlatnessReferenceId = PlaneFlatnessReferenceId;
    private string planeFlatnessUnit = "model";
    private int planeFlatnessMaxSampledPoints = PlaneFlatnessMaxSampledPoints;
    private bool planeFlatnessEnabled = true;
    private bool planeFlatnessVisible;
    private string planeFlatnessSummary = "Flatness: preview not run";
    private string planeFlatnessDetails = "Reference ROI and signed surface deviation: pending";
    private double planeFlatnessReferenceCenterX;
    private double planeFlatnessReferenceCenterZ;
    private double planeFlatnessReferenceHalfWidth = 2.0;
    private double planeFlatnessReferenceHalfDepth = 2.0;
    private double planeFlatnessTolerance = 3.0;
    private double planeFlatnessValue = double.NaN;
    private double planeFlatnessMinimumDeviation = double.NaN;
    private double planeFlatnessMaximumDeviation = double.NaN;
    private double planeFlatnessRms = double.NaN;
    private int planeFlatnessReferenceSampleCount;
    private int planeFlatnessMeasurementSampleCount;
    private ToolResult? c3dPointPairDimensionsPreview;
    private bool c3dPointPairDimensionsPreviewActive;
    private bool pointPairDimensionsConfigured;
    private string pointPairDimensionsStepId = PointPairDimensionsStepId;
    private string pointPairDimensionsSourceEntityId = C3DEntityId;
    private string pointPairDimensionsUnit = "model";
    private bool pointPairDimensionsEnabled = true;
    private C3DGridPointReference? pointPairFirstReference;
    private C3DGridPointReference? pointPairSecondReference;
    private double pointPairExpectedDistance = 5.0;
    private double pointPairDistanceTolerance = 0.5;
    private double pointPairExpectedWidth = 5.0;
    private double pointPairWidthTolerance = 0.5;
    private double pointPairExpectedAngleDegrees;
    private double pointPairAngleToleranceDegrees = 5.0;
    private bool pointPairDimensionsVisible;
    private string pointPairDimensionsSummary = "Point pair dimensions: preview not run";
    private string pointPairDimensionsDetails = "Select two C3D points and run Preview Dimensions.";
    private double pointPairDistance = double.NaN;
    private double pointPairWidth = double.NaN;
    private double pointPairAngleDegrees = double.NaN;
    private ToolResult? c3dGapFlushPreview;
    private bool c3dGapFlushPreviewActive;
    private bool gapFlushConfigured;
    private string gapFlushStepId = GapFlushStepId;
    private string gapFlushSourceEntityId = C3DEntityId;
    private string gapFlushLeftReferenceId = GapFlushLeftReferenceId;
    private string gapFlushRightReferenceId = GapFlushRightReferenceId;
    private string gapFlushGapUnit = "model";
    private string gapFlushFlushUnit = "raw-height";
    private int gapFlushMaxSampledPoints = GapFlushMaxSampledPoints;
    private bool gapFlushEnabled = true;
    private double gapFlushExpectedGap = 1.322;
    private double gapFlushGapTolerance = 0.100;
    private double gapFlushExpectedFlush = 243.5;
    private double gapFlushFlushTolerance = 5.0;
    private bool gapFlushVisible;
    private string gapFlushSummary = "Gap / Flush: preview not run";
    private string gapFlushDetails = "Two recipe-owned C3D regions are required.";
    private double gapFlushGap = double.NaN;
    private double gapFlushFlush = double.NaN;
    private double gapFlushModelFlush = double.NaN;
    private int gapFlushLeftPointCount;
    private int gapFlushRightPointCount;
    private ToolResult? c3dVolumePreview;
    private bool c3dVolumePreviewActive;
    private bool volumeConfigured;
    private bool volumeVisible;
    private string volumeSummary = "Volume: preview not run";
    private string volumeDetails = "Reference ROI plane and measurement ROI are required.";
    private double volumeExpectedNet;
    private double volumeTolerance = 1.0;
    private double volumeAbove = double.NaN;
    private double volumeBelow = double.NaN;
    private double volumeNet = double.NaN;
    private int volumeReferenceSampleCount;
    private int volumeMeasurementSampleCount;
    private string volumeStepId = VolumeStepId;
    private string volumeReferenceId = VolumeReferenceId;
    private string volumeMeasurementId = VolumeMeasurementId;
    private string volumeUnit = "model^3";
    private int volumeMaxSampledPoints = VolumeMaxSampledPoints;
    private bool volumeEnabled = true;
    private ToolResult? c3dCrossSectionPreview;
    private bool c3dCrossSectionPreviewActive;
    private bool crossSectionConfigured;
    private bool crossSectionVisible;
    private string crossSectionSummary = "Cross-section: preview not run";
    private string crossSectionDetails = "An exact C3D row and inclusive column range are required.";
    private string crossSectionStepId = CrossSectionStepId;
    private string crossSectionReferenceId = CrossSectionReferenceId;
    private int crossSectionRow = 983;
    private int crossSectionStartColumn = 200;
    private int crossSectionEndColumn = 1100;
    private double crossSectionExpectedWidth = 4.247;
    private double crossSectionWidthTolerance = 0.010;
    private double crossSectionExpectedHeightRange = 1708.232;
    private double crossSectionHeightTolerance = 0.010;
    private double crossSectionWidth = double.NaN;
    private double crossSectionHeightRange = double.NaN;
    private double crossSectionRawMinimum = double.NaN;
    private double crossSectionRawMaximum = double.NaN;
    private int crossSectionValidSampleCount;
    private string crossSectionWidthUnit = "model";
    private string crossSectionHeightUnit = "raw-height";
    private bool crossSectionEnabled = true;
    private IReadOnlyList<SourceEntity> sourceEntities = [];
    private IReadOnlyList<ResultEntity> resultEntities = [];
    private NominalActualComparisonInput? nominalActualInput;
    private string publishedResultSummary = "Published result: none";
    private IReadOnlyList<EntityLayer> entityLayers = [];
    private string sceneContractSummary = "(pending)";
    private ModelTransform c3DModelTransform = ModelTransform.Identity;
    private string transformSummary = "Transform: Identity | T(0.000, 0.000, 0.000) | R(0.0, 0.0, 0.0) | S 1.000";
    private string alignmentSummary = "Alignment: Not aligned | source frame";
    private string coordinateMappingSummary = "Mapping: source = aligned | raw-height retained";
    private string alignmentWorkflowSummary = "ROI alignment: not applied";
    private bool hudDetailsVisible = true;
    private bool deviationLegendVisible;
    private string deviationLegendStatus = "Status: inactive";
    private string deviationLegendPeak = "Peak: none";
    private string deviationLegendTolerance = "Tolerance: none";
    private string deviationLegendScale = "Scale: mean to peak deviation";
    private string deviationLegendLowLabel = "Mean";
    private string deviationLegendMiddleLabel = "Tolerance";
    private string deviationLegendHighLabel = "Peak";
    private bool pointCloudColorLegendVisible;
    private string pointCloudColorLegendTitle = "Point Cloud Height Scale";
    private string pointCloudColorLegendLow = "Low: not loaded";
    private string pointCloudColorLegendHigh = "High: not loaded";
    private string pointCloudColorLegendScale = "Scale: source Z minimum to maximum";
    private double lazHeightMinimum = double.NaN;
    private double lazHeightMaximum = double.NaN;
    private string lazHeightUnit = "source-z";
    private bool heightMapVisible;
    private ImageSource? heightMapImageSource;
    private string heightMapSummary = "Height map: not loaded";
    private string heightMapRange = "Range: not loaded";
    private int heightMapPixelWidth;
    private int heightMapPixelHeight;
    private bool sectionProfileVisible;
    private string sectionProfileSummary = "Profile: not loaded";
    private string sectionProfileRange = "Range: not loaded";
    private string sectionProfilePathData = "M 0,30 L 240,30";
    private int sectionProfileSampleCount;
    private bool twoPointMeasurementVisible;
    private string twoPointMeasurementSummary = "Two-point: pick P1 and P2 on the C3D height grid.";
    private string twoPointMeasurementDetails = "Distance and height delta: pending";
    private string lazTwoPointAcceptanceSummary = "LAZ/LAS acceptance: pending";
    private double lazTwoPointExpectedDistance = DefaultLazExpectedDistance;
    private double lazTwoPointDistanceTolerance = DefaultLazDistanceTolerance;
    private double lazTwoPointExpectedHeightDelta = DefaultLazExpectedHeightDelta;
    private double lazTwoPointHeightDeltaTolerance = DefaultLazHeightDeltaTolerance;
    private Vector3? lazTwoPointPreviewFirst;
    private Vector3? lazTwoPointPreviewSecond;
    private string lazTwoPointPreviewHeightUnit = "source-z-units";
    private double twoPointDistance = double.NaN;
    private double twoPointDeltaX = double.NaN;
    private double twoPointDeltaY = double.NaN;
    private double twoPointDeltaZ = double.NaN;
    private double twoPointRawHeightDelta = double.NaN;
    private bool planeReferenceMeasurementVisible;
    private string planeReferenceMeasurementSummary = "Plane reference: pending";
    private string planeReferenceMeasurementDetails = "Distance to reference plane: pending";
    private double planeReferenceSignedDistance = double.NaN;
    private double planeReferenceAbsoluteDistance = double.NaN;
    private double planeReferenceY = double.NaN;
    private double planeReferenceTargetY = double.NaN;
    private double planeReferenceRawHeightDelta = double.NaN;
    private double planeReferenceNormalX = double.NaN;
    private double planeReferenceNormalY = double.NaN;
    private double planeReferenceNormalZ = double.NaN;
    private double planeReferenceFitRms = double.NaN;
    private int planeReferenceSampleCount;
    private bool roiStepMeasurementVisible;
    private string roiStepMeasurementSummary = "ROI step: compare two C3D regions.";
    private string roiStepMeasurementDetails = "Left/right ROI height delta: pending";
    private string roiStepEditSummary = "ROI edit: auto regions; click left ROI then right ROI.";
    private string roiStepSelectionMode = "Auto";
    private double roiStepLeftRawMean = double.NaN;
    private double roiStepRightRawMean = double.NaN;
    private double roiStepRawHeightDelta = double.NaN;
    private double roiStepModelHeightDelta = double.NaN;
    private int roiStepLeftPointCount;
    private int roiStepRightPointCount;
    private string performanceSummary = "Performance: waiting for first frame";
    private double viewportFps = double.NaN;
    private double viewportDrawMilliseconds = double.NaN;
    private string lazSamplingSummary = "LAZ/LAS sampling: not loaded";
    private double lazLoadMilliseconds = double.NaN;
    private double lazSamplePercent = double.NaN;
    private int lazSampleStride;
    private string activePreviewLayerId = "layer.preview.synthetic-height-deviation";
    private string activePreviewLayerName = "Preview: Synthetic Height Deviation";
    private string activePreviewSourceEntityId = PointCloudEntityId;
    private string activeResultEntityId = SyntheticResultEntityId;
    private string activeResultEntityName = "Published Synthetic Height Deviation";
    private double cameraTargetX = 2.05;
    private double cameraTargetY = -0.25;
    private double cameraTargetZ;
    private readonly RelayCommand applyRoiAlignmentCommand;
    private readonly RelayCommand fitPlaneCommand;
    private readonly RelayCommand teachThicknessRoiCommand;
    private readonly RelayCommand previewThicknessCommand;
    private readonly RelayCommand previewPlaneFlatnessCommand;
    private readonly RelayCommand previewPointPairDimensionsCommand;
    private readonly RelayCommand previewGapFlushCommand;
    private readonly RelayCommand previewVolumeCommand;
    private readonly RelayCommand previewCrossSectionCommand;
    private readonly RelayCommand publishResultCommand;
    private readonly RelayCommand fitAllCommand;
    private readonly RelayCommand fitSelectionCommand;
    private readonly RelayCommand openRecipeCommand;
    private readonly RelayCommand resetCommand;
    private readonly RelayCommand saveRecipeCommand;
    private readonly RelayCommand screenshotCommand;
    private readonly RelayCommand profileCommand;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ApplyRoiAlignmentRequested;
    public event EventHandler? FitPlaneRequested;
    public event EventHandler? PreviewThicknessRequested;
    public event EventHandler? PreviewPlaneFlatnessRequested;
    public event EventHandler? PreviewPointPairDimensionsRequested;
    public event EventHandler? PreviewGapFlushRequested;
    public event EventHandler? PreviewVolumeRequested;
    public event EventHandler? PreviewCrossSectionRequested;
    public event EventHandler? FitAllRequested;
    public event EventHandler? FitSelectionRequested;
    public event EventHandler? OpenRecipeRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler? SaveRecipeRequested;
    public event EventHandler? ScreenshotRequested;
    public event EventHandler? ProfileViewRequested;
    public event EventHandler? PublishPreviewResultRequested;

    public MainWindowViewModel()
    {
        Display.PropertyChanged += OnDisplayPropertyChanged;
        Display.RenderSettingsChanged += OnDisplayRenderSettingsChanged;
        NominalActual.PropertyChanged += OnNominalActualPropertyChanged;
        NominalActual.ConfigureNextDisplaySampling(SelectedRenderDensity, NominalActualMaxDisplaySamples);
        RefreshSourceEntities();
        fitAllCommand = new RelayCommand(_ => FitAllRequested?.Invoke(this, EventArgs.Empty));
        fitSelectionCommand = new RelayCommand(_ => FitSelectionRequested?.Invoke(this, EventArgs.Empty));
        resetCommand = new RelayCommand(_ => ResetRequested?.Invoke(this, EventArgs.Empty));
        openRecipeCommand = new RelayCommand(_ => OpenRecipeRequested?.Invoke(this, EventArgs.Empty));
        applyRoiAlignmentCommand = new RelayCommand(_ => ApplyRoiAlignmentRequested?.Invoke(this, EventArgs.Empty), _ => C3DSampleVisible);
        fitPlaneCommand = new RelayCommand(_ => FitPlaneRequested?.Invoke(this, EventArgs.Empty), _ => C3DSampleVisible);
        teachThicknessRoiCommand = new RelayCommand(_ => BeginThicknessRoiTeaching(), _ => C3DSampleVisible);
        previewThicknessCommand = new RelayCommand(_ => PreviewThicknessRequested?.Invoke(this, EventArgs.Empty), _ => C3DSampleVisible && ThicknessConfigured);
        teachWarpageRoiCommand = new RelayCommand(_ => BeginWarpageRoiTeaching(), _ => C3DSampleVisible);
        previewWarpageCommand = new RelayCommand(_ => PreviewWarpageRequested?.Invoke(this, EventArgs.Empty), _ => C3DSampleVisible && WarpageConfigured);
        previewPlaneFlatnessCommand = new RelayCommand(_ => PreviewPlaneFlatnessRequested?.Invoke(this, EventArgs.Empty), _ => C3DSampleVisible);
        previewPointPairDimensionsCommand = new RelayCommand(
            _ => PreviewPointPairDimensionsRequested?.Invoke(this, EventArgs.Empty),
            _ => C3DSampleVisible && pointPairFirstReference is not null && pointPairSecondReference is not null);
        previewGapFlushCommand = new RelayCommand(_ => PreviewGapFlushRequested?.Invoke(this, EventArgs.Empty), _ => C3DSampleVisible);
        previewVolumeCommand = new RelayCommand(_ => PreviewVolumeRequested?.Invoke(this, EventArgs.Empty), _ => C3DSampleVisible);
        previewCrossSectionCommand = new RelayCommand(_ => PreviewCrossSectionRequested?.Invoke(this, EventArgs.Empty), _ => C3DSampleVisible);
        publishResultCommand = new RelayCommand(_ => PublishPreviewResultRequested?.Invoke(this, EventArgs.Empty), _ => PreviewToolResult.Status != ResultStatus.NotRun);
        saveRecipeCommand = new RelayCommand(_ => SaveRecipeRequested?.Invoke(this, EventArgs.Empty));
        screenshotCommand = new RelayCommand(_ => ScreenshotRequested?.Invoke(this, EventArgs.Empty));
        profileCommand = new RelayCommand(
            _ =>
            {
                SelectedSelectionMode = ProfileSelectionMode;
                ProfileViewRequested?.Invoke(this, EventArgs.Empty);
            },
            _ => C3DSampleVisible);

        RefreshRecipeParameterSummary();
        RefreshSceneContracts();
        RefreshCommandCanExecute();
    }

    public IReadOnlyList<string> ColorModes => Display.AvailableColorMaps;

    public string[] RenderDensityModes { get; } = ["Fast", "Balanced", "Detailed"];

    public string[] SelectionModes { get; } = ["Point", ProfileSelectionMode, "Two Point Measure", "Plane Distance", "Plane Flatness", ThicknessRoiSelectionMode, WarpageRoiSelectionMode, "ROI Step Compare", "Gap / Flush", "Volume", "Cross-section Dimensions", "Box ROI", "Section Plane"];

    public ViewerDisplaySettingsViewModel Display { get; } = new();

    public NominalActualComparisonViewModel NominalActual { get; } = new();

    public NominalActualComparisonInput? NominalActualInput
    {
        get => nominalActualInput;
        private set
        {
            if (SetField(ref nominalActualInput, value))
            {
                OnPropertyChanged(nameof(C3DRecipeEditorVisible));
            }
        }
    }

    public bool C3DRecipeEditorVisible => NominalActualInput is null;

    public string CoordinateFrameSummary { get; } = "Right-handed | Y-up height | X red | Y green | Z blue";
    public ICommand ApplyRoiAlignmentCommand => applyRoiAlignmentCommand;
    public ICommand FitPlaneCommand => fitPlaneCommand;
    public ICommand TeachThicknessRoiCommand => teachThicknessRoiCommand;
    public ICommand PreviewThicknessCommand => previewThicknessCommand;
    public ICommand PreviewPlaneFlatnessCommand => previewPlaneFlatnessCommand;
    public ICommand PreviewPointPairDimensionsCommand => previewPointPairDimensionsCommand;
    public ICommand PreviewGapFlushCommand => previewGapFlushCommand;
    public ICommand PreviewVolumeCommand => previewVolumeCommand;
    public ICommand PreviewCrossSectionCommand => previewCrossSectionCommand;
    public ICommand FitAllCommand => fitAllCommand;
    public ICommand FitSelectionCommand => fitSelectionCommand;
    public ICommand OpenRecipeCommand => openRecipeCommand;
    public ICommand PublishResultCommand => publishResultCommand;
    public ICommand ResetCommand => resetCommand;
    public ICommand SaveRecipeCommand => saveRecipeCommand;
    public ICommand ScreenshotCommand => screenshotCommand;
    public ICommand ProfileCommand => profileCommand;

    public bool HudDetailsVisible
    {
        get => hudDetailsVisible;
        set
        {
            if (SetField(ref hudDetailsVisible, value))
            {
                OnPropertyChanged(nameof(ImportedMeshHudDetailsVisible));
                OnPropertyChanged(nameof(LazHudDetailsVisible));
            }
        }
    }

    public bool ImportedMeshHudDetailsVisible => HudDetailsVisible && GlbSampleVisible;

    public bool LazHudDetailsVisible => HudDetailsVisible && LazSampleVisible;

    public IReadOnlyList<SourceEntity> SourceEntities
    {
        get => sourceEntities;
        private set => SetField(ref sourceEntities, value);
    }

    public IReadOnlyList<EntityLayer> EntityLayers
    {
        get => entityLayers;
        private set => SetField(ref entityLayers, value);
    }

    public string SceneContractSummary
    {
        get => sceneContractSummary;
        private set => SetField(ref sceneContractSummary, value);
    }

    public ModelTransform C3DModelTransform
    {
        get => c3DModelTransform;
        private set => SetField(ref c3DModelTransform, value);
    }

    public string TransformSummary
    {
        get => transformSummary;
        private set => SetField(ref transformSummary, value);
    }

    public string AlignmentSummary
    {
        get => alignmentSummary;
        private set => SetField(ref alignmentSummary, value);
    }

    public string CoordinateMappingSummary
    {
        get => coordinateMappingSummary;
        private set => SetField(ref coordinateMappingSummary, value);
    }

    public string AlignmentWorkflowSummary
    {
        get => alignmentWorkflowSummary;
        private set => SetField(ref alignmentWorkflowSummary, value);
    }

    public bool CubeVisible
    {
        get => cubeVisible;
        set
        {
            if (SetField(ref cubeVisible, value))
            {
                ViewerStatus = value ? "Cube layer visible" : "Cube layer hidden";
                RefreshSceneContracts();
            }
        }
    }

    public bool PointCloudVisible
    {
        get => pointCloudVisible;
        set
        {
            if (SetField(ref pointCloudVisible, value))
            {
                ViewerStatus = value ? "Point cloud layer visible" : "Point cloud layer hidden";
                RefreshSceneContracts();
            }
        }
    }

    public bool C3DSampleVisible
    {
        get => c3DSampleVisible;
        set
        {
            if (SetField(ref c3DSampleVisible, value))
            {
                ViewerStatus = value ? "C3D sample visible" : "C3D sample hidden";
                if (ResultOverlayVisible)
                {
                    ApplyActivePreviewResult();
                }
                else
                {
                    RefreshSceneContracts();
                }

                RefreshC3DHeightDistributionLegend();
                RefreshCommandCanExecute();
            }
        }
    }

    public bool GlbSampleVisible
    {
        get => glbSampleVisible;
        set
        {
            if (SetField(ref glbSampleVisible, value))
            {
                if (NominalActual.InputsReady
                    && !syncingNominalMeshVisibility
                    && NominalActual.NominalVisible != value)
                {
                    syncingNominalMeshVisibility = true;
                    try
                    {
                        NominalActual.NominalVisible = value;
                    }
                    finally
                    {
                        syncingNominalMeshVisibility = false;
                    }
                }

                ViewerStatus = value ? $"{ImportedMeshFormat} mesh visible" : $"{ImportedMeshFormat} mesh hidden";
                OnPropertyChanged(nameof(ImportedMeshHudDetailsVisible));
                RefreshSceneContracts();
            }
        }
    }

    private void OnNominalActualPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(NominalActualComparisonViewModel.InputsReady)
            or nameof(NominalActualComparisonViewModel.Unit))
        {
            UpdateCameraStatus();
        }

        if (args.PropertyName is nameof(NominalActualComparisonViewModel.ActualVisible)
            or nameof(NominalActualComparisonViewModel.NominalVisible)
            or nameof(NominalActualComparisonViewModel.PreviewResult)
            or nameof(NominalActualComparisonViewModel.State))
        {
            RefreshSceneContracts();
        }

        if (args.PropertyName is not nameof(NominalActualComparisonViewModel.InputsReady)
            and not nameof(NominalActualComparisonViewModel.NominalVisible)
            || !NominalActual.InputsReady
            || syncingNominalMeshVisibility
            || GlbSampleVisible == NominalActual.NominalVisible)
        {
            return;
        }

        syncingNominalMeshVisibility = true;
        try
        {
            GlbSampleVisible = NominalActual.NominalVisible;
        }
        finally
        {
            syncingNominalMeshVisibility = false;
        }
    }

    private void OnDisplayPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewerDisplaySettingsViewModel.AvailableColorMaps))
        {
            OnPropertyChanged(nameof(ColorModes));
        }

        if ((args.PropertyName is nameof(ViewerDisplaySettingsViewModel.FallbackApplied)
             or nameof(ViewerDisplaySettingsViewModel.FallbackSummary))
            && Display.FallbackApplied)
        {
            ViewerStatus = Display.FallbackSummary;
        }
    }

    private void OnDisplayRenderSettingsChanged(object? sender, EventArgs args)
    {
        var settings = Display.EffectiveSettings;
        DisplaySettingsRevision = unchecked(DisplaySettingsRevision + 1);
        OnPropertyChanged(nameof(SelectedColorMode));
        OnPropertyChanged(nameof(SelectedGeometryStyle));
        RefreshPointCloudColorLegend();
        RefreshC3DHeightDistributionLegend();
        ViewerStatus = Display.FallbackApplied
            ? Display.FallbackSummary
            : $"Display: {Display.EffectiveGeometryStyle} | {ViewerDisplaySettingsViewModel.GetColorMapLabel(settings.ColorMap)}";
    }

    public bool LazSampleVisible
    {
        get => lazSampleVisible;
        set
        {
            if (SetField(ref lazSampleVisible, value))
            {
                ViewerStatus = value ? "LAZ/LAS point cloud visible" : "LAZ/LAS point cloud hidden";
                OnPropertyChanged(nameof(LazHudDetailsVisible));
                RefreshPointCloudColorLegend();
                RefreshSceneContracts();
            }
        }
    }

    public bool MeasurementVisible
    {
        get => measurementVisible;
        set
        {
            if (SetField(ref measurementVisible, value))
            {
                MeasurementSummary = value
                    ? "Cube width: 2.000 model units\nExpected center: (0.000, 0.000, 0.000)"
                    : "Measurement overlay hidden";
                ViewerStatus = value ? "Measurement overlay visible" : "Measurement overlay hidden";
            }
        }
    }

    public string MeasurementSummary
    {
        get => measurementSummary;
        set => SetField(ref measurementSummary, value);
    }

    public string SelectedColorMode
    {
        get => ViewerDisplaySettingsViewModel.GetColorMapLabel(Display.EffectiveSettings.ColorMap);
        set => Display.SelectedColorMap = value;
    }

    public string SelectedGeometryStyle => Display.EffectiveGeometryStyle;

    public int DisplaySettingsRevision
    {
        get => displaySettingsRevision;
        private set => SetField(ref displaySettingsRevision, value);
    }

    public double PointSize
    {
        get => pointSize;
        set
        {
            var clamped = Math.Clamp(value, 1.0, 6.0);
            if (SetField(ref pointSize, clamped))
            {
                ViewerStatus = string.Create(CultureInfo.InvariantCulture, $"Point size: {clamped:F1}px");
            }
        }
    }

    public string SelectedRenderDensity
    {
        get => selectedRenderDensity;
        set
        {
            var mode = RenderDensityModes.Contains(value) ? value : "Balanced";
            if (SetField(ref selectedRenderDensity, mode))
            {
                RenderDensitySummary = FormatRenderDensitySummary(mode);
                OnPropertyChanged(nameof(C3DMaxRenderedPoints));
                OnPropertyChanged(nameof(LazMaxSampledPoints));
                OnPropertyChanged(nameof(ImportedMeshMaxRenderedTriangles));
                OnPropertyChanged(nameof(NominalActualMaxDisplaySamples));
                NominalActual.ConfigureNextDisplaySampling(mode, NominalActualMaxDisplaySamples);
                ViewerStatus = $"Render density: {mode}";
            }
        }
    }

    public string RenderDensitySummary
    {
        get => renderDensitySummary;
        private set => SetField(ref renderDensitySummary, value);
    }

    public int C3DMaxRenderedPoints => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 140000,
        _ => 55000
    };

    public int LazMaxSampledPoints => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 150000,
        _ => 50000
    };

    public int ImportedMeshMaxRenderedTriangles => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 180000,
        _ => 60000
    };

    public int NominalActualMaxDisplaySamples => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 150000,
        _ => 60000
    };

    public string PointCloudPointCount
    {
        get => pointCloudPointCount;
        set => SetField(ref pointCloudPointCount, value);
    }

    public string C3DSamplePointCount
    {
        get => c3DSamplePointCount;
        set => SetField(ref c3DSamplePointCount, value);
    }

    public string C3DSampleSummary
    {
        get => c3DSampleSummary;
        set => SetField(ref c3DSampleSummary, value);
    }

    public string GlbSampleTriangleCount
    {
        get => glbSampleTriangleCount;
        set => SetField(ref glbSampleTriangleCount, value);
    }

    public string GlbSampleSummary
    {
        get => glbSampleSummary;
        set => SetField(ref glbSampleSummary, value);
    }

    public string GlbSampleName
    {
        get => glbSampleName;
        private set => SetField(ref glbSampleName, value);
    }

    public string GlbSampleSourcePath
    {
        get => glbSampleSourcePath;
        private set => SetField(ref glbSampleSourcePath, value);
    }

    public string ImportedMeshFormat
    {
        get => importedMeshFormat;
        private set => SetField(ref importedMeshFormat, value);
    }

    public string ImportedMeshLayerLabel => $"{ImportedMeshFormat} Mesh: {GlbSampleName}";

    public string LazSamplePointCount
    {
        get => lazSamplePointCount;
        set => SetField(ref lazSamplePointCount, value);
    }

    public string LazSampleSummary
    {
        get => lazSampleSummary;
        set => SetField(ref lazSampleSummary, value);
    }

    public string LazSampleName
    {
        get => lazSampleName;
        private set => SetField(ref lazSampleName, value);
    }

    public string LazSampleSourcePath
    {
        get => lazSampleSourcePath;
        private set => SetField(ref lazSampleSourcePath, value);
    }

    public string SelectedSelectionMode
    {
        get => selectedSelectionMode;
        set
        {
            if (SetField(ref selectedSelectionMode, value))
            {
                SelectionSummary = value switch
                {
                    "Box ROI" => "Box ROI: viewer state only",
                    "Two Point Measure" => TwoPointMeasurementSummary,
                    "Plane Distance" => PlaneReferenceMeasurementDetails,
                    "Plane Flatness" => PlaneFlatnessDetails,
                    ThicknessRoiSelectionMode => ThicknessDetails,
                    WarpageRoiSelectionMode => WarpageDetails,
                    "ROI Step Compare" => RoiStepMeasurementDetails,
                    "Gap / Flush" => GapFlushDetails,
                    "Volume" => VolumeDetails,
                    "Cross-section Dimensions" => CrossSectionDetails,
                    "Section Plane" => SectionProfileVisible ? SectionProfileSummary : "Section plane: profile not loaded",
                    _ => "Point selection: generated point cloud peak"
                };
                ViewerStatus = $"Selection mode: {value}";
            }
        }
    }

    public string SelectionSummary
    {
        get => selectionSummary;
        set => SetField(ref selectionSummary, value);
    }

    public bool SelectionOverlayVisible
    {
        get => selectionOverlayVisible;
        set
        {
            if (SetField(ref selectionOverlayVisible, value))
            {
                ViewerStatus = value ? "Selection overlay visible" : "Selection overlay hidden";
            }
        }
    }

    public bool ResultOverlayVisible
    {
        get => resultOverlayVisible;
        set
        {
            if (SetField(ref resultOverlayVisible, value))
            {
                if (value)
                {
                    ApplyActivePreviewResult();
                }
                else
                {
                    ResetActivePreviewIdentity();
                    PreviewToolResult = CreateNotRunToolResult();
                    ResultSummary = FormatToolResult(PreviewToolResult);
                    RefreshSceneContracts();
                }

                ViewerStatus = value ? "Result overlay visible" : "Result overlay hidden";
            }
        }
    }

    public string ResultSummary
    {
        get => resultSummary;
        set => SetField(ref resultSummary, value);
    }

    public string RecipeSummary
    {
        get => recipeSummary;
        set => SetField(ref recipeSummary, value);
    }

    public string RecipeSourceName
    {
        get => recipeSourceName;
        set
        {
            if (SetField(ref recipeSourceName, string.IsNullOrWhiteSpace(value) ? "C3D Thickness Sample" : value))
            {
                RefreshRecipeSummary();
            }
        }
    }

    public string RecipeSourcePath
    {
        get => recipeSourcePath;
        set
        {
            if (SetField(ref recipeSourcePath, string.IsNullOrWhiteSpace(value) ? @"3D\Thickness\Ori_20240116_094414.C3D" : value))
            {
                RefreshRecipeSummary();
            }
        }
    }

    public string RecipeSourceUnit
    {
        get => recipeSourceUnit;
        set
        {
            if (SetField(ref recipeSourceUnit, string.IsNullOrWhiteSpace(value) ? "raw-height" : value))
            {
                RefreshRecipeSummary();
            }
        }
    }

    public double RecipePeakTolerance
    {
        get => recipePeakTolerance;
        set
        {
            var clamped = double.IsFinite(value) ? Math.Max(0.001, value) : 1200.0;
            if (SetField(ref recipePeakTolerance, clamped))
            {
                RefreshRecipeSummary();
            }
        }
    }

    public string RecipeSaveSummary
    {
        get => recipeSaveSummary;
        private set => SetField(ref recipeSaveSummary, value);
    }

    public string RecipeParameterSummary
    {
        get => recipeParameterSummary;
        private set => SetField(ref recipeParameterSummary, value);
    }

    public string RecipeValidationSummary
    {
        get => recipeValidationSummary;
        private set => SetField(ref recipeValidationSummary, value);
    }

    public double RecipeTransformTranslateX
    {
        get => C3DModelTransform.TranslateX;
        set => SetRecipeTransform(C3DModelTransform with { TranslateX = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformTranslateY
    {
        get => C3DModelTransform.TranslateY;
        set => SetRecipeTransform(C3DModelTransform with { TranslateY = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformTranslateZ
    {
        get => C3DModelTransform.TranslateZ;
        set => SetRecipeTransform(C3DModelTransform with { TranslateZ = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformRotateXDegrees
    {
        get => C3DModelTransform.RotateXDegrees;
        set => SetRecipeTransform(C3DModelTransform with { RotateXDegrees = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformRotateYDegrees
    {
        get => C3DModelTransform.RotateYDegrees;
        set => SetRecipeTransform(C3DModelTransform with { RotateYDegrees = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformRotateZDegrees
    {
        get => C3DModelTransform.RotateZDegrees;
        set => SetRecipeTransform(C3DModelTransform with { RotateZDegrees = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformScale
    {
        get => C3DModelTransform.Scale;
        set => SetRecipeTransform(C3DModelTransform with { Scale = Math.Max(0.001, CoerceFinite(value, 1.0)) });
    }

    public string RecipeRoiMode
    {
        get => recipeRoiMode;
        private set => SetField(ref recipeRoiMode, string.IsNullOrWhiteSpace(value) ? "Auto" : value);
    }

    public double RecipeRoiLeftCenterX
    {
        get => recipeRoiLeftCenterX;
        set
        {
            if (SetField(ref recipeRoiLeftCenterX, CoerceFinite(value, recipeRoiLeftCenterX)))
            {
                OnRecipeRoiChanged();
            }
        }
    }

    public double RecipeRoiLeftCenterZ
    {
        get => recipeRoiLeftCenterZ;
        set
        {
            if (SetField(ref recipeRoiLeftCenterZ, CoerceFinite(value, recipeRoiLeftCenterZ)))
            {
                OnRecipeRoiChanged();
            }
        }
    }

    public double RecipeRoiLeftHalfWidth
    {
        get => recipeRoiLeftHalfWidth;
        set
        {
            if (SetField(ref recipeRoiLeftHalfWidth, Math.Max(0.0001, CoerceFinite(value, recipeRoiLeftHalfWidth))))
            {
                OnRecipeRoiChanged();
            }
        }
    }

    public double RecipeRoiLeftHalfDepth
    {
        get => recipeRoiLeftHalfDepth;
        set
        {
            if (SetField(ref recipeRoiLeftHalfDepth, Math.Max(0.0001, CoerceFinite(value, recipeRoiLeftHalfDepth))))
            {
                OnRecipeRoiChanged();
            }
        }
    }

    public double RecipeRoiRightCenterX
    {
        get => recipeRoiRightCenterX;
        set
        {
            if (SetField(ref recipeRoiRightCenterX, CoerceFinite(value, recipeRoiRightCenterX)))
            {
                OnRecipeRoiChanged();
            }
        }
    }

    public double RecipeRoiRightCenterZ
    {
        get => recipeRoiRightCenterZ;
        set
        {
            if (SetField(ref recipeRoiRightCenterZ, CoerceFinite(value, recipeRoiRightCenterZ)))
            {
                OnRecipeRoiChanged();
            }
        }
    }

    public double RecipeRoiRightHalfWidth
    {
        get => recipeRoiRightHalfWidth;
        set
        {
            if (SetField(ref recipeRoiRightHalfWidth, Math.Max(0.0001, CoerceFinite(value, recipeRoiRightHalfWidth))))
            {
                OnRecipeRoiChanged();
            }
        }
    }

    public double RecipeRoiRightHalfDepth
    {
        get => recipeRoiRightHalfDepth;
        set
        {
            if (SetField(ref recipeRoiRightHalfDepth, Math.Max(0.0001, CoerceFinite(value, recipeRoiRightHalfDepth))))
            {
                OnRecipeRoiChanged();
            }
        }
    }

    public int RecipeRoiMaxSampledPoints
    {
        get => recipeRoiMaxSampledPoints;
        private set => SetField(ref recipeRoiMaxSampledPoints, Math.Max(1, value));
    }

    public bool PlaneFlatnessConfigured
    {
        get => planeFlatnessConfigured;
        private set => SetField(ref planeFlatnessConfigured, value);
    }

    public double PlaneFlatnessReferenceCenterX
    {
        get => planeFlatnessReferenceCenterX;
        set => SetPlaneFlatnessParameter(ref planeFlatnessReferenceCenterX, CoerceFinite(value, planeFlatnessReferenceCenterX), nameof(PlaneFlatnessReferenceCenterX));
    }

    public double PlaneFlatnessReferenceCenterZ
    {
        get => planeFlatnessReferenceCenterZ;
        set => SetPlaneFlatnessParameter(ref planeFlatnessReferenceCenterZ, CoerceFinite(value, planeFlatnessReferenceCenterZ), nameof(PlaneFlatnessReferenceCenterZ));
    }

    public double PlaneFlatnessReferenceHalfWidth
    {
        get => planeFlatnessReferenceHalfWidth;
        set => SetPlaneFlatnessParameter(ref planeFlatnessReferenceHalfWidth, Math.Max(0.0001, CoerceFinite(value, planeFlatnessReferenceHalfWidth)), nameof(PlaneFlatnessReferenceHalfWidth));
    }

    public double PlaneFlatnessReferenceHalfDepth
    {
        get => planeFlatnessReferenceHalfDepth;
        set => SetPlaneFlatnessParameter(ref planeFlatnessReferenceHalfDepth, Math.Max(0.0001, CoerceFinite(value, planeFlatnessReferenceHalfDepth)), nameof(PlaneFlatnessReferenceHalfDepth));
    }

    public double PlaneFlatnessTolerance
    {
        get => planeFlatnessTolerance;
        set => SetPlaneFlatnessParameter(ref planeFlatnessTolerance, Math.Max(0.0001, CoerceFinite(value, planeFlatnessTolerance)), nameof(PlaneFlatnessTolerance));
    }

    public string PlaneFlatnessUnit => planeFlatnessUnit;

    public bool PointPairDimensionsConfigured
    {
        get => pointPairDimensionsConfigured;
        private set => SetField(ref pointPairDimensionsConfigured, value);
    }

    public bool HasPointPairReferences => pointPairFirstReference is not null && pointPairSecondReference is not null;

    public double PointPairExpectedDistance
    {
        get => pointPairExpectedDistance;
        set => SetPointPairParameter(ref pointPairExpectedDistance, Math.Max(0.0, CoerceFinite(value, pointPairExpectedDistance)), nameof(PointPairExpectedDistance));
    }

    public double PointPairDistanceTolerance
    {
        get => pointPairDistanceTolerance;
        set => SetPointPairParameter(ref pointPairDistanceTolerance, Math.Max(0.0, CoerceFinite(value, pointPairDistanceTolerance)), nameof(PointPairDistanceTolerance));
    }

    public double PointPairExpectedWidth
    {
        get => pointPairExpectedWidth;
        set => SetPointPairParameter(ref pointPairExpectedWidth, Math.Max(0.0, CoerceFinite(value, pointPairExpectedWidth)), nameof(PointPairExpectedWidth));
    }

    public double PointPairWidthTolerance
    {
        get => pointPairWidthTolerance;
        set => SetPointPairParameter(ref pointPairWidthTolerance, Math.Max(0.0, CoerceFinite(value, pointPairWidthTolerance)), nameof(PointPairWidthTolerance));
    }

    public double PointPairExpectedAngleDegrees
    {
        get => pointPairExpectedAngleDegrees;
        set => SetPointPairParameter(
            ref pointPairExpectedAngleDegrees,
            Math.Clamp(CoerceFinite(value, pointPairExpectedAngleDegrees), -90.0, 90.0),
            nameof(PointPairExpectedAngleDegrees));
    }

    public double PointPairAngleToleranceDegrees
    {
        get => pointPairAngleToleranceDegrees;
        set => SetPointPairParameter(ref pointPairAngleToleranceDegrees, Math.Max(0.0, CoerceFinite(value, pointPairAngleToleranceDegrees)), nameof(PointPairAngleToleranceDegrees));
    }

    public string PointPairDimensionsUnit => pointPairDimensionsUnit;

    public bool PointPairDimensionsVisible
    {
        get => pointPairDimensionsVisible;
        private set => SetField(ref pointPairDimensionsVisible, value);
    }

    public string PointPairDimensionsSummary
    {
        get => pointPairDimensionsSummary;
        private set => SetField(ref pointPairDimensionsSummary, value);
    }

    public string PointPairDimensionsDetails
    {
        get => pointPairDimensionsDetails;
        private set => SetField(ref pointPairDimensionsDetails, value);
    }

    public double PointPairDistance
    {
        get => pointPairDistance;
        private set => SetField(ref pointPairDistance, value);
    }

    public double PointPairWidth
    {
        get => pointPairWidth;
        private set => SetField(ref pointPairWidth, value);
    }

    public double PointPairAngleDegrees
    {
        get => pointPairAngleDegrees;
        private set => SetField(ref pointPairAngleDegrees, value);
    }

    public bool ThicknessConfigured
    {
        get => thicknessConfigured;
        private set => SetField(ref thicknessConfigured, value);
    }

    public int ThicknessRoiRow
    {
        get => thicknessRoiRow;
        set => SetThicknessParameter(ref thicknessRoiRow, Math.Max(0, value), nameof(ThicknessRoiRow), "Thickness ROI changed; run Preview Thickness again");
    }

    public int ThicknessRoiColumn
    {
        get => thicknessRoiColumn;
        set => SetThicknessParameter(ref thicknessRoiColumn, Math.Max(0, value), nameof(ThicknessRoiColumn), "Thickness ROI changed; run Preview Thickness again");
    }

    public int ThicknessRoiRowCount
    {
        get => thicknessRoiRowCount;
        set => SetThicknessParameter(ref thicknessRoiRowCount, Math.Max(1, value), nameof(ThicknessRoiRowCount), "Thickness ROI size changed; run Preview Thickness again");
    }

    public int ThicknessRoiColumnCount
    {
        get => thicknessRoiColumnCount;
        set => SetThicknessParameter(ref thicknessRoiColumnCount, Math.Max(1, value), nameof(ThicknessRoiColumnCount), "Thickness ROI size changed; run Preview Thickness again");
    }

    public double ThicknessMinimum
    {
        get => thicknessMinimum;
        set => SetThicknessParameter(ref thicknessMinimum, CoerceFinite(value, thicknessMinimum), nameof(ThicknessMinimum), "Thickness lower limit changed; run Preview Thickness again");
    }

    public double ThicknessMaximum
    {
        get => thicknessMaximum;
        set => SetThicknessParameter(ref thicknessMaximum, CoerceFinite(value, thicknessMaximum), nameof(ThicknessMaximum), "Thickness upper limit changed; run Preview Thickness again");
    }

    public int ThicknessMinimumValidSamples
    {
        get => thicknessMinimumValidSamples;
        set => SetThicknessParameter(ref thicknessMinimumValidSamples, Math.Max(1, value), nameof(ThicknessMinimumValidSamples), "Thickness minimum sample count changed; run Preview Thickness again");
    }

    public string ThicknessUnit => thicknessUnit;

    public string ThicknessFrameId => thicknessFrameId;

    public bool ThicknessVisible
    {
        get => thicknessVisible;
        private set => SetField(ref thicknessVisible, value);
    }

    public string ThicknessSummary
    {
        get => thicknessSummary;
        private set => SetField(ref thicknessSummary, value);
    }

    public string ThicknessDetails
    {
        get => thicknessDetails;
        private set => SetField(ref thicknessDetails, value);
    }

    public double ThicknessMean
    {
        get => thicknessMean;
        private set => SetField(ref thicknessMean, value);
    }

    public double ThicknessMinimumMeasured
    {
        get => thicknessMinimumMeasured;
        private set => SetField(ref thicknessMinimumMeasured, value);
    }

    public double ThicknessMaximumMeasured
    {
        get => thicknessMaximumMeasured;
        private set => SetField(ref thicknessMaximumMeasured, value);
    }

    public double ThicknessRange
    {
        get => thicknessRange;
        private set => SetField(ref thicknessRange, value);
    }

    public int ThicknessValidSampleCount
    {
        get => thicknessValidSampleCount;
        private set => SetField(ref thicknessValidSampleCount, value);
    }

    public int ThicknessBelowLowerLimitCount
    {
        get => thicknessBelowLowerLimitCount;
        private set => SetField(ref thicknessBelowLowerLimitCount, value);
    }

    public int ThicknessAboveUpperLimitCount
    {
        get => thicknessAboveUpperLimitCount;
        private set => SetField(ref thicknessAboveUpperLimitCount, value);
    }

    public bool GapFlushConfigured
    {
        get => gapFlushConfigured;
        private set => SetField(ref gapFlushConfigured, value);
    }

    public double GapFlushExpectedGap
    {
        get => gapFlushExpectedGap;
        set => SetGapFlushParameter(ref gapFlushExpectedGap, CoerceFinite(value, gapFlushExpectedGap), nameof(GapFlushExpectedGap));
    }

    public double GapFlushGapTolerance
    {
        get => gapFlushGapTolerance;
        set => SetGapFlushParameter(ref gapFlushGapTolerance, Math.Max(0.0, CoerceFinite(value, gapFlushGapTolerance)), nameof(GapFlushGapTolerance));
    }

    public double GapFlushExpectedFlush
    {
        get => gapFlushExpectedFlush;
        set => SetGapFlushParameter(ref gapFlushExpectedFlush, CoerceFinite(value, gapFlushExpectedFlush), nameof(GapFlushExpectedFlush));
    }

    public double GapFlushFlushTolerance
    {
        get => gapFlushFlushTolerance;
        set => SetGapFlushParameter(ref gapFlushFlushTolerance, Math.Max(0.0, CoerceFinite(value, gapFlushFlushTolerance)), nameof(GapFlushFlushTolerance));
    }

    public string GapFlushGapUnit => gapFlushGapUnit;
    public string GapFlushFlushUnit => gapFlushFlushUnit;

    public bool GapFlushVisible
    {
        get => gapFlushVisible;
        private set => SetField(ref gapFlushVisible, value);
    }

    public string GapFlushSummary
    {
        get => gapFlushSummary;
        private set => SetField(ref gapFlushSummary, value);
    }

    public string GapFlushDetails
    {
        get => gapFlushDetails;
        private set => SetField(ref gapFlushDetails, value);
    }

    public double GapFlushGap
    {
        get => gapFlushGap;
        private set => SetField(ref gapFlushGap, value);
    }

    public double GapFlushFlush
    {
        get => gapFlushFlush;
        private set => SetField(ref gapFlushFlush, value);
    }

    public double GapFlushModelFlush
    {
        get => gapFlushModelFlush;
        private set => SetField(ref gapFlushModelFlush, value);
    }

    public int GapFlushLeftPointCount
    {
        get => gapFlushLeftPointCount;
        private set => SetField(ref gapFlushLeftPointCount, value);
    }

    public int GapFlushRightPointCount
    {
        get => gapFlushRightPointCount;
        private set => SetField(ref gapFlushRightPointCount, value);
    }

    public bool VolumeConfigured
    {
        get => volumeConfigured;
        private set => SetField(ref volumeConfigured, value);
    }

    public double VolumeExpectedNet
    {
        get => volumeExpectedNet;
        set => SetVolumeParameter(ref volumeExpectedNet, CoerceFinite(value, volumeExpectedNet), nameof(VolumeExpectedNet));
    }

    public double VolumeTolerance
    {
        get => volumeTolerance;
        set => SetVolumeParameter(ref volumeTolerance, Math.Max(0.0, CoerceFinite(value, volumeTolerance)), nameof(VolumeTolerance));
    }

    public string VolumeUnit => volumeUnit;

    public bool VolumeVisible
    {
        get => volumeVisible;
        private set => SetField(ref volumeVisible, value);
    }

    public string VolumeSummary
    {
        get => volumeSummary;
        private set => SetField(ref volumeSummary, value);
    }

    public string VolumeDetails
    {
        get => volumeDetails;
        private set => SetField(ref volumeDetails, value);
    }

    public double VolumeAbove { get => volumeAbove; private set => SetField(ref volumeAbove, value); }
    public double VolumeBelow { get => volumeBelow; private set => SetField(ref volumeBelow, value); }
    public double VolumeNet { get => volumeNet; private set => SetField(ref volumeNet, value); }
    public int VolumeReferenceSampleCount { get => volumeReferenceSampleCount; private set => SetField(ref volumeReferenceSampleCount, value); }
    public int VolumeMeasurementSampleCount { get => volumeMeasurementSampleCount; private set => SetField(ref volumeMeasurementSampleCount, value); }

    public bool CrossSectionConfigured
    {
        get => crossSectionConfigured;
        private set => SetField(ref crossSectionConfigured, value);
    }

    public int CrossSectionRow
    {
        get => crossSectionRow;
        set => SetCrossSectionParameter(ref crossSectionRow, Math.Max(0, value), nameof(CrossSectionRow));
    }

    public int CrossSectionStartColumn
    {
        get => crossSectionStartColumn;
        set => SetCrossSectionParameter(ref crossSectionStartColumn, Math.Max(0, value), nameof(CrossSectionStartColumn));
    }

    public int CrossSectionEndColumn
    {
        get => crossSectionEndColumn;
        set => SetCrossSectionParameter(ref crossSectionEndColumn, Math.Max(0, value), nameof(CrossSectionEndColumn));
    }

    public double CrossSectionExpectedWidth
    {
        get => crossSectionExpectedWidth;
        set => SetCrossSectionParameter(ref crossSectionExpectedWidth, CoerceFinite(value, crossSectionExpectedWidth), nameof(CrossSectionExpectedWidth));
    }

    public double CrossSectionWidthTolerance
    {
        get => crossSectionWidthTolerance;
        set => SetCrossSectionParameter(ref crossSectionWidthTolerance, Math.Max(0.0, CoerceFinite(value, crossSectionWidthTolerance)), nameof(CrossSectionWidthTolerance));
    }

    public double CrossSectionExpectedHeightRange
    {
        get => crossSectionExpectedHeightRange;
        set => SetCrossSectionParameter(ref crossSectionExpectedHeightRange, CoerceFinite(value, crossSectionExpectedHeightRange), nameof(CrossSectionExpectedHeightRange));
    }

    public double CrossSectionHeightTolerance
    {
        get => crossSectionHeightTolerance;
        set => SetCrossSectionParameter(ref crossSectionHeightTolerance, Math.Max(0.0, CoerceFinite(value, crossSectionHeightTolerance)), nameof(CrossSectionHeightTolerance));
    }

    public bool CrossSectionVisible { get => crossSectionVisible; private set => SetField(ref crossSectionVisible, value); }
    public string CrossSectionSummary { get => crossSectionSummary; private set => SetField(ref crossSectionSummary, value); }
    public string CrossSectionDetails { get => crossSectionDetails; private set => SetField(ref crossSectionDetails, value); }
    public double CrossSectionWidth { get => crossSectionWidth; private set => SetField(ref crossSectionWidth, value); }
    public double CrossSectionHeightRange { get => crossSectionHeightRange; private set => SetField(ref crossSectionHeightRange, value); }
    public double CrossSectionRawMinimum { get => crossSectionRawMinimum; private set => SetField(ref crossSectionRawMinimum, value); }
    public double CrossSectionRawMaximum { get => crossSectionRawMaximum; private set => SetField(ref crossSectionRawMaximum, value); }
    public int CrossSectionValidSampleCount { get => crossSectionValidSampleCount; private set => SetField(ref crossSectionValidSampleCount, value); }
    public string CrossSectionWidthUnit => crossSectionWidthUnit;
    public string CrossSectionHeightUnit => crossSectionHeightUnit;

    public ToolResult PreviewToolResult
    {
        get => previewToolResult;
        private set
        {
            if (SetField(ref previewToolResult, value))
            {
                RefreshDeviationLegend(value);
                RefreshCommandCanExecute();
            }
        }
    }

    private void RefreshCommandCanExecute()
    {
        applyRoiAlignmentCommand.RaiseCanExecuteChanged();
        fitPlaneCommand.RaiseCanExecuteChanged();
        teachThicknessRoiCommand.RaiseCanExecuteChanged();
        previewThicknessCommand.RaiseCanExecuteChanged();
        teachWarpageRoiCommand.RaiseCanExecuteChanged();
        previewWarpageCommand.RaiseCanExecuteChanged();
        previewPlaneFlatnessCommand.RaiseCanExecuteChanged();
        previewPointPairDimensionsCommand.RaiseCanExecuteChanged();
        previewGapFlushCommand.RaiseCanExecuteChanged();
        previewVolumeCommand.RaiseCanExecuteChanged();
        previewCrossSectionCommand.RaiseCanExecuteChanged();
        profileCommand.RaiseCanExecuteChanged();
        publishResultCommand.RaiseCanExecuteChanged();
    }

    public IReadOnlyList<ResultEntity> ResultEntities
    {
        get => resultEntities;
        private set => SetField(ref resultEntities, value);
    }

    public string PublishedResultSummary
    {
        get => publishedResultSummary;
        private set => SetField(ref publishedResultSummary, value);
    }

    public string SelectedEntity
    {
        get => selectedEntity;
        set => SetField(ref selectedEntity, value);
    }

    public string PickCoordinate
    {
        get => pickCoordinate;
        set => SetField(ref pickCoordinate, value);
    }

    public string LastScreenshotPath
    {
        get => lastScreenshotPath;
        set => SetField(ref lastScreenshotPath, value);
    }

    public string ViewerStatus
    {
        get => viewerStatus;
        set => SetField(ref viewerStatus, value);
    }

    public string BottomStatus
    {
        get => bottomStatus;
        set => SetField(ref bottomStatus, value);
    }

    public double YawDegrees { get; set; } = 38.0;

    public double PitchDegrees { get; set; } = 24.0;

    public double CameraDistance { get; set; } = 9.2;

    public double CameraTargetX
    {
        get => cameraTargetX;
        set => SetField(ref cameraTargetX, value);
    }

    public double CameraTargetY
    {
        get => cameraTargetY;
        set => SetField(ref cameraTargetY, value);
    }

    public double CameraTargetZ
    {
        get => cameraTargetZ;
        set => SetField(ref cameraTargetZ, value);
    }

    public bool DeviationLegendVisible
    {
        get => deviationLegendVisible;
        private set => SetField(ref deviationLegendVisible, value);
    }

    public string DeviationLegendStatus
    {
        get => deviationLegendStatus;
        private set => SetField(ref deviationLegendStatus, value);
    }

    public string DeviationLegendPeak
    {
        get => deviationLegendPeak;
        private set => SetField(ref deviationLegendPeak, value);
    }

    public string DeviationLegendTolerance
    {
        get => deviationLegendTolerance;
        private set => SetField(ref deviationLegendTolerance, value);
    }

    public string DeviationLegendScale
    {
        get => deviationLegendScale;
        private set => SetField(ref deviationLegendScale, value);
    }

    public string DeviationLegendLowLabel
    {
        get => deviationLegendLowLabel;
        private set => SetField(ref deviationLegendLowLabel, value);
    }

    public string DeviationLegendMiddleLabel
    {
        get => deviationLegendMiddleLabel;
        private set => SetField(ref deviationLegendMiddleLabel, value);
    }

    public string DeviationLegendHighLabel
    {
        get => deviationLegendHighLabel;
        private set => SetField(ref deviationLegendHighLabel, value);
    }

    public bool PointCloudColorLegendVisible
    {
        get => pointCloudColorLegendVisible;
        private set => SetField(ref pointCloudColorLegendVisible, value);
    }

    public string PointCloudColorLegendTitle
    {
        get => pointCloudColorLegendTitle;
        private set => SetField(ref pointCloudColorLegendTitle, value);
    }

    public string PointCloudColorLegendLow
    {
        get => pointCloudColorLegendLow;
        private set => SetField(ref pointCloudColorLegendLow, value);
    }

    public string PointCloudColorLegendHigh
    {
        get => pointCloudColorLegendHigh;
        private set => SetField(ref pointCloudColorLegendHigh, value);
    }

    public string PointCloudColorLegendScale
    {
        get => pointCloudColorLegendScale;
        private set => SetField(ref pointCloudColorLegendScale, value);
    }

    public bool HeightMapVisible
    {
        get => heightMapVisible;
        private set => SetField(ref heightMapVisible, value);
    }

    public ImageSource? HeightMapImageSource
    {
        get => heightMapImageSource;
        private set => SetField(ref heightMapImageSource, value);
    }

    public string HeightMapSummary
    {
        get => heightMapSummary;
        private set => SetField(ref heightMapSummary, value);
    }

    public string HeightMapRange
    {
        get => heightMapRange;
        private set => SetField(ref heightMapRange, value);
    }

    public int HeightMapPixelWidth
    {
        get => heightMapPixelWidth;
        private set => SetField(ref heightMapPixelWidth, value);
    }

    public int HeightMapPixelHeight
    {
        get => heightMapPixelHeight;
        private set => SetField(ref heightMapPixelHeight, value);
    }

    public bool SectionProfileVisible
    {
        get => sectionProfileVisible;
        private set => SetField(ref sectionProfileVisible, value);
    }

    public string SectionProfileSummary
    {
        get => sectionProfileSummary;
        private set => SetField(ref sectionProfileSummary, value);
    }

    public string SectionProfileRange
    {
        get => sectionProfileRange;
        private set => SetField(ref sectionProfileRange, value);
    }

    public string SectionProfilePathData
    {
        get => sectionProfilePathData;
        private set => SetField(ref sectionProfilePathData, value);
    }

    public int SectionProfileSampleCount
    {
        get => sectionProfileSampleCount;
        private set => SetField(ref sectionProfileSampleCount, value);
    }

    public bool TwoPointMeasurementVisible
    {
        get => twoPointMeasurementVisible;
        private set => SetField(ref twoPointMeasurementVisible, value);
    }

    public string TwoPointMeasurementSummary
    {
        get => twoPointMeasurementSummary;
        private set => SetField(ref twoPointMeasurementSummary, value);
    }

    public string TwoPointMeasurementDetails
    {
        get => twoPointMeasurementDetails;
        private set => SetField(ref twoPointMeasurementDetails, value);
    }

    public string LazTwoPointAcceptanceSummary
    {
        get => lazTwoPointAcceptanceSummary;
        private set => SetField(ref lazTwoPointAcceptanceSummary, value);
    }

    public double LazTwoPointExpectedDistance
    {
        get => lazTwoPointExpectedDistance;
        set
        {
            if (SetField(ref lazTwoPointExpectedDistance, CoerceFinite(value, DefaultLazExpectedDistance)))
            {
                RefreshLazTwoPointAcceptanceState();
            }
        }
    }

    public double LazTwoPointDistanceTolerance
    {
        get => lazTwoPointDistanceTolerance;
        set
        {
            var tolerance = Math.Max(0.0, CoerceFinite(value, DefaultLazDistanceTolerance));
            if (SetField(ref lazTwoPointDistanceTolerance, tolerance))
            {
                RefreshLazTwoPointAcceptanceState();
            }
        }
    }

    public double LazTwoPointExpectedHeightDelta
    {
        get => lazTwoPointExpectedHeightDelta;
        set
        {
            if (SetField(ref lazTwoPointExpectedHeightDelta, CoerceFinite(value, DefaultLazExpectedHeightDelta)))
            {
                RefreshLazTwoPointAcceptanceState();
            }
        }
    }

    public double LazTwoPointHeightDeltaTolerance
    {
        get => lazTwoPointHeightDeltaTolerance;
        set
        {
            var tolerance = Math.Max(0.0, CoerceFinite(value, DefaultLazHeightDeltaTolerance));
            if (SetField(ref lazTwoPointHeightDeltaTolerance, tolerance))
            {
                RefreshLazTwoPointAcceptanceState();
            }
        }
    }

    public double TwoPointDistance
    {
        get => twoPointDistance;
        private set => SetField(ref twoPointDistance, value);
    }

    public double TwoPointDeltaX
    {
        get => twoPointDeltaX;
        private set => SetField(ref twoPointDeltaX, value);
    }

    public double TwoPointDeltaY
    {
        get => twoPointDeltaY;
        private set => SetField(ref twoPointDeltaY, value);
    }

    public double TwoPointDeltaZ
    {
        get => twoPointDeltaZ;
        private set => SetField(ref twoPointDeltaZ, value);
    }

    public double TwoPointRawHeightDelta
    {
        get => twoPointRawHeightDelta;
        private set => SetField(ref twoPointRawHeightDelta, value);
    }

    public bool PlaneReferenceMeasurementVisible
    {
        get => planeReferenceMeasurementVisible;
        private set => SetField(ref planeReferenceMeasurementVisible, value);
    }

    public string PlaneReferenceMeasurementSummary
    {
        get => planeReferenceMeasurementSummary;
        private set => SetField(ref planeReferenceMeasurementSummary, value);
    }

    public string PlaneReferenceMeasurementDetails
    {
        get => planeReferenceMeasurementDetails;
        private set => SetField(ref planeReferenceMeasurementDetails, value);
    }

    public double PlaneReferenceSignedDistance
    {
        get => planeReferenceSignedDistance;
        private set => SetField(ref planeReferenceSignedDistance, value);
    }

    public double PlaneReferenceAbsoluteDistance
    {
        get => planeReferenceAbsoluteDistance;
        private set => SetField(ref planeReferenceAbsoluteDistance, value);
    }

    public double PlaneReferenceY
    {
        get => planeReferenceY;
        private set => SetField(ref planeReferenceY, value);
    }

    public double PlaneReferenceTargetY
    {
        get => planeReferenceTargetY;
        private set => SetField(ref planeReferenceTargetY, value);
    }

    public double PlaneReferenceRawHeightDelta
    {
        get => planeReferenceRawHeightDelta;
        private set => SetField(ref planeReferenceRawHeightDelta, value);
    }

    public double PlaneReferenceNormalX
    {
        get => planeReferenceNormalX;
        private set => SetField(ref planeReferenceNormalX, value);
    }

    public double PlaneReferenceNormalY
    {
        get => planeReferenceNormalY;
        private set => SetField(ref planeReferenceNormalY, value);
    }

    public double PlaneReferenceNormalZ
    {
        get => planeReferenceNormalZ;
        private set => SetField(ref planeReferenceNormalZ, value);
    }

    public double PlaneReferenceFitRms
    {
        get => planeReferenceFitRms;
        private set => SetField(ref planeReferenceFitRms, value);
    }

    public int PlaneReferenceSampleCount
    {
        get => planeReferenceSampleCount;
        private set => SetField(ref planeReferenceSampleCount, value);
    }

    public bool PlaneFlatnessVisible
    {
        get => planeFlatnessVisible;
        private set => SetField(ref planeFlatnessVisible, value);
    }

    public string PlaneFlatnessSummary
    {
        get => planeFlatnessSummary;
        private set => SetField(ref planeFlatnessSummary, value);
    }

    public string PlaneFlatnessDetails
    {
        get => planeFlatnessDetails;
        private set => SetField(ref planeFlatnessDetails, value);
    }

    public double PlaneFlatnessValue
    {
        get => planeFlatnessValue;
        private set => SetField(ref planeFlatnessValue, value);
    }

    public double PlaneFlatnessMinimumDeviation
    {
        get => planeFlatnessMinimumDeviation;
        private set => SetField(ref planeFlatnessMinimumDeviation, value);
    }

    public double PlaneFlatnessMaximumDeviation
    {
        get => planeFlatnessMaximumDeviation;
        private set => SetField(ref planeFlatnessMaximumDeviation, value);
    }

    public double PlaneFlatnessRms
    {
        get => planeFlatnessRms;
        private set => SetField(ref planeFlatnessRms, value);
    }

    public int PlaneFlatnessReferenceSampleCount
    {
        get => planeFlatnessReferenceSampleCount;
        private set => SetField(ref planeFlatnessReferenceSampleCount, value);
    }

    public int PlaneFlatnessMeasurementSampleCount
    {
        get => planeFlatnessMeasurementSampleCount;
        private set => SetField(ref planeFlatnessMeasurementSampleCount, value);
    }

    public bool RoiStepMeasurementVisible
    {
        get => roiStepMeasurementVisible;
        private set => SetField(ref roiStepMeasurementVisible, value);
    }

    public string RoiStepMeasurementSummary
    {
        get => roiStepMeasurementSummary;
        private set => SetField(ref roiStepMeasurementSummary, value);
    }

    public string RoiStepMeasurementDetails
    {
        get => roiStepMeasurementDetails;
        private set => SetField(ref roiStepMeasurementDetails, value);
    }

    public string RoiStepEditSummary
    {
        get => roiStepEditSummary;
        private set => SetField(ref roiStepEditSummary, value);
    }

    public string RoiStepSelectionMode
    {
        get => roiStepSelectionMode;
        private set => SetField(ref roiStepSelectionMode, value);
    }

    public double RoiStepLeftRawMean
    {
        get => roiStepLeftRawMean;
        private set => SetField(ref roiStepLeftRawMean, value);
    }

    public double RoiStepRightRawMean
    {
        get => roiStepRightRawMean;
        private set => SetField(ref roiStepRightRawMean, value);
    }

    public double RoiStepRawHeightDelta
    {
        get => roiStepRawHeightDelta;
        private set => SetField(ref roiStepRawHeightDelta, value);
    }

    public double RoiStepModelHeightDelta
    {
        get => roiStepModelHeightDelta;
        private set => SetField(ref roiStepModelHeightDelta, value);
    }

    public int RoiStepLeftPointCount
    {
        get => roiStepLeftPointCount;
        private set => SetField(ref roiStepLeftPointCount, value);
    }

    public int RoiStepRightPointCount
    {
        get => roiStepRightPointCount;
        private set => SetField(ref roiStepRightPointCount, value);
    }

    public string PerformanceSummary
    {
        get => performanceSummary;
        private set => SetField(ref performanceSummary, value);
    }

    public double ViewportFps
    {
        get => viewportFps;
        private set => SetField(ref viewportFps, value);
    }

    public double ViewportDrawMilliseconds
    {
        get => viewportDrawMilliseconds;
        private set => SetField(ref viewportDrawMilliseconds, value);
    }

    public string LazSamplingSummary
    {
        get => lazSamplingSummary;
        private set => SetField(ref lazSamplingSummary, value);
    }

    public double LazLoadMilliseconds
    {
        get => lazLoadMilliseconds;
        private set => SetField(ref lazLoadMilliseconds, value);
    }

    public double LazSamplePercent
    {
        get => lazSamplePercent;
        private set => SetField(ref lazSamplePercent, value);
    }

    public int LazSampleStride
    {
        get => lazSampleStride;
        private set => SetField(ref lazSampleStride, value);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
