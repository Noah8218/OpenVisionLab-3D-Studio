using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl : UserControl, IOpenVisionThreeDViewerHost
{
    public static readonly DependencyProperty SidePanelsVisibleProperty =
        DependencyProperty.Register(
            nameof(SidePanelsVisible),
            typeof(bool),
            typeof(OpenVisionThreeDViewerControl),
            new PropertyMetadata(true, OnSidePanelsVisibleChanged));

    private const float FieldOfViewDegrees = 45.0f;
    private const string DefaultC3DSamplePath = @"3D\Thickness\Ori_20240116_094414.C3D";
    private const string DefaultGlbSamplePath = @"3D\PublicSamples\glTF\Box.glb";
    private const string DefaultLazSamplePath = @"3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz";
    private const double DefaultC3DHeightDeviationTolerance = 1200.0;
    private const int PlaneFitMaxSampledPoints = 140000;
    private const string TwoPointSelectionMode = "Two Point Measure";
    private const string RoiStepSelectionMode = "ROI Step Compare";
    private const uint GlTexture2D = 0x0DE1;
    private const uint GlTextureMinFilter = 0x2801;
    private const uint GlTextureMagFilter = 0x2800;
    private const uint GlTextureWrapS = 0x2802;
    private const uint GlTextureWrapT = 0x2803;
    private const uint GlLinear = 0x2601;
    private const uint GlRepeat = 0x2901;
    private const uint GlRgba = 0x1908;
    private const uint GlBgra = 0x80E1;
    private const uint GlUnsignedByte = 0x1401;
    private const uint GlUnpackAlignment = 0x0CF5;

    private readonly HeightGridPoint[] generatedPointCloud = CreateGeneratedPointCloud();
    private C3DHeightGrid? c3dSample;
    private C3DHeightGrid? c3dRenderProxySource;
    private C3DHeightGridRenderProxy? c3dRenderProxy;
    private ModelTransform c3dRenderPositionsTransform;
    private Vector3[]? c3dRenderPositions;
    private uint c3dDisplayListId;
    private C3DDisplayListKey? c3dDisplayListKey;
    private ImportedMesh? importedMesh;
    private LazPointCloudMetadata? lazSample;
    private LazPointCloud? lazPointCloud;
    private (double X, double Y, double Z) lazViewerOrigin;
    private Vector3? selectedImportedMeshPoint;
    private string selectedImportedMeshPickKind = "mesh point";
    private int? selectedImportedMeshTriangleIndex;
    private Vector3? selectedImportedMeshSurfaceNormal;
    private LazPointCloudPoint? selectedLazPoint;
    private ImportedMesh? importedMeshTextureSource;
    private uint importedMeshTextureId;
    private bool importedMeshTextureUploadFailed;
    private string importedMeshTextureUploadSummary = "texture none";
    private string? smokeScreenshotPath;
    private string? smokeScreenshotQualityReportPath;
    private string? smokeContractsPath;
    private string? smokePointerInputReportPath;
    private string? smokeSaveRecipePath;
    private bool smokePublishResult;
    private bool smokeNominalActualPreview;
    private int smokeRenderFrameCount;
    private int smokeRenderFramesCompleted;
    private int smokeExitCode;
    private readonly MainWindowViewModel viewModel = new();
    private readonly EventHandler fitAllRequestedHandler;
    private readonly EventHandler fitSelectionRequestedHandler;
    private readonly EventHandler resetRequestedHandler;
    private readonly EventHandler openRecipeRequestedHandler;
    private readonly EventHandler saveRecipeRequestedHandler;
    private readonly EventHandler applyRoiAlignmentRequestedHandler;
    private readonly EventHandler fitPlaneRequestedHandler;
    private readonly EventHandler previewThicknessRequestedHandler;
    private readonly EventHandler previewWarpageRequestedHandler;
    private readonly EventHandler previewPlaneFlatnessRequestedHandler;
    private readonly EventHandler previewPointPairDimensionsRequestedHandler;
    private readonly EventHandler previewGapFlushRequestedHandler;
    private readonly EventHandler previewVolumeRequestedHandler;
    private readonly EventHandler previewCrossSectionRequestedHandler;
    private readonly EventHandler screenshotRequestedHandler;
    private readonly EventHandler profileViewRequestedHandler;
    private readonly EventHandler publishPreviewResultRequestedHandler;
    private readonly EventHandler<NominalActualPreviewRequestedEventArgs> nominalActualPreviewRequestedHandler;
    private readonly EventHandler<NominalActualPublishRequestedEventArgs> nominalActualPublishRequestedHandler;
    private readonly PropertyChangedEventHandler viewModelPropertyChangedHandler;
    private readonly PropertyChangedEventHandler nominalActualPropertyChangedHandler;
    private readonly NominalActualComparisonExecutor nominalActualComparisonExecutor = new();
    private bool viewModelEventsSubscribed;
    private bool isOrbiting;
    private bool isPanning;
    private bool pointerInputRegressionActive;
    private int pointerInputMouseDownCount;
    private int pointerInputMouseMoveCount;
    private int pointerInputMouseUpCount;
    private int pointerInputMouseWheelCount;
    private PointerInputRegressionResult? pointerInputRegressionResult;
    private string? smokePickTarget;
    private string? smokeMeasureMode;
    private string? smokeNextRenderDensity;
    private HeightGridPoint? twoPointFirst;
    private HeightGridPoint? twoPointSecond;
    private HeightGridPoint? profileFirst;
    private HeightGridPoint? profileSecond;
    private int profileDraggedEndpoint;
    private string? profileSourceSha256;
    private Point? profilePointerDownPosition;
    private bool profilePointerDragExceeded;
    private Vector3? importedMeshTwoPointFirst;
    private Vector3? importedMeshTwoPointSecond;
    private LazPointCloudPoint? lazTwoPointFirst;
    private LazPointCloudPoint? lazTwoPointSecond;
    private (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY)? roiStepLeftBounds;
    private (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY)? roiStepRightBounds;
    private Vector3? roiStepLeftCenter;
    private Vector3? roiStepRightCenter;
    private Vector3? roiStepLeftAnchor;
    private Vector3? roiStepRightAnchor;
    private HeightDeviationRecipeRoiRegion? roiStepLeftRecipeRegion;
    private HeightDeviationRecipeRoiRegion? roiStepRightRecipeRegion;
    private (Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 Target, Vector3 Projection)? planeReferenceMeasurement;
    private PlaneFlatnessEvaluation? planeFlatnessEvaluation;
    private bool roiStepInteractiveSelection;
    private bool roiStepNextPickSetsRight;
    private bool suppressRecipeParameterSync;
    private long lastFrameTimestamp;
    private int performanceFrameCount;
    private int performanceDrawCount;
    private double accumulatedFrameIntervalMilliseconds;
    private double accumulatedDrawMilliseconds;
    private Point lastMousePosition;

    private readonly record struct CameraSnapshot(
        double Yaw,
        double Pitch,
        double Distance,
        double TargetX,
        double TargetY,
        double TargetZ);

    private readonly record struct C3DDisplayListKey(
        C3DHeightGrid Source,
        ModelTransform Transform,
        ViewerGeometryStyle GeometryStyle,
        ViewerColorMap ColorMap,
        double PointSize);

    private sealed record PointerInputRegressionResult(
        bool Passed,
        bool WindowActivated,
        bool PickPassed,
        bool OrbitPassed,
        bool PanPassed,
        bool RightPanPassed,
        bool RightPanMenuSuppressed,
        bool ZoomPassed,
        bool ContextMenuPassed,
        bool ContextMenuBindingsPassed,
        int ContextMenuCommandCount,
        bool TopViewMenuBindingsPassed,
        int TopViewMenuCommandCount,
        bool RoutedEventsPassed,
        int MouseDownCount,
        int MouseMoveCount,
        int MouseUpCount,
        int MouseWheelCount,
        double ViewportWidth,
        double ViewportHeight,
        CameraSnapshot InitialCamera,
        CameraSnapshot OrbitCamera,
        CameraSnapshot PanCamera,
        CameraSnapshot RightPanCamera,
        CameraSnapshot ZoomCamera,
        string PickedEntity,
        string PickCoordinate,
        string SelectionSummary,
        string Failure);

    public OpenVisionThreeDViewerControl()
    {
        InitializeComponent();
        UpdateSidePanelsVisibility();
        DataContext = viewModel;
        fitAllRequestedHandler = (_, _) => HandleFitAllCommand();
        fitSelectionRequestedHandler = (_, _) => HandleFitSelectionCommand();
        resetRequestedHandler = (_, _) => HandleResetCommand();
        openRecipeRequestedHandler = (_, _) => HandleOpenRecipeCommand();
        saveRecipeRequestedHandler = (_, _) => HandleSaveRecipeCommand();
        applyRoiAlignmentRequestedHandler = (_, _) => HandleApplyRoiAlignmentCommand();
        fitPlaneRequestedHandler = (_, _) => FitC3DReferencePlane();
        previewThicknessRequestedHandler = (_, _) => PreviewC3DThickness();
        previewWarpageRequestedHandler = (_, _) => PreviewC3DWarpage();
        previewPlaneFlatnessRequestedHandler = (_, _) => PreviewC3DPlaneFlatness();
        previewPointPairDimensionsRequestedHandler = (_, _) => PreviewC3DPointPairDimensions();
        previewGapFlushRequestedHandler = (_, _) => PreviewC3DGapFlush();
        previewVolumeRequestedHandler = (_, _) => PreviewC3DVolume();
        previewCrossSectionRequestedHandler = (_, _) => PreviewC3DCrossSection();
        screenshotRequestedHandler = (_, _) => HandleScreenshotCommand();
        profileViewRequestedHandler = (_, _) => OpenProfileView();
        publishPreviewResultRequestedHandler = (_, _) => HandlePublishResultCommand();
        nominalActualPreviewRequestedHandler = OnNominalActualPreviewRequested;
        nominalActualPublishRequestedHandler = OnNominalActualPublishRequested;
        viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
        nominalActualPropertyChangedHandler = OnNominalActualPropertyChanged;
        SubscribeViewModelEvents();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        c3dSample = LoadDefaultC3DSample();
        importedMesh = LoadDefaultGlbSample();
        lazSample = LoadDefaultLazSample();
        ConfigureC3DHeightDeviationRule();
        viewModel.PointCloudPointCount = generatedPointCloud.Length.ToString("N0", CultureInfo.InvariantCulture);
        SetC3DSampleStatus();
        SetGlbSampleStatus();
        SetLazSampleStatus();
    }

}
