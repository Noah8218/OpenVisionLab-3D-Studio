using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum ModelSurfaceEdgeKind
{
    Boundary,
    Crease
}

public enum SceneSurfaceEdgeAxis
{
    AcrossColumns,
    AcrossRows
}

public sealed record ModelSurfaceEdgeExtractionParameters(
    string Method,
    double MinimumEdgeLength,
    double MinimumCreaseAngleDegrees,
    bool IncludeBoundaryEdges)
{
    public const string TopologyBoundaryAndCreaseMethod =
        "mesh-topology-boundary-and-dihedral-v1";
}

public sealed record SceneSurfaceEdgeExtractionParameters(
    string Method,
    double MinimumAbsoluteHeightStep,
    bool IncludeColumnNeighbors,
    bool IncludeRowNeighbors)
{
    public const string OrganizedHeightStepMethod =
        "organized-height-step-higher-endpoint-v1";
}

public sealed record ModelSurfaceEdgeSample(
    int Order,
    int FirstPointIndex,
    int SecondPointIndex,
    SurfaceModelPoint3 FirstPosition,
    SurfaceModelPoint3 SecondPosition,
    SurfaceModelPoint3 Anchor,
    double Length,
    double StrengthDegrees,
    ModelSurfaceEdgeKind Kind);

public sealed record SceneSurfaceEdgeSample(
    int Order,
    int FirstPointIndex,
    int SecondPointIndex,
    int AnchorPointIndex,
    SurfaceModelPoint3 FirstPosition,
    SurfaceModelPoint3 SecondPosition,
    SurfaceModelPoint3 Anchor,
    double AbsoluteHeightStep,
    SceneSurfaceEdgeAxis Axis);

/// <summary>
/// Identified model-edge evidence derived from one immutable SurfaceModel.
/// It stores stable topology locators and geometry but no scene score or
/// acceptance decision.
/// </summary>
public sealed record ModelSurfaceEdgeArtifact(
    string SchemaVersion,
    string Semantics,
    string ArtifactId,
    string ModelContentSha256,
    string Unit,
    string FrameId,
    int SourcePointCount,
    int SourceTriangleCount,
    ModelSurfaceEdgeExtractionParameters Parameters,
    ModelSurfaceEdgeSample[] Edges,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "identified-model-surface-edges-no-score-v1";

    public static ModelSurfaceEdgeArtifact Create(
        SurfaceModelArtifact model,
        ModelSurfaceEdgeExtractionParameters parameters,
        IReadOnlyList<ModelSurfaceEdgeSample> edges)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(edges);

        var artifact = new ModelSurfaceEdgeArtifact(
            CurrentSchemaVersion,
            CurrentSemantics,
            $"edges.model.{model.ArtifactId}",
            model.ContentSha256,
            model.Unit,
            model.FrameId,
            model.Points.Length,
            SurfaceModelSurfaceDomain
                .GetRetainedSourceTriangleIndices(model)
                .Length,
            parameters,
            edges.ToArray(),
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };
        var validity = SurfaceEdgeArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Model surface-edge artifact is invalid: {string.Join(" ", validity.Errors)}");
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        ModelSurfaceEdgeArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.Parameters);
        ArgumentNullException.ThrowIfNull(artifact.Edges);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.ModelSurfaceEdgeArtifact");
            WriteText(writer, artifact.SchemaVersion);
            WriteText(writer, artifact.Semantics);
            WriteText(writer, artifact.ArtifactId);
            WriteText(writer, artifact.ModelContentSha256);
            WriteText(writer, artifact.Unit);
            WriteText(writer, artifact.FrameId);
            writer.Write(artifact.SourcePointCount);
            writer.Write(artifact.SourceTriangleCount);
            WriteText(writer, artifact.Parameters.Method);
            writer.Write(artifact.Parameters.MinimumEdgeLength);
            writer.Write(artifact.Parameters.MinimumCreaseAngleDegrees);
            writer.Write(artifact.Parameters.IncludeBoundaryEdges);
            writer.Write(artifact.Edges.Length);
            foreach (var edge in artifact.Edges)
            {
                writer.Write(edge.Order);
                writer.Write(edge.FirstPointIndex);
                writer.Write(edge.SecondPointIndex);
                WritePoint(writer, edge.FirstPosition);
                WritePoint(writer, edge.SecondPosition);
                WritePoint(writer, edge.Anchor);
                writer.Write(edge.Length);
                writer.Write(edge.StrengthDegrees);
                writer.Write((int)edge.Kind);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    internal static void WriteText(BinaryWriter writer, string? value) =>
        writer.Write(value ?? string.Empty);

    internal static void WritePoint(
        BinaryWriter writer,
        SurfaceModelPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        writer.Write(point.X);
        writer.Write(point.Y);
        writer.Write(point.Z);
    }
}

