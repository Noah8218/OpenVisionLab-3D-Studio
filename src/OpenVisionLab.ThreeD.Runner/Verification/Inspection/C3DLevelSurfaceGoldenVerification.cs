using System.Globalization;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
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
            Check("two-explicit-reference-regions",
                direct.Transform?.ReferenceRegions.Count == 2
                && direct.Transform.ReferenceSampleCount == 96,
                $"regions={direct.Transform?.ReferenceRegions.Count};samples={direct.Transform?.ReferenceSampleCount}"),
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
            && adapterOutput.RootSourceSha256 == sourceSha256Before
            && adapterOutput.ContentSha256.Length == 64
            && adapterOutput.IsDerived
            && !string.Equals(adapterOutput.EntityId, fixture.EntityId, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(fixturePath), StringComparison.OrdinalIgnoreCase)
            && saved?.ContentSha256 == adapterOutput.ContentSha256
            && sourceFileUnchanged,
            $"status={adapter.Result.Status};sourceBefore={sourceSha256Before};sourceAfter={sourceSha256After};bytesBefore={sourceBytesBefore.LongLength};bytesAfter={sourceBytesAfter.LongLength};output={adapter.Output?.ContentSha256};outputEntity={adapter.Output?.EntityId};outputPath={outputPath};isDerived={adapter.Output?.IsDerived};root={adapter.Output?.RootSourceSha256};transform={adapter.Transform?.ContentSha256}"));

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DLevelSurfaceGoldenVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            $"Contract|fit={C3DLevelingTransform.ReferenceFitPolicy}|level={C3DLevelingTransform.LevelingPolicy}|missing={C3DLevelingTransform.MissingValuePolicy}|grid={C3DLevelingTransform.GridPolicy}|sourceMutation=false",
            $"Fixture|path={fixturePath}|recipe={recipePath}|width={fixture.Width}|height={fixture.Height}|valid={fixture.ValidCount}|missing={fixture.MissingCount}",
            $"SourceIdentity|path={fixturePath}|beforeBytes={sourceBytesBefore.LongLength}|afterBytes={sourceBytesAfter.LongLength}|beforeSha256={sourceSha256Before}|afterSha256={sourceSha256After}|unchanged={sourceFileUnchanged}",
            $"Input|slopeX={direct.Transform?.FittedSlopeX:R}|slopeZ={direct.Transform?.FittedSlopeZ:R}|referenceRms={direct.Transform?.ReferenceResidualRms:R}",
            $"Output|path={outputPath}|entity={direct.Output?.EntityId}|isDerived={direct.Output?.IsDerived}|sha256={direct.Output?.ContentSha256}|slopeX={direct.OutputReferenceSlopeX:R}|slopeZ={direct.OutputReferenceSlopeZ:R}|rootSourceSha256={direct.Output?.RootSourceSha256}",
            $"Transform|sha256={direct.Transform?.ContentSha256}|entity={direct.Transform?.OutputEntityId}"
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
            && evaluation.Transform is not null,
            $"status={evaluation.Result.Status};rms={evaluation.Transform?.ReferenceResidualRms:R}");
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
}
