using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Composes the independent workspace selection and selected-tool projection
/// with the existing recipe, teaching, PropertyGrid, and artifact owners.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private bool isSynchronizingInspectionWorkspace;
    private readonly Dictionary<string, (IReadOnlyList<Metric> Metrics, string SourceIdentity, string Value, string Unit, string Status, string? Previous)> teachingResultComparisons = new(StringComparer.Ordinal);
    private RelayCommand selectWorkspaceInputCommand = null!;
    private RelayCommand chooseWorkspaceInputCommand = null!;
    private RelayCommand selectWorkspaceRegionCommand = null!;
    private RelayCommand selectWorkspaceOutputCommand = null!;
    private RelayCommand fitWorkspaceRegionCommand = null!;
    private RelayCommand showWorkspaceOutputCommand = null!;
    private RelayCommand pinWorkspaceOutputCommand = null!;
    private RelayCommand compareWorkspaceOutputCommand = null!;
    public InspectionWorkspaceSelectionSession WorkspaceSelection { get; }
    public SelectedToolWorkspaceViewModel SelectedToolWorkspace { get; }
    public InspectionQueueWorkspaceViewModel InspectionQueue => inspectionQueueWorkspace!;
    public bool IsInspectionQueueRunning => inspectionQueueWorkspace?.IsRunning == true;
    public ResettableObservableCollection<TeachingMeasurementResultItem> TeachingResults { get; } = [];
    public bool HasTeachingResults => TeachingResults.Count > 0;
    private string TeachingResultSourceIdentity => $"{Source.Id}|{Source.Unit}|{Source.FrameId}|{SourceSession.SourceBinding?.ContentSha256}";
    public bool HasSelectedToolResultComparison => SelectedPipelineStep is { } step && teachingResultComparisons.TryGetValue(step.Id, out var comparison)
        && comparison.SourceIdentity == TeachingResultSourceIdentity && comparison.Previous is not null;
    public string SelectedToolResultComparisonText => HasSelectedToolResultComparison && SelectedPipelineStep is { } step
        && teachingResultComparisons.TryGetValue(step.Id, out var comparison)
        ? $"{comparison.Previous} → {comparison.Value} {comparison.Unit} · {comparison.Status}"
            + (SelectedTeachingResult?.IsCurrent == false ? " · " + Localize("다시 실행 필요", "Run again") : string.Empty) : Localize("변경 전 결과 없음", "No earlier result");
    public IReadOnlyList<TeachingResultSummaryItem> TeachingResultSummary => TeachingResults.GroupBy(row => row.State)
        .Select(group => new TeachingResultSummaryItem(group.Key, group.First().StatusText, group.First().StatusIcon,
            group.Select(row => row.Step.Id).Distinct(StringComparer.Ordinal).Count())).ToArray();
    public TeachingMeasurementResultItem? SelectedTeachingResult
    {
        get => TeachingResults.FirstOrDefault(item => ReferenceEquals(item.Step, SelectedPipelineStep));
        set
        {
            if (value is not null) SelectedPipelineStep = value.Step;
            OnPropertyChanged();
        }
    }
    public SelectedToolInputItem? ViewerInspectionInput =>
        SelectedToolWorkspace.Inputs.FirstOrDefault(item => item.IsSelected)
        ?? SelectedToolWorkspace.Inputs.FirstOrDefault();
    public SelectedToolOutputItem? ViewerInspectionOutput => SelectedToolWorkspace.CurrentOutput;
    public ICommand SelectWorkspaceInputCommand => selectWorkspaceInputCommand;
    public ICommand ChooseWorkspaceInputCommand => chooseWorkspaceInputCommand;
    public ICommand SelectWorkspaceRegionCommand => selectWorkspaceRegionCommand;
    public ICommand SelectWorkspaceOutputCommand => selectWorkspaceOutputCommand;
    public ICommand FitWorkspaceRegionCommand => fitWorkspaceRegionCommand;
    public ICommand ShowWorkspaceOutputCommand => showWorkspaceOutputCommand;
    public ICommand PinWorkspaceOutputCommand => pinWorkspaceOutputCommand;
    public ICommand CompareWorkspaceOutputCommand => compareWorkspaceOutputCommand;
    public event EventHandler? FitWorkspaceRegionRequested;
    public event EventHandler? OutputComparePaneRequested;

    private void InitializeInspectionQueue()
    {
        inspectionQueueWorkspace = new InspectionQueueWorkspaceViewModel(
            CreateDocument,
            () => RecipePath,
            GetInspectionQueueRecipeReadiness,
            ValidateInspectionQueueInput,
            RunInspectionQueueInputAsync,
            orderedRunExecutionOwner.Cancel,
            Localize,
            () =>
            {
                orderedRunExecutionOwner.RetryRecordSave();
                return CreateInspectionQueueRunOutcome();
            },
            () => !IsC3DSourceLoading && !IsRecipeMutationBlocked && !HasPendingStepParameterChanges);
        inspectionQueueWorkspace.PropertyChanged += OnInspectionQueuePropertyChanged;
    }

    private (bool CanRun, string Message) GetInspectionQueueRecipeReadiness(ToolRecipeDocument document)
    {
        if (document.Steps.FirstOrDefault(step => step.OutputEnabled && step.AcceptanceLimitsConfigured == false) is { } unconfigured)
            return (false, Localize($"{unconfigured.ToolName}: 판정 규격을 설정하고 레시피를 저장하세요.", $"{unconfigured.ToolName}: Configure acceptance limits and save the recipe."));
        var canRun = TryGetOrderedRunCapability(document, out var message, inspectionInput: true);
        return (canRun, canRun
            ? Localize(
                $"검사 준비 · 호환 입력 크기 {document.Source.GridWidth} × {document.Source.GridHeight}",
                $"Ready · compatible input size {document.Source.GridWidth} × {document.Source.GridHeight}")
            : message);
    }

    private (bool IsCompatible, string Message) ValidateInspectionQueueInput(
        ToolRecipeDocument document,
        string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return (false, Localize("입력 파일을 찾을 수 없습니다.", "Input file was not found."));
        }
        if (!string.Equals(Path.GetExtension(sourcePath), ".c3d", StringComparison.OrdinalIgnoreCase))
        {
            return (false, Localize("검사 입력은 C3D 파일이어야 합니다.", "Inspection input must be a C3D file."));
        }
        if (document.Source.GridWidth is not { } expectedWidth
            || document.Source.GridHeight is not { } expectedHeight)
        {
            return (false, Localize("저장된 레시피의 기준 격자 정보가 없습니다.", "The saved recipe has no reference grid identity."));
        }

        try
        {
            var identity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            if (!string.Equals(identity.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                || identity.GridWidth != expectedWidth
                || identity.GridHeight != expectedHeight)
            {
                return (false, Localize(
                    $"입력 격자가 레시피와 다릅니다. 레시피 {expectedWidth} × {expectedHeight}, 입력 {identity.GridWidth} × {identity.GridHeight}.",
                    $"Input grid is incompatible. Recipe: {expectedWidth} × {expectedHeight}; input: {identity.GridWidth} × {identity.GridHeight}."));
            }

            return (true, Localize("입력 파일과 레시피 격자가 호환됩니다.", "Input file and recipe grid are compatible."));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException
            or OverflowException)
        {
            return (false, exception.Message);
        }
    }

    private (bool CanRun, string Message) TryGetInspectionInputCapability(
        ToolRecipeDocument document,
        string sourcePath)
    {
        if (!TryGetOrderedRunCapability(document, out var message, inspectionInput: true))
        {
            return (false, message);
        }

        var input = ValidateInspectionQueueInput(document, sourcePath);
        return (input.IsCompatible, input.Message);
    }

    private async Task<InspectionQueueRunOutcome> RunInspectionQueueInputAsync(
        ToolRecipeDocument document,
        string recipePath,
        string sourcePath)
    {
        var previousResult = orderedRunExecutionOwner.Result;
        await orderedRunExecutionOwner.RunInspectionInputAsync(document, recipePath, sourcePath);
        if (ReferenceEquals(previousResult, orderedRunExecutionOwner.Result))
            return new InspectionQueueRunOutcome(orderedRunExecutionOwner.WasLastRunCancelled, null, null, orderedRunExecutionOwner.Summary);
        return CreateInspectionQueueRunOutcome();
    }

    private InspectionQueueRunOutcome CreateInspectionQueueRunOutcome()
    {
        return new InspectionQueueRunOutcome(
            orderedRunExecutionOwner.WasLastRunCancelled,
            orderedRunExecutionOwner.Record,
            orderedRunExecutionOwner.RecordPath,
            orderedRunExecutionOwner.Summary,
            orderedRunExecutionOwner.HasPendingRecordSave)
        {
            HeightMaps = orderedRunExecutionOwner.HeightMaps,
            MeasurementColors = orderedRunExecutionOwner.Result?.Steps.Where(step => step.ColorSnapshot is not null)
                .ToDictionary(step => step.StepId, step => step.ColorSnapshot!) ?? new Dictionary<string, HeightMeasurementColorSnapshot>()
        };
    }

    private void OnInspectionQueuePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(InspectionQueueWorkspaceViewModel.HasPendingRecordSave)) OnPropertyChanged(nameof(RetryOrderedRunRecordSaveCommand));
        if (args.PropertyName == nameof(InspectionQueueWorkspaceViewModel.SelectedStepResult) && IsInspectionFileView
            && inspectionQueueWorkspace?.SelectedStepResult is { } selectedResult
            && PipelineSteps.FirstOrDefault(step => step.Id == selectedResult.StepId) is { } selectedStep)
        {
            SelectedPipelineStep = selectedStep;
        }
        if (args.PropertyName == nameof(InspectionQueueWorkspaceViewModel.SelectedSample) && IsInspectionFileView)
        {
            viewerWorkspaceViewModel.RefreshInspectionFileContext();
        }
        if (args.PropertyName != nameof(InspectionQueueWorkspaceViewModel.IsRunning)) return;
        OnPropertyChanged(nameof(IsInspectionQueueRunning));
        OnPropertyChanged(nameof(IsRecipeMutationBlocked));
        OnPropertyChanged(nameof(CanEditRecipe));
        OnPropertyChanged(nameof(CanOpenRecipeInfoEditor));
        openRecipeInfoEditorCommand?.RaiseCanExecuteChanged();
        (OpenRecentTeachingRecipeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        sourceLoadOwner?.RefreshCommandStates();
        RefreshStepCommands();
        RefreshFlowConnectionCommands();
    }

    private bool CanChooseWorkspaceInput(SelectedToolInputChoice choice) =>
        CanEditFlowConnection && !IsReadOnlySampleView && !HasPendingFlowConnection && choice.IsAvailable
        && ReferenceEquals(choice.Port.Step, SelectedPipelineStep) && PipelineSteps.Contains(choice.Port.Step)
        && IsFlowOutputAvailable(choice.EntityId)
        && IsFlowInputCompatible(choice.Port, choice.EntityId)
        && !string.Equals(choice.Port.Step.InputEntityIds.ElementAtOrDefault(choice.Port.Index), choice.EntityId, StringComparison.OrdinalIgnoreCase);

    private Task EnsureSelectedToolPreviewAsync(HeightImageViewerViewModel image, string entityId)
    {
        if (GetRenderableSignedDeltaArtifact(entityId) is { } delta)
        {
            image.ShowSignedDeltaArtifactIfChanged(delta);
            return Task.CompletedTask;
        }
        var target = renderableC3DCatalogOwner.GetTarget(entityId);
        if (target is not { IsDisplayable: true })
        {
            image.ClearSource();
            return Task.CompletedTask;
        }
        var artifact = ArtifactRegistry.FirstOrDefault(item => string.Equals(item.Id, entityId, StringComparison.OrdinalIgnoreCase));
        return target.IsSource
            ? image.EnsureSourceAsync(target.C3DPath, entityId, artifact?.Unit ?? Source.Unit, artifact?.FrameId ?? Source.FrameId, GetOrLoadDecodedC3DSourceAsync)
            : image.EnsureSourceAsync(target.C3DPath, entityId, artifact?.Unit ?? string.Empty, artifact?.FrameId ?? string.Empty);
    }

    private void InitializeInspectionWorkspace()
    {
        chooseWorkspaceInputCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is not SelectedToolInputChoice choice || !CanChooseWorkspaceInput(choice)) return;
                BeginFlowConnection(choice.EntityId);
                ConnectFlowOutput(choice.Port);
                if (HasPendingFlowConnection)
                {
                    pendingFlowOutputEntityId = null;
                    RefreshFlowConnectionCommands();
                }
            }, parameter => parameter is SelectedToolInputChoice choice && CanChooseWorkspaceInput(choice));
        selectWorkspaceInputCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is SelectedToolInputItem input)
                {
                    WorkspaceSelection.SelectInput(input.EntityId);
                }
            },
            parameter => parameter is SelectedToolInputItem);
        selectWorkspaceRegionCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is SelectedToolRegionItem region)
                {
                    WorkspaceSelection.SelectRegion(region.Role, region.SelectionId);
                }
            },
            parameter => parameter is SelectedToolRegionItem);
        selectWorkspaceOutputCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is SelectedToolOutputItem output)
                {
                    WorkspaceSelection.SelectOutput(output.EntityId);
                }
            },
            parameter => parameter is SelectedToolOutputItem);
        fitWorkspaceRegionCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is not SelectedToolRegionItem region)
                {
                    return;
                }

                WorkspaceSelection.SelectRegion(region.Role, region.SelectionId);
                FitWorkspaceRegionRequested?.Invoke(this, EventArgs.Empty);
            },
            parameter => parameter is SelectedToolRegionItem
            {
                Lifecycle: InspectionWorkspaceRegionLifecycleState.Review
                    or InspectionWorkspaceRegionLifecycleState.Applied
            });
        showWorkspaceOutputCommand = new RelayCommand(
            parameter => ShowWorkspaceOutput(parameter as SelectedToolOutputItem),
            parameter => ResolveDisplayedOutput(parameter as SelectedToolOutputItem) is { CanShowInViewer: true });
        pinWorkspaceOutputCommand = new RelayCommand(
            parameter => PinWorkspaceOutput(parameter as SelectedToolOutputItem),
            parameter => ResolveDisplayedOutput(parameter as SelectedToolOutputItem) is { CanPinToCompare: true });
        compareWorkspaceOutputCommand = new RelayCommand(
            parameter => CompareWorkspaceOutput(parameter as SelectedToolOutputItem),
            parameter => ResolveDisplayedOutput(parameter as SelectedToolOutputItem) is
                { IsPinnedToCompare: true } or { CanPinToCompare: true });
    }

    private void SynchronizeInspectionWorkspace()
    {
        var step = SelectedPipelineStep;
        var sameStep = string.Equals(
            WorkspaceSelection.SelectedStepId,
            step?.Id,
            StringComparison.OrdinalIgnoreCase);
        var inputId = sameStep
                      && step?.InputEntityIds.Contains(
                          WorkspaceSelection.SelectedInputEntityId ?? string.Empty,
                          StringComparer.OrdinalIgnoreCase) == true
            ? WorkspaceSelection.SelectedInputEntityId
            : step?.InputEntityIds.FirstOrDefault();
        var role = GetInspectionWorkspaceRegionRole();
        var regionId = SelectedStepTeachingSelection?.Id;
        var outputId = sameStep && !string.IsNullOrWhiteSpace(WorkspaceSelection.SelectedOutputEntityId)
            ? WorkspaceSelection.SelectedOutputEntityId
            : step?.OutputEntityId;

        isSynchronizingInspectionWorkspace = true;
        try
        {
            WorkspaceSelection.SynchronizeTool(
                step?.Id,
                inputId,
                role,
                regionId,
                outputId);
        }
        finally
        {
            isSynchronizingInspectionWorkspace = false;
        }

        RefreshSelectedToolWorkspaceProjection();
    }

    private void OnInspectionWorkspaceSelectionChanged(
        InspectionWorkspaceSelectionChangedEventArgs args)
    {
        SynchronizeViewerWorkspaceFocus(args.Current.FocusedViewerSlotId);
        if (isSynchronizingInspectionWorkspace)
        {
            return;
        }

        if (IsSelectedStepDualRoiMeasurement
            && string.Equals(
                args.Current.SelectedStepId,
                SelectedPipelineStep?.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            var selectsSecondRole =
                args.Current.ActiveRegionRole is InspectionWorkspaceRegionRole.Measurement
                    or InspectionWorkspaceRegionRole.Second;
            var selectsFirstRole =
                args.Current.ActiveRegionRole is InspectionWorkspaceRegionRole.Reference
                    or InspectionWorkspaceRegionRole.First;
            if ((selectsFirstRole || selectsSecondRole)
                && isPlaneFlatnessMeasurementRole != selectsSecondRole)
            {
                isPlaneFlatnessMeasurementRole = selectsSecondRole;
                NotifyPlaneFlatnessTeachingState();
                RefreshTeachingSelectionContext();
                return;
            }
        }

        RefreshSelectedToolWorkspaceProjection();
    }

    private void CompleteWorkspaceProjectionUpdate()
    {
        if (--workspaceProjectionUpdateDepth == 0 && workspaceProjectionPending)
        {
            workspaceProjectionPending = false;
            RefreshSelectedToolWorkspaceProjection();
        }
    }

    private void RefreshSelectedToolWorkspaceProjection()
    {
        // Execution owners still refresh synchronously; project their final state once per outer update.
        if (workspaceProjectionUpdateDepth > 0)
        {
            workspaceProjectionPending = true;
            return;
        }
        OnPropertyChanged(nameof(IsSelectedStepAcceptanceLimitsUnconfigured));
        configureSelectedStepAcceptanceLimitsCommand?.RaiseCanExecuteChanged();
        SelectedToolWorkspace.Refresh(new SelectedToolWorkspaceProjection(
            SelectedPipelineStep,
            SelectedStepPropertyDraft,
            IsSelectedStepPropertyGridSupported,
            HasPendingStepParameterChanges,
            StepParameterEditStatus == "Thickness limits must be finite and ordered."
                ? Localize("두께는 유한한 수로 입력하고 최소 두께를 최대 두께 이하로 설정하세요.", "Enter finite thickness values and set the minimum at or below the maximum.")
                : StepParameterEditStatus == "Maximum P2V raw height must be finite and greater than zero."
                    ? OpenVisionLab.ThreeD.Viewer.Localization.ViewerLocalization.Shared.LocalizeRuntimeText(StepParameterEditStatus, "ToolLabSummary") : StepParameterEditStatus,
            ArtifactRegistry.ToArray(),
            DisplayedOutputs.ToArray(),
            CreateSelectedToolOutputEvidence(),
            SelectedStepSelectionRequirement,
            SelectedStepTeachingSelection,
            IsSelectedStepDualRoiMeasurement && !IsSelectedStepCompletenessGridUsingEditableRegion,
            IsSelectedStepGapFlush,
            PlaneFlatnessReferenceSelection,
            PlaneFlatnessMeasurementSelection,
            DualRoiFirstLabel,
            DualRoiSecondLabel,
            IsTeachingSelectionCaptureActive,
            CanApplyTeachingSelectionCapture,
            Localization));
        RefreshTeachingResults();
        inspectionQueueWorkspace?.RefreshResultPresentation();
        viewerWorkspaceViewModel.RefreshInspectionDataView();
        OnPropertyChanged(nameof(ViewerInspectionInput));
        OnPropertyChanged(nameof(ViewerInspectionOutput));
        OnPropertyChanged(nameof(SelectedWorkspaceTitle));
        OnPropertyChanged(nameof(SelectedWorkspaceState));
        RefreshHeightImageRoiProjection();
        fitWorkspaceRegionCommand?.RaiseCanExecuteChanged();
        showWorkspaceOutputCommand?.RaiseCanExecuteChanged();
        pinWorkspaceOutputCommand?.RaiseCanExecuteChanged();
        compareWorkspaceOutputCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshTeachingResults()
    {
        var rows = new List<TeachingMeasurementResultItem>();
        if (orderedRunResult is { } run)
        {
            foreach (var executed in run.Steps)
            {
                var step = PipelineSteps.FirstOrDefault(item => item.Id == executed.StepId);
                if (step is null) continue;
                var metric = SelectPrimaryMeasurementMetric(step.ToolId, executed.Result.Metrics);
                var value = metric is { } number && double.IsFinite(number.Value)
                    ? $"{number.Name}: {number.Value.ToString("R", CultureInfo.InvariantCulture)}" : "—";
                var parameters = run.ReboundDocument.Steps.First(item => item.Id == executed.StepId).Parameters;
                var current = !IsOrderedRunRunning && !HasPendingStepParameterChanges && !IsSelectedStepPreviewRunning;
                rows.Add(new(step, current ? MeasurementResultStatusLabel(executed.Result)
                    : Localize("이전 결과 · 다시 실행 필요", "Previous result · Run again"), value, metric?.Unit ?? "raw-height",
                    MeasurementLimitSummary(step.ToolId, executed.Result, parameters), current ? MeasurementResultMessage(executed.Result)
                        : Localize("이전 입력 또는 조건의 결과", "Result from previous input or conditions"), current, executed.Result.Metrics) { ExecutionStatus = executed.Result.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage ? null : executed.Result.Status, RecordedAt = orderedRunExecutionOwner.Record?.RecordedAtUtc });
            }
        }
        foreach (var step in PipelineSteps.Where(step => !rows.Any(row => ReferenceEquals(row.Step, step))))
        {
            if (!heightMeasurementExecutionOwner.TryGetCompletedResult(step, out var output, out var current)
                || output is null) continue;
            var metric = SelectPrimaryMeasurementMetric(step.ToolId, output.Result.Metrics);
            var status = current ? MeasurementResultStatusLabel(output.Result)
                : Localize("이전 결과 · 다시 실행 필요", "Previous result · Run again");
            rows.Add(new(step, status, metric is { } value && double.IsFinite(value.Value)
                ? $"{value.Name}: {value.Value.ToString("R", CultureInfo.InvariantCulture)}" : "—",
                metric?.Unit ?? output.Unit, MeasurementLimitSummary(step.ToolId, output.Result, heightMeasurementExecutionOwner.GetCompletedParameters(step)),
                current ? MeasurementResultMessage(output.Result) : Localize("이전 입력 또는 조건의 결과", "Result from previous input or conditions"),
                current, output.Result.Metrics) { ExecutionStatus = output.Result.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage ? null : output.Result.Status });
        }
        foreach (var step in PipelineSteps.Where(step => !rows.Any(row => ReferenceEquals(row.Step, step))))
        {
            if (selectedStepExecutionOwner is null || !selectedStepExecutionOwner.TryGetCompletedResult(step, out var output, out var current)) continue;
            output = LocalizeCalculatedOutputEvidence(step, output);
            current &= !ReferenceEquals(step, SelectedPipelineStep) || !HasPendingStepParameterChanges;
            rows.Add(new(step, current ? output.ResultStatus : Localize("이전 결과 · 다시 실행 필요", "Previous result · Run again"),
                output.Value, output.Unit, output.LimitSummary,
                current ? output.Reason : Localize("이전 입력 또는 조건의 결과", "Result from previous input or conditions"), current, []) { OutputLabel = output.ValueLabel });
        }
        rows.Sort((first, second) => PipelineSteps.IndexOf(first.Step).CompareTo(PipelineSteps.IndexOf(second.Step)));
        foreach (var step in PipelineSteps)
        {
            var row = rows.FirstOrDefault(item => ReferenceEquals(item.Step, step));
            var metric = row is null ? null : SelectPrimaryMeasurementMetric(step.ToolId, row.Metrics);
            var failed = metric?.Status is ResultStatus.Fail or ResultStatus.Error || row?.Status == Localization.ResultStatusLabel(ResultStatus.Fail) || row?.Status == Localization.ResultStatusLabel(ResultStatus.Error);
            var value = metric is not null && double.IsFinite(metric.Value)
                ? metric.Value.ToString(failed ? "R" : "G6", CultureInfo.InvariantCulture) : row?.Value ?? string.Empty;
            step.UpdateResultPresentation(row is null ? string.Empty : $"{value} {row.Unit}".Trim(), row?.Status ?? string.Empty, row?.IsCurrent == true, failed);
        }
        foreach (var row in rows.Where(row => row.IsCurrent && row.Metrics.Count > 0))
        {
            if (teachingResultComparisons.TryGetValue(row.Step.Id, out var previous) && previous.SourceIdentity == TeachingResultSourceIdentity && ReferenceEquals(previous.Metrics, row.Metrics)) continue;
            var oldValue = teachingResultComparisons.TryGetValue(row.Step.Id, out previous) && previous.SourceIdentity == TeachingResultSourceIdentity && previous.Unit == row.Unit ? previous.Value : null;
            teachingResultComparisons[row.Step.Id] = (row.Metrics, TeachingResultSourceIdentity, row.Value, row.Unit, row.Status, oldValue);
        }
        OnPropertyChanged(nameof(HasSelectedToolResultComparison));
        OnPropertyChanged(nameof(SelectedToolResultComparisonText));
        TeachingResults.ReplaceAll(rows.Select(row => row with
        {
            Sequence = PipelineSteps.IndexOf(row.Step) + 1,
            IsSelectedTool = ReferenceEquals(row.Step, SelectedPipelineStep),
            DisplayedOutput = DisplayedOutputs.FirstOrDefault(output => string.Equals(output.Id, row.Step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        }));
        OnPropertyChanged(nameof(HasTeachingResults));
        OnPropertyChanged(nameof(TeachingResultSummary));
        OnPropertyChanged(nameof(SelectedTeachingResult));
    }

    internal static Metric? SelectPrimaryMeasurementMetric(string toolId, IReadOnlyList<Metric> metrics)
    {
        var name = toolId switch
        {
            "thickness" => "Mean",
            "plane-flatness" => "Flatness",
            "warpage" => "PeakToValley",
            "datum-plane-raw-height-deviation" => "PeakToValleyRawHeight",
            "gap-flush" => "Signed gap",
            "cross-section-dimensions" => "Section width",
            "point-pair-dimensions" => "3D distance",
            "volume" => "Signed net volume",
            "completeness-grid" => metrics.Any(metric => metric.Name == "Passed cells") ? "Passed cells" : "Cell count",
            "connected-region-gate" => "Region finite coverage",
            _ => null
        };
        return metrics.FirstOrDefault(metric => metric.Name == name)
            ?? metrics.FirstOrDefault(metric => metric.Kind != MetricKind.Count && metric.Status is not null)
            ?? metrics.FirstOrDefault(metric => metric.Kind != MetricKind.Count)
            ?? metrics.FirstOrDefault();
    }

    internal static string MeasurementResultStatusLabel(ToolResult result) => result.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage
        ? ThreeDLocalization.Shared.Resolve("ThreeD.Workspace.Unjudged", "미판정", "Unjudged") : ThreeDLocalization.Shared.ResultStatusLabel(result.Status);

    internal static string MeasurementResultMessage(ToolResult result) => result.Message switch
    {
        ToolRecipeStep.UnconfiguredAcceptanceMessage => ThreeDLocalization.Shared.Resolve("ThreeD.Workspace.UnconfiguredLimitsResult", "규격 미설정 · 측정값만 확인할 수 있습니다.", "Limits not configured · measurement values only."),
        "All measured H-axis separations from the fitted reference surface are within limits." => ThreeDLocalization.Shared.Resolve("ThreeD.Workspace.ThicknessWithinLimits", "기준면과의 높이 차이가 모두 규격 안에 있습니다.", result.Message),
        "One or more measured H-axis separations from the reference surface exceed the limits." or
        "One or more measured H-axis separations from the fitted reference surface exceed the limits." => ThreeDLocalization.Shared.Resolve("ThreeD.Workspace.ThicknessOutsideLimits", "기준면과의 높이 차이 중 규격을 벗어난 값이 있습니다.", result.Message),
        "Datum-plane raw-height residuals are within the local limit. Source C3D is unchanged." => ThreeDLocalization.Shared.Resolve("ThreeD.Workspace.DatumWithinLimits", "기준면 높이 편차가 규격 안에 있습니다.", "Datum-plane height deviation is within limits."),
        "Datum-plane raw-height residuals exceed the local limit. Source C3D is unchanged." => ThreeDLocalization.Shared.Resolve("ThreeD.Workspace.DatumOutsideLimits", "기준면 높이 편차가 규격을 벗어났습니다.", "Datum-plane height deviation exceeds the limit."),
        _ => result.Message
    };

    internal static string MeasurementLimitSummary(string toolId, ToolResult result, IReadOnlyList<ToolRecipeParameter> parameters)
    {
        if (result.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage)
            return ThreeDLocalization.Shared.Resolve("ThreeD.Workspace.UnconfiguredLimits", "규격 미설정", "Limits not configured");
        var metrics = result.Metrics;
        var lower = metrics.FirstOrDefault(metric => metric.Name == "LowerLimit" && double.IsFinite(metric.Value));
        var upper = metrics.FirstOrDefault(metric => metric.Name == "UpperLimit" && double.IsFinite(metric.Value));
        if (lower is not null && upper is not null && lower.Unit == upper.Unit)
            return $"{lower.Value.ToString("R", CultureInfo.InvariantCulture)}–{upper.Value.ToString("R", CultureInfo.InvariantCulture)} {lower.Unit}";
        var limits = metrics.Where(metric => metric.Kind != MetricKind.Count && double.IsFinite(metric.Value)
            && (metric.Name.Contains("tolerance", StringComparison.OrdinalIgnoreCase)
                || metric.Name.StartsWith("Expected ", StringComparison.Ordinal)
                || metric.Name is "MaximumPeakToValley" or "MaximumRms" or "MaximumPeakToValleyRawHeight"));
        var summary = string.Join("; ", limits.Select(metric => $"{metric.Name}: {metric.Value.ToString("R", CultureInfo.InvariantCulture)} {metric.Unit}"));
        if (!string.IsNullOrEmpty(summary)) return summary;
        string[] names = toolId switch
        {
            "thickness" => ["MinimumThickness", "MaximumThickness"],
            "warpage" => ["MaximumPeakToValley", "MaximumRms"],
            "plane-flatness" => ["MaximumFlatness"],
            "datum-plane-raw-height-deviation" => ["MaximumPeakToValleyRawHeight"],
            "point-pair-dimensions" => ["ExpectedDistance", "DistanceTolerance", "ExpectedPlanarWidth", "PlanarWidthTolerance", "ExpectedElevationAngleDegrees", "ElevationAngleToleranceDegrees"],
            "gap-flush" => ["ExpectedGap", "GapTolerance", "ExpectedFlush", "FlushTolerance"],
            "volume" => ["ExpectedNetVolume", "VolumeTolerance"],
            "cross-section-dimensions" => ["ExpectedWidth", "WidthTolerance", "ExpectedHeightRange", "HeightTolerance"],
            "completeness-grid" => ["MinimumFiniteCoverageRatio", "MinimumReferenceRelativeMeanRawHeight", "MaximumReferenceRelativeMeanRawHeight"],
            "connected-region-gate" => ["MinimumFiniteCoverageRatio", "MinimumMeanHeight", "MaximumMeanHeight"],
            _ => []
        };
        return string.Join("; ", parameters.Where(parameter => names.Contains(parameter.Name, StringComparer.Ordinal))
            .Select(parameter => $"{parameter.Name}: {parameter.Value}")) is { Length: > 0 } configured ? configured : "—";
    }

    private SelectedToolOutputEvidence CreateSelectedToolOutputEvidence()
    {
        if (SelectedPipelineStep is { } selectedStep && orderedRunResult is { } run
            && run.Steps.FirstOrDefault(item => item.StepId == selectedStep.Id) is { } executed)
        {
            var metric = SelectPrimaryMeasurementMetric(selectedStep.ToolId, executed.Result.Metrics);
            return new SelectedToolOutputEvidence(metric?.Name ?? Localization.SelectedToolOutputValueLabel,
                metric is { } value && double.IsFinite(value.Value) ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "—",
                metric?.Unit ?? string.Empty, !HasPendingStepParameterChanges && !IsSelectedStepPreviewRunning
                    ? MeasurementResultStatusLabel(executed.Result) : Localize("이전 결과 · 다시 실행 필요", "Previous result · Run again"),
                MeasurementLimitSummary(selectedStep.ToolId, executed.Result, run.ReboundDocument.Steps.First(item => item.Id == selectedStep.Id).Parameters),
                MeasurementResultMessage(executed.Result));
        }
        if (SelectedPipelineStep is { } step && selectedStepExecutionOwner is not null
            && selectedStepExecutionOwner.TryGetCompletedResult(step, out var output, out var current))
        {
            output = LocalizeCalculatedOutputEvidence(step, output);
            return current && !HasPendingStepParameterChanges ? output : output with
            {
                ResultStatus = Localize("이전 결과 · 다시 실행 필요", "Previous result · Run again"),
                Reason = Localize("이전 입력 또는 조건의 결과", "Result from previous input or conditions")
            };
        }
        if (SelectedPipelineStep is { } selected && !IsSelectedStepMeasurement
            && !ArtifactRegistry.Any(item => string.Equals(item.Id, selected.OutputEntityId, StringComparison.OrdinalIgnoreCase) && item.HasContentHash))
            return SelectedToolOutputEvidence.Empty;
        var captured = CaptureSelectedToolOutputEvidence();
        return SelectedPipelineStep is { } selectedStepForOutput ? LocalizeCalculatedOutputEvidence(selectedStepForOutput, captured) : captured;
    }

    private SelectedToolOutputEvidence LocalizeCalculatedOutputEvidence(ToolWorkbenchPipelineStepItem step, SelectedToolOutputEvidence output)
    {
        if (step.ToolId == "datum-plane-raw-height-deviation" && Enum.TryParse<ResultStatus>(output.ResultStatus, out var status))
        {
            var result = new ToolResult(step.ToolName, status, output.Reason, TimeSpan.Zero, [], []);
            return output with { ResultStatus = MeasurementResultStatusLabel(result), Reason = MeasurementResultMessage(result),
                LimitSummary = result.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage ? MeasurementLimitSummary(step.ToolId, result, []) : output.LimitSummary };
        }
        if (output.ResultStatus != "Calculated") return output;
        return output with
        {
            ValueLabel = step.ToolId == "height-difference-edge" ? Localize("검출 포인트", "Detected points")
                : step.ToolId == ToolRecipeNinePointSurfaceCompensationExecution.ToolId ? Localize("9점 기준면 편차(P–V)", "9-point plane deviation (P–V)")
                : step.ToolId == "filter" ? Localization.SelectedToolOutputCellsLabel : Localization.SelectedToolOutputValueLabel,
            ResultStatus = Localize("계산됨", "Calculated"),
            Reason = step.ToolId == "height-difference-edge" ? Localize("에지 포인트를 계산했습니다.", "Edge points calculated.")
                : step.ToolId == "filter" ? output.Reason : Localize("출력을 계산했습니다.", "Output calculated.")
        };
    }

    private SelectedToolOutputEvidence CaptureSelectedToolOutputEvidence()
    {
        if (IsSelectedStepNinePointCompensation && ninePointExecutionOwner.Current is { Numerical: { } ninePoint } corrected)
            return new SelectedToolOutputEvidence("P–V", ninePoint.ReferencePeakToValley.ToString("R", CultureInfo.InvariantCulture),
                corrected.Output!.Unit, "Calculated", HasExecutionResult: true)
            { ExecutionStatus = corrected.Result.Status };
        if (IsSelectedStepFilter && filterPreviewOutput is { } filter)
            return new SelectedToolOutputEvidence(Localization.SelectedToolOutputCellsLabel,
                filter.ValidCount.ToString(CultureInfo.InvariantCulture), "count", "Calculated",
                HasExecutionResult: !IsFilterPreviewRunning);
        if (SelectedPipelineStep is { ToolId: "height-difference-edge" } edgeStep)
        {
            var edge = string.Equals(CurrentHeightDifferenceEdgeOutput?.OutputEntityId, edgeStep.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                ? CurrentHeightDifferenceEdgeOutput
                : TryGetPublishedHeightDifferenceEdgeOutput(edgeStep.OutputEntityId, out var publishedEdge) ? publishedEdge : null;
            if (edge is not null)
            {
                var stale = ReferenceEquals(edge, CurrentHeightDifferenceEdgeOutput) && IsEdgePreviewStale;
                return new SelectedToolOutputEvidence(Localize("검출 포인트", "Detected points"), edge.Points.Count.ToString(CultureInfo.InvariantCulture), "count",
                    stale ? Localize("이전 결과 · 다시 실행 필요", "Previous result · Run again") : "Calculated",
                    Reason: stale ? Localize("이전 입력 또는 조건의 결과", "Result from previous input or conditions") : Localize("에지 포인트를 계산했습니다.", "Edge points calculated."),
                    HasExecutionResult: !IsEdgePreviewRunning);
            }
        }
        if (IsSelectedStepDatumPlaneDeviation && CurrentDatumPlaneDeviationOutput is { } datum && CurrentDatumPlaneDeviationResult is { } datumResult)
            return new SelectedToolOutputEvidence("P2V", datum.PeakToValleyRawHeight.ToString("R", CultureInfo.InvariantCulture), datum.Unit,
                datumResult.Status.ToString(), $"≤ {datum.MaximumPeakToValleyRawHeight.ToString("R", CultureInfo.InvariantCulture)} {datum.Unit}", datumResult.Message,
                HasExecutionResult: !IsDatumPlaneDeviationPreviewRunning)
            { ExecutionStatus = datumResult.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage ? null : datumResult.Status };
        if (IsSelectedStepSignedDeltaBackgroundSubtraction
            && CurrentSignedDeltaPreviewOutput is { } signedDelta)
        {
            var status = IsSignedDeltaPreviewPublished
                ? Localization.RecipeHealthPublished
                : IsSignedDeltaPreviewRunning
                    ? Localization.SelectedToolOutputPreviewRunningStatus
                    : Localization.Preview;
            var value = string.Create(
                CultureInfo.CurrentCulture,
                $"paired={signedDelta.PairedValidSampleCount:N0}; missing={signedDelta.MissingEitherSampleCount:N0}; zero={signedDelta.ZeroDeltaSampleCount:N0}; +={signedDelta.PositiveDeltaSampleCount:N0}; -={signedDelta.NegativeDeltaSampleCount:N0}");
            return new SelectedToolOutputEvidence(
                Localization.SelectedToolSignedDeltaLabel,
                value,
                "count",
                $"{status} · {(GetRenderableSignedDeltaArtifact(signedDelta.OutputEntityId) is not null ? Localization.DisplayableHeightImageData : Localization.NoCurrentDisplayableOutput)}",
                HasExecutionResult: !IsSignedDeltaPreviewRunning);
        }

        if (IsSelectedStepPhysicalEdgeGapFlush)
        {
            var output = CurrentPhysicalEdgeGapFlushOutput;
            var state = output?.MeasurementState.ToString()
                ?? Localization.Resolve(
                    "ThreeD.Workbench.PhysicalEdgeGapFlushNotExecuted",
                    "미실행",
                    "Not executed");
            var value = output is null
                ? string.Empty
                : $"gap {output.SignedGap.ToString("R", CultureInfo.InvariantCulture)} / flush {output.SignedFlush.ToString("R", CultureInfo.InvariantCulture)}";
            var status = output is null
                ? PhysicalEdgeGapFlushExecutionSummary
                : output.EvidenceSummary;
            return new SelectedToolOutputEvidence(
                Localization.Resolve(
                    "ThreeD.Workbench.PhysicalEdgeGapFlushValidity",
                    "측정 유효성",
                    "Measurement validity"),
                state,
                output?.Unit ?? string.Empty,
                string.IsNullOrWhiteSpace(value) ? status : $"{value} | {status}",
                HasExecutionResult: output is not null && !IsPhysicalEdgeGapFlushPreviewRunning);
        }

        if (IsSelectedStepRemoveOutlierPixels
            && CurrentRemoveOutlierPreviewOutput is not null
            && CurrentRemoveOutlierMask is { } outlierMask)
        {
            return new SelectedToolOutputEvidence(
                Localization.SelectedToolRemovedOutliersLabel,
                outlierMask.OutlierCellCount.ToString(
                    CultureInfo.InvariantCulture),
                "count",
                IsRemoveOutlierPreviewRunning
                    ? Localization.SelectedToolOutputPreviewRunningStatus
                    : IsRemoveOutlierPreviewPublished
                        ? Localization.RecipeHealthPublished
                        : Localization.Preview,
                HasExecutionResult: !IsRemoveOutlierPreviewRunning);
        }

        if (IsSelectedStepHeightThresholdBackgroundRemoval
            && CurrentHeightThresholdPreviewEvidence is { } thresholdEvidence)
        {
            return new SelectedToolOutputEvidence(
                "Retained foreground samples",
                thresholdEvidence.RetainedValidSampleCount.ToString(CultureInfo.InvariantCulture),
                "count",
                IsHeightThresholdPreviewRunning
                    ? Localization.SelectedToolOutputPreviewRunningStatus
                    : IsHeightThresholdPreviewPublished
                        ? Localization.RecipeHealthPublished
                        : Localization.Preview,
                HasExecutionResult: !IsHeightThresholdPreviewRunning);
        }

        if (IsSelectedStepLevelSurface
            && CurrentLevelSurfaceTransform is { } transform)
        {
            return new SelectedToolOutputEvidence(
                Localization.SelectedToolReferenceRmsLabel,
                transform.ReferenceResidualRms.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                transform.SourceUnit,
                IsLevelSurfacePreviewPublished
                    ? Localization.RecipeHealthPublished
                    : IsLevelSurfacePreviewRunning
                        ? Localization.SelectedToolOutputPreviewRunningStatus
                        : Localization.Preview,
                HasExecutionResult: !IsLevelSurfacePreviewRunning);
        }

        if (IsSelectedStepDomainMask
            && CurrentDomainMaskPreviewOutput is { } domainMaskOutput)
        {
            return new SelectedToolOutputEvidence(
                Localization.SelectedToolOutputMissingCellsLabel,
                domainMaskOutput.MissingCount.ToString(CultureInfo.InvariantCulture),
                "count",
                IsDomainMaskPreviewPublished
                    ? Localization.RecipeHealthPublished
                    : IsDomainMaskPreviewRunning
                        ? Localization.SelectedToolOutputPreviewRunningStatus
                        : Localization.Preview,
                HasExecutionResult: !IsDomainMaskPreviewRunning);
        }

        if (IsSelectedStepRoiCrop
            && CurrentRoiCropPreviewOutput is { } crop)
        {
            return new SelectedToolOutputEvidence(
                Localization.SelectedToolOutputCellsLabel,
                (crop.Width * crop.Height).ToString(CultureInfo.InvariantCulture),
                "count",
                IsRoiCropPreviewPublished
                    ? Localization.RecipeHealthPublished
                    : IsRoiCropPreviewRunning
                        ? Localization.SelectedToolOutputPreviewRunningStatus
                        : Localization.Preview,
                HasExecutionResult: !IsRoiCropPreviewRunning);
        }

        if (SelectedPipelineStep is not { } step
            || CurrentMeasurementOutput is not { } measurementOutput
            || !string.Equals(
                measurementOutput.OutputEntityId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            var artifact = SelectedPipelineStep is { } selected
                ? ArtifactRegistry.FirstOrDefault(item => string.Equals(item.Id, selected.OutputEntityId, StringComparison.OrdinalIgnoreCase) && item.HasContentHash)
                : null;
            return artifact is null ? SelectedToolOutputEvidence.Empty : new SelectedToolOutputEvidence(
                Localization.SelectedToolOutputValueLabel, "—", string.Empty,
                artifact.State == "Stale" ? Localize("이전 결과 · 다시 실행 필요", "Previous result · Run again") : "Calculated",
                Reason: artifact.State == "Stale" ? Localize("이전 입력 또는 조건의 결과", "Result from previous input or conditions") : Localize("출력을 계산했습니다.", "Output calculated."));
        }

        var primaryMetric = SelectPrimaryMeasurementMetric(step.ToolId, measurementOutput.Result.Metrics);
        var measurementStatus = HasCurrentMeasurementPreview
            ? MeasurementResultStatusLabel(measurementOutput.Result)
            : Localize("이전 결과 · 다시 실행 필요", "Previous result · Run again");
        var limitSummary = MeasurementLimitSummary(step.ToolId, measurementOutput.Result, heightMeasurementExecutionOwner.GetCompletedParameters(step));
        var reason = HasCurrentMeasurementPreview
            ? MeasurementResultMessage(measurementOutput.Result)
            : Localize("이전 입력 또는 조건의 결과", "Result from previous input or conditions");
        var outputEvidence = primaryMetric is null
            ? new SelectedToolOutputEvidence(
                Localization.SelectedToolOutputValueLabel,
                "\u2014",
                measurementOutput.Unit,
                measurementStatus,
                limitSummary,
                reason)
            : new SelectedToolOutputEvidence(
                primaryMetric.Name,
                primaryMetric.Value.ToString("R", CultureInfo.InvariantCulture),
                primaryMetric.Unit,
                measurementStatus,
                limitSummary,
                reason);
        return outputEvidence with { ExecutionStatus = HasCurrentMeasurementPreview && measurementOutput.Result.Message != ToolRecipeStep.UnconfiguredAcceptanceMessage ? measurementOutput.Result.Status : null };
    }

    private ToolWorkbenchDisplayedOutputItem? ResolveDisplayedOutput(SelectedToolOutputItem? output) =>
        output is null
            ? null
            : DisplayedOutputs.FirstOrDefault(item =>
                string.Equals(item.Id, output.EntityId, StringComparison.OrdinalIgnoreCase));

    private void ShowWorkspaceOutput(SelectedToolOutputItem? output)
    {
        if (ResolveDisplayedOutput(output) is not { CanShowInViewer: true } displayed)
        {
            return;
        }

        RequestDisplayedOutputInViewer(displayed);
    }

    private void PinWorkspaceOutput(SelectedToolOutputItem? output)
    {
        if (ResolveDisplayedOutput(output) is not { CanPinToCompare: true } displayed)
        {
            return;
        }

        WorkspaceSelection.SelectOutput(displayed.Id);
        PinDisplayedOutputToCompare(displayed);
    }

    private void CompareWorkspaceOutput(SelectedToolOutputItem? output)
    {
        var displayed = ResolveDisplayedOutput(output);
        if (displayed is null)
        {
            return;
        }

        if (TryOpenPreparationQualityComparison(displayed))
        {
            return;
        }

        WorkspaceSelection.SelectOutput(displayed.Id);
        if (!displayed.IsPinnedToCompare)
        {
            PinDisplayedOutputToCompare(displayed);
        }

        if (displayed.IsPinnedToCompare)
        {
            if (viewerWorkspaceViewModel.ShowOutputCompareViewerCommand.CanExecute(null))
            {
                viewerWorkspaceViewModel.ShowOutputCompareViewerCommand.Execute(null);
            }
            else
            {
                OutputComparePaneRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private InspectionWorkspaceRegionRole GetInspectionWorkspaceRegionRole()
    {
        if (SelectedPipelineStep is null || SelectedStepSelectionRequirement is null)
        {
            return InspectionWorkspaceRegionRole.None;
        }

        if (!IsSelectedStepDualRoiMeasurement)
        {
            return InspectionWorkspaceRegionRole.Selection;
        }

        if (IsSelectedStepGapFlush)
        {
            return IsPlaneFlatnessMeasurementRoleActive
                ? InspectionWorkspaceRegionRole.Second
                : InspectionWorkspaceRegionRole.First;
        }

        return IsPlaneFlatnessMeasurementRoleActive
            ? InspectionWorkspaceRegionRole.Measurement
            : InspectionWorkspaceRegionRole.Reference;
    }
}
