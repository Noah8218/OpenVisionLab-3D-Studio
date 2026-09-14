using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

internal static class ViewerConsumerBoundaryVerification
{
    public static int Run()
    {
        var checks = new List<(string Name, bool Passed)>
        {
            ("HardwareContract", VerifyHardwareContract()),
            ("HardwareContractRejectsMissingRenderer", VerifyHardwareContractRejectsMissingRenderer()),
            ("ResourceRetirementContract", VerifyResourceRetirementContract()),
            ("ResourceRetirementContractRejectsMissingLine", VerifyResourceRetirementContractRejectsMissingLine()),
            ("SnapshotArithmetic", VerifySnapshotArithmetic()),
            ("ProcessObservation", VerifyProcessObservation()),
            ("ContractCaptureWithoutPath", VerifyContractCaptureWithoutPath()),
            ("HostApiSourceAndSelectionSurface", VerifyHostApiSourceAndSelectionSurface()),
            ("HostApiReportPolicy", VerifyHostApiReportPolicy()),
            ("MemoryObservationPolicy", VerifyMemoryObservationPolicy()),
            ("LifecycleOptionsPolicy", VerifyLifecycleOptionsPolicy()),
            ("LifecycleReportPolicy", VerifyLifecycleReportPolicy()),
            ("WindowCloseObservationPolicy", VerifyWindowCloseObservationPolicy()),
            ("GpuObservationBarrierPolicy", VerifyGpuObservationBarrierPolicy()),
            ("InputPreconditionPolicy", VerifyInputPreconditionPolicy())
        };

        foreach (var check in checks)
        {
            Console.WriteLine($"Check|name={check.Name}|pass={check.Passed}");
        }

        var passed = checks.Count(check => check.Passed);
        Console.WriteLine($"ViewerConsumerBoundaryVerification|{(passed == checks.Count ? "Pass" : "Fail")}|checks={passed}/{checks.Count}");
        return passed == checks.Count ? 0 : 1;
    }

    private static bool VerifyHardwareContract()
    {
        var analyzer = new ViewerConsumerContractAnalyzer(
            requireHardwareOpenGL: true,
            requireImportedTextureRelease: true);
        var result = analyzer.AnalyzeHardwareRenderPath(
            "OpenGLCapabilities|renderer=NVIDIA|c3dPath=VBO+IBO+DrawElements|fallbacks=0\nC3DRenderProxy|loaded=True|gpuBufferReady=True|",
            "GLB|loaded=True|hasTexture=True|textureUploads=1|");
        return result.Passed;
    }

    private static bool VerifyHardwareContractRejectsMissingRenderer()
    {
        var analyzer = new ViewerConsumerContractAnalyzer(
            requireHardwareOpenGL: true,
            requireImportedTextureRelease: false);
        var result = analyzer.AnalyzeHardwareRenderPath(
            "OpenGLCapabilities|renderer=GDI Generic|c3dPath=VBO+IBO+DrawElements|fallbacks=0\nC3DRenderProxy|loaded=True|gpuBufferReady=True|",
            "GLB|loaded=True|");
        return !result.Passed;
    }

    private static bool VerifyResourceRetirementContract()
    {
        var analyzer = new ViewerConsumerContractAnalyzer(
            requireHardwareOpenGL: true,
            requireImportedTextureRelease: true);
        var result = analyzer.AnalyzeResourceRetirement(
            "OpenGLResourceLifetime|disposed=True|managedHandlesCleared=True|c3dGpuReleases=1|c3dGpuReleaseFailures=0|meshTextureReleases=1|meshTextureReleaseFailures=0|displayListReleaseFailures=0|retirementAttempts=1|retirementCallbacks=3|retirementContextUnavailable=0|retirementFailures=0|renderContextDisposeAttempted=True|renderContextDisposed=True|renderContextDisposeAttempts=1|renderContextDisposeFailures=0|renderContextHandleActive=False|");
        return result.Passed;
    }

    private static bool VerifyResourceRetirementContractRejectsMissingLine()
    {
        var analyzer = new ViewerConsumerContractAnalyzer(
            requireHardwareOpenGL: true,
            requireImportedTextureRelease: false);
        return !analyzer.AnalyzeResourceRetirement(string.Empty).Passed;
    }

    private static bool VerifySnapshotArithmetic()
    {
        var first = new NativeResourceSnapshot(10, 2, 3);
        var second = new NativeResourceSnapshot(14, 1, 7);
        var minimum = first.Min(second);
        var maximum = first.Max(second);
        return minimum == new NativeResourceSnapshot(10, 1, 3)
            && maximum == new NativeResourceSnapshot(14, 2, 7)
            && string.Equals(first.DeltaFrom(second), "handles=-4,gdi=1,user=-4", StringComparison.Ordinal);
    }

