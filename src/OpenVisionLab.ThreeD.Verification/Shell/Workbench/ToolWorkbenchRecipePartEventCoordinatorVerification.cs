using System.ComponentModel;
using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchRecipePartEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench recipe-part event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: source, step, and parameter notification ownership"
        };
        var passed = 0;
        var total = 0;
        Exception? failure = null;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            var source = new ToolWorkbenchSourceItem(
                "source",
                "Source",
                "C3D",
                "raw-height",
                "frame",
                "source.c3d");
            var tool = new ToolWorkbenchToolItem(
                "Test",
                "Test tool",
                "test-tool",
                0,
                string.Empty,
                "test-output",
                "Verification tool",
                []);
            var step = new ToolWorkbenchPipelineStepItem(
                "step",
                tool,
                string.Empty,
                "output");
            var parameter = new ToolWorkbenchParameterItem("threshold", "1");
            step.Parameters.Add(parameter);
            var callbacks = 0;
            using var coordinator = new ToolWorkbenchRecipePartEventCoordinator(
                source,
                (_, _) => callbacks++);

            source.Name = "Changed source";
            Check(
                "Source notification reaches the Workbench callback",
                callbacks == 1,
                $"callbacks={callbacks}");

            coordinator.SubscribeStep(step);
            step.Id = "changed-step";
            parameter.Value = "2";
            Check(
                "Step and parameter notifications reach the Workbench callback",
                callbacks == 3,
                $"callbacks={callbacks}");

            coordinator.UnsubscribeStep(step);
            var callbacksBeforeUnsubscribeCheck = callbacks;
            step.Id = "unsubscribed-step";
            parameter.Value = "3";
            Check(
                "UnsubscribeStep detaches the step and its parameters",
                callbacks == callbacksBeforeUnsubscribeCheck,
                $"before={callbacksBeforeUnsubscribeCheck};after={callbacks}");

            coordinator.Dispose();
            coordinator.Dispose();
            source.Path = "disposed.c3d";
            Check(
                "Dispose is idempotent and suppresses later source notifications",
                callbacks == callbacksBeforeUnsubscribeCheck,
                $"callbacks={callbacks}");
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is not null)
        {
            lines.Add($"FAIL | verifier exception | {failure.GetType().Name}: {failure.Message}");
        }

        var succeeded = failure is null && passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ToolWorkbenchRecipePartEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
