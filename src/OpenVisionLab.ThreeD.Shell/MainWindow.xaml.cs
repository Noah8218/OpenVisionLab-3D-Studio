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
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Recipe;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;
using WpfMessageDialogButtons = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogButtons;
using WpfMessageDialogKind = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogKind;
using WpfMessageDialogOptions = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogOptions;
using WpfMessageDialogResult = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogResult;
using WpfMessageDialogWindow = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogWindow;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
    private readonly EventHandler<ToolWorkbenchTeachingCaptureRequestEventArgs> _workbenchBeginTeachingCaptureRequestedHandler;
    private readonly EventHandler _workbenchUndoTeachingCaptureRequestedHandler;
    private readonly EventHandler _workbenchCancelTeachingCaptureRequestedHandler;
    private readonly EventHandler _workbenchApplyTeachingCaptureRequestedHandler;
    private readonly EventHandler _workbenchAppliedTeachingSelectionsChangedHandler;
    private readonly EventHandler<ToolWorkbenchToolLabRequestEventArgs> _workbenchToolLabRequestedHandler;
    private readonly EventHandler<ToolWorkbenchFilterDisplayRequestEventArgs> _workbenchFilterDisplayRequestedHandler;
    private readonly EventHandler<ToolWorkbenchArtifactDisplayRequestEventArgs> _workbenchArtifactDisplayRequestedHandler;
    private readonly EventHandler<ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs> _workbenchHeightDifferenceEdgeDisplayRequestedHandler;
    private readonly EventHandler<ToolWorkbenchTwoPointLineDisplayRequestEventArgs> _workbenchTwoPointLineDisplayRequestedHandler;
    private readonly EventHandler _workbenchTwoPointLineDisplayClearedHandler;
    private readonly EventHandler<ToolWorkbenchThreePointPlaneDisplayRequestEventArgs> _workbenchThreePointPlaneDisplayRequestedHandler;
    private readonly EventHandler _workbenchThreePointPlaneDisplayClearedHandler;
    private readonly EventHandler<ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs> _workbenchDatumPlaneDeviationDisplayRequestedHandler;
    private readonly EventHandler _workbenchDatumPlaneDeviationDisplayClearedHandler;
    private readonly EventHandler<ToolWorkbenchLineFitDisplayRequestEventArgs> _workbenchLineFitDisplayRequestedHandler;
    private readonly EventHandler _workbenchLineFitDisplayClearedHandler;
    private readonly EventHandler<ToolWorkbenchLineIntersectionDisplayRequestEventArgs> _workbenchLineIntersectionDisplayRequestedHandler;
    private readonly EventHandler _workbenchLineIntersectionDisplayClearedHandler;
    private readonly EventHandler<ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs> _workbenchLandmarkCorrespondenceDisplayRequestedHandler;
    private readonly EventHandler _workbenchLandmarkCorrespondenceDisplayClearedHandler;
    private readonly EventHandler<WorkbenchLineFitPointSelectedEventArgs> _viewerWorkbenchLineFitPointSelectedHandler;
    private readonly EventHandler<TeachingCaptureStateChangedEventArgs> _viewerTeachingCaptureStateChangedHandler;
    private readonly PropertyChangedEventHandler _viewModelPropertyChangedHandler;
    private readonly EventHandler _inspectionTaskChangedHandler;
    private RecipeManagerWindow? recipeManagerWindow;
    private FilterToolLabWindow? filterToolLabWindow;
    private HeightDifferenceEdgeToolLabWindow? heightDifferenceEdgeToolLabWindow;
    private TwoPointLineToolLabWindow? twoPointLineToolLabWindow;
    private ThreePointPlaneToolLabWindow? threePointPlaneToolLabWindow;
    private DatumPlaneDeviationToolLabWindow? datumPlaneDeviationToolLabWindow;
    private LineIntersectionToolLabWindow? lineIntersectionToolLabWindow;
    private LandmarkCorrespondenceToolLabWindow? landmarkCorrespondenceToolLabWindow;
    private XYZAffineSolveToolLabWindow? xyzAffineSolveToolLabWindow;
    private XYZAffineApplyToolLabWindow? xyzAffineApplyToolLabWindow;
    private RegridHeightMapToolLabWindow? regridHeightMapToolLabWindow;
    private CancellationTokenSource? c3dSourceLoadCancellation;
    private RoutedEventHandler _shellSmokeLoadedHandler = (_, _) => { };

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
            GetCommandLineValue("--csv-report"));
        _viewModel.SelectedEvidenceTabIndex = GetEvidenceTabIndex(GetCommandLineValue("--shell-evidence-tab"));
        DataContext = _viewModel;
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
        _workbenchBeginTeachingCaptureRequestedHandler = OnWorkbenchBeginTeachingCaptureRequested;
        _workbenchUndoTeachingCaptureRequestedHandler = OnWorkbenchUndoTeachingCaptureRequested;
        _workbenchCancelTeachingCaptureRequestedHandler = OnWorkbenchCancelTeachingCaptureRequested;
        _workbenchApplyTeachingCaptureRequestedHandler = OnWorkbenchApplyTeachingCaptureRequested;
        _workbenchAppliedTeachingSelectionsChangedHandler = (_, _) => SyncAppliedTeachingSelections();
        _workbenchToolLabRequestedHandler = OnWorkbenchToolLabRequested;
        _workbenchFilterDisplayRequestedHandler = OnWorkbenchFilterDisplayRequested;
        _workbenchArtifactDisplayRequestedHandler = OnWorkbenchArtifactDisplayRequested;
        _workbenchHeightDifferenceEdgeDisplayRequestedHandler = OnWorkbenchHeightDifferenceEdgeDisplayRequested;
        _workbenchTwoPointLineDisplayRequestedHandler = OnWorkbenchTwoPointLineDisplayRequested;
        _workbenchTwoPointLineDisplayClearedHandler = (_, _) => _viewer.ClearWorkbenchTwoPointLine();
        _workbenchThreePointPlaneDisplayRequestedHandler = OnWorkbenchThreePointPlaneDisplayRequested;
        _workbenchThreePointPlaneDisplayClearedHandler = (_, _) => _viewer.ClearWorkbenchThreePointPlane();
        _workbenchDatumPlaneDeviationDisplayRequestedHandler = OnWorkbenchDatumPlaneDeviationDisplayRequested;
        _workbenchDatumPlaneDeviationDisplayClearedHandler = (_, _) => _viewer.ClearWorkbenchDatumPlaneDeviation();
        _workbenchLineFitDisplayRequestedHandler = OnWorkbenchLineFitDisplayRequested;
        _workbenchLineFitDisplayClearedHandler = (_, _) => _viewer.ClearWorkbenchLineFit();
        _workbenchLineIntersectionDisplayRequestedHandler = OnWorkbenchLineIntersectionDisplayRequested;
        _workbenchLineIntersectionDisplayClearedHandler = (_, _) => _viewer.ClearWorkbenchLineIntersection();
        _workbenchLandmarkCorrespondenceDisplayRequestedHandler = OnWorkbenchLandmarkCorrespondenceDisplayRequested;
        _workbenchLandmarkCorrespondenceDisplayClearedHandler = (_, _) => _viewer.ClearWorkbenchLandmarkCorrespondence();
        _viewerWorkbenchLineFitPointSelectedHandler = (_, args) => _viewModel.Workbench.SelectLineFitDiagnostic(args.InputPointIndex);
        _viewerTeachingCaptureStateChangedHandler = OnViewerTeachingCaptureStateChanged;
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
        _viewModel.Workbench.BeginTeachingSelectionCaptureRequested += _workbenchBeginTeachingCaptureRequestedHandler;
        _viewModel.Workbench.UndoTeachingSelectionCaptureRequested += _workbenchUndoTeachingCaptureRequestedHandler;
        _viewModel.Workbench.CancelTeachingSelectionCaptureRequested += _workbenchCancelTeachingCaptureRequestedHandler;
        _viewModel.Workbench.ApplyTeachingSelectionCaptureRequested += _workbenchApplyTeachingCaptureRequestedHandler;
        _viewModel.Workbench.AppliedTeachingSelectionsChanged += _workbenchAppliedTeachingSelectionsChangedHandler;
        _viewModel.Workbench.ToolLabRequested += _workbenchToolLabRequestedHandler;
        _viewModel.Workbench.FilterDisplayRequested += _workbenchFilterDisplayRequestedHandler;
        _viewModel.Workbench.ViewerArtifactDisplayRequested += _workbenchArtifactDisplayRequestedHandler;
        _viewModel.Workbench.HeightDifferenceEdgeDisplayRequested += _workbenchHeightDifferenceEdgeDisplayRequestedHandler;
        _viewModel.Workbench.TwoPointLineDisplayRequested += _workbenchTwoPointLineDisplayRequestedHandler;
        _viewModel.Workbench.TwoPointLineDisplayCleared += _workbenchTwoPointLineDisplayClearedHandler;
        _viewModel.Workbench.ThreePointPlaneDisplayRequested += _workbenchThreePointPlaneDisplayRequestedHandler;
        _viewModel.Workbench.ThreePointPlaneDisplayCleared += _workbenchThreePointPlaneDisplayClearedHandler;
        _viewModel.Workbench.DatumPlaneDeviationDisplayRequested += _workbenchDatumPlaneDeviationDisplayRequestedHandler;
        _viewModel.Workbench.DatumPlaneDeviationDisplayCleared += _workbenchDatumPlaneDeviationDisplayClearedHandler;
        _viewModel.Workbench.LineFitDisplayRequested += _workbenchLineFitDisplayRequestedHandler;
        _viewModel.Workbench.LineFitDisplayCleared += _workbenchLineFitDisplayClearedHandler;
        _viewModel.Workbench.LineIntersectionDisplayRequested += _workbenchLineIntersectionDisplayRequestedHandler;
        _viewModel.Workbench.LineIntersectionDisplayCleared += _workbenchLineIntersectionDisplayClearedHandler;
        _viewModel.Workbench.LandmarkCorrespondenceDisplayRequested += _workbenchLandmarkCorrespondenceDisplayRequestedHandler;
        _viewModel.Workbench.LandmarkCorrespondenceDisplayCleared += _workbenchLandmarkCorrespondenceDisplayClearedHandler;
        _viewer.WorkbenchLineFitPointSelected += _viewerWorkbenchLineFitPointSelectedHandler;
        _viewer.TeachingCaptureStateChanged += _viewerTeachingCaptureStateChangedHandler;

        ConfigureCalibrationStudyFromCommandLine();
        ConfigureToolTeachingRecipeFromCommandLine();
        ConfigureOutputCompareFromCommandLine();
        ConfigureWorkbenchBottomPaneFromCommandLine();
        ConfigureValidationSetFromCommandLine();
        ConfigureC3DSourceLoadProgressFromCommandLine();
        SyncAppliedTeachingSelections();
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
        _viewModel.Workbench.BeginTeachingSelectionCaptureRequested -= _workbenchBeginTeachingCaptureRequestedHandler;
        _viewModel.Workbench.UndoTeachingSelectionCaptureRequested -= _workbenchUndoTeachingCaptureRequestedHandler;
        _viewModel.Workbench.CancelTeachingSelectionCaptureRequested -= _workbenchCancelTeachingCaptureRequestedHandler;
        _viewModel.Workbench.ApplyTeachingSelectionCaptureRequested -= _workbenchApplyTeachingCaptureRequestedHandler;
        _viewModel.Workbench.AppliedTeachingSelectionsChanged -= _workbenchAppliedTeachingSelectionsChangedHandler;
        _viewModel.Workbench.ToolLabRequested -= _workbenchToolLabRequestedHandler;
        _viewModel.Workbench.FilterDisplayRequested -= _workbenchFilterDisplayRequestedHandler;
        _viewModel.Workbench.ViewerArtifactDisplayRequested -= _workbenchArtifactDisplayRequestedHandler;
        _viewModel.Workbench.HeightDifferenceEdgeDisplayRequested -= _workbenchHeightDifferenceEdgeDisplayRequestedHandler;
        _viewModel.Workbench.TwoPointLineDisplayRequested -= _workbenchTwoPointLineDisplayRequestedHandler;
        _viewModel.Workbench.TwoPointLineDisplayCleared -= _workbenchTwoPointLineDisplayClearedHandler;
        _viewModel.Workbench.ThreePointPlaneDisplayRequested -= _workbenchThreePointPlaneDisplayRequestedHandler;
        _viewModel.Workbench.ThreePointPlaneDisplayCleared -= _workbenchThreePointPlaneDisplayClearedHandler;
        _viewModel.Workbench.DatumPlaneDeviationDisplayRequested -= _workbenchDatumPlaneDeviationDisplayRequestedHandler;
        _viewModel.Workbench.DatumPlaneDeviationDisplayCleared -= _workbenchDatumPlaneDeviationDisplayClearedHandler;
        _viewModel.Workbench.LineFitDisplayRequested -= _workbenchLineFitDisplayRequestedHandler;
        _viewModel.Workbench.LineFitDisplayCleared -= _workbenchLineFitDisplayClearedHandler;
        _viewModel.Workbench.LineIntersectionDisplayRequested -= _workbenchLineIntersectionDisplayRequestedHandler;
        _viewModel.Workbench.LineIntersectionDisplayCleared -= _workbenchLineIntersectionDisplayClearedHandler;
        _viewModel.Workbench.LandmarkCorrespondenceDisplayRequested -= _workbenchLandmarkCorrespondenceDisplayRequestedHandler;
        _viewModel.Workbench.LandmarkCorrespondenceDisplayCleared -= _workbenchLandmarkCorrespondenceDisplayClearedHandler;
        _viewer.WorkbenchLineFitPointSelected -= _viewerWorkbenchLineFitPointSelectedHandler;
        _viewer.TeachingCaptureStateChanged -= _viewerTeachingCaptureStateChangedHandler;
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
        var shellScreenshotPath = GetCommandLineValue("--shell-smoke-screenshot");
        var screenshotQualityReportPath = GetCommandLineValue("--shell-screenshot-quality-report");
        var recipeManagerScreenshotPath = GetCommandLineValue("--recipe-manager-screenshot");
        var recipeManagerScreenshotQualityReportPath = GetCommandLineValue("--recipe-manager-screenshot-quality-report");
        var messageDialogScreenshotPath = GetCommandLineValue("--message-dialog-screenshot");
        var messageDialogScreenshotQualityReportPath = GetCommandLineValue("--message-dialog-screenshot-quality-report");
        WpfMessageDialogWindow? messageDialogSmokeWindow = null;
        var filterToolLabScreenshotPath = GetCommandLineValue("--filter-tool-lab-screenshot");
        var filterToolLabScreenshotQualityReportPath = GetCommandLineValue("--filter-tool-lab-screenshot-quality-report");
        var edgeToolLabScreenshotPath = GetCommandLineValue("--edge-tool-lab-screenshot");
        var edgeToolLabScreenshotQualityReportPath = GetCommandLineValue("--edge-tool-lab-screenshot-quality-report");
        var twoPointLineToolLabScreenshotPath = GetCommandLineValue("--two-point-line-tool-lab-screenshot");
        var twoPointLineToolLabScreenshotQualityReportPath = GetCommandLineValue("--two-point-line-tool-lab-screenshot-quality-report");
        var threePointPlaneToolLabScreenshotPath = GetCommandLineValue("--three-point-plane-tool-lab-screenshot");
        var threePointPlaneToolLabScreenshotQualityReportPath = GetCommandLineValue("--three-point-plane-tool-lab-screenshot-quality-report");
        var datumPlaneDeviationToolLabScreenshotPath = GetCommandLineValue("--datum-plane-deviation-tool-lab-screenshot");
        var datumPlaneDeviationToolLabScreenshotQualityReportPath = GetCommandLineValue("--datum-plane-deviation-tool-lab-screenshot-quality-report");
        var lineIntersectionToolLabScreenshotPath = GetCommandLineValue("--line-intersection-tool-lab-screenshot");
        var lineIntersectionToolLabScreenshotQualityReportPath = GetCommandLineValue("--line-intersection-tool-lab-screenshot-quality-report");
        var landmarkCorrespondenceToolLabScreenshotPath = GetCommandLineValue("--landmark-correspondence-tool-lab-screenshot");
        var landmarkCorrespondenceToolLabScreenshotQualityReportPath = GetCommandLineValue("--landmark-correspondence-tool-lab-screenshot-quality-report");
        var xyzAffineSolveToolLabScreenshotPath = GetCommandLineValue("--xyz-affine-solve-tool-lab-screenshot");
        var xyzAffineSolveToolLabScreenshotQualityReportPath = GetCommandLineValue("--xyz-affine-solve-tool-lab-screenshot-quality-report");
        var xyzAffineApplyToolLabScreenshotPath = GetCommandLineValue("--xyz-affine-apply-tool-lab-screenshot");
        var xyzAffineApplyToolLabScreenshotQualityReportPath = GetCommandLineValue("--xyz-affine-apply-tool-lab-screenshot-quality-report");
        var regridHeightMapToolLabScreenshotPath = GetCommandLineValue("--regrid-height-map-tool-lab-screenshot");
        var regridHeightMapToolLabScreenshotQualityReportPath = GetCommandLineValue("--regrid-height-map-tool-lab-screenshot-quality-report");
        var smokeSaveRecipePath = GetCommandLineValue("--smoke-save-recipe");
        var teachingSelectionSmokeMode = GetCommandLineValue("--smoke-tool-teaching-selection");
        var teachingSelectionSmokeReportPath = GetCommandLineValue("--smoke-tool-teaching-selection-report");
        var teachingRecipeSmokeSavePath = GetCommandLineValue("--smoke-save-tool-teaching-recipe");
        var newRecipeLifecycleSmokePath = GetCommandLineValue("--smoke-new-recipe-lifecycle");
        var newRecipeLifecycleSmokeReportPath = GetCommandLineValue("--smoke-new-recipe-lifecycle-report");
        var openRecipeLifecycleSmokePath = GetCommandLineValue("--smoke-open-recipe-lifecycle");
        var openRecipeLifecycleSmokeReportPath = GetCommandLineValue("--smoke-open-recipe-lifecycle-report");
        var asyncC3DLoadSmokePath = GetCommandLineValue("--smoke-async-c3d-load");
        var asyncC3DLoadSmokeReportPath = GetCommandLineValue("--smoke-async-c3d-load-report");
        var asyncC3DLoadCancelAt = double.TryParse(
            GetCommandLineValue("--smoke-async-c3d-load-cancel-at"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsedCancelAt)
            ? parsedCancelAt
            : (double?)null;
        var asyncC3DLoadExpectFailure = Environment.GetCommandLineArgs()
            .Contains("--smoke-async-c3d-load-expect-failure", StringComparer.OrdinalIgnoreCase);
        var planeFlatnessLiveA3PointerSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-plane-flatness-live-a3-pointer", StringComparer.OrdinalIgnoreCase);
        var planeFlatnessLiveA3PointerReportPath = GetCommandLineValue("--smoke-plane-flatness-live-a3-pointer-report");
        var planeFlatnessLiveA3PointerSavePath = GetCommandLineValue("--smoke-plane-flatness-live-a3-pointer-save");
        var profilePointerSmokeReportPath = GetCommandLineValue("--smoke-profile-pointer-report");
        var smokeSelectToolId = GetCommandLineValue("--smoke-select-tool");
        var filterPublishSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-filter-publish", StringComparer.OrdinalIgnoreCase);
        var twoPointLinePublishSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-two-point-line-publish", StringComparer.OrdinalIgnoreCase);
        var twoPointLinePreviewSmoke = twoPointLinePublishSmoke || Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-two-point-line-preview", StringComparer.OrdinalIgnoreCase);
        var threePointPlanePublishSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-three-point-plane-publish", StringComparer.OrdinalIgnoreCase);
        var threePointPlanePreviewSmoke = threePointPlanePublishSmoke || Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-three-point-plane-preview", StringComparer.OrdinalIgnoreCase);
        var datumPlaneDeviationPublishSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-datum-plane-deviation-publish", StringComparer.OrdinalIgnoreCase);
        var datumPlaneDeviationPreviewSmoke = datumPlaneDeviationPublishSmoke || Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-datum-plane-deviation-preview", StringComparer.OrdinalIgnoreCase);
        var filterPreviewSmoke = filterPublishSmoke || Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-filter-preview", StringComparer.OrdinalIgnoreCase);
        var edgePublishSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-edge-publish", StringComparer.OrdinalIgnoreCase);
        var lineFitPreviewSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-line-fit-preview", StringComparer.OrdinalIgnoreCase);
        var edgePreviewSmoke = edgePublishSmoke || lineFitPreviewSmoke || Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-edge-preview", StringComparer.OrdinalIgnoreCase);
        var invalidEdgeDraftSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-wpg-invalid-edge", StringComparer.OrdinalIgnoreCase);
        var edgeStepId = GetCommandLineValue("--tool-teaching-step");
        var edgeSmokeReportPath = GetCommandLineValue("--smoke-tool-edge-report");
        var lineFitSmokeReportPath = GetCommandLineValue("--smoke-tool-line-fit-report");
        if (teachingSelectionSmokeMode is not null || planeFlatnessLiveA3PointerSmoke || profilePointerSmokeReportPath is not null || edgePreviewSmoke || lineFitPreviewSmoke || twoPointLinePreviewSmoke || threePointPlanePreviewSmoke || datumPlaneDeviationPreviewSmoke)
        {
            Width = 1280;
            Height = 760;
        }

        if (int.TryParse(GetCommandLineValue("--shell-smoke-width"), out var smokeWidth)
            && int.TryParse(GetCommandLineValue("--shell-smoke-height"), out var smokeHeight)
            && smokeWidth >= MinWidth
            && smokeHeight >= MinHeight)
        {
            WindowState = WindowState.Normal;
            Width = smokeWidth;
            Height = smokeHeight;
        }

        var smokePublishResult = Environment.GetCommandLineArgs()
            .Contains("--smoke-publish-result", StringComparer.OrdinalIgnoreCase);
        var waitForNominalActualPreview = Environment.GetCommandLineArgs()
            .Contains("--smoke-nominal-actual", StringComparer.OrdinalIgnoreCase)
            || _viewer.ViewModel.NominalActualInput is not null;
        if (shellScreenshotPath is not null
            || recipeManagerScreenshotPath is not null
            || messageDialogScreenshotPath is not null
            || filterToolLabScreenshotPath is not null
            || edgeToolLabScreenshotPath is not null
            || twoPointLineToolLabScreenshotPath is not null
            || threePointPlaneToolLabScreenshotPath is not null
            || datumPlaneDeviationToolLabScreenshotPath is not null
            || lineIntersectionToolLabScreenshotPath is not null
            || landmarkCorrespondenceToolLabScreenshotPath is not null
            || xyzAffineSolveToolLabScreenshotPath is not null
            || xyzAffineApplyToolLabScreenshotPath is not null
            || regridHeightMapToolLabScreenshotPath is not null
            || _viewer.HasConfiguredSmokeScreenshot
            || teachingSelectionSmokeMode is not null
            || planeFlatnessLiveA3PointerSmoke
            || profilePointerSmokeReportPath is not null
            || filterPreviewSmoke
            || edgePreviewSmoke
            || lineFitPreviewSmoke
            || twoPointLinePreviewSmoke
            || threePointPlanePreviewSmoke
            || datumPlaneDeviationPreviewSmoke
            || newRecipeLifecycleSmokePath is not null
            || openRecipeLifecycleSmokePath is not null
            || asyncC3DLoadSmokePath is not null)
        {
            _shellSmokeLoadedHandler = async (_, _) =>
            {
                await Dispatcher.InvokeAsync(() => { });
                if (asyncC3DLoadSmokePath is not null)
                {
                    var dispatcherTicks = 0;
                    var cancelIssued = false;
                    var previousPath = _viewer.CurrentC3DSourcePath;
                    var timer = new DispatcherTimer(
                        TimeSpan.FromMilliseconds(1),
                        DispatcherPriority.Input,
                        (_, _) =>
                        {
                            dispatcherTicks++;
                            if (!cancelIssued
                                && asyncC3DLoadCancelAt is { } cancelAt
                                && _viewModel.Workbench.IsC3DSourceLoading
                                && _viewModel.Workbench.C3DSourceLoadProgressPercent >= cancelAt)
                            {
                                cancelIssued = true;
                                _viewModel.Workbench.CancelC3DSourceLoadCommand.Execute(null);
                            }
                        },
                        Dispatcher);
                    var stopwatch = Stopwatch.StartNew();
                    timer.Start();
                    await LoadWorkbenchC3DSourceAsync(asyncC3DLoadSmokePath, showFailureDialog: false);
                    timer.Stop();
                    stopwatch.Stop();
                    var expectedPath = Path.GetFullPath(asyncC3DLoadSmokePath);
                    var loadPerformance = _viewer.LastC3DSourceLoadPerformance;
                    var loadStateCleared = !_viewModel.Workbench.IsC3DSourceLoading;
                    var passed = asyncC3DLoadExpectFailure
                        ? !cancelIssued
                          && previousPath is not null
                          && IsViewerSourceAlreadyLoaded(previousPath)
                          && loadStateCleared
                        : asyncC3DLoadCancelAt is null
                        ? IsViewerSourceAlreadyLoaded(expectedPath)
                          && loadStateCleared
                          && dispatcherTicks > 0
                        : cancelIssued
                          && previousPath is not null
                          && IsViewerSourceAlreadyLoaded(previousPath)
                          && loadStateCleared;
                    if (!string.IsNullOrWhiteSpace(asyncC3DLoadSmokeReportPath))
                    {
                        var reportPath = Path.GetFullPath(asyncC3DLoadSmokeReportPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                        File.WriteAllLines(reportPath,
                        [
                            "OpenVisionLab 3D actual EXE asynchronous C3D load smoke",
                            $"Result: {(passed ? "Pass" : "Fail")}",
                            $"Mode: {(asyncC3DLoadExpectFailure ? "Failure" : asyncC3DLoadCancelAt is null ? "Complete" : "Cancel")}",
                            $"PreviousPath: {previousPath}",
                            $"TargetPath: {expectedPath}",
                            $"CurrentPath: {_viewer.CurrentC3DSourcePath}",
                            $"ElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}",
                            $"GridReadAndStatisticsMilliseconds: {loadPerformance?.Grid.ReadAndStatisticsMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"GridDistributionMilliseconds: {loadPerformance?.Grid.DistributionMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"GridRenderPointsMilliseconds: {loadPerformance?.Grid.RenderPointsMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"GridHashMilliseconds: {loadPerformance?.Grid.HashMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"GridTotalMilliseconds: {loadPerformance?.Grid.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"RenderTopologyMilliseconds: {loadPerformance?.TopologyMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"RenderPositionsMilliseconds: {loadPerformance?.PositionsMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"WorkerTotalMilliseconds: {loadPerformance?.WorkerMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"UiApplyAndFirstRenderMilliseconds: {loadPerformance?.ApplyMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"DispatcherTicksDuringLoad: {dispatcherTicks}",
                            $"CancelAtPercent: {asyncC3DLoadCancelAt?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}",
                            $"CancelIssued: {cancelIssued}",
                            $"LoadStateCleared: {loadStateCleared}",
                            $"FinalProgressPercent: {_viewModel.Workbench.C3DSourceLoadProgressPercent:F1}"
                        ]);
                    }

                    if (!passed)
                    {
                        _viewModel.SetViewerSmokeFailed("Asynchronous C3D load smoke did not keep the Dispatcher responsive or activate the target source.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (newRecipeLifecycleSmokePath is not null)
                {
                    ShowRecipeManagerWindow();
                    _viewModel.Workbench.RecipeName = "Discard this current draft";
                    var doNotSaveClick = ClickUnsavedRecipeDoNotSaveForSmokeAsync();
                    _viewModel.Workbench.NewTeachingRecipeCommand.Execute(null);
                    var clickedDoNotSave = await doNotSaveClick;
                    var createdPath = Path.GetFullPath(newRecipeLifecycleSmokePath);
                    ToolRecipeDocument? createdDocument = null;
                    if (File.Exists(createdPath))
                    {
                        createdDocument = ToolRecipeDocumentStore.Load(createdPath);
                    }

                    var passed = clickedDoNotSave
                        && createdDocument is not null
                        && createdDocument.Steps.Count == 0
                        && string.IsNullOrWhiteSpace(createdDocument.Source.Path)
                        && string.Equals(_viewModel.Workbench.RecipePath, createdPath, StringComparison.OrdinalIgnoreCase)
                        && !_viewModel.Workbench.IsSourceReadyForRecipe
                        && _viewer.CurrentC3DSourcePath is null
                        && !_viewModel.Workbench.IsDirty;
                    if (!string.IsNullOrWhiteSpace(newRecipeLifecycleSmokeReportPath))
                    {
                        var reportPath = Path.GetFullPath(newRecipeLifecycleSmokeReportPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                        File.WriteAllLines(reportPath,
                        [
                            "OpenVisionLab 3D actual EXE new-recipe lifecycle smoke",
                            $"Result: {(passed ? "Pass" : "Fail")}",
                            $"DoNotSaveButtonClicked: {clickedDoNotSave}",
                            $"RecipePath: {_viewModel.Workbench.RecipePath}",
                            $"RecipeExists: {File.Exists(createdPath)}",
                            $"StepCount: {createdDocument?.Steps.Count}",
                            $"SourcePath: {createdDocument?.Source.Path}",
                            $"SourceReady: {_viewModel.Workbench.IsSourceReadyForRecipe}",
                            $"ViewerSourcePath: {_viewer.CurrentC3DSourcePath}",
                            $"IsDirty: {_viewModel.Workbench.IsDirty}"
                        ]);
                    }

                    if (!passed)
                    {
                        _viewModel.SetViewerSmokeFailed("New recipe lifecycle smoke did not create and open a clean zero-step recipe.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (openRecipeLifecycleSmokePath is not null)
                {
                    ShowRecipeManagerWindow();
                    var stopwatch = Stopwatch.StartNew();
                    OpenWorkbenchRecipe(openRecipeLifecycleSmokePath);
                    stopwatch.Stop();
                    var expectedPath = Path.GetFullPath(openRecipeLifecycleSmokePath);
                    var passed = string.Equals(_viewModel.Workbench.RecipePath, expectedPath, StringComparison.OrdinalIgnoreCase)
                        && !_viewModel.Workbench.IsDirty
                        && recipeManagerWindow?.IsVisible != true;
                    if (!string.IsNullOrWhiteSpace(openRecipeLifecycleSmokeReportPath))
                    {
                        var reportPath = Path.GetFullPath(openRecipeLifecycleSmokeReportPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                        File.WriteAllLines(reportPath,
                        [
                            "OpenVisionLab 3D actual EXE open-recipe lifecycle smoke",
                            $"Result: {(passed ? "Pass" : "Fail")}",
                            $"OpenWorkbenchRecipeMilliseconds: {stopwatch.ElapsedMilliseconds}",
                            $"RecipePath: {_viewModel.Workbench.RecipePath}",
                            $"IsDirty: {_viewModel.Workbench.IsDirty}",
                            $"RecipeManagerVisible: {recipeManagerWindow?.IsVisible == true}",
                            $"ViewerSourceReused: {IsViewerSourceAlreadyLoaded(_viewModel.Workbench.Source.Path)}"
                        ]);
                    }
                    if (!passed)
                    {
                        _viewModel.SetViewerSmokeFailed("Open recipe lifecycle smoke did not activate the saved recipe in Workbench.");
                        Application.Current.Shutdown(1);
                        return;
                    }
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

                if (filterToolLabScreenshotPath is not null
                    && !ShowFilterToolLabWindow(showMissingFilterMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Filter Tool Lab smoke requires a Filter recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstFilterToolLabWindow = filterToolLabWindow;
                if (filterToolLabScreenshotPath is not null
                    && (firstFilterToolLabWindow is null
                        || !ShowFilterToolLabWindow(showMissingFilterMessage: false)
                        || !ReferenceEquals(firstFilterToolLabWindow, filterToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("Filter Tool Lab smoke could not reuse its single window instance.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (edgeToolLabScreenshotPath is not null
                    && !ShowHeightDifferenceEdgeToolLabWindow(showMissingEdgeMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Edge Tool Lab smoke requires a Height Difference Edge recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstEdgeToolLabWindow = heightDifferenceEdgeToolLabWindow;
                if (edgeToolLabScreenshotPath is not null
                    && (firstEdgeToolLabWindow is null
                        || !ShowHeightDifferenceEdgeToolLabWindow(showMissingEdgeMessage: false)
                        || !ReferenceEquals(firstEdgeToolLabWindow, heightDifferenceEdgeToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("Edge Tool Lab smoke could not reuse its single window instance.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (twoPointLineToolLabScreenshotPath is not null
                    && !ShowTwoPointLineToolLabWindow(showMissingTwoPointLineMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("2-Point Line Tool Lab smoke requires a 2-Point Line recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (threePointPlaneToolLabScreenshotPath is not null
                    && !ShowThreePointPlaneToolLabWindow(showMissingThreePointPlaneMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("3-Point Plane Tool Lab smoke requires a 3-Point Plane recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstThreePointPlaneToolLabWindow = threePointPlaneToolLabWindow;
                if (threePointPlaneToolLabScreenshotPath is not null
                    && (firstThreePointPlaneToolLabWindow is null
                        || !ShowThreePointPlaneToolLabWindow(showMissingThreePointPlaneMessage: false)
                        || !ReferenceEquals(firstThreePointPlaneToolLabWindow, threePointPlaneToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("3-Point Plane Tool Lab smoke could not reuse its single window instance.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (datumPlaneDeviationToolLabScreenshotPath is not null
                    && !ShowDatumPlaneDeviationToolLabWindow(showMissingDatumDeviationMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Datum Plane Deviation Tool Lab smoke requires a Datum Plane Raw-Height Deviation recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstDatumPlaneDeviationToolLabWindow = datumPlaneDeviationToolLabWindow;
                if (datumPlaneDeviationToolLabScreenshotPath is not null
                    && (firstDatumPlaneDeviationToolLabWindow is null
                        || !ShowDatumPlaneDeviationToolLabWindow(showMissingDatumDeviationMessage: false)
                        || !ReferenceEquals(firstDatumPlaneDeviationToolLabWindow, datumPlaneDeviationToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("Datum Plane Deviation Tool Lab smoke could not reuse its single window instance.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstTwoPointLineToolLabWindow = twoPointLineToolLabWindow;
                if (twoPointLineToolLabScreenshotPath is not null
                    && (firstTwoPointLineToolLabWindow is null
                        || !ShowTwoPointLineToolLabWindow(showMissingTwoPointLineMessage: false)
                        || !ReferenceEquals(firstTwoPointLineToolLabWindow, twoPointLineToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("2-Point Line Tool Lab smoke could not reuse its single window instance.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (lineIntersectionToolLabScreenshotPath is not null
                    && !ShowLineIntersectionToolLabWindow(showMissingLineIntersectionMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Line Intersection Tool Lab smoke requires a Line Intersection recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstLineIntersectionToolLabWindow = lineIntersectionToolLabWindow;
                if (lineIntersectionToolLabScreenshotPath is not null
                    && (firstLineIntersectionToolLabWindow is null
                        || !ShowLineIntersectionToolLabWindow(showMissingLineIntersectionMessage: false)
                        || !ReferenceEquals(firstLineIntersectionToolLabWindow, lineIntersectionToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("Line Intersection Tool Lab smoke could not reuse its single window instance.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (landmarkCorrespondenceToolLabScreenshotPath is not null
                    && !ShowLandmarkCorrespondenceToolLabWindow(showMissingCorrespondenceMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Landmark Correspondence Tool Lab smoke requires a Landmark Correspondence recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstLandmarkCorrespondenceToolLabWindow = landmarkCorrespondenceToolLabWindow;
                if (landmarkCorrespondenceToolLabScreenshotPath is not null
                    && (firstLandmarkCorrespondenceToolLabWindow is null
                        || !ShowLandmarkCorrespondenceToolLabWindow(showMissingCorrespondenceMessage: false)
                        || !ReferenceEquals(firstLandmarkCorrespondenceToolLabWindow, landmarkCorrespondenceToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("Landmark Correspondence Tool Lab smoke could not reuse its single window instance.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (xyzAffineSolveToolLabScreenshotPath is not null
                    && !EnsureToolLabStepSelected("xyz-affine-solve", preserveSelectedStep: false))
                {
                    _viewModel.Workbench.SelectedTool = _viewModel.Workbench.Tools.Single(tool => tool.Id == "xyz-affine-solve");
                    _viewModel.Workbench.AddSelectedToolCommand.Execute(null);
                    if (_viewModel.Workbench.SelectedPipelineStep is not { } affineStep)
                    {
                        _viewModel.SetViewerSmokeFailed("XYZ Affine Solve smoke could not author its isolated waiting step.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    affineStep.InputEntityIdsText = "derived.correspondences.01";
                }

                if (xyzAffineSolveToolLabScreenshotPath is not null
                    && !ShowXYZAffineSolveToolLabWindow(showMissingAffineSolveMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("XYZ Affine Solve Tool Lab smoke requires an XYZ Affine Solve recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstXYZAffineSolveToolLabWindow = xyzAffineSolveToolLabWindow;
                if (xyzAffineSolveToolLabScreenshotPath is not null
                    && (firstXYZAffineSolveToolLabWindow is null
                        || !ShowXYZAffineSolveToolLabWindow(showMissingAffineSolveMessage: false)
                        || !ReferenceEquals(firstXYZAffineSolveToolLabWindow, xyzAffineSolveToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("XYZ Affine Solve Tool Lab smoke could not reuse its single window instance.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (xyzAffineApplyToolLabScreenshotPath is not null
                    && !EnsureToolLabStepSelected("xyz-affine-apply", preserveSelectedStep: false))
                {
                    _viewModel.Workbench.SelectedTool = _viewModel.Workbench.Tools.Single(tool => tool.Id == "xyz-affine-apply");
                    _viewModel.Workbench.AddSelectedToolCommand.Execute(null);
                    if (_viewModel.Workbench.SelectedPipelineStep is not { } affineApplyStep)
                    {
                        _viewModel.SetViewerSmokeFailed("XYZ Affine Apply smoke could not author its isolated waiting step.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    affineApplyStep.InputEntityIdsText = "source.c3d.height-map;derived.affine-transform.01";
                }

                if (xyzAffineApplyToolLabScreenshotPath is not null
                    && !ShowXYZAffineApplyToolLabWindow(showMissingAffineApplyMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("XYZ Affine Apply Tool Lab smoke requires an Apply XYZ Affine recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstXYZAffineApplyToolLabWindow = xyzAffineApplyToolLabWindow;
                if (xyzAffineApplyToolLabScreenshotPath is not null
                    && (firstXYZAffineApplyToolLabWindow is null
                        || !ShowXYZAffineApplyToolLabWindow(showMissingAffineApplyMessage: false)
                        || !ReferenceEquals(firstXYZAffineApplyToolLabWindow, xyzAffineApplyToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("XYZ Affine Apply Tool Lab smoke could not reuse its single window instance.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (regridHeightMapToolLabScreenshotPath is not null
                    && !EnsureToolLabStepSelected("re-grid-height-map", preserveSelectedStep: false))
                {
                    _viewModel.Workbench.SelectedTool = _viewModel.Workbench.Tools.Single(tool => tool.Id == "re-grid-height-map");
                    _viewModel.Workbench.AddSelectedToolCommand.Execute(null);
                    if (_viewModel.Workbench.SelectedPipelineStep is not { } regridStep)
                    {
                        _viewModel.SetViewerSmokeFailed("Re-grid Height Map smoke could not author its isolated waiting step.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    regridStep.InputEntityIdsText = "derived.affine-point-cloud.01";
                }

                if (regridHeightMapToolLabScreenshotPath is not null
                    && !ShowRegridHeightMapToolLabWindow(showMissingRegridMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Re-grid Height Map Tool Lab smoke requires a Re-grid Height Map recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                var firstRegridHeightMapToolLabWindow = regridHeightMapToolLabWindow;
                if (regridHeightMapToolLabScreenshotPath is not null
                    && (firstRegridHeightMapToolLabWindow is null
                        || !ShowRegridHeightMapToolLabWindow(showMissingRegridMessage: false)
                        || !ReferenceEquals(firstRegridHeightMapToolLabWindow, regridHeightMapToolLabWindow)))
                {
                    _viewModel.SetViewerSmokeFailed("Re-grid Height Map Tool Lab smoke could not reuse its single window instance.");
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
                    && !await RunToolTeachingSelectionSmokeAsync(
                        teachingSelectionSmokeMode,
                        teachingSelectionSmokeReportPath))
                {
                    Application.Current.Shutdown(1);
                    return;
                }

                if (planeFlatnessLiveA3PointerSmoke
                    && !await RunPlaneFlatnessLiveA3PointerSmokeAsync(
                        planeFlatnessLiveA3PointerReportPath,
                        planeFlatnessLiveA3PointerSavePath))
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

                UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                await Task.Delay(100);
                if (shellScreenshotPath is not null
                    && !await CaptureWindowWithRetryAsync(this, shellScreenshotPath, screenshotQualityReportPath, "Shell"))
                {
                    _viewModel.SetViewerSmokeFailed("Shell screenshot remained blank or invalid after 3 attempts.");
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

                if (filterToolLabScreenshotPath is not null
                    && (filterToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(filterToolLabWindow),
                            filterToolLabScreenshotPath,
                            filterToolLabScreenshotQualityReportPath,
                            "FilterToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Filter Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (edgeToolLabScreenshotPath is not null
                    && (heightDifferenceEdgeToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(heightDifferenceEdgeToolLabWindow),
                            edgeToolLabScreenshotPath,
                            edgeToolLabScreenshotQualityReportPath,
                            "EdgeToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Edge Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (twoPointLineToolLabScreenshotPath is not null
                    && (twoPointLineToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(twoPointLineToolLabWindow),
                            twoPointLineToolLabScreenshotPath,
                            twoPointLineToolLabScreenshotQualityReportPath,
                            "TwoPointLineToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("2-Point Line Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (threePointPlaneToolLabScreenshotPath is not null
                    && (threePointPlaneToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(threePointPlaneToolLabWindow),
                            threePointPlaneToolLabScreenshotPath,
                            threePointPlaneToolLabScreenshotQualityReportPath,
                            "ThreePointPlaneToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("3-Point Plane Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (datumPlaneDeviationToolLabScreenshotPath is not null
                    && (datumPlaneDeviationToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(datumPlaneDeviationToolLabWindow),
                            datumPlaneDeviationToolLabScreenshotPath,
                            datumPlaneDeviationToolLabScreenshotQualityReportPath,
                            "DatumPlaneDeviationToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Datum Plane Deviation Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (lineIntersectionToolLabScreenshotPath is not null
                    && (lineIntersectionToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(lineIntersectionToolLabWindow),
                            lineIntersectionToolLabScreenshotPath,
                            lineIntersectionToolLabScreenshotQualityReportPath,
                            "LineIntersectionToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Line Intersection Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (landmarkCorrespondenceToolLabScreenshotPath is not null
                    && (landmarkCorrespondenceToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(landmarkCorrespondenceToolLabWindow),
                            landmarkCorrespondenceToolLabScreenshotPath,
                            landmarkCorrespondenceToolLabScreenshotQualityReportPath,
                            "LandmarkCorrespondenceToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Landmark Correspondence Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (xyzAffineSolveToolLabScreenshotPath is not null
                    && (xyzAffineSolveToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(xyzAffineSolveToolLabWindow),
                            xyzAffineSolveToolLabScreenshotPath,
                            xyzAffineSolveToolLabScreenshotQualityReportPath,
                            "XYZAffineSolveToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("XYZ Affine Solve Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (xyzAffineApplyToolLabScreenshotPath is not null
                    && (xyzAffineApplyToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(xyzAffineApplyToolLabWindow),
                            xyzAffineApplyToolLabScreenshotPath,
                            xyzAffineApplyToolLabScreenshotQualityReportPath,
                            "XYZAffineApplyToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("XYZ Affine Apply Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (regridHeightMapToolLabScreenshotPath is not null
                    && (regridHeightMapToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            RefreshToolLabForCapture(regridHeightMapToolLabWindow),
                            regridHeightMapToolLabScreenshotPath,
                            regridHeightMapToolLabScreenshotQualityReportPath,
                            "RegridHeightMapToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Re-grid Height Map Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                await Task.Delay(100);
                if (xyzAffineSolveToolLabScreenshotPath is not null && xyzAffineSolveToolLabWindow is { IsVisible: true })
                {
                    xyzAffineSolveToolLabWindow.Close();
                }
                if (xyzAffineApplyToolLabScreenshotPath is not null && xyzAffineApplyToolLabWindow is { IsVisible: true })
                {
                    xyzAffineApplyToolLabWindow.Close();
                }
                if (regridHeightMapToolLabScreenshotPath is not null && regridHeightMapToolLabWindow is { IsVisible: true })
                {
                    regridHeightMapToolLabWindow.Close();
                }
                if (datumPlaneDeviationToolLabScreenshotPath is not null && datumPlaneDeviationToolLabWindow is { IsVisible: true })
                {
                    datumPlaneDeviationToolLabWindow.Close();
                }
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

    private static Window RefreshToolLabForCapture(Window window)
    {
        switch (window)
        {
            case FilterToolLabWindow filter:
                filter.RefreshViews();
                break;
            case HeightDifferenceEdgeToolLabWindow edge:
                edge.RefreshViews();
                break;
            case TwoPointLineToolLabWindow twoPointLine:
                twoPointLine.RefreshViews();
                break;
            case ThreePointPlaneToolLabWindow threePointPlane:
                threePointPlane.RefreshViews();
                break;
            case DatumPlaneDeviationToolLabWindow datumPlaneDeviation:
                datumPlaneDeviation.RefreshViews();
                break;
            case LineIntersectionToolLabWindow intersection:
                intersection.RefreshViews();
                break;
            case LandmarkCorrespondenceToolLabWindow correspondence:
                correspondence.RefreshViews();
                break;
            case XYZAffineSolveToolLabWindow affine:
                affine.RefreshViews();
                break;
            case XYZAffineApplyToolLabWindow apply:
                apply.RefreshViews();
                break;
            case RegridHeightMapToolLabWindow regrid:
                regrid.RefreshViews();
                break;
        }

        return window;
    }

    private async Task<bool> RunPlaneFlatnessLiveA3PointerSmokeAsync(
        string? reportPath,
        string? savePath)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Plane Flatness live A3 pointer Shell smoke",
            "Boundary|Deterministic synthetic display-frame evidence only; this is not physical calibration, Gauge R&R, or metrology evidence."
        };
        var workbench = _viewModel.Workbench;
        var previewBefore = _viewer.ViewModel.PreviewToolResult;
        var resultsBefore = _viewer.ViewModel.ResultEntities;

        bool Complete(bool passed, string message)
        {
            lines.Add($"InspectionBoundary|previewReferenceUnchanged={ReferenceEquals(previewBefore, _viewer.ViewModel.PreviewToolResult)}|resultReferenceUnchanged={ReferenceEquals(resultsBefore, _viewer.ViewModel.ResultEntities)}");
            lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{message}");
            WriteTeachingSelectionSmokeReport(reportPath, lines);
            Console.WriteLine(lines[^1]);
            if (!passed)
            {
                _viewModel.SetViewerSmokeFailed(message);
            }
            return passed;
        }

        string? PointerReport(string role)
        {
            if (string.IsNullOrWhiteSpace(reportPath)) return null;
            var fullReportPath = Path.GetFullPath(reportPath);
            return Path.Combine(
                Path.GetDirectoryName(fullReportPath)!,
                $"{Path.GetFileNameWithoutExtension(fullReportPath)}.{role}-pointer.txt");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(reportPath) || string.IsNullOrWhiteSpace(savePath))
            {
                return Complete(false, "Live A3 pointer smoke requires explicit report and saved-recipe paths.");
            }
            if (string.IsNullOrWhiteSpace(workbench.RecipePath))
            {
                return Complete(false, "Live A3 pointer smoke requires the prepared fixture recipe to be opened by --tool-teaching-recipe.");
            }

            var regridStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.RegridStepId, StringComparison.OrdinalIgnoreCase));
            var planeStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PlaneFlatnessStepId, StringComparison.OrdinalIgnoreCase));
            var pointPairStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PointPairStepId, StringComparison.OrdinalIgnoreCase));
            var gapFlushStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.GapFlushStepId, StringComparison.OrdinalIgnoreCase));
            var volumeStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.VolumeStepId, StringComparison.OrdinalIgnoreCase));
            var crossSectionStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.CrossSectionStepId, StringComparison.OrdinalIgnoreCase));
            if (regridStep is null || planeStep is null || pointPairStep is null || gapFlushStep is null || volumeStep is null || crossSectionStep is null)
            {
                return Complete(false, "Prepared fixture recipe is missing its Re-grid, Plane Flatness, Point Pair, Gap / Flush, Volume, or Cross-section step.");
            }

            workbench.SelectedPipelineStep = regridStep;
            var publishedA2 = PlaneFlatnessLiveA3PointerSmokeFixture.CreatePublishedA2(workbench.RecipePath);
            if (!workbench.TryRegisterSyntheticPublishedAffineApplyOutputForSmoke(publishedA2, out var a2Message))
            {
                return Complete(false, a2Message);
            }
            lines.Add($"A2|entity={publishedA2.OutputEntityId}|sha256={publishedA2.ContentSha256}|finite={publishedA2.FinitePointCount}|registration={a2Message}");

            if (!await workbench.PreviewSelectedRegridHeightFieldAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Normal Re-grid Preview was not publishable: {workbench.RegridHeightFieldExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsRegridHeightFieldPreviewPublished
                || !workbench.TryGetPublishedRegridHeightFieldOutput(
                    PlaneFlatnessLiveA3PointerSmokeFixture.HeightFieldEntityId,
                    out var publishedA3)
                || publishedA3 is null)
            {
                return Complete(false, "Normal Re-grid Publish did not register the exact Preview A3 output.");
            }
            var expectedBinding = ToolRecipeSelectionSourceBindingVerifier.FromTransformedHeightField(publishedA3);
            lines.Add($"A3|entity={publishedA3.OutputEntityId}|sha256={publishedA3.ContentSha256}|populated={publishedA3.PopulatedCellCount}/{publishedA3.Cells.Count}|coverage={publishedA3.CoverageRatio:R}|published=True");

            workbench.SelectedPipelineStep = planeStep;
            if (!workbench.CapturePlaneFlatnessReferenceRoiCommand.CanExecute(null))
            {
                return Complete(false, "Plane Flatness reference ROI capture was not enabled after A3 Publish.");
            }
            workbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (!_viewer.TeachingCaptureSnapshot.IsActive
                || !await _viewer.RunTeachingCapturePointerSmokeAsync(
                    cancelWhenReady: false,
                    PointerReport("reference"),
                    exerciseNavigationGestures: false)
                || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
            {
                return Complete(false, "Two real Viewer pointer picks did not produce an applicable reference ROI candidate.");
            }
            _viewer.TryGetC3DTeachingCandidate(out var referenceCandidate, out var referenceCandidateMessage);
            lines.Add($"ReferenceCandidate|id={referenceCandidate?.Id}|rectangle={referenceCandidate?.GridRectangle}|owner={referenceCandidate?.SourceBinding.OwnerEntityId}|sha256={referenceCandidate?.SourceBinding.ContentSha256}|message={referenceCandidateMessage}");
            workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            lines.Add($"ReferenceApplyState|workbenchActive={workbench.IsTeachingSelectionCaptureActive}|viewerActive={_viewer.TeachingCaptureSnapshot.IsActive}|progress={workbench.TeachingSelectionCaptureProgress}");
            if (workbench.IsTeachingSelectionCaptureActive || _viewer.TeachingCaptureSnapshot.IsActive)
            {
                return Complete(false, "Reference ROI candidate was not accepted by the workbench.");
            }
            var referenceSelection = workbench.PlaneFlatnessReferenceSelection;
            if (referenceSelection?.GridRectangle is null)
            {
                return Complete(false, "Applied reference ROI was not routed into the Plane Flatness step.");
            }
            lines.Add($"ReferenceROI|id={referenceSelection.Id}|rectangle={referenceSelection.GridRectangle.Row},{referenceSelection.GridRectangle.Column},{referenceSelection.GridRectangle.RowCount},{referenceSelection.GridRectangle.ColumnCount}|owner={referenceSelection.SourceBinding.OwnerEntityId}|sha256={referenceSelection.SourceBinding.ContentSha256}");

            if (!workbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null))
            {
                return Complete(false, "Plane Flatness measurement ROI capture was not enabled after the reference ROI was applied.");
            }
            workbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (!_viewer.TeachingCaptureSnapshot.IsActive
                || !await _viewer.RunTeachingCapturePointerSmokeAsync(
                    cancelWhenReady: false,
                    PointerReport("measurement"),
                    exerciseNavigationGestures: false)
                || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
            {
                return Complete(false, "Two real Viewer pointer picks did not produce an applicable measurement ROI candidate.");
            }
            _viewer.TryGetC3DTeachingCandidate(out var measurementCandidate, out var measurementCandidateMessage);
            lines.Add($"MeasurementCandidate|id={measurementCandidate?.Id}|rectangle={measurementCandidate?.GridRectangle}|owner={measurementCandidate?.SourceBinding.OwnerEntityId}|sha256={measurementCandidate?.SourceBinding.ContentSha256}|message={measurementCandidateMessage}");
            workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            lines.Add($"MeasurementApplyState|workbenchActive={workbench.IsTeachingSelectionCaptureActive}|viewerActive={_viewer.TeachingCaptureSnapshot.IsActive}|progress={workbench.TeachingSelectionCaptureProgress}");
            if (workbench.IsTeachingSelectionCaptureActive || _viewer.TeachingCaptureSnapshot.IsActive)
            {
                return Complete(false, "Measurement ROI candidate was not accepted by the workbench.");
            }
            var measurementSelection = workbench.PlaneFlatnessMeasurementSelection;
            if (measurementSelection?.GridRectangle is null
                || string.Equals(referenceSelection.Id, measurementSelection.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Complete(false, "Applied measurement ROI was not routed as a distinct Plane Flatness role.");
            }
            lines.Add($"MeasurementROI|id={measurementSelection.Id}|rectangle={measurementSelection.GridRectangle.Row},{measurementSelection.GridRectangle.Column},{measurementSelection.GridRectangle.RowCount},{measurementSelection.GridRectangle.ColumnCount}|owner={measurementSelection.SourceBinding.OwnerEntityId}|sha256={measurementSelection.SourceBinding.ContentSha256}");

            var executionUnchanged = ReferenceEquals(previewBefore, _viewer.ViewModel.PreviewToolResult)
                && ReferenceEquals(resultsBefore, _viewer.ViewModel.ResultEntities);
            if (!executionUnchanged)
            {
                return Complete(false, "ROI teaching changed inspection Preview/Run evidence before an explicit measurement Preview.");
            }

            workbench.SelectedPipelineStep = pointPairStep;
            if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
            {
                return Complete(false, "Point Pair capture was not enabled against the Published A3 output.");
            }
            workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (!_viewer.TeachingCaptureSnapshot.IsActive
                || !await _viewer.RunTeachingCapturePointerSmokeAsync(
                    cancelWhenReady: false,
                    PointerReport("point-pair"),
                    exerciseNavigationGestures: false)
                || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
            {
                return Complete(false, "Two real Viewer pointer picks did not produce an applicable PointSet(2) candidate.");
            }
            _viewer.TryGetC3DTeachingCandidate(out var pointPairCandidate, out var pointPairCandidateMessage);
            lines.Add($"PointPairCandidate|id={pointPairCandidate?.Id}|points={pointPairCandidate?.Points?.Count}|owner={pointPairCandidate?.SourceBinding.OwnerEntityId}|sha256={pointPairCandidate?.SourceBinding.ContentSha256}|message={pointPairCandidateMessage}");
            workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var pointPairSelection = workbench.SelectedStepTeachingSelection;
            if (pointPairSelection?.Points?.Count != 2
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, pointPairSelection.SourceBinding))
            {
                return Complete(false, "Applied PointSet(2) was not routed with the exact A3 binding.");
            }
            lines.Add($"PointPair|id={pointPairSelection.Id}|first={pointPairSelection.Points[0].Locator.Row},{pointPairSelection.Points[0].Locator.Column}|second={pointPairSelection.Points[1].Locator.Row},{pointPairSelection.Points[1].Locator.Column}|owner={pointPairSelection.SourceBinding.OwnerEntityId}|sha256={pointPairSelection.SourceBinding.ContentSha256}");

            if (!await workbench.PreviewSelectedMeasurementAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Point Pair Preview was not publishable: {workbench.MeasurementExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsMeasurementPreviewPublished || workbench.CurrentMeasurementOutput is null)
            {
                return Complete(false, "Point Pair Publish did not preserve the exact Preview output.");
            }
            lines.Add($"PointPairResult|status={workbench.CurrentMeasurementOutput.Result.Status}|sha256={workbench.CurrentMeasurementOutput.ContentSha256}|evidence={workbench.CurrentMeasurementOutput.EvidenceSummary}|published=True");

            workbench.SelectedPipelineStep = gapFlushStep;
            var gapFirst = workbench.PlaneFlatnessReferenceSelection;
            var gapSecond = workbench.PlaneFlatnessMeasurementSelection;
            if (gapFirst?.GridRectangle is null || gapSecond?.GridRectangle is null
                || string.Equals(gapFirst.Id, gapSecond.Id, StringComparison.OrdinalIgnoreCase)
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, gapFirst.SourceBinding)
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, gapSecond.SourceBinding))
            {
                return Complete(false, "Gap / Flush did not expose two distinct ordered ROIs with the exact A3 binding.");
            }
            if (!await workbench.PreviewSelectedMeasurementAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Gap / Flush Preview was not publishable: {workbench.MeasurementExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsMeasurementPreviewPublished || workbench.CurrentMeasurementOutput is null)
            {
                return Complete(false, "Gap / Flush Publish did not preserve the exact Preview output.");
            }
            lines.Add($"GapFlush|first={gapFirst.Id}:{gapFirst.GridRectangle.Row},{gapFirst.GridRectangle.Column},{gapFirst.GridRectangle.RowCount},{gapFirst.GridRectangle.ColumnCount}|second={gapSecond.Id}:{gapSecond.GridRectangle.Row},{gapSecond.GridRectangle.Column},{gapSecond.GridRectangle.RowCount},{gapSecond.GridRectangle.ColumnCount}|status={workbench.CurrentMeasurementOutput.Result.Status}|sha256={workbench.CurrentMeasurementOutput.ContentSha256}|evidence={workbench.CurrentMeasurementOutput.EvidenceSummary}|published=True");

            workbench.SelectedPipelineStep = volumeStep;
            var volumeReference = workbench.PlaneFlatnessReferenceSelection;
            var volumeMeasurement = workbench.PlaneFlatnessMeasurementSelection;
            if (volumeReference?.GridRectangle is null || volumeMeasurement?.GridRectangle is null
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, volumeReference.SourceBinding)
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, volumeMeasurement.SourceBinding))
            {
                return Complete(false, "Volume did not expose ordered reference and measurement ROIs with the exact A3 binding.");
            }
            if (!await workbench.PreviewSelectedMeasurementAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Volume Preview was not publishable: {workbench.MeasurementExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsMeasurementPreviewPublished || workbench.CurrentMeasurementOutput is null)
            {
                return Complete(false, "Volume Publish did not preserve the exact Preview output.");
            }
            lines.Add($"Volume|reference={volumeReference.Id}:{volumeReference.GridRectangle.Row},{volumeReference.GridRectangle.Column},{volumeReference.GridRectangle.RowCount},{volumeReference.GridRectangle.ColumnCount}|measurement={volumeMeasurement.Id}:{volumeMeasurement.GridRectangle.Row},{volumeMeasurement.GridRectangle.Column},{volumeMeasurement.GridRectangle.RowCount},{volumeMeasurement.GridRectangle.ColumnCount}|status={workbench.CurrentMeasurementOutput.Result.Status}|sha256={workbench.CurrentMeasurementOutput.ContentSha256}|evidence={workbench.CurrentMeasurementOutput.EvidenceSummary}|published=True");

            workbench.SelectedPipelineStep = crossSectionStep;
            var crossSectionSelection = workbench.SelectedStepTeachingSelection;
            if (crossSectionSelection?.GridRectangle is not { RowCount: 1, ColumnCount: >= 2 }
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, crossSectionSelection.SourceBinding))
            {
                return Complete(false, "Cross-section did not expose one A3 row segment with the exact Published A3 binding.");
            }
            if (!await workbench.PreviewSelectedMeasurementAsync()
                || !workbench.PublishSelectedStepCommand.CanExecute(null))
            {
                return Complete(false, $"Cross-section Preview was not publishable: {workbench.MeasurementExecutionSummary}");
            }
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsMeasurementPreviewPublished || workbench.CurrentMeasurementOutput is null)
            {
                return Complete(false, "Cross-section Publish did not preserve the exact Preview output.");
            }
            lines.Add($"CrossSection|selection={crossSectionSelection.Id}:{crossSectionSelection.GridRectangle.Row},{crossSectionSelection.GridRectangle.Column},{crossSectionSelection.GridRectangle.RowCount},{crossSectionSelection.GridRectangle.ColumnCount}|status={workbench.CurrentMeasurementOutput.Result.Status}|sha256={workbench.CurrentMeasurementOutput.ContentSha256}|evidence={workbench.CurrentMeasurementOutput.EvidenceSummary}|published=True");

            var fullSavePath = Path.GetFullPath(savePath);
            if (!workbench.TrySaveTeachingRecipe(fullSavePath, out var saveMessage))
            {
                return Complete(false, saveMessage);
            }
            lines.Add($"Save|path={fullSavePath}|message={saveMessage}");

            if (!workbench.TryOpenTeachingRecipe(fullSavePath, out var reopenMessage))
            {
                return Complete(false, reopenMessage);
            }
            var reopenedStep = workbench.PipelineSteps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PlaneFlatnessStepId, StringComparison.OrdinalIgnoreCase));
            workbench.SelectedPipelineStep = reopenedStep;
            var reopenedReference = workbench.Selections.Single(selection =>
                string.Equals(selection.Id, reopenedStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenedMeasurement = workbench.Selections.Single(selection =>
                string.Equals(selection.Id, reopenedStep.InputEntityIds[2], StringComparison.OrdinalIgnoreCase));
            var reopenedDocument = ToolRecipeDocumentStore.Load(fullSavePath);
            var reopenedDocumentStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PlaneFlatnessStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedPointPairStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.PointPairStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedPointPair = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedPointPairStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenedGapStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.GapFlushStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedGapFirst = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedGapStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenedGapSecond = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedGapStep.InputEntityIds[2], StringComparison.OrdinalIgnoreCase));
            var reopenedVolumeStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.VolumeStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedVolumeReference = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedVolumeStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenedVolumeMeasurement = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedVolumeStep.InputEntityIds[2], StringComparison.OrdinalIgnoreCase));
            var reopenedCrossSectionStep = reopenedDocument.Steps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.CrossSectionStepId, StringComparison.OrdinalIgnoreCase));
            var reopenedCrossSection = reopenedDocument.Selections!.Single(selection =>
                string.Equals(selection.Id, reopenedCrossSectionStep.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var reopenPassed = reopenedDocument.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && reopenedStep.InputEntityIds.Count == 3
                && reopenedDocumentStep.InputEntityIds.SequenceEqual(reopenedStep.InputEntityIds, StringComparer.OrdinalIgnoreCase)
                && reopenedReference.GridRectangle == referenceSelection.GridRectangle
                && reopenedMeasurement.GridRectangle == measurementSelection.GridRectangle
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedReference.SourceBinding)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedMeasurement.SourceBinding)
                && reopenedPointPair.Points?.Count == 2
                && reopenedPointPair.Points.SequenceEqual(pointPairSelection.Points)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedPointPair.SourceBinding)
                && reopenedGapFirst.GridRectangle == gapFirst.GridRectangle
                && reopenedGapSecond.GridRectangle == gapSecond.GridRectangle
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedGapFirst.SourceBinding)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedGapSecond.SourceBinding)
                && reopenedVolumeReference.GridRectangle == volumeReference.GridRectangle
                && reopenedVolumeMeasurement.GridRectangle == volumeMeasurement.GridRectangle
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedVolumeReference.SourceBinding)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedVolumeMeasurement.SourceBinding)
                && reopenedCrossSection.GridRectangle == crossSectionSelection.GridRectangle
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, reopenedCrossSection.SourceBinding)
                && !workbench.IsDirty
                && ReferenceEquals(previewBefore, _viewer.ViewModel.PreviewToolResult)
                && ReferenceEquals(resultsBefore, _viewer.ViewModel.ResultEntities);
            lines.Add($"Reopen|schema={reopenedDocument.SchemaVersion}|stepInputs={string.Join(';', reopenedStep.InputEntityIds)}|reference={reopenedReference.Id}|measurement={reopenedMeasurement.Id}|pointPair={reopenedPointPair.Id}|gapFirst={reopenedGapFirst.Id}|gapSecond={reopenedGapSecond.Id}|volumeReference={reopenedVolumeReference.Id}|volumeMeasurement={reopenedVolumeMeasurement.Id}|crossSection={reopenedCrossSection.Id}|dirty={workbench.IsDirty}|message={reopenMessage}");
            workbench.SelectedPipelineStep = workbench.PipelineSteps.Single(step =>
                string.Equals(step.Id, PlaneFlatnessLiveA3PointerSmokeFixture.CrossSectionStepId, StringComparison.OrdinalIgnoreCase));
            _viewer.ShowWorkbenchRegridHeightField(publishedA3, isPublished: true);
            SyncAppliedTeachingSelections();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            return Complete(
                reopenPassed,
                reopenPassed
                    ? "One live Shell session published synthetic A3, taught Plane Flatness and Point Pair through real Viewer pointer input, explicitly Previewed/Published Point Pair, Gap / Flush, Volume, and Cross-section Dimensions, then saved and reopened exact geometry/binding evidence."
                    : "Saved/reopened Plane Flatness, Point Pair, Gap / Flush, Volume, or Cross-section geometry, A3 binding, or explicit-execution boundary did not match.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or OverflowException)
        {
            return Complete(false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private async Task<bool> RunToolTeachingSelectionSmokeAsync(string modeValue, string? reportPath)
    {
        var mode = modeValue.Trim().ToLowerInvariant();
        var workbench = _viewModel.Workbench;
        var step = workbench.SelectedPipelineStep;
        var previewBefore = _viewer.ViewModel.PreviewToolResult;
        var resultEntitiesBefore = _viewer.ViewModel.ResultEntities;
        var dirtyBefore = workbench.IsDirty;
        var schemaBefore = workbench.RecipeSchemaVersion;
        var selectionCountBefore = workbench.Selections.Count;
        var inputIdsBefore = step?.InputEntityIds.ToArray() ?? [];
        var lines = new List<string>
        {
            "OpenVisionLab 3D generic teaching-selection Shell smoke",
            $"Mode={mode}",
            $"Step={step?.Id ?? "(none)"}",
            $"AuthoredBefore|dirty={dirtyBefore}|schema={schemaBefore}|selections={selectionCountBefore}|inputs={string.Join(';', inputIdsBefore)}",
            $"ExecutionBefore|preview={previewBefore.Status}|results={resultEntitiesBefore.Count}"
        };

        bool Complete(bool passed, string message)
        {
            var state = _viewer.TeachingCaptureSnapshot;
            var previewUnchanged = ReferenceEquals(previewBefore, _viewer.ViewModel.PreviewToolResult);
            var resultsUnchanged = ReferenceEquals(resultEntitiesBefore, _viewer.ViewModel.ResultEntities);
            lines.Add($"CaptureAfter|active={state.IsActive}|progress={state.CapturedPointCount}/{state.RequiredPointCount}|canUndo={state.CanUndo}|canApply={state.CanApply}|message={state.Message}");
            lines.Add($"AuthoredAfter|dirty={workbench.IsDirty}|schema={workbench.RecipeSchemaVersion}|selections={workbench.Selections.Count}|inputs={string.Join(';', step?.InputEntityIds ?? [])}");
            lines.Add($"ExecutionAfter|preview={_viewer.ViewModel.PreviewToolResult.Status}|results={_viewer.ViewModel.ResultEntities.Count}|previewReferenceUnchanged={previewUnchanged}|resultReferenceUnchanged={resultsUnchanged}");
            lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{message}");
            WriteTeachingSelectionSmokeReport(reportPath, lines);
            Console.WriteLine(lines[^1]);
            if (!passed)
            {
                _viewModel.SetViewerSmokeFailed(message);
            }

            return passed;
        }

        bool AuthoredStateUnchanged() =>
            workbench.IsDirty == dirtyBefore
            && string.Equals(workbench.RecipeSchemaVersion, schemaBefore, StringComparison.Ordinal)
            && workbench.Selections.Count == selectionCountBefore
            && (step?.InputEntityIds ?? []).SequenceEqual(inputIdsBefore, StringComparer.OrdinalIgnoreCase);

        bool ExecutionStateUnchanged() =>
            ReferenceEquals(previewBefore, _viewer.ViewModel.PreviewToolResult)
            && ReferenceEquals(resultEntitiesBefore, _viewer.ViewModel.ResultEntities);

        if (step is null)
        {
            return Complete(false, "No teaching pipeline step is selected.");
        }

        if (dirtyBefore
            || !string.Equals(schemaBefore, ToolRecipeDocument.LegacySchemaVersion, StringComparison.Ordinal)
            || selectionCountBefore != 0)
        {
            return Complete(
                false,
                "Teaching-selection smoke requires a clean legacy 1.0 recipe with no authored selections.");
        }

        if (mode == "inactive")
        {
            var passed = !_viewer.TeachingCaptureSnapshot.IsActive
                && AuthoredStateUnchanged()
                && ExecutionStateUnchanged();
            return Complete(
                passed,
                passed
                    ? "Inactive state retained legacy authored data and did not run Preview or Run."
                    : "Inactive state changed capture, authored recipe, or execution evidence.");
        }

        if (mode is not ("capturing" or "applied"))
        {
            return Complete(false, $"Unsupported teaching-selection smoke mode: {modeValue}");
        }

        if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "The selected step cannot begin Viewer teaching capture.");
        }

        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        if (!_viewer.TeachingCaptureSnapshot.IsActive)
        {
            return Complete(false, "Viewer teaching capture did not become active.");
        }

        if (mode == "capturing")
        {
            var state = _viewer.TeachingCaptureSnapshot;
            var passed = state.CapturedPointCount == 0
                && state.RequiredPointCount == 2
                && !state.CanApply
                && AuthoredStateUnchanged()
                && ExecutionStateUnchanged();
            return Complete(
                passed,
                passed
                    ? "Capture ribbon is active at 0/2; transient capture did not dirty the recipe or run inspection."
                    : "Capturing state violated the transient authored/execution boundary.");
        }

        var cancelPointerReportPath = string.IsNullOrWhiteSpace(reportPath)
            ? null
            : Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
                $"{Path.GetFileNameWithoutExtension(reportPath)}.cancel-pointer.txt");
        if (!await _viewer.RunTeachingCapturePointerSmokeAsync(
                cancelWhenReady: false,
                cancelPointerReportPath))
        {
            return Complete(false, _viewer.HostState.ViewerStatus);
        }

        var cancelCandidateState = _viewer.TeachingCaptureSnapshot;
        if (!cancelCandidateState.IsActive
            || cancelCandidateState.CapturedPointCount != 2
            || !cancelCandidateState.CanApply
            || !workbench.CancelTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "Actual Viewer pointer capture did not produce a cancellable 2-point candidate.");
        }

        workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var cancelBoundaryPassed = !_viewer.TeachingCaptureSnapshot.IsActive
            && !workbench.IsTeachingSelectionCaptureActive
            && AuthoredStateUnchanged()
            && ExecutionStateUnchanged();
        lines.Add($"CancelBoundary|pass={cancelBoundaryPassed}|authoredUnchanged={AuthoredStateUnchanged()}|executionUnchanged={ExecutionStateUnchanged()}");
        if (!cancelBoundaryPassed)
        {
            return Complete(false, "Cancel after two real Viewer picks changed authored or execution state.");
        }

        if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "The selected step could not restart Viewer teaching capture after Cancel.");
        }

        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        if (!_viewer.TeachingCaptureSnapshot.IsActive
            || !await _viewer.RunTeachingCapturePointerSmokeAsync(exerciseNavigationGestures: false))
        {
            return Complete(false, "Viewer teaching capture could not restart and produce a second candidate after Cancel.");
        }

        var candidateState = _viewer.TeachingCaptureSnapshot;
        if (candidateState.CapturedPointCount != 2
            || !candidateState.CanApply
            || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "Second actual Viewer pointer capture did not produce an applicable 2-point candidate.");
        }

        workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        _viewer.ResetView();
        _viewer.FitAll();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        lines.Add("EvidenceView|reset=True|fitAll=True|inspectionExecution=False");
        var appliedSelection = workbench.SelectedStepTeachingSelection;
        var applied = !_viewer.TeachingCaptureSnapshot.IsActive
            && appliedSelection is not null
            && workbench.Selections.Count == selectionCountBefore + 1
            && string.Equals(workbench.RecipeSchemaVersion, ToolRecipeDocument.CurrentSchemaVersion, StringComparison.Ordinal)
            && workbench.IsDirty
            && step.InputEntityIds.Contains(appliedSelection.Id, StringComparer.OrdinalIgnoreCase)
            && ExecutionStateUnchanged();
        return Complete(
            applied,
            applied
                ? "Two real Viewer picks were applied, the recipe uses the current structured-selection schema, the step route became dirty, and Preview/Run remained untouched."
                : "Applying the Viewer candidate did not satisfy recipe persistence or execution-boundary checks.");
    }

    private static void WriteTeachingSelectionSmokeReport(string? path, IReadOnlyList<string> lines)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

    private async Task<bool> CaptureWindowWithRetryAsync(
        Window window,
        string path,
        string? qualityReportPath,
        string scope)
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
            UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            var result = WpfScreenshotCapture.Capture(window);
            var qualityLine = $"{scope}Screenshot|attempt={attempt}|{result.Quality.Summary}";
            qualityLines.Add(qualityLine);
            Console.WriteLine(qualityLine);
            if (result.Quality.IsAcceptable)
            {
                WpfScreenshotCapture.Save(result.Bitmap, fullPath);
                qualityLines.Add($"{scope}ScreenshotResult|accepted=True|attempts={attempt}|screenshot={fullPath}");
                WriteScreenshotQualityReport(qualityReportPath, qualityLines);
                return true;
            }

            var rejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            WpfScreenshotCapture.Save(result.Bitmap, rejectedPath);
            await Task.Delay(250);
        }

        qualityLines.Add($"{scope}ScreenshotResult|accepted=False|attempts={maximumAttempts}|screenshot={fullPath}");
        WriteScreenshotQualityReport(qualityReportPath, qualityLines);
        return false;
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
            _viewModel.Workbench.SetC3DSource(loadedSourcePath);
            _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
            if (_viewModel.IsWorkbenchWorkspaceSelected)
            {
                _viewer.ViewModel.HudDetailsVisible = false;
            }
            return;
        }

        OVLog.Write(LogCategory.UI, LogLevel.Error, $"Tool teaching recipe source load failed: {_viewer.HostState.ViewerStatus}");
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
            _viewModel.Workbench.SetC3DSource(Path.GetFullPath(dialog.FileName));
            _viewer.ViewModel.HudDetailsVisible = false;
            return;
        }

        await LoadWorkbenchC3DSourceAsync(dialog.FileName);
    }

    private async Task LoadWorkbenchC3DSourceAsync(string path, bool showFailureDialog = true)
    {
        var cancellation = new CancellationTokenSource();
        c3dSourceLoadCancellation = cancellation;
        var stopwatch = Stopwatch.StartNew();
        _viewModel.Workbench.BeginC3DSourceLoad(path);
        var progress = new Progress<double>(_viewModel.Workbench.ReportC3DSourceLoadProgress);

        try
        {
            if (await _viewer.LoadC3DSourceAsync(path, cancellation.Token, progress)
                && _viewer.CurrentC3DSourcePath is { } sourcePath)
            {
                _viewModel.Workbench.SetC3DSource(sourcePath);
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

    private void OnWorkbenchBeginTeachingCaptureRequested(
        object? sender,
        ToolWorkbenchTeachingCaptureRequestEventArgs args)
    {
        if (string.Equals(args.SourceBinding.Format, "TransformedHeightField", StringComparison.Ordinal))
        {
            if (!_viewModel.Workbench.TryGetPublishedRegridHeightFieldOutput(
                    args.SourceBinding.OwnerEntityId ?? string.Empty,
                    out var transformedHeightField)
                || transformedHeightField is null)
            {
                _viewModel.Workbench.RejectTeachingSelectionCapture(
                    "The ROI owner TransformedHeightField is not currently Published.");
                return;
            }
            _viewer.ShowWorkbenchRegridHeightField(transformedHeightField, isPublished: true, standaloneReferenceDisplay: true);
            SyncAppliedTeachingSelections();
        }
        else
        {
            _viewer.ClearWorkbenchRegridHeightField();
            _viewer.ViewModel.C3DSampleVisible = true;
        }

        var request = new TeachingCaptureRequest(
            args.SelectionId,
            args.SelectionName,
            args.Kind,
            args.RequiredPointCount,
            args.RootSourceId,
            args.FrameId,
            args.SourceBinding);
        if (!_viewer.BeginC3DTeachingCapture(request, out var message))
        {
            _viewModel.Workbench.RejectTeachingSelectionCapture(message);
            return;
        }

        ApplyViewerTeachingCaptureState(_viewer.TeachingCaptureSnapshot);
    }

    private void OnWorkbenchUndoTeachingCaptureRequested(object? sender, EventArgs args)
    {
        _viewer.UndoC3DTeachingCapture();
        ApplyViewerTeachingCaptureState(_viewer.TeachingCaptureSnapshot);
    }

    private void OnWorkbenchCancelTeachingCaptureRequested(object? sender, EventArgs args)
    {
        _viewer.CancelC3DTeachingCapture();
        ApplyViewerTeachingCaptureState(_viewer.TeachingCaptureSnapshot);
    }

    private void OnWorkbenchApplyTeachingCaptureRequested(object? sender, EventArgs args)
    {
        if (!_viewer.TryGetC3DTeachingCandidate(out var selection, out var message))
        {
            var state = _viewer.TeachingCaptureSnapshot;
            _viewModel.Workbench.UpdateTeachingSelectionCaptureState(
                state.IsActive,
                state.CapturedPointCount,
                state.RequiredPointCount,
                state.CanApply,
                message);
            return;
        }

        if (!_viewModel.Workbench.TryApplyCapturedTeachingSelection(selection, out message))
        {
            var state = _viewer.TeachingCaptureSnapshot;
            _viewModel.Workbench.UpdateTeachingSelectionCaptureState(
                state.IsActive,
                state.CapturedPointCount,
                state.RequiredPointCount,
                state.CanApply,
                message);
            return;
        }

        _viewer.ConfirmC3DTeachingCaptureApplied();
        SyncAppliedTeachingSelections();
    }

    private void OnViewerTeachingCaptureStateChanged(
        object? sender,
        TeachingCaptureStateChangedEventArgs args) =>
        ApplyViewerTeachingCaptureState(args.State);

    private void ApplyViewerTeachingCaptureState(TeachingCaptureState state)
    {
        if (state.IsActive)
        {
            ToolWorkbench.IsBottomPaneExpanded = false;
        }

        _viewModel.Workbench.UpdateTeachingSelectionCaptureState(
            state.IsActive,
            state.CapturedPointCount,
            state.RequiredPointCount,
            state.CanApply,
            state.Message);
    }

    private void SyncAppliedTeachingSelections() =>
        _viewer.SetAppliedTeachingSelections(_viewModel.Workbench.GetCurrentAppliedTeachingSelections());

    private void OnWorkbenchFilterDisplayRequested(
        object? sender,
        ToolWorkbenchFilterDisplayRequestEventArgs args)
    {
        if (filterToolLabWindow is { IsVisible: true })
        {
            filterToolLabWindow.ShowFilterResult(args);
            return;
        }

        var hashLabel = args.ContentSha256.Length >= 12
            ? args.ContentSha256[..12]
            : args.ContentSha256;
        var label = args.IsSource
            ? $"Source | {Path.GetFileName(args.C3DPath)}"
            : $"Filter Preview | {hashLabel}";
        if (_viewer.ShowC3DWorkbenchResult(args.C3DPath, label))
        {
            _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
            SyncAppliedTeachingSelections();
            return;
        }

        OVLog.Write(LogCategory.UI, LogLevel.Error, _viewer.HostState.ViewerStatus);
    }

    private void OnWorkbenchArtifactDisplayRequested(
        object? sender,
        ToolWorkbenchArtifactDisplayRequestEventArgs args)
    {
        var label = $"{args.DisplayName} | {args.Contract} | {args.State}";
        args.WasDisplayed = _viewer.ShowC3DWorkbenchResult(args.C3DPath, label);
        if (args.WasDisplayed)
        {
            _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
            SyncAppliedTeachingSelections();
            return;
        }

        OVLog.Write(LogCategory.UI, LogLevel.Error, _viewer.HostState.ViewerStatus);
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
        (preserveSelectedStep
            && string.Equals(_viewModel.Workbench.SelectedPipelineStep?.ToolId, toolId, StringComparison.Ordinal))
        || _viewModel.Workbench.SelectFirstPipelineStepForTool(toolId);

    private bool ShowFilterToolLabWindow(bool showMissingFilterMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("filter", preserveSelectedStep))
        {
            if (showMissingFilterMessage)
            {
                ShowMissingToolLabStep("Filter");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;

        if (filterToolLabWindow is null)
        {
            filterToolLabWindow = new FilterToolLabWindow(_viewModel.Workbench, step)
            {
                Owner = this
            };
            filterToolLabWindow.Closed += (_, _) => filterToolLabWindow = null;
        }
        else
        {
            filterToolLabWindow.SetLabStep(step);
        }

        filterToolLabWindow.RefreshViews();
        filterToolLabWindow.Show();
        filterToolLabWindow.Activate();
        return true;
    }

    private void OpenEdgeToolLabRequested(object? sender, EventArgs args)
    {
        ShowHeightDifferenceEdgeToolLabWindow(showMissingEdgeMessage: true);
    }

    private bool ShowHeightDifferenceEdgeToolLabWindow(bool showMissingEdgeMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("height-difference-edge", preserveSelectedStep))
        {
            if (showMissingEdgeMessage)
            {
                ShowMissingToolLabStep("Height Difference Edge");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;

        if (heightDifferenceEdgeToolLabWindow is null)
        {
            heightDifferenceEdgeToolLabWindow = new HeightDifferenceEdgeToolLabWindow(_viewModel.Workbench, step)
            {
                Owner = this
            };
            heightDifferenceEdgeToolLabWindow.Closed += (_, _) => heightDifferenceEdgeToolLabWindow = null;
        }
        else
        {
            heightDifferenceEdgeToolLabWindow.SetLabStep(step);
        }

        heightDifferenceEdgeToolLabWindow.RefreshViews();
        heightDifferenceEdgeToolLabWindow.Show();
        heightDifferenceEdgeToolLabWindow.Activate();
        return true;
    }

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

    private bool ShowTwoPointLineToolLabWindow(bool showMissingTwoPointLineMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("two-point-line", preserveSelectedStep))
        {
            if (showMissingTwoPointLineMessage)
            {
                ShowMissingToolLabStep("2-Point Line");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;
        if (twoPointLineToolLabWindow is null)
        {
            twoPointLineToolLabWindow = new TwoPointLineToolLabWindow(_viewModel.Workbench, step) { Owner = this };
            twoPointLineToolLabWindow.Closed += (_, _) => twoPointLineToolLabWindow = null;
        }
        else
        {
            twoPointLineToolLabWindow.SetLabStep(step);
        }

        twoPointLineToolLabWindow.RefreshViews();
        twoPointLineToolLabWindow.Show();
        twoPointLineToolLabWindow.Activate();
        return true;
    }

    private bool ShowThreePointPlaneToolLabWindow(bool showMissingThreePointPlaneMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("three-point-plane", preserveSelectedStep))
        {
            if (showMissingThreePointPlaneMessage)
            {
                ShowMissingToolLabStep("3-Point Plane");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;
        if (threePointPlaneToolLabWindow is null)
        {
            threePointPlaneToolLabWindow = new ThreePointPlaneToolLabWindow(_viewModel.Workbench, step) { Owner = this };
            threePointPlaneToolLabWindow.Closed += (_, _) => threePointPlaneToolLabWindow = null;
        }
        else
        {
            threePointPlaneToolLabWindow.SetLabStep(step);
        }

        threePointPlaneToolLabWindow.RefreshViews();
        threePointPlaneToolLabWindow.Show();
        threePointPlaneToolLabWindow.Activate();
        return true;
    }

    private bool ShowDatumPlaneDeviationToolLabWindow(bool showMissingDatumDeviationMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("datum-plane-raw-height-deviation", preserveSelectedStep))
        {
            if (showMissingDatumDeviationMessage)
            {
                ShowMissingToolLabStep("Datum Plane Raw-Height Deviation");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;
        if (datumPlaneDeviationToolLabWindow is null)
        {
            datumPlaneDeviationToolLabWindow = new DatumPlaneDeviationToolLabWindow(_viewModel.Workbench, step) { Owner = this };
            datumPlaneDeviationToolLabWindow.Closed += (_, _) => datumPlaneDeviationToolLabWindow = null;
        }
        else
        {
            datumPlaneDeviationToolLabWindow.SetLabStep(step);
        }

        datumPlaneDeviationToolLabWindow.RefreshViews();
        datumPlaneDeviationToolLabWindow.Show();
        datumPlaneDeviationToolLabWindow.Activate();
        return true;
    }

    private bool ShowLineIntersectionToolLabWindow(bool showMissingLineIntersectionMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("line-intersection", preserveSelectedStep))
        {
            if (showMissingLineIntersectionMessage)
            {
                ShowMissingToolLabStep("Line Intersection");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;

        if (lineIntersectionToolLabWindow is null)
        {
            lineIntersectionToolLabWindow = new LineIntersectionToolLabWindow(_viewModel.Workbench, step)
            {
                Owner = this
            };
            lineIntersectionToolLabWindow.Closed += (_, _) => lineIntersectionToolLabWindow = null;
        }
        else
        {
            lineIntersectionToolLabWindow.SetLabStep(step);
        }

        lineIntersectionToolLabWindow.RefreshViews();
        lineIntersectionToolLabWindow.Show();
        lineIntersectionToolLabWindow.Activate();
        return true;
    }

    private void OpenLandmarkCorrespondenceToolLabRequested(object? sender, EventArgs args)
    {
        ShowLandmarkCorrespondenceToolLabWindow(showMissingCorrespondenceMessage: true);
    }

    private bool ShowLandmarkCorrespondenceToolLabWindow(bool showMissingCorrespondenceMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("landmark-correspondence", preserveSelectedStep))
        {
            if (showMissingCorrespondenceMessage)
            {
                ShowMissingToolLabStep("Landmark Correspondence");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;

        if (landmarkCorrespondenceToolLabWindow is null)
        {
            landmarkCorrespondenceToolLabWindow = new LandmarkCorrespondenceToolLabWindow(_viewModel.Workbench, step)
            {
                Owner = this
            };
            landmarkCorrespondenceToolLabWindow.Closed += (_, _) => landmarkCorrespondenceToolLabWindow = null;
        }
        else
        {
            landmarkCorrespondenceToolLabWindow.SetLabStep(step);
        }

        landmarkCorrespondenceToolLabWindow.RefreshViews();
        landmarkCorrespondenceToolLabWindow.Show();
        landmarkCorrespondenceToolLabWindow.Activate();
        return true;
    }

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

    private bool ShowXYZAffineSolveToolLabWindow(bool showMissingAffineSolveMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("xyz-affine-solve", preserveSelectedStep))
        {
            if (showMissingAffineSolveMessage)
            {
                ShowMissingToolLabStep("XYZ Affine Solve");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;
        if (xyzAffineSolveToolLabWindow is null)
        {
            xyzAffineSolveToolLabWindow = new XYZAffineSolveToolLabWindow(_viewModel.Workbench, step)
            {
                Owner = this
            };
            xyzAffineSolveToolLabWindow.Closed += (_, _) => xyzAffineSolveToolLabWindow = null;
        }
        else
        {
            xyzAffineSolveToolLabWindow.SetLabStep(step);
        }

        xyzAffineSolveToolLabWindow.RefreshViews();
        xyzAffineSolveToolLabWindow.Show();
        xyzAffineSolveToolLabWindow.Activate();
        return true;
    }

    private bool ShowXYZAffineApplyToolLabWindow(bool showMissingAffineApplyMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("xyz-affine-apply", preserveSelectedStep))
        {
            if (showMissingAffineApplyMessage)
            {
                ShowMissingToolLabStep("Apply XYZ Affine");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;
        if (xyzAffineApplyToolLabWindow is null)
        {
            xyzAffineApplyToolLabWindow = new XYZAffineApplyToolLabWindow(_viewModel.Workbench, step)
            {
                Owner = this
            };
            xyzAffineApplyToolLabWindow.Closed += (_, _) => xyzAffineApplyToolLabWindow = null;
        }
        else
        {
            xyzAffineApplyToolLabWindow.SetLabStep(step);
        }

        xyzAffineApplyToolLabWindow.RefreshViews();
        xyzAffineApplyToolLabWindow.Show();
        xyzAffineApplyToolLabWindow.Activate();
        return true;
    }

    private bool ShowRegridHeightMapToolLabWindow(bool showMissingRegridMessage, bool preserveSelectedStep = false)
    {
        if (!EnsureToolLabStepSelected("re-grid-height-map", preserveSelectedStep))
        {
            if (showMissingRegridMessage)
            {
                ShowMissingToolLabStep("Re-grid Height Map");
            }
            return false;
        }

        var step = _viewModel.Workbench.SelectedPipelineStep!;
        if (regridHeightMapToolLabWindow is null)
        {
            regridHeightMapToolLabWindow = new RegridHeightMapToolLabWindow(_viewModel.Workbench, step)
            {
                Owner = this
            };
            regridHeightMapToolLabWindow.Closed += (_, _) => regridHeightMapToolLabWindow = null;
        }
        else
        {
            regridHeightMapToolLabWindow.SetLabStep(step);
        }

        regridHeightMapToolLabWindow.RefreshViews();
        regridHeightMapToolLabWindow.Show();
        regridHeightMapToolLabWindow.Activate();
        return true;
    }

    private void OnWorkbenchHeightDifferenceEdgeDisplayRequested(
        object? sender,
        ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs args)
    {
        if (heightDifferenceEdgeToolLabWindow is { IsVisible: true })
        {
            heightDifferenceEdgeToolLabWindow.ShowEdgeResult(args);
            return;
        }

        var state = args.IsPublished ? "Published" : "Preview - not published";
        var label = $"Height Difference Edge {state} | {args.Output.ContentSha256[..12]}";
        if (_viewer.ShowC3DWorkbenchResult(args.C3DPath, label))
        {
            _viewer.ShowWorkbenchHeightDifferenceEdge(args.Output, args.IsPublished);
            _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
            SyncAppliedTeachingSelections();
            return;
        }

        OVLog.Write(LogCategory.UI, LogLevel.Error, _viewer.HostState.ViewerStatus);
    }

    private void OnWorkbenchLineFitDisplayRequested(
        object? sender,
        ToolWorkbenchLineFitDisplayRequestEventArgs args)
    {
        _viewer.ShowWorkbenchLineFit(args.Output, args.IsPublished);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
        ToolWorkbench.ActivateFitDiagnosticsPane();
    }

    private void OnWorkbenchTwoPointLineDisplayRequested(
        object? sender,
        ToolWorkbenchTwoPointLineDisplayRequestEventArgs args)
    {
        if (twoPointLineToolLabWindow is { IsVisible: true })
        {
            twoPointLineToolLabWindow.ShowTwoPointLineResult(args);
        }
        _viewer.ShowWorkbenchTwoPointLine(args.Output, args.IsPublished);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
    }

    private void OnWorkbenchThreePointPlaneDisplayRequested(
        object? sender,
        ToolWorkbenchThreePointPlaneDisplayRequestEventArgs args)
    {
        if (threePointPlaneToolLabWindow is { IsVisible: true })
        {
            threePointPlaneToolLabWindow.ShowThreePointPlaneResult(args);
        }
        _viewer.ShowWorkbenchThreePointPlane(args.Output, args.IsPublished);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
    }

    private void OnWorkbenchDatumPlaneDeviationDisplayRequested(
        object? sender,
        ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs args)
    {
        if (datumPlaneDeviationToolLabWindow is { IsVisible: true })
        {
            datumPlaneDeviationToolLabWindow.ShowDatumPlaneDeviationResult(args);
        }
        _viewer.ShowWorkbenchDatumPlaneDeviation(args.Plane, args.MeasurementSelection, args.Output, args.IsPublished);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
    }

    private void OnWorkbenchLineIntersectionDisplayRequested(
        object? sender,
        ToolWorkbenchLineIntersectionDisplayRequestEventArgs args)
    {
        if (lineIntersectionToolLabWindow is { IsVisible: true })
        {
            lineIntersectionToolLabWindow.ShowLineIntersectionResult(args);
        }
        _viewer.ShowWorkbenchLineIntersection(args.FirstLine, args.SecondLine, args.Output, args.IsPublished);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
        if (_viewModel.IsExpertWorkspaceSelected)
        {
            Workspace.ActivateIntersectionEvidencePane();
        }
        else
        {
            ToolWorkbench.ActivateIntersectionEvidencePane();
        }
    }

    private void OnWorkbenchLandmarkCorrespondenceDisplayRequested(
        object? sender,
        ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs args)
    {
        if (landmarkCorrespondenceToolLabWindow is { IsVisible: true })
        {
            landmarkCorrespondenceToolLabWindow.ShowLandmarkCorrespondenceResult(args);
        }
        _viewer.ShowWorkbenchLandmarkCorrespondence(args.Anchors, args.Output, args.IsPublished);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
        if (_viewModel.IsExpertWorkspaceSelected)
        {
            Workspace.ActivateCorrespondenceEvidencePane();
        }
        else
        {
            ToolWorkbench.ActivateCorrespondenceEvidencePane();
        }
    }

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
            SyncAppliedTeachingSelections();
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
            _viewModel.Workbench.SetC3DSource(loadedSourcePath);
            SyncAppliedTeachingSelections();
        }
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

    private void SyncWorkbenchSourceFromViewer()
    {
        if (_viewer.CurrentC3DSourcePath is { } sourcePath
            && string.IsNullOrWhiteSpace(_viewModel.Workbench.Source.Path))
        {
            _viewModel.Workbench.SetC3DSource(sourcePath, markDirty: false);
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
