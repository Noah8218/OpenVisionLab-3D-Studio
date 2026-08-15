using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Projects existing artifact identity into read-only input/output port diagnostics.
/// It never edits a route, changes a parameter, or invokes Preview, Run, or Publish.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private bool isSelectedToolInputSectionExpanded;
    private bool isAdvancedInputRouteEditingExpanded;
    private RelayCommand previousRecipeHealthIssueCommand = null!;
    private RelayCommand nextRecipeHealthIssueCommand = null!;

    public ResettableObservableCollection<ToolWorkbenchFlowPortDiagnosticItem> FlowPortDiagnostics { get; } = [];
    public ResettableObservableCollection<ToolWorkbenchRecipeHealthItem> RecipeHealthItems { get; } = [];

    public ICommand FocusFlowProblemStepCommand { get; private set; } = null!;
    public ICommand PreviousRecipeHealthIssueCommand => previousRecipeHealthIssueCommand;
    public ICommand NextRecipeHealthIssueCommand => nextRecipeHealthIssueCommand;

    public string FlowProblemsSummary => string.Format(
        Localization.ProblemsSummaryFormat,
        FlowPortDiagnostics.Count,
        ValidationMessages.Count);

    public bool HasFlowProblems => FlowPortDiagnostics.Count > 0 || ValidationMessages.Count > 0;

    public ToolWorkbenchFlowPortDiagnosticItem? SelectedStepFlowProblem =>
        SelectedPipelineStep is null
            ? null
            : FlowPortDiagnostics.FirstOrDefault(item =>
                item.Port == "Input"
                && ReferenceEquals(item.Step, SelectedPipelineStep));

    public bool HasSelectedStepFlowProblem => SelectedStepFlowProblem is not null;

    public int RecipeHealthReadyCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.Ready);
    public int RecipeHealthNeedsInputCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.NeedsInput);
    public int RecipeHealthNeedsSelectionCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.NeedsSelection);
    public int RecipeHealthNeedsParametersCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.NeedsParameters);
    public int RecipeHealthStalePreviewCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.StalePreview);
    public int RecipeHealthPublishedCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.Published);
    public int RecipeHealthIssueCount => RecipeHealthItems.Count(item => item.IsIssue);

    public string RecipeHealthSummary => string.Format(
        Localization.RecipeHealthSummaryFormat,
        RecipeHealthIssueCount);

    public string RecipeHealthCountsPrimary => string.Format(
        Localization.RecipeHealthCountsPrimaryFormat,
        RecipeHealthReadyCount,
        RecipeHealthNeedsInputCount,
        RecipeHealthNeedsSelectionCount);

    public string RecipeHealthCountsSecondary => string.Format(
        Localization.RecipeHealthCountsSecondaryFormat,
        RecipeHealthNeedsParametersCount,
        RecipeHealthStalePreviewCount,
        RecipeHealthPublishedCount);

    public ToolWorkbenchRecipeHealthItem? SelectedRecipeHealthItem =>
        SelectedPipelineStep is null
            ? null
            : RecipeHealthItems.FirstOrDefault(item =>
                ReferenceEquals(item.Step, SelectedPipelineStep));

    public string SelectedRecipeHealthTitle => SelectedRecipeHealthItem?.Title
        ?? Localization.RecipeHealthNoStep;

    public string SelectedRecipeHealthDetail => SelectedRecipeHealthItem?.Detail
        ?? Localization.RecipeHealthNoStepDetail;

    public bool CanNavigatePreviousRecipeHealthIssue =>
        CanNavigateRecipeHealth
        && FindPreviousRecipeHealthIssue() is not null;

    public bool CanNavigateNextRecipeHealthIssue =>
        CanNavigateRecipeHealth
        && FindNextRecipeHealthIssue() is not null;

    public bool IsSelectedToolInputSectionExpanded
    {
        get => isSelectedToolInputSectionExpanded;
        set
        {
            if (isSelectedToolInputSectionExpanded == value)
            {
                return;
            }

            isSelectedToolInputSectionExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsAdvancedInputRouteEditingExpanded
    {
        get => isAdvancedInputRouteEditingExpanded;
        set
        {
            if (isAdvancedInputRouteEditingExpanded == value)
            {
                return;
            }

            isAdvancedInputRouteEditingExpanded = value;
            OnPropertyChanged();
        }
    }

    private void InitializeFlowDiagnostics()
    {
        FocusFlowProblemStepCommand = new RelayCommand(
            parameter => FocusFlowProblemStep(parameter as ToolWorkbenchFlowPortDiagnosticItem),
            parameter => parameter is ToolWorkbenchFlowPortDiagnosticItem);
        previousRecipeHealthIssueCommand = new RelayCommand(
            _ => NavigateRecipeHealthIssue(forward: false),
            _ => CanNavigatePreviousRecipeHealthIssue);
        nextRecipeHealthIssueCommand = new RelayCommand(
            _ => NavigateRecipeHealthIssue(forward: true),
            _ => CanNavigateNextRecipeHealthIssue);
    }

    private void OnFlowDiagnosticsLocalizationChanged(object? sender, PropertyChangedEventArgs args) =>
        RebuildFlowPortDiagnostics();

    private void RebuildFlowPortDiagnostics()
    {
        var diagnostics = new List<ToolWorkbenchFlowPortDiagnosticItem>();
        foreach (var step in PipelineSteps)
        {
            var input = DescribeInputPort(step);
            var output = DescribeOutputPort(step);
            step.UpdateFlowPortPresentation(
                input.Status,
                input.Detail,
                input.IsProblem,
                output.Status,
                output.Detail,
                output.IsProblem);

            if (input.IsProblem)
            {
                diagnostics.Add(new ToolWorkbenchFlowPortDiagnosticItem(
                    "Input",
                    input.Kind,
                    input.Status,
                    step.InputSummary,
                    input.Detail,
                    step));
            }

            if (output.IsProblem)
            {
                diagnostics.Add(new ToolWorkbenchFlowPortDiagnosticItem(
                    "Output",
                    output.Kind,
                    output.Status,
                    step.OutputEntityId,
                    output.Detail,
                    step));
            }
        }

        FlowPortDiagnostics.ReplaceAll(diagnostics);
        OnPropertyChanged(nameof(FlowProblemsSummary));
        OnPropertyChanged(nameof(HasFlowProblems));
        NotifySelectedStepFlowProblemChanged();
        RebuildRecipeHealthProjection();
    }

    private void NotifySelectedStepFlowProblemChanged()
    {
        OnPropertyChanged(nameof(SelectedStepFlowProblem));
        OnPropertyChanged(nameof(HasSelectedStepFlowProblem));
        NotifyRecipeHealthSelectionChanged();
    }

    private void RebuildRecipeHealthProjection()
    {
        RecipeHealthItems.ReplaceAll(PipelineSteps.Select(CreateRecipeHealthItem));
        OnPropertyChanged(nameof(RecipeHealthReadyCount));
        OnPropertyChanged(nameof(RecipeHealthNeedsInputCount));
        OnPropertyChanged(nameof(RecipeHealthNeedsSelectionCount));
        OnPropertyChanged(nameof(RecipeHealthNeedsParametersCount));
        OnPropertyChanged(nameof(RecipeHealthStalePreviewCount));
        OnPropertyChanged(nameof(RecipeHealthPublishedCount));
        OnPropertyChanged(nameof(RecipeHealthIssueCount));
        OnPropertyChanged(nameof(RecipeHealthSummary));
        OnPropertyChanged(nameof(RecipeHealthCountsPrimary));
        OnPropertyChanged(nameof(RecipeHealthCountsSecondary));
        NotifyRecipeHealthSelectionChanged();
    }

    private ToolWorkbenchRecipeHealthItem CreateRecipeHealthItem(
        ToolWorkbenchPipelineStepItem step)
    {
        if (string.Equals(step.State, "Published", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.Published,
                Localization.RecipeHealthPublished,
                Localization.RecipeHealthPublishedDetail);
        }

        if (string.Equals(step.State, "Preview stale", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.StalePreview,
                Localization.RecipeHealthStalePreview,
                Localization.RecipeHealthStalePreviewDetail);
        }

        var input = DescribeInputPort(step);
        if (input.IsProblem)
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.NeedsInput,
                Localization.RecipeHealthNeedsInput,
                input.Detail);
        }

        if (TryDescribeMissingSelection(step, out var selectionDetail))
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.NeedsSelection,
                Localization.RecipeHealthNeedsSelection,
                selectionDetail);
        }

        if (TryDescribeParameterRequirement(step, out var parameterDetail))
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.NeedsParameters,
                Localization.RecipeHealthNeedsParameters,
                parameterDetail);
        }

        return CreateRecipeHealthItem(
            step,
            ToolWorkbenchRecipeHealthCategory.Ready,
            Localization.RecipeHealthReady,
            Localization.RecipeHealthReadyDetail);
    }

    private ToolWorkbenchRecipeHealthItem CreateRecipeHealthItem(
        ToolWorkbenchPipelineStepItem step,
        ToolWorkbenchRecipeHealthCategory category,
        string label,
        string detail) =>
        new(
            category,
            label,
            detail,
            string.Format(
                Localization.RecipeHealthStepTitleFormat,
                step.Order,
                step.ToolName,
                label),
            step);

    private bool TryDescribeMissingSelection(
        ToolWorkbenchPipelineStepItem step,
        out string detail)
    {
        detail = string.Empty;
        if (IsDualRoiMeasurementTool(step.ToolId))
        {
            var routed = GetRoutedSelections(step)
                .Where(selection =>
                    string.Equals(
                        selection.Kind,
                        ToolRecipeSelectionKinds.GridRectangle,
                        StringComparison.OrdinalIgnoreCase)
                    && selection.GridRectangle is not null
                    && IsSelectionCurrent(selection))
                .ToArray();
            if (routed.Length >= 2)
            {
                return false;
            }

            detail = string.Format(
                Localization.RecipeHealthDualRoiRequiredFormat,
                routed.Length,
                2);
            return true;
        }

        var requirement = CreateSelectionRequirement(step);
        if (requirement is null)
        {
            return false;
        }

        var hasCurrentSelection = GetRoutedSelections(step).Any(selection =>
            SelectionMatchesRequirement(selection, requirement)
            && IsSelectionCurrent(selection));
        if (hasCurrentSelection)
        {
            return false;
        }

        var kind = requirement.Kind switch
        {
            ToolRecipeSelectionKinds.GridRectangle => Localization.RecipeHealthGridRectangle,
            ToolRecipeSelectionKinds.PointSet => string.Format(
                Localization.RecipeHealthPointSetFormat,
                requirement.RequiredPointCount),
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet =>
                Localization.RecipeHealthLandmarkSet,
            _ => requirement.Kind
        };
        detail = string.Format(
            Localization.RecipeHealthSelectionRequiredFormat,
            kind);
        return true;
    }

    private IEnumerable<ToolRecipeSelection> GetRoutedSelections(
        ToolWorkbenchPipelineStepItem step)
    {
        var routedIds = step.DualRoiRouting is { } routing
            ? step.InputEntityIds
                .Concat(
                [
                    routing.FirstRegionSelectionId ?? string.Empty,
                    routing.SecondRegionSelectionId ?? string.Empty
                ])
            : step.InputEntityIds;
        return routedIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => Selections.FirstOrDefault(selection =>
                string.Equals(selection.Id, id, StringComparison.OrdinalIgnoreCase)))
            .OfType<ToolRecipeSelection>();
    }

    private static bool IsDualRoiMeasurementTool(string toolId) =>
        toolId is "thickness" or "plane-flatness" or "gap-flush" or "volume" or "completeness-grid";

    private static bool TryDescribeParameterRequirement(
        ToolWorkbenchPipelineStepItem step,
        out string detail)
    {
        detail = string.Empty;
        if (!ToolWorkbenchStepPropertySession.IsSupportedTool(step))
        {
            return false;
        }

        var session = new ToolWorkbenchStepPropertySession();
        session.Refresh(step);
        if (session.TryCreateParameterValues(step, out _, out var message))
        {
            return false;
        }

        detail = message;
        return true;
    }

    private int CountRecipeHealth(ToolWorkbenchRecipeHealthCategory category) =>
        RecipeHealthItems.Count(item => item.Category == category);

    private bool CanNavigateRecipeHealth =>
        !IsRecipeMutationBlocked
        && !HasPendingStepParameterChanges
        && !IsTeachingSelectionCaptureActive
        && !ThicknessRepeatGrid.IsActive
        && !OrientedBoxEditor.IsDraftOpen;

    private ToolWorkbenchRecipeHealthItem? FindPreviousRecipeHealthIssue()
    {
        var selectedIndex = SelectedPipelineStep is null
            ? PipelineSteps.Count
            : PipelineSteps.IndexOf(SelectedPipelineStep);
        return RecipeHealthItems
            .Where(item => item.IsIssue && PipelineSteps.IndexOf(item.Step) < selectedIndex)
            .LastOrDefault();
    }

    private ToolWorkbenchRecipeHealthItem? FindNextRecipeHealthIssue()
    {
        var selectedIndex = SelectedPipelineStep is null
            ? -1
            : PipelineSteps.IndexOf(SelectedPipelineStep);
        return RecipeHealthItems.FirstOrDefault(item =>
            item.IsIssue && PipelineSteps.IndexOf(item.Step) > selectedIndex);
    }

    private void NavigateRecipeHealthIssue(bool forward)
    {
        var item = forward
            ? FindNextRecipeHealthIssue()
            : FindPreviousRecipeHealthIssue();
        if (item is null || !CanNavigateRecipeHealth)
        {
            return;
        }

        deferSelectedStepStateRefresh = true;
        try
        {
            SelectedPipelineStep = item.Step;
        }
        finally
        {
            deferSelectedStepStateRefresh = false;
        }

        RefreshTeachingSelectionContext();
        RefreshStepCommands();
        RefreshNavigatorSelection();
    }

    private void NotifyRecipeHealthSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedRecipeHealthItem));
        OnPropertyChanged(nameof(SelectedRecipeHealthTitle));
        OnPropertyChanged(nameof(SelectedRecipeHealthDetail));
        OnPropertyChanged(nameof(CanNavigatePreviousRecipeHealthIssue));
        OnPropertyChanged(nameof(CanNavigateNextRecipeHealthIssue));
        previousRecipeHealthIssueCommand?.RaiseCanExecuteChanged();
        nextRecipeHealthIssueCommand?.RaiseCanExecuteChanged();
    }

    private FlowPortPresentation DescribeInputPort(ToolWorkbenchPipelineStepItem step)
    {
        if (step.InputEntityIds.Count == 0)
        {
            return new FlowPortPresentation(
                "Unresolved",
                Localization.FlowPortUnresolved,
                Localization.FlowPortNoInputDetail,
                true);
        }

        var primaryInputId = step.InputEntityIds[0];
        var primaryArtifact = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Id, primaryInputId, StringComparison.OrdinalIgnoreCase));
        if (primaryArtifact is not null
            && ToolRecipePrimaryInputContract.TryGetRequiredContract(
                step.ToolId,
                out var requiredContract)
            && !ToolRecipePrimaryInputContract.IsCompatible(step.ToolId, primaryArtifact.Contract))
        {
            return new FlowPortPresentation(
                "Incompatible",
                Localization.FlowPortIncompatible,
                string.Format(
                    Localization.FlowPortIncompatibleDetailFormat,
                    primaryInputId,
                    primaryArtifact.Contract,
                    requiredContract),
                true);
        }

        var assessments = step.InputEntityIds
            .Select(DescribeInputArtifact)
            .OrderByDescending(assessment => assessment.Priority)
            .ToArray();
        var primary = assessments[0];
        var detail = string.Join(" ", assessments
            .Where(assessment => assessment.IsProblem)
            .Select(assessment => assessment.Detail));

        return new FlowPortPresentation(
            primary.Kind,
            primary.Status,
            string.IsNullOrWhiteSpace(detail)
                ? string.Join(" | ", assessments.Select(assessment => assessment.Detail))
                : detail,
            primary.IsProblem);
    }

    private FlowPortAssessment DescribeInputArtifact(string inputId)
    {
        var producingStep = PipelineSteps.FirstOrDefault(step =>
            string.Equals(step.OutputEntityId, inputId, StringComparison.OrdinalIgnoreCase));
        if (producingStep is not null)
        {
            if (string.Equals(producingStep.State, "Published", StringComparison.OrdinalIgnoreCase))
            {
                return new FlowPortAssessment(
                    "Ready",
                    Localization.FlowPortReady,
                    $"{inputId} | Published",
                    false,
                    0);
            }

            if (string.Equals(producingStep.State, "Preview stale", StringComparison.OrdinalIgnoreCase))
            {
                return new FlowPortAssessment(
                    "Stale",
                    Localization.FlowPortStale,
                    string.Format(Localization.FlowPortStaleDetailFormat, inputId),
                    true,
                    2);
            }

            return new FlowPortAssessment(
                "WaitingForUpstream",
                Localization.FlowPortWaitingForUpstream,
                string.Format(Localization.FlowPortWaitingDetailFormat, inputId),
                true,
                1);
        }

        var artifact = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Id, inputId, StringComparison.OrdinalIgnoreCase));
        if (artifact is null)
        {
            return new FlowPortAssessment(
                "Unresolved",
                Localization.FlowPortUnresolved,
                string.Format(Localization.FlowPortUnresolvedDetailFormat, inputId),
                true,
                3);
        }

        if (IsStaleArtifact(artifact))
        {
            return new FlowPortAssessment(
                "Stale",
                Localization.FlowPortStale,
                string.Format(Localization.FlowPortStaleDetailFormat, inputId),
                true,
                2);
        }

        if (string.Equals(artifact.State, "Declared", StringComparison.OrdinalIgnoreCase))
        {
            return new FlowPortAssessment(
                "WaitingForUpstream",
                Localization.FlowPortWaitingForUpstream,
                string.Format(Localization.FlowPortWaitingDetailFormat, inputId),
                true,
                1);
        }

        if (IsCurrentArtifact(artifact))
        {
            return new FlowPortAssessment(
                "Ready",
                Localization.FlowPortReady,
                $"{inputId} | {artifact.State}",
                false,
                0);
        }

        return new FlowPortAssessment(
            "Unresolved",
            Localization.FlowPortUnresolved,
            string.Format(Localization.FlowPortUnresolvedDetailFormat, inputId),
            true,
            3);
    }

    private FlowPortPresentation DescribeOutputPort(ToolWorkbenchPipelineStepItem step)
    {
        var artifact = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Id, step.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (artifact is null)
        {
            return new FlowPortPresentation(
                "Unresolved",
                Localization.FlowPortUnresolved,
                string.Format(Localization.FlowPortUnresolvedDetailFormat, step.OutputEntityId),
                true);
        }

        if (IsStaleArtifact(artifact))
        {
            return new FlowPortPresentation(
                "Stale",
                Localization.FlowPortStale,
                string.Format(Localization.FlowPortStaleDetailFormat, step.OutputEntityId),
                true);
        }

        if (string.Equals(artifact.State, "Declared", StringComparison.OrdinalIgnoreCase))
        {
            return new FlowPortPresentation(
                "Declared",
                Localization.FlowPortDeclared,
                string.Format(Localization.FlowPortDeclaredDetailFormat, step.OutputEntityId),
                false);
        }

        if (IsCurrentArtifact(artifact))
        {
            return new FlowPortPresentation(
                "Current",
                Localization.FlowPortCurrent,
                string.Format(Localization.FlowPortCurrentDetailFormat, step.OutputEntityId),
                false);
        }

        return new FlowPortPresentation(
            "Unresolved",
            Localization.FlowPortUnresolved,
            string.Format(Localization.FlowPortUnresolvedDetailFormat, step.OutputEntityId),
            true);
    }

    private void FocusFlowProblemStep(ToolWorkbenchFlowPortDiagnosticItem? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedPipelineStep = item.Step;
        IsSelectedToolInputSectionExpanded = true;
        IsAdvancedInputRouteEditingExpanded = true;
        RefreshNavigatorSelection();
    }

    private static bool IsCurrentArtifact(ToolWorkbenchArtifactItem artifact) =>
        artifact.State is "Ready" or "Current selection" or "Preview" or "Published";

    private static bool IsStaleArtifact(ToolWorkbenchArtifactItem artifact) =>
        artifact.State.StartsWith("Stale", StringComparison.OrdinalIgnoreCase);

    private sealed record FlowPortPresentation(
        string Kind,
        string Status,
        string Detail,
        bool IsProblem);

    private sealed record FlowPortAssessment(
        string Kind,
        string Status,
        string Detail,
        bool IsProblem,
        int Priority);
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
