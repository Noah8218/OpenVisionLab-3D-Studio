using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Presentation.Commands;
using OpenVisionLab.ThreeD.Presentation.Controls;
using OpenVisionLab.ThreeD.Presentation.Models;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.PropertyGrid;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell;

internal static class NumericDisplayVerification
{
    public static bool VerifyMeasurementText(string reportPath, out string summary)
    {
        var lines = new List<string> { "Boundary|Read-only measurement bindings; no inspection algorithm or SDK numerical evaluation." };
        var converter = new OpenVisionLab.ThreeD.Shell.Views.Workbench.MeasurementNumberTextConverter();
        (string Raw, string Display)[] cases =
        [
            ("102.3155008530864 raw-height", "102.32 raw-height"),
            ("792.3187659588774 raw-height", "792.32 raw-height"),
            ("-6.789012 mm", "-6.79 mm"),
            ("0.4799–0.4801 mm", "0.48–0.48 mm"),
            ("MaximumRms: 5.3999999999999995 raw-height", "MaximumRms: 5.4 raw-height"),
            ("Mean: 8.123456 → 8.239876 mm · NG", "Mean: 8.12 → 8.24 mm · NG"),
            ("gap -1.2345 / flush 2.3456", "gap -1.23 / flush 2.35"),
            ("1.234e-5 mm", "0 mm"),
            ("-0.000001 mm", "0 mm"),
            (".123456 mm", "0.12 mm"),
            ("유효 35,409 · RMS 16.722129", "유효 35,409 · RMS 16.72"),
            ("frame.c3d-grid-index / v1.2.3 / step.01", "frame.c3d-grid-index / v1.2.3 / step.01"),
            ("— / NaN / Infinity", "— / NaN / Infinity")
        ];
        var passed = 0;
        try
        {
            foreach (var test in cases)
            {
                var actual = converter.Convert(test.Raw, typeof(string), null!, CultureInfo.InvariantCulture);
                if (!Equals(actual, test.Display)) throw new InvalidOperationException($"Expected '{test.Display}', got '{actual}'.");
                lines.Add($"PASS | {test.Raw} -> {test.Display}");passed++;
            }
            var source = new { Value = "792.3187659588774 raw-height" };
            var label = new TextBlock();
            label.SetBinding(TextBlock.TextProperty, new Binding(nameof(source.Value)) { Source = source, Mode = BindingMode.OneWay, Converter = converter });
            label.GetBindingExpression(TextBlock.TextProperty)!.UpdateTarget();
            if (label.Text != "792.32 raw-height" || source.Value != "792.3187659588774 raw-height"
                || !ReferenceEquals(converter.ConvertBack("792.32", typeof(string), null!, CultureInfo.InvariantCulture), Binding.DoNothing))
                throw new InvalidOperationException("Binding must format the label and retain the original source text.");
            lines.Add("PASS | TextBlock binding shortens display and retains exact source text; ConvertBack performs no write");passed++;
            summary = $"Measurement number text verification: Pass ({passed} checks)";
            File.WriteAllLines(reportPath, lines.Append(summary));return true;
        }
        catch (Exception exception)
        {
            summary = $"Measurement number text verification: Fail after {passed} checks: {exception}";
            File.WriteAllLines(reportPath, lines.Append(summary));return false;
        }
    }

