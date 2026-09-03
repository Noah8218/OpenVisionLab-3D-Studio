using System.IO;
using System.Text.Json;
using System.Threading;
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
            : $"Transform {transform.ContentSha256} | input slope X {transform.FittedSlopeX:G6}, Z {transform.FittedSlopeZ:G6} | target {transform.TargetHeight:G6} {transform.SourceUnit}";

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
            : $"Reference RMS {transform.ReferenceResidualRms:G6} | P2V {transform.ReferenceResidualPeakToValley:G6} | output slope X {levelSurfacePreview.OutputReferenceSlopeX:G6}, Z {levelSurfacePreview.OutputReferenceSlopeZ:G6} | software confidence {levelSurfacePreview.QualityEvidence?.State.ToString() ?? "Unavailable"} | minimum reference coverage {levelSurfacePreview.QualityEvidence?.MinimumObservedCoverageRatio.ToString("P1") ?? "Unavailable"}";

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
            var evaluation = await Task.Run(
                () => ToolRecipeLevelSurfaceExecution.Execute(
                    document,
                    step.Id,
                    recipeDirectory,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass
                || evaluation.Output is null
                || evaluation.Transform is null
                || evaluation.LevelFrame is null
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
            evaluation.Output.SaveC3D(levelSurfacePreviewPath);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Preview ready";
            SetLevelSurfaceSummary(
                $"Preview ready | {evaluation.Transform.ReferenceRegions.Count} reference ROI(s) | input slope X {evaluation.Transform.FittedSlopeX:G6}, Z {evaluation.Transform.FittedSlopeZ:G6} | output slope X {evaluation.OutputReferenceSlopeX:G6}, Z {evaluation.OutputReferenceSlopeZ:G6} | level frame {evaluation.LevelFrame.ContentSha256[..12]} | frame chain {evaluation.FrameChain.ContentSha256[..12]} | source unchanged");
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
            || !HasCurrentLevelSurfacePreview)
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
            if (selected is not null)
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
                FrameChain: { } frameChain
            }
            || string.IsNullOrWhiteSpace(getRecipePath())
            || getSelectedPipelineStep() is not { } step)
        {
            return;
        }

        try
        {
            var recipePath = getRecipePath()!;
            var c3dPath = GetArtifactC3DPath(recipePath, step.OutputEntityId);
            output.SaveC3D(c3dPath);
            var sidecarPath = GetArtifactSidecarPath(recipePath, step.OutputEntityId);
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
                ToQualityRecord(levelSurfacePreview.QualityEvidence),
                ToFrameChainRecord(frameChain));
            File.WriteAllText(
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

        var sidecarPath = GetArtifactSidecarPath(recipePath, step.OutputEntityId);
        var c3dPath = GetArtifactC3DPath(recipePath, step.OutputEntityId);
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

            if (!string.Equals(record.StepId, step.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.OutputEntityId, record.OutputArtifactId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Level Surface sidecar and recipe output identities do not match.");
            }

            var source = LoadRecipeSource(document);
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

            var qualityEvidence = record.Quality is null
                ? null
                : CreateQualityEvidence(record.Quality, levelFrame, transform);
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

    private string GetRecipeDirectory()
    {
        var path = getRecipePath();
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

    private static string GetArtifactC3DPath(string recipePath, string outputEntityId) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(recipePath)) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.level-surface.{Sanitize(outputEntityId)}.c3d");

    private static string GetArtifactSidecarPath(string recipePath, string outputEntityId) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(recipePath)) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.level-surface.{Sanitize(outputEntityId)}.json");

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

    private static C3DHeightFieldSnapshot LoadRecipeSource(ToolRecipeDocument document)
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
            throw new InvalidDataException("The Level Surface recipe source identity is incomplete.");
        }

        return C3DHeightFieldSnapshot.LoadVerified(
            sourcePath,
            document.Source.Id,
            document.Source.Unit,
            document.Source.FrameId,
            byteLength,
            contentSha256,
            gridWidth,
            gridHeight);
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
        FrameChainSidecarRecord? FrameChain = null);

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
