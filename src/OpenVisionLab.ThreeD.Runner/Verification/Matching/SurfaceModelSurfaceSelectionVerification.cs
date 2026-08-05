using System.Numerics;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class SurfaceModelSurfaceSelectionVerification
{
    private const string SourceSha256 =
        "B18E281497A59831E6C0A4D3E00684EBEC845EB743C0931DD1003954435A16E8";

    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var mesh = CreateMesh();
        var sourceIndices = mesh.Indices.ToArray();
        var parameters = new SurfaceModelPreparationParameters(
            SurfaceModelPreparationParameters
                .DeterministicTriangleCentroidSampling,
            6,
            1e-9,
            1e-6,
            0.9);
        var request = new SurfaceModelPreparationRequest(
            "surface-model.surface-selection",
            "Controlled Surface Selection",
            "source.mesh.surface-selection",
            SourceSha256,
            "mm",
            "model-frame",
            parameters);
        var legacy = SurfaceModelPreparation.Prepare(mesh, request);
        var selectedRequest = request with
        {
            SurfaceSelection = new SurfaceModelSurfaceSelectionRequest(
                [1],
                [2],
                true)
        };
        var selected = SurfaceModelPreparation.Prepare(
            mesh,
            selectedRequest);
        var repeated = SurfaceModelPreparation.Prepare(
            mesh,
            selectedRequest);
        var validity = SurfaceModelArtifactValidator.Inspect(selected);

        var modelPath = Path.Combine(
            directory,
            "controlled-selection.surface-model.json");
        SurfaceModelArtifactStore.Save(modelPath, selected);
        var loaded = SurfaceModelArtifactStore.Load(modelPath);

        var tampered = selected with
        {
            SurfaceSelection = selected.SurfaceSelection! with
            {
                RetainedSourceTriangleIndices = [0, 1, 4]
            }
        };
        tampered = tampered with
        {
            ContentSha256 =
                SurfaceModelArtifact.CalculateContentSha256(tampered)
        };
        var tamperedValidity =
            SurfaceModelArtifactValidator.Inspect(tampered);
        var tamperedSaveRejected = ThrowsInvalidData(
            () => SurfaceModelArtifactStore.Save(
                Path.Combine(directory, "tampered.surface-model.json"),
                tampered),
            out var tamperedSaveEvidence);
        var overlapRejected = ThrowsInvalidData(
            () => SurfaceModelPreparation.Prepare(
                mesh,
                selectedRequest with
                {
                    SurfaceSelection =
                        new SurfaceModelSurfaceSelectionRequest(
                            [1],
                            [1],
                            true)
                }),
            out var overlapEvidence);

        var edgeParameters = new ModelSurfaceEdgeExtractionParameters(
            ModelSurfaceEdgeExtractionParameters
                .TopologyBoundaryAndCreaseMethod,
            0.1,
            30.0,
            true);
        var edges = ModelSurfaceEdgeExtractor.Extract(
            selected,
            edgeParameters);

        var scenePoints = selected.Samples
            .Select(sample => sample.Position)
            .ToArray();
        var scene = PreparedScenePreparation.Prepare(
            new PreparedScenePreparationRequest(
                "prepared-scene.surface-selection",
                "Controlled Surface Selection Scene",
                PreparedSceneArtifact.CurrentCoordinateConvention,
                SurfaceMatchingFoundationVerification.CreateQuality(
                    "scene.surface-selection",
                    "fixture://surface-selection",
                    "scene-frame",
                    scenePoints),
                scenePoints,
                new PreparedScenePreparationParameters(
                    PreparedScenePreparationParameters
                        .DeterministicEvenPointSampling,
                    scenePoints.Length)));
        var search = RigidSurfacePoseSearch.Execute(
            selected,
            scene,
            new RigidSurfacePoseSearchParameters(
                0.0, 0.0, 1.0,
                0.0, 0.0, 1.0,
                0.0, 0.0, 1.0,
                0.0, 0.0,
                0.0, 0.0,
                0.0, 0.0,
                1e-9,
                3,
                1));
        var execution = SurfaceMatchExecutionArtifact.Create(
            selected,
            scene,
            search);

        var selection = selected.SurfaceSelection!;
        var cases = new[]
        {
            Check(
                "no-selection-preserves-legacy-behavior",
                legacy.SchemaVersion == SurfaceModelArtifact.LegacySchemaVersion
                && legacy.SurfaceSelection is null
                && legacy.Triangles.Length == 6
                && legacy.Samples.Length == 6
                && legacy.Samples.Select(sample => sample.SourceTriangleIndex)
                    .SequenceEqual(Enumerable.Range(0, 6)),
                $"schema={legacy.SchemaVersion};triangles={legacy.Triangles.Length};samples={legacy.Samples.Length}"),
            Check(
                "selection-is-valid-current-schema",
                selected.SchemaVersion == SurfaceModelArtifact.CurrentSchemaVersion
                && selected.Symmetry == SurfaceModelSymmetryDeclaration.None
                && validity.IsValid
                && validity.SurfaceSelectionValid
                && validity.RetainedSurfaceCount == 3
                && validity.RemovedSurfaceCount == 3,
                validity.Evidence),
            Check(
                "explicit-internal-and-unobservable-evidence",
                selection.ExplicitInternalSourceTriangleIndices
                    .SequenceEqual([1])
                && selection.ExplicitUnobservableSourceTriangleIndices
                    .SequenceEqual([2])
                && selection.RemovedSurfaces.Any(item =>
                    item.SourceTriangleIndex == 1
                    && item.Reason == SurfaceModelSurfaceSelection
                        .ExplicitInternalReason)
                && selection.RemovedSurfaces.Any(item =>
                    item.SourceTriangleIndex == 2
                    && item.Reason == SurfaceModelSurfaceSelection
                        .ExplicitUnobservableReason),
                string.Join(",", selection.RemovedSurfaces.Select(item =>
                    $"{item.SourceTriangleIndex}:{item.Reason}"))),
            Check(
                "exact-coordinate-duplicate-removal-evidence",
                selection.RemoveExactDuplicateTriangles
                && selection.RemovedSurfaces.Any(item =>
                    item.SourceTriangleIndex == 3
                    && item.Reason == SurfaceModelSurfaceSelection
                        .ExactDuplicateReason
                    && item.DuplicateOfSourceTriangleIndex == 0),
                string.Join(",", selection.RemovedSurfaces.Select(item =>
                    $"{item.SourceTriangleIndex}:{item.Reason}:{item.DuplicateOfSourceTriangleIndex}"))),
            Check(
                "retained-domain-and-samples-use-source-locators",
                selection.RetainedSourceTriangleIndices
                    .SequenceEqual([0, 4, 5])
                && selected.Samples
                    .Select(sample => sample.SourceTriangleIndex)
                    .SequenceEqual([0, 4, 5]),
                $"retained={string.Join(',', selection.RetainedSourceTriangleIndices)};samples={string.Join(',', selected.Samples.Select(sample => sample.SourceTriangleIndex))}"),
            Check(
                "source-mesh-and-full-topology-remain-immutable",
                mesh.Indices.SequenceEqual(sourceIndices)
                && selected.Triangles.Length == mesh.TriangleCount
                && selected.Points.Length == mesh.Positions.Length,
                $"sourceTriangles={mesh.TriangleCount};storedTriangles={selected.Triangles.Length};points={selected.Points.Length}"),
            Check(
                "selection-is-repeatable",
                selected.ContentSha256 == repeated.ContentSha256
                && repeated.SurfaceSelection is { } repeatedSelection
                && selection.RetainedSourceTriangleIndices.SequenceEqual(
                    repeatedSelection.RetainedSourceTriangleIndices)
                && selection.RemovedSurfaces.SequenceEqual(
                    repeatedSelection.RemovedSurfaces),
                $"first={selected.ContentSha256};repeated={repeated.ContentSha256}"),
            Check(
                "selection-changes-identified-content",
                selected.ContentSha256 != legacy.ContentSha256,
                $"legacy={legacy.ContentSha256};selected={selected.ContentSha256}"),
            Check(
                "save-load-roundtrip-preserves-selection",
                loaded.ContentSha256 == selected.ContentSha256
                && loaded.SurfaceSelection is { } loadedSelection
                && loadedSelection.RetainedSourceTriangleIndices
                    .SequenceEqual(selection.RetainedSourceTriangleIndices)
                && loadedSelection.RemovedSurfaces
                    .SequenceEqual(selection.RemovedSurfaces)
                && File.ReadAllText(modelPath).Contains(
                    "\"surfaceSelection\"",
                    StringComparison.Ordinal),
                $"path={modelPath};sha256={loaded.ContentSha256}"),
            Check(
                "tampered-selection-fails-closed",
                !tamperedValidity.IsValid
                && !tamperedValidity.SurfaceSelectionValid
                && tamperedSaveRejected,
                $"validator={string.Join(' ', tamperedValidity.Errors)};store={tamperedSaveEvidence}"),
            Check(
                "overlapping-explicit-roles-fail-closed",
                overlapRejected,
                overlapEvidence),
            Check(
                "model-edge-extraction-uses-retained-domain",
                edges.SourceTriangleCount == 3
                && edges.Edges.Length == 9
                && edges.Edges.All(edge =>
                    edge.FirstPointIndex is (>= 0 and <= 2) or (>= 12 and <= 17)
                    && edge.SecondPointIndex is (>= 0 and <= 2) or (>= 12 and <= 17)),
                $"sourceTriangles={edges.SourceTriangleCount};edges={edges.Edges.Length}"),
            Check(
                "matching-uses-retained-samples",
                search.State == RigidSurfacePoseSearchState.Matched
                && search.Coverage.ModelSampleCount == 3
                && search.Coverage.MatchedModelSampleCount == 3
                && Math.Abs(search.Coverage.CoverageRatio - 1.0) < 1e-12,
                search.Coverage.Evidence),
            Check(
                "overlay-uses-retained-topology",
                execution.Overlay is { } overlay
                && overlay.Triangles.SequenceEqual(
                    SurfaceModelSurfaceDomain.GetRetainedTriangles(selected))
                && overlay.Triangles.Length == 3,
                $"overlayTriangles={execution.Overlay?.Triangles.Length}"),
            Check(
                "selection-contract-does-not-claim-viewpoint-inference",
                selection.Policy == SurfaceModelSurfaceSelection
                    .ExactDuplicateAndExplicitExclusionPolicy
                && !selection.Policy.Contains(
                    "viewpoint",
                    StringComparison.OrdinalIgnoreCase),
                selection.Policy)
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"SurfaceModelSurfaceSelectionVerification|{(passed == cases.Length ? "PASS" : "FAIL")}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            $"Contract|schema={SurfaceModelArtifact.CurrentSchemaVersion}|policy={selection.Policy}|automatic=exact-coordinate-duplicate|authored=internal,unobservable|viewpointInference=false|toleranceInference=false|sourceMutation=false|ui=false",
            $"Sdk|package=OpenVisionLab.Vision3D {VisionSdkHeightMapInspection.PackageVersion}|sourceCommit={VisionSdkHeightMapInspection.PackageSourceCommit}|tool=DeterministicModelSurfaceSelectionTool",
            $"Artifact|path={modelPath}|sha256={selected.ContentSha256}|sourceTriangles={selection.SourceTriangleCount}|retained={selection.RetainedSourceTriangleIndices.Length}|removed={selection.RemovedSurfaces.Length}|samples={selected.Samples.Length}",
            $"ActiveDomain|retained={string.Join(',', selection.RetainedSourceTriangleIndices)}|modelEdges={edges.Edges.Length}|overlayTriangles={execution.Overlay?.Triangles.Length}|coverage={search.Coverage.CoverageRatio:G17}"
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(false));
        Console.WriteLine(
            $"SurfaceModel surface-selection verification: "
            + $"{(passed == cases.Length ? "PASS" : "FAIL")} "
            + $"({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 1;
    }

    private static ImportedMesh CreateMesh()
    {
        var positions = new[]
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
            new Vector3(2, 0, 0), new Vector3(3, 0, 0), new Vector3(2, 1, 0),
            new Vector3(4, 0, 0), new Vector3(5, 0, 0), new Vector3(4, 1, 0),
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
            new Vector3(6, 0, 0), new Vector3(7, 0, 0), new Vector3(6, 1, 0),
            new Vector3(8, 0, 0), new Vector3(9, 0, 0), new Vector3(8, 1, 0)
        };
        return ImportedMesh.CreateTriangleMesh(
            "controlled-selection.stl",
            "Controlled Surface Selection",
            "STL",
            positions,
            Enumerable.Range(0, positions.Length).ToArray(),
            positions.Select(_ => Vector3.UnitZ).ToArray());
    }

    private static bool ThrowsInvalidData(
        Action action,
        out string evidence)
    {
        try
        {
            action();
            evidence = "No exception.";
            return false;
        }
        catch (InvalidDataException exception)
        {
            evidence = exception.Message;
            return true;
        }
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);
}
