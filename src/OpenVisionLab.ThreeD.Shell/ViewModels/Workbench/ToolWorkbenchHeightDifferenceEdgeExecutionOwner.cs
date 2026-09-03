using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchHeightDifferenceEdgeExecutionOwner : IDisposable
{
    private static readonly string[] AxisOptions =
        ["Select comparison axis", "AcrossColumns (+X)", "AcrossRows (+Z)"];
    private static readonly string[] PolarityOptions =
        ["Select polarity", "Rising", "Falling", "Absolute"];

    private readonly Func<bool> isSelectedStepHeightDifferenceEdge;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<bool> isFilterPreviewRunning;
    private readonly Func<C3DHeightFieldSnapshot?> getFilterPreviewOutput;
    private readonly Func<string?> getFilterPreviewPath;
    private readonly Func<bool> isFilterPreviewStale;
    private readonly Func<bool> isFilterPreviewPublished;
    private readonly Func<ToolRecipeSelection?> getSelectedTeachingSelection;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs> requestDisplay;
    private readonly Action refreshLineFit;
    private readonly Action markLineFitStale;
    private readonly Action clearLineFit;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DHeightDifferenceEdgePointSet? previewOutput;
    private readonly Dictionary<string, C3DHeightDifferenceEdgePointSet> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Teach a search band and explicit MinimumDelta, then publish Filter before Preview.";
    private int disposalState;

    public ToolWorkbenchHeightDifferenceEdgeExecutionOwner(
        Func<bool> isSelectedStepHeightDifferenceEdge,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<bool> isFilterPreviewRunning,
        Func<C3DHeightFieldSnapshot?> getFilterPreviewOutput,
        Func<string?> getFilterPreviewPath,
        Func<bool> isFilterPreviewStale,
        Func<bool> isFilterPreviewPublished,
        Func<ToolRecipeSelection?> getSelectedTeachingSelection,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action<ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs> requestDisplay,
        Action refreshLineFit,
        Action markLineFitStale,
        Action clearLineFit,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepHeightDifferenceEdge = isSelectedStepHeightDifferenceEdge;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.isFilterPreviewRunning = isFilterPreviewRunning;
        this.getFilterPreviewOutput = getFilterPreviewOutput;
        this.getFilterPreviewPath = getFilterPreviewPath;
        this.isFilterPreviewStale = isFilterPreviewStale;
        this.isFilterPreviewPublished = isFilterPreviewPublished;
        this.getSelectedTeachingSelection = getSelectedTeachingSelection;
        this.findStepByOutputEntityId = findStepByOutputEntityId;
        this.createDocument = createDocument;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.refreshLineFit = refreshLineFit;
        this.markLineFitStale = markLineFitStale;
        this.clearLineFit = clearLineFit;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStepHeightDifferenceEdge => !IsDisposed && isSelectedStepHeightDifferenceEdge();
    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed && previewOutput is not null && !isPreviewStale;
    public bool IsPreviewStale => !IsDisposed && isPreviewStale;
    public bool IsPreviewPublished => !IsDisposed && isPreviewPublished;
    public C3DHeightDifferenceEdgePointSet? CurrentOutput => IsDisposed ? null : previewOutput;
    public IReadOnlyList<string> ComparisonAxisOptions => AxisOptions;
    public IReadOnlyList<string> EdgePolarityOptions => PolarityOptions;
    public string ExecutionSummary => IsDisposed
        ? "Height Difference Edge execution owner has been disposed."
        : executionSummary;

    public string SelectedComparisonAxis
    {
        get => GetParameter("ComparisonAxis") switch
        {
            "AcrossColumns" => AxisOptions[1],
            "AcrossRows" => AxisOptions[2],
            _ => AxisOptions[0]
        };
        set => SetParameter("ComparisonAxis", value switch
        {
            "AcrossColumns (+X)" => "AcrossColumns",
            "AcrossRows (+Z)" => "AcrossRows",
            _ => "Set explicitly"
        });
    }

    public string SelectedPolarity
    {
        get => GetParameter("Polarity") switch
        {
            "Rising" => "Rising",
            "Falling" => "Falling",
            "Absolute" => "Absolute",
            _ => PolarityOptions[0]
        };
        set => SetParameter(
            "Polarity",
            value == PolarityOptions[0] ? "Set explicitly" : value);
    }

    public string MinimumDelta
    {
        get => GetParameter("MinimumDelta") ?? string.Empty;
        set => SetParameter("MinimumDelta", value ?? string.Empty);
    }

    public string ExpectedOrientation => GetParameter("ComparisonAxis") == "AcrossRows"
        ? "Expected edge along columns (X)"
        : "Expected edge along rows (Z)";

    public string UpstreamSummary
    {
        get
        {
            if (IsDisposed)
            {
                return "Height Difference Edge execution owner has been disposed.";
            }

            var step = getSelectedPipelineStep();
            if (step is null || step.InputEntityIds.Count == 0)
            {
                return "Missing routed height field";
            }

            var filterOutput = getFilterPreviewOutput();
            if (filterOutput is null || isFilterPreviewStale())
            {
                return $"{step.InputEntityIds[0]} | no current Filter Preview";
            }

            return string.Equals(
                step.InputEntityIds[0],
                filterOutput.EntityId,
                StringComparison.OrdinalIgnoreCase)
                ? $"{step.InputEntityIds[0]} | {(isFilterPreviewPublished() ? "Published | current" : "Preview only | publish required")}"
                : $"{step.InputEntityIds[0]} | does not match current Filter output";
        }
    }

    public string BandSummary
    {
        get
        {
            if (IsDisposed)
            {
                return "Height Difference Edge execution owner has been disposed.";
            }

            var rectangle = getSelectedTeachingSelection()?.GridRectangle;
            return rectangle is null
                ? "No recipe-owned GridRectangle routed"
                : $"Rows {rectangle.Row}..{rectangle.Row + rectangle.RowCount - 1} | columns {rectangle.Column}..{rectangle.Column + rectangle.ColumnCount - 1}";
        }
    }

    public string OutputHashSummary => IsDisposed || previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DHeightDifferenceEdgePointSet? output)
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
        SetSummary("Height Difference Edge Preview is running from the exact Published Filter output.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"Height Difference Edge Preview started: {step.Id}.");

        try
        {
            var filterOutput = getFilterPreviewOutput();
            if (filterOutput is null)
            {
                step.State = "Waiting for upstream";
                SetSummary("The routed Filter output is not current and Published.");
                return false;
            }

            var evaluation = await Task.Run(
                () => ToolRecipeHeightDifferenceEdgeExecution.Execute(
                    createDocument(),
                    step.Id,
                    filterOutput,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                previewOutput = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (!IsCurrentPreview(currentCancellation))
                {
                    return false;
                }

                appendLog("Error", $"Height Difference Edge Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            step.State = "Preview ready";
            var diagnostics = evaluation.Output.Diagnostics;
            SetSummary($"Preview ready | points {diagnostics.AcceptedScanlineCount:N0}/{diagnostics.ScanlineCount:N0} | eligible pairs {diagnostics.EligiblePairCount:N0} | missing skips {diagnostics.SkippedMissingPairCount:N0} | no OK/NG");
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            appendLog("Preview", $"Height Difference Edge Preview ready: {evaluation.Output.ContentSha256}.");
            if (IsCurrentPreview(currentCancellation))
            {
                RequestDisplay(evaluation.Output, false);
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
            SetSummary("Preview canceled. Published Filter output and authored recipe were not changed.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", "Height Difference Edge Preview canceled.");
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
        if (IsDisposed || !IsSelectedStepHeightDifferenceEdge || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges() || isPreviewRunning
            || isFilterPreviewRunning() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var filterOutput = getFilterPreviewOutput();
        if (filterOutput is null
            || isFilterPreviewStale()
            || !isFilterPreviewPublished())
        {
            return false;
        }

        return ToolRecipeHeightDifferenceEdgeExecution.TryPrepare(
            createDocument(), step.Id, filterOutput, out _, out _);
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

        refreshLineFit();
        if (!IsDisposed)
        {
            appendLog("Publish", $"Height Difference Edge output published without re-running: {step.OutputEntityId}.");
        }

        if (!IsDisposed)
        {
            RequestDisplay(previewOutput, true);
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

    public void SetParameter(string name, string value)
    {
        if (IsDisposed || !IsSelectedStepHeightDifferenceEdge)
        {
            return;
        }

        var parameter = getSelectedPipelineStep()!.Parameters.SingleOrDefault(item => item.Name == name);
        if (parameter is null || parameter.Value == value)
        {
            return;
        }

        parameter.Value = value;
        onExecutionStateChanged();
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
            var selectedIsEdge = string.Equals(
                selected?.ToolId,
                "height-difference-edge",
                StringComparison.Ordinal);
            var selectedIsCurrentEdge = selectedIsEdge
                && string.Equals(
                    selected?.OutputEntityId,
                    previewOutput.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase);
            var isSelectedEdgeParameter = selectedIsEdge
                && sender is ToolWorkbenchParameterItem parameter
                && (selected?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsCurrentEdge
                || (!ReferenceEquals(sender, selected) && !isSelectedEdgeParameter))
            {
                return;
            }
        }

        MarkStale("Input, search band, parameter, route, or output changed. Preview again before Publish.");
    }

    public void MarkStale(string summary)
    {
        if (IsDisposed || previewOutput is null)
        {
            return;
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        publishedOutputs.Clear();
        var step = findStepByOutputEntityId(previewOutput.OutputEntityId);
        if (step is not null)
        {
            step.State = "Preview stale";
        }

        if (!IsDisposed)
        {
            markLineFitStale();
        }

        SetSummary(summary);
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
        isPreviewStale = false;
        isPreviewPublished = false;
        publishedOutputs.Clear();
        clearLineFit();
        if (IsDisposed)
        {
            return;
        }

        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedPipelineStep() is { } step && IsSelectedStepHeightDifferenceEdge
            && (previewOutput is null
                || !string.Equals(
                    previewOutput.OutputEntityId,
                    step.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase)
                || isPreviewStale)
            && !isPreviewRunning)
        {
            var filterOutput = getFilterPreviewOutput();
            if (filterOutput is null || isFilterPreviewStale() || !isFilterPreviewPublished())
            {
                step.State = "Waiting for upstream";
            }
            else if (ToolRecipeHeightDifferenceEdgeExecution.TryPrepare(
                createDocument(), step.Id, filterOutput, out _, out var message))
            {
                step.State = "Ready";
                executionSummary = "Ready for explicit Preview. Filter will not run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                executionSummary = message;
            }
        }

        onExecutionStateChanged();
        refreshLineFit();
    }

    private string? GetParameter(string name) => IsSelectedStepHeightDifferenceEdge
        ? getSelectedPipelineStep()!.Parameters.SingleOrDefault(parameter => parameter.Name == name)?.Value
        : null;

    private void RequestDisplay(C3DHeightDifferenceEdgePointSet output, bool isPublished)
    {
        if (IsDisposed)
        {
            return;
        }

        var path = getFilterPreviewPath();
        if (path is null || !File.Exists(path))
        {
            return;
        }

        if (!IsDisposed)
        {
            requestDisplay(new ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs(
                path,
                output,
                isPublished));
        }
    }

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isPreviewRunning = value;
        onExecutionStateChanged();
    }

    private void SetSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        executionSummary = value;
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
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
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
}
