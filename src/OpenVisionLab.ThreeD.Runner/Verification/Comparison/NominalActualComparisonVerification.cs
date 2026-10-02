using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Tools;

internal static class NominalActualComparisonVerification
{
    public static int Run(string reportPath)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"OpenVisionLab.ThreeD.NominalActual.{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var cases = new List<VerificationCase>();

        try
        {
            var actualPath = Path.Combine(tempDirectory, "actual-source.stl");
            var nominalPath = Path.Combine(tempDirectory, "nominal.stl");
            var queryPath = Path.Combine(tempDirectory, "query.ply");
            var sparseQueryPath = Path.Combine(tempDirectory, "sparse-query.ply");
            var denseQueryPath = Path.Combine(tempDirectory, "dense-query.ply");
            var farQueryPath = Path.Combine(tempDirectory, "far-query.ply");
            var alignedQueryPath = Path.Combine(tempDirectory, "aligned-query.ply");
            var emptyQueryPath = Path.Combine(tempDirectory, "empty-query.ply");
            WriteBinaryStl(
                actualPath,
                new BinaryStlTriangle(
                    Vector3.UnitZ,
                    new Vector3(0, 0, 10),
                    new Vector3(2, 0, 10),
                    new Vector3(0, 2, 10),
                    0));
            WriteBinaryStl(
                nominalPath,
                new BinaryStlTriangle(
                    Vector3.UnitZ,
                    Vector3.Zero,
                    new Vector3(2, 0, 0),
                    new Vector3(0, 2, 0),
                    0));
            WriteBinaryPly(
                queryPath,
                [
                    new Vector3(0.25f, 0.25f, 1.0f),
                    new Vector3(0.5f, 0.25f, -2.0f),
                    new Vector3(0.25f, 0.5f, 0.0f),
                    new Vector3(0.5f, 0.5f, 0.5f),
                    new Vector3(1.0f, -1.0f, 1.0f)
                ]);
            WriteBinaryPly(sparseQueryPath, [new Vector3(0.5f, 0.5f, 1.0f)]);
            WriteBinaryPly(
                denseQueryPath,
                [
                    new Vector3(0.25f, 0.25f, 1.0f),
                    new Vector3(0.5f, 0.25f, 1.0f),
                    new Vector3(0.25f, 0.5f, 1.0f),
                    new Vector3(0.75f, 0.25f, 1.0f),
                    new Vector3(0.25f, 0.75f, 1.0f),
                    new Vector3(0.5f, 0.5f, 1.0f)
                ]);
            WriteBinaryPly(
                farQueryPath,
                [
                    new Vector3(100, 100, 100),
                    new Vector3(-100, -100, -100)
                ]);
            WriteBinaryPly(
                alignedQueryPath,
                [new Vector3(2.0f / 3.0f, 2.0f / 3.0f, 10.0f)]);
            WriteBinaryPly(emptyQueryPath, []);

            var input = new NominalActualComparisonInput(
                "step.synthetic-surface-deviation",
                CreateIdentity("source.actual", "Synthetic actual", actualPath),
                CreateIdentity("source.nominal", "Synthetic nominal", nominalPath),
                CreateIdentity("query.actual-vertices", "Synthetic query", queryPath),
                "mm",
                "frame.synthetic-identity",
                "alignment.identity",
                -3.0,
                3.0);
            var recipePath = Path.Combine(tempDirectory, "nominal-actual.recipe.json");
            var recipe = NominalActualComparisonRecipe.FromInput(input, recipePath);
            recipe.Save(recipePath);
            var loadedRecipe = NominalActualComparisonRecipe.Load(recipePath);
            var reopenedInput = loadedRecipe.ToInput(recipePath);
            Check(
                "typed-recipe-roundtrip",
                reopenedInput == input
                && loadedRecipe.Step.Direction == NominalActualComparisonInput.Direction
                && loadedRecipe.Step.EvaluationSampling == NominalActualComparisonRecipe.FullQuerySampling,
                $"type={loadedRecipe.RecipeType},direction={loadedRecipe.Step.Direction},sampling={loadedRecipe.Step.EvaluationSampling}");
            Check(
                "typed-recipe-relative-paths",
                !Path.IsPathRooted(loadedRecipe.Step.ActualSource.Path)
                && !Path.IsPathRooted(loadedRecipe.Step.NominalSource.Path)
                && !Path.IsPathRooted(loadedRecipe.Step.QuerySource.Path),
                $"actual={loadedRecipe.Step.ActualSource.Path},nominal={loadedRecipe.Step.NominalSource.Path},query={loadedRecipe.Step.QuerySource.Path}");
            Check(
                "typed-recipe-invalid-direction-rejected",
                Throws<InvalidDataException>(() =>
                    (recipe with
                    {
                        Step = recipe.Step with { Direction = "NominalToActual" }
                    }).Save(Path.Combine(tempDirectory, "invalid-direction.recipe.json")),
                    "ActualToNominal"),
                "unsupported signed direction rejected");
            Check(
                "typed-recipe-missing-source-rejected",
                Throws<FileNotFoundException>(() =>
                    (recipe with
                    {
                        Step = recipe.Step with
                        {
                            QuerySource = recipe.Step.QuerySource with
                            {
                                Path = "missing-query.ply"
                            }
                        }
                    }).ToInput(recipePath),
                    "source is missing"),
                "missing recipe query source rejected before execution");
            Check(
                "typed-recipe-empty-unit-rejected",
                Throws<InvalidDataException>(() =>
                    (recipe with
                    {
                        Step = recipe.Step with { Unit = " " }
                    }).Save(Path.Combine(tempDirectory, "empty-unit.recipe.json")),
                    "units"),
                "empty recipe unit rejected");
            Check(
                "typed-recipe-empty-frame-rejected",
                Throws<InvalidDataException>(() =>
                    (recipe with
                    {
                        Step = recipe.Step with { FrameId = " " }
                    }).Save(Path.Combine(tempDirectory, "empty-frame.recipe.json")),
                    "frame"),
                "empty recipe frame rejected");
            var executor = new NominalActualComparisonExecutor();
            var progress = new CollectingProgress();
            var passResult = executor.ExecuteAsync(input, 2, progress).GetAwaiter().GetResult();

            Check(
                "pass-status-and-count",
                passResult.Status == ResultStatus.Pass
                && passResult.ComparedPointCount == 5
                && passResult.OutOfToleranceCount == 0,
                $"status={passResult.Status},points={passResult.ComparedPointCount},out={passResult.OutOfToleranceCount}");
            var swappedInput = input with
            {
                ActualSource = input.NominalSource,
                NominalSource = input.ActualSource
            };
            var swappedResult = executor.ExecuteAsync(swappedInput, 0).GetAwaiter().GetResult();
            Check(
                "input-swap-preserves-direction-and-role-identity",
                NominalActualComparisonInput.Direction == "ActualToNominal"
                && swappedInput.SourceFingerprint != input.SourceFingerprint
                && swappedResult.Input.ActualSource.Id == input.NominalSource.Id
                && swappedResult.Input.NominalSource.Id == input.ActualSource.Id
                && swappedResult.Status == ResultStatus.Fail
                && swappedResult.ComparedPointCount == passResult.ComparedPointCount,
                $"direction={NominalActualComparisonInput.Direction},sourceChanged={swappedInput.SourceFingerprint != input.SourceFingerprint},actual={swappedResult.Input.ActualSource.Id},nominal={swappedResult.Input.NominalSource.Id},status={swappedResult.Status},points={swappedResult.ComparedPointCount}");
            var unboundRegisteredInput = input with
            {
                AlignmentId = "alignment.registered-transform.identity.v1"
            };
            Check(
                "unbound-registered-transform-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(unboundRegisteredInput, 0)
                    .GetAwaiter().GetResult(), "requires exact Surface Match artifact references"),
                "a non-identity alignment token without identified artifacts is rejected before deviation");

