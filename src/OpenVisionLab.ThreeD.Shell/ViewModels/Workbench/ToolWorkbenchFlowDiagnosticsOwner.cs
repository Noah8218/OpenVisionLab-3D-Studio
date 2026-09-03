using System.ComponentModel;
using System.Threading;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the read-only flow-port and Recipe Health projection used by the
/// Workbench. Recipe/source/artifact mutation remains behind explicit facade
/// callbacks; this owner only evaluates state, refreshes presentation, and
/// navigates to an existing pipeline step.
/// </summary>
internal sealed class ToolWorkbenchFlowDiagnosticsOwner : INotifyPropertyChanged, IDisposable
{
    private readonly ThreeDLocalization localization;
    private readonly Func<IEnumerable<ToolWorkbenchPipelineStepItem>> getPipelineSteps;
    private readonly Func<IEnumerable<ToolWorkbenchArtifactItem>> getArtifacts;
    private readonly Func<IEnumerable<ToolRecipeSelection>> getSelections;
    private readonly Func<int> getValidationMessageCount;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedStep;
    private readonly Action<ToolWorkbenchPipelineStepItem, bool> selectPipelineStep;
    private readonly Func<ToolWorkbenchPipelineStepItem, ToolWorkbenchTeachingSelectionRequirement?> createSelectionRequirement;
    private readonly Func<ToolRecipeSelection, bool> isSelectionCurrent;
    private readonly Func<bool> isRecipeMutationBlocked;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<bool> isTeachingSelectionCaptureActive;
    private readonly Func<bool> isThicknessRepeatGridActive;
    private readonly Func<bool> isOrientedBoxDraftOpen;
    private readonly Action refreshTeachingSelectionContext;
    private readonly Action refreshStepCommands;
    private readonly Action refreshNavigatorSelection;
    private readonly RelayCommand focusFlowProblemStepCommand;
    private readonly RelayCommand previousRecipeHealthIssueCommand;
    private readonly RelayCommand nextRecipeHealthIssueCommand;

    private bool isSelectedToolInputSectionExpanded;
    private bool isAdvancedInputRouteEditingExpanded;
    private int disposalState;

    public ToolWorkbenchFlowDiagnosticsOwner(
        ThreeDLocalization localization,
        Func<IEnumerable<ToolWorkbenchPipelineStepItem>> getPipelineSteps,
        Func<IEnumerable<ToolWorkbenchArtifactItem>> getArtifacts,
        Func<IEnumerable<ToolRecipeSelection>> getSelections,
        Func<int> getValidationMessageCount,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedStep,
        Action<ToolWorkbenchPipelineStepItem, bool> selectPipelineStep,
        Func<ToolWorkbenchPipelineStepItem, ToolWorkbenchTeachingSelectionRequirement?> createSelectionRequirement,
        Func<ToolRecipeSelection, bool> isSelectionCurrent,
        Func<bool> isRecipeMutationBlocked,
        Func<bool> hasPendingStepParameterChanges,
        Func<bool> isTeachingSelectionCaptureActive,
        Func<bool> isThicknessRepeatGridActive,
        Func<bool> isOrientedBoxDraftOpen,
        Action refreshTeachingSelectionContext,
        Action refreshStepCommands,
        Action refreshNavigatorSelection)
    {
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        this.getPipelineSteps = getPipelineSteps ?? throw new ArgumentNullException(nameof(getPipelineSteps));
        this.getArtifacts = getArtifacts ?? throw new ArgumentNullException(nameof(getArtifacts));
        this.getSelections = getSelections ?? throw new ArgumentNullException(nameof(getSelections));
        this.getValidationMessageCount = getValidationMessageCount
            ?? throw new ArgumentNullException(nameof(getValidationMessageCount));
        this.getSelectedStep = getSelectedStep ?? throw new ArgumentNullException(nameof(getSelectedStep));
        this.selectPipelineStep = selectPipelineStep ?? throw new ArgumentNullException(nameof(selectPipelineStep));
        this.createSelectionRequirement = createSelectionRequirement
            ?? throw new ArgumentNullException(nameof(createSelectionRequirement));
        this.isSelectionCurrent = isSelectionCurrent
            ?? throw new ArgumentNullException(nameof(isSelectionCurrent));
        this.isRecipeMutationBlocked = isRecipeMutationBlocked
            ?? throw new ArgumentNullException(nameof(isRecipeMutationBlocked));
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges
            ?? throw new ArgumentNullException(nameof(hasPendingStepParameterChanges));
        this.isTeachingSelectionCaptureActive = isTeachingSelectionCaptureActive
            ?? throw new ArgumentNullException(nameof(isTeachingSelectionCaptureActive));
        this.isThicknessRepeatGridActive = isThicknessRepeatGridActive
            ?? throw new ArgumentNullException(nameof(isThicknessRepeatGridActive));
        this.isOrientedBoxDraftOpen = isOrientedBoxDraftOpen
            ?? throw new ArgumentNullException(nameof(isOrientedBoxDraftOpen));
        this.refreshTeachingSelectionContext = refreshTeachingSelectionContext
            ?? throw new ArgumentNullException(nameof(refreshTeachingSelectionContext));
        this.refreshStepCommands = refreshStepCommands
            ?? throw new ArgumentNullException(nameof(refreshStepCommands));
        this.refreshNavigatorSelection = refreshNavigatorSelection
            ?? throw new ArgumentNullException(nameof(refreshNavigatorSelection));

        focusFlowProblemStepCommand = new RelayCommand(
            parameter => FocusFlowProblemStep(parameter as ToolWorkbenchFlowPortDiagnosticItem),
            parameter => parameter is ToolWorkbenchFlowPortDiagnosticItem);
        previousRecipeHealthIssueCommand = new RelayCommand(
            _ => NavigateRecipeHealthIssue(forward: false),
            _ => CanNavigatePreviousRecipeHealthIssue);
        nextRecipeHealthIssueCommand = new RelayCommand(
            _ => NavigateRecipeHealthIssue(forward: true),
            _ => CanNavigateNextRecipeHealthIssue);
        localization.PropertyChanged += OnLocalizationChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        localization.PropertyChanged -= OnLocalizationChanged;
    }

