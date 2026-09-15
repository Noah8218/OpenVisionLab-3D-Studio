using System.IO;
using OpenVisionLab.ThreeD.Shell.Coordination;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class ShellSourceLoadCancellationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var lines = new List<string>();
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string evidence)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")}|{name}|{evidence}");
        }

        var preCanceledExternal = new CancellationTokenSource();
        preCanceledExternal.Cancel();
        using (var coordinator = new ShellSourceLoadOperationCoordinator())
        using (var operation = coordinator.Begin(preCanceledExternal.Token))
        {
            Check(
                "pre-canceled-external-token-is-linked",
                operation.IsCancellationRequested,
                $"operationCanceled={operation.IsCancellationRequested}");
        }
        preCanceledExternal.Dispose();

        using (var external = new CancellationTokenSource())
        using (var coordinator = new ShellSourceLoadOperationCoordinator())
        using (var operation = coordinator.Begin(external.Token))
        {
            var before = operation.IsCancellationRequested;
            external.Cancel();
            Check(
                "mid-load-external-cancel-reaches-operation",
                !before && operation.IsCancellationRequested,
                $"before={before};after={operation.IsCancellationRequested}");
        }

        using (var coordinator = new ShellSourceLoadOperationCoordinator())
        using (var operation = coordinator.Begin())
        {
            coordinator.CancelCurrent();
            Check(
                "internal-cancel-remains-supported",
                operation.IsCancellationRequested,
                $"operationCanceled={operation.IsCancellationRequested}");
        }

        using (var firstExternal = new CancellationTokenSource())
        using (var secondExternal = new CancellationTokenSource())
        using (var coordinator = new ShellSourceLoadOperationCoordinator())
        using (var first = coordinator.Begin(firstExternal.Token))
        using (var second = coordinator.Begin(secondExternal.Token))
        {
            Check(
                "replacement-cancels-previous-and-keeps-new-current",
                first.IsCancellationRequested
                && second.IsCurrent
                && !second.IsCancellationRequested,
                $"firstCanceled={first.IsCancellationRequested};secondCurrent={second.IsCurrent};secondCanceled={second.IsCancellationRequested}");
        }

        var disposedCoordinator = new ShellSourceLoadOperationCoordinator();
        var disposedOperation = disposedCoordinator.Begin();
        disposedCoordinator.Dispose();
        Check(
            "coordinator-dispose-cancels-active-operation",
            disposedOperation.IsCancellationRequested,
            $"operationCanceled={disposedOperation.IsCancellationRequested}");
        disposedOperation.Dispose();

        var success = passed == total;
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ShellSourceLoadCancellation|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        return success;
    }
}
