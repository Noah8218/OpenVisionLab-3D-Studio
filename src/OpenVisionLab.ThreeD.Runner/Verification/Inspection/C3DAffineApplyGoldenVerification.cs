using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DAffineApplyGoldenVerification
{
    private const string SourceId = "source.affine-apply.fixture";
    private const string SourceUnit = "raw-height";
    private const string SourceFrame = "frame.c3d-grid-index";

    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("ordered-full-xyz-application-and-source-immutability", VerifyDirectApply),
            Check("deterministic-output-and-verified-recipe-adapter", VerifyDeterminismAndAdapter),
            Check("stale-affine-and-invalid-route-rejected", VerifyStaleAndInvalidRoute),
            Check("cancellation-propagates", VerifyCancellation)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath,
        [
            $"C3DAffineApplyGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|input=raw-C3D-plus-Published-AffineTransform3D|output=TransformedPointCloud|coordinate=column-rawHeight-row|regrid=excluded|measurement=excluded",
            .. cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
        ]);
        Console.WriteLine($"3D XYZ Affine Apply golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyDirectApply()
    {
        var snapshot = CreateSnapshot();
        var transform = CreatePublishedTransform(snapshot);
        var beforeHash = snapshot.ContentSha256;
        var beforeValues = snapshot.Values.ToArray();
        var evaluation = C3DAffineApplyRule.Evaluate(new C3DAffineApplyInput(
            "step.affine.apply", snapshot, transform, "derived.affine-point-cloud"));
        var output = evaluation.Output;
        var first = output?.Points.FirstOrDefault();
        var expected = transform.Transform(0, 1, 0);
        var pass = evaluation.Result.Status == ResultStatus.Pass
            && output is not null
            && output.FinitePointCount == 6
            && output.MissingPointCount == 0
            && output.Points.Select(point => (point.Row, point.Column)).SequenceEqual(
                Enumerable.Range(0, 6).Select(index => (index / 3, index % 3)))
            && first is { Row: 0, Column: 0 }
            && Nearly(first.Value.X, expected.X)
            && Nearly(first.Value.Y, expected.Y)
            && Nearly(first.Value.Z, expected.Z)
            && snapshot.ContentSha256 == beforeHash
            && snapshot.Values.Span.SequenceEqual(beforeValues);
        return (pass, $"status={evaluation.Result.Status};points={output?.FinitePointCount};missing={output?.MissingPointCount};first={first?.X:G8},{first?.Y:G8},{first?.Z:G8};sourceHashUnchanged={snapshot.ContentSha256 == beforeHash}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminismAndAdapter()
    {
        var snapshot = CreateSnapshot();
        var transform = CreatePublishedTransform(snapshot);
        var first = C3DAffineApplyRule.Evaluate(new C3DAffineApplyInput("step.affine.apply", snapshot, transform, "derived.affine-point-cloud"));
        var second = C3DAffineApplyRule.Evaluate(new C3DAffineApplyInput("step.affine.apply", snapshot, transform, "derived.affine-point-cloud"));
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab3D", "AffineApplyGolden", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var c3dPath = Path.Combine(directory, "fixture.c3d");
            snapshot.SaveC3D(c3dPath);
            var recipe = CreateApplyRecipe(snapshot, transform, Path.GetFileName(c3dPath));
            var prepared = ToolRecipeXYZAffineApplyExecution.TryPrepare(recipe, "step.affine.apply", transform, directory, out var input, out var message);
            var adapter = ToolRecipeXYZAffineApplyExecution.Execute(recipe, "step.affine.apply", transform, directory);
            var pass = first.Output is not null && second.Output is not null && adapter.Output is not null
                && first.Output.ContentSha256 == second.Output.ContentSha256
                && first.Output.ContentSha256 == adapter.Output.ContentSha256
                && prepared && input is not null && adapter.Result.Status == ResultStatus.Pass;
            return (pass, $"prepared={prepared}:{message};direct={first.Output?.ContentSha256};adapter={adapter.Output?.ContentSha256}");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyStaleAndInvalidRoute()
    {
        var snapshot = CreateSnapshot();
        var transform = CreatePublishedTransform(snapshot);
        var staleSnapshot = C3DHeightFieldSnapshot.CreateForVerification("source.other", 3, 2, [1d, 4d, 2d, 3d, 5d, 6d], SourceUnit, SourceFrame);
        var stale = C3DAffineApplyRule.Evaluate(new C3DAffineApplyInput(
            "step.affine.apply", staleSnapshot, transform, "derived.stale"));
        var invalidRecipe = CreateApplyRecipe(snapshot, transform, "fixture.c3d") with
        {
            Steps =
            [
                new ToolRecipeStep("step.fixture.solve", "fixture-affine-solve", "Fixture solve route", 1, [SourceId], transform.OutputEntityId, []),
                new ToolRecipeStep("step.affine.apply", "xyz-affine-apply", "Apply XYZ Affine", 2, [transform.OutputEntityId, SourceId], "derived.affine-point-cloud", [])
            ]
        };
        var prepared = ToolRecipeXYZAffineApplyExecution.TryPrepare(invalidRecipe, "step.affine.apply", transform, null, out _, out var message);
        var pass = stale.Result.Status == ResultStatus.Error
            && stale.Output is null
            && stale.Result.Message.Contains("identity/frame/unit/convention", StringComparison.Ordinal)
            && !prepared
            && message.Contains("source first", StringComparison.OrdinalIgnoreCase);
        return (pass, $"stale={stale.Result.Status}:{stale.Result.Message};invalidRoutePrepared={prepared}:{message}");
    }

    private static (bool Passed, string Evidence) VerifyCancellation()
    {
        var snapshot = CreateSnapshot();
        var transform = CreatePublishedTransform(snapshot);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = C3DAffineApplyRule.Evaluate(new C3DAffineApplyInput(
                "step.affine.apply", snapshot, transform, "derived.affine-point-cloud"), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        return (canceled, $"canceled={canceled}");
    }

    private static C3DHeightFieldSnapshot CreateSnapshot() =>
        C3DHeightFieldSnapshot.CreateForVerification(SourceId, 3, 2, [1d, 4d, 2d, 3d, 5d, 6d], SourceUnit, SourceFrame);

    private static C3DAffineTransform3D CreatePublishedTransform(C3DHeightFieldSnapshot snapshot)
    {
        var coordinates = new[] { (Row: 0, Column: 0), (Row: 0, Column: 1), (Row: 0, Column: 2), (Row: 1, Column: 0) };
        var pairs = coordinates.Select((locator, index) =>
        {
            var raw = snapshot.Values.Span[locator.Row * snapshot.Width + locator.Column];
            var source = (X: (double)locator.Column, Y: raw, Z: (double)locator.Row);
            var reference = ApplyFixtureMatrix(source.X, source.Y, source.Z);
            return new C3DLandmarkCorrespondencePair(
                $"derived.fixture.corner.{index}", "Corner anchor", snapshot.RootSourceSha256,
                source.X, source.Y, source.Z, $"fixture.corner.{index}", reference.X, reference.Y, reference.Z);
        }).ToArray();
        var correspondence = C3DLandmarkCorrespondenceSet.Create(
            "derived.fixture.correspondence", pairs, snapshot.EntityId, snapshot.RootSourceSha256,
            snapshot.Unit, snapshot.FrameId, "frame.fixture-reference", "fixture-unit", "fixture reference", "R1",
            1e-12, 4, 4, 0.1, 0.1, "affine apply golden fixture");
        var solve = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput(
            "step.fixture.solve", "derived.fixture.affine", correspondence, 1000, 1e-12));
        if (solve.Result.Status != ResultStatus.Pass || solve.Output is null)
        {
            throw new InvalidDataException($"Affine apply golden fixture could not publish A1: {solve.Result.Message}");
        }
        return solve.Output;
    }

    private static ToolRecipeDocument CreateApplyRecipe(C3DHeightFieldSnapshot snapshot, C3DAffineTransform3D transform, string sourcePath) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Affine apply fixture",
            new ToolRecipeSource(snapshot.EntityId, "Fixture C3D", "C3D", snapshot.Unit, snapshot.FrameId, sourcePath,
                snapshot.ByteLength, snapshot.ContentSha256, snapshot.Width, snapshot.Height),
            [],
            [
                new ToolRecipeStep("step.fixture.solve", "fixture-affine-solve", "Fixture solve route", 1, [snapshot.EntityId], transform.OutputEntityId, []),
                new ToolRecipeStep("step.affine.apply", "xyz-affine-apply", "Apply XYZ Affine", 2,
                    [snapshot.EntityId, transform.OutputEntityId], "derived.affine-point-cloud", [])
            ],
            []);

    private static (double X, double Y, double Z) ApplyFixtureMatrix(double x, double y, double z) =>
        (2 * x + 0.5 * y - 0.25 * z + 10,
        -x + 3 * y + 0.75 * z + 20,
        0.2 * x - 0.3 * y + 4 * z + 30);

    private static bool Nearly(double actual, double expected) => Math.Abs(actual - expected) <= 1e-10;
    private static (string Name, bool Passed, string Evidence) Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return (name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return (name, false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}
