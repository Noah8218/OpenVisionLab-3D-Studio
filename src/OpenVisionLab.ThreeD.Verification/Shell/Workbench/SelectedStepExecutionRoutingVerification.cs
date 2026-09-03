using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class SelectedStepExecutionRoutingVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>();
        var total = 0;
        var passed = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        var firstStep = CreateStep("first", outputEnabled: true);
        ToolWorkbenchPipelineStepItem? selectedStep = firstStep;
        var firstPreview = 0;
        var firstPublish = 0;
        var firstCancel = 0;
        var firstRefresh = 0;
        var firstRunning = false;
        var firstCanPreview = true;
        var firstCanPublish = true;
        var secondPreview = 0;
        var secondRefresh = 0;

        var routes = new Dictionary<string, ToolWorkbenchSelectedStepExecutionRoute>(
            StringComparer.Ordinal)
        {
            ["first"] = new(
                () =>
                {
                    firstPreview++;
                    return Task.FromResult(true);
                },
                () => firstCanPreview,
                () => firstPublish++,
                () => firstCanPublish,
                () => firstCancel++,
                () => firstRunning,
                () => firstRefresh++),
            ["second"] = new(
                () =>
                {
                    secondPreview++;
                    return Task.FromResult(true);
                },
                () => true,
                () => { },
                () => true,
                () => { },
                () => false,
                () => secondRefresh++)
        };
        var owner = new ToolWorkbenchSelectedStepExecutionOwner(
            () => selectedStep,
            routes);

        var previewed = owner.PreviewAsync().GetAwaiter().GetResult();
        Check(
            "selected Preview dispatches exactly once",
            previewed && firstPreview == 1 && secondPreview == 0,
            $"result={previewed};first={firstPreview};second={secondPreview}");

        owner.Publish();
        owner.Cancel();
        Check(
            "Publish and Cancel dispatch only to the selected route",
            firstPublish == 1 && firstCancel == 1,
            $"publish={firstPublish};cancel={firstCancel}");

        firstRunning = true;
        owner.RefreshCommandStates();
        Check(
            "running state drives the selected Cancel command",
            owner.IsRunning && owner.CancelCommand.CanExecute(null),
            $"running={owner.IsRunning};canCancel={owner.CancelCommand.CanExecute(null)}");

        firstCanPreview = false;
        firstCanPublish = false;
        owner.RefreshCommandStates();
        Check(
            "route predicates drive Preview and Publish commands",
            !owner.PreviewCommand.CanExecute(null)
            && !owner.PublishCommand.CanExecute(null),
            $"preview={owner.PreviewCommand.CanExecute(null)};publish={owner.PublishCommand.CanExecute(null)}");

        owner.RefreshSelectedStepState();
        Check(
            "selected state refresh dispatches exactly once",
            firstRefresh == 1 && secondRefresh == 0,
            $"first={firstRefresh};second={secondRefresh}");

        selectedStep = CreateStep("second", outputEnabled: false);
        owner.RefreshSelectedStepState();
        Check(
            "output-disabled step fails closed without route dispatch",
            selectedStep.State == "Disabled"
            && secondRefresh == 0
            && !owner.CanPreview()
            && !owner.CanPublish(),
            $"state={selectedStep.State};refresh={secondRefresh};preview={owner.CanPreview()};publish={owner.CanPublish()}");

        selectedStep = CreateStep("unknown", outputEnabled: true);
        var unknownPreview = owner.PreviewAsync().GetAwaiter().GetResult();
        owner.Publish();
        owner.Cancel();
        owner.RefreshSelectedStepState();
        Check(
            "unknown ToolId performs no fallback execution",
            !unknownPreview
            && firstPreview == 1
            && firstPublish == 1
            && firstCancel == 1
            && firstRefresh == 1
            && secondPreview == 0
            && secondRefresh == 0,
            $"preview={unknownPreview};first={firstPreview}/{firstPublish}/{firstCancel}/{firstRefresh};second={secondPreview}/{secondRefresh}");

        selectedStep = null;
        Check(
            "no selection disables all selected-step commands",
            !owner.PreviewCommand.CanExecute(null)
            && !owner.PublishCommand.CanExecute(null)
            && !owner.CancelCommand.CanExecute(null),
            $"preview={owner.PreviewCommand.CanExecute(null)};publish={owner.PublishCommand.CanExecute(null)};cancel={owner.CancelCommand.CanExecute(null)}");

        var passedAll = total > 0 && passed == total;
        lines.Add($"SelectedStepExecutionRouting|{(passedAll ? "PASS" : "FAIL")}|checks={passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = lines[^1];
        return passedAll;
    }

    private static ToolWorkbenchPipelineStepItem CreateStep(
        string toolId,
        bool outputEnabled)
    {
        var tool = new ToolWorkbenchToolItem(
            "Verification",
            toolId,
            toolId,
            1,
            "Input",
            "Output",
            "Selected-step routing fixture.",
            []);
        return new ToolWorkbenchPipelineStepItem(
            $"step.{toolId}",
            tool,
            "input.fixture",
            $"output.{toolId}",
            outputEnabled: outputEnabled);
    }
}
