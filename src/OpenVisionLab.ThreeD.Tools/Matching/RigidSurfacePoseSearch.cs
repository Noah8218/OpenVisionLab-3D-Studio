using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Studio artifact/identity adapter for OpenVisionLab Vision SDK's deterministic bounded
/// rigid-surface pose search. This class owns no pose or coverage arithmetic.
/// </summary>
public static class RigidSurfacePoseSearch
{
    public const int AbsoluteMaximumCandidateCount =
        DeterministicRigidSurfacePoseSearchTool
            .AbsoluteMaximumCandidateCount;

    public static RigidSurfacePoseSearchResult Execute(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidSurfacePoseSearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(parameters);

        var modelValidity =
            SurfaceModelArtifactValidator.Inspect(model);
        if (!modelValidity.IsValid)
        {
            throw new InvalidDataException(
                "Rigid pose search requires a valid SurfaceModel.");
        }

        var sceneValidity =
            PreparedSceneArtifactValidator.Inspect(scene);
        if (!sceneValidity.IsValid)
        {
            throw new InvalidDataException(
                "Rigid pose search requires a valid Prepared Scene.");
        }

        if (!string.Equals(
                model.Unit,
                scene.Unit,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Rigid pose search requires identical explicit units.");
        }

        var parameterValidity =
            RigidSurfacePoseSearchParameterValidator.Inspect(
                parameters);
        if (!parameterValidity.IsValid)
        {
            throw new InvalidDataException(
                string.Join(" ", parameterValidity.Errors));
        }

        if (parameters.MinimumMatchedSampleCount
                > model.Samples.Length
            || parameters.MinimumMatchedSampleCount
                > scene.Samples.Length)
        {
            throw new InvalidDataException(
                "Rigid pose search minimum matched sample count exceeds the available model or scene samples.");
        }

        var sdkResult =
            new DeterministicRigidSurfacePoseSearchTool()
                .Execute(
                    VisionSdkSurfaceMatching.ModelSamples(model),
                    VisionSdkSurfaceMatching.SceneSamples(scene),
                    VisionSdkSurfaceMatching.SearchOptions(parameters));
        if (!sdkResult.Success)
        {
            throw new InvalidDataException(sdkResult.Message);
        }

        var coverage =
            VisionSdkSurfaceMatching.Coverage(
                sdkResult.Coverage);
        var state = sdkResult.Matched
            ? RigidSurfacePoseSearchState.Matched
            : RigidSurfacePoseSearchState.NoMatch;
        var pose = sdkResult.Pose is null
            ? null
            : VisionSdkSurfaceMatching.Pose(
                sdkResult.Pose,
                model.Unit,
                model.FrameId,
                scene.FrameId);
        return RigidSurfacePoseSearchResult.Create(
            model.ContentSha256,
            scene.ContentSha256,
            parameters,
            state,
            sdkResult.EvaluatedCandidateCount,
            pose,
            coverage,
            sdkResult.RejectionReason);
    }
}
