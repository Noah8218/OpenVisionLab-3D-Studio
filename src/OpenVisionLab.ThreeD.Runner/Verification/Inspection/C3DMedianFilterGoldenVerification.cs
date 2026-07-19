using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DMedianFilterGoldenVerification
{
    public static int Run(string reportPath)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"ovl3d-filter-golden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var cases = new[]
            {
                Check("constant-preserved", VerifyConstant),
                Check("isolated-spike-removed", VerifySpike),
                Check("missing-center-preserved", VerifyMissingCenter),
                Check("missing-neighbors-ignored", VerifyMissingNeighbors),
                Check("border-available-neighbors", VerifyBorder),
                Check("finite-zero-output-rejected", VerifyFiniteZeroOutput),
                Check("kernel-3-5-7", VerifyKernelChoices),
                Check("invalid-kernel-controlled", VerifyInvalidKernel),
                Check("all-missing-controlled", VerifyAllMissing),
                Check("deterministic-output-hash", VerifyDeterminism),
                Check("unknown-parameter-preserved", () => VerifyStrictParameters(tempDirectory)),
                Check("same-byte-source-identity", () => VerifySourceIdentity(tempDirectory)),
                Check("recipe-adapter-output-roundtrip", () => VerifyRecipeAdapter(tempDirectory))
            };

            var passed = cases.Count(item => item.Passed);
            var status = passed == cases.Length ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DMedianFilterGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
                "Definition|raw-height-only|method=Median|kernels=3,5,7|missing=PreserveMask|boundary=AvailableNeighbors|roi=separate"
            };
            lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, lines);
            Console.WriteLine($"C3D Median Filter golden verification: {status} ({passed}/{cases.Length})");
            return passed == cases.Length ? 0 : 5;
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static (bool Passed, string Evidence) VerifyConstant()
    {
        var evaluation = Evaluate(3, 3, Enumerable.Repeat(4.25, 9).ToArray(), 3);
        return (evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Output!.Values.Span.ToArray().All(value => value == 4.25),
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifySpike()
    {
        var evaluation = Evaluate(3, 3, [1, 1, 1, 1, 100, 1, 1, 1, 1], 3);
        return (evaluation.Output is not null
            && evaluation.Output.Values.Span.ToArray().All(value => value == 1),
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyMissingCenter()
    {
        var evaluation = Evaluate(3, 3, [1, 2, 3, 4, double.NaN, 6, 7, 8, 9], 3);
        return (evaluation.Output is not null
            && double.IsNaN(evaluation.Output.Values.Span[4])
            && evaluation.Output.MissingCount == 1,
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyMissingNeighbors()
    {
        var evaluation = Evaluate(3, 1, [1, double.NaN, 5], 3);
        var output = evaluation.Output!.Values.Span;
        return (output[0] == 1 && double.IsNaN(output[1]) && output[2] == 5, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyBorder()
    {
        var evaluation = Evaluate(2, 2, [1, 2, 3, 4], 3);
        return (evaluation.Output!.Values.Span.ToArray().All(value => value == 2.5), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyFiniteZeroOutput()
    {
        var evaluation = Evaluate(2, 1, [-1, 1], 3);
        return (evaluation.Result.Status == ResultStatus.Error
            && evaluation.Output is null
            && evaluation.Result.Message.Contains("finite zero", StringComparison.OrdinalIgnoreCase),
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyKernelChoices()
    {
        var source = Enumerable.Range(1, 49).Select(value => (double)value).ToArray();
        var hashes = new[] { 3, 5, 7 }
            .Select(kernel => Evaluate(7, 7, source, kernel))
            .Select(evaluation => evaluation.Output?.ContentSha256)
            .ToArray();
        return (hashes.All(hash => hash?.Length == 64), string.Join(",", hashes));
    }

    private static (bool Passed, string Evidence) VerifyInvalidKernel()
    {
        var evaluation = Evaluate(2, 2, [1, 2, 3, 4], 9);
        return (evaluation.Result.Status == ResultStatus.Error && evaluation.Output is null, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyAllMissing()
    {
        var evaluation = Evaluate(2, 2, [double.NaN, double.NaN, double.NaN, double.NaN], 3);
        return (evaluation.Result.Status == ResultStatus.Error && evaluation.Output is null, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var values = new double[] { 1, 2, 9, 4, 5, 6, 7, 8, double.NaN };
        var first = Evaluate(3, 3, values, 5).Output!;
        var second = Evaluate(3, 3, values, 5).Output!;
        return (first.ContentSha256 == second.ContentSha256, $"first={first.ContentSha256},second={second.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyStrictParameters(string tempDirectory)
    {
        var document = CreateRecipe(tempDirectory);
        var invalid = document with
        {
            Steps = [document.Steps[0] with
            {
                Parameters = [.. document.Steps[0].Parameters, new ToolRecipeParameter("Sigma", "1")]
            }]
        };
        var result = ToolRecipeFilterExecution.Execute(invalid, invalid.Steps[0].Id, tempDirectory);
        return (result.Result.Status == ResultStatus.Pass && result.Output is not null, result.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifySourceIdentity(string tempDirectory)
    {
        var document = CreateRecipe(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, document.Source.Path);
        using (var stream = File.Open(sourcePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            stream.Position = stream.Length - 1;
            var original = stream.ReadByte();
            stream.Position = stream.Length - 1;
            stream.WriteByte((byte)(original ^ 0x01));
        }

        var result = ToolRecipeFilterExecution.Execute(document, document.Steps[0].Id, tempDirectory);
        return (result.Result.Status == ResultStatus.Error && result.Result.Message.Contains("identity", StringComparison.OrdinalIgnoreCase), result.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyRecipeAdapter(string tempDirectory)
    {
        var document = CreateRecipe(tempDirectory);
        var evaluation = ToolRecipeFilterExecution.Execute(document, document.Steps[0].Id, tempDirectory);
        if (evaluation.Output is null)
        {
            return (false, evaluation.Result.Message);
        }

        var outputPath = Path.Combine(tempDirectory, "output.c3d");
        evaluation.Output.SaveC3D(outputPath);
        var reloaded = C3DHeightFieldSnapshot.LoadVerified(
            outputPath,
            evaluation.Output.EntityId,
            evaluation.Output.Unit,
            evaluation.Output.FrameId,
            evaluation.Output.ByteLength,
            evaluation.Output.ContentSha256,
            evaluation.Output.Width,
            evaluation.Output.Height);
        return (reloaded.ContentSha256 == evaluation.Output.ContentSha256
            && reloaded.Values.Span.SequenceEqual(evaluation.Output.Values.Span),
            Evidence(evaluation));
    }

    private static ToolRecipeDocument CreateRecipe(string tempDirectory)
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.synthetic",
            3,
            3,
            [1, 1, 1, 1, 9, 1, 1, 1, 1]);
        var fileName = $"source-{Guid.NewGuid():N}.c3d";
        source.SaveC3D(Path.Combine(tempDirectory, fileName));
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Filter Golden",
            new ToolRecipeSource(
                source.EntityId,
                "Synthetic",
                "C3D",
                source.Unit,
                source.FrameId,
                fileName,
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            [new ToolRecipeStep(
                "step.filter.01",
                "filter",
                "Filter",
                1,
                [source.EntityId],
                "derived.filtered.01",
                [
                    new("Method", "Median"),
                    new("KernelSize", "3"),
                    new("MissingValuePolicy", "PreserveMask"),
                    new("BoundaryPolicy", "AvailableNeighbors")
                ])],
            []);
    }

    private static C3DMedianFilterEvaluation Evaluate(int width, int height, IReadOnlyList<double> values, int kernel) =>
        C3DMedianFilterRule.Evaluate(new C3DMedianFilterInput(
            "step.filter.01",
            C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", width, height, values),
            "derived.filtered.01",
            kernel));

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

    private static string Evidence(C3DMedianFilterEvaluation evaluation) =>
        $"status={evaluation.Result.Status},valid={evaluation.Output?.ValidCount},missing={evaluation.Output?.MissingCount},hash={evaluation.Output?.ContentSha256},message={evaluation.Result.Message}";

    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
