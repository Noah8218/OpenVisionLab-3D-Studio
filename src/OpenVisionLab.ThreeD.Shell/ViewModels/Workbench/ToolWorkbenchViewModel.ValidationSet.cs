using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private readonly ObservableCollection<ValidationSetSampleRow> validationSetSamples = [];
    private readonly ObservableCollection<ValidationSetSampleRow> filteredValidationSetSamples = [];
    private readonly ObservableCollection<ValidationSetStepRow> selectedValidationSetSteps = [];
    private readonly ObservableCollection<ValidationEvidenceDistributionRow>
        validationEvidenceDistributions = [];
    private readonly ObservableCollection<ValidationThresholdCandidateRow>
        validationThresholdCandidates = [];
    private readonly ObservableCollection<ValidationThresholdDecisionRow>
        selectedValidationThresholdDecisions = [];
    private readonly ObservableCollection<ValidationThresholdParameterChangeRow>
        validationThresholdParameterChanges = [];
    private readonly ObservableCollection<ValidationThresholdHeldOutSampleRow>
        validationThresholdHeldOutSamples = [];
    private readonly ObservableCollection<ValidationThresholdDevelopmentSampleRow>
        validationThresholdDevelopmentSamples = [];
    private RelayCommand selectValidationSetSourcesCommand = null!;
    private RelayCommand addCurrentSourceToValidationSetCommand = null!;
    private RelayCommand runValidationSetCommand = null!;
    private RelayCommand clearValidationSetCommand = null!;
    private RelayCommand cancelValidationSetCommand = null!;
    private RelayCommand setValidationSetFilterCommand = null!;
    private RelayCommand previousValidationSetIssueCommand = null!;
    private RelayCommand nextValidationSetIssueCommand = null!;
    private RelayCommand openValidationSetComparisonCommand = null!;
    private RelayCommand setValidationSampleRoleCommand = null!;
    private RelayCommand reviewValidationThresholdCandidateCommand = null!;
    private RelayCommand cancelValidationThresholdReviewCommand = null!;
    private RelayCommand applyValidationThresholdCandidateCommand = null!;
    private RelayCommand revalidateValidationThresholdCorrectionCommand = null!;
    private RelayCommand replayValidationThresholdHeldOutCommand = null!;
    private ValidationSetSampleRow? selectedValidationSetSample;
    private ValidationSetStepRow? selectedValidationSetStep;
    private ValidationSetStatusFilter validationSetFilter = ValidationSetStatusFilter.All;
    private string validationSetSummary = string.Empty;
    private string validationSetCapability = string.Empty;
    private string validationSetProgressText = string.Empty;
    private double validationSetProgress;
    private bool isValidationSetRunning;
    private CancellationTokenSource? validationSetCancellation;
    private ToolRecipeLabeledEvidenceReport? validationEvidenceReport;
    private ToolRecipeThresholdCandidateReport? validationThresholdReport;
    private ValidationThresholdCandidateRow? selectedValidationThresholdCandidate;
    private ToolRecipeThresholdParameterProposal? validationThresholdReviewProposal;
    private ToolRecipeThresholdCorrectionEvidence? validationThresholdCorrectionEvidence;
    private ToolRecipeValidationSetResult?
        validationThresholdBeforeDevelopmentResult;
    private ToolRecipeValidationSetResult?
        validationThresholdAfterDevelopmentResult;
    private IReadOnlyList<ToolRecipeThresholdManualParameterChange>
        validationThresholdManualChanges = [];
    private bool isValidationThresholdReviewActive;
    private bool isValidationThresholdCandidateApplied;
    private bool isValidationThresholdManualCorrectionCommitted;
    private bool isValidationThresholdDevelopmentValidated;
    private string validationThresholdCorrectionSummary =
        "Select a mapped candidate, then use Review before applying it to the PropertyGrid draft.";
    private bool isValidationSetDefinitionDirty;
    private bool isValidationEvidenceExpanded;
    private bool isValidationThresholdExpanded;
    private ValidationFailureCorrectionContext? activeValidationFailureCorrectionContext;

    public event EventHandler? SelectValidationSetSourcesRequested;
    public event EventHandler? ValidationSetComparisonRequested;

    public ReadOnlyObservableCollection<ValidationSetSampleRow> ValidationSetSamples { get; private set; } = null!;

    public ReadOnlyObservableCollection<ValidationSetStepRow> SelectedValidationSetSteps { get; private set; } = null!;

    public ReadOnlyObservableCollection<ValidationEvidenceDistributionRow>
        ValidationEvidenceDistributions { get; private set; } = null!;

    public ReadOnlyObservableCollection<ValidationThresholdCandidateRow>
        ValidationThresholdCandidates { get; private set; } = null!;

    public ReadOnlyObservableCollection<ValidationThresholdDecisionRow>
        SelectedValidationThresholdDecisions { get; private set; } = null!;

    public ReadOnlyObservableCollection<ValidationThresholdParameterChangeRow>
        ValidationThresholdParameterChanges { get; private set; } = null!;

    public ReadOnlyObservableCollection<ValidationThresholdHeldOutSampleRow>
        ValidationThresholdHeldOutSamples { get; private set; } = null!;

    public ReadOnlyObservableCollection<ValidationThresholdDevelopmentSampleRow>
        ValidationThresholdDevelopmentSamples { get; private set; } = null!;

    public ICommand SelectValidationSetSourcesCommand => selectValidationSetSourcesCommand;

    public ICommand AddCurrentSourceToValidationSetCommand => addCurrentSourceToValidationSetCommand;

    public ICommand RunValidationSetCommand => runValidationSetCommand;

    public ICommand ClearValidationSetCommand => clearValidationSetCommand;

    public ICommand CancelValidationSetCommand => cancelValidationSetCommand;

    public ICommand SetValidationSetFilterCommand => setValidationSetFilterCommand;

    public ICommand PreviousValidationSetIssueCommand => previousValidationSetIssueCommand;

    public ICommand NextValidationSetIssueCommand => nextValidationSetIssueCommand;

    public ICommand OpenValidationSetComparisonCommand => openValidationSetComparisonCommand;

    public ICommand SetValidationSampleRoleCommand =>
        setValidationSampleRoleCommand;

    public ICommand ReviewValidationThresholdCandidateCommand =>
        reviewValidationThresholdCandidateCommand;

    public ICommand CancelValidationThresholdReviewCommand =>
        cancelValidationThresholdReviewCommand;

    public ICommand ApplyValidationThresholdCandidateCommand =>
        applyValidationThresholdCandidateCommand;

    public ICommand RevalidateValidationThresholdCorrectionCommand =>
        revalidateValidationThresholdCorrectionCommand;

    public ICommand ReplayValidationThresholdHeldOutCommand =>
        replayValidationThresholdHeldOutCommand;

    public ValidationSetSampleRow? SelectedValidationSetSample
    {
        get => selectedValidationSetSample;
        set
        {
            if (ReferenceEquals(selectedValidationSetSample, value))
            {
                return;
            }

            selectedValidationSetSample = value;
            selectedValidationSetSteps.Clear();
            foreach (var step in value?.Steps ?? [])
            {
                selectedValidationSetSteps.Add(step);
            }
            SelectedValidationSetStep =
                value?.Steps.FirstOrDefault(step => step.Status is "Fail" or "Error")
                ?? value?.Steps.FirstOrDefault();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedValidationSetSample));
            OnPropertyChanged(nameof(IsSelectedValidationRoleGood));
            OnPropertyChanged(nameof(IsSelectedValidationRoleBad));
            OnPropertyChanged(nameof(IsSelectedValidationRoleHeldOut));
            previousValidationSetIssueCommand.RaiseCanExecuteChanged();
            nextValidationSetIssueCommand.RaiseCanExecuteChanged();
            openValidationSetComparisonCommand.RaiseCanExecuteChanged();
            setValidationSampleRoleCommand.RaiseCanExecuteChanged();
            RefreshValidationThresholdCorrectionCommands();
        }
    }

    public ValidationSetStepRow? SelectedValidationSetStep
    {
        get => selectedValidationSetStep;
        set
        {
            if (ReferenceEquals(selectedValidationSetStep, value)) return;
            selectedValidationSetStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedValidationSetStep));
        }
    }

    public bool HasSelectedValidationSetStep => SelectedValidationSetStep is not null;

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

    public ValidationSetStatusFilter ValidationSetFilter
    {
        get => validationSetFilter;
        private set
        {
            if (validationSetFilter == value) return;
            validationSetFilter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValidationSetFilterAll));
            OnPropertyChanged(nameof(IsValidationSetFilterPass));
            OnPropertyChanged(nameof(IsValidationSetFilterFail));
            OnPropertyChanged(nameof(IsValidationSetFilterError));
            RefreshFilteredValidationSetSamples();
        }
    }

    public bool IsValidationSetFilterAll => ValidationSetFilter == ValidationSetStatusFilter.All;
    public bool IsValidationSetFilterPass => ValidationSetFilter == ValidationSetStatusFilter.Pass;
    public bool IsValidationSetFilterFail => ValidationSetFilter == ValidationSetStatusFilter.Fail;
    public bool IsValidationSetFilterError => ValidationSetFilter == ValidationSetStatusFilter.Error;

    public int ValidationSetAllCount => validationSetSamples.Count;
    public int ValidationSetPassCount => validationSetSamples.Count(row => row.Status == "Pass");
    public int ValidationSetFailCount => validationSetSamples.Count(row => row.Status == "Fail");
    public int ValidationSetErrorCount => validationSetSamples.Count(row => row.Status == "Error");
    public int ValidationSetGoodCount => validationSetSamples.Count(row =>
        row.Role == ToolRecipeValidationSampleRole.Good);
    public int ValidationSetBadCount => validationSetSamples.Count(row =>
        row.Role == ToolRecipeValidationSampleRole.Bad);
    public int ValidationSetHeldOutCount => validationSetSamples.Count(row =>
        row.Role == ToolRecipeValidationSampleRole.HeldOut);
    public bool HasValidationSetIssues => validationSetSamples.Any(row =>
        row.Status is "Fail" or "Error");
    public bool IsSelectedValidationRoleGood =>
        SelectedValidationSetSample?.Role == ToolRecipeValidationSampleRole.Good;
    public bool IsSelectedValidationRoleBad =>
        SelectedValidationSetSample?.Role == ToolRecipeValidationSampleRole.Bad;
    public bool IsSelectedValidationRoleHeldOut =>
        SelectedValidationSetSample?.Role == ToolRecipeValidationSampleRole.HeldOut;
    public bool HasValidationEvidence =>
        validationEvidenceDistributions.Count > 0;
    public bool HasValidationThresholdCandidates =>
        validationThresholdCandidates.Count > 0;

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
        isValidationSetDefinitionDirty;

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
        get => selectedValidationThresholdCandidate;
        set
        {
            if (ReferenceEquals(selectedValidationThresholdCandidate, value))
            {
                return;
            }

            if (validationThresholdReviewProposal is { } proposal
                && !string.Equals(
                    proposal.CandidateId,
                    value?.CandidateId,
                    StringComparison.Ordinal))
            {
                ClearValidationThresholdCorrectionState(
                    "Candidate selection changed. Start Review again.");
            }
            selectedValidationThresholdCandidate = value;
            selectedValidationThresholdDecisions.Clear();
            foreach (var decision in value?.Candidate.Decisions ?? [])
            {
                selectedValidationThresholdDecisions.Add(
                    new ValidationThresholdDecisionRow(
                        decision.SampleOrder,
                        Path.GetFileName(decision.SourcePath),
                        decision.SampleIdentity,
                        decision.ExpectedRole.ToString(),
                        decision.PredictedRole.ToString(),
                        decision.Decision.ToString(),
                        decision.Value.ToString(
                            "G6",
                            System.Globalization.CultureInfo.InvariantCulture),
                        decision.EvidenceLocator));
            }
            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasSelectedValidationThresholdCandidate));
            RefreshValidationThresholdCorrectionCommands();
        }
    }

    public bool HasSelectedValidationThresholdCandidate =>
        SelectedValidationThresholdCandidate is not null;

    public bool IsValidationThresholdReviewActive =>
        isValidationThresholdReviewActive;

    public bool IsValidationThresholdCandidateApplied =>
        isValidationThresholdCandidateApplied;

    public bool IsValidationThresholdManualCorrectionCommitted =>
        isValidationThresholdManualCorrectionCommitted;

    public bool IsValidationThresholdDevelopmentValidated =>
        isValidationThresholdDevelopmentValidated;

    private bool RequiresValidationThresholdDevelopmentReplay =>
        string.Equals(
            validationThresholdReviewProposal?.ToolId,
            "completeness-grid",
            StringComparison.Ordinal);

    public bool HasValidationThresholdParameterChanges =>
        validationThresholdParameterChanges.Count > 0;

    public bool HasValidationThresholdHeldOutEvidence =>
        validationThresholdHeldOutSamples.Count > 0;

    public bool HasValidationThresholdDevelopmentEvidence =>
        validationThresholdDevelopmentSamples.Count > 0;

    public string ValidationThresholdCorrectionSummary
    {
        get => validationThresholdCorrectionSummary;
        private set
        {
            if (string.Equals(
                    validationThresholdCorrectionSummary,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }
            validationThresholdCorrectionSummary = value;
            OnPropertyChanged();
        }
    }

    public string ValidationEvidenceSummary =>
        validationEvidenceReport?.Message
        ?? "Run the labeled Validation Set to calculate step and ROI distributions.";
    public string ValidationEvidenceWarning =>
        validationEvidenceReport?.Warnings.Count > 0
            ? string.Join(" ", validationEvidenceReport.Warnings)
            : string.Empty;
    public string ValidationThresholdSummary =>
        validationThresholdReport?.Message
        ?? "Run the labeled Validation Set to calculate review-only threshold candidates.";
    public string ValidationThresholdWarning =>
        validationThresholdReport is not { } report
            ? string.Empty
            : report.EvidenceWarnings.Count > 0
                ? FormatThresholdEvidenceWarnings(report.EvidenceWarnings)
                : report.Warnings.Count > 0
                    ? string.Join(" ", report.Warnings)
                    : string.Empty;

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

    public string ValidationSetProgressText
    {
        get => validationSetProgressText;
        private set
        {
            if (validationSetProgressText == value) return;
            validationSetProgressText = value;
            OnPropertyChanged();
        }
    }

    public double ValidationSetProgress
    {
        get => validationSetProgress;
        private set
        {
            if (Math.Abs(validationSetProgress - value) < 0.001) return;
            validationSetProgress = value;
            OnPropertyChanged();
        }
    }

    public bool IsValidationSetRunning
    {
        get => isValidationSetRunning;
        private set
        {
            if (isValidationSetRunning == value) return;
            isValidationSetRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValidationSetIdle));
            addCurrentSourceToValidationSetCommand.RaiseCanExecuteChanged();
            runValidationSetCommand.RaiseCanExecuteChanged();
            clearValidationSetCommand.RaiseCanExecuteChanged();
            selectValidationSetSourcesCommand.RaiseCanExecuteChanged();
            cancelValidationSetCommand.RaiseCanExecuteChanged();
            previousValidationSetIssueCommand.RaiseCanExecuteChanged();
            nextValidationSetIssueCommand.RaiseCanExecuteChanged();
            openValidationSetComparisonCommand.RaiseCanExecuteChanged();
            setValidationSampleRoleCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsValidationSetIdle => !IsValidationSetRunning;

    public bool HasValidationSetSamples => validationSetSamples.Count > 0;

    public bool HasSelectedValidationSetSample => SelectedValidationSetSample is not null;

    private void InitializeValidationSet()
    {
        ValidationSetSamples = new ReadOnlyObservableCollection<ValidationSetSampleRow>(filteredValidationSetSamples);
        SelectedValidationSetSteps = new ReadOnlyObservableCollection<ValidationSetStepRow>(selectedValidationSetSteps);
        ValidationEvidenceDistributions =
            new ReadOnlyObservableCollection<ValidationEvidenceDistributionRow>(
                validationEvidenceDistributions);
        ValidationThresholdCandidates =
            new ReadOnlyObservableCollection<ValidationThresholdCandidateRow>(
                validationThresholdCandidates);
        SelectedValidationThresholdDecisions =
            new ReadOnlyObservableCollection<ValidationThresholdDecisionRow>(
                selectedValidationThresholdDecisions);
        ValidationThresholdParameterChanges =
            new ReadOnlyObservableCollection<ValidationThresholdParameterChangeRow>(
                validationThresholdParameterChanges);
        ValidationThresholdHeldOutSamples =
            new ReadOnlyObservableCollection<ValidationThresholdHeldOutSampleRow>(
                validationThresholdHeldOutSamples);
        ValidationThresholdDevelopmentSamples =
            new ReadOnlyObservableCollection<ValidationThresholdDevelopmentSampleRow>(
                validationThresholdDevelopmentSamples);
        selectValidationSetSourcesCommand = new RelayCommand(
            _ => SelectValidationSetSourcesRequested?.Invoke(this, EventArgs.Empty),
            _ => !IsValidationSetRunning);
        addCurrentSourceToValidationSetCommand = new RelayCommand(
            _ => AddCurrentSourceToValidationSet(),
            _ => !IsValidationSetRunning
                 && IsSourceReadyForRecipe
                 && !string.IsNullOrWhiteSpace(Source.Path)
                 && File.Exists(Source.Path));
        runValidationSetCommand = new RelayCommand(
            _ => _ = RunValidationSetAsync(),
            _ => !IsValidationSetRunning && validationSetSamples.Count > 0);
        clearValidationSetCommand = new RelayCommand(
            _ => ClearValidationSet(),
            _ => !IsValidationSetRunning && validationSetSamples.Count > 0);
        cancelValidationSetCommand = new RelayCommand(
            _ => validationSetCancellation?.Cancel(),
            _ => IsValidationSetRunning);
        setValidationSetFilterCommand = new RelayCommand(
            parameter => SetValidationSetFilter(parameter?.ToString()));
        previousValidationSetIssueCommand = new RelayCommand(
            _ => MoveValidationSetIssue(-1),
            _ => !IsValidationSetRunning && VisibleValidationSetIssues().Count > 0);
        nextValidationSetIssueCommand = new RelayCommand(
            _ => MoveValidationSetIssue(1),
            _ => !IsValidationSetRunning && VisibleValidationSetIssues().Count > 0);
        openValidationSetComparisonCommand = new RelayCommand(
            _ => OpenSelectedValidationSetComparison(),
            _ => !IsValidationSetRunning
                 && SelectedValidationSetSample is { SourcePath: var path }
                 && File.Exists(path)
                 && IsSourceReadyForRecipe);
        setValidationSampleRoleCommand = new RelayCommand(
            parameter => SetSelectedValidationSampleRole(parameter?.ToString()),
            _ => !IsValidationSetRunning
                 && SelectedValidationSetSample is not null);
        reviewValidationThresholdCandidateCommand = new RelayCommand(
            _ => ReviewSelectedValidationThresholdCandidate(),
            _ => !IsValidationSetRunning
                 && !HasPendingStepParameterChanges
                 && HasSelectedValidationThresholdCandidate
                 && !IsValidationThresholdReviewActive
                 && !IsValidationThresholdCandidateApplied);
        cancelValidationThresholdReviewCommand = new RelayCommand(
            _ => CancelValidationThresholdReview(),
            _ => !IsValidationSetRunning
                 && IsValidationThresholdReviewActive
                 && !IsValidationThresholdCandidateApplied);
        applyValidationThresholdCandidateCommand = new RelayCommand(
            _ => ApplyReviewedValidationThresholdCandidate(),
            _ => !IsValidationSetRunning
                 && IsValidationThresholdReviewActive
                 && !IsValidationThresholdCandidateApplied);
        revalidateValidationThresholdCorrectionCommand = new RelayCommand(
            _ => _ = RevalidateValidationThresholdCorrectionAsync(),
            _ => !IsValidationSetRunning
                 && (IsValidationThresholdManualCorrectionCommitted
                     || RequiresValidationThresholdDevelopmentReplay)
                 && IsValidationThresholdCandidateApplied
                 && !IsValidationThresholdDevelopmentValidated
                 && (!IsValidationThresholdManualCorrectionCommitted
                     || !HasPendingStepParameterChanges)
                 && validationThresholdBeforeDevelopmentResult is not null);
        replayValidationThresholdHeldOutCommand = new RelayCommand(
            _ => _ = ReplayValidationThresholdHeldOutAsync(),
            _ => !IsValidationSetRunning
                 && IsValidationThresholdCandidateApplied
                 && ((!IsValidationThresholdManualCorrectionCommitted
                      && !RequiresValidationThresholdDevelopmentReplay)
                     || (IsValidationThresholdDevelopmentValidated
                         && validationThresholdBeforeDevelopmentResult
                             is not null
                         && validationThresholdAfterDevelopmentResult
                             is not null))
                 && validationSetSamples.Any(sample =>
                     sample.Role == ToolRecipeValidationSampleRole.HeldOut));
        Localization.PropertyChanged += (_, _) => RefreshValidationSetLocalization();
        RefreshValidationSetCapability();
        RefreshValidationSetSummary();
    }

    private void AddCurrentSourceToValidationSet()
    {
        if (!IsSourceReadyForRecipe
            || string.IsNullOrWhiteSpace(Source.Path)
            || !File.Exists(Source.Path))
        {
            return;
        }

        SetValidationSetSources(
            validationSetSamples
                .Select(sample => sample.SourcePath)
                .Append(Source.Path));
        AppendLog(
            "Validation Set",
            $"Current recipe input staged without execution: {Path.GetFullPath(Source.Path)}.");
    }

    public void SetValidationSetSources(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        var existingRoles = validationSetSamples.ToDictionary(
            sample => sample.SourcePath,
            sample => sample.Role,
            StringComparer.OrdinalIgnoreCase);
        var paths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        validationSetSamples.Clear();
        foreach (var (path, index) in paths.Select((path, index) => (path, index)))
        {
            validationSetSamples.Add(new ValidationSetSampleRow(
                index + 1,
                path,
                existingRoles.GetValueOrDefault(
                    path,
                    ToolRecipeValidationSampleRole.Good),
                "Pending",
                Localize("대기", "Pending"),
                Localize(
                    "미실행 · '샘플 세트 실행'을 선택하세요.",
                    "Not run · choose Run sample set."),
                string.Empty,
                []));
        }

        ClearValidationEvidence();
        SetValidationSetDefinitionDirty(true);
        ValidationSetFilter = ValidationSetStatusFilter.All;
        RefreshFilteredValidationSetSamples();
        NotifyValidationSetCountsChanged();
        RebuildOutputCompareCandidates();
        runValidationSetCommand.RaiseCanExecuteChanged();
        clearValidationSetCommand.RaiseCanExecuteChanged();
        RefreshValidationSetSummary();
    }

    internal async Task RunValidationSetAsync()
    {
        if (IsValidationSetRunning || validationSetSamples.Count == 0)
        {
            return;
        }

        validationSetCancellation?.Dispose();
        validationSetCancellation = new CancellationTokenSource();
        validationThresholdBeforeDevelopmentResult = null;
        IsValidationSetRunning = true;
        ValidationSetProgress = 0;
        ValidationSetProgressText = Localize("반복 검증 준비 중", "Preparing repeat validation");
        ValidationSetSummary = Localize(
            $"{validationSetSamples.Count}개 샘플을 순서대로 실행하고 있습니다.",
            $"Running {validationSetSamples.Count} sample(s) sequentially.");
        try
        {
            var document = CreateDocument();
            var samples = validationSetSamples.Select(row =>
                new ToolRecipeValidationSampleInput(
                    row.SourcePath,
                    row.Role)).ToArray();
            var progress = new Progress<ToolRecipeValidationProgress>(ReportValidationSetProgress);
            var result = await Task.Run(() => ToolRecipeValidationSetExecution.Execute(
                document,
                samples,
                validationSetCancellation.Token,
                progress));
            validationSetSamples.Clear();
            foreach (var sample in result.Samples)
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
                        metric.Value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture),
                        metric.Unit,
                        metric.Status?.ToString() ?? string.Empty,
                        metric.Status is { } metricStatus ? LocalizeStatus(metricStatus) : string.Empty)).ToArray(),
                    step.Overlays.Select(overlay => new ValidationSetOverlayRow(
                        overlay.Kind.ToString(),
                        overlay.Label,
                        overlay.Status?.ToString() ?? string.Empty,
                        overlay.Status is { } overlayStatus ? LocalizeStatus(overlayStatus) : string.Empty)).ToArray())).ToArray();
                validationSetSamples.Add(new ValidationSetSampleRow(
                    sample.Order,
                    sample.SourcePath,
                    sample.Role,
                    sample.Status.ToString(),
                    LocalizeStatus(sample.Status),
                    LocalizeResultMessage(sample),
                    sample.Duration.TotalMilliseconds.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " ms",
                    steps));
            }

            SetValidationEvidence(
                ToolRecipeLabeledEvidenceAnalyzer.Analyze(document, result));
            SetValidationThresholdEvidence(
                ToolRecipeThresholdCandidateAnalyzer.Analyze(
                    document,
                    result));
            validationThresholdBeforeDevelopmentResult =
                CreateDevelopmentResult(result);
            ValidationSetFilter = ValidationSetStatusFilter.All;
            RefreshFilteredValidationSetSamples();
            NotifyValidationSetCountsChanged();
            RebuildOutputCompareCandidates();
            ValidationSetSummary = result.Samples.Count == 0
                ? Localize(result.Message, result.Message)
                : LocalizeResultSummary(result);
            SelectedValidationSetSample =
                validationSetSamples.FirstOrDefault(row => row.Status is "Fail" or "Error")
                ?? validationSetSamples.FirstOrDefault();
            ValidationSetProgress = 100;
            ValidationSetProgressText = Localize(
                $"완료 {result.Samples.Count}개",
                $"{result.Samples.Count} completed");
            AppendLog("Validation Set", result.Message);
        }
        catch (OperationCanceledException)
        {
            ValidationSetSummary = Localize(
                "반복 검증이 취소되었습니다. 작성 중인 레시피와 3D 뷰 입력은 변경되지 않았습니다.",
                "Repeat validation was canceled. The authored recipe and 3D Viewer input were not changed.");
            ValidationSetProgressText = Localize("사용자 취소", "Canceled");
            AppendLog("Validation Set", "Canceled by operator.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or OverflowException)
        {
            ValidationSetSummary = Localize(
                $"반복 검증을 시작할 수 없습니다: {exception.Message}",
                $"Validation Set could not start: {exception.Message}");
            AppendLog("Validation Set", exception.Message);
        }
        finally
        {
            IsValidationSetRunning = false;
            validationSetCancellation?.Dispose();
            validationSetCancellation = null;
            OnPropertyChanged(nameof(HasValidationSetSamples));
        }
    }

    private void ClearValidationSet()
    {
        validationSetSamples.Clear();
        filteredValidationSetSamples.Clear();
        ClearValidationEvidence();
        ActiveValidationFailureCorrectionContext = null;
        SetValidationSetDefinitionDirty(true);
        SelectedValidationSetSample = null;
        SelectedValidationSetStep = null;
        ValidationSetFilter = ValidationSetStatusFilter.All;
        ValidationSetProgress = 0;
        ValidationSetProgressText = string.Empty;
        ClearValidationSetComparePins();
        NotifyValidationSetCountsChanged();
        RebuildOutputCompareCandidates();
        runValidationSetCommand.RaiseCanExecuteChanged();
        clearValidationSetCommand.RaiseCanExecuteChanged();
        RefreshValidationSetSummary();
    }

    private void SetSelectedValidationSampleRole(string? value)
    {
        if (SelectedValidationSetSample is not { } selected
            || !Enum.TryParse<ToolRecipeValidationSampleRole>(
                value,
                ignoreCase: true,
                out var role)
            || !Enum.IsDefined(role)
            || selected.Role == role)
        {
            return;
        }

        var index = validationSetSamples.IndexOf(selected);
        if (index < 0)
        {
            return;
        }

        var updated = selected with
        {
            Role = role,
            Status = "Pending",
            StatusText = Localize("대기", "Pending"),
            Message = Localize(
                "기대 역할이 변경됐습니다. '샘플 세트 실행'을 선택하세요.",
                "Expected role changed; choose Run sample set."),
            Duration = string.Empty,
            Steps = []
        };
        validationSetSamples[index] = updated;
        ClearValidationEvidence();
        SetValidationSetDefinitionDirty(true);
        RefreshFilteredValidationSetSamples();
        SelectedValidationSetSample = validationSetSamples.FirstOrDefault(
            sample => string.Equals(
                sample.SourcePath,
                updated.SourcePath,
                StringComparison.OrdinalIgnoreCase));
        NotifyValidationSetCountsChanged();
        RefreshValidationSetSummary();
        AppendLog(
            "Validation Set",
            $"Sample role changed without execution: {updated.FileName} -> {role}.");
    }

    private void SetValidationSetFilter(string? value)
    {
        if (Enum.TryParse<ValidationSetStatusFilter>(value, ignoreCase: true, out var filter))
        {
            ValidationSetFilter = filter;
        }
    }

    private void RefreshFilteredValidationSetSamples()
    {
        var selectedPath = SelectedValidationSetSample?.SourcePath;
        filteredValidationSetSamples.Clear();
        foreach (var sample in validationSetSamples.Where(MatchesValidationSetFilter))
        {
            filteredValidationSetSamples.Add(sample);
        }

        SelectedValidationSetSample = selectedPath is null
            ? filteredValidationSetSamples.FirstOrDefault()
            : filteredValidationSetSamples.FirstOrDefault(sample =>
                string.Equals(sample.SourcePath, selectedPath, StringComparison.OrdinalIgnoreCase))
              ?? filteredValidationSetSamples.FirstOrDefault();
        OnPropertyChanged(nameof(HasValidationSetSamples));
        previousValidationSetIssueCommand.RaiseCanExecuteChanged();
        nextValidationSetIssueCommand.RaiseCanExecuteChanged();
    }

    private bool MatchesValidationSetFilter(ValidationSetSampleRow sample) =>
        ValidationSetFilter switch
        {
            ValidationSetStatusFilter.Pass => sample.Status == "Pass",
            ValidationSetStatusFilter.Fail => sample.Status == "Fail",
            ValidationSetStatusFilter.Error => sample.Status == "Error",
            _ => true
        };

    private IReadOnlyList<ValidationSetSampleRow> VisibleValidationSetIssues() =>
        filteredValidationSetSamples
            .Where(sample => sample.Status is "Fail" or "Error")
            .ToArray();

    private void MoveValidationSetIssue(int offset)
    {
        var issues = VisibleValidationSetIssues();
        if (issues.Count == 0) return;

        var currentIndex = -1;
        for (var index = 0; index < issues.Count; index++)
        {
            if (ReferenceEquals(issues[index], SelectedValidationSetSample))
            {
                currentIndex = index;
                break;
            }
        }
        var nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + offset + issues.Count) % issues.Count;
        SelectedValidationSetSample = issues[nextIndex];
    }

    private void OpenSelectedValidationSetComparison()
    {
        if (SelectedValidationSetSample is not { } sample || !File.Exists(sample.SourcePath))
        {
            return;
        }

        RebuildOutputCompareCandidates();
        CompareSlotAArtifactId = Source.Id;
        CompareSlotBArtifactId = GetValidationSetCompareArtifactId(sample);
        CompareSlotCArtifactId = string.Empty;
        ValidationSetComparisonRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReportValidationSetProgress(ToolRecipeValidationProgress progress)
    {
        ValidationSetProgress = progress.TotalCount == 0
            ? 0
            : progress.CompletedCount * 100d / progress.TotalCount;
        ValidationSetProgressText = progress.CompletedStatus is null
            ? Localize(
                $"{progress.CompletedCount + 1}/{progress.TotalCount} 실행 중 · {Path.GetFileName(progress.CurrentSourcePath)}",
                $"Running {progress.CompletedCount + 1}/{progress.TotalCount} · {Path.GetFileName(progress.CurrentSourcePath)}")
            : Localize(
                $"{progress.CompletedCount}/{progress.TotalCount} 완료 · {LocalizeStatus(progress.CompletedStatus.Value)}",
                $"{progress.CompletedCount}/{progress.TotalCount} completed · {LocalizeStatus(progress.CompletedStatus.Value)}");
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
        previousValidationSetIssueCommand.RaiseCanExecuteChanged();
        nextValidationSetIssueCommand.RaiseCanExecuteChanged();
        openValidationSetComparisonCommand.RaiseCanExecuteChanged();
        setValidationSampleRoleCommand.RaiseCanExecuteChanged();
    }

    private void SetValidationEvidence(
        ToolRecipeLabeledEvidenceReport report)
    {
        validationEvidenceReport = report;
        validationEvidenceDistributions.Clear();
        foreach (var distribution in report.Distributions)
        {
            validationEvidenceDistributions.Add(
                new ValidationEvidenceDistributionRow(
                    distribution.Scope.ToString(),
                    distribution.OwnerId,
                    distribution.OwnerName,
                    distribution.MetricName,
                    distribution.Unit,
                    FormatStatistics(distribution, ToolRecipeValidationSampleRole.Good),
                    FormatStatistics(distribution, ToolRecipeValidationSampleRole.Bad),
                    FormatStatistics(distribution, ToolRecipeValidationSampleRole.HeldOut)));
        }

        NotifyValidationEvidenceChanged();
    }

    private void ClearValidationEvidence()
    {
        validationThresholdBeforeDevelopmentResult = null;
        validationEvidenceReport = null;
        validationThresholdReport = null;
        validationEvidenceDistributions.Clear();
        validationThresholdCandidates.Clear();
        SelectedValidationThresholdCandidate = null;
        ClearValidationThresholdCorrectionState(
            "Run the labeled Validation Set, select a mapped candidate, then start Review.");
        NotifyValidationEvidenceChanged();
    }

    private void NotifyValidationEvidenceChanged()
    {
        OnPropertyChanged(nameof(HasValidationEvidence));
        OnPropertyChanged(nameof(ValidationEvidenceSummary));
        OnPropertyChanged(nameof(ValidationEvidenceWarning));
        OnPropertyChanged(nameof(HasValidationThresholdCandidates));
        OnPropertyChanged(nameof(ValidationThresholdSummary));
        OnPropertyChanged(nameof(ValidationThresholdWarning));
    }

    private void SetValidationThresholdEvidence(
        ToolRecipeThresholdCandidateReport report)
    {
        ClearValidationThresholdCorrectionState(
            "Select a mapped candidate, then use Review before applying it to the PropertyGrid draft.");
        validationThresholdReport = report;
        validationThresholdCandidates.Clear();
        foreach (var candidate in report.Candidates)
        {
            validationThresholdCandidates.Add(
                new ValidationThresholdCandidateRow(
                    candidate.CandidateId,
                    candidate.Scope.ToString(),
                    candidate.OwnerName,
                    candidate.MetricName,
                    candidate.Unit,
                    candidate.LimitKind.ToString(),
                    FormatThresholdLimits(candidate),
                    candidate.CorrectCount,
                    candidate.ErrorCount,
                    candidate.BadAcceptedCount,
                    candidate.GoodRejectedCount,
                    candidate));
        }

        SelectedValidationThresholdCandidate =
            validationThresholdCandidates.FirstOrDefault();
        NotifyValidationEvidenceChanged();
    }

    private static string FormatThresholdEvidenceWarnings(
        IReadOnlyList<ToolRecipeThresholdEvidenceWarning> warnings)
    {
        const int visibleWarningCount = 3;
        var visible = warnings
            .Take(visibleWarningCount)
            .Select(warning => warning.Message);
        var remaining = warnings.Count - visibleWarningCount;
        return $"{warnings.Count} evidence warning(s): "
               + string.Join(" ", visible)
               + (remaining > 0
                   ? $" +{remaining} more in the Runner contract."
                   : string.Empty);
    }

    private void ReviewSelectedValidationThresholdCandidate()
    {
        if (SelectedValidationThresholdCandidate is not { } selected
            || HasPendingStepParameterChanges)
        {
            ValidationThresholdCorrectionSummary =
                "Finish or discard the current PropertyGrid draft before starting threshold Review.";
            return;
        }

        var document = CreateDocument();
        if (!ToolRecipeThresholdCandidateParameterMapper.TryCreateProposal(
                document,
                selected.Candidate,
                out var proposal,
                out var message)
            || proposal is null)
        {
            ValidationThresholdCorrectionSummary = message;
            return;
        }

        var step = PipelineSteps.FirstOrDefault(item =>
            string.Equals(item.Id, proposal.StepId, StringComparison.Ordinal));
        if (step is null)
        {
            ValidationThresholdCorrectionSummary =
                $"Mapped step '{proposal.StepId}' is not available in the current Workbench.";
            return;
        }

        SelectedPipelineStep = step;
        if (!ReferenceEquals(SelectedPipelineStep, step))
        {
            ValidationThresholdCorrectionSummary =
                "The mapped step could not be selected. Finish the current editing session first.";
            return;
        }

        validationThresholdReviewProposal = proposal;
        validationThresholdCorrectionEvidence = null;
        validationThresholdAfterDevelopmentResult = null;
        validationThresholdManualChanges = [];
        isValidationThresholdReviewActive = true;
        isValidationThresholdCandidateApplied = false;
        isValidationThresholdManualCorrectionCommitted = false;
        isValidationThresholdDevelopmentValidated = false;
        validationThresholdParameterChanges.Clear();
        foreach (var change in proposal.Changes)
        {
            validationThresholdParameterChanges.Add(
                new ValidationThresholdParameterChangeRow(
                    change.ParameterName,
                    change.BeforeValue,
                    change.ProposedValue));
        }
        validationThresholdHeldOutSamples.Clear();
        validationThresholdDevelopmentSamples.Clear();
        ValidationThresholdCorrectionSummary =
            $"{message} Review is read-only until Apply.";
        NotifyValidationThresholdCorrectionChanged();
    }

    private void CancelValidationThresholdReview()
    {
        if (!IsValidationThresholdReviewActive
            || IsValidationThresholdCandidateApplied)
        {
            return;
        }
        ClearValidationThresholdCorrectionState(
            "Threshold Review canceled. Recipe, PropertyGrid draft, and execution state were unchanged.");
    }

    private void ApplyReviewedValidationThresholdCandidate()
    {
        if (!IsValidationThresholdReviewActive
            || validationThresholdReviewProposal is not { } proposal
            || SelectedPipelineStep is not { } step)
        {
            return;
        }
        if (!stepPropertySession.TryApplyThresholdProposal(
                step,
                proposal,
                out var message))
        {
            ValidationThresholdCorrectionSummary = message;
            return;
        }

        isValidationThresholdReviewActive = false;
        isValidationThresholdCandidateApplied = true;
        ValidationThresholdCorrectionSummary =
            RequiresValidationThresholdDevelopmentReplay
                ? "Completeness candidate applied to the PropertyGrid draft only. Explicit development revalidation is required before the separate Held-out replay."
                : "Candidate applied to the PropertyGrid draft only. Recipe Apply remains a separate explicit action; Held-out replay uses a projected copy.";
        NotifyValidationThresholdCorrectionChanged();
    }

    internal async Task RevalidateValidationThresholdCorrectionAsync()
    {
        var candidateReplay =
            RequiresValidationThresholdDevelopmentReplay
            && !IsValidationThresholdManualCorrectionCommitted;
        if (IsValidationSetRunning
            || (!IsValidationThresholdManualCorrectionCommitted
                && !candidateReplay)
            || (IsValidationThresholdManualCorrectionCommitted
                && HasPendingStepParameterChanges)
            || validationThresholdBeforeDevelopmentResult is not { } before
            || validationThresholdReviewProposal is not { } proposal)
        {
            return;
        }

        var beforeMismatchCount = before.Samples.Count(sample =>
            !ToolRecipeThresholdCorrectionEvidenceBuilder.IsExpectedMatch(
                sample));
        if (!candidateReplay && beforeMismatchCount == 0)
        {
            ValidationThresholdCorrectionSummary =
                "Manual correction evidence rejected: the preserved pre-correction development run has no genuine expected-role mismatch.";
            return;
        }

        var development = validationSetSamples
            .Where(sample => sample.Role is
                ToolRecipeValidationSampleRole.Good
                or ToolRecipeValidationSampleRole.Bad)
            .Select(sample => new ToolRecipeValidationSampleInput(
                sample.SourcePath,
                sample.Role))
            .ToArray();
        if (development.Length == 0)
        {
            ValidationThresholdCorrectionSummary =
                "Development revalidation requires Good/Bad samples.";
            return;
        }

        validationSetCancellation?.Dispose();
        validationSetCancellation = new CancellationTokenSource();
        IsValidationSetRunning = true;
        ValidationSetProgress = 0;
        ValidationSetProgressText = "Preparing corrected development replay";
        try
        {
            var progress = new Progress<ToolRecipeValidationProgress>(
                ReportValidationSetProgress);
            var replayDocument = candidateReplay
                ? ToolRecipeThresholdCandidateParameterMapper.ApplyProposal(
                    CreateDocument(),
                    proposal)
                : CreateDocument();
            var result = await Task.Run(() =>
                ToolRecipeValidationSetExecution.Execute(
                    replayDocument,
                    development,
                    validationSetCancellation.Token,
                    progress));
            validationThresholdAfterDevelopmentResult = result;
            var afterMismatchCount = result.Samples.Count(sample =>
                !ToolRecipeThresholdCorrectionEvidenceBuilder.IsExpectedMatch(
                    sample));
            isValidationThresholdDevelopmentValidated =
                result.Samples.Count == development.Length
                && afterMismatchCount == 0;
            validationThresholdDevelopmentSamples.Clear();
            AddDevelopmentRows("Before", before);
            AddDevelopmentRows("After", result);
            ValidationSetProgress = 100;
            ValidationSetProgressText =
                $"{result.Samples.Count} corrected development sample(s) completed";
            ValidationThresholdCorrectionSummary =
                IsValidationThresholdDevelopmentValidated
                    ? candidateReplay
                        ? $"Completeness candidate validated explicitly on development samples: mismatch {beforeMismatchCount}->{afterMismatchCount}. Held-out replay remains separate."
                        : $"Development correction validated explicitly: before mismatch {beforeMismatchCount}, after mismatch 0. Held-out replay remains separate."
                    : candidateReplay
                        ? $"Completeness candidate development replay has {afterMismatchCount} expected-role mismatch(es). Held-out replay remains locked."
                        : $"Development correction is not valid: before mismatch {beforeMismatchCount}, after mismatch {afterMismatchCount}. Held-out replay remains locked.";
            AppendLog(
                "Validation Set",
                $"Threshold {(candidateReplay ? "candidate" : "manual correction")} development replay | beforeMismatch={beforeMismatchCount} | afterMismatch={afterMismatchCount} | heldOutRun=false.");
            NotifyValidationThresholdCorrectionChanged();
        }
        catch (OperationCanceledException)
        {
            ValidationThresholdCorrectionSummary =
                "Corrected development replay canceled. Held-out replay remains locked.";
            ValidationSetProgressText = Localize("?ъ슜??痍⑥냼", "Canceled");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or OverflowException)
        {
            isValidationThresholdDevelopmentValidated = false;
            ValidationThresholdCorrectionSummary =
                $"Corrected development replay could not complete: {exception.Message}";
            AppendLog("Validation Set", exception.Message);
        }
        finally
        {
            IsValidationSetRunning = false;
            validationSetCancellation?.Dispose();
            validationSetCancellation = null;
            RefreshValidationThresholdCorrectionCommands();
        }
    }

    internal async Task ReplayValidationThresholdHeldOutAsync()
    {
        if (IsValidationSetRunning
            || !IsValidationThresholdCandidateApplied
            || validationThresholdReviewProposal is not { } proposal
            || ((IsValidationThresholdManualCorrectionCommitted
                 || RequiresValidationThresholdDevelopmentReplay)
                && (!IsValidationThresholdDevelopmentValidated
                    || validationThresholdBeforeDevelopmentResult is null
                    || validationThresholdAfterDevelopmentResult is null)))
        {
            return;
        }
        var heldOut = validationSetSamples
            .Where(sample =>
                sample.Role == ToolRecipeValidationSampleRole.HeldOut)
            .Select(sample => new ToolRecipeValidationSampleInput(
                sample.SourcePath,
                ToolRecipeValidationSampleRole.HeldOut))
            .ToArray();
        if (heldOut.Length == 0)
        {
            ValidationThresholdCorrectionSummary =
                "Held-out replay requires at least one sample with the HeldOut role.";
            return;
        }

        validationSetCancellation?.Dispose();
        validationSetCancellation = new CancellationTokenSource();
        IsValidationSetRunning = true;
        ValidationSetProgress = 0;
        ValidationSetProgressText =
            Localize("Held-out 재실행 준비 중", "Preparing Held-out replay");
        try
        {
            var projectedDocument =
                IsValidationThresholdManualCorrectionCommitted
                    ? CreateDocument()
                    : ToolRecipeThresholdCandidateParameterMapper.ApplyProposal(
                        CreateDocument(),
                        proposal);
            var progress = new Progress<ToolRecipeValidationProgress>(
                ReportValidationSetProgress);
            var result = await Task.Run(() =>
                ToolRecipeValidationSetExecution.Execute(
                    projectedDocument,
                    heldOut,
                    validationSetCancellation.Token,
                    progress));
            var evidence =
                IsValidationThresholdManualCorrectionCommitted
                    ? ToolRecipeThresholdCorrectionEvidenceBuilder
                        .BuildManualCorrection(
                            projectedDocument,
                            proposal,
                            validationThresholdManualChanges,
                            validationThresholdBeforeDevelopmentResult!,
                            validationThresholdAfterDevelopmentResult!,
                            result)
                    : ToolRecipeThresholdCorrectionEvidenceBuilder.Build(
                        projectedDocument,
                        proposal,
                        result);
            SetValidationThresholdCorrectionEvidence(evidence);
            if (!string.IsNullOrWhiteSpace(RecipePath))
            {
                ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(
                    RecipePath,
                    evidence);
            }

            ValidationSetProgress = 100;
            ValidationSetProgressText =
                Localize(
                    $"Held-out 완료 {result.Samples.Count}개",
                    $"{result.Samples.Count} Held-out completed");
            ValidationThresholdCorrectionSummary =
                IsValidationThresholdManualCorrectionCommitted
                    ? FormatManualCorrectionSummary(evidence)
                    : $"Held-out replay completed against the projected threshold draft only: {result.Message}";
            AppendLog(
                "Validation Set",
                $"Threshold Held-out replay | candidate={proposal.CandidateId} | {result.Message}");
        }
        catch (OperationCanceledException)
        {
            ValidationThresholdCorrectionSummary =
                "Held-out replay canceled. Recipe and PropertyGrid draft were not changed.";
            ValidationSetProgressText = Localize("사용자 취소", "Canceled");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or OverflowException)
        {
            ValidationThresholdCorrectionSummary =
                $"Held-out replay could not complete: {exception.Message}";
            AppendLog("Validation Set", exception.Message);
        }
        finally
        {
            IsValidationSetRunning = false;
            validationSetCancellation?.Dispose();
            validationSetCancellation = null;
        }
    }

    private void SetValidationThresholdCorrectionEvidence(
        ToolRecipeThresholdCorrectionEvidence evidence)
    {
        var preservedCandidateBefore =
            validationThresholdBeforeDevelopmentResult;
        var preservedCandidateAfter =
            validationThresholdAfterDevelopmentResult;
        var preserveCandidateDevelopment =
            evidence.ManualCorrection is null
            && RequiresValidationThresholdDevelopmentReplay
            && isValidationThresholdDevelopmentValidated
            && preservedCandidateBefore is not null
            && preservedCandidateAfter is not null;
        validationThresholdCorrectionEvidence = evidence;
        validationThresholdReviewProposal = evidence.Proposal;
        isValidationThresholdReviewActive = false;
        isValidationThresholdCandidateApplied = true;
        isValidationThresholdManualCorrectionCommitted =
            evidence.ManualCorrection is not null;
        isValidationThresholdDevelopmentValidated =
            evidence.ManualCorrection?.AfterMismatchCount == 0
            || preserveCandidateDevelopment;
        validationThresholdManualChanges =
            evidence.ManualCorrection?.ParameterChanges ?? [];
        validationThresholdBeforeDevelopmentResult =
            preserveCandidateDevelopment
                ? preservedCandidateBefore
                : null;
        validationThresholdAfterDevelopmentResult =
            preserveCandidateDevelopment
                ? preservedCandidateAfter
                : null;
        validationThresholdParameterChanges.Clear();
        foreach (var change in evidence.Proposal.Changes)
        {
            validationThresholdParameterChanges.Add(
                new ValidationThresholdParameterChangeRow(
                    change.ParameterName,
                    change.BeforeValue,
                    change.ProposedValue,
                    evidence.ManualCorrection?.ParameterChanges.FirstOrDefault(
                        manual => string.Equals(
                            manual.ParameterName,
                            change.ParameterName,
                            StringComparison.Ordinal))?.ManualValue
                    ?? string.Empty));
        }
        validationThresholdDevelopmentSamples.Clear();
        if (evidence.ManualCorrection is { } manualCorrection)
        {
            AddDevelopmentRows(
                "Before",
                manualCorrection.BeforeDevelopmentSamples);
            AddDevelopmentRows(
                "After",
                manualCorrection.AfterDevelopmentSamples);
        }
        else if (preserveCandidateDevelopment)
        {
            AddDevelopmentRows("Before", preservedCandidateBefore!);
            AddDevelopmentRows("After", preservedCandidateAfter!);
        }
        validationThresholdHeldOutSamples.Clear();
        foreach (var sample in evidence.HeldOutSamples)
        {
            validationThresholdHeldOutSamples.Add(
                new ValidationThresholdHeldOutSampleRow(
                    sample.SampleOrder,
                    Path.GetFileName(sample.SourcePath),
                    sample.SampleIdentity,
                    sample.Status.ToString(),
                    string.Join(
                        " | ",
                        sample.Metrics.Select(metric =>
                            $"{metric.StepName}.{metric.MetricName}={metric.Value:G6} {metric.Unit} ({metric.Status})"))));
        }
        NotifyValidationThresholdCorrectionChanged();
    }

    private void ClearValidationThresholdCorrectionState(string summary)
    {
        validationThresholdReviewProposal = null;
        validationThresholdCorrectionEvidence = null;
        isValidationThresholdReviewActive = false;
        isValidationThresholdCandidateApplied = false;
        isValidationThresholdManualCorrectionCommitted = false;
        isValidationThresholdDevelopmentValidated = false;
        validationThresholdAfterDevelopmentResult = null;
        validationThresholdManualChanges = [];
        validationThresholdParameterChanges.Clear();
        validationThresholdHeldOutSamples.Clear();
        validationThresholdDevelopmentSamples.Clear();
        ValidationThresholdCorrectionSummary = summary;
        NotifyValidationThresholdCorrectionChanged();
    }

    private void NotifyValidationThresholdCorrectionChanged()
    {
        OnPropertyChanged(nameof(IsValidationThresholdReviewActive));
        OnPropertyChanged(nameof(IsValidationThresholdCandidateApplied));
        OnPropertyChanged(
            nameof(IsValidationThresholdManualCorrectionCommitted));
        OnPropertyChanged(nameof(IsValidationThresholdDevelopmentValidated));
        OnPropertyChanged(nameof(HasValidationThresholdParameterChanges));
        OnPropertyChanged(nameof(HasValidationThresholdHeldOutEvidence));
        OnPropertyChanged(nameof(HasValidationThresholdDevelopmentEvidence));
        RefreshValidationThresholdCorrectionCommands();
    }

    private void RefreshValidationThresholdCorrectionCommands()
    {
        reviewValidationThresholdCandidateCommand?.RaiseCanExecuteChanged();
        cancelValidationThresholdReviewCommand?.RaiseCanExecuteChanged();
        applyValidationThresholdCandidateCommand?.RaiseCanExecuteChanged();
        revalidateValidationThresholdCorrectionCommand?.RaiseCanExecuteChanged();
        replayValidationThresholdHeldOutCommand?.RaiseCanExecuteChanged();
    }

    private void NotifyValidationThresholdDraftCommitted(
        ToolWorkbenchPipelineStepItem step,
        bool changed)
    {
        if (validationThresholdReviewProposal is not { } proposal
            || !string.Equals(
                proposal.StepId,
                step.Id,
                StringComparison.Ordinal))
        {
            return;
        }

        if (!changed)
        {
            ValidationThresholdCorrectionSummary =
                "Normal PropertyGrid Apply found no additional recipe parameter change.";
            RefreshValidationThresholdCorrectionCommands();
            return;
        }

        validationThresholdManualChanges = proposal.Changes.Select(change =>
        {
            var manualValue = step.Parameters.First(parameter =>
                string.Equals(
                    parameter.Name,
                    change.ParameterName,
                    StringComparison.Ordinal)).Value;
            return new ToolRecipeThresholdManualParameterChange(
                change.ParameterName,
                change.ProposedValue,
                manualValue);
        }).ToArray();
        isValidationThresholdManualCorrectionCommitted =
            validationThresholdManualChanges.Any(change => !string.Equals(
                change.SuggestedValue,
                change.ManualValue,
                StringComparison.Ordinal));
        if (!IsValidationThresholdManualCorrectionCommitted)
        {
            ValidationThresholdCorrectionSummary =
                "Candidate values committed by the normal PropertyGrid Apply action. Preview, Publish, Run, and Held-out replay were not invoked.";
            NotifyValidationThresholdCorrectionChanged();
            return;
        }

        isValidationThresholdDevelopmentValidated = false;
        validationThresholdAfterDevelopmentResult = null;
        validationThresholdCorrectionEvidence = null;
        validationThresholdHeldOutSamples.Clear();
        validationThresholdDevelopmentSamples.Clear();
        validationThresholdParameterChanges.Clear();
        foreach (var change in proposal.Changes)
        {
            validationThresholdParameterChanges.Add(
                new ValidationThresholdParameterChangeRow(
                    change.ParameterName,
                    change.BeforeValue,
                    change.ProposedValue,
                    validationThresholdManualChanges.Single(manual =>
                        string.Equals(
                            manual.ParameterName,
                            change.ParameterName,
                            StringComparison.Ordinal)).ManualValue));
        }
        ValidationThresholdCorrectionSummary =
            "Manual values committed through the ordinary PropertyGrid. Explicit development revalidation is required before Held-out replay.";
        NotifyValidationThresholdCorrectionChanged();
        RefreshValidationThresholdCorrectionCommands();
    }

    private void AddDevelopmentRows(
        string stage,
        ToolRecipeValidationSetResult result)
    {
        foreach (var sample in result.Samples)
        {
            validationThresholdDevelopmentSamples.Add(
                new ValidationThresholdDevelopmentSampleRow(
                    stage,
                    sample.Order,
                    Path.GetFileName(sample.SourcePath),
                    sample.SourceContentSha256,
                    sample.Role.ToString(),
                    sample.Status.ToString(),
                    ToolRecipeThresholdCorrectionEvidenceBuilder
                        .IsExpectedMatch(sample)
                        ? "Match"
                        : "Mismatch",
                    FormatDevelopmentMetrics(sample)));
        }
    }

    private void AddDevelopmentRows(
        string stage,
        IReadOnlyList<ToolRecipeThresholdDevelopmentSampleEvidence> samples)
    {
        foreach (var sample in samples)
        {
            validationThresholdDevelopmentSamples.Add(
                new ValidationThresholdDevelopmentSampleRow(
                    stage,
                    sample.SampleOrder,
                    Path.GetFileName(sample.SourcePath),
                    sample.SampleIdentity,
                    sample.Role.ToString(),
                    sample.Status.ToString(),
                    sample.ExpectedMatch ? "Match" : "Mismatch",
                    string.Join(
                        " | ",
                        sample.Metrics.Select(metric =>
                            $"{metric.StepName}.{metric.MetricName}={metric.Value:G6} {metric.Unit} ({metric.Status})"))));
        }
    }

    private static string FormatDevelopmentMetrics(
        ToolRecipeValidationSampleResult sample) =>
        string.Join(
            " | ",
            sample.Steps.SelectMany(step => step.Metrics.Select(metric =>
                $"{step.ToolName}.{metric.Name}={metric.Value:G6} {metric.Unit} ({metric.Status ?? step.Status})")));

    private static string FormatManualCorrectionSummary(
        ToolRecipeThresholdCorrectionEvidence evidence)
    {
        if (evidence.ManualCorrection is not { } manual)
        {
            return evidence.Message;
        }

        var before = string.Join(
            ", ",
            evidence.Proposal.Changes.Select(change =>
                $"{change.ParameterName}={change.BeforeValue}"));
        var suggested = string.Join(
            ", ",
            evidence.Proposal.Changes.Select(change =>
                $"{change.ParameterName}={change.ProposedValue}"));
        var committed = string.Join(
            ", ",
            manual.ParameterChanges.Select(change =>
                $"{change.ParameterName}={change.ManualValue}"));
        var heldOutPassCount = evidence.HeldOutSamples.Count(sample =>
            sample.Status == ResultStatus.Pass);
        return
            $"Before [{before}] | Suggested [{suggested}] | Manual [{committed}] | Development mismatch {manual.BeforeMismatchCount}->{manual.AfterMismatchCount} | Held-out Pass {heldOutPassCount}/{evidence.HeldOutSamples.Count}.";
    }

    private static ToolRecipeValidationSetResult CreateDevelopmentResult(
        ToolRecipeValidationSetResult result)
    {
        var samples = result.Samples.Where(sample =>
            sample.Role is ToolRecipeValidationSampleRole.Good
                or ToolRecipeValidationSampleRole.Bad).ToArray();
        return new ToolRecipeValidationSetResult(
            samples.Any(sample => sample.Status == ResultStatus.Error)
                ? ResultStatus.Error
                : samples.Any(sample => sample.Status == ResultStatus.Fail)
                    ? ResultStatus.Fail
                    : ResultStatus.Pass,
            $"{samples.Length} development sample(s) preserved before correction.",
            result.Duration,
            samples);
    }

    private void NotifyValidationThresholdDraftDiscarded(string? stepId)
    {
        if (validationThresholdReviewProposal is not { } proposal
            || !string.Equals(
                proposal.StepId,
                stepId,
                StringComparison.Ordinal))
        {
            return;
        }

        ClearValidationThresholdCorrectionState(
            "Candidate draft discarded. Recipe parameters and execution state were unchanged.");
    }

    private static string FormatThresholdLimits(
        ToolRecipeThresholdCandidate candidate)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return candidate.LimitKind switch
        {
            ToolRecipeThresholdLimitKind.Minimum =>
                $"≥ {candidate.Minimum?.ToString("G6", culture)}",
            ToolRecipeThresholdLimitKind.Maximum =>
                $"≤ {candidate.Maximum?.ToString("G6", culture)}",
            ToolRecipeThresholdLimitKind.Range =>
                $"{candidate.Minimum?.ToString("G6", culture)} .. "
                + $"{candidate.Maximum?.ToString("G6", culture)}",
            _ => "—"
        };
    }

    private static string FormatStatistics(
        ToolRecipeLabeledMetricDistribution distribution,
        ToolRecipeValidationSampleRole role)
    {
        var statistics = distribution.RoleStatistics.Single(item =>
            item.Role == role);
        return statistics.ValueCount == 0
            ? "—"
            : $"n={statistics.SampleCount} | μ={statistics.Mean:G6} | {statistics.Minimum:G6}..{statistics.Maximum:G6}";
    }

    private void SetValidationSetDefinitionDirty(bool value)
    {
        if (isValidationSetDefinitionDirty == value)
        {
            return;
        }

        isValidationSetDefinitionDirty = value;
        OnPropertyChanged(nameof(IsValidationSetDefinitionDirty));
        OnPropertyChanged(nameof(HasUncommittedRecipeChanges));
        OnPropertyChanged(nameof(RecipeStateSummary));
        OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
    }

    private void SaveValidationSetDefinition(string recipePath)
    {
        if (validationSetSamples.Count == 0)
        {
            var manifestPath =
                ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(
                    recipePath);
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
            SetValidationSetDefinitionDirty(false);
            return;
        }

        var sourceHash = CreateDocument().Source.ContentSha256;
        if (string.IsNullOrWhiteSpace(sourceHash))
        {
            throw new InvalidDataException(
                "Validation Set roles cannot be saved without the identified recipe source SHA-256.");
        }

        ToolRecipeValidationSetDefinitionStore.SaveForRecipe(
            recipePath,
            new ToolRecipeValidationSetDefinition(
                ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
                RecipeName,
                sourceHash,
                validationSetSamples.Select((sample, index) =>
                    new ToolRecipeValidationSampleDefinition(
                        index + 1,
                        sample.SourcePath,
                        sample.Role)).ToArray()));
        SetValidationSetDefinitionDirty(false);
    }

    private void SaveValidationThresholdCorrectionEvidence(string recipePath)
    {
        if (validationThresholdCorrectionEvidence is { } evidence)
        {
            ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(
                recipePath,
                evidence);
            return;
        }

        var path =
            ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(
                recipePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private void LoadValidationSetDefinition(
        string recipePath,
        ToolRecipeDocument document)
    {
        var definition =
            ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath);
        if (definition is null
            || !string.Equals(
                definition.RecipeSourceSha256,
                document.Source.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            validationSetSamples.Clear();
            filteredValidationSetSamples.Clear();
            ClearValidationEvidence();
            SetValidationSetDefinitionDirty(false);
            RefreshFilteredValidationSetSamples();
            NotifyValidationSetCountsChanged();
            RefreshValidationSetSummary();
            return;
        }

        validationSetSamples.Clear();
        foreach (var sample in definition.Samples.OrderBy(sample => sample.Order))
        {
            validationSetSamples.Add(new ValidationSetSampleRow(
                sample.Order,
                sample.SourcePath,
                sample.Role,
                "Pending",
                Localize("대기", "Pending"),
                Localize(
                    "저장된 역할을 불러왔습니다. 명시적 전체 실행을 기다립니다.",
                    "Saved role loaded; waiting for explicit Run All."),
                string.Empty,
                []));
        }

        ClearValidationEvidence();
        SetValidationSetDefinitionDirty(false);
        ValidationSetFilter = ValidationSetStatusFilter.All;
        RefreshFilteredValidationSetSamples();
        NotifyValidationSetCountsChanged();
        RefreshValidationSetSummary();
    }

    private void LoadValidationThresholdCorrectionEvidence(
        string recipePath,
        ToolRecipeDocument document)
    {
        var evidence =
            ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(
                recipePath);
        if (evidence is null)
        {
            return;
        }
        if (!string.Equals(
                evidence.RecipeSourceSha256,
                document.Source.ContentSha256,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                evidence.RecipeName,
                document.Name,
                StringComparison.Ordinal))
        {
            ValidationThresholdCorrectionSummary =
                "Stored threshold correction evidence does not match the current recipe identity.";
            return;
        }

        _ = ToolRecipeThresholdCandidateParameterMapper.ApplyProposal(
            document,
            evidence.Proposal);
        SetValidationThresholdCorrectionEvidence(evidence);
        ValidationThresholdCorrectionSummary =
            evidence.ManualCorrection is null
                ? $"Loaded durable Held-out replay evidence for candidate {evidence.Proposal.CandidateId}."
                : FormatManualCorrectionSummary(evidence);
    }

    private void ClearValidationSetComparePins()
    {
        if (IsValidationSetCompareArtifactId(CompareSlotAArtifactId)) CompareSlotAArtifactId = string.Empty;
        if (IsValidationSetCompareArtifactId(CompareSlotBArtifactId)) CompareSlotBArtifactId = string.Empty;
        if (IsValidationSetCompareArtifactId(CompareSlotCArtifactId)) CompareSlotCArtifactId = string.Empty;
    }

    private void RefreshValidationSetCapability()
    {
        addCurrentSourceToValidationSetCommand?.RaiseCanExecuteChanged();
        if (ToolRecipeValidationSetExecution.CanExecute(CreateDocument(), out var message))
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
        ValidationSetSummary = validationSetSamples.Count == 0
            ? Localize(
                "C3D 샘플을 추가하고 기대 역할을 지정한 다음 '샘플 세트 실행'을 선택하세요. 샘플 선택만으로 검사는 실행되지 않습니다.",
                "Add C3D samples, assign expected roles, then choose Run sample set. Selecting samples never runs inspection.")
            : Localize(
                $"{validationSetSamples.Count}개 샘플 준비됨 · 실행 전",
                $"{validationSetSamples.Count} sample(s) ready · not run");
    }

    private void RefreshValidationSetLocalization()
    {
        RefreshValidationSetCapability();
        for (var index = 0; index < validationSetSamples.Count; index++)
        {
            var sample = validationSetSamples[index];
            var status = Enum.TryParse<ResultStatus>(sample.Status, out var parsed)
                ? parsed
                : (ResultStatus?)null;
            var steps = sample.Steps.Select(step =>
            {
                var stepStatus = Enum.TryParse<ResultStatus>(step.Status, out var parsedStep)
                    ? parsedStep
                    : ResultStatus.Error;
                return step with
                {
                    StatusText = LocalizeStatus(stepStatus),
                    Metrics = step.Metrics.Select(metric => metric with
                    {
                        StatusText = Enum.TryParse<ResultStatus>(metric.Status, out var metricStatus)
                            ? LocalizeStatus(metricStatus)
                            : string.Empty
                    }).ToArray(),
                    Overlays = step.Overlays.Select(overlay => overlay with
                    {
                        StatusText = Enum.TryParse<ResultStatus>(overlay.Status, out var overlayStatus)
                            ? LocalizeStatus(overlayStatus)
                            : string.Empty
                    }).ToArray()
                };
            }).ToArray();
            validationSetSamples[index] = sample with
            {
                StatusText = status is null ? Localize("대기", "Pending") : LocalizeStatus(status.Value),
                Steps = steps
            };
        }

        RefreshFilteredValidationSetSamples();
        NotifyValidationSetCountsChanged();
        RebuildOutputCompareCandidates();
        if (validationSetSamples.All(sample => sample.Status == "Pending"))
        {
            RefreshValidationSetSummary();
        }
        else if (validationSetSamples.Count > 0)
        {
            var pass = validationSetSamples.Count(sample => sample.Status == "Pass");
            var fail = validationSetSamples.Count(sample => sample.Status == "Fail");
            var error = validationSetSamples.Count(sample => sample.Status == "Error");
            ValidationSetSummary = Localize(
                $"완료 {validationSetSamples.Count}개 · 통과 {pass} · 실패 {fail} · 오류 {error}",
                $"Completed {validationSetSamples.Count} · Pass {pass} · Fail {fail} · Error {error}");
        }
    }

    private static string Localize(string korean, string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean;

    private static string LocalizeStatus(ResultStatus status) => status switch
    {
        ResultStatus.Pass => Localize("통과", "Pass"),
        ResultStatus.Fail => Localize("실패", "Fail"),
        ResultStatus.Warning => Localize("경고", "Warning"),
        _ => Localize("오류", "Error")
    };

    private static string LocalizeResultMessage(ToolRecipeValidationSampleResult sample) =>
        sample.Status switch
        {
            ResultStatus.Pass => Localize("모든 지원 검사 단계가 통과했습니다.", "All supported inspection steps passed."),
            ResultStatus.Fail => Localize("하나 이상의 검사 단계가 허용 범위를 벗어났습니다.", "One or more inspection steps are out of tolerance."),
            ResultStatus.Warning => Localize("검사가 경고와 함께 완료되었습니다.", "Inspection completed with warnings."),
            _ => Localize($"실행 오류: {LocalizeValidationError(sample.Message)}", $"Execution error: {sample.Message}")
        };

    private static string LocalizeValidationError(string message)
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

    private static string LocalizeResultSummary(ToolRecipeValidationSetResult result)
    {
        var pass = result.Samples.Count(sample => sample.Status == ResultStatus.Pass);
        var fail = result.Samples.Count(sample => sample.Status == ResultStatus.Fail);
        var error = result.Samples.Count(sample => sample.Status == ResultStatus.Error);
        return Localize(
            $"완료 {result.Samples.Count}개 · 통과 {pass} · 실패 {fail} · 오류 {error} · {result.Duration.TotalMilliseconds:N0} ms",
            $"Completed {result.Samples.Count} · Pass {pass} · Fail {fail} · Error {error} · {result.Duration.TotalMilliseconds:N0} ms");
    }
}

public sealed record ValidationSetSampleRow(
    int Order,
    string SourcePath,
    ToolRecipeValidationSampleRole Role,
    string Status,
    string StatusText,
    string Message,
    string Duration,
    IReadOnlyList<ValidationSetStepRow> Steps)
{
    public string FileName => Path.GetFileName(SourcePath);
    public string RoleText => Role == ToolRecipeValidationSampleRole.HeldOut
        ? "Held-out"
        : Role.ToString();
}

public sealed record ValidationEvidenceDistributionRow(
    string Scope,
    string OwnerId,
    string OwnerName,
    string MetricName,
    string Unit,
    string Good,
    string Bad,
    string HeldOut);

public sealed record ValidationThresholdCandidateRow(
    string CandidateId,
    string Scope,
    string OwnerName,
    string MetricName,
    string Unit,
    string LimitKind,
    string Limits,
    int CorrectCount,
    int ErrorCount,
    int FalseAcceptCount,
    int FalseRejectCount,
    ToolRecipeThresholdCandidate Candidate);

public sealed record ValidationThresholdDecisionRow(
    int SampleOrder,
    string FileName,
    string SampleIdentity,
    string ExpectedRole,
    string PredictedRole,
    string Decision,
    string Value,
    string EvidenceLocator);

public sealed record ValidationThresholdParameterChangeRow(
    string ParameterName,
    string BeforeValue,
    string ProposedValue,
    string ManualValue = "");

public sealed record ValidationThresholdDevelopmentSampleRow(
    string Stage,
    int SampleOrder,
    string FileName,
    string SampleIdentity,
    string Role,
    string Status,
    string ExpectedMatch,
    string Metrics);

public sealed record ValidationThresholdHeldOutSampleRow(
    int SampleOrder,
    string FileName,
    string SampleIdentity,
    string Status,
    string Metrics);

public sealed record ValidationSetStepRow(
    int Order,
    string StepId,
    string ToolName,
    string Status,
    string StatusText,
    string Evidence,
    IReadOnlyList<ValidationSetMetricRow> Metrics,
    IReadOnlyList<ValidationSetOverlayRow> Overlays);

public sealed record ValidationSetMetricRow(
    string Name,
    string Value,
    string Unit,
    string Status,
    string StatusText);

public sealed record ValidationSetOverlayRow(
    string Kind,
    string Label,
    string Status,
    string StatusText);

public sealed record ValidationFailureCorrectionContext(
    string SourcePath,
    string SampleName,
    string SampleStatus,
    string StepId,
    string ToolName,
    string Reason,
    string CellSummary);

public enum ValidationSetStatusFilter
{
    All,
    Pass,
    Fail,
    Error
}
