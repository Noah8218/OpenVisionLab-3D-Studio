using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private RelayCommand previewSelectedStepCommand => filterExecutionOwner.PreviewSelectedStepCommand;
    private RelayCommand runTeachingRecipeCommand => filterExecutionOwner.RunTeachingRecipeCommand;
    private RelayCommand publishSelectedStepCommand => filterExecutionOwner.PublishSelectedStepCommand;
    private RelayCommand cancelFilterPreviewCommand => filterExecutionOwner.CancelFilterPreviewCommand;
    private RelayCommand showFilterSourceCommand => filterExecutionOwner.ShowFilterSourceCommand;
    private RelayCommand setFilterKernel3Command => filterExecutionOwner.SetFilterKernel3Command;
    private RelayCommand setFilterKernel5Command => filterExecutionOwner.SetFilterKernel5Command;
    private RelayCommand setFilterKernel7Command => filterExecutionOwner.SetFilterKernel7Command;
    private C3DHeightFieldSnapshot? filterPreviewOutput => filterExecutionOwner.FilterPreviewOutput;
    private string? filterPreviewPath => filterExecutionOwner.FilterPreviewPath;
    private bool isFilterPreviewRunning => filterExecutionOwner.IsFilterPreviewRunning;
    private bool isFilterPreviewStale => filterExecutionOwner.IsFilterPreviewStale;
    private bool isFilterPreviewPublished => filterExecutionOwner.IsFilterPreviewPublished;
    private string filterExecutionSummary => filterExecutionOwner.FilterExecutionSummary;
    private bool isOrderedRunRunning
    {
        get => filterExecutionOwner.IsOrderedRunRunning;
        set => filterExecutionOwner.IsOrderedRunRunning = value;
    }
    private ToolRecipeOrderedGraphExecutionResult? orderedRunResult
    {
        get => filterExecutionOwner.OrderedRunResult;
        set => filterExecutionOwner.OrderedRunResult = value;
    }
    private string? orderedRunRecordPath
    {
        get => filterExecutionOwner.OrderedRunRecordPath;
        set => filterExecutionOwner.OrderedRunRecordPath = value;
    }
    private string orderedRunSummary
    {
        get => filterExecutionOwner.OrderedRunSummary;
        set => filterExecutionOwner.OrderedRunSummary = value;
    }

    public event EventHandler<ToolWorkbenchFilterDisplayRequestEventArgs>? FilterDisplayRequested;
    public event EventHandler<ToolWorkbenchOrderedRunCompletedEventArgs>? OrderedRunCompleted;
    public event EventHandler? OrderedRunInvalidated;

    public ICommand PreviewSelectedStepCommand { get; private set; } = null!;
    public ICommand RunTeachingRecipeCommand { get; private set; } = null!;
    public ICommand PublishSelectedStepCommand { get; private set; } = null!;
    public ICommand CancelFilterPreviewCommand { get; private set; } = null!;
    public ICommand CancelSelectedPreviewCommand { get; private set; } = null!;
    public ICommand ShowFilterSourceCommand { get; private set; } = null!;
    public ICommand SetFilterKernel3Command { get; private set; } = null!;
    public ICommand SetFilterKernel5Command { get; private set; } = null!;
    public ICommand SetFilterKernel7Command { get; private set; } = null!;

    public bool IsSelectedStepFilter =>
        string.Equals(SelectedPipelineStep?.ToolId, "filter", StringComparison.Ordinal);

    public bool IsFilterPreviewRunning => isFilterPreviewRunning;
    public bool IsSelectedStepPreviewRunning =>
        IsSelectedStepSurfaceMatch ? IsSurfaceMatchExperimentRunning
            : IsSelectedStepFilter ? isFilterPreviewRunning
            : IsSelectedStepRemoveOutlierPixels ? IsRemoveOutlierPreviewRunning
            : IsSelectedStepLevelSurface ? IsLevelSurfacePreviewRunning
            : IsSelectedStepRoiCrop ? IsRoiCropPreviewRunning
            : IsSelectedStepHeightDifferenceEdge ? IsEdgePreviewRunning
            : IsSelectedStepTwoPointLine ? IsTwoPointLinePreviewRunning
            : IsSelectedStepThreePointPlane ? IsThreePointPlanePreviewRunning
            : IsSelectedStepDatumPlaneDeviation ? IsDatumPlaneDeviationPreviewRunning
            : IsSelectedStepLineFit ? IsLineFitPreviewRunning
            : IsSelectedStepLineIntersection ? IsLineIntersectionPreviewRunning
            : IsSelectedStepLandmarkCorrespondence ? IsLandmarkCorrespondencePreviewRunning
            : IsSelectedStepXYZAffineSolve ? IsAffineSolvePreviewRunning
            : IsSelectedStepXYZAffineApply ? IsAffineApplyPreviewRunning
            : IsSelectedStepRegridHeightField ? IsRegridHeightFieldPreviewRunning
            : IsSelectedStepMeasurement && IsMeasurementPreviewRunning;
    public bool HasCurrentFilterPreview => filterPreviewOutput is not null && !isFilterPreviewStale;
    public bool IsFilterPreviewStale => isFilterPreviewStale;
    public bool IsFilterPreviewPublished => isFilterPreviewPublished;
    public string? CurrentFilterPreviewPath => filterPreviewPath;
    public string CurrentFilterPreviewOutputSummary => filterPreviewOutput is null
        ? "No Filter Preview output."
        : $"Filtered Surface | SHA-256 {filterPreviewOutput.ContentSha256[..12]} | {(isFilterPreviewPublished ? "Published" : "Preview only")}";
    public string FilterExecutionSummary => filterExecutionSummary;
    public string FilterKernelSummary
    {
        get
        {
            var size = GetFilterParameter("KernelSize") ?? "-";
            return $"{size} x {size}";
        }
    }
    public string FilterOutputHashSummary => filterPreviewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {filterPreviewOutput.ContentSha256}";
    public bool IsOrderedRunRunning => isOrderedRunRunning;
    public bool HasOrderedRunResult => orderedRunResult is not null;
    public ToolRecipeOrderedGraphExecutionResult? CurrentOrderedRunResult =>
        orderedRunResult;
    public string? CurrentOrderedRunRecordPath => orderedRunRecordPath;
    public string OrderedRunStatus => isOrderedRunRunning
        ? Localize("실행 중", "Running")
        : orderedRunResult is null
            ? CanRunTeachingRecipe()
                ? Localize("준비", "Ready")
                : Localize("실행 불가", "Blocked")
            : LocalizeStatus(orderedRunResult.Status);
    public string OrderedRunCapabilitySummary
    {
        get
        {
            if (isOrderedRunRunning || orderedRunResult is not null)
            {
                return orderedRunSummary;
            }

            TryGetOrderedRunCapability(out var message);
            return message;
        }
    }
    public string OrderedRunEvidenceSummary => orderedRunRecordPath is null
        ? Localize(
            "명시적 실행 전에는 Run Record가 생성되지 않습니다.",
            "No Run Record is created before explicit Run.")
        : Localize(
            $"Run Record 저장됨 · {Path.GetFileName(Path.GetDirectoryName(orderedRunRecordPath))}",
            $"Run Record saved · {Path.GetFileName(Path.GetDirectoryName(orderedRunRecordPath))}");

    private void InitializeFilterExecution()
    {
        PreviewSelectedStepCommand = previewSelectedStepCommand;
        RunTeachingRecipeCommand = runTeachingRecipeCommand;
        PublishSelectedStepCommand = publishSelectedStepCommand;
        CancelFilterPreviewCommand = cancelFilterPreviewCommand;
        CancelSelectedPreviewCommand = cancelFilterPreviewCommand;
        ShowFilterSourceCommand = showFilterSourceCommand;
        SetFilterKernel3Command = setFilterKernel3Command;
        SetFilterKernel5Command = setFilterKernel5Command;
        SetFilterKernel7Command = setFilterKernel7Command;
    }

    private bool CanShowFilterSource() =>
        filterExecutionOwner.FilterPreviewOutput is not null && File.Exists(Source.Path);

    private Task<bool> PreviewSelectedStepAsync() => IsSelectedStepSurfaceMatch
        ? PreviewSelectedSurfaceMatchExperimentAsync()
        : IsSelectedStepMeasurement ? PreviewSelectedMeasurementAsync()
        : IsSelectedStepRemoveOutlierPixels
        ? PreviewSelectedRemoveOutlierPixelsAsync()
        : IsSelectedStepLevelSurface
        ? PreviewSelectedLevelSurfaceAsync()
        : IsSelectedStepRoiCrop
        ? PreviewSelectedRoiCropAsync()
        : IsSelectedStepRegridHeightField
        ? PreviewSelectedRegridHeightFieldAsync()
        : IsSelectedStepXYZAffineApply
        ? PreviewSelectedXYZAffineApplyAsync()
        : IsSelectedStepXYZAffineSolve
        ? PreviewSelectedXYZAffineSolveAsync()
        : IsSelectedStepDatumPlaneDeviation
        ? PreviewSelectedDatumPlaneDeviationAsync()
        : IsSelectedStepLandmarkCorrespondence
        ? PreviewSelectedLandmarkCorrespondenceAsync()
        : IsSelectedStepLineIntersection
        ? PreviewSelectedLineIntersectionAsync()
        : IsSelectedStepTwoPointLine ? PreviewSelectedTwoPointLineAsync()
        : IsSelectedStepThreePointPlane ? PreviewSelectedThreePointPlaneAsync()
        : IsSelectedStepLineFit ? PreviewSelectedLineFitAsync()
        : IsSelectedStepHeightDifferenceEdge ? PreviewSelectedHeightDifferenceEdgeAsync() : PreviewSelectedFilterAsync();

    private bool CanPreviewSelectedStep() => IsSelectedStepSurfaceMatch
        ? CanPreviewSelectedSurfaceMatchExperiment()
        : IsSelectedStepMeasurement ? CanPreviewSelectedMeasurement()
        : IsSelectedStepRemoveOutlierPixels
        ? CanPreviewSelectedRemoveOutlierPixels()
        : IsSelectedStepLevelSurface
        ? CanPreviewSelectedLevelSurface()
        : IsSelectedStepRoiCrop
        ? CanPreviewSelectedRoiCrop()
        : IsSelectedStepRegridHeightField
        ? CanPreviewSelectedRegridHeightField()
        : IsSelectedStepXYZAffineApply
        ? CanPreviewSelectedXYZAffineApply()
        : IsSelectedStepXYZAffineSolve
        ? CanPreviewSelectedXYZAffineSolve()
        : IsSelectedStepDatumPlaneDeviation
        ? CanPreviewSelectedDatumPlaneDeviation()
        : IsSelectedStepLandmarkCorrespondence
        ? CanPreviewSelectedLandmarkCorrespondence()
        : IsSelectedStepLineIntersection
        ? CanPreviewSelectedLineIntersection()
        : IsSelectedStepTwoPointLine ? CanPreviewSelectedTwoPointLine()
        : IsSelectedStepThreePointPlane ? CanPreviewSelectedThreePointPlane()
        : IsSelectedStepLineFit ? CanPreviewSelectedLineFit()
        : IsSelectedStepHeightDifferenceEdge ? CanPreviewSelectedHeightDifferenceEdge() : CanPreviewSelectedFilter();

    private void PublishSelectedStep()
    {
        if (IsSelectedStepSurfaceMatch)
        {
            PublishSelectedSurfaceMatchExperiment();
        }
        else if (IsSelectedStepMeasurement)
        {
            PublishSelectedMeasurement();
        }
        else if (IsSelectedStepRemoveOutlierPixels)
        {
            PublishSelectedRemoveOutlierPixels();
        }
        else if (IsSelectedStepLevelSurface)
        {
            PublishSelectedLevelSurface();
        }
        else if (IsSelectedStepRoiCrop)
        {
            PublishSelectedRoiCrop();
        }
        else if (IsSelectedStepRegridHeightField)
        {
            PublishSelectedRegridHeightField();
        }
        else if (IsSelectedStepXYZAffineApply)
        {
            PublishSelectedXYZAffineApply();
        }
        else if (IsSelectedStepXYZAffineSolve)
        {
            PublishSelectedXYZAffineSolve();
        }
        else if (IsSelectedStepDatumPlaneDeviation)
        {
            PublishSelectedDatumPlaneDeviation();
        }
        else if (IsSelectedStepLandmarkCorrespondence)
        {
            PublishSelectedLandmarkCorrespondence();
        }
        else if (IsSelectedStepLineIntersection)
        {
            PublishSelectedLineIntersection();
        }
        else if (IsSelectedStepTwoPointLine)
        {
            PublishSelectedTwoPointLine();
        }
        else if (IsSelectedStepThreePointPlane)
        {
            PublishSelectedThreePointPlane();
        }
        else if (IsSelectedStepLineFit)
        {
            PublishSelectedLineFit();
        }
        else if (IsSelectedStepHeightDifferenceEdge)
        {
            PublishSelectedHeightDifferenceEdge();
        }
        else
        {
            PublishSelectedFilter();
        }
    }

    private bool CanPublishSelectedStep() => IsSelectedStepSurfaceMatch
        ? CanPublishSelectedSurfaceMatchExperiment()
        : IsSelectedStepMeasurement ? HasCurrentMeasurementPreview && !IsMeasurementPreviewPublished
        : IsSelectedStepRemoveOutlierPixels
        ? HasCurrentRemoveOutlierPreview && !IsRemoveOutlierPreviewPublished
        : IsSelectedStepLevelSurface
        ? HasCurrentLevelSurfacePreview && !IsLevelSurfacePreviewPublished
        : IsSelectedStepRoiCrop
        ? HasCurrentRoiCropPreview && !IsRoiCropPreviewPublished
        : IsSelectedStepRegridHeightField
        ? CanPublishRegridHeightFieldPreview
        : IsSelectedStepXYZAffineApply
        ? HasCurrentAffineApplyPreview && !IsAffineApplyPreviewPublished
        : IsSelectedStepXYZAffineSolve
        ? HasCurrentAffineSolvePreview && !IsAffineSolvePreviewPublished
        : IsSelectedStepDatumPlaneDeviation
        ? HasCurrentDatumPlaneDeviationPreview && !IsDatumPlaneDeviationPreviewPublished
        : IsSelectedStepLandmarkCorrespondence
        ? HasCurrentLandmarkCorrespondencePreview && !IsLandmarkCorrespondencePreviewPublished
        : IsSelectedStepLineIntersection
        ? HasCurrentLineIntersectionPreview && !IsLineIntersectionPreviewPublished
        : IsSelectedStepTwoPointLine
        ? HasCurrentTwoPointLinePreview && !IsTwoPointLinePreviewPublished
        : IsSelectedStepThreePointPlane
        ? HasCurrentThreePointPlanePreview && !IsThreePointPlanePreviewPublished
        : IsSelectedStepLineFit
        ? HasCurrentLineFitPreview && !IsLineFitPreviewPublished
        : IsSelectedStepHeightDifferenceEdge ? HasCurrentEdgePreview && !IsEdgePreviewPublished : IsSelectedStepFilter && HasCurrentFilterPreview && !isFilterPreviewPublished;

    private void CancelSelectedPreview()
    {
        if (IsSelectedStepSurfaceMatch)
        {
            CancelSurfaceMatchExperimentPreview();
        }
        else if (IsSelectedStepMeasurement)
        {
            CancelMeasurementPreview();
        }
        else if (IsSelectedStepRemoveOutlierPixels)
        {
            CancelRemoveOutlierPreview();
        }
        else if (IsSelectedStepLevelSurface)
        {
            CancelLevelSurfacePreview();
        }
        else if (IsSelectedStepRoiCrop)
        {
            CancelRoiCropPreview();
        }
        else if (IsSelectedStepRegridHeightField)
        {
            CancelRegridHeightFieldPreview();
        }
        else if (IsSelectedStepXYZAffineApply)
        {
            CancelXYZAffineApplyPreview();
        }
        else if (IsSelectedStepXYZAffineSolve)
        {
            CancelXYZAffineSolvePreview();
        }
        else if (IsSelectedStepDatumPlaneDeviation)
        {
            CancelDatumPlaneDeviationPreview();
        }
        else if (IsSelectedStepLandmarkCorrespondence)
        {
            CancelLandmarkCorrespondencePreview();
        }
        else if (IsSelectedStepLineIntersection)
        {
            CancelLineIntersectionPreview();
        }
        else if (IsSelectedStepTwoPointLine)
        {
            CancelTwoPointLinePreview();
        }
        else if (IsSelectedStepThreePointPlane)
        {
            CancelThreePointPlanePreview();
        }
        else if (IsSelectedStepLineFit)
        {
            CancelLineFitPreview();
        }
        else if (IsSelectedStepHeightDifferenceEdge)
        {
            CancelHeightDifferenceEdgePreview();
        }
        else
        {
            filterExecutionOwner.CancelPreview();
        }
    }

    public Task<bool> PreviewSelectedFilterAsync() =>
        filterExecutionOwner.PreviewAsync();

    public async Task<bool> RunTeachingRecipeAsync()
    {
        var document = CreateDocument();
        if (!TryGetOrderedRunCapability(document, out var message)
            || RecipePath is not { } recipePath)
        {
            orderedRunSummary = message;
            NotifyOrderedRunState();
            AppendLog("Run", message);
            return false;
        }

        orderedRunResult = null;
        orderedRunRecordPath = null;
        isOrderedRunRunning = true;
        orderedRunSummary = Localize(
            $"저장된 현재 레시피의 {document.Steps.Count}개 단계를 순서대로 실행하고 있습니다.",
            $"Running {document.Steps.Count} saved current-recipe step(s) in order.");
        foreach (var step in PipelineSteps)
        {
            step.State = "Run running";
        }
        NotifyOrderedRunState();
        AppendLog("Run", $"Ordered recipe Run started: {Path.GetFileName(recipePath)} | steps={document.Steps.Count}.");

        try
        {
            var execution = await Task.Run(() =>
                ToolRecipeOrderedGraphExecution.Execute(
                    document,
                    Source.Path,
                    SourceQuality.Report));
            orderedRunResult = execution;
            foreach (var step in PipelineSteps)
            {
                step.State = "Not run";
            }
            foreach (var stepResult in execution.Steps)
            {
                var step = PipelineSteps.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, stepResult.StepId, StringComparison.Ordinal));
                if (step is not null)
                {
                    step.State = $"Run {stepResult.Result.Status}";
                }
            }

            orderedRunSummary = CreateOrderedRunSummary(execution);
            NotifyOrderedRunState();
            AppendLog(
                "Run",
                $"Ordered recipe Run completed: status={execution.Status} | steps={execution.Steps.Count} | elapsedMs={execution.Duration.TotalMilliseconds:F3}.");
            OrderedRunCompleted?.Invoke(
                this,
                new ToolWorkbenchOrderedRunCompletedEventArgs(
                    Path.GetFullPath(recipePath),
                    document,
                    Path.GetFullPath(Source.Path),
                    execution));
            return execution.Status is not (ResultStatus.Error or ResultStatus.NotRun);
        }
        finally
        {
            isOrderedRunRunning = false;
            NotifyOrderedRunState();
        }
    }

    private void PublishSelectedFilter() => filterExecutionOwner.Publish();

    private void ShowFilterSource()
    {
        if (!File.Exists(Source.Path))
        {
            return;
        }

        FilterDisplayRequested?.Invoke(
            this,
            new ToolWorkbenchFilterDisplayRequestEventArgs(
                Source.Path,
                SourceSession.SourceBinding?.ContentSha256 ?? string.Empty,
                true));
        SetFilterSummary("Showing the original taught C3D source. Preview output remains available.");
    }

    private bool CanPreviewSelectedFilter() => filterExecutionOwner.CanPreview();

    private bool CanRunTeachingRecipe() =>
        TryGetOrderedRunCapability(out _);

    private bool TryGetOrderedRunCapability(out string message) =>
        TryGetOrderedRunCapability(CreateDocument(), out message);

    private bool TryGetOrderedRunCapability(
        ToolRecipeDocument document,
        out string message)
    {
        if (isOrderedRunRunning)
        {
            message = Localize(
                "현재 레시피 실행이 끝날 때까지 기다리세요.",
                "Wait for the current recipe Run to finish.");
            return false;
        }
        if (IsRecipeMutationBlocked)
        {
            message = Localize(
                "진행 중인 미리보기 또는 검증이 끝난 뒤 실행하세요.",
                "Run after the active Preview or validation finishes.");
            return false;
        }
        if (!IsSourceReadyForRecipe)
        {
            message = Localize(
                $"3D 입력을 먼저 준비하세요. {LocalizedSourceReadinessSummary}",
                $"Prepare the 3D input first. {LocalizedSourceReadinessSummary}");
            return false;
        }
        if (HasPendingStepParameterChanges)
        {
            message = Localize(
                "파라미터 초안을 적용하거나 취소한 뒤 실행하세요.",
                "Apply or discard the parameter draft before Run.");
            return false;
        }
        if (IsDirty)
        {
            message = Localize(
                "현재 레시피 변경 사항을 저장한 뒤 실행하세요.",
                "Save the current recipe changes before Run.");
            return false;
        }
        if (RecipePath is null || !File.Exists(RecipePath))
        {
            message = Localize(
                "Run Record의 레시피 신원을 보존하려면 레시피를 먼저 저장하세요.",
                "Save the recipe first so the Run Record can preserve its identity.");
            return false;
        }
        if (!ToolRecipeOrderedGraphExecution.CanExecute(document, out message))
        {
            return false;
        }

        message = Localize(
            $"준비 완료 · 저장된 현재 레시피의 {document.Steps.Count}개 단계를 명시적으로 실행합니다.",
            $"Ready · explicitly run {document.Steps.Count} saved current-recipe step(s) in order.");
        return true;
    }

    private string CreateOrderedRunSummary(
        ToolRecipeOrderedGraphExecutionResult execution)
    {
        var finalStep = execution.Steps.LastOrDefault();
        var metric = finalStep?.Result.Metrics.FirstOrDefault(candidate =>
            double.IsFinite(candidate.Value));
        var metricSummary = metric is null
            ? Localize("측정값 없음", "no finite metric")
            : $"{metric.Name} {metric.Value:G6} {metric.Unit}".TrimEnd();
        var outputHash = string.IsNullOrWhiteSpace(finalStep?.OutputContentSha256)
            ? "(none)"
            : finalStep.OutputContentSha256[..Math.Min(
                12,
                finalStep.OutputContentSha256.Length)];
        var output = finalStep is null
            ? Localize("출력 없음", "no output")
            : $"{finalStep.OutputEntityId} · SHA-256 {outputHash}";
        return Localize(
            $"{LocalizeStatus(execution.Status)} · {execution.Steps.Count}개 단계 · {metricSummary} · {output}",
            $"{LocalizeStatus(execution.Status)} · {execution.Steps.Count} step(s) · {metricSummary} · {output}");
    }

    internal void AttachOrderedRunRecord(string path)
    {
        orderedRunRecordPath = Path.GetFullPath(path);
        NotifyOrderedRunState();
    }

    internal void ReportOrderedRunRecordFailure(string message)
    {
        orderedRunSummary = Localize(
            $"실행은 완료되었지만 Run Record 저장에 실패했습니다: {message}",
            $"Run completed, but its Run Record could not be saved: {message}");
        AppendLog("Error", orderedRunSummary);
        NotifyOrderedRunState();
    }

    private void InvalidateOrderedRun(string summary)
    {
        if (orderedRunResult is null && orderedRunRecordPath is null)
        {
            return;
        }

        orderedRunResult = null;
        orderedRunRecordPath = null;
        orderedRunSummary = summary;
        NotifyOrderedRunState();
        OrderedRunInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyOrderedRunState()
    {
        OnPropertyChanged(nameof(IsOrderedRunRunning));
        OnPropertyChanged(nameof(HasOrderedRunResult));
        OnPropertyChanged(nameof(CurrentOrderedRunResult));
        OnPropertyChanged(nameof(CurrentOrderedRunRecordPath));
        OnPropertyChanged(nameof(OrderedRunStatus));
        OnPropertyChanged(nameof(OrderedRunCapabilitySummary));
        OnPropertyChanged(nameof(OrderedRunEvidenceSummary));
        runTeachingRecipeCommand?.RaiseCanExecuteChanged();
    }

    private bool CanSetFilterKernel(int kernelSize) => IsSelectedStepFilter && !IsFilterPreviewRunning && kernelSize > 0;

    private void SetFilterKernel(int kernelSize)
    {
        var parameter = SelectedPipelineStep?.Parameters.SingleOrDefault(item => item.Name == "KernelSize");
        if (parameter is not null)
        {
            parameter.Value = kernelSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(FilterKernelSummary));
        }
    }

    private string? GetFilterParameter(string name) =>
        IsSelectedStepFilter
            ? SelectedPipelineStep!.Parameters.SingleOrDefault(parameter => parameter.Name == name)?.Value
            : null;

    private void MarkFilterPreviewStaleIfNeeded(object? sender) =>
        filterExecutionOwner.MarkPreviewStaleIfNeeded(sender);

    private void ClearFilterPreview(string summary) =>
        filterExecutionOwner.ClearPreview(summary);

    private void RefreshFilterExecutionState() =>
        filterExecutionOwner.RefreshExecutionState();

    private void RefreshFilterCommands()
    {
        if (previewSelectedStepCommand is null)
        {
            return;
        }
        previewSelectedStepCommand.RaiseCanExecuteChanged();
        runTeachingRecipeCommand.RaiseCanExecuteChanged();
        publishSelectedStepCommand.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand.RaiseCanExecuteChanged();
        showFilterSourceCommand.RaiseCanExecuteChanged();
        setFilterKernel3Command.RaiseCanExecuteChanged();
        setFilterKernel5Command.RaiseCanExecuteChanged();
        setFilterKernel7Command.RaiseCanExecuteChanged();
    }

    private void SetFilterSummary(string value) =>
        filterExecutionOwner.UpdateSummary(value);
}

public sealed class ToolWorkbenchFilterDisplayRequestEventArgs : EventArgs
{
    public ToolWorkbenchFilterDisplayRequestEventArgs(
        string c3DPath,
        string contentSha256,
        bool isSource,
        string displayLabel = "Filter Preview")
    {
        C3DPath = c3DPath;
        ContentSha256 = contentSha256;
        IsSource = isSource;
        DisplayLabel = displayLabel;
    }

    public string C3DPath { get; }
    public string ContentSha256 { get; }
    public bool IsSource { get; }
    public string DisplayLabel { get; }
}

public sealed class ToolWorkbenchOrderedRunCompletedEventArgs : EventArgs
{
    public ToolWorkbenchOrderedRunCompletedEventArgs(
        string recipePath,
        ToolRecipeDocument document,
        string sourcePath,
        ToolRecipeOrderedGraphExecutionResult execution)
    {
        RecipePath = recipePath;
        Document = document;
        SourcePath = sourcePath;
        Execution = execution;
    }

    public string RecipePath { get; }
    public ToolRecipeDocument Document { get; }
    public string SourcePath { get; }
    public ToolRecipeOrderedGraphExecutionResult Execution { get; }
}
