using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DLandmarkCorrespondenceGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("valid-exact-four-and-deterministic-hash", VerifyValidExactFour),
            Check("source-and-reference-coplanar-rejected", VerifyCoplanarRejection),
            Check("duplicate-and-lineage-fail-closed", VerifyDuplicateAndLineageFailures),
            Check("strict-schema-1.2-recipe-adapter-and-roundtrip", VerifyStrictRecipeAdapterAndRoundtrip),
            Check("cancellation-propagates", VerifyCancellation)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DLandmarkCorrespondenceGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|pairs=ExactlyFour|source=CurrentPublishedCornerAnchor|independence=RequireNonDegenerateTetrahedra|no-affine-transform"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"3D Landmark Correspondence golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyValidExactFour()
    {
        var anchors = CreateTetrahedronAnchors();
        var references = CreateReferenceTetrahedron();
        var one = Evaluate(anchors, references);
        var two = Evaluate(anchors, references);
        var output = one.Output;
        return (one.Result.Status == ResultStatus.Pass
            && output is not null
            && output.Pairs.Count == 4
            && output.SourceRank == 4
            && output.ReferenceRank == 4
            && output.SourceNormalizedTetrahedronVolume > 0.1
            && output.ReferenceNormalizedTetrahedronVolume > 0.1
            && output.ContentSha256 == two.Output?.ContentSha256,
            $"one={Evidence(one)};two={Evidence(two)}");
    }

    private static (bool Passed, string Evidence) VerifyCoplanarRejection()
    {
        var anchors = CreateTetrahedronAnchors();
        var planarSource = anchors.Select((anchor, index) => index == 3
                ? CreateAnchor(anchor.OutputEntityId, anchor.OutputRole, 1, 1, 0)
                : anchor)
            .ToArray();
        var sourceRejected = Evaluate(planarSource, CreateReferenceTetrahedron());
        var planarReferences =
            new[]
            {
                new C3DReferenceLandmark("fixture.a", 10, 10, 0),
                new C3DReferenceLandmark("fixture.b", 11, 10, 0),
                new C3DReferenceLandmark("fixture.c", 10, 11, 0),
                new C3DReferenceLandmark("fixture.d", 11, 11, 0)
            };
        var referenceRejected = Evaluate(anchors, planarReferences);
        return (sourceRejected.Result.Status == ResultStatus.Error
            && sourceRejected.Result.Message.Contains("Source landmark tetrahedron", StringComparison.Ordinal)
            && referenceRejected.Result.Status == ResultStatus.Error
            && referenceRejected.Result.Message.Contains("Reference landmark tetrahedron", StringComparison.Ordinal),
            $"source={Evidence(sourceRejected)};reference={Evidence(referenceRejected)}");
    }

    private static (bool Passed, string Evidence) VerifyDuplicateAndLineageFailures()
    {
        var anchors = CreateTetrahedronAnchors();
        var duplicate = anchors.ToArray();
        duplicate[3] = duplicate[0];
        var duplicateRejected = Evaluate(duplicate, CreateReferenceTetrahedron());
        var mismatched = anchors.ToArray();
        mismatched[3] = CreateAnchor("derived.corner.d", "CornerD", 0, 0, 1, rootHash: new string('B', 64));
        var lineageRejected = Evaluate(mismatched, CreateReferenceTetrahedron());
        var duplicateReference = CreateReferenceTetrahedron().ToArray();
        duplicateReference[3] = duplicateReference[0];
        var referenceRejected = Evaluate(anchors, duplicateReference);
        return (duplicateRejected.Result.Status == ResultStatus.Error
            && lineageRejected.Result.Status == ResultStatus.Error
            && referenceRejected.Result.Status == ResultStatus.Error,
            $"duplicate={Evidence(duplicateRejected)};lineage={Evidence(lineageRejected)};reference={Evidence(referenceRejected)}");
    }

    private static (bool Passed, string Evidence) VerifyStrictRecipeAdapterAndRoundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "LandmarkCorrespondenceGolden", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.synthetic", 4, 4, Enumerable.Range(0, 16).Select(index => (double)index).ToArray());
            var sourcePath = Path.Combine(root, "fixture.c3d");
            source.SaveC3D(sourcePath);
            var anchors = CreateTetrahedronAnchors(source.ContentSha256);
            var document = CreateDocument(anchors, source, sourcePath);
            var prepared = ToolRecipeLandmarkCorrespondenceExecution.TryPrepare(document, "step.correspondence.01", anchors, out var input, out var readyMessage);
            var evaluation = ToolRecipeLandmarkCorrespondenceExecution.Execute(document, "step.correspondence.01", anchors);
            var invalid = document with { Steps = [.. document.Steps.Take(document.Steps.Count - 1), document.Steps[^1] with { Parameters = [.. document.Steps[^1].Parameters, new ToolRecipeParameter("Unknown", "x")] }] };
            var rejected = !ToolRecipeLandmarkCorrespondenceExecution.TryPrepare(invalid, "step.correspondence.01", anchors, out _, out var rejectedMessage);
            var path = Path.Combine(root, "correspondence.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(path, document);
            var reopened = ToolRecipeDocumentStore.Load(path);
            var descriptor = reopened.Selections!.Single().CorrespondenceDescriptor;
            return (prepared
                && input is not null
                && evaluation.Result.Status == ResultStatus.Pass
                && rejected
                && reopened.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && descriptor is not null
                && descriptor.ReferenceRevision == "REV-1"
                && rejectedMessage.Contains("exactly", StringComparison.OrdinalIgnoreCase),
                $"prepared={prepared}:{readyMessage};evaluation={Evidence(evaluation)};rejected={rejected}:{rejectedMessage};schema={reopened.SchemaVersion};descriptor={descriptor?.ReferenceProvenance}@{descriptor?.ReferenceRevision}");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static (bool Passed, string Evidence) VerifyCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            _ = C3DLandmarkCorrespondenceRule.Evaluate(CreateInput(CreateTetrahedronAnchors(), CreateReferenceTetrahedron()), cancellation.Token);
            return (false, "no cancellation thrown");
        }
        catch (OperationCanceledException)
        {
            return (true, "OperationCanceledException");
        }
    }

    private static C3DLandmarkCorrespondenceEvaluation Evaluate(
        IReadOnlyList<C3DLineIntersectionFeature> anchors,
        IReadOnlyList<C3DReferenceLandmark> references) =>
        C3DLandmarkCorrespondenceRule.Evaluate(CreateInput(anchors, references));

    private static C3DLandmarkCorrespondenceInput CreateInput(
        IReadOnlyList<C3DLineIntersectionFeature> anchors,
        IReadOnlyList<C3DReferenceLandmark> references) =>
        new("step.correspondence.01", "derived.correspondences.01", anchors, references,
            "frame.fixture", "fixture-coordinate", "fixture.synthetic", "REV-1", 0.1);

    private static ToolRecipeDocument CreateDocument(
        IReadOnlyList<C3DLineIntersectionFeature> anchors,
        C3DHeightFieldSnapshot source,
        string sourcePath)
    {
        var descriptor = new ToolRecipeLandmarkCorrespondenceDescriptor(
            "frame.fixture", "fixture-coordinate", "fixture.synthetic", "REV-1",
            "ExactlyFour", "CurrentPublishedCornerAnchor", "RequireNonDegenerateTetrahedra", 0.1);
        var rows = CreateReferenceTetrahedron()
            .Select((reference, index) => new ToolRecipeLandmarkCorrespondence(
                anchors[index].OutputEntityId, reference.Id,
                new ToolRecipeXyz(reference.X, reference.Y, reference.Z), "frame.fixture"))
            .ToArray();
        var selection = new ToolRecipeSelection(
            "selection.correspondence.01", "Synthetic fixture correspondence",
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
            source.EntityId, source.FrameId,
            new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height),
            null, null, rows, descriptor);
        var anchorSteps = anchors.Select(anchor => new ToolRecipeStep(
            $"step.{anchor.OutputRole.ToLowerInvariant()}", "fixture-anchor", "Fixture CornerAnchor", 1,
            ["source.synthetic"], anchor.OutputEntityId, [])).ToArray();
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Landmark Correspondence fixture",
            new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
            [],
            [.. anchorSteps, new ToolRecipeStep("step.correspondence.01", "landmark-correspondence", "Landmark Correspondence", 1, [selection.Id], "derived.correspondences.01", [new("PairCountPolicy", "ExactlyFour"), new("SourceArtifactPolicy", "CurrentPublishedCornerAnchor"), new("AffineIndependencePolicy", "RequireNonDegenerateTetrahedra")])],
            [selection]);
    }

    private static C3DLineIntersectionFeature[] CreateTetrahedronAnchors(string? rootHash = null) =>
    [
        CreateAnchor("derived.corner.a", "CornerA", 0, 0, 0, rootHash),
        CreateAnchor("derived.corner.b", "CornerB", 1, 0, 0, rootHash),
        CreateAnchor("derived.corner.c", "CornerC", 0, 1, 0, rootHash),
        CreateAnchor("derived.corner.d", "CornerD", 0, 0, 1, rootHash)
    ];

    private static C3DReferenceLandmark[] CreateReferenceTetrahedron() =>
    [
        new("fixture.a", 10, 20, 30),
        new("fixture.b", 11, 20, 30),
        new("fixture.c", 10, 21, 30),
        new("fixture.d", 10, 20, 31)
    ];

    private static C3DLineIntersectionFeature CreateAnchor(
        string outputId,
        string role,
        double x,
        double y,
        double z,
        string? rootHash = null)
    {
        var first = CreateLine($"{outputId}.line-a", 1, 0, 0, rootHash);
        var second = CreateLine($"{outputId}.line-b", 0, 1, 0, rootHash);
        return C3DLineIntersectionFeature.Create(
            outputId, first, second, 1, 45, 0, role,
            x, y, z, x, y, z, x, y, z, 0, 0, 90, 0,
            -1, 1, 0, -1, 1, 0, "synthetic");
    }

    private static C3DLineFeature CreateLine(string outputId, double dx, double dy, double dz, string? rootHash)
    {
        var edge = C3DHeightDifferenceEdgePointSet.Create(
            $"{outputId}.edge", "source.synthetic", rootHash ?? new string('A', 64),
            "derived.filtered.01", new string('B', 64), "selection.synthetic",
            new ToolRecipeGridRectangle(0, 0, 3, 3), "raw-height", "frame.c3d-grid-index",
            C3DHeightDifferenceComparisonAxis.AcrossColumns,
            C3DHeightDifferencePolarity.Rising, 1,
            [
                new C3DHeightDifferenceEdgePoint(0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0),
                new C3DHeightDifferenceEdgePoint(1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1),
                new C3DHeightDifferenceEdgePoint(2, 2, 0, 0, 2, 1, 0, 1, 1, 0, 0, 2)
            ],
            new C3DHeightDifferenceEdgeDiagnostics(3, 3, 0, 3, 0, 1, 1, 1), "synthetic");
        return C3DLineFeature.Create(
            outputId, edge, 0.1, 3, 1, 2,
            0, 0, 0, dx, dy, dz,
            -1 * dx, -1 * dy, -1 * dz, dx, dy, dz,
            new C3DLineFeatureDiagnostics(3, 3, 0, 1, 0, 2, 2, 0, 0, 0, 4, 1, 1), [], "synthetic");
    }

    private static string Evidence(C3DLandmarkCorrespondenceEvaluation evaluation) =>
        $"status={evaluation.Result.Status};hash={evaluation.Output?.ContentSha256};sourceRank={evaluation.Output?.SourceRank};referenceRank={evaluation.Output?.ReferenceRank};message={evaluation.Result.Message}";
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
