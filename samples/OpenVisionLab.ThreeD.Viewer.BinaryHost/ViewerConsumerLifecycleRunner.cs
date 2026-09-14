using System.IO;
using System.Windows;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

internal sealed class ViewerConsumerLifecycleRunner
{
    private readonly ViewerConsumerLifecycleOptions options;
    private readonly ViewerConsumerContractAnalyzer contractAnalyzer;
    private readonly ViewerConsumerInputPreconditionValidator inputPreconditions;
    private readonly ViewerConsumerLifecycleReportCoordinator lifecycleReport;
    private readonly ViewerConsumerContractCaptureCoordinator contractCapture;
    private Application? application;
    private Window? window;
    private OpenVisionThreeDViewerControl? currentViewer;
    private int exitCode;
    private long cleanProcessBaselinePrivateMemory;
    private long cleanProcessBaselineManagedMemory;
    private NativeResourceSnapshot cleanProcessBaselineNativeResources = new(-1, -1, -1);
    private long emptyWindowBaselinePrivateMemory;
    private long emptyWindowBaselineManagedMemory;
    private NativeResourceSnapshot emptyWindowBaselineNativeResources = new(-1, -1, -1);

    private ViewerConsumerLifecycleRunner(ViewerConsumerLifecycleOptions options)
    {
        this.options = options;
        contractAnalyzer = new ViewerConsumerContractAnalyzer(
            options.RequireHardwareOpenGL,
            options.RequireImportedTextureRelease);
        inputPreconditions = new ViewerConsumerInputPreconditionValidator(
            options.ReportPath,
            options.C3DPath,
            options.MeshPath,
            options.PointCloudPath,
            options.SmokeContractPath);
        lifecycleReport = new ViewerConsumerLifecycleReportCoordinator(options.ReportPath);
        contractCapture = new ViewerConsumerContractCaptureCoordinator(
            options.SmokeContractPath,
            options.ReportPath,
            lifecycleReport.AddLine);
    }

    public static int Run(ViewerConsumerLifecycleOptions options)
    {
        var runner = new ViewerConsumerLifecycleRunner(options);
        return runner.RunCore();
    }

    private int RunCore()
    {
        try
        {
            inputPreconditions.Validate();
            ViewerConsumerProcessResourceObserver.CollectForObservation();
            cleanProcessBaselinePrivateMemory = ViewerConsumerProcessResourceObserver.ReadPrivateMemoryBytes();
            cleanProcessBaselineManagedMemory = ViewerConsumerProcessResourceObserver.ReadManagedMemoryBytes();
            cleanProcessBaselineNativeResources = ViewerConsumerProcessResourceObserver.ReadNativeResources();
            application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            window = new Window
            {
                Title = "OpenVisionLab 3D Viewer Independent Consumer",
                Width = 1280,
                Height = 760,
                MinWidth = 960,
                MinHeight = 640,
                WindowStartupLocation = WindowStartupLocation.Manual
            };
            window.Loaded += OnWindowLoaded;
            application.Run(window);
        }
        catch (Exception exception)
        {
            lifecycleReport.RecordCheck("RunnerStartup", false, exception.ToString());
            lifecycleReport.Write();
            exitCode = 1;
        }

        return exitCode;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs args)
    {
        if (window is null || application is null)
        {
            return;
        }

        try
        {
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            ViewerConsumerProcessResourceObserver.CollectForObservation();
            emptyWindowBaselinePrivateMemory = ViewerConsumerProcessResourceObserver.ReadPrivateMemoryBytes();
            emptyWindowBaselineManagedMemory = ViewerConsumerProcessResourceObserver.ReadManagedMemoryBytes();
            emptyWindowBaselineNativeResources = ViewerConsumerProcessResourceObserver.ReadNativeResources();
            currentViewer = CreateViewer();
            window.Content = currentViewer;
            await ExecuteAsync();
            exitCode = lifecycleReport.FailedChecks == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            lifecycleReport.RecordCheck("RunnerExecution", false, exception.ToString());
            exitCode = 1;
        }
        finally
        {
            try
            {
                currentViewer?.Dispose();
                currentViewer = null;
                window.Content = null;
            }
            catch (Exception exception)
            {
                lifecycleReport.RecordCheck("FinalCleanup", false, exception.ToString());
                exitCode = 1;
            }

            try
            {
                window.Close();
            }
            catch (Exception exception)
            {
                lifecycleReport.RecordCheck("WindowClose", false, exception.ToString());
                exitCode = 1;
            }

            if (options.GpuPostCloseObservationBarrierPath is not null)
            {
                var barrier = await WaitForGpuObservationBarrierAsync();
                lifecycleReport.RecordCheck("GpuPostCloseObservationBarrier", barrier.Passed, barrier.Details);
            }

            exitCode = lifecycleReport.FailedChecks == 0 ? 0 : 1;
            lifecycleReport.Write();
            application.Shutdown(exitCode);
        }
    }

