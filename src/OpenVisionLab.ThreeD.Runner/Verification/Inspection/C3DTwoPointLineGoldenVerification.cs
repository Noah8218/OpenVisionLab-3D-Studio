using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DTwoPointLineGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("exact-current-source-segment", VerifyExactCurrentSourceSegment),
            Check("ordered-picks-produce-distinct-hash", VerifyOrderedPicks),
            Check("missing-current-cell-fails-closed", VerifyMissingCell),
            Check("strict-adapter-policy-and-binding", VerifyStrictAdapter),
            Check("source-tamper-fails-closed", VerifySourceTamper),
            Check("runner-replay", VerifyRunnerReplay),
            Check("runner-report-atomicity", () => VerifyRunnerReportAtomicity(reportPath)),
            Check("cancellation-propagates", VerifyCancellation)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DTwoPointLineGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|numeric=X-column,Y-current-raw-height,Z-row|policy=OrderedPointsDefineSegment|source=verified-C3D"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(GetReportDirectory(reportPath)!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"3D 2-Point Line golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyExactCurrentSourceSegment()
    {
        using var fixture = Fixture.Create();
        var evaluation = ToolRecipeTwoPointLineExecution.Execute(fixture.Document, fixture.StepId, fixture.Root);
        var output = evaluation.Output;
        return (evaluation.Result.Status == ResultStatus.Pass && output is not null
            && output.FirstRow == 0 && output.FirstColumn == 0
            && output.SecondRow == 2 && output.SecondColumn == 1
            && Approximately(output.SegmentStartX, 0) && Approximately(output.SegmentStartY, 1) && Approximately(output.SegmentStartZ, 0)
            && Approximately(output.SegmentEndX, 1) && Approximately(output.SegmentEndY, 8) && Approximately(output.SegmentEndZ, 2)
            && Approximately(output.SegmentLength, Math.Sqrt(54)), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyOrderedPicks()
    {
        using var fixture = Fixture.Create();
        var normal = ToolRecipeTwoPointLineExecution.Execute(fixture.Document, fixture.StepId, fixture.Root);
        var selection = fixture.Document.Selections!.Single();
        var reversed = selection with { Points = [selection.Points![1], selection.Points[0]] };
        var reversedDocument = fixture.Document with { Selections = [reversed] };
        var reverse = ToolRecipeTwoPointLineExecution.Execute(reversedDocument, fixture.StepId, fixture.Root);
        return (normal.Output is not null && reverse.Output is not null
            && normal.Output.ContentSha256 != reverse.Output.ContentSha256
            && Approximately(normal.Output.SegmentLength, reverse.Output.SegmentLength)
            && Approximately(normal.Output.SegmentStartX, reverse.Output.SegmentEndX), $"normal={Evidence(normal)};reversed={Evidence(reverse)}");
    }

    private static (bool Passed, string Evidence) VerifyMissingCell()
    {
        var captured = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", 3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var current = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", 3, 3, [1, 2, 3, 4, 5, 6, 7, double.NaN, 9]);
        var selection = CreateSelection(captured, 0, 0, 2, 1) with
        {
            SourceBinding = new ToolRecipeSelectionSourceBinding("C3D", current.ContentSha256, current.Width, current.Height)
        };
        var evaluation = C3DTwoPointLineRule.Evaluate(new C3DTwoPointLineInput("step.line", current, selection, "derived.line", "FixtureA"));
        return (evaluation.Result.Status == ResultStatus.Error
            && evaluation.Result.Message.Contains("no current finite", StringComparison.OrdinalIgnoreCase), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyStrictAdapter()
    {
        using var fixture = Fixture.Create();
        var ready = ToolRecipeTwoPointLineExecution.TryPrepare(fixture.Document, fixture.StepId, fixture.Root, out _, out var readyMessage);
        var invalidPolicy = fixture.Document with
        {
            Steps = [fixture.Document.Steps[0] with { Parameters = [new ToolRecipeParameter("OutputRole", "FixtureEdgeA"), new ToolRecipeParameter("ConstructionPolicy", "Freehand")] }]
        };
        var rejectedPolicy = !ToolRecipeTwoPointLineExecution.TryPrepare(invalidPolicy, fixture.StepId, fixture.Root, out _, out var policyMessage);
        var wrongBinding = fixture.Document with { Selections = [fixture.Document.Selections![0] with { SourceBinding = fixture.Document.Selections[0].SourceBinding with { ContentSha256 = new string('F', 64) } }] };
        var rejectedBinding = !ToolRecipeTwoPointLineExecution.TryPrepare(wrongBinding, fixture.StepId, fixture.Root, out _, out var bindingMessage);
        return (ready && rejectedPolicy && rejectedBinding, $"ready={ready}:{readyMessage};policy={policyMessage};binding={bindingMessage}");
    }

    private static (bool Passed, string Evidence) VerifySourceTamper()
    {
        using var fixture = Fixture.Create();
        File.WriteAllBytes(fixture.SourcePath, [1, 2, 3]);
        try
        {
            var evaluation = ToolRecipeTwoPointLineExecution.Execute(fixture.Document, fixture.StepId, fixture.Root);
            return (evaluation.Result.Status == ResultStatus.Error && evaluation.Result.Message.Contains("C3D", StringComparison.OrdinalIgnoreCase), Evidence(evaluation));
        }
        catch (C3DSourceTopologyException exception)
        {
            return (true, $"rejected={exception.Message}");
        }
    }

    private static (bool Passed, string Evidence) VerifyRunnerReplay()
    {
        using var fixture = Fixture.Create();
        var recipePath = Path.Combine(fixture.Root, "two-point-line.ov3d-teach.json");
        var reportPath = Path.Combine(fixture.Root, "runner.txt");
        ToolRecipeDocumentStore.Save(recipePath, fixture.Document);
        var expected = ToolRecipeTwoPointLineExecution.Execute(fixture.Document, fixture.StepId, fixture.Root);
        var exitCode = ToolRecipeTwoPointLineRunnerExecution.Run(recipePath, fixture.StepId, reportPath);
        var report = File.ReadAllText(reportPath);
        return (exitCode == 0 && expected.Output is not null && report.Contains(expected.Output.ContentSha256, StringComparison.Ordinal),
            $"exit={exitCode};hash={expected.Output?.ContentSha256};report={report.Replace(Environment.NewLine, ";")}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerReportAtomicity(string reportPath)
    {
        var reportDirectory = GetReportDirectory(reportPath) ?? Environment.CurrentDirectory;
        var directory = Path.Combine(reportDirectory, $"atomic-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var fixture = Fixture.Create(rootOverride: directory);
            var recipePath = Path.Combine(directory, "two-point-line.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, fixture.Document);
            var expected = ToolRecipeTwoPointLineExecution.Execute(fixture.Document, fixture.StepId, directory);
            if (expected.Result.Status != ResultStatus.Pass || expected.Output is null)
            {
                return (false, $"expected={expected.Result.Status}:{expected.Result.Message}");
            }

            var runnerReportPath = Path.Combine(directory, "runner.txt");
            var firstExit = ToolRecipeTwoPointLineRunnerExecution.Run(
                recipePath,
                fixture.StepId,
                runnerReportPath);
            var firstBytes = File.Exists(runnerReportPath) ? File.ReadAllBytes(runnerReportPath) : [];
            var firstText = Encoding.UTF8.GetString(firstBytes);

            File.WriteAllText(runnerReportPath, "pre-existing-output", new UTF8Encoding(false));
            var overwriteExit = ToolRecipeTwoPointLineRunnerExecution.Run(
                recipePath,
                fixture.StepId,
                runnerReportPath);
            var overwriteBytes = File.Exists(runnerReportPath) ? File.ReadAllBytes(runnerReportPath) : [];
            var overwriteText = Encoding.UTF8.GetString(overwriteBytes);

            var lockedPath = Path.Combine(directory, "locked.txt");
            var lockedSentinel = Encoding.UTF8.GetBytes("locked-output");
            File.WriteAllBytes(lockedPath, lockedSentinel);
            int lockedExit;
            using (var lockStream = new FileStream(
                       lockedPath,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                lockedExit = ToolRecipeTwoPointLineRunnerExecution.Run(
                    recipePath,
                    fixture.StepId,
                    lockedPath);
            }
            var lockedPreserved = File.ReadAllBytes(lockedPath).SequenceEqual(lockedSentinel);

            var invalidParentMarker = Path.Combine(directory, "parent-file");
            File.WriteAllText(invalidParentMarker, "parent-file", new UTF8Encoding(false));
            var invalidParentExit = ToolRecipeTwoPointLineRunnerExecution.Run(
                recipePath,
                fixture.StepId,
                Path.Combine(invalidParentMarker, "report.txt"));
            var invalidParentPreserved = File.ReadAllText(invalidParentMarker) == "parent-file";
            var temporaryFilesRemain = Directory.GetFiles(directory, "*.txt.tmp.*").Length != 0;
            var noBom = !HasUtf8Bom(overwriteBytes);
            var sentinelAbsent = !overwriteText.Contains("pre-existing-output", StringComparison.Ordinal);
            var reportShape = firstText.Contains("TwoPointLine|status=Pass", StringComparison.Ordinal)
                && firstText.Contains(expected.Output.ContentSha256, StringComparison.Ordinal)
                && overwriteText.Contains("TwoPointLine|status=Pass", StringComparison.Ordinal)
                && overwriteText.Contains(expected.Output.ContentSha256, StringComparison.Ordinal)
                && firstBytes.SequenceEqual(overwriteBytes);
            var passed = firstExit == 0
                && overwriteExit == 0
                && firstBytes.Length > 0
                && reportShape
                && noBom
                && sentinelAbsent
                && lockedExit == 5
                && lockedPreserved
                && invalidParentExit == 5
                && invalidParentPreserved
                && !temporaryFilesRemain;
            return (
                passed,
                $"firstExit={firstExit};overwriteExit={overwriteExit};bytes={firstBytes.Length}/{overwriteBytes.Length};reportShape={reportShape};byteStable={firstBytes.SequenceEqual(overwriteBytes)};noBom={noBom};sentinelAbsent={sentinelAbsent};lockedExit={lockedExit};lockedPreserved={lockedPreserved};invalidParentExit={invalidParentExit};invalidParentPreserved={invalidParentPreserved};temporaryFiles={temporaryFilesRemain}");
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
        var source = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", 2, 2, [1, 2, 3, 4]);
        var selection = CreateSelection(source, 0, 0, 1, 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            _ = C3DTwoPointLineRule.Evaluate(new C3DTwoPointLineInput("step.line", source, selection, "derived.line", "FixtureA"), cancellation.Token);
            return (false, "no cancellation thrown");
        }
        catch (OperationCanceledException)
        {
            return (true, "OperationCanceledException");
        }
    }

    private static ToolRecipeSelection CreateSelection(C3DHeightFieldSnapshot source, int firstRow, int firstColumn, int secondRow, int secondColumn) => new(
        "selection.line.01", "Fixture line picks", ToolRecipeSelectionKinds.PointSet, source.EntityId, source.FrameId,
        new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), null,
        [
            CreatePoint(source, firstRow, firstColumn),
            CreatePoint(source, secondRow, secondColumn)
        ], null);

    private static ToolRecipeSelectionPoint CreatePoint(C3DHeightFieldSnapshot source, int row, int column)
    {
        var height = source.Values.Span[row * source.Width + column];
        return new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", row, column), new ToolRecipeXyz(column, height, row), height);
    }

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) => double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;
    private static string Evidence(C3DTwoPointLineEvaluation evaluation) => $"status={evaluation.Result.Status};hash={evaluation.Output?.ContentSha256};length={evaluation.Output?.SegmentLength};message={evaluation.Result.Message}";
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private static string? GetReportDirectory(string reportPath) => Path.GetDirectoryName(Path.GetFullPath(reportPath));
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

        public static Fixture Create(IReadOnlyList<double>? values = null, string? rootOverride = null)
        {
            var root = rootOverride ?? Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "TwoPointLine", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification("source.c3d.height-map", 3, 3, values ?? [1, 2, 3, 4, 5, 6, 7, 8, 9]);
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var selection = CreateSelection(source, 0, 0, 2, 1);
            var stepId = "step.two-point-line.01";
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Two point line fixture",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [new ToolRecipeStep(stepId, "two-point-line", "2-Point Line", 1, [source.EntityId, selection.Id], "derived.fixture-line.01", [new ToolRecipeParameter("OutputRole", "FixtureEdgeA"), new ToolRecipeParameter("ConstructionPolicy", "OrderedPointsDefineSegment")])],
                [selection]);
            return new Fixture(root, sourcePath, stepId, document);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
