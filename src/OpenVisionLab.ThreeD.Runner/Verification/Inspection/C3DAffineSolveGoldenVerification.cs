using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DAffineSolveGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("identity-and-general-affine-matrix", VerifyKnownAffine),
            Check("deterministic-hash-and-recipe-adapter", VerifyDeterminismAndAdapter),
            Check("condition-and-invalid-input-rejected", VerifyConditionAndInvalidInput),
            Check("five-pairs-and-cancellation-fail-closed", VerifyFivePairsAndCancellation)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath,
        [
            $"C3DAffineSolveGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|input=PublishedCorrespondenceSet|output=AffineTransform3D|policy=ExactFourPartialPivot|apply=excluded",
            .. cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
        ]);
        Console.WriteLine($"3D XYZ Affine Solve golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyKnownAffine()
    {
        var source = StandardPairs();
        var output = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput(
            "step.affine.01", "derived.affine.01", source, 1000, 1e-10));
        var matrix = output.Output?.Matrix;
        var expected = new[] { 2d, 0.5, -0.25, 10d, -1d, 3d, 0.75, 20d, 0.2, -0.3, 4d, 30d };
        var actual = matrix?.Values ?? [];
        var mapped = output.Output?.Transform(2, -1, 3) ?? default;
        var expectedMapped = (X: 12.75, Y: 17.25, Z: 42.7);
        var pass = output.Result.Status == ResultStatus.Pass
            && output.Output is not null
            && actual.Count == expected.Length
            && actual.Zip(expected).All(pair => Nearly(pair.First, pair.Second))
            && Nearly(mapped.X, expectedMapped.X)
            && Nearly(mapped.Y, expectedMapped.Y)
            && Nearly(mapped.Z, expectedMapped.Z)
            && output.Output.ArithmeticMaximumResidual <= 1e-12
            && Nearly(output.Output.SourceAugmentedDeterminant, -1d)
            && Nearly(output.Output.LinearDeterminantAbsolute, 26.6d);
        return (pass, $"status={output.Result.Status};matrix={string.Join(',', actual.Select(value => value.ToString("G8")))};mapped={mapped.X:G8},{mapped.Y:G8},{mapped.Z:G8};maxResidual={output.Output?.ArithmeticMaximumResidual:G8}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminismAndAdapter()
    {
        var correspondence = StandardPairs();
        var one = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput("step.affine.01", "derived.affine.01", correspondence, 1000, 1e-10));
        var two = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput("step.affine.01", "derived.affine.01", correspondence, 1000, 1e-10));
        var recipe = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Affine solve fixture",
            new ToolRecipeSource("source.synthetic", "Synthetic", "C3D", "raw-height", "frame.source", "fixture.c3d", 1, new string('A', 64), 1, 1),
            [],
            [
                new ToolRecipeStep("step.correspondence.01", "fixture-correspondence", "Fixture correspondence", 1, ["source.synthetic"], correspondence.OutputEntityId, []),
                new ToolRecipeStep("step.affine.01", "xyz-affine-solve", "XYZ Affine Solve", 1, [correspondence.OutputEntityId], "derived.affine.01",
                    [new("SolvePolicy", "ExactFourPartialPivot"), new("MaximumConditionEstimate", "1000"), new("ArithmeticResidualWarning", "0.0000000001")])
            ],
            []);
        var prepared = ToolRecipeXYZAffineSolveExecution.TryPrepare(recipe, "step.affine.01", correspondence, out var input, out var message);
        var adapter = ToolRecipeXYZAffineSolveExecution.Execute(recipe, "step.affine.01", correspondence);
        var pass = one.Output is not null && two.Output is not null && adapter.Output is not null
            && one.Output.ContentSha256 == two.Output.ContentSha256
            && one.Output.ContentSha256 == adapter.Output.ContentSha256
            && prepared && input is not null && adapter.Result.Status == ResultStatus.Pass;
        return (pass, $"prepared={prepared}:{message};one={one.Output?.ContentSha256};two={two.Output?.ContentSha256};adapter={adapter.Output?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyConditionAndInvalidInput()
    {
        var illConditioned = CreateCorrespondence(
        [
            Pair("a", 0, 0, 0), Pair("b", 1, 0, 0), Pair("c", 0, 1, 0), Pair("d", 0, 0, 1e-8)
        ]);
        var condition = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput("step.affine.01", "derived.affine.01", illConditioned, 1e6, 0));
        var invalidMaximum = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput("step.affine.01", "derived.affine.01", StandardPairs(), 0, 0));
        var warning = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput("step.affine.01", "derived.affine.01", StandardPairs(), 1000, -1));
        var pass = condition.Result.Status == ResultStatus.Error
            && condition.Result.Message.Contains("condition estimate", StringComparison.Ordinal)
            && invalidMaximum.Result.Status == ResultStatus.Error
            && warning.Result.Status == ResultStatus.Error;
        return (pass, $"condition={condition.Result.Status}:{condition.Result.Message};maximum={invalidMaximum.Result.Status}:{invalidMaximum.Result.Message};warning={warning.Result.Status}:{warning.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyFivePairsAndCancellation()
    {
        var five = CreateCorrespondence(
        [
            Pair("a", 0, 0, 0), Pair("b", 1, 0, 0), Pair("c", 0, 1, 0), Pair("d", 0, 0, 1), Pair("e", 1, 1, 1)
        ]);
        var rejected = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput("step.affine.01", "derived.affine.01", five, 1000, 0));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput("step.affine.01", "derived.affine.01", StandardPairs(), 1000, 0), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        return (rejected.Result.Status == ResultStatus.Error && canceled, $"five={rejected.Result.Status}:{rejected.Result.Message};canceled={canceled}");
    }

    private static C3DLandmarkCorrespondenceSet StandardPairs() => CreateCorrespondence(
    [
        Pair("a", 0, 0, 0), Pair("b", 1, 0, 0), Pair("c", 0, 1, 0), Pair("d", 0, 0, 1)
    ]);

    private static C3DLandmarkCorrespondencePair Pair(string id, double x, double y, double z) =>
        new($"derived.corner.{id}", $"Corner{id.ToUpperInvariant()}", Hash(id), x, y, z, $"fixture.{id}",
            2 * x + 0.5 * y - 0.25 * z + 10,
            -x + 3 * y + 0.75 * z + 20,
            0.2 * x - 0.3 * y + 4 * z + 30);

    private static C3DLandmarkCorrespondenceSet CreateCorrespondence(IReadOnlyList<C3DLandmarkCorrespondencePair> pairs) =>
        C3DLandmarkCorrespondenceSet.Create(
            "derived.correspondences.01", pairs, "source.synthetic", new string('A', 64),
            "raw-height", "frame.source", "frame.fixture", "fixture-unit", "fixture.synthetic", "REV-1",
            1e-12, 4, 4, 0.1, 0.1, "synthetic correspondence");

    private static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
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