    public static bool VerifyReadOnlyFormatting(string reportPath, out string summary)
    {
        var lines = new List<string> { "Boundary|Read-only presentation formatters only. No calibration, inspection algorithm or SDK numerical evaluation is executed." };
        var passed = 0;
        void Check(string name, bool condition)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
            if (!condition) throw new InvalidOperationException(name);
            passed++;
        }
        try
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var raw = typeof(OpenVisionLab.ThreeD.Viewer.ViewModels.MainWindowViewModel).GetMethod("FormatRawValue", flags)!;
            Check("positive height label uses two decimals", Equals(raw.Invoke(null, [127.56625366210938, 34d]), "127.57"));
            Check("negative height label uses two decimals", Equals(raw.Invoke(null, [-6.7890123456789, 34d]), "-6.79"));
            Check("small nonconstant span rounds display only", Equals(raw.Invoke(null, [1.00005, .00005]), "1"));
            var calibration = typeof(CalibrationCenterViewModel).GetMethod("Format", flags)!;
            Check("read-only study value uses two decimals", Equals(calibration.Invoke(null, [10.004, "mm"]), "10 mm"));
            Check("read-only signed study value preserves its unit", Equals(calibration.Invoke(null, [-.125, "mm"]), "-0.13 mm"));
            Check("two-sided inspection limits are display values", InspectionQueueMeasurementPoint.FormatRange(.1234567, .2345678, "mm") == "0.12–0.23 mm");
            Check("one-sided inspection limit stays one-sided", InspectionQueueMeasurementPoint.FormatRange(null, .2, "raw-height") == "≤ 0.2 raw-height");
            Check("unavailable inspection limits stay unavailable", InspectionQueueMeasurementPoint.FormatRange(null, null, "mm") == "—");
            var change = typeof(ToolWorkbenchViewModel).GetMethod("FormatParameterChangeValue", flags)!;
            Check("settings history shortens numeric text", Equals(change.Invoke(null, ["7.123456789"]), "7.12"));
            Check("settings history preserves identifiers", Equals(change.Invoke(null, ["frame.c3d-grid-index"]), "frame.c3d-grid-index"));
            var metric = new ResultsStoredMetric("metric", "step", "tool", "Mean", OpenVisionLab.ThreeD.Core.MetricKind.Length,
                8.123456789012346, "raw-height", null, OpenVisionLab.ThreeD.Core.ResultStatus.Pass, "settings");
            Check("stored Results value displays two decimals without replacing the raw double", metric.ValueText == "8.12" && metric.Value == 8.123456789012346);
            var statistics = new ResultsMeasurementStatistics("recipe", "settings", "frame", "raw-height", 1, .123456789, -.987654321, 123.456789, .000012345, 0, 0);
            Check("Results summaries use at most two fractional digits", statistics.MeanText == "0.12" && statistics.MinimumText == "-0.99"
                && statistics.MaximumText == "123.46" && statistics.SigmaText == "0" && statistics.SampleStandardDeviation == .000012345);
            var controlType = typeof(OpenVisionLab.ThreeD.Viewer.OpenVisionThreeDViewerControl);
            var point = new System.Numerics.Vector3(1.23456f, -6.789012f, 0);
            Check("machine vector contract keeps the existing three-decimal format", Equals(controlType.GetMethod("FormatVector", flags)!.Invoke(null, [point]), "(1.235, -6.789, 0.000)"));
            Check("human vector display uses at most two decimals", Equals(controlType.GetMethod("FormatDisplayVector", flags)!.Invoke(null, [point]), "(1.23, -6.79, 0)"));
            Check("machine orientation vector contract stays unchanged", Equals(controlType.GetMethod("FormatVector2", flags)!.Invoke(null, [new System.Numerics.Vector2(1.23456f, 0)]), "(1.235, 0.000)"));
            var sourcePoint = new LazPointCloudPoint(point, 0, 10, 20, 30) { HasPreciseSourceCoordinate = true,
                SourceCoordinate = new(123456.78901234567, -6.7890123456789, .000123456789, 7) };
            var sourceFormatter = controlType.GetMethod("FormatLazSourceCoordinate", flags)!;
            var sourceText = (string)sourceFormatter.Invoke(null, [sourcePoint, "G17"])!;
            var coordinates = sourceText.Trim('(', ')').Split(',').Select(text => double.Parse(text, CultureInfo.InvariantCulture)).ToArray();
            Check("machine point-cloud source coordinates retain exact doubles", coordinates.SequenceEqual(new[] { sourcePoint.SourceCoordinate.X, sourcePoint.SourceCoordinate.Y, sourcePoint.SourceCoordinate.Z }));
            Check("point-cloud source coordinates also use two decimals in the UI", Equals(sourceFormatter.Invoke(null, [sourcePoint, "0.##"]), "(123456.79,-6.79,0)"));
            summary = $"Numeric summary verification: Pass ({passed} checks)";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Numeric summary verification: Fail after {passed} checks: {exception}";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return false;
        }
    }

    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "Boundary|Two-decimal presentation and exact untouched editor values. Routed WPF input is not Computer Use or SDK numerical qualification." };
        var originalCulture = CultureInfo.CurrentCulture;
        var passed = 0;
        Window? window = null;
        void Check(string name, bool condition)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
            if (!condition) throw new InvalidOperationException(name);
            passed++;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            window = new Window { Width = 540, Height = 760, Left = area.Left, Top = area.Top, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false, Background = (Brush)Application.Current.FindResource("ThreeD.PanelBrush"), Foreground = (Brush)Application.Current.FindResource("ThreeD.TextBrush") };
            foreach (var cultureName in new[] { "en-US", "de-DE" })
            {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                window.Language = XmlLanguage.GetLanguage(cultureName);
                var settings = new ThicknessStepProperties { MinimumThickness = -6.7890123456789, MaximumThickness = 127.56625366210938, MinimumValidSampleCount = 3 };
                var initial = (settings.MinimumThickness, settings.MaximumThickness);
                var descriptor = TypeDescriptor.GetProperties(LocalizedPropertyGridObject.Create(settings))[nameof(settings.MinimumThickness)]!;
                var expected = cultureName == "en-US" ? "-6.79" : "-6,79";
                var displayed = descriptor.Converter.ConvertToString(null, culture, initial.MinimumThickness);
                Check($"{cultureName} descriptor limits fractional digits", displayed == expected);
                Check($"{cultureName} untouched conversion retains every original bit", Equals(descriptor.Converter.ConvertFromString(null, culture, displayed!), initial.MinimumThickness));
                Check($"{cultureName} an explicit edit still accepts precise input", Equals(descriptor.Converter.ConvertFromString(null, culture, cultureName == "en-US" ? "4.12345" : "4,12345"), 4.12345));

                var grid = new RecipeStepPropertyGridHost { SelectedObject = settings, IsCompactPresentation = true, Margin = new Thickness(12) };
                var requests = new List<HeightLegendEditRequest>();
                var legend = new HeightLegendRangeBar { Minimum = initial.MinimumThickness, Maximum = initial.MaximumThickness,
                    DataMinimum = initial.MinimumThickness, DataMaximum = initial.MaximumThickness, Width = 140, Height = 240,
                    Background = window.Background, Foreground = window.Foreground, BorderBrush = Brushes.Gray,
                    EditCommand = new RelayCommand(value => requests.Add((HeightLegendEditRequest)value!)) };
                var content = new StackPanel();
                content.Children.Add(grid);
                content.Children.Add(legend);
                window.Content = content;
                window.Show();
                void Settle() => window.Dispatcher.Invoke(DispatcherPriority.Input, new Action(window.UpdateLayout));
                Settle();
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(window, reportPath + $".{cultureName}.monitor.txt");
                var editor = Descendants<TextBox>(grid).Single(item => AutomationProperties.GetAutomationId(item) == nameof(settings.MinimumThickness));
                Check($"{cultureName} actual WPG editor uses two-decimal text", editor.Text == expected);
                Check($"{cultureName} untouched Apply preserves exact raw settings", grid.CommitPendingEdit(out _) && initial == (settings.MinimumThickness, settings.MaximumThickness));
                editor.Text = cultureName == "en-US" ? "4.21" : "4,21";
                Check($"{cultureName} explicit WPG edit reaches the numeric property", grid.CommitPendingEdit(out _) && settings.MinimumThickness == 4.21 && settings.MaximumThickness == initial.MaximumThickness);
                editor.Text = "not-a-number";
                Check($"{cultureName} invalid text is rejected without replacing numeric state", !grid.CommitPendingEdit(out _) && settings.MinimumThickness == 4.21);
                editor.Text = cultureName == "en-US" ? "5.43" : "5,43";
                Check($"{cultureName} valid edit recovers after failure", grid.CommitPendingEdit(out _) && settings.MinimumThickness == 5.43 && settings.MaximumThickness == initial.MaximumThickness);

                var upper = (Thumb)legend.FindName("UpperHandle");
                var numeric = (TextBox)legend.FindName("NumericInput");
                var popup = (Popup)legend.FindName("NumericPopup");
                void Press(UIElement target, Key key)
                {
                    target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
                    Settle();
                }
                Check($"{cultureName} legend upper text is rounded", ((TextBlock)legend.FindName("UpperValueLabel")).Text == (cultureName == "en-US" ? "127.57" : "127,57"));
                Check($"{cultureName} legend tooltip is formatted text", upper.ToolTip is string tooltip && tooltip == (cultureName == "en-US" ? "127.57" : "127,57"));
                Press(upper, Key.Enter);
                Check($"{cultureName} popup starts with two decimals and keyboard focus", popup.IsOpen && numeric.IsKeyboardFocused && numeric.Text == (cultureName == "en-US" ? "127.57" : "127,57"));
                Press(numeric, Key.Enter);
                Check($"{cultureName} untouched popup confirmation sends no rounded replacement", !popup.IsOpen && requests.All(request => request.Kind is not HeightLegendEditKind.InputUpper) && legend.Maximum == initial.MaximumThickness);
                Press(upper, Key.Enter);
                legend.ErrorText = "Previous invalid numeric input";
                var beforeRecovery = requests.Count;
                Press(numeric, Key.Enter);
                Check($"{cultureName} returning to original display text closes after an input error", !popup.IsOpen && requests.Skip(beforeRecovery).Any(request => request.Kind == HeightLegendEditKind.Cancel));
                Check($"{cultureName} original-value recovery preserves exact raw range", legend.Maximum == initial.MaximumThickness && requests.Skip(beforeRecovery).All(request => request.Kind != HeightLegendEditKind.InputUpper));
                legend.ErrorText = string.Empty;
                Press(upper, Key.Enter);
                numeric.Text = "120";
                Press(numeric, Key.Enter);
                Check($"{cultureName} explicit popup text reaches its existing command", requests.Count(request => request is { Kind: HeightLegendEditKind.InputUpper, Text: "120" }) == 1);
                Press(upper, Key.Enter);
                numeric.Text = "119";
                var inputs = requests.Count(request => request.Kind == HeightLegendEditKind.InputUpper);
                Press(numeric, Key.Escape);
                Check($"{cultureName} popup cancellation sends no replacement", !popup.IsOpen && requests.Count(request => request.Kind == HeightLegendEditKind.InputUpper) == inputs && legend.Maximum == initial.MaximumThickness);
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(window).Bitmap, Path.ChangeExtension(reportPath, $".{cultureName}.png"));
            }
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var folder = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            using (var model = new HeightImageViewerViewModel(ThreeDLocalization.Shared, new SharedHeightCursorSession()))
            {
                var source = Path.Combine(folder, "range-source.C3D");
                using (var writer = new BinaryWriter(File.Create(source)))
                {
                    writer.Write(2); writer.Write(2);
                    foreach (var value in new[] { 1f, 2f, 3f, 4f }) writer.Write(value);
                }
                var load = model.EnsureSourceAsync(source, "precision-source", "raw-height", "frame.c3d-grid-index");
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (!load.IsCompleted && DateTime.UtcNow < deadline) window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                Check("Height image fixture is ready", load.IsCompletedSuccessfully && model.HasImage);
                Check("programmatic manual range preserves both exact endpoints", model.TryApplyManualRange(0.00123456789, 0.00234567891)
                    && model.HeightLegendMinimum == 0.00123456789 && model.HeightLegendMaximum == 0.00234567891);
                Check("small-range text still has at most two decimals", model.RangeMinimumText == "0" && model.RangeMaximumText == "0");
                Check("unchanged small-range Apply remains available", model.ApplyManualRangeCommand.CanExecute(null));
                model.ApplyManualRangeCommand.Execute(null);
                Check("unchanged small-range Apply keeps the full range", model.HeightLegendMinimum == 0.00123456789 && model.HeightLegendMaximum == 0.00234567891);
            }
            using (var workbench = new ToolWorkbenchViewModel(Path.Combine(folder, "recent.json")))
            {
                Check("precise recipe opens", workbench.TryOpenTeachingRecipe(WorkbenchSettingsVerificationFixture.CreatePrecisionRecipe(folder), out _));
                var draft = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
                var before = (draft.MinimumThickness, draft.MaximumThickness);
                var grid = new RecipeStepPropertyGridHost { SelectedObject = draft, IsCompactPresentation = true };
                window.Content = grid;
                window.Dispatcher.Invoke(DispatcherPriority.Input, new Action(window.UpdateLayout));
                var beforeParameters = string.Join(";", workbench.SelectedPipelineStep!.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"));
                var committed = grid.CommitPendingEdit(out var commitMessage);
                var applied = workbench.TryApplySelectedStepParameterDraft(out var applyMessage);
                lines.Add($"PrecisionApply|committed={committed}|applied={applied}|dirty={workbench.IsDirty}|before={before}|after={(draft.MinimumThickness, draft.MaximumThickness)}|parametersBefore={beforeParameters}|parametersAfter={string.Join(";", workbench.SelectedPipelineStep.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"))}|commit={commitMessage}|apply={applyMessage}");
                Check("real recipe untouched Apply retains its exact draft", committed && applied
                    && before == (draft.MinimumThickness, draft.MaximumThickness) && !workbench.IsDirty);
                var saved = Path.Combine(folder, "saved-precision.ov3d-recipe.json");
                Check("precise recipe saves", workbench.TrySaveTeachingRecipe(saved, out _));
                Check("precise recipe reopens", workbench.TryOpenTeachingRecipe(saved, out _));
                var restored = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
                Check("save/reopen preserves exact values without execution", before == (restored.MinimumThickness, restored.MaximumThickness)
                    && !workbench.IsSelectedStepPreviewRunning && !workbench.IsValidationSetRunning && !workbench.IsDirty);
            }
            summary = $"Numeric display verification: Pass ({passed} checks)";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Numeric display verification: Fail after {passed} checks: {exception}";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return false;
        }
        finally { window?.Close(); CultureInfo.CurrentCulture = originalCulture; }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
