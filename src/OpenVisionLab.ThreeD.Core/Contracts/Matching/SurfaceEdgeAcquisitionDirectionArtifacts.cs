using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum SurfaceEdgeAcquisitionOrientation
{
    SensorFacing,
    AwayFromSensor,
    Grazing
}

public sealed record SurfaceEdgeAcquisitionOrientationItem(
    int ModelEdgeOrder,
    double AlignmentCosine,
    SurfaceEdgeAcquisitionOrientation Orientation);

/// <summary>
/// Display-only orientation evidence linked to one immutable edge diagnostic
/// overlay and one identified source. It never changes matching or acceptance.
/// </summary>
public sealed record SurfaceEdgeAcquisitionDirectionArtifact(
    string SchemaVersion,
    string Semantics,
    string EdgeDiagnosticOverlayContentSha256,
    string SourceContentSha256,
    string FrameId,
    ToolRecipeAcquisitionDirectionConvention DirectionConvention,
    SurfaceModelPoint3 NormalizedSensorToSceneDirection,
    double GrazingAbsoluteCosineMaximum,
    SurfaceEdgeAcquisitionOrientationItem[] Items,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "surface-edge-declared-normal-acquisition-orientation-v1";

    public static string CalculateContentSha256(
        SurfaceEdgeAcquisitionDirectionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.Items);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.SurfaceEdgeAcquisitionDirectionArtifact");
            writer.Write(artifact.SchemaVersion ?? string.Empty);
            writer.Write(artifact.Semantics ?? string.Empty);
            writer.Write(artifact.EdgeDiagnosticOverlayContentSha256 ?? string.Empty);
            writer.Write(artifact.SourceContentSha256 ?? string.Empty);
            writer.Write(artifact.FrameId ?? string.Empty);
            writer.Write((int)artifact.DirectionConvention);
            WritePoint(writer, artifact.NormalizedSensorToSceneDirection);
            writer.Write(artifact.GrazingAbsoluteCosineMaximum);
            writer.Write(artifact.Items.Length);
            foreach (var item in artifact.Items)
            {
                writer.Write(item.ModelEdgeOrder);
                writer.Write(item.AlignmentCosine);
                writer.Write((int)item.Orientation);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WritePoint(BinaryWriter writer, SurfaceModelPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        writer.Write(point.X);
        writer.Write(point.Y);
        writer.Write(point.Z);
    }
}

public sealed record SurfaceEdgeAcquisitionDirectionValidityReport(
    bool IsValid,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence);

public static class SurfaceEdgeAcquisitionDirectionArtifactValidator
{
    public static SurfaceEdgeAcquisitionDirectionValidityReport Inspect(
        SurfaceEdgeAcquisitionDirectionArtifact artifact,
        SurfaceEdgeDiagnosticOverlayArtifact? overlay = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        var items = artifact.Items ?? [];
        if (artifact.SchemaVersion != SurfaceEdgeAcquisitionDirectionArtifact.CurrentSchemaVersion
            || artifact.Semantics != SurfaceEdgeAcquisitionDirectionArtifact.CurrentSemantics)
        {
            errors.Add("Surface-edge acquisition-direction schema or semantics are unsupported.");
        }
        if (!CanonicalSha256(artifact.EdgeDiagnosticOverlayContentSha256)
            || !CanonicalSha256(artifact.SourceContentSha256))
        {
            errors.Add("Surface-edge acquisition direction requires canonical linked SHA-256 identities.");
        }
        if (string.IsNullOrWhiteSpace(artifact.FrameId)
            || artifact.DirectionConvention != ToolRecipeAcquisitionDirectionConvention.SensorToScene)
        {
            errors.Add("Surface-edge acquisition direction requires a source frame and SensorToScene convention.");
        }
        if (!Unit(artifact.NormalizedSensorToSceneDirection)
            || !double.IsFinite(artifact.GrazingAbsoluteCosineMaximum)
            || artifact.GrazingAbsoluteCosineMaximum is < 0.0 or >= 1.0)
        {
            errors.Add("Surface-edge acquisition direction requires a unit direction and a grazing threshold in [0,1).");
        }
        if (!items.Select(item => item.ModelEdgeOrder)
                .SequenceEqual(Enumerable.Range(0, items.Length))
            || items.Any(item => !double.IsFinite(item.AlignmentCosine)
                || item.AlignmentCosine is < -1.000000000001 or > 1.000000000001
                || !Enum.IsDefined(item.Orientation)
                || ExpectedOrientation(
                    item.AlignmentCosine,
                    artifact.GrazingAbsoluteCosineMaximum) != item.Orientation))
        {
            errors.Add("Surface-edge acquisition orientation items are not canonical or consistently classified.");
        }
        if (overlay is not null
            && (!SurfaceEdgeDiagnosticOverlayArtifactValidator.Inspect(overlay).IsValid
                || artifact.EdgeDiagnosticOverlayContentSha256 != overlay.ContentSha256
                || artifact.FrameId != overlay.TargetFrameId
                || items.Length != overlay.ModelSegments.Length))
        {
            errors.Add("Surface-edge acquisition direction is linked to different edge overlay evidence.");
        }

        var identityValid = false;
        try
        {
            identityValid = string.Equals(
                artifact.ContentSha256,
                SurfaceEdgeAcquisitionDirectionArtifact.CalculateContentSha256(artifact),
                StringComparison.Ordinal);
        }
        catch
        {
            identityValid = false;
        }
        if (!identityValid)
        {
            errors.Add("Surface-edge acquisition-direction content identity is invalid.");
        }

        return new SurfaceEdgeAcquisitionDirectionValidityReport(
            errors.Count == 0,
            identityValid,
            errors,
            $"items={items.Length};facing={items.Count(item => item.Orientation == SurfaceEdgeAcquisitionOrientation.SensorFacing)};away={items.Count(item => item.Orientation == SurfaceEdgeAcquisitionOrientation.AwayFromSensor)};grazing={items.Count(item => item.Orientation == SurfaceEdgeAcquisitionOrientation.Grazing)};identity={identityValid}");
    }

    private static SurfaceEdgeAcquisitionOrientation ExpectedOrientation(
        double cosine,
        double grazingMaximum) =>
        Math.Abs(cosine) <= grazingMaximum + 1e-12
            ? SurfaceEdgeAcquisitionOrientation.Grazing
            : cosine < 0.0
                ? SurfaceEdgeAcquisitionOrientation.SensorFacing
                : SurfaceEdgeAcquisitionOrientation.AwayFromSensor;

    private static bool CanonicalSha256(string? value) =>
        value?.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static bool Unit(SurfaceModelPoint3? point)
    {
        if (point is null
            || !double.IsFinite(point.X)
            || !double.IsFinite(point.Y)
            || !double.IsFinite(point.Z))
        {
            return false;
        }
        var length = Math.Sqrt(point.X * point.X + point.Y * point.Y + point.Z * point.Z);
        return Math.Abs(length - 1.0) <= 1e-9;
    }
}
