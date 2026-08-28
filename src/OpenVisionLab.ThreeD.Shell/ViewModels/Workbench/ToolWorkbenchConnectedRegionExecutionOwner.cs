using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchConnectedRegionExecutionOwner
{
    private readonly Func<bool> isSelectedStepConnectedRegion;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string, (C3DHeightFieldSnapshot Output, C3DOutlierCellMap Mask)?> getPublishedRemoveOutlierInput;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? connectedRegionPreviewCancellation;
    private C3DConnectedRegionArtifact? connectedRegionPreview;
    private (C3DHeightFieldSnapshot Output, C3DOutlierCellMap Mask)? restoredUpstream;
    private string? connectedRegionPreviewPath;
    private bool isConnectedRegionPreviewRunning;
    private bool isConnectedRegionPreviewStale;
    private bool isConnectedRegionPreviewPublished;
    private string connectedRegionExecutionSummary =
        "Select Connected Region after publishing Remove Outlier Pixels, then Preview.";

    public ToolWorkbenchConnectedRegionExecutionOwner(
        Func<bool> isSelectedStepConnectedRegion,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<string, (C3DHeightFieldSnapshot Output, C3DOutlierCellMap Mask)?> getPublishedRemoveOutlierInput,
        Func<object?, bool> isSourceChangeEvent,
        Action<string, string> appendLog,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepConnectedRegion = isSelectedStepConnectedRegion;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.getPublishedRemoveOutlierInput = getPublishedRemoveOutlierInput;
        this.isSourceChangeEvent = isSourceChangeEvent;
        this.appendLog = appendLog;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStepConnectedRegion => isSelectedStepConnectedRegion();
    public bool IsConnectedRegionPreviewRunning => isConnectedRegionPreviewRunning;
    public bool HasCurrentConnectedRegionPreview =>
        connectedRegionPreview is not null && !isConnectedRegionPreviewStale;
    public bool IsConnectedRegionPreviewStale => isConnectedRegionPreviewStale;
    public bool IsConnectedRegionPreviewPublished => isConnectedRegionPreviewPublished;
    public C3DConnectedRegionArtifact? CurrentConnectedRegionArtifact => connectedRegionPreview;
    public string? CurrentConnectedRegionArtifactPath => connectedRegionPreviewPath;
    public string ConnectedRegionExecutionSummary => connectedRegionExecutionSummary;

    internal C3DConnectedRegionArtifact? TryGetPublishedArtifact(string entityId)
    {
        return isConnectedRegionPreviewPublished
            && !isConnectedRegionPreviewStale
            && connectedRegionPreview is { } artifact
            && string.Equals(artifact.ArtifactId, entityId, StringComparison.OrdinalIgnoreCase)
            ? artifact
            : null;
    }

    internal (C3DHeightFieldSnapshot Output, C3DOutlierCellMap Mask)? TryGetRestoredUpstreamInput(
        string entityId)
    {
        if (restoredUpstream is not { } upstream
            || !string.Equals(upstream.Output.EntityId, entityId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return upstream;
    }

    public async Task<bool> PreviewSelectedConnectedRegionAsync()
    {
        if (!CanPreviewSelectedConnectedRegion()
            || getSelectedPipelineStep() is not { } step
            || step.InputEntityIds.Count != 1
            || TryGetUpstream(step.InputEntityIds[0]) is not { } upstream)
        {
            return false;
        }

        connectedRegionPreviewCancellation?.Dispose();
        connectedRegionPreviewCancellation = new CancellationTokenSource();
        isConnectedRegionPreviewRunning = true;
        isConnectedRegionPreviewStale = false;
        isConnectedRegionPreviewPublished = false;
        step.State = "Preview running";
        SetSummary(
            "Connected Region Preview is labeling the exact published outlier mask without changing source data.");
        appendLog("Preview", $"Connected Region Preview started: {step.Id}.");

        try
        {
            var document = createDocument();
            var evaluation = await Task.Run(
                () => ToolRecipeConnectedRegionExecution.Execute(
                    document,
                    step.Id,
                    upstream.Output,
                    upstream.Mask,
                    connectedRegionPreviewCancellation.Token),
                connectedRegionPreviewCancellation.Token);

            if (evaluation.Result.Status != ResultStatus.Pass
                || evaluation.Output is null)
            {
                connectedRegionPreview = null;
                connectedRegionPreviewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Connected Region Preview failed: {evaluation.Result.Message}");
                return false;
            }

            connectedRegionPreview = evaluation.Output;
            connectedRegionPreviewPath = null;
            step.State = "Preview ready";
            SetSummary(
                $"Preview ready | regions {evaluation.Output.Regions.Count:N0} | artifact SHA-256 {evaluation.Output.ContentSha256} | source unchanged");
            appendLog(
                "Preview",
                $"Connected Region Preview ready: artifact={evaluation.Output.ContentSha256}; regions={evaluation.Output.Regions.Count}; mask={evaluation.Output.MaskContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. Source, published upstream, and authored recipe were not changed.");
            appendLog("Preview", "Connected Region Preview canceled.");
            return false;
        }
        finally
        {
            isConnectedRegionPreviewRunning = false;
            onExecutionStateChanged();
        }
    }

    public bool CanPreviewSelectedConnectedRegion() =>
        isSelectedStepConnectedRegion()
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isConnectedRegionPreviewRunning
        && getSelectedPipelineStep() is { } step
        && step.InputEntityIds.Count == 1
        && TryGetUpstream(step.InputEntityIds[0]) is not null
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void PublishSelectedConnectedRegion()
    {
        if (getSelectedPipelineStep() is not { } step
            || !HasCurrentConnectedRegionPreview)
        {
            return;
        }

        isConnectedRegionPreviewPublished = true;
        step.State = "Published";
        SetSummary(
            $"Published artifact {step.OutputEntityId} | SHA-256 {connectedRegionPreview!.ContentSha256} | source and upstream mask unchanged");
        appendLog(
            "Publish",
            $"Connected Region artifact published without re-running: {step.OutputEntityId}.");
        PersistPublishedArtifactIfPossible();
    }

    public void PersistPublishedArtifactIfPossible()
    {
        if (!isConnectedRegionPreviewPublished
            || isConnectedRegionPreviewStale
            || connectedRegionPreview is null
            || string.IsNullOrWhiteSpace(getRecipePath()))
        {
            return;
        }

        try
        {
            connectedRegionPreviewPath = GetArtifactPath(getRecipePath()!, connectedRegionPreview.ArtifactId);
            C3DConnectedRegionArtifactStore.Save(connectedRegionPreviewPath, connectedRegionPreview);
            appendLog("Save", $"Connected Region artifact sidecar saved: {connectedRegionPreviewPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            connectedRegionPreviewPath = null;
            SetSummary(
                $"Published in this session, but the Connected Region artifact sidecar could not be saved: {exception.Message}");
            appendLog("Error", $"Connected Region artifact sidecar save failed: {exception.Message}");
        }
    }

    public void RestorePublishedConnectedRegionArtifact()
    {
        var recipePath = getRecipePath();
        var document = createDocument();
        var recipeStep = document.Steps.FirstOrDefault(candidate =>
            string.Equals(candidate.ToolId, "connected-region", StringComparison.Ordinal));
        if (recipeStep is null || string.IsNullOrWhiteSpace(recipePath))
        {
            return;
        }

        var artifactPath = GetArtifactPath(recipePath, recipeStep.OutputEntityId);
        if (!File.Exists(artifactPath))
        {
            return;
        }

        try
        {
            var artifact = C3DConnectedRegionArtifactStore.Load(artifactPath);
            if (!TryReconstructUpstream(document, recipeStep, artifact, out var upstream, out var reason))
            {
                SetSummary($"Saved Connected Region artifact was not restored: {reason}");
                appendLog("Warning", $"Connected Region artifact restore skipped: {reason}");
                return;
            }

            restoredUpstream = upstream;
            connectedRegionPreview = artifact;
            connectedRegionPreviewPath = artifactPath;
            isConnectedRegionPreviewStale = false;
            isConnectedRegionPreviewPublished = true;
            if (getSelectedPipelineStep() is { } selected
                && string.Equals(selected.Id, recipeStep.Id, StringComparison.OrdinalIgnoreCase))
            {
                selected.State = "Published";
            }
            SetSummary(
                $"Restored Published Connected Region artifact {artifact.ArtifactId} from sidecar without executing the tool.");
            appendLog("Open", $"Connected Region artifact restored: {artifactPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            SetSummary($"Saved Connected Region artifact was not restored: {exception.Message}");
            appendLog("Warning", $"Connected Region artifact restore skipped: {exception.Message}");
        }
    }

    public void CancelConnectedRegionPreview() =>
        connectedRegionPreviewCancellation?.Cancel();

    public void MarkConnectedRegionPreviewStaleIfNeeded(object? sender)
    {
        if (connectedRegionPreview is null || isConnectedRegionPreviewRunning)
        {
            return;
        }

        var selected = getSelectedPipelineStep();
        var selectedIsConnectedRegion = IsSelectedStepConnectedRegion;
        var isSelectedParameter = selectedIsConnectedRegion
            && sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        if (selected is { } selectedStep
            && (isSourceChangeEvent(sender)
                || ReferenceEquals(sender, selectedStep)
                || isSelectedParameter))
        {
            MarkStale(selectedStep,
                "Source, routing, output, or Connected Region parameters changed. Preview again before Publish.");
        }
    }

    public void MarkStaleIfUpstreamChanged()
    {
        if (connectedRegionPreview is null
            || isConnectedRegionPreviewRunning
            || getSelectedPipelineStep() is not { } step
            || step.InputEntityIds.Count != 1)
        {
            return;
        }

        var upstream = TryGetUpstream(step.InputEntityIds[0]);
        if (upstream is null
            || !string.Equals(
                connectedRegionPreview.SourceContentSha256,
                upstream.Value.Output.ContentSha256,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                connectedRegionPreview.MaskContentSha256,
                upstream.Value.Mask.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            MarkStale(step, "Published upstream output or outlier mask changed. Preview again before Publish.");
        }
    }

    public void ClearConnectedRegionPreview(string summary)
    {
        connectedRegionPreviewCancellation?.Cancel();
        connectedRegionPreview = null;
        restoredUpstream = null;
        connectedRegionPreviewPath = null;
        isConnectedRegionPreviewStale = false;
        isConnectedRegionPreviewPublished = false;
        SetSummary(summary);
    }

    public void RefreshConnectedRegionExecutionState()
    {
        if (getSelectedPipelineStep() is { } step
            && IsSelectedStepConnectedRegion
            && connectedRegionPreview is null
            && !isConnectedRegionPreviewRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = CanPreviewSelectedConnectedRegion()
                ? "Ready"
                : "Taught / needs correction";
        }

        onExecutionStateChanged();
    }

    public void SetConnectedRegionRunning(bool value)
    {
        isConnectedRegionPreviewRunning = value;
        onExecutionStateChanged();
    }

    private (C3DHeightFieldSnapshot Output, C3DOutlierCellMap Mask)? TryGetUpstream(
        string entityId) =>
        getPublishedRemoveOutlierInput(entityId) ?? TryGetRestoredUpstreamInput(entityId);

    private void MarkStale(ToolWorkbenchPipelineStepItem step, string summary)
    {
        isConnectedRegionPreviewStale = true;
        isConnectedRegionPreviewPublished = false;
        step.State = "Preview stale";
        SetSummary(summary);
    }

    private bool TryReconstructUpstream(
        ToolRecipeDocument document,
        ToolRecipeStep step,
        C3DConnectedRegionArtifact artifact,
        out (C3DHeightFieldSnapshot Output, C3DOutlierCellMap Mask) upstream,
        out string reason)
    {
        upstream = default;
        reason = string.Empty;
        if (step.InputEntityIds.Count != 1
            || !string.Equals(artifact.ArtifactId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.SourceEntityId, step.InputEntityIds[0], StringComparison.OrdinalIgnoreCase))
        {
            reason = "artifact and recipe entity identities do not match.";
            return false;
        }

        if (document.Source is not
            {
                Path: { Length: > 0 } sourcePath,
                ByteLength: { } byteLength,
                ContentSha256: { Length: > 0 } contentSha256,
                GridWidth: { } gridWidth,
                GridHeight: { } gridHeight
            })
        {
            reason = "recipe source identity is incomplete.";
            return false;
        }

        var source = C3DHeightFieldSnapshot.LoadVerified(
            sourcePath,
            document.Source.Id,
            document.Source.Unit,
            document.Source.FrameId,
            byteLength,
            contentSha256,
            gridWidth,
            gridHeight);
        if (!string.Equals(artifact.RootSourceSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || artifact.GridWidth != source.Width
            || artifact.GridHeight != source.Height
            || !string.Equals(artifact.Unit, source.Unit, StringComparison.Ordinal)
            || !string.Equals(artifact.FrameId, source.FrameId, StringComparison.Ordinal))
        {
            reason = "artifact source identity, grid, unit, or frame does not match the recipe source.";
            return false;
        }

        var indices = new List<int>();
        foreach (var region in artifact.Regions)
        {
            foreach (var cell in region.Cells)
            {
                if (cell.Row < 0 || cell.Row >= source.Height
                    || cell.Column < 0 || cell.Column >= source.Width)
                {
                    reason = "artifact contains a cell outside the recipe source grid.";
                    return false;
                }

                indices.Add(checked(cell.Row * source.Width + cell.Column));
            }
        }

        var mask = C3DOutlierCellMap.Create(source.Width, source.Height, indices);
        if (!string.Equals(mask.Sha256, artifact.MaskContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            reason = "artifact mask cells do not reproduce the persisted mask identity.";
            return false;
        }

        var values = source.Values.ToArray();
        foreach (var index in indices.Distinct())
        {
            values[index] = double.NaN;
        }

        var output = source.CreateDerived(
            artifact.SourceEntityId,
            values,
            $"restored:connected-region:{artifact.ArtifactId}:{artifact.MaskContentSha256}");
        if (!string.Equals(output.ContentSha256, artifact.SourceContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            reason = "artifact source-output identity cannot be reproduced from the persisted mask.";
            return false;
        }

        if (!MatchesAuthoredParameters(step, artifact, out reason))
        {
            return false;
        }

        upstream = (output, mask);
        return true;
    }

    private static bool MatchesAuthoredParameters(
        ToolRecipeStep step,
        C3DConnectedRegionArtifact artifact,
        out string reason)
    {
        reason = string.Empty;
        string Value(string name) =>
            step.Parameters?.SingleOrDefault(parameter => parameter.Name == name)?.Value ?? string.Empty;

        if (!string.Equals(Value("Connectivity"), artifact.Connectivity, StringComparison.Ordinal)
            || !double.TryParse(Value("OriginX"), NumberStyles.Float, CultureInfo.InvariantCulture, out var originX)
            || !double.TryParse(Value("OriginY"), NumberStyles.Float, CultureInfo.InvariantCulture, out var originY)
            || !double.TryParse(Value("ColumnPitch"), NumberStyles.Float, CultureInfo.InvariantCulture, out var columnPitch)
            || !double.TryParse(Value("RowPitch"), NumberStyles.Float, CultureInfo.InvariantCulture, out var rowPitch)
            || originX != artifact.OriginX
            || originY != artifact.OriginY
            || columnPitch != artifact.ColumnPitch
            || rowPitch != artifact.RowPitch
            || !string.Equals(Value("AreaUnit"), artifact.AreaUnit, StringComparison.Ordinal))
        {
            reason = "artifact parameters do not match the current authored Connected Region step.";
            return false;
        }

        return true;
    }

    private void SetSummary(string value)
    {
        connectedRegionExecutionSummary = value;
        onExecutionStateChanged();
    }

    private static string GetArtifactPath(string recipePath, string outputEntityId)
    {
        var fullRecipePath = Path.GetFullPath(recipePath);
        var directory = Path.GetDirectoryName(fullRecipePath)
            ?? Environment.CurrentDirectory;
        var recipeName = Path.GetFileNameWithoutExtension(fullRecipePath);
        var safeOutputId = new string(
            outputEntityId
                .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)
                .ToArray());
        return Path.Combine(directory, $"{recipeName}.connected-region.{safeOutputId}.json");
    }
}
