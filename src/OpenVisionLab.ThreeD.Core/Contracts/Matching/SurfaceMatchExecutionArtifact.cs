using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Identified, display-only transformed SurfaceModel geometry. Point order and
/// triangle indexes remain identical to the source SurfaceModel; only the
/// point coordinates are mapped into the Prepared Scene frame.
/// </summary>
public sealed record SurfaceMatchOverlayArtifact(
    string SchemaVersion,
    string Semantics,
    string OverlayId,
    string ModelContentSha256,
    string SceneContentSha256,
    string PoseResultContentSha256,
    string Unit,
    string SourceFrameId,
    string TargetFrameId,
    SurfaceModelPoint3[] TransformedPoints,
    SurfaceModelTriangle[] Triangles,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "identified-transformed-surface-model-wireframe-v1";

    public static SurfaceMatchOverlayArtifact Create(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidSurfacePoseSearchResult poseResult)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(poseResult);

        if (!SurfaceModelArtifactValidator.Inspect(model).IsValid
            || !PreparedSceneArtifactValidator.Inspect(scene).IsValid)
        {
            throw new InvalidDataException(
                "Surface match overlay requires valid model and scene artifacts.");
        }

        if (poseResult.State != RigidSurfacePoseSearchState.Matched
            || poseResult.Pose is not { } pose)
        {
            throw new InvalidDataException(
                "Surface match overlay requires an identified matched pose.");
        }

        if (!string.Equals(
                poseResult.ModelContentSha256,
                model.ContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                poseResult.SceneContentSha256,
                scene.ContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                pose.Unit,
                model.Unit,
                StringComparison.Ordinal)
            || !string.Equals(
                pose.SourceFrameId,
                model.FrameId,
                StringComparison.Ordinal)
            || !string.Equals(
                pose.TargetFrameId,
                scene.FrameId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Surface match overlay identities, units, or frames do not match the pose evidence.");
        }

        var artifact = new SurfaceMatchOverlayArtifact(
            CurrentSchemaVersion,
            CurrentSemantics,
            $"overlay.surface-match.{model.ArtifactId}",
            model.ContentSha256,
            scene.ContentSha256,
            poseResult.ContentSha256,
            model.Unit,
            model.FrameId,
            scene.FrameId,
            model.Points
                .Select(pose.TransformPoint)
                .ToArray(),
            model.Triangles.ToArray(),
            string.Empty);
        return artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };
    }

    public static string CalculateContentSha256(
        SurfaceMatchOverlayArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.TransformedPoints);
        ArgumentNullException.ThrowIfNull(artifact.Triangles);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            WriteText(writer, artifact.SchemaVersion);
            WriteText(writer, artifact.Semantics);
            WriteText(writer, artifact.OverlayId);
            WriteText(writer, artifact.ModelContentSha256);
            WriteText(writer, artifact.SceneContentSha256);
            WriteText(writer, artifact.PoseResultContentSha256);
            WriteText(writer, artifact.Unit);
            WriteText(writer, artifact.SourceFrameId);
            WriteText(writer, artifact.TargetFrameId);
            writer.Write(artifact.TransformedPoints.Length);
            foreach (var point in artifact.TransformedPoints)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
                writer.Write(point.Z);
            }

            writer.Write(artifact.Triangles.Length);
            foreach (var triangle in artifact.Triangles)
            {
                writer.Write(triangle.FirstPointIndex);
                writer.Write(triangle.SecondPointIndex);
                writer.Write(triangle.ThirdPointIndex);
            }
        }

        return Convert.ToHexString(
            SHA256.HashData(stream.ToArray()));
    }

    private static void WriteText(
        BinaryWriter writer,
        string? value) =>
        writer.Write(value ?? string.Empty);
}

/// <summary>
/// One deterministic model-to-scene execution result shared by Runner and
/// Workbench. It intentionally contains no Pass/Fail acceptance policy.
/// </summary>
public sealed record SurfaceMatchExecutionArtifact(
    string SchemaVersion,
    string Semantics,
    string ModelContentSha256,
    string SceneContentSha256,
    RigidSurfacePoseSearchResult PoseResult,
    SurfaceMatchOverlayArtifact? Overlay,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "pose-coverage-identified-overlay-no-acceptance-v1";

    public static SurfaceMatchExecutionArtifact Create(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidSurfacePoseSearchResult poseResult)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(poseResult);

        var overlay = poseResult.State == RigidSurfacePoseSearchState.Matched
            ? SurfaceMatchOverlayArtifact.Create(
                model,
                scene,
                poseResult)
            : null;
        var artifact = new SurfaceMatchExecutionArtifact(
            CurrentSchemaVersion,
            CurrentSemantics,
            model.ContentSha256,
            scene.ContentSha256,
            poseResult,
            overlay,
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };
        var validity =
            SurfaceMatchExecutionArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Surface match execution validation failed: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        SurfaceMatchExecutionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.PoseResult);

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
            writer.Write(artifact.PoseResult.ContentSha256 ?? string.Empty);
            writer.Write(artifact.Overlay?.ContentSha256 ?? "(none)");
        }

        return Convert.ToHexString(
            SHA256.HashData(stream.ToArray()));
    }
}
