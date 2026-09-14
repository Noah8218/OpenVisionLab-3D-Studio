using System.Buffers.Binary;
using System.Security.Cryptography;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Owns the C3D wire format and the float32 persistence boundary used by
/// height-field snapshots. Domain identity and derivation stay on the
/// snapshot; this type only translates bytes and persisted scalar values.
/// </summary>
internal static class C3DHeightFieldBinaryCodec
{
    public static (long ByteLength, string ContentSha256, int Width, int Height, double[] Values) ParseAndHash(
        string fullPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        var byteLength = stream.Length;
        var layout = C3DSourceTopology.ReadAndValidate(stream);
        cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            var remainingBytes = checked((layout.SampleCount - index) * sizeof(float));
            var bytesToRead = remainingBytes < buffer.Length
                ? remainingBytes
                : buffer.Length;
            stream.ReadExactly(buffer.AsSpan(0, bytesToRead));
            hash.AppendData(buffer.AsSpan(0, bytesToRead));
            for (var offset = 0; offset < bytesToRead; offset += sizeof(float))
            {
                if ((index & 0x3fff) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var bits = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset));
                var value = BitConverter.Int32BitsToSingle(bits);
                values[index++] = float.IsFinite(value) && value != 0.0f
                    ? value
                    : double.NaN;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return (
            byteLength,
            Convert.ToHexString(hash.GetHashAndReset()),
            layout.Width,
            layout.Height,
            values);
    }

    public static double[] NormalizeDerivedValues(IReadOnlyList<double> values)
    {
        var normalized = new double[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (double.IsNaN(value))
            {
                normalized[index] = double.NaN;
                continue;
            }

            if (double.IsInfinity(value))
            {
                throw new InvalidDataException(
                    $"Derived C3D sample {index} is infinite and cannot be represented by the float32 C3D format.");
            }

            float persistedValue;
            try
            {
                persistedValue = checked((float)value);
            }
            catch (OverflowException exception)
            {
                throw new InvalidDataException(
                    $"Derived C3D sample {index} overflows the float32 C3D format.",
                    exception);
            }

            if (!float.IsFinite(persistedValue))
            {
                throw new InvalidDataException(
                    $"Derived C3D sample {index} overflows the float32 C3D format.");
            }

            if (persistedValue == 0.0f)
            {
                throw new InvalidDataException(
                    $"Derived C3D sample {index} underflows to the zero value reserved for missing data.");
            }

            normalized[index] = persistedValue;
        }

        return normalized;
    }

    public static byte[] Encode(int width, int height, IReadOnlyList<double> values)
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
