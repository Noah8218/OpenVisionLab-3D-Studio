using System.IO;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Shell;
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

        var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
        try
        {
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishUnsupported = ShellWorkbenchSourceLoadCoordinator.FormatUnsupportedViewerOnlyInput("PNG");
            Check(
                "unsupported-format failure gives localized actionable English detail",
                englishUnsupported.Contains("PNG", StringComparison.Ordinal)
                && englishUnsupported.Contains("not supported", StringComparison.OrdinalIgnoreCase)
                && englishUnsupported.Contains("C3D", StringComparison.Ordinal)
                && englishUnsupported.Contains("GLB", StringComparison.Ordinal)
                && englishUnsupported.Contains("STL", StringComparison.Ordinal)
                && englishUnsupported.Contains("LAS", StringComparison.Ordinal)
                && englishUnsupported.Contains("LAZ", StringComparison.Ordinal),
                englishUnsupported);

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanUnsupported = ShellWorkbenchSourceLoadCoordinator.FormatUnsupportedViewerOnlyInput("PNG");
            Check(
                "unsupported-format failure gives localized actionable Korean detail",
                koreanUnsupported.Contains("PNG", StringComparison.Ordinal)
                && koreanUnsupported.Contains("지원하지 않습니다", StringComparison.Ordinal)
                && koreanUnsupported.Contains("C3D", StringComparison.Ordinal)
                && koreanUnsupported.Contains("GLB", StringComparison.Ordinal)
                && koreanUnsupported.Contains("STL", StringComparison.Ordinal)
                && koreanUnsupported.Contains("LAS", StringComparison.Ordinal)
                && koreanUnsupported.Contains("LAZ", StringComparison.Ordinal),
                koreanUnsupported);

            var staleViewerStatus = "C3D source load failed; current source retained: CellCountOverflow";
            var deliveredDetails = new List<string>();
            using (var sourceRequestOwner = new ShellWorkbenchRequestOwner(exception =>
                deliveredDetails.Add(ShellWorkbenchSourceLoadCoordinator.FormatSourceRequestFailure(exception))))
            {
                var started = sourceRequestOwner.TryStart(_ =>
                    Task.FromException(new NotSupportedException(koreanUnsupported)));
                SpinWait.SpinUntil(() => !sourceRequestOwner.IsRunning, TimeSpan.FromSeconds(1));
                var delivered = deliveredDetails.SingleOrDefault();
                Check(
                    "source request reports the current exception instead of stale ViewerStatus",
                    started
                    && deliveredDetails.Count == 1
                    && string.Equals(delivered, koreanUnsupported, StringComparison.Ordinal)
                    && !string.Equals(delivered, staleViewerStatus, StringComparison.Ordinal)
                    && !delivered!.Contains("CellCountOverflow", StringComparison.Ordinal),
                    $"started={started};callbacks={deliveredDetails.Count};details={delivered};stale={staleViewerStatus}");
            }

            var emptyExceptionDetail = ShellWorkbenchSourceLoadCoordinator.FormatSourceRequestFailure(
                new EmptyMessageException());
            Check(
                "source request keeps a usable detail when exception text is empty",
                string.Equals(emptyExceptionDetail, nameof(EmptyMessageException), StringComparison.Ordinal),
                $"detail={emptyExceptionDetail}");
        }
        finally
        {
            if (OpenVisionLanguageService.CurrentLanguage != originalLanguage)
            {
                OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            }
        }

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ShellWorkbenchRequestOwner|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private sealed class EmptyMessageException() : Exception(string.Empty);
}
