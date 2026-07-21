using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns explicit Preview/Publish state for one typed, raw-height datum-plane
/// result. The numerical residual calculation remains in Library-Noah.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? datumPlaneDeviationPreviewCancellation;
    private C3DDatumPlaneDeviationFeature? datumPlaneDeviationPreviewOutput;
    private readonly Dictionary<string, C3DDatumPlaneDeviationFeature> publishedDatumPlaneDeviationOutputs = new(StringComparer.OrdinalIgnoreCase);
    private bool isDatumPlaneDeviationPreviewRunning;
    private bool isDatumPlaneDeviationPreviewStale;
    private bool isDatumPlaneDeviationPreviewPublished;
    private string datumPlaneDeviationExecutionSummary = "Publish one 3-Point Plane, capture a measurement rectangle, teach the raw-height P2V limit, then Preview explicitly.";

    public event EventHandler<ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs>? DatumPlaneDeviationDisplayRequested;
    public event EventHandler? DatumPlaneDeviationDisplayCleared;

    public bool IsSelectedStepDatumPlaneDeviation => string.Equals(SelectedPipelineStep?.ToolId, "datum-plane-raw-height-deviation", StringComparison.Ordinal);
    public bool IsDatumPlaneDeviationPreviewRunning => isDatumPlaneDeviationPreviewRunning;
    public bool HasCurrentDatumPlaneDeviationPreview => datumPlaneDeviationPreviewOutput is not null && !isDatumPlaneDeviationPreviewStale;
    public bool IsDatumPlaneDeviationPreviewStale => isDatumPlaneDeviationPreviewStale;
    public bool IsDatumPlaneDeviationPreviewPublished => isDatumPlaneDeviationPreviewPublished;
    internal C3DDatumPlaneDeviationFeature? CurrentDatumPlaneDeviationOutput => datumPlaneDeviationPreviewOutput;
    internal bool TryGetPublishedDatumPlaneDeviationOutput(string outputEntityId, out C3DDatumPlaneDeviationFeature? output) =>
        publishedDatumPlaneDeviationOutputs.TryGetValue(outputEntityId, out output);

    internal bool TryGetCurrentDatumPlaneDeviationInputs(
        out C3DThreePointPlaneFeature? plane,
        out ToolRecipeSelection? measurementSelection)
    {
        plane = null;
        measurementSelection = null;
        if (SelectedPipelineStep is not { InputEntityIds.Count: 3 } step
            || !TryGetPublishedThreePointPlaneOutput(step.InputEntityIds[1], out plane)
            || plane is null)
        {
            return false;
        }
        measurementSelection = Selections.FirstOrDefault(item => string.Equals(item.Id, step.InputEntityIds[2], StringComparison.OrdinalIgnoreCase));
        return measurementSelection is not null;
    }

    public string DatumPlaneDeviationExecutionSummary => datumPlaneDeviationExecutionSummary;
    public string DatumPlaneDeviationOutputHashSummary => datumPlaneDeviationPreviewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {datumPlaneDeviationPreviewOutput.ContentSha256}";
    public string DatumPlaneDeviationUpstreamSummary
    {
        get
        {
            var step = SelectedPipelineStep;
            if (step is null || step.InputEntityIds.Count != 3) return "Raw source, Published PlaneFeature, and GridRectangle are required.";
            var planeReady = TryGetPublishedThreePointPlaneOutput(step.InputEntityIds[1], out var plane) && plane is not null;
            var selectionReady = Selections.Any(item => string.Equals(item.Id, step.InputEntityIds[2], StringComparison.OrdinalIgnoreCase)
                && item.Kind == ToolRecipeSelectionKinds.GridRectangle && IsSelectionCurrent(item));
            return $"Plane {step.InputEntityIds[1]}: {(planeReady ? "Published" : "missing/stale")} | ROI {step.InputEntityIds[2]}: {(selectionReady ? "current" : "missing/stale")}";
        }
    }
    public string DatumPlaneDeviationEvidenceSummary => datumPlaneDeviationPreviewOutput is null
        ? "No residual evidence until Preview completes."
        : $"{datumPlaneDeviationPreviewOutput.OutputRole} | P2V {datumPlaneDeviationPreviewOutput.PeakToValleyRawHeight:G6} raw-height | RMS {datumPlaneDeviationPreviewOutput.RmsRawHeightResidual:G6} | {datumPlaneDeviationPreviewOutput.ValidSampleCount:N0} valid samples";

    public async Task<bool> PreviewSelectedDatumPlaneDeviationAsync()
    {
        if (!CanPreviewSelectedDatumPlaneDeviation() || SelectedPipelineStep is not { } step
            || !TryGetCurrentDatumPlaneDeviationInputs(out var plane, out var selection)
            || plane is null || selection is null)
        {
            if (SelectedPipelineStep is { } waiting) waiting.State = "Waiting for upstream";
            SetDatumPlaneDeviationSummary("The current raw C3D, a Published 3-Point Plane, and a current GridRectangle are required.");
            return false;
        }

        datumPlaneDeviationPreviewCancellation?.Dispose();
        datumPlaneDeviationPreviewCancellation = new CancellationTokenSource();
        SetDatumPlaneDeviationRunning(true);
        isDatumPlaneDeviationPreviewStale = false;
        isDatumPlaneDeviationPreviewPublished = false;
        step.State = "Preview running";
        SetDatumPlaneDeviationSummary("Datum Plane Raw-Height Deviation Preview is evaluating the exact Published plane and recipe-owned measurement rectangle.");
        AppendLog("Preview", $"Datum Plane Raw-Height Deviation Preview started: {step.Id}.");
        try
        {
            var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
            var evaluation = await Task.Run(
                () => ToolRecipeDatumPlaneDeviationExecution.Execute(CreateDocument(), step.Id, plane, recipeDirectory, datumPlaneDeviationPreviewCancellation.Token),
                datumPlaneDeviationPreviewCancellation.Token);
            if (evaluation.Output is null || evaluation.Result.Status is not (ResultStatus.Pass or ResultStatus.Fail))
            {
                datumPlaneDeviationPreviewOutput = null;
                step.State = "Error";
                SetDatumPlaneDeviationSummary(evaluation.Result.Message);
                AppendLog("Error", $"Datum Plane Raw-Height Deviation Preview failed: {evaluation.Result.Message}");
                return false;
            }

            datumPlaneDeviationPreviewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetDatumPlaneDeviationSummary($"Preview ready | {DatumPlaneDeviationEvidenceSummary} | local raw-height software result only; source C3D is unchanged.");
            AppendLog("Preview", $"Datum Plane Raw-Height Deviation Preview ready: {evaluation.Output.ContentSha256}.");
            DatumPlaneDeviationDisplayRequested?.Invoke(this, new ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs(plane, selection, evaluation.Output, false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetDatumPlaneDeviationSummary("Preview canceled. The source, Published PlaneFeature, ROI, and authored recipe were not changed.");
            AppendLog("Preview", "Datum Plane Raw-Height Deviation Preview canceled.");
            return false;
        }
        finally
        {
            SetDatumPlaneDeviationRunning(false);
        }
    }

    private bool CanPreviewSelectedDatumPlaneDeviation()
    {
        if (!IsSelectedStepDatumPlaneDeviation || !IsSourceReadyForRecipe || HasPendingStepParameterChanges
            || isDatumPlaneDeviationPreviewRunning || SelectedPipelineStep is not { InputEntityIds.Count: 3 } step
            || !TryGetCurrentDatumPlaneDeviationInputs(out var plane, out _)
            || plane is null)
        {
            return false;
        }
        var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
        return ToolRecipeDatumPlaneDeviationExecution.TryPrepare(CreateDocument(), step.Id, plane, recipeDirectory, out _, out _);
    }

    private void PublishSelectedDatumPlaneDeviation()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentDatumPlaneDeviationPreview
            || !TryGetCurrentDatumPlaneDeviationInputs(out var plane, out var selection)
            || plane is null || selection is null)
        {
            return;
        }
        isDatumPlaneDeviationPreviewPublished = true;
        publishedDatumPlaneDeviationOutputs[datumPlaneDeviationPreviewOutput!.OutputEntityId] = datumPlaneDeviationPreviewOutput;
        step.State = "Published";
        SetDatumPlaneDeviationSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {datumPlaneDeviationPreviewOutput.ContentSha256} | local raw-height software result only; not physical metrology.");
        AppendLog("Publish", $"Datum Plane Raw-Height Deviation output published without re-running: {step.OutputEntityId}.");
        DatumPlaneDeviationDisplayRequested?.Invoke(this, new ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs(plane, selection, datumPlaneDeviationPreviewOutput, true));
    }

    private void CancelDatumPlaneDeviationPreview() => datumPlaneDeviationPreviewCancellation?.Cancel();

    private void MarkDatumPlaneDeviationPreviewStaleIfNeeded(object? sender = null, string? upstreamPlaneOutputId = null)
    {
        if (datumPlaneDeviationPreviewOutput is null || isDatumPlaneDeviationPreviewRunning) return;
        var step = PipelineSteps.FirstOrDefault(item => string.Equals(item.OutputEntityId, datumPlaneDeviationPreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (step is null) return;
        if (upstreamPlaneOutputId is not null
            && !string.Equals(step.InputEntityIds.ElementAtOrDefault(1), upstreamPlaneOutputId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (sender is not null)
        {
            var selectedIsCurrentDatum = IsSelectedStepDatumPlaneDeviation && ReferenceEquals(SelectedPipelineStep, step);
            var isDatumParameter = sender is ToolWorkbenchParameterItem parameter && step.Parameters.Contains(parameter);
            var isUpstreamPlaneStep = sender is ToolWorkbenchPipelineStepItem upstream
                && string.Equals(upstream.OutputEntityId, step.InputEntityIds.ElementAtOrDefault(1), StringComparison.OrdinalIgnoreCase);
            if (!selectedIsCurrentDatum && !isDatumParameter && !isUpstreamPlaneStep) return;
        }
        isDatumPlaneDeviationPreviewStale = true;
        isDatumPlaneDeviationPreviewPublished = false;
        publishedDatumPlaneDeviationOutputs.Clear();
        step.State = "Preview stale";
        DatumPlaneDeviationDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetDatumPlaneDeviationSummary("Published plane, measurement ROI, source, parameter, route, or output changed. Preview again before Publish.");
    }

    private void ClearDatumPlaneDeviationPreview(string summary)
    {
        datumPlaneDeviationPreviewCancellation?.Cancel();
        datumPlaneDeviationPreviewOutput = null;
        publishedDatumPlaneDeviationOutputs.Clear();
        isDatumPlaneDeviationPreviewStale = false;
        isDatumPlaneDeviationPreviewPublished = false;
        DatumPlaneDeviationDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetDatumPlaneDeviationSummary(summary);
    }

    private void RefreshDatumPlaneDeviationExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepDatumPlaneDeviation));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(DatumPlaneDeviationUpstreamSummary));
        if (SelectedPipelineStep is { } step && IsSelectedStepDatumPlaneDeviation
            && (datumPlaneDeviationPreviewOutput is null
                || !string.Equals(datumPlaneDeviationPreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || isDatumPlaneDeviationPreviewStale)
            && !isDatumPlaneDeviationPreviewRunning)
        {
            if (!TryGetCurrentDatumPlaneDeviationInputs(out var plane, out _) || plane is null)
            {
                step.State = "Waiting for upstream";
                datumPlaneDeviationExecutionSummary = "A current raw C3D, Published 3-Point Plane, and current measurement rectangle are required. No upstream tool will run implicitly.";
            }
            else
            {
                var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
                if (ToolRecipeDatumPlaneDeviationExecution.TryPrepare(CreateDocument(), step.Id, plane, recipeDirectory, out _, out var message))
                {
                    step.State = "Ready";
                    datumPlaneDeviationExecutionSummary = "Ready for explicit Preview. Published plane and source residual calculation will not run implicitly.";
                }
                else
                {
                    step.State = "Taught incomplete";
                    datumPlaneDeviationExecutionSummary = message;
                }
            }
        }
        OnPropertyChanged(nameof(DatumPlaneDeviationExecutionSummary));
        OnPropertyChanged(nameof(DatumPlaneDeviationOutputHashSummary));
        OnPropertyChanged(nameof(DatumPlaneDeviationEvidenceSummary));
        RefreshDatumPlaneDeviationCommands();
    }

    private void SetDatumPlaneDeviationRunning(bool value)
    {
        isDatumPlaneDeviationPreviewRunning = value;
        OnPropertyChanged(nameof(IsDatumPlaneDeviationPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshDatumPlaneDeviationCommands();
    }

    private void SetDatumPlaneDeviationSummary(string value)
    {
        datumPlaneDeviationExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(DatumPlaneDeviationExecutionSummary));
        OnPropertyChanged(nameof(DatumPlaneDeviationOutputHashSummary));
        OnPropertyChanged(nameof(DatumPlaneDeviationUpstreamSummary));
        OnPropertyChanged(nameof(DatumPlaneDeviationEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentDatumPlaneDeviationPreview));
        OnPropertyChanged(nameof(IsDatumPlaneDeviationPreviewStale));
        OnPropertyChanged(nameof(IsDatumPlaneDeviationPreviewPublished));
        RefreshDatumPlaneDeviationCommands();
    }

    private void RefreshDatumPlaneDeviationCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
}

public sealed class ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs(
    C3DThreePointPlaneFeature plane,
    ToolRecipeSelection measurementSelection,
    C3DDatumPlaneDeviationFeature output,
    bool isPublished) : EventArgs
{
    public C3DThreePointPlaneFeature Plane { get; } = plane;
    public ToolRecipeSelection MeasurementSelection { get; } = measurementSelection;
    public C3DDatumPlaneDeviationFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}
