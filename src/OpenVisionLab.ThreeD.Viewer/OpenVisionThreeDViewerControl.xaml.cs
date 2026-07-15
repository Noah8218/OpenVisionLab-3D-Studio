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
    private readonly EventHandler previewPlaneFlatnessRequestedHandler;
    private readonly EventHandler previewPointPairDimensionsRequestedHandler;
    private readonly EventHandler previewGapFlushRequestedHandler;
    private readonly EventHandler previewVolumeRequestedHandler;
    private readonly EventHandler previewCrossSectionRequestedHandler;
    private readonly EventHandler screenshotRequestedHandler;
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
        bool ZoomPassed,
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
        previewPlaneFlatnessRequestedHandler = (_, _) => PreviewC3DPlaneFlatness();
        previewPointPairDimensionsRequestedHandler = (_, _) => PreviewC3DPointPairDimensions();
        previewGapFlushRequestedHandler = (_, _) => PreviewC3DGapFlush();
        previewVolumeRequestedHandler = (_, _) => PreviewC3DVolume();
        previewCrossSectionRequestedHandler = (_, _) => PreviewC3DCrossSection();
        screenshotRequestedHandler = (_, _) => HandleScreenshotCommand();
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeViewModelEvents();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeViewModelEvents();
    }

    private void SubscribeViewModelEvents()
    {
        if (viewModelEventsSubscribed)
        {
            return;
        }

        viewModel.FitAllRequested += fitAllRequestedHandler;
        viewModel.FitSelectionRequested += fitSelectionRequestedHandler;
        viewModel.ResetRequested += resetRequestedHandler;
        viewModel.OpenRecipeRequested += openRecipeRequestedHandler;
        viewModel.SaveRecipeRequested += saveRecipeRequestedHandler;
        viewModel.ApplyRoiAlignmentRequested += applyRoiAlignmentRequestedHandler;
        viewModel.FitPlaneRequested += fitPlaneRequestedHandler;
        viewModel.PreviewPlaneFlatnessRequested += previewPlaneFlatnessRequestedHandler;
        viewModel.PreviewPointPairDimensionsRequested += previewPointPairDimensionsRequestedHandler;
        viewModel.PreviewGapFlushRequested += previewGapFlushRequestedHandler;
        viewModel.PreviewVolumeRequested += previewVolumeRequestedHandler;
        viewModel.PreviewCrossSectionRequested += previewCrossSectionRequestedHandler;
        viewModel.ScreenshotRequested += screenshotRequestedHandler;
        viewModel.PublishPreviewResultRequested += publishPreviewResultRequestedHandler;
        viewModel.NominalActual.PreviewRequested += nominalActualPreviewRequestedHandler;
        viewModel.NominalActual.PublishRequested += nominalActualPublishRequestedHandler;
        viewModel.NominalActual.PropertyChanged += nominalActualPropertyChangedHandler;
        viewModel.PropertyChanged += viewModelPropertyChangedHandler;
        viewModelEventsSubscribed = true;
    }

    private void UnsubscribeViewModelEvents()
    {
        viewModel.FitAllRequested -= fitAllRequestedHandler;
        viewModel.FitSelectionRequested -= fitSelectionRequestedHandler;
        viewModel.ResetRequested -= resetRequestedHandler;
        viewModel.OpenRecipeRequested -= openRecipeRequestedHandler;
        viewModel.SaveRecipeRequested -= saveRecipeRequestedHandler;
        viewModel.ApplyRoiAlignmentRequested -= applyRoiAlignmentRequestedHandler;
        viewModel.FitPlaneRequested -= fitPlaneRequestedHandler;
        viewModel.PreviewPlaneFlatnessRequested -= previewPlaneFlatnessRequestedHandler;
        viewModel.PreviewPointPairDimensionsRequested -= previewPointPairDimensionsRequestedHandler;
        viewModel.PreviewGapFlushRequested -= previewGapFlushRequestedHandler;
        viewModel.PreviewVolumeRequested -= previewVolumeRequestedHandler;
        viewModel.PreviewCrossSectionRequested -= previewCrossSectionRequestedHandler;
        viewModel.ScreenshotRequested -= screenshotRequestedHandler;
        viewModel.PublishPreviewResultRequested -= publishPreviewResultRequestedHandler;
        viewModel.NominalActual.PreviewRequested -= nominalActualPreviewRequestedHandler;
        viewModel.NominalActual.PublishRequested -= nominalActualPublishRequestedHandler;
        viewModel.NominalActual.PropertyChanged -= nominalActualPropertyChangedHandler;
        viewModel.PropertyChanged -= viewModelPropertyChangedHandler;
        viewModelEventsSubscribed = false;
    }

    public bool SidePanelsVisible
    {
        get => (bool)GetValue(SidePanelsVisibleProperty);
        set => SetValue(SidePanelsVisibleProperty, value);
    }

    public MainWindowViewModel ViewModel => viewModel;

    public int SmokeExitCode => smokeExitCode;

    public string HostApiVersion => ViewerHostContract.ApiVersion;

    public ViewerHostState HostState => new(
        viewModel.C3DSampleVisible,
        viewModel.SelectedEntity,
        viewModel.SelectedSelectionMode,
        viewModel.PickCoordinate,
        viewModel.MeasurementSummary,
        viewModel.ResultSummary,
        viewModel.RecipeSummary,
        viewModel.ViewerStatus,
        viewModel.CoordinateFrameSummary);

    public event EventHandler<ViewerHostStateChangedEventArgs>? HostStateChanged;

    public void FitAll() => ExecuteHostCommand(viewModel.FitAllCommand);

    public void FitSelection() => ExecuteHostCommand(viewModel.FitSelectionCommand);

    public void ResetView() => ExecuteHostCommand(viewModel.ResetCommand);

    public bool SaveRecipe(string path) => SaveCurrentRecipe(path, isSmoke: false);

    public bool PublishCurrentPreviewResult()
    {
        if (viewModel.NominalActualInput is not null)
        {
            if (!viewModel.NominalActual.CanPublish)
            {
                return false;
            }

            viewModel.NominalActual.PublishCommand.Execute(null);
            return viewModel.NominalActual.State == NominalActualComparisonState.Published;
        }

        return viewModel.PublishPreviewResult();
    }

    private static void OnSidePanelsVisibleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((OpenVisionThreeDViewerControl)dependencyObject).UpdateSidePanelsVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainWindowViewModel.DeviationLegendVisible))
        {
            UpdateDeviationLegendVisibility();
        }

        if (args.PropertyName == nameof(MainWindowViewModel.PointCloudColorLegendVisible))
        {
            UpdatePointCloudColorLegendVisibility();
        }

        if (args.PropertyName is nameof(MainWindowViewModel.CubeVisible)
            or nameof(MainWindowViewModel.PointCloudVisible)
            or nameof(MainWindowViewModel.C3DSampleVisible)
            or nameof(MainWindowViewModel.GlbSampleVisible)
            or nameof(MainWindowViewModel.LazSampleVisible)
            or nameof(MainWindowViewModel.MeasurementVisible)
            or nameof(MainWindowViewModel.DisplaySettingsRevision)
            or nameof(MainWindowViewModel.PointSize)
            or nameof(MainWindowViewModel.RecipePeakTolerance)
            or nameof(MainWindowViewModel.C3DModelTransform)
            or nameof(MainWindowViewModel.SelectedSelectionMode)
            or nameof(MainWindowViewModel.SelectionOverlayVisible)
            or nameof(MainWindowViewModel.ResultOverlayVisible)
            or nameof(MainWindowViewModel.ResultEntities))
        {
            if (args.PropertyName == nameof(MainWindowViewModel.RecipePeakTolerance))
            {
                ConfigureC3DHeightDeviationRule();
            }

            if ((args.PropertyName == nameof(MainWindowViewModel.SelectedSelectionMode)
                    || args.PropertyName == nameof(MainWindowViewModel.C3DSampleVisible)
                    || args.PropertyName == nameof(MainWindowViewModel.C3DModelTransform))
                && viewModel.SelectedSelectionMode == RoiStepSelectionMode)
            {
                UpdateRoiStepMeasurement();
            }

            if (args.PropertyName == nameof(MainWindowViewModel.C3DModelTransform)
                && viewModel.SelectedSelectionMode == "Plane Distance"
                && viewModel.PlaneReferenceMeasurementVisible)
            {
                FitC3DReferencePlane();
            }

            if (args.PropertyName == nameof(MainWindowViewModel.C3DModelTransform)
                && viewModel.PlaneFlatnessVisible)
            {
                planeFlatnessEvaluation = null;
                planeReferenceMeasurement = null;
                viewModel.InvalidatePlaneFlatnessPreview("Alignment changed; run Preview Flatness again");
            }

            RenderNow();
        }
        else if (args.PropertyName == nameof(MainWindowViewModel.SelectedRenderDensity))
        {
            ReloadDefaultC3DSample();
            ReloadCurrentLazPointCloud();
            if (viewModel.SelectedSelectionMode == RoiStepSelectionMode)
            {
                UpdateRoiStepMeasurement();
            }

            RenderNow();
        }
        else if (IsRecipeRoiEditProperty(args.PropertyName))
        {
            if (!suppressRecipeParameterSync)
            {
                ApplyEditedRoiStepParameters();
            }

            RenderNow();
        }

        RaiseHostStateChanged(args.PropertyName);
    }

    private void OnNominalActualPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(NominalActualComparisonViewModel.ActualVisible)
            or nameof(NominalActualComparisonViewModel.NominalVisible)
            or nameof(NominalActualComparisonViewModel.LowerTolerance)
            or nameof(NominalActualComparisonViewModel.UpperTolerance)
            or nameof(NominalActualComparisonViewModel.PreviewResult)
            or nameof(NominalActualComparisonViewModel.SelectedDeviation)
            or nameof(NominalActualComparisonViewModel.State))
        {
            RenderNow();
        }
    }

    private async void OnNominalActualPreviewRequested(
        object? sender,
        NominalActualPreviewRequestedEventArgs args)
    {
        var comparison = viewModel.NominalActual;
        if (viewModel.NominalActualInput is not { } configuredInput)
        {
            comparison.FailPreview(args.RequestId, "Comparison inputs are not connected.");
            return;
        }

        var executionInput = configuredInput with
        {
            LowerTolerance = comparison.LowerTolerance,
            UpperTolerance = comparison.UpperTolerance
        };
        if (!executionInput.ExecutionFingerprint.Equals(args.Fingerprint, StringComparison.Ordinal))
        {
            comparison.FailPreview(args.RequestId, "Comparison input fingerprint changed before execution.");
            return;
        }

        var progress = new Progress<NominalActualComparisonProgress>(value =>
            comparison.ReportPreviewProgress(
                args.RequestId,
                value.ProcessedPointCount,
                value.TotalPointCount,
                value.Elapsed,
                value.Stage));

        try
        {
            var result = await nominalActualComparisonExecutor.ExecuteAsync(
                executionInput,
                args.MaximumDisplaySamples,
                progress,
                args.CancellationToken);
            if (!comparison.CompletePreview(args.RequestId, result))
            {
                return;
            }

            viewModel.SelectedEntity = "Nominal / Actual Surface Deviation";
            viewModel.MeasurementSummary = result.Message;
            viewModel.ViewerStatus =
                $"Nominal/actual Preview complete: {result.Status}, {result.ComparedPointCount:N0} full-query points";
            RenderNow();
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            // The ViewModel already owns the cancelled/stale state transition.
        }
        catch (Exception exception)
        {
            if (comparison.FailPreview(args.RequestId, exception.Message))
            {
                viewModel.ViewerStatus = $"Nominal/actual Preview failed: {exception.Message}";
            }

            if (smokeNominalActualPreview)
            {
                smokeExitCode = 1;
            }

            RenderNow();
        }
    }

    private void OnNominalActualPublishRequested(
        object? sender,
        NominalActualPublishRequestedEventArgs args)
    {
        var comparison = viewModel.NominalActual;
        var result = comparison.PreviewResult;
        if (result is null
            || !result.Input.ExecutionFingerprint.Equals(args.Fingerprint, StringComparison.Ordinal)
            || !viewModel.PublishNominalActualComparison(result))
        {
            viewModel.ViewerStatus = "Nominal/actual Publish failed: current Preview evidence is unavailable";
            return;
        }

        comparison.ConfirmPublished(
            $"Published result entity {NominalActualComparisonContract.ResultEntityId} | fingerprint {args.Fingerprint}");
        RenderNow();
    }

    private void RaiseHostStateChanged(string? viewModelPropertyName)
    {
        var hostPropertyName = viewModelPropertyName switch
        {
            nameof(MainWindowViewModel.C3DSampleVisible) => nameof(ViewerHostState.C3DSampleVisible),
            nameof(MainWindowViewModel.SelectedEntity) => nameof(ViewerHostState.ActiveEntity),
            nameof(MainWindowViewModel.SelectedSelectionMode) => nameof(ViewerHostState.SelectionMode),
            nameof(MainWindowViewModel.PickCoordinate) => nameof(ViewerHostState.PickCoordinate),
            nameof(MainWindowViewModel.MeasurementSummary) => nameof(ViewerHostState.MeasurementSummary),
            nameof(MainWindowViewModel.ResultSummary) => nameof(ViewerHostState.ResultSummary),
            nameof(MainWindowViewModel.RecipeSummary) => nameof(ViewerHostState.RecipeSummary),
            nameof(MainWindowViewModel.ViewerStatus) => nameof(ViewerHostState.ViewerStatus),
            _ => null
        };

        if (hostPropertyName is not null)
        {
            HostStateChanged?.Invoke(this, new ViewerHostStateChangedEventArgs(HostState, hostPropertyName));
        }
    }

    private static void ExecuteHostCommand(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void UpdateSidePanelsVisibility()
    {
        if (LeftSidePanel is null || RightSidePanel is null)
        {
            return;
        }

        var visibility = SidePanelsVisible ? Visibility.Visible : Visibility.Collapsed;
        LeftSidePanel.Visibility = visibility;
        RightSidePanel.Visibility = visibility;
        UpdateDeviationLegendVisibility();
        UpdatePointCloudColorLegendVisibility();
    }

    private void UpdateDeviationLegendVisibility()
    {
        if (DeviationLegendPanel is null)
        {
            return;
        }

        DeviationLegendPanel.Visibility = viewModel.DeviationLegendVisible && SidePanelsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdatePointCloudColorLegendVisibility()
    {
        if (PointCloudColorLegendPanel is null)
        {
            return;
        }

        PointCloudColorLegendPanel.Visibility = viewModel.PointCloudColorLegendVisible && SidePanelsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public void EnableSmokeFromCommandLine() => EnableSmokeFromCommandLine(ownsApplicationLifecycle: true);

    public void EnableSmokeFromCommandLine(bool ownsApplicationLifecycle)
    {
        var args = Environment.GetCommandLineArgs();
        var smokeIndex = Array.IndexOf(args, "--smoke-screenshot");
        if (smokeIndex >= 0 && smokeIndex + 1 < args.Length)
        {
            smokeScreenshotPath = args[smokeIndex + 1];
        }

        var screenshotQualityIndex = Array.IndexOf(args, "--smoke-screenshot-quality-report");
        if (screenshotQualityIndex >= 0 && screenshotQualityIndex + 1 < args.Length)
        {
            smokeScreenshotQualityReportPath = args[screenshotQualityIndex + 1];
        }

        ApplySmokeArguments(args);
        if (ownsApplicationLifecycle && smokeScreenshotPath is not null)
        {
            Loaded += SmokeCaptureOnLoaded;
        }
    }

    public bool HasConfiguredSmokeScreenshot => smokeScreenshotPath is not null;

    public async Task<bool> CaptureConfiguredSmokeViewAsync()
    {
        await RunConfiguredSmokeRenderFramesAsync();

        if (smokeContractsPath is not null)
        {
            WriteSceneContracts(smokeContractsPath);
        }

        if (smokeScreenshotPath is null)
        {
            return smokeExitCode == 0;
        }

        if (!await CaptureSmokeViewWithRetryAsync(smokeScreenshotPath, smokeScreenshotQualityReportPath))
        {
            SetSmokeFailure("Viewer screenshot remained blank or invalid after 3 attempts.");
        }

        return smokeExitCode == 0;
    }

    public bool ApplyConfiguredSmokePick()
    {
        switch (smokePickTarget)
        {
            case null:
                return true;
            case "cube":
                ApplySmokePickCube();
                break;
            case "c3d":
                ApplySmokePickC3D();
                break;
            case "laz":
            case "laz-point":
            case "laz-points":
                ApplySmokePickLaz();
                break;
            case "glb":
            case "mesh":
            case "glb-mesh":
                ApplySmokePickGlb();
                break;
            case "nominal-actual":
            case "nominal":
            case "deviation":
                ApplySmokePickNominalActual();
                break;
            default:
                SetSmokeFailure($"Unsupported smoke pick target: {smokePickTarget}");
                break;
        }

        RenderNow();
        return smokeExitCode == 0;
    }

    public bool ApplyConfiguredSmokeNextDensity()
    {
        if (smokeNextRenderDensity is null)
        {
            return true;
        }

        if (!viewModel.RenderDensityModes.Contains(smokeNextRenderDensity, StringComparer.Ordinal))
        {
            SetSmokeFailure($"Unsupported next Preview density: {smokeNextRenderDensity}");
            return false;
        }

        if (viewModel.NominalActual.PreviewResult is null)
        {
            SetSmokeFailure("Next Preview density smoke requires a completed nominal/actual result");
            return false;
        }

        viewModel.SelectedRenderDensity = smokeNextRenderDensity;
        RenderNow();
        return smokeExitCode == 0;
    }

    public async Task<bool> RunConfiguredPointerInputRegressionAsync()
    {
        if (smokePointerInputReportPath is null)
        {
            return true;
        }

        pointerInputRegressionResult = await RunPointerInputRegressionAsync();
        WritePointerInputRegressionReport(smokePointerInputReportPath, pointerInputRegressionResult);
        if (!pointerInputRegressionResult.Passed)
        {
            SetSmokeFailure($"Pointer input regression failed: {pointerInputRegressionResult.Failure}");
        }
        else
        {
            viewModel.ViewerStatus = "Pointer input regression passed: pick, orbit, pan, and zoom";
        }

        RenderNow();
        return pointerInputRegressionResult.Passed;
    }

    private async Task<PointerInputRegressionResult> RunPointerInputRegressionAsync()
    {
        var initialCamera = CaptureCameraSnapshot();
        var orbitCamera = initialCamera;
        var panCamera = initialCamera;
        var zoomCamera = initialCamera;
        var pickedEntity = "(none)";
        var pickCoordinate = "(none)";
        var selectionSummary = "(none)";
        var viewportWidth = 0.0;
        var viewportHeight = 0.0;
        var windowActivated = false;
        var pickPassed = false;
        var orbitPassed = false;
        var panPassed = false;
        var zoomPassed = false;
        var failure = string.Empty;
        var originalPointer = default(Point);
        var hasOriginalPointer = false;
        var leftPressed = false;
        var rightPressed = false;
        var middlePressed = false;
        Window? hostWindow = null;
        var originalTopmost = false;

        pointerInputMouseDownCount = 0;
        pointerInputMouseMoveCount = 0;
        pointerInputMouseUpCount = 0;
        pointerInputMouseWheelCount = 0;

        try
        {
            hostWindow = Window.GetWindow(this)
                ?? throw new InvalidOperationException("Viewer is not attached to a visible WPF window.");

            originalTopmost = hostWindow.Topmost;
            hostWindow.Topmost = true;
            hostWindow.Activate();
            hostWindow.Focus();
            await Dispatcher.InvokeAsync(() =>
            {
                viewModel.Reset();
                viewModel.CubeVisible = true;
                viewModel.PointCloudVisible = false;
                viewModel.SelectionOverlayVisible = false;
                viewModel.ResultOverlayVisible = false;
                viewModel.MeasurementVisible = true;
                viewModel.SelectedEntity = "Generated Unit Cube";
                viewModel.FitSelection();
                viewModel.PickCoordinate = "(none)";
                viewModel.ViewerStatus = "Pointer input regression ready";
                RenderNow();
            }, DispatcherPriority.Render);
            await Task.Delay(250);

            windowActivated = hostWindow.IsActive;
            viewportWidth = Viewport.ActualWidth;
            viewportHeight = Viewport.ActualHeight;
            if (!Viewport.IsVisible || viewportWidth < 200.0 || viewportHeight < 180.0)
            {
                throw new InvalidOperationException(
                    $"Viewport is not ready for pointer input ({viewportWidth:F0}x{viewportHeight:F0}).");
            }

            initialCamera = CaptureCameraSnapshot();
            orbitCamera = initialCamera;
            panCamera = initialCamera;
            zoomCamera = initialCamera;
            hasOriginalPointer = WindowsPointerInput.TryGetPosition(out originalPointer);
            pointerInputRegressionActive = true;

            var center = Viewport.PointToScreen(new Point(viewportWidth * 0.5, viewportHeight * 0.5));
            WindowsPointerInput.MoveTo(center);
            await Task.Delay(120);
            WindowsPointerInput.LeftDown();
            leftPressed = true;
            await Task.Delay(100);
            WindowsPointerInput.LeftUp();
            leftPressed = false;
            await Task.Delay(180);

            pickedEntity = viewModel.SelectedEntity;
            pickCoordinate = viewModel.PickCoordinate;
            selectionSummary = viewModel.SelectionSummary;
            pickPassed = pickedEntity == "Generated Unit Cube"
                && !pickCoordinate.Equals("(none)", StringComparison.Ordinal)
                && selectionSummary.StartsWith("Cube pick:", StringComparison.Ordinal);

            var orbitStart = Viewport.PointToScreen(new Point(viewportWidth * 0.72, viewportHeight * 0.60));
            var orbitEnd = Viewport.PointToScreen(new Point(viewportWidth * 0.86, viewportHeight * 0.50));
            WindowsPointerInput.MoveTo(orbitStart);
            await Task.Delay(100);
            WindowsPointerInput.RightDown();
            rightPressed = true;
            await Task.Delay(100);
            WindowsPointerInput.MoveTo(orbitEnd);
            await Task.Delay(180);
            WindowsPointerInput.RightUp();
            rightPressed = false;
            await Task.Delay(160);
            orbitCamera = CaptureCameraSnapshot();
            orbitPassed = IsFinite(orbitCamera)
                && Math.Abs(orbitCamera.Yaw - initialCamera.Yaw) > 1.0
                && Math.Abs(orbitCamera.Pitch - initialCamera.Pitch) > 1.0;

            var panStart = Viewport.PointToScreen(new Point(viewportWidth * 0.82, viewportHeight * 0.70));
            var panEnd = Viewport.PointToScreen(new Point(viewportWidth * 0.70, viewportHeight * 0.62));
            WindowsPointerInput.MoveTo(panStart);
            await Task.Delay(100);
            WindowsPointerInput.MiddleDown();
            middlePressed = true;
            await Task.Delay(100);
            WindowsPointerInput.MoveTo(panEnd);
            await Task.Delay(180);
            WindowsPointerInput.MiddleUp();
            middlePressed = false;
            await Task.Delay(160);
            panCamera = CaptureCameraSnapshot();
            panPassed = IsFinite(panCamera) && TargetChanged(orbitCamera, panCamera);

            WindowsPointerInput.MoveTo(center);
            await Task.Delay(100);
            WindowsPointerInput.Wheel(120);
            await Task.Delay(180);
            zoomCamera = CaptureCameraSnapshot();
            zoomPassed = IsFinite(zoomCamera)
                && zoomCamera.Distance < panCamera.Distance - 0.000001;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }
        finally
        {
            pointerInputRegressionActive = false;
            if (leftPressed)
            {
                WindowsPointerInput.LeftUp();
            }

            if (rightPressed)
            {
                WindowsPointerInput.RightUp();
            }

            if (middlePressed)
            {
                WindowsPointerInput.MiddleUp();
            }

            if (hasOriginalPointer)
            {
                try
                {
                    WindowsPointerInput.MoveTo(originalPointer);
                }
                catch (Win32Exception)
                {
                    // Pointer restoration is best effort after the regression evidence is captured.
                }
            }

            if (hostWindow is not null)
            {
                hostWindow.Topmost = originalTopmost;
            }
        }

        var routedEventsPassed = pointerInputMouseDownCount >= 3
            && pointerInputMouseMoveCount >= 2
            && pointerInputMouseUpCount >= 3
            && pointerInputMouseWheelCount >= 1;
        var passed = pickPassed && orbitPassed && panPassed && zoomPassed && routedEventsPassed;
        if (!passed && string.IsNullOrWhiteSpace(failure))
        {
            failure = CreatePointerInputFailureSummary(
                pickPassed,
                orbitPassed,
                panPassed,
                zoomPassed,
                routedEventsPassed);
        }

        return new PointerInputRegressionResult(
            passed,
            windowActivated,
            pickPassed,
            orbitPassed,
            panPassed,
            zoomPassed,
            routedEventsPassed,
            pointerInputMouseDownCount,
            pointerInputMouseMoveCount,
            pointerInputMouseUpCount,
            pointerInputMouseWheelCount,
            viewportWidth,
            viewportHeight,
            initialCamera,
            orbitCamera,
            panCamera,
            zoomCamera,
            pickedEntity,
            pickCoordinate,
            selectionSummary,
            failure);
    }

    private CameraSnapshot CaptureCameraSnapshot() => new(
        viewModel.YawDegrees,
        viewModel.PitchDegrees,
        viewModel.CameraDistance,
        viewModel.CameraTargetX,
        viewModel.CameraTargetY,
        viewModel.CameraTargetZ);

    private static bool IsFinite(CameraSnapshot camera) =>
        double.IsFinite(camera.Yaw)
        && double.IsFinite(camera.Pitch)
        && double.IsFinite(camera.Distance)
        && double.IsFinite(camera.TargetX)
        && double.IsFinite(camera.TargetY)
        && double.IsFinite(camera.TargetZ);

    private static bool TargetChanged(CameraSnapshot before, CameraSnapshot after)
    {
        var dx = after.TargetX - before.TargetX;
        var dy = after.TargetY - before.TargetY;
        var dz = after.TargetZ - before.TargetZ;
        return (dx * dx) + (dy * dy) + (dz * dz) > 0.00000001;
    }

    private static string CreatePointerInputFailureSummary(
        bool pickPassed,
        bool orbitPassed,
        bool panPassed,
        bool zoomPassed,
        bool routedEventsPassed)
    {
        var failures = new List<string>();
        if (!pickPassed) failures.Add("pick state did not change");
        if (!orbitPassed) failures.Add("orbit camera did not change");
        if (!panPassed) failures.Add("pan target did not change");
        if (!zoomPassed) failures.Add("zoom distance did not change");
        if (!routedEventsPassed) failures.Add("WPF mouse event counts were incomplete");
        return string.Join("; ", failures);
    }

    private static void WritePointerInputRegressionReport(
        string path,
        PointerInputRegressionResult result)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var lines = new[]
        {
            "PointerInputRegression",
            $"Result|pass={result.Passed}|windowActivated={result.WindowActivated}|viewport={result.ViewportWidth:F0}x{result.ViewportHeight:F0}",
            $"RoutedEvents|pass={result.RoutedEventsPassed}|mouseDown={result.MouseDownCount}|mouseMove={result.MouseMoveCount}|mouseUp={result.MouseUpCount}|mouseWheel={result.MouseWheelCount}",
            $"Pick|pass={result.PickPassed}|entity={result.PickedEntity}|coordinate={result.PickCoordinate}|summary={result.SelectionSummary}",
            $"Orbit|pass={result.OrbitPassed}|before={FormatCameraSnapshot(result.InitialCamera)}|after={FormatCameraSnapshot(result.OrbitCamera)}",
            $"Pan|pass={result.PanPassed}|before={FormatCameraSnapshot(result.OrbitCamera)}|after={FormatCameraSnapshot(result.PanCamera)}",
            $"Zoom|pass={result.ZoomPassed}|before={FormatCameraSnapshot(result.PanCamera)}|after={FormatCameraSnapshot(result.ZoomCamera)}",
            $"Failure|summary={result.Failure}"
        };
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
    }

    private static string FormatCameraSnapshot(CameraSnapshot camera) => string.Create(
        CultureInfo.InvariantCulture,
        $"yaw:{camera.Yaw:R},pitch:{camera.Pitch:R},distance:{camera.Distance:R},target:({camera.TargetX:R},{camera.TargetY:R},{camera.TargetZ:R})");

    private string CreatePointerInputRegressionContractLine()
    {
        if (smokePointerInputReportPath is null)
        {
            return "PointerInputRegression|configured=False";
        }

        if (pointerInputRegressionResult is null)
        {
            return "PointerInputRegression|configured=True|pass=False|failure=not-run";
        }

        var result = pointerInputRegressionResult;
        return $"PointerInputRegression|configured=True|pass={result.Passed}|pick={result.PickPassed}|orbit={result.OrbitPassed}|pan={result.PanPassed}|zoom={result.ZoomPassed}|routedEvents={result.RoutedEventsPassed}|mouseDown={result.MouseDownCount}|mouseMove={result.MouseMoveCount}|mouseUp={result.MouseUpCount}|mouseWheel={result.MouseWheelCount}|windowActivated={result.WindowActivated}|viewport={result.ViewportWidth:F0}x{result.ViewportHeight:F0}|failure={CleanContractText(result.Failure)}";
    }

    private void ApplySmokeArguments(string[] args)
    {
        var renderFramesIndex = Array.IndexOf(args, "--smoke-render-frames");
        if (renderFramesIndex >= 0)
        {
            if (renderFramesIndex + 1 >= args.Length
                || !int.TryParse(
                    args[renderFramesIndex + 1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out smokeRenderFrameCount)
                || smokeRenderFrameCount is < 16 or > 200)
            {
                smokeRenderFrameCount = 0;
                SetSmokeFailure("Smoke render frames must be an integer from 16 through 200.");
            }
        }

        var densityIndex = Array.IndexOf(args, "--smoke-density");
        if (densityIndex >= 0 && densityIndex + 1 < args.Length)
        {
            viewModel.SelectedRenderDensity = args[densityIndex + 1];
        }

        var nextDensityIndex = Array.IndexOf(args, "--smoke-next-density");
        if (nextDensityIndex >= 0 && nextDensityIndex + 1 < args.Length)
        {
            smokeNextRenderDensity = args[nextDensityIndex + 1];
        }

        var pointSizeIndex = Array.IndexOf(args, "--smoke-point-size");
        if (pointSizeIndex >= 0
            && pointSizeIndex + 1 < args.Length
            && double.TryParse(args[pointSizeIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointSize))
        {
            viewModel.PointSize = pointSize;
        }

        ApplySmokeTolerance(args);

        var sceneIndex = Array.IndexOf(args, "--smoke-scene");
        if (sceneIndex >= 0 && sceneIndex + 1 < args.Length && args[sceneIndex + 1].Equals("pointcloud", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.UsePointCloudSmokeScene();
        }

        var c3dIndex = Array.IndexOf(args, "--smoke-c3d");
        if (c3dIndex >= 0)
        {
            ApplySmokeC3D();
        }

        var glbIndex = Array.IndexOf(args, "--smoke-glb");
        if (glbIndex >= 0)
        {
            var glbPath = glbIndex + 1 < args.Length && !args[glbIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[glbIndex + 1]
                : null;
            ApplySmokeGlb(glbPath);
        }

        var stlIndex = Array.IndexOf(args, "--smoke-stl");
        if (stlIndex >= 0)
        {
            var stlPath = stlIndex + 1 < args.Length && !args[stlIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[stlIndex + 1]
                : null;
            ApplySmokeStl(stlPath);
        }

        var lazIndex = Array.IndexOf(args, "--smoke-laz");
        if (lazIndex >= 0)
        {
            var lazPath = lazIndex + 1 < args.Length && !args[lazIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[lazIndex + 1]
                : null;
            ApplySmokeLaz(lazPath);
        }

        var lazPointsIndex = Array.IndexOf(args, "--smoke-laz-points");
        if (lazPointsIndex >= 0)
        {
            var lazPath = lazPointsIndex + 1 < args.Length && !args[lazPointsIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[lazPointsIndex + 1]
                : null;
            ApplySmokeLazPoints(lazPath);
        }

        ApplySmokeNominalActual(args);

        var actionIndex = Array.IndexOf(args, "--smoke-action");
        if (actionIndex >= 0 && actionIndex + 1 < args.Length)
        {
            ApplySmokeAction(args[actionIndex + 1]);
        }

        var selectionIndex = Array.IndexOf(args, "--smoke-selection");
        if (selectionIndex >= 0 && selectionIndex + 1 < args.Length)
        {
            ApplySmokeSelection(args[selectionIndex + 1]);
        }

        var overlayIndex = Array.IndexOf(args, "--smoke-overlay");
        if (overlayIndex >= 0 && overlayIndex + 1 < args.Length)
        {
            ApplySmokeOverlay(args[overlayIndex + 1]);
        }

        var ruleIndex = Array.IndexOf(args, "--smoke-rule");
        if (ruleIndex >= 0 && ruleIndex + 1 < args.Length)
        {
            ApplySmokeRule(args[ruleIndex + 1]);
        }

        var recipeIndex = Array.IndexOf(args, "--smoke-recipe");
        if (recipeIndex >= 0 && recipeIndex + 1 < args.Length)
        {
            ApplySmokeRecipe(args[recipeIndex + 1]);
        }

        ApplySmokeTolerance(args);

        if (recipeIndex >= 0 && selectionIndex >= 0 && selectionIndex + 1 < args.Length)
        {
            ApplySmokeSelection(args[selectionIndex + 1]);
        }

        var pickIndex = Array.IndexOf(args, "--smoke-pick");
        if (pickIndex >= 0 && pickIndex + 1 < args.Length)
        {
            smokePickTarget = args[pickIndex + 1].ToLowerInvariant();
        }

        var alignmentIndex = Array.IndexOf(args, "--smoke-alignment");
        if (alignmentIndex >= 0 && alignmentIndex + 1 < args.Length)
        {
            ApplySmokeAlignment(args[alignmentIndex + 1]);
        }

        var measureIndex = Array.IndexOf(args, "--smoke-measure");
        if (measureIndex >= 0 && measureIndex + 1 < args.Length)
        {
            smokeMeasureMode = args[measureIndex + 1];
            ApplySmokeMeasure(smokeMeasureMode);
        }

        var hudIndex = Array.IndexOf(args, "--smoke-hud");
        if (hudIndex >= 0 && hudIndex + 1 < args.Length)
        {
            viewModel.HudDetailsVisible = args[hudIndex + 1].Equals("details", StringComparison.OrdinalIgnoreCase);
        }

        var editParametersIndex = Array.IndexOf(args, "--smoke-edit-parameters");
        if (editParametersIndex >= 0 && editParametersIndex + 1 < args.Length)
        {
            ApplySmokeRecipeParameterEdit(args[editParametersIndex + 1]);
        }

        var invalidRoiIndex = Array.IndexOf(args, "--smoke-invalid-roi");
        if (invalidRoiIndex >= 0 && invalidRoiIndex + 1 < args.Length)
        {
            ApplySmokeInvalidRoi(args[invalidRoiIndex + 1]);
        }

        if (Array.IndexOf(args, "--smoke-align-from-roi") >= 0)
        {
            ApplyRoiReferenceAlignment();
        }

        var contractsIndex = Array.IndexOf(args, "--smoke-contracts");
        if (contractsIndex >= 0 && contractsIndex + 1 < args.Length)
        {
            smokeContractsPath = args[contractsIndex + 1];
        }

        var pointerInputReportIndex = Array.IndexOf(args, "--smoke-pointer-input-report");
        if (pointerInputReportIndex >= 0 && pointerInputReportIndex + 1 < args.Length)
        {
            smokePointerInputReportPath = args[pointerInputReportIndex + 1];
        }

        var saveRecipeIndex = Array.IndexOf(args, "--smoke-save-recipe");
        if (saveRecipeIndex >= 0 && saveRecipeIndex + 1 < args.Length)
        {
            smokeSaveRecipePath = args[saveRecipeIndex + 1];
        }

        ApplyNominalActualViewModelVerification(args);
        ApplyDisplayViewModelVerification(args);

        smokePublishResult = Array.IndexOf(args, "--smoke-publish-result") >= 0;
        if (smokePublishResult && smokeScreenshotPath is null && !smokeNominalActualPreview)
        {
            PublishCurrentPreviewResult();
        }
    }

    private void ApplyNominalActualViewModelVerification(string[] args)
    {
        var verificationIndex = Array.IndexOf(args, "--verify-nominal-actual-viewmodel");
        if (verificationIndex < 0)
        {
            return;
        }

        if (verificationIndex + 1 >= args.Length
            || args[verificationIndex + 1].StartsWith("--", StringComparison.Ordinal))
        {
            smokeExitCode = 1;
            viewModel.ViewerStatus = "Nominal/actual ViewModel verification requires a report path.";
            return;
        }

        if (!NominalActualComparisonViewModelVerification.Verify(args[verificationIndex + 1], out var summary))
        {
            smokeExitCode = 1;
            viewModel.ViewerStatus = summary;
        }
    }

    private void ApplyDisplayViewModelVerification(string[] args)
    {
        var verificationIndex = Array.IndexOf(args, "--verify-display-viewmodel");
        if (verificationIndex < 0)
        {
            return;
        }

        if (verificationIndex + 1 >= args.Length
            || args[verificationIndex + 1].StartsWith("--", StringComparison.Ordinal))
        {
            smokeExitCode = 1;
            viewModel.ViewerStatus = "Display-settings ViewModel verification requires a report path.";
            return;
        }

        if (!ViewerDisplaySettingsViewModelVerification.Verify(args[verificationIndex + 1], out var summary))
        {
            smokeExitCode = 1;
            viewModel.ViewerStatus = summary;
        }
    }

    private void ApplySmokeNominalActual(string[] args)
    {
        var comparisonIndex = Array.IndexOf(args, "--smoke-nominal-actual");
        if (comparisonIndex < 0)
        {
            return;
        }

        smokeNominalActualPreview = true;
        if (comparisonIndex + 3 >= args.Length
            || args[comparisonIndex + 1].StartsWith("--", StringComparison.Ordinal)
            || args[comparisonIndex + 2].StartsWith("--", StringComparison.Ordinal)
            || args[comparisonIndex + 3].StartsWith("--", StringComparison.Ordinal))
        {
            SetSmokeFailure(
                "Nominal/actual smoke requires <actual.stl> <validation-query.ply> <nominal.stl>.");
            return;
        }

        try
        {
            var sourceIdentity = ResolveSmokeNominalActualSourceIdentity(args);
            var actual = CaptureComparisonFileIdentity(
                sourceIdentity.ActualId,
                sourceIdentity.ActualName,
                args[comparisonIndex + 1]);
            var query = CaptureComparisonFileIdentity(
                sourceIdentity.QueryId,
                sourceIdentity.QueryName,
                args[comparisonIndex + 2]);
            var nominal = CaptureComparisonFileIdentity(
                "source.nist-overhang-x4-nominal-9x5x5",
                "NIST Overhang X4 nominal 9x5x5 mm",
                args[comparisonIndex + 3]);
            var comparison = viewModel.NominalActual;
            var input = new NominalActualComparisonInput(
                "step.nist-overhang-x4-surface-deviation",
                actual,
                nominal,
                query,
                "mm",
                "frame.nist-overhang-x4-321-part",
                "alignment.identity-source-provided",
                comparison.LowerTolerance,
                comparison.UpperTolerance);

            ApplySmokeStl(nominal.Path);
            if (importedMesh is null)
            {
                throw new InvalidDataException("The nominal comparison mesh could not be loaded for display.");
            }

            viewModel.ConfigureNominalActualComparison(input);
            comparison.PreviewCommand.Execute(null);
        }
        catch (Exception exception)
        {
            viewModel.ClearNominalActualComparison(exception.Message);
            SetSmokeFailure($"Nominal/actual smoke failed: {exception.Message}");
        }
    }

    private static (string ActualId, string ActualName, string QueryId, string QueryName)
        ResolveSmokeNominalActualSourceIdentity(string[] args)
    {
        var datasetIndex = Array.IndexOf(args, "--smoke-nominal-actual-dataset");
        var dataset = datasetIndex < 0
            ? "nist-overhang-x4-part1"
            : datasetIndex + 1 < args.Length
                && !args[datasetIndex + 1].StartsWith("--", StringComparison.Ordinal)
                    ? args[datasetIndex + 1]
                    : throw new ArgumentException(
                        "Nominal/actual smoke dataset requires nist-overhang-x4-part1 or nist-overhang-x4-part2.");

        return dataset.ToLowerInvariant() switch
        {
            "nist-overhang-x4-part1" => (
                "source.nist-overhang-x4-actual-part1",
                "NIST Overhang X4 Part 1 XCT surface",
                "query.nist-overhang-x4-cloudcompare-vertices",
                "NIST Overhang X4 validation vertices"),
            "nist-overhang-x4-part2" => (
                "source.nist-overhang-x4-actual-part2",
                "NIST Overhang X4 Part 2 XCT surface",
                "query.nist-overhang-x4-part2-cloudcompare-vertices",
                "NIST Overhang X4 Part 2 validation vertices"),
            _ => throw new ArgumentException($"Unsupported nominal/actual smoke dataset: {dataset}"),
        };
    }

    private static NominalActualFileIdentity CaptureComparisonFileIdentity(
        string id,
        string name,
        string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        return new NominalActualFileIdentity(
            id,
            name,
            fullPath,
            stream.Length,
            Convert.ToHexString(SHA256.HashData(stream)));
    }

    private void ApplySmokeTolerance(string[] args)
    {
        var toleranceIndex = Array.IndexOf(args, "--smoke-tolerance");
        if (toleranceIndex >= 0
            && toleranceIndex + 1 < args.Length
            && double.TryParse(args[toleranceIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var tolerance))
        {
            viewModel.RecipePeakTolerance = tolerance;
        }

        var flatnessToleranceIndex = Array.IndexOf(args, "--smoke-flatness-tolerance");
        if (flatnessToleranceIndex >= 0
            && flatnessToleranceIndex + 1 < args.Length
            && double.TryParse(args[flatnessToleranceIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var flatnessTolerance))
        {
            viewModel.PlaneFlatnessTolerance = flatnessTolerance;
        }
    }

    private void ApplySmokeAlignment(string mode)
    {
        if (!viewModel.C3DSampleVisible)
        {
            ApplySmokeC3D();
        }

        var transform = mode.ToLowerInvariant() switch
        {
            "offset" or "translated" => new ModelTransform(0.350, 0.180, -0.250, 0.0, 0.0, 0.0, 1.0),
            "tilt" or "rotated" => new ModelTransform(0.250, 0.120, -0.180, 0.0, 0.0, 2.5, 1.0),
            _ => ModelTransform.Identity
        };
        var alignmentName = ModelTransformIsIdentity(transform) ? "Identity / not aligned" : $"Smoke {mode} alignment";
        viewModel.SetC3DAlignment(transform, alignmentName, "C3D source frame");
        viewModel.SelectedEntity = "C3D Alignment";
        viewModel.ViewerStatus = $"Smoke alignment: {mode}";
    }

    private void Viewport_OpenGLInitialized(object sender, OpenGLRoutedEventArgs args)
    {
        c3dDisplayListId = 0;
        c3dDisplayListKey = null;
        var gl = args.OpenGL;
        gl.ClearColor(0.08f, 0.10f, 0.13f, 1.0f);
        gl.Enable(OpenGL.GL_DEPTH_TEST);
        gl.DepthFunc(OpenGL.GL_LEQUAL);
        gl.ShadeModel(OpenGL.GL_SMOOTH);
    }

    private void Viewport_Resized(object sender, OpenGLRoutedEventArgs args)
    {
        ConfigureProjection(args.OpenGL);
    }

    private void Viewport_OpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    {
        var drawStart = Stopwatch.GetTimestamp();
        UpdateFrameInterval(drawStart);

        var gl = args.OpenGL;
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

        ConfigureProjection(gl);
        ConfigureCamera(gl);
        DrawGrid(gl);
        DrawAxes(gl);

        if (viewModel.CubeVisible)
        {
            DrawCube(gl);
        }

        if (viewModel.PointCloudVisible)
        {
            DrawPointCloud(gl, generatedPointCloud);
        }

        if (viewModel.C3DSampleVisible && c3dSample is not null)
        {
            DrawC3DHeightGrid(gl);
        }

        if (viewModel.GlbSampleVisible
            && importedMesh is not null
            && (viewModel.NominalActualInput is null || viewModel.NominalActual.NominalVisible))
        {
            DrawImportedMesh(gl);
        }

        if (viewModel.NominalActual.PreviewResult is not null
            && viewModel.NominalActual.ActualVisible)
        {
            DrawNominalActualDeviation(gl);
            DrawNominalActualSelectedDeviation(gl);
        }

        if (viewModel.LazSampleVisible && lazSample is not null)
        {
            if (lazPointCloud is null)
            {
                DrawLazMetadata(gl);
            }
            else
            {
                DrawLazPointCloud(gl);
            }
        }

        if (viewModel.MeasurementVisible)
        {
            InspectionOverlayRenderer.DrawMeasurement(gl, viewModel.CubeVisible, viewModel.PointCloudVisible);
        }

        if (viewModel.SelectionOverlayVisible)
        {
            InspectionOverlayRenderer.DrawSelectionOverlay(gl, viewModel.SelectedSelectionMode);
        }

        DrawTwoPointMeasurement(gl);
        DrawPlaneReferenceMeasurement(gl);
        DrawPlaneFlatnessExtrema(gl);
        DrawRoiStepMeasurement(gl);

        if (viewModel.ResultOverlayVisible || viewModel.ResultEntities.Count > 0)
        {
            InspectionOverlayRenderer.DrawResultOverlay(gl, viewModel.C3DSampleVisible);
        }

        gl.Flush();
        UpdateDrawPerformance(drawStart);
    }

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (pointerInputRegressionActive)
        {
            pointerInputMouseDownCount++;
        }

        lastMousePosition = e.GetPosition(Viewport);

        var panRequested = e.ChangedButton == MouseButton.Middle || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (e.ChangedButton == MouseButton.Left && !panRequested)
        {
            if (TryHandleTwoPointPick(lastMousePosition))
            {
                viewModel.NominalActual.ClearSelectedDeviation();
                RenderNow();
                return;
            }

            if (TryHandleRoiStepPick(lastMousePosition))
            {
                viewModel.NominalActual.ClearSelectedDeviation();
                RenderNow();
                return;
            }

            if (TryPickNominalActualDeviation(lastMousePosition, out var deviationSample))
            {
                SetNominalActualDeviationPick(deviationSample, "Picked nominal/actual deviation point");
            }
            else
            {
                viewModel.NominalActual.ClearSelectedDeviation();
                if (TryPickCube(lastMousePosition, out var hit))
                {
                    var summary = CameraMath.FormatPoint(hit);
                    viewModel.SelectedEntity = "Generated Unit Cube";
                    viewModel.PickCoordinate = summary;
                    viewModel.SelectionSummary = $"Cube pick: {summary}";
                    viewModel.ViewerStatus = "Picked generated cube face";
                }
                else if (TryPickC3DPoint(lastMousePosition, out var c3dPoint))
                {
                    viewModel.SelectedEntity = "C3D Height Grid";
                    viewModel.PickCoordinate = FormatC3DPoint(c3dPoint);
                    viewModel.ViewerStatus = "Picked C3D height-grid point";
                }
                else if (TryPickImportedMesh(lastMousePosition, out var importedMeshPoint, out var importedMeshPickKind, out var importedMeshTriangleIndex, out var importedMeshSurfaceNormal))
                {
                    SetImportedMeshPick(importedMeshPoint, $"Picked {viewModel.ImportedMeshFormat} {importedMeshPickKind}", importedMeshPickKind, importedMeshTriangleIndex, importedMeshSurfaceNormal);
                }
                else if (TryPickLazPoint(lastMousePosition, out var lazPoint))
                {
                    SetLazPick(lazPoint, "Picked LAZ/LAS sampled point");
                }
                else
                {
                    viewModel.SelectedEntity = "(none)";
                    viewModel.PickCoordinate = "(none)";
                    viewModel.ViewerStatus = "No pick target under cursor";
                }
            }
        }

        if (panRequested)
        {
            isPanning = true;
            Viewport.CaptureMouse();
            return;
        }

        if (e.ChangedButton is MouseButton.Left or MouseButton.Right)
        {
            isOrbiting = true;
            Viewport.CaptureMouse();
        }
    }

    private void UpdateFrameInterval(long timestamp)
    {
        if (lastFrameTimestamp != 0)
        {
            accumulatedFrameIntervalMilliseconds += Stopwatch.GetElapsedTime(lastFrameTimestamp, timestamp).TotalMilliseconds;
            performanceFrameCount++;
        }

        lastFrameTimestamp = timestamp;
    }

    private void UpdateDrawPerformance(long drawStart)
    {
        accumulatedDrawMilliseconds += Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
        performanceDrawCount++;

        if (performanceFrameCount < 15 || accumulatedFrameIntervalMilliseconds <= 0.0)
        {
            return;
        }

        var averageFrameInterval = accumulatedFrameIntervalMilliseconds / performanceFrameCount;
        var averageDraw = accumulatedDrawMilliseconds / Math.Max(1, performanceDrawCount);
        viewModel.SetRenderPerformance(1000.0 / averageFrameInterval, averageDraw);

        performanceFrameCount = 0;
        performanceDrawCount = 0;
        accumulatedFrameIntervalMilliseconds = 0.0;
        accumulatedDrawMilliseconds = 0.0;
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (pointerInputRegressionActive)
        {
            pointerInputMouseMoveCount++;
        }

        if (!isOrbiting && !isPanning)
        {
            return;
        }

        var current = e.GetPosition(Viewport);
        var delta = current - lastMousePosition;
        lastMousePosition = current;

        if (isPanning)
        {
            if (e.MiddleButton != MouseButtonState.Pressed && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                isPanning = false;
                Viewport.ReleaseMouseCapture();
                return;
            }

            PanCamera(delta);
        }
        else
        {
            if (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
            {
                isOrbiting = false;
                Viewport.ReleaseMouseCapture();
                return;
            }

            viewModel.YawDegrees += delta.X * 0.35;
            viewModel.PitchDegrees = Math.Clamp(viewModel.PitchDegrees - delta.Y * 0.35, -80.0, 80.0);
            viewModel.UpdateCameraStatus();
        }

        RenderNow();
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (pointerInputRegressionActive)
        {
            pointerInputMouseUpCount++;
        }

        isOrbiting = false;
        isPanning = false;
        Viewport.ReleaseMouseCapture();
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (pointerInputRegressionActive)
        {
            pointerInputMouseWheelCount++;
        }

        var zoomScale = e.Delta > 0 ? 0.88 : 1.14;
        viewModel.ZoomCamera(zoomScale);
        RenderNow();
    }

    private void HandleFitAllCommand()
    {
        viewModel.FitAll();
        RenderNow();
    }

    private void HandleFitSelectionCommand()
    {
        viewModel.FitSelection();
        RenderNow();
    }

    private void HandleResetCommand()
    {
        viewModel.Reset();
        RenderNow();
    }

    private void HandleScreenshotCommand()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "artifacts", $"sharpgl_viewer_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        CaptureWindow(path);
    }

    private void HandleOpenRecipeCommand()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            ApplyRecipeFile(dialog.FileName, isSmoke: false);
            RenderNow();
        }
    }

    private void HandleSaveRecipeCommand()
    {
        SaveCurrentRecipeWithDialog();
    }

    private void HandleApplyRoiAlignmentCommand()
    {
        ApplyRoiReferenceAlignment();
    }

    public void SaveCurrentRecipeWithDialog()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            FileName = ShouldSaveCurrentNominalActualRecipe()
                ? "nominal-actual-surface-deviation.recipe.json"
                : ShouldSaveCurrentLazTwoPointRecipe()
                ? "laz-two-point-measurement.recipe.json"
                : ShouldSaveCurrentGapFlushRecipe()
                    ? "c3d-gap-flush.recipe.json"
                : ShouldSaveCurrentPointPairDimensionsRecipe()
                    ? "c3d-point-pair-dimensions.recipe.json"
                    : "c3d-height-deviation.recipe.json",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            SaveCurrentRecipe(dialog.FileName, isSmoke: false);
        }
    }

    public bool SaveCurrentRecipe(string path, bool isSmoke) =>
        ShouldSaveCurrentNominalActualRecipe()
            ? SaveCurrentNominalActualRecipe(path, isSmoke)
            : ShouldSaveCurrentLazTwoPointRecipe()
            ? SaveCurrentLazTwoPointRecipe(path, isSmoke)
            : ShouldSaveCurrentGapFlushRecipe()
                ? SaveCurrentGapFlushRecipe(path, isSmoke)
            : ShouldSaveCurrentPointPairDimensionsRecipe()
                ? SaveCurrentPointPairDimensionsRecipe(path, isSmoke)
                : SaveCurrentHeightDeviationRecipe(path, isSmoke);

    private bool ShouldSaveCurrentNominalActualRecipe() =>
        viewModel.NominalActualInput is not null
        && viewModel.NominalActual.PreviewResult is not null
        && viewModel.NominalActual.State is NominalActualComparisonState.PreviewReady
            or NominalActualComparisonState.Published;

    private bool ShouldSaveCurrentLazTwoPointRecipe() =>
        lazPointCloud is not null
        && lazTwoPointFirst is not null
        && lazTwoPointSecond is not null
        && viewModel.SelectedEntity.Contains("Two Point Measurement", StringComparison.OrdinalIgnoreCase)
        && viewModel.LazSampleVisible;

    private bool ShouldSaveCurrentPointPairDimensionsRecipe() =>
        c3dSample is not null
        && viewModel.C3DSampleVisible
        && viewModel.PointPairDimensionsConfigured
        && viewModel.HasPointPairReferences;

    private bool ShouldSaveCurrentGapFlushRecipe() =>
        c3dSample is not null
        && viewModel.C3DSampleVisible
        && viewModel.GapFlushConfigured
        && viewModel.GapFlushVisible;

    public bool ApplyRoiReferenceAlignment()
    {
        if (!ValidateRecipeState(requireRoi: true, out var warning))
        {
            SetRecipeValidationWarning(warning);
            viewModel.ViewerStatus = warning;
            return false;
        }

        if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            viewModel.ViewerStatus = "ROI alignment requires a visible C3D height grid";
            return false;
        }

        if (!UpdateRoiStepMeasurement()
            || roiStepLeftBounds is not { } leftBounds
            || roiStepRightBounds is not { } rightBounds
            || roiStepLeftCenter is not { } leftCenter
            || roiStepRightCenter is not { } rightCenter)
        {
            SetRecipeValidationWarning("Validation warning: ROI alignment requires valid left and right ROI regions.");
            viewModel.ViewerStatus = "ROI alignment requires left and right ROI regions";
            return false;
        }

        var referenceX = (leftCenter.X + rightCenter.X) * 0.5f;
        var referenceY = (leftCenter.Y + rightCenter.Y) * 0.5f;
        var referenceZ = (leftCenter.Z + rightCenter.Z) * 0.5f;
        var alignedLeft = OffsetRoiRegion(CreateRoiRegion(leftBounds), -referenceX, -referenceZ);
        var alignedRight = OffsetRoiRegion(CreateRoiRegion(rightBounds), -referenceX, -referenceZ);
        var current = viewModel.C3DModelTransform;
        var transform = current with
        {
            TranslateX = current.TranslateX - referenceX,
            TranslateY = current.TranslateY - referenceY,
            TranslateZ = current.TranslateZ - referenceZ
        };

        roiStepInteractiveSelection = true;
        roiStepNextPickSetsRight = false;
        roiStepLeftRecipeRegion = alignedLeft;
        roiStepRightRecipeRegion = alignedRight;
        roiStepLeftAnchor = new Vector3((float)alignedLeft.CenterX, 0.0f, (float)alignedLeft.CenterZ);
        roiStepRightAnchor = new Vector3((float)alignedRight.CenterX, 0.0f, (float)alignedRight.CenterZ);
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        viewModel.SetC3DAlignment(transform, "ROI reference alignment", "ROI step centers");
        SyncRecipeRoiEditFromRegions("Interactive", alignedLeft, alignedRight, viewModel.RecipeRoiMaxSampledPoints);

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SetAlignmentWorkflowSummary(string.Create(
                CultureInfo.InvariantCulture,
                $"ROI alignment: ROI pair centered at origin; dT({-referenceX:F3}, {-referenceY:F3}, {-referenceZ:F3})"));
            SetRecipeValidationOk();
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "ROI alignment applied from selected regions";
            RenderNow();
            return true;
        }

        viewModel.ViewerStatus = "ROI alignment applied, but ROI measurement could not be recalculated";
        RenderNow();
        return false;
    }

    private void HandlePublishResultCommand()
    {
        viewModel.PublishPreviewResult();
        RenderNow();
    }

    private async void SmokeCaptureOnLoaded(object sender, RoutedEventArgs e)
    {
        await Dispatcher.InvokeAsync(RenderNow);
        if (smokeNominalActualPreview
            && !await WaitForNominalActualPreviewAsync(TimeSpan.FromMinutes(10)))
        {
            smokeExitCode = 1;
            if (viewModel.NominalActual.State == NominalActualComparisonState.PreviewRunning)
            {
                viewModel.ViewerStatus = "Nominal/actual Preview timed out before screenshot capture.";
            }

            await Dispatcher.InvokeAsync(RenderNow);
        }

        ApplyConfiguredSmokeNextDensity();
        await Dispatcher.InvokeAsync(RenderNow);

        if (smokePickTarget is not null)
        {
            ApplyConfiguredSmokePick();
            await Dispatcher.InvokeAsync(RenderNow);
        }

        if (smokePublishResult)
        {
            if (!PublishCurrentPreviewResult())
            {
                smokeExitCode = 1;
                viewModel.ViewerStatus = "Smoke Publish failed: current Preview evidence is unavailable";
            }

            await Dispatcher.InvokeAsync(RenderNow);
        }

        if (smokeSaveRecipePath is not null)
        {
            if (!SaveCurrentRecipe(smokeSaveRecipePath, isSmoke: true))
            {
                smokeExitCode = 1;
            }

            await Dispatcher.InvokeAsync(RenderNow);
        }

        await RunConfiguredPointerInputRegressionAsync();
        await Dispatcher.InvokeAsync(RenderNow);

        await Task.Delay(900);
        await CaptureConfiguredSmokeViewAsync();

        await Task.Delay(100);
        Application.Current.Shutdown(smokeExitCode);
    }

    private async Task<bool> WaitForNominalActualPreviewAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (viewModel.NominalActual.State == NominalActualComparisonState.PreviewRunning
            && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        return viewModel.NominalActual.State is NominalActualComparisonState.PreviewReady
            or NominalActualComparisonState.Published;
    }

    private async Task<bool> CaptureSmokeViewWithRetryAsync(string path, string? qualityReportPath)
    {
        const int maximumAttempts = 3;
        var fullPath = Path.GetFullPath(path);
        var qualityLines = new List<string>();
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            var previousRejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            if (File.Exists(previousRejectedPath))
            {
                File.Delete(previousRejectedPath);
            }
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            RenderNow();
            UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            var result = WpfScreenshotCapture.Capture(this);
            var qualityLine = $"ViewerScreenshot|attempt={attempt}|{result.Quality.Summary}";
            qualityLines.Add(qualityLine);
            Console.WriteLine(qualityLine);
            if (result.Quality.IsAcceptable)
            {
                WpfScreenshotCapture.Save(result.Bitmap, fullPath);
                qualityLines.Add($"ViewerScreenshotResult|accepted=True|attempts={attempt}|screenshot={fullPath}");
                WriteScreenshotQualityReport(qualityReportPath, qualityLines);
                viewModel.LastScreenshotPath = fullPath;
                viewModel.ViewerStatus = "Screenshot captured";
                return true;
            }

            WpfScreenshotCapture.Save(result.Bitmap, GetRejectedScreenshotPath(fullPath, attempt));
            await Task.Delay(250);
        }

        qualityLines.Add($"ViewerScreenshotResult|accepted=False|attempts={maximumAttempts}|screenshot={fullPath}");
        WriteScreenshotQualityReport(qualityReportPath, qualityLines);
        return false;
    }

    private async Task RunConfiguredSmokeRenderFramesAsync()
    {
        if (smokeRenderFrameCount == 0)
        {
            return;
        }

        ResetDrawPerformanceTelemetry();
        smokeRenderFramesCompleted = 0;
        for (var frame = 0; frame < smokeRenderFrameCount; frame++)
        {
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            smokeRenderFramesCompleted++;
        }

        if (smokeMeasureMode is not null)
        {
            ApplySmokeMeasure(smokeMeasureMode);
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
        }

        if (!double.IsFinite(viewModel.ViewportFps)
            || !double.IsFinite(viewModel.ViewportDrawMilliseconds))
        {
            SetSmokeFailure(
                $"Render performance remained pending after {smokeRenderFramesCompleted} forced frames.");
        }
    }

    private void ResetDrawPerformanceTelemetry()
    {
        lastFrameTimestamp = 0;
        performanceFrameCount = 0;
        performanceDrawCount = 0;
        accumulatedFrameIntervalMilliseconds = 0.0;
        accumulatedDrawMilliseconds = 0.0;
        viewModel.ResetRenderPerformance();
    }

    private static void WriteScreenshotQualityReport(string? path, IReadOnlyList<string> lines)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllLines(path, lines);
    }

    private static string GetRejectedScreenshotPath(string fullPath, int attempt) =>
        Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            $"{Path.GetFileNameWithoutExtension(fullPath)}.rejected-attempt-{attempt}{Path.GetExtension(fullPath)}");

    private void CaptureWindow(string path)
    {
        RenderNow();
        var result = WpfScreenshotCapture.Capture(this);
        WpfScreenshotCapture.Save(result.Bitmap, path);

        viewModel.LastScreenshotPath = Path.GetFullPath(path);
        viewModel.ViewerStatus = "Screenshot captured";
    }

    private void ApplySmokeAction(string action)
    {
        if (action.Equals("fit-selection", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.FitSelection();
        }
        else if (action.Equals("color-height", StringComparison.OrdinalIgnoreCase)
            || action.Equals("height-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Height";
        }
        else if (action.Equals("color-rgb", StringComparison.OrdinalIgnoreCase)
            || action.Equals("rgb-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "RGB";
        }
        else if (action.Equals("color-solid", StringComparison.OrdinalIgnoreCase)
            || action.Equals("solid-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Solid";
        }
        else if (action.Equals("color-grayscale", StringComparison.OrdinalIgnoreCase)
            || action.Equals("grayscale-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Grayscale";
        }
        else if (action.Equals("color-thermal", StringComparison.OrdinalIgnoreCase)
            || action.Equals("thermal-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Thermal";
        }
        else if (action.Equals("color-deviation", StringComparison.OrdinalIgnoreCase)
            || action.Equals("deviation-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Deviation";
        }
        else if (action.Equals("geometry-points", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Display.SelectedGeometryStyle = "Points";
        }
        else if (action.Equals("geometry-wireframe", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Display.SelectedGeometryStyle = "Wireframe";
        }
        else if (action.Equals("geometry-surface", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Display.SelectedGeometryStyle = "Surface";
        }
        else if (action.Equals("geometry-surface-edges", StringComparison.OrdinalIgnoreCase)
            || action.Equals("geometry-surface-with-edges", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Display.SelectedGeometryStyle = "Surface + Edges";
        }
        else if (action.Equals("pan", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Pan(-0.75, 0.35, 0.0);
        }
    }

    private void ApplySmokeSelection(string mode)
    {
        var selectionMode = mode.ToLowerInvariant() switch
        {
            "box" or "box-roi" => "Box ROI",
            "roi" or "roi-step" or "step-height" or "roi-interactive" or "interactive-roi" => RoiStepSelectionMode,
            "section" or "section-plane" => "Section Plane",
            "two-point" or "distance" or "distance-height" => TwoPointSelectionMode,
            _ => "Point"
        };

        if (selectionMode == TwoPointSelectionMode)
        {
            if (viewModel.LazSampleVisible && lazPointCloud is not null)
            {
                ApplySmokeLazTwoPointMeasurement();
            }
            else if (viewModel.GlbSampleVisible && importedMesh is not null)
            {
                ApplySmokeImportedMeshTwoPointMeasurement();
            }
            else
            {
                ApplySmokeTwoPointMeasurement();
            }

            return;
        }

        if (selectionMode == RoiStepSelectionMode)
        {
            if (mode.Equals("roi-interactive", StringComparison.OrdinalIgnoreCase)
                || mode.Equals("interactive-roi", StringComparison.OrdinalIgnoreCase))
            {
                ApplySmokeInteractiveRoiStepMeasurement();
            }
            else
            {
                ApplySmokeRoiStepMeasurement();
            }

            return;
        }

        viewModel.UseSelectionSmokeScene(selectionMode);
    }

    private void ApplySmokeMeasure(string measure)
    {
        if (measure.Equals("dimensions", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("point-pair-dimensions", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("width-distance-angle", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokePointPairDimensions();
        }
        else if (measure.Equals("two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("distance-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("laz-two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("laz-distance-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("glb-two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("mesh-two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("glb-distance-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("mesh-distance-height", StringComparison.OrdinalIgnoreCase))
        {
            if (measure.StartsWith("laz-", StringComparison.OrdinalIgnoreCase)
                || (viewModel.LazSampleVisible && lazPointCloud is not null))
            {
                ApplySmokeLazTwoPointMeasurement();
            }
            else if (measure.StartsWith("glb-", StringComparison.OrdinalIgnoreCase)
                || measure.StartsWith("mesh-", StringComparison.OrdinalIgnoreCase)
                || (viewModel.GlbSampleVisible && importedMesh is not null))
            {
                ApplySmokeImportedMeshTwoPointMeasurement();
            }
            else
            {
                ApplySmokeTwoPointMeasurement();
            }
        }
        else if (measure.Equals("roi-step", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("step-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("roi", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeRoiStepMeasurement();
        }
        else if (measure.Equals("roi-interactive", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("interactive-roi", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeInteractiveRoiStepMeasurement();
        }
        else if (measure.Equals("plane-distance", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("distance-to-plane", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("reference-plane", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokePlaneReferenceMeasurement();
        }
        else if (measure.Equals("flatness", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("plane-flatness", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("reference-roi-flatness", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokePlaneFlatness();
        }
        else if (measure.Equals("gap-flush", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("gapflush", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeGapFlush();
        }
        else if (measure.Equals("volume", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeVolume();
        }
        else if (measure.Equals("cross-section", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("cross-section-dimensions", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeCrossSection();
        }
    }

    private void ApplySmokeRecipeParameterEdit(string mode)
    {
        if (mode.Equals("laz-acceptance", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("laz-two-point", StringComparison.OrdinalIgnoreCase))
        {
            if (lazTwoPointFirst is null || lazTwoPointSecond is null)
            {
                ApplySmokeLazTwoPointMeasurement();
            }

            if (double.IsFinite(viewModel.TwoPointDistance))
            {
                viewModel.LazTwoPointExpectedDistance = viewModel.TwoPointDistance - 0.001;
            }

            if (double.IsFinite(viewModel.TwoPointRawHeightDelta))
            {
                viewModel.LazTwoPointExpectedHeightDelta = viewModel.TwoPointRawHeightDelta - 0.001;
            }

            viewModel.LazTwoPointDistanceTolerance = 0.020;
            viewModel.LazTwoPointHeightDeltaTolerance = 0.020;
            viewModel.SelectedEntity = "LAZ/LAS Two Point Measurement";
            viewModel.ViewerStatus = "Smoke recipe parameter edit: LAZ/LAS acceptance";
            return;
        }

        if (!mode.Equals("roi-align", StringComparison.OrdinalIgnoreCase)
            && !mode.Equals("roi-alignment", StringComparison.OrdinalIgnoreCase)
            && !mode.Equals("parameters", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!viewModel.C3DSampleVisible)
        {
            ApplySmokeC3D();
        }

        if (!viewModel.RoiStepMeasurementVisible)
        {
            ApplySmokeInteractiveRoiStepMeasurement();
        }

        viewModel.RecipeTransformTranslateX += 0.125;
        viewModel.RecipeTransformTranslateY += 0.025;
        viewModel.RecipeRoiLeftCenterX += 0.120;
        viewModel.RecipeRoiRightCenterZ += 0.080;
        viewModel.RecipeRoiLeftHalfWidth = Math.Max(0.050, viewModel.RecipeRoiLeftHalfWidth * 0.92);
        viewModel.RecipeRoiRightHalfDepth = Math.Max(0.050, viewModel.RecipeRoiRightHalfDepth * 0.96);
        ApplyEditedRoiStepParameters();
        viewModel.ViewerStatus = "Smoke recipe parameter edit: ROI/alignment";
    }

    private void ApplySmokeInvalidRoi(string mode)
    {
        if (!viewModel.C3DSampleVisible)
        {
            ApplySmokeC3D();
        }

        if (!viewModel.RoiStepMeasurementVisible)
        {
            ApplySmokeInteractiveRoiStepMeasurement();
        }

        if (mode.Equals("overlap", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.RecipeRoiRightCenterX = viewModel.RecipeRoiLeftCenterX;
            viewModel.RecipeRoiRightCenterZ = viewModel.RecipeRoiLeftCenterZ;
        }
        else
        {
            viewModel.RecipeRoiLeftCenterX = 1000.0;
            viewModel.RecipeRoiRightCenterX = 1002.0;
        }

        ApplyEditedRoiStepParameters();
        if (!ValidateRecipeState(requireRoi: true, out var warning))
        {
            SetRecipeValidationWarning(warning);
            viewModel.ViewerStatus = "Smoke invalid ROI: validation warning";
        }
    }

    private void ApplySmokeOverlay(string overlay)
    {
        if (overlay.Equals("result", StringComparison.OrdinalIgnoreCase))
        {
            if (viewModel.C3DSampleVisible)
            {
                viewModel.UseC3DHeightDeviationRuleSmokeScene();
            }
            else
            {
                viewModel.UseResultSmokeScene();
            }
        }
    }

    private void ApplySmokeRule(string rule)
    {
        if (rule.Equals("height-deviation", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.UseC3DHeightDeviationRuleSmokeScene();
        }
    }

    private void ApplySmokeRecipe(string path)
    {
        if (!ApplyRecipeFile(path, isSmoke: true))
        {
            smokeExitCode = 1;
        }
    }

    private bool ApplyRecipeFile(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipeType = ReadRecipeType(fullRecipePath);
            if (recipeType.Equals(NominalActualComparisonRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyNominalActualRecipe(fullRecipePath, isSmoke);
            }

            if (recipeType.Equals(LazTwoPointMeasurementRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyLazTwoPointRecipe(fullRecipePath, isSmoke);
            }

            if (recipeType.Equals(C3DGapFlushRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyC3DGapFlushRecipe(fullRecipePath, isSmoke);
            }

            return recipeType.Equals(C3DPointPairDimensionsRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
                ? ApplyC3DPointPairDimensionsRecipe(fullRecipePath, isSmoke)
                : ApplyHeightDeviationRecipe(fullRecipePath, isSmoke);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke recipe" : "Recipe", ex);
        }
    }

    private bool ApplyNominalActualRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = NominalActualComparisonRecipe.Load(fullRecipePath);
            var input = recipe.ToInput(fullRecipePath);
            ApplySmokeStl(input.NominalSource.Path);
            if (importedMesh is null)
            {
                throw new InvalidDataException("The nominal comparison mesh could not be loaded for display.");
            }

            viewModel.ConfigureNominalActualComparison(input);
            viewModel.SetNominalActualRecipeLoaded(fullRecipePath);
            smokeNominalActualPreview |= isSmoke;
            viewModel.NominalActual.PreviewCommand.Execute(null);
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke nominal/actual recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Nominal/actual recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            viewModel.ClearNominalActualComparison(ex.Message);
            return SetRecipeLoadFailure(isSmoke ? "Smoke nominal/actual recipe" : "Nominal/actual recipe", ex);
        }
    }

    private bool ApplyHeightDeviationRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = HeightDeviationRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            var grid = C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
            c3dSample = grid;
            SetC3DSampleStatus();
            var result = HeightDeviationRule.Evaluate(new HeightDeviationRuleInput(
                recipe.Source.EntityId,
                recipe.Source.Name,
                grid.Min,
                grid.Max,
                grid.Mean,
                grid.ValidSampleCount,
                recipe.Rule.PeakTolerance,
                recipe.Source.Unit));

            viewModel.ClearPlaneFlatnessRecipeStep();
            viewModel.ClearPointPairDimensionsRecipeStep();
            viewModel.ClearGapFlushRecipeStep();
            viewModel.ClearVolumeRecipeStep();
            viewModel.ClearCrossSectionRecipeStep();
            viewModel.SetC3DHeightDeviationPreview(result);
            viewModel.UseC3DHeightDeviationRuleSmokeScene();
            viewModel.SetRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath, recipe.Source.Unit, recipe.Rule.PeakTolerance);
            viewModel.SetC3DAlignment(recipe.Transform ?? ModelTransform.Identity, recipe.Transform is null ? "Recipe identity alignment" : "Recipe alignment", recipe.Source.Name);
            ApplyRecipeRoiStep(recipe.RoiStep);
            if (recipe.PlaneFlatness is { } planeFlatness)
            {
                viewModel.SetPlaneFlatnessRecipeStep(planeFlatness);
                if (planeFlatness.Enabled)
                {
                    PreviewC3DPlaneFlatness();
                }
            }
            if (recipe.Volume is { } volume)
            {
                viewModel.SetVolumeRecipeStep(volume);
                if (volume.Enabled)
                {
                    PreviewC3DVolume();
                }
            }
            if (recipe.CrossSection is { } crossSection)
            {
                viewModel.SetCrossSectionRecipeStep(crossSection);
                if (crossSection.Enabled)
                {
                    PreviewC3DCrossSection();
                }
            }
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke recipe" : "Recipe", ex);
        }
    }

    private bool ApplyC3DPointPairDimensionsRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = C3DPointPairDimensionsRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            var grid = C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
            var first = grid.ReadPoint(recipe.Step.First.Row, recipe.Step.First.Column);
            var second = grid.ReadPoint(recipe.Step.Second.Row, recipe.Step.Second.Column);

            c3dSample = grid;
            SetC3DSampleStatus();
            planeFlatnessEvaluation = null;
            planeReferenceMeasurement = null;
            viewModel.ClearPlaneFlatnessRecipeStep();
            viewModel.ClearPointPairDimensionsRecipeStep();
            viewModel.ClearGapFlushRecipeStep();
            viewModel.ClearVolumeRecipeStep();
            viewModel.ClearCrossSectionRecipeStep();
            viewModel.UseC3DSmokeScene();
            viewModel.SetC3DAlignment(
                recipe.Transform ?? ModelTransform.Identity,
                recipe.Transform is null ? "Recipe identity alignment" : "Recipe alignment",
                recipe.Source.Name);
            ApplyRecipeRoiStep(null);
            viewModel.SetPointPairDimensionsRecipeStep(recipe.Step);
            viewModel.SetPointPairRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath, recipe.Source.Unit);
            SetTwoPointMeasurement(first, second, updatePointPairReferences: false);
            viewModel.SelectedSelectionMode = TwoPointSelectionMode;
            viewModel.SelectionOverlayVisible = true;

            if (recipe.Step.Enabled && !PreviewC3DPointPairDimensions())
            {
                throw new InvalidDataException("Point pair dimensions preview failed for the configured source cells.");
            }

            viewModel.ViewerStatus = isSmoke
                ? $"Smoke point pair recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Point pair recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke point pair recipe" : "Point pair recipe", ex);
        }
    }

    private bool ApplyC3DGapFlushRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = C3DGapFlushRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            c3dSample = C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
            SetC3DSampleStatus();
            planeFlatnessEvaluation = null;
            planeReferenceMeasurement = null;
            viewModel.ClearPlaneFlatnessRecipeStep();
            viewModel.ClearPointPairDimensionsRecipeStep();
            viewModel.ClearGapFlushRecipeStep();
            viewModel.ClearVolumeRecipeStep();
            viewModel.ClearCrossSectionRecipeStep();
            viewModel.UseC3DSmokeScene();
            viewModel.SetC3DAlignment(
                recipe.Transform ?? ModelTransform.Identity,
                recipe.Transform is null ? "Recipe identity alignment" : "Recipe alignment",
                recipe.Source.Name);
            viewModel.SetGapFlushRecipeStep(recipe.Step);
            viewModel.SetPointPairRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath, recipe.Source.Unit);
            roiStepLeftRecipeRegion = recipe.Step.LeftRegion;
            roiStepRightRecipeRegion = recipe.Step.RightRegion;
            roiStepInteractiveSelection = false;
            roiStepNextPickSetsRight = false;

            if (recipe.Step.Enabled && !PreviewC3DGapFlush())
            {
                throw new InvalidDataException("Gap / Flush preview failed for the configured regions.");
            }

            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Gap / Flush recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Gap / Flush recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke Gap / Flush recipe" : "Gap / Flush recipe", ex);
        }
    }

    private bool ApplyLazTwoPointRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = LazTwoPointMeasurementRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            lazPointCloud = LoadLazPointCloud(sourcePath, recipe.Measurement.MaxSampledPoints);
            lazSample = lazPointCloud?.Metadata;
            if (lazPointCloud is null || lazSample is null)
            {
                throw new InvalidDataException("LAZ/LAS two-point recipe source could not be decoded.");
            }

            viewModel.SetLazSampleSource(sourcePath, recipe.Source.Name);
            viewModel.LazTwoPointExpectedDistance = recipe.Acceptance.ExpectedDistance;
            viewModel.LazTwoPointDistanceTolerance = recipe.Acceptance.DistanceTolerance;
            viewModel.LazTwoPointExpectedHeightDelta = recipe.Acceptance.ExpectedHeightDelta;
            viewModel.LazTwoPointHeightDeltaTolerance = recipe.Acceptance.HeightDeltaTolerance;
            ApplySmokeLazTwoPointMeasurement(recipe.Measurement.HeightUnit);
            viewModel.SetLazRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath);
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke LAZ/LAS recipe: {Path.GetFileName(fullRecipePath)}"
                : $"LAZ/LAS recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke LAZ/LAS recipe" : "LAZ/LAS recipe", ex);
        }
    }

    private bool SetRecipeLoadFailure(string label, Exception exception)
    {
        var message = $"{label} failed: {exception.Message}";
        SetRecipeValidationWarning(message);
        viewModel.ViewerStatus = message;
        return false;
    }

    private bool SaveCurrentNominalActualRecipe(string path, bool isSmoke)
    {
        try
        {
            var comparison = viewModel.NominalActual;
            if (comparison.PreviewResult is not { } result
                || comparison.State is not (NominalActualComparisonState.PreviewReady
                    or NominalActualComparisonState.Published)
                || !result.Input.ExecutionFingerprint.Equals(
                    comparison.CompletedPreviewFingerprint,
                    StringComparison.Ordinal))
            {
                viewModel.ViewerStatus =
                    "Nominal/actual recipe save requires a current completed Preview";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipe = NominalActualComparisonRecipe.FromInput(result.Input, fullRecipePath);
            recipe.Save(fullRecipePath);
            viewModel.SetNominalActualRecipeSaved(fullRecipePath);
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke nominal/actual recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Nominal/actual recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus =
                $"{(isSmoke ? "Smoke nominal/actual recipe save" : "Nominal/actual recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentHeightDeviationRecipe(string path, bool isSmoke)
    {
        try
        {
            if (!ValidateRecipeState(requireRoi: viewModel.SelectedSelectionMode == RoiStepSelectionMode, out var warning))
            {
                SetRecipeValidationWarning(warning);
                viewModel.ViewerStatus = warning;
                return false;
            }

            if (viewModel.PlaneFlatnessConfigured && !ValidatePlaneFlatnessRecipeState(out warning))
            {
                SetRecipeValidationWarning(warning);
                viewModel.ViewerStatus = warning;
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = ResolveCurrentRecipeSourcePath();
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new HeightDeviationRecipe(
                HeightDeviationRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.C3DEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                new HeightDeviationRecipeRule(viewModel.RecipePeakTolerance),
                viewModel.C3DModelTransform,
                CreateCurrentRoiStepRecipe(),
                viewModel.PlaneFlatnessConfigured ? viewModel.CreatePlaneFlatnessRecipeStep() : null,
                viewModel.VolumeConfigured ? viewModel.CreateVolumeRecipeStep() : null,
                viewModel.CrossSectionConfigured ? viewModel.CreateCrossSectionRecipeStep() : null);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke recipe save" : "Recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentLazTwoPointRecipe(string path, bool isSmoke)
    {
        try
        {
            if (lazPointCloud is null || lazTwoPointFirst is null || lazTwoPointSecond is null)
            {
                viewModel.ViewerStatus = "LAZ/LAS two-point recipe save requires a measured LAZ/LAS pair";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(lazPointCloud.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new LazTwoPointMeasurementRecipe(
                LazTwoPointMeasurementRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.LazEntityId,
                    viewModel.LazSampleName,
                    sourceRecipePath,
                    "source-units"),
                new LazTwoPointMeasurementRecipeMeasurement(
                    "sample-extreme-x",
                    Math.Max(2, lazPointCloud.SampledPoints.Length),
                    "source-z-units"),
                new LazTwoPointMeasurementRecipeAcceptance(
                    viewModel.LazTwoPointExpectedDistance,
                    viewModel.LazTwoPointDistanceTolerance,
                    viewModel.LazTwoPointExpectedHeightDelta,
                    viewModel.LazTwoPointHeightDeltaTolerance));

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke LAZ recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"LAZ recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke LAZ recipe save" : "LAZ recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentPointPairDimensionsRecipe(string path, bool isSmoke)
    {
        try
        {
            var step = viewModel.CreatePointPairDimensionsRecipeStep();
            if (c3dSample is null || step is null)
            {
                viewModel.ViewerStatus = "Point pair recipe save requires two selected C3D source cells";
                return false;
            }

            c3dSample.ReadPoint(step.First.Row, step.First.Column);
            c3dSample.ReadPoint(step.Second.Row, step.Second.Column);
            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(c3dSample.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new C3DPointPairDimensionsRecipe(
                C3DPointPairDimensionsRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.C3DEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                viewModel.C3DModelTransform,
                step);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke point pair recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Point pair recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke point pair recipe save" : "Point pair recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentGapFlushRecipe(string path, bool isSmoke)
    {
        try
        {
            if (c3dSample is null || !viewModel.GapFlushVisible)
            {
                viewModel.ViewerStatus = "Gap / Flush recipe save requires a successful preview";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(c3dSample.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new C3DGapFlushRecipe(
                C3DGapFlushRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.C3DEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                viewModel.C3DModelTransform,
                viewModel.CreateGapFlushRecipeStep());

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Gap / Flush recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Gap / Flush recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke Gap / Flush recipe save" : "Gap / Flush recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private HeightDeviationRecipeRoiStep? CreateCurrentRoiStepRecipe()
    {
        if (!viewModel.RoiStepMeasurementVisible)
        {
            return null;
        }

        return new HeightDeviationRecipeRoiStep(
            viewModel.RecipeRoiMode,
            CreateLeftRoiRegionFromViewModel(),
            CreateRightRoiRegionFromViewModel(),
            viewModel.RecipeRoiMaxSampledPoints);
    }

    private static HeightDeviationRecipeRoiRegion CreateRoiRegion((float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) bounds) =>
        new(
            (bounds.MinX + bounds.MaxX) * 0.5,
            (bounds.MinZ + bounds.MaxZ) * 0.5,
            Math.Max(0.0001, (bounds.MaxX - bounds.MinX) * 0.5),
            Math.Max(0.0001, (bounds.MaxZ - bounds.MinZ) * 0.5));

    private static HeightDeviationRecipeRoiRegion OffsetRoiRegion(HeightDeviationRecipeRoiRegion region, double offsetX, double offsetZ) =>
        new(region.CenterX + offsetX, region.CenterZ + offsetZ, region.HalfWidth, region.HalfDepth);

    private HeightDeviationRecipeRoiRegion CreateLeftRoiRegionFromViewModel() =>
        new(
            viewModel.RecipeRoiLeftCenterX,
            viewModel.RecipeRoiLeftCenterZ,
            viewModel.RecipeRoiLeftHalfWidth,
            viewModel.RecipeRoiLeftHalfDepth);

    private HeightDeviationRecipeRoiRegion CreateRightRoiRegionFromViewModel() =>
        new(
            viewModel.RecipeRoiRightCenterX,
            viewModel.RecipeRoiRightCenterZ,
            viewModel.RecipeRoiRightHalfWidth,
            viewModel.RecipeRoiRightHalfDepth);

    private bool ValidateRecipeState(bool requireRoi, out string warning)
    {
        var transform = viewModel.C3DModelTransform;
        if (!double.IsFinite(transform.TranslateX)
            || !double.IsFinite(transform.TranslateY)
            || !double.IsFinite(transform.TranslateZ)
            || !double.IsFinite(transform.RotateXDegrees)
            || !double.IsFinite(transform.RotateYDegrees)
            || !double.IsFinite(transform.RotateZDegrees)
            || !double.IsFinite(transform.Scale)
            || transform.Scale <= 0.0)
        {
            warning = "Validation warning: transform values must be finite and scale must be positive.";
            return false;
        }

        if (!requireRoi)
        {
            warning = "Validation: OK";
            return true;
        }

        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length == 0)
        {
            warning = "Validation warning: ROI validation requires a visible C3D height grid.";
            return false;
        }

        var left = CreateLeftRoiRegionFromViewModel();
        var right = CreateRightRoiRegionFromViewModel();
        if (!IsValidRegion(left) || !IsValidRegion(right))
        {
            warning = "Validation warning: ROI center and size values must be finite and positive.";
            return false;
        }

        var bounds = GetTransformedC3DBounds();
        if (!RegionIntersectsBounds(left, bounds))
        {
            warning = "Validation warning: left ROI is outside the visible C3D bounds.";
            return false;
        }

        if (!RegionIntersectsBounds(right, bounds))
        {
            warning = "Validation warning: right ROI is outside the visible C3D bounds.";
            return false;
        }

        if (RegionsOverlap(left, right))
        {
            warning = "Validation warning: left and right ROI regions overlap.";
            return false;
        }

        if (!TryCalculateRoiStats(CreateRoiBounds(left, bounds), out var leftStats) || leftStats.Count < 10)
        {
            warning = "Validation warning: left ROI has too few C3D samples.";
            return false;
        }

        if (!TryCalculateRoiStats(CreateRoiBounds(right, bounds), out var rightStats) || rightStats.Count < 10)
        {
            warning = "Validation warning: right ROI has too few C3D samples.";
            return false;
        }

        warning = "Validation: OK";
        return true;
    }

    private bool ValidatePlaneFlatnessRecipeState(out string warning)
    {
        var step = viewModel.CreatePlaneFlatnessRecipeStep();
        if (!IsValidRegion(step.ReferenceRegion)
            || !double.IsFinite(step.Tolerance)
            || step.Tolerance <= 0.0)
        {
            warning = "Validation warning: flatness reference ROI and tolerance must be finite and positive.";
            return false;
        }

        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length == 0)
        {
            warning = "Validation warning: plane flatness requires a visible C3D height grid.";
            return false;
        }

        var referenceSampleCount = c3dSample.Points.Count(point => Contains(step.ReferenceRegion, TransformC3DPosition(point.Position)));
        if (referenceSampleCount < 3)
        {
            warning = "Validation warning: flatness reference ROI contains fewer than three C3D samples.";
            return false;
        }

        warning = "Validation: OK";
        return true;
    }

    private void SetRecipeValidationOk() => viewModel.SetRecipeValidationSummary("Validation: OK");

    private void SetRecipeValidationWarning(string warning) => viewModel.SetRecipeValidationSummary(warning);

    private static bool IsValidRegion(HeightDeviationRecipeRoiRegion region) =>
        double.IsFinite(region.CenterX)
        && double.IsFinite(region.CenterZ)
        && double.IsFinite(region.HalfWidth)
        && double.IsFinite(region.HalfDepth)
        && region.HalfWidth > 0.0
        && region.HalfDepth > 0.0;

    private static bool RegionsOverlap(HeightDeviationRecipeRoiRegion left, HeightDeviationRecipeRoiRegion right) =>
        Math.Abs(left.CenterX - right.CenterX) < left.HalfWidth + right.HalfWidth
        && Math.Abs(left.CenterZ - right.CenterZ) < left.HalfDepth + right.HalfDepth;

    private static bool RegionIntersectsBounds(
        HeightDeviationRecipeRoiRegion region,
        (float MinX, float MaxX, float MinZ, float MaxZ) bounds) =>
        region.CenterX + region.HalfWidth >= bounds.MinX
        && region.CenterX - region.HalfWidth <= bounds.MaxX
        && region.CenterZ + region.HalfDepth >= bounds.MinZ
        && region.CenterZ - region.HalfDepth <= bounds.MaxZ;

    private void ApplyEditedRoiStepParameters()
    {
        if (!viewModel.C3DSampleVisible)
        {
            viewModel.UseC3DSmokeScene();
        }

        roiStepInteractiveSelection = true;
        roiStepNextPickSetsRight = false;
        roiStepLeftRecipeRegion = CreateLeftRoiRegionFromViewModel();
        roiStepRightRecipeRegion = CreateRightRoiRegionFromViewModel();
        roiStepLeftAnchor = new Vector3((float)viewModel.RecipeRoiLeftCenterX, 0.0f, (float)viewModel.RecipeRoiLeftCenterZ);
        roiStepRightAnchor = new Vector3((float)viewModel.RecipeRoiRightCenterX, 0.0f, (float)viewModel.RecipeRoiRightCenterZ);
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        if (UpdateRoiStepMeasurement())
        {
            if (ValidateRecipeState(requireRoi: true, out var warning))
            {
                SetRecipeValidationOk();
            }
            else
            {
                SetRecipeValidationWarning(warning);
            }

            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Recipe ROI parameters updated";
        }
        else
        {
            ValidateRecipeState(requireRoi: true, out var warning);
            SetRecipeValidationWarning(warning);
        }
    }

    private void ApplyRecipeRoiStep(HeightDeviationRecipeRoiStep? roiStep)
    {
        ClearRecipeRoiStep();
        if (roiStep is null)
        {
            return;
        }

        if (!viewModel.C3DSampleVisible)
        {
            viewModel.UseC3DSmokeScene();
        }

        roiStepInteractiveSelection = roiStep.Mode.Equals("Interactive", StringComparison.OrdinalIgnoreCase);
        roiStepNextPickSetsRight = false;
        roiStepLeftRecipeRegion = roiStep.Left;
        roiStepRightRecipeRegion = roiStep.Right;
        SyncRecipeRoiEditFromRegions(roiStep.Mode, roiStep.Left, roiStep.Right, roiStep.MaxSampledPoints);
        roiStepLeftAnchor = new Vector3((float)roiStep.Left.CenterX, 0.0f, (float)roiStep.Left.CenterZ);
        roiStepRightAnchor = new Vector3((float)roiStep.Right.CenterX, 0.0f, (float)roiStep.Right.CenterZ);
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Recipe ROI step restored";
        }
    }

    private void ClearRecipeRoiStep()
    {
        roiStepLeftRecipeRegion = null;
        roiStepRightRecipeRegion = null;
    }

    private void SyncRecipeRoiEditFromBounds(
        string mode,
        (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) leftBounds,
        (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) rightBounds)
    {
        SyncRecipeRoiEditFromRegions(
            mode,
            CreateRoiRegion(leftBounds),
            CreateRoiRegion(rightBounds),
            viewModel.RecipeRoiMaxSampledPoints);
    }

    private void SyncRecipeRoiEditFromRegions(
        string mode,
        HeightDeviationRecipeRoiRegion left,
        HeightDeviationRecipeRoiRegion right,
        int maxSampledPoints)
    {
        suppressRecipeParameterSync = true;
        try
        {
            viewModel.SetRecipeRoiStepEdit(
                mode,
                left.CenterX,
                left.CenterZ,
                left.HalfWidth,
                left.HalfDepth,
                right.CenterX,
                right.CenterZ,
                right.HalfWidth,
                right.HalfDepth,
                maxSampledPoints);
        }
        finally
        {
            suppressRecipeParameterSync = false;
        }
    }

    private string ResolveCurrentRecipeSourcePath()
    {
        var candidate = viewModel.RecipeSourcePath;
        if (!Path.IsPathRooted(candidate))
        {
            candidate = Path.GetFullPath(candidate);
        }

        if (File.Exists(candidate))
        {
            return candidate;
        }

        var defaultSample = FindDefaultC3DSamplePath();
        return defaultSample is not null ? Path.GetFullPath(defaultSample) : candidate;
    }

    private void ApplySmokeC3D()
    {
        if (c3dSample is null)
        {
            SetSmokeFailure("Smoke C3D failed: sample missing or unsupported");
            return;
        }

        viewModel.UseC3DSmokeScene();
    }

    private void ApplySmokeGlb(string? path)
    {
        selectedImportedMeshPoint = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            importedMesh = LoadGlbSample(path);
            if (importedMesh is not null)
            {
                SetGlbSampleStatus();
            }
        }

        if (importedMesh is null)
        {
            viewModel.UseGlbFailureScene(viewModel.GlbSampleSummary);
            SetSmokeFailure(CreateSmokeFailureMessage("Smoke GLB failed", viewModel.GlbSampleSummary));
            return;
        }

        viewModel.UseGlbSmokeScene();
    }

    private void ApplySmokeStl(string? path)
    {
        selectedImportedMeshPoint = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            importedMesh = null;
            viewModel.SetGlbSampleSource("(none)", "STL Mesh", "STL");
            viewModel.GlbSampleTriangleCount = "(missing)";
            viewModel.GlbSampleSummary = "Missing STL sample path";
        }
        else
        {
            importedMesh = LoadStlSample(path);
            if (importedMesh is not null)
            {
                SetGlbSampleStatus();
            }
        }

        if (importedMesh is null)
        {
            viewModel.UseGlbFailureScene(viewModel.GlbSampleSummary);
            SetSmokeFailure(CreateSmokeFailureMessage("Smoke STL failed", viewModel.GlbSampleSummary));
            return;
        }

        viewModel.UseGlbSmokeScene();
    }

    private void ApplySmokeLaz(string? path)
    {
        selectedImportedMeshPoint = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedLazPoint = null;
        twoPointFirst = null;
        twoPointSecond = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        lazPointCloud = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            lazSample = LoadLazSample(path);
            if (lazSample is not null)
            {
                SetLazSampleStatus();
            }
        }

        if (lazSample is null)
        {
            viewModel.UseLazFailureScene(viewModel.LazSampleSummary);
            SetSmokeFailure(CreateSmokeFailureMessage("Smoke LAZ failed", viewModel.LazSampleSummary));
            return;
        }

        viewModel.UseLazSmokeScene();
    }

    private void ApplySmokeLazPoints(string? path)
    {
        selectedImportedMeshPoint = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedLazPoint = null;
        twoPointFirst = null;
        twoPointSecond = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            lazPointCloud = LoadLazPointCloud(path);
            lazSample = lazPointCloud?.Metadata;
            if (lazPointCloud is not null && lazSample is not null)
            {
                SetLazSampleStatus();
            }
        }
        else if (lazPointCloud is null && lazSample is not null)
        {
            lazPointCloud = LoadLazPointCloud(lazSample.SourcePath);
            lazSample = lazPointCloud?.Metadata ?? lazSample;
            if (lazPointCloud is not null)
            {
                SetLazSampleStatus();
            }
        }

        if (lazPointCloud is null || lazSample is null)
        {
            viewModel.UseLazFailureScene(viewModel.LazSampleSummary);
            SetSmokeFailure(CreateSmokeFailureMessage("Smoke LAZ/LAS points failed", viewModel.LazSampleSummary));
            return;
        }

        viewModel.UseLazPointSmokeScene();
    }

    private void SetSmokeFailure(string message)
    {
        smokeExitCode = 1;
        viewModel.ViewerStatus = message;
    }

    private static string CreateSmokeFailureMessage(string prefix, string detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? $"{prefix}: sample missing or unsupported"
            : $"{prefix}: {detail}";

    private void ApplySmokePickLaz()
    {
        if (lazPointCloud is null)
        {
            ApplySmokeLazPoints(null);
        }

        if (lazPointCloud is null || lazPointCloud.SampledPoints.Length == 0)
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Smoke pick failed: LAZ/LAS point cloud missing";
            return;
        }

        viewModel.UseLazPointSmokeScene();
        var target = FindLazSmokePickTarget();
        var viewerPosition = MapLazPosition(target.Position);
        viewModel.CameraTargetX = viewerPosition.X;
        viewModel.CameraTargetY = viewerPosition.Y;
        viewModel.CameraTargetZ = viewerPosition.Z;
        viewModel.UpdateCameraStatus();

        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickLazPoint(center, out var point))
        {
            SetLazPick(point, "Smoke pick: LAZ/LAS sampled point");
        }
        else
        {
            SetLazPick(target, "Smoke pick: LAZ/LAS sampled point fallback");
        }
    }

    private void ApplySmokePickGlb()
    {
        if (importedMesh is null)
        {
            ApplySmokeGlb(null);
        }

        if (importedMesh is null || importedMesh.Positions.Length == 0)
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            SetSmokeFailure($"Smoke pick failed: {viewModel.ImportedMeshFormat} mesh missing");
            return;
        }

        viewModel.UseGlbSmokeScene();
        var target = FindImportedMeshSmokeSurfacePickTarget();
        viewModel.CameraTargetX = target.X;
        viewModel.CameraTargetY = target.Y;
        viewModel.CameraTargetZ = target.Z;
        viewModel.UpdateCameraStatus();

        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickImportedMesh(center, out var point, out var pickKind, out var triangleIndex, out var surfaceNormal))
        {
            SetImportedMeshPick(point, $"Smoke pick: {viewModel.ImportedMeshFormat} {pickKind}", pickKind, triangleIndex, surfaceNormal);
        }
        else
        {
            SetImportedMeshPick(target, $"Smoke pick: {viewModel.ImportedMeshFormat} mesh point fallback", "mesh point fallback");
        }
    }

    private void ApplySmokePickNominalActual()
    {
        var comparison = viewModel.NominalActual;
        if (!comparison.ActualVisible || comparison.PreviewResult is null)
        {
            SetSmokeFailure("Smoke pick failed: nominal/actual Preview result is unavailable");
            return;
        }

        viewModel.SelectedSelectionMode = "Point";
        var center = new Point(
            Math.Max(1.0, Viewport.ActualWidth) / 2.0,
            Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickNominalActualDeviation(center, out var sample))
        {
            SetNominalActualDeviationPick(sample, "Smoke pick: nominal/actual deviation point");
            return;
        }

        SetSmokeFailure("Smoke pick failed: no rendered nominal/actual point under the viewport center");
    }

    private void ApplySmokePickCube()
    {
        viewModel.Reset();
        viewModel.CubeVisible = true;
        viewModel.PointCloudVisible = false;
        viewModel.SelectionOverlayVisible = false;
        viewModel.ResultOverlayVisible = false;
        viewModel.MeasurementVisible = true;
        viewModel.SelectedEntity = "Generated Unit Cube";
        viewModel.FitSelection();

        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickCube(center, out var hit))
        {
            var summary = CameraMath.FormatPoint(hit);
            viewModel.SelectedEntity = "Generated Unit Cube";
            viewModel.PickCoordinate = summary;
            viewModel.SelectionSummary = $"Cube pick: {summary}";
            viewModel.ViewerStatus = "Smoke pick: generated cube";
        }
        else
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Smoke pick failed";
        }
    }

    private void ApplySmokePickC3D()
    {
        if (c3dSample is null)
        {
            viewModel.ViewerStatus = "Smoke pick failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickC3DPoint(center, out var point))
        {
            viewModel.SelectedEntity = "C3D Height Grid";
            viewModel.PickCoordinate = FormatC3DPoint(point);
            viewModel.ViewerStatus = "Smoke pick: C3D height grid";
        }
        else
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Smoke pick failed: C3D height grid";
        }
    }

    private void ApplySmokeTwoPointMeasurement()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        viewModel.SelectedSelectionMode = TwoPointSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        var first = c3dSample.Points.MinBy(point => point.RawValue);
        var second = c3dSample.Points.MaxBy(point => point.RawValue);
        SetTwoPointMeasurement(first, second);
        viewModel.SelectedEntity = "Two Point Measurement";
        viewModel.PickCoordinate = FormatC3DPoint(second);
        viewModel.ViewerStatus = "Smoke measure: two-point distance and height delta";
    }

    private void ApplySmokePointPairDimensions()
    {
        viewModel.UseC3DSmokeScene();
        ApplySmokeTwoPointMeasurement();
        if (twoPointFirst is null || twoPointSecond is null)
        {
            SetSmokeFailure("Smoke dimensions failed: C3D point pair missing");
            return;
        }

        var delta = TransformC3DPosition(twoPointSecond.Value.Position)
            - TransformC3DPosition(twoPointFirst.Value.Position);
        var width = Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        viewModel.PointPairExpectedDistance = delta.Length();
        viewModel.PointPairDistanceTolerance = 0.001;
        viewModel.PointPairExpectedWidth = width;
        viewModel.PointPairWidthTolerance = 0.001;
        viewModel.PointPairExpectedAngleDegrees = Math.Atan2(delta.Y, width) * 180.0 / Math.PI;
        viewModel.PointPairAngleToleranceDegrees = 0.01;
        if (PreviewC3DPointPairDimensions())
        {
            viewModel.ViewerStatus = "Smoke measure: C3D point pair width, distance, and angle";
        }
        else
        {
            smokeExitCode = 1;
        }
    }

    private void ApplySmokeLazTwoPointMeasurement(string heightUnit = "source-z-units")
    {
        if (lazPointCloud is null)
        {
            ApplySmokeLazPoints(null);
        }

        if (lazPointCloud is null || lazPointCloud.SampledPoints.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: LAZ/LAS point cloud missing";
            return;
        }

        viewModel.UseLazPointSmokeScene();
        viewModel.SelectedSelectionMode = TwoPointSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;

        var first = lazPointCloud.SampledPoints.MinBy(point => MapLazPosition(point.Position).X);
        var second = lazPointCloud.SampledPoints.MaxBy(point => MapLazPosition(point.Position).X);
        SetLazTwoPointMeasurement(first, second, heightUnit);

        var firstPosition = MapLazPosition(first.Position);
        var secondPosition = MapLazPosition(second.Position);
        var midpoint = (firstPosition + secondPosition) * 0.5f;
        viewModel.CameraTargetX = midpoint.X;
        viewModel.CameraTargetY = midpoint.Y;
        viewModel.CameraTargetZ = midpoint.Z;
        viewModel.UpdateCameraStatus();
        viewModel.SelectedEntity = "LAZ/LAS Two Point Measurement";
        viewModel.PickCoordinate = FormatLazPoint(second);
        viewModel.ViewerStatus = "Smoke measure: LAZ/LAS two-point distance and height delta";
    }

    private void ApplySmokeImportedMeshTwoPointMeasurement()
    {
        if (importedMesh is null)
        {
            ApplySmokeGlb(null);
        }

        if (importedMesh is null || importedMesh.Positions.Length < 2)
        {
            viewModel.SelectedEntity = $"{viewModel.ImportedMeshFormat} Two Point Measurement";
            viewModel.PickCoordinate = "(none)";
            SetSmokeFailure($"Smoke measure failed: {viewModel.ImportedMeshFormat} mesh missing");
            return;
        }

        viewModel.UseGlbSmokeScene();
        viewModel.SelectedSelectionMode = TwoPointSelectionMode;
        viewModel.MeasurementVisible = true;
        viewModel.SelectionOverlayVisible = true;

        var (first, second) = FindImportedMeshSmokeMeasurementPair();

        SetImportedMeshTwoPointMeasurement(first, second);
        viewModel.SelectedEntity = $"{viewModel.ImportedMeshFormat} Two Point Measurement";
        viewModel.PickCoordinate = FormatImportedMeshPoint(second);
        viewModel.ViewerStatus = $"Smoke measure: {viewModel.ImportedMeshFormat} two-point distance";
    }

    private void ApplySmokeRoiStepMeasurement()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        roiStepInteractiveSelection = false;
        roiStepLeftAnchor = null;
        roiStepRightAnchor = null;
        ClearRecipeRoiStep();
        roiStepNextPickSetsRight = false;

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Smoke measure: ROI step-height comparison";
        }
    }

    private void ApplySmokeInteractiveRoiStepMeasurement()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        roiStepInteractiveSelection = true;
        roiStepNextPickSetsRight = false;
        ClearRecipeRoiStep();

        var bounds = GetTransformedC3DBounds();
        var centerZ = (bounds.MinZ + bounds.MaxZ) * 0.5f;
        roiStepLeftAnchor = new Vector3(bounds.MinX + (bounds.MaxX - bounds.MinX) * 0.30f, 0.0f, centerZ);
        roiStepRightAnchor = new Vector3(bounds.MinX + (bounds.MaxX - bounds.MinX) * 0.70f, 0.0f, centerZ);

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Smoke measure: interactive ROI step-height comparison";
        }
    }

    private void ApplySmokePlaneReferenceMeasurement()
    {
        viewModel.UseC3DSmokeScene();
        if (FitC3DReferencePlane())
        {
            viewModel.ViewerStatus = "Smoke measure: distance to fitted C3D plane";
        }
    }

    private void ApplySmokePlaneFlatness()
    {
        viewModel.UseC3DSmokeScene();
        if (PreviewC3DPlaneFlatness())
        {
            viewModel.ViewerStatus = "Smoke measure: reference ROI plane flatness";
        }
    }

    private void ApplySmokeGapFlush()
    {
        if (!viewModel.C3DSampleVisible)
        {
            ApplySmokeC3D();
        }

        if (!PreviewC3DGapFlush())
        {
            SetSmokeFailure("Smoke Gap / Flush preview failed");
        }
    }

    private void ApplySmokeVolume()
    {
        if (!viewModel.C3DSampleVisible) ApplySmokeC3D();
        if (!PreviewC3DVolume()) SetSmokeFailure("Smoke Volume preview failed");
    }

    private void ApplySmokeCrossSection()
    {
        if (!viewModel.C3DSampleVisible) ApplySmokeC3D();
        if (!PreviewC3DCrossSection()) SetSmokeFailure("Smoke Cross-section Dimensions preview failed");
    }

    public bool FitC3DReferencePlane()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Plane fit requires a visible C3D height grid";
            return false;
        }

        C3DHeightGrid fitSample;
        try
        {
            fitSample = C3DHeightGrid.Load(c3dSample.SourcePath, PlaneFitMaxSampledPoints);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Plane fit sample load failed: {ex.Message}";
            return false;
        }

        var transformed = fitSample.Points
            .Select(point => (Point: point, Position: TransformC3DPosition(point.Position)))
            .ToArray();
        HeightFieldPlaneFitResult result;
        try
        {
            result = HeightFieldPlaneFit.Fit(
                transformed
                    .Select(item => new HeightFieldPlaneSample(item.Position, item.Point.RawValue))
                    .ToArray());
        }
        catch (ArgumentException ex)
        {
            viewModel.ViewerStatus = $"Plane fit failed: {ex.Message}";
            return false;
        }

        twoPointFirst = null;
        twoPointSecond = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedImportedMeshPoint = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        selectedLazPoint = null;
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;
        viewModel.ClearTwoPointMeasurement();
        viewModel.ClearRoiStepMeasurement();
        viewModel.SelectedSelectionMode = "Plane Distance";
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;

        var bounds = (
            MinX: transformed.Min(item => item.Position.X),
            MaxX: transformed.Max(item => item.Position.X),
            MinZ: transformed.Min(item => item.Position.Z),
            MaxZ: transformed.Max(item => item.Position.Z));
        planeReferenceMeasurement = (
            CreatePlaneCorner(result, bounds.MinX, bounds.MinZ),
            CreatePlaneCorner(result, bounds.MaxX, bounds.MinZ),
            CreatePlaneCorner(result, bounds.MaxX, bounds.MaxZ),
            CreatePlaneCorner(result, bounds.MinX, bounds.MaxZ),
            result.Target,
            result.TargetProjection);
        var target = transformed.MinBy(item => Vector3.DistanceSquared(item.Position, result.Target));
        viewModel.SetPlaneReferenceMeasurement(result, "C3D least-squares height field / fixed sample");
        viewModel.SelectedEntity = "Plane Distance Measurement";
        viewModel.PickCoordinate = FormatC3DPoint(target.Point);
        viewModel.ViewerStatus = "Fitted C3D plane and maximum residual measured";
        RenderNow();
        return true;
    }

    public bool PreviewC3DPlaneFlatness()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Plane flatness requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreatePlaneFlatnessRecipeStep();
        C3DHeightGrid measurementSample;
        try
        {
            measurementSample = C3DHeightGrid.Load(c3dSample.SourcePath, step.MaxSampledPoints);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Plane flatness sample load failed: {ex.Message}";
            return false;
        }

        var measurementSamples = measurementSample.Points
            .Select(point => new HeightFieldPlaneSample(TransformC3DPosition(point.Position), point.RawValue))
            .ToArray();
        var referenceSamples = measurementSamples
            .Where(sample => Contains(step.ReferenceRegion, sample.Position))
            .ToArray();
        var evaluation = PlaneFlatnessRule.Evaluate(new PlaneFlatnessRuleInput(
            step.SourceEntityId,
            referenceSamples,
            measurementSamples,
            step.Tolerance,
            step.Unit));

        twoPointFirst = null;
        twoPointSecond = null;
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;
        viewModel.ClearTwoPointMeasurement();
        viewModel.ClearPlaneReferenceMeasurement();
        viewModel.ClearRoiStepMeasurement();
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        planeFlatnessEvaluation = evaluation;

        if (evaluation.ReferencePlane is { } plane)
        {
            var region = step.ReferenceRegion;
            planeReferenceMeasurement = (
                CreatePlaneCorner(plane, (float)(region.CenterX - region.HalfWidth), (float)(region.CenterZ - region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX + region.HalfWidth), (float)(region.CenterZ - region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX + region.HalfWidth), (float)(region.CenterZ + region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX - region.HalfWidth), (float)(region.CenterZ + region.HalfDepth)),
                evaluation.MaximumPoint,
                evaluation.MaximumProjection);
            viewModel.PickCoordinate = string.Create(
                CultureInfo.InvariantCulture,
                $"Maximum deviation point {CameraMath.FormatPoint(evaluation.MaximumPoint)}");
        }
        else
        {
            planeReferenceMeasurement = null;
            viewModel.PickCoordinate = "(invalid reference ROI)";
        }

        viewModel.SetPlaneFlatnessPreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    public bool PreviewC3DPointPairDimensions()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Point pair dimensions require a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreatePointPairDimensionsRecipeStep();
        if (step is null)
        {
            viewModel.ViewerStatus = "Point pair dimensions require two selected C3D source cells";
            return false;
        }

        HeightGridPoint first;
        HeightGridPoint second;
        try
        {
            first = c3dSample.ReadPoint(step.First.Row, step.First.Column);
            second = c3dSample.ReadPoint(step.Second.Row, step.Second.Column);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException)
        {
            viewModel.ViewerStatus = $"Point pair dimensions failed: {ex.Message}";
            return false;
        }

        SetTwoPointMeasurement(first, second, updatePointPairReferences: false);
        var evaluation = PointPairDimensionsRule.Evaluate(new PointPairDimensionsInput(
            step.SourceEntityId,
            TransformC3DPosition(first.Position),
            TransformC3DPosition(second.Position),
            first.RawValue,
            second.RawValue,
            step.Acceptance,
            step.Unit,
            viewModel.RecipeSourceUnit));
        viewModel.SetPointPairDimensionsPreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    public bool PreviewC3DGapFlush()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Gap / Flush requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreateGapFlushRecipeStep();
        C3DHeightGrid measurementSample;
        try
        {
            measurementSample = C3DHeightGrid.Load(c3dSample.SourcePath, step.MaxSampledPoints);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Gap / Flush sample load failed: {ex.Message}";
            return false;
        }

        TryCalculateGapFlushStats(measurementSample.Points, step.LeftRegion, out var left);
        TryCalculateGapFlushStats(measurementSample.Points, step.RightRegion, out var right);
        var evaluation = GapFlushRule.Evaluate(new GapFlushInput(
            step.SourceEntityId,
            step.LeftRegion,
            step.RightRegion,
            left,
            right,
            step.Acceptance,
            step.GapUnit,
            step.FlushUnit));

        roiStepLeftRecipeRegion = step.LeftRegion;
        roiStepRightRecipeRegion = step.RightRegion;
        roiStepInteractiveSelection = false;
        roiStepNextPickSetsRight = false;
        roiStepLeftBounds = (
            (float)(step.LeftRegion.CenterX - step.LeftRegion.HalfWidth),
            (float)(step.LeftRegion.CenterX + step.LeftRegion.HalfWidth),
            (float)(step.LeftRegion.CenterZ - step.LeftRegion.HalfDepth),
            (float)(step.LeftRegion.CenterZ + step.LeftRegion.HalfDepth),
            (float)left.ModelYMean);
        roiStepRightBounds = (
            (float)(step.RightRegion.CenterX - step.RightRegion.HalfWidth),
            (float)(step.RightRegion.CenterX + step.RightRegion.HalfWidth),
            (float)(step.RightRegion.CenterZ - step.RightRegion.HalfDepth),
            (float)(step.RightRegion.CenterZ + step.RightRegion.HalfDepth),
            (float)right.ModelYMean);
        roiStepLeftCenter = new Vector3((float)step.LeftRegion.CenterX, (float)left.ModelYMean, (float)step.LeftRegion.CenterZ);
        roiStepRightCenter = new Vector3((float)step.RightRegion.CenterX, (float)right.ModelYMean, (float)step.RightRegion.CenterZ);
        viewModel.SetRoiStepMeasurement(
            left.PointCount,
            left.RawMean,
            left.ModelYMean,
            right.PointCount,
            right.RawMean,
            right.ModelYMean,
            "GapFlush");
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        viewModel.SetGapFlushPreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    private bool TryCalculateGapFlushStats(
        IReadOnlyList<HeightGridPoint> points,
        HeightDeviationRecipeRoiRegion region,
        out GapFlushRegionStats stats)
    {
        var count = 0;
        var rawSum = 0.0;
        var modelYSum = 0.0;
        foreach (var point in points)
        {
            var position = TransformC3DPosition(point.Position);
            if (!Contains(region, position))
            {
                continue;
            }

            count++;
            rawSum += point.RawValue;
            modelYSum += position.Y;
        }

        if (count == 0)
        {
            stats = new GapFlushRegionStats(0, double.NaN, double.NaN);
            return false;
        }

        stats = new GapFlushRegionStats(count, rawSum / count, modelYSum / count);
        return true;
    }

    public bool PreviewC3DVolume()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Volume requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreateVolumeRecipeStep();
        C3DHeightGrid measurementGrid;
        try { measurementGrid = C3DHeightGrid.Load(c3dSample.SourcePath, step.MaxSampledPoints); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Volume sample load failed: {ex.Message}";
            return false;
        }

        var samples = measurementGrid.Points
            .Select(point => new HeightFieldPlaneSample(TransformC3DPosition(point.Position), point.RawValue))
            .ToArray();
        var reference = samples.Where(sample => Contains(step.ReferenceRegion, sample.Position)).ToArray();
        var measured = samples.Where(sample => Contains(step.MeasurementRegion, sample.Position)).ToArray();
        var spacing = measurementGrid.HorizontalScale * measurementGrid.PointStride * viewModel.C3DModelTransform.Scale;
        var evaluation = VolumeRule.Evaluate(new VolumeRuleInput(
            step.SourceEntityId, reference, measured, spacing * spacing,
            step.ExpectedNetVolume, step.Tolerance, step.Unit));

        if (evaluation.ReferencePlane is { } plane)
        {
            var region = step.ReferenceRegion;
            planeReferenceMeasurement = (
                CreatePlaneCorner(plane, (float)(region.CenterX - region.HalfWidth), (float)(region.CenterZ - region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX + region.HalfWidth), (float)(region.CenterZ - region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX + region.HalfWidth), (float)(region.CenterZ + region.HalfDepth)),
                CreatePlaneCorner(plane, (float)(region.CenterX - region.HalfWidth), (float)(region.CenterZ + region.HalfDepth)),
                plane.Target,
                plane.TargetProjection);
        }

        var meanY = measured.Length == 0 ? 0.0 : measured.Average(sample => sample.Position.Y);
        roiStepLeftBounds = (
            (float)(step.MeasurementRegion.CenterX - step.MeasurementRegion.HalfWidth),
            (float)(step.MeasurementRegion.CenterX + step.MeasurementRegion.HalfWidth),
            (float)(step.MeasurementRegion.CenterZ - step.MeasurementRegion.HalfDepth),
            (float)(step.MeasurementRegion.CenterZ + step.MeasurementRegion.HalfDepth),
            (float)meanY);
        roiStepRightBounds = null;
        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        viewModel.SetVolumePreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    public bool PreviewC3DCrossSection()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Cross-section Dimensions requires a visible C3D height grid";
            return false;
        }

        var step = viewModel.CreateCrossSectionRecipeStep();
        HeightGridPoint[] sourcePoints;
        try
        {
            sourcePoints = c3dSample.ReadRowRange(step.Row, step.StartColumn, step.EndColumn);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            viewModel.ViewerStatus = $"Cross-section source read failed: {ex.Message}";
            return false;
        }

        var samples = sourcePoints
            .Select(point => new CrossSectionSample(point.Column, TransformC3DPosition(point.Position), point.RawValue))
            .ToArray();
        var evaluation = CrossSectionDimensionsRule.Evaluate(new CrossSectionDimensionsInput(
            step.SourceEntityId,
            step.Row,
            step.StartColumn,
            step.EndColumn,
            samples,
            step.ExpectedWidth,
            step.WidthTolerance,
            step.ExpectedHeightRange,
            step.HeightTolerance,
            step.WidthUnit,
            step.HeightUnit));

        if (sourcePoints.Length >= 2)
        {
            var minimum = sourcePoints.Min(point => point.RawValue);
            var maximum = sourcePoints.Max(point => point.RawValue);
            var mean = sourcePoints.Average(point => point.RawValue);
            viewModel.SetSectionProfile(
                viewModel.RecipeSourceName,
                step.Row,
                sourcePoints.Length,
                minimum,
                maximum,
                mean,
                BuildSectionProfilePath(sourcePoints, minimum, maximum));
        }

        viewModel.SelectionOverlayVisible = true;
        viewModel.MeasurementVisible = true;
        viewModel.SetCrossSectionPreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    private static bool Contains(HeightDeviationRecipeRoiRegion region, Vector3 point) =>
        point.X >= region.CenterX - region.HalfWidth
        && point.X <= region.CenterX + region.HalfWidth
        && point.Z >= region.CenterZ - region.HalfDepth
        && point.Z <= region.CenterZ + region.HalfDepth;

    private static Vector3 CreatePlaneCorner(HeightFieldPlaneFitResult result, float x, float z) =>
        new(x, (float)result.EvaluateY(x, z), z);

    private void ConfigureProjection(OpenGL gl)
    {
        var width = Math.Max(1, (int)Viewport.ActualWidth);
        var height = Math.Max(1, (int)Viewport.ActualHeight);
        gl.Viewport(0, 0, width, height);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        var farPlane = Math.Max(100.0, viewModel.CameraDistance + 300.0);
        gl.Perspective(FieldOfViewDegrees, (double)width / height, 0.1, farPlane);
    }

    private void ConfigureCamera(OpenGL gl)
    {
        var target = GetCameraTarget();
        var eye = GetCameraPosition();
        gl.MatrixMode(OpenGL.GL_MODELVIEW);
        gl.LoadIdentity();
        gl.LookAt(eye.X, eye.Y, eye.Z, target.X, target.Y, target.Z, 0.0, 1.0, 0.0);
    }

    private Vector3 GetCameraPosition()
    {
        return CameraMath.OrbitCameraPosition(
            GetCameraTarget(),
            viewModel.YawDegrees,
            viewModel.PitchDegrees,
            viewModel.CameraDistance);
    }

    private Vector3 GetCameraTarget()
    {
        return CameraMath.CameraTarget(viewModel.CameraTargetX, viewModel.CameraTargetY, viewModel.CameraTargetZ);
    }

    private void DrawGrid(OpenGL gl)
    {
        gl.LineWidth(1.0f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Color(0.25, 0.29, 0.36);

        for (var i = -5; i <= 5; i++)
        {
            gl.Vertex(i, -1.02, -5.0);
            gl.Vertex(i, -1.02, 5.0);
            gl.Vertex(-5.0, -1.02, i);
            gl.Vertex(5.0, -1.02, i);
        }

        gl.End();
    }

    private void DrawAxes(OpenGL gl)
    {
        gl.LineWidth(2.0f);
        gl.Begin(OpenGL.GL_LINES);

        gl.Color(0.95, 0.25, 0.25);
        gl.Vertex(0.0, 0.0, 0.0);
        gl.Vertex(2.3, 0.0, 0.0);

        gl.Color(0.25, 0.85, 0.35);
        gl.Vertex(0.0, 0.0, 0.0);
        gl.Vertex(0.0, 2.3, 0.0);

        gl.Color(0.35, 0.55, 1.0);
        gl.Vertex(0.0, 0.0, 0.0);
        gl.Vertex(0.0, 0.0, 2.3);

        gl.End();
    }

    private void DrawTwoPointMeasurement(OpenGL gl)
    {
        Vector3 firstPosition;
        Vector3 secondPosition;
        if (twoPointFirst is { } first && twoPointSecond is { } second)
        {
            firstPosition = TransformC3DPosition(first.Position);
            secondPosition = TransformC3DPosition(second.Position);
        }
        else if (lazTwoPointFirst is { } lazFirst && lazTwoPointSecond is { } lazSecond)
        {
            firstPosition = MapLazPosition(lazFirst.Position);
            secondPosition = MapLazPosition(lazSecond.Position);
        }
        else if (importedMeshTwoPointFirst is { } importedMeshFirst && importedMeshTwoPointSecond is { } importedMeshSecond)
        {
            firstPosition = importedMeshFirst;
            secondPosition = importedMeshSecond;
        }
        else
        {
            return;
        }

        gl.LineWidth(3.0f);
        gl.Begin(OpenGL.GL_LINES);

        gl.Color(1.0, 0.72, 0.10);
        gl.Vertex(firstPosition.X, firstPosition.Y, firstPosition.Z);
        gl.Vertex(secondPosition.X, secondPosition.Y, secondPosition.Z);

        gl.Color(0.20, 0.95, 0.45);
        gl.Vertex(secondPosition.X, firstPosition.Y, secondPosition.Z);
        gl.Vertex(secondPosition.X, secondPosition.Y, secondPosition.Z);

        gl.End();

        gl.PointSize(8.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 1.0, 1.0);
        gl.Vertex(firstPosition.X, firstPosition.Y, firstPosition.Z);
        gl.Color(1.0, 0.72, 0.10);
        gl.Vertex(secondPosition.X, secondPosition.Y, secondPosition.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawPlaneReferenceMeasurement(OpenGL gl)
    {
        if (planeReferenceMeasurement is not { } measurement
            || (!viewModel.PlaneReferenceMeasurementVisible && !viewModel.PlaneFlatnessVisible))
        {
            return;
        }

        gl.LineWidth(2.0f);
        gl.Color(0.68, 0.54, 1.0);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(measurement.A.X, measurement.A.Y, measurement.A.Z);
        gl.Vertex(measurement.B.X, measurement.B.Y, measurement.B.Z);
        gl.Vertex(measurement.C.X, measurement.C.Y, measurement.C.Z);
        gl.Vertex(measurement.D.X, measurement.D.Y, measurement.D.Z);
        gl.End();

        gl.LineWidth(3.0f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Color(1.0, 0.90, 0.20);
        gl.Vertex(measurement.Projection.X, measurement.Projection.Y, measurement.Projection.Z);
        gl.Vertex(measurement.Target.X, measurement.Target.Y, measurement.Target.Z);
        gl.End();

        gl.PointSize(8.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.90, 0.20);
        gl.Vertex(measurement.Target.X, measurement.Target.Y, measurement.Target.Z);
        gl.Color(0.68, 0.54, 1.0);
        gl.Vertex(measurement.Projection.X, measurement.Projection.Y, measurement.Projection.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawPlaneFlatnessExtrema(OpenGL gl)
    {
        if (!viewModel.PlaneFlatnessVisible
            || planeFlatnessEvaluation is not { ReferencePlane: not null } evaluation)
        {
            return;
        }
        gl.LineWidth(3.0f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Color(0.20, 0.80, 1.0);
        gl.Vertex(evaluation.MinimumProjection.X, evaluation.MinimumProjection.Y, evaluation.MinimumProjection.Z);
        gl.Vertex(evaluation.MinimumPoint.X, evaluation.MinimumPoint.Y, evaluation.MinimumPoint.Z);
        gl.Color(1.0, 0.32, 0.20);
        gl.Vertex(evaluation.MaximumProjection.X, evaluation.MaximumProjection.Y, evaluation.MaximumProjection.Z);
        gl.Vertex(evaluation.MaximumPoint.X, evaluation.MaximumPoint.Y, evaluation.MaximumPoint.Z);
        gl.End();

        gl.PointSize(9.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(0.20, 0.80, 1.0);
        gl.Vertex(evaluation.MinimumPoint.X, evaluation.MinimumPoint.Y, evaluation.MinimumPoint.Z);
        gl.Color(1.0, 0.32, 0.20);
        gl.Vertex(evaluation.MaximumPoint.X, evaluation.MaximumPoint.Y, evaluation.MaximumPoint.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawRoiStepMeasurement(OpenGL gl)
    {
        if (roiStepLeftBounds is not { } left)
        {
            return;
        }

        DrawRoiBounds(gl, left, 0.20, 0.95, 0.45);

        if (roiStepRightBounds is not { } right
            || roiStepLeftCenter is not { } leftCenter
            || roiStepRightCenter is not { } rightCenter)
        {
            return;
        }

        DrawRoiBounds(gl, right, 1.0, 0.72, 0.10);

        gl.LineWidth(3.0f);
        gl.Begin(OpenGL.GL_LINES);

        gl.Color(1.0, 0.85, 0.20);
        gl.Vertex(leftCenter.X, leftCenter.Y, leftCenter.Z);
        gl.Vertex(rightCenter.X, rightCenter.Y, rightCenter.Z);

        gl.Color(0.20, 0.95, 0.45);
        gl.Vertex(rightCenter.X, leftCenter.Y, rightCenter.Z);
        gl.Vertex(rightCenter.X, rightCenter.Y, rightCenter.Z);

        gl.End();

        gl.PointSize(8.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(0.20, 0.95, 0.45);
        gl.Vertex(leftCenter.X, leftCenter.Y, leftCenter.Z);
        gl.Color(1.0, 0.72, 0.10);
        gl.Vertex(rightCenter.X, rightCenter.Y, rightCenter.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private static void DrawRoiBounds(OpenGL gl, (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) bounds, double red, double green, double blue)
    {
        gl.LineWidth(2.5f);
        gl.Color(red, green, blue);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(bounds.MinX, bounds.MeanY, bounds.MinZ);
        gl.Vertex(bounds.MaxX, bounds.MeanY, bounds.MinZ);
        gl.Vertex(bounds.MaxX, bounds.MeanY, bounds.MaxZ);
        gl.Vertex(bounds.MinX, bounds.MeanY, bounds.MaxZ);
        gl.End();
    }

    private void DrawCube(OpenGL gl)
    {
        gl.Begin(OpenGL.GL_QUADS);

        gl.Color(0.20, 0.62, 0.86);
        Quad(gl, (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1));

        gl.Color(0.14, 0.48, 0.75);
        Quad(gl, (1, -1, -1), (-1, -1, -1), (-1, 1, -1), (1, 1, -1));

        gl.Color(0.95, 0.72, 0.32);
        Quad(gl, (-1, 1, 1), (1, 1, 1), (1, 1, -1), (-1, 1, -1));

        gl.Color(0.78, 0.46, 0.25);
        Quad(gl, (-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1));

        gl.Color(0.45, 0.72, 0.42);
        Quad(gl, (1, -1, 1), (1, -1, -1), (1, 1, -1), (1, 1, 1));

        gl.Color(0.36, 0.60, 0.36);
        Quad(gl, (-1, -1, -1), (-1, -1, 1), (-1, 1, 1), (-1, 1, -1));

        gl.End();

        DrawCubeWire(gl);
    }

    private void DrawCubeWire(OpenGL gl)
    {
        gl.LineWidth(1.5f);
        gl.Color(1.0, 1.0, 1.0);
        gl.Begin(OpenGL.GL_LINES);

        Edge(gl, (-1, -1, -1), (1, -1, -1));
        Edge(gl, (1, -1, -1), (1, -1, 1));
        Edge(gl, (1, -1, 1), (-1, -1, 1));
        Edge(gl, (-1, -1, 1), (-1, -1, -1));
        Edge(gl, (-1, 1, -1), (1, 1, -1));
        Edge(gl, (1, 1, -1), (1, 1, 1));
        Edge(gl, (1, 1, 1), (-1, 1, 1));
        Edge(gl, (-1, 1, 1), (-1, 1, -1));
        Edge(gl, (-1, -1, -1), (-1, 1, -1));
        Edge(gl, (1, -1, -1), (1, 1, -1));
        Edge(gl, (1, -1, 1), (1, 1, 1));
        Edge(gl, (-1, -1, 1), (-1, 1, 1));

        gl.End();
    }

    private void DrawPointCloud(OpenGL gl, IReadOnlyList<HeightGridPoint> points)
    {
        gl.PointSize((float)viewModel.PointSize);
        gl.Begin(OpenGL.GL_POINTS);

        foreach (var point in points)
        {
            ApplyPointColor(gl, point);
            gl.Vertex(point.Position.X, point.Position.Y, point.Position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
        DrawPointCloudFrame(gl);
    }

    private void DrawC3DHeightGrid(OpenGL gl)
    {
        var renderProxy = GetC3DRenderProxy();
        var positions = GetC3DRenderPositions(renderProxy);
        var geometryStyle = viewModel.Display.EffectiveSettings.GeometryStyle;
        gl.Disable(GlTexture2D);

        if (UsesDynamicC3DColor())
        {
            ReleaseC3DDisplayList(gl);
            DrawC3DGeometry(gl, renderProxy, positions, geometryStyle);
            DrawC3DFrame(gl);
            return;
        }

        var displayListKey = new C3DDisplayListKey(
            c3dSample!,
            c3dRenderPositionsTransform,
            geometryStyle,
            viewModel.Display.EffectiveSettings.ColorMap,
            viewModel.PointSize);
        if (c3dDisplayListId == 0 || c3dDisplayListKey != displayListKey)
        {
            ReleaseC3DDisplayList(gl);
            c3dDisplayListId = gl.GenLists(1);
            if (c3dDisplayListId != 0)
            {
                gl.NewList(c3dDisplayListId, OpenGL.GL_COMPILE);
                DrawC3DGeometry(gl, renderProxy, positions, geometryStyle);
                gl.EndList();
                c3dDisplayListKey = displayListKey;
            }
        }

        if (c3dDisplayListId != 0)
        {
            gl.CallList(c3dDisplayListId);
        }
        else
        {
            DrawC3DGeometry(gl, renderProxy, positions, geometryStyle);
        }

        DrawC3DFrame(gl);
    }

    private void DrawC3DGeometry(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions,
        ViewerGeometryStyle geometryStyle)
    {
        switch (geometryStyle)
        {
            case ViewerGeometryStyle.Wireframe when renderProxy.HasSurface:
                DrawC3DEdges(
                    gl,
                    renderProxy,
                    positions,
                    renderProxy.GridEdgeIndices,
                    usePointColors: true);
                break;
            case ViewerGeometryStyle.Surface when renderProxy.HasSurface:
                DrawC3DSurface(gl, renderProxy, positions, offsetForEdges: false);
                break;
            case ViewerGeometryStyle.SurfaceWithEdges when renderProxy.HasSurface:
                DrawC3DSurface(gl, renderProxy, positions, offsetForEdges: true);
                DrawC3DEdges(
                    gl,
                    renderProxy,
                    positions,
                    renderProxy.SurfaceEdgeIndices,
                    usePointColors: false);
                break;
            default:
                DrawC3DPoints(gl, renderProxy, positions);
                break;
        }
    }

    private bool UsesDynamicC3DColor() =>
        viewModel.SelectedColorMode == "Deviation"
        && viewModel.PlaneFlatnessVisible
        && planeFlatnessEvaluation is { ReferencePlane: not null };

    private void ReleaseC3DDisplayList(OpenGL gl)
    {
        if (c3dDisplayListId != 0)
        {
            gl.DeleteLists(c3dDisplayListId, 1);
        }

        c3dDisplayListId = 0;
        c3dDisplayListKey = null;
    }

    private void DrawC3DPoints(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions)
    {
        gl.PointSize((float)viewModel.PointSize);
        gl.Begin(OpenGL.GL_POINTS);
        for (var index = 0; index < renderProxy.Points.Length; index++)
        {
            var point = renderProxy.Points[index];
            var position = positions[index];
            ApplyC3DColor(gl, point, position);
            gl.Vertex(position.X, position.Y, position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawC3DSurface(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions,
        bool offsetForEdges)
    {
        if (offsetForEdges)
        {
            gl.Enable(OpenGL.GL_POLYGON_OFFSET_FILL);
            gl.PolygonOffset(1.0f, 1.0f);
        }

        gl.Begin(OpenGL.GL_TRIANGLES);
        foreach (var index in renderProxy.TriangleIndices)
        {
            var point = renderProxy.Points[index];
            var position = positions[index];
            ApplyC3DColor(gl, point, position);
            gl.Vertex(position.X, position.Y, position.Z);
        }

        gl.End();
        if (offsetForEdges)
        {
            gl.Disable(OpenGL.GL_POLYGON_OFFSET_FILL);
        }
    }

    private void DrawC3DEdges(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> edgeIndices,
        bool usePointColors)
    {
        gl.LineWidth(1.0f);
        if (!usePointColors)
        {
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.Color(0.02, 0.05, 0.08, 0.32);
        }

        gl.Begin(OpenGL.GL_LINES);
        foreach (var index in edgeIndices)
        {
            var position = positions[index];
            if (usePointColors)
            {
                ApplyC3DColor(gl, renderProxy.Points[index], position);
            }

            gl.Vertex(position.X, position.Y, position.Z);
        }

        gl.End();
        if (!usePointColors)
        {
            gl.Disable(OpenGL.GL_BLEND);
        }

        gl.LineWidth(1.0f);
    }

    private void ApplyC3DColor(OpenGL gl, HeightGridPoint point, Vector3 position)
    {
        if (viewModel.SelectedColorMode == "Deviation"
            && viewModel.PlaneFlatnessVisible
            && planeFlatnessEvaluation is { ReferencePlane: not null } flatness)
        {
            ApplyPlaneFlatnessColor(gl, position, flatness);
            return;
        }

        ApplyPointColor(gl, point);
    }

    private C3DHeightGridRenderProxy GetC3DRenderProxy()
    {
        var sample = c3dSample
            ?? throw new InvalidOperationException("C3D display proxy requires a loaded sample.");
        if (!ReferenceEquals(c3dRenderProxySource, sample) || c3dRenderProxy is null)
        {
            c3dRenderProxySource = sample;
            c3dRenderProxy = C3DHeightGridRenderProxy.Create(sample);
            c3dRenderPositions = null;
        }

        return c3dRenderProxy;
    }

    private Vector3[] GetC3DRenderPositions(C3DHeightGridRenderProxy renderProxy)
    {
        var transform = viewModel.C3DModelTransform;
        if (c3dRenderPositions is null || c3dRenderPositionsTransform != transform)
        {
            c3dRenderPositions = new Vector3[renderProxy.Points.Length];
            for (var index = 0; index < renderProxy.Points.Length; index++)
            {
                c3dRenderPositions[index] = ApplyModelTransform(
                    renderProxy.Points[index].Position,
                    transform);
            }

            c3dRenderPositionsTransform = transform;
        }

        return c3dRenderPositions;
    }

    private void InvalidateC3DRenderProxy()
    {
        c3dRenderProxySource = null;
        c3dRenderProxy = null;
        c3dRenderPositions = null;
        c3dDisplayListKey = null;
    }

    private void DrawNominalActualDeviation(OpenGL gl)
    {
        var result = viewModel.NominalActual.PreviewResult!;
        gl.Disable(GlTexture2D);
        gl.PointSize((float)Math.Max(2.0, viewModel.PointSize));
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var sample in result.DisplaySamples)
        {
            var color = GetSignedDeviationColor(
                sample.SignedDeviation,
                result.Input.LowerTolerance,
                result.Input.UpperTolerance);
            gl.Color(color.Red, color.Green, color.Blue);
            gl.Vertex(sample.Position.X, sample.Position.Y, sample.Position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawNominalActualSelectedDeviation(OpenGL gl)
    {
        if (viewModel.NominalActual.SelectedDeviation is not { } sample)
        {
            return;
        }

        gl.Disable(OpenGL.GL_DEPTH_TEST);
        gl.LineWidth(2.0f);
        gl.Color(1.0, 0.75, 0.10);
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(sample.Position.X, sample.Position.Y, sample.Position.Z);
        gl.Vertex(sample.ClosestNominalPoint.X, sample.ClosestNominalPoint.Y, sample.ClosestNominalPoint.Z);
        gl.End();

        gl.PointSize(10.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.85, 0.10);
        gl.Vertex(sample.Position.X, sample.Position.Y, sample.Position.Z);
        gl.Color(0.10, 0.90, 0.90);
        gl.Vertex(sample.ClosestNominalPoint.X, sample.ClosestNominalPoint.Y, sample.ClosestNominalPoint.Z);
        gl.End();
        gl.PointSize(1.0f);
        gl.LineWidth(1.0f);
        gl.Enable(OpenGL.GL_DEPTH_TEST);
    }

    private static (double Red, double Green, double Blue) GetSignedDeviationColor(
        double value,
        double lowerTolerance,
        double upperTolerance)
    {
        const double blueRed = 0.145;
        const double blueGreen = 0.388;
        const double blueBlue = 0.922;
        const double redRed = 0.937;
        const double redGreen = 0.267;
        const double redBlue = 0.267;

        if (value <= lowerTolerance)
        {
            return (blueRed, blueGreen, blueBlue);
        }

        if (value >= upperTolerance)
        {
            return (redRed, redGreen, redBlue);
        }

        if (value < 0)
        {
            var ratio = Math.Clamp(value / lowerTolerance, 0.0, 1.0);
            return (
                1.0 + (blueRed - 1.0) * ratio,
                1.0 + (blueGreen - 1.0) * ratio,
                1.0 + (blueBlue - 1.0) * ratio);
        }

        var positiveRatio = Math.Clamp(value / upperTolerance, 0.0, 1.0);
        return (
            1.0 + (redRed - 1.0) * positiveRatio,
            1.0 + (redGreen - 1.0) * positiveRatio,
            1.0 + (redBlue - 1.0) * positiveRatio);
    }

    private void DrawImportedMesh(OpenGL gl)
    {
        var mesh = importedMesh!;
        var triangleStride = GetImportedMeshRenderTriangleStride();
        var useTexture = EnsureImportedMeshTexture(gl);
        if (useTexture)
        {
            gl.Enable(GlTexture2D);
            gl.BindTexture(GlTexture2D, importedMeshTextureId);
            gl.Color(1.0, 1.0, 1.0);
        }

        gl.Begin(OpenGL.GL_TRIANGLES);
        for (var triangle = 0; triangle < mesh.TriangleCount; triangle += triangleStride)
        {
            var offset = triangle * 3;
            DrawImportedMeshVertex(gl, mesh, mesh.Indices[offset], useTexture);
            DrawImportedMeshVertex(gl, mesh, mesh.Indices[offset + 1], useTexture);
            DrawImportedMeshVertex(gl, mesh, mesh.Indices[offset + 2], useTexture);
        }

        gl.End();
        if (useTexture)
        {
            gl.Disable(GlTexture2D);
        }

        gl.LineWidth(1.2f);
        gl.Color(1.0, 0.92, 0.78);
        gl.Begin(OpenGL.GL_LINES);
        for (var triangle = 0; triangle < mesh.TriangleCount; triangle += triangleStride)
        {
            var offset = triangle * 3;
            var a = mesh.Positions[mesh.Indices[offset]];
            var b = mesh.Positions[mesh.Indices[offset + 1]];
            var c = mesh.Positions[mesh.Indices[offset + 2]];
            gl.Vertex(a.X, a.Y, a.Z);
            gl.Vertex(b.X, b.Y, b.Z);
            gl.Vertex(b.X, b.Y, b.Z);
            gl.Vertex(c.X, c.Y, c.Z);
            gl.Vertex(c.X, c.Y, c.Z);
            gl.Vertex(a.X, a.Y, a.Z);
        }

        gl.End();
        DrawImportedMeshFrame(gl);
        DrawSelectedGlbPoint(gl);
    }

    private static void DrawImportedMeshVertex(OpenGL gl, ImportedMesh mesh, int index, bool useTexture)
    {
        if (useTexture)
        {
            var uv = mesh.TextureCoordinates[index];
            gl.Color(1.0, 1.0, 1.0);
            gl.TexCoord(uv.X, 1.0f - uv.Y);
        }
        else if (mesh.HasVertexColors)
        {
            var color = mesh.VertexColors[index];
            gl.Color(color.X, color.Y, color.Z);
        }
        else
        {
            gl.Color(0.88, 0.48, 0.22);
        }

        var position = mesh.Positions[index];
        gl.Vertex(position.X, position.Y, position.Z);
    }

    private int GetImportedMeshRenderTriangleStride()
    {
        if (importedMesh is null || importedMesh.TriangleCount <= viewModel.ImportedMeshMaxRenderedTriangles)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling((double)importedMesh.TriangleCount / viewModel.ImportedMeshMaxRenderedTriangles));
    }

    private int GetImportedMeshRenderedTriangleCount()
    {
        if (importedMesh is null)
        {
            return 0;
        }

        var stride = GetImportedMeshRenderTriangleStride();
        return (importedMesh.TriangleCount + stride - 1) / stride;
    }

    private void DrawPointCloudFrame(OpenGL gl)
    {
        gl.LineWidth(1.5f);
        gl.Color(0.78, 0.86, 0.98);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(1.0, -1.18, -2.25);
        gl.Vertex(5.4, -1.18, -2.25);
        gl.Vertex(5.4, -1.18, 2.25);
        gl.Vertex(1.0, -1.18, 2.25);
        gl.End();
    }

    private void DrawC3DFrame(OpenGL gl)
    {
        var x = c3dSample!.XHalfExtent;
        var z = c3dSample.ZHalfExtent;
        var a = TransformC3DPosition(new Vector3(-x, 0.0f, -z));
        var b = TransformC3DPosition(new Vector3(x, 0.0f, -z));
        var c = TransformC3DPosition(new Vector3(x, 0.0f, z));
        var d = TransformC3DPosition(new Vector3(-x, 0.0f, z));

        gl.LineWidth(1.5f);
        gl.Color(0.78, 0.86, 0.98);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(a.X, a.Y, a.Z);
        gl.Vertex(b.X, b.Y, b.Z);
        gl.Vertex(c.X, c.Y, c.Z);
        gl.Vertex(d.X, d.Y, d.Z);
        gl.End();
    }

    private void DrawImportedMeshFrame(OpenGL gl)
    {
        var mesh = importedMesh!;
        var min = mesh.Min;
        var max = mesh.Max;

        gl.LineWidth(1.5f);
        gl.Color(0.78, 0.86, 0.98);
        gl.Begin(OpenGL.GL_LINES);
        Edge(gl, (min.X, min.Y, min.Z), (max.X, min.Y, min.Z));
        Edge(gl, (max.X, min.Y, min.Z), (max.X, min.Y, max.Z));
        Edge(gl, (max.X, min.Y, max.Z), (min.X, min.Y, max.Z));
        Edge(gl, (min.X, min.Y, max.Z), (min.X, min.Y, min.Z));
        Edge(gl, (min.X, max.Y, min.Z), (max.X, max.Y, min.Z));
        Edge(gl, (max.X, max.Y, min.Z), (max.X, max.Y, max.Z));
        Edge(gl, (max.X, max.Y, max.Z), (min.X, max.Y, max.Z));
        Edge(gl, (min.X, max.Y, max.Z), (min.X, max.Y, min.Z));
        Edge(gl, (min.X, min.Y, min.Z), (min.X, max.Y, min.Z));
        Edge(gl, (max.X, min.Y, min.Z), (max.X, max.Y, min.Z));
        Edge(gl, (max.X, min.Y, max.Z), (max.X, max.Y, max.Z));
        Edge(gl, (min.X, min.Y, max.Z), (min.X, max.Y, max.Z));
        gl.End();
    }

    private void DrawSelectedGlbPoint(OpenGL gl)
    {
        if (selectedImportedMeshPoint is not { } point)
        {
            return;
        }

        DrawSelectedGlbTriangle(gl);
        DrawSelectedGlbNormal(gl, point);

        gl.PointSize(9.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 1.0, 0.12);
        gl.Vertex(point.X, point.Y, point.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawSelectedGlbTriangle(OpenGL gl)
    {
        if (importedMesh is not { } mesh || selectedImportedMeshTriangleIndex is not { } triangleIndex)
        {
            return;
        }

        var offset = triangleIndex * 3;
        if (offset < 0 || offset + 2 >= mesh.Indices.Length)
        {
            return;
        }

        var firstIndex = mesh.Indices[offset];
        var secondIndex = mesh.Indices[offset + 1];
        var thirdIndex = mesh.Indices[offset + 2];
        if (!ImportedMeshIndexInRange(mesh, firstIndex) || !ImportedMeshIndexInRange(mesh, secondIndex) || !ImportedMeshIndexInRange(mesh, thirdIndex))
        {
            return;
        }

        var first = mesh.Positions[firstIndex];
        var second = mesh.Positions[secondIndex];
        var third = mesh.Positions[thirdIndex];

        gl.LineWidth(3.0f);
        gl.Color(1.0, 0.95, 0.10);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(first.X, first.Y, first.Z);
        gl.Vertex(second.X, second.Y, second.Z);
        gl.Vertex(third.X, third.Y, third.Z);
        gl.End();
        gl.LineWidth(1.0f);
    }

    private void DrawSelectedGlbNormal(OpenGL gl, Vector3 point)
    {
        if (selectedImportedMeshSurfaceNormal is not { } normal || normal.LengthSquared() <= 0.0f)
        {
            return;
        }

        var end = point + Vector3.Normalize(normal) * GetImportedMeshSurfaceOverlayScale();
        gl.LineWidth(3.0f);
        gl.Color(0.10, 0.95, 1.0);
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(point.X, point.Y, point.Z);
        gl.Vertex(end.X, end.Y, end.Z);
        gl.End();
        gl.LineWidth(1.0f);
    }

    private void DrawLazMetadata(OpenGL gl)
    {
        var points = GetLazBoundsCorners(lazSample!);

        gl.LineWidth(1.8f);
        gl.Color(0.94, 0.82, 0.24);
        gl.Begin(OpenGL.GL_LINES);
        DrawBoxEdges(gl, points);
        gl.End();

        gl.PointSize(5.0f);
        gl.Color(0.35, 0.86, 1.0);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var point in points)
        {
            gl.Vertex(point.X, point.Y, point.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawLazPointCloud(OpenGL gl)
    {
        var pointCloud = lazPointCloud!;

        gl.PointSize((float)viewModel.PointSize);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var point in pointCloud.SampledPoints)
        {
            ApplyLazPointColor(gl, point);
            var position = MapLazPosition(point.Position);
            gl.Vertex(position.X, position.Y, position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
        DrawLazMetadata(gl);
        DrawSelectedLazPoint(gl);
    }

    private Vector3[] GetLazBoundsCorners(LazPointCloudMetadata metadata)
    {
        return
        [
            MapLazPosition(metadata.MinX, metadata.MinY, metadata.MinZ),
            MapLazPosition(metadata.MaxX, metadata.MinY, metadata.MinZ),
            MapLazPosition(metadata.MaxX, metadata.MaxY, metadata.MinZ),
            MapLazPosition(metadata.MinX, metadata.MaxY, metadata.MinZ),
            MapLazPosition(metadata.MinX, metadata.MinY, metadata.MaxZ),
            MapLazPosition(metadata.MaxX, metadata.MinY, metadata.MaxZ),
            MapLazPosition(metadata.MaxX, metadata.MaxY, metadata.MaxZ),
            MapLazPosition(metadata.MinX, metadata.MaxY, metadata.MaxZ)
        ];
    }

    private Vector3 MapLazPosition(Vector3 source) =>
        MapLazPosition(source.X, source.Y, source.Z);

    private Vector3 MapLazPosition(double x, double y, double z) =>
        new((float)(x - lazViewerOrigin.X), (float)(z - lazViewerOrigin.Z), (float)(y - lazViewerOrigin.Y));

    private void ApplyLazPointColor(OpenGL gl, LazPointCloudPoint point)
    {
        static double Normalize(ushort value) => value > 255 ? value / 65535.0 : value / 255.0;

        var (r, g, b) = viewModel.SelectedColorMode switch
        {
            "Solid" => (0.72, 0.84, 1.0),
            "Height" => C3DPointMapPalette.Height(NormalizeLazHeight(point.Position.Z)),
            _ => (Normalize(point.Red), Normalize(point.Green), Normalize(point.Blue))
        };

        gl.Color(r, g, b);
    }

    private double NormalizeLazHeight(float sourceZ)
    {
        if (lazSample is null || Math.Abs(lazSample.MaxZ - lazSample.MinZ) < 0.000001)
        {
            return 0.5;
        }

        return (sourceZ - lazSample.MinZ) / (lazSample.MaxZ - lazSample.MinZ);
    }

    private void DrawSelectedLazPoint(OpenGL gl)
    {
        if (selectedLazPoint is not { } point)
        {
            return;
        }

        var position = MapLazPosition(point.Position);
        gl.PointSize((float)Math.Max(8.0, viewModel.PointSize + 6.0));
        gl.Color(1.0, 0.95, 0.10);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Vertex(position.X, position.Y, position.Z);
        gl.End();
        gl.PointSize(1.0f);

        const float markerSize = 2.0f;
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(position.X - markerSize, position.Y, position.Z);
        gl.Vertex(position.X + markerSize, position.Y, position.Z);
        gl.Vertex(position.X, position.Y - markerSize, position.Z);
        gl.Vertex(position.X, position.Y + markerSize, position.Z);
        gl.Vertex(position.X, position.Y, position.Z - markerSize);
        gl.Vertex(position.X, position.Y, position.Z + markerSize);
        gl.End();
    }

    private static void DrawBoxEdges(OpenGL gl, IReadOnlyList<Vector3> points)
    {
        Edge(points[0], points[1]);
        Edge(points[1], points[2]);
        Edge(points[2], points[3]);
        Edge(points[3], points[0]);
        Edge(points[4], points[5]);
        Edge(points[5], points[6]);
        Edge(points[6], points[7]);
        Edge(points[7], points[4]);
        Edge(points[0], points[4]);
        Edge(points[1], points[5]);
        Edge(points[2], points[6]);
        Edge(points[3], points[7]);

        void Edge(Vector3 a, Vector3 b)
        {
            gl.Vertex(a.X, a.Y, a.Z);
            gl.Vertex(b.X, b.Y, b.Z);
        }
    }

    private void ApplyPointColor(OpenGL gl, HeightGridPoint point)
    {
        var (r, g, b) = viewModel.SelectedColorMode switch
        {
            "Solid" => (0.62, 0.82, 1.0),
            "Grayscale" => ViewerColorMapPalette.Grayscale(point.HeightScalar),
            "Thermal" => ViewerColorMapPalette.Thermal(point.HeightScalar),
            "Deviation" => DeviationColor(point.DeviationScalar),
            _ => C3DPointMapPalette.Height(point.HeightScalar)
        };

        gl.Color(r, g, b);
    }

    private static void ApplyPlaneFlatnessColor(OpenGL gl, Vector3 position, PlaneFlatnessEvaluation evaluation)
    {
        var plane = evaluation.ReferencePlane!;
        var signedDistance = plane.Normal.X * position.X
            + plane.Normal.Y * position.Y
            + plane.Normal.Z * position.Z
            + plane.Offset;
        var range = Math.Max(1e-9, Math.Max(Math.Abs(evaluation.MinimumSignedDistance), Math.Abs(evaluation.MaximumSignedDistance)));
        var normalized = Math.Clamp(signedDistance / range, -1.0, 1.0);
        var intensity = Math.Abs(normalized);
        var color = normalized >= 0.0
            ? (R: 1.0, G: 1.0 - 0.78 * intensity, B: 1.0 - 0.88 * intensity)
            : (R: 1.0 - 0.88 * intensity, G: 1.0 - 0.64 * intensity, B: 1.0);
        gl.Color(color.R, color.G, color.B);
    }

    private bool TryPickNominalActualDeviation(
        Point screenPoint,
        out NominalActualDeviationSample hit)
    {
        hit = default;
        var comparison = viewModel.NominalActual;
        if (viewModel.SelectedSelectionMode != "Point"
            || !comparison.ActualVisible
            || comparison.PreviewResult is not { } result
            || Viewport.ActualWidth <= 0
            || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        var maximumDistance = Math.Max(0.035f, (float)viewModel.CameraDistance * 0.006f);
        var nearestDepth = float.PositiveInfinity;
        var nearestRayDistance = float.PositiveInfinity;
        foreach (var sample in result.DisplaySamples)
        {
            var toPoint = sample.Position - ray.origin;
            var alongRay = Vector3.Dot(toPoint, ray.direction);
            if (alongRay < 0.0f)
            {
                continue;
            }

            var closestOnRay = ray.origin + ray.direction * alongRay;
            var rayDistance = Vector3.Distance(sample.Position, closestOnRay);
            if (rayDistance > maximumDistance
                || alongRay > nearestDepth
                || (alongRay == nearestDepth && rayDistance >= nearestRayDistance))
            {
                continue;
            }

            nearestDepth = alongRay;
            nearestRayDistance = rayDistance;
            hit = sample;
        }

        return float.IsFinite(nearestDepth);
    }

    private bool TryPickCube(Point screenPoint, out Vector3 hit)
    {
        hit = default;

        if (!viewModel.CubeVisible || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        if (!CameraMath.IntersectUnitCube(ray.origin, ray.direction, 1.0f, out var distance))
        {
            return false;
        }

        hit = ray.origin + ray.direction * distance;
        return true;
    }

    private bool TryPickC3DPoint(Point screenPoint, out HeightGridPoint hit)
    {
        hit = default;

        if (!viewModel.C3DSampleVisible || c3dSample is null || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        var bestDistance = float.PositiveInfinity;
        var maxDistance = Math.Max(0.12f, (float)viewModel.CameraDistance * 0.025f);

        foreach (var point in c3dSample.Points)
        {
            var position = TransformC3DPosition(point.Position);
            var toPoint = position - ray.origin;
            var alongRay = Vector3.Dot(toPoint, ray.direction);
            if (alongRay < 0)
            {
                continue;
            }

            var closest = ray.origin + ray.direction * alongRay;
            var distance = Vector3.Distance(position, closest);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = point;
            }
        }

        return bestDistance <= maxDistance;
    }

    private bool TryPickImportedMesh(
        Point screenPoint,
        out Vector3 hit,
        out string hitKind,
        out int? triangleIndex,
        out Vector3? surfaceNormal)
    {
        hit = default;
        hitKind = "mesh point";
        triangleIndex = null;
        surfaceNormal = null;

        if (!viewModel.GlbSampleVisible || importedMesh is null || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        if (TryPickImportedMeshTriangle(ray.origin, ray.direction, out hit, out var pickedTriangleIndex, out var pickedSurfaceNormal))
        {
            hitKind = "mesh surface";
            triangleIndex = pickedTriangleIndex;
            surfaceNormal = pickedSurfaceNormal;
            return true;
        }

        if (TryPickImportedMeshNearestVertex(ray.origin, ray.direction, out hit))
        {
            hitKind = "mesh vertex fallback";
            return true;
        }

        return false;
    }

    private bool TryPickImportedMeshTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out Vector3 hit,
        out int? triangleIndex,
        out Vector3? surfaceNormal)
    {
        hit = default;
        triangleIndex = null;
        surfaceNormal = null;

        var mesh = importedMesh!;
        var bestDistance = float.PositiveInfinity;
        var bestTriangleIndex = -1;
        var bestNormal = Vector3.Zero;
        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3)
        {
            var firstIndex = mesh.Indices[i];
            var secondIndex = mesh.Indices[i + 1];
            var thirdIndex = mesh.Indices[i + 2];
            if (!ImportedMeshIndexInRange(mesh, firstIndex) || !ImportedMeshIndexInRange(mesh, secondIndex) || !ImportedMeshIndexInRange(mesh, thirdIndex))
            {
                continue;
            }

            var first = mesh.Positions[firstIndex];
            var second = mesh.Positions[secondIndex];
            var third = mesh.Positions[thirdIndex];
            if (!TryIntersectRayTriangle(rayOrigin, rayDirection, first, second, third, out var distance, out var candidate))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = candidate;
                bestTriangleIndex = i / 3;
                bestNormal = CalculateTriangleNormal(first, second, third);
            }
        }

        if (!float.IsFinite(bestDistance))
        {
            return false;
        }

        triangleIndex = bestTriangleIndex;
        surfaceNormal = bestNormal;
        return true;
    }

    private bool TryPickImportedMeshNearestVertex(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 hit)
    {
        hit = default;
        var bestDistance = float.PositiveInfinity;
        var maxDistance = Math.Max(0.02f, (float)viewModel.CameraDistance * 0.025f);
        var positions = importedMesh!.Positions;

        foreach (var position in positions)
        {
            var toPoint = position - rayOrigin;
            var alongRay = Vector3.Dot(toPoint, rayDirection);
            if (alongRay < 0)
            {
                continue;
            }

            var closest = rayOrigin + rayDirection * alongRay;
            var distance = Vector3.Distance(position, closest);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = position;
            }
        }

        return bestDistance <= maxDistance;
    }

    private static bool ImportedMeshIndexInRange(ImportedMesh mesh, int index) =>
        (uint)index < (uint)mesh.Positions.Length;

    private static Vector3 CalculateTriangleNormal(Vector3 first, Vector3 second, Vector3 third)
    {
        var normal = Vector3.Cross(second - first, third - first);
        return normal.LengthSquared() <= 0.000000000001f
            ? Vector3.Zero
            : Vector3.Normalize(normal);
    }

    private float GetImportedMeshSurfaceOverlayScale()
    {
        if (importedMesh is null)
        {
            return 0.05f;
        }

        var diagonal = Vector3.Distance(importedMesh.Min, importedMesh.Max);
        return Math.Clamp(diagonal * 0.35f, 0.02f, 1.0f);
    }

    private static bool TryIntersectRayTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 first,
        Vector3 second,
        Vector3 third,
        out float distance,
        out Vector3 hit)
    {
        const float Epsilon = 0.0000001f;

        distance = 0.0f;
        hit = default;

        var edge1 = second - first;
        var edge2 = third - first;
        var p = Vector3.Cross(rayDirection, edge2);
        var determinant = Vector3.Dot(edge1, p);
        if (Math.Abs(determinant) < Epsilon)
        {
            return false;
        }

        var inverseDeterminant = 1.0f / determinant;
        var t = rayOrigin - first;
        var u = Vector3.Dot(t, p) * inverseDeterminant;
        if (u < -Epsilon || u > 1.0f + Epsilon)
        {
            return false;
        }

        var q = Vector3.Cross(t, edge1);
        var v = Vector3.Dot(rayDirection, q) * inverseDeterminant;
        if (v < -Epsilon || u + v > 1.0f + Epsilon)
        {
            return false;
        }

        distance = Vector3.Dot(edge2, q) * inverseDeterminant;
        if (distance < 0.0f)
        {
            return false;
        }

        hit = rayOrigin + rayDirection * distance;
        return true;
    }

    private bool TryPickLazPoint(Point screenPoint, out LazPointCloudPoint hit)
    {
        hit = default;

        if (!viewModel.LazSampleVisible || lazPointCloud is null || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        var bestDistance = float.PositiveInfinity;
        var maxDistance = Math.Max(1.0f, (float)viewModel.CameraDistance * 0.025f);

        foreach (var point in lazPointCloud.SampledPoints)
        {
            var position = MapLazPosition(point.Position);
            var toPoint = position - ray.origin;
            var alongRay = Vector3.Dot(toPoint, ray.direction);
            if (alongRay < 0)
            {
                continue;
            }

            var closest = ray.origin + ray.direction * alongRay;
            var distance = Vector3.Distance(position, closest);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = point;
            }
        }

        return bestDistance <= maxDistance;
    }

    private bool TryHandleTwoPointPick(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != TwoPointSelectionMode)
        {
            return false;
        }

        if (viewModel.LazSampleVisible && lazPointCloud is not null)
        {
            return TryHandleLazTwoPointPick(screenPoint);
        }

        if (viewModel.GlbSampleVisible && importedMesh is not null)
        {
            return TryHandleGlbTwoPointPick(screenPoint);
        }

        if (!TryPickC3DPoint(screenPoint, out var point))
        {
            viewModel.SelectedEntity = "Two Point Measurement";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Two-point pick missed C3D height grid";
            return true;
        }

        if (twoPointFirst is null || twoPointSecond is not null)
        {
            twoPointFirst = point;
            twoPointSecond = null;
            viewModel.SetTwoPointMeasurementStart(TransformC3DPosition(point.Position), point.RawValue);
            viewModel.SetPointPairFirstReference(point.Row, point.Column);
        }
        else
        {
            SetTwoPointMeasurement(twoPointFirst.Value, point);
        }

        viewModel.SelectedEntity = "Two Point Measurement";
        viewModel.PickCoordinate = FormatC3DPoint(point);
        return true;
    }

    private bool TryHandleGlbTwoPointPick(Point screenPoint)
    {
        if (!TryPickImportedMesh(screenPoint, out var point, out var pickKind, out var triangleIndex, out var surfaceNormal))
        {
            viewModel.SelectedEntity = "GLB Two Point Measurement";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = $"Two-point pick missed {viewModel.ImportedMeshFormat} mesh";
            return true;
        }

        if (importedMeshTwoPointFirst is null || importedMeshTwoPointSecond is not null)
        {
            importedMeshTwoPointFirst = point;
            importedMeshTwoPointSecond = null;
            twoPointFirst = null;
            twoPointSecond = null;
            lazTwoPointFirst = null;
            lazTwoPointSecond = null;
            selectedImportedMeshPoint = point;
            selectedImportedMeshPickKind = pickKind;
            selectedImportedMeshTriangleIndex = triangleIndex;
            selectedImportedMeshSurfaceNormal = surfaceNormal;
            viewModel.SetTwoPointMeasurementStart(point, point.Y, "model-y");
        }
        else
        {
            SetImportedMeshTwoPointMeasurement(importedMeshTwoPointFirst.Value, point);
        }

        viewModel.SelectedEntity = "GLB Two Point Measurement";
        viewModel.PickCoordinate = FormatImportedMeshPoint(point, pickKind);
        return true;
    }

    private bool TryHandleLazTwoPointPick(Point screenPoint)
    {
        if (!TryPickLazPoint(screenPoint, out var point))
        {
            viewModel.SelectedEntity = "LAZ/LAS Two Point Measurement";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Two-point pick missed LAZ/LAS point cloud";
            return true;
        }

        if (lazTwoPointFirst is null || lazTwoPointSecond is not null)
        {
            lazTwoPointFirst = point;
            lazTwoPointSecond = null;
            twoPointFirst = null;
            twoPointSecond = null;
            selectedLazPoint = point;
            var position = MapLazPosition(point.Position);
            viewModel.SetTwoPointMeasurementStart(position, position.Y, "source-z-units");
        }
        else
        {
            SetLazTwoPointMeasurement(lazTwoPointFirst.Value, point);
        }

        viewModel.SelectedEntity = "LAZ/LAS Two Point Measurement";
        viewModel.PickCoordinate = FormatLazPoint(point);
        return true;
    }

    private bool TryHandleRoiStepPick(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != RoiStepSelectionMode)
        {
            return false;
        }

        if (!TryPickC3DPoint(screenPoint, out var point))
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "ROI pick missed C3D height grid";
            return true;
        }

        var anchor = TransformC3DPosition(point.Position);
        roiStepInteractiveSelection = true;
        ClearRecipeRoiStep();
        if (!roiStepNextPickSetsRight || roiStepLeftAnchor is null || roiStepRightAnchor is not null)
        {
            roiStepLeftAnchor = anchor;
            roiStepRightAnchor = null;
            roiStepNextPickSetsRight = true;
        }
        else
        {
            roiStepRightAnchor = anchor;
            roiStepNextPickSetsRight = false;
        }

        UpdateRoiStepMeasurement();
        viewModel.SelectedEntity = "ROI Step Compare";
        viewModel.PickCoordinate = FormatC3DPoint(point);
        return true;
    }

    private void SetTwoPointMeasurement(HeightGridPoint first, HeightGridPoint second, bool updatePointPairReferences = true)
    {
        ClearPlaneReferenceMeasurement();
        twoPointFirst = first;
        twoPointSecond = second;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedImportedMeshPoint = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        viewModel.SetTwoPointMeasurement(TransformC3DPosition(first.Position), first.RawValue, TransformC3DPosition(second.Position), second.RawValue);
        if (updatePointPairReferences)
        {
            viewModel.SetPointPairReferences(first.Row, first.Column, second.Row, second.Column);
        }
    }

    private void SetLazTwoPointMeasurement(LazPointCloudPoint first, LazPointCloudPoint second, string heightUnit = "source-z-units")
    {
        ClearPlaneReferenceMeasurement();
        lazTwoPointFirst = first;
        lazTwoPointSecond = second;
        selectedLazPoint = second;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        selectedImportedMeshPoint = null;
        twoPointFirst = null;
        twoPointSecond = null;

        var firstPosition = MapLazPosition(first.Position);
        var secondPosition = MapLazPosition(second.Position);
        viewModel.SetTwoPointMeasurement(firstPosition, firstPosition.Y, secondPosition, secondPosition.Y, heightUnit);
        viewModel.SetLazTwoPointMeasurementPreview(firstPosition, secondPosition, secondPosition.Y - firstPosition.Y, heightUnit);
    }

    private void SetImportedMeshTwoPointMeasurement(Vector3 first, Vector3 second)
    {
        ClearPlaneReferenceMeasurement();
        importedMeshTwoPointFirst = first;
        importedMeshTwoPointSecond = second;
        selectedImportedMeshPoint = second;
        selectedImportedMeshPickKind = "mesh measurement point";
        selectedImportedMeshTriangleIndex = null;
        selectedImportedMeshSurfaceNormal = null;
        twoPointFirst = null;
        twoPointSecond = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        selectedLazPoint = null;
        viewModel.SetTwoPointMeasurement(first, first.Y, second, second.Y, "model-y");
        viewModel.SelectionSummary = $"GLB measurement: {viewModel.TwoPointMeasurementDetails}";
        viewModel.MeasurementSummary = $"GLB measurement: {viewModel.TwoPointMeasurementDetails}";
    }

    private void ClearPlaneReferenceMeasurement()
    {
        planeReferenceMeasurement = null;
        viewModel.ClearPlaneReferenceMeasurement();
    }

    private bool UpdateRoiStepMeasurement()
    {
        ClearPlaneReferenceMeasurement();
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;

        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ClearRoiStepMeasurement("ROI step requires a visible C3D height grid.");
            viewModel.SelectedEntity = "ROI Step Compare";
            return false;
        }

        var bounds = GetTransformedC3DBounds();
        var width = Math.Max(0.001f, bounds.MaxX - bounds.MinX);
        var depth = Math.Max(0.001f, bounds.MaxZ - bounds.MinZ);
        var halfWidth = width * 0.15f;
        var halfDepth = depth * 0.25f;
        var zMin = bounds.MinZ + depth * 0.25f;
        var zMax = bounds.MinZ + depth * 0.75f;
        var leftBounds = roiStepLeftRecipeRegion is { } leftRegion
            ? CreateRoiBounds(leftRegion, bounds)
            : roiStepInteractiveSelection && roiStepLeftAnchor is { } leftAnchor
                ? CreateRoiBounds(leftAnchor, halfWidth, halfDepth, bounds)
                : (MinX: bounds.MinX + width * 0.10f, MaxX: bounds.MinX + width * 0.40f, MinZ: zMin, MaxZ: zMax, MeanY: 0.0f);
        var rightBounds = roiStepRightRecipeRegion is { } rightRegion
            ? CreateRoiBounds(rightRegion, bounds)
            : roiStepInteractiveSelection && roiStepRightAnchor is { } rightAnchor
                ? CreateRoiBounds(rightAnchor, halfWidth, halfDepth, bounds)
                : (MinX: bounds.MinX + width * 0.60f, MaxX: bounds.MinX + width * 0.90f, MinZ: zMin, MaxZ: zMax, MeanY: 0.0f);

        if (!TryCalculateRoiStats(leftBounds, out var left))
        {
            viewModel.ClearRoiStepMeasurement("ROI step found no C3D points in the left region.");
            viewModel.SelectedEntity = "ROI Step Compare";
            return false;
        }

        roiStepLeftBounds = (leftBounds.MinX, leftBounds.MaxX, leftBounds.MinZ, leftBounds.MaxZ, (float)left.ModelYMean);
        roiStepLeftCenter = left.Center;

        if (roiStepInteractiveSelection && roiStepRightAnchor is null)
        {
            viewModel.SetRoiStepSelectionPending(
                string.Create(CultureInfo.InvariantCulture, $"ROI step: L {left.Count:N0} pts, pick R"),
                string.Create(CultureInfo.InvariantCulture, $"Left mean raw {left.RawMean:F3}; click right ROI center."),
                "Interactive");
            viewModel.SelectedEntity = "ROI Step Compare";
            return true;
        }

        if (!TryCalculateRoiStats(rightBounds, out var right))
        {
            viewModel.ClearRoiStepMeasurement("ROI step found no C3D points in the right region.");
            viewModel.SelectedEntity = "ROI Step Compare";
            return false;
        }

        roiStepRightBounds = (rightBounds.MinX, rightBounds.MaxX, rightBounds.MinZ, rightBounds.MaxZ, (float)right.ModelYMean);
        roiStepRightCenter = right.Center;

        viewModel.SetRoiStepMeasurement(
            left.Count,
            left.RawMean,
            left.ModelYMean,
            right.Count,
            right.RawMean,
            right.ModelYMean,
            roiStepInteractiveSelection ? "Interactive" : "Auto");
        SyncRecipeRoiEditFromBounds(roiStepInteractiveSelection ? "Interactive" : "Auto", leftBounds, rightBounds);
        viewModel.SelectedEntity = "ROI Step Compare";
        viewModel.PickCoordinate = string.Create(
            CultureInfo.InvariantCulture,
            $"ROI centers: L {CameraMath.FormatPoint(left.Center)} | R {CameraMath.FormatPoint(right.Center)}");
        return true;
    }

    private (float MinX, float MaxX, float MinZ, float MaxZ) GetTransformedC3DBounds()
    {
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var minZ = float.PositiveInfinity;
        var maxZ = float.NegativeInfinity;

        foreach (var point in c3dSample!.Points)
        {
            var position = TransformC3DPosition(point.Position);
            minX = Math.Min(minX, position.X);
            maxX = Math.Max(maxX, position.X);
            minZ = Math.Min(minZ, position.Z);
            maxZ = Math.Max(maxZ, position.Z);
        }

        return (minX, maxX, minZ, maxZ);
    }

    private static (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) CreateRoiBounds(
        Vector3 center,
        float halfWidth,
        float halfDepth,
        (float MinX, float MaxX, float MinZ, float MaxZ) sceneBounds) =>
        (
            Math.Clamp(center.X - halfWidth, sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp(center.X + halfWidth, sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp(center.Z - halfDepth, sceneBounds.MinZ, sceneBounds.MaxZ),
            Math.Clamp(center.Z + halfDepth, sceneBounds.MinZ, sceneBounds.MaxZ),
            center.Y);

    private static (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) CreateRoiBounds(
        HeightDeviationRecipeRoiRegion region,
        (float MinX, float MaxX, float MinZ, float MaxZ) sceneBounds) =>
        (
            Math.Clamp((float)(region.CenterX - region.HalfWidth), sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp((float)(region.CenterX + region.HalfWidth), sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp((float)(region.CenterZ - region.HalfDepth), sceneBounds.MinZ, sceneBounds.MaxZ),
            Math.Clamp((float)(region.CenterZ + region.HalfDepth), sceneBounds.MinZ, sceneBounds.MaxZ),
            0.0f);

    private bool TryCalculateRoiStats(
        (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) bounds,
        out (int Count, double RawMean, double ModelYMean, Vector3 Center) stats)
    {
        var count = 0;
        var rawSum = 0.0;
        var xSum = 0.0;
        var ySum = 0.0;
        var zSum = 0.0;

        foreach (var point in c3dSample!.Points)
        {
            var position = TransformC3DPosition(point.Position);
            if (position.X < bounds.MinX || position.X > bounds.MaxX
                || position.Z < bounds.MinZ || position.Z > bounds.MaxZ)
            {
                continue;
            }

            count++;
            rawSum += point.RawValue;
            xSum += position.X;
            ySum += position.Y;
            zSum += position.Z;
        }

        if (count == 0)
        {
            stats = default;
            return false;
        }

        var inverse = 1.0 / count;
        stats = (
            count,
            rawSum * inverse,
            ySum * inverse,
            new Vector3((float)(xSum * inverse), (float)(ySum * inverse), (float)(zSum * inverse)));
        return true;
    }

    private (Vector3 origin, Vector3 direction) CreatePickRay(Point screenPoint)
    {
        return CameraMath.CreatePickRay(
            screenPoint,
            Viewport.ActualWidth,
            Viewport.ActualHeight,
            FieldOfViewDegrees,
            GetCameraPosition(),
            GetCameraTarget());
    }

    private void PanCamera(System.Windows.Vector delta)
    {
        var target = GetCameraTarget();
        var eye = GetCameraPosition();
        var movement = CameraMath.PanDelta(
            delta,
            Viewport.ActualHeight,
            FieldOfViewDegrees,
            viewModel.CameraDistance,
            target,
            eye);

        viewModel.Pan(movement.X, movement.Y, movement.Z);
    }

    private C3DHeightGrid? LoadDefaultC3DSample()
    {
        var path = FindDefaultC3DSamplePath();
        if (path is null)
        {
            return null;
        }

        try
        {
            return C3DHeightGrid.Load(path, viewModel.C3DMaxRenderedPoints);
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private ImportedMesh? LoadDefaultGlbSample()
    {
        var path = FindDefaultGlbSamplePath();
        return path is null ? null : LoadGlbSample(path);
    }

    private LazPointCloudMetadata? LoadDefaultLazSample()
    {
        var path = FindDefaultLazSamplePath();
        return path is null ? null : LoadLazSample(path);
    }

    private ImportedMesh? LoadGlbSample(string path)
    {
        ResetImportedMeshTextureUpload();
        var candidate = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        viewModel.SetGlbSampleSource(path, Path.GetFileNameWithoutExtension(path), "GLB");
        if (!File.Exists(candidate))
        {
            viewModel.GlbSampleTriangleCount = "(missing)";
            viewModel.GlbSampleSummary = $"Missing GLB sample: {path}";
            return null;
        }

        try
        {
            var mesh = GlbMesh.Load(candidate);
            viewModel.SetGlbSampleSource(path, Path.GetFileNameWithoutExtension(path), "GLB");
            return mesh;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            viewModel.GlbSampleTriangleCount = "(unsupported)";
            viewModel.GlbSampleSummary = $"Unsupported or corrupt GLB: {ex.Message}";
            return null;
        }
    }

    private ImportedMesh? LoadStlSample(string path)
    {
        ResetImportedMeshTextureUpload();
        var candidate = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        viewModel.SetGlbSampleSource(path, Path.GetFileNameWithoutExtension(path), "STL");
        if (!File.Exists(candidate))
        {
            viewModel.GlbSampleTriangleCount = "(missing)";
            viewModel.GlbSampleSummary = $"Missing STL sample: {path}";
            return null;
        }

        try
        {
            var mesh = StlMesh.Load(candidate);
            viewModel.SetGlbSampleSource(path, Path.GetFileNameWithoutExtension(path), "STL");
            return mesh;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or OverflowException)
        {
            viewModel.GlbSampleTriangleCount = "(unsupported)";
            viewModel.GlbSampleSummary = $"Unsupported or corrupt STL: {ex.Message}";
            return null;
        }
    }

    private void ResetImportedMeshTextureUpload()
    {
        importedMeshTextureSource = null;
        importedMeshTextureId = 0;
        importedMeshTextureUploadFailed = false;
        importedMeshTextureUploadSummary = "texture none";
    }

    private LazPointCloudMetadata? LoadLazSample(string path)
    {
        lazPointCloud = null;
        lazViewerOrigin = default;
        var candidate = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        viewModel.SetLazSampleSource(path, Path.GetFileNameWithoutExtension(path));
        if (!File.Exists(candidate))
        {
            viewModel.LazSamplePointCount = "(missing)";
            viewModel.LazSampleSummary = $"Missing LAZ/LAS sample: {path}";
            return null;
        }

        try
        {
            var metadata = LazPointCloudMetadata.Load(candidate);
            SetLazViewerOrigin(metadata);
            viewModel.SetLazSampleSource(path, Path.GetFileNameWithoutExtension(path));
            return metadata;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            viewModel.LazSamplePointCount = "(unsupported)";
            viewModel.LazSampleSummary = $"Unsupported or corrupt LAZ/LAS: {ex.Message}";
            lazViewerOrigin = default;
            return null;
        }
    }

    private LazPointCloud? LoadLazPointCloud(string path) => LoadLazPointCloud(path, viewModel.LazMaxSampledPoints);

    private LazPointCloud? LoadLazPointCloud(string path, int maxSampledPoints)
    {
        var candidate = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        viewModel.SetLazSampleSource(path, Path.GetFileNameWithoutExtension(path));
        if (!File.Exists(candidate))
        {
            viewModel.LazSamplePointCount = "(missing)";
            viewModel.LazSampleSummary = $"Missing LAZ/LAS sample: {path}";
            lazViewerOrigin = default;
            return null;
        }

        try
        {
            var loadStart = Stopwatch.GetTimestamp();
            var pointCloud = LazPointCloud.Load(candidate, Math.Max(2, maxSampledPoints));
            var loadMilliseconds = Stopwatch.GetElapsedTime(loadStart).TotalMilliseconds;
            SetLazViewerOrigin(pointCloud.Metadata);
            viewModel.SetLazSampleSource(path, Path.GetFileNameWithoutExtension(path));
            viewModel.SetLazSamplingTelemetry(
                pointCloud.DecodedPointCount,
                pointCloud.SampledPoints.Length,
                pointCloud.SampleStride,
                loadMilliseconds);
            return pointCloud;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            viewModel.LazSamplePointCount = "(unsupported)";
            viewModel.LazSampleSummary = $"Unsupported or corrupt LAZ/LAS point decode: {ex.Message}";
            viewModel.ClearLazSamplingTelemetry("LAZ/LAS sampling: load failed");
            lazViewerOrigin = default;
            return null;
        }
    }

    private void ReloadCurrentLazPointCloud()
    {
        if (lazPointCloud is null)
        {
            return;
        }

        var sourcePath = lazPointCloud.SourcePath;
        var reloaded = LoadLazPointCloud(sourcePath);
        if (reloaded is null)
        {
            viewModel.UseLazFailureScene(viewModel.LazSampleSummary);
            return;
        }

        lazPointCloud = reloaded;
        lazSample = reloaded.Metadata;
        selectedLazPoint = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        viewModel.ClearTwoPointMeasurement();
        SetLazSampleStatus();
        viewModel.SelectionSummary = "Point selection: reset after point-cloud density change";
        viewModel.MeasurementSummary = "Distance and height delta: reset after point-cloud density change";
        viewModel.PickCoordinate = "(none)";
        viewModel.ViewerStatus = $"Point cloud re-sampled: {viewModel.SelectedRenderDensity}";
    }

    private void SetLazViewerOrigin(LazPointCloudMetadata metadata)
    {
        lazViewerOrigin = (
            (metadata.MinX + metadata.MaxX) * 0.5,
            (metadata.MinY + metadata.MaxY) * 0.5,
            (metadata.MinZ + metadata.MaxZ) * 0.5);
        var corners = GetLazBoundsCorners(metadata);
        var min = corners[0];
        var max = corners[0];
        foreach (var corner in corners)
        {
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        viewModel.SetLazSampleBounds(min, max);
    }

    private static string ReadRecipeType(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.TryGetProperty("recipeType", out var recipeType)
            ? recipeType.GetString() ?? throw new InvalidDataException($"Recipe type is empty: {path}")
            : throw new InvalidDataException($"Recipe type is missing: {path}");
    }

    private static string? FindDefaultC3DSamplePath()
    {
        foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, DefaultC3DSamplePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static string? FindDefaultGlbSamplePath()
    {
        foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, DefaultGlbSamplePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static string? FindDefaultLazSamplePath()
    {
        foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, DefaultLazSamplePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static string ResolveRecipePath(string path, string recipeDirectory)
    {
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(recipeDirectory, path));
    }

    private void SetC3DSampleStatus()
    {
        if (c3dSample is null)
        {
            InvalidateC3DRenderProxy();
            viewModel.SetC3DDisplayCapabilities(surfaceGeometryAvailable: false);
            viewModel.C3DSamplePointCount = "(missing)";
            viewModel.C3DSampleSummary = $"Missing sample: {DefaultC3DSamplePath}";
            viewModel.ClearHeightMap();
            viewModel.ClearSectionProfile();
            return;
        }

        var renderProxy = GetC3DRenderProxy();
        viewModel.SetC3DDisplayCapabilities(renderProxy.HasSurface);
        viewModel.C3DSamplePointCount = c3dSample.Points.Length.ToString("N0", CultureInfo.InvariantCulture);
        viewModel.C3DSampleSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{c3dSample.Width} x {c3dSample.Height} | rendered {c3dSample.Points.Length:N0} | density {viewModel.SelectedRenderDensity} | valid {c3dSample.ValidSampleCount:N0} | zero {c3dSample.ZeroSampleCount:N0} | min {c3dSample.Min:F3} | max {c3dSample.Max:F3}");
        UpdateHeightMapFromC3D();
        UpdateSectionProfileFromC3D();
    }

    private void SetGlbSampleStatus()
    {
        viewModel.SetImportedMeshDisplayCapabilities(
            importedMesh is { } mesh && (mesh.HasVertexColors || mesh.HasBaseColorTexture));

        if (importedMesh is null)
        {
            viewModel.GlbSampleTriangleCount = "(missing)";
            viewModel.GlbSampleSummary = $"Missing sample: {DefaultGlbSamplePath}";
            return;
        }

        viewModel.GlbSampleTriangleCount = importedMesh.TriangleCount.ToString("N0", CultureInfo.InvariantCulture);
        var colorSummary = importedMesh.HasVertexColors
            ? $"vertex colors {importedMesh.VertexColors.Length:N0}"
            : "vertex colors none";
        var textureSummary = importedMesh.HasBaseColorTexture
            ? $"texture {importedMesh.BaseColorTexture!.MimeType} {importedMesh.BaseColorTexture.Bytes.Length:N0} bytes | texcoords {importedMesh.TextureCoordinates.Length:N0}"
            : "texture none";
        viewModel.GlbSampleSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{Path.GetFileName(importedMesh.SourcePath)} | format {importedMesh.Format} | vertices {importedMesh.Positions.Length:N0} | triangles {importedMesh.TriangleCount:N0} | {colorSummary} | {textureSummary} | bounds {FormatVector(importedMesh.Min)} to {FormatVector(importedMesh.Max)}");
        viewModel.SetGlbSampleSource(importedMesh.SourcePath, Path.GetFileNameWithoutExtension(importedMesh.SourcePath), importedMesh.Format);
        viewModel.SetGlbSampleBounds(importedMesh.Min, importedMesh.Max);
    }

    private void SetLazSampleStatus()
    {
        viewModel.SetLazDisplayCapabilities(lazPointCloud?.HasRgb == true);

        if (lazSample is null)
        {
            viewModel.LazSamplePointCount = "(missing)";
            viewModel.LazSampleSummary = $"Missing sample: {DefaultLazSamplePath}";
            viewModel.SetLazHeightRange(double.NaN, double.NaN, "source-z");
            viewModel.ClearLazSamplingTelemetry("LAZ/LAS sampling: not loaded");
            return;
        }

        viewModel.LazSamplePointCount = lazSample.PointCount.ToString("N0", CultureInfo.InvariantCulture);
        if (lazPointCloud is null)
        {
            viewModel.LazSampleSummary = $"{lazSample.FormatSummary()} | metadata only; point rendering pending";
            viewModel.ClearLazSamplingTelemetry("LAZ/LAS sampling: metadata only");
        }
        else
        {
            viewModel.LazSamplePointCount = string.Create(
                CultureInfo.InvariantCulture,
                $"{lazPointCloud.DecodedPointCount:N0} / sampled {lazPointCloud.SampledPoints.Length:N0}");
            viewModel.LazSampleSummary = string.Create(
                CultureInfo.InvariantCulture,
                $"{Path.GetFileName(lazPointCloud.SourcePath)} | decoded {lazPointCloud.DecodedPointCount:N0} | sampled {lazPointCloud.SampledPoints.Length:N0} | density {viewModel.SelectedRenderDensity} | load {viewModel.LazLoadMilliseconds:F0} ms | sample {viewModel.LazSamplePercent:F2}% | RGB {lazPointCloud.HasRgb} | bounds match {lazPointCloud.BoundsMatch}");
        }

        viewModel.SetLazSampleSource(lazSample.SourcePath, Path.GetFileNameWithoutExtension(lazSample.SourcePath));
        viewModel.SetLazHeightRange(lazSample.MinZ, lazSample.MaxZ, "source-z");
    }

    private bool EnsureImportedMeshTexture(OpenGL gl)
    {
        if (importedMesh is null || !importedMesh.HasBaseColorTexture)
        {
            return false;
        }

        if (ReferenceEquals(importedMeshTextureSource, importedMesh))
        {
            return importedMeshTextureId != 0;
        }

        if (importedMeshTextureUploadFailed)
        {
            return false;
        }

        try
        {
            var texture = DecodeTexture(importedMesh.BaseColorTexture!.Bytes);
            var ids = new uint[1];
            gl.GenTextures(1, ids);
            importedMeshTextureId = ids[0];
            gl.BindTexture(GlTexture2D, importedMeshTextureId);
            gl.TexParameter(GlTexture2D, GlTextureMinFilter, (int)GlLinear);
            gl.TexParameter(GlTexture2D, GlTextureMagFilter, (int)GlLinear);
            gl.TexParameter(GlTexture2D, GlTextureWrapS, (int)GlRepeat);
            gl.TexParameter(GlTexture2D, GlTextureWrapT, (int)GlRepeat);
            gl.PixelStore(GlUnpackAlignment, 1);
            gl.TexImage2D(
                GlTexture2D,
                0,
                GlRgba,
                texture.Width,
                texture.Height,
                0,
                GlBgra,
                GlUnsignedByte,
                texture.Pixels);
            importedMeshTextureSource = importedMesh;
            importedMeshTextureUploadSummary = string.Create(
                CultureInfo.InvariantCulture,
                $"uploaded {texture.Width}x{texture.Height} {importedMesh.BaseColorTexture.MimeType}");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or NotSupportedException)
        {
            importedMeshTextureUploadFailed = true;
            importedMeshTextureUploadSummary = $"upload failed: {ex.Message}";
            return false;
        }
    }

    private static (int Width, int Height, byte[] Pixels) DecodeTexture(byte[] encodedImage)
    {
        using var stream = new MemoryStream(encodedImage);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0)
        {
            throw new InvalidOperationException("Texture image has no frames.");
        }

        BitmapSource source = decoder.Frames[0];
        if (source.Format != PixelFormats.Bgra32)
        {
            source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        }

        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return (source.PixelWidth, source.PixelHeight, pixels);
    }

    private void UpdateHeightMapFromC3D()
    {
        if (c3dSample is null || c3dSample.Points.Length == 0)
        {
            viewModel.ClearHeightMap();
            return;
        }

        const int pixelWidth = 240;
        const int pixelHeight = 72;
        var pixels = new byte[pixelWidth * pixelHeight * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 31;
            pixels[index + 1] = 24;
            pixels[index + 2] = 17;
            pixels[index + 3] = 255;
        }

        foreach (var point in c3dSample.Points)
        {
            var x = (point.Position.X + c3dSample.XHalfExtent) / Math.Max(0.0001f, c3dSample.XHalfExtent * 2.0f);
            var z = (point.Position.Z + c3dSample.ZHalfExtent) / Math.Max(0.0001f, c3dSample.ZHalfExtent * 2.0f);
            var column = (int)Math.Round(Math.Clamp(x, 0.0f, 1.0f) * (pixelWidth - 1));
            var row = (int)Math.Round(Math.Clamp(z, 0.0f, 1.0f) * (pixelHeight - 1));
            PaintHeightMapPixel(pixels, pixelWidth, pixelHeight, column, row, point.HeightScalar);
        }

        var bitmap = BitmapSource.Create(
            pixelWidth,
            pixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            pixelWidth * 4);
        bitmap.Freeze();

        viewModel.SetHeightMap(
            bitmap,
            c3dSample.Width,
            c3dSample.Height,
            c3dSample.Points.Length,
            c3dSample.Min,
            c3dSample.Max,
            c3dSample.Mean,
            pixelWidth,
            pixelHeight);
    }

    private static void PaintHeightMapPixel(byte[] pixels, int pixelWidth, int pixelHeight, int column, int row, double heightScalar)
    {
        var (r, g, b) = HeightMapColor(heightScalar);
        for (var y = Math.Max(0, row - 1); y <= Math.Min(pixelHeight - 1, row + 1); y++)
        {
            for (var x = Math.Max(0, column - 1); x <= Math.Min(pixelWidth - 1, column + 1); x++)
            {
                var index = (y * pixelWidth + x) * 4;
                pixels[index] = b;
                pixels[index + 1] = g;
                pixels[index + 2] = r;
                pixels[index + 3] = 255;
            }
        }
    }

    private static (byte R, byte G, byte B) HeightMapColor(double value)
    {
        var (r, g, b) = C3DPointMapPalette.Height(value);
        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private void UpdateSectionProfileFromC3D()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ClearSectionProfile();
            return;
        }

        var centerZ = c3dSample.Points.MinBy(point => Math.Abs(point.Position.Z)).Position.Z;
        var samples = c3dSample.Points
            .Where(point => Math.Abs(point.Position.Z - centerZ) < 0.0005f)
            .OrderBy(point => point.Position.X)
            .ToArray();

        if (samples.Length < 2)
        {
            viewModel.ClearSectionProfile();
            return;
        }

        var min = samples.Min(point => point.RawValue);
        var max = samples.Max(point => point.RawValue);
        var mean = samples.Average(point => point.RawValue);
        var rowIndex = EstimateProfileRowIndex(centerZ);
        viewModel.SetSectionProfile(
            "C3D Thickness Sample",
            rowIndex,
            samples.Length,
            min,
            max,
            mean,
            BuildSectionProfilePath(samples, min, max));
    }

    private int EstimateProfileRowIndex(float z)
    {
        if (c3dSample is null || c3dSample.ZHalfExtent <= 0.0f)
        {
            return 0;
        }

        var normalized = (z + c3dSample.ZHalfExtent) / (c3dSample.ZHalfExtent * 2.0f);
        return (int)Math.Round(Math.Clamp(normalized, 0.0f, 1.0f) * (c3dSample.Height - 1));
    }

    private static string BuildSectionProfilePath(IReadOnlyList<HeightGridPoint> samples, double min, double max)
    {
        const double chartWidth = 240.0;
        const double chartHeight = 54.0;
        const double padding = 3.0;
        var span = Math.Max(0.001, max - min);
        var stride = Math.Max(1, (int)Math.Ceiling(samples.Count / 80.0));
        var reduced = samples.Where((_, index) => index % stride == 0).ToList();
        if (reduced[^1] != samples[^1])
        {
            reduced.Add(samples[^1]);
        }

        var builder = new StringBuilder();
        for (var index = 0; index < reduced.Count; index++)
        {
            var sample = reduced[index];
            var x = reduced.Count == 1 ? 0.0 : chartWidth * index / (reduced.Count - 1);
            var y = padding + (1.0 - ((sample.RawValue - min) / span)) * (chartHeight - padding * 2.0);
            builder.Append(index == 0 ? "M " : " L ");
            builder.Append(x.ToString("F1", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(y.ToString("F1", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private void ReloadDefaultC3DSample()
    {
        var sourcePath = c3dSample?.SourcePath ?? FindDefaultC3DSamplePath();
        var pointPairStep = viewModel.CreatePointPairDimensionsRecipeStep();
        var restorePointPairPreview = viewModel.PointPairDimensionsVisible;
        var restoreFlatnessPreview = viewModel.PlaneFlatnessVisible;
        var restoreVolumePreview = viewModel.VolumeVisible;
        var restoreCrossSectionPreview = viewModel.CrossSectionVisible;
        try
        {
            c3dSample = string.IsNullOrWhiteSpace(sourcePath)
                ? null
                : C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            c3dSample = null;
            viewModel.ViewerStatus = $"C3D render-density reload failed: {ex.Message}";
        }

        twoPointFirst = null;
        twoPointSecond = null;
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;
        roiStepLeftAnchor = null;
        roiStepRightAnchor = null;
        ClearRecipeRoiStep();
        roiStepInteractiveSelection = false;
        roiStepNextPickSetsRight = false;
        viewModel.ClearTwoPointMeasurement();
        viewModel.ClearRoiStepMeasurement();
        SetC3DSampleStatus();
        if (c3dSample is not null && pointPairStep is not null)
        {
            try
            {
                var first = c3dSample.ReadPoint(pointPairStep.First.Row, pointPairStep.First.Column);
                var second = c3dSample.ReadPoint(pointPairStep.Second.Row, pointPairStep.Second.Column);
                SetTwoPointMeasurement(first, second, updatePointPairReferences: false);
                if (restorePointPairPreview)
                {
                    PreviewC3DPointPairDimensions();
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException)
            {
                viewModel.InvalidatePointPairDimensionsPreview($"C3D reload invalidated point references: {ex.Message}");
            }
        }
        else if (restoreCrossSectionPreview)
        {
            PreviewC3DCrossSection();
        }
        else if (restoreVolumePreview)
        {
            PreviewC3DVolume();
        }
        else if (restoreFlatnessPreview)
        {
            PreviewC3DPlaneFlatness();
        }
        else
        {
            ConfigureC3DHeightDeviationRule();
        }
    }

    private void ConfigureC3DHeightDeviationRule()
    {
        if (c3dSample is null)
        {
            return;
        }

        var result = HeightDeviationRule.Evaluate(new HeightDeviationRuleInput(
            MainWindowViewModel.C3DEntityId,
            viewModel.RecipeSourceName,
            c3dSample.Min,
            c3dSample.Max,
            c3dSample.Mean,
            c3dSample.ValidSampleCount,
            viewModel.RecipePeakTolerance,
            viewModel.RecipeSourceUnit));

        viewModel.SetC3DHeightDeviationPreview(result);
    }

    private void WriteSceneContracts(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var displaySettings = viewModel.Display.EffectiveSettings;
        var c3dRenderProxyForContract = c3dSample is null ? null : GetC3DRenderProxy();
        var geometryRenderBridge = displaySettings.Source == ViewerDisplaySourceKind.C3DHeightGrid
            ? "SharpGLC3DSampledGrid"
            : "Pending";
        var lines = new List<string>
        {
            viewModel.SceneContractSummary,
            "SourceEntities"
        };

        lines.AddRange(viewModel.SourceEntities.Select(InspectionContractText.FormatSourceEntity));
        lines.Add("EntityLayers");
        lines.AddRange(viewModel.EntityLayers.Select(InspectionContractText.FormatEntityLayer));
        lines.Add(InspectionContractText.PreviewToolResultMarker);
        var result = viewModel.PreviewToolResult;
        lines.Add(InspectionContractText.FormatToolResult(result));
        lines.Add(InspectionContractText.PreviewMetricsMarker);
        lines.AddRange(result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add(InspectionContractText.PreviewOverlaysMarker);
        lines.AddRange(result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
        lines.Add("ColorScaleLegend");
        lines.Add($"DeviationLegend|visible={viewModel.DeviationLegendVisible}|{viewModel.DeviationLegendStatus}|{viewModel.DeviationLegendPeak}|{viewModel.DeviationLegendTolerance}|{viewModel.DeviationLegendScale}");
        lines.Add($"PointCloudColorLegend|visible={viewModel.PointCloudColorLegendVisible}|{viewModel.PointCloudColorLegendLow}|{viewModel.PointCloudColorLegendHigh}|{viewModel.PointCloudColorLegendScale}");
        lines.Add("RenderControls");
        lines.Add($"PointSize|value={viewModel.PointSize.ToString("F1", CultureInfo.InvariantCulture)}");
        lines.Add($"ColorMode|mode={CleanContractText(viewModel.SelectedColorMode)}");
        lines.Add($"DisplaySettings|sourceId={displaySettings.Source}|activeSource={CleanContractText(viewModel.Display.ActiveSource)}|geometryStyleId={displaySettings.GeometryStyle}|geometryStyle={CleanContractText(viewModel.Display.EffectiveGeometryStyle)}|geometrySelectable={viewModel.Display.CanSelectGeometryStyle}|availableGeometry={CleanContractText(string.Join(",", viewModel.Display.AvailableGeometryStyles))}|colorMapId={displaySettings.ColorMap}|colorMap={CleanContractText(viewModel.Display.EffectiveColorMap)}|colorSelectable={viewModel.Display.CanSelectColorMap}|availableColorMaps={CleanContractText(string.Join(",", viewModel.Display.AvailableColorMaps))}|fallbackApplied={viewModel.Display.FallbackApplied}|fallback={CleanContractText(viewModel.Display.FallbackSummary)}|displayOnly={displaySettings.IsDisplayOnly}|renderBridge=ColorCompatibilitySnapshot|geometryRenderBridge={geometryRenderBridge}");
        lines.Add($"MeasurementOverlay|visible={viewModel.MeasurementVisible}");
        lines.Add($"RenderDensity|mode={viewModel.SelectedRenderDensity}|maxRenderedPoints={viewModel.C3DMaxRenderedPoints}|maxLazSampledPoints={viewModel.LazMaxSampledPoints}|maxImportedMeshTriangles={viewModel.ImportedMeshMaxRenderedTriangles}|maxNominalActualDisplaySamples={viewModel.NominalActualMaxDisplaySamples}|renderedC3DPoints={c3dSample?.Points.Length ?? 0}|sampledLazPoints={lazPointCloud?.SampledPoints.Length ?? 0}|renderedImportedMeshTriangles={GetImportedMeshRenderedTriangleCount()}|summary={viewModel.RenderDensitySummary}");
        lines.Add(c3dSample is null
            ? "C3DMap|loaded=False|displayFrame=NotAvailable|physicalScale=Unverified"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"C3DMap|loaded=True|displayFrame=right-handed-y-up|x=column|y=raw-height|z=row|modelUnit=unitless|rawUnit=raw-height|horizontalSpan={C3DHeightGrid.ViewerHorizontalSpan:R}|horizontalScale={c3dSample.HorizontalScale:R}|heightScale={C3DHeightGrid.ViewerHeightScale:R}|heightCenterRaw={c3dSample.Mean:R}|stride={c3dSample.PointStride}|physicalScale=Unverified"));
        lines.Add(c3dRenderProxyForContract is null
            ? "C3DRenderProxy|loaded=False"
            : $"C3DRenderProxy|loaded=True|points={c3dRenderProxyForContract.Points.Length}|triangles={c3dRenderProxyForContract.TriangleCount}|edges={c3dRenderProxyForContract.EdgeCount}|gridEdges={c3dRenderProxyForContract.GridEdgeCount}|surfaceEdges={c3dRenderProxyForContract.SurfaceEdgeCount}|surfaceEdgeInterval={C3DHeightGridRenderProxy.SurfaceEdgeSampleInterval}|topology=sampled-grid-neighbors|effectiveStyle={displaySettings.GeometryStyle}|renderCache=OpenGLDisplayList|renderCacheReady={c3dDisplayListId != 0}|displayOnly=True|measurementGeometry=SourceCells");
        lines.Add($"PointCloudPerformance|loadMs={FormatContractNumber(viewModel.LazLoadMilliseconds)}|samplePercent={FormatContractNumber(viewModel.LazSamplePercent)}|sampleStride={viewModel.LazSampleStride}|summary={CleanContractText(viewModel.LazSamplingSummary)}");
        lines.Add("ImportedMesh");
        lines.Add(CreateImportedMeshContractLine());
        lines.Add("ImportedPointCloud");
        lines.Add(CreateLazContractLine());
        lines.Add($"ViewerInternalHud|detailsVisible={viewModel.HudDetailsVisible}|importedMeshDetailsVisible={viewModel.ImportedMeshHudDetailsVisible}|lazDetailsVisible={viewModel.LazHudDetailsVisible}");
        var nominalActual = viewModel.NominalActual;
        var nominalActualInput = viewModel.NominalActualInput;
        var nominalActualResult = nominalActual.PreviewResult;
        lines.Add("NominalActualComparison");
        lines.Add($"NominalActualViewModel|type={nameof(NominalActualComparisonViewModel)}|state={nominalActual.State}|inputsReady={nominalActual.InputsReady}|canEdit={nominalActual.CanEdit}|canPreview={nominalActual.CanPreview}|canCancel={nominalActual.CanCancel}|canPublish={nominalActual.CanPublish}|actualVisible={nominalActual.ActualVisible}|nominalVisible={nominalActual.NominalVisible}|hudVisible={nominalActual.HudVisible}|legendVisible={nominalActual.LegendVisible}|distributionVisible={nominalActual.DistributionVisible}|typedResult={nominalActual.PreviewResult is not null}|lowerTolerance={FormatContractNumber(nominalActual.LowerTolerance)}|upperTolerance={FormatContractNumber(nominalActual.UpperTolerance)}|inputFingerprint={CleanContractText(nominalActual.CurrentInputFingerprint)}|previewFingerprint={CleanContractText(nominalActual.CompletedPreviewFingerprint)}|publishedFingerprint={CleanContractText(nominalActual.PublishedPreviewFingerprint)}");
        lines.Add($"NominalActualSummary|state={CleanContractText(nominalActual.StateSummary)}|validation={CleanContractText(nominalActual.ValidationSummary)}|actual={CleanContractText(nominalActual.ActualSourceSummary)}|nominal={CleanContractText(nominalActual.NominalSourceSummary)}|query={CleanContractText(nominalActual.QuerySourceSummary)}|frame={CleanContractText(nominalActual.FrameSummary)}|alignment={CleanContractText(nominalActual.AlignmentSummary)}|result={CleanContractText(nominalActual.ResultSummary)}|evidence={CleanContractText(nominalActual.EvidenceSummary)}");
        lines.Add(nominalActualInput is null
            ? "NominalActualInput|configured=False"
            : $"NominalActualInput|configured=True|step={CleanContractText(nominalActualInput.StepId)}|direction={NominalActualComparisonInput.Direction}|unit={CleanContractText(nominalActualInput.Unit)}|frame={CleanContractText(nominalActualInput.FrameId)}|alignment={CleanContractText(nominalActualInput.AlignmentId)}|actualId={CleanContractText(nominalActualInput.ActualSource.Id)}|actualBytes={nominalActualInput.ActualSource.ByteLength}|actualSha256={nominalActualInput.ActualSource.Sha256}|nominalId={CleanContractText(nominalActualInput.NominalSource.Id)}|nominalBytes={nominalActualInput.NominalSource.ByteLength}|nominalSha256={nominalActualInput.NominalSource.Sha256}|queryId={CleanContractText(nominalActualInput.QuerySource.Id)}|queryBytes={nominalActualInput.QuerySource.ByteLength}|querySha256={nominalActualInput.QuerySource.Sha256}|sourceFingerprint={nominalActualInput.SourceFingerprint}");
        lines.Add(nominalActualResult is null
            ? "NominalActualResult|available=False"
            : $"NominalActualResult|available=True|status={nominalActualResult.Status}|message={CleanContractText(nominalActualResult.Message)}|executionFingerprint={nominalActualResult.Input.ExecutionFingerprint}|points={nominalActualResult.ComparedPointCount}|below={nominalActualResult.BelowLowerToleranceCount}|within={nominalActualResult.WithinToleranceCount}|above={nominalActualResult.AboveUpperToleranceCount}|directSign={nominalActualResult.DirectSignResolvedCount}|robustRecovered={nominalActualResult.RobustSignRecoveredCount}|indexMs={FormatContractNumber(nominalActualResult.IndexElapsed.TotalMilliseconds)}|calculationMs={FormatContractNumber(nominalActualResult.CalculationElapsed.TotalMilliseconds)}|totalMs={FormatContractNumber(nominalActualResult.TotalElapsed.TotalMilliseconds)}|fullQuery=True");
        if (nominalActualResult is not null)
        {
            lines.Add($"NominalActualSignedStatistics|count={nominalActualResult.Signed.Count}|min={FormatPreciseContractNumber(nominalActualResult.Signed.Minimum)}|max={FormatPreciseContractNumber(nominalActualResult.Signed.Maximum)}|mean={FormatPreciseContractNumber(nominalActualResult.Signed.Mean)}|stdPopulation={FormatPreciseContractNumber(nominalActualResult.Signed.StandardDeviationPopulation)}|rms={FormatPreciseContractNumber(nominalActualResult.Signed.RootMeanSquare)}|unit={CleanContractText(nominalActualResult.Input.Unit)}");
            lines.Add($"NominalActualUnsignedStatistics|count={nominalActualResult.Unsigned.Count}|min={FormatPreciseContractNumber(nominalActualResult.Unsigned.Minimum)}|max={FormatPreciseContractNumber(nominalActualResult.Unsigned.Maximum)}|mean={FormatPreciseContractNumber(nominalActualResult.Unsigned.Mean)}|stdPopulation={FormatPreciseContractNumber(nominalActualResult.Unsigned.StandardDeviationPopulation)}|rms={FormatPreciseContractNumber(nominalActualResult.Unsigned.RootMeanSquare)}|unit={CleanContractText(nominalActualResult.Input.Unit)}");
            lines.Add($"NominalActualDisplaySampling|samples={nominalActualResult.DisplaySamples.Count}|stride={nominalActualResult.DisplaySampleStride}|measuredPoints={nominalActualResult.ComparedPointCount}|metricsIndependent=True|colorScale=zero-centred-blue-white-red");
        }
        lines.Add($"NominalActualDisplayDensityState|current={CleanContractText(nominalActual.CurrentDisplayDensity)}|currentBudget={nominalActual.CurrentDisplaySampleBudget}|next={CleanContractText(nominalActual.NextPreviewDisplayDensity)}|nextBudget={nominalActual.NextPreviewDisplaySampleBudget}|changePending={nominalActual.DisplaySamplingChangePending}|explicitPreviewRequired={nominalActual.DisplaySamplingChangePending}|currentSummary={CleanContractText(nominalActual.CurrentDisplaySamplingSummary)}|nextSummary={CleanContractText(nominalActual.NextPreviewSamplingSummary)}");
        lines.Add(nominalActual.SelectedDeviation is not { } selectedDeviation
            ? "NominalActualSelectedDeviation|selected=False"
            : $"NominalActualSelectedDeviation|selected=True|queryIndex={selectedDeviation.QueryPointIndex}|position={FormatVector(selectedDeviation.Position)}|signedDeviation={FormatPreciseContractNumber(selectedDeviation.SignedDeviation)}|unsignedDeviation={FormatPreciseContractNumber(selectedDeviation.UnsignedDeviation)}|nominalTriangleIndex={selectedDeviation.NominalTriangleIndex}|closestNominal={FormatVector(selectedDeviation.ClosestNominalPoint)}|toleranceStatus={CleanContractText(nominalActual.SelectedDeviationToleranceStatus)}|robustSignRecovered={selectedDeviation.RobustSignRecovered}|actualId={CleanContractText(nominalActualResult!.Input.ActualSource.Id)}|queryId={CleanContractText(nominalActualResult.Input.QuerySource.Id)}|unit={CleanContractText(nominalActualResult.Input.Unit)}");
        lines.Add($"ViewerStatus|summary={CleanContractText(viewModel.ViewerStatus)}|smokeExitCode={smokeExitCode}");
        lines.Add($"CoordinateFrame|visible=True|summary={CleanContractText(viewModel.CoordinateFrameSummary)}");
        lines.Add($"Camera|yaw={FormatContractNumber(viewModel.YawDegrees)}|pitch={FormatContractNumber(viewModel.PitchDegrees)}|distance={FormatContractNumber(viewModel.CameraDistance)}|target={FormatVector(GetCameraTarget())}|summary={CleanContractText(viewModel.BottomStatus)}");
        lines.Add(CreatePointerInputRegressionContractLine());
        lines.Add($"SelectionMode|value={viewModel.SelectedSelectionMode}");
        lines.Add($"PickCoordinate|value={CleanContractText(viewModel.PickCoordinate)}");
        lines.Add(CreateImportedMeshPickContractLine());
        lines.Add(CreateImportedMeshSurfaceOverlayContractLine());
        lines.Add(CreateLazPickContractLine());
        lines.Add($"Performance|fps={FormatContractNumber(viewModel.ViewportFps)}|drawMs={FormatContractNumber(viewModel.ViewportDrawMilliseconds)}|summary={CleanContractText(viewModel.PerformanceSummary)}");
        lines.Add($"PerformanceSmoke|configured={smokeRenderFrameCount > 0}|requestedFrames={smokeRenderFrameCount}|completedFrames={smokeRenderFramesCompleted}|finite={double.IsFinite(viewModel.ViewportFps) && double.IsFinite(viewModel.ViewportDrawMilliseconds)}|measurement=SharpGL.DoRender");
        lines.Add("TransformAlignment");
        lines.Add($"C3DTransform|entity={MainWindowViewModel.C3DEntityId}|tx={FormatContractNumber(viewModel.C3DModelTransform.TranslateX)}|ty={FormatContractNumber(viewModel.C3DModelTransform.TranslateY)}|tz={FormatContractNumber(viewModel.C3DModelTransform.TranslateZ)}|rx={FormatContractNumber(viewModel.C3DModelTransform.RotateXDegrees)}|ry={FormatContractNumber(viewModel.C3DModelTransform.RotateYDegrees)}|rz={FormatContractNumber(viewModel.C3DModelTransform.RotateZDegrees)}|scale={FormatContractNumber(viewModel.C3DModelTransform.Scale)}|summary={CleanContractText(viewModel.TransformSummary)}");
        lines.Add($"Alignment|summary={CleanContractText(viewModel.AlignmentSummary)}|mapping={CleanContractText(viewModel.CoordinateMappingSummary)}");
        lines.Add($"AlignmentWorkflow|summary={CleanContractText(viewModel.AlignmentWorkflowSummary)}");
        lines.Add("TwoPointMeasurement");
        lines.Add($"TwoPoint|visible={viewModel.TwoPointMeasurementVisible}|distance={FormatContractNumber(viewModel.TwoPointDistance)}|dx={FormatContractNumber(viewModel.TwoPointDeltaX)}|dy={FormatContractNumber(viewModel.TwoPointDeltaY)}|dz={FormatContractNumber(viewModel.TwoPointDeltaZ)}|heightDeltaRaw={FormatContractNumber(viewModel.TwoPointRawHeightDelta)}|summary={CleanContractText(viewModel.TwoPointMeasurementDetails)}");
        lines.Add($"LAZAcceptance|visible={viewModel.LazSampleVisible}|summary={CleanContractText(viewModel.LazTwoPointAcceptanceSummary)}");
        lines.Add($"LAZAcceptanceParameters|visible={viewModel.LazSampleVisible}|expectedDistance={FormatContractNumber(viewModel.LazTwoPointExpectedDistance)}|distanceTolerance={FormatContractNumber(viewModel.LazTwoPointDistanceTolerance)}|expectedHeightDelta={FormatContractNumber(viewModel.LazTwoPointExpectedHeightDelta)}|heightDeltaTolerance={FormatContractNumber(viewModel.LazTwoPointHeightDeltaTolerance)}");
        lines.Add("PointPairDimensionsInspection");
        var pointPairStep = viewModel.CreatePointPairDimensionsRecipeStep();
        if (pointPairStep is not null)
        {
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                pointPairStep.Id,
                PointPairDimensionsRule.ToolName,
                pointPairStep.SourceEntityId,
                $"{pointPairStep.First.Id},{pointPairStep.Second.Id}",
                pointPairStep.Enabled)));
            lines.Add($"PointPairDimensionsStep|configured=True|id={pointPairStep.Id}|source={pointPairStep.SourceEntityId}|first={pointPairStep.First.Id}@({pointPairStep.First.Row},{pointPairStep.First.Column})|second={pointPairStep.Second.Id}@({pointPairStep.Second.Row},{pointPairStep.Second.Column})|enabled={pointPairStep.Enabled}|expectedDistance={FormatContractNumber(pointPairStep.Acceptance.ExpectedDistance)}|distanceTolerance={FormatContractNumber(pointPairStep.Acceptance.DistanceTolerance)}|expectedWidth={FormatContractNumber(pointPairStep.Acceptance.ExpectedWidth)}|widthTolerance={FormatContractNumber(pointPairStep.Acceptance.WidthTolerance)}|expectedAngle={FormatContractNumber(pointPairStep.Acceptance.ExpectedElevationAngleDegrees)}|angleTolerance={FormatContractNumber(pointPairStep.Acceptance.ElevationAngleToleranceDegrees)}|unit={pointPairStep.Unit}");
        }
        else
        {
            lines.Add($"PointPairDimensionsStep|configured=False|references={viewModel.HasPointPairReferences}");
        }

        lines.Add($"PointPairDimensions|visible={viewModel.PointPairDimensionsVisible}|status={(viewModel.PointPairDimensionsVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|distance={FormatContractNumber(viewModel.PointPairDistance)}|width={FormatContractNumber(viewModel.PointPairWidth)}|angleDegrees={FormatContractNumber(viewModel.PointPairAngleDegrees)}|summary={CleanContractText(viewModel.PointPairDimensionsSummary)}|details={CleanContractText(viewModel.PointPairDimensionsDetails)}");
        lines.Add("GapFlushInspection");
        if (viewModel.GapFlushConfigured)
        {
            var gapFlushStep = viewModel.CreateGapFlushRecipeStep();
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                gapFlushStep.Id,
                GapFlushRule.ToolName,
                gapFlushStep.SourceEntityId,
                $"{gapFlushStep.LeftReferenceId},{gapFlushStep.RightReferenceId}",
                gapFlushStep.Enabled)));
            lines.Add($"GapFlushStep|configured=True|id={gapFlushStep.Id}|source={gapFlushStep.SourceEntityId}|leftReference={gapFlushStep.LeftReferenceId}|rightReference={gapFlushStep.RightReferenceId}|left={FormatContractRegion(gapFlushStep.LeftRegion)}|right={FormatContractRegion(gapFlushStep.RightRegion)}|expectedGap={FormatContractNumber(gapFlushStep.Acceptance.ExpectedGap)}|gapTolerance={FormatContractNumber(gapFlushStep.Acceptance.GapTolerance)}|expectedFlush={FormatContractNumber(gapFlushStep.Acceptance.ExpectedFlush)}|flushTolerance={FormatContractNumber(gapFlushStep.Acceptance.FlushTolerance)}|gapUnit={gapFlushStep.GapUnit}|flushUnit={gapFlushStep.FlushUnit}|maxSampledPoints={gapFlushStep.MaxSampledPoints}|enabled={gapFlushStep.Enabled}");
        }
        else
        {
            lines.Add("GapFlushStep|configured=False");
        }

        lines.Add($"GapFlush|visible={viewModel.GapFlushVisible}|status={(viewModel.GapFlushVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|gap={FormatContractNumber(viewModel.GapFlushGap)}|flush={FormatContractNumber(viewModel.GapFlushFlush)}|modelFlush={FormatContractNumber(viewModel.GapFlushModelFlush)}|leftCount={viewModel.GapFlushLeftPointCount}|rightCount={viewModel.GapFlushRightPointCount}|summary={CleanContractText(viewModel.GapFlushSummary)}|details={CleanContractText(viewModel.GapFlushDetails)}");
        lines.Add("VolumeInspection");
        if (viewModel.VolumeConfigured)
        {
            var volumeStep = viewModel.CreateVolumeRecipeStep();
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(volumeStep.Id, VolumeRule.ToolName, volumeStep.SourceEntityId, $"{volumeStep.ReferenceId},{volumeStep.MeasurementId}", volumeStep.Enabled)));
            lines.Add($"VolumeStep|configured=True|id={volumeStep.Id}|source={volumeStep.SourceEntityId}|reference={volumeStep.ReferenceId}|measurement={volumeStep.MeasurementId}|referenceRegion={FormatContractRegion(volumeStep.ReferenceRegion)}|measurementRegion={FormatContractRegion(volumeStep.MeasurementRegion)}|expectedNet={FormatContractNumber(volumeStep.ExpectedNetVolume)}|tolerance={FormatContractNumber(volumeStep.Tolerance)}|unit={volumeStep.Unit}|maxSampledPoints={volumeStep.MaxSampledPoints}|enabled={volumeStep.Enabled}");
        }
        else lines.Add("VolumeStep|configured=False");
        lines.Add($"Volume|visible={viewModel.VolumeVisible}|status={(viewModel.VolumeVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|above={FormatContractNumber(viewModel.VolumeAbove)}|below={FormatContractNumber(viewModel.VolumeBelow)}|net={FormatContractNumber(viewModel.VolumeNet)}|referenceSamples={viewModel.VolumeReferenceSampleCount}|measurementSamples={viewModel.VolumeMeasurementSampleCount}|summary={CleanContractText(viewModel.VolumeSummary)}|details={CleanContractText(viewModel.VolumeDetails)}");
        lines.Add("CrossSectionInspection");
        if (viewModel.CrossSectionConfigured)
        {
            var crossSectionStep = viewModel.CreateCrossSectionRecipeStep();
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(crossSectionStep.Id, CrossSectionDimensionsRule.ToolName, crossSectionStep.SourceEntityId, crossSectionStep.ReferenceId, crossSectionStep.Enabled)));
            lines.Add($"CrossSectionStep|configured=True|id={crossSectionStep.Id}|source={crossSectionStep.SourceEntityId}|reference={crossSectionStep.ReferenceId}|row={crossSectionStep.Row}|startColumn={crossSectionStep.StartColumn}|endColumn={crossSectionStep.EndColumn}|expectedWidth={FormatContractNumber(crossSectionStep.ExpectedWidth)}|widthTolerance={FormatContractNumber(crossSectionStep.WidthTolerance)}|expectedHeightRange={FormatContractNumber(crossSectionStep.ExpectedHeightRange)}|heightTolerance={FormatContractNumber(crossSectionStep.HeightTolerance)}|widthUnit={crossSectionStep.WidthUnit}|heightUnit={crossSectionStep.HeightUnit}|enabled={crossSectionStep.Enabled}");
        }
        else lines.Add("CrossSectionStep|configured=False");
        lines.Add($"CrossSection|visible={viewModel.CrossSectionVisible}|status={(viewModel.CrossSectionVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|width={FormatContractNumber(viewModel.CrossSectionWidth)}|heightRange={FormatContractNumber(viewModel.CrossSectionHeightRange)}|rawMinimum={FormatContractNumber(viewModel.CrossSectionRawMinimum)}|rawMaximum={FormatContractNumber(viewModel.CrossSectionRawMaximum)}|validSamples={viewModel.CrossSectionValidSampleCount}|summary={CleanContractText(viewModel.CrossSectionSummary)}|details={CleanContractText(viewModel.CrossSectionDetails)}");
        lines.Add("PlaneReferenceMeasurement");
        lines.Add($"PlaneReference|visible={viewModel.PlaneReferenceMeasurementVisible}|fit=least-squares-height-field|sampleBudget={PlaneFitMaxSampledPoints}|samples={viewModel.PlaneReferenceSampleCount}|normal=({FormatContractNumber(viewModel.PlaneReferenceNormalX)},{FormatContractNumber(viewModel.PlaneReferenceNormalY)},{FormatContractNumber(viewModel.PlaneReferenceNormalZ)})|rms={FormatContractNumber(viewModel.PlaneReferenceFitRms)}|signedDistance={FormatContractNumber(viewModel.PlaneReferenceSignedDistance)}|absoluteDistance={FormatContractNumber(viewModel.PlaneReferenceAbsoluteDistance)}|referenceY={FormatContractNumber(viewModel.PlaneReferenceY)}|targetY={FormatContractNumber(viewModel.PlaneReferenceTargetY)}|rawHeightDelta={FormatContractNumber(viewModel.PlaneReferenceRawHeightDelta)}|summary={CleanContractText(viewModel.PlaneReferenceMeasurementDetails)}");
        lines.Add("PlaneFlatnessInspection");
        if (viewModel.PlaneFlatnessConfigured)
        {
            var flatnessStep = viewModel.CreatePlaneFlatnessRecipeStep();
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                flatnessStep.Id,
                PlaneFlatnessRule.ToolName,
                flatnessStep.SourceEntityId,
                flatnessStep.ReferenceId,
                flatnessStep.Enabled)));
            lines.Add($"PlaneFlatnessStep|configured=True|id={flatnessStep.Id}|source={flatnessStep.SourceEntityId}|reference={flatnessStep.ReferenceId}|enabled={flatnessStep.Enabled}|roi={FormatContractRegion(flatnessStep.ReferenceRegion)}|tolerance={FormatContractNumber(flatnessStep.Tolerance)}|unit={flatnessStep.Unit}|maxSampledPoints={flatnessStep.MaxSampledPoints}");
        }
        else
        {
            lines.Add("PlaneFlatnessStep|configured=False");
        }

        lines.Add($"PlaneFlatness|visible={viewModel.PlaneFlatnessVisible}|status={(viewModel.PlaneFlatnessVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|referenceSamples={viewModel.PlaneFlatnessReferenceSampleCount}|measurementSamples={viewModel.PlaneFlatnessMeasurementSampleCount}|minimum={FormatContractNumber(viewModel.PlaneFlatnessMinimumDeviation)}|maximum={FormatContractNumber(viewModel.PlaneFlatnessMaximumDeviation)}|flatness={FormatContractNumber(viewModel.PlaneFlatnessValue)}|rms={FormatContractNumber(viewModel.PlaneFlatnessRms)}|summary={CleanContractText(viewModel.PlaneFlatnessSummary)}");
        lines.Add("RoiStepMeasurement");
        lines.Add($"RoiStep|visible={viewModel.RoiStepMeasurementVisible}|mode={viewModel.RoiStepSelectionMode}|leftCount={viewModel.RoiStepLeftPointCount}|rightCount={viewModel.RoiStepRightPointCount}|leftMeanRaw={FormatContractNumber(viewModel.RoiStepLeftRawMean)}|rightMeanRaw={FormatContractNumber(viewModel.RoiStepRightRawMean)}|heightDeltaRaw={FormatContractNumber(viewModel.RoiStepRawHeightDelta)}|modelDeltaY={FormatContractNumber(viewModel.RoiStepModelHeightDelta)}|summary={CleanContractText(viewModel.RoiStepMeasurementDetails)}|edit={CleanContractText(viewModel.RoiStepEditSummary)}");
        lines.Add("RecipeState");
        if (nominalActualInput is not null)
        {
            lines.Add($"RecipeType|value={NominalActualComparisonRecipe.SupportedRecipeType}|version=1.0");
            lines.Add($"RecipeTolerance|lower={FormatPreciseContractNumber(nominalActual.LowerTolerance)}|upper={FormatPreciseContractNumber(nominalActual.UpperTolerance)}|unit={CleanContractText(nominalActualInput.Unit)}");
            lines.Add($"RecipeSource|actual={CleanContractText(nominalActualInput.ActualSource.Id)}|nominal={CleanContractText(nominalActualInput.NominalSource.Id)}|query={CleanContractText(nominalActualInput.QuerySource.Id)}");
            lines.Add($"RecipeFrame|direction={NominalActualComparisonInput.Direction}|sampling={NominalActualComparisonRecipe.FullQuerySampling}|frame={CleanContractText(nominalActualInput.FrameId)}|alignment={CleanContractText(nominalActualInput.AlignmentId)}");
            lines.Add("RecipeValidation|summary=Validation: OK");
            lines.Add("RecipeParameterSummary|summary=Full-query metrics / display sampling independent");
            lines.Add("RecipeTransform|applicable=False|reason=alignment-contract-owned");
            lines.Add("RecipeRoiStep|configured=False");
        }
        else
        {
            lines.Add($"RecipeTolerance|value={viewModel.RecipePeakTolerance.ToString("F3", CultureInfo.InvariantCulture)}|unit={viewModel.RecipeSourceUnit}");
            lines.Add($"RecipeSource|name={viewModel.RecipeSourceName}|path={viewModel.RecipeSourcePath}");
            lines.Add($"RecipeValidation|summary={CleanContractText(string.IsNullOrWhiteSpace(viewModel.RecipeValidationSummary) ? "Validation: OK" : viewModel.RecipeValidationSummary)}");
            lines.Add($"RecipeParameterSummary|summary={CleanContractText(viewModel.RecipeParameterSummary)}");
            lines.Add($"RecipeTransform|tx={FormatContractNumber(viewModel.C3DModelTransform.TranslateX)}|ty={FormatContractNumber(viewModel.C3DModelTransform.TranslateY)}|tz={FormatContractNumber(viewModel.C3DModelTransform.TranslateZ)}|rx={FormatContractNumber(viewModel.C3DModelTransform.RotateXDegrees)}|ry={FormatContractNumber(viewModel.C3DModelTransform.RotateYDegrees)}|rz={FormatContractNumber(viewModel.C3DModelTransform.RotateZDegrees)}|scale={FormatContractNumber(viewModel.C3DModelTransform.Scale)}");
            lines.Add(CreateCurrentRoiStepRecipe() is { } roiStep
                ? $"RecipeRoiStep|configured=True|mode={roiStep.Mode}|maxSampledPoints={roiStep.MaxSampledPoints}|left={FormatContractRegion(roiStep.Left)}|right={FormatContractRegion(roiStep.Right)}"
                : "RecipeRoiStep|configured=False");
        }
        lines.Add($"RecipeSave|summary={viewModel.RecipeSaveSummary}");
        lines.Add("LinkedViewHeightMap");
        lines.Add($"HeightMap|visible={viewModel.HeightMapVisible}|pixels={viewModel.HeightMapPixelWidth}x{viewModel.HeightMapPixelHeight}|summary={viewModel.HeightMapSummary.Replace('|', '/')}");
        lines.Add($"HeightMapRange|summary={viewModel.HeightMapRange.Replace('|', '/')}");
        lines.Add("LinkedViewProfile");
        lines.Add($"SectionProfile|visible={viewModel.SectionProfileVisible}|samples={viewModel.SectionProfileSampleCount}|summary={viewModel.SectionProfileSummary.Replace('|', '/')}");
        lines.Add($"SectionProfileRange|summary={viewModel.SectionProfileRange.Replace('|', '/')}");
        lines.Add("PublishedResultEntities");
        lines.AddRange(viewModel.ResultEntities.Select(InspectionContractText.FormatResultEntity));
        lines.Add("PublishedMetrics");
        lines.AddRange(viewModel.ResultEntities.SelectMany(entity => entity.Metrics.Select(metric =>
            InspectionContractText.FormatMetric(metric, entity.Id))));
        lines.Add("PublishedOverlays");
        lines.AddRange(viewModel.ResultEntities.SelectMany(entity => entity.Overlays.Select(overlay =>
            InspectionContractText.FormatOverlay(overlay, entity.Id))));

        File.WriteAllLines(path, lines);
    }

    private string FormatC3DPoint(HeightGridPoint point)
    {
        var aligned = TransformC3DPosition(point.Position);
        if (ModelTransformIsIdentity(viewModel.C3DModelTransform))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{CameraMath.FormatPoint(point.Position)} | raw {point.RawValue:F3}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"src {CameraMath.FormatPoint(point.Position)} -> aligned {CameraMath.FormatPoint(aligned)} | raw {point.RawValue:F3}");
    }

    private void SetLazPick(LazPointCloudPoint point, string status)
    {
        selectedLazPoint = point;
        var summary = FormatLazPoint(point);
        viewModel.SelectedEntity = "Public LAZ/LAS Point Cloud";
        viewModel.SelectedSelectionMode = "Point";
        viewModel.PickCoordinate = summary;
        viewModel.SelectionSummary = $"LAZ/LAS point: {summary}";
        viewModel.MeasurementSummary = $"LAZ/LAS point pick: {summary}";
        viewModel.ViewerStatus = status;
    }

    private void SetNominalActualDeviationPick(
        NominalActualDeviationSample sample,
        string status)
    {
        var comparison = viewModel.NominalActual;
        if (!comparison.SelectDeviation(sample))
        {
            viewModel.ViewerStatus = "Nominal/actual pick rejected: Preview sample is stale";
            return;
        }

        selectedImportedMeshPoint = null;
        selectedImportedMeshTriangleIndex = null;
        selectedImportedMeshSurfaceNormal = null;
        selectedLazPoint = null;
        viewModel.SelectedEntity = "Nominal / Actual Deviation Point";
        viewModel.SelectedSelectionMode = "Point";
        viewModel.PickCoordinate = string.Create(
            CultureInfo.InvariantCulture,
            $"query #{sample.QueryPointIndex:N0} | {CameraMath.FormatPoint(sample.Position)} | signed {sample.SignedDeviation:G7} {comparison.PreviewResult!.Input.Unit}");
        viewModel.SelectionSummary = comparison.SelectedDeviationSummary;
        viewModel.MeasurementSummary = comparison.SelectedDeviationDetails;
        viewModel.ViewerStatus = status;
    }

    private void SetImportedMeshPick(
        Vector3 point,
        string status,
        string kind = "mesh point",
        int? triangleIndex = null,
        Vector3? surfaceNormal = null)
    {
        selectedImportedMeshPoint = point;
        selectedImportedMeshPickKind = kind;
        selectedImportedMeshTriangleIndex = triangleIndex;
        selectedImportedMeshSurfaceNormal = surfaceNormal;
        selectedLazPoint = null;
        var summary = FormatImportedMeshPoint(point, kind);
        var format = viewModel.ImportedMeshFormat;
        viewModel.SelectedEntity = format == "GLB" ? "Public GLB Mesh" : $"{format} Mesh";
        viewModel.SelectedSelectionMode = "Point";
        viewModel.PickCoordinate = summary;
        viewModel.SelectionSummary = $"{format} {kind}: {summary}";
        viewModel.MeasurementSummary = $"{format} pick: {summary}";
        viewModel.ViewerStatus = status;
    }

    private Vector3 FindImportedMeshSmokePickTarget()
    {
        var mesh = importedMesh!;
        var center = (mesh.Min + mesh.Max) * 0.5f;
        return mesh.Positions.MinBy(position => Vector3.DistanceSquared(position, center));
    }

    private Vector3 FindImportedMeshSmokeSurfacePickTarget()
    {
        var mesh = importedMesh!;
        var center = (mesh.Min + mesh.Max) * 0.5f;
        var best = FindImportedMeshSmokePickTarget();
        var bestDistanceSquared = Vector3.DistanceSquared(best, center);

        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3)
        {
            var firstIndex = mesh.Indices[i];
            var secondIndex = mesh.Indices[i + 1];
            var thirdIndex = mesh.Indices[i + 2];
            if (!ImportedMeshIndexInRange(mesh, firstIndex) || !ImportedMeshIndexInRange(mesh, secondIndex) || !ImportedMeshIndexInRange(mesh, thirdIndex))
            {
                continue;
            }

            var centroid = (mesh.Positions[firstIndex] + mesh.Positions[secondIndex] + mesh.Positions[thirdIndex]) / 3.0f;
            var distanceSquared = Vector3.DistanceSquared(centroid, center);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                best = centroid;
            }
        }

        return best;
    }

    private (Vector3 First, Vector3 Second) FindImportedMeshSmokeMeasurementPair()
    {
        var mesh = importedMesh!;
        var positions = mesh.Positions;
        var extent = mesh.Max - mesh.Min;
        var axis = extent.X >= extent.Y && extent.X >= extent.Z
            ? 0
            : extent.Y >= extent.Z ? 1 : 2;

        var first = positions[0];
        var second = positions[0];
        var minValue = AxisValue(first, axis);
        var maxValue = minValue;

        foreach (var position in positions)
        {
            var value = AxisValue(position, axis);
            if (value < minValue)
            {
                minValue = value;
                first = position;
            }
            else if (value > maxValue)
            {
                maxValue = value;
                second = position;
            }
        }

        return (first, second);
    }

    private static float AxisValue(Vector3 point, int axis) => axis switch
    {
        0 => point.X,
        1 => point.Y,
        _ => point.Z
    };

    private LazPointCloudPoint FindLazSmokePickTarget()
    {
        var metadata = lazPointCloud!.Metadata;
        var sourceCenter = new Vector3(
            (float)((metadata.MinX + metadata.MaxX) * 0.5),
            (float)((metadata.MinY + metadata.MaxY) * 0.5),
            (float)((metadata.MinZ + metadata.MaxZ) * 0.5));
        var viewerCenter = MapLazPosition(sourceCenter);
        return lazPointCloud.SampledPoints.MinBy(point => Vector3.DistanceSquared(MapLazPosition(point.Position), viewerCenter));
    }

    private string FormatLazPoint(LazPointCloudPoint point)
    {
        var viewer = MapLazPosition(point.Position);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"src {FormatVector(point.Position)} -> viewer {FormatVector(viewer)} | RGB {point.Red},{point.Green},{point.Blue}");
    }

    private static string FormatImportedMeshPoint(Vector3 point, string kind = "mesh point") =>
        string.Create(CultureInfo.InvariantCulture, $"{kind} {FormatVector(point)}");

    private Vector3 TransformC3DPosition(Vector3 sourcePosition) =>
        ApplyModelTransform(sourcePosition, viewModel.C3DModelTransform);

    private static Vector3 ApplyModelTransform(Vector3 sourcePosition, ModelTransform transform)
    {
        var position = sourcePosition * (float)transform.Scale;
        position = Vector3.Transform(position, Matrix4x4.CreateRotationX(ToRadians(transform.RotateXDegrees)));
        position = Vector3.Transform(position, Matrix4x4.CreateRotationY(ToRadians(transform.RotateYDegrees)));
        position = Vector3.Transform(position, Matrix4x4.CreateRotationZ(ToRadians(transform.RotateZDegrees)));
        return position + new Vector3((float)transform.TranslateX, (float)transform.TranslateY, (float)transform.TranslateZ);
    }

    private static float ToRadians(double degrees) => (float)(degrees * Math.PI / 180.0);

    private static bool ModelTransformIsIdentity(ModelTransform transform) =>
        transform.TranslateX == 0.0
        && transform.TranslateY == 0.0
        && transform.TranslateZ == 0.0
        && transform.RotateXDegrees == 0.0
        && transform.RotateYDegrees == 0.0
        && transform.RotateZDegrees == 0.0
        && transform.Scale == 1.0;

    private static string FormatVector(Vector3 point) =>
        string.Create(CultureInfo.InvariantCulture, $"({point.X:F3}, {point.Y:F3}, {point.Z:F3})");

    private string CreateImportedMeshContractLine()
    {
        var format = CleanContractText(viewModel.ImportedMeshFormat);
        if (importedMesh is null)
        {
            return $"{format}|loaded=False|source={CleanContractText(viewModel.GlbSampleSourcePath)}|summary={CleanContractText(viewModel.GlbSampleSummary)}";
        }

        return $"{format}|loaded=True|entity={MainWindowViewModel.GlbEntityId}|visible={viewModel.GlbSampleVisible}|source={CleanContractText(viewModel.GlbSampleSourcePath)}|vertices={importedMesh.Positions.Length}|triangles={importedMesh.TriangleCount}|renderedTriangles={GetImportedMeshRenderedTriangleCount()}|renderTriangleStride={GetImportedMeshRenderTriangleStride()}|vertexColors={importedMesh.VertexColors.Length}|usesVertexColors={importedMesh.HasVertexColors}|texCoords={importedMesh.TextureCoordinates.Length}|hasTexture={importedMesh.HasBaseColorTexture}|textureBytes={importedMesh.BaseColorTexture?.Bytes.Length ?? 0}|textureMime={importedMesh.BaseColorTexture?.MimeType ?? "(none)"}|textureUpload={CleanContractText(importedMeshTextureUploadSummary)}|min={FormatVector(importedMesh.Min)}|max={FormatVector(importedMesh.Max)}|summary={CleanContractText(viewModel.GlbSampleSummary)}";
    }

    private string CreateLazContractLine()
    {
        if (lazSample is null)
        {
            return $"LAZ|loaded=False|source={CleanContractText(viewModel.LazSampleSourcePath)}|summary={CleanContractText(viewModel.LazSampleSummary)}";
        }

        var common = $"LAZ|loaded=True|entity={MainWindowViewModel.LazEntityId}|visible={viewModel.LazSampleVisible}|source={CleanContractText(viewModel.LazSampleSourcePath)}|version={lazSample.Version}|pointFormat={lazSample.PointDataFormat}|rawPointFormat={lazSample.RawPointDataFormat}|compressed={lazSample.IsCompressed}|laszipVlr={lazSample.HasLaszipVlr}|points={lazSample.PointCount}|recordLength={lazSample.PointDataRecordLength}|offset={lazSample.PointDataOffset}|boundsX={FormatContractNumber(lazSample.MinX)}..{FormatContractNumber(lazSample.MaxX)}|boundsY={FormatContractNumber(lazSample.MinY)}..{FormatContractNumber(lazSample.MaxY)}|boundsZ={FormatContractNumber(lazSample.MinZ)}..{FormatContractNumber(lazSample.MaxZ)}";
        return lazPointCloud is null
            ? $"{common}|decoder=metadata-only|summary={CleanContractText(viewModel.LazSampleSummary)}"
            : $"{common}|decoder=points-decoded|decodedPoints={lazPointCloud.DecodedPointCount}|sampledPoints={lazPointCloud.SampledPoints.Length}|sampleStride={lazPointCloud.SampleStride}|rgb={lazPointCloud.HasRgb}|boundsMatch={lazPointCloud.BoundsMatch}|avgRgb={FormatContractNumber(lazPointCloud.AverageRed)},{FormatContractNumber(lazPointCloud.AverageGreen)},{FormatContractNumber(lazPointCloud.AverageBlue)}|summary={CleanContractText(viewModel.LazSampleSummary)}";
    }

    private string CreateImportedMeshPickContractLine()
    {
        var format = CleanContractText(viewModel.ImportedMeshFormat);
        if (selectedImportedMeshPoint is not { } point)
        {
            return $"{format}Pick|selected=False";
        }

        var triangleIndex = selectedImportedMeshTriangleIndex?.ToString(CultureInfo.InvariantCulture) ?? "(none)";
        var normal = selectedImportedMeshSurfaceNormal is { } surfaceNormal ? FormatVector(surfaceNormal) : "(none)";
        return $"{format}Pick|selected=True|kind={CleanContractText(selectedImportedMeshPickKind)}|triangleIndex={triangleIndex}|normal={normal}|position={FormatVector(point)}|summary={CleanContractText(viewModel.PickCoordinate)}";
    }

    private string CreateImportedMeshSurfaceOverlayContractLine()
    {
        var format = CleanContractText(viewModel.ImportedMeshFormat);
        var visible = selectedImportedMeshPoint is not null
            && selectedImportedMeshTriangleIndex is not null
            && selectedImportedMeshSurfaceNormal is not null;
        var triangleIndex = selectedImportedMeshTriangleIndex?.ToString(CultureInfo.InvariantCulture) ?? "(none)";
        var normal = selectedImportedMeshSurfaceNormal is { } surfaceNormal ? FormatVector(surfaceNormal) : "(none)";
        return $"{format}SurfaceOverlay|visible={visible}|triangleIndex={triangleIndex}|normal={normal}|normalScale={FormatContractNumber(visible ? GetImportedMeshSurfaceOverlayScale() : double.NaN)}";
    }

    private string CreateLazPickContractLine()
    {
        if (selectedLazPoint is not { } point)
        {
            return "LAZPick|selected=False";
        }

        return $"LAZPick|selected=True|source={FormatVector(point.Position)}|viewer={FormatVector(MapLazPosition(point.Position))}|rgb={point.Red},{point.Green},{point.Blue}|summary={CleanContractText(viewModel.PickCoordinate)}";
    }

    private static string FormatContractNumber(double value) =>
        double.IsFinite(value) ? value.ToString("F3", CultureInfo.InvariantCulture) : "(pending)";

    private static string FormatPreciseContractNumber(double value) =>
        double.IsFinite(value) ? value.ToString("G17", CultureInfo.InvariantCulture) : "(pending)";

    private static string FormatContractRegion(HeightDeviationRecipeRoiRegion region) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"cx={region.CenterX:F3},cz={region.CenterZ:F3},halfWidth={region.HalfWidth:F3},halfDepth={region.HalfDepth:F3}");

    private static bool IsRecipeRoiEditProperty(string? propertyName) =>
        propertyName is nameof(MainWindowViewModel.RecipeRoiLeftCenterX)
            or nameof(MainWindowViewModel.RecipeRoiLeftCenterZ)
            or nameof(MainWindowViewModel.RecipeRoiLeftHalfWidth)
            or nameof(MainWindowViewModel.RecipeRoiLeftHalfDepth)
            or nameof(MainWindowViewModel.RecipeRoiRightCenterX)
            or nameof(MainWindowViewModel.RecipeRoiRightCenterZ)
            or nameof(MainWindowViewModel.RecipeRoiRightHalfWidth)
            or nameof(MainWindowViewModel.RecipeRoiRightHalfDepth);

    private static string CleanContractText(string value) => value.Replace('|', '/').Replace(Environment.NewLine, " ");

    private static void Quad(OpenGL gl, (double X, double Y, double Z) a, (double X, double Y, double Z) b, (double X, double Y, double Z) c, (double X, double Y, double Z) d)
    {
        gl.Vertex(a.X, a.Y, a.Z);
        gl.Vertex(b.X, b.Y, b.Z);
        gl.Vertex(c.X, c.Y, c.Z);
        gl.Vertex(d.X, d.Y, d.Z);
    }

    private static void Edge(OpenGL gl, (double X, double Y, double Z) a, (double X, double Y, double Z) b)
    {
        gl.Vertex(a.X, a.Y, a.Z);
        gl.Vertex(b.X, b.Y, b.Z);
    }

    private static HeightGridPoint[] CreateGeneratedPointCloud()
    {
        const int columns = 55;
        const int rows = 41;
        var points = new HeightGridPoint[columns * rows];
        var index = 0;

        for (var row = 0; row < rows; row++)
        {
            var z = -2.0f + row * (4.0f / (rows - 1));
            for (var column = 0; column < columns; column++)
            {
                var localX = -2.2f + column * (4.4f / (columns - 1));
                var wave = 0.16 * Math.Sin(localX * 1.35) + 0.10 * Math.Cos(z * 1.8);
                var bump = 0.42 * Math.Exp(-((localX - 0.58) * (localX - 0.58) + (z + 0.32) * (z + 0.32)) / 0.32);
                var dent = -0.24 * Math.Exp(-((localX + 1.05) * (localX + 1.05) + (z - 0.88) * (z - 0.88)) / 0.24);
                var y = -0.70f + (float)(wave + bump + dent);
                var position = new Vector3(localX + 3.2f, y, z);
                var heightScalar = Clamp01((y + 1.05) / 0.86);
                var deviationScalar = Clamp01(Math.Abs(bump + dent) / 0.42);
                points[index++] = new HeightGridPoint(position, heightScalar, deviationScalar, y);
            }
        }

        return points;
    }

    private static (double R, double G, double B) DeviationColor(double value)
    {
        var t = Clamp01(value);
        return (0.12 + 0.88 * t, 0.84 - 0.68 * t, 0.64 - 0.52 * t);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

    private void RenderNow()
    {
        if (Viewport.IsLoaded)
        {
            Viewport.DoRender();
        }
    }

}
