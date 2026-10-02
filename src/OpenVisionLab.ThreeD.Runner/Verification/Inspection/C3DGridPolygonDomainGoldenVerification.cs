using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DGridPolygonDomainGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var sourcePath = Path.Combine(directory, "grid-polygon-domain-fixture.c3d");
        var recipePath = Path.Combine(directory, "grid-polygon-domain-fixture.ov3d-teach.json");
        var artifactPath = Path.Combine(directory, "grid-polygon-domain.fixture.json");
        var tamperedArtifactPath = Path.Combine(directory, "grid-polygon-domain.tampered.json");
        var source = CreateFixture();
        source.SaveC3D(sourcePath);
        source = C3DHeightFieldSnapshot.LoadIdentified(
            sourcePath,
            source.EntityId,
            source.Unit,
            source.FrameId);
        var recipe = CreateRecipe(source, Path.GetFileName(sourcePath));
        var selection = recipe.Selections!.Single();
        var checks = new List<(string Name, bool Passed, string Evidence)>();

        var validation = ToolRecipeValidator.Validate(recipe);
        checks.Add(Check(
            "recipe-validates-authored-domain-and-consumer",
            validation.IsValid,
            string.Join(" | ", validation.Errors)));

        var directDomain = ToolRecipeGridPolygonDomainExecution.Execute(
            recipe,
            "step.polygon-domain",
            source,
            selection);
        var domain = directDomain.Output;
        checks.Add(Check(
            "direct-producer-publishes-source-bound-domain",
            directDomain.Result.Status == ResultStatus.Pass
            && domain is not null
            && domain.SourceEntityId == source.EntityId
            && domain.SourceContentSha256 == source.ContentSha256
            && domain.RootSourceSha256 == source.RootSourceSha256
            && domain.SelectionId == selection.Id
            && domain.Cells.SequenceEqual([
                new C3DGridDomainArtifactCell(1, 1),
                new C3DGridDomainArtifactCell(1, 2),
                new C3DGridDomainArtifactCell(1, 3),
                new C3DGridDomainArtifactCell(1, 4),
                new C3DGridDomainArtifactCell(2, 1),
                new C3DGridDomainArtifactCell(2, 2),
                new C3DGridDomainArtifactCell(2, 3),
                new C3DGridDomainArtifactCell(2, 4),
                new C3DGridDomainArtifactCell(3, 1),
                new C3DGridDomainArtifactCell(3, 2),
                new C3DGridDomainArtifactCell(3, 3),
                new C3DGridDomainArtifactCell(3, 4),
                new C3DGridDomainArtifactCell(4, 1),
                new C3DGridDomainArtifactCell(4, 2),
                new C3DGridDomainArtifactCell(4, 3),
                new C3DGridDomainArtifactCell(4, 4)
            ]),
            $"status={directDomain.Result.Status};message={directDomain.Result.Message};cells={domain?.Cells.Count}"));

        var derivedSource = source.CreateDerived(
            "derived.grid-polygon-domain",
            source.Values.ToArray(),
            "verification:derived-grid-polygon-domain");
        var derivedSelection = selection with
        {
            SourceBinding = ToolRecipeSelectionSourceBindingVerifier.FromHeightField(derivedSource)
        };
        var derivedRecipe = recipe with
        {
            Steps =
            [
                new ToolRecipeStep(
                    "step.filter.derived-domain",
                    "filter",
                    "Filter",
                    1,
                    [source.EntityId],
                    derivedSource.EntityId,
                    [
                        new ToolRecipeParameter("Method", "Median"),
                        new ToolRecipeParameter("KernelSize", "3"),
                        new ToolRecipeParameter("MissingValuePolicy", "PreserveMask"),
                        new ToolRecipeParameter("BoundaryPolicy", "AvailableNeighbors")
                    ]),
                new ToolRecipeStep(
                    "step.derived-polygon-domain",
                    "grid-polygon-domain",
                    "Grid Polygon Domain",
                    2,
                    [derivedSource.EntityId, derivedSelection.Id],
                    "derived.grid-polygon-domain.output",
                    [])
            ],
            Selections = [derivedSelection]
        };
        var derivedResult = ToolRecipeGridPolygonDomainExecution.Execute(
            derivedRecipe,
            "step.derived-polygon-domain",
            derivedSource,
            derivedSelection);
        checks.Add(Check(
            "derived-heightfield-owner-binding-produces-domain",
            derivedResult.Result.Status == ResultStatus.Pass
            && derivedResult.Output is { } derivedDomain
            && derivedDomain.SourceEntityId == derivedSource.EntityId
            && derivedDomain.RootSourceSha256 == source.RootSourceSha256
            && derivedDomain.SourceContentSha256 == derivedSource.ContentSha256
            && derivedDomain.Cells.SequenceEqual(domain?.Cells ?? []),
            $"status={derivedResult.Result.Status};message={derivedResult.Result.Message};cells={derivedResult.Output?.Cells.Count};source={derivedResult.Output?.SourceEntityId}"));

        C3DHeightFieldSnapshot? directOutput = null;
        if (domain is not null)
        {
            var directMask = ToolRecipeDomainMaskExecution.Execute(
                recipe,
                "step.domain-mask",
                source,
                domain);
            directOutput = directMask.Output;
            var expected = source.Values.ToArray();
            var foreground = domain.Cells.Select(cell => (cell.Row, cell.Column)).ToHashSet();
            for (var row = 0; row < source.Height; row++)
            {
                for (var column = 0; column < source.Width; column++)
                {
                    if (!foreground.Contains((row, column)))
                    {
                        expected[row * source.Width + column] = double.NaN;
                    }
                }
            }

            checks.Add(Check(
                "domain-mask-reuses-sdk-projection-and-preserves-foreground-missing",
                directMask.Result.Status == ResultStatus.Pass
                && directOutput is not null
                && directOutput.RootSourceSha256 == source.RootSourceSha256
                && SameValues(directOutput.Values.Span, expected)
                && double.IsNaN(directOutput.Values.Span[2 * source.Width + 2]),
                directOutput is null
                    ? directMask.Result.Message
                    : $"status={directMask.Result.Status};output={directOutput.ContentSha256};valid={directOutput.ValidCount};missing={directOutput.MissingCount}"));

            C3DGridDomainArtifactStore.Save(artifactPath, domain);
            var restored = C3DGridDomainArtifactStore.Load(artifactPath);
            checks.Add(Check(
                "domain-artifact-round-trip-preserves-content-and-mask-identity",
                restored.ContentSha256 == domain.ContentSha256
                && restored.MaskContentSha256 == domain.MaskContentSha256
                && restored.Cells.SequenceEqual(domain.Cells),
                $"saved={File.Exists(artifactPath)};content={restored.ContentSha256};mask={restored.MaskContentSha256};cells={restored.Cells.Count}"));

            var tamperedJson = File.ReadAllText(artifactPath);
            var lastHashIndex = tamperedJson.LastIndexOf(domain.ContentSha256, StringComparison.Ordinal);
            if (lastHashIndex >= 0)
            {
                tamperedJson = tamperedJson.Remove(lastHashIndex, domain.ContentSha256.Length)
                    .Insert(lastHashIndex, new string('0', 64));
            }
            File.WriteAllText(tamperedArtifactPath, tamperedJson);
            var tamperedRejected = false;
            try
            {
                _ = C3DGridDomainArtifactStore.Load(tamperedArtifactPath);
            }
            catch (InvalidDataException)
            {
                tamperedRejected = true;
            }
            checks.Add(Check(
                "tampered-domain-artifact-fails-closed",
                tamperedRejected,
                $"rejected={tamperedRejected};tamperedFile={File.Exists(tamperedArtifactPath)}"));

            ToolRecipeDocumentStore.Save(recipePath, recipe);
            var reopened = ToolRecipeDocumentStore.Load(recipePath);
            var replayed = ToolRecipeOrderedGraphExecution.Execute(reopened, sourcePath);
            var replayedDomain = replayed.Steps.SingleOrDefault(step => step.ToolId == "grid-polygon-domain");
            var replayedMask = replayed.Steps.SingleOrDefault(step => step.ToolId == "domain-mask");
            checks.Add(Check(
                "save-reopen-and-ordered-replay-regenerate-identical-identities",
                File.Exists(recipePath)
                && reopened.Selections is [var reopenedSelection]
                && reopenedSelection.GridPolygon?.Vertices.SequenceEqual(selection.GridPolygon!.Vertices) == true
                && replayed.Status == ResultStatus.Pass
                && replayedDomain?.OutputContentSha256 == domain.ContentSha256
                && replayedMask?.OutputContentSha256 == directOutput?.ContentSha256,
                $"recipe={File.Exists(recipePath)};reopenedVertices={reopened.Selections?.SingleOrDefault()?.GridPolygon?.Vertices.Count};ordered={replayed.Status};domain={replayedDomain?.OutputContentSha256};mask={replayedMask?.OutputContentSha256}"));
        }

        var staleSelection = selection with
        {
            SourceBinding = selection.SourceBinding with
            {
                ContentSha256 = new string('0', 64)
            }
        };
        var staleResult = ToolRecipeGridPolygonDomainExecution.Execute(
            recipe with { Selections = [staleSelection] },
            "step.polygon-domain",
            source,
            staleSelection);
        checks.Add(Check(
            "stale-selection-source-binding-fails-closed",
            staleResult.Result.Status == ResultStatus.Error && staleResult.Output is null,
            $"status={staleResult.Result.Status};message={staleResult.Result.Message}"));

        var invalidSelection = selection with
        {
            GridPolygon = new ToolRecipeGridPolygon(
            [
                new ToolRecipeGridPolygonVertex(1, 1),
                new ToolRecipeGridPolygonVertex(4, 4),
                new ToolRecipeGridPolygonVertex(1, 4),
                new ToolRecipeGridPolygonVertex(4, 1)
            ])
        };
        var invalidResult = ToolRecipeGridPolygonDomainExecution.Execute(
            recipe with { Selections = [invalidSelection] },
            "step.polygon-domain",
            source,
            invalidSelection);
        checks.Add(Check(
            "invalid-polygon-fails-before-domain-publication",
            invalidResult.Result.Status == ResultStatus.Error && invalidResult.Output is null,
            $"status={invalidResult.Result.Status};message={invalidResult.Result.Message}"));

        var zeroCoverageSelection = selection with
        {
            GridPolygon = new ToolRecipeGridPolygon(
            [
                new ToolRecipeGridPolygonVertex(0.1, 0.1),
                new ToolRecipeGridPolygonVertex(0.1, 0.2),
                new ToolRecipeGridPolygonVertex(0.2, 0.1)
            ])
        };
        var zeroCoverageResult = ToolRecipeGridPolygonDomainExecution.Execute(
            recipe with { Selections = [zeroCoverageSelection] },
            "step.polygon-domain",
            source,
            zeroCoverageSelection);
        checks.Add(Check(
            "zero-coverage-polygon-fails-closed",
            zeroCoverageResult.Result.Status == ResultStatus.Error && zeroCoverageResult.Output is null,
            $"status={zeroCoverageResult.Result.Status};message={zeroCoverageResult.Result.Message}"));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = ToolRecipeGridPolygonDomainExecution.Execute(
                recipe,
                "step.polygon-domain",
                source,
                selection,
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        checks.Add(Check(
            "polygon-domain-cancellation-propagates",
            canceled,
            $"canceled={canceled}"));

        var passed = checks.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DGridPolygonDomainGoldenVerification|{(passed == checks.Count ? "PASS" : "FAIL")}|cases={checks.Count}|passed={passed}|failed={checks.Count - passed}",
            $"Fixture|path={sourcePath}|width={source.Width}|height={source.Height}|sourceSha256={source.ContentSha256}",
            $"Direct|domain={domain?.ContentSha256}|mask={directOutput?.ContentSha256}|cells={domain?.Cells.Count}"
        };
        lines.AddRange(checks.Select(item => $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"C3D GridPolygon Domain verification: {(passed == checks.Count ? "PASS" : "FAIL")} ({passed}/{checks.Count})");
        return passed == checks.Count ? 0 : 1;
    }

    private static C3DHeightFieldSnapshot CreateFixture()
    {
        const int width = 6;
        const int height = 6;
        var values = Enumerable.Repeat(10d, width * height).ToArray();
        values[2 * width + 2] = double.NaN;
        values[0] = double.NaN;
        return C3DHeightFieldSnapshot.CreateForVerification(
            "source.grid-polygon-domain",
            width,
            height,
            values,
            "raw-height",
            "frame.c3d-grid-index");
    }

    private static ToolRecipeDocument CreateRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath)
    {
        var selection = new ToolRecipeSelection(
            "selection.polygon-domain.01",
            "Authored polygon inspection domain",
            ToolRecipeSelectionKinds.GridPolygon,
            source.EntityId,
            source.FrameId,
            new ToolRecipeSelectionSourceBinding(
                "C3D",
                source.ContentSha256,
                source.Width,
                source.Height),
            null,
            null,
            null,
            GridPolygon: new ToolRecipeGridPolygon(
            [
                new ToolRecipeGridPolygonVertex(1, 1),
                new ToolRecipeGridPolygonVertex(1, 4),
                new ToolRecipeGridPolygonVertex(4, 4),
                new ToolRecipeGridPolygonVertex(4, 1)
            ]));
        return new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "GridPolygon Domain Fixture",
            new ToolRecipeSource(
                source.EntityId,
                "GridPolygon Domain Fixture",
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
                    "step.polygon-domain",
                    "grid-polygon-domain",
                    "Grid Polygon Domain",
                    2,
                    [source.EntityId, selection.Id],
                    "grid.polygon-domain",
                    []),
                new ToolRecipeStep(
                    "step.domain-mask",
                    "domain-mask",
                    "Domain / Mask",
                    2,
                    [source.EntityId, "grid.polygon-domain"],
                    "grid.polygon-mask",
                    [])
            ],
            [selection]);
    }

    private static bool SameValues(ReadOnlySpan<double> actual, IReadOnlyList<double> expected)
    {
        if (actual.Length != expected.Count)
        {
            return false;
        }

        for (var index = 0; index < actual.Length; index++)
        {
            if (double.IsNaN(actual[index]) || double.IsNaN(expected[index]))
            {
                if (!double.IsNaN(actual[index]) || !double.IsNaN(expected[index]))
                {
                    return false;
                }
            }
            else if (actual[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);
}
