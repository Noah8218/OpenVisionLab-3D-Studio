using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Bridges the independent Height Image ROI interaction surface to Workbench
/// policy callbacks. The coordinator owns only event subscriptions and their
/// lifetime; recipe state and command guards remain in the Workbench.
/// </summary>
internal sealed class ToolWorkbenchHeightImageRoiCoordinator : IDisposable
{
    private readonly HeightImageRoiWorkspaceViewModel workspace;
    private readonly Func<ToolRecipeGridRectangle, HeightImageRoiCandidateUpdateResult>
        tryUpdateCandidate;
    private readonly Action<string> selectPipelineStep;
    private readonly Action<string> appendWarning;
    private readonly Action applyTeachingSelectionCapture;
    private readonly Action cancelTeachingSelectionCapture;
    private readonly Action removeSelectedTeachingSelection;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchHeightImageRoiCoordinator(
        HeightImageRoiWorkspaceViewModel workspace,
        Func<ToolRecipeGridRectangle, HeightImageRoiCandidateUpdateResult> tryUpdateCandidate,
        Action<string> selectPipelineStep,
        Action<string> appendWarning,
        Action applyTeachingSelectionCapture,
        Action cancelTeachingSelectionCapture,
        Action removeSelectedTeachingSelection)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        this.tryUpdateCandidate =
            tryUpdateCandidate ?? throw new ArgumentNullException(nameof(tryUpdateCandidate));
        this.selectPipelineStep =
            selectPipelineStep ?? throw new ArgumentNullException(nameof(selectPipelineStep));
        this.appendWarning = appendWarning ?? throw new ArgumentNullException(nameof(appendWarning));
        this.applyTeachingSelectionCapture = applyTeachingSelectionCapture
            ?? throw new ArgumentNullException(nameof(applyTeachingSelectionCapture));
        this.cancelTeachingSelectionCapture = cancelTeachingSelectionCapture
            ?? throw new ArgumentNullException(nameof(cancelTeachingSelectionCapture));
        this.removeSelectedTeachingSelection = removeSelectedTeachingSelection
            ?? throw new ArgumentNullException(nameof(removeSelectedTeachingSelection));

        workspace.CandidateChanged += OnCandidateChanged;
        workspace.SelectionRequested += OnSelectionRequested;
        workspace.ApplyRequested += OnApplyRequested;
        workspace.CancelRequested += OnCancelRequested;
        workspace.DeleteRequested += OnDeleteRequested;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            workspace.CandidateChanged -= OnCandidateChanged;
            workspace.SelectionRequested -= OnSelectionRequested;
            workspace.ApplyRequested -= OnApplyRequested;
            workspace.CancelRequested -= OnCancelRequested;
            workspace.DeleteRequested -= OnDeleteRequested;
        }
    }

    private void OnCandidateChanged(object? sender, HeightImageRoiCandidateChangedEventArgs args)
    {
        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            var result = tryUpdateCandidate(args.Rectangle);
            if (!result.Updated)
            {
                appendWarning($"Height Image ROI edit rejected | reason={result.Message}");
            }
        }
    }

    private void OnSelectionRequested(object? sender, HeightImageRoiSelectionRequestedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed && !string.IsNullOrWhiteSpace(args.SelectionId))
            {
                selectPipelineStep(args.SelectionId);
            }
        }
    }

    private void OnApplyRequested(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                applyTeachingSelectionCapture();
            }
        }
    }

    private void OnCancelRequested(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                cancelTeachingSelectionCapture();
            }
        }
    }

    private void OnDeleteRequested(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                removeSelectedTeachingSelection();
            }
        }
    }
}

internal readonly record struct HeightImageRoiCandidateUpdateResult(
    bool Updated,
    string Message);
