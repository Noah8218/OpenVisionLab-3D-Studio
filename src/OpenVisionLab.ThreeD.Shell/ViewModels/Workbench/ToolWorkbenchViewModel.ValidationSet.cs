using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Preserves existing Validation Set bindings while the workspace owns the feature.
/// Only cross-workspace comparison and recipe-state notifications are routed here.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private ValidationSetWorkspaceViewModel validationSetWorkspace = null!;

    public event EventHandler? SelectValidationSetSourcesRequested;
    public event EventHandler? ValidationSetComparisonRequested;

    public ReadOnlyObservableCollection<ValidationSetSampleRow> ValidationSetSamples => validationSetWorkspace.ValidationSetSamples;

    public ReadOnlyObservableCollection<ValidationSetStepRow> SelectedValidationSetSteps => validationSetWorkspace.SelectedValidationSetSteps;

    public ReadOnlyObservableCollection<ValidationEvidenceDistributionRow> ValidationEvidenceDistributions => validationSetWorkspace.ValidationEvidenceDistributions;

    public ReadOnlyObservableCollection<ValidationThresholdCandidateRow> ValidationThresholdCandidates => validationSetWorkspace.ValidationThresholdCandidates;

    public ReadOnlyObservableCollection<ValidationThresholdDecisionRow> SelectedValidationThresholdDecisions => validationSetWorkspace.SelectedValidationThresholdDecisions;

    public ReadOnlyObservableCollection<ValidationThresholdParameterChangeRow> ValidationThresholdParameterChanges => validationSetWorkspace.ValidationThresholdParameterChanges;

    public ReadOnlyObservableCollection<ValidationThresholdHeldOutSampleRow> ValidationThresholdHeldOutSamples => validationSetWorkspace.ValidationThresholdHeldOutSamples;

    public ReadOnlyObservableCollection<ValidationThresholdDevelopmentSampleRow> ValidationThresholdDevelopmentSamples => validationSetWorkspace.ValidationThresholdDevelopmentSamples;

    public ICommand SelectValidationSetSourcesCommand => validationSetWorkspace.SelectValidationSetSourcesCommand;

    public ICommand AddCurrentSourceToValidationSetCommand => validationSetWorkspace.AddCurrentSourceToValidationSetCommand;

    public ICommand RunValidationSetCommand => validationSetWorkspace.RunValidationSetCommand;

    public ICommand ClearValidationSetCommand => validationSetWorkspace.ClearValidationSetCommand;

    public ICommand CancelValidationSetCommand => validationSetWorkspace.CancelValidationSetCommand;

    public ICommand SetValidationSetFilterCommand => validationSetWorkspace.SetValidationSetFilterCommand;

    public ICommand PreviousValidationSetIssueCommand => validationSetWorkspace.PreviousValidationSetIssueCommand;

    public ICommand NextValidationSetIssueCommand => validationSetWorkspace.NextValidationSetIssueCommand;

    public ICommand OpenValidationSetComparisonCommand => validationSetWorkspace.OpenValidationSetComparisonCommand;

    public ICommand SetValidationSampleRoleCommand => validationSetWorkspace.SetValidationSampleRoleCommand;

    public ICommand ProposeValidationThresholdCandidateCommand => validationSetWorkspace.ProposeValidationThresholdCandidateCommand;

    public ICommand ReviewValidationThresholdCandidateCommand => validationSetWorkspace.ReviewValidationThresholdCandidateCommand;

    public ICommand CancelValidationThresholdReviewCommand => validationSetWorkspace.CancelValidationThresholdReviewCommand;

    public ICommand ApplyValidationThresholdCandidateCommand => validationSetWorkspace.ApplyValidationThresholdCandidateCommand;

    public ICommand RevalidateValidationThresholdCorrectionCommand => validationSetWorkspace.RevalidateValidationThresholdCorrectionCommand;

    public ICommand ReplayValidationThresholdHeldOutCommand => validationSetWorkspace.ReplayValidationThresholdHeldOutCommand;

    public ValidationSetSampleRow? SelectedValidationSetSample
    {
        get => validationSetWorkspace.SelectedValidationSetSample;
        set => validationSetWorkspace.SelectedValidationSetSample = value;
    }

    public ValidationSetStepRow? SelectedValidationSetStep
    {
        get => validationSetWorkspace.SelectedValidationSetStep;
        set => validationSetWorkspace.SelectedValidationSetStep = value;
    }

    public bool HasSelectedValidationSetStep => validationSetWorkspace.HasSelectedValidationSetStep;

    public ValidationFailureCorrectionContext? ActiveValidationFailureCorrectionContext => validationSetWorkspace.ActiveValidationFailureCorrectionContext;

    public bool HasActiveValidationFailureCorrectionContext => validationSetWorkspace.HasActiveValidationFailureCorrectionContext;

    public ValidationSetStatusFilter ValidationSetFilter => validationSetWorkspace.ValidationSetFilter;

    public bool IsValidationSetFilterAll => validationSetWorkspace.IsValidationSetFilterAll;

    public bool IsValidationSetFilterPass => validationSetWorkspace.IsValidationSetFilterPass;

    public bool IsValidationSetFilterFail => validationSetWorkspace.IsValidationSetFilterFail;

    public bool IsValidationSetFilterError => validationSetWorkspace.IsValidationSetFilterError;

    public int ValidationSetAllCount => validationSetWorkspace.ValidationSetAllCount;

    public int ValidationSetPassCount => validationSetWorkspace.ValidationSetPassCount;

    public int ValidationSetFailCount => validationSetWorkspace.ValidationSetFailCount;

    public int ValidationSetErrorCount => validationSetWorkspace.ValidationSetErrorCount;

    public int ValidationSetGoodCount => validationSetWorkspace.ValidationSetGoodCount;

    public int ValidationSetBadCount => validationSetWorkspace.ValidationSetBadCount;

    public int ValidationSetHeldOutCount => validationSetWorkspace.ValidationSetHeldOutCount;

    public bool HasValidationSetIssues => validationSetWorkspace.HasValidationSetIssues;

    public bool IsSelectedValidationRoleGood => validationSetWorkspace.IsSelectedValidationRoleGood;

    public bool IsSelectedValidationRoleBad => validationSetWorkspace.IsSelectedValidationRoleBad;

    public bool IsSelectedValidationRoleHeldOut => validationSetWorkspace.IsSelectedValidationRoleHeldOut;

    public bool HasValidationEvidence => validationSetWorkspace.HasValidationEvidence;

    public bool HasValidationThresholdCandidates => validationSetWorkspace.HasValidationThresholdCandidates;

    public bool HasValidationThresholdAssistantAnalysis => validationSetWorkspace.HasValidationThresholdAssistantAnalysis;

    public bool HasValidationThresholdAssistantProposal => validationSetWorkspace.HasValidationThresholdAssistantProposal;

    public ValidationThresholdAssistantStage ValidationThresholdAssistantStage => validationSetWorkspace.ValidationThresholdAssistantStage;

    public string ValidationThresholdAssistantStageText => validationSetWorkspace.ValidationThresholdAssistantStageText;

    public string ValidationThresholdAssistantSummary => validationSetWorkspace.ValidationThresholdAssistantSummary;

    public bool IsValidationEvidenceExpanded
    {
        get => validationSetWorkspace.IsValidationEvidenceExpanded;
        set => validationSetWorkspace.IsValidationEvidenceExpanded = value;
    }

    public bool IsValidationSetDefinitionDirty => validationSetWorkspace.IsValidationSetDefinitionDirty;

    public bool IsValidationThresholdExpanded
    {
        get => validationSetWorkspace.IsValidationThresholdExpanded;
        set => validationSetWorkspace.IsValidationThresholdExpanded = value;
    }

    public ValidationThresholdCandidateRow? SelectedValidationThresholdCandidate
    {
        get => validationSetWorkspace.SelectedValidationThresholdCandidate;
        set => validationSetWorkspace.SelectedValidationThresholdCandidate = value;
    }

    public bool HasSelectedValidationThresholdCandidate => validationSetWorkspace.HasSelectedValidationThresholdCandidate;

    public bool IsValidationThresholdReviewActive => validationSetWorkspace.IsValidationThresholdReviewActive;

    public bool IsValidationThresholdCandidateApplied => validationSetWorkspace.IsValidationThresholdCandidateApplied;

    public bool IsValidationThresholdManualCorrectionCommitted => validationSetWorkspace.IsValidationThresholdManualCorrectionCommitted;

    public bool IsValidationThresholdDevelopmentValidated => validationSetWorkspace.IsValidationThresholdDevelopmentValidated;

    public bool HasValidationThresholdParameterChanges => validationSetWorkspace.HasValidationThresholdParameterChanges;

    public bool HasValidationThresholdHeldOutEvidence => validationSetWorkspace.HasValidationThresholdHeldOutEvidence;

    public bool HasValidationThresholdDevelopmentEvidence => validationSetWorkspace.HasValidationThresholdDevelopmentEvidence;

    public string ValidationThresholdCorrectionSummary => validationSetWorkspace.ValidationThresholdCorrectionSummary;

    public string ValidationEvidenceSummary => validationSetWorkspace.ValidationEvidenceSummary;

    public string ValidationEvidenceWarning => validationSetWorkspace.ValidationEvidenceWarning;

    public string ValidationThresholdSummary => validationSetWorkspace.ValidationThresholdSummary;

    public string ValidationThresholdWarning => validationSetWorkspace.ValidationThresholdWarning;

    public string ValidationSetSummary => validationSetWorkspace.ValidationSetSummary;

    public string ValidationSetCapability => validationSetWorkspace.ValidationSetCapability;

    public string ValidationSetProgressText => validationSetWorkspace.ValidationSetProgressText;

    public double ValidationSetProgress => validationSetWorkspace.ValidationSetProgress;

    public bool IsValidationSetRunning => validationSetWorkspace?.IsValidationSetRunning ?? false;

    public bool IsValidationSetIdle => !IsValidationSetRunning;

    public bool HasValidationSetSamples => validationSetWorkspace.HasValidationSetSamples;

    public bool HasSelectedValidationSetSample => validationSetWorkspace.HasSelectedValidationSetSample;

    private void InitializeValidationSet()
    {
        validationSetWorkspace = new ValidationSetWorkspaceViewModel(
            CreateDocument,
            () => RecipeName,
            () => RecipePath,
            () => Source.Path,
            () => IsSourceReadyForRecipe,
            PipelineSteps,
            () => HasPendingStepParameterChanges,
            () => SelectedPipelineStep,
            TrySelectValidationThresholdPipelineStep,
            stepPropertySession,
            AppendLog,
            Localize,
            LocalizeStatus);
        validationSetWorkspace.PropertyChanged += OnValidationSetWorkspacePropertyChanged;
        validationSetWorkspace.SamplesChanged += OnValidationSetSamplesChanged;
        validationSetWorkspace.DefinitionDirtyChanged += OnValidationSetDefinitionDirtyChanged;
        validationSetWorkspace.ComparePinsClearRequested += OnValidationSetComparePinsClearRequested;
        validationSetWorkspace.ComparisonRequested += OnValidationSetComparisonRequested;
        validationSetWorkspace.SelectValidationSetSourcesRequested += OnSelectValidationSetSourcesRequested;
    }

    private void OnValidationSetWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    private void OnValidationSetSamplesChanged(object? sender, EventArgs args) => RebuildRenderableC3DConsumers();

    private void OnSelectValidationSetSourcesRequested(object? sender, EventArgs args) =>
        SelectValidationSetSourcesRequested?.Invoke(this, args);

    private void OnValidationSetDefinitionDirtyChanged(object? sender, EventArgs args)
    {
        OnPropertyChanged(nameof(HasUncommittedRecipeChanges));
        OnPropertyChanged(nameof(RecipeStateSummary));
        OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
    }

    private void OnValidationSetComparisonRequested(object? sender, ValidationSetSampleRow sample)
    {
        RebuildRenderableC3DConsumers();
        CompareSlotAArtifactId = Source.Id;
        CompareSlotBArtifactId = GetValidationSetCompareArtifactId(sample);
        CompareSlotCArtifactId = string.Empty;
        ValidationSetComparisonRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnValidationSetComparePinsClearRequested(object? sender, EventArgs args)
    {
        if (IsValidationSetCompareArtifactId(CompareSlotAArtifactId)) CompareSlotAArtifactId = string.Empty;
        if (IsValidationSetCompareArtifactId(CompareSlotBArtifactId)) CompareSlotBArtifactId = string.Empty;
        if (IsValidationSetCompareArtifactId(CompareSlotCArtifactId)) CompareSlotCArtifactId = string.Empty;
    }

    private void OnValidationSetLocalizationChanged(object? sender, PropertyChangedEventArgs args) =>
        validationSetWorkspace.RefreshValidationSetLocalization();

    private bool TrySelectValidationThresholdPipelineStep(ToolWorkbenchPipelineStepItem step)
    {
        SelectedPipelineStep = step;
        return ReferenceEquals(SelectedPipelineStep, step);
    }

    public bool BeginValidationFailureCorrectionContext() => validationSetWorkspace.BeginValidationFailureCorrectionContext();

    public void SetValidationSetSources(IEnumerable<string> sourcePaths) => validationSetWorkspace.SetValidationSetSources(sourcePaths);

    internal Task RunValidationSetAsync() => validationSetWorkspace.RunValidationSetAsync();

    internal Task RevalidateValidationThresholdCorrectionAsync() => validationSetWorkspace.RevalidateValidationThresholdCorrectionAsync();

    internal Task ReplayValidationThresholdHeldOutAsync() => validationSetWorkspace.ReplayValidationThresholdHeldOutAsync();

    private void ClearValidationSet() => validationSetWorkspace.ClearValidationSet();

    private void RefreshValidationThresholdCorrectionCommands() => validationSetWorkspace.RefreshValidationThresholdCorrectionCommands();

    private void NotifyValidationThresholdDraftCommitted(ToolWorkbenchPipelineStepItem step, bool changed) =>
        validationSetWorkspace.NotifyValidationThresholdDraftCommitted(step, changed);

    private void NotifyValidationThresholdDraftDiscarded(string? stepId) => validationSetWorkspace.NotifyValidationThresholdDraftDiscarded(stepId);

    private void SetValidationSetDefinitionDirty(bool value) => validationSetWorkspace.SetValidationSetDefinitionDirty(value);

    private void SaveValidationSetDefinition(string recipePath) => validationSetWorkspace.SaveValidationSetDefinition(recipePath);

    private void SaveValidationThresholdCorrectionEvidence(string recipePath) => validationSetWorkspace.SaveValidationThresholdCorrectionEvidence(recipePath);

    private void LoadValidationSetDefinition(string recipePath, ToolRecipeDocument document) =>
        validationSetWorkspace.LoadValidationSetDefinition(recipePath, document);

    private void LoadValidationThresholdCorrectionEvidence(string recipePath, ToolRecipeDocument document) =>
        validationSetWorkspace.LoadValidationThresholdCorrectionEvidence(recipePath, document);

    private void RefreshValidationSetCapability() => validationSetWorkspace.RefreshValidationSetCapability();

    private static string Localize(string korean, string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean;

    private static string LocalizeStatus(ResultStatus status) => status switch
    {
        ResultStatus.Pass => Localize("통과", "Pass"),
        ResultStatus.Fail => Localize("실패", "Fail"),
        ResultStatus.Warning => Localize("경고", "Warning"),
        _ => Localize("오류", "Error")
    };
}
