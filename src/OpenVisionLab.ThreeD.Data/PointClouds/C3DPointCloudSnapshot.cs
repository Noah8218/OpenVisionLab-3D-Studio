using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Immutable identified XYZ point-cloud data. The canonical payload is used
/// for deterministic content identity; it is not a calibrated measurement
/// container and does not infer transforms, normals, colors, or alignment.
/// </summary>
public sealed class C3DPointCloudSnapshot
{
    private readonly C3DPoint3[] points;

    private C3DPointCloudSnapshot(
        string entityId,
        string sourcePath,
        string sourceFormat,
        string unit,
        string frameId,
        string coordinateConvention,
        long byteLength,
        string contentSha256,
        string rootSourceSha256,
        C3DPoint3[] points,
        string provenance,
        bool isDerived)
    {
        EntityId = entityId;
        SourcePath = sourcePath;
        SourceFormat = sourceFormat;
        Unit = unit;
        FrameId = frameId;
        CoordinateConvention = coordinateConvention;
        ByteLength = byteLength;
        ContentSha256 = contentSha256;
        RootSourceSha256 = rootSourceSha256;
        this.points = points;
        ValidPointCount = points.Length;
        Provenance = provenance;
        IsDerived = isDerived;
    }

    public string EntityId { get; }
    public string SourcePath { get; }
    public string SourceFormat { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string CoordinateConvention { get; }
    public long ByteLength { get; }
    public string ContentSha256 { get; }
    public string RootSourceSha256 { get; }
    public IReadOnlyList<C3DPoint3> Points => points;
    public int ValidPointCount { get; }
    public string Provenance { get; }
    public bool IsDerived { get; }

    public static C3DPointCloudSnapshot CreateForVerification(
        string entityId,
        string sourcePath,
        string sourceFormat,
        string unit,
        string frameId,
        string coordinateConvention,
        IReadOnlyList<C3DPoint3> sourcePoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinateConvention);
        ArgumentNullException.ThrowIfNull(sourcePoints);
        var copy = sourcePoints.ToArray();
        ValidatePoints(copy, requireNonEmpty: true);
        var bytes = Encode(sourceFormat, unit, frameId, coordinateConvention, copy);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return new C3DPointCloudSnapshot(
            entityId,
            sourcePath,
            sourceFormat,
            unit,
            frameId,
            coordinateConvention,
            bytes.LongLength,
            hash,
            hash,
            copy,
            $"verification:{hash}",
            false);
    }

    public C3DPointCloudSnapshot CreateDerived(
        string outputEntityId,
        IReadOnlyList<C3DPoint3> retainedPoints,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(retainedPoints);
        if (string.Equals(outputEntityId, EntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Point-cloud background-filter output identity must differ from the current source.");
        }

        var copy = retainedPoints.ToArray();
        ValidatePoints(copy, requireNonEmpty: false);
        var bytes = Encode(SourceFormat, Unit, FrameId, CoordinateConvention, copy);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return new C3DPointCloudSnapshot(
            outputEntityId,
            string.Empty,
            SourceFormat,
            Unit,
            FrameId,
            CoordinateConvention,
            bytes.LongLength,
            hash,
            RootSourceSha256,
            copy,
            provenance,
            true);
    }

    private static void ValidatePoints(
        IReadOnlyList<C3DPoint3> values,
        bool requireNonEmpty)
    {
        if (requireNonEmpty && values.Count == 0)
        {
            throw new InvalidDataException("Identified point-cloud sources require at least one point.");
        }

        for (var index = 0; index < values.Count; index++)
        {
            if (!values[index].IsFinite)
            {
                throw new InvalidDataException($"Point-cloud point {index} must contain finite XYZ coordinates.");
            }
        }
    }

    private static byte[] Encode(
        string sourceFormat,
        string unit,
        string frameId,
        string coordinateConvention,
        IReadOnlyList<C3DPoint3> values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DPointCloudSnapshot");
        writer.Write("1.0");
        writer.Write(sourceFormat);
        writer.Write(unit);
        writer.Write(frameId);
        writer.Write(coordinateConvention);
        writer.Write(values.Count);
        foreach (var point in values)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
            writer.Write(point.Z);
        }

        writer.Flush();
        return stream.ToArray();
    }
}

public readonly record struct C3DPoint3(double X, double Y, double Z)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}
