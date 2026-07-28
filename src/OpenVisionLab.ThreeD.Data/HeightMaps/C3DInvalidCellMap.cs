using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Immutable, coordinate-true missing-cell map for one native C3D grid.
/// Bit index is row * width + column. One means missing and bits are packed
/// least-significant-bit first without flipping, sampling, or interpolation.
/// </summary>
public sealed class C3DInvalidCellMap
{
    public const string ContractVersion = "1.0";
    public const string Encoding =
        "row-major-bitset;1=missing;lsb-first;identity=prefix+version+width+height+byteLength+bytes";

    private const string IdentityPrefix =
        "OpenVisionLab.SourceQualityInvalidCellMask";

    private readonly byte[] packedBits;

    private C3DInvalidCellMap(
        int width,
        int height,
        int missingCellCount,
        byte[] packedBits,
        string sha256)
    {
        Width = width;
        Height = height;
        CellCount = checked(width * height);
        MissingCellCount = missingCellCount;
        this.packedBits = packedBits;
        Sha256 = sha256;
    }

    public int Width { get; }
    public int Height { get; }
    public int CellCount { get; }
    public int MissingCellCount { get; }
    public int PackedByteLength => packedBits.Length;
    public ReadOnlyMemory<byte> PackedBits => packedBits;
    public string Sha256 { get; }

    public SourceQualityInvalidCellMaskIdentity Identity =>
        new(ContractVersion, Encoding, PackedByteLength, Sha256);

    public static C3DInvalidCellMap Create(C3DHeightFieldSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var expectedCellCount = checked(source.Width * source.Height);
        if (source.Values.Length != expectedCellCount)
        {
            throw new InvalidDataException(
                "C3D invalid-cell mapping requires one source value for every native grid cell.");
        }

        var packedBits = new byte[(expectedCellCount + 7) / 8];
        var missingCellCount = 0;
        var values = source.Values.Span;
        for (var index = 0; index < values.Length; index++)
        {
            if (double.IsFinite(values[index]))
            {
                continue;
            }

            packedBits[index / 8] |= (byte)(1 << (index % 8));
            missingCellCount++;
        }

        if (missingCellCount != source.MissingCount)
        {
            throw new InvalidDataException(
                $"C3D invalid-cell count mismatch: snapshot={source.MissingCount}, map={missingCellCount}.");
        }

        return new C3DInvalidCellMap(
            source.Width,
            source.Height,
            missingCellCount,
            packedBits,
            CalculateSha256(source.Width, source.Height, packedBits));
    }

    public bool TryIsMissing(int column, int row, out bool isMissing)
    {
        if (column < 0 || column >= Width || row < 0 || row >= Height)
        {
            isMissing = false;
            return false;
        }

        isMissing = IsMissingIndex(checked(row * Width + column));
        return true;
    }

    internal bool IsMissingIndex(int index) =>
        (packedBits[index / 8] & (1 << (index % 8))) != 0;

    private static string CalculateSha256(
        int width,
        int height,
        ReadOnlySpan<byte> packedBits)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(
            stream,
            System.Text.Encoding.UTF8,
            leaveOpen: true);
        writer.Write(IdentityPrefix);
        writer.Write(ContractVersion);
        writer.Write(width);
        writer.Write(height);
        writer.Write(packedBits.Length);
        writer.Write(packedBits);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
