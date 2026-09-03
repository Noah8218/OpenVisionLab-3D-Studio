using System.IO;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerSourceLoadOperationCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer source-load operation coordinator verification",
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

        using (var coordinator = new ViewerSourceLoadOperationCoordinator())
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

            var staleApplyCount = 0;
            var staleApplied = coordinator.TryApply(first, () => staleApplyCount++);
            var activeApplyCount = 0;
            var activeApplied = coordinator.TryApply(second, () => activeApplyCount++);
            Check(
                "stale operation cannot enter the View apply callback",
                !staleApplied && staleApplyCount == 0 && activeApplied && activeApplyCount == 1,
                $"staleApplied={staleApplied};staleCount={staleApplyCount};activeApplied={activeApplied};activeCount={activeApplyCount}");

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

        using (var externalCancellation = new CancellationTokenSource())
        using (var coordinator = new ViewerSourceLoadOperationCoordinator())
        {
            using var operation = coordinator.Begin(externalCancellation.Token);
            externalCancellation.Cancel();
            var applied = coordinator.TryApply(operation, static () => { });
            Check(
                "external cancellation is linked and blocks View apply",
                operation.IsCancellationRequested && operation.IsCurrent && !applied,
                $"cancelled={operation.IsCancellationRequested};current={operation.IsCurrent};applied={applied}");
        }

        using (var coordinator = new ViewerSourceLoadOperationCoordinator())
        {
            using var operation = coordinator.Begin();
            coordinator.CancelCurrent();
            Check(
                "Viewer-owned cancellation is distinguishable from external cancellation",
                operation.IsCancellationRequested
                && operation.IsCurrent
                && !operation.IsExternalCancellationRequested,
                $"cancelled={operation.IsCancellationRequested};current={operation.IsCurrent};external={operation.IsExternalCancellationRequested}");
        }

        using (var completionCoordinator = new ViewerSourceLoadOperationCoordinator())
        {
            var completed = completionCoordinator.Begin();
            completed.Dispose();
            using var replacement = completionCoordinator.Begin();
            Check(
                "completion clears only its own active operation",
                !completed.IsCurrent
                && !completed.IsCancellationRequested
                && replacement.IsCurrent
                && replacement.Generation == 2,
                $"completedCurrent={completed.IsCurrent};completedCancelled={completed.IsCancellationRequested};replacementCurrent={replacement.IsCurrent};replacementGeneration={replacement.Generation}");
        }

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerSourceLoadOperationCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
