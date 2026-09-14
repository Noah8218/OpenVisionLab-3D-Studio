using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class HeightImageViewerLoadVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var result = Task.Run(VerifyCore).GetAwaiter().GetResult();
        var succeeded = result.Passed == result.Total;
        result.Lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({result.Passed}/{result.Total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, result.Lines);
        summary = $"HeightImageViewerLoadOwner|pass={succeeded}|checks={result.Passed}/{result.Total}|report={fullReportPath}";
        return succeeded;
    }

    private static (List<string> Lines, int Passed, int Total) VerifyCore()
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Height Image viewer load ownership verification",
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

        using var disposedViewer = CreateViewer();
        var pendingFailure = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lateFailure = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = disposedViewer.StartObservedLoad(
            () => pendingFailure.Task,
            exception => lateFailure.TrySetResult(exception));
        var retainedBeforeDispose = disposedViewer.IsObservedLoadRunning;
        disposedViewer.Dispose();
        pendingFailure.TrySetException(
            new InvalidOperationException("late Height Image viewer failure"));
        try
        {
            pendingFailure.Task.GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
        }

        var lateFailureSuppressed = Task.WhenAny(
                lateFailure.Task,
                Task.Delay(TimeSpan.FromMilliseconds(100)))
            .GetAwaiter()
            .GetResult() != lateFailure.Task;
        Check(
            "disposed-viewer-suppresses-late-failure",
            accepted
            && retainedBeforeDispose
            && disposedViewer.IsDisposed
            && !disposedViewer.IsObservedLoadRunning
            && lateFailureSuppressed,
            $"accepted={accepted};retained={retainedBeforeDispose};disposed={disposedViewer.IsDisposed};lateFailureSuppressed={lateFailureSuppressed}");
        Check(
            "disposed-viewer-rejects-new-observed-load",
            !disposedViewer.StartObservedLoad(
                () => Task.CompletedTask,
                _ => { }),
            "acceptedAfterDispose=false");

        using var reusableViewer = CreateViewer();
        var firstCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstAccepted = reusableViewer.StartObservedLoad(
            () => firstCompletion.Task,
            _ => { });
        var retainedFirstLoad = reusableViewer.IsObservedLoadRunning;
        firstCompletion.TrySetResult(null);
        firstCompletion.Task.GetAwaiter().GetResult();
        var firstReleased = WaitUntil(
            () => !reusableViewer.IsObservedLoadRunning,
            TimeSpan.FromSeconds(5));
        var secondCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondAccepted = reusableViewer.StartObservedLoad(
            () => secondCompletion.Task,
            _ => { });
        var retainedSecondLoad = reusableViewer.IsObservedLoadRunning;
        secondCompletion.TrySetResult(null);
        secondCompletion.Task.GetAwaiter().GetResult();
        var secondReleased = WaitUntil(
            () => !reusableViewer.IsObservedLoadRunning,
            TimeSpan.FromSeconds(5));
        Check(
            "completed-viewer-load-releases-and-reuses-slot",
            firstAccepted
            && retainedFirstLoad
            && firstReleased
            && secondAccepted
            && retainedSecondLoad
            && secondReleased,
            $"firstAccepted={firstAccepted};firstRetained={retainedFirstLoad};firstReleased={firstReleased};secondAccepted={secondAccepted};secondRetained={retainedSecondLoad};secondReleased={secondReleased}");

        var synchronousFailureCount = 0;
        var synchronousFailureAccepted = reusableViewer.StartObservedLoad(
            () => throw new InvalidOperationException("synchronous viewer failure"),
            _ => synchronousFailureCount++);
        Check(
            "synchronous-viewer-load-failure-is-reported",
            !synchronousFailureAccepted
            && synchronousFailureCount == 1
            && !reusableViewer.IsObservedLoadRunning,
            $"accepted={synchronousFailureAccepted};failureCount={synchronousFailureCount};running={reusableViewer.IsObservedLoadRunning}");

        var concurrentRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            nameof(HeightImageViewerLoadVerification),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(concurrentRoot);
        var concurrentSourcePath = Path.Combine(concurrentRoot, "concurrent-source.c3d");
        File.WriteAllBytes(concurrentSourcePath, [1]);
        try
        {
            using var concurrentViewer = CreateViewer();
            var release = new TaskCompletionSource<C3DHeightFieldSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var loaderCalls = 0;
            var snapshot = C3DHeightFieldSnapshot.CreateForVerification(
                "source.concurrent-height-image",
                2,
                2,
                [1.0, 2.0, 3.0, 4.0]);
            var firstLoad = concurrentViewer.EnsureSourceAsync(
                concurrentSourcePath,
                "source.concurrent-height-image",
                "raw-height",
                "frame.c3d-grid-index",
                _ =>
                {
                    Interlocked.Increment(ref loaderCalls);
                    return release.Task;
                });
            var secondLoad = concurrentViewer.EnsureSourceAsync(
                concurrentSourcePath,
                "source.concurrent-height-image",
                "raw-height",
                "frame.c3d-grid-index",
                _ =>
                {
                    Interlocked.Increment(ref loaderCalls);
                    return Task.FromResult(snapshot);
                });
            release.TrySetResult(snapshot);
            Task.WhenAll(firstLoad, secondLoad).GetAwaiter().GetResult();
            Check(
                "same-source-viewer-loads-converge",
                ReferenceEquals(firstLoad, secondLoad)
                && loaderCalls == 1
                && concurrentViewer.Frame is { Width: 2, Height: 2 }
                && concurrentViewer.DisplayFrame is not null
                && !concurrentViewer.HasError,
                $"sameTask={ReferenceEquals(firstLoad, secondLoad)};loaderCalls={loaderCalls};hasFrame={concurrentViewer.Frame is not null};hasDisplay={concurrentViewer.DisplayFrame is not null};error={concurrentViewer.Error}");
        }
        finally
        {
            if (Directory.Exists(concurrentRoot))
            {
                Directory.Delete(concurrentRoot, recursive: true);
            }
        }

        return (lines, passed, total);
    }

    private static HeightImageViewerViewModel CreateViewer() =>
        new(ThreeDLocalization.Shared, new SharedHeightCursorSession());

    private static bool WaitUntil(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return true;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(10));
        }

        return predicate();
    }
}
