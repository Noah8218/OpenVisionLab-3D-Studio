using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict Studio identity and acceptance adapter over OpenVisionLab Vision SDK's
/// deterministic multiple-instance matching Tool. It owns no search arithmetic.
/// </summary>
public static class MultipleSurfaceMatchEvaluationExecutor
{
    public const int AbsoluteMaximumMatchCount =
        DeterministicMultipleSurfaceMatchTool.AbsoluteMaximumMatchCount;
    public const int AbsoluteMaximumExpandedCandidateCount =
        DeterministicMultipleSurfaceMatchTool
            .AbsoluteMaximumExpandedCandidateCount;

    public static SurfaceMatchCollectionArtifact Execute(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidSurfacePoseSearchParameters searchParameters,
        SurfaceMatchAcceptancePolicy acceptancePolicy,
        int maximumMatchCount,
        int maximumExpandedCandidateCount)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(searchParameters);
        ArgumentNullException.ThrowIfNull(acceptancePolicy);
        if (!SurfaceModelArtifactValidator.Inspect(model).IsValid
            || !PreparedSceneArtifactValidator.Inspect(scene).IsValid
            || !string.Equals(model.Unit, scene.Unit, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Multiple Surface Match requires valid model and scene artifacts with identical explicit units.");
        }

        var searchValidity =
            RigidSurfacePoseSearchParameterValidator.Inspect(
                searchParameters);
        if (!searchValidity.IsValid)
        {
            throw new InvalidDataException(
                string.Join(" ", searchValidity.Errors));
        }

        var sdkResult = new DeterministicMultipleSurfaceMatchTool().Execute(
            VisionSdkSurfaceMatching.ModelSamples(model),
            VisionSdkSurfaceMatching.SceneSamples(scene),
            VisionSdkSurfaceMatching.MultipleSearchOptions(
                searchParameters,
                maximumMatchCount,
                maximumExpandedCandidateCount));
        if (!sdkResult.Success)
        {
            throw new InvalidDataException(sdkResult.Message);
        }

        var items = sdkResult.Matches.Select(match =>
        {
            var coverage = VisionSdkSurfaceMatching.Coverage(
                match.Coverage);
            var pose = VisionSdkSurfaceMatching.Pose(
                match.Pose,
                model.Unit,
                model.FrameId,
                scene.FrameId);
            var poseResult = RigidSurfacePoseSearchResult.Create(
                model.ContentSha256,
                scene.ContentSha256,
                searchParameters,
                RigidSurfacePoseSearchState.Matched,
                match.EvaluatedCandidateCount,
                pose,
                coverage,
                string.Empty);
            var execution = SurfaceMatchExecutionArtifact.Create(
                model,
                scene,
                poseResult);
            var expected = SurfaceMatchAssessmentArtifactValidator
                .ExpectedDecision(
                    poseResult.State,
                    coverage.CoverageRatio,
                    coverage.InlierRmse,
                    acceptancePolicy);
            var assessment = SurfaceMatchAssessmentArtifact.Create(
                execution,
                acceptancePolicy,
                expected.Decision,
                expected.Reason);
            return SurfaceMatchCollectionItem.Create(
                match.Order,
                execution,
                assessment);
        }).ToArray();
        return SurfaceMatchCollectionArtifact.Create(
            model,
            scene,
            searchParameters,
            acceptancePolicy,
            maximumMatchCount,
            maximumExpandedCandidateCount,
            sdkResult.EvaluatedCandidateCount,
            sdkResult.StopReason,
            items);
    }
}
