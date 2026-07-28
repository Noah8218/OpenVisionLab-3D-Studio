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
    private readonly EventHandler _profileViewRequestedHandler;
    private readonly EventHandler _refreshRecipeComparisonRequestedHandler;
    private readonly EventHandler _saveRecipeRequestedHandler;
    private readonly EventHandler _applyRoiAlignmentRequestedHandler;
    private readonly EventHandler _fitPlaneRequestedHandler;
    private readonly EventHandler _publishInspectionResultRequestedHandler;
    private readonly EventHandler _calibrationLoadStudyRequestedHandler;
    private readonly EventHandler<EvidenceArtifactOpenRequestEventArgs> _openEvidenceArtifactRequestedHandler;
    private readonly EventHandler _openRunRecordRequestedHandler;
    private readonly EventHandler _exportRunRecordRequestedHandler;
    private readonly EventHandler _workbenchNewTeachingRecipeRequestedHandler;
    private readonly EventHandler _workbenchSaveTeachingRecipeRequestedHandler;
    private readonly EventHandler _workbenchSaveTeachingRecipeAsRequestedHandler;
    private readonly EventHandler _workbenchOpenToolLibraryRequestedHandler;
    private readonly EventHandler _workbenchOpenTeachingRecipeRequestedHandler;
    private readonly EventHandler<ToolWorkbenchRecipePathRequestEventArgs> _workbenchOpenRecentTeachingRecipeRequestedHandler;
    private readonly EventHandler _workbenchLoadC3DSourceRequestedHandler;
    private readonly EventHandler _workbenchCancelC3DSourceLoadRequestedHandler;
    private readonly WorkbenchViewerTeachingCoordinator _workbenchViewerTeaching;
    private readonly EventHandler<ToolWorkbenchToolLabRequestEventArgs> _workbenchToolLabRequestedHandler;
    private readonly EventHandler _workbenchSelectValidationSetSourcesRequestedHandler;
    private readonly EventHandler _workbenchValidationSetComparisonRequestedHandler;
    private readonly WorkbenchViewerDisplayCoordinator _workbenchViewerDisplay;
    private readonly PropertyChangedEventHandler _viewModelPropertyChangedHandler;
    private readonly EventHandler _inspectionTaskChangedHandler;
    private RecipeManagerWindow? recipeManagerWindow;
    private readonly ToolLabWindowManager _toolLabWindows;
    private CancellationTokenSource? c3dSourceLoadCancellation;
    private double lastWorkbenchSourceBindingMilliseconds;
    private RoutedEventHandler _shellSmokeLoadedHandler = (_, _) => { };

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
        _profileViewRequestedHandler = OnProfileViewRequested;
        _viewer.ProfileViewRequested += _profileViewRequestedHandler;
        _viewer.EnableSmokeFromCommandLine(ownsApplicationLifecycle: false);

        _refreshRecipeComparisonRequestedHandler = (_, _) => _viewModel.RefreshRecipeComparison();
        _saveRecipeRequestedHandler = (_, _) => _viewer.SaveCurrentRecipeWithDialog();
        _applyRoiAlignmentRequestedHandler = (_, _) => _viewer.ApplyRoiReferenceAlignment();
        _fitPlaneRequestedHandler = (_, _) => _viewer.FitC3DReferencePlane();
        _publishInspectionResultRequestedHandler = (_, _) => OnPublishInspectionResultRequested();
        _calibrationLoadStudyRequestedHandler = OnCalibrationLoadStudyRequested;
        _openEvidenceArtifactRequestedHandler = OnOpenEvidenceArtifactRequested;
        _openRunRecordRequestedHandler = OnOpenRunRecordRequested;
        _exportRunRecordRequestedHandler = OnExportRunRecordRequested;
        _workbenchNewTeachingRecipeRequestedHandler = OnWorkbenchNewTeachingRecipeRequested;
        _workbenchSaveTeachingRecipeRequestedHandler = OnWorkbenchSaveTeachingRecipeRequested;
        _workbenchSaveTeachingRecipeAsRequestedHandler = OnWorkbenchSaveTeachingRecipeAsRequested;
        _workbenchOpenToolLibraryRequestedHandler = OnWorkbenchOpenToolLibraryRequested;
        _workbenchOpenTeachingRecipeRequestedHandler = OnWorkbenchOpenTeachingRecipeRequested;
        _workbenchOpenRecentTeachingRecipeRequestedHandler = OnWorkbenchOpenRecentTeachingRecipeRequested;
        _workbenchLoadC3DSourceRequestedHandler = OnWorkbenchLoadC3DSourceRequested;
        _workbenchCancelC3DSourceLoadRequestedHandler = OnWorkbenchCancelC3DSourceLoadRequested;
        _workbenchToolLabRequestedHandler = OnWorkbenchToolLabRequested;
        _workbenchSelectValidationSetSourcesRequestedHandler = OnWorkbenchSelectValidationSetSourcesRequested;
        _workbenchValidationSetComparisonRequestedHandler = OnWorkbenchValidationSetComparisonRequested;
        _viewModel.RefreshRecipeComparisonRequested += _refreshRecipeComparisonRequestedHandler;
        _viewModel.SaveRecipeRequested += _saveRecipeRequestedHandler;
        _viewModel.ApplyRoiAlignmentRequested += _applyRoiAlignmentRequestedHandler;
        _viewModel.FitPlaneRequested += _fitPlaneRequestedHandler;
        _viewModel.PublishInspectionResultRequested += _publishInspectionResultRequestedHandler;
        _viewModel.Calibration.LoadStudyRequested += _calibrationLoadStudyRequestedHandler;
        _viewModel.OpenEvidenceArtifactRequested += _openEvidenceArtifactRequestedHandler;
        _viewModel.OpenRunRecordRequested += _openRunRecordRequestedHandler;
        _viewModel.ExportRunRecordRequested += _exportRunRecordRequestedHandler;
        _viewModel.Workbench.NewTeachingRecipeRequested += _workbenchNewTeachingRecipeRequestedHandler;
        _viewModel.Workbench.SaveTeachingRecipeRequested += _workbenchSaveTeachingRecipeRequestedHandler;
        _viewModel.Workbench.SaveTeachingRecipeAsRequested += _workbenchSaveTeachingRecipeAsRequestedHandler;
        _viewModel.Workbench.OpenToolLibraryRequested += _workbenchOpenToolLibraryRequestedHandler;
        _viewModel.Workbench.OpenTeachingRecipeRequested += _workbenchOpenTeachingRecipeRequestedHandler;
        _viewModel.Workbench.OpenRecentTeachingRecipeRequested += _workbenchOpenRecentTeachingRecipeRequestedHandler;
        _viewModel.Workbench.LoadC3DSourceRequested += _workbenchLoadC3DSourceRequestedHandler;
        _viewModel.Workbench.CancelC3DSourceLoadRequested += _workbenchCancelC3DSourceLoadRequestedHandler;
        _viewModel.Workbench.ToolLabRequested += _workbenchToolLabRequestedHandler;
        _viewModel.Workbench.SelectValidationSetSourcesRequested += _workbenchSelectValidationSetSourcesRequestedHandler;
        _viewModel.Workbench.ValidationSetComparisonRequested += _workbenchValidationSetComparisonRequestedHandler;
        _workbenchViewerTeaching = new WorkbenchViewerTeachingCoordinator(
            _viewModel.Workbench,
            _viewer,
            () => ToolWorkbench.IsBottomPaneExpanded = false);
        _workbenchViewerDisplay = new WorkbenchViewerDisplayCoordinator(
            _viewModel,
            _viewer,
            _toolLabWindows,
            ToolWorkbench,
            Workspace,
            _workbenchViewerTeaching);

        ConfigureCalibrationStudyFromCommandLine();
        ConfigureToolTeachingRecipeFromCommandLine();
        RestoreMostRecentWorkbenchRecipe();
        ConfigureOutputCompareFromCommandLine();
        ConfigureWorkbenchBottomPaneFromCommandLine();
        ConfigureValidationSetFromCommandLine();
        ConfigureC3DSourceLoadProgressFromCommandLine();
        _workbenchViewerTeaching.SyncAppliedSelections();
        Loaded += ConfigureViewerViewFromCommandLine;
        Loaded += EnsureWorkbenchViewerSourceConsistency;
        EnableShellSmokeFromCommandLine();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!IsAutomatedShellRun() && !TryResolveWorkbenchChanges("closing 3D Studio"))
        {
            e.Cancel = true;
            return;
        }

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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

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
        _viewer.ProfileViewRequested -= _profileViewRequestedHandler;
        _viewModel.RefreshRecipeComparisonRequested -= _refreshRecipeComparisonRequestedHandler;
        _viewModel.SaveRecipeRequested -= _saveRecipeRequestedHandler;
        _viewModel.ApplyRoiAlignmentRequested -= _applyRoiAlignmentRequestedHandler;
        _viewModel.FitPlaneRequested -= _fitPlaneRequestedHandler;
        _viewModel.PublishInspectionResultRequested -= _publishInspectionResultRequestedHandler;
        _viewModel.Calibration.LoadStudyRequested -= _calibrationLoadStudyRequestedHandler;
        _viewModel.OpenEvidenceArtifactRequested -= _openEvidenceArtifactRequestedHandler;
        _viewModel.OpenRunRecordRequested -= _openRunRecordRequestedHandler;
        _viewModel.ExportRunRecordRequested -= _exportRunRecordRequestedHandler;
        _viewModel.Workbench.NewTeachingRecipeRequested -= _workbenchNewTeachingRecipeRequestedHandler;
        _viewModel.Workbench.SaveTeachingRecipeRequested -= _workbenchSaveTeachingRecipeRequestedHandler;
        _viewModel.Workbench.SaveTeachingRecipeAsRequested -= _workbenchSaveTeachingRecipeAsRequestedHandler;
        _viewModel.Workbench.OpenToolLibraryRequested -= _workbenchOpenToolLibraryRequestedHandler;
        _viewModel.Workbench.OpenTeachingRecipeRequested -= _workbenchOpenTeachingRecipeRequestedHandler;
        _viewModel.Workbench.OpenRecentTeachingRecipeRequested -= _workbenchOpenRecentTeachingRecipeRequestedHandler;
        _viewModel.Workbench.LoadC3DSourceRequested -= _workbenchLoadC3DSourceRequestedHandler;
        _viewModel.Workbench.CancelC3DSourceLoadRequested -= _workbenchCancelC3DSourceLoadRequestedHandler;
        _viewModel.Workbench.ToolLabRequested -= _workbenchToolLabRequestedHandler;
        _viewModel.Workbench.SelectValidationSetSourcesRequested -= _workbenchSelectValidationSetSourcesRequestedHandler;
        _viewModel.Workbench.ValidationSetComparisonRequested -= _workbenchValidationSetComparisonRequestedHandler;
        _workbenchViewerDisplay.Dispose();
        _workbenchViewerTeaching.Dispose();
        _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        _viewModel.InspectionTaskChanged -= _inspectionTaskChangedHandler;
        Loaded -= _shellSmokeLoadedHandler;
        Loaded -= EnsureWorkbenchViewerSourceConsistency;
        c3dSourceLoadCancellation?.Cancel();
        c3dSourceLoadCancellation?.Dispose();
        c3dSourceLoadCancellation = null;
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
        var newRecipeLifecycleSmokeReportPath = smoke.NewRecipeLifecycleSmokeReportPath;
        var openRecipeLifecycleSmokePath = smoke.OpenRecipeLifecycleSmokePath;
        var openRecipeLifecycleSmokeReportPath = smoke.OpenRecipeLifecycleSmokeReportPath;
        var asyncC3DLoadSmokePath = smoke.AsyncC3DLoadSmokePath;
        var asyncC3DLoadSmokeReportPath = smoke.AsyncC3DLoadSmokeReportPath;
        var asyncC3DLoadCancelAt = smoke.AsyncC3DLoadCancelAt;
        var asyncC3DLoadExpectFailure = smoke.AsyncC3DLoadExpectFailure;
        var sourceQualitySmokeReportPath = smoke.SourceQualitySmokeReportPath;
        var heightImagePaletteSmoke = smoke.HeightImagePaletteSmoke;
        var heightImageRangeMinimumSmoke = smoke.HeightImageRangeMinimumSmoke;
        var heightImageRangeMaximumSmoke = smoke.HeightImageRangeMaximumSmoke;
        var heightImageDisplayRangeSmokeReportPath =
            smoke.HeightImageDisplayRangeSmokeReportPath;
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
        var smokeSelectToolId = smoke.SmokeSelectToolId;
        var workbenchInteractionReportPath = smoke.WorkbenchInteractionReportPath;
        var filterPublishSmoke = smoke.FilterPublishSmoke;
        var twoPointLinePublishSmoke = smoke.TwoPointLinePublishSmoke;
        var twoPointLinePreviewSmoke = smoke.TwoPointLinePreviewSmoke;
        var threePointPlanePublishSmoke = smoke.ThreePointPlanePublishSmoke;
        var threePointPlanePreviewSmoke = smoke.ThreePointPlanePreviewSmoke;
        var datumPlaneDeviationPublishSmoke = smoke.DatumPlaneDeviationPublishSmoke;
        var datumPlaneDeviationPreviewSmoke = smoke.DatumPlaneDeviationPreviewSmoke;
        var filterPreviewSmoke = smoke.FilterPreviewSmoke;
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

        var smokePublishResult = smoke.SmokePublishResult;
        var waitForNominalActualPreview = smoke.WaitForNominalActualPreview
            || _viewer.ViewModel.NominalActualInput is not null;
        if (smoke.ShouldAttachLoadedHandler(_viewer.HasConfiguredSmokeScreenshot))
        {
            _shellSmokeLoadedHandler = async (_, _) =>
            {
                await Dispatcher.InvokeAsync(() => { });
                if (asyncC3DLoadSmokePath is not null
                    && !await ShellAsyncC3DLoadSmoke.RunAsync(
                        _viewer,
                        _viewModel.Workbench,
                        Dispatcher,
                        asyncC3DLoadSmokePath,
                        asyncC3DLoadSmokeReportPath,
                        asyncC3DLoadCancelAt,
                        asyncC3DLoadExpectFailure,
                        path => LoadWorkbenchC3DSourceAsync(path, showFailureDialog: false),
                        IsViewerSourceAlreadyLoaded,
                        () => lastWorkbenchSourceBindingMilliseconds))
                {
                    _viewModel.SetViewerSmokeFailed("Asynchronous C3D load smoke did not keep the Dispatcher responsive or activate the target source.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (smoke.SourceQualitySmoke
                    && !await RunSourceQualitySmokeAsync(sourceQualitySmokeReportPath))
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Source Quality did not become ready or changed authored/execution state.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (newRecipeLifecycleSmokePath is not null
                    && !await ShellRecipeLifecycleSmoke.RunNewAsync(
                        _viewModel,
                        _viewer,
                        newRecipeLifecycleSmokePath,
                        newRecipeLifecycleSmokeReportPath,
                        ShowRecipeManagerWindow,
                        ClickUnsavedRecipeDoNotSaveForSmokeAsync))
                {
                    _viewModel.SetViewerSmokeFailed("New recipe lifecycle smoke did not create and open a clean zero-step recipe.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (openRecipeLifecycleSmokePath is not null
                    && !ShellRecipeLifecycleSmoke.RunOpen(
                        _viewModel,
                        openRecipeLifecycleSmokePath,
                        openRecipeLifecycleSmokeReportPath,
                        ShowRecipeManagerWindow,
                        OpenWorkbenchRecipe,
                        () => recipeManagerWindow?.IsVisible == true,
                        IsViewerSourceAlreadyLoaded))
                {
                    _viewModel.SetViewerSmokeFailed("Open recipe lifecycle smoke did not activate the saved recipe in Workbench.");
                    Application.Current.Shutdown(1);
                    return;
                }
                if (recipeManagerScreenshotPath is not null)
                {
                    ShowRecipeManagerWindow();
                    var firstRecipeManagerWindow = recipeManagerWindow;
                    ShowRecipeManagerWindow();
                    if (!ReferenceEquals(firstRecipeManagerWindow, recipeManagerWindow))
                    {
                        _viewModel.SetViewerSmokeFailed("Recipe Manager smoke opened more than one window instance.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (messageDialogScreenshotPath is not null)
                {
                    messageDialogSmokeWindow = new WpfMessageDialogWindow(new WpfMessageDialogOptions
                    {
                        Title = DialogText("ThreeD.Dialog.RecipeSave.Title", "레시피 저장", "Save Recipe"),
                        Message = DialogText(
                            "ThreeD.Dialog.RecipeSave.Failed",
                            "레시피 파일을 저장할 수 없습니다. 표시된 파일 또는 구조 오류를 확인하세요.",
                            "The recipe file could not be saved. Check the listed file or structural error."),
                        Details = "Access to the selected recipe folder was denied.",
                        Kind = WpfMessageDialogKind.Warning,
                        Buttons = WpfMessageDialogButtons.OK
                    })
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

                if (viewerLayoutSmoke is not null)
                {
                    if (!ToolWorkbench.ConfigureViewerWorkspaceLayoutForSmoke(viewerLayoutSmoke))
                    {
                        _viewModel.SetViewerSmokeFailed(
                            $"Viewer workspace layout smoke could not activate '{viewerLayoutSmoke}'.");
                        Application.Current.Shutdown(1);
                        return;
                    }

                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                    await Task.Delay(600);
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
                            new ToolRecipeGridRectangle(285, 290, 135, 16),
                            "AcrossColumns",
                            "Rising",
                            "100",
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

                if (!_viewer.ApplyConfiguredSmokeNextDensity())
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

                var workbenchUiApplyStarted = Stopwatch.GetTimestamp();
                UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var workbenchUiApplyMilliseconds = Stopwatch.GetElapsedTime(workbenchUiApplyStarted).TotalMilliseconds;
                if (workbenchInteractionReportPath is not null)
                {
                    var fullReportPath = Path.GetFullPath(workbenchInteractionReportPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
                    File.WriteAllLines(fullReportPath,
                    [
                        "OpenVisionLab 3D Workbench interaction timing",
                        "Boundary|Local Release EXE smoke timing; this is not a broad hardware benchmark.",
                        $"Tool|id={smokeSelectToolId ?? "(none)"}|selected={_viewModel.Workbench.SelectedTool?.Id ?? "(none)"}|step={_viewModel.Workbench.SelectedPipelineStep?.Id ?? "(none)"}",
                        $"Timing|toolSelectionMs={_viewModel.Workbench.LastToolSelectionMilliseconds:F3}|toolAddMs={_viewModel.Workbench.LastToolAddMilliseconds:F3}|stepSelectionMs={_viewModel.Workbench.LastStepSelectionMilliseconds:F3}|uiApplyMs={workbenchUiApplyMilliseconds:F3}",
                        $"RecipeRefresh|totalMs={_viewModel.Workbench.LastRecipeRefreshMilliseconds:F3}|validationMs={_viewModel.Workbench.LastRecipeValidationMilliseconds:F3}|entityRebuildMs={_viewModel.Workbench.LastRecipeEntityRebuildMilliseconds:F3}|executionStateMs={_viewModel.Workbench.LastRecipeExecutionStateMilliseconds:F3}|notificationMs={_viewModel.Workbench.LastRecipeNotificationMilliseconds:F3}",
                        $"Budget|toolSelection50ms={_viewModel.Workbench.LastToolSelectionMilliseconds <= 50.0}|toolAdd150ms={_viewModel.Workbench.LastToolAddMilliseconds <= 150.0}|stepSelection150ms={_viewModel.Workbench.LastStepSelectionMilliseconds <= 150.0}|uiApply150ms={workbenchUiApplyMilliseconds <= 150.0}",
                        $"Recipe|steps={_viewModel.Workbench.PipelineSteps.Count}|state={_viewModel.Workbench.SelectedPipelineStep?.State ?? "(none)"}|publishAvailable={_viewModel.Workbench.PublishSelectedStepCommand.CanExecute(null)}"
                    ]);
                }
                await Task.Delay(100);
                if (shellScreenshotPath is not null
                    && !await CaptureWindowWithRetryAsync(this, shellScreenshotPath, screenshotQualityReportPath, "Shell"))
                {
                    _viewModel.SetViewerSmokeFailed("Shell screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
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
                    && (recipeManagerWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            recipeManagerWindow,
                            recipeManagerScreenshotPath,
                            recipeManagerScreenshotQualityReportPath,
                            "RecipeManager")))
                {
                    _viewModel.SetViewerSmokeFailed("Recipe Manager screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (messageDialogScreenshotPath is not null
                    && (messageDialogSmokeWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            messageDialogSmokeWindow,
                            messageDialogScreenshotPath,
                            messageDialogScreenshotQualityReportPath,
                            "MessageDialog")))
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

    private async Task<bool> RunSourceQualitySmokeAsync(string? reportPath)
    {
        var quality = _viewModel.Workbench.SourceQuality;
        var timeout = Stopwatch.StartNew();
        while (!quality.HasReport
               && !quality.HasError
               && timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(25);
        }

        var beforeDirty = _viewModel.Workbench.IsDirty;
        var beforeStepCount = _viewModel.Workbench.PipelineSteps.Count;
        var beforeSelectionCount = _viewModel.Workbench.Selections.Count;
        var beforeLogCount = _viewModel.Workbench.RunLog.Count;
        var beforePreviewRunning = _viewModel.Workbench.IsSelectedStepPreviewRunning;
        _viewModel.Workbench.SelectSourceQualityCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { });

        var passed = quality.HasReport
                     && !quality.IsLoading
                     && !quality.HasError
                     && _viewModel.Workbench.IsSourceQualityWorkspaceVisible
                     && !_viewModel.Workbench.HasSelectedPipelineStep
                     && _viewModel.Workbench.IsDirty == beforeDirty
                     && _viewModel.Workbench.PipelineSteps.Count == beforeStepCount
                     && _viewModel.Workbench.Selections.Count == beforeSelectionCount
                     && _viewModel.Workbench.RunLog.Count == beforeLogCount
                     && _viewModel.Workbench.IsSelectedStepPreviewRunning == beforePreviewRunning;

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var sourceReport = quality.Report;
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"SourceQualityWorkspaceSmoke|{(passed ? "Pass" : "Fail")}|viewOnly=true|recipeChanged=false|inspectionRun=false",
                    $"State|loading={quality.IsLoading}|hasReport={quality.HasReport}|hasError={quality.HasError}|visible={_viewModel.Workbench.IsSourceQualityWorkspaceVisible}|selectedStep={_viewModel.Workbench.SelectedPipelineStep?.Id ?? "(none)"}",
                    $"Source|name={quality.SourceName}|grid={sourceReport?.Grid.Width ?? 0}x{sourceReport?.Grid.Height ?? 0}|cells={sourceReport?.Grid.CellCount ?? 0}|valid={sourceReport?.Coverage.ValidSampleCount ?? 0}|validRatio={sourceReport?.Coverage.ValidRatio ?? 0:R}|missing={sourceReport?.Coverage.MissingSampleCount ?? 0}|missingRatio={sourceReport?.Coverage.MissingRatio ?? 0:R}",
                    $"Height|min={sourceReport?.Height.Minimum?.ToString("R") ?? "null"}|max={sourceReport?.Height.Maximum?.ToString("R") ?? "null"}|mean={sourceReport?.Height.Mean?.ToString("R") ?? "null"}|bins={sourceReport?.Height.Distribution?.BinCount ?? 0}|peak={sourceReport?.Height.Distribution?.PeakBinIndex ?? -1}",
                    $"Mask|bytes={sourceReport?.Coverage.InvalidCellMask.ByteLength ?? 0}|sha256={quality.MaskSha256}",
                    $"Channels|count={quality.Channels.Count}|available={string.Join(',', quality.Channels.Where(channel => channel.IsAvailable).Select(channel => channel.Name))}",
                    $"Boundary|dirty={beforeDirty}->{_viewModel.Workbench.IsDirty}|steps={beforeStepCount}->{_viewModel.Workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{_viewModel.Workbench.Selections.Count}|logs={beforeLogCount}->{_viewModel.Workbench.RunLog.Count}|previewRunning={beforePreviewRunning}->{_viewModel.Workbench.IsSelectedStepPreviewRunning}",
                    $"Error|{quality.Error}"
                ]);
        }

        return passed;
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
            return false;
        }

        var nativePixelSha256 = heightImage.Frame!.PixelSha256;
        heightImage.SelectedPalette = requestedPalette;
        var rangeApplied = heightImage.TryApplyManualRange(
            requestedMinimum,
            requestedMaximum);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        var passed = rangeApplied
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
                    $"Boundary|dirty={beforeDirty}->{_viewModel.Workbench.IsDirty}|steps={beforeStepCount}->{_viewModel.Workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{_viewModel.Workbench.Selections.Count}|logs={beforeLogCount}->{_viewModel.Workbench.RunLog.Count}|previewRunning={beforePreviewRunning}->{_viewModel.Workbench.IsSelectedStepPreviewRunning}|outputSame={ReferenceEquals(_viewModel.Workbench.CurrentMeasurementOutput, beforeOutput)}",
                    $"Error|{heightImage.RangeError}"
                ]);
        }

        return passed;
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
    }

    private void ConfigureViewerViewFromCommandLine(object sender, RoutedEventArgs e)
    {
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
        ToolWorkbench.IsBottomPaneExpanded = true;
        ToolWorkbench.ActivateValidationSet();
        if (Environment.GetCommandLineArgs().Contains("--smoke-validation-set-run", StringComparer.OrdinalIgnoreCase))
        {
            _viewModel.Workbench.RunValidationSetCommand.Execute(null);
            if (Environment.GetCommandLineArgs().Contains(
                    "--smoke-validation-set-open-compare",
                    StringComparer.OrdinalIgnoreCase))
            {
                _ = OpenValidationSetComparisonForSmokeAsync();
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

    private async void OnWorkbenchLoadC3DSourceRequested(object? sender, EventArgs args)
    {
        var dialog = new OpenFileDialog
        {
            Title = DialogText("ThreeD.FileDialog.LoadC3D.Title", "레시피 티칭용 C3D 입력 불러오기", "Load C3D Input for Recipe Teaching"),
            Filter = DialogText("ThreeD.FileDialog.LoadC3D.Filter", "C3D 높이 맵 (*.C3D)|*.C3D|모든 파일 (*.*)|*.*", "C3D height map (*.C3D)|*.C3D|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (IsViewerSourceAlreadyLoaded(dialog.FileName))
        {
            SetWorkbenchC3DSourceFromViewer(Path.GetFullPath(dialog.FileName));
            _viewer.ViewModel.HudDetailsVisible = false;
            return;
        }

        await LoadWorkbenchC3DSourceAsync(dialog.FileName);
    }

    private async Task LoadWorkbenchC3DSourceAsync(string path, bool showFailureDialog = true)
    {
        var cancellation = new CancellationTokenSource();
        c3dSourceLoadCancellation = cancellation;
        lastWorkbenchSourceBindingMilliseconds = 0.0;
        var stopwatch = Stopwatch.StartNew();
        _viewModel.Workbench.BeginC3DSourceLoad(path);
        var progress = new Progress<double>(_viewModel.Workbench.ReportC3DSourceLoadProgress);

        try
        {
            if (await _viewer.LoadC3DSourceAsync(path, cancellation.Token, progress)
                && _viewer.CurrentC3DSourcePath is { } sourcePath)
            {
                SetWorkbenchC3DSourceFromViewer(sourcePath);
                _viewer.ViewModel.HudDetailsVisible = false;
                _viewModel.Workbench.CompleteC3DSourceLoad(sourcePath, stopwatch.ElapsedMilliseconds);
                return;
            }

            _viewModel.Workbench.FailC3DSourceLoad(path, stopwatch.ElapsedMilliseconds);
            if (showFailureDialog)
            {
                ShowLoadSourceFailure(_viewer.HostState.ViewerStatus);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _viewModel.Workbench.CancelC3DSourceLoad(stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            if (ReferenceEquals(c3dSourceLoadCancellation, cancellation))
            {
                c3dSourceLoadCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    private void OnWorkbenchCancelC3DSourceLoadRequested(object? sender, EventArgs args)
    {
        c3dSourceLoadCancellation?.Cancel();
    }

    private void OpenRecipeManagerRequested(object? sender, EventArgs args)
    {
        ShowRecipeManagerWindow();
    }

    private void ShowRecipeManagerWindow()
    {
        if (recipeManagerWindow is null)
        {
            recipeManagerWindow = new RecipeManagerWindow
            {
                Owner = this,
                DataContext = _viewModel.Workbench
            };
            recipeManagerWindow.Closed += (_, _) => recipeManagerWindow = null;
        }

        recipeManagerWindow.Show();
        recipeManagerWindow.Activate();
    }

    private void OnWorkbenchOpenToolLibraryRequested(object? sender, EventArgs args)
    {
        recipeManagerWindow?.Close();
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

    private void OnWorkbenchNewTeachingRecipeRequested(object? sender, EventArgs args)
    {
        if (!TryResolveWorkbenchChanges("creating a new recipe"))
        {
            return;
        }

        var path = SelectNewWorkbenchRecipePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _viewModel.Workbench.CreateNewTeachingRecipe(GetRecipeNameFromPath(path));
        if (!_viewModel.Workbench.TrySaveTeachingRecipe(path, out var message))
        {
            ShowRecipeSaveFailure(message);
            return;
        }

        _viewer.ClearC3DTeachingSource(_viewModel.Workbench.LocalizedSourceReadinessSummary);
        _viewModel.UpdateC3DSampleVisible(false);
        ActivateWorkbenchAfterRecipeLifecycle();
    }

    private string? SelectNewWorkbenchRecipePath()
    {
        var smokePath = GetCommandLineValue("--smoke-new-recipe-lifecycle");
        if (!string.IsNullOrWhiteSpace(smokePath))
        {
            return Path.GetFullPath(smokePath);
        }

        var dialog = new SaveFileDialog
        {
            Title = DialogText("ThreeD.FileDialog.CreateRecipe.Title", "새 3D 검사 레시피 만들기", "Create New 3D Inspection Recipe"),
            Filter = DialogText("ThreeD.FileDialog.SaveRecipe.Filter", "OpenVisionLab 3D 검사 레시피 (*.ov3d-recipe.json)|*.ov3d-recipe.json|기존 티칭 레시피 (*.ov3d-teach.json)|*.ov3d-teach.json|JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*", "OpenVisionLab 3D inspection recipe (*.ov3d-recipe.json)|*.ov3d-recipe.json|Legacy teaching recipe (*.ov3d-teach.json)|*.ov3d-teach.json|JSON files (*.json)|*.json|All files (*.*)|*.*"),
            FileName = "new-inspection.ov3d-recipe.json",
            OverwritePrompt = true
        };
        return dialog.ShowDialog(GetRecipeLifecycleDialogOwner()) == true
            ? dialog.FileName
            : null;
    }

    private static string GetRecipeNameFromPath(string path)
    {
        const string currentSuffix = ".ov3d-recipe.json";
        const string legacySuffix = ".ov3d-teach.json";
        var fileName = Path.GetFileName(path);
        if (fileName.EndsWith(currentSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^currentSuffix.Length];
        }
        if (fileName.EndsWith(legacySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^legacySuffix.Length];
        }
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private Window GetRecipeLifecycleDialogOwner() =>
        recipeManagerWindow?.IsVisible == true ? recipeManagerWindow : this;

    private void ActivateWorkbenchAfterRecipeLifecycle()
    {
        recipeManagerWindow?.Hide();
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

    private bool IsViewerSourceAlreadyLoaded(string path)
    {
        if (_viewer.CurrentC3DSourcePath is not { } currentPath)
        {
            return false;
        }
        return string.Equals(
            Path.GetFullPath(currentPath),
            Path.GetFullPath(path),
            StringComparison.OrdinalIgnoreCase);
    }

    private void SetWorkbenchC3DSourceFromViewer(string path, bool markDirty = true)
    {
        var sourceBindingStart = Stopwatch.GetTimestamp();
        if (!_viewer.TryGetCurrentC3DSourceBinding(path, out var sourceBinding))
        {
            throw new InvalidOperationException(
                "The Viewer source identity is unavailable or does not match the requested C3D path.");
        }

        _viewModel.Workbench.SetC3DSourceFromLoadedViewer(path, sourceBinding, markDirty);
        lastWorkbenchSourceBindingMilliseconds =
            Stopwatch.GetElapsedTime(sourceBindingStart).TotalMilliseconds;
    }

    private async Task<bool> ClickUnsavedRecipeDoNotSaveForSmokeAsync()
    {
        var buttonText = DialogText(
            "ThreeD.Dialog.UnsavedRecipe.DoNotSave",
            "저장 안 함",
            "Don't Save");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            var clicked = await Dispatcher.InvokeAsync(() =>
            {
                var dialog = Application.Current.Windows
                    .OfType<WpfMessageDialogWindow>()
                    .FirstOrDefault(window => window.IsVisible);
                var button = dialog is null
                    ? null
                    : FindVisualDescendants<System.Windows.Controls.Button>(dialog)
                        .FirstOrDefault(candidate => string.Equals(candidate.Content?.ToString(), buttonText, StringComparison.Ordinal));
                if (button is null)
                {
                    return false;
                }
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                return true;
            });
            if (clicked)
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void OnWorkbenchSaveTeachingRecipeRequested(object? sender, EventArgs args)
    {
        if (TryResolveParameterDraft())
        {
            SaveWorkbenchRecipe(forceDialog: false);
        }
    }

    private void OnWorkbenchSaveTeachingRecipeAsRequested(object? sender, EventArgs args)
    {
        if (TryResolveParameterDraft())
        {
            SaveWorkbenchRecipe(forceDialog: true);
        }
    }

    private bool SaveWorkbenchRecipe(bool forceDialog)
    {
        var path = _viewModel.Workbench.RecipePath;
        if (forceDialog || string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Title = forceDialog
                    ? DialogText("ThreeD.FileDialog.SaveRecipeAs.Title", "3D 검사 레시피 다른 이름으로 저장", "Save 3D Inspection Recipe As")
                    : DialogText("ThreeD.FileDialog.SaveRecipe.Title", "3D 검사 레시피 저장", "Save 3D Inspection Recipe"),
                Filter = DialogText("ThreeD.FileDialog.SaveRecipe.Filter", "OpenVisionLab 3D 검사 레시피 (*.ov3d-recipe.json)|*.ov3d-recipe.json|기존 티칭 레시피 (*.ov3d-teach.json)|*.ov3d-teach.json|JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*", "OpenVisionLab 3D inspection recipe (*.ov3d-recipe.json)|*.ov3d-recipe.json|Legacy teaching recipe (*.ov3d-teach.json)|*.ov3d-teach.json|JSON files (*.json)|*.json|All files (*.*)|*.*"),
                FileName = string.IsNullOrWhiteSpace(path) ? "inspection-recipe.ov3d-recipe.json" : Path.GetFileName(path),
                InitialDirectory = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path),
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(GetRecipeLifecycleDialogOwner()) != true)
            {
                return false;
            }

            path = dialog.FileName;
        }

        if (_viewModel.Workbench.TrySaveTeachingRecipe(path, out var message))
        {
            return true;
        }

        ShowRecipeSaveFailure(message);
        return false;
    }

    private void OnWorkbenchOpenTeachingRecipeRequested(object? sender, EventArgs args)
    {
        if (!TryResolveWorkbenchChanges("opening another recipe"))
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = DialogText("ThreeD.FileDialog.OpenRecipe.Title", "3D 검사 레시피 열기", "Open 3D Inspection Recipe"),
            Filter = DialogText("ThreeD.FileDialog.OpenRecipe.Filter", "OpenVisionLab 3D 검사 레시피 (*.ov3d-recipe.json;*.ov3d-teach.json)|*.ov3d-recipe.json;*.ov3d-teach.json|JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*", "OpenVisionLab 3D inspection recipe (*.ov3d-recipe.json;*.ov3d-teach.json)|*.ov3d-recipe.json;*.ov3d-teach.json|JSON files (*.json)|*.json|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(GetRecipeLifecycleDialogOwner()) != true)
        {
            return;
        }

        OpenWorkbenchRecipe(dialog.FileName);
    }

    private void OnWorkbenchOpenRecentTeachingRecipeRequested(
        object? sender,
        ToolWorkbenchRecipePathRequestEventArgs args)
    {
        if (!TryResolveWorkbenchChanges("opening a recent recipe"))
        {
            return;
        }

        OpenWorkbenchRecipe(args.Path);
    }

    private void OpenWorkbenchRecipe(string path)
    {
        if (!File.Exists(path))
        {
            ShowRecipeFileUnavailable(path);
            return;
        }

        if (!_viewModel.Workbench.TryOpenTeachingRecipe(path, out var message))
        {
            ShowRecipeOpenFailure(message);
            return;
        }

        ActivateWorkbenchAfterRecipeLifecycle();

        var source = _viewModel.Workbench.Source;
        if (!_viewModel.Workbench.IsSourceReadyForRecipe)
        {
            _viewer.ClearC3DTeachingSource(_viewModel.Workbench.SourceReadinessSummary);
            _viewModel.UpdateC3DSampleVisible(false);
            ShowRecipeSourceNotReady();
            return;
        }

        if (IsViewerSourceAlreadyLoaded(source.Path))
        {
            _workbenchViewerTeaching.SyncAppliedSelections();
            return;
        }

        if (!_viewer.LoadC3DSource(source.Path))
        {
            var loadFailure = _viewer.HostState.ViewerStatus;
            _viewer.ClearC3DTeachingSource("Recipe source could not be loaded. Relink a valid C3D source.");
            _viewModel.UpdateC3DSampleVisible(false);
            ShowRecipeSourceLoadFailure(loadFailure);
            return;
        }

        if (_viewer.CurrentC3DSourcePath is { } loadedSourcePath)
        {
            SetWorkbenchC3DSourceFromViewer(loadedSourcePath);
            _workbenchViewerTeaching.SyncAppliedSelections();
        }
    }

    private void RestoreMostRecentWorkbenchRecipe()
    {
        if (IsAutomatedShellRun() || !string.IsNullOrWhiteSpace(_viewModel.Workbench.RecipePath))
        {
            return;
        }

        var path = _viewModel.Workbench.MostRecentAvailableRecipePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        OVLog.Write(
            LogCategory.UI,
            LogLevel.Info,
            $"Workbench[Open] Restoring most recent recipe | path={path} | preview=false | run=false | publish=false.");
        OpenWorkbenchRecipe(path);
    }

    private bool TryResolveWorkbenchChanges(string _)
    {
        if (!TryResolveParameterDraft())
        {
            return false;
        }

        if (!_viewModel.Workbench.IsDirty)
        {
            return true;
        }

        var result = ConfirmUnsavedRecipeChanges();
        return result switch
        {
            WpfMessageDialogResult.Yes => SaveWorkbenchRecipe(forceDialog: false),
            WpfMessageDialogResult.No => true,
            _ => false
        };
    }

    private bool TryResolveParameterDraft()
    {
        if (!_viewModel.Workbench.HasPendingStepParameterChanges)
        {
            return true;
        }

        var result = ConfirmPendingParameterChanges();
        if (result == WpfMessageDialogResult.Cancel)
        {
            return false;
        }

        if (result == WpfMessageDialogResult.No)
        {
            _viewModel.Workbench.DiscardSelectedStepParameterDraft();
            return true;
        }

        if (!ToolWorkbench.CommitPendingParameterEdit(out var message)
            || !_viewModel.Workbench.TryApplySelectedStepParameterDraft(out message))
        {
            _viewModel.Workbench.ReportParameterDraftCommitError(message);
            ShowParameterApplyFailure(message);
            return false;
        }

        return true;
    }

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

    private void SyncWorkbenchSourceFromViewer()
    {
        if (_viewer.CurrentC3DSourcePath is { } sourcePath
            && string.IsNullOrWhiteSpace(_viewModel.Workbench.Source.Path))
        {
            SetWorkbenchC3DSourceFromViewer(sourcePath, markDirty: false);
        }
    }

    private void OnOpenEvidenceArtifactRequested(object? sender, EvidenceArtifactOpenRequestEventArgs args)
    {
        if (!File.Exists(args.Path) && !Directory.Exists(args.Path))
        {
            ShowEvidenceArtifactMissing(args.Label, args.Path);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(args.Path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowEvidenceArtifactOpenFailure(args.Label, args.Path, ex.Message);
        }
    }

    private void OnOpenRunRecordRequested(object? sender, EventArgs args)
    {
        var english = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English;
        var dialog = new OpenFileDialog
        {
            Title = english ? "Open Run Record" : "\uC2E4\uD589 \uAE30\uB85D \uC5F4\uAE30",
            Filter = english
                ? "OpenVisionLab Run Record (*.json)|*.json|All files (*.*)|*.*"
                : "OpenVisionLab \uC2E4\uD589 \uAE30\uB85D (*.json)|*.json|\uBAA8\uB4E0 \uD30C\uC77C (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true
            && !_viewModel.LoadRunRecord(dialog.FileName, out var message))
        {
            ShowRunRecordOpenFailure(message);
        }
    }

    private void OnExportRunRecordRequested(object? sender, EventArgs args)
    {
        var english = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English;
        var dialog = new OpenFolderDialog
        {
            Title = english ? "Export Run Record Bundle" : "\uC2E4\uD589 \uAE30\uB85D \uBB36\uC74C \uB0B4\uBCF4\uB0B4\uAE30",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true
            && !_viewModel.ExportCurrentRunRecordBundle(dialog.FolderName, out var message))
        {
            ShowRunRecordExportFailure(message);
        }
    }

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
            ToolWorkbench.ViewerContent = null;
            _viewer.ViewModel.HudDetailsVisible = true;
            if (!ReferenceEquals(Workspace.ViewerContent, _viewer))
            {
                TaskWorkspace.ViewerContent = null;
                Workspace.ViewerContent = _viewer;
            }

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
