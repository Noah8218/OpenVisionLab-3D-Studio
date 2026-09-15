using System.IO;
using System.Threading;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerDispatcherShutdownDisposeVerification
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer Dispatcher shutdown Dispose verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: normal worker Dispose and Dispatcher shutdown Dispose without a desktop window or active GL context"
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

        var normal = RunDispose(shutdownBeforeDispose: false);
        Check(
            "worker Dispose marshals to a running Dispatcher",
            normal.Completed
            && normal.Disposed
            && normal.ManagedDataCleared
            && normal.RepeatedDisposeSucceeded
            && normal.Exception is null
            && normal.UiException is null
            && normal.UiExited,
            normal.ToDetail());

        var shuttingDown = RunDispose(shutdownBeforeDispose: true);
        Check(
            "worker Dispose remains safe after Dispatcher shutdown starts",
            shuttingDown.Completed
            && shuttingDown.Disposed
            && shuttingDown.ManagedDataCleared
            && shuttingDown.RepeatedDisposeSucceeded
            && shuttingDown.Exception is null
            && shuttingDown.UiException is null
            && shuttingDown.DispatcherShutdownStarted
            && shuttingDown.RenderContextUnavailable
            && shuttingDown.ResourceContextUnavailableCount > 0
            && shuttingDown.UiExited,
            shuttingDown.ToDetail());

        var succeeded = passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerDispatcherShutdownDispose|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static DisposeObservation RunDispose(bool shutdownBeforeDispose)
    {
        using var ready = new ManualResetEventSlim();
        OpenVisionThreeDViewerControl? control = null;
        Dispatcher? dispatcher = null;
        Exception? uiException = null;

        var uiThread = new Thread(
            () =>
            {
                try
                {
                    dispatcher = Dispatcher.CurrentDispatcher;
                    control = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
                    ready.Set();
                    Dispatcher.Run();
                }
                catch (Exception exception)
                {
                    uiException = exception;
                    ready.Set();
                }
            });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();

        if (!ready.Wait(WaitTimeout) || control is null || dispatcher is null)
        {
            return new DisposeObservation(
                Completed: false,
                Disposed: false,
                ManagedDataCleared: false,
                DispatcherShutdownStarted: false,
                UiExited: !uiThread.IsAlive,
                Exception: new TimeoutException("Viewer Dispatcher did not initialize."),
                UiException: uiException,
                RepeatedDisposeSucceeded: false,
                RenderContextUnavailable: false,
                ResourceContextUnavailableCount: 0);
        }

        var dispatcherShutdownStarted = false;
        if (shutdownBeforeDispose)
        {
            try
            {
                control.RequestVisibleFrame();
            }
            catch (Exception exception)
            {
                uiException = exception;
            }

            try
            {
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                dispatcherShutdownStarted = SpinWait.SpinUntil(
                    () => dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished,
                    WaitTimeout);
            }
            catch (Exception exception)
            {
                uiException = exception;
            }
        }

        Exception? disposeException = null;
        var disposeTask = Task.Run(
            () =>
            {
                try
                {
                    control.Dispose();
                }
                catch (Exception exception)
                {
                    disposeException = exception;
                }
            });
        var completed = disposeTask.Wait(WaitTimeout);
        Exception? repeatedDisposeException = null;
        try
        {
            control.Dispose();
        }
        catch (Exception exception)
        {
            repeatedDisposeException = exception;
        }

        if (!shutdownBeforeDispose)
        {
            try
            {
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
            }
            catch (InvalidOperationException)
            {
                // The Dispatcher may already be closing after a failed disposal.
            }
        }

        var uiExited = uiThread.Join(WaitTimeout);
        return new DisposeObservation(
            Completed: completed,
            Disposed: control.IsDisposed,
            ManagedDataCleared: !control.HasManagedDataReferences,
            DispatcherShutdownStarted: dispatcherShutdownStarted,
            UiExited: uiExited,
            Exception: disposeException,
            UiException: uiException,
            RepeatedDisposeSucceeded: repeatedDisposeException is null,
            RenderContextUnavailable: control.RenderContextDisposeUnavailable,
            ResourceContextUnavailableCount: control.OpenGLResourceRetirementContextUnavailableCount);
    }

    private sealed record DisposeObservation(
        bool Completed,
        bool Disposed,
        bool ManagedDataCleared,
        bool DispatcherShutdownStarted,
        bool UiExited,
        Exception? Exception,
        Exception? UiException,
        bool RepeatedDisposeSucceeded,
        bool RenderContextUnavailable,
        int ResourceContextUnavailableCount)
    {
        public string ToDetail() =>
            $"completed={Completed};disposed={Disposed};managedDataCleared={ManagedDataCleared};repeatedDisposeSucceeded={RepeatedDisposeSucceeded};dispatcherShutdownStarted={DispatcherShutdownStarted};uiExited={UiExited};renderContextUnavailable={RenderContextUnavailable};resourceContextUnavailableCount={ResourceContextUnavailableCount};exception={Format(Exception)};uiException={Format(UiException)}";

        private static string Format(Exception? exception) =>
            exception is null
                ? "(none)"
                : exception.ToString().Replace(Environment.NewLine, " | ");
    }
}
