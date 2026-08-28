using System.Globalization;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DLevelSurfaceGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var fixturePath = Path.Combine(directory, "tilted-level-surface-fixture.c3d");
        CreateFixture().SaveC3D(fixturePath);
        var fixture = C3DHeightFieldSnapshot.LoadIdentified(
            fixturePath,
            "source.tilted-level-surface",
            "raw-height",
            "frame.c3d-grid-index");
        var sourceValuesBefore = fixture.Values.ToArray();
        var sourceBytesBefore = File.ReadAllBytes(fixturePath);
        var sourceSha256Before = Convert.ToHexString(SHA256.HashData(sourceBytesBefore));
        var selections = CreateSelections(fixture);
        var direct = Evaluate(fixture, selections, 0.1);
        var repeated = Evaluate(fixture, selections, 0.1);
        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check("known-input-plane",
                direct.Transform is { } transform
                && Math.Abs(transform.FittedSlopeX - 0.8) < 0.01
                && Math.Abs(transform.FittedSlopeZ + 0.4) < 0.01,
                $"slopeX={direct.Transform?.FittedSlopeX:R};slopeZ={direct.Transform?.FittedSlopeZ:R}"),
            Check("leveled-output-plane",
                direct.Result.Status == ResultStatus.Pass
                && Math.Abs(direct.OutputReferenceSlopeX) < 0.00001
                && Math.Abs(direct.OutputReferenceSlopeZ) < 0.00001,
                $"slopeX={direct.OutputReferenceSlopeX:R};slopeZ={direct.OutputReferenceSlopeZ:R}"),
            Check("typed-transform-matrix",
                direct.Transform is { } typed
                && typed.Matrix.M11 == 1
                && typed.Matrix.M22 == 1
                && typed.Matrix.M33 == 1
                && Math.Abs(typed.Matrix.M21 + typed.FittedSlopeX) < 1e-12
                && Math.Abs(typed.Matrix.M23 + typed.FittedSlopeZ) < 1e-12,
                $"transform={direct.Transform?.ContentSha256};matrix={string.Join(',', direct.Transform?.Matrix.Values ?? [])}"),
            Check("reusable-level-frame-basis",
                direct.LevelFrame is { } frame
                && frame.LevelingTransformContentSha256 == direct.Transform?.ContentSha256
                && Math.Abs(Determinant(frame.SourceToFrame) - 1.0) < 1e-9
                && Math.Abs(Length(frame.UAxis) - 1.0) < 1e-9
                && Math.Abs(Length(frame.VAxis) - 1.0) < 1e-9
                && Math.Abs(Length(frame.HAxis) - 1.0) < 1e-9
                && Math.Abs(Dot(frame.UAxis, frame.VAxis)) < 1e-9
                && Math.Abs(Dot(frame.UAxis, frame.HAxis)) < 1e-9
                && Math.Abs(Dot(frame.VAxis, frame.HAxis)) < 1e-9,
                $"frame={direct.LevelFrame?.ContentSha256};transform={direct.LevelFrame?.LevelingTransformContentSha256};determinant={Determinant(direct.LevelFrame?.SourceToFrame ?? default)}"),
            Check("two-explicit-reference-regions",
                direct.Transform?.ReferenceRegions.Count == 2
                && direct.Transform.ReferenceSampleCount == 96,
                $"regions={direct.Transform?.ReferenceRegions.Count};samples={direct.Transform?.ReferenceSampleCount}"),
            Check("quality-evidence-accepted",
                direct.QualityEvidence is
                {
                    State: C3DLevelFrameQualityState.Accepted,
                    Reason: C3DLevelFrameQualityReason.MeetsPolicy,
                    ReferenceCoverage.Count: 2,
                    MinimumObservedCoverageRatio: 1.0
                }
                && direct.QualityEvidence.LevelFrameContentSha256
                    == direct.LevelFrame?.ContentSha256
                && direct.QualityEvidence.LevelingTransformContentSha256
                    == direct.Transform?.ContentSha256,
                $"state={direct.QualityEvidence?.State};reason={direct.QualityEvidence?.Reason};coverage={direct.QualityEvidence?.MinimumObservedCoverageRatio:R};quality={direct.QualityEvidence?.ContentSha256}"),
            Check("quality-evidence-deterministic",
                direct.QualityEvidence?.ContentSha256
                    == repeated.QualityEvidence?.ContentSha256,
                $"quality={direct.QualityEvidence?.ContentSha256};repeat={repeated.QualityEvidence?.ContentSha256}"),
            Check("named-coordinate-frame-chain",
                direct.FrameChain is { } chain
                && chain.Source.Role == C3DCoordinateFrameRole.Source
                && chain.Reference.Role == C3DCoordinateFrameRole.Reference
                && chain.Result?.Role == C3DCoordinateFrameRole.Result
                && chain.Level.Role == C3DCoordinateFrameRole.Level
                && chain.Source.FrameId == fixture.FrameId
                && chain.Reference.FrameId == fixture.FrameId
                && chain.Result.FrameId == fixture.FrameId
                && chain.Level.FrameId == direct.LevelFrame?.LevelFrameId
                && chain.Reference.SelectionIds.SequenceEqual(selections.Select(selection => selection.Id))
                && chain.Links.Count == 3
                && chain.Links.Any(link => link.Relation == C3DLevelSurfaceCoordinateFrameChain.SourceToResultRelation
                    && link.TransformContentSha256 == direct.Transform?.ContentSha256)
                && chain.ContentSha256 == repeated.FrameChain?.ContentSha256,
                $"chain={direct.FrameChain?.ContentSha256};source={direct.FrameChain?.Source.FrameId};reference={direct.FrameChain?.Reference.FrameId};result={direct.FrameChain?.Result?.FrameId};level={direct.FrameChain?.Level.FrameId};links={direct.FrameChain?.Links.Count}"),
            Check("frame-chain-invalid-target-fails-closed",
                RejectsInvalidFrameChain(direct),
                $"rejected={RejectsInvalidFrameChain(direct)}"),
            Check("quality-policy-invalid-input-fails-closed",
                RejectsInvalidQualityPolicy(direct),
                $"rejected={RejectsInvalidQualityPolicy(direct)}"),
            Check("missing-mask-and-grid-preserved",
                direct.Output?.Width == fixture.Width
                && direct.Output?.Height == fixture.Height
                && direct.Output?.ValidCount == fixture.ValidCount
                && direct.Output?.MissingCount == fixture.MissingCount,
                $"grid={direct.Output?.Width}x{direct.Output?.Height};valid={direct.Output?.ValidCount};missing={direct.Output?.MissingCount}"),
            Check("source-immutable",
                fixture.ContentSha256 == sourceSha256Before
                && direct.Output?.RootSourceSha256 == fixture.ContentSha256
                && fixture.ValidCount == 191
                && fixture.MissingCount == 1
                && fixture.Values.Span.SequenceEqual(sourceValuesBefore),
                $"source={fixture.ContentSha256};root={direct.Output?.RootSourceSha256};valuesUnchanged={fixture.Values.Span.SequenceEqual(sourceValuesBefore)}"),
            Check("deterministic-output-and-transform",
                direct.Output?.ContentSha256 == repeated.Output?.ContentSha256
                && direct.Transform?.ContentSha256 == repeated.Transform?.ContentSha256,
                $"output={direct.Output?.ContentSha256};transform={direct.Transform?.ContentSha256}"),
            VerifyResidualGate(fixture, selections)
        };

        var recipePath = Path.Combine(directory, "tilted-level-surface-fixture.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, CreateRecipe(fixture, Path.GetFileName(fixturePath), selections));
        var adapter = ToolRecipeLevelSurfaceExecution.Execute(
            ToolRecipeDocumentStore.Load(recipePath),
            "step.level-surface.01",
            directory);
        var orderedGraph = ToolRecipeOrderedGraphExecution.Execute(
            ToolRecipeDocumentStore.Load(recipePath),
            fixturePath);
        var orderedGraphStep = orderedGraph.Steps.SingleOrDefault();
        var transformOverlayLabel = adapter.Transform is { } adapterTransform
            ? $"Leveling transform {adapterTransform.ContentSha256[..12]}"
            : null;
        var outputPath = Path.Combine(directory, "level-surface-output.c3d");
        adapter.Output?.SaveC3D(outputPath);
        var saved = File.Exists(outputPath)
            ? C3DHeightFieldSnapshot.LoadIdentified(
                outputPath,
                "saved.level-surface",
                fixture.Unit,
                fixture.FrameId)
            : null;
        var sourceBytesAfter = File.ReadAllBytes(fixturePath);
        var sourceSha256After = Convert.ToHexString(SHA256.HashData(sourceBytesAfter));
        var sourceFileUnchanged = sourceBytesBefore.LongLength == sourceBytesAfter.LongLength
            && string.Equals(sourceSha256Before, sourceSha256After, StringComparison.Ordinal)
            && sourceBytesBefore.SequenceEqual(sourceBytesAfter);
        cases.Add(Check("recipe-adapter-parity-and-source-file-immutability",
            adapter.Result.Status == ResultStatus.Pass
            && adapter.Output is { } adapterOutput
            && adapterOutput.ContentSha256 == direct.Output?.ContentSha256
            && adapter.Transform?.ContentSha256 == direct.Transform?.ContentSha256
            && adapter.LevelFrame?.ContentSha256 == direct.LevelFrame?.ContentSha256
            && adapter.QualityEvidence?.ContentSha256 == direct.QualityEvidence?.ContentSha256
            && adapter.FrameChain?.ContentSha256 == direct.FrameChain?.ContentSha256
            && adapterOutput.RootSourceSha256 == sourceSha256Before
            && adapterOutput.ContentSha256.Length == 64
            && adapterOutput.IsDerived
            && !string.Equals(adapterOutput.EntityId, fixture.EntityId, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(fixturePath), StringComparison.OrdinalIgnoreCase)
            && saved?.ContentSha256 == adapterOutput.ContentSha256
            && adapter.Transform is { } provenanceTransform
            && adapterOutput.Provenance.Contains(provenanceTransform.ContentSha256, StringComparison.Ordinal)
            && sourceFileUnchanged,
            $"status={adapter.Result.Status};sourceBefore={sourceSha256Before};sourceAfter={sourceSha256After};bytesBefore={sourceBytesBefore.LongLength};bytesAfter={sourceBytesAfter.LongLength};output={adapter.Output?.ContentSha256};outputEntity={adapter.Output?.EntityId};outputPath={outputPath};isDerived={adapter.Output?.IsDerived};root={adapter.Output?.RootSourceSha256};transform={adapter.Transform?.ContentSha256};quality={adapter.QualityEvidence?.ContentSha256};qualityState={adapter.QualityEvidence?.State}"));
        cases.Add(Check("ordered-graph-parity-and-transform-evidence",
            orderedGraph.Status == ResultStatus.Pass
            && orderedGraph.SourceContentSha256 == sourceSha256Before
            && orderedGraphStep is not null
            && orderedGraphStep.ToolId == "level-surface"
            && orderedGraphStep.OutputContentSha256 == adapter.Output?.ContentSha256
            && orderedGraphStep.LevelFrameContentSha256 == adapter.LevelFrame?.ContentSha256
            && orderedGraphStep.LevelFrameQualityContentSha256 == adapter.QualityEvidence?.ContentSha256
            && orderedGraphStep.FrameChainContentSha256 == adapter.FrameChain?.ContentSha256
            && orderedGraphStep.Result.Overlays.Any(overlay =>
                    string.Equals(overlay.Label, transformOverlayLabel, StringComparison.Ordinal))
            && orderedGraphStep.Result.Overlays.Any(overlay =>
                    string.Equals(overlay.Label, $"Level Frame {adapter.LevelFrame?.ContentSha256[..12]}", StringComparison.Ordinal))
            && orderedGraph.ReboundDocument.Source.Path == Path.GetFullPath(fixturePath)
            && sourceFileUnchanged,
            $"status={orderedGraph.Status};steps={orderedGraph.Steps.Count};source={orderedGraph.SourceContentSha256};output={orderedGraphStep?.OutputContentSha256};transformOverlay={transformOverlayLabel};sourceUnchanged={sourceFileUnchanged}"));

        var partialSourceValues = fixture.Values.ToArray();
        partialSourceValues[0] = double.NaN;
        var partialSource = C3DHeightFieldSnapshot.CreateForVerification(
            "source.partial-level-surface",
            fixture.Width,
            fixture.Height,
            partialSourceValues);
        var partial = Evaluate(partialSource, CreateSelections(partialSource), 0.1);
        cases.Add(Check("quality-evidence-review-for-partial-reference",
            partial.Result.Status == ResultStatus.Pass
            && partial.QualityEvidence is
            {
                State: C3DLevelFrameQualityState.Review,
                Reason: C3DLevelFrameQualityReason.ReferenceCoverageBelowMinimum
            }
            && partial.QualityEvidence.MinimumObservedCoverageRatio < 1.0
            && partial.QualityEvidence.MinimumObservedCoverageRatio > 0.9,
            $"status={partial.Result.Status};state={partial.QualityEvidence?.State};reason={partial.QualityEvidence?.Reason};coverage={partial.QualityEvidence?.MinimumObservedCoverageRatio:R}"));

        var orderedRunnerReportPath = Path.Combine(directory, "ordered-level-surface-runner.txt");
        var orderedRunRecordPath = Path.Combine(directory, "ordered-level-surface-run-record.json");
        var orderedRunRecordHtmlPath = Path.Combine(directory, "ordered-level-surface-run-record.html");
        var orderedRunRecordCsvPath = Path.Combine(directory, "ordered-level-surface-run-record.csv");
        var orderedRunnerExitCode = RunnerApplication.RunToolRecipe(
            recipePath,
            null,
            orderedRunnerReportPath,
            "Pass",
            new RunArtifactOptions(
                orderedRunRecordPath,
                orderedRunRecordHtmlPath,
                orderedRunRecordCsvPath,
                null));
        var orderedRunRecord = File.Exists(orderedRunRecordPath)
            ? InspectionRunRecordJson.Read(orderedRunRecordPath)
            : null;
        var orderedRunStep = orderedRunRecord?.Steps?.SingleOrDefault();
        var sourceBytesAfterRunner = File.ReadAllBytes(fixturePath);
        var sourceSha256AfterRunner = Convert.ToHexString(SHA256.HashData(sourceBytesAfterRunner));
        var sourceFileUnchangedAfterRunner = sourceBytesBefore.LongLength == sourceBytesAfterRunner.LongLength
            && string.Equals(sourceSha256Before, sourceSha256AfterRunner, StringComparison.Ordinal)
            && sourceBytesBefore.SequenceEqual(sourceBytesAfterRunner);
        cases.Add(Check("ordered-runner-run-record-parity-and-source-file-immutability",
            orderedRunnerExitCode == 0
            && orderedRunRecord is not null
            && orderedRunRecord.SchemaVersion == "1.9"
            && orderedRunRecord.Status == ResultStatus.Pass
            && orderedRunRecord.Source.Sha256 == sourceSha256Before
            && orderedRunStep is not null
            && orderedRunStep.ToolId == "level-surface"
            && orderedRunStep.OutputContentSha256 == adapter.Output?.ContentSha256
            && orderedRunStep.LevelFrameQualityContentSha256 == adapter.QualityEvidence?.ContentSha256
            && orderedRunStep.FrameChainContentSha256 == adapter.FrameChain?.ContentSha256
            && orderedRunStep.Overlays.Any(overlay =>
                string.Equals(overlay.Label, transformOverlayLabel, StringComparison.Ordinal))
            && File.Exists(orderedRunnerReportPath)
            && File.Exists(orderedRunRecordHtmlPath)
            && File.Exists(orderedRunRecordCsvPath)
            && sourceFileUnchangedAfterRunner,
            $"exit={orderedRunnerExitCode};schema={orderedRunRecord?.SchemaVersion};status={orderedRunRecord?.Status};source={orderedRunRecord?.Source.Sha256};output={orderedRunStep?.OutputContentSha256};quality={orderedRunStep?.LevelFrameQualityContentSha256};transformOverlay={transformOverlayLabel};sourceAfter={sourceSha256AfterRunner};sourceUnchanged={sourceFileUnchangedAfterRunner}"));

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DLevelSurfaceGoldenVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            $"Contract|fit={C3DLevelingTransform.ReferenceFitPolicy}|level={C3DLevelingTransform.LevelingPolicy}|missing={C3DLevelingTransform.MissingValuePolicy}|grid={C3DLevelingTransform.GridPolicy}|frame={C3DLevelFrameArtifact.FramePolicy}|quality={C3DLevelFrameQualityEvidence.ContractVersion}|coverage={C3DLevelFrameQualityEvidence.CoverageSemantics}|confidence={C3DLevelFrameQualityEvidence.ConfidenceSemantics}|frameChain={C3DLevelSurfaceCoordinateFrameChain.ContractVersion}|frameChainSemantics={C3DLevelSurfaceCoordinateFrameChain.ChainSemantics}|sourceMutation=false",
            $"Fixture|path={fixturePath}|recipe={recipePath}|width={fixture.Width}|height={fixture.Height}|valid={fixture.ValidCount}|missing={fixture.MissingCount}",
            $"SourceIdentity|path={fixturePath}|beforeBytes={sourceBytesBefore.LongLength}|afterBytes={sourceBytesAfter.LongLength}|beforeSha256={sourceSha256Before}|afterSha256={sourceSha256After}|unchanged={sourceFileUnchanged}",
            $"OrderedGraph|status={orderedGraph.Status}|steps={orderedGraph.Steps.Count}|output={orderedGraphStep?.OutputContentSha256}|transformOverlay={transformOverlayLabel}",
            $"OrderedRunner|exit={orderedRunnerExitCode}|report={orderedRunnerReportPath}|record={orderedRunRecordPath}|schema={orderedRunRecord?.SchemaVersion}|status={orderedRunRecord?.Status}|output={orderedRunStep?.OutputContentSha256}|sourceUnchanged={sourceFileUnchangedAfterRunner}",
            $"Input|slopeX={direct.Transform?.FittedSlopeX:R}|slopeZ={direct.Transform?.FittedSlopeZ:R}|referenceRms={direct.Transform?.ReferenceResidualRms:R}",
            $"Output|path={outputPath}|entity={direct.Output?.EntityId}|isDerived={direct.Output?.IsDerived}|sha256={direct.Output?.ContentSha256}|slopeX={direct.OutputReferenceSlopeX:R}|slopeZ={direct.OutputReferenceSlopeZ:R}|rootSourceSha256={direct.Output?.RootSourceSha256}",
            $"Transform|sha256={direct.Transform?.ContentSha256}|entity={direct.Transform?.OutputEntityId}",
            $"LevelFrame|sha256={direct.LevelFrame?.ContentSha256}|entity={direct.LevelFrame?.OutputEntityId}|frameId={direct.LevelFrame?.LevelFrameId}|transform={direct.LevelFrame?.LevelingTransformContentSha256}",
            $"QualityEvidence|sha256={direct.QualityEvidence?.ContentSha256}|state={direct.QualityEvidence?.State}|reason={direct.QualityEvidence?.Reason}|minimumCoverage={direct.QualityEvidence?.MinimumObservedCoverageRatio:R}|frame={direct.QualityEvidence?.LevelFrameContentSha256}|transform={direct.QualityEvidence?.LevelingTransformContentSha256}",
            $"FrameChain|sha256={direct.FrameChain?.ContentSha256}|source={direct.FrameChain?.Source.FrameId}|reference={direct.FrameChain?.Reference.FrameId}|result={direct.FrameChain?.Result?.FrameId}|level={direct.FrameChain?.Level.FrameId}|links={direct.FrameChain?.Links.Count}"
        };
        lines.AddRange(cases.Select(item => $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"Level Surface golden verification: {(passed == cases.Count ? "PASS" : "FAIL")} ({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static C3DHeightFieldSnapshot CreateFixture()
    {
        const int width = 16;
        const int height = 12;
        var values = new double[width * height];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var residual = ((row * 7 + column * 11) % 5 - 2) * 0.01;
                values[row * width + column] = 100 + 0.8 * column - 0.4 * row + residual;
            }
        }
        values[6 * width + 8] = double.NaN;
        return C3DHeightFieldSnapshot.CreateForVerification(
            "source.tilted-level-surface", width, height, values);
    }

    private static IReadOnlyList<ToolRecipeSelection> CreateSelections(C3DHeightFieldSnapshot source) =>
    [
        Selection("selection.level.reference.left", "Left datum", 0, 0, 8, 6, source),
        Selection("selection.level.reference.right", "Right datum", 4, 10, 8, 6, source)
    ];

    private static ToolRecipeSelection Selection(
        string id, string name, int row, int column, int rowCount, int columnCount,
        C3DHeightFieldSnapshot source) =>
        new(
            id, name, ToolRecipeSelectionKinds.GridRectangle, source.EntityId, source.FrameId,
            new ToolRecipeSelectionSourceBinding(
                "C3D", source.RootSourceSha256, source.Width, source.Height),
            new ToolRecipeGridRectangle(row, column, rowCount, columnCount),
            null, null);

    private static C3DLevelSurfaceEvaluation Evaluate(
        C3DHeightFieldSnapshot source,
        IReadOnlyList<ToolRecipeSelection> selections,
        double maximumRms) =>
        C3DLevelSurfaceRule.Evaluate(new C3DLevelSurfaceInput(
            "step.level-surface.01", source, selections,
            "derived.leveled-height.01", 12, maximumRms));

    private static (string Name, bool Passed, string Evidence) VerifyResidualGate(
        C3DHeightFieldSnapshot source,
        IReadOnlyList<ToolRecipeSelection> selections)
    {
        var evaluation = Evaluate(source, selections, 0.0001);
        return Check(
            "reference-rms-gate-fails-closed",
            evaluation.Result.Status == ResultStatus.Fail
            && evaluation.Output is null
            && evaluation.Transform is not null
            && evaluation.LevelFrame is not null
            && evaluation.FrameChain is { Result: null, Links.Count: 2 }
            && evaluation.QualityEvidence is
            {
                State: C3DLevelFrameQualityState.Rejected,
                Reason: C3DLevelFrameQualityReason.ReferenceResidualAboveMaximum
            },
            $"status={evaluation.Result.Status};rms={evaluation.Transform?.ReferenceResidualRms:R};levelFrame={evaluation.LevelFrame?.ContentSha256};quality={evaluation.QualityEvidence?.ContentSha256};frameChain={evaluation.FrameChain?.ContentSha256};qualityState={evaluation.QualityEvidence?.State};reason={evaluation.QualityEvidence?.Reason}");
    }

    private static bool RejectsInvalidQualityPolicy(
        C3DLevelSurfaceEvaluation evaluation)
    {
        if (evaluation.LevelFrame is not { } frame
            || evaluation.Transform is not { } transform)
        {
            return false;
        }

        try
        {
            _ = C3DLevelFrameQualityEvidence.Create(
                frame,
                transform,
                new C3DLevelFrameQualityPolicy(double.NaN, 0.1),
                "invalid-policy");
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    private static bool RejectsInvalidFrameChain(C3DLevelSurfaceEvaluation evaluation)
    {
        if (evaluation.FrameChain is not { } chain)
        {
            return false;
        }

        var invalidLevel = new C3DCoordinateFrameNode(
            C3DCoordinateFrameRole.Level,
            chain.Source.FrameId,
            chain.Level.Unit,
            C3DLevelFrameArtifact.FrameCoordinateConvention,
            chain.Level.EntityId,
            chain.Level.ContentSha256);
        try
        {
            _ = C3DLevelSurfaceCoordinateFrameChain.Create(
                chain.ChainId,
                chain.Source,
                chain.Reference,
                chain.Result,
                invalidLevel,
                chain.Links,
                chain.RootSourceEntityId,
                chain.RootSourceSha256,
                chain.SourceUnit,
                chain.SourceFrameId,
                chain.Provenance);
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    private static ToolRecipeDocument CreateRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        IReadOnlyList<ToolRecipeSelection> selections) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Tilted Level Surface Fixture",
            new ToolRecipeSource(
                source.EntityId, "Tilted Level Surface Fixture", "C3D",
                source.Unit, source.FrameId, sourcePath, source.ByteLength,
                source.ContentSha256, source.Width, source.Height),
            [],
            [
                new ToolRecipeStep(
                    "step.level-surface.01", "level-surface", "Level Surface", 2,
                    [source.EntityId, .. selections.Select(selection => selection.Id)],
                    "derived.leveled-height.01",
                    [
                        new("ReferenceFitPolicy", C3DLevelingTransform.ReferenceFitPolicy),
                        new("LevelingPolicy", C3DLevelingTransform.LevelingPolicy),
                        new("MissingValuePolicy", C3DLevelingTransform.MissingValuePolicy),
                        new("GridPolicy", C3DLevelingTransform.GridPolicy),
                        new("MinimumValidSampleCount", "12"),
                        new("MaximumReferenceRmsResidual", 0.1.ToString("G17", CultureInfo.InvariantCulture))
                    ])
            ],
            selections);

    private static (string Name, bool Passed, string Evidence) Check(
        string name, bool passed, string evidence) =>
        (name, passed, evidence);

    private static double Length(C3DReferenceGridVector value) =>
        Math.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));

    private static double Dot(C3DReferenceGridVector left, C3DReferenceGridVector right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    private static double Determinant(C3DAffineMatrix3x4 matrix) =>
        matrix.M11 * ((matrix.M22 * matrix.M33) - (matrix.M23 * matrix.M32))
        - matrix.M12 * ((matrix.M21 * matrix.M33) - (matrix.M23 * matrix.M31))
        + matrix.M13 * ((matrix.M21 * matrix.M32) - (matrix.M22 * matrix.M31));
}
