using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Loading;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

/// <summary>
/// Measures the existing C3D raw, derived/encode, and CPU render-copy owners
/// on deterministic scale fixtures. This is an observation harness; it does
/// not establish a product budget or perform context-bound OpenGL uploads.
/// </summary>
internal static class C3DMemoryMeasurementVerification
{
    private const int MaxRenderedPoints = 55_000;
    private const int SampleBufferBytes = 64 * 1024;
    private static readonly (int Width, int Height, string Name)[] Scales =
    [
        (1280, 840, "baseline-1280x840"),
        (2048, 2048, "medium-2048x2048"),
        (4096, 4096, "large-4096x4096")
    ];

    public static bool Verify(string reportPath, out string summary)
        => VerifyWithStageObserver(reportPath, out summary, null);

    internal static bool VerifyWithStageObserver(
        string reportPath, out string summary, Action<string>? stageCompleted)
        => VerifyWithCancellation(reportPath, out summary, stageCompleted, CancellationToken.None);

    internal static bool VerifyWithCancellation(
        string reportPath, out string summary, Action<string>? stageCompleted, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var verificationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        verificationCancellation.CancelAfter(TimeSpan.FromSeconds(60));
        var token = verificationCancellation.Token;
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)!;
        Directory.CreateDirectory(reportDirectory);
        var lines = new List<string>
        {
            "OpenVisionLab 3D C3D raw/derived/encode/render-copy memory measurement",
            $"Generated: {DateTimeOffset.Now:O}",
            $"Process: {Environment.ProcessId}",
            $"Runtime: {Environment.Version}",
            $"MaxRenderedPoints: {MaxRenderedPoints.ToString(CultureInfo.InvariantCulture)}",
            "ObservationBoundary: process private/working-set and managed heap sampling; GPU context upload is outside this CPU verifier.",
            "PolicyBoundary: observations are not a product memory budget or universal leak-free claim.",
            "ObservationImplementation: bounded STA workers enclose the unchanged forced-GC/sampler/CPU procedures; worker orchestration changes measurement overhead. Prior byte/peak observations are not bitwise comparison targets."
        };
        var passed = 0;
        var total = 0;
        var fixtureCleanupDeferred = false;
        var cleaningUp = false;

