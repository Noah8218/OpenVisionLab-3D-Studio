using System.Collections.ObjectModel;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchLineFitExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelectedStepLineFit;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<bool> isEdgePreviewRunning;
    private readonly Func<string, C3DHeightDifferenceEdgePointSet?> getPublishedEdge;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchLineFitDisplayRequestEventArgs> requestDisplay;
    private readonly Action clearDisplay;
    private readonly Action refreshLineIntersection;
    private readonly Action markLineIntersectionStale;
    private readonly Action clearLineIntersection;
    private readonly Action onExecutionStateChanged;
    private readonly Action onDiagnosticSelectionChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DLineFeature? previewOutput;
    private readonly Dictionary<string, C3DLineFeature> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private C3DLineFeaturePointDiagnostic? selectedDiagnostic;
    private string executionSummary =
        "Teach all four Line Fit limits, then publish the exact upstream EdgePointSet before Preview.";
    private int disposalState;

    public ToolWorkbenchLineFitExecutionOwner(
        Func<bool> isSelectedStepLineFit,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<bool> isEdgePreviewRunning,
        Func<string, C3DHeightDifferenceEdgePointSet?> getPublishedEdge,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action<ToolWorkbenchLineFitDisplayRequestEventArgs> requestDisplay,
        Action clearDisplay,
        Action refreshLineIntersection,
        Action markLineIntersectionStale,
        Action clearLineIntersection,
        Action onExecutionStateChanged,
        Action onDiagnosticSelectionChanged)
    {
        this.isSelectedStepLineFit = isSelectedStepLineFit;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.isEdgePreviewRunning = isEdgePreviewRunning;
        this.getPublishedEdge = getPublishedEdge;
        this.findStepByOutputEntityId = findStepByOutputEntityId;
        this.createDocument = createDocument;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.clearDisplay = clearDisplay;
        this.refreshLineIntersection = refreshLineIntersection;
        this.markLineIntersectionStale = markLineIntersectionStale;
        this.clearLineIntersection = clearLineIntersection;
        this.onExecutionStateChanged = onExecutionStateChanged;
        this.onDiagnosticSelectionChanged = onDiagnosticSelectionChanged;
        SelectDiagnosticCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is int index)
                {
                    SelectDiagnostic(index);
                }
                else if (int.TryParse(parameter?.ToString(), out var parsed))
                {
                    SelectDiagnostic(parsed);
                }
            },
            _ => HasCurrentPreview);
    }

    public bool IsSelectedStepLineFit => !IsDisposed && isSelectedStepLineFit();
    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed && previewOutput is not null && !isPreviewStale;
    public bool IsPreviewStale => !IsDisposed && isPreviewStale;
    public bool IsPreviewPublished => !IsDisposed && isPreviewPublished;
    public C3DLineFeature? CurrentOutput => IsDisposed ? null : previewOutput;
    public C3DLineFeaturePointDiagnostic? SelectedDiagnostic => IsDisposed ? null : selectedDiagnostic;
    public IReadOnlyList<C3DLineFeaturePointDiagnostic> PointDiagnostics =>
        IsDisposed ? [] : previewOutput?.PointDiagnostics ?? [];
    public ObservableCollection<LineFitResidualPlotPoint> ResidualPlotPoints { get; } = [];
    public RelayCommand SelectDiagnosticCommand { get; }
    public string ExecutionSummary => IsDisposed
        ? "Line Fit execution owner has been disposed."
        : executionSummary;

    public string OutputHashSummary => IsDisposed || previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public string UpstreamSummary
    {
        get
        {
            if (IsDisposed)
            {
                return "Line Fit execution owner has been disposed.";
            }

            var step = getSelectedPipelineStep();
            if (step is null || step.InputEntityIds.Count != 1)
            {
                return "Missing routed EdgePointSet";
            }

            var output = getPublishedEdge(step.InputEntityIds[0]);
            return output is not null
                ? $"{step.InputEntityIds[0]} | Published | {output.ContentSha256[..12]}"
                : $"{step.InputEntityIds[0]} | no current Published EdgePointSet";
        }
    }

    public string SelectedDiagnosticSummary => IsDisposed || selectedDiagnostic is null
        ? "Select an inlier/outlier diagnostic to review its source-coordinate residual."
        : $"scanline {selectedDiagnostic.ScanlineIndex} | residual {selectedDiagnostic.OrthogonalResidual:G6} source-coordinate | {(selectedDiagnostic.IsInlier ? "inlier" : "outlier")} | XYZ ({selectedDiagnostic.X:G6}, {selectedDiagnostic.Y:G6}, {selectedDiagnostic.Z:G6})";

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool TryGetPublishedOutput(string outputEntityId, out C3DLineFeature? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        return publishedOutputs.TryGetValue(outputEntityId, out output);
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var currentCancellation = new CancellationTokenSource();
        var cancellationToken = currentCancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref previewCancellation,
            currentCancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    currentCancellation),
                currentCancellation))
            {
                currentCancellation.Dispose();
            }

            return false;
        }

        SetRunning(true);
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("3D Line Fit Preview is running from the exact Published EdgePointSet.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"3D Line Fit Preview started: {step.Id}.");
        try
        {
            var publishedEdge = getPublishedEdge(step.InputEntityIds[0]);
            if (publishedEdge is null)
            {
                step.State = "Waiting for upstream";
                SetSummary("The routed EdgePointSet is not current and Published.");
                return false;
            }

            var evaluation = await Task.Run(
                () => ToolRecipeLineFitExecution.Execute(
                    createDocument(),
                    step.Id,
                    publishedEdge,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                previewOutput = null;
                selectedDiagnostic = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (!IsCurrentPreview(currentCancellation))
                {
                    return false;
                }

                appendLog("Error", $"3D Line Fit Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            selectedDiagnostic = evaluation.Output.PointDiagnostics.FirstOrDefault();
            RebuildResidualPlot(evaluation.Output);
            step.State = "Preview ready";
            var diagnostics = evaluation.Output.Diagnostics;
            SetSummary($"Preview ready | inliers {diagnostics.InlierCount:N0}/{diagnostics.InputPointCount:N0} ({diagnostics.InlierRatio:P1}) | residual RMS {diagnostics.ResidualRms:G6} source-coordinate | no OK/NG");
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            appendLog("Preview", $"3D Line Fit Preview ready: {evaluation.Output.ContentSha256}.");
            if (IsCurrentPreview(currentCancellation))
            {
                requestDisplay(new ToolWorkbenchLineFitDisplayRequestEventArgs(evaluation.Output, false));
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetSummary("Preview canceled. Published EdgePointSet and authored recipe were not changed.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", "3D Line Fit Preview canceled.");
            }

            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    currentCancellation),
                currentCancellation);
            if (ownsCancellation)
            {
                currentCancellation.Dispose();
                if (!IsDisposed)
                {
                    SetRunning(false);
                }
            }
        }
    }

    public bool CanPreview()
    {
        if (IsDisposed || !IsSelectedStepLineFit || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges() || isPreviewRunning
            || isEdgePreviewRunning() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var publishedEdge = getPublishedEdge(step.InputEntityIds.Single());
        return publishedEdge is not null
            && ToolRecipeLineFitExecution.TryPrepare(
                createDocument(),
                step.Id,
                publishedEdge,
                out _,
                out _);
    }

    public void Publish()
    {
        if (IsDisposed || getSelectedPipelineStep() is not { } step || !HasCurrentPreview)
        {
            return;
        }

        isPreviewPublished = true;
        publishedOutputs[previewOutput!.OutputEntityId] = previewOutput;
        step.State = "Published";
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput.ContentSha256} | feature extraction only, no OK/NG");
        if (IsDisposed)
        {
            return;
        }

        appendLog("Publish", $"3D Line Fit output published without re-running: {step.OutputEntityId}.");
        if (!IsDisposed)
        {
            requestDisplay(new ToolWorkbenchLineFitDisplayRequestEventArgs(previewOutput, true));
        }

        if (!IsDisposed)
        {
            refreshLineIntersection();
        }
    }

    public void Cancel()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref previewCancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal or replacement already released the token source.
        }
    }

    public void SelectDiagnostic(int inputPointIndex)
    {
        if (IsDisposed)
        {
            return;
        }

        var value = PointDiagnostics.FirstOrDefault(point => point.InputPointIndex == inputPointIndex);
        if (ReferenceEquals(selectedDiagnostic, value))
        {
            return;
        }

        selectedDiagnostic = value;
        onDiagnosticSelectionChanged();
    }

    public void MarkStaleIfNeeded(object? sender = null)
    {
        if (IsDisposed || previewOutput is null || isPreviewRunning)
        {
            return;
        }

        if (sender is not null)
        {
            var selected = getSelectedPipelineStep();
            var selectedIsLineFit = string.Equals(
                selected?.ToolId,
                "three-d-line-fit",
                StringComparison.Ordinal);
            var selectedIsCurrentLineFit = selectedIsLineFit
                && string.Equals(
                    selected?.OutputEntityId,
                    previewOutput.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase);
            var isSelectedLineFitParameter = selectedIsLineFit
                && sender is ToolWorkbenchParameterItem parameter
                && (selected?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsCurrentLineFit
                || (!ReferenceEquals(sender, selected) && !isSelectedLineFitParameter))
            {
                return;
            }
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        publishedOutputs.Clear();
        if (!IsDisposed)
        {
            markLineIntersectionStale();
        }

        ResidualPlotPoints.Clear();
        if (IsDisposed)
        {
            return;
        }

        var step = findStepByOutputEntityId(previewOutput.OutputEntityId);
        if (step is not null)
        {
            step.State = "Preview stale";
        }

        clearDisplay();
        if (IsDisposed)
        {
            return;
        }

        SetSummary("Input, Line Fit parameter, route, or output changed. Preview again before Publish.");
    }

    public void Clear(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref previewCancellation,
            null);
        CancelAndDispose(currentCancellation);
        previewOutput = null;
        publishedOutputs.Clear();
        clearLineIntersection();
        selectedDiagnostic = null;
        ResidualPlotPoints.Clear();
        SelectDiagnosticCommand.RaiseCanExecuteChanged();
        isPreviewStale = false;
        isPreviewPublished = false;
        clearDisplay();
        if (IsDisposed)
        {
            return;
        }

        SetSummary(summary);
        if (IsDisposed)
        {
            return;
        }
    }

    public void RefreshState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedPipelineStep() is { } step && IsSelectedStepLineFit
            && (previewOutput is null
                || !string.Equals(
                    previewOutput.OutputEntityId,
                    step.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase)
                || isPreviewStale)
            && !isPreviewRunning)
        {
            var publishedEdge = getPublishedEdge(step.InputEntityIds.Single());
            if (publishedEdge is null)
            {
                step.State = "Waiting for upstream";
            }
            else if (ToolRecipeLineFitExecution.TryPrepare(
                createDocument(),
                step.Id,
                publishedEdge,
                out _,
                out var message))
            {
                step.State = "Ready";
                executionSummary = "Ready for explicit Preview. Height Difference Edge will not run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                executionSummary = message;
            }
        }

        onExecutionStateChanged();
    }

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isPreviewRunning = value;
        SelectDiagnosticCommand.RaiseCanExecuteChanged();
        onExecutionStateChanged();
    }

    private void SetSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        executionSummary = value;
        SelectDiagnosticCommand.RaiseCanExecuteChanged();
        onExecutionStateChanged();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref previewCancellation,
            null);
        CancelAndDispose(currentCancellation);
        previewOutput = null;
        publishedOutputs.Clear();
        selectedDiagnostic = null;
        ResidualPlotPoints.Clear();
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
        SelectDiagnosticCommand.RaiseCanExecuteChanged();
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref previewCancellation),
            cancellation);

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal or replacement already released the token source.
        }

        cancellation.Dispose();
    }

    private void RebuildResidualPlot(C3DLineFeature output)
    {
        ResidualPlotPoints.Clear();
        var points = output.PointDiagnostics;
        if (points.Count == 0)
        {
            return;
        }

        var maximum = Math.Max(
            output.MaximumOrthogonalResidual,
            points.Max(point => point.OrthogonalResidual));
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var x = points.Count == 1 ? 142 : 8 + 268 * index / (points.Count - 1.0);
            var y = 64 - 54 * point.OrthogonalResidual / maximum;
            ResidualPlotPoints.Add(new LineFitResidualPlotPoint(
                point.InputPointIndex,
                point.ScanlineIndex,
                Math.Clamp(x, 0, 280),
                Math.Clamp(y, 2, 64),
                point.IsInlier,
                point.OrthogonalResidual));
        }
    }
}
