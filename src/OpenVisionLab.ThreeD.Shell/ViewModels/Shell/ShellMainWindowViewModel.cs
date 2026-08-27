using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.ViewModels.Integration;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell;

public enum ShellWorkspaceMode
{
    Workbench,
    Teach,
    Inspect,
    Review,
    Calibrate,
    Expert,
    Exchange
}

public enum ShellInspectionTask
{
    Thickness,
    Warpage
}

public sealed class ShellMainWindowViewModel : INotifyPropertyChanged
{
    private readonly RelayCommand selectWorkspaceCommand;
    private readonly RelayCommand openSelectedValidationIssueInTeachCommand;
    private bool c3DSampleVisible;
    private readonly string? comparisonContractPath;
    private readonly string? comparisonReportPath;
    private readonly string? shellScreenshotPath;
    private readonly string? runRecordPath;
    private readonly string? htmlReportPath;
    private readonly string? csvReportPath;
    private readonly string recentRunRecordsPath;
    private readonly string? orderedRunRecordRoot;
    private readonly bool hasStartupEvidenceOverrides;
    private bool startupEvidenceActive;
    private string? currentContractPath;
    private string? currentReportPath;
    private string? currentShellScreenshotPath;
    private string? currentRunRecordPath;
    private string? currentHtmlReportPath;
    private string? currentCsvReportPath;
    private string statusText = "Viewer hosted";
    private string recipeComparisonSummary = "No recipe comparison evidence loaded.";
    private string recipeComparisonHistory = "(pending)";
    private string recipeComparisonDetails = "(pending)";
    private string runSnapshotSummary = "No run snapshot evidence loaded.";
    private string runSnapshotEvidence = "(pending)";
    private string inspectionStepSummary = "No inspection steps loaded.";
    private string thresholdCorrectionState = "Unavailable";
    private string thresholdCorrectionSummary =
        "No threshold-correction evidence was recorded.";
    private string sourceQualityState = "Unavailable";
    private string sourceQualitySummary =
        "No Source Quality evidence was recorded.";
    private string sourceQualityDetail =
        "No Source Quality evidence was recorded.";
    private int selectedEvidenceTabIndex;
    private ShellWorkspaceMode selectedWorkspaceMode = ShellWorkspaceMode.Workbench;
    private ShellInspectionTask selectedInspectionTask = ShellInspectionTask.Thickness;
    private readonly IReadOnlyList<OpenVisionLanguageOption> languageOptions;
    private OpenVisionLanguageOption? selectedLanguageOption;
    private double lastLanguageChangeMilliseconds;
    private RunRecordRecentItem? selectedRecentRunRecord;
    private static readonly JsonSerializerOptions RunRecordJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

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
        string? integrationSettingsPath = null,
        Func<IntegrationApplicationIdentity>? integrationProducerIdentityProvider = null)
    {
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
        this.recentRunRecordsPath = recentRunRecordsPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenVisionLab",
                "ThreeDStudio",
                "recent-run-records.json");
        languageOptions = OpenVisionLanguageService.GetLanguageOptions();
        selectedLanguageOption = languageOptions.FirstOrDefault(option => option.Language == OpenVisionLanguageService.CurrentLanguage)
            ?? languageOptions[0];
        ResultsWorkspace = new ResultsWorkspaceViewModel();
        Workbench = new ToolWorkbenchViewModel(recentRecipesPath);
        IntegrationExchange = new ThreeDIntegrationViewModel(
            () => currentRunRecordPath,
            integrationSettingsPath,
            integrationProducerIdentityProvider);
        InspectionSteps.CollectionChanged += (_, _) =>
            RaisePropertyChanged(nameof(ResultsOperatorAffectedStepsSummary));
        Calibration = new CalibrationCenterViewModel();
        selectWorkspaceCommand = new RelayCommand(
            parameter => SelectWorkspace(parameter),
            CanSelectWorkspace);
        SelectWorkspaceCommand = selectWorkspaceCommand;
        openSelectedValidationIssueInTeachCommand = new RelayCommand(
            _ => OpenSelectedValidationIssueInTeach(),
            _ => CanOpenSelectedValidationIssueInTeach());
        OpenSelectedValidationIssueInTeachCommand =
            openSelectedValidationIssueInTeachCommand;
        Workbench.PropertyChanged += OnWorkbenchNavigationStateChanged;
        Workbench.OrderedRunCompleted += OnWorkbenchOrderedRunCompleted;
        Workbench.OrderedRunInvalidated += OnWorkbenchOrderedRunInvalidated;
        OpenVisionLanguageService.LanguageChanged += (_, _) => RefreshLocalizedPresentation();
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
        ExportRunRecordCommand = new RelayCommand(
            _ => ExportRunRecordRequested?.Invoke(this, EventArgs.Empty),
            _ => !string.IsNullOrWhiteSpace(currentRunRecordPath));
        ExportPrivacySafeSupportBundleCommand = new RelayCommand(
            _ => ExportPrivacySafeSupportBundleRequested?.Invoke(this, EventArgs.Empty),
            _ => !string.IsNullOrWhiteSpace(currentRunRecordPath));
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
    public ICommand ExportRunRecordCommand { get; }
    public ICommand ExportPrivacySafeSupportBundleCommand { get; }
    public ToolWorkbenchViewModel Workbench { get; }
    public ResultsWorkspaceViewModel ResultsWorkspace { get; }
    public CalibrationCenterViewModel Calibration { get; }
    public ThreeDIntegrationViewModel IntegrationExchange { get; }
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
        string[] propertyNames =
        [
            nameof(WorkspaceSummary),
            nameof(StatusText),
            nameof(RecipeComparisonSummary),
            nameof(RecipeComparisonHistory),
            nameof(RecipeComparisonDetails),
            nameof(RunSnapshotSummary),
            nameof(RunSnapshotEvidence),
            nameof(InspectionStepSummary),
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
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Workbench;
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

    public bool IsAuthoringWorkspaceSelected =>
        SelectedWorkspaceMode is ShellWorkspaceMode.Workbench or ShellWorkspaceMode.Teach;

    public bool IsWorkbenchWorkspaceSelected
    {
        get => SelectedWorkspaceMode is ShellWorkspaceMode.Workbench
            or ShellWorkspaceMode.Teach
            or ShellWorkspaceMode.Inspect
            or ShellWorkspaceMode.Review;
        set
        {
            if (value)
            {
                TrySelectWorkspace(ShellWorkspaceMode.Workbench);
            }
        }
    }

    public bool IsInspectionWorkspaceSelected =>
        SelectedWorkspaceMode is ShellWorkspaceMode.Workbench
            or ShellWorkspaceMode.Teach
            or ShellWorkspaceMode.Inspect
            or ShellWorkspaceMode.Review;

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
        get => IsInspectWorkspaceSelected;
        set => IsInspectWorkspaceSelected = value;
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
        ShellWorkspaceMode.Teach => L("티칭 | 데이터, 영역, 파라미터와 검출 결과를 확인합니다", "Teach | Configure data, regions, parameters, and detection"),
        ShellWorkspaceMode.Inspect => L("검증 | 샘플 실행과 실패 근거를 검토합니다", "Validate | Replay samples and inspect failures"),
        ShellWorkspaceMode.Review => L("결과 | 실행 기록과 출력 증거를 검토합니다", "Results | Review run records and output evidence"),
        ShellWorkspaceMode.Calibrate => L("교정 작업공간 | 오프라인 데이터셋", "Calibration workspace | Offline datasets"),
        ShellWorkspaceMode.Expert => L("고급 작업공간 | 전체 검사 레이아웃", "Expert workspace | Full inspection layout"),
        ShellWorkspaceMode.Exchange => L("Machine Studio 연동 | 명시적 Handoff 검토와 결과 게시", "Machine Studio exchange | Explicit handoff review and result publishing"),
        _ => L("검사 작업공간", "Inspection workspace")
    };

    public string InspectionStageNavigationStatus =>
        Workbench.IsSelectionCandidateActive
            ? L("ROI 검토를 적용하거나 취소한 후 화면을 이동하세요.", "Apply or cancel the ROI review before changing stages.")
            : Workbench.HasPendingStepParameterChanges
                ? L("파라미터 초안을 적용하거나 취소한 후 화면을 이동하세요.", "Apply or discard the parameter draft before changing stages.")
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
        private set
        {
            if (SetField(ref inspectionStepSummary, value))
            {
                RaisePropertyChanged(nameof(ResultsOperatorDecisionSummary));
            }
        }
    }

    public string ResultsOperatorDecisionSummary => InspectionStepSummary;

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

    public string ThresholdCorrectionState
    {
        get => thresholdCorrectionState;
        private set => SetField(ref thresholdCorrectionState, value);
    }

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
        set => SetField(ref selectedRecentRunRecord, value);
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

        SelectedWorkspaceMode = mode;
        if (mode == ShellWorkspaceMode.Exchange)
        {
            IntegrationExchange.SyncRunRecord();
        }
    }

    private bool CanOpenSelectedValidationIssueInTeach() =>
        Workbench.SelectedValidationSetStep is { StepId.Length: > 0 } selected
        && Workbench.PipelineSteps.Any(
            step => string.Equals(
                step.Id,
                selected.StepId,
                StringComparison.OrdinalIgnoreCase))
        && CanSelectWorkspace(ShellWorkspaceMode.Teach);

    private void OpenSelectedValidationIssueInTeach()
    {
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
        private set => SetField(ref sourceQualityState, value);
    }

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
        try
        {
            var artifact = ShellOrderedRunRecordWriter.Write(
                args.RecipePath,
                args.Document,
                args.SourcePath,
                args.Execution,
                orderedRunRecordRoot);
            Workbench.AttachOrderedRunRecord(artifact.JsonPath);
            startupEvidenceActive = false;
            RefreshRecipeComparison(
                artifact.JsonPath,
                useStartupOverrides: false);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException
            or JsonException)
        {
            Workbench.ReportOrderedRunRecordFailure(exception.Message);
            StatusText = L(
                $"현재 레시피 실행은 완료되었지만 Run Record 저장에 실패했습니다: {exception.Message}",
                $"The current recipe Run completed, but its Run Record could not be saved: {exception.Message}");
        }
    }

    private void OnWorkbenchOrderedRunInvalidated(object? sender, EventArgs args) =>
        ClearCurrentRunEvidenceForRecipeContext();

    public void SetViewerSmokeFailed(string viewerStatus)
    {
        var root = ResolveWorkspaceRoot();
        currentContractPath = null;
        currentReportPath = null;
        currentShellScreenshotPath = ResolveOptionalPath(root, shellScreenshotPath);
        currentRunRecordPath = ResolveOptionalPath(root, runRecordPath);
        currentHtmlReportPath = ResolveOptionalPath(root, htmlReportPath);
        currentCsvReportPath = ResolveOptionalPath(root, csvReportPath);
        RefreshCommandCanExecute();

        StatusText = "Viewer hosted | viewer smoke failed";
        RecipeComparisonSummary = string.IsNullOrWhiteSpace(viewerStatus)
            ? "Viewer smoke failed before recipe comparison."
            : $"Viewer smoke failed before recipe comparison.\n{viewerStatus}";
        RecipeComparisonHistory = "No recipe comparison was run for this failed viewer smoke.";
        RecipeComparisonDetails = "See Tool / Inspector and Viewer contract output for the loader failure details.";
        RunSnapshotSummary = "Viewer smoke failed | Status: ViewerFailed | Key metric: No recipe metric | Evidence: Blocked";
        RunSnapshotEvidence = $"Shell: {FormatShellScreenshotTarget(root)} | Runner: not created | UI: viewer smoke output";
        InspectionStepSummary = "Viewer smoke: Failed";
        RefreshSourceQuality(null);
        RefreshThresholdCorrection(null);
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
        SelectedRecentRunRecord = null;
        RefreshCommandCanExecute();

        RecipeComparisonSummary = L(
            "현재 레시피와 소스의 Run Record가 없습니다.",
            "No Run Record is loaded for the current recipe and source.");
        RecipeComparisonHistory = L(
            "현재 레시피를 실행하거나 호환되는 최근 Run Record를 명시적으로 여세요.",
            "Run the current recipe or explicitly open a compatible recent Run Record.");
        RecipeComparisonDetails = L(
            "최근 Run Record 목록은 유지되지만 자동으로 선택되지 않습니다.",
            "Recent Run Records remain available but are never selected automatically.");
        RunSnapshotSummary = L(
            "현재 레시피의 실행 스냅샷이 없습니다.",
            "No current recipe run snapshot is loaded.");
        RunSnapshotEvidence = L(
            "명시적으로 실행하거나 기록을 열기 전에는 현재 레시피/소스 증거가 없습니다.",
            "Current recipe/source evidence is unavailable until an explicit run or record open.");
        InspectionStepSummary = L(
            "현재 레시피의 Run Record가 없습니다.",
            "No current recipe Run Record is loaded.");
        InspectionSteps.Clear();
        RecipeRunHistory.Clear();
        RefreshSourceQuality(null);
        RefreshThresholdCorrection(null);
        StatusText = "Viewer hosted | current recipe evidence cleared";
    }

    private void RefreshRecipeComparison(string? selectedRunRecordPath, bool useStartupOverrides)
    {
        var root = ResolveWorkspaceRoot();
        currentRunRecordPath = ResolveOptionalPath(root, selectedRunRecordPath);
        var runRecord = ReadRunRecord(currentRunRecordPath);
        var contractPath = ResolvePath(
            root,
            (useStartupOverrides ? comparisonContractPath : null) ?? runRecord?.Artifacts.ViewerContract,
            Path.Combine(root, "artifacts", "shell_recipe_ui_after.txt"));
        var reportPath = ResolvePath(
            root,
            (useStartupOverrides ? comparisonReportPath : null) ?? runRecord?.Artifacts.RunnerTextReport,
            Path.Combine(root, "artifacts", "runner_shell_recipe_ui_compare_after.txt"));
        currentContractPath = contractPath;
        currentReportPath = reportPath;
        currentShellScreenshotPath = ResolveOptionalPath(root, (useStartupOverrides ? shellScreenshotPath : null) ?? runRecord?.Artifacts.ViewerScreenshot);
        currentHtmlReportPath = ResolveOptionalPath(root, (useStartupOverrides ? htmlReportPath : null) ?? runRecord?.Artifacts.HtmlReport);
        currentCsvReportPath = ResolveOptionalPath(root, (useStartupOverrides ? csvReportPath : null) ?? runRecord?.Artifacts.CsvReport);
        RefreshCommandCanExecute();

        if (runRecord?.ViewerRunnerMatchState
            == ShellOrderedRunRecordWriter.EvidenceState)
        {
            RefreshShellOrderedRunRecord(root, runRecord);
            return;
        }

        var contractLines = ReadLinesOrEmpty(contractPath);
        var reportLines = ReadLinesOrEmpty(reportPath);
        var recipePath = ExtractRecipePath(root, reportLines);
        var uiEvidence = ExtractUiEvidence(contractLines);
        var runnerEvidence = ExtractRunnerEvidence(reportLines);
        var comparisonState = uiEvidence.Matches(runnerEvidence)
            ? "Runner/UI contract matched"
            : "Comparison evidence missing or different";

        RecipeComparisonSummary =
            $"{comparisonState}\nUI: {uiEvidence.ToolName} / {uiEvidence.Status}\nRunner: {runnerEvidence.ToolName} / {runnerEvidence.Status}\nUI metric: {uiEvidence.KeyMetricSummary}\nRunner metric: {runnerEvidence.KeyMetricSummary}";
        RecipeComparisonHistory =
            $"Recipe: {FormatEvidencePath(root, recipePath)}\nUI contract: {FormatEvidencePath(root, contractPath)}\nRunner report: {FormatEvidencePath(root, reportPath)}";
        RecipeComparisonDetails =
            $"{PreviewLines(root, "Runner report", reportPath, reportLines)}\n\n{PreviewLines(root, "UI contract", contractPath, contractLines)}";
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
            : $"{metric.Name} {metric.Value:G6} {metric.Unit}".TrimEnd();
        RecipeComparisonSummary = L(
            $"Studio 명시적 순차 실행 · {record.Status} · {steps.Count}개 단계\n동일 ordered graph 엔진으로 실행했으며 Preview/Publish 상태는 변경하지 않았습니다.",
            $"Explicit Studio ordered Run · {record.Status} · {steps.Count} step(s)\nExecuted by the same ordered graph engine without changing Preview or Publish state.");
        RecipeComparisonHistory = L(
            $"레시피: {FormatEvidencePath(root, record.Recipe.Path)}\n소스: {FormatEvidencePath(root, record.Source.Path)}\nRun Record: {FormatEvidencePath(root, currentRunRecordPath ?? string.Empty)}",
            $"Recipe: {FormatEvidencePath(root, record.Recipe.Path)}\nSource: {FormatEvidencePath(root, record.Source.Path)}\nRun Record: {FormatEvidencePath(root, currentRunRecordPath ?? string.Empty)}");
        RecipeComparisonDetails = string.Join(
            Environment.NewLine,
            steps.Select(step =>
                $"{step.RecipeIndex + 1:D2} | {step.Id} | {step.ToolName} | {step.Status} | {step.OutputEntityId} | SHA-256 {step.OutputContentSha256 ?? "(none)"}"));
        RunSnapshotSummary = L(
            $"현재 레시피 실행 · 상태 {record.Status} · 핵심 측정값 {metricSummary} · {record.ElapsedMilliseconds:F3} ms",
            $"Current recipe Run · {record.Status} · key metric {metricSummary} · {record.ElapsedMilliseconds:F3} ms");
        RunSnapshotEvidence =
            $"Recipe: {FormatShortEvidencePath(root, record.Recipe.Path)} | Source: {FormatShortEvidencePath(root, record.Source.Path)} | JSON: {FormatOptionalArtifact(root, currentRunRecordPath)} | Report: {FormatOptionalArtifact(root, currentReportPath)}";
        RefreshInspectionStepsFromRecord(record);
        RecipeRunHistory.Clear();
        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            record.RecordedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
            record.Status.ToString(),
            metricSummary,
            ShellOrderedRunRecordWriter.EvidenceState,
            FormatShortEvidencePath(root, currentRunRecordPath ?? string.Empty)));
        if (currentRunRecordPath is not null)
        {
            RecordRecentRunRecord(currentRunRecordPath, record);
        }
        StatusText = L(
            $"Viewer hosted | 현재 레시피 실행 {record.Status}",
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
        var status = SelectEvidenceStatus(uiEvidence, runnerEvidence);
        var keyMetricSummary = SelectEvidenceMetric(uiEvidence, runnerEvidence);
        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";

        RunSnapshotSummary =
            $"{comparisonState} | Status: {status} | Key metric: {keyMetricSummary} | Evidence: {evidenceState} | Run: {FormatRunTime(reportPath, contractPath)}";
        RunSnapshotEvidence =
            $"Recipe: {FormatShortEvidencePath(root, recipePath)} | UI: {FormatShortEvidencePath(root, contractPath)} | Runner: {FormatShortEvidencePath(root, reportPath)} | Shell: {FormatShellScreenshotTarget(root)} | JSON: {FormatOptionalArtifact(root, currentRunRecordPath)} | HTML: {FormatOptionalArtifact(root, currentHtmlReportPath)} | CSV: {FormatOptionalArtifact(root, currentCsvReportPath)}";
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
            var enabled = ExtractTaggedValue(parts, "enabled=");
            var status = enabled?.Equals("False", StringComparison.OrdinalIgnoreCase) == true ? "Disabled" : uiEvidence.Status;
            var tool = ExtractTaggedValue(parts, "tool=") ?? "Inspection step";
            var id = ExtractTaggedValue(parts, "id=") ?? "(missing ID)";
            var source = ExtractTaggedValue(parts, "source=") ?? "(missing source)";
            var reference = ExtractTaggedValue(parts, "reference=") ?? "(missing reference)";
            InspectionSteps.Add(new InspectionStepItem(
                (order++).ToString(CultureInfo.InvariantCulture),
                tool,
                status,
                $"{id} | source {source} | reference {reference}"));
        }

        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Recipe", File.Exists(recipePath) ? "Loaded" : "Missing", FormatShortEvidencePath(root, recipePath)));
        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Source", ExtractSourceLoadStatus(reportLines), ExtractSourceSummary(root, reportLines, contractLines)));

        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Viewer preview", uiEvidence.Status, $"{uiEvidence.ToolName} | {uiEvidence.KeyMetricSummary}"));
        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Runner replay", runnerEvidence.Status, $"{runnerEvidence.ToolName} | {runnerEvidence.KeyMetricSummary}"));
        InspectionSteps.Add(new InspectionStepItem(order.ToString(CultureInfo.InvariantCulture), "Evidence compare", evidenceState, $"{comparisonState} | UI {FormatShortEvidencePath(root, contractPath)} | Runner {FormatShortEvidencePath(root, reportPath)}"));

        InspectionStepSummary = recipeSteps.Length == 0
            ? $"Recipe: {InspectionSteps[0].Status} | Source: {InspectionSteps[1].Status} | Viewer: {uiEvidence.Status} | Runner: {runnerEvidence.Status} | Compare: {evidenceState}"
            : $"Recipe steps: {recipeSteps.Length} | Viewer: {uiEvidence.Status} | Runner: {runnerEvidence.Status} | Compare: {evidenceState}";
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
            var metricSummary = metric is null
                ? "no metrics"
                : $"{metric.Name} {metric.Value:G6} {metric.Unit}";
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
            $"{timing.TotalElapsedMilliseconds:F3} ms · {string.Join("; ", timing.Stages.Select(stage => $"{stage.StageId} {stage.ElapsedMilliseconds:F3} ms"))}");
    }

    private void RefreshSourceQuality(InspectionRunRecord? runRecord)
    {
        var evidence = runRecord?.SourceQualityEvidence;
        if (evidence is null)
        {
            SourceQualityState = "Unavailable";
            SourceQualitySummary = runRecord is null
                ? L(
                    "Run Record가 로드되지 않았습니다.",
                    "No Run Record is loaded.")
                : L(
                    "이전 Run Record에는 소스 품질 증거가 없습니다.",
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
                    ? "No Run Record is loaded."
                    : "This legacy Run Record did not record threshold-correction evidence.";
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

        var status = SelectEvidenceStatus(uiEvidence, runnerEvidence);
        var keyMetricSummary = SelectEvidenceMetric(uiEvidence, runnerEvidence);
        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";

        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            FormatRunTime(reportPath, contractPath),
            status,
            keyMetricSummary,
            evidenceState,
            FormatShortEvidencePath(root, reportPath)));
    }

    private static string SelectEvidenceStatus(ToolComparisonEvidence uiEvidence, ToolComparisonEvidence runnerEvidence) =>
        runnerEvidence.Status != "(missing)" ? runnerEvidence.Status : uiEvidence.Status;

    private static string SelectEvidenceMetric(ToolComparisonEvidence uiEvidence, ToolComparisonEvidence runnerEvidence) =>
        runnerEvidence.KeyMetricSummary != "(missing)" ? runnerEvidence.KeyMetricSummary : uiEvidence.KeyMetricSummary;

    private static string ResolveWorkspaceRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenVisionLab.ThreeDStudio.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string ResolvePath(string root, string? requestedPath, string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return fallbackPath;
        }

        return Path.IsPathRooted(requestedPath)
            ? requestedPath
            : Path.Combine(root, requestedPath);
    }

    private static string? ResolveOptionalPath(string root, string? requestedPath) =>
        string.IsNullOrWhiteSpace(requestedPath)
            ? null
            : Path.IsPathRooted(requestedPath)
                ? requestedPath
                : Path.Combine(root, requestedPath);

    private static string[] ReadLinesOrEmpty(string path) =>
        File.Exists(path) ? File.ReadAllLines(path) : [];

    private static InspectionRunRecord? ReadRunRecord(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<InspectionRunRecord>(File.ReadAllText(path), RunRecordJsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public bool LoadRunRecord(string path, out string message)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var record = ReadRunRecord(fullPath);
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

    public bool ExportCurrentRunRecordBundle(string targetRoot, out string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentRunRecordPath) || !File.Exists(currentRunRecordPath))
            {
                message = Localization.RunRecordOpenFailed;
                return false;
            }

            var record = ReadRunRecord(currentRunRecordPath);
            if (record is null)
            {
                message = Localization.RunRecordOpenFailed;
                return false;
            }

            var safeRunId = string.Concat(record.RunId.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            var exportDirectory = Path.Combine(Path.GetFullPath(targetRoot), $"RunRecord-{safeRunId}");
            for (var suffix = 2; Directory.Exists(exportDirectory); suffix++)
            {
                exportDirectory = Path.Combine(Path.GetFullPath(targetRoot), $"RunRecord-{safeRunId}-{suffix}");
            }

            Directory.CreateDirectory(exportDirectory);
            foreach (var sourcePath in new[] { currentRunRecordPath, currentHtmlReportPath, currentCsvReportPath }
                         .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)))
            {
                var source = sourcePath!;
                File.Copy(source, Path.Combine(exportDirectory, Path.GetFileName(source)), overwrite: false);
            }

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

            var outputPath = PrivacySafeSupportBundleWriter.Write(
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
            foreach (var path in RecipeRecentFileStore.Load(recentRunRecordsPath))
            {
                RecentRunRecords.Add(CreateRecentRunRecordItem(path, ReadRunRecord(path)));
            }

            SelectedRecentRunRecord = null;
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
        while (RecentRunRecords.Count > RecipeRecentFileStore.MaximumEntries)
        {
            RecentRunRecords.RemoveAt(RecentRunRecords.Count - 1);
        }

        SelectedRecentRunRecord = item;
        try
        {
            RecipeRecentFileStore.Save(recentRunRecordsPath, RecentRunRecords.Select(recent => recent.Path));
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
            record is not null);

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

    private static ToolComparisonEvidence ExtractUiEvidence(string[] lines)
    {
        var nominalActual = ExtractNominalActualUiEvidence(lines);
        if (nominalActual is not null)
        {
            return nominalActual;
        }

        var resultLine = FindLineAfterMarker(lines, InspectionContractText.PreviewToolResultMarker);
        var parts = resultLine?.Split('|');
        var metrics = ExtractMetrics(lines, InspectionContractText.PreviewMetricsMarker);
        return new ToolComparisonEvidence(
            parts is { Length: > 0 } ? parts[0] : "(missing)",
            parts is { Length: > 1 } ? parts[1] : "(missing)",
            FormatKeyMetricSummary(metrics));
    }

    private static ToolComparisonEvidence ExtractRunnerEvidence(string[] lines)
    {
        var resultLine = lines.FirstOrDefault(line => line.StartsWith(InspectionContractText.ToolResultPrefix + "|", StringComparison.Ordinal));
        var parts = resultLine?.Split('|');
        var metrics = ExtractMetrics(lines, InspectionContractText.MetricsMarker);
        return new ToolComparisonEvidence(
            parts is { Length: > 1 } ? parts[1] : "(missing)",
            parts is { Length: > 2 } ? parts[2] : "(missing)",
            FormatKeyMetricSummary(metrics));
    }

    private static string ExtractRecipePath(string root, string[] reportLines)
    {
        var recipeLine = reportLines.FirstOrDefault(line => line.StartsWith("Recipe|", StringComparison.Ordinal));
        var path = recipeLine is null ? null : ExtractTaggedValue(recipeLine.Split('|'), "path=");
        return string.IsNullOrWhiteSpace(path)
            ? Path.Combine(root, "recipes", "c3d-height-deviation.recipe.json")
            : ResolvePath(root, path, path);
    }

    private static string ExtractSourceLoadStatus(string[] reportLines) =>
        reportLines.Any(line =>
            line.StartsWith("Source|", StringComparison.Ordinal)
            || line.StartsWith("NominalActualActualSource|", StringComparison.Ordinal))
            ? "Loaded"
            : "Pending";

    private static string ExtractSourceSummary(string root, string[] reportLines, string[] contractLines)
    {
        var actual = reportLines.FirstOrDefault(line =>
            line.StartsWith("NominalActualActualSource|", StringComparison.Ordinal));
        var nominal = reportLines.FirstOrDefault(line =>
            line.StartsWith("NominalActualNominalSource|", StringComparison.Ordinal));
        var query = reportLines.FirstOrDefault(line =>
            line.StartsWith("NominalActualQuerySource|", StringComparison.Ordinal));
        if (actual is not null && nominal is not null && query is not null)
        {
            return string.Join(
                " | ",
                FormatNominalActualSource(root, "actual", actual),
                FormatNominalActualSource(root, "nominal", nominal),
                FormatNominalActualSource(root, "query", query));
        }

        var sourceLine = reportLines.FirstOrDefault(line => line.StartsWith("Source|", StringComparison.Ordinal));
        if (sourceLine is not null)
        {
            var parts = sourceLine.Split('|');
            var name = ExtractTaggedValue(parts, "name=") ?? (parts.Length > 1 ? parts[1] : "source");
            var unit = ExtractTaggedValue(parts, "unit=") ?? "(unit unknown)";
            var path = ExtractTaggedValue(parts, "path=");
            var shortPath = string.IsNullOrWhiteSpace(path) ? "(path unknown)" : ShortenWorkspacePaths(root, path);
            return $"{name} | unit {unit} | {shortPath}";
        }

        var sourceEntitiesIndex = Array.FindIndex(contractLines, line => line.Equals("SourceEntities", StringComparison.Ordinal));
        if (sourceEntitiesIndex >= 0 && sourceEntitiesIndex + 1 < contractLines.Length)
        {
            return ShortenWorkspacePaths(root, contractLines[sourceEntitiesIndex + 1]);
        }

        return "No source evidence found.";
    }

    private static ToolComparisonEvidence? ExtractNominalActualUiEvidence(string[] lines)
    {
        var resultLine = lines.FirstOrDefault(line =>
            line.StartsWith("NominalActualResult|", StringComparison.Ordinal));
        var statisticsLine = lines.FirstOrDefault(line =>
            line.StartsWith("NominalActualSignedStatistics|", StringComparison.Ordinal));
        if (resultLine is null || statisticsLine is null)
        {
            return null;
        }

        var resultParts = resultLine.Split('|');
        if (!string.Equals(
                ExtractTaggedValue(resultParts, "available="),
                "True",
                StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(
                ExtractTaggedValue(resultParts, "below="),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var below)
            || !long.TryParse(
                ExtractTaggedValue(resultParts, "above="),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var above))
        {
            return null;
        }

        var statisticsParts = statisticsLine.Split('|');
        if (!double.TryParse(
                ExtractTaggedValue(statisticsParts, "mean="),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var signedMean))
        {
            return null;
        }

        var unit = ExtractTaggedValue(statisticsParts, "unit=") ?? "(unitless)";
        return new ToolComparisonEvidence(
            NominalActualComparisonContract.ToolName,
            ExtractTaggedValue(resultParts, "status=") ?? "(missing)",
            string.Create(
                CultureInfo.InvariantCulture,
                $"Signed mean deviation {signedMean:F3} {unit} | Out-of-tolerance point count {below + above:F3} count"));
    }

    private static string FormatNominalActualSource(
        string root,
        string role,
        string line)
    {
        var parts = line.Split('|');
        var id = ExtractTaggedValue(parts, "id=") ?? "(missing ID)";
        var path = ExtractTaggedValue(parts, "path=");
        return $"{role} {id} ({(string.IsNullOrWhiteSpace(path) ? "path unknown" : ShortenWorkspacePaths(root, path))})";
    }

    private static string? FindLineAfterMarker(string[] lines, string marker)
    {
        var index = Array.FindIndex(lines, line => line.Equals(marker, StringComparison.Ordinal));
        if (index < 0)
        {
            return null;
        }

        for (var i = index + 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                return lines[i];
            }
        }

        return null;
    }

    private static List<MetricEvidence> ExtractMetrics(string[] lines, string marker)
    {
        var index = Array.FindIndex(lines, line => line.Equals(marker, StringComparison.Ordinal));
        if (index < 0)
        {
            return [];
        }

        var metrics = new List<MetricEvidence>();
        for (var i = index + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('|');
            var value = ExtractTaggedValue(parts, "value=");
            if (parts.Length < 3 || value is null)
            {
                break;
            }

            metrics.Add(new MetricEvidence(
                parts[0],
                value,
                ExtractTaggedValue(parts, "unit=") ?? "(unitless)",
                ExtractTaggedValue(parts, "status=") ?? "(none)"));
        }

        return metrics;
    }

    private static string? ExtractTaggedValue(string[] parts, string prefix) =>
        parts.FirstOrDefault(part => part.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

    private static string FormatKeyMetricSummary(IReadOnlyList<MetricEvidence> metrics)
    {
        var signedMean = metrics.FirstOrDefault(metric =>
            metric.Name.Equals("Signed mean deviation", StringComparison.Ordinal));
        var outOfTolerance = metrics.FirstOrDefault(metric =>
            metric.Name.Equals("Out-of-tolerance point count", StringComparison.Ordinal));
        if (signedMean is not null && outOfTolerance is not null)
        {
            return $"Signed mean deviation {FormatMetricValue(signedMean)} | Out-of-tolerance point count {FormatMetricValue(outOfTolerance)}";
        }

        var peakDeviation = metrics.FirstOrDefault(metric => metric.Name.Equals("Peak absolute deviation", StringComparison.Ordinal));
        if (peakDeviation is not null)
        {
            return $"Peak {FormatMetricValue(peakDeviation)}";
        }

        var distance = metrics.FirstOrDefault(metric => metric.Name.Equals("Distance", StringComparison.Ordinal));
        var heightDelta = metrics.FirstOrDefault(metric => metric.Name.Equals("Source Z height delta", StringComparison.Ordinal));
        if (distance is not null && heightDelta is not null)
        {
            return $"Distance {FormatMetricValue(distance)} | Height {FormatMetricValue(heightDelta)}";
        }

        var fallback = metrics.FirstOrDefault();
        return fallback is null ? "(missing)" : $"{fallback.Name} {FormatMetricValue(fallback)}";
    }

    private static string FormatMetricValue(MetricEvidence metric) =>
        string.IsNullOrWhiteSpace(metric.Unit) || metric.Unit.Equals("(unitless)", StringComparison.Ordinal)
            ? metric.Value
            : $"{metric.Value} {metric.Unit}";

    private static string FormatEvidencePath(string root, string path)
    {
        var displayPath = Path.GetRelativePath(root, path);
        if (!File.Exists(path))
        {
            return $"missing: {displayPath}";
        }

        var timestamp = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return $"{displayPath}\n  modified: {timestamp}";
    }

    private static string FormatShortEvidencePath(string root, string path) =>
        File.Exists(path) ? Path.GetRelativePath(root, path) : $"missing: {Path.GetRelativePath(root, path)}";

    private static string FormatOptionalArtifact(string root, string? path) =>
        string.IsNullOrWhiteSpace(path) ? "(not requested)" : FormatShortEvidencePath(root, path);

    private string FormatShellScreenshotTarget(string root)
    {
        if (string.IsNullOrWhiteSpace(shellScreenshotPath))
        {
            return "(not requested)";
        }

        var path = Path.IsPathRooted(shellScreenshotPath)
            ? shellScreenshotPath
            : Path.Combine(root, shellScreenshotPath);
        return Path.GetRelativePath(root, path);
    }

    private static string FormatRunTime(string reportPath, string contractPath)
    {
        var evidencePath = File.Exists(reportPath) ? reportPath : contractPath;
        if (!File.Exists(evidencePath))
        {
            return "(pending)";
        }

        return File.GetLastWriteTime(evidencePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string PreviewLines(string root, string title, string path, string[] lines)
    {
        var displayPath = FormatShortEvidencePath(root, path);
        if (lines.Length == 0)
        {
            return $"{title}: missing or empty: {displayPath}";
        }

        return $"{title}: {displayPath}\n{string.Join(Environment.NewLine, lines.Take(18).Select(line => ShortenWorkspacePaths(root, line)))}";
    }

    private static string ShortenWorkspacePaths(string root, string text)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return text
            .Replace(normalizedRoot + Path.DirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(normalizedRoot + Path.AltDirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase);
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

    private void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string L(string korean, string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean;
}

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
    bool IsAvailable);

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

internal sealed record ToolComparisonEvidence(string ToolName, string Status, string KeyMetricSummary)
{
    public bool Matches(ToolComparisonEvidence other) =>
        !Status.Equals("(missing)", StringComparison.Ordinal)
        && ToolName.Equals(other.ToolName, StringComparison.Ordinal)
        && Status.Equals(other.Status, StringComparison.Ordinal)
        && KeyMetricSummary.Equals(other.KeyMetricSummary, StringComparison.Ordinal);
}

internal sealed record MetricEvidence(string Name, string Value, string Unit, string Status);
