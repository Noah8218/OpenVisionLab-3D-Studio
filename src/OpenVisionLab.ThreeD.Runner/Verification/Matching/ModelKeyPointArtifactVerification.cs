using System.Numerics;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ModelKeyPointArtifactVerification
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
        var model = SurfaceModelPreparation.Prepare(
            mesh,
            new SurfaceModelPreparationRequest(
                "surface-model.key-points",
                "Controlled Model Key Points",
                "source.mesh.key-points",
                SourceSha256,
                "mm",
                "model-frame",
                new SurfaceModelPreparationParameters(
                    SurfaceModelPreparationParameters
                        .DeterministicTriangleCentroidSampling,
                    6,
                    1e-9,
                    1e-6,
                    0.9),
                SurfaceModelSymmetryDeclaration.None,
                new SurfaceModelSurfaceSelectionRequest(
                    [1],
                    [2],
                    true)));
        var originalModelSha256 = model.ContentSha256;
        var originalSamples = model.Samples.ToArray();
        var parameters = new ModelKeyPointExtractionParameters(
            ModelKeyPointExtractionParameters
                .DeterministicFarthestModelSampleMethod,
            2,
            1.0);
        var first = ModelKeyPointExtractor.Extract(model, parameters);
        var repeated = ModelKeyPointExtractor.Extract(model, parameters);
        var all = ModelKeyPointExtractor.Extract(
            model,
            parameters with
            {
                MaximumKeyPointCount = 3,
                MinimumSeparation = 0.0
            });
        var validity = ModelKeyPointArtifactValidator.Inspect(first, model);
        var overlay = ModelKeyPointDebugOverlayBuilder.Build(model, first);
        var overlayValidity =
            ModelKeyPointDebugOverlayArtifactValidator.Inspect(
                overlay,
                first,
                model);

        var artifactPath = Path.Combine(
            directory,
            "controlled-model-key-points.json");
        ModelKeyPointArtifactStore.Save(artifactPath, first);
        var loaded = ModelKeyPointArtifactStore.Load(artifactPath);
        var loadedValidity = ModelKeyPointArtifactValidator.Inspect(
            loaded,
            model);

        var tampered = first with
        {
            KeyPoints = first.KeyPoints
                .Select((point, index) => index == 0
                    ? point with
                    {
                        SourceTriangleIndex = point.SourceTriangleIndex + 1
                    }
                    : point)
                .ToArray()
        };
        var tamperedValidity = ModelKeyPointArtifactValidator.Inspect(
            tampered,
            model);
        var tamperedSaveRejected = ThrowsInvalidData(
            () => ModelKeyPointArtifactStore.Save(
                Path.Combine(directory, "tampered-model-key-points.json"),
                tampered),
            out var tamperedSaveEvidence);
        var unsupportedMethodRejected = ThrowsInvalidData(
            () => ModelKeyPointExtractor.Extract(
                model,
                parameters with { Method = "unsupported" }),
            out var unsupportedMethodEvidence);

        var selection = model.SurfaceSelection!;
        var cases = new[]
        {
            Check(
                "identified-artifact-contract",
                first.SchemaVersion == ModelKeyPointArtifact.CurrentSchemaVersion
                && first.Semantics == ModelKeyPointArtifact.CurrentSemantics
                && first.ArtifactId == "key-points.model.surface-model.key-points"
                && first.ModelContentSha256 == model.ContentSha256
                && first.Unit == model.Unit
                && first.FrameId == model.FrameId,
                $"id={first.ArtifactId};model={first.ModelContentSha256};unit={first.Unit};frame={first.FrameId}"),
            Check(
                "stable-key-point-count-and-identity",
                first.KeyPoints.Length == 2
                && first.KeyPoints
                    .Select(point => point.KeyPointId)
                    .SequenceEqual([
                        "kp.sample.00000000",
                        "kp.sample.00000002"
                    ])
                && first.KeyPoints
                    .Select(point => point.SourceSampleOrder)
                    .SequenceEqual([0, 2]),
                $"count={first.KeyPoints.Length};ids={string.Join(',', first.KeyPoints.Select(point => point.KeyPointId))}"),
            Check(
                "farthest-point-separation-evidence",
                first.KeyPoints[0].NearestSelectedDistance == 0.0
                && Math.Abs(
                    first.KeyPoints[1].NearestSelectedDistance - 8.0)
                    < 1e-12,
                $"distances={string.Join(',', first.KeyPoints.Select(point => point.NearestSelectedDistance.ToString("G17")))}"),
            Check(
                "j05-retained-domain-source-locators",
                selection.RetainedSourceTriangleIndices
                    .SequenceEqual([0, 4, 5])
                && model.Samples
                    .Select(sample => sample.SourceTriangleIndex)
                    .SequenceEqual([0, 4, 5])
                && first.KeyPoints
                    .Select(point => point.SourceTriangleIndex)
                    .SequenceEqual([0, 5]),
                $"retained={string.Join(',', selection.RetainedSourceTriangleIndices)};samples={string.Join(',', model.Samples.Select(sample => sample.SourceTriangleIndex))};keyPoints={string.Join(',', first.KeyPoints.Select(point => point.SourceTriangleIndex))}"),
            Check(
                "repeatable-content-identity",
                first.ContentSha256 == repeated.ContentSha256
                && first.KeyPoints.SequenceEqual(repeated.KeyPoints),
                $"first={first.ContentSha256};repeated={repeated.ContentSha256}"),
            Check(
                "parameters-change-content-and-count",
                all.KeyPoints.Length == 3
                && all.KeyPoints
                    .Select(point => point.SourceSampleOrder)
                    .SequenceEqual([0, 2, 1])
                && all.ContentSha256 != first.ContentSha256,
                $"bounded={first.ContentSha256};all={all.ContentSha256};allOrder={string.Join(',', all.KeyPoints.Select(point => point.SourceSampleOrder))}"),
            Check(
                "artifact-validator-pass",
                validity.IsValid && validity.ContentIdentityValid,
                validity.Evidence),
            Check(
                "save-load-roundtrip",
                loaded.ContentSha256 == first.ContentSha256
                && loaded.KeyPoints.SequenceEqual(first.KeyPoints)
                && loadedValidity.IsValid
                && File.ReadAllText(artifactPath).Contains(
                    "\"keyPointId\"",
                    StringComparison.Ordinal),
                $"path={artifactPath};sha256={loaded.ContentSha256}"),
            Check(
                "tampered-artifact-fails-closed",
                !tamperedValidity.IsValid
                && !tamperedValidity.ContentIdentityValid
                && tamperedSaveRejected,
                $"validator={string.Join(' ', tamperedValidity.Errors)};store={tamperedSaveEvidence}"),
            Check(
                "unsupported-method-fails-closed",
                unsupportedMethodRejected,
                unsupportedMethodEvidence),
            Check(
                "wpf-neutral-debug-overlay-chain",
                overlayValidity.IsValid
                && overlay.Semantics
                    == ModelKeyPointDebugOverlayArtifact.CurrentSemantics
                && overlay.ModelContentSha256 == model.ContentSha256
                && overlay.KeyPointContentSha256 == first.ContentSha256
                && overlay.Markers.Length == first.KeyPoints.Length
                && overlay.Markers.Select(marker => marker.KeyPointId)
                    .SequenceEqual(first.KeyPoints.Select(
                        point => point.KeyPointId)),
                overlayValidity.Evidence),
            Check(
                "debug-overlay-preserves-position-and-normal",
                overlay.Markers.Zip(first.KeyPoints).All(pair =>
                    pair.First.Position == pair.Second.Position
                    && pair.First.Normal == pair.Second.Normal
                    && pair.First.SourceTriangleIndex
                        == pair.Second.SourceTriangleIndex),
                $"markers={overlay.Markers.Length};frame={overlay.FrameId};unit={overlay.Unit}"),
            Check(
                "extraction-and-overlay-do-not-mutate-model",
                model.ContentSha256 == originalModelSha256
                && model.Samples.SequenceEqual(originalSamples),
                $"before={originalModelSha256};after={model.ContentSha256};samples={model.Samples.Length}"),
            Check(
                "contract-does-not-change-matching",
                first.Semantics.Contains(
                    "no-matching-effect",
                    StringComparison.Ordinal)
                && overlayValidity.Evidence.Contains(
                    "matchingEffect=false",
                    StringComparison.Ordinal),
                $"artifact={first.Semantics};overlay={overlayValidity.Evidence}"),
            Check(
                "noah-package-provenance",
                LibraryNoahHeightMapInspection.PackageVersion == "2.9.1"
                && LibraryNoahHeightMapInspection.PackageSourceCommit
                    == "9dd95690d3e439b459c39aea99878880cdcc5808",
                $"version={LibraryNoahHeightMapInspection.PackageVersion};commit={LibraryNoahHeightMapInspection.PackageSourceCommit}")
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"ModelKeyPointArtifactVerification|{(passed == cases.Length ? "PASS" : "FAIL")}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            $"Contract|schema={ModelKeyPointArtifact.CurrentSchemaVersion}|method={parameters.Method}|seed=lowest-source-sample-order|next=max-nearest-distance|tie=lowest-source-sample-order|minimumSeparation=strict|matchingEffect=false|ui=false",
            $"Noah|package=Lib.ThreeD {LibraryNoahHeightMapInspection.PackageVersion}|sourceCommit={LibraryNoahHeightMapInspection.PackageSourceCommit}|tool=DeterministicModelKeyPointExtractionTool",
            $"Artifact|path={artifactPath}|sha256={first.ContentSha256}|sourceSamples={first.SourceSampleCount}|keyPoints={first.KeyPoints.Length}|ids={string.Join(',', first.KeyPoints.Select(point => point.KeyPointId))}",
            $"Overlay|sha256={overlay.ContentSha256}|markers={overlay.Markers.Length}|frame={overlay.FrameId}|displayOnly=true"
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
        Console.WriteLine(
            "Model key-point artifact verification: "
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
            "controlled-key-points.stl",
            "Controlled Model Key Points",
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
