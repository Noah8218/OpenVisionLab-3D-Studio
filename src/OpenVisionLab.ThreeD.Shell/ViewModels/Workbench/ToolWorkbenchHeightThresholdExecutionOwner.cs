using System.IO;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Height Threshold Background Removal Preview lifecycle. The rule and
/// recipe adapter remain outside the ViewModel; this owner only coordinates
/// cancellation, stale state, Preview persistence, and explicit Publish.
/// </summary>
internal sealed class ToolWorkbenchHeightThresholdExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelectedStep;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DHeightThresholdBackgroundRemovalEvaluation? preview;
    private string? previewPath;
    private bool isRunning;
    private bool isStale;
    private bool isPublished;
    private string executionSummary =
        "Select Height Threshold Background Removal, review its inclusive rule, then Preview.";
    private int disposalState;

    public ToolWorkbenchHeightThresholdExecutionOwner(
        Func<bool> isSelectedStep,
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
        this.isSelectedStep = isSelectedStep;
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

    public bool IsSelectedStep => !IsDisposed && isSelectedStep();
    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;
    public bool IsPreviewRunning => !IsDisposed && isRunning;
    public bool HasCurrentPreview => !IsDisposed && preview?.Output is not null && !isStale;
    public bool IsPreviewStale => !IsDisposed && isStale;
    public bool IsPreviewPublished => !IsDisposed && isPublished;
    public C3DHeightFieldSnapshot? CurrentOutput => IsDisposed ? null : preview?.Output;
    public C3DHeightThresholdBackgroundRemovalEvidence? CurrentEvidence => IsDisposed ? null : preview?.Evidence;
    public string? CurrentOutputPath => IsDisposed ? null : previewPath;
    public string ExecutionSummary => IsDisposed
        ? "Height Threshold Background Removal execution owner has been disposed."
        : executionSummary;

    public string RuleSummary
    {
        get
        {
            if (IsDisposed)
            {
                return "Height Threshold Background Removal execution owner has been disposed.";
            }

            var step = getSelectedPipelineStep();
            if (!IsSelectedStep || step is null)
            {
                return "No authored height-threshold rule.";
            }

            static string Value(ToolWorkbenchPipelineStepItem item, string name) =>
                item.Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Value ?? "?";

            return $"{Value(step, "Mode")} | threshold {Value(step, "Threshold")} {createDocument().Source.Unit} | inclusive | existing missing preserved";
        }
    }

    public string OutputSummary =>
        IsDisposed || preview is not { Output: { } output, Evidence: { } evidence }
            ? "No height-threshold Preview output."
            : $"Retained {evidence.RetainedValidSampleCount:N0} | removed {evidence.RemovedBackgroundSampleCount:N0} | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isPublished ? "Published" : "Preview only")}";

    public string EvidenceSummary =>
        IsDisposed || preview?.Evidence is not { } evidence
            ? "Threshold evidence SHA-256 is available after Preview."
            : $"Evidence SHA-256 {evidence.ContentSha256} | output SHA-256 {evidence.OutputContentSha256}";

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
        isRunning = false;
        isStale = false;
        isPublished = false;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var cancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref previewCancellation, cancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            CancelAndDispose(cancellation);
            return false;
        }

        SetRunning(true);
        isStale = false;
        isPublished = false;
        step.State = "Preview running";
        SetSummary("Height Threshold Background Removal Preview is evaluating the verified source without changing it.");
        appendLog("Preview", $"Height Threshold Background Removal Preview started: {step.Id}.");

        try
        {
            var document = createDocument();
            var evaluation = await Task.Run(
                () => ToolRecipeHeightThresholdBackgroundRemovalExecution.Execute(
                    document,
                    step.Id,
                    GetRecipeDirectory(),
                    cancellation.Token),
                cancellation.Token);

            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            if (evaluation.Result.Status is not (ResultStatus.Pass or ResultStatus.Warning)
                || evaluation.Output is null
                || evaluation.Evidence is null)
            {
                preview = null;
                previewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Height Threshold Background Removal Preview failed: {evaluation.Result.Message}");
                return false;
            }

            preview = evaluation;
            previewPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            SaveC3DAtomically(evaluation.Output, previewPath);
            step.State = evaluation.Result.Status == ResultStatus.Warning ? "Preview warning" : "Preview ready";
            SetSummary($"Preview ready | retained {evaluation.Evidence.RetainedValidSampleCount:N0} | removed {evaluation.Evidence.RemovedBackgroundSampleCount:N0} | source unchanged");
            appendLog("Preview", $"Height Threshold Background Removal Preview ready: output={evaluation.Output.ContentSha256}; evidence={evaluation.Evidence.ContentSha256}.");
            requestDisplay(new ToolWorkbenchFilterDisplayRequestEventArgs(
                previewPath,
                evaluation.Output.ContentSha256,
                false,
                "Height Threshold Background Removal Preview"));
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetSummary("Preview canceled. Source and authored recipe were not changed.");
            appendLog("Preview", "Height Threshold Background Removal Preview canceled.");
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
                if (!IsDisposed)
                {
                    SetRunning(false);
                }
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
        if (IsDisposed || getSelectedPipelineStep() is not { } step || !HasCurrentPreview)
        {
            return;
        }

        isPublished = true;
        step.State = "Published";
        SetSummary($"Published output {step.OutputEntityId} | output SHA-256 {preview!.Output!.ContentSha256} | source unchanged");
        appendLog("Publish", $"Height Threshold Background Removal output published without re-running: {step.OutputEntityId}.");
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
        if (IsDisposed || preview is null || isRunning)
        {
            return;
        }

        var selected = getSelectedPipelineStep();
        var selectedParameter = IsSelectedStep
            && sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        if (selected is { } step
            && (isSourceChangeEvent(sender) || ReferenceEquals(sender, step) || selectedParameter))
        {
            isStale = true;
            isPublished = false;
            step.State = "Preview stale";
            SetSummary("Source, routing, output, or threshold rule changed. Preview again before Publish.");
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
        isStale = false;
        isPublished = false;
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
            && !isRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid
                ? "Ready"
                : "Taught / needs correction";
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

    internal bool TryGetPublishedOutput(string entityId, out C3DHeightFieldSnapshot? output)
    {
        output = null;
        if (!IsDisposed
            && isPublished
            && !isStale
            && preview?.Output is { } current
            && string.Equals(current.EntityId, entityId, StringComparison.OrdinalIgnoreCase))
        {
            output = current;
        }

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

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(Volatile.Read(ref previewCancellation), cancellation);

    private string GetRecipeDirectory()
    {
        var path = getRecipePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return Environment.CurrentDirectory;
        }

        return Path.GetDirectoryName(Path.GetFullPath(path)) ?? Environment.CurrentDirectory;
    }

    private static string CreatePreviewPath(string hash)
    {
        var root = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var directory = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenVisionLab", "3DStudio", "Preview")
            : Path.Combine(Path.GetFullPath(root), "Preview");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"height-threshold-{hash}.c3d");
    }

    private static void SaveC3DAtomically(C3DHeightFieldSnapshot output, string path)
    {
        var temporaryPath = $"{path}.tmp.{Guid.NewGuid():N}";
        try
        {
            output.SaveC3D(temporaryPath);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
