using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace OpenVisionLab.ThreeD.Data;

public sealed class BinaryPlyVertexReader : IDisposable
{
    private const int DefaultChunkVertexCount = 65_536;
    private const int MaximumHeaderLineBytes = 4096;

    private readonly FileStream stream;
    private readonly byte[] buffer;
    private readonly Dictionary<string, int> propertyIndices;
    private long remainingVertexCount;

    public BinaryPlyVertexReader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        SourcePath = Path.GetFullPath(path);
        stream = new FileStream(
            SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.SequentialScan);

        try
        {
            var header = ReadHeader(stream, SourcePath);
            VertexCount = header.VertexCount;
            Properties = header.Properties;
            RecordByteCount = checked(Properties.Count * sizeof(float));
            HeaderByteCount = stream.Position;
            var expectedLength = checked(HeaderByteCount + VertexCount * (long)RecordByteCount);
            if (stream.Length != expectedLength)
            {
                throw new InvalidDataException(
                    $"Binary PLY length does not match its vertex contract: {SourcePath}");
            }

            propertyIndices = Properties
                .Select((name, index) => (name, index))
                .ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
            buffer = new byte[checked(DefaultChunkVertexCount * RecordByteCount)];
            remainingVertexCount = VertexCount;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public string SourcePath { get; }

    public long VertexCount { get; }

    public IReadOnlyList<string> Properties { get; }

    public int RecordByteCount { get; }

    public long HeaderByteCount { get; }

    public int CurrentChunkVertexCount { get; private set; }

    public bool IsComplete => remainingVertexCount == 0;

    public int ReadChunk(int maximumVertexCount = int.MaxValue)
    {
        if (maximumVertexCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumVertexCount));
        }

        if (remainingVertexCount == 0)
        {
            CurrentChunkVertexCount = 0;
            return 0;
        }

        CurrentChunkVertexCount = (int)Math.Min(
            Math.Min(remainingVertexCount, maximumVertexCount),
            buffer.Length / RecordByteCount);
        var byteCount = checked(CurrentChunkVertexCount * RecordByteCount);
        stream.ReadExactly(buffer.AsSpan(0, byteCount));
        remainingVertexCount -= CurrentChunkVertexCount;
        return CurrentChunkVertexCount;
    }

    public int GetPropertyIndex(string name) =>
        propertyIndices.TryGetValue(name, out var index)
            ? index
            : throw new InvalidDataException($"Binary PLY property is missing: {name} ({SourcePath})");

    public Vector3 GetPosition(int vertexIndex) =>
        new(
            GetSingle(vertexIndex, 0),
            GetSingle(vertexIndex, 1),
            GetSingle(vertexIndex, 2));

    public float GetSingle(int vertexIndex, int propertyIndex)
    {
        ValidateVertexIndex(vertexIndex);
        if ((uint)propertyIndex >= Properties.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(propertyIndex));
        }

        var offset = checked(vertexIndex * RecordByteCount + propertyIndex * sizeof(float));
        return BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, sizeof(float))));
    }

    public bool CoordinatesEqual(BinaryPlyVertexReader other, int vertexIndex)
    {
        ArgumentNullException.ThrowIfNull(other);
        ValidateVertexIndex(vertexIndex);
        other.ValidateVertexIndex(vertexIndex);

        var thisOffset = checked(vertexIndex * RecordByteCount);
        var otherOffset = checked(vertexIndex * other.RecordByteCount);
        return buffer.AsSpan(thisOffset, 3 * sizeof(float))
            .SequenceEqual(other.buffer.AsSpan(otherOffset, 3 * sizeof(float)));
    }

    public void Dispose() => stream.Dispose();

    private void ValidateVertexIndex(int vertexIndex)
    {
        if ((uint)vertexIndex >= CurrentChunkVertexCount)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexIndex));
        }
    }

    private static PlyHeader ReadHeader(FileStream stream, string path)
    {
        if (!ReadLine(stream, path).Equals("ply", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"PLY magic is missing: {path}");
        }

        if (!ReadLine(stream, path).Equals("format binary_little_endian 1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Only binary little-endian PLY 1.0 is supported: {path}");
        }

        long vertexCount = -1;
        var properties = new List<string>();
        string? currentElement = null;
        while (true)
        {
            var line = ReadLine(stream, path);
            if (line.Equals("end_header", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith("element ", StringComparison.Ordinal))
            {
                var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length != 3 || !long.TryParse(fields[2], out var count) || count < 0)
                {
                    throw new InvalidDataException($"Invalid PLY element declaration: {line} ({path})");
                }

                currentElement = fields[1];
                if (currentElement.Equals("vertex", StringComparison.Ordinal))
                {
                    vertexCount = count;
                }
                else if (count != 0)
                {
                    throw new InvalidDataException($"Non-empty PLY element is unsupported: {line} ({path})");
                }

                continue;
            }

            if (currentElement?.Equals("vertex", StringComparison.Ordinal) == true
                && line.StartsWith("property ", StringComparison.Ordinal))
            {
                var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length != 3
                    || (!fields[1].Equals("float", StringComparison.Ordinal)
                        && !fields[1].Equals("float32", StringComparison.Ordinal)))
                {
                    throw new InvalidDataException($"Unsupported PLY vertex property: {line} ({path})");
                }

                properties.Add(fields[2]);
            }
        }

        if (vertexCount <= 0)
        {
            throw new InvalidDataException($"PLY vertex declaration must be positive: {path}");
        }

        if (properties.Count < 3
            || !properties[0].Equals("x", StringComparison.Ordinal)
            || !properties[1].Equals("y", StringComparison.Ordinal)
            || !properties[2].Equals("z", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"PLY vertex properties must start with x,y,z: {path}");
        }

        if (properties.Distinct(StringComparer.Ordinal).Count() != properties.Count)
        {
            throw new InvalidDataException($"PLY vertex properties must be unique: {path}");
        }

        return new PlyHeader(vertexCount, properties.ToArray());
    }

    private static string ReadLine(FileStream stream, string path)
    {
        var bytes = new List<byte>(128);
        while (true)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                throw new InvalidDataException($"PLY header ended before end_header: {path}");
            }

            if (value == '\n')
            {
                return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            }

            if (value > 0x7f || bytes.Count >= MaximumHeaderLineBytes)
            {
                throw new InvalidDataException($"PLY header contains an invalid line: {path}");
            }

            bytes.Add((byte)value);
        }
    }

    private sealed record PlyHeader(long VertexCount, string[] Properties);
}
