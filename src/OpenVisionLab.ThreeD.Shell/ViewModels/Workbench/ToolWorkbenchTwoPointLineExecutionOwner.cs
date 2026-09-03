using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchTwoPointLineExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelectedStepTwoPointLine;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeSelection?> getSelectedTeachingSelection;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchTwoPointLineDisplayRequestEventArgs> requestDisplay;
    private readonly Action clearDisplay;
    private readonly Action refreshLineIntersection;
    private readonly Action markLineIntersectionStale;
    private readonly Action clearLineIntersection;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DTwoPointLineFeature? previewOutput;
    private readonly Dictionary<string, C3DTwoPointLineFeature> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> staleOutputIds = new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Capture exactly two ordered C3D grid cells, teach an output role, then Preview explicitly.";
    private int disposalState;

    public ToolWorkbenchTwoPointLineExecutionOwner(
        Func<bool> isSelectedStepTwoPointLine,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeSelection?> getSelectedTeachingSelection,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Action<string, string> appendLog,
        Action<ToolWorkbenchTwoPointLineDisplayRequestEventArgs> requestDisplay,
        Action clearDisplay,
        Action refreshLineIntersection,
        Action markLineIntersectionStale,
        Action clearLineIntersection,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepTwoPointLine = isSelectedStepTwoPointLine;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.getSelectedTeachingSelection = getSelectedTeachingSelection;
        this.findStepByOutputEntityId = findStepByOutputEntityId;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.clearDisplay = clearDisplay;
        this.refreshLineIntersection = refreshLineIntersection;
        this.markLineIntersectionStale = markLineIntersectionStale;
        this.clearLineIntersection = clearLineIntersection;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    private bool IsPreviewForSelectedStep => !IsDisposed && previewOutput is not null
        && string.Equals(
            getSelectedPipelineStep()?.OutputEntityId,
            previewOutput.OutputEntityId,
            StringComparison.OrdinalIgnoreCase);

    public bool IsSelectedStepTwoPointLine => !IsDisposed && isSelectedStepTwoPointLine();
    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed && IsPreviewForSelectedStep && !IsPreviewStale;
    public bool IsPreviewStale => !IsDisposed && getSelectedPipelineStep() is { } step
        && staleOutputIds.Contains(step.OutputEntityId);
    public bool IsPreviewPublished => !IsDisposed && isPreviewPublished && IsPreviewForSelectedStep;
    public C3DTwoPointLineFeature? CurrentOutput => IsDisposed ? null : IsPreviewForSelectedStep
        ? previewOutput
        : null;
    public string ExecutionSummary => IsDisposed
        ? "2-Point Line execution owner has been disposed."
        : executionSummary;

    public string OutputHashSummary => IsDisposed || previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public string SelectionSummary
    {
        get
        {
            if (IsDisposed)
            {
                return "2-Point Line execution owner has been disposed.";
            }

            var selection = getSelectedTeachingSelection();
            return (selection?.Points is { Count: 2 } points
                ? $"Ordered picks: row {points[0].Locator.Row}, column {points[0].Locator.Column} → row {points[1].Locator.Row}, column {points[1].Locator.Column}"
                : "Capture exactly two ordered grid-cell picks before Preview.").Replace(
                    "??",
                    "->",
                    StringComparison.Ordinal);
        }
    }

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DTwoPointLineFeature? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        return publishedOutputs.TryGetValue(outputEntityId, out output);
    }

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var currentCancellation = new CancellationTokenSource();
        var cancellationToken = currentCancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref previewCancellation,
            currentCancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    currentCancellation),
                currentCancellation))
            {
                currentCancellation.Dispose();
            }

            return false;
        }

        SetRunning(true);
        isPreviewStale = false;
        isPreviewPublished = false;
        staleOutputIds.Remove(step.OutputEntityId);
        step.State = "Preview running";
        SetSummary("2-Point Line Preview is resolving the exact current raw C3D values for the authored ordered picks.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"2-Point Line Preview started: {step.Id}.");
        try
        {
            var recipeDirectory = GetRecipeDirectory();
            var evaluation = await Task.Run(
                () => ToolRecipeTwoPointLineExecution.Execute(
                    createDocument(),
                    step.Id,
                    recipeDirectory,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                previewOutput = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (!IsCurrentPreview(currentCancellation))
                {
                    return false;
                }

                appendLog("Error", $"2-Point Line Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetSummary($"Preview ready | {SelectionSummary} | segment {evaluation.Output.SegmentLength:G6} source-coordinate | no fitting or OK/NG");
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            appendLog("Preview", $"2-Point Line Preview ready: {evaluation.Output.ContentSha256}.");
            if (IsCurrentPreview(currentCancellation))
            {
                requestDisplay(new ToolWorkbenchTwoPointLineDisplayRequestEventArgs(evaluation.Output, false));
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetSummary("Preview canceled. The source, picks, and authored recipe were not changed.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", "2-Point Line Preview canceled.");
            }

            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    currentCancellation),
                currentCancellation);
            if (ownsCancellation)
            {
                currentCancellation.Dispose();
                if (!IsDisposed)
                {
                    SetRunning(false);
                }
            }
        }
    }

    public bool CanPreview()
    {
        if (IsDisposed || !IsSelectedStepTwoPointLine || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges() || isPreviewRunning
            || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        return ToolRecipeTwoPointLineExecution.TryPrepare(
            createDocument(),
            step.Id,
            GetRecipeDirectory(),
            out _,
            out _);
    }

    public void Publish()
    {
        if (IsDisposed || getSelectedPipelineStep() is not { } step || !HasCurrentPreview)
        {
            return;
        }

        isPreviewPublished = true;
        publishedOutputs[previewOutput!.OutputEntityId] = previewOutput;
        step.State = "Published";
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput.ContentSha256} | construction evidence only, no fit or OK/NG");
        if (IsDisposed)
        {
            return;
        }

        appendLog("Publish", $"2-Point Line output published without re-running: {step.OutputEntityId}.");
        if (!IsDisposed)
        {
            requestDisplay(new ToolWorkbenchTwoPointLineDisplayRequestEventArgs(previewOutput, true));
        }

        if (!IsDisposed)
        {
            refreshLineIntersection();
        }
    }

    public void Cancel()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref previewCancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal or replacement already released the token source.
        }
    }

    public void MarkStaleIfNeeded(object? sender = null)
    {
        if (IsDisposed)
        {
            return;
        }

        var preview = previewOutput;
        if (isPreviewRunning)
        {
            return;
        }

        ToolWorkbenchPipelineStepItem? affectedStep = null;
        if (sender is not null)
        {
            var step = getSelectedPipelineStep();
            var selectedIsTwoPointLine = string.Equals(
                step?.ToolId,
                "two-point-line",
                StringComparison.Ordinal);
            var parameterChanged = sender is ToolWorkbenchParameterItem parameter
                && (step?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsTwoPointLine
                || (!ReferenceEquals(sender, step) && !parameterChanged))
            {
                return;
            }

            affectedStep = step;
        }
        else if (preview is not null)
        {
            affectedStep = findStepByOutputEntityId(preview.OutputEntityId);
        }

        if (affectedStep is null)
        {
            return;
        }

        var affectedOutputId = affectedStep.OutputEntityId;
        var currentPreviewIsAffected = preview is not null
            && string.Equals(
                preview.OutputEntityId,
                affectedOutputId,
                StringComparison.OrdinalIgnoreCase);
        var hadPublishedOutput = publishedOutputs.Remove(affectedOutputId);
        if (!currentPreviewIsAffected && !hadPublishedOutput)
        {
            return;
        }

        staleOutputIds.Add(affectedOutputId);
        if (currentPreviewIsAffected)
        {
            isPreviewStale = true;
            isPreviewPublished = false;
        }

        if (!IsDisposed)
        {
            markLineIntersectionStale();
        }

        affectedStep.State = "Preview stale";
        if (!IsDisposed)
        {
            clearDisplay();
        }

        SetSummary("Input, ordered picks, 2-Point Line parameters, route, or output changed. Preview again before Publish.");
    }

    public void Clear(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref previewCancellation,
            null);
        CancelAndDispose(currentCancellation);
        previewOutput = null;
        publishedOutputs.Clear();
        staleOutputIds.Clear();
        isPreviewStale = false;
        isPreviewPublished = false;
        if (!IsDisposed)
        {
            clearLineIntersection();
            clearDisplay();
        }

        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedPipelineStep() is { } step && IsSelectedStepTwoPointLine
            && (previewOutput is null
                || !string.Equals(
                    previewOutput.OutputEntityId,
                    step.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase)
                || isPreviewStale)
            && !isPreviewRunning)
        {
            if (ToolRecipeTwoPointLineExecution.TryPrepare(
                createDocument(),
                step.Id,
                GetRecipeDirectory(),
                out _,
                out var message))
            {
                step.State = "Ready";
                executionSummary = "Ready for explicit Preview. Pick capture and source resolution never run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                executionSummary = message;
            }
        }

        onExecutionStateChanged();
    }

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isPreviewRunning = value;
        onExecutionStateChanged();
    }

    private void SetSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        executionSummary = value;
        onExecutionStateChanged();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref previewCancellation,
            null);
        CancelAndDispose(currentCancellation);
        previewOutput = null;
        publishedOutputs.Clear();
        staleOutputIds.Clear();
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref previewCancellation),
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
            // A concurrent owner disposal or replacement already released the token source.
        }

        cancellation.Dispose();
    }

    private string? GetRecipeDirectory()
    {
        var path = getRecipePath();
        return path is null
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(path));
    }
}
