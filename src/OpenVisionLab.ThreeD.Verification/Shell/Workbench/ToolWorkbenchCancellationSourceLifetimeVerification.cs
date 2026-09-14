using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchCancellationSourceLifetimeVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Workbench cancellation-source lifetime verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: shared cancel-then-dispose policy used by execution owners"
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

        var callbackCount = 0;
        var cancellation = new CancellationTokenSource();
        using var registration = cancellation.Token.Register(() => callbackCount++);
        ToolWorkbenchCancellationSourceLifetime.CancelAndDispose(cancellation);
        Check(
            "A live source is cancelled and its callback runs once",
            cancellation.IsCancellationRequested && callbackCount == 1,
            $"cancelled={cancellation.IsCancellationRequested}|callbacks={callbackCount}");

        var disposedSource = new CancellationTokenSource();
        disposedSource.Dispose();
        var disposedSourceAccepted = true;
        try
        {
            ToolWorkbenchCancellationSourceLifetime.CancelAndDispose(disposedSource);
        }
        catch
        {
            disposedSourceAccepted = false;
        }

        Check(
            "An already-disposed source is tolerated",
            disposedSourceAccepted,
            $"accepted={disposedSourceAccepted}");

        var disposedTokenObserved = false;
        try
        {
            _ = cancellation.Token.WaitHandle;
        }
        catch (ObjectDisposedException)
        {
            disposedTokenObserved = true;
        }

        ToolWorkbenchCancellationSourceLifetime.CancelAndDispose(null);
        Check(
            "The helper releases the live source and accepts null",
            disposedTokenObserved,
            $"sourceDisposed={disposedTokenObserved}|nullAccepted=True");

        summary = $"ToolWorkbenchCancellationSourceLifetime|pass={passed == total}|checks={passed}/{total}";
        lines.Add($"Result: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary += $"|report={fullReportPath}";
        return passed == total;
    }
}
