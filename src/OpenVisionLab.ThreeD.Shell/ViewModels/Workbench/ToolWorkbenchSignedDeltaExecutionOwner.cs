using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns Preview/Publish state for signed background subtraction. The result is
/// an evidence artifact rather than a C3D file, so this owner deliberately does
/// not route it to the C3D viewer or serialize finite zero through the C3D wire
/// format.
/// </summary>
internal sealed class ToolWorkbenchSignedDeltaExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelectedStep;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> getPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action onExecutionStateChanged;
    private CancellationTokenSource? previewCancellation;
    private C3DSignedHeightDeltaEvaluation? preview;
    private bool isRunning;
    private bool isStale;
    private bool isPublished;
    private string executionSummary =
        "Select Signed Delta Background Subtraction, bind a saved background, then Preview.";
    private int disposalState;

    public ToolWorkbenchSignedDeltaExecutionOwner(
        Func<bool> isSelectedStep,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<string, ToolWorkbenchPipelineStepItem?> getPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<object?, bool> isSourceChangeEvent,
        Action<string, string> appendLog,
        Action onExecutionStateChanged)
    {
        this.isSelectedStep = isSelectedStep;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.getPipelineStep = getPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.isSourceChangeEvent = isSourceChangeEvent;
        this.appendLog = appendLog;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStep => !IsDisposed && isSelectedStep();
    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;
    public bool IsPreviewRunning => !IsDisposed && isRunning;
    public bool HasCurrentPreview => !IsDisposed && preview?.Output is not null && !isStale;
    public bool IsPreviewStale => !IsDisposed && isStale;
    public bool IsPreviewPublished => !IsDisposed && isPublished;
    public C3DSignedHeightDeltaArtifact? CurrentOutput => IsDisposed ? null : preview?.Output;
    public string ExecutionSummary => IsDisposed
        ? "Signed Delta Background Subtraction execution owner has been disposed."
        : executionSummary;
    public string OutputSummary => IsDisposed || preview?.Output is not { } output
        ? "No signed-delta Preview output."
        : $"paired {output.PairedValidSampleCount:N0} | missing {output.MissingEitherSampleCount:N0} | zero {output.ZeroDeltaSampleCount:N0} | +{output.PositiveDeltaSampleCount:N0} / -{output.NegativeDeltaSampleCount:N0} | {(isPublished ? "Published" : "Preview only")}";
    public string EvidenceSummary => IsDisposed || preview?.Output is not { } output
        ? "Signed-delta evidence SHA-256 is available after Preview."
        : $"Artifact SHA-256 {output.ContentSha256} | values SHA-256 {output.OutputContentSha256} | finite zero preserved";

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0) return;
        var cancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(cancellation);
        preview = null;
        isRunning = false;
        isStale = false;
        isPublished = false;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview() || getSelectedPipelineStep() is not { } step) return false;
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref previewCancellation, cancellation);
        CancelAndDispose(previous);
        if (IsDisposed)
        {
            CancelAndDispose(cancellation);
            return false;
        }

        SetRunning(true);
        isStale = false;
        isPublished = false;
        step.State = "Preview running";
        SetSummary("Signed Delta Background Subtraction Preview is verifying the saved background and preserving finite zero.");
        appendLog("Preview", $"Signed Delta Background Subtraction Preview started: {step.Id}.");
        try
        {
            var document = createDocument();
            var evaluation = await Task.Run(
                () => ToolRecipeSignedDeltaBackgroundSubtractionExecution.Execute(
                    document,
                    step.Id,
                    recipeDirectory: GetRecipeDirectory(),
                    cancellationToken: cancellation.Token),
                cancellation.Token);
            if (!IsCurrentPreview(cancellation)) return false;
            if (evaluation.Result.Status is not (ResultStatus.Pass or ResultStatus.Warning)
                || evaluation.Output is null)
            {
                preview = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Signed Delta Background Subtraction Preview failed: {evaluation.Result.Message}");
                return false;
            }

            preview = evaluation;
            step.State = evaluation.Result.Status == ResultStatus.Warning ? "Preview warning" : "Preview ready";
            SetSummary($"Preview ready | paired {evaluation.Output.PairedValidSampleCount:N0} | missing {evaluation.Output.MissingEitherSampleCount:N0} | finite zero {evaluation.Output.ZeroDeltaSampleCount:N0} | evidence only");
            appendLog("Preview", $"Signed Delta Background Subtraction Preview ready: artifact={evaluation.Output.ContentSha256}; values={evaluation.Output.OutputContentSha256}; finite zero preserved.");
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(cancellation)) return false;
            step.State = "Ready";
            SetSummary("Preview canceled. Source, saved background, and authored recipe were not changed.");
            appendLog("Preview", "Signed Delta Background Subtraction Preview canceled.");
            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(Interlocked.CompareExchange(ref previewCancellation, null, cancellation), cancellation);
            if (ownsCancellation)
            {
                cancellation.Dispose();
                if (!IsDisposed) SetRunning(false);
            }
        }
    }

    public bool CanPreview() =>
        !IsDisposed
        && IsSelectedStep
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isRunning
        && getSelectedPipelineStep() is { } step
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void Publish()
    {
        if (IsDisposed || getSelectedPipelineStep() is not { } step || !HasCurrentPreview) return;
        isPublished = true;
        step.State = "Published";
        SetSummary($"Published evidence artifact {step.OutputEntityId} | SHA-256 {preview!.Output!.ContentSha256} | no C3D encoding performed");
        appendLog("Publish", $"Signed Delta Background Subtraction evidence published without re-running: {step.OutputEntityId}.");
    }

    /// <summary>
    /// Persists the currently published signed-delta artifact and returns the
    /// explicit recipe reference that the caller must place in the recipe
    /// document. A preview-only or stale result is deliberately not saved.
    /// </summary>
    public ToolRecipeSignedDeltaSidecarReference? PersistPublishedSidecar(string recipePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        if (IsDisposed
            || !isPublished
            || isStale
            || preview?.Output is not { } output)
        {
            return null;
        }

        var document = createDocument();
        var step = document.Steps.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, output.StepId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.ToolId, C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.OutputEntityId, output.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (step is null)
        {
            throw new InvalidDataException(
                "The published signed-delta output no longer matches a recipe step.");
        }

        if (!TryValidateArtifactAgainstRecipe(document, step, output, reference: null, out var validationMessage))
        {
            throw new InvalidDataException(validationMessage);
        }

        var recipeDirectory = GetRecipeDirectory(recipePath);
        var sidecarPath = GetSidecarPath(recipeDirectory, recipePath, output.ContentSha256);
        C3DSignedHeightDeltaArtifactStore.Save(sidecarPath, output);
        var relativePath = Path.GetRelativePath(recipeDirectory, sidecarPath);
        appendLog("Save", $"Signed Delta evidence sidecar saved: {sidecarPath}.");
        var reference = CreateSidecarReference(relativePath, output);
        if (getSelectedPipelineStep() is { } selectedStep
            && string.Equals(selectedStep.Id, output.StepId, StringComparison.OrdinalIgnoreCase))
        {
            selectedStep.SignedDeltaSidecar = reference;
        }
        onExecutionStateChanged();
        return reference;
    }

    /// <summary>
    /// Loads and validates the sidecar explicitly referenced by an incoming
    /// recipe. This method has no owner-state side effects, so a malformed,
    /// missing, or tampered sidecar can reject Open before the current recipe
    /// or result is replaced.
    /// </summary>
    public bool TryLoadPublishedSidecar(
        string recipePath,
        ToolRecipeDocument document,
        out C3DSignedHeightDeltaArtifact? output,
        out string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        ArgumentNullException.ThrowIfNull(document);
        output = null;

        var referencedSteps = document.Steps
            .Where(step => step.SignedDeltaSidecar is not null)
            .ToArray();
        if (referencedSteps.Length == 0)
        {
            message = "No signed-delta sidecar is explicitly referenced by this recipe.";
            return true;
        }

        if (referencedSteps.Length != 1
            || !string.Equals(
                referencedSteps[0].ToolId,
                C3DSignedHeightDeltaRecipePolicy.ToolId,
                StringComparison.OrdinalIgnoreCase))
        {
            message = "A recipe must contain exactly one signed-delta sidecar reference owned by a signed-delta step.";
            return false;
        }

        var step = referencedSteps[0];
        var reference = step.SignedDeltaSidecar!;
        if (!TryResolveSidecarPath(recipePath, reference.Path, out var sidecarPath, out message))
        {
            return false;
        }

        try
        {
            if (!File.Exists(sidecarPath))
            {
                message = $"The explicitly referenced signed-delta sidecar is missing: {sidecarPath}";
                return false;
            }

            var loaded = C3DSignedHeightDeltaArtifactStore.Load(sidecarPath);
            if (!TryValidateArtifactAgainstRecipe(document, step, loaded, reference, out message))
            {
                return false;
            }

            output = loaded;
            message = $"Signed-delta sidecar validated: {reference.Path}";
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException)
        {
            message = $"Signed-delta sidecar could not be loaded: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Restores a prevalidated artifact into this owner without running the
    /// signed-delta tool. The caller must invoke this only after ApplyDocument
    /// has completed successfully.
    /// </summary>
    public void RestorePublishedSidecar(
        ToolRecipeDocument document,
        C3DSignedHeightDeltaArtifact? output)
    {
        if (IsDisposed || output is null)
        {
            return;
        }

        var step = document.Steps.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, output.StepId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.ToolId, C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.OrdinalIgnoreCase));
        if (step is null)
        {
            return;
        }

        preview = new C3DSignedHeightDeltaEvaluation(
            new ToolResult(
                C3DHeightBackgroundSubtractionRule.SignedDeltaToolName,
                ResultStatus.Pass,
                "Published signed-delta sidecar restored without Preview, Publish, or Run.",
                TimeSpan.Zero,
                [],
                []),
            output);
        isStale = false;
        isPublished = true;
        var selectedStep = getPipelineStep(output.StepId) ?? getSelectedPipelineStep();
        if (selectedStep is not null
            && string.Equals(selectedStep.Id, output.StepId, StringComparison.OrdinalIgnoreCase))
        {
            selectedStep.State = "Published";
        }

        SetSummary($"Restored published signed-delta evidence {output.OutputEntityId} | SHA-256 {output.ContentSha256} | no Preview/Run");
        appendLog("Open", $"Signed Delta evidence sidecar restored without execution: {output.OutputEntityId}.");
    }

    public void Cancel()
    {
        if (!IsDisposed)
        {
            try { Volatile.Read(ref previewCancellation)?.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    public void MarkStaleIfNeeded(object? sender)
    {
        if (IsDisposed || preview is null || isRunning) return;
        var selected = getSelectedPipelineStep();
        var selectedParameter = IsSelectedStep
            && sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        if (selected is { } step && (isSourceChangeEvent(sender) || ReferenceEquals(sender, step) || selectedParameter))
        {
            isStale = true;
            isPublished = false;
            step.SignedDeltaSidecar = null;
            step.State = "Preview stale";
            SetSummary("Source, saved-background identity, output, or policy changed. Preview again before Publish.");
        }
    }

    public void Clear(string summary)
    {
        if (IsDisposed) return;
        var cancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(cancellation);
        preview = null;
        isStale = false;
        isPublished = false;
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (IsDisposed) return;
        if (getSelectedPipelineStep() is { } step && IsSelectedStep && preview is null && !isRunning && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid ? "Ready" : "Taught / needs correction";
        }
        onExecutionStateChanged();
    }

    public void SetRunning(bool value)
    {
        if (!IsDisposed)
        {
            isRunning = value;
            onExecutionStateChanged();
        }
    }

    internal bool TryGetPublishedOutput(string entityId, out C3DSignedHeightDeltaArtifact? output)
    {
        output = null;
        if (!IsDisposed && isPublished && !isStale && preview?.Output is { } current && string.Equals(current.OutputEntityId, entityId, StringComparison.OrdinalIgnoreCase)) output = current;
        return output is not null;
    }

    private void SetSummary(string value)
    {
        if (!IsDisposed)
        {
            executionSummary = value;
            onExecutionStateChanged();
        }
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) => !IsDisposed && ReferenceEquals(Volatile.Read(ref previewCancellation), cancellation);

    private string GetRecipeDirectory()
    {
        var path = getRecipePath();
        return string.IsNullOrWhiteSpace(path) ? Environment.CurrentDirectory : GetRecipeDirectory(path);
    }

    private static string GetRecipeDirectory(string recipePath) =>
        Path.GetDirectoryName(Path.GetFullPath(recipePath)) ?? Environment.CurrentDirectory;

    private static string GetSidecarPath(string recipeDirectory, string recipePath, string contentSha256) =>
        Path.Combine(
            recipeDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.signed-delta.{contentSha256}{C3DSignedHeightDeltaArtifactStore.FileExtension}");

    private static ToolRecipeSignedDeltaSidecarReference CreateSidecarReference(
        string relativePath,
        C3DSignedHeightDeltaArtifact output) =>
        new(
            relativePath,
            output.StepId,
            output.OutputEntityId,
            output.ContentSha256,
            output.OutputContentSha256,
            output.CurrentSourceEntityId,
            output.CurrentSourceContentSha256,
            output.CurrentRootSourceSha256,
            output.BackgroundEntityId,
            output.BackgroundContentSha256,
            output.BackgroundRootSourceSha256,
            output.Width,
            output.Height,
            output.GridOriginColumn,
            output.GridOriginRow,
            output.Unit,
            output.FrameId);

    private static bool TryResolveSidecarPath(
        string recipePath,
        string relativePath,
        out string sidecarPath,
        out string message)
    {
        sidecarPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathFullyQualified(relativePath)
            || !relativePath.EndsWith(C3DSignedHeightDeltaArtifactStore.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            message = "Signed-delta sidecar reference must be a relative .sdelta.json path.";
            return false;
        }

        var recipeDirectory = GetRecipeDirectory(recipePath);
        sidecarPath = Path.GetFullPath(Path.Combine(recipeDirectory, relativePath));
        var relativeToRecipe = Path.GetRelativePath(recipeDirectory, sidecarPath);
        if (relativeToRecipe is "."
            || relativeToRecipe.Equals("..", StringComparison.Ordinal)
            || relativeToRecipe.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToRecipe))
        {
            sidecarPath = string.Empty;
            message = "Signed-delta sidecar reference must remain under the recipe directory.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryValidateArtifactAgainstRecipe(
        ToolRecipeDocument document,
        ToolRecipeStep step,
        C3DSignedHeightDeltaArtifact artifact,
        ToolRecipeSignedDeltaSidecarReference? reference,
        out string message)
    {
        if (!artifact.TryValidate(out message))
        {
            return false;
        }

        if (reference is not null
            && !reference.TryValidateContract(
                step.Id,
                step.ToolId,
                step.OutputEntityId,
                artifact,
                out message))
        {
            return false;
        }

        if (!string.Equals(step.Id, artifact.StepId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(step.OutputEntityId, artifact.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(document.Source.Id, artifact.CurrentSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(document.Source.ContentSha256)
            || !string.Equals(document.Source.ContentSha256, artifact.CurrentSourceContentSha256, StringComparison.OrdinalIgnoreCase)
            || (document.Source.ByteLength is { } sourceByteLength && sourceByteLength != artifact.CurrentByteLength)
            || (document.Source.GridWidth is { } sourceWidth && sourceWidth != artifact.Width)
            || (document.Source.GridHeight is { } sourceHeight && sourceHeight != artifact.Height)
            || !string.Equals(document.Source.Unit, artifact.Unit, StringComparison.Ordinal)
            || !string.Equals(document.Source.FrameId, artifact.FrameId, StringComparison.Ordinal))
        {
            message = "Signed-delta sidecar source, output, grid, unit, or frame identity does not match the recipe.";
            return false;
        }

        if (!C3DSignedHeightDeltaRecipePolicy.TryRead(step.Parameters, out var background, out message))
        {
            return false;
        }

        if (!string.Equals(background.EntityId, artifact.BackgroundEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(background.ContentSha256, artifact.BackgroundContentSha256, StringComparison.OrdinalIgnoreCase)
            || background.ByteLength != artifact.BackgroundByteLength
            || background.Width != artifact.Width
            || background.Height != artifact.Height
            || !string.Equals(background.Unit, artifact.Unit, StringComparison.Ordinal)
            || !string.Equals(background.FrameId, artifact.FrameId, StringComparison.Ordinal))
        {
            message = "Signed-delta sidecar background identity does not match the recipe.";
            return false;
        }

        message = "Signed-delta sidecar and recipe identities match.";
        return true;
    }

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null) return;
        try { cancellation.Cancel(); } catch (ObjectDisposedException) { }
        cancellation.Dispose();
    }
}
