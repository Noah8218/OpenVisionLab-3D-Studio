using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerOnlySourceLoadCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)!;
        Directory.CreateDirectory(reportDirectory);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer-only source load coordinator verification",
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

        var fixturePath = Path.Combine("3D", "PublicSamples", "glTF", "Box.glb");
        var fixtureExists = File.Exists(fixturePath);
        var request = ViewerOnlySourceLoadCoordinator.Prepare(fixturePath, 1);
        Check(
            "preparation normalizes the source identity and sample budget",
            string.Equals(request.FullPath, Path.GetFullPath(fixturePath), StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.SourceName, "Box.glb", StringComparison.Ordinal)
            && string.Equals(request.Extension, ".glb", StringComparison.Ordinal)
            && request.MaxSampledPoints == 2
            && request.FileExists == fixtureExists,
            $"path={request.FullPath};extension={request.Extension};budget={request.MaxSampledPoints};exists={request.FileExists}");

        var missingRequest = ViewerOnlySourceLoadCoordinator.Prepare(
            Path.Combine(reportDirectory, "missing-viewer-only-source.glb"),
            int.MaxValue);
        Check(
            "preparation reports a missing source without touching View state",
            !missingRequest.FileExists
            && missingRequest.MaxSampledPoints == int.MaxValue
            && string.Equals(missingRequest.SourceName, "missing-viewer-only-source.glb", StringComparison.Ordinal),
            $"exists={missingRequest.FileExists};budget={missingRequest.MaxSampledPoints};source={missingRequest.SourceName}");

        using var lazPointCloudLoadCoordinator = new LazPointCloudLoadCoordinator(new LazPointCloudSampleCache());
        var coordinator = new ViewerOnlySourceLoadCoordinator(lazPointCloudLoadCoordinator);
        if (fixtureExists)
        {
            var progress = new RecordingProgress();
            var loaded = coordinator
                .LoadAsync(request, CancellationToken.None, progress)
                .GetAwaiter()
                .GetResult();
            Check(
                "GLB dispatch returns a decoded mesh without WPF coupling",
                loaded is { Mesh: { TriangleCount: > 0 }, Format: "GLB" }
                && string.Equals(
                    Path.GetFullPath(loaded.Value.Mesh!.SourcePath),
                    request.FullPath,
                    StringComparison.OrdinalIgnoreCase),
                $"loaded={loaded is not null};format={loaded?.Format};triangles={loaded?.Mesh?.TriangleCount:N0}");
            Check(
                "GLB dispatch preserves the preparation progress boundary",
                progress.Values.Any(value => Math.Abs(value - 90.0) < 0.001),
                $"progress={string.Join(',', progress.Values.Select(value => value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)))}");

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var cancelled = false;
            try
            {
                _ = coordinator
                    .LoadAsync(request, cancellation.Token)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Check(
                "dispatch honors cancellation before CPU decode",
                cancelled,
                $"cancelled={cancelled}");
        }
        else
        {
            Check(
                "GLB fixture exists for focused dispatch verification",
                false,
                $"path={Path.GetFullPath(fixturePath)}");
        }

        var missingRejected = false;
        try
        {
            _ = coordinator
                .LoadAsync(missingRequest)
                .GetAwaiter()
                .GetResult();
        }
        catch (FileNotFoundException)
        {
            missingRejected = true;
        }

        Check(
            "dispatch rejects a missing source before format decode",
            missingRejected,
            $"rejected={missingRejected}");

        var callbackInvocationCount = 0;
        var callbackRequest = new ViewerOnlySourceLoadRequest(
            request.FullPath,
            request.SourceName,
            ".laz",
            request.MaxSampledPoints,
            FileExists: true);
        var callbackResult = coordinator
            .LoadAsync(
                callbackRequest,
                lazLoadAsync: (path, maxSampledPoints, cancellationToken, progress, isCurrent) =>
                {
                    callbackInvocationCount++;
                    _ = path;
                    _ = maxSampledPoints;
                    _ = cancellationToken;
                    _ = progress;
                    _ = isCurrent;
                    return Task.FromResult<LazPointCloudLoadResult?>(
                        new LazPointCloudLoadResult(null, 0.0, Reused: false, WasCanceled: true));
                })
            .GetAwaiter()
            .GetResult();
        Check(
            "dispatch accepts the View-owned LAZ status adapter without coupling to WPF",
            callbackInvocationCount == 1 && callbackResult is null,
            $"callbackInvocations={callbackInvocationCount};resultNull={callbackResult is null}");

        var unsupportedPath = Path.Combine(reportDirectory, "unsupported-viewer-only-source.xyz");
        File.WriteAllText(unsupportedPath, "not a supported 3D source");
        var unsupportedRequest = ViewerOnlySourceLoadCoordinator.Prepare(unsupportedPath, 4);
        var unsupportedRejected = false;
        try
        {
            _ = coordinator
                .LoadAsync(unsupportedRequest)
                .GetAwaiter()
                .GetResult();
        }
        catch (NotSupportedException)
        {
            unsupportedRejected = true;
        }

        Check(
            "dispatch rejects an existing unsupported extension before decode",
            unsupportedRequest.FileExists && unsupportedRejected,
            $"exists={unsupportedRequest.FileExists};rejected={unsupportedRejected}");

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerOnlySourceLoadCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }
}
