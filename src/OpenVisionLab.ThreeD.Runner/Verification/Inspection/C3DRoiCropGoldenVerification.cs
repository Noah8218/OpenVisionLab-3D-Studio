using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DRoiCropGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var fixturePath = Path.Combine(directory, "roi-crop-fixture.c3d");
        CreateFixture().SaveC3D(fixturePath);
        var source = C3DHeightFieldSnapshot.LoadIdentified(
            fixturePath,
            "source.roi-crop-golden",
            "raw-height",
            "frame.c3d-grid-index");
        var sourceValuesBefore = source.Values.ToArray();
        var sourceValidCountBefore = source.ValidCount;
        var sourceMissingCountBefore = source.MissingCount;
        var sourceBytesBefore = File.ReadAllBytes(fixturePath);
        var sourceSha256Before = Convert.ToHexString(SHA256.HashData(sourceBytesBefore));
        var document = CreateRecipe(source, Path.GetFileName(fixturePath));
        var recipePath = Path.Combine(directory, "roi-crop-fixture.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, document);

        var direct = ToolRecipeRoiCropExecution.Execute(document, "step.roi-crop.01", directory);
        var repeated = ToolRecipeRoiCropExecution.Execute(document, "step.roi-crop.01", directory);
        var ordered = ToolRecipeOrderedGraphExecution.Execute(document, fixturePath);
        var outputPath = Path.Combine(directory, "roi-crop-output.c3d");
        direct.Output?.SaveC3D(outputPath);
        var saved = File.Exists(outputPath)
            ? C3DHeightFieldSnapshot.LoadIdentified(
                outputPath,
                "saved.roi-crop",
                "raw-height",
                "frame.c3d-grid-index")
            : null;
        var invalidRegion = VerifyInvalidRegion(source);
        var sourceBytesAfter = File.ReadAllBytes(fixturePath);
        var sourceSha256After = Convert.ToHexString(SHA256.HashData(sourceBytesAfter));
        var sourceFileUnchanged = sourceBytesBefore.LongLength == sourceBytesAfter.LongLength
            && string.Equals(sourceSha256Before, sourceSha256After, StringComparison.Ordinal)
            && sourceBytesBefore.SequenceEqual(sourceBytesAfter);
        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "exact-cropped-values",
                direct.Result.Status == ResultStatus.Pass
                    && direct.Output is
                    {
                        Width: 3,
                        Height: 3,
                        GridOriginColumn: 2,
                        GridOriginRow: 1,
                        ValidCount: 8,
                        MissingCount: 1
                    }
                    && direct.Output.Values.Span.SequenceEqual(
                        new[] { 9d, 10d, 11d, 15d, double.NaN, 17d, 21d, 22d, 23d }),
                $"status={direct.Result.Status};grid={direct.Output?.Width}x{direct.Output?.Height};origin={direct.Output?.GridOriginColumn},{direct.Output?.GridOriginRow}"),
            Check(
                "source-identity-and-evidence",
                source.ContentSha256 == sourceSha256Before
                    && direct.Output is { } cropOutput
                    && cropOutput.RootSourceSha256 == source.ContentSha256
                    && cropOutput.ContentSha256.Length == 64
                    && cropOutput.IsDerived
                    && !string.Equals(cropOutput.EntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(fixturePath), StringComparison.OrdinalIgnoreCase)
                    && source.Values.Span.SequenceEqual(sourceValuesBefore)
                    && source.ValidCount == sourceValidCountBefore
                    && source.MissingCount == sourceMissingCountBefore
                    && sourceFileUnchanged
                    && direct.Result.Metrics.Count == 6
                    && direct.Result.Overlays.Count == 1
                    && direct.SourceRegion == new ToolRecipeGridRectangle(1, 2, 3, 3),
                $"sourceBefore={sourceSha256Before};sourceAfter={sourceSha256After};bytesBefore={sourceBytesBefore.LongLength};bytesAfter={sourceBytesAfter.LongLength};sourceValidBefore={sourceValidCountBefore};sourceValidAfter={source.ValidCount};sourceMissingBefore={sourceMissingCountBefore};sourceMissingAfter={source.MissingCount};output={direct.Output?.ContentSha256};outputEntity={direct.Output?.EntityId};outputPath={outputPath};isDerived={direct.Output?.IsDerived};root={direct.Output?.RootSourceSha256};valuesUnchanged={source.Values.Span.SequenceEqual(sourceValuesBefore)};metrics={direct.Result.Metrics.Count};overlays={direct.Result.Overlays.Count}"),
            Check(
                "deterministic-output",
                direct.Output?.ContentSha256 == repeated.Output?.ContentSha256,
                $"first={direct.Output?.ContentSha256};second={repeated.Output?.ContentSha256}"),
            Check(
                "ordered-runner-parity",
                ordered.Status == ResultStatus.Pass
                    && ordered.Steps.Count == 1
                    && ordered.Steps[0].ToolId == "roi-crop"
                    && ordered.Steps[0].OutputContentSha256 == direct.Output?.ContentSha256,
                $"status={ordered.Status};steps={ordered.Steps.Count};output={ordered.Steps.SingleOrDefault()?.OutputContentSha256}"),
            Check(
                "saved-c3d-byte-parity",
                saved is { Width: 3, Height: 3 }
                    && saved.ContentSha256 == direct.Output?.ContentSha256,
                $"saved={saved?.ContentSha256};direct={direct.Output?.ContentSha256}"),
            invalidRegion
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DRoiCropGoldenVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            "Contract|crop=RectangularSubset|missing=PreserveSelectedCells|frame=KeepSourceFrame|sourceMutation=false",
            $"Fixture|path={fixturePath}|recipe={recipePath}|width={source.Width}|height={source.Height}|sha256={source.ContentSha256}",
            $"SourceIdentity|path={fixturePath}|beforeBytes={sourceBytesBefore.LongLength}|afterBytes={sourceBytesAfter.LongLength}|beforeSha256={sourceSha256Before}|afterSha256={sourceSha256After}|unchanged={sourceFileUnchanged}",
            $"Output|path={outputPath}|entity={direct.Output?.EntityId}|isDerived={direct.Output?.IsDerived}|sha256={direct.Output?.ContentSha256}|rootSourceSha256={direct.Output?.RootSourceSha256}|width={direct.Output?.Width}|height={direct.Output?.Height}|originColumn={direct.Output?.GridOriginColumn}|originRow={direct.Output?.GridOriginRow}|valid={direct.Output?.ValidCount}|missing={direct.Output?.MissingCount}"
        };
        lines.AddRange(cases.Select(item => $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"ROI / Crop golden verification: {(passed == cases.Count ? "PASS" : "FAIL")} ({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static C3DHeightFieldSnapshot CreateFixture()
    {
        var values = Enumerable.Range(1, 30).Select(value => (double)value).ToArray();
        values[2 * 6 + 3] = double.NaN;
        return C3DHeightFieldSnapshot.CreateForVerification("source.roi-crop-golden", 6, 5, values);
    }

    private static ToolRecipeDocument CreateRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        ToolRecipeGridRectangle? region = null)
    {
        var selection = new ToolRecipeSelection(
            "selection.roi-crop.01",
            "Crop region",
            ToolRecipeSelectionKinds.GridRectangle,
            source.EntityId,
            source.FrameId,
            new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height),
            region ?? new ToolRecipeGridRectangle(1, 2, 3, 3),
            null,
            null);
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "ROI Crop Golden",
            new ToolRecipeSource(
                source.EntityId,
                "ROI Crop Fixture",
                "C3D",
                source.Unit,
                source.FrameId,
                sourcePath,
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            [
                new ToolRecipeStep(
                    "step.roi-crop.01",
                    "roi-crop",
                    "ROI / Crop",
                    1,
                    [source.EntityId, selection.Id],
                    "derived.roi-crop.01",
                    [new("ROI", "Select in Viewer"), new("Output frame", "Keep source frame")])
            ],
            [selection]);
    }

    private static (string Name, bool Passed, string Evidence) VerifyInvalidRegion(
        C3DHeightFieldSnapshot source)
    {
        var invalid = CreateRecipe(source, source.SourcePath, new ToolRecipeGridRectangle(4, 5, 2, 2));
        var result = ToolRecipeRoiCropExecution.Execute(invalid, "step.roi-crop.01");
        return Check(
            "invalid-region-fails-closed",
            result.Result.Status == ResultStatus.Error && result.Output is null,
            $"status={result.Result.Status};message={result.Result.Message}");
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) => (name, passed, evidence);
}
