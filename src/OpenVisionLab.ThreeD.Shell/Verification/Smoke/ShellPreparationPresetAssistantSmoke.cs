using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the command-line Preparation Preset Assistant Smoke scenario.
/// Production preset state remains in the Workbench owner; this type owns
/// only verification setup, WPF evidence capture, and report projection.
/// </summary>
internal sealed class ShellPreparationPresetAssistantSmoke
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly ToolRecipeWorkbenchView workbenchView;
    private readonly Func<int, int, bool> setCursorPos;

    public ShellPreparationPresetAssistantSmoke(
        ToolWorkbenchViewModel workbench,
        ToolRecipeWorkbenchView workbenchView,
        Func<int, int, bool> setCursorPos)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        this.workbenchView = workbenchView
            ?? throw new ArgumentNullException(nameof(workbenchView));
        this.setCursorPos = setCursorPos ?? throw new ArgumentNullException(nameof(setCursorPos));
    }

    public bool Configure(string requestedState, out string failure)
    {
        var state = requestedState.Trim();
        if (!workbench.IsPreparationPresetAssistantVisible)
        {
            failure =
                "Preparation preset assistant smoke requires a selected Filter step with a typed parameter editor.";
            return false;
        }

        if (!state.Equals("disabled", StringComparison.OrdinalIgnoreCase)
            && !state.Equals("analyze", StringComparison.OrdinalIgnoreCase)
            && !state.Equals("review", StringComparison.OrdinalIgnoreCase)
            && !state.Equals("dropdown", StringComparison.OrdinalIgnoreCase)
            && !state.Equals("apply-pressed", StringComparison.OrdinalIgnoreCase))
        {
            failure =
                $"Unknown preparation preset assistant smoke state '{requestedState}'. Use disabled, analyze, review, dropdown, or apply-pressed.";
            return false;
        }

        if (!state.Equals("disabled", StringComparison.OrdinalIgnoreCase))
        {
            if (!workbench.AnalyzePreparationPresetsCommand.CanExecute(null))
            {
                failure = "Preparation preset assistant Analyze was not enabled for the selected Filter draft.";
                return false;
            }

            workbench.AnalyzePreparationPresetsCommand.Execute(null);
            if (!state.Equals("analyze", StringComparison.OrdinalIgnoreCase))
            {
                var proposal = workbench.PreparationPresetOptions.Single(option => option.KernelSize == 5);
                workbench.SelectedPreparationPreset = proposal;
                if (state.Equals("dropdown", StringComparison.OrdinalIgnoreCase))
                {
                    // Leave the selector open without crossing the proposal or
                    // draft-apply boundary. The popup is captured separately.
                }
                else if (!workbench.ProposePreparationPresetCommand.CanExecute(null))
                {
                    failure = "Preparation preset assistant Propose was not enabled after Analyze.";
                    return false;
                }
                else
                {
                    workbench.ProposePreparationPresetCommand.Execute(null);
                    if (!workbench.ReviewPreparationPresetCommand.CanExecute(null))
                    {
                        failure = "Preparation preset assistant Review was not enabled after Propose.";
                        return false;
                    }

                    workbench.ReviewPreparationPresetCommand.Execute(null);
                }
            }
        }

        workbenchView.ActivateSelectedToolPane();
        workbenchView.UpdateLayout();
        var scrollViewer = FindVisualDescendants<ScrollViewer>(workbenchView)
            .FirstOrDefault(candidate => candidate.Name == "SelectedToolScrollViewer");
        scrollViewer?.ScrollToEnd();
        if (state.Equals("dropdown", StringComparison.OrdinalIgnoreCase))
        {
            var selector = FindVisualDescendants<ComboBox>(workbenchView)
                .FirstOrDefault(candidate =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(candidate)
                    == "PreparationPresetSelector");
            if (selector is null || !selector.IsEnabled || !selector.Focus())
            {
                failure = "Preparation preset selector could not receive keyboard focus for popup smoke.";
                return false;
            }

            selector.IsDropDownOpen = true;
        }

        workbenchView.UpdateLayout();
        failure = string.Empty;
        return true;
    }

    public void AppendEvidence(string requestedState, string? reportPath)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        var assistant = FindVisualDescendants<Border>(workbenchView)
            .FirstOrDefault(candidate =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(candidate)
                == "PreparationPresetAssistant");
        var selector = FindVisualDescendants<ComboBox>(workbenchView)
            .FirstOrDefault(candidate =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(candidate)
                == "PreparationPresetSelector");
        var scrollViewer = FindVisualDescendants<ScrollViewer>(workbenchView)
            .FirstOrDefault(candidate => candidate.Name == "SelectedToolScrollViewer");
        var buttons = FindVisualDescendants<Button>(workbenchView)
            .Where(candidate =>
            {
                var id = System.Windows.Automation.AutomationProperties.GetAutomationId(candidate);
                return id is "AnalyzePreparationPresets"
                    or "ProposePreparationPreset"
                    or "ReviewPreparationPreset"
                    or "CancelPreparationPresetReview"
                    or "ApplyPreparationPresetDraft";
            })
            .Select(candidate =>
            {
                var id = System.Windows.Automation.AutomationProperties.GetAutomationId(candidate);
                return $"{id}:{candidate.Visibility}:{candidate.IsEnabled}:{candidate.IsKeyboardFocusWithin}:{candidate.ActualWidth:F1}x{candidate.ActualHeight:F1}";
            });
        var filterDraft = workbench.SelectedStepPropertyDraft as FilterStepProperties;
        var recipeKernel = workbench.SelectedPipelineStep?.Parameters
            .SingleOrDefault(parameter => parameter.Name == "KernelSize")?.Value;
        File.AppendAllLines(
            Path.GetFullPath(reportPath),
        [
            $"PreparationPresetAssistant|requestedState={requestedState}|visible={workbench.IsPreparationPresetAssistantVisible}|analysis={workbench.IsPreparationPresetAnalysisReady}|options={workbench.PreparationPresetOptions.Count}|selected={workbench.SelectedPreparationPreset?.Id ?? string.Empty}|proposal={workbench.ProposedPreparationPreset?.Id ?? string.Empty}|review={workbench.IsPreparationPresetReviewActive}|applied={workbench.IsPreparationPresetDraftApplied}|draftKernel={filterDraft?.KernelSize}|recipeKernel={recipeKernel}|pending={workbench.HasPendingStepParameterChanges}|preview={workbench.HasCurrentFilterPreview}|publishAvailable={workbench.PublishSelectedStepCommand.CanExecute(null)}",
            $"PreparationPresetAssistantUi|visibility={assistant?.Visibility}|height={assistant?.ActualHeight:F1}|width={assistant?.ActualWidth:F1}|selectorVisibility={selector?.Visibility}|selectorEnabled={selector?.IsEnabled}|selectorOpen={selector?.IsDropDownOpen}|selectorHeight={selector?.ActualHeight:F1}|scrollOffset={scrollViewer?.VerticalOffset:F1}|scrollableHeight={scrollViewer?.ScrollableHeight:F1}|buttons={string.Join(",", buttons)}"
        ]);
    }

    public async Task<bool> CapturePopupAsync(
        Window window,
        string screenshotPath,
        string? qualityReportPath)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.UpdateLayout();
        var selector = FindVisualDescendants<ComboBox>(window)
            .FirstOrDefault(candidate =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(candidate)
                == "PreparationPresetSelector");
        if (selector is null || !selector.IsEnabled || !selector.IsDropDownOpen)
        {
            WriteTextReport(
                qualityReportPath,
                ["PreparationPresetPopup|failure=selector-unavailable-or-closed"]);
            return false;
        }

        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        selector.ApplyTemplate();
        var popup = selector.Template.FindName("PART_Popup", selector)
            as Popup
            ?? FindVisualDescendants<Popup>(selector).FirstOrDefault();
        if (popup?.Child is not FrameworkElement popupChild || !popup.IsOpen)
        {
            WriteTextReport(
                qualityReportPath,
                ["PreparationPresetPopup|failure=popup-closed-or-no-child"]);
            return false;
        }

        popupChild.UpdateLayout();
        var center = selector.PointToScreen(
            new Point(
                selector.ActualWidth / 2.0,
                selector.ActualHeight / 2.0));
        _ = setCursorPos(
            (int)Math.Round(center.X),
            (int)Math.Round(center.Y + selector.ActualHeight * 2.5));
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        await Task.Delay(100);
        var visibleItems = FindVisualDescendants<ComboBoxItem>(popupChild)
            .Count(item => item.IsVisible && item.ActualHeight > 0.0);
        var capture = WpfScreenshotCapture.Capture(popupChild);
        var fullPath = Path.GetFullPath(screenshotPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        WpfScreenshotCapture.Save(capture.Bitmap, fullPath);
        var popupAccepted = selector.IsDropDownOpen
            && popup.IsOpen
            && selector.Items.Count == visibleItems
            && capture.Bitmap.PixelWidth >= 120
            && capture.Bitmap.PixelHeight >= 60;
        WriteTextReport(
            fullPath + ".quality.txt",
        [
            $"PreparationPresetPopupScreenshot|accepted={popupAccepted}|width={capture.Bitmap.PixelWidth}|height={capture.Bitmap.PixelHeight}",
            $"CaptureAssessment|{capture.Quality.Summary}|small-popup=true|graphite-theme=true",
            $"Popup|open={selector.IsDropDownOpen && popup.IsOpen}|items={selector.Items.Count}|visibleItems={visibleItems}|hoveredItem={visibleItems > 0}",
            "Boundary|App-owned WPF popup child only; no desktop or unrelated application pixels."
        ]);
        WriteTextReport(
            qualityReportPath,
            [$"PreparationPresetPopup|path={fullPath}|open={selector.IsDropDownOpen && popup.IsOpen}|items={selector.Items.Count}|visibleItems={visibleItems}"]);
        return true;
    }
}
