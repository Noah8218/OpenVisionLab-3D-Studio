using System.Buffers.Binary;
using System.Security.Cryptography;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Immutable full-resolution C3D raw-height data plus the identity of the
/// exact bytes that were parsed. Zero and non-finite samples are missing.
/// </summary>
public sealed class C3DHeightFieldSnapshot
{
    private readonly double[] values;

    private C3DHeightFieldSnapshot(
        string entityId,
        string sourcePath,
        string unit,
        string frameId,
        long byteLength,
        string contentSha256,
        string rootSourceSha256,
        int width,
        int height,
        double[] values,
        string provenance,
        bool isDerived)
    {
        EntityId = entityId;
        SourcePath = sourcePath;
        Unit = unit;
        FrameId = frameId;
        ByteLength = byteLength;
        ContentSha256 = contentSha256;
        RootSourceSha256 = rootSourceSha256;
        Width = width;
        Height = height;
        this.values = values;
        Provenance = provenance;
        IsDerived = isDerived;

        var valid = values.Where(double.IsFinite).ToArray();
        ValidCount = valid.Length;
        MissingCount = values.Length - valid.Length;
        Minimum = valid.Length == 0 ? double.NaN : valid.Min();
        Maximum = valid.Length == 0 ? double.NaN : valid.Max();
        Mean = valid.Length == 0 ? double.NaN : valid.Average();
    }

    public string EntityId { get; }
    public string SourcePath { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public long ByteLength { get; }
    public string ContentSha256 { get; }
    public string RootSourceSha256 { get; }
    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<double> Values => values;
    public int ValidCount { get; }
    public int MissingCount { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Mean { get; }
    public string ScalarMeaning => "raw-height";
    public string Provenance { get; }
    public bool IsDerived { get; }

    public static C3DHeightFieldSnapshot LoadVerified(
        string path,
        string entityId,
        string unit,
        string frameId,
        long expectedByteLength,
        string expectedContentSha256,
        int expectedWidth,
        int expectedHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var fullPath = Path.GetFullPath(path);
        var bytes = File.ReadAllBytes(fullPath);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (bytes.LongLength != expectedByteLength
            || !string.Equals(hash, expectedContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("C3D source byte identity does not match the teaching recipe.");
        }

        var (width, height, values) = Parse(bytes);
        if (width != expectedWidth || height != expectedHeight)
        {
            throw new InvalidDataException("C3D source grid identity does not match the teaching recipe.");
        }

        return new C3DHeightFieldSnapshot(
            entityId,
            fullPath,
            unit,
            frameId,
            bytes.LongLength,
            hash,
            hash,
            width,
            height,
            values,
            $"source:{hash}",
            false);
    }

    public static C3DHeightFieldSnapshot CreateForVerification(
        string entityId,
        int width,
        int height,
        IReadOnlyList<double> sourceValues,
        string unit = "raw-height",
        string frameId = "frame.c3d-grid-index")
    {
        if (width <= 0 || height <= 0 || sourceValues.Count != checked(width * height))
        {
            throw new ArgumentException("Verification height field dimensions do not match its values.");
        }

        var values = sourceValues
            .Select(value => double.IsFinite(value) && value != 0.0 ? value : double.NaN)
            .ToArray();
        var bytes = Encode(width, height, values);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return new C3DHeightFieldSnapshot(
            entityId,
            string.Empty,
            unit,
            frameId,
            bytes.LongLength,
            hash,
            hash,
            width,
            height,
            values,
            $"verification:{hash}",
            false);
    }

    public C3DHeightFieldSnapshot CreateDerived(string outputEntityId, IReadOnlyList<double> outputValues, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        if (outputValues.Count != values.Length)
        {
            throw new ArgumentException("Derived height field dimensions must match the source.", nameof(outputValues));
        }

        var copy = outputValues.ToArray();
        if (copy.Any(value => double.IsFinite(value) && value == 0.0))
        {
            throw new InvalidDataException(
                "Derived C3D contains a finite zero that the C3D format reserves for missing data; preserving the missing mask requires a controlled error.");
        }
        var bytes = Encode(Width, Height, copy);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return new C3DHeightFieldSnapshot(
            outputEntityId,
            string.Empty,
            Unit,
            FrameId,
            bytes.LongLength,
            hash,
            RootSourceSha256,
            Width,
            Height,
            copy,
            provenance,
            true);
    }

    public void SaveC3D(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        File.WriteAllBytes(fullPath, Encode(Width, Height, values));
    }

    private static (int Width, int Height, double[] Values) Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8)
        {
            throw new InvalidDataException("C3D header is incomplete.");
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        var height = BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("C3D grid dimensions must be positive.");
        }

        var count = checked(width * height);
        var expectedLength = checked(8 + count * sizeof(float));
        if (bytes.Length != expectedLength)
        {
            throw new InvalidDataException("C3D byte length does not match its grid dimensions.");
        }

        var values = new double[count];
        for (var index = 0; index < count; index++)
        {
            var bits = BinaryPrimitives.ReadInt32LittleEndian(bytes[(8 + index * sizeof(float))..]);
            var value = BitConverter.Int32BitsToSingle(bits);
            values[index] = float.IsFinite(value) && value != 0.0f ? value : double.NaN;
        }

        return (width, height, values);
    }

    private static byte[] Encode(int width, int height, IReadOnlyList<double> values)
    {
        var bytes = new byte[checked(8 + values.Count * sizeof(float))];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), height);
        for (var index = 0; index < values.Count; index++)
        {
            var value = double.IsFinite(values[index]) ? checked((float)values[index]) : 0.0f;
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(8 + index * sizeof(float)),
                BitConverter.SingleToInt32Bits(value));
        }

        return bytes;
    }
}
