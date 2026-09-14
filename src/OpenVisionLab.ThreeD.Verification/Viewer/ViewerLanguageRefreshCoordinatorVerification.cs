using System.IO;
using System.Threading;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer.Localization;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerLanguageRefreshCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer language-refresh coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: Dispatcher queueing, coalescing, generation invalidation, and disposal"
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
                    var isLoaded = true;
                    var refreshCount = 0;
                    var coordinator = new ViewerLanguageRefreshCoordinator(
                        dispatcher,
                        () => false,
                        () => isLoaded,
                        () => refreshCount++);

                    coordinator.Request();
                    Check(
                        "a UI-thread language event refreshes immediately",
                        refreshCount == 1,
                        $"refreshCount={refreshCount}");

                    RunOnWorker(coordinator.Request);
                    Drain(dispatcher);
                    Check(
                        "an off-thread language event refreshes on the Dispatcher",
                        refreshCount == 2,
                        $"refreshCount={refreshCount}");

                    RunOnWorker(coordinator.Request);
                    RunOnWorker(coordinator.Request);
                    Drain(dispatcher);
                    Check(
                        "repeated off-thread events coalesce to one pending refresh",
                        refreshCount == 3,
                        $"refreshCount={refreshCount}");

                    isLoaded = false;
                    RunOnWorker(coordinator.Request);
                    coordinator.Cancel();
                    isLoaded = true;
                    Drain(dispatcher);
                    Check(
                        "Cancel invalidates a queued refresh before it can run",
                        refreshCount == 3,
                        $"refreshCount={refreshCount}");

                    RunOnWorker(coordinator.Request);
                    Drain(dispatcher);
                    Check(
                        "a later generation refreshes after the View is loaded",
                        refreshCount == 4,
                        $"refreshCount={refreshCount}");

                    coordinator.Dispose();
                    coordinator.Dispose();
                    RunOnWorker(coordinator.Request);
                    Drain(dispatcher);
                    Check(
                        "Dispose is idempotent and prevents later refresh callbacks",
                        refreshCount == 4,
                        $"refreshCount={refreshCount}");
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
        summary = $"ViewerLanguageRefresh|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static void RunOnWorker(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("Worker dispatch failed.", failure);
        }
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
