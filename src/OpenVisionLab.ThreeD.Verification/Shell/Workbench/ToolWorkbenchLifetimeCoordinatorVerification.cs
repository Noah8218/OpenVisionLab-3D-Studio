using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchLifetimeCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Workbench lifetime coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: ordered resource disposal, source-quality cancellation, and idempotence"
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

        try
        {
            var beforeCancellationNames = Enumerable.Range(1, 43)
                .Select(index => $"before-{index:00}")
                .ToArray();
            var afterCancellationNames = new[] { "height-image-viewer", "source-quality", "source-session" };
            var events = new List<string>();
            var beforeCancellationResources = beforeCancellationNames
                .Select(name => new RecordingResource(name, events))
                .Cast<IDisposable>()
                .ToArray();
            var afterCancellationResources = afterCancellationNames
                .Select(name => new RecordingResource(name, events))
                .Cast<IDisposable>()
                .ToArray();
            var cancellationCount = 0;

            using var coordinator = new ToolWorkbenchLifetimeCoordinator(
                beforeCancellationResources,
                () =>
                {
                    cancellationCount++;
                    events.Add("cancel-source-quality-ui");
                },
                afterCancellationResources);

            coordinator.Dispose();
            coordinator.Dispose();

            var expectedEvents = beforeCancellationNames
                .Concat(new[] { "cancel-source-quality-ui" })
                .Concat(afterCancellationNames)
                .ToArray();
            Check(
                "Resources and cancellation retain the exact shutdown order",
                events.SequenceEqual(expectedEvents),
                $"actual={string.Join(',', events)}");

            var allResources = beforeCancellationResources
                .Concat(afterCancellationResources)
                .Cast<RecordingResource>()
                .ToArray();
            Check(
                "Every resource is disposed exactly once",
                allResources.All(resource => resource.DisposeCount == 1),
                $"counts={string.Join(',', allResources.Select(resource => resource.DisposeCount))}");
            Check(
                "Repeated Dispose is harmless and cancellation runs once",
                cancellationCount == 1 && events.Count == expectedEvents.Length,
                $"cancellationCount={cancellationCount};eventCount={events.Count}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | verifier exception | {exception.GetType().Name}: {exception.Message}");
        }

        var succeeded = passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ToolWorkbenchLifetimeCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private sealed class RecordingResource : IDisposable
    {
        private readonly string name;
        private readonly ICollection<string> events;

        public RecordingResource(string name, ICollection<string> events)
        {
            this.name = name;
            this.events = events;
        }

        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            events.Add(name);
        }
    }
}
