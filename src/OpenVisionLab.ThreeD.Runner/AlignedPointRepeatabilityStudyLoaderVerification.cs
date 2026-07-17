using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class AlignedPointRepeatabilityStudyLoaderVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Run(string reportPath)
    {
        var fixtureDirectory = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab-3D-AlignedPointRepeatability",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            return RunCore(fixtureDirectory, reportPath);
        }
        finally
        {
            if (Directory.Exists(fixtureDirectory))
            {
                Directory.Delete(fixtureDirectory, recursive: true);
            }
        }
    }

    private static int RunCore(string fixtureDirectory, string reportPath)
    {
        var fixture = CreateFixture(fixtureDirectory);
        var loaded = AlignedPointRepeatabilityStudyLoader.Load(fixture.StudyPath);
        var validation = AlignedPointRepeatabilityRule.Validate(loaded.Input);
        var evaluation = AlignedPointRepeatabilityRule.Evaluate(loaded.Input);
        var cases = new[]
        {
            Check("valid-relative-study-loads", () =>
                (loaded.Sources.Count == 3
                 && loaded.Mappings.Count == 3
                 && loaded.Sources.All(source => Path.IsPathFullyQualified(source.Path))
                 && loaded.Mappings.All(mapping => Path.IsPathFullyQualified(mapping.Path)),
                 $"study={loaded.Path},sources={loaded.Sources.Count},mappings={loaded.Mappings.Count}")),
            Check("source-and-mapping-identities-match-files", () =>
                (loaded.Sources.All(source =>
                     source.ByteLength == new FileInfo(source.Path).Length
                     && source.Sha256 == Hash(source.Path))
                 && loaded.Mappings.All(mapping =>
                     mapping.ByteLength == new FileInfo(mapping.Path).Length
                     && mapping.Sha256 == Hash(mapping.Path)),
                 $"sourceHashes={string.Join(',', loaded.Sources.Select(source => source.Sha256))},mappingHashes={string.Join(',', loaded.Mappings.Select(mapping => mapping.Sha256))}")),
            Check("study-identity-matches-loaded-bytes", () =>
                (loaded.Study.ByteLength == new FileInfo(loaded.Study.Path).Length
                 && loaded.Study.Sha256 == Hash(loaded.Study.Path),
                 $"path={loaded.Study.Path},bytes={loaded.Study.ByteLength},sha256={loaded.Study.Sha256}")),
            Check("utf8-bom-study-and-mapping-load-with-identities", VerifyUtf8BomStudyAndMapping),
            Check("loaded-input-is-ready-and-calculates", () =>
                (validation.IsReady
                 && validation.State == AlignedPointRepeatabilityInputState.Ready
                 && evaluation.Result.Status == ResultStatus.Pass
                 && evaluation.Decision == AlignedPointRepeatabilityDecision.Accepted
                 && evaluation.RunCount == 3
                 && evaluation.CorrespondenceCount == 3,
                 $"state={validation.State},status={evaluation.Result.Status},runs={evaluation.RunCount},correspondences={evaluation.CorrespondenceCount}")),
            Check("headless-study-runner-emits-provenance-report", () => VerifyHeadlessStudyRunnerReport(fixture)),
            Check("headless-study-runner-writes-error-report-for-altered-source", VerifyHeadlessStudyRunnerErrorReport),
            Check("unsupported-study-type-and-version-rejected", VerifyUnsupportedStudyTypeAndVersion),
            Check("unknown-study-property-rejected", VerifyUnknownStudyProperty),
            Check("unsupported-mapping-type-and-version-rejected", VerifyUnsupportedMappingTypeAndVersion),
            Check("unknown-mapping-property-rejected", VerifyUnknownMappingProperty),
            Check("source-file-identity-failures-rejected", VerifySourceFileIdentityFailures),
            Check("mapping-file-identity-failures-rejected", VerifyMappingFileIdentityFailures),
            Check("duplicate-source-path-and-hash-rejected", VerifyDuplicateSourcePathAndHash),
            Check("duplicate-mapping-path-and-hash-rejected", VerifyDuplicateMappingPathAndHash),
            Check("mapping-run-and-source-binding-rejected", VerifyMappingRunAndSourceBinding),
            Check("mapping-unit-frame-alignment-correspondence-rejected", VerifyMappingContextBinding),
            Check("mapping-alignment-method-evidence-required", VerifyMappingAlignmentEvidence),
            Check("mapping-coverage-failures-rejected", VerifyMappingCoverageFailures),
            Check("duplicate-reference-point-rejected", VerifyDuplicateReferencePoint)
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"AlignedPointRepeatabilityStudyLoaderVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
            $"Fixture|study={fixture.StudyPath}|sources=3|mappings=3|synthetic=True|physicalCalibrationClaim=False",
            "Contract|study=closed-schema-1.0|mapping=closed-schema-1.0|sourceByteLength=True|sourceSha256=True|mappingByteLength=True|mappingSha256=True|fullCoverage=True|physicalCalibrationClaim=False"
        };
        lines.AddRange(cases.Select(item =>
            $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));

        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
        Console.WriteLine($"Aligned point repeatability study loader verification: {status} ({passedCount}/{cases.Length})");
        return passedCount == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyHeadlessStudyRunnerReport(StudyFixture fixture)
    {
        var reportPath = Path.Combine(fixture.Directory, "headless-study-runner-report.txt");
        var exitCode = AlignedPointRepeatabilityStudyExecution.Run(fixture.StudyPath, reportPath);
        var lines = File.ReadAllLines(reportPath);
        var passed = exitCode == 0
            && lines.Any(line => line.StartsWith("AlignedPointRepeatabilityStudyRun|Pass|", StringComparison.Ordinal))
            && lines.Any(line => line.StartsWith("Study|", StringComparison.Ordinal)
                                 && line.Contains($"bytes={new FileInfo(fixture.StudyPath).Length}", StringComparison.Ordinal)
                                 && line.Contains($"sha256={Hash(fixture.StudyPath)}", StringComparison.Ordinal))
            && lines.Any(line => line.StartsWith("Source|run=run.synthetic.001|", StringComparison.Ordinal))
            && lines.Any(line => line.StartsWith("Mapping|run=run.synthetic.001|", StringComparison.Ordinal))
            && lines.Any(line => line.StartsWith("Point|id=point.alpha|", StringComparison.Ordinal))
            && lines.Contains("ClaimBoundary|physicalCalibration=False|gaugeRr=False|mappingDerivationVerified=False|viewerLinkedSelection=False");
        return (passed, $"exit={exitCode},lines={lines.Length}");
    }

    private static (bool Passed, string Evidence) VerifyHeadlessStudyRunnerErrorReport()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            var sourcePath = Path.Combine(fixture.Directory, fixture.Document.Runs![0].SourcePath!);
            File.AppendAllText(sourcePath, "altered");
            var reportPath = Path.Combine(fixture.Directory, "headless-study-runner-error.txt");
            var exitCode = AlignedPointRepeatabilityStudyExecution.Run(fixture.StudyPath, reportPath);
            var lines = File.ReadAllLines(reportPath);
            var passed = exitCode == 1
                && lines.Any(line => line.StartsWith("AlignedPointRepeatabilityStudyRun|Error|", StringComparison.Ordinal))
                && lines.Any(line => line.Contains("byte length", StringComparison.OrdinalIgnoreCase));
            return (passed, $"exit={exitCode},lines={lines.Length}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyUtf8BomStudyAndMapping()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            var mappingPath = Path.Combine(fixture.Directory, fixture.Document.Runs![0].MappingPath!);
            WriteMapping(mappingPath, fixture.MappingDocuments[0], new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var mappingInfo = new FileInfo(mappingPath);
            var runs = fixture.Document.Runs.ToArray();
            runs[0] = runs[0] with
            {
                MappingByteLength = mappingInfo.Length,
                MappingSha256 = Hash(mappingPath)
            };
            var studyPath = Path.Combine(fixture.Directory, "utf8-bom-study.json");
            WriteStudy(
                studyPath,
                fixture.Document with { Runs = runs },
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var loaded = AlignedPointRepeatabilityStudyLoader.Load(studyPath);
            var evaluation = AlignedPointRepeatabilityRule.Evaluate(loaded.Input);
            var passed = loaded.Study.ByteLength == new FileInfo(studyPath).Length
                && loaded.Study.Sha256 == Hash(studyPath)
                && loaded.Mappings[0].ByteLength == mappingInfo.Length
                && loaded.Mappings[0].Sha256 == Hash(mappingPath)
                && evaluation.Result.Status == ResultStatus.Pass;
            return (passed, $"studyBytes={loaded.Study.ByteLength},mappingBytes={loaded.Mappings[0].ByteLength},status={evaluation.Result.Status}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyUnsupportedStudyTypeAndVersion()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var type = RejectDocument(
                fixture,
                fixture.Document with { StudyType = "other-study" },
                "unsupported-study-type.json",
                typeof(InvalidDataException),
                "study type");
            var version = RejectDocument(
                fixture,
                fixture.Document with { SchemaVersion = "2.0" },
                "unsupported-study-version.json",
                typeof(InvalidDataException),
                "schema version");
            return (type.Passed && version.Passed, $"type={type.Evidence},version={version.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyUnknownStudyProperty()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var root = JsonNode.Parse(File.ReadAllText(fixture.StudyPath))!.AsObject();
            root["unexpectedProperty"] = true;
            var path = Path.Combine(fixture.Directory, "unknown-study-property.json");
            File.WriteAllText(path, root.ToJsonString(JsonOptions), new UTF8Encoding(false));
            return Reject(path, typeof(JsonException), "could not be mapped");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyUnsupportedMappingTypeAndVersion()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var type = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { MappingType = "other-mapping" },
                "unsupported-mapping-type",
                typeof(InvalidDataException),
                "mapping type");
            var version = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { SchemaVersion = "2.0" },
                "unsupported-mapping-version",
                typeof(InvalidDataException),
                "mapping schema version");
            return (type.Passed && version.Passed, $"type={type.Evidence},version={version.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyUnknownMappingProperty()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var originalPath = Path.Combine(fixture.Directory, fixture.Document.Runs![0].MappingPath!);
            var root = JsonNode.Parse(File.ReadAllText(originalPath))!.AsObject();
            root["unexpectedProperty"] = true;
            var mappingPath = Path.Combine(fixture.Directory, "unknown-mapping-property.json");
            File.WriteAllText(mappingPath, root.ToJsonString(JsonOptions), new UTF8Encoding(false));
            var studyPath = WriteStudyWithMapping(fixture, 0, mappingPath, "unknown-mapping-study.json");
            return Reject(studyPath, typeof(JsonException), "could not be mapped");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifySourceFileIdentityFailures()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var missing = RejectChangedRun(
                fixture,
                0,
                run => run with { SourcePath = "missing-source.bin" },
                "missing-source.json",
                typeof(FileNotFoundException),
                null);
            var length = RejectChangedRun(
                fixture,
                0,
                run => run with { SourceByteLength = run.SourceByteLength + 1 },
                "wrong-source-length.json",
                typeof(InvalidDataException),
                "Source for run");
            var hash = RejectChangedRun(
                fixture,
                0,
                run => run with { SourceSha256 = new string('0', 64) },
                "wrong-source-hash.json",
                typeof(InvalidDataException),
                "SHA-256");
            return (missing.Passed && length.Passed && hash.Passed, $"missing={missing.Evidence},length={length.Evidence},hash={hash.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyMappingFileIdentityFailures()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var missing = RejectChangedRun(
                fixture,
                0,
                run => run with { MappingPath = "no-such-mapping.json" },
                "missing-mapping-study.json",
                typeof(FileNotFoundException),
                null);
            var length = RejectChangedRun(
                fixture,
                0,
                run => run with { MappingByteLength = run.MappingByteLength + 1 },
                "wrong-mapping-length.json",
                typeof(InvalidDataException),
                "Correspondence mapping");
            var hash = RejectChangedRun(
                fixture,
                0,
                run => run with { MappingSha256 = new string('0', 64) },
                "wrong-mapping-hash.json",
                typeof(InvalidDataException),
                "SHA-256");
            return (missing.Passed && length.Passed && hash.Passed, $"missing={missing.Evidence},length={length.Evidence},hash={hash.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyDuplicateSourcePathAndHash()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var sourceRuns = fixture.Document.Runs!;
            var path = RejectChangedRun(
                fixture,
                1,
                run => run with
                {
                    SourcePath = sourceRuns[0].SourcePath,
                    SourceByteLength = sourceRuns[0].SourceByteLength,
                    SourceSha256 = sourceRuns[0].SourceSha256
                },
                "duplicate-source-path.json",
                typeof(InvalidDataException),
                "source path");
            var duplicatePath = Path.Combine(fixture.Directory, "duplicate-source.bin");
            File.Copy(
                Path.Combine(fixture.Directory, sourceRuns[0].SourcePath!),
                duplicatePath,
                overwrite: true);
            var hash = RejectChangedRun(
                fixture,
                1,
                run => run with
                {
                    SourcePath = Path.GetFileName(duplicatePath),
                    SourceByteLength = sourceRuns[0].SourceByteLength,
                    SourceSha256 = sourceRuns[0].SourceSha256
                },
                "duplicate-source-hash.json",
                typeof(InvalidDataException),
                "Byte-identical source");
            return (path.Passed && hash.Passed, $"path={path.Evidence},hash={hash.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyDuplicateMappingPathAndHash()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var runs = fixture.Document.Runs!;
            var path = RejectChangedRun(
                fixture,
                1,
                run => run with
                {
                    MappingPath = runs[0].MappingPath,
                    MappingByteLength = runs[0].MappingByteLength,
                    MappingSha256 = runs[0].MappingSha256
                },
                "duplicate-mapping-path.json",
                typeof(InvalidDataException),
                "mapping path");
            var duplicatePath = Path.Combine(fixture.Directory, "duplicate-mapping.json");
            File.Copy(
                Path.Combine(fixture.Directory, runs[0].MappingPath!),
                duplicatePath,
                overwrite: true);
            var hash = RejectChangedRun(
                fixture,
                1,
                run => run with
                {
                    MappingPath = Path.GetFileName(duplicatePath),
                    MappingByteLength = runs[0].MappingByteLength,
                    MappingSha256 = runs[0].MappingSha256
                },
                "duplicate-mapping-hash.json",
                typeof(InvalidDataException),
                "Byte-identical correspondence mapping");
            return (path.Passed && hash.Passed, $"path={path.Evidence},hash={hash.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyMappingRunAndSourceBinding()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var run = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { RunId = "run.other" },
                "mapping-run-mismatch",
                typeof(InvalidDataException),
                "Mapping run ID");
            var sourceId = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { SourceEntityId = "source.other" },
                "mapping-source-id-mismatch",
                typeof(InvalidDataException),
                "Mapping source entity ID");
            var sourceLength = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { SourceByteLength = mapping.SourceByteLength + 1 },
                "mapping-source-length-mismatch",
                typeof(InvalidDataException),
                "Mapping source byte length");
            var sourceHash = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { SourceSha256 = new string('0', 64) },
                "mapping-source-hash-mismatch",
                typeof(InvalidDataException),
                "Mapping source SHA-256");
            return (run.Passed && sourceId.Passed && sourceLength.Passed && sourceHash.Passed, $"run={run.Evidence},sourceId={sourceId.Evidence},sourceLength={sourceLength.Evidence},sourceHash={sourceHash.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyMappingContextBinding()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var unit = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { Unit = "um" },
                "mapping-unit-mismatch",
                typeof(InvalidDataException),
                "Mapping unit");
            var frame = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { FrameId = "frame.other" },
                "mapping-frame-mismatch",
                typeof(InvalidDataException),
                "Mapping frame");
            var alignment = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { AlignmentReferenceId = "alignment.other" },
                "mapping-alignment-mismatch",
                typeof(InvalidDataException),
                "Mapping alignment reference");
            var correspondence = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { CorrespondenceDefinitionId = "correspondence.other" },
                "mapping-correspondence-mismatch",
                typeof(InvalidDataException),
                "Mapping correspondence definition");
            return (unit.Passed && frame.Passed && alignment.Passed && correspondence.Passed, $"unit={unit.Evidence},frame={frame.Evidence},alignment={alignment.Evidence},correspondence={correspondence.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyMappingAlignmentEvidence()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var method = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { AlignmentMethodId = "" },
                "mapping-method-missing",
                typeof(InvalidDataException),
                "alignment method and evidence");
            var evidence = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { AlignmentEvidenceId = "" },
                "mapping-evidence-missing",
                typeof(InvalidDataException),
                "alignment method and evidence");
            return (method.Passed && evidence.Passed, $"method={method.Evidence},evidence={evidence.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyMappingCoverageFailures()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var missing = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { Observations = mapping.Observations!.Take(2).ToArray() },
                "mapping-coverage-missing",
                typeof(InvalidDataException),
                "coverage does not exactly match");
            var extra = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with
                {
                    Observations = mapping.Observations!
                        .Append(new AlignedPointRepeatabilityObservation("point.extra", 0.0))
                        .ToArray()
                },
                "mapping-coverage-extra",
                typeof(InvalidDataException),
                "coverage does not exactly match");
            var observations = fixture.MappingDocuments[0].Observations!.ToArray();
            observations[1] = observations[1] with { CorrespondenceId = observations[0].CorrespondenceId };
            var duplicate = RejectChangedMapping(
                fixture,
                0,
                mapping => mapping with { Observations = observations },
                "mapping-coverage-duplicate",
                typeof(InvalidDataException),
                "duplicates correspondence ID");
            return (missing.Passed && extra.Passed && duplicate.Passed, $"missing={missing.Evidence},extra={extra.Evidence},duplicate={duplicate.Evidence}");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyDuplicateReferencePoint()
    {
        var fixture = CreateFixture(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(fixture.Directory);
            var points = fixture.Document.ReferencePoints!.ToArray();
            points[1] = points[1] with { CorrespondenceId = points[0].CorrespondenceId };
            return RejectDocument(
                fixture,
                fixture.Document with { ReferencePoints = points },
                "duplicate-reference-point.json",
                typeof(InvalidDataException),
                "Reference correspondence ID is duplicated");
        }
        finally
        {
            Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static StudyFixture CreateFixture(string directory)
    {
        Directory.CreateDirectory(directory);
        var capturedAt = new DateTimeOffset(2026, 7, 17, 3, 0, 0, TimeSpan.Zero);
        var referencePoints = new[]
        {
            new AlignedPointRepeatabilityReferencePoint("point.gamma", 2.0, 0.0, 0.0),
            new AlignedPointRepeatabilityReferencePoint("point.alpha", 0.0, 0.0, 0.0),
            new AlignedPointRepeatabilityReferencePoint("point.beta", 1.0, 0.0, 0.0)
        };
        double[][] values =
        [
            [10.000, 20.000, 30.000],
            [10.004, 20.003, 30.002],
            [9.998, 19.999, 29.998]
        ];
        var runs = new AlignedPointRepeatabilityStudyRunDocument[values.Length];
        var mappings = new AlignedPointRepeatabilityMappingDocument[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var runNumber = index + 1;
            var runId = $"run.synthetic.{runNumber:000}";
            var sourceId = $"source.synthetic.{runNumber:000}";
            var sourceName = $"acquisition-{runNumber:000}.bin";
            var sourcePath = Path.Combine(directory, sourceName);
            File.WriteAllBytes(
                sourcePath,
                Encoding.UTF8.GetBytes($"OpenVisionLab aligned synthetic acquisition {runNumber:000}\n"));
            var sourceInfo = new FileInfo(sourcePath);
            var sourceHash = Hash(sourcePath);
            var mapping = new AlignedPointRepeatabilityMappingDocument(
                AlignedPointRepeatabilityStudyLoader.SupportedMappingType,
                AlignedPointRepeatabilityStudyLoader.SupportedMappingSchemaVersion,
                runId,
                sourceId,
                sourceInfo.Length,
                sourceHash,
                "mm",
                "frame.synthetic-aligned-repeatability",
                "alignment.synthetic-fixture",
                "correspondence.synthetic-grid",
                "alignment.synthetic-fixture-method",
                $"alignment-evidence.synthetic.{runNumber:000}",
                new[]
                {
                    new AlignedPointRepeatabilityObservation("point.gamma", values[index][2]),
                    new AlignedPointRepeatabilityObservation("point.alpha", values[index][0]),
                    new AlignedPointRepeatabilityObservation("point.beta", values[index][1])
                });
            var mappingName = $"mapping-{runNumber:000}.json";
            var mappingPath = Path.Combine(directory, mappingName);
            WriteMapping(mappingPath, mapping);
            var mappingInfo = new FileInfo(mappingPath);
            mappings[index] = mapping;
            runs[index] = new AlignedPointRepeatabilityStudyRunDocument(
                runId,
                sourceId,
                sourceName,
                sourceInfo.Length,
                sourceHash,
                capturedAt.AddMinutes(index),
                mappingName,
                mappingInfo.Length,
                Hash(mappingPath));
        }

        var document = new AlignedPointRepeatabilityStudyDocument(
            AlignedPointRepeatabilityStudyLoader.SupportedStudyType,
            AlignedPointRepeatabilityStudyLoader.SupportedSchemaVersion,
            "study.synthetic-aligned-repeatability",
            "measurement.synthetic-thickness",
            "roi.synthetic-reference",
            "mm",
            "frame.synthetic-aligned-repeatability",
            "alignment.synthetic-fixture",
            "correspondence.synthetic-grid",
            new AlignedPointRepeatabilityAcceptance(3, 3, 0.005, 0.010),
            referencePoints,
            runs);
        var studyPath = Path.Combine(directory, "valid-aligned-study.json");
        WriteStudy(studyPath, document);
        return new StudyFixture(directory, studyPath, document, mappings);
    }

    private static (bool Passed, string Evidence) RejectDocument(
        StudyFixture fixture,
        AlignedPointRepeatabilityStudyDocument document,
        string fileName,
        Type exceptionType,
        string? messageFragment)
    {
        var path = Path.Combine(fixture.Directory, fileName);
        WriteStudy(path, document);
        return Reject(path, exceptionType, messageFragment);
    }

    private static (bool Passed, string Evidence) RejectChangedRun(
        StudyFixture fixture,
        int index,
        Func<AlignedPointRepeatabilityStudyRunDocument, AlignedPointRepeatabilityStudyRunDocument> change,
        string fileName,
        Type exceptionType,
        string? messageFragment)
    {
        var runs = fixture.Document.Runs!.ToArray();
        runs[index] = change(runs[index]);
        return RejectDocument(
            fixture,
            fixture.Document with { Runs = runs },
            fileName,
            exceptionType,
            messageFragment);
    }

    private static (bool Passed, string Evidence) RejectChangedMapping(
        StudyFixture fixture,
        int index,
        Func<AlignedPointRepeatabilityMappingDocument, AlignedPointRepeatabilityMappingDocument> change,
        string name,
        Type exceptionType,
        string? messageFragment)
    {
        var mappingPath = Path.Combine(fixture.Directory, $"{name}.json");
        WriteMapping(mappingPath, change(fixture.MappingDocuments[index]));
        var studyPath = WriteStudyWithMapping(fixture, index, mappingPath, $"{name}-study.json");
        return Reject(studyPath, exceptionType, messageFragment);
    }

    private static string WriteStudyWithMapping(
        StudyFixture fixture,
        int index,
        string mappingPath,
        string studyFileName)
    {
        var mappingInfo = new FileInfo(mappingPath);
        var runs = fixture.Document.Runs!.ToArray();
        runs[index] = runs[index] with
        {
            MappingPath = Path.GetFileName(mappingPath),
            MappingByteLength = mappingInfo.Length,
            MappingSha256 = Hash(mappingPath)
        };
        var studyPath = Path.Combine(fixture.Directory, studyFileName);
        WriteStudy(studyPath, fixture.Document with { Runs = runs });
        return studyPath;
    }

    private static (bool Passed, string Evidence) Reject(
        string path,
        Type exceptionType,
        string? messageFragment)
    {
        try
        {
            AlignedPointRepeatabilityStudyLoader.Load(path);
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

    private static void WriteStudy(
        string path,
        AlignedPointRepeatabilityStudyDocument document,
        Encoding? encoding = null) =>
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(document, JsonOptions),
            encoding ?? new UTF8Encoding(false));

    private static void WriteMapping(
        string path,
        AlignedPointRepeatabilityMappingDocument document,
        Encoding? encoding = null) =>
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(document, JsonOptions),
            encoding ?? new UTF8Encoding(false));

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
        AlignedPointRepeatabilityStudyDocument Document,
        AlignedPointRepeatabilityMappingDocument[] MappingDocuments);

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
