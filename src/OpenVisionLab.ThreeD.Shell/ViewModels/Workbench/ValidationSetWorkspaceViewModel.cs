using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Validation Set workspace: sample staging and roles, explicit run,
/// result review, threshold correction, localization, persistence, and lifetime.
/// Workbench supplies recipe reads and handles comparison and catalog requests.
/// </summary>
internal sealed class ValidationSetWorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string> getRecipeName;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string> getSourcePath;
    private readonly Func<bool> isSourceReady;
    private readonly IReadOnlyList<ToolWorkbenchPipelineStepItem> pipelineSteps;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<ToolWorkbenchPipelineStepItem, bool> trySelectPipelineStep;
    private readonly ToolWorkbenchStepPropertySession stepPropertySession;
    private readonly Action<string, string> appendLog;
    private readonly Func<string, string, string> localize;
    private readonly Func<ResultStatus, string> localizeStatus;
    private readonly ToolWorkbenchValidationSetExecutionOwner validationSetExecutionOwner;
    private ToolWorkbenchValidationSetEventCoordinator validationSetEventCoordinator = null!;
    private int disposalState;
    private ToolWorkbenchValidationSetDefinitionOwner
        validationSetDefinitionOwner = null!;
    private ToolWorkbenchValidationSetReviewOwner validationSetReviewOwner = null!;
    private ToolWorkbenchValidationThresholdWorkflowOwner
        validationThresholdWorkflowOwner = null!;
    private RelayCommand selectValidationSetSourcesCommand = null!;
    private RelayCommand addCurrentSourceToValidationSetCommand = null!;
    private RelayCommand runValidationSetCommand = null!;
    private RelayCommand clearValidationSetCommand = null!;
    private RelayCommand cancelValidationSetCommand = null!;
    private RelayCommand setValidationSampleRoleCommand = null!;
    private string validationSetSummary = string.Empty;
    private string validationSetCapability = string.Empty;
    private bool isValidationEvidenceExpanded;
    private bool isValidationThresholdExpanded;
    private ValidationFailureCorrectionContext? activeValidationFailureCorrectionContext;

    public ValidationSetWorkspaceViewModel(
        Func<ToolRecipeDocument> createDocument,
        Func<string> getRecipeName,
        Func<string?> getRecipePath,
        Func<string> getSourcePath,
        Func<bool> isSourceReady,
        IReadOnlyList<ToolWorkbenchPipelineStepItem> pipelineSteps,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<ToolWorkbenchPipelineStepItem, bool> trySelectPipelineStep,
        ToolWorkbenchStepPropertySession stepPropertySession,
        Action<string, string> appendLog,
        Func<string, string, string> localize,
        Func<ResultStatus, string> localizeStatus)
    {
        this.createDocument = createDocument;
        this.getRecipeName = getRecipeName;
        this.getRecipePath = getRecipePath;
        this.getSourcePath = getSourcePath;
        this.isSourceReady = isSourceReady;
        this.pipelineSteps = pipelineSteps;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.trySelectPipelineStep = trySelectPipelineStep;
        this.stepPropertySession = stepPropertySession;
        this.appendLog = appendLog;
        this.localize = localize;
        this.localizeStatus = localizeStatus;
        validationSetExecutionOwner = new ToolWorkbenchValidationSetExecutionOwner(RefreshValidationSetExecutionState);
        InitializeValidationSet();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SamplesChanged;
    public event EventHandler? DefinitionDirtyChanged;
    public event EventHandler? ComparePinsClearRequested;
    public event EventHandler<ValidationSetSampleRow>? ComparisonRequested;

    public event EventHandler? SelectValidationSetSourcesRequested;

    internal bool IsDisposed => Volatile.Read(ref disposalState) != 0;
    internal IReadOnlyList<ValidationSetSampleRow> AllSamples => validationSetDefinitionOwner.Samples;

    public ReadOnlyObservableCollection<ValidationSetSampleRow> ValidationSetSamples =>
        validationSetReviewOwner.ValidationSetSamples;

    public ReadOnlyObservableCollection<ValidationSetStepRow> SelectedValidationSetSteps =>
        validationSetReviewOwner.SelectedValidationSetSteps;

    public ReadOnlyObservableCollection<ValidationEvidenceDistributionRow>
        ValidationEvidenceDistributions =>
        validationThresholdWorkflowOwner.ValidationEvidenceDistributions;

    public ReadOnlyObservableCollection<ValidationThresholdCandidateRow>
        ValidationThresholdCandidates =>
        validationThresholdWorkflowOwner.ValidationThresholdCandidates;

    public ReadOnlyObservableCollection<ValidationThresholdDecisionRow>
        SelectedValidationThresholdDecisions =>
        validationThresholdWorkflowOwner.SelectedValidationThresholdDecisions;

    public ReadOnlyObservableCollection<ValidationThresholdParameterChangeRow>
        ValidationThresholdParameterChanges =>
        validationThresholdWorkflowOwner.ValidationThresholdParameterChanges;

    public ReadOnlyObservableCollection<ValidationThresholdHeldOutSampleRow>
        ValidationThresholdHeldOutSamples =>
        validationThresholdWorkflowOwner.ValidationThresholdHeldOutSamples;

    public ReadOnlyObservableCollection<ValidationThresholdDevelopmentSampleRow>
        ValidationThresholdDevelopmentSamples =>
        validationThresholdWorkflowOwner.ValidationThresholdDevelopmentSamples;

    public ValidationSetSampleRow? SelectedValidationSetSample
    {
        get => validationSetReviewOwner.SelectedValidationSetSample;
        set => validationSetReviewOwner.SelectedValidationSetSample = value;
    }

    public ValidationSetStepRow? SelectedValidationSetStep
    {
        get => validationSetReviewOwner.SelectedValidationSetStep;
        set => validationSetReviewOwner.SelectedValidationSetStep = value;
    }

    public bool HasSelectedValidationSetStep =>
        validationSetReviewOwner.HasSelectedValidationSetStep;

    public ValidationFailureCorrectionContext? ActiveValidationFailureCorrectionContext
    {
        get => activeValidationFailureCorrectionContext;
        private set
        {
            if (Equals(activeValidationFailureCorrectionContext, value))
            {
                return;
            }

            activeValidationFailureCorrectionContext = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasActiveValidationFailureCorrectionContext));
        }
    }

    public bool HasActiveValidationFailureCorrectionContext =>
        ActiveValidationFailureCorrectionContext is not null;

    public ValidationSetStatusFilter ValidationSetFilter
    {
        get => validationSetReviewOwner.ValidationSetFilter;
        private set => validationSetReviewOwner.SetFilter(value);
    }

    public bool IsValidationSetFilterAll => validationSetReviewOwner.IsValidationSetFilterAll;
    public bool IsValidationSetFilterPass => validationSetReviewOwner.IsValidationSetFilterPass;
    public bool IsValidationSetFilterFail => validationSetReviewOwner.IsValidationSetFilterFail;
    public bool IsValidationSetFilterError => validationSetReviewOwner.IsValidationSetFilterError;

    public int ValidationSetAllCount => validationSetDefinitionOwner.Samples.Count;
    public int ValidationSetPassCount => validationSetDefinitionOwner.Samples.Count(row => row.Status == "Pass");
    public int ValidationSetFailCount => validationSetDefinitionOwner.Samples.Count(row => row.Status == "Fail");
    public int ValidationSetErrorCount => validationSetDefinitionOwner.Samples.Count(row => row.Status == "Error");
    public int ValidationSetGoodCount => validationSetDefinitionOwner.Samples.Count(row =>
        row.Role == ToolRecipeValidationSampleRole.Good);
    public int ValidationSetBadCount => validationSetDefinitionOwner.Samples.Count(row =>
        row.Role == ToolRecipeValidationSampleRole.Bad);
    public int ValidationSetHeldOutCount => validationSetDefinitionOwner.Samples.Count(row =>
        row.Role == ToolRecipeValidationSampleRole.HeldOut);
    public bool HasValidationSetIssues => validationSetDefinitionOwner.Samples.Any(row =>
        row.Status is "Fail" or "Error");
    public bool IsSelectedValidationRoleGood =>
        validationSetReviewOwner.IsSelectedValidationRoleGood;
    public bool IsSelectedValidationRoleBad =>
        validationSetReviewOwner.IsSelectedValidationRoleBad;
    public bool IsSelectedValidationRoleHeldOut =>
        validationSetReviewOwner.IsSelectedValidationRoleHeldOut;
    public bool HasValidationEvidence =>
        validationThresholdWorkflowOwner.HasValidationEvidence;
    public bool HasValidationThresholdCandidates =>
        validationThresholdWorkflowOwner.HasValidationThresholdCandidates;

    public bool HasValidationThresholdAssistantAnalysis =>
        validationThresholdWorkflowOwner.HasValidationThresholdAssistantAnalysis;

    public bool HasValidationThresholdAssistantProposal =>
        validationThresholdWorkflowOwner.HasValidationThresholdAssistantProposal;

    public ValidationThresholdAssistantStage ValidationThresholdAssistantStage =>
        validationThresholdWorkflowOwner.ValidationThresholdAssistantStage;

    public string ValidationThresholdAssistantStageText =>
        validationThresholdWorkflowOwner.ValidationThresholdAssistantStageText;

    public string ValidationThresholdAssistantSummary =>
        validationThresholdWorkflowOwner.ValidationThresholdAssistantSummary;

    public bool IsValidationEvidenceExpanded
    {
        get => isValidationEvidenceExpanded;
        set
        {
            if (isValidationEvidenceExpanded == value) return;
            isValidationEvidenceExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsValidationSetDefinitionDirty =>
        validationSetDefinitionOwner.IsValidationSetDefinitionDirty;

    public bool IsValidationThresholdExpanded
    {
        get => isValidationThresholdExpanded;
        set
        {
            if (isValidationThresholdExpanded == value) return;
            isValidationThresholdExpanded = value;
            OnPropertyChanged();
        }
    }

    public ValidationThresholdCandidateRow?
        SelectedValidationThresholdCandidate
    {
        get => validationThresholdWorkflowOwner.SelectedValidationThresholdCandidate;
        set => validationThresholdWorkflowOwner.SelectedValidationThresholdCandidate = value;
    }

    public bool HasSelectedValidationThresholdCandidate =>
        validationThresholdWorkflowOwner.HasSelectedValidationThresholdCandidate;

    public bool IsValidationThresholdReviewActive =>
        validationThresholdWorkflowOwner.IsValidationThresholdReviewActive;

    public bool IsValidationThresholdCandidateApplied =>
        validationThresholdWorkflowOwner.IsValidationThresholdCandidateApplied;

    public bool IsValidationThresholdManualCorrectionCommitted =>
        validationThresholdWorkflowOwner.IsValidationThresholdManualCorrectionCommitted;

    public bool IsValidationThresholdDevelopmentValidated =>
        validationThresholdWorkflowOwner.IsValidationThresholdDevelopmentValidated;

    public bool HasValidationThresholdParameterChanges =>
        validationThresholdWorkflowOwner.HasValidationThresholdParameterChanges;

    public bool HasValidationThresholdHeldOutEvidence =>
        validationThresholdWorkflowOwner.HasValidationThresholdHeldOutEvidence;

    public bool HasValidationThresholdDevelopmentEvidence =>
        validationThresholdWorkflowOwner.HasValidationThresholdDevelopmentEvidence;

    public string ValidationThresholdCorrectionSummary =>
        validationThresholdWorkflowOwner.ValidationThresholdCorrectionSummary;

    public string ValidationEvidenceSummary =>
        validationThresholdWorkflowOwner.ValidationEvidenceSummary;
    public string ValidationEvidenceWarning =>
        validationThresholdWorkflowOwner.ValidationEvidenceWarning;
    public string ValidationThresholdSummary =>
        validationThresholdWorkflowOwner.ValidationThresholdSummary;
    public string ValidationThresholdWarning =>
        validationThresholdWorkflowOwner.ValidationThresholdWarning;

    public string ValidationSetSummary
    {
        get => validationSetSummary;
        private set
        {
            if (validationSetSummary == value) return;
            validationSetSummary = value;
            OnPropertyChanged();
        }
    }

    public string ValidationSetCapability
    {
        get => validationSetCapability;
        private set
        {
            if (validationSetCapability == value) return;
            validationSetCapability = value;
            OnPropertyChanged();
        }
    }

    public string ValidationSetProgressText =>
        validationThresholdWorkflowOwner.ValidationSetProgressText;

    public double ValidationSetProgress =>
        validationThresholdWorkflowOwner.ValidationSetProgress;

    public bool IsValidationSetRunning => validationSetExecutionOwner.IsRunning;

    public bool IsValidationSetIdle => !IsValidationSetRunning;

    public bool HasValidationSetSamples => validationSetDefinitionOwner.Samples.Count > 0;

    public bool HasSelectedValidationSetSample =>
        validationSetReviewOwner.HasSelectedValidationSetSample;

    public ICommand SelectValidationSetSourcesCommand => selectValidationSetSourcesCommand;

    public ICommand AddCurrentSourceToValidationSetCommand => addCurrentSourceToValidationSetCommand;

    public ICommand RunValidationSetCommand => runValidationSetCommand;

    public ICommand ClearValidationSetCommand => clearValidationSetCommand;

    public ICommand CancelValidationSetCommand => cancelValidationSetCommand;

    public ICommand SetValidationSetFilterCommand =>
        validationSetReviewOwner.SetValidationSetFilterCommand;

    public ICommand PreviousValidationSetIssueCommand =>
        validationSetReviewOwner.PreviousValidationSetIssueCommand;

    public ICommand NextValidationSetIssueCommand =>
        validationSetReviewOwner.NextValidationSetIssueCommand;

    public ICommand OpenValidationSetComparisonCommand =>
        validationSetReviewOwner.OpenValidationSetComparisonCommand;

    public ICommand SetValidationSampleRoleCommand =>
        setValidationSampleRoleCommand;

    public ICommand ProposeValidationThresholdCandidateCommand =>
        validationThresholdWorkflowOwner.ProposeValidationThresholdCandidateCommand;

    public ICommand ReviewValidationThresholdCandidateCommand =>
        validationThresholdWorkflowOwner.ReviewValidationThresholdCandidateCommand;

    public ICommand CancelValidationThresholdReviewCommand =>
        validationThresholdWorkflowOwner.CancelValidationThresholdReviewCommand;

    public ICommand ApplyValidationThresholdCandidateCommand =>
        validationThresholdWorkflowOwner.ApplyValidationThresholdCandidateCommand;

    public ICommand RevalidateValidationThresholdCorrectionCommand =>
        validationThresholdWorkflowOwner.RevalidateValidationThresholdCorrectionCommand;

    public ICommand ReplayValidationThresholdHeldOutCommand =>
        validationThresholdWorkflowOwner.ReplayValidationThresholdHeldOutCommand;

    public bool BeginValidationFailureCorrectionContext()
    {
        if (SelectedValidationSetSample is not { } sample
            || SelectedValidationSetStep is not { } step
            || sample.Status is not ("Fail" or "Error")
            || step.Status is not ("Fail" or "Error"))
        {
            return false;
        }

        var failedCells = step.Metrics.FirstOrDefault(metric =>
            string.Equals(metric.Name, "Failed cells", StringComparison.OrdinalIgnoreCase));
        var passedCells = step.Metrics.FirstOrDefault(metric =>
            string.Equals(metric.Name, "Passed cells", StringComparison.OrdinalIgnoreCase));
        var failingMetricCount = step.Metrics.Count(metric =>
            metric.Status is "Fail" or "Error");
        var failingOverlayCount = step.Overlays.Count(overlay =>
            overlay.Status is "Fail" or "Error");
        var cellSummary = failedCells is not null
            ? passedCells is not null
                ? $"{failedCells.Value} failed cells · {passedCells.Value} passed cells"
                : $"{failedCells.Value} failed cells"
            : failingOverlayCount > 0
                ? $"{failingOverlayCount} failed regions · {failingMetricCount} failing metrics"
                : $"{failingMetricCount} failing metrics";

        ActiveValidationFailureCorrectionContext =
            new ValidationFailureCorrectionContext(
                sample.SourcePath,
                sample.FileName,
                sample.Status,
                step.StepId,
                step.ToolName,
                string.IsNullOrWhiteSpace(step.Evidence)
                    ? sample.Message
                    : step.Evidence,
                cellSummary);
        return true;
    }

    private void InitializeValidationSet()
    {
        validationSetDefinitionOwner =
            new ToolWorkbenchValidationSetDefinitionOwner(
                createDocument,
                getRecipeName,
                Localize,
                OnValidationSetDefinitionChanged,
                OnValidationSetDefinitionDirtyChanged);
        validationSetReviewOwner = new ToolWorkbenchValidationSetReviewOwner(
            validationSetDefinitionOwner.Samples,
            () => IsValidationSetRunning,
            isSourceReady,
            OpenSelectedValidationSetComparison);
        validationThresholdWorkflowOwner =
            new ToolWorkbenchValidationThresholdWorkflowOwner(
                validationSetExecutionOwner,
                validationSetDefinitionOwner.Samples,
                pipelineSteps,
                createDocument,
                hasPendingStepParameterChanges,
                getSelectedPipelineStep,
                trySelectPipelineStep,
                stepPropertySession,
                getRecipePath,
                appendLog,
                Localize,
                LocalizeStatus);
        validationSetEventCoordinator = CreateValidationSetEventCoordinator();
        selectValidationSetSourcesCommand = new RelayCommand(
            _ => SelectValidationSetSourcesRequested?.Invoke(this, EventArgs.Empty),
            _ => validationSetExecutionOwner.CanStart);
        addCurrentSourceToValidationSetCommand = new RelayCommand(
            _ => AddCurrentSourceToValidationSet(),
            _ => validationSetExecutionOwner.CanStart
                 && isSourceReady()
                 && !string.IsNullOrWhiteSpace(getSourcePath())
                 && File.Exists(getSourcePath()));
        runValidationSetCommand = new RelayCommand(
            _ => StartRunValidationSetCommand(),
            _ => validationSetExecutionOwner.CanStart
                 && validationSetDefinitionOwner.Samples.Count > 0);
        clearValidationSetCommand = new RelayCommand(
            _ => ClearValidationSet(),
            _ => validationSetExecutionOwner.CanStart
                 && validationSetDefinitionOwner.Samples.Count > 0);
        cancelValidationSetCommand = new RelayCommand(
            _ => validationSetExecutionOwner.Cancel(),
            _ => !validationSetExecutionOwner.IsDisposed
                 && IsValidationSetRunning);
        setValidationSampleRoleCommand = new RelayCommand(
            parameter => SetSelectedValidationSampleRole(parameter?.ToString()),
            _ => validationSetExecutionOwner.CanStart
                 && SelectedValidationSetSample is not null);
        RefreshValidationSetCapability();
        RefreshValidationSetSummary();
    }

    private ToolWorkbenchValidationSetEventCoordinator CreateValidationSetEventCoordinator() =>
        new(
            validationSetDefinitionOwner,
            validationSetReviewOwner,
            validationThresholdWorkflowOwner,
            args => OnPropertyChanged(args.PropertyName),
            args => OnValidationSetReviewOwnerPropertyChanged(this, args),
            args => OnPropertyChanged(args.PropertyName));

    private void AddCurrentSourceToValidationSet()
    {
        if (!isSourceReady()
            || string.IsNullOrWhiteSpace(getSourcePath())
            || !File.Exists(getSourcePath()))
        {
            return;
        }

        SetValidationSetSources(
            validationSetDefinitionOwner.Samples
                .Select(sample => sample.SourcePath)
                .Append(getSourcePath()));
        appendLog(
            "Validation Set",
            $"Current recipe input staged without execution: {Path.GetFullPath(getSourcePath())}.");
    }

    public void SetValidationSetSources(IEnumerable<string> sourcePaths)
    {
        validationSetDefinitionOwner.SetValidationSetSources(sourcePaths);
    }

    internal async Task RunValidationSetAsync()
    {
        if (validationSetExecutionOwner.IsDisposed
            || IsValidationSetRunning
            || validationSetDefinitionOwner.Samples.Count == 0)
        {
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        ValidationSetSummary = Localize(
            $"{validationSetDefinitionOwner.Samples.Count}개 샘플을 순서대로 실행하고 있습니다.",
            $"Running {validationSetDefinitionOwner.Samples.Count} sample(s) sequentially.");
        try
        {
            var document = createDocument();
            var samples = validationSetDefinitionOwner.Samples.Select(row =>
                new ToolRecipeValidationSampleInput(
                    row.SourcePath,
                    row.Role)).ToArray();
            var result = await validationThresholdWorkflowOwner.AnalyzeAsync(
                document,
                samples,
                ProjectValidationSetResult);
            ValidationSetFilter = ValidationSetStatusFilter.All;
            RefreshValidationSetReviewSamples();
            NotifyValidationSetCountsChanged();
            SamplesChanged?.Invoke(this, EventArgs.Empty);
            ValidationSetSummary = result.Samples.Count == 0
                ? Localize(result.Message, result.Message)
                : LocalizeResultSummary(result);
            SelectedValidationSetSample =
                validationSetDefinitionOwner.Samples.FirstOrDefault(row => row.Status is "Fail" or "Error")
                ?? validationSetDefinitionOwner.Samples.FirstOrDefault();
            validationThresholdWorkflowOwner.CompleteAnalysis(
                result.Samples.Count);
            appendLog("Validation Set", result.Message);
        }
        catch (OperationCanceledException)
        {
            if (IsDisposed)
            {
                return;
            }

            ValidationSetSummary = Localize(
                "반복 검증이 취소되었습니다. 작성 중인 레시피와 3D 뷰 입력은 변경되지 않았습니다.",
                "Repeat validation was canceled. The authored recipe and 3D Viewer input were not changed.");
            appendLog("Validation Set", "Canceled by operator.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or OverflowException)
        {
            if (IsDisposed)
            {
                return;
            }

            ValidationSetSummary = Localize(
                $"반복 검증을 시작할 수 없습니다: {exception.Message}",
                $"Validation Set could not start: {exception.Message}");
            appendLog("Validation Set", exception.Message);
        }
        finally
        {
            if (!IsDisposed)
            {
                OnPropertyChanged(nameof(HasValidationSetSamples));
            }
        }
    }

    private void StartRunValidationSetCommand()
    {
        if (IsDisposed)
        {
            return;
        }

        validationSetExecutionOwner.TryStartCommand(
            RunValidationSetAsync,
            ReportRunValidationSetCommandFailure);
    }

    private void ReportRunValidationSetCommandFailure(Exception exception)
    {
        if (!IsDisposed)
        {
            appendLog("Error", $"Validation Set command failed: {exception.Message}");
        }
    }

    private void ProjectValidationSetResult(ToolRecipeValidationSetResult result)
    {
        validationSetDefinitionOwner.ReplaceExecutionResult(
            result.Samples.Select(sample =>
        {
            var steps = sample.Steps.Select(step => new ValidationSetStepRow(
                step.Order,
                step.StepId,
                step.ToolName,
                step.Status.ToString(),
                LocalizeStatus(step.Status),
                step.Evidence,
                step.Metrics.Select(metric => new ValidationSetMetricRow(
                    metric.Name,
                    metric.Value.ToString(
                        "G6",
                        System.Globalization.CultureInfo.InvariantCulture),
                    metric.Unit,
                    metric.Status?.ToString() ?? string.Empty,
                    metric.Status is { } metricStatus
                        ? LocalizeStatus(metricStatus)
                        : string.Empty)).ToArray(),
                step.Overlays.Select(overlay => new ValidationSetOverlayRow(
                    overlay.Kind.ToString(),
                    overlay.Label,
                    overlay.Status?.ToString() ?? string.Empty,
                    overlay.Status is { } overlayStatus
                        ? LocalizeStatus(overlayStatus)
                        : string.Empty)).ToArray())).ToArray();
            return new ValidationSetSampleRow(
                sample.Order,
                sample.SourcePath,
                sample.Role,
                sample.Status.ToString(),
                LocalizeStatus(sample.Status),
                LocalizeResultMessage(sample),
                sample.Duration.TotalMilliseconds.ToString(
                    "N0",
                    System.Globalization.CultureInfo.InvariantCulture) + " ms",
                steps);
        }));
    }

    public void ClearValidationSet()
    {
        validationSetDefinitionOwner.ClearDefinition();
        ActiveValidationFailureCorrectionContext = null;
        SelectedValidationSetSample = null;
        SelectedValidationSetStep = null;
        ValidationSetFilter = ValidationSetStatusFilter.All;
        RefreshValidationSetReviewSamples();
        validationThresholdWorkflowOwner.ResetProgress();
        ClearValidationSetComparePins();
        NotifyValidationSetCountsChanged();
        SamplesChanged?.Invoke(this, EventArgs.Empty);
        runValidationSetCommand.RaiseCanExecuteChanged();
        clearValidationSetCommand.RaiseCanExecuteChanged();
        RefreshValidationSetSummary();
    }

    private void SetSelectedValidationSampleRole(string? value)
    {
        var updated = validationSetDefinitionOwner.SetSelectedSampleRole(
            SelectedValidationSetSample,
            value);
        if (updated is null)
        {
            return;
        }

        SelectedValidationSetSample = validationSetDefinitionOwner.Samples.FirstOrDefault(
            sample => string.Equals(
                sample.SourcePath,
                updated.SourcePath,
                StringComparison.OrdinalIgnoreCase));
        NotifyValidationSetCountsChanged();
        RefreshValidationSetSummary();
        appendLog(
            "Validation Set",
            $"Sample role changed without execution: {updated.FileName} -> {updated.Role}.");
    }

    private void RefreshValidationSetReviewSamples()
    {
        validationSetReviewOwner.RefreshSamples();
        OnPropertyChanged(nameof(HasValidationSetSamples));
    }

    private void OnValidationSetReviewOwnerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        OnPropertyChanged(args.PropertyName);
        if (args.PropertyName == nameof(SelectedValidationSetSample))
        {
            setValidationSampleRoleCommand?.RaiseCanExecuteChanged();
            RefreshValidationThresholdCorrectionCommands();
        }
    }

    private void OpenSelectedValidationSetComparison()
    {
        if (SelectedValidationSetSample is not { } sample || !File.Exists(sample.SourcePath))
        {
            return;
        }

        ComparisonRequested?.Invoke(this, sample);
    }

    private void NotifyValidationSetCountsChanged()
    {
        OnPropertyChanged(nameof(HasValidationSetSamples));
        OnPropertyChanged(nameof(ValidationSetAllCount));
        OnPropertyChanged(nameof(ValidationSetPassCount));
        OnPropertyChanged(nameof(ValidationSetFailCount));
        OnPropertyChanged(nameof(ValidationSetErrorCount));
        OnPropertyChanged(nameof(ValidationSetGoodCount));
        OnPropertyChanged(nameof(ValidationSetBadCount));
        OnPropertyChanged(nameof(ValidationSetHeldOutCount));
        OnPropertyChanged(nameof(HasValidationSetIssues));
        OnPropertyChanged(nameof(IsSelectedValidationRoleGood));
        OnPropertyChanged(nameof(IsSelectedValidationRoleBad));
        OnPropertyChanged(nameof(IsSelectedValidationRoleHeldOut));
        validationSetReviewOwner.RefreshCommandStates();
        setValidationSampleRoleCommand.RaiseCanExecuteChanged();
        RefreshValidationThresholdCorrectionCommands();
    }

    private void ClearValidationEvidence() =>
        validationThresholdWorkflowOwner.ClearAnalysis();

    internal Task RevalidateValidationThresholdCorrectionAsync() =>
        validationThresholdWorkflowOwner.RevalidateAsync();

    internal Task ReplayValidationThresholdHeldOutAsync() =>
        validationThresholdWorkflowOwner.ReplayHeldOutAsync();

    private void RefreshValidationSetExecutionState()
    {
        OnPropertyChanged(nameof(IsValidationSetRunning));
        OnPropertyChanged(nameof(IsValidationSetIdle));
        addCurrentSourceToValidationSetCommand.RaiseCanExecuteChanged();
        runValidationSetCommand.RaiseCanExecuteChanged();
        clearValidationSetCommand.RaiseCanExecuteChanged();
        selectValidationSetSourcesCommand.RaiseCanExecuteChanged();
        cancelValidationSetCommand.RaiseCanExecuteChanged();
        validationSetReviewOwner.RefreshCommandStates();
        setValidationSampleRoleCommand.RaiseCanExecuteChanged();
        RefreshValidationThresholdCorrectionCommands();
    }

    public void RefreshValidationThresholdCorrectionCommands() =>
        validationThresholdWorkflowOwner.RefreshCommandStates();

    public void NotifyValidationThresholdDraftCommitted(
        ToolWorkbenchPipelineStepItem step,
        bool changed) =>
        validationThresholdWorkflowOwner.NotifyDraftCommitted(step, changed);

    public void NotifyValidationThresholdDraftDiscarded(string? stepId) =>
        validationThresholdWorkflowOwner.NotifyDraftDiscarded(stepId);

    public void SetValidationSetDefinitionDirty(bool value) =>
        validationSetDefinitionOwner.SetDefinitionDirty(value);

    private void OnValidationSetDefinitionDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsValidationSetDefinitionDirty));
        DefinitionDirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnValidationSetDefinitionChanged(bool resetFilter)
    {
        ClearValidationEvidence();
        if (resetFilter)
        {
            ValidationSetFilter = ValidationSetStatusFilter.All;
        }

        RefreshValidationSetReviewSamples();
        NotifyValidationSetCountsChanged();
        SamplesChanged?.Invoke(this, EventArgs.Empty);
        runValidationSetCommand.RaiseCanExecuteChanged();
        clearValidationSetCommand.RaiseCanExecuteChanged();
        RefreshValidationSetSummary();
    }

    public void SaveValidationSetDefinition(string recipePath) =>
        validationSetDefinitionOwner.SaveForRecipe(recipePath);

    public void SaveValidationThresholdCorrectionEvidence(string recipePath) =>
        validationThresholdWorkflowOwner.SaveCorrectionEvidence(recipePath);

    public void LoadValidationSetDefinition(
        string recipePath,
        ToolRecipeDocument document)
        => validationSetDefinitionOwner.LoadForRecipe(recipePath, document);

    public void LoadValidationThresholdCorrectionEvidence(
        string recipePath,
        ToolRecipeDocument document) =>
        validationThresholdWorkflowOwner.LoadCorrectionEvidence(
            recipePath,
            document);

    private void ClearValidationSetComparePins() =>
        ComparePinsClearRequested?.Invoke(this, EventArgs.Empty);

    public void RefreshValidationSetCapability()
    {
        addCurrentSourceToValidationSetCommand?.RaiseCanExecuteChanged();
        if (ToolRecipeValidationSetExecution.CanExecute(createDocument(), out var message))
        {
            ValidationSetCapability = Localize(
                "현재 레시피의 지원되는 전체 툴 체인을 동일 그리드 C3D 샘플에 안전하게 재바인딩하여 순서대로 실행할 수 있습니다.",
                "The complete supported tool chain can be safely rebound and replayed in order against same-grid C3D samples.");
            return;
        }

        ValidationSetCapability = Localize(
            $"현재 레시피 실행 범위: {message}",
            $"Current recipe coverage: {message}");
    }

    private void RefreshValidationSetSummary()
    {
        ValidationSetSummary = validationSetDefinitionOwner.Samples.Count == 0
            ? Localize(
                "C3D 샘플을 추가하고 기대 역할을 지정한 다음 '샘플 세트 실행'을 선택하세요. 샘플 선택만으로 검사는 실행되지 않습니다.",
                "Add C3D samples, assign expected roles, then choose Run sample set. Selecting samples never runs inspection.")
            : Localize(
                $"{validationSetDefinitionOwner.Samples.Count}개 샘플 준비됨 · 실행 전",
                $"{validationSetDefinitionOwner.Samples.Count} sample(s) ready · not run");
    }

    public void RefreshValidationSetLocalization()
    {
        validationThresholdWorkflowOwner.RefreshLocalization();
        RefreshValidationSetCapability();
        validationSetDefinitionOwner.RefreshLocalization(LocalizeStatus);

        RefreshValidationSetReviewSamples();
        NotifyValidationSetCountsChanged();
        SamplesChanged?.Invoke(this, EventArgs.Empty);
        if (validationSetDefinitionOwner.Samples.All(sample => sample.Status == "Pending"))
        {
            RefreshValidationSetSummary();
        }
        else if (validationSetDefinitionOwner.Samples.Count > 0)
        {
            var pass = validationSetDefinitionOwner.Samples.Count(sample => sample.Status == "Pass");
            var fail = validationSetDefinitionOwner.Samples.Count(sample => sample.Status == "Fail");
            var error = validationSetDefinitionOwner.Samples.Count(sample => sample.Status == "Error");
            ValidationSetSummary = Localize(
                $"완료 {validationSetDefinitionOwner.Samples.Count}개 · 통과 {pass} · 실패 {fail} · 오류 {error}",
                $"Completed {validationSetDefinitionOwner.Samples.Count} · Pass {pass} · Fail {fail} · Error {error}");
        }
    }

    private string Localize(string korean, string english) => localize(korean, english);

    private string LocalizeStatus(ResultStatus status) => localizeStatus(status);

    private string LocalizeResultMessage(ToolRecipeValidationSampleResult sample) =>
        sample.Status switch
        {
            ResultStatus.Pass => Localize("모든 지원 검사 단계가 통과했습니다.", "All supported inspection steps passed."),
            ResultStatus.Fail => Localize("하나 이상의 검사 단계가 허용 범위를 벗어났습니다.", "One or more inspection steps are out of tolerance."),
            ResultStatus.Warning => Localize("검사가 경고와 함께 완료되었습니다.", "Inspection completed with warnings."),
            _ => Localize($"실행 오류: {LocalizeValidationError(sample.Message)}", $"Execution error: {sample.Message}")
        };

    private string LocalizeValidationError(string message)
    {
        if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English)
        {
            return message;
        }

        const string gridPrefix = "Grid mismatch. Recipe expects ";
        if (message.StartsWith(gridPrefix, StringComparison.Ordinal))
        {
            return "그리드 불일치. 레시피 "
                   + message[gridPrefix.Length..]
                       .Replace("; sample is ", "; 샘플 ", StringComparison.Ordinal)
                       .TrimEnd('.');
        }
        if (message.StartsWith("Validation sample does not exist.", StringComparison.Ordinal))
        {
            return "검증 샘플 파일이 없습니다.";
        }
        if (message.Equals("Recipe source grid identity is incomplete.", StringComparison.Ordinal))
        {
            return "레시피 소스의 그리드 식별 정보가 완전하지 않습니다.";
        }

        return message;
    }

    private string LocalizeResultSummary(ToolRecipeValidationSetResult result)
    {
        var pass = result.Samples.Count(sample => sample.Status == ResultStatus.Pass);
        var fail = result.Samples.Count(sample => sample.Status == ResultStatus.Fail);
        var error = result.Samples.Count(sample => sample.Status == ResultStatus.Error);
        return Localize(
            $"완료 {result.Samples.Count}개 · 통과 {pass} · 실패 {fail} · 오류 {error} · {result.Duration.TotalMilliseconds:N0} ms",
            $"Completed {result.Samples.Count} · Pass {pass} · Fail {fail} · Error {error} · {result.Duration.TotalMilliseconds:N0} ms");
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        validationSetEventCoordinator.Dispose();
        validationThresholdWorkflowOwner.Dispose();
        validationSetExecutionOwner.Dispose();
        PropertyChanged = null;
        SamplesChanged = null;
        DefinitionDirtyChanged = null;
        ComparePinsClearRequested = null;
        ComparisonRequested = null;
        SelectValidationSetSourcesRequested = null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
