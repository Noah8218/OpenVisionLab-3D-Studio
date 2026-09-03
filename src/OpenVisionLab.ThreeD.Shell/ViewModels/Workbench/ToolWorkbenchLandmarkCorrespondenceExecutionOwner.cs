using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the explicit Landmark Correspondence Preview/Publish lifecycle. The
/// Workbench facade supplies recipe identity and current Published CornerAnchor
/// access without sharing this owner's private execution state.
/// </summary>
internal sealed class ToolWorkbenchLandmarkCorrespondenceExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelected;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedStep;
    private readonly Func<bool> isSourceReady;
    private readonly Func<bool> hasPendingParameterChanges;
    private readonly Func<string> getSourceId;
    private readonly Func<string> getSourceUnit;
    private readonly Func<string> getSourceFrameId;
    private readonly Func<ToolRecipeSelectionSourceBinding?> getSourceBinding;
    private readonly Func<string, ToolRecipeSelection?> getSelection;
    private readonly Func<string, C3DLineIntersectionFeature?> getPublishedAnchor;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> getStepByOutputId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs> requestDisplay;
    private readonly Action clearDisplay;
    private readonly Action markAffineSolveStale;
    private readonly Action<string> clearAffineSolve;
    private readonly Action refreshAffineState;
    private readonly Action onStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DLandmarkCorrespondenceSet? previewOutput;
    private readonly Dictionary<string, C3DLandmarkCorrespondenceSet> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Teach four explicit CornerAnchor/reference pairs and the reference descriptor, then publish every named CornerAnchor before Preview.";
    private int disposalState;

    public ToolWorkbenchLandmarkCorrespondenceExecutionOwner(
        Func<bool> isSelected,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedStep,
        Func<bool> isSourceReady,
        Func<bool> hasPendingParameterChanges,
        Func<string> getSourceId,
        Func<string> getSourceUnit,
        Func<string> getSourceFrameId,
        Func<ToolRecipeSelectionSourceBinding?> getSourceBinding,
        Func<string, ToolRecipeSelection?> getSelection,
        Func<string, C3DLineIntersectionFeature?> getPublishedAnchor,
        Func<string, ToolWorkbenchPipelineStepItem?> getStepByOutputId,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action<ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs> requestDisplay,
        Action clearDisplay,
        Action markAffineSolveStale,
        Action<string> clearAffineSolve,
        Action refreshAffineState,
        Action onStateChanged)
    {
        this.isSelected = isSelected;
        this.getSelectedStep = getSelectedStep;
        this.isSourceReady = isSourceReady;
        this.hasPendingParameterChanges = hasPendingParameterChanges;
        this.getSourceId = getSourceId;
        this.getSourceUnit = getSourceUnit;
        this.getSourceFrameId = getSourceFrameId;
        this.getSourceBinding = getSourceBinding;
        this.getSelection = getSelection;
        this.getPublishedAnchor = getPublishedAnchor;
        this.getStepByOutputId = getStepByOutputId;
        this.createDocument = createDocument;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.clearDisplay = clearDisplay;
        this.markAffineSolveStale = markAffineSolveStale;
        this.clearAffineSolve = clearAffineSolve;
        this.refreshAffineState = refreshAffineState;
        this.onStateChanged = onStateChanged;
    }

    public bool IsSelected => !IsDisposed && isSelected();
    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed && previewOutput is not null && !isPreviewStale;
    public bool IsPreviewStale => !IsDisposed && isPreviewStale;
    public bool IsPreviewPublished => !IsDisposed && isPreviewPublished;
    public C3DLandmarkCorrespondenceSet? CurrentOutput => IsDisposed ? null : previewOutput;
    public string ExecutionSummary => IsDisposed
        ? "Landmark Correspondence execution owner has been disposed."
        : executionSummary;
    public string OutputHashSummary => IsDisposed || previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";
    public string EvidenceSummary => IsDisposed || previewOutput is null
        ? "No correspondence evidence until Preview completes."
        : $"{previewOutput.Pairs.Count} pairs | source rank {previewOutput.SourceRank}/4 | reference rank {previewOutput.ReferenceRank}/4 | normalized volume {previewOutput.SourceNormalizedTetrahedronVolume:G6} / {previewOutput.ReferenceNormalizedTetrahedronVolume:G6}";

    public string UpstreamSummary
    {
        get
        {
            if (IsDisposed)
            {
                return "Landmark Correspondence execution owner has been disposed.";
            }

            if (!TryGetSelectedSelection(out var selection))
            {
                return "One routed landmark-correspondence selection is required.";
            }

            var rows = selection.Rows ?? [];
            var states = rows.Select(row =>
                $"{row.SourceEntityId}: {(getPublishedAnchor(row.SourceEntityId) is not null ? "Published" : "missing/stale")}");
            return $"{rows.Count}/4 authored rows | {string.Join(" | ", states)}";
        }
    }

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DLandmarkCorrespondenceSet? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        return publishedOutputs.TryGetValue(outputEntityId, out output);
    }

    public bool TryGetCurrentInputs(out IReadOnlyList<C3DLineIntersectionFeature> anchors)
    {
        anchors = [];
        if (IsDisposed || !TryGetSelectedSelection(out var selection))
        {
            return false;
        }

        var results = new List<C3DLineIntersectionFeature>();
        foreach (var row in selection.Rows ?? [])
        {
            var anchor = getPublishedAnchor(row.SourceEntityId);
            if (anchor is null)
            {
                return false;
            }

            results.Add(anchor);
        }

        anchors = results;
        return results.Count == 4;
    }

    public bool TryRegisterSyntheticPublishedOutput(
        C3DLandmarkCorrespondenceSet output,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (IsDisposed)
        {
            message = "Landmark Correspondence execution owner has been disposed.";
            return false;
        }

        if (getSelectedStep() is not { ToolId: "xyz-affine-solve", InputEntityIds.Count: 1 } solveStep
            || !string.Equals(solveStep.InputEntityIds[0], output.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(getSourceId(), output.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || getSourceBinding() is not { } binding
            || !string.Equals(binding.ContentSha256, output.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(getSourceUnit(), output.SourceUnit, StringComparison.Ordinal)
            || !string.Equals(getSourceFrameId(), output.SourceFrameId, StringComparison.Ordinal))
        {
            message = "Synthetic smoke CorrespondenceSet identity does not match the selected XYZ Affine Solve route and loaded recipe source.";
            return false;
        }

        publishedOutputs[output.OutputEntityId] = output;
        appendLog(
            "Smoke",
            $"Registered deterministic synthetic Published CorrespondenceSet prerequisite {output.OutputEntityId} ({output.ContentSha256}); normal Landmark Correspondence Preview/Publish remains explicit.");
        if (IsDisposed)
        {
            message = "Landmark Correspondence execution owner has been disposed.";
            return false;
        }

        if (!IsDisposed)
        {
            refreshAffineState();
        }

        message = $"Synthetic Published CorrespondenceSet registered for smoke-only execution: {output.ContentSha256}";
        return true;
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
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview() || getSelectedStep() is not { } step
            || !TryGetCurrentInputs(out var anchors))
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
        step.State = "Preview running";
        SetSummary("Landmark Correspondence Preview validates only the four exact Published CornerAnchors and explicit reference coordinates.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"Landmark Correspondence Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeLandmarkCorrespondenceExecution.Execute(
                    createDocument(),
                    step.Id,
                    anchors,
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

                appendLog("Error", $"Landmark Correspondence Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetSummary($"Preview ready | {EvidenceSummary} | no affine matrix or OK/NG");
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            appendLog("Preview", $"Landmark Correspondence Preview ready: {evaluation.Output.ContentSha256}.");
            if (IsCurrentPreview(currentCancellation))
            {
                requestDisplay(new ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs(
                    anchors,
                    evaluation.Output,
                    false));
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
            SetSummary("Preview canceled. Published CornerAnchors and authored recipe were not changed.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", "Landmark Correspondence Preview canceled.");
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
        if (IsDisposed || !IsSelected || !isSourceReady() || hasPendingParameterChanges()
            || isPreviewRunning || getSelectedStep() is not { } step
            || !TryGetCurrentInputs(out var anchors))
        {
            return false;
        }

        return ToolRecipeLandmarkCorrespondenceExecution.TryPrepare(
            createDocument(),
            step.Id,
            anchors,
            out _,
            out _);
    }

    public void Publish()
    {
        if (IsDisposed || getSelectedStep() is not { } step || !HasCurrentPreview)
        {
            return;
        }

        isPreviewPublished = true;
        publishedOutputs[previewOutput!.OutputEntityId] = previewOutput;
        step.State = "Published";
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput.ContentSha256} | correspondence evidence only, no affine matrix or OK/NG");
        if (IsDisposed)
        {
            return;
        }

        appendLog("Publish", $"Landmark Correspondence output published without re-running: {step.OutputEntityId}.");
        if (!IsDisposed && TryGetCurrentInputs(out var anchors))
        {
            requestDisplay(new ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs(
                anchors,
                previewOutput,
                true));
        }

        if (!IsDisposed)
        {
            refreshAffineState();
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
        if (IsDisposed || previewOutput is null || isPreviewRunning)
        {
            return;
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        publishedOutputs.Clear();
        var step = getStepByOutputId(previewOutput.OutputEntityId);
        if (step is not null)
        {
            step.State = "Preview stale";
        }

        clearDisplay();
        if (IsDisposed)
        {
            return;
        }

        SetSummary("Recipe, correspondence row, descriptor, or published CornerAnchor changed. Preview again before Publish.");
        if (IsDisposed)
        {
            return;
        }

        markAffineSolveStale();
        if (!IsDisposed)
        {
            refreshAffineState();
        }
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
        SetRunning(false);
        isPreviewStale = false;
        isPreviewPublished = false;
        clearDisplay();
        if (IsDisposed)
        {
            return;
        }

        SetSummary(summary);
        if (IsDisposed)
        {
            return;
        }

        clearAffineSolve("Published CorrespondenceSet was cleared. XYZ Affine Solve Preview was cleared without execution.");
        if (!IsDisposed)
        {
            refreshAffineState();
        }
    }

    public void RefreshState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedStep() is { } step && IsSelected
            && (previewOutput is null
                || !string.Equals(previewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || isPreviewStale)
            && !isPreviewRunning)
        {
            if (!TryGetCurrentInputs(out var anchors))
            {
                step.State = "Waiting for upstream";
            }
            else if (ToolRecipeLandmarkCorrespondenceExecution.TryPrepare(
                createDocument(),
                step.Id,
                anchors,
                out _,
                out var message))
            {
                step.State = "Ready";
                executionSummary = "Ready for explicit Preview. Line Intersection and Affine will not run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                executionSummary = message;
            }
        }

        onStateChanged();
        if (!IsDisposed)
        {
            refreshAffineState();
        }
    }

    private bool TryGetSelectedSelection(out ToolRecipeSelection selection)
    {
        selection = null!;
        if (IsDisposed || getSelectedStep() is not { InputEntityIds.Count: 1 } step)
        {
            return false;
        }

        var candidate = getSelection(step.InputEntityIds[0]);
        if (candidate is null
            || !string.Equals(
                candidate.Kind,
                ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
                StringComparison.Ordinal))
        {
            return false;
        }

        selection = candidate;
        return true;
    }

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isPreviewRunning = value;
        onStateChanged();
    }

    private void SetSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        executionSummary = value;
        onStateChanged();
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
}