    private static bool VerifyProcessObservation()
    {
        var managed = ViewerConsumerProcessResourceObserver.ReadManagedMemoryBytes();
        var native = ViewerConsumerProcessResourceObserver.ReadNativeResources();
        return ViewerConsumerProcessResourceObserver.ProcessId == Environment.ProcessId
            && managed >= 0
            && native is not null;
    }

    private static bool VerifyContractCaptureWithoutPath()
    {
        var reportLines = new List<string>();
        var callbackCalled = false;
        var coordinator = new ViewerConsumerContractCaptureCoordinator(
            smokeContractPath: null,
            reportPath: "report.txt",
            reportLines.Add);
        var content = coordinator.CaptureAsync(
                "no-path",
                () =>
                {
                    callbackCalled = true;
                    return Task.FromResult(true);
                })
            .GetAwaiter()
            .GetResult();
        return !callbackCalled
            && content.Length == 0
            && coordinator.ContractCount == 0
            && reportLines.Count == 1
            && reportLines[0].Contains("reason=no-smoke-contract-path", StringComparison.Ordinal);
    }

    private static bool VerifyHostApiReportPolicy()
    {
        var reportPath = Path.Combine(
            "E:\\OpenVisionLab-TestData\\OpenVisionLab-3D-Studio\\binaryhost-boundary",
            $"host-api-report-{Guid.NewGuid():N}.txt");
        try
        {
            var host = new FakeViewerHost();
            var coordinator = new ViewerConsumerHostApiReportCoordinator(reportPath, host);
            coordinator.ObserveStateChanged(
                new ViewerHostStateChangedEventArgs(host.HostState, nameof(ViewerHostState.ViewerStatus)));
            coordinator.ObserveStateChanged(
                new ViewerHostStateChangedEventArgs(host.HostState, nameof(ViewerHostState.ActiveEntity)));
            coordinator.WriteReport(recipeSaved: true, recipePath: "recipe.json");
            var lines = File.ReadAllLines(reportPath);
            return lines.SequenceEqual(
            [
                "HostApi|version=1.0-test",
                "HostState|activeEntity=FakeEntity|selectionMode=Point|viewerStatus=Ready",
                "HostEvents|count=2|lastProperty=ActiveEntity",
                "HostLifecycle|concreteDisposable=True|disposedAfterRun=True",
                "HostCommands|invoked=ResetView,FitAll,FitSelection|saveRecipe=True|recipePath=recipe.json"
            ]);
        }
        finally
        {
            if (File.Exists(reportPath))
            {
                File.Delete(reportPath);
            }
        }
    }

