using System;
using System.IO;
using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchFilterExecutionOwner
{
    private readonly RelayCommand previewSelectedStepCommand;
    private readonly RelayCommand runTeachingRecipeCommand;
    private readonly RelayCommand publishSelectedStepCommand;
    private readonly RelayCommand cancelFilterPreviewCommand;
    private readonly RelayCommand showFilterSourceCommand;
    private readonly RelayCommand setFilterKernel3Command;
    private readonly RelayCommand setFilterKernel5Command;
    private readonly RelayCommand setFilterKernel7Command;

    private readonly Func<bool> isSelectedStepFilter;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<bool> isEdgePreviewRunning;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay;
    private readonly Action<string> markEdgePreviewStale;
    private readonly Action<string> clearEdgePreview;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? filterPreviewCancellation;
    private C3DHeightFieldSnapshot? filterPreviewOutput;
    private string? filterPreviewPath;
    private bool isFilterPreviewRunning;
    private bool isFilterPreviewStale;
    private bool isFilterPreviewPublished;
    private string filterExecutionSummary;
    private bool isOrderedRunRunning;
    private ToolRecipeOrderedGraphExecutionResult? orderedRunResult;
    private string? orderedRunRecordPath;
    private string orderedRunSummary;

    public ToolWorkbenchFilterExecutionOwner(
        Func<Task> previewSelectedStepAsync,
        Func<bool> canPreviewSelectedStep,
        Func<Task> runTeachingRecipeAsync,
        Func<bool> canRunTeachingRecipe,
        Action publishSelectedStep,
        Func<bool> canPublishSelectedStep,
        Action cancelSelectedPreview,
        Func<bool> canCancelSelectedPreview,
        Func<bool> canShowFilterSource,
        Action showFilterSource,
        Action setFilterKernel3,
        Func<bool> canSetFilterKernel3,
        Action setFilterKernel5,
        Func<bool> canSetFilterKernel5,
        Action setFilterKernel7,
        Func<bool> canSetFilterKernel7,
        Func<bool> isSelectedStepFilter,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<bool> isEdgePreviewRunning,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<object?, bool> isSourceChangeEvent,
        Action<string, string> appendLog,
        Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay,
        Action<string> markEdgePreviewStale,
        Action<string> clearEdgePreview,
        Action onExecutionStateChanged)
    {
        previewSelectedStepCommand = new RelayCommand(_ => _ = previewSelectedStepAsync(), _ => canPreviewSelectedStep());
        runTeachingRecipeCommand = new RelayCommand(_ => _ = runTeachingRecipeAsync(), _ => canRunTeachingRecipe());
        publishSelectedStepCommand = new RelayCommand(_ => publishSelectedStep(), _ => canPublishSelectedStep());
        cancelFilterPreviewCommand = new RelayCommand(_ => cancelSelectedPreview(), _ => canCancelSelectedPreview());
        showFilterSourceCommand = new RelayCommand(_ => showFilterSource(), _ => canShowFilterSource());
        setFilterKernel3Command = new RelayCommand(_ => setFilterKernel3(), _ => canSetFilterKernel3());
        setFilterKernel5Command = new RelayCommand(_ => setFilterKernel5(), _ => canSetFilterKernel5());
        setFilterKernel7Command = new RelayCommand(_ => setFilterKernel7(), _ => canSetFilterKernel7());

        this.isSelectedStepFilter = isSelectedStepFilter;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.isEdgePreviewRunning = isEdgePreviewRunning;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.isSourceChangeEvent = isSourceChangeEvent;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.markEdgePreviewStale = markEdgePreviewStale;
        this.clearEdgePreview = clearEdgePreview;
        this.onExecutionStateChanged = onExecutionStateChanged;

        filterExecutionSummary = "Select a Filter step, then Preview explicitly.";
        orderedRunSummary = "Run the saved current recipe explicitly to create a Run Record.";
    }

    public RelayCommand PreviewSelectedStepCommand => previewSelectedStepCommand;
    public RelayCommand RunTeachingRecipeCommand => runTeachingRecipeCommand;
    public RelayCommand PublishSelectedStepCommand => publishSelectedStepCommand;
    public RelayCommand CancelFilterPreviewCommand => cancelFilterPreviewCommand;
    public RelayCommand ShowFilterSourceCommand => showFilterSourceCommand;
    public RelayCommand SetFilterKernel3Command => setFilterKernel3Command;
    public RelayCommand SetFilterKernel5Command => setFilterKernel5Command;
    public RelayCommand SetFilterKernel7Command => setFilterKernel7Command;

    public C3DHeightFieldSnapshot? FilterPreviewOutput => filterPreviewOutput;
    public string? FilterPreviewPath => filterPreviewPath;
    public bool IsFilterPreviewRunning => isFilterPreviewRunning;
    public bool HasCurrentFilterPreview => filterPreviewOutput is not null && !isFilterPreviewStale;
    public bool IsFilterPreviewStale => isFilterPreviewStale;
    public bool IsFilterPreviewPublished => isFilterPreviewPublished;
    public string FilterExecutionSummary => filterExecutionSummary;

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        filterPreviewCancellation?.Dispose();
        filterPreviewCancellation = new CancellationTokenSource();
        SetRunning(true);
        isFilterPreviewStale = false;
        isFilterPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Median Preview is running from the verified C3D source bytes.");
        appendLog("Preview", $"Filter Preview started: {step.Id}.");

        try
        {
            var document = createDocument();
            var evaluation = await Task.Run(
                () => ToolRecipeFilterExecution.Execute(
                    document,
                    step.Id,
                    GetRecipeDirectory(),
                    filterPreviewCancellation.Token),
                filterPreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                filterPreviewOutput = null;
                filterPreviewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Filter Preview failed: {evaluation.Result.Message}");
                return false;
            }

            filterPreviewOutput = evaluation.Output;
            filterPreviewPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(filterPreviewPath);
            step.State = "Preview ready";
            SetSummary($"Preview ready | valid {evaluation.Output.ValidCount:N0} | missing {evaluation.Output.MissingCount:N0} | preprocessing only, no OK/NG");
            appendLog("Preview", $"Filter Preview ready: {evaluation.Output.ContentSha256}.");
            requestDisplay(
                new ToolWorkbenchFilterDisplayRequestEventArgs(
                    filterPreviewPath,
                    evaluation.Output.ContentSha256,
                    false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. Source and authored recipe were not changed.");
            appendLog("Preview", "Filter Preview canceled.");
            return false;
        }
        finally
        {
            SetRunning(false);
        }
    }

    public bool CanPreview() =>
        isSelectedStepFilter()
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isFilterPreviewRunning
        && !isEdgePreviewRunning()
        && getSelectedPipelineStep() is { } step
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void Publish()
    {
        if (getSelectedPipelineStep() is not { } step || !HasCurrentFilterPreview)
        {
            return;
        }

        isFilterPreviewPublished = true;
        step.State = "Published";
        SetSummary($"Published output {step.OutputEntityId} | SHA-256 {filterPreviewOutput!.ContentSha256} | preprocessing only, no OK/NG");
        appendLog("Publish", $"Filter output published without re-running: {step.OutputEntityId}.");
    }

    public void CancelPreview() => filterPreviewCancellation?.Cancel();

    public void MarkPreviewStaleIfNeeded(object? sender)
    {
        if (filterPreviewOutput is null || isFilterPreviewRunning)
        {
            return;
        }

        var selected = getSelectedPipelineStep();
        var selectedIsFilter = isSelectedStepFilter();
        var isSelectedFilterParameter = selectedIsFilter
            && sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        if (isSourceChangeEvent(sender)
            || selectedIsFilter && ReferenceEquals(sender, selected)
            || isSelectedFilterParameter)
        {
            isFilterPreviewStale = true;
            isFilterPreviewPublished = false;
            markEdgePreviewStale(
                "Published Filter input changed. Preview Edge again after Filter is republished.");
            if (selected is not null)
            {
                selected.State = "Preview stale";
            }
            SetSummary("Source, routing, output, or Kernel changed. Preview again before Publish.");
        }
    }

    public void ClearPreview(string summary)
    {
        filterPreviewCancellation?.Cancel();
        filterPreviewOutput = null;
        filterPreviewPath = null;
        isFilterPreviewStale = false;
        isFilterPreviewPublished = false;
        clearEdgePreview(
            "Published Filter output is unavailable; Edge Preview is required after Filter is republished.");
        SetSummary(summary);
    }

    public void RefreshExecutionState()
    {
        if (getSelectedPipelineStep() is { } step
            && isSelectedStepFilter()
            && filterPreviewOutput is null
            && !isFilterPreviewRunning
            && step.State == "Taught / pending")
        {
            step.State = ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid
                ? "Ready"
                : "Taught / needs correction";
        }
        onExecutionStateChanged();
    }

    public void UpdateSummary(string value) => SetSummary(value);

    private void SetRunning(bool value)
    {
        isFilterPreviewRunning = value;
        onExecutionStateChanged();
    }

    private void SetSummary(string value)
    {
        filterExecutionSummary = value;
        onExecutionStateChanged();
    }

    private string GetRecipeDirectory()
    {
        var path = getRecipePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return Environment.CurrentDirectory;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return string.IsNullOrWhiteSpace(directory)
            ? Environment.CurrentDirectory
            : directory;
    }

    private static string CreatePreviewPath(string hash)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "3DStudio",
            "Preview");
        return Path.Combine(directory, $"filter-{hash}.c3d");
    }

    public bool IsOrderedRunRunning
    {
        get => isOrderedRunRunning;
        set => isOrderedRunRunning = value;
    }

    public ToolRecipeOrderedGraphExecutionResult? OrderedRunResult
    {
        get => orderedRunResult;
        set => orderedRunResult = value;
    }

    public string? OrderedRunRecordPath
    {
        get => orderedRunRecordPath;
        set => orderedRunRecordPath = value;
    }

    public string OrderedRunSummary
    {
        get => orderedRunSummary;
        set => orderedRunSummary = value;
    }
}
