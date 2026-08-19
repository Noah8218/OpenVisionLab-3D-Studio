using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public abstract class ToolLabWindowBase : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly string expectedToolId;
    private readonly string expectedStepMismatchMessage;
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
        Loaded += (_, _) => RefreshViews();
        if (activateOnActivated)
        {
            Activated += (_, _) => ActivateLabStep();
        }
    }

    protected ToolWorkbenchViewModel Workbench => workbench;

    protected string LabStepId => labStepId;

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
        if (!string.Equals(workbench.SelectedPipelineStep?.Id, labStepId, StringComparison.Ordinal))
        {
            workbench.SelectPipelineStepCommand.Execute(labStepId);
        }
    }

    protected static bool HasSourcePath(string sourcePath) =>
        !string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath);

    public abstract void RefreshViews();
}