    private static bool VerifyHostApiSourceAndSelectionSurface()
    {
        var hostType = typeof(IOpenVisionThreeDViewerHost);
        var stateType = typeof(ViewerHostState);
        var fake = new FakeViewerHost();
        var state = fake.HostState;
        return hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.LoadC3DSource)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.LoadC3DSourceAsync)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.LoadViewerOnlySourceAsync)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TryGetCurrentC3DSourceBinding)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.CaptureCameraState)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TryApplyCameraState)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetSelectionMode)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetSelectionOverlayVisible)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetHudDetailsVisible)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetC3DSampleVisible)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetC3DHeightColorMinimumRaw)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetC3DHeightColorMaximumRaw)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TryResetC3DHeightColorRange)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TryApplyLinkedC3DHeightColorRange)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetSelectedColorMap)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetSelectedDiagnosticChannel)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetResultOverlayVisible)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TrySetMeasurementVisible)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TryShiftC3DHeightColorMinimum)) is not null
            && hostType.GetMethod(nameof(IOpenVisionThreeDViewerHost.TryShiftC3DHeightColorMaximum)) is not null
            && stateType.GetProperty(nameof(ViewerHostState.Sources)) is not null
            && stateType.GetProperty(nameof(ViewerHostState.Selection)) is not null
            && stateType.GetProperty(nameof(ViewerHostState.Presentation)) is not null
            && state.Sources == new ViewerHostSourceState("c3d", "mesh", "GLB")
            && state.Selection == new ViewerHostSelectionState("FakeEntity", "Point", "(1,2,3)", true)
            && fake.CaptureCameraState().IsValid
            && fake.TrySetSelectionMode("Point")
            && fake.TrySetSelectionOverlayVisible(true)
            && fake.TrySetHudDetailsVisible(false)
            && fake.TrySetC3DSampleVisible(true)
            && fake.TrySetC3DHeightColorMinimumRaw(0.0)
            && fake.TrySetC3DHeightColorMaximumRaw(1.0)
            && fake.TryResetC3DHeightColorRange()
            && fake.TryApplyLinkedC3DHeightColorRange(0.0, 1.0)
            && fake.TrySetSelectedColorMap("Viridis")
            && fake.TrySetSelectedDiagnosticChannel(null)
            && fake.TrySetResultOverlayVisible(true)
            && fake.TrySetMeasurementVisible(true)
            && fake.TryShiftC3DHeightColorMinimum(1)
            && fake.TryShiftC3DHeightColorMaximum(-1);
    }

    private static bool VerifyMemoryObservationPolicy()
    {
        var reportLines = new List<string>();
        var coordinator = new ViewerConsumerMemoryObservationCoordinator(
            cycleCount: 2,
            baselinePrivateMemory: 100,
            baselineManagedMemory: 50,
            baselineNativeResources: new NativeResourceSnapshot(10, 2, 3),
            emptyWindowPrivateMemory: 90,
            emptyWindowManagedMemory: 40,
            emptyWindowNativeResources: new NativeResourceSnapshot(9, 1, 2),
            cleanProcessPrivateMemory: 80,
            cleanProcessManagedMemory: 30,
            cleanProcessNativeResources: new NativeResourceSnapshot(8, 1, 1),
            reportLines.Add);
        var result = coordinator.RunAsync(
                _ => Task.FromResult(new ViewerConsumerMemoryCycleResult(
                    Loaded: true,
                    SourceMatch: true,
                    Disposed: true)))
            .GetAwaiter()
            .GetResult();
        return result.Passed
            && result.Details.Contains("observed=2/2", StringComparison.Ordinal)
            && reportLines.Count == 3
            && reportLines[0].StartsWith(
                "RecreateCycle|index=1|loaded=True|sourceMatch=True|dispose=True|",
                StringComparison.Ordinal)
            && reportLines[1].StartsWith(
                "RecreateCycle|index=2|loaded=True|sourceMatch=True|dispose=True|",
                StringComparison.Ordinal)
            && reportLines[2].Contains(
                "interpretation=observation-only-no-leak-free-claim",
                StringComparison.Ordinal);
    }

    private static bool VerifyLifecycleOptionsPolicy()
    {
        var defaults = ViewerConsumerLifecycleOptions.Parse(
        [
            "--consumer-c3d", "height.c3d",
            "--consumer-mesh", "mesh.glb",
            "--consumer-pointcloud", "points.laz"
        ],
        "report.txt");
        var explicitOptions = ViewerConsumerLifecycleOptions.Parse(
        [
            "--consumer-c3d", "height.c3d",
            "--consumer-mesh", "mesh.glb",
            "--consumer-pointcloud", "points.laz",
            "--consumer-lifecycle-recreate-count", "15",
            "--consumer-window-close-cycles", "2",
            "--smoke-contracts", "contracts.txt",
            "--consumer-require-hardware-opengl",
            "--consumer-require-texture-release",
            "--consumer-gpu-post-close-observation-barrier", "ready.txt"
        ],
        "explicit-report.txt");
        var invalidRejected = false;
        try
        {
            _ = ViewerConsumerLifecycleOptions.Parse(
            [
                "--consumer-c3d", "height.c3d",
                "--consumer-mesh", "mesh.glb",
                "--consumer-pointcloud", "points.laz",
                "--consumer-lifecycle-recreate-count", "9"
            ],
            "invalid-report.txt");
        }
        catch (ArgumentException exception)
        {
            invalidRejected = exception.Message.Contains(
                "must be an integer from 10 through 100",
                StringComparison.Ordinal);
        }

        return defaults.RecreateCycles == 10
            && defaults.WindowCloseCycles == 0
            && defaults.SmokeContractPath is null
            && !defaults.RequireHardwareOpenGL
            && !defaults.RequireImportedTextureRelease
            && defaults.GpuPostCloseObservationBarrierPath is null
            && Path.IsPathFullyQualified(defaults.ReportPath)
            && Path.IsPathFullyQualified(defaults.C3DPath)
            && explicitOptions.RecreateCycles == 15
            && explicitOptions.WindowCloseCycles == 2
            && explicitOptions.SmokeContractPath is not null
            && explicitOptions.RequireHardwareOpenGL
            && explicitOptions.RequireImportedTextureRelease
            && explicitOptions.GpuPostCloseObservationBarrierPath is not null
            && invalidRejected;
    }

    private static bool VerifyLifecycleReportPolicy()
    {
        var reportPath = Path.Combine(
            "E:\\OpenVisionLab-TestData\\OpenVisionLab-3D-Studio\\binaryhost-boundary",
            $"lifecycle-report-{Guid.NewGuid():N}.txt");
        try
        {
            var coordinator = new ViewerConsumerLifecycleReportCoordinator(reportPath);
            coordinator.AddLine("Header");
            coordinator.RecordCheck("Pass", true, "ok");
            coordinator.RecordCheck("Fail", false, "line\r\npipe|value");
            coordinator.AddSanitizedLine("WindowCloseCycle|index=1|", "line\r\npipe|value");
            coordinator.Write();
            coordinator.Write();
            var lines = File.ReadAllLines(reportPath);
            return coordinator.FailedChecks == 1
                && lines.SequenceEqual(
                [
                    "Header",
                    "Check|name=Pass|pass=True|ok",
                    "Check|name=Fail|pass=False|line  pipe/value",
                    "WindowCloseCycle|index=1|line  pipe/value",
                    "Result|Fail|checks=1/2|failed=1"
                ]);
        }
        finally
        {
            if (File.Exists(reportPath))
            {
                File.Delete(reportPath);
            }
        }
    }

    private static bool VerifyWindowCloseObservationPolicy()
    {
        var reportLines = new List<string>();
        var snapshots = new Queue<NativeResourceSnapshot>(
        [
            new NativeResourceSnapshot(10, 2, 3)
        ]);
        var coordinator = new ViewerConsumerWindowCloseObservationCoordinator(
            cycleCount: 2,
            readNativeResources: () => snapshots.Dequeue(),
            reportLines.Add);
        var result = coordinator.RunAsync(
                cycle => Task.FromResult(
                    new ViewerConsumerWindowCloseCycleResult(
                        Passed: cycle == 1,
                        Before: new NativeResourceSnapshot(10 + cycle, 2, 3),
                        After: new NativeResourceSnapshot(12 + cycle, 3 + cycle, 5 + cycle),
                        Details: $"closed={cycle == 1}|pipe|line\r\nvalue")))
            .GetAwaiter()
            .GetResult();
        return result.Observed == 1
            && result.NativeHandleDelta == 4
            && result.GdiDelta == 3
            && result.UserDelta == 4
            && reportLines.SequenceEqual(
            [
                "WindowCloseCycle|index=1|closed=True/pipe/line  value",
                "WindowCloseCycle|index=2|closed=False/pipe/line  value"
            ]);
    }

    private static bool VerifyGpuObservationBarrierPolicy()
    {
        var root = Path.Combine(
            "E:\\OpenVisionLab-TestData\\OpenVisionLab-3D-Studio\\binaryhost-boundary",
            $"gpu-barrier-{Guid.NewGuid():N}");
        var staleReadyPath = Path.Combine(root, "stale-ready.txt");
        var staleContinuePath = staleReadyPath + ".continue";
        var liveReadyPath = Path.Combine(root, "live-ready.txt");
        var liveContinuePath = liveReadyPath + ".continue";
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(staleContinuePath, "stale");
            var stale = new ViewerConsumerGpuObservationBarrierCoordinator(
                staleReadyPath,
                processId: 7,
                readPrivateMemoryBytes: () => 123,
                readNativeResources: () => new NativeResourceSnapshot(1, 2, 3))
                .WaitAsync()
                .GetAwaiter()
                .GetResult();

            var live = new ViewerConsumerGpuObservationBarrierCoordinator(
                liveReadyPath,
                processId: 8,
                readPrivateMemoryBytes: () =>
                {
                    File.WriteAllText(liveContinuePath, "continue");
                    return 456;
                },
                readNativeResources: () => new NativeResourceSnapshot(4, 5, 6))
                .WaitAsync()
                .GetAwaiter()
                .GetResult();
            var readyContent = File.ReadAllText(liveReadyPath);
            return !stale.Passed
                && stale.Details.Contains("reason=stale-continue-file", StringComparison.Ordinal)
                && !File.Exists(staleReadyPath)
                && live.Passed
                && live.Details.Contains("continued=True", StringComparison.Ordinal)
                && readyContent.Contains("pid=8|privateBytes=456|native=", StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static bool VerifyInputPreconditionPolicy()
    {
        var root = Path.Combine(
            "E:\\OpenVisionLab-TestData\\OpenVisionLab-3D-Studio\\binaryhost-boundary",
            $"input-preconditions-{Guid.NewGuid():N}");
        var reportPath = Path.Combine(root, "reports", "run.txt");
        var smokePath = Path.Combine(root, "smoke", "contracts.txt");
        var c3DPath = Path.Combine(root, "source", "height.c3d");
        var meshPath = Path.Combine(root, "source", "mesh.glb");
        var pointCloudPath = Path.Combine(root, "source", "points.laz");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(c3DPath)!);
            File.WriteAllText(c3DPath, "c3d");
            File.WriteAllText(meshPath, "mesh");
            File.WriteAllText(pointCloudPath, "points");
            new ViewerConsumerInputPreconditionValidator(
                reportPath,
                c3DPath,
                meshPath,
                pointCloudPath,
                smokePath)
                .Validate();

            var missingPath = Path.Combine(root, "source", "missing.glb");
            var missingFailure = false;
            try
            {
                new ViewerConsumerInputPreconditionValidator(
                    reportPath,
                    c3DPath,
                    missingPath,
                    pointCloudPath,
                    smokePath)
                    .Validate();
            }
            catch (FileNotFoundException exception)
            {
                missingFailure = exception.FileName == missingPath
                    && exception.Message.Contains("mesh source was not found", StringComparison.Ordinal);
            }

            return Directory.Exists(Path.GetDirectoryName(reportPath)!)
                && Directory.Exists(Path.GetDirectoryName(smokePath)!)
                && missingFailure;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FakeViewerHost : IOpenVisionThreeDViewerHost
    {
        public string HostApiVersion => "1.0-test";

        public ViewerHostState HostState => new(
            C3DSampleVisible: true,
            ActiveEntity: "FakeEntity",
            SelectionMode: "Point",
            PickCoordinate: "(1,2,3)",
            MeasurementSummary: "measurement",
            ResultSummary: "result",
            RecipeSummary: "recipe",
            ViewerStatus: "Ready",
            CoordinateFrameSummary: "Scene")
        {
            GlbSampleVisible = true,
            LazSampleVisible = false,
            SelectionOverlayVisible = true,
            Sources = new ViewerHostSourceState("c3d", "mesh", "GLB")
        };

        public event EventHandler<ViewerHostStateChangedEventArgs>? HostStateChanged
        {
            add { }
            remove { }
        }

        public void FitAll()
        {
        }

        public void FitSelection()
        {
        }

        public void ResetView()
        {
        }

        public bool SaveRecipe(string path) => true;

        public bool TrySetHudDetailsVisible(bool visible) => true;

        public bool TrySetC3DSampleVisible(bool visible) => true;

        public bool TrySetC3DHeightColorMinimumRaw(double value) => true;

        public bool TrySetC3DHeightColorMaximumRaw(double value) => true;

        public bool TryResetC3DHeightColorRange() => true;

        public bool TryApplyLinkedC3DHeightColorRange(double minimum, double maximum) => true;

        public bool TrySetSelectedColorMap(string colorMap) => !string.IsNullOrWhiteSpace(colorMap);

        public bool TrySetSelectedDiagnosticChannel(ViewerDiagnosticChannelOption? channel) => true;

        public bool TrySetResultOverlayVisible(bool visible) => true;

        public bool TrySetMeasurementVisible(bool visible) => true;

        public bool TryShiftC3DHeightColorMinimum(int direction) => direction != 0;

        public bool TryShiftC3DHeightColorMaximum(int direction) => direction != 0;

        public bool LoadC3DSource(string path) => true;

        public Task<bool> LoadC3DSourceAsync(
            string path,
            CancellationToken cancellationToken = default,
            IProgress<double>? progress = null) =>
            Task.FromResult(true);

        public Task<bool> LoadViewerOnlySourceAsync(
            string path,
            CancellationToken cancellationToken = default,
            IProgress<double>? progress = null) =>
            Task.FromResult(true);

        public bool TryGetCurrentC3DSourceBinding(
            string path,
            out ToolRecipeSelectionSourceBinding binding)
        {
            binding = new ToolRecipeSelectionSourceBinding("C3D", "fake", 1, 1);
            return true;
        }

        public ViewerCameraState CaptureCameraState() =>
            new(0.0, 0.0, 1.0, 0.0, 0.0, 0.0, ViewerProjectionMode.Perspective, 1.0);

        public bool TryApplyCameraState(ViewerCameraState state) => state.IsValid;

        public bool TrySetSelectionMode(string selectionMode) => !string.IsNullOrWhiteSpace(selectionMode);

        public bool TrySetSelectionOverlayVisible(bool visible) => true;
    }
}