        void Check(string name, bool condition, string detail)
        {
            if (!cleaningUp) token.ThrowIfCancellationRequested();
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var fixturePaths = new List<string>();
        DirectoryInfo? ownedFixtureDirectory = null;
        try
        {
            ownedFixtureDirectory = Directory.CreateTempSubdirectory("ovl-memory-verification-");
            var fixtureDirectory = ownedFixtureDirectory.FullName;
            lines.Add($"FixtureDirectory: {fixtureDirectory}");
            stageCompleted?.Invoke("memory-fixture-directory-created");
            stageCompleted?.Invoke("memory-before-admission");
            var admittedBoundary = C3DMemoryAdmissionPolicy.Evaluate(4096, 4096);
            var rejectedBoundary = C3DMemoryAdmissionPolicy.Evaluate(4097, 4096);
            Check(
                "measured cell-count boundary admits the largest passing scale",
                admittedBoundary.IsAdmitted
                && admittedBoundary.SampleCount == C3DMemoryAdmissionPolicy.MaxSupportedSampleCount,
                $"admitted={admittedBoundary.IsAdmitted};cells={admittedBoundary.SampleCount};limit={C3DMemoryAdmissionPolicy.MaxSupportedSampleCount}");
            Check(
                "cell-count boundary rejects the next larger scale",
                !rejectedBoundary.IsAdmitted
                && rejectedBoundary.SampleCount > C3DMemoryAdmissionPolicy.MaxSupportedSampleCount,
                $"admitted={rejectedBoundary.IsAdmitted};cells={rejectedBoundary.SampleCount};reason={rejectedBoundary.Reason}");

            stageCompleted?.Invoke("memory-admission-boundary-checked");
            var oversizedHeaderPath = Path.Combine(fixtureDirectory, "oversized-header-only.C3D");
            fixturePaths.Add(oversizedHeaderPath);
            WriteHeaderOnlyFixture(oversizedHeaderPath, 4097, 4096);
            var admissionRejectedBeforePayloadAllocation = false;
            C3DSourceTopologyReason? admissionReason = null;
            try
            {
                _ = C3DHeightGrid.Load(oversizedHeaderPath, maxRenderedPoints: 0);
            }
            catch (C3DSourceTopologyException exception) when (exception.Reason == C3DSourceTopologyReason.AdmissionLimitExceeded)
            {
                admissionRejectedBeforePayloadAllocation = true;
                admissionReason = exception.Reason;
            }

            Check(
                "oversized header is rejected before payload allocation",
                admissionRejectedBeforePayloadAllocation,
                $"rejected={admissionRejectedBeforePayloadAllocation};reason={admissionReason?.ToString() ?? "none"};path={oversizedHeaderPath}");

            stageCompleted?.Invoke("memory-oversized-header-checked");
            foreach (var scale in Scales)
            {
                token.ThrowIfCancellationRequested();
                var fixturePath = Path.Combine(fixtureDirectory, $"{scale.Name}.C3D");
                fixturePaths.Add(fixturePath);
                WriteFixture(fixturePath, scale.Width, scale.Height, token);
                var info = new FileInfo(fixturePath);
                var expectedBytes = checked(8L + (long)scale.Width * scale.Height * sizeof(float));
                Check(
                    $"fixture {scale.Name} has deterministic topology",
                    info.Length == expectedBytes,
                    $"path={fixturePath};bytes={info.Length};expectedBytes={expectedBytes};cells={(long)scale.Width * scale.Height}");

                if (scale.Name == "baseline-1280x840")
                {
                    VerifyViewerRetention(
                        fixturePath,
                        oversizedHeaderPath,
                        lines,
                        Check, token);
                }

                MeasureScale(fixturePath, scale.Width, scale.Height, scale.Name, lines, Check, token);
            }
        }
        catch (VerificationStaIncompleteException exception)
        {
            fixtureCleanupDeferred = true;
            exception.Data["FixtureDirectory"] = ownedFixtureDirectory?.FullName;
            exception.Data["FixtureCleanupDeferred"] = true;
            throw;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException("Memory verification canceled; no result committed.", exception, cancellationToken); }
        catch (OperationCanceledException exception) when (token.IsCancellationRequested)
        { throw new TimeoutException("Memory verification60s work deadline; no result committed.", exception); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or OverflowException or OutOfMemoryException)
        {
            Check("verifier exception", false, exception.ToString());
        }
        finally
        {
            cleaningUp = true;
            if (!fixtureCleanupDeferred)
            {
            foreach (var fixturePath in fixturePaths)
            {
                var fixtureFailure = TryDelete(fixturePath);
                if (fixtureFailure is not null)
                {
                    Check("fixture cleanup", false, $"path={fixturePath};failure={fixtureFailure}");
                }

                var derivedPath = Path.ChangeExtension(fixturePath, ".derived.C3D");
                var derivedFailure = TryDelete(derivedPath);
                if (derivedFailure is not null)
                {
                    Check("fixture cleanup", false, $"path={derivedPath};failure={derivedFailure}");
                }
            }
            try
            {
                if (ownedFixtureDirectory is not null && Directory.Exists(ownedFixtureDirectory.FullName))
                    DeleteOwnedFixtureDirectory(ownedFixtureDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Check("fixture-directory cleanup", false, exception.ToString());
            }
            }
        }

        stageCompleted?.Invoke("memory-before-report");
        cancellationToken.ThrowIfCancellationRequested();
        token.ThrowIfCancellationRequested();
        var succeeded = passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(fullReportPath, lines);
        summary = $"C3DMemoryMeasurement|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static void MeasureScale(
        string fixturePath,
        int width,
        int height,
        string scaleName,
        List<string> lines,
        Action<string, bool, string> check,
        CancellationToken cancellationToken)
    {
        C3DHeightFieldSnapshot? snapshot = null;
        C3DHeightGrid? grid = null;
        C3DHeightGridRenderProxy? renderProxy = null;
        Vector3[]? positions = null;
        var workerUnsettled = false;
        try
        {
            snapshot = Measure(
                "raw-snapshot-load",
                scaleName,
                () => C3DHeightFieldSnapshot.LoadIdentified(
                    fixturePath,
                    $"memory.{scaleName}",
                    "raw-height",
                    "frame.c3d-grid-index", cancellationToken),
                lines, cancellationToken);
            check(
                $"{scaleName} raw snapshot identity",
                snapshot.Width == width
                && snapshot.Height == height
                && snapshot.ValidCount == checked(width * height),
                $"width={snapshot.Width};height={snapshot.Height};valid={snapshot.ValidCount};bytes={snapshot.ByteLength};sha256={snapshot.ContentSha256}");

            grid = Measure(
                "raw-grid-load",
                scaleName,
                () => C3DHeightGrid.Load(fixturePath, MaxRenderedPoints, cancellationToken, progress: null),
                lines, cancellationToken);
            check(
                $"{scaleName} raw grid identity",
                grid.Width == width
                && grid.Height == height
                && grid.Points.Length > 0
                && grid.Points.Length <= MaxRenderedPoints,
                $"width={grid.Width};height={grid.Height};points={grid.Points.Length};stride={grid.PointStride};totalMs={grid.LoadPerformance.TotalMilliseconds:F3}");

            var derivedValues = new OffsetHeightValues(snapshot.Values, 0.25, cancellationToken);
            var derivedOutputPath = Path.Combine(
                Path.GetDirectoryName(fixturePath)!,
                $"{Path.GetFileNameWithoutExtension(fixturePath)}.derived.C3D");
            var derived = Measure(
                "derived-transform-and-encode",
                scaleName,
                () => snapshot.CreateDerived(
                    $"derived.{scaleName}",
                    derivedValues,
                    $"memory-measurement:{scaleName}"),
                lines, cancellationToken);
            check(
                $"{scaleName} derived snapshot preserves dimensions",
                derived.Width == width
                && derived.Height == height
                && derived.IsDerived
                && derived.RootSourceSha256 == snapshot.RootSourceSha256,
                $"width={derived.Width};height={derived.Height};derived={derived.IsDerived};rootSha256={derived.RootSourceSha256}");

            Measure(
                "encode-save",
                scaleName,
                () =>
                {
                    derived.SaveC3D(derivedOutputPath);
                    return new FileInfo(derivedOutputPath).Length;
                },
                lines, cancellationToken);
            check(
                $"{scaleName} encoded output has expected length",
                File.Exists(derivedOutputPath)
                && new FileInfo(derivedOutputPath).Length == checked(8L + (long)width * height * sizeof(float)),
                $"path={derivedOutputPath};bytes={(File.Exists(derivedOutputPath) ? new FileInfo(derivedOutputPath).Length : 0)}");

            renderProxy = Measure(
                "render-proxy-and-positions",
                scaleName,
                () =>
                {
                    var proxy = C3DHeightGridRenderProxy.Create(grid, cancellationToken);
                    var cache = new C3DRenderPositionCache();
                    var renderPositions = cache.GetOrCreate(proxy, ModelTransform.Identity);
                    positions = renderPositions;
                    return (proxy, renderPositions);
                },
                lines, cancellationToken).proxy;
            check(
                $"{scaleName} render copy is bounded",
                renderProxy.Points.Length == grid.Points.Length
                && positions is { Length: > 0 }
                && positions.Length == renderProxy.Points.Length,
                $"points={renderProxy.Points.Length};triangles={renderProxy.TriangleCount};gridEdges={renderProxy.GridEdgeCount};positions={positions?.Length ?? 0}");

            var vertices = Measure(
                "gpu-vertex-copy",
                scaleName,
                () => C3DGpuVertexBuilder.Build(
                    renderProxy.Points,
                    positions!,
                    static (_, _) => (0.62, 0.82, 1.0)),
                lines, cancellationToken);
            check(
                $"{scaleName} GPU vertex payload is complete",
                vertices.Length == checked(renderProxy.Points.Length * 6),
                $"floats={vertices.Length};bytes={(long)vertices.Length * sizeof(float)};gpuUpload=outside-cpu-verifier");
        }
        catch (VerificationStaIncompleteException)
        {
            workerUnsettled = true;
            throw;
        }
        finally
        {
            if (!workerUnsettled)
            {
            positions = null;
            renderProxy = null;
            grid = null;
            snapshot = null;
            }
        }
        // Successful-scale post-collection uses the same GC protocol. A failed
        // or canceled scale never masks its failure with another GC wait.
        ForceCollection(cancellationToken);
    }

    private static T Measure<T>(
        string stage,
        string scaleName,
        Func<T> action,
        List<string> lines,
        CancellationToken cancellationToken = default,
        Action<string, object>? samplerObserver = null,
        TimeSpan? cleanupTimeout = null,
        Action? collectionOverride = null)
    {
        T result = default!;
        var elapsedMilliseconds = 0.0;
        MemoryObservation observation = default;
        var lifetime = VerificationStaOperation.Run(
            () => { (collectionOverride ?? CollectGarbage)(); return new MemorySampler(); },
            (sampler, token) =>
            {
                sampler.Start();
                samplerObserver?.Invoke("started", sampler);
                token.ThrowIfCancellationRequested();
                var start = Stopwatch.GetTimestamp();
                result = action();
                token.ThrowIfCancellationRequested();
                elapsedMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                samplerObserver?.Invoke("stop-enter", sampler);
                observation = sampler.Stop();
                return Task.CompletedTask;
            },
            sampler => { samplerObserver?.Invoke("release-enter", sampler); sampler.Dispose(); },
            cancellationToken, TimeSpan.FromSeconds(60), cleanupTimeout ?? TimeSpan.FromSeconds(10));
        lines.Add($"WORKER-LIFETIME | scale={scaleName};stage={stage};{lifetime}");
        lines.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"MEASURE | scale={scaleName} | stage={stage} | elapsedMs={elapsedMilliseconds:F3} | privateStart={observation.PrivateStartBytes} | privatePeak={observation.PrivatePeakBytes} | privateDelta={observation.PrivatePeakDeltaBytes} | workingSetStart={observation.WorkingSetStartBytes} | workingSetPeak={observation.WorkingSetPeakBytes} | workingSetDelta={observation.WorkingSetPeakDeltaBytes} | managedStart={observation.ManagedStartBytes} | managedPeak={observation.ManagedPeakBytes} | managedDelta={observation.ManagedPeakDeltaBytes} | allocatedDelta={observation.AllocatedDeltaBytes} | samples={observation.SampleCount}"));
        return result;
    }

    private static void WriteFixture(string path, int width, int height, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expectedBytes = checked(8L + (long)width * height * sizeof(float));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            SampleBufferBytes,
            FileOptions.SequentialScan);
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, width);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], height);
        stream.Write(header);

        var buffer = new byte[SampleBufferBytes];
        var index = 0L;
        while (index < (long)width * height)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sampleCount = Math.Min(buffer.Length / sizeof(float), (long)width * height - index);
            for (var offset = 0; offset < sampleCount; offset++)
            {
                var value = 1.0f + (float)((index + offset) % 1000) * 0.001f;
                BinaryPrimitives.WriteInt32LittleEndian(
                    buffer.AsSpan(offset * sizeof(float), sizeof(float)),
                    BitConverter.SingleToInt32Bits(value));
            }

            stream.Write(buffer, 0, checked((int)(sampleCount * sizeof(float))));
            index += sampleCount;
        }

        if (stream.Length != expectedBytes)
        {
            throw new InvalidDataException(
                $"Generated C3D fixture length mismatch: {stream.Length} != {expectedBytes}.");
        }
    }

    private static void WriteHeaderOnlyFixture(string path, int width, int height)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, width);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], height);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        stream.Write(header);
    }

    private static void VerifyViewerRetention(
        string validFixturePath,
        string oversizedHeaderPath,
        List<string> lines,
        Action<string, bool, string> check,
        CancellationToken cancellationToken = default)
    {
        Exception? failure = null;
        var validLoaded = false;
        var rejected = false;
        var retained = false;
        var retainedPath = string.Empty;
        VerificationStaObservation? lifetime = null;
        try
        {
            lifetime = VerificationStaOperation.Run(
                () => new OpenVisionThreeDViewerControl(loadDefaultSamples: false),
                async (control, token) =>
                {
                validLoaded = await control.LoadC3DSourceAsync(validFixturePath, token);
                var currentPath = control.CurrentC3DSourcePath;
                token.ThrowIfCancellationRequested();
                rejected = !await control.LoadC3DSourceAsync(oversizedHeaderPath, token);
                retainedPath = control.CurrentC3DSourcePath ?? string.Empty;
                retained = !string.IsNullOrWhiteSpace(currentPath)
                    && string.Equals(currentPath, retainedPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        Path.GetFullPath(validFixturePath),
                        retainedPath,
                        StringComparison.OrdinalIgnoreCase);
                },
                control => control.Dispose(), cancellationToken,
                TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10));
        }
        catch (VerificationStaIncompleteException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            failure = exception;
            lifetime = exception.Data["VerificationStaObservation"] as VerificationStaObservation;
        }
        lines.Add($"STA-LIFETIME | {lifetime};failure={failure}");

        check(
            "Viewer retains the current source after oversized admission rejection",
            failure is null && validLoaded && rejected && retained,
            $"validLoaded={validLoaded};rejected={rejected};retained={retained};retainedPath={retainedPath};failure={failure?.GetType().Name ?? "none"}");
        lines.Add("ViewerRetentionBoundary|current-source-preserved=true|display-mutation-on-rejection=false|gpu-context=not-initialized");
    }

    private static void ForceCollection(
        CancellationToken cancellationToken = default, Action? collectionOverride = null,
        TimeSpan? workTimeout = null, TimeSpan? cleanupTimeout = null)
    {
        VerificationStaOperation.Run(
            () => new object(),
            (_, token) => { (collectionOverride ?? CollectGarbage)(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; },
            _ => { }, cancellationToken, workTimeout ?? TimeSpan.FromSeconds(60), cleanupTimeout ?? TimeSpan.FromSeconds(10));
    }

    private static void CollectGarbage()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static void DeleteOwnedFixtureDirectory(DirectoryInfo directory)
    {
        var resolved = Path.GetFullPath(directory.FullName);
        var temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (!string.Equals(Path.GetDirectoryName(resolved), temporaryRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("ovl-memory-verification-", StringComparison.Ordinal)
            || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Verifier fixture directory no longer matches its owned temporary root.");
        Directory.Delete(resolved, recursive: true);
    }

    private static Exception? TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException exception)
        {
            return exception;
        }
        catch (UnauthorizedAccessException exception)
        {
            return exception;
        }

        return null;
    }

    private sealed class OffsetHeightValues(ReadOnlyMemory<double> source, double offset, CancellationToken cancellationToken) : IReadOnlyList<double>
    {
        public int Count => source.Length;

        public double this[int index]
        {
            get
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = source.Span[index];
                return double.IsFinite(value) ? value + offset : value;
            }
        }

        public IEnumerator<double> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class MemorySampler : IDisposable
    {
        private readonly Process process = Process.GetCurrentProcess();
        private readonly CancellationTokenSource cancellation = new();
        private Task? samplingTask;
        private long privateStartBytes;
        private long privatePeakBytes;
        private long workingSetStartBytes;
        private long workingSetPeakBytes;
        private long managedStartBytes;
        private long managedPeakBytes;
        private long allocatedStartBytes;
        private long allocatedPeakBytes;
        private int sampleCount;

        public void Start()
        {
            Sample(isStart: true);
            samplingTask = Task.Run(SampleLoop);
        }

        public MemoryObservation Stop()
        {
            Sample(isStart: false);
            cancellation.Cancel();
            samplingTask?.GetAwaiter().GetResult();
            Sample(isStart: false);
            return new MemoryObservation(
                privateStartBytes,
                privatePeakBytes,
                Math.Max(0, privatePeakBytes - privateStartBytes),
                workingSetStartBytes,
                workingSetPeakBytes,
                Math.Max(0, workingSetPeakBytes - workingSetStartBytes),
                managedStartBytes,
                managedPeakBytes,
                Math.Max(0, managedPeakBytes - managedStartBytes),
                Math.Max(0, allocatedPeakBytes - allocatedStartBytes),
                sampleCount);
        }

        public void Dispose()
        {
            try
            {
                cancellation.Cancel();
                samplingTask?.GetAwaiter().GetResult();
            }
            finally
            {
                try
                {
                    cancellation.Dispose();
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private async Task SampleLoop()
        {
            while (!cancellation.IsCancellationRequested)
            {
                Sample(isStart: false);
                try
                {
                    await Task.Delay(5, cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void Sample(bool isStart)
        {
            process.Refresh();
            var privateBytes = process.PrivateMemorySize64;
            var workingSetBytes = process.WorkingSet64;
            var managedBytes = GC.GetTotalMemory(forceFullCollection: false);
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
            if (isStart)
            {
                privateStartBytes = privatePeakBytes = privateBytes;
                workingSetStartBytes = workingSetPeakBytes = workingSetBytes;
                managedStartBytes = managedPeakBytes = managedBytes;
                allocatedStartBytes = allocatedPeakBytes = allocatedBytes;
            }
            else
            {
                privatePeakBytes = Math.Max(privatePeakBytes, privateBytes);
                workingSetPeakBytes = Math.Max(workingSetPeakBytes, workingSetBytes);
                managedPeakBytes = Math.Max(managedPeakBytes, managedBytes);
                allocatedPeakBytes = Math.Max(allocatedPeakBytes, allocatedBytes);
            }

            sampleCount++;
        }
    }

    private readonly record struct MemoryObservation(
        long PrivateStartBytes,
        long PrivatePeakBytes,
        long PrivatePeakDeltaBytes,
        long WorkingSetStartBytes,
        long WorkingSetPeakBytes,
        long WorkingSetPeakDeltaBytes,
        long ManagedStartBytes,
        long ManagedPeakBytes,
        long ManagedPeakDeltaBytes,
        long AllocatedDeltaBytes,
        int SampleCount);
}
