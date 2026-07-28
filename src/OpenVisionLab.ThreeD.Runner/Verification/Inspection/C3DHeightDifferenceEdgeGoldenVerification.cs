using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DHeightDifferenceEdgeGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("across-columns-rising", VerifyAcrossColumnsRising),
            Check("across-rows-falling", VerifyAcrossRowsFalling),
            Check("absolute-preserves-sign", VerifyAbsolutePreservesSign),
            Check("inclusive-threshold", VerifyInclusiveThreshold),
            Check("strongest-per-scanline", VerifyStrongest),
            Check("tie-lowest-start-index", VerifyTie),
            Check("missing-pairs-skipped", VerifyMissing),
            Check("within-selection-boundary", VerifyBoundary),
            Check("controlled-output-failures", VerifyControlledOutputFailures),
            Check("unknown-preserved-recognized-schema-strict", VerifyStrictParameters),
            Check("root-and-selection-identity", VerifyIdentity),
            Check("deterministic-output-hash", VerifyDeterminism),
            Check("recipe-adapter-contract", VerifyRecipeAdapter)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DHeightDifferenceEdgeGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|numeric=X-column,Y-raw-height,Z-row|candidate=StrongestPerScanline|tie=LowestStartIndex|point=PairMidpoint|missing=SkipPair|boundary=WithinSelection|minimumOutput=2",
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"C3D Height Difference Edge golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyAcrossColumnsRising()
    {
        var evaluation = Evaluate(
            4,
            3,
            [1, 1, 5, 5, 1, 1, 5, 5, 1, 1, 5, 5],
            new ToolRecipeGridRectangle(0, 0, 3, 4),
            C3DHeightDifferenceComparisonAxis.AcrossColumns,
            C3DHeightDifferencePolarity.Rising,
            4);
        var points = evaluation.Output?.Points;
        return (evaluation.Result.Status == ResultStatus.Pass
            && points?.Count == 3
            && points.All(point => point.X == 1.5 && point.Y == 3 && point.Z == point.ScanlineIndex),
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyAcrossRowsFalling()
    {
        var evaluation = Evaluate(
            3,
            4,
            [5, 5, 5, 5, 5, 5, 1, 1, 1, 1, 1, 1],
            new ToolRecipeGridRectangle(0, 0, 4, 3),
            C3DHeightDifferenceComparisonAxis.AcrossRows,
            C3DHeightDifferencePolarity.Falling,
            4);
        var points = evaluation.Output?.Points;
        return (points?.Count == 3
            && points.All(point => point.X == point.ScanlineIndex && point.Y == 3 && point.Z == 1.5 && point.SignedDelta == -4),
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyAbsolutePreservesSign()
    {
        var evaluation = Evaluate(
            3,
            2,
            [5, 1, 1, 1, 5, 5],
            new ToolRecipeGridRectangle(0, 0, 2, 3),
            C3DHeightDifferenceComparisonAxis.AcrossColumns,
            C3DHeightDifferencePolarity.Absolute,
            4);
        return (evaluation.Output?.Points.Select(point => point.SignedDelta).SequenceEqual([-4.0, 4.0]) == true,
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInclusiveThreshold()
    {
        var evaluation = Evaluate(
            2,
            2,
            [1, 5, 2, 6],
            new ToolRecipeGridRectangle(0, 0, 2, 2),
            C3DHeightDifferenceComparisonAxis.AcrossColumns,
            C3DHeightDifferencePolarity.Rising,
            4);
        return (evaluation.Output?.Points.Count == 2, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyStrongest()
    {
        var evaluation = Evaluate(
            4,
            2,
            [1, 4, 1, 10, 1, 4, 1, 10],
            new ToolRecipeGridRectangle(0, 0, 2, 4),
            C3DHeightDifferenceComparisonAxis.AcrossColumns,
            C3DHeightDifferencePolarity.Absolute,
            2);
        return (evaluation.Output?.Points.All(point => point.FirstColumn == 2 && point.Magnitude == 9) == true,
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyTie()
    {
        var evaluation = Evaluate(
            3,
            2,
            [1, 5, 1, 1, 5, 1],
            new ToolRecipeGridRectangle(0, 0, 2, 3),
            C3DHeightDifferenceComparisonAxis.AcrossColumns,
            C3DHeightDifferencePolarity.Absolute,
            4);
        return (evaluation.Output?.Points.All(point => point.FirstColumn == 0 && point.SignedDelta == 4) == true,
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyMissing()
    {
        var evaluation = Evaluate(
            4,
            2,
            [1, double.NaN, 6, 10, 1, double.NaN, 6, 10],
            new ToolRecipeGridRectangle(0, 0, 2, 4),
            C3DHeightDifferenceComparisonAxis.AcrossColumns,
            C3DHeightDifferencePolarity.Rising,
            4);
        var diagnostics = evaluation.Output?.Diagnostics;
        return (evaluation.Output?.Points.All(point => point.FirstColumn == 2) == true
            && diagnostics?.SkippedMissingPairCount == 4
            && diagnostics.EligiblePairCount == 2,
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyBoundary()
    {
        var evaluation = Evaluate(
            5,
            2,
            [1, 100, 101, 105, 200, 1, 100, 101, 105, 200],
            new ToolRecipeGridRectangle(0, 2, 2, 2),
            C3DHeightDifferenceComparisonAxis.AcrossColumns,
            C3DHeightDifferencePolarity.Rising,
            3);
        return (evaluation.Output?.Points.All(point => point.FirstColumn == 2 && point.SignedDelta == 4) == true
            && evaluation.Output.Diagnostics.EligiblePairCount == 2,
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyControlledOutputFailures()
    {
        var narrow = Evaluate(
            2, 2, [1, 5, 1, 5], new ToolRecipeGridRectangle(0, 0, 2, 1),
            C3DHeightDifferenceComparisonAxis.AcrossColumns, C3DHeightDifferencePolarity.Rising, 1);
        var oneScanline = Evaluate(
            2, 1, [1, 5], new ToolRecipeGridRectangle(0, 0, 1, 2),
            C3DHeightDifferenceComparisonAxis.AcrossColumns, C3DHeightDifferencePolarity.Rising, 1);
        var noCandidate = Evaluate(
            2, 2, [1, 2, 1, 2], new ToolRecipeGridRectangle(0, 0, 2, 2),
            C3DHeightDifferenceComparisonAxis.AcrossColumns, C3DHeightDifferencePolarity.Rising, 9);
        var singleOutput = Evaluate(
            2, 2, [1, 5, 1, 2], new ToolRecipeGridRectangle(0, 0, 2, 2),
            C3DHeightDifferenceComparisonAxis.AcrossColumns, C3DHeightDifferencePolarity.Rising, 4);
        var evaluations = new[] { narrow, oneScanline, noCandidate, singleOutput };
        return (evaluations.All(item => item.Result.Status == ResultStatus.Error && item.Output is null),
            string.Join(";", evaluations.Select(item => item.Result.Message)));
    }

    private static (bool Passed, string Evidence) VerifyStrictParameters()
    {
        var fixture = CreateAdapterFixture();
        var extra = fixture.Document with
        {
            Steps = [fixture.Document.Steps[0], fixture.Document.Steps[1] with
            {
                Parameters = [.. fixture.Document.Steps[1].Parameters, new ToolRecipeParameter("Unknown", "1")]
            }]
        };
        var malformed = fixture.Document with
        {
            Steps = [fixture.Document.Steps[0], fixture.Document.Steps[1] with
            {
                Parameters = fixture.Document.Steps[1].Parameters
                    .Select(parameter => parameter.Name == "MinimumDelta" ? parameter with { Value = " 4" } : parameter)
                    .ToArray()
            }]
        };
        var badFixed = fixture.Document with
        {
            Steps = [fixture.Document.Steps[0], fixture.Document.Steps[1] with
            {
                Parameters = fixture.Document.Steps[1].Parameters
                    .Select(parameter => parameter.Name == "PointPolicy" ? parameter with { Value = "FirstSample" } : parameter)
                    .ToArray()
            }]
        };
        var numericEnum = fixture.Document with
        {
            Steps = [fixture.Document.Steps[0], fixture.Document.Steps[1] with
            {
                Parameters = fixture.Document.Steps[1].Parameters
                    .Select(parameter => parameter.Name == "ComparisonAxis" ? parameter with { Value = "0" } : parameter)
                    .ToArray()
            }]
        };
        var duplicate = fixture.Document with
        {
            Steps = [fixture.Document.Steps[0], fixture.Document.Steps[1] with
            {
                Parameters = [.. fixture.Document.Steps[1].Parameters, fixture.Document.Steps[1].Parameters[0]]
            }]
        };
        var evaluations = new[] { extra, malformed, badFixed, numericEnum, duplicate }
            .Select(document => ToolRecipeHeightDifferenceEdgeExecution.Execute(document, document.Steps[1].Id, fixture.Derived))
            .ToArray();
        return (evaluations[0].Result.Status == ResultStatus.Pass
            && evaluations[0].Output is not null
            && evaluations.Skip(1).All(item => item.Result.Status == ResultStatus.Error && item.Output is null),
            string.Join(";", evaluations.Select(item => item.Result.Message)));
    }

    private static (bool Passed, string Evidence) VerifyIdentity()
    {
        var fixture = CreateAdapterFixture();
        var selection = fixture.Document.Selections![0];
        var invalid = fixture.Document with
        {
            Selections = [selection with
            {
                SourceBinding = selection.SourceBinding with { ContentSha256 = new string('A', 64) }
            }]
        };
        var evaluation = ToolRecipeHeightDifferenceEdgeExecution.Execute(invalid, invalid.Steps[1].Id, fixture.Derived);
        return (evaluation.Result.Status == ResultStatus.Error
            && evaluation.Output is null
            && evaluation.Result.Message.Contains("identity", StringComparison.OrdinalIgnoreCase),
            evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var fixture = CreateAdapterFixture();
        var first = ToolRecipeHeightDifferenceEdgeExecution.Execute(fixture.Document, fixture.Document.Steps[1].Id, fixture.Derived);
        var second = ToolRecipeHeightDifferenceEdgeExecution.Execute(fixture.Document, fixture.Document.Steps[1].Id, fixture.Derived);
        return (first.Output?.ContentSha256 == second.Output?.ContentSha256
            && first.Output?.Points.SequenceEqual(second.Output?.Points ?? []) == true
            && first.Output?.Diagnostics == second.Output?.Diagnostics,
            $"first={first.Output?.ContentSha256},second={second.Output?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyRecipeAdapter()
    {
        var fixture = CreateAdapterFixture();
        var evaluation = ToolRecipeHeightDifferenceEdgeExecution.Execute(fixture.Document, fixture.Document.Steps[1].Id, fixture.Derived);
        var output = evaluation.Output;
        var directory = Path.Combine(Path.GetTempPath(), $"ovl3d-edge-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.c3d");
            fixture.Root.SaveC3D(sourcePath);
            var document = fixture.Document with { Source = fixture.Document.Source with { Path = sourcePath } };
            var recipePath = Path.Combine(directory, "edge.ov3d-teach.json");
            var reportPath = Path.Combine(directory, "runner.txt");
            ToolRecipeDocumentStore.Save(recipePath, document);
            var runnerExit = ToolRecipeHeightDifferenceEdgeRunnerExecution.Run(recipePath, document.Steps[1].Id, reportPath);
            var runnerReport = File.ReadAllText(reportPath);
            return (output is not null
                && output.RootSourceSha256 == fixture.Root.ContentSha256
                && output.InputContentSha256 == fixture.Derived.ContentSha256
                && output.SelectionId == fixture.Document.Selections![0].Id
                && output.Points.Count == 4
                && output.ContentSha256.Length == 64
                && runnerExit == 0
                && runnerReport.Contains(output.ContentSha256, StringComparison.Ordinal),
                $"{Evidence(evaluation)},runnerExit={runnerExit},runnerHashMatched={runnerReport.Contains(output?.ContentSha256 ?? "(none)", StringComparison.Ordinal)}");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static C3DHeightDifferenceEdgeEvaluation Evaluate(
        int width,
        int height,
        IReadOnlyList<double> values,
        ToolRecipeGridRectangle rectangle,
        C3DHeightDifferenceComparisonAxis axis,
        C3DHeightDifferencePolarity polarity,
        double minimumDelta)
    {
        var root = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", width, height, values);
        var derived = root.CreateDerived("derived.filtered.01", root.Values.Span.ToArray(), "verification-filter-published");
        return C3DHeightDifferenceEdgeRule.Evaluate(new C3DHeightDifferenceEdgeInput(
            "step.edge.01",
            derived,
            root.EntityId,
            "selection.edge.01",
            rectangle,
            "derived.edgepoints.01",
            axis,
            polarity,
            minimumDelta));
    }

    private static AdapterFixture CreateAdapterFixture()
    {
        var root = C3DHeightFieldSnapshot.CreateForVerification(
            "source.synthetic",
            4,
            4,
            [1, 1, 5, 5, 1, 1, 5, 5, 1, 1, 5, 5, 1, 1, 5, 5]);
        var derived = root.CreateDerived("derived.filtered.01", root.Values.Span.ToArray(), "verification-filter-published");
        var selection = new ToolRecipeSelection(
            "selection.edge.01",
            "Edge search band",
            ToolRecipeSelectionKinds.GridRectangle,
            root.EntityId,
            root.FrameId,
            new ToolRecipeSelectionSourceBinding("C3D", root.ContentSha256, root.Width, root.Height),
            new ToolRecipeGridRectangle(0, 0, 4, 4),
            null,
            null);
        var document = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Edge Golden",
            new ToolRecipeSource(
                root.EntityId,
                "Synthetic",
                "C3D",
                root.Unit,
                root.FrameId,
                "synthetic.c3d",
                root.ByteLength,
                root.ContentSha256,
                root.Width,
                root.Height),
            [],
            [
                new ToolRecipeStep(
                    "step.filter.01",
                    "filter",
                    "Filter",
                    1,
                    [root.EntityId],
                    derived.EntityId,
                    [
                        new("Method", "Median"),
                        new("KernelSize", "3"),
                        new("MissingValuePolicy", "PreserveMask"),
                        new("BoundaryPolicy", "AvailableNeighbors")
                    ]),
                new ToolRecipeStep(
                "step.edge.01",
                "height-difference-edge",
                "Height Difference Edge",
                1,
                [derived.EntityId, selection.Id],
                "derived.edgepoints.01",
                Parameters(4))
            ],
            [selection]);
        return new AdapterFixture(root, derived, document);
    }

    private static ToolRecipeParameter[] Parameters(double minimumDelta) =>
    [
        new("ComparisonAxis", "AcrossColumns"),
        new("Polarity", "Rising"),
        new("MinimumDelta", minimumDelta.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("CandidatePolicy", "StrongestPerScanline"),
        new("PointPolicy", "PairMidpoint"),
        new("MissingValuePolicy", "SkipPair"),
        new("BoundaryPolicy", "WithinSelection")
    ];

    private static VerificationCase Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return new VerificationCase(name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return new VerificationCase(name, false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string Evidence(C3DHeightDifferenceEdgeEvaluation evaluation) =>
        $"status={evaluation.Result.Status},points={evaluation.Output?.Points.Count},eligible={evaluation.Output?.Diagnostics.EligiblePairCount},missing={evaluation.Output?.Diagnostics.SkippedMissingPairCount},hash={evaluation.Output?.ContentSha256},message={evaluation.Result.Message}";

    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record AdapterFixture(
        C3DHeightFieldSnapshot Root,
        C3DHeightFieldSnapshot Derived,
        ToolRecipeDocument Document);

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
