using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchOutputCompareEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench Output Compare event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: compare slot property/pin event routing and disposal"
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

        var session = new ToolWorkbenchOutputCompareSession();
        var propertyChangedCount = 0;
        var pinsChangedCount = 0;
        var coordinator = new ToolWorkbenchOutputCompareEventCoordinator(
            session,
            _ => propertyChangedCount++,
            () => pinsChangedCount++);

        try
        {
            session.CompareSlotAArtifactId = "artifact-a";
            Check(
                "PropertyChanged reaches the Workbench callback",
                propertyChangedCount > 0,
                $"propertyChangedCount={propertyChangedCount}");

            var propertyCountBeforePin = propertyChangedCount;
            session.TryPin("artifact-b");
            Check(
                "PinsChanged reaches the presentation callback",
                pinsChangedCount > 0 && propertyChangedCount > propertyCountBeforePin,
                $"pinsChangedCount={pinsChangedCount}; propertyChangedCount={propertyChangedCount}");

            coordinator.Dispose();
            coordinator.Dispose();
            var propertyChangedAfterDispose = propertyChangedCount;
            var pinsChangedAfterDispose = pinsChangedCount;
            session.CompareSlotCArtifactId = "artifact-c";
            Check(
                "Dispose is idempotent and suppresses both later callbacks",
                propertyChangedCount == propertyChangedAfterDispose
                && pinsChangedCount == pinsChangedAfterDispose,
                $"property={propertyChangedCount}; pins={pinsChangedCount}");
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
        summary = $"ToolWorkbenchOutputCompareEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
