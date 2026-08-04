using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public sealed record SurfaceMatchCollectionItem(
    int Order,
    string MatchId,
    SurfaceMatchExecutionArtifact Execution,
    SurfaceMatchAssessmentArtifact Assessment)
{
    public static SurfaceMatchCollectionItem Create(
        int order,
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchAssessmentArtifact assessment)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(assessment);
        return new SurfaceMatchCollectionItem(
            order,
            MatchIdFor(execution),
            execution,
            assessment);
    }

    public static string MatchIdFor(
        SurfaceMatchExecutionArtifact execution)
    {
        ArgumentNullException.ThrowIfNull(execution);
        return $"match.surface.{execution.PoseResult.ContentSha256}";
    }
}

/// <summary>
/// Identified deterministic collection of disjoint Surface Match results.
/// Selection is presentation state and is intentionally excluded.
/// </summary>
public sealed record SurfaceMatchCollectionArtifact(
    string SchemaVersion,
    string Semantics,
    string CollectionId,
    string ModelContentSha256,
    string SceneContentSha256,
    RigidSurfacePoseSearchParameters SearchParameters,
    SurfaceMatchAcceptancePolicy AcceptancePolicy,
    int MaximumMatchCount,
    int MaximumExpandedCandidateCount,
    int EvaluatedCandidateCount,
    string StopReason,
    SurfaceMatchCollectionItem[] Items,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "stable-identified-disjoint-surface-match-collection-v1";
    public const string CollectionIdPrefix = "collection.surface-match.";

    public static SurfaceMatchCollectionArtifact Create(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidSurfacePoseSearchParameters searchParameters,
        SurfaceMatchAcceptancePolicy acceptancePolicy,
        int maximumMatchCount,
        int maximumExpandedCandidateCount,
        int evaluatedCandidateCount,
        string stopReason,
        IReadOnlyList<SurfaceMatchCollectionItem> items)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(searchParameters);
        ArgumentNullException.ThrowIfNull(acceptancePolicy);
        ArgumentNullException.ThrowIfNull(items);
        var artifact = new SurfaceMatchCollectionArtifact(
            CurrentSchemaVersion,
            CurrentSemantics,
            string.Empty,
            model.ContentSha256,
            scene.ContentSha256,
            searchParameters,
            acceptancePolicy,
            maximumMatchCount,
            maximumExpandedCandidateCount,
            evaluatedCandidateCount,
            stopReason ?? string.Empty,
            items.ToArray(),
            string.Empty);
        var contentSha256 = CalculateContentSha256(artifact);
        artifact = artifact with
        {
            CollectionId = CollectionIdPrefix + contentSha256,
            ContentSha256 = contentSha256
        };
        var validity = SurfaceMatchCollectionArtifactValidator.Inspect(
            artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface match collection validation failed: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        SurfaceMatchCollectionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.SearchParameters);
        ArgumentNullException.ThrowIfNull(artifact.AcceptancePolicy);
        ArgumentNullException.ThrowIfNull(artifact.Items);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write(artifact.SchemaVersion ?? string.Empty);
            writer.Write(artifact.Semantics ?? string.Empty);
            writer.Write(artifact.ModelContentSha256 ?? string.Empty);
            writer.Write(artifact.SceneContentSha256 ?? string.Empty);
            WriteSearch(writer, artifact.SearchParameters);
            writer.Write(artifact.AcceptancePolicy.ContentSha256 ?? string.Empty);
            writer.Write(artifact.MaximumMatchCount);
            writer.Write(artifact.MaximumExpandedCandidateCount);
            writer.Write(artifact.EvaluatedCandidateCount);
            writer.Write(artifact.StopReason ?? string.Empty);
            writer.Write(artifact.Items.Length);
            foreach (var item in artifact.Items)
            {
                writer.Write(item.Order);
                writer.Write(item.MatchId ?? string.Empty);
                writer.Write(item.Execution?.ContentSha256 ?? string.Empty);
                writer.Write(item.Assessment?.ContentSha256 ?? string.Empty);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteSearch(
        BinaryWriter writer,
        RigidSurfacePoseSearchParameters value)
    {
        writer.Write(value.MinimumRotationXDegrees);
        writer.Write(value.MaximumRotationXDegrees);
        writer.Write(value.RotationStepXDegrees);
        writer.Write(value.MinimumRotationYDegrees);
        writer.Write(value.MaximumRotationYDegrees);
        writer.Write(value.RotationStepYDegrees);
        writer.Write(value.MinimumRotationZDegrees);
        writer.Write(value.MaximumRotationZDegrees);
        writer.Write(value.RotationStepZDegrees);
        writer.Write(value.MinimumTranslationX);
        writer.Write(value.MaximumTranslationX);
        writer.Write(value.MinimumTranslationY);
        writer.Write(value.MaximumTranslationY);
        writer.Write(value.MinimumTranslationZ);
        writer.Write(value.MaximumTranslationZ);
        writer.Write(value.MaximumCorrespondenceDistance);
        writer.Write(value.MinimumMatchedSampleCount);
        writer.Write(value.MaximumCandidateCount);
    }
}

public sealed record SurfaceMatchCollectionValidityReport(
    string SchemaVersion,
    bool IsValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";
}

public static class SurfaceMatchCollectionArtifactValidator
{
    public static SurfaceMatchCollectionValidityReport Inspect(
        SurfaceMatchCollectionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        if (artifact.SchemaVersion
                != SurfaceMatchCollectionArtifact.CurrentSchemaVersion
            || artifact.Semantics
                != SurfaceMatchCollectionArtifact.CurrentSemantics)
        {
            errors.Add("Surface match collection schema or semantics are unsupported.");
        }

        if (!CanonicalSha256(artifact.ModelContentSha256)
            || !CanonicalSha256(artifact.SceneContentSha256))
        {
            errors.Add("Surface match collection requires canonical model and scene identities.");
        }

        if (artifact.SearchParameters is null
            || !RigidSurfacePoseSearchParameterValidator
                .Inspect(artifact.SearchParameters).IsValid)
        {
            errors.Add("Surface match collection search parameters are invalid.");
        }

        if (artifact.AcceptancePolicy is null
            || artifact.AcceptancePolicy.SchemaVersion
                != SurfaceMatchAcceptancePolicy.CurrentSchemaVersion
            || artifact.AcceptancePolicy.Semantics
                != SurfaceMatchAcceptancePolicy.CurrentSemantics
            || !double.IsFinite(
                artifact.AcceptancePolicy.MinimumCoverageRatio)
            || artifact.AcceptancePolicy.MinimumCoverageRatio < 0.0
            || artifact.AcceptancePolicy.MinimumCoverageRatio > 1.0
            || !double.IsFinite(
                artifact.AcceptancePolicy.MaximumInlierRmse)
            || artifact.AcceptancePolicy.MaximumInlierRmse < 0.0
            || !string.Equals(
                artifact.AcceptancePolicy.ContentSha256,
                SurfaceMatchAcceptancePolicy.CalculateContentSha256(
                    artifact.AcceptancePolicy),
                StringComparison.Ordinal))
        {
            errors.Add("Surface match collection acceptance policy is invalid.");
        }

        if (artifact.MaximumMatchCount <= 0
            || artifact.MaximumExpandedCandidateCount <= 0
            || artifact.EvaluatedCandidateCount < 0)
        {
            errors.Add("Surface match collection limits or candidate count are invalid.");
        }

        var items = artifact.Items ?? [];
        if (items.Length > artifact.MaximumMatchCount)
        {
            errors.Add("Surface match collection exceeds its authored maximum match count.");
        }

        var matchIds = new HashSet<string>(StringComparer.Ordinal);
        var claimedSceneSamples = new HashSet<int>();
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item is null
                || item.Order != index
                || item.Execution is null
                || item.Assessment is null)
            {
                errors.Add($"Surface match collection item {index} is incomplete or out of order.");
                continue;
            }

            if (!SurfaceMatchExecutionArtifactValidator
                    .Inspect(item.Execution).IsValid
                || item.Execution.PoseResult.State
                    != RigidSurfacePoseSearchState.Matched
                || item.Execution.ModelContentSha256
                    != artifact.ModelContentSha256
                || item.Execution.SceneContentSha256
                    != artifact.SceneContentSha256)
            {
                errors.Add($"Surface match collection item {index} has invalid execution linkage.");
            }

            if (!SurfaceMatchAssessmentArtifactValidator
                    .Inspect(item.Assessment).IsValid
                || item.Assessment.ExecutionContentSha256
                    != item.Execution.ContentSha256
                || item.Assessment.Policy.ContentSha256
                    != artifact.AcceptancePolicy?.ContentSha256)
            {
                errors.Add($"Surface match collection item {index} has invalid assessment linkage.");
            }

            var expectedMatchId =
                SurfaceMatchCollectionItem.MatchIdFor(item.Execution);
            if (item.MatchId != expectedMatchId
                || !matchIds.Add(item.MatchId))
            {
                errors.Add($"Surface match collection item {index} has an invalid or duplicate stable ID.");
            }

            foreach (var match in item.Execution.PoseResult.Coverage.Matches)
            {
                if (!claimedSceneSamples.Add(match.SceneSampleOrder))
                {
                    errors.Add(
                        $"Surface match collection item {index} shares scene sample {match.SceneSampleOrder} with another result.");
                }
            }
        }

        try
        {
            var expectedContent =
                SurfaceMatchCollectionArtifact.CalculateContentSha256(
                    artifact);
            if (artifact.ContentSha256 != expectedContent
                || artifact.CollectionId
                    != SurfaceMatchCollectionArtifact.CollectionIdPrefix
                       + expectedContent)
            {
                errors.Add("Surface match collection content or collection identity is invalid.");
            }
        }
        catch
        {
            errors.Add("Surface match collection identity could not be calculated.");
        }

        var evidence =
            $"items={items.Length};uniqueMatches={matchIds.Count};"
            + $"claimedSceneSamples={claimedSceneSamples.Count};"
            + $"identity={artifact.ContentSha256};selection=excluded";
        return new SurfaceMatchCollectionValidityReport(
            SurfaceMatchCollectionValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            errors,
            evidence);
    }

    private static bool CanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
