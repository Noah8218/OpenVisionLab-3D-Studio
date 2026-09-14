using System.IO;
using System.Windows;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ResultsWorkspaceSectionBindingVerification
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
            "OpenVisionLab 3D ResultsWorkspace section binding verification",
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
        var view = new ResultsWorkspaceView();
        var command = new RecordingCommand();
        view.SelectSectionCommand = command;
        view.SetSection(ResultsWorkspaceSection.OutputCompare);
        checks.Add((
            "BoundCommandReceivesSection",
            command.ExecutionCount == 1
                && command.LastParameter is ResultsWorkspaceSection.OutputCompare,
            $"count={command.ExecutionCount};parameter={command.LastParameter}"));

        var disabled = new RecordingCommand { CanExecuteValue = false };
        view.SelectSectionCommand = disabled;
        view.SetSection(ResultsWorkspaceSection.Reports);
        checks.Add((
            "CanExecuteIsRespected",
            disabled.ExecutionCount == 0,
            $"count={disabled.ExecutionCount}"));

        view.SelectSectionCommand = null;
        view.SetSection(ResultsWorkspaceSection.RunRecord);
        checks.Add((
            "MissingCommandIsSafe",
            disabled.ExecutionCount == 0,
            $"count={disabled.ExecutionCount}"));

        view.ActiveSection = ResultsWorkspaceSection.Reports;
        checks.Add((
            "ActiveSectionDependencyPropertyRoundTrip",
            view.ActiveSection == ResultsWorkspaceSection.Reports,
            $"section={view.ActiveSection}"));

        return checks;
    }

    private sealed class RecordingCommand : ICommand
    {
        public bool CanExecuteValue { get; init; } = true;

        public int ExecutionCount { get; private set; }

        public object? LastParameter { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => CanExecuteValue;

        public void Execute(object? parameter)
        {
            ExecutionCount++;
            LastParameter = parameter;
        }
    }
}
