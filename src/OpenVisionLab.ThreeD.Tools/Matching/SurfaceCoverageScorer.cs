using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Studio identity and contract adapter for OpenVisionLab Vision SDK's decision-free
/// one-way nominal-surface coverage algorithm.
/// </summary>
public static class SurfaceCoverageScorer
{
    public static SurfaceCoverageEvaluation Evaluate(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidPose3D pose,
        double maximumCorrespondenceDistance)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(pose);

        var modelValidity =
            SurfaceModelArtifactValidator.Inspect(model);
        if (!modelValidity.IsValid)
        {
            throw new InvalidDataException(
                "Surface coverage requires a valid SurfaceModel.");
        }

        var sceneValidity =
            PreparedSceneArtifactValidator.Inspect(scene);
        if (!sceneValidity.IsValid)
        {
            throw new InvalidDataException(
                "Surface coverage requires a valid Prepared Scene.");
        }

        if (!string.Equals(
                model.Unit,
                scene.Unit,
                StringComparison.Ordinal)
            || !string.Equals(
                model.Unit,
                pose.Unit,
                StringComparison.Ordinal)
            || !string.Equals(
                model.FrameId,
                pose.SourceFrameId,
                StringComparison.Ordinal)
            || !string.Equals(
                scene.FrameId,
                pose.TargetFrameId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Surface coverage requires matching units and explicit model-to-scene frames.");
        }

        if (!pose.IsRigid(1e-9))
        {
            throw new InvalidDataException(
                "Surface coverage requires a finite rigid pose.");
        }

        if (!double.IsFinite(maximumCorrespondenceDistance)
            || maximumCorrespondenceDistance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCorrespondenceDistance),
                "Surface coverage distance must be finite and positive.");
        }

        var result = new DeterministicSurfaceCoverageTool()
            .Execute(
                VisionSdkSurfaceMatching.ModelSamples(model),
                VisionSdkSurfaceMatching.SceneSamples(scene),
                VisionSdkSurfaceMatching.Pose(pose),
                maximumCorrespondenceDistance);
        return VisionSdkSurfaceMatching.Coverage(result);
    }
}
