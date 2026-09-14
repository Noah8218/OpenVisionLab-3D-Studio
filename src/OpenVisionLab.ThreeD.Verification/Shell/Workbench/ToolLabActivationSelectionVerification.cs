using System.IO;
using System.Threading;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolLabActivationSelectionVerification
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
                checks = VerifyOnSta(reportPath);
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
            "OpenVisionLab 3D Tool Lab activation selection verification",
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

    private static List<(string Name, bool Passed, string Detail)> VerifyOnSta(string reportPath)
    {
        var checks = new List<(string Name, bool Passed, string Detail)>();
        var fixtureRoot = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
            $"tool-lab-activation-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureRoot);

        var workbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent.json"));
        var filterTool = workbench.Tools.Single(tool => tool.Id == "filter");
        var thicknessTool = workbench.Tools.Single(tool => tool.Id == "thickness");
        var filterStep = new ToolWorkbenchPipelineStepItem(
            "filter-step",
            filterTool,
            string.Empty,
            "filter-output");
        var thicknessStep = new ToolWorkbenchPipelineStepItem(
            "thickness-step",
            thicknessTool,
            string.Empty,
            "thickness-output");
        workbench.PipelineSteps.Add(filterStep);
        workbench.PipelineSteps.Add(thicknessStep);
        workbench.SelectPipelineStep(thicknessStep.Id);

        var selectorCalls = new List<string>();
        bool SelectPipelineStep(string stepId)
        {
            selectorCalls.Add(stepId);
            return workbench.SelectPipelineStep(stepId);
        }

        using var window = new ProbeToolLabWindow(workbench, filterStep, SelectPipelineStep);
        checks.Add((
            "constructor-activation-uses-selector",
            selectorCalls.SequenceEqual([filterStep.Id])
                && ReferenceEquals(workbench.SelectedPipelineStep, filterStep),
            $"calls={string.Join(",", selectorCalls)};selected={workbench.SelectedPipelineStep?.Id}"));

        window.ActivateLabStep();
        checks.Add((
            "already-selected-step-does-not-repeat",
            selectorCalls.Count == 1,
            $"calls={selectorCalls.Count}"));

        workbench.SelectPipelineStep(thicknessStep.Id);
        window.ActivateLabStep();
        checks.Add((
            "reactivation-restores-exact-step",
            selectorCalls.SequenceEqual([filterStep.Id, filterStep.Id])
                && ReferenceEquals(workbench.SelectedPipelineStep, filterStep),
            $"calls={string.Join(",", selectorCalls)};selected={workbench.SelectedPipelineStep?.Id}"));

        window.Dispose();
        checks.Add((
            "dispose-is-idempotent",
            !window.IsVisible,
            $"isVisible={window.IsVisible}"));

        return checks;
    }

    private sealed class ProbeToolLabWindow(
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        Func<string, bool> selectPipelineStep)
        : ToolLabWindowBase(
            workbench,
            step,
            selectPipelineStep,
            "filter",
            "Probe Tool Lab requires a Filter step.")
    {
        public override void RefreshViews()
        {
        }
    }
}
