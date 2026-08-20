using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchRegridHeightFieldExecutionOwner
{
    private readonly Func<bool> isSelectedStepRegridHeightField;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<string, C3DTransformedPointCloud?> getPublishedAffineApplyOutput;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action onPublishedOutputChanged;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DTransformedHeightField? previewOutput;
    private readonly Dictionary<string, C3DTransformedHeightField> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Route one current Published TransformedPointCloud, author its ReferenceGridProfile, then Preview explicitly.";

    public ToolWorkbenchRegridHeightFieldExecutionOwner(
        Func<bool> isSelectedStepRegridHeightField,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> hasPendingStepParameterChanges,
        Func<string, C3DTransformedPointCloud?> getPublishedAffineApplyOutput,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action onPublishedOutputChanged,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepRegridHeightField = isSelectedStepRegridHeightField;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.getPublishedAffineApplyOutput = getPublishedAffineApplyOutput;
        this.createDocument = createDocument;
        this.appendLog = appendLog;
        this.onPublishedOutputChanged = onPublishedOutputChanged;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStepRegridHeightField => isSelectedStepRegridHeightField();
    public bool IsPreviewRunning => isPreviewRunning;
    public bool HasCurrentPreview => previewOutput is not null && !isPreviewStale;
    public bool IsPreviewStale => isPreviewStale;
    public bool IsPreviewPublished => isPreviewPublished;
    public bool CanPublish => HasCurrentPreview && !isPreviewPublished && previewOutput!.MeetsMinimumCoverage;
    public C3DTransformedHeightField? CurrentOutput => previewOutput;
    public string ExecutionSummary => executionSummary;

    public string OutputHashSummary => previewOutput is null
        ? "No TransformedHeightField hash until Preview completes."
        : $"TransformedHeightField SHA-256 {previewOutput.ContentSha256}";

    public string UpstreamSummary => TryGetCurrentInput(out var cloud)
        ? $"Published TransformedPointCloud | SHA-256 {cloud.ContentSha256[..12]}"
        : "Publish A2 TransformedPointCloud first; upstream tools do not run implicitly.";

    public string EvidenceSummary => previewOutput is null
        ? "No reference-grid evidence until Preview completes."
        : $"populated {previewOutput.PopulatedCellCount:N0}/{previewOutput.Cells.Count:N0} | coverage {previewOutput.CoverageRatio:P2} | missing {previewOutput.MissingCellCount:N0} | collisions {previewOutput.CollisionCount:N0}";

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DTransformedHeightField? output) =>
        publishedOutputs.TryGetValue(outputEntityId, out output);

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview() || getSelectedPipelineStep() is not { } step
            || !TryGetCurrentInput(out var cloud))
        {
            return false;
        }

        previewCancellation?.Dispose();
        previewCancellation = new CancellationTokenSource();
        SetRunning(true);
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Re-grid Height Map Preview projects the Published A2 cloud into the authored U/V/H grid. It rejects out-of-bounds input, preserves holes, and does not interpolate, write C3D, or measure.");
        appendLog("Preview", $"Re-grid Height Map Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeRegridHeightFieldExecution.Execute(
                    createDocument(),
                    step.Id,
                    cloud,
                    previewCancellation.Token),
                previewCancellation.Token);
            if (evaluation.Result.Status == ResultStatus.Error || evaluation.Output is null)
            {
                previewOutput = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Re-grid Height Map Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            step.State = evaluation.Output.MeetsMinimumCoverage
                ? "Preview ready"
                : "Preview coverage below publish minimum";
            SetSummary($"Preview ready | {EvidenceSummary}"
                + (evaluation.Output.MeetsMinimumCoverage
                    ? string.Empty
                    : " | Publish blocked by the authored minimum coverage ratio."));
            appendLog("Preview", $"Re-grid Height Map Preview ready: {evaluation.Output.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. The Published A2 cloud and authored recipe were not changed.");
            appendLog("Preview", "Re-grid Height Map Preview canceled.");
            return false;
        }
        finally
        {
            SetRunning(false);
        }
    }

    public bool CanPreview()
    {
        if (!IsSelectedStepRegridHeightField || hasPendingStepParameterChanges() || isPreviewRunning
            || !TryGetCurrentInput(out var cloud) || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        return ToolRecipeRegridHeightFieldExecution.TryValidateRoute(
            createDocument(),
            step.Id,
            cloud,
            out _,
            out _,
            out _);
    }

    public void Publish()
    {
        if (getSelectedPipelineStep() is not { } step || !CanPublish)
        {
            return;
        }

        isPreviewPublished = true;
        publishedOutputs[previewOutput!.OutputEntityId] = previewOutput;
        onPublishedOutputChanged();
        step.State = "Published";
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput.ContentSha256} | no interpolation or measurement was run.");
        appendLog("Publish", $"Re-grid Height Map output published without re-running: {step.OutputEntityId}.");
    }

    public void Cancel() => previewCancellation?.Cancel();

    public void Clear(string summary)
    {
        previewCancellation?.Cancel();
        previewOutput = null;
        publishedOutputs.Clear();
        isPreviewStale = false;
        isPreviewPublished = false;
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (getSelectedPipelineStep() is { } step && IsSelectedStepRegridHeightField)
        {
            var message = "Re-grid Height Map v1 route is incomplete.";
            C3DReferenceGridProfile? profile = null;
            var hasCurrentRoute = TryGetCurrentInput(out var cloud)
                && ToolRecipeRegridHeightFieldExecution.TryValidateRoute(
                    createDocument(),
                    step.Id,
                    cloud,
                    out _,
                    out profile,
                    out message);
            var outputMatches = previewOutput is not null && hasCurrentRoute && profile is not null
                && string.Equals(previewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(previewOutput.SourceContentSha256, cloud.ContentSha256, StringComparison.OrdinalIgnoreCase)
                && string.Equals(previewOutput.ReferenceGridProfileSha256, profile.ContentSha256, StringComparison.OrdinalIgnoreCase);
            if (previewOutput is not null && !outputMatches && !isPreviewRunning)
            {
                isPreviewStale = true;
                isPreviewPublished = false;
                publishedOutputs.Clear();
                step.State = "Preview stale";
                executionSummary = "The A2 output route or authored ReferenceGridProfile changed. Preview again before Publish.";
            }
            else if (previewOutput is null || isPreviewStale)
            {
                if (!TryGetCurrentInput(out _))
                {
                    step.State = "Waiting for upstream";
                    executionSummary = "Publish the current A2 TransformedPointCloud first. A1/A2 are not executed implicitly.";
                }
                else if (hasCurrentRoute)
                {
                    step.State = "Ready";
                    executionSummary = "Ready for explicit Preview. A3 preserves holes, rejects out-of-bounds points, and only enables Publish after the authored coverage gate.";
                }
                else
                {
                    step.State = "Taught incomplete";
                    executionSummary = message;
                }
            }
        }

        onExecutionStateChanged();
    }

    private bool TryGetCurrentInput(out C3DTransformedPointCloud cloud)
    {
        cloud = null!;
        if (getSelectedPipelineStep() is not { InputEntityIds.Count: 1 } step
            || getPublishedAffineApplyOutput(step.InputEntityIds[0]) is not { } published)
        {
            return false;
        }

        cloud = published;
        return true;
    }

    private void SetRunning(bool value)
    {
        isPreviewRunning = value;
        onExecutionStateChanged();
    }

    private void SetSummary(string value)
    {
        executionSummary = value;
        onExecutionStateChanged();
    }
}
