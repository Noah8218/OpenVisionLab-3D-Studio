using System.ComponentModel;
using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchCompatibleToolCatalogEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench Compatible Tool Catalog event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: PropertyChanged forwarding and disposal"
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
            var source = new PropertyChangedSource();
            var callbacks = 0;
            using var coordinator = new ToolWorkbenchCompatibleToolCatalogEventCoordinator(
                source,
                _ => callbacks++);

            source.Raise("CompatibleToolCatalogSummary");
            Check(
                "Compatible Tool Catalog notification reaches the Workbench callback",
                callbacks == 1,
                $"callbacks={callbacks}");

            coordinator.Dispose();
            coordinator.Dispose();
            source.Raise("SelectedToolProposedRouteDetail");
            Check(
                "Dispose is idempotent and suppresses later notifications",
                callbacks == 1,
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
        summary = $"ToolWorkbenchCompatibleToolCatalogEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private sealed class PropertyChangedSource : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void Raise(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
