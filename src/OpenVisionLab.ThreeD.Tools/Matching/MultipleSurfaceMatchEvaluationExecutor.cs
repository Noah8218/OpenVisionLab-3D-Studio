using Lib.ThreeD.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict Studio identity and acceptance adapter over Library-Noah's
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

        var noahResult = new DeterministicMultipleSurfaceMatchTool().Execute(
            LibraryNoahSurfaceMatching.ModelSamples(model),
            LibraryNoahSurfaceMatching.SceneSamples(scene),
            LibraryNoahSurfaceMatching.MultipleSearchOptions(
                searchParameters,
                maximumMatchCount,
                maximumExpandedCandidateCount));
        if (!noahResult.Success)
        {
            throw new InvalidDataException(noahResult.Message);
        }

        var items = noahResult.Matches.Select(match =>
        {
            var coverage = LibraryNoahSurfaceMatching.Coverage(
                match.Coverage);
            var pose = LibraryNoahSurfaceMatching.Pose(
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
            noahResult.EvaluatedCandidateCount,
            noahResult.StopReason,
            items);
    }
}
