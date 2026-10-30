using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class RunRecordJsonRecoveryTests
{
    [Theory]
    [InlineData("metrics")]
    [InlineData("metric-entry")]
    [InlineData("step-metrics")]
    [InlineData("step-metric-entry")]
    [InlineData("step-overlays")]
    [InlineData("step-overlay-entry")]
    public void MalformedResultCollectionsAreRejectedAndNormalReportsRecover(string missing)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", $"record-shapes-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var recordPath = Path.Combine(directory, "record.json");
        var htmlPath = Path.Combine(directory, "record.html");
        var csvPath = Path.Combine(directory, "record.csv");
        var valid = CreateRecord(directory);
        var step = valid.Steps![0];
        var malformed = missing switch
        {
            "metrics" => valid with { Metrics = null!, Steps = null },
            "metric-entry" => valid with { Metrics = [null!], Steps = null },
            "step-metrics" => valid with { Steps = [step with { Metrics = null! }] },
            "step-metric-entry" => valid with { Steps = [step with { Metrics = [null!] }] },
            "step-overlays" => valid with { Steps = [step with { Overlays = null! }] },
            "step-overlay-entry" => valid with { Steps = [step with { Overlays = [null!] }] },
            _ => throw new ArgumentOutOfRangeException(nameof(missing))
        };
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var original = JsonSerializer.SerializeToUtf8Bytes(malformed, options);
        try
        {
            File.WriteAllBytes(recordPath, original);
            var rejected = Record.Exception(() => InspectionRunRecordJson.Read(recordPath));
            if (rejected is null)
            {
                var exportFailure = Record.Exception(() => InspectionRunRecordReports.WriteHtml(htmlPath, InspectionRunRecordJson.Read(recordPath)));
                Assert.Fail($"Malformed record was accepted; HTML result: {exportFailure?.GetType().Name ?? "exported malformed evidence"}.");
            }
            Assert.IsType<InvalidDataException>(rejected);
            Assert.Equal(original, File.ReadAllBytes(recordPath));
            Assert.False(File.Exists(htmlPath));
            Assert.False(File.Exists(csvPath));

            InspectionRunRecordJson.Write(recordPath, valid);
            var restored = InspectionRunRecordJson.Read(recordPath);
            InspectionRunRecordReports.WriteHtml(htmlPath, restored);
            InspectionRunRecordReports.WriteCsv(csvPath, restored);
            Assert.Contains("MeasuredHeight", File.ReadAllText(htmlPath));
            Assert.Contains("1.25", File.ReadAllText(htmlPath));
            Assert.Contains("measurement-marker", File.ReadAllText(htmlPath));
            Assert.Contains("MeasuredHeight", File.ReadAllText(csvPath));
            Assert.Contains("1.25", File.ReadAllText(csvPath));
            Assert.Contains("measurement-marker", File.ReadAllText(csvPath));
        }
        finally
        {
            File.Delete(recordPath);
            File.Delete(htmlPath);
            File.Delete(csvPath);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void EmptyErrorMeasurementsRemainReadableAndExportable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", $"record-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var recordPath = Path.Combine(directory, "record.json");
        var htmlPath = Path.Combine(directory, "record.html");
        var csvPath = Path.Combine(directory, "record.csv");
        var record = CreateRecord(directory) with { Status = ResultStatus.Error, Message = "Input unavailable", Metrics = [], Steps = null };
        try
        {
            InspectionRunRecordJson.Write(recordPath, record);
            var restored = InspectionRunRecordJson.Read(recordPath);
            Assert.Empty(restored.Metrics);
            InspectionRunRecordReports.WriteHtml(htmlPath, restored);
            InspectionRunRecordReports.WriteCsv(csvPath, restored);
            Assert.Contains("Input unavailable", File.ReadAllText(htmlPath));
            Assert.Contains("Error", File.ReadAllText(csvPath));
        }
        finally
        {
            File.Delete(recordPath);
            File.Delete(htmlPath);
            File.Delete(csvPath);
            Directory.Delete(directory);
        }
    }

    private static InspectionRunRecord CreateRecord(string directory)
    {
        var metrics = new InspectionRunMetric[] { new("MeasuredHeight", MetricKind.Length, 1.25, "raw-height", ResultStatus.Pass) };
        var overlays = new InspectionRunOverlay[] { new("measurement-marker", OverlayKind.Point, "Measured location", ResultStatus.Pass, "source") };
        return new InspectionRunRecord(
            InspectionRunRecord.CurrentSchemaVersion, "run.record-shapes", DateTimeOffset.Parse("2026-10-23T00:00:00Z"),
            new("ToolRecipe", ToolRecipeDocument.CurrentSchemaVersion, Path.Combine(directory, "recipe.json"), new string('A', 64)),
            new("source", Path.Combine(directory, "source.C3D"), new string('B', 64), 12, "raw-height"),
            "Height", ResultStatus.Pass, "Recorded measurement", 1, metrics, overlays, "NotCompared", new("", null, null, null, null, null))
        {
            Steps = [new(1, "step.height", "height", "Height", ["source"], "output", ResultStatus.Pass, "Recorded measurement", 1, metrics, overlays)]
        };
    }
}
