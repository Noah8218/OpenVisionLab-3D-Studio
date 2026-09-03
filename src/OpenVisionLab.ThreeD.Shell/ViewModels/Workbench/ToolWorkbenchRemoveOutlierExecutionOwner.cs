using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns Remove Outlier Pixels Preview state and cancellation. Each asynchronous
/// operation must still own the registered token before it can update the
/// downstream Connected Region input or Workbench state.
/// </summary>
internal sealed class ToolWorkbenchRemoveOutlierExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelectedStepRemoveOutlier;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? removeOutlierPreviewCancellation;
    private C3DRemoveOutlierPixelsEvaluation? removeOutlierPreview;
    private string? removeOutlierPreviewPath;
    private bool isRemoveOutlierPreviewRunning;
    private bool isRemoveOutlierPreviewStale;
    private bool isRemoveOutlierPreviewPublished;
    private string removeOutlierExecutionSummary =
        "Select Remove Outlier Pixels, review its explicit rule, then Preview.";
    private int disposalState;

    public ToolWorkbenchRemoveOutlierExecutionOwner(
        Func<bool> isSelectedStepRemoveOutlier,
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
        this.isSelectedStepRemoveOutlier = isSelectedStepRemoveOutlier;
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

    public bool IsSelectedStepRemoveOutlierPixels => !IsDisposed && isSelectedStepRemoveOutlier();

    public bool IsRemoveOutlierPreviewRunning => !IsDisposed && isRemoveOutlierPreviewRunning;
    public bool HasCurrentRemoveOutlierPreview =>
        !IsDisposed && removeOutlierPreview?.Output is not null && !isRemoveOutlierPreviewStale;
    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;
    public bool IsRemoveOutlierPreviewStale => !IsDisposed && isRemoveOutlierPreviewStale;
    public bool IsRemoveOutlierPreviewPublished => !IsDisposed && isRemoveOutlierPreviewPublished;
    public C3DHeightFieldSnapshot? CurrentRemoveOutlierPreviewOutput => IsDisposed ? null : removeOutlierPreview?.Output;
    public C3DOutlierCellMap? CurrentRemoveOutlierMask => IsDisposed ? null : removeOutlierPreview?.OutlierMask;
    public string? CurrentRemoveOutlierPreviewPath => IsDisposed ? null : removeOutlierPreviewPath;
    public string RemoveOutlierExecutionSummary => IsDisposed
        ? "Remove Outlier execution owner has been disposed."
        : removeOutlierExecutionSummary;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(
            ref removeOutlierPreviewCancellation,
            null);
        CancelAndDispose(cancellation);
        removeOutlierPreview = null;
        removeOutlierPreviewPath = null;
        isRemoveOutlierPreviewRunning = false;
        isRemoveOutlierPreviewStale = false;
        isRemoveOutlierPreviewPublished = false;
    }

    public string RemoveOutlierRuleSummary
    {
        get
        {
            if (IsDisposed)
            {
                return "Remove Outlier execution owner has been disposed.";
            }

            var step = getSelectedPipelineStep();
            if (!isSelectedStepRemoveOutlier() || step is null)
            {
                return "No authored outlier rule.";
            }

            static string Value(ToolWorkbenchPipelineStepItem item, string name) =>
                item.Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Value ?? "?";

            return
                $"{Value(step, "Rule")} | {Value(step, "WindowSize")} x {Value(step, "WindowSize")} | threshold > {Value(step, "MaximumAbsoluteDeviation")} {createDocument().Source.Unit} | minimum neighbors {Value(step, "MinimumValidNeighbors")}";
        }
    }

    public string RemoveOutlierOutputSummary =>
        IsDisposed || removeOutlierPreview is not { Output: { } output, OutlierMask: { } mask }
            ? "No outlier-removal Preview output."
            : $"Removed {mask.OutlierCellCount:N0} | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isRemoveOutlierPreviewPublished ? "Published" : "Preview only")}";

    public string RemoveOutlierMaskSummary =>
        IsDisposed || removeOutlierPreview?.OutlierMask is not { } mask
            ? "Outlier mask SHA-256 is available after Preview."
            : $"Outlier mask SHA-256 {mask.Sha256}";

    internal (C3DHeightFieldSnapshot Output, C3DOutlierCellMap Mask)? TryGetPublishedInput(
        string entityId)
    {
        if (IsDisposed
            || !isRemoveOutlierPreviewPublished
            || isRemoveOutlierPreviewStale
            || removeOutlierPreview?.Output is not { } output
            || removeOutlierPreview.OutlierMask is not { } mask
            || !string.Equals(output.EntityId, entityId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return (output, mask);
    }

    public async Task<bool> PreviewSelectedRemoveOutlierPixelsAsync()
    {
        if (IsDisposed
            || !CanPreviewSelectedRemoveOutlierPixels()
            || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref removeOutlierPreviewCancellation,
            cancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref removeOutlierPreviewCancellation,
                    null,
                    cancellation),
                cancellation))
            {
                cancellation.Dispose();
            }

            return false;
        }

        SetRemoveOutlierRunning(true);
        isRemoveOutlierPreviewStale = false;
        isRemoveOutlierPreviewPublished = false;
        step.State = "Preview running";
        SetRemoveOutlierSummary(
            "Remove Outlier Pixels Preview is evaluating the verified source without changing it.");
        if (!IsCurrentPreview(cancellation))
        {
            return false;
        }

        appendLog(
            "Preview",
            $"Remove Outlier Pixels Preview started: {step.Id}.");

        try
        {
            var document = createDocument();
            var recipeDirectory = GetRecipeDirectory();
            var evaluation = await Task.Run(
                () => ToolRecipeRemoveOutlierPixelsExecution.Execute(
                    document,
                    step.Id,
                    recipeDirectory,
                    cancellationToken),
                cancellationToken);

            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass
                || evaluation.Output is null
                || evaluation.OutlierMask is null)
            {
                removeOutlierPreview = null;
                removeOutlierPreviewPath = null;
                step.State = "Error";
                SetRemoveOutlierSummary(evaluation.Result.Message);
                if (IsCurrentPreview(cancellation))
                {
                    appendLog(
                        "Error",
                        $"Remove Outlier Pixels Preview failed: {evaluation.Result.Message}");
                }

                return false;
            }

            removeOutlierPreview = evaluation;
            removeOutlierPreviewPath =
                CreateRemoveOutlierPreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(removeOutlierPreviewPath);
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            step.State = "Preview ready";
            SetRemoveOutlierSummary(
                $"Preview ready | input valid {evaluation.Output.ValidCount + evaluation.OutlierMask.OutlierCellCount:N0} | removed {evaluation.OutlierMask.OutlierCellCount:N0} | output valid {evaluation.Output.ValidCount:N0} | source unchanged");
            if (IsCurrentPreview(cancellation))
            {
                appendLog(
                    "Preview",
                    $"Remove Outlier Pixels Preview ready: output={evaluation.Output.ContentSha256}; mask={evaluation.OutlierMask.Sha256}; removed={evaluation.OutlierMask.OutlierCellCount}.");
            }

            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            requestDisplay(
                new ToolWorkbenchFilterDisplayRequestEventArgs(
                    removeOutlierPreviewPath,
                    evaluation.Output.ContentSha256,
                    false,
                    "Remove Outlier Pixels Preview"));
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetRemoveOutlierSummary(
                "Preview canceled. Source and authored recipe were not changed.");
            appendLog("Preview", "Remove Outlier Pixels Preview canceled.");
            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref removeOutlierPreviewCancellation,
                    null,
                    cancellation),
                cancellation);
            if (ownsCancellation)
            {
                cancellation.Dispose();
            }

            if (ownsCancellation && !IsDisposed)
            {
                SetRemoveOutlierRunning(false);
            }
        }
    }

    public bool CanPreviewSelectedRemoveOutlierPixels() =>
        !IsDisposed
        && isSelectedStepRemoveOutlier()
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isRemoveOutlierPreviewRunning
        && getSelectedPipelineStep() is { } step
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void PublishSelectedRemoveOutlierPixels()
    {
        if (IsDisposed
            || getSelectedPipelineStep() is not { } step
            || !HasCurrentRemoveOutlierPreview)
        {
            return;
        }

        isRemoveOutlierPreviewPublished = true;
        step.State = "Published";
        SetRemoveOutlierSummary(
            $"Published output {step.OutputEntityId} | output SHA-256 {removeOutlierPreview!.Output!.ContentSha256} | mask SHA-256 {removeOutlierPreview.OutlierMask!.Sha256} | source unchanged");
        appendLog(
            "Publish",
            $"Remove Outlier Pixels output published without re-running: {step.OutputEntityId}.");
    }

    public void CancelRemoveOutlierPreview()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref removeOutlierPreviewCancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }
    }

    public void MarkRemoveOutlierPreviewStaleIfNeeded(object? sender)
    {
        if (IsDisposed
            || removeOutlierPreview is null
            || isRemoveOutlierPreviewRunning)
        {
            return;
        }

        var selected = getSelectedPipelineStep();
        var selectedIsOutlier = IsSelectedStepRemoveOutlierPixels;
        var isSelectedParameter = selectedIsOutlier
            && sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        if (selected is { } selectedStep
            && (isSourceChangeEvent(sender)
                || ReferenceEquals(sender, selectedStep)
                || isSelectedParameter))
        {
            isRemoveOutlierPreviewStale = true;
            isRemoveOutlierPreviewPublished = false;
            selectedStep.State = "Preview stale";
            SetRemoveOutlierSummary(
                "Source, routing, output, or outlier rule changed. Preview again before Publish.");
            return;
        }
    }

    public void ClearRemoveOutlierPreview(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(
            ref removeOutlierPreviewCancellation,
            null);
        CancelAndDispose(cancellation);
        removeOutlierPreview = null;
        removeOutlierPreviewPath = null;
        isRemoveOutlierPreviewStale = false;
        isRemoveOutlierPreviewPublished = false;
        SetRemoveOutlierSummary(summary);
    }

    public void RefreshRemoveOutlierExecutionState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedPipelineStep() is { } step
            && IsSelectedStepRemoveOutlierPixels
            && removeOutlierPreview is null
            && !isRemoveOutlierPreviewRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid
                ? "Ready"
                : "Taught / needs correction";
        }

        onExecutionStateChanged();
    }

    public void SetRemoveOutlierRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isRemoveOutlierPreviewRunning = value;
        onExecutionStateChanged();
    }

    private void SetRemoveOutlierSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        removeOutlierExecutionSummary = value;
        onExecutionStateChanged();
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref removeOutlierPreviewCancellation),
            cancellation);

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }

        cancellation.Dispose();
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

    private static string CreateRemoveOutlierPreviewPath(string hash)
    {
        var testArtifactRoot = Environment.GetEnvironmentVariable(
            "OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var directory = string.IsNullOrWhiteSpace(testArtifactRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenVisionLab",
                "3DStudio",
                "Preview")
            : Path.Combine(Path.GetFullPath(testArtifactRoot), "Preview");
        return Path.Combine(directory, $"remove-outlier-{hash}.c3d");
    }
}
