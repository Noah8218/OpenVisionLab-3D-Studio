using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DSignedDeltaBackgroundSubtractionGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureDirectory = Path.Combine(reportDirectory, $"signed-delta-background-subtraction-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var checks = new[]
            {
                Check("direct-signed-delta-preserves-missing-and-sign", VerifyDirectSignedDelta),
                Check("finite-zero-remains-valid-not-missing", VerifyFiniteZero),
                Check("recipe-background-identity-fails-closed", () => VerifyStaleIdentity(fixtureDirectory)),
                Check("ordered-run-save-reopen-and-run-record-parity", () => VerifyOrderedParity(fixtureDirectory))
            };
            var passed = checks.Count(item => item.Passed);
            var lines = new List<string>
            {
                $"C3DSignedDeltaBackgroundSubtractionGoldenVerification|{(passed == checks.Length ? "PASS" : "FAIL")}|cases={checks.Length}|passed={passed}|failed={checks.Length - passed}",
                $"Definition|tool={C3DSignedHeightDeltaRecipePolicy.ToolId}|subtraction={C3DSignedHeightDeltaRecipePolicy.SubtractionPolicyName}|missing={C3DSignedHeightDeltaRecipePolicy.MissingValuePolicyName}|zero={C3DSignedHeightDeltaRecipePolicy.ZeroDeltaPolicyName}|c3d-wire-zero=not-used",
            };
            lines.AddRange(checks.Select(item => $"Case|{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllLines(fullReportPath, lines);
            Console.WriteLine($"C3D signed-delta background subtraction verification: {(passed == checks.Length ? "PASS" : "FAIL")} ({passed}/{checks.Length})");
            return passed == checks.Length ? 0 : 5;
        }
        finally
        {
            if (Directory.Exists(fixtureDirectory))
            {
                Directory.Delete(fixtureDirectory, true);
            }
        }
    }

    private static (bool Passed, string Evidence) VerifyDirectSignedDelta()
    {
        var current = C3DHeightFieldSnapshot.CreateForVerification(
            "current.signed.delta",
            3,
            2,
            [5d, 5d, double.NaN, 10d, 8d, 4d]);
        var background = C3DHeightFieldSnapshot.CreateForVerification(
            "background.signed.delta",
            3,
            2,
            [2d, 2d, 7d, double.NaN, 5d, 5d]);
        var evaluation = C3DHeightBackgroundSubtractionRule.EvaluateSignedDelta(
            new C3DHeightBackgroundSubtractionInput("step.signed.delta", current, background, "output.signed.delta"));
        var expected = new[] { 3d, 3d, double.NaN, double.NaN, 3d, -1d };
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Output is { } output
            && output.TryValidate(out _)
            && output.MissingEitherSampleCount == 2
            && output.PairedValidSampleCount == 4
            && output.ZeroDeltaSampleCount == 0
            && output.PositiveDeltaSampleCount == 3
            && output.NegativeDeltaSampleCount == 1
            && SameValues(output.Values, expected)
            && output.CurrentSourceEntityId == current.EntityId
            && output.BackgroundEntityId == background.EntityId;
        return (passed, $"status={evaluation.Result.Status};hash={evaluation.Output?.ContentSha256};missing={evaluation.Output?.MissingEitherSampleCount};positive={evaluation.Output?.PositiveDeltaSampleCount};negative={evaluation.Output?.NegativeDeltaSampleCount}");
    }

    private static (bool Passed, string Evidence) VerifyFiniteZero()
    {
        var current = C3DHeightFieldSnapshot.CreateForVerification("current.signed.zero", 2, 1, [7d, 9d]);
        var background = C3DHeightFieldSnapshot.CreateForVerification("background.signed.zero", 2, 1, [7d, 4d]);
        var evaluation = C3DHeightBackgroundSubtractionRule.EvaluateSignedDelta(
            new C3DHeightBackgroundSubtractionInput("step.signed.zero", current, background, "output.signed.zero"));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Output is { } output
            && output.TryValidate(out _)
            && output.ZeroDeltaSampleCount == 1
            && output.MissingEitherSampleCount == 0
            && output.Values[0] == 0d
            && !double.IsNaN(output.Values[0]);
        return (passed, $"status={evaluation.Result.Status};zeroCount={evaluation.Output?.ZeroDeltaSampleCount};value0={evaluation.Output?.Values.FirstOrDefault():R};missing={evaluation.Output?.MissingEitherSampleCount}");
    }

    private static (bool Passed, string Evidence) VerifyStaleIdentity(string fixtureDirectory)
    {
        var current = C3DHeightFieldSnapshot.CreateForVerification("current.signed.stale", 2, 1, [5d, 6d]);
        var background = C3DHeightFieldSnapshot.CreateForVerification("background.signed.stale", 2, 1, [2d, 3d]);
        var currentPath = Path.Combine(fixtureDirectory, "stale-current.c3d");
        var backgroundPath = Path.Combine(fixtureDirectory, "stale-background.c3d");
        current.SaveC3D(currentPath);
        background.SaveC3D(backgroundPath);
        var recipe = CreateRecipe(current, background, currentPath, backgroundPath);
        var staleParameters = recipe.Steps[0].Parameters
            .Select(parameter => parameter.Name == C3DSignedHeightDeltaRecipePolicy.BackgroundContentSha256ParameterName
                ? parameter with { Value = new string('0', 64) }
                : parameter)
            .ToArray();
        var staleRecipe = recipe with { Steps = [recipe.Steps[0] with { Parameters = staleParameters }] };
        var result = ToolRecipeSignedDeltaBackgroundSubtractionExecution.Execute(staleRecipe, recipe.Steps[0].Id, current, fixtureDirectory);
        var passed = result.Result.Status == ResultStatus.Error
            && result.Output is null
            && (result.Result.Message.Contains("parameters", StringComparison.OrdinalIgnoreCase)
                || result.Result.Message.Contains("identity", StringComparison.OrdinalIgnoreCase));
        return (passed, $"status={result.Result.Status};message={result.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyOrderedParity(string fixtureDirectory)
    {
        var current = C3DHeightFieldSnapshot.CreateForVerification("source.signed.ordered", 3, 2, [5d, 5d, double.NaN, 10d, 8d, 4d]);
        var background = C3DHeightFieldSnapshot.CreateForVerification("background.signed.ordered", 3, 2, [2d, 2d, 7d, double.NaN, 5d, 5d]);
        var currentPath = Path.Combine(fixtureDirectory, "current.c3d");
        var backgroundPath = Path.Combine(fixtureDirectory, "background.c3d");
        current.SaveC3D(currentPath);
        background.SaveC3D(backgroundPath);
        var recipe = CreateRecipe(current, background, Path.GetFileName(currentPath), Path.GetFileName(backgroundPath));
        var recipePath = Path.Combine(fixtureDirectory, "signed-delta.recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, recipe);
        var reopened = ToolRecipeDocumentStore.Load(recipePath);
        var first = ToolRecipeOrderedGraphExecution.Execute(reopened, currentPath);
        var second = ToolRecipeOrderedGraphExecution.Execute(reopened, currentPath);
        var firstStep = first.Steps.SingleOrDefault(step => step.StepId == "step.signed.delta");
        var secondStep = second.Steps.SingleOrDefault(step => step.StepId == "step.signed.delta");
        var firstRecord = first.Status is ResultStatus.Pass or ResultStatus.Fail
            ? ToolRecipeOrderedGraphRunRecordProjection.Create(reopened, first).Single(step => step.Id == "step.signed.delta")
            : null;
        var passed = first.Status == ResultStatus.Pass
            && second.Status == ResultStatus.Pass
            && firstStep?.SignedDeltaBackgroundSubtraction is { } firstOutput
            && secondStep?.SignedDeltaBackgroundSubtraction is { } secondOutput
            && firstOutput.ContentSha256 == secondOutput.ContentSha256
            && firstStep.OutputContentSha256 == firstOutput.ContentSha256
            && firstRecord?.SignedDeltaBackgroundSubtraction?.ContentSha256 == firstOutput.ContentSha256
            && firstOutput.Values[0] == 3d
            && firstOutput.Values[2] is double value && double.IsNaN(value);
        return (passed, $"first={first.Status};second={second.Status};output={firstStep?.OutputContentSha256};record={firstRecord?.SignedDeltaBackgroundSubtraction?.ContentSha256};zero={firstStep?.SignedDeltaBackgroundSubtraction?.ZeroDeltaSampleCount};missing={firstStep?.SignedDeltaBackgroundSubtraction?.MissingEitherSampleCount}");
    }

    private static ToolRecipeDocument CreateRecipe(
        C3DHeightFieldSnapshot current,
        C3DHeightFieldSnapshot background,
        string currentPath,
        string backgroundPath)
    {
        var source = new ToolRecipeSource(
            current.EntityId,
            "Signed Delta Source",
            "C3D",
            current.Unit,
            current.FrameId,
            currentPath,
            current.ByteLength,
            current.ContentSha256,
            current.Width,
            current.Height);
        var reference = new C3DSignedHeightDeltaReference(
            backgroundPath,
            background.EntityId,
            background.ByteLength,
            background.ContentSha256,
            background.Width,
            background.Height,
            background.Unit,
            background.FrameId);
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Signed Delta Background Subtraction",
            source,
            [],
            [new ToolRecipeStep(
                "step.signed.delta",
                C3DSignedHeightDeltaRecipePolicy.ToolId,
                "Signed Delta Background Subtraction",
                1,
                [current.EntityId],
                "output.signed.delta",
                C3DSignedHeightDeltaRecipePolicy.ToParameters(reference))]);
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        Func<(bool Passed, string Evidence)> check)
    {
        try
        {
            var result = check();
            return (name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return (name, false, $"exception={exception.GetType().Name}:{exception.Message}");
        }
    }

    private static bool SameValues(IReadOnlyList<double> actual, IReadOnlyList<double> expected)
    {
        if (actual.Count != expected.Count) return false;
        for (var index = 0; index < actual.Count; index++)
        {
            if (double.IsNaN(actual[index]) || double.IsNaN(expected[index]))
            {
                if (!double.IsNaN(actual[index]) || !double.IsNaN(expected[index])) return false;
            }
            else if (actual[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }
}
