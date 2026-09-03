using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellSourceQualitySmoke
{
    public static async Task<bool> RunAsync(
        ToolWorkbenchViewModel workbench,
        ToolRecipeWorkbenchView workbenchView,
        DependencyObject shellRoot,
        Dispatcher dispatcher,
        string? reportPath)
    {
        var quality = workbench.SourceQuality;
        var timeout = Stopwatch.StartNew();
        while (!quality.HasReport
               && !quality.HasError
               && timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(25);
        }

        var beforeDirty = workbench.IsDirty;
        var beforeStepCount = workbench.PipelineSteps.Count;
        var beforeSelectionCount = workbench.Selections.Count;
        var beforeLogCount = workbench.RunLog.Count;
        var beforePreviewRunning = workbench.IsSelectedStepPreviewRunning;
        workbench.SelectSourceQualityCommand.Execute(null);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await dispatcher.InvokeAsync(() => workbenchView.UpdateLayout(), DispatcherPriority.Render);

        var visibleText = workbenchView.GetSelectedToolVisibleTextLayout();
        var diagnosticsSectionVisible = visibleText.Any(line => line.Contains(
            quality.Localization.SourceQualityGridDiagnostics,
            StringComparison.Ordinal));
        var visibleDiagnosticItemCount = quality.GridDiagnostics.Count(item =>
            visibleText.Any(line => line.Contains($"text={item.Title}", StringComparison.Ordinal)));
        var qualityBadge = InspectCurrentSourceQualityBadge(shellRoot, workbench.CurrentSourceQualitySummary);

        var expectedGlobalKind = quality.HasGridDiagnosticError
            ? "Error"
            : quality.Report?.Coverage.MissingSampleCount > 0
                ? "Warning"
                : "Pass";
        var passed = quality.HasReport
                      && !quality.IsLoading
                      && !quality.HasError
                      && workbench.IsCurrentSourceQualityStatusVisible
                      && string.Equals(
                          workbench.CurrentSourceQualityStatusKind,
                          expectedGlobalKind,
                          StringComparison.Ordinal)
                      && !string.IsNullOrWhiteSpace(workbench.CurrentSourceQualitySummary)
                      && !string.IsNullOrWhiteSpace(workbench.CurrentSourceQualityDetail)
                      && workbench.IsSourceQualityWorkspaceVisible
                      && !workbench.HasSelectedPipelineStep
                      && workbenchView.IsToolInspectorPaneSelected
                      && quality.GridDiagnostics.Count == 4
                      && diagnosticsSectionVisible
                      && visibleDiagnosticItemCount == quality.GridDiagnostics.Count
                      && qualityBadge.IsFullyVisible
                      && workbench.IsDirty == beforeDirty
                      && workbench.PipelineSteps.Count == beforeStepCount
                      && workbench.Selections.Count == beforeSelectionCount
                      && workbench.RunLog.Count == beforeLogCount
                      && workbench.IsSelectedStepPreviewRunning == beforePreviewRunning;

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var sourceReport = quality.Report;
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"SourceQualityWorkspaceSmoke|{(passed ? "Pass" : "Fail")}|viewOnly=true|recipeChanged=false|inspectionRun=false",
                    $"State|loading={quality.IsLoading}|hasReport={quality.HasReport}|hasError={quality.HasError}|visible={workbench.IsSourceQualityWorkspaceVisible}|selectedStep={workbench.SelectedPipelineStep?.Id ?? "(none)"}",
                    $"Diagnostics|count={quality.GridDiagnostics.Count}|state={sourceReport?.GridDiagnostics?.State}|paneSelected={workbenchView.IsToolInspectorPaneSelected}|sectionVisible={diagnosticsSectionVisible}|visibleItems={visibleDiagnosticItemCount}|codes={string.Join(',', quality.GridDiagnostics.Select(item => item.Code))}",
                    $"JobBarQualityBadge|visible={qualityBadge.IsVisible}|fullyVisible={qualityBadge.IsFullyVisible}|withinTitleBar={qualityBadge.IsWithinTitleBar}|withinWindow={qualityBadge.IsWithinWindow}|wrap={qualityBadge.TextWrapping}|trimming={qualityBadge.TextTrimming}|badgeSize={qualityBadge.BadgeWidth.ToString("F1", CultureInfo.InvariantCulture)}x{qualityBadge.BadgeHeight.ToString("F1", CultureInfo.InvariantCulture)}|textSize={qualityBadge.TextWidth.ToString("F1", CultureInfo.InvariantCulture)}x{qualityBadge.TextHeight.ToString("F1", CultureInfo.InvariantCulture)}|requiredTextHeight={qualityBadge.RequiredTextHeight.ToString("F1", CultureInfo.InvariantCulture)}|text={qualityBadge.Text}",
                    $"GlobalStatus|visible={workbench.IsCurrentSourceQualityStatusVisible}|kind={workbench.CurrentSourceQualityStatusKind}|summary={workbench.CurrentSourceQualitySummary}|detail={workbench.CurrentSourceQualityDetail.Replace(Environment.NewLine, " | ")}",
                    $"Source|name={quality.SourceName}|grid={sourceReport?.Grid.Width ?? 0}x{sourceReport?.Grid.Height ?? 0}|cells={sourceReport?.Grid.CellCount ?? 0}|valid={sourceReport?.Coverage.ValidSampleCount ?? 0}|validRatio={sourceReport?.Coverage.ValidRatio ?? 0:R}|missing={sourceReport?.Coverage.MissingSampleCount ?? 0}|missingRatio={sourceReport?.Coverage.MissingRatio ?? 0:R}",
                    $"Height|min={sourceReport?.Height.Minimum?.ToString("R") ?? "null"}|max={sourceReport?.Height.Maximum?.ToString("R") ?? "null"}|mean={sourceReport?.Height.Mean?.ToString("R") ?? "null"}|bins={sourceReport?.Height.Distribution?.BinCount ?? 0}|peak={sourceReport?.Height.Distribution?.PeakBinIndex ?? -1}",
                    $"Mask|bytes={sourceReport?.Coverage.InvalidCellMask.ByteLength ?? 0}|sha256={quality.MaskSha256}",
                    $"Channels|count={quality.Channels.Count}|available={string.Join(',', quality.Channels.Where(channel => channel.IsAvailable).Select(channel => channel.Name))}",
                    $"Boundary|dirty={beforeDirty}->{workbench.IsDirty}|steps={beforeStepCount}->{workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{workbench.Selections.Count}|logs={beforeLogCount}->{workbench.RunLog.Count}|previewRunning={beforePreviewRunning}->{workbench.IsSelectedStepPreviewRunning}",
                    $"Error|{quality.Error}"
                ]);
        }

        return passed;
    }

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

    private static SourceQualityBadgeEvidence InspectCurrentSourceQualityBadge(
        DependencyObject shellRoot,
        string expectedText)
    {
        var badge = FindVisualDescendant<Border>(
            shellRoot,
            element => AutomationProperties.GetAutomationId(element)
                == "StudioCurrentSourceQualityStatus");
        var text = badge is null
            ? null
            : FindVisualDescendant<TextBlock>(badge, _ => true);
        var titleBar = badge is null
            ? null
            : FindVisualAncestor<FrameworkElement>(
                badge,
                element => AutomationProperties.GetAutomationId(element) == "StudioTitleBar");
        var window = badge is null ? null : Window.GetWindow(badge);
        if (badge is null || text is null || titleBar is null || window is null)
        {
            return SourceQualityBadgeEvidence.Missing;
        }

        var pixelsPerDip = VisualTreeHelper.GetDpi(text).PixelsPerDip;
        var formattedText = new FormattedText(
            text.Text,
            CultureInfo.CurrentUICulture,
            text.FlowDirection,
            new Typeface(
                text.FontFamily,
                text.FontStyle,
                text.FontWeight,
                text.FontStretch),
            text.FontSize,
            text.Foreground,
            pixelsPerDip)
        {
            MaxTextWidth = Math.Max(1d, text.ActualWidth),
            Trimming = TextTrimming.None
        };
        const double tolerance = 1d;
        var isWithinTitleBar = IsWithin(badge, titleBar, tolerance);
        var isWithinWindow = IsWithin(badge, window, tolerance);
        var textHasFullHeight = text.ActualHeight + tolerance >= formattedText.Height;
        var isVisible = badge.IsVisible
                        && text.IsVisible
                        && badge.ActualWidth > 0d
                        && badge.ActualHeight > 0d
                        && text.ActualWidth > 0d
                        && text.ActualHeight > 0d;
        var isFullyVisible = isVisible
                             && string.Equals(text.Text, expectedText, StringComparison.Ordinal)
                             && text.TextWrapping == TextWrapping.Wrap
                             && text.TextTrimming == TextTrimming.None
                             && textHasFullHeight
                             && isWithinTitleBar
                             && isWithinWindow;

        return new SourceQualityBadgeEvidence(
            isVisible,
            isFullyVisible,
            isWithinTitleBar,
            isWithinWindow,
            text.TextWrapping,
            text.TextTrimming,
            badge.ActualWidth,
            badge.ActualHeight,
            text.ActualWidth,
            text.ActualHeight,
            formattedText.Height,
            text.Text);
    }

    private static bool IsWithin(FrameworkElement element, FrameworkElement ancestor, double tolerance)
    {
        var bounds = element.TransformToAncestor(ancestor)
            .TransformBounds(new Rect(element.RenderSize));
        return bounds.Left >= -tolerance
               && bounds.Top >= -tolerance
               && bounds.Right <= ancestor.ActualWidth + tolerance
               && bounds.Bottom <= ancestor.ActualHeight + tolerance;
    }

    private static T? FindVisualDescendant<T>(
        DependencyObject root,
        Func<T, bool> predicate)
        where T : DependencyObject
        => FindVisualDescendants<T>(root).FirstOrDefault(predicate);

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static T? FindVisualAncestor<T>(
        DependencyObject element,
        Func<T, bool> predicate)
        where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(element);
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match && predicate(match))
            {
                return match;
            }
        }

        return null;
    }

    private readonly record struct SourceQualityBadgeEvidence(
        bool IsVisible,
        bool IsFullyVisible,
        bool IsWithinTitleBar,
        bool IsWithinWindow,
        TextWrapping TextWrapping,
        TextTrimming TextTrimming,
        double BadgeWidth,
        double BadgeHeight,
        double TextWidth,
        double TextHeight,
        double RequiredTextHeight,
        string Text)
    {
        public static SourceQualityBadgeEvidence Missing { get; } = new(
            false,
            false,
            false,
            false,
            TextWrapping.NoWrap,
            TextTrimming.None,
            0d,
            0d,
            0d,
            0d,
            0d,
            string.Empty);
    }
}