    public ResettableObservableCollection<ToolWorkbenchFlowPortDiagnosticItem> FlowPortDiagnostics { get; } = [];
    public ResettableObservableCollection<ToolWorkbenchRecipeHealthItem> RecipeHealthItems { get; } = [];

    public ICommand FocusFlowProblemStepCommand => focusFlowProblemStepCommand;
    public ICommand PreviousRecipeHealthIssueCommand => previousRecipeHealthIssueCommand;
    public ICommand NextRecipeHealthIssueCommand => nextRecipeHealthIssueCommand;

    public string FlowProblemsSummary => string.Format(
        localization.ProblemsSummaryFormat,
        FlowPortDiagnostics.Count,
        getValidationMessageCount());

    public bool HasFlowProblems => FlowPortDiagnostics.Count > 0 || getValidationMessageCount() > 0;

    public ToolWorkbenchFlowPortDiagnosticItem? SelectedStepFlowProblem =>
        getSelectedStep() is { } selectedStep
            ? FlowPortDiagnostics.FirstOrDefault(item =>
                item.Port == "Input"
                && ReferenceEquals(item.Step, selectedStep))
            : null;

    public bool HasSelectedStepFlowProblem => SelectedStepFlowProblem is not null;

    public int RecipeHealthReadyCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.Ready);
    public int RecipeHealthNeedsInputCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.NeedsInput);
    public int RecipeHealthNeedsSelectionCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.NeedsSelection);
    public int RecipeHealthNeedsParametersCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.NeedsParameters);
    public int RecipeHealthStalePreviewCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.StalePreview);
    public int RecipeHealthPublishedCount => CountRecipeHealth(ToolWorkbenchRecipeHealthCategory.Published);
    public int RecipeHealthIssueCount => RecipeHealthItems.Count(item => item.IsIssue);

    public string RecipeHealthSummary => string.Format(
        localization.RecipeHealthSummaryFormat,
        RecipeHealthIssueCount);

    public string RecipeHealthCountsPrimary => string.Format(
        localization.RecipeHealthCountsPrimaryFormat,
        RecipeHealthReadyCount,
        RecipeHealthNeedsInputCount,
        RecipeHealthNeedsSelectionCount);

    public string RecipeHealthCountsSecondary => string.Format(
        localization.RecipeHealthCountsSecondaryFormat,
        RecipeHealthNeedsParametersCount,
        RecipeHealthStalePreviewCount,
        RecipeHealthPublishedCount);

    public ToolWorkbenchRecipeHealthItem? SelectedRecipeHealthItem =>
        getSelectedStep() is { } selectedStep
            ? RecipeHealthItems.FirstOrDefault(item => ReferenceEquals(item.Step, selectedStep))
            : null;

    public string SelectedRecipeHealthTitle => SelectedRecipeHealthItem?.Title
        ?? localization.RecipeHealthNoStep;

    public string SelectedRecipeHealthDetail => SelectedRecipeHealthItem?.Detail
        ?? localization.RecipeHealthNoStepDetail;

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

    public void RebuildFlowPortDiagnostics()
    {
        var steps = getPipelineSteps().ToArray();
        var artifacts = getArtifacts().ToArray();
        var selections = getSelections().ToArray();
        var diagnostics = new List<ToolWorkbenchFlowPortDiagnosticItem>();
        foreach (var step in steps)
        {
            var input = DescribeInputPort(step, steps, artifacts);
            var output = DescribeOutputPort(step, artifacts);
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
        RebuildRecipeHealthProjection(steps, artifacts, selections);
    }

    public void RebuildRecipeHealthProjection()
    {
        RebuildRecipeHealthProjection(
            getPipelineSteps().ToArray(),
            getArtifacts().ToArray(),
            getSelections().ToArray());
    }

    public void NotifySelectedStepFlowProblemChanged()
    {
        OnPropertyChanged(nameof(SelectedStepFlowProblem));
        OnPropertyChanged(nameof(HasSelectedStepFlowProblem));
        NotifyRecipeHealthSelectionChanged();
    }

    public void NotifyRecipeHealthSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedRecipeHealthItem));
        OnPropertyChanged(nameof(SelectedRecipeHealthTitle));
        OnPropertyChanged(nameof(SelectedRecipeHealthDetail));
        OnPropertyChanged(nameof(CanNavigatePreviousRecipeHealthIssue));
        OnPropertyChanged(nameof(CanNavigateNextRecipeHealthIssue));
        previousRecipeHealthIssueCommand.RaiseCanExecuteChanged();
        nextRecipeHealthIssueCommand.RaiseCanExecuteChanged();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs args) =>
        RebuildFlowPortDiagnostics();

    private void RebuildRecipeHealthProjection(
        IReadOnlyList<ToolWorkbenchPipelineStepItem> steps,
        IReadOnlyList<ToolWorkbenchArtifactItem> artifacts,
        IReadOnlyList<ToolRecipeSelection> selections)
    {
        RecipeHealthItems.ReplaceAll(steps.Select(step => CreateRecipeHealthItem(step, steps, artifacts, selections)));
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
        ToolWorkbenchPipelineStepItem step,
        IReadOnlyList<ToolWorkbenchPipelineStepItem> steps,
        IReadOnlyList<ToolWorkbenchArtifactItem> artifacts,
        IReadOnlyList<ToolRecipeSelection> selections)
    {
        if (string.Equals(step.State, "Published", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.Published,
                localization.RecipeHealthPublished,
                localization.RecipeHealthPublishedDetail);
        }

        if (string.Equals(step.State, "Preview stale", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.StalePreview,
                localization.RecipeHealthStalePreview,
                localization.RecipeHealthStalePreviewDetail);
        }

        var input = DescribeInputPort(step, steps, artifacts);
        if (input.IsProblem)
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.NeedsInput,
                localization.RecipeHealthNeedsInput,
                input.Detail);
        }

        if (TryDescribeMissingSelection(step, selections, out var selectionDetail))
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.NeedsSelection,
                localization.RecipeHealthNeedsSelection,
                selectionDetail);
        }

        if (TryDescribeParameterRequirement(step, out var parameterDetail))
        {
            return CreateRecipeHealthItem(
                step,
                ToolWorkbenchRecipeHealthCategory.NeedsParameters,
                localization.RecipeHealthNeedsParameters,
                parameterDetail);
        }

        return CreateRecipeHealthItem(
            step,
            ToolWorkbenchRecipeHealthCategory.Ready,
            localization.RecipeHealthReady,
            localization.RecipeHealthReadyDetail);
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
                localization.RecipeHealthStepTitleFormat,
                step.Order,
                step.ToolName,
                label),
            step);

    private bool TryDescribeMissingSelection(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlyList<ToolRecipeSelection> selections,
        out string detail)
    {
        detail = string.Empty;
        if (IsDualRoiMeasurementTool(step.ToolId))
        {
            var routed = GetRoutedSelections(step, selections)
                .Where(selection =>
                    string.Equals(
                        selection.Kind,
                        ToolRecipeSelectionKinds.GridRectangle,
                        StringComparison.OrdinalIgnoreCase)
                    && selection.GridRectangle is not null
                    && isSelectionCurrent(selection))
                .ToArray();
            if (routed.Length >= 2)
            {
                return false;
            }

            detail = string.Format(
                localization.RecipeHealthDualRoiRequiredFormat,
                routed.Length,
                2);
            return true;
        }

        var requirement = createSelectionRequirement(step);
        if (requirement is null)
        {
            return false;
        }

        var hasCurrentSelection = GetRoutedSelections(step, selections).Any(selection =>
            SelectionMatchesRequirement(selection, requirement)
            && isSelectionCurrent(selection));
        if (hasCurrentSelection)
        {
            return false;
        }

        var kind = requirement.Kind switch
        {
            ToolRecipeSelectionKinds.GridRectangle => localization.RecipeHealthGridRectangle,
            ToolRecipeSelectionKinds.PointSet => string.Format(
                localization.RecipeHealthPointSetFormat,
                requirement.RequiredPointCount),
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet =>
                localization.RecipeHealthLandmarkSet,
            _ => requirement.Kind
        };
        detail = string.Format(
            localization.RecipeHealthSelectionRequiredFormat,
            kind);
        return true;
    }

    private static IEnumerable<ToolRecipeSelection> GetRoutedSelections(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlyList<ToolRecipeSelection> selections)
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
            .Select(id => selections.FirstOrDefault(selection =>
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
        !isRecipeMutationBlocked()
        && !hasPendingStepParameterChanges()
        && !isTeachingSelectionCaptureActive()
        && !isThicknessRepeatGridActive()
        && !isOrientedBoxDraftOpen();

    private ToolWorkbenchRecipeHealthItem? FindPreviousRecipeHealthIssue()
    {
        var steps = getPipelineSteps().ToArray();
        var selectedStep = getSelectedStep();
        var selectedIndex = selectedStep is null
            ? steps.Length
            : Array.IndexOf(steps, selectedStep);
        return RecipeHealthItems
            .Where(item => item.IsIssue && Array.IndexOf(steps, item.Step) < selectedIndex)
            .LastOrDefault();
    }

    private ToolWorkbenchRecipeHealthItem? FindNextRecipeHealthIssue()
    {
        var steps = getPipelineSteps().ToArray();
        var selectedStep = getSelectedStep();
        var selectedIndex = selectedStep is null
            ? -1
            : Array.IndexOf(steps, selectedStep);
        return RecipeHealthItems.FirstOrDefault(item =>
            item.IsIssue && Array.IndexOf(steps, item.Step) > selectedIndex);
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

        selectPipelineStep(item.Step, true);
        refreshTeachingSelectionContext();
        refreshStepCommands();
        refreshNavigatorSelection();
    }

    private void FocusFlowProblemStep(ToolWorkbenchFlowPortDiagnosticItem? item)
    {
        if (item is null)
        {
            return;
        }

        selectPipelineStep(item.Step, false);
        IsSelectedToolInputSectionExpanded = true;
        IsAdvancedInputRouteEditingExpanded = true;
        refreshNavigatorSelection();
    }

    private FlowPortPresentation DescribeInputPort(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlyList<ToolWorkbenchPipelineStepItem> steps,
        IReadOnlyList<ToolWorkbenchArtifactItem> artifacts)
    {
        if (step.InputEntityIds.Count == 0)
        {
            return new FlowPortPresentation(
                "Unresolved",
                localization.FlowPortUnresolved,
                localization.FlowPortNoInputDetail,
                true);
        }

        var primaryInputId = step.InputEntityIds[0];
        var primaryArtifact = artifacts.FirstOrDefault(item =>
            string.Equals(item.Id, primaryInputId, StringComparison.OrdinalIgnoreCase));
        if (primaryArtifact is not null
            && ToolRecipePrimaryInputContract.TryGetRequiredContract(
                step.ToolId,
                out var requiredContract)
            && !ToolRecipePrimaryInputContract.IsCompatible(step.ToolId, primaryArtifact.Contract))
        {
            return new FlowPortPresentation(
                "Incompatible",
                localization.FlowPortIncompatible,
                string.Format(
                    localization.FlowPortIncompatibleDetailFormat,
                    primaryInputId,
                    primaryArtifact.Contract,
                    requiredContract),
                true);
        }

        var assessments = step.InputEntityIds
            .Select(inputId => DescribeInputArtifact(inputId, steps, artifacts))
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

    private FlowPortAssessment DescribeInputArtifact(
        string inputId,
        IReadOnlyList<ToolWorkbenchPipelineStepItem> steps,
        IReadOnlyList<ToolWorkbenchArtifactItem> artifacts)
    {
        var producingStep = steps.FirstOrDefault(step =>
            string.Equals(step.OutputEntityId, inputId, StringComparison.OrdinalIgnoreCase));
        if (producingStep is not null)
        {
            if (string.Equals(producingStep.State, "Published", StringComparison.OrdinalIgnoreCase))
            {
                return new FlowPortAssessment(
                    "Ready",
                    localization.FlowPortReady,
                    $"{inputId} | Published",
                    false,
                    0);
            }

            if (string.Equals(producingStep.State, "Preview stale", StringComparison.OrdinalIgnoreCase))
            {
                return new FlowPortAssessment(
                    "Stale",
                    localization.FlowPortStale,
                    string.Format(localization.FlowPortStaleDetailFormat, inputId),
                    true,
                    2);
            }

            return new FlowPortAssessment(
                "WaitingForUpstream",
                localization.FlowPortWaitingForUpstream,
                string.Format(localization.FlowPortWaitingDetailFormat, inputId),
                true,
                1);
        }

        var artifact = artifacts.FirstOrDefault(item =>
            string.Equals(item.Id, inputId, StringComparison.OrdinalIgnoreCase));
        if (artifact is null)
        {
            return new FlowPortAssessment(
                "Unresolved",
                localization.FlowPortUnresolved,
                string.Format(localization.FlowPortUnresolvedDetailFormat, inputId),
                true,
                3);
        }

        if (IsStaleArtifact(artifact))
        {
            return new FlowPortAssessment(
                "Stale",
                localization.FlowPortStale,
                string.Format(localization.FlowPortStaleDetailFormat, inputId),
                true,
                2);
        }

        if (string.Equals(artifact.State, "Declared", StringComparison.OrdinalIgnoreCase))
        {
            return new FlowPortAssessment(
                "WaitingForUpstream",
                localization.FlowPortWaitingForUpstream,
                string.Format(localization.FlowPortWaitingDetailFormat, inputId),
                true,
                1);
        }

        if (IsCurrentArtifact(artifact))
        {
            return new FlowPortAssessment(
                "Ready",
                localization.FlowPortReady,
                $"{inputId} | {artifact.State}",
                false,
                0);
        }

        return new FlowPortAssessment(
            "Unresolved",
            localization.FlowPortUnresolved,
            string.Format(localization.FlowPortUnresolvedDetailFormat, inputId),
            true,
            3);
    }

    private FlowPortPresentation DescribeOutputPort(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlyList<ToolWorkbenchArtifactItem> artifacts)
    {
        var artifact = artifacts.FirstOrDefault(item =>
            string.Equals(item.Id, step.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (artifact is null)
        {
            return new FlowPortPresentation(
                "Unresolved",
                localization.FlowPortUnresolved,
                string.Format(localization.FlowPortUnresolvedDetailFormat, step.OutputEntityId),
                true);
        }

        if (IsStaleArtifact(artifact))
        {
            return new FlowPortPresentation(
                "Stale",
                localization.FlowPortStale,
                string.Format(localization.FlowPortStaleDetailFormat, step.OutputEntityId),
                true);
        }

        if (string.Equals(artifact.State, "Declared", StringComparison.OrdinalIgnoreCase))
        {
            return new FlowPortPresentation(
                "Declared",
                localization.FlowPortDeclared,
                string.Format(localization.FlowPortDeclaredDetailFormat, step.OutputEntityId),
                false);
        }

        if (IsCurrentArtifact(artifact))
        {
            return new FlowPortPresentation(
                "Current",
                localization.FlowPortCurrent,
                string.Format(localization.FlowPortCurrentDetailFormat, step.OutputEntityId),
                false);
        }

        return new FlowPortPresentation(
            "Unresolved",
            localization.FlowPortUnresolved,
            string.Format(localization.FlowPortUnresolvedDetailFormat, step.OutputEntityId),
            true);
    }

    private static bool SelectionMatchesRequirement(
        ToolRecipeSelection selection,
        ToolWorkbenchTeachingSelectionRequirement? requirement)
    {
        if (requirement is null
            || !string.Equals(selection.Kind, requirement.Kind, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return requirement.Kind switch
        {
            ToolRecipeSelectionKinds.GridRectangle => selection.GridRectangle is not null,
            ToolRecipeSelectionKinds.GridCircle => selection.GridCircle is not null,
            ToolRecipeSelectionKinds.GridPolygon => selection.GridPolygon is not null,
            ToolRecipeSelectionKinds.PointSet => selection.Points?.Count == requirement.RequiredPointCount,
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet => selection.Rows is not null,
            _ => false
        };
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

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
