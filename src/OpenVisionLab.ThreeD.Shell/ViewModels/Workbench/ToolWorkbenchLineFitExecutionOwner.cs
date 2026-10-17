using System.Collections.ObjectModel;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
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
    private ToolWorkbenchPipelineStepItem? previewStep;
    private string previewContext = string.Empty;
    private readonly Dictionary<string, (C3DLineFeature Output, string StepId, string Context)> publishedOutputs =
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
        : $"scanline {selectedDiagnostic.ScanlineIndex} | residual {selectedDiagnostic.OrthogonalResidual:0.##} source-coordinate | {(selectedDiagnostic.IsInlier ? "inlier" : "outlier")} | XYZ ({selectedDiagnostic.X:0.##}, {selectedDiagnostic.Y:0.##}, {selectedDiagnostic.Z:0.##})";

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool TryGetPublishedOutput(string outputEntityId, out C3DLineFeature? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        if (publishedOutputs.TryGetValue(outputEntityId, out var published)
            && string.Equals(published.Context, createDocument().CreateStepExecutionContext(published.StepId), StringComparison.Ordinal))
        {
            output = published.Output;
            return true;
        }
        output = null;
        return false;
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

        var document = createDocument();
        var context = document.CreateStepExecutionContext(step.Id);
        publishedOutputs.Remove(step.OutputEntityId);
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
                    document,
                    step.Id,
                    publishedEdge,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            if (!ReferenceEquals(step, getSelectedPipelineStep())
                || !string.Equals(context, createDocument().CreateStepExecutionContext(step.Id), StringComparison.Ordinal))
            {
                isPreviewStale = previewOutput is not null;
                step.State = "Preview stale";
                SetSummary("Input or conditions changed. Run again for the current result.");
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
            previewStep = step;
            previewContext = context;
            selectedDiagnostic = evaluation.Output.PointDiagnostics.FirstOrDefault();
            RebuildResidualPlot(evaluation.Output);
            step.State = "Preview ready";
            var diagnostics = evaluation.Output.Diagnostics;
            SetSummary($"Preview ready | inliers {diagnostics.InlierCount:N0}/{diagnostics.InputPointCount:N0} ({diagnostics.InlierRatio:P1}) | residual RMS {diagnostics.ResidualRms:0.##} source-coordinate | no OK/NG");
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
        if (IsDisposed || getSelectedPipelineStep() is not { } step || !HasCurrentPreview
            || !ReferenceEquals(step, previewStep))
        {
            return;
        }

        isPreviewPublished = true;
        publishedOutputs[previewOutput!.OutputEntityId] = (previewOutput, step.Id, previewContext);
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
        if (IsDisposed || isPreviewRunning || previewOutput is null && publishedOutputs.Count == 0)
        {
            return;
        }

        var document = createDocument();
        var changed = false;
        foreach (var (id, published) in publishedOutputs.ToArray())
        {
            if (string.Equals(published.Context, document.CreateStepExecutionContext(published.StepId), StringComparison.Ordinal)) continue;
            publishedOutputs.Remove(id);
            if (findStepByOutputEntityId(id) is { } step) step.State = "Preview stale";
            changed = true;
        }
        if (previewOutput is not null && !isPreviewStale && previewStep is not null
            && !string.Equals(previewContext, document.CreateStepExecutionContext(previewStep.Id), StringComparison.Ordinal))
        {
            isPreviewStale = true;
            isPreviewPublished = false;
            previewStep.State = "Preview stale";
            ResidualPlotPoints.Clear();
            clearDisplay();
            changed = true;
        }
        if (!changed || IsDisposed) return;
        markLineIntersectionStale();
        if (isPreviewStale) SetSummary("Input, Line Fit parameter, route, or output changed. Preview again before Publish.");
        else onExecutionStateChanged();
    }

    public void ForgetOutput(string outputEntityId)
    {
        if (IsDisposed) return;
        publishedOutputs.Remove(outputEntityId);
        if (!string.Equals(previewOutput?.OutputEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)) return;
        previewOutput = null;
        previewStep = null;
        previewContext = string.Empty;
        selectedDiagnostic = null;
        ResidualPlotPoints.Clear();
        isPreviewStale = false;
        isPreviewPublished = false;
        clearDisplay();
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
        previewStep = null;
        previewContext = string.Empty;
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

        MarkStaleIfNeeded();

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
        previewStep = null;
        previewContext = string.Empty;
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
