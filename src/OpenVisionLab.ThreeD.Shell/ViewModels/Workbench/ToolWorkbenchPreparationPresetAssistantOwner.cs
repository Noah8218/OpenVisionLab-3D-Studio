using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Presentation.Commands;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the bounded v1 preparation-preset assistant. Presets are curated
/// Filter kernel choices and are applied only to the existing typed PropertyGrid
/// draft; recipe mutation and all execution stages remain explicit callbacks.
/// </summary>
internal sealed class ToolWorkbenchPreparationPresetAssistantOwner : INotifyPropertyChanged
{
    private readonly Func<string, string, string> localize;
    private readonly Func<bool> isAssistantVisible;
    private readonly Func<bool> isRecipeMutationBlocked;
    private readonly Func<int> getFilterDraftKernelSize;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedStep;
    private readonly Func<ToolWorkbenchPipelineStepItem, int, (bool Success, string Message)>
        tryApplyFilterKernelPresetDraft;
    private readonly RelayCommand analyzePreparationPresetsCommand;
    private readonly RelayCommand proposePreparationPresetCommand;
    private readonly RelayCommand reviewPreparationPresetCommand;
    private readonly RelayCommand cancelPreparationPresetReviewCommand;
    private readonly RelayCommand applyPreparationPresetDraftCommand;

    private ToolWorkbenchPreparationPresetOption? selectedPreparationPreset;
    private ToolWorkbenchPreparationPresetOption? proposedPreparationPreset;
    private bool preparationPresetAnalysisReady;
    private bool isPreparationPresetReviewActive;
    private bool isPreparationPresetDraftApplied;
    private string preparationPresetAssistantSummary = string.Empty;

