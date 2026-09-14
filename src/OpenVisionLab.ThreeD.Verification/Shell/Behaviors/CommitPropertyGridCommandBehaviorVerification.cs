using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.Behaviors;
using OpenVisionLab.ThreeD.Shell.PropertyGrid;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Verification.Shell.Behaviors;

internal static class CommitPropertyGridCommandBehaviorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
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
            "OpenVisionLab 3D CommitPropertyGridCommandBehavior verification",
        };
        lines.AddRange(checks.Select(check =>
            $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}|{check.Detail}"));
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
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
        if (Application.Current is null)
        {
            var app = new App();
            app.InitializeComponent();
        }
        var host = new RecipeStepPropertyGridHost();
        var button = new Button();
        var command = new RecordingCommand();
        var errorCommand = new RecordingCommand();
        CommitPropertyGridCommandBehavior.SetPropertyGrid(button, host);
        CommitPropertyGridCommandBehavior.SetCommand(button, command);
        CommitPropertyGridCommandBehavior.SetErrorCommand(button, errorCommand);
        CommitPropertyGridCommandBehavior.SetIsEnabled(button, true);

        var args = RaiseClick(button);
        checks.Add((
            "ValidCommitForwardsCommandOnce",
            command.ExecutionCount == 1
                && errorCommand.ExecutionCount == 0
                && args.Handled,
            $"command={command.ExecutionCount};error={errorCommand.ExecutionCount};handled={args.Handled}"));

        var disabled = new RecordingCommand { CanExecuteValue = false };
        CommitPropertyGridCommandBehavior.SetCommand(button, disabled);
        var disabledArgs = RaiseClick(button);
        checks.Add((
            "CanExecuteIsRespected",
            disabled.ExecutionCount == 0 && !disabledArgs.Handled,
            $"command={disabled.ExecutionCount};handled={disabledArgs.Handled}"));

        var first = new RecordingCommand();
        var second = new RecordingCommand();
        CommitPropertyGridCommandBehavior.SetCommand(button, first);
        CommitPropertyGridCommandBehavior.SetCommand(button, second);
        RaiseClick(button);
        checks.Add((
            "CommandReplacementUsesCurrentCommand",
            first.ExecutionCount == 0 && second.ExecutionCount == 1,
            $"first={first.ExecutionCount};second={second.ExecutionCount}"));

        CommitPropertyGridCommandBehavior.SetIsEnabled(button, false);
        CommitPropertyGridCommandBehavior.SetIsEnabled(button, true);
        RaiseClick(button);
        checks.Add((
            "RepeatedEnableDoesNotDuplicateHandler",
            second.ExecutionCount == 2,
            $"second={second.ExecutionCount}"));

        var noGridButton = new Button();
        var noGridCommand = new RecordingCommand();
        CommitPropertyGridCommandBehavior.SetCommand(noGridButton, noGridCommand);
        CommitPropertyGridCommandBehavior.SetIsEnabled(noGridButton, true);
        var noGridArgs = RaiseClick(noGridButton);
        checks.Add((
            "MissingPropertyGridDoesNotExecute",
            noGridCommand.ExecutionCount == 0 && !noGridArgs.Handled,
            $"command={noGridCommand.ExecutionCount};handled={noGridArgs.Handled}"));

        return checks;
    }

    private static RoutedEventArgs RaiseClick(Button button)
    {
        var args = new RoutedEventArgs(ButtonBase.ClickEvent, button);
        button.RaiseEvent(args);
        return args;
    }

    private sealed class RecordingCommand : ICommand
    {
        public bool CanExecuteValue { get; init; } = true;

        public int ExecutionCount { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => CanExecuteValue;

        public void Execute(object? parameter) => ExecutionCount++;
    }
}
