using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using NoahCoverage = Lib.ThreeD.FeatureExtraction.DeterministicSurfaceCoverageResult;
using NoahPoint = Lib.ThreeD.FeatureExtraction.ThreeDPoint;
using NoahPose = Lib.ThreeD.FeatureExtraction.RigidSurfacePose;
using NoahSample = Lib.ThreeD.FeatureExtraction.SurfaceMatchSample;
using NoahSearchOptions = Lib.ThreeD.FeatureExtraction.DeterministicRigidSurfacePoseSearchOptions;
using NoahMultipleSearchOptions = Lib.ThreeD.FeatureExtraction.DeterministicMultipleSurfaceMatchOptions;
using NoahSymmetry = Lib.ThreeD.FeatureExtraction.RigidPoseSymmetry;
using NoahSymmetryAxis = Lib.ThreeD.FeatureExtraction.RigidPoseSymmetryAxis;
using NoahSymmetryKind = Lib.ThreeD.FeatureExtraction.RigidPoseSymmetryKind;
using NoahSymmetryOptions = Lib.ThreeD.FeatureExtraction.RigidPoseSymmetryEquivalenceOptions;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict source/unit/frame-neutral adapter between Studio artifacts and the
/// vendored Library-Noah surface-matching kernel. It owns no matching math.
/// </summary>
internal static class LibraryNoahSurfaceMatching
{
    public static NoahSample[] ModelSamples(
        SurfaceModelArtifact model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Samples
            .Select(sample => new NoahSample(
                sample.Order,
                Point(sample.Position)))
            .ToArray();
    }

    public static NoahSample[] SceneSamples(
        PreparedSceneArtifact scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return scene.Samples
            .Select(sample => new NoahSample(
                sample.Order,
                Point(sample.Position)))
            .ToArray();
    }

    public static NoahPose Pose(RigidPose3D pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        return new NoahPose(
            pose.M11,
            pose.M12,
            pose.M13,
            pose.M21,
            pose.M22,
            pose.M23,
            pose.M31,
            pose.M32,
            pose.M33,
            pose.TranslationX,
            pose.TranslationY,
            pose.TranslationZ);
    }

    public static RigidPose3D Pose(
        NoahPose pose,
        string unit,
        string sourceFrameId,
        string targetFrameId)
    {
        ArgumentNullException.ThrowIfNull(pose);
        return new RigidPose3D(
            unit,
            sourceFrameId,
            targetFrameId,
            pose.M11,
            pose.M12,
            pose.M13,
            pose.M21,
            pose.M22,
            pose.M23,
            pose.M31,
            pose.M32,
            pose.M33,
            pose.TranslationX,
            pose.TranslationY,
            pose.TranslationZ);
    }

    public static NoahSearchOptions SearchOptions(
        RigidSurfacePoseSearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new NoahSearchOptions
        {
            MinimumRotationXDegrees =
                parameters.MinimumRotationXDegrees,
            MaximumRotationXDegrees =
                parameters.MaximumRotationXDegrees,
            RotationStepXDegrees =
                parameters.RotationStepXDegrees,
            MinimumRotationYDegrees =
                parameters.MinimumRotationYDegrees,
            MaximumRotationYDegrees =
                parameters.MaximumRotationYDegrees,
            RotationStepYDegrees =
                parameters.RotationStepYDegrees,
            MinimumRotationZDegrees =
                parameters.MinimumRotationZDegrees,
            MaximumRotationZDegrees =
                parameters.MaximumRotationZDegrees,
            RotationStepZDegrees =
                parameters.RotationStepZDegrees,
            MinimumTranslationX = parameters.MinimumTranslationX,
            MaximumTranslationX = parameters.MaximumTranslationX,
            MinimumTranslationY = parameters.MinimumTranslationY,
            MaximumTranslationY = parameters.MaximumTranslationY,
            MinimumTranslationZ = parameters.MinimumTranslationZ,
            MaximumTranslationZ = parameters.MaximumTranslationZ,
            MaximumCorrespondenceDistance =
                parameters.MaximumCorrespondenceDistance,
            MinimumMatchedSampleCount =
                parameters.MinimumMatchedSampleCount,
            MaximumCandidateCount = parameters.MaximumCandidateCount
        };
    }