            var alignedQuery = CreateIdentity(
                "query.aligned",
                "Aligned validation query",
                alignedQueryPath);
            var alignedFixture = CreateAlignmentFixture(
                tempDirectory,
                input.NominalSource,
                alignedQuery);
            var alignedInput = input with
            {
                QuerySource = alignedQuery,
                FrameId = "frame.scene",
                AlignmentId = alignedFixture.Assessment.ContentSha256,
                LowerTolerance = -0.01,
                UpperTolerance = 0.01,
                Alignment = alignedFixture.Reference
            };
            var alignedRecipePath = Path.Combine(tempDirectory, "aligned.nominal-actual.recipe.json");
            var alignedRecipe = NominalActualComparisonRecipe.FromInput(alignedInput, alignedRecipePath);
            alignedRecipe.Save(alignedRecipePath);
            var savedAlignedRecipe = NominalActualComparisonRecipe.Load(alignedRecipePath);
            var reopenedAlignedInput = savedAlignedRecipe.ToInput(alignedRecipePath);
            Check(
                "registered-alignment-save-reopen",
                savedAlignedRecipe.Version == "1.1"
                && reopenedAlignedInput == alignedInput
                && reopenedAlignedInput.SourceFingerprint == alignedInput.SourceFingerprint
                && savedAlignedRecipe.Step.Alignment is { } savedAlignment
                && !Path.IsPathRooted(savedAlignment.Model.Path)
                && !Path.IsPathRooted(savedAlignment.Scene.Path)
                && !Path.IsPathRooted(savedAlignment.Execution.Path)
                && !Path.IsPathRooted(savedAlignment.Assessment.Path),
                $"version={savedAlignedRecipe.Version};fingerprint={reopenedAlignedInput.SourceFingerprint};relative={savedAlignedRecipe.Step.Alignment?.Model.Path}");
            var alignedResult = executor.ExecuteAsync(reopenedAlignedInput, 1).GetAwaiter().GetResult();
            Check(
                "registered-model-to-scene-transform-applied",
                alignedResult.Status == ResultStatus.Pass
                && alignedResult.ComparedPointCount == 1
                && UniformZeroOffset(alignedResult.Signed, 1)
                && alignedResult.AlignmentEvidence is { } accepted
                && accepted.AssessmentContentSha256 == alignedFixture.Assessment.ContentSha256
                && Approximately(accepted.CoverageRatio, 1.0)
                && Approximately(accepted.MinimumCoverageRatio, 1.0)
                && accepted.SourceFrameId == "frame.model"
                && accepted.TargetFrameId == "frame.scene",
                $"status={alignedResult.Status};mean={Format(alignedResult.Signed.Mean)};coverage={Format(alignedResult.AlignmentEvidence?.CoverageRatio ?? double.NaN)};assessment={alignedResult.AlignmentEvidence?.AssessmentContentSha256}");
            var alignedToolResult = NominalActualComparisonContract.CreateToolResult(alignedResult);
            Check(
                "accepted-coverage-policy-is-result-evidence",
                alignedToolResult.Metrics.Single(metric => metric.Name == "Alignment spatial coverage").Value == 1.0
                && alignedToolResult.Metrics.Single(metric => metric.Name == "Minimum alignment spatial coverage").Value == 1.0
                && alignedToolResult.Metrics.Single(metric => metric.Name == "Alignment inlier RMSE").Value == 0.0,
                $"metrics={alignedToolResult.Metrics.Count};coverage={alignedToolResult.Metrics.Single(metric => metric.Name == "Alignment spatial coverage").Value:G17}");

