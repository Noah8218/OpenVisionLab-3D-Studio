using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Workbench Preview/Publish state for the physical-edge Gap/Flush
/// route. The calculation itself remains in the Tools adapter; this owner
/// only snapshots published inputs, gates execution, and retains the typed
/// result until the recipe or an upstream artifact changes.
/// </summary>
internal sealed class ToolWorkbenchPhysicalEdgeGapFlushExecutionOwner : IDisposable
{
    private const int RequiredInputCount = 4;
    private readonly Func<bool> isSelectedStep;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<string, C3DLineFeature?> getPublishedLine;
    private readonly Func<string, C3DHeightDifferenceEdgePointSet?> getPublishedEdge;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private readonly Dictionary<string, C3DPhysicalEdgeGapFlushResult> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private C3DPhysicalEdgeGapFlushResult? previewOutput;
    private string? previewExecutionFingerprint;
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Route two Published LineFeature inputs followed by their matching Published EdgePointSet inputs, then Preview explicitly.";
    private int disposalState;

    public ToolWorkbenchPhysicalEdgeGapFlushExecutionOwner(
        Func<bool> isSelectedStep,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<string, C3DLineFeature?> getPublishedLine,
        Func<string, C3DHeightDifferenceEdgePointSet?> getPublishedEdge,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action onExecutionStateChanged)
    {
        this.isSelectedStep = isSelectedStep ?? throw new ArgumentNullException(nameof(isSelectedStep));
        this.getSelectedPipelineStep = getSelectedPipelineStep ?? throw new ArgumentNullException(nameof(getSelectedPipelineStep));
        this.isSourceReadyForRecipe = isSourceReadyForRecipe ?? throw new ArgumentNullException(nameof(isSourceReadyForRecipe));
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges ?? throw new ArgumentNullException(nameof(hasPendingStepParameterChanges));
        this.getPublishedLine = getPublishedLine ?? throw new ArgumentNullException(nameof(getPublishedLine));
        this.getPublishedEdge = getPublishedEdge ?? throw new ArgumentNullException(nameof(getPublishedEdge));
        this.createDocument = createDocument ?? throw new ArgumentNullException(nameof(createDocument));
        this.appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));
        this.onExecutionStateChanged = onExecutionStateChanged ?? throw new ArgumentNullException(nameof(onExecutionStateChanged));
    }

    public bool IsSelectedStep => !IsDisposed && isSelectedStep();
    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed
        && !isPreviewRunning
        && previewOutput is not null
        && !isPreviewStale
        && previewExecutionFingerprint is not null
        && TryGetCurrentInputs(out var step, out var firstLine, out var secondLine, out var firstEdge, out var secondEdge, out _)
        && string.Equals(
            previewExecutionFingerprint,
            CreateExecutionFingerprint(step!, firstLine!, secondLine!, firstEdge!, secondEdge!),
            StringComparison.Ordinal);
    public bool IsPreviewStale => !IsDisposed && isPreviewStale;
    public bool IsPreviewPublished => !IsDisposed && isPreviewPublished;
    public C3DPhysicalEdgeGapFlushResult? CurrentOutput => IsDisposed ? null : previewOutput;
    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public string ExecutionSummary => IsDisposed
        ? "Physical Edge Gap / Flush execution owner has been disposed."
        : executionSummary;

    public string OutputHashSummary => IsDisposed || previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public string UpstreamSummary => IsDisposed
        ? "Physical Edge Gap / Flush execution owner has been disposed."
        : DescribeInputs();

    public string EvidenceSummary => IsDisposed
        ? "No physical-edge measurement evidence after the execution owner has been disposed."
        : previewOutput?.EvidenceSummary
        ?? "No physical-edge measurement evidence until Preview completes.";

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview())
        {
            return false;
        }

        if (!TryGetCurrentInputs(
                out var step,
                out var firstLine,
                out var secondLine,
                out var firstEdge,
                out var secondEdge,
                out var inputMessage))
        {
            SetSummary(inputMessage ?? "Four Published physical-edge inputs are required before Preview.");
            if (step is not null)
            {
                step.State = "Taught incomplete";
            }

            return false;
        }

        var currentCancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref previewCancellation, currentCancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            CancelAndDispose(currentCancellation);
            return false;
        }

        var cancellationToken = currentCancellation.Token;
        var stepSnapshot = step!;
        var firstLineSnapshot = firstLine!;
        var secondLineSnapshot = secondLine!;
        var firstEdgeSnapshot = firstEdge!;
        var secondEdgeSnapshot = secondEdge!;
        var documentSnapshot = createDocument();
        var executionFingerprint = CreateExecutionFingerprint(
            stepSnapshot,
            firstLineSnapshot,
            secondLineSnapshot,
            firstEdgeSnapshot,
            secondEdgeSnapshot);

        isPreviewRunning = true;
        isPreviewStale = false;
        isPreviewPublished = false;
        previewOutput = null;
        previewExecutionFingerprint = null;
        publishedOutputs.Clear();
        stepSnapshot.State = "Preview running";
        SetSummary("Physical Edge Gap / Flush Preview is evaluating the authored line/edge evidence.");
        appendLog("Preview", $"Physical Edge Gap / Flush Preview started: {stepSnapshot.Id}.");

        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipePhysicalEdgeGapFlushExecution.Execute(
                    documentSnapshot,
                    stepSnapshot.Id,
                    firstLineSnapshot,
                    secondLineSnapshot,
                    firstEdgeSnapshot,
                    secondEdgeSnapshot,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            if (evaluation.Output is null || evaluation.Result.Status == ResultStatus.Error)
            {
                isPreviewStale = false;
                isPreviewPublished = false;
                previewOutput = null;
                previewExecutionFingerprint = null;
                stepSnapshot.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Physical Edge Gap / Flush Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            previewExecutionFingerprint = executionFingerprint;
            isPreviewStale = false;
            isPreviewPublished = false;
            stepSnapshot.State = "Preview ready";
            SetSummary($"Preview ready | {evaluation.Result.Status} | {evaluation.Output.EvidenceSummary}");
            appendLog("Preview", $"Physical Edge Gap / Flush Preview ready: {evaluation.Output.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            previewOutput = null;
            previewExecutionFingerprint = null;
            isPreviewStale = false;
            isPreviewPublished = false;
            stepSnapshot.State = "Ready";
            SetSummary("Preview canceled. Published inputs and the authored recipe were not changed.");
            appendLog("Preview", "Physical Edge Gap / Flush Preview canceled.");
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            previewOutput = null;
            previewExecutionFingerprint = null;
            isPreviewStale = false;
            isPreviewPublished = false;
            stepSnapshot.State = "Error";
            SetSummary(exception.Message);
            appendLog("Error", $"Physical Edge Gap / Flush Preview failed: {exception.Message}");
            return false;
        }
        finally
        {
            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref previewCancellation, null, currentCancellation),
                    currentCancellation))
            {
                currentCancellation.Dispose();
                if (!IsDisposed)
                {
                    isPreviewRunning = false;
                    onExecutionStateChanged();
                }
            }
        }
    }

    public bool CanPreview()
    {
        if (IsDisposed
            || !IsSelectedStep
            || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges()
            || isPreviewRunning
            || !TryGetCurrentInputs(
                out var step,
                out var firstLine,
                out var secondLine,
                out var firstEdge,
                out var secondEdge,
                out _))
        {
            return false;
        }

        return ToolRecipePhysicalEdgeGapFlushExecution.TryPrepare(
            createDocument(),
            step!.Id,
            firstLine!,
            secondLine!,
            firstEdge!,
            secondEdge!,
            out _,
            out _);
    }

    public bool CanPublish() => !IsDisposed && HasCurrentPreview && !isPreviewPublished;

    public void Publish()
    {
        if (!CanPublish() || previewOutput is null || getSelectedPipelineStep() is not { } step)
        {
            return;
        }

        isPreviewPublished = true;
        publishedOutputs[previewOutput.OutputEntityId] = previewOutput;
        step.State = "Published";
        SetSummary($"Published exact Preview as {previewOutput.OutputEntityId} | {previewOutput.EvidenceSummary}");
        appendLog("Publish", $"Physical Edge Gap / Flush output published without re-running: {previewOutput.OutputEntityId}.");
    }

    public bool TryGetPublishedOutput(string outputEntityId, out C3DPhysicalEdgeGapFlushResult? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        return publishedOutputs.TryGetValue(outputEntityId, out output);
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
            // A concurrent completion or disposal already released the token source.
        }
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
            var selectedIsCurrent = string.Equals(
                selected?.ToolId,
                "physical-edge-gap-flush",
                StringComparison.Ordinal)
                && string.Equals(
                    selected?.OutputEntityId,
                    previewOutput.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase);
            var selectedParameter = selectedIsCurrent
                && sender is ToolWorkbenchParameterItem parameter
                && (selected?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsCurrent || (!ReferenceEquals(sender, selected) && !selectedParameter))
            {
                return;
            }
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        publishedOutputs.Clear();
        if (getSelectedPipelineStep() is { } step)
        {
            step.State = "Preview stale";
        }

        SetSummary("Physical-edge input, profile, route, or output changed. Preview again before Publish.");
    }

    public void Clear(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(currentCancellation);
        previewOutput = null;
        previewExecutionFingerprint = null;
        publishedOutputs.Clear();
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (IsDisposed || !IsSelectedStep || isPreviewRunning)
        {
            return;
        }

        if (getSelectedPipelineStep() is not { } step)
        {
            return;
        }

        if (HasCurrentPreview)
        {
            step.State = isPreviewPublished ? "Published" : "Preview ready";
        }
        else
        {
            var adapterMessage = string.Empty;
            var inputsReady = TryGetCurrentInputs(
                out _,
                out var firstLine,
                out var secondLine,
                out var firstEdge,
                out var secondEdge,
                out var inputMessage);
            var adapterReady = inputsReady
                && ToolRecipePhysicalEdgeGapFlushExecution.TryPrepare(
                    createDocument(),
                    step.Id,
                    firstLine!,
                    secondLine!,
                    firstEdge!,
                    secondEdge!,
                    out _,
                    out adapterMessage);
            if (adapterReady)
            {
                step.State = "Ready";
                SetSummary("Ready for explicit Preview. Published line/edge evidence will not execute implicitly.");
            }
            else
            {
                step.State = "Taught incomplete";
                SetSummary(inputMessage ?? adapterMessage ?? "Physical-edge inputs are not ready for Preview.");
            }
        }

        onExecutionStateChanged();
    }

    private bool TryGetCurrentInputs(
        out ToolWorkbenchPipelineStepItem? step,
        out C3DLineFeature? firstLine,
        out C3DLineFeature? secondLine,
        out C3DHeightDifferenceEdgePointSet? firstEdge,
        out C3DHeightDifferenceEdgePointSet? secondEdge,
        out string? message)
    {
        step = getSelectedPipelineStep();
        firstLine = null;
        secondLine = null;
        firstEdge = null;
        secondEdge = null;
        message = null;
        if (step is null)
        {
            message = "No physical-edge step is selected.";
            return false;
        }

        if (step.InputEntityIds.Count != RequiredInputCount)
        {
            message = $"Physical Edge Gap / Flush requires {RequiredInputCount} inputs in order: FirstLine; SecondLine; FirstEdge; SecondEdge.";
            return false;
        }

        firstLine = getPublishedLine(step.InputEntityIds[0]);
        secondLine = getPublishedLine(step.InputEntityIds[1]);
        firstEdge = getPublishedEdge(step.InputEntityIds[2]);
        secondEdge = getPublishedEdge(step.InputEntityIds[3]);
        if (firstLine is null || secondLine is null || firstEdge is null || secondEdge is null)
        {
            message = "Every physical-edge input must resolve to a Published LineFeature or EdgePointSet.";
            return false;
        }

        if (string.Equals(firstLine.OutputEntityId, secondLine.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstEdge.OutputEntityId, secondEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            message = "First and Second physical-edge evidence must be distinct.";
            return false;
        }

        if (!string.Equals(firstLine.InputEdgePointSetEntityId, firstEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(secondLine.InputEdgePointSetEntityId, secondEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            message = "Each Published LineFeature must match its authored EdgePointSet input.";
            return false;
        }

        return true;
    }

    private string DescribeInputs()
    {
        if (!TryGetCurrentInputs(
                out var step,
                out var firstLine,
                out var secondLine,
                out var firstEdge,
                out var secondEdge,
                out var message))
        {
            return message ?? "Physical-edge inputs are unavailable.";
        }

        return $"Published inputs ready: {step!.InputEntityIds[0]} ({firstLine!.ContentSha256[..12]}); {step.InputEntityIds[1]} ({secondLine!.ContentSha256[..12]}); {step.InputEntityIds[2]} ({firstEdge!.ContentSha256[..12]}); {step.InputEntityIds[3]} ({secondEdge!.ContentSha256[..12]})";
    }

    private string CreateExecutionFingerprint(
        ToolWorkbenchPipelineStepItem step,
        C3DLineFeature firstLine,
        C3DLineFeature secondLine,
        C3DHeightDifferenceEdgePointSet firstEdge,
        C3DHeightDifferenceEdgePointSet secondEdge)
    {
        var document = createDocument();
        var values = string.Join(
            "|",
            step.Id,
            step.OutputEntityId,
            string.Join(";", step.InputEntityIds),
            string.Join(";", step.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}")),
            document.Source.Id,
            document.Source.ContentSha256,
            document.Source.FrameId,
            document.Source.Unit,
            firstLine.ContentSha256,
            secondLine.ContentSha256,
            firstEdge.ContentSha256,
            secondEdge.ContentSha256);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(values)));
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(Volatile.Read(ref previewCancellation), cancellation);

    private void SetSummary(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        executionSummary = summary;
        onExecutionStateChanged();
    }

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
        }

        cancellation.Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(currentCancellation);
        previewOutput = null;
        previewExecutionFingerprint = null;
        publishedOutputs.Clear();
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
    }
}
