using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchHeightMeasurementExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelectedStepMeasurement;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string> getSourceEntityId;
    private readonly Func<string, C3DHeightFieldSnapshot?> getPublishedCroppedHeightField;
    private readonly Func<string, C3DTransformedHeightField?> getPublishedHeightField;
    private readonly Func<string, C3DEditableRegionArtifact?> getPublishedEditableRegion;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolRecipeHeightMeasurementOutput?> updateCompletenessPresentation;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private ToolRecipeHeightMeasurementOutput? previewOutput;
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Route a verified HeightField and recipe-owned GridRectangle, then Preview explicitly.";
    private int disposalState;

    public ToolWorkbenchHeightMeasurementExecutionOwner(
        Func<bool> isSelectedStepMeasurement,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> hasPendingStepParameterChanges,
        Func<string?> getRecipePath,
        Func<string> getSourceEntityId,
        Func<string, C3DHeightFieldSnapshot?> getPublishedCroppedHeightField,
        Func<string, C3DTransformedHeightField?> getPublishedHeightField,
        Func<string, C3DEditableRegionArtifact?> getPublishedEditableRegion,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action<ToolRecipeHeightMeasurementOutput?> updateCompletenessPresentation,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepMeasurement = isSelectedStepMeasurement;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.getRecipePath = getRecipePath;
        this.getSourceEntityId = getSourceEntityId;
        this.getPublishedCroppedHeightField = getPublishedCroppedHeightField;
        this.getPublishedHeightField = getPublishedHeightField;
        this.getPublishedEditableRegion = getPublishedEditableRegion;
        this.findStepByOutputEntityId = findStepByOutputEntityId;
        this.createDocument = createDocument;
        this.appendLog = appendLog;
        this.updateCompletenessPresentation = updateCompletenessPresentation;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed && previewOutput is not null && !isPreviewStale;
    public bool IsPreviewStale => !IsDisposed && isPreviewStale;
    public bool IsPreviewPublished => !IsDisposed && isPreviewPublished;
    public ToolRecipeHeightMeasurementOutput? CurrentOutput => IsDisposed ? null : previewOutput;
    public string ExecutionSummary => IsDisposed
        ? "Height Measurement execution owner has been disposed."
        : executionSummary;
    public string EvidenceSummary => IsDisposed
        ? "No measurement evidence after the execution owner has been disposed."
        : previewOutput?.EvidenceSummary
        ?? "No measurement evidence until Preview completes.";

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

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
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            if (!IsDisposed && getSelectedPipelineStep() is { } waiting)
            {
                waiting.State = "Taught incomplete";
            }

            if (!IsDisposed)
            {
                SetSummary("A current raw or Published transformed HeightField and its owned GridRectangle are required.");
            }

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
        SetSummary($"{step.ToolName} Preview is evaluating only the selected tool step.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"{step.ToolName} Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeHeightMeasurementExecution.Execute(
                    createDocument(),
                    step.Id,
                    GetCurrentCroppedHeightField(),
                    GetCurrentTransformedHeightField(),
                    GetCurrentEditableRegion(),
                    GetRecipeDirectory(),
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            if (evaluation.Output is null || evaluation.Result.Status == ResultStatus.Error)
            {
                if (!IsCurrentPreview(currentCancellation))
                {
                    return false;
                }

                previewOutput = null;
                UpdateCompletenessPresentation(null);
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (IsCurrentPreview(currentCancellation))
                {
                    appendLog("Error", $"{step.ToolName} Preview failed: {evaluation.Result.Message}");
                }

                return false;
            }

            previewOutput = evaluation.Output;
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            UpdateCompletenessPresentation(previewOutput);
            step.State = "Preview ready";
            SetSummary($"Preview ready | {previewOutput.EvidenceSummary} | {evaluation.Result.Status} | declared source units only.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", $"{step.ToolName} Preview ready: {previewOutput.ContentSha256}.");
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
            SetSummary("Preview canceled. The source, ROI, and authored recipe were not changed.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", $"{step.ToolName} Preview canceled.");
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
        if (IsDisposed || !isSelectedStepMeasurement() || hasPendingStepParameterChanges()
            || isPreviewRunning || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        return ToolRecipeHeightMeasurementExecution.TryPrepare(
            createDocument(),
            step.Id,
            GetCurrentCroppedHeightField(),
            GetCurrentTransformedHeightField(),
            GetCurrentEditableRegion(),
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
        step.State = "Published";
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput!.ContentSha256} | no recalculation.");
        appendLog("Publish", $"{step.ToolName} output published without re-running: {step.OutputEntityId}.");
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
        UpdateCompletenessPresentation(null);
        SetRunning(false);
        isPreviewStale = false;
        isPreviewPublished = false;
        SetSummary(summary);
    }

    public void MarkStaleIfNeeded(object? sender = null)
    {
        if (IsDisposed || previewOutput is null || isPreviewRunning)
        {
            return;
        }

        var step = findStepByOutputEntityId(previewOutput.OutputEntityId);
        if (step is null)
        {
            return;
        }

        if (sender is not null
            && !ReferenceEquals(sender, step)
            && (sender is not ToolWorkbenchParameterItem parameter
                || !step.Parameters.Contains(parameter)))
        {
            return;
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        UpdateCompletenessPresentation(null);
        step.State = "Preview stale";
        SetSummary("Source, route, ROI, output, or parameter changed. Preview again before Publish.");
    }

    public void MarkInputStaleIfNeeded(string? inputEntityId)
    {
        if (IsDisposed || previewOutput is null || isPreviewRunning || string.IsNullOrWhiteSpace(inputEntityId))
        {
            return;
        }

        var step = findStepByOutputEntityId(previewOutput.OutputEntityId);
        if (step is null || step.InputEntityIds.Count == 0
            || !string.Equals(step.InputEntityIds[0], inputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        UpdateCompletenessPresentation(null);
        step.State = "Preview stale";
        SetSummary("The Published input HeightField changed. Preview this measurement again before Publish.");
    }

    public void RefreshState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedPipelineStep() is { } step && isSelectedStepMeasurement()
            && (previewOutput is null || isPreviewStale)
            && !isPreviewRunning)
        {
            if (ToolRecipeHeightMeasurementExecution.TryPrepare(
                createDocument(),
                step.Id,
                GetCurrentCroppedHeightField(),
                GetCurrentTransformedHeightField(),
                GetCurrentEditableRegion(),
                GetRecipeDirectory(),
                out _,
                out var message))
            {
                step.State = "Ready";
                executionSummary = $"{step.ToolName} is ready for explicit Preview. It remains one composable recipe step.";
            }
            else
            {
                step.State = "Taught incomplete";
                executionSummary = message;
            }
        }

        onExecutionStateChanged();
    }

    private C3DTransformedHeightField? GetCurrentTransformedHeightField()
    {
        if (IsDisposed
            || getSelectedPipelineStep() is not { InputEntityIds.Count: > 0 } step
            || string.Equals(
                step.InputEntityIds[0],
                getSourceEntityId(),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return getPublishedHeightField(step.InputEntityIds[0]);
    }

    private C3DHeightFieldSnapshot? GetCurrentCroppedHeightField()
    {
        if (IsDisposed
            || getSelectedPipelineStep() is not { InputEntityIds.Count: > 0 } step
            || string.Equals(
                step.InputEntityIds[0],
                getSourceEntityId(),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return getPublishedCroppedHeightField(step.InputEntityIds[0]);
    }

    private C3DEditableRegionArtifact? GetCurrentEditableRegion()
    {
        if (IsDisposed
            || getSelectedPipelineStep() is not { } step
            || !string.Equals(step.ToolId, "completeness-grid", StringComparison.Ordinal)
            || step.InputEntityIds.Count < 3)
        {
            return null;
        }

        return getPublishedEditableRegion(step.InputEntityIds[2]);
    }

    private string? GetRecipeDirectory()
    {
        var recipePath = getRecipePath();
        return recipePath is null
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(recipePath));
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

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isPreviewRunning = value;
        onExecutionStateChanged();
    }

    private void UpdateCompletenessPresentation(
        ToolRecipeHeightMeasurementOutput? output)
    {
        if (IsDisposed)
        {
            return;
        }

        updateCompletenessPresentation(output);
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref previewCancellation),
            cancellation);

    private static void CancelAndDispose(CancellationTokenSource? currentCancellation)
    {
        if (currentCancellation is null)
        {
            return;
        }

        try
        {
            currentCancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }

        currentCancellation.Dispose();
    }
}
