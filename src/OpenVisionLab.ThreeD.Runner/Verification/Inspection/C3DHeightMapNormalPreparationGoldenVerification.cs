using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DHeightMapNormalPreparationGoldenVerification
{
    private static readonly double PlaneNormalX = -2d / Math.Sqrt(14d);
    private static readonly double PlaneNormalY = 1d / Math.Sqrt(14d);
    private static readonly double PlaneNormalZ = -3d / Math.Sqrt(14d);

    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureDirectory = Path.Combine(reportDirectory, $"height-map-normal-preparation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var cases = new[]
            {
                Check("analytic-plane-central-and-boundary-differences", VerifyAnalyticPlane),
                Check("missing-neighbor-is-unavailable-with-warning", VerifyMissingNeighbor),
                Check("reversed-expected-normal-fails-validation", VerifyReversedValidation),
                Check("runner-replay-and-direct-parity", () => VerifyRunnerParity(fixtureDirectory)),
                Check("invalid-input-and-cancellation-fail-closed", VerifyGuardsAndCancellation)
            };
            var passed = cases.Count(item => item.Passed);
            var status = passed == cases.Length ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DHeightMapNormalPreparationGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
                "Definition|source=raw-height-regular-grid|derivative=central-interior-one-sided-finite-boundary|missing=unavailable-no-finite-neighbor-no-interpolation|validation=explicit-expected-normal-cosine-and-angle|source-mutation=false|physical-calibration=not-claimed"
            };
            lines.AddRange(cases.Select(item =>
                $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath, lines);
            Console.WriteLine($"C3D Height-Map Normal Preparation golden verification: {status} ({passed}/{cases.Length})");
            return passed == cases.Length ? 0 : 5;
        }
        finally
        {
            if (Directory.Exists(fixtureDirectory))
            {
                Directory.Delete(fixtureDirectory, true);
            }
        }
    }

    private static (bool Passed, string Evidence) VerifyAnalyticPlane()
    {
        var source = CreatePlaneFixture("source.normal.plane");
        var before = source.Values.ToArray();
        var evaluation = Evaluate(source, "derived.normal.plane");
        var evidence = evaluation.Evidence;
        var center = evidence?.Samples.SingleOrDefault(sample => sample.Row == 1 && sample.Column == 1);
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evidence is not null
            && evidence.RowCount == 3
            && evidence.ColumnCount == 3
            && evidence.InputFiniteSampleCount == 9
            && evidence.CalculatedNormalCount == 9
            && evidence.UnavailableNormalCount == 0
            && evidence.CentralDerivativeCount == 6
            && evidence.OneSidedDerivativeCount == 12
            && evidence.MissingDerivativeCount == 0
            && evidence.Samples.Count == 9
            && center is not null
            && Nearly(center.NormalX, PlaneNormalX)
            && Nearly(center.NormalY, PlaneNormalY)
            && Nearly(center.NormalZ, PlaneNormalZ)
            && center.CentralColumnDerivative
            && center.CentralRowDerivative
            && evidence.ValidationState == C3DHeightMapNormalValidationState.NotRequested
            && source.Values.Span.SequenceEqual(before)
            && evidence.SourceEntityId == source.EntityId
            && evidence.OutputEntityId == "derived.normal.plane"
            && evidence.SourceContentSha256 == source.ContentSha256
            && evidence.OutputRootSourceSha256 == source.RootSourceSha256;
        return (passed, $"status={evaluation.Result.Status};calculated={evidence?.CalculatedNormalCount};central={evidence?.CentralDerivativeCount};oneSided={evidence?.OneSidedDerivativeCount};sourceUnchanged={source.Values.Span.SequenceEqual(before)};output={evidence?.OutputContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyMissingNeighbor()
    {
        var values = PlaneValues();
        values[1 * 3 + 1] = double.NaN;
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.normal.missing",
            3,
            3,
            values,
            "mm",
            "frame.normal");
        var before = source.Values.ToArray();
        var evaluation = Evaluate(source, "derived.normal.missing");
        var evidence = evaluation.Evidence;
        var passed = evaluation.Result.Status == ResultStatus.Warning
            && evidence is not null
            && evidence.InputFiniteSampleCount == 8
            && evidence.CalculatedNormalCount > 0
            && evidence.UnavailableNormalCount > 0
            && evidence.MissingDerivativeCount > 0
            && evidence.Samples.All(sample => !(sample.Row == 1 && sample.Column == 1))
            && source.Values.Span.SequenceEqual(before);
        return (passed, $"status={evaluation.Result.Status};finite={evidence?.InputFiniteSampleCount};calculated={evidence?.CalculatedNormalCount};unavailable={evidence?.UnavailableNormalCount};missingDerivatives={evidence?.MissingDerivativeCount};sourceUnchanged={source.Values.Span.SequenceEqual(before)}");
    }

    private static (bool Passed, string Evidence) VerifyReversedValidation()
    {
        var source = CreatePlaneFixture("source.normal.reversed");
        var evaluation = Evaluate(
            source,
            "derived.normal.reversed",
            new C3DHeightMapNormalValidationOptions(
                -PlaneNormalX,
                -PlaneNormalY,
                -PlaneNormalZ));
        var evidence = evaluation.Evidence;
        var passed = evaluation.Result.Status == ResultStatus.Fail
            && evidence is not null
            && evidence.ValidationState == C3DHeightMapNormalValidationState.Failed
            && evidence.ValidatedNormalCount == evidence.CalculatedNormalCount
            && evidence.ConsistentNormalCount == 0
            && evidence.ReversedNormalCount == evidence.CalculatedNormalCount
            && evidence.MinimumAlignment.HasValue
            && evidence.MinimumAlignment.Value < -0.99
            && evidence.MaximumAngularErrorDegrees.HasValue
            && evidence.MaximumAngularErrorDegrees.Value > 170d;
        return (passed, $"status={evaluation.Result.Status};validation={evidence?.ValidationState};validated={evidence?.ValidatedNormalCount};consistent={evidence?.ConsistentNormalCount};reversed={evidence?.ReversedNormalCount};minimumAlignment={evidence?.MinimumAlignment};maxAngle={evidence?.MaximumAngularErrorDegrees}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerParity(string fixtureDirectory)
    {
        var source = CreatePlaneFixture("source.normal.runner");
        var direct = C3DHeightMapNormalPreparationRule.Evaluate(
            new C3DHeightMapNormalPreparationInput(
                "step.normal.runner",
                source,
                "derived.normal.runner",
                new C3DHeightMapNormalValidationOptions(PlaneNormalX, PlaneNormalY, PlaneNormalZ)));
        var specificationPath = Path.Combine(fixtureDirectory, "normal-preparation-spec.json");
        var runnerReportPath = Path.Combine(fixtureDirectory, "normal-preparation-report.json");
        var specification = new
        {
            stepId = "step.normal.runner",
            outputEntityId = "derived.normal.runner",
            source = new
            {
                entityId = source.EntityId,
                width = source.Width,
                height = source.Height,
                unit = source.Unit,
                frameId = source.FrameId,
                byteLength = source.ByteLength,
                contentSha256 = source.ContentSha256,
                rootSourceSha256 = source.RootSourceSha256,
                values = source.Values.ToArray()
            },
            validation = new
            {
                expectedNormalX = PlaneNormalX,
                expectedNormalY = PlaneNormalY,
                expectedNormalZ = PlaneNormalZ,
                minimumAlignmentCosine = 0.999
            }
        };
        File.WriteAllText(specificationPath, JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }));
        var runnerExitCode = C3DHeightMapNormalPreparationRunnerExecution.Run(specificationPath, runnerReportPath);
        using var document = JsonDocument.Parse(File.ReadAllText(runnerReportPath));
        var root = document.RootElement;
        var runnerEvidence = root.GetProperty("evidence");
        var runnerResult = root.GetProperty("result");
        var directHash = direct.Evidence?.ContentSha256;
        var runnerHash = runnerEvidence.GetProperty("contentSha256").GetString();
        var runnerOutputHash = runnerEvidence.GetProperty("outputContentSha256").GetString();
        var directOutputHash = direct.Evidence?.OutputContentSha256;
        var parity = runnerExitCode == 0
            && direct.Result.Status == ResultStatus.Pass
            && runnerResult.GetProperty("status").GetString() == nameof(ResultStatus.Pass)
            && string.Equals(runnerHash, directHash, StringComparison.Ordinal)
            && string.Equals(runnerOutputHash, directOutputHash, StringComparison.Ordinal)
            && runnerEvidence.GetProperty("calculatedNormalCount").GetInt32() == direct.Evidence?.CalculatedNormalCount
            && root.GetProperty("sourceMutation").GetBoolean() == false;

        var invalidSpecification = new
        {
            stepId = specification.stepId,
            outputEntityId = specification.outputEntityId,
            source = new
            {
                entityId = specification.source.entityId,
                width = specification.source.width,
                height = specification.source.height,
                unit = specification.source.unit,
                frameId = specification.source.frameId,
                byteLength = specification.source.byteLength,
                contentSha256 = new string('0', 64),
                rootSourceSha256 = specification.source.rootSourceSha256,
                values = specification.source.values
            },
            validation = specification.validation
        };
        var invalidPath = Path.Combine(fixtureDirectory, "normal-preparation-invalid-spec.json");
        var invalidReportPath = Path.Combine(fixtureDirectory, "normal-preparation-invalid-report.txt");
        File.WriteAllText(invalidPath, JsonSerializer.Serialize(invalidSpecification));
        var invalidExitCode = C3DHeightMapNormalPreparationRunnerExecution.Run(invalidPath, invalidReportPath);
        var invalidRejected = invalidExitCode == 5
            && File.ReadAllText(invalidReportPath).Contains("byte identity", StringComparison.OrdinalIgnoreCase);
        return (parity && invalidRejected, $"runnerExit={runnerExitCode};invalidExit={invalidExitCode};directEvidence={directHash};runnerEvidence={runnerHash};directOutput={directOutputHash};runnerOutput={runnerOutputHash};invalidRejected={invalidRejected}");
    }

    private static (bool Passed, string Evidence) VerifyGuardsAndCancellation()
    {
        var invalidOptions = Evaluate(
            CreatePlaneFixture("source.normal.invalid-options"),
            "derived.normal.invalid-options",
            new C3DHeightMapNormalValidationOptions(0d, 0d, 0d));
        var noNeighbor = Evaluate(
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.normal.no-neighbor",
                1,
                1,
                [5d],
                "mm",
                "frame.normal"),
            "derived.normal.no-neighbor");
        var canceled = false;
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            _ = C3DHeightMapNormalPreparationRule.Evaluate(
                new C3DHeightMapNormalPreparationInput(
                    "step.normal.canceled",
                    CreatePlaneFixture("source.normal.canceled"),
                    "derived.normal.canceled"),
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        var passed = invalidOptions.Result.Status == ResultStatus.Error
            && invalidOptions.Evidence is null
            && noNeighbor.Result.Status == ResultStatus.Error
            && noNeighbor.Evidence is null
            && canceled;
        return (passed, $"invalidOptions={invalidOptions.Result.Status};noNeighbor={noNeighbor.Result.Status};cancellationPropagated={canceled}");
    }

    private static C3DHeightMapNormalPreparationEvaluation Evaluate(
        C3DHeightFieldSnapshot source,
        string outputEntityId,
        C3DHeightMapNormalValidationOptions? validation = null) =>
        C3DHeightMapNormalPreparationRule.Evaluate(
            new C3DHeightMapNormalPreparationInput(
                "step.normal.golden",
                source,
                outputEntityId,
                validation));

    private static C3DHeightFieldSnapshot CreatePlaneFixture(string entityId) =>
        C3DHeightFieldSnapshot.CreateForVerification(
            entityId,
            3,
            3,
            PlaneValues(),
            "mm",
            "frame.normal");

    private static double[] PlaneValues() =>
        Enumerable.Range(0, 3)
            .SelectMany(row => Enumerable.Range(0, 3).Select(column => 10d + (2d * column) + (3d * row)))
            .ToArray();

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        Func<(bool Passed, string Evidence)> action)
    {
        try
        {
            var result = action();
            return (name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return (name, false, $"exception={exception.GetType().Name}:{exception.Message}");
        }
    }

    private static bool Nearly(double actual, double expected) =>
        Math.Abs(actual - expected) <= 1e-12;

    private static string Clean(string value) =>
        value.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
}
