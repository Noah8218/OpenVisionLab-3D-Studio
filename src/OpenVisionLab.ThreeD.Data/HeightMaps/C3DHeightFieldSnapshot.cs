using System.Buffers.Binary;
using System.Security.Cryptography;
using OpenVisionLab.Vision3D.FeatureExtraction;

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
        int gridOriginColumn,
        int gridOriginRow,
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
        GridOriginColumn = gridOriginColumn;
        GridOriginRow = gridOriginRow;
        this.values = values;
        Provenance = provenance;
        IsDerived = isDerived;

        var summary = new HeightDistributionStatisticsTool().Execute(
            values,
            new HeightDistributionStatisticsOptions
            {
                BinCount = 1,
                ZeroIsMissing = false
            });
        if (!summary.Success)
        {
            throw new InvalidDataException(summary.Message);
        }
        ValidCount = summary.ValidSampleCount;
        MissingCount = summary.MissingSampleCount;
        Minimum = summary.Minimum;
        Maximum = summary.Maximum;
        Mean = summary.Mean;
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
    public int GridOriginColumn { get; }
    public int GridOriginRow { get; }
    public ReadOnlyMemory<double> Values => values;
    internal IReadOnlyList<double> ValueList => values;
    public int ValidCount { get; }
    public int MissingCount { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Mean { get; }
    public string ScalarMeaning => "raw-height";
    public string Provenance { get; }
    public bool IsDerived { get; }

    public static C3DHeightFieldSnapshot LoadIdentified(
        string path,
        string entityId,
        string unit,
        string frameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        var fullPath = Path.GetFullPath(path);
        var (byteLength, hash, width, height, values) = ParseAndHash(fullPath);
        return new C3DHeightFieldSnapshot(
            entityId,
            fullPath,
            unit,
            frameId,
            byteLength,
            hash,
            hash,
            width,
            height,
            0,
            0,
            values,
            $"source:{hash}",
            false);
    }

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
        var (byteLength, hash, width, height, values) = ParseAndHash(fullPath);
        if (byteLength != expectedByteLength
            || !string.Equals(hash, expectedContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("C3D source byte identity does not match the teaching recipe.");
        }

        if (width != expectedWidth || height != expectedHeight)
        {
            throw new InvalidDataException("C3D source grid identity does not match the teaching recipe.");
        }

        return new C3DHeightFieldSnapshot(
            entityId,
            fullPath,
            unit,
            frameId,
            byteLength,
            hash,
            hash,
            width,
            height,
            0,
            0,
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
            0,
            0,
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
            GridOriginColumn,
            GridOriginRow,
            copy,
            provenance,
            true);
    }

    public C3DHeightFieldSnapshot CreateCrop(
        string outputEntityId,
        HeightMapCropResult crop,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(crop);
        if (!crop.Success || crop.Output is null)
        {
            throw new InvalidDataException(crop.Message);
        }

        var output = crop.Output;
        var expectedOriginColumn = checked(GridOriginColumn + crop.SourceRoi.Column);
        var expectedOriginRow = checked(GridOriginRow + crop.SourceRoi.Row);
        if (output.Columns != crop.SourceRoi.ColumnCount
            || output.Rows != crop.SourceRoi.RowCount
            || output.OriginX != expectedOriginColumn
            || output.OriginY != expectedOriginRow
            || output.ColumnPitch != 1d
            || output.RowPitch != 1d
            || !string.Equals(output.PlanarUnit, "grid-index", StringComparison.Ordinal)
            || !string.Equals(output.HeightUnit, Unit, StringComparison.Ordinal)
            || !string.Equals(output.FrameId, FrameId, StringComparison.Ordinal)
            || !string.Equals(output.SourceId, EntityId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The SDK crop output does not preserve the expected source-grid frame, unit, pitch, or identity.");
        }

        var copy = output.CopyValues();
        if (copy.Any(value => double.IsFinite(value) && value == 0.0))
        {
            throw new InvalidDataException(
                "Cropped C3D contains a finite zero that the C3D format reserves for missing data.");
        }

        var bytes = Encode(output.Columns, output.Rows, copy);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return new C3DHeightFieldSnapshot(
            outputEntityId,
            string.Empty,
            Unit,
            FrameId,
            bytes.LongLength,
            hash,
            RootSourceSha256,
            output.Columns,
            output.Rows,
            expectedOriginColumn,
            expectedOriginRow,
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

    private static (long ByteLength, string ContentSha256, int Width, int Height, double[] Values)
        ParseAndHash(string fullPath)
    {
        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        var byteLength = stream.Length;
        var layout = C3DSourceTopology.ReadAndValidate(stream);
        Span<byte> header = stackalloc byte[8];
        stream.Position = 0;
        stream.ReadExactly(header);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(header);
        var values = new double[layout.SampleCount];
        var buffer = new byte[64 * 1024];
        var index = 0;
        while (index < layout.SampleCount)
        {
            var remainingBytes = checked((layout.SampleCount - index) * sizeof(float));
            var bytesToRead = remainingBytes < buffer.Length
                ? remainingBytes
                : buffer.Length;
            stream.ReadExactly(buffer.AsSpan(0, bytesToRead));
            hash.AppendData(buffer.AsSpan(0, bytesToRead));
            for (var offset = 0; offset < bytesToRead; offset += sizeof(float))
            {
                var bits = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset));
                var value = BitConverter.Int32BitsToSingle(bits);
                values[index++] = float.IsFinite(value) && value != 0.0f
                    ? value
                    : double.NaN;
            }
        }

        return (
            byteLength,
            Convert.ToHexString(hash.GetHashAndReset()),
            layout.Width,
            layout.Height,
            values);
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
