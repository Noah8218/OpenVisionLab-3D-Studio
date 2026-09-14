using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellSourceAcquisitionProvenanceSmoke
{
    public static async Task<string?> ConfigureAcquisitionProvenanceStateAsync(
        ToolWorkbenchViewModel workbench,
        ToolRecipeWorkbenchView workbenchView,
        Dispatcher dispatcher,
        string requestedState,
        string? popupScreenshotPath)
    {
        var quality = workbench.SourceQuality;
        if (!workbench.IsSourceQualityWorkspaceVisible)
        {
            return "Acquisition provenance state smoke requires the visible Source Quality workspace.";
        }

        workbenchView.ActivateSelectedToolPane();
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        var beforeDirty = workbench.IsDirty;
        var beforeSteps = workbench.PipelineSteps.Count;
        var beforeSelections = workbench.Selections.Count;
        var beforeLogs = workbench.RunLog.Count;
        var beforePreview = workbench.IsSelectedStepPreviewRunning;
        var beforeValidation = workbench.IsValidationSetRunning;

        switch (requestedState.Trim().ToLowerInvariant())
        {
            case "validation-focus":
            {
                quality.AcquisitionEvidenceDraft = string.Empty;
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var evidence = FindVisualDescendant<TextBox>(
                    workbenchView,
                    textBox => AutomationProperties.GetAutomationId(textBox)
                               == "SourceAcquisitionEvidence");
                if (evidence is null
                    || !evidence.Focus()
                    || !quality.HasAcquisitionValidationError
                    || quality.ApplyAcquisitionProvenanceCommand.CanExecute(null))
                {
                    return "Acquisition provenance validation state or keyboard focus was unavailable.";
                }

                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                break;
            }
            case "available-hover":
            {
                quality.SelectedAcquisitionStateOption =
                    quality.AcquisitionStateOptions.Single(option =>
                        option.State == ToolRecipeAcquisitionProvenanceState.Available);
                quality.AcquisitionEvidenceDraft =
                    "Verified acquisition record ACQ-20260804-17 is available.";
                quality.AcquisitionLimitationNotesDraft =
                    "Viewpoint, sensor pose, calibration, and capture conditions were not supplied.";
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var apply = FindVisualDescendant<Button>(
                    workbenchView,
                    button => AutomationProperties.GetAutomationId(button)
                              == "ApplySourceAcquisitionProvenance");
                if (apply is null
                    || !apply.IsEnabled
                    || !quality.ApplyAcquisitionProvenanceCommand.CanExecute(null)
                    || !apply.Focus())
                {
                    return "Acquisition provenance enabled Apply state was unavailable.";
                }

                var center = apply.PointToScreen(
                    new Point(
                        apply.ActualWidth / 2.0,
                        apply.ActualHeight / 2.0));
                if (!SetCursorPos(
                        (int)Math.Round(center.X),
                        (int)Math.Round(center.Y)))
                {
                    return "Acquisition provenance Apply hover state was unavailable.";
                }

                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                await Task.Delay(100);
                break;
            }
            case "direction-available-focus":
            {
                quality.SelectedAcquisitionStateOption =
                    quality.AcquisitionStateOptions.Single(option =>
                        option.State == ToolRecipeAcquisitionProvenanceState.Available);
                quality.SelectedAcquisitionDirectionStateOption =
                    quality.AcquisitionDirectionStateOptions.Single(option =>
                        option.State == ToolRecipeAcquisitionDirectionState.Available);
                quality.AcquisitionEvidenceDraft =
                    "Verified acquisition record ACQ-20260804-17 is available.";
                quality.AcquisitionLimitationNotesDraft =
                    "Direction is explicit; camera pose and calibration were not supplied.";
                quality.AcquisitionDirectionXDraft = "0";
                quality.AcquisitionDirectionYDraft = "0";
                quality.AcquisitionDirectionZDraft = "-1";
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var zDirection = FindVisualDescendant<TextBox>(
                    workbenchView,
                    textBox => AutomationProperties.GetAutomationId(textBox)
                               == "SourceAcquisitionDirectionZ");
                if (zDirection is null || !zDirection.IsEnabled)
                {
                    return "Acquisition direction enabled input or keyboard-focus state was unavailable.";
                }

                zDirection.BringIntoView();
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                if (!zDirection.Focus()
                    || !quality.ApplyAcquisitionProvenanceCommand.CanExecute(null))
                {
                    return "Acquisition direction enabled input or keyboard-focus state was unavailable.";
                }

                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                break;
            }
            case "open-dropdown":
            {
                var selectors = FindVisualDescendants<ComboBox>(workbenchView)
                    .Where(comboBox =>
                        AutomationProperties.GetAutomationId(comboBox)
                        == "SourceAcquisitionProvenanceState")
                    .ToArray();
                var selector = selectors.FirstOrDefault(comboBox =>
                    comboBox.IsVisible
                    && comboBox.IsEnabled
                    && comboBox.ActualWidth > 0.0
                    && comboBox.ActualHeight > 0.0);
                if (selector is null)
                {
                    var candidateStates = string.Join(
                        "; ",
                        selectors.Select(comboBox =>
                            $"visible={comboBox.IsVisible}, enabled={comboBox.IsEnabled}, "
                            + $"size={comboBox.ActualWidth:0.#}x{comboBox.ActualHeight:0.#}"));
                    return "Acquisition provenance state selector was unavailable. "
                           + $"Candidates={selectors.Length}: {candidateStates}";
                }

                _ = selector.Focus();
                selector.IsDropDownOpen = true;
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var center = selector.PointToScreen(
                    new Point(
                        selector.ActualWidth / 2.0,
                        selector.ActualHeight / 2.0));
                if (!SetCursorPos(
                        (int)Math.Round(center.X),
                        (int)Math.Round(center.Y + selector.ActualHeight * 2.5)))
                {
                    return "Acquisition provenance popup hover state was unavailable.";
                }

                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                await Task.Delay(100);
                if (!string.IsNullOrWhiteSpace(popupScreenshotPath))
                {
                    selector.ApplyTemplate();
                    var popup = selector.Template.FindName("PART_Popup", selector)
                        as Popup
                        ?? FindVisualDescendants<Popup>(selector).FirstOrDefault();
                    if (popup?.Child is not FrameworkElement popupChild || !popup.IsOpen)
                    {
                        return "Acquisition provenance popup was closed or had no captureable child.";
                    }

                    var fullPopupPath = Path.GetFullPath(popupScreenshotPath);
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(fullPopupPath) ?? Environment.CurrentDirectory);
                    popupChild.UpdateLayout();
                    var capture = WpfScreenshotCapture.Capture(popupChild);
                    WpfScreenshotCapture.Save(capture.Bitmap, fullPopupPath);
                    WriteTextReport(
                        fullPopupPath + ".quality.txt",
                    [
                        $"SourceAcquisitionProvenancePopup|{capture.Quality.Summary}",
                        "Boundary|App-owned WPF popup child only; no desktop or unrelated application pixels."
                    ]);
                }

                break;
            }
            default:
                return $"Unknown acquisition provenance smoke state: {requestedState}.";
        }

        var boundaryPreserved = workbench.IsDirty == beforeDirty
                                && workbench.PipelineSteps.Count == beforeSteps
                                && workbench.Selections.Count == beforeSelections
                                && workbench.RunLog.Count == beforeLogs
                                && workbench.IsSelectedStepPreviewRunning == beforePreview
                                && workbench.IsValidationSetRunning == beforeValidation;
        return boundaryPreserved
            ? null
            : "Acquisition provenance visual-state smoke changed recipe or execution state.";
    }

    private static T? FindVisualDescendant<T>(
        DependencyObject root,
        Func<T, bool> predicate)
        where T : DependencyObject =>
        FindVisualDescendants<T>(root).FirstOrDefault(predicate);
}
