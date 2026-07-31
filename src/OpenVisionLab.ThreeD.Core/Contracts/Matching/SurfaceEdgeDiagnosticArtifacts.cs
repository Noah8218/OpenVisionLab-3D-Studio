using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public sealed record SurfaceEdgeModelDiagnosticSegment(
    int ModelEdgeOrder,
    SurfaceModelPoint3 FirstPosition,
    SurfaceModelPoint3 SecondPosition,
    SurfaceModelPoint3 Anchor,
    SurfaceModelPoint3 EdgeDirection,
    SurfaceModelPoint3 DeclaredNormal,
    ModelSurfaceEdgeKind Kind,
    bool IsMatched,
    int? SceneEdgeOrder);

public sealed record SurfaceEdgeSceneDiagnosticSegment(
    int SceneEdgeOrder,
    SurfaceModelPoint3 FirstPosition,
    SurfaceModelPoint3 SecondPosition,
    SurfaceModelPoint3 Anchor,
    SurfaceModelPoint3 EdgeDirection,
    SceneSurfaceEdgeAxis Axis,
    bool IsMatched,
    int? ModelEdgeOrder);

/// <summary>
/// Identified display-only edge and declared-normal geometry in the matched
/// scene frame. Acquisition/viewpoint direction is deliberately absent.
/// </summary>
public sealed record SurfaceEdgeDiagnosticOverlayArtifact(
    string SchemaVersion,
    string Semantics,
    string SurfaceMatchExecutionContentSha256,
    string ModelContentSha256,
    string SceneContentSha256,
    string ModelEdgeContentSha256,
    string SceneEdgeContentSha256,
    string ScoreContentSha256,
    string Unit,
    string TargetFrameId,
    SurfaceEdgeModelDiagnosticSegment[] ModelSegments,
    SurfaceEdgeSceneDiagnosticSegment[] SceneSegments,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "matched-model-edge-direction-and-declared-normal-diagnostic-v1";

    public static string CalculateContentSha256(
        SurfaceEdgeDiagnosticOverlayArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.ModelSegments);
        ArgumentNullException.ThrowIfNull(artifact.SceneSegments);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.SurfaceEdgeDiagnosticOverlayArtifact");
            WriteText(writer, artifact.SchemaVersion);
            WriteText(writer, artifact.Semantics);
            WriteText(writer, artifact.SurfaceMatchExecutionContentSha256);
            WriteText(writer, artifact.ModelContentSha256);
            WriteText(writer, artifact.SceneContentSha256);
            WriteText(writer, artifact.ModelEdgeContentSha256);
            WriteText(writer, artifact.SceneEdgeContentSha256);
            WriteText(writer, artifact.ScoreContentSha256);
            WriteText(writer, artifact.Unit);
            WriteText(writer, artifact.TargetFrameId);
            writer.Write(artifact.ModelSegments.Length);
            foreach (var segment in artifact.ModelSegments)
            {
                writer.Write(segment.ModelEdgeOrder);
                WritePoint(writer, segment.FirstPosition);
                WritePoint(writer, segment.SecondPosition);
                WritePoint(writer, segment.Anchor);
                WritePoint(writer, segment.EdgeDirection);
                WritePoint(writer, segment.DeclaredNormal);
                writer.Write((int)segment.Kind);
                writer.Write(segment.IsMatched);
                WriteNullable(writer, segment.SceneEdgeOrder);
            }

            writer.Write(artifact.SceneSegments.Length);
            foreach (var segment in artifact.SceneSegments)
            {
                writer.Write(segment.SceneEdgeOrder);
                WritePoint(writer, segment.FirstPosition);
                WritePoint(writer, segment.SecondPosition);
                WritePoint(writer, segment.Anchor);
                WritePoint(writer, segment.EdgeDirection);
                writer.Write((int)segment.Axis);
                writer.Write(segment.IsMatched);
                WriteNullable(writer, segment.ModelEdgeOrder);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteText(BinaryWriter writer, string? value) =>
        writer.Write(value ?? string.Empty);

    private static void WritePoint(
        BinaryWriter writer,
        SurfaceModelPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        writer.Write(point.X);
        writer.Write(point.Y);
        writer.Write(point.Z);
    }

    private static void WriteNullable(BinaryWriter writer, int? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }
}

public sealed record SurfaceEdgeDiagnosticOverlayValidityReport(
    string SchemaVersion,
    bool IsValid,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";
}

public static class SurfaceEdgeDiagnosticOverlayArtifactValidator
{
    public static SurfaceEdgeDiagnosticOverlayValidityReport Inspect(
        SurfaceEdgeDiagnosticOverlayArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        var modelSegments = artifact.ModelSegments ?? [];
        var sceneSegments = artifact.SceneSegments ?? [];
        if (artifact.SchemaVersion
                != SurfaceEdgeDiagnosticOverlayArtifact.CurrentSchemaVersion
            || artifact.Semantics
                != SurfaceEdgeDiagnosticOverlayArtifact.CurrentSemantics)
        {
            errors.Add("Surface-edge diagnostic overlay schema or semantics are unsupported.");
        }

        foreach (var identity in new[]
                 {
                     artifact.SurfaceMatchExecutionContentSha256,
                     artifact.ModelContentSha256,
                     artifact.SceneContentSha256,
                     artifact.ModelEdgeContentSha256,
                     artifact.SceneEdgeContentSha256,
                     artifact.ScoreContentSha256
                 })
        {
            if (!IsCanonicalSha256(identity))
            {
                errors.Add("Surface-edge diagnostic overlay requires canonical linked SHA-256 identities.");
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(artifact.Unit)
            || string.IsNullOrWhiteSpace(artifact.TargetFrameId))
        {
            errors.Add("Surface-edge diagnostic overlay requires explicit unit and target frame.");
        }

        if (!modelSegments.Select(segment => segment.ModelEdgeOrder)
                .SequenceEqual(Enumerable.Range(0, modelSegments.Length))
            || !sceneSegments.Select(segment => segment.SceneEdgeOrder)
                .SequenceEqual(Enumerable.Range(0, sceneSegments.Length)))
        {
            errors.Add("Surface-edge diagnostic segments require canonical contiguous order.");
        }

        var modelMatches = modelSegments
            .Where(segment => segment.IsMatched)
            .ToArray();
        var sceneMatches = sceneSegments
            .Where(segment => segment.IsMatched)
            .ToArray();
        if (modelMatches.Any(segment => !segment.SceneEdgeOrder.HasValue)
            || modelSegments.Any(segment => !segment.IsMatched && segment.SceneEdgeOrder.HasValue)
            || sceneMatches.Any(segment => !segment.ModelEdgeOrder.HasValue)
            || sceneSegments.Any(segment => !segment.IsMatched && segment.ModelEdgeOrder.HasValue)
            || modelMatches.Length != sceneMatches.Length
            || modelMatches.Any(model => !sceneMatches.Any(scene =>
                scene.SceneEdgeOrder == model.SceneEdgeOrder
                && scene.ModelEdgeOrder == model.ModelEdgeOrder)))
        {
            errors.Add("Surface-edge diagnostic match links are incomplete or inconsistent.");
        }

        if (modelSegments.Any(segment =>
                !Enum.IsDefined(segment.Kind)
                || !Finite(segment.FirstPosition)
                || !Finite(segment.SecondPosition)
                || !Finite(segment.Anchor)
                || !Unit(segment.EdgeDirection)
                || !Unit(segment.DeclaredNormal))
            || sceneSegments.Any(segment =>
                !Enum.IsDefined(segment.Axis)
                || !Finite(segment.FirstPosition)
                || !Finite(segment.SecondPosition)
                || !Finite(segment.Anchor)
                || !Unit(segment.EdgeDirection)))
        {
            errors.Add("Surface-edge diagnostic geometry must be finite with unit directions.");
        }

        var identityValid = false;
        try
        {
            identityValid = string.Equals(
                artifact.ContentSha256,
                SurfaceEdgeDiagnosticOverlayArtifact
                    .CalculateContentSha256(artifact),
                StringComparison.Ordinal);
        }
        catch
        {
            identityValid = false;
        }

        if (!identityValid)
        {
            errors.Add("Surface-edge diagnostic overlay content identity is invalid.");
        }

        return new SurfaceEdgeDiagnosticOverlayValidityReport(
            SurfaceEdgeDiagnosticOverlayValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            identityValid,
            errors,
            $"model={modelSegments.Length};scene={sceneSegments.Length};matched={modelMatches.Length};identity={identityValid};acquisitionDirection=unavailable");
    }

    private static bool IsCanonicalSha256(string? value) =>
        value?.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static bool Finite(SurfaceModelPoint3? point) =>
        point is not null
        && double.IsFinite(point.X)
        && double.IsFinite(point.Y)
        && double.IsFinite(point.Z);

    private static bool Unit(SurfaceModelPoint3? point)
    {
        if (!Finite(point))
        {
            return false;
        }

        var length = Math.Sqrt(point!.X * point.X + point.Y * point.Y + point.Z * point.Z);
        return Math.Abs(length - 1.0) <= 1e-9;
    }
}
