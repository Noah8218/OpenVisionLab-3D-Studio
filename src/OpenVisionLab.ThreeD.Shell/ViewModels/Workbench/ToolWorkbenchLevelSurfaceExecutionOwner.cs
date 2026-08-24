using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchLevelSurfaceExecutionOwner
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

    public bool IsSelectedStepLevelSurface => isSelectedStepLevelSurface();

    public bool IsLevelSurfacePreviewRunning => isLevelSurfacePreviewRunning;
    public bool HasCurrentLevelSurfacePreview =>
        levelSurfacePreview?.Output is not null
        && levelSurfacePreview.Transform is not null
        && !isLevelSurfacePreviewStale;
    public bool IsLevelSurfacePreviewStale => isLevelSurfacePreviewStale;
    public bool IsLevelSurfacePreviewPublished => isLevelSurfacePreviewPublished;
    public C3DHeightFieldSnapshot? CurrentLevelSurfacePreviewOutput =>
        levelSurfacePreview?.Output;
    public C3DLevelingTransform? CurrentLevelSurfaceTransform =>
        levelSurfacePreview?.Transform;
    public double CurrentLevelSurfaceOutputSlopeX =>
        levelSurfacePreview?.OutputReferenceSlopeX ?? double.NaN;
    public double CurrentLevelSurfaceOutputSlopeZ =>
        levelSurfacePreview?.OutputReferenceSlopeZ ?? double.NaN;
    public string? CurrentLevelSurfacePreviewPath => levelSurfacePreviewPath;
    public string LevelSurfaceExecutionSummary => levelSurfaceExecutionSummary;

    public string LevelSurfaceReferenceSummary =>
        getSelectedPipelineStep() is not { } step || !IsSelectedStepLevelSurface
            ? "No Level Surface reference routing."
            : $"{Math.Max(0, step.InputEntityIds.Count - 1)} explicit reference ROI(s) | unique finite cells | overlap counted once";

    public string LevelSurfaceTransformSummary =>
        levelSurfacePreview?.Transform is not { } transform
            ? "No typed leveling transform until Preview completes."
            : $"Transform {transform.ContentSha256} | input slope X {transform.FittedSlopeX:G6}, Z {transform.FittedSlopeZ:G6} | target {transform.TargetHeight:G6} {transform.SourceUnit}";

    public string LevelSurfaceResidualSummary =>
        levelSurfacePreview is not { Transform: { } transform }
            ? "Reference residual and output slope evidence are available after Preview."
            : $"Reference RMS {transform.ReferenceResidualRms:G6} | P2V {transform.ReferenceResidualPeakToValley:G6} | output slope X {levelSurfacePreview.OutputReferenceSlopeX:G6}, Z {levelSurfacePreview.OutputReferenceSlopeZ:G6}";

    public string LevelSurfaceOutputSummary =>
        levelSurfacePreview?.Output is not { } output
            ? "No leveled C3D output."
            : $"{output.Width} x {output.Height} | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isLevelSurfacePreviewPublished ? "Published" : "Preview only")}";

    public async Task<bool> PreviewSelectedLevelSurfaceAsync()
    {
        if (!CanPreviewSelectedLevelSurface() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        levelSurfacePreviewCancellation?.Dispose();
        levelSurfacePreviewCancellation = new CancellationTokenSource();
        SetLevelSurfaceRunning(true);
        isLevelSurfacePreviewStale = false;
        isLevelSurfacePreviewPublished = false;
        step.State = "Preview running";
        SetLevelSurfaceSummary(
            "Level Surface Preview is fitting the explicit reference regions without changing the source.");
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
                    levelSurfacePreviewCancellation.Token),
                levelSurfacePreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass
                || evaluation.Output is null
                || evaluation.Transform is null)
            {
                levelSurfacePreview = evaluation;
                levelSurfacePreviewPath = null;
                step.State = evaluation.Result.Status == ResultStatus.Fail
                    ? "Reference gate failed"
                    : "Error";
                SetLevelSurfaceSummary(evaluation.Result.Message);
                appendLog(
                    evaluation.Result.Status == ResultStatus.Fail ? "Preview" : "Error",
                    $"Level Surface Preview did not produce output: {evaluation.Result.Message}");
                return false;
            }

            levelSurfacePreview = evaluation;
            levelSurfacePreviewPath = CreateLevelSurfacePreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(levelSurfacePreviewPath);
            step.State = "Preview ready";
            SetLevelSurfaceSummary(
                $"Preview ready | {evaluation.Transform.ReferenceRegions.Count} reference ROI(s) | input slope X {evaluation.Transform.FittedSlopeX:G6}, Z {evaluation.Transform.FittedSlopeZ:G6} | output slope X {evaluation.OutputReferenceSlopeX:G6}, Z {evaluation.OutputReferenceSlopeZ:G6} | source unchanged");
            appendLog(
                "Preview",
                $"Level Surface Preview ready: output={evaluation.Output.ContentSha256}; transform={evaluation.Transform.ContentSha256}; referenceRms={evaluation.Transform.ReferenceResidualRms:R}.");
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
            step.State = "Ready";
            SetLevelSurfaceSummary("Preview canceled. Source and authored recipe were not changed.");
            appendLog("Preview", "Level Surface Preview canceled.");
            return false;
        }
        finally
        {
            SetLevelSurfaceRunning(false);
        }
    }

    public bool CanPreviewSelectedLevelSurface() =>
        IsSelectedStepLevelSurface
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isLevelSurfacePreviewRunning
        && getSelectedPipelineStep() is { } step
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void PublishSelectedLevelSurface()
    {
        if (getSelectedPipelineStep() is not { } step
            || !HasCurrentLevelSurfacePreview)
        {
            return;
        }

        isLevelSurfacePreviewPublished = true;
        step.State = "Published";
        SetLevelSurfaceSummary(
            $"Published {step.OutputEntityId} | output SHA-256 {levelSurfacePreview!.Output!.ContentSha256} | leveling transform SHA-256 {levelSurfacePreview.Transform!.ContentSha256} | source unchanged");
        appendLog(
            "Publish",
            $"Level Surface output and typed transform published without re-running: {step.OutputEntityId}.");
    }

    public void CancelLevelSurfacePreview() =>
        levelSurfacePreviewCancellation?.Cancel();

    public void MarkLevelSurfacePreviewStaleIfNeeded(object? sender)
    {
        if (levelSurfacePreview is null || isLevelSurfacePreviewRunning)
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
        levelSurfacePreviewCancellation?.Cancel();
        levelSurfacePreview = null;
        levelSurfacePreviewPath = null;
        isLevelSurfacePreviewStale = false;
        isLevelSurfacePreviewPublished = false;
        SetLevelSurfaceSummary(summary);
    }

    public void RefreshLevelSurfaceExecutionState()
    {
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
        isLevelSurfacePreviewRunning = value;
        onExecutionStateChanged();
    }

    private void SetLevelSurfaceSummary(string value)
    {
        levelSurfaceExecutionSummary = value;
        onExecutionStateChanged();
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
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "3DStudio",
            "Preview");
        return Path.Combine(directory, $"level-surface-{hash}.c3d");
    }
}
