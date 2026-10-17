using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.Behaviors;
using OpenVisionLab.ThreeD.Presentation.Behaviors;
using OpenVisionLab.ThreeD.Presentation.Controls;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Verification.Shell.Behaviors;

internal static class HeightImageViewerKeyboardBehaviorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var result = RunOnSta();
        result.Lines.Add($"Result: {(result.Passed == result.Total ? "Pass" : "Fail")} ({result.Passed}/{result.Total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, result.Lines);
        var passed = result.Passed == result.Total;
        summary = $"HeightImageViewerKeyboardBehavior|pass={passed}|checks={result.Passed}/{result.Total}|report={fullReportPath}";
        return passed;
    }

    private static VerificationResult RunOnSta()
    {
        VerificationResult? result = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = VerifyOnSta();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException(
                "Height Image keyboard behavior verification failed.",
                failure);
        }

        return result ?? throw new InvalidOperationException("Height Image keyboard behavior produced no result.");
    }

    private static VerificationResult VerifyOnSta()
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Height Image keyboard behavior verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var calls = new List<string>();
        var commands = new Dictionary<string, RecordingCommand>(StringComparer.Ordinal)
        {
            ["ApplyRoi"] = new("ApplyRoi", calls),
            ["CancelRoi"] = new("CancelRoi", calls),
            ["DeleteRoi"] = new("DeleteRoi", calls),
            ["Fit"] = new("Fit", calls),
            ["ActualPixels"] = new("ActualPixels", calls),
            ["ZoomIn"] = new("ZoomIn", calls),
            ["ZoomOut"] = new("ZoomOut", calls),
            ["AutoRange"] = new("AutoRange", calls)
        };
        var target = new Grid();
        HeightImageViewerKeyboardBehavior.SetApplyRoiCommand(target, commands["ApplyRoi"]);
        HeightImageViewerKeyboardBehavior.SetCancelRoiCommand(target, commands["CancelRoi"]);
        HeightImageViewerKeyboardBehavior.SetDeleteRoiCommand(target, commands["DeleteRoi"]);
        HeightImageViewerKeyboardBehavior.SetFitCommand(target, commands["Fit"]);
        HeightImageViewerKeyboardBehavior.SetActualPixelsCommand(target, commands["ActualPixels"]);
        HeightImageViewerKeyboardBehavior.SetZoomInCommand(target, commands["ZoomIn"]);
        HeightImageViewerKeyboardBehavior.SetZoomOutCommand(target, commands["ZoomOut"]);
        HeightImageViewerKeyboardBehavior.SetAutoRangeCommand(target, commands["AutoRange"]);
        HeightImageViewerKeyboardBehavior.SetIsEnabled(target, true);

        var gestures = new[]
        {
            (Key.Enter, ModifierKeys.None, "ApplyRoi"),
            (Key.Escape, ModifierKeys.None, "CancelRoi"),
            (Key.Delete, ModifierKeys.None, "DeleteRoi"),
            (Key.F, ModifierKeys.None, "Fit"),
            (Key.Add, ModifierKeys.None, "ZoomIn"),
            (Key.OemPlus, ModifierKeys.Shift, "ZoomIn"),
            (Key.Subtract, ModifierKeys.None, "ZoomOut"),
            (Key.OemMinus, ModifierKeys.Shift, "ZoomOut"),
            (Key.R, ModifierKeys.Control, "AutoRange")
        };
        foreach (var (key, modifiers, expected) in gestures)
        {
            calls.Clear();
            var executed = HeightImageViewerKeyboardBehavior.TryExecute(target, target, key, modifiers);
            Check(
                $"gesture-{key}-{modifiers}",
                executed && calls.SequenceEqual([expected]),
                $"executed={executed};calls={string.Join(",", calls)};expected={expected}");
        }

        calls.Clear();
        var unsupported = HeightImageViewerKeyboardBehavior.TryExecute(
            target,
            target,
            Key.F,
            ModifierKeys.Control);
        Check(
            "unsupported-modifier-is-ignored",
            !unsupported && calls.Count == 0,
            $"executed={unsupported};calls={calls.Count}");

        var textBox = new TextBox();
        var comboBox = new ComboBox();
        calls.Clear();
        var textBoxExecuted = HeightImageViewerKeyboardBehavior.TryExecute(
            target,
            textBox,
            Key.F,
            ModifierKeys.None);
        var comboBoxExecuted = HeightImageViewerKeyboardBehavior.TryExecute(
            target,
            comboBox,
            Key.F,
            ModifierKeys.None);
        Check(
            "text-input-controls-are-ignored",
            !textBoxExecuted && !comboBoxExecuted && calls.Count == 0,
            $"textBox={textBoxExecuted};comboBox={comboBoxExecuted};calls={calls.Count}");

        commands["Fit"].CanExecuteValue = false;
        var disabled = HeightImageViewerKeyboardBehavior.TryExecute(
            target,
            target,
            Key.F,
            ModifierKeys.None);
        Check(
            "can-execute-is-respected",
            !disabled && calls.Count == 0,
            $"executed={disabled};calls={calls.Count}");

        var profileCancel = new RecordingCommand("CancelProfile", calls);
        HeightImageViewerKeyboardBehavior.SetCancelProfileCommand(target, profileCancel);
        calls.Clear();
        var profileCancelled = HeightImageViewerKeyboardBehavior.TryExecute(target, target, Key.Escape, ModifierKeys.None);
        Check("profile-escape-uses-typed-cancel-without-roi-duplicate",
            profileCancelled && calls.SequenceEqual(["CancelProfile"]) && profileCancel.LastParameter is ViewerHeightProfileAction.Cancel,
            $"executed={profileCancelled};calls={string.Join(",", calls)};parameter={profileCancel.LastParameter}");
        calls.Clear();
        profileCancel.CanExecuteValue = false;
        var roiCancelled = HeightImageViewerKeyboardBehavior.TryExecute(target, target, Key.Escape, ModifierKeys.None);
        Check("unavailable-profile-cancel-keeps-existing-roi-escape", roiCancelled && calls.SequenceEqual(["CancelRoi"]),
            $"executed={roiCancelled};calls={string.Join(",", calls)}");
        profileCancel.CanExecuteValue = true;
        calls.Clear();
        var profileTextBoxIgnored = !HeightImageViewerKeyboardBehavior.TryExecute(target, textBox, Key.Escape, ModifierKeys.None);
        var profileComboBoxIgnored = !HeightImageViewerKeyboardBehavior.TryExecute(target, comboBox, Key.Escape, ModifierKeys.None);
        Check("active-profile-keeps-text-entry-escape-out-of-viewer", profileTextBoxIgnored && profileComboBoxIgnored && calls.Count == 0,
            $"textBoxIgnored={profileTextBoxIgnored};comboBoxIgnored={profileComboBoxIgnored};calls={calls.Count}");
        var modifiedEscape = HeightImageViewerKeyboardBehavior.TryExecute(target, target, Key.Escape, ModifierKeys.Control);
        Check("modified-escape-does-not-cancel-profile-or-roi", !modifiedEscape && calls.Count == 0,
            $"executed={modifiedEscape};calls={calls.Count}");

        HeightImageViewerKeyboardBehavior.SetIsEnabled(target, false);
        Check(
            "behavior-can-be-detached",
            !HeightImageViewerKeyboardBehavior.GetIsEnabled(target),
            "isEnabled=false");

        calls.Clear();
        Check("height-view-does-not-consume-view-kind-1", !HeightImageViewerKeyboardBehavior.TryExecute(target, target, Key.D1, ModifierKeys.None) && calls.Count == 0, "1 is the workspace 3D view shortcut");
        var viewerTarget = new Grid();
        ViewerKeyboardBehavior.SetIsEnabled(viewerTarget, true);
        Check("viewer-BAML-collection-is-initialized-before-get-object", viewerTarget.GetValue(ViewerKeyboardBehavior.BindingsProperty) is FreezableCollection<KeyBinding>, "per-view collection, no shared default");
        var shortcut = new RecordingCommand("ViewerPaneCount", calls);
        ViewerKeyboardBehavior.GetBindings(viewerTarget).Add(new KeyBinding(shortcut, Key.D4, ModifierKeys.Control) { CommandParameter = 4 });
        calls.Clear();
        Check("viewer-ctrl4-forwards-parameter", ViewerKeyboardBehavior.TryExecute(viewerTarget, viewerTarget, Key.D4, ModifierKeys.Control)
            && calls.SequenceEqual(["ViewerPaneCount"]) && Equals(shortcut.LastParameter, 4), "existing command, parameter 4");
        calls.Clear();
        Check("viewer-plain4-is-unassigned", !ViewerKeyboardBehavior.TryExecute(viewerTarget, viewerTarget, Key.D4, ModifierKeys.None) && calls.Count == 0, "layout requires Ctrl");
        var editor = new TextBox();
        var editorChild = new Border();
        editor.Template = new System.Windows.Controls.ControlTemplate(typeof(TextBox)) { VisualTree = new FrameworkElementFactory(typeof(Border)) };
        editor.ApplyTemplate();
        editorChild = (Border)System.Windows.Media.VisualTreeHelper.GetChild(editor, 0);
        Check("viewer-nested-editor-keeps-number-input", !ViewerKeyboardBehavior.TryExecute(viewerTarget, editorChild, Key.D4, ModifierKeys.Control)
            && calls.Count == 0, "template child of editor");
        Check("viewer-combo-keeps-selection-keys", !ViewerKeyboardBehavior.TryExecute(viewerTarget, new ComboBox(), Key.D4, ModifierKeys.Control), "combo popup/editor owns keys");
        shortcut.CanExecuteValue = false;
        Check("viewer-disabled-command-does-not-execute", !ViewerKeyboardBehavior.TryExecute(viewerTarget, viewerTarget, Key.D4, ModifierKeys.Control) && calls.Count == 0, "CanExecute false");

        var legend = new HeightLegendRangeBar();
        legend.Measure(new Size(160, 400));
        legend.Arrange(new Rect(0, 0, 160, 400));
        legend.UpdateLayout();
        var legendHandle = (Thumb)legend.FindName("UpperHandle");
        legendHandle.ApplyTemplate();
        var legendChild = System.Windows.Media.VisualTreeHelper.GetChild(legendHandle, 0);
        var password = new PasswordBox();
        var slider = new Slider();
        comboBox.Template = new ControlTemplate(typeof(ComboBox)) { VisualTree = new FrameworkElementFactory(typeof(Border)) };
        comboBox.ApplyTemplate();
        var comboChild = System.Windows.Media.VisualTreeHelper.GetChild(comboBox, 0);
        foreach (var (name, source) in new (string, DependencyObject)[]
                 { ("text-template", editorChild), ("combo-template", comboChild), ("password", password), ("slider", slider), ("legend-handle", legendHandle), ("legend-template", legendChild) })
        {
            calls.Clear();
            var ignored = new[] { Key.Enter, Key.Escape, Key.Delete, Key.F, Key.Add, Key.Subtract }
                .All(key => !HeightImageViewerKeyboardBehavior.TryExecute(target, source, key, ModifierKeys.None));
            Check($"height-{name}-owns-editing-keys", ignored && calls.Count == 0, $"ignored={ignored};calls={string.Join(',', calls)}");
        }
        return new VerificationResult(lines, passed, total);
    }

    private sealed class RecordingCommand(string name, List<string> calls) : ICommand
    {
        public bool CanExecuteValue { get; set; } = true;
        public object? LastParameter { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => CanExecuteValue;

        public void Execute(object? parameter)
        {
            LastParameter = parameter;
            calls.Add(name);
        }
    }

    private sealed record VerificationResult(List<string> Lines, int Passed, int Total);
}
