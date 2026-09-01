using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal sealed class ShellValidationThresholdAssistantSmoke
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly ToolRecipeWorkbenchView workbenchView;

    public ShellValidationThresholdAssistantSmoke(
        ToolWorkbenchViewModel workbench,
        ToolRecipeWorkbenchView workbenchView)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        this.workbenchView = workbenchView ?? throw new ArgumentNullException(nameof(workbenchView));
    }

    public async Task ReassertForCaptureAsync()
    {
        // Constructor-time dock navigation can be deferred by the template.
        workbenchView.IsBottomPaneExpanded = true;
        workbenchView.ActivateValidationSet();
        workbench.IsValidationThresholdExpanded = true;
        await workbenchView.Dispatcher.InvokeAsync(
            () => { },
            System.Windows.Threading.DispatcherPriority.Loaded);
        await workbenchView.Dispatcher.InvokeAsync(
            () => { },
            System.Windows.Threading.DispatcherPriority.Render);
        await Task.Delay(250);
    }

    public void AppendEvidence(
        Visual windowRoot,
        string screenshotQualityReportPath)
    {
        ArgumentNullException.ThrowIfNull(windowRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotQualityReportPath);

        var thresholdExpanders =
            ShellSmokeArtifacts.FindVisualDescendants<Expander>(workbenchView)
                .Where(item => item.Name == "ValidationThresholdExpander")
                .ToArray();
        var thresholdExpander = thresholdExpanders.FirstOrDefault();
        var assistantBorders =
            ShellSmokeArtifacts.FindVisualDescendants<Border>(workbenchView)
                .Where(item =>
                    System.Windows.Automation.AutomationProperties.GetName(item)
                    == workbench.Localization.ValidationSetThresholdAssistant)
                .ToArray();
        var assistantBorder = assistantBorders.FirstOrDefault();
        var assistantPoint = assistantBorder is null
            ? new Point(double.NaN, double.NaN)
            : assistantBorder.TransformToAncestor(windowRoot).Transform(new Point(0, 0));
        var expanderPoint = thresholdExpander is null
            ? new Point(double.NaN, double.NaN)
            : thresholdExpander.TransformToAncestor(windowRoot).Transform(new Point(0, 0));
        var workbenchPoint = workbenchView.TransformToAncestor(windowRoot).Transform(new Point(0, 0));
        var hostButtons = ShellSmokeArtifacts.FindVisualDescendants<Button>(workbenchView)
            .Where(item => item.Name is "AnalyzeValidationThresholdButton"
                or "ProposeValidationThresholdButton"
                or "ReviewValidationThresholdButton"
                or "CancelValidationThresholdReviewButton"
                or "ApplyValidationThresholdButton")
            .Select(item =>
            {
                var point = item.TransformToAncestor(windowRoot).Transform(new Point(0, 0));
                return $"{item.Name}:{item.Visibility}:{item.IsEnabled}:{item.ActualWidth:F1}x{item.ActualHeight:F1}@{point.X:F1},{point.Y:F1}";
            });
        var correctionTextBlock =
            ShellSmokeArtifacts.FindVisualDescendants<TextBlock>(workbenchView)
                .FirstOrDefault(item =>
                    item.Text == workbench.ValidationThresholdCorrectionSummary);
        var correctionPoint = correctionTextBlock is null
            ? new Point(double.NaN, double.NaN)
            : correctionTextBlock.TransformToAncestor(windowRoot).Transform(new Point(0, 0));

        File.AppendAllLines(
            Path.GetFullPath(screenshotQualityReportPath),
        [
            $"ValidationAssistant|samples={workbench.ValidationSetSamples.Count}|report={workbench.HasValidationThresholdAssistantAnalysis}|candidates={workbench.ValidationThresholdCandidates.Count}|selected={workbench.SelectedValidationThresholdCandidate?.CandidateId ?? string.Empty}|stage={workbench.ValidationThresholdAssistantStage}|proposal={workbench.HasValidationThresholdAssistantProposal}|review={workbench.IsValidationThresholdReviewActive}|applied={workbench.IsValidationThresholdCandidateApplied}",
            $"ValidationAssistantUi|expanders={thresholdExpanders.Length}|expanded={thresholdExpander?.IsExpanded}|visibility={thresholdExpander?.Visibility}|height={thresholdExpander?.ActualHeight:F1}|expanderPoint={expanderPoint.X:F1},{expanderPoint.Y:F1}|workbenchPoint={workbenchPoint.X:F1},{workbenchPoint.Y:F1}|assistantBorders={assistantBorders.Length}|assistantHeight={assistantBorder?.ActualHeight:F1}|assistantVisibility={assistantBorder?.Visibility}|assistantPoint={assistantPoint.X:F1},{assistantPoint.Y:F1}|buttons={string.Join(",", hostButtons)}|correctionLength={workbench.ValidationThresholdCorrectionSummary.Length}|correctionHeight={correctionTextBlock?.ActualHeight:F1}|correctionPoint={correctionPoint.X:F1},{correctionPoint.Y:F1}"
        ]);
    }
}
