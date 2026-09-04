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
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.Layout;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Recipe;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop;
using WpfMessageDialogButtons = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogButtons;
using WpfMessageDialogKind = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogKind;
using WpfMessageDialogOptions = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogOptions;
using WpfMessageDialogResult = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogResult;
using WpfMessageDialogWindow = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogWindow;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    private readonly ShellMessageDialogController _messageDialogs;
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
    private readonly ShellCurrentRecipeRunSmoke _currentRecipeRunSmoke;
    private readonly ShellPreparationPreviewSmokeCoordinator _preparationPreviewSmoke;
    private readonly ShellRecipeMeasurementSmokeCoordinator _recipeMeasurementSmoke;
    private readonly ShellWorkbenchInteractionSmokeCoordinator _workbenchInteractionSmoke;
    private readonly ShellToolTeachingStartupCoordinator _toolTeachingStartup;
    private readonly ShellCommandLineArguments _commandLineArguments;
    private readonly ShellSmokeOperation _shellSmokeOperation = new();
    private readonly ShellSmokeScreenshotEvidenceCoordinator _shellSmokeScreenshotEvidence;
    private readonly ShellNominalActualPreviewWaiter _nominalActualPreviewWaiter;
    private readonly ShellStartupConfigurationPlan _startupConfiguration;
    private readonly ShellStartupViewerProjectionCoordinator _startupViewerProjection;
    private RoutedEventHandler _shellSmokeLoadedHandler = (_, _) => { };
    private DispatcherTimer? _importDialogSmokeTimer;
    private DispatcherOperation? _expertViewerActivationOperation;
    private bool _isClosed;
    private ShellValidationSetSmokeState validationSetSmoke =
        ShellValidationSetSmokeState.Empty;

    public MainWindow()
    {
        _commandLineArguments = ShellCommandLineArguments.Capture();
        _startupConfiguration = ShellStartupConfigurationPlanner.Parse(
            _commandLineArguments);
        OpenVisionLanguageService.Load();
        ApplyCommandLineLanguage(_startupConfiguration);
        OVLog.Write(LogCategory.System, LogLevel.Info, "OpenVisionLab 3D Studio starting.");
        _viewer = new OpenVisionThreeDViewerControl(
            loadDefaultSamples: !_startupConfiguration.ShouldStartWithEmptyRecipeInput);
        _nominalActualPreviewWaiter = new ShellNominalActualPreviewWaiter(
            () => _viewer.ViewModel.NominalActual.State);
        InitializeComponent();
        _viewModel = new ShellMainWindowViewModel(
            _commandLineArguments.GetValue("--recipe-comparison-contract"),
            _commandLineArguments.GetValue("--recipe-comparison-report"),
            _commandLineArguments.GetValue("--shell-smoke-screenshot"),
            _commandLineArguments.GetValue("--run-record"),
            _commandLineArguments.GetValue("--html-report"),
            _commandLineArguments.GetValue("--csv-report"),
            recentRecipesPath: _startupConfiguration.IsAutomatedShellRun
                ? null
                : GetPersistentRecentRecipesPath(),
            integrationSettingsPath: _commandLineArguments.GetValue("--smoke-integration-settings"));
        _viewModel.SelectedEvidenceTabIndex = _startupConfiguration.EvidenceTabIndex;
        DataContext = _viewModel;
        _messageDialogs = new ShellMessageDialogController(
            GetRecipeLifecycleDialogOwner,
            _viewModel,
            DialogText);
        _toolLabWindows = new ToolLabWindowManager(this, _viewModel.Workbench, _messageDialogs.ShowMissingToolLabStep);
        _preparationPresetSmoke = new ShellPreparationPresetAssistantSmoke(
            _viewModel.Workbench,
            ToolWorkbench);
        _validationThresholdSmoke = new ShellValidationThresholdAssistantSmoke(
            _viewModel.Workbench,
            ToolWorkbench);
        _shellSmokeScreenshotEvidence = new ShellSmokeScreenshotEvidenceCoordinator(
            new ShellSmokeScreenshotEvidenceCallbacks
            {
                AppendWindowMonitorEvidence = reportPath =>
                    AppendWindowMonitorEvidence(this, reportPath),
                AppendValidationThresholdEvidence = reportPath =>
                    _validationThresholdSmoke.AppendEvidence(this, reportPath),
                AppendPreparationPresetEvidence = (state, reportPath) =>
                    _preparationPresetSmoke.AppendEvidence(state, reportPath)
            },
            (path, lines) => File.AppendAllLines(path, lines));
        _surfaceMatchInteractionSmoke = new ShellSurfaceMatchInteractionSmoke(
            _viewModel.Workbench,
            ToolWorkbench,
            SetCursorPos,
            Dispatcher);
        _viewerWorkspaceSmoke = new ShellViewerWorkspaceSmoke(
            ToolWorkbench,
            Dispatcher);
        _currentRecipeRunSmoke = new ShellCurrentRecipeRunSmoke(
            _viewModel,
            Dispatcher,
            () => FindVisualDescendants<System.Windows.Controls.Button>(this)
                .FirstOrDefault(button =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(button)
                    == "RunCurrentRecipeButton"));
        _preparationPreviewSmoke = new ShellPreparationPreviewSmokeCoordinator(
            _viewModel.Workbench);
        _recipeMeasurementSmoke = new ShellRecipeMeasurementSmokeCoordinator(
            _viewModel.Workbench);
        _workbenchInteractionSmoke = new ShellWorkbenchInteractionSmokeCoordinator(
            _viewModel.Workbench);
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
                ShowLoadSourceFailure = _messageDialogs.ShowLoadSourceFailure,
                ShowRecipeSaveFailure = _messageDialogs.ShowRecipeSaveFailure,
                ShowFirstRecipeCreateFailure = _messageDialogs.ShowFirstRecipeCreateFailure,
                ShowFirstRecipeSetupPersistenceFailure = _messageDialogs.ShowFirstRecipeSetupPersistenceFailure,
                ShowRecipeFileUnavailable = _messageDialogs.ShowRecipeFileUnavailable,
                ShowRecipeOpenFailure = _messageDialogs.ShowRecipeOpenFailure,
                ShowRecipeSourceNotReady = _messageDialogs.ShowRecipeSourceNotReady,
                ShowRecipeSourceLoadFailure = _messageDialogs.ShowRecipeSourceLoadFailure,
                ShowParameterApplyFailure = _messageDialogs.ShowParameterApplyFailure,
                ConfirmUnsavedRecipeChanges = () => ToLifecycleDialogChoice(_messageDialogs.ConfirmUnsavedRecipeChanges()),
                ConfirmPendingParameterChanges = () => ToLifecycleDialogChoice(_messageDialogs.ConfirmPendingParameterChanges()),
                CommitPendingParameterEdit = () =>
                {
                    var success = ToolWorkbench.CommitPendingParameterEdit(out var message);
                    return (success, message);
                },
                DiscardPendingParameterChanges = () => _viewModel.Workbench.DiscardSelectedStepParameterDraft(),
                ActivateWorkbench = ActivateWorkbenchAfterRecipeLifecycle,
                DialogText = DialogText
            });
        _toolTeachingStartup = new ShellToolTeachingStartupCoordinator(
            _viewModel.Workbench,
            new ShellToolTeachingStartupCallbacks
            {
                ClearViewerSource = _viewer.ClearC3DTeachingSource,
                UpdateSampleVisible = _viewModel.UpdateC3DSampleVisible,
                ViewerSampleVisible = () => _viewer.HostState.C3DSampleVisible,
                IsViewerSourceAlreadyLoaded = _workbenchLifecycle.IsViewerSourceAlreadyLoaded,
                LoadViewerSource = _viewer.LoadC3DSource,
                CurrentViewerSourcePath = () => _viewer.CurrentC3DSourcePath,
                ViewerStatus = () => _viewer.HostState.ViewerStatus,
                SetWorkbenchSourceFromViewer = path => _workbenchLifecycle.SetWorkbenchC3DSourceFromViewer(path),
                IsWorkbenchWorkspaceSelected = () => _viewModel.IsWorkbenchWorkspaceSelected,
                HideWorkbenchHudDetails = () => _viewer.ViewModel.HudDetailsVisible = false
            });
        if (Workspace.ProfileContent is Views.Workbench.HeightProfileView advancedHeightProfileView)
        {
            advancedHeightProfileView.DataContext = _viewer.ViewModel;
        }
        OVLog.Write(LogCategory.UI, LogLevel.Info, "Tool Workbench is the default Shell workspace.");
        if (_startupConfiguration.ShouldStartWithEmptyRecipeInput)
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
        ConfigureWorkspaceFromCommandLine(_startupConfiguration);
        ConfigureInspectionTaskFromCommandLine(_startupConfiguration);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);

        _viewerHostStateChangedHandler = OnViewerHostStateChanged;
        _viewer.HostStateChanged += _viewerHostStateChangedHandler;
        _viewer.EnableSmokeFromCommandLine(ownsApplicationLifecycle: false);

        _evidenceDialogs = new ShellEvidenceDialogController(
            this,
            _viewModel,
            new ShellEvidenceDialogErrors
            {
                ArtifactMissing = _messageDialogs.ShowEvidenceArtifactMissing,
                ArtifactOpenFailure = _messageDialogs.ShowEvidenceArtifactOpenFailure,
                RunRecordOpenFailure = _messageDialogs.ShowRunRecordOpenFailure,
                RunRecordExportFailure = _messageDialogs.ShowRunRecordExportFailure
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
                RemoveSelectedStep = _messageDialogs.OnWorkbenchRemoveSelectedStepRequested,
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
        if (!_startupConfiguration.IsAutomatedShellRun)
        {
            _workbenchLifecycle.RestoreMostRecentWorkbenchRecipe();
        }
        ConfigureOutputCompareFromCommandLine(_startupConfiguration);
        ConfigureValidationSetFromCommandLine();
        ConfigureWorkbenchBottomPaneFromCommandLine(_startupConfiguration);
        ConfigureC3DSourceLoadProgressFromCommandLine(_startupConfiguration);
        _workbenchViewerTeaching.SyncAppliedSelections();
        _studioLayout = new StudioLayoutController(
            this,
            ToolWorkbench,
            Workspace,
            _viewModel,
            _startupConfiguration.IsAutomatedShellRun,
            _commandLineArguments.GetValue("--smoke-layout-profile"),
            _commandLineArguments.GetValue("--smoke-layout-state-report"));
        _startupViewerProjection = new ShellStartupViewerProjectionCoordinator(
            new ShellStartupViewerProjectionCallbacks
            {
                SelectWorkbenchWorkspace = () => _viewModel.IsSetupWorkspaceSelected = true,
                SelectTeachWorkspace = () => _viewModel.IsTeachWorkspaceSelected = true,
                SelectInspectWorkspace = () => _viewModel.IsValidateWorkspaceSelected = true,
                SelectReviewWorkspace = () => _viewModel.IsResultsWorkspaceSelected = true,
                UseTopView = _viewer.UseTopView,
                UsePerspectiveView = _viewer.UsePerspectiveView,
                FitRoi = _viewer.FitRoi,
                SetHeightColorMinimumRaw = value => _viewer.ViewModel.C3DHeightColorMinimumRaw = value,
                SetHeightColorMaximumRaw = value => _viewer.ViewModel.C3DHeightColorMaximumRaw = value
            });
        Loaded += ConfigureViewerViewFromCommandLine;
        Loaded += EnsureWorkbenchViewerSourceConsistency;
        EnableShellSmokeFromCommandLine();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_startupConfiguration.IsAutomatedShellRun
            && !_workbenchLifecycle.TryResolveWorkbenchChanges("closing 3D Studio"))
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
            source.AddHook(ShellWindowNativeInterop.ConstrainMaximizeToWorkArea);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _shellSmokeOperation.Dispose();
        CancelExpertViewerActivation();
        StopImportDialogSmokeTimer();
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
        _toolLabWindows.Dispose();
        ToolWorkbench.Dispose();
        _viewer.Dispose();
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
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private void StartImportDialogSmokeTimer()
    {
        StopImportDialogSmokeTimer();
        if (_isClosed)
        {
            return;
        }

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        timer.Tick += OnImportDialogSmokeTimerTick;
        _importDialogSmokeTimer = timer;
        try
        {
            timer.Start();
        }
        catch
        {
            StopImportDialogSmokeTimer();
            throw;
        }
    }

    private void StopImportDialogSmokeTimer()
    {
        if (_importDialogSmokeTimer is not { } timer)
        {
            return;
        }

        timer.Stop();
        timer.Tick -= OnImportDialogSmokeTimerTick;
        _importDialogSmokeTimer = null;
    }

    private void OnImportDialogSmokeTimerTick(object? sender, EventArgs args)
    {
        StopImportDialogSmokeTimer();
        if (_isClosed)
        {
            return;
        }

        _viewModel.Workbench.Import3DDataCommand.Execute(null);
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
        var smoke = ShellSmokeCommandLineOptions.Parse(_commandLineArguments);
        var shellScreenshotPath = smoke.ShellScreenshotPath;
        var screenshotQualityReportPath = smoke.ScreenshotQualityReportPath;
        var viewerLayoutSmoke = smoke.ViewerLayoutSmoke;
        var viewerPresentationSmoke = smoke.ViewerPresentationSmoke;
        var viewerPresentationPressedSmoke = smoke.ViewerPresentationPressedSmoke;
        string? viewerPresentationCameraLinkSmokeSummary = null;
        var integrationExchangeSmokeState = smoke.IntegrationExchangeSmokeState;
        var integrationExchangeExeRole = smoke.IntegrationExchangeExeRole;
        ShellIntegrationExchangeSmokeResult? integrationExchangeSmoke = null;
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
            _ = TryGetLeftmostWorkAreaOrigin(out var left, out var top);
            Left = left;
            Top = top;
        }

        var smokePublishResult = smoke.SmokePublishResult;
        var waitForNominalActualPreview = smoke.WaitForNominalActualPreview
            || _viewer.ViewModel.NominalActualInput is not null;
        if (smoke.ShouldAttachLoadedHandler(_viewer.HasConfiguredSmokeScreenshot))
        {
            _shellSmokeLoadedHandler = async (_, _) =>
            {
                if (!_shellSmokeOperation.TryEnter())
                {
                    return;
                }

                await Dispatcher.InvokeAsync(() => { });
                if (_isClosed || !_shellSmokeOperation.IsActive)
                {
                    return;
                }

                if (smoke.OpenImport3DDataDialogSmoke)
                {
                    StartImportDialogSmokeTimer();
                    return;
                }
                ConfigureResultsSectionFromCommandLine(_startupConfiguration);
                if (!TryConfigureSurfaceMatchEvidenceFromCommandLine(
                        out var surfaceMatchFailure))
                {
                    _viewModel.SetViewerSmokeFailed(
                        surfaceMatchFailure);
                    Application.Current.Shutdown(1);
                    return;
                }
                if (_commandLineArguments.HasFlag("--smoke-focus-selected-tool"))
                {
                    ToolWorkbench.ActivateSelectedToolPane();
                }

                if (_commandLineArguments.HasFlag("--smoke-collapse-selected-tool"))
                {
                    ToolWorkbench.ToggleSelectedToolSideCollapse();
                }

                if (workbenchRunLogSmoke)
                {
                    ToolWorkbench.ActivateSessionLogPane();
                }
                if (currentRecipeRunReadySmoke || currentRecipeRunPressedSmoke)
                {
                    var currentRecipeRun = await _currentRecipeRunSmoke.PrepareAsync(
                        runSmoke: true,
                        cancellationToken: _shellSmokeOperation.Token);
                    if (currentRecipeRun.IsCanceled
                        || !_shellSmokeOperation.IsActive)
                    {
                        return;
                    }
                    if (!currentRecipeRun.Succeeded)
                    {
                        _viewModel.SetViewerSmokeFailed(
                            currentRecipeRun.Failure!);
                        Application.Current.Shutdown(1);
                        return;
                    }
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
                        await ShellSourceQualitySmoke.ConfigureAcquisitionProvenanceStateAsync(
                            _viewModel.Workbench,
                            ToolWorkbench,
                            Dispatcher,
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
                            ? _messageDialogs.CreateRecipeStepRemovalDialogOptions(request)
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

                var preparationPreview = await _preparationPreviewSmoke.RunAsync(
                    new ShellPreparationPreviewSmokeRequest(
                        smoke.FilterPreviewSmoke,
                        smoke.PreparationQualityComparisonSmoke,
                        smoke.RemoveOutlierPreviewSmoke,
                        smoke.LevelSurfacePreviewSmoke,
                        smoke.RoiCropPreviewSmoke,
                        smoke.MeasurementPreviewSmoke,
                        edgeStepId));
                if (!preparationPreview.Succeeded)
                {
                    _viewModel.SetViewerSmokeFailed(preparationPreview.Failure!);
                    Application.Current.Shutdown(1);
                    return;
                }

                if (preparationPreview.BringMeasurementOutputIntoView)
                {
                    ToolWorkbench.BringSelectedOutputIntoView();
                    await Dispatcher.InvokeAsync(() => { });
                }

                var recipeMeasurementSmokeFailure =
                    await _recipeMeasurementSmoke.RunAsync(
                        new ShellRecipeMeasurementSmokeRequest
                        {
                            ThicknessRepeatGridMode = thicknessRepeatGridSmoke,
                            FilterPublish = filterPublishSmoke,
                            TwoPointLinePreview = twoPointLinePreviewSmoke,
                            TwoPointLinePublish = twoPointLinePublishSmoke,
                            ThreePointPlanePreview = threePointPlanePreviewSmoke,
                            ThreePointPlanePublish = threePointPlanePublishSmoke,
                            DatumPlaneDeviationPreview = datumPlaneDeviationPreviewSmoke,
                            DatumPlaneDeviationPublish = datumPlaneDeviationPublishSmoke,
                            EdgePreview = edgePreviewSmoke,
                            EdgePublish = edgePublishSmoke,
                            LineFitPreview = lineFitPreviewSmoke,
                            EdgeStepId = edgeStepId,
                            EdgeSmokeReportPath = edgeSmokeReportPath,
                            LineFitSmokeReportPath = lineFitSmokeReportPath
                        },
                        () => ToolWorkbench.BringThicknessRepeatGridIntoView(),
                        async () => await Dispatcher.InvokeAsync(
                            () => { },
                            DispatcherPriority.Render));
                if (recipeMeasurementSmokeFailure is not null)
                {
                    _viewModel.SetViewerSmokeFailed(recipeMeasurementSmokeFailure);
                    Application.Current.Shutdown(1);
                    return;
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
                    && !await ShellHeightImagePaletteStateSmoke.RunAsync(
                        this,
                        ToolWorkbench,
                        _viewModel.Workbench,
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
                    || await _nominalActualPreviewWaiter.WaitAsync(
                        TimeSpan.FromMinutes(10),
                        _shellSmokeOperation.Token);
                if (!_shellSmokeOperation.IsActive || _isClosed)
                {
                    return;
                }
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

                if (!await _shellSmokeOperation.DelayAsync(TimeSpan.FromMilliseconds(900)))
                {
                    return;
                }
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

                var workbenchInteractionSmokeFailure =
                    await _workbenchInteractionSmoke.RunAsync(
                        new ShellWorkbenchInteractionSmokeRequest
                        {
                            SurfaceMatchExperimentPreview = surfaceMatchExperimentPreviewSmoke,
                            CollectionNavigationFocusHover = surfaceMatchCollectionNavigationFocusHoverSmoke,
                            CollectionDisabled = surfaceMatchCollectionDisabledSmoke,
                            ExperimentFocusHover = surfaceMatchExperimentFocusHoverSmoke,
                            CollectionPopup = surfaceMatchCollectionPopupSmoke,
                            CollectionPopupScreenshotPath = surfaceMatchCollectionPopupScreenshotPath,
                            WorkbenchInteractionReportPath = workbenchInteractionReportPath,
                            SelectedToolId = smokeSelectToolId
                        },
                        UpdateLayout,
                        () => Dispatcher.InvokeAsync(
                            () => { },
                            DispatcherPriority.Render).Task,
                        () => _surfaceMatchInteractionSmoke.RunAsync(
                            surfaceMatchCollectionNavigationFocusHoverSmoke,
                            surfaceMatchCollectionDisabledSmoke,
                            surfaceMatchExperimentFocusHoverSmoke,
                            surfaceMatchCollectionPopupSmoke,
                            surfaceMatchCollectionPopupScreenshotPath),
                        () => ToolWorkbench.GetSelectedToolVisibleTextLayout());
                if (workbenchInteractionSmokeFailure is not null)
                {
                    _viewModel.SetViewerSmokeFailed(workbenchInteractionSmokeFailure);
                    Application.Current.Shutdown(1);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(integrationExchangeSmokeState))
                {
                    var integrationExchangeOutcome =
                        await ShellIntegrationExchangeSmoke.RunAsync(
                            integrationExchangeSmokeState,
                            _viewModel.IsIntegrationExchangeSelected,
                            _viewModel.IntegrationExchange,
                            this,
                            Dispatcher,
                            _shellSmokeOperation.Token);
                    integrationExchangeSmoke = integrationExchangeOutcome;
                    if (integrationExchangeOutcome.IsCanceled
                        || !_shellSmokeOperation.IsActive)
                    {
                        return;
                    }
                    if (!integrationExchangeOutcome.Succeeded)
                    {
                        _viewModel.SetViewerSmokeFailed(integrationExchangeOutcome.Failure!);
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(integrationExchangeExeRole))
                {
                    var integrationExchangeExeOutcome =
                        await ShellIntegrationExchangeExeSmoke.RunAsync(
                            integrationExchangeExeRole,
                            _viewModel,
                            () => IsVisible,
                            () => Dispatcher.InvokeAsync(
                                () => { },
                                DispatcherPriority.ApplicationIdle).Task,
                            smoke.IntegrationExchangeExeReportPath,
                            _commandLineArguments,
                            _shellSmokeOperation.Token);
                    if (!integrationExchangeExeOutcome.Succeeded)
                    {
                        _viewModel.SetViewerSmokeFailed(
                            integrationExchangeExeOutcome.Failure!);
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (validationSetSmoke.ThresholdSelectionTask is not null)
                {
                    await validationSetSmoke.ThresholdSelectionTask;
                    await Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                }

                if (validationSetSmoke.SectionSelectionTask is not null)
                {
                    await validationSetSmoke.SectionSelectionTask;
                    await Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                }

                if (validationThresholdAssistantPressedSmoke
                    || validationThresholdAssistantDisabledSmoke)
                {
                    await _validationThresholdSmoke.ReassertForCaptureAsync();
                }

                if (!await _shellSmokeOperation.DelayAsync(TimeSpan.FromMilliseconds(100)))
                {
                    return;
                }
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
                var screenshotTarget = ShellSmokeScreenshotTargetSelector.Select(
                    new ShellSmokeScreenshotTargetRequest
                    {
                        Import3DDataPressed = import3DDataPressedSmoke,
                        ValidationThresholdAssistantPressed = validationThresholdAssistantPressedSmoke,
                        ViewerToolbarPressed = viewerToolbarPressedSmoke,
                        ViewerPresentationPressed = viewerPresentationPressedSmoke,
                        RecipeHealthNavigationPressed = recipeHealthNavigationPressedSmoke,
                        SupportBundlePressed = supportBundlePressedSmoke,
                        CurrentRecipeRunPressed = currentRecipeRunPressedSmoke,
                        IntegrationExchangePressed = integrationExchangeSmoke?.HasPressedCapture == true,
                        IntegrationExchangeAutomationId = integrationExchangeSmoke?.PressedCaptureAutomationId,
                        IntegrationExchangeScope = integrationExchangeSmoke?.PressedCaptureScope,
                        PreparationPresetAssistantMode = preparationPresetAssistantSmoke
                    });
                var screenshotCaptureSucceeded = true;
                if (shellScreenshotPath is not null)
                {
                    screenshotCaptureSucceeded = screenshotTarget switch
                    {
                        { Kind: ShellSmokeScreenshotTargetKind.Button } target =>
                            await CaptureButtonPressedForSmokeAsync(
                                this,
                                target.AutomationId!,
                                shellScreenshotPath,
                                screenshotQualityReportPath,
                                target.Scope!),
                        { Kind: ShellSmokeScreenshotTargetKind.RecipeHealthNavigation } =>
                            await CaptureRecipeHealthNavigationPressedForSmokeAsync(
                                this,
                                shellScreenshotPath,
                                screenshotQualityReportPath),
                        _ => await CaptureWindowWithRetryAsync(
                            this,
                            shellScreenshotPath,
                            screenshotQualityReportPath,
                            "Shell")
                    };
                }
                if (shellScreenshotPath is not null
                    && !screenshotCaptureSucceeded)
                {
                    _viewModel.SetViewerSmokeFailed("Shell screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }
                if (shellScreenshotPath is not null)
                {
                    _shellSmokeScreenshotEvidence.Append(
                        new ShellSmokeScreenshotEvidenceRequest
                        {
                            QualityReportPath = screenshotQualityReportPath,
                            ViewerPresentationCameraLinkSummary = viewerPresentationCameraLinkSmokeSummary,
                            AppendValidationThresholdEvidence = validationSetSmoke.ThresholdSelectionTask is not null,
                            IntegrationExchangeEvidenceLine = integrationExchangeSmoke?.EvidenceLine,
                            PreparationPresetAssistantMode = preparationPresetAssistantSmoke
                        });
                }
                if (currentRecipeRunPressedSmoke)
                {
                    var currentRecipeRun =
                        await _currentRecipeRunSmoke.ExecuteAfterCaptureAsync(
                            pressedSmoke: true,
                            screenshotQualityReportPath: screenshotQualityReportPath,
                            cancellationToken: _shellSmokeOperation.Token);
                    if (currentRecipeRun.IsCanceled
                        || !_shellSmokeOperation.IsActive)
                    {
                        return;
                    }
                    if (!currentRecipeRun.Succeeded)
                    {
                        _viewModel.SetViewerSmokeFailed(
                            currentRecipeRun.Failure!);
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
                if (!await _shellSmokeOperation.DelayAsync(TimeSpan.FromMilliseconds(100)))
                {
                    return;
                }
                toolLabSmoke.CloseTemporaryWindows(smoke);
                if (messageDialogSmokeWindow is { IsVisible: true })
                {
                    messageDialogSmokeWindow.Close();
                }
                if (recipeManagerScreenshotPath is not null
                    && !ShellRecipeLifecycleSmoke.RunWindowLifetime(
                        _workbenchLifecycle.ShowRecipeManagerWindow,
                        _workbenchLifecycle.CloseRecipeManager,
                        () => _workbenchLifecycle.RecipeManagerWindow,
                        () => _workbenchLifecycle.IsRecipeManagerVisible,
                        _workbenchLifecycle.Dispose,
                        recipeManagerScreenshotQualityReportPath))
                {
                    _viewModel.SetViewerSmokeFailed("Recipe Manager window lifetime did not preserve hide/reopen semantics or clear the forced-close reference.");
                    Application.Current.Shutdown(1);
                    return;
                }
                Application.Current.Shutdown(
                    nominalActualReady ? _viewer.SmokeExitCode : 1);
            };

            Loaded += _shellSmokeLoadedHandler;
        }
    }

    private bool TryConfigureSurfaceMatchEvidenceFromCommandLine(
        out string failure) =>
        ShellSurfaceMatchSmoke.TryConfigureEvidenceFromCommandLine(
            _commandLineArguments,
            _viewModel.Workbench,
            out failure);

    private static void ApplyCommandLineLanguage(ShellStartupConfigurationPlan configuration)
    {
        if (configuration.RequestedLanguage is not { } requestedLanguage)
        {
            return;
        }

        OpenVisionLanguageService.SetLanguage(requestedLanguage, save: false);
    }

    private void ConfigureCalibrationStudyFromCommandLine()
    {
        var studyPath = _commandLineArguments.GetValue("--calibration-study");
        if (studyPath is null)
        {
            return;
        }

        _viewModel.IsCalibrationWorkspaceSelected = true;
        _viewModel.Calibration.SelectedSection = CalibrationSection.Repeatability;
        if (_viewModel.Calibration.LoadStudy(studyPath)
            && _commandLineArguments.HasFlag("--smoke-calibration-calculate"))
        {
            _viewModel.Calibration.CalculateCommand.Execute(null);
        }
    }

    private void ConfigureToolTeachingRecipeFromCommandLine()
    {
        var result = _toolTeachingStartup.Configure(
            new ShellToolTeachingStartupRequest(
                _commandLineArguments.GetValue("--plane-flatness-live-a3-fixture"),
                _commandLineArguments.GetValue("--tool-teaching-recipe"),
                _commandLineArguments.GetValue("--tool-teaching-step")));
        if (result.SmokeFailure is { } failure)
        {
            _viewModel.SetViewerSmokeFailed(failure);
        }
    }

    private void RestoreStartupRunRecordAfterRecipeLoad()
    {
        var requestedRunRecord = _commandLineArguments.GetValue("--run-record");
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

    private Task<bool> RunHeightImageDisplayRangeSmokeAsync(
        string? paletteText,
        double? minimum,
        double? maximum,
        string? reportPath) =>
        ShellHeightImageDisplayRangeSmoke.RunAsync(
            paletteText,
            minimum,
            maximum,
            reportPath,
            _viewModel.Workbench,
            _viewer,
            Dispatcher);
    private Task<bool> RunSharedHeightHoverSmokeAsync(
        int? row,
        int? column,
        string? reportPath) =>
        ShellSharedHeightHoverSmoke.RunAsync(
            _viewModel.Workbench,
            _viewer,
            Dispatcher,
            row,
            column,
            reportPath);

    private Task<bool> RunHeightImageRoiPointerSmokeAsync(
        string mode,
        string? reportPath,
        string? savePath) =>
        ShellHeightImageRoiPointerSmoke.RunAsync(
            mode,
            reportPath,
            savePath,
            _viewModel.Workbench,
            ToolWorkbench,
            _viewer,
            Dispatcher);

    private void ConfigureWorkspaceFromCommandLine(ShellStartupConfigurationPlan configuration)
    {
        if (configuration.Workspace is { } workspace)
        {
            _viewModel.SelectWorkspaceCommand.Execute(workspace);
        }

        ConfigureResultsSectionFromCommandLine(configuration);
    }

    private void ConfigureResultsSectionFromCommandLine(
        ShellStartupConfigurationPlan configuration)
    {
        if (_viewModel.IsResultsWorkspaceSelected
            && configuration.ResultsSection is { } resultsSection)
        {
            ToolWorkbench.SetResultsWorkspaceSection(resultsSection);
        }
    }

    private void ConfigureViewerViewFromCommandLine(object sender, RoutedEventArgs e) =>
        _startupViewerProjection.Apply(_startupConfiguration);

    private void ConfigureInspectionTaskFromCommandLine(
        ShellStartupConfigurationPlan configuration)
    {
        if (configuration.InspectionTask is { } task)
        {
            _viewModel.SelectInspectionTask(task);
        }
    }

    private void ConfigureValidationSetFromCommandLine()
    {
        validationSetSmoke = ShellValidationSetSmoke.Configure(
            _commandLineArguments,
            _viewModel.Workbench,
            () =>
            {
                ToolWorkbench.IsBottomPaneExpanded = true;
                ToolWorkbench.ActivateValidationSet();
            },
            () => _viewModel.Workbench.IsValidationEvidenceExpanded = true,
            () => _viewModel.Workbench.IsValidationThresholdExpanded = true,
            ApplyValidationWorkspaceSectionForSmokeAsync,
            section => ToolWorkbench.ActiveValidationWorkspaceSection == section);
    }

    private async Task ApplyValidationWorkspaceSectionForSmokeAsync(
        ValidationWorkspaceSection section)
    {
        await Dispatcher.InvokeAsync(
            () => ToolWorkbench.SetValidationWorkspaceSection(section),
            DispatcherPriority.Loaded);
        await Dispatcher.InvokeAsync(
            () => { },
            DispatcherPriority.Render);
    }

    private void ConfigureWorkbenchBottomPaneFromCommandLine(
        ShellStartupConfigurationPlan configuration)
    {
        switch (configuration.BottomPane)
        {
            case ShellStartupBottomPane.FlowMap:
                ToolWorkbench.ActivateFlowMap();
                break;
            case ShellStartupBottomPane.Problems:
                ToolWorkbench.ActivateProblems();
                break;
            case ShellStartupBottomPane.RunRecord:
                ToolWorkbench.ActivateRunRecord();
                break;
            case ShellStartupBottomPane.ValidationSet:
                ToolWorkbench.ActivateValidationSet();
                break;
            case ShellStartupBottomPane.OutputCompare:
                ToolWorkbench.ActivateOutputComparePane();
                break;
            case ShellStartupBottomPane.DisplayedOutputs:
                ToolWorkbench.ActivateDisplayedOutputsPane();
                break;
            case ShellStartupBottomPane.SessionLog:
                ToolWorkbench.ActivateSessionLogPane();
                break;
            case ShellStartupBottomPane.Profile:
                ToolWorkbench.ActivateProfilePane();
                break;
            case ShellStartupBottomPane.FitDiagnostics:
                ToolWorkbench.ActivateFitDiagnosticsPane();
                break;
            case ShellStartupBottomPane.IntersectionEvidence:
                ToolWorkbench.ActivateIntersectionEvidencePane();
                break;
            case ShellStartupBottomPane.CorrespondenceEvidence:
                ToolWorkbench.ActivateCorrespondenceEvidencePane();
                break;
        }
    }

    private void ConfigureOutputCompareFromCommandLine(
        ShellStartupConfigurationPlan configuration)
    {
        _viewModel.Workbench.CompareSlotAArtifactId = configuration.CompareSlotAArtifactId;
        _viewModel.Workbench.CompareSlotBArtifactId = configuration.CompareSlotBArtifactId;
        _viewModel.Workbench.CompareSlotCArtifactId = configuration.CompareSlotCArtifactId;
    }

    private void ConfigureC3DSourceLoadProgressFromCommandLine(
        ShellStartupConfigurationPlan configuration)
    {
        if (configuration.C3DSourceLoadProgress is not { } progress)
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
        _toolLabWindows.ShowForTool("filter", showMissing: true);
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
        _toolLabWindows.ShowForTool(
            args.ToolId,
            showMissing: false,
            preserveSelectedStep: true);
    }

    private void OpenEdgeToolLabRequested(object? sender, EventArgs args)
    {
        _toolLabWindows.ShowForTool("height-difference-edge", showMissing: true);
    }

    private void OpenLineIntersectionToolLabRequested(object? sender, EventArgs args)
    {
        _toolLabWindows.ShowForTool("line-intersection", showMissing: true);
    }

    private void OpenTwoPointLineToolLabRequested(object? sender, EventArgs args)
    {
        _toolLabWindows.ShowForTool("two-point-line", showMissing: true);
    }

    private void OpenThreePointPlaneToolLabRequested(object? sender, EventArgs args)
    {
        _toolLabWindows.ShowForTool("three-point-plane", showMissing: true);
    }

    private void OpenDatumPlaneDeviationToolLabRequested(object? sender, EventArgs args)
    {
        _toolLabWindows.ShowForTool("datum-plane-raw-height-deviation", showMissing: true);
    }

    private void OpenLandmarkCorrespondenceToolLabRequested(object? sender, EventArgs args)
    {
        _toolLabWindows.ShowForTool("landmark-correspondence", showMissing: true);
    }

    private void OpenXYZAffineSolveToolLabRequested(object? sender, EventArgs args)
    {
        _toolLabWindows.ShowForTool("xyz-affine-solve", showMissing: true);
    }

    private void OpenXYZAffineApplyToolLabRequested(object? sender, EventArgs args)
    {
        _toolLabWindows.ShowForTool("xyz-affine-apply", showMissing: true);
    }

    private void OpenRegridHeightMapToolLabRequested(object? sender, EventArgs args)
    {
        _toolLabWindows.ShowForTool("re-grid-height-map", showMissing: true);
    }

    private void OnWorkbenchNewTeachingRecipeRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.NewTeachingRecipeRequested(sender, args);

    private void OnWorkbenchBrowseFirstRecipeFolderRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.BrowseFirstRecipeFolderRequested(sender, args);

    private void OnWorkbenchBrowseFirstRecipeSourceRequested(object? sender, EventArgs args) =>
        _workbenchLifecycle.BrowseFirstRecipeSourceRequested(sender, args);

    private Window GetRecipeLifecycleDialogOwner() => _workbenchLifecycle.GetRecipeLifecycleDialogOwner();

    private static string DialogText(string key, string korean, string english) =>
        ThreeDLocalization.Shared.Resolve(key, korean, english);

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
        if (_isClosed)
        {
            return;
        }

        CancelExpertViewerActivation();

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

            QueueExpertViewerActivation();
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

    private void QueueExpertViewerActivation()
    {
        if (_isClosed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (_expertViewerActivationOperation?.Status is DispatcherOperationStatus.Pending or DispatcherOperationStatus.Executing)
        {
            return;
        }

        try
        {
            _expertViewerActivationOperation = Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(ApplyExpertViewerActivation));
        }
        catch (InvalidOperationException)
        {
            _expertViewerActivationOperation = null;
        }
    }

    private void ApplyExpertViewerActivation()
    {
        _expertViewerActivationOperation = null;
        if (_isClosed)
        {
            return;
        }

        if (_viewModel.IsExpertWorkspaceSelected
            && ReferenceEquals(Workspace.ViewerContent, _viewer))
        {
            Workspace.UpdateLayout();
            _viewer.RequestVisibleFrame();
        }
    }

    private void CancelExpertViewerActivation()
    {
        var operation = _expertViewerActivationOperation;
        _expertViewerActivationOperation = null;
        if (operation?.Status == DispatcherOperationStatus.Pending)
        {
            operation.Abort();
        }
    }
}
