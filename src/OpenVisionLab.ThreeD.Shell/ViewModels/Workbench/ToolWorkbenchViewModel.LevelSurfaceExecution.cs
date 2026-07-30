using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? levelSurfacePreviewCancellation;
    private C3DLevelSurfaceEvaluation? levelSurfacePreview;
    private string? levelSurfacePreviewPath;
    private bool isLevelSurfacePreviewRunning;
    private bool isLevelSurfacePreviewStale;
    private bool isLevelSurfacePreviewPublished;
    private string levelSurfaceExecutionSummary =
        "Select Level Surface, teach one or more reference ROIs, then Preview.";

    public bool IsSelectedStepLevelSurface =>
        string.Equals(
            SelectedPipelineStep?.ToolId,
            "level-surface",
            StringComparison.Ordinal);
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
        SelectedPipelineStep is not { } step || !IsSelectedStepLevelSurface
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
        if (!CanPreviewSelectedLevelSurface()
            || SelectedPipelineStep is not { } step)
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
        AppendLog("Preview", $"Level Surface Preview started: {step.Id}.");
        try
        {
            var document = CreateDocument();
            var recipeDirectory = RecipePath is null
                ? Environment.CurrentDirectory
                : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
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
                AppendLog(
                    evaluation.Result.Status == ResultStatus.Fail ? "Preview" : "Error",
                    $"Level Surface Preview did not produce output: {evaluation.Result.Message}");
                return false;
            }

            levelSurfacePreview = evaluation;
            levelSurfacePreviewPath =
                CreateLevelSurfacePreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(levelSurfacePreviewPath);
            step.State = "Preview ready";
            SetLevelSurfaceSummary(
                $"Preview ready | {evaluation.Transform.ReferenceRegions.Count} reference ROI(s) | input slope X {evaluation.Transform.FittedSlopeX:G6}, Z {evaluation.Transform.FittedSlopeZ:G6} | output slope X {evaluation.OutputReferenceSlopeX:G6}, Z {evaluation.OutputReferenceSlopeZ:G6} | source unchanged");
            AppendLog(
                "Preview",
                $"Level Surface Preview ready: output={evaluation.Output.ContentSha256}; transform={evaluation.Transform.ContentSha256}; referenceRms={evaluation.Transform.ReferenceResidualRms:R}.");
            FilterDisplayRequested?.Invoke(
                this,
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
            SetLevelSurfaceSummary(
                "Preview canceled. Source and authored recipe were not changed.");
            AppendLog("Preview", "Level Surface Preview canceled.");
            return false;
        }
        finally
        {
            SetLevelSurfaceRunning(false);
        }
    }

    private bool CanPreviewSelectedLevelSurface() =>
        IsSelectedStepLevelSurface
        && IsSourceReadyForRecipe
        && !HasPendingStepParameterChanges
        && !isLevelSurfacePreviewRunning
        && ToolRecipeValidator.Validate(CreateDocument()).IsValid;

    private void PublishSelectedLevelSurface()
    {
        if (SelectedPipelineStep is not { } step
            || !HasCurrentLevelSurfacePreview)
        {
            return;
        }
        isLevelSurfacePreviewPublished = true;
        step.State = "Published";
        SetLevelSurfaceSummary(
            $"Published {step.OutputEntityId} | output SHA-256 {levelSurfacePreview!.Output!.ContentSha256} | leveling transform SHA-256 {levelSurfacePreview.Transform!.ContentSha256} | source unchanged");
        AppendLog(
            "Publish",
            $"Level Surface output and typed transform published without re-running: {step.OutputEntityId}.");
    }

    private void CancelLevelSurfacePreview() =>
        levelSurfacePreviewCancellation?.Cancel();

    private void MarkLevelSurfacePreviewStaleIfNeeded(object? sender)
    {
        if (levelSurfacePreview is null || isLevelSurfacePreviewRunning)
        {
            return;
        }
        var selected = SelectedPipelineStep;
        var selectedIsLevel = IsSelectedStepLevelSurface;
        var isSelectedParameter = selectedIsLevel
            && sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        var isReferenceSelection = sender is ToolRecipeSelection selection
            && selected?.InputEntityIds.Skip(1).Contains(
                selection.Id,
                StringComparer.OrdinalIgnoreCase) == true;
        if (ReferenceEquals(sender, Source)
            || selectedIsLevel && ReferenceEquals(sender, selected)
            || isSelectedParameter
            || isReferenceSelection)
        {
            isLevelSurfacePreviewStale = true;
            isLevelSurfacePreviewPublished = false;
            if (selected is not null) selected.State = "Preview stale";
            SetLevelSurfaceSummary(
                "Source, reference ROI, routing, output, or leveling policy changed. Preview again before Publish.");
        }
    }

    private void ClearLevelSurfacePreview(string summary)
    {
        levelSurfacePreviewCancellation?.Cancel();
        levelSurfacePreview = null;
        levelSurfacePreviewPath = null;
        isLevelSurfacePreviewStale = false;
        isLevelSurfacePreviewPublished = false;
        SetLevelSurfaceSummary(summary);
    }

    private void RefreshLevelSurfaceExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepLevelSurface));
        OnPropertyChanged(nameof(IsLevelSurfacePreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        if (SelectedPipelineStep is { } step
            && IsSelectedStepLevelSurface
            && levelSurfacePreview is null
            && !isLevelSurfacePreviewRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = ToolRecipeValidator.Validate(CreateDocument()).IsValid
                ? "Ready"
                : "Taught / needs correction";
        }
        RefreshFilterCommands();
    }

    private void SetLevelSurfaceRunning(bool value)
    {
        isLevelSurfacePreviewRunning = value;
        OnPropertyChanged(nameof(IsLevelSurfacePreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshSelectedToolWorkspaceProjection();
        RefreshFilterCommands();
    }

    private void SetLevelSurfaceSummary(string value)
    {
        levelSurfaceExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(LevelSurfaceExecutionSummary));
        OnPropertyChanged(nameof(LevelSurfaceReferenceSummary));
        OnPropertyChanged(nameof(LevelSurfaceTransformSummary));
        OnPropertyChanged(nameof(LevelSurfaceResidualSummary));
        OnPropertyChanged(nameof(LevelSurfaceOutputSummary));
        OnPropertyChanged(nameof(CurrentLevelSurfacePreviewOutput));
        OnPropertyChanged(nameof(CurrentLevelSurfaceTransform));
        OnPropertyChanged(nameof(CurrentLevelSurfaceOutputSlopeX));
        OnPropertyChanged(nameof(CurrentLevelSurfaceOutputSlopeZ));
        OnPropertyChanged(nameof(CurrentLevelSurfacePreviewPath));
        OnPropertyChanged(nameof(HasCurrentLevelSurfacePreview));
        OnPropertyChanged(nameof(IsLevelSurfacePreviewStale));
        OnPropertyChanged(nameof(IsLevelSurfacePreviewPublished));
        RefreshFilterCommands();
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
