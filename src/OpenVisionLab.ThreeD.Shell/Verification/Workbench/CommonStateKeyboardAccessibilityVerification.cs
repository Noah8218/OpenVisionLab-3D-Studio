using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell;

internal static class CommonStateKeyboardAccessibilityVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D common state, keyboard, accessibility, and localization verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var originalLanguage = OpenVisionLanguage.Korean;
        Window? host = null;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            Check(
                "verification-runs-on-sta",
                Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                $"apartment={Thread.CurrentThread.GetApartmentState()}");

            OpenVisionLanguageService.Load();
            originalLanguage = OpenVisionLanguageService.CurrentLanguage;

            var descriptors = InspectionStepStateMatrix.All;
            var expectedStates = Enum.GetValues<InspectionStepState>();
            Check(
                "canonical-state-matrix-covers-eight-operator-states",
                descriptors.Count == 8
                && descriptors.Select(item => item.State).OrderBy(item => item)
                    .SequenceEqual(expectedStates.OrderBy(item => item))
                && descriptors.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() == 8,
                $"states={string.Join(",", descriptors.Select(item => item.Key))}");

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanLabels = descriptors.ToDictionary(
                item => item.State,
                item => ThreeDLocalization.Shared.StateLabel(item.State));
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishLabels = descriptors.ToDictionary(
                item => item.State,
                item => ThreeDLocalization.Shared.StateLabel(item.State));
            Check(
                "all-canonical-states-have-korean-and-english-labels",
                koreanLabels.Values.All(IsResolvedLabel)
                && englishLabels.Values.All(IsResolvedLabel)
                && koreanLabels.Values.All(label => !englishLabels.Values.Contains(label, StringComparer.Ordinal)),
                $"ko={string.Join(",", koreanLabels.Values)}|en={string.Join(",", englishLabels.Values)}");

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var catalogTool = ToolWorkbenchToolCatalog.Create().First();
            var step = new ToolWorkbenchPipelineStepItem(
                "p1d-state-check",
                catalogTool,
                string.Empty,
                "p1d.output");
            var propertyChanges = new List<string>();
            step.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is { } propertyName)
                {
                    propertyChanges.Add(propertyName);
                }
            };
            step.State = "Preview stale";
            Check(
                "pipeline-step-projects-authored-state-to-canonical-stale",
                step.CanonicalState == InspectionStepState.Stale
                && step.CanonicalStateKey == "stale"
                && step.CanonicalStateLabel == koreanLabels[InspectionStepState.Stale]
                && step.CanonicalStateAccessibleName.Contains("(stale)", StringComparison.Ordinal),
                $"state={step.CanonicalState};key={step.CanonicalStateKey};label={step.CanonicalStateLabel}");
            Check(
                "state-changes-notify-label-and-accessible-name",
                propertyChanges.Contains(nameof(step.CanonicalStateLabel), StringComparer.Ordinal)
                && propertyChanges.Contains(nameof(step.CanonicalStateAccessibleName), StringComparer.Ordinal),
                string.Join(",", propertyChanges));

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            propertyChanges.Clear();
            step.RefreshLocalizedStatePresentation();
            Check(
                "language-refresh-notifies-existing-pipeline-step-presentation",
                step.CanonicalStateLabel == englishLabels[InspectionStepState.Stale]
                && propertyChanges.Contains(nameof(step.CanonicalStateLabel), StringComparer.Ordinal)
                && propertyChanges.Contains(nameof(step.CanonicalStateAccessibleName), StringComparer.Ordinal),
                $"label={step.CanonicalStateLabel};notifications={string.Join(",", propertyChanges)}");

            step.OutputEnabled = false;
            Check(
                "disabled-output-retains-canonical-state-and-localized-policy-label",
                step.CanonicalStateKey == "disabled"
                && step.CanonicalStateLabel == ThreeDLocalization.Shared.OutputDisabled
                && step.CanonicalStateAccessibleName.Contains("(disabled)", StringComparison.Ordinal),
                $"key={step.CanonicalStateKey};label={step.CanonicalStateLabel}");

            var workbench = new ToolRecipeWorkbenchView
            {
                DataContext = new object(),
                ViewerContent = new object()
            };
            var shortcuts = workbench.InputBindings
                .OfType<KeyBinding>()
                .Select(binding => (binding.Key, binding.Modifiers))
                .ToHashSet();
            Check(
                "publish-is-reachable-with-explicit-control-enter-shortcut",
                shortcuts.Contains((Key.Enter, ModifierKeys.Control)),
                string.Join(", ", shortcuts.OrderBy(item => item.Key).ThenBy(item => item.Modifiers)));

            var application = Application.Current;
            var badgeStyle = application?.TryFindResource("ThreeD.WorkflowStateBadgeStyle") as Style;
            var textStyle = application?.TryFindResource("ThreeD.WorkflowStateTextStyle") as Style;
            var iconStyle = application?.TryFindResource("ThreeD.WorkflowStateIconStyle") as Style;
            Check(
                "shared-workflow-state-styles-are-available",
                application is not null && badgeStyle is not null && textStyle is not null && iconStyle is not null,
                $"application={application is not null};badge={badgeStyle is not null};text={textStyle is not null};icon={iconStyle is not null}");

            if (application is not null && badgeStyle is not null && textStyle is not null && iconStyle is not null)
            {
                var readyStep = new ToolWorkbenchPipelineStepItem(
                    "p1d-ready",
                    catalogTool,
                    string.Empty,
                    "p1d.ready");
                readyStep.State = "Published";
                var disabledStep = new ToolWorkbenchPipelineStepItem(
                    "p1d-disabled",
                    catalogTool,
                    string.Empty,
                    "p1d.disabled",
                    outputEnabled: false);
                disabledStep.State = "Published";

                var readyText = new TextBlock
                {
                    Style = textStyle,
                    TextWrapping = TextWrapping.NoWrap
                };
                readyText.SetBinding(TextBlock.TextProperty, new Binding(nameof(readyStep.CanonicalStateLabel)));
                var readyBadge = CreateBadge(readyStep, badgeStyle, readyText);
                var readyIcon = new Wpf.Ui.Controls.SymbolIcon
                {
                    Style = iconStyle,
                    DataContext = readyStep
                };

                var disabledText = new TextBlock
                {
                    Style = textStyle,
                    TextWrapping = TextWrapping.NoWrap
                };
                disabledText.SetBinding(TextBlock.TextProperty, new Binding(nameof(disabledStep.CanonicalStateLabel)));
                var disabledBadge = CreateBadge(disabledStep, badgeStyle, disabledText);

                var surface = new StackPanel
                {
                    Background = application.TryFindResource("ThreeD.WorkspaceBrush") as System.Windows.Media.Brush,
                    Margin = new Thickness(8)
                };
                surface.Children.Add(readyBadge);
                surface.Children.Add(readyIcon);
                surface.Children.Add(disabledBadge);
                host = new Window
                {
                    Content = surface,
                    Width = 320,
                    Height = 160,
                    ShowInTaskbar = false,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.None,
                    ShowActivated = false
                };
                host.Show();
                host.UpdateLayout();
                surface.UpdateLayout();

                var screenshotPath = Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(reportPath)) ?? Environment.CurrentDirectory,
                    "common-state-badges.png");
                WpfScreenshotCapture.Save(
                    WpfScreenshotCapture.Capture(surface).Bitmap,
                    screenshotPath);

                Check(
                    "canonical-state-badge-renders-text-and-automation-name",
                    readyBadge.ActualWidth > 0
                    && readyBadge.ActualHeight > 0
                    && readyText.Text == ThreeDLocalization.Shared.StateLabel(InspectionStepState.Ready)
                    && AutomationProperties.GetName(readyBadge) == readyStep.CanonicalStateAccessibleName
                    && AutomationProperties.GetHelpText(readyBadge) == readyStep.State,
                    $"size={readyBadge.ActualWidth:F0}x{readyBadge.ActualHeight:F0};text={readyText.Text};automation={AutomationProperties.GetName(readyBadge)}");
                Check(
                    "disabled-state-uses-distinct-rendered-surface-and-label",
                    disabledBadge.ActualWidth > 0
                    && disabledBadge.ActualHeight > 0
                    && disabledText.Text == ThreeDLocalization.Shared.OutputDisabled
                    && AutomationProperties.GetName(disabledBadge) == disabledStep.CanonicalStateAccessibleName
                    && !BrushesMatch(readyBadge.Background, disabledBadge.Background),
                    $"size={disabledBadge.ActualWidth:F0}x{disabledBadge.ActualHeight:F0};text={disabledText.Text};automation={AutomationProperties.GetName(disabledBadge)}");
                Check(
                    "state-icon-style-renders-alongside-text-cue",
                    readyIcon.ActualWidth > 0 && readyIcon.ActualHeight > 0,
                    $"size={readyIcon.ActualWidth:F0}x{readyIcon.ActualHeight:F0}");
                Check(
                    "fresh-wpf-state-evidence-is-written",
                    File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
                    screenshotPath);
            }
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | verifier-exception | {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            host?.Close();
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
        }

        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        var passedAll = passed == total && total > 0 && !lines.Any(line => line.StartsWith("FAIL | verifier-exception", StringComparison.Ordinal));
        lines.Add($"Result={(passedAll ? "PASS" : "FAIL")}|{passed}/{total}");
        File.WriteAllLines(fullReportPath, lines);
        summary = lines[^1];
        return passedAll;
    }

    private static Border CreateBadge(
        ToolWorkbenchPipelineStepItem step,
        Style badgeStyle,
        TextBlock text)
    {
        var border = new Border
        {
            Style = badgeStyle,
            DataContext = step,
            Child = text,
            Margin = new Thickness(0, 0, 0, 4)
        };
        border.SetBinding(
            AutomationProperties.NameProperty,
            new Binding(nameof(step.CanonicalStateAccessibleName)));
        border.SetBinding(
            AutomationProperties.HelpTextProperty,
            new Binding(nameof(step.State)));
        return border;
    }

    private static bool IsResolvedLabel(string label) =>
        !string.IsNullOrWhiteSpace(label)
        && !label.Contains("ThreeD.Inspection.State.", StringComparison.Ordinal);

    private static bool BrushesMatch(
        System.Windows.Media.Brush? first,
        System.Windows.Media.Brush? second) =>
        first is SolidColorBrush firstColor
        && second is SolidColorBrush secondColor
        ? firstColor.Color == secondColor.Color
        : Equals(first, second);
}
