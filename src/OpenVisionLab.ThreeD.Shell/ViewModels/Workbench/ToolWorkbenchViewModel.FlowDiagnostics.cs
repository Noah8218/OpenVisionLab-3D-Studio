using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Projects existing artifact identity into read-only input/output port diagnostics.
/// It never edits a route, changes a parameter, or invokes Preview, Run, or Publish.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    public ObservableCollection<ToolWorkbenchFlowPortDiagnosticItem> FlowPortDiagnostics { get; } = [];

    public ICommand FocusFlowProblemStepCommand { get; private set; } = null!;

    public string FlowProblemsSummary => string.Format(
        Localization.ProblemsSummaryFormat,
        FlowPortDiagnostics.Count,
        ValidationMessages.Count);

    public bool HasFlowProblems => FlowPortDiagnostics.Count > 0 || ValidationMessages.Count > 0;

    private void InitializeFlowDiagnostics()
    {
        FocusFlowProblemStepCommand = new RelayCommand(
            parameter => FocusFlowProblemStep(parameter as ToolWorkbenchFlowPortDiagnosticItem),
            parameter => parameter is ToolWorkbenchFlowPortDiagnosticItem);
    }

    private void OnFlowDiagnosticsLocalizationChanged(object? sender, PropertyChangedEventArgs args) =>
        RebuildFlowPortDiagnostics();

    private void RebuildFlowPortDiagnostics()
    {
        FlowPortDiagnostics.Clear();
        foreach (var step in PipelineSteps)
        {
            var input = DescribeInputPort(step);
            var output = DescribeOutputPort(step);
            step.UpdateFlowPortPresentation(
                input.Status,
                input.Detail,
                input.IsProblem,
                output.Status,
                output.Detail,
                output.IsProblem);

            if (input.IsProblem)
            {
                FlowPortDiagnostics.Add(new ToolWorkbenchFlowPortDiagnosticItem(
                    "Input",
                    input.Kind,
                    input.Status,
                    step.InputSummary,
                    input.Detail,
                    step));
            }

            if (output.IsProblem)
            {
                FlowPortDiagnostics.Add(new ToolWorkbenchFlowPortDiagnosticItem(
                    "Output",
                    output.Kind,
                    output.Status,
                    step.OutputEntityId,
                    output.Detail,
                    step));
            }
        }

        OnPropertyChanged(nameof(FlowProblemsSummary));
        OnPropertyChanged(nameof(HasFlowProblems));
    }

    private FlowPortPresentation DescribeInputPort(ToolWorkbenchPipelineStepItem step)
    {
        if (step.InputEntityIds.Count == 0)
        {
            return new FlowPortPresentation(
                "Unresolved",
                Localization.FlowPortUnresolved,
                Localization.FlowPortNoInputDetail,
                true);
        }

        var assessments = step.InputEntityIds
            .Select(DescribeInputArtifact)
            .OrderByDescending(assessment => assessment.Priority)
            .ToArray();
        var primary = assessments[0];
        var detail = string.Join(" ", assessments
            .Where(assessment => assessment.IsProblem)
            .Select(assessment => assessment.Detail));

        return new FlowPortPresentation(
            primary.Kind,
            primary.Status,
            string.IsNullOrWhiteSpace(detail)
                ? string.Join(" | ", assessments.Select(assessment => assessment.Detail))
                : detail,
            primary.IsProblem);
    }

    private FlowPortAssessment DescribeInputArtifact(string inputId)
    {
        var artifact = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Id, inputId, StringComparison.OrdinalIgnoreCase));
        if (artifact is null)
        {
            return new FlowPortAssessment(
                "Unresolved",
                Localization.FlowPortUnresolved,
                string.Format(Localization.FlowPortUnresolvedDetailFormat, inputId),
                true,
                3);
        }

        if (IsStaleArtifact(artifact))
        {
            return new FlowPortAssessment(
                "Stale",
                Localization.FlowPortStale,
                string.Format(Localization.FlowPortStaleDetailFormat, inputId),
                true,
                2);
        }

        if (string.Equals(artifact.State, "Declared", StringComparison.OrdinalIgnoreCase))
        {
            return new FlowPortAssessment(
                "WaitingForUpstream",
                Localization.FlowPortWaitingForUpstream,
                string.Format(Localization.FlowPortWaitingDetailFormat, inputId),
                true,
                1);
        }

        if (IsCurrentArtifact(artifact))
        {
            return new FlowPortAssessment(
                "Ready",
                Localization.FlowPortReady,
                $"{inputId} | {artifact.State}",
                false,
                0);
        }

        return new FlowPortAssessment(
            "Unresolved",
            Localization.FlowPortUnresolved,
            string.Format(Localization.FlowPortUnresolvedDetailFormat, inputId),
            true,
            3);
    }

    private FlowPortPresentation DescribeOutputPort(ToolWorkbenchPipelineStepItem step)
    {
        var artifact = ArtifactRegistry.FirstOrDefault(item =>
            string.Equals(item.Id, step.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (artifact is null)
        {
            return new FlowPortPresentation(
                "Unresolved",
                Localization.FlowPortUnresolved,
                string.Format(Localization.FlowPortUnresolvedDetailFormat, step.OutputEntityId),
                true);
        }

        if (IsStaleArtifact(artifact))
        {
            return new FlowPortPresentation(
                "Stale",
                Localization.FlowPortStale,
                string.Format(Localization.FlowPortStaleDetailFormat, step.OutputEntityId),
                true);
        }

        if (string.Equals(artifact.State, "Declared", StringComparison.OrdinalIgnoreCase))
        {
            return new FlowPortPresentation(
                "Declared",
                Localization.FlowPortDeclared,
                string.Format(Localization.FlowPortDeclaredDetailFormat, step.OutputEntityId),
                false);
        }

        if (IsCurrentArtifact(artifact))
        {
            return new FlowPortPresentation(
                "Current",
                Localization.FlowPortCurrent,
                string.Format(Localization.FlowPortCurrentDetailFormat, step.OutputEntityId),
                false);
        }

        return new FlowPortPresentation(
            "Unresolved",
            Localization.FlowPortUnresolved,
            string.Format(Localization.FlowPortUnresolvedDetailFormat, step.OutputEntityId),
            true);
    }

    private void FocusFlowProblemStep(ToolWorkbenchFlowPortDiagnosticItem? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedPipelineStep = item.Step;
        RefreshNavigatorSelection();
    }

    private static bool IsCurrentArtifact(ToolWorkbenchArtifactItem artifact) =>
        artifact.State is "Ready" or "Current selection" or "Preview" or "Published";

    private static bool IsStaleArtifact(ToolWorkbenchArtifactItem artifact) =>
        artifact.State.StartsWith("Stale", StringComparison.OrdinalIgnoreCase);

    private sealed record FlowPortPresentation(
        string Kind,
        string Status,
        string Detail,
        bool IsProblem);

    private sealed record FlowPortAssessment(
        string Kind,
        string Status,
        string Detail,
        bool IsProblem,
        int Priority);
}

public sealed record ToolWorkbenchFlowPortDiagnosticItem(
    string Port,
    string Kind,
    string Status,
    string EntityId,
    string Detail,
    ToolWorkbenchPipelineStepItem Step)
{
    public string StepTitle => $"Step {Step.Order}: {Step.ToolName}";
    public string AccessibleName => $"{StepTitle}. {Port}. {Status}. {Detail}";
}
