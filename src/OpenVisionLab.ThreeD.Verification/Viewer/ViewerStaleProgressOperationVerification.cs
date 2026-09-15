using System.IO;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerStaleProgressOperationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer stale progress operation verification",
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

        using var coordinator = new ViewerSourceLoadOperationCoordinator();
        using var first = coordinator.Begin();
        var visibleProgress = new List<string>();
        var firstProgress = new Action<double>(value =>
        {
            if (first.IsCurrent && !first.IsCancellationRequested)
            {
                visibleProgress.Add($"A:{value:F0}");
            }
        });
        firstProgress(10.0);
        Check(
            "current operation accepts its own progress",
            visibleProgress.SequenceEqual(["A:10"]),
            $"events={string.Join(',', visibleProgress)}");

        using var second = coordinator.Begin();
        var secondProgress = new Action<double>(value =>
        {
            if (second.IsCurrent && !second.IsCancellationRequested)
            {
                visibleProgress.Add($"B:{value:F0}");
            }
        });
        var dispatcher = Dispatcher.CurrentDispatcher;
        var dispatcherFrame = new DispatcherFrame();
        dispatcher.BeginInvoke(
            DispatcherPriority.Normal,
            new Action(() => firstProgress(90.0)));
        dispatcher.BeginInvoke(
            DispatcherPriority.Normal,
            new Action(() =>
            {
                secondProgress(20.0);
                dispatcherFrame.Continue = false;
            }));
        Dispatcher.PushFrame(dispatcherFrame);
        Check(
            "retired operation progress is ignored after replacement",
            first.IsCancellationRequested
            && !first.IsCurrent
            && visibleProgress.SequenceEqual(["A:10", "B:20"]),
            $"firstCancelled={first.IsCancellationRequested};firstCurrent={first.IsCurrent};events={string.Join(',', visibleProgress)}");

        var completionEvents = new List<string>();
        var firstCompletion = new Action(() =>
        {
            if (first.IsCurrent && !first.IsCancellationRequested)
            {
                completionEvents.Add("A:complete");
            }
        });
        var secondCompletion = new Action(() =>
        {
            if (second.IsCurrent && !second.IsCancellationRequested)
            {
                completionEvents.Add("B:complete");
            }
        });
        firstCompletion();
        secondCompletion();
        Check(
            "retired completion cannot overwrite the current operation",
            completionEvents.SequenceEqual(["B:complete"]),
            $"events={string.Join(',', completionEvents)}");

        var normalProgress = new List<double>();
        foreach (var value in new[] { 0.0, 50.0, 100.0 })
        {
            secondProgress(value);
            normalProgress.Add(value);
        }

        Check(
            "current operation retains the normal 0-to-100 progress path",
            normalProgress.SequenceEqual([0.0, 50.0, 100.0])
            && visibleProgress.SequenceEqual(["A:10", "B:20", "B:0", "B:50", "B:100"]),
            $"normal={string.Join(',', normalProgress)};events={string.Join(',', visibleProgress)}");

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerStaleProgressOperation|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
