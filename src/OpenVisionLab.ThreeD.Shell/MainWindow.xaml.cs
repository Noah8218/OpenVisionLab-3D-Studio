extern alias OvlMessageDialogs;

using OpenVisionLab;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.Automation;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.Layout;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Recipe;
using OpenVisionLab.ThreeD.Shell.Views.Shell;
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
    private readonly ShellViewerHostCoordinator _viewerHost;
    private readonly WorkbenchViewerTeachingCoordinator _workbenchViewerTeaching;
    private readonly WorkbenchViewerDisplayCoordinator _workbenchViewerDisplay;
    private readonly ShellMessageDialogController _messageDialogs;
    private readonly ShellEvidenceDialogController _evidenceDialogs;
    private readonly RecipeFileDialogService _recipeFileDialogs;
    private readonly ShellSourceFileDialogService _sourceFileDialogs;
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
    private readonly ShellCalibrationStartupCoordinator _calibrationStartup;
    private readonly ShellSmokePublishCoordinator _shellSmokePublish;
    private readonly ShellToolSelectionSmoke _toolSelectionSmoke;
    private readonly ShellViewerPointerSmokeCoordinator _viewerPointerSmoke;
    private readonly ShellTeachingSmokeCoordinator _teachingSmoke;
    private readonly ShellCommandLineArguments _commandLineArguments;
    private readonly ShellNominalActualPreviewWaiter _nominalActualPreviewWaiter;
    private readonly ShellStartupConfigurationPlan _startupConfiguration;
    private readonly ShellStartupViewerProjectionCoordinator _startupViewerProjection;
    private readonly ShellStartupCoordinator _startupCoordinator;
    private readonly ShellSmokeScenarioRunner _shellSmokeScenario;
    private DispatcherTimer? _importDialogSmokeTimer;
    private bool _isClosed;
    private ShellValidationSetSmokeState validationSetSmoke =
        ShellValidationSetSmokeState.Empty;

    public MainWindow()
    {
        _commandLineArguments = ShellCommandLineArguments.Capture();
        _startupConfiguration = ShellStartupConfigurationPlanner.Parse(
            _commandLineArguments);
        OpenVisionLanguageService.Load();
        ShellStartupCoordinator.ApplyLanguage(
            _startupConfiguration,
            language => OpenVisionLanguageService.SetLanguage(language, save: false));
        OVLog.Write(LogCategory.System, LogLevel.Info, "OpenVisionLab 3D Studio starting.");
        _viewer = new OpenVisionThreeDViewerControl(
            loadDefaultSamples: !_startupConfiguration.ShouldStartWithEmptyRecipeInput);
        _nominalActualPreviewWaiter = new ShellNominalActualPreviewWaiter(
            () => _viewer.HostState.NominalActualState);
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
        _viewModel.SetIntegrationDialogHost(new ThreeDIntegrationDialogHost(() => this));
        _viewModel.SelectedEvidenceTabIndex = _startupConfiguration.EvidenceTabIndex;
        DataContext = _viewModel;
        _messageDialogs = new ShellMessageDialogController(
            GetRecipeLifecycleDialogOwner,
            _viewModel,
            DialogText);
        _toolLabWindows = new ToolLabWindowManager(
            this,
            _viewModel.Workbench,
            _viewModel.Workbench.SelectFirstPipelineStepForTool,
            _viewModel.Workbench.SelectPipelineStep,
            _messageDialogs.ShowMissingToolLabStep);
        _preparationPresetSmoke = new ShellPreparationPresetAssistantSmoke(
            _viewModel.Workbench,
            ToolWorkbench);
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
        _sourceFileDialogs = new ShellSourceFileDialogService(() => this);
        var sourceLoadCoordinator = new ShellWorkbenchSourceLoadCoordinator(
            _sourceFileDialogs,
            _viewer,
            _viewModel,
            new ShellWorkbenchSourceLoadCallbacks
            {
                ShowLoadSourceFailure = _messageDialogs.ShowLoadSourceFailure
            });
        _workbenchLifecycle = new ShellWorkbenchLifecycleController(
            this,
            _viewModel,
            _recipeFileDialogs,
            _sourceFileDialogs,
            _workbenchViewerTeaching,
            sourceLoadCoordinator,
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
                HideWorkbenchHudDetails = () => _viewer.TrySetHudDetailsVisible(false)
            });
        _calibrationStartup = new ShellCalibrationStartupCoordinator(
            new ShellCalibrationStartupCallbacks
            {
                SelectCalibrationWorkspace = () => _viewModel.IsCalibrationWorkspaceSelected = true,
                SelectRepeatabilitySection = () => _viewModel.Calibration.SelectedSection = CalibrationSection.Repeatability,
                LoadStudy = _viewModel.Calibration.LoadStudy,
                Calculate = () => _viewModel.Calibration.CalculateCommand.Execute(null)
            });
        _shellSmokePublish = new ShellSmokePublishCoordinator(
            new ShellSmokePublishCallbacks
            {
                PublishCurrentPreview = _viewer.PublishCurrentPreviewResult,
                ShowReviewWorkspace = _viewModel.ShowReviewWorkspace,
                SaveCurrentRecipe = path => _viewer.SaveCurrentRecipe(path, isSmoke: true)
            });
        _toolSelectionSmoke = new ShellToolSelectionSmoke(
            _viewModel.Workbench,
            ToolWorkbench,
            Dispatcher);
        _viewerPointerSmoke = new ShellViewerPointerSmokeCoordinator(
            new ShellViewerPointerSmokeCallbacks
            {
                ApplyConfiguredNextDensity = _viewer.ApplyConfiguredSmokeNextDensityAsync,
                ApplyConfiguredPick = _viewer.ApplyConfiguredSmokePick,
                RunConfiguredPointerInputRegression = _viewer.RunConfiguredPointerInputRegressionAsync,
                RunProfilePointerSmoke = _viewer.RunProfilePointerSmokeAsync,
                RunTeachingOrientedBoxPointerSmoke = _viewer.RunTeachingOrientedBoxPointerSmokeAsync,
                ViewerStatus = () => _viewer.HostState.ViewerStatus
            });
        _teachingSmoke = new ShellTeachingSmokeCoordinator(
            new ShellTeachingSmokeCallbacks
            {
                RunTeachingSelection = (mode, reportPath) =>
                    ShellTeachingSelectionSmoke.RunAsync(
                        _viewModel,
                        _viewer,
                        mode,
                        reportPath),
                RunPlaneFlatnessLiveA3 = (reportPath, savePath) =>
                    ShellPlaneFlatnessLiveA3Smoke.RunAsync(
                        _viewModel,
                        _viewer,
                        reportPath,
                        savePath,
                        _workbenchViewerTeaching.SyncAppliedSelections),
                SaveTeachingRecipe = path =>
                {
                    var succeeded = _viewModel.Workbench.TrySaveTeachingRecipe(
                        path,
                        out var message);
                    return (succeeded, message);
                }
            });
        if (Workspace.ProfileContent is Views.Workbench.HeightProfileView advancedHeightProfileView)
        {
            advancedHeightProfileView.ViewerHost = _viewer;
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
        TaskWorkspace.ViewerEditor = _viewer.Editor;
        _viewerHost = new ShellViewerHostCoordinator(
            Dispatcher,
            new ShellViewerHostCoordinatorCallbacks
            {
                GetWorkspaceMode = () => _viewModel.SelectedWorkspaceMode,
                IsTeachingCaptureActive = () => _viewer.TeachingCaptureSnapshot.IsActive,
                CancelTeachingCapture = _viewer.CancelC3DTeachingCapture,
                ShowWorkbenchViewer = ShowWorkbenchViewer,
                ShowExpertViewer = ShowExpertViewer,
                ShowTaskViewer = ShowTaskViewer,
                ClearViewerHosts = ClearViewerHosts,
                IsExpertViewerAttached = () =>
                    _viewModel.IsExpertWorkspaceSelected
                    && ReferenceEquals(Workspace.ViewerContent, _viewer),
                UpdateExpertViewerLayout = Workspace.UpdateLayout,
                RequestVisibleFrame = _viewer.RequestVisibleFrame,
                IsClosed = () => _isClosed
            });
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
                SetHeightColorMinimumRaw = value => _viewer.TrySetC3DHeightColorMinimumRaw(value),
                SetHeightColorMaximumRaw = value => _viewer.TrySetC3DHeightColorMaximumRaw(value)
            });
        _startupCoordinator = new ShellStartupCoordinator(
            new ShellStartupCoordinatorCallbacks
            {
                SelectWorkspace = workspace => _viewModel.SelectWorkspaceCommand.Execute(workspace),
                IsResultsWorkspaceSelected = () => _viewModel.IsResultsWorkspaceSelected,
                SelectResultsSection = section => ToolWorkbench.SetResultsWorkspaceSection(section),
                SelectInspectionTask = _viewModel.SelectInspectionTask,
                ActivateBottomPane = ActivateStartupBottomPane,
                SetOutputCompareSlots = (slotA, slotB, slotC) =>
                {
                    _viewModel.Workbench.CompareSlotAArtifactId = slotA;
                    _viewModel.Workbench.CompareSlotBArtifactId = slotB;
                    _viewModel.Workbench.CompareSlotCArtifactId = slotC;
                },
                ApplyC3DSourceLoadProgress = progress =>
                {
                    _viewModel.Workbench.BeginC3DSourceLoad("large-inspection.C3D");
                    _viewModel.Workbench.ReportC3DSourceLoadProgress(progress);
                },
                ConfigureValidationSet = commandLine =>
                    ShellValidationSetSmoke.Configure(
                        commandLine,
                        _viewModel.Workbench,
                        () =>
                        {
                            ToolWorkbench.IsBottomPaneExpanded = true;
                            ToolWorkbench.ActivateValidationSet();
                        },
                        () => _viewModel.Workbench.IsValidationEvidenceExpanded = true,
                        () => _viewModel.Workbench.IsValidationThresholdExpanded = true,
                        ApplyValidationWorkspaceSectionForSmokeAsync,
                        section => ToolWorkbench.ActiveValidationWorkspaceSection == section),
                TryLoadRunRecord = path =>
                {
                    var succeeded = _viewModel.LoadRunRecord(path, out var message);
                    return new ShellStartupRunRecordLoadResult(succeeded, message);
                },
                ReportRunRecordRestoreFailure = message => OVLog.Write(
                    LogCategory.UI,
                    LogLevel.Warning,
                    $"Startup Run Record could not be restored after recipe load: {message}"),
                ApplyCalibration = (studyPath, calculate) => _calibrationStartup.Apply(studyPath, calculate),
                ConfigureToolTeaching = request => _toolTeachingStartup.Configure(request),
                SetViewerSmokeFailure = _viewModel.SetViewerSmokeFailed,
                ApplyViewerProjection = _startupViewerProjection.Apply
            });
        _viewModelPropertyChangedHandler = OnShellViewModelPropertyChanged;
        _viewModel.PropertyChanged += _viewModelPropertyChangedHandler;
        _inspectionTaskChangedHandler = (_, _) => LoadSelectedInspectionTask();
        _viewModel.InspectionTaskChanged += _inspectionTaskChangedHandler;
        _viewerHost.Apply();
        _startupCoordinator.ApplyWorkspaceAndResults(_startupConfiguration);
        _startupCoordinator.ApplyInspectionTask(_startupConfiguration);
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
            _viewModel,
            new ShellRequestCallbacks
            {
                SubscribeProfileView = handler => _viewer.ProfileViewRequested += handler,
                UnsubscribeProfileView = handler => _viewer.ProfileViewRequested -= handler,
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

        _startupCoordinator.ApplyCalibration(_commandLineArguments);
        _startupCoordinator.ApplyToolTeaching(_commandLineArguments);
        _startupCoordinator.RestoreRunRecord(_commandLineArguments);
        if (!_startupConfiguration.IsAutomatedShellRun)
        {
            _workbenchLifecycle.RestoreMostRecentWorkbenchRecipe();
        }
        _startupCoordinator.ApplyOutputCompare(_startupConfiguration);
        validationSetSmoke = _startupCoordinator.ConfigureValidationSet(_commandLineArguments);
        _startupCoordinator.ApplyWorkbenchBottomPane(_startupConfiguration);
        _startupCoordinator.ApplyC3DSourceLoadProgress(_startupConfiguration);
        _workbenchViewerTeaching.SyncAppliedSelections();
        _studioLayout = new StudioLayoutController(
            this,
            ToolWorkbench,
            Workspace,
            _viewModel,
            _startupConfiguration.IsAutomatedShellRun,
            _commandLineArguments.GetValue("--smoke-layout-profile"),
            _commandLineArguments.GetValue("--smoke-layout-state-report"));
        Loaded += ApplyViewerProjectionOnLoaded;
        Loaded += EnsureWorkbenchViewerSourceConsistency;
        _shellSmokeScenario = new ShellSmokeScenarioRunner(
            new ShellSmokeScenarioContext
            {
                Window = this,
                Viewer = _viewer,
                ViewModel = _viewModel,
                Workbench = ToolWorkbench,
                CommandLineArguments = _commandLineArguments,
                StartupConfiguration = _startupConfiguration,
                WorkbenchLifecycle = _workbenchLifecycle,
                MessageDialogs = _messageDialogs,
                ToolLabWindows = _toolLabWindows,
                PreparationPresetSmoke = _preparationPresetSmoke,
                ValidationThresholdSmoke = _validationThresholdSmoke,
                SurfaceMatchInteractionSmoke = _surfaceMatchInteractionSmoke,
                ViewerWorkspaceSmoke = _viewerWorkspaceSmoke,
                CurrentRecipeRunSmoke = _currentRecipeRunSmoke,
                PreparationPreviewSmoke = _preparationPreviewSmoke,
                RecipeMeasurementSmoke = _recipeMeasurementSmoke,
                WorkbenchInteractionSmoke = _workbenchInteractionSmoke,
                ShellSmokePublish = _shellSmokePublish,
                ToolSelectionSmoke = _toolSelectionSmoke,
                ViewerPointerSmoke = _viewerPointerSmoke,
                TeachingSmoke = _teachingSmoke,
                NominalActualPreviewWaiter = _nominalActualPreviewWaiter,
                ValidationSetSmoke = validationSetSmoke,
                StartImportDialogSmokeTimer = StartImportDialogSmokeTimer,
                ConfigureResultsSectionFromCommandLine = _startupCoordinator.ApplyResultsSection,
                IsClosed = () => _isClosed,
                ShutdownApplication = exitCode => Application.Current.Shutdown(exitCode)
            });
        _shellSmokeScenario.Attach();
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
        _shellSmokeScenario.Dispose();
        _viewerHost.Dispose();
        StopImportDialogSmokeTimer();
        OVLog.Write(LogCategory.System, LogLevel.Info, "OpenVisionLab 3D Studio shutdown.");
        _viewer.HostStateChanged -= _viewerHostStateChangedHandler;
        _requestCoordinator.Dispose();
        _workbenchViewerDisplay.Dispose();
        _workbenchViewerTeaching.Dispose();
        _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        _viewModel.InspectionTaskChanged -= _inspectionTaskChangedHandler;
        Loaded -= EnsureWorkbenchViewerSourceConsistency;
        _studioLayout.Dispose();
        _workbenchLifecycle.Dispose();
        _toolLabWindows.Dispose();
        ToolWorkbench.Dispose();
        if (Workspace.ProfileContent is Views.Workbench.HeightProfileView advancedHeightProfileView)
        {
            advancedHeightProfileView.Dispose();
        }
        _viewer.Dispose();
        try
        {
            _viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            OVLog.Write(
                LogCategory.System,
                LogLevel.Error,
                $"Shell ViewModel shutdown failed: {exception.GetBaseException().Message}");
        }
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

    private void ActivateStartupBottomPane(ShellStartupBottomPane pane)
    {
        switch (pane)
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

    private void ApplyViewerProjectionOnLoaded(object sender, RoutedEventArgs args) =>
        _startupCoordinator.ApplyViewerProjection(_startupConfiguration);

    private void LoadSelectedInspectionTask()
    {
        var recipeFileName = _viewModel.SelectedInspectionTask == ShellInspectionTask.Warpage
            ? "c3d-warpage.recipe.json"
            : "c3d-thickness.recipe.json";
        _viewer.LoadInspectionTaskRecipe(recipeFileName);
    }

    private void OnCalibrationLoadStudyRequested(object? sender, EventArgs args)
    {
        if (_sourceFileDialogs.TrySelectRepeatabilityStudyPath(out var path))
        {
            _viewModel.Calibration.LoadStudy(path);
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

    private void OpenToolLabRequested(object? sender, StudioToolLabRequestEventArgs args) =>
        _toolLabWindows.ShowForTool(args.ToolId, showMissing: true);

    private void OnWorkbenchSelectValidationSetSourcesRequested(object? sender, EventArgs args)
    {
        if (_sourceFileDialogs.TrySelectValidationSetSources(out var paths))
        {
            _viewModel.Workbench.SetValidationSetSources(paths);
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
            _viewerHost.Apply();
        }
    }

    private void ShowWorkbenchViewer()
    {
        TaskWorkspace.ViewerContent = null;
        Workspace.ViewerContent = null;
        _viewer.TrySetHudDetailsVisible(false);
        if (!ReferenceEquals(ToolWorkbench.ViewerContent, _viewer))
        {
            ToolWorkbench.ViewerContent = _viewer;
        }
    }

    private void ShowExpertViewer()
    {
        ToolWorkbench.ReleaseMainViewer(_viewer);
        ToolWorkbench.ViewerContent = null;
        _viewer.TrySetHudDetailsVisible(true);
        TaskWorkspace.ViewerContent = null;
        Workspace.ReactivateViewerContent(_viewer);
    }

    private void ShowTaskViewer()
    {
        ToolWorkbench.ViewerContent = null;
        Workspace.ViewerContent = null;
        if (!ReferenceEquals(TaskWorkspace.ViewerContent, _viewer))
        {
            TaskWorkspace.ViewerContent = _viewer;
        }
    }

    private void ClearViewerHosts()
    {
        ToolWorkbench.ViewerContent = null;
        Workspace.ViewerContent = null;
        TaskWorkspace.ViewerContent = null;
    }
}
