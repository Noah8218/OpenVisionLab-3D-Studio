using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Compatibility facade for the read-only FlowDiagnostics and Recipe Health
/// surface. Projection, summaries, and issue navigation live in
/// <see cref="ToolWorkbenchFlowDiagnosticsOwner"/>; recipe/source mutation
/// remains owned by the Workbench ViewModel and its execution owners.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private ToolWorkbenchFlowDiagnosticsOwner flowDiagnosticsOwner = null!;

    public ResettableObservableCollection<ToolWorkbenchFlowPortDiagnosticItem> FlowPortDiagnostics =>
        flowDiagnosticsOwner.FlowPortDiagnostics;

    public ResettableObservableCollection<ToolWorkbenchRecipeHealthItem> RecipeHealthItems =>
        flowDiagnosticsOwner.RecipeHealthItems;

    public ICommand FocusFlowProblemStepCommand => flowDiagnosticsOwner.FocusFlowProblemStepCommand;
    public ICommand PreviousRecipeHealthIssueCommand => flowDiagnosticsOwner.PreviousRecipeHealthIssueCommand;
    public ICommand NextRecipeHealthIssueCommand => flowDiagnosticsOwner.NextRecipeHealthIssueCommand;

    public string FlowProblemsSummary => flowDiagnosticsOwner.FlowProblemsSummary;
    public bool HasFlowProblems => flowDiagnosticsOwner.HasFlowProblems;
    public ToolWorkbenchFlowPortDiagnosticItem? SelectedStepFlowProblem =>
        flowDiagnosticsOwner.SelectedStepFlowProblem;
    public bool HasSelectedStepFlowProblem => flowDiagnosticsOwner.HasSelectedStepFlowProblem;

    public int RecipeHealthReadyCount => flowDiagnosticsOwner.RecipeHealthReadyCount;
    public int RecipeHealthNeedsInputCount => flowDiagnosticsOwner.RecipeHealthNeedsInputCount;
    public int RecipeHealthNeedsSelectionCount => flowDiagnosticsOwner.RecipeHealthNeedsSelectionCount;
    public int RecipeHealthNeedsParametersCount => flowDiagnosticsOwner.RecipeHealthNeedsParametersCount;
    public int RecipeHealthStalePreviewCount => flowDiagnosticsOwner.RecipeHealthStalePreviewCount;
    public int RecipeHealthPublishedCount => flowDiagnosticsOwner.RecipeHealthPublishedCount;
    public int RecipeHealthIssueCount => flowDiagnosticsOwner.RecipeHealthIssueCount;
    public string RecipeHealthSummary => flowDiagnosticsOwner.RecipeHealthSummary;
    public string RecipeHealthCountsPrimary => flowDiagnosticsOwner.RecipeHealthCountsPrimary;
    public string RecipeHealthCountsSecondary => flowDiagnosticsOwner.RecipeHealthCountsSecondary;
    public ToolWorkbenchRecipeHealthItem? SelectedRecipeHealthItem =>
        flowDiagnosticsOwner.SelectedRecipeHealthItem;
    public string SelectedRecipeHealthTitle => flowDiagnosticsOwner.SelectedRecipeHealthTitle;
    public string SelectedRecipeHealthDetail => flowDiagnosticsOwner.SelectedRecipeHealthDetail;
    public bool CanNavigatePreviousRecipeHealthIssue =>
        flowDiagnosticsOwner.CanNavigatePreviousRecipeHealthIssue;
    public bool CanNavigateNextRecipeHealthIssue =>
        flowDiagnosticsOwner.CanNavigateNextRecipeHealthIssue;

    public bool IsSelectedToolInputSectionExpanded
    {
        get => flowDiagnosticsOwner.IsSelectedToolInputSectionExpanded;
        set => flowDiagnosticsOwner.IsSelectedToolInputSectionExpanded = value;
    }

    public bool IsAdvancedInputRouteEditingExpanded
    {
        get => flowDiagnosticsOwner.IsAdvancedInputRouteEditingExpanded;
        set => flowDiagnosticsOwner.IsAdvancedInputRouteEditingExpanded = value;
    }

    private void InitializeFlowDiagnostics()
    {
        flowDiagnosticsOwner = new ToolWorkbenchFlowDiagnosticsOwner(
            Localization,
            () => PipelineSteps,
            () => ArtifactRegistry,
            () => Selections,
            () => ValidationMessages.Count,
            () => SelectedPipelineStep,
            SelectPipelineStepFromFlowDiagnostics,
            step => CreateSelectionRequirement(step),
            IsSelectionCurrent,
            () => IsRecipeMutationBlocked,
            () => HasPendingStepParameterChanges,
            () => IsTeachingSelectionCaptureActive,
            () => ThicknessRepeatGrid.IsActive,
            () => OrientedBoxEditor.IsDraftOpen,
            RefreshTeachingSelectionContext,
            RefreshStepCommands,
            RefreshNavigatorSelection);
        flowDiagnosticsOwner.PropertyChanged += OnFlowDiagnosticsOwnerPropertyChanged;
    }

    private void SelectPipelineStepFromFlowDiagnostics(
        ToolWorkbenchPipelineStepItem step,
        bool deferStateRefresh)
    {
        deferSelectedStepStateRefresh = deferStateRefresh;
        try
        {
            SelectedPipelineStep = step;
        }
        finally
        {
            deferSelectedStepStateRefresh = false;
        }
    }

    private void OnFlowDiagnosticsOwnerPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    private void RebuildFlowPortDiagnostics() => flowDiagnosticsOwner.RebuildFlowPortDiagnostics();

    private void RebuildRecipeHealthProjection() => flowDiagnosticsOwner.RebuildRecipeHealthProjection();

    private void NotifySelectedStepFlowProblemChanged() =>
        flowDiagnosticsOwner.NotifySelectedStepFlowProblemChanged();

    private void NotifyRecipeHealthSelectionChanged() =>
        flowDiagnosticsOwner.NotifyRecipeHealthSelectionChanged();
}

public sealed record ToolWorkbenchFlowPortDiagnosticItem(
    string Port,
    string Kind,
    string Status,
    string EntityId,
    string Detail,
    ToolWorkbenchPipelineStepItem Step)
{
    public string StepTitle => $"Step {Step.Order}: {Step.ToolName}";
    public string AccessibleName => $"{StepTitle}. {Port}. {Status}. {Detail}";
}

public enum ToolWorkbenchRecipeHealthCategory
{
    Ready,
    NeedsInput,
    NeedsSelection,
    NeedsParameters,
    StalePreview,
    Published
}

public sealed record ToolWorkbenchRecipeHealthItem(
    ToolWorkbenchRecipeHealthCategory Category,
    string Label,
    string Detail,
    string Title,
    ToolWorkbenchPipelineStepItem Step)
{
    public bool IsIssue => Category is ToolWorkbenchRecipeHealthCategory.NeedsInput
        or ToolWorkbenchRecipeHealthCategory.NeedsSelection
        or ToolWorkbenchRecipeHealthCategory.NeedsParameters
        or ToolWorkbenchRecipeHealthCategory.StalePreview;

    public string AccessibleName => $"{Title}. {Detail}";
}
