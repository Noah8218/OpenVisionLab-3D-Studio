using System.ComponentModel;
using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchReferenceCatalogEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench reference catalog event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: catalog, reference-item, and mutation notifications"
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
            var owner = new ToolWorkbenchReferenceCatalogOwner(value => value ?? string.Empty);
            var propertyCallbacks = 0;
            var referencePropertyCallbacks = 0;
            var mutationCallbacks = 0;
            using var coordinator = new ToolWorkbenchReferenceCatalogEventCoordinator(
                owner,
                (_, _) => propertyCallbacks++,
                (_, _) => referencePropertyCallbacks++,
                (_, _) => mutationCallbacks++);

            owner.NewReferenceName = "Changed landmarks";
            Check(
                "Catalog notification reaches the Workbench callback",
                propertyCallbacks > 0,
                $"callbacks={propertyCallbacks}");

            owner.AddReferenceCommand.Execute(null);
            Check(
                "Reference mutation reaches the Workbench callback",
                mutationCallbacks == 1,
                $"callbacks={mutationCallbacks}");

            owner.References[0].Name = "Updated landmarks";
            Check(
                "Reference-item notification reaches the Workbench callback",
                referencePropertyCallbacks == 1,
                $"callbacks={referencePropertyCallbacks}");

            var callbacksBeforeDispose =
                propertyCallbacks + referencePropertyCallbacks + mutationCallbacks;
            coordinator.Dispose();
            coordinator.Dispose();
            owner.NewReferenceKind = "Updated kind";
            owner.References[0].Kind = "Updated item kind";
            owner.RemoveSelectedReferenceCommand.Execute(null);
            var callbacksAfterDispose =
                propertyCallbacks + referencePropertyCallbacks + mutationCallbacks;
            Check(
                "Dispose is idempotent and suppresses later notifications",
                callbacksAfterDispose == callbacksBeforeDispose,
                $"before={callbacksBeforeDispose};after={callbacksAfterDispose}");
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
        summary = $"ToolWorkbenchReferenceCatalogEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
