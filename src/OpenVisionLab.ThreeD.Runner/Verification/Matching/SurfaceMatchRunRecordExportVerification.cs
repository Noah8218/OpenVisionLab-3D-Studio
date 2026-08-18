using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

internal static class SurfaceMatchRunRecordExportVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var upstreamReport = Path.Combine(
            directory,
            "surface-edge-review-regression.txt");
        if (SurfaceEdgeDiagnosticReviewVerification.Run(upstreamReport) != 0)
        {
            File.WriteAllText(
                fullReportPath,
                "SurfaceMatchRunRecordExportVerification|FAIL|surface-edge review fixture failed",
                new UTF8Encoding(false));
            return 1;
        }

        var modelPath = Path.Combine(
            directory,
            "edge-score.surface-model.json");
        var scenePath = Path.Combine(
            directory,
            "edge-score-height.prepared-scene.json");
        var executionPath = Path.Combine(
            directory,
            "edge-score.surface-match-execution.json");
        var scorePath = Path.Combine(
            directory,
            "edge-score.height.score.json");
        var assessmentPath = Path.Combine(
            directory,
            "edge-review.accepted.assessment.json");
        var model = SurfaceModelArtifactStore.Load(modelPath);
        var scene = PreparedSceneArtifactStore.Load(scenePath);
        var execution = SurfaceMatchExecutionArtifactStore.Load(
            executionPath);
        var score = SurfaceEdgeArtifactStore.LoadScore(scorePath);
        var assessment =
            SurfaceEdgeDiagnosticReviewArtifactStore.LoadAssessment(
                assessmentPath);
        var runtimePath = Path.Combine(
            directory,
            "edge-score.surface-match-runtime.json");
        var runtime = new SurfaceMatchRuntimeReport(
            SurfaceMatchRuntimeReport.CurrentSchemaVersion,
            SurfaceMatchRuntimeReport.CurrentClock,
            execution.ContentSha256,
            assessment.ContentSha256,
            [
                new SurfaceMatchRuntimeStage(
                    SurfaceMatchRuntimeReport.PoseSearchStage,
                    TimeSpan.FromMilliseconds(12.5).Ticks),
                new SurfaceMatchRuntimeStage(
                    SurfaceMatchRuntimeReport.ExecutionArtifactStage,
                    TimeSpan.FromMilliseconds(1.25).Ticks),
                new SurfaceMatchRuntimeStage(
                    SurfaceMatchRuntimeReport.AcceptanceEvaluationStage,
                    TimeSpan.FromMilliseconds(0.25).Ticks)
            ],
            TimeSpan.FromMilliseconds(14).Ticks,
            DateTimeOffset.UnixEpoch);
        SurfaceMatchAssessmentArtifactStore.SaveRuntime(
            runtimePath,
            runtime);
        var recipePath = Path.Combine(
            directory,
            "surface-match-export.recipe.json");
        var source = scene.SourceQuality.Source;
        ToolRecipeDocumentStore.Save(
            recipePath,
            new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Surface Match export verification",
                new ToolRecipeSource(
                    source.EntityId,
                    "Surface Match scene",
                    source.Format,
                    scene.Unit,
                    scene.FrameId,
                    source.Path,
                    source.ByteLength,
                    source.RootSourceSha256),
                [],
                []));

        var jsonPath = Path.Combine(
            directory,
            "surface-match.run-record.json");
        var htmlPath = Path.Combine(
            directory,
            "surface-match.run-record.html");
        var csvPath = Path.Combine(
            directory,
            "surface-match.run-record.csv");
        var exportReportPath = Path.Combine(
            directory,
            "surface-match-export.txt");
        var sourceArtifactBytes = new[]
        {
            File.ReadAllBytes(modelPath),
            File.ReadAllBytes(scenePath),
            File.ReadAllBytes(executionPath),
            File.ReadAllBytes(scorePath),
            File.ReadAllBytes(assessmentPath),
            File.ReadAllBytes(runtimePath)
        };
        var exitCode = SurfaceMatchRunRecordExportExecution.Run(
            recipePath,
            modelPath,
            scenePath,
            executionPath,
            scorePath,
            assessmentPath,
            runtimePath,
            exportReportPath,
            new RunArtifactOptions(
                jsonPath,
                htmlPath,
                csvPath,
                null));

        var record = File.Exists(jsonPath)
            ? JsonSerializer.Deserialize<InspectionRunRecord>(
                File.ReadAllText(jsonPath),
                JsonOptions)
            : null;
        var html = File.Exists(htmlPath)
            ? File.ReadAllText(htmlPath)
            : string.Empty;
        var csv = File.Exists(csvPath)
            ? File.ReadAllText(csvPath)
            : string.Empty;
        var lines = new List<string>
        {
            "OpenVisionLab 3D Surface Match Run Record export verification",
            "Boundary|Identified artifact projection only; no pose search, scoring, acceptance evaluation, Preview, Publish, Run, Validation, camera, calibration, metrology, or weighted score."
        };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string evidenceText)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add(
                $"{(condition ? "PASS" : "FAIL")} | {name} | {evidenceText}");
        }

        var exported = record?.SurfaceMatchEvidence;
        Check(
            "export-command-succeeds",
            exitCode == 0
            && File.Exists(jsonPath)
            && File.Exists(htmlPath)
            && File.Exists(csvPath),
            $"exit={exitCode};json={File.Exists(jsonPath)};html={File.Exists(htmlPath)};csv={File.Exists(csvPath)}");
        Check(
            "run-record-schema-1.9",
            record?.SchemaVersion == "1.9"
            && exported?.SchemaVersion
                == InspectionRunSurfaceMatchEvidence.CurrentSchemaVersion,
            $"record={record?.SchemaVersion};surface={exported?.SchemaVersion}");
        Check(
            "prepared-scene-source-quality-is-projected-verbatim",
            record?.SourceQualityEvidence is
            {
                State: InspectionRunSourceQualityEvidenceState.Available,
                Report: not null
            } sourceQualityEvidence
            && sourceQualityEvidence.TryValidate(record.Source, out _)
            && sourceQualityEvidence.SourceQualitySha256
                == scene.SourceQualitySha256
            && sourceQualityEvidence.Report.Source == scene.SourceQuality.Source
            && sourceQualityEvidence.Report.Grid == scene.SourceQuality.Grid
            && sourceQualityEvidence.Report.Coverage.ValidSampleCount
                == scene.SourceQuality.Coverage.ValidSampleCount
            && sourceQualityEvidence.Report.Channels.Select(channel =>
                    (channel.Channel, channel.State, channel.Evidence))
                .SequenceEqual(scene.SourceQuality.Channels.Select(channel =>
                    (channel.Channel, channel.State, channel.Evidence))),
            $"state={record?.SourceQualityEvidence?.State};sha256={record?.SourceQualityEvidence?.SourceQualitySha256};scene={scene.SourceQualitySha256}");
        Check(
            "persisted-stage-timing-is-projected-without-reexecution",
            record?.Timing is { State: InspectionRunTimingState.Available } timing
            && timing.TryValidate(out _)
            && timing.Clock == SurfaceMatchRuntimeReport.CurrentClock
            && timing.TotalElapsedMilliseconds == 14d
            && timing.Stages.Select(stage => stage.StageId).SequenceEqual([
                SurfaceMatchRuntimeReport.PoseSearchStage,
                SurfaceMatchRuntimeReport.ExecutionArtifactStage,
                SurfaceMatchRuntimeReport.AcceptanceEvaluationStage
            ])
            && timing.Stages.Select(stage => stage.ElapsedMilliseconds).SequenceEqual([
                12.5d,
                1.25d,
                0.25d
            ]),
            $"state={record?.Timing?.State};totalMs={record?.Timing?.TotalElapsedMilliseconds:G17};stages={string.Join(',', record?.Timing?.Stages.Select(stage => stage.StageId) ?? [])}");
        Check(
            "exact-model-scene-execution-identities",
            exported?.ModelContentSha256 == model.ContentSha256
            && exported.SceneContentSha256 == scene.ContentSha256
            && exported.Execution.ContentSha256
                == execution.ContentSha256,
            $"model={exported?.ModelContentSha256};scene={exported?.SceneContentSha256};execution={exported?.Execution.ContentSha256}");
        Check(
            "source-identity-is-preserved-verbatim",
            record?.Source.EntityId == source.EntityId
            && record.Source.Path == source.Path
            && record.Source.Sha256 == source.RootSourceSha256
            && record.Source.ByteLength == source.ByteLength,
            $"entity={record?.Source.EntityId};path={record?.Source.Path};sha256={record?.Source.Sha256}");
        Check(
            "exact-pose-and-overlay-identities",
            exported?.Execution.PoseResult.ContentSha256
                == execution.PoseResult.ContentSha256
            && exported.Execution.Overlay?.ContentSha256
                == execution.Overlay?.ContentSha256,
            $"pose={exported?.Execution.PoseResult.ContentSha256};overlay={exported?.Execution.Overlay?.ContentSha256}");
        Check(
            "pose-matrix-and-translation-preserved",
            exported?.Execution.PoseResult.Pose
                == execution.PoseResult.Pose,
            $"pose={FormatPose(exported?.Execution.PoseResult.Pose)}");
        Check(
            "surface-and-edge-scores-remain-separate",
            exported?.Score?.ContentSha256 == score.ContentSha256
            && exported.Score.SurfaceScore == score.SurfaceScore
            && exported.Score.EdgeScore.Semantics
                == score.EdgeScore.Semantics
            && exported.Score.EdgeScore.ModelEdgeCount
                == score.EdgeScore.ModelEdgeCount
            && exported.Score.EdgeScore.SceneEdgeCount
                == score.EdgeScore.SceneEdgeCount
            && exported.Score.EdgeScore.MatchedModelEdgeCount
                == score.EdgeScore.MatchedModelEdgeCount
            && exported.Score.EdgeScore.UnmatchedModelEdgeCount
                == score.EdgeScore.UnmatchedModelEdgeCount
            && exported.Score.EdgeScore.CoverageRatio
                == score.EdgeScore.CoverageRatio
            && exported.Score.EdgeScore.InlierRmse
                == score.EdgeScore.InlierRmse
            && exported.Score.EdgeScore.MaximumCorrespondenceDistance
                == score.EdgeScore.MaximumCorrespondenceDistance
            && exported.Score.EdgeScore.Matches.SequenceEqual(
                score.EdgeScore.Matches)
            && exported.Score.EdgeScore.Evidence
                == score.EdgeScore.Evidence
            && exported.Score.Semantics
                == SurfaceAndEdgeMatchScoreArtifact.CurrentSemantics,
            $"surface={exported?.Score?.SurfaceScore.CoverageRatio:G17};edge={exported?.Score?.EdgeScore.CoverageRatio:G17}");
        Check(
            "assessment-remains-separate-from-raw-score",
            exported?.Assessment == assessment
            && exported.Assessment.ScoreContentSha256
                == exported.Score?.ContentSha256
            && exported.Assessment.Semantics
                == SurfaceAndEdgeMatchAssessmentArtifact.CurrentSemantics,
            $"decision={exported?.Assessment?.Decision};reason={exported?.Assessment?.Reason}");

        var requiredValues = new[]
        {
            execution.ContentSha256,
            execution.PoseResult.ContentSha256,
            score.ContentSha256,
            assessment.ContentSha256,
            scene.SourceQualitySha256,
            scene.SourceQuality.Coverage.InvalidCellMask.Sha256,
            scene.SourceQuality.Provenance,
            scene.SourceQuality.Channels[0].Evidence,
            SurfaceMatchRuntimeReport.PoseSearchStage,
            SurfaceMatchRuntimeReport.ExecutionArtifactStage,
            SurfaceMatchRuntimeReport.AcceptanceEvaluationStage,
            runtime.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture),
            score.SurfaceScore.CoverageRatio.ToString("R", CultureInfo.InvariantCulture),
            score.EdgeScore.CoverageRatio.ToString("R", CultureInfo.InvariantCulture),
            execution.PoseResult.Pose!.TranslationX.ToString("R", CultureInfo.InvariantCulture),
            execution.PoseResult.Pose.TranslationY.ToString("R", CultureInfo.InvariantCulture),
            execution.PoseResult.Pose.TranslationZ.ToString("R", CultureInfo.InvariantCulture)
        };
        Check(
            "html-pose-score-assessment-parity",
            requiredValues.All(value => html.Contains(
                value,
                StringComparison.Ordinal)),
            $"values={requiredValues.Length};htmlLength={html.Length}");
        Check(
            "csv-pose-score-assessment-parity",
            requiredValues.All(value => csv.Contains(
                value,
                StringComparison.Ordinal)),
            $"values={requiredValues.Length};csvLines={csv.Split('\n').Length}");
        Check(
            "report-declares-no-recomputation",
            File.ReadAllText(exportReportPath).Contains(
                "matchingRecomputed=false",
                StringComparison.Ordinal),
            exportReportPath);
        Check(
            "projection-has-no-tools-reference",
            typeof(SurfaceMatchRunRecordProjection).Assembly
                .GetReferencedAssemblies()
                .All(reference => reference.Name
                    != "OpenVisionLab.ThreeD.Tools"),
            string.Join(
                ",",
                typeof(SurfaceMatchRunRecordProjection).Assembly
                    .GetReferencedAssemblies()
                    .Select(reference => reference.Name)));
        Check(
            "source-artifacts-remain-byte-identical",
            sourceArtifactBytes[0].SequenceEqual(File.ReadAllBytes(modelPath))
            && sourceArtifactBytes[1].SequenceEqual(File.ReadAllBytes(scenePath))
            && sourceArtifactBytes[2].SequenceEqual(File.ReadAllBytes(executionPath))
            && sourceArtifactBytes[3].SequenceEqual(File.ReadAllBytes(scorePath))
            && sourceArtifactBytes[4].SequenceEqual(File.ReadAllBytes(assessmentPath))
            && sourceArtifactBytes[5].SequenceEqual(File.ReadAllBytes(runtimePath)),
            "model=true;scene=true;execution=true;score=true;assessment=true;runtime=true");

        var flatScore = SurfaceEdgeArtifactStore.LoadScore(
            Path.Combine(directory, "edge-score.flat.score.json"));
        var rejectedAssessment =
            SurfaceEdgeDiagnosticReviewArtifactStore.LoadAssessment(
                Path.Combine(
                    directory,
                    "edge-review.rejected.assessment.json"));
        Check(
            "mismatched-score-fails-closed",
            ThrowsInvalidData(() =>
                SurfaceMatchRunRecordProjection.Create(
                    model,
                    scene,
                    execution,
                    flatScore,
                    assessment)),
            $"execution={execution.ContentSha256};score={flatScore.ContentSha256}");
        Check(
            "mismatched-assessment-fails-closed",
            ThrowsInvalidData(() =>
                SurfaceMatchRunRecordProjection.Create(
                    model,
                    scene,
                    execution,
                    score,
                    rejectedAssessment)),
            $"score={score.ContentSha256};assessment={rejectedAssessment.ContentSha256}");
        Check(
            "tampered-execution-fails-closed",
            ThrowsInvalidData(() =>
                SurfaceMatchRunRecordProjection.Create(
                    model,
                    scene,
                    execution with
                    {
                        ModelContentSha256 = new string('A', 64)
                    },
                    score,
                    assessment)),
            "tamperedModelIdentity=true");
        Check(
            "mismatched-runtime-fails-closed",
            ThrowsInvalidData(() => RunRecordWriter.WriteSurfaceMatch(
                new RunArtifactOptions(
                    Path.Combine(directory, "runtime-mismatch.json"),
                    null,
                    null,
                    null),
                recipePath,
                ToolRecipeDocumentStore.Load(recipePath),
                model,
                scene,
                execution,
                score,
                assessment,
                runtime with { ExecutionContentSha256 = new string('A', 64) },
                exportReportPath)),
            $"execution={execution.ContentSha256};runtimeExecution={new string('A', 64)}");
        Check(
            "mismatched-runtime-assessment-fails-closed",
            ThrowsInvalidData(() => RunRecordWriter.WriteSurfaceMatch(
                new RunArtifactOptions(
                    Path.Combine(directory, "runtime-assessment-mismatch.json"),
                    null,
                    null,
                    null),
                recipePath,
                ToolRecipeDocumentStore.Load(recipePath),
                model,
                scene,
                execution,
                score,
                assessment,
                runtime with { AssessmentContentSha256 = new string('B', 64) },
                exportReportPath)),
            $"assessment={assessment.ContentSha256};runtimeAssessment={new string('B', 64)}");

        var noMatchCoverage = new SurfaceCoverageEvaluation(
            SurfaceCoverageEvaluation.CurrentSemantics,
            model.Samples.Length,
            scene.Samples.Length,
            0,
            model.Samples.Length,
            0.0,
            null,
            execution.PoseResult.Parameters.MaximumCorrespondenceDistance,
            [],
            "No model samples matched the prepared scene.");
        var noMatchPose = RigidSurfacePoseSearchResult.Create(
            model.ContentSha256,
            scene.ContentSha256,
            execution.PoseResult.Parameters,
            RigidSurfacePoseSearchState.NoMatch,
            1,
            null,
            noMatchCoverage,
            "No candidate met the authored search domain.");
        var noMatchExecution = SurfaceMatchExecutionArtifact.Create(
            model,
            scene,
            noMatchPose);
        var noMatchEvidence = SurfaceMatchRunRecordProjection.Create(
            model,
            scene,
            noMatchExecution,
            null,
            null);
        Check(
            "no-match-exports-without-invented-score-or-pose",
            noMatchEvidence.Execution.PoseResult.State
                == RigidSurfacePoseSearchState.NoMatch
            && noMatchEvidence.Execution.PoseResult.Pose is null
            && noMatchEvidence.Execution.Overlay is null
            && noMatchEvidence.Score is null
            && noMatchEvidence.Assessment is null,
            $"state={noMatchEvidence.Execution.PoseResult.State};pose={noMatchEvidence.Execution.PoseResult.Pose is not null};score={noMatchEvidence.Score is not null}");

        var noMatchJsonPath = Path.Combine(
            directory,
            "surface-match.no-match.run-record.json");
        var noMatchHtmlPath = Path.Combine(
            directory,
            "surface-match.no-match.run-record.html");
        var noMatchCsvPath = Path.Combine(
            directory,
            "surface-match.no-match.run-record.csv");
        RunRecordWriter.WriteSurfaceMatch(
            new RunArtifactOptions(
                noMatchJsonPath,
                noMatchHtmlPath,
                noMatchCsvPath,
                null),
            recipePath,
            ToolRecipeDocumentStore.Load(recipePath),
            model,
            scene,
            noMatchExecution,
            null,
            null,
            null,
            exportReportPath);
        var noMatchRecord =
            JsonSerializer.Deserialize<InspectionRunRecord>(
                File.ReadAllText(noMatchJsonPath),
                JsonOptions);
        Check(
            "no-match-json-html-csv-remain-explicit",
            noMatchRecord?.Status == ResultStatus.Fail
            && noMatchRecord.Timing is { State: InspectionRunTimingState.Unavailable }
            && noMatchRecord.Timing.TryValidate(out _)
            && noMatchRecord.SurfaceMatchEvidence?.Execution.PoseResult.State
                == RigidSurfacePoseSearchState.NoMatch
            && noMatchRecord.SurfaceMatchEvidence.Score is null
            && noMatchRecord.SurfaceMatchEvidence.Assessment is null
            && File.ReadAllText(noMatchHtmlPath).Contains(
                "No pose was produced.",
                StringComparison.Ordinal)
            && File.ReadAllText(noMatchCsvPath).Contains(
                "\"state\",\"NoMatch\"",
                StringComparison.Ordinal),
            $"status={noMatchRecord?.Status};state={noMatchRecord?.SurfaceMatchEvidence?.Execution.PoseResult.State}");

        var legacy = new InspectionRunRecord(
            "1.5",
            "legacy-run",
            DateTimeOffset.UnixEpoch,
            new InspectionRunRecipe("tool-recipe", "1.5", recipePath, new string('B', 64)),
            new InspectionRunSource("source", source.Path, source.RootSourceSha256, source.ByteLength, scene.Unit),
            "Legacy",
            ResultStatus.Pass,
            "Legacy Run Record",
            1.0,
            [],
            [],
            "NotCompared",
            new InspectionRunArtifacts(string.Empty, null, null, null, null, null));
        var legacyRoundTrip = JsonSerializer.Deserialize<InspectionRunRecord>(
            JsonSerializer.Serialize(legacy, JsonOptions),
            JsonOptions);
        Check(
            "legacy-run-record-remains-readable",
            legacyRoundTrip?.SchemaVersion == "1.5"
            && legacyRoundTrip.SurfaceMatchEvidence is null
            && legacyRoundTrip.Timing is null,
            $"schema={legacyRoundTrip?.SchemaVersion};surfaceEvidence={legacyRoundTrip?.SurfaceMatchEvidence is not null};timing={legacyRoundTrip?.Timing is not null}");

        lines.Add(
            $"Summary|passed={passed}|total={total}|failed={total - passed}");
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(false));
        return passed == total ? 0 : 1;
    }

    private static bool ThrowsInvalidData(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    private static string FormatPose(RigidPose3D? pose) => pose is null
        ? "(none)"
        : string.Create(
            CultureInfo.InvariantCulture,
            $"[{pose.M11:R},{pose.M12:R},{pose.M13:R};{pose.M21:R},{pose.M22:R},{pose.M23:R};{pose.M31:R},{pose.M32:R},{pose.M33:R}] + [{pose.TranslationX:R},{pose.TranslationY:R},{pose.TranslationZ:R}]");
}
