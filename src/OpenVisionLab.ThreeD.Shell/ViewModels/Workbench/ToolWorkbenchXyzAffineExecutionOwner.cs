using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the explicit A1 Solve -> A2 Apply execution lifecycle. The Workbench
/// facade supplies recipe/source identity, upstream publication, and downstream
/// Re-grid callbacks without sharing this owner's private execution state.
/// </summary>
internal sealed class ToolWorkbenchXyzAffineExecutionOwner : IDisposable
{
    private readonly Func<bool> isSelectedSolve;
    private readonly Func<bool> isSelectedApply;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedStep;
    private readonly Func<bool> isSourceReady;
    private readonly Func<bool> hasPendingParameterChanges;
    private readonly Func<string> getSourceId;
    private readonly Func<ToolRecipeSelectionSourceBinding?> getSourceBinding;
    private readonly Func<string, C3DLandmarkCorrespondenceSet?> getPublishedCorrespondence;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> getStepByOutputId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action<string> clearRegridPreview;
    private readonly Action refreshRegridState;
    private readonly Action onStateChanged;

    private CancellationTokenSource? solveCancellation;
    private C3DAffineTransform3D? solveOutput;
    private readonly Dictionary<string, C3DAffineTransform3D> publishedSolveOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private bool isSolveRunning;
    private bool isSolveStale;
    private bool isSolvePublished;
    private string solveSummary =
        "Route one current Published CorrespondenceSet, teach the numerical review limits, then Preview explicitly.";

    private CancellationTokenSource? applyCancellation;
    private C3DTransformedPointCloud? applyOutput;
    private readonly Dictionary<string, C3DTransformedPointCloud> publishedApplyOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private bool isApplyRunning;
    private bool isApplyStale;
    private bool isApplyPublished;
    private string applySummary =
        "Route the verified raw C3D first and the current Published AffineTransform3D second, then Preview explicitly.";
    private int disposalState;

    public ToolWorkbenchXyzAffineExecutionOwner(
        Func<bool> isSelectedSolve,
        Func<bool> isSelectedApply,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedStep,
        Func<bool> isSourceReady,
        Func<bool> hasPendingParameterChanges,
        Func<string> getSourceId,
        Func<ToolRecipeSelectionSourceBinding?> getSourceBinding,
        Func<string, C3DLandmarkCorrespondenceSet?> getPublishedCorrespondence,
        Func<string, ToolWorkbenchPipelineStepItem?> getStepByOutputId,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action<string> clearRegridPreview,
        Action refreshRegridState,
        Action onStateChanged)
    {
        this.isSelectedSolve = isSelectedSolve;
        this.isSelectedApply = isSelectedApply;
        this.getSelectedStep = getSelectedStep;
        this.isSourceReady = isSourceReady;
        this.hasPendingParameterChanges = hasPendingParameterChanges;
        this.getSourceId = getSourceId;
        this.getSourceBinding = getSourceBinding;
        this.getPublishedCorrespondence = getPublishedCorrespondence;
        this.getStepByOutputId = getStepByOutputId;
        this.createDocument = createDocument;
        this.appendLog = appendLog;
        this.clearRegridPreview = clearRegridPreview;
        this.refreshRegridState = refreshRegridState;
        this.onStateChanged = onStateChanged;
    }

