using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class SurfaceModelFoundationVerification
{
    private const string SourceSha256 =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory =
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var mesh = CreateValidMesh();
        var oneSampleParameters = CreateParameters(maximumSampleCount: 1);
        var twoSampleParameters = CreateParameters(maximumSampleCount: 2);
        var request = CreateRequest(oneSampleParameters);
        var first = SurfaceModelPreparation.Prepare(mesh, request);
        var repeated = SurfaceModelPreparation.Prepare(mesh, request);
        var twoSamples = SurfaceModelPreparation.Prepare(
            mesh,
            request with { Parameters = twoSampleParameters });
        var validity = SurfaceModelArtifactValidator.Inspect(first);
        var artifactPath = Path.Combine(
            directory,
            "known-valid.surface-model.json");

        SurfaceModelArtifactStore.Save(artifactPath, first);
        var loaded = SurfaceModelArtifactStore.Load(artifactPath);

        SurfaceModelArtifactStore.Save(artifactPath, twoSamples);
        var overwritten = SurfaceModelArtifactStore.Load(artifactPath);
        SurfaceModelArtifactStore.Save(artifactPath, first);

        var invalidPoint = Rehash(first with
        {
            Points =
            [
                new SurfaceModelPoint3(double.NaN, 0.0, 0.0),
                .. first.Points.Skip(1)
            ]
        });
        var invalidIndex = Rehash(first with
        {
            Triangles =
            [
                new SurfaceModelTriangle(0, 1, 99),
                first.Triangles[1]
            ]
        });
        var degenerateTriangle = Rehash(first with
        {
            Points =
            [
                first.Points[0],
                new SurfaceModelPoint3(1.0, 1.0, 0.0),
                .. first.Points.Skip(2)
            ]
        });
        var missingNormal = Rehash(first with
        {
            Normals = first.Normals[..^1]
        });
        var nonUnitNormal = Rehash(first with
        {
            Normals =
            [
                new SurfaceModelPoint3(0.0, 0.0, 2.0),
                .. first.Normals.Skip(1)
            ]
        });
        var reversedNormal = Rehash(first with
        {
            Normals =
            [
                new SurfaceModelPoint3(0.0, 0.0, -1.0),
                .. first.Normals.Skip(1)
            ]
        });
        var invalidSample = Rehash(first with
        {
            Samples =
            [
                first.Samples[0] with
                {
                    SourceTriangleIndex = 0
                }
            ]
        });
        var tamperedHash = first with
        {
            ContentSha256 =
                "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
        };
        var unsupported = Rehash(first with { SchemaVersion = "9.0" });

        var corruptPath = Path.Combine(
            directory,
            "corrupt.surface-model.json");
        File.WriteAllText(corruptPath, "{not-json");
        var corruptRejected = ThrowsInvalidData(
            () => SurfaceModelArtifactStore.Load(corruptPath),
            out var corruptEvidence);

        var unsupportedPath = Path.Combine(
            directory,
            "unsupported.surface-model.json");
        File.WriteAllText(
            unsupportedPath,
            System.Text.Json.JsonSerializer.Serialize(
                unsupported,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy =
                        System.Text.Json.JsonNamingPolicy.CamelCase
                }));
        var unsupportedRejected = ThrowsInvalidData(
            () => SurfaceModelArtifactStore.Load(unsupportedPath),
            out var unsupportedEvidence);

        SurfaceModelArtifactStore.Save(artifactPath, first);
        var invalidSaveRejected = ThrowsInvalidData(
            () => SurfaceModelArtifactStore.Save(artifactPath, tamperedHash),
            out var invalidSaveEvidence);
        var preservedAfterRejectedSave =
            SurfaceModelArtifactStore.Load(artifactPath);

        var reversedSourceRejected = ThrowsInvalidData(
            () => SurfaceModelPreparation.Prepare(
                CreateReversedNormalMesh(),
                request),
            out var reversedSourceEvidence);
        var sparseSourceRejected = ThrowsInvalidData(
            () => SurfaceModelPreparation.Prepare(
                CreateSparseNormalMesh(),
                request),
            out var sparseSourceEvidence);

        var invalidPointReport =
            SurfaceModelArtifactValidator.Inspect(invalidPoint);
        var invalidIndexReport =
            SurfaceModelArtifactValidator.Inspect(invalidIndex);
        var degenerateReport =
            SurfaceModelArtifactValidator.Inspect(degenerateTriangle);
        var missingNormalReport =
            SurfaceModelArtifactValidator.Inspect(missingNormal);
        var nonUnitReport =
            SurfaceModelArtifactValidator.Inspect(nonUnitNormal);
        var reversedReport =
            SurfaceModelArtifactValidator.Inspect(reversedNormal);
        var invalidSampleReport =
            SurfaceModelArtifactValidator.Inspect(invalidSample);
        var tamperedHashReport =
            SurfaceModelArtifactValidator.Inspect(tamperedHash);

        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "identified-artifact-contract",
                first.SchemaVersion
                    == SurfaceModelArtifact.CurrentSchemaVersion
                && first.ArtifactId == "surface-model.nominal.square"
                && first.SourceEntityId == "source.mesh.nominal.square"
                && first.SourceContentSha256 == SourceSha256
                && first.Unit == "mm"
                && first.FrameId == "fixture-frame"
                && first.CoordinateConvention
                    == SurfaceModelArtifact.CurrentCoordinateConvention,
                $"{first.ArtifactId}|{first.SourceEntityId}|{first.Unit}|{first.FrameId}"),
            Check(
                "full-source-geometry-preserved",
                first.Points.Length == 4
                && first.Triangles.Length == 2
                && first.Normals.Length == 4
                && first.Points[2] == new SurfaceModelPoint3(2.0, 2.0, 0.0)
                && first.Triangles[1]
                    == new SurfaceModelTriangle(0, 2, 3),
                $"points={first.Points.Length};triangles={first.Triangles.Length};normals={first.Normals.Length}"),
            Check(
                "deterministic-sampling-parameters",
                first.Preparation == oneSampleParameters
                && first.Samples is
                [
                    {
                        Order: 0,
                        SourceTriangleIndex: 1,
                        Position: { X: 2.0 / 3.0, Y: 4.0 / 3.0, Z: 0.0 },
                        Normal: { X: 0.0, Y: 0.0, Z: 1.0 }
                    }
                ],
                $"policy={first.Preparation.SamplingPolicy};sample={first.Samples[0]}"),
            Check(
                "deterministic-content-hash",
                first.ContentSha256 == repeated.ContentSha256
                && first.ContentSha256.Length == 64,
                first.ContentSha256),
            Check(
                "sampling-parameter-changes-identity",
                twoSamples.Samples.Length == 2
                && twoSamples.Samples
                    .Select(sample => sample.SourceTriangleIndex)
                    .SequenceEqual([0, 1])
                && twoSamples.ContentSha256 != first.ContentSha256,
                $"one={first.ContentSha256};two={twoSamples.ContentSha256}"),
            Check(
                "known-valid-model",
                validity.IsValid
                && validity.FinitePointCount == 4
                && validity.IndexValidTriangleCount == 2
                && validity.NonDegenerateTriangleCount == 2
                && validity.UnitNormalCount == 4
                && validity.ConsistentNormalCornerCount == 6
                && validity.ValidSampleCount == 1
                && validity.ContentIdentityValid,
                validity.Evidence),
            Check(
                "save-load-content-identity",
                loaded.ContentSha256 == first.ContentSha256
                && loaded.ArtifactId == first.ArtifactId
                && loaded.Points.SequenceEqual(first.Points)
                && loaded.Triangles.SequenceEqual(first.Triangles)
                && loaded.Normals.SequenceEqual(first.Normals)
                && loaded.Samples.SequenceEqual(first.Samples),
                $"saved={first.ContentSha256};loaded={loaded.ContentSha256}"),
            Check(
                "atomic-overwrite-round-trip",
                overwritten.ContentSha256 == twoSamples.ContentSha256
                && !Directory.EnumerateFiles(
                        directory,
                        "*.tmp.*",
                        SearchOption.TopDirectoryOnly)
                    .Any(),
                overwritten.ContentSha256),
            Check(
                "non-finite-point-rejected",
                !invalidPointReport.IsValid
                && invalidPointReport.Errors.Any(error =>
                    error.Contains(
                        "points must all be finite",
                        StringComparison.Ordinal)),
                string.Join(" ", invalidPointReport.Errors)),
            Check(
                "out-of-range-index-rejected",
                !invalidIndexReport.IsValid
                && invalidIndexReport.Errors.Any(error =>
                    error.Contains(
                        "invalid or repeated point indices",
                        StringComparison.Ordinal)),
                string.Join(" ", invalidIndexReport.Errors)),
            Check(
                "degenerate-triangle-rejected",
                !degenerateReport.IsValid
                && degenerateReport.Errors.Any(error =>
                    error.Contains(
                        "degenerate or below the minimum area",
                        StringComparison.Ordinal)),
                string.Join(" ", degenerateReport.Errors)),
            Check(
                "missing-normal-rejected",
                !missingNormalReport.IsValid
                && missingNormalReport.Errors.Any(error =>
                    error.Contains(
                        "one declared normal per point",
                        StringComparison.Ordinal)),
                string.Join(" ", missingNormalReport.Errors)),
            Check(
                "non-unit-normal-rejected",
                !nonUnitReport.IsValid
                && nonUnitReport.Errors.Any(error =>
                    error.Contains(
                        "unit length",
                        StringComparison.Ordinal)),
                string.Join(" ", nonUnitReport.Errors)),
            Check(
                "reversed-normal-rejected",
                !reversedReport.IsValid
                && reversedReport.Errors.Any(error =>
                    error.Contains(
                        "triangle winding",
                        StringComparison.Ordinal)),
                string.Join(" ", reversedReport.Errors)),
            Check(
                "invalid-sample-locator-rejected",
                !invalidSampleReport.IsValid
                && invalidSampleReport.Errors.Any(error =>
                    error.Contains(
                        "expected usable triangle",
                        StringComparison.Ordinal)),
                string.Join(" ", invalidSampleReport.Errors)),
            Check(
                "tampered-content-hash-rejected",
                !tamperedHashReport.IsValid
                && !tamperedHashReport.ContentIdentityValid,
                string.Join(" ", tamperedHashReport.Errors)),
            Check(
                "malformed-json-rejected",
                corruptRejected,
                corruptEvidence),
            Check(
                "unsupported-schema-rejected",
                unsupportedRejected,
                unsupportedEvidence),
            Check(
                "rejected-save-preserves-prior-artifact",
                invalidSaveRejected
                && preservedAfterRejectedSave.ContentSha256
                    == first.ContentSha256,
                $"{invalidSaveEvidence}|preserved={preservedAfterRejectedSave.ContentSha256}"),
            Check(
                "reversed-source-normals-fail-closed",
                reversedSourceRejected
                && reversedSourceEvidence.Contains(
                    "dense, valid declared normals",
                    StringComparison.Ordinal),
                reversedSourceEvidence),
            Check(
                "sparse-source-normals-fail-closed",
                sparseSourceRejected
                && sparseSourceEvidence.Contains(
                    "dense, valid declared normals",
                    StringComparison.Ordinal),
                sparseSourceEvidence),
            Check(
                "preparation-does-not-mutate-source",
                mesh.Positions[2] == new Vector3(2.0f, 2.0f, 0.0f)
                && mesh.Normals.All(normal =>
                    normal == Vector3.UnitZ)
                && mesh.Indices.SequenceEqual([0, 1, 2, 0, 2, 3]),
                $"positions={mesh.Positions.Length};triangles={mesh.TriangleCount};declaredNormals={mesh.DeclaredNormalCount}")
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"SurfaceModelFoundationVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            $"Contract|schema={SurfaceModelArtifact.CurrentSchemaVersion}|coordinate={SurfaceModelArtifact.CurrentCoordinateConvention}|sampling={SurfaceModelPreparationParameters.DeterministicTriangleCentroidSampling}|sourceMutation=false|repair=false",
            $"Artifact|path={artifactPath}|id={first.ArtifactId}|sha256={first.ContentSha256}|points={first.Points.Length}|triangles={first.Triangles.Length}|normals={first.Normals.Length}|samples={first.Samples.Length}",
            $"Validity|{validity.Evidence}"
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine(
            $"SurfaceModel foundation verification: "
            + $"{(passed == cases.Count ? "PASS" : "FAIL")} "
            + $"({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static SurfaceModelArtifact Rehash(
        SurfaceModelArtifact model) =>
        model with
        {
            ContentSha256 =
                SurfaceModelArtifact.CalculateContentSha256(model)
        };

    private static ImportedMesh CreateValidMesh() =>
        ImportedMesh.CreateTriangleMesh(
            "known-valid-square.stl",
            "Known Valid Square",
            "STL",
            [
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(2.0f, 0.0f, 0.0f),
                new Vector3(2.0f, 2.0f, 0.0f),
                new Vector3(0.0f, 2.0f, 0.0f)
            ],
            [0, 1, 2, 0, 2, 3],
            [
                Vector3.UnitZ,
                Vector3.UnitZ,
                Vector3.UnitZ,
                Vector3.UnitZ
            ]);

    private static ImportedMesh CreateReversedNormalMesh() =>
        ImportedMesh.CreateTriangleMesh(
            "known-reversed-square.stl",
            "Known Reversed Square",
            "STL",
            CreateValidMesh().Positions.ToArray(),
            [0, 1, 2, 0, 2, 3],
            [
                -Vector3.UnitZ,
                -Vector3.UnitZ,
                -Vector3.UnitZ,
                -Vector3.UnitZ
            ]);

    private static ImportedMesh CreateSparseNormalMesh() =>
        ImportedMesh.CreateTriangleMesh(
            "known-sparse-square.glb",
            "Known Sparse Square",
            "GLB",
            CreateValidMesh().Positions.ToArray(),
            [0, 1, 2, 0, 2, 3],
            [
                Vector3.UnitZ,
                Vector3.UnitZ,
                Vector3.UnitZ,
                default
            ],
            [true, true, true, false]);

    private static SurfaceModelPreparationParameters CreateParameters(
        int maximumSampleCount) =>
        new(
            SurfaceModelPreparationParameters
                .DeterministicTriangleCentroidSampling,
            maximumSampleCount,
            1e-9,
            1e-6,
            0.9);

    private static SurfaceModelPreparationRequest CreateRequest(
        SurfaceModelPreparationParameters parameters) =>
        new(
            "surface-model.nominal.square",
            "Known Valid Square SurfaceModel",
            "source.mesh.nominal.square",
            SourceSha256,
            "mm",
            "fixture-frame",
            parameters);

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
