using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.Behaviors;

namespace OpenVisionLab.ThreeD.Verification.Shell.Behaviors;

internal static class PasswordChangedCommandBehaviorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        List<(string Name, bool Passed)>? checks = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks =
                [
                    ("ValidPasswordIsForwarded", VerifyValidPasswordIsForwarded()),
                    ("EmptyPasswordIsForwarded", VerifyEmptyPasswordIsForwarded()),
                    ("CanExecuteIsRespected", VerifyCanExecuteIsRespected()),
                    ("CommandReplacementDetachesOldHandler", VerifyCommandReplacementDetachesOldHandler()),
                    ("CommandClearingDetachesHandler", VerifyCommandClearingDetachesHandler())
                ];
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        checks ??= [];
        if (failure is not null)
        {
            checks.Add(("StaSetup", false));
        }

        var passed = checks.All(check => check.Passed);
        var lines = new List<string>
        {
            "OpenVisionLab 3D PasswordChangedCommandBehavior verification"
        };
        lines.AddRange(checks.Select(check =>
            $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}"));
        if (failure is not null)
        {
            lines.Add($"Failure={failure.GetBaseException().Message}");
        }
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        summary = lines[^1];
        return passed;
    }

    private static bool VerifyValidPasswordIsForwarded()
    {
        var command = new RecordingCommand();
        var input = CreateInput(command);
        input.Password = "valid-value";
        return command.Parameters.SequenceEqual(["valid-value"]);
    }

    private static bool VerifyEmptyPasswordIsForwarded()
    {
        var command = new RecordingCommand();
        var input = CreateInput(command);
        input.Password = "temporary";
        input.Password = string.Empty;
        return command.Parameters.SequenceEqual(["temporary", string.Empty]);
    }

    private static bool VerifyCanExecuteIsRespected()
    {
        var command = new RecordingCommand { CanExecuteValue = false };
        var input = CreateInput(command);
        input.Password = "blocked";
        return command.Parameters.Count == 0;
    }

    private static bool VerifyCommandReplacementDetachesOldHandler()
    {
        var first = new RecordingCommand();
        var second = new RecordingCommand();
        var input = CreateInput(first);
        PasswordChangedCommandBehavior.SetCommand(input, second);
        input.Password = "replacement";
        return first.Parameters.Count == 0
            && second.Parameters.SequenceEqual(["replacement"]);
    }

    private static bool VerifyCommandClearingDetachesHandler()
    {
        var command = new RecordingCommand();
        var input = CreateInput(command);
        PasswordChangedCommandBehavior.SetCommand(input, null);
        input.Password = "detached";
        return command.Parameters.Count == 0;
    }

    private static PasswordBox CreateInput(RecordingCommand command)
    {
        var input = new PasswordBox();
        PasswordChangedCommandBehavior.SetCommand(input, command);
        return input;
    }

    private sealed class RecordingCommand : ICommand
    {
        public bool CanExecuteValue { get; init; } = true;

        public List<string> Parameters { get; } = [];

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => CanExecuteValue;

        public void Execute(object? parameter) => Parameters.Add(parameter as string ?? string.Empty);
    }
}