    private async Task ExecuteAsync()
    {
        var viewer = currentViewer
            ?? throw new InvalidOperationException("The initial Viewer control was not created.");
        IOpenVisionThreeDViewerHost host = viewer;
        await WaitForLoadedAsync(viewer);
        await WaitForRenderReadyAsync(viewer);

        lifecycleReport.AddLine("OpenVisionLab 3D independent Viewer consumer lifecycle");
        lifecycleReport.AddLine($"Generated={DateTimeOffset.Now:O}");
        lifecycleReport.AddLine("ProjectReferences=0");
        lifecycleReport.AddLine($"HostApiVersion={host.HostApiVersion}");
        lifecycleReport.AddLine($"C3DSource={options.C3DPath}");
        lifecycleReport.AddLine($"MeshSource={options.MeshPath}");
        lifecycleReport.AddLine($"PointCloudSource={options.PointCloudPath}");
        lifecycleReport.AddLine($"RequestedRecreateCycles={options.RecreateCycles}");
        lifecycleReport.AddLine($"RequestedWindowCloseCycles={options.WindowCloseCycles}");
        lifecycleReport.AddLine($"CleanProcessBaseline|privateBytes={cleanProcessBaselinePrivateMemory}|managedBytes={cleanProcessBaselineManagedMemory}|native={cleanProcessBaselineNativeResources}");
        lifecycleReport.AddLine($"EmptyWindowBaseline|privateBytes={emptyWindowBaselinePrivateMemory}|managedBytes={emptyWindowBaselineManagedMemory}|native={emptyWindowBaselineNativeResources}");

        var c3dLoaded = await host.LoadC3DSourceAsync(options.C3DPath, CancellationToken.None);
        var c3dContract = await contractCapture.CaptureAsync("height-map", viewer.CaptureConfiguredSmokeViewAsync);
        lifecycleReport.RecordCheck(
            "HeightMapDisplay",
            c3dLoaded
                && PathsEqual(host.HostState.Sources.CurrentC3DSourcePath, options.C3DPath)
                && host.HostState.C3DSampleVisible
                && c3dContract.Contains("C3DMap|loaded=True", StringComparison.Ordinal)
                && c3dContract.Contains("C3DRenderProxy|loaded=True", StringComparison.Ordinal),
            $"loaded={c3dLoaded}|current={host.HostState.Sources.CurrentC3DSourcePath}|contract={HasContract(c3dContract, "C3DMap|loaded=True")}");

        var selectionOverlay = await ExerciseSelectionAndOverlayAsync(viewer);
        lifecycleReport.RecordCheck("SelectionAndOverlay", selectionOverlay.Passed, selectionOverlay.Details);
        _ = await contractCapture.CaptureAsync("selection-overlay", viewer.CaptureConfiguredSmokeViewAsync);

        var recipeSourceBeforeViewerOnly = host.HostState.Sources.CurrentC3DSourcePath;
        var meshLoaded = await host.LoadViewerOnlySourceAsync(options.MeshPath, CancellationToken.None);
        var meshContract = await contractCapture.CaptureAsync("mesh", viewer.CaptureConfiguredSmokeViewAsync);
        lifecycleReport.RecordCheck(
            "MeshDisplay",
            meshLoaded
                && PathsEqual(host.HostState.Sources.CurrentViewerOnlySourcePath, options.MeshPath)
                && string.Equals(host.HostState.Sources.CurrentViewerOnlySourceFormat, "GLB", StringComparison.Ordinal)
                && host.HostState.GlbSampleVisible
                && PathsEqual(host.HostState.Sources.CurrentC3DSourcePath, recipeSourceBeforeViewerOnly)
                && meshContract.Contains("GLB|loaded=True", StringComparison.Ordinal),
            $"loaded={meshLoaded}|format={host.HostState.Sources.CurrentViewerOnlySourceFormat}|recipeSourceRetained={PathsEqual(host.HostState.Sources.CurrentC3DSourcePath, recipeSourceBeforeViewerOnly)}");

        if (options.RequireHardwareOpenGL)
        {
            var hardware = contractAnalyzer.AnalyzeHardwareRenderPath(c3dContract, meshContract);
            lifecycleReport.RecordCheck("HardwareOpenGLRenderPath", hardware.Passed, hardware.Details);
        }

        var pointCloudLoaded = await host.LoadViewerOnlySourceAsync(options.PointCloudPath, CancellationToken.None);
        var pointCloudContract = await contractCapture.CaptureAsync("point-cloud", viewer.CaptureConfiguredSmokeViewAsync);
        lifecycleReport.RecordCheck(
            "PointCloudDisplay",
            pointCloudLoaded
                && PathsEqual(host.HostState.Sources.CurrentViewerOnlySourcePath, options.PointCloudPath)
                && string.Equals(host.HostState.Sources.CurrentViewerOnlySourceFormat, "LAZ", StringComparison.Ordinal)
                && host.HostState.LazSampleVisible
                && PathsEqual(host.HostState.Sources.CurrentC3DSourcePath, recipeSourceBeforeViewerOnly)
                && pointCloudContract.Contains("LAZ|loaded=True", StringComparison.Ordinal)
                && pointCloudContract.Contains("decoder=points-decoded", StringComparison.Ordinal),
            $"loaded={pointCloudLoaded}|format={host.HostState.Sources.CurrentViewerOnlySourceFormat}|recipeSourceRetained={PathsEqual(host.HostState.Sources.CurrentC3DSourcePath, recipeSourceBeforeViewerOnly)}");

        var camera = ExerciseCamera(host);
        lifecycleReport.RecordCheck("CameraCaptureApply", camera.Passed, camera.Details);

        if (options.WindowCloseCycles > 0)
        {
            var closeCycles = await ExerciseWindowCloseCyclesAsync();
            lifecycleReport.RecordCheck(
                "WindowCloseCycles",
                closeCycles.Observed == options.WindowCloseCycles,
                $"observed={closeCycles.Observed}/{options.WindowCloseCycles}|nativeHandleDelta={closeCycles.NativeHandleDelta}|gdiDelta={closeCycles.GdiDelta}|userDelta={closeCycles.UserDelta}");
        }

        if (options.RequireHardwareOpenGL)
        {
            var closeReparent = await ExerciseCloseReparentCancellationAsync(viewer);
            lifecycleReport.RecordCheck("CloseReparentCancellation", closeReparent.Passed, closeReparent.Details);
        }

        var firstMemory = ViewerConsumerProcessResourceObserver.ReadPrivateMemoryBytes();
        var firstManagedMemory = ViewerConsumerProcessResourceObserver.ReadManagedMemoryBytes();
        var firstNativeResources = ViewerConsumerProcessResourceObserver.ReadNativeResources();
        var firstRemoval = await RemoveViewerAsync(disposeBeforeRemove: true);
        lifecycleReport.RecordCheck("RemoveAndDispose", firstRemoval.Passed, firstRemoval.Details);
        if (options.RequireHardwareOpenGL)
        {
            lifecycleReport.RecordCheck(
                "HardwareResourceRetirement",
                firstRemoval.ResourceRetirementPassed,
                firstRemoval.ResourceRetirementDetails);
        }

        var recreated = await AttachViewerAsync();
        IOpenVisionThreeDViewerHost recreatedHost = recreated;
        var recreatedLoaded = await recreatedHost.LoadViewerOnlySourceAsync(options.MeshPath, CancellationToken.None);
        await WaitForRenderReadyAsync(recreated);
        lifecycleReport.RecordCheck(
            "RecreateNewControl",
            recreatedLoaded
                && PathsEqual(recreatedHost.HostState.Sources.CurrentViewerOnlySourcePath, options.MeshPath)
                && recreatedHost.HostState.GlbSampleVisible,
            $"loaded={recreatedLoaded}|current={recreatedHost.HostState.Sources.CurrentViewerOnlySourcePath}");

        var secondRemoval = await RemoveViewerAsync(disposeBeforeRemove: false);
        lifecycleReport.RecordCheck("RemoveThenDispose", secondRemoval.Passed, secondRemoval.Details);

        await RunRecreateCyclesAsync(firstMemory, firstManagedMemory, firstNativeResources);
        lifecycleReport.AddLine($"Contracts={contractCapture.ContractCount}");
    }

