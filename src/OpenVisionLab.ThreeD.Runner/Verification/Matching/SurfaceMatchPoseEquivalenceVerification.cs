using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class SurfaceMatchPoseEquivalenceVerification
{
    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath)
                        ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var foundationReport = Path.Combine(
            directory,
            "pose-equivalence-surface-model-foundation.txt");
        if (SurfaceModelFoundationVerification.Run(foundationReport) != 0)
        {
            File.WriteAllText(
                fullReportPath,
                "SurfaceMatchPoseEquivalenceVerification|FAIL|SurfaceModel foundation failed",
                new UTF8Encoding(false));
            return 1;
        }

        var legacy = SurfaceModelArtifactStore.Load(Path.Combine(
            directory,
            "known-valid.surface-model.json"));
        var rotational = SurfaceModelArtifactStore.Load(Path.Combine(
            directory,
            "known-rotational-z4.surface-model.json"));
        var declaredNone = WithSymmetry(
            legacy,
            SurfaceModelSymmetryDeclaration.None);
        var x2 = WithSymmetry(
            legacy,
            new SurfaceModelSymmetryDeclaration(
                SurfaceModelSymmetryDeclaration.DiscreteRotationKind,
                SurfaceModelSymmetryDeclaration.XAxis,
                2));
        var y3 = WithSymmetry(
            legacy,
            new SurfaceModelSymmetryDeclaration(
                SurfaceModelSymmetryDeclaration.DiscreteRotationKind,
                SurfaceModelSymmetryDeclaration.YAxis,
                3));
        var reference = Pose(
            legacy.Unit,
            legacy.FrameId,
            "scene-frame",
            "x",
            30.0,
            10.0,
            -4.0,
            2.0);
        var z90 = ComposeModelRotation(reference, "z", 90.0);
        var z45 = ComposeModelRotation(reference, "z", 45.0);
        var translated = z90 with { TranslationX = 10.01 };

        var symmetric = SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
            rotational,
            reference,
            z90,
            1e-9,
            1e-6);
        var repeated = SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
            rotational,
            reference,
            z90,
            1e-9,
            1e-6);
        var nonEquivalent = SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
            rotational,
            reference,
            z45,
            1e-9,
            44.9);
        var translationRejected =
            SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
                rotational,
                reference,
                translated,
                0.009,
                1e-6);
        var legacyDirect = SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
            legacy,
            reference,
            z90,
            1e-9,
            89.9);
        var noneDirect = SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
            declaredNone,
            reference,
            z90,
            1e-9,
            89.9);
        var identity = Pose(
            legacy.Unit,
            legacy.FrameId,
            "scene-frame",
            "z",
            0.0,
            0.0,
            0.0,
            0.0);
        var xEquivalent = SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
            x2,
            identity,
            ComposeModelRotation(identity, "x", 180.0),
            1e-9,
            1e-6);
        var yEquivalent = SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
            y3,
            identity,
            ComposeModelRotation(identity, "y", 120.0),
            1e-9,
            1e-6);
        var cases = new List<Case>();
        void Check(string name, bool passed, string evidence) =>
            cases.Add(new Case(name, passed, evidence));

        Check(
            "z4-model-space-post-multiply-equivalent",
            symmetric.Equivalent
            && symmetric.SymmetryOperationIndex == 1
            && Near(symmetric.SymmetryOperationAngleDegrees, 90.0)
            && symmetric.TranslationDifference <= 1e-12
            && symmetric.RotationDifferenceDegrees <= 1e-6,
            symmetric.Evidence);
        Check(
            "equivalence-evidence-deterministic",
            symmetric == repeated,
            repeated.Evidence);
        Check(
            "z4-half-step-not-equivalent",
            !nonEquivalent.Equivalent
            && nonEquivalent.SymmetryOperationIndex == 0
            && Near(nonEquivalent.RotationDifferenceDegrees, 45.0),
            nonEquivalent.Evidence);
        Check(
            "translation-limit-independent",
            !translationRejected.Equivalent
            && Near(translationRejected.TranslationDifference, 0.01),
            translationRejected.Evidence);
        Check(
            "legacy-undeclared-preserves-direct-comparison",
            !legacyDirect.Equivalent
            && legacyDirect.SymmetryKind
                == SurfaceModelSymmetryDeclaration.NoneKind
            && legacyDirect.SymmetryOperationIndex == 0,
            legacyDirect.Evidence);
        Check(
            "declared-none-preserves-direct-comparison",
            !noneDirect.Equivalent
            && noneDirect.SymmetryKind
                == SurfaceModelSymmetryDeclaration.NoneKind
            && noneDirect.SymmetryOperationIndex == 0,
            noneDirect.Evidence);
        Check(
            "x2-mapping-equivalent",
            xEquivalent.Equivalent
            && xEquivalent.SymmetryAxis
                == SurfaceModelSymmetryDeclaration.XAxis
            && xEquivalent.SymmetryOperationIndex == 1,
            xEquivalent.Evidence);
        Check(
            "y3-mapping-equivalent",
            yEquivalent.Equivalent
            && yEquivalent.SymmetryAxis
                == SurfaceModelSymmetryDeclaration.YAxis
            && yEquivalent.SymmetryOperationIndex == 1,
            yEquivalent.Evidence);
        Check(
            "model-identity-retained",
            symmetric.ModelContentSha256 == rotational.ContentSha256
            && symmetric.Unit == rotational.Unit
            && symmetric.SourceFrameId == rotational.FrameId
            && symmetric.TargetFrameId == reference.TargetFrameId,
            $"model={symmetric.ModelContentSha256};unit={symmetric.Unit};frames={symmetric.SourceFrameId}->{symmetric.TargetFrameId}");
        Check(
            "input-state-not-mutated",
            reference == Pose(
                legacy.Unit,
                legacy.FrameId,
                "scene-frame",
                "x",
                30.0,
                10.0,
                -4.0,
                2.0)
            && rotational.Symmetry?.Order == 4,
            "poses=model declarations remain immutable");
        Check(
            "mismatched-unit-rejected",
            ThrowsInvalidData(() =>
                SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
                    rotational,
                    reference,
                    z90 with { Unit = "inch" },
                    1e-9,
                    1e-6)),
            "candidateUnit=inch");
        Check(
            "mismatched-source-frame-rejected",
            ThrowsInvalidData(() =>
                SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
                    rotational,
                    reference,
                    z90 with { SourceFrameId = "other-model" },
                    1e-9,
                    1e-6)),
            "candidateSourceFrame=other-model");
        Check(
            "mismatched-target-frame-rejected",
            ThrowsInvalidData(() =>
                SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
                    rotational,
                    reference,
                    z90 with { TargetFrameId = "other-scene" },
                    1e-9,
                    1e-6)),
            "candidateTargetFrame=other-scene");
        Check(
            "invalid-limit-rejected",
            ThrowsInvalidData(() =>
                SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
                    rotational,
                    reference,
                    z90,
                    -1.0,
                    181.0)),
            "translation=-1;rotation=181");
        Check(
            "tampered-model-rejected",
            ThrowsInvalidData(() =>
                SurfaceMatchPoseEquivalenceEvaluator.Evaluate(
                    rotational with
                    {
                        ContentSha256 = new string('F', 64)
                    },
                    reference,
                    z90,
                    1e-9,
                    1e-6)),
            "tamperedModel=true");

        var passedCount = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"SurfaceMatchPoseEquivalenceVerification|{(passedCount == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passedCount}|failed={cases.Count - passedCount}",
            "Contract|semantics=model-declared-cyclic-rigid-pose-equivalence-v1|composition=reference-pose-times-model-symmetry|translation=model-origin-distance|rotation=geodesic-angle|none=direct-comparison|ui=false|matchingExecution=false",
            $"SdkPackage|version={VisionSdkHeightMapInspection.PackageVersion}|sourceCommit={VisionSdkHeightMapInspection.PackageSourceCommit}",
            $"Fixture|model={rotational.ContentSha256}|symmetry={rotational.Symmetry?.Kind}:{rotational.Symmetry?.Axis}:{rotational.Symmetry?.Order}|reference=Rx30|candidate=Rx30*Rz90"
        };
        lines.AddRange(cases.Select(item =>
            $"{(item.Passed ? "PASS" : "FAIL")} | {item.Name} | {item.Evidence}"));
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(false));
        Console.WriteLine(
            $"Surface Match pose equivalence verification: "
            + $"{(passedCount == cases.Count ? "PASS" : "FAIL")} "
            + $"({passedCount}/{cases.Count})");
        return passedCount == cases.Count ? 0 : 1;
    }

    private static SurfaceModelArtifact WithSymmetry(
        SurfaceModelArtifact source,
        SurfaceModelSymmetryDeclaration symmetry) =>
        SurfaceModelArtifact.Create(
            source.ArtifactId,
            source.Name,
            source.SourceEntityId,
            source.SourceContentSha256,
            source.SourceFormat,
            source.Unit,
            source.FrameId,
            source.Preparation,
            source.Points,
            source.Triangles,
            source.Normals,
            source.Samples,
            symmetry);

    private static RigidPose3D Pose(
        string unit,
        string sourceFrameId,
        string targetFrameId,
        string axis,
        double angleDegrees,
        double translationX,
        double translationY,
        double translationZ)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return axis switch
        {
            "x" => new RigidPose3D(
                unit,
                sourceFrameId,
                targetFrameId,
                1.0, 0.0, 0.0,
                0.0, cosine, -sine,
                0.0, sine, cosine,
                translationX, translationY, translationZ),
            "y" => new RigidPose3D(
                unit,
                sourceFrameId,
                targetFrameId,
                cosine, 0.0, sine,
                0.0, 1.0, 0.0,
                -sine, 0.0, cosine,
                translationX, translationY, translationZ),
            _ => new RigidPose3D(
                unit,
                sourceFrameId,
                targetFrameId,
                cosine, -sine, 0.0,
                sine, cosine, 0.0,
                0.0, 0.0, 1.0,
                translationX, translationY, translationZ)
        };
    }

    private static RigidPose3D ComposeModelRotation(
        RigidPose3D reference,
        string axis,
        double angleDegrees)
    {
        var symmetry = Pose(
            reference.Unit,
            reference.SourceFrameId,
            reference.TargetFrameId,
            axis,
            angleDegrees,
            0.0,
            0.0,
            0.0);
        return new RigidPose3D(
            reference.Unit,
            reference.SourceFrameId,
            reference.TargetFrameId,
            reference.M11 * symmetry.M11
                + reference.M12 * symmetry.M21
                + reference.M13 * symmetry.M31,
            reference.M11 * symmetry.M12
                + reference.M12 * symmetry.M22
                + reference.M13 * symmetry.M32,
            reference.M11 * symmetry.M13
                + reference.M12 * symmetry.M23
                + reference.M13 * symmetry.M33,
            reference.M21 * symmetry.M11
                + reference.M22 * symmetry.M21
                + reference.M23 * symmetry.M31,
            reference.M21 * symmetry.M12
                + reference.M22 * symmetry.M22
                + reference.M23 * symmetry.M32,
            reference.M21 * symmetry.M13
                + reference.M22 * symmetry.M23
                + reference.M23 * symmetry.M33,
            reference.M31 * symmetry.M11
                + reference.M32 * symmetry.M21
                + reference.M33 * symmetry.M31,
            reference.M31 * symmetry.M12
                + reference.M32 * symmetry.M22
                + reference.M33 * symmetry.M32,
            reference.M31 * symmetry.M13
                + reference.M32 * symmetry.M23
                + reference.M33 * symmetry.M33,
            reference.TranslationX,
            reference.TranslationY,
            reference.TranslationZ);
    }

    private static bool Near(double actual, double expected) =>
        Math.Abs(actual - expected) <= 1e-9;

    private static bool ThrowsInvalidData(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    private sealed record Case(
        string Name,
        bool Passed,
        string Evidence);
}
