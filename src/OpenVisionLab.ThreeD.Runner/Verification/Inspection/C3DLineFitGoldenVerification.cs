using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DLineFitGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("across-columns-canonical-positive-z", VerifyAcrossColumnsCanonicalDirection),
            Check("across-rows-canonical-positive-x", VerifyAcrossRowsCanonicalDirection),
            Check("full-xyz-oblique-tls-with-outliers", VerifyFullXyzOutlierRejection),
            Check("inclusive-maximum-residual", VerifyInclusiveResidual),
            Check("support-gates-fail-closed", VerifySupportFailure),
            Check("degenerate-and-non-finite-fail-closed", VerifyDegenerateFailures),
            Check("deterministic-hash-and-diagnostics", VerifyDeterminism),
            Check("strict-recipe-adapter", VerifyStrictRecipeAdapter),
            Check("cancellation-propagates", VerifyCancellation)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DLineFitGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|numeric=X-column,Y-raw-height,Z-row|method=DeterministicConsensusOrthogonalTls|hypotheses=Sha256PairSchedule/256|refinement=OrthogonalTlsUntilStable10|residual=source-coordinate"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"3D Line Fit golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyAcrossColumnsCanonicalDirection()
    {
        var edge = CreateEdge(C3DHeightDifferenceComparisonAxis.AcrossColumns, [(0, 0d, 0d, 0d), (1, 0d, 0d, 1d), (2, 0d, 0d, 2d), (3, 0d, 0d, 3d)]);
        var evaluation = Evaluate(edge, maximumResidual: 0.01, minimumCount: 3, minimumRatio: 0.75, minimumSpan: 2);
        var output = evaluation.Output;
        return (evaluation.Result.Status == ResultStatus.Pass && output is not null && output.DirectionZ > 0.999999 && Approximately(output.SegmentStartZ, 0) && Approximately(output.SegmentEndZ, 3), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyAcrossRowsCanonicalDirection()
    {
        var edge = CreateEdge(C3DHeightDifferenceComparisonAxis.AcrossRows, [(0, 0d, 0d, 0d), (1, 1d, 0d, 0d), (2, 2d, 0d, 0d), (3, 3d, 0d, 0d)]);
        var evaluation = Evaluate(edge, maximumResidual: 0.01, minimumCount: 3, minimumRatio: 0.75, minimumSpan: 2);
        var output = evaluation.Output;
        return (evaluation.Result.Status == ResultStatus.Pass && output is not null && output.DirectionX > 0.999999 && Approximately(output.SegmentStartX, 0) && Approximately(output.SegmentEndX, 3), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyFullXyzOutlierRejection()
    {
        var points = new List<(int Scanline, double X, double Y, double Z)>();
        for (var index = 0; index < 8; index++) points.Add((index, 2 + 0.5 * index, -3 + 0.25 * index, index));
        points.Add((8, 20, -30, 8));
        points.Add((9, -10, 25, 9));
        var edge = CreateEdge(C3DHeightDifferenceComparisonAxis.AcrossColumns, points);
        var evaluation = Evaluate(edge, maximumResidual: 0.05, minimumCount: 6, minimumRatio: 0.6, minimumSpan: 5);
        var output = evaluation.Output;
        var expected = new[] { 0.5, 0.25, 1d };
        var norm = Math.Sqrt(expected.Sum(value => value * value));
        return (evaluation.Result.Status == ResultStatus.Pass && output is not null
            && output.Diagnostics.InlierCount == 8
            && Approximately(output.DirectionX, expected[0] / norm, 1e-6)
            && Approximately(output.DirectionY, expected[1] / norm, 1e-6)
            && Approximately(output.DirectionZ, expected[2] / norm, 1e-6)
            && output.PointDiagnostics.Count(point => !point.IsInlier) == 2,
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInclusiveResidual()
    {
        var edge = CreateEdge(C3DHeightDifferenceComparisonAxis.AcrossColumns, [(0, 0d, 0d, 0d), (1, 0d, 0d, 1d), (2, 0d, 0d, 2d), (3, 1d, 0d, 3d)]);
        var evaluation = Evaluate(edge, maximumResidual: 1, minimumCount: 4, minimumRatio: 1, minimumSpan: 3);
        return (evaluation.Result.Status == ResultStatus.Pass && evaluation.Output?.Diagnostics.InlierCount == 4, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifySupportFailure()
    {
        var edge = CreateEdge(C3DHeightDifferenceComparisonAxis.AcrossColumns, [(0, 0d, 0d, 0d), (1, 0d, 0d, 1d), (2, 0d, 0d, 2d), (3, 50d, 50d, 3d), (4, -50d, -50d, 4d)]);
        var evaluation = Evaluate(edge, maximumResidual: 0.01, minimumCount: 3, minimumRatio: 0.8, minimumSpan: 2);
        return (evaluation.Result.Status == ResultStatus.Error && evaluation.Output is null && evaluation.Result.Message.Contains("support", StringComparison.OrdinalIgnoreCase), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyDegenerateFailures()
    {
        var identical = CreateEdge(C3DHeightDifferenceComparisonAxis.AcrossColumns, [(0, 1d, 1d, 1d), (1, 1d, 1d, 1d), (2, 1d, 1d, 1d)]);
        var duplicate = Evaluate(identical, 0.1, 3, 1, 2);
        var nonFinite = C3DHeightDifferenceEdgePointSet.Create(
            "derived.edge.nonfinite", "source.synthetic", new string('A', 64), "derived.filtered.01", new string('B', 64), "selection.fixture", new ToolRecipeGridRectangle(0, 0, 3, 3), "raw-height", "frame.c3d-grid-index", C3DHeightDifferenceComparisonAxis.AcrossColumns, C3DHeightDifferencePolarity.Rising, 1,
            [new C3DHeightDifferenceEdgePoint(0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0), new C3DHeightDifferenceEdgePoint(1, 1, 0, 0, 1, 1, 0, 1, 1, double.NaN, 0, 1), new C3DHeightDifferenceEdgePoint(2, 2, 0, 0, 2, 1, 0, 1, 1, 0, 0, 2)],
            new C3DHeightDifferenceEdgeDiagnostics(3, 3, 0, 3, 0, 1, 1, 1), "fixture");
        var invalid = Evaluate(nonFinite, 0.1, 3, 1, 2);
        return (duplicate.Result.Status == ResultStatus.Error && invalid.Result.Status == ResultStatus.Error && invalid.Result.Message.Contains("non-finite", StringComparison.OrdinalIgnoreCase), $"duplicate={Evidence(duplicate)};nonFinite={Evidence(invalid)}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var points = Enumerable.Range(0, 40).Select(index => ((int)index, (double)index, (double)index / 2, (double)index)).ToArray();
        var edge = CreateEdge(C3DHeightDifferenceComparisonAxis.AcrossColumns, points);
        var first = Evaluate(edge, 0.001, 3, 0.9, 20);
        var second = Evaluate(edge, 0.001, 3, 0.9, 20);
        return (first.Output?.ContentSha256 == second.Output?.ContentSha256
            && first.Output?.PointDiagnostics.Select(point => point.IsInlier).SequenceEqual(second.Output?.PointDiagnostics.Select(point => point.IsInlier) ?? []) == true,
            $"first={Evidence(first)};second={Evidence(second)}");
    }

    private static (bool Passed, string Evidence) VerifyStrictRecipeAdapter()
    {
        var edge = CreateEdge(C3DHeightDifferenceComparisonAxis.AcrossColumns, [(0, 0d, 0d, 0d), (1, 0d, 0d, 1d), (2, 0d, 0d, 2d), (3, 0d, 0d, 3d)]);
        var document = CreateDocument(edge, [
            new("FitMethod", "DeterministicConsensusOrthogonalTls"), new("MaximumOrthogonalResidual", "0.1"), new("MinimumInlierCount", "3"), new("MinimumInlierRatio", "0.75"), new("MinimumInlierScanlineSpan", "2"), new("HypothesisPolicy", "Sha256PairSchedule"), new("MaximumHypotheses", "256"), new("RefinementPolicy", "OrthogonalTlsUntilStable10"), new("DirectionPolicy", "PositiveScanlineAxis"), new("EndpointPolicy", "InlierProjectionExtents")]);
        var ready = ToolRecipeLineFitExecution.TryPrepare(document, "step.line.01", edge, out _, out var readyMessage);
        var invalid = document with { Steps = [document.Steps[0] with { Parameters = [.. document.Steps[0].Parameters, new ToolRecipeParameter("Unknown", "x")] }] };
        var rejected = !ToolRecipeLineFitExecution.TryPrepare(invalid, "step.line.01", edge, out _, out var rejectedMessage);
        return (ready && rejected && rejectedMessage.Contains("exactly", StringComparison.OrdinalIgnoreCase), $"ready={ready}:{readyMessage};rejected={rejected}:{rejectedMessage}");
    }

    private static (bool Passed, string Evidence) VerifyCancellation()
    {
        var edge = CreateEdge(C3DHeightDifferenceComparisonAxis.AcrossColumns, Enumerable.Range(0, 300).Select(index => ((int)index, 0d, 0d, (double)index)).ToArray());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            _ = C3DLineFitRule.Evaluate(new C3DLineFitInput("step.line.01", edge, "derived.line.01", 0.1, 3, 0.5, 2), cancellation.Token);
            return (false, "no cancellation thrown");
        }
        catch (OperationCanceledException)
        {
            return (true, "OperationCanceledException");
        }
    }

    private static C3DLineFitEvaluation Evaluate(C3DHeightDifferenceEdgePointSet edge, double maximumResidual, int minimumCount, double minimumRatio, int minimumSpan) =>
        C3DLineFitRule.Evaluate(new C3DLineFitInput("step.line.01", edge, "derived.line.01", maximumResidual, minimumCount, minimumRatio, minimumSpan));

    private static C3DHeightDifferenceEdgePointSet CreateEdge(C3DHeightDifferenceComparisonAxis axis, IEnumerable<(int Scanline, double X, double Y, double Z)> values)
    {
        var points = values.Select(item => new C3DHeightDifferenceEdgePoint(item.Scanline, item.Scanline, 0, item.Y, item.Scanline, 1, item.Y, 1, 1, item.X, item.Y, item.Z)).ToArray();
        return C3DHeightDifferenceEdgePointSet.Create("derived.edgepoints.01", "source.synthetic", new string('A', 64), "derived.filtered.01", new string('B', 64), "selection.fixture", new ToolRecipeGridRectangle(0, 0, Math.Max(3, points.Length), 3), "raw-height", "frame.c3d-grid-index", axis, C3DHeightDifferencePolarity.Rising, 1, points, new C3DHeightDifferenceEdgeDiagnostics(points.Length, points.Length, 0, points.Length, 0, 1, 1, 1), "fixture");
    }

    private static ToolRecipeDocument CreateDocument(C3DHeightDifferenceEdgePointSet edge, IReadOnlyList<ToolRecipeParameter> parameters) => new(
        ToolRecipeDocument.CurrentSchemaVersion,
        "Line fit fixture",
        new ToolRecipeSource(edge.RootSourceEntityId, "Synthetic", "C3D", edge.Unit, edge.FrameId, "fixture.c3d", 1, edge.RootSourceSha256, 3, 3),
        [],
        [
            new ToolRecipeStep("step.edge.fixture.01", "fixture-edge", "Fixture Edge", 1, [edge.RootSourceEntityId], edge.OutputEntityId, []),
            new ToolRecipeStep("step.line.01", "three-d-line-fit", "3D Line Fit", 1, [edge.OutputEntityId], "derived.line.01", parameters)
        ],
        []);

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) => Math.Abs(actual - expected) <= tolerance;
    private static string Evidence(C3DLineFitEvaluation evaluation) => $"status={evaluation.Result.Status};inliers={evaluation.Output?.Diagnostics.InlierCount};hash={evaluation.Output?.ContentSha256};message={evaluation.Result.Message}";
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
