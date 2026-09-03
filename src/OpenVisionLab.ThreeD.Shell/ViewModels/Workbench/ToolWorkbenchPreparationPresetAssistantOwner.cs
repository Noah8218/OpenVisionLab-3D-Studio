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
                ? localize("초안 적용", "Apply draft")
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
            "선택한 Filter 단계에서 Analyze를 실행하면 준비 프리셋을 검토할 수 있습니다.",
            "Select Analyze for the selected Filter step to review preparation presets.");
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
                "KernelSize = 3 · 더 좁은 이웃 범위",
                "KernelSize = 3 · narrower neighborhood"),
            3));
        PreparationPresetOptions.Add(new(
            "filter-median-5",
            localize("중앙값 5 × 5", "Median 5 x 5"),
            localize(
                "KernelSize = 5 · 균형 잡힌 이웃 범위",
                "KernelSize = 5 · balanced neighborhood"),
            5));
        PreparationPresetOptions.Add(new(
            "filter-median-7",
            localize("중앙값 7 × 7", "Median 7 x 7"),
            localize(
                "KernelSize = 7 · 더 넓은 이웃 범위",
                "KernelSize = 7 · broader neighborhood"),
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
                $"Filter 준비 프리셋 3개를 확인했습니다. 현재 초안은 {current.DisplayName}입니다. 제안을 선택한 뒤 검토하세요.",
                $"Found three Filter preparation presets. The current draft is {current.DisplayName}. Select a proposal and review it.")
            : localize(
                "Filter 준비 프리셋 3개를 확인했습니다. 유효한 프리셋을 선택한 뒤 제안하세요.",
                "Found three Filter preparation presets. Select a valid preset before proposing it.");
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
            $"{selected.DisplayName} 제안이 준비되었습니다. Review 전에는 초안·레시피·실행이 변경되지 않습니다.",
            $"{selected.DisplayName} is ready as a proposal. Draft, recipe, and execution remain unchanged until Review.");
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
            $"{proposal.DisplayName}을 검토 중입니다. Apply draft는 PropertyGrid 초안만 변경합니다.",
            $"Reviewing {proposal.DisplayName}. Apply draft changes only the PropertyGrid draft.");
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
            "프리셋 제안을 취소했습니다. 파라미터 초안과 실행 상태는 변경되지 않았습니다.",
            "The preset proposal was canceled. The parameter draft and execution state were unchanged.");
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
            $"{proposal.DisplayName}을 PropertyGrid 초안에만 적용했습니다. 일반 Apply를 선택하기 전에는 레시피가 변경되지 않으며 Preview/Publish/Run도 실행되지 않습니다.",
            $"Applied {proposal.DisplayName} to the PropertyGrid draft only. The recipe is unchanged until normal Apply, and Preview/Publish/Run were not invoked.");
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
