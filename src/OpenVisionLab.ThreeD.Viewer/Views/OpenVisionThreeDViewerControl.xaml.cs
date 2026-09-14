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
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Automation;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Loading;
using OpenVisionLab.ThreeD.Viewer.Localization;
using OpenVisionLab.ThreeD.Viewer.Recipes;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl : UserControl, IOpenVisionThreeDViewerHost, IDisposable
{
    private static bool useSoftwareRenderingForProcess;

    public static readonly DependencyProperty SidePanelsVisibleProperty =
        DependencyProperty.Register(
            nameof(SidePanelsVisible),
            typeof(bool),
            typeof(OpenVisionThreeDViewerControl),
            new PropertyMetadata(true, OnSidePanelsVisibleChanged));

    public static readonly DependencyProperty HostStateProperty =
        DependencyProperty.Register(
            nameof(HostState),
            typeof(ViewerHostState),
            typeof(OpenVisionThreeDViewerControl),
            new PropertyMetadata(ViewerHostState.Empty));

    private const float FieldOfViewDegrees = 45.0f;
    private const string DefaultC3DSamplePath = @"3D\Samples\ThicknessCouponV1\thickness-coupon-v1.C3D";
    private const string DefaultGlbSamplePath = @"3D\PublicSamples\glTF\Box.glb";
    private const string DefaultLazSamplePath = @"3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz";
    private const double DefaultC3DHeightDeviationTolerance = 1200.0;
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

    private readonly HeightGridPoint[] generatedPointCloud = ViewerGeneratedPointCloudFactory.Create();
    private readonly ViewerSourceLoadOperationCoordinator sourceLoadOperations = new();
    private readonly ViewerOnlySourceLoadCoordinator viewerOnlySourceLoadCoordinator;
    private readonly C3DHeightGridRenderProxyCache c3dRenderProxyCache = new();
    private readonly C3DRenderResourceState c3dRenderResources = new();
    private C3DHeightGrid? c3dSample;
    private readonly C3DRenderPositionCache c3dRenderPositionCache = new();
    private uint c3dDisplayListId
    {
        get => c3dRenderResources.DisplayListId;
        set => c3dRenderResources.DisplayListId = value;
    }

    private C3DDisplayListKey? c3dDisplayListKey
    {
        get => c3dRenderResources.DisplayListKey;
        set => c3dRenderResources.DisplayListKey = value;
    }

    private C3DGpuBufferSet? c3dGpuBuffers => c3dRenderResources.GpuBuffers;

    private bool c3dGpuReleasePending => c3dRenderResources.GpuReleasePending;

    private bool c3dGpuBuffersAvailable => c3dRenderResources.GpuBuffersAvailable;

    private uint c3dInteractionDisplayListId
    {
        get => c3dRenderResources.InteractionDisplayListId;
        set => c3dRenderResources.InteractionDisplayListId = value;
    }

    private C3DDisplayListKey? c3dInteractionDisplayListKey
    {
        get => c3dRenderResources.InteractionDisplayListKey;
        set => c3dRenderResources.InteractionDisplayListKey = value;
    }
    private readonly C3DGpuTelemetry c3dGpuTelemetry = new();
    private string openGLVendor = "(pending)";
    private string openGLRenderer = "(pending)";
    private string openGLVersion = "(pending)";
    private ImportedMesh? importedMesh;
    private readonly ViewerLazPointCloudSession lazPointCloudSession = new();
    private ViewerLazPointCloudState lazSourceState => lazPointCloudSession.State;
    private LazPointCloudMetadata? lazSample
    {
        get => lazSourceState.Metadata;
        set => lazSourceState.Metadata = value;
    }

    private LazPointCloud? lazPointCloud
    {
        get => lazSourceState.PointCloud;
        set => lazSourceState.PointCloud = value;
    }

    private LazPointCloudSampleCache lazPointCloudCache => lazPointCloudSession.Cache;
    private LazPointCloudLoadCoordinator lazPointCloudLoadCoordinator => lazPointCloudSession.LoadCoordinator;
    private readonly LazPointCloudLoadTelemetry lazPointCloudLoadTelemetry = new();
    private LazSceneTransform lazSceneTransform
    {
        get => lazSourceState.SceneTransform;
        set => lazSourceState.SceneTransform = value;
    }
    private Vector3? selectedImportedMeshPoint;
    private string selectedImportedMeshPickKind = "mesh point";
    private int? selectedImportedMeshTriangleIndex;
    private Vector3? selectedImportedMeshSurfaceNormal;
    private LazPointCloudPoint? selectedLazPoint;
    private readonly ImportedMeshTextureState importedMeshTextureState = new();
    private bool smokeLazProgressScreenshotCaptured;
    private readonly MainWindowViewModel viewModel = new();
    private readonly ViewerHostEditorSurface editor;
    private readonly ViewerHostDisplayEditorSurface displayEditor;
    private readonly ViewerHostNominalActualEditorSurface nominalActualEditor;
    private readonly ViewerHostLinkedViewSurface linkedView;
    private readonly ViewerHostStateCoordinator hostStateCoordinator;
    private readonly ViewerHostOperationFacade hostOperations;
    private readonly ViewerWorkbenchOverlayRenderer workbenchOverlayRenderer;
    private readonly ViewerEventSubscription viewerEventSubscription;
    private readonly ViewerVisibleFrameRequestCoordinator visibleFrameRequests;
    private readonly ViewerSourceUnloadCancellationCoordinator sourceUnloadCancellation;
    private readonly ViewerLanguageRefreshCoordinator languageRefresh;
    private readonly NominalActualComparisonExecutor nominalActualComparisonExecutor = new();
    private readonly NominalActualComparisonCoordinator nominalActualComparisonCoordinator;
    private readonly C3DRoiEditingSession roiEditingSession;
    private readonly ViewerRecipeSaveWorkflow recipeSaveWorkflow;
    private readonly ViewerRecipeLoadWorkflow recipeLoadWorkflow;
    private readonly IViewerRecipeDialogHost recipeDialogHost;
    private readonly IViewerSamplePathResolver samplePathResolver;
    private readonly IViewerLocalizationProvider localizationProvider;
    private readonly ViewerSmokeScenarioRunner smokeScenario;
    private readonly C3DInteractionLodState interactionLodState = new();
    private readonly ViewerLinkedHeightHoverSamplingState linkedHeightHoverSampling = new();
    private readonly CancellationTokenSource viewerLifetimeCancellation = new();
    private readonly CancellationToken viewerLifetimeToken;
    private int disposalState;
    private bool isOrbiting;
    private bool isPanning;
    private bool pointerInputRegressionActive;
    private readonly ViewerInteractionTelemetry interactionTelemetry = new();
    private PointerInputRegressionResult? pointerInputRegressionResult;
    private HeightGridPoint? twoPointFirst;
    private HeightGridPoint? twoPointSecond;
    private HeightGridPoint? profileFirst;
    private HeightGridPoint? profileSecond;
    private HeightGridPoint[] profileSamples = [];
    private int profileDraggedEndpoint;
    private string? profileSourceSha256;
    private Point? profilePointerDownPosition;
    private bool profilePointerDragExceeded;
    private Vector3? importedMeshTwoPointFirst;
    private Vector3? importedMeshTwoPointSecond;
    private LazPointCloudPoint? lazTwoPointFirst;
    private LazPointCloudPoint? lazTwoPointSecond;
    private (Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 Target, Vector3 Projection)? planeReferenceMeasurement;
    private ViewerPlaneFlatnessDisplayEvaluation? planeFlatnessEvaluation;
    private int c3dDisplayListBuildCount;
    private int c3dDisplayListReleaseCount;
    private int c3dDisplayListReleaseFailureCount;
    private double lastC3DDisplayListBuildMilliseconds;
    private string lastC3DDisplayListBuildReason = "none";
    private string pendingC3DDisplayListBuildReason = "initial";
    private readonly C3DSourceApplyRenderState c3dSourceApplyRenderState = new();
    private readonly OpenGLResourceRetirementTelemetry openGLResourceRetirementTelemetry = new();
    private readonly OpenGLResourceRetirementCoordinator openGLResourceRetirement;
    private Point lastMousePosition;

    internal bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    private readonly record struct CameraSnapshot(
        double Yaw,
        double Pitch,
        double Distance,
        double TargetX,
        double TargetY,
        double TargetZ);

    private sealed record PointerInputRegressionResult(
        bool Passed,
        bool WindowActivated,
        bool PickPassed,
        bool OrbitPassed,
        bool PanPassed,
        bool RightPanPassed,
        bool RightPanMenuSuppressed,
        bool ZoomPassed,
        bool DoubleClickFitPassed,
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
        int MouseMoveTimingCount,
        double AverageMouseMoveMilliseconds,
        double MaximumMouseMoveMilliseconds,
        int NextFrameTimingCount,
        double AverageNextFrameMilliseconds,
        double MaximumNextFrameMilliseconds,
        int ScheduledMouseMoveRenderCount,
        int ImmediateMouseMoveRenderCount,
        bool InteractiveRenderPerformancePassed,
        bool InteractionLodExpected,
        bool InteractionLodPassed,
        int InteractionLodActivationCount,
        int InteractionLodMediumTransitionCount,
        int InteractionLodRestoreCount,
        int MediumGridEdgeCount,
        int CoarseGridEdgeCount,
        int InteractionGpuUploadDelta,
        bool GpuBufferReady,
        bool C3DSourceReloadedDuringInteraction,
        bool C3DSceneActive,
        int C3DRenderedPointCount,
        int C3DDisplayListBuildCount,
        double LastC3DDisplayListBuildMilliseconds,
        double ViewportWidth,
        double ViewportHeight,
        CameraSnapshot InitialCamera,
        CameraSnapshot OrbitCamera,
        CameraSnapshot PanCamera,
        CameraSnapshot RightPanCamera,
        CameraSnapshot ZoomCamera,
        CameraSnapshot DoubleClickFitCamera,
        string PickedEntity,
        string PickCoordinate,
        string SelectionSummary,
        string Failure);

    public OpenVisionThreeDViewerControl()
        : this(loadDefaultSamples: true)
    {
    }

    public OpenVisionThreeDViewerControl(bool loadDefaultSamples)
        : this(loadDefaultSamples, recipeDialogHost: null, samplePathResolver: null)
    {
    }

    public OpenVisionThreeDViewerControl(bool loadDefaultSamples, IViewerRecipeDialogHost? recipeDialogHost)
        : this(loadDefaultSamples, recipeDialogHost, samplePathResolver: null)
    {
    }

    public OpenVisionThreeDViewerControl(
        bool loadDefaultSamples,
        IViewerRecipeDialogHost? recipeDialogHost,
        IViewerSamplePathResolver? samplePathResolver)
        : this(loadDefaultSamples, recipeDialogHost, samplePathResolver, localizationProvider: null)
    {
    }

    public OpenVisionThreeDViewerControl(
        bool loadDefaultSamples,
        IViewerRecipeDialogHost? recipeDialogHost,
        IViewerSamplePathResolver? samplePathResolver,
        IViewerLocalizationProvider? localizationProvider)
    {
        this.recipeDialogHost = recipeDialogHost ?? new ViewerRecipeDialogHost(this);
        this.samplePathResolver = samplePathResolver ?? new ViewerSamplePathResolver();
        this.localizationProvider = localizationProvider ?? ViewerLocalization.Shared;
        ViewerLocalizationScope.Attach(this, this.localizationProvider);
        viewerLifetimeToken = viewerLifetimeCancellation.Token;
        hostStateCoordinator = new ViewerHostStateCoordinator(
            () => ViewerHostStateProjection.From(
                viewModel,
                new ViewerHostSourceState(
                    CurrentC3DSourcePath,
                    CurrentViewerOnlySourcePath,
                    CurrentViewerOnlySourceFormat)),
            () => IsDisposed,
            PublishHostStateChanged);
        viewerOnlySourceLoadCoordinator = new ViewerOnlySourceLoadCoordinator(lazPointCloudLoadCoordinator);
        openGLResourceRetirement = new OpenGLResourceRetirementCoordinator(
            ReleaseC3DGpuBuffers,
            ReleaseImportedMeshTexture,
            ReleaseC3DDisplayLists,
            DropOpenGLResourceReferencesAfterDispose,
            openGLResourceRetirementTelemetry);
        roiEditingSession = new C3DRoiEditingSession(viewModel, () => c3dSample, ClearPlaneReferenceMeasurement, RenderNow);
        smokeScenario = new ViewerSmokeScenarioRunner(new SmokeViewAdapter(this), viewerLifetimeToken);
        nominalActualComparisonCoordinator = new NominalActualComparisonCoordinator(
            viewModel,
            nominalActualComparisonExecutor,
            viewerLifetimeToken,
            () => IsDisposed,
            RenderNow,
            () =>
            {
                if (smokeScenario.NominalActualPreviewRequested)
                {
                    smokeScenario.MarkFailed();
                }
            });
        recipeSaveWorkflow = new ViewerRecipeSaveWorkflow(
            viewModel,
            () => c3dSample,
            () => lazPointCloud,
            () => lazPointCloud is not null
                && (!viewModel.RecipeOutputEnabled
                    || (lazTwoPointFirst is not null
                        && lazTwoPointSecond is not null
                        && viewModel.SelectedEntity.Contains("Two Point Measurement", StringComparison.OrdinalIgnoreCase)))
                && viewModel.LazSampleVisible,
            () => lazTwoPointFirst is not null && lazTwoPointSecond is not null,
            () => viewModel.SelectedSelectionMode == RoiStepSelectionMode,
            requireRoi =>
            {
                roiEditingSession.ValidateRecipeState(requireRoi, out var warning);
                return new ViewerRecipeValidationResult(warning == "Validation: OK", warning);
            },
            () =>
            {
                roiEditingSession.ValidatePlaneFlatnessRecipeState(out var warning);
                return new ViewerRecipeValidationResult(warning == "Validation: OK", warning);
            },
            roiEditingSession.CreateCurrentRoiStepRecipe,
            ResolveCurrentRecipeSourcePath,
            SetRecipeValidationOk,
            SetRecipeValidationWarning);
        recipeLoadWorkflow = new ViewerRecipeLoadWorkflow(
            new ViewerRecipeLoadWorkflowCallbacks(
                viewModel,
                loaded => c3dSample = loaded,
                loaded =>
                {
                    lazPointCloud = loaded;
                    lazSample = loaded.Metadata;
                },
                LoadLazPointCloud,
                LoadLazPointCloudAsync,
                SetLoadedLazPointCloudTelemetry,
                () =>
                {
                    lazTwoPointFirst = null;
                    lazTwoPointSecond = null;
                    selectedLazPoint = null;
                },
                heightUnit => ApplySmokeLazTwoPointMeasurement(heightUnit),
                ApplySmokeStl,
                () => importedMesh is not null,
                () => smokeScenario.RequireNominalActualPreview(),
                SetC3DSampleStatus,
                roiEditingSession.ApplyRecipeRoiStep,
                PreviewC3DPlaneFlatness,
                PreviewC3DVolume,
                PreviewC3DCrossSection,
                () =>
                {
                    planeFlatnessEvaluation = null;
                    planeReferenceMeasurement = null;
                },
                ClearWarpageTransientInspectionState,
                roiEditingSession.ApplyGapFlushRecipeRoiState,
                roiEditingSession.ApplyGapFlushPreviewOverlay,
                (first, second) => SetTwoPointMeasurement(first, second, updatePointPairReferences: false),
                RenderNow,
                () => IsDisposed,
                SetRecipeLoadFailure));
        InitializeComponent();
        if (useSoftwareRenderingForProcess)
        {
            Viewport.RenderContextType = RenderContextType.DIBSection;
        }

        if (Resources["ViewerRuntimeTextConverter"] is ViewerRuntimeTextConverter runtimeTextConverter)
        {
            runtimeTextConverter.Localization = this.localizationProvider;
        }

        sourceUnloadCancellation = new ViewerSourceUnloadCancellationCoordinator(
            Dispatcher,
            () => IsDisposed,
            () => IsLoaded,
            () =>
            {
                sourceLoadOperations.CancelCurrent();
                lazPointCloudLoadCoordinator.CancelCurrent();
            });
        languageRefresh = new ViewerLanguageRefreshCoordinator(
            Dispatcher,
            () => IsDisposed,
            () => IsLoaded,
            viewModel.RefreshLocalizedPresentation);
        visibleFrameRequests = new ViewerVisibleFrameRequestCoordinator(
            Dispatcher,
            () => IsDisposed,
            () => IsLoaded
                && IsVisible
                && Viewport.IsVisible
                && Viewport.ActualWidth >= 2
                && Viewport.ActualHeight >= 2,
            () =>
            {
                Viewport.UpdateLayout();
                Viewport.RenderTrigger = RenderTrigger.Manual;
                Viewport.DoRender();
                Viewport.RenderTrigger = RenderTrigger.TimerBased;
                Viewport.InvalidateVisual();
            });
        workbenchOverlayRenderer = new ViewerWorkbenchOverlayRenderer(
            new ViewerWorkbenchOverlayCallbacks(
                () => viewModel.IsWorkbenchAffineApplyPublished,
                () => viewModel.IsWorkbenchRegridHeightFieldPublished,
                () => viewModel.C3DSampleVisible,
                () => viewModel.CameraDistance,
                () => Viewport.ActualWidth,
                () => Viewport.ActualHeight,
                CreatePickRay,
                () => viewModel.AppliedTeachingSelections,
                () => viewModel.TeachingCaptureSnapshot,
                () => viewModel.TeachingCaptureSourceBinding));
        hostOperations = new ViewerHostOperationFacade(
            viewModel,
            () => IsDisposed,
            RequestVisibleFrame,
            new ViewerHostOperationCallbacks(
                () => ExecuteViewModelCommand(viewModel.FitAllCommand),
                () => ExecuteViewModelCommand(viewModel.FitSelectionCommand),
                () => ExecuteViewModelCommand(viewModel.FitRoiCommand),
                () => ExecuteViewModelCommand(viewModel.TopViewCommand),
                () => ExecuteViewModelCommand(viewModel.PerspectiveViewCommand),
                () => ExecuteViewModelCommand(viewModel.ResetCommand),
                path => recipeSaveWorkflow.Save(path, isSmoke: false)));

        UpdateSidePanelsVisibility();
        DataContext = viewModel;
        viewerEventSubscription = new ViewerEventSubscription(
            viewModel,
            this.localizationProvider,
            new ViewerEventHandlerSet
            {
                FitAllRequested = (_, _) => HandleFitAllCommand(),
                FitSelectionRequested = (_, _) => HandleFitSelectionCommand(),
                FitRoiRequested = (_, _) => HandleFitRoiCommand(),
                TopViewRequested = (_, _) => HandleTopViewCommand(),
                PerspectiveViewRequested = (_, _) => HandlePerspectiveViewCommand(),
                ResetRequested = (_, _) => HandleResetCommand(),
                OpenRecipeRequested = (_, _) => HandleOpenRecipeCommand(),
                SaveRecipeRequested = (_, _) => HandleSaveRecipeCommand(),
                ApplyRoiAlignmentRequested = (_, _) => HandleApplyRoiAlignmentCommand(),
                FitPlaneRequested = (_, _) => FitC3DReferencePlane(),
                PreviewThicknessRequested = (_, _) => PreviewC3DThickness(),
                PreviewWarpageRequested = (_, _) => PreviewC3DWarpage(),
                PreviewPlaneFlatnessRequested = (_, _) => PreviewC3DPlaneFlatness(),
                PreviewPointPairDimensionsRequested = (_, _) => PreviewC3DPointPairDimensions(),
                PreviewGapFlushRequested = (_, _) => PreviewC3DGapFlush(),
                PreviewVolumeRequested = (_, _) => PreviewC3DVolume(),
                PreviewCrossSectionRequested = (_, _) => PreviewC3DCrossSection(),
                ScreenshotRequested = (_, _) => HandleScreenshotCommand(),
                ProfileViewRequested = (_, _) => OpenProfileView(),
                PublishPreviewResultRequested = (_, _) => HandlePublishResultCommand(),
                NominalActualPreviewRequested = OnNominalActualPreviewRequested,
                NominalActualPublishRequested = OnNominalActualPublishRequested,
                ViewModelPropertyChanged = OnViewModelPropertyChanged,
                NominalActualPropertyChanged = OnNominalActualPropertyChanged,
                CameraChanged = OnViewModelCameraChanged,
                TeachingRoiDisplayHeightChanged = OnViewModelTeachingRoiDisplayHeightChanged,
                LanguageChanged = OnViewerLanguageChanged
            });
        viewerEventSubscription.Attach();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        c3dSample = loadDefaultSamples ? LoadDefaultC3DSample() : null;
        importedMesh = loadDefaultSamples ? LoadDefaultGlbSample() : null;
        lazSample = loadDefaultSamples ? LoadDefaultLazSample() : null;
        if (c3dSample is not null)
        {
            HeightDeviationRuleCoordinator.ApplyToViewModel(
                viewModel,
                c3dSample,
                viewModel.RecipeSourceName,
                viewModel.RecipePeakTolerance,
                viewModel.RecipeSourceUnit);
        }
        viewModel.PointCloudPointCount = generatedPointCloud.Length.ToString("N0", CultureInfo.InvariantCulture);
        SetC3DSampleStatus();
        SetGlbSampleStatus();
        SetLazSampleStatus();
        SetCurrentValue(HostStateProperty, hostStateCoordinator.Current);
        nominalActualEditor = new ViewerHostNominalActualEditorSurface(viewModel.NominalActual);
        editor = new ViewerHostEditorSurface(viewModel);
        displayEditor = new ViewerHostDisplayEditorSurface(viewModel.Display);
        linkedView = new ViewerHostLinkedViewSurface(viewModel);
    }

    public static void UseSoftwareRenderingForProcess() =>
        useSoftwareRenderingForProcess = true;

    public void ShowWorkbenchAffineApply(C3DTransformedPointCloud output, bool isPublished, bool standaloneReferenceDisplay = true)
    {
        if (IsDisposed)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(output);
        workbenchOverlayRenderer.PrepareAffineApply(output);
        viewModel.C3DSampleVisible = !standaloneReferenceDisplay;
        viewModel.SetWorkbenchAffineApply(output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchAffineApply()
    {
        workbenchOverlayRenderer.ClearAffineApply();
        viewModel.ClearWorkbenchAffineApply();
        RenderNow();
    }

    public void ShowWorkbenchRegridHeightField(C3DTransformedHeightField output, bool isPublished, bool standaloneReferenceDisplay = true)
    {
        if (IsDisposed)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(output);
        workbenchOverlayRenderer.PrepareRegridHeightField(output);
        viewModel.C3DSampleVisible = !standaloneReferenceDisplay;
        viewModel.SetWorkbenchRegridHeightField(output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchRegridHeightField()
    {
        workbenchOverlayRenderer.ClearRegridHeightField();
        viewModel.ClearWorkbenchRegridHeightField();
        RenderNow();
    }

    private static void ExecuteViewModelCommand(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

}
