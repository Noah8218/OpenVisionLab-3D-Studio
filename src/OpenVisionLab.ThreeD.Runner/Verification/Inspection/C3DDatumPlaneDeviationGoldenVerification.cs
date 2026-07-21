using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DDatumPlaneDeviationGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("analytic-pass-and-fail-retain-measurement", VerifyAnalyticPassAndFail),
            Check("vertical-plane-rejected", VerifyVerticalPlaneRejected),
            Check("strict-lineage-and-roi-binding", VerifyStrictLineage),
            Check("runner-replay", VerifyRunnerReplay),
            Check("cancellation-propagates", VerifyCancellation)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath,
        [
            $"C3DDatumPlaneDeviationGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|numeric=X-column,Y-current-raw-height,Z-row|residual=rawHeight-datumPlanePredictedRawHeight|source=verified-C3D",
            .. cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
        ]);
        Console.WriteLine($"3D Datum Plane Raw-Height Deviation golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyAnalyticPassAndFail()
    {
        using var fixture = Fixture.Create();
        var plane = CreatePlane(fixture.Source);
        var pass = C3DDatumPlaneDeviationRule.Evaluate(CreateInput(fixture, plane, 0.5d));
        var fail = C3DDatumPlaneDeviationRule.Evaluate(CreateInput(fixture, plane, 0.3d));
        return (pass.Output is not null && fail.Output is not null
            && pass.Result.Status == ResultStatus.Pass && fail.Result.Status == ResultStatus.Fail
            && Approximately(pass.Output.PeakToValleyRawHeight, 0.4d)
            && Approximately(pass.Output.MinimumRawHeightResidual, 0d)
            && Approximately(pass.Output.MaximumRawHeightResidual, 0.4d)
            && pass.Output.ValidSampleCount == 9 && pass.Output.MissingSampleCount == 0
            && pass.Output.OverlaySamples.Count == 9,
            $"pass={Evidence(pass)};fail={Evidence(fail)}");
    }

    private static (bool Passed, string Evidence) VerifyVerticalPlaneRejected()
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification("source.vertical", 3, 3, [2, 2, 2, 3, 2, 2, 2, 2, 2]);
        var plane = C3DThreePointPlaneRule.Evaluate(new C3DThreePointPlaneInput(
            "step.vertical-plane", source,
            CreatePlaneSelection(source, "selection.vertical-plane", (0, 0), (1, 0), (2, 0)),
            "derived.vertical-plane", "VerticalFixture"));
        var selection = CreateRectangleSelection(source, "selection.vertical-roi", 0, 0, 3, 3);
        var evaluation = plane.Output is null
            ? new C3DDatumPlaneDeviationEvaluation(new ToolResult("fixture", ResultStatus.Error, "plane construction failed", TimeSpan.Zero, [], []), null)
            : C3DDatumPlaneDeviationRule.Evaluate(new C3DDatumPlaneDeviationInput("step.vertical", source, plane.Output, selection, "derived.vertical", 1d, 3, 0.1d, "Vertical"));
        return (plane.Output is not null && evaluation.Result.Status == ResultStatus.Error
            && evaluation.Result.Message.Contains("cannot be represented as raw height", StringComparison.OrdinalIgnoreCase), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyStrictLineage()
    {
        using var fixture = Fixture.Create();
        var plane = CreatePlane(fixture.Source);
        var wrongBinding = fixture.MeasurementSelection with
        {
            SourceBinding = fixture.MeasurementSelection.SourceBinding with { ContentSha256 = new string('F', 64) }
        };
        var evaluation = C3DDatumPlaneDeviationRule.Evaluate(new C3DDatumPlaneDeviationInput(
            fixture.StepId, fixture.Source, plane, wrongBinding, "derived.wrong", 0.5d, 3, 0.1d, "Fixture"));
        return (evaluation.Result.Status == ResultStatus.Error
            && evaluation.Result.Message.Contains("identity", StringComparison.OrdinalIgnoreCase), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyRunnerReplay()
    {
        using var fixture = Fixture.Create();
        var recipePath = Path.Combine(fixture.Root, "fixture.recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, fixture.Document);
        var reportPath = Path.Combine(fixture.Root, "runner.txt");
        var exitCode = ToolRecipeDatumPlaneDeviationRunnerExecution.Run(recipePath, fixture.StepId, reportPath);
        var report = File.Exists(reportPath) ? File.ReadAllText(reportPath) : string.Empty;
        return (exitCode == 0 && report.Contains("DatumPlaneDeviation|status=Pass", StringComparison.Ordinal)
            && report.Contains("p2vRawHeight=", StringComparison.Ordinal), $"exit={exitCode};report={report.Replace(Environment.NewLine, " / ")}");
    }

    private static (bool Passed, string Evidence) VerifyCancellation()
    {
        using var fixture = Fixture.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            _ = C3DDatumPlaneDeviationRule.Evaluate(CreateInput(fixture, CreatePlane(fixture.Source), 0.5d), cancellation.Token);
            return (false, "no cancellation thrown");
        }
        catch (OperationCanceledException)
        {
            return (true, "OperationCanceledException");
        }
    }

    private static C3DDatumPlaneDeviationInput CreateInput(Fixture fixture, C3DThreePointPlaneFeature plane, double maximum) =>
        new(fixture.StepId, fixture.Source, plane, fixture.MeasurementSelection, "derived.fixture-deviation", maximum, 3, 0.1d, "FixtureDeviation");

    private static C3DThreePointPlaneFeature CreatePlane(C3DHeightFieldSnapshot source)
    {
        var evaluation = C3DThreePointPlaneRule.Evaluate(new C3DThreePointPlaneInput(
            "step.fixture-plane", source,
            CreatePlaneSelection(source, "selection.fixture-plane", (0, 0), (0, 1), (1, 0)),
            "derived.fixture-plane", "FixtureDatum"));
        return evaluation.Output ?? throw new InvalidOperationException(evaluation.Result.Message);
    }

    private static ToolRecipeSelection CreatePlaneSelection(C3DHeightFieldSnapshot source, string id, params (int Row, int Column)[] points) => new(
        id, "Fixture plane picks", ToolRecipeSelectionKinds.PointSet, source.EntityId, source.FrameId,
        new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), null,
        points.Select(point =>
        {
            var height = source.Values.Span[(point.Row * source.Width) + point.Column];
            return new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", point.Row, point.Column), new ToolRecipeXyz(point.Column, height, point.Row), height);
        }).ToArray(), null);

    private static ToolRecipeSelection CreateRectangleSelection(C3DHeightFieldSnapshot source, string id, int row, int column, int rows, int columns) => new(
        id, "Fixture measurement ROI", ToolRecipeSelectionKinds.GridRectangle, source.EntityId, source.FrameId,
        new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height),
        new ToolRecipeGridRectangle(row, column, rows, columns), null, null);

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) => double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;
    private static string Evidence(C3DDatumPlaneDeviationEvaluation evaluation) => $"status={evaluation.Result.Status};p2v={evaluation.Output?.PeakToValleyRawHeight};hash={evaluation.Output?.ContentSha256};message={evaluation.Result.Message}";
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private static VerificationCase Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try { var result = verify(); return new VerificationCase(name, result.Passed, result.Evidence); }
        catch (Exception exception) { return new VerificationCase(name, false, $"unexpected {exception.GetType().Name}: {exception.Message}"); }
    }

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);

    private sealed class Fixture : IDisposable
    {
        private Fixture(string root, C3DHeightFieldSnapshot source, ToolRecipeSelection measurementSelection, string stepId, ToolRecipeDocument document)
        {
            Root = root; Source = source; MeasurementSelection = measurementSelection; StepId = stepId; Document = document;
        }

        public string Root { get; }
        public C3DHeightFieldSnapshot Source { get; }
        public ToolRecipeSelection MeasurementSelection { get; }
        public string StepId { get; }
        public ToolRecipeDocument Document { get; }

        public static Fixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "DatumPlaneDeviation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification("source.c3d.height-map", 3, 3, [10d, 11d, 12d, 12d, 13d, 14d, 14d, 15d, 16.4d]);
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var planeSelection = CreatePlaneSelection(source, "selection.fixture-plane", (0, 0), (0, 1), (1, 0));
            var rectangle = CreateRectangleSelection(source, "selection.fixture-roi", 0, 0, 3, 3);
            var planeStepId = "step.fixture-plane";
            var stepId = "step.fixture-datum-deviation";
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Datum plane deviation fixture",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [
                    new ToolRecipeStep(planeStepId, "three-point-plane", "3-Point Plane", 1, [source.EntityId, planeSelection.Id], "derived.fixture-plane", [new ToolRecipeParameter("OutputRole", "FixtureDatum"), new ToolRecipeParameter("ConstructionPolicy", "OrderedPointsDefineOrientedPlane")]),
                    new ToolRecipeStep(stepId, "datum-plane-raw-height-deviation", "Datum Plane Raw-Height Deviation", 2, [source.EntityId, "derived.fixture-plane", rectangle.Id], "derived.fixture-deviation", [new ToolRecipeParameter("MaximumPeakToValleyRawHeight", "0.5"), new ToolRecipeParameter("OutputRole", "FixtureDeviation"), new ToolRecipeParameter("ResidualPolicy", "RawHeightMinusDatumPlanePredictedRawHeight"), new ToolRecipeParameter("MinimumValidSampleCount", "3"), new ToolRecipeParameter("MinimumAbsoluteNormalY", "0.1")])
                ],
                [planeSelection, rectangle]);
            return new Fixture(root, source, rectangle, stepId, document);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
