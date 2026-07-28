using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DThreePointPlaneGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("exact-current-source-plane", VerifyExactCurrentSourcePlane),
            Check("ordered-picks-reverse-normal-and-hash", VerifyOrderedPicks),
            Check("collinear-current-source-fails-closed", VerifyCollinearCurrentSource),
            Check("strict-adapter-policy-and-binding", VerifyStrictAdapter),
            Check("source-tamper-fails-closed", VerifySourceTamper),
            Check("runner-replay", VerifyRunnerReplay),
            Check("cancellation-propagates", VerifyCancellation)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DThreePointPlaneGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|numeric=X-column,Y-current-raw-height,Z-row|policy=OrderedPointsDefineOrientedPlane|source=verified-C3D"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"3D 3-Point Plane golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyExactCurrentSourcePlane()
    {
        using var fixture = Fixture.Create();
        var evaluation = ToolRecipeThreePointPlaneExecution.Execute(fixture.Document, fixture.StepId, fixture.Root);
        var output = evaluation.Output;
        var firstOnPlane = output is not null && Approximately(DotPlane(output, output.AnchorX, output.AnchorY, output.AnchorZ), 0);
        var secondOnPlane = output is not null && Approximately(DotPlane(output, output.SecondX, output.SecondY, output.SecondZ), 0);
        var thirdOnPlane = output is not null && Approximately(DotPlane(output, output.ThirdX, output.ThirdY, output.ThirdZ), 0);
        return (evaluation.Result.Status == ResultStatus.Pass && output is not null
            && output.FirstRow == 0 && output.FirstColumn == 0
            && output.SecondRow == 0 && output.SecondColumn == 1
            && output.ThirdRow == 1 && output.ThirdColumn == 0
            && Approximately(Math.Sqrt((output.NormalX * output.NormalX) + (output.NormalY * output.NormalY) + (output.NormalZ * output.NormalZ)), 1)
            && firstOnPlane && secondOnPlane && thirdOnPlane, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyOrderedPicks()
    {
        using var fixture = Fixture.Create();
        var normal = ToolRecipeThreePointPlaneExecution.Execute(fixture.Document, fixture.StepId, fixture.Root);
        var selection = fixture.Document.Selections!.Single();
        var reversed = selection with { Points = [selection.Points![0], selection.Points[2], selection.Points[1]] };
        var reversedDocument = fixture.Document with { Selections = [reversed] };
        var reverse = ToolRecipeThreePointPlaneExecution.Execute(reversedDocument, fixture.StepId, fixture.Root);
        return (normal.Output is not null && reverse.Output is not null
            && normal.Output.ContentSha256 != reverse.Output.ContentSha256
            && Approximately(normal.Output.NormalX, -reverse.Output.NormalX)
            && Approximately(normal.Output.NormalY, -reverse.Output.NormalY)
            && Approximately(normal.Output.NormalZ, -reverse.Output.NormalZ)
            && Approximately(normal.Output.PlaneOffset, -reverse.Output.PlaneOffset), $"normal={Evidence(normal)};reversed={Evidence(reverse)}");
    }

    private static (bool Passed, string Evidence) VerifyCollinearCurrentSource()
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", 3, 3, [1, 0, 0, 0, 2, 0, 0, 0, 3]);
        var selection = CreateSelection(source, (0, 0), (1, 1), (2, 2));
        var evaluation = C3DThreePointPlaneRule.Evaluate(new C3DThreePointPlaneInput("step.plane", source, selection, "derived.plane", "FixtureDatum"));
        return (evaluation.Result.Status == ResultStatus.Error
            && evaluation.Result.Message.Contains("collinear", StringComparison.OrdinalIgnoreCase), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyStrictAdapter()
    {
        using var fixture = Fixture.Create();
        var ready = ToolRecipeThreePointPlaneExecution.TryPrepare(fixture.Document, fixture.StepId, fixture.Root, out _, out var readyMessage);
        var invalidPolicy = fixture.Document with
        {
            Steps = [fixture.Document.Steps[0] with { Parameters = [new ToolRecipeParameter("OutputRole", "FixtureDatum"), new ToolRecipeParameter("ConstructionPolicy", "CanonicalNormal")] }]
        };
        var rejectedPolicy = !ToolRecipeThreePointPlaneExecution.TryPrepare(invalidPolicy, fixture.StepId, fixture.Root, out _, out var policyMessage);
        var wrongBinding = fixture.Document with { Selections = [fixture.Document.Selections![0] with { SourceBinding = fixture.Document.Selections[0].SourceBinding with { ContentSha256 = new string('F', 64) } }] };
        var rejectedBinding = !ToolRecipeThreePointPlaneExecution.TryPrepare(wrongBinding, fixture.StepId, fixture.Root, out _, out var bindingMessage);
        return (ready && rejectedPolicy && rejectedBinding, $"ready={ready}:{readyMessage};policy={policyMessage};binding={bindingMessage}");
    }

    private static (bool Passed, string Evidence) VerifySourceTamper()
    {
        using var fixture = Fixture.Create();
        File.WriteAllBytes(fixture.SourcePath, [1, 2, 3]);
        var evaluation = ToolRecipeThreePointPlaneExecution.Execute(fixture.Document, fixture.StepId, fixture.Root);
        return (evaluation.Result.Status == ResultStatus.Error && evaluation.Result.Message.Contains("C3D", StringComparison.OrdinalIgnoreCase), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyRunnerReplay()
    {
        using var fixture = Fixture.Create();
        var recipePath = Path.Combine(fixture.Root, "three-point-plane.ov3d-teach.json");
        var reportPath = Path.Combine(fixture.Root, "runner.txt");
        ToolRecipeDocumentStore.Save(recipePath, fixture.Document);
        var expected = ToolRecipeThreePointPlaneExecution.Execute(fixture.Document, fixture.StepId, fixture.Root);
        var exitCode = ToolRecipeThreePointPlaneRunnerExecution.Run(recipePath, fixture.StepId, reportPath);
        var report = File.ReadAllText(reportPath);
        return (exitCode == 0 && expected.Output is not null && report.Contains(expected.Output.ContentSha256, StringComparison.Ordinal),
            $"exit={exitCode};hash={expected.Output?.ContentSha256};report={report.Replace(Environment.NewLine, ";")}");
    }

    private static (bool Passed, string Evidence) VerifyCancellation()
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", 2, 2, [1, 2, 3, 4]);
        var selection = CreateSelection(source, (0, 0), (0, 1), (1, 0));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            _ = C3DThreePointPlaneRule.Evaluate(new C3DThreePointPlaneInput("step.plane", source, selection, "derived.plane", "FixtureDatum"), cancellation.Token);
            return (false, "no cancellation thrown");
        }
        catch (OperationCanceledException)
        {
            return (true, "OperationCanceledException");
        }
    }

    private static ToolRecipeSelection CreateSelection(C3DHeightFieldSnapshot source, (int Row, int Column) first, (int Row, int Column) second, (int Row, int Column) third) => new(
        "selection.plane.01", "Fixture plane picks", ToolRecipeSelectionKinds.PointSet, source.EntityId, source.FrameId,
        new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), null,
        [CreatePoint(source, first.Row, first.Column), CreatePoint(source, second.Row, second.Column), CreatePoint(source, third.Row, third.Column)], null);

    private static ToolRecipeSelectionPoint CreatePoint(C3DHeightFieldSnapshot source, int row, int column)
    {
        var height = source.Values.Span[row * source.Width + column];
        return new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", row, column), new ToolRecipeXyz(column, height, row), height);
    }

    private static double DotPlane(C3DThreePointPlaneFeature output, double x, double y, double z) => (output.NormalX * x) + (output.NormalY * y) + (output.NormalZ * z) + output.PlaneOffset;
    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) => double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;
    private static string Evidence(C3DThreePointPlaneEvaluation evaluation) => $"status={evaluation.Result.Status};hash={evaluation.Output?.ContentSha256};normal={evaluation.Output?.NormalX},{evaluation.Output?.NormalY},{evaluation.Output?.NormalZ};message={evaluation.Result.Message}";
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private static VerificationCase Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try { var result = verify(); return new VerificationCase(name, result.Passed, result.Evidence); }
        catch (Exception exception) { return new VerificationCase(name, false, $"unexpected {exception.GetType().Name}: {exception.Message}"); }
    }

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);

    private sealed class Fixture : IDisposable
    {
        private Fixture(string root, string sourcePath, string stepId, ToolRecipeDocument document)
        {
            Root = root; SourcePath = sourcePath; StepId = stepId; Document = document;
        }

        public string Root { get; }
        public string SourcePath { get; }
        public string StepId { get; }
        public ToolRecipeDocument Document { get; }

        public static Fixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "ThreePointPlane", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification("source.c3d.height-map", 3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var selection = CreateSelection(source, (0, 0), (0, 1), (1, 0));
            var stepId = "step.three-point-plane.01";
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Three point plane fixture",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [new ToolRecipeStep(stepId, "three-point-plane", "3-Point Plane", 1, [source.EntityId, selection.Id], "derived.fixture-plane.01", [new ToolRecipeParameter("OutputRole", "FixtureDatum"), new ToolRecipeParameter("ConstructionPolicy", "OrderedPointsDefineOrientedPlane")])],
                [selection]);
            return new Fixture(root, sourcePath, stepId, document);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
