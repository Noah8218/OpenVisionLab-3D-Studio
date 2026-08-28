using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DRegionGrowingComponentGoldenVerification
{
    private const int Width = 5;
    private const int Height = 4;

    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureDirectory = Path.Combine(
            reportDirectory,
            $"region-growing-component-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var checks = new[]
            {
                Check("selected-component-projection-and-order", VerifySelectedComponent),
                Check("deterministic-output-and-evidence-identity", VerifyDeterminism),
                Check("identity-metadata-index-and-artifact-guards", VerifyGuards),
                Check("runner-replay-and-direct-parity", () => VerifyRunnerParity(fixtureDirectory)),
                Check("empty-component-warning-and-cancellation", VerifyWarningAndCancellation)
            };
            var passed = checks.Count(item => item.Passed);
            var status = passed == checks.Length ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DRegionGrowingComponentGoldenVerification|{status}|cases={checks.Length}|passed={passed}|failed={checks.Length - passed}",
                $"Definition|mode={C3DRegionGrowingComponentMode.SelectConnectedRegion}|projection={C3DRegionGrowingComponentEvidence.ProjectionPolicyName}|missing={C3DRegionGrowingComponentEvidence.MissingValuePolicyName}|lineage={C3DRegionGrowingComponentEvidence.LineagePolicyName}|coordinate={C3DRegionGrowingComponentEvidence.CoordinatePolicyName}|sdk=HeightMapDomainMaskTool|physical-calibration=not-claimed"
            };
            lines.AddRange(checks.Select(item =>
                $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath, lines);
            Console.WriteLine($"C3D region-growing component golden verification: {status} ({passed}/{checks.Length})");
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

    private static (bool Passed, string Evidence) VerifySelectedComponent()
    {
        var source = CreateSource("source.region-growing.selected");
        var artifact = CreateArtifact(source, "connected.region-growing.selected");
        var beforeValues = source.Values.ToArray();
        var beforeArtifactHash = artifact.ContentSha256;
        var evaluation = Evaluate(source, artifact, 0, "component.region-growing.selected");
        var output = evaluation.Output;
        var selectedCells = artifact.Regions[0].Cells
            .Select(cell => (cell.Row, cell.Column))
            .ToHashSet();
        var expected = source.Values.ToArray();
        var reducedCount = 0;
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                if (!selectedCells.Contains((row, column)))
                {
                    if (double.IsFinite(expected[row * Width + column]))
                    {
                        reducedCount++;
                    }

                    expected[row * Width + column] = double.NaN;
                }
            }
        }

        var passed = evaluation.Result.Status == ResultStatus.Pass
            && output is not null
            && evaluation.Evidence is not null
            && output.IsDerived
            && output.RootSourceSha256 == source.RootSourceSha256
            && output.ContentSha256 != source.ContentSha256
            && evaluation.Evidence.SelectedRegionIndex == 0
            && evaluation.Evidence.SelectedCellCount == selectedCells.Count
            && evaluation.Evidence.InputValidSampleCount == source.ValidCount
            && evaluation.Evidence.InputMissingSampleCount == source.MissingCount
            && evaluation.Evidence.RetainedValidSampleCount == selectedCells.Count
            && evaluation.Evidence.ReducedBackgroundSampleCount == reducedCount
            && SameValues(output.Values.Span, expected)
            && source.Values.Span.SequenceEqual(beforeValues)
            && artifact.ContentSha256 == beforeArtifactHash;
        return (
            passed,
            $"status={evaluation.Result.Status};region={evaluation.Evidence?.SelectedRegionIndex};cells={evaluation.Evidence?.SelectedCellCount};retained={evaluation.Evidence?.RetainedValidSampleCount};reduced={evaluation.Evidence?.ReducedBackgroundSampleCount};output={output?.ContentSha256};sourceUnchanged={source.Values.Span.SequenceEqual(beforeValues)};artifactUnchanged={artifact.ContentSha256 == beforeArtifactHash}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var firstSource = CreateSource("source.region-growing.determinism");
        var first = Evaluate(
            firstSource,
            CreateArtifact(firstSource, "connected.region-growing.determinism"),
            0,
            "component.region-growing.determinism");
        var secondSource = CreateSource("source.region-growing.determinism");
        var second = Evaluate(
            secondSource,
            CreateArtifact(secondSource, "connected.region-growing.determinism"),
            0,
            "component.region-growing.determinism");
        var passed = IsSuccessful(first)
            && IsSuccessful(second)
            && first.Output!.ContentSha256 == second.Output!.ContentSha256
            && first.Evidence!.ContentSha256 == second.Evidence!.ContentSha256
            && first.Output.Provenance == second.Output.Provenance;
        return (
            passed,
            $"outputFirst={first.Output?.ContentSha256};outputSecond={second.Output?.ContentSha256};evidenceFirst={first.Evidence?.ContentSha256};evidenceSecond={second.Evidence?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyGuards()
    {
        var source = CreateSource("source.region-growing.guards");
        var artifact = CreateArtifact(source, "connected.region-growing.guards");
        var invalidIndex = Evaluate(source, artifact, -1, "component.region-growing.invalid-index");
        var mismatchedIdentity = Evaluate(
            CreateSource("source.region-growing.other"),
            artifact,
            0,
            "component.region-growing.identity");
        var mismatchedContentArtifact = RehashArtifact(
            artifact with { SourceContentSha256 = new string('A', 64) });
        var mismatchedContent = Evaluate(
            source,
            mismatchedContentArtifact,
            0,
            "component.region-growing.content");
        var mismatchedRootArtifact = RehashArtifact(
            artifact with { RootSourceSha256 = new string('B', 64) });
        var mismatchedRoot = Evaluate(
            source,
            mismatchedRootArtifact,
            0,
            "component.region-growing.root");
        var wrongUnitSource = C3DHeightFieldSnapshot.CreateForVerification(
            source.EntityId,
            Width,
            Height,
            source.Values.ToArray(),
            "inch",
            source.FrameId);
        var wrongUnit = Evaluate(wrongUnitSource, artifact, 0, "component.region-growing.unit");
        var wrongFrameSource = C3DHeightFieldSnapshot.CreateForVerification(
            source.EntityId,
            Width,
            Height,
            source.Values.ToArray(),
            source.Unit,
            "frame.other");
        var wrongFrame = Evaluate(wrongFrameSource, artifact, 0, "component.region-growing.frame");
        var wrongGridSource = C3DHeightFieldSnapshot.CreateForVerification(
            source.EntityId,
            Width - 1,
            Height,
            source.Values.ToArray().Take((Width - 1) * Height).ToArray(),
            source.Unit,
            source.FrameId);
        var wrongGrid = Evaluate(wrongGridSource, artifact, 0, "component.region-growing.grid");
        var wrongConvention = Evaluate(
            source,
            RehashArtifact(artifact with { CoordinateConvention = "wrong-grid-convention" }),
            0,
            "component.region-growing.convention");
        var tamperedArtifact = artifact with { ContentSha256 = new string('0', 64) };
        var tampered = Evaluate(source, tamperedArtifact, 0, "component.region-growing.tampered");
        var outputCollision = Evaluate(source, artifact, 0, source.EntityId);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = Evaluate(source, artifact, 0, "component.region-growing.canceled", cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        var passed = invalidIndex.Result.Status == ResultStatus.Error
            && invalidIndex.Output is null
            && mismatchedIdentity.Result.Status == ResultStatus.Error
            && mismatchedIdentity.Output is null
            && mismatchedContent.Result.Status == ResultStatus.Error
            && mismatchedContent.Output is null
            && mismatchedRoot.Result.Status == ResultStatus.Error
            && mismatchedRoot.Output is null
            && wrongUnit.Result.Status == ResultStatus.Error
            && wrongUnit.Output is null
            && wrongFrame.Result.Status == ResultStatus.Error
            && wrongFrame.Output is null
            && wrongGrid.Result.Status == ResultStatus.Error
            && wrongGrid.Output is null
            && wrongConvention.Result.Status == ResultStatus.Error
            && wrongConvention.Output is null
            && tampered.Result.Status == ResultStatus.Error
            && tampered.Output is null
            && outputCollision.Result.Status == ResultStatus.Error
            && outputCollision.Output is null
            && canceled;
        return (
            passed,
            $"index={invalidIndex.Result.Status};identity={mismatchedIdentity.Result.Status};content={mismatchedContent.Result.Status};root={mismatchedRoot.Result.Status};unit={wrongUnit.Result.Status};frame={wrongFrame.Result.Status};grid={wrongGrid.Result.Status};convention={wrongConvention.Result.Status};tampered={tampered.Result.Status};collision={outputCollision.Result.Status};canceled={canceled}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerParity(string fixtureDirectory)
    {
        var sourceFixture = CreateSource("source.region-growing.runner");
        var sourcePath = Path.Combine(fixtureDirectory, "region-growing-source.c3d");
        sourceFixture.SaveC3D(sourcePath);
        var source = C3DHeightFieldSnapshot.LoadIdentified(
            sourcePath,
            sourceFixture.EntityId,
            sourceFixture.Unit,
            sourceFixture.FrameId);
        var artifact = CreateArtifact(source, "connected.region-growing.runner");
        var artifactPath = Path.Combine(fixtureDirectory, "connected-region.json");
        C3DConnectedRegionArtifactStore.Save(artifactPath, artifact);
        var direct = Evaluate(
            source,
            artifact,
            0,
            "component.region-growing.runner",
            stepId: "step.region-growing.runner");
        if (!IsSuccessful(direct))
        {
            return (false, $"direct={direct.Result.Status}:{direct.Result.Message}");
        }

        var specificationPath = Path.Combine(fixtureDirectory, "region-growing-component.json");
        var outputPath = Path.Combine(fixtureDirectory, "region-growing-component.c3d");
        var runnerReportPath = Path.Combine(fixtureDirectory, "runner-report.json");
        var specification = CreateSpecification(source, artifact, artifactPath, outputPath);
        File.WriteAllText(
            specificationPath,
            JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }));
        var runnerExit = RunnerCommandRouter.Run(
            ["--region-growing-component-spec", specificationPath, "--report", runnerReportPath]);
        using var report = File.Exists(runnerReportPath)
            ? JsonDocument.Parse(File.ReadAllText(runnerReportPath))
            : null;
        var output = File.Exists(outputPath)
            ? C3DHeightFieldSnapshot.LoadIdentified(
                outputPath,
                "component.region-growing.runner",
                source.Unit,
                source.FrameId)
            : null;
        var outputHash = report?.RootElement.GetProperty("output").GetProperty("contentSha256").GetString();
        var evidenceHash = report?.RootElement.GetProperty("evidence").GetProperty("contentSha256").GetString();
        var status = report?.RootElement.GetProperty("result").GetProperty("status").GetString();
        var sourceMutation = report?.RootElement.GetProperty("sourceMutation").GetBoolean();
        var connectedMutation = report?.RootElement.GetProperty("connectedRegionMutation").GetBoolean();
        var parity = runnerExit == 0
            && report is not null
            && output is not null
            && outputHash == direct.Output!.ContentSha256
            && evidenceHash == direct.Evidence!.ContentSha256
            && status == "Pass"
            && output.ContentSha256 == direct.Output!.ContentSha256
            && sourceMutation == false
            && connectedMutation == false;

        var collisionSpecification = CloneSpecification(
            specification,
            outputPath: sourcePath);
        var collisionSpecificationPath = Path.Combine(fixtureDirectory, "collision-specification.json");
        File.WriteAllText(
            collisionSpecificationPath,
            JsonSerializer.Serialize(collisionSpecification, new JsonSerializerOptions { WriteIndented = true }));
        var collisionPath = Path.Combine(fixtureDirectory, "collision-report.txt");
        var collisionExit = RunnerCommandRouter.Run(
            ["--region-growing-component-spec", collisionSpecificationPath, "--report", collisionPath]);
        var collisionRejected = collisionExit == 5
            && File.ReadAllText(collisionPath).Contains("differ", StringComparison.OrdinalIgnoreCase)
            && collisionSpecification.OutputPath == sourcePath;

        var invalidOutputPath = Path.Combine(fixtureDirectory, "invalid-output.c3d");
        var invalidSpecification = CloneSpecification(
            specification,
            connectedRegionContentSha256: new string('0', 64),
            outputPath: invalidOutputPath);
        var invalidSpecificationPath = Path.Combine(fixtureDirectory, "invalid-specification.json");
        var invalidReportPath = Path.Combine(fixtureDirectory, "invalid-report.txt");
        File.WriteAllText(
            invalidSpecificationPath,
            JsonSerializer.Serialize(invalidSpecification, new JsonSerializerOptions { WriteIndented = true }));
        var invalidExit = RunnerCommandRouter.Run(
            ["--region-growing-component-spec", invalidSpecificationPath, "--report", invalidReportPath]);
        var identityRejected = invalidExit == 5
            && File.ReadAllText(invalidReportPath).Contains("identity", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(invalidSpecification.OutputPath);
        return (
            parity && collisionRejected && identityRejected,
            $"runnerExit={runnerExit};outputHash={outputHash};evidenceHash={evidenceHash};status={status};reopened={output?.ContentSha256 == direct.Output?.ContentSha256};mutations={sourceMutation}/{connectedMutation};collisionExit={collisionExit};collisionRejected={collisionRejected};identityExit={invalidExit};identityRejected={identityRejected}");
    }

    private static (bool Passed, string Evidence) VerifyWarningAndCancellation()
    {
        var values = Enumerable.Range(1, Width * Height).Select(value => (double)value).ToArray();
        values[1] = double.NaN;
        values[2] = double.NaN;
        values[6] = double.NaN;
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.region-growing.warning",
            Width,
            Height,
            values,
            "raw-height",
            "frame.region-growing");
        var artifact = CreateArtifact(source, "connected.region-growing.warning");
        var warning = Evaluate(source, artifact, 0, "component.region-growing.warning");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            var canceledSource = CreateSource("source.region-growing.cancel");
            _ = Evaluate(
                canceledSource,
                CreateArtifact(canceledSource, "connected.region-growing.cancel"),
                0,
                "component.region-growing.cancel",
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        var passed = warning.Result.Status == ResultStatus.Warning
            && warning.Output is not null
            && warning.Output.ValidCount == 0
            && warning.Evidence is not null
            && !warning.Evidence.HasFiniteComponent
            && canceled;
        return (
            passed,
            $"warning={warning.Result.Status};outputValid={warning.Output?.ValidCount};outputMissing={warning.Output?.MissingCount};canceled={canceled}");
    }

    private static C3DRegionGrowingComponentEvaluation Evaluate(
        C3DHeightFieldSnapshot source,
        C3DConnectedRegionArtifact artifact,
        int selectedRegionIndex,
        string outputEntityId,
        CancellationToken cancellationToken = default,
        string stepId = "step.region-growing.component") =>
        C3DRegionGrowingComponentRule.Evaluate(
            new C3DRegionGrowingComponentInput(
                stepId,
                source,
                artifact,
                selectedRegionIndex,
                outputEntityId),
            cancellationToken);

    private static C3DRegionGrowingComponentRunnerSpecification CreateSpecification(
        C3DHeightFieldSnapshot source,
        C3DConnectedRegionArtifact artifact,
        string artifactPath,
        string outputPath) =>
        new()
        {
            StepId = "step.region-growing.runner",
            Source = new C3DRegionGrowingComponentRunnerSource
            {
                Path = source.SourcePath,
                EntityId = source.EntityId,
                Unit = source.Unit,
                FrameId = source.FrameId,
                ByteLength = source.ByteLength,
                ContentSha256 = source.ContentSha256,
                RootSourceSha256 = source.RootSourceSha256,
                Width = source.Width,
                Height = source.Height
            },
            ConnectedRegionArtifactPath = artifactPath,
            ConnectedRegionArtifactId = artifact.ArtifactId,
            ConnectedRegionContentSha256 = artifact.ContentSha256,
            SelectedRegionIndex = 0,
            OutputEntityId = "component.region-growing.runner",
            OutputPath = outputPath
        };

    private static C3DRegionGrowingComponentRunnerSpecification CloneSpecification(
        C3DRegionGrowingComponentRunnerSpecification source,
        string? connectedRegionContentSha256 = null,
        string? outputPath = null) =>
        new()
        {
            StepId = source.StepId,
            Source = source.Source is null
                ? null
                : new C3DRegionGrowingComponentRunnerSource
                {
                    Path = source.Source.Path,
                    EntityId = source.Source.EntityId,
                    Unit = source.Source.Unit,
                    FrameId = source.Source.FrameId,
                    ByteLength = source.Source.ByteLength,
                    ContentSha256 = source.Source.ContentSha256,
                    RootSourceSha256 = source.Source.RootSourceSha256,
                    Width = source.Source.Width,
                    Height = source.Source.Height
                },
            ConnectedRegionArtifactPath = source.ConnectedRegionArtifactPath,
            ConnectedRegionArtifactId = source.ConnectedRegionArtifactId,
            ConnectedRegionContentSha256 = connectedRegionContentSha256 ?? source.ConnectedRegionContentSha256,
            SelectedRegionIndex = source.SelectedRegionIndex,
            OutputEntityId = source.OutputEntityId,
            OutputPath = outputPath ?? source.OutputPath
        };

    private static C3DHeightFieldSnapshot CreateSource(string entityId) =>
        C3DHeightFieldSnapshot.CreateForVerification(
            entityId,
            Width,
            Height,
            Enumerable.Range(1, Width * Height).Select(value => (double)value).ToArray(),
            "raw-height",
            "frame.region-growing");

    private static C3DConnectedRegionArtifact CreateArtifact(
        C3DHeightFieldSnapshot source,
        string artifactId)
    {
        var mask = C3DOutlierCellMap.Create(
            Width,
            Height,
            [1, 2, 6, 10, 19]);
        var analysis = C3DConnectedRegionAnalyzer.AnalyzeOutlierMask(
            mask,
            C3DConnectedRegionConnectivity.Four);
        return C3DConnectedRegionArtifactFactory.Create(
            artifactId,
            "Region-growing component fixture",
            source,
            mask,
            analysis,
            C3DConnectedRegionConnectivity.Four);
    }

    private static C3DConnectedRegionArtifact RehashArtifact(
        C3DConnectedRegionArtifact artifact) =>
        artifact with
        {
            ContentSha256 = C3DConnectedRegionArtifact.CalculateContentSha256(artifact)
        };

    private static bool IsSuccessful(C3DRegionGrowingComponentEvaluation evaluation) =>
        evaluation.Result.Status == ResultStatus.Pass
        && evaluation.Output is not null
        && evaluation.Evidence is not null;

    private static bool SameValues(
        ReadOnlySpan<double> actual,
        IReadOnlyList<double> expected)
    {
        if (actual.Length != expected.Count)
        {
            return false;
        }

        for (var index = 0; index < actual.Length; index++)
        {
            if (double.IsNaN(actual[index]) || double.IsNaN(expected[index]))
            {
                if (!double.IsNaN(actual[index]) || !double.IsNaN(expected[index]))
                {
                    return false;
                }
            }
            else if (actual[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

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

    private static string Clean(string evidence) =>
        evidence.Replace(Environment.NewLine, " ", StringComparison.Ordinal)
            .Replace('|', '/');
}