    public bool IsSelectedSolve => !IsDisposed && isSelectedSolve();
    public bool IsSolveRunning => !IsDisposed && isSolveRunning;
    public bool HasCurrentSolvePreview => !IsDisposed && solveOutput is not null && !isSolveStale;
    public bool IsSolveStale => !IsDisposed && isSolveStale;
    public bool IsSolvePublished => !IsDisposed && isSolvePublished;
    public C3DAffineTransform3D? CurrentSolveOutput => IsDisposed ? null : solveOutput;
    public string SolveSummary => solveSummary;
    public string SolveOutputHashSummary => solveOutput is null
        ? "No affine output hash until Preview completes."
        : $"AffineTransform3D SHA-256 {solveOutput.ContentSha256}";
    public string SolveUpstreamSummary => TryGetSolveInput(out var correspondence)
        ? $"Published CorrespondenceSet | {correspondence.Pairs.Count}/4 pairs | SHA-256 {correspondence.ContentSha256[..12]}"
        : "One current Published CorrespondenceSet must be routed from Landmark Correspondence.";
    public string SolveEvidenceSummary => solveOutput is null
        ? "No matrix evidence until Preview completes."
        : $"condition {solveOutput.ConditionEstimate:G6} / {solveOutput.MaximumConditionEstimate:G6} | residual RMS {solveOutput.ArithmeticRmsResidual:G6}, max {solveOutput.ArithmeticMaximumResidual:G6} {solveOutput.ReferenceUnit} | no C3D point moved";
    public string SolveMatrixSummary => solveOutput is null
        ? "No source-to-reference matrix until Preview completes."
        : string.Join(Environment.NewLine,
        [
            $"Xref = {solveOutput.Matrix.M11:G8} X + {solveOutput.Matrix.M12:G8} Y + {solveOutput.Matrix.M13:G8} Z + {solveOutput.Matrix.M14:G8}",
            $"Yref = {solveOutput.Matrix.M21:G8} X + {solveOutput.Matrix.M22:G8} Y + {solveOutput.Matrix.M23:G8} Z + {solveOutput.Matrix.M24:G8}",
            $"Zref = {solveOutput.Matrix.M31:G8} X + {solveOutput.Matrix.M32:G8} Y + {solveOutput.Matrix.M33:G8} Z + {solveOutput.Matrix.M34:G8}"
        ]);

    public bool IsSelectedApply => !IsDisposed && isSelectedApply();
    public bool IsApplyRunning => !IsDisposed && isApplyRunning;
    public bool HasCurrentApplyPreview => !IsDisposed && applyOutput is not null && !isApplyStale;
    public bool IsApplyStale => !IsDisposed && isApplyStale;
    public bool IsApplyPublished => !IsDisposed && isApplyPublished;
    public C3DTransformedPointCloud? CurrentApplyOutput => IsDisposed ? null : applyOutput;
    public string ApplySummary => applySummary;
    public string ApplyOutputHashSummary => applyOutput is null
        ? "No TransformedPointCloud hash until Preview completes."
        : $"TransformedPointCloud SHA-256 {applyOutput.ContentSha256}";
    public string ApplyUpstreamSummary => TryGetApplyInput(out var transform)
        ? $"Raw C3D + Published AffineTransform3D | matrix SHA-256 {transform.ContentSha256[..12]}"
        : "Route the raw recipe C3D first and one current Published AffineTransform3D second.";
    public string ApplyEvidenceSummary => applyOutput is null
        ? "No transformed point evidence until Preview completes."
        : $"finite {applyOutput.FinitePointCount:N0} | missing source cells {applyOutput.MissingPointCount:N0} | source-grid order retained | re-grid excluded";

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool TryGetPublishedSolveOutput(
        string outputEntityId,
        out C3DAffineTransform3D? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        return publishedSolveOutputs.TryGetValue(outputEntityId, out output);
    }

    public bool TryGetPublishedApplyOutput(
        string outputEntityId,
        out C3DTransformedPointCloud? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        return publishedApplyOutputs.TryGetValue(outputEntityId, out output);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentSolveCancellation = Interlocked.Exchange(
            ref solveCancellation,
            null);
        CancelAndDispose(currentSolveCancellation);
        var currentApplyCancellation = Interlocked.Exchange(
            ref applyCancellation,
            null);
        CancelAndDispose(currentApplyCancellation);
        solveOutput = null;
        publishedSolveOutputs.Clear();
        isSolveRunning = false;
        isSolveStale = false;
        isSolvePublished = false;
        applyOutput = null;
        publishedApplyOutputs.Clear();
        isApplyRunning = false;
        isApplyStale = false;
        isApplyPublished = false;
    }

