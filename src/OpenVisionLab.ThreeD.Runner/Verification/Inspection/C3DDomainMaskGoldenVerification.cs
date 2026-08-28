using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DDomainMaskGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var source = CreateFixture();
        var sourcePath = Path.Combine(directory, "domain-mask-fixture.c3d");
        source.SaveC3D(sourcePath);
        var recipe = CreateRecipe(source, Path.GetFileName(sourcePath));
        var remove = ToolRecipeRemoveOutlierPixelsExecution.Execute(recipe, "step.remove", directory);
        var connected = remove.Output is { } filtered && remove.OutlierMask is { } mask
            ? ToolRecipeConnectedRegionExecution.Execute(recipe, "step.connected", filtered, mask)
            : null;

        var checks = new List<(string Name, bool Passed, string Evidence)>();
        checks.Add(Check(
            "upstream-connected-region-is-ready",
            remove.Result.Status == ResultStatus.Pass
            && connected?.Result.Status == ResultStatus.Pass
            && connected.Output is { Regions.Count: > 0 },
            $"remove={remove.Result.Status};connected={connected?.Result.Status};regions={connected?.Output?.Regions.Count}"));

        C3DHeightFieldSnapshot? directOutput = null;
        if (connected?.Output is { } domain)
        {
            var filteredSource = remove.Output!;
            var beforeSourceHash = filteredSource.ContentSha256;
            var direct = C3DDomainMaskRule.Evaluate(
                new C3DDomainMaskInput(
                    "step.domain",
                    filteredSource,
                    domain,
                    "domain.height-field"));
            directOutput = direct.Output;
            var foreground = domain.Regions.SelectMany(region => region.Cells).ToHashSet();
            var expectedValues = filteredSource.Values.ToArray();
            for (var row = 0; row < filteredSource.Height; row++)
            {
                for (var column = 0; column < filteredSource.Width; column++)
                {
                    if (!foreground.Contains(new C3DConnectedRegionArtifactCell(row, column)))
                    {
                        expectedValues[row * filteredSource.Width + column] = double.NaN;
                    }
                }
            }

            checks.Add(Check(
                "direct-domain-mask-preserves-source-and-reduces-outside-cells",
                direct.Result.Status == ResultStatus.Pass
                && direct.Output is not null
                && direct.Output.EntityId == "domain.height-field"
                && direct.Output.RootSourceSha256 == source.RootSourceSha256
                && direct.Output.ContentSha256 != source.ContentSha256
                && filteredSource.ContentSha256 == beforeSourceHash
                && SameValues(direct.Output.Values.Span, expectedValues),
                direct.Output is null
                    ? direct.Result.Message
                    : $"status={direct.Result.Status};input={filteredSource.ContentSha256};output={direct.Output.ContentSha256};regions={domain.Regions.Count};cells={foreground.Count};valid={direct.Output.ValidCount};missing={direct.Output.MissingCount}"));

            var artifactPath = Path.Combine(directory, "domain-mask.connected-region.json");
            C3DConnectedRegionArtifactStore.Save(artifactPath, domain);
            var restored = C3DConnectedRegionArtifactStore.Load(artifactPath);
            checks.Add(Check(
                "domain-artifact-round-trip-preserves-identity",
                restored.ContentSha256 == domain.ContentSha256
                && restored.SourceEntityId == domain.SourceEntityId
                && restored.Regions.SelectMany(region => region.Cells)
                    .SequenceEqual(domain.Regions.SelectMany(region => region.Cells)),
                $"saved={File.Exists(artifactPath)};sha256={restored.ContentSha256};cells={restored.Regions.Sum(region => region.Cells.Count)}"));

            var ordered = ToolRecipeOrderedGraphExecution.Execute(recipe, sourcePath);
            var orderedStep = ordered.Steps.SingleOrDefault(step => step.ToolId == "domain-mask");
            checks.Add(Check(
                "ordered-graph-publishes-domain-mask-output",
                ordered.Status == ResultStatus.Pass
                && ordered.Steps.Count == 3
                && orderedStep?.Result.Status == ResultStatus.Pass
                && orderedStep.OutputContentSha256 == direct.Output?.ContentSha256,
                $"overall={ordered.Status};steps={ordered.Steps.Count};domainStatus={orderedStep?.Result.Status};domainOutput={orderedStep?.OutputContentSha256}"));

            var mismatchedSource = C3DHeightFieldSnapshot.CreateForVerification(
                "source.other",
                source.Width,
                source.Height,
                source.Values.ToArray(),
                source.Unit,
                source.FrameId);
            var mismatch = C3DDomainMaskRule.Evaluate(
                new C3DDomainMaskInput("step.domain", mismatchedSource, domain, "domain.mismatch"));
            checks.Add(Check(
                "mismatched-domain-identity-fails-closed",
                mismatch.Result.Status == ResultStatus.Error && mismatch.Output is null,
                $"status={mismatch.Result.Status};message={mismatch.Result.Message}"));

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var canceled = false;
            try
            {
                _ = C3DDomainMaskRule.Evaluate(
                    new C3DDomainMaskInput("step.domain", filteredSource, domain, "domain.canceled"),
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            checks.Add(Check(
                "domain-mask-cancellation-propagates",
                canceled,
                $"canceled={canceled}"));
        }

        var passed = checks.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DDomainMaskGoldenVerification|{(passed == checks.Count ? "PASS" : "FAIL")}|cases={checks.Count}|passed={passed}|failed={checks.Count - passed}",
            $"Fixture|path={sourcePath}|width={source.Width}|height={source.Height}|sourceSha256={source.ContentSha256}",
            $"Direct|output={directOutput?.ContentSha256}|valid={directOutput?.ValidCount}|missing={directOutput?.MissingCount}"
        };
        lines.AddRange(checks.Select(item => $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"C3D Domain / Mask verification: {(passed == checks.Count ? "PASS" : "FAIL")} ({passed}/{checks.Count})");
        return passed == checks.Count ? 0 : 1;
    }

    private static C3DHeightFieldSnapshot CreateFixture()
    {
        const int width = 6;
        const int height = 6;
        var values = Enumerable.Repeat(10d, width * height).ToArray();
        values[2 * width + 2] = 100d;
        values[2 * width + 3] = 100d;
        values[3 * width + 2] = 100d;
        values[0] = double.NaN;
        return C3DHeightFieldSnapshot.CreateForVerification(
            "source.domain-mask",
            width,
            height,
            values,
            "raw-height",
            "frame.c3d-grid-index");
    }

    private static ToolRecipeDocument CreateRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Domain Mask Fixture",
            new ToolRecipeSource(
                source.EntityId,
                "Domain Mask Fixture",
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
                    "step.remove",
                    "remove-outlier-pixels",
                    "Remove Outlier Pixels",
                    1,
                    [source.EntityId],
                    "filtered.height-field",
                    [
                        new("Rule", "LocalMedianAbsoluteDeviation"),
                        new("WindowSize", "3"),
                        new("MaximumAbsoluteDeviation", "20"),
                        new("MinimumValidNeighbors", "3"),
                        new("MissingValuePolicy", "PreserveMask"),
                        new("BoundaryPolicy", "AvailableNeighbors"),
                        new("OutlierPolicy", "SetMissing")
                    ]),
                new ToolRecipeStep(
                    "step.connected",
                    "connected-region",
                    "Connected Region",
                    1,
                    ["filtered.height-field"],
                    "connected.regions",
                    [
                        new("Connectivity", "Four"),
                        new("OriginX", "0"),
                        new("OriginY", "0"),
                        new("ColumnPitch", "1"),
                        new("RowPitch", "1"),
                        new("AreaUnit", "grid-unit^2")
                    ]),
                new ToolRecipeStep(
                    "step.domain",
                    "domain-mask",
                    "Domain / Mask",
                    2,
                    ["filtered.height-field", "connected.regions"],
                    "domain.height-field",
                    [])
            ],
            []);

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