    private async Task<SelectionOverlayObservation> ExerciseSelectionAndOverlayAsync(
        OpenVisionThreeDViewerControl viewer)
    {
        var hoverCount = 0;
        C3DGridCursor? hoverCursor = null;
        EventHandler<C3DGridHoverChangedEventArgs> handler = (_, eventArgs) =>
        {
            hoverCount++;
            hoverCursor = eventArgs.Cursor;
        };

        viewer.C3DGridHoverChanged += handler;
        try
        {
            var host = (IOpenVisionThreeDViewerHost)viewer;
            var selectionModeSet = host.TrySetSelectionMode("Point");
            var overlaySet = host.TrySetSelectionOverlayVisible(true);
            var published = TryPublishFirstValidC3DCell(viewer);
            if (hoverCursor is { } cursor)
            {
                viewer.SetLinkedHeightCursor(cursor);
            }

            await WaitForRenderReadyAsync(viewer);
            var passed = published
                && hoverCount > 0
                && hoverCursor is { IsValid: true }
                && viewer.LinkedHeightCursor is { IsValid: true }
                && selectionModeSet
                && overlaySet
                && host.HostState.Selection.OverlayVisible;
            return new SelectionOverlayObservation(
                passed,
                $"published={published}|hoverEvents={hoverCount}|cursorValid={hoverCursor?.IsValid ?? false}|linkedCursor={viewer.LinkedHeightCursor is not null}|selectionOverlay={host.HostState.Selection.OverlayVisible}");
        }
        finally
        {
            viewer.C3DGridHoverChanged -= handler;
        }
    }

