using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns ROI / Crop Preview state, output persistence, and cancellation. The
/// registered cancellation source identifies the operation allowed to publish
/// the derived HeightField or request a display update.
/// </summary>
internal sealed class ToolWorkbenchRoiCropExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelected;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedStep;
    private readonly Func<bool> isSourceReady;
    private readonly Func<bool> hasPendingParameters;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay;
    private readonly Action onStateChanged;

    private CancellationTokenSource? cancellation;
    private C3DRoiCropEvaluation? preview;
    private string? previewPath;
    private bool isRunning;
    private bool isStale;
    private bool isPublished;
    private string summary = "Select ROI / Crop, teach one GridRectangle, then Preview.";
    private int disposalState;

    public ToolWorkbenchRoiCropExecutionOwner(
        Func<bool> isSelected,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedStep,
        Func<bool> isSourceReady,
        Func<bool> hasPendingParameters,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<object?, bool> isSourceChangeEvent,
        Action<string, string> appendLog,
        Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay,
        Action onStateChanged)
    {
        this.isSelected = isSelected;
        this.getSelectedStep = getSelectedStep;
        this.isSourceReady = isSourceReady;
        this.hasPendingParameters = hasPendingParameters;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.isSourceChangeEvent = isSourceChangeEvent;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.onStateChanged = onStateChanged;
    }

    public bool IsSelected => !IsDisposed && isSelected();
    public bool IsRunning => !IsDisposed && isRunning;
    public bool HasCurrentPreview => !IsDisposed && preview?.Output is not null && !isStale;
    public bool IsStale => !IsDisposed && isStale;
    public bool IsPublished => !IsDisposed && isPublished;
    public C3DHeightFieldSnapshot? CurrentOutput => IsDisposed ? null : preview?.Output;
    public ToolRecipeGridRectangle? CurrentRegion => IsDisposed ? null : preview?.SourceRegion;
    public string? CurrentPreviewPath => IsDisposed ? null : previewPath;
    public string Summary => IsDisposed
        ? "ROI / Crop execution owner has been disposed."
        : summary;
    public string RegionSummary => IsDisposed || preview?.SourceRegion is not { } region
        ? "No crop region evidence until Preview completes."
        : $"Source row {region.Row}, column {region.Column} | {region.RowCount} x {region.ColumnCount}";
    public string OutputSummary => IsDisposed || preview?.Output is not { } output
        ? "No cropped HeightField output."
        : $"{output.Width} x {output.Height} | source origin ({output.GridOriginColumn}, {output.GridOriginRow}) | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isPublished ? "Published" : "Preview only")}";

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref cancellation,
            null);
        CancelAndDispose(currentCancellation);
        preview = null;
        previewPath = null;
        isRunning = false;
        isStale = false;
        isPublished = false;
    }

    public bool TryGetPublishedOutput(string outputEntityId, out C3DHeightFieldSnapshot? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        var current = preview?.Output;
        output = isPublished && !isStale
            && string.Equals(current?.EntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)
            ? current
            : null;
        return output is not null;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed
            || !CanPreview()
            || getSelectedStep() is not { } step)
        {
            return false;
        }

        var currentCancellation = new CancellationTokenSource();
        var cancellationToken = currentCancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref cancellation,
            currentCancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref cancellation,
                    null,
                    currentCancellation),
                currentCancellation))
            {
                currentCancellation.Dispose();
            }

            return false;
        }

        SetRunning(true);
        isStale = false;
        isPublished = false;
        step.State = "Preview running";
        SetSummary("ROI / Crop Preview is copying the selected source cells without changing the source.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"ROI / Crop Preview started: {step.Id}.");
        try
        {
            var document = createDocument();
            var evaluation = await Task.Run(
                () => ToolRecipeRoiCropExecution.Execute(
                    document,
                    step.Id,
                    GetRecipeDirectory(),
                    cancellationToken),
                cancellationToken);

            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                if (!IsCurrentPreview(currentCancellation))
                {
                    return false;
                }

                preview = evaluation;
                previewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (IsCurrentPreview(currentCancellation))
                {
                    appendLog("Error", $"ROI / Crop Preview did not produce output: {evaluation.Result.Message}");
                }

                return false;
            }

            preview = evaluation;
            previewPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(previewPath);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Preview ready";
            SetSummary($"Preview ready | {RegionSummary} | output {evaluation.Output.ContentSha256} | source unchanged");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", $"ROI / Crop Preview ready: output={evaluation.Output.ContentSha256}; {RegionSummary}.");
            }

            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            requestDisplay(new ToolWorkbenchFilterDisplayRequestEventArgs(
                previewPath,
                evaluation.Output.ContentSha256,
                false,
                "ROI / Crop Preview"));
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetSummary("Preview canceled. Source and authored recipe were not changed.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", "ROI / Crop Preview canceled.");
            }

            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref cancellation,
                    null,
                    currentCancellation),
                currentCancellation);
            if (ownsCancellation)
            {
                currentCancellation.Dispose();
            }

            if (ownsCancellation && !IsDisposed)
            {
                SetRunning(false);
            }
        }
    }

    public bool CanPreview() => !IsDisposed
        && IsSelected
        && isSourceReady()
        && !hasPendingParameters()
        && !isRunning
        && getSelectedStep() is { } step
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void Publish()
    {
        if (IsDisposed
            || getSelectedStep() is not { } step
            || !HasCurrentPreview)
        {
            return;
        }
        isPublished = true;
        step.State = "Published";
        SetSummary($"Published {step.OutputEntityId} | output SHA-256 {preview!.Output!.ContentSha256} | {RegionSummary} | source unchanged");
        appendLog("Publish", $"ROI / Crop output published without re-running: {step.OutputEntityId}.");
    }

    public void Cancel()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref cancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }
    }

    public void MarkStaleIfNeeded(object? sender)
    {
        if (IsDisposed
            || preview is null
            || isRunning)
        {
            return;
        }
        var step = getSelectedStep();
        var isSelectedParameter = IsSelected
            && sender is ToolWorkbenchParameterItem parameter
            && (step?.Parameters.Contains(parameter) ?? false);
        var isSelection = sender is ToolRecipeSelection selection
            && step?.InputEntityIds.Skip(1).Contains(selection.Id, StringComparer.OrdinalIgnoreCase) == true;
        if (isSourceChangeEvent(sender) || (IsSelected && ReferenceEquals(sender, step)) || isSelectedParameter || isSelection)
        {
            isStale = true;
            isPublished = false;
            if (step is not null)
            {
                step.State = "Preview stale";
            }
            SetSummary("Source, crop ROI, routing, output, or fixed crop policy changed. Preview again before Publish.");
        }
    }

    public void Clear(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref cancellation,
            null);
        CancelAndDispose(currentCancellation);
        preview = null;
        previewPath = null;
        isStale = false;
        isPublished = false;
        SetRunning(false);
        SetSummary(value);
    }

    public void Refresh()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedStep() is { } step
            && IsSelected
            && preview is null
            && !isRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid
                ? "Ready"
                : "Taught / needs correction";
        }
        onStateChanged();
    }

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isRunning = value;
        onStateChanged();
    }

    private void SetSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        summary = value;
        onStateChanged();
    }

    private bool IsCurrentPreview(CancellationTokenSource currentCancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref cancellation),
            currentCancellation);

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
        var directory = string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(path));
        return string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory;
    }

    private static string CreatePreviewPath(string hash)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "3DStudio",
            "Preview");
        return Path.Combine(directory, $"roi-crop-{hash}.c3d");
    }
}
