using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;

internal static class RunRecordReportVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static int Run(string reportPath)
    {
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "report-contract");
        Directory.CreateDirectory(directory);
        var checks = new List<(string Name, bool Passed)>();
        var originalCulture = CultureInfo.CurrentCulture;
        var cases = CreateCases(directory);
        try
        {
            foreach (var (name, record) in cases)
            {
                var snapshot = JsonSerializer.Serialize(record, JsonOptions);
                File.WriteAllText(Path.Combine(directory, name + ".record.json"), snapshot, new UTF8Encoding(false));
                byte[]? firstHtml = null;
                byte[]? firstCsv = null;
                foreach (var culture in new[] { "en-US", "ko-KR", "de-DE" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    var prefix = Path.Combine(directory, culture, name);
                    InspectionRunRecordReports.WriteHtml(prefix + ".html", record);
                    InspectionRunRecordReports.WriteCsv(prefix + ".csv", record);
                    var html = File.ReadAllBytes(prefix + ".html");
                    var csv = File.ReadAllBytes(prefix + ".csv");
                    var htmlText = Encoding.UTF8.GetString(html);
                    var csvText = Encoding.UTF8.GetString(csv);
                    checks.Add(($"{name}/{culture}/utf8-without-bom", !HasBom(html) && !HasBom(csv)));
                    checks.Add(($"{name}/{culture}/html-document", htmlText.StartsWith("<!doctype html>") && htmlText.EndsWith("</html>")));
                    checks.Add(($"{name}/{culture}/csv-final-newline", csvText.EndsWith(Environment.NewLine)));
                    checks.Add(($"{name}/{culture}/record-unchanged", snapshot == JsonSerializer.Serialize(record, JsonOptions)));
                    checks.Add(($"{name}/{culture}/source-not-required", !File.Exists(record.Source.Path) && !File.Exists(record.Recipe.Path)));
                    if (firstHtml is not null)
                    {
                        checks.Add(($"{name}/{culture}/culture-independent", html.AsSpan().SequenceEqual(firstHtml) && csv.AsSpan().SequenceEqual(firstCsv)));
                    }
                    firstHtml ??= html;
                    firstCsv ??= csv;
                    File.WriteAllText(prefix + ".html", "stale output");
                    File.WriteAllText(prefix + ".csv", "stale output");
                    InspectionRunRecordReports.WriteHtml(prefix + ".html", record);
                    InspectionRunRecordReports.WriteCsv(prefix + ".csv", record);
                    checks.Add(($"{name}/{culture}/overwrite", html.AsSpan().SequenceEqual(File.ReadAllBytes(prefix + ".html")) && csv.AsSpan().SequenceEqual(File.ReadAllBytes(prefix + ".csv"))));
                }
            }

            var legacyHtml = File.ReadAllText(Path.Combine(directory, "en-US", "legacy.html"));
            var legacyCsv = File.ReadAllText(Path.Combine(directory, "en-US", "legacy.csv"));
            checks.Add(("html-escapes-user-text", legacyHtml.Contains("&lt;script&gt;") && !legacyHtml.Contains("<script>")));
            checks.Add(("csv-quotes-delimiters-and-newlines", legacyCsv.Contains("\"<script>,\"\"quoted\"\"\r\nmetric\"")));
            checks.Add(("legacy-environment-unavailable", legacyHtml.Contains("unknown") && legacyHtml.Contains("legacy Run Record")));
            checks.Add(("empty-metrics-header-only", File.ReadAllLines(Path.Combine(directory, "en-US", "empty.csv")).Length == 1));
            checks.Add(("ordered-empty-step-retained", File.ReadAllText(Path.Combine(directory, "en-US", "ordered.csv")).Contains("\"empty-step\"")));
            checks.Add(("manual-threshold-evidence", File.ReadAllText(Path.Combine(directory, "en-US", "threshold.html")).Contains("Development mismatch:</strong> 1 -&gt; 0")));
            var measurementHtml = File.ReadAllText(Path.Combine(directory, "en-US", "measurement.html"));
            var measurementCsv = File.ReadAllText(Path.Combine(directory, "en-US", "measurement.csv"));
            var measurementJson = File.ReadAllText(Path.Combine(directory, "measurement.record.json"));
            checks.Add(("measurement-state-exported", measurementHtml.Contains("CalibratedPhysical")
                && measurementHtml.Contains("calibration-1")
                && measurementHtml.Contains("frame.calibrated")
                && measurementCsv.Contains("CalibratedPhysical")
                && measurementCsv.Contains("calibration-1")
                && measurementCsv.Contains("frame.calibrated")));
            var orderedHtml = File.ReadAllText(Path.Combine(directory, "en-US", "ordered.html"));
            var orderedCsv = File.ReadAllText(Path.Combine(directory, "en-US", "ordered.csv"));
            var orderedJsonPath = Path.Combine(directory, "ordered-roundtrip.json");
            InspectionRunRecordJson.Write(orderedJsonPath, cases.Single(item => item.Name == "ordered").Record);
            var orderedRoundtrip = InspectionRunRecordJson.Read(orderedJsonPath);
            var orderedStep = orderedRoundtrip.Steps?.LastOrDefault();
            checks.Add(("ordered-semantic-evidence-roundtrip", orderedRoundtrip.SchemaVersion == InspectionRunRecord.CurrentSchemaVersion
                && orderedRoundtrip.Source.FrameId == "frame.ordered"
                && orderedStep?.SemanticFingerprint?.Length == 64
                && orderedStep.AlgorithmEvidence?.SdkPackageId == "OpenVisionLab.Vision3D"));
            checks.Add(("ordered-semantic-evidence-exported", orderedHtml.Contains("Semantic fingerprint", StringComparison.Ordinal)
                && orderedHtml.Contains("height-measurement-execution-v1", StringComparison.Ordinal)
                && orderedHtml.Contains("OpenVisionLab.Vision3D 3.0.1-dev", StringComparison.Ordinal)
                && orderedCsv.Contains("semanticFingerprint", StringComparison.Ordinal)
                && orderedCsv.Contains("height-measurement-execution-v1", StringComparison.Ordinal)
                && orderedCsv.Contains("OpenVisionLab.Vision3D", StringComparison.Ordinal)));
            checks.Add(("measurement-state-recorded", measurementJson.Contains("\"MeasurementEvidence\"", StringComparison.Ordinal)
                && measurementJson.Contains("\"State\": \"CalibratedPhysical\"", StringComparison.Ordinal)
                && measurementJson.Contains("\"CalibrationId\": \"calibration-1\"", StringComparison.Ordinal)));

            foreach (var (name, write) in new (string, Action<string, InspectionRunRecord>)[]
            {
                ("html", InspectionRunRecordReports.WriteHtml),
                ("csv", InspectionRunRecordReports.WriteCsv)
            })
            {
                var blockedParent = Path.Combine(directory, "blocked-parent-" + name);
                File.WriteAllText(blockedParent, "parent remains a file");
                checks.Add(($"{name}/invalid-parent-propagates", ThrowsIo(() => write(Path.Combine(blockedParent, "output"), cases[0].Record))));
                var lockedPath = Path.Combine(directory, "locked-output." + name);
                File.WriteAllText(lockedPath, "preserve locked output");
                using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    checks.Add(($"{name}/locked-output-propagates", ThrowsIo(() => write(lockedPath, cases[0].Record))));
                }
                checks.Add(($"{name}/locked-output-preserved", File.ReadAllText(lockedPath) == "preserve locked output"));
                checks.Add(($"{name}/locked-output-temp-cleanup", !Directory.GetFiles(
                    directory,
                    $"locked-output.{name}.tmp.*").Any()));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        var passed = checks.Count(check => check.Passed);
        var lines = checks.Select(check => $"{(check.Passed ? "PASS" : "FAIL")} | {check.Name}").ToList();
        lines.Add($"Run Record report verification: {passed}/{checks.Count} passed");
        File.WriteAllLines(reportPath, lines, new UTF8Encoding(false));
        Console.WriteLine(lines[^1]);
        return passed == checks.Count ? 0 : 1;
    }

    private static List<(string Name, InspectionRunRecord Record)> CreateCases(string directory)
    {
        var metric = new InspectionRunMetric("<script>,\"quoted\"\r\nmetric", MetricKind.Length, -1234.56789, "mm<&>", ResultStatus.Warning);
        var legacy = new InspectionRunRecord(
            "1.2", "run-고정<&>", DateTimeOffset.UnixEpoch,
            new InspectionRunRecipe("recipe<&>", "1.0", Path.Combine(directory, "absent.recipe.json"), new string('A', 64)),
            new InspectionRunSource("source<&>", Path.Combine(directory, "absent.C3D"), new string('B', 64), 123, "mm"),
            "tool<&>", ResultStatus.Warning, "<script> & \"text\"\n한글", 1.25,
            [metric, new("small", MetricKind.Number, 1.2345e-12, "ratio", null)], [], "NotCompared",
            new InspectionRunArtifacts("recorded.txt", null, null, null, null, null));
        var timing = InspectionRunTiming.Available(InspectionRunTiming.StopwatchClock, 1.25, [new("tool-execution", 1.25)], "recorded timing");
        var step = new InspectionRunStepResult(1, "metric-step", "tool-id", "tool<&>", ["source", "reference"], "result",
            ResultStatus.Warning, "recorded result", 1.25, [metric], [new("overlay<&>", OverlayKind.Point, "label", null, "source")])
        {
            Timing = timing,
            OutputContentSha256 = new string('C', 64),
            SemanticFingerprint = new string('F', 64),
            AlgorithmEvidence = new InspectionRunAlgorithmEvidence(
                "height-measurement-execution-v1",
                "OpenVisionLab.Vision3D",
                "3.0.1-dev.20260829.normal-preparation.1")
        };
        var context = legacy with
        {
            ExecutionEnvironment = new("recorded-app", "0.2.0-dev", "1.0", "recorded-commit", "recorded-tree", "recorded-runtime", "recorded-os", "x64"),
            Step = new("single-step", "source", ["reference,one"], ["measurement\"one"]),
            Timing = timing,
            SourceQualityEvidence = InspectionRunSourceQualityEvidence.Unavailable("source not captured <reason>")
        };
        var ordered = context with
        {
            SchemaVersion = InspectionRunRecord.CurrentSchemaVersion,
            Source = context.Source with { FrameId = "frame.ordered" },
            Steps = [step with { RecipeIndex = 0, Id = "empty-step", Metrics = [], Overlays = [], Timing = null }, step],
            Timing = InspectionRunTiming.Unavailable("no overall observation")
        };
        var measurementSnapshot = C3DHeightFieldSnapshot.CreateForVerification(
            "source.measurement",
            2,
            2,
            [1.0, 2.0, 3.0, 4.0],
            unit: "mm",
            frameId: "frame.calibrated");
        var measurementEvidence = HeightMeasurementEvidence.CalibratedPhysical(
            "sensor-1",
            "calibration-1",
            measurementSnapshot.FrameId,
            "Operator supplied calibration record calibration-1.",
            DateTimeOffset.UtcNow.AddHours(1));
        var measurementSource = ordered.Source with
        {
            EntityId = measurementSnapshot.EntityId,
            ByteLength = measurementSnapshot.ByteLength,
            Sha256 = measurementSnapshot.ContentSha256,
            Unit = measurementSnapshot.Unit,
            FrameId = measurementSnapshot.FrameId,
            SensorId = "sensor-1",
            MeasurementEvidence = measurementEvidence
        };
        var measurementReport = C3DSourceQualityAnalyzer.Create(
            measurementSnapshot,
            measurementEvidence: measurementEvidence,
            sourceSensorId: "sensor-1");
        var measurement = ordered with
        {
            RunId = "run-measurement-evidence",
            Source = measurementSource,
            SourceQualityEvidence = InspectionRunSourceQualityEvidence.Available(
                measurementSource,
                measurementReport)
        };
        var candidate = new ToolRecipeThresholdCandidate("candidate", ToolRecipeEvidenceScope.StepMetric, "metric-step", "step", "height", "mm",
            ToolRecipeThresholdLimitKind.Maximum, null, 2, 1, 0, 1, 0, []);
        var proposal = new ToolRecipeThresholdParameterProposal("1.0", "candidate", "metric-step", "tool-id", "tool", "height",
            ToolRecipeThresholdLimitKind.Maximum, [new("MaximumHeight", "1", "2")], candidate);
        var before = new ToolRecipeThresholdDevelopmentSampleEvidence(1, "before<&>", "before.C3D", ToolRecipeValidationSampleRole.Good, ResultStatus.Fail, false, "mismatch", []);
        var correction = new ToolRecipeThresholdManualCorrectionEvidence("1.0", [new("MaximumHeight", "2", "2.5")], 1, [before], 0,
            [before with { SampleIdentity = "after<&>", Status = ResultStatus.Pass, ExpectedMatch = true }]);
        var threshold = new ToolRecipeThresholdCorrectionEvidence("1.0", "recipe", new string('A', 64), proposal, ResultStatus.Pass, "committed",
            [new(2, "held-out<&>", "held-out.C3D", ResultStatus.Pass, "observed", [])], correction);
        return
        [
            ("legacy", legacy),
            ("empty", legacy with { Metrics = [], Steps = [] }),
            ("context", context),
            ("ordered", ordered),
            ("measurement", measurement),
            ("threshold", ordered with { ThresholdCorrectionEvidence = new(InspectionRunThresholdCorrectionEvidenceState.Available, "available", "sidecar.json", new string('D', 64), threshold) }),
            ("stale-threshold", ordered with { ThresholdCorrectionEvidence = new(InspectionRunThresholdCorrectionEvidenceState.Stale, "stale <reason>", "sidecar.json", null, null) })
        ];
    }

    private static bool HasBom(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF });

    private static bool ThrowsIo(Action action)
    {
        try { action(); return false; }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
