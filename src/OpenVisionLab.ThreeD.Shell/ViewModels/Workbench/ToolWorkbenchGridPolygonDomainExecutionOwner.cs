using System.IO;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns Preview/Publish and recipe-sidecar persistence for the authored
/// GridPolygon Domain step. Domain membership is produced by the typed Tools
/// adapter; this owner does not perform rasterization or mask arithmetic.
/// </summary>
internal sealed class ToolWorkbenchGridPolygonDomainExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelectedStepGridPolygonDomain;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string, C3DHeightFieldSnapshot?> getPublishedHeightField;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DGridDomainArtifact? preview;
    private string? previewPath;
    private string? previewInputEntityId;
    private string? previewSelectionId;
    private string? previewProducerStepId;
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Select Grid Polygon Domain after teaching a GridPolygon, then Preview.";
    private int disposalState;

    public ToolWorkbenchGridPolygonDomainExecutionOwner(
        Func<bool> isSelectedStepGridPolygonDomain,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<string, C3DHeightFieldSnapshot?> getPublishedHeightField,
        Func<object?, bool> isSourceChangeEvent,
        Action<string, string> appendLog,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepGridPolygonDomain = isSelectedStepGridPolygonDomain;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.getPublishedHeightField = getPublishedHeightField;
        this.isSourceChangeEvent = isSourceChangeEvent;
        this.appendLog = appendLog;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStep => !IsDisposed && isSelectedStepGridPolygonDomain();
    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed && preview is not null && !isPreviewStale;
    public bool IsPreviewStale => !IsDisposed && isPreviewStale;
    public bool IsPreviewPublished => !IsDisposed && isPreviewPublished;
    public C3DGridDomainArtifact? CurrentArtifact => IsDisposed ? null : preview;
    public string? CurrentArtifactPath => IsDisposed ? null : previewPath;
    public string ExecutionSummary => IsDisposed
        ? "Grid Polygon Domain execution owner has been disposed."
        : executionSummary;
    public string OutputSummary => IsDisposed || preview is not { } artifact
        ? "No Grid Polygon Domain output."
        : $"{artifact.Cells.Count:N0} foreground cell(s) | SHA-256 {artifact.ContentSha256} | {(isPreviewPublished ? "Published" : "Preview only")}";

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(cancellation);
        preview = null;
        previewPath = null;
        previewInputEntityId = null;
        previewSelectionId = null;
        previewProducerStepId = null;
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
    }

    internal C3DGridDomainArtifact? TryGetPublishedArtifact(string entityId) =>
        !IsDisposed
        && isPreviewPublished
        && !isPreviewStale
        && preview is { } artifact
        && string.Equals(artifact.ArtifactId, entityId, StringComparison.OrdinalIgnoreCase)
            ? artifact
            : null;

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
        var previous = Interlocked.Exchange(ref previewCancellation, cancellation);
        CancelAndDispose(previous);
        if (IsDisposed)
        {
            CancelAndDispose(cancellation);
            return false;
        }

        SetRunning(true);
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Grid Polygon Domain Preview is rasterizing the taught polygon into a source-bound domain artifact.");
        appendLog("Preview", $"Grid Polygon Domain Preview started: {step.Id}.");

        try
        {
            var document = createDocument();
            if (!TryResolveInputs(document, step.InputEntityIds, out var source, out var selection, out var reason))
            {
                if (IsCurrentPreview(cancellation))
                {
                    step.State = "Taught / needs correction";
                    SetSummary(reason);
                    appendLog("Warning", $"Grid Polygon Domain Preview blocked: {reason}");
                }

                return false;
            }

            var evaluation = await Task.Run(
                () => ToolRecipeGridPolygonDomainExecution.Execute(
                    document,
                    step.Id,
                    source,
                    selection,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                preview = null;
                previewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Grid Polygon Domain Preview failed: {evaluation.Result.Message}");
                return false;
            }

            preview = evaluation.Output;
            previewInputEntityId = source.EntityId;
            previewSelectionId = selection.Id;
            previewProducerStepId = step.Id;
            previewPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            C3DGridDomainArtifactStore.Save(previewPath, evaluation.Output);
            step.State = "Preview ready";
            SetSummary(
                $"Preview ready | {evaluation.Output.Cells.Count:N0} foreground cell(s) | artifact SHA-256 {evaluation.Output.ContentSha256} | source unchanged");
            appendLog(
                "Preview",
                $"Grid Polygon Domain Preview ready: artifact={evaluation.Output.ContentSha256}; selection={selection.Id}; source={source.EntityId}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentPreview(cancellation))
            {
                if (getSelectedPipelineStep() is { } canceledStep)
                {
                    canceledStep.State = "Ready";
                }

                SetSummary("Preview canceled. Source, selection, domain, and recipe were not changed.");
                appendLog("Preview", "Grid Polygon Domain Preview canceled.");
            }

            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(ref previewCancellation, null, cancellation),
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
        && isSelectedStepGridPolygonDomain()
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isPreviewRunning
        && getSelectedPipelineStep() is { } step
        && step.InputEntityIds.Count == 2
        && TryResolveInputs(
            createDocument(),
            step.InputEntityIds,
            out _,
            out _,
            out _)
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void Publish()
    {
        if (IsDisposed
            || getSelectedPipelineStep() is not { } step
            || !HasCurrentPreview
            || !string.Equals(step.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(step.OutputEntityId, preview!.ArtifactId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        isPreviewPublished = true;
        step.State = "Published";
        SetSummary(
            $"Published {step.OutputEntityId} | {preview!.Cells.Count:N0} cells | SHA-256 {preview.ContentSha256} | source and selection remain unchanged");
        appendLog("Publish", $"Grid Polygon Domain output published without re-running: {step.OutputEntityId}.");
        PersistPublishedArtifactIfPossible();
    }

    public void Cancel()
    {
        if (!IsDisposed)
        {
            try
            {
                Volatile.Read(ref previewCancellation)?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public void MarkStaleIfNeeded(object? sender)
    {
        if (IsDisposed || preview is null || isPreviewRunning)
        {
            return;
        }

        if (isSourceChangeEvent(sender)
            || sender is ToolWorkbenchPipelineStepItem changedStep
                && string.Equals(changedStep.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            || sender is ToolRecipeSelection selection
                && string.Equals(selection.Id, previewSelectionId, StringComparison.OrdinalIgnoreCase))
        {
            MarkStale("Source, GridPolygon selection, or routing changed. Preview Grid Polygon Domain again before Publish.");
        }
    }

    public void MarkStaleIfUpstreamChanged()
    {
        if (IsDisposed
            || preview is null
            || isPreviewStale
            || isPreviewRunning)
        {
            return;
        }

        var document = createDocument();
        // An unrelated selected consumer does not own the cached Grid artifact.
        var producers = document.Steps.Where(candidate =>
            string.Equals(candidate.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.ToolId, "grid-polygon-domain", StringComparison.Ordinal)
            && string.Equals(candidate.OutputEntityId, preview.ArtifactId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (producers.Length != 1 || producers[0].InputEntityIds.Count != 2)
        {
            MarkStale("Source, GridPolygon selection, or routing changed. Preview Grid Polygon Domain again before Publish.");
            return;
        }
        var step = producers[0];
        var source = getPublishedHeightField(step.InputEntityIds[0]);
        var sourceIsRecipeSource = string.Equals(
            step.InputEntityIds[0],
            document.Source.Id,
            StringComparison.OrdinalIgnoreCase);
        var selection = document.Selections?.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, step.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
        var selectionContentSha256 = TryCalculateSelectionContentSha256(selection);
        var currentSourceContentSha256 = source?.ContentSha256 ?? document.Source.ContentSha256;
        var currentRootSourceSha256 = source?.RootSourceSha256 ?? document.Source.ContentSha256;
        var currentSourceWidth = source?.Width ?? document.Source.GridWidth;
        var currentSourceHeight = source?.Height ?? document.Source.GridHeight;
        var currentSourceUnit = source?.Unit ?? document.Source.Unit;
        var currentSourceFrame = source?.FrameId ?? document.Source.FrameId;
        var sourceIdentityChanged = sourceIsRecipeSource
            ? !string.Equals(preview.SourceEntityId, step.InputEntityIds[0], StringComparison.OrdinalIgnoreCase)
                || !string.Equals(preview.SourceContentSha256, currentSourceContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(preview.RootSourceSha256, currentRootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || preview.GridWidth != currentSourceWidth
                || preview.GridHeight != currentSourceHeight
                || !string.Equals(preview.Unit, currentSourceUnit, StringComparison.Ordinal)
                || !string.Equals(preview.FrameId, currentSourceFrame, StringComparison.Ordinal)
            : source is null
                || !string.Equals(previewInputEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(preview.SourceContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(preview.RootSourceSha256, source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || preview.GridWidth != source.Width
                || preview.GridHeight != source.Height
                || !string.Equals(preview.Unit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(preview.FrameId, source.FrameId, StringComparison.Ordinal);
        if (sourceIdentityChanged
            || selection is null
            || !string.Equals(previewSelectionId, selection.Id, StringComparison.OrdinalIgnoreCase)
            || selectionContentSha256 is null
            || !string.Equals(preview.SelectionContentSha256, selectionContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            MarkStale("Published HeightField or GridPolygon selection changed. Preview Grid Polygon Domain again before Publish.");
        }
    }

    public void Clear(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(cancellation);
        preview = null;
        previewPath = null;
        previewInputEntityId = null;
        previewSelectionId = null;
        previewProducerStepId = null;
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
            && IsSelectedStep
            && preview is null
            && !isPreviewRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = CanPreview() ? "Ready" : "Taught / needs correction";
        }

        onExecutionStateChanged();
    }

    public void SetRunning(bool value)
    {
        if (!IsDisposed)
        {
            isPreviewRunning = value;
            onExecutionStateChanged();
        }
    }

    public void PersistPublishedArtifactIfPossible()
    {
        if (IsDisposed
            || !isPreviewPublished
            || isPreviewStale
            || preview is null
            || string.IsNullOrWhiteSpace(getRecipePath()))
        {
            return;
        }

        try
        {
            MarkStaleIfUpstreamChanged();
            if (isPreviewStale) return;
            var directory = Path.GetDirectoryName(Path.GetFullPath(getRecipePath()!))
                ?? Environment.CurrentDirectory;
            previewPath = GetArtifactPath(directory, getRecipePath()!, preview.ArtifactId);
            C3DGridDomainArtifactStore.Save(previewPath, preview);
            appendLog("Save", $"Grid Polygon Domain artifact saved: {previewPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            SetSummary($"Published in this session, but the Grid Polygon Domain artifact could not be saved: {exception.Message}");
            appendLog("Error", $"Grid Polygon Domain artifact save failed: {exception.Message}");
        }
    }

    public void RestorePublishedArtifact()
    {
        if (IsDisposed || string.IsNullOrWhiteSpace(getRecipePath()))
        {
            return;
        }

        var document = createDocument();
        var step = document.Steps.FirstOrDefault(candidate =>
            string.Equals(candidate.ToolId, "grid-polygon-domain", StringComparison.Ordinal));
        if (step is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(getRecipePath()!))
            ?? Environment.CurrentDirectory;
        var path = GetArtifactPath(directory, getRecipePath()!, step.OutputEntityId);
        if (!File.Exists(path))
        {
            var legacyId = SanitizeLegacy(step.OutputEntityId);
            if (document.Steps.Count(candidate =>
                    string.Equals(candidate.ToolId, "grid-polygon-domain", StringComparison.Ordinal)
                    && string.Equals(SanitizeLegacy(candidate.OutputEntityId), legacyId, StringComparison.OrdinalIgnoreCase)) != 1)
            {
                appendLog("Warning", "Grid Polygon Domain legacy artifact restore skipped: ambiguous output filename.");
                return;
            }
            path = GetArtifactPath(directory, getRecipePath()!, step.OutputEntityId, legacy: true);
        }
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var artifact = C3DGridDomainArtifactStore.Load(path);
            if (!TryResolveInputs(document, step.InputEntityIds, out var source, out var selection, out var reason))
            {
                SetSummary($"Saved Grid Polygon Domain artifact was not restored: {reasonOrDefault(reason)}");
                appendLog("Warning", "Grid Polygon Domain artifact restore skipped: current source, selection, or output identity differs.");
                return;
            }

            var selectionContentSha256 = TryCalculateSelectionContentSha256(selection);
            if (!string.Equals(artifact.SourceEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(artifact.SourceContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(artifact.RootSourceSha256, source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || artifact.GridWidth != source.Width
                || artifact.GridHeight != source.Height
                || !string.Equals(artifact.Unit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(artifact.FrameId, source.FrameId, StringComparison.Ordinal)
                || !string.Equals(artifact.CoordinateConvention, C3DGridDomainArtifact.CurrentCoordinateConvention, StringComparison.Ordinal)
                || !string.Equals(artifact.SelectionId, selection.Id, StringComparison.OrdinalIgnoreCase)
                || selectionContentSha256 is null
                || !string.Equals(artifact.SelectionContentSha256, selectionContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(artifact.ArtifactId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                SetSummary("Saved Grid Polygon Domain artifact was not restored: current source, selection, or output identity differs.");
                appendLog("Warning", "Grid Polygon Domain artifact restore skipped: current source, selection, or output identity differs.");
                return;
            }

            preview = artifact;
            previewPath = path;
            previewInputEntityId = source.EntityId;
            previewSelectionId = selection.Id;
            previewProducerStepId = step.Id;
            isPreviewStale = false;
            isPreviewPublished = true;
            if (getSelectedPipelineStep() is { } selected
                && string.Equals(selected.Id, step.Id, StringComparison.OrdinalIgnoreCase))
            {
                selected.State = "Published";
            }

            SetSummary($"Restored Published Grid Polygon Domain artifact {artifact.ArtifactId} without executing the tool.");
            appendLog("Open", $"Grid Polygon Domain artifact restored: {path}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            SetSummary($"Saved Grid Polygon Domain artifact was not restored: {exception.Message}");
            appendLog("Warning", $"Grid Polygon Domain artifact restore skipped: {exception.Message}");
        }
    }

    private bool TryResolveInputs(
        ToolRecipeDocument document,
        IReadOnlyList<string> inputEntityIds,
        out C3DHeightFieldSnapshot source,
        out ToolRecipeSelection selection,
        out string reason)
    {
        source = null!;
        selection = null!;
        reason = string.Empty;
        if (inputEntityIds.Count != 2)
        {
            reason = "Grid Polygon Domain requires one HeightField followed by one GridPolygon selection.";
            return false;
        }

        source = getPublishedHeightField(inputEntityIds[0])!;
        if (source is null
            && string.Equals(inputEntityIds[0], document.Source.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (document.Source is not
                {
                    Path: { Length: > 0 } sourcePath,
                    ByteLength: { } byteLength,
                    ContentSha256: { Length: > 0 } contentSha256,
                    GridWidth: { } gridWidth,
                    GridHeight: { } gridHeight
                })
            {
                reason = "The recipe source identity is incomplete.";
                return false;
            }

            source = C3DHeightFieldSnapshot.LoadVerified(
                sourcePath,
                document.Source.Id,
                document.Source.Unit,
                document.Source.FrameId,
                byteLength,
                contentSha256,
                gridWidth,
                gridHeight);
        }

        if (source is null)
        {
            reason = "The Grid Polygon Domain HeightField input is not Published or restorable.";
            return false;
        }

        selection = document.Selections?.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, inputEntityIds[1], StringComparison.OrdinalIgnoreCase))!;
        if (selection is null)
        {
            reason = "The GridPolygon selection is not present in the recipe.";
            return false;
        }

        return true;
    }

    private void MarkStale(string summary)
    {
        isPreviewStale = true;
        isPreviewPublished = false;
        if (getSelectedPipelineStep() is { } step
            && string.Equals(step.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase))
            step.State = "Preview stale";
        SetSummary(summary);
    }

    private void SetSummary(string value)
    {
        if (!IsDisposed)
        {
            executionSummary = value;
            onExecutionStateChanged();
        }
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(Volatile.Read(ref previewCancellation), cancellation);

    private static string reasonOrDefault(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? "artifact identity or current input did not match." : reason;

    private static string? TryCalculateSelectionContentSha256(ToolRecipeSelection? selection)
    {
        if (selection is null)
        {
            return null;
        }

        try
        {
            return C3DGridDomainArtifact.CalculateSelectionContentSha256(selection);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string CreatePreviewPath(string hash)
    {
        var root = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var directory = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenVisionLab",
                "3DStudio",
                "Preview")
            : Path.Combine(Path.GetFullPath(root), "Preview");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"grid-polygon-domain-{hash}.json");
    }

    private static string GetArtifactPath(
        string recipeDirectory,
        string recipePath,
        string outputEntityId, bool legacy = false) =>
        Path.Combine(
            recipeDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.grid-polygon-domain.{(legacy ? SanitizeLegacy(outputEntityId) : Sanitize(outputEntityId))}.json");

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
}