    public static NoahMultipleSearchOptions MultipleSearchOptions(
        RigidSurfacePoseSearchParameters parameters,
        int maximumMatchCount,
        int maximumExpandedCandidateCount)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new NoahMultipleSearchOptions
        {
            PoseSearchOptions = SearchOptions(parameters),
            MaximumMatchCount = maximumMatchCount,
            MaximumExpandedCandidateCount = maximumExpandedCandidateCount
        };
    }

    public static NoahSymmetryOptions SymmetryEquivalenceOptions(
        SurfaceModelArtifact model,
        double maximumTranslationDifference,
        double maximumRotationDifferenceDegrees)
    {
        ArgumentNullException.ThrowIfNull(model);
        var declaration = model.Symmetry;
        if (declaration is null
            || declaration.Kind
                == SurfaceModelSymmetryDeclaration.NoneKind)
        {
            return new NoahSymmetryOptions
            {
                Symmetry = new NoahSymmetry(
                    NoahSymmetryKind.None,
                    NoahSymmetryAxis.None,
                    1),
                MaximumTranslationDifference =
                    maximumTranslationDifference,
                MaximumRotationDifferenceDegrees =
                    maximumRotationDifferenceDegrees,
                RigidTransformTolerance = 1e-9
            };
        }

        var axis = declaration.Axis switch
        {
            SurfaceModelSymmetryDeclaration.XAxis =>
                NoahSymmetryAxis.X,
            SurfaceModelSymmetryDeclaration.YAxis =>
                NoahSymmetryAxis.Y,
            SurfaceModelSymmetryDeclaration.ZAxis =>
                NoahSymmetryAxis.Z,
            _ => NoahSymmetryAxis.None
        };
        return new NoahSymmetryOptions
        {
            Symmetry = new NoahSymmetry(
                NoahSymmetryKind.DiscreteRotation,
                axis,
                declaration.Order),
            MaximumTranslationDifference =
                maximumTranslationDifference,
            MaximumRotationDifferenceDegrees =
                maximumRotationDifferenceDegrees,
            RigidTransformTolerance = 1e-9
        };
    }

    public static SurfaceCoverageEvaluation Coverage(
        NoahCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        if (!coverage.Success)
        {
            throw new InvalidDataException(coverage.Message);
        }

        if (!string.Equals(
                NoahCoverage.Semantics,
                SurfaceCoverageEvaluation.CurrentSemantics,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Library-Noah surface coverage semantics do not match the Studio contract.");
        }

        double? inlierRmse = coverage.HasInlierRmse
            ? coverage.InlierRmse
            : null;
        var matches = coverage.Matches
            .Select(match => new SurfaceCoverageMatch(
                match.ModelSampleOrder,
                match.SceneSampleOrder,
                match.Distance))
            .ToArray();
        var evidence =
            $"semantics={SurfaceCoverageEvaluation.CurrentSemantics};"
            + $"matched={coverage.MatchedModelSampleCount}/{coverage.ModelSampleCount};"
            + $"scene={coverage.SceneSampleCount};"
            + $"coverage={coverage.CoverageRatio:G17};"
            + $"rmse={(inlierRmse.HasValue ? inlierRmse.Value.ToString("G17", CultureInfo.InvariantCulture) : "unavailable")};"
            + $"maximumDistance={coverage.MaximumCorrespondenceDistance:G17}";
        return new SurfaceCoverageEvaluation(
            SurfaceCoverageEvaluation.CurrentSemantics,
            coverage.ModelSampleCount,
            coverage.SceneSampleCount,
            coverage.MatchedModelSampleCount,
            coverage.UnmatchedModelSampleCount,
            coverage.CoverageRatio,
            inlierRmse,
            coverage.MaximumCorrespondenceDistance,
            matches,
            evidence);
    }

    public static NoahPoint Point(SurfaceModelPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return new NoahPoint(point.X, point.Y, point.Z);
    }
}
