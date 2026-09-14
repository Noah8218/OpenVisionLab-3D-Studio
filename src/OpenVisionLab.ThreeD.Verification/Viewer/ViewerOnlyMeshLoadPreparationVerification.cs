using System.IO;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerOnlyMeshLoadPreparationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer-only mesh load preparation verification",
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
        Check(
            "GLB fixture exists for WPF-free preparation",
            fixtureExists,
            $"path={Path.GetFullPath(fixturePath)}");

        if (fixtureExists)
        {
            var progress = new RecordingProgress();
            var prepared = ViewerOnlyMeshLoadPreparation
                .LoadAsync(fixturePath, CancellationToken.None, progress)
                .GetAwaiter()
                .GetResult();
            Check(
                "GLB preparation returns the decoded mesh and canonical format",
                prepared.Mesh.TriangleCount > 0
                && string.Equals(prepared.Format, "GLB", StringComparison.Ordinal)
                && string.Equals(
                    Path.GetFullPath(prepared.Mesh.SourcePath),
                    Path.GetFullPath(fixturePath),
                    StringComparison.OrdinalIgnoreCase),
                $"format={prepared.Format};triangles={prepared.Mesh.TriangleCount:N0};source={prepared.Mesh.SourcePath}");
            Check(
                "mesh preparation reports its post-decode progress boundary",
                progress.Values.Any(value => Math.Abs(value - 90.0) < 0.001),
                $"progress={string.Join(',', progress.Values.Select(value => value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)))}");

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var cancelled = false;
            try
            {
                _ = ViewerOnlyMeshLoadPreparation
                    .LoadAsync(fixturePath, cancellation.Token)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Check(
                "preparation honors cancellation before decode",
                cancelled,
                $"cancelled={cancelled}");
        }

        var unsupported = false;
        try
        {
            _ = ViewerOnlyMeshLoadPreparation
                .LoadAsync(Path.Combine(Path.GetTempPath(), "viewer-only-source.xyz"), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        catch (NotSupportedException)
        {
            unsupported = true;
        }

        Check(
            "preparation rejects unsupported mesh formats before I/O",
            unsupported,
            $"rejected={unsupported}");

        var succeeded = passed == total;
        summary = $"Viewer-only mesh load preparation verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return succeeded;
    }

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }
}
