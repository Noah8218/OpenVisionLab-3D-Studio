using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Binding facade for the bounded v1 preparation-preset assistant. Policy,
/// workflow state, and draft-only Filter mutation live in
/// <see cref="ToolWorkbenchPreparationPresetAssistantOwner"/>.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private ToolWorkbenchPreparationPresetAssistantOwner preparationPresetAssistantOwner = null!;

    public ObservableCollection<ToolWorkbenchPreparationPresetOption> PreparationPresetOptions =>
        preparationPresetAssistantOwner.PreparationPresetOptions;

    public ICommand AnalyzePreparationPresetsCommand =>
        preparationPresetAssistantOwner.AnalyzePreparationPresetsCommand;

    public ICommand ProposePreparationPresetCommand =>
        preparationPresetAssistantOwner.ProposePreparationPresetCommand;

    public ICommand ReviewPreparationPresetCommand =>
        preparationPresetAssistantOwner.ReviewPreparationPresetCommand;

    public ICommand CancelPreparationPresetReviewCommand =>
        preparationPresetAssistantOwner.CancelPreparationPresetReviewCommand;

    public ICommand ApplyPreparationPresetDraftCommand =>
        preparationPresetAssistantOwner.ApplyPreparationPresetDraftCommand;

    public ToolWorkbenchPreparationPresetOption? SelectedPreparationPreset
    {
        get => preparationPresetAssistantOwner.SelectedPreparationPreset;
        set => preparationPresetAssistantOwner.SelectedPreparationPreset = value;
    }

    public ToolWorkbenchPreparationPresetOption? ProposedPreparationPreset =>
        preparationPresetAssistantOwner.ProposedPreparationPreset;

    public bool IsPreparationPresetAssistantVisible =>
        preparationPresetAssistantOwner.IsPreparationPresetAssistantVisible;

    public bool IsPreparationPresetAnalysisReady =>
        preparationPresetAssistantOwner.IsPreparationPresetAnalysisReady;

    public bool IsPreparationPresetReviewActive =>
        preparationPresetAssistantOwner.IsPreparationPresetReviewActive;

    public bool IsPreparationPresetDraftApplied =>
        preparationPresetAssistantOwner.IsPreparationPresetDraftApplied;

    public string PreparationPresetAssistantStageText =>
        preparationPresetAssistantOwner.PreparationPresetAssistantStageText;

    public string PreparationPresetAssistantSummary =>
        preparationPresetAssistantOwner.PreparationPresetAssistantSummary;

    private void InitializePreparationPresetAssistant()
    {
        preparationPresetAssistantOwner = new ToolWorkbenchPreparationPresetAssistantOwner(
            Localize,
            () => IsSelectedStepFilter && IsSelectedStepPropertyGridSupported,
            () => IsRecipeMutationBlocked,
            () => SelectedStepPropertyDraft is FilterStepProperties filter
                ? filter.KernelSize
                : 0,
            () => SelectedPipelineStep,
            TryApplyFilterKernelPresetDraftForAssistant);
        preparationPresetAssistantOwner.PropertyChanged +=
            OnPreparationPresetAssistantOwnerPropertyChanged;
    }

    private void OnPreparationPresetAssistantOwnerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    private void ResetPreparationPresetAssistant() =>
        preparationPresetAssistantOwner.Reset();

    private void RefreshPreparationPresetOptions() =>
        preparationPresetAssistantOwner.RefreshOptions();

    private void RefreshPreparationPresetCommands() =>
        preparationPresetAssistantOwner.RefreshCommands();
}
