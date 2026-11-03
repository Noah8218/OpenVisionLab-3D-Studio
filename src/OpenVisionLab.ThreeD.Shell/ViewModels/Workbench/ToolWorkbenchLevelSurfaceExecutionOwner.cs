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

/// <summary>
/// Owns the Level Surface Preview lifecycle and its typed leveling artifacts.
/// The registered cancellation source identifies the operation allowed to
/// publish output, persist state, or request a Viewer update.
/// </summary>
internal sealed class ToolWorkbenchLevelSurfaceExecutionOwner : IDisposable
{
    private const int ArtifactSchemaVersion = 2;
    private readonly Func<bool> isSelectedStepLevelSurface;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? levelSurfacePreviewCancellation;
    private C3DLevelSurfaceEvaluation? levelSurfacePreview;
    private string? levelSurfacePreviewPath;
    private string? levelSurfacePreviewStepId;
    private string? levelSurfacePreviewProducerContextSha256;
    private bool isLevelSurfacePreviewRunning;
    private bool isLevelSurfacePreviewStale;
    private bool isLevelSurfacePreviewPublished;
    private string levelSurfaceExecutionSummary =
        "Select Level Surface, teach one or more reference ROIs, then Preview.";
    private int disposalState;

    public ToolWorkbenchLevelSurfaceExecutionOwner(
        Func<bool> isSelectedStepLevelSurface,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<object?, bool> isSourceChangeEvent,
        Action<string, string> appendLog,
        Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepLevelSurface = isSelectedStepLevelSurface;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.isSourceChangeEvent = isSourceChangeEvent;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStepLevelSurface => !IsDisposed && isSelectedStepLevelSurface();

    public bool IsLevelSurfacePreviewRunning => !IsDisposed && isLevelSurfacePreviewRunning;
    public bool HasCurrentLevelSurfacePreview =>
        !IsDisposed
        && levelSurfacePreview?.Output is not null
        && levelSurfacePreview.Transform is not null
        && levelSurfacePreview.LevelFrame is not null
        && levelSurfacePreview.FrameChain is not null
        && !isLevelSurfacePreviewStale;
    public bool IsLevelSurfacePreviewStale => !IsDisposed && isLevelSurfacePreviewStale;
    public bool IsLevelSurfacePreviewPublished => !IsDisposed && isLevelSurfacePreviewPublished;
    public C3DHeightFieldSnapshot? CurrentLevelSurfacePreviewOutput =>
        IsDisposed ? null : levelSurfacePreview?.Output;
    public C3DLevelingTransform? CurrentLevelSurfaceTransform =>
        IsDisposed ? null : levelSurfacePreview?.Transform;
    public C3DLevelFrameArtifact? CurrentLevelSurfaceLevelFrame =>
        IsDisposed ? null : levelSurfacePreview?.LevelFrame;
    public C3DLevelFrameQualityEvidence? CurrentLevelSurfaceQualityEvidence =>
        IsDisposed ? null : levelSurfacePreview?.QualityEvidence;
    public C3DLevelSurfaceCoordinateFrameChain? CurrentLevelSurfaceFrameChain =>
        IsDisposed ? null : levelSurfacePreview?.FrameChain;
    public double CurrentLevelSurfaceOutputSlopeX =>
        IsDisposed ? double.NaN : levelSurfacePreview?.OutputReferenceSlopeX ?? double.NaN;
    public double CurrentLevelSurfaceOutputSlopeZ =>
        IsDisposed ? double.NaN : levelSurfacePreview?.OutputReferenceSlopeZ ?? double.NaN;
    public string? CurrentLevelSurfacePreviewPath => IsDisposed ? null : levelSurfacePreviewPath;
    public string LevelSurfaceExecutionSummary => IsDisposed
        ? "Level Surface execution owner has been disposed."
        : levelSurfaceExecutionSummary;

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public string LevelSurfaceReferenceSummary =>
        IsDisposed
            || getSelectedPipelineStep() is not { } step
            || !IsSelectedStepLevelSurface
            ? "No Level Surface reference routing."
            : $"{Math.Max(0, step.InputEntityIds.Count - 1)} explicit reference ROI(s) | unique finite cells | overlap counted once";

    public string LevelSurfaceTransformSummary =>
        IsDisposed || levelSurfacePreview?.Transform is not { } transform
            ? "No typed leveling transform until Preview completes."
            : $"Transform {transform.ContentSha256} | input slope X {transform.FittedSlopeX:0.##}, Z {transform.FittedSlopeZ:0.##} | target {transform.TargetHeight:0.##} {transform.SourceUnit}";

    public string LevelSurfaceFrameSummary =>
        IsDisposed || levelSurfacePreview?.LevelFrame is not { } frame
            ? "No reusable Level Frame until Preview completes."
            : $"Level Frame {frame.ContentSha256} | {frame.LevelFrameId} | U/V/H right-handed software frame | source unchanged";

    public string LevelSurfaceFrameChainSummary =>
        IsDisposed || levelSurfacePreview?.FrameChain is not { } chain
            ? "No named Source / Reference / Result / Level frame chain until Preview completes."
            : $"Source: {chain.Source.FrameId} | Reference: {chain.Reference.FrameId} ({chain.Reference.SelectionIds.Count} ROI) | Result: {chain.Result?.FrameId ?? "(none; source preserved)"} | Level: {chain.Level.FrameId}";

    public string LevelSurfaceResidualSummary =>
        IsDisposed || levelSurfacePreview is not { Transform: { } transform }
            ? "Reference residual and output slope evidence are available after Preview."
            : $"Reference RMS {transform.ReferenceResidualRms:0.##} | P2V {transform.ReferenceResidualPeakToValley:0.##} | output slope X {levelSurfacePreview.OutputReferenceSlopeX:0.##}, Z {levelSurfacePreview.OutputReferenceSlopeZ:0.##} | software confidence {levelSurfacePreview.QualityEvidence?.State.ToString() ?? "Unavailable"} | minimum reference coverage {levelSurfacePreview.QualityEvidence?.MinimumObservedCoverageRatio.ToString("P1") ?? "Unavailable"}";

    public string LevelSurfaceOutputSummary =>
        IsDisposed || levelSurfacePreview?.Output is not { } output
            ? "No leveled C3D output."
            : $"{output.Width} x {output.Height} | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isLevelSurfacePreviewPublished ? "Published" : "Preview only")}";

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref levelSurfacePreviewCancellation,
            null);
        CancelAndDispose(currentCancellation);
        levelSurfacePreview = null;
        levelSurfacePreviewPath = null;
        levelSurfacePreviewStepId = null;
        levelSurfacePreviewProducerContextSha256 = null;
        isLevelSurfacePreviewRunning = false;
        isLevelSurfacePreviewStale = false;
        isLevelSurfacePreviewPublished = false;
    }

    public async Task<bool> PreviewSelectedLevelSurfaceAsync()
    {
        if (IsDisposed
            || !CanPreviewSelectedLevelSurface()
            || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var currentCancellation = new CancellationTokenSource();
        var cancellationToken = currentCancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref levelSurfacePreviewCancellation,
            currentCancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref levelSurfacePreviewCancellation,
                    null,
                    currentCancellation),
                currentCancellation))
            {
                currentCancellation.Dispose();
            }

            return false;
        }

        SetLevelSurfaceRunning(true);
        isLevelSurfacePreviewStale = false;
        isLevelSurfacePreviewPublished = false;
        step.State = "Preview running";
        SetLevelSurfaceSummary(
            "Level Surface Preview is fitting the explicit reference regions without changing the source.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"Level Surface Preview started: {step.Id}.");
        try
        {
            var document = createDocument();
            var recipeDirectory = GetRecipeDirectory();
            var preview = await Task.Run(
                () => ExecutePreview(
                    document,
                    step.Id,
                    recipeDirectory,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            var evaluation = preview.Evaluation;
            levelSurfacePreviewStepId = step.Id;
            levelSurfacePreviewProducerContextSha256 = preview.ProducerContextSha256;
            if (evaluation.Result.Status != ResultStatus.Pass
                || evaluation.Output is null
                || evaluation.Transform is null
                || evaluation.LevelFrame is null
                || evaluation.QualityEvidence is null
                || evaluation.FrameChain is null)
            {
                if (!IsCurrentPreview(currentCancellation))
                {
                    return false;
                }

                levelSurfacePreview = evaluation;
                levelSurfacePreviewPath = null;
                step.State = evaluation.Result.Status == ResultStatus.Fail
                    ? "Reference gate failed"
                    : "Error";
                SetLevelSurfaceSummary(evaluation.Result.Message);
                if (IsCurrentPreview(currentCancellation))
                {
                    appendLog(
                        evaluation.Result.Status == ResultStatus.Fail ? "Preview" : "Error",
                        $"Level Surface Preview did not produce output: {evaluation.Result.Message}");
                }

                return false;
            }

            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            levelSurfacePreview = evaluation;
            levelSurfacePreviewPath = CreateLevelSurfacePreviewPath(evaluation.Output.ContentSha256);
            SaveC3DAtomically(evaluation.Output, levelSurfacePreviewPath);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Preview ready";
            SetLevelSurfaceSummary(
                $"Preview ready | {evaluation.Transform.ReferenceRegions.Count} reference ROI(s) | input slope X {evaluation.Transform.FittedSlopeX:0.##}, Z {evaluation.Transform.FittedSlopeZ:0.##} | output slope X {evaluation.OutputReferenceSlopeX:0.##}, Z {evaluation.OutputReferenceSlopeZ:0.##} | level frame {evaluation.LevelFrame.ContentSha256[..12]} | frame chain {evaluation.FrameChain.ContentSha256[..12]} | source unchanged");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog(
                    "Preview",
                    $"Level Surface Preview ready: output={evaluation.Output.ContentSha256}; transform={evaluation.Transform.ContentSha256}; levelFrame={evaluation.LevelFrame.ContentSha256}; frameChain={evaluation.FrameChain.ContentSha256}; referenceRms={evaluation.Transform.ReferenceResidualRms:R}.");
            }

            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            requestDisplay(
                new ToolWorkbenchFilterDisplayRequestEventArgs(
                    levelSurfacePreviewPath,
                    evaluation.Output.ContentSha256,
                    false,
                    "Level Surface Preview"));
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetLevelSurfaceSummary("Preview canceled. Source and authored recipe were not changed.");
            appendLog("Preview", "Level Surface Preview canceled.");
            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref levelSurfacePreviewCancellation,
                    null,
                    currentCancellation),
                currentCancellation);
            if (ownsCancellation)
            {
                currentCancellation.Dispose();
            }

            if (ownsCancellation && !IsDisposed)
            {
                SetLevelSurfaceRunning(false);
            }
        }
    }

    public bool CanPreviewSelectedLevelSurface() =>
        !IsDisposed
        && IsSelectedStepLevelSurface
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isLevelSurfacePreviewRunning
        && getSelectedPipelineStep() is { } step
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void PublishSelectedLevelSurface()
    {
        if (IsDisposed
            || getSelectedPipelineStep() is not { } step
            || !HasCurrentLevelSurfacePreview
            || !string.Equals(levelSurfacePreviewStepId, step.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(levelSurfacePreview?.Output?.EntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        isLevelSurfacePreviewPublished = true;
        step.State = "Published";
        SetLevelSurfaceSummary(
            $"Published {step.OutputEntityId} | output SHA-256 {levelSurfacePreview!.Output!.ContentSha256} | leveling transform SHA-256 {levelSurfacePreview.Transform!.ContentSha256} | level frame SHA-256 {levelSurfacePreview.LevelFrame!.ContentSha256} | frame chain SHA-256 {levelSurfacePreview.FrameChain!.ContentSha256} | source unchanged");
        appendLog(
            "Publish",
            $"Level Surface output, typed transform, and Level Frame published without re-running: {step.OutputEntityId}.");
    }

    public void CancelLevelSurfacePreview()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref levelSurfacePreviewCancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }
    }

    public void MarkLevelSurfacePreviewStaleIfNeeded(object? sender)
    {
        if (IsDisposed
            || levelSurfacePreview is null
            || isLevelSurfacePreviewRunning)
        {
            return;
        }

        var selected = getSelectedPipelineStep();
        var selectedIsLevel = IsSelectedStepLevelSurface;
        var isSelectedParameter = selectedIsLevel
            && sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        var isReferenceSelection = sender is ToolRecipeSelection selection
            && selected?.InputEntityIds.Skip(1).Contains(
                selection.Id,
                StringComparer.OrdinalIgnoreCase) == true;
        if (isSourceChangeEvent(sender) || (selectedIsLevel && ReferenceEquals(sender, selected)) || isSelectedParameter || isReferenceSelection)
        {
            isLevelSurfacePreviewStale = true;
            isLevelSurfacePreviewPublished = false;
            if (selected is not null
                && string.Equals(selected.Id, levelSurfacePreviewStepId, StringComparison.OrdinalIgnoreCase))
            {
                selected.State = "Preview stale";
            }

            SetLevelSurfaceSummary(
                "Source, reference ROI, routing, output, or leveling policy changed. Preview again before Publish.");
        }
    }

    public void ClearLevelSurfacePreview(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref levelSurfacePreviewCancellation,
            null);
        CancelAndDispose(currentCancellation);
        levelSurfacePreview = null;
        levelSurfacePreviewPath = null;
        levelSurfacePreviewStepId = null;
        levelSurfacePreviewProducerContextSha256 = null;
        isLevelSurfacePreviewStale = false;
        isLevelSurfacePreviewPublished = false;
        SetLevelSurfaceSummary(summary);
    }

    public void RefreshLevelSurfaceExecutionState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedPipelineStep() is { } step
            && IsSelectedStepLevelSurface
            && levelSurfacePreview is null
            && !isLevelSurfacePreviewRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid
                ? "Ready"
                : "Taught / needs correction";
        }

        onExecutionStateChanged();
    }

    public void SetLevelSurfaceRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isLevelSurfacePreviewRunning = value;
        onExecutionStateChanged();
    }

    public void PersistPublishedArtifactIfPossible()
    {
        if (IsDisposed
            || !isLevelSurfacePreviewPublished
            || isLevelSurfacePreviewStale
            || levelSurfacePreview is not
            {
                Output: { } output,
                Transform: { } transform,
                LevelFrame: { } levelFrame,
                QualityEvidence: { } qualityEvidence,
                FrameChain: { } frameChain
            }
            || string.IsNullOrWhiteSpace(getRecipePath())
            || string.IsNullOrWhiteSpace(levelSurfacePreviewStepId))
        {
            return;
        }

        try
        {
            var recipePath = getRecipePath()!;
            var recipeDirectory = GetRecipeDirectory(recipePath);
            var document = createDocument();
            var producers = document.Steps.Where(candidate =>
                    string.Equals(candidate.Id, levelSurfacePreviewStepId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.ToolId, "level-surface", StringComparison.Ordinal)
                    && string.Equals(candidate.OutputEntityId, output.EntityId, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            if (producers.Length != 1)
            {
                MarkProducerContextStale("The published Level Surface producer is missing or ambiguous in the current recipe.");
                return;
            }

            var step = producers[0];
            var contextReady = TryPrepareProducerContext(
                document, step.Id, recipeDirectory,
                out var input, out var currentContextSha256, out var message);
            if (!contextReady
                || string.IsNullOrWhiteSpace(levelSurfacePreviewProducerContextSha256)
                || !string.Equals(
                    levelSurfacePreviewProducerContextSha256,
                    currentContextSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                var reason = contextReady
                    ? "Source, reference ROI, routing, output, or leveling policy changed."
                    : message;
                MarkProducerContextStale(reason);
                return;
            }

            ValidateProducerEvidence(input!, transform, levelFrame, qualityEvidence);
            var c3dPath = GetArtifactC3DPath(recipeDirectory, recipePath, step.OutputEntityId);
            SaveC3DAtomically(output, c3dPath);
            var sidecarPath = GetArtifactSidecarPath(recipeDirectory, recipePath, step.OutputEntityId);
            var sidecar = new LevelSurfaceArtifactRecord(
                step.Id,
                step.OutputEntityId,
                output.EntityId,
                output.ContentSha256,
                new FileInfo(c3dPath).Length,
                output.Width,
                output.Height,
                output.Unit,
                output.FrameId,
                output.Provenance,
                levelSurfacePreview.OutputReferenceSlopeX,
                levelSurfacePreview.OutputReferenceSlopeZ,
                ToTransformRecord(transform),
                ToLevelFrameRecord(levelFrame),
                ToQualityRecord(qualityEvidence),
                ToFrameChainRecord(frameChain),
                ArtifactSchemaVersion,
                levelSurfacePreviewProducerContextSha256);
            WriteTextAtomically(
                sidecarPath,
                JsonSerializer.Serialize(sidecar, new JsonSerializerOptions { WriteIndented = true }));
            levelSurfacePreviewPath = c3dPath;
            if (IsDisposed)
            {
                return;
            }

            appendLog("Save", $"Level Surface output and Level Frame sidecar saved: {sidecarPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            if (IsDisposed)
            {
                return;
            }

            SetLevelSurfaceSummary($"Published in this session, but the Level Surface sidecar could not be saved: {exception.Message}");
            appendLog("Error", $"Level Surface sidecar save failed: {exception.Message}");
        }
    }

    private void MarkProducerContextStale(string reason)
    {
        isLevelSurfacePreviewStale = true;
        isLevelSurfacePreviewPublished = false;
        if (getSelectedPipelineStep() is { } selected
            && string.Equals(selected.Id, levelSurfacePreviewStepId, StringComparison.OrdinalIgnoreCase))
        {
            selected.State = "Preview stale";
        }

        SetLevelSurfaceSummary(
            $"Level Surface producer context is no longer current. Preview again before Publish. {reason}");
    }

    public void RestorePublishedArtifact()
    {
        if (IsDisposed)
        {
            return;
        }

        var recipePath = getRecipePath();
        var document = createDocument();
        var step = document.Steps.FirstOrDefault(candidate =>
            string.Equals(candidate.ToolId, "level-surface", StringComparison.Ordinal));
        if (step is null || string.IsNullOrWhiteSpace(recipePath))
        {
            return;
        }

        var recipeDirectory = GetRecipeDirectory(recipePath);
        var sidecarPath = GetArtifactSidecarPath(recipeDirectory, recipePath, step.OutputEntityId);
        var c3dPath = GetArtifactC3DPath(recipeDirectory, recipePath, step.OutputEntityId);
        // A partial modern pair is a failed publication; never mix naming versions.
        if (!File.Exists(sidecarPath) && !File.Exists(c3dPath))
        {
            var legacyId = SanitizeLegacy(step.OutputEntityId);
            if (document.Steps.Count(candidate =>
                    string.Equals(candidate.ToolId, "level-surface", StringComparison.Ordinal)
                    && string.Equals(SanitizeLegacy(candidate.OutputEntityId), legacyId, StringComparison.OrdinalIgnoreCase)) != 1)
            {
                appendLog("Warning", "Level Surface legacy artifact restore skipped: ambiguous output filename.");
                return;
            }
            sidecarPath = GetArtifactSidecarPath(recipeDirectory, recipePath, step.OutputEntityId, legacy: true);
            c3dPath = GetArtifactC3DPath(recipeDirectory, recipePath, step.OutputEntityId, legacy: true);
        }
        if (!File.Exists(sidecarPath) || !File.Exists(c3dPath))
        {
            return;
        }

        try
        {
            var record = JsonSerializer.Deserialize<LevelSurfaceArtifactRecord>(
                File.ReadAllText(sidecarPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Level Surface sidecar is empty.");
            if (IsDisposed)
            {
                return;
            }

            if (record.SchemaVersion != ArtifactSchemaVersion
                || record.ProducerContextSha256 is not { Length: 64 }
                || !record.ProducerContextSha256.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException("Level Surface sidecar has no supported producer context. Preview again before Publish.");
            }

            if (record.Transform is null || record.Frame is null
                || record.Quality is null || record.FrameChain is null)
            {
                throw new InvalidDataException("Level Surface sidecar producer evidence is incomplete.");
            }

            if (!string.Equals(record.StepId, step.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.OutputEntityId, record.OutputArtifactId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Level Surface sidecar and recipe output identities do not match.");
            }

            if (!TryPrepareProducerContext(
                    document, step.Id, recipeDirectory,
                    out var input, out var currentContextSha256, out var message))
            {
                throw new InvalidDataException($"Level Surface current producer context is unavailable: {message}");
            }

            if (!string.Equals(record.ProducerContextSha256, currentContextSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Level Surface sidecar producer context does not match the current source, reference ROIs, routing, or policies. Preview again before Publish.");
            }

            var source = input!.Source;
            if (!string.Equals(record.Transform.RootSourceEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.Transform.RootSourceSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.Frame.RootSourceEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.Frame.RootSourceSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || record.Width != source.Width
                || record.Height != source.Height
                || !string.Equals(record.Unit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(record.FrameId, source.FrameId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Level Surface sidecar source, grid, unit, or frame identity does not match the current recipe.");
            }

            var loaded = C3DHeightFieldSnapshot.LoadIdentified(
                c3dPath,
                record.OutputArtifactId,
                source.Unit,
                source.FrameId);
            if (loaded.ByteLength != record.ByteLength
                || !string.Equals(loaded.ContentSha256, record.OutputContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Level Surface sidecar output bytes do not match the recorded identity.");
            }

            var output = source.CreateDerived(
                step.OutputEntityId,
                loaded.Values.ToArray(),
                record.OutputProvenance);
            if (!string.Equals(output.ContentSha256, record.OutputContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Level Surface sidecar output hash could not be reproduced.");
            }

            var transform = CreateTransform(record.Transform);
            if (!string.Equals(transform.ContentSha256, record.Transform.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Level Surface sidecar leveling transform hash could not be reproduced.");
            }

            var levelFrame = CreateLevelFrame(record.Frame, transform);
            if (!string.Equals(levelFrame.ContentSha256, record.Frame.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Level Surface sidecar Level Frame hash could not be reproduced.");
            }

            var qualityEvidence = CreateQualityEvidence(record.Quality, levelFrame, transform);
            ValidateProducerEvidence(input, transform, levelFrame, qualityEvidence);
            var frameChain = C3DLevelSurfaceRule.CreateFrameChain(source, output, transform, levelFrame, step.Id);
            if (record.FrameChain is { } chainRecord
                && (!string.Equals(frameChain.ChainId, chainRecord.ChainId, StringComparison.Ordinal)
                    || !string.Equals(frameChain.Source.FrameId, chainRecord.SourceFrameId, StringComparison.Ordinal)
                    || !string.Equals(frameChain.Reference.FrameId, chainRecord.ReferenceFrameId, StringComparison.Ordinal)
                    || !string.Equals(frameChain.Result?.FrameId, chainRecord.ResultFrameId, StringComparison.Ordinal)
                    || !string.Equals(frameChain.Level.FrameId, chainRecord.LevelFrameId, StringComparison.Ordinal)
                    || !string.Equals(frameChain.RootSourceEntityId, chainRecord.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(frameChain.RootSourceSha256, chainRecord.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(frameChain.ContentSha256, chainRecord.ContentSha256, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("Level Surface sidecar frame chain does not match the current source, result, transform, and Level Frame.");
            }

            if (IsDisposed)
            {
                return;
            }

            levelSurfacePreview = new C3DLevelSurfaceEvaluation(
                new ToolResult(
                    C3DLevelSurfaceRule.ToolName,
                    ResultStatus.Pass,
                    "Restored Level Surface output, leveling transform, and Level Frame from sidecar without executing the tool.",
                    TimeSpan.Zero,
                    [],
                    []),
                output,
                transform,
                record.OutputReferenceSlopeX,
                record.OutputReferenceSlopeZ,
                levelFrame,
                qualityEvidence,
                frameChain);
            levelSurfacePreviewPath = c3dPath;
            levelSurfacePreviewStepId = step.Id;
            levelSurfacePreviewProducerContextSha256 = currentContextSha256;
            isLevelSurfacePreviewStale = false;
            isLevelSurfacePreviewPublished = true;
            if (getSelectedPipelineStep() is { } selected
                && string.Equals(selected.Id, step.Id, StringComparison.OrdinalIgnoreCase))
            {
                selected.State = "Published";
            }

            SetLevelSurfaceSummary(
                $"Restored Published Level Surface output {output.EntityId}, Level Frame {levelFrame.ContentSha256[..12]}, and frame chain {frameChain.ContentSha256[..12]} without executing the tool.");
            appendLog("Open", $"Level Surface and Level Frame sidecar restored: {sidecarPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or JsonException)
        {
            if (IsDisposed)
            {
                return;
            }

            levelSurfacePreview = null;
            levelSurfacePreviewPath = null;
            levelSurfacePreviewStepId = null;
            levelSurfacePreviewProducerContextSha256 = null;
            isLevelSurfacePreviewStale = true;
            isLevelSurfacePreviewPublished = false;
            if (getSelectedPipelineStep() is { } selected
                && string.Equals(selected.Id, step.Id, StringComparison.OrdinalIgnoreCase))
            {
                selected.State = "Preview stale";
            }

            SetLevelSurfaceSummary($"Saved Level Surface output was not restored: {exception.Message}");
            appendLog("Warning", $"Level Surface sidecar restore skipped: {exception.Message}");
        }
    }

    private void SetLevelSurfaceSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        levelSurfaceExecutionSummary = value;
        onExecutionStateChanged();
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref levelSurfacePreviewCancellation),
            cancellation);

    private string GetRecipeDirectory(string? recipePath = null)
    {
        var path = recipePath ?? getRecipePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return Environment.CurrentDirectory;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return string.IsNullOrWhiteSpace(directory)
            ? Environment.CurrentDirectory
            : directory;
    }

    private static string CreateLevelSurfacePreviewPath(string hash)
    {
        var testArtifactRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var directory = Path.Combine(
            string.IsNullOrWhiteSpace(testArtifactRoot)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpenVisionLab",
                    "3DStudio")
                : Path.GetFullPath(testArtifactRoot),
            "Preview");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"level-surface-{hash}.c3d");
    }

    private static void SaveC3DAtomically(C3DHeightFieldSnapshot output, string path)
        => WriteAtomically(path, output.SaveC3D);

    private static void WriteTextAtomically(string path, string contents)
        => WriteAtomically(path, temporaryPath => File.WriteAllText(temporaryPath, contents));

    private static void WriteAtomically(string path, Action<string> write)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            write(temporaryPath);
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

    private static string GetArtifactC3DPath(string recipeDirectory, string recipePath, string outputEntityId, bool legacy = false) =>
        Path.Combine(
            recipeDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.level-surface.{(legacy ? SanitizeLegacy(outputEntityId) : Sanitize(outputEntityId))}.c3d");

    private static string GetArtifactSidecarPath(string recipeDirectory, string recipePath, string outputEntityId, bool legacy = false) =>
        Path.Combine(
            recipeDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.level-surface.{(legacy ? SanitizeLegacy(outputEntityId) : Sanitize(outputEntityId))}.json");

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

    private static (C3DLevelSurfaceEvaluation Evaluation, string? ProducerContextSha256) ExecutePreview(
        ToolRecipeDocument document,
        string stepId,
        string recipeDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPrepareProducerContext(
                document, stepId, recipeDirectory,
                out var input, out var contextSha256, out var message))
        {
            return (new C3DLevelSurfaceEvaluation(
                new ToolResult(C3DLevelSurfaceRule.ToolName, ResultStatus.Error, message, TimeSpan.Zero, [], []),
                null, null, double.NaN, double.NaN), null);
        }

        // Capture the context of the exact prepared input passed to the existing
        // rule. Save/Open may validate inputs, but never execute this rule.
        return (C3DLevelSurfaceRule.Evaluate(input!, cancellationToken), contextSha256);
    }

    private static bool TryPrepareProducerContext(
        ToolRecipeDocument document,
        string stepId,
        string recipeDirectory,
        out C3DLevelSurfaceInput? input,
        out string? contextSha256,
        out string message)
    {
        input = null;
        contextSha256 = null;
        try
        {
            if (!ToolRecipeLevelSurfaceExecution.TryPrepare(document, stepId, recipeDirectory, out input, out message))
            {
                return false;
            }

            contextSha256 = CreateProducerContextSha256(input!);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            input = null;
            message = exception.Message;
            return false;
        }
    }

    private static string CreateProducerContextSha256(C3DLevelSurfaceInput input) =>
        CalculateContextSha256(writer =>
        {
            writer.Write("OpenVisionLab.LevelSurface.ProducerExecutionContext.v1");
            writer.Write(input.StepId);
            writer.Write("level-surface");
            writer.Write(input.OutputEntityId);
            var source = input.Source;
            writer.Write("C3D");
            writer.Write(source.EntityId);
            writer.Write(source.ByteLength);
            writer.Write(source.ContentSha256.ToUpperInvariant());
            writer.Write(source.RootSourceSha256.ToUpperInvariant());
            writer.Write(source.Width);
            writer.Write(source.Height);
            writer.Write(source.GridOriginColumn);
            writer.Write(source.GridOriginRow);
            writer.Write(source.Unit);
            writer.Write(source.FrameId);
            writer.Write(source.ScalarMeaning);
            writer.Write(input.ReferenceSelections.Count);
            foreach (var selection in input.ReferenceSelections)
            {
                // Route order and the exact rectangle/source-binding payload
                // belong to the producer, even when a selection ID is reused.
                writer.Write(selection.Id);
                writer.Write(CalculateContextSha256(selectionWriter =>
                {
                    selectionWriter.Write(selection.Id);
                    selectionWriter.Write(selection.Kind);
                    selectionWriter.Write(selection.RootSourceId);
                    selectionWriter.Write(selection.FrameId);
                    var binding = selection.SourceBinding;
                    selectionWriter.Write(binding.Format);
                    selectionWriter.Write(binding.ContentSha256.ToUpperInvariant());
                    selectionWriter.Write(binding.GridWidth);
                    selectionWriter.Write(binding.GridHeight);
                    selectionWriter.Write(binding.OwnerEntityId ?? string.Empty);
                    selectionWriter.Write((binding.RootSourceContentSha256 ?? string.Empty).ToUpperInvariant());
                    selectionWriter.Write(binding.Unit ?? string.Empty);
                    selectionWriter.Write(binding.FrameId ?? string.Empty);
                    var rectangle = selection.GridRectangle!;
                    selectionWriter.Write(rectangle.Row);
                    selectionWriter.Write(rectangle.Column);
                    selectionWriter.Write(rectangle.RowCount);
                    selectionWriter.Write(rectangle.ColumnCount);
                }));
            }

            // TryPrepare has validated all six authored parameters. Numeric
            // spellings are canonicalized by using their parsed typed values.
            writer.Write(C3DLevelingTransform.ContractVersion);
            writer.Write(C3DLevelingTransform.ReferenceFitPolicy);
            writer.Write(C3DLevelingTransform.LevelingPolicy);
            writer.Write(C3DLevelingTransform.MissingValuePolicy);
            writer.Write(C3DLevelingTransform.GridPolicy);
            writer.Write(input.MinimumValidSampleCount);
            writer.Write(input.MaximumReferenceRmsResidual);
            writer.Write(C3DLevelFrameArtifact.ContractVersion);
            writer.Write(C3DLevelFrameArtifact.FramePolicy);
            writer.Write(C3DLevelFrameQualityEvidence.ContractVersion);
            writer.Write(C3DLevelFrameQualityEvidence.CoverageSemantics);
            writer.Write(C3DLevelFrameQualityEvidence.ConfidenceSemantics);
            writer.Write(C3DLevelFrameQualityPolicy.PolicyId);
            var qualityPolicy = C3DLevelFrameQualityPolicy.CompleteCoverage(input.MaximumReferenceRmsResidual);
            writer.Write(qualityPolicy.MinimumReferenceCoverageRatio);
            writer.Write(qualityPolicy.MaximumReferenceRmsResidual);
        });

    private static string CalculateContextSha256(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            write(writer);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void ValidateProducerEvidence(
        C3DLevelSurfaceInput input,
        C3DLevelingTransform transform,
        C3DLevelFrameArtifact frame,
        C3DLevelFrameQualityEvidence quality)
    {
        if (!string.Equals(transform.OutputEntityId, $"{input.OutputEntityId}.transform", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(frame.OutputEntityId, $"{input.OutputEntityId}.level-frame", StringComparison.OrdinalIgnoreCase)
            || transform.ReferenceRegions.Count != input.ReferenceSelections.Count
            || transform.ReferenceSampleCount < input.MinimumValidSampleCount
            || transform.ReferenceResidualRms > input.MaximumReferenceRmsResidual
            || quality.Policy != C3DLevelFrameQualityPolicy.CompleteCoverage(input.MaximumReferenceRmsResidual))
        {
            throw new InvalidDataException("Level Surface evidence does not match the current producer output or fit/quality policies.");
        }

        for (var index = 0; index < input.ReferenceSelections.Count; index++)
        {
            var selection = input.ReferenceSelections[index];
            var rectangle = selection.GridRectangle!;
            var region = transform.ReferenceRegions[index];
            if (!string.Equals(region.SelectionId, selection.Id, StringComparison.OrdinalIgnoreCase)
                || region.Row != rectangle.Row || region.Column != rectangle.Column
                || region.RowCount != rectangle.RowCount || region.ColumnCount != rectangle.ColumnCount)
            {
                throw new InvalidDataException("Level Surface evidence does not match the current reference ROI geometry.");
            }
        }
    }

    private static LevelingTransformSidecarRecord ToTransformRecord(C3DLevelingTransform transform) =>
        new(
            transform.OutputEntityId,
            transform.RootSourceEntityId,
            transform.RootSourceSha256,
            transform.SourceUnit,
            transform.SourceFrameId,
            transform.SourceGridWidth,
            transform.SourceGridHeight,
            transform.Matrix.Values.ToArray(),
            transform.FittedSlopeX,
            transform.FittedSlopeZ,
            transform.FittedIntercept,
            transform.TargetHeight,
            transform.ReferenceSampleCount,
            transform.ReferenceResidualRms,
            transform.ReferenceResidualPeakToValley,
            transform.ReferenceRegions.ToArray(),
            transform.Provenance,
            transform.ContentSha256);

    private static LevelFrameSidecarRecord ToLevelFrameRecord(C3DLevelFrameArtifact frame) =>
        new(
            frame.OutputEntityId,
            frame.LevelFrameId,
            frame.RootSourceEntityId,
            frame.RootSourceSha256,
            frame.SourceUnit,
            frame.SourceFrameId,
            frame.SourceGridWidth,
            frame.SourceGridHeight,
            frame.LevelingTransformEntityId,
            frame.LevelingTransformContentSha256,
            frame.SourceToFrame.Values.ToArray(),
            ToVectorRecord(frame.Origin),
            ToVectorRecord(frame.UAxis),
            ToVectorRecord(frame.VAxis),
            ToVectorRecord(frame.HAxis),
            frame.Provenance,
            frame.ContentSha256);

    private static QualityEvidenceSidecarRecord? ToQualityRecord(
        C3DLevelFrameQualityEvidence? quality)
    {
        if (quality is null)
        {
            return null;
        }

        return new QualityEvidenceSidecarRecord(
            quality.LevelFrameId,
            quality.LevelFrameContentSha256,
            quality.LevelingTransformEntityId,
            quality.LevelingTransformContentSha256,
            quality.Policy.MinimumReferenceCoverageRatio,
            quality.Policy.MaximumReferenceRmsResidual,
            quality.MinimumObservedCoverageRatio,
            quality.State,
            quality.Reason,
            quality.ReferenceCoverage.ToArray(),
            quality.Provenance,
            quality.ContentSha256);
    }

    private static FrameChainSidecarRecord ToFrameChainRecord(
        C3DLevelSurfaceCoordinateFrameChain chain) =>
        new(
            chain.ChainId,
            chain.Source.FrameId,
            chain.Reference.FrameId,
            chain.Result?.FrameId,
            chain.Level.FrameId,
            chain.RootSourceEntityId,
            chain.RootSourceSha256,
            chain.ContentSha256);

    private static VectorSidecarRecord ToVectorRecord(C3DReferenceGridVector value) =>
        new(value.X, value.Y, value.Z);

    private static C3DLevelingTransform CreateTransform(LevelingTransformSidecarRecord record)
    {
        if (record.Matrix is null || record.Matrix.Length != 12)
        {
            throw new InvalidDataException("Level Surface sidecar transform matrix must contain 12 values.");
        }

        var transform = C3DLevelingTransform.Create(
            record.OutputEntityId,
            record.RootSourceEntityId,
            record.RootSourceSha256,
            record.SourceUnit,
            record.SourceFrameId,
            record.SourceGridWidth,
            record.SourceGridHeight,
            record.FittedSlopeX,
            record.FittedSlopeZ,
            record.FittedIntercept,
            record.TargetHeight,
            record.ReferenceSampleCount,
            record.ReferenceResidualRms,
            record.ReferenceResidualPeakToValley,
            record.ReferenceRegions ?? [],
            record.Provenance);
        if (!transform.Matrix.Values.SequenceEqual(record.Matrix))
        {
            throw new InvalidDataException("Level Surface sidecar transform matrix does not match its typed plane.");
        }

        return transform;
    }

    private static C3DLevelFrameArtifact CreateLevelFrame(
        LevelFrameSidecarRecord record,
        C3DLevelingTransform transform)
    {
        if (record.Matrix is null || record.Matrix.Length != 12
            || record.Origin is null
            || record.UAxis is null
            || record.VAxis is null
            || record.HAxis is null)
        {
            throw new InvalidDataException("Level Surface sidecar Level Frame geometry is incomplete.");
        }

        var frame = C3DLevelFrameArtifact.Create(
            record.OutputEntityId,
            record.LevelFrameId,
            transform,
            new C3DAffineMatrix3x4(
                record.Matrix[0], record.Matrix[1], record.Matrix[2], record.Matrix[3],
                record.Matrix[4], record.Matrix[5], record.Matrix[6], record.Matrix[7],
                record.Matrix[8], record.Matrix[9], record.Matrix[10], record.Matrix[11]),
            new C3DReferenceGridVector(record.Origin.X, record.Origin.Y, record.Origin.Z),
            new C3DReferenceGridVector(record.UAxis.X, record.UAxis.Y, record.UAxis.Z),
            new C3DReferenceGridVector(record.VAxis.X, record.VAxis.Y, record.VAxis.Z),
            new C3DReferenceGridVector(record.HAxis.X, record.HAxis.Y, record.HAxis.Z),
            record.Provenance);
        if (!string.Equals(frame.LevelingTransformEntityId, record.LevelingTransformEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(frame.LevelingTransformContentSha256, record.LevelingTransformContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Level Surface sidecar Level Frame transform link does not match.");
        }

        return frame;
    }

    private static C3DLevelFrameQualityEvidence CreateQualityEvidence(
        QualityEvidenceSidecarRecord record,
        C3DLevelFrameArtifact levelFrame,
        C3DLevelingTransform transform)
    {
        var quality = C3DLevelFrameQualityEvidence.Create(
            levelFrame,
            transform,
            new C3DLevelFrameQualityPolicy(
                record.MinimumReferenceCoverageRatio,
                record.MaximumReferenceRmsResidual),
            record.Provenance);
        if (!string.Equals(quality.LevelFrameId, record.LevelFrameId, StringComparison.Ordinal)
            || !string.Equals(quality.LevelFrameContentSha256, record.LevelFrameContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(quality.LevelingTransformEntityId, record.LevelingTransformEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(quality.LevelingTransformContentSha256, record.LevelingTransformContentSha256, StringComparison.OrdinalIgnoreCase)
            || Math.Abs(quality.MinimumObservedCoverageRatio - record.MinimumObservedCoverageRatio) > 1e-12
            || quality.State != record.State
            || quality.Reason != record.Reason
            || !(record.ReferenceCoverage ?? []).SequenceEqual(quality.ReferenceCoverage)
            || !string.Equals(quality.ContentSha256, record.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Level Surface sidecar quality evidence does not match the linked Level Frame and LevelingTransform.");
        }

        return quality;
    }

    private sealed record LevelSurfaceArtifactRecord(
        string StepId,
        string OutputEntityId,
        string OutputArtifactId,
        string OutputContentSha256,
        long ByteLength,
        int Width,
        int Height,
        string Unit,
        string FrameId,
        string OutputProvenance,
        double OutputReferenceSlopeX,
        double OutputReferenceSlopeZ,
        LevelingTransformSidecarRecord Transform,
        LevelFrameSidecarRecord Frame,
        QualityEvidenceSidecarRecord? Quality = null,
        FrameChainSidecarRecord? FrameChain = null,
        int SchemaVersion = 0,
        string? ProducerContextSha256 = null);

    private sealed record LevelingTransformSidecarRecord(
        string OutputEntityId,
        string RootSourceEntityId,
        string RootSourceSha256,
        string SourceUnit,
        string SourceFrameId,
        int SourceGridWidth,
        int SourceGridHeight,
        double[] Matrix,
        double FittedSlopeX,
        double FittedSlopeZ,
        double FittedIntercept,
        double TargetHeight,
        int ReferenceSampleCount,
        double ReferenceResidualRms,
        double ReferenceResidualPeakToValley,
        C3DLevelingReferenceRegion[] ReferenceRegions,
        string Provenance,
        string ContentSha256);

    private sealed record LevelFrameSidecarRecord(
        string OutputEntityId,
        string LevelFrameId,
        string RootSourceEntityId,
        string RootSourceSha256,
        string SourceUnit,
        string SourceFrameId,
        int SourceGridWidth,
        int SourceGridHeight,
        string LevelingTransformEntityId,
        string LevelingTransformContentSha256,
        double[] Matrix,
        VectorSidecarRecord Origin,
        VectorSidecarRecord UAxis,
        VectorSidecarRecord VAxis,
        VectorSidecarRecord HAxis,
        string Provenance,
        string ContentSha256);

    private sealed record QualityEvidenceSidecarRecord(
        string LevelFrameId,
        string LevelFrameContentSha256,
        string LevelingTransformEntityId,
        string LevelingTransformContentSha256,
        double MinimumReferenceCoverageRatio,
        double MaximumReferenceRmsResidual,
        double MinimumObservedCoverageRatio,
        C3DLevelFrameQualityState State,
        C3DLevelFrameQualityReason Reason,
        C3DLevelFrameReferenceCoverage[] ReferenceCoverage,
        string Provenance,
        string ContentSha256);

    private sealed record FrameChainSidecarRecord(
        string ChainId,
        string SourceFrameId,
        string ReferenceFrameId,
        string? ResultFrameId,
        string LevelFrameId,
        string RootSourceEntityId,
        string RootSourceSha256,
        string ContentSha256);

    private sealed record VectorSidecarRecord(double X, double Y, double Z);
}
