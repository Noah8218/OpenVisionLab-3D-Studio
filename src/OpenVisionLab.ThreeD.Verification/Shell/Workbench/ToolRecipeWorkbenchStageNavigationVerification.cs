using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

public sealed class ToolRecipeWorkbenchStageNavigationBindingOwner(ICommand command)
{
    public ICommand SelectWorkspaceCommand { get; } = command;
}

internal static class ToolRecipeWorkbenchStageNavigationVerification
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
            "OpenVisionLab 3D Workbench stage-navigation binding verification",
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
        if (Application.Current is null)
        {
            var app = new App();
            app.InitializeComponent();
        }

        var command = new RecordingCommand();
        var owner = new ToolRecipeWorkbenchStageNavigationBindingOwner(command);
        using var view = new ToolRecipeWorkbenchView
        {
            DataContext = owner
        };

        checks.Add((
            "workspace-command-binding",
            ReferenceEquals(view.WorkspaceSelectionCommand, command)
                && BindingOperations.GetBinding(
                    view,
                    ToolRecipeWorkbenchView.WorkspaceSelectionCommandProperty)?.Path.Path
                    == "DataContext.SelectWorkspaceCommand",
            $"resolved={ReferenceEquals(view.WorkspaceSelectionCommand, command)};path={BindingOperations.GetBinding(view, ToolRecipeWorkbenchView.WorkspaceSelectionCommandProperty)?.Path.Path ?? "null"}"));

        view.ActivateFlowMap();
        checks.Add((
            "flow-map-forwards-inspect-mode",
            command.CanExecuteCount == 1
                && command.ExecutionCount == 1
                && command.LastParameter is ShellWorkspaceMode.Inspect,
            $"canExecute={command.CanExecuteCount};executions={command.ExecutionCount};parameter={command.LastParameter ?? "null"}"));

        command.ResetMetrics();
        command.CanExecuteValue = false;
        view.ActivateProblems();
        checks.Add((
            "can-execute-suppresses-stage-change",
            command.CanExecuteCount == 1
                && command.ExecutionCount == 0,
            $"canExecute={command.CanExecuteCount};executions={command.ExecutionCount};parameter={command.LastParameter ?? "null"}"));

        command.CanExecuteValue = true;
        command.ResetMetrics();
        view.ActivateRunRecord();
        checks.Add((
            "run-record-forwards-review-mode",
            command.CanExecuteCount == 1
                && command.ExecutionCount == 1
                && command.LastParameter is ShellWorkspaceMode.Review,
            $"canExecute={command.CanExecuteCount};executions={command.ExecutionCount};parameter={command.LastParameter ?? "null"}"));

        return checks;
    }

    private sealed class RecordingCommand : ICommand
    {
        public bool CanExecuteValue { get; set; } = true;

        public int CanExecuteCount { get; private set; }

        public int ExecutionCount { get; private set; }

        public object? LastParameter { get; private set; }

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

        public void Execute(object? parameter)
        {
            ExecutionCount++;
            LastParameter = parameter;
        }

        public void ResetMetrics()
        {
            CanExecuteCount = 0;
            ExecutionCount = 0;
            LastParameter = null;
        }
    }
}
