using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using SdkCoverage = OpenVisionLab.Vision3D.FeatureExtraction.DeterministicSurfaceCoverageResult;
using SdkPoint = OpenVisionLab.Vision3D.FeatureExtraction.ThreeDPoint;
using SdkPose = OpenVisionLab.Vision3D.FeatureExtraction.RigidSurfacePose;
using SdkSample = OpenVisionLab.Vision3D.FeatureExtraction.SurfaceMatchSample;
using SdkSearchOptions = OpenVisionLab.Vision3D.FeatureExtraction.DeterministicRigidSurfacePoseSearchOptions;
using SdkMultipleSearchOptions = OpenVisionLab.Vision3D.FeatureExtraction.DeterministicMultipleSurfaceMatchOptions;
using SdkSymmetry = OpenVisionLab.Vision3D.FeatureExtraction.RigidPoseSymmetry;
using SdkSymmetryAxis = OpenVisionLab.Vision3D.FeatureExtraction.RigidPoseSymmetryAxis;
using SdkSymmetryKind = OpenVisionLab.Vision3D.FeatureExtraction.RigidPoseSymmetryKind;
using SdkSymmetryOptions = OpenVisionLab.Vision3D.FeatureExtraction.RigidPoseSymmetryEquivalenceOptions;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict source/unit/frame-neutral adapter between Studio artifacts and the
/// vendored OpenVisionLab Vision SDK surface-matching kernel. It owns no matching math.
/// </summary>
internal static class VisionSdkSurfaceMatching
{
    public static SdkSample[] ModelSamples(
        SurfaceModelArtifact model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Samples
            .Select(sample => new SdkSample(
                sample.Order,
                Point(sample.Position)))
            .ToArray();
    }

    public static SdkSample[] SceneSamples(
        PreparedSceneArtifact scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return scene.Samples
            .Select(sample => new SdkSample(
                sample.Order,
                Point(sample.Position)))
            .ToArray();
    }

    public static SdkPose Pose(RigidPose3D pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        return new SdkPose(
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
        SdkPose pose,
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

    public static SdkSearchOptions SearchOptions(
        RigidSurfacePoseSearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new SdkSearchOptions
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

    public static SdkMultipleSearchOptions MultipleSearchOptions(
        RigidSurfacePoseSearchParameters parameters,
        int maximumMatchCount,
        int maximumExpandedCandidateCount)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new SdkMultipleSearchOptions
        {
            PoseSearchOptions = SearchOptions(parameters),
            MaximumMatchCount = maximumMatchCount,
            MaximumExpandedCandidateCount = maximumExpandedCandidateCount
        };
    }

    public static SdkSymmetryOptions SymmetryEquivalenceOptions(
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
            return new SdkSymmetryOptions
            {
                Symmetry = new SdkSymmetry(
                    SdkSymmetryKind.None,
                    SdkSymmetryAxis.None,
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
                SdkSymmetryAxis.X,
            SurfaceModelSymmetryDeclaration.YAxis =>
                SdkSymmetryAxis.Y,
            SurfaceModelSymmetryDeclaration.ZAxis =>
                SdkSymmetryAxis.Z,
            _ => SdkSymmetryAxis.None
        };
        return new SdkSymmetryOptions
        {
            Symmetry = new SdkSymmetry(
                SdkSymmetryKind.DiscreteRotation,
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
        SdkCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        if (!coverage.Success)
        {
            throw new InvalidDataException(coverage.Message);
        }

        if (!string.Equals(
                SdkCoverage.Semantics,
                SurfaceCoverageEvaluation.CurrentSemantics,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "OpenVisionLab Vision SDK surface coverage semantics do not match the Studio contract.");
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

    public static SdkPoint Point(SurfaceModelPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return new SdkPoint(point.X, point.Y, point.Z);
    }
}
