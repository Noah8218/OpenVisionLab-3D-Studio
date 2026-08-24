using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchRoiCropExecutionOwner
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

    public bool IsSelected => isSelected();
    public bool IsRunning => isRunning;
    public bool HasCurrentPreview => preview?.Output is not null && !isStale;
    public bool IsStale => isStale;
    public bool IsPublished => isPublished;
    public C3DHeightFieldSnapshot? CurrentOutput => preview?.Output;
    public ToolRecipeGridRectangle? CurrentRegion => preview?.SourceRegion;
    public string? CurrentPreviewPath => previewPath;
    public string Summary => summary;
    public string RegionSummary => preview?.SourceRegion is not { } region
        ? "No crop region evidence until Preview completes."
        : $"Source row {region.Row}, column {region.Column} | {region.RowCount} x {region.ColumnCount}";
    public string OutputSummary => preview?.Output is not { } output
        ? "No cropped HeightField output."
        : $"{output.Width} x {output.Height} | source origin ({output.GridOriginColumn}, {output.GridOriginRow}) | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isPublished ? "Published" : "Preview only")}";

    public bool TryGetPublishedOutput(string outputEntityId, out C3DHeightFieldSnapshot? output)
    {
        var current = preview?.Output;
        output = isPublished && !isStale
            && string.Equals(current?.EntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)
            ? current
            : null;
        return output is not null;
    }

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview() || getSelectedStep() is not { } step)
        {
            return false;
        }

        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        SetRunning(true);
        isStale = false;
        isPublished = false;
        step.State = "Preview running";
        SetSummary("ROI / Crop Preview is copying the selected source cells without changing the source.");
        appendLog("Preview", $"ROI / Crop Preview started: {step.Id}.");
        try
        {
            var document = createDocument();
            var evaluation = await Task.Run(
                () => ToolRecipeRoiCropExecution.Execute(
                    document,
                    step.Id,
                    GetRecipeDirectory(),
                    cancellation.Token),
                cancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                preview = evaluation;
                previewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"ROI / Crop Preview did not produce output: {evaluation.Result.Message}");
                return false;
            }

            preview = evaluation;
            previewPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(previewPath);
            step.State = "Preview ready";
            SetSummary($"Preview ready | {RegionSummary} | output {evaluation.Output.ContentSha256} | source unchanged");
            appendLog("Preview", $"ROI / Crop Preview ready: output={evaluation.Output.ContentSha256}; {RegionSummary}.");
            requestDisplay(new ToolWorkbenchFilterDisplayRequestEventArgs(
                previewPath,
                evaluation.Output.ContentSha256,
                false,
                "ROI / Crop Preview"));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. Source and authored recipe were not changed.");
            appendLog("Preview", "ROI / Crop Preview canceled.");
            return false;
        }
        finally
        {
            SetRunning(false);
        }
    }

    public bool CanPreview() => IsSelected
        && isSourceReady()
        && !hasPendingParameters()
        && !isRunning
        && ToolRecipeValidator.Validate(createDocument()).IsValid;

    public void Publish()
    {
        if (getSelectedStep() is not { } step || !HasCurrentPreview)
        {
            return;
        }
        isPublished = true;
        step.State = "Published";
        SetSummary($"Published {step.OutputEntityId} | output SHA-256 {preview!.Output!.ContentSha256} | {RegionSummary} | source unchanged");
        appendLog("Publish", $"ROI / Crop output published without re-running: {step.OutputEntityId}.");
    }

    public void Cancel() => cancellation?.Cancel();

    public void MarkStaleIfNeeded(object? sender)
    {
        if (preview is null || isRunning)
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
        cancellation?.Cancel();
        preview = null;
        previewPath = null;
        isStale = false;
        isPublished = false;
        SetSummary(value);
    }

    public void Refresh()
    {
        if (getSelectedStep() is { } step
            && IsSelected
            && preview is null
            && !isRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = ToolRecipeValidator.Validate(createDocument()).IsValid
                ? "Ready"
                : "Taught / needs correction";
        }
        onStateChanged();
    }

    private void SetRunning(bool value)
    {
        isRunning = value;
        onStateChanged();
    }

    private void SetSummary(string value)
    {
        summary = value;
        onStateChanged();
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
