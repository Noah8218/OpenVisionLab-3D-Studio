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
        var declaredNone = SurfaceModelPreparation.Prepare(
            mesh,
            request with
            {
                Symmetry = SurfaceModelSymmetryDeclaration.None
            });
        var rotational = SurfaceModelPreparation.Prepare(
            mesh,
            request with
            {
                Symmetry = new SurfaceModelSymmetryDeclaration(
                    SurfaceModelSymmetryDeclaration.DiscreteRotationKind,
                    SurfaceModelSymmetryDeclaration.ZAxis,
                    4)
            });
        var validity = SurfaceModelArtifactValidator.Inspect(first);
        var declaredNoneValidity =
            SurfaceModelArtifactValidator.Inspect(declaredNone);
        var rotationalValidity =
            SurfaceModelArtifactValidator.Inspect(rotational);
        var artifactPath = Path.Combine(
            directory,
            "known-valid.surface-model.json");
        var rotationalArtifactPath = Path.Combine(
            directory,
            "known-rotational-z4.surface-model.json");

        SurfaceModelArtifactStore.Save(artifactPath, first);
        SurfaceModelArtifactStore.Save(
            rotationalArtifactPath,
            rotational);
        var loadedRotational = SurfaceModelArtifactStore.Load(
            rotationalArtifactPath);
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
        var missingCurrentSymmetry = Rehash(declaredNone with
        {
            Symmetry = null
        });
        var invalidNoneShape = Rehash(declaredNone with
        {
            Symmetry = new SurfaceModelSymmetryDeclaration(
                SurfaceModelSymmetryDeclaration.NoneKind,
                SurfaceModelSymmetryDeclaration.ZAxis,
                1)
        });
        var unsupportedSymmetryKind = Rehash(declaredNone with
        {
            Symmetry = new SurfaceModelSymmetryDeclaration(
                "mirror",
                SurfaceModelSymmetryDeclaration.NoAxis,
                1)
        });
        var invalidRotationalAxis = Rehash(rotational with
        {
            Symmetry = rotational.Symmetry! with { Axis = "diagonal" }
        });
        var invalidRotationalOrder = Rehash(rotational with
        {
            Symmetry = rotational.Symmetry! with { Order = 1 }
        });
        var legacyWithSymmetry = Rehash(first with
        {
            Symmetry = SurfaceModelSymmetryDeclaration.None
        });
        var tamperedSymmetry = declaredNone with
        {
            Symmetry = rotational.Symmetry
        };

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
        var missingCurrentSymmetryReport =
            SurfaceModelArtifactValidator.Inspect(missingCurrentSymmetry);
        var invalidNoneShapeReport =
            SurfaceModelArtifactValidator.Inspect(invalidNoneShape);
        var unsupportedSymmetryKindReport =
            SurfaceModelArtifactValidator.Inspect(
                unsupportedSymmetryKind);
        var invalidRotationalAxisReport =
            SurfaceModelArtifactValidator.Inspect(invalidRotationalAxis);
        var invalidRotationalOrderReport =
            SurfaceModelArtifactValidator.Inspect(invalidRotationalOrder);
        var legacyWithSymmetryReport =
            SurfaceModelArtifactValidator.Inspect(legacyWithSymmetry);
        var tamperedSymmetryReport =
            SurfaceModelArtifactValidator.Inspect(tamperedSymmetry);

        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "identified-artifact-contract",
                first.SchemaVersion
                    == SurfaceModelArtifact.LegacySchemaVersion
                && first.Symmetry is null
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
                "legacy-schema-remains-content-and-json-compatible",
                first.SchemaVersion
                    == SurfaceModelArtifact.LegacySchemaVersion
                && first.Symmetry is null
                && validity.SymmetryDeclarationValid
                && validity.SymmetryEvidence
                    == "schema-1.0-undeclared"
                && loaded.ContentSha256 == first.ContentSha256
                && !File.ReadAllText(artifactPath).Contains(
                    "\"symmetry\"",
                    StringComparison.Ordinal),
                $"schema={first.SchemaVersion};symmetry={validity.SymmetryEvidence};sha256={first.ContentSha256}"),
            Check(
                "symmetry-schema-explicit-none-declaration-valid",
                declaredNone.SchemaVersion
                    == SurfaceModelArtifact.SymmetrySchemaVersion
                && declaredNone.Symmetry
                    == SurfaceModelSymmetryDeclaration.None
                && declaredNoneValidity.IsValid
                && declaredNoneValidity.SymmetryDeclarationValid
                && declaredNoneValidity.SymmetryEvidence == "none",
                $"schema={declaredNone.SchemaVersion};symmetry={declaredNoneValidity.SymmetryEvidence};sha256={declaredNone.ContentSha256}"),
            Check(
                "symmetry-schema-discrete-rotation-valid",
                rotational.SchemaVersion
                    == SurfaceModelArtifact.SymmetrySchemaVersion
                && rotational.Symmetry is
                {
                    Kind: SurfaceModelSymmetryDeclaration.DiscreteRotationKind,
                    Axis: SurfaceModelSymmetryDeclaration.ZAxis,
                    Order: 4
                }
                && rotationalValidity.IsValid
                && rotationalValidity.SymmetryDeclarationValid
                && rotationalValidity.SymmetryEvidence
                    == "discrete-rotation:z:4",
                $"schema={rotational.SchemaVersion};symmetry={rotationalValidity.SymmetryEvidence};sha256={rotational.ContentSha256}"),
            Check(
                "symmetry-declaration-changes-content-identity",
                declaredNone.ContentSha256 != first.ContentSha256
                && rotational.ContentSha256 != first.ContentSha256
                && rotational.ContentSha256
                    != declaredNone.ContentSha256,
                $"legacy={first.ContentSha256};none={declaredNone.ContentSha256};z4={rotational.ContentSha256}"),
            Check(
                "symmetry-save-load-round-trip",
                loadedRotational.ContentSha256
                    == rotational.ContentSha256
                && loadedRotational.Symmetry == rotational.Symmetry
                && loadedRotational.Points.SequenceEqual(
                    rotational.Points)
                && loadedRotational.Samples.SequenceEqual(
                    rotational.Samples)
                && File.ReadAllText(rotationalArtifactPath).Contains(
                    "\"symmetry\"",
                    StringComparison.Ordinal),
                $"path={rotationalArtifactPath};symmetry={loadedRotational.Symmetry};sha256={loadedRotational.ContentSha256}"),
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
                && validity.SymmetryDeclarationValid
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
                "current-schema-missing-symmetry-rejected",
                !missingCurrentSymmetryReport.IsValid
                && !missingCurrentSymmetryReport
                    .SymmetryDeclarationValid
                && missingCurrentSymmetryReport.Errors.Any(error =>
                    error.Contains(
                        "requires a symmetry declaration",
                        StringComparison.Ordinal)),
                string.Join(" ", missingCurrentSymmetryReport.Errors)),
            Check(
                "none-symmetry-shape-rejected",
                !invalidNoneShapeReport.IsValid
                && !invalidNoneShapeReport.SymmetryDeclarationValid
                && invalidNoneShapeReport.Errors.Any(error =>
                    error.Contains(
                        "axis 'none' and order 1",
                        StringComparison.Ordinal)),
                string.Join(" ", invalidNoneShapeReport.Errors)),
            Check(
                "unsupported-symmetry-kind-rejected",
                !unsupportedSymmetryKindReport.IsValid
                && !unsupportedSymmetryKindReport
                    .SymmetryDeclarationValid
                && unsupportedSymmetryKindReport.Errors.Any(error =>
                    error.Contains(
                        "symmetry kind 'mirror' is unsupported",
                        StringComparison.Ordinal)),
                string.Join(" ", unsupportedSymmetryKindReport.Errors)),
            Check(
                "invalid-rotational-axis-rejected",
                !invalidRotationalAxisReport.IsValid
                && !invalidRotationalAxisReport
                    .SymmetryDeclarationValid
                && invalidRotationalAxisReport.Errors.Any(error =>
                    error.Contains(
                        "model axis x, y, or z",
                        StringComparison.Ordinal)),
                string.Join(" ", invalidRotationalAxisReport.Errors)),
            Check(
                "invalid-rotational-order-rejected",
                !invalidRotationalOrderReport.IsValid
                && !invalidRotationalOrderReport
                    .SymmetryDeclarationValid
                && invalidRotationalOrderReport.Errors.Any(error =>
                    error.Contains(
                        "order at least 2",
                        StringComparison.Ordinal)),
                string.Join(" ", invalidRotationalOrderReport.Errors)),
            Check(
                "legacy-schema-with-symmetry-rejected",
                !legacyWithSymmetryReport.IsValid
                && !legacyWithSymmetryReport
                    .SymmetryDeclarationValid
                && legacyWithSymmetryReport.Errors.Any(error =>
                    error.Contains(
                        "schema 1.0 cannot contain",
                        StringComparison.Ordinal)),
                string.Join(" ", legacyWithSymmetryReport.Errors)),
            Check(
                "tampered-symmetry-content-hash-rejected",
                !tamperedSymmetryReport.IsValid
                && tamperedSymmetryReport.SymmetryDeclarationValid
                && !tamperedSymmetryReport.ContentIdentityValid,
                string.Join(" ", tamperedSymmetryReport.Errors)),
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
            $"Contract|legacySchema={SurfaceModelArtifact.LegacySchemaVersion}|symmetrySchema={SurfaceModelArtifact.SymmetrySchemaVersion}|currentSchema={SurfaceModelArtifact.CurrentSchemaVersion}|symmetry=none,discrete-rotation(x|y|z,order>=2)|surfaceSelection=optional-in-current-schema|coordinate={SurfaceModelArtifact.CurrentCoordinateConvention}|sampling={SurfaceModelPreparationParameters.DeterministicTriangleCentroidSampling}|sourceMutation=false|repair=false|poseEquivalence=false",
            $"Artifact|path={artifactPath}|id={first.ArtifactId}|sha256={first.ContentSha256}|points={first.Points.Length}|triangles={first.Triangles.Length}|normals={first.Normals.Length}|samples={first.Samples.Length}",
            $"SymmetryArtifact|path={rotationalArtifactPath}|id={rotational.ArtifactId}|sha256={rotational.ContentSha256}|kind={rotational.Symmetry!.Kind}|axis={rotational.Symmetry.Axis}|order={rotational.Symmetry.Order}",
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
