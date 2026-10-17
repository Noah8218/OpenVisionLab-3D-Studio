using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.Services;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.ViewModels.Integration;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

public enum ShellWorkspaceMode
{
    Workbench,
    Teach,
    Inspect,
    Review,
    Calibrate,
    Expert,
    Exchange,
    Validate
}

public enum ShellInspectionTask
{
    Thickness,
    Warpage
}

public enum RunRecordComparisonState
{
    Unavailable,
    DirectlyComparable,
    ConditionsChanged,
    NotDirectlyComparable,
    Error
}

public sealed class ShellMainWindowViewModel : INotifyPropertyChanged, IDisposable, IAsyncDisposable
{
    private readonly RelayCommand selectWorkspaceCommand;
    private readonly RelayCommand openSelectedValidationIssueInTeachCommand;
    private bool c3DSampleVisible;
    private bool isStartPageSelected;
    private readonly string? comparisonContractPath;
    private readonly string? comparisonReportPath;
    private readonly string? shellScreenshotPath;
    private readonly string? runRecordPath;
    private readonly string? htmlReportPath;
    private readonly string? csvReportPath;
    private readonly ShellRunRecordPersistence runRecordPersistence;
    private readonly string? orderedRunRecordRoot;
    private readonly bool hasStartupEvidenceOverrides;
    private readonly ShellMainWindowEventCoordinator shellEventCoordinator;
    private readonly SynchronizationContext? uiSynchronizationContext;
    private bool startupEvidenceActive;
    private string? currentContractPath;
    private string? currentReportPath;
    private string? currentShellScreenshotPath;
    private string? currentRunRecordPath;
    private string? currentHtmlReportPath;
    private string? currentCsvReportPath;
    private InspectionRunRecord? currentResultsRecord;
    private ShellOrderedRunRecordArtifact? pendingOrderedRunArtifact;
    private ToolRecipeOrderedGraphExecutionResult? pendingOrderedRunExecution;
    private string statusText = "Viewer hosted";
    private string recipeComparisonSummary = "No recipe comparison evidence loaded.";
    private string recipeComparisonHistory = "(pending)";
    private string recipeComparisonDetails = "(pending)";
    private string runSnapshotSummary = "No run snapshot evidence loaded.";
    private string runSnapshotEvidence = "(pending)";
    private string inspectionStepSummary = "No inspection steps loaded.";
    private string resultsOperatorDecisionSummary =
        "Decision: NotRun\nPrimary measurement: Not recorded\nLimits: Not recorded\nReference-fit RMS: Not recorded\nReason: No inspection result is available for the current recipe.";
    private string resultsValueValiditySummary =
        "Definition: Unknown\nUnit: Unknown\nSource / ROI: Unknown\nValid samples: Unknown\nFit residual: Unknown\nInvalid reason: No inspection result is available for the current recipe.";
    private string thresholdCorrectionState = "Unavailable";
    private string thresholdCorrectionSummary =
        "No threshold-correction evidence was recorded.";
    private string sourceQualityState = "Unavailable";
    private string sourceQualitySummary =
        "No Source Quality evidence was recorded.";
    private string sourceQualityDetail =
        "No Source Quality evidence was recorded.";
    private RunRecordComparisonState runRecordComparisonState;
    private string runRecordComparisonSummary = string.Empty;
    private string runRecordComparisonDetails = string.Empty;
    private int selectedEvidenceTabIndex;
    private ShellWorkspaceMode selectedWorkspaceMode = ShellWorkspaceMode.Workbench;
    private ShellInspectionTask selectedInspectionTask = ShellInspectionTask.Thickness;
    private readonly IReadOnlyList<OpenVisionLanguageOption> languageOptions;
    private OpenVisionLanguageOption? selectedLanguageOption;
    private double lastLanguageChangeMilliseconds;
    private RunRecordRecentItem? selectedRecentRunRecord;
    private int disposalState;
    private readonly object disposalGate = new();
    private Task? disposalTask;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ApplyRoiAlignmentRequested;
    public event EventHandler? FitPlaneRequested;
    public event EventHandler? RefreshRecipeComparisonRequested;
    public event EventHandler? SaveRecipeRequested;
    public event EventHandler? PublishInspectionResultRequested;
    public event EventHandler? InspectionTaskChanged;
    public event EventHandler? OpenRunRecordRequested;
    public event EventHandler? ExportRunRecordRequested;
    public event EventHandler? ExportPrivacySafeSupportBundleRequested;
    public event EventHandler<EvidenceArtifactOpenRequestEventArgs>? OpenEvidenceArtifactRequested;

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public ShellMainWindowViewModel(
        string? comparisonContractPath = null,
        string? comparisonReportPath = null,
        string? shellScreenshotPath = null,
        string? runRecordPath = null,
        string? htmlReportPath = null,
        string? csvReportPath = null,
        string? recentRunRecordsPath = null,
        string? recentRecipesPath = null,
        string? orderedRunRecordRoot = null,
        string? integrationSettingsPath = null)
    {
        uiSynchronizationContext = SynchronizationContext.Current;
        this.comparisonContractPath = comparisonContractPath;
        this.comparisonReportPath = comparisonReportPath;
        this.shellScreenshotPath = shellScreenshotPath;
        this.runRecordPath = runRecordPath;
        this.htmlReportPath = htmlReportPath;
        this.csvReportPath = csvReportPath;
        this.orderedRunRecordRoot = orderedRunRecordRoot;
        hasStartupEvidenceOverrides =
            !string.IsNullOrWhiteSpace(comparisonContractPath)
            || !string.IsNullOrWhiteSpace(comparisonReportPath)
            || !string.IsNullOrWhiteSpace(shellScreenshotPath)
            || !string.IsNullOrWhiteSpace(runRecordPath)
            || !string.IsNullOrWhiteSpace(htmlReportPath)
            || !string.IsNullOrWhiteSpace(csvReportPath);
        var resolvedRecentRunRecordsPath = recentRunRecordsPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenVisionLab",
                "ThreeDStudio",
                "recent-run-records.json");
        runRecordPersistence = new ShellRunRecordPersistence(resolvedRecentRunRecordsPath);
        languageOptions = OpenVisionLanguageService.GetLanguageOptions();
        selectedLanguageOption = languageOptions.FirstOrDefault(option => option.Language == OpenVisionLanguageService.CurrentLanguage)
            ?? languageOptions[0];
        ResultsWorkspace = new ResultsWorkspaceViewModel();
        Workbench = new ToolWorkbenchViewModel(recentRecipesPath);
        IntegrationExchange = new ThreeDIntegrationViewModel(
            () => currentRunRecordPath,
            integrationSettingsPath);
        IntegrationExchange.PropertyChanged += OnIntegrationExchangePropertyChanged;
        Calibration = new CalibrationCenterViewModel();
        selectWorkspaceCommand = new RelayCommand(
            parameter => SelectWorkspace(parameter),
            CanSelectWorkspace);
        SelectWorkspaceCommand = selectWorkspaceCommand;
        openSelectedValidationIssueInTeachCommand = new RelayCommand(
            parameter => OpenSelectedValidationIssueInTeach(parameter),
            parameter => CanOpenSelectedValidationIssueInTeach(parameter));
        OpenSelectedValidationIssueInTeachCommand =
            openSelectedValidationIssueInTeachCommand;
        ApplyRoiAlignmentCommand = new RelayCommand(_ => ApplyRoiAlignmentRequested?.Invoke(this, EventArgs.Empty), _ => c3DSampleVisible);
        FitPlaneCommand = new RelayCommand(_ => FitPlaneRequested?.Invoke(this, EventArgs.Empty), _ => c3DSampleVisible);
        RefreshRecipeComparisonCommand = new RelayCommand(_ => RefreshRecipeComparisonRequested?.Invoke(this, EventArgs.Empty));
        SaveRecipeCommand = new RelayCommand(_ => SaveRecipeRequested?.Invoke(this, EventArgs.Empty));
        PublishInspectionResultCommand = new RelayCommand(_ => PublishInspectionResultRequested?.Invoke(this, EventArgs.Empty));
        OpenUiContractCommand = new RelayCommand(_ => RequestEvidenceArtifact("UI contract", currentContractPath), _ => !string.IsNullOrWhiteSpace(currentContractPath));
        OpenRunnerReportCommand = new RelayCommand(_ => RequestEvidenceArtifact("Runner report", currentReportPath), _ => !string.IsNullOrWhiteSpace(currentReportPath));
        OpenShellScreenshotCommand = new RelayCommand(_ => RequestEvidenceArtifact("Shell screenshot", currentShellScreenshotPath), _ => !string.IsNullOrWhiteSpace(currentShellScreenshotPath));
        OpenRunRecordCommand = new RelayCommand(_ => RequestEvidenceArtifact("Run JSON", currentRunRecordPath), _ => !string.IsNullOrWhiteSpace(currentRunRecordPath));
        OpenHtmlReportCommand = new RelayCommand(_ => RequestEvidenceArtifact("HTML report", currentHtmlReportPath), _ => !string.IsNullOrWhiteSpace(currentHtmlReportPath));
        OpenCsvReportCommand = new RelayCommand(_ => RequestEvidenceArtifact("CSV report", currentCsvReportPath), _ => !string.IsNullOrWhiteSpace(currentCsvReportPath));
        OpenRunRecordFolderCommand = new RelayCommand(
            _ => RequestEvidenceArtifact("Run folder", Path.GetDirectoryName(currentRunRecordPath)),
            _ => !string.IsNullOrWhiteSpace(currentRunRecordPath));
        SelectRunRecordCommand = new RelayCommand(_ => OpenRunRecordRequested?.Invoke(this, EventArgs.Empty));
        OpenRecentRunRecordCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is RunRecordRecentItem item)
                {
                    LoadRunRecord(item.Path, out _);
                }
            },
            parameter => parameter is RunRecordRecentItem { IsAvailable: true });
        CompareRecentRunRecordCommand = new RelayCommand(
            _ =>
            {
                CompareSelectedRunRecord(out var ignoredMessage);
            },
            _ => CanCompareSelectedRunRecord());
        ExportRunRecordCommand = new RelayCommand(
            _ => ExportRunRecordRequested?.Invoke(this, EventArgs.Empty),
            _ => !string.IsNullOrWhiteSpace(currentRunRecordPath));
        ExportPrivacySafeSupportBundleCommand = new RelayCommand(
            _ => ExportPrivacySafeSupportBundleRequested?.Invoke(this, EventArgs.Empty),
            _ => !string.IsNullOrWhiteSpace(currentRunRecordPath));
        shellEventCoordinator = CreateShellMainWindowEventCoordinator();
        LoadRecentRunRecords();
        if (hasStartupEvidenceOverrides)
        {
            startupEvidenceActive = true;
            RefreshRecipeComparison(runRecordPath, useStartupOverrides: true);
        }
        else
        {
            ClearCurrentRunEvidenceForRecipeContext();
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public ValueTask DisposeAsync()
    {
        Task cleanupTask;
        lock (disposalGate)
        {
            disposalTask ??= DisposeChildrenAsync();
            cleanupTask = disposalTask;
        }

        return new ValueTask(cleanupTask);
    }

    private async Task DisposeChildrenAsync()
    {
        Interlocked.Exchange(ref disposalState, 1);
        pendingOrderedRunArtifact = null;
        pendingOrderedRunExecution = null;
        try
        {
            await IntegrationExchange.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            IntegrationExchange.PropertyChanged -= OnIntegrationExchangePropertyChanged;
            shellEventCoordinator.Dispose();
            Calibration.Dispose();
            ResultsWorkspace.Dispose();
            Workbench.Dispose();
        }
    }

    private ShellMainWindowEventCoordinator CreateShellMainWindowEventCoordinator() =>
        new(
            InspectionSteps,
            Workbench,
            () => RaisePropertyChanged(nameof(ResultsOperatorAffectedStepsSummary)),
            args => OnWorkbenchNavigationStateChanged(this, args),
            args => OnWorkbenchOrderedRunCompleted(this, args),
            () => OnWorkbenchOrderedRunInvalidated(this, EventArgs.Empty),
            RefreshLocalizedPresentation,
            OpenValidationSetComparisonInTeach);

    private void OnIntegrationExchangePropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (
            nameof(ThreeDIntegrationViewModel.SelectedInputArtifactPath)
            or nameof(ThreeDIntegrationViewModel.SelectedInputArtifactId)
            or nameof(ThreeDIntegrationViewModel.SelectedInputUnit)
            or nameof(ThreeDIntegrationViewModel.SelectedInputFrameId)))
        {
            return;
        }

        if (uiSynchronizationContext is not null
            && !ReferenceEquals(SynchronizationContext.Current, uiSynchronizationContext))
        {
            uiSynchronizationContext.Post(_ => LoadSelectedIntegrationHeightImage(), null);
            return;
        }

        LoadSelectedIntegrationHeightImage();
    }

    private void LoadSelectedIntegrationHeightImage()
    {
        _ = LoadSelectedIntegrationHeightImageAsync();
    }

    private async Task LoadSelectedIntegrationHeightImageAsync()
    {
        try
        {
            var path = IntegrationExchange.SelectedInputArtifactPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                Workbench.HeightImageViewer.ClearSource();
                return;
            }

            await Workbench.HeightImageViewer.EnsureSourceAsync(
                path,
                IntegrationExchange.SelectedInputArtifactId,
                IntegrationExchange.SelectedInputUnit,
                IntegrationExchange.SelectedInputFrameId);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException)
        {
            if (!IsDisposed)
            {
                Workbench.AppendLog("Integration viewer", $"Height Image load failed: {exception.Message}");
            }
        }
    }

    public ICommand ApplyRoiAlignmentCommand { get; }
    public ICommand SelectWorkspaceCommand { get; }
    public ICommand OpenSelectedValidationIssueInTeachCommand { get; }
    public ICommand FitPlaneCommand { get; }
    public ICommand RefreshRecipeComparisonCommand { get; }
    public ICommand SaveRecipeCommand { get; }
    public ICommand PublishInspectionResultCommand { get; }
    public ICommand OpenUiContractCommand { get; }
    public ICommand OpenRunnerReportCommand { get; }
    public ICommand OpenShellScreenshotCommand { get; }
    public ICommand OpenRunRecordCommand { get; }
    public ICommand OpenHtmlReportCommand { get; }
    public ICommand OpenCsvReportCommand { get; }
    public ICommand OpenRunRecordFolderCommand { get; }
    public ICommand SelectRunRecordCommand { get; }
    public ICommand OpenRecentRunRecordCommand { get; }
    public ICommand CompareRecentRunRecordCommand { get; }
    public ICommand ExportRunRecordCommand { get; }
    public ICommand ExportPrivacySafeSupportBundleCommand { get; }
    public ToolWorkbenchViewModel Workbench { get; }
    public ResultsWorkspaceViewModel ResultsWorkspace { get; }
    public CalibrationCenterViewModel Calibration { get; }
    public ThreeDIntegrationViewModel IntegrationExchange { get; }

    internal void SetIntegrationDialogHost(IThreeDIntegrationDialogHost dialogHost) =>
        IntegrationExchange.SetDialogHost(dialogHost);
    public ThreeDLocalization Localization => ThreeDLocalization.Shared;
    public IReadOnlyList<OpenVisionLanguageOption> LanguageOptions => languageOptions;

    public OpenVisionLanguageOption? SelectedLanguageOption
    {
        get => selectedLanguageOption;
        set => SetSelectedLanguage(value, save: true);
    }

    public double LastLanguageChangeMilliseconds
    {
        get => lastLanguageChangeMilliseconds;
        private set => SetField(ref lastLanguageChangeMilliseconds, value);
    }

    internal void SetSelectedLanguageForVerification(OpenVisionLanguageOption value) =>
        SetSelectedLanguage(value, save: false);

    private void SetSelectedLanguage(OpenVisionLanguageOption? value, bool save)
    {
        if (value is null || !SetField(ref selectedLanguageOption, value))
        {
            return;
        }

        var started = Stopwatch.GetTimestamp();
        OpenVisionLanguageService.SetLanguage(value.Language, save);
        RefreshRecipeComparison();
        LastLanguageChangeMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        StatusText = L(
            $"언어 적용 완료 · {LastLanguageChangeMilliseconds:F0} ms",
            $"Language applied · {LastLanguageChangeMilliseconds:F0} ms");
    }

    private void RefreshLocalizedPresentation()
    {
        if (currentResultsRecord is not null)
        {
            ResultsOperatorDecisionSummary = FormatResultsOperatorDecisionSummary(currentResultsRecord);
        }

        string[] propertyNames =
        [
            nameof(WorkspaceSummary),
            nameof(StatusText),
            nameof(RecipeComparisonSummary),
            nameof(RecipeComparisonHistory),
            nameof(RecipeComparisonDetails),
            nameof(RunSnapshotSummary),
            nameof(RunSnapshotEvidence),
            nameof(RunRecordComparisonStateText),
            nameof(RunRecordComparisonSummary),
            nameof(RunRecordComparisonDetails),
            nameof(InspectionStepSummary),
            nameof(ResultsOperatorDecisionSummary),
            nameof(ResultsOperatorDecisionDisplaySummary),
            nameof(ResultsOperatorCompactDecision),
            nameof(ResultsValueValiditySummary),
            nameof(ResultsValueValidityDisplaySummary),
            nameof(ThresholdCorrectionStateText),
            nameof(SourceQualityStateText),
            nameof(InspectionSteps),
            nameof(RecipeRunHistory),
            nameof(InspectionStageNavigationStatus)
        ];

        foreach (var propertyName in propertyNames)
        {
            RaisePropertyChanged(propertyName);
        }
    }

    public ShellWorkspaceMode SelectedWorkspaceMode
    {
        get => selectedWorkspaceMode;
        private set
        {
            if (!SetField(ref selectedWorkspaceMode, value))
            {
                return;
            }

            Workbench.SetInspectionFileView(value == ShellWorkspaceMode.Inspect);
            Workbench.SetValidationSampleView(value == ShellWorkspaceMode.Validate);

            RaisePropertyChanged(nameof(IsInspectWorkspaceSelected));
            RaisePropertyChanged(nameof(IsTeachWorkspaceSelected));
            RaisePropertyChanged(nameof(IsAuthoringWorkspaceSelected));
            RaisePropertyChanged(nameof(IsReviewWorkspaceSelected));
            RaisePropertyChanged(nameof(IsSetupWorkspaceSelected));
            RaisePropertyChanged(nameof(IsValidateWorkspaceSelected));
            RaisePropertyChanged(nameof(IsResultsWorkspaceSelected));
            RaisePropertyChanged(nameof(IsInspectionWorkspaceSelected));
            RaisePropertyChanged(nameof(IsCalibrationWorkspaceSelected));
            RaisePropertyChanged(nameof(IsWorkbenchWorkspaceSelected));
            RaisePropertyChanged(nameof(IsExpertWorkspaceSelected));
            RaisePropertyChanged(nameof(IsIntegrationExchangeSelected));
            RaisePropertyChanged(nameof(IsTaskWorkspaceSelected));
            RaisePropertyChanged(nameof(WorkspaceSummary));
            RaisePropertyChanged(nameof(InspectionStageNavigationStatus));
            selectWorkspaceCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsSetupWorkspaceSelected
    {
        get => !IsStartPageSelected && SelectedWorkspaceMode == ShellWorkspaceMode.Workbench;
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Workbench);
            }
        }
    }

    public bool IsTeachWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Teach;
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Teach);
            }
        }
    }

    public bool IsAuthoringWorkspaceSelected => !IsStartPageSelected
        && (SelectedWorkspaceMode is ShellWorkspaceMode.Workbench or ShellWorkspaceMode.Teach);

    public bool IsWorkbenchWorkspaceSelected
    {
        get => !IsStartPageSelected && (SelectedWorkspaceMode is ShellWorkspaceMode.Workbench
            or ShellWorkspaceMode.Teach
            or ShellWorkspaceMode.Validate
            or ShellWorkspaceMode.Inspect
            or ShellWorkspaceMode.Review);
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Workbench);
            }
        }
    }

    public bool IsInspectionWorkspaceSelected => !IsStartPageSelected
        && (SelectedWorkspaceMode is ShellWorkspaceMode.Workbench
            or ShellWorkspaceMode.Teach
            or ShellWorkspaceMode.Validate
            or ShellWorkspaceMode.Inspect
            or ShellWorkspaceMode.Review);

    public bool IsStartPageSelected => isStartPageSelected;

    public bool IsNavigationRailVisible => !IsStartPageSelected;

    public bool IsRecipeActionsVisible => !IsStartPageSelected;

    public bool IsInspectWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Inspect;
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Inspect);
            }
        }
    }

    public bool IsValidateWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Validate;
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Validate);
            }
        }
    }

    public bool IsReviewWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Review;
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Review);
            }
        }
    }

    public bool IsResultsWorkspaceSelected
    {
        get => IsReviewWorkspaceSelected;
        set => IsReviewWorkspaceSelected = value;
    }

    public bool IsCalibrationWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Calibrate;
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Calibrate);
            }
        }
    }

    public bool IsExpertWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Expert;
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Expert);
            }
        }
    }

    public bool IsIntegrationExchangeSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Exchange;
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Exchange);
            }
        }
    }

    // The former Thickness/Warpage task page is retained only as a source-level
    // compatibility view. Product navigation always uses the generic tool recipe workbench.
    public bool IsTaskWorkspaceSelected => false;

    public ShellInspectionTask SelectedInspectionTask
    {
        get => selectedInspectionTask;
        private set
        {
            if (!SetField(ref selectedInspectionTask, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsThicknessTaskSelected));
            RaisePropertyChanged(nameof(IsWarpageTaskSelected));
            RaisePropertyChanged(nameof(CurrentInspectionTaskLabel));
            RaisePropertyChanged(nameof(WorkspaceSummary));
            InspectionTaskChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsThicknessTaskSelected
    {
        get => SelectedInspectionTask == ShellInspectionTask.Thickness;
        set
        {
            if (value)
            {
                SelectInspectionTask(ShellInspectionTask.Thickness);
            }
        }
    }

    public bool IsWarpageTaskSelected
    {
        get => SelectedInspectionTask == ShellInspectionTask.Warpage;
        set
        {
            if (value)
            {
                SelectInspectionTask(ShellInspectionTask.Warpage);
            }
        }
    }

    public string CurrentInspectionTaskLabel => SelectedInspectionTask == ShellInspectionTask.Warpage
        ? "Warpage"
        : "Thickness";

    public string WorkspaceSummary => SelectedWorkspaceMode switch
    {
        ShellWorkspaceMode.Workbench => L("검사 구성 | 도구와 검사 순서를 구성합니다", "Inspection Setup | Compose tools and inspection order"),
        ShellWorkspaceMode.Teach => L("티칭 | 데이터, 영역, 설정값과 검출 결과를 확인합니다", "Teach | Configure data, regions, parameters, and detection"),
        ShellWorkspaceMode.Validate => L("샘플의 기대 역할과 실행 결과를 비교하세요.", "Compare sample expectations with run results."),
        ShellWorkspaceMode.Inspect => L("파일을 추가하고 체크한 파일을 검사하세요.", "Add files, then inspect the checked files."),
        ShellWorkspaceMode.Review => L("결과 | 실행 기록과 출력 정보를 검토합니다", "Results | Review run records and output evidence"),
        ShellWorkspaceMode.Calibrate => L("교정 작업공간 | 오프라인 데이터셋", "Calibration workspace | Offline datasets"),
        ShellWorkspaceMode.Expert => L("고급 작업공간 | 전체 검사 레이아웃", "Expert workspace | Full inspection layout"),
        ShellWorkspaceMode.Exchange => L("Machine Studio 연동 | 검사 요청 검토와 결과 전송 준비", "Machine Studio exchange | Explicit handoff review and result publishing"),
        _ => L("검사 작업공간", "Inspection workspace")
    };

    public string InspectionStageNavigationStatus =>
        Workbench.IsSelectionCandidateActive
            ? L("ROI 검토를 적용하거나 취소한 후 화면을 이동하세요.", "Apply or cancel the ROI review before changing stages.")
            : Workbench.HasPendingStepParameterChanges
                ? L("편집한 설정값을 적용하거나 변경을 취소한 후 화면을 이동하세요.", "Apply or discard the parameter draft before changing stages.")
                : Workbench.IsOrderedRunRunning
                    ? L("현재 레시피 실행이 끝난 후 화면을 이동하세요.", "Wait for the current recipe Run to finish before changing stages.")
                : Workbench.IsSelectedStepPreviewRunning
                    ? L("미리보기가 끝나거나 취소된 후 화면을 이동하세요.", "Wait for Preview to finish or cancel it before changing stages.")
                    : Workbench.IsValidationSetRunning
                        ? L("검증 실행이 끝나거나 취소된 후 화면을 이동하세요.", "Wait for validation to finish or cancel it before changing stages.")
                        : WorkspaceSummary;

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public void ReportLayoutStatus(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusText = message;
        }
    }

    public string RecipeComparisonSummary
    {
        get => recipeComparisonSummary;
        private set => SetField(ref recipeComparisonSummary, value);
    }

    public string RecipeComparisonHistory
    {
        get => recipeComparisonHistory;
        private set => SetField(ref recipeComparisonHistory, value);
    }

    public string RecipeComparisonDetails
    {
        get => recipeComparisonDetails;
        private set => SetField(ref recipeComparisonDetails, value);
    }

    public string RunSnapshotSummary
    {
        get => runSnapshotSummary;
        private set => SetField(ref runSnapshotSummary, value);
    }

    public string RunSnapshotEvidence
    {
        get => runSnapshotEvidence;
        private set => SetField(ref runSnapshotEvidence, value);
    }

    public string InspectionStepSummary
    {
        get => inspectionStepSummary;
        private set => SetField(ref inspectionStepSummary, value);
    }

    public string ResultsOperatorDecisionSummary
    {
        get => resultsOperatorDecisionSummary;
        private set
        {
            if (SetField(ref resultsOperatorDecisionSummary, value))
            {
                RaisePropertyChanged(nameof(ResultsOperatorDecisionDisplaySummary));
                RaisePropertyChanged(nameof(ResultsOperatorCompactDecision));
            }
        }
    }

    public string ResultsOperatorDecisionDisplaySummary => LocalizeResultSummaryForDisplay(ResultsOperatorDecisionSummary);

    public string ResultsOperatorCompactDecision => ResultsOperatorDecisionDisplaySummary.Split('\n')[0].TrimEnd('\r');

    public string ResultsOperatorAffectedStepsSummary
    {
        get
        {
            if (Workbench.SelectedValidationSetStep is { } selected)
            {
                return $"{selected.ToolName} \u00b7 {selected.StatusText}";
            }

            return InspectionSteps.Count == 0
                ? L("실행 기록 없음", "No run record")
                : string.Join(
                    ", ",
                    InspectionSteps.Take(3).Select(step =>
                        $"{step.Stage} \u00b7 {step.Status}"));
        }
    }

    public RunRecordComparisonState RunRecordComparisonStatus
    {
        get => runRecordComparisonState;
        private set
        {
            if (SetField(ref runRecordComparisonState, value))
            {
                RaisePropertyChanged(nameof(HasRunRecordComparison));
                RaisePropertyChanged(nameof(RunRecordComparisonStateText));
            }
        }
    }

    public bool HasRunRecordComparison =>
        RunRecordComparisonStatus != RunRecordComparisonState.Unavailable;

    public string RunRecordComparisonStateText => RunRecordComparisonStatus switch
    {
        RunRecordComparisonState.DirectlyComparable => L("직접 비교 가능", "Directly comparable"),
        RunRecordComparisonState.ConditionsChanged => L("검사 조건 변경", "Inspection conditions changed"),
        RunRecordComparisonState.NotDirectlyComparable => L("직접 비교 불가", "Not directly comparable"),
        RunRecordComparisonState.Error => L("비교 실패", "Comparison failed"),
        _ => L("비교 대기", "Comparison pending")
    };

    public string RunRecordComparisonSummary
    {
        get => runRecordComparisonSummary;
        private set => SetField(ref runRecordComparisonSummary, value);
    }

    public string RunRecordComparisonDetails
    {
        get => runRecordComparisonDetails;
        private set => SetField(ref runRecordComparisonDetails, value);
    }

    public string ResultsValueValiditySummary
    {
        get => resultsValueValiditySummary;
        private set
        {
            if (SetField(ref resultsValueValiditySummary, value))
            {
                RaisePropertyChanged(nameof(ResultsValueValidityDisplaySummary));
            }
        }
    }

    public string ResultsValueValidityDisplaySummary => LocalizeResultSummaryForDisplay(ResultsValueValiditySummary);

    public string ThresholdCorrectionState
    {
        get => thresholdCorrectionState;
        private set
        {
            if (SetField(ref thresholdCorrectionState, value))
            {
                RaisePropertyChanged(nameof(ThresholdCorrectionStateText));
            }
        }
    }

    public string ThresholdCorrectionStateText => LocalizeStateForDisplay(ThresholdCorrectionState);

    public string ThresholdCorrectionSummary
    {
        get => thresholdCorrectionSummary;
        private set => SetField(ref thresholdCorrectionSummary, value);
    }

    public int SelectedEvidenceTabIndex
    {
        get => selectedEvidenceTabIndex;
        set => SetField(ref selectedEvidenceTabIndex, Math.Clamp(value, 0, 4));
    }

    public ObservableCollection<RecipeRunHistoryItem> RecipeRunHistory { get; } = [];

    public ObservableCollection<InspectionStepItem> InspectionSteps { get; } = [];

    public ObservableCollection<InspectionThresholdCorrectionItem>
        ThresholdCorrectionItems { get; } = [];

    public ObservableCollection<RunRecordRecentItem> RecentRunRecords { get; } = [];

    public RunRecordRecentItem? SelectedRecentRunRecord
    {
        get => selectedRecentRunRecord;
        set
        {
            if (!SetField(ref selectedRecentRunRecord, value))
            {
                return;
            }

            ResetRunRecordComparison();
            ((RelayCommand)CompareRecentRunRecordCommand).RaiseCanExecuteChanged();
        }
    }

    internal void ShowStartPage()
    {
        if (isStartPageSelected)
        {
            return;
        }

        isStartPageSelected = true;
        RaisePropertyChanged(nameof(IsStartPageSelected));
        RaisePropertyChanged(nameof(IsNavigationRailVisible));
        RaisePropertyChanged(nameof(IsRecipeActionsVisible));
        RaisePropertyChanged(nameof(IsSetupWorkspaceSelected));
        RaisePropertyChanged(nameof(IsAuthoringWorkspaceSelected));
        RaisePropertyChanged(nameof(IsWorkbenchWorkspaceSelected));
        RaisePropertyChanged(nameof(IsInspectionWorkspaceSelected));
        selectWorkspaceCommand.RaiseCanExecuteChanged();
    }

    private void HideStartPage()
    {
        if (!isStartPageSelected)
        {
            return;
        }

        isStartPageSelected = false;
        RaisePropertyChanged(nameof(IsStartPageSelected));
        RaisePropertyChanged(nameof(IsNavigationRailVisible));
        RaisePropertyChanged(nameof(IsRecipeActionsVisible));
        RaisePropertyChanged(nameof(IsSetupWorkspaceSelected));
        RaisePropertyChanged(nameof(IsAuthoringWorkspaceSelected));
        RaisePropertyChanged(nameof(IsWorkbenchWorkspaceSelected));
        RaisePropertyChanged(nameof(IsInspectionWorkspaceSelected));
        selectWorkspaceCommand.RaiseCanExecuteChanged();
    }

    public void ShowReviewWorkspace() => TrySelectWorkspace(ShellWorkspaceMode.Review);

    public void SelectInspectionTask(ShellInspectionTask task)
    {
        if (Enum.IsDefined(typeof(ShellInspectionTask), task))
        {
            SelectedInspectionTask = task;
        }
    }

    private void SelectWorkspace(object? parameter)
    {
        if (parameter is ShellWorkspaceMode mode
            && Enum.IsDefined(typeof(ShellWorkspaceMode), mode))
        {
            TrySelectWorkspace(mode);
        }
    }

    private bool CanSelectWorkspace(object? parameter)
    {
        if (parameter is not ShellWorkspaceMode mode
            || !Enum.IsDefined(typeof(ShellWorkspaceMode), mode))
        {
            return false;
        }

        return mode == SelectedWorkspaceMode
            || !IsInspectionWorkspaceSelected
            || (!Workbench.IsSelectionCandidateActive
                && !Workbench.HasPendingStepParameterChanges
                && !Workbench.IsOrderedRunRunning
                && !Workbench.IsSelectedStepPreviewRunning
                && !Workbench.IsValidationSetRunning);
    }

    private void TrySelectWorkspace(ShellWorkspaceMode mode)
    {
        if (!CanSelectWorkspace(mode))
        {
            StatusText = InspectionStageNavigationStatus;
            return;
        }

        HideStartPage();
        SelectedWorkspaceMode = mode;
        if (mode == ShellWorkspaceMode.Exchange)
        {
            IntegrationExchange.SyncRunRecord();
        }
    }

    private void OpenValidationSetComparisonInTeach()
    {
        if (!CanSelectWorkspace(ShellWorkspaceMode.Teach))
        {
            StatusText = InspectionStageNavigationStatus;
            return;
        }

        TrySelectWorkspace(ShellWorkspaceMode.Teach);
        if (!Workbench.IsOutputCompareViewerActive && Workbench.ShowOutputCompareViewerCommand.CanExecute(null)) Workbench.ShowOutputCompareViewerCommand.Execute(null);
    }

    private bool CanOpenSelectedValidationIssueInTeach(object? parameter = null) =>
        (parameter is ValidationSetSampleRow sample ? sample.MismatchStep : Workbench.SelectedValidationSetStep) is { StepId.Length: > 0 } selected
        && Workbench.PipelineSteps.Any(
            step => string.Equals(
                step.Id,
                selected.StepId,
                StringComparison.OrdinalIgnoreCase))
        && (parameter is not ValidationSetSampleRow row || Workbench.SelectValidationMismatchSampleCommand.CanExecute(row))
        && CanSelectWorkspace(ShellWorkspaceMode.Teach);

    private void OpenSelectedValidationIssueInTeach(object? parameter = null)
    {
        if (parameter is ValidationSetSampleRow sample)
        {
            if (!CanOpenSelectedValidationIssueInTeach(sample)) return;
            Workbench.SelectValidationMismatchSampleCommand.Execute(sample);
        }
        if (Workbench.SelectedValidationSetStep is not { StepId.Length: > 0 } selected
            || !CanSelectWorkspace(ShellWorkspaceMode.Teach))
        {
            StatusText = InspectionStageNavigationStatus;
            return;
        }

        if (!Workbench.SelectPipelineStep(selected.StepId))
        {
            StatusText = L(
                $"\uC120\uD0DD\uD55C \uC2E4\uD328 \uB2E8\uACC4 '{selected.StepId}'\uB97C \uD604\uC7AC \uB808\uC2DC\uD53C\uC5D0\uC11C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.",
                $"The selected failed step '{selected.StepId}' is not available in the current recipe.");
            return;
        }

        if (!Workbench.BeginValidationFailureCorrectionContext())
        {
            StatusText = L(
                "\uC120\uD0DD\uD55C \uC2E4\uD328 \uC0D8\uD50C\uC758 \uD2F0\uCE6D \uCEE8\uD14D\uC2A4\uD2B8\uB97C \uC900\uBE44\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.",
                "The selected failure could not be prepared for correction in Teach.");
            return;
        }

        TrySelectWorkspace(ShellWorkspaceMode.Teach);
        StatusText = L(
            $"\uD2F0\uCE6D\uC5D0\uC11C '{selected.StepId}' \uB2E8\uACC4\uB97C \uC5F4\uC5C8\uC2B5\uB2C8\uB2E4. \uB808\uC2DC\uD53C\uB294 \uBCC0\uACBD\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.",
            $"Opened step '{selected.StepId}' in Teach. The recipe was not changed.");
    }

    private void OnWorkbenchNavigationStateChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ToolWorkbenchViewModel.IsSelectionCandidateActive)
            or nameof(ToolWorkbenchViewModel.HasPendingStepParameterChanges)
            or nameof(ToolWorkbenchViewModel.IsOrderedRunRunning)
            or nameof(ToolWorkbenchViewModel.IsSelectedStepPreviewRunning)
            or nameof(ToolWorkbenchViewModel.IsValidationSetRunning)
            or nameof(ToolWorkbenchViewModel.SelectedValidationSetStep)
            or nameof(ToolWorkbenchViewModel.SelectedPipelineStep))
        {
            RaisePropertyChanged(nameof(InspectionStageNavigationStatus));
            selectWorkspaceCommand.RaiseCanExecuteChanged();
            openSelectedValidationIssueInTeachCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(ResultsOperatorAffectedStepsSummary));
        }
    }

    public string SourceQualityState
    {
        get => sourceQualityState;
        private set
        {
            if (SetField(ref sourceQualityState, value))
            {
                RaisePropertyChanged(nameof(SourceQualityStateText));
            }
        }
    }

    public string SourceQualityStateText => LocalizeStateForDisplay(SourceQualityState);

    public string SourceQualitySummary
    {
        get => sourceQualitySummary;
        private set => SetField(ref sourceQualitySummary, value);
    }

    public string SourceQualityDetail
    {
        get => sourceQualityDetail;
        private set => SetField(ref sourceQualityDetail, value);
    }

    private void OnWorkbenchOrderedRunCompleted(
        object? sender,
        ToolWorkbenchOrderedRunCompletedEventArgs args)
    {
        ShellOrderedRunRecordArtifact artifact;
        try
        {
            if (!ReferenceEquals(pendingOrderedRunExecution, args.Execution))
            {
                pendingOrderedRunArtifact = ShellOrderedRunRecordWriter.Prepare(
                    args.RecipePath,
                    args.Document,
                    args.SourcePath,
                    args.Execution,
                    orderedRunRecordRoot,
                    args.RecordIdentity);
                pendingOrderedRunExecution = args.Execution;
            }
            artifact = pendingOrderedRunArtifact!;
            ShellOrderedRunRecordWriter.Save(artifact, args.Execution);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException
            or JsonException)
        {
            Workbench.ReportOrderedRunRecordFailure(exception.Message, pendingOrderedRunArtifact?.Record);
            currentResultsRecord = null;
            ResultsWorkspace.SetOpenedRecord(null);
            StatusText = L(
                $"현재 레시피 실행은 완료되었지만 검사 기록 저장에 실패했습니다: {exception.Message}",
                $"The current recipe Run completed, but its Run Record could not be saved: {exception.Message}");
            RunSnapshotSummary = L(
                $"실행 완료 · 상태를 확정할 검사 기록 저장 실패: {exception.Message}",
                $"Run completed · Run Record save failed before the result could be confirmed: {exception.Message}");
            RunSnapshotEvidence = L(
                "검사 기록이 생성되지 않았습니다. 정상으로 판정하지 마세요.",
                "No Run Record was created. Do not interpret the result as Pass.");
            InspectionStepSummary = L(
                "실행은 완료되었지만 검사 기록 저장 실패로 판정 정보를 확정할 수 없습니다.",
                "Execution completed, but Run Record save failure prevents confirming the decision evidence.");
            ResultsOperatorDecisionSummary = FormatResultsOperatorDecisionSummary(
                null,
                L(
                    $"검사 기록 저장 실패: {exception.Message}",
                    $"Run Record save failed: {exception.Message}"),
                ResultStatus.Error);
            InspectionSteps.Clear();
            InspectionSteps.Add(new InspectionStepItem(
                "1",
                "Run Record",
                ResultStatus.Error.ToString(),
                exception.Message));
            RefreshSourceQuality(null);
            RefreshThresholdCorrection(null);
            RefreshResultsValueValiditySummary(
                null,
                L(
                    $"검사 기록 저장 실패: {exception.Message}",
                    $"Run Record save failed: {exception.Message}"),
                ResultStatus.Error);
            RaisePropertyChanged(nameof(ResultsOperatorAffectedStepsSummary));
            return;
        }

        pendingOrderedRunArtifact = null;
        pendingOrderedRunExecution = null;
        Workbench.AttachOrderedRunRecord(artifact.JsonPath, artifact.Record);
        startupEvidenceActive = false;
        RefreshRecipeComparison(artifact.JsonPath, useStartupOverrides: false);
    }

    private void OnWorkbenchOrderedRunInvalidated(object? sender, EventArgs args) =>
        ClearCurrentRunEvidenceForRecipeContext();

    public void SetViewerSmokeFailed(string viewerStatus)
    {
        var root = ShellEvidencePathResolver.ResolveWorkspaceRoot();
        currentContractPath = null;
        currentReportPath = null;
        currentShellScreenshotPath = ShellEvidencePathResolver.ResolveOptionalPath(root, shellScreenshotPath);
        currentRunRecordPath = ShellEvidencePathResolver.ResolveOptionalPath(root, runRecordPath);
        currentHtmlReportPath = ShellEvidencePathResolver.ResolveOptionalPath(root, htmlReportPath);
        currentCsvReportPath = ShellEvidencePathResolver.ResolveOptionalPath(root, csvReportPath);
        currentResultsRecord = null;
        ResultsWorkspace.SetOpenedRecord(null);
        RefreshCommandCanExecute();

        StatusText = "Viewer hosted | viewer smoke failed";
        RecipeComparisonSummary = string.IsNullOrWhiteSpace(viewerStatus)
            ? "Viewer smoke failed before recipe comparison."
            : $"Viewer smoke failed before recipe comparison.\n{viewerStatus}";
        RecipeComparisonHistory = "No recipe comparison was run for this failed viewer smoke.";
        RecipeComparisonDetails = "See Tool / Inspector and Viewer contract output for the loader failure details.";
        RunSnapshotSummary = "Viewer smoke failed | Status: ViewerFailed | Key metric: No recipe metric | Evidence: Blocked";
        RunSnapshotEvidence = $"Shell: {ShellEvidenceTextParser.FormatScreenshotTarget(root, shellScreenshotPath)} | Runner: not created | UI: viewer smoke output";
        InspectionStepSummary = "Viewer smoke: Failed";
        ResultsOperatorDecisionSummary = FormatResultsOperatorDecisionSummary(
            null,
            viewerStatus,
            ResultStatus.Error);
        RefreshSourceQuality(null);
        RefreshThresholdCorrection(null);
        RefreshResultsValueValiditySummary(null, viewerStatus);
        InspectionSteps.Clear();
        InspectionSteps.Add(new InspectionStepItem("1", "Viewer smoke", "Failed", string.IsNullOrWhiteSpace(viewerStatus) ? "Viewer smoke failed before recipe comparison." : viewerStatus));
        RecipeRunHistory.Clear();
        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            "ViewerFailed",
            "No recipe metric",
            "Blocked",
            "(viewer smoke failure)"));
    }

    public void RefreshRecipeComparison()
    {
        if (string.IsNullOrWhiteSpace(currentRunRecordPath))
        {
            if (startupEvidenceActive)
            {
                RefreshRecipeComparison(
                    selectedRunRecordPath: null,
                    useStartupOverrides: true);
            }
            else
            {
                ClearCurrentRunEvidenceForRecipeContext();
            }

            return;
        }

        RefreshRecipeComparison(currentRunRecordPath, useStartupOverrides: false);
    }

    public void ClearCurrentRunEvidenceForRecipeContext()
    {
        startupEvidenceActive = false;
        currentContractPath = null;
        currentReportPath = null;
        currentShellScreenshotPath = null;
        currentRunRecordPath = null;
        currentHtmlReportPath = null;
        currentCsvReportPath = null;
        currentResultsRecord = null;
        ResultsWorkspace.SetOpenedRecord(null);
        SelectedRecentRunRecord = null;
        ResetRunRecordComparison();
        RefreshCommandCanExecute();

        RecipeComparisonSummary = L(
            "현재 레시피의 실행 결과가 없습니다.",
            "No run result is available for the current recipe and source.");
        RecipeComparisonHistory = L(
            "현재 레시피를 실행하거나 호환되는 최근 실행 결과를 직접 여세요.",
            "Run the current recipe or explicitly open a compatible recent result.");
        RecipeComparisonDetails = L(
            "최근 실행 결과는 유지되지만 자동으로 선택되지 않습니다.",
            "Recent run results remain available but are not selected automatically.");
        RunSnapshotSummary = L(
            "현재 레시피의 실행 기록이 없습니다.",
            "No current recipe run snapshot is loaded.");
        RunSnapshotEvidence = L(
            "레시피를 실행하거나 기록을 직접 열기 전에는 현재 레시피·입력 데이터 결과를 확인할 수 없습니다.",
            "Run the recipe or explicitly open a record to view the current recipe/source result.");
        InspectionStepSummary = L(
            "현재 레시피의 검사 결과가 없습니다.",
            "No inspection result is available for the current recipe.");
        ResultsOperatorDecisionSummary = FormatResultsOperatorDecisionSummary(null);
        RefreshResultsValueValiditySummary(null);
        InspectionSteps.Clear();
        RecipeRunHistory.Clear();
        RefreshSourceQuality(null);
        RefreshThresholdCorrection(null);
        StatusText = L(
            "현재 레시피 결과 없음",
            "Current recipe result unavailable");
    }

    private void RefreshRecipeComparison(string? selectedRunRecordPath, bool useStartupOverrides)
    {
        var root = ShellEvidencePathResolver.ResolveWorkspaceRoot();
        currentRunRecordPath = ShellEvidencePathResolver.ResolveOptionalPath(root, selectedRunRecordPath);
        ResetRunRecordComparison();
        var runRecord = runRecordPersistence.Read(currentRunRecordPath);
        currentResultsRecord = runRecord;
        ResultsWorkspace.SetOpenedRecord(runRecord);
        var contractPath = ShellEvidencePathResolver.ResolvePath(
            root,
            (useStartupOverrides ? comparisonContractPath : null) ?? runRecord?.Artifacts.ViewerContract,
            Path.Combine(root, "artifacts", "shell_recipe_ui_after.txt"));
        var reportPath = ShellEvidencePathResolver.ResolvePath(
            root,
            (useStartupOverrides ? comparisonReportPath : null) ?? runRecord?.Artifacts.RunnerTextReport,
            Path.Combine(root, "artifacts", "runner_shell_recipe_ui_compare_after.txt"));
        currentContractPath = contractPath;
        currentReportPath = reportPath;
        currentShellScreenshotPath = ShellEvidencePathResolver.ResolveOptionalPath(root, (useStartupOverrides ? shellScreenshotPath : null) ?? runRecord?.Artifacts.ViewerScreenshot);
        currentHtmlReportPath = ShellEvidencePathResolver.ResolveOptionalPath(root, (useStartupOverrides ? htmlReportPath : null) ?? runRecord?.Artifacts.HtmlReport);
        currentCsvReportPath = ShellEvidencePathResolver.ResolveOptionalPath(root, (useStartupOverrides ? csvReportPath : null) ?? runRecord?.Artifacts.CsvReport);
        RefreshCommandCanExecute();

        if (runRecord?.ViewerRunnerMatchState
            == ShellOrderedRunRecordWriter.EvidenceState)
        {
            RefreshShellOrderedRunRecord(root, runRecord);
            return;
        }

        var contractLines = ReadLinesOrEmpty(contractPath);
        var reportLines = ReadLinesOrEmpty(reportPath);
        var recipePath = ShellEvidenceTextParser.ExtractRecipePath(root, reportLines);
        var uiEvidence = ShellEvidenceTextParser.ExtractUiEvidence(contractLines);
        var runnerEvidence = ShellEvidenceTextParser.ExtractRunnerEvidence(reportLines);
        var comparisonState = uiEvidence.Matches(runnerEvidence)
            ? "Runner/UI contract matched"
            : "Comparison evidence missing or different";

        RecipeComparisonSummary =
            $"{comparisonState}\nUI: {uiEvidence.ToolName} / {uiEvidence.Status}\nRunner: {runnerEvidence.ToolName} / {runnerEvidence.Status}\nUI metric: {uiEvidence.KeyMetricSummary}\nRunner metric: {runnerEvidence.KeyMetricSummary}";
        RecipeComparisonHistory =
            $"Recipe: {ShellEvidenceTextParser.FormatEvidencePath(root, recipePath)}\nUI contract: {ShellEvidenceTextParser.FormatEvidencePath(root, contractPath)}\nRunner report: {ShellEvidenceTextParser.FormatEvidencePath(root, reportPath)}";
        RecipeComparisonDetails =
            $"{ShellEvidenceTextParser.PreviewLines(root, "Runner report", reportPath, reportLines)}\n\n{ShellEvidenceTextParser.PreviewLines(root, "UI contract", contractPath, contractLines)}";
        RefreshRunSnapshot(root, recipePath, contractPath, reportPath, uiEvidence, runnerEvidence, comparisonState);
        RefreshInspectionSteps(root, recipePath, contractPath, reportPath, contractLines, reportLines, uiEvidence, runnerEvidence, comparisonState, runRecord);
        RefreshRunHistory(root, contractPath, reportPath, uiEvidence, runnerEvidence, comparisonState);
        if (runRecord is not null && currentRunRecordPath is not null)
        {
            RecordRecentRunRecord(currentRunRecordPath, runRecord);
        }
        StatusText = comparisonState == "Runner/UI contract matched"
            ? "Viewer hosted | recipe comparison matched"
            : "Viewer hosted | recipe comparison pending";
    }

    private void RefreshShellOrderedRunRecord(
        string root,
        InspectionRunRecord record)
    {
        var steps = record.Steps ?? [];
        var metric = steps.SelectMany(step => step.Metrics).FirstOrDefault();
        var metricSummary = metric is null
            ? L("유한 측정값 없음", "No finite metric")
            : $"{metric.Name} {metric.Value:0.##} {metric.Unit}".TrimEnd();
        RecipeComparisonSummary = L(
            $"레시피 순차 검사 · {record.Status} · {steps.Count}개 단계\n레시피에 연결된 순서대로 실행했으며 미리보기와 결과 적용 상태는 변경하지 않았습니다.",
            $"Explicit Studio ordered Run · {record.Status} · {steps.Count} step(s)\nExecuted by the same ordered graph engine without changing Preview or Publish state.");
        RecipeComparisonHistory = L(
            $"레시피: {ShellEvidenceTextParser.FormatEvidencePath(root, record.Recipe.Path)}\n입력 데이터: {ShellEvidenceTextParser.FormatEvidencePath(root, record.Source.Path)}\n검사 기록: {ShellEvidenceTextParser.FormatEvidencePath(root, currentRunRecordPath ?? string.Empty)}",
            $"Recipe: {ShellEvidenceTextParser.FormatEvidencePath(root, record.Recipe.Path)}\nSource: {ShellEvidenceTextParser.FormatEvidencePath(root, record.Source.Path)}\nRun Record: {ShellEvidenceTextParser.FormatEvidencePath(root, currentRunRecordPath ?? string.Empty)}");
        RecipeComparisonDetails = string.Join(
            Environment.NewLine,
            steps.Select(step =>
                $"{step.RecipeIndex + 1:D2} | {step.Id} | {step.ToolName} | {step.Status} | {step.OutputEntityId} | SHA-256 {step.OutputContentSha256 ?? "(none)"}"));
        RunSnapshotSummary = L(
            $"현재 레시피 실행 · 상태 {record.Status} · 핵심 측정값 {metricSummary} · {record.ElapsedMilliseconds:0.##} ms",
            $"Current recipe Run · {record.Status} · key metric {metricSummary} · {record.ElapsedMilliseconds:0.##} ms");
        RunSnapshotEvidence =
            $"Recipe: {ShellEvidenceTextParser.FormatShortEvidencePath(root, record.Recipe.Path)} | Source: {ShellEvidenceTextParser.FormatShortEvidencePath(root, record.Source.Path)} | JSON: {ShellEvidenceTextParser.FormatOptionalArtifact(root, currentRunRecordPath)} | Report: {ShellEvidenceTextParser.FormatOptionalArtifact(root, currentReportPath)}";
        RefreshInspectionStepsFromRecord(record);
        RecipeRunHistory.Clear();
        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            record.RecordedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
            record.Status.ToString(),
            metricSummary,
            ShellOrderedRunRecordWriter.EvidenceState,
            ShellEvidenceTextParser.FormatShortEvidencePath(root, currentRunRecordPath ?? string.Empty)));
        if (currentRunRecordPath is not null)
        {
            RecordRecentRunRecord(currentRunRecordPath, record);
        }
        StatusText = L(
            $"3D 뷰어 | 현재 레시피 실행 {record.Status}",
            $"Viewer hosted | current recipe Run {record.Status}");
    }

    private void RefreshRunSnapshot(
        string root,
        string recipePath,
        string contractPath,
        string reportPath,
        ToolComparisonEvidence uiEvidence,
        ToolComparisonEvidence runnerEvidence,
        string comparisonState)
    {
        var status = ShellEvidenceTextParser.SelectEvidenceStatus(uiEvidence, runnerEvidence);
        var keyMetricSummary = ShellEvidenceTextParser.SelectEvidenceMetric(uiEvidence, runnerEvidence);
        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";

        RunSnapshotSummary =
            $"{comparisonState} | Status: {status} | Key metric: {keyMetricSummary} | Evidence: {evidenceState} | Run: {ShellEvidenceTextParser.FormatRunTime(reportPath, contractPath)}";
        RunSnapshotEvidence =
            $"Recipe: {ShellEvidenceTextParser.FormatShortEvidencePath(root, recipePath)} | UI: {ShellEvidenceTextParser.FormatShortEvidencePath(root, contractPath)} | Runner: {ShellEvidenceTextParser.FormatShortEvidencePath(root, reportPath)} | Shell: {ShellEvidenceTextParser.FormatScreenshotTarget(root, shellScreenshotPath)} | JSON: {ShellEvidenceTextParser.FormatOptionalArtifact(root, currentRunRecordPath)} | HTML: {ShellEvidenceTextParser.FormatOptionalArtifact(root, currentHtmlReportPath)} | CSV: {ShellEvidenceTextParser.FormatOptionalArtifact(root, currentCsvReportPath)}";
    }

    private void RefreshInspectionSteps(
        string root,
        string recipePath,
        string contractPath,
        string reportPath,
        string[] contractLines,
        string[] reportLines,
        ToolComparisonEvidence uiEvidence,
        ToolComparisonEvidence runnerEvidence,
        string comparisonState,
        InspectionRunRecord? runRecord)
    {
        InspectionSteps.Clear();
        RefreshSourceQuality(runRecord);
        RefreshThresholdCorrection(runRecord);

        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";
        var order = 1;
        var orderedRunSteps = runRecord?.Steps ?? [];
        var recipeSteps = Array.Empty<string>();
        if (orderedRunSteps.Count > 0)
        {
            RefreshInspectionStepsFromRecord(runRecord!);
            return;
        }

        recipeSteps = contractLines
            .Concat(reportLines)
            .Where(line => line.StartsWith(InspectionContractText.InspectionStepMarker + "|", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var stepLine in recipeSteps)
        {
            var parts = stepLine.Split('|');
            var enabled = ShellEvidenceTextParser.ExtractTaggedValue(parts, "enabled=");
            var status = enabled?.Equals("False", StringComparison.OrdinalIgnoreCase) == true ? "Disabled" : uiEvidence.Status;
            var tool = ShellEvidenceTextParser.ExtractTaggedValue(parts, "tool=") ?? "Inspection step";
            var id = ShellEvidenceTextParser.ExtractTaggedValue(parts, "id=") ?? "(missing ID)";
            var source = ShellEvidenceTextParser.ExtractTaggedValue(parts, "source=") ?? "(missing source)";
            var reference = ShellEvidenceTextParser.ExtractTaggedValue(parts, "reference=") ?? "(missing reference)";
            InspectionSteps.Add(new InspectionStepItem(
                (order++).ToString(CultureInfo.InvariantCulture),
                tool,
                status,
                $"{id} | source {source} | reference {reference}"));
        }

        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Recipe", File.Exists(recipePath) ? "Loaded" : "Missing", ShellEvidenceTextParser.FormatShortEvidencePath(root, recipePath)));
        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Source", ShellEvidenceTextParser.ExtractSourceLoadStatus(reportLines), ShellEvidenceTextParser.ExtractSourceSummary(root, reportLines, contractLines)));

        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Viewer preview", uiEvidence.Status, $"{uiEvidence.ToolName} | {uiEvidence.KeyMetricSummary}"));
        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Runner replay", runnerEvidence.Status, $"{runnerEvidence.ToolName} | {runnerEvidence.KeyMetricSummary}"));
        InspectionSteps.Add(new InspectionStepItem(order.ToString(CultureInfo.InvariantCulture), "Evidence compare", evidenceState, $"{comparisonState} | UI {ShellEvidenceTextParser.FormatShortEvidencePath(root, contractPath)} | Runner {ShellEvidenceTextParser.FormatShortEvidencePath(root, reportPath)}"));

        InspectionStepSummary = recipeSteps.Length == 0
            ? $"Recipe: {InspectionSteps[0].Status} | Source: {InspectionSteps[1].Status} | Viewer: {uiEvidence.Status} | Runner: {runnerEvidence.Status} | Compare: {evidenceState}"
            : $"Recipe steps: {recipeSteps.Length} | Viewer: {uiEvidence.Status} | Runner: {runnerEvidence.Status} | Compare: {evidenceState}";
        ResultsOperatorDecisionSummary = FormatResultsOperatorDecisionSummary(runRecord);
        RefreshResultsValueValiditySummary(runRecord);
    }

    private void RefreshInspectionStepsFromRecord(InspectionRunRecord record)
    {
        InspectionSteps.Clear();
        RefreshSourceQuality(record);
        RefreshThresholdCorrection(record);
        var orderedRunSteps = record.Steps ?? [];
        foreach (var step in orderedRunSteps)
        {
            var metric = step.Metrics.FirstOrDefault();
            var metricSummary = step.SignedDeltaBackgroundSubtraction is { } signedDelta
                ? FormatSignedDeltaStepSummary(signedDelta)
                : metric is null
                    ? "no metrics"
                    : $"{metric.Name} {metric.Value:0.##} {metric.Unit}";
            var outputHash = string.IsNullOrWhiteSpace(step.OutputContentSha256)
                ? string.Empty
                : $" | SHA-256 {step.OutputContentSha256[..Math.Min(12, step.OutputContentSha256.Length)]}";
            InspectionSteps.Add(new InspectionStepItem(
                (step.RecipeIndex + 1).ToString(CultureInfo.InvariantCulture),
                step.ToolName,
                step.Status.ToString(),
                $"{step.Id} | {string.Join(";", step.InputEntityIds)} -> {step.OutputEntityId} | {metricSummary}{outputHash}",
                FormatTiming(step.Timing)));
        }
        if (orderedRunSteps.Count == 0
            && record.SurfaceMatchEvidence is not null)
        {
            InspectionSteps.Add(new InspectionStepItem(
                "1",
                record.ToolName,
                record.Status.ToString(),
                record.SurfaceMatchEvidence.Execution.ContentSha256,
                FormatTiming(record.Timing)));
        }
        InspectionStepSummary = string.Format(
            CultureInfo.CurrentCulture,
            Localization.RunRecordSummaryFormat,
            record.SchemaVersion,
            orderedRunSteps.Count,
            record.Status);
        ResultsOperatorDecisionSummary = FormatResultsOperatorDecisionSummary(record);
        RefreshResultsValueValiditySummary(record);
    }

    private static string FormatSignedDeltaStepSummary(
        C3DSignedHeightDeltaArtifact artifact) =>
        string.Create(
            CultureInfo.CurrentCulture,
            $"signed delta | paired {artifact.PairedValidSampleCount:N0} | missing {artifact.MissingEitherSampleCount:N0} | zero {artifact.ZeroDeltaSampleCount:N0} | +{artifact.PositiveDeltaSampleCount:N0} / -{artifact.NegativeDeltaSampleCount:N0} | background {artifact.BackgroundEntityId}");

    private static string FormatResultsOperatorDecisionSummary(
        InspectionRunRecord? record,
        string? overrideReason = null,
        ResultStatus? statusOverride = null)
    {
        if (record is null)
        {
            var reason = string.IsNullOrWhiteSpace(overrideReason)
                ? L(
                    "현재 레시피의 검사 결과가 없습니다.",
                    "No inspection result is available for the current recipe.")
                : overrideReason;
            return string.Join(
                Environment.NewLine,
                [
                    $"{L("판정", "Decision")}: {FormatResultStatus(statusOverride ?? ResultStatus.NotRun)}",
                    $"{L("대표 측정값", "Primary measurement")}: {L("기록되지 않음", "Not recorded")}",
                    $"{L("허용범위", "Limits")}: {L("기록되지 않음", "Not recorded")}",
                    $"{L("기준면 Fit RMS", "Reference-fit RMS")}: {L("기록되지 않음", "Not recorded")}",
                    $"{L("사유", "Reason")}: {reason}"
                ]);
        }

        var step = SelectResultsExplanationStep(record);
        if (step?.SignedDeltaBackgroundSubtraction is { } signedDelta)
        {
            return FormatSignedDeltaOperatorDecisionSummary(
                record,
                step,
                signedDelta,
                overrideReason,
                statusOverride);
        }

        var metrics = (step?.Metrics ?? record.Metrics ?? []).ToArray();
        var toolName = step?.ToolName ?? record.ToolName;
        var toolId = step?.ToolId ?? string.Empty;

        var isThickness = toolId.Equals("thickness", StringComparison.OrdinalIgnoreCase)
            || toolName.Contains("thickness", StringComparison.OrdinalIgnoreCase);
        var isWarpage = toolId.Equals("warpage", StringComparison.OrdinalIgnoreCase)
            || toolName.Contains("warpage", StringComparison.OrdinalIgnoreCase);
        string[]? primaryMetricNames = toolId.ToLowerInvariant() switch
        {
            "plane-flatness" => ["Reference-plane deviation range", "Reference-plane absolute peak"],
            "gap-flush" => ["Signed gap", "Signed flush"],
            "volume" => ["Signed net volume", "Above-plane volume", "Below-plane volume"],
            "cross-section-dimensions" => ["Section width", "H range"],
            _ => null
        };
        var primaryMetrics = primaryMetricNames is not null
            ? primaryMetricNames.Select(name => metrics.FirstOrDefault(metric => metric.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                .OfType<InspectionRunMetric>().ToArray()
            : isWarpage
            ? metrics.Where(metric =>
                metric.Name.Equals("PeakToValley", StringComparison.OrdinalIgnoreCase)
                || metric.Name.Equals("P2V", StringComparison.OrdinalIgnoreCase)
                || metric.Name.Equals("Rms", StringComparison.OrdinalIgnoreCase)).ToArray()
            : isThickness
                ? metrics.Where(metric => metric.Name.Equals("Mean", StringComparison.OrdinalIgnoreCase)).Take(1).ToArray()
                : metrics.Where(metric =>
                    metric.Name.Equals("Mean", StringComparison.OrdinalIgnoreCase)
                    || metric.Name.Equals("PeakToValley", StringComparison.OrdinalIgnoreCase)
                    || metric.Name.Equals("P2V", StringComparison.OrdinalIgnoreCase)
                    || metric.Name.Equals("Rms", StringComparison.OrdinalIgnoreCase)).Take(3).ToArray();
        var primarySummary = primaryMetrics.Length == 0
            ? L("기록되지 않음", "Not recorded")
            : string.Join("; ", primaryMetrics.Select(FormatMetric));

        string[] limitMetricNames = toolId.ToLowerInvariant() switch
        {
            "plane-flatness" => ["Flatness tolerance"],
            "gap-flush" => ["Expected gap", "Gap tolerance", "Expected flush", "Flush tolerance"],
            "volume" => ["Expected net volume", "Volume tolerance"],
            _ => ["LowerLimit", "UpperLimit"]
        };
        var limitMetrics = limitMetricNames.Select(name => metrics.FirstOrDefault(metric => metric.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).ToArray();
        var limitsSummary = limitMetrics.All(metric => metric is null)
            ? L("기록되지 않음", "Not recorded")
            : string.Join("; ", limitMetrics.Select((metric, index) => metric is null
                ? limitMetricNames[index] + "=" + L("기록되지 않음", "Not recorded")
                : FormatMetric(metric)));

        var referenceFitRms = metrics.FirstOrDefault(metric =>
            metric.Name.Contains("fit", StringComparison.OrdinalIgnoreCase)
            && metric.Name.Contains("rms", StringComparison.OrdinalIgnoreCase));
        var referenceFitSummary = referenceFitRms is null
            ? L("기록되지 않음", "Not recorded")
            : FormatMetric(referenceFitRms);
        var status = statusOverride ?? (metrics.Any(metric => !double.IsFinite(metric.Value)) ? ResultStatus.Error : record.Status);
        var reasons = new List<string>();
        AddReason(reasons, overrideReason);
        if (record.Status != ResultStatus.Pass)
        {
            AddReason(reasons, record.Message);
        }

        if (step is not null && step.Status != ResultStatus.Pass)
        {
            AddReason(reasons, step.Message);
        }

        foreach (var metric in metrics.Where(metric => metric.Status is not null and not ResultStatus.Pass))
        {
            AddReason(reasons, $"{metric.Name}: {FormatResultStatus(metric.Status!.Value)}");
        }
        foreach (var metric in metrics.Where(metric => !double.IsFinite(metric.Value)))
        {
            AddReason(reasons, $"{metric.Name}: {L("계산 불가", "Unavailable")} · {L("기록된 판정", "Recorded decision")}={FormatResultStatus(metric.Status ?? step?.Status ?? record.Status)}");
        }

        var reasonSummary = reasons.Count == 0
            ? L("기록된 무효 사유 없음", "None recorded")
            : string.Join("; ", reasons);
        return string.Join(
            Environment.NewLine,
            [
                $"{L("검사 도구", "Tool")}: {toolName}",
                $"{L("기록 단계", "Recorded step")}: {step?.Id ?? L("기록되지 않음", "Not recorded")}",
                $"{L("대표 측정값", "Primary measurement")}: {primarySummary}",
                $"{L("허용범위", "Limits")}: {limitsSummary}",
                $"{L("기준면 Fit RMS", "Reference-fit RMS")}: {referenceFitSummary}",
                $"{L("판정", "Decision")}: {FormatResultStatus(status)}",
                $"{L("사유", "Reason")}: {reasonSummary}"
            ]);
    }

    private static string FormatSignedDeltaOperatorDecisionSummary(
        InspectionRunRecord record,
        InspectionRunStepResult step,
        C3DSignedHeightDeltaArtifact artifact,
        string? overrideReason,
        ResultStatus? statusOverride)
    {
        var status = statusOverride ?? step.Status;
        var reasons = new List<string>();
        AddReason(reasons, overrideReason);
        if (record.Status != ResultStatus.Pass)
        {
            AddReason(reasons, record.Message);
        }

        if (step.Status != ResultStatus.Pass)
        {
            AddReason(reasons, step.Message);
        }

        if (!artifact.TryValidate(out var validationMessage))
        {
            AddReason(reasons, validationMessage);
        }

        var reasonSummary = reasons.Count == 0
            ? L("기록된 무효 사유 없음", "None recorded")
            : string.Join("; ", reasons);
        return string.Join(
            Environment.NewLine,
            [
                $"{L("검사 도구", "Tool")}: {step.ToolName}",
                $"{L("대표 측정값", "Primary measurement")}: {FormatSignedDeltaCounts(artifact)}",
                $"{L("허용범위", "Limits")}: {L("이 signed delta 결과에는 기록되지 않음", "Not recorded for this signed-delta field")}",
                $"{L("기준면 Fit RMS", "Reference-fit RMS")}: {L("해당 없음", "Not applicable")}",
                $"{L("판정", "Decision")}: {FormatResultStatus(status)}",
                $"{L("표시", "Display")}: {L("검토용 evidence-only · C3D finite-zero wire 변환 없음", "Evidence-only review · no C3D finite-zero wire conversion")}",
                $"{L("사유", "Reason")}: {reasonSummary}"
            ]);
    }

    private static string FormatSignedDeltaCounts(
        C3DSignedHeightDeltaArtifact artifact) =>
        string.Create(
            CultureInfo.CurrentCulture,
            $"paired={artifact.PairedValidSampleCount:N0}; missing={artifact.MissingEitherSampleCount:N0}; zero={artifact.ZeroDeltaSampleCount:N0}; +={artifact.PositiveDeltaSampleCount:N0}; -={artifact.NegativeDeltaSampleCount:N0}");

    private void RefreshResultsValueValiditySummary(
        InspectionRunRecord? record,
        string? overrideInvalidReason = null,
        ResultStatus? statusOverride = null) =>
        ResultsValueValiditySummary = FormatResultsValueValiditySummary(
            record,
            overrideInvalidReason,
            statusOverride);

    private static string FormatResultsValueValiditySummary(
        InspectionRunRecord? record,
        string? overrideInvalidReason,
        ResultStatus? statusOverride)
    {
        var unknown = "Unknown";
        if (record is null)
        {
            var reason = string.IsNullOrWhiteSpace(overrideInvalidReason)
                ? L(
                    "현재 레시피의 검사 결과가 없습니다.",
                    "No inspection result is available for the current recipe.")
                : overrideInvalidReason;
            return string.Join(
                Environment.NewLine,
                [
                    $"{L("정의", "Definition")}: {unknown}",
                    $"{L("단위", "Unit")}: {unknown}",
                    $"{L("소스 / ROI", "Source / ROI")}: {unknown}",
                    $"{L("유효 샘플 수", "Valid samples")}: {unknown}",
                    $"{L("Fit residual", "Fit residual")}: {unknown}",
                    $"{L("상태 / 판정", "Status / decision")}: {FormatResultStatus(statusOverride ?? ResultStatus.NotRun)}",
                    $"{L("무효 사유", "Invalid reason")}: {reason}"
                ]);
        }

        var step = SelectResultsExplanationStep(record);
        if (step?.SignedDeltaBackgroundSubtraction is { } signedDelta)
        {
            return FormatSignedDeltaValueValiditySummary(
                record,
                step,
                signedDelta,
                overrideInvalidReason,
                statusOverride);
        }

        var metrics = (step?.Metrics ?? record.Metrics ?? [])
            .Where(metric => double.IsFinite(metric.Value))
            .ToArray();
        var definition = step?.ToolName ?? record.ToolName;
        if (step?.AlgorithmEvidence is { } algorithmEvidence)
        {
            definition = string.Join(
                " · ",
                definition,
                algorithmEvidence.AlgorithmDefinitionVersion);
        }

        if (step?.ToolId.ToLowerInvariant() is "thickness" or "plane-flatness" or "gap-flush" or "volume" or "cross-section-dimensions")
        {
            // Pin the description to the recorded definition; a future producer version needs review.
            var meaning = step.AlgorithmEvidence?.AlgorithmDefinitionVersion == "height-measurement-execution-v1"
                ? step.ToolId.ToLowerInvariant() switch
                {
                    "thickness" => L("같은 위치의 측정 높이 − 최소제곱 기준면 높이인 H축 높이차. 기준면 수직거리가 아닙니다.",
                        "Signed H-axis height difference: measured height minus least-squares reference height at the same position; not orthogonal distance."),
                    "plane-flatness" => L("별도 기준 ROI의 최소제곱 평면에 대한 부호 있는 수직거리의 최대 − 최소. GD&T 최소영역 평면도가 아닙니다.",
                        "Range of signed orthogonal distances (maximum minus minimum) to the least-squares plane of the reference ROI; not GD&T minimum-zone flatness."),
                    "gap-flush" => L("간격은 마주 보는 ROI 가장자리의 부호 있는 U축 거리, 단차는 두 ROI 평균 높이의 H축 차이입니다. ROI 순서에 따라 부호가 달라집니다.",
                        "Gap is signed U-axis separation between facing ROI edges; flush is the H-axis difference of ROI mean heights. ROI order determines the sign."),
                    "volume" => L("기준면에 대한 H축 높이차에 U·V 셀 면적을 곱해 합산합니다. 순체적과 기준면 위·아래 체적을 구분합니다.",
                        "Sum of H-axis residuals from the reference plane times U/V cell area. Net, above-plane, and below-plane volumes are recorded separately."),
                    _ => L("단면의 U축 너비와 H축 최대·최소 높이차입니다. 경사진 표면을 따라 잰 길이가 아닙니다.",
                        "U-axis section width and H-axis maximum-minus-minimum height range; not length along the sloped surface.")
                }
                : L("측정 방식 확인 불가: 정의 버전이 없거나 지원되지 않습니다.",
                    "Measurement definition unavailable: the recorded definition version is missing or unsupported.");
            definition += $" — {meaning}";
        }

        var unitBasis = FormatResultsUnitBasis(record.Source, step?.Metrics ?? record.Metrics ?? []);

        var roiEvidence = step?.Overlays?
            .Where(overlay => overlay.Label.Contains("ROI", StringComparison.OrdinalIgnoreCase)
                || overlay.Label.Contains("region", StringComparison.OrdinalIgnoreCase)
                || overlay.Label.Contains("row segment", StringComparison.OrdinalIgnoreCase))
            .Select(overlay => overlay.Label).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        var roi = step?.CompletenessGrid is { } completeness
            ? FormatGridRegion(completeness.InspectionGridRegion)
            : roiEvidence.Length > 0 ? string.Join("; ", roiEvidence) : L(
                "ROI 형상은 검사 기록에 기록되지 않았습니다.",
                "ROI geometry was not recorded in the Run Record.");
        var inputIds = step?.InputEntityIds is { Count: > 0 } ids
            ? string.Join(", ", ids)
            : unknown;
        var source = string.IsNullOrWhiteSpace(record.Source.Path)
            ? record.Source.EntityId
            : $"{record.Source.EntityId} · {Path.GetFileName(record.Source.Path)}";

        var validCountMetric = metrics.FirstOrDefault(IsValidCountMetric);
        var validCount = validCountMetric is null
            ? unknown
            : FormatMetric(validCountMetric);
        var residuals = metrics
            .Where(IsFitResidualMetric)
            .Take(6)
            .Select(FormatMetric)
            .ToArray();
        var residualSummary = residuals.Length == 0
            ? unknown
            : string.Join("; ", residuals);
        var status = statusOverride ?? ((step?.Metrics ?? record.Metrics ?? []).Any(metric => !double.IsFinite(metric.Value)) ? ResultStatus.Error : record.Status);
        var invalidReasons = new List<string>();
        AddReason(invalidReasons, overrideInvalidReason);
        if (record.Status != ResultStatus.Pass)
        {
            AddReason(invalidReasons, record.Message);
        }

        if (step is not null && step.Status != ResultStatus.Pass)
        {
            AddReason(invalidReasons, step.Message);
        }

        foreach (var metric in metrics.Where(metric => metric.Status is not null and not ResultStatus.Pass))
        {
            AddReason(
                invalidReasons,
                $"{metric.Name}: {FormatResultStatus(metric.Status!.Value)}");
        }
        foreach (var metric in (step?.Metrics ?? record.Metrics ?? []).Where(metric => !double.IsFinite(metric.Value)))
        {
            AddReason(invalidReasons, $"{metric.Name}: {L("계산 불가", "Unavailable")} ({FormatResultStatus(metric.Status ?? ResultStatus.NotRun)})");
        }

        var invalidReason = invalidReasons.Count == 0
            ? L("기록된 무효 사유 없음", "None recorded")
            : string.Join("; ", invalidReasons);
        return string.Join(
            Environment.NewLine,
            [
                $"{L("정의", "Definition")}: {definition}",
                $"{L("단위 근거", "Unit basis")}: {unitBasis}",
                $"{L("기록 단계", "Recorded step")}: {step?.Id ?? unknown}",
                $"{L("소스 / ROI", "Source / ROI")}: {source} · input={inputIds} · ROI={roi}",
                $"{L("유효 샘플 수", "Valid samples")}: {validCount}",
                $"{L("Fit residual", "Fit residual")}: {residualSummary}",
                $"{L("상태 / 판정", "Status / decision")}: {FormatResultStatus(status)}",
                $"{L("무효 사유", "Invalid reason")}: {invalidReason}"
            ]);
    }

    private static string FormatResultsUnitBasis(InspectionRunSource source, IReadOnlyList<InspectionRunMetric> metrics)
    {
        // Units are recorded metadata even for unavailable (NaN) values; source units cannot fill metric gaps.
        var units = metrics.Where(metric => metric.Kind != MetricKind.Count)
            .Select(metric => string.IsNullOrWhiteSpace(metric.Unit) ? "Unknown" : metric.Unit)
            .Distinct(StringComparer.Ordinal).ToArray();
        var measurementUnits = units.Length == 0 ? "Unknown" : string.Join(", ", units);
        var sourceUnit = string.IsNullOrWhiteSpace(source.Unit) ? "Unknown" : source.Unit;
        var evidence = source.EffectiveMeasurementEvidence;
        var basis = evidence.State switch
        {
            HeightMeasurementEvidenceState.RawHeight => L("원시 높이 입력. 물리 교정 기록 없음.", "Raw-height input; no physical calibration recorded."),
            HeightMeasurementEvidenceState.DeclaredUnit => L("입력 단위만 선언됨. 물리 교정 기록 없음.", "Declared input unit only; no physical calibration recorded."),
            HeightMeasurementEvidenceState.CalibratedPhysical => L($"기록된 입력 교정: {evidence.CalibrationId}. 측정값별 교정 확인과는 별개입니다.", $"Recorded input calibration: {evidence.CalibrationId}; this does not establish calibration of each metric."),
            _ => L("입력 단위 근거 없음. 물리 교정 여부 확인 불가.", "Input unit evidence unavailable; physical calibration is unknown.")
        };
        return L($"측정 단위: {measurementUnits}; 입력 단위: {sourceUnit}. {basis}",
            $"Measurement units: {measurementUnits}; input unit: {sourceUnit}. {basis}");
    }

    private static string FormatSignedDeltaValueValiditySummary(
        InspectionRunRecord record,
        InspectionRunStepResult step,
        C3DSignedHeightDeltaArtifact artifact,
        string? overrideInvalidReason,
        ResultStatus? statusOverride)
    {
        var invalidReasons = new List<string>();
        AddReason(invalidReasons, overrideInvalidReason);
        if (record.Status != ResultStatus.Pass)
        {
            AddReason(invalidReasons, record.Message);
        }

        if (step.Status != ResultStatus.Pass)
        {
            AddReason(invalidReasons, step.Message);
        }

        if (!artifact.TryValidate(out var validationMessage))
        {
            AddReason(invalidReasons, validationMessage);
        }

        var invalidReason = invalidReasons.Count == 0
            ? L("기록된 무효 사유 없음", "None recorded")
            : string.Join("; ", invalidReasons);
        var status = statusOverride ?? step.Status;
        var source = string.IsNullOrWhiteSpace(record.Source.Path)
            ? record.Source.EntityId
            : $"{record.Source.EntityId} · {Path.GetFileName(record.Source.Path)}";
        var provenance = string.Join(
            "; ",
            [
                $"current={artifact.CurrentSourceEntityId}",
                $"background={artifact.BackgroundEntityId}",
                $"grid={artifact.Width} × {artifact.Height}",
                $"origin={artifact.GridOriginColumn},{artifact.GridOriginRow}",
                $"currentSha={artifact.CurrentSourceContentSha256}",
                $"backgroundSha={artifact.BackgroundContentSha256}",
                $"outputValuesSha={artifact.OutputContentSha256}",
                $"artifactSha={artifact.ContentSha256}"
            ]);
        return string.Join(
            Environment.NewLine,
            [
                $"{L("정의", "Definition")}: SignedDeltaHeightField · {artifact.SubtractionPolicy} · {artifact.MissingValuePolicy}",
                $"{L("단위 근거", "Unit basis")}: {L($"signed delta scalar · {artifact.Unit}; finite zero는 유효하며 C3D 누락으로 변환하지 않습니다.", $"Signed-delta scalar · {artifact.Unit}; finite zero is valid and is not converted to C3D missing.")}",
                $"{L("소스 / ROI", "Source / ROI")}: {source} · {provenance}",
                $"{L("유효 샘플 수", "Valid samples")}: {FormatSignedDeltaCounts(artifact)}",
                $"{L("Fit residual", "Fit residual")}: {L("해당 없음 — signed delta 셀 분포를 기록합니다.", "Not applicable — the signed-delta cell distribution is recorded.")}",
                $"{L("상태 / 판정", "Status / decision")}: {FormatResultStatus(status)}",
                $"{L("무효 사유", "Invalid reason")}: {invalidReason}"
            ]);
    }

    private static InspectionRunStepResult? SelectResultsExplanationStep(
        InspectionRunRecord record)
    {
        var measurements = record.Steps?.Where(step => step.ToolId.ToLowerInvariant() is
            "thickness" or "warpage" or "plane-flatness" or "gap-flush" or "volume"
            or "cross-section-dimensions" or "point-pair-dimensions" or "completeness-grid"
            or "physical-edge-gap-flush" or "connected-region-gate").ToArray() ?? [];
        return measurements.FirstOrDefault(step => step.Status == ResultStatus.Error)
            ?? measurements.FirstOrDefault(step => step.Metrics.Any(metric => !double.IsFinite(metric.Value)))
            ?? measurements.FirstOrDefault(step => step.Status == ResultStatus.Fail)
            ?? measurements.FirstOrDefault(step => step.Status != ResultStatus.Pass)
            ?? measurements.FirstOrDefault()
            ?? record.Steps?.FirstOrDefault(step => step.ToolId.Equals(C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.OrdinalIgnoreCase))
            ?? record.Steps?.FirstOrDefault();
    }

    private static bool IsValidCountMetric(InspectionRunMetric metric) =>
        metric.Kind == MetricKind.Count
        && ((metric.Name.Contains("valid", StringComparison.OrdinalIgnoreCase)
                || metric.Name.Contains("finite", StringComparison.OrdinalIgnoreCase))
            && (metric.Name.Contains("sample", StringComparison.OrdinalIgnoreCase)
                || metric.Name.Contains("cell", StringComparison.OrdinalIgnoreCase)));

    private static bool IsFitResidualMetric(InspectionRunMetric metric) =>
        metric.Name.Contains("residual", StringComparison.OrdinalIgnoreCase)
        || (metric.Name.Contains("fit", StringComparison.OrdinalIgnoreCase)
            && metric.Name.Contains("rms", StringComparison.OrdinalIgnoreCase))
        || metric.Name.Equals("PeakToValley", StringComparison.OrdinalIgnoreCase)
        || metric.Name.Equals("Rms", StringComparison.OrdinalIgnoreCase);

    private static string FormatMetric(InspectionRunMetric metric) =>
        !double.IsFinite(metric.Value)
            ? $"{metric.Name}={L("계산 불가", "Unavailable")}{(string.IsNullOrWhiteSpace(metric.Unit) ? string.Empty : " " + metric.Unit)}"
            : string.IsNullOrWhiteSpace(metric.Unit)
            ? $"{metric.Name}={metric.Value:0.##}"
            : $"{metric.Name}={metric.Value:0.##} {metric.Unit}";

    private static string FormatGridRegion(ToolRecipeGridRectangle region) =>
        string.Create(
            CultureInfo.CurrentCulture,
            $"row={region.Row}, column={region.Column}, size={region.RowCount} × {region.ColumnCount}");

    private static string FormatResultStatus(ResultStatus status) =>
        status == ResultStatus.Fail ? "Fail (NG)" : status.ToString();

    private static void AddReason(ICollection<string> reasons, string? reason)
    {
        if (!string.IsNullOrWhiteSpace(reason)
            && !reasons.Contains(reason, StringComparer.Ordinal))
        {
            reasons.Add(reason);
        }
    }

    private static string FormatTiming(InspectionRunTiming? timing)
    {
        if (timing is null || !timing.TryValidate(out _)
            || timing.State != InspectionRunTimingState.Available)
        {
            return L("사용 불가", "Unavailable");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{timing.TotalElapsedMilliseconds:0.##} ms · {string.Join("; ", timing.Stages.Select(stage => $"{stage.StageId} {stage.ElapsedMilliseconds:0.##} ms"))}");
    }

    private void RefreshSourceQuality(InspectionRunRecord? runRecord)
    {
        var evidence = runRecord?.SourceQualityEvidence;
        if (evidence is null)
        {
            SourceQualityState = "Unavailable";
            SourceQualitySummary = runRecord is null
                ? L(
                    "실행 기록이 로드되지 않았습니다.",
                    "No Run Record is loaded.")
                : L(
                    "이전 실행 기록에는 입력 상태 확인 결과가 없습니다.",
                    "This legacy Run Record did not record Source Quality evidence.");
            SourceQualityDetail = SourceQualitySummary;
            return;
        }

        if (evidence.Report is not { } report)
        {
            SourceQualityState = "Unavailable";
            SourceQualitySummary = evidence.Message;
            SourceQualityDetail = evidence.Message;
            return;
        }

        if (runRecord is null)
        {
            SourceQualityState = "Mismatch";
            SourceQualitySummary =
                "Source Quality cannot be validated without its Run Record source.";
            SourceQualityDetail = SourceQualitySummary;
            return;
        }

        if (!evidence.TryValidate(runRecord.Source, out var validationMessage))
        {
            SourceQualityState = "Mismatch";
            SourceQualitySummary = validationMessage;
            SourceQualityDetail = validationMessage;
            return;
        }

        var diagnostics = report.GridDiagnostics;
        SourceQualityState = diagnostics?.State
            == SourceQualityGridDiagnosticState.Error
            ? "Error"
            : report.Coverage.MissingSampleCount > 0
            ? "Warning"
            : "Pass";
        SourceQualitySummary = string.Format(
            CultureInfo.CurrentCulture,
            L(
                "{0} × {1} · 유효 {2:P1} · 누락 {3:P1}{4}",
                "{0} × {1} · valid {2:P1} · missing {3:P1}{4}"),
            report.Grid.Width,
            report.Grid.Height,
            report.Coverage.ValidRatio,
            report.Coverage.MissingRatio,
            diagnostics is null
                ? string.Empty
                : $" · {L("진단", "diagnostics")} {diagnostics.State}");
        var detailLines = new List<string>
        {
            $"SHA-256 {evidence.SourceQualitySha256}",
            $"{L("유효", "Valid")} {report.Coverage.ValidSampleCount:N0} · {L("누락", "Missing")} {report.Coverage.MissingSampleCount:N0}",
            $"{L("누락 셀 맵", "Invalid-cell mask")} SHA-256 {report.Coverage.InvalidCellMask.Sha256}",
            $"{L("좌표", "Coordinates")} {report.Coordinates.FrameId} · {report.Coordinates.Unit} · {report.Coordinates.CoordinateConvention}",
            $"{L("출처", "Provenance")} {report.Provenance}",
            $"{L("채널", "Channels")} {string.Join("; ", report.Channels.Select(channel => $"{channel.Channel}={channel.State}: {channel.Evidence}"))}"
        };
        if (diagnostics is not null)
        {
            detailLines.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{L("그리드 진단", "Grid diagnostics")} {diagnostics.State} · schema={diagnostics.SchemaVersion} · declared={diagnostics.DeclaredCellCount} · observed={diagnostics.ObservedSampleCount} · unique={diagnostics.UniqueLocatorCount}"));
            detailLines.AddRange(diagnostics.Checks.Select(
                FormatSourceQualityDiagnostic));
        }

        SourceQualityDetail = string.Join(Environment.NewLine, detailLines);
    }

    private static string FormatSourceQualityDiagnostic(
        SourceQualityGridDiagnosticCheck check)
    {
        var locationParts = new List<string>();
        if (check.FirstSampleOrdinal is { } ordinal)
        {
            locationParts.Add($"ordinal={ordinal.ToString(CultureInfo.InvariantCulture)}");
        }

        if (check.FirstRow is { } row)
        {
            locationParts.Add($"row={row.ToString(CultureInfo.InvariantCulture)}");
        }

        if (check.FirstColumn is { } column)
        {
            locationParts.Add($"column={column.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrEmpty(check.FirstComponent))
        {
            locationParts.Add($"component={check.FirstComponent}");
        }

        var location = locationParts.Count == 0
            ? L("없음", "none")
            : string.Join(", ", locationParts);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatSourceQualityDiagnosticTitle(check.Code)} ({check.Code}) · {check.State} · {L("영향", "affected")}={check.AffectedCount} · {L("최초", "first")}={location} · {check.Message}");
    }

    private static string FormatSourceQualityDiagnosticTitle(
        SourceQualityGridDiagnosticCode code) =>
        code switch
        {
            SourceQualityGridDiagnosticCode.Topology =>
                L("그리드 토폴로지", "Grid topology"),
            SourceQualityGridDiagnosticCode.LocatorMonotonicity =>
                L("로케이터 순서", "Locator order"),
            SourceQualityGridDiagnosticCode.DuplicateLocator =>
                L("중복 로케이터", "Duplicate locators"),
            SourceQualityGridDiagnosticCode.CoordinateFiniteness =>
                L("좌표 유한성", "Coordinate finiteness"),
            _ => code.ToString()
        };

    private void RefreshThresholdCorrection(InspectionRunRecord? runRecord)
    {
        ThresholdCorrectionItems.Clear();
        var snapshot = runRecord?.ThresholdCorrectionEvidence;
        if (snapshot is null)
        {
            ThresholdCorrectionState = "Unavailable";
            ThresholdCorrectionSummary =
                runRecord is null
                    ? L(
                        "실행 기록이 로드되지 않았습니다.",
                        "No Run Record is loaded.")
                    : L(
                        "이전 실행 기록에는 임계값 보정 정보가 없습니다.",
                        "This legacy Run Record did not record threshold-correction evidence.");
            return;
        }

        ThresholdCorrectionState = snapshot.State.ToString();
        ThresholdCorrectionSummary = snapshot.Message;
        if (snapshot.Evidence is not { } evidence)
        {
            ThresholdCorrectionItems.Add(new InspectionThresholdCorrectionItem(
                "Sidecar",
                snapshot.SidecarSha256 is null
                    ? snapshot.SidecarPath
                    : $"{snapshot.SidecarPath} | SHA-256 {snapshot.SidecarSha256}"));
            return;
        }

        var proposal = evidence.Proposal;
        ThresholdCorrectionItems.Add(new InspectionThresholdCorrectionItem(
            "Identity",
            $"{proposal.CandidateId} | {proposal.StepId} / {proposal.ToolId} | {proposal.MetricName} {proposal.LimitKind}"));
        ThresholdCorrectionItems.Add(new InspectionThresholdCorrectionItem(
            "Before",
            FormatThresholdParameters(
                proposal.Changes.Select(change =>
                    (change.ParameterName, change.BeforeValue)))));
        ThresholdCorrectionItems.Add(new InspectionThresholdCorrectionItem(
            "Suggested",
            FormatThresholdParameters(
                proposal.Changes.Select(change =>
                    (change.ParameterName, change.ProposedValue)))));

        if (evidence.ManualCorrection is { } manual)
        {
            ThresholdCorrectionItems.Add(new InspectionThresholdCorrectionItem(
                "Manual",
                FormatThresholdParameters(
                    manual.ParameterChanges.Select(change =>
                        (change.ParameterName, change.ManualValue)))));
            ThresholdCorrectionItems.Add(new InspectionThresholdCorrectionItem(
                "Development",
                $"Mismatch {manual.BeforeMismatchCount}->{manual.AfterMismatchCount} | "
                + $"before [{FormatDevelopmentSampleIdentities(manual.BeforeDevelopmentSamples)}] | "
                + $"corrected [{FormatDevelopmentSampleIdentities(manual.AfterDevelopmentSamples)}]"));
        }
        else
        {
            ThresholdCorrectionItems.Add(new InspectionThresholdCorrectionItem(
                "Committed",
                "The deterministic suggestion was applied without a later manual correction."));
        }

        ThresholdCorrectionItems.Add(new InspectionThresholdCorrectionItem(
            "Held-out",
            string.Join(
                "; ",
                evidence.HeldOutSamples.Select(sample =>
                    $"{sample.SampleOrder}:{sample.SampleIdentity}:{sample.Status}"))));
    }

    private static string FormatThresholdParameters(
        IEnumerable<(string Name, string Value)> parameters) =>
        string.Join(
            ", ",
            parameters.Select(parameter =>
                $"{parameter.Name}={parameter.Value}"));

    private static string FormatDevelopmentSampleIdentities(
        IReadOnlyList<ToolRecipeThresholdDevelopmentSampleEvidence> samples) =>
        string.Join(
            "; ",
            samples.Select(sample =>
                $"{sample.SampleOrder}:{sample.Role}:{sample.SampleIdentity}:{sample.Status}:match={sample.ExpectedMatch}"));

    private void RefreshRunHistory(
        string root,
        string contractPath,
        string reportPath,
        ToolComparisonEvidence uiEvidence,
        ToolComparisonEvidence runnerEvidence,
        string comparisonState)
    {
        RecipeRunHistory.Clear();

        var status = ShellEvidenceTextParser.SelectEvidenceStatus(uiEvidence, runnerEvidence);
        var keyMetricSummary = ShellEvidenceTextParser.SelectEvidenceMetric(uiEvidence, runnerEvidence);
        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";

        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            ShellEvidenceTextParser.FormatRunTime(reportPath, contractPath),
            status,
            keyMetricSummary,
            evidenceState,
            ShellEvidenceTextParser.FormatShortEvidencePath(root, reportPath)));
    }

    private static string[] ReadLinesOrEmpty(string path) =>
        File.Exists(path) ? File.ReadAllLines(path) : [];

    public bool LoadRunRecord(string path, out string message)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var record = runRecordPersistence.Read(fullPath);
            if (record is null)
            {
                message = Localization.RunRecordOpenFailed;
                return false;
            }

            startupEvidenceActive = false;
            RefreshRecipeComparison(fullPath, useStartupOverrides: false);
            message = fullPath;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            return false;
        }
    }

    public bool CompareSelectedRunRecord(out string message)
    {
        if (!CanCompareSelectedRunRecord())
        {
            message = L(
                "현재 기록과 다른 사용 가능한 최근 기록을 선택하세요.",
                "Select an available recent record that is different from the current record.");
            return false;
        }

        var selected = SelectedRecentRunRecord!;
        var current = runRecordPersistence.Read(currentRunRecordPath);
        var comparison = runRecordPersistence.Read(selected.Path);
        if (current is null
            || comparison is null
            || !HasRequiredRunRecordComparisonShape(current)
            || !HasRequiredRunRecordComparisonShape(comparison))
        {
            RunRecordComparisonStatus = RunRecordComparisonState.Error;
            RunRecordComparisonSummary = L(
                "현재 기록 또는 선택 기록을 읽을 수 없습니다. 파일과 JSON 스키마를 확인하세요.",
                "The current or selected record could not be read. Check the file and JSON schema.");
            RunRecordComparisonDetails = L(
                $"현재: {Path.GetFileName(currentRunRecordPath)}\n선택: {Path.GetFileName(selected.Path)}\n현재 결과와 레시피 상태는 변경되지 않았습니다.",
                $"Current: {Path.GetFileName(currentRunRecordPath)}\nSelected: {Path.GetFileName(selected.Path)}\nThe current result and recipe state were not changed.");
            StatusText = L(
                "3D 뷰어 | 검사 기록 비교 실패",
                "Viewer hosted | Run Record comparison failed");
            message = RunRecordComparisonSummary;
            return false;
        }

        var projection = CompareRunRecords(current, comparison);
        RunRecordComparisonStatus = projection.State;
        RunRecordComparisonSummary = projection.Summary;
        RunRecordComparisonDetails = projection.Details;
        StatusText = L(
            $"3D 뷰어 | 검사 기록 비교 {RunRecordComparisonStateText}",
            $"Viewer hosted | Run Record comparison {RunRecordComparisonStateText}");
        message = projection.Summary;
        return true;
    }

    private bool CanCompareSelectedRunRecord()
    {
        if (string.IsNullOrWhiteSpace(currentRunRecordPath)
            || !File.Exists(currentRunRecordPath)
            || SelectedRecentRunRecord is not { IsAvailable: true } selected
            || !File.Exists(selected.Path))
        {
            return false;
        }

        try
        {
            return !string.Equals(
                Path.GetFullPath(currentRunRecordPath),
                Path.GetFullPath(selected.Path),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool HasRequiredRunRecordComparisonShape(InspectionRunRecord record) =>
        record.Source is not null
        && record.Recipe is not null
        && (record.Steps is null
            || record.Steps.All(step =>
                step is not null
                && step.InputEntityIds is not null
                && step.Metrics is not null));

    private static RunRecordComparisonProjection CompareRunRecords(
        InspectionRunRecord current,
        InspectionRunRecord comparison)
    {
        var currentSteps = current.Steps ?? [];
        var comparisonSteps = comparison.Steps ?? [];
        var schemaSupported = InspectionRunRecord.SupportsSchemaVersion(current.SchemaVersion)
            && InspectionRunRecord.SupportsSchemaVersion(comparison.SchemaVersion);
        var sourceContentSame = !string.IsNullOrWhiteSpace(current.Source.Sha256)
            && string.Equals(current.Source.Sha256, comparison.Source.Sha256, StringComparison.OrdinalIgnoreCase)
            && current.Source.ByteLength == comparison.Source.ByteLength;
        var unitSame = string.Equals(current.Source.Unit, comparison.Source.Unit, StringComparison.Ordinal);
        var frameSame = string.Equals(
            current.Source.FrameId ?? string.Empty,
            comparison.Source.FrameId ?? string.Empty,
            StringComparison.Ordinal);
        var topologySame = currentSteps.Count > 0
            && currentSteps.Count == comparisonSteps.Count
            && currentSteps.Zip(comparisonSteps).All(pair =>
                pair.First.RecipeIndex == pair.Second.RecipeIndex
                && string.Equals(pair.First.Id, pair.Second.Id, StringComparison.Ordinal)
                && string.Equals(pair.First.ToolId, pair.Second.ToolId, StringComparison.Ordinal)
                && pair.First.InputEntityIds.SequenceEqual(pair.Second.InputEntityIds, StringComparer.Ordinal)
                && string.Equals(pair.First.OutputEntityId, pair.Second.OutputEntityId, StringComparison.Ordinal));
        var alignedSteps = topologySame
            ? currentSteps.Zip(comparisonSteps).ToArray()
            : [];
        var semanticEvidenceAvailable = topologySame
            && alignedSteps.All(pair =>
                !string.IsNullOrWhiteSpace(pair.First.SemanticFingerprint)
                && !string.IsNullOrWhiteSpace(pair.Second.SemanticFingerprint));
        var semanticSame = semanticEvidenceAvailable
            && alignedSteps.All(pair => string.Equals(
                pair.First.SemanticFingerprint,
                pair.Second.SemanticFingerprint,
                StringComparison.OrdinalIgnoreCase));
        var algorithmEvidenceAvailable = topologySame
            && alignedSteps.All(pair =>
                pair.First.AlgorithmEvidence is { } currentAlgorithm
                && pair.Second.AlgorithmEvidence is { } comparisonAlgorithm
                && currentAlgorithm.TryValidate(out _)
                && comparisonAlgorithm.TryValidate(out _));
        var algorithmSame = algorithmEvidenceAvailable
            && alignedSteps.All(pair => pair.First.AlgorithmEvidence == pair.Second.AlgorithmEvidence);
        var recipeSame = string.Equals(current.Recipe.RecipeType, comparison.Recipe.RecipeType, StringComparison.Ordinal)
            && string.Equals(current.Recipe.Version, comparison.Recipe.Version, StringComparison.Ordinal)
            && string.Equals(current.Recipe.Sha256, comparison.Recipe.Sha256, StringComparison.OrdinalIgnoreCase);

        var state = !schemaSupported
            || !sourceContentSame
            || !unitSame
            || !frameSame
            || !topologySame
            || !semanticEvidenceAvailable
            || !algorithmEvidenceAvailable
                ? RunRecordComparisonState.NotDirectlyComparable
                : !recipeSame || !semanticSame || !algorithmSame
                    ? RunRecordComparisonState.ConditionsChanged
                    : RunRecordComparisonState.DirectlyComparable;

        var conditionLines = new List<string>
        {
            L(
                $"입력 데이터 바이트: {SameText(sourceContentSame)} · 단위: {SameText(unitSame)} · 좌표계: {SameText(frameSame)}",
                $"Source bytes: {SameText(sourceContentSame)} · unit: {SameText(unitSame)} · coordinate frame: {SameText(frameSame)}"),
            L(
                $"레시피 내용: {SameText(recipeSame)} · 단계 구조: {SameText(topologySame)}",
                $"Recipe content: {SameText(recipeSame)} · step topology: {SameText(topologySame)}"),
            L(
                $"검사 정의 fingerprint: {EvidenceText(semanticEvidenceAvailable, semanticSame)} · 알고리즘/SDK: {EvidenceText(algorithmEvidenceAvailable, algorithmSame)}",
                $"Inspection-definition fingerprint: {EvidenceText(semanticEvidenceAvailable, semanticSame)} · algorithm/SDK: {EvidenceText(algorithmEvidenceAvailable, algorithmSame)}")
        };
        if (semanticEvidenceAvailable && !semanticSame)
        {
            conditionLines.Add(L(
                "레시피 식별 정보가 달라 ROI·설정값·검사 조건 중 하나 이상이 변경되었습니다. 식별 정보만으로 변경 항목을 알 수는 없습니다.",
                "The fingerprint changed, so at least one authored ROI, parameter, or inspection-definition input changed. The hash does not identify a specific field."));
        }
        if (!semanticEvidenceAvailable || !algorithmEvidenceAvailable)
        {
            conditionLines.Add(L(
                "저장된 검사 조건 또는 알고리즘 정보가 부족해 동일한 조건인지 확인할 수 없습니다.",
                "Stored semantic or algorithm evidence is insufficient to prove equal conditions."));
        }

        var resultLines = new List<string>
        {
            L(
                $"전체 판정: {current.Status} → {comparison.Status}",
                $"Overall decision: {current.Status} → {comparison.Status}")
        };
        if (topologySame)
        {
            foreach (var pair in alignedSteps)
            {
                var metricChanges = CountMetricChanges(pair.First.Metrics, pair.Second.Metrics);
                var outputSame = string.Equals(
                    pair.First.OutputContentSha256 ?? string.Empty,
                    pair.Second.OutputContentSha256 ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
                if (pair.First.Status != pair.Second.Status || metricChanges > 0 || !outputSame)
                {
                    resultLines.Add(L(
                        $"{pair.First.Id}: 상태 {pair.First.Status} → {pair.Second.Status} · 측정값 변경 {metricChanges}개 · 출력 {SameText(outputSame)}",
                        $"{pair.First.Id}: status {pair.First.Status} → {pair.Second.Status} · metric changes {metricChanges} · output {SameText(outputSame)}"));
                }
            }
        }
        if (resultLines.Count == 1 && current.Status == comparison.Status)
        {
            resultLines.Add(L(
                "저장된 단계 상태·측정값·출력 해시 차이가 없습니다.",
                "No stored step-status, metric, or output-hash differences were found."));
        }

        var summary = state switch
        {
            RunRecordComparisonState.DirectlyComparable => L(
                "동일한 저장 조건 정보가 확인되었습니다. 아래 결과 차이를 직접 검토할 수 있습니다.",
                "The stored condition evidence matches. The result differences below can be reviewed directly."),
            RunRecordComparisonState.ConditionsChanged => L(
                "입력 형식은 비교 가능하지만 검사 조건이 변경되었습니다. 결과 차이를 시간 변화로 해석하지 마세요.",
                "The input basis is comparable, but inspection conditions changed. Do not attribute result differences to time."),
            _ => L(
                "입력 데이터·단위·좌표계·단계 구조 또는 필수 정보가 달라 직접 비교할 수 없습니다.",
                "The source, unit, frame, step topology, or required evidence differs, so the runs are not directly comparable.")
        };
        var timingLines = new[]
        {
            L(
                $"관찰 시각: {current.RecordedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} → {comparison.RecordedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
                $"Observed time: {current.RecordedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} → {comparison.RecordedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}"),
            L(
                $"관찰 수행 시간: {current.ElapsedMilliseconds:0.##} ms → {comparison.ElapsedMilliseconds:0.##} ms · 판정/동일성 근거에 사용하지 않음",
                $"Observed duration: {current.ElapsedMilliseconds:0.##} ms → {comparison.ElapsedMilliseconds:0.##} ms · excluded from condition identity and decisions")
        };
        var details = string.Join(
            Environment.NewLine,
            conditionLines
                .Concat(new[] { string.Empty })
                .Concat(resultLines)
                .Concat(new[] { string.Empty })
                .Concat(timingLines));
        return new RunRecordComparisonProjection(state, summary, details);
    }

    private static int CountMetricChanges(
        IReadOnlyList<InspectionRunMetric> current,
        IReadOnlyList<InspectionRunMetric> comparison)
    {
        static string Key(InspectionRunMetric metric) =>
            $"{metric.Name}\u001f{metric.Kind}\u001f{metric.Unit}";

        var currentByKey = current.GroupBy(Key, StringComparer.Ordinal).ToDictionary(
            group => group.Key,
            group => group.First(),
            StringComparer.Ordinal);
        var comparisonByKey = comparison.GroupBy(Key, StringComparer.Ordinal).ToDictionary(
            group => group.Key,
            group => group.First(),
            StringComparer.Ordinal);
        return currentByKey.Keys.Union(comparisonByKey.Keys, StringComparer.Ordinal).Count(key =>
            !currentByKey.TryGetValue(key, out var currentMetric)
            || !comparisonByKey.TryGetValue(key, out var comparisonMetric)
            || !currentMetric.Value.Equals(comparisonMetric.Value)
            || currentMetric.Status != comparisonMetric.Status);
    }

    private static string SameText(bool same) => L(
        same ? "동일" : "다름",
        same ? "same" : "different");

    private static string EvidenceText(bool available, bool same) => available
        ? SameText(same)
        : L("정보 없음", "evidence unavailable");

    private void ResetRunRecordComparison()
    {
        RunRecordComparisonStatus = RunRecordComparisonState.Unavailable;
        RunRecordComparisonSummary = string.Empty;
        RunRecordComparisonDetails = string.Empty;
    }

    public bool ExportCurrentRunRecordBundle(string targetRoot, out string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentRunRecordPath) || !File.Exists(currentRunRecordPath))
            {
                message = Localization.RunRecordOpenFailed;
                return false;
            }

            var record = runRecordPersistence.Read(currentRunRecordPath);
            if (record is null)
            {
                message = Localization.RunRecordOpenFailed;
                return false;
            }

            var exportDirectory = runRecordPersistence.ExportRunRecordBundle(
                currentRunRecordPath,
                record,
                currentHtmlReportPath,
                currentCsvReportPath,
                targetRoot);

            StatusText = string.Format(CultureInfo.CurrentCulture, Localization.RunRecordExportedFormat, exportDirectory);
            message = exportDirectory;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            return false;
        }
    }

    public bool ExportInspectionQueueCsv(string targetPath, out string message)
    {
        try
        {
            runRecordPersistence.ExportInspectionQueueCsv(
                Workbench.InspectionQueue.CreateExportRows(),
                targetPath);
            message = Path.GetFullPath(targetPath);
            StatusText = L("검사 목록 CSV를 저장했습니다.", "Inspection queue CSV was exported.");
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            message = exception.Message;
            return false;
        }
    }

    public bool ExportPrivacySafeSupportBundle(string targetRoot, out string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentRunRecordPath)
                || !File.Exists(currentRunRecordPath))
            {
                message = Localization.RunRecordOpenFailed;
                return false;
            }

            var outputPath = runRecordPersistence.ExportPrivacySafeSupportBundle(
                currentRunRecordPath,
                targetRoot,
                Workbench.RunLog.ToArray());
            StatusText = string.Format(
                CultureInfo.CurrentCulture,
                Localization.SupportBundleExportedFormat,
                outputPath);
            message = outputPath;
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or JsonException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException
            or System.Security.Cryptography.CryptographicException)
        {
            message = exception.Message;
            return false;
        }
    }

    private void LoadRecentRunRecords()
    {
        try
        {
            foreach (var path in runRecordPersistence.LoadRecentPaths())
            {
                RecentRunRecords.Add(CreateRecentRunRecordItem(
                    path,
                    runRecordPersistence.Read(path)));
            }

            SelectedRecentRunRecord = null;
            ResultsWorkspace.RefreshHistory(RecentRunRecords.ToArray());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            StatusText = $"Viewer hosted | recent Run Records unavailable: {exception.Message}";
        }
    }

    private void RecordRecentRunRecord(string path, InspectionRunRecord record)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = RecentRunRecords.FirstOrDefault(item =>
            string.Equals(item.Path, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentRunRecords.Remove(existing);
        }

        var item = CreateRecentRunRecordItem(fullPath, record);
        RecentRunRecords.Insert(0, item);
        while (RecentRunRecords.Count > ShellRunRecordPersistence.MaximumRecentRecords)
        {
            RecentRunRecords.RemoveAt(RecentRunRecords.Count - 1);
        }

        SelectedRecentRunRecord = item;
        ResultsWorkspace.RefreshHistory(RecentRunRecords.ToArray());
        try
        {
            runRecordPersistence.SaveRecentPaths(RecentRunRecords.Select(recent => recent.Path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            StatusText = $"Viewer hosted | recent Run Records could not be saved: {exception.Message}";
        }
    }

    private static RunRecordRecentItem CreateRecentRunRecordItem(string path, InspectionRunRecord? record) =>
        new(
            Path.GetFullPath(path),
            Path.GetFileName(path),
            record?.RecordedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
                ?? File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
            record?.Status.ToString() ?? "Unavailable",
            record?.Steps?.Count ?? (record?.Step is null ? 0 : 1),
            record is not null)
        {
            Snapshot = record is null ? null : ResultsRunSnapshot.FromRecord(record)
        };

    public void UpdateC3DSampleVisible(bool isVisible)
    {
        if (c3DSampleVisible != isVisible)
        {
            c3DSampleVisible = isVisible;
            RefreshCommandCanExecute();
        }
    }

    private void RefreshCommandCanExecute()
    {
        ((RelayCommand)ApplyRoiAlignmentCommand).RaiseCanExecuteChanged();
        ((RelayCommand)FitPlaneCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenUiContractCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenRunnerReportCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenShellScreenshotCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenRunRecordCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenHtmlReportCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenCsvReportCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenRunRecordFolderCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ExportRunRecordCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ExportPrivacySafeSupportBundleCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CompareRecentRunRecordCommand).RaiseCanExecuteChanged();
    }

    private void RequestEvidenceArtifact(string label, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = $"Viewer hosted | {label} artifact path is not available";
            return;
        }

        OpenEvidenceArtifactRequested?.Invoke(this, new EvidenceArtifactOpenRequestEventArgs(label, path));
    }

    private static string LocalizeResultSummaryForDisplay(string summary)
    {
        var english = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English;
        return summary
            .Replace("NotRun", english ? "Not run" : "미실행", StringComparison.Ordinal)
            .Replace("Fail (NG)", english ? "Fail (NG)" : "실패", StringComparison.Ordinal)
            .Replace("Pass", english ? "Pass" : "통과", StringComparison.Ordinal)
            .Replace("Fail", english ? "Fail" : "실패", StringComparison.Ordinal)
            .Replace("Warning", english ? "Warning" : "경고", StringComparison.Ordinal)
            .Replace("Error", english ? "Error" : "오류", StringComparison.Ordinal)
            .Replace("Unknown", english ? "Unknown" : "알 수 없음", StringComparison.Ordinal)
            .Replace("Unavailable", english ? "Unavailable" : "사용할 수 없음", StringComparison.Ordinal);
    }

    private static string LocalizeStateForDisplay(string state) => state switch
    {
        "Pass" => L("통과", "Pass"),
        "Warning" => L("경고", "Warning"),
        "Error" => L("오류", "Error"),
        "Mismatch" => L("불일치", "Mismatch"),
        "Unavailable" => L("사용할 수 없음", "Unavailable"),
        "Available" => L("사용 가능", "Available"),
        "Stale" => L("오래됨", "Stale"),
        "Invalid" => L("유효하지 않음", "Invalid"),
        _ => state
    };

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

    private void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string L(string korean, string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean;
}

internal sealed record RunRecordComparisonProjection(
    RunRecordComparisonState State,
    string Summary,
    string Details);

public sealed class RecipeRunHistoryItem
{
    public RecipeRunHistoryItem(
        string runTime,
        string status,
        string keyMetricSummary,
        string evidenceState,
        string reportPath)
    {
        RunTime = runTime;
        Status = status;
        KeyMetricSummary = keyMetricSummary;
        EvidenceState = evidenceState;
        ReportPath = reportPath;
    }

    public string RunTime { get; }

    public string Status { get; }

    public string KeyMetricSummary { get; }

    public string EvidenceState { get; }

    public string ReportPath { get; }
}

public sealed class InspectionStepItem
{
    public InspectionStepItem(
        string order,
        string stage,
        string status,
        string evidence,
        string timing = "")
    {
        Order = order;
        Stage = stage;
        Status = status;
        Evidence = evidence;
        Timing = timing;
    }

    public string Order { get; }

    public string Stage { get; }

    public string Status { get; }

    public string Timing { get; }

    public string Evidence { get; }
}

public sealed record InspectionThresholdCorrectionItem(
    string Label,
    string Evidence);

public sealed record RunRecordRecentItem(
    string Path,
    string Name,
    string RecordedAt,
    string Status,
    int StepCount,
    bool IsAvailable)
{
    public ResultsRunSnapshot? Snapshot { get; init; }
    public string SourceName => Snapshot?.SourceName ?? Name;
}

public sealed class EvidenceArtifactOpenRequestEventArgs : EventArgs
{
    public EvidenceArtifactOpenRequestEventArgs(string label, string path)
    {
        Label = label;
        Path = path;
    }

    public string Label { get; }

    public string Path { get; }
}
