using System.Globalization;
using System.Numerics;
using System.Text;

namespace OpenVisionLab.ThreeD.Data;

public static class StlMesh
{
    private const int BinaryHeaderBytes = 80;
    private const int BinaryTriangleBytes = 50;
    private const int MaxTriangleCount = 1_000_000;

    public static ImportedMesh Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 15)
        {
            throw new InvalidDataException($"STL file is too small: {path}");
        }

        if (TryLoadBinary(path, bytes, out var binaryMesh))
        {
            return binaryMesh;
        }

        return LoadAscii(path, bytes);
    }

    private static bool TryLoadBinary(string path, byte[] bytes, out ImportedMesh mesh)
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

        if (triangleCount > MaxTriangleCount)
        {
            throw new InvalidDataException($"STL triangle count exceeds the smoke loader limit: {triangleCount:N0}");
        }

        var positions = new Vector3[checked((int)triangleCount * 3)];
        var indices = new int[positions.Length];
        var offset = BinaryHeaderBytes + 4;
        for (var triangle = 0; triangle < triangleCount; triangle++)
        {
            offset += 12;
            for (var vertex = 0; vertex < 3; vertex++)
            {
                var index = checked((int)triangle * 3 + vertex);
                positions[index] = new Vector3(
                    BitConverter.ToSingle(bytes, offset),
                    BitConverter.ToSingle(bytes, offset + 4),
                    BitConverter.ToSingle(bytes, offset + 8));
                indices[index] = index;
                offset += 12;
            }

            offset += 2;
        }

        mesh = ImportedMesh.CreateTriangleMesh(path, Path.GetFileNameWithoutExtension(path), "STL", positions, indices);
        return true;
    }

    private static ImportedMesh LoadAscii(string path, byte[] bytes)
    {
        var vertices = new List<Vector3>();
        using var reader = new StringReader(Encoding.UTF8.GetString(bytes));
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
            {
                throw new InvalidDataException($"STL vertex line must have exactly 3 coordinates: {path}");
            }

            vertices.Add(new Vector3(
                ParseSingle(parts[1], path),
                ParseSingle(parts[2], path),
                ParseSingle(parts[3], path)));
        }

        if (vertices.Count == 0 || vertices.Count % 3 != 0)
        {
            throw new InvalidDataException($"STL ASCII mesh has no complete triangles: {path}");
        }

        var triangleCount = vertices.Count / 3;
        if (triangleCount > MaxTriangleCount)
        {
            throw new InvalidDataException($"STL triangle count exceeds the smoke loader limit: {triangleCount:N0}");
        }

        var positions = vertices.ToArray();
        var indices = Enumerable.Range(0, positions.Length).ToArray();
        return ImportedMesh.CreateTriangleMesh(path, Path.GetFileNameWithoutExtension(path), "STL", positions, indices);
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
