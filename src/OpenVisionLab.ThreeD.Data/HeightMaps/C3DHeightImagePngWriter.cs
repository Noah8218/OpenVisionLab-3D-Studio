using System.Buffers.Binary;
using System.IO.Compression;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Serializes the exact BGRA display pixels owned by <see cref="C3DHeightImageFrame"/>
/// to a portable PNG artifact. The artifact is a display preview only; the C3D
/// or signed-delta value buffer remains the inspection input and source of truth.
/// </summary>
public static class C3DHeightImagePngWriter
{
    private static readonly byte[] Signature =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
    ];

    private static readonly uint[] CrcTable = CreateCrcTable();

    public static void Write(
        C3DHeightImageFrame frame,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var scanlines = CreateRgbaScanlines(frame, cancellationToken);

        using var stream = File.Create(fullPath);
        stream.Write(Signature);
        WriteHeader(stream, frame.Width, frame.Height);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(scanlines);
        }

        WriteChunk(stream, "IDAT"u8, compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)));
        WriteChunk(stream, "IEND"u8, ReadOnlySpan<byte>.Empty);
        stream.Flush();
    }

    private static byte[] CreateRgbaScanlines(
        C3DHeightImageFrame frame,
        CancellationToken cancellationToken)
    {
        var rowLength = checked(frame.Width * 4);
        var scanlines = new byte[checked((rowLength + 1) * frame.Height)];
        var source = frame.Bgra32Pixels.Span;
        for (var row = 0; row < frame.Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceOffset = checked(row * frame.Stride);
            var targetOffset = checked(row * (rowLength + 1));
            var sourceRow = source.Slice(sourceOffset, rowLength);
            var targetRow = scanlines.AsSpan(targetOffset + 1, rowLength);
            for (var column = 0; column < frame.Width; column++)
            {
                var pixelOffset = column * 4;
                targetRow[pixelOffset] = sourceRow[pixelOffset + 2];
                targetRow[pixelOffset + 1] = sourceRow[pixelOffset + 1];
                targetRow[pixelOffset + 2] = sourceRow[pixelOffset];
                targetRow[pixelOffset + 3] = sourceRow[pixelOffset + 3];
            }
        }

        return scanlines;
    }

    private static void WriteHeader(Stream stream, int width, int height)
    {
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], checked((uint)height));
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(stream, "IHDR"u8, header);
    }

    private static void WriteChunk(
        Stream stream,
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc(type, data));
        stream.Write(crc);
    }

    private static uint ComputeCrc(
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
        {
            crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 0xFF];
        }

        foreach (var value in data)
        {
            crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 0xFF];
        }

        return crc ^ uint.MaxValue;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) == 0
                    ? value >> 1
                    : 0xEDB88320u ^ (value >> 1);
            }

            table[index] = value;
        }

        return table;
    }
}
