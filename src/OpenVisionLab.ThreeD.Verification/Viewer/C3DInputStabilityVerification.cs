using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

/// <summary>
/// Verifies the existing C3D and Viewer-only input boundaries with small
/// isolated fixtures. The measurements are rejection observations, not a
/// machine-wide memory or decoder safety budget.
/// </summary>
internal static class C3DInputStabilityVerification
{
    private const int RegularWidth = 16;
    private const int RegularHeight = 12;
    private const int MalformedTimeBoundMilliseconds = 2_000;
    private const long ComparablePayloadBytes = 1_048_576;

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)!;
        var fixtureDirectory = Path.Combine(reportDirectory, "fixtures");
        Directory.CreateDirectory(reportDirectory);

        var lines = new List<string>
        {
            "OpenVisionLab 3D C3D and Viewer-only input stability verification",
            $"Generated: {DateTimeOffset.Now:O}",
            $"Process: {Environment.ProcessId}",
            $"Runtime: {Environment.Version}",
            $"MalformedTimeObservationBoundMs: {MalformedTimeBoundMilliseconds.ToString(CultureInfo.InvariantCulture)}",
            "ObservationBoundary: allocation and elapsed-time samples are local rejection observations; they are not a product memory budget or universal LASzip safety claim.",
            "OwnerBoundary: C3DSourceTopology/C3DHeightFieldBinaryCodec and existing Viewer-only LAS/LAZ dispatch remain the validation owners."
        };
        var passed = 0;
        var total = 0;
        var cleanupFailed = false;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var generatedPaths = new List<string>();
        try
        {
            Directory.CreateDirectory(fixtureDirectory);
            var minimalPath = Path.Combine(fixtureDirectory, "minimal-nodata.C3D");
            WriteC3D(minimalPath, 2, 2, [1.0f, 0.0f, float.NaN, -2.0f]);
            generatedPaths.Add(minimalPath);

            var regularPath = Path.Combine(fixtureDirectory, "regular-16x12.C3D");
            WriteC3D(
                regularPath,
                RegularWidth,
                RegularHeight,
                Enumerable.Range(0, RegularWidth * RegularHeight)
                    .Select(index => 1.0f + index * 0.01f)
                    .ToArray());
            generatedPaths.Add(regularPath);

            VerifyNormalC3D(minimalPath, regularPath, Check, lines);
            VerifyHashAndNoData(minimalPath, Check, lines);

            foreach (var fixture in CreateMalformedFixtures())
            {
                var fixturePath = Path.Combine(fixtureDirectory, fixture.Name + ".C3D");
                File.WriteAllBytes(fixturePath, fixture.Bytes);
                generatedPaths.Add(fixturePath);
                VerifyMalformedC3D(fixturePath, fixture, Check, lines);
            }

            var lockedPath = Path.Combine(fixtureDirectory, "locked-source.C3D");
            WriteC3D(lockedPath, 2, 2, [1.0f, 2.0f, 3.0f, 4.0f]);
            generatedPaths.Add(lockedPath);
            using (var lockStream = new FileStream(
                       lockedPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.None,
                       bufferSize: 4096,
                       options: FileOptions.SequentialScan))
            {
                VerifyLockedC3D(lockedPath, Check, lines);
            }

            var corruptLazPath = Path.Combine(fixtureDirectory, "corrupt-input.laz");
            File.WriteAllBytes(corruptLazPath, Encoding.ASCII.GetBytes("not-a-LAS-or-LAZ"));
            generatedPaths.Add(corruptLazPath);

            var unsupportedPath = Path.Combine(fixtureDirectory, "unsupported-layout.xyz");
            File.WriteAllText(unsupportedPath, "unsupported viewer-only layout", Encoding.UTF8);
            generatedPaths.Add(unsupportedPath);
            VerifyViewerOnlyInputs(
                corruptLazPath,
                unsupportedPath,
                Check,
                lines);

            VerifyViewerRetention(
                regularPath,
                [
                    Path.Combine(fixtureDirectory, "incomplete-header.C3D"),
                    Path.Combine(fixtureDirectory, "truncated-payload.C3D"),
                    Path.Combine(fixtureDirectory, "oversized-header.C3D")
                ],
                Check,
                lines);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or ArgumentException
            or OverflowException
            or OutOfMemoryException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            lines.Add($"FAIL | unexpected verifier exception | {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            foreach (var generatedPath in generatedPaths)
            {
                if (!TryDelete(generatedPath))
                {
                    cleanupFailed = true;
                    lines.Add($"FAIL | fixture cleanup | path={generatedPath}");
                }
            }

            try
            {
                if (Directory.Exists(fixtureDirectory))
                {
                    Directory.Delete(fixtureDirectory, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                cleanupFailed = true;
                lines.Add($"FAIL | fixture-directory cleanup | {exception.Message}");
            }
        }

        var succeeded = passed == total
            && total > 0
            && !cleanupFailed
            && !lines.Any(line => line.StartsWith("FAIL | unexpected", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(fullReportPath, lines);
        summary = $"C3DInputStability|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static void VerifyNormalC3D(
        string minimalPath,
        string regularPath,
        Action<string, bool, string> check,
        List<string> lines)
    {
        var minimalSnapshot = C3DHeightFieldSnapshot.LoadIdentified(
            minimalPath,
            "input-stability.minimal",
            "raw-height",
            "frame.c3d-grid-index");
        var minimalGrid = C3DHeightGrid.Load(minimalPath, maxRenderedPoints: 0);
        check(
            "minimal C3D loads through Snapshot and HeightGrid owners",
            minimalSnapshot.Width == 2
            && minimalSnapshot.Height == 2
            && minimalSnapshot.ValidCount == 2
            && minimalGrid.Width == 2
            && minimalGrid.Height == 2,
            $"snapshot={minimalSnapshot.Width}x{minimalSnapshot.Height};snapshotValid={minimalSnapshot.ValidCount};grid={minimalGrid.Width}x{minimalGrid.Height}");

        var regularSnapshot = C3DHeightFieldSnapshot.LoadIdentified(
            regularPath,
            "input-stability.regular",
            "raw-height",
            "frame.c3d-grid-index");
        var regularGrid = C3DHeightGrid.Load(regularPath, maxRenderedPoints: 0);
        check(
            "regular C3D loads without render-point allocation",
            regularSnapshot.Width == RegularWidth
            && regularSnapshot.Height == RegularHeight
            && regularSnapshot.ValidCount == RegularWidth * RegularHeight
            && regularGrid.Points.Length == 0
            && regularGrid.PointStride == 0,
            $"snapshot={regularSnapshot.Width}x{regularSnapshot.Height};valid={regularSnapshot.ValidCount};points={regularGrid.Points.Length};stride={regularGrid.PointStride}");

        lines.Add(
            $"NORMAL | c3d=minimal;source={minimalPath};snapshotHash={minimalSnapshot.ContentSha256};gridHash={minimalGrid.ContentSha256}");
        lines.Add(
            $"NORMAL | c3d=regular;source={regularPath};bytes={regularSnapshot.ByteLength};sha256={regularSnapshot.ContentSha256}");
    }

    private static void VerifyHashAndNoData(
        string path,
        Action<string, bool, string> check,
        List<string> lines)
    {
        var expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var snapshot = C3DHeightFieldSnapshot.LoadIdentified(
            path,
            "input-stability.hash",
            "raw-height",
            "frame.c3d-grid-index");
        var grid = C3DHeightGrid.Load(path, maxRenderedPoints: 0);
        check(
            "same-stream C3D hash identity remains canonical",
            string.Equals(snapshot.ContentSha256, expectedHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(grid.ContentSha256, expectedHash, StringComparison.OrdinalIgnoreCase),
            $"expected={expectedHash};snapshot={snapshot.ContentSha256};grid={grid.ContentSha256}");

        var snapshotValues = snapshot.Values.Span;
        var gridValues = grid.ReadHeightMapValues();
        check(
            "zero and non-finite C3D samples remain NoData",
            snapshot.MissingCount == 2
            && double.IsNaN(snapshotValues[1])
            && double.IsNaN(snapshotValues[2])
            && gridValues.Length == 4
            && gridValues[0] == 1.0
            && double.IsNaN(gridValues[1])
            && double.IsNaN(gridValues[2])
            && gridValues[3] == -2.0,
            $"snapshotValid={snapshot.ValidCount};snapshotMissing={snapshot.MissingCount};gridValues={string.Join(',', gridValues.Select(Format))}");
        lines.Add(
            $"REGRESSION | same-stream-sha256={expectedHash};zero-nodata=true;nonfinite-nodata=true");
    }

    private static void VerifyMalformedC3D(
        string path,
        C3DFixture fixture,
        Action<string, bool, string> check,
        List<string> lines)
    {
        var snapshotObservation = Observe(() =>
            C3DHeightFieldSnapshot.LoadIdentified(
                path,
                $"input-stability.{fixture.Name}",
                "raw-height",
                "frame.c3d-grid-index"));
        var gridObservation = Observe(() => C3DHeightGrid.Load(path, maxRenderedPoints: 0));

        VerifyTopologyObservation(
            $"Snapshot rejects {fixture.Name}",
            fixture,
            snapshotObservation,
            check);
        VerifyTopologyObservation(
            $"HeightGrid rejects {fixture.Name}",
            fixture,
            gridObservation,
            check);
        lines.Add(
            $"REJECT | fixture={fixture.Name};expected={fixture.ExpectedReason};snapshot={Describe(snapshotObservation)};grid={Describe(gridObservation)}");
    }

    private static void VerifyTopologyObservation(
        string name,
        C3DFixture fixture,
        InputObservation observation,
        Action<string, bool, string> check)
    {
        var topology = observation.Error as C3DSourceTopologyException;
        var reasonMatches = topology?.Reason == fixture.ExpectedReason;
        var timeMatches = observation.ElapsedMilliseconds <= MalformedTimeBoundMilliseconds;
        var allocationMatches = fixture.ExpectedPayloadBytes is not long payload
            || payload < ComparablePayloadBytes
            || observation.AllocatedDeltaBytes < payload;
        check(
            name,
            reasonMatches && timeMatches && allocationMatches,
            $"reason={topology?.Reason.ToString() ?? "none"};expected={fixture.ExpectedReason};elapsedMs={observation.ElapsedMilliseconds:F3};allocatedDelta={observation.AllocatedDeltaBytes};privateDelta={observation.PrivateDeltaBytes};payload={fixture.ExpectedPayloadBytes?.ToString(CultureInfo.InvariantCulture) ?? "n/a"};allocationObservation={allocationMatches}");
    }

    private static void VerifyLockedC3D(
        string path,
        Action<string, bool, string> check,
        List<string> lines)
    {
        var snapshotObservation = Observe(() =>
            C3DHeightFieldSnapshot.LoadIdentified(
                path,
                "input-stability.locked",
                "raw-height",
                "frame.c3d-grid-index"));
        var gridObservation = Observe(() => C3DHeightGrid.Load(path, maxRenderedPoints: 0));
        check(
            "locked C3D Snapshot fails with a controlled I/O error",
            snapshotObservation.Error is IOException or UnauthorizedAccessException,
            Describe(snapshotObservation));
        check(
            "locked C3D HeightGrid fails with a controlled I/O error",
            gridObservation.Error is IOException or UnauthorizedAccessException,
            Describe(gridObservation));
        lines.Add(
            $"REJECT | fixture=locked-source; Snapshot={Describe(snapshotObservation)};HeightGrid={Describe(gridObservation)}");
    }

    private static void VerifyViewerOnlyInputs(
        string corruptLazPath,
        string unsupportedPath,
        Action<string, bool, string> check,
        List<string> lines)
    {
        var lasPath = Path.GetFullPath(Path.Combine("3D", "PublicSamples", "PointCloud", "interesting.las"));
        var lazPath = Path.GetFullPath(Path.Combine("3D", "PublicSamples", "PointCloud", "xyzrgb_manuscript.laz"));
        using var lazLoadCoordinator = new LazPointCloudLoadCoordinator(new LazPointCloudSampleCache());
        var viewerOnlyCoordinator = new ViewerOnlySourceLoadCoordinator(lazLoadCoordinator);

        var las = ObserveViewerOnlyLoad(
            viewerOnlyCoordinator,
            ViewerOnlySourceLoadCoordinator.Prepare(lasPath, 16));
        check(
            "supported LAS input loads through the existing Viewer-only owner",
            las.Error is null
            && las.Result is { Format: "LAS", PointCloud: { SampledPointView.Count: > 0 } },
            $"exists={File.Exists(lasPath)};format={las.Result?.Format ?? "none"};sampled={las.Result?.PointCloud?.SampledPointView.Count.ToString(CultureInfo.InvariantCulture) ?? "0"};elapsedMs={las.ElapsedMilliseconds:F3};error={las.Error?.GetType().Name ?? "none"}");

        var laz = ObserveViewerOnlyLoad(
            viewerOnlyCoordinator,
            ViewerOnlySourceLoadCoordinator.Prepare(lazPath, 16));
        check(
            "supported LAZ input reaches the existing decoder boundary",
            laz.Error is null
            && laz.Result is { Format: "LAZ", PointCloud: { SampledPointView.Count: > 0 } },
            $"exists={File.Exists(lazPath)};format={laz.Result?.Format ?? "none"};sampled={laz.Result?.PointCloud?.SampledPointView.Count.ToString(CultureInfo.InvariantCulture) ?? "0"};elapsedMs={laz.ElapsedMilliseconds:F3};error={laz.Error?.GetType().Name ?? "none"}");

        var corrupt = ObserveViewerOnlyLoad(
            viewerOnlyCoordinator,
            ViewerOnlySourceLoadCoordinator.Prepare(corruptLazPath, 16));
        check(
            "corrupt LAZ input fails with a controlled decoder error",
            corrupt.Error is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or NotSupportedException
                or ArgumentException
            && corrupt.ElapsedMilliseconds <= MalformedTimeBoundMilliseconds,
            $"error={corrupt.Error?.GetType().Name ?? "none"};elapsedMs={corrupt.ElapsedMilliseconds:F3};message={Clean(corrupt.Error?.Message)}");

        var unsupported = ObserveViewerOnlyLoad(
            viewerOnlyCoordinator,
            ViewerOnlySourceLoadCoordinator.Prepare(unsupportedPath, 16));
        check(
            "unsupported Viewer-only layout fails before decoder dispatch",
            unsupported.Error is NotSupportedException,
            $"extension=.xyz;error={unsupported.Error?.GetType().Name ?? "none"};message={Clean(unsupported.Error?.Message)}");

        lines.Add(
            $"VIEWER-ONLY | LAS={Describe(las)};LAZ={Describe(laz)};corrupt-LAZ={Describe(corrupt)};unsupported={Describe(unsupported)}");
    }

    private static void VerifyViewerRetention(
        string validPath,
        IReadOnlyList<string> rejectedPaths,
        Action<string, bool, string> check,
        List<string> lines)
    {
        var loaded = false;
        var rejected = true;
        var pathRetained = true;
        var bindingRetained = true;
        var beforePath = string.Empty;
        var afterPath = string.Empty;
        var beforeHash = string.Empty;
        var failure = default(Exception);
        var thread = new Thread(() =>
        {
            OpenVisionThreeDViewerControl? control = null;
            try
            {
                control = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
                loaded = control.LoadC3DSource(validPath);
                beforePath = control.CurrentC3DSourcePath ?? string.Empty;
                var hasBeforeBinding = control.TryGetCurrentC3DSourceBinding(validPath, out var beforeBinding);
                beforeHash = hasBeforeBinding ? beforeBinding.ContentSha256 : string.Empty;
                foreach (var rejectedPath in rejectedPaths)
                {
                    var accepted = control.LoadC3DSource(rejectedPath);
                    rejected &= !accepted;
                    afterPath = control.CurrentC3DSourcePath ?? string.Empty;
                    pathRetained &= string.Equals(beforePath, afterPath, StringComparison.OrdinalIgnoreCase);
                    var hasAfterBinding = control.TryGetCurrentC3DSourceBinding(validPath, out var afterBinding);
                    bindingRetained &= hasBeforeBinding
                        && hasAfterBinding
                        && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(beforeBinding, afterBinding)
                        && string.Equals(beforeHash, afterBinding.ContentSha256, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                control?.Dispose();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        check(
            "Viewer retains the current C3D source after malformed and oversized rejection",
            failure is null
            && loaded
            && rejected
            && pathRetained
            && bindingRetained
            && string.Equals(Path.GetFullPath(validPath), beforePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFullPath(validPath), afterPath, StringComparison.OrdinalIgnoreCase),
            $"loaded={loaded};rejected={rejected};pathRetained={pathRetained};bindingRetained={bindingRetained};before={beforePath};after={afterPath};hash={beforeHash};failure={failure?.GetType().Name ?? "none"}");
        lines.Add(
            $"VIEWER-RETENTION | current-source-preserved={pathRetained};binding-preserved={bindingRetained};display-apply-on-rejection=false;gpu-context=not-initialized");
    }

    private static IReadOnlyList<C3DFixture> CreateMalformedFixtures() =>
    [
        new("incomplete-header", [0x01, 0x02, 0x03, 0x04], C3DSourceTopologyReason.HeaderIncomplete, null),
        new("corrupt-header", Enumerable.Repeat((byte)0xFF, 8).ToArray(), C3DSourceTopologyReason.DimensionsNonPositive, null),
        new(
            "truncated-payload",
            CreateC3DBytes(512, 512, [1.0f]),
            C3DSourceTopologyReason.PayloadLengthMismatch,
            512L * 512 * sizeof(float)),
        new(
            "trailing-payload",
            CreateC3DBytes(2, 2, [1.0f, 2.0f, 3.0f, 4.0f, 5.0f]),
            C3DSourceTopologyReason.PayloadLengthMismatch,
            2L * 2 * sizeof(float)),
        new(
            "oversized-header",
            CreateHeaderBytes(4097, 4096),
            C3DSourceTopologyReason.AdmissionLimitExceeded,
            4097L * 4096 * sizeof(float)),
        new(
            "overflowing-header",
            CreateHeaderBytes(int.MaxValue, int.MaxValue),
            C3DSourceTopologyReason.CellCountOverflow,
            null)
    ];

    private static InputObservation Observe(Action action)
    {
        ForceCollection();
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var privateStart = process.PrivateMemorySize64;
        var allocatedStart = GC.GetTotalAllocatedBytes(precise: true);
        var start = Stopwatch.GetTimestamp();
        Exception? error = null;
        try
        {
            action();
        }
        catch (Exception exception)
        {
            error = exception;
        }

        process.Refresh();
        return new InputObservation(
            error,
            Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            Math.Max(0, GC.GetTotalAllocatedBytes(precise: true) - allocatedStart),
            Math.Max(0, process.PrivateMemorySize64 - privateStart));
    }

    private static ViewerOnlyObservation ObserveViewerOnlyLoad(
        ViewerOnlySourceLoadCoordinator coordinator,
        ViewerOnlySourceLoadRequest request)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            var result = coordinator.LoadAsync(request).GetAwaiter().GetResult();
            return new ViewerOnlyObservation(
                result,
                null,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        catch (Exception exception)
        {
            return new ViewerOnlyObservation(
                null,
                exception,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    private static string Describe(InputObservation observation) =>
        $"error={observation.Error?.GetType().Name ?? "none"};elapsedMs={observation.ElapsedMilliseconds:F3};allocatedDelta={observation.AllocatedDeltaBytes};privateDelta={observation.PrivateDeltaBytes}";

    private static string Describe(ViewerOnlyObservation observation) =>
        $"format={observation.Result?.Format ?? "none"};loaded={observation.Result?.PointCloud is not null};elapsedMs={observation.ElapsedMilliseconds:F3};error={observation.Error?.GetType().Name ?? "none"}";

    private static void WriteC3D(
        string path,
        int width,
        int height,
        IReadOnlyList<float> values)
    {
        File.WriteAllBytes(path, CreateC3DBytes(width, height, values));
    }

    private static byte[] CreateC3DBytes(
        int width,
        int height,
        IReadOnlyList<float> values)
    {
        var bytes = new byte[checked(8 + values.Count * sizeof(float))];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), height);
        for (var index = 0; index < values.Count; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(8 + index * sizeof(float)),
                BitConverter.SingleToInt32Bits(values[index]));
        }

        return bytes;
    }

    private static byte[] CreateHeaderBytes(int width, int height)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), height);
        return bytes;
    }

    private static string Format(double value) =>
        double.IsNaN(value) ? "NaN" : value.ToString("G17", CultureInfo.InvariantCulture);

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return !File.Exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void ForceCollection()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private sealed record C3DFixture(
        string Name,
        byte[] Bytes,
        C3DSourceTopologyReason ExpectedReason,
        long? ExpectedPayloadBytes);

    private readonly record struct InputObservation(
        Exception? Error,
        double ElapsedMilliseconds,
        long AllocatedDeltaBytes,
        long PrivateMemoryDeltaBytes)
    {
        public long PrivateDeltaBytes => PrivateMemoryDeltaBytes;
    }

    private readonly record struct ViewerOnlyObservation(
        ViewerOnlySourceLoadResult? Result,
        Exception? Error,
        double ElapsedMilliseconds);
}