            var alignedRunRecordPath = Path.Combine(tempDirectory, "aligned.run-record.json");
            var alignedReportPath = Path.Combine(tempDirectory, "aligned.runner.txt");
            var alignedRunExitCode = RunnerApplication.RunRecipe(
                alignedRecipePath,
                alignedReportPath,
                ResultStatus.Pass.ToString(),
                null,
                new RunArtifactOptions(alignedRunRecordPath, null, null, null));
            var alignedRunRecord = InspectionRunRecordJson.Read(alignedRunRecordPath);
            var alignedRunnerReport = File.ReadAllText(alignedReportPath);
            Check(
                "run-record-retains-exact-alignment-identities",
                alignedRunExitCode == 0
                && alignedRunRecord.SchemaVersion == InspectionRunRecord.CurrentSchemaVersion
                && alignedRunRecord.NominalActualAlignmentEvidence == alignedResult.AlignmentEvidence
                && alignedRunRecord.NominalActualAlignmentEvidence?.ExecutionContentSha256 == alignedFixture.Execution.ContentSha256
                && alignedRunRecord.NominalActualAlignmentEvidence?.PolicyContentSha256 == alignedFixture.Assessment.Policy.ContentSha256
                && alignedRunnerReport.Contains(
                    $"assessmentSha256={alignedFixture.Assessment.ContentSha256}",
                    StringComparison.Ordinal),
                $"exit={alignedRunExitCode};schema={alignedRunRecord.SchemaVersion};execution={alignedRunRecord.NominalActualAlignmentEvidence?.ExecutionContentSha256};policy={alignedRunRecord.NominalActualAlignmentEvidence?.PolicyContentSha256}");

