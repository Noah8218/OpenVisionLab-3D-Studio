using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchRecipePartCollectionTrackingVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D recipe-part collection tracking verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: parameter collection add/remove subscription tracking"
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
            var callbacks = 0;
            using var coordinator = new ToolWorkbenchRecipePartEventCoordinator(
                source,
                (_, _) => callbacks++);
            coordinator.SubscribeStep(step);

            var addedParameter = new ToolWorkbenchParameterItem("added", "1");
            step.Parameters.Add(addedParameter);
            addedParameter.Value = "2";
            Check(
                "A parameter added after step subscription is tracked",
                callbacks == 1,
                $"callbacks={callbacks}");

            step.Parameters.Remove(addedParameter);
            var callbacksBeforeRemovalMutation = callbacks;
            addedParameter.Value = "3";
            Check(
                "A removed parameter is detached from the callback",
                callbacks == callbacksBeforeRemovalMutation,
                $"before={callbacksBeforeRemovalMutation};after={callbacks}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | verifier exception | {exception.GetType().Name}: {exception.Message}");
        }

        var succeeded = passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ToolWorkbenchRecipePartCollectionTracking|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
