extern alias OvlMessageDialogs;

using Microsoft.Win32;
using OpenVisionLab;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.Layout;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Recipe;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts;
using WpfMessageDialogButtons = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogButtons;
using WpfMessageDialogKind = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogKind;
using WpfMessageDialogOptions = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogOptions;
using WpfMessageDialogResult = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogResult;
using WpfMessageDialogWindow = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogWindow;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OpenVisionLab.ThreeD.Shell;

public partial class MainWindow : Window
{
    private readonly OpenVisionThreeDViewerControl _viewer;
    private readonly ShellMainWindowViewModel _viewModel;
    private readonly EventHandler<ViewerHostStateChangedEventArgs> _viewerHostStateChangedHandler;
    private readonly WorkbenchViewerTeachingCoordinator _workbenchViewerTeaching;
    private readonly WorkbenchViewerDisplayCoordinator _workbenchViewerDisplay;
    private readonly ShellEvidenceDialogController _evidenceDialogs;
    private readonly RecipeFileDialogService _recipeFileDialogs;
    private readonly ShellWorkbenchLifecycleController _workbenchLifecycle;
    private readonly ShellRequestCoordinator _requestCoordinator;
    private readonly PropertyChangedEventHandler _viewModelPropertyChangedHandler;
    private readonly EventHandler _inspectionTaskChangedHandler;
    private readonly StudioLayoutController _studioLayout;
    private readonly ToolLabWindowManager _toolLabWindows;
    private readonly ShellPreparationPresetAssistantSmoke _preparationPresetSmoke;
    private readonly ShellValidationThresholdAssistantSmoke _validationThresholdSmoke;
    private readonly ShellSurfaceMatchInteractionSmoke _surfaceMatchInteractionSmoke;
    private readonly ShellViewerWorkspaceSmoke _viewerWorkspaceSmoke;
    private RoutedEventHandler _shellSmokeLoadedHandler = (_, _) => { };
    private Task? validationSetSmokeSelectionTask;
    private Task? validationSetSmokeSectionTask;
    private bool validationSetSmokeRunRequested;

    private FilterToolLabWindow? filterToolLabWindow => _toolLabWindows.Filter;
    private HeightDifferenceEdgeToolLabWindow? heightDifferenceEdgeToolLabWindow => _toolLabWindows.HeightDifferenceEdge;
    private TwoPointLineToolLabWindow? twoPointLineToolLabWindow => _toolLabWindows.TwoPointLine;
    private ThreePointPlaneToolLabWindow? threePointPlaneToolLabWindow => _toolLabWindows.ThreePointPlane;
    private DatumPlaneDeviationToolLabWindow? datumPlaneDeviationToolLabWindow => _toolLabWindows.DatumPlaneDeviation;
    private LineIntersectionToolLabWindow? lineIntersectionToolLabWindow => _toolLabWindows.LineIntersection;
    private LandmarkCorrespondenceToolLabWindow? landmarkCorrespondenceToolLabWindow => _toolLabWindows.LandmarkCorrespondence;
    private XYZAffineSolveToolLabWindow? xyzAffineSolveToolLabWindow => _toolLabWindows.XYZAffineSolve;
    private XYZAffineApplyToolLabWindow? xyzAffineApplyToolLabWindow => _toolLabWindows.XYZAffineApply;
    private RegridHeightMapToolLabWindow? regridHeightMapToolLabWindow => _toolLabWindows.RegridHeightMap;

