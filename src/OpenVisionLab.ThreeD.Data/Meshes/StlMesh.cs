using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace OpenVisionLab.ThreeD.Data;

public static class StlMesh
{
    private const int BinaryHeaderBytes = 80;
    private const int BinaryTriangleBytes = 50;
    private const int MaxTriangleCount = 1_000_000;
    private const long MaximumFileBytes = 512L * 1024 * 1024;

    public static ImportedMesh Load(string path) => Load(path, CancellationToken.None);

    public static ImportedMesh Load(
        string path,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PreflightBinaryTriangleCount(path);
        progress?.Report(8.0);

        var bytes = ReadAllBytes(path, cancellationToken);
        progress?.Report(28.0);
        if (bytes.Length < 15)
        {
            throw new InvalidDataException($"STL file is too small: {path}");
        }

        if (TryLoadBinary(path, bytes, cancellationToken, progress, out var binaryMesh))
        {
            progress?.Report(100.0);
            return binaryMesh;
        }

        var asciiMesh = LoadAscii(path, bytes, cancellationToken, progress);
        progress?.Report(100.0);
        return asciiMesh;
    }

    private static void PreflightBinaryTriangleCount(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"STL file size {stream.Length:N0} bytes exceeds the supported limit {MaximumFileBytes:N0} bytes: {path}");
        }

        if (stream.Length < BinaryHeaderBytes + 4)
        {
            return;
        }

        Span<byte> prefix = stackalloc byte[BinaryHeaderBytes + 4];
        stream.ReadExactly(prefix);
        var triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(prefix[BinaryHeaderBytes..]);
        var expectedLength = BinaryHeaderBytes + 4L + triangleCount * (long)BinaryTriangleBytes;
        if (triangleCount > 0 && expectedLength == stream.Length)
        {
            ValidateTriangleCount(path, triangleCount);
        }
    }

    private static bool TryLoadBinary(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken,
        IProgress<double>? progress,
        out ImportedMesh mesh)
    {
        mesh = null!;
        if (bytes.Length < BinaryHeaderBytes + 4)
        {
            return false;
        }

        var triangleCount = BitConverter.ToUInt32(bytes, BinaryHeaderBytes);
        var expectedLength = BinaryHeaderBytes + 4L + triangleCount * (long)BinaryTriangleBytes;
        if (triangleCount == 0 || expectedLength != bytes.LongLength)
        {
            return false;
        }

        ValidateTriangleCount(path, triangleCount);

        var positions = new Vector3[checked((int)triangleCount * 3)];
        var indices = new int[positions.Length];
        var normals = new Vector3[positions.Length];
        var offset = BinaryHeaderBytes + 4;
        for (var triangle = 0; triangle < triangleCount; triangle++)
        {
            if ((triangle & 4095) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(28.0 + 62.0 * triangle / triangleCount);
            }

            var storedNormal = new Vector3(
                BitConverter.ToSingle(bytes, offset),
                BitConverter.ToSingle(bytes, offset + 4),
                BitConverter.ToSingle(bytes, offset + 8));
            offset += 12;
            for (var vertex = 0; vertex < 3; vertex++)
            {
                var index = checked((int)triangle * 3 + vertex);
                positions[index] = new Vector3(
                    BitConverter.ToSingle(bytes, offset),
                    BitConverter.ToSingle(bytes, offset + 4),
                    BitConverter.ToSingle(bytes, offset + 8));
                indices[index] = index;
                normals[index] = storedNormal;
                offset += 12;
            }

            offset += 2;
        }

        mesh = ImportedMesh.CreateTriangleMesh(
            path,
            Path.GetFileNameWithoutExtension(path),
            "STL",
            positions,
            indices,
            normals);
        return true;
    }

    private static ImportedMesh LoadAscii(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken,
        IProgress<double>? progress)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var normalPresence = new List<bool>();
        Vector3? currentFacetNormal = null;
        using var reader = new StringReader(Encoding.UTF8.GetString(bytes));
        string? line;
        var lineCount = 0;
        var processedCharacters = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            processedCharacters += line.Length + 1;
            if ((lineCount++ & 2047) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(Math.Min(88.0, 28.0 + 60.0 * processedCharacters / Math.Max(1, bytes.Length)));
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith("facet normal ", StringComparison.OrdinalIgnoreCase))
            {
                var normalParts = trimmed.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries);
                if (normalParts.Length != 5)
                {
                    throw new InvalidDataException(
                        $"STL facet normal must have exactly 3 coordinates: {path}");
                }

                currentFacetNormal = new Vector3(
                    ParseSingle(normalParts[2], path),
                    ParseSingle(normalParts[3], path),
                    ParseSingle(normalParts[4], path));
                continue;
            }

            if (trimmed.StartsWith("endfacet", StringComparison.OrdinalIgnoreCase))
            {
                currentFacetNormal = null;
                continue;
            }

            if (!trimmed.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
            {
                throw new InvalidDataException($"STL vertex line must have exactly 3 coordinates: {path}");
            }

            if (vertices.Count >= MaxTriangleCount * 3)
            {
                ValidateTriangleCount(path, vertices.Count / 3L + 1);
            }

            vertices.Add(new Vector3(
                ParseSingle(parts[1], path),
                ParseSingle(parts[2], path),
                ParseSingle(parts[3], path)));
            if (currentFacetNormal is { } facetNormal)
            {
                normals.Add(facetNormal);
                normalPresence.Add(true);
            }
            else
            {
                normals.Add(default);
                normalPresence.Add(false);
            }
        }

        if (vertices.Count == 0 || vertices.Count % 3 != 0)
        {
            throw new InvalidDataException($"STL ASCII mesh has no complete triangles: {path}");
        }

        var triangleCount = vertices.Count / 3;
        ValidateTriangleCount(path, triangleCount);

        var positions = vertices.ToArray();
        var indices = Enumerable.Range(0, positions.Length).ToArray();
        var hasAnyDeclaredNormal = normalPresence.Any(present => present);
        var declaredNormals = hasAnyDeclaredNormal
            ? normals.ToArray()
            : Array.Empty<Vector3>();
        var declaredNormalPresence = hasAnyDeclaredNormal
            ? normalPresence.ToArray()
            : Array.Empty<bool>();
        return ImportedMesh.CreateTriangleMesh(
            path,
            Path.GetFileNameWithoutExtension(path),
            "STL",
            positions,
            indices,
            declaredNormals,
            declaredNormalPresence);
    }

    private static byte[] ReadAllBytes(string path, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        var bytes = new byte[checked((int)stream.Length)];
        var offset = 0;
        while (offset < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = stream.Read(bytes, offset, Math.Min(1024 * 1024, bytes.Length - offset));
            if (count == 0)
            {
                throw new EndOfStreamException($"Unexpected end of STL file: {path}");
            }
            offset += count;
        }
        return bytes;
    }

    private static void ValidateTriangleCount(string path, long triangleCount)
    {
        if (triangleCount > MaxTriangleCount)
        {
            throw new InvalidDataException(
                $"STL triangle count {triangleCount:N0} exceeds the supported limit {MaxTriangleCount:N0}: {path}");
        }
    }

    private static float ParseSingle(string value, string path)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && float.IsFinite(parsed))
        {
            return parsed;
        }

        throw new InvalidDataException($"STL vertex coordinate is invalid in {path}: {value}");
    }
}
