using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

internal sealed record ViewerConsumerLifecycleOptions(
    string ReportPath,
    string C3DPath,
    string MeshPath,
    string PointCloudPath,
    int RecreateCycles,
    int WindowCloseCycles,
    string? SmokeContractPath,
    bool RequireHardwareOpenGL,
    bool RequireImportedTextureRelease,
    string? GpuPostCloseObservationBarrierPath)
{
    public static ViewerConsumerLifecycleOptions Parse(
        string[] args,
        string reportPath)
    {
        return new ViewerConsumerLifecycleOptions(
            Path.GetFullPath(reportPath),
            GetRequiredPath(args, "--consumer-c3d"),
            GetRequiredPath(args, "--consumer-mesh"),
            GetRequiredPath(args, "--consumer-pointcloud"),
            GetCycleCount(args),
            GetWindowCloseCycles(args),
            GetOptionalPath(args, "--smoke-contracts"),
            HasFlag(args, "--consumer-require-hardware-opengl"),
            HasFlag(args, "--consumer-require-texture-release"),
            GetOptionalPath(args, "--consumer-gpu-post-close-observation-barrier"));
    }

    private static int GetCycleCount(string[] args)
    {
        var value = GetArgumentValue(args, "--consumer-lifecycle-recreate-count");
        if (value is null)
        {
            return 10;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles)
            || cycles < 10
            || cycles > 100)
        {
            throw new ArgumentException(
                "--consumer-lifecycle-recreate-count must be an integer from 10 through 100.",
                nameof(args));
        }

        return cycles;
    }

    private static int GetWindowCloseCycles(string[] args)
    {
        var value = GetArgumentValue(args, "--consumer-window-close-cycles");
        if (value is null)
        {
            return 0;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles)
            || cycles < 0
            || cycles > 20)
        {
            throw new ArgumentException(
                "--consumer-window-close-cycles must be an integer from 0 through 20.",
                nameof(args));
        }

        return cycles;
    }

    private static string GetRequiredPath(string[] args, string name)
    {
        var value = GetOptionalPath(args, name);
        return value ?? throw new ArgumentException(
            $"The independent consumer lifecycle requires {name}.",
            nameof(args));
    }

    private static string? GetOptionalPath(string[] args, string name)
    {
        var value = GetArgumentValue(args, name);
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
}

