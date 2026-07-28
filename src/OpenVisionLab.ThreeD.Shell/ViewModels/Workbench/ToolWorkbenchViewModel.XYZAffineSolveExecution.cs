using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? affineSolvePreviewCancellation;
    private C3DAffineTransform3D? affineSolvePreviewOutput;
    private readonly Dictionary<string, C3DAffineTransform3D> publishedAffineSolveOutputs = new(StringComparer.OrdinalIgnoreCase);
    private bool isAffineSolvePreviewRunning;
    private bool isAffineSolvePreviewStale;
    private bool isAffineSolvePreviewPublished;
    private string affineSolveExecutionSummary = "Route one current Published CorrespondenceSet, teach the numerical review limits, then Preview explicitly.";

    public bool IsSelectedStepXYZAffineSolve =>
        string.Equals(SelectedPipelineStep?.ToolId, "xyz-affine-solve", StringComparison.Ordinal);
    public bool IsAffineSolvePreviewRunning => isAffineSolvePreviewRunning;
    public bool HasCurrentAffineSolvePreview => affineSolvePreviewOutput is not null && !isAffineSolvePreviewStale;
    public bool IsAffineSolvePreviewStale => isAffineSolvePreviewStale;
    public bool IsAffineSolvePreviewPublished => isAffineSolvePreviewPublished;
    internal C3DAffineTransform3D? CurrentAffineSolveOutput => affineSolvePreviewOutput;
    internal bool TryGetPublishedAffineSolveOutput(string outputEntityId, out C3DAffineTransform3D? output) =>
        publishedAffineSolveOutputs.TryGetValue(outputEntityId, out output);
    public string AffineSolveExecutionSummary => affineSolveExecutionSummary;
    public string AffineSolveOutputHashSummary => affineSolvePreviewOutput is null
        ? "No affine output hash until Preview completes."
        : $"AffineTransform3D SHA-256 {affineSolvePreviewOutput.ContentSha256}";
    public string AffineSolveUpstreamSummary => TryGetCurrentAffineSolveInput(out var correspondence)
        ? $"Published CorrespondenceSet | {correspondence.Pairs.Count}/4 pairs | SHA-256 {correspondence.ContentSha256[..12]}"
        : "One current Published CorrespondenceSet must be routed from Landmark Correspondence.";
    public string AffineSolveEvidenceSummary => affineSolvePreviewOutput is null
        ? "No matrix evidence until Preview completes."
        : $"condition {affineSolvePreviewOutput.ConditionEstimate:G6} / {affineSolvePreviewOutput.MaximumConditionEstimate:G6} | residual RMS {affineSolvePreviewOutput.ArithmeticRmsResidual:G6}, max {affineSolvePreviewOutput.ArithmeticMaximumResidual:G6} {affineSolvePreviewOutput.ReferenceUnit} | no C3D point moved";
    public string AffineSolveMatrixSummary => affineSolvePreviewOutput is null
        ? "No source-to-reference matrix until Preview completes."
        : string.Join(Environment.NewLine,
        [
            $"Xref = {affineSolvePreviewOutput.Matrix.M11:G8} X + {affineSolvePreviewOutput.Matrix.M12:G8} Y + {affineSolvePreviewOutput.Matrix.M13:G8} Z + {affineSolvePreviewOutput.Matrix.M14:G8}",
            $"Yref = {affineSolvePreviewOutput.Matrix.M21:G8} X + {affineSolvePreviewOutput.Matrix.M22:G8} Y + {affineSolvePreviewOutput.Matrix.M23:G8} Z + {affineSolvePreviewOutput.Matrix.M24:G8}",
            $"Zref = {affineSolvePreviewOutput.Matrix.M31:G8} X + {affineSolvePreviewOutput.Matrix.M32:G8} Y + {affineSolvePreviewOutput.Matrix.M33:G8} Z + {affineSolvePreviewOutput.Matrix.M34:G8}"
        ]);

    public async Task<bool> PreviewSelectedXYZAffineSolveAsync()
    {
        if (!CanPreviewSelectedXYZAffineSolve() || SelectedPipelineStep is not { } step
            || !TryGetCurrentAffineSolveInput(out var correspondence)) return false;

        affineSolvePreviewCancellation?.Dispose();
        affineSolvePreviewCancellation = new CancellationTokenSource();
        SetAffineSolveRunning(true);
        isAffineSolvePreviewStale = false;
        isAffineSolvePreviewPublished = false;
        step.State = "Preview running";
        SetAffineSolveSummary("XYZ Affine Solve Preview computes one matrix from the exact current Published CorrespondenceSet. It does not move C3D points.");
        AppendLog("Preview", $"XYZ Affine Solve Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeXYZAffineSolveExecution.Execute(CreateDocument(), step.Id, correspondence, affineSolvePreviewCancellation.Token),
                affineSolvePreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                affineSolvePreviewOutput = null;
                step.State = "Error";
                SetAffineSolveSummary(evaluation.Result.Message);
                AppendLog("Error", $"XYZ Affine Solve Preview failed: {evaluation.Result.Message}");
                return false;
            }

            affineSolvePreviewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetAffineSolveSummary($"Preview ready | {AffineSolveEvidenceSummary}");
            AppendLog("Preview", $"XYZ Affine Solve Preview ready: {evaluation.Output.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetAffineSolveSummary("Preview canceled. Published CorrespondenceSet and authored recipe were not changed.");
            AppendLog("Preview", "XYZ Affine Solve Preview canceled.");
            return false;
        }
        finally
        {
            SetAffineSolveRunning(false);
        }
    }

    private bool CanPreviewSelectedXYZAffineSolve()
    {
        if (!IsSelectedStepXYZAffineSolve || !IsSourceReadyForRecipe || HasPendingStepParameterChanges
            || isAffineSolvePreviewRunning || SelectedPipelineStep is not { } step
            || !TryGetCurrentAffineSolveInput(out var correspondence)) return false;
        return ToolRecipeXYZAffineSolveExecution.TryPrepare(CreateDocument(), step.Id, correspondence, out _, out _);
    }

    private void PublishSelectedXYZAffineSolve()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentAffineSolvePreview) return;
        isAffineSolvePreviewPublished = true;
        publishedAffineSolveOutputs[affineSolvePreviewOutput!.OutputEntityId] = affineSolvePreviewOutput;
        step.State = "Published";
        SetAffineSolveSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {affineSolvePreviewOutput.ContentSha256} | solve evidence only; no C3D point was moved.");
        AppendLog("Publish", $"XYZ Affine Solve output published without re-running: {step.OutputEntityId}.");
        RefreshXYZAffineApplyExecutionState();
    }

    private void CancelXYZAffineSolvePreview() => affineSolvePreviewCancellation?.Cancel();

    private void MarkAffineSolvePreviewStaleIfNeeded(object? sender = null)
    {
        if (affineSolvePreviewOutput is null || isAffineSolvePreviewRunning) return;
        if (sender is not null)
        {
            var selected = SelectedPipelineStep;
            var current = IsSelectedStepXYZAffineSolve
                && string.Equals(selected?.OutputEntityId, affineSolvePreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase);
            var parameterChanged = current && sender is ToolWorkbenchParameterItem parameter && (selected?.Parameters.Contains(parameter) ?? false);
            if (!current || (!(ReferenceEquals(sender, selected)) && !parameterChanged)) return;
        }
        isAffineSolvePreviewStale = true;
        isAffineSolvePreviewPublished = false;
        publishedAffineSolveOutputs.Clear();
        var step = PipelineSteps.FirstOrDefault(item => string.Equals(item.OutputEntityId, affineSolvePreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (step is not null) step.State = "Preview stale";
        ClearXYZAffineApplyPreview("Published AffineTransform3D changed. Apply XYZ Affine Preview was cleared without execution.");
        SetAffineSolveSummary("Correspondence identity, route, or affine parameter changed. Preview again before Publish.");
    }

    private void ClearXYZAffineSolvePreview(string summary)
    {
        affineSolvePreviewCancellation?.Cancel();
        affineSolvePreviewOutput = null;
        publishedAffineSolveOutputs.Clear();
        isAffineSolvePreviewStale = false;
        isAffineSolvePreviewPublished = false;
        ClearXYZAffineApplyPreview("Published AffineTransform3D was cleared. Apply XYZ Affine Preview was cleared without execution.");
        SetAffineSolveSummary(summary);
    }

    private void RefreshXYZAffineSolveExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepXYZAffineSolve));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(AffineSolveUpstreamSummary));
        if (SelectedPipelineStep is { } step && IsSelectedStepXYZAffineSolve
            && (affineSolvePreviewOutput is null
                || !string.Equals(affineSolvePreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || isAffineSolvePreviewStale)
            && !isAffineSolvePreviewRunning)
        {
            if (!TryGetCurrentAffineSolveInput(out var correspondence))
            {
                step.State = "Waiting for upstream";
                affineSolveExecutionSummary = "Route one current Published CorrespondenceSet. Upstream tools do not run implicitly.";
            }
            else if (ToolRecipeXYZAffineSolveExecution.TryPrepare(CreateDocument(), step.Id, correspondence, out _, out var message))
            {
                step.State = "Ready";
                affineSolveExecutionSummary = "Ready for explicit Preview. A1 solves a matrix only; Apply is a separate future tool.";
            }
            else
            {
                step.State = "Taught incomplete";
                affineSolveExecutionSummary = message;
            }
        }
        OnPropertyChanged(nameof(AffineSolveExecutionSummary));
        OnPropertyChanged(nameof(AffineSolveOutputHashSummary));
        OnPropertyChanged(nameof(AffineSolveEvidenceSummary));
        OnPropertyChanged(nameof(AffineSolveMatrixSummary));
        RefreshAffineSolveCommands();
        RefreshXYZAffineApplyExecutionState();
    }

    private bool TryGetCurrentAffineSolveInput(out C3DLandmarkCorrespondenceSet correspondence)
    {
        correspondence = null!;
        if (SelectedPipelineStep is not { InputEntityIds.Count: 1 } step
            || !TryGetPublishedLandmarkCorrespondenceOutput(step.InputEntityIds[0], out var output)
            || output is null) return false;
        correspondence = output;
        return true;
    }

    private void SetAffineSolveRunning(bool value)
    {
        isAffineSolvePreviewRunning = value;
        OnPropertyChanged(nameof(IsAffineSolvePreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshAffineSolveCommands();
    }

    private void SetAffineSolveSummary(string value)
    {
        affineSolveExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(AffineSolveExecutionSummary));
        OnPropertyChanged(nameof(AffineSolveOutputHashSummary));
        OnPropertyChanged(nameof(AffineSolveEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentAffineSolvePreview));
        OnPropertyChanged(nameof(IsAffineSolvePreviewStale));
        OnPropertyChanged(nameof(IsAffineSolvePreviewPublished));
        RefreshAffineSolveCommands();
    }

    private void RefreshAffineSolveCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
}
