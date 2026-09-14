using System.IO;
using OpenVisionLab.ThreeD.Shell.Coordination;

namespace OpenVisionLab.ThreeD.Verification.Shell;

internal static class ShellWorkbenchRequestOwnerVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Shell Workbench request owner verification",
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

        var failureCount = 0;
        using (var owner = new ShellWorkbenchRequestOwner(_ => failureCount++))
        {
            var entered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken observedToken = default;
            var started = owner.TryStart(token =>
            {
                observedToken = token;
                return entered.Task;
            });
            Check(
                "starts-one-request",
                started && owner.IsRunning && !owner.IsDisposed,
                $"started={started};running={owner.IsRunning};disposed={owner.IsDisposed}");
            Check(
                "passes-owned-cancellation-token",
                observedToken.CanBeCanceled && !observedToken.IsCancellationRequested,
                $"canBeCanceled={observedToken.CanBeCanceled};cancelled={observedToken.IsCancellationRequested}");
            var duplicateAccepted = owner.TryStart(_ => Task.CompletedTask);
            Check(
                "rejects-duplicate-request",
                !duplicateAccepted,
                $"duplicateAccepted={duplicateAccepted}");

            owner.Dispose();
            Check(
                "dispose-cancels-active-request",
                owner.IsDisposed && observedToken.IsCancellationRequested,
                $"disposed={owner.IsDisposed};cancelled={observedToken.IsCancellationRequested}");

            entered.TrySetException(new InvalidOperationException("late failure"));
            SpinWait.SpinUntil(() => !owner.IsRunning, TimeSpan.FromSeconds(1));
            owner.Dispose();
            Check(
                "late-failure-does-not-report-after-dispose",
                failureCount == 0,
                $"failureCount={failureCount}");
            Check(
                "rejects-request-after-dispose",
                !owner.TryStart(_ => Task.CompletedTask),
                "acceptedAfterDispose=false");
        }

        var completedFailureCount = 0;
        using (var completedOwner = new ShellWorkbenchRequestOwner(_ => completedFailureCount++))
        {
            Check(
                "completed-request-starts",
                completedOwner.TryStart(_ => Task.CompletedTask),
                "started=true");
            SpinWait.SpinUntil(() => !completedOwner.IsRunning, TimeSpan.FromSeconds(1));
            Check(
                "completed-request-releases-slot",
                !completedOwner.IsRunning,
                $"running={completedOwner.IsRunning}");
            Check(
                "completed-request-can-restart",
                completedOwner.TryStart(_ => Task.CompletedTask),
                "restarted=true");
            Check(
                "completed-request-does-not-report-failure",
                completedFailureCount == 0,
                $"failureCount={completedFailureCount}");
        }

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ShellWorkbenchRequestOwner|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