    public async Task<bool> PreviewSolveAsync()
    {
        if (IsDisposed || !CanPreviewSolve() || getSelectedStep() is not { } step
            || !TryGetSolveInput(out var correspondence))
        {
            return false;
        }

        var currentCancellation = new CancellationTokenSource();
        var cancellationToken = currentCancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref solveCancellation,
            currentCancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref solveCancellation,
                    null,
                    currentCancellation),
                currentCancellation))
            {
                currentCancellation.Dispose();
            }

            return false;
        }

        SetSolveRunning(true);
        isSolveStale = false;
        isSolvePublished = false;
        step.State = "Preview running";
        SetSolveSummary("XYZ Affine Solve Preview computes one matrix from the exact current Published CorrespondenceSet. It does not move C3D points.");
        appendLog("Preview", $"XYZ Affine Solve Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeXYZAffineSolveExecution.Execute(
                    createDocument(),
                    step.Id,
                    correspondence,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentSolve(currentCancellation))
            {
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                solveOutput = null;
                step.State = "Error";
                SetSolveSummary(evaluation.Result.Message);
                appendLog("Error", $"XYZ Affine Solve Preview failed: {evaluation.Result.Message}");
                return false;
            }

            solveOutput = evaluation.Output;
            step.State = "Preview ready";
            SetSolveSummary($"Preview ready | {SolveEvidenceSummary}");
            appendLog("Preview", $"XYZ Affine Solve Preview ready: {evaluation.Output.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentSolve(currentCancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetSolveSummary("Preview canceled. Published CorrespondenceSet and authored recipe were not changed.");
            appendLog("Preview", "XYZ Affine Solve Preview canceled.");
            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref solveCancellation,
                    null,
                    currentCancellation),
                currentCancellation);
            if (ownsCancellation)
            {
                currentCancellation.Dispose();
                if (!IsDisposed)
                {
                    SetSolveRunning(false);
                }
            }
        }
    }

    public bool CanPreviewSolve()
    {
        if (IsDisposed || !IsSelectedSolve || !isSourceReady() || hasPendingParameterChanges()
            || isSolveRunning || getSelectedStep() is not { } step
            || !TryGetSolveInput(out var correspondence))
        {
            return false;
        }

        return ToolRecipeXYZAffineSolveExecution.TryPrepare(
            createDocument(), step.Id, correspondence, out _, out _);
    }

    public void PublishSolve()
    {
        if (IsDisposed || getSelectedStep() is not { } step || !HasCurrentSolvePreview)
        {
            return;
        }

        isSolvePublished = true;
        publishedSolveOutputs[solveOutput!.OutputEntityId] = solveOutput;
        step.State = "Published";
        SetSolveSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {solveOutput.ContentSha256} | solve evidence only; no C3D point was moved.");
        appendLog("Publish", $"XYZ Affine Solve output published without re-running: {step.OutputEntityId}.");
        RefreshApplyState();
    }

    public void CancelSolve()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref solveCancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal or replacement already released the token source.
        }
    }

    public void MarkSolveStaleIfNeeded(object? sender = null)
    {
        if (IsDisposed || solveOutput is null || isSolveRunning)
        {
            return;
        }

        if (sender is not null)
        {
            var selected = getSelectedStep();
            var current = IsSelectedSolve
                && string.Equals(selected?.OutputEntityId, solveOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase);
            var parameterChanged = current
                && sender is ToolWorkbenchParameterItem parameter
                && (selected?.Parameters.Contains(parameter) ?? false);
            if (!current || (!ReferenceEquals(sender, selected) && !parameterChanged))
            {
                return;
            }
        }

        isSolveStale = true;
        isSolvePublished = false;
        publishedSolveOutputs.Clear();
        var step = getStepByOutputId(solveOutput.OutputEntityId);
        if (step is not null)
        {
            step.State = "Preview stale";
        }

        ClearApply("Published AffineTransform3D changed. Apply XYZ Affine Preview was cleared without execution.");
        SetSolveSummary("Correspondence identity, route, or affine parameter changed. Preview again before Publish.");
    }

    public void ClearSolve(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref solveCancellation,
            null);
        CancelAndDispose(currentCancellation);
        solveOutput = null;
        publishedSolveOutputs.Clear();
        SetSolveRunning(false);
        isSolveStale = false;
        isSolvePublished = false;
        ClearApply("Published AffineTransform3D was cleared. Apply XYZ Affine Preview was cleared without execution.");
        SetSolveSummary(summary);
    }

    public void RefreshSolveState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedStep() is { } step && IsSelectedSolve
            && (solveOutput is null
                || !string.Equals(solveOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || isSolveStale)
            && !isSolveRunning)
        {
            if (!TryGetSolveInput(out var correspondence))
            {
                step.State = "Waiting for upstream";
                solveSummary = "Route one current Published CorrespondenceSet. Upstream tools do not run implicitly.";
            }
            else if (ToolRecipeXYZAffineSolveExecution.TryPrepare(
                createDocument(), step.Id, correspondence, out _, out var message))
            {
                step.State = "Ready";
                solveSummary = "Ready for explicit Preview. A1 solves a matrix only; Apply is a separate future tool.";
            }
            else
            {
                step.State = "Taught incomplete";
                solveSummary = message;
            }
        }

        onStateChanged();
        RefreshApplyState();
    }

    public async Task<bool> PreviewApplyAsync()
    {
        if (IsDisposed || !CanPreviewApply() || getSelectedStep() is not { } step
            || !TryGetApplyInput(out var transform))
        {
            return false;
        }

        var currentCancellation = new CancellationTokenSource();
        var cancellationToken = currentCancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref applyCancellation,
            currentCancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref applyCancellation,
                    null,
                    currentCancellation),
                currentCancellation))
            {
                currentCancellation.Dispose();
            }

            return false;
        }

        SetApplyRunning(true);
        isApplyStale = false;
        isApplyPublished = false;
        step.State = "Preview running";
        SetApplySummary("Apply XYZ Affine Preview verifies the raw C3D identity, then transforms each finite source-grid point once. It does not re-grid, interpolate, or measure.");
        appendLog("Preview", $"Apply XYZ Affine Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeXYZAffineApplyExecution.Execute(
                    createDocument(),
                    step.Id,
                    transform,
                    cancellationToken: cancellationToken),
                cancellationToken);
            if (!IsCurrentApply(currentCancellation))
            {
                return false;
            }

            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                applyOutput = null;
                step.State = "Error";
                SetApplySummary(evaluation.Result.Message);
                appendLog("Error", $"Apply XYZ Affine Preview failed: {evaluation.Result.Message}");
                return false;
            }

            applyOutput = evaluation.Output;
            step.State = "Preview ready";
            SetApplySummary($"Preview ready | {ApplyEvidenceSummary}");
            appendLog("Preview", $"Apply XYZ Affine Preview ready: {evaluation.Output.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentApply(currentCancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetApplySummary("Preview canceled. The raw C3D, Published AffineTransform3D, and authored recipe were not changed.");
            appendLog("Preview", "Apply XYZ Affine Preview canceled.");
            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref applyCancellation,
                    null,
                    currentCancellation),
                currentCancellation);
            if (ownsCancellation)
            {
                currentCancellation.Dispose();
                if (!IsDisposed)
                {
                    SetApplyRunning(false);
                }
            }
        }
    }

    public bool CanPreviewApply()
    {
        if (IsDisposed || !IsSelectedApply || !isSourceReady() || hasPendingParameterChanges()
            || isApplyRunning || !TryGetApplyInput(out var transform)
            || getSelectedStep() is not { } step)
        {
            return false;
        }

        return ToolRecipeXYZAffineApplyExecution.TryValidateRoute(
            createDocument(), step.Id, transform, out _, out _);
    }

    public void PublishApply()
    {
        if (IsDisposed || getSelectedStep() is not { } step || !HasCurrentApplyPreview)
        {
            return;
        }

        isApplyPublished = true;
        publishedApplyOutputs[applyOutput!.OutputEntityId] = applyOutput;
        step.State = "Published";
        SetApplySummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {applyOutput.ContentSha256} | raw source remains unchanged; A3 re-grid remains separate.");
        appendLog("Publish", $"Apply XYZ Affine output published without re-running: {step.OutputEntityId}.");
    }

    public void CancelApply()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref applyCancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal or replacement already released the token source.
        }
    }

    public void ClearApply(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref applyCancellation,
            null);
        CancelAndDispose(currentCancellation);
        applyOutput = null;
        publishedApplyOutputs.Clear();
        SetApplyRunning(false);
        isApplyStale = false;
        isApplyPublished = false;
        SetApplySummary(summary);
        clearRegridPreview("Published A2 TransformedPointCloud changed. Re-grid Height Map Preview was cleared without execution.");
    }

    public void RefreshApplyState()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedStep() is { } step && IsSelectedApply)
        {
            var hasCurrentRoute = TryGetApplyInput(out var transform);
            var outputMatches = applyOutput is not null
                && string.Equals(applyOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                && hasCurrentRoute
                && string.Equals(applyOutput.AffineTransformContentSha256, transform.ContentSha256, StringComparison.OrdinalIgnoreCase);
            if (applyOutput is not null && !outputMatches && !isApplyRunning)
            {
                isApplyStale = true;
                isApplyPublished = false;
                publishedApplyOutputs.Clear();
                step.State = "Preview stale";
                applySummary = "The route or current Published AffineTransform3D changed. Preview again before Publish.";
            }
            else if (applyOutput is null || isApplyStale)
            {
                if (!hasCurrentRoute)
                {
                    step.State = "Waiting for upstream";
                    applySummary = "Route the raw recipe C3D first and the current Published AffineTransform3D second. Upstream tools do not run implicitly.";
                }
                else if (ToolRecipeXYZAffineApplyExecution.TryValidateRoute(
                    createDocument(), step.Id, transform, out _, out var message))
                {
                    step.State = "Ready";
                    applySummary = "Ready for explicit Preview. A2 creates an ordered transformed point cloud only; A3 re-grid is separate.";
                }
                else
                {
                    step.State = "Taught incomplete";
                    applySummary = message;
                }
            }
        }

        onStateChanged();
    }

    public bool TryRegisterSyntheticPublishedApplyOutput(
        C3DTransformedPointCloud output,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (IsDisposed)
        {
            message = "XYZ Affine execution owner has been disposed.";
            return false;
        }

        if (getSelectedStep() is not { ToolId: "re-grid-height-map", InputEntityIds.Count: 1 } regridStep
            || !string.Equals(regridStep.InputEntityIds[0], output.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || getStepByOutputId(output.OutputEntityId) is not { ToolId: "xyz-affine-apply" }
            || !string.Equals(getSourceId(), output.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || getSourceBinding() is not { } sourceBinding
            || !string.Equals(sourceBinding.ContentSha256, output.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || sourceBinding.GridWidth != output.SourceGridWidth
            || sourceBinding.GridHeight != output.SourceGridHeight)
        {
            message = "Synthetic smoke A2 identity does not match the selected Re-grid route and loaded recipe source.";
            return false;
        }

        publishedApplyOutputs[output.OutputEntityId] = output;
        appendLog(
            "Smoke",
            $"Registered deterministic synthetic Published A2 prerequisite {output.OutputEntityId} ({output.ContentSha256}); normal Re-grid Preview/Publish remains explicit.");
        refreshRegridState();
        message = $"Synthetic Published A2 registered for smoke-only execution: {output.ContentSha256}";
        return true;
    }

    private bool TryGetSolveInput(out C3DLandmarkCorrespondenceSet correspondence)
    {
        correspondence = null!;
        if (IsDisposed
            || getSelectedStep() is not { InputEntityIds.Count: 1 } step
            || getPublishedCorrespondence(step.InputEntityIds[0]) is not { } published)
        {
            return false;
        }

        correspondence = published;
        return true;
    }

    private bool TryGetApplyInput(out C3DAffineTransform3D transform)
    {
        transform = null!;
        if (IsDisposed
            || getSelectedStep() is not { InputEntityIds.Count: 2 } step
            || !string.Equals(step.InputEntityIds[0], getSourceId(), StringComparison.OrdinalIgnoreCase)
            || !publishedSolveOutputs.TryGetValue(step.InputEntityIds[1], out var published))
        {
            return false;
        }

        transform = published;
        return true;
    }

    private void SetSolveRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isSolveRunning = value;
        onStateChanged();
    }

    private void SetSolveSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        solveSummary = value;
        onStateChanged();
    }

    private void SetApplyRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isApplyRunning = value;
        onStateChanged();
    }

    private void SetApplySummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        applySummary = value;
        onStateChanged();
    }

    private bool IsCurrentSolve(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref solveCancellation),
            cancellation);

    private bool IsCurrentApply(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref applyCancellation),
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
