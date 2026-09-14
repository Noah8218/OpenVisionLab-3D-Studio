using System.ComponentModel;
using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchSessionEventCoordinatorsVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Workbench session event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: Source Quality, Thickness Repeat Grid, and Viewer Workspace notifications"
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
            VerifyCoordinator(
                "Source Quality",
                (source, callback) => new ToolWorkbenchSourceQualityEventCoordinator(
                    source,
                    callback),
                Check);
            VerifyCoordinator(
                "Thickness Repeat Grid",
                (source, callback) => new ToolWorkbenchThicknessRepeatGridEventCoordinator(
                    source,
                    callback),
                Check);
            VerifyCoordinator(
                "Viewer Workspace",
                (source, callback) => new ToolWorkbenchViewerWorkspaceEventCoordinator(
                    source,
                    callback),
                Check);
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
        summary = $"ToolWorkbenchSessionEventCoordinators|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static void VerifyCoordinator(
        string name,
        Func<INotifyPropertyChanged, Action<PropertyChangedEventArgs>, IDisposable> create,
        Action<string, bool, string> check)
    {
        var source = new PropertyChangedSource();
        var callbacks = 0;
        using var coordinator = create(source, _ => callbacks++);

        source.Raise("PresentationState");
        check(
            $"{name} notification reaches the Workbench callback",
            callbacks == 1,
            $"callbacks={callbacks}");

        coordinator.Dispose();
        coordinator.Dispose();
        source.Raise("DisposedState");
        check(
            $"{name} disposal is idempotent and suppresses later notifications",
            callbacks == 1,
            $"callbacks={callbacks}");
    }

    private sealed class PropertyChangedSource : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void Raise(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