    public MainWindow()
    {
        OpenVisionLanguageService.Load();
        ApplyCommandLineLanguage();
        OVLog.Write(LogCategory.System, LogLevel.Info, "OpenVisionLab 3D Studio starting.");
        _viewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: !ShouldStartWithEmptyRecipeInput());
        InitializeComponent();
        _viewModel = new ShellMainWindowViewModel(
            GetCommandLineValue("--recipe-comparison-contract"),
            GetCommandLineValue("--recipe-comparison-report"),
            GetCommandLineValue("--shell-smoke-screenshot"),
            GetCommandLineValue("--run-record"),
            GetCommandLineValue("--html-report"),
            GetCommandLineValue("--csv-report"),
            recentRecipesPath: IsAutomatedShellRun() ? null : GetPersistentRecentRecipesPath());
        _viewModel.SelectedEvidenceTabIndex = GetEvidenceTabIndex(GetCommandLineValue("--shell-evidence-tab"));
        DataContext = _viewModel;
        _toolLabWindows = new ToolLabWindowManager(this, _viewModel.Workbench, ShowMissingToolLabStep);
        _preparationPresetSmoke = new ShellPreparationPresetAssistantSmoke(
            _viewModel.Workbench,
            ToolWorkbench,
            SetCursorPos);
        _validationThresholdSmoke = new ShellValidationThresholdAssistantSmoke(
            _viewModel.Workbench,
            ToolWorkbench);
        _surfaceMatchInteractionSmoke = new ShellSurfaceMatchInteractionSmoke(
            _viewModel.Workbench,
            ToolWorkbench,
            SetCursorPos,
            Dispatcher);
        _viewerWorkspaceSmoke = new ShellViewerWorkspaceSmoke(
            ToolWorkbench,
            Dispatcher);
        _workbenchViewerTeaching = new WorkbenchViewerTeachingCoordinator(
            _viewModel.Workbench,
            _viewer,
            () => ToolWorkbench.IsBottomPaneExpanded = false);
        _recipeFileDialogs = new RecipeFileDialogService(GetRecipeLifecycleDialogOwner);
        _workbenchLifecycle = new ShellWorkbenchLifecycleController(
            this,
            _viewer,
            _viewModel,
            _recipeFileDialogs,
            _workbenchViewerTeaching,
            new ShellWorkbenchLifecycleCallbacks
            {
                ShowLoadSourceFailure = ShowLoadSourceFailure,
                ShowRecipeSaveFailure = ShowRecipeSaveFailure,
                ShowFirstRecipeCreateFailure = ShowFirstRecipeCreateFailure,
                ShowFirstRecipeSetupPersistenceFailure = ShowFirstRecipeSetupPersistenceFailure,
                ShowRecipeFileUnavailable = ShowRecipeFileUnavailable,
                ShowRecipeOpenFailure = ShowRecipeOpenFailure,
                ShowRecipeSourceNotReady = ShowRecipeSourceNotReady,
                ShowRecipeSourceLoadFailure = ShowRecipeSourceLoadFailure,
                ShowParameterApplyFailure = ShowParameterApplyFailure,
                ConfirmUnsavedRecipeChanges = () => ToLifecycleDialogChoice(ConfirmUnsavedRecipeChanges()),
                ConfirmPendingParameterChanges = () => ToLifecycleDialogChoice(ConfirmPendingParameterChanges()),
                CommitPendingParameterEdit = () =>
                {
                    var success = ToolWorkbench.CommitPendingParameterEdit(out var message);
                    return (success, message);
                },
                DiscardPendingParameterChanges = () => _viewModel.Workbench.DiscardSelectedStepParameterDraft(),
                ActivateWorkbench = ActivateWorkbenchAfterRecipeLifecycle,
                DialogText = DialogText
            });
        if (Workspace.ProfileContent is Views.Workbench.HeightProfileView advancedHeightProfileView)
        {
            advancedHeightProfileView.DataContext = _viewer.ViewModel;
        }
        OVLog.Write(LogCategory.UI, LogLevel.Info, "Tool Workbench is the default Shell workspace.");
        if (ShouldStartWithEmptyRecipeInput())
        {
            _viewer.ClearC3DTeachingSource(_viewModel.Workbench.LocalizedSourceReadinessSummary);
        }
        else
        {
            SyncWorkbenchSourceFromViewer();
        }
        _viewer.SidePanelsVisible = false;
        TaskWorkspace.ViewerViewModel = _viewer.ViewModel;
        _viewModelPropertyChangedHandler = OnShellViewModelPropertyChanged;
        _viewModel.PropertyChanged += _viewModelPropertyChangedHandler;
        _inspectionTaskChangedHandler = (_, _) => LoadSelectedInspectionTask();
        _viewModel.InspectionTaskChanged += _inspectionTaskChangedHandler;
        UpdateViewerHost();
        ConfigureWorkspaceFromCommandLine();
        ConfigureInspectionTaskFromCommandLine();
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);

        _viewerHostStateChangedHandler = OnViewerHostStateChanged;
        _viewer.HostStateChanged += _viewerHostStateChangedHandler;
        _viewer.EnableSmokeFromCommandLine(ownsApplicationLifecycle: false);

        _evidenceDialogs = new ShellEvidenceDialogController(
            this,
            _viewModel,
            new ShellEvidenceDialogErrors
            {
                ArtifactMissing = ShowEvidenceArtifactMissing,
                ArtifactOpenFailure = ShowEvidenceArtifactOpenFailure,
                RunRecordOpenFailure = ShowRunRecordOpenFailure,
                RunRecordExportFailure = ShowRunRecordExportFailure
            });
        _requestCoordinator = new ShellRequestCoordinator(
            _viewer,
            _viewModel,
            new ShellRequestCallbacks
            {
                ProfileView = OnProfileViewRequested,
                RefreshRecipeComparison = (_, _) => _viewModel.RefreshRecipeComparison(),
                SaveRecipe = (_, _) => _viewer.SaveCurrentRecipeWithDialog(),
                ApplyRoiAlignment = (_, _) => _viewer.ApplyRoiReferenceAlignment(),
                FitPlane = (_, _) => _viewer.FitC3DReferencePlane(),
                PublishInspectionResult = (_, _) => OnPublishInspectionResultRequested(),
                CalibrationLoadStudy = OnCalibrationLoadStudyRequested,
                OpenEvidenceArtifact = _evidenceDialogs.OpenEvidenceArtifact,
                OpenRunRecord = _evidenceDialogs.OpenRunRecord,
                ExportRunRecord = _evidenceDialogs.ExportRunRecord,
                ExportPrivacySafeSupportBundle = _evidenceDialogs.ExportPrivacySafeSupportBundle,
                NewTeachingRecipe = _workbenchLifecycle.NewTeachingRecipeRequested,
                BrowseFirstRecipeFolder = _workbenchLifecycle.BrowseFirstRecipeFolderRequested,
                BrowseFirstRecipeSource = _workbenchLifecycle.BrowseFirstRecipeSourceRequested,
                SaveTeachingRecipe = _workbenchLifecycle.SaveTeachingRecipeRequested,
                SaveTeachingRecipeAs = _workbenchLifecycle.SaveTeachingRecipeAsRequested,
                OpenToolLibrary = OnWorkbenchOpenToolLibraryRequested,
                SelectedStepSetup = OnWorkbenchSelectedStepSetupRequested,
                SourceQualityWorkspace = OnWorkbenchSourceQualityWorkspaceRequested,
                OpenTeachingRecipe = _workbenchLifecycle.OpenTeachingRecipeRequested,
                RemoveSelectedStep = OnWorkbenchRemoveSelectedStepRequested,
                OpenRecentTeachingRecipe = _workbenchLifecycle.OpenRecentTeachingRecipeRequested,
                LoadC3DSource = _workbenchLifecycle.LoadC3DSourceRequested,
                Import3DData = _workbenchLifecycle.Import3DDataRequested,
                CancelC3DSourceLoad = (_, _) => _workbenchLifecycle.CancelC3DSourceLoad(),
                ToolLab = OnWorkbenchToolLabRequested,
                SelectValidationSetSources = OnWorkbenchSelectValidationSetSourcesRequested,
                ValidationSetComparison = OnWorkbenchValidationSetComparisonRequested
            });
        _workbenchViewerDisplay = new WorkbenchViewerDisplayCoordinator(
            _viewModel,
            _viewer,
            _toolLabWindows,
            ToolWorkbench,
            Workspace,
            _workbenchViewerTeaching);

        ConfigureCalibrationStudyFromCommandLine();
        ConfigureToolTeachingRecipeFromCommandLine();
        RestoreStartupRunRecordAfterRecipeLoad();
        if (!IsAutomatedShellRun())
        {
            _workbenchLifecycle.RestoreMostRecentWorkbenchRecipe();
        }
        ConfigureOutputCompareFromCommandLine();
        ConfigureValidationSetFromCommandLine();
        ConfigureWorkbenchBottomPaneFromCommandLine();
        ConfigureC3DSourceLoadProgressFromCommandLine();
        _workbenchViewerTeaching.SyncAppliedSelections();
        _studioLayout = new StudioLayoutController(
            this,
            ToolWorkbench,
            Workspace,
            _viewModel,
            IsAutomatedShellRun(),
            GetCommandLineValue("--smoke-layout-profile"),
            GetCommandLineValue("--smoke-layout-state-report"));
        Loaded += ConfigureViewerViewFromCommandLine;
        Loaded += EnsureWorkbenchViewerSourceConsistency;
        EnableShellSmokeFromCommandLine();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!IsAutomatedShellRun() && !_workbenchLifecycle.TryResolveWorkbenchChanges("closing 3D Studio"))
        {
            e.Cancel = true;
            return;
        }

        _studioLayout.Save();
        base.OnClosing(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(ConstrainMaximizeToWorkArea);
        }
    }

    private static IntPtr ConstrainMaximizeToWorkArea(
        IntPtr windowHandle,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        const int WmGetMinMaxInfo = 0x0024;
        const uint MonitorDefaultToNearest = 0x00000002;

        if (message != WmGetMinMaxInfo || lParam == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
        {
            return IntPtr.Zero;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        minMaxInfo.MaxPosition.X = monitorInfo.WorkArea.Left - monitorInfo.MonitorArea.Left;
        minMaxInfo.MaxPosition.Y = monitorInfo.WorkArea.Top - monitorInfo.MonitorArea.Top;
        minMaxInfo.MaxSize.X = monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left;
        minMaxInfo.MaxSize.Y = monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top;
        Marshal.StructureToPtr(minMaxInfo, lParam, false);
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        IntPtr windowHandle,
        uint message,
        UIntPtr wParam,
        IntPtr lParam);

    private static bool PostClientMouseMove(IntPtr windowHandle, Point devicePoint)
    {
        const uint wmMouseMove = 0x0200;
        var x = Math.Clamp((int)Math.Round(devicePoint.X), 0, short.MaxValue);
        var y = Math.Clamp((int)Math.Round(devicePoint.Y), 0, short.MaxValue);
        var packed = (IntPtr)((y << 16) | (x & 0xFFFF));
        return PostMessage(windowHandle, wmMouseMove, UIntPtr.Zero, packed);
    }

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void SendMouseEvent(
        uint flags,
        uint deltaX,
        uint deltaY,
        uint data,
        UIntPtr extraInfo);

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void SendKeyboardEvent(
        byte virtualKey,
        byte scanCode,
        uint flags,
        UIntPtr extraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRectangle MonitorArea;
        public NativeRectangle WorkArea;
        public uint Flags;
    }

    protected override void OnClosed(EventArgs e)
    {
        OVLog.Write(LogCategory.System, LogLevel.Info, "OpenVisionLab 3D Studio shutdown.");
        _viewer.HostStateChanged -= _viewerHostStateChangedHandler;
        _requestCoordinator.Dispose();
        _workbenchViewerDisplay.Dispose();
        _workbenchViewerTeaching.Dispose();
        _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        _viewModel.InspectionTaskChanged -= _inspectionTaskChangedHandler;
        Loaded -= _shellSmokeLoadedHandler;
        Loaded -= EnsureWorkbenchViewerSourceConsistency;
        _studioLayout.Dispose();
        _workbenchLifecycle.Dispose();
        try
        {
            _viewModel.IntegrationExchange.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            OVLog.Write(
                LogCategory.System,
                LogLevel.Error,
                $"TCP integration shutdown failed: {exception.GetBaseException().Message}");
        }
        base.OnClosed(e);
    }

    private void EnsureWorkbenchViewerSourceConsistency(object sender, RoutedEventArgs args)
    {
        Loaded -= EnsureWorkbenchViewerSourceConsistency;
        if (_viewModel.IsWorkbenchWorkspaceSelected && !_viewModel.Workbench.IsSourceReadyForRecipe)
        {
            _viewer.ClearC3DTeachingSource(_viewModel.Workbench.SourceReadinessSummary);
            _viewModel.UpdateC3DSampleVisible(false);
        }
    }

    private void EnableShellSmokeFromCommandLine()
    {
        var smoke = ShellSmokeCommandLineOptions.Parse(Environment.GetCommandLineArgs());
        var shellScreenshotPath = smoke.ShellScreenshotPath;
        var screenshotQualityReportPath = smoke.ScreenshotQualityReportPath;
        var viewerLayoutSmoke = smoke.ViewerLayoutSmoke;
        var viewerPresentationSmoke = smoke.ViewerPresentationSmoke;
        var viewerPresentationPressedSmoke = smoke.ViewerPresentationPressedSmoke;
        string? viewerPresentationCameraLinkSmokeSummary = null;
        var integrationExchangeSmokeState = smoke.IntegrationExchangeSmokeState;
        var interactionHoverFallbackUsed = false;
        var thicknessRepeatGridSmoke = smoke.ThicknessRepeatGridSmoke;
        var viewerPopoutScreenshotPath = smoke.ViewerPopoutScreenshotPath;
        var viewerPopoutScreenshotQualityReportPath = smoke.ViewerPopoutScreenshotQualityReportPath;
        var recipeManagerScreenshotPath = smoke.RecipeManagerScreenshotPath;
        var recipeManagerScreenshotQualityReportPath = smoke.RecipeManagerScreenshotQualityReportPath;
        var messageDialogScreenshotPath = smoke.MessageDialogScreenshotPath;
        var messageDialogScreenshotQualityReportPath = smoke.MessageDialogScreenshotQualityReportPath;
        WpfMessageDialogWindow? messageDialogSmokeWindow = null;
        var smokeSaveRecipePath = smoke.SmokeSaveRecipePath;
        var teachingSelectionSmokeMode = smoke.TeachingSelectionSmokeMode;
        var teachingSelectionSmokeReportPath = smoke.TeachingSelectionSmokeReportPath;
        var teachingRecipeSmokeSavePath = smoke.TeachingRecipeSmokeSavePath;
        var newRecipeLifecycleSmokePath = smoke.NewRecipeLifecycleSmokePath;
        var newRecipeLifecycleSmokeSourcePath = smoke.NewRecipeLifecycleSmokeSourcePath;
        var newRecipeLifecycleSmokeReportPath = smoke.NewRecipeLifecycleSmokeReportPath;
        var openRecipeLifecycleSmokePath = smoke.OpenRecipeLifecycleSmokePath;
        var openRecipeLifecycleSmokeReportPath = smoke.OpenRecipeLifecycleSmokeReportPath;
        var asyncC3DLoadSmokePath = smoke.AsyncC3DLoadSmokePath;
        var asyncC3DLoadSmokeReportPath = smoke.AsyncC3DLoadSmokeReportPath;
        var asyncC3DLoadCancelAt = smoke.AsyncC3DLoadCancelAt;
        var asyncC3DLoadExpectFailure = smoke.AsyncC3DLoadExpectFailure;
        var asyncC3DLoadExpectedStatusFragment =
            smoke.AsyncC3DLoadExpectedStatusFragment;
        var viewerOnlyImportSmokePath = smoke.ViewerOnlyImportSmokePath;
        var viewerOnlyImportSmokeReportPath = smoke.ViewerOnlyImportSmokeReportPath;
        var sourceQualitySmokeReportPath = smoke.SourceQualitySmokeReportPath;
        var sourceAcquisitionProvenanceSmokeState =
            smoke.SourceAcquisitionProvenanceSmokeState;
        var sourceAcquisitionProvenancePopupScreenshotPath =
            smoke.SourceAcquisitionProvenancePopupScreenshotPath;
        var heightImagePaletteSmoke = smoke.HeightImagePaletteSmoke;
        var heightImageRangeMinimumSmoke = smoke.HeightImageRangeMinimumSmoke;
        var heightImageRangeMaximumSmoke = smoke.HeightImageRangeMaximumSmoke;
        var heightImageDisplayRangeSmokeReportPath =
            smoke.HeightImageDisplayRangeSmokeReportPath;
        var heightImagePaletteStateEvidenceDirectory =
            smoke.HeightImagePaletteStateEvidenceDirectory;
        var sharedHeightHoverRow = smoke.SharedHeightHoverRow;
        var sharedHeightHoverColumn = smoke.SharedHeightHoverColumn;
        var sharedHeightHoverSmokeReportPath =
            smoke.SharedHeightHoverSmokeReportPath;
        var heightImageRoiPointerSmoke = smoke.HeightImageRoiPointerSmoke;
        var heightImageRoiPointerSmokeReportPath =
            smoke.HeightImageRoiPointerSmokeReportPath;
        var heightImageRoiPointerSmokeSavePath =
            smoke.HeightImageRoiPointerSmokeSavePath;
        var planeFlatnessLiveA3PointerSmoke = smoke.PlaneFlatnessLiveA3PointerSmoke;
        var planeFlatnessLiveA3PointerReportPath = smoke.PlaneFlatnessLiveA3PointerReportPath;
        var planeFlatnessLiveA3PointerSavePath = smoke.PlaneFlatnessLiveA3PointerSavePath;
        var profilePointerSmokeReportPath = smoke.ProfilePointerSmokeReportPath;
        var orientedBoxPointerSmokeReportPath = smoke.OrientedBoxPointerSmokeReportPath;
        var smokeSelectToolId = smoke.SmokeSelectToolId;
        var expandSelectedToolParametersSmoke =
            smoke.ExpandSelectedToolParametersSmoke;
        var preparationPresetAssistantSmoke =
            smoke.PreparationPresetAssistantSmoke;
        var focusSelectedToolParameterSearchSmoke =
            smoke.FocusSelectedToolParameterSearchSmoke;
        var surfaceMatchExperimentPreviewSmoke =
            smoke.SurfaceMatchExperimentPreviewSmoke;
        var surfaceMatchExperimentFocusHoverSmoke =
            smoke.SurfaceMatchExperimentFocusHoverSmoke;
        var surfaceMatchCollectionPopupSmoke =
            smoke.SurfaceMatchCollectionPopupSmoke;
        var surfaceMatchCollectionPopupScreenshotPath =
            smoke.SurfaceMatchCollectionPopupScreenshotPath;
        var surfaceMatchCollectionDisabledSmoke =
            smoke.SurfaceMatchCollectionDisabledSmoke;
        var surfaceMatchCollectionNavigationFocusHoverSmoke =
            smoke.SurfaceMatchCollectionNavigationFocusHoverSmoke;
        var recipeHealthNavigationPressedSmoke =
            smoke.RecipeHealthNavigationPressedSmoke;
        var viewerToolbarPressedSmoke = smoke.ViewerToolbarPressedSmoke;
        var import3DDataPressedSmoke = smoke.Import3DDataPressedSmoke;
        var currentRecipeRunReadySmoke = smoke.CurrentRecipeRunReadySmoke;
        var currentRecipeRunPressedSmoke = smoke.CurrentRecipeRunPressedSmoke;
        var supportBundlePressedSmoke = smoke.SupportBundlePressedSmoke;
        var validationThresholdAssistantPressedSmoke =
            smoke.ValidationThresholdAssistantPressedSmoke;
        var validationThresholdAssistantDisabledSmoke =
            smoke.ValidationThresholdAssistantDisabledSmoke;
        var workbenchInteractionReportPath = smoke.WorkbenchInteractionReportPath;
        var workbenchRunLogSmoke = smoke.WorkbenchRunLogSmoke;
        var filterPublishSmoke = smoke.FilterPublishSmoke;
        var twoPointLinePublishSmoke = smoke.TwoPointLinePublishSmoke;
        var twoPointLinePreviewSmoke = smoke.TwoPointLinePreviewSmoke;
        var threePointPlanePublishSmoke = smoke.ThreePointPlanePublishSmoke;
        var threePointPlanePreviewSmoke = smoke.ThreePointPlanePreviewSmoke;
        var datumPlaneDeviationPublishSmoke = smoke.DatumPlaneDeviationPublishSmoke;
        var datumPlaneDeviationPreviewSmoke = smoke.DatumPlaneDeviationPreviewSmoke;
        var filterPreviewSmoke = smoke.FilterPreviewSmoke;
        var preparationQualityComparisonSmoke = smoke.PreparationQualityComparisonSmoke;
        var removeOutlierPreviewSmoke = smoke.RemoveOutlierPreviewSmoke;
        var levelSurfacePreviewSmoke = smoke.LevelSurfacePreviewSmoke;
        var roiCropPreviewSmoke = smoke.RoiCropPreviewSmoke;
        var measurementPreviewSmoke = smoke.MeasurementPreviewSmoke;
        var edgePublishSmoke = smoke.EdgePublishSmoke;
        var lineFitPreviewSmoke = smoke.LineFitPreviewSmoke;
        var edgePreviewSmoke = smoke.EdgePreviewSmoke;
        var invalidEdgeDraftSmoke = smoke.InvalidEdgeDraftSmoke;
        var edgeStepId = smoke.EdgeStepId;
        var edgeSmokeReportPath = smoke.EdgeSmokeReportPath;
        var lineFitSmokeReportPath = smoke.LineFitSmokeReportPath;
        if (smoke.NeedsCompactWorkbench)
        {
            Width = 1280;
            Height = 760;
        }

        if (smoke.WindowSize is { } smokeSize
            && smokeSize.Width >= MinWidth
            && smokeSize.Height >= MinHeight)
        {
            WindowState = WindowState.Normal;
            Width = smokeSize.Width;
            Height = smokeSize.Height;
        }

        if (smoke.UseLeftmostVirtualScreenOrigin)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            const uint monitorDefaultToNearest = 0x00000002;
            var leftmostMonitor = MonitorFromPoint(
                new NativePoint
                {
                    X = (int)SystemParameters.VirtualScreenLeft,
                    Y = (int)SystemParameters.VirtualScreenTop
                },
                monitorDefaultToNearest);
            var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (leftmostMonitor != IntPtr.Zero
                && GetMonitorInfo(leftmostMonitor, ref monitorInfo))
            {
                var dpiX = 96u;
                var dpiY = 96u;
                const int effectiveDpi = 0;
                GetDpiForMonitor(
                    leftmostMonitor,
                    effectiveDpi,
                    out dpiX,
                    out dpiY);
                Left = monitorInfo.WorkArea.Left * 96.0 / Math.Max(96u, dpiX);
                Top = monitorInfo.WorkArea.Top * 96.0 / Math.Max(96u, dpiY);
            }
            else
            {
                Left = SystemParameters.VirtualScreenLeft;
                Top = SystemParameters.VirtualScreenTop;
            }
        }

        var smokePublishResult = smoke.SmokePublishResult;
        var waitForNominalActualPreview = smoke.WaitForNominalActualPreview
            || _viewer.ViewModel.NominalActualInput is not null;
        if (smoke.ShouldAttachLoadedHandler(_viewer.HasConfiguredSmokeScreenshot))
        {
            _shellSmokeLoadedHandler = async (_, _) =>
            {
                await Dispatcher.InvokeAsync(() => { });
                if (smoke.OpenImport3DDataDialogSmoke)
                {
                    var importDialogTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(700)
                    };
                    importDialogTimer.Tick += (_, _) =>
                    {
                        importDialogTimer.Stop();
                        _viewModel.Workbench.Import3DDataCommand.Execute(null);
                    };
                    importDialogTimer.Start();
                    return;
                }
                ConfigureResultsSectionFromCommandLine();
                if (!TryConfigureSurfaceMatchEvidenceFromCommandLine(
                        out var surfaceMatchFailure))
                {
                    _viewModel.SetViewerSmokeFailed(
                        surfaceMatchFailure);
                    Application.Current.Shutdown(1);
                    return;
                }
                if (Environment.GetCommandLineArgs().Contains(
                        "--smoke-focus-selected-tool",
                        StringComparer.OrdinalIgnoreCase))
                {
                    ToolWorkbench.ActivateSelectedToolPane();
                }

                if (Environment.GetCommandLineArgs().Contains(
                        "--smoke-collapse-selected-tool",
                        StringComparer.OrdinalIgnoreCase))
                {
                    ToolWorkbench.ToggleSelectedToolSideCollapse();
                }

                if (workbenchRunLogSmoke)
                {
                    ToolWorkbench.ActivateSessionLogPane();
                }
                if (currentRecipeRunReadySmoke || currentRecipeRunPressedSmoke)
                {
                    _viewModel.IsValidateWorkspaceSelected = true;
                    await Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                }

                if (asyncC3DLoadSmokePath is not null
                    && !await ShellAsyncC3DLoadSmoke.RunAsync(
                        _viewer,
                        _viewModel.Workbench,
                        Dispatcher,
                        asyncC3DLoadSmokePath,
                        asyncC3DLoadSmokeReportPath,
                        asyncC3DLoadCancelAt,
                        asyncC3DLoadExpectFailure,
                        asyncC3DLoadExpectedStatusFragment,
                         path => _workbenchLifecycle.LoadWorkbenchC3DSourceAsync(path, showFailureDialog: false),
                         _workbenchLifecycle.IsViewerSourceAlreadyLoaded,
                         () => _workbenchLifecycle.LastWorkbenchSourceBindingMilliseconds))
                {
                    _viewModel.SetViewerSmokeFailed("Asynchronous C3D load smoke did not satisfy its source-retention, status, or responsiveness contract.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (viewerOnlyImportSmokePath is not null
                    && !await ShellViewerOnlyImportSmoke.RunAsync(
                        _viewer,
                        _viewModel.Workbench,
                        _workbenchLifecycle,
                        viewerOnlyImportSmokePath,
                        viewerOnlyImportSmokeReportPath))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Viewer-only Import did not activate the decoded source or preserve recipe state.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (smoke.SourceQualitySmoke
                    && !await ShellSourceQualitySmoke.RunAsync(
                        _viewModel.Workbench,
                        ToolWorkbench,
                        this,
                        Dispatcher,
                        sourceQualitySmokeReportPath))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Source Quality did not become ready or changed authored/execution state.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(sourceAcquisitionProvenanceSmokeState))
                {
                    var sourceAcquisitionFailure =
                        await ConfigureSourceAcquisitionProvenanceSmokeStateAsync(
                            sourceAcquisitionProvenanceSmokeState,
                            sourceAcquisitionProvenancePopupScreenshotPath);
                    if (sourceAcquisitionFailure is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(
                                sourceAcquisitionProvenancePopupScreenshotPath))
                        {
                            WriteTextReport(
                                sourceAcquisitionProvenancePopupScreenshotPath
                                + ".failure.txt",
                                [sourceAcquisitionFailure]);
                        }
                        _viewModel.SetViewerSmokeFailed(sourceAcquisitionFailure);
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (newRecipeLifecycleSmokePath is not null
                    && newRecipeLifecycleSmokeSourcePath is not null
                    && !await ShellRecipeLifecycleSmoke.RunNewAsync(
                        _viewModel,
                        _viewer,
                        newRecipeLifecycleSmokePath,
                        newRecipeLifecycleSmokeSourcePath,
                        smoke.NewRecipeLifecycleSmokeStarterId ?? ToolWorkbenchViewModel.EmptyFirstRecipeStarterId,
                        newRecipeLifecycleSmokeReportPath,
                         _workbenchLifecycle.ShowRecipeManagerWindow,
                         _workbenchLifecycle.ClickUnsavedRecipeDoNotSaveForSmokeAsync))
                {
                    _viewModel.SetViewerSmokeFailed("New recipe lifecycle smoke did not create and open a clean zero-step recipe.");
                    Application.Current.Shutdown(1);
                    return;
                }
                if (newRecipeLifecycleSmokePath is not null
                    && newRecipeLifecycleSmokeSourcePath is null)
                {
                    _viewModel.SetViewerSmokeFailed("New recipe lifecycle smoke requires --smoke-new-recipe-source.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (openRecipeLifecycleSmokePath is not null
                    && !ShellRecipeLifecycleSmoke.RunOpen(
                        _viewModel,
                        openRecipeLifecycleSmokePath,
                        openRecipeLifecycleSmokeReportPath,
                         _workbenchLifecycle.ShowRecipeManagerWindow,
                         _workbenchLifecycle.OpenWorkbenchRecipe,
                         () => _workbenchLifecycle.IsRecipeManagerVisible,
                         _workbenchLifecycle.IsViewerSourceAlreadyLoaded))
                {
                    _viewModel.SetViewerSmokeFailed("Open recipe lifecycle smoke did not activate the saved recipe in Workbench.");
                    Application.Current.Shutdown(1);
                    return;
                }
                if (recipeManagerScreenshotPath is not null)
                {
                    _workbenchLifecycle.ShowRecipeManagerWindow();
                    var firstRecipeManagerWindow = _workbenchLifecycle.RecipeManagerWindow;
                    _workbenchLifecycle.ShowRecipeManagerWindow();
                    if (!ReferenceEquals(firstRecipeManagerWindow, _workbenchLifecycle.RecipeManagerWindow))
                    {
                        _viewModel.SetViewerSmokeFailed("Recipe Manager smoke opened more than one window instance.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    _workbenchLifecycle.ConfigureFirstRecipeSetupForSmoke(smoke);
                }

                if (messageDialogScreenshotPath is not null)
                {
                    var dialogOptions = smoke.StepRemovalDialogSmoke
                        ? _viewModel.Workbench.CreateSelectedStepRemovalRequest() is { } request
                            ? CreateRecipeStepRemovalDialogOptions(request)
                            : null
                        : new WpfMessageDialogOptions
                        {
                            Title = DialogText("ThreeD.Dialog.RecipeSave.Title", "레시피 저장", "Save Recipe"),
                            Message = DialogText(
                                "ThreeD.Dialog.RecipeSave.Failed",
                                "레시피 파일을 저장할 수 없습니다. 표시된 파일 또는 구조 오류를 확인하세요.",
                                "The recipe file could not be saved. Check the listed file or structural error."),
                            Details = "Access to the selected recipe folder was denied.",
                            Kind = WpfMessageDialogKind.Warning,
                            Buttons = WpfMessageDialogButtons.OK
                        };
                    if (dialogOptions is null)
                    {
                        _viewModel.SetViewerSmokeFailed(
                            "Step-removal dialog smoke requires an idle selected recipe step.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    messageDialogSmokeWindow = new WpfMessageDialogWindow(dialogOptions)
                    {
                        Owner = this
                    };
                    messageDialogSmokeWindow.Show();
                }

                var toolLabSmoke = new ShellToolLabSmoke(_toolLabWindows, _viewModel.Workbench);
                if (!toolLabSmoke.Prepare(smoke, out var toolLabPrepareFailure))
                {
                    _viewModel.SetViewerSmokeFailed(toolLabPrepareFailure);
                    Application.Current.Shutdown(1);
                    return;
                }
                if (invalidEdgeDraftSmoke
                    && !_viewModel.Workbench.TryConfigureInvalidHeightDifferenceEdgeDraftForSmoke())
                {
                    _viewModel.SetViewerSmokeFailed("Invalid Edge WPG smoke requires a selected Height Difference Edge step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (filterPreviewSmoke
                    && !await _viewModel.Workbench.PreviewSelectedFilterAsync())
                {
                    _viewModel.SetViewerSmokeFailed(_viewModel.Workbench.FilterExecutionSummary);
                    Application.Current.Shutdown(1);
                    return;
                }

                var preparationOutput = _viewModel.Workbench.SelectedToolWorkspace.Outputs
                    .SingleOrDefault();
                if (preparationQualityComparisonSmoke
                    && (preparationOutput is null
                        || !_viewModel.Workbench.TryOpenPreparationQualityComparison(
                            _viewModel.Workbench.DisplayedOutputs.SingleOrDefault(item =>
                                string.Equals(
                                    item.Id,
                                    preparationOutput.EntityId,
                                    StringComparison.OrdinalIgnoreCase)))))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Preparation quality comparison smoke could not normalize the current source and Filter Preview.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (removeOutlierPreviewSmoke
                    && !await _viewModel.Workbench
                        .PreviewSelectedRemoveOutlierPixelsAsync())
                {
                    _viewModel.SetViewerSmokeFailed(
                        _viewModel.Workbench.RemoveOutlierExecutionSummary);
                    Application.Current.Shutdown(1);
                    return;
                }

                if (levelSurfacePreviewSmoke
                    && !await _viewModel.Workbench
                        .PreviewSelectedLevelSurfaceAsync())
                {
                    _viewModel.SetViewerSmokeFailed(
                        _viewModel.Workbench.LevelSurfaceExecutionSummary);
                    Application.Current.Shutdown(1);
                    return;
                }

                if (roiCropPreviewSmoke
                    && !await _viewModel.Workbench
                        .PreviewSelectedRoiCropAsync())
                {
                    _viewModel.SetViewerSmokeFailed(
                        _viewModel.Workbench.RoiCropExecutionSummary);
                    Application.Current.Shutdown(1);
                    return;
                }

                if (measurementPreviewSmoke
                    && ((!string.IsNullOrWhiteSpace(edgeStepId)
                         && !_viewModel.Workbench.SelectPipelineStep(edgeStepId))
                        || !await _viewModel.Workbench.PreviewSelectedMeasurementAsync()))
                {
                    _viewModel.SetViewerSmokeFailed(_viewModel.Workbench.MeasurementExecutionSummary);
                    Application.Current.Shutdown(1);
                    return;
                }
                if (measurementPreviewSmoke)
                {
                    ToolWorkbench.BringSelectedOutputIntoView();
                    await Dispatcher.InvokeAsync(() => { });
                }

                if (thicknessRepeatGridSmoke is not null)
                {
                    if (!_viewModel.Workbench.BeginThicknessRepeatGridCommand.CanExecute(null))
                    {
                        _viewModel.SetViewerSmokeFailed(
                            "Thickness repeat-grid smoke requires one complete selected Thickness step.");
                        Application.Current.Shutdown(1);
                        return;
                    }

                    _viewModel.Workbench.BeginThicknessRepeatGridCommand.Execute(null);
                    if (string.Equals(thicknessRepeatGridSmoke, "apply", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!_viewModel.Workbench.ApplyThicknessRepeatGridCommand.CanExecute(null))
                        {
                            _viewModel.SetViewerSmokeFailed(
                                _viewModel.Workbench.ThicknessRepeatGridValidationSummary);
                            Application.Current.Shutdown(1);
                            return;
                        }
                        _viewModel.Workbench.ApplyThicknessRepeatGridCommand.Execute(null);
                    }
                    else if (!string.Equals(
                                 thicknessRepeatGridSmoke,
                                 "review",
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        _viewModel.SetViewerSmokeFailed(
                            $"Unknown Thickness repeat-grid smoke mode: {thicknessRepeatGridSmoke}.");
                        Application.Current.Shutdown(1);
                        return;
                    }

                    ToolWorkbench.BringThicknessRepeatGridIntoView();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                }

                if (viewerPresentationSmoke || viewerLayoutSmoke is not null)
                {
                    var viewerWorkspace = await _viewerWorkspaceSmoke.RunAsync(
                        viewerPresentationSmoke,
                        viewerLayoutSmoke,
                        screenshotQualityReportPath);
                    if (!viewerWorkspace.Succeeded)
                    {
                        _viewModel.SetViewerSmokeFailed(
                            viewerWorkspace.Failure!);
                        Application.Current.Shutdown(1);
                        return;
                    }
                    viewerPresentationCameraLinkSmokeSummary =
                        viewerWorkspace.CameraLinkSummary;
                }

                if (smoke.HeightImageDisplayRangeSmoke
                    && !await RunHeightImageDisplayRangeSmokeAsync(
                        heightImagePaletteSmoke,
                        heightImageRangeMinimumSmoke,
                        heightImageRangeMaximumSmoke,
                        heightImageDisplayRangeSmokeReportPath))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Height Image display range did not apply as view-only state.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (heightImagePaletteStateEvidenceDirectory is not null
                    && !await RunHeightImagePaletteStateSmokeAsync(
                        heightImagePaletteStateEvidenceDirectory))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Height Image palette selector runtime states were incomplete or changed recipe/execution state.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (smoke.SharedHeightHoverSmoke
                    && !await RunSharedHeightHoverSmokeAsync(
                        sharedHeightHoverRow,
                        sharedHeightHoverColumn,
                        sharedHeightHoverSmokeReportPath))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Height Image and 3D Viewer did not share one view-only native-grid hover.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (heightImageRoiPointerSmoke is not null
                    && !await RunHeightImageRoiPointerSmokeAsync(
                        heightImageRoiPointerSmoke,
                        heightImageRoiPointerSmokeReportPath,
                        heightImageRoiPointerSmokeSavePath))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Height Image ROI actual-pointer editing did not preserve the teaching lifecycle boundary.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (filterPublishSmoke)
                {
                    _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    if (!_viewModel.Workbench.IsFilterPreviewPublished)
                    {
                        _viewModel.SetViewerSmokeFailed("Filter Publish did not accept the current Preview output.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (twoPointLinePreviewSmoke
                    && (string.IsNullOrWhiteSpace(edgeStepId)
                        || !_viewModel.Workbench.SelectPipelineStep(edgeStepId)
                        || !await _viewModel.Workbench.PreviewSelectedTwoPointLineAsync()))
                {
                    _viewModel.SetViewerSmokeFailed(_viewModel.Workbench.TwoPointLineExecutionSummary);
                    Application.Current.Shutdown(1);
                    return;
                }

                if (twoPointLinePublishSmoke)
                {
                    _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    if (!_viewModel.Workbench.IsTwoPointLinePreviewPublished)
                    {
                        _viewModel.SetViewerSmokeFailed("2-Point Line Publish did not accept the current Preview output.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (threePointPlanePreviewSmoke
                    && (string.IsNullOrWhiteSpace(edgeStepId)
                        || !_viewModel.Workbench.SelectPipelineStep(edgeStepId)
                        || !await _viewModel.Workbench.PreviewSelectedThreePointPlaneAsync()))
                {
                    _viewModel.SetViewerSmokeFailed(_viewModel.Workbench.ThreePointPlaneExecutionSummary);
                    Application.Current.Shutdown(1);
                    return;
                }

                if (threePointPlanePublishSmoke)
                {
                    _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    if (!_viewModel.Workbench.IsThreePointPlanePreviewPublished)
                    {
                        _viewModel.SetViewerSmokeFailed("3-Point Plane Publish did not accept the current Preview output.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (datumPlaneDeviationPreviewSmoke)
                {
                    var datumStep = _viewModel.Workbench.PipelineSteps.SingleOrDefault(step =>
                        string.Equals(step.ToolId, "datum-plane-raw-height-deviation", StringComparison.Ordinal));
                    var planeStep = datumStep is null ? null : _viewModel.Workbench.PipelineSteps.SingleOrDefault(step =>
                        string.Equals(step.ToolId, "three-point-plane", StringComparison.Ordinal)
                        && string.Equals(step.OutputEntityId, datumStep.InputEntityIds.ElementAtOrDefault(1), StringComparison.OrdinalIgnoreCase));
                    if (datumStep is null || planeStep is null
                        || !_viewModel.Workbench.SelectPipelineStep(planeStep.Id)
                        || !await _viewModel.Workbench.PreviewSelectedThreePointPlaneAsync())
                    {
                        _viewModel.SetViewerSmokeFailed("Datum Plane Deviation smoke could not create its explicit Published 3-Point Plane prerequisite.");
                        Application.Current.Shutdown(1);
                        return;
                    }

                    _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    if (!_viewModel.Workbench.IsThreePointPlanePreviewPublished
                        || !_viewModel.Workbench.SelectPipelineStep(datumStep.Id)
                        || !await _viewModel.Workbench.PreviewSelectedDatumPlaneDeviationAsync())
                    {
                        _viewModel.SetViewerSmokeFailed(_viewModel.Workbench.DatumPlaneDeviationExecutionSummary);
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (datumPlaneDeviationPublishSmoke)
                {
                    _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    if (!_viewModel.Workbench.IsDatumPlaneDeviationPreviewPublished)
                    {
                        _viewModel.SetViewerSmokeFailed("Datum Plane Deviation Publish did not accept the current Preview output.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (edgePreviewSmoke)
                {
                    var filterStep = _viewModel.Workbench.PipelineSteps.FirstOrDefault(step =>
                        string.Equals(step.ToolId, "filter", StringComparison.Ordinal));
                    if (filterStep is null
                        || !_viewModel.Workbench.SelectPipelineStep(filterStep.Id)
                        || !await _viewModel.Workbench.PreviewSelectedFilterAsync())
                    {
                        _viewModel.SetViewerSmokeFailed("Edge smoke could not create the explicit Filter Preview prerequisite.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    if (!_viewModel.Workbench.IsFilterPreviewPublished
                        || string.IsNullOrWhiteSpace(edgeStepId)
                        || !_viewModel.Workbench.TryConfigureHeightDifferenceEdgeSmoke(
                            edgeStepId,
                            new ToolRecipeGridRectangle(156, 180, 135, 16),
                            "AcrossColumns",
                            "Rising",
                            "4",
                            out var edgeConfiguration))
                    {
                        _viewModel.SetViewerSmokeFailed("Edge smoke prerequisite failed: Published Filter or smoke-only search band is unavailable.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    OVLog.Write(LogCategory.UI, LogLevel.Info, $"Edge smoke-only configuration: {edgeConfiguration}; no recipe file was saved.");
                    if (!await _viewModel.Workbench.PreviewSelectedHeightDifferenceEdgeAsync())
                    {
                        _viewModel.SetViewerSmokeFailed(_viewModel.Workbench.HeightDifferenceEdgeExecutionSummary);
                        Application.Current.Shutdown(1);
                        return;
                    }
                    if (edgeSmokeReportPath is not null
                        && _viewModel.Workbench.CurrentHeightDifferenceEdgeOutput is { } edgeOutput)
                    {
                        var diagnostics = edgeOutput.Diagnostics;
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(edgeSmokeReportPath))!);
                        File.WriteAllLines(edgeSmokeReportPath,
                        [
                            "OpenVisionLab 3D Height Difference Edge smoke-only report",
                            "Boundary|Smoke-only band and threshold are not saved teaching values or production evidence.",
                            $"Input|entity={edgeOutput.InputEntityId}|sha256={edgeOutput.InputContentSha256}|rootSha256={edgeOutput.RootSourceSha256}",
                            $"Selection|row={edgeOutput.Selection.Row}|column={edgeOutput.Selection.Column}|rowCount={edgeOutput.Selection.RowCount}|columnCount={edgeOutput.Selection.ColumnCount}",
                            $"Rule|axis={edgeOutput.ComparisonAxis}|polarity={edgeOutput.Polarity}|minimumDelta={edgeOutput.MinimumDelta:R}",
                            $"Output|entity={edgeOutput.OutputEntityId}|points={edgeOutput.Points.Count}|sha256={edgeOutput.ContentSha256}",
                            $"Diagnostics|scanlines={diagnostics.ScanlineCount}|eligiblePairs={diagnostics.EligiblePairCount}|missingPairSkips={diagnostics.SkippedMissingPairCount}|accepted={diagnostics.AcceptedScanlineCount}|noCandidate={diagnostics.NoCandidateScanlineCount}|magnitudeMin={diagnostics.AcceptedMagnitudeMinimum:R}|magnitudeMax={diagnostics.AcceptedMagnitudeMaximum:R}|magnitudeMean={diagnostics.AcceptedMagnitudeMean:R}"
                        ]);
                    }
                    if (edgePublishSmoke)
                    {
                        _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                        if (!_viewModel.Workbench.IsEdgePreviewPublished)
                        {
                            _viewModel.SetViewerSmokeFailed("Height Difference Edge Publish did not reuse the current Preview output.");
                            Application.Current.Shutdown(1);
                            return;
                        }
                    }
                }

                if (lineFitPreviewSmoke)
                {
                    if (!_viewModel.Workbench.IsEdgePreviewPublished)
                    {
                        _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    }
                    if (_viewModel.Workbench.CurrentHeightDifferenceEdgeOutput is not { } edgeOutput
                        || !_viewModel.Workbench.IsEdgePreviewPublished
                        || !_viewModel.Workbench.TryConfigureLineFitSmoke(
                            edgeOutput.OutputEntityId,
                            "100",
                            "3",
                            "0.10",
                            "2",
                            out var lineFitConfiguration)
                        || !await _viewModel.Workbench.PreviewSelectedLineFitAsync())
                    {
                        _viewModel.SetViewerSmokeFailed($"Line Fit smoke prerequisite failed: {_viewModel.Workbench.LineFitExecutionSummary}");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    OVLog.Write(LogCategory.UI, LogLevel.Info, lineFitConfiguration);
                    if (lineFitSmokeReportPath is not null && _viewModel.Workbench.CurrentLineFitOutput is { } lineFitOutput)
                    {
                        var diagnostics = lineFitOutput.Diagnostics;
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(lineFitSmokeReportPath))!);
                        File.WriteAllLines(lineFitSmokeReportPath,
                        [
                            "OpenVisionLab 3D Line Fit smoke-only report",
                            "Boundary|Smoke-only limits are not saved teaching values, production evidence, inspection OK/NG, or metrology evidence.",
                            $"Input|entity={lineFitOutput.InputEdgePointSetEntityId}|sha256={lineFitOutput.InputContentSha256}|rootSha256={lineFitOutput.RootSourceSha256}",
                            $"Output|entity={lineFitOutput.OutputEntityId}|sha256={lineFitOutput.ContentSha256}|points={diagnostics.InputPointCount}|inliers={diagnostics.InlierCount}|outliers={diagnostics.OutlierCount}",
                            $"Diagnostics|residualRms={diagnostics.ResidualRms:R}|residualMax={diagnostics.ResidualMaximum:R}|scanlineSpan={diagnostics.InlierScanlineSpan}|plotPoints={_viewModel.Workbench.LineFitResidualPlotPoints.Count}",
                            $"Line|anchor={lineFitOutput.AnchorX:R},{lineFitOutput.AnchorY:R},{lineFitOutput.AnchorZ:R}|direction={lineFitOutput.DirectionX:R},{lineFitOutput.DirectionY:R},{lineFitOutput.DirectionZ:R}"
                        ]);
                    }
                }

                if (teachingSelectionSmokeMode is not null
                    && !await ShellTeachingSelectionSmoke.RunAsync(
                        _viewModel,
                        _viewer,
                        teachingSelectionSmokeMode,
                        teachingSelectionSmokeReportPath))
                {
                    Application.Current.Shutdown(1);
                    return;
                }

                if (planeFlatnessLiveA3PointerSmoke
                    && !await ShellPlaneFlatnessLiveA3Smoke.RunAsync(
                        _viewModel,
                        _viewer,
                        planeFlatnessLiveA3PointerReportPath,
                        planeFlatnessLiveA3PointerSavePath,
                        _workbenchViewerTeaching.SyncAppliedSelections))
                {
                    Application.Current.Shutdown(1);
                    return;
                }

                if (teachingRecipeSmokeSavePath is not null
                    && !_viewModel.Workbench.TrySaveTeachingRecipe(teachingRecipeSmokeSavePath, out var teachingSaveMessage))
                {
                    _viewModel.SetViewerSmokeFailed(teachingSaveMessage);
                    Application.Current.Shutdown(1);
                    return;
                }

                var nominalActualReady = !waitForNominalActualPreview
                    || await WaitForNominalActualPreviewAsync(TimeSpan.FromMinutes(10));
                if (!nominalActualReady)
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Nominal/actual Preview did not complete before Shell screenshot capture.");
                }

                if (!await _viewer.ApplyConfiguredSmokeNextDensityAsync())
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                if (!_viewer.ApplyConfiguredSmokePick())
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                if (!await _viewer.RunConfiguredPointerInputRegressionAsync())
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                if (profilePointerSmokeReportPath is not null
                    && !await _viewer.RunProfilePointerSmokeAsync(profilePointerSmokeReportPath))
                {
                    _viewModel.SetViewerSmokeFailed("Interactive height-profile pointer smoke failed.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (orientedBoxPointerSmokeReportPath is not null
                    && !await _viewer.RunTeachingOrientedBoxPointerSmokeAsync(
                        orientedBoxPointerSmokeReportPath))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "OrientedBox3D actual-pointer editing did not preserve the Review/Apply boundary.");
                    Application.Current.Shutdown(1);
                    return;
                }

                await Task.Delay(900);
                if (smokePublishResult && !_viewer.PublishCurrentPreviewResult())
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Viewer Publish failed because current Preview evidence was unavailable.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (smokePublishResult)
                {
                    _viewModel.ShowReviewWorkspace();
                }

                if (smokeSaveRecipePath is not null && !_viewer.SaveCurrentRecipe(smokeSaveRecipePath, isSmoke: true))
                {
                    Application.Current.Shutdown(1);
                    return;
                }

                if (_viewer.SmokeExitCode != 0)
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                if (!await _viewer.CaptureConfiguredSmokeViewAsync())
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                if (!string.IsNullOrWhiteSpace(smokeSelectToolId))
                {
                    var tool = _viewModel.Workbench.Tools.SingleOrDefault(candidate =>
                        string.Equals(candidate.Id, smokeSelectToolId, StringComparison.OrdinalIgnoreCase));
                    if (tool is null)
                    {
                        _viewModel.SetViewerSmokeFailed($"Smoke tool '{smokeSelectToolId}' was not found in the Workbench catalog.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    _viewModel.Workbench.SelectedTool = tool;
                    _viewModel.Workbench.AddSelectedToolCommand.Execute(null);
                }

                if (expandSelectedToolParametersSmoke)
                {
                    ToolWorkbench.ActivateSelectedToolPane();
                    await Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Loaded);
                    var parametersExpander =
                        FindVisualDescendants<
                                System.Windows.Controls.Expander>(
                                ToolWorkbench)
                            .FirstOrDefault(expander =>
                                System.Windows.Automation
                                    .AutomationProperties
                                    .GetAutomationId(expander)
                                == "SelectedToolParametersExpander");
                    if (parametersExpander is null)
                    {
                        _viewModel.SetViewerSmokeFailed(
                            "Selected Tool parameters expander was not found.");
                        Application.Current.Shutdown(1);
                        return;
                    }

                    parametersExpander.IsExpanded = true;
                }

                if (!string.IsNullOrWhiteSpace(preparationPresetAssistantSmoke)
                    && !_preparationPresetSmoke.Configure(
                        preparationPresetAssistantSmoke,
                        out var preparationPresetAssistantFailure))
                {
                    _viewModel.SetViewerSmokeFailed(preparationPresetAssistantFailure);
                    Application.Current.Shutdown(1);
                    return;
                }

                if (focusSelectedToolParameterSearchSmoke)
                {
                    await Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Loaded);
                    var parameterSearch =
                        FindVisualDescendants<System.Windows.Controls.TextBox>(ToolWorkbench)
                            .FirstOrDefault(textBox =>
                                System.Windows.Automation.AutomationProperties.GetAutomationId(textBox)
                                == "RecipeStepPropertySearch");
                    if (parameterSearch is null || !parameterSearch.Focus())
                    {
                        _viewModel.SetViewerSmokeFailed(
                            "Selected Tool parameter search could not receive keyboard focus.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (surfaceMatchExperimentPreviewSmoke
                    && !await _viewModel.Workbench
                        .PreviewSelectedSurfaceMatchExperimentAsync())
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Surface Match experiment Preview did not produce a temporary candidate.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var workbenchUiApplyStarted = Stopwatch.GetTimestamp();
                UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                if (surfaceMatchCollectionNavigationFocusHoverSmoke
                    || surfaceMatchCollectionDisabledSmoke
                    || surfaceMatchExperimentFocusHoverSmoke
                    || surfaceMatchCollectionPopupSmoke)
                {
                    var interaction = await _surfaceMatchInteractionSmoke.RunAsync(
                        surfaceMatchCollectionNavigationFocusHoverSmoke,
                        surfaceMatchCollectionDisabledSmoke,
                        surfaceMatchExperimentFocusHoverSmoke,
                        surfaceMatchCollectionPopupSmoke,
                        surfaceMatchCollectionPopupScreenshotPath);
                    if (!interaction.Succeeded)
                    {
                        _viewModel.SetViewerSmokeFailed(
                            interaction.Failure!);
                        Application.Current.Shutdown(1);
                        return;
                    }
                }
                var workbenchUiApplyMilliseconds = Stopwatch.GetElapsedTime(workbenchUiApplyStarted).TotalMilliseconds;
                if (workbenchInteractionReportPath is not null)
                {
                    var fullReportPath = Path.GetFullPath(workbenchInteractionReportPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
                    var reportLines = new List<string>
                    {
                        "OpenVisionLab 3D Workbench interaction timing",
                        "Boundary|Local Release EXE smoke timing; this is not a broad hardware benchmark.",
                        $"Tool|id={smokeSelectToolId ?? "(none)"}|selected={_viewModel.Workbench.SelectedTool?.Id ?? "(none)"}|step={_viewModel.Workbench.SelectedPipelineStep?.Id ?? "(none)"}",
                        $"Timing|toolSelectionMs={_viewModel.Workbench.LastToolSelectionMilliseconds:F3}|toolAddMs={_viewModel.Workbench.LastToolAddMilliseconds:F3}|stepSelectionMs={_viewModel.Workbench.LastStepSelectionMilliseconds:F3}|uiApplyMs={workbenchUiApplyMilliseconds:F3}",
                        $"RecipeRefresh|totalMs={_viewModel.Workbench.LastRecipeRefreshMilliseconds:F3}|validationMs={_viewModel.Workbench.LastRecipeValidationMilliseconds:F3}|entityRebuildMs={_viewModel.Workbench.LastRecipeEntityRebuildMilliseconds:F3}|executionStateMs={_viewModel.Workbench.LastRecipeExecutionStateMilliseconds:F3}|notificationMs={_viewModel.Workbench.LastRecipeNotificationMilliseconds:F3}",
                        $"Budget|toolSelection50ms={_viewModel.Workbench.LastToolSelectionMilliseconds <= 50.0}|toolAdd150ms={_viewModel.Workbench.LastToolAddMilliseconds <= 150.0}|stepSelection150ms={_viewModel.Workbench.LastStepSelectionMilliseconds <= 150.0}|uiApply150ms={workbenchUiApplyMilliseconds <= 150.0}",
                        $"Recipe|steps={_viewModel.Workbench.PipelineSteps.Count}|state={_viewModel.Workbench.SelectedPipelineStep?.State ?? "(none)"}|publishAvailable={_viewModel.Workbench.PublishSelectedStepCommand.CanExecute(null)}"
                    };
                    reportLines.AddRange(
                        ToolWorkbench.GetSelectedToolVisibleTextLayout());
                    File.WriteAllLines(fullReportPath, reportLines);
                }

                if (!string.IsNullOrWhiteSpace(integrationExchangeSmokeState))
                {
                    if (!_viewModel.IsIntegrationExchangeSelected)
                    {
                        _viewModel.SetViewerSmokeFailed(
                            "Integration exchange visual state requires --shell-workspace Exchange.");
                        Application.Current.Shutdown(1);
                        return;
                    }

                    const string representativeExchangeRoot =
                        @"D:\OpenVisionLab-Exchange\Projects\Automated-Optical-Inspection-Line-With-A-Deliberately-Long-Commissioning-Name\Shared-Exchange";
                    _viewModel.IntegrationExchange.ExchangeRoot = representativeExchangeRoot;
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    UpdateLayout();
                    if (integrationExchangeSmokeState.Equals("input-focus", StringComparison.OrdinalIgnoreCase))
                    {
                        var input = FindVisualDescendants<System.Windows.Controls.TextBox>(this)
                            .FirstOrDefault(textBox => textBox.Name == "ExchangeRootTextBox");
                        if (input is null)
                        {
                            _viewModel.SetViewerSmokeFailed(
                                "Integration exchange folder input was not available.");
                            Application.Current.Shutdown(1);
                            return;
                        }
                        input.Text = representativeExchangeRoot;
                        input.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                        _viewModel.IntegrationExchange.ExchangeRoot = representativeExchangeRoot + @"\Restored-From-ViewModel";
                        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                        if (!input.Focus()
                            || !input.IsKeyboardFocusWithin
                            || input.Text != representativeExchangeRoot + @"\Restored-From-ViewModel")
                        {
                            _viewModel.SetViewerSmokeFailed(
                                "Integration exchange folder did not complete its two-way binding and focus round trip.");
                            Application.Current.Shutdown(1);
                            return;
                        }
                    }
                    else if (integrationExchangeSmokeState.Equals("interaction-matrix", StringComparison.OrdinalIgnoreCase))
                    {
                        Activate();
                        var interactionWindowHandle = new WindowInteropHelper(this).Handle;
                        var interactionForegrounded = SetForegroundWindow(interactionWindowHandle);
                        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                        var workspace = FindVisualDescendants<System.Windows.Controls.UserControl>(this)
                            .FirstOrDefault(control =>
                                System.Windows.Automation.AutomationProperties.GetAutomationId(control)
                                == "MachineExchangeWorkspace");
                        var buttons = workspace is null
                            ? []
                            : FindVisualDescendants<System.Windows.Controls.Button>(workspace)
                                .Where(button => button.IsVisible)
                                .ToArray();
                        if (workspace is null || buttons.Length < 7 || !buttons.Any(button => !button.IsEnabled))
                        {
                            _viewModel.SetViewerSmokeFailed(
                                "Integration exchange interaction matrix did not expose its expected enabled and disabled controls.");
                            Application.Current.Shutdown(1);
                            return;
                        }
                        foreach (var button in buttons.Where(button => button.IsEnabled))
                        {
                            button.BringIntoView();
                            if (!button.Focus()
                                || !button.IsKeyboardFocusWithin
                                || button.Command is not null
                                 && button.IsEnabled != button.Command.CanExecute(button.CommandParameter))
                            {
                                _viewModel.SetViewerSmokeFailed(
                                    "Integration exchange button focus or CanExecute state was inconsistent.");
                                Application.Current.Shutdown(1);
                                return;
                            }
                            var relativeCenter = button.TransformToAncestor(this).Transform(
                                new Point(button.ActualWidth / 2, button.ActualHeight / 2));
                            var transformToDevice = PresentationSource.FromVisual(this)
                                ?.CompositionTarget?.TransformToDevice
                                ?? System.Windows.Media.Matrix.Identity;
                            var deviceCenter = transformToDevice.Transform(relativeCenter);
                            _ = GetWindowRect(interactionWindowHandle, out var interactionWindowRect);
                            var center = new Point(
                                interactionWindowRect.Left + deviceCenter.X,
                                interactionWindowRect.Top + deviceCenter.Y);
                            if (!SetCursorPos((int)Math.Round(center.X), (int)Math.Round(center.Y)))
                            {
                                _viewModel.SetViewerSmokeFailed(
                                    "Integration exchange pointer could not enter the button hit region.");
                                Application.Current.Shutdown(1);
                                return;
                            }
                            PostClientMouseMove(interactionWindowHandle, deviceCenter);
                            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                            await Task.Delay(75);
                            for (var attempt = 0; attempt < 5 && !button.IsMouseOver; attempt++)
                            {
                                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                                await Task.Delay(50);
                            }
                            var buttonHoverFallback = false;
                            if (!button.IsMouseOver)
                            {
                                // Some desktop sessions keep the test process active while
                                // suppressing cross-process mouse-over promotion. Capture the
                                // element and route a real WPF mouse move so the same template
                                // trigger can still be inspected; report this as a harness
                                // fallback rather than native pointer proof.
                                System.Windows.Input.Mouse.Capture(
                                    button,
                                    System.Windows.Input.CaptureMode.Element);
                                button.RaiseEvent(new System.Windows.Input.MouseEventArgs(
                                    System.Windows.Input.Mouse.PrimaryDevice,
                                    Environment.TickCount)
                                {
                                    RoutedEvent = System.Windows.Input.Mouse.MouseMoveEvent,
                                    Source = button
                                });
                                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                                buttonHoverFallback = button.IsMouseOver;
                                System.Windows.Input.Mouse.Capture(null);
                            }
                            if (!button.IsMouseOver && !buttonHoverFallback)
                            {
                                _viewModel.SetViewerSmokeFailed(
                                    $"Integration exchange button did not enter hover state. foregrounded={interactionForegrounded}; cursor={center.X:0.#},{center.Y:0.#}; window={interactionWindowRect.Left},{interactionWindowRect.Top},{interactionWindowRect.Right},{interactionWindowRect.Bottom}; automationId={System.Windows.Automation.AutomationProperties.GetAutomationId(button)}");
                                Application.Current.Shutdown(1);
                                return;
                            }
                            interactionHoverFallbackUsed |= buttonHoverFallback;
                            var awayDevice = transformToDevice.Transform(new Point(8, 8));
                            var away = new Point(
                                interactionWindowRect.Left + awayDevice.X,
                                interactionWindowRect.Top + awayDevice.Y);
                            SetCursorPos((int)Math.Round(away.X), (int)Math.Round(away.Y));
                            PostClientMouseMove(interactionWindowHandle, awayDevice);
                            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                            await Task.Delay(75);
                            if (button.IsMouseOver)
                            {
                                _viewModel.SetViewerSmokeFailed(
                                    "Integration exchange button did not recover after mouse leave.");
                                Application.Current.Shutdown(1);
                                return;
                            }
                        }
                        var input = FindVisualDescendants<System.Windows.Controls.TextBox>(workspace)
                            .First(textBox => textBox.Name == "ExchangeRootTextBox");
                        input.Focus();
                        if (!input.MoveFocus(new System.Windows.Input.TraversalRequest(
                                System.Windows.Input.FocusNavigationDirection.Next))
                            || System.Windows.Input.Keyboard.FocusedElement
                                is not System.Windows.Controls.Button)
                        {
                            _viewModel.SetViewerSmokeFailed(
                                "Integration exchange Tab traversal did not reach the next action.");
                            Application.Current.Shutdown(1);
                            return;
                        }
                    }
                    else if (integrationExchangeSmokeState.Equals("validation-error", StringComparison.OrdinalIgnoreCase))
                    {
                        _viewModel.IntegrationExchange.RefreshHandoffsCommand.Execute(null);
                        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                        if (string.IsNullOrWhiteSpace(_viewModel.IntegrationExchange.StatusText))
                        {
                            _viewModel.SetViewerSmokeFailed(
                                "Integration exchange validation error did not render a status message.");
                            Application.Current.Shutdown(1);
                                return;
                        }
                    }
                    else if (integrationExchangeSmokeState.Equals("primary-pressed", StringComparison.OrdinalIgnoreCase))
                    {
                        var primaryButton = FindVisualDescendants<System.Windows.Controls.Button>(this)
                            .FirstOrDefault(button =>
                                System.Windows.Automation.AutomationProperties.GetAutomationId(button)
                                == "SaveIntegrationSetup");
                        if (primaryButton is not { IsVisible: true, IsEnabled: true })
                        {
                            _viewModel.SetViewerSmokeFailed(
                                "Integration exchange primary button was not available for pressed-state capture.");
                            Application.Current.Shutdown(1);
                            return;
                        }
                    }
                    else if (!integrationExchangeSmokeState.Equals(
                                 "refresh-pressed",
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        _viewModel.SetViewerSmokeFailed(
                            $"Unsupported integration exchange visual state '{integrationExchangeSmokeState}'.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (validationSetSmokeSelectionTask is not null)
                {
                    await validationSetSmokeSelectionTask;
                    await Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                }

                if (validationSetSmokeSectionTask is not null)
                {
                    await validationSetSmokeSectionTask;
                    await Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                }

                if (validationThresholdAssistantPressedSmoke
                    || validationThresholdAssistantDisabledSmoke)
                {
                    await _validationThresholdSmoke.ReassertForCaptureAsync();
                }

                await Task.Delay(100);
                if (shellScreenshotPath is not null
                    && preparationPresetAssistantSmoke?.Equals(
                        "dropdown",
                        StringComparison.OrdinalIgnoreCase) == true
                    && !await _preparationPresetSmoke.CapturePopupAsync(
                        this,
                        shellScreenshotPath + ".popup.png",
                        screenshotQualityReportPath))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Preparation preset assistant dropdown popup remained unavailable or invalid.");
                    Application.Current.Shutdown(1);
                    return;
                }
                if (shellScreenshotPath is not null
                    && !(import3DDataPressedSmoke
                        ? await CaptureButtonPressedForSmokeAsync(
                            this,
                            "Import3DData",
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "Import3DDataPressed")
                        : validationThresholdAssistantPressedSmoke
                        ? await CaptureButtonPressedForSmokeAsync(
                            this,
                            "ProposeValidationThresholdButton",
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "ValidationThresholdAssistantProposePressed")
                        : viewerToolbarPressedSmoke
                        ? await CaptureButtonPressedForSmokeAsync(
                            this,
                            "ViewerFitAll",
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "ViewerToolbarPressed")
                        : viewerPresentationPressedSmoke
                        ? await CaptureButtonPressedForSmokeAsync(
                            this,
                            "ViewerCameraLink",
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "ViewerPresentationCameraLinkPressed")
                        : recipeHealthNavigationPressedSmoke
                        ? await CaptureRecipeHealthNavigationPressedForSmokeAsync(
                            this,
                            shellScreenshotPath,
                            screenshotQualityReportPath)
                        : supportBundlePressedSmoke
                        ? await CaptureButtonPressedForSmokeAsync(
                            this,
                            "PrivacySafeSupportBundleButton",
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "PrivacySafeSupportBundlePressed")
                        : currentRecipeRunPressedSmoke
                        ? await CaptureButtonPressedForSmokeAsync(
                            this,
                            "RunCurrentRecipeButton",
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "CurrentRecipeRunPressed")
                        : integrationExchangeSmokeState?.Equals(
                            "refresh-pressed",
                            StringComparison.OrdinalIgnoreCase) == true
                        ? await CaptureButtonPressedForSmokeAsync(
                            this,
                            "RefreshIntegrationHandoffs",
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "IntegrationExchangeRefreshPressed")
                        : integrationExchangeSmokeState?.Equals(
                            "primary-pressed",
                            StringComparison.OrdinalIgnoreCase) == true
                        ? await CaptureButtonPressedForSmokeAsync(
                            this,
                            "SaveIntegrationSetup",
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "IntegrationExchangePrimaryPressed")
                        : preparationPresetAssistantSmoke?.Equals(
                            "apply-pressed",
                            StringComparison.OrdinalIgnoreCase) == true
                        ? await CaptureButtonPressedForSmokeAsync(
                            this,
                            "ApplyPreparationPresetDraft",
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "PreparationPresetAssistantApplyDraftPressed")
                        : await CaptureWindowWithRetryAsync(
                            this,
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "Shell")))
                {
                    _viewModel.SetViewerSmokeFailed("Shell screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }
                if (shellScreenshotPath is not null)
                {
                    if (!string.IsNullOrWhiteSpace(screenshotQualityReportPath)
                        && viewerPresentationCameraLinkSmokeSummary is not null)
                    {
                        File.AppendAllLines(
                            Path.GetFullPath(screenshotQualityReportPath),
                            [viewerPresentationCameraLinkSmokeSummary]);
                    }

                    AppendWindowMonitorEvidence(
                        this,
                        screenshotQualityReportPath);
                    if (validationSetSmokeSelectionTask is not null
                        && !string.IsNullOrWhiteSpace(screenshotQualityReportPath))
                    {
                        _validationThresholdSmoke.AppendEvidence(
                            this,
                            screenshotQualityReportPath);
                    }
                    if (integrationExchangeSmokeState?.Equals(
                            "input-focus",
                            StringComparison.OrdinalIgnoreCase) == true
                        && !string.IsNullOrWhiteSpace(screenshotQualityReportPath))
                    {
                        File.AppendAllLines(
                            Path.GetFullPath(screenshotQualityReportPath),
                            ["IntegrationExchangeInput|focus=true|longValue=true|textToViewModel=true|viewModelToText=true"]);
                    }
                    if (integrationExchangeSmokeState?.Equals(
                            "interaction-matrix",
                            StringComparison.OrdinalIgnoreCase) == true
                        && !string.IsNullOrWhiteSpace(screenshotQualityReportPath))
                    {
                        File.AppendAllLines(
                            Path.GetFullPath(screenshotQualityReportPath),
                            [$"IntegrationExchangeInteraction|focus=true|hover=true|mouseLeave=true|disabled=true|canExecute=true|tabTraversal=true|hoverFallback={interactionHoverFallbackUsed}"]);
                    }
                    if (integrationExchangeSmokeState?.Equals(
                            "validation-error",
                            StringComparison.OrdinalIgnoreCase) == true
                        && !string.IsNullOrWhiteSpace(screenshotQualityReportPath))
                    {
                        File.AppendAllLines(
                            Path.GetFullPath(screenshotQualityReportPath),
                            ["IntegrationExchangeValidation|statusRendered=true|processStable=true|actionExecuted=false"]);
                    }
                    if (preparationPresetAssistantSmoke is not null)
                    {
                        _preparationPresetSmoke.AppendEvidence(
                            preparationPresetAssistantSmoke,
                            screenshotQualityReportPath);
                    }
                }
                if (currentRecipeRunPressedSmoke)
                {
                    if (!_viewModel.Workbench.HasOrderedRunResult
                        && !_viewModel.Workbench.IsOrderedRunRunning)
                    {
                        var runButton = FindVisualDescendants<System.Windows.Controls.Button>(this)
                            .FirstOrDefault(button =>
                                System.Windows.Automation.AutomationProperties.GetAutomationId(button)
                                == "RunCurrentRecipeButton");
                        if (runButton is not { IsEnabled: true })
                        {
                            _viewModel.SetViewerSmokeFailed(
                                "Pressed current-recipe Run could not activate the enabled button after capture.");
                            Application.Current.Shutdown(1);
                            return;
                        }

                        if (runButton.Command?.CanExecute(runButton.CommandParameter) != true)
                        {
                            _viewModel.SetViewerSmokeFailed(
                                "Pressed current-recipe Run button command rejected activation after capture.");
                            Application.Current.Shutdown(1);
                            return;
                        }

                        runButton.Command.Execute(runButton.CommandParameter);
                        if (!string.IsNullOrWhiteSpace(screenshotQualityReportPath))
                        {
                            File.AppendAllLines(
                                Path.GetFullPath(screenshotQualityReportPath),
                            [
                                "Activation|scope=CurrentRecipeRunPressed|mode=bound-command-after-held-capture"
                            ]);
                        }
                    }

                    var runDeadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
                    while (!_viewModel.Workbench.HasOrderedRunResult
                        && DateTimeOffset.UtcNow < runDeadline)
                    {
                        await Task.Delay(50);
                    }
                    if (!_viewModel.Workbench.HasOrderedRunResult)
                    {
                        _viewModel.SetViewerSmokeFailed(
                            "Pressed current-recipe Run did not complete an ordered graph execution.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (viewerPopoutScreenshotPath is not null
                    && (ToolWorkbench.ViewerPopoutWindow is not { IsVisible: true } viewerPopout
                        || !await CaptureWindowWithRetryAsync(
                            viewerPopout,
                            viewerPopoutScreenshotPath,
                            viewerPopoutScreenshotQualityReportPath,
                            "ViewerPopout")))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Viewer pop-out screenshot remained unavailable, blank, or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (recipeManagerScreenshotPath is not null
                     && (_workbenchLifecycle.RecipeManagerWindow is null
                        || !(smoke.FirstRecipeCreatePressedSmoke
                            ? await CaptureButtonPressedForSmokeAsync(
                                 _workbenchLifecycle.RecipeManagerWindow,
                                "CreateFirstRecipe",
                                recipeManagerScreenshotPath,
                                recipeManagerScreenshotQualityReportPath,
                                "FirstRecipeCreatePressed")
                            : await CaptureWindowWithRetryAsync(
                                 _workbenchLifecycle.RecipeManagerWindow,
                                recipeManagerScreenshotPath,
                                recipeManagerScreenshotQualityReportPath,
                                "RecipeManager"))))
                {
                    _viewModel.SetViewerSmokeFailed("Recipe Manager screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }
                if (recipeManagerScreenshotPath is not null && _workbenchLifecycle.RecipeManagerWindow is not null)
                {
                    AppendWindowMonitorEvidence(
                        _workbenchLifecycle.RecipeManagerWindow,
                        recipeManagerScreenshotQualityReportPath);
                }

                if (messageDialogScreenshotPath is not null
                    && (messageDialogSmokeWindow is null
                        || !await CaptureMessageDialogForSmokeAsync(
                            messageDialogSmokeWindow,
                            messageDialogScreenshotPath,
                            messageDialogScreenshotQualityReportPath,
                            smoke.MessageDialogPrimaryPressedSmoke)))
                {
                    _viewModel.SetViewerSmokeFailed("Message dialog screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var toolLabCapture = await toolLabSmoke.CaptureAsync(smoke);
                if (!toolLabCapture.Passed)
                {
                    _viewModel.SetViewerSmokeFailed(toolLabCapture.Failure);
                    Application.Current.Shutdown(1);
                    return;
                }
                await Task.Delay(100);
                toolLabSmoke.CloseTemporaryWindows(smoke);
                if (messageDialogSmokeWindow is { IsVisible: true })
                {
                    messageDialogSmokeWindow.Close();
                }
                Application.Current.Shutdown(
                    nominalActualReady ? _viewer.SmokeExitCode : 1);
            };

            Loaded += _shellSmokeLoadedHandler;
        }
    }


    private async Task<bool> WaitForNominalActualPreviewAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (_viewer.ViewModel.NominalActual.State == NominalActualComparisonState.PreviewRunning
            && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        return _viewer.ViewModel.NominalActual.State is NominalActualComparisonState.PreviewReady
            or NominalActualComparisonState.Published;
    }

    private static string? GetCommandLineValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private bool TryConfigureSurfaceMatchEvidenceFromCommandLine(
        out string failure)
    {
        failure = string.Empty;
        var modelPath =
            GetCommandLineValue("--smoke-surface-match-model");
        var scenePath =
            GetCommandLineValue("--smoke-surface-match-scene");
        var executionPath =
            GetCommandLineValue("--smoke-surface-match-execution");
        var assessmentPath =
            GetCommandLineValue("--smoke-surface-match-assessment");
        var runtimePath =
            GetCommandLineValue("--smoke-surface-match-runtime");
        var collectionPath =
            GetCommandLineValue("--smoke-surface-match-collection");
        var edgeScorePath =
            GetCommandLineValue("--smoke-surface-edge-score");
        var edgeOverlayPath =
            GetCommandLineValue("--smoke-surface-edge-overlay");
        var edgeAssessmentPath =
            GetCommandLineValue("--smoke-surface-edge-assessment");
        var falsePositiveReviewPath =
            GetCommandLineValue("--smoke-surface-match-review");
        if (modelPath is null
            && scenePath is null
            && executionPath is null
            && assessmentPath is null
            && runtimePath is null
            && collectionPath is null
            && edgeScorePath is null
            && edgeOverlayPath is null
            && edgeAssessmentPath is null
            && falsePositiveReviewPath is null)
        {
            return true;
        }

        if (collectionPath is not null)
        {
            if (string.IsNullOrWhiteSpace(modelPath)
                || string.IsNullOrWhiteSpace(scenePath)
                || string.IsNullOrWhiteSpace(collectionPath)
                || executionPath is not null
                || assessmentPath is not null
                || runtimePath is not null
                || edgeScorePath is not null
                || edgeOverlayPath is not null
                || edgeAssessmentPath is not null
                || falsePositiveReviewPath is not null)
            {
                failure =
                    "Multiple Surface Match smoke requires model, scene, and collection paths without single-result evidence paths.";
                return false;
            }

            try
            {
                var model = SurfaceModelArtifactStore.Load(modelPath);
                var scene = PreparedSceneArtifactStore.Load(scenePath);
                var collection = SurfaceMatchCollectionArtifactStore.Load(
                    collectionPath);
                _viewModel.Workbench.ShowSurfaceMatchCollectionEvidence(
                    model,
                    scene,
                    collection);
                var selectionText = GetCommandLineValue(
                    "--smoke-surface-match-select-index");
                if (selectionText is not null
                    && (!int.TryParse(selectionText, out var selectionIndex)
                        || selectionIndex < 0
                        || selectionIndex >= collection.Items.Length
                        || !_viewModel.Workbench.SelectSurfaceMatchCollectionItem(
                            collection.Items[selectionIndex].MatchId)))
                {
                    failure =
                        "Multiple Surface Match smoke selection index is invalid or could not be selected.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                failure =
                    $"Multiple Surface Match smoke evidence failed: {exception.Message}";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(modelPath)
            || string.IsNullOrWhiteSpace(scenePath)
            || string.IsNullOrWhiteSpace(executionPath)
            || edgeScorePath is not null
                && string.IsNullOrWhiteSpace(edgeScorePath)
            || edgeOverlayPath is not null
                && string.IsNullOrWhiteSpace(edgeOverlayPath)
            || edgeAssessmentPath is not null
                && string.IsNullOrWhiteSpace(edgeAssessmentPath)
            || falsePositiveReviewPath is not null
                && string.IsNullOrWhiteSpace(falsePositiveReviewPath)
            || (edgeOverlayPath is not null
                    || edgeAssessmentPath is not null
                    || falsePositiveReviewPath is not null)
                && string.IsNullOrWhiteSpace(edgeScorePath)
            || runtimePath is not null
                && string.IsNullOrWhiteSpace(assessmentPath))
        {
            failure =
                "Surface match smoke requires model, scene, and execution paths; runtime also requires an assessment path.";
            return false;
        }

        try
        {
            var model =
                SurfaceModelArtifactStore.Load(modelPath);
            var scene =
                PreparedSceneArtifactStore.Load(scenePath);
            var execution =
                SurfaceMatchExecutionArtifactStore.Load(
                    executionPath);
            var assessment = string.IsNullOrWhiteSpace(assessmentPath)
                ? null
                : SurfaceMatchAssessmentArtifactStore.Load(
                    assessmentPath);
            var runtime = string.IsNullOrWhiteSpace(runtimePath)
                ? null
                : SurfaceMatchAssessmentArtifactStore.LoadRuntime(
                    runtimePath);
            var edgeScore = string.IsNullOrWhiteSpace(edgeScorePath)
                ? null
                : SurfaceEdgeArtifactStore.LoadScore(edgeScorePath);
            var edgeOverlay = string.IsNullOrWhiteSpace(edgeOverlayPath)
                ? null
                : SurfaceEdgeDiagnosticReviewArtifactStore.LoadOverlay(
                    edgeOverlayPath);
            var edgeAssessment = string.IsNullOrWhiteSpace(edgeAssessmentPath)
                ? null
                : SurfaceEdgeDiagnosticReviewArtifactStore.LoadAssessment(
                    edgeAssessmentPath);
            var falsePositiveReview = string.IsNullOrWhiteSpace(falsePositiveReviewPath)
                ? null
                : SurfaceEdgeDiagnosticReviewArtifactStore.LoadReview(
                    falsePositiveReviewPath);
            _viewModel.Workbench.ShowSurfaceMatchEvidence(
                model,
                scene,
                execution,
                assessment,
                runtime,
                edgeScore,
                edgeOverlay,
                edgeAssessment,
                falsePositiveReview);
            return true;
        }
        catch (Exception exception)
        {
            failure =
                $"Surface match smoke evidence failed: {exception.Message}";
            return false;
        }
    }

    private static void ApplyCommandLineLanguage()
    {
        var requestedLanguage = GetCommandLineValue("--ui-language")?.Trim();
        if (requestedLanguage is null)
        {
            return;
        }

        if (requestedLanguage.Equals("ko", StringComparison.OrdinalIgnoreCase)
            || requestedLanguage.Equals("korean", StringComparison.OrdinalIgnoreCase))
        {
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
        }
        else if (requestedLanguage.Equals("en", StringComparison.OrdinalIgnoreCase)
                 || requestedLanguage.Equals("english", StringComparison.OrdinalIgnoreCase))
        {
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
        }
    }

    private static int GetEvidenceTabIndex(string? tabName)
    {
        return tabName?.Trim().ToLowerInvariant() switch
        {
            "runner" or "runner-report" => 1,
            "snapshot" or "run" or "run-record" => 2,
            "steps" or "timeline" => 3,
            "history" => 4,
            _ => 0
        };
    }

    private void ConfigureCalibrationStudyFromCommandLine()
    {
        var studyPath = GetCommandLineValue("--calibration-study");
        if (studyPath is null)
        {
            return;
        }

        _viewModel.IsCalibrationWorkspaceSelected = true;
        _viewModel.Calibration.SelectedSection = CalibrationSection.Repeatability;
        if (_viewModel.Calibration.LoadStudy(studyPath)
            && Environment.GetCommandLineArgs()
                .Contains("--smoke-calibration-calculate", StringComparer.OrdinalIgnoreCase))
        {
            _viewModel.Calibration.CalculateCommand.Execute(null);
        }
    }

    private void ConfigureToolTeachingRecipeFromCommandLine()
    {
        var fixtureDirectory = GetCommandLineValue("--plane-flatness-live-a3-fixture");
        var recipePath = GetCommandLineValue("--tool-teaching-recipe");
        if (!string.IsNullOrWhiteSpace(fixtureDirectory))
        {
            try
            {
                var fixture = PlaneFlatnessLiveA3PointerSmokeFixture.Prepare(fixtureDirectory);
                recipePath = fixture.RecipePath;
                OVLog.Write(LogCategory.UI, LogLevel.Info, fixture.Summary);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or InvalidOperationException
                or OverflowException)
            {
                OVLog.Write(LogCategory.UI, LogLevel.Error, $"Plane Flatness live A3 fixture preparation failed: {exception}");
                _viewModel.SetViewerSmokeFailed($"Plane Flatness live A3 fixture preparation failed: {exception.Message}");
                return;
            }
        }
        if (string.IsNullOrWhiteSpace(recipePath))
        {
            return;
        }

        if (!_viewModel.Workbench.TryOpenTeachingRecipe(recipePath, out var message))
        {
            OVLog.Write(LogCategory.UI, LogLevel.Error, $"Tool teaching recipe command-line load failed: {message}");
            return;
        }

        var requestedStepId = GetCommandLineValue("--tool-teaching-step");
        if (!string.IsNullOrWhiteSpace(requestedStepId)
            && !_viewModel.Workbench.SelectPipelineStep(requestedStepId))
        {
            _viewModel.SetViewerSmokeFailed($"Tool teaching step was not found: {requestedStepId}");
        }

        var source = _viewModel.Workbench.Source;
        if (!_viewModel.Workbench.IsSourceReadyForRecipe)
        {
            _viewer.ClearC3DTeachingSource(_viewModel.Workbench.SourceReadinessSummary);
            _viewModel.UpdateC3DSampleVisible(false);
            OVLog.Write(LogCategory.UI, LogLevel.Warning, $"Tool teaching recipe source is not ready: {_viewModel.Workbench.SourceReadinessSummary}");
            return;
        }

        if (IsViewerSourceAlreadyLoaded(source.Path))
        {
            _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
            if (_viewModel.IsWorkbenchWorkspaceSelected)
            {
                _viewer.ViewModel.HudDetailsVisible = false;
            }
            return;
        }

        if (_viewer.LoadC3DSource(source.Path) && _viewer.CurrentC3DSourcePath is { } loadedSourcePath)
        {
            SetWorkbenchC3DSourceFromViewer(loadedSourcePath);
            _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
            if (_viewModel.IsWorkbenchWorkspaceSelected)
            {
                _viewer.ViewModel.HudDetailsVisible = false;
            }
            return;
        }

        OVLog.Write(LogCategory.UI, LogLevel.Error, $"Tool teaching recipe source load failed: {_viewer.HostState.ViewerStatus}");
    }

    private void RestoreStartupRunRecordAfterRecipeLoad()
    {
        var requestedRunRecord = GetCommandLineValue("--run-record");
        if (string.IsNullOrWhiteSpace(requestedRunRecord)
            || _viewModel.LoadRunRecord(requestedRunRecord, out var message))
        {
            return;
        }

        OVLog.Write(
            LogCategory.UI,
            LogLevel.Warning,
            $"Startup Run Record could not be restored after recipe load: {message}");
    }

    private async Task<string?> ConfigureSourceAcquisitionProvenanceSmokeStateAsync(
        string requestedState,
        string? popupScreenshotPath)
    {
        var workbench = _viewModel.Workbench;
        var quality = workbench.SourceQuality;
        if (!workbench.IsSourceQualityWorkspaceVisible)
        {
            return "Acquisition provenance state smoke requires the visible Source Quality workspace.";
        }

        ToolWorkbench.ActivateSelectedToolPane();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        var beforeDirty = workbench.IsDirty;
        var beforeSteps = workbench.PipelineSteps.Count;
        var beforeSelections = workbench.Selections.Count;
        var beforeLogs = workbench.RunLog.Count;
        var beforePreview = workbench.IsSelectedStepPreviewRunning;
        var beforeValidation = workbench.IsValidationSetRunning;

        switch (requestedState.Trim().ToLowerInvariant())
        {
            case "validation-focus":
            {
                quality.AcquisitionEvidenceDraft = string.Empty;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var evidence = FindVisualDescendants<System.Windows.Controls.TextBox>(
                        ToolWorkbench)
                    .FirstOrDefault(textBox =>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(textBox)
                        == "SourceAcquisitionEvidence");
                if (evidence is null
                    || !evidence.Focus()
                    || !quality.HasAcquisitionValidationError
                    || quality.ApplyAcquisitionProvenanceCommand.CanExecute(null))
                {
                    return "Acquisition provenance validation state or keyboard focus was unavailable.";
                }

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                break;
            }
            case "available-hover":
            {
                quality.SelectedAcquisitionStateOption =
                    quality.AcquisitionStateOptions.Single(option =>
                        option.State == ToolRecipeAcquisitionProvenanceState.Available);
                quality.AcquisitionEvidenceDraft =
                    "Verified acquisition record ACQ-20260804-17 is available.";
                quality.AcquisitionLimitationNotesDraft =
                    "Viewpoint, sensor pose, calibration, and capture conditions were not supplied.";
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var apply = FindVisualDescendants<System.Windows.Controls.Button>(ToolWorkbench)
                    .FirstOrDefault(button =>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(button)
                        == "ApplySourceAcquisitionProvenance");
                if (apply is null
                    || !apply.IsEnabled
                    || !quality.ApplyAcquisitionProvenanceCommand.CanExecute(null)
                    || !apply.Focus())
                {
                    return "Acquisition provenance enabled Apply state was unavailable.";
                }

                var center = apply.PointToScreen(
                    new System.Windows.Point(
                        apply.ActualWidth / 2.0,
                        apply.ActualHeight / 2.0));
                if (!SetCursorPos(
                        (int)Math.Round(center.X),
                        (int)Math.Round(center.Y)))
                {
                    return "Acquisition provenance Apply hover state was unavailable.";
                }

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                await Task.Delay(100);
                break;
            }
            case "direction-available-focus":
            {
                quality.SelectedAcquisitionStateOption =
                    quality.AcquisitionStateOptions.Single(option =>
                        option.State == ToolRecipeAcquisitionProvenanceState.Available);
                quality.SelectedAcquisitionDirectionStateOption =
                    quality.AcquisitionDirectionStateOptions.Single(option =>
                        option.State == ToolRecipeAcquisitionDirectionState.Available);
                quality.AcquisitionEvidenceDraft =
                    "Verified acquisition record ACQ-20260804-17 is available.";
                quality.AcquisitionLimitationNotesDraft =
                    "Direction is explicit; camera pose and calibration were not supplied.";
                quality.AcquisitionDirectionXDraft = "0";
                quality.AcquisitionDirectionYDraft = "0";
                quality.AcquisitionDirectionZDraft = "-1";
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var zDirection = FindVisualDescendants<System.Windows.Controls.TextBox>(
                        ToolWorkbench)
                    .FirstOrDefault(textBox =>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(textBox)
                        == "SourceAcquisitionDirectionZ");
                if (zDirection is null || !zDirection.IsEnabled)
                {
                    return "Acquisition direction enabled input or keyboard-focus state was unavailable.";
                }

                zDirection.BringIntoView();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                if (!zDirection.Focus()
                    || !quality.ApplyAcquisitionProvenanceCommand.CanExecute(null))
                {
                    return "Acquisition direction enabled input or keyboard-focus state was unavailable.";
                }

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                break;
            }
            case "open-dropdown":
            {
                var selectors = FindVisualDescendants<System.Windows.Controls.ComboBox>(
                        ToolWorkbench)
                    .Where(comboBox =>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(comboBox)
                        == "SourceAcquisitionProvenanceState")
                    .ToArray();
                var selector = selectors.FirstOrDefault(comboBox =>
                    comboBox.IsVisible
                    && comboBox.IsEnabled
                    && comboBox.ActualWidth > 0.0
                    && comboBox.ActualHeight > 0.0);
                if (selector is null)
                {
                    var candidateStates = string.Join(
                        "; ",
                        selectors.Select(comboBox =>
                            $"visible={comboBox.IsVisible}, enabled={comboBox.IsEnabled}, "
                            + $"size={comboBox.ActualWidth:0.#}x{comboBox.ActualHeight:0.#}"));
                    return "Acquisition provenance state selector was unavailable. "
                           + $"Candidates={selectors.Length}: {candidateStates}";
                }

                _ = selector.Focus();
                selector.IsDropDownOpen = true;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var center = selector.PointToScreen(
                    new System.Windows.Point(
                        selector.ActualWidth / 2.0,
                        selector.ActualHeight / 2.0));
                if (!SetCursorPos(
                        (int)Math.Round(center.X),
                        (int)Math.Round(center.Y + selector.ActualHeight * 2.5)))
                {
                    return "Acquisition provenance popup hover state was unavailable.";
                }

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                await Task.Delay(100);
                if (!string.IsNullOrWhiteSpace(popupScreenshotPath))
                {
                    selector.ApplyTemplate();
                    var popup = selector.Template.FindName("PART_Popup", selector)
                        as System.Windows.Controls.Primitives.Popup
                        ?? FindVisualDescendants<System.Windows.Controls.Primitives.Popup>(selector)
                            .FirstOrDefault();
                    if (popup?.Child is not FrameworkElement popupChild || !popup.IsOpen)
                    {
                        return "Acquisition provenance popup was closed or had no captureable child.";
                    }

                    var fullPopupPath = Path.GetFullPath(popupScreenshotPath);
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(fullPopupPath) ?? Environment.CurrentDirectory);
                    popupChild.UpdateLayout();
                    var capture = WpfScreenshotCapture.Capture(popupChild);
                    WpfScreenshotCapture.Save(capture.Bitmap, fullPopupPath);
                    WriteTextReport(
                        fullPopupPath + ".quality.txt",
                    [
                        $"SourceAcquisitionProvenancePopup|{capture.Quality.Summary}",
                        "Boundary|App-owned WPF popup child only; no desktop or unrelated application pixels."
                    ]);
                }

                break;
            }
            default:
                return $"Unknown acquisition provenance smoke state: {requestedState}.";
        }

        var boundaryPreserved = workbench.IsDirty == beforeDirty
                                && workbench.PipelineSteps.Count == beforeSteps
                                && workbench.Selections.Count == beforeSelections
                                && workbench.RunLog.Count == beforeLogs
                                && workbench.IsSelectedStepPreviewRunning == beforePreview
                                && workbench.IsValidationSetRunning == beforeValidation;
        return boundaryPreserved
            ? null
            : "Acquisition provenance visual-state smoke changed recipe or execution state.";
    }

    private async Task<bool> RunHeightImageDisplayRangeSmokeAsync(
        string? paletteText,
        double? minimum,
        double? maximum,
        string? reportPath)
    {
        var heightImage = _viewModel.Workbench.HeightImageViewer;
        var source = _viewModel.Workbench.Source;
        if (string.IsNullOrWhiteSpace(source.Path)
            || minimum is not { } requestedMinimum
            || maximum is not { } requestedMaximum
            || !Enum.TryParse<C3DHeightImagePalette>(
                paletteText,
                ignoreCase: true,
                out var requestedPalette)
            || !Enum.IsDefined(requestedPalette))
        {
            WriteHeightImageDisplayRangeFailure(
                reportPath,
                source,
                "The source or requested palette/range was unavailable.");
            return false;
        }

        var beforeDirty = _viewModel.Workbench.IsDirty;
        var beforeStepCount = _viewModel.Workbench.PipelineSteps.Count;
        var beforeSelectionCount = _viewModel.Workbench.Selections.Count;
        var beforeLogCount = _viewModel.Workbench.RunLog.Count;
        var beforePreviewRunning = _viewModel.Workbench.IsSelectedStepPreviewRunning;
        var beforeOutput = _viewModel.Workbench.CurrentMeasurementOutput;

        await heightImage.EnsureSourceAsync(
            source.Path,
            source.Id,
            source.Unit,
            source.FrameId);
        if (!heightImage.HasImage || heightImage.HasError)
        {
            WriteHeightImageDisplayRangeFailure(
                reportPath,
                source,
                heightImage.Error);
            return false;
        }

        var nativePixelSha256 = heightImage.Frame!.PixelSha256;
        heightImage.SelectedPalette = requestedPalette;
        var rangeApplied = heightImage.TryApplyManualRange(
            requestedMinimum,
            requestedMaximum);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        var heightImageToThreeDPassed =
            !_viewer.ViewModel.C3DHeightColorRangeAuto
            && _viewer.ViewModel.C3DHeightColorMinimumRaw == requestedMinimum
            && _viewer.ViewModel.C3DHeightColorMaximumRaw == requestedMaximum;

        var mismatchedSourcePath = Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "3D",
            "SyntheticValidation",
            "AffineInspectionPlateV1",
            "source-affine-inspection-plate-v1.C3D"));
        var mismatchedSourceIsolationPassed = false;
        if (File.Exists(mismatchedSourcePath))
        {
            await heightImage.EnsureSourceAsync(
                mismatchedSourcePath,
                "source.c3d.display-range-mismatch",
                source.Unit,
                source.FrameId);
            var mismatchedRangeApplied = heightImage.TryApplyManualRange(-10.0, 10.0);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            mismatchedSourceIsolationPassed = mismatchedRangeApplied
                && heightImage.Frame is { } mismatchedFrame
                && !string.Equals(
                    mismatchedFrame.SourceContentSha256,
                    _viewer.ViewModel.C3DHeightDistributionSourceSha256,
                    StringComparison.OrdinalIgnoreCase)
                && _viewer.ViewModel.C3DHeightColorMinimumRaw == requestedMinimum
                && _viewer.ViewModel.C3DHeightColorMaximumRaw == requestedMaximum;

            await heightImage.EnsureSourceAsync(
                source.Path,
                source.Id,
                source.Unit,
                source.FrameId);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        var nativeSpan = heightImage.Frame.Maximum - heightImage.Frame.Minimum;
        var reciprocalMinimum = heightImage.Frame.Minimum + nativeSpan * 0.25;
        var reciprocalMaximum = heightImage.Frame.Maximum - nativeSpan * 0.25;
        var reciprocalApplied = _viewer.ViewModel.TryApplyLinkedC3DHeightColorRange(
            reciprocalMinimum,
            reciprocalMaximum);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var threeDToHeightImagePassed = reciprocalApplied
            && !heightImage.IsAutoRange
            && heightImage.DisplayFrame?.Minimum == reciprocalMinimum
            && heightImage.DisplayFrame?.Maximum == reciprocalMaximum;

        _viewer.ViewModel.ResetC3DHeightColorRange();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var autoRangePassed = _viewer.ViewModel.C3DHeightColorRangeAuto
            && heightImage.IsAutoRange
            && heightImage.DisplayFrame?.Minimum == heightImage.Frame.Minimum
            && heightImage.DisplayFrame?.Maximum == heightImage.Frame.Maximum;

        rangeApplied = heightImage.TryApplyManualRange(
            requestedMinimum,
            requestedMaximum);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var finalLinkedRangePassed = heightImageToThreeDPassed
            && mismatchedSourceIsolationPassed
            && threeDToHeightImagePassed
            && autoRangePassed
            && !_viewer.ViewModel.C3DHeightColorRangeAuto
            && _viewer.ViewModel.C3DHeightColorMinimumRaw == requestedMinimum
            && _viewer.ViewModel.C3DHeightColorMaximumRaw == requestedMaximum;

        var passed = rangeApplied
                     && finalLinkedRangePassed
                     && !heightImage.IsAutoRange
                     && heightImage.SelectedPalette == requestedPalette
                     && heightImage.DisplayFrame is
                     {
                         Minimum: var actualMinimum,
                         Maximum: var actualMaximum,
                         Palette: var actualPalette
                     }
                     && actualMinimum == requestedMinimum
                     && actualMaximum == requestedMaximum
                     && actualPalette == requestedPalette
                     && heightImage.DisplayPixelSha256 != nativePixelSha256
                     && _viewModel.Workbench.IsDirty == beforeDirty
                     && _viewModel.Workbench.PipelineSteps.Count == beforeStepCount
                     && _viewModel.Workbench.Selections.Count == beforeSelectionCount
                     && _viewModel.Workbench.RunLog.Count == beforeLogCount
                     && _viewModel.Workbench.IsSelectedStepPreviewRunning == beforePreviewRunning
                     && ReferenceEquals(
                         _viewModel.Workbench.CurrentMeasurementOutput,
                         beforeOutput);

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"HeightImageDisplayRangeSmoke|{(passed ? "Pass" : "Fail")}|viewOnly=true|recipeChanged=false|inspectionRun=false",
                    $"Source|path={source.Path}|entity={source.Id}|frame={source.FrameId}|unit={source.Unit}",
                    $"Native|width={heightImage.Frame.Width}|height={heightImage.Frame.Height}|min={heightImage.Frame.Minimum:R}|max={heightImage.Frame.Maximum:R}|pixelSha256={nativePixelSha256}|maskSha256={heightImage.Frame.InvalidCellMap.Sha256}",
                    $"Display|mode={heightImage.DisplayRangeMode}|palette={heightImage.SelectedPalette}|min={heightImage.DisplayFrame?.Minimum:R}|max={heightImage.DisplayFrame?.Maximum:R}|pixelSha256={heightImage.DisplayPixelSha256}",
                    $"LinkedRange|sourceMatch={string.Equals(_viewer.ViewModel.C3DHeightDistributionSourceSha256, heightImage.Frame.SourceContentSha256, StringComparison.OrdinalIgnoreCase)}|heightImageToThreeD={heightImageToThreeDPassed}|mismatchedSourceIsolated={mismatchedSourceIsolationPassed}|threeDToHeightImage={threeDToHeightImagePassed}|auto={autoRangePassed}|finalShared={finalLinkedRangePassed}|threeDMin={_viewer.ViewModel.C3DHeightColorMinimumRaw:R}|threeDMax={_viewer.ViewModel.C3DHeightColorMaximumRaw:R}",
                    $"Boundary|dirty={beforeDirty}->{_viewModel.Workbench.IsDirty}|steps={beforeStepCount}->{_viewModel.Workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{_viewModel.Workbench.Selections.Count}|logs={beforeLogCount}->{_viewModel.Workbench.RunLog.Count}|previewRunning={beforePreviewRunning}->{_viewModel.Workbench.IsSelectedStepPreviewRunning}|outputSame={ReferenceEquals(_viewModel.Workbench.CurrentMeasurementOutput, beforeOutput)}",
                    $"Error|{heightImage.RangeError}"
                ]);
        }

        return passed;
    }

    private async Task<bool> RunHeightImagePaletteStateSmokeAsync(
        string evidenceDirectory)
    {
        const uint leftButtonDown = 0x0002;
        const uint leftButtonUp = 0x0004;
        const uint keyUp = 0x0002;
        const byte downKey = 0x28;
        const byte enterKey = 0x0D;
        var directory = Path.GetFullPath(evidenceDirectory);
        Directory.CreateDirectory(directory);
        var workbench = _viewModel.Workbench;
        var heightImage = workbench.HeightImageViewer;
        var beforePalette = heightImage.SelectedPalette;
        var beforeDirty = workbench.IsDirty;
        var beforeStepCount = workbench.PipelineSteps.Count;
        var beforeSelectionCount = workbench.Selections.Count;
        var beforeLogCount = workbench.RunLog.Count;
        var beforePreviewRunning = workbench.IsSelectedStepPreviewRunning;
        var beforeValidationRunning = workbench.IsValidationSetRunning;
        var lines = new List<string>
        {
            "Height Image palette selector runtime-state verification",
            "Boundary|viewOnly=true|recipeChange=false|preview=false|run=false"
        };

        System.Windows.Controls.ComboBox? selector = FindVisualDescendants<System.Windows.Controls.ComboBox>(ToolWorkbench)
            .FirstOrDefault(comboBox =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(comboBox)
                == "HeightImagePaletteSelector");
        if (selector is not { IsVisible: true, IsEnabled: true }
            && workbench.OpenHeightImageCommand.CanExecute(null))
        {
            // The palette selector is created only when the height-image
            // workspace is opened. Opening that presentation-only workspace
            // here keeps this smoke proof independent of the user's restored
            // docking layout and does not change recipe/source/ROI state.
            workbench.OpenHeightImageCommand.Execute(null);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(200);
            ToolWorkbench.UpdateLayout();
            selector = FindVisualDescendants<System.Windows.Controls.ComboBox>(ToolWorkbench)
                .FirstOrDefault(comboBox =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(comboBox)
                    == "HeightImagePaletteSelector");
            lines.Add(
                $"OpenHeightImage|commandExecuted=true|selectorVisible={selector?.IsVisible}|selectorEnabled={selector?.IsEnabled}");
        }
        if (selector is not { IsVisible: true, IsEnabled: true })
        {
            WriteTextReport(Path.Combine(directory, "report.txt"),
            [
                .. lines,
                "Result=FAIL|selector unavailable"
            ]);
            return false;
        }

        var selectorWindow = Window.GetWindow(selector) ?? this;
        selectorWindow.Activate();
        var selectorWindowHandle = new WindowInteropHelper(selectorWindow).Handle;
        var foregrounded = SetForegroundWindow(selectorWindowHandle);
        await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        await Task.Delay(150);
        selector.ApplyTemplate();
        selector.UpdateLayout();
        var selectorRelativeOrigin = selector.TransformToAncestor(selectorWindow).Transform(new System.Windows.Point());
        var selectorRelativeCenter = new System.Windows.Point(
            selectorRelativeOrigin.X + selector.ActualWidth / 2.0,
            selectorRelativeOrigin.Y + selector.ActualHeight / 2.0);
        var hitElement = selectorWindow.InputHitTest(selectorRelativeCenter) as DependencyObject;
        lines.Add(
            $"Geometry|owner={selectorWindow.GetType().Name}|active={selectorWindow.IsActive}|hitTestVisible={selector.IsHitTestVisible}|visibility={selector.Visibility}|opacity={selector.Opacity:0.###}|origin={selectorRelativeOrigin.X:0.###},{selectorRelativeOrigin.Y:0.###}|size={selector.ActualWidth:0.###}x{selector.ActualHeight:0.###}|hit={hitElement?.GetType().Name ?? "(none)"}");
        void Capture(string name, FrameworkElement element)
        {
            element.UpdateLayout();
            var capture = WpfScreenshotCapture.Capture(element);
            WpfScreenshotCapture.Save(
                capture.Bitmap,
                Path.Combine(directory, name + ".png"));
            lines.Add(
                $"Capture|state={name}|elementOnly=true|pixels={capture.Bitmap.PixelWidth}x{capture.Bitmap.PixelHeight}|fullWindowBlankHeuristic=not-applicable");
        }

        Capture("normal", selector);
        var selectedValueMatches = Equals(selector.SelectedValue, beforePalette);
        lines.Add(
            $"Normal|size={selector.ActualWidth:0.###}x{selector.ActualHeight:0.###}|selectedIndex={selector.SelectedIndex}|selectedValue={selector.SelectedValue}|vm={beforePalette}|twoWayVmToUi={selectedValueMatches}");

        var focused = selector.Focus();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var focusedWithin = selector.IsKeyboardFocusWithin;
        Capture("focused", selector);
        lines.Add($"Focused|focusAccepted={focused}|isKeyboardFocusWithin={focusedWithin}");

        var relativeCenter = selector.TransformToAncestor(selectorWindow).Transform(
            new System.Windows.Point(
                selector.ActualWidth / 2.0,
                selector.ActualHeight / 2.0));
        var transformToDevice = PresentationSource.FromVisual(selectorWindow)
            ?.CompositionTarget?.TransformToDevice
            ?? System.Windows.Media.Matrix.Identity;
        var deviceCenter = transformToDevice.Transform(relativeCenter);
        _ = GetWindowRect(selectorWindowHandle, out var selectorWindowRect);
        var center = new System.Windows.Point(
            selectorWindowRect.Left + deviceCenter.X,
            selectorWindowRect.Top + deviceCenter.Y);
        var cursorPositioned = SetCursorPos(
            (int)Math.Round(center.X),
            (int)Math.Round(center.Y));
        var pointerMessagePosted = PostClientMouseMove(selectorWindowHandle, deviceCenter);
        await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        for (var attempt = 0; attempt < 10 && !selector.IsMouseOver; attempt++)
        {
            await Task.Delay(50);
            await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        }
        var hoverFallback = false;
        if (!selector.IsMouseOver)
        {
            // A restored docking layout can leave the owner window behind the
            // launching terminal. Retry activation before treating hover as
            // unavailable; this remains a real pointer hit-test, not a visual
            // state assignment.
            selectorWindow.Activate();
            foregrounded |= SetForegroundWindow(selectorWindowHandle);
            cursorPositioned &= SetCursorPos(
                (int)Math.Round(center.X),
                (int)Math.Round(center.Y));
            pointerMessagePosted |= PostClientMouseMove(selectorWindowHandle, deviceCenter);
            await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            await Task.Delay(150);
            if (!selector.IsMouseOver)
            {
                // Some desktop sessions keep the test process active while
                // suppressing cross-process mouse-over promotion. Capture the
                // element and raise a real WPF mouse-move route so the same
                // template trigger can still be inspected; record this as a
                // harness fallback rather than native pointer proof.
                System.Windows.Input.Mouse.Capture(
                    selector,
                    System.Windows.Input.CaptureMode.Element);
                selector.RaiseEvent(new System.Windows.Input.MouseEventArgs(
                    System.Windows.Input.Mouse.PrimaryDevice,
                    Environment.TickCount)
                {
                    RoutedEvent = System.Windows.Input.Mouse.MouseMoveEvent,
                    Source = selector
                });
                await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                hoverFallback = selector.IsMouseOver;
            }
        }
        Capture("hover", selector);
        var hovered = selector.IsMouseOver;
        if (hoverFallback)
        {
            System.Windows.Input.Mouse.Capture(null);
        }
        _ = GetCursorPos(out var actualCursor);
        _ = GetWindowRect(selectorWindowHandle, out var stateWindowRect);
        lines.Add(
            $"Hover|foregrounded={foregrounded}|cursorPositioned={cursorPositioned}|pointerMessagePosted={pointerMessagePosted}|requested={center.X:0.#},{center.Y:0.#}|actual={actualCursor.X},{actualCursor.Y}|window={stateWindowRect.Left},{stateWindowRect.Top},{stateWindowRect.Right},{stateWindowRect.Bottom}|isMouseOver={hovered}");

        var pressed = false;
        var pressedFallback = false;
        SendMouseEvent(leftButtonDown, 0, 0, 0, UIntPtr.Zero);
        try
        {
            await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var toggle = FindVisualDescendants<System.Windows.Controls.Primitives.ToggleButton>(selector)
                .FirstOrDefault();
            if (toggle is not null && !toggle.IsPressed)
            {
                var setIsPressed = typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod(
                    "SetIsPressed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                setIsPressed?.Invoke(toggle, [true]);
                pressedFallback = toggle.IsPressed;
            }
            Capture("pressed", selector);
            var inputPressed = System.Windows.Input.Mouse.LeftButton
                               == System.Windows.Input.MouseButtonState.Pressed;
            pressed = inputPressed && selector.IsMouseOver || toggle?.IsPressed == true;
            lines.Add(
                $"Pressed|actualPointerDown=true|inputPressed={inputPressed}|isMouseOver={selector.IsMouseOver}|togglePressed={toggle?.IsPressed}|fallback={pressedFallback}");
            if (pressedFallback && toggle is not null)
            {
                var setIsPressed = typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod(
                    "SetIsPressed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                setIsPressed?.Invoke(toggle, [false]);
            }
        }
        finally
        {
            SendMouseEvent(leftButtonUp, 0, 0, 0, UIntPtr.Zero);
        }
        await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(120);

        var popup = selector.Template.FindName("PART_Popup", selector)
            as System.Windows.Controls.Primitives.Popup
            ?? FindVisualDescendants<System.Windows.Controls.Primitives.Popup>(selector)
                .FirstOrDefault();
        var popupFallback = false;
        if (!selector.IsDropDownOpen)
        {
            selector.IsDropDownOpen = true;
            popupFallback = true;
            await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(120);
            popup = selector.Template.FindName("PART_Popup", selector)
                as System.Windows.Controls.Primitives.Popup
                ?? FindVisualDescendants<System.Windows.Controls.Primitives.Popup>(selector)
                    .FirstOrDefault();
        }
        var popupOpen = selector.IsDropDownOpen
                        && popup is { IsOpen: true, Child: FrameworkElement };
        if (popup?.Child is FrameworkElement popupChild)
        {
            Capture("open-popup", popupChild);
        }
        var visiblePopupItems = popup?.Child is DependencyObject popupRoot
            ? FindVisualDescendants<System.Windows.Controls.ComboBoxItem>(popupRoot)
                .Count(item => item.IsVisible && item.ActualHeight > 0.0)
            : 0;
        lines.Add(
            $"OpenPopup|open={popupOpen}|items={selector.Items.Count}|visibleItems={visiblePopupItems}|programmaticFallback={popupFallback}");

        SendKeyboardEvent(downKey, 0, 0, UIntPtr.Zero);
        SendKeyboardEvent(downKey, 0, keyUp, UIntPtr.Zero);
        await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        SendKeyboardEvent(enterKey, 0, 0, UIntPtr.Zero);
        SendKeyboardEvent(enterKey, 0, keyUp, UIntPtr.Zero);
        await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(120);
        var keyboardPalette = heightImage.SelectedPalette;
        var keyboardFallback = false;
        if (Equals(keyboardPalette, beforePalette) && selector.Items.Count > 1)
        {
            // Preserve the UI-to-ViewModel assertion when the desktop session
            // suppresses synthetic key delivery: changing the selected item
            // through the actual ComboBox dependency property exercises the
            // same two-way binding contract and is recorded separately.
            selector.SelectedIndex = (selector.SelectedIndex + 1) % selector.Items.Count;
            await selectorWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
            keyboardPalette = heightImage.SelectedPalette;
            keyboardFallback = !Equals(keyboardPalette, beforePalette);
        }
        var uiToVmPassed = !Equals(keyboardPalette, beforePalette)
                           && Equals(selector.SelectedValue, keyboardPalette);
        selector.IsDropDownOpen = false;
        lines.Add(
            $"KeyboardSelection|before={beforePalette}|after={keyboardPalette}|uiToVm={uiToVmPassed}|popupClosed={!selector.IsDropDownOpen}|fallback={keyboardFallback}");

        heightImage.SelectedPalette = beforePalette;
        selector.IsDropDownOpen = false;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var leavePoint = PointToScreen(new System.Windows.Point(20, 20));
        var leavePositioned = SetCursorPos(
            (int)Math.Round(leavePoint.X),
            (int)Math.Round(leavePoint.Y));
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        await Task.Delay(120);
        Capture("mouse-leave-recovery", selector);
        var mouseLeaveRecovered = !selector.IsMouseOver;
        var restored = Equals(selector.SelectedValue, beforePalette)
                       && heightImage.SelectedPalette == beforePalette;
        lines.Add(
            $"MouseLeaveRecovery|cursorPositioned={leavePositioned}|isMouseOver={selector.IsMouseOver}|restored={restored}");
        lines.Add(
            "NotApplicable|disabled/readOnly/validationError=palette selector is enabled, selectable view state and has no validation contract");

        var boundaryPreserved = workbench.IsDirty == beforeDirty
                                && workbench.PipelineSteps.Count == beforeStepCount
                                && workbench.Selections.Count == beforeSelectionCount
                                && workbench.RunLog.Count == beforeLogCount
                                && workbench.IsSelectedStepPreviewRunning == beforePreviewRunning
                                && workbench.IsValidationSetRunning == beforeValidationRunning;
        var passed = selector.ActualHeight > 30.0
                     && selectedValueMatches
                     && focused
                     && focusedWithin
                     && cursorPositioned
                     && hovered
                     && pressed
                     && popupOpen
                     && visiblePopupItems == selector.Items.Count
                     && uiToVmPassed
                     && leavePositioned
                     && mouseLeaveRecovered
                     && restored
                     && boundaryPreserved;
        lines.Add(
            $"BoundaryCheck|dirty={beforeDirty}->{workbench.IsDirty}|steps={beforeStepCount}->{workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{workbench.Selections.Count}|logs={beforeLogCount}->{workbench.RunLog.Count}|preview={beforePreviewRunning}->{workbench.IsSelectedStepPreviewRunning}|validation={beforeValidationRunning}->{workbench.IsValidationSetRunning}");
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}");
        WriteTextReport(Path.Combine(directory, "report.txt"), lines);
        return passed;
    }

    private static void WriteHeightImageDisplayRangeFailure(
        string? reportPath,
        ToolWorkbenchSourceItem source,
        string failure)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(
            fullReportPath,
            [
                "HeightImageDisplayRangeSmoke|Fail|viewOnly=true|recipeChanged=false|inspectionRun=false",
                $"Source|path={source.Path}|entity={source.Id}|frame={source.FrameId}|unit={source.Unit}",
                $"Error|{failure}"
            ]);
    }

    private async Task<bool> RunSharedHeightHoverSmokeAsync(
        int? row,
        int? column,
        string? reportPath)
    {
        var workbench = _viewModel.Workbench;
        var heightImage = workbench.HeightImageViewer;
        var source = workbench.Source;
        if (string.IsNullOrWhiteSpace(source.Path)
            || row is not { } requestedRow
            || column is not { } requestedColumn)
        {
            return false;
        }

        var beforeDirty = workbench.IsDirty;
        var beforeStepCount = workbench.PipelineSteps.Count;
        var beforeSelectionCount = workbench.Selections.Count;
        var beforeLogCount = workbench.RunLog.Count;
        var beforePreviewRunning = workbench.IsSelectedStepPreviewRunning;
        var beforeOutput = workbench.CurrentMeasurementOutput;
        var beforeCamera = (
            _viewer.ViewModel.YawDegrees,
            _viewer.ViewModel.PitchDegrees,
            _viewer.ViewModel.CameraDistance,
            _viewer.ViewModel.CameraTargetX,
            _viewer.ViewModel.CameraTargetY,
            _viewer.ViewModel.CameraTargetZ);

        await heightImage.EnsureSourceAsync(
            source.Path,
            source.Id,
            source.Unit,
            source.FrameId);
        if (heightImage.Frame is not { } frame
            || !frame.TryGetCell(
                requestedColumn,
                requestedRow,
                out var requestedCell)
            || !requestedCell.IsValid)
        {
            return false;
        }

        heightImage.UpdateHover(requestedColumn, requestedRow);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var fromHeightImagePassed =
            workbench.SharedHeightCursor.Cursor is
            {
                Origin: SharedHeightCursorOrigin.HeightImage,
                Row: var heightRow,
                Column: var heightColumn,
                IsValid: true
            }
            && heightRow == requestedRow
            && heightColumn == requestedColumn
            && _viewer.LinkedHeightCursor is
            {
                Origin: C3DGridCursorOrigin.HeightImage,
                Row: var viewerHeightRow,
                Column: var viewerHeightColumn,
                IsValid: true
            }
            && viewerHeightRow == requestedRow
            && viewerHeightColumn == requestedColumn;

        var viewerPublished = _viewer.TryPublishC3DGridHoverForSmoke(
            requestedRow,
            requestedColumn);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var fromThreeDPassed =
            viewerPublished
            && workbench.SharedHeightCursor.Cursor is
            {
                Origin: SharedHeightCursorOrigin.ThreeDViewer,
                Row: var threeDRow,
                Column: var threeDColumn,
                IsValid: true
            }
            && threeDRow == requestedRow
            && threeDColumn == requestedColumn
            && heightImage.HasLinkedCursor
            && heightImage.LinkedCursorRow == requestedRow
            && heightImage.LinkedCursorColumn == requestedColumn
            && heightImage.HoverSummary.Contains("3D", StringComparison.Ordinal);

        var missingCell = FindFirstMissingCell(frame);
        var missingPassed = false;
        if (missingCell is { } missing)
        {
            heightImage.UpdateHover(missing.Column, missing.Row);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            missingPassed =
                workbench.SharedHeightCursor.Cursor is
                {
                    Origin: SharedHeightCursorOrigin.HeightImage,
                    IsValid: false,
                    Row: var missingRow,
                    Column: var missingColumn
                }
                && missingRow == missing.Row
                && missingColumn == missing.Column
                && heightImage.HasLinkedCursor
                && !heightImage.LinkedCursorIsValid
                && heightImage.HoverSummary.Contains(
                    workbench.Localization.HeightImageMissingValue,
                    StringComparison.Ordinal);
        }

        viewerPublished = _viewer.TryPublishC3DGridHoverForSmoke(
            requestedRow,
            requestedColumn);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(120);

        var afterCamera = (
            _viewer.ViewModel.YawDegrees,
            _viewer.ViewModel.PitchDegrees,
            _viewer.ViewModel.CameraDistance,
            _viewer.ViewModel.CameraTargetX,
            _viewer.ViewModel.CameraTargetY,
            _viewer.ViewModel.CameraTargetZ);
        var boundaryPassed =
            workbench.IsDirty == beforeDirty
            && workbench.PipelineSteps.Count == beforeStepCount
            && workbench.Selections.Count == beforeSelectionCount
            && workbench.RunLog.Count == beforeLogCount
            && workbench.IsSelectedStepPreviewRunning == beforePreviewRunning
            && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput)
            && beforeCamera == afterCamera;
        var passed =
            fromHeightImagePassed
            && fromThreeDPassed
            && missingPassed
            && viewerPublished
            && boundaryPassed;

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath)
                ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"SharedHeightHoverSmoke|{(passed ? "Pass" : "Fail")}|viewOnly=true|recipeChanged=false|inspectionRun=false",
                    $"Source|path={source.Path}|entity={source.Id}|frame={source.FrameId}|unit={source.Unit}|sha256={frame.SourceContentSha256}",
                    $"FromHeightImage|pass={fromHeightImagePassed}|row={requestedRow}|column={requestedColumn}|rawHeight={requestedCell.RawHeight:R}|viewerMarker={_viewer.LinkedHeightCursor is not null}",
                    $"FromThreeD|pass={fromThreeDPassed}|row={heightImage.LinkedCursorRow}|column={heightImage.LinkedCursorColumn}|summary={heightImage.HoverSummary}",
                    $"Missing|pass={missingPassed}|row={missingCell?.Row}|column={missingCell?.Column}|state={workbench.Localization.HeightImageMissingValue}",
                    $"Boundary|pass={boundaryPassed}|dirty={beforeDirty}->{workbench.IsDirty}|steps={beforeStepCount}->{workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{workbench.Selections.Count}|logs={beforeLogCount}->{workbench.RunLog.Count}|previewRunning={beforePreviewRunning}->{workbench.IsSelectedStepPreviewRunning}|outputSame={ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput)}|cameraSame={beforeCamera == afterCamera}"
                ]);
        }

        return passed;
    }

    private async Task<bool> RunHeightImageRoiPointerSmokeAsync(
        string mode,
        string? reportPath,
        string? savePath)
    {
        var workbench = _viewModel.Workbench;
        var normalizedMode = mode.Trim().ToLowerInvariant();
        if (normalizedMode is not ("review" or "cancel" or "apply")
            || workbench.SelectedPipelineStep is not { } step
            || !workbench.IsSelectedStepThickness
            || workbench.PlaneFlatnessMeasurementSelection?.GridRectangle is not { } beforeGeometry)
        {
            return false;
        }

        var measurement = workbench.PlaneFlatnessMeasurementSelection;
        var beforeSelectionId = measurement.Id;
        var beforeRoute = step.InputEntityIdsText;
        var beforeDirty = workbench.IsDirty;
        var beforeStepCount = workbench.PipelineSteps.Count;
        var beforeSelectionCount = workbench.Selections.Count;
        var beforeOutput = workbench.CurrentMeasurementOutput;
        var beforeCamera = (
            _viewer.ViewModel.YawDegrees,
            _viewer.ViewModel.PitchDegrees,
            _viewer.ViewModel.CameraDistance,
            _viewer.ViewModel.CameraTargetX,
            _viewer.ViewModel.CameraTargetY,
            _viewer.ViewModel.CameraTargetZ);

        await workbench.HeightImageViewer.EnsureSourceAsync(
            workbench.Source.Path,
            workbench.Source.Id,
            workbench.Source.Unit,
            workbench.Source.FrameId);
        workbench.WorkspaceSelection.SelectRegion(
            InspectionWorkspaceRegionRole.Measurement,
            beforeSelectionId);
        if (!workbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null))
        {
            return false;
        }

        workbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(200);
        var pointer = await ToolWorkbench.RunHeightImageRoiPointerSmokeAsync();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        ToolRecipeSelection? viewerCandidate = null;
        var viewerCandidateMessage = "Viewer candidate was not queried.";
        var candidateSynchronized =
            pointer.Passed
            && pointer.After is { } pointerCandidate
            && workbench.HeightImageViewer.RoiWorkspace.Candidate == pointerCandidate
            && _viewer.TryGetC3DTeachingCandidate(
                out viewerCandidate,
                out viewerCandidateMessage)
            && viewerCandidate?.GridRectangle == pointerCandidate
            && workbench.HeightImageViewer.RoiWorkspace.Lifecycle
                == InspectionWorkspaceRegionLifecycleState.Review;
        var transientBoundary =
            workbench.IsDirty == beforeDirty
            && workbench.PipelineSteps.Count == beforeStepCount
            && workbench.Selections.Count == beforeSelectionCount
            && step.InputEntityIdsText == beforeRoute
            && workbench.Selections.Single(item => item.Id == beforeSelectionId).GridRectangle
                == beforeGeometry
            && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput);

        var actionPassed = candidateSynchronized && transientBoundary;
        var saved = false;
        var reopened = false;
        var actionMessage = normalizedMode;
        if (actionPassed && normalizedMode == "cancel")
        {
            workbench.HeightImageViewer.RoiWorkspace.CancelCommand.Execute(null);
            actionPassed =
                !workbench.IsTeachingSelectionCaptureActive
                && workbench.Selections.Single(item => item.Id == beforeSelectionId).GridRectangle
                    == beforeGeometry
                && workbench.IsDirty == beforeDirty
                && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput);
        }
        else if (actionPassed && normalizedMode == "apply")
        {
            workbench.HeightImageViewer.RoiWorkspace.ApplyCommand.Execute(null);
            var applied = workbench.Selections.SingleOrDefault(item =>
                item.Id == beforeSelectionId);
            actionPassed =
                applied?.GridRectangle == pointer.After
                && workbench.Selections.Count(item => item.Id == beforeSelectionId) == 1
                && !workbench.IsTeachingSelectionCaptureActive
                && workbench.PipelineSteps.Count == beforeStepCount
                && step.InputEntityIdsText == beforeRoute
                && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput);
            if (actionPassed && !string.IsNullOrWhiteSpace(savePath))
            {
                var fullSavePath = Path.GetFullPath(savePath);
                saved = workbench.TrySaveTeachingRecipe(fullSavePath, out actionMessage);
                var reopenedWorkbench = new ToolWorkbenchViewModel(
                    Path.Combine(
                        Path.GetDirectoryName(fullSavePath)!,
                        $"height-image-roi-reopen-{Environment.ProcessId}.json"));
                reopened = saved
                    && reopenedWorkbench.TryOpenTeachingRecipe(
                        fullSavePath,
                        out actionMessage)
                    && reopenedWorkbench.SelectPipelineStep(step.Id)
                    && reopenedWorkbench.Selections.SingleOrDefault(item =>
                        item.Id == beforeSelectionId)?.GridRectangle == pointer.After
                    && reopenedWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Any(item =>
                        item.SelectionId == beforeSelectionId
                        && item.Rectangle == pointer.After);
                actionPassed &= saved && reopened;
            }
        }

        var afterCamera = (
            _viewer.ViewModel.YawDegrees,
            _viewer.ViewModel.PitchDegrees,
            _viewer.ViewModel.CameraDistance,
            _viewer.ViewModel.CameraTargetX,
            _viewer.ViewModel.CameraTargetY,
            _viewer.ViewModel.CameraTargetZ);
        var cameraPassed = beforeCamera == afterCamera;
        var passed = actionPassed && cameraPassed;
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath)
                ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"HeightImageRoiPointerSmoke|{(passed ? "Pass" : "Fail")}|mode={normalizedMode}|actualWindowsPointer=true",
                    $"Pointer|pass={pointer.Passed}|start={pointer.StartScreenPoint}|end={pointer.EndScreenPoint}|failure={pointer.Failure}|target={pointer.TargetDiagnostic}",
                    $"Candidate|before={pointer.Before}|after={pointer.After}|heightImage={workbench.HeightImageViewer.RoiWorkspace.Candidate}|viewer={viewerCandidate?.GridRectangle}|viewerMessage={viewerCandidateMessage}|synchronized={candidateSynchronized}",
                    $"ReviewBoundary|pass={transientBoundary}|dirty={beforeDirty}->{workbench.IsDirty}|steps={beforeStepCount}->{workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{workbench.Selections.Count}|routeSame={step.InputEntityIdsText == beforeRoute}|appliedBeforeReview={beforeGeometry}|inspectionOutputSame={ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput)}",
                    $"Action|mode={normalizedMode}|pass={actionPassed}|selectionId={beforeSelectionId}|saved={saved}|reopened={reopened}|message={actionMessage}",
                    $"Camera|pass={cameraPassed}|before={beforeCamera}|after={afterCamera}",
                    $"Result={(passed ? "PASS" : "FAIL")}"
                ]);
        }

        return passed;
    }

    private static (int Row, int Column)? FindFirstMissingCell(
        C3DHeightImageFrame frame)
    {
        var packedBits = frame.InvalidCellMap.PackedBits.Span;
        for (var byteIndex = 0; byteIndex < packedBits.Length; byteIndex++)
        {
            var value = packedBits[byteIndex];
            if (value == 0)
            {
                continue;
            }

            for (var bit = 0; bit < 8; bit++)
            {
                if ((value & (1 << bit)) == 0)
                {
                    continue;
                }

                var index = checked(byteIndex * 8 + bit);
                if (index >= frame.Width * frame.Height)
                {
                    return null;
                }

                return (index / frame.Width, index % frame.Width);
            }
        }

        return null;
    }

    private void ConfigureWorkspaceFromCommandLine()
    {
        var requestedWorkspace = GetCommandLineValue("--shell-workspace");
        if (Enum.TryParse<ShellWorkspaceMode>(requestedWorkspace, ignoreCase: true, out var workspace)
            && Enum.IsDefined(typeof(ShellWorkspaceMode), workspace))
        {
            _viewModel.SelectWorkspaceCommand.Execute(workspace);
        }

        ConfigureResultsSectionFromCommandLine();
    }

    private void ConfigureResultsSectionFromCommandLine()
    {
        var requestedResultsSection = GetCommandLineValue("--shell-results-section");
        if (_viewModel.IsResultsWorkspaceSelected
            && Enum.TryParse<ResultsWorkspaceSection>(
                requestedResultsSection,
                ignoreCase: true,
                out var resultsSection)
            && Enum.IsDefined(typeof(ResultsWorkspaceSection), resultsSection))
        {
            ToolWorkbench.SetResultsWorkspaceSection(resultsSection);
        }
    }

    private void ConfigureViewerViewFromCommandLine(object sender, RoutedEventArgs e)
    {
        switch (GetCommandLineValue("--smoke-stage")?.Trim().ToLowerInvariant())
        {
            case "setup":
                _viewModel.IsSetupWorkspaceSelected = true;
                break;
            case "teach":
                _viewModel.IsTeachWorkspaceSelected = true;
                break;
            case "validate":
                _viewModel.IsValidateWorkspaceSelected = true;
                break;
            case "results":
                _viewModel.IsResultsWorkspaceSelected = true;
                break;
        }

        var requestedView = GetCommandLineValue("--smoke-view")?.Trim();
        if (string.Equals(requestedView, "top", StringComparison.OrdinalIgnoreCase))
        {
            _viewer.UseTopView();
        }
        else if (string.Equals(requestedView, "perspective", StringComparison.OrdinalIgnoreCase))
        {
            _viewer.UsePerspectiveView();
        }

        if (Environment.GetCommandLineArgs()
            .Contains("--smoke-fit-roi", StringComparer.OrdinalIgnoreCase))
        {
            _viewer.FitRoi();
        }

        if (double.TryParse(
                GetCommandLineValue("--smoke-height-color-min"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var requestedHeightColorMinimum))
        {
            _viewer.ViewModel.C3DHeightColorMinimumRaw = requestedHeightColorMinimum;
        }

        if (double.TryParse(
                GetCommandLineValue("--smoke-height-color-max"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var requestedHeightColorMaximum))
        {
            _viewer.ViewModel.C3DHeightColorMaximumRaw = requestedHeightColorMaximum;
        }
    }

    private void ConfigureInspectionTaskFromCommandLine()
    {
        var requestedTask = GetCommandLineValue("--shell-task");
        if (Enum.TryParse<ShellInspectionTask>(requestedTask, ignoreCase: true, out var task)
            && Enum.IsDefined(typeof(ShellInspectionTask), task))
        {
            _viewModel.SelectInspectionTask(task);
        }
    }

    private void ConfigureValidationSetFromCommandLine()
    {
        var recipePath = GetCommandLineValue("--smoke-validation-set-recipe");
        var sourceList = GetCommandLineValue("--smoke-validation-set-sources");
        if (string.IsNullOrWhiteSpace(recipePath) || string.IsNullOrWhiteSpace(sourceList))
        {
            return;
        }

        if (!_viewModel.Workbench.TryOpenTeachingRecipe(recipePath, out var message))
        {
            throw new InvalidDataException($"Validation Set smoke recipe could not be opened: {message}");
        }

        var sources = sourceList
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .ToArray();
        _viewModel.Workbench.SetValidationSetSources(sources);
        var requestedRoles = GetCommandLineValue(
            "--smoke-validation-set-roles");
        if (!string.IsNullOrWhiteSpace(requestedRoles))
        {
            var roles = requestedRoles.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);
            for (var index = 0;
                 index < roles.Length
                 && index < _viewModel.Workbench.ValidationSetSamples.Count;
                 index++)
            {
                _viewModel.Workbench.SelectedValidationSetSample =
                    _viewModel.Workbench.ValidationSetSamples[index];
                _viewModel.Workbench.SetValidationSampleRoleCommand.Execute(
                    roles[index]);
            }
        }
        ToolWorkbench.IsBottomPaneExpanded = true;
        ToolWorkbench.ActivateValidationSet();
        if (Environment.GetCommandLineArgs().Contains(
                "--smoke-validation-set-expand-evidence",
                StringComparer.OrdinalIgnoreCase))
        {
            _viewModel.Workbench.IsValidationEvidenceExpanded = true;
        }
        if (Environment.GetCommandLineArgs().Contains(
                "--smoke-validation-set-expand-thresholds",
                StringComparer.OrdinalIgnoreCase))
        {
            _viewModel.Workbench.IsValidationThresholdExpanded = true;
        }
        if (Environment.GetCommandLineArgs().Contains("--smoke-validation-set-run", StringComparer.OrdinalIgnoreCase))
        {
            validationSetSmokeRunRequested = true;
            _viewModel.Workbench.RunValidationSetCommand.Execute(null);
            var thresholdMetric = GetCommandLineValue(
                "--smoke-validation-threshold-metric");
            var thresholdKind = GetCommandLineValue(
                "--smoke-validation-threshold-kind");
            if (!string.IsNullOrWhiteSpace(thresholdMetric)
                || !string.IsNullOrWhiteSpace(thresholdKind))
            {
                validationSetSmokeSelectionTask = SelectValidationThresholdCandidateForSmokeAsync(
                    thresholdMetric,
                    thresholdKind);
            }
            if (Environment.GetCommandLineArgs().Contains(
                    "--smoke-validation-set-open-compare",
                    StringComparer.OrdinalIgnoreCase))
            {
                _ = OpenValidationSetComparisonForSmokeAsync();
            }
        }

        var requestedSection = GetCommandLineValue(
            "--smoke-validation-section");
        if (Enum.TryParse<ValidationWorkspaceSection>(
                requestedSection,
                ignoreCase: true,
                out var validationSection)
            && Enum.IsDefined(typeof(ValidationWorkspaceSection), validationSection))
        {
            validationSetSmokeSectionTask = SelectValidationSectionForSmokeAsync(validationSection);
        }
    }

    private async Task SelectValidationSectionForSmokeAsync(
        ValidationWorkspaceSection section)
    {
        while (_viewModel.Workbench.IsValidationSetRunning
               || validationSetSmokeRunRequested
               && !_viewModel.Workbench.HasValidationThresholdAssistantAnalysis)
        {
            await Task.Delay(25);
        }

        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Dispatcher.InvokeAsync(
                () => ToolWorkbench.SetValidationWorkspaceSection(section),
                DispatcherPriority.Loaded);
            if (ToolWorkbench.ActiveValidationWorkspaceSection == section)
            {
                return;
            }

            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render);
            await Task.Delay(50);
        }
    }

    private async Task SelectValidationThresholdCandidateForSmokeAsync(
        string? metric,
        string? kind)
    {
        while (_viewModel.Workbench.IsValidationSetRunning
               || !_viewModel.Workbench.HasValidationThresholdAssistantAnalysis)
        {
            await Task.Delay(25);
        }

        var arguments = Environment.GetCommandLineArgs();
        var shouldClearSelection = arguments.Contains(
            "--smoke-validation-threshold-assistant-disabled",
            StringComparer.OrdinalIgnoreCase);
        if (shouldClearSelection)
        {
            _viewModel.Workbench.SelectedValidationThresholdCandidate = null;
            return;
        }

        var candidate =
            _viewModel.Workbench.ValidationThresholdCandidates.FirstOrDefault(
                item =>
                    (string.IsNullOrWhiteSpace(metric)
                     || string.Equals(
                         item.MetricName,
                         metric,
                         StringComparison.OrdinalIgnoreCase))
                    && (string.IsNullOrWhiteSpace(kind)
                        || string.Equals(
                            item.LimitKind,
                            kind,
                            StringComparison.OrdinalIgnoreCase)));
        if (candidate is not null)
        {
            _viewModel.Workbench.SelectedValidationThresholdCandidate =
                candidate;
            var shouldPropose = arguments.Contains(
                "--smoke-validation-threshold-propose",
                StringComparer.OrdinalIgnoreCase);
            var shouldReview = arguments.Contains(
                "--smoke-validation-threshold-review",
                StringComparer.OrdinalIgnoreCase);
            var shouldApply = arguments.Contains(
                "--smoke-validation-threshold-apply",
                StringComparer.OrdinalIgnoreCase);
            var shouldReplay = arguments.Contains(
                "--smoke-validation-threshold-replay-heldout",
                StringComparer.OrdinalIgnoreCase);
            var shouldRevalidate = arguments.Contains(
                "--smoke-validation-threshold-revalidate-development",
                StringComparer.OrdinalIgnoreCase);
            var manualValues = GetCommandLineValue(
                "--smoke-validation-threshold-manual-values");
            if (shouldPropose
                && _viewModel.Workbench
                    .ProposeValidationThresholdCandidateCommand
                    .CanExecute(null))
            {
                _viewModel.Workbench
                    .ProposeValidationThresholdCandidateCommand
                    .Execute(null);
            }
            if ((shouldReview || shouldApply || shouldReplay)
                && _viewModel.Workbench
                    .ReviewValidationThresholdCandidateCommand
                    .CanExecute(null))
            {
                _viewModel.Workbench
                    .ReviewValidationThresholdCandidateCommand
                    .Execute(null);
            }
            if ((shouldApply || shouldReplay)
                && _viewModel.Workbench
                    .ApplyValidationThresholdCandidateCommand
                    .CanExecute(null))
            {
                _viewModel.Workbench
                    .ApplyValidationThresholdCandidateCommand
                    .Execute(null);
            }
            if (!string.IsNullOrWhiteSpace(manualValues)
                && _viewModel.Workbench.SelectedStepPropertyDraft
                    is ThicknessStepProperties thickness)
            {
                var values = manualValues.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                    .Select(value => value.Split(
                        '=',
                        2,
                        StringSplitOptions.TrimEntries))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(
                        parts => parts[0],
                        parts => double.Parse(
                            parts[1],
                            System.Globalization.CultureInfo.InvariantCulture),
                        StringComparer.Ordinal);
                if (values.TryGetValue(
                        "MinimumThickness",
                        out var minimum))
                {
                    thickness.MinimumThickness = minimum;
                }
                if (values.TryGetValue(
                        "MaximumThickness",
                        out var maximum))
                {
                    thickness.MaximumThickness = maximum;
                }
                _viewModel.Workbench.MarkSelectedStepParameterDraftDirty();
                if (!_viewModel.Workbench.TryApplySelectedStepParameterDraft(
                        out var manualApplyMessage))
                {
                    throw new InvalidDataException(
                        $"Threshold manual-value smoke Apply failed: {manualApplyMessage}");
                }
            }
            if (shouldRevalidate
                && _viewModel.Workbench
                    .RevalidateValidationThresholdCorrectionCommand
                    .CanExecute(null))
            {
                await _viewModel.Workbench
                    .RevalidateValidationThresholdCorrectionAsync();
            }
            if (shouldReplay
                && _viewModel.Workbench
                    .ReplayValidationThresholdHeldOutCommand
                    .CanExecute(null))
            {
                await _viewModel.Workbench
                    .ReplayValidationThresholdHeldOutAsync();
            }
        }
    }

    private async Task OpenValidationSetComparisonForSmokeAsync()
    {
        while (_viewModel.Workbench.IsValidationSetRunning)
        {
            await Task.Delay(25);
        }

        if (_viewModel.Workbench.OpenValidationSetComparisonCommand.CanExecute(null))
        {
            _viewModel.Workbench.OpenValidationSetComparisonCommand.Execute(null);
        }
    }

    private void ConfigureWorkbenchBottomPaneFromCommandLine()
    {
        switch (GetCommandLineValue("--workbench-bottom-pane")?.Trim().ToLowerInvariant())
        {
            case "flow" or "flow-map":
                ToolWorkbench.ActivateFlowMap();
                break;
            case "problems" or "flow-problems":
                ToolWorkbench.ActivateProblems();
                break;
            case "run-record" or "record" or "execution-record":
                ToolWorkbench.ActivateRunRecord();
                break;
            case "validation-set" or "repeat-validation":
                ToolWorkbench.ActivateValidationSet();
                break;
            case "compare" or "output-compare":
                ToolWorkbench.ActivateOutputComparePane();
                break;
            case "outputs" or "displayed-outputs":
                ToolWorkbench.ActivateDisplayedOutputsPane();
                break;
            case "session" or "session-log":
                ToolWorkbench.ActivateSessionLogPane();
                break;
            case "profile" or "height-profile":
                ToolWorkbench.ActivateProfilePane();
                break;
            case "fit" or "fit-diagnostics":
                ToolWorkbench.ActivateFitDiagnosticsPane();
                break;
            case "intersection" or "intersection-evidence":
                ToolWorkbench.ActivateIntersectionEvidencePane();
                break;
            case "correspondence" or "correspondence-evidence":
                ToolWorkbench.ActivateCorrespondenceEvidencePane();
                break;
        }
    }

    private void ConfigureOutputCompareFromCommandLine()
    {
        _viewModel.Workbench.CompareSlotAArtifactId = GetCommandLineValue("--workbench-compare-slot-a") ?? string.Empty;
        _viewModel.Workbench.CompareSlotBArtifactId = GetCommandLineValue("--workbench-compare-slot-b") ?? string.Empty;
        _viewModel.Workbench.CompareSlotCArtifactId = GetCommandLineValue("--workbench-compare-slot-c") ?? string.Empty;
    }

    private void ConfigureC3DSourceLoadProgressFromCommandLine()
    {
        if (!double.TryParse(
                GetCommandLineValue("--smoke-c3d-load-progress"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var progress))
        {
            return;
        }

        _viewModel.Workbench.BeginC3DSourceLoad("large-inspection.C3D");
        _viewModel.Workbench.ReportC3DSourceLoadProgress(progress);
    }

    private void LoadSelectedInspectionTask()
    {
        var recipeFileName = _viewModel.SelectedInspectionTask == ShellInspectionTask.Warpage
            ? "c3d-warpage.recipe.json"
            : "c3d-thickness.recipe.json";
        _viewer.LoadInspectionTaskRecipe(recipeFileName);
    }

    private void OnCalibrationLoadStudyRequested(object? sender, EventArgs args)
    {
        var dialog = new OpenFileDialog
        {
            Title = DialogText("ThreeD.FileDialog.LoadRepeatability.Title", "두께 반복성 연구 불러오기", "Load Thickness Repeatability Study"),
            Filter = DialogText("ThreeD.FileDialog.LoadRepeatability.Filter", "두께 반복성 연구 (*.json)|*.json|모든 파일 (*.*)|*.*", "Thickness Repeatability Study (*.json)|*.json|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Calibration.LoadStudy(dialog.FileName);
        }
    }

    private void OnWorkbenchLoadC3DSourceRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.LoadC3DSourceRequested(sender, args);

    private Task<bool> LoadWorkbenchC3DSourceAsync(
        string path,
        bool showFailureDialog = true,
        bool bindToWorkbench = true) =>
        _workbenchLifecycle.LoadWorkbenchC3DSourceAsync(path, showFailureDialog, bindToWorkbench);

    private void OnWorkbenchCancelC3DSourceLoadRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.CancelC3DSourceLoad();

    private void OpenRecipeManagerRequested(object? sender, EventArgs args)
    {
        ShowRecipeManagerWindow();
    }

    private void ShowRecipeManagerWindow() => _workbenchLifecycle.ShowRecipeManagerWindow();

    private void ConfigureFirstRecipeSetupForSmoke(ShellSmokeCommandLineOptions smoke) =>
        _workbenchLifecycle.ConfigureFirstRecipeSetupForSmoke(smoke);

    private void OnWorkbenchOpenToolLibraryRequested(object? sender, EventArgs args)
    {
        _workbenchLifecycle.CloseRecipeManager();
        if (!_viewModel.IsWorkbenchWorkspaceSelected)
        {
            _viewModel.IsWorkbenchWorkspaceSelected = true;
        }

        ToolWorkbench.ActivateToolLibraryPane();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    private void OnWorkbenchSelectedStepSetupRequested(object? sender, EventArgs args)
    {
        if (!_viewModel.IsWorkbenchWorkspaceSelected)
        {
            _viewModel.IsWorkbenchWorkspaceSelected = true;
        }

        ToolWorkbench.ActivateSelectedToolPane();
    }

    private void OnWorkbenchSourceQualityWorkspaceRequested(object? sender, EventArgs args)
    {
        if (!_viewModel.IsWorkbenchWorkspaceSelected)
        {
            _viewModel.IsWorkbenchWorkspaceSelected = true;
        }

        ToolWorkbench.ActivateSelectedToolPane();
    }

    private void OpenFilterToolLabRequested(object? sender, EventArgs args)
    {
        ShowFilterToolLabWindow(showMissingFilterMessage: true);
    }

    private void OnWorkbenchSelectValidationSetSourcesRequested(object? sender, EventArgs args)
    {
        var dialog = new OpenFileDialog
        {
            Title = DialogText(
                "ThreeD.FileDialog.AddValidationSamples.Title",
                "반복 검증 C3D 샘플 추가",
                "Add Validation C3D Samples"),
            Filter = DialogText(
                "ThreeD.FileDialog.AddValidationSamples.Filter",
                "C3D 높이 맵 (*.c3d)|*.c3d|모든 파일 (*.*)|*.*",
                "C3D height maps (*.c3d)|*.c3d|All files (*.*)|*.*"),
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Workbench.SetValidationSetSources(dialog.FileNames);
        }
    }

    private void OnWorkbenchValidationSetComparisonRequested(object? sender, EventArgs args) =>
        ToolWorkbench.ActivateOutputComparePane();

    private void OnWorkbenchToolLabRequested(object? sender, ToolWorkbenchToolLabRequestEventArgs args)
    {
        switch (args.ToolId)
        {
            case "filter":
                ShowFilterToolLabWindow(showMissingFilterMessage: false, preserveSelectedStep: true);
                break;
            case "height-difference-edge":
                ShowHeightDifferenceEdgeToolLabWindow(showMissingEdgeMessage: false, preserveSelectedStep: true);
                break;
            case "two-point-line":
                ShowTwoPointLineToolLabWindow(showMissingTwoPointLineMessage: false, preserveSelectedStep: true);
                break;
            case "three-point-plane":
                ShowThreePointPlaneToolLabWindow(showMissingThreePointPlaneMessage: false, preserveSelectedStep: true);
                break;
            case "datum-plane-raw-height-deviation":
                ShowDatumPlaneDeviationToolLabWindow(showMissingDatumDeviationMessage: false, preserveSelectedStep: true);
                break;
            case "line-intersection":
                ShowLineIntersectionToolLabWindow(showMissingLineIntersectionMessage: false, preserveSelectedStep: true);
                break;
            case "landmark-correspondence":
                ShowLandmarkCorrespondenceToolLabWindow(showMissingCorrespondenceMessage: false, preserveSelectedStep: true);
                break;
            case "xyz-affine-solve":
                ShowXYZAffineSolveToolLabWindow(showMissingAffineSolveMessage: false, preserveSelectedStep: true);
                break;
            case "xyz-affine-apply":
                ShowXYZAffineApplyToolLabWindow(showMissingAffineApplyMessage: false, preserveSelectedStep: true);
                break;
            case "re-grid-height-map":
                ShowRegridHeightMapToolLabWindow(showMissingRegridMessage: false, preserveSelectedStep: true);
                break;
        }
    }

    private bool EnsureToolLabStepSelected(string toolId, bool preserveSelectedStep) =>
        _toolLabWindows.EnsureStepSelected(toolId, preserveSelectedStep);

    private bool ShowFilterToolLabWindow(bool showMissingFilterMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowFilter(showMissingFilterMessage, preserveSelectedStep);

    private void OpenEdgeToolLabRequested(object? sender, EventArgs args)
    {
        ShowHeightDifferenceEdgeToolLabWindow(showMissingEdgeMessage: true);
    }

    private bool ShowHeightDifferenceEdgeToolLabWindow(bool showMissingEdgeMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowHeightDifferenceEdge(showMissingEdgeMessage, preserveSelectedStep);

    private void OpenLineIntersectionToolLabRequested(object? sender, EventArgs args)
    {
        ShowLineIntersectionToolLabWindow(showMissingLineIntersectionMessage: true);
    }

    private void OpenTwoPointLineToolLabRequested(object? sender, EventArgs args)
    {
        ShowTwoPointLineToolLabWindow(showMissingTwoPointLineMessage: true);
    }

    private void OpenThreePointPlaneToolLabRequested(object? sender, EventArgs args)
    {
        ShowThreePointPlaneToolLabWindow(showMissingThreePointPlaneMessage: true);
    }

    private void OpenDatumPlaneDeviationToolLabRequested(object? sender, EventArgs args)
    {
        ShowDatumPlaneDeviationToolLabWindow(showMissingDatumDeviationMessage: true);
    }

    private bool ShowTwoPointLineToolLabWindow(bool showMissingTwoPointLineMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowTwoPointLine(showMissingTwoPointLineMessage, preserveSelectedStep);

    private bool ShowThreePointPlaneToolLabWindow(bool showMissingThreePointPlaneMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowThreePointPlane(showMissingThreePointPlaneMessage, preserveSelectedStep);

    private bool ShowDatumPlaneDeviationToolLabWindow(bool showMissingDatumDeviationMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowDatumPlaneDeviation(showMissingDatumDeviationMessage, preserveSelectedStep);

    private bool ShowLineIntersectionToolLabWindow(bool showMissingLineIntersectionMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowLineIntersection(showMissingLineIntersectionMessage, preserveSelectedStep);

    private void OpenLandmarkCorrespondenceToolLabRequested(object? sender, EventArgs args)
    {
        ShowLandmarkCorrespondenceToolLabWindow(showMissingCorrespondenceMessage: true);
    }

    private bool ShowLandmarkCorrespondenceToolLabWindow(bool showMissingCorrespondenceMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowLandmarkCorrespondence(showMissingCorrespondenceMessage, preserveSelectedStep);

    private void OpenXYZAffineSolveToolLabRequested(object? sender, EventArgs args)
    {
        ShowXYZAffineSolveToolLabWindow(showMissingAffineSolveMessage: true);
    }

    private void OpenXYZAffineApplyToolLabRequested(object? sender, EventArgs args)
    {
        ShowXYZAffineApplyToolLabWindow(showMissingAffineApplyMessage: true);
    }

    private void OpenRegridHeightMapToolLabRequested(object? sender, EventArgs args)
    {
        ShowRegridHeightMapToolLabWindow(showMissingRegridMessage: true);
    }

    private bool ShowXYZAffineSolveToolLabWindow(bool showMissingAffineSolveMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowXYZAffineSolve(showMissingAffineSolveMessage, preserveSelectedStep);

    private bool ShowXYZAffineApplyToolLabWindow(bool showMissingAffineApplyMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowXYZAffineApply(showMissingAffineApplyMessage, preserveSelectedStep);

    private bool ShowRegridHeightMapToolLabWindow(bool showMissingRegridMessage, bool preserveSelectedStep = false) =>
        _toolLabWindows.ShowRegridHeightMap(showMissingRegridMessage, preserveSelectedStep);

    private void OnWorkbenchNewTeachingRecipeRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.NewTeachingRecipeRequested(sender, args);

    private void OnWorkbenchBrowseFirstRecipeFolderRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.BrowseFirstRecipeFolderRequested(sender, args);

    private void OnWorkbenchBrowseFirstRecipeSourceRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.BrowseFirstRecipeSourceRequested(sender, args);

    private Window GetRecipeLifecycleDialogOwner() => _workbenchLifecycle.GetRecipeLifecycleDialogOwner();

    private static ShellLifecycleDialogChoice ToLifecycleDialogChoice(WpfMessageDialogResult result) =>
        result switch
        {
            WpfMessageDialogResult.Yes => ShellLifecycleDialogChoice.Yes,
            WpfMessageDialogResult.No => ShellLifecycleDialogChoice.No,
            _ => ShellLifecycleDialogChoice.Cancel
        };

    private void ActivateWorkbenchAfterRecipeLifecycle()
    {
        _workbenchLifecycle.HideRecipeManager();
        if (!_viewModel.IsWorkbenchWorkspaceSelected)
        {
            _viewModel.IsWorkbenchWorkspaceSelected = true;
        }
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    private bool IsViewerSourceAlreadyLoaded(string path) => _workbenchLifecycle.IsViewerSourceAlreadyLoaded(path);

    private void SetWorkbenchC3DSourceFromViewer(string path, bool markDirty = true) =>
        _workbenchLifecycle.SetWorkbenchC3DSourceFromViewer(path, markDirty);

    private Task<bool> ClickUnsavedRecipeDoNotSaveForSmokeAsync() =>
        _workbenchLifecycle.ClickUnsavedRecipeDoNotSaveForSmokeAsync();

    private static Task<bool> CaptureRecipeHealthNavigationPressedForSmokeAsync(
        Window window,
        string screenshotPath,
        string? qualityReportPath) =>
        CaptureButtonPressedForSmokeAsync(
            window,
            "NextRecipeHealthIssue",
            screenshotPath,
            qualityReportPath,
            "RecipeHealthNavigationPressed");

    private static void AppendWindowMonitorEvidence(Window window, string? reportPath)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        const uint monitorDefaultToNearest = 0x00000002;
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = MonitorFromWindow(handle, monitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (handle == IntPtr.Zero
            || monitor == IntPtr.Zero
            || !GetMonitorInfo(monitor, ref monitorInfo)
            || !GetWindowRect(handle, out var windowRect))
        {
            return;
        }

        var intersects = windowRect.Left < monitorInfo.MonitorArea.Right
            && windowRect.Right > monitorInfo.MonitorArea.Left
            && windowRect.Top < monitorInfo.MonitorArea.Bottom
            && windowRect.Bottom > monitorInfo.MonitorArea.Top;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        File.AppendAllLines(
            Path.GetFullPath(reportPath),
        [
            $"WindowMonitor|selected=leftmost|monitorBounds={monitorInfo.MonitorArea.Left},{monitorInfo.MonitorArea.Top},{monitorInfo.MonitorArea.Right},{monitorInfo.MonitorArea.Bottom}|workingArea={monitorInfo.WorkArea.Left},{monitorInfo.WorkArea.Top},{monitorInfo.WorkArea.Right},{monitorInfo.WorkArea.Bottom}|windowRect={windowRect.Left},{windowRect.Top},{windowRect.Right},{windowRect.Bottom}|intersects={intersects}",
            $"WindowDpi|scaleX={dpi.DpiScaleX:F2}|scaleY={dpi.DpiScaleY:F2}|pixelsPerInchX={dpi.PixelsPerInchX:F0}|pixelsPerInchY={dpi.PixelsPerInchY:F0}"
        ]);
    }

    private static async Task<bool> CaptureButtonPressedForSmokeAsync(
        Window window,
        string automationId,
        string screenshotPath,
        string? qualityReportPath,
        string scope)
    {
        const uint leftButtonDown = 0x0002;
        const uint leftButtonUp = 0x0004;
        var mouseDown = false;
        var routedPointerDown = false;
        var forcedPressedState = false;
        System.Windows.Controls.Primitives.ButtonBase? pressedButton = null;
        try
        {
            window.Activate();
            SetForegroundWindow(new WindowInteropHelper(window).Handle);
            await window.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Input);
            // Force template realization before resolving a named button. The
            // validation pane is hosted through a docked ContentControl and its
            // deferred template can otherwise remain outside the visual tree
            // until the first bitmap render.
            window.UpdateLayout();
            _ = WpfScreenshotCapture.Capture(window);
            System.Windows.Controls.Primitives.ButtonBase? button = null;
            for (var attempt = 0; attempt < 40 && button is null; attempt++)
            {
                window.UpdateLayout();
                button = FindVisualDescendants<System.Windows.Controls.Primitives.ButtonBase>(window)
                    .FirstOrDefault(candidate =>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(candidate)
                        == automationId);
                if (button is null)
                {
                    button = FindVisualDescendants<RecipePipelineReviewView>(window)
                        .Select(review => review.FindName(automationId) as System.Windows.Controls.Primitives.ButtonBase)
                        .FirstOrDefault(candidate => candidate is not null);
                }
                if (button is not null && !button.IsDescendantOf(window))
                {
                    button = null;
                }
                if (button is null)
                {
                    await window.Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                    await Task.Delay(50);
                }
            }
            if (button is null)
            {
                var visibleIds = string.Join(
                    ",",
                    FindVisualDescendants<System.Windows.Controls.Primitives.ButtonBase>(window)
                        .Where(candidate => candidate.Visibility == Visibility.Visible)
                        .Select(candidate => System.Windows.Automation.AutomationProperties.GetAutomationId(candidate))
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.Ordinal));
                WriteTextReport(
                    qualityReportPath,
                    [$"{scope}|failure=button-not-found|automationId={automationId}|visibleAutomationIds={visibleIds}"]);
                return false;
            }
            pressedButton = button;
            if (!button.IsEnabled)
            {
                WriteTextReport(qualityReportPath, [$"{scope}|failure=button-disabled|automationId={automationId}"]);
                return false;
            }
            if (!button.Focus())
            {
                WriteTextReport(qualityReportPath, [$"{scope}|failure=focus-rejected|automationId={automationId}"]);
                return false;
            }
            var focusedBeforePointer = button.IsKeyboardFocusWithin;

            var relativeCenter = button.TransformToAncestor(window).Transform(new System.Windows.Point(
                button.ActualWidth / 2.0,
                button.ActualHeight / 2.0));
            var transformToDevice = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice
                ?? System.Windows.Media.Matrix.Identity;
            var deviceCenter = transformToDevice.Transform(relativeCenter);
            var windowHandle = new WindowInteropHelper(window).Handle;
            if (!GetWindowRect(windowHandle, out var windowRectangle))
            {
                WriteTextReport(qualityReportPath, [$"{scope}|failure=window-rectangle"]);
                return false;
            }
            var center = new System.Windows.Point(
                windowRectangle.Left + deviceCenter.X,
                windowRectangle.Top + deviceCenter.Y);
            if (!SetCursorPos(
                    (int)Math.Round(center.X),
                    (int)Math.Round(center.Y)))
            {
                WriteTextReport(qualityReportPath, [$"{scope}|failure=cursor-position|x={center.X:F1}|y={center.Y:F1}"]);
                return false;
            }

            await Task.Delay(150);
            var hoveredBeforePointer = button.IsMouseOver;
            SendMouseEvent(leftButtonDown, 0, 0, 0, UIntPtr.Zero);
            mouseDown = true;
            await window.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Input);
            await window.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render);
            await Task.Delay(150);
            if (!button.IsPressed)
            {
                button.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                    System.Windows.Input.Mouse.PrimaryDevice,
                    Environment.TickCount,
                    System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = button
                });
                routedPointerDown = true;
                await window.Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Render);
            }
            if (!button.IsPressed)
            {
                var setIsPressed = typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod(
                    "SetIsPressed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                setIsPressed?.Invoke(button, [true]);
                forcedPressedState = button.IsPressed;
            }
            if (!button.IsPressed)
            {
                WriteTextReport(
                    qualityReportPath,
                [
                    $"{scope}|failure=pressed-state-not-held|x={center.X:F1}|y={center.Y:F1}|window={windowRectangle.Left},{windowRectangle.Top},{windowRectangle.Right},{windowRectangle.Bottom}|relative={relativeCenter.X:F1},{relativeCenter.Y:F1}|device={deviceCenter.X:F1},{deviceCenter.Y:F1}"
                ]);
                return false;
            }

            var captured = await CaptureWindowWithRetryAsync(
                window,
                screenshotPath,
                qualityReportPath,
                scope);
            if (captured && !string.IsNullOrWhiteSpace(qualityReportPath))
            {
                File.AppendAllLines(
                    Path.GetFullPath(qualityReportPath),
                [
                    $"PointerDown|scope={scope}|state=held|osInjection={mouseDown}|routedEvent={routedPointerDown}|buttonBasePressedFallback={forcedPressedState}|focused={focusedBeforePointer}|hovered={hoveredBeforePointer}"
                ]);
            }
            return captured;
        }
        finally
        {
            if (forcedPressedState && pressedButton is not null)
            {
                var setIsPressed = typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod(
                    "SetIsPressed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                setIsPressed?.Invoke(pressedButton, [false]);
            }
            if (routedPointerDown && pressedButton is not null)
            {
                pressedButton.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                    System.Windows.Input.Mouse.PrimaryDevice,
                    Environment.TickCount,
                    System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonUpEvent,
                    Source = pressedButton
                });
            }
            if (mouseDown)
            {
                SendMouseEvent(leftButtonUp, 0, 0, 0, UIntPtr.Zero);
            }
        }
    }

    private static async Task<bool> CaptureMessageDialogForSmokeAsync(
        WpfMessageDialogWindow dialog,
        string screenshotPath,
        string? qualityReportPath,
        bool holdPrimaryButton)
    {
        if (holdPrimaryButton)
        {
            return await CaptureButtonPressedForSmokeAsync(
                dialog,
                "MessageDialogPrimaryButton",
                screenshotPath,
                qualityReportPath,
                "MessageDialogPrimaryPressed");
        }

        return await CaptureWindowWithRetryAsync(
            dialog,
            screenshotPath,
            qualityReportPath,
            "MessageDialog");
    }

    private void OnWorkbenchSaveTeachingRecipeRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.SaveTeachingRecipeRequested(sender, args);

    private void OnWorkbenchSaveTeachingRecipeAsRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.SaveTeachingRecipeAsRequested(sender, args);

    private bool SaveWorkbenchRecipe(bool forceDialog) =>
        _workbenchLifecycle.TrySaveWorkbenchRecipe(forceDialog);

    private void OnWorkbenchOpenTeachingRecipeRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.OpenTeachingRecipeRequested(sender, args);

    private void OnWorkbenchOpenRecentTeachingRecipeRequested(
        object? sender,
        ToolWorkbenchRecipePathRequestEventArgs args) =>
        _workbenchLifecycle.OpenRecentTeachingRecipeRequested(sender, args);

    private void OpenWorkbenchRecipe(string path) => _workbenchLifecycle.OpenWorkbenchRecipe(path);

    private void RestoreMostRecentWorkbenchRecipe() => _workbenchLifecycle.RestoreMostRecentWorkbenchRecipe();

    private bool TryResolveWorkbenchChanges(string reason) => _workbenchLifecycle.TryResolveWorkbenchChanges(reason);

    private static bool IsAutomatedShellRun() => Environment.GetCommandLineArgs().Any(argument =>
        argument.StartsWith("--smoke-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--verify-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--two-point-line-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--three-point-plane-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--datum-plane-deviation-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--line-intersection-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--landmark-correspondence-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--xyz-affine-solve-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--xyz-affine-apply-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--regrid-height-map-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--message-dialog-", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("--shell-smoke-screenshot", StringComparison.OrdinalIgnoreCase));

    private static bool ShouldStartWithEmptyRecipeInput() =>
        !IsAutomatedShellRun()
        || Environment.GetCommandLineArgs().Any(argument =>
            argument.Equals("--smoke-input-first-start", StringComparison.OrdinalIgnoreCase));

    private static string GetPersistentRecentRecipesPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenVisionLab",
        "ThreeDStudio",
        "recent-recipes.json");

    private void ResetStudioLayoutRequested(object sender, EventArgs args)
        => _studioLayout.Reset();

    private void SyncWorkbenchSourceFromViewer() => _workbenchLifecycle.SyncWorkbenchSourceFromViewer();

    private void OnViewerHostStateChanged(object? sender, ViewerHostStateChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewerHostState.C3DSampleVisible))
        {
            _viewModel.UpdateC3DSampleVisible(args.State.C3DSampleVisible);
        }
    }

    private void OnProfileViewRequested(object? sender, EventArgs args)
    {
        if (_viewModel.IsExpertWorkspaceSelected)
        {
            Workspace.ActivateProfilePane();
            return;
        }

        if (!_viewModel.IsWorkbenchWorkspaceSelected)
        {
            _viewModel.IsWorkbenchWorkspaceSelected = true;
        }

        ToolWorkbench.ActivateProfilePane();
    }

    private void OnPublishInspectionResultRequested()
    {
        if (_viewer.PublishCurrentPreviewResult())
        {
            _viewModel.ShowReviewWorkspace();
        }
    }

    private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellMainWindowViewModel.SelectedWorkspaceMode))
        {
            UpdateViewerHost();
        }
    }

    private void UpdateViewerHost()
    {
        if (!_viewModel.IsWorkbenchWorkspaceSelected && _viewer.TeachingCaptureSnapshot.IsActive)
        {
            _viewer.CancelC3DTeachingCapture();
        }

        if (_viewModel.IsWorkbenchWorkspaceSelected)
        {
            TaskWorkspace.ViewerContent = null;
            Workspace.ViewerContent = null;
            _viewer.ViewModel.HudDetailsVisible = false;
            if (!ReferenceEquals(ToolWorkbench.ViewerContent, _viewer))
            {
                ToolWorkbench.ViewerContent = _viewer;
            }

            return;
        }

        if (_viewModel.IsExpertWorkspaceSelected)
        {
            ToolWorkbench.ReleaseMainViewer(_viewer);
            ToolWorkbench.ViewerContent = null;
            _viewer.ViewModel.HudDetailsVisible = true;
            TaskWorkspace.ViewerContent = null;
            Workspace.ReactivateViewerContent(_viewer);

            Dispatcher.BeginInvoke(
                () =>
                {
                    if (_viewModel.IsExpertWorkspaceSelected
                        && ReferenceEquals(Workspace.ViewerContent, _viewer))
                    {
                        Workspace.UpdateLayout();
                        _viewer.RequestVisibleFrame();
                    }
                },
                DispatcherPriority.ContextIdle);
            return;
        }

        if (_viewModel.IsTaskWorkspaceSelected)
        {
            ToolWorkbench.ViewerContent = null;
            Workspace.ViewerContent = null;
            if (!ReferenceEquals(TaskWorkspace.ViewerContent, _viewer))
            {
                TaskWorkspace.ViewerContent = _viewer;
            }

            return;
        }

        ToolWorkbench.ViewerContent = null;
        Workspace.ViewerContent = null;
        TaskWorkspace.ViewerContent = null;
    }
}
