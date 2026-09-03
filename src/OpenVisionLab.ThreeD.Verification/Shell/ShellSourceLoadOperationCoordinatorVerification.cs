using System.IO;
using OpenVisionLab.ThreeD.Shell.Coordination;

namespace OpenVisionLab.ThreeD.Verification.Shell;

internal static class ShellSourceLoadOperationCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Shell source-load operation coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}"
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

        using (var coordinator = new ShellSourceLoadOperationCoordinator())
        {
            using var first = coordinator.Begin();
            Check(
                "first operation starts current and identified",
                first.IsCurrent && first.Generation == 1 && !first.IsCancellationRequested,
                $"current={first.IsCurrent};generation={first.Generation};cancelled={first.IsCancellationRequested}");

            using var second = coordinator.Begin();
            Check(
                "new operation cancels and supersedes the previous one",
                first.IsCancellationRequested
                && !first.IsCurrent
                && second.IsCurrent
                && second.Generation == 2,
                $"firstCancelled={first.IsCancellationRequested};firstCurrent={first.IsCurrent};secondCurrent={second.IsCurrent};secondGeneration={second.Generation}");

            coordinator.CancelCurrent();
            Check(
                "CancelCurrent cancels only the active operation",
                second.IsCancellationRequested
                && first.IsCancellationRequested,
                $"firstCancelled={first.IsCancellationRequested};secondCancelled={second.IsCancellationRequested}");

            coordinator.Dispose();
            Check(
                "Dispose cancels and retires the active operation",
                second.IsCancellationRequested && !second.IsCurrent,
                $"cancelled={second.IsCancellationRequested};current={second.IsCurrent}");

            var tokenWaitHandleAvailableAfterCoordinatorDispose = false;
            try
            {
                using var waitHandle = second.Token.WaitHandle;
                tokenWaitHandleAvailableAfterCoordinatorDispose = true;
            }
            catch (ObjectDisposedException)
            {
            }

            Check(
                "Dispose defers cancellation-source disposal until operation completion",
                tokenWaitHandleAvailableAfterCoordinatorDispose,
                $"waitHandleAvailable={tokenWaitHandleAvailableAfterCoordinatorDispose}");

            second.Dispose();
            var tokenWaitHandleDisposedAfterCompletion = false;
            try
            {
                using var waitHandle = second.Token.WaitHandle;
            }
            catch (ObjectDisposedException)
            {
                tokenWaitHandleDisposedAfterCompletion = true;
            }

            Check(
                "operation completion releases its cancellation source",
                tokenWaitHandleDisposedAfterCompletion,
                $"waitHandleDisposed={tokenWaitHandleDisposedAfterCompletion}");

            var rejectedAfterDispose = false;
            try
            {
                coordinator.Begin();
            }
            catch (ObjectDisposedException)
            {
                rejectedAfterDispose = true;
            }

            Check(
                "disposed coordinator rejects a new operation",
                rejectedAfterDispose,
                $"rejected={rejectedAfterDispose}");
        }

        using (var completionCoordinator = new ShellSourceLoadOperationCoordinator())
        {
            var completed = completionCoordinator.Begin();
            completed.Dispose();
            var replacement = completionCoordinator.Begin();
            Check(
                "completion clears only its own active operation",
                !completed.IsCurrent
                && !completed.IsCancellationRequested
                && replacement.IsCurrent
                && replacement.Generation == 2,
                $"completedCurrent={completed.IsCurrent};completedCancelled={completed.IsCancellationRequested};replacementCurrent={replacement.IsCurrent};replacementGeneration={replacement.Generation}");
            replacement.Dispose();
            Check(
                "replacement completion retires the active operation",
                !replacement.IsCurrent,
                $"replacementCurrent={replacement.IsCurrent}");
        }

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ShellSourceLoadOperationCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
