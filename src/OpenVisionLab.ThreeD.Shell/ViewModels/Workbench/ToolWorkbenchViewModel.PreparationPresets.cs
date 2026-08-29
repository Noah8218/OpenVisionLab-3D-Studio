using System.Collections.ObjectModel;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Hosts the bounded v1 preparation-preset assistant. Presets are curated
/// Filter kernel choices and are applied only to the existing typed PropertyGrid
/// draft; the recipe and all execution stages remain explicit.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private readonly ObservableCollection<ToolWorkbenchPreparationPresetOption>
        preparationPresetOptions = [];

    private RelayCommand analyzePreparationPresetsCommand = null!;
    private RelayCommand proposePreparationPresetCommand = null!;
    private RelayCommand reviewPreparationPresetCommand = null!;
    private RelayCommand cancelPreparationPresetReviewCommand = null!;
    private RelayCommand applyPreparationPresetDraftCommand = null!;
    private ToolWorkbenchPreparationPresetOption? selectedPreparationPreset;
    private ToolWorkbenchPreparationPresetOption? proposedPreparationPreset;
    private bool preparationPresetAnalysisReady;
    private bool isPreparationPresetReviewActive;
    private bool isPreparationPresetDraftApplied;
    private string preparationPresetAssistantSummary =
        "Analyze the selected Filter step to review the available preparation presets.";

    public ObservableCollection<ToolWorkbenchPreparationPresetOption> PreparationPresetOptions =>
        preparationPresetOptions;

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
                preparationPresetAssistantSummary = Localize(
                    "프리셋 선택이 변경되었습니다. 새 제안을 다시 검토하세요.",
                    "Preset selection changed. Review the new proposal again.");
            }

            OnPropertyChanged();
            NotifyPreparationPresetState();
        }
    }

    public ToolWorkbenchPreparationPresetOption? ProposedPreparationPreset =>
        proposedPreparationPreset;

    public bool IsPreparationPresetAssistantVisible =>
        IsSelectedStepFilter && IsSelectedStepPropertyGridSupported;

    public bool IsPreparationPresetAnalysisReady =>
        preparationPresetAnalysisReady;

    public bool IsPreparationPresetReviewActive =>
        isPreparationPresetReviewActive;

    public bool IsPreparationPresetDraftApplied =>
        isPreparationPresetDraftApplied;

    public string PreparationPresetAssistantStageText =>
        !IsPreparationPresetAssistantVisible
            ? Localize("준비", "Analyze")
            : isPreparationPresetDraftApplied
                ? Localize("초안 적용", "Apply draft")
                : isPreparationPresetReviewActive
                    ? Localize("검토", "Review")
                    : proposedPreparationPreset is not null
                        ? Localize("제안", "Propose")
                        : Localize("분석", "Analyze");

    public string PreparationPresetAssistantSummary =>
        !IsPreparationPresetAssistantVisible
            ? Localize(
                "현재 단계에서 사용할 수 있는 준비 프리셋이 없습니다.",
                "No preparation presets are available for the current step.")
            : preparationPresetAssistantSummary;

    private void InitializePreparationPresetAssistant()
    {
        RefreshPreparationPresetOptions();
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
        ResetPreparationPresetAssistant();
    }

    private bool CanAnalyzePreparationPresets() =>
        IsPreparationPresetAssistantVisible && !IsRecipeMutationBlocked;

    private bool CanProposePreparationPreset() =>
        IsPreparationPresetAssistantVisible
        && preparationPresetAnalysisReady
        && selectedPreparationPreset is not null
        && proposedPreparationPreset is null
        && !IsRecipeMutationBlocked;

    private bool CanReviewPreparationPreset() =>
        IsPreparationPresetAssistantVisible
        && proposedPreparationPreset is not null
        && !isPreparationPresetReviewActive
        && !isPreparationPresetDraftApplied
        && !IsRecipeMutationBlocked;

    private bool CanCancelPreparationPresetReview() =>
        IsPreparationPresetAssistantVisible
        && proposedPreparationPreset is not null
        && !isPreparationPresetDraftApplied;

    private bool CanApplyPreparationPresetDraft() =>
        IsPreparationPresetAssistantVisible
        && proposedPreparationPreset is not null
        && isPreparationPresetReviewActive
        && !isPreparationPresetDraftApplied
        && !IsRecipeMutationBlocked;

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
        var currentKernel = GetFilterDraftKernelSize();
        selectedPreparationPreset = preparationPresetOptions.FirstOrDefault(option =>
            option.KernelSize == currentKernel);
        preparationPresetAssistantSummary = selectedPreparationPreset is { } current
            ? Localize(
                $"Filter 준비 프리셋 3개를 확인했습니다. 현재 초안은 {current.DisplayName}입니다. 제안을 선택한 뒤 검토하세요.",
                $"Found three Filter preparation presets. The current draft is {current.DisplayName}. Select a proposal and review it.")
            : Localize(
                "Filter 준비 프리셋 3개를 확인했습니다. 유효한 프리셋을 선택한 뒤 제안하세요.",
                "Found three Filter preparation presets. Select a valid preset before proposing it.");
        OnPropertyChanged(nameof(SelectedPreparationPreset));
        NotifyPreparationPresetState();
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
        preparationPresetAssistantSummary = Localize(
            $"{selected.DisplayName} 제안이 준비되었습니다. Review 전에는 초안·레시피·실행이 변경되지 않습니다.",
            $"{selected.DisplayName} is ready as a proposal. Draft, recipe, and execution remain unchanged until Review.");
        NotifyPreparationPresetState();
    }

    private void ReviewPreparationPreset()
    {
        if (!CanReviewPreparationPreset() || proposedPreparationPreset is not { } proposal)
        {
            return;
        }

        isPreparationPresetReviewActive = true;
        preparationPresetAssistantSummary = Localize(
            $"{proposal.DisplayName}을 검토 중입니다. Apply draft는 PropertyGrid 초안만 변경합니다.",
            $"Reviewing {proposal.DisplayName}. Apply draft changes only the PropertyGrid draft.");
        NotifyPreparationPresetState();
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
        preparationPresetAssistantSummary = Localize(
            "프리셋 제안을 취소했습니다. 파라미터 초안과 실행 상태는 변경되지 않았습니다.",
            "The preset proposal was canceled. The parameter draft and execution state were unchanged.");
        NotifyPreparationPresetState();
    }

    private void ApplyPreparationPresetDraft()
    {
        if (!CanApplyPreparationPresetDraft()
            || proposedPreparationPreset is not { } proposal
            || SelectedPipelineStep is not { } step)
        {
            return;
        }

        if (!stepPropertySession.TryApplyFilterKernelPresetDraft(
                step,
                proposal.KernelSize,
                out var message))
        {
            preparationPresetAssistantSummary = message;
            NotifyPreparationPresetState();
            return;
        }

        isPreparationPresetReviewActive = false;
        isPreparationPresetDraftApplied = true;
        preparationPresetAssistantSummary = Localize(
            $"{proposal.DisplayName}을 PropertyGrid 초안에만 적용했습니다. 일반 Apply를 선택하기 전에는 레시피가 변경되지 않으며 Preview/Publish/Run도 실행되지 않습니다.",
            $"Applied {proposal.DisplayName} to the PropertyGrid draft only. The recipe is unchanged until normal Apply, and Preview/Publish/Run were not invoked.");
        NotifyPreparationPresetState();
    }

    private int GetFilterDraftKernelSize() =>
        SelectedStepPropertyDraft is FilterStepProperties filter
            ? filter.KernelSize
            : 0;

    private void ResetPreparationPresetAssistant()
    {
        preparationPresetAnalysisReady = false;
        selectedPreparationPreset = null;
        proposedPreparationPreset = null;
        isPreparationPresetReviewActive = false;
        isPreparationPresetDraftApplied = false;
        preparationPresetAssistantSummary = Localize(
            "선택한 Filter 단계에서 Analyze를 실행하면 준비 프리셋을 검토할 수 있습니다.",
            "Select Analyze for the selected Filter step to review preparation presets.");
        NotifyPreparationPresetState();
    }

    private void RefreshPreparationPresetOptions()
    {
        var selectedId = selectedPreparationPreset?.Id;
        var proposedId = proposedPreparationPreset?.Id;
        preparationPresetOptions.Clear();
        preparationPresetOptions.Add(new(
            "filter-median-3",
            Localize("중앙값 3 × 3", "Median 3 x 3"),
            Localize(
                "KernelSize = 3 · 더 좁은 이웃 범위",
                "KernelSize = 3 · narrower neighborhood"),
            3));
        preparationPresetOptions.Add(new(
            "filter-median-5",
            Localize("중앙값 5 × 5", "Median 5 x 5"),
            Localize(
                "KernelSize = 5 · 균형 잡힌 이웃 범위",
                "KernelSize = 5 · balanced neighborhood"),
            5));
        preparationPresetOptions.Add(new(
            "filter-median-7",
            Localize("중앙값 7 × 7", "Median 7 x 7"),
            Localize(
                "KernelSize = 7 · 더 넓은 이웃 범위",
                "KernelSize = 7 · broader neighborhood"),
            7));

        selectedPreparationPreset = selectedId is null
            ? null
            : preparationPresetOptions.FirstOrDefault(option =>
                string.Equals(option.Id, selectedId, StringComparison.Ordinal));
        proposedPreparationPreset = proposedId is null
            ? null
            : preparationPresetOptions.FirstOrDefault(option =>
                string.Equals(option.Id, proposedId, StringComparison.Ordinal));
        OnPropertyChanged(nameof(PreparationPresetOptions));
        OnPropertyChanged(nameof(SelectedPreparationPreset));
        OnPropertyChanged(nameof(ProposedPreparationPreset));
    }

    private void RefreshPreparationPresetCommands()
    {
        analyzePreparationPresetsCommand?.RaiseCanExecuteChanged();
        proposePreparationPresetCommand?.RaiseCanExecuteChanged();
        reviewPreparationPresetCommand?.RaiseCanExecuteChanged();
        cancelPreparationPresetReviewCommand?.RaiseCanExecuteChanged();
        applyPreparationPresetDraftCommand?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsPreparationPresetAssistantVisible));
    }

    private void NotifyPreparationPresetState()
    {
        OnPropertyChanged(nameof(ProposedPreparationPreset));
        OnPropertyChanged(nameof(IsPreparationPresetAnalysisReady));
        OnPropertyChanged(nameof(IsPreparationPresetReviewActive));
        OnPropertyChanged(nameof(IsPreparationPresetDraftApplied));
        OnPropertyChanged(nameof(PreparationPresetAssistantStageText));
        OnPropertyChanged(nameof(PreparationPresetAssistantSummary));
        RefreshPreparationPresetCommands();
    }
}

public sealed record ToolWorkbenchPreparationPresetOption(
    string Id,
    string DisplayName,
    string Detail,
    int KernelSize);
