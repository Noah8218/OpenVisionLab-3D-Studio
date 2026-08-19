using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class PrivacySafeSupportBundleVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D privacy-safe support bundle verification",
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

        try
        {
            var reportDirectory = Path.GetDirectoryName(
                Path.GetFullPath(reportPath))!;
            var root = Path.Combine(
                reportDirectory,
                $"support-bundle-fixture-{Guid.NewGuid():N}");
            var privateRoot = Path.Combine(
                root,
                $"private-{Environment.UserName}-{Environment.MachineName}");
            Directory.CreateDirectory(privateRoot);
            var fixture = CreateFixture(privateRoot);
            var logs = Enumerable.Range(0, 250)
                .Select(index => new ToolWorkbenchLogItem(
                    $"12:{index / 60:00}:{index % 60:00}",
                    "Support",
                    $"entry={index:D3}|path={privateRoot}\\log-{index:D3}.txt|username={Environment.UserName}|machine={Environment.MachineName}"))
                .ToArray();
            var exportRoot = Path.Combine(root, "exports");
            var first = PrivacySafeSupportBundleWriter.Write(
                fixture.RunRecordPath,
                exportRoot,
                logs);
            var second = PrivacySafeSupportBundleWriter.Write(
                fixture.RunRecordPath,
                exportRoot,
                logs);

            Check(
                "collision-safe ZIP names preserve both exports",
                File.Exists(first)
                && File.Exists(second)
                && !string.Equals(first, second, StringComparison.OrdinalIgnoreCase),
                $"first={Path.GetFileName(first)};second={Path.GetFileName(second)}");

            var entries = ReadEntries(first);
            var expectedEntries = new[]
            {
                "manifest.json",
                "recipe.json",
                "log-excerpt.json",
                "source-identity.json",
                "source-quality.json",
                "current-result.json"
            };
            Check(
                "bundle contains only the six documented entries",
                entries.Keys.OrderBy(name => name, StringComparer.Ordinal)
                    .SequenceEqual(expectedEntries.OrderBy(name => name, StringComparer.Ordinal)),
                string.Join(",", entries.Keys.OrderBy(name => name, StringComparer.Ordinal)));

            using var manifest = JsonDocument.Parse(entries["manifest.json"]);
            var manifestPayloads = manifest.RootElement
                .GetProperty("Payloads")
                .EnumerateArray()
                .ToArray();
            var manifestIntegrity = manifestPayloads.All(payload =>
            {
                var entryName = payload.GetProperty("Entry").GetString()!;
                var bytes = entries[entryName];
                return payload.GetProperty("Length").GetInt32() == bytes.Length
                    && string.Equals(
                        payload.GetProperty("Sha256").GetString(),
                        Convert.ToHexString(SHA256.HashData(bytes)),
                        StringComparison.OrdinalIgnoreCase);
            });
            Check(
                "manifest names every payload with byte length and SHA-256",
                manifestPayloads.Length == 5 && manifestIntegrity,
                $"payloads={manifestPayloads.Length};integrity={manifestIntegrity}");

            var allText = string.Join(
                "\n",
                entries.Values.Select(bytes => Encoding.UTF8.GetString(bytes)));
            Check(
                "raw source bytes and exact private roots are absent",
                !allText.Contains(fixture.RawSourceMarker, StringComparison.Ordinal)
                && !allText.Contains(privateRoot, StringComparison.OrdinalIgnoreCase)
                && !allText.Contains(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    StringComparison.OrdinalIgnoreCase),
                $"sourceMarker={fixture.RawSourceMarker};privateRoot={privateRoot}");
            Check(
                "labeled user and machine identity are redacted from logs",
                !allText.Contains(
                    $"username={Environment.UserName}",
                    StringComparison.OrdinalIgnoreCase)
                && !allText.Contains(
                    $"machine={Environment.MachineName}",
                    StringComparison.OrdinalIgnoreCase),
                "username and machine labels absent");

            using var recipe = JsonDocument.Parse(entries["recipe.json"]);
            var recipeDocument = recipe.RootElement.GetProperty("Recipe");
            var recipeParameter = recipeDocument.GetProperty("Steps")[0]
                .GetProperty("Parameters")[0];
            Check(
                "recipe preserves algorithm configuration while omitting free-form names notes and paths",
                recipe.RootElement.GetProperty("State").GetString() == "Available"
                && recipeDocument.GetProperty("Name").GetString() == "<omitted>"
                && recipeDocument.GetProperty("Source").GetProperty("Path").GetString() == "<omitted-path>"
                && recipeDocument.GetProperty("Source").GetProperty("AcquisitionProvenance").GetProperty("Evidence").GetString() == "<omitted>"
                && recipeParameter.GetProperty("Name").GetString() == "Method"
                && recipeParameter.GetProperty("Value").GetString() == "Median",
                $"state={recipe.RootElement.GetProperty("State").GetString()};parameter={recipeParameter}");

            using var log = JsonDocument.Parse(entries["log-excerpt.json"]);
            var logEntries = log.RootElement.GetProperty("Entries");
            Check(
                "session log is newest-first and bounded to 200 entries",
                log.RootElement.GetProperty("NewestFirst").GetBoolean()
                && logEntries.GetArrayLength()
                    == PrivacySafeSupportBundleWriter.MaximumSessionLogEntries
                && logEntries[0].GetProperty("Message").GetString()!.Contains("entry=000", StringComparison.Ordinal)
                && logEntries[199].GetProperty("Message").GetString()!.Contains("entry=199", StringComparison.Ordinal),
                $"count={logEntries.GetArrayLength()}");

            using var sourceIdentity = JsonDocument.Parse(
                entries["source-identity.json"]);
            Check(
                "source identity keeps hash size unit and explicitly excludes bytes and path",
                sourceIdentity.RootElement.GetProperty("Sha256").GetString()
                    == fixture.SourceSha256
                && sourceIdentity.RootElement.GetProperty("ByteLength").GetInt64()
                    == fixture.SourceByteLength
                && !sourceIdentity.RootElement.GetProperty("SourceBytesIncluded").GetBoolean()
                && sourceIdentity.RootElement.GetProperty("Path").GetString()
                    == "<omitted-path>",
                sourceIdentity.RootElement.ToString());

            using var sourceQuality = JsonDocument.Parse(
                entries["source-quality.json"]);
            Check(
                "exact recorded Source Quality identity is retained with its path omitted",
                sourceQuality.RootElement.GetProperty("State").GetString() == "Available"
                && sourceQuality.RootElement.GetProperty("Evidence")
                    .GetProperty("SourceQualitySha256").GetString()
                    == fixture.SourceQualitySha256
                && sourceQuality.RootElement.GetProperty("Evidence")
                    .GetProperty("Report").GetProperty("Source")
                    .GetProperty("Path").GetString() == "<omitted-path>",
                $"quality={fixture.SourceQualitySha256}");

            using var currentResult = JsonDocument.Parse(
                entries["current-result.json"]);
            Check(
                "current result keeps decision metrics overlays and timing without environment or artifact paths",
                currentResult.RootElement.GetProperty("Status").GetString() == "Pass"
                && currentResult.RootElement.GetProperty("Metrics").GetArrayLength() == 1
                && currentResult.RootElement.GetProperty("Overlays").GetArrayLength() == 1
                && currentResult.RootElement.GetProperty("Timing").GetProperty("State").GetString() == "Available"
                && !currentResult.RootElement.TryGetProperty("Artifacts", out _)
                && !currentResult.RootElement.TryGetProperty("ExecutionEnvironment", out _),
                currentResult.RootElement.GetProperty("RunId").GetString() ?? string.Empty);

            var legacy = CreateLegacyFixture(root, fixture);
            var legacyBundle = PrivacySafeSupportBundleWriter.Write(
                legacy,
                exportRoot,
                []);
            var legacyEntries = ReadEntries(legacyBundle);
            using var legacyRecipe = JsonDocument.Parse(legacyEntries["recipe.json"]);
            using var legacyQuality = JsonDocument.Parse(legacyEntries["source-quality.json"]);
            Check(
                "missing recipe and legacy Source Quality are explicit unavailable payloads",
                legacyRecipe.RootElement.GetProperty("State").GetString() == "Unavailable"
                && legacyQuality.RootElement.GetProperty("State").GetString() == "Unavailable"
                && legacyRecipe.RootElement.GetProperty("Message").GetString()!.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
                && legacyQuality.RootElement.GetProperty("Message").GetString()!.Contains("legacy", StringComparison.OrdinalIgnoreCase),
                "recipe=Unavailable;sourceQuality=Unavailable");

            var invalid = CreateInvalidQualityFixture(root, fixture);
            var exportCountBeforeInvalid = Directory.GetFiles(exportRoot, "*.zip").Length;
            var invalidRejected = false;
            try
            {
                PrivacySafeSupportBundleWriter.Write(invalid, exportRoot, []);
            }
            catch (InvalidDataException)
            {
                invalidRejected = true;
            }
            Check(
                "invalid Source Quality fails closed without leaving a ZIP",
                invalidRejected
                && Directory.GetFiles(exportRoot, "*.zip").Length == exportCountBeforeInvalid,
                $"rejected={invalidRejected};zipCount={Directory.GetFiles(exportRoot, "*.zip").Length}");

            var recentPath = Path.Combine(root, "recent.json");
            var viewModel = new ShellMainWindowViewModel(
                runRecordPath: fixture.RunRecordPath,
                recentRunRecordsPath: recentPath);
            var stepCount = viewModel.Workbench.PipelineSteps.Count;
            var logCount = viewModel.Workbench.RunLog.Count;
            var dirty = viewModel.Workbench.IsDirty;
            var vmExported = viewModel.ExportPrivacySafeSupportBundle(
                exportRoot,
                out var vmPath);
            Check(
                "ViewModel export is enabled and presentation-only",
                viewModel.ExportPrivacySafeSupportBundleCommand.CanExecute(null)
                && vmExported
                && File.Exists(vmPath)
                && viewModel.Workbench.PipelineSteps.Count == stepCount
                && viewModel.Workbench.RunLog.Count == logCount
                && viewModel.Workbench.IsDirty == dirty
                && !viewModel.Workbench.IsSelectedStepPreviewRunning
                && !viewModel.Workbench.IsValidationSetRunning,
                $"exported={vmExported};steps={stepCount}->{viewModel.Workbench.PipelineSteps.Count};logs={logCount}->{viewModel.Workbench.RunLog.Count};dirty={dirty}->{viewModel.Workbench.IsDirty}");

            var requestCount = 0;
            viewModel.ExportPrivacySafeSupportBundleRequested += (_, _) => requestCount++;
            viewModel.ExportPrivacySafeSupportBundleCommand.Execute(null);
            Check(
                "command raises one explicit folder-selection request and does not auto-export",
                requestCount == 1
                && Directory.GetFiles(exportRoot, "*.zip").Length
                    == exportCountBeforeInvalid + 1,
                $"requests={requestCount};zipCount={Directory.GetFiles(exportRoot, "*.zip").Length}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith(
                "FAIL | unexpected exception",
                StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Privacy-safe support bundle verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static Fixture CreateFixture(string root)
    {
        var rawSourceMarker = "RAW_3D_SOURCE_BYTES_SECRET_MARKER";
        var sourcePath = Path.Combine(root, "private-source.C3D");
        File.WriteAllText(sourcePath, rawSourceMarker);
        var sourceBytes = File.ReadAllBytes(sourcePath);
        var sourceSha256 = Convert.ToHexString(SHA256.HashData(sourceBytes));
        var recipePath = Path.Combine(root, "private-recipe.ov3d-recipe.json");
        var document = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Operator private recipe name",
            new ToolRecipeSource(
                "source.private",
                "Private source file name",
                "C3D",
                "raw-height",
                "frame.private",
                sourcePath,
                sourceBytes.LongLength,
                sourceSha256,
                2,
                2,
                new ToolRecipeAcquisitionProvenance(
                    ToolRecipeAcquisitionProvenanceState.Available,
                    $"operator={Environment.UserName};path={sourcePath}",
                    $"machine={Environment.MachineName}")),
            [],
            [
                new ToolRecipeStep(
                    "step.private",
                    "filter",
                    "Filter",
                    1,
                    ["source.private"],
                    "result.private",
                    [
                        new ToolRecipeParameter("Method", "Median"),
                        new ToolRecipeParameter("KernelSize", "3"),
                        new ToolRecipeParameter("MissingValuePolicy", "PreserveMask"),
                        new ToolRecipeParameter("BoundaryPolicy", "AvailableNeighbors")
                    ])
            ]);
        ToolRecipeDocumentStore.Save(recipePath, document);
        var recipeSha256 = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(recipePath)));
        var source = new InspectionRunSource(
            "source.private",
            sourcePath,
            sourceSha256,
            sourceBytes.LongLength,
            "raw-height");
        var report = new SourceQualityReport(
            SourceQualityReport.CurrentSchemaVersion,
            new SourceQualitySourceIdentity(
                source.EntityId,
                "C3D",
                sourcePath,
                source.ByteLength,
                source.Sha256,
                source.Sha256),
            new SourceQualityGrid(2, 2, 4),
            new SourceQualityCoverage(
                4,
                4,
                0,
                1.0,
                0.0,
                "explicit-mask",
                new SourceQualityInvalidCellMaskIdentity(
                    "1.0",
                    "packed-bit-lsb0",
                    1,
                    Convert.ToHexString(SHA256.HashData([0])))),
            new SourceQualityHeightStatistics("raw-height", 1, 4, 2.5, null),
            new SourceQualityCoordinateContext(
                "raw-height",
                "frame.private",
                "c3d-grid-index"),
            $"path={sourcePath};operator={Environment.UserName}",
            false,
            [
                new SourceQualityChannelAvailability(
                    SourceQualityChannel.Height,
                    SourceQualityChannelState.Available,
                    $"source={sourcePath}")
            ]);
        var quality = InspectionRunSourceQualityEvidence.Available(source, report);
        var runRecordPath = Path.Combine(root, "private-run-record.json");
        var timing = InspectionRunTiming.Available(
            InspectionRunTiming.StopwatchClock,
            2.5,
            [new InspectionRunStageTiming(InspectionRunTiming.ToolExecutionStage, 2.5)],
            "Recorded from the ordered run.");
        var record = new InspectionRunRecord(
            "1.9",
            "run-private-support",
            new DateTimeOffset(2026, 8, 18, 4, 0, 0, TimeSpan.Zero),
            new InspectionRunRecipe(
                "tool-recipe",
                ToolRecipeDocument.CurrentSchemaVersion,
                recipePath,
                recipeSha256),
            source,
            "Thickness",
            ResultStatus.Pass,
            $"result path={root}",
            2.5,
            [new InspectionRunMetric("Thickness", MetricKind.Length, 3.0, "raw-height", ResultStatus.Pass)],
            [new InspectionRunOverlay("overlay.private", OverlayKind.Point, "Private overlay", ResultStatus.Pass, source.EntityId)],
            "NotCompared",
            new InspectionRunArtifacts(
                Path.Combine(root, "runner.txt"),
                null,
                null,
                runRecordPath,
                Path.Combine(root, "run.html"),
                Path.Combine(root, "run.csv")))
        {
            SourceQualityEvidence = quality,
            Timing = timing,
            ExecutionEnvironment = new InspectionRunEnvironment(
                "OpenVisionLab 3D Studio",
                "private",
                "1.0",
                "commit",
                "clean",
                ".NET",
                Environment.UserName,
                Environment.MachineName)
        };
        File.WriteAllText(
            runRecordPath,
            JsonSerializer.Serialize(record, JsonOptions));
        return new Fixture(
            runRecordPath,
            sourceSha256,
            sourceBytes.LongLength,
            quality.SourceQualitySha256,
            rawSourceMarker,
            record);
    }

    private static string CreateLegacyFixture(string root, Fixture fixture)
    {
        var path = Path.Combine(root, "legacy-run-record.json");
        var record = fixture.Record with
        {
            RunId = "run-legacy-support",
            Recipe = fixture.Record.Recipe with
            {
                Path = Path.Combine(root, "missing.recipe.json")
            },
            SourceQualityEvidence = null
        };
        File.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions));
        return path;
    }

    private static string CreateInvalidQualityFixture(
        string root,
        Fixture fixture)
    {
        var path = Path.Combine(root, "invalid-quality-run-record.json");
        var evidence = fixture.Record.SourceQualityEvidence! with
        {
            SourceQualitySha256 = new string('0', 64)
        };
        var record = fixture.Record with
        {
            RunId = "run-invalid-quality",
            SourceQualityEvidence = evidence
        };
        File.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions));
        return path;
    }

    private static Dictionary<string, byte[]> ReadEntries(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return archive.Entries.ToDictionary(
            entry => entry.FullName,
            entry =>
            {
                using var stream = entry.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            },
            StringComparer.Ordinal);
    }

    private sealed record Fixture(
        string RunRecordPath,
        string SourceSha256,
        long SourceByteLength,
        string SourceQualitySha256,
        string RawSourceMarker,
        InspectionRunRecord Record);
}
