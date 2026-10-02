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

            var rawRegionRecipe = recipe with
            {
                Steps = recipe.Steps
                    .Select(step => string.Equals(step.ToolId, "domain-mask", StringComparison.Ordinal)
                        ? step with
                        {
                            InputEntityIds = [source.EntityId, filteredSource.EntityId, domain.ArtifactId],
                            Parameters = [new ToolRecipeParameter("SelectedRegionIndex", "0")]
                        }
                        : step)
                    .ToArray()
            };
            var rawRegion = ToolRecipeDomainMaskExecution.Execute(
                rawRegionRecipe,
                "step.domain",
                source,
                filteredSource,
                domain,
                selectedRegionIndex: 0);
            var selectedCells = domain.Regions[0].Cells.ToHashSet();
            var rawExpected = source.Values.ToArray();
            for (var row = 0; row < source.Height; row++)
            {
                for (var column = 0; column < source.Width; column++)
                {
                    if (!selectedCells.Contains(new C3DConnectedRegionArtifactCell(row, column)))
                    {
                        rawExpected[row * source.Width + column] = double.NaN;
                    }
                }
            }

            checks.Add(Check(
                "raw-region-route-preserves-selected-raw-values",
                rawRegion.Result.Status == ResultStatus.Pass
                && rawRegion.Output is not null
                && rawRegion.Output.RootSourceSha256 == source.RootSourceSha256
                && rawRegion.Output.EntityId == "domain.height-field"
                && SameValues(rawRegion.Output.Values.Span, rawExpected),
                rawRegion.Output is null
                    ? rawRegion.Result.Message
                    : $"status={rawRegion.Result.Status};raw={source.ContentSha256};detection={filteredSource.ContentSha256};region=0;cells={selectedCells.Count};valid={rawRegion.Output.ValidCount};missing={rawRegion.Output.MissingCount}"));

            var orderedRawRegion = ToolRecipeOrderedGraphExecution.Execute(
                rawRegionRecipe,
                sourcePath);
            var orderedRawRegionStep = orderedRawRegion.Steps.SingleOrDefault(step => step.ToolId == "domain-mask");
            checks.Add(Check(
                "ordered-graph-runs-raw-region-route",
                orderedRawRegion.Status == ResultStatus.Pass
                && orderedRawRegionStep?.Result.Status == ResultStatus.Pass
                && orderedRawRegionStep.OutputContentSha256 == rawRegion.Output?.ContentSha256,
                $"overall={orderedRawRegion.Status};step={orderedRawRegionStep?.Result.Status};output={orderedRawRegionStep?.OutputContentSha256}"));

            var mismatchedDetection = C3DHeightFieldSnapshot.CreateForVerification(
                "detection.other",
                filteredSource.Width,
                filteredSource.Height,
                filteredSource.Values.ToArray(),
                filteredSource.Unit,
                filteredSource.FrameId);
            var mismatchRawRegion = C3DDomainMaskRule.Evaluate(
                new C3DRawRegionDomainMaskInput(
                    "step.domain",
                    source,
                    mismatchedDetection,
                    domain,
                    0,
                    "domain.mismatch-detection"));
            checks.Add(Check(
                "raw-region-mismatched-detection-fails-closed",
                mismatchRawRegion.Result.Status == ResultStatus.Error
                && mismatchRawRegion.Output is null,
                $"status={mismatchRawRegion.Result.Status};message={mismatchRawRegion.Result.Message}"));

            var mismatchedUnit = C3DHeightFieldSnapshot.CreateForVerification(
                "detection.unit-mismatch",
                filteredSource.Width,
                filteredSource.Height,
                filteredSource.Values.ToArray(),
                "other-unit",
                filteredSource.FrameId);
            var unitMismatch = C3DDomainMaskRule.Evaluate(
                new C3DRawRegionDomainMaskInput(
                    "step.domain",
                    source,
                    mismatchedUnit,
                    domain,
                    0,
                    "domain.mismatch-unit"));
            checks.Add(Check(
                "raw-region-unit-mismatch-fails-closed",
                unitMismatch.Result.Status == ResultStatus.Error
                && unitMismatch.Output is null,
                $"status={unitMismatch.Result.Status};message={unitMismatch.Result.Message}"));

            var mismatchedFrame = C3DHeightFieldSnapshot.CreateForVerification(
                "detection.frame-mismatch",
                filteredSource.Width,
                filteredSource.Height,
                filteredSource.Values.ToArray(),
                filteredSource.Unit,
                "frame.other");
            var frameMismatch = C3DDomainMaskRule.Evaluate(
                new C3DRawRegionDomainMaskInput(
                    "step.domain",
                    source,
                    mismatchedFrame,
                    domain,
                    0,
                    "domain.mismatch-frame"));
            checks.Add(Check(
                "raw-region-frame-mismatch-fails-closed",
                frameMismatch.Result.Status == ResultStatus.Error
                && frameMismatch.Output is null,
                $"status={frameMismatch.Result.Status};message={frameMismatch.Result.Message}"));

            var mismatchedHashDomain = domain with
            {
                SourceContentSha256 = new string('0', 64)
            };
            mismatchedHashDomain = mismatchedHashDomain with
            {
                ContentSha256 = C3DConnectedRegionArtifact.CalculateContentSha256(mismatchedHashDomain)
            };
            var hashMismatch = C3DDomainMaskRule.Evaluate(
                new C3DRawRegionDomainMaskInput(
                    "step.domain",
                    source,
                    filteredSource,
                    mismatchedHashDomain,
                    0,
                    "domain.mismatch-hash"));
            checks.Add(Check(
                "raw-region-content-mismatch-fails-closed",
                hashMismatch.Result.Status == ResultStatus.Error
                && hashMismatch.Output is null,
                $"status={hashMismatch.Result.Status};message={hashMismatch.Result.Message}"));

            var mismatchedGeometryDomain = domain with
            {
                OriginX = domain.OriginX + 1d
            };
            mismatchedGeometryDomain = mismatchedGeometryDomain with
            {
                ContentSha256 = C3DConnectedRegionArtifact.CalculateContentSha256(mismatchedGeometryDomain)
            };
            var geometryMismatch = C3DDomainMaskRule.Evaluate(
                new C3DRawRegionDomainMaskInput(
                    "step.domain",
                    source,
                    filteredSource,
                    mismatchedGeometryDomain,
                    0,
                    "domain.mismatch-geometry"));
            checks.Add(Check(
                "raw-region-geometry-mismatch-fails-closed",
                geometryMismatch.Result.Status == ResultStatus.Error
                && geometryMismatch.Output is null,
                $"status={geometryMismatch.Result.Status};message={geometryMismatch.Result.Message}"));

            var outOfRangeRawRegion = C3DDomainMaskRule.Evaluate(
                new C3DRawRegionDomainMaskInput(
                    "step.domain",
                    source,
                    filteredSource,
                    domain,
                    domain.Regions.Count,
                    "domain.out-of-range"));
            checks.Add(Check(
                "raw-region-index-fails-closed",
                outOfRangeRawRegion.Result.Status == ResultStatus.Error
                && outOfRangeRawRegion.Output is null,
                $"status={outOfRangeRawRegion.Result.Status};message={outOfRangeRawRegion.Result.Message}"));
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
