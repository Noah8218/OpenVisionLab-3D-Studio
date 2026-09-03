using System.IO;
using System.Threading;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public abstract class ToolLabWindowBase : Window, IDisposable
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly string expectedToolId;
    private readonly string expectedStepMismatchMessage;
    private List<OpenVisionThreeDViewerControl>? ownedViewers = new();
    private int disposalState;
    private string labStepId = string.Empty;

    protected ToolLabWindowBase(
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        string expectedToolId,
        string expectedStepMismatchMessage,
        bool activateOnActivated = true)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        this.expectedToolId = string.IsNullOrWhiteSpace(expectedToolId)
            ? throw new ArgumentException("Expected tool id is required.", nameof(expectedToolId))
            : expectedToolId;
        this.expectedStepMismatchMessage = string.IsNullOrWhiteSpace(expectedStepMismatchMessage)
            ? throw new ArgumentException("Expected mismatch message is required.", nameof(expectedStepMismatchMessage))
            : expectedStepMismatchMessage;
        SetLabStep(step);
        Loaded += OnLoaded;
        if (activateOnActivated)
        {
            Activated += OnActivated;
        }
        Closed += OnClosed;
    }

    protected ToolWorkbenchViewModel Workbench => workbench;

    protected string LabStepId => labStepId;

    protected bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    protected bool IsActiveStep => string.Equals(workbench.SelectedPipelineStep?.Id, labStepId, StringComparison.Ordinal);

    public void SetLabStep(ToolWorkbenchPipelineStepItem step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!string.Equals(step.ToolId, expectedToolId, StringComparison.Ordinal))
        {
            throw new ArgumentException(expectedStepMismatchMessage, nameof(step));
        }

        labStepId = step.Id;
        ActivateLabStep();
    }

    public void ActivateLabStep()
    {
        if (IsDisposed)
        {
            return;
        }

        if (!string.Equals(workbench.SelectedPipelineStep?.Id, labStepId, StringComparison.Ordinal))
        {
            workbench.SelectPipelineStepCommand.Execute(labStepId);
        }
    }

    protected void OwnViewer(OpenVisionThreeDViewerControl viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        if (IsDisposed)
        {
            viewer.Dispose();
            return;
        }

        ownedViewers!.Add(viewer);
    }

    public virtual void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        Loaded -= OnLoaded;
        Activated -= OnActivated;
        Closed -= OnClosed;

        var viewers = Interlocked.Exchange(ref ownedViewers, null);
        if (viewers is not null)
        {
            foreach (var viewer in viewers)
            {
                viewer.Dispose();
            }
        }

        GC.SuppressFinalize(this);
    }

    protected static bool HasSourcePath(string sourcePath) =>
        !string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath);

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (!IsDisposed)
        {
            RefreshViews();
        }
    }

    private void OnActivated(object? sender, EventArgs args)
    {
        if (!IsDisposed)
        {
            ActivateLabStep();
        }
    }

    private void OnClosed(object? sender, EventArgs args) => Dispose();

    public abstract void RefreshViews();
}
