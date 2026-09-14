using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.Behaviors;

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
            (Key.D1, ModifierKeys.None, "ActualPixels"),
            (Key.NumPad1, ModifierKeys.None, "ActualPixels"),
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

        HeightImageViewerKeyboardBehavior.SetIsEnabled(target, false);
        Check(
            "behavior-can-be-detached",
            !HeightImageViewerKeyboardBehavior.GetIsEnabled(target),
            "isEnabled=false");

        return new VerificationResult(lines, passed, total);
    }

    private sealed class RecordingCommand(string name, List<string> calls) : ICommand
    {
        public bool CanExecuteValue { get; set; } = true;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => CanExecuteValue;

        public void Execute(object? parameter) => calls.Add(name);
    }

    private sealed record VerificationResult(List<string> Lines, int Passed, int Total);
}
