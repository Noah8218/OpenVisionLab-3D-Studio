using System.IO;
using System.Threading;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerSourceUnloadCancellationCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer source-unload cancellation coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: Dispatcher generation, coalescing, stale callback rejection, and disposal"
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

        var thread = new Thread(
            () =>
            {
                try
                {
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    var isLoaded = false;
                    var cancellationCount = 0;
                    var coordinator = new ViewerSourceUnloadCancellationCoordinator(
                        dispatcher,
                        () => false,
                        () => isLoaded,
                        () => cancellationCount++);

                    coordinator.ScheduleAfterUnload();
                    Drain(dispatcher);
                    Check(
                        "unloaded control invokes cancellation after the deferred callback",
                        cancellationCount == 1,
                        $"cancellationCount={cancellationCount}");

                    isLoaded = true;
                    coordinator.ScheduleAfterUnload();
                    coordinator.MarkLoaded();
                    Drain(dispatcher);
                    Check(
                        "Loaded invalidates a pending unload cancellation",
                        cancellationCount == 1,
                        $"cancellationCount={cancellationCount}");

                    isLoaded = false;
                    coordinator.ScheduleAfterUnload();
                    Drain(dispatcher);
                    Check(
                        "a later unload schedules a fresh current-generation cancellation",
                        cancellationCount == 2,
                        $"cancellationCount={cancellationCount}");

                    coordinator.Dispose();
                    coordinator.Dispose();
                    coordinator.ScheduleAfterUnload();
                    Drain(dispatcher);
                    Check(
                        "Dispose is idempotent and prevents later unload callbacks",
                        cancellationCount == 2,
                        $"cancellationCount={cancellationCount}");
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            lines.Add($"FAIL | verifier exception | {failure.GetType().Name}: {failure.Message}");
        }

        var succeeded = failure is null && passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerSourceUnloadCancellation|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
