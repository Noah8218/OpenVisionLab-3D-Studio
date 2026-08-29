using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DRegionTransformPropagationGoldenVerification
{
    private const int Width = 4;
    private const int Height = 3;
    private const string SourceUnit = "raw-height";
    private const string SourceFrame = "frame.c3d-grid-index";

    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        var fixtureDirectory = Path.Combine(
            reportDirectory,
            $"region-transform-propagation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var cases = new[]
            {
                Check("exact-region-membership-and-transform-propagation", VerifyPropagation),
                Check("deterministic-output-and-json-roundtrip", () => VerifyDeterminismAndPersistence(fixtureDirectory)),
                Check("source-region-transform-identity-guards", VerifyGuards),
                Check("all-missing-warning-and-cancellation", VerifyWarningAndCancellation)
            };
            var passed = cases.Count(item => item.Passed);
            var status = passed == cases.Length ? "Pass" : "Fail";
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"C3DRegionTransformPropagationGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
                    $"Definition|input=raw-C3D-plus-Published-ConnectedRegionArtifact-plus-Published-AffineTransform3D|output=TransformedRegionArtifact|policy={C3DTransformedRegionArtifact.TransformPolicyName}|missing={C3DTransformedRegionArtifact.MissingValuePolicyName}|regrid=excluded|measurement=excluded|physical-calibration=not-claimed",
                    .. cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
                ]);
            Console.WriteLine($"3D region transform propagation golden verification: {status} ({passed}/{cases.Length})");
            return passed == cases.Length ? 0 : 5;
        }
        finally
        {
            if (Directory.Exists(fixtureDirectory))
            {
                Directory.Delete(fixtureDirectory, recursive: true);
            }
        }
    }

    private static (bool Passed, string Evidence) VerifyPropagation()
    {
        var source = CreateSource("source.region-transform.propagation");
        var region = CreateRegionArtifact(source, "connected.region-transform.propagation", [
            new C3DConnectedRegionArtifactCell(0, 0),
            new C3DConnectedRegionArtifactCell(0, 1),
            new C3DConnectedRegionArtifactCell(0, 2)
        ]);
        var transform = CreatePublishedTransform(source);
        var sourceHash = source.ContentSha256;
        var sourceValues = source.Values.ToArray();
        var regionHash = region.ContentSha256;
        var evaluation = Evaluate(source, region, transform, 0, "derived.region-transform.propagation");
        var output = evaluation.Output;
        var expected = transform.Transform(0, 1, 0);
        var first = output?.Cells[0];
        var pass = evaluation.Result.Status == ResultStatus.Pass
            && output is not null
            && output.SourceRegionArtifactId == region.ArtifactId
            && output.SourceRegionContentSha256 == region.ContentSha256
            && output.SourceRegionIndex == 0
            && output.SourceEntityId == source.EntityId
            && output.SourceContentSha256 == source.ContentSha256
            && output.RootSourceSha256 == source.RootSourceSha256
            && output.TransformEntityId == transform.OutputEntityId
            && output.TransformContentSha256 == transform.ContentSha256
            && output.ReferenceFrameId == transform.ReferenceFrameId
            && output.CellCount == 3
            && output.FiniteCellCount == 2
            && output.MissingCellCount == 1
            && output.Cells.Select(cell => (cell.Row, cell.Column)).SequenceEqual(
                new[] { (0, 0), (0, 1), (0, 2) })
            && first is not null
            && first.HasFinitePoint
            && Nearly(first.X!.Value, expected.X)
            && Nearly(first.Y!.Value, expected.Y)
            && Nearly(first.Z!.Value, expected.Z)
            && output.Cells[2].RawHeight is null
            && output.Cells[2].X is null
            && source.ContentSha256 == sourceHash
            && source.Values.Span.SequenceEqual(sourceValues)
            && region.ContentSha256 == regionHash;
        return (
            pass,
            $"status={evaluation.Result.Status};cells={output?.CellCount};finite={output?.FiniteCellCount};missing={output?.MissingCellCount};first={first?.X:G8},{first?.Y:G8},{first?.Z:G8};sourceUnchanged={source.ContentSha256 == sourceHash};regionUnchanged={region.ContentSha256 == regionHash}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminismAndPersistence(
        string fixtureDirectory)
    {
        var source = CreateSource("source.region-transform.persistence");
        var region = CreateRegionArtifact(source, "connected.region-transform.persistence", [
            new C3DConnectedRegionArtifactCell(0, 0),
            new C3DConnectedRegionArtifactCell(0, 1),
            new C3DConnectedRegionArtifactCell(0, 2)
        ]);
        var transform = CreatePublishedTransform(source);
        var first = Evaluate(source, region, transform, 0, "derived.region-transform.persistence");
        var second = Evaluate(source, region, transform, 0, "derived.region-transform.persistence");
        var path = Path.Combine(fixtureDirectory, "transformed-region.json");
        if (first.Output is not null)
        {
            C3DTransformedRegionArtifactStore.Save(path, first.Output);
        }

        var restored = File.Exists(path)
            ? C3DTransformedRegionArtifactStore.Load(path)
            : null;
        var pass = first.Result.Status == ResultStatus.Pass
            && second.Result.Status == ResultStatus.Pass
            && first.Output is not null
            && second.Output is not null
            && restored is not null
            && first.Output.ContentSha256 == second.Output.ContentSha256
            && first.Output.ContentSha256 == restored.ContentSha256
            && first.Output.Cells.SequenceEqual(restored.Cells)
            && restored.SourceRegionArtifactId == region.ArtifactId
            && restored.TransformEntityId == transform.OutputEntityId;
        return (
            pass,
            $"first={first.Output?.ContentSha256};second={second.Output?.ContentSha256};restored={restored?.ContentSha256};cells={restored?.CellCount};path={File.Exists(path)}");
    }

    private static (bool Passed, string Evidence) VerifyGuards()
    {
        var source = CreateSource("source.region-transform.guards");
        var region = CreateRegionArtifact(source, "connected.region-transform.guards", [
            new C3DConnectedRegionArtifactCell(0, 0),
            new C3DConnectedRegionArtifactCell(0, 1)
        ]);
        var transform = CreatePublishedTransform(source);
        var otherSource = CreateSource("source.region-transform.other");
        var mismatchedSource = Evaluate(otherSource, region, transform, 0, "derived.region-transform.mismatch-source");
        var mismatchedRegion = Evaluate(
            source,
            region with { SourceContentSha256 = new string('B', 64) },
            transform,
            0,
            "derived.region-transform.mismatch-region");
        var mismatchedTransform = Evaluate(
            source,
            region,
            CreatePublishedTransform(otherSource),
            0,
            "derived.region-transform.mismatch-transform");
        var invalidIndex = Evaluate(source, region, transform, 3, "derived.region-transform.invalid-index");
        var collision = Evaluate(source, region, transform, 0, source.EntityId);
        var tampered = Evaluate(
            source,
            region with { ContentSha256 = new string('0', 64) },
            transform,
            0,
            "derived.region-transform.tampered");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = Evaluate(source, region, transform, 0, "derived.region-transform.canceled", cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        var pass = mismatchedSource.Result.Status == ResultStatus.Error
            && mismatchedSource.Output is null
            && mismatchedRegion.Result.Status == ResultStatus.Error
            && mismatchedRegion.Output is null
            && mismatchedTransform.Result.Status == ResultStatus.Error
            && mismatchedTransform.Output is null
            && invalidIndex.Result.Status == ResultStatus.Error
            && invalidIndex.Output is null
            && collision.Result.Status == ResultStatus.Error
            && collision.Output is null
            && tampered.Result.Status == ResultStatus.Error
            && tampered.Output is null
            && canceled;
        return (
            pass,
            $"source={mismatchedSource.Result.Status};region={mismatchedRegion.Result.Status};transform={mismatchedTransform.Result.Status};index={invalidIndex.Result.Status};collision={collision.Result.Status};tampered={tampered.Result.Status};canceled={canceled}");
    }

    private static (bool Passed, string Evidence) VerifyWarningAndCancellation()
    {
        var source = CreateSource("source.region-transform.warning");
        var region = CreateRegionArtifact(source, "connected.region-transform.warning", [
            new C3DConnectedRegionArtifactCell(0, 2)
        ]);
        var transform = CreatePublishedTransform(source);
        var evaluation = Evaluate(source, region, transform, 0, "derived.region-transform.warning");
        var pass = evaluation.Result.Status == ResultStatus.Warning
            && evaluation.Output is not null
            && evaluation.Output.CellCount == 1
            && evaluation.Output.FiniteCellCount == 0
            && evaluation.Output.MissingCellCount == 1
            && evaluation.Output.Cells[0].RawHeight is null;
        return (
            pass,
            $"status={evaluation.Result.Status};cells={evaluation.Output?.CellCount};finite={evaluation.Output?.FiniteCellCount};missing={evaluation.Output?.MissingCellCount}");
    }

    private static C3DRegionTransformPropagationEvaluation Evaluate(
        C3DHeightFieldSnapshot source,
        C3DConnectedRegionArtifact region,
        C3DAffineTransform3D transform,
        int regionIndex,
        string outputEntityId,
        CancellationToken cancellationToken = default) =>
        C3DRegionTransformPropagationRule.Evaluate(
            new C3DRegionTransformPropagationInput(
                "step.region-transform.propagation",
                source,
                region,
                regionIndex,
                transform,
                outputEntityId),
            cancellationToken);

    private static C3DHeightFieldSnapshot CreateSource(string entityId) =>
        C3DHeightFieldSnapshot.CreateForVerification(
            entityId,
            4,
            3,
            [1d, 2d, double.NaN, 4d, 7d, 9d, 9d, 10d, 11d, 12d, 13d, 14d],
            SourceUnit,
            SourceFrame);

    private static C3DConnectedRegionArtifact CreateRegionArtifact(
        C3DHeightFieldSnapshot source,
        string artifactId,
        IReadOnlyList<C3DConnectedRegionArtifactCell> cells)
    {
        var minimumRow = cells.Min(cell => cell.Row);
        var minimumColumn = cells.Min(cell => cell.Column);
        var maximumRow = cells.Max(cell => cell.Row);
        var maximumColumn = cells.Max(cell => cell.Column);
        return C3DConnectedRegionArtifact.Create(
            artifactId,
            "Region transform propagation fixture",
            source.EntityId,
            source.ContentSha256,
            source.RootSourceSha256,
            new string('A', 64),
            source.Unit,
            source.FrameId,
            source.Width,
            source.Height,
            C3DConnectedRegionArtifact.FourConnectivity,
            0,
            0,
            1,
            1,
            "grid-unit^2",
            [new C3DConnectedRegionArtifactRegion(
                0,
                cells[0].Row,
                cells[0].Column,
                cells,
                minimumRow,
                minimumColumn,
                maximumRow,
                maximumColumn,
                null)]);
    }

    private static C3DAffineTransform3D CreatePublishedTransform(
        C3DHeightFieldSnapshot source)
    {
        var locators = new[]
        {
            (Row: 0, Column: 0),
            (Row: 0, Column: 1),
            (Row: 1, Column: 0),
            (Row: 1, Column: 1)
        };
        var pairs = locators.Select((locator, index) =>
        {
            var rawHeight = source.Values.Span[locator.Row * source.Width + locator.Column];
            var point = ApplyFixtureMatrix(locator.Column, rawHeight, locator.Row);
            return new C3DLandmarkCorrespondencePair(
                $"derived.region-transform.corner.{index}",
                "Region transform corner",
                source.RootSourceSha256,
                locator.Column,
                rawHeight,
                locator.Row,
                $"region-transform.reference.{index}",
                point.X,
                point.Y,
                point.Z);
        }).ToArray();
        var correspondence = C3DLandmarkCorrespondenceSet.Create(
            "derived.region-transform.correspondence",
            pairs,
            source.EntityId,
            source.RootSourceSha256,
            source.Unit,
            source.FrameId,
            "frame.region-transform.reference",
            "fixture-unit",
            "region transform reference",
            "REV-1",
            1e-12,
            4,
            4,
            0.1,
            0.1,
            "region transform propagation fixture");
        var solve = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput(
            "step.region-transform.solve",
            "derived.region-transform.affine",
            correspondence,
            1000,
            1e-12));
        if (solve.Result.Status != ResultStatus.Pass || solve.Output is null)
        {
            throw new InvalidDataException(
                $"Region transform propagation fixture could not publish its affine transform: {solve.Result.Message}");
        }

        return solve.Output;
    }

    private static (double X, double Y, double Z) ApplyFixtureMatrix(
        double x,
        double y,
        double z) =>
        (2 * x + 0.5 * y - 0.25 * z + 10,
         -x + 3 * y + 0.75 * z + 20,
         0.2 * x - 0.3 * y + 4 * z + 30);

    private static bool Nearly(double actual, double expected) =>
        Math.Abs(actual - expected) <= 1e-10;

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

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}
