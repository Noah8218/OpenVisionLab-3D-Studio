using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? affineApplyPreviewCancellation;
    private C3DTransformedPointCloud? affineApplyPreviewOutput;
    private readonly Dictionary<string, C3DTransformedPointCloud> publishedAffineApplyOutputs = new(StringComparer.OrdinalIgnoreCase);
    private bool isAffineApplyPreviewRunning;
    private bool isAffineApplyPreviewStale;
    private bool isAffineApplyPreviewPublished;
    private string affineApplyExecutionSummary = "Route the verified raw C3D first and the current Published AffineTransform3D second, then Preview explicitly.";

    public bool IsSelectedStepXYZAffineApply =>
        string.Equals(SelectedPipelineStep?.ToolId, "xyz-affine-apply", StringComparison.Ordinal);
    public bool IsAffineApplyPreviewRunning => isAffineApplyPreviewRunning;
    public bool HasCurrentAffineApplyPreview => affineApplyPreviewOutput is not null && !isAffineApplyPreviewStale;
    public bool IsAffineApplyPreviewStale => isAffineApplyPreviewStale;
    public bool IsAffineApplyPreviewPublished => isAffineApplyPreviewPublished;
    internal C3DTransformedPointCloud? CurrentAffineApplyOutput => affineApplyPreviewOutput;
    internal bool TryGetPublishedAffineApplyOutput(string outputEntityId, out C3DTransformedPointCloud? output) =>
        publishedAffineApplyOutputs.TryGetValue(outputEntityId, out output);
    internal bool TryRegisterSyntheticPublishedAffineApplyOutputForSmoke(
        C3DTransformedPointCloud output,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (SelectedPipelineStep is not { ToolId: "re-grid-height-map", InputEntityIds.Count: 1 } regridStep
            || !string.Equals(regridStep.InputEntityIds[0], output.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || !PipelineSteps.Any(step =>
                string.Equals(step.ToolId, "xyz-affine-apply", StringComparison.Ordinal)
                && string.Equals(step.OutputEntityId, output.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            || !string.Equals(Source.Id, output.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || loadedSourceBinding is null
            || !string.Equals(loadedSourceBinding.ContentSha256, output.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || loadedSourceBinding.GridWidth != output.SourceGridWidth
            || loadedSourceBinding.GridHeight != output.SourceGridHeight)
        {
            message = "Synthetic smoke A2 identity does not match the selected Re-grid route and loaded recipe source.";
            return false;
        }

        publishedAffineApplyOutputs[output.OutputEntityId] = output;
        AppendLog(
            "Smoke",
            $"Registered deterministic synthetic Published A2 prerequisite {output.OutputEntityId} ({output.ContentSha256}); normal Re-grid Preview/Publish remains explicit.");
        RefreshRegridHeightFieldExecutionState();
        message = $"Synthetic Published A2 registered for smoke-only execution: {output.ContentSha256}";
        return true;
    }
    public string AffineApplyExecutionSummary => affineApplyExecutionSummary;
    public string AffineApplyOutputHashSummary => affineApplyPreviewOutput is null
        ? "No TransformedPointCloud hash until Preview completes."
        : $"TransformedPointCloud SHA-256 {affineApplyPreviewOutput.ContentSha256}";
    public string AffineApplyUpstreamSummary => TryGetCurrentAffineApplyInput(out var transform)
        ? $"Raw C3D + Published AffineTransform3D | matrix SHA-256 {transform.ContentSha256[..12]}"
        : "Route the raw recipe C3D first and one current Published AffineTransform3D second.";
    public string AffineApplyEvidenceSummary => affineApplyPreviewOutput is null
        ? "No transformed point evidence until Preview completes."
        : $"finite {affineApplyPreviewOutput.FinitePointCount:N0} | missing source cells {affineApplyPreviewOutput.MissingPointCount:N0} | source-grid order retained | re-grid excluded";

    public async Task<bool> PreviewSelectedXYZAffineApplyAsync()
    {
        if (!CanPreviewSelectedXYZAffineApply() || SelectedPipelineStep is not { } step
            || !TryGetCurrentAffineApplyInput(out var transform)) return false;

        affineApplyPreviewCancellation?.Dispose();
        affineApplyPreviewCancellation = new CancellationTokenSource();
        SetAffineApplyRunning(true);
        isAffineApplyPreviewStale = false;
        isAffineApplyPreviewPublished = false;
        step.State = "Preview running";
        SetAffineApplySummary("Apply XYZ Affine Preview verifies the raw C3D identity, then transforms each finite source-grid point once. It does not re-grid, interpolate, or measure.");
        AppendLog("Preview", $"Apply XYZ Affine Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeXYZAffineApplyExecution.Execute(
                    CreateDocument(), step.Id, transform, cancellationToken: affineApplyPreviewCancellation.Token),
                affineApplyPreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                affineApplyPreviewOutput = null;
                step.State = "Error";
                SetAffineApplySummary(evaluation.Result.Message);
                AppendLog("Error", $"Apply XYZ Affine Preview failed: {evaluation.Result.Message}");
                return false;
            }

            affineApplyPreviewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetAffineApplySummary($"Preview ready | {AffineApplyEvidenceSummary}");
            AppendLog("Preview", $"Apply XYZ Affine Preview ready: {evaluation.Output.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetAffineApplySummary("Preview canceled. The raw C3D, Published AffineTransform3D, and authored recipe were not changed.");
            AppendLog("Preview", "Apply XYZ Affine Preview canceled.");
            return false;
        }
        finally
        {
            SetAffineApplyRunning(false);
        }
    }

    private bool CanPreviewSelectedXYZAffineApply()
    {
        if (!IsSelectedStepXYZAffineApply || !IsSourceReadyForRecipe || HasPendingStepParameterChanges
            || isAffineApplyPreviewRunning || !TryGetCurrentAffineApplyInput(out var transform)
            || SelectedPipelineStep is not { } step) return false;
        return ToolRecipeXYZAffineApplyExecution.TryValidateRoute(CreateDocument(), step.Id, transform, out _, out _);
    }

    private void PublishSelectedXYZAffineApply()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentAffineApplyPreview) return;
        isAffineApplyPreviewPublished = true;
        publishedAffineApplyOutputs[affineApplyPreviewOutput!.OutputEntityId] = affineApplyPreviewOutput;
        step.State = "Published";
        SetAffineApplySummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {affineApplyPreviewOutput.ContentSha256} | raw source remains unchanged; A3 re-grid remains separate.");
        AppendLog("Publish", $"Apply XYZ Affine output published without re-running: {step.OutputEntityId}.");
    }

    private void CancelXYZAffineApplyPreview() => affineApplyPreviewCancellation?.Cancel();

    private void ClearXYZAffineApplyPreview(string summary)
    {
        affineApplyPreviewCancellation?.Cancel();
        affineApplyPreviewOutput = null;
        publishedAffineApplyOutputs.Clear();
        isAffineApplyPreviewStale = false;
        isAffineApplyPreviewPublished = false;
        SetAffineApplySummary(summary);
        ClearRegridHeightFieldPreview("Published A2 TransformedPointCloud changed. Re-grid Height Map Preview was cleared without execution.");
    }

    private void RefreshXYZAffineApplyExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepXYZAffineApply));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(AffineApplyUpstreamSummary));
        if (SelectedPipelineStep is { } step && IsSelectedStepXYZAffineApply)
        {
            var hasCurrentRoute = TryGetCurrentAffineApplyInput(out var transform);
            var outputMatches = affineApplyPreviewOutput is not null
                && string.Equals(affineApplyPreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                && hasCurrentRoute
                && string.Equals(affineApplyPreviewOutput.AffineTransformContentSha256, transform.ContentSha256, StringComparison.OrdinalIgnoreCase);
            if (affineApplyPreviewOutput is not null && !outputMatches && !isAffineApplyPreviewRunning)
            {
                isAffineApplyPreviewStale = true;
                isAffineApplyPreviewPublished = false;
                publishedAffineApplyOutputs.Clear();
                step.State = "Preview stale";
                affineApplyExecutionSummary = "The route or current Published AffineTransform3D changed. Preview again before Publish.";
            }
            else if (affineApplyPreviewOutput is null || isAffineApplyPreviewStale)
            {
                if (!hasCurrentRoute)
                {
                    step.State = "Waiting for upstream";
                    affineApplyExecutionSummary = "Route the raw recipe C3D first and the current Published AffineTransform3D second. Upstream tools do not run implicitly.";
                }
                else if (ToolRecipeXYZAffineApplyExecution.TryValidateRoute(CreateDocument(), step.Id, transform, out _, out var message))
                {
                    step.State = "Ready";
                    affineApplyExecutionSummary = "Ready for explicit Preview. A2 creates an ordered transformed point cloud only; A3 re-grid is separate.";
                }
                else
                {
                    step.State = "Taught incomplete";
                    affineApplyExecutionSummary = message;
                }
            }
        }
        OnPropertyChanged(nameof(AffineApplyExecutionSummary));
        OnPropertyChanged(nameof(AffineApplyOutputHashSummary));
        OnPropertyChanged(nameof(AffineApplyEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentAffineApplyPreview));
        OnPropertyChanged(nameof(IsAffineApplyPreviewStale));
        OnPropertyChanged(nameof(IsAffineApplyPreviewPublished));
        OnPropertyChanged(nameof(AlignmentStatusSummary));
        RefreshAffineApplyCommands();
    }

    private bool TryGetCurrentAffineApplyInput(out C3DAffineTransform3D transform)
    {
        transform = null!;
        if (SelectedPipelineStep is not { InputEntityIds.Count: 2 } step
            || !string.Equals(step.InputEntityIds[0], Source.Id, StringComparison.OrdinalIgnoreCase)
            || !TryGetPublishedAffineSolveOutput(step.InputEntityIds[1], out var published)
            || published is null) return false;
        transform = published;
        return true;
    }

    private void SetAffineApplyRunning(bool value)
    {
        isAffineApplyPreviewRunning = value;
        OnPropertyChanged(nameof(IsAffineApplyPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshAffineApplyCommands();
    }

    private void SetAffineApplySummary(string value)
    {
        affineApplyExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(AffineApplyExecutionSummary));
        OnPropertyChanged(nameof(AffineApplyOutputHashSummary));
        OnPropertyChanged(nameof(AffineApplyEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentAffineApplyPreview));
        OnPropertyChanged(nameof(IsAffineApplyPreviewStale));
        OnPropertyChanged(nameof(IsAffineApplyPreviewPublished));
        RefreshAffineApplyCommands();
    }

    private void RefreshAffineApplyCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
}