    public ToolWorkbenchPreparationPresetAssistantOwner(
        Func<string, string, string> localize,
        Func<bool> isAssistantVisible,
        Func<bool> isRecipeMutationBlocked,
        Func<int> getFilterDraftKernelSize,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedStep,
        Func<ToolWorkbenchPipelineStepItem, int, (bool Success, string Message)>
            tryApplyFilterKernelPresetDraft)
    {
        this.localize = localize ?? throw new ArgumentNullException(nameof(localize));
        this.isAssistantVisible = isAssistantVisible
            ?? throw new ArgumentNullException(nameof(isAssistantVisible));
        this.isRecipeMutationBlocked = isRecipeMutationBlocked
            ?? throw new ArgumentNullException(nameof(isRecipeMutationBlocked));
        this.getFilterDraftKernelSize = getFilterDraftKernelSize
            ?? throw new ArgumentNullException(nameof(getFilterDraftKernelSize));
        this.getSelectedStep = getSelectedStep
            ?? throw new ArgumentNullException(nameof(getSelectedStep));
        this.tryApplyFilterKernelPresetDraft = tryApplyFilterKernelPresetDraft
            ?? throw new ArgumentNullException(nameof(tryApplyFilterKernelPresetDraft));

        analyzePreparationPresetsCommand = new RelayCommand(
            _ => AnalyzePreparationPresets(),
            _ => CanAnalyzePreparationPresets());
        proposePreparationPresetCommand = new RelayCommand(
            _ => ProposePreparationPreset(),
            _ => CanProposePreparationPreset());
        reviewPreparationPresetCommand = new RelayCommand(
            _ => ReviewPreparationPreset(),
            _ => CanReviewPreparationPreset());
        cancelPreparationPresetReviewCommand = new RelayCommand(
            _ => CancelPreparationPresetReview(),
            _ => CanCancelPreparationPresetReview());
        applyPreparationPresetDraftCommand = new RelayCommand(
            _ => ApplyPreparationPresetDraft(),
            _ => CanApplyPreparationPresetDraft());

        RefreshOptions();
        Reset();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ToolWorkbenchPreparationPresetOption>
        PreparationPresetOptions { get; } = [];

    public ICommand AnalyzePreparationPresetsCommand => analyzePreparationPresetsCommand;
    public ICommand ProposePreparationPresetCommand => proposePreparationPresetCommand;
    public ICommand ReviewPreparationPresetCommand => reviewPreparationPresetCommand;
    public ICommand CancelPreparationPresetReviewCommand =>
        cancelPreparationPresetReviewCommand;
    public ICommand ApplyPreparationPresetDraftCommand =>
        applyPreparationPresetDraftCommand;

    public ToolWorkbenchPreparationPresetOption? SelectedPreparationPreset
    {
        get => selectedPreparationPreset;
        set
        {
            if (ReferenceEquals(selectedPreparationPreset, value))
            {
                return;
            }

            selectedPreparationPreset = value;
            if (proposedPreparationPreset is not null
                || isPreparationPresetReviewActive
                || isPreparationPresetDraftApplied)
            {
                proposedPreparationPreset = null;
                isPreparationPresetReviewActive = false;
                isPreparationPresetDraftApplied = false;
                preparationPresetAssistantSummary = localize(
                    "프리셋 선택이 변경되었습니다. 새 제안을 다시 검토하세요.",
                    "Preset selection changed. Review the new proposal again.");
            }

            OnPropertyChanged();
            NotifyState();
        }
    }

    public ToolWorkbenchPreparationPresetOption? ProposedPreparationPreset =>
        proposedPreparationPreset;

    public bool IsPreparationPresetAssistantVisible => isAssistantVisible();

    public bool IsPreparationPresetAnalysisReady => preparationPresetAnalysisReady;

    public bool IsPreparationPresetReviewActive => isPreparationPresetReviewActive;

    public bool IsPreparationPresetDraftApplied => isPreparationPresetDraftApplied;

    public string PreparationPresetAssistantStageText =>
        !IsPreparationPresetAssistantVisible
            ? localize("준비", "Analyze")
            : isPreparationPresetDraftApplied
                ? localize("설정창에 입력", "Apply draft")
                : isPreparationPresetReviewActive
                    ? localize("검토", "Review")
                    : proposedPreparationPreset is not null
                        ? localize("제안", "Propose")
                        : localize("분석", "Analyze");

    public string PreparationPresetAssistantSummary =>
        !IsPreparationPresetAssistantVisible
            ? localize(
                "현재 단계에서 사용할 수 있는 준비 프리셋이 없습니다.",
                "No preparation presets are available for the current step.")
            : preparationPresetAssistantSummary;

    public void Reset()
    {
        preparationPresetAnalysisReady = false;
        selectedPreparationPreset = null;
        proposedPreparationPreset = null;
        isPreparationPresetReviewActive = false;
        isPreparationPresetDraftApplied = false;
        preparationPresetAssistantSummary = localize(
            "필터 설정을 분석하세요.",
            "Analyze filter settings.");
        NotifyState();
    }

    public void RefreshOptions()
    {
        var selectedId = selectedPreparationPreset?.Id;
        var proposedId = proposedPreparationPreset?.Id;
        PreparationPresetOptions.Clear();
        PreparationPresetOptions.Add(new(
            "filter-median-3",
            localize("중앙값 3 × 3", "Median 3 x 3"),
            localize(
                "3 × 3",
                "3 × 3"),
            3));
        PreparationPresetOptions.Add(new(
            "filter-median-5",
            localize("중앙값 5 × 5", "Median 5 x 5"),
            localize(
                "5 × 5",
                "5 × 5"),
            5));
        PreparationPresetOptions.Add(new(
            "filter-median-7",
            localize("중앙값 7 × 7", "Median 7 x 7"),
            localize(
                "7 × 7",
                "7 × 7"),
            7));

        selectedPreparationPreset = selectedId is null
            ? null
            : PreparationPresetOptions.FirstOrDefault(option =>
                string.Equals(option.Id, selectedId, StringComparison.Ordinal));
        proposedPreparationPreset = proposedId is null
            ? null
            : PreparationPresetOptions.FirstOrDefault(option =>
                string.Equals(option.Id, proposedId, StringComparison.Ordinal));
        OnPropertyChanged(nameof(PreparationPresetOptions));
        OnPropertyChanged(nameof(SelectedPreparationPreset));
        OnPropertyChanged(nameof(ProposedPreparationPreset));
    }

    public void RefreshCommands()
    {
        analyzePreparationPresetsCommand.RaiseCanExecuteChanged();
        proposePreparationPresetCommand.RaiseCanExecuteChanged();
        reviewPreparationPresetCommand.RaiseCanExecuteChanged();
        cancelPreparationPresetReviewCommand.RaiseCanExecuteChanged();
        applyPreparationPresetDraftCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsPreparationPresetAssistantVisible));
    }

    private bool CanAnalyzePreparationPresets() =>
        IsPreparationPresetAssistantVisible && !isRecipeMutationBlocked();

    private bool CanProposePreparationPreset() =>
        IsPreparationPresetAssistantVisible
        && preparationPresetAnalysisReady
        && selectedPreparationPreset is not null
        && proposedPreparationPreset is null
        && !isRecipeMutationBlocked();

    private bool CanReviewPreparationPreset() =>
        IsPreparationPresetAssistantVisible
        && proposedPreparationPreset is not null
        && !isPreparationPresetReviewActive
        && !isPreparationPresetDraftApplied
        && !isRecipeMutationBlocked();

    private bool CanCancelPreparationPresetReview() =>
        IsPreparationPresetAssistantVisible
        && proposedPreparationPreset is not null
        && !isPreparationPresetDraftApplied;

    private bool CanApplyPreparationPresetDraft() =>
        IsPreparationPresetAssistantVisible
        && proposedPreparationPreset is not null
        && isPreparationPresetReviewActive
        && !isPreparationPresetDraftApplied
        && !isRecipeMutationBlocked();

    private void AnalyzePreparationPresets()
    {
        if (!CanAnalyzePreparationPresets())
        {
            return;
        }

        preparationPresetAnalysisReady = true;
        proposedPreparationPreset = null;
        isPreparationPresetReviewActive = false;
        isPreparationPresetDraftApplied = false;
        var currentKernel = getFilterDraftKernelSize();
        selectedPreparationPreset = PreparationPresetOptions.FirstOrDefault(option =>
            option.KernelSize == currentKernel);
        preparationPresetAssistantSummary = selectedPreparationPreset is { } current
            ? localize(
                $"프리셋 3개 · 현재 {current.DisplayName}",
                $"3 presets · current {current.DisplayName}")
            : localize(
                "프리셋을 선택하세요.",
                "Select a preset.");
        OnPropertyChanged(nameof(SelectedPreparationPreset));
        NotifyState();
    }

    private void ProposePreparationPreset()
    {
        if (!CanProposePreparationPreset() || selectedPreparationPreset is not { } selected)
        {
            return;
        }

        proposedPreparationPreset = selected;
        isPreparationPresetReviewActive = false;
        isPreparationPresetDraftApplied = false;
        preparationPresetAssistantSummary = localize(
            $"추천 설정: {selected.DisplayName}",
            $"Proposed preset: {selected.DisplayName}");
        NotifyState();
    }

    private void ReviewPreparationPreset()
    {
        if (!CanReviewPreparationPreset() || proposedPreparationPreset is not { } proposal)
        {
            return;
        }

        isPreparationPresetReviewActive = true;
        preparationPresetAssistantSummary = localize(
            $"{proposal.DisplayName} 검토 중",
            $"Reviewing {proposal.DisplayName}");
        NotifyState();
    }

    private void CancelPreparationPresetReview()
    {
        if (!CanCancelPreparationPresetReview())
        {
            return;
        }

        proposedPreparationPreset = null;
        isPreparationPresetReviewActive = false;
        isPreparationPresetDraftApplied = false;
        preparationPresetAssistantSummary = localize(
            "프리셋 제안을 취소했습니다.",
            "Preset proposal canceled.");
        NotifyState();
    }

    private void ApplyPreparationPresetDraft()
    {
        if (!CanApplyPreparationPresetDraft()
            || proposedPreparationPreset is not { } proposal
            || getSelectedStep() is not { } step)
        {
            return;
        }

        var result = tryApplyFilterKernelPresetDraft(step, proposal.KernelSize);
        if (!result.Success)
        {
            preparationPresetAssistantSummary = result.Message;
            NotifyState();
            return;
        }

        isPreparationPresetReviewActive = false;
        isPreparationPresetDraftApplied = true;
        preparationPresetAssistantSummary = localize(
            $"{proposal.DisplayName} · 설정창에 입력됨 · 미적용",
            $"{proposal.DisplayName} · entered in settings · not applied");
        NotifyState();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(ProposedPreparationPreset));
        OnPropertyChanged(nameof(IsPreparationPresetAnalysisReady));
        OnPropertyChanged(nameof(IsPreparationPresetReviewActive));
        OnPropertyChanged(nameof(IsPreparationPresetDraftApplied));
        OnPropertyChanged(nameof(PreparationPresetAssistantStageText));
        OnPropertyChanged(nameof(PreparationPresetAssistantSummary));
        RefreshCommands();
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record ToolWorkbenchPreparationPresetOption(
    string Id,
    string DisplayName,
    string Detail,
    int KernelSize);
