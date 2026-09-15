using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
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
    private readonly object previewStateGate = new();

    private CancellationTokenSource? previewCancellation;
    private ToolRecipeHeightMeasurementOutput? previewOutput;
    private string? previewExecutionFingerprint;
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
    public bool HasCurrentPreview => !IsDisposed
        && !isPreviewRunning
        && previewOutput is not null
        && previewExecutionFingerprint is not null
        && !isPreviewStale
        && IsCurrentExecutionSnapshot(previewExecutionFingerprint);
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
        previewExecutionFingerprint = null;
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
            TryShowPreviewCanceled(currentCancellation, step);
            return false;
        }

        appendLog("Preview", $"{step.ToolName} Preview started: {step.Id}.");
        try
        {
            var documentSnapshot = createDocument();
            var stepIdSnapshot = step.Id;
            var inputEntityIdsSnapshot = step.InputEntityIds;
            var sourceEntityIdSnapshot = getSourceEntityId();
            var croppedHeightFieldSnapshot = GetCurrentCroppedHeightField(
                inputEntityIdsSnapshot,
                sourceEntityIdSnapshot);
            var transformedHeightFieldSnapshot = GetCurrentTransformedHeightField(
                inputEntityIdsSnapshot,
                sourceEntityIdSnapshot);
            var editableRegionSnapshot = GetCurrentEditableRegion(
                step.ToolId,
                inputEntityIdsSnapshot);
            var recipeDirectorySnapshot = GetRecipeDirectory(getRecipePath());
            var pendingStepParameterChangesSnapshot = hasPendingStepParameterChanges();
            var executionFingerprint = CreateExecutionFingerprint(
                documentSnapshot,
                stepIdSnapshot,
                inputEntityIdsSnapshot,
                sourceEntityIdSnapshot,
                croppedHeightFieldSnapshot,
                transformedHeightFieldSnapshot,
                editableRegionSnapshot,
                recipeDirectorySnapshot,
                pendingStepParameterChangesSnapshot);
            var evaluation = await Task.Run(
                () => ToolRecipeHeightMeasurementExecution.Execute(
                    documentSnapshot,
                    stepIdSnapshot,
                    croppedHeightFieldSnapshot,
                    transformedHeightFieldSnapshot,
                    editableRegionSnapshot,
                    recipeDirectorySnapshot,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                TryShowPreviewCanceled(currentCancellation, step);
                return false;
            }

            if (!IsCurrentExecutionSnapshot(executionFingerprint))
            {
                TryShowPreviewStale(currentCancellation, step);
                return false;
            }

            if (evaluation.Output is null || evaluation.Result.Status == ResultStatus.Error)
            {
                if (!TryCommitCurrentPreview(
                        currentCancellation,
                        () =>
                        {
                            previewOutput = null;
                            previewExecutionFingerprint = null;
                            UpdateCompletenessPresentation(null);
                            step.State = "Error";
                            SetSummary(evaluation.Result.Message);
                            appendLog("Error", $"{step.ToolName} Preview failed: {evaluation.Result.Message}");
                        }))
                {
                    TryShowPreviewCanceled(currentCancellation, step);
                    return false;
                }

                return false;
            }

            if (!TryCommitCurrentPreview(
                    currentCancellation,
                    () =>
                    {
                        previewOutput = evaluation.Output;
                        previewExecutionFingerprint = executionFingerprint;
                        UpdateCompletenessPresentation(previewOutput);
                        step.State = "Preview ready";
                        SetSummary($"Preview ready | {previewOutput!.EvidenceSummary} | {evaluation.Result.Status} | declared source units only.");
                        appendLog("Preview", $"{step.ToolName} Preview ready: {previewOutput.ContentSha256}.");
                    },
                    executionFingerprint))
            {
                if (!TryShowPreviewCanceled(currentCancellation, step))
                {
                    TryShowPreviewStale(currentCancellation, step);
                }

                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            TryShowPreviewCanceled(currentCancellation, step);
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

    public bool CanPublish()
    {
        if (IsDisposed
            || isPreviewRunning
            || getSelectedPipelineStep() is not { }
            || !HasCurrentPreview
            || previewExecutionFingerprint is null)
        {
            return false;
        }

        return IsCurrentExecutionSnapshot(previewExecutionFingerprint);
    }

    public void Publish()
    {
        if (IsDisposed
            || isPreviewRunning
            || getSelectedPipelineStep() is not { } step
            || previewOutput is null
            || previewExecutionFingerprint is null
            || isPreviewStale)
        {
            return;
        }

        if (!IsCurrentExecutionSnapshot(previewExecutionFingerprint))
        {
            var previewStep = findStepByOutputEntityId(previewOutput.OutputEntityId) ?? step;
            MarkCurrentPreviewStale(previewStep);
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
            lock (previewStateGate)
            {
                Volatile.Read(ref previewCancellation)?.Cancel();
            }
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
        previewExecutionFingerprint = null;
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

        MarkCurrentPreviewStale(step);
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

        MarkCurrentPreviewStale(
            step,
            "The Published input HeightField changed. Preview this measurement again before Publish.");
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

    private C3DTransformedHeightField? GetCurrentTransformedHeightField() =>
        getSelectedPipelineStep() is { } step
            ? GetCurrentTransformedHeightField(step.InputEntityIds, getSourceEntityId())
            : null;

    private C3DTransformedHeightField? GetCurrentTransformedHeightField(
        IReadOnlyList<string> inputEntityIds,
        string sourceEntityId)
    {
        if (IsDisposed
            || inputEntityIds.Count == 0
            || string.Equals(
                inputEntityIds[0],
                sourceEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return getPublishedHeightField(inputEntityIds[0]);
    }

    private C3DHeightFieldSnapshot? GetCurrentCroppedHeightField() =>
        getSelectedPipelineStep() is { } step
            ? GetCurrentCroppedHeightField(step.InputEntityIds, getSourceEntityId())
            : null;

    private C3DHeightFieldSnapshot? GetCurrentCroppedHeightField(
        IReadOnlyList<string> inputEntityIds,
        string sourceEntityId)
    {
        if (IsDisposed
            || inputEntityIds.Count == 0
            || string.Equals(
                inputEntityIds[0],
                sourceEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return getPublishedCroppedHeightField(inputEntityIds[0]);
    }

    private C3DEditableRegionArtifact? GetCurrentEditableRegion() =>
        getSelectedPipelineStep() is { } step
            ? GetCurrentEditableRegion(step.ToolId, step.InputEntityIds)
            : null;

    private C3DEditableRegionArtifact? GetCurrentEditableRegion(
        string toolId,
        IReadOnlyList<string> inputEntityIds)
    {
        if (IsDisposed
            || !string.Equals(toolId, "completeness-grid", StringComparison.Ordinal)
            || inputEntityIds.Count < 3)
        {
            return null;
        }

        return getPublishedEditableRegion(inputEntityIds[2]);
    }

    private string? GetRecipeDirectory() => GetRecipeDirectory(getRecipePath());

    private static string? GetRecipeDirectory(string? recipePath)
    {
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

    private bool IsCurrentExecutionSnapshot(string expectedFingerprint)
    {
        if (IsDisposed)
        {
            return false;
        }

        var pendingStepParameterChanges = hasPendingStepParameterChanges();
        if (pendingStepParameterChanges || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var inputEntityIds = step.InputEntityIds;
        var sourceEntityId = getSourceEntityId();
        var currentFingerprint = CreateExecutionFingerprint(
            createDocument(),
            step.Id,
            inputEntityIds,
            sourceEntityId,
            GetCurrentCroppedHeightField(inputEntityIds, sourceEntityId),
            GetCurrentTransformedHeightField(inputEntityIds, sourceEntityId),
            GetCurrentEditableRegion(step.ToolId, inputEntityIds),
            GetRecipeDirectory(getRecipePath()),
            pendingStepParameterChanges);
        return string.Equals(expectedFingerprint, currentFingerprint, StringComparison.Ordinal);
    }

    private static string CreateExecutionFingerprint(
        ToolRecipeDocument document,
        string stepId,
        IReadOnlyList<string> inputEntityIds,
        string sourceEntityId,
        C3DHeightFieldSnapshot? croppedHeightField,
        C3DTransformedHeightField? transformedHeightField,
        C3DEditableRegionArtifact? editableRegion,
        string? recipeDirectory,
        bool hasPendingStepParameterChanges)
    {
        var canonical = new StringBuilder();
        AppendFingerprintValue(canonical, JsonSerializer.Serialize(document));
        AppendFingerprintValue(canonical, stepId);
        AppendFingerprintValue(canonical, string.Join(";", inputEntityIds));
        AppendFingerprintValue(canonical, sourceEntityId);
        AppendFingerprintValue(canonical, recipeDirectory);
        AppendFingerprintValue(canonical, hasPendingStepParameterChanges ? "pending" : "applied");
        AppendFingerprintValue(canonical, croppedHeightField?.EntityId);
        AppendFingerprintValue(canonical, croppedHeightField?.ContentSha256);
        AppendFingerprintValue(canonical, croppedHeightField?.RootSourceSha256);
        AppendFingerprintValue(canonical, croppedHeightField is null ? null : $"{croppedHeightField.Width}x{croppedHeightField.Height}");
        AppendFingerprintValue(canonical, transformedHeightField?.OutputEntityId);
        AppendFingerprintValue(canonical, transformedHeightField?.ContentSha256);
        AppendFingerprintValue(canonical, transformedHeightField?.RootSourceSha256);
        AppendFingerprintValue(canonical, transformedHeightField?.SourceContentSha256);
        AppendFingerprintValue(canonical, transformedHeightField is null ? null : $"{transformedHeightField.RowCount}x{transformedHeightField.ColumnCount}");
        AppendFingerprintValue(canonical, editableRegion?.ArtifactId);
        AppendFingerprintValue(canonical, editableRegion?.ContentSha256);
        AppendFingerprintValue(canonical, editableRegion?.SourceEntityId);
        AppendFingerprintValue(canonical, editableRegion?.SourceContentSha256);
        AppendFingerprintValue(canonical, editableRegion is null ? null : $"{editableRegion.GridWidth}x{editableRegion.GridHeight}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void AppendFingerprintValue(StringBuilder canonical, string? value)
    {
        canonical.Append(value?.Length ?? -1).Append(':').Append(value).Append('|');
    }

    private bool TryCommitCurrentPreview(
        CancellationTokenSource cancellation,
        Action commit,
        string? expectedFingerprint = null)
    {
        lock (previewStateGate)
        {
            if (!IsCurrentPreview(cancellation)
                || (expectedFingerprint is not null
                    && !IsCurrentExecutionSnapshot(expectedFingerprint)))
            {
                return false;
            }

            commit();
            return IsCurrentPreview(cancellation);
        }
    }

    private bool TryShowPreviewCanceled(
        CancellationTokenSource cancellation,
        ToolWorkbenchPipelineStepItem step)
    {
        lock (previewStateGate)
        {
            if (!OwnsPreview(cancellation) || !cancellation.IsCancellationRequested)
            {
                return false;
            }

            previewOutput = null;
            previewExecutionFingerprint = null;
            UpdateCompletenessPresentation(null);
            step.State = "Ready";
            SetSummary("Preview canceled. The source, ROI, and authored recipe were not changed.");
            appendLog("Preview", $"{step.ToolName} Preview canceled.");
            return true;
        }
    }

    private bool TryShowPreviewStale(
        CancellationTokenSource cancellation,
        ToolWorkbenchPipelineStepItem step)
    {
        lock (previewStateGate)
        {
            if (!OwnsPreview(cancellation))
            {
                return false;
            }

            previewOutput = null;
            previewExecutionFingerprint = null;
            isPreviewStale = true;
            isPreviewPublished = false;
            UpdateCompletenessPresentation(null);
            step.State = "Preview stale";
            SetSummary("Preview discarded because the recipe, selection, ROI, source, or upstream input changed after its start snapshot. Preview again before Publish.");
            appendLog("Preview", $"{step.ToolName} Preview discarded because its start snapshot is no longer current.");
            return true;
        }
    }

    private bool OwnsPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref previewCancellation),
            cancellation);

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        OwnsPreview(cancellation) && !cancellation.IsCancellationRequested;

    private void MarkCurrentPreviewStale(
        ToolWorkbenchPipelineStepItem step,
        string summary = "Source, route, ROI, output, or parameter changed. Preview again before Publish.")
    {
        isPreviewStale = true;
        isPreviewPublished = false;
        previewExecutionFingerprint = null;
        UpdateCompletenessPresentation(null);
        step.State = "Preview stale";
        SetSummary(summary);
    }

}
