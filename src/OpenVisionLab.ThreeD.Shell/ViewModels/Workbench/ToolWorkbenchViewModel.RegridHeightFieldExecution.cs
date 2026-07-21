using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? regridHeightFieldPreviewCancellation;
    private C3DTransformedHeightField? regridHeightFieldPreviewOutput;
    private readonly Dictionary<string, C3DTransformedHeightField> publishedRegridHeightFieldOutputs = new(StringComparer.OrdinalIgnoreCase);
    private bool isRegridHeightFieldPreviewRunning;
    private bool isRegridHeightFieldPreviewStale;
    private bool isRegridHeightFieldPreviewPublished;
    private string regridHeightFieldExecutionSummary = "Route one current Published TransformedPointCloud, author its ReferenceGridProfile, then Preview explicitly.";

    public bool IsSelectedStepRegridHeightField =>
        string.Equals(SelectedPipelineStep?.ToolId, "re-grid-height-map", StringComparison.Ordinal);
    public bool IsRegridHeightFieldPreviewRunning => isRegridHeightFieldPreviewRunning;
    public bool HasCurrentRegridHeightFieldPreview => regridHeightFieldPreviewOutput is not null && !isRegridHeightFieldPreviewStale;
    public bool IsRegridHeightFieldPreviewStale => isRegridHeightFieldPreviewStale;
    public bool IsRegridHeightFieldPreviewPublished => isRegridHeightFieldPreviewPublished;
    internal C3DTransformedHeightField? CurrentRegridHeightFieldOutput => regridHeightFieldPreviewOutput;
    internal bool TryGetPublishedRegridHeightFieldOutput(string outputEntityId, out C3DTransformedHeightField? output) =>
        publishedRegridHeightFieldOutputs.TryGetValue(outputEntityId, out output);
    public string RegridHeightFieldExecutionSummary => regridHeightFieldExecutionSummary;
    public string RegridHeightFieldOutputHashSummary => regridHeightFieldPreviewOutput is null
        ? "No TransformedHeightField hash until Preview completes."
        : $"TransformedHeightField SHA-256 {regridHeightFieldPreviewOutput.ContentSha256}";
    public string RegridHeightFieldUpstreamSummary => TryGetCurrentRegridHeightFieldInput(out var cloud)
        ? $"Published TransformedPointCloud | SHA-256 {cloud.ContentSha256[..12]}"
        : "Publish A2 TransformedPointCloud first; upstream tools do not run implicitly.";
    public string RegridHeightFieldEvidenceSummary => regridHeightFieldPreviewOutput is null
        ? "No reference-grid evidence until Preview completes."
        : $"populated {regridHeightFieldPreviewOutput.PopulatedCellCount:N0}/{regridHeightFieldPreviewOutput.Cells.Count:N0} | coverage {regridHeightFieldPreviewOutput.CoverageRatio:P2} | missing {regridHeightFieldPreviewOutput.MissingCellCount:N0} | collisions {regridHeightFieldPreviewOutput.CollisionCount:N0}";

    public async Task<bool> PreviewSelectedRegridHeightFieldAsync()
    {
        if (!CanPreviewSelectedRegridHeightField() || SelectedPipelineStep is not { } step
            || !TryGetCurrentRegridHeightFieldInput(out var cloud)) return false;

        regridHeightFieldPreviewCancellation?.Dispose();
        regridHeightFieldPreviewCancellation = new CancellationTokenSource();
        SetRegridHeightFieldRunning(true);
        isRegridHeightFieldPreviewStale = false;
        isRegridHeightFieldPreviewPublished = false;
        step.State = "Preview running";
        SetRegridHeightFieldSummary("Re-grid Height Map Preview projects the Published A2 cloud into the authored U/V/H grid. It rejects out-of-bounds input, preserves holes, and does not interpolate, write C3D, or measure.");
        AppendLog("Preview", $"Re-grid Height Map Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeRegridHeightFieldExecution.Execute(CreateDocument(), step.Id, cloud, regridHeightFieldPreviewCancellation.Token),
                regridHeightFieldPreviewCancellation.Token);
            if (evaluation.Result.Status == ResultStatus.Error || evaluation.Output is null)
            {
                regridHeightFieldPreviewOutput = null;
                step.State = "Error";
                SetRegridHeightFieldSummary(evaluation.Result.Message);
                AppendLog("Error", $"Re-grid Height Map Preview failed: {evaluation.Result.Message}");
                return false;
            }

            regridHeightFieldPreviewOutput = evaluation.Output;
            step.State = evaluation.Output.MeetsMinimumCoverage ? "Preview ready" : "Preview coverage below publish minimum";
            SetRegridHeightFieldSummary($"Preview ready | {RegridHeightFieldEvidenceSummary}" + (evaluation.Output.MeetsMinimumCoverage ? string.Empty : " | Publish blocked by the authored minimum coverage ratio."));
            AppendLog("Preview", $"Re-grid Height Map Preview ready: {evaluation.Output.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetRegridHeightFieldSummary("Preview canceled. The Published A2 cloud and authored recipe were not changed.");
            AppendLog("Preview", "Re-grid Height Map Preview canceled.");
            return false;
        }
        finally
        {
            SetRegridHeightFieldRunning(false);
        }
    }

    private bool CanPreviewSelectedRegridHeightField()
    {
        if (!IsSelectedStepRegridHeightField || HasPendingStepParameterChanges || isRegridHeightFieldPreviewRunning
            || !TryGetCurrentRegridHeightFieldInput(out var cloud) || SelectedPipelineStep is not { } step) return false;
        return ToolRecipeRegridHeightFieldExecution.TryValidateRoute(CreateDocument(), step.Id, cloud, out _, out _, out _);
    }

    private void PublishSelectedRegridHeightField()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentRegridHeightFieldPreview || !regridHeightFieldPreviewOutput!.MeetsMinimumCoverage) return;
        isRegridHeightFieldPreviewPublished = true;
        publishedRegridHeightFieldOutputs[regridHeightFieldPreviewOutput.OutputEntityId] = regridHeightFieldPreviewOutput;
        step.State = "Published";
        SetRegridHeightFieldSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {regridHeightFieldPreviewOutput.ContentSha256} | no interpolation or measurement was run.");
        AppendLog("Publish", $"Re-grid Height Map output published without re-running: {step.OutputEntityId}.");
    }

    private void CancelRegridHeightFieldPreview() => regridHeightFieldPreviewCancellation?.Cancel();

    private void ClearRegridHeightFieldPreview(string summary)
    {
        regridHeightFieldPreviewCancellation?.Cancel();
        regridHeightFieldPreviewOutput = null;
        publishedRegridHeightFieldOutputs.Clear();
        isRegridHeightFieldPreviewStale = false;
        isRegridHeightFieldPreviewPublished = false;
        SetRegridHeightFieldSummary(summary);
    }

    private void RefreshRegridHeightFieldExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepRegridHeightField));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(RegridHeightFieldUpstreamSummary));
        if (SelectedPipelineStep is { } step && IsSelectedStepRegridHeightField)
        {
            var message = "Re-grid Height Map v1 route is incomplete.";
            C3DReferenceGridProfile? profile = null;
            var hasCurrentRoute = TryGetCurrentRegridHeightFieldInput(out var cloud)
                && ToolRecipeRegridHeightFieldExecution.TryValidateRoute(CreateDocument(), step.Id, cloud, out _, out profile, out message);
            var outputMatches = regridHeightFieldPreviewOutput is not null && hasCurrentRoute && profile is not null
                && string.Equals(regridHeightFieldPreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(regridHeightFieldPreviewOutput.SourceContentSha256, cloud.ContentSha256, StringComparison.OrdinalIgnoreCase)
                && string.Equals(regridHeightFieldPreviewOutput.ReferenceGridProfileSha256, profile.ContentSha256, StringComparison.OrdinalIgnoreCase);
            if (regridHeightFieldPreviewOutput is not null && !outputMatches && !isRegridHeightFieldPreviewRunning)
            {
                isRegridHeightFieldPreviewStale = true;
                isRegridHeightFieldPreviewPublished = false;
                publishedRegridHeightFieldOutputs.Clear();
                step.State = "Preview stale";
                regridHeightFieldExecutionSummary = "The A2 output route or authored ReferenceGridProfile changed. Preview again before Publish.";
            }
            else if (regridHeightFieldPreviewOutput is null || isRegridHeightFieldPreviewStale)
            {
                if (!TryGetCurrentRegridHeightFieldInput(out _))
                {
                    step.State = "Waiting for upstream";
                    regridHeightFieldExecutionSummary = "Publish the current A2 TransformedPointCloud first. A1/A2 are not executed implicitly.";
                }
                else if (hasCurrentRoute)
                {
                    step.State = "Ready";
                    regridHeightFieldExecutionSummary = "Ready for explicit Preview. A3 preserves holes, rejects out-of-bounds points, and only enables Publish after the authored coverage gate.";
                }
                else
                {
                    step.State = "Taught incomplete";
                    regridHeightFieldExecutionSummary = message;
                }
            }
        }
        OnPropertyChanged(nameof(RegridHeightFieldExecutionSummary));
        OnPropertyChanged(nameof(RegridHeightFieldOutputHashSummary));
        OnPropertyChanged(nameof(RegridHeightFieldEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentRegridHeightFieldPreview));
        OnPropertyChanged(nameof(IsRegridHeightFieldPreviewStale));
        OnPropertyChanged(nameof(IsRegridHeightFieldPreviewPublished));
        OnPropertyChanged(nameof(AlignmentStatusSummary));
        RefreshRegridHeightFieldCommands();
    }

    private bool TryGetCurrentRegridHeightFieldInput(out C3DTransformedPointCloud cloud)
    {
        cloud = null!;
        if (SelectedPipelineStep is not { InputEntityIds.Count: 1 } step
            || !TryGetPublishedAffineApplyOutput(step.InputEntityIds[0], out var published)
            || published is null) return false;
        cloud = published;
        return true;
    }

    private void SetRegridHeightFieldRunning(bool value)
    {
        isRegridHeightFieldPreviewRunning = value;
        OnPropertyChanged(nameof(IsRegridHeightFieldPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshRegridHeightFieldCommands();
    }

    private void SetRegridHeightFieldSummary(string value)
    {
        regridHeightFieldExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(RegridHeightFieldExecutionSummary));
        OnPropertyChanged(nameof(RegridHeightFieldOutputHashSummary));
        OnPropertyChanged(nameof(RegridHeightFieldEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentRegridHeightFieldPreview));
        OnPropertyChanged(nameof(IsRegridHeightFieldPreviewStale));
        OnPropertyChanged(nameof(IsRegridHeightFieldPreviewPublished));
        RefreshRegridHeightFieldCommands();
    }

    private void RefreshRegridHeightFieldCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
}