            var rejectedFixture = CreateNoMatchFixture(tempDirectory, alignedFixture);
            Check(
                "no-match-non-pass-assessment-rejected-before-deviation",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    alignedInput with
                    {
                        AlignmentId = rejectedFixture.Assessment.ContentSha256,
                        Alignment = rejectedFixture.Reference
                    },
                    0).GetAwaiter().GetResult(), "Pass assessment"),
                $"decision={rejectedFixture.Assessment.Decision};state={rejectedFixture.Execution.PoseResult.State}");
            Check(
                "stale-alignment-reference-rejected-before-deviation",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    alignedInput with
                    {
                        Alignment = alignedFixture.Reference with
                        {
                            Execution = alignedFixture.Reference.Execution with
                            {
                                ContentSha256 = new string('0', 64)
                            }
                        }
                    },
                    0).GetAwaiter().GetResult(), "content identity"),
                "recipe execution identity differs from the loaded artifact");
            Check(
                "nominal-model-source-mismatch-rejected-before-deviation",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    alignedInput with
                    {
                        NominalSource = alignedInput.NominalSource with
                        {
                            Id = "source.other-nominal"
                        }
                    },
                    0).GetAwaiter().GetResult(), "model source identity"),
                "persisted Surface Model must identify the exact nominal STL");
            Check(
                "query-scene-source-mismatch-rejected-before-deviation",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    alignedInput with
                    {
                        QuerySource = alignedInput.QuerySource with
                        {
                            Id = "query.other-scene"
                        }
                    },
                    0).GetAwaiter().GetResult(), "Prepared Scene source identity"),
                "persisted Prepared Scene must identify the exact measured query");
            Check(
                "alignment-unit-mismatch-rejected-before-deviation",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    alignedInput with { Unit = "inch" },
                    0).GetAwaiter().GetResult(), "units must agree"),
                "v1 does not perform unit conversion");
            Check(
                "alignment-frame-mismatch-rejected-before-deviation",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    alignedInput with { FrameId = "frame.wrong" },
                    0).GetAwaiter().GetResult(), "frames do not agree"),
                "actual query must remain in the accepted scene frame");
            var scaledFixture = CreateScaledFixture(tempDirectory, alignedFixture);
            Check(
                "non-rigid-scale-rejected-before-deviation",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    alignedInput with
                    {
                        AlignmentId = scaledFixture.Assessment.ContentSha256,
                        Alignment = scaledFixture.Reference
                    },
                    0).GetAwaiter().GetResult(), "must be rigid"),
                "scale-bearing pose is content-addressed but unsupported by the v1 rigid contract");
            var farResult = executor.ExecuteAsync(
                input with
                {
                    QuerySource = CreateIdentity("query.far", "Far query", farQueryPath)
                },
                0).GetAwaiter().GetResult();
            Check(
                "far-query-remains-closest-point-result-without-coverage-policy",
                farResult.Status == ResultStatus.Fail
                && farResult.ComparedPointCount == 2
                && farResult.Signed.Count == 2,
                $"status={farResult.Status},points={farResult.ComparedPointCount},signed={farResult.Signed.Count},spatialCoverage=not-defined");
            Check(
                "signed-statistics",
                Approximately(passResult.Signed.Minimum, -2.0)
                && Approximately(passResult.Signed.Maximum, Math.Sqrt(2.0))
                && passResult.Signed.Count == 5,
                $"min={Format(passResult.Signed.Minimum)},max={Format(passResult.Signed.Maximum)},count={passResult.Signed.Count}");
            Check(
                "robust-sign-recovery",
                passResult.DirectSignResolvedCount == 4
                && passResult.RobustSignRecoveredCount == 1,
                $"direct={passResult.DirectSignResolvedCount},robust={passResult.RobustSignRecoveredCount}");
            Check(
                "display-sampling-is-separate",
                passResult.DisplaySampleStride == 3
                && passResult.DisplaySamples.Count == 2
                && passResult.Signed.Count == 5,
                $"stride={passResult.DisplaySampleStride},display={passResult.DisplaySamples.Count},measured={passResult.Signed.Count}");
            Check(
                "display-sample-provenance",
                passResult.DisplaySamples[0].QueryPointIndex == 0
                && passResult.DisplaySamples[0].NominalTriangleIndex == 0
                && passResult.DisplaySamples[0].Position == new Vector3(0.25f, 0.25f, 1.0f)
                && passResult.DisplaySamples[0].ClosestNominalPoint == new Vector3(0.25f, 0.25f, 0.0f)
                && Approximately(passResult.DisplaySamples[0].UnsignedDeviation, 1.0)
                && Approximately(passResult.DisplaySamples[0].SignedDeviation, 1.0)
                && !passResult.DisplaySamples[0].RobustSignRecovered
                && passResult.DisplaySamples[1].QueryPointIndex == 3,
                $"firstQuery={passResult.DisplaySamples[0].QueryPointIndex},triangle={passResult.DisplaySamples[0].NominalTriangleIndex},unsigned={Format(passResult.DisplaySamples[0].UnsignedDeviation)},secondQuery={passResult.DisplaySamples[1].QueryPointIndex}");
            var detailedDisplayResult = executor.ExecuteAsync(input, 5).GetAwaiter().GetResult();
            Check(
                "display-budget-does-not-change-measurement",
                detailedDisplayResult.DisplaySampleStride == 1
                && detailedDisplayResult.DisplaySamples.Count == 5
                && detailedDisplayResult.Input.ExecutionFingerprint == passResult.Input.ExecutionFingerprint
                && detailedDisplayResult.Status == passResult.Status
                && detailedDisplayResult.ComparedPointCount == passResult.ComparedPointCount
                && detailedDisplayResult.Unsigned == passResult.Unsigned
                && detailedDisplayResult.Signed == passResult.Signed
                && detailedDisplayResult.BelowLowerToleranceCount == passResult.BelowLowerToleranceCount
                && detailedDisplayResult.WithinToleranceCount == passResult.WithinToleranceCount
                && detailedDisplayResult.AboveUpperToleranceCount == passResult.AboveUpperToleranceCount
                && detailedDisplayResult.DirectSignResolvedCount == passResult.DirectSignResolvedCount
                && detailedDisplayResult.RobustSignRecoveredCount == passResult.RobustSignRecoveredCount
                && detailedDisplayResult.DisplaySamples[^1].QueryPointIndex == 4
                && detailedDisplayResult.DisplaySamples[^1].RobustSignRecovered,
                $"fastDisplay={passResult.DisplaySamples.Count},detailedDisplay={detailedDisplayResult.DisplaySamples.Count},points={detailedDisplayResult.ComparedPointCount},fingerprint={detailedDisplayResult.Input.ExecutionFingerprint}");
            var sparseResult = executor.ExecuteAsync(
                input with
                {
                    QuerySource = CreateIdentity("query.sparse", "Sparse query", sparseQueryPath)
                },
                1).GetAwaiter().GetResult();
            var denseResult = executor.ExecuteAsync(
                input with
                {
                    QuerySource = CreateIdentity("query.dense", "Dense query", denseQueryPath)
                },
                2).GetAwaiter().GetResult();
            Check(
                "sparse-dense-full-query-outcomes",
                sparseResult.ComparedPointCount == 1
                && denseResult.ComparedPointCount == 6
                && sparseResult.DirectSignResolvedCount == 1
                && denseResult.DirectSignResolvedCount == 6
                && sparseResult.RobustSignRecoveredCount == 0
                && denseResult.RobustSignRecoveredCount == 0
                && UniformUnitOffset(sparseResult.Signed, 1)
                && UniformUnitOffset(denseResult.Signed, 6)
                && sparseResult.DisplaySampleStride == 1
                && sparseResult.DisplaySamples.Count == 1
                && denseResult.DisplaySampleStride == 3
                && denseResult.DisplaySamples.Count == 2,
                $"sparse={sparseResult.ComparedPointCount}/{sparseResult.DisplaySamples.Count},dense={denseResult.ComparedPointCount}/{denseResult.DisplaySamples.Count},sparseMean={Format(sparseResult.Signed.Mean)},denseMean={Format(denseResult.Signed.Mean)}");
            Check(
                "progress-reaches-total",
                progress.Items.Count >= 3
                && progress.Items[^1].Stage == "Comparing actual to nominal"
                && progress.Items[^1].ProcessedPointCount == 5
                && progress.Items[^1].TotalPointCount == 5,
                progress.Items.Count == 0
                    ? "no progress"
                    : $"events={progress.Items.Count},last={progress.Items[^1].ProcessedPointCount}/{progress.Items[^1].TotalPointCount}");

            var failResult = executor.ExecuteAsync(
                input with { LowerTolerance = -0.5, UpperTolerance = 0.5 },
                0).GetAwaiter().GetResult();
            Check(
                "tolerance-fail",
                failResult.Status == ResultStatus.Fail
                && failResult.BelowLowerToleranceCount == 1
                && failResult.AboveUpperToleranceCount == 2
                && failResult.WithinToleranceCount == 2,
                $"status={failResult.Status},below={failResult.BelowLowerToleranceCount},within={failResult.WithinToleranceCount},above={failResult.AboveUpperToleranceCount}");
            var toolResult = NominalActualComparisonContract.CreateToolResult(failResult);
            var publishedResult = NominalActualComparisonContract.CreateResultEntity(failResult);
            Check(
                "shared-result-contract",
                toolResult.Status == failResult.Status
                && toolResult.Metrics.Single(metric => metric.Name == "Out-of-tolerance point count").Value == 3.0
                && toolResult.Overlays.Single().Kind == OverlayKind.ColorMap,
                $"status={toolResult.Status},metrics={toolResult.Metrics.Count},overlays={toolResult.Overlays.Count}");
            Check(
                "published-result-separates-source",
                publishedResult.Id == NominalActualComparisonContract.ResultEntityId
                && publishedResult.SourceEntityId == input.ActualSource.Id
                && publishedResult.Overlays.Single().SourceEntityId == input.ActualSource.Id,
                $"result={publishedResult.Id},source={publishedResult.SourceEntityId}");
            Check(
                "fingerprints-separate-source-and-parameters",
                input.SourceFingerprint == failResult.Input.SourceFingerprint
                && input.ExecutionFingerprint != failResult.Input.ExecutionFingerprint,
                $"source={input.SourceFingerprint},pass={input.ExecutionFingerprint},fail={failResult.Input.ExecutionFingerprint}");
            Check(
                "pre-cancelled-execution",
                Throws<OperationCanceledException>(() =>
                {
                    using var cancellation = new CancellationTokenSource();
                    cancellation.Cancel();
                    executor.ExecuteAsync(input, 0, cancellationToken: cancellation.Token)
                        .GetAwaiter().GetResult();
                }),
                "pre-cancelled token rejected before result");
            Check(
                "missing-source-rejected",
                Throws<FileNotFoundException>(() => executor.ExecuteAsync(
                    input with
                    {
                        QuerySource = input.QuerySource with
                        {
                            Path = Path.Combine(tempDirectory, "missing-query-direct.ply")
                        }
                    },
                    0).GetAwaiter().GetResult(), "source is missing"),
                "missing direct query source rejected before parsing");
            Check(
                "empty-unit-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    input with { Unit = " " },
                    0).GetAwaiter().GetResult(), "Unit is required"),
                "empty execution unit rejected");
            Check(
                "empty-frame-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    input with { FrameId = " " },
                    0).GetAwaiter().GetResult(), "FrameId is required"),
                "empty execution frame rejected");

            var corruptQueryPath = Path.Combine(tempDirectory, "corrupt-query.ply");
            File.WriteAllText(corruptQueryPath, "not-ply\n", Encoding.ASCII);
            Check(
                "corrupt-query-header-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    input with
                    {
                        QuerySource = CreateIdentity(
                            "query.corrupt",
                            "Corrupt query",
                            corruptQueryPath)
                    },
                    0).GetAwaiter().GetResult(), "PLY magic"),
                "corrupt query header rejected by the binary PLY reader");
            Check(
                "empty-query-no-correspondence-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    input with
                    {
                        QuerySource = CreateIdentity(
                            "query.empty",
                            "Empty query",
                            emptyQueryPath)
                    },
                    0).GetAwaiter().GetResult(), "vertex declaration must be positive"),
                "empty validation query rejected before producing a false result");

            var truncatedQueryPath = Path.Combine(tempDirectory, "truncated-query.ply");
            WriteBinaryPly(truncatedQueryPath, [Vector3.Zero]);
            using (var stream = new FileStream(truncatedQueryPath, FileMode.Open, FileAccess.Write))
            {
                stream.SetLength(stream.Length - 1);
            }

            Check(
                "truncated-query-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    input with
                    {
                        QuerySource = CreateIdentity(
                            "query.truncated",
                            "Truncated query",
                            truncatedQueryPath)
                    },
                    0).GetAwaiter().GetResult(), "length does not match its vertex contract"),
                "truncated query payload rejected after source identity validation");
            Check(
                "hash-mismatch-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    input with
                    {
                        ActualSource = input.ActualSource with
                        {
                            Sha256 = new string('0', 64)
                        }
                    },
                    0).GetAwaiter().GetResult(), "SHA-256"),
                "actual source hash mismatch rejected");
            Check(
                "length-mismatch-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    input with
                    {
                        QuerySource = input.QuerySource with
                        {
                            ByteLength = input.QuerySource.ByteLength + 1
                        }
                    },
                    0).GetAwaiter().GetResult(), "byte length"),
                "query byte length mismatch rejected");
            Check(
                "same-path-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    input with
                    {
                        ActualSource = input.ActualSource with
                        {
                            Path = input.NominalSource.Path,
                            ByteLength = input.NominalSource.ByteLength,
                            Sha256 = input.NominalSource.Sha256
                        }
                    },
                    0).GetAwaiter().GetResult(), "paths must be distinct"),
                "actual and nominal path alias rejected");
            Check(
                "invalid-tolerance-rejected",
                Throws<InvalidDataException>(() => executor.ExecuteAsync(
                    input with { LowerTolerance = 0.0 },
                    0).GetAwaiter().GetResult(), "zero-centred"),
                "non-negative lower tolerance rejected");
        }
        catch (Exception exception)
        {
            cases.Add(new VerificationCase(
                "verification-runtime",
                false,
                $"unexpected {exception.GetType().Name}: {exception.Message}"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }

        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Count ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"NominalActualComparisonVerification|{status}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            "ExecutionContract|owner=OpenVisionLab.ThreeD.Tools|input=Core|parsing=Data|direction=ActualToNominal|sampling=full-query|displaySampling=independent|cancellation=required"
        };
        lines.AddRange(cases.Select(item =>
            $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"Nominal/actual comparison verification: {status} ({passed}/{cases.Count})");
        return status == "Pass" ? 0 : 5;

        void Check(string name, bool condition, string evidence) =>
            cases.Add(new VerificationCase(name, condition, evidence));
    }

    private static NominalActualFileIdentity CreateIdentity(string id, string name, string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        return new NominalActualFileIdentity(
            id,
            name,
            fullPath,
            stream.Length,
            Convert.ToHexString(SHA256.HashData(stream)));
    }

    private static AlignmentFixture CreateAlignmentFixture(
        string directory,
        NominalActualFileIdentity nominal,
        NominalActualFileIdentity query)
    {
        var points = new[]
        {
            new SurfaceModelPoint3(0.0, 0.0, 0.0),
            new SurfaceModelPoint3(2.0, 0.0, 0.0),
            new SurfaceModelPoint3(0.0, 2.0, 0.0)
        };
        var triangle = new SurfaceModelTriangle(0, 1, 2);
        var sample = new SurfaceModelSample(
            0,
            0,
            new SurfaceModelPoint3(2.0 / 3.0, 2.0 / 3.0, 0.0),
            new SurfaceModelPoint3(0.0, 0.0, 1.0));
        var model = SurfaceModelArtifact.Create(
            "model.nominal",
            "Nominal model",
            nominal.Id,
            nominal.Sha256,
            "STL",
            "mm",
            "frame.model",
            new SurfaceModelPreparationParameters(
                SurfaceModelPreparationParameters.DeterministicTriangleCentroidSampling,
                1,
                1e-9,
                1e-6,
                0.9),
            points,
            [triangle],
            points.Select(_ => new SurfaceModelPoint3(0.0, 0.0, 1.0)).ToArray(),
            [sample]);
        var scenePoint = new SurfaceModelPoint3(2.0 / 3.0, 2.0 / 3.0, 10.0);
        var scene = PreparedSceneArtifact.Create(
            "scene.actual",
            "Measured scene",
            PreparedSceneArtifact.CurrentCoordinateConvention,
            CreateQueryQuality(query),
            new PreparedScenePreparationParameters(
                PreparedScenePreparationParameters.DeterministicEvenPointSampling,
                1),
            [scenePoint],
            [new PreparedSceneSample(0, 0, scenePoint)]);
        var pose = new RigidPose3D(
            "mm",
            "frame.model",
            "frame.scene",
            1.0, 0.0, 0.0,
            0.0, 1.0, 0.0,
            0.0, 0.0, 1.0,
            0.0, 0.0, 10.0);
        var parameters = new RigidSurfacePoseSearchParameters(
            0.0, 0.0, 1.0,
            0.0, 0.0, 1.0,
            0.0, 0.0, 1.0,
            0.0, 0.0,
            0.0, 0.0,
            10.0, 10.0,
            0.01,
            1,
            1);
        var coverage = SurfaceCoverageScorer.Evaluate(
            model,
            scene,
            pose,
            parameters.MaximumCorrespondenceDistance);
        var poseResult = RigidSurfacePoseSearchResult.Create(
            model.ContentSha256,
            scene.ContentSha256,
            parameters,
            RigidSurfacePoseSearchState.Matched,
            1,
            pose,
            coverage,
            string.Empty);
        var execution = SurfaceMatchExecutionArtifact.Create(model, scene, poseResult);
        var policy = SurfaceMatchAcceptancePolicy.Create(1.0, 0.01);
        var expected = SurfaceMatchAssessmentArtifactValidator.ExpectedDecision(
            poseResult.State,
            coverage.CoverageRatio,
            coverage.InlierRmse,
            policy);
        var assessment = SurfaceMatchAssessmentArtifact.Create(
            execution,
            policy,
            expected.Decision,
            expected.Reason);
        return SaveAlignmentFixture(directory, "accepted", model, scene, execution, assessment);
    }

    private static AlignmentFixture CreateNoMatchFixture(
        string directory,
        AlignmentFixture accepted)
    {
        var parameters = accepted.Execution.PoseResult.Parameters;
        var coverage = new SurfaceCoverageEvaluation(
            SurfaceCoverageEvaluation.CurrentSemantics,
            accepted.Model.Samples.Length,
            accepted.Scene.Samples.Length,
            0,
            accepted.Model.Samples.Length,
            0.0,
            null,
            parameters.MaximumCorrespondenceDistance,
            [],
            "No overlap in controlled rejection fixture.");
        var poseResult = RigidSurfacePoseSearchResult.Create(
            accepted.Model.ContentSha256,
            accepted.Scene.ContentSha256,
            parameters,
            RigidSurfacePoseSearchState.NoMatch,
            1,
            null,
            coverage,
            "No model samples overlap the measured scene.");
        var execution = SurfaceMatchExecutionArtifact.Create(
            accepted.Model,
            accepted.Scene,
            poseResult);
        var policy = SurfaceMatchAcceptancePolicy.Create(1.0, 0.01);
        var expected = SurfaceMatchAssessmentArtifactValidator.ExpectedDecision(
            poseResult.State,
            coverage.CoverageRatio,
            coverage.InlierRmse,
            policy);
        var assessment = SurfaceMatchAssessmentArtifact.Create(
            execution,
            policy,
            expected.Decision,
            expected.Reason);
        return SaveAlignmentFixture(
            directory,
            "no-match",
            accepted.Model,
            accepted.Scene,
            execution,
            assessment);
    }

    private static AlignmentFixture CreateScaledFixture(
        string directory,
        AlignmentFixture accepted)
    {
        var rigidPose = accepted.Execution.PoseResult.Pose!;
        var scaledPose = rigidPose with { M11 = 2.0 };
        var poseResult = accepted.Execution.PoseResult with
        {
            Pose = scaledPose,
            ContentSha256 = string.Empty
        };
        poseResult = poseResult with
        {
            ContentSha256 = RigidSurfacePoseSearchResult.CalculateContentSha256(poseResult)
        };
        var execution = SurfaceMatchExecutionArtifact.Create(
            accepted.Model,
            accepted.Scene,
            poseResult);
        var assessment = SurfaceMatchAssessmentArtifact.Create(
            execution,
            accepted.Assessment.Policy,
            SurfaceMatchDecision.Pass,
            SurfaceMatchDecisionReason.MeetsAuthoredLimits);
        return SaveAlignmentFixture(
            directory,
            "scaled",
            accepted.Model,
            accepted.Scene,
            execution,
            assessment);
    }

    private static AlignmentFixture SaveAlignmentFixture(
        string directory,
        string name,
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchAssessmentArtifact assessment)
    {
        var modelPath = Path.Combine(directory, $"{name}.surface-model.json");
        var scenePath = Path.Combine(directory, $"{name}.prepared-scene.json");
        var executionPath = Path.Combine(directory, $"{name}.surface-match-execution.json");
        var assessmentPath = Path.Combine(directory, $"{name}.surface-match-assessment.json");
        SurfaceModelArtifactStore.Save(modelPath, model);
        PreparedSceneArtifactStore.Save(scenePath, scene);
        SurfaceMatchExecutionArtifactStore.Save(executionPath, execution);
        SurfaceMatchAssessmentArtifactStore.Save(assessmentPath, assessment);
        return new AlignmentFixture(
            model,
            scene,
            execution,
            assessment,
            new NominalActualAlignmentReference(
                new NominalActualArtifactReference(modelPath, model.ContentSha256),
                new NominalActualArtifactReference(scenePath, scene.ContentSha256),
                new NominalActualArtifactReference(executionPath, execution.ContentSha256),
                new NominalActualArtifactReference(assessmentPath, assessment.ContentSha256)));
    }

    private static SourceQualityReport CreateQueryQuality(NominalActualFileIdentity query)
    {
        var mask = new byte[1];
        return new SourceQualityReport(
            SourceQualityReport.LegacySchemaVersion,
            new SourceQualitySourceIdentity(
                query.Id,
                "PLY",
                query.Path,
                query.ByteLength,
                query.Sha256,
                query.Sha256),
            new SourceQualityGrid(1, 1, 1),
            new SourceQualityCoverage(
                1,
                1,
                0,
                1.0,
                0.0,
                "complete-organized-xyz-grid",
                new SourceQualityInvalidCellMaskIdentity(
                    "synthetic-packed-mask-1.0",
                    "packed-lsb-row-major",
                    mask.Length,
                    Convert.ToHexString(SHA256.HashData(mask)))),
            new SourceQualityHeightStatistics(
                "cartesian-z",
                10.0,
                10.0,
                10.0,
                null),
            new SourceQualityCoordinateContext(
                "mm",
                "frame.scene",
                PreparedSceneArtifact.CurrentCoordinateConvention),
            "controlled-nominal-actual-alignment-fixture",
            false,
            Enum.GetValues<SourceQualityChannel>()
                .Select(channel => new SourceQualityChannelAvailability(
                    channel,
                    channel == SourceQualityChannel.Height
                        ? SourceQualityChannelState.Available
                        : SourceQualityChannelState.Unavailable,
                    channel == SourceQualityChannel.Height
                        ? "Controlled Cartesian Z query is present."
                        : "Controlled fixture does not declare this source channel."))
                .ToArray());
    }

    private static bool Throws<TException>(Action action, string? messageFragment = null)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException exception)
        {
            return messageFragment is null
                || exception.Message.Contains(messageFragment, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void WriteBinaryStl(string path, params BinaryStlTriangle[] triangles)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[80]);
        writer.Write((uint)triangles.Length);
        foreach (var triangle in triangles)
        {
            WriteVector(writer, triangle.StoredNormal);
            WriteVector(writer, triangle.A);
            WriteVector(writer, triangle.B);
            WriteVector(writer, triangle.C);
            writer.Write(triangle.AttributeByteCount);
        }
    }

    private static void WriteBinaryPly(string path, IReadOnlyList<Vector3> points)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes(
            $"ply\nformat binary_little_endian 1.0\nelement vertex {points.Count}\nproperty float x\nproperty float y\nproperty float z\nend_header\n"));
        foreach (var point in points)
        {
            WriteVector(writer, point);
        }
    }

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static bool Approximately(double actual, double expected, double tolerance = 1e-6) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static bool UniformUnitOffset(NominalActualDeviationStatistics statistics, long expectedCount) =>
        statistics.Count == expectedCount
        && Approximately(statistics.Minimum, 1)
        && Approximately(statistics.Maximum, 1)
        && Approximately(statistics.Mean, 1)
        && Approximately(statistics.StandardDeviationPopulation, 0)
        && Approximately(statistics.RootMeanSquare, 1);

    private static bool UniformZeroOffset(NominalActualDeviationStatistics statistics, long expectedCount) =>
        statistics.Count == expectedCount
        && Approximately(statistics.Minimum, 0)
        && Approximately(statistics.Maximum, 0)
        && Approximately(statistics.Mean, 0)
        && Approximately(statistics.StandardDeviationPopulation, 0)
        && Approximately(statistics.RootMeanSquare, 0);

    private static string Format(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed class CollectingProgress : IProgress<NominalActualComparisonProgress>
    {
        public List<NominalActualComparisonProgress> Items { get; } = [];

        public void Report(NominalActualComparisonProgress value) => Items.Add(value);
    }

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);

    private sealed record AlignmentFixture(
        SurfaceModelArtifact Model,
        PreparedSceneArtifact Scene,
        SurfaceMatchExecutionArtifact Execution,
        SurfaceMatchAssessmentArtifact Assessment,
        NominalActualAlignmentReference Reference);
}
