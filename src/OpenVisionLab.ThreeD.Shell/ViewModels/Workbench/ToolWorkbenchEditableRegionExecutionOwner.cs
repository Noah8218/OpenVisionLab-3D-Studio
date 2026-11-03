using System.Globalization;
using System.IO;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the lifetime of the Editable Region preview and its published
/// sidecar. The ViewModel remains a composition facade; execution, stale
/// state, cancellation, and artifact cleanup have one independently testable
/// owner.
/// </summary>
internal sealed class ToolWorkbenchEditableRegionExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelectedStepEditableRegion;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string, C3DConnectedRegionArtifact?> getPublishedConnectedRegion;
    private readonly Action<string, string> appendLog;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DEditableRegionArtifact? preview;
    private string? previewPath;
    private string? previewProducerStepId;
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Select a published Connected Region, then choose its stable region index and Preview.";
    private int disposalState;

    public ToolWorkbenchEditableRegionExecutionOwner(
        Func<bool> isSelectedStepEditableRegion,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<string, C3DConnectedRegionArtifact?> getPublishedConnectedRegion,
        Action<string, string> appendLog,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepEditableRegion = isSelectedStepEditableRegion;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.getPublishedConnectedRegion = getPublishedConnectedRegion;
        this.appendLog = appendLog;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;
    public bool IsSelectedStepEditableRegion => !IsDisposed && isSelectedStepEditableRegion();
    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed && preview is not null && !isPreviewStale;
    public bool IsPreviewStale => !IsDisposed && isPreviewStale;
    public bool IsPreviewPublished => !IsDisposed && isPreviewPublished;
    public C3DEditableRegionArtifact? CurrentArtifact => IsDisposed ? null : preview;
    public string? CurrentArtifactPath => IsDisposed ? null : previewPath;
    public string ExecutionSummary => IsDisposed
        ? "Editable Region execution owner has been disposed."
        : executionSummary;

    internal C3DEditableRegionArtifact? TryGetPublishedArtifact(string entityId)
    {
        if (IsDisposed)
        {
            return null;
        }

        if (isPreviewPublished && !isPreviewStale && preview is { } current
            && string.Equals(current.ArtifactId, entityId, StringComparison.OrdinalIgnoreCase))
            MarkStaleIfUpstreamChanged();
        return isPreviewPublished
            && !isPreviewStale
            && preview is not null
            && string.Equals(preview.ArtifactId, entityId, StringComparison.OrdinalIgnoreCase)
            ? preview
            : null;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            if (!IsDisposed)
            {
                if (getSelectedPipelineStep() is { } waiting)
                {
                    waiting.State = "Taught incomplete";
                }

                SetSummary("Editable Region requires a current Published ConnectedRegionArtifact input.");
            }

            return false;
        }

        var connected = getPublishedConnectedRegion(step.InputEntityIds[0]);
        if (connected is null)
        {
            SetSummary("Editable Region requires a current Published ConnectedRegionArtifact input.");
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
        SetSummary("Editable Region Preview is selecting one connected region without changing its source artifact.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"Editable Region Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeEditableRegionExecution.Execute(
                    createDocument(),
                    step.Id,
                    connected,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                preview = null;
                previewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (IsCurrentPreview(currentCancellation))
                {
                    appendLog("Error", $"Editable Region Preview failed: {evaluation.Result.Message}");
                }

                return false;
            }

            preview = evaluation.Output;
            previewPath = null;
            previewProducerStepId = step.Id;
            step.State = "Preview ready";
            SetSummary(
                $"Preview ready | region {preview.RegionIndex} | cells {preview.Cells.Count:N0} | SHA-256 {preview.ContentSha256} | source unchanged");
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            appendLog("Preview", $"Editable Region Preview ready: artifact={preview.ContentSha256}; region={preview.RegionIndex}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetSummary("Preview canceled. Source, Connected Region, and recipe were not changed.");
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
        if (IsDisposed || !IsSelectedStepEditableRegion || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges() || isPreviewRunning
            || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        return step.InputEntityIds.Count == 1
            && getPublishedConnectedRegion(step.InputEntityIds[0]) is not null
            && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;
    }

    public void Publish()
    {
        MarkStaleIfUpstreamChanged();
        if (IsDisposed || isPreviewRunning || getSelectedPipelineStep() is not { } step || !HasCurrentPreview
            || !string.Equals(step.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(step.OutputEntityId, preview!.ArtifactId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        isPreviewPublished = true;
        step.State = "Published";
        SetSummary($"Published exact Editable Region Preview as {step.OutputEntityId} | SHA-256 {preview!.ContentSha256}.");
        if (IsDisposed)
        {
            return;
        }

        appendLog("Publish", $"Editable Region artifact published without re-running: {step.OutputEntityId}.");
        PersistPublishedArtifactIfPossible();
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

    public void PersistPublishedArtifactIfPossible()
    {
        MarkStaleIfUpstreamChanged();
        if (IsDisposed || !isPreviewPublished || isPreviewStale || preview is null)
        {
            return;
        }

        var recipePath = getRecipePath();
        if (string.IsNullOrWhiteSpace(recipePath))
        {
            return;
        }

        var artifact = preview;
        var artifactPath = GetArtifactPath(recipePath, artifact.ArtifactId);
        try
        {
            C3DEditableRegionArtifactStore.Save(artifactPath, artifact);
            if (IsDisposed)
            {
                return;
            }

            previewPath = artifactPath;
            appendLog("Save", $"Editable Region artifact sidecar saved: {artifactPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            if (IsDisposed)
            {
                return;
            }

            previewPath = null;
            SetSummary($"Published in this session, but the Editable Region sidecar could not be saved: {exception.Message}");
            appendLog("Error", $"Editable Region artifact sidecar save failed: {exception.Message}");
        }
    }

    public void RestorePublishedArtifact()
    {
        if (IsDisposed)
        {
            return;
        }

        var recipePath = getRecipePath();
        if (string.IsNullOrWhiteSpace(recipePath))
        {
            return;
        }

        var document = createDocument();
        var step = document.Steps.FirstOrDefault(candidate =>
            string.Equals(candidate.ToolId, "editable-region", StringComparison.Ordinal));
        if (step is null)
        {
            return;
        }

        var path = GetArtifactPath(recipePath, step.OutputEntityId);
        if (!File.Exists(path))
        {
            var legacyId = SanitizeLegacy(step.OutputEntityId);
            if (document.Steps.Count(candidate =>
                    string.Equals(candidate.ToolId, "editable-region", StringComparison.Ordinal)
                    && string.Equals(SanitizeLegacy(candidate.OutputEntityId), legacyId, StringComparison.OrdinalIgnoreCase)) != 1)
            {
                appendLog("Warning", "Editable Region legacy artifact restore skipped: ambiguous output filename.");
                return;
            }
            path = GetArtifactPath(recipePath, step.OutputEntityId, legacy: true);
        }
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var artifact = C3DEditableRegionArtifactStore.Load(path);
            if (IsDisposed)
            {
                return;
            }

            if (!MatchesCurrentRoute(step, artifact, out var reason))
            {
                SetSummary($"Saved Editable Region artifact was not restored: {reason}");
                appendLog("Warning", $"Editable Region artifact restore skipped: {reason}");
                return;
            }

            preview = artifact;
            previewPath = path;
            previewProducerStepId = step.Id;
            isPreviewStale = false;
            isPreviewPublished = true;
            if (getSelectedPipelineStep() is { } selected
                && string.Equals(selected.Id, step.Id, StringComparison.OrdinalIgnoreCase))
            {
                selected.State = "Published";
            }

            SetSummary($"Restored Published Editable Region artifact {artifact.ArtifactId} without executing the tool.");
            appendLog("Open", $"Editable Region artifact restored: {path}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            if (IsDisposed)
            {
                return;
            }

            SetSummary($"Saved Editable Region artifact was not restored: {exception.Message}");
            appendLog("Warning", $"Editable Region artifact restore skipped: {exception.Message}");
        }
    }

    public void MarkStaleIfNeeded(object? sender)
    {
        if (IsDisposed || preview is null || isPreviewRunning || isPreviewStale)
        {
            return;
        }

        var step = getSelectedPipelineStep();
        var isParameter = sender is ToolWorkbenchParameterItem parameter
            && string.Equals(step?.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            && (step?.Parameters.Contains(parameter) ?? false);
        if (sender is ToolWorkbenchPipelineStepItem changed
                && string.Equals(changed.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            || isParameter)
        {
            MarkStale("Editable Region route or SelectedRegionIndex changed. Preview again before Publish.");
        }
    }

    public void MarkStaleIfUpstreamChanged()
    {
        if (IsDisposed || preview is null || isPreviewRunning || isPreviewStale)
        {
            return;
        }

        var document = createDocument();
        var producers = document.Steps.Where(step =>
            string.Equals(step.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(step.ToolId, "editable-region", StringComparison.Ordinal)
            && string.Equals(step.OutputEntityId, preview.ArtifactId, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (producers.Length != 1
            || !ToolRecipeValidator.ValidateForStepExecution(document, producers[0].Id).IsValid
            || !MatchesCurrentRoute(producers[0], preview, out _))
        {
            MarkStale("Published Connected Region changed. Preview Editable Region again before Publish.");
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
        preview = null;
        previewPath = null;
        previewProducerStepId = null;
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedPipelineStep() is { } step
            && IsSelectedStepEditableRegion
            && preview is null
            && !isPreviewRunning)
        {
            step.State = CanPreview() ? "Ready" : "Taught / needs correction";
        }

        if (!IsDisposed)
        {
            onExecutionStateChanged();
        }
    }

    private bool MatchesCurrentRoute(
        ToolRecipeStep step,
        C3DEditableRegionArtifact artifact,
        out string reason)
    {
        reason = string.Empty;
        var connected = step.InputEntityIds is { Count: 1 }
            ? getPublishedConnectedRegion(step.InputEntityIds[0])
            : null;
        if (connected is null
            || !string.Equals(artifact.ArtifactId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.SourceConnectedRegionArtifactId, connected.ArtifactId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.SourceConnectedRegionContentSha256, connected.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            reason = "artifact and current Connected Region identities do not match.";
            return false;
        }

        if (!int.TryParse(
                step.Parameters?.SingleOrDefault(parameter => parameter.Name == ToolRecipeEditableRegionExecution.SelectedRegionIndexParameter)?.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var index)
            || index != artifact.RegionIndex)
        {
            reason = "artifact SelectedRegionIndex does not match the current recipe.";
            return false;
        }

        return true;
    }

    private void MarkStale(string summary)
    {
        if (IsDisposed || isPreviewStale)
        {
            return;
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        // Commit the transition before callbacks can re-enter freshness checks.
        if (getSelectedPipelineStep() is { } step
            && string.Equals(step.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase))
            step.State = "Preview stale";
        SetSummary(summary);
    }

    private static string GetArtifactPath(string recipePath, string artifactId, bool legacy = false)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(recipePath)) ?? Environment.CurrentDirectory;
        var stem = Path.GetFileNameWithoutExtension(recipePath);
        var safeId = legacy ? SanitizeLegacy(artifactId) : Sanitize(artifactId);
        return Path.Combine(directory, $"{stem}.editable-region.{safeId}.json");
    }

    // Ordinal-ignore-case logical identity, encoded as exact UTF-16LE code units.
    // '~' cannot occur in a legacy sanitized ID, so new writes cannot alias old paths.
    private static string Sanitize(string value)
    {
        var canonical = value.ToUpperInvariant();
        var bytes = new byte[checked(canonical.Length * 2)];
        for (var index = 0; index < canonical.Length; index++)
        {
            bytes[index * 2] = (byte)(canonical[index] & 0xff);
            bytes[index * 2 + 1] = (byte)(canonical[index] >> 8);
        }
        return "v2~" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private static string SanitizeLegacy(string value) =>
        string.Concat(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

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
        preview = null;
        previewPath = null;
        previewProducerStepId = null;
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref previewCancellation),
            cancellation);

}
