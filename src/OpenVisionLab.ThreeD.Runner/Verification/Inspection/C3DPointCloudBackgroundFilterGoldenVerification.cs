using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DPointCloudBackgroundFilterGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureDirectory = Path.Combine(
            reportDirectory,
            $"point-cloud-background-filter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var cases = new[]
            {
                Check("nearest-distance-filter-and-order", VerifyNearestDistanceFilter),
                Check("deterministic-output-and-evidence-identity", VerifyDeterminism),
                Check("identity-metadata-threshold-and-finite-guards", VerifyGuards),
                Check("runner-replay-and-direct-parity", () => VerifyRunnerParity(fixtureDirectory)),
                Check("all-removed-warning-and-cancellation", VerifyWarningAndCancellation)
            };
            var passed = cases.Count(item => item.Passed);
            var status = passed == cases.Length ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DPointCloudBackgroundFilterGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
                $"Definition|distance={C3DPointCloudBackgroundFilterEvidence.DistancePolicyName}|removal={C3DPointCloudBackgroundFilterEvidence.RemovalPolicyName}|matching={C3DPointCloudBackgroundFilterEvidence.MatchingPolicyName}|lineage={C3DPointCloudBackgroundFilterEvidence.LineagePolicyName}|algorithm=deterministic-O(NxM)|physical-calibration=not-claimed"
            };
            lines.AddRange(cases.Select(item =>
                $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath, lines);
            Console.WriteLine(
                $"C3D point-cloud background-filter golden verification: {status} ({passed}/{cases.Length})");
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

    private static (bool Passed, string Evidence) VerifyNearestDistanceFilter()
    {
        var current = CreateCurrent("current.point-cloud.filter");
        var background = CreateBackground("background.point-cloud.saved");
        var currentBefore = current.Points.ToArray();
        var backgroundBefore = background.Points.ToArray();
        var evaluation = Evaluate(current, background, "filtered.point-cloud.01");
        var retained = evaluation.Output?.Points.ToArray() ?? [];
        var passed = IsPass(evaluation)
            && evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Evidence!.DistancePolicy == C3DPointCloudBackgroundFilterEvidence.DistancePolicyName
            && evaluation.Evidence.RemovalPolicy == C3DPointCloudBackgroundFilterEvidence.RemovalPolicyName
            && evaluation.Evidence.MatchingPolicy == C3DPointCloudBackgroundFilterEvidence.MatchingPolicyName
            && evaluation.Evidence.InputPointCount == 4
            && evaluation.Evidence.BackgroundPointCount == 2
            && evaluation.Evidence.RetainedPointCount == 2
            && evaluation.Evidence.RemovedPointCount == 2
            && Approximately(evaluation.Evidence.MinimumNearestBackgroundDistance, 0.0)
            && Approximately(evaluation.Evidence.MaximumNearestBackgroundDistance, 2.0)
            && Approximately(evaluation.Evidence.MeanNearestBackgroundDistance, 0.85)
            && retained.SequenceEqual(new[] { new C3DPoint3(2.0, 0.0, 0.0), new C3DPoint3(5.0, 0.0, 0.0) })
            && evaluation.Output!.RootSourceSha256 == current.RootSourceSha256
            && evaluation.Output.Unit == current.Unit
            && evaluation.Output.FrameId == current.FrameId
            && evaluation.Output.CoordinateConvention == current.CoordinateConvention
            && current.Points.SequenceEqual(currentBefore)
            && background.Points.SequenceEqual(backgroundBefore);
        return (
            passed,
            $"status={evaluation.Result.Status};retained={string.Join(';', retained.Select(FormatPoint))};removed={evaluation.Evidence?.RemovedPointCount};distance={evaluation.Evidence?.MinimumNearestBackgroundDistance:R}..{evaluation.Evidence?.MaximumNearestBackgroundDistance:R};mean={evaluation.Evidence?.MeanNearestBackgroundDistance:R};sourceUnchanged={current.Points.SequenceEqual(currentBefore) && background.Points.SequenceEqual(backgroundBefore)}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var first = Evaluate(
            CreateCurrent("current.point-cloud.determinism"),
            CreateBackground("background.point-cloud.determinism"),
            "filtered.point-cloud.determinism");
        var second = Evaluate(
            CreateCurrent("current.point-cloud.determinism"),
            CreateBackground("background.point-cloud.determinism"),
            "filtered.point-cloud.determinism");
        var passed = IsPass(first)
            && IsPass(second)
            && first.Output!.ContentSha256 == second.Output!.ContentSha256
            && first.Evidence!.ContentSha256 == second.Evidence!.ContentSha256
            && first.Output.Provenance == second.Output.Provenance;
        return (
            passed,
            $"outputFirst={first.Output?.ContentSha256};outputSecond={second.Output?.ContentSha256};evidenceFirst={first.Evidence?.ContentSha256};evidenceSecond={second.Evidence?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyGuards()
    {
        var current = CreateCurrent("current.point-cloud.guards");
        var background = CreateBackground("background.point-cloud.guards");
        var wrongUnit = Evaluate(
            current,
            C3DPointCloudSnapshot.CreateForVerification(
                "background.point-cloud.wrong-unit",
                "background-wrong-unit.xyz",
                "verification-xyz",
                "inch",
                current.FrameId,
                current.CoordinateConvention,
                background.Points),
            "filtered.point-cloud.wrong-unit");
        var wrongFrame = Evaluate(
            current,
            C3DPointCloudSnapshot.CreateForVerification(
                "background.point-cloud.wrong-frame",
                "background-wrong-frame.xyz",
                "verification-xyz",
                current.Unit,
                "frame.other",
                current.CoordinateConvention,
                background.Points),
            "filtered.point-cloud.wrong-frame");
        var wrongConvention = Evaluate(
            current,
            C3DPointCloudSnapshot.CreateForVerification(
                "background.point-cloud.wrong-convention",
                "background-wrong-convention.xyz",
                "verification-xyz",
                current.Unit,
                current.FrameId,
                "XYZ-left-handed",
                background.Points),
            "filtered.point-cloud.wrong-convention");
        var invalidThreshold = Evaluate(
            current,
            background,
            "filtered.point-cloud.invalid-threshold",
            maximumBackgroundDistance: double.NaN);
        var negativeThreshold = Evaluate(
            current,
            background,
            "filtered.point-cloud.negative-threshold",
            maximumBackgroundDistance: -1d);
        var outputCollision = Evaluate(
            current,
            background,
            current.EntityId);
        var sameInputIdentity = Evaluate(
            current,
            C3DPointCloudSnapshot.CreateForVerification(
                current.EntityId,
                "background-same-id.xyz",
                background.SourceFormat,
                background.Unit,
                background.FrameId,
                background.CoordinateConvention,
                background.Points),
            "filtered.point-cloud.same-input-id");
        var nonFiniteRejected = false;
        try
        {
            _ = C3DPointCloudSnapshot.CreateForVerification(
                "invalid.point-cloud.nonfinite",
                "invalid.xyz",
                "verification-xyz",
                current.Unit,
                current.FrameId,
                current.CoordinateConvention,
                [new C3DPoint3(double.NaN, 0d, 0d)]);
        }
        catch (InvalidDataException)
        {
            nonFiniteRejected = true;
        }

        var passed = wrongUnit.Result.Status == ResultStatus.Error
            && wrongUnit.Output is null
            && wrongFrame.Result.Status == ResultStatus.Error
            && wrongFrame.Output is null
            && wrongConvention.Result.Status == ResultStatus.Error
            && wrongConvention.Output is null
            && invalidThreshold.Result.Status == ResultStatus.Error
            && invalidThreshold.Output is null
            && negativeThreshold.Result.Status == ResultStatus.Error
            && negativeThreshold.Output is null
            && outputCollision.Result.Status == ResultStatus.Error
            && outputCollision.Output is null
            && sameInputIdentity.Result.Status == ResultStatus.Error
            && sameInputIdentity.Output is null
            && nonFiniteRejected;
        return (
            passed,
            $"unit={wrongUnit.Result.Status};frame={wrongFrame.Result.Status};convention={wrongConvention.Result.Status};nan={invalidThreshold.Result.Status};negative={negativeThreshold.Result.Status};collision={outputCollision.Result.Status};sameInput={sameInputIdentity.Result.Status};nonFiniteRejected={nonFiniteRejected}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerParity(string fixtureDirectory)
    {
        var current = CreateCurrent("current.point-cloud.runner");
        var background = CreateBackground("background.point-cloud.runner");
        var direct = Evaluate(current, background, "filtered.point-cloud.runner", "step.point-cloud.runner");
        if (!IsPass(direct) || direct.Output is null || direct.Evidence is null)
        {
            return (false, $"direct={direct.Result.Status}:{direct.Result.Message}");
        }

        var specificationPath = Path.Combine(fixtureDirectory, "point-cloud-background-filter.json");
        var outputPath = Path.Combine(fixtureDirectory, "filtered-point-cloud.json");
        var runnerReportPath = Path.Combine(fixtureDirectory, "runner-report.json");
        var specification = CreateSpecification(current, background, direct.Output.EntityId, outputPath);
        File.WriteAllText(
            specificationPath,
            JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }));
        var runnerExit = C3DPointCloudBackgroundFilterRunnerExecution.Run(
            specificationPath,
            runnerReportPath);
        using var report = File.Exists(runnerReportPath)
            ? JsonDocument.Parse(File.ReadAllText(runnerReportPath))
            : null;
        using var outputDocument = File.Exists(outputPath)
            ? JsonDocument.Parse(File.ReadAllText(outputPath))
            : null;
        var outputHash = report?.RootElement.GetProperty("output").GetProperty("contentSha256").GetString();
        var evidenceHash = report?.RootElement.GetProperty("evidence").GetProperty("contentSha256").GetString();
        var status = report?.RootElement.GetProperty("result").GetProperty("status").GetString();
        var currentMutation = report?.RootElement.GetProperty("currentMutation").GetBoolean();
        var backgroundMutation = report?.RootElement.GetProperty("backgroundMutation").GetBoolean();
        var outputPointCount = outputDocument?.RootElement.GetProperty("output").GetProperty("points").GetArrayLength();
        var parity = runnerExit == 0
            && report is not null
            && outputDocument is not null
            && outputHash == direct.Output.ContentSha256
            && evidenceHash == direct.Evidence.ContentSha256
            && status == "Pass"
            && outputPointCount == direct.Output.ValidPointCount
            && currentMutation == false
            && backgroundMutation == false;

        var collisionReportPath = Path.Combine(fixtureDirectory, "collision-report.txt");
        var collisionExit = C3DPointCloudBackgroundFilterRunnerExecution.Run(
            specificationPath,
            collisionReportPath);
        var collisionRejected = collisionExit == 5
            && File.ReadAllText(collisionReportPath).Contains("already exists", StringComparison.OrdinalIgnoreCase);

        var invalidSpecification = CreateSpecification(current, background, "filtered.point-cloud.invalid", Path.Combine(fixtureDirectory, "invalid-output.json"));
        invalidSpecification.SavedBackground!.ContentSha256 = new string('0', 64);
        var invalidSpecificationPath = Path.Combine(fixtureDirectory, "invalid-specification.json");
        var invalidReportPath = Path.Combine(fixtureDirectory, "invalid-report.txt");
        File.WriteAllText(
            invalidSpecificationPath,
            JsonSerializer.Serialize(invalidSpecification, new JsonSerializerOptions { WriteIndented = true }));
        var invalidExit = C3DPointCloudBackgroundFilterRunnerExecution.Run(
            invalidSpecificationPath,
            invalidReportPath);
        var identityRejected = invalidExit == 5
            && File.ReadAllText(invalidReportPath).Contains("identity", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(invalidSpecification.OutputPath);
        return (
            parity && collisionRejected && identityRejected,
            $"runnerExit={runnerExit};outputHash={outputHash};evidenceHash={evidenceHash};status={status};points={outputPointCount};mutations={currentMutation}/{backgroundMutation};collisionExit={collisionExit};collisionRejected={collisionRejected};identityExit={invalidExit};identityRejected={identityRejected}");
    }

    private static (bool Passed, string Evidence) VerifyWarningAndCancellation()
    {
        var warning = Evaluate(
            CreateCurrent("current.point-cloud.warning"),
            CreateBackground("background.point-cloud.warning"),
            "filtered.point-cloud.warning",
            maximumBackgroundDistance: 100d);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = Evaluate(
                CreateCurrent("current.point-cloud.cancel"),
                CreateBackground("background.point-cloud.cancel"),
                "filtered.point-cloud.cancel",
                cancellationToken: cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        var passed = warning.Result.Status == ResultStatus.Warning
            && warning.Output is not null
            && warning.Output.ValidPointCount == 0
            && warning.Evidence is not null
            && !warning.Evidence.HasRetainedPoints
            && canceled;
        return (
            passed,
            $"warning={warning.Result.Status};outputPoints={warning.Output?.ValidPointCount};canceled={canceled}");
    }

    private static C3DPointCloudBackgroundFilterRunnerSpecification CreateSpecification(
        C3DPointCloudSnapshot current,
        C3DPointCloudSnapshot background,
        string outputEntityId,
        string outputPath) =>
        new()
        {
            StepId = "step.point-cloud.runner",
            Current = ToRunnerSource(current),
            SavedBackground = ToRunnerSource(background),
            OutputEntityId = outputEntityId,
            OutputPath = outputPath,
            MaximumBackgroundDistance = 0.5
        };

    private static C3DPointCloudBackgroundFilterRunnerSource ToRunnerSource(
        C3DPointCloudSnapshot source) =>
        new()
        {
            Path = source.SourcePath,
            EntityId = source.EntityId,
            SourceFormat = source.SourceFormat,
            Unit = source.Unit,
            FrameId = source.FrameId,
            CoordinateConvention = source.CoordinateConvention,
            ByteLength = source.ByteLength,
            ContentSha256 = source.ContentSha256,
            RootSourceSha256 = source.RootSourceSha256,
            Points = source.Points
                .Select(point => new C3DPointCloudBackgroundFilterRunnerPoint
                {
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z
                })
                .ToList()
        };

    private static C3DPointCloudBackgroundFilterEvaluation Evaluate(
        C3DPointCloudSnapshot current,
        C3DPointCloudSnapshot background,
        string outputEntityId,
        string stepId = "step.point-cloud.01",
        double maximumBackgroundDistance = 0.5,
        CancellationToken cancellationToken = default) =>
        C3DPointCloudBackgroundFilterRule.Evaluate(
            new C3DPointCloudBackgroundFilterInput(
                stepId,
                current,
                background,
                outputEntityId,
                maximumBackgroundDistance),
            cancellationToken);

    private static C3DPointCloudSnapshot CreateCurrent(string entityId) =>
        C3DPointCloudSnapshot.CreateForVerification(
            entityId,
            $"{entityId}.xyz",
            "verification-xyz",
            "mm",
            "frame.point-cloud.xyz",
            "XYZ-right-handed",
            [
                new C3DPoint3(0d, 0d, 0d),
                new C3DPoint3(0.4d, 0d, 0d),
                new C3DPoint3(2d, 0d, 0d),
                new C3DPoint3(5d, 0d, 0d)
            ]);

    private static C3DPointCloudSnapshot CreateBackground(string entityId) =>
        C3DPointCloudSnapshot.CreateForVerification(
            entityId,
            $"{entityId}.xyz",
            "verification-xyz",
            "mm",
            "frame.point-cloud.xyz",
            "XYZ-right-handed",
            [
                new C3DPoint3(0d, 0d, 0d),
                new C3DPoint3(3d, 0d, 0d)
            ]);

    private static bool IsPass(C3DPointCloudBackgroundFilterEvaluation evaluation) =>
        evaluation.Result.Status == ResultStatus.Pass
        && evaluation.Output is not null
        && evaluation.Evidence is not null;

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string FormatPoint(C3DPoint3 point) =>
        $"({point.X:R},{point.Y:R},{point.Z:R})";

    private static string Clean(string evidence) =>
        evidence.Replace(Environment.NewLine, " ", StringComparison.Ordinal)
            .Replace('|', '/');

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        Func<(bool Passed, string Evidence)> verify)
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
}
