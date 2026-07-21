using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable A2 output. Points are source-grid locators transformed into the
/// published affine reference frame; this is not a re-gridded height field.
/// </summary>
public sealed class C3DTransformedPointCloud
{
    public const string ContractVersion = "1.0";
    private readonly C3DTransformedPoint[] points;

    private C3DTransformedPointCloud(
        string outputEntityId,
        C3DAffineTransform3D affineTransform,
        int sourceGridWidth,
        int sourceGridHeight,
        C3DTransformedPoint[] points,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        RootSourceEntityId = affineTransform.RootSourceEntityId;
        RootSourceSha256 = affineTransform.RootSourceSha256;
        SourceContentSha256 = affineTransform.RootSourceSha256;
        SourceUnit = affineTransform.SourceUnit;
        SourceFrameId = affineTransform.SourceFrameId;
        SourceCoordinateConvention = affineTransform.SourceCoordinateConvention;
        AffineTransformEntityId = affineTransform.OutputEntityId;
        AffineTransformContentSha256 = affineTransform.ContentSha256;
        ReferenceFrameId = affineTransform.ReferenceFrameId;
        ReferenceUnit = affineTransform.ReferenceUnit;
        ReferenceProvenance = affineTransform.ReferenceProvenance;
        ReferenceRevision = affineTransform.ReferenceRevision;
        SourceGridWidth = sourceGridWidth;
        SourceGridHeight = sourceGridHeight;
        this.points = points;
        FinitePointCount = points.Length;
        MissingPointCount = checked(sourceGridWidth * sourceGridHeight - points.Length);
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string SourceContentSha256 { get; }
    public string SourceUnit { get; }
    public string SourceFrameId { get; }
    public string SourceCoordinateConvention { get; }
    public string SurfaceFlavor => "raw";
    public string AffineTransformEntityId { get; }
    public string AffineTransformContentSha256 { get; }
    public string ReferenceFrameId { get; }
    public string ReferenceUnit { get; }
    public string ReferenceProvenance { get; }
    public string ReferenceRevision { get; }
    public int SourceGridWidth { get; }
    public int SourceGridHeight { get; }
    public int FinitePointCount { get; }
    public int MissingPointCount { get; }
    public IReadOnlyList<C3DTransformedPoint> Points => points;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DTransformedPointCloud Create(
        string outputEntityId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceUnit,
        string sourceFrameId,
        string sourceCoordinateConvention,
        int sourceGridWidth,
        int sourceGridHeight,
        C3DAffineTransform3D affineTransform,
        IReadOnlyList<C3DTransformedPoint> points,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCoordinateConvention);
        ArgumentNullException.ThrowIfNull(affineTransform);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (sourceGridWidth <= 0 || sourceGridHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceGridWidth), "Source grid dimensions must be positive.");
        }
        if (string.Equals(outputEntityId, sourceEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputEntityId, affineTransform.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("TransformedPointCloud output ID must differ from its raw source and affine transform inputs.");
        }
        if (!string.Equals(sourceEntityId, affineTransform.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(sourceContentSha256, affineTransform.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(sourceUnit, affineTransform.SourceUnit, StringComparison.Ordinal)
            || !string.Equals(sourceFrameId, affineTransform.SourceFrameId, StringComparison.Ordinal)
            || !string.Equals(sourceCoordinateConvention, affineTransform.SourceCoordinateConvention, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Raw source identity/frame/unit/convention does not match the Published AffineTransform3D.");
        }

        var copy = points.ToArray();
        ValidatePoints(copy, sourceGridWidth, sourceGridHeight);
        var hash = CalculateContentSha256(outputEntityId, affineTransform, sourceGridWidth, sourceGridHeight, copy);
        return new C3DTransformedPointCloud(outputEntityId, affineTransform, sourceGridWidth, sourceGridHeight, copy, provenance, hash);
    }

    private static void ValidatePoints(IReadOnlyList<C3DTransformedPoint> points, int width, int height)
    {
        var previous = -1;
        foreach (var point in points)
        {
            if (point.Row < 0 || point.Row >= height || point.Column < 0 || point.Column >= width
                || !double.IsFinite(point.RawHeight) || !double.IsFinite(point.X)
                || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
            {
                throw new InvalidDataException("TransformedPointCloud contains an invalid source locator or non-finite point.");
            }
            var ordinal = checked(point.Row * width + point.Column);
            if (ordinal <= previous)
            {
                throw new InvalidDataException("TransformedPointCloud points must be unique and ordered by source grid locator.");
            }
            previous = ordinal;
        }
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        C3DAffineTransform3D affineTransform,
        int width,
        int height,
        IReadOnlyList<C3DTransformedPoint> points)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DTransformedPointCloud");
        writer.Write(ContractVersion);
        writer.Write(outputEntityId);
        writer.Write(affineTransform.RootSourceEntityId);
        writer.Write(affineTransform.RootSourceSha256.ToUpperInvariant());
        writer.Write(affineTransform.SourceUnit); writer.Write(affineTransform.SourceFrameId);
        writer.Write(affineTransform.SourceCoordinateConvention); writer.Write("raw");
        writer.Write(affineTransform.OutputEntityId); writer.Write(affineTransform.ContentSha256.ToUpperInvariant());
        writer.Write(affineTransform.ReferenceFrameId); writer.Write(affineTransform.ReferenceUnit);
        writer.Write(affineTransform.ReferenceProvenance); writer.Write(affineTransform.ReferenceRevision);
        writer.Write(width); writer.Write(height); writer.Write(points.Count);
        foreach (var point in points)
        {
            writer.Write(point.Row); writer.Write(point.Column); writer.Write(point.RawHeight);
            writer.Write(point.X); writer.Write(point.Y); writer.Write(point.Z);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public readonly record struct C3DTransformedPoint(
    int Row,
    int Column,
    double RawHeight,
    double X,
    double Y,
    double Z);
