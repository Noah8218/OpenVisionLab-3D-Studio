using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DLineIntersectionGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("exact-perpendicular-corner", VerifyExactPerpendicularCorner),
            Check("skew-gap-inclusive-midpoint", VerifySkewGapAndMidpoint),
            Check("angle-and-parallel-rejection", VerifyAngleAndParallelRejection),
            Check("bounded-support-extension", VerifyBoundedSupportExtension),
            Check("lineage-and-geometry-fail-closed", VerifyLineageAndGeometryFailures),
            Check("strict-recipe-adapter", VerifyStrictRecipeAdapter),
            Check("deterministic-hash-and-input-order", VerifyDeterminismAndOrder),
            Check("runner-full-feature-chain", VerifyRunnerFullFeatureChain),
            Check("cancellation-propagates", VerifyCancellation)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DLineIntersectionGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|numeric=X-column,Y-raw-height,Z-row|closest=MidpointOfClosestPoints|parallel=RejectBelowMinimumAcuteAngle|support=InlierProjectionExtents+MaximumExtension"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"3D Line Intersection golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyExactPerpendicularCorner()
    {
        var first = CreateLine("derived.line.first", (0, 0, 0), (1, 0, 0), (-2, 0, 0), (2, 0, 0));
        var second = CreateLine("derived.line.second", (0, 0, 0), (0, 1, 0), (0, -2, 0), (0, 2, 0));
        var evaluation = Evaluate(first, second, maximumGap: 0.001, minimumAngle: 90, maximumExtension: 0);
        var output = evaluation.Output;
        return (evaluation.Result.Status == ResultStatus.Pass && output is not null
            && Approximately(output.CornerAnchorX, 0) && Approximately(output.CornerAnchorY, 0) && Approximately(output.CornerAnchorZ, 0)
            && Approximately(output.ClosestApproachDistance, 0) && Approximately(output.AcuteAngleDegrees, 90), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifySkewGapAndMidpoint()
    {
        var first = CreateLine("derived.line.first", (0, 0, 0), (1, 0, 0), (-2, 0, 0), (2, 0, 0));
        var second = CreateLine("derived.line.second", (0, 0, 0.5), (0, 1, 0), (0, -2, 0.5), (0, 2, 0.5));
        var inclusive = Evaluate(first, second, maximumGap: 0.5, minimumAngle: 45, maximumExtension: 0);
        var rejected = Evaluate(first, second, maximumGap: 0.499999, minimumAngle: 45, maximumExtension: 0);
        var output = inclusive.Output;
        return (inclusive.Result.Status == ResultStatus.Pass && output is not null
            && Approximately(output.ClosestApproachDistance, 0.5) && Approximately(output.CornerAnchorZ, 0.25)
            && rejected.Result.Status == ResultStatus.Error, $"inclusive={Evidence(inclusive)};rejected={Evidence(rejected)}");
    }

    private static (bool Passed, string Evidence) VerifyAngleAndParallelRejection()
    {
        var first = CreateLine("derived.line.first", (0, 0, 0), (1, 0, 0), (-2, 0, 0), (2, 0, 0));
        var shallow = CreateLine("derived.line.second", (0, 0, 0), (Math.Sqrt(3) / 2, 0.5, 0), (-Math.Sqrt(3), -1, 0), (Math.Sqrt(3), 1, 0));
        var parallel = CreateLine("derived.line.parallel", (0, 1, 0), (1, 0, 0), (-2, 1, 0), (2, 1, 0));
        var equality = Evaluate(first, shallow, maximumGap: 0.001, minimumAngle: 30, maximumExtension: 0);
        var below = Evaluate(first, shallow, maximumGap: 0.001, minimumAngle: 30.000001, maximumExtension: 0);
        var parallelRejected = Evaluate(first, parallel, maximumGap: 2, minimumAngle: 1, maximumExtension: 0);
        return (equality.Result.Status == ResultStatus.Pass && below.Result.Status == ResultStatus.Error && parallelRejected.Result.Status == ResultStatus.Error,
            $"equality={Evidence(equality)};below={Evidence(below)};parallel={Evidence(parallelRejected)}");
    }

    private static (bool Passed, string Evidence) VerifyBoundedSupportExtension()
    {
        var first = CreateLine("derived.line.first", (0, 0, 0), (1, 0, 0), (-1, 0, 0), (1, 0, 0));
        var second = CreateLine("derived.line.second", (2, 0, 0), (0, 1, 0), (2, -1, 0), (2, 1, 0));
        var equality = Evaluate(first, second, maximumGap: 0.001, minimumAngle: 45, maximumExtension: 1);
        var rejected = Evaluate(first, second, maximumGap: 0.001, minimumAngle: 45, maximumExtension: 0.999999);
        return (equality.Result.Status == ResultStatus.Pass && equality.Output is not null
            && Approximately(equality.Output.FirstSupportExtension, 1) && rejected.Result.Status == ResultStatus.Error,
            $"equality={Evidence(equality)};rejected={Evidence(rejected)}");
    }

    private static (bool Passed, string Evidence) VerifyLineageAndGeometryFailures()
    {
        var first = CreateLine("derived.line.first", (0, 0, 0), (1, 0, 0), (-2, 0, 0), (2, 0, 0));
        var mismatched = CreateLine("derived.line.second", (0, 0, 0), (0, 1, 0), (0, -2, 0), (0, 2, 0), rootHash: new string('C', 64));
        var lineage = Evaluate(first, mismatched, 0.1, 45, 0);
        var malformed = C3DLineFeature.Create("derived.line.nonfinite", CreateEdge("derived.edge.nonfinite"), 0.1, 3, 1, 2,
            0, 0, 0, double.NaN, 0, 0, -1, 0, 0, 1, 0, 0, Diagnostics(), [], "fixture");
        var geometry = Evaluate(first, malformed, 0.1, 45, 0);
        var duplicate = Evaluate(first, first, 0.1, 45, 0);
        return (lineage.Result.Status == ResultStatus.Error && geometry.Result.Status == ResultStatus.Error && duplicate.Result.Status == ResultStatus.Error,
            $"lineage={Evidence(lineage)};geometry={Evidence(geometry)};duplicate={Evidence(duplicate)}");
    }

    private static (bool Passed, string Evidence) VerifyStrictRecipeAdapter()
    {
        var first = CreateLine("derived.line.first", (0, 0, 0), (1, 0, 0), (-2, 0, 0), (2, 0, 0));
        var second = CreateLine("derived.line.second", (0, 0, 0), (0, 1, 0), (0, -2, 0), (0, 2, 0));
        var document = CreateDocument(first, second, Parameters("UpperLeftCorner"));
        var ready = ToolRecipeLineIntersectionExecution.TryPrepare(document, "step.corner.01", first, second, out _, out var readyMessage);
        var invalid = document with { Steps = [.. document.Steps.Take(2), document.Steps[2] with { Parameters = [.. document.Steps[2].Parameters, new ToolRecipeParameter("Unknown", "x")] }] };
        var rejected = !ToolRecipeLineIntersectionExecution.TryPrepare(invalid, "step.corner.01", first, second, out _, out var rejectedMessage);
        return (ready && rejected && rejectedMessage.Contains("exactly", StringComparison.OrdinalIgnoreCase), $"ready={ready}:{readyMessage};rejected={rejected}:{rejectedMessage}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminismAndOrder()
    {
        var first = CreateLine("derived.line.first", (0, 0, 0), (1, 0, 0), (-2, 0, 0), (2, 0, 0));
        var second = CreateLine("derived.line.second", (0, 0, 0.25), (0, 1, 0), (0, -2, 0.25), (0, 2, 0.25));
        var one = Evaluate(first, second, 0.25, 45, 0);
        var two = Evaluate(first, second, 0.25, 45, 0);
        var swapped = Evaluate(second, first, 0.25, 45, 0);
        return (one.Output?.ContentSha256 == two.Output?.ContentSha256 && one.Output is not null && swapped.Output is not null
            && one.Output.ContentSha256 != swapped.Output.ContentSha256
            && Approximately(one.Output.CornerAnchorX, swapped.Output.CornerAnchorX)
            && Approximately(one.Output.CornerAnchorY, swapped.Output.CornerAnchorY)
            && Approximately(one.Output.CornerAnchorZ, swapped.Output.CornerAnchorZ),
            $"one={Evidence(one)};two={Evidence(two)};swapped={Evidence(swapped)}");
    }

    private static (bool Passed, string Evidence) VerifyCancellation()
    {
        var first = CreateLine("derived.line.first", (0, 0, 0), (1, 0, 0), (-2, 0, 0), (2, 0, 0));
        var second = CreateLine("derived.line.second", (0, 0, 0), (0, 1, 0), (0, -2, 0), (0, 2, 0));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            _ = C3DLineIntersectionRule.Evaluate(new C3DLineIntersectionInput("step.corner.01", first, second, "derived.corner.01", 0.1, 45, 0, "UpperLeftCorner"), cancellation.Token);
            return (false, "no cancellation thrown");
        }
        catch (OperationCanceledException)
        {
            return (true, "OperationCanceledException");
        }
    }

    private static (bool Passed, string Evidence) VerifyRunnerFullFeatureChain()
    {
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "LineIntersectionRunner", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.synthetic",
                10,
                10,
                Enumerable.Range(0, 100)
                    .Select(index => index / 10 < 5 && index % 10 < 5 ? 1d : 10d)
                    .ToArray());
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var firstSelection = new ToolRecipeSelection("selection.line-a", "Line A", ToolRecipeSelectionKinds.GridRectangle, source.EntityId, source.FrameId, new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), new ToolRecipeGridRectangle(1, 0, 3, 10), null, null);
            var secondSelection = new ToolRecipeSelection("selection.line-b", "Line B", ToolRecipeSelectionKinds.GridRectangle, source.EntityId, source.FrameId, new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), new ToolRecipeGridRectangle(0, 1, 10, 3), null, null);
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Line Intersection Runner fixture",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [
                    new ToolRecipeStep("step.filter.01", "filter", "Filter", 1, [source.EntityId], "derived.filtered.01", [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]),
                    CreateEdgeStep("step.edge.a", firstSelection.Id, "AcrossColumns", "derived.edge.a"),
                    CreateLineStep("step.line.a", "derived.edge.a", "derived.line.a"),
                    CreateEdgeStep("step.edge.b", secondSelection.Id, "AcrossRows", "derived.edge.b"),
                    CreateLineStep("step.line.b", "derived.edge.b", "derived.line.b"),
                    new ToolRecipeStep("step.corner.01", "line-intersection", "Line Intersection", 2, ["derived.line.a", "derived.line.b"], "derived.corner.01", [new("MaximumClosestApproachDistance", "0.001"), new("MinimumAcuteAngleDegrees", "45"), new("MaximumSupportExtension", "1.5"), new("OutputRole", "SyntheticCorner"), new("ClosestApproachPolicy", "MidpointOfClosestPoints"), new("ParallelPolicy", "RejectBelowMinimumAcuteAngle"), new("SupportPolicy", "WithinInlierProjectionExtentsWithMaximumExtension")]),
                ],
                [firstSelection, secondSelection]);
            var recipePath = Path.Combine(root, "fixture.ov3d-teach.json");
            var reportPath = Path.Combine(root, "runner.txt");
            ToolRecipeDocumentStore.Save(recipePath, document);
            var runnerExitCode = ToolRecipeLineIntersectionRunnerExecution.Run(recipePath, "step.corner.01", reportPath);
            var filter = ToolRecipeFilterExecution.Execute(document, "step.filter.01", root);
            var edgeA = ToolRecipeHeightDifferenceEdgeExecution.Execute(document, "step.edge.a", filter.Output!);
            var lineA = ToolRecipeLineFitExecution.Execute(document, "step.line.a", edgeA.Output!);
            var edgeB = ToolRecipeHeightDifferenceEdgeExecution.Execute(document, "step.edge.b", filter.Output!);
            var lineB = ToolRecipeLineFitExecution.Execute(document, "step.line.b", edgeB.Output!);
            var expected = ToolRecipeLineIntersectionExecution.Execute(document, "step.corner.01", lineA.Output!, lineB.Output!);
            var report = File.ReadAllText(reportPath);
            return (runnerExitCode == 0
                && expected.Output is not null
                && report.Contains($"sha256={expected.Output.ContentSha256}", StringComparison.Ordinal)
                && report.Contains("LineIntersection|status=Pass", StringComparison.Ordinal),
                $"runnerExit={runnerExitCode};expected={expected.Output?.ContentSha256};report={report.Replace(Environment.NewLine, ";")}");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static ToolRecipeStep CreateEdgeStep(string id, string selectionId, string axis, string output) =>
        new(id, "height-difference-edge", "Height Difference Edge", 1, ["derived.filtered.01", selectionId], output, [new("ComparisonAxis", axis), new("Polarity", "Rising"), new("MinimumDelta", "5"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")]);

    private static ToolRecipeStep CreateLineStep(string id, string input, string output) =>
        new(id, "three-d-line-fit", "3D Line Fit", 1, [input], output, [new("FitMethod", "DeterministicConsensusOrthogonalTls"), new("MaximumOrthogonalResidual", "0.001"), new("MinimumInlierCount", "3"), new("MinimumInlierRatio", "1"), new("MinimumInlierScanlineSpan", "2"), new("HypothesisPolicy", "Sha256PairSchedule"), new("MaximumHypotheses", "256"), new("RefinementPolicy", "OrthogonalTlsUntilStable10"), new("DirectionPolicy", "PositiveScanlineAxis"), new("EndpointPolicy", "InlierProjectionExtents")]);

    private static C3DLineIntersectionEvaluation Evaluate(C3DLineFeature first, C3DLineFeature second, double maximumGap, double minimumAngle, double maximumExtension) =>
        C3DLineIntersectionRule.Evaluate(new C3DLineIntersectionInput("step.corner.01", first, second, "derived.corner.01", maximumGap, minimumAngle, maximumExtension, "UpperLeftCorner"));

    private static C3DLineFeature CreateLine(string outputId, (double X, double Y, double Z) anchor, (double X, double Y, double Z) direction, (double X, double Y, double Z) start, (double X, double Y, double Z) end, string? rootHash = null) =>
        C3DLineFeature.Create(outputId, CreateEdge($"{outputId}.edge", rootHash), 0.1, 3, 1, 2,
            anchor.X, anchor.Y, anchor.Z, direction.X, direction.Y, direction.Z,
            start.X, start.Y, start.Z, end.X, end.Y, end.Z, Diagnostics(), [], "fixture");

    private static C3DHeightDifferenceEdgePointSet CreateEdge(string outputId, string? rootHash = null) =>
        C3DHeightDifferenceEdgePointSet.Create(outputId, "source.synthetic", rootHash ?? new string('A', 64), "derived.filtered.01", new string('B', 64), "selection.fixture", new ToolRecipeGridRectangle(0, 0, 3, 3), "raw-height", "frame.c3d-grid-index", C3DHeightDifferenceComparisonAxis.AcrossColumns, C3DHeightDifferencePolarity.Rising, 1,
            [new C3DHeightDifferenceEdgePoint(0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0), new C3DHeightDifferenceEdgePoint(1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1), new C3DHeightDifferenceEdgePoint(2, 2, 0, 0, 2, 1, 0, 1, 1, 0, 0, 2)],
            new C3DHeightDifferenceEdgeDiagnostics(3, 3, 0, 3, 0, 1, 1, 1), "fixture");

    private static C3DLineFeatureDiagnostics Diagnostics() => new(3, 3, 0, 1, 0, 2, 2, 0, 0, 0, 4, 1, 1);
    private static ToolRecipeDocument CreateDocument(C3DLineFeature first, C3DLineFeature second, IReadOnlyList<ToolRecipeParameter> parameters) => new(
        ToolRecipeDocument.CurrentSchemaVersion, "Intersection fixture",
        new ToolRecipeSource("source.synthetic", "Synthetic", "C3D", "raw-height", "frame.c3d-grid-index", "fixture.c3d", 1, new string('A', 64), 3, 3), [],
        [
            new ToolRecipeStep("step.line.first", "fixture-line", "Fixture line", 1, ["source.synthetic"], first.OutputEntityId, []),
            new ToolRecipeStep("step.line.second", "fixture-line", "Fixture line", 1, ["source.synthetic"], second.OutputEntityId, []),
            new ToolRecipeStep("step.corner.01", "line-intersection", "Line Intersection", 2, [first.OutputEntityId, second.OutputEntityId], "derived.corner.01", parameters)
        ], []);

    private static IReadOnlyList<ToolRecipeParameter> Parameters(string role) =>
    [
        new("MaximumClosestApproachDistance", "0.5"), new("MinimumAcuteAngleDegrees", "45"), new("MaximumSupportExtension", "0"), new("OutputRole", role),
        new("ClosestApproachPolicy", "MidpointOfClosestPoints"), new("ParallelPolicy", "RejectBelowMinimumAcuteAngle"), new("SupportPolicy", "WithinInlierProjectionExtentsWithMaximumExtension")
    ];
    private static bool Approximately(double actual, double expected, double tolerance = 1e-8) => Math.Abs(actual - expected) <= tolerance;
    private static string Evidence(C3DLineIntersectionEvaluation evaluation) => $"status={evaluation.Result.Status};gap={evaluation.Output?.ClosestApproachDistance};angle={evaluation.Output?.AcuteAngleDegrees};hash={evaluation.Output?.ContentSha256};message={evaluation.Result.Message}";
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
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
