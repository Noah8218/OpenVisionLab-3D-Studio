using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

public sealed class ViewerWorkspacePopoutBindingOwner(ICommand command)
{
    public ICommand SetSingleViewerLayoutCommand { get; } = command;
}

internal static class ViewerWorkspacePopoutDismissalVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        List<(string Name, bool Passed, string Detail)>? checks = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks = VerifyOnSta();
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
            checks.Add(("StaSetup", false, failure.GetBaseException().Message));
        }

        var passed = checks.All(check => check.Passed);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer workspace popout dismissal verification",
        };
        lines.AddRange(checks.Select(check =>
            $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}|{check.Detail}"));
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}");

        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = lines[^1];
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        return passed;
    }

    private static List<(string Name, bool Passed, string Detail)> VerifyOnSta()
    {
        var checks = new List<(string Name, bool Passed, string Detail)>();
        var command = new RecordingCommand();
        var dataContext = new ViewerWorkspacePopoutBindingOwner(command);
        var window = new ViewerWorkspacePopoutWindow
        {
            DataContext = dataContext
        };
        var dismissedCount = 0;
        window.Dismissed += (_, _) =>
        {
            dismissedCount++;
            var dismissalCommand = window.DismissedCommand;
            if (dismissalCommand?.CanExecute(null) == true)
            {
                dismissalCommand.Execute(null);
            }
        };

        window.Show();
        checks.Add((
            "xaml-dismissal-command-binding",
            ReferenceEquals(window.DismissedCommand, command)
                && BindingOperations.GetBinding(
                    window,
                    ViewerWorkspacePopoutWindow.DismissedCommandProperty)?.Path.Path
                    == "SetSingleViewerLayoutCommand",
            $"resolved={ReferenceEquals(window.DismissedCommand, command)};type={window.DismissedCommand?.GetType().FullName ?? "null"};path={BindingOperations.GetBinding(window, ViewerWorkspacePopoutWindow.DismissedCommandProperty)?.Path.Path ?? "null"}"));

        command.ResetMetrics();
        window.Close();
        checks.Add((
            "user-close-forwards-command-once",
            command.CanExecuteCount >= 1
                && command.ExecutionCount == 1
                && dismissedCount == 1
                && !window.IsVisible,
            $"canExecute={command.CanExecuteCount};executions={command.ExecutionCount};dismissed={dismissedCount};visible={window.IsVisible}"));

        window.CloseForOwner();
        checks.Add((
            "close-for-owner-does-not-forward-dismissal",
            command.ExecutionCount == 1
                && dismissedCount == 1
                && !window.IsVisible,
            $"executions={command.ExecutionCount};dismissed={dismissedCount};visible={window.IsVisible}"));

        var disabledCommand = new RecordingCommand { CanExecuteValue = false };
        var disabledWindow = new ViewerWorkspacePopoutWindow
        {
            DataContext = new ViewerWorkspacePopoutBindingOwner(disabledCommand)
        };
        var disabledDismissedCount = 0;
        disabledWindow.Dismissed += (_, _) =>
        {
            disabledDismissedCount++;
            var dismissalCommand = disabledWindow.DismissedCommand;
            if (dismissalCommand?.CanExecute(null) == true)
            {
                dismissalCommand.Execute(null);
            }
        };
        disabledCommand.ResetMetrics();
        disabledWindow.Show();
        disabledWindow.Close();
        checks.Add((
            "can-execute-is-respected",
            disabledCommand.CanExecuteCount >= 1
                && disabledCommand.ExecutionCount == 0
                && disabledDismissedCount == 1
                && !disabledWindow.IsVisible,
            $"canExecute={disabledCommand.CanExecuteCount};executions={disabledCommand.ExecutionCount};dismissed={disabledDismissedCount};visible={disabledWindow.IsVisible}"));
        disabledWindow.CloseForOwner();

        return checks;
    }

    private sealed class RecordingCommand : ICommand
    {
        public bool CanExecuteValue { get; init; } = true;

        public int CanExecuteCount { get; private set; }

        public int ExecutionCount { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            CanExecuteCount++;
            return CanExecuteValue;
        }

        public void Execute(object? parameter) => ExecutionCount++;

        public void ResetMetrics()
        {
            CanExecuteCount = 0;
            ExecutionCount = 0;
        }
    }
}
