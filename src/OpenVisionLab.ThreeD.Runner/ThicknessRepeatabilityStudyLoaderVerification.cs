using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ThicknessRepeatabilityStudyLoaderVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var fixtureDirectory = Path.Combine(
            Path.GetDirectoryName(fullReportPath)!,
            "thickness-repeatability-study-fixture");
        Directory.CreateDirectory(fixtureDirectory);

        var fixture = CreateFixture(fixtureDirectory);
        var loaded = ThicknessRepeatabilityStudyLoader.Load(fixture.StudyPath);
        var validation = ThicknessRepeatabilityRule.Validate(loaded.Input);
        var evaluation = ThicknessRepeatabilityRule.Evaluate(loaded.Input);
        var cases = new[]
        {
            Check("valid-relative-study-loads", () =>
                (loaded.Sources.Count == 3
                 && loaded.Sources.All(source => Path.IsPathFullyQualified(source.Path)),
                 $"study={loaded.Path},sources={loaded.Sources.Count}")),
            Check("source-identities-match-files", () =>
                (loaded.Sources.All(source =>
                    source.ByteLength == new FileInfo(source.Path).Length
                    && source.Sha256 == Hash(source.Path)),
                 string.Join(',', loaded.Sources.Select(source => source.Sha256)))),
            Check("model-input-is-ready-without-calculation", () =>
                (validation.IsReady
                 && validation.State == ThicknessRepeatabilityInputState.Ready
                 && validation.RunCount == 3,
                 $"state={validation.State},count={validation.RunCount}")),
            Check("explicit-model-calculation-passes", () =>
                (evaluation.Result.Status == ResultStatus.Pass
                 && evaluation.Decision == ThicknessRepeatabilityDecision.Accepted
                 && evaluation.RunCount == 3,
                 $"status={evaluation.Result.Status},decision={evaluation.Decision},mean={evaluation.Mean:R}")),
            Check("unsupported-study-type-rejected", () => RejectDocument(
                fixture,
                fixture.Document with { StudyType = "other-study" },
                "unsupported-type.json",
                "study type")),
            Check("unsupported-schema-version-rejected", () => RejectDocument(
                fixture,
                fixture.Document with { SchemaVersion = "2.0" },
                "unsupported-version.json",
                "schema version")),
            Check("unknown-json-property-rejected", () => RejectUnknownProperty(fixture)),
            Check("missing-source-rejected", () => RejectChangedRun(
                fixture,
                0,
                run => run with { SourcePath = "missing-source.bin" },
                "missing-source.json",
                typeof(FileNotFoundException),
                null)),
            Check("source-length-mismatch-rejected", () => RejectChangedRun(
                fixture,
                0,
                run => run with { SourceByteLength = run.SourceByteLength + 1 },
                "wrong-length.json",
                typeof(InvalidDataException),
                "byte length")),
            Check("source-sha256-mismatch-rejected", () => RejectChangedRun(
                fixture,
                0,
                run => run with { SourceSha256 = new string('0', 64) },
                "wrong-hash.json",
                typeof(InvalidDataException),
                "SHA-256")),
            Check("duplicate-source-path-rejected", () => RejectDuplicatePath(fixture)),
            Check("byte-identical-source-rejected", () => RejectDuplicateHash(fixture)),
            Check("model-invalid-unit-frame-evidence-rejected", () => VerifyModelInvalidStudy(fixture))
        };

        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"ThicknessRepeatabilityStudyLoaderVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            $"Fixture|study={fixture.StudyPath}|sources=3|synthetic=True|physicalCalibrationClaim=False",
            "Contract|json=closed-schema-1.0|relativePaths=True|byteLength=True|sha256=True|duplicatePathRejected=True|duplicateHashRejected=True"
        };
        lines.AddRange(cases.Select(item =>
            $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
        Console.WriteLine($"Thickness repeatability study loader verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static StudyFixture CreateFixture(string directory)
    {
        var capturedAt = new DateTimeOffset(2026, 7, 17, 1, 0, 0, TimeSpan.Zero);
        var values = new[] { 10.000, 10.004, 9.998 };
        var runDocuments = new ThicknessRepeatabilityStudyRunDocument[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var name = $"acquisition-{index + 1:000}.bin";
            var sourcePath = Path.Combine(directory, name);
            File.WriteAllBytes(
                sourcePath,
                Encoding.UTF8.GetBytes($"OpenVisionLab synthetic acquisition {index + 1:000}\n"));
            var info = new FileInfo(sourcePath);
            runDocuments[index] = new ThicknessRepeatabilityStudyRunDocument(
                $"run.synthetic.{index + 1:000}",
                $"source.synthetic.{index + 1:000}",
                name,
                info.Length,
                Hash(sourcePath),
                capturedAt.AddMinutes(index),
                "mm",
                "frame.synthetic-repeatability",
                values[index]);
        }

        var document = new ThicknessRepeatabilityStudyDocument(
            ThicknessRepeatabilityStudyLoader.SupportedStudyType,
            ThicknessRepeatabilityStudyLoader.SupportedSchemaVersion,
            "study.synthetic-thickness-repeatability",
            "measurement.synthetic-thickness",
            "roi.synthetic-reference",
            "mm",
            "frame.synthetic-repeatability",
            new ThicknessRepeatabilityAcceptance(3, 0.005, 0.010),
            runDocuments);
        var studyPath = Path.Combine(directory, "valid-study.json");
        WriteDocument(studyPath, document);
        return new StudyFixture(directory, studyPath, document);
    }

    private static (bool Passed, string Evidence) RejectDocument(
        StudyFixture fixture,
        ThicknessRepeatabilityStudyDocument document,
        string fileName,
        string messageFragment)
    {
        var path = Path.Combine(fixture.Directory, fileName);
        WriteDocument(path, document);
        return Reject(path, typeof(InvalidDataException), messageFragment);
    }

    private static (bool Passed, string Evidence) RejectChangedRun(
        StudyFixture fixture,
        int index,
        Func<ThicknessRepeatabilityStudyRunDocument, ThicknessRepeatabilityStudyRunDocument> change,
        string fileName,
        Type exceptionType,
        string? messageFragment)
    {
        var runs = fixture.Document.Runs!.ToArray();
        runs[index] = change(runs[index]);
        var path = Path.Combine(fixture.Directory, fileName);
        WriteDocument(path, fixture.Document with { Runs = runs });
        return Reject(path, exceptionType, messageFragment);
    }

    private static (bool Passed, string Evidence) RejectUnknownProperty(StudyFixture fixture)
    {
        var root = JsonNode.Parse(File.ReadAllText(fixture.StudyPath))!.AsObject();
        root["unexpectedProperty"] = true;
        var path = Path.Combine(fixture.Directory, "unknown-property.json");
        File.WriteAllText(path, root.ToJsonString(JsonOptions), new UTF8Encoding(false));
        return Reject(path, typeof(JsonException), "could not be mapped");
    }

    private static (bool Passed, string Evidence) RejectDuplicatePath(StudyFixture fixture)
    {
        var runs = fixture.Document.Runs!.ToArray();
        runs[1] = runs[1] with
        {
            SourcePath = runs[0].SourcePath,
            SourceByteLength = runs[0].SourceByteLength,
            SourceSha256 = runs[0].SourceSha256
        };
        var path = Path.Combine(fixture.Directory, "duplicate-path.json");
        WriteDocument(path, fixture.Document with { Runs = runs });
        return Reject(path, typeof(InvalidDataException), "source path");
    }

    private static (bool Passed, string Evidence) RejectDuplicateHash(StudyFixture fixture)
    {
        var runs = fixture.Document.Runs!.ToArray();
        var duplicateName = "acquisition-duplicate.bin";
        var duplicatePath = Path.Combine(fixture.Directory, duplicateName);
        File.Copy(
            Path.Combine(fixture.Directory, runs[0].SourcePath!),
            duplicatePath,
            overwrite: true);
        runs[1] = runs[1] with
        {
            SourcePath = duplicateName,
            SourceByteLength = runs[0].SourceByteLength,
            SourceSha256 = runs[0].SourceSha256
        };
        var path = Path.Combine(fixture.Directory, "duplicate-hash.json");
        WriteDocument(path, fixture.Document with { Runs = runs });
        return Reject(path, typeof(InvalidDataException), "Byte-identical");
    }

    private static (bool Passed, string Evidence) VerifyModelInvalidStudy(StudyFixture fixture)
    {
        var runs = fixture.Document.Runs!.ToArray();
        runs[1] = runs[1] with { Unit = "um" };
        var path = Path.Combine(fixture.Directory, "model-invalid-unit.json");
        WriteDocument(path, fixture.Document with { Runs = runs });
        var loaded = ThicknessRepeatabilityStudyLoader.Load(path);
        var validation = ThicknessRepeatabilityRule.Validate(loaded.Input);
        var passed = !validation.IsReady
            && validation.State == ThicknessRepeatabilityInputState.InvalidInput
            && validation.Message.Contains("unit does not match", StringComparison.OrdinalIgnoreCase);
        return (passed, $"state={validation.State},message={validation.Message}");
    }

    private static (bool Passed, string Evidence) Reject(
        string path,
        Type exceptionType,
        string? messageFragment)
    {
        try
        {
            ThicknessRepeatabilityStudyLoader.Load(path);
            return (false, "study unexpectedly loaded");
        }
        catch (Exception exception)
        {
            var passed = exception.GetType() == exceptionType
                && (messageFragment is null
                    || exception.Message.Contains(messageFragment, StringComparison.OrdinalIgnoreCase));
            return (passed, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void WriteDocument(string path, ThicknessRepeatabilityStudyDocument document) =>
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(document, JsonOptions),
            new UTF8Encoding(false));

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static VerificationCase Check(
        string name,
        Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return new VerificationCase(name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return new VerificationCase(
                name,
                false,
                $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record StudyFixture(
        string Directory,
        string StudyPath,
        ThicknessRepeatabilityStudyDocument Document);

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
