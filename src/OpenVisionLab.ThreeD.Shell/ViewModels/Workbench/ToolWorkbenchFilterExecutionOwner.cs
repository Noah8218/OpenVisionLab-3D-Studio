using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns Filter Preview state and cancellation. The Workbench facade only routes
/// commands and projections; an older Preview must not publish after a newer
/// Preview, Clear, or disposal has replaced its operation.
/// </summary>
internal sealed class ToolWorkbenchFilterExecutionOwner : IDisposable
{
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
    private int disposalState;
    public ToolWorkbenchFilterExecutionOwner(
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
    }

    public RelayCommand ShowFilterSourceCommand => showFilterSourceCommand;
    public RelayCommand SetFilterKernel3Command => setFilterKernel3Command;
    public RelayCommand SetFilterKernel5Command => setFilterKernel5Command;
    public RelayCommand SetFilterKernel7Command => setFilterKernel7Command;

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;
    public C3DHeightFieldSnapshot? FilterPreviewOutput => IsDisposed ? null : filterPreviewOutput;
    public string? FilterPreviewPath => IsDisposed ? null : filterPreviewPath;
    public bool IsFilterPreviewRunning => !IsDisposed && isFilterPreviewRunning;
    public bool HasCurrentFilterPreview => !IsDisposed && filterPreviewOutput is not null && !isFilterPreviewStale;
    public bool IsFilterPreviewStale => !IsDisposed && isFilterPreviewStale;
    public bool IsFilterPreviewPublished => !IsDisposed && isFilterPreviewPublished;
    public string FilterExecutionSummary => IsDisposed
        ? "Filter execution owner has been disposed."
        : filterExecutionSummary;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(ref filterPreviewCancellation, null);
        CancelAndDispose(cancellation);
        filterPreviewOutput = null;
        filterPreviewPath = null;
        isFilterPreviewRunning = false;
        isFilterPreviewStale = false;
        isFilterPreviewPublished = false;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed
            || !CanPreview()
            || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref filterPreviewCancellation,
            cancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(ref filterPreviewCancellation, null, cancellation),
                cancellation))
            {
                cancellation.Dispose();
            }

            return false;
        }

        SetRunning(true);
        isFilterPreviewStale = false;
        isFilterPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Median Preview is running from the verified C3D source bytes.");
        if (!IsCurrentPreview(cancellation))
        {
            return false;
        }

        appendLog("Preview", $"Filter Preview started: {step.Id}.");

        try
        {
            var document = createDocument();
            var evaluation = await Task.Run(
                () => ToolRecipeFilterExecution.Execute(
                    document,
                    step.Id,
                    GetRecipeDirectory(),
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                filterPreviewOutput = null;
                filterPreviewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (IsCurrentPreview(cancellation))
                {
                    appendLog("Error", $"Filter Preview failed: {evaluation.Result.Message}");
                }

                return false;
            }

            filterPreviewOutput = evaluation.Output;
            filterPreviewPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(filterPreviewPath);
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

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
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetSummary("Preview canceled. Source and authored recipe were not changed.");
            appendLog("Preview", "Filter Preview canceled.");
            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(ref filterPreviewCancellation, null, cancellation),
                cancellation);
            if (ownsCancellation)
            {
                cancellation.Dispose();
            }

            if (ownsCancellation && !IsDisposed)
            {
                SetRunning(false);
            }
        }
    }

    public bool CanPreview() =>
        !IsDisposed
        && isSelectedStepFilter()
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isFilterPreviewRunning
        && !isEdgePreviewRunning()
        && getSelectedPipelineStep() is { } step
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void Publish()
    {
        if (IsDisposed || getSelectedPipelineStep() is not { } step || !HasCurrentFilterPreview)
        {
            return;
        }

        isFilterPreviewPublished = true;
        step.State = "Published";
        SetSummary($"Published output {step.OutputEntityId} | SHA-256 {filterPreviewOutput!.ContentSha256} | preprocessing only, no OK/NG");
        appendLog("Publish", $"Filter output published without re-running: {step.OutputEntityId}.");
    }

    public void CancelPreview()
    {
        if (Volatile.Read(ref disposalState) == 0)
        {
            try
            {
                Volatile.Read(ref filterPreviewCancellation)?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // A concurrent owner disposal already released the token source.
            }
        }
    }

    public void MarkPreviewStaleIfNeeded(object? sender)
    {
        if (IsDisposed || filterPreviewOutput is null || isFilterPreviewRunning)
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
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(ref filterPreviewCancellation, null);
        CancelAndDispose(cancellation);
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
        if (IsDisposed)
        {
            return;
        }

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

    public void UpdateSummary(string value)
    {
        if (!IsDisposed)
        {
            SetSummary(value);
        }
    }

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isFilterPreviewRunning = value;
        onExecutionStateChanged();
    }

    private void SetSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        filterExecutionSummary = value;
        onExecutionStateChanged();
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref filterPreviewCancellation),
            cancellation);

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }

        cancellation.Dispose();
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

}
