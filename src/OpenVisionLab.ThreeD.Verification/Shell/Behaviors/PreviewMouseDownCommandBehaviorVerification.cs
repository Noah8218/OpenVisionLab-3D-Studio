using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.Behaviors;

namespace OpenVisionLab.ThreeD.Verification.Shell.Behaviors;

internal static class PreviewMouseDownCommandBehaviorVerification
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
        summary = $"PreviewMouseDownCommandBehavior|pass={passed}|checks={result.Passed}/{result.Total}|report={fullReportPath}";
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
                "PreviewMouseDown command behavior verification failed.",
                failure);
        }

        return result ?? throw new InvalidOperationException("PreviewMouseDown command behavior produced no result.");
    }

    private static VerificationResult VerifyOnSta()
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D PreviewMouseDown command behavior verification",
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

        var calls = new List<object?>();
        var target = new Grid();
        var mainCommand = new RecordingCommand(calls);
        PreviewMouseDownCommandBehavior.SetCommand(target, mainCommand);
        PreviewMouseDownCommandBehavior.SetCommandParameter(target, "viewer.main");
        PreviewMouseDownCommandBehavior.SetIsEnabled(target, true);

        Check(
            "main-slot-command-is-attached",
            PreviewMouseDownCommandBehavior.GetIsEnabled(target)
                && ReferenceEquals(PreviewMouseDownCommandBehavior.GetCommand(target), mainCommand),
            $"isEnabled={PreviewMouseDownCommandBehavior.GetIsEnabled(target)}");
        Check(
            "main-slot-parameter-is-forwarded",
            Equals(PreviewMouseDownCommandBehavior.GetCommandParameter(target), "viewer.main"),
            $"parameter={PreviewMouseDownCommandBehavior.GetCommandParameter(target)}");

        var mainExecuted = PreviewMouseDownCommandBehavior.TryExecute(target);
        Check(
            "main-slot-command-executes",
            mainExecuted && calls.SequenceEqual(["viewer.main"]),
            $"executed={mainExecuted};calls={string.Join(",", calls)}");

        calls.Clear();
        PreviewMouseDownCommandBehavior.SetCommandParameter(target, "viewer.auxiliary");
        var auxiliaryExecuted = PreviewMouseDownCommandBehavior.TryExecute(target);
        Check(
            "auxiliary-slot-parameter-is-forwarded",
            auxiliaryExecuted && calls.SequenceEqual(["viewer.auxiliary"]),
            $"executed={auxiliaryExecuted};calls={string.Join(",", calls)}");

        mainCommand.CanExecuteValue = false;
        calls.Clear();
        var blocked = PreviewMouseDownCommandBehavior.TryExecute(target);
        Check(
            "can-execute-is-respected",
            !blocked && calls.Count == 0,
            $"executed={blocked};calls={calls.Count}");

        PreviewMouseDownCommandBehavior.SetCommand(target, null);
        Check(
            "missing-command-is-ignored",
            !PreviewMouseDownCommandBehavior.TryExecute(target) && calls.Count == 0,
            $"calls={calls.Count}");

        var disabledTarget = new Border();
        var disabledCommand = new RecordingCommand(calls);
        PreviewMouseDownCommandBehavior.SetCommand(disabledTarget, disabledCommand);
        PreviewMouseDownCommandBehavior.SetCommandParameter(disabledTarget, "viewer.main");
        PreviewMouseDownCommandBehavior.SetIsEnabled(disabledTarget, true);
        PreviewMouseDownCommandBehavior.SetIsEnabled(disabledTarget, false);
        Check(
            "behavior-can-be-detached",
            !PreviewMouseDownCommandBehavior.GetIsEnabled(disabledTarget),
            "isEnabled=false");

        return new VerificationResult(lines, passed, total);
    }

    private sealed class RecordingCommand(List<object?> calls) : ICommand
    {
        public bool CanExecuteValue { get; set; } = true;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => CanExecuteValue;

        public void Execute(object? parameter) => calls.Add(parameter);
    }

    private sealed record VerificationResult(List<string> Lines, int Passed, int Total);
}
