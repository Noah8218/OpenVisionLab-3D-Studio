using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchEditorSessionEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench editor-session event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: selection and PropertyGrid session routing, idempotent disposal, and callback suppression"
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

        var workspaceSelection = new InspectionWorkspaceSelectionSession();
        var stepPropertySession = new ToolWorkbenchStepPropertySession();
        var selectionChangedCount = 0;
        var propertyChangedCount = 0;
        var coordinator = new ToolWorkbenchEditorSessionEventCoordinator(
            workspaceSelection,
            _ => selectionChangedCount++,
            stepPropertySession,
            _ => propertyChangedCount++);

        try
        {
            workspaceSelection.SelectInput("input-1");
            Check(
                "Selection session reaches the Workbench callback",
                selectionChangedCount == 1,
                $"selectionChangedCount={selectionChangedCount}");

            stepPropertySession.SetStatus("draft status changed");
            Check(
                "PropertyGrid session reaches the Workbench callback",
                propertyChangedCount == 1,
                $"propertyChangedCount={propertyChangedCount}");

            coordinator.Dispose();
            coordinator.Dispose();
            var selectionCountAfterDispose = selectionChangedCount;
            var propertyCountAfterDispose = propertyChangedCount;
            workspaceSelection.SelectOutput("output-1");
            stepPropertySession.SetStatus("ignored after dispose");
            Check(
                "Dispose is idempotent and suppresses both later callbacks",
                selectionChangedCount == selectionCountAfterDispose
                && propertyChangedCount == propertyCountAfterDispose,
                $"selection={selectionChangedCount}; property={propertyChangedCount}");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            coordinator.Dispose();
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
        summary = $"ToolWorkbenchEditorSessionEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