internal sealed class ViewerConsumerLifecycleRunner
{
    private const uint GdiObjectCount = 0;
    private const uint UserObjectCount = 1;

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr processHandle, uint flags);

    private readonly ViewerConsumerLifecycleOptions options;
    private readonly List<string> reportLines = [];
    private readonly List<string> contractPaths = [];
    private readonly Process process = Process.GetCurrentProcess();
    private Application? application;
    private Window? window;
    private OpenVisionThreeDViewerControl? currentViewer;
    private int totalChecks;
    private int failedChecks;
    private bool reportWritten;
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
            ValidateInputs();
            CollectForObservation();
            cleanProcessBaselinePrivateMemory = ReadPrivateMemoryBytes();
            cleanProcessBaselineManagedMemory = ReadManagedMemoryBytes();
            cleanProcessBaselineNativeResources = ReadNativeResources();
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
            RecordCheck("RunnerStartup", false, exception.ToString());
            WriteReport();
            exitCode = 1;
        }

        return exitCode;
    }

    private void ValidateInputs()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
        RequireFile(options.C3DPath, "C3D source");
        RequireFile(options.MeshPath, "mesh source");
        RequireFile(options.PointCloudPath, "point-cloud source");
        if (options.SmokeContractPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(options.SmokeContractPath)!);
        }
    }

    private static void RequireFile(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The {label} was not found.", path);
        }
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
            CollectForObservation();
            emptyWindowBaselinePrivateMemory = ReadPrivateMemoryBytes();
            emptyWindowBaselineManagedMemory = ReadManagedMemoryBytes();
            emptyWindowBaselineNativeResources = ReadNativeResources();
            currentViewer = CreateViewer();
            window.Content = currentViewer;
            await ExecuteAsync();
            exitCode = failedChecks == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            RecordCheck("RunnerExecution", false, exception.ToString());
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
                RecordCheck("FinalCleanup", false, exception.ToString());
                exitCode = 1;
            }

            try
            {
                window.Close();
            }
            catch (Exception exception)
            {
                RecordCheck("WindowClose", false, exception.ToString());
                exitCode = 1;
            }

            if (options.GpuPostCloseObservationBarrierPath is not null)
            {
                var barrier = await WaitForGpuObservationBarrierAsync();
                RecordCheck("GpuPostCloseObservationBarrier", barrier.Passed, barrier.Details);
            }

            exitCode = failedChecks == 0 ? 0 : 1;
            WriteReport();
            application.Shutdown(exitCode);
        }
    }

    private async Task ExecuteAsync()
    {
        var viewer = currentViewer
            ?? throw new InvalidOperationException("The initial Viewer control was not created.");
        await WaitForLoadedAsync(viewer);
        await WaitForRenderReadyAsync(viewer);

        reportLines.Add("OpenVisionLab 3D independent Viewer consumer lifecycle");
        reportLines.Add($"Generated={DateTimeOffset.Now:O}");
        reportLines.Add("ProjectReferences=0");
        reportLines.Add($"HostApiVersion={viewer.HostApiVersion}");
        reportLines.Add($"C3DSource={options.C3DPath}");
        reportLines.Add($"MeshSource={options.MeshPath}");
        reportLines.Add($"PointCloudSource={options.PointCloudPath}");
        reportLines.Add($"RequestedRecreateCycles={options.RecreateCycles}");
        reportLines.Add($"RequestedWindowCloseCycles={options.WindowCloseCycles}");
        reportLines.Add($"CleanProcessBaseline|privateBytes={cleanProcessBaselinePrivateMemory}|managedBytes={cleanProcessBaselineManagedMemory}|native={cleanProcessBaselineNativeResources}");
        reportLines.Add($"EmptyWindowBaseline|privateBytes={emptyWindowBaselinePrivateMemory}|managedBytes={emptyWindowBaselineManagedMemory}|native={emptyWindowBaselineNativeResources}");

        var c3dLoaded = await viewer.LoadC3DSourceAsync(options.C3DPath, CancellationToken.None);
        var c3dContract = await CaptureContractAsync(viewer, "height-map");
        RecordCheck(
            "HeightMapDisplay",
            c3dLoaded
                && PathsEqual(viewer.CurrentC3DSourcePath, options.C3DPath)
                && viewer.ViewModel.C3DSampleVisible
                && c3dContract.Contains("C3DMap|loaded=True", StringComparison.Ordinal)
                && c3dContract.Contains("C3DRenderProxy|loaded=True", StringComparison.Ordinal),
            $"loaded={c3dLoaded}|current={viewer.CurrentC3DSourcePath}|contract={HasContract(c3dContract, "C3DMap|loaded=True")}");

        var selectionOverlay = await ExerciseSelectionAndOverlayAsync(viewer);
        RecordCheck("SelectionAndOverlay", selectionOverlay.Passed, selectionOverlay.Details);
        _ = await CaptureContractAsync(viewer, "selection-overlay");

        var recipeSourceBeforeViewerOnly = viewer.CurrentC3DSourcePath;
        var meshLoaded = await viewer.LoadViewerOnlySourceAsync(options.MeshPath, CancellationToken.None);
        var meshContract = await CaptureContractAsync(viewer, "mesh");
        RecordCheck(
            "MeshDisplay",
            meshLoaded
                && PathsEqual(viewer.CurrentViewerOnlySourcePath, options.MeshPath)
                && string.Equals(viewer.CurrentViewerOnlySourceFormat, "GLB", StringComparison.Ordinal)
                && viewer.ViewModel.GlbSampleVisible
                && PathsEqual(viewer.CurrentC3DSourcePath, recipeSourceBeforeViewerOnly)
                && meshContract.Contains("GLB|loaded=True", StringComparison.Ordinal),
            $"loaded={meshLoaded}|format={viewer.CurrentViewerOnlySourceFormat}|recipeSourceRetained={PathsEqual(viewer.CurrentC3DSourcePath, recipeSourceBeforeViewerOnly)}");

        if (options.RequireHardwareOpenGL)
        {
            var hardware = AnalyzeHardwareRenderPath(c3dContract, meshContract);
            RecordCheck("HardwareOpenGLRenderPath", hardware.Passed, hardware.Details);
        }

        var pointCloudLoaded = await viewer.LoadViewerOnlySourceAsync(options.PointCloudPath, CancellationToken.None);
        var pointCloudContract = await CaptureContractAsync(viewer, "point-cloud");
        RecordCheck(
            "PointCloudDisplay",
            pointCloudLoaded
                && PathsEqual(viewer.CurrentViewerOnlySourcePath, options.PointCloudPath)
                && string.Equals(viewer.CurrentViewerOnlySourceFormat, "LAZ", StringComparison.Ordinal)
                && viewer.ViewModel.LazSampleVisible
                && PathsEqual(viewer.CurrentC3DSourcePath, recipeSourceBeforeViewerOnly)
                && pointCloudContract.Contains("LAZ|loaded=True", StringComparison.Ordinal)
                && pointCloudContract.Contains("decoder=points-decoded", StringComparison.Ordinal),
            $"loaded={pointCloudLoaded}|format={viewer.CurrentViewerOnlySourceFormat}|recipeSourceRetained={PathsEqual(viewer.CurrentC3DSourcePath, recipeSourceBeforeViewerOnly)}");

        var camera = ExerciseCamera(viewer);
        RecordCheck("CameraCaptureApply", camera.Passed, camera.Details);

        if (options.WindowCloseCycles > 0)
        {
            var closeCycles = await ExerciseWindowCloseCyclesAsync();
            RecordCheck(
                "WindowCloseCycles",
                closeCycles.Observed == options.WindowCloseCycles,
                $"observed={closeCycles.Observed}/{options.WindowCloseCycles}|nativeHandleDelta={closeCycles.NativeHandleDelta}|gdiDelta={closeCycles.GdiDelta}|userDelta={closeCycles.UserDelta}");
        }

        if (options.RequireHardwareOpenGL)
        {
            var closeReparent = await ExerciseCloseReparentCancellationAsync(viewer);
            RecordCheck("CloseReparentCancellation", closeReparent.Passed, closeReparent.Details);
        }

        var firstMemory = ReadPrivateMemoryBytes();
        var firstManagedMemory = ReadManagedMemoryBytes();
        var firstNativeResources = ReadNativeResources();
        var firstRemoval = await RemoveViewerAsync(disposeBeforeRemove: true);
        RecordCheck("RemoveAndDispose", firstRemoval.Passed, firstRemoval.Details);
        if (options.RequireHardwareOpenGL)
        {
            RecordCheck(
                "HardwareResourceRetirement",
                firstRemoval.ResourceRetirementPassed,
                firstRemoval.ResourceRetirementDetails);
        }

        var recreated = await AttachViewerAsync();
        var recreatedLoaded = await recreated.LoadViewerOnlySourceAsync(options.MeshPath, CancellationToken.None);
        await WaitForRenderReadyAsync(recreated);
        RecordCheck(
            "RecreateNewControl",
            recreatedLoaded
                && PathsEqual(recreated.CurrentViewerOnlySourcePath, options.MeshPath)
                && recreated.ViewModel.GlbSampleVisible,
            $"loaded={recreatedLoaded}|current={recreated.CurrentViewerOnlySourcePath}");

        var secondRemoval = await RemoveViewerAsync(disposeBeforeRemove: false);
        RecordCheck("RemoveThenDispose", secondRemoval.Passed, secondRemoval.Details);

        await RunRecreateCyclesAsync(firstMemory, firstManagedMemory, firstNativeResources);
        reportLines.Add($"Contracts={contractPaths.Count}");
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
            viewer.ViewModel.SelectedSelectionMode = "Point";
            viewer.ViewModel.SelectionOverlayVisible = true;
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
                && viewer.ViewModel.SelectionOverlayVisible;
            return new SelectionOverlayObservation(
                passed,
                $"published={published}|hoverEvents={hoverCount}|cursorValid={hoverCursor?.IsValid ?? false}|linkedCursor={viewer.LinkedHeightCursor is not null}|selectionOverlay={viewer.ViewModel.SelectionOverlayVisible}");
        }
        finally
        {
            viewer.C3DGridHoverChanged -= handler;
        }
    }

    private bool TryPublishFirstValidC3DCell(OpenVisionThreeDViewerControl viewer)
    {
        if (!viewer.TryGetCurrentC3DSourceBinding(options.C3DPath, out var binding))
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

    private static CameraObservation ExerciseCamera(OpenVisionThreeDViewerControl viewer)
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

        var resourceRetirement = AnalyzeResourceRetirement(
            await CaptureContractAsync(
                viewer,
                disposeBeforeRemove ? "disposed-before-remove" : "disposed-after-remove"));
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
        var cycleObservations = 0;
        var minimumPrivateMemory = long.MaxValue;
        var maximumPrivateMemory = 0L;
        var minimumManagedMemory = long.MaxValue;
        var maximumManagedMemory = 0L;
        var minimumNativeResources = firstNativeResources;
        var maximumNativeResources = firstNativeResources;
        for (var cycle = 1; cycle <= options.RecreateCycles; cycle++)
        {
            CollectForObservation();
            var beforePrivate = ReadPrivateMemoryBytes();
            var beforeManaged = ReadManagedMemoryBytes();
            var beforeNative = ReadNativeResources();
            minimumPrivateMemory = Math.Min(minimumPrivateMemory, beforePrivate);
            maximumPrivateMemory = Math.Max(maximumPrivateMemory, beforePrivate);
            minimumManagedMemory = Math.Min(minimumManagedMemory, beforeManaged);
            maximumManagedMemory = Math.Max(maximumManagedMemory, beforeManaged);
            minimumNativeResources = minimumNativeResources.Min(beforeNative);
            maximumNativeResources = maximumNativeResources.Max(beforeNative);
            var viewer = await AttachViewerAsync();
            var loaded = await viewer.LoadViewerOnlySourceAsync(options.MeshPath, CancellationToken.None);
            await WaitForRenderReadyAsync(viewer);
            var sourcePath = viewer.CurrentViewerOnlySourcePath;
            var dispose = await RemoveViewerAsync(disposeBeforeRemove: true);
            CollectForObservation();
            var afterPrivate = ReadPrivateMemoryBytes();
            var afterManaged = ReadManagedMemoryBytes();
            var afterNative = ReadNativeResources();
            minimumPrivateMemory = Math.Min(minimumPrivateMemory, afterPrivate);
            maximumPrivateMemory = Math.Max(maximumPrivateMemory, afterPrivate);
            minimumManagedMemory = Math.Min(minimumManagedMemory, afterManaged);
            maximumManagedMemory = Math.Max(maximumManagedMemory, afterManaged);
            minimumNativeResources = minimumNativeResources.Min(afterNative);
            maximumNativeResources = maximumNativeResources.Max(afterNative);
            reportLines.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"RecreateCycle|index={cycle}|loaded={loaded}|sourceMatch={PathsEqual(sourcePath, options.MeshPath)}|dispose={dispose.Passed}|privateBeforeBytes={beforePrivate}|privateAfterBytes={afterPrivate}|privateAfterMiB={afterPrivate / 1048576.0:F3}|managedBeforeBytes={beforeManaged}|managedAfterBytes={afterManaged}|managedAfterMiB={afterManaged / 1048576.0:F3}|nativeBefore={beforeNative}|nativeAfter={afterNative}"));
            if (loaded && PathsEqual(sourcePath, options.MeshPath) && dispose.Passed)
            {
                cycleObservations++;
            }
        }

        var finalPrivateMemory = ReadPrivateMemoryBytes();
        var finalManagedMemory = ReadManagedMemoryBytes();
        var finalNativeResources = ReadNativeResources();
        reportLines.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"MemoryObservation|baselineAfterDataPrivateBytes={firstPrivateMemory}|minimumCycleBeforePrivateBytes={minimumPrivateMemory}|maximumCycleObservedPrivateBytes={maximumPrivateMemory}|finalPrivateBytes={finalPrivateMemory}|privateDeltaFromBaselineMiB={(finalPrivateMemory - firstPrivateMemory) / 1048576.0:F3}|emptyWindowPrivateBytes={emptyWindowBaselinePrivateMemory}|privateDeltaFromEmptyWindowMiB={(finalPrivateMemory - emptyWindowBaselinePrivateMemory) / 1048576.0:F3}|cleanProcessPrivateBytes={cleanProcessBaselinePrivateMemory}|privateDeltaFromCleanProcessMiB={(finalPrivateMemory - cleanProcessBaselinePrivateMemory) / 1048576.0:F3}|baselineAfterDataManagedBytes={firstManagedMemory}|minimumCycleBeforeManagedBytes={minimumManagedMemory}|maximumCycleObservedManagedBytes={maximumManagedMemory}|finalManagedBytes={finalManagedMemory}|managedDeltaFromBaselineMiB={(finalManagedMemory - firstManagedMemory) / 1048576.0:F3}|emptyWindowManagedBytes={emptyWindowBaselineManagedMemory}|managedDeltaFromEmptyWindowMiB={(finalManagedMemory - emptyWindowBaselineManagedMemory) / 1048576.0:F3}|cleanProcessManagedBytes={cleanProcessBaselineManagedMemory}|managedDeltaFromCleanProcessMiB={(finalManagedMemory - cleanProcessBaselineManagedMemory) / 1048576.0:F3}|baselineNative={firstNativeResources}|minimumNativeObserved={minimumNativeResources}|maximumNativeObserved={maximumNativeResources}|finalNative={finalNativeResources}|nativeDelta={finalNativeResources.DeltaFrom(firstNativeResources)}|emptyWindowNativeDelta={finalNativeResources.DeltaFrom(emptyWindowBaselineNativeResources)}|cleanProcessNativeDelta={finalNativeResources.DeltaFrom(cleanProcessBaselineNativeResources)}|interpretation=observation-only-no-leak-free-claim"));
        RecordCheck(
            "RecreateCycles",
            cycleObservations == options.RecreateCycles,
            $"observed={cycleObservations}/{options.RecreateCycles}|privateDeltaMiB={(finalPrivateMemory - firstPrivateMemory) / 1048576.0:F3}|managedDeltaMiB={(finalManagedMemory - firstManagedMemory) / 1048576.0:F3}|nativeDelta={finalNativeResources.DeltaFrom(firstNativeResources)}|emptyWindowNativeDelta={finalNativeResources.DeltaFrom(emptyWindowBaselineNativeResources)}");
    }

    private async Task<GpuObservationBarrier> WaitForGpuObservationBarrierAsync()
    {
        var readyPath = options.GpuPostCloseObservationBarrierPath
            ?? throw new InvalidOperationException("GPU observation barrier path is not configured.");
        var continuePath = readyPath + ".continue";
        if (File.Exists(continuePath))
        {
            return new GpuObservationBarrier(false, "ready=False|continued=False|reason=stale-continue-file");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(readyPath)!);
        var native = ReadNativeResources();
        File.WriteAllText(
            readyPath,
            $"pid={process.Id}|privateBytes={ReadPrivateMemoryBytes()}|native={native}");
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!File.Exists(continuePath) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        var continued = File.Exists(continuePath);
        return new GpuObservationBarrier(
            continued,
            $"ready=True|continued={continued}|pid={process.Id}|native={native}");
    }

    private NativeResourceSnapshot ReadNativeResources()
    {
        try
        {
            process.Refresh();
            var processHandle = process.Handle;
            return new NativeResourceSnapshot(
                process.HandleCount,
                GetGuiResources(processHandle, GdiObjectCount),
                GetGuiResources(processHandle, UserObjectCount));
        }
        catch
        {
            return new NativeResourceSnapshot(-1, -1, -1);
        }
    }

    private async Task<string> CaptureContractAsync(
        OpenVisionThreeDViewerControl viewer,
        string stage)
    {
        if (options.SmokeContractPath is null)
        {
            reportLines.Add($"Contract|stage={stage}|captured=False|reason=no-smoke-contract-path");
            return string.Empty;
        }

        var captured = await viewer.CaptureConfiguredSmokeViewAsync();
        var path = options.SmokeContractPath;
        var content = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var stagePath = Path.Combine(
            Path.GetDirectoryName(options.ReportPath)!,
            $"viewer-consumer-{stage}-contract.txt");
        if (File.Exists(path))
        {
            File.Copy(path, stagePath, overwrite: true);
            contractPaths.Add(stagePath);
        }

        reportLines.Add($"Contract|stage={stage}|captured={content.Length > 0}|smokeResult={captured}|path={stagePath}");
        return content;
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
            var loadTask = transientViewer.LoadViewerOnlySourceAsync(
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
            var noStaleSource = transientViewer.CurrentViewerOnlySourcePath is null;
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

    private async Task<WindowCloseCyclesObservation> ExerciseWindowCloseCyclesAsync()
    {
        var observed = 0;
        var first = ReadNativeResources();
        var last = first;
        for (var cycle = 1; cycle <= options.WindowCloseCycles; cycle++)
        {
            var observation = await ExerciseWindowCloseCycleAsync(cycle);
            if (observation.Passed)
            {
                observed++;
            }

            last = observation.After;
            reportLines.Add($"WindowCloseCycle|index={cycle}|{Sanitize(observation.Details)}");
        }

        return new WindowCloseCyclesObservation(
            observed,
            last.HandleCount - first.HandleCount,
            last.GdiObjects - first.GdiObjects,
            last.UserObjects - first.UserObjects);
    }

    private async Task<WindowCloseCycleObservation> ExerciseWindowCloseCycleAsync(int cycle)
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

        var before = ReadNativeResources();
        var after = before;
        try
        {
            closeWindow.Content = viewer;
            closeWindow.Show();
            await WaitForLoadedAsync(viewer);
            await WaitForRenderReadyAsync(viewer);

            var c3dLoaded = await viewer.LoadC3DSourceAsync(options.C3DPath, CancellationToken.None);
            var c3dContract = await CaptureContractAsync(viewer, $"window-close-{cycle}-c3d");
            var meshLoaded = await viewer.LoadViewerOnlySourceAsync(options.MeshPath, CancellationToken.None);
            await WaitForRenderReadyAsync(viewer);
            var meshContract = await CaptureContractAsync(viewer, $"window-close-{cycle}-mesh");
            var hardware = options.RequireHardwareOpenGL
                ? AnalyzeHardwareRenderPath(c3dContract, meshContract)
                : new HardwareRenderObservation(true, "hardwareRequired=False");

            closeWindow.Close();
            await closedCompletion.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var disposedContract = await CaptureContractAsync(viewer, $"window-close-{cycle}-disposed");
            var resource = AnalyzeResourceRetirement(disposedContract);
            after = ReadNativeResources();
            var passed = c3dLoaded
                && meshLoaded
                && hardware.Passed
                && closed
                && closingDisposed
                && closingException is null
                && resource.Passed;
            return new WindowCloseCycleObservation(
                passed,
                before,
                after,
                $"closed={closed}|closingDisposed={closingDisposed}|closingException={closingException?.GetType().Name ?? "none"}|c3dLoaded={c3dLoaded}|meshLoaded={meshLoaded}|hardware={hardware.Passed}|resource={resource.Details}|nativeBefore={before}|nativeAfter={after}");
        }
        catch (Exception exception)
        {
            after = ReadNativeResources();
            return new WindowCloseCycleObservation(
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

    private HardwareRenderObservation AnalyzeHardwareRenderPath(
        string c3dContract,
        string meshContract)
    {
        var capabilities = GetContractLine(c3dContract, "OpenGLCapabilities|");
        var renderProxy = GetContractLine(c3dContract, "C3DRenderProxy|loaded=True|");
        var c3dHardware = capabilities is not null
            && !capabilities.Contains("renderer=GDI Generic", StringComparison.Ordinal)
            && !capabilities.Contains("renderer=(pending)", StringComparison.Ordinal)
            && capabilities.Contains("c3dPath=VBO+IBO+DrawElements", StringComparison.Ordinal)
            && capabilities.Contains("fallbacks=0", StringComparison.Ordinal)
            && renderProxy is not null
            && renderProxy.Contains("gpuBufferReady=True", StringComparison.Ordinal);
        var meshLine = GetContractLine(meshContract, "GLB|loaded=True|");
        var textureUploaded = !options.RequireImportedTextureRelease
            || (meshLine is not null
                && string.Equals(
                    GetContractFieldValue(meshLine, "hasTexture"),
                    "True",
                    StringComparison.OrdinalIgnoreCase)
                && GetContractInt(meshLine, "textureUploads") > 0);
        var passed = c3dHardware && textureUploaded;
        return new HardwareRenderObservation(
            passed,
            $"c3dHardware={c3dHardware}|textureUploaded={textureUploaded}|capabilities={capabilities ?? "missing"}|renderProxy={renderProxy ?? "missing"}|mesh={meshLine ?? "missing"}");
    }

    private ResourceRetirementObservation AnalyzeResourceRetirement(string contract)
    {
        var line = GetContractLine(contract, "OpenGLResourceLifetime|");
        if (!options.RequireHardwareOpenGL)
        {
            return new ResourceRetirementObservation(
                true,
                $"hardwareRequired=False|contract={line ?? "missing"}");
        }

        if (line is null)
        {
            return new ResourceRetirementObservation(
                false,
                "hardwareRequired=True|contract=missing");
        }

        var passed = GetContractBool(line, "disposed")
            && GetContractBool(line, "managedHandlesCleared")
            && GetContractInt(line, "c3dGpuReleases") > 0
            && GetContractInt(line, "c3dGpuReleaseFailures") == 0
            && GetContractInt(line, "meshTextureReleases") >= (options.RequireImportedTextureRelease ? 1 : 0)
            && GetContractInt(line, "meshTextureReleaseFailures") == 0
            && GetContractInt(line, "displayListReleaseFailures") == 0
            && GetContractInt(line, "retirementAttempts") > 0
            && GetContractInt(line, "retirementCallbacks") > 0
            && GetContractInt(line, "retirementContextUnavailable") == 0
            && GetContractInt(line, "retirementFailures") == 0;
        passed = passed
            && GetContractBool(line, "renderContextDisposeAttempted")
            && GetContractBool(line, "renderContextDisposed")
            && GetContractInt(line, "renderContextDisposeAttempts") == 1
            && GetContractInt(line, "renderContextDisposeFailures") == 0
            && !GetContractBool(line, "renderContextHandleActive");
        return new ResourceRetirementObservation(
            passed,
            $"hardwareRequired=True|contract={line ?? "missing"}");
    }

    private static string? GetContractLine(string content, string prefix) =>
        content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));

    private static string? GetContractFieldValue(string line, string field)
    {
        foreach (var segment in line.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.StartsWith(field + "=", StringComparison.Ordinal))
            {
                return segment[(field.Length + 1)..];
            }
        }

        return null;
    }

    private static int GetContractInt(string line, string field) =>
        int.TryParse(
            GetContractFieldValue(line, field),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;

    private static bool GetContractBool(string line, string field) =>
        bool.TryParse(GetContractFieldValue(line, field), out var value) && value;

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

    private void RecordCheck(string name, bool passed, string details)
    {
        totalChecks++;
        if (!passed)
        {
            failedChecks++;
        }

        reportLines.Add($"Check|name={name}|pass={passed}|{Sanitize(details)}");
    }

    private void WriteReport()
    {
        if (reportWritten)
        {
            return;
        }

        reportWritten = true;
        reportLines.Add($"Result|{(failedChecks == 0 ? "Pass" : "Fail")}|checks={totalChecks - failedChecks}/{totalChecks}|failed={failedChecks}");
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
        File.WriteAllLines(options.ReportPath, reportLines);
    }

    private static long ReadPrivateMemoryBytes()
    {
        var current = Process.GetCurrentProcess();
        current.Refresh();
        return current.PrivateMemorySize64;
    }

    private static long ReadManagedMemoryBytes() => GC.GetTotalMemory(forceFullCollection: false);

    private static void CollectForObservation()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
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

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');

    private sealed record SelectionOverlayObservation(bool Passed, string Details);

    private sealed record CameraObservation(bool Passed, string Details);

    private sealed record CloseReparentObservation(bool Passed, string Details);

    private sealed record HardwareRenderObservation(bool Passed, string Details);

    private sealed record ResourceRetirementObservation(bool Passed, string Details);

    private sealed record NativeResourceSnapshot(
        long HandleCount,
        long GdiObjects,
        long UserObjects)
    {
        public NativeResourceSnapshot Min(NativeResourceSnapshot other) =>
            new(
                Math.Min(HandleCount, other.HandleCount),
                Math.Min(GdiObjects, other.GdiObjects),
                Math.Min(UserObjects, other.UserObjects));

        public NativeResourceSnapshot Max(NativeResourceSnapshot other) =>
            new(
                Math.Max(HandleCount, other.HandleCount),
                Math.Max(GdiObjects, other.GdiObjects),
                Math.Max(UserObjects, other.UserObjects));

        public string DeltaFrom(NativeResourceSnapshot baseline) =>
            $"handles={HandleCount - baseline.HandleCount},gdi={GdiObjects - baseline.GdiObjects},user={UserObjects - baseline.UserObjects}";
    }

    private sealed record WindowCloseCycleObservation(
        bool Passed,
        NativeResourceSnapshot Before,
        NativeResourceSnapshot After,
        string Details);

    private sealed record WindowCloseCyclesObservation(
        int Observed,
        long NativeHandleDelta,
        long GdiDelta,
        long UserDelta);

    private sealed record GpuObservationBarrier(bool Passed, string Details);

    private sealed record DisposalObservation(
        bool Passed,
        string Details,
        bool ResourceRetirementPassed,
        string ResourceRetirementDetails);
}
