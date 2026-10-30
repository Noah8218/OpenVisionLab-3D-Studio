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

        var area = SystemParameters.WorkArea;
        using var bounds = new ProbeToolLabWindow(workbench, filterStep, SelectPipelineStep)
        {
            Width = 1600, Height = 920, MinWidth = 1120, MinHeight = 700,
            Left = area.Left + 156, Top = area.Top + 156, WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false
        };
        System.Windows.Shell.WindowChrome.SetWindowChrome(bounds, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 56, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false
        });
        void Settle() => bounds.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(bounds.UpdateLayout));
        bool Contained() => bounds.Left >= area.Left - 1 && bounds.Top >= area.Top - 1
            && bounds.Left + bounds.ActualWidth <= area.Right + 1 && bounds.Top + bounds.ActualHeight <= area.Bottom + 1;
        try
        {
            bounds.Show();Settle();
            checks.Add(("normal-lab-is-inside-work-area", Contained(), $"area={area};window={bounds.Left},{bounds.Top},{bounds.ActualWidth},{bounds.ActualHeight}"));
            var normal = bounds.RestoreBounds;
            bounds.WindowState = WindowState.Maximized;Settle();
            var handle = new System.Windows.Interop.WindowInteropHelper(bounds).Handle;
            // Read-only native metadata: the maximized client must stop above the taskbar.
            checks.Add(("maximized-lab-is-inside-work-area", bounds.ActualWidth <= area.Width + 1 && bounds.ActualHeight <= area.Height + 1,
                $"area={area};actual={bounds.ActualWidth},{bounds.ActualHeight};hwnd={handle}"));
            bounds.WindowState = WindowState.Normal;Settle();
            checks.Add(("restored-lab-is-inside-work-area", Contained() && Math.Abs(bounds.Width - normal.Width) <= 1,
                $"normal={normal};restored={bounds.Left},{bounds.Top},{bounds.ActualWidth},{bounds.ActualHeight}"));
            bounds.Width = 1280;bounds.Height = 760;Settle();
            checks.Add(("compact-lab-can-resize", Math.Abs(bounds.ActualWidth - 1280) <= 1 && Math.Abs(bounds.ActualHeight - 760) <= 1,
                $"actual={bounds.ActualWidth},{bounds.ActualHeight}"));
        }
        finally { bounds.Close(); }

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
