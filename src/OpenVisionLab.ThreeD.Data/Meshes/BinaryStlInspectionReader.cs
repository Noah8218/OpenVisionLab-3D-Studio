using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace OpenVisionLab.ThreeD.Data;

public readonly record struct BinaryStlTriangle(
    Vector3 StoredNormal,
    Vector3 A,
    Vector3 B,
    Vector3 C,
    ushort AttributeByteCount);

public sealed record BinaryStlInspectionSummary(
    string SourcePath,
    long SourceByteLength,
    string SourceSha256,
    uint DeclaredTriangleCount,
    long ProcessedTriangleCount,
    long ExpandedVertexCount,
    Vector3 BoundsMinimum,
    Vector3 BoundsMaximum);

public static class BinaryStlInspectionReader
{
    private const int HeaderByteCount = 84;
    private const int TriangleByteCount = 50;
    private const int TrianglesPerBuffer = 8192;

    public static BinaryStlInspectionSummary Scan(
        string path,
        Action<long, BinaryStlTriangle>? visitTriangle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.SequentialScan);
        if (stream.Length < HeaderByteCount)
        {
            throw new InvalidDataException($"Binary STL file is too small: {fullPath}");
        }

        Span<byte> header = stackalloc byte[HeaderByteCount];
        stream.ReadExactly(header);
        var triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(header[80..]);
        var expectedLength = HeaderByteCount + triangleCount * (long)TriangleByteCount;
        if (triangleCount == 0 || expectedLength != stream.Length)
        {
            throw new InvalidDataException(
                $"Binary STL length does not match its declared triangle count: {fullPath}");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(header);

        var buffer = new byte[TriangleByteCount * TrianglesPerBuffer];
        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        long processedTriangleCount = 0;

        while (processedTriangleCount < triangleCount)
        {
            var remainingTriangles = triangleCount - processedTriangleCount;
            var bufferedTriangles = (int)Math.Min(remainingTriangles, TrianglesPerBuffer);
            var bufferedBytes = checked(bufferedTriangles * TriangleByteCount);
            var bytes = buffer.AsSpan(0, bufferedBytes);
            stream.ReadExactly(bytes);
            hash.AppendData(bytes);

            for (var bufferedTriangle = 0; bufferedTriangle < bufferedTriangles; bufferedTriangle++)
            {
                var record = bytes.Slice(bufferedTriangle * TriangleByteCount, TriangleByteCount);
                var triangle = new BinaryStlTriangle(
                    ReadVector(record, 0),
                    ReadVector(record, 12),
                    ReadVector(record, 24),
                    ReadVector(record, 36),
                    BinaryPrimitives.ReadUInt16LittleEndian(record[48..]));
                ValidateFinite(triangle, fullPath, processedTriangleCount);

                Include(ref minimum, ref maximum, triangle.A);
                Include(ref minimum, ref maximum, triangle.B);
                Include(ref minimum, ref maximum, triangle.C);
                visitTriangle?.Invoke(processedTriangleCount, triangle);
                processedTriangleCount++;
            }
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException($"Binary STL contains unread trailing data: {fullPath}");
        }

        return new BinaryStlInspectionSummary(
            fullPath,
            stream.Length,
            Convert.ToHexString(hash.GetHashAndReset()),
            triangleCount,
            processedTriangleCount,
            checked(processedTriangleCount * 3),
            minimum,
            maximum);
    }

    private static Vector3 ReadVector(ReadOnlySpan<byte> record, int offset) =>
        new(ReadSingle(record, offset), ReadSingle(record, offset + 4), ReadSingle(record, offset + 8));

    private static float ReadSingle(ReadOnlySpan<byte> bytes, int offset) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]));

    private static void ValidateFinite(BinaryStlTriangle triangle, string path, long triangleIndex)
    {
        if (IsFinite(triangle.StoredNormal)
            && IsFinite(triangle.A)
            && IsFinite(triangle.B)
            && IsFinite(triangle.C))
        {
            return;
        }

        throw new InvalidDataException(
            $"Binary STL triangle {triangleIndex} contains a non-finite value: {path}");
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void Include(ref Vector3 minimum, ref Vector3 maximum, Vector3 value)
    {
        minimum = Vector3.Min(minimum, value);
        maximum = Vector3.Max(maximum, value);
    }
}
