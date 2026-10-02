using System;
using System.IO;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
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
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay;
    private readonly Action<string> markEdgePreviewStale;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? filterPreviewCancellation;
    private C3DHeightFieldSnapshot? filterPreviewOutput;
    private ToolWorkbenchPipelineStepItem? previewStep;
    private string previewContext = string.Empty;
    private readonly Dictionary<string, (C3DHeightFieldSnapshot Output, string Path, string StepId, string Context)> publishedOutputs = new(StringComparer.OrdinalIgnoreCase);
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
        Action<string, string> appendLog,
        Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay,
        Action<string> markEdgePreviewStale,
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
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.markEdgePreviewStale = markEdgePreviewStale;
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
    public bool HasCurrentFilterPreview => !IsDisposed && !isFilterPreviewRunning && filterPreviewOutput is not null && !isFilterPreviewStale;
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
        publishedOutputs.Clear();
        previewStep = null;
        previewContext = string.Empty;
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

        publishedOutputs.Remove(step.OutputEntityId);
        SetRunning(true);
        isFilterPreviewStale = filterPreviewOutput is not null;
        isFilterPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Median Preview is running from the verified C3D source bytes.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            appendLog("Preview", $"Filter Preview started: {step.Id}.");
            var document = createDocument();
            var context = document.CreateStepExecutionContext(step.Id);
            var evaluation = await Task.Run(
                () => ToolRecipeFilterExecution.Execute(
                    document,
                    step.Id,
                    GetRecipeDirectory(),
                    cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            if (!ReferenceEquals(step, getSelectedPipelineStep())
                || !string.Equals(context, createDocument().CreateStepExecutionContext(step.Id), StringComparison.Ordinal))
            {
                isFilterPreviewStale = filterPreviewOutput is not null;
                step.State = "Preview stale";
                SetSummary("Input or conditions changed. Run again for the current result.");
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                isFilterPreviewStale = filterPreviewOutput is not null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (IsCurrentPreview(cancellation))
                {
                    appendLog("Error", $"Filter Preview failed: {evaluation.Result.Message}");
                }

                return false;
            }

            var outputPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            SaveC3DAtomically(evaluation.Output, outputPath);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            filterPreviewOutput = evaluation.Output;
            previewStep = step;
            previewContext = context;
            filterPreviewPath = outputPath;
            isFilterPreviewStale = false;
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
            if (IsDisposed || !ReferenceEquals(Volatile.Read(ref filterPreviewCancellation), cancellation))
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
        if (IsDisposed || getSelectedPipelineStep() is not { } step || !HasCurrentFilterPreview
            || !ReferenceEquals(step, previewStep))
        {
            return;
        }

        isFilterPreviewPublished = true;
        publishedOutputs[filterPreviewOutput!.EntityId] = (filterPreviewOutput, filterPreviewPath!, step.Id, previewContext);
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
        if (IsDisposed || isFilterPreviewRunning || filterPreviewOutput is null && publishedOutputs.Count == 0)
        {
            return;
        }

        var document = createDocument();
        var changed = false;
        foreach (var (id, published) in publishedOutputs.ToArray())
        {
            if (string.Equals(published.Context, document.CreateStepExecutionContext(published.StepId), StringComparison.Ordinal)) continue;
            publishedOutputs.Remove(id);
            changed = true;
        }
        if (filterPreviewOutput is not null && !isFilterPreviewStale && previewStep is not null
            && !string.Equals(previewContext, document.CreateStepExecutionContext(previewStep.Id), StringComparison.Ordinal))
        {
            isFilterPreviewStale = true;
            isFilterPreviewPublished = false;
            previewStep.State = "Preview stale";
            changed = true;
        }
        if (!changed) return;
        markEdgePreviewStale("Published Filter input changed. Preview Edge again after Filter is republished.");
        if (isFilterPreviewStale) SetSummary("Source, routing, output, or Kernel changed. Preview again before Publish.");
        else onExecutionStateChanged();
    }

    public (C3DHeightFieldSnapshot Output, string Path)? GetPublishedOutput(string? outputEntityId)
    {
        if (IsDisposed || outputEntityId is null || !publishedOutputs.TryGetValue(outputEntityId, out var published)
            || !string.Equals(published.Context, createDocument().CreateStepExecutionContext(published.StepId), StringComparison.Ordinal)) return null;
        return (published.Output, published.Path);
    }

    public void ForgetOutput(string outputEntityId)
    {
        if (IsDisposed) return;
        publishedOutputs.Remove(outputEntityId);
        if (!string.Equals(filterPreviewOutput?.EntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)) return;
        filterPreviewOutput = null;
        filterPreviewPath = null;
        previewStep = null;
        previewContext = string.Empty;
        isFilterPreviewStale = false;
        isFilterPreviewPublished = false;
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
        publishedOutputs.Clear();
        previewStep = null;
        previewContext = string.Empty;
        filterPreviewPath = null;
        isFilterPreviewStale = false;
        isFilterPreviewPublished = false;
        markEdgePreviewStale(
            "Published Filter output is unavailable; Edge Preview is required after Filter is republished.");
        SetSummary(summary);
    }

    public void RefreshExecutionState()
    {
        if (IsDisposed)
        {
            return;
        }

        MarkPreviewStaleIfNeeded(null);

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
        !IsDisposed && !cancellation.IsCancellationRequested && ReferenceEquals(
            Volatile.Read(ref filterPreviewCancellation),
            cancellation);

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
        var root = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var directory = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenVisionLab", "3DStudio", "Preview")
            : Path.Combine(Path.GetFullPath(root), "Preview");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"filter-{hash}.c3d");
    }

    private static void SaveC3DAtomically(C3DHeightFieldSnapshot output, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            output.SaveC3D(temporaryPath);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

}
