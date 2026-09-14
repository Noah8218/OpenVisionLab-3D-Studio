using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DPointCloudVoxelDownsampleGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureDirectory = Path.Combine(
            reportDirectory,
            $"point-cloud-voxel-downsample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var cases = new[]
            {
                Check("first-representative-reduction-and-bounds", VerifyReductionAndBounds),
                Check("deterministic-output-and-evidence-identity", VerifyDeterminism),
                Check("explicit-option-identity-and-finite-guards", VerifyGuards),
                Check("runner-replay-and-direct-parity", () => VerifyRunnerParity(fixtureDirectory)),
                Check("runner-report-atomicity", () => VerifyRunnerReportAtomicity(fixtureDirectory)),
                Check("cancellation-propagation", VerifyCancellation)
            };
            var passed = cases.Count(item => item.Passed);
            var status = passed == cases.Length ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DPointCloudVoxelDownsampleGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
                $"Definition|index={C3DPointCloudVoxelDownsampleEvidence.VoxelIndexPolicyName}|representative={C3DPointCloudVoxelDownsampleEvidence.RepresentativePolicyName}|order={C3DPointCloudVoxelDownsampleEvidence.OutputOrderPolicyName}|lineage={C3DPointCloudVoxelDownsampleEvidence.LineagePolicyName}|origin=inferred:false|alignment=excluded|interpolation=excluded|physical-calibration=not-claimed"
            };
            lines.AddRange(cases.Select(item =>
                $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllLines(fullReportPath, lines);
            Console.WriteLine(
                $"C3D point-cloud voxel-downsample golden verification: {status} ({passed}/{cases.Length})");
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

    private static (bool Passed, string Evidence) VerifyReductionAndBounds()
    {
        var source = CreateSource("source.point-cloud.voxel.reduction");
        var sourceBefore = source.Points.ToArray();
        var evaluation = Evaluate(source, "reduced.point-cloud.voxel.reduction");
        var outputPoints = evaluation.Output?.Points.ToArray() ?? [];
        var expected = new[]
        {
            new C3DPoint3(0.1, 0.1, 0.1),
            new C3DPoint3(1.0, 0.0, 0.0),
            new C3DPoint3(-0.1, 0.0, 0.0),
            new C3DPoint3(2.1, 2.1, 2.1)
        };
        var passed = IsPass(evaluation)
            && evaluation.Evidence!.VoxelIndexPolicy == C3DPointCloudVoxelDownsampleEvidence.VoxelIndexPolicyName
            && evaluation.Evidence.RepresentativePolicy == C3DPointCloudVoxelDownsampleEvidence.RepresentativePolicyName
            && evaluation.Evidence.OutputOrderPolicy == C3DPointCloudVoxelDownsampleEvidence.OutputOrderPolicyName
            && evaluation.Evidence.InputPointCount == 5
            && evaluation.Evidence.OutputPointCount == 4
            && evaluation.Evidence.ReducedPointCount == 1
            && evaluation.Evidence.RepresentativeSourceIndexes.SequenceEqual(new[] { 0, 2, 3, 4 })
            && outputPoints.SequenceEqual(expected)
            && Approximately(evaluation.Evidence.InputMinimumX, -0.1)
            && Approximately(evaluation.Evidence.OutputMaximumZ, 2.1)
            && evaluation.Output!.RootSourceSha256 == source.RootSourceSha256
            && evaluation.Output.Unit == source.Unit
            && evaluation.Output.FrameId == source.FrameId
            && evaluation.Output.CoordinateConvention == source.CoordinateConvention
            && source.Points.SequenceEqual(sourceBefore);
        return (
            passed,
            $"status={evaluation.Result.Status};input={evaluation.Evidence?.InputPointCount};output={evaluation.Evidence?.OutputPointCount};reduced={evaluation.Evidence?.ReducedPointCount};lineage={string.Join(',', evaluation.Evidence?.RepresentativeSourceIndexes ?? [])};points={string.Join(';', outputPoints.Select(FormatPoint))};sourceUnchanged={source.Points.SequenceEqual(sourceBefore)}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var first = Evaluate(
            CreateSource("source.point-cloud.voxel.determinism"),
            "reduced.point-cloud.voxel.determinism");
        var second = Evaluate(
            CreateSource("source.point-cloud.voxel.determinism"),
            "reduced.point-cloud.voxel.determinism");
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
        var source = CreateSource("source.point-cloud.voxel.guards");
        var zeroEdge = Evaluate(
            source,
            "reduced.point-cloud.voxel.zero-edge",
            voxelEdgeLength: 0d);
        var nonFiniteOrigin = Evaluate(
            source,
            "reduced.point-cloud.voxel.nan-origin",
            originX: double.NaN);
        var overflowingIndex = Evaluate(
            source,
            "reduced.point-cloud.voxel.overflow-index",
            voxelEdgeLength: 1e-300,
            originX: -1e300);
        var outputCollision = Evaluate(
            source,
            source.EntityId);
        var passed = zeroEdge.Result.Status == ResultStatus.Error
            && zeroEdge.Output is null
            && nonFiniteOrigin.Result.Status == ResultStatus.Error
            && nonFiniteOrigin.Output is null
            && overflowingIndex.Result.Status == ResultStatus.Error
            && overflowingIndex.Output is null
            && outputCollision.Result.Status == ResultStatus.Error
            && outputCollision.Output is null;
        return (
            passed,
            $"zeroEdge={zeroEdge.Result.Status};nanOrigin={nonFiniteOrigin.Result.Status};overflowIndex={overflowingIndex.Result.Status};collision={outputCollision.Result.Status}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerParity(string fixtureDirectory)
    {
        var source = CreateSource("source.point-cloud.voxel.runner");
        var direct = Evaluate(
            source,
            "reduced.point-cloud.voxel.runner",
            stepId: "step.point-cloud.voxel.runner");
        if (!IsPass(direct) || direct.Output is null || direct.Evidence is null)
        {
            return (false, $"direct={direct.Result.Status}:{direct.Result.Message}");
        }

        var specificationPath = Path.Combine(fixtureDirectory, "point-cloud-voxel-downsample.json");
        var outputPath = Path.Combine(fixtureDirectory, "reduced-point-cloud.json");
        var runnerReportPath = Path.Combine(fixtureDirectory, "runner-report.json");
        var specification = CreateSpecification(source, direct.Output.EntityId, outputPath);
        File.WriteAllText(
            specificationPath,
            JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }));
        var runnerExit = C3DPointCloudVoxelDownsampleRunnerExecution.Run(
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
        var runnerRepresentativeSourceIndexes = report?.RootElement.GetProperty("evidence")
            .GetProperty("representativeSourceIndexes")
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToArray();
        var status = report?.RootElement.GetProperty("result").GetProperty("status").GetString();
        var sourceMutation = report?.RootElement.GetProperty("sourceMutation").GetBoolean();
        var outputPointCount = outputDocument?.RootElement.GetProperty("output").GetProperty("points").GetArrayLength();
        var parity = runnerExit == 0
            && report is not null
            && outputDocument is not null
            && outputHash == direct.Output.ContentSha256
            && evidenceHash == direct.Evidence.ContentSha256
            && runnerRepresentativeSourceIndexes is not null
            && runnerRepresentativeSourceIndexes.SequenceEqual(direct.Evidence.RepresentativeSourceIndexes)
            && status == "Pass"
            && outputPointCount == direct.Output.ValidPointCount
            && sourceMutation == false;

        var collisionReportPath = Path.Combine(fixtureDirectory, "collision-report.txt");
        var collisionExit = C3DPointCloudVoxelDownsampleRunnerExecution.Run(
            specificationPath,
            collisionReportPath);
        var collisionRejected = collisionExit == 5
            && File.ReadAllText(collisionReportPath).Contains("already exists", StringComparison.OrdinalIgnoreCase);

        var invalidSpecification = CreateSpecification(
            source,
            "reduced.point-cloud.voxel.invalid",
            Path.Combine(fixtureDirectory, "invalid-output.json"));
        invalidSpecification.Source!.ContentSha256 = new string('0', 64);
        var invalidSpecificationPath = Path.Combine(fixtureDirectory, "invalid-specification.json");
        var invalidReportPath = Path.Combine(fixtureDirectory, "invalid-report.txt");
        File.WriteAllText(
            invalidSpecificationPath,
            JsonSerializer.Serialize(invalidSpecification, new JsonSerializerOptions { WriteIndented = true }));
        var invalidExit = C3DPointCloudVoxelDownsampleRunnerExecution.Run(
            invalidSpecificationPath,
            invalidReportPath);
        var identityRejected = invalidExit == 5
            && File.ReadAllText(invalidReportPath).Contains("identity", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(invalidSpecification.OutputPath);
        return (
            parity && collisionRejected && identityRejected,
            $"runnerExit={runnerExit};outputHash={outputHash};evidenceHash={evidenceHash};status={status};points={outputPointCount};sourceMutation={sourceMutation};collisionExit={collisionExit};collisionRejected={collisionRejected};identityExit={invalidExit};identityRejected={identityRejected}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerReportAtomicity(string fixtureDirectory)
    {
        var directory = Path.Combine(fixtureDirectory, $"atomic-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var source = CreateSource("source.point-cloud.voxel.atomic");
            var direct = Evaluate(
                source,
                "reduced.point-cloud.voxel.atomic",
                stepId: "step.point-cloud.voxel.atomic");
            if (!IsPass(direct) || direct.Output is null || direct.Evidence is null)
            {
                return (false, $"direct={direct.Result.Status}:{direct.Result.Message}");
            }

            var specificationPath = Path.Combine(directory, "point-cloud-voxel-downsample.json");
            var firstOutputPath = Path.Combine(directory, "reduced-point-cloud-first.json");
            var secondOutputPath = Path.Combine(directory, "reduced-point-cloud-second.json");
            var reportOutputPath = Path.Combine(directory, "runner-report.json");
            var specification = CreateSpecification(source, direct.Output.EntityId, firstOutputPath);
            specification.StepId = "step.point-cloud.voxel.atomic";
            File.WriteAllText(
                specificationPath,
                JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var firstExit = C3DPointCloudVoxelDownsampleRunnerExecution.Run(
                specificationPath,
                reportOutputPath);
            var firstBytes = File.Exists(reportOutputPath)
                ? File.ReadAllBytes(reportOutputPath)
                : [];
            using var firstDocument = File.Exists(reportOutputPath)
                ? JsonDocument.Parse(firstBytes)
                : null;
            var firstOutputHash = firstDocument?.RootElement.GetProperty("output").GetProperty("contentSha256").GetString();
            var firstEvidenceHash = firstDocument?.RootElement.GetProperty("evidence").GetProperty("contentSha256").GetString();
            var firstOutputBytes = File.Exists(firstOutputPath)
                ? File.ReadAllBytes(firstOutputPath)
                : [];

            specification.OutputPath = secondOutputPath;
            File.WriteAllText(
                specificationPath,
                JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(reportOutputPath, "pre-existing-output", new UTF8Encoding(false));
            var overwriteExit = C3DPointCloudVoxelDownsampleRunnerExecution.Run(
                specificationPath,
                reportOutputPath);
            var overwriteBytes = File.Exists(reportOutputPath)
                ? File.ReadAllBytes(reportOutputPath)
                : [];
            using var overwriteDocument = File.Exists(reportOutputPath)
                ? JsonDocument.Parse(overwriteBytes)
                : null;
            var overwriteOutputHash = overwriteDocument?.RootElement.GetProperty("output").GetProperty("contentSha256").GetString();
            var overwriteEvidenceHash = overwriteDocument?.RootElement.GetProperty("evidence").GetProperty("contentSha256").GetString();
            var overwriteOutputBytes = File.Exists(secondOutputPath)
                ? File.ReadAllBytes(secondOutputPath)
                : [];

            var lockedPath = Path.Combine(directory, "locked.json");
            var lockedSentinel = Encoding.UTF8.GetBytes("locked-output");
            File.WriteAllBytes(lockedPath, lockedSentinel);
            int lockedExit;
            using (var lockStream = new FileStream(
                       lockedPath,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                specification.OutputPath = Path.Combine(directory, "reduced-point-cloud-locked.json");
                File.WriteAllText(
                    specificationPath,
                    JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                lockedExit = C3DPointCloudVoxelDownsampleRunnerExecution.Run(
                    specificationPath,
                    lockedPath);
            }
            var lockedPreserved = File.ReadAllBytes(lockedPath).SequenceEqual(lockedSentinel);

            var invalidParentMarker = Path.Combine(directory, "parent-file");
            File.WriteAllText(invalidParentMarker, "parent-file", new UTF8Encoding(false));
            specification.OutputPath = Path.Combine(directory, "reduced-point-cloud-invalid-parent.json");
            File.WriteAllText(
                specificationPath,
                JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var invalidParentExit = C3DPointCloudVoxelDownsampleRunnerExecution.Run(
                specificationPath,
                Path.Combine(invalidParentMarker, "report.json"));
            var invalidParentPreserved = File.ReadAllText(invalidParentMarker) == "parent-file";
            var temporaryFilesRemain = Directory.GetFiles(directory, "*.json.tmp.*").Length != 0;
            var noBom = !HasUtf8Bom(overwriteBytes);
            var outputNoBom = !HasUtf8Bom(firstOutputBytes) && !HasUtf8Bom(overwriteOutputBytes);
            var sentinelAbsent = !Encoding.UTF8.GetString(overwriteBytes).Contains("pre-existing-output", StringComparison.Ordinal);
            var passed = firstExit == 0
                && overwriteExit == 0
                && firstBytes.Length > 0
                && overwriteBytes.Length > 0
                && firstOutputHash == direct.Output.ContentSha256
                && firstEvidenceHash == direct.Evidence.ContentSha256
                && overwriteOutputHash == firstOutputHash
                && overwriteEvidenceHash == firstEvidenceHash
                && noBom
                && outputNoBom
                && sentinelAbsent
                && lockedExit == 5
                && lockedPreserved
                && invalidParentExit == 5
                && invalidParentPreserved
                && !temporaryFilesRemain;
            return (
                passed,
                $"firstExit={firstExit};overwriteExit={overwriteExit};bytes={firstBytes.Length}/{overwriteBytes.Length};artifactBytes={firstOutputBytes.Length}/{overwriteOutputBytes.Length};hashes={firstOutputHash}/{overwriteOutputHash};evidence={firstEvidenceHash}/{overwriteEvidenceHash};noBom={noBom};artifactNoBom={outputNoBom};sentinelAbsent={sentinelAbsent};lockedExit={lockedExit};lockedPreserved={lockedPreserved};invalidParentExit={invalidParentExit};invalidParentPreserved={invalidParentPreserved};temporaryFiles={temporaryFilesRemain}");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static bool HasUtf8Bom(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF });

    private static (bool Passed, string Evidence) VerifyCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = Evaluate(
                CreateSource("source.point-cloud.voxel.cancel"),
                "reduced.point-cloud.voxel.cancel",
                cancellationToken: cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        return (canceled, $"canceled={canceled}");
    }

    private static C3DPointCloudVoxelDownsampleRunnerSpecification CreateSpecification(
        C3DPointCloudSnapshot source,
        string outputEntityId,
        string outputPath) =>
        new()
        {
            StepId = "step.point-cloud.voxel.runner",
            Source = ToRunnerSource(source),
            OutputEntityId = outputEntityId,
            OutputPath = outputPath,
            VoxelEdgeLength = 1.0,
            OriginX = 0.0,
            OriginY = 0.0,
            OriginZ = 0.0
        };

    private static C3DPointCloudVoxelDownsampleRunnerSource ToRunnerSource(
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
                .Select(point => new C3DPointCloudVoxelDownsampleRunnerPoint
                {
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z
                })
                .ToList()
        };

    private static C3DPointCloudVoxelDownsampleEvaluation Evaluate(
        C3DPointCloudSnapshot source,
        string outputEntityId,
        string stepId = "step.point-cloud.voxel.01",
        double voxelEdgeLength = 1.0,
        double originX = 0.0,
        double originY = 0.0,
        double originZ = 0.0,
        CancellationToken cancellationToken = default) =>
        C3DPointCloudVoxelDownsampleRule.Evaluate(
            new C3DPointCloudVoxelDownsampleInput(
                stepId,
                source,
                outputEntityId,
                voxelEdgeLength,
                originX,
                originY,
                originZ),
            cancellationToken);

    private static C3DPointCloudSnapshot CreateSource(string entityId) =>
        C3DPointCloudSnapshot.CreateForVerification(
            entityId,
            $"{entityId}.xyz",
            "verification-xyz",
            "mm",
            "frame.point-cloud.xyz",
            "XYZ-right-handed",
            [
                new C3DPoint3(0.1, 0.1, 0.1),
                new C3DPoint3(0.9, 0.2, 0.4),
                new C3DPoint3(1.0, 0.0, 0.0),
                new C3DPoint3(-0.1, 0.0, 0.0),
                new C3DPoint3(2.1, 2.1, 2.1)
            ]);

    private static bool IsPass(C3DPointCloudVoxelDownsampleEvaluation evaluation) =>
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
