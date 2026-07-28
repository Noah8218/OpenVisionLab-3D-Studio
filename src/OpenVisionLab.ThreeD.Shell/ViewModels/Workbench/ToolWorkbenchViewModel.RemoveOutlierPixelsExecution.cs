using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? removeOutlierPreviewCancellation;
    private C3DRemoveOutlierPixelsEvaluation? removeOutlierPreview;
    private string? removeOutlierPreviewPath;
    private bool isRemoveOutlierPreviewRunning;
    private bool isRemoveOutlierPreviewStale;
    private bool isRemoveOutlierPreviewPublished;
    private string removeOutlierExecutionSummary =
        "Select Remove Outlier Pixels, review its explicit rule, then Preview.";

    public bool IsSelectedStepRemoveOutlierPixels =>
        string.Equals(
            SelectedPipelineStep?.ToolId,
            "remove-outlier-pixels",
            StringComparison.Ordinal);

    public bool IsRemoveOutlierPreviewRunning => isRemoveOutlierPreviewRunning;
    public bool HasCurrentRemoveOutlierPreview =>
        removeOutlierPreview?.Output is not null && !isRemoveOutlierPreviewStale;
    public bool IsRemoveOutlierPreviewStale => isRemoveOutlierPreviewStale;
    public bool IsRemoveOutlierPreviewPublished => isRemoveOutlierPreviewPublished;
    public C3DHeightFieldSnapshot? CurrentRemoveOutlierPreviewOutput =>
        removeOutlierPreview?.Output;
    public C3DOutlierCellMap? CurrentRemoveOutlierMask =>
        removeOutlierPreview?.OutlierMask;
    public string? CurrentRemoveOutlierPreviewPath => removeOutlierPreviewPath;
    public string RemoveOutlierExecutionSummary => removeOutlierExecutionSummary;
    public string RemoveOutlierRuleSummary
    {
        get
        {
            if (!IsSelectedStepRemoveOutlierPixels
                || SelectedPipelineStep is not { } step)
            {
                return "No authored outlier rule.";
            }

            string Value(string name) =>
                step.Parameters.FirstOrDefault(
                    parameter => parameter.Name == name)?.Value ?? "?";
            return
                $"{Value("Rule")} | {Value("WindowSize")} x {Value("WindowSize")} | threshold > {Value("MaximumAbsoluteDeviation")} {Source.Unit} | minimum neighbors {Value("MinimumValidNeighbors")}";
        }
    }
    public string RemoveOutlierOutputSummary =>
        removeOutlierPreview is not { Output: { } output, OutlierMask: { } mask }
            ? "No outlier-removal Preview output."
            : $"Removed {mask.OutlierCellCount:N0} | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isRemoveOutlierPreviewPublished ? "Published" : "Preview only")}";
    public string RemoveOutlierMaskSummary =>
        removeOutlierPreview?.OutlierMask is not { } mask
            ? "Outlier mask SHA-256 is available after Preview."
            : $"Outlier mask SHA-256 {mask.Sha256}";

    public async Task<bool> PreviewSelectedRemoveOutlierPixelsAsync()
    {
        if (!CanPreviewSelectedRemoveOutlierPixels()
            || SelectedPipelineStep is not { } step)
        {
            return false;
        }

        removeOutlierPreviewCancellation?.Dispose();
        removeOutlierPreviewCancellation = new CancellationTokenSource();
        SetRemoveOutlierRunning(true);
        isRemoveOutlierPreviewStale = false;
        isRemoveOutlierPreviewPublished = false;
        step.State = "Preview running";
        SetRemoveOutlierSummary(
            "Remove Outlier Pixels Preview is evaluating the verified source without changing it.");
        AppendLog(
            "Preview",
            $"Remove Outlier Pixels Preview started: {step.Id}.");

        try
        {
            var document = CreateDocument();
            var recipeDirectory = RecipePath is null
                ? Environment.CurrentDirectory
                : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
            var evaluation = await Task.Run(
                () => ToolRecipeRemoveOutlierPixelsExecution.Execute(
                    document,
                    step.Id,
                    recipeDirectory,
                    removeOutlierPreviewCancellation.Token),
                removeOutlierPreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass
                || evaluation.Output is null
                || evaluation.OutlierMask is null)
            {
                removeOutlierPreview = null;
                removeOutlierPreviewPath = null;
                step.State = "Error";
                SetRemoveOutlierSummary(evaluation.Result.Message);
                AppendLog(
                    "Error",
                    $"Remove Outlier Pixels Preview failed: {evaluation.Result.Message}");
                return false;
            }

            removeOutlierPreview = evaluation;
            removeOutlierPreviewPath =
                CreateRemoveOutlierPreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(removeOutlierPreviewPath);
            step.State = "Preview ready";
            SetRemoveOutlierSummary(
                $"Preview ready | input valid {evaluation.Output.ValidCount + evaluation.OutlierMask.OutlierCellCount:N0} | removed {evaluation.OutlierMask.OutlierCellCount:N0} | output valid {evaluation.Output.ValidCount:N0} | source unchanged");
            AppendLog(
                "Preview",
                $"Remove Outlier Pixels Preview ready: output={evaluation.Output.ContentSha256}; mask={evaluation.OutlierMask.Sha256}; removed={evaluation.OutlierMask.OutlierCellCount}.");
            FilterDisplayRequested?.Invoke(
                this,
                new ToolWorkbenchFilterDisplayRequestEventArgs(
                    removeOutlierPreviewPath,
                    evaluation.Output.ContentSha256,
                    false,
                    "Remove Outlier Pixels Preview"));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetRemoveOutlierSummary(
                "Preview canceled. Source and authored recipe were not changed.");
            AppendLog("Preview", "Remove Outlier Pixels Preview canceled.");
            return false;
        }
        finally
        {
            SetRemoveOutlierRunning(false);
        }
    }

    private bool CanPreviewSelectedRemoveOutlierPixels() =>
        IsSelectedStepRemoveOutlierPixels
        && IsSourceReadyForRecipe
        && !HasPendingStepParameterChanges
        && !isRemoveOutlierPreviewRunning
        && ToolRecipeValidator.Validate(CreateDocument()).IsValid;

    private void PublishSelectedRemoveOutlierPixels()
    {
        if (SelectedPipelineStep is not { } step
            || !HasCurrentRemoveOutlierPreview)
        {
            return;
        }

        isRemoveOutlierPreviewPublished = true;
        step.State = "Published";
        SetRemoveOutlierSummary(
            $"Published output {step.OutputEntityId} | output SHA-256 {removeOutlierPreview!.Output!.ContentSha256} | mask SHA-256 {removeOutlierPreview.OutlierMask!.Sha256} | source unchanged");
        AppendLog(
            "Publish",
            $"Remove Outlier Pixels output published without re-running: {step.OutputEntityId}.");
    }

    private void CancelRemoveOutlierPreview() =>
        removeOutlierPreviewCancellation?.Cancel();

    private void MarkRemoveOutlierPreviewStaleIfNeeded(object? sender)
    {
        if (removeOutlierPreview is null || isRemoveOutlierPreviewRunning)
        {
            return;
        }

        var selected = SelectedPipelineStep;
        var selectedIsOutlier = IsSelectedStepRemoveOutlierPixels;
        var isSelectedParameter = selectedIsOutlier
            && sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        if (ReferenceEquals(sender, Source)
            || selectedIsOutlier && ReferenceEquals(sender, selected)
            || isSelectedParameter)
        {
            isRemoveOutlierPreviewStale = true;
            isRemoveOutlierPreviewPublished = false;
            if (selected is not null)
            {
                selected.State = "Preview stale";
            }

            SetRemoveOutlierSummary(
                "Source, routing, output, or outlier rule changed. Preview again before Publish.");
        }
    }

    private void ClearRemoveOutlierPreview(string summary)
    {
        removeOutlierPreviewCancellation?.Cancel();
        removeOutlierPreview = null;
        removeOutlierPreviewPath = null;
        isRemoveOutlierPreviewStale = false;
        isRemoveOutlierPreviewPublished = false;
        SetRemoveOutlierSummary(summary);
    }

    private void RefreshRemoveOutlierExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepRemoveOutlierPixels));
        OnPropertyChanged(nameof(IsRemoveOutlierPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        if (SelectedPipelineStep is { } step
            && IsSelectedStepRemoveOutlierPixels
            && removeOutlierPreview is null
            && !isRemoveOutlierPreviewRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = ToolRecipeValidator.Validate(CreateDocument()).IsValid
                ? "Ready"
                : "Taught / needs correction";
        }

        RefreshFilterCommands();
    }

    private void SetRemoveOutlierRunning(bool value)
    {
        isRemoveOutlierPreviewRunning = value;
        OnPropertyChanged(nameof(IsRemoveOutlierPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshFilterCommands();
    }

    private void SetRemoveOutlierSummary(string value)
    {
        removeOutlierExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(RemoveOutlierExecutionSummary));
        OnPropertyChanged(nameof(RemoveOutlierRuleSummary));
        OnPropertyChanged(nameof(RemoveOutlierOutputSummary));
        OnPropertyChanged(nameof(RemoveOutlierMaskSummary));
        OnPropertyChanged(nameof(CurrentRemoveOutlierPreviewOutput));
        OnPropertyChanged(nameof(CurrentRemoveOutlierMask));
        OnPropertyChanged(nameof(CurrentRemoveOutlierPreviewPath));
        OnPropertyChanged(nameof(HasCurrentRemoveOutlierPreview));
        OnPropertyChanged(nameof(IsRemoveOutlierPreviewStale));
        OnPropertyChanged(nameof(IsRemoveOutlierPreviewPublished));
        RefreshFilterCommands();
    }

    private static string CreateRemoveOutlierPreviewPath(string hash)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "3DStudio",
            "Preview");
        return Path.Combine(directory, $"remove-outlier-{hash}.c3d");
    }
}
