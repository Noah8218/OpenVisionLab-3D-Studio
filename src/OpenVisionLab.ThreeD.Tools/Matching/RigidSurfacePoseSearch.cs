using Lib.ThreeD.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Studio artifact/identity adapter for Library-Noah's deterministic bounded
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

        var noahResult =
            new DeterministicRigidSurfacePoseSearchTool()
                .Execute(
                    LibraryNoahSurfaceMatching.ModelSamples(model),
                    LibraryNoahSurfaceMatching.SceneSamples(scene),
                    LibraryNoahSurfaceMatching.SearchOptions(parameters));
        if (!noahResult.Success)
        {
            throw new InvalidDataException(noahResult.Message);
        }

        var coverage =
            LibraryNoahSurfaceMatching.Coverage(
                noahResult.Coverage);
        var state = noahResult.Matched
            ? RigidSurfacePoseSearchState.Matched
            : RigidSurfacePoseSearchState.NoMatch;
        var pose = noahResult.Pose is null
            ? null
            : LibraryNoahSurfaceMatching.Pose(
                noahResult.Pose,
                model.Unit,
                model.FrameId,
                scene.FrameId);
        return RigidSurfacePoseSearchResult.Create(
            model.ContentSha256,
            scene.ContentSha256,
            parameters,
            state,
            noahResult.EvaluatedCandidateCount,
            pose,
            coverage,
            noahResult.RejectionReason);
    }
}