/// <summary>
/// Identified scene-edge evidence for a complete organized XYZ grid. Version
/// 1 refuses missing/unorganized data rather than guessing adjacency.
/// </summary>
public sealed record SceneSurfaceEdgeArtifact(
    string SchemaVersion,
    string Semantics,
    string ArtifactId,
    string SceneContentSha256,
    string Unit,
    string FrameId,
    int SourcePointCount,
    int SourceWidth,
    int SourceHeight,
    SceneSurfaceEdgeExtractionParameters Parameters,
    SceneSurfaceEdgeSample[] Edges,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "identified-organized-scene-surface-edges-no-score-v1";

    public static SceneSurfaceEdgeArtifact Create(
        PreparedSceneArtifact scene,
        SceneSurfaceEdgeExtractionParameters parameters,
        IReadOnlyList<SceneSurfaceEdgeSample> edges)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(edges);
        var artifact = new SceneSurfaceEdgeArtifact(
            CurrentSchemaVersion,
            CurrentSemantics,
            $"edges.scene.{scene.ArtifactId}",
            scene.ContentSha256,
            scene.Unit,
            scene.FrameId,
            scene.Points.Length,
            scene.SourceQuality.Grid.Width,
            scene.SourceQuality.Grid.Height,
            parameters,
            edges.ToArray(),
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };
        var validity = SurfaceEdgeArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Scene surface-edge artifact is invalid: {string.Join(" ", validity.Errors)}");
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        SceneSurfaceEdgeArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.Parameters);
        ArgumentNullException.ThrowIfNull(artifact.Edges);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.SceneSurfaceEdgeArtifact");
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.SchemaVersion);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.Semantics);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.ArtifactId);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.SceneContentSha256);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.Unit);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.FrameId);
            writer.Write(artifact.SourcePointCount);
            writer.Write(artifact.SourceWidth);
            writer.Write(artifact.SourceHeight);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.Parameters.Method);
            writer.Write(artifact.Parameters.MinimumAbsoluteHeightStep);
            writer.Write(artifact.Parameters.IncludeColumnNeighbors);
            writer.Write(artifact.Parameters.IncludeRowNeighbors);
            writer.Write(artifact.Edges.Length);
            foreach (var edge in artifact.Edges)
            {
                writer.Write(edge.Order);
                writer.Write(edge.FirstPointIndex);
                writer.Write(edge.SecondPointIndex);
                writer.Write(edge.AnchorPointIndex);
                ModelSurfaceEdgeArtifact.WritePoint(writer, edge.FirstPosition);
                ModelSurfaceEdgeArtifact.WritePoint(writer, edge.SecondPosition);
                ModelSurfaceEdgeArtifact.WritePoint(writer, edge.Anchor);
                writer.Write(edge.AbsoluteHeightStep);
                writer.Write((int)edge.Axis);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public sealed record SurfaceEdgeCoverageMatch(
    int ModelEdgeOrder,
    int SceneEdgeOrder,
    double Distance);

public sealed record SurfaceMatchScoreComponent(
    string Semantics,
    string PoseResultContentSha256,
    int ModelSampleCount,
    int SceneSampleCount,
    int MatchedModelSampleCount,
    double CoverageRatio,
    double? InlierRmse,
    double MaximumCorrespondenceDistance);

public sealed record SurfaceEdgeScoreComponent(
    string Semantics,
    int ModelEdgeCount,
    int SceneEdgeCount,
    int MatchedModelEdgeCount,
    int UnmatchedModelEdgeCount,
    double CoverageRatio,
    double? InlierRmse,
    double MaximumCorrespondenceDistance,
    SurfaceEdgeCoverageMatch[] Matches,
    string Evidence)
{
    public const string CurrentSemantics =
        "one-way-model-edge-greedy-unique-nearest-anchor-v1";
}

/// <summary>
/// Identified diagnostic score evidence. Surface and edge channels are kept
/// as separate components; this artifact owns no acceptance threshold and
/// does not rewrite the immutable surface-match execution.
/// </summary>
public sealed record SurfaceAndEdgeMatchScoreArtifact(
    string SchemaVersion,
    string Semantics,
    string SurfaceMatchExecutionContentSha256,
    string ModelEdgeContentSha256,
    string SceneEdgeContentSha256,
    SurfaceMatchScoreComponent SurfaceScore,
    SurfaceEdgeScoreComponent EdgeScore,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "separate-surface-and-edge-scores-no-acceptance-v1";

    public static SurfaceAndEdgeMatchScoreArtifact Create(
        SurfaceMatchExecutionArtifact execution,
        ModelSurfaceEdgeArtifact modelEdges,
        SceneSurfaceEdgeArtifact sceneEdges,
        SurfaceEdgeScoreComponent edgeScore)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(modelEdges);
        ArgumentNullException.ThrowIfNull(sceneEdges);
        ArgumentNullException.ThrowIfNull(edgeScore);
        var coverage = execution.PoseResult.Coverage;
        var artifact = new SurfaceAndEdgeMatchScoreArtifact(
            CurrentSchemaVersion,
            CurrentSemantics,
            execution.ContentSha256,
            modelEdges.ContentSha256,
            sceneEdges.ContentSha256,
            new SurfaceMatchScoreComponent(
                coverage.Semantics,
                execution.PoseResult.ContentSha256,
                coverage.ModelSampleCount,
                coverage.SceneSampleCount,
                coverage.MatchedModelSampleCount,
                coverage.CoverageRatio,
                coverage.InlierRmse,
                coverage.MaximumCorrespondenceDistance),
            edgeScore,
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };
        var validity = SurfaceEdgeArtifactValidator.Inspect(
            artifact,
            execution);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Surface/edge score artifact is invalid: {string.Join(" ", validity.Errors)}");
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        SurfaceAndEdgeMatchScoreArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.SurfaceScore);
        ArgumentNullException.ThrowIfNull(artifact.EdgeScore);
        ArgumentNullException.ThrowIfNull(artifact.EdgeScore.Matches);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.SurfaceAndEdgeMatchScoreArtifact");
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.SchemaVersion);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.Semantics);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.SurfaceMatchExecutionContentSha256);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.ModelEdgeContentSha256);
            ModelSurfaceEdgeArtifact.WriteText(writer, artifact.SceneEdgeContentSha256);
            var surface = artifact.SurfaceScore;
            ModelSurfaceEdgeArtifact.WriteText(writer, surface.Semantics);
            ModelSurfaceEdgeArtifact.WriteText(writer, surface.PoseResultContentSha256);
            writer.Write(surface.ModelSampleCount);
            writer.Write(surface.SceneSampleCount);
            writer.Write(surface.MatchedModelSampleCount);
            writer.Write(surface.CoverageRatio);
            WriteNullable(writer, surface.InlierRmse);
            writer.Write(surface.MaximumCorrespondenceDistance);
            var edge = artifact.EdgeScore;
            ModelSurfaceEdgeArtifact.WriteText(writer, edge.Semantics);
            writer.Write(edge.ModelEdgeCount);
            writer.Write(edge.SceneEdgeCount);
            writer.Write(edge.MatchedModelEdgeCount);
            writer.Write(edge.UnmatchedModelEdgeCount);
            writer.Write(edge.CoverageRatio);
            WriteNullable(writer, edge.InlierRmse);
            writer.Write(edge.MaximumCorrespondenceDistance);
            writer.Write(edge.Matches.Length);
            foreach (var match in edge.Matches)
            {
                writer.Write(match.ModelEdgeOrder);
                writer.Write(match.SceneEdgeOrder);
                writer.Write(match.Distance);
            }
            ModelSurfaceEdgeArtifact.WriteText(writer, edge.Evidence);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteNullable(BinaryWriter writer, double? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }
}