    private bool TryPublishFirstValidC3DCell(OpenVisionThreeDViewerControl viewer)
    {
        var host = (IOpenVisionThreeDViewerHost)viewer;
        if (!host.TryGetCurrentC3DSourceBinding(options.C3DPath, out var binding))
        {
            return false;
        }

        var centerRow = binding.GridHeight / 2;
        var centerColumn = binding.GridWidth / 2;
        if (viewer.TryPublishC3DGridHoverForSmoke(centerRow, centerColumn))
        {
            return true;
        }

        var attempts = 0;
        for (var row = 0; row < binding.GridHeight && attempts < 4096; row++)
        {
            for (var column = 0; column < binding.GridWidth && attempts < 4096; column++)
            {
                attempts++;
                if (viewer.TryPublishC3DGridHoverForSmoke(row, column))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static CameraObservation ExerciseCamera(IOpenVisionThreeDViewerHost viewer)
    {
        var before = viewer.CaptureCameraState();
        var requested = before with
        {
            YawDegrees = before.YawDegrees + 11.0,
            PitchDegrees = before.PitchDegrees - 7.0,
            Distance = before.Distance + 0.5
        };
        var applied = viewer.TryApplyCameraState(requested);
        var after = viewer.CaptureCameraState();
        return new CameraObservation(
            applied && after == requested,
            $"applied={applied}|before={before}|after={after}|exact={after == requested}");
    }

    private async Task<DisposalObservation> RemoveViewerAsync(bool disposeBeforeRemove)
    {
        var viewer = currentViewer
            ?? throw new InvalidOperationException("No current Viewer control is available for removal.");
        var stateBefore = viewer.CaptureCameraState();
        Exception? firstException = null;
        Exception? secondException = null;
        try
        {
            if (disposeBeforeRemove)
            {
                viewer.Dispose();
            }

            if (window is null)
            {
                throw new InvalidOperationException("The consumer Window is not available.");
            }

            window.Content = null;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            if (!disposeBeforeRemove)
            {
                viewer.Dispose();
            }
        }
        catch (Exception exception)
        {
            firstException = exception;
        }

        try
        {
            viewer.Dispose();
        }
        catch (Exception exception)
        {
            secondException = exception;
        }

        var resourceRetirement = contractAnalyzer.AnalyzeResourceRetirement(
            await contractCapture.CaptureAsync(
                disposeBeforeRemove ? "disposed-before-remove" : "disposed-after-remove",
                viewer.CaptureConfiguredSmokeViewAsync));
        currentViewer = null;
        var postDisposeApply = viewer.TryApplyCameraState(stateBefore);
        var savedAfterDispose = viewer.SaveRecipe(
            Path.Combine(Path.GetDirectoryName(options.ReportPath)!, "post-dispose.recipe.json"));
        var passed = firstException is null
            && secondException is null
            && window?.Content is null
            && !postDisposeApply
            && !savedAfterDispose;
        return new DisposalObservation(
            passed,
            $"disposeBeforeRemove={disposeBeforeRemove}|firstException={firstException?.GetType().Name ?? "none"}|secondException={secondException?.GetType().Name ?? "none"}|contentNull={window?.Content is null}|postDisposeApply={postDisposeApply}|postDisposeSave={savedAfterDispose}|resourceRetirement={resourceRetirement.Details}",
            resourceRetirement.Passed,
            resourceRetirement.Details);
    }

    private async Task<OpenVisionThreeDViewerControl> AttachViewerAsync()
    {
        if (window is null)
        {
            throw new InvalidOperationException("The consumer Window is not available.");
        }

        var viewer = CreateViewer();
        currentViewer = viewer;
        window.Content = viewer;
        await WaitForLoadedAsync(viewer);
        await WaitForRenderReadyAsync(viewer);
        return viewer;
    }

    private async Task RunRecreateCyclesAsync(
        long firstPrivateMemory,
        long firstManagedMemory,
        NativeResourceSnapshot firstNativeResources)
    {
        var coordinator = new ViewerConsumerMemoryObservationCoordinator(
            options.RecreateCycles,
            firstPrivateMemory,
            firstManagedMemory,
            firstNativeResources,
            emptyWindowBaselinePrivateMemory,
            emptyWindowBaselineManagedMemory,
            emptyWindowBaselineNativeResources,
            cleanProcessBaselinePrivateMemory,
            cleanProcessBaselineManagedMemory,
            cleanProcessBaselineNativeResources,
            lifecycleReport.AddLine);
        var observation = await coordinator.RunAsync(async _ =>
        {
            var viewer = await AttachViewerAsync();
            IOpenVisionThreeDViewerHost host = viewer;
            var loaded = await host.LoadViewerOnlySourceAsync(options.MeshPath, CancellationToken.None);
            await WaitForRenderReadyAsync(viewer);
            var sourceMatch = PathsEqual(host.HostState.Sources.CurrentViewerOnlySourcePath, options.MeshPath);
            var dispose = await RemoveViewerAsync(disposeBeforeRemove: true);
            return new ViewerConsumerMemoryCycleResult(loaded, sourceMatch, dispose.Passed);
        });
        lifecycleReport.RecordCheck("RecreateCycles", observation.Passed, observation.Details);
    }

    private async Task<GpuObservationBarrier> WaitForGpuObservationBarrierAsync()
    {
        var readyPath = options.GpuPostCloseObservationBarrierPath
            ?? throw new InvalidOperationException("GPU observation barrier path is not configured.");
        var coordinator = new ViewerConsumerGpuObservationBarrierCoordinator(
            readyPath,
            ViewerConsumerProcessResourceObserver.ProcessId,
            ViewerConsumerProcessResourceObserver.ReadPrivateMemoryBytes,
            ViewerConsumerProcessResourceObserver.ReadNativeResources);
        var result = await coordinator.WaitAsync();
        return new GpuObservationBarrier(result.Passed, result.Details);
    }

    private OpenVisionThreeDViewerControl CreateViewer()
    {
        var viewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
        viewer.EnableSmokeFromCommandLine(ownsApplicationLifecycle: false);
        return viewer;
    }

    private async Task<CloseReparentObservation> ExerciseCloseReparentCancellationAsync(
        OpenVisionThreeDViewerControl stableViewer)
    {
        if (window is null)
        {
            throw new InvalidOperationException("The consumer Window is not available.");
        }

        var transientViewer = CreateViewer();
        var taskCanceled = false;
        var sourceApplied = false;
        Exception? failure = null;
        try
        {
            window.Content = null;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            currentViewer = transientViewer;
            window.Content = transientViewer;
            await WaitForLoadedAsync(transientViewer);
            await WaitForRenderReadyAsync(transientViewer);

            using var cancellation = new CancellationTokenSource();
            IOpenVisionThreeDViewerHost transientHost = transientViewer;
            var loadTask = transientHost.LoadViewerOnlySourceAsync(
                options.PointCloudPath,
                cancellation.Token);
            window.Content = null;
            cancellation.Cancel();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            try
            {
                sourceApplied = await loadTask;
            }
            catch (OperationCanceledException)
            {
                taskCanceled = true;
            }

            await transientViewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            var noStaleSource = transientHost.HostState.Sources.CurrentViewerOnlySourcePath is null;
            return new CloseReparentObservation(
                (taskCanceled || !sourceApplied) && noStaleSource,
                $"taskCanceled={taskCanceled}|sourceApplied={sourceApplied}|noStaleSource={noStaleSource}");
        }
        catch (Exception exception)
        {
            failure = exception;
            return new CloseReparentObservation(
                false,
                $"taskCanceled={taskCanceled}|sourceApplied={sourceApplied}|exception={failure.GetType().Name}:{failure.Message}");
        }
        finally
        {
            transientViewer.Dispose();
            currentViewer = stableViewer;
            window.Content = stableViewer;
            await WaitForLoadedAsync(stableViewer);
            await WaitForRenderReadyAsync(stableViewer);
        }
    }

    private async Task<ViewerConsumerWindowCloseCyclesResult> ExerciseWindowCloseCyclesAsync()
    {
        var coordinator = new ViewerConsumerWindowCloseObservationCoordinator(
            options.WindowCloseCycles,
            ViewerConsumerProcessResourceObserver.ReadNativeResources,
            lifecycleReport.AddLine);
        return await coordinator.RunAsync(ExerciseWindowCloseCycleAsync);
    }

    private async Task<ViewerConsumerWindowCloseCycleResult> ExerciseWindowCloseCycleAsync(int cycle)
    {
        var closeWindow = new Window
        {
            Title = $"OpenVisionLab 3D Viewer Window Close Cycle {cycle}",
            Width = 960,
            Height = 640,
            MinWidth = 640,
            MinHeight = 480,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        var viewer = CreateViewer();
        var closedCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var closingDisposed = false;
        var closed = false;
        Exception? closingException = null;
        closeWindow.Closing += (_, _) =>
        {
            try
            {
                viewer.Dispose();
                closingDisposed = true;
            }
            catch (Exception exception)
            {
                closingException = exception;
            }
        };
        closeWindow.Closed += (_, _) =>
        {
            closed = true;
            closedCompletion.TrySetResult(null);
        };

        var before = ViewerConsumerProcessResourceObserver.ReadNativeResources();
        var after = before;
        try
        {
            closeWindow.Content = viewer;
            closeWindow.Show();
            await WaitForLoadedAsync(viewer);
            await WaitForRenderReadyAsync(viewer);

            IOpenVisionThreeDViewerHost host = viewer;
            var c3dLoaded = await host.LoadC3DSourceAsync(options.C3DPath, CancellationToken.None);
            var c3dContract = await contractCapture.CaptureAsync(
                $"window-close-{cycle}-c3d",
                viewer.CaptureConfiguredSmokeViewAsync);
            var meshLoaded = await host.LoadViewerOnlySourceAsync(options.MeshPath, CancellationToken.None);
            await WaitForRenderReadyAsync(viewer);
            var meshContract = await contractCapture.CaptureAsync(
                $"window-close-{cycle}-mesh",
                viewer.CaptureConfiguredSmokeViewAsync);
            var hardware = options.RequireHardwareOpenGL
                ? contractAnalyzer.AnalyzeHardwareRenderPath(c3dContract, meshContract)
                : new ViewerConsumerHardwareRenderObservation(true, "hardwareRequired=False");

            closeWindow.Close();
            await closedCompletion.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var disposedContract = await contractCapture.CaptureAsync(
                $"window-close-{cycle}-disposed",
                viewer.CaptureConfiguredSmokeViewAsync);
            var resource = contractAnalyzer.AnalyzeResourceRetirement(disposedContract);
            after = ViewerConsumerProcessResourceObserver.ReadNativeResources();
            var passed = c3dLoaded
                && meshLoaded
                && hardware.Passed
                && closed
                && closingDisposed
                && closingException is null
                && resource.Passed;
            return new ViewerConsumerWindowCloseCycleResult(
                passed,
                before,
                after,
                $"closed={closed}|closingDisposed={closingDisposed}|closingException={closingException?.GetType().Name ?? "none"}|c3dLoaded={c3dLoaded}|meshLoaded={meshLoaded}|hardware={hardware.Passed}|resource={resource.Details}|nativeBefore={before}|nativeAfter={after}");
        }
        catch (Exception exception)
        {
            after = ViewerConsumerProcessResourceObserver.ReadNativeResources();
            return new ViewerConsumerWindowCloseCycleResult(
                false,
                before,
                after,
                $"closed={closed}|closingDisposed={closingDisposed}|closingException={closingException?.GetType().Name ?? "none"}|exception={exception.GetType().Name}:{exception.Message}|nativeBefore={before}|nativeAfter={after}");
        }
        finally
        {
            try
            {
                viewer.Dispose();
            }
            catch
            {
                // The close-cycle result already records the first disposal failure.
            }

            try
            {
                if (closeWindow.IsVisible)
                {
                    closeWindow.Close();
                }
            }
            catch
            {
                // The close-cycle result already records the first close failure.
            }

            closeWindow.Content = null;
        }
    }

    private static async Task WaitForLoadedAsync(OpenVisionThreeDViewerControl viewer)
    {
        if (!viewer.IsLoaded)
        {
            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            RoutedEventHandler? handler = null;
            handler = (_, _) =>
            {
                viewer.Loaded -= handler;
                completion.TrySetResult(null);
            };
            viewer.Loaded += handler;
            if (viewer.IsLoaded)
            {
                viewer.Loaded -= handler;
                completion.TrySetResult(null);
            }

            await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private static async Task WaitForRenderReadyAsync(OpenVisionThreeDViewerControl viewer)
    {
        viewer.RequestVisibleFrame();
        await viewer.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(180);
    }

    private static bool PathsEqual(string? first, string? second) =>
        first is not null
        && second is not null
        && string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            StringComparison.OrdinalIgnoreCase);

    private static bool HasContract(string content, string marker) =>
        content.Contains(marker, StringComparison.Ordinal);

    private sealed record SelectionOverlayObservation(bool Passed, string Details);

    private sealed record CameraObservation(bool Passed, string Details);

    private sealed record CloseReparentObservation(bool Passed, string Details);

    private sealed record GpuObservationBarrier(bool Passed, string Details);

    private sealed record DisposalObservation(
        bool Passed,
        string Details,
        bool ResourceRetirementPassed,
        string ResourceRetirementDetails);
}
