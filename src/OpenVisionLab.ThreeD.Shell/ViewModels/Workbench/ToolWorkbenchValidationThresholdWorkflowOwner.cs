using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns Validation Set analysis evidence and the explicit threshold workflow
/// from proposal through development and Held-out replay. Canonical sample
/// projection, review navigation, role editing, and definition persistence
/// remain with their existing owners.
/// </summary>
internal sealed class ToolWorkbenchValidationThresholdWorkflowOwner :
    INotifyPropertyChanged,
    IDisposable
{
    private readonly ToolWorkbenchValidationSetExecutionOwner executionOwner;
    private readonly IReadOnlyList<ValidationSetSampleRow> samples;
    private readonly IReadOnlyList<ToolWorkbenchPipelineStepItem> pipelineSteps;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<ToolWorkbenchPipelineStepItem, bool> trySelectPipelineStep;
    private readonly ToolWorkbenchStepPropertySession stepPropertySession;
    private readonly Func<string?> getRecipePath;
    private readonly Action<string, string> appendLog;
    private readonly Func<string, string, string> localize;
    private readonly Func<ResultStatus, string> localizeStatus;
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
    private readonly RelayCommand proposeValidationThresholdCandidateCommand;
    private readonly RelayCommand reviewValidationThresholdCandidateCommand;
    private readonly RelayCommand cancelValidationThresholdReviewCommand;
    private readonly RelayCommand applyValidationThresholdCandidateCommand;
    private readonly RelayCommand revalidateValidationThresholdCorrectionCommand;
    private readonly RelayCommand replayValidationThresholdHeldOutCommand;
    private string validationSetProgressText = string.Empty;
    private double validationSetProgress;
    private ToolRecipeLabeledEvidenceReport? validationEvidenceReport;
    private ToolRecipeThresholdCandidateReport? validationThresholdReport;
    private ValidationThresholdCandidateRow? selectedValidationThresholdCandidate;
    private ToolRecipeThresholdParameterProposal? validationThresholdReviewProposal;
    private ToolRecipeThresholdCorrectionEvidence? validationThresholdCorrectionEvidence;
    private ToolRecipeValidationSetResult? validationThresholdBeforeDevelopmentResult;
    private ToolRecipeValidationSetResult? validationThresholdAfterDevelopmentResult;
    private IReadOnlyList<ToolRecipeThresholdManualParameterChange>
        validationThresholdManualChanges = [];
    private bool isValidationThresholdReviewActive;
    private bool isValidationThresholdCandidateApplied;
    private bool isValidationThresholdManualCorrectionCommitted;
    private bool isValidationThresholdDevelopmentValidated;
    private string validationThresholdCorrectionSummary =
        "Select a mapped candidate, then use Review before applying it to the PropertyGrid draft.";
    private int disposalState;

    public ToolWorkbenchValidationThresholdWorkflowOwner(
        ToolWorkbenchValidationSetExecutionOwner executionOwner,
        IReadOnlyList<ValidationSetSampleRow> samples,
        IReadOnlyList<ToolWorkbenchPipelineStepItem> pipelineSteps,
        Func<ToolRecipeDocument> createDocument,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<ToolWorkbenchPipelineStepItem, bool> trySelectPipelineStep,
        ToolWorkbenchStepPropertySession stepPropertySession,
        Func<string?> getRecipePath,
        Action<string, string> appendLog,
        Func<string, string, string> localize,
        Func<ResultStatus, string> localizeStatus)
    {
        this.executionOwner = executionOwner
            ?? throw new ArgumentNullException(nameof(executionOwner));
        this.samples = samples ?? throw new ArgumentNullException(nameof(samples));
        this.pipelineSteps = pipelineSteps
            ?? throw new ArgumentNullException(nameof(pipelineSteps));
        this.createDocument = createDocument
            ?? throw new ArgumentNullException(nameof(createDocument));
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges
            ?? throw new ArgumentNullException(nameof(hasPendingStepParameterChanges));
        this.getSelectedPipelineStep = getSelectedPipelineStep
            ?? throw new ArgumentNullException(nameof(getSelectedPipelineStep));
        this.trySelectPipelineStep = trySelectPipelineStep
            ?? throw new ArgumentNullException(nameof(trySelectPipelineStep));
        this.stepPropertySession = stepPropertySession
            ?? throw new ArgumentNullException(nameof(stepPropertySession));
        this.getRecipePath = getRecipePath
            ?? throw new ArgumentNullException(nameof(getRecipePath));
        this.appendLog = appendLog
            ?? throw new ArgumentNullException(nameof(appendLog));
        this.localize = localize ?? throw new ArgumentNullException(nameof(localize));
        this.localizeStatus = localizeStatus
            ?? throw new ArgumentNullException(nameof(localizeStatus));

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
        proposeValidationThresholdCandidateCommand = new RelayCommand(
            _ => ProposeSelectedValidationThresholdCandidate(),
            _ => CanInteract
                 && !hasPendingStepParameterChanges()
                 && HasSelectedValidationThresholdCandidate
                 && !HasValidationThresholdAssistantProposal
                 && !IsValidationThresholdReviewActive
                 && !IsValidationThresholdCandidateApplied);
        reviewValidationThresholdCandidateCommand = new RelayCommand(
            _ => ReviewSelectedValidationThresholdCandidate(),
            _ => CanInteract
                 && !hasPendingStepParameterChanges()
                 && HasSelectedValidationThresholdCandidate
                 && !IsValidationThresholdReviewActive
                 && !IsValidationThresholdCandidateApplied);
        cancelValidationThresholdReviewCommand = new RelayCommand(
            _ => CancelValidationThresholdReview(),
            _ => CanInteract
                 && HasValidationThresholdAssistantProposal
                 && !IsValidationThresholdCandidateApplied);
        applyValidationThresholdCandidateCommand = new RelayCommand(
            _ => ApplyReviewedValidationThresholdCandidate(),
            _ => CanInteract
                 && IsValidationThresholdReviewActive
                 && !IsValidationThresholdCandidateApplied);
        revalidateValidationThresholdCorrectionCommand = new RelayCommand(
            _ => _ = RevalidateAsync(),
            _ => CanInteract
                 && (IsValidationThresholdManualCorrectionCommitted
                     || RequiresValidationThresholdDevelopmentReplay)
                 && IsValidationThresholdCandidateApplied
                 && !IsValidationThresholdDevelopmentValidated
                 && (!IsValidationThresholdManualCorrectionCommitted
                     || !hasPendingStepParameterChanges())
                 && validationThresholdBeforeDevelopmentResult is not null);
        replayValidationThresholdHeldOutCommand = new RelayCommand(
            _ => _ = ReplayHeldOutAsync(),
            _ => CanInteract
                 && IsValidationThresholdCandidateApplied
                 && ((!IsValidationThresholdManualCorrectionCommitted
                      && !RequiresValidationThresholdDevelopmentReplay)
                     || (IsValidationThresholdDevelopmentValidated
                         && validationThresholdBeforeDevelopmentResult is not null
                         && validationThresholdAfterDevelopmentResult is not null))
                 && samples.Any(sample =>
                     sample.Role == ToolRecipeValidationSampleRole.HeldOut));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ValidationEvidenceDistributionRow>
        ValidationEvidenceDistributions { get; }

    public ReadOnlyObservableCollection<ValidationThresholdCandidateRow>
        ValidationThresholdCandidates { get; }

    public ReadOnlyObservableCollection<ValidationThresholdDecisionRow>
        SelectedValidationThresholdDecisions { get; }

    public ReadOnlyObservableCollection<ValidationThresholdParameterChangeRow>
        ValidationThresholdParameterChanges { get; }

    public ReadOnlyObservableCollection<ValidationThresholdHeldOutSampleRow>
        ValidationThresholdHeldOutSamples { get; }

    public ReadOnlyObservableCollection<ValidationThresholdDevelopmentSampleRow>
        ValidationThresholdDevelopmentSamples { get; }

    public ICommand ProposeValidationThresholdCandidateCommand =>
        proposeValidationThresholdCandidateCommand;

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

    public bool IsRunning => executionOwner.IsRunning;

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool CanInteract => !IsDisposed && !IsRunning;

    public bool HasValidationEvidence => validationEvidenceDistributions.Count > 0;

    public bool HasValidationThresholdCandidates =>
        validationThresholdCandidates.Count > 0;

    public bool HasValidationThresholdAssistantAnalysis =>
        validationThresholdReport is not null;

    public bool HasValidationThresholdAssistantProposal =>
        validationThresholdReviewProposal is not null;

    public ValidationThresholdAssistantStage ValidationThresholdAssistantStage =>
        !HasValidationThresholdAssistantAnalysis
        || !HasValidationThresholdCandidates
        || IsRunning
            ? ValidationThresholdAssistantStage.Analyze
            : IsValidationThresholdCandidateApplied
                ? ValidationThresholdAssistantStage.Apply
                : IsValidationThresholdReviewActive
                    ? ValidationThresholdAssistantStage.Review
                    : ValidationThresholdAssistantStage.Propose;

    public string ValidationThresholdAssistantStageText =>
        ValidationThresholdAssistantStage switch
        {
            ValidationThresholdAssistantStage.Analyze => localize("분석", "Analyze"),
            ValidationThresholdAssistantStage.Propose => localize("제안", "Propose"),
            ValidationThresholdAssistantStage.Review => localize("검토", "Review"),
            ValidationThresholdAssistantStage.Apply => localize("초안 적용", "Apply draft"),
            _ => localize("분석", "Analyze")
        };

    public string ValidationThresholdAssistantSummary =>
        IsRunning
            ? localize(
                "Validation Set을 분석하는 중입니다. 완료될 때까지 적용할 수 없습니다.",
                "Analyzing the Validation Set. Apply remains unavailable until it completes.")
            : !HasValidationThresholdAssistantAnalysis
                ? localize(
                    "분석을 실행하면 결정론적 임계값 후보가 생성됩니다.",
                    "Run analysis to generate deterministic threshold candidates.")
                : !HasSelectedValidationThresholdCandidate
                    ? localize(
                        "후보를 선택한 뒤 제안을 만들 수 있습니다.",
                        "Select a candidate to create a proposal.")
                    : IsValidationThresholdCandidateApplied
                        ? localize(
                            "PropertyGrid 초안에만 적용되었습니다. 일반 Apply와 실행은 별도입니다.",
                            "Applied to the PropertyGrid draft only. Normal Apply and execution remain separate.")
                        : IsValidationThresholdReviewActive
                            ? localize(
                                "제안을 검토 중입니다. Apply 전에는 레시피와 실행 상태가 변하지 않습니다.",
                                "Reviewing the proposal. Recipe and execution remain unchanged until Apply.")
                            : HasValidationThresholdAssistantProposal
                                ? localize(
                                    "제안이 준비되었습니다. 검토를 거친 뒤 초안에 적용하세요.",
                                    "Proposal ready. Review it before applying to the draft.")
                                : localize(
                                    "선택한 후보에서 초안 제안을 만드세요.",
                                    "Create a draft proposal from the selected candidate.");

    public ValidationThresholdCandidateRow? SelectedValidationThresholdCandidate
    {
        get => selectedValidationThresholdCandidate;
        set
        {
            if (IsDisposed
                || ReferenceEquals(selectedValidationThresholdCandidate, value))
            {
                return;
            }

            if (validationThresholdReviewProposal is { } proposal
                && !string.Equals(
                    proposal.CandidateId,
                    value?.CandidateId,
                    StringComparison.Ordinal))
            {
                ClearCorrectionState(
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
            OnPropertyChanged(nameof(HasSelectedValidationThresholdCandidate));
            OnPropertyChanged(nameof(ValidationThresholdAssistantStage));
            OnPropertyChanged(nameof(ValidationThresholdAssistantStageText));
            OnPropertyChanged(nameof(ValidationThresholdAssistantSummary));
            RefreshCommandStates();
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
            if (IsDisposed
                || string.Equals(
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

    public string ValidationSetProgressText
    {
        get => validationSetProgressText;
        private set
        {
            if (IsDisposed || validationSetProgressText == value)
            {
                return;
            }
            validationSetProgressText = value;
            OnPropertyChanged();
        }
    }

    public double ValidationSetProgress
    {
        get => validationSetProgress;
        private set
        {
            if (IsDisposed || Math.Abs(validationSetProgress - value) < 0.001)
            {
                return;
            }
            validationSetProgress = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Releases threshold evidence and workflow state owned by this owner.
    /// The Workbench disposes the shared execution owner separately after this
    /// state boundary has stopped accepting continuations.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        validationSetProgressText = string.Empty;
        validationSetProgress = 0;
        validationEvidenceReport = null;
        validationThresholdReport = null;
        selectedValidationThresholdCandidate = null;
        validationThresholdReviewProposal = null;
        validationThresholdCorrectionEvidence = null;
        validationThresholdBeforeDevelopmentResult = null;
        validationThresholdAfterDevelopmentResult = null;
        validationThresholdManualChanges = [];
        isValidationThresholdReviewActive = false;
        isValidationThresholdCandidateApplied = false;
        isValidationThresholdManualCorrectionCommitted = false;
        isValidationThresholdDevelopmentValidated = false;
        validationThresholdCorrectionSummary = string.Empty;
        validationEvidenceDistributions.Clear();
        validationThresholdCandidates.Clear();
        selectedValidationThresholdDecisions.Clear();
        validationThresholdParameterChanges.Clear();
        validationThresholdHeldOutSamples.Clear();
        validationThresholdDevelopmentSamples.Clear();
        PropertyChanged = null;
    }

    public async Task<ToolRecipeValidationSetResult> AnalyzeAsync(
        ToolRecipeDocument document,
        IReadOnlyList<ToolRecipeValidationSampleInput> sampleInputs,
        Action<ToolRecipeValidationSetResult> projectSamples)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sampleInputs);
        ArgumentNullException.ThrowIfNull(projectSamples);
        if (IsDisposed)
        {
            throw new OperationCanceledException();
        }

        validationThresholdBeforeDevelopmentResult = null;
        ValidationSetProgress = 0;
        ValidationSetProgressText = localize(
            "반복 검증 준비 중",
            "Preparing repeat validation");
        try
        {
            var progress = new Progress<ToolRecipeValidationProgress>(ReportProgress);
            var result = await executionOwner.ExecuteAsync(
                document,
                sampleInputs,
                progress);
            if (IsDisposed)
            {
                throw new OperationCanceledException();
            }

            projectSamples(result);
            SetValidationEvidence(
                ToolRecipeLabeledEvidenceAnalyzer.Analyze(document, result));
            SetValidationThresholdEvidence(
                ToolRecipeThresholdCandidateAnalyzer.Analyze(document, result));
            validationThresholdBeforeDevelopmentResult =
                CreateDevelopmentResult(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                ValidationSetProgressText = localize("사용자 취소", "Canceled");
            }

            throw;
        }
        catch (ObjectDisposedException) when (IsDisposed)
        {
            throw new OperationCanceledException();
        }
    }

    public void CompleteAnalysis(int sampleCount)
    {
        if (IsDisposed)
        {
            return;
        }

        ValidationSetProgress = 100;
        ValidationSetProgressText = localize(
            $"완료 {sampleCount}개",
            $"{sampleCount} completed");
    }

    public void ResetProgress()
    {
        if (IsDisposed)
        {
            return;
        }

        ValidationSetProgress = 0;
        ValidationSetProgressText = string.Empty;
    }

    public void ClearAnalysis()
    {
        if (IsDisposed)
        {
            return;
        }

        validationThresholdBeforeDevelopmentResult = null;
        validationEvidenceReport = null;
        validationThresholdReport = null;
        validationEvidenceDistributions.Clear();
        validationThresholdCandidates.Clear();
        SelectedValidationThresholdCandidate = null;
        ClearCorrectionState(
            "Run the labeled Validation Set, select a mapped candidate, then start Review.");
        NotifyValidationEvidenceChanged();
    }

    public async Task RevalidateAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        var candidateReplay =
            RequiresValidationThresholdDevelopmentReplay
            && !IsValidationThresholdManualCorrectionCommitted;
        if (IsRunning
            || (!IsValidationThresholdManualCorrectionCommitted
                && !candidateReplay)
            || (IsValidationThresholdManualCorrectionCommitted
                && hasPendingStepParameterChanges())
            || validationThresholdBeforeDevelopmentResult is not { } before
            || validationThresholdReviewProposal is not { } proposal)
        {
            return;
        }

        var beforeMismatchCount = before.Samples.Count(sample =>
            !ToolRecipeThresholdCorrectionEvidenceBuilder.IsExpectedMatch(sample));
        if (!candidateReplay && beforeMismatchCount == 0)
        {
            ValidationThresholdCorrectionSummary =
                "Manual correction evidence rejected: the preserved pre-correction development run has no genuine expected-role mismatch.";
            return;
        }

        var development = samples
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

        ValidationSetProgress = 0;
        ValidationSetProgressText = "Preparing corrected development replay";
        try
        {
            var progress = new Progress<ToolRecipeValidationProgress>(ReportProgress);
            var replayDocument = candidateReplay
                ? ToolRecipeThresholdCandidateParameterMapper.ApplyProposal(
                    createDocument(),
                    proposal)
                : createDocument();
            var result = await executionOwner.ExecuteAsync(
                replayDocument,
                development,
                progress);
            if (IsDisposed)
            {
                return;
            }

            validationThresholdAfterDevelopmentResult = result;
            var afterMismatchCount = result.Samples.Count(sample =>
                !ToolRecipeThresholdCorrectionEvidenceBuilder.IsExpectedMatch(sample));
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
            appendLog(
                "Validation Set",
                $"Threshold {(candidateReplay ? "candidate" : "manual correction")} development replay | beforeMismatch={beforeMismatchCount} | afterMismatch={afterMismatchCount} | heldOutRun=false.");
            NotifyCorrectionChanged();
        }
        catch (OperationCanceledException)
        {
            if (IsDisposed)
            {
                return;
            }

            ValidationThresholdCorrectionSummary =
                "Corrected development replay canceled. Held-out replay remains locked.";
            ValidationSetProgressText = localize("?ъ슜??痍⑥냼", "Canceled");
        }
        catch (ObjectDisposedException) when (IsDisposed)
        {
            return;
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

            isValidationThresholdDevelopmentValidated = false;
            ValidationThresholdCorrectionSummary =
                $"Corrected development replay could not complete: {exception.Message}";
            appendLog("Validation Set", exception.Message);
        }
        finally
        {
            if (!IsDisposed)
            {
                RefreshCommandStates();
            }
        }
    }

    public async Task ReplayHeldOutAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        if (IsRunning
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
        var heldOut = samples
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

        ValidationSetProgress = 0;
        ValidationSetProgressText = localize(
            "Held-out 재실행 준비 중",
            "Preparing Held-out replay");
        try
        {
            var projectedDocument =
                IsValidationThresholdManualCorrectionCommitted
                    ? createDocument()
                    : ToolRecipeThresholdCandidateParameterMapper.ApplyProposal(
                        createDocument(),
                        proposal);
            var progress = new Progress<ToolRecipeValidationProgress>(ReportProgress);
            var result = await executionOwner.ExecuteAsync(
                projectedDocument,
                heldOut,
                progress);
            if (IsDisposed)
            {
                return;
            }

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
            SetCorrectionEvidence(evidence);
            var recipePath = getRecipePath();
            if (!string.IsNullOrWhiteSpace(recipePath))
            {
                ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(
                    recipePath,
                    evidence);
            }

            ValidationSetProgress = 100;
            ValidationSetProgressText = localize(
                $"Held-out 완료 {result.Samples.Count}개",
                $"{result.Samples.Count} Held-out completed");
            ValidationThresholdCorrectionSummary =
                IsValidationThresholdManualCorrectionCommitted
                    ? FormatManualCorrectionSummary(evidence)
                    : $"Held-out replay completed against the projected threshold draft only: {result.Message}";
            appendLog(
                "Validation Set",
                $"Threshold Held-out replay | candidate={proposal.CandidateId} | {result.Message}");
        }
        catch (OperationCanceledException)
        {
            if (IsDisposed)
            {
                return;
            }

            ValidationThresholdCorrectionSummary =
                "Held-out replay canceled. Recipe and PropertyGrid draft were not changed.";
            ValidationSetProgressText = localize("사용자 취소", "Canceled");
        }
        catch (ObjectDisposedException) when (IsDisposed)
        {
            return;
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

            ValidationThresholdCorrectionSummary =
                $"Held-out replay could not complete: {exception.Message}";
            appendLog("Validation Set", exception.Message);
        }
        finally
        {
            if (!IsDisposed)
            {
                RefreshCommandStates();
            }
        }
    }

    public void RefreshCommandStates()
    {
        if (IsDisposed)
        {
            return;
        }

        proposeValidationThresholdCandidateCommand.RaiseCanExecuteChanged();
        reviewValidationThresholdCandidateCommand.RaiseCanExecuteChanged();
        cancelValidationThresholdReviewCommand.RaiseCanExecuteChanged();
        applyValidationThresholdCandidateCommand.RaiseCanExecuteChanged();
        revalidateValidationThresholdCorrectionCommand.RaiseCanExecuteChanged();
        replayValidationThresholdHeldOutCommand.RaiseCanExecuteChanged();
    }

    public void NotifyDraftCommitted(
        ToolWorkbenchPipelineStepItem step,
        bool changed)
    {
        if (IsDisposed)
        {
            return;
        }

        if (validationThresholdReviewProposal is not { } proposal
            || !string.Equals(proposal.StepId, step.Id, StringComparison.Ordinal))
        {
            return;
        }

        if (!changed)
        {
            ValidationThresholdCorrectionSummary =
                "Normal PropertyGrid Apply found no additional recipe parameter change.";
            RefreshCommandStates();
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
            NotifyCorrectionChanged();
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
        NotifyCorrectionChanged();
        RefreshCommandStates();
    }

    public void NotifyDraftDiscarded(string? stepId)
    {
        if (IsDisposed)
        {
            return;
        }

        if (validationThresholdReviewProposal is not { } proposal
            || !string.Equals(proposal.StepId, stepId, StringComparison.Ordinal))
        {
            return;
        }

        ClearCorrectionState(
            "Candidate draft discarded. Recipe parameters and execution state were unchanged.");
    }

    public void SaveCorrectionEvidence(string recipePath)
    {
        if (IsDisposed)
        {
            return;
        }

        if (validationThresholdCorrectionEvidence is { } evidence)
        {
            ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(
                recipePath,
                evidence);
            return;
        }

        var path = ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(
            recipePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void LoadCorrectionEvidence(
        string recipePath,
        ToolRecipeDocument document)
    {
        if (IsDisposed)
        {
            return;
        }

        var evidence =
            ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(recipePath);
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
        SetCorrectionEvidence(evidence);
        ValidationThresholdCorrectionSummary =
            evidence.ManualCorrection is null
                ? $"Loaded durable Held-out replay evidence for candidate {evidence.Proposal.CandidateId}."
                : FormatManualCorrectionSummary(evidence);
    }

    public void RefreshLocalization()
    {
        if (IsDisposed)
        {
            return;
        }

        OnPropertyChanged(nameof(ValidationThresholdAssistantStageText));
        OnPropertyChanged(nameof(ValidationThresholdAssistantSummary));
    }

    private bool RequiresValidationThresholdDevelopmentReplay =>
        string.Equals(
            validationThresholdReviewProposal?.ToolId,
            "completeness-grid",
            StringComparison.Ordinal);

    private void SetValidationEvidence(ToolRecipeLabeledEvidenceReport report)
    {
        if (IsDisposed)
        {
            return;
        }

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
                    FormatStatistics(
                        distribution,
                        ToolRecipeValidationSampleRole.Good),
                    FormatStatistics(
                        distribution,
                        ToolRecipeValidationSampleRole.Bad),
                    FormatStatistics(
                        distribution,
                        ToolRecipeValidationSampleRole.HeldOut)));
        }

        NotifyValidationEvidenceChanged();
    }

    private void NotifyValidationEvidenceChanged()
    {
        OnPropertyChanged(nameof(HasValidationEvidence));
        OnPropertyChanged(nameof(ValidationEvidenceSummary));
        OnPropertyChanged(nameof(ValidationEvidenceWarning));
        OnPropertyChanged(nameof(HasValidationThresholdCandidates));
        OnPropertyChanged(nameof(HasValidationThresholdAssistantAnalysis));
        OnPropertyChanged(nameof(ValidationThresholdAssistantStage));
        OnPropertyChanged(nameof(ValidationThresholdAssistantStageText));
        OnPropertyChanged(nameof(ValidationThresholdAssistantSummary));
        OnPropertyChanged(nameof(ValidationThresholdSummary));
        OnPropertyChanged(nameof(ValidationThresholdWarning));
    }

    private void SetValidationThresholdEvidence(
        ToolRecipeThresholdCandidateReport report)
    {
        if (IsDisposed)
        {
            return;
        }

        ClearCorrectionState(
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

    private void ProposeSelectedValidationThresholdCandidate()
    {
        if (!TryPrepareValidationThresholdProposal(out var message))
        {
            return;
        }

        isValidationThresholdReviewActive = false;
        ValidationThresholdCorrectionSummary =
            $"{message} Proposal ready. Review it before Apply.";
        NotifyCorrectionChanged();
    }

    private void ReviewSelectedValidationThresholdCandidate()
    {
        if (!TryPrepareValidationThresholdProposal(out var message))
        {
            return;
        }

        isValidationThresholdReviewActive = true;
        ValidationThresholdCorrectionSummary =
            $"{message} Review is read-only until Apply.";
        NotifyCorrectionChanged();
    }

    private bool TryPrepareValidationThresholdProposal(out string message)
    {
        if (SelectedValidationThresholdCandidate is not { } selected
            || hasPendingStepParameterChanges())
        {
            message =
                "Finish or discard the current PropertyGrid draft before creating a threshold proposal.";
            ValidationThresholdCorrectionSummary = message;
            return false;
        }

        var document = createDocument();
        if (!ToolRecipeThresholdCandidateParameterMapper.TryCreateProposal(
                document,
                selected.Candidate,
                out var proposal,
                out message)
            || proposal is null)
        {
            ValidationThresholdCorrectionSummary = message;
            return false;
        }

        var step = pipelineSteps.FirstOrDefault(item =>
            string.Equals(item.Id, proposal.StepId, StringComparison.Ordinal));
        if (step is null)
        {
            message =
                $"Mapped step '{proposal.StepId}' is not available in the current Workbench.";
            ValidationThresholdCorrectionSummary = message;
            return false;
        }

        if (!trySelectPipelineStep(step))
        {
            message =
                "The mapped step could not be selected. Finish the current editing session first.";
            ValidationThresholdCorrectionSummary = message;
            return false;
        }

        validationThresholdReviewProposal = proposal;
        validationThresholdCorrectionEvidence = null;
        validationThresholdAfterDevelopmentResult = null;
        validationThresholdManualChanges = [];
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
        return true;
    }

    private void CancelValidationThresholdReview()
    {
        if (!HasValidationThresholdAssistantProposal
            || IsValidationThresholdCandidateApplied)
        {
            return;
        }
        ClearCorrectionState(
            "Threshold Review canceled. Recipe, PropertyGrid draft, and execution state were unchanged.");
    }

    private void ApplyReviewedValidationThresholdCandidate()
    {
        if (!IsValidationThresholdReviewActive
            || validationThresholdReviewProposal is not { } proposal
            || getSelectedPipelineStep() is not { } step)
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
        NotifyCorrectionChanged();
    }

    private void SetCorrectionEvidence(
        ToolRecipeThresholdCorrectionEvidence evidence)
    {
        if (IsDisposed)
        {
            return;
        }

        var preservedCandidateBefore = validationThresholdBeforeDevelopmentResult;
        var preservedCandidateAfter = validationThresholdAfterDevelopmentResult;
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
            preserveCandidateDevelopment ? preservedCandidateBefore : null;
        validationThresholdAfterDevelopmentResult =
            preserveCandidateDevelopment ? preservedCandidateAfter : null;
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
        NotifyCorrectionChanged();
    }

    private void ClearCorrectionState(string summary)
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
        NotifyCorrectionChanged();
    }

    private void NotifyCorrectionChanged()
    {
        OnPropertyChanged(nameof(IsValidationThresholdReviewActive));
        OnPropertyChanged(nameof(IsValidationThresholdCandidateApplied));
        OnPropertyChanged(nameof(HasValidationThresholdAssistantProposal));
        OnPropertyChanged(nameof(ValidationThresholdAssistantStage));
        OnPropertyChanged(nameof(ValidationThresholdAssistantStageText));
        OnPropertyChanged(nameof(ValidationThresholdAssistantSummary));
        OnPropertyChanged(nameof(IsValidationThresholdManualCorrectionCommitted));
        OnPropertyChanged(nameof(IsValidationThresholdDevelopmentValidated));
        OnPropertyChanged(nameof(HasValidationThresholdParameterChanges));
        OnPropertyChanged(nameof(HasValidationThresholdHeldOutEvidence));
        OnPropertyChanged(nameof(HasValidationThresholdDevelopmentEvidence));
        RefreshCommandStates();
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

    private void ReportProgress(ToolRecipeValidationProgress progress)
    {
        ValidationSetProgress = progress.TotalCount == 0
            ? 0
            : progress.CompletedCount * 100d / progress.TotalCount;
        ValidationSetProgressText = progress.CompletedStatus is null
            ? localize(
                $"{progress.CompletedCount + 1}/{progress.TotalCount} 실행 중 · {Path.GetFileName(progress.CurrentSourcePath)}",
                $"Running {progress.CompletedCount + 1}/{progress.TotalCount} · {Path.GetFileName(progress.CurrentSourcePath)}")
            : localize(
                $"{progress.CompletedCount}/{progress.TotalCount} 완료 · {localizeStatus(progress.CompletedStatus.Value)}",
                $"{progress.CompletedCount}/{progress.TotalCount} completed · {localizeStatus(progress.CompletedStatus.Value)}");
    }

    private static string FormatThresholdEvidenceWarnings(
        IReadOnlyList<ToolRecipeThresholdEvidenceWarning> warnings)
    {
        const int visibleWarningCount = 3;
        var visible = warnings.Take(visibleWarningCount).Select(warning => warning.Message);
        var remaining = warnings.Count - visibleWarningCount;
        return $"{warnings.Count} evidence warning(s): "
               + string.Join(" ", visible)
               + (remaining > 0
                   ? $" +{remaining} more in the Runner contract."
                   : string.Empty);
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (IsDisposed)
        {
            return;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
