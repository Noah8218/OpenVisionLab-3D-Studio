using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchEditableRegionExecutionOwner
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
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Select a published Connected Region, then choose its stable region index and Preview.";

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

    public bool IsSelectedStepEditableRegion => isSelectedStepEditableRegion();
    public bool IsPreviewRunning => isPreviewRunning;
    public bool HasCurrentPreview => preview is not null && !isPreviewStale;
    public bool IsPreviewStale => isPreviewStale;
    public bool IsPreviewPublished => isPreviewPublished;
    public C3DEditableRegionArtifact? CurrentArtifact => preview;
    public string? CurrentArtifactPath => previewPath;
    public string ExecutionSummary => executionSummary;

    internal C3DEditableRegionArtifact? TryGetPublishedArtifact(string entityId) =>
        isPreviewPublished
        && !isPreviewStale
        && preview is not null
        && string.Equals(preview.ArtifactId, entityId, StringComparison.OrdinalIgnoreCase)
            ? preview
            : null;

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview()
            || getSelectedPipelineStep() is not { } step
            || step.InputEntityIds.Count != 1
            || getPublishedConnectedRegion(step.InputEntityIds[0]) is not { } connected)
        {
            if (getSelectedPipelineStep() is { } waiting) waiting.State = "Taught incomplete";
            SetSummary("Editable Region requires a current Published ConnectedRegionArtifact input.");
            return false;
        }

        previewCancellation?.Dispose();
        previewCancellation = new CancellationTokenSource();
        isPreviewRunning = true;
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Editable Region Preview is selecting one connected region without changing its source artifact.");
        appendLog("Preview", $"Editable Region Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeEditableRegionExecution.Execute(
                    createDocument(),
                    step.Id,
                    connected,
                    previewCancellation.Token),
                previewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                preview = null;
                previewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Editable Region Preview failed: {evaluation.Result.Message}");
                return false;
            }

            preview = evaluation.Output;
            previewPath = null;
            step.State = "Preview ready";
            SetSummary(
                $"Preview ready | region {preview.RegionIndex} | cells {preview.Cells.Count:N0} | SHA-256 {preview.ContentSha256} | source unchanged");
            appendLog("Preview", $"Editable Region Preview ready: artifact={preview.ContentSha256}; region={preview.RegionIndex}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. Source, Connected Region, and recipe were not changed.");
            return false;
        }
        finally
        {
            isPreviewRunning = false;
            onExecutionStateChanged();
        }
    }

    public bool CanPreview() =>
        isSelectedStepEditableRegion()
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isPreviewRunning
        && getSelectedPipelineStep() is { } step
        && step.InputEntityIds.Count == 1
        && getPublishedConnectedRegion(step.InputEntityIds[0]) is not null
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void Publish()
    {
        if (getSelectedPipelineStep() is not { } step || !HasCurrentPreview) return;
        isPreviewPublished = true;
        step.State = "Published";
        SetSummary($"Published exact Editable Region Preview as {step.OutputEntityId} | SHA-256 {preview!.ContentSha256}.");
        appendLog("Publish", $"Editable Region artifact published without re-running: {step.OutputEntityId}.");
        PersistPublishedArtifactIfPossible();
    }

    public void Cancel() => previewCancellation?.Cancel();

    public void PersistPublishedArtifactIfPossible()
    {
        if (!isPreviewPublished || isPreviewStale || preview is null || string.IsNullOrWhiteSpace(getRecipePath())) return;
        try
        {
            previewPath = GetArtifactPath(getRecipePath()!, preview.ArtifactId);
            C3DEditableRegionArtifactStore.Save(previewPath, preview);
            appendLog("Save", $"Editable Region artifact sidecar saved: {previewPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            previewPath = null;
            SetSummary($"Published in this session, but the Editable Region sidecar could not be saved: {exception.Message}");
            appendLog("Error", $"Editable Region artifact sidecar save failed: {exception.Message}");
        }
    }

    public void RestorePublishedArtifact()
    {
        var recipePath = getRecipePath();
        var document = createDocument();
        var step = document.Steps.FirstOrDefault(candidate =>
            string.Equals(candidate.ToolId, "editable-region", StringComparison.Ordinal));
        if (step is null || string.IsNullOrWhiteSpace(recipePath)) return;
        var path = GetArtifactPath(recipePath, step.OutputEntityId);
        if (!File.Exists(path)) return;
        try
        {
            var artifact = C3DEditableRegionArtifactStore.Load(path);
            if (!MatchesCurrentRoute(step, artifact, out var reason))
            {
                SetSummary($"Saved Editable Region artifact was not restored: {reason}");
                appendLog("Warning", $"Editable Region artifact restore skipped: {reason}");
                return;
            }
            preview = artifact;
            previewPath = path;
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
            SetSummary($"Saved Editable Region artifact was not restored: {exception.Message}");
            appendLog("Warning", $"Editable Region artifact restore skipped: {exception.Message}");
        }
    }

    public void MarkStaleIfNeeded(object? sender)
    {
        if (preview is null || isPreviewRunning) return;
        var step = getSelectedPipelineStep();
        var isParameter = sender is ToolWorkbenchParameterItem parameter
            && (step?.Parameters.Contains(parameter) ?? false);
        if (step is { } selected
            && isSelectedStepEditableRegion()
            && (ReferenceEquals(sender, selected) || isParameter))
        {
            MarkStale(selected, "Editable Region route or SelectedRegionIndex changed. Preview again before Publish.");
        }
    }

    public void MarkStaleIfUpstreamChanged()
    {
        if (preview is null || isPreviewRunning || getSelectedPipelineStep() is not { } step || step.InputEntityIds.Count != 1) return;
        var connected = getPublishedConnectedRegion(step.InputEntityIds[0]);
        if (connected is null
            || !string.Equals(preview.SourceConnectedRegionContentSha256, connected.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            MarkStale(step, "Published Connected Region changed. Preview Editable Region again before Publish.");
        }
    }

    public void Clear(string summary)
    {
        previewCancellation?.Cancel();
        preview = null;
        previewPath = null;
        isPreviewStale = false;
        isPreviewPublished = false;
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (getSelectedPipelineStep() is { } step
            && IsSelectedStepEditableRegion
            && preview is null
            && !isPreviewRunning)
        {
            step.State = CanPreview() ? "Ready" : "Taught / needs correction";
        }
        onExecutionStateChanged();
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

    private void MarkStale(ToolWorkbenchPipelineStepItem step, string summary)
    {
        isPreviewStale = true;
        isPreviewPublished = false;
        step.State = "Preview stale";
        SetSummary(summary);
    }

    private static string GetArtifactPath(string recipePath, string artifactId)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(recipePath)) ?? Environment.CurrentDirectory;
        var stem = Path.GetFileNameWithoutExtension(recipePath);
        var safeId = string.Concat(artifactId.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        return Path.Combine(directory, $"{stem}.editable-region.{safeId}.json");
    }

    private void SetSummary(string value)
    {
        executionSummary = value;
        onExecutionStateChanged();
    }
}
